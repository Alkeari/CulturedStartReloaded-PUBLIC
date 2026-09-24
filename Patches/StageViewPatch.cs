using System;
using System.Reflection;
using CulturedStartReloaded.CharacterCreation.Editor;
using CulturedStartReloaded.Services;
using HarmonyLib;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Ties the camera control overlay to the vanilla narrative stage view's
    ///     lifetime, so the rotate and zoom buttons exist for the whole stage,
    ///     with or without the Start Editor open. The view type lives in the
    ///     SandBox.GauntletUI module assembly, so it is resolved by name.
    /// </summary>
    [HarmonyPatch]
    public static class StageViewOpenPatch
    {
        public static MethodBase? TargetMethod()
        {
            return AccessTools.Method(
                "SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView:SetGenericScene");
        }

        public static bool Prepare() => TargetMethod() != null;

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                StageCameraControls.Open();
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewOpenPatch: opening camera controls failed.", ex);
            }
        }
    }

    /// <summary>
    ///     Ticks the Start Editor's Escape handling; the editor layer captures
    ///     input, so the vanilla menus never see the key while it is open.
    /// </summary>
    [HarmonyPatch]
    public static class ScreenTickPatch
    {
        public static MethodBase? TargetMethod()
        {
            return AccessTools.Method(
                "SandBox.View.CharacterCreation.CharacterCreationScreen:OnFrameTick");
        }

        public static bool Prepare() => TargetMethod() != null;

        [HarmonyPostfix]
        public static void Postfix()
        {
            // This runs on every frame of character creation. An unguarded throw here would repeat
            // for as long as the screen is open, so it is caught and logged once per session rather
            // than flooding the log or taking the screen down with it.
            try
            {
                EditorEscape.Tick();
                StageCameraControls.Tick();
            }
            catch (Exception ex)
            {
                if (_tickFaulted) return;
                _tickFaulted = true;
                CSLogger.Error("ScreenTickPatch: the editor frame tick failed; it is skipped from here.", ex);
            }
        }

        private static bool _tickFaulted;
    }

    /// <inheritdoc cref="StageViewOpenPatch"/>
    [HarmonyPatch]
    public static class StageViewClosePatch
    {
        public static MethodBase? TargetMethod()
        {
            return AccessTools.Method(
                "SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView:OnFinalize");
        }

        public static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                StartEditorScreen.Close();
                StageCameraControls.Close();
            }
            catch (Exception ex)
            {
                CSLogger.Error("StageViewClosePatch: closing camera controls failed.", ex);
            }
        }
    }
}
