using System.Collections.Generic;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>
    ///     What the player has answered so far, as a scene's appearance predicate
    ///     sees it. Read-only on purpose: a predicate that could write would make
    ///     the branch depend on how often it was asked, and the run asks a
    ///     predicate as many times as it likes.
    ///
    ///     Free of engine types, so a scene set can be walked end to end outside a
    ///     running game.
    /// </summary>
    public interface ISceneAnswers
    {
        /// <summary>Every option chosen, in the order it was chosen.</summary>
        IReadOnlyList<string> ChosenOptionIds { get; }

        /// <summary>
        ///     Whether this exact option was chosen. This is the whole vocabulary a
        ///     branch has: a scene asks about answers, never about the person the
        ///     answers add up to, because that reading belongs to whatever consumes
        ///     the consequences and would be a second definition of the character
        ///     if a branch could make it too.
        /// </summary>
        bool Chose(string optionId);
    }
}
