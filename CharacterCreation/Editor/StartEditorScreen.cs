using System;
using CulturedStartReloaded.Services;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Hosts the Start Editor as a Gauntlet layer over whatever screen is
    ///     active during character creation. Opening degrades to an in-game
    ///     message if the UI system refuses, so the popup pickers always remain
    ///     a working fallback.
    /// </summary>
    public static class StartEditorScreen
    {
        private const string MovieName = "CSRStartEditor";
        private const int LayerOrder = 4500;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static StartEditorVM? _viewModel;
        private static ScreenBase? _host;

        /// <summary>
        ///     Opens the editor. From the custom path it finishes creation, so
        ///     Done advances the stage and nothing is withheld. Opened from
        ///     inside a narrative menu as an escape hatch it must hand the player
        ///     back to that menu instead, and it must answer only the question
        ///     that menu asked: pass the scope that names those tabs.
        /// </summary>
        public static void Open(EditorScope? scope = null, bool advanceOnDone = true)
        {
            if (_layer != null)
            {
                CSLogger.Info("StartEditorScreen: already open.");
                return;
            }

            try
            {
                var host = ScreenManager.TopScreen;
                if (host == null)
                {
                    CSLogger.Warn("StartEditorScreen: no top screen to attach to.");
                    return;
                }

                _viewModel = new StartEditorVM(Close, scope, advanceOnDone);
                _layer = new GauntletLayer(nameof(StartEditorScreen), LayerOrder);
                _movie = _layer.LoadMovie(MovieName, _viewModel);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _host = host;

                CSLogger.Info("StartEditorScreen: opened.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartEditorScreen: failed to open.", ex);
                Close();
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject(
                            "{=CSR_Editor_OpenFailed}The Start Editor could not open; the menu pickers still work.")
                        .ToString()));
            }
        }

        /// <summary>
        ///     Lets Escape close the editor the way it backs out of any menu.
        ///     Called every frame from the creation screen's tick patch; the
        ///     editor layer holds input focus, so nobody else sees the key.
        /// </summary>
        public static void TickEscape()
        {
            try
            {
                if (_layer != null && _layer.Input.IsKeyReleased(TaleWorlds.InputSystem.InputKey.Escape))
                {
                    // Through the view model, so Escape gets the same restore
                    // and discard guard as the Close button
                    if (_viewModel != null) _viewModel.ExecuteClose();
                    else Close();
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartEditorScreen: escape handling failed.", ex);
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
                    // Removing a layer never clears the screen manager's focus, and a stale
                    // focus on this layer's order refuses focus to every lower layer after it,
                    // the map's among them, which leaves Escape and every key going nowhere
                    ScreenManager.TryLoseFocus(_layer);

                    if (_movie != null)
                        _layer.ReleaseMovie(_movie);

                    _host?.RemoveLayer(_layer);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartEditorScreen: close failed.", ex);
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
