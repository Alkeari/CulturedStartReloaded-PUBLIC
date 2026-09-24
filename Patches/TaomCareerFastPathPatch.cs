using System;
using CulturedStartReloaded.Services;
using HarmonyLib;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Tales from the Age of Men's Player Switcher answers every narrative menu with its first
    ///     option until it reaches its career menu, once a hero is picked on the face screen. The
    ///     route choice is one of those menus, and its first option is Vanilla Start, so the switcher
    ///     chose the route for the player. Its walk stops on any of this mod's menus instead; the
    ///     Vanilla Start resumes it (<see cref="TaomCompat.ContinueCareerFastPath"/>).
    ///     Applied by hand from <see cref="TaomCompat"/>, since the target exists only with TAOM.
    /// </summary>
    public static class TaomCareerFastPathPatch
    {
        public static bool HoldOnModMenu(object? __0)
        {
            try
            {
                if (__0 == null) return true;

                var menuId = AccessTools.Property(__0.GetType(), "CurrentMenuId")?.GetValue(__0) as string;
                if (menuId == null || !menuId.StartsWith("cs_", StringComparison.Ordinal)) return true;

                CSLogger.Info($"TaomCareerFastPathPatch: TAOM's career fast path held on {menuId}; the player answers it.");
                return false;
            }
            catch (Exception ex)
            {
                CSLogger.Error("TaomCareerFastPathPatch: reading the stage failed; TAOM's fast path runs.", ex);
                return true;
            }
        }
    }
}
