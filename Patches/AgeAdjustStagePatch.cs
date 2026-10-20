using System;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Lets the age chapter advance with nothing clicked, and tells it when the
    ///     stage has finished entering it.
    ///
    ///     The stage enables Next only while an option is selected, and a player who
    ///     keeps the age the life came to has selected nothing. It asks this after
    ///     every selection and at the end of every menu entry, which is also the one
    ///     moment after its replay of a recorded option, so the chapter hears here that
    ///     the next select handler is a real click.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreationNarrativeStageVM), "CanAdvanceToNextStage")]
    public static class AgeAdjustStagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            try
            {
                var state = GameStateManager.Current?.ActiveState as CharacterCreationState;
                if (AgeAdjustMenu.Settles(state?.CharacterCreationManager?.CurrentMenu))
                    __result = true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("AgeAdjustStagePatch: reading the age chapter's state failed.", ex);
            }
        }
    }
}
