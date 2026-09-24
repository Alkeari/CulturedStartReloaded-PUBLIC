using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.CharacterCreation.Scenes
{
    /// <summary>
    ///     What a set of answers left behind, gathered in the order it was chosen.
    ///
    ///     Everything downstream of the guided route reads the character from this
    ///     one list rather than from the answers themselves, so there is a single
    ///     account of what a life came to. A reader that walked the scenes on its
    ///     own would be a second account and would disagree the moment a scene
    ///     changed which options it offered.
    /// </summary>
    public static class SceneReading
    {
        public static IReadOnlyList<ChoiceConsequence> Consequences(
            IReadOnlyList<Scene> scenes, ISceneAnswers answers)
        {
            var gathered = new List<ChoiceConsequence>();
            foreach (var option in Chosen(scenes, answers))
                gathered.AddRange(option.Consequences);

            return gathered;
        }

        /// <summary>
        ///     The answers themselves, in the order they were given, for whatever
        ///     reads the life back to the player rather than applying it.
        /// </summary>
        public static IReadOnlyList<SceneOption> Chosen(
            IReadOnlyList<Scene> scenes, ISceneAnswers answers)
        {
            var chosen = new List<SceneOption>();
            if (scenes == null || answers == null) return chosen;

            foreach (string optionId in answers.ChosenOptionIds)
            foreach (var scene in scenes)
            {
                var option = scene.Find(optionId);
                if (option == null) continue;

                chosen.Add(option);
                break;
            }

            return chosen;
        }

        /// <summary>How many scenes this life actually answered.</summary>
        public static int Answered(ISceneAnswers answers) =>
            answers?.ChosenOptionIds.Count ?? 0;
    }
}
