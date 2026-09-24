using System;
using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>
    ///     One line written a second time, for the lives a named state holds of.
    ///
    ///     A scene's prompt and an option's prose are both written before anybody
    ///     has answered anything, so both can assert something a life has since
    ///     settled otherwise: a prompt that assumes a house the first scene said
    ///     there was none of, an answer whose writing buries parents the life has
    ///     already said are living. The obvious repair is out, since the
    ///     answer may not be taken off the table, so the writing moves instead and
    ///     the answer goes on being offered.
    ///
    ///     One carrier serves both, because a prompt and a piece of prose are the
    ///     same thing to it: a string chosen by reading the answers. The state is
    ///     held as a predicate rather than as a flag somebody sets, so asking twice
    ///     gives the same answer, and <see cref="When"/> carries the script's own
    ///     label for it, which is what makes the script and the catalog comparable
    ///     line for line.
    /// </summary>
    public sealed class TextVariant
    {
        public TextVariant(string when, Func<ISceneAnswers, bool> holds, string text)
        {
            When = when;
            Holds = holds;
            Text = text;
        }

        /// <summary>The state's own name, as <c>SceneScript.md</c> writes it in backticks.</summary>
        public string When { get; }

        /// <summary>Whether this life is one of the lives this writing is for.</summary>
        public Func<ISceneAnswers, bool> Holds { get; }

        /// <summary>The line itself, in the localization id plus fallback form.</summary>
        public string Text { get; }

        public override string ToString() => When;
    }

    /// <summary>
    ///     One answer to a scene: the line on the button, the prose that stands
    ///     under it once it is picked, and what picking it leaves behind.
    ///
    ///     There is deliberately no third text field for what the option says
    ///     about the character. That sentence is derived from
    ///     <see cref="Consequences"/> where the scene is rendered, so an author
    ///     cannot write one thing and grant another; a hand-written summary would
    ///     be a second declaration of the same fact and would drift from the first.
    /// </summary>
    public sealed class SceneOption
    {
        public SceneOption(string id, string title, string prose,
            IReadOnlyList<ChoiceConsequence>? consequences = null,
            IReadOnlyList<TextVariant>? variants = null)
        {
            Id = id;
            Title = title;
            Prose = prose;
            Consequences = consequences ?? Array.Empty<ChoiceConsequence>();
            Variants = variants ?? Array.Empty<TextVariant>();
        }

        /// <summary>
        ///     Identity a later scene's predicate names to branch on this answer,
        ///     so it is unique across the whole scene set rather than within one
        ///     scene.
        /// </summary>
        public string Id { get; }

        /// <summary>The short thematic line, in the localization id plus fallback form.</summary>
        public string Title { get; }

        /// <summary>
        ///     The description shown once selected, in the same form, for a life no
        ///     variant holds of.
        /// </summary>
        public string Prose { get; }

        /// <summary>What this answer leaves behind. Empty is an ordinary answer, not a defect.</summary>
        public IReadOnlyList<ChoiceConsequence> Consequences { get; }

        /// <summary>
        ///     The other writings of <see cref="Prose"/>, in the order the script
        ///     puts them. Empty is an answer that reads the same to everybody.
        /// </summary>
        public IReadOnlyList<TextVariant> Variants { get; }

        /// <summary>
        ///     The writing this life reads, first state that holds. The caller
        ///     hands in the life with this answer's own scene held out, because an
        ///     answer that read its own scene as settled would be describing a
        ///     choice the player has not made yet: the stage records an answer the
        ///     moment the selection lands on it.
        /// </summary>
        public string ProseFor(ISceneAnswers? life)
        {
            if (life == null) return Prose;

            foreach (var variant in Variants)
                if (variant.Holds(life))
                    return variant.Text;

            return Prose;
        }

        public override string ToString() => $"{Id}[{Consequences.Count}]";
    }
}
