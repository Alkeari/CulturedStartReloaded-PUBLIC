using System;
using CulturedStartReloaded.Helpers;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using HarmonyLib;
using StoryMode;
using StoryMode.GameComponents.CampaignBehaviors;
using StoryMode.StoryModePhases;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    /// Patches TrainingFieldCampaignBehavior.OnCharacterCreationIsOver to skip the tutorial.
    /// Returns false to prevent vanilla from executing its own teleport/mission logic.
    /// The tutorial completion call triggers FinalizeTutorialPhasePatch which handles cleanup.
    /// Our CulturedStartBehavior separately handles teleporting to the player's chosen location.
    ///
    /// From game v1.5.0 the method takes the creation pass and acts only on pass 1, and the game
    /// raises the event ten times. The argument is read through Harmony's __args rather than
    /// declared, because declaring it would fail to bind on the older games where the method takes
    /// nothing, and every other pass is handed straight back so it is neither repeated nor lost.
    /// </summary>
    [HarmonyPatch(typeof(TrainingFieldCampaignBehavior), "OnCharacterCreationIsOver")]
    public static class TutorialSkipPatch
    {
        private const int TutorialPass = 1;

        [HarmonyPrefix]
        public static bool Prefix(object[] __args, ref bool ___SkipTutorialMission)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] is int pass && pass != TutorialPass)
                    return true;

                if (!CSGameModeService.IsStoryMode() || !CSSettings.IsModEnabled)
                    return true; // Let vanilla run in non-story mode or when the mod is disabled

                CSLogger.Info(">>> START: TutorialSkipPatch");

                // Tell vanilla to skip the training field mission
                ___SkipTutorialMission = true;
                CSLogger.Info("  Set SkipTutorialMission = true");

                // Mark tutorial dialogue as done
                TutorialPhase.Instance?.PlayerTalkedWithBrotherForTheFirstTime();
                CSLogger.Info("  PlayerTalkedWithBrotherForTheFirstTime called.");

                // Complete the tutorial phase (triggers FinalizeTutorialPhasePatch)
                StoryModeManager.Current?.MainStoryLine?.CompleteTutorialPhase(true);
                CSLogger.Info("  CompleteTutorialPhase(true) called.");

                CSLogger.Info("<<< END: TutorialSkipPatch [SUCCESS]");
                return false; // Skip vanilla's method: we handle everything
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: TutorialSkipPatch [FAILED]", ex);
                return true; // Fall back to vanilla on error
            }
        }
    }
}
