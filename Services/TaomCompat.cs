using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The seams with Tales from the Age of Men's Player Switcher. Its types are named as strings
    ///     and never referenced, so the assembly binds nothing of TAOM and every download keeps one
    ///     shape; without TAOM each lookup finds nothing and this stands down.
    /// </summary>
    public static class TaomCompat
    {
        private const string FastPathServiceType = "TAOM.Features.PlayerSwitcher.NarrativeCareerFastPathService";
        private const string FastPathHookType = "TAOM.Features.PlayerSwitcher.Hooks.Patch78_CharacterCreationManager_StartNarrativeStage";

        public static void PatchCareerFastPath(Harmony harmony, Type patchHost)
        {
#pragma warning disable BHA0003
            var service = AccessTools.TypeByName(FastPathServiceType);
#pragma warning restore BHA0003
            if (service == null) return;

            var target = AccessTools.Method(service, "SkipToCareerMenu");
            if (target == null)
            {
                CSLogger.Warn("TaomCompat: TAOM's career fast path has no SkipToCareerMenu; the route choice is unguarded.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(patchHost, "HoldOnModMenu")));
                CSLogger.Info("TaomCompat: TAOM's career fast path now stops on this mod's menus.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("TaomCompat: patching TAOM's career fast path failed.", ex);
            }
        }

        /// <summary>
        ///     Runs TAOM's fast path from where the stage now stands. The Vanilla Start hands the stage
        ///     to the game's first narrative menu, which is where TAOM would have begun the walk had
        ///     this mod's route choice not stood first; with no hero picked TAOM's own check makes it
        ///     do nothing.
        /// </summary>
        public static void ContinueCareerFastPath(CharacterCreationManager manager)
        {
#pragma warning disable BHA0003
            MethodInfo? hook = AccessTools.Method(AccessTools.TypeByName(FastPathHookType), "Postfix");
#pragma warning restore BHA0003
            if (hook == null) return;

            try
            {
                hook.Invoke(null, new object[] { manager });
            }
            catch (Exception ex)
            {
                CSLogger.Error("TaomCompat: resuming TAOM's career fast path failed; the backstory menus stay.", ex);
            }
        }
    }
}
