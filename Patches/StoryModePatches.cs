using System;
using CulturedStartReloaded.Helpers;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using HarmonyLib;
using StoryMode.GameComponents.CampaignBehaviors;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Skips vanilla tutorial finalization in story mode. Keeps the elder
    ///     brother alive (he joins the party in StoryModeStep for Phase 2+ starts)
    ///     and leaves inventory and gold to the mod's own apply pipeline.
    /// </summary>
    [HarmonyPatch(typeof(TutorialPhaseCampaignBehavior), "FinalizeTutorialPhase")]
    public static class FinalizeTutorialPhasePatch
    {
        // A game without FinalizeTutorialPhase has nothing to skip, so the class stands down instead of
        // failing to patch
        [HarmonyPrepare]
        public static bool Prepare()
        {
            if (AccessTools.Method(typeof(TutorialPhaseCampaignBehavior), "FinalizeTutorialPhase") != null)
                return true;

            CSLogger.Info("FinalizeTutorialPhasePatch: this game has no FinalizeTutorialPhase; nothing to patch.");
            return false;
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            try
            {
                if (!CSGameModeService.IsStoryMode() || !CSSettings.IsModEnabled)
                    return true;

                // Heal the hero to full
                Hero.MainHero.Heal(Hero.MainHero.MaxHitPoints, false);
                CSLogger.Info("FinalizeTutorialPhasePatch: vanilla finalization skipped, hero healed.");

                return false; // Skip vanilla FinalizeTutorialPhase
            }
            catch (Exception ex)
            {
                CSLogger.Error("FinalizeTutorialPhasePatch failed; falling back to vanilla.", ex);
                return true;
            }
        }
    }
}
