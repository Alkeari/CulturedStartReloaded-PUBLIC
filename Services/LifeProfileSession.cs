using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The half of <see cref="LifeProfile"/> that knows about the creation
    ///     session. It is separate so the deriving half stays free of engine types
    ///     and can be compiled into the test project, which is the only way any of
    ///     this is verifiable without launching the game.
    /// </summary>
    public sealed partial class LifeProfile
    {
        /// <summary>
        ///     What this run's answers add up to, and nothing when it has no
        ///     answers of its own.
        ///
        ///     Answers left behind by a guided run the player backed out of are not
        ///     this character's life, so the session's route decides and the answers
        ///     only say whether there is anything to read. A run that told no life
        ///     reads as the untold one rather than as a life of its own.
        /// </summary>
        public static LifeProfile From(CharacterCreationSession? session) =>
            Application.GuidedRun.WasWalkedBy(session)
                ? From(SceneReading.Consequences(Application.GuidedRoute.ScenesOf(session), SceneMenus.Answered))
                : From((IReadOnlyList<ChoiceConsequence>?)null);
    }
}
