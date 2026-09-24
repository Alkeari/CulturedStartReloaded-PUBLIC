using System;
using CulturedStartReloaded.Services;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Hosts the mod's confirmations and text prompts in its own palette, built the way the game
    ///     hosts its inquiries: a global layer, so a prompt raised while one screen hands over to the
    ///     next (the campaign opening on a new realm) is not lost with the screen, and a layer that
    ///     ticks itself, so Enter and Escape work on any screen without a patch to deliver them.
    ///
    ///     The layer is added once and kept for the session, loading a movie per dialog and
    ///     suspending itself between them. Removing a global layer gained a parameter in v1.5.0, so
    ///     never removing one keeps this assembly bound to members every supported game has.
    /// </summary>
    public sealed class DialogScreen : GlobalLayer
    {
        private const string MovieName = "CSRDialog";
        private const int LayerOrder = 19200;
        private const string KeyCategory = "GenericPanelGameKeyCategory";

        private static DialogScreen? _instance;
        private static int _closedOnFrame = -1;

        private readonly GauntletLayer _gauntletLayer;
        private GauntletMovieIdentifier? _movie;
        private DialogVM? _viewModel;

        private DialogScreen()
        {
            _gauntletLayer = new GauntletLayer(nameof(DialogScreen), LayerOrder);
            Layer = _gauntletLayer;
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory(KeyCategory));
            ScreenManager.AddGlobalLayer(this, true);
            ScreenManager.SetSuspendLayer(Layer, true);
        }

        /// <summary>
        ///     True while a dialog is up, and for the rest of the frame that closed one: the key that
        ///     closed it is still released this frame, and a picker beneath must not act on it too.
        /// </summary>
        public static bool IsOpen =>
            _instance?._viewModel != null || _closedOnFrame == TaleWorlds.Engine.Utilities.EngineFrameNo;

        /// <summary>Shows the dialog, replacing any open one. False when the layer could not be built.</summary>
        public static bool Show(DialogVM viewModel)
        {
            try
            {
                _instance ??= new DialogScreen();
                _instance.Open(viewModel);
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("DialogScreen: failed to open.", ex);
                _instance?.Close(viewModel);
                return false;
            }
        }

        private void Open(DialogVM viewModel)
        {
            if (_viewModel != null) Close(_viewModel);

            InformationManager.HideTooltip();
            viewModel.BindClose(Close);
            _viewModel = viewModel;
            _movie = _gauntletLayer.LoadMovie(MovieName, viewModel);
            SetFocus(true);
        }

        private void Close(DialogVM viewModel)
        {
            if (_viewModel != viewModel) return;

            try
            {
                if (_movie != null) _gauntletLayer.ReleaseMovie(_movie);
                SetFocus(false);
            }
            catch (Exception ex)
            {
                CSLogger.Error("DialogScreen: close failed.", ex);
            }
            finally
            {
                _movie = null;
                _viewModel = null;
                viewModel.OnFinalize();
                _closedOnFrame = TaleWorlds.Engine.Utilities.EngineFrameNo;
            }
        }

        protected override void OnEarlyTick(float dt)
        {
            base.OnEarlyTick(dt);
            var viewModel = _viewModel;
            if (viewModel == null) return;

            try
            {
                // Anything that takes focus while the dialog is up (a picker opening beneath it)
                // hands it back, as the game's own inquiries do
                if (ScreenManager.FocusedLayer != Layer)
                    SetFocus(true);

                if (_gauntletLayer.Input.IsHotKeyReleased("Confirm"))
                    viewModel.ExecuteAffirmative();
                else if (_gauntletLayer.Input.IsHotKeyReleased("Exit"))
                    viewModel.ExecuteNegative();
            }
            catch (Exception ex)
            {
                CSLogger.Error("DialogScreen: key handling failed.", ex);
            }
        }

        private void SetFocus(bool focused)
        {
            if (focused)
            {
                ScreenManager.SetSuspendLayer(Layer, false);
                Layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(Layer);
                Layer.InputRestrictions.SetInputRestrictions();
            }
            else
            {
                Layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.SetSuspendLayer(Layer, true);
                Layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(Layer);
            }
        }
    }
}
