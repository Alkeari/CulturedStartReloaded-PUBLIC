using CulturedStartReloaded.Services;
using TaleWorlds.Library;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Owns the order in which the editor's layers see the Escape key.
    ///     Gauntlet input reaches a layer even when a higher layer holds focus,
    ///     so without this the editor's own handler ran while a picker covered
    ///     it: one press canceled the picker and raised the editor's discard
    ///     prompt behind it, and confirming threw away the whole session.
    /// </summary>
    public static class EditorEscape
    {
        /// <summary>True while anything modal covers the editor panel.</summary>
        public static bool AnyModalOpen =>
            DialogScreen.IsOpen || ItemPickerScreen.IsOpen || PerkPickerScreen.IsOpen || OptionPickerScreen.IsOpen ||
            InformationManager.IsAnyInquiryActive();

        /// <summary>Topmost first; the first layer that is open consumes the key.</summary>
        public static void Tick()
        {
            try
            {
                // A dialog sits above every picker and handles its own keys
                if (DialogScreen.IsOpen) return;

                if (ItemPickerScreen.TickEscape()) return;
                if (PerkPickerScreen.TickEscape()) return;
                if (OptionPickerScreen.TickEscape()) return;

                // An inquiry draws its own buttons and handles its own keys
                if (InformationManager.IsAnyInquiryActive()) return;

                StartEditorScreen.TickEscape();
            }
            catch (System.Exception ex)
            {
                CSLogger.Error("EditorEscape: dispatch failed.", ex);
            }
        }
    }
}
