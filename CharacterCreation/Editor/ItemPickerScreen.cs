using System;
using System.Collections.Generic;
using CulturedStartReloaded.Services;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Hosts the armory picker as a modal Gauntlet layer above the Start
    ///     Editor. If the custom picker cannot open, the plain searchable popup
    ///     remains as the fallback so picking always works.
    /// </summary>
    public static class ItemPickerScreen
    {
        private const string MovieName = "CSRItemPicker";
        private const int LayerOrder = 4700;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ItemPickerVM? _viewModel;
        private static ScreenBase? _host;

        public static bool IsOpen => _layer != null;

        public static void Open(string title, IReadOnlyList<ItemObject> items,
            Action<ItemPickChoice, ItemObject?> onPick, bool includeAutoOption = true,
            bool includeNoneOption = true, ItemObject? currentItem = null)
        {
            if (_layer != null) Close();

            try
            {
                var host = ScreenManager.TopScreen;
                if (host == null)
                {
                    EditorPopups.ShowItems(title, items, onPick, includeAutoOption, includeNoneOption);
                    return;
                }

                _viewModel = new ItemPickerVM(title, items, includeAutoOption, includeNoneOption, onPick, Close,
                    currentItem);
                _layer = new GauntletLayer(nameof(ItemPickerScreen), LayerOrder);
                _movie = _layer.LoadMovie(MovieName, _viewModel);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _host = host;
            }
            catch (Exception ex)
            {
                CSLogger.Error("ItemPickerScreen: failed to open; falling back to the popup.", ex);
                Close();
                EditorPopups.ShowItems(title, items, onPick, includeAutoOption, includeNoneOption);
            }
        }

        /// <summary>
        ///     Escape backs out without picking anything. Returns true whenever
        ///     the picker is open, so the editor underneath never acts on the
        ///     same key press.
        /// </summary>
        public static bool TickEscape()
        {
            try
            {
                if (_layer == null) return false;
                if (_layer.Input.IsKeyReleased(InputKey.Escape)) Close();
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("ItemPickerScreen: escape handling failed.", ex);
                return true;
            }
        }

        public static void Close()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _layer.IsFocusLayer = false;
                    // Removing a layer never clears the screen manager's focus
                    ScreenManager.TryLoseFocus(_layer);

                    if (_movie != null)
                        _layer.ReleaseMovie(_movie);

                    _host?.RemoveLayer(_layer);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("ItemPickerScreen: close failed.", ex);
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
}
