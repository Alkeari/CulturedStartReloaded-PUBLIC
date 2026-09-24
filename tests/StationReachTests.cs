using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Whether the reachability walk tells the truth.
    ///
    ///     The panel states a closure to the player as a fact, so a station it
    ///     calls closed has to be one no combination of the remaining answers can
    ///     bring back to first place. The whole run is far too wide to enumerate,
    ///     which is exactly why the walk exists, so the claim is proved where
    ///     enumeration IS possible: the same thirteen scenes with their answers
    ///     cut down to two or three apiece, where every completion can be walked
    ///     by hand and the two answers compared outright.
    ///
    ///     The reduced run is not a toy fixture. It is the real catalog, the real
    ///     consequences, the real gates and the real thirteen-scene length, with
    ///     some answers withheld, so every number the walk reads is a number the
    ///     shipped catalog produces.
    /// </summary>
    public sealed class StationReachTests
    {
        private readonly ITestOutputHelper _out;

        public StationReachTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        ///     The real scenes with all but the first few answers withheld.
        ///
        ///     A scene put to everybody is rebuilt as one, rather than as one
        ///     carrying a predicate that happens to hold: the walk bounds a scene
        ///     some lives skip more loosely than one every life meets, so handing
        ///     it predicates here would prove the loose reading and leave the one
        ///     the shipped run uses unproven.
        /// </summary>
        private static IReadOnlyList<Scene> Narrowed(int keep) =>
            SceneCatalog.All
                .Select(scene => new Scene(scene.Id, scene.Title, scene.Prompt,
                    scene.Options.Take(keep).ToList(), scene.Age,
                    scene.Everyone ? null : scene.Appears, scene.Severity, scene.PromptVariants))
                .ToList();

        /// <summary>
        ///     The shipped run puts every scene to every life, which is the reading
        ///     the comparisons below are walked under. A scene set that branched
        ///     would be bounded more loosely and would leave them proving something
        ///     else.
        /// </summary>
        [Fact]
        public void Every_scene_of_the_shipped_run_is_put_to_everybody()
        {
            SceneCatalog.All.Should().OnlyContain(scene => scene.Everyone);
            Narrowed(2).Should().OnlyContain(scene => scene.Everyone);
        }

        private static bool Shuts(ChoiceConsequence consequence) =>
            consequence.Kind == ConsequenceKind.Gate && !string.IsNullOrEmpty(consequence.Target);

        /// <summary>
        ///     Every station some completion of this prefix really finishes on,
        ///     found by walking every completion there is.
        /// </summary>
        private static HashSet<StartType> Enumerated(IReadOnlyList<Scene> scenes,
            IReadOnlyList<SceneOption> prefix)
        {
            var found = new HashSet<StartType>();
            var chosen = new List<SceneOption>(prefix);
            var shut = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var option in prefix)
            foreach (var consequence in option.Consequences)
                if (Shuts(consequence))
                {
                    shut.TryGetValue(consequence.Target!, out int seen);
                    shut[consequence.Target!] = seen + 1;
                }

            void Walk(int at)
            {
                if (at >= scenes.Count)
                {
                    var left = chosen.SelectMany(option => option.Consequences).ToList();
                    found.Add(Portrait.From(left, chosen.Count, scenes).Station);
                    return;
                }

                foreach (var option in scenes[at].Options)
                {
                    if (shut.TryGetValue(option.Id, out int closed) && closed > 0) continue;

                    chosen.Add(option);
                    foreach (var consequence in option.Consequences)
                        if (Shuts(consequence))
                        {
                            shut.TryGetValue(consequence.Target!, out int seen);
                            shut[consequence.Target!] = seen + 1;
                        }

                    Walk(at + 1);

                    foreach (var consequence in option.Consequences)
                        if (Shuts(consequence))
                            shut[consequence.Target!] -= 1;

                    chosen.RemoveAt(chosen.Count - 1);
                }
            }

            Walk(prefix.Count);
            return found;
        }

        private static IReadOnlyList<SceneOption> Prefix(IReadOnlyList<Scene> scenes, Random dice, int depth)
        {
            var chosen = new List<SceneOption>();
            var shut = new HashSet<string>(StringComparer.Ordinal);

            for (int at = 0; at < depth; at++)
            {
                var offered = scenes[at].Options.Where(option => !shut.Contains(option.Id)).ToList();
                var pick = offered[dice.Next(offered.Count)];

                chosen.Add(pick);
                foreach (var consequence in pick.Consequences)
                    if (Shuts(consequence))
                        shut.Add(consequence.Target!);
            }

            return chosen;
        }

        /// <summary>
        ///     The walk and a full enumeration of the same run must name exactly
        ///     the same stations, at every depth. Two answers to the thirteenth
        ///     scene, so the whole tree under any prefix can be walked.
        /// </summary>
        [Fact]
        public void Two_answers_a_scene_the_walk_matches_a_full_enumeration()
        {
            var scenes = Narrowed(2);
            var dice = new Random(20260911);
            int compared = 0;

            for (int depth = 0; depth <= scenes.Count; depth++)
            for (int life = 0; life < 8; life++)
            {
                var prefix = Prefix(scenes, dice, depth);
                var walked = Portrait.StillOpen(scenes, prefix.Select(option => option.Id).ToList());
                var enumerated = Enumerated(scenes, prefix);

                walked.Should().BeEquivalentTo(enumerated,
                    $"the walk and the enumeration read one run [{string.Join(">", prefix.Select(o => o.Id))}]");
                compared++;
            }

            _out.WriteLine($"two answers a scene: {compared} prefixes agreed exactly");
        }

        /// <summary>
        ///     The same, three answers a scene, from the sixth on so the tree under
        ///     a prefix stays walkable.
        /// </summary>
        [Fact]
        public void Three_answers_a_scene_the_walk_matches_a_full_enumeration()
        {
            var scenes = Narrowed(3);
            var dice = new Random(11092026);
            int compared = 0;
            int closedSeen = 0;

            for (int depth = 6; depth <= scenes.Count; depth++)
            for (int life = 0; life < 6; life++)
            {
                var prefix = Prefix(scenes, dice, depth);
                var walked = Portrait.StillOpen(scenes, prefix.Select(option => option.Id).ToList());
                var enumerated = Enumerated(scenes, prefix);

                walked.Should().BeEquivalentTo(enumerated,
                    $"the walk and the enumeration read one run [{string.Join(">", prefix.Select(o => o.Id))}]");

                compared++;
                closedSeen += 8 - walked.Count;
            }

            closedSeen.Should().BeGreaterThan(0,
                "a comparison where nothing is ever closed proves the walk agrees about the easy half only");
            _out.WriteLine($"three answers a scene: {compared} prefixes agreed exactly, " +
                           $"{closedSeen} closures between them");
        }

        /// <summary>
        ///     On the shipped run, a station the walk calls closed must be one no
        ///     life actually finishes on. Enumeration is out of reach there, so
        ///     this walks lives instead: every completion of a prefix that a player
        ///     could really give has to end on a station the walk left open.
        /// </summary>
        [Fact]
        public void No_life_finishes_on_a_station_the_walk_closed()
        {
            var scenes = SceneCatalog.All;
            var dice = new Random(4711);
            int checkedLives = 0;

            for (int run = 0; run < 400; run++)
            {
                int depth = dice.Next(1, scenes.Count);
                var prefix = Prefix(scenes, dice, depth);
                var open = Portrait.StillOpen(scenes, prefix.Select(option => option.Id).ToList());

                for (int completion = 0; completion < 40; completion++)
                {
                    var whole = Prefix(scenes, dice, scenes.Count);
                    var life = prefix.Concat(whole.Skip(depth)).ToList();

                    // The tail was drawn against its own prefix, so it may name an
                    // answer this life already had shut; only a legal life counts
                    if (!Legal(life)) continue;

                    var station = Portrait.From(
                        life.SelectMany(option => option.Consequences).ToList(), life.Count, scenes).Station;

                    open.Should().Contain(station,
                        $"[{string.Join(">", life.Select(o => o.Id))}] finishes there");
                    checkedLives++;
                }
            }

            checkedLives.Should().BeGreaterThan(1000);
            _out.WriteLine($"{checkedLives} whole lives finished on a station the walk had left open");
        }

        private static bool Legal(IReadOnlyList<SceneOption> life)
        {
            var shut = new HashSet<string>(StringComparer.Ordinal);

            foreach (var option in life)
            {
                if (shut.Contains(option.Id)) return false;

                foreach (var consequence in option.Consequences)
                    if (Shuts(consequence))
                        shut.Add(consequence.Target!);
            }

            return true;
        }

        /// <summary>
        ///     What the panel pays for the line, and how often the line has
        ///     anything to say. The panel is recomposed whenever the selection
        ///     moves, so the cost that matters is one option's worth.
        /// </summary>
        [Fact]
        public void The_line_is_cheap_enough_to_compose_when_the_selection_moves()
        {
            var scenes = SceneCatalog.All;
            var dice = new Random(90210);

            // Warm the scene set up, so the measurement is the walk rather than
            // the one-off measuring of the run
            Portrait.StillOpen(scenes, Array.Empty<string>());

            var worst = TimeSpan.Zero;
            double total = 0;
            int asked = 0;
            int closing = 0;
            var closures = new Dictionary<int, int>();
            var howMany = new Dictionary<int, int>();

            for (int run = 0; run < 120; run++)
            {
                var chosen = new List<string>();
                var shut = new HashSet<string>(StringComparer.Ordinal);

                for (int at = 0; at < scenes.Count; at++)
                {
                    var offered = scenes[at].Options.Where(option => !shut.Contains(option.Id)).ToList();

                    foreach (var option in offered)
                    {
                        var clock = Stopwatch.StartNew();
                        var closed = Portrait.ClosedBy(scenes, chosen, option.Id);
                        clock.Stop();

                        if (clock.Elapsed > worst) worst = clock.Elapsed;
                        total += clock.Elapsed.TotalMilliseconds;
                        asked++;

                        if (closed.Count <= 0) continue;

                        closing++;
                        closures.TryGetValue(at, out int seen);
                        closures[at] = seen + 1;
                        howMany.TryGetValue(closed.Count, out int often);
                        howMany[closed.Count] = often + 1;
                    }

                    var pick = offered[dice.Next(offered.Count)];
                    chosen.Add(pick.Id);
                    foreach (var consequence in pick.Consequences)
                        if (Shuts(consequence))
                            shut.Add(consequence.Target!);
                }
            }

            _out.WriteLine($"{asked} answers weighed, {total / asked:F3} ms each on average, " +
                           $"{worst.TotalMilliseconds:F1} ms at worst");
            _out.WriteLine($"{closing} of them closed a beginning ({100.0 * closing / asked:F1} percent)");
            foreach (var scene in closures.OrderBy(pair => pair.Key))
                _out.WriteLine($"  scene {scene.Key + 1}: {scene.Value}");
            foreach (var many in howMany.OrderBy(pair => pair.Key))
                _out.WriteLine($"  {many.Key} closed at once: {many.Value}");

            worst.TotalMilliseconds.Should().BeLessThan(250,
                "the panel is recomposed the moment the selection moves");
        }
    }
}
