using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     One life, told to its end and then read back.
    ///
    ///     Every defect the last playtest found was an END STATE defect: the code
    ///     ran, nothing threw, and the character that came out was wrong. Nothing
    ///     that asserts about the catalog can see one of those, because the catalog
    ///     was correct in every case. So this carries a whole life rather than a
    ///     scene or an option, and everything asserted about it is asserted about
    ///     what the life came to.
    /// </summary>
    internal sealed class Life
    {
        /// <summary>
        ///     The set this life was told from is carried as well as the answers,
        ///     because the reading is taken against that set's own ordinary life.
        ///     Two routes are written and the harnesses narrow either of them, so a
        ///     life read against a set it was not told from is a life nobody lived.
        /// </summary>
        internal Life(IReadOnlyList<Scene> scenes, IReadOnlyList<SceneOption> chosen)
        {
            Scenes = scenes;
            Chosen = chosen;
            Left = chosen.SelectMany(option => option.Consequences).ToList();
            Outcome = SceneOutcome.From(Left);
            Reading = Portrait.From(Left, chosen.Count, scenes);
        }

        /// <summary>The scenes this life was told from.</summary>
        public IReadOnlyList<Scene> Scenes { get; }

        /// <summary>The answers this life gave, in the order the run put them.</summary>
        public IReadOnlyList<SceneOption> Chosen { get; }

        /// <summary>Everything those answers left behind, which is what the panel stated one line at a time.</summary>
        public IReadOnlyList<ChoiceConsequence> Left { get; }

        /// <summary>The same list sorted into the kinds the pipeline applies.</summary>
        public SceneOutcome Outcome { get; }

        /// <summary>The station and age the life reads as, which is what the run writes onto the session.</summary>
        public Portrait Reading { get; }

        /// <summary>
        ///     The life as a line a person can replay by hand. Every assertion below
        ///     carries this, because "some life somewhere" is not a bug report.
        /// </summary>
        public string Trail => string.Join(" > ", Chosen.Select(option => option.Id));

        public override string ToString() => Trail;
    }

    /// <summary>
    ///     Walks the guided route. The catalog branches, so one walk proves almost
    ///     nothing: everything here is asked of thousands of lives, and every life
    ///     is a legal one that a player could actually have lived.
    /// </summary>
    internal static class LifeHarness
    {
        /// <summary>
        ///     Lives walked per sweep. Large enough that every option is met many
        ///     times over and the rarer gate combinations turn up; small enough that
        ///     the whole suite still finishes in the time a test run is allowed to
        ///     take.
        /// </summary>
        public const int Sweep = 4000;

        /// <summary>
        ///     A gate shuts an option for the rest of the life, and the run itself
        ///     does not enforce that: <see cref="SceneRun"/> takes any answer the
        ///     scene owns, and it is the menu builder that asks
        ///     <c>ChoiceGates.Allows</c> before offering one. That call needs a
        ///     creation session and the whole engine behind it, so the one rule it
        ///     applies to the scene half is restated here. Restating it is what
        ///     makes a walked life a life a player could have lived rather than one
        ///     the menus would never have offered.
        /// </summary>
        private static bool Shuts(ChoiceConsequence consequence) =>
            consequence.Kind == ConsequenceKind.Gate && consequence.Target != null;

        /// <summary>
        ///     One life, with a chooser that says which of the answers still on the
        ///     table this player gave.
        /// </summary>
        public static Life Walk(Func<Scene, IReadOnlyList<SceneOption>, SceneOption> choose) =>
            Walk(SceneCatalog.All, choose);

        /// <summary>
        ///     The same, over a run handed in rather than the installed one, so a
        ///     property can be asked of the run a War Sails owner walks as well as
        ///     of the one everybody else does.
        /// </summary>
        public static Life Walk(IReadOnlyList<Scene> scenes,
            Func<Scene, IReadOnlyList<SceneOption>, SceneOption> choose)
        {
            var answers = new SceneAnswers();
            var run = new SceneRun(scenes, answers);
            var chosen = new List<SceneOption>();
            var shut = new HashSet<string>(StringComparer.Ordinal);

            while (!run.IsFinished)
            {
                var scene = run.Current!;
                var offered = scene.Options.Where(option => !shut.Contains(option.Id)).ToList();
                if (offered.Count == 0)
                    throw new InvalidOperationException(
                        $"{scene.Id} was gated down to nothing, so this life cannot be told");

                var pick = choose(scene, offered);
                if (!run.Answer(pick.Id))
                    throw new InvalidOperationException(
                        $"{scene.Id} refused {pick.Id}, which it offered");

                chosen.Add(pick);
                foreach (var consequence in pick.Consequences)
                    if (Shuts(consequence))
                        shut.Add(consequence.Target!);
            }

            return new Life(scenes, chosen);
        }

        /// <summary>
        ///     A sweep of lives. Deterministic on purpose: a harness that finds a
        ///     broken character on one run and a clean one on the next reports a
        ///     coin toss rather than a defect, and the trail in the failure message
        ///     has to still name that life tomorrow.
        /// </summary>
        public static IReadOnlyList<Life> Lives(int count = Sweep, int seed = 20260910) =>
            Lives(SceneCatalog.All, count, seed);

        /// <summary>A sweep over a run handed in rather than the installed one.</summary>
        public static IReadOnlyList<Life> Lives(IReadOnlyList<Scene> scenes,
            int count = Sweep, int seed = 20260910)
        {
            var lives = new List<Life>(count);
            var roll = new Roll(seed);

            for (int i = 0; i < count; i++)
                lives.Add(Walk(scenes, (_, offered) => offered[roll.Next(offered.Count)]));

            return lives;
        }

        /// <summary>
        ///     One life per answer in the catalog, each one taking that answer
        ///     wherever the gates allow it. A sweep meets the common answers
        ///     thousands of times and an answer behind two gates only rarely, so
        ///     this is what makes the coverage claim true rather than likely.
        /// </summary>
        public static IReadOnlyList<Life> LifePerAnswer(int seed = 5150)
        {
            var lives = new List<Life>();
            var roll = new Roll(seed);

            foreach (var scene in SceneCatalog.All)
            foreach (var wanted in scene.Options)
            {
                var life = Walk((asked, offered) =>
                {
                    if (asked.Id != scene.Id) return offered[roll.Next(offered.Count)];

                    // The wanted answer where this life may still give it, and
                    // anything else where an earlier answer shut it: a life that
                    // cannot reach the answer is still a life, and the reachability
                    // assertion is the one that cares which it was
                    foreach (var option in offered)
                        if (option.Id == wanted.Id)
                            return option;

                    return offered[roll.Next(offered.Count)];
                });

                lives.Add(life);
            }

            return lives;
        }

        /// <summary>
        ///     A pseudo-random source of its own rather than <c>System.Random</c>,
        ///     whose sequence the framework is free to change between releases. The
        ///     trail in a failure message has to name the same life next year.
        /// </summary>
        private sealed class Roll
        {
            private uint _state;

            public Roll(int seed) => _state = (uint)seed | 1u;

            public int Next(int bound)
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (int)(_state % (uint)bound);
            }
        }
    }
}
