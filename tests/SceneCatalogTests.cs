using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The catalog is transcribed from a script by hand, so the failures it can
    ///     carry are transcription failures: an id typed twice, a gate pointing at
    ///     an option that no longer exists, a localization key reused so two
    ///     different lines show the same text in game.
    ///
    ///     None of those is a compile error and none of them throws. Each one is a
    ///     player meeting the wrong sentence, which is why they are asserted here
    ///     rather than left to a playthrough to find.
    ///
    ///     There are two runs, not one. War Sails replaces the ninth scene rather
    ///     than adding a fourteenth, so the catalog builds either the town scene or
    ///     the waterfront one in that slot, and a property that holds for the run a
    ///     player without the DLC walks says nothing about the run an owner walks.
    ///     Every property below is therefore asserted against both, which is what
    ///     the <c>warSails</c> argument on each of them is.
    /// </summary>
    public class SceneCatalogTests
    {
        private static IReadOnlyList<Scene> Run(bool warSails) => SceneCatalog.Build(warSails);

        private static readonly Regex Tag = new(@"^\{=([A-Za-z0-9_]+)\}(.+)$", RegexOptions.Singleline);

        private static IEnumerable<SceneOption> OptionsOf(IReadOnlyList<Scene> scenes) =>
            scenes.SelectMany(scene => scene.Options);

        /// <summary>
        ///     Every line the run can put in front of a player, the writing that
        ///     stands in for a line on the lives a state holds of included. A
        ///     variant is a line somebody reads, so everything asked of the plain
        ///     writing is asked of it.
        /// </summary>
        private static IEnumerable<string> Readable(IReadOnlyList<Scene> scenes) =>
            scenes.Select(scene => scene.Title)
                .Concat(scenes.Select(scene => scene.Prompt))
                .Concat(scenes.SelectMany(scene => scene.PromptVariants).Select(variant => variant.Text))
                .Concat(OptionsOf(scenes).Select(option => option.Title))
                .Concat(OptionsOf(scenes).Select(option => option.Prose))
                .Concat(OptionsOf(scenes).SelectMany(option => option.Variants).Select(variant => variant.Text));

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_scene_id_is_its_own(bool warSails)
        {
            Run(warSails).Select(scene => scene.Id).Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_option_id_is_its_own_across_the_whole_run(bool warSails)
        {
            // Scene-wide rather than per scene: a later scene's gate names an
            // option by id alone, so two scenes sharing one would shut both
            OptionsOf(Run(warSails)).Select(option => option.Id).Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_gate_points_at_an_option_that_exists(bool warSails)
        {
            var scenes = Run(warSails);
            var known = OptionsOf(scenes).Select(option => option.Id).ToHashSet();

            var gates = OptionsOf(scenes)
                .SelectMany(option => option.Consequences)
                .Where(c => c.Kind == ConsequenceKind.Gate)
                .ToList();

            gates.Should().NotBeEmpty("a run where nothing an answer does shuts anything later is not a branching one");
            gates.Select(gate => gate.Target).Should().OnlyContain(target => target != null && known.Contains(target));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_gate_never_shuts_an_option_in_its_own_scene(bool warSails)
        {
            foreach (var scene in Run(warSails))
            {
                var shut = scene.Options
                    .SelectMany(option => option.Consequences)
                    .Where(c => c.Kind == ConsequenceKind.Gate)
                    .Select(c => c.Target);

                foreach (string? target in shut)
                    scene.Find(target!).Should().BeNull(
                        "an option cannot shut one the player is choosing between right now");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_scene_can_be_gated_down_to_nothing(bool warSails)
        {
            // Every gate in the catalog standing at once is not a life anybody can
            // live, but it is the worst case the menu has to survive now that the
            // gates are enforced: a scene whose options are all shut puts an empty
            // list in front of the player and the run cannot go on from it.
            var scenes = Run(warSails);
            var shut = OptionsOf(scenes)
                .SelectMany(option => option.Consequences)
                .Where(c => c.Kind == ConsequenceKind.Gate)
                .Select(c => c.Target)
                .ToHashSet();

            foreach (var scene in scenes)
                scene.Options.Count(option => !shut.Contains(option.Id))
                    .Should().BeGreaterThan(0, $"{scene.Id} would have no answer left to give");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_gate_says_why_the_option_is_gone(bool warSails)
        {
            var gates = OptionsOf(Run(warSails))
                .SelectMany(option => option.Consequences)
                .Where(c => c.Kind == ConsequenceKind.Gate);

            gates.Should().OnlyContain(gate => !string.IsNullOrWhiteSpace(gate.Note));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_line_the_player_reads_is_localized_and_says_something(bool warSails)
        {
            foreach (string text in Readable(Run(warSails)))
                Tag.IsMatch(text).Should().BeTrue($"'{text}' is shown to a player and needs a key and text");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_two_lines_share_a_localization_key(bool warSails)
        {
            Readable(Run(warSails))
                .Select(text => Tag.Match(text).Groups[1].Value)
                .Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void The_two_runs_never_share_a_localization_key_either()
        {
            // The two ninth scenes are compiled into one assembly and both sit in
            // the string file, so a key reused between them is a line that reads
            // correctly in whichever run was checked and wrongly in the other
            Keys(Run(false)[8]).Should().NotIntersectWith(Keys(Run(true)[8]),
                "the town scene and the waterfront scene are different text under one id");
        }

        [Fact]
        public void The_ninth_scene_is_not_shown_under_the_heading_of_the_scene_it_replaces()
        {
            // The waterfront scene stands in the town scene's place under that
            // scene's id, so anything looked up by id gives it the town scene's
            // name: a War Sails owner was shown "What the Town Said" over a
            // waterfront. The heading is carried on the scene for that reason
            Run(true)[8].Title.Should().NotBe(Run(false)[8].Title,
                "the two ninth scenes are different situations and cannot share a heading");
        }

        /// <summary>Every localization key one scene puts in front of a player.</summary>
        private static IEnumerable<string> Keys(Scene scene) =>
            Readable(new[] { scene }).Select(text => Tag.Match(text).Groups[1].Value);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_answer_leaves_something_behind(bool warSails)
        {
            // An option that grants nothing has nothing for the effect panel to
            // state, and the route's whole promise is that a player never has to
            // work out what a choice did
            OptionsOf(Run(warSails)).Should().OnlyContain(option => option.Consequences.Count > 0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_scene_offers_a_real_choice(bool warSails)
        {
            Run(warSails).Should().OnlyContain(scene => scene.Options.Count >= 2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_life_that_answers_everything_reaches_the_end_of_the_run(bool warSails)
        {
            var run = new SceneRun(Run(warSails));
            int guard = 0;

            while (!run.IsFinished && guard++ < 100)
                run.Answer(run.Current!.Options[0].Id).Should().BeTrue();

            run.IsFinished.Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Only_traits_the_game_has_are_asked_for(bool warSails)
        {
            var known = new[] { "Mercy", "Valor", "Honor", "Generosity", "Calculating" };

            OptionsOf(Run(warSails)).SelectMany(option => option.Consequences)
                .Where(c => c.Kind == ConsequenceKind.Trait)
                .Select(c => c.Target)
                .Should().OnlyContain(target => known.Contains(target));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Only_people_the_pipeline_can_find_are_offended_or_pleased(bool warSails)
        {
            var known = new[] { "culture_lords", "town_merchants", "town_gang_leaders", "village_headmen" };

            OptionsOf(Run(warSails)).SelectMany(option => option.Consequences)
                .Where(c => c.Kind is ConsequenceKind.Goodwill or ConsequenceKind.Enmity)
                .Select(c => c.Target)
                .Should().OnlyContain(target => known.Contains(target));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_answer_is_strictly_worse_than_another_answer_to_the_same_scene(bool warSails)
        {
            // An option that gives everything a neighbor gives and less of nothing
            // is an option there is no state in which to pick. It compiles, it
            // reads well, and it is dead weight on the menu it sits in.
            var dead = new List<string>();

            foreach (var scene in Run(warSails))
            foreach (var worse in scene.Options)
            foreach (var better in scene.Options)
            {
                if (ReferenceEquals(worse, better)) continue;
                if (Dominates(better, worse)) dead.Add($"{worse.Id} is dominated by {better.Id}");
            }

            dead.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_two_scenes_offer_the_same_answer_under_different_words(bool warSails)
        {
            // Two options carrying the same consequences are one option asked
            // twice, however differently they are written: the second scene took
            // the player's time and changed nothing the first had not changed.
            var flat = Run(warSails)
                .SelectMany((scene, index) => scene.Options.Select(o => (Scene: index, Option: o)))
                .ToList();

            var twins = new List<string>();
            for (int i = 0; i < flat.Count; i++)
            for (int j = i + 1; j < flat.Count; j++)
            {
                if (flat[i].Scene == flat[j].Scene) continue;
                if (Same(flat[i].Option, flat[j].Option))
                    twins.Add($"{flat[i].Option.Id} and {flat[j].Option.Id} leave the same life behind");
            }

            twins.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_scene_levies_the_same_cost_on_every_one_of_its_answers(bool warSails)
        {
            // A debt, a lost year, an enemy and a shut road only ever take, so a
            // scene that puts one on all of its answers is not asking about it, it
            // is charging it: the player can pick how much but never whether, and
            // whatever the reading infers from it is out of their hands. That is
            // exactly what scene 12 did with LostYears, and it put a floor under
            // how young a finished character could be that no answer could lift.
            var tolls = new[]
            {
                ConsequenceKind.Debt, ConsequenceKind.Enmity,
                ConsequenceKind.LostYears, ConsequenceKind.Gate
            };

            var levied = new List<string>();

            foreach (var scene in Run(warSails))
            foreach (var toll in tolls)
            {
                if (scene.Options.All(option => option.Consequences.Any(c => c.Kind == toll)))
                    levied.Add($"{scene.Id} charges {toll} on every answer");
            }

            levied.Should().BeEmpty();
        }

        #region The sea, and who is allowed to reach it

        /// <summary>
        ///     The one target in either run that scores a sea lean. Named here so
        ///     the tests below say what they are guarding rather than checking a
        ///     number: everything about War Sails on this route hangs on nothing
        ///     else reaching <see cref="LifeProfile.Lean.Sea"/>.
        /// </summary>
        private const string TheWater = "the_open_water";

        [Fact]
        public void Without_the_dlc_the_run_is_the_one_that_shipped_before_it()
        {
            // Twelve scenes are literally the same objects in both runs, so the
            // only thing that can differ is the ninth, and this says so rather
            // than trusting that reading the builder is enough
            var ashore = Run(false);
            var afloat = Run(true);

            ashore.Should().HaveCount(13);
            afloat.Should().HaveCount(13);
            ashore.Select(s => s.Id).Should().Equal(afloat.Select(s => s.Id));

            for (int i = 0; i < ashore.Count; i++)
            {
                if (i == 8) continue;
                ReferenceEquals(ashore[i], afloat[i]).Should().BeFalse("each build makes its own scenes");
                Told(ashore[i]).Should().Equal(Told(afloat[i]),
                    $"{ashore[i].Id} is not the scene War Sails replaces and may not move");
            }

            Told(ashore[8]).Should().NotEqual(Told(afloat[8]),
                "the ninth scene is the whole of what the DLC changes, so it has to change");
        }

        /// <summary>A scene as everything a player could read and be given, flattened.</summary>
        private static IReadOnlyList<string> Told(Scene scene)
        {
            var told = new List<string> { scene.Id, scene.Title, scene.Prompt, scene.Severity.ToString() };
            told.AddRange(scene.PromptVariants.Select(variant => $"{variant.When}={variant.Text}"));

            foreach (var option in scene.Options)
            {
                told.Add(option.Id);
                told.Add(option.Title);
                told.Add(option.Prose);
                told.AddRange(option.Variants.Select(variant => $"{variant.When}={variant.Text}"));
                told.AddRange(Written(option));
            }

            return told;
        }

        [Fact]
        public void Nothing_a_player_without_the_dlc_can_answer_reaches_the_sea()
        {
            // The profile sums its answers, so an answer that cannot score the sea
            // proves no combination of answers can
            OptionsOf(Run(false))
                .Where(o => LifeProfile.From(o.Consequences).Score(LifeProfile.Lean.Sea) != 0)
                .Select(o => o.Id)
                .Should().BeEmpty();

            OptionsOf(Run(false))
                .SelectMany(o => o.Consequences)
                .Select(c => c.Target)
                .Should().NotContain(TheWater);
        }

        [Fact]
        public void With_the_dlc_the_sea_is_reachable_and_worth_reaching()
        {
            var afloat = OptionsOf(Run(true)).ToList();

            var seagoing = afloat
                .Where(o => LifeProfile.From(o.Consequences).Score(LifeProfile.Lean.Sea) > 0)
                .ToList();

            seagoing.Should().NotBeEmpty(
                "a run that owns the DLC and can still put nobody on the water is the defect this replaced");

            // Band three is what it takes for the three naval skills to outrank
            // the skills a life never went near. A reachable sea lean that leaves
            // them in the tail of the sheet is the old behavior with extra words.
            seagoing.Max(o => LifeProfile.From(o.Consequences).Band(LifeProfile.Lean.Sea))
                .Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public void Only_one_target_in_either_run_reaches_the_sea()
        {
            // A second road to the sea is how a land answer starts scoring it by
            // accident, which is the failure that has to stay impossible
            foreach (bool warSails in new[] { false, true })
            foreach (var option in OptionsOf(Run(warSails)))
            {
                if (LifeProfile.From(option.Consequences).Score(LifeProfile.Lean.Sea) == 0) continue;

                option.Consequences.Should().Contain(c => c.Target == TheWater,
                    $"{option.Id} scores a sea lean without naming the water");
            }
        }

        [Fact]
        public void The_service_the_catalog_asks_about_the_dlc_still_answers_to_that_name()
        {
            // SceneCatalog reaches NavalDLCService by name rather than by
            // reference, to keep engine types out of the tier this test compiles.
            // A rename there would silently take the sea out of the run, so the
            // name is read back off the service's own file.
            string service = File.ReadAllText(SourcePath("Services", "NavalDLCService.cs"));

            service.Should().Contain("namespace CulturedStartReloaded.Services");
            service.Should().Contain("class NavalDLCService");
            service.Should().Contain("public static bool IsNavalDLCLoaded()");

            string catalog = File.ReadAllText(SourcePath("CharacterCreation", "Catalog", "SceneCatalog.cs"));
            catalog.Should().Contain("\"CulturedStartReloaded.Services.NavalDLCService\"");
            catalog.Should().Contain("\"IsNavalDLCLoaded\"");
        }

        [Fact]
        public void A_test_run_holds_no_game_so_the_catalog_it_gets_is_the_land_one()
        {
            // Which is what lets WarSailsGatingTests read SceneCatalog.All and be
            // reading exactly what a player without the DLC is shown
            SceneCatalog.All.Select(s => s.Id).Should().Equal(Run(false).Select(s => s.Id));
            OptionsOf(SceneCatalog.All).Select(o => o.Id)
                .Should().Equal(OptionsOf(Run(false)).Select(o => o.Id));
        }

        #endregion

        #region What the player is promised

        /// <summary>
        ///     The bound a personality trait is held to in the game. The apply step
        ///     reads the live <c>TraitObject</c>'s own range rather than this, which
        ///     is why a character finishing the run is always correct; the number is
        ///     repeated here because a test cannot reach a running game, and because
        ///     what is being checked is not the character but the PROMISE. An answer
        ///     that says a life is counted more valorous, taken eight times, is eight
        ///     statements the game can keep two of.
        /// </summary>
        private const int GameHolds = 2;

        /// <summary>
        ///     Which sentence the effect panel will print for a consequence, as an
        ///     identity rather than as English.
        ///
        ///     It mirrors the branching in <c>Services/ChoiceEffects</c>, which is
        ///     where the English lives: a gate names the answer it shuts, a trait
        ///     prints its own signed figure and the range it is held in, a standing
        ///     prints the relation it is worth, an item names the thing and how
        ///     many, and a debt or a year prints its figure. Two consequences with
        ///     the same key render the same line word for word, which is what the
        ///     player is actually reading twice. Change the branching there and
        ///     this has to move with it.
        /// </summary>
        private static string LineKey(ChoiceConsequence consequence)
        {
            int amount = consequence.Amount;
            int size = Math.Abs(amount);

            switch (consequence.Kind)
            {
                // A gate's own note explains the option it SHUTS, where the menu
                // prints it; the panel line here names that option instead, so two
                // gates shutting one answer are one sentence however they are worded
                case ConsequenceKind.Gate:
                    return $"Gate:{consequence.Target ?? "-"}";
                case ConsequenceKind.Trait:
                    return $"Trait:{consequence.Target}:{(amount == 0 ? 1 : amount)}";
                case ConsequenceKind.Goodwill:
                case ConsequenceKind.Enmity:
                    return $"{consequence.Kind}:{consequence.Target}:{size}";
                case ConsequenceKind.Item:
                    return $"Item:{consequence.Target}:{size}";
                case ConsequenceKind.Debt:
                    return $"Debt:{size}";
                case ConsequenceKind.LostYears:
                    return $"LostYears:{size}";
                default:
                    return $"{consequence.Kind}:{consequence.Target ?? "-"}";
            }
        }

        private static IEnumerable<string> LinesOf(SceneOption option) =>
            option.Consequences.Select(LineKey);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_life_can_be_promised_more_of_a_trait_than_the_game_holds(bool warSails)
        {
            // A trait runs from minus two to plus two and the apply step clamps to
            // that, so the character is right whatever the catalog says. The PLAYER
            // is not: every option that moved valor said so in the panel, and a life
            // that answered for valor ten times was told ten times and given two.
            // The reach of a trait is the sum, over the scenes, of the largest
            // movement any one answer to that scene can give it, because the player
            // answers every scene and may take the loudest answer in each.
            var over = new List<string>();

            foreach (string trait in new[] { "Mercy", "Valor", "Honor", "Generosity", "Calculating" })
            {
                int up = 0;
                int down = 0;

                foreach (var scene in Run(warSails))
                {
                    int best = 0;
                    int worst = 0;

                    foreach (var option in scene.Options)
                    {
                        int moved = option.Consequences
                            .Where(c => c.Kind == ConsequenceKind.Trait && c.Target == trait)
                            .Sum(c => c.Amount);

                        best = Math.Max(best, moved);
                        worst = Math.Min(worst, moved);
                    }

                    up += best;
                    down += worst;
                }

                if (up > GameHolds) over.Add($"{trait} can be promised {up}, and the game holds {GameHolds}");
                if (down < -GameHolds) over.Add($"{trait} can be promised {down}, and the game holds {-GameHolds}");
            }

            over.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_scene_moves_one_trait_the_same_way_on_two_of_its_answers(bool warSails)
        {
            // Two answers to the same question both saying "you are counted more
            // valorous" is the choice not being a choice about valor, and it is what
            // spends the reach above on a scene that asked nothing.
            var doubled = new List<string>();

            foreach (var scene in Run(warSails))
            {
                var moved = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var option in scene.Options)
                foreach (var consequence in option.Consequences
                             .Where(c => c.Kind == ConsequenceKind.Trait))
                {
                    string way = $"{consequence.Target} {(consequence.Amount < 0 ? "down" : "up")}";
                    moved.TryGetValue(way, out int seen);
                    moved[way] = seen + 1;
                }

                foreach (var pair in moved.Where(p => p.Value > 1))
                    doubled.Add($"{scene.Id} moves {pair.Key} on {pair.Value} of its answers");
            }

            doubled.Should().BeEmpty();
        }

        /// <summary>
        ///     How many of one scene's answers may print the same line. Two answers
        ///     that both cost a year are a scene asking how the year went rather than
        ///     whether; three of six is the scene answering itself.
        /// </summary>
        private const int SameLinePerScene = 2;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_line_stands_on_more_than_two_answers_of_one_scene(bool warSails)
        {
            var crowded = new List<string>();

            foreach (var scene in Run(warSails))
            foreach (var group in scene.Options
                         .SelectMany(option => LinesOf(option).Distinct())
                         .GroupBy(line => line, StringComparer.Ordinal)
                         .Where(g => g.Count() > SameLinePerScene))
                crowded.Add($"{scene.Id} prints {group.Key} on {group.Count()} of its answers");

            crowded.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_answer_prints_the_same_line_twice_to_itself(bool warSails)
        {
            foreach (var option in OptionsOf(Run(warSails)))
                LinesOf(option).Should().OnlyHaveUniqueItems(
                    $"{option.Id} would put one sentence in front of the player twice at once");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_single_line_dominates_the_run(bool warSails)
        {
            // The panel is read on every answer of every scene, not only on the
            // thirteen taken, so a line carried by a large share of the catalog is
            // what a player meets over and over: the complaint that started this was
            // one sentence standing on seventeen of the seventy-nine answers. An
            // eighth is the bar, which is generous and still rules that out.
            var options = OptionsOf(Run(warSails)).ToList();
            int cap = options.Count / 8;

            var loudest = options
                .SelectMany(option => LinesOf(option).Distinct())
                .GroupBy(line => line, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .First();

            loudest.Count().Should().BeLessThanOrEqualTo(cap,
                $"{loudest.Key} stands on {loudest.Count()} answers of {options.Count}");
        }

        #endregion

        #region Age

        /// <summary>
        ///     The options a life actually met, gates respected. Null when the wish
        ///     names an option something earlier in it shut, which is not a life
        ///     anybody could have lived.
        /// </summary>
        private static IReadOnlyList<SceneOption>? Walk(IReadOnlyList<Scene> scenes, IReadOnlyList<string> wish)
        {
            var met = new List<SceneOption>();
            var shut = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < scenes.Count; i++)
            {
                var option = scenes[i].Find(wish[i]);
                if (option == null || shut.Contains(option.Id)) return null;

                met.Add(option);
                foreach (var consequence in option.Consequences)
                    if (consequence.Kind == ConsequenceKind.Gate && consequence.Target != null)
                        shut.Add(consequence.Target);
            }

            return met;
        }

        /// <summary>One legal life to start a search from, picked without a random source so the run repeats.</summary>
        private static List<string> Opening(IReadOnlyList<Scene> scenes, int offset)
        {
            var wish = new List<string>();
            var shut = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scene in scenes)
            {
                var open = scene.Options.Where(option => !shut.Contains(option.Id)).ToList();
                var pick = open[(offset + wish.Count) % open.Count];
                wish.Add(pick.Id);

                foreach (var consequence in pick.Consequences)
                    if (consequence.Kind == ConsequenceKind.Gate && consequence.Target != null)
                        shut.Add(consequence.Target);
            }

            return wish;
        }

        private static IReadOnlyList<ChoiceConsequence> Left(IReadOnlyList<SceneOption> life) =>
            life.SelectMany(option => option.Consequences).ToList();

        private static StartingAge AgeOf(IReadOnlyList<SceneOption> life) =>
            Portrait.From(Left(life), life.Count).Age;

        /// <summary>
        ///     A compass for the search and nothing else: the assertion reads the age
        ///     the reader infers, never this. What the reader ages a character on is
        ///     how seasoned the life reads plus the years it threw away, so steering
        ///     by those two finds the ends of the range in a handful of passes where
        ///     sampling would need billions of runs to stumble on them.
        /// </summary>
        private static double Weathering(IReadOnlyList<SceneOption> life)
        {
            var left = Left(life);
            double seasoning = Portrait.From(left, life.Count).Read(Portrait.Facet.Seasoning).Share;
            int lost = left.Where(c => c.Kind == ConsequenceKind.LostYears).Sum(c => Math.Max(0, c.Amount));

            return seasoning * 30 + lost;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_life_can_finish_at_every_age_the_game_offers(bool warSails)
        {
            // Every answer to scene 12 used to cost years, so no finished life could
            // read as young however carefully the rest of it was answered: the
            // youngest character the run could produce was thirty. A band nothing
            // reaches is a band the player is shown and cannot have.
            var scenes = Run(warSails);
            var reached = new HashSet<StartingAge>();

            foreach (int toward in new[] { 1, -1 })
            foreach (int offset in new[] { 0, 1, 2, 3, 4, 5 })
            {
                var wish = Opening(scenes, offset);
                var life = Walk(scenes, wish)!;
                reached.Add(AgeOf(life));
                double best = toward * Weathering(life);

                bool improved = true;
                while (improved)
                {
                    improved = false;

                    for (int scene = 0; scene < scenes.Count; scene++)
                    foreach (var alternative in scenes[scene].Options)
                    {
                        if (alternative.Id == wish[scene]) continue;

                        var candidate = new List<string>(wish);
                        candidate[scene] = alternative.Id;

                        var lived = Walk(scenes, candidate);
                        if (lived == null) continue;

                        reached.Add(AgeOf(lived));
                        double score = toward * Weathering(lived);
                        if (score <= best) continue;

                        best = score;
                        wish = candidate;
                        improved = true;
                    }
                }
            }

            var offered = ((StartingAge[])Enum.GetValues(typeof(StartingAge))).ToList();
            reached.Should().HaveCountGreaterThan(1, "a run that can only make one age is not asking about age");
            reached.Should().BeEquivalentTo(offered,
                "every age band the game offers has to be the end of some life the run can tell");
        }

        #endregion

        #region Severity

        /// <summary>
        ///     How much of a character one answer settles, in one number.
        ///
        ///     Severity is a claim about weight, so the assertion needs a weight to
        ///     check it against. The units are trait points: one point of a trait or
        ///     one step of standing with somebody counts one. An object counts two
        ///     because it arrives in the kit and stays there, a place two, and a
        ///     title or a companion three, since each of those is a fact about the
        ///     character that every later scene has to live with. A gate counts two:
        ///     it settles an answer the player will not get to give. A lost year
        ///     counts half, because it ages the character and adds nothing else, and
        ///     a debt counts a point per four hundred denars, which is what the
        ///     smallest debt in the script is worth.
        /// </summary>
        private static double Settles(SceneOption option)
        {
            double settled = 0;

            foreach (var consequence in option.Consequences)
            {
                int amount = Math.Abs(consequence.Amount == 0 ? 1 : consequence.Amount);
                settled += consequence.Kind switch
                {
                    ConsequenceKind.Trait => amount,
                    ConsequenceKind.Goodwill => amount,
                    ConsequenceKind.Enmity => amount,
                    ConsequenceKind.Item => 2.0 * amount,
                    ConsequenceKind.Place => 2.0,
                    ConsequenceKind.Title => 3.0,
                    ConsequenceKind.Ally => 3.0,
                    ConsequenceKind.Gate => 2.0,
                    ConsequenceKind.LostYears => 0.5 * amount,
                    ConsequenceKind.Debt => amount / 400.0,
                    _ => 0.0
                };
            }

            return settled;
        }

        /// <summary>What a scene settles, which is what each of its answers settles.</summary>
        private static double Settles(Scene scene) => scene.Options.Average(Settles);

        /// <summary>
        ///     The margin the bands are held apart by. Ordering alone would pass on
        ///     a hundredth of a trait point, which is not a scene mattering more.
        /// </summary>
        private const double Margin = 0.25;

        private static double Heaviest(IReadOnlyList<Scene> scenes, Severity severity) =>
            scenes.Where(s => s.Severity == severity).Max(Settles);

        private static double Lightest(IReadOnlyList<Scene> scenes, Severity severity) =>
            scenes.Where(s => s.Severity == severity).Min(Settles);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Every_severity_the_run_uses_is_carried_by_a_scene(bool warSails)
        {
            // Bands are only checkable against each other, so a run that uses one
            // label is a run where nothing is being claimed
            Run(warSails).Select(scene => scene.Severity).Distinct().Count()
                .Should().BeGreaterThan(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_defining_scene_settles_more_than_any_formative_one(bool warSails)
        {
            var scenes = Run(warSails);

            Lightest(scenes, Severity.Defining)
                .Should().BeGreaterThan(Heaviest(scenes, Severity.Formative) + Margin,
                    "a hinge that decides less than an ordinary turn of a life is mislabeled");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_formative_scene_settles_more_than_any_habit(bool warSails)
        {
            var scenes = Run(warSails);

            Lightest(scenes, Severity.Formative)
                .Should().BeGreaterThan(Heaviest(scenes, Severity.Habit) + Margin,
                    "a coloring that decides as much as a turn of a life is mislabeled");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_scene_is_flat_beside_the_run_it_sits_in(bool warSails)
        {
            // A scene whose answers are alike is a scene the player can answer at
            // random. Severity says how much a scene decides; this says the answers
            // inside it decide different things.
            var scenes = Run(warSails);
            double mean = scenes.Average(Spread);

            foreach (var scene in scenes)
                Spread(scene).Should().BeGreaterThan(0.6 * mean,
                    $"{scene.Id} asks a question its answers barely disagree about");
        }

        /// <summary>Mean distance between two answers to the same scene.</summary>
        private static double Spread(Scene scene)
        {
            var distances = new List<double>();
            for (int i = 0; i < scene.Options.Count; i++)
            for (int j = i + 1; j < scene.Options.Count; j++)
                distances.Add(Distance(scene.Options[i], scene.Options[j]));

            return distances.Count == 0 ? 0 : distances.Average();
        }

        #endregion

        #region Consequence arithmetic

        /// <summary>
        ///     One answer's consequences as an amount per axis, so two answers can
        ///     be compared without caring what order they were written in.
        /// </summary>
        private static Dictionary<string, double> Axes(SceneOption option)
        {
            var axes = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var consequence in option.Consequences)
            {
                string axis = $"{consequence.Kind}:{consequence.Target ?? "-"}";
                axes.TryGetValue(axis, out double had);
                axes[axis] = had + (consequence.Amount == 0 ? 1 : consequence.Amount);
            }

            return axes;
        }

        private static bool Same(SceneOption a, SceneOption b) => Distance(a, b) < 0.001;

        private static double Distance(SceneOption a, SceneOption b)
        {
            var left = Axes(a);
            var right = Axes(b);
            double sum = 0;

            foreach (string axis in left.Keys.Union(right.Keys))
            {
                left.TryGetValue(axis, out double l);
                right.TryGetValue(axis, out double r);
                // A debt is in denars and everything else is in single steps, so it
                // is scaled to the same units before the two are subtracted
                double scale = axis.StartsWith("Debt", StringComparison.Ordinal) ? 400.0 : 1.0;
                sum += Math.Abs(l - r) / scale;
            }

            return sum;
        }

        /// <summary>
        ///     Whether one answer gives everything another gives and something more.
        ///     Debt, lost years, enmity and a gate count the other way round, so
        ///     taking on more of any of them is never the improvement.
        /// </summary>
        private static bool Dominates(SceneOption better, SceneOption worse)
        {
            var strong = Axes(better);
            var weak = Axes(worse);
            bool strictly = false;

            foreach (string axis in strong.Keys.Union(weak.Keys))
            {
                strong.TryGetValue(axis, out double s);
                weak.TryGetValue(axis, out double w);

                bool cost = axis.StartsWith("Debt", StringComparison.Ordinal) ||
                            axis.StartsWith("LostYears", StringComparison.Ordinal) ||
                            axis.StartsWith("Enmity", StringComparison.Ordinal) ||
                            axis.StartsWith("Gate", StringComparison.Ordinal);

                double gain = cost ? w - s : s - w;
                if (gain < 0) return false;
                if (gain > 0) strictly = true;
            }

            return strictly;
        }

        #endregion

        #region What a life is promised twice

        /// <summary>
        ///     The three grants that are a quantity rather than an object. Coin is
        ///     denars and adds up. Trade goods are drawn fresh from the market's own
        ///     list on every grant, so two grants are two different goods. Pack
        ///     animals come as a team and a second beast is a bigger load carried.
        ///
        ///     Everything else the catalog can promise is one object standing in one
        ///     place: a hauberk on a back, a harness on a horse, the horse under the
        ///     rider. A life promised one of those twice read the sentence twice and
        ///     wears one, which is the panel and the character disagreeing in front
        ///     of the player, which the panel may never do.
        /// </summary>
        private static readonly HashSet<string> Quantities =
            new(StringComparer.Ordinal) { "coin_pouch", "trade_goods", "pack_mule" };

        /// <summary>
        ///     How many of one life's answers may promise the same quantity. Three
        ///     purses is a life that came into money three times; a fourth is the
        ///     panel repeating itself.
        /// </summary>
        private const int QuantityPerLife = 3;

        /// <summary>
        ///     Mounts a life may be promised. Two, and only as an upgrade: the
        ///     animal the story gave the character and the horse the story had them
        ///     buy are different animals and the better one is ridden. A third is a
        ///     string of horses nobody asked for.
        /// </summary>
        private const int MountsPerLife = 2;

        private static readonly string[] Mounts = { "riding_horse", "war_horse" };

        private static IEnumerable<string> ItemsOf(SceneOption option) =>
            option.Consequences
                .Where(c => c.Kind == ConsequenceKind.Item && c.Target != null)
                .Select(c => c.Target!);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_object_is_promised_by_two_different_scenes(bool warSails)
        {
            // The catalog-side statement of it, and the cheaper of the two: a life
            // answers every scene it reaches, so an object promised in two scenes is
            // an object some life is promised twice. Two answers to the SAME scene
            // may both promise it, because a player gives one of them.
            var carriers = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var scene in Run(warSails))
            foreach (string target in scene.Options.SelectMany(ItemsOf)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!carriers.TryGetValue(target, out var scenes))
                    carriers[target] = scenes = new List<string>();

                scenes.Add(scene.Id);
            }

            carriers.Where(pair => !Quantities.Contains(pair.Key) && pair.Value.Count > 1)
                .Select(pair => $"{pair.Key} is promised by {string.Join(" and ", pair.Value)}")
                .Should().BeEmpty();

            carriers.Where(pair => Quantities.Contains(pair.Key) &&
                                   pair.Value.Count > QuantityPerLife)
                .Select(pair => $"{pair.Key} is promised by {pair.Value.Count} scenes")
                .Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_life_is_promised_the_same_object_twice(bool warSails)
        {
            // The same claim asked of whole lives rather than of the catalog, since
            // the catalog-side one is a proxy and a gate or an appearance predicate
            // could one day make it the wrong proxy. The measured defect this
            // guards: one life in seven was promised two saddles, one in twelve two
            // mail hauberks, and a life could collect seven horses.
            var over = new List<string>();

            foreach (var life in LifeHarness.Lives(Run(warSails)))
            {
                var promised = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (string target in life.Chosen.SelectMany(ItemsOf))
                {
                    promised.TryGetValue(target, out int already);
                    promised[target] = already + 1;
                }

                foreach (var pair in promised)
                {
                    int allowed = Quantities.Contains(pair.Key) ? QuantityPerLife : 1;
                    if (pair.Value > allowed)
                        over.Add($"{pair.Key} promised {pair.Value} times: {life.Trail}");
                }

                int mounts = Mounts.Sum(mount => promised.TryGetValue(mount, out int n) ? n : 0);
                if (mounts > MountsPerLife)
                    over.Add($"{mounts} mounts promised: {life.Trail}");
            }

            over.Take(3).Should().BeEmpty();
        }

        #endregion

        #region The script and the catalog agree

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_put_the_same_scenes_in_the_same_order(bool warSails)
        {
            Script(warSails).Select(scene => scene.Id)
                .Should().Equal(Run(warSails).Select(scene => scene.Id));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_agree_on_every_severity(bool warSails)
        {
            var scenes = Run(warSails);

            foreach (var written in Script(warSails))
            {
                var built = scenes.Single(scene => scene.Id == written.Id);
                built.Severity.ToString().Should().Be(written.Severity,
                    $"{written.Id} is labeled one way in the script and built another");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_agree_on_every_heading(bool warSails)
        {
            var scenes = Run(warSails);

            foreach (var written in Script(warSails))
            {
                var built = scenes.Single(scene => scene.Id == written.Id);
                Tag.Match(built.Title).Groups[2].Value.Should().Be(written.Title,
                    $"{written.Id} is headed one way in the script and built another");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_agree_on_every_consequence(bool warSails)
        {
            // The script is the content and the catalog is the transcription, so a
            // consequence changed in one and not the other is a scene that plays
            // differently from the one that was written. Checked in full rather
            // than read side by side, because an eye passes over a moved digit.
            var scenes = Run(warSails);

            foreach (var written in Script(warSails))
            {
                var built = scenes.Single(scene => scene.Id == written.Id);
                built.Options.Select(option => option.Id)
                    .Should().Equal(written.Options.Select(option => option.Id),
                        $"{written.Id} offers different answers in the script");

                foreach (var option in written.Options)
                    Written(built.Options.Single(o => o.Id == option.Id))
                        .Should().Equal(option.Consequences,
                            $"{option.Id} does not leave behind what the script says it does");
            }
        }

        [Fact]
        public void The_script_writes_both_ninth_scenes_and_marks_which_is_which()
        {
            // The marker is what tells the two apart, since they share an id, and a
            // variant written without it would silently be read as the land scene
            var ashore = Script(false);
            var afloat = Script(true);

            ashore.Should().HaveCount(13);
            afloat.Should().HaveCount(13);
            ashore[8].Options.Select(o => o.Id)
                .Should().NotEqual(afloat[8].Options.Select(o => o.Id),
                    "the script carries one ninth scene, so the War Sails heading was not found");
        }

        /// <summary>One consequence in the form the script writes it.</summary>
        private static IReadOnlyList<string> Written(SceneOption option) =>
            option.Consequences.Select(c => $"{c.Kind}({c.Target ?? "null"},{c.Amount})").ToList();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_vary_the_same_lines_for_the_same_lives(bool warSails)
        {
            // A variant is a line somebody reads, so the script owns it the way it
            // owns every other line. The predicate cannot be written in a markdown
            // file, so what is compared is the state's NAME and the order of them:
            // a variant added to the catalog and not to the script is writing
            // nobody authored, and one added to the script and not to the catalog
            // is writing nobody will ever be shown
            var scenes = Run(warSails);

            foreach (var written in Script(warSails))
            {
                var built = scenes.Single(scene => scene.Id == written.Id);

                built.PromptVariants.Select(variant => variant.When)
                    .Should().Equal(written.Variants,
                        $"{written.Id} puts its situation to different lives differently in the script");

                foreach (var option in written.Options)
                    built.Options.Single(o => o.Id == option.Id).Variants
                        .Select(variant => variant.When)
                        .Should().Equal(option.Variants,
                            $"{option.Id} is written for different lives in the script");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_line_is_never_varied_for_one_state_twice(bool warSails)
        {
            // The first state that holds wins, so a label written twice leaves a
            // second piece of writing nobody can ever be shown
            var scenes = Run(warSails);

            foreach (var scene in scenes)
            {
                scene.PromptVariants.Select(variant => variant.When).Should().OnlyHaveUniqueItems();

                foreach (var option in scene.Options)
                    option.Variants.Select(variant => variant.When).Should().OnlyHaveUniqueItems();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_script_and_the_catalog_agree_on_every_age(bool warSails)
        {
            // The stage direction is where a scene's year is authored, and the
            // 3D render is drawn at whatever the catalog carries, so a scene
            // written for a child of ten and built at seventeen is a player
            // reading one life and looking at another.
            var scenes = Run(warSails);

            foreach (var written in Script(warSails))
            {
                var built = scenes.Single(scene => scene.Id == written.Id);
                built.Age.Should().Be(written.Age,
                    $"{written.Id} happens at one age in the script and is built at another");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Only_the_last_scene_happens_at_whatever_age_the_life_has_reached(bool warSails)
        {
            // Every other scene names its year, so an age that came back empty
            // is a stage direction this file could not read rather than a scene
            // that deliberately has none
            var run = Run(warSails);

            run.Where(scene => scene.Age == null).Should().HaveCount(1);
            run.Last().Age.Should().BeNull();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_run_tells_a_life_in_the_order_it_was_lived(bool warSails)
        {
            // A scene cannot happen before the one in front of it. Caught here
            // rather than by the comparison above, which passes happily when the
            // script and the catalog carry the same wrong number.
            var years = Run(warSails).Select(scene => scene.Age).Where(age => age != null).ToList();

            years.Should().Equal(years.OrderBy(age => age));
        }

        private sealed record ScriptOption(string Id, IReadOnlyList<string> Consequences,
            IReadOnlyList<string> Variants);

        private sealed record ScriptScene(string Id, string Title, string Severity, bool WarSails,
            int? Age, IReadOnlyList<string> Variants, IReadOnlyList<ScriptOption> Options);

        private static readonly Regex SceneHeading =
            new(@"^## Scene \d+(?<variant> \(War Sails\))?: `(?<id>[a-z0-9_]+)`");

        private static readonly Regex SeverityLine = new(@"\*\*Severity:\*\* (?<severity>\w+)");

        /// <summary>
        ///     The year of a life a scene happens in, as its stage direction
        ///     opens. The direction is prose, so the age is a word: the last
        ///     scene names no year at all and says so in words too, which reads
        ///     back as no age rather than as a wrong one.
        /// </summary>
        private static readonly Regex StageLine = new(@"^\*\*Stage:\*\* The player at (?<age>[a-z-]+)");
        private static readonly Regex TitleLine = new(@"^\*\*Title:\*\* (?<title>.+)$");
        private static readonly Regex OptionHeading = new(@"^### `(?<id>[a-z0-9_]+)`");

        /// <summary>
        ///     A line written a second time for the lives one state holds of. The
        ///     state's name is all the file can carry, since the predicate that
        ///     reads it is code; the catalog carries the same name beside the
        ///     predicate, which is what makes the two comparable.
        /// </summary>
        private static readonly Regex PromptVariantLine =
            new(@"^\*\*Prompt when `(?<when>[a-z0-9-]+)`:\*\*");

        private static readonly Regex ProseVariantLine =
            new(@"^\*\*Prose when `(?<when>[a-z0-9-]+)`:\*\*");
        private static readonly Regex ConsequenceCall =
            new(@"(?<kind>Debt|Enmity|Goodwill|Ally|Place|Title|LostYears|Item|Trait|Gate|Household)\((?<args>[^)]*)\)");

        /// <summary>
        ///     One of the two runs the script writes, in order.
        ///
        ///     A scene under a War Sails heading stands in for the plain scene of
        ///     the same id, so the file carries fourteen headings and either run
        ///     reads back as thirteen scenes.
        /// </summary>
        private static IReadOnlyList<ScriptScene> Script(bool warSails)
        {
            var written = ReadScript();
            var run = new List<ScriptScene>();

            foreach (var scene in written)
            {
                if (scene.WarSails) continue;

                var variant = written.FirstOrDefault(s => s.WarSails && s.Id == scene.Id);
                run.Add(warSails && variant != null ? variant : scene);
            }

            return run;
        }

        /// <summary>The script itself, parsed, so the comparison is with the file and not with a copy of it.</summary>
        private static IReadOnlyList<ScriptScene> ReadScript()
        {
            var scenes = new List<ScriptScene>();
            string? sceneId = null;
            string title = string.Empty;
            string severity = string.Empty;
            int? age = null;
            bool warSails = false;
            var options = new List<ScriptOption>();
            string? optionId = null;
            var consequences = new List<string>();
            var promptVariants = new List<string>();
            var proseVariants = new List<string>();
            bool reading = false;

            void CloseOption()
            {
                if (optionId != null)
                    options.Add(new ScriptOption(optionId, consequences.ToList(), proseVariants.ToList()));
                optionId = null;
                consequences.Clear();
                proseVariants.Clear();
                reading = false;
            }

            void CloseScene()
            {
                CloseOption();
                if (sceneId != null)
                    scenes.Add(new ScriptScene(sceneId, title, severity, warSails, age,
                        promptVariants.ToList(), options.ToList()));
                sceneId = null;
                age = null;
                options.Clear();
                promptVariants.Clear();
            }

            foreach (string line in File.ReadLines(ScriptPath()))
            {
                var scene = SceneHeading.Match(line);
                if (scene.Success)
                {
                    CloseScene();
                    sceneId = scene.Groups["id"].Value;
                    warSails = scene.Groups["variant"].Success;
                    continue;
                }

                if (sceneId == null) continue;

                var option = OptionHeading.Match(line);
                if (option.Success)
                {
                    CloseOption();
                    optionId = option.Groups["id"].Value;
                    continue;
                }

                if (optionId == null)
                {
                    var found = SeverityLine.Match(line);
                    if (found.Success) severity = found.Groups["severity"].Value;

                    // Only at this level: an option carries a title line of its own
                    var named = TitleLine.Match(line);
                    if (named.Success) title = named.Groups["title"].Value.Trim();

                    var staged = StageLine.Match(line);
                    if (staged.Success) age = Years(staged.Groups["age"].Value);

                    var otherwise = PromptVariantLine.Match(line);
                    if (otherwise.Success) promptVariants.Add(otherwise.Groups["when"].Value);
                    continue;
                }

                var varied = ProseVariantLine.Match(line);
                if (varied.Success) proseVariants.Add(varied.Groups["when"].Value);

                if (line.StartsWith("**Consequences:**", StringComparison.Ordinal)) reading = true;
                else if (line.StartsWith("**", StringComparison.Ordinal) || line.Length == 0) reading = false;

                if (!reading) continue;

                foreach (Match call in ConsequenceCall.Matches(line))
                    consequences.Add(Normalized(call));
            }

            CloseScene();
            return scenes;
        }

        /// <summary>
        ///     A call as the script writes it, in the catalog's own vocabulary. The
        ///     script gives a title, an ally and a gate a target and no amount, a
        ///     place a target and sometimes an amount, and lost years an amount and
        ///     no target.
        /// </summary>
        private static string Normalized(Match call)
        {
            string kind = call.Groups["kind"].Value;
            var args = call.Groups["args"].Value
                .Split(',')
                .Select(part => part.Trim().Trim('`'))
                .Where(part => part.Length > 0)
                .ToList();

            if (kind == "LostYears") return $"LostYears(null,{args[0]})";
            if (args.Count == 1) return $"{kind}({args[0]},0)";
            return $"{kind}({args[0]},{args[1]})";
        }

        /// <summary>
        ///     A year of a life as the script writes it, or null where the stage
        ///     direction opens on something that is not a year.
        ///
        ///     The script writes its numbers as words because a stage direction
        ///     is prose, and the ages are read back out of that prose rather than
        ///     declared a second time in a form easier to parse: a second
        ///     declaration is the thing every comparison in this file exists to
        ///     stop, and the age is no different from the severity or the title.
        /// </summary>
        private static int? Years(string written)
        {
            var parts = written.Split('-');

            int tens = Array.IndexOf(Tens, parts[0]);
            if (tens > 0)
            {
                if (parts.Length == 1) return tens * 10;

                int and = Array.IndexOf(Units, parts[1]);
                return and > 0 ? tens * 10 + and : null;
            }

            if (parts.Length > 1) return null;

            int alone = Array.IndexOf(Units, parts[0]);
            return alone >= 0 ? alone : null;
        }

        private static readonly string[] Units =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen"
        };

        private static readonly string[] Tens =
        {
            "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
        };

        private static string ScriptPath() =>
            SourcePath("CharacterCreation", "Catalog", "SceneScript.md");

        /// <summary>A file of the mod's own source, from this test file's place in the tree.</summary>
        private static string SourcePath(params string[] parts) =>
            Path.Combine(new[] { ModRoot() }.Concat(parts).ToArray());

        private static string ModRoot([CallerFilePath] string here = "") =>
            Path.GetDirectoryName(Path.GetDirectoryName(here)!)!;

        #endregion
    }
}
