using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Reads the game's own Advanced Starting Options, which exist from v1.5.0 and
    ///     only in Sandbox. Everything here answers null or false on older games and in
    ///     Campaign mode, where the options carry nothing.
    /// </summary>
    public static class AdvancedStartBridge
    {
        /// <summary>
        ///     The Player Start entries this mod adds to the game's own list, one per
        ///     route it owns. Vanilla is not among them: it is what the screen already
        ///     offers, and a player who names none of these is taken to have chosen it.
        /// </summary>
        public const string CulturedStartId = "csr_cultured";

        /// <summary>Cultured Start Revamped: the thirteen scenes.</summary>
        public const string RevampedStartId = "csr_revamped";

        public const string StartEditorId = "csr_editor";

        /// <summary>Every entry this mod adds, in the order the screen lists them.</summary>
        public static readonly string[] EntryIds = { CulturedStartId, RevampedStartId, StartEditorId };

        /// <summary>
        ///     The start type the player chose on the game's Advanced Starting Options
        ///     screen, or null when there is none: game v1.4.8 and older, Campaign mode,
        ///     or a Sandbox game started without opening the screen.
        /// </summary>
        public static string? ChosenStartType() => GameCompat.AdvancedStartType();

        /// <summary>
        ///     The route the game's own screen already decided, or null when there was
        ///     no such screen: game v1.4.8 and older, or Campaign mode, where the player
        ///     picks a route on this mod's first menu as usual.
        ///
        ///     Anything the screen offers that is not one of this mod's two entries is a
        ///     vanilla start, so the mod stays out of the way entirely.
        /// </summary>
        public static SetupMode? RouteFromAdvancedStart() => ChosenStartType() switch
        {
            // The screen records only options the player moved off their default, so
            // leaving the start type alone writes no entry at all. That is still the
            // screen having asked, and the route it named is a vanilla start
            null => ScreenAsked() ? SetupMode.Vanilla : null,
            CulturedStartId => SetupMode.LifePath,
            RevampedStartId => SetupMode.Narrative,
            StartEditorId => SetupMode.Custom,
            _ => SetupMode.Vanilla
        };

        /// <summary>
        ///     Whether the game put its own starting-options screen to this player: it
        ///     arrived in v1.5.0 and renders for Sandbox alone, so a Campaign start on a
        ///     new game is asked by this mod's own first menu exactly as an older game is.
        /// </summary>
        private static bool ScreenAsked() =>
            GameCompat.HasAdvancedStartOptions && !Helpers.CSGameModeService.IsStoryMode();

        /// <summary>
        ///     True when this mod, not the game's own starting-options behavior, grants
        ///     the start. The Vanilla route is exactly the promise that it does not, so
        ///     the game keeps whatever its own screen was told to do.
        /// </summary>
        public static bool ModOwnsTheStart() => CreationSession.Current.Mode != SetupMode.Vanilla;
    }
}
