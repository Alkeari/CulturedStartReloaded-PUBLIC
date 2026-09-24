using System;
using System.Collections.Generic;
using CulturedStartReloaded.Services;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>Hosts the generic exclusivity-aware multi-select picker as a modal layer.</summary>
    public static class OptionPickerScreen
    {
        private const string MovieName = "CSROptionPicker";
        private const int LayerOrder = 4700;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static OptionPickerVM? _viewModel;
        private static ScreenBase? _host;

        public static bool IsOpen => _layer != null;

        /// <summary>Opens the picker; false when it could not, so a caller can fall back.</summary>
        public static bool Open(string title, IReadOnlyList<PickerOption> options, bool allowSelectAll,
            Action<List<object?>> onConfirm, string? description = null, bool singleSelect = false,
            bool searchable = false, bool requireSelection = false)
        {
            if (_layer != null) Close();

            try
            {
                var host = ScreenManager.TopScreen;
                if (host == null) return false;

                _viewModel = new OptionPickerVM(title, options, allowSelectAll, onConfirm, Close, description,
                    singleSelect, searchable, requireSelection);
                _layer = new GauntletLayer(nameof(OptionPickerScreen), LayerOrder);
                _movie = _layer.LoadMovie(MovieName, _viewModel);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _host = host;
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("OptionPickerScreen: failed to open.", ex);
                Close();
                return false;
            }
        }

        /// <inheritdoc cref="ItemPickerScreen.TickEscape"/>
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
                CSLogger.Error("OptionPickerScreen: escape handling failed.", ex);
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
                CSLogger.Error("OptionPickerScreen: close failed.", ex);
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
