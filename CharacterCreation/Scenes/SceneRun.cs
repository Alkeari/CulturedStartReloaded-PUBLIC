using System.Collections.Generic;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>
    ///     Walks a scene set: which situation is in front of the player now, what
    ///     answering does, and what going back undoes.
    ///
    ///     The run holds no cursor. Where the player stands is derived from the
    ///     answers every time it is asked, as the first scene in order that this
    ///     life reaches and has not yet answered. An index would be a second
    ///     account of the same position and would disagree with the first the
    ///     moment an answer changed which scenes appear, which is the whole point
    ///     of a branching set.
    /// </summary>
    public sealed class SceneRun
    {
        private readonly IReadOnlyList<Scene> _scenes;
        private readonly SceneAnswers _answers;

        public SceneRun(IReadOnlyList<Scene> scenes, SceneAnswers? answers = null)
        {
            _scenes = scenes;
            _answers = answers ?? new SceneAnswers();
        }

        /// <summary>The scenes this run may put, in the order it considers them.</summary>
        public IReadOnlyList<Scene> Scenes => _scenes;

        public ISceneAnswers Answers => _answers;

        /// <summary>The situation in front of the player, or null when the life is told.</summary>
        public Scene? Current
        {
            get
            {
                foreach (var scene in _scenes)
                {
                    if (_answers.Answered(scene.Id)) continue;
                    if (scene.Appears(_answers)) return scene;
                }

                return null;
            }
        }

        public bool IsFinished => Current == null;

        public bool CanGoBack => _answers.InOrder.Count > 0;

        /// <summary>
        ///     Answers the current scene with one of its own options. False when
        ///     the life is told or the option belongs to some other scene, and
        ///     nothing is recorded in either case: an answer to a question that was
        ///     not asked would branch a life the player never lived.
        /// </summary>
        public bool Answer(string optionId)
        {
            var scene = Current;
            if (scene == null) return false;
            if (scene.Find(optionId) == null) return false;

            _answers.Record(scene.Id, optionId);
            return true;
        }

        /// <summary>
        ///     Puts the previous scene back in front of the player, unanswered.
        ///     False when nothing has been answered yet.
        /// </summary>
        public bool Back() => _answers.Undo() != null;

        /// <summary>
        ///     The option that answered a scene, for rendering a scene already
        ///     behind the player. Null while it stands unanswered, and null for a
        ///     scene this life never reached.
        /// </summary>
        public SceneOption? Chosen(Scene scene)
        {
            string? optionId = _answers.AnswerTo(scene.Id);
            return optionId == null ? null : scene.Find(optionId);
        }
    }
}
