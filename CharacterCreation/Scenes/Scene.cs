using System;
using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>
    ///     One situation put to the player, with the answers it accepts.
    ///
    ///     A scene never asks what the player is, only what they do, and what the
    ///     answer says about them is inferred from the option's consequences
    ///     rather than declared in the prompt.
    /// </summary>
    public sealed class Scene
    {
        public Scene(string id, string title, string prompt, IReadOnlyList<SceneOption> options,
            int? age,
            Func<ISceneAnswers, bool>? appears = null,
            Severity severity = Severity.Formative,
            IReadOnlyList<TextVariant>? promptVariants = null)
        {
            Id = id;
            Title = title;
            Prompt = prompt;
            Options = options;
            Age = age;
            Appears = appears ?? (_ => true);
            Everyone = appears == null;
            Severity = severity;
            PromptVariants = promptVariants ?? Array.Empty<TextVariant>();
        }

        public string Id { get; }

        /// <summary>
        ///     The header this scene puts at the top of the stage's panel, in the
        ///     localization id plus fallback form.
        ///
        ///     Carried by the scene rather than looked up by its id, because a
        ///     scene that stands in another's place under that id would otherwise
        ///     be given its predecessor's heading.
        ///
        ///     The prompt cannot serve as one. The vanilla stage draws the title in
        ///     a fixed 670 by 55 widget and the description in a scrolling panel
        ///     that covers its children, so a paragraph placed in the title is cut
        ///     off after one line while the same paragraph in the description wraps
        ///     and scrolls.
        /// </summary>
        public string Title { get; }

        /// <summary>The situation itself, in the localization id plus fallback form.</summary>
        public string Prompt { get; }

        /// <summary>
        ///     How old the character is in this scene, which is the year of their
        ///     life it happens in. Null where the script's own stage direction
        ///     puts the scene at whatever age the life has reached by then, which
        ///     the last scene is and nothing else is.
        ///
        ///     Carried by the scene for the same reason <see cref="Title"/> is: a
        ///     table keyed by the menu id gave a scene standing in another's place
        ///     under that id its predecessor's heading, and an age looked up the
        ///     same way would be the same defect drawn on the character instead of
        ///     written above them. The stage direction in <c>SceneScript.md</c> is
        ///     where the number is authored and <c>SceneCatalogTests</c> reads the
        ///     two back against each other, so the render can never be one age
        ///     while the scene is written for another.
        /// </summary>
        public int? Age { get; }

        public IReadOnlyList<SceneOption> Options { get; }

        /// <summary>
        ///     How much of a character this scene settles, as data rather than as a
        ///     line in the script and a doc comment.
        ///
        ///     Held here so the claim can be checked: a label that lives only in
        ///     prose cannot disagree with anything, so a scene drifting away from
        ///     the weight its label promises is invisible until a player meets a
        ///     hinge that decides nothing. The test weighs the options and asserts
        ///     the bands do not overlap.
        /// </summary>
        public Severity Severity { get; }

        /// <summary>
        ///     Whether this scene is put to this player at all, given what they
        ///     have already answered. A scene that is not shown is not a scene the
        ///     player skipped: it never belonged to their life, so nothing later
        ///     may treat its absence as an answer.
        ///
        ///     Held as a predicate over the answers rather than as a flag the run
        ///     sets, so walking the same answers twice reaches the same scene.
        /// </summary>
        public Func<ISceneAnswers, bool> Appears { get; }

        /// <summary>
        ///     Whether this scene is put to every life rather than to some of
        ///     them. Held as a fact rather than worked out by asking
        ///     <see cref="Appears"/>, which can only ever answer for one life:
        ///     a reader that has to reason about the lives it has not seen needs
        ///     to know that there are none.
        /// </summary>
        public bool Everyone { get; }

        /// <summary>
        ///     The other writings of <see cref="Prompt"/>, in the order the script
        ///     puts them. A prompt carries no consequences, so nothing downstream
        ///     can weigh it against the character and nothing but this can keep it
        ///     honest: the situation a scene puts is the one place a run can assert
        ///     a house an earlier answer said there was none of.
        /// </summary>
        public IReadOnlyList<TextVariant> PromptVariants { get; }

        /// <summary>The situation as this life meets it, first state that holds.</summary>
        public string PromptFor(ISceneAnswers? life)
        {
            if (life == null) return Prompt;

            foreach (var variant in PromptVariants)
                if (variant.Holds(life))
                    return variant.Text;

            return Prompt;
        }

        /// <summary>
        ///     The life as it stands with nothing this scene settled in it.
        ///
        ///     What a variant reads has to be the answers ALREADY GIVEN, and the
        ///     run carries more than those: the stage records an answer the moment
        ///     the selection lands on an option, so by the time this scene's own
        ///     writing is composed the run already holds the very answer being
        ///     described, and walking back into the scene leaves its earlier answer
        ///     in as well. Both belong to this scene rather than to the life the
        ///     writing is read against, so the scene is held out whole, which is how
        ///     <c>Services/ChoiceEffects</c> reads the same run.
        /// </summary>
        public ISceneAnswers LifeBefore(ISceneAnswers? answers)
        {
            var before = new List<string>();

            if (answers != null)
                foreach (string optionId in answers.ChosenOptionIds)
                    if (Find(optionId) == null)
                        before.Add(optionId);

            return new Earlier(before);
        }

        /// <summary>This scene's option with that id, or null when it has none.</summary>
        public SceneOption? Find(string optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return null;

            foreach (var option in Options)
                if (string.Equals(option.Id, optionId, StringComparison.Ordinal))
                    return option;

            return null;
        }

        public override string ToString() => Id;

        /// <summary>A fixed list of answers, which is what "already given" is.</summary>
        private sealed class Earlier : ISceneAnswers
        {
            private readonly IReadOnlyList<string> _chosen;

            public Earlier(IReadOnlyList<string> chosen) => _chosen = chosen;

            public IReadOnlyList<string> ChosenOptionIds => _chosen;

            public bool Chose(string optionId)
            {
                if (string.IsNullOrEmpty(optionId)) return false;

                foreach (string chosen in _chosen)
                    if (string.Equals(chosen, optionId, StringComparison.Ordinal))
                        return true;

                return false;
            }
        }
    }
}
