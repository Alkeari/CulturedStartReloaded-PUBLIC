using System;
using System.Linq;
using System.Reflection;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Engine;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Reflection bridge into the vanilla narrative stage view: the live
    ///     camera and scene layer for the preview controls, and the stage's
    ///     view model for advancing the flow and refreshing the left-side
    ///     stat panel. Every accessor is a safe null when the narrative stage
    ///     is not the active one.
    /// </summary>
    public static class StageViewBridge
    {
        public static object? FindNarrativeStageView()
        {
            var screen = ScreenManager.TopScreen;
            if (screen == null) return null;

            foreach (var field in screen.GetType()
                         .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var value = field.GetValue(screen);
                if (value == null) continue;

                if (value.GetType().Name.Contains("NarrativeStageView"))
                    return value;

                // Stage views may live inside a collection field
                if (value is System.Collections.IEnumerable enumerable and not string)
                {
                    var match = enumerable.Cast<object?>()
                        .FirstOrDefault(o => o?.GetType().Name.Contains("NarrativeStageView") == true);
                    if (match != null) return match;
                }
            }

            return null;
        }

        public static Camera? GetCamera(object stageView)
        {
            return stageView.GetType()
                .GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(stageView) as Camera;
        }

        public static Scene? GetCharacterScene(object stageView)
        {
            return stageView.GetType()
                .GetField("_characterScene", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(stageView) as Scene;
        }

        /// <summary>
        ///     The scene the narrative stage draws in, reached from the character
        ///     creation SCREEN rather than from the stage view.
        ///
        ///     The screen reads <c>character_menu_new</c> once and hands the same
        ///     scene to every stage, so it exists before the first stage view does.
        ///     The view's own <c>_characterScene</c> is still null while the first
        ///     menu's characters are being composed, which is exactly the moment
        ///     the staging marks have to be in it.
        /// </summary>
        public static Scene? GetStageScene()
        {
            try
            {
                var screen = ScreenManager.TopScreen;
                var fromScreen = screen?.GetType()
                    .GetField("_genericScene", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(screen) as Scene;
                if (fromScreen != null) return fromScreen;

                var stageView = FindNarrativeStageView();
                return stageView != null ? GetCharacterScene(stageView) : null;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewBridge: the stage scene could not be reached.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Re-applies the camera to the stage's scene layer. The engine reads
        ///     camera parameters at SetCamera time, so a mutated frame is invisible
        ///     until it is pushed again; vanilla's face generator does the same.
        /// </summary>
        public static void PushCamera(object stageView, Camera camera)
        {
            try
            {
                var layer = stageView.GetType()
                    .GetProperty("CharacterLayer", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(stageView);
                if (layer == null) return;

                layer.GetType().GetMethod("SetCamera", new[] { typeof(Camera) })
                    ?.Invoke(layer, new object[] { camera });
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewBridge: pushing the camera failed.", ex);
            }
        }

        /// <summary>
        ///     Makes the stage view respawn its character visuals on its next tick.
        ///     Refreshing the menu data alone is not enough: the view only rebuilds
        ///     visuals on menu interactions, so popup-driven equipment changes
        ///     would stay invisible until the next click without this.
        /// </summary>
        public static void MarkAgentVisualsDirty()
        {
            try
            {
                var stageView = FindNarrativeStageView();
                stageView?.GetType()
                    .GetField("_isAgentVisualsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(stageView, true);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewBridge: marking visuals dirty failed.", ex);
            }
        }

        public static CharacterCreationNarrativeStageVM? GetStageVM()
        {
            try
            {
                var stageView = FindNarrativeStageView();
                return stageView?.GetType()
                    .GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(stageView) as CharacterCreationNarrativeStageVM;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewBridge: stage view model lookup failed.", ex);
                return null;
            }
        }

        /// <summary>Advances exactly as the vanilla Next button would.</summary>
        public static void AdvanceStage()
        {
            var vm = GetStageVM();
            if (vm == null)
            {
                CSLogger.Warn("StageViewBridge: cannot advance, no narrative stage view model.");
                return;
            }

            vm.OnNextStage();
        }

        /// <summary>
        ///     Recomputes the vanilla left-side attribute and skill panel so it
        ///     reflects Start Editor changes the moment they are made.
        /// </summary>
        public static void RefreshGainedProperties()
        {
            try
            {
                GetStageVM()?.GainedPropertiesController?.UpdateValues();
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewBridge: stat panel refresh failed.", ex);
            }
        }
    }
}
