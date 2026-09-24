using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Every sentence the panel showed is still true of the finished
    ///     character.
    ///
    ///     That is the promise written as an assertion rather than as a rule an
    ///     author has to remember, and it is a different question from the one the
    ///     rest of this project asks. Everything else here weighs ONE declaration:
    ///     whether it has English, whether the pipeline can hand it over, whether
    ///     the panel names it. This weighs two of them against each other, which is
    ///     the class of defect that has shipped repeatedly under a green suite,
    ///     because each half was correct and only the pair was wrong. A visible
    ///     contradiction and a silent overwrite are one failure to it: both end
    ///     with the player holding a sentence the character disproves, and the
    ///     silent one is worse, since nothing on screen ever disagrees.
    ///
    ///     One axis is in reach, and it is in reach because it resolves to a
    ///     terminal state computable from pure logic: the household, folded out of
    ///     what the answers declared. Two more assertions are catalog structure and
    ///     cost nothing: a thing promised once is granted once, and the reason a
    ///     shut door gives is true of whoever is reading it. The starting place was
    ///     the second axis until the panel stopped stating one: every start type is
    ///     asked where it begins by a later chapter whose answer LocationStep
    ///     prefers, so nothing a scene declares about it could be true at the end.
    ///
    ///     WHAT THIS CANNOT SEE, said plainly so the next person does not read it
    ///     as cover it does not give:
    ///
    ///     - Prose that asserts a fact no consequence carries. "There was no house
    ///       to speak of" is writing, not a declaration, so nothing downstream
    ///       reads it and nothing here can weigh it against the character. Only
    ///       what an option DECLARES is checkable.
    ///     - Scene prompts, which carry no consequences at all by design: a scene
    ///       puts a situation and never says what the character is.
    ///     - Register. Two sentences can both be true and still read as nonsense
    ///       in sequence. That is an editorial judgment and no assertion reaches
    ///       it.
    ///
    ///     None of the files this reasons about can be compiled into this project.
    ///     HouseholdMenu and ChoiceEffects both name engine types, so
    ///     they must stay out, and what they hold is read out of their own
    ///     source the way <see cref="ModSource"/> reads every other table this
    ///     project cannot link. Where a rule has to be restated here to be applied
    ///     at all, every value it turns on is read from the file and the shape is
    ///     pinned against it, so the restatement cannot go on passing after the
    ///     original has moved.
    /// </summary>
    public class StillTrueAtTheEndTests
    {
        private const string HouseholdMenu = "CharacterCreation/Menus/HouseholdMenu.cs";

        private const string HeroLore = "Services/HeroLore.cs";

        /// <summary>
        ///     The composed house, named by enough of its declaration to tell it
        ///     from the three places that call it. A member is found by the first
        ///     appearance of the text naming it, so the bare name would read a
        ///     caller's body instead and every arm below would go missing at once.
        /// </summary>
        private const string Fold = "bool Spouse) DerivedHouse";

        private static IEnumerable<SceneOption> Answers =>
            SceneCatalog.All.SelectMany(scene => scene.Options);

        private static IEnumerable<ChoiceConsequence> Of(SceneOption option, ConsequenceKind kind) =>
            option.Consequences.Where(c => c.Kind == kind && c.Target != null);

        private static IEnumerable<string> Targets(SceneOption option, ConsequenceKind kind) =>
            Of(option, kind).Select(c => c.Target!);

        #region The house

        /// <summary>
        ///     The house as the composed household ends up holding it. The
        ///     unsettled sibling is the one answer that names somebody without
        ///     saying which kind: the run settles that from its own seed and the
        ///     panel prints whichever it settled on, so both halves name the same
        ///     person and this only has to know that a person is there.
        /// </summary>
        private readonly struct House
        {
            public House(bool father, bool mother, int brothers, int sisters, int unsettled, bool spouse)
            {
                Father = father;
                Mother = mother;
                Brothers = brothers;
                Sisters = sisters;
                Unsettled = unsettled;
                Spouse = spouse;
            }

            public bool Father { get; }

            public bool Mother { get; }

            public int Brothers { get; }

            public int Sisters { get; }

            public int Unsettled { get; }

            public bool Spouse { get; }

            public override string ToString() =>
                $"father {(Father ? "alive" : "buried")}, mother {(Mother ? "alive" : "buried")}, " +
                $"{Brothers} brothers, {Sisters} sisters, {Unsettled} unsettled, " +
                $"{(Spouse ? "married" : "unmarried")}";
        }

        /// <summary>
        ///     What each household fact writes into the composed house, and what
        ///     its sentence therefore claims of the finished character.
        ///
        ///     A restatement of <c>HouseholdMenu.DerivedHouse</c>, which names
        ///     engine types and cannot be compiled here. Every arm of it is pinned
        ///     against the real file below, in both directions: an arm that writes
        ///     a flag this does not, or stops writing one this does, fails there
        ///     rather than going on quietly disagreeing.
        /// </summary>
        private sealed class Fact
        {
            public Fact(Func<House, House> writes, Func<House, bool> stillTrue, params string[] mentions)
            {
                Writes = writes;
                StillTrue = stillTrue;
                Mentions = mentions;
            }

            public Func<House, House> Writes { get; }

            public Func<House, bool> StillTrue { get; }

            /// <summary>The names this fact's arm may mention, and the only ones.</summary>
            public string[] Mentions { get; }
        }

        private static readonly IReadOnlyDictionary<string, Fact> Facts =
            new Dictionary<string, Fact>(StringComparer.Ordinal)
            {
                ["both_parents_living"] = new Fact(
                    h => new House(true, true, h.Brothers, h.Sisters, h.Unsettled, h.Spouse),
                    h => h.Father && h.Mother, "father = true", "mother = true"),

                ["both_parents_buried"] = new Fact(
                    h => new House(false, false, h.Brothers, h.Sisters, h.Unsettled, h.Spouse),
                    h => !h.Father && !h.Mother, "father = false", "mother = false"),

                ["father_buried"] = new Fact(
                    h => new House(false, h.Mother, h.Brothers, h.Sisters, h.Unsettled, h.Spouse),
                    h => !h.Father, "father = false"),

                ["mother_buried"] = new Fact(
                    h => new House(h.Father, false, h.Brothers, h.Sisters, h.Unsettled, h.Spouse),
                    h => !h.Mother, "mother = false"),

                ["a_brother"] = new Fact(
                    h => new House(h.Father, h.Mother, h.Brothers + 1, h.Sisters, h.Unsettled, h.Spouse),
                    h => h.Brothers >= 1, "brothers++"),

                ["a_sister"] = new Fact(
                    h => new House(h.Father, h.Mother, h.Brothers, h.Sisters + 1, h.Unsettled, h.Spouse),
                    h => h.Sisters >= 1, "sisters++"),

                ["a_spouse"] = new Fact(
                    h => new House(h.Father, h.Mother, h.Brothers, h.Sisters, h.Unsettled, true),
                    h => h.Spouse, "spouse = true"),

                // Somebody under that roof whose kind the answer keeps back. Both
                // halves ask the run and get the same person, so what is owed to
                // the sentence is only that the person is there
                ["a_sibling"] = new Fact(
                    h => new House(h.Father, h.Mother, h.Brothers, h.Sisters, h.Unsettled + 1, h.Spouse),
                    h => h.Brothers + h.Sisters + h.Unsettled >= 1, "brothers++", "sisters++"),

                // The grandparents are built by FamilyStep rather than composed by
                // the three chapters, and no answer anywhere raises the dead, so
                // this one cannot be taken back once it has been said
                ["forebears_buried"] = new Fact(h => h, _ => true)
            };

        /// <summary>
        ///     The two facts that are not themselves facts: each reads the life,
        ///     one settling whichever parent is still standing and the other
        ///     finding them living only where the life has said nothing. That is
        ///     real logic and is asked of the real code rather than restated.
        /// </summary>
        private static string? Resolved(string fact, IReadOnlyList<string> said)
        {
            if (fact == StoryGraves.Declares) return StoryGraves.Settled(said);
            if (fact == StoryHome.Declares) return StoryHome.Settled(said);

            return fact;
        }

        private static bool Declares(SceneOption option) =>
            Of(option, ConsequenceKind.Household).Any();

        private static House Composed(IReadOnlyList<string> said)
        {
            var house = new House(true, true, 0, 0, 0, false);

            foreach (string fact in said)
            {
                string? settled = Resolved(fact, said);
                if (settled != null && Facts.TryGetValue(settled, out var known))
                    house = known.Writes(house);
            }

            return house;
        }

        [Fact]
        public void Every_house_a_sentence_can_state_is_one_this_knows_how_to_weigh()
        {
            // Both directions. A fact with no reading here is a sentence the player
            // is shown and nothing weighs; a reading with no fact behind it is a
            // rule about a sentence nobody prints. The English is the third party
            // to the same closed set, so it is read too: a target with no sentence
            // is caught by HouseholdDerivationTests, and a sentence with no target
            // would leave this asserting about a promise the route cannot make
            var spoken = new HashSet<string>(
                ModSource.SentencesIn(HeroLore, "HouseText").Keys, StringComparer.Ordinal);
            spoken.UnionWith(Answers.SelectMany(option => Targets(option, ConsequenceKind.Household)));
            spoken.Remove(StoryGraves.Declares);
            spoken.Remove(StoryHome.Declares);

            Facts.Keys.Should().BeEquivalentTo(spoken,
                "the household facts this weighs and the ones the route can state have come apart");
        }

        [Fact]
        public void Every_arm_of_the_composed_house_writes_what_this_says_it_writes()
        {
            // What makes the restatement above an assertion about the shipped fold
            // rather than about a copy of it. Each arm is pinned in both
            // directions: it has to write what this claims, and it may not touch
            // anything this does not claim. That second half is the one that
            // caught a live defect: every parent arm used to write BOTH flags, so
            // burying a father silently stood the mother back up
            string body = ModSource.MemberBody(HouseholdMenu, Fold);
            var names = new[] { "father", "mother", "brothers", "sisters", "spouse" };

            foreach (var fact in Facts)
            {
                if (fact.Value.Mentions.Length == 0) continue;

                string arm = Arm(body, fact.Key);

                foreach (string written in fact.Value.Mentions)
                    Squeezed(arm).Should().Contain(Squeezed(written),
                        $"the arm for {fact.Key} no longer writes {written}");

                foreach (string name in names)
                {
                    if (fact.Value.Mentions.Any(written => written.StartsWith(name, StringComparison.Ordinal)))
                        continue;

                    arm.Should().NotContain(name,
                        $"the arm for {fact.Key} touches {name}, which this file does not know it writes, " +
                        "so the house it composes and the house weighed here have come apart");
                }
            }

            Arm(body, StoryGraves.Declares).Should().Contain("StoryGraves",
                $"the arm for {StoryGraves.Declares} no longer asks the life which parent it left standing");

            Arm(body, StoryHome.Declares).Should().Contain("StoryHome",
                $"the arm for {StoryHome.Declares} no longer asks the life whether it has spoken about " +
                "the parents at all, so the coin sent home stands them both back up again");
        }

        /// <summary>
        ///     One arm of the fold, closed at the first <c>break</c>. That is exact
        ///     for a flat arm and short for one holding a switch of its own, which
        ///     is why the arm that does is asked only whether it still delegates.
        /// </summary>
        private static string Arm(string body, string fact)
        {
            var found = Regex.Match(body, @"case\s+""" + Regex.Escape(fact) + @"""\s*:(?<arm>.*?)break\s*;",
                RegexOptions.Singleline);

            found.Success.Should().BeTrue($"the composed house no longer folds {fact}");
            return found.Groups["arm"].Value;
        }

        private static string Squeezed(string text) => Regex.Replace(text, @"\s+", string.Empty);

        [Fact]
        public void No_answer_that_settles_the_house_can_be_shut_by_another()
        {
            // Half of what makes the sweep below total. A gate shuts an option for
            // the rest of a life, so if one of these could be shut then some
            // households would be unreachable and the enumeration would be walking
            // lives nobody can live
            var settling = Answers.Where(Declares).Select(option => option.Id).ToHashSet(StringComparer.Ordinal);

            Answers.SelectMany(option => Targets(option, ConsequenceKind.Gate))
                .Where(settling.Contains)
                .Should().BeEmpty("an answer that settles the house is shut by another answer, so the " +
                                  "households walked below are no longer all of them");
        }

        /// <summary>
        ///     Every household a life can come to, walked once each.
        ///
        ///     TOTAL, not sampled, and that is the whole point of it. A sweep meets
        ///     the collisions that happen to be common and misses the pair that
        ///     ships; the pair that shipped last time turned up in one life in
        ///     eight. The run is thirteen scenes deep and far too wide to
        ///     enumerate, so what is enumerated is its projection onto the house,
        ///     which is sound for three reasons this file checks rather than
        ///     assumes.
        ///
        ///     The house is composed from the household facts a life declared, in
        ///     the order it declared them, and from nothing else, so two lives
        ///     declaring the same facts in the same order end in the same house.
        ///     Every scene is put to every player, which the walk re-checks on each
        ///     life, so that order is the catalog's order. And no answer that
        ///     settles the house can be shut, checked directly above, so every
        ///     combination is one somebody can live. What is left is one answer per
        ///     scene that settles nothing, standing in for all of them, because
        ///     answers that declare no household fact are interchangeable to a
        ///     composition that reads only household facts.
        /// </summary>
        private static IReadOnlyList<Life> EveryHousehold()
        {
            var axes = new List<(string Scene, IReadOnlyList<SceneOption?> States)>();
            foreach (var scene in SceneCatalog.All)
            {
                var states = scene.Options.Where(Declares).Cast<SceneOption?>().ToList();
                if (states.Count == 0) continue;

                states.Add(null);
                axes.Add((scene.Id, states));
            }

            axes.Should().NotBeEmpty("no scene settles the house any more; has Household been renamed?");

            var lives = new List<Life>();
            var counter = new int[axes.Count];

            while (true)
            {
                var wanted = new Dictionary<string, SceneOption?>(StringComparer.Ordinal);
                for (int axis = 0; axis < axes.Count; axis++)
                    wanted[axes[axis].Scene] = axes[axis].States[counter[axis]];

                lives.Add(HouseholdLife(wanted));

                int at = axes.Count - 1;
                while (at >= 0 && ++counter[at] == axes[at].States.Count) counter[at--] = 0;
                if (at < 0) break;
            }

            return lives;
        }

        private static Life HouseholdLife(IReadOnlyDictionary<string, SceneOption?> wanted)
        {
            var life = LifeHarness.Walk((scene, offered) =>
            {
                if (!wanted.TryGetValue(scene.Id, out var pick) || pick == null)
                {
                    foreach (var option in offered)
                        if (!Declares(option))
                            return option;

                    throw new InvalidOperationException(
                        $"{scene.Id} offers no answer that leaves the house alone");
                }

                foreach (var option in offered)
                    if (option.Id == pick.Id)
                        return option;

                throw new InvalidOperationException(
                    $"{pick.Id} was not offered, so a household counted here as reachable is not");
            });

            if (life.Chosen.Count != SceneCatalog.All.Count)
                throw new InvalidOperationException(
                    $"a life answered {life.Chosen.Count} of {SceneCatalog.All.Count} scenes, so a scene " +
                    "has become conditional and the projection enumerated here is no longer the whole of it");

            return life;
        }

        [Fact]
        public void No_household_sentence_is_disproved_by_the_house_the_character_ends_with()
        {
            var lives = EveryHousehold();
            var broken = new List<string>();

            foreach (var life in lives)
            {
                var said = life.Left
                    .Where(c => c.Kind == ConsequenceKind.Household && c.Target != null)
                    .Select(c => c.Target!)
                    .ToList();

                var house = Composed(said);

                foreach (var option in life.Chosen)
                foreach (string stated in Targets(option, ConsequenceKind.Household))
                {
                    string? settled = Resolved(stated, said);

                    // The last scene's answer settles nothing where the life has
                    // already said both parents are living, and then it states
                    // nothing about them either
                    if (settled == null) continue;

                    if (!Facts[settled].StillTrue(house))
                        broken.Add($"{option.Id} states '{settled}' and the character ends " +
                                   $"[{house}] :: {life.Trail}");
                }
            }

            broken.Count.Should().Be(0,
                "a household sentence the player read is disproved by the character they were handed. " +
                $"{broken.Count} such sentences across {lives.Count} households, for example: " +
                string.Join(" | ", broken.Take(3)));
        }

        #endregion

        #region What a life is promised and handed

        /// <summary>
        ///     The three things a life may honestly be promised more than once,
        ///     because they add rather than replace: coin is denars that sum,
        ///     trade goods are drawn fresh from the market list on every grant,
        ///     and pack animals come as a team. Every other object is singular
        ///     and two grant sites for one of those is a life promised it twice.
        /// </summary>
        private static readonly string[] Repeatable = { "coin_pouch", "pack_mule", "trade_goods" };
        [Fact]
        public void A_thing_promised_once_cannot_be_granted_twice_in_one_life()
        {
            // Two grant sites in one scene are safe, because a player answers a
            // scene once. Two sites in two scenes are a life that can collect the
            // same singular thing twice, with the panel stating it twice as though
            // they were two of them
            var sites = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var scene in SceneCatalog.All)
            foreach (var option in scene.Options)
            foreach (string granted in Targets(option, ConsequenceKind.Item))
            {
                if (Repeatable.Contains(granted, StringComparer.Ordinal)) continue;
                if (!sites.TryGetValue(granted, out var where))
                    sites[granted] = where = new List<string>();
                where.Add(scene.Id);
            }

            sites.Should().NotBeEmpty("the catalog grants nothing singular; has Item been renamed?");

            sites.Where(pair => pair.Value.Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(pair => $"{pair.Key} is granted in {string.Join(" and ", pair.Value.Distinct())}")
                .Should().BeEmpty("a singular thing is granted from two scenes, so one life can be " +
                                  "promised it twice and handed two of it");
        }

        [Fact]
        public void Nothing_is_excused_from_that_rule_without_needing_to_be()
        {
            // The exemption list cleans itself. An id that stops being granted
            // twice stops needing to be excused, and an excuse nobody needs is
            // cover for a second grant site somebody adds later
            var counted = Answers
                .SelectMany(option => Targets(option, ConsequenceKind.Item))
                .GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            foreach (string id in Repeatable)
            {
                counted.Should().ContainKey(id, $"{id} is excused and the catalog never grants it");
                counted[id].Should().BeGreaterThan(1,
                    $"{id} is granted once and does not need excusing, so the excuse should go before " +
                    "somebody adds a second grant site under cover of it");
            }
        }

        #endregion

        #region The reason a door is shut

        private static readonly Regex Key = new(@"^\{=[A-Za-z0-9_]+\}", RegexOptions.Compiled);

        /// <summary>
        ///     A note that offers the player a choice of reasons. Only one of the
        ///     causes it lists can be the reader's, so a note listing what MIGHT
        ///     have shut the door is a sentence most of the lives that read it
        ///     disprove.
        /// </summary>
        private static readonly Regex Menu = new(@",\s+or\s", RegexOptions.Compiled);

        private static IEnumerable<ChoiceConsequence> Edges =>
            Answers.SelectMany(option => Of(option, ConsequenceKind.Gate));

        private static IEnumerable<IGrouping<string, ChoiceConsequence>> Doors =>
            Edges.GroupBy(gate => gate.Target!, StringComparer.Ordinal);

        /// <summary>
        ///     The edges that share one sentence. <c>ChoiceGates.ClosedBecause</c>
        ///     hands back the note of a gate the life itself set, so a sentence
        ///     carried by one edge is only ever read by somebody who gave that
        ///     answer and is free to name what that answer did. A sentence carried
        ///     by several is read by lives that gave any one of them, and has to
        ///     hold for all of them.
        /// </summary>
        private static IEnumerable<IGrouping<string, ChoiceConsequence>> Reasons =>
            Edges.GroupBy(gate => gate.Note ?? string.Empty, StringComparer.Ordinal);

        [Fact]
        public void No_reason_offers_a_menu_of_causes()
        {
            // This stands where "a door shut from two places gives one reason"
            // used to. That rule described how the catalog happened to be written
            // rather than what a player is owed: the note is resolved from a gate
            // this life actually set, so two edges may give two reasons and each
            // is read only by the life that earned it. What a player is owed is
            // that the sentence in front of them is true of them, and what gives
            // a false one away mechanically is a list of alternatives: this, or
            // that. Only one of them can be theirs. Checked on every note rather
            // than only on the multi-edge ones the old rule looked at, since a
            // menu of causes is no truer on a door shut from one place
            Doors.Should().NotBeEmpty("the catalog gates nothing; has Gate been renamed?");

            foreach (var reason in Reasons)
                Menu.IsMatch(Key.Replace(reason.Key, string.Empty)).Should().BeFalse(
                    $"the note on {string.Join(", ", reason.Select(gate => gate.Target))} offers the player a " +
                    "choice of reasons and only one of them can be theirs; it has to hold for every life " +
                    "that reads it");
        }

        [Fact]
        public void A_reason_explains_the_one_door_it_is_written_against()
        {
            // The other half of the retired rule, and the half worth keeping. A
            // note says why a named option is gone, so one sentence standing
            // against two different missing options is describing at most one of
            // them
            foreach (var reason in Reasons)
                reason.Select(gate => gate.Target).Distinct(StringComparer.Ordinal).Should().HaveCount(1,
                    $"one sentence is written against {reason.Count()} different shut options, and it can " +
                    "only be saying why one of them is gone");
        }

        [Fact]
        public void The_door_want_of_a_teacher_shut_stops_denying_an_animal_the_life_holds()
        {
            // The reason moves, not the door. Two earlier answers hand over an animal
            // and state that they do, so the sentence beside the shut door reads
            // what the life is holding rather than the common case
            var gate = Edges.Single(edge => edge.Target == "cs_opt_you_could_ride_at_a_man");

            SceneCatalog.NoteFor(gate, Life("cs_opt_nobody_taught_you")).Should().Be(gate.Note,
                "nothing handed this life an animal, so the note it was written with is true of it");

            foreach (string handed in new[] { "cs_opt_the_halter_and_what_was_on_it", "cs_opt_a_road_and_no_roof" })
                SceneCatalog.NoteFor(gate, Life(handed, "cs_opt_nobody_taught_you")).Should().NotBe(gate.Note,
                    $"{handed} puts an animal in this character's hands and the panel says so, so the shut " +
                    "door cannot go on telling him he never had one");
        }

        /// <summary>What the answers name, as the run would have gathered them.</summary>
        private static IReadOnlyList<ChoiceConsequence> Life(params string[] optionIds)
        {
            var answers = new SceneAnswers();
            foreach (string optionId in optionIds)
                answers.Record(
                    SceneCatalog.All.Single(scene => scene.Find(optionId) != null).Id,
                    optionId);

            return SceneReading.Consequences(SceneCatalog.All, answers);
        }

        #endregion
    }
}
