using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Helpers;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Library;

namespace CulturedStartReloaded.CharacterCreation
{
    public class CharacterCreationProvider : ICharacterCreationContentHandler
    {
        private EquipmentPreviewService? _previewService;

        public void InitializeContent(CharacterCreationManager characterCreationManager)
        {
            CSLogger.Info(">>> START: InitializeContent");
            try
            {
                // Initialize equipment preview service
                _previewService = new EquipmentPreviewService();
                _previewService.Initialize();
                CharacterPreviewHelper.SetPreviewService(_previewService);
                MeansMenu.SetPreviewService(_previewService);
                ArmsMenu.SetPreviewService(_previewService);
                GearCustomizationMenu.SetPreviewService(_previewService);
                CulturedStart.CulturedStartMenus.SetPreviewService(_previewService);

                CSLogger.Info("  Adding ModeMenu and CustomHubMenu...");
                ModeMenu.AddModeMenu(characterCreationManager);
                CustomHubMenu.AddCustomHubMenu(characterCreationManager);

                CSLogger.Info("  Adding the life scenes (13 menus)...");
                SceneMenus.Reset();
                SceneMenus.AddSceneMenus(characterCreationManager);

                if (CSGameModeService.IsStoryMode())
                {
                    CSLogger.Info("  Adding StoryProgressMenu (Story Mode detected)...");
                    StoryProgressMenu.AddStoryProgressMenu(characterCreationManager);
                }
                else
                {
                    CSLogger.Info("  Skipping StoryProgressMenu (Sandbox mode).");
                }

                CSLogger.Info("  Adding StandingMenu...");
                StandingMenu.AddStandingMenu(characterCreationManager);
                AgeAdjustMenu.AddAgeAdjustMenu(characterCreationManager);

                CSLogger.Info("  Adding MeansMenu...");
                MeansMenu.AddMeansMenu(characterCreationManager);

                CSLogger.Info("  Adding CompanionSelectMenu...");
                CompanionSelectMenu.AddCompanionMenu(characterCreationManager);

                CSLogger.Info("  Adding HouseholdMenu...");
                HouseholdMenu.AddHouseholdMenu(characterCreationManager);

                CSLogger.Info("  Adding WarbandMenu...");
                WarbandMenu.AddWarbandMenu(characterCreationManager);

                CSLogger.Info("  Adding ContextualMenus (Kingdom, Settlement)...");
                ContextualMenus.AddContextualMenus(characterCreationManager);

                CSLogger.Info("  Adding FoundingMenu...");
                FoundingMenu.AddFoundingMenu(characterCreationManager);

                CSLogger.Info("  Adding StartLocationMenu...");
                StartLocationMenu.AddStartLocationMenu(characterCreationManager);

                CSLogger.Info("  Adding ScenarioChapterMenus...");
                ScenarioChapterMenus.AddChapterMenus(characterCreationManager);

                if (Services.TaomBridge.IsLoaded)
            {
                CSLogger.Info("  Adding CareerMenu (the conversion offers careers)...");
                CareerMenu.AddCareerMenu(characterCreationManager);
            }

            CSLogger.Info("  Adding GearCustomizationMenu...");
                GearCustomizationMenu.AddGearCustomizationMenu(characterCreationManager);

                CSLogger.Info("  Adding ProvisionsMenu...");
                ProvisionsMenu.AddProvisionsMenu(characterCreationManager);

                CSLogger.Info("  Adding FleetMenu...");
                FleetMenu.AddFleetMenu(characterCreationManager);

                CSLogger.Info("  Adding ArmsMenu...");
                ArmsMenu.AddArmsMenu(characterCreationManager);

                CSLogger.Info("  Adding NameMenu and EpilogueMenu...");
                NameMenu.AddNameMenus(characterCreationManager);
                EpilogueMenu.AddEpilogueMenu(characterCreationManager);

                CSLogger.Info("  Adding Cultured Start's own menus...");
                CulturedStart.CulturedStartMenus.Register(characterCreationManager);

                CSLogger.Info("<<< END: InitializeContent [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: InitializeContent [FAILED]", ex);
                throw;
            }
        }

        public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
        {
            CSLogger.Info(">>> START: AfterInitializeContent");
            try
            {
                ReorderMenusToFront(characterCreationManager);
                CSLogger.Info("<<< END: AfterInitializeContent [SUCCESS]");
            }
            catch (Exception ex)
            {
                // Everything that can fail in the reorder fails before the menu list is
                // touched, so the cost of failing is that this mod's menus sit behind the
                // game's own rather than in front: messy, and playable. Rethrowing here
                // took character creation down over an ordering problem instead.
                CSLogger.Error(
                    "<<< END: AfterInitializeContent [FAILED] - the menus keep the game's own order.", ex);
            }
        }

        public void OnStageCompleted(CharacterCreationStageBase stage)
        {
            if (stage is CharacterCreationNarrativeStage)
            {
                var narrativeManager = (TaleWorlds.Core.GameStateManager.Current?.ActiveState
                    as CharacterCreationState)?.CharacterCreationManager;
                if (narrativeManager != null)
                {
                    CreationSession.Current.ResumeMenuId = narrativeManager.CurrentMenu?.StringId;
                    CulturedStart.CulturedStartMenus.DropAnswersOfOtherRoutes(narrativeManager);
                }

                DropDuplicateNamingStages();
                Services.Application.Steps.EquipmentStep.DressForTheRemainingScreens(CreationSession.Current);
            }

            if (stage is CharacterCreationCultureStage)
            {
                CSLogger.Info(">>> START: OnStageCompleted (CultureStage)");
                try
                {
                    var state = TaleWorlds.Core.GameStateManager.Current?.ActiveState as CharacterCreationState;
                    var manager = state?.CharacterCreationManager;
                    if (manager != null)
                    {
                        CreationSession.Current.SelectedCulture =
                            manager.CharacterCreationContent.SelectedCulture;
                        // Walking forward again from the culture screen is a new walk of the
                        // narrative stage, which starts at its beginning
                        CreationSession.Current.ResumeMenuId = null;
                        CSLogger.Info($"  Culture captured: {CreationSession.Current.SelectedCulture?.StringId ?? "null"}");
                    }
                    else
                    {
                        CSLogger.Warn("  Could not capture culture: manager was null.");
                    }
                    CSLogger.Info("<<< END: OnStageCompleted (CultureStage) [SUCCESS]");
                }
                catch (Exception ex)
                {
                    CSLogger.Error("<<< END: OnStageCompleted (CultureStage) [FAILED]", ex);
                }
            }
        }

        /// <summary>
        ///     Both of this mod's routes ask for the character's name and the clan's
        ///     name themselves, with the culture's own name pools and a way to type
        ///     one instead. The game then asks again on its clan-naming screen and
        ///     once more on the review screen, both showing a name the player never
        ///     chose, and the second of the two silently overwrites the first. The
        ///     stages are dropped once this mod's flow has ended and the answers are
        ///     in hand. A Vanilla Start keeps both: it promises the game's own
        ///     character creation exactly as if the mod were absent.
        ///
        ///     Safe at this point in the walk because the narrative stage sits third
        ///     and both of these sit after the banner editor, so the index the
        ///     manager has already advanced to is never one of the two removed.
        /// </summary>
        private static void DropDuplicateNamingStages()
        {
            try
            {
                if (CreationSession.Current.Mode == SetupMode.Vanilla)
                {
                    CSLogger.Info("Naming stages kept: this is a Vanilla Start.");
                    return;
                }

                // Cultured Start asks no name of its own and hands both over to the
                // game's clan naming and review screens, as it always has
                if (CreationSession.Current.Mode == SetupMode.LifePath)
                {
                    CSLogger.Info("Naming stages kept: Cultured Start names the character on them.");
                    return;
                }

                var state = TaleWorlds.Core.GameStateManager.Current?.ActiveState as CharacterCreationState;
                var manager = state?.CharacterCreationManager;
                if (manager == null)
                {
                    CSLogger.Warn("Naming stages kept: the creation manager could not be reached.");
                    return;
                }

                bool clan = manager.RemoveStage<CharacterCreationClanNamingStage>();
                bool review = manager.RemoveStage<CharacterCreationReviewStage>();
                CSLogger.Info($"Naming stages dropped: clan naming {clan}, review {review}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("Dropping the duplicate naming stages failed; both are left in place.", ex);
            }
        }

        public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
        {
            CSLogger.Info(">>> START: OnCharacterCreationFinalize");
            try
            {
                CreationSession.Current.SelectedCulture =
                    characterCreationManager.CharacterCreationContent.SelectedCulture;
                CSLogger.Info($"  Final culture: {CreationSession.Current.SelectedCulture?.StringId ?? "null"}");

                // Cleanup preview roster and clear every static holder so no menu
                // keeps a reference to the torn-down service
                _previewService?.Cleanup();
                _previewService = null;
                CharacterPreviewHelper.SetPreviewService(null);
                MeansMenu.SetPreviewService(null);
                ArmsMenu.SetPreviewService(null);
                GearCustomizationMenu.SetPreviewService(null);
                CulturedStart.CulturedStartMenus.SetPreviewService(null);

                CSLogger.Info("<<< END: OnCharacterCreationFinalize [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: OnCharacterCreationFinalize [FAILED]", ex);
            }
        }

        private void ReorderMenusToFront(CharacterCreationManager manager)
        {
            CSLogger.Info(">>> START: ReorderMenusToFront");
            try
            {
                var narrativeMenusField = AccessTools.Field(typeof(CharacterCreationManager), "_narrativeMenus");
                if (narrativeMenusField == null)
                {
                    CSLogger.Error("  Could not find _narrativeMenus field via reflection.");
                    throw new InvalidOperationException("_narrativeMenus field not found.");
                }

                var narrativeMenus = narrativeMenusField.GetValue(manager) as MBList<NarrativeMenu>;
                if (narrativeMenus == null)
                {
                    CSLogger.Error("  _narrativeMenus value is null.");
                    throw new InvalidOperationException("_narrativeMenus is null.");
                }

                CSLogger.Info($"  Total menus before reorder: {narrativeMenus.Count}");

                var session = CreationSession.Current;
                CulturedStart.CulturedStartMenus.Activate(manager, session.Mode);

                // A vanilla start named on the game's own screen is exactly the promise
                // that this mod stays out of the way: leave the game's menus in front and
                // the stage runs as if the mod were not installed.
                if (session.RouteDecidedExternally && session.Mode == SetupMode.Vanilla)
                {
                    CSLogger.Info("  Advanced Starting Options chose a vanilla start; menus left untouched.");
                    CSLogger.Info("<<< END: ReorderMenusToFront [SKIPPED]");
                    return;
                }

                var ourMenus = new List<NarrativeMenu>();
                var otherMenus = new List<NarrativeMenu>();

                foreach (var menu in narrativeMenus)
                {
                    if (menu.StringId.StartsWith("cs_"))
                        ourMenus.Add(menu);
                    else
                        otherMenus.Add(menu);
                }

                // The route is already known, so the stage must open on that route's own
                // first menu rather than on the mode menu. The stage opens whatever sits
                // at index 0, which the flow's include predicates do not govern.
                if (session.RouteDecidedExternally)
                {
                    var firstId = CreationFlow.GetFirst(session);
                    var firstMenu = ourMenus.FirstOrDefault(m => m.StringId == firstId);
                    if (firstMenu != null)
                    {
                        ourMenus.Remove(firstMenu);
                        ourMenus.Insert(0, firstMenu);
                        CSLogger.Info($"  Route decided as {session.Mode}; the stage opens on {firstId}.");
                    }
                    else
                    {
                        CSLogger.Warn($"  Route decided as {session.Mode} but no menu matched '{firstId}'; " +
                                      "the mode menu still leads.");
                    }
                }

                CSLogger.Info($"  Our menus: {ourMenus.Count}, Vanilla menus: {otherMenus.Count}");

                narrativeMenus.Clear();
                foreach (var menu in ourMenus) narrativeMenus.Add(menu);
                foreach (var menu in otherMenus) narrativeMenus.Add(menu);

                CSLogger.Info($"  Reordered. Menu order:");
                for (int i = 0; i < Math.Min(narrativeMenus.Count, 30); i++)
                {
                    CSLogger.Debug($"    [{i}] {narrativeMenus[i].StringId}");
                }

                CSLogger.Info("<<< END: ReorderMenusToFront [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: ReorderMenusToFront [FAILED]", ex);
                throw;
            }
        }
    }
}
