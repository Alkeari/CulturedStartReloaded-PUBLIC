using System;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Patches
{
    [HarmonyPatch(typeof(Campaign), "OnNewGameCreated")]
    public static class CleanupPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Campaign __instance)
        {
            try
            {
                CreationSession.StartNew();
                CSLogger.Info("CleanupPatch: creation session reset for new game.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("Error in CleanupPatch.", ex);
            }
        }
    }
}