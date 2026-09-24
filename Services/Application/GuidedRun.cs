using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     What the guided route answered, for the steps that apply it.
    ///
    ///     One place, because a step that gathered the answers itself would be a
    ///     second account of the same run and the two would disagree the moment a
    ///     scene changed. The outcome of a run that is not guided is empty rather
    ///     than absent, so a step never has to ask which route it is on before
    ///     reading it.
    ///
    ///     Answers alone do not make a run guided. The route is chosen on
    ///     `cs_mode_menu` and the game's own Back button reaches it from the first
    ///     scene, so a player can answer a scene, back out, and build the whole
    ///     character by hand in the Start Editor instead; the answers they left
    ///     behind are not part of that character. The session's mode is what
    ///     decides, and the answers only say whether there is anything to read.
    /// </summary>
    public static class GuidedRun
    {
        /// <summary>True while this run told its life through the scenes.</summary>
        public static bool WasWalked => WasWalkedBy(CreationSession.Current);

        /// <summary>The same question asked of one session rather than the live one.</summary>
        public static bool WasWalkedBy(CharacterCreationSession? session) =>
            session != null &&
            GuidedRoute.IsGuided(session.Mode) &&
            SceneReading.Answered(SceneMenus.Answered) > 0;

        public static SceneOutcome Outcome() =>
            WasWalked
                ? SceneOutcome.From(SceneReading.Consequences(GuidedRoute.Scenes, SceneMenus.Answered))
                : SceneOutcome.From(null);
    }
}
