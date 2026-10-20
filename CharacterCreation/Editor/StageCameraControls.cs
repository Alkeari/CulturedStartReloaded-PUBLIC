using System;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     The rotate and zoom buttons under the 3D character, present for the
    ///     whole narrative stage whether or not the Start Editor is open. Lives
    ///     above the editor layer so the controls stay clickable in both states.
    /// </summary>
    public static class StageCameraControls
    {
        private const string MovieName = "CSRStageControls";
        private const int LayerOrder = 4600;
        private const float DragRotateFactor = 6f;
        private const float DragZoomFactor = 10f;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static StageCameraControlsVM? _viewModel;
        private static ScreenBase? _host;
        private static bool _dragging;
        private static bool _wasMouseDown;
        private static Vec2 _lastMouse;

        /// <summary>
        ///     Click and hold on the character to drag: left-right orbits, up-down
        ///     zooms. Global input state is polled directly, because the overlay
        ///     layer's own input context only reports while the layer is hit. The
        ///     drag must START over the center stage, and never while a picker or
        ///     inquiry covers the screen, so panel and popup clicks cannot move
        ///     the camera. Runs every frame from the creation screen's tick patch.
        /// </summary>
        /// <summary>
        ///     Reads whatever option the stage currently has selected and shows
        ///     what it does. Selection is how this stage is read at all: a list row
        ///     renders only its title, so the player learns an option by landing on
        ///     it, and this panel has to follow that rather than a hover.
        /// </summary>
        private static void RefreshEffectText()
        {
            if (_viewModel == null) return;

            try
            {
                var state = TaleWorlds.Core.GameStateManager.Current?.ActiveState
                    as TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState;
                var manager = state?.CharacterCreationManager;
                var menu = manager?.CurrentMenu;

                // Cultured Start's answers state what they grant in their own prose and in
                // the game's own effect line, and carry no panel of their own
                bool culturedStart = CulturedStartReloaded.CharacterCreation.Session.CreationSession.Current.Mode ==
                                     CulturedStartReloaded.Models.SetupMode.LifePath;

                if (culturedStart || manager == null || menu == null ||
                    !manager.SelectedOptions.TryGetValue(menu, out var option) || option == null)
                {
                    _viewModel.EffectTitle = string.Empty;
                    _viewModel.EffectText = string.Empty;
                    return;
                }

                _viewModel.EffectTitle = option.Text?.ToString() ?? string.Empty;
                _viewModel.EffectText = Services.ChoiceEffects.BlockFor(option.StringId);
            }
            catch (Exception ex)
            {
                if (_effectFaulted) return;
                _effectFaulted = true;
                Services.CSLogger.Error("StageCameraControls: the effect panel stopped refreshing.", ex);
            }
        }

        private static bool _effectFaulted;

        private static bool _silenceFaulted;

        private static string? _lastMenuId;

        /// <summary>
        ///     The Start Editor route's one menu exists only to host the editor, so arriving on it
        ///     opens the editor rather than asking for a click. Only on arrival: a player who closes
        ///     the editor stays on the menu, whose button opens it again.
        /// </summary>
        private static void OpenTheStartEditorOnItsMenu()
        {
            try
            {
                var manager = (TaleWorlds.Core.GameStateManager.Current?.ActiveState as CharacterCreationState)
                    ?.CharacterCreationManager;
                string? menuId = manager?.CurrentMenu?.StringId;
                if (menuId == _lastMenuId) return;
                _lastMenuId = menuId;

                if (menuId == "cs_custom_menu" &&
                    CharacterCreation.Session.CreationSession.Current.Mode == Models.SetupMode.Custom)
                    StartEditorScreen.Open();
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageCameraControls: opening the Start Editor on its menu failed.", ex);
            }
        }

        /// <summary>
        ///     Takes the vanilla effect line off the stage wherever it would state
        ///     nothing.
        ///
        ///     The line centered under the character is the selected option's
        ///     <c>PositiveEffectText</c>, which the game composes from that
        ///     option's own <see cref="NarrativeMenuOptionArgs"/>. With no skills
        ///     declared it falls back to a template of two counts and nothing
        ///     else, so an option declaring no effects reads "0 unspent Focus
        ///     Point" and "0 unspent Attribute Point" however much the character
        ///     actually holds, and goes on reading it while the sheet beside it
        ///     climbs. Every menu this mod adds declares nothing, deliberately:
        ///     the game replays an option's args at the end of creation, so
        ///     filling them in would hand the character everything a second time
        ///     on top of what the pipeline already granted. What each answer does
        ///     is stated in the panel beside the character instead, derived from
        ///     the choice's own consequences.
        ///
        ///     So the line is emptied rather than fed, and only where it would
        ///     read zero and zero. Every option the game itself ships declares
        ///     either skills or a non-zero pair, on v1.3.15 and v1.5.2 alike, so
        ///     the Vanilla Start route keeps the line it has always had.
        ///
        ///     Re-asserted every frame because the string is not read per render:
        ///     the option view models write it once when the menu is entered and
        ///     again on each click, so a one-shot clear would last until the next
        ///     click and no longer.
        /// </summary>
        private static void SilenceEmptyEffectLines()
        {
            try
            {
                var options = NarrativeStage()?.SelectionList;
                if (options == null) return;

                foreach (var option in options)
                {
                    if (option?.Option == null || !SaysNothing(option.Option.Args)) continue;

                    option.PositiveEffectText = string.Empty;
                }
            }
            catch (Exception ex)
            {
                if (_silenceFaulted) return;
                _silenceFaulted = true;
                CSLogger.Error("StageCameraControls: the vanilla effect line stopped being silenced.", ex);
            }
        }

        /// <summary>
        ///     Whether the game's own effect line for this option would carry no
        ///     information at all. The fallback template holds the two unspent
        ///     counts and nothing more: no skill, trait, renown or gold ever
        ///     reaches it, so both counts at zero leaves two sentences about
        ///     nothing. An option naming even one skill is left alone.
        /// </summary>
        private static bool SaysNothing(NarrativeMenuOptionArgs args)
        {
            return args != null
                   && (args.AffectedSkills == null || args.AffectedSkills.Count == 0)
                   && args.UnspentFocusToAdd == 0
                   && args.UnspentAttributeToAdd == 0;
        }

        /// <summary>
        ///     The view model behind the narrative stage, reached the same way the
        ///     stage view patch reaches the view itself: by name, because both
        ///     fields belong to the SandBox module assemblies this mod does not
        ///     reference. Null whenever some other stage is up.
        /// </summary>
        internal static CharacterCreationNarrativeStageVM? NarrativeStage()
        {
            var host = _host;
            if (host == null) return null;

            object? view = AccessTools.Field(host.GetType(), "_currentStageView")?.GetValue(host);
            if (view == null) return null;

            return AccessTools.Field(view.GetType(), "_dataSource")?.GetValue(view)
                as CharacterCreationNarrativeStageVM;
        }

        public static void Tick()
        {
            if (_layer == null) return;

            OpenTheStartEditorOnItsMenu();
            RefreshEffectText();
            SilenceEmptyEffectLines();
            Menus.AgeAdjustMenu.RefreshIfPending(NarrativeStage());

            // The staging asks for a width while it is being composed, which is
            // before the stage view that owns the camera exists. Here it always
            // does, and the request costs nothing until it changes
            StagePreviewControls.ApplyPendingFit();

            try
            {
                bool down = Input.IsKeyDown(InputKey.LeftMouseButton);
                var mouse = Input.MousePositionRanged;

                if (!_dragging)
                {
                    bool blocked = EditorEscape.AnyModalOpen;
                    bool justPressed = down && !_wasMouseDown;
                    if (!blocked && justPressed &&
                        mouse.x > 0.30f && mouse.x < 0.70f && mouse.y > 0.04f && mouse.y < 0.90f)
                    {
                        _dragging = true;
                        _lastMouse = mouse;
                    }
                }
                else if (!down)
                {
                    _dragging = false;
                }
                else
                {
                    var delta = mouse - _lastMouse;
                    _lastMouse = mouse;
                    if (System.Math.Abs(delta.x) > 0.0001f)
                        StagePreviewControls.RotateBy(delta.x * DragRotateFactor);
                    if (System.Math.Abs(delta.y) > 0.0001f)
                        StagePreviewControls.ZoomBy(delta.y * DragZoomFactor);
                }

                _wasMouseDown = down;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageCameraControls: drag handling failed.", ex);
                _dragging = false;
            }
        }

        public static void Open()
        {
            if (_layer != null) return;

            try
            {
                StagePreviewControls.ClearHomeFrame();
                StagePreviewControls.ClearFit();
                var host = ScreenManager.TopScreen;
                if (host == null) return;

                _viewModel = new StageCameraControlsVM();
                _layer = new GauntletLayer(nameof(StageCameraControls), LayerOrder);
                _movie = _layer.LoadMovie(MovieName, _viewModel);
                // Mouse clicks only; keyboard focus stays with the vanilla stage
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.MouseButtons);
                host.AddLayer(_layer);
                _host = host;

                CSLogger.Info("StageCameraControls: opened.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageCameraControls: failed to open.", ex);
                Close();
            }
        }

        public static void Close()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();

                    if (_movie != null)
                        _layer.ReleaseMovie(_movie);

                    _host?.RemoveLayer(_layer);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageCameraControls: close failed.", ex);
            }
            finally
            {
                _viewModel?.OnFinalize();
                _viewModel = null;
                _movie = null;
                _layer = null;
                _host = null;
            }
        }
    }

    public class StageCameraControlsVM : ViewModel
    {
        public StageCameraControlsVM()
        {
            RotateLeftHint = Hint("{=CSR_Hint_RotateLeft}Rotate the character to the left.");
            RotateRightHint = Hint("{=CSR_Hint_RotateRight}Rotate the character to the right.");
            ZoomInHint = Hint("{=CSR_Hint_ZoomIn}Move the camera closer.");
            ZoomOutHint = Hint("{=CSR_Hint_ZoomOut}Move the camera back.");
            PanLeftHint = Hint("{=CSR_Hint_PanLeft}Slide the camera to the left.");
            PanRightHint = Hint("{=CSR_Hint_PanRight}Slide the camera to the right.");
            ResetViewHint = Hint("{=CSR_Hint_ResetView}Return the camera to where the stage placed it.");
        }

        private string _effectText = string.Empty;

        /// <summary>
        ///     The third text every option carries: exactly what taking it does.
        ///     Derived from the choice's own effects rather than written beside
        ///     them, so it cannot say one thing while the choice does another.
        /// </summary>
        [DataSourceProperty]
        public string EffectText
        {
            get => _effectText;
            set
            {
                if (value == _effectText) return;
                _effectText = value;
                OnPropertyChangedWithValue(value, nameof(EffectText));
                OnPropertyChangedWithValue(HasEffect, nameof(HasEffect));
            }
        }

        /// <summary>False while nothing is selected, so the panel does not sit empty.</summary>
        [DataSourceProperty]
        public bool HasEffect => !string.IsNullOrEmpty(_effectText);

        private string _effectTitle = string.Empty;

        /// <summary>
        ///     The selected option's own title, sitting above its effects. It comes
        ///     from the option the manager holds rather than a copy kept here, so
        ///     the column can never name one choice while describing another.
        /// </summary>
        [DataSourceProperty]
        public string EffectTitle
        {
            get => _effectTitle;
            set
            {
                if (value == _effectTitle) return;
                _effectTitle = value;
                OnPropertyChangedWithValue(value, nameof(EffectTitle));
            }
        }

        private static TaleWorlds.Core.ViewModelCollection.Information.HintViewModel Hint(string key)
        {
            return new TaleWorlds.Core.ViewModelCollection.Information.HintViewModel(
                new TaleWorlds.Localization.TextObject(key));
        }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel RotateLeftHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel RotateRightHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel ZoomInHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel ZoomOutHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel ResetViewHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel PanLeftHint { get; }

        [DataSourceProperty]
        public TaleWorlds.Core.ViewModelCollection.Information.HintViewModel PanRightHint { get; }

        public void ExecutePanLeft() => StagePreviewControls.PanLeft();

        public void ExecutePanRight() => StagePreviewControls.PanRight();

        public void ExecuteRotateLeft() => StagePreviewControls.RotateLeft();

        public void ExecuteRotateRight() => StagePreviewControls.RotateRight();

        public void ExecuteZoomIn() => StagePreviewControls.ZoomIn();

        public void ExecuteZoomOut() => StagePreviewControls.ZoomOut();

        public void ExecuteResetView() => StagePreviewControls.ResetView();
    }
}
