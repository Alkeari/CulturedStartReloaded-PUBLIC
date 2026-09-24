using System;
using CulturedStartReloaded.Services;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     Orbits and zooms the vanilla narrative stage camera around the
    ///     character. The engine only reads camera parameters when the camera is
    ///     set on the scene layer, so every mutation is pushed back through
    ///     <see cref="StageViewBridge.PushCamera"/>. Every operation is a safe
    ///     no-op when the stage is not the narrative one.
    /// </summary>
    public static class StagePreviewControls
    {
        private const float MinDistance = 1.2f;
        private const float MaxDistance = 14f;

        /// <summary>
        ///     How far the camera may be walked sideways from where the stage put
        ///     it. Far enough to bring either end of a three-wide row into the
        ///     middle of the shot, and not so far that the character can be left
        ///     behind entirely.
        /// </summary>
        private const float MaxPan = 3f;

        /// <summary>
        ///     The widest thing the vanilla shot is known to hold: the two outer
        ///     human marks of the stage scene, 0.99 m apart, which is the pair
        ///     vanilla itself stages two people on.
        /// </summary>
        private const float VanillaSpan = 0.99f;

        /// <summary>One body across, so the outermost character is framed rather than clipped.</summary>
        private const float ShoulderAllowance = 0.6f;

        private const float RotateStep = 0.26f;
        private const float ZoomStep = 0.6f;
        private const float PanStep = 0.3f;

        public static void RotateLeft() => Rotate(RotateStep);

        public static void RotateRight() => Rotate(-RotateStep);

        public static void ZoomIn() => Zoom(-ZoomStep);

        public static void ZoomOut() => Zoom(ZoomStep);

        public static void PanLeft() => Pan(-PanStep);

        public static void PanRight() => Pan(PanStep);

        /// <summary>Continuous rotation for mouse dragging.</summary>
        public static void RotateBy(float radians) => Rotate(radians);

        /// <summary>Continuous zoom for mouse dragging.</summary>
        public static void ZoomBy(float amount) => Zoom(amount);

        private static MatrixFrame? _homeFrame;

        private static float _requestedSpan;
        private static float _fittedSpan = -1f;
        private static float _panned;

        /// <summary>
        ///     Asks the shot to hold a row of characters this many meters wide,
        ///     measured between the outermost two of them.
        ///
        ///     Recorded rather than applied, because the staging is composed while
        ///     the stage view may not exist yet: the first menu's characters are
        ///     built before the view that draws them. <see cref="ApplyPendingFit"/>
        ///     runs it from the frame tick, where the camera is always reachable.
        /// </summary>
        public static void FitSpan(float span) => FitSpan(span, 0, 0, 0);

        /// <summary>
        ///     The same, then the shot is moved by whole presses of the stage's own
        ///     buttons, so the framing lands exactly where a player pressing them by
        ///     hand would have put it: negative pan is left, positive rotate is
        ///     left, positive zoom is out.
        /// </summary>
        public static void FitSpan(float span, int panPresses, int rotatePresses, int zoomPresses)
        {
            _requestedSpan = span;
            _requestedPresses = (panPresses, rotatePresses, zoomPresses);
        }

        private static (int Pan, int Rotate, int Zoom) _requestedPresses;

        /// <summary>Forgets what the shot was opened for, for a stage that starts over.</summary>
        public static void ClearFit()
        {
            _requestedSpan = 0f;
            _requestedPresses = (0, 0, 0);
            _fittedSpan = -1f;
            _panned = 0f;
        }

        /// <summary>
        ///     Opens the shot far enough that everything staged is in it with the
        ///     player doing nothing.
        ///
        ///     Visible width grows exactly in step with distance at a fixed field
        ///     of view, so a row this much wider than the widest thing vanilla
        ///     stages needs the camera exactly that much further back. It is
        ///     measured off the vanilla framing rather than off a field of view
        ///     read from the engine, so nothing here depends on what units that
        ///     number is in or on how much of the screen the menu panel covers.
        ///
        ///     Only ever wider: the stage's own framing is the floor, a row no
        ///     wider than vanilla's is left exactly where the stage placed it, and
        ///     a player who has zoomed, orbited or panned keeps that until the
        ///     staging itself changes width.
        /// </summary>
        public static void ApplyPendingFit()
        {
            if (TaleWorlds.Library.MathF.Abs(_requestedSpan - _fittedSpan) < 0.001f) return;

            float span = _requestedSpan;
            bool fitted = false;
            WithCamera((camera, pivot) =>
            {
                var home = _homeFrame!.Value;
                var toPivot = pivot - home.origin;
                float distance = toPivot.Length;
                if (distance < 0.001f) return;

                float ratio = span <= VanillaSpan
                    ? 1f
                    : (span + ShoulderAllowance) / (VanillaSpan + ShoulderAllowance);

                var frame = home;
                frame.origin = pivot - toPivot.NormalizedCopy() * (distance * ratio);
                camera.Frame = frame;

                _panned = 0f;
                _fittedSpan = span;
                fitted = true;
                CSLogger.Info($"StagePreviewControls: framing {span:0.00} m of stage from " +
                              $"{distance * ratio:0.00} m, where the stage stood at {distance:0.00} m.");
            });

            if (!fitted) return;

            // In the order a player presses them, since an orbit after a pan is not
            // the same shot as a pan after an orbit
            var presses = _requestedPresses;
            if (presses.Pan != 0) Pan(presses.Pan * PanStep);
            if (presses.Rotate != 0) Rotate(presses.Rotate * RotateStep);
            if (presses.Zoom != 0) Zoom(presses.Zoom * ZoomStep);
        }

        /// <summary>Returns the camera to where the stage first placed it.</summary>
        public static void ResetView()
        {
            try
            {
                if (_homeFrame == null) return;
                var stageView = StageViewBridge.FindNarrativeStageView();
                if (stageView == null) return;
                var camera = StageViewBridge.GetCamera(stageView);
                if (camera == null) return;

                camera.Frame = _homeFrame.Value;
                StageViewBridge.PushCamera(stageView, camera);

                // Reset is the framing the stage opened with, and for a row of
                // characters that is the widened one rather than vanilla's, which
                // would put the outer two off screen and leave the player to find
                // them with the very controls the widening exists to save them
                _panned = 0f;
                _fittedSpan = -1f;
                ApplyPendingFit();
            }
            catch (Exception ex)
            {
                CSLogger.Error("StagePreviewControls: reset view failed.", ex);
            }
        }

        /// <summary>Forgets the remembered home frame when a new stage begins.</summary>
        public static void ClearHomeFrame() => _homeFrame = null;

        /// <summary>
        ///     Walks the camera sideways, keeping the direction it looks in.
        ///
        ///     The axis is the shot's own horizontal right rather than a world
        ///     axis: the player can orbit, so after a quarter turn world x is no
        ///     longer sideways on screen and a button labelled left would move the
        ///     view toward the viewer instead. It is the look direction crossed
        ///     with world up, which is horizontal by construction, so panning
        ///     never lifts or drops the shot.
        /// </summary>
        private static void Pan(float amount)
        {
            WithCamera((camera, pivot) =>
            {
                var sideways = Vec3.CrossProduct(camera.Direction, new Vec3(0f, 0f, 1f));
                if (sideways.Length < 0.001f) return;

                float step = TaleWorlds.Library.MathF.Max(-MaxPan - _panned, TaleWorlds.Library.MathF.Min(MaxPan - _panned, amount));
                if (TaleWorlds.Library.MathF.Abs(step) < 0.0001f) return;

                var frame = camera.Frame;
                frame.origin += sideways.NormalizedCopy() * step;
                camera.Frame = frame;
                _panned += step;
            });
        }

        private static void Rotate(float radians)
        {
            WithCamera((camera, pivot) =>
            {
                var frame = camera.Frame;

                // Yaw the whole frame about the WORLD vertical axis. Rotating the
                // camera's local up axis instead would roll the view sideways,
                // because a camera's local up points along its line of sight.
                float cos = TaleWorlds.Library.MathF.Cos(radians);
                float sin = TaleWorlds.Library.MathF.Sin(radians);

                Vec3 Yaw(Vec3 v) => new(
                    v.x * cos - v.y * sin,
                    v.x * sin + v.y * cos,
                    v.z);

                frame.origin = pivot + Yaw(frame.origin - pivot);
                frame.rotation.s = Yaw(frame.rotation.s);
                frame.rotation.f = Yaw(frame.rotation.f);
                frame.rotation.u = Yaw(frame.rotation.u);

                camera.Frame = frame;
            });
        }

        private static void Zoom(float amount)
        {
            WithCamera((camera, pivot) =>
            {
                var frame = camera.Frame;
                var toPivot = pivot - frame.origin;
                float distance = toPivot.Length;
                if (distance < 0.001f) return;

                float newDistance = TaleWorlds.Library.MathF.Min(MaxDistance,
                    TaleWorlds.Library.MathF.Max(MinDistance, distance + amount));
                frame.origin = pivot - toPivot.NormalizedCopy() * newDistance;

                camera.Frame = frame;
            });
        }

        private static void WithCamera(Action<Camera, Vec3> action)
        {
            try
            {
                var stageView = StageViewBridge.FindNarrativeStageView();
                if (stageView == null) return;

                var camera = StageViewBridge.GetCamera(stageView);
                if (camera == null) return;

                _homeFrame ??= camera.Frame;

                var scene = StageViewBridge.GetCharacterScene(stageView);

                var pivot = camera.Frame.origin + camera.Direction * 3f;
                var cradle = scene?.FindEntityWithName("cradle");
                if (cradle != null)
                {
                    var cradlePosition = cradle.GlobalPosition;
                    // Orbit at eye height around the character, not the floor
                    pivot = new Vec3(cradlePosition.x, cradlePosition.y, camera.Frame.origin.z);
                }

                action(camera, pivot);
                StageViewBridge.PushCamera(stageView, camera);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StagePreviewControls: camera operation failed.", ex);
            }
        }
    }
}
