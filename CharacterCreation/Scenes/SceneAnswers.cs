using System;
using System.Collections.Generic;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>One scene and the option that answered it.</summary>
    public sealed class SceneAnswer
    {
        public SceneAnswer(string sceneId, string optionId)
        {
            SceneId = sceneId;
            OptionId = optionId;
        }

        public string SceneId { get; }
        public string OptionId { get; }

        public override string ToString() => $"{SceneId}={OptionId}";
    }

    /// <summary>
    ///     The answers themselves, and the only thing in this folder that changes.
    ///     Everything else reads them through <see cref="ISceneAnswers"/>, so the
    ///     one place that can rewrite a life is the one the run holds.
    ///
    ///     An answer carries the scene it answered as well as the option, because
    ///     going back has to know what to take away, and two scenes may be dropped
    ///     from a life by the same earlier choice.
    /// </summary>
    public sealed class SceneAnswers : ISceneAnswers
    {
        private readonly List<SceneAnswer> _answers = new();

        public SceneAnswers()
        {
        }

        public SceneAnswers(IEnumerable<SceneAnswer> answers)
        {
            _answers.AddRange(answers);
        }

        /// <summary>Every answer, oldest first.</summary>
        public IReadOnlyList<SceneAnswer> InOrder => _answers;

        public IReadOnlyList<string> ChosenOptionIds
        {
            get
            {
                var ids = new string[_answers.Count];
                for (int i = 0; i < _answers.Count; i++)
                    ids[i] = _answers[i].OptionId;

                return ids;
            }
        }

        public bool Chose(string optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return false;

            foreach (var answer in _answers)
                if (string.Equals(answer.OptionId, optionId, StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>The option that answered this scene, or null while it stands unanswered.</summary>
        public string? AnswerTo(string sceneId)
        {
            foreach (var answer in _answers)
                if (string.Equals(answer.SceneId, sceneId, StringComparison.Ordinal))
                    return answer.OptionId;

            return null;
        }

        public bool Answered(string sceneId) => AnswerTo(sceneId) != null;

        public void Record(string sceneId, string optionId) =>
            _answers.Add(new SceneAnswer(sceneId, optionId));

        /// <summary>
        ///     Answers a scene, replacing any answer it already had and discarding
        ///     everything answered after it. A player who backs up and chooses
        ///     differently has changed their mind, and the answers that followed
        ///     belong to a life they are no longer living: some of those scenes may
        ///     not even be put again.
        /// </summary>
        public void Replace(string sceneId, string optionId)
        {
            for (int i = 0; i < _answers.Count; i++)
            {
                if (!string.Equals(_answers[i].SceneId, sceneId, StringComparison.Ordinal)) continue;

                _answers.RemoveRange(i, _answers.Count - i);
                break;
            }

            _answers.Add(new SceneAnswer(sceneId, optionId));
        }

        /// <summary>Forgets everything, for a creation run that starts over.</summary>
        public void Clear() => _answers.Clear();

        /// <summary>
        ///     Takes back the newest answer and returns it, or returns null when
        ///     nothing has been answered. Newest only: an older answer can decide
        ///     whether a younger scene was ever put, so removing one out of order
        ///     would leave answers to scenes this life no longer contains.
        /// </summary>
        public SceneAnswer? Undo()
        {
            if (_answers.Count == 0) return null;

            var last = _answers[_answers.Count - 1];
            _answers.RemoveAt(_answers.Count - 1);
            return last;
        }
    }
}
