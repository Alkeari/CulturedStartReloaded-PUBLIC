using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The character at the end of the route, rather than any step on the way
    ///     to it.
    ///
    ///     Every defect the last playtest turned up was invisible to a green
    ///     build, a green suite and several readings of the code, because in every
    ///     case the code ran and nothing threw: the character that came out was
    ///     simply wrong. A character stronger than any lord alive. Two friends the
    ///     player had never met. A saddle worn and a second one carried. So
    ///     nothing here asserts about a scene, an option or a table. Each one
    ///     walks lives to their end and asks what arrived.
    ///
    ///     The catalog branches, so one walk proves nothing: every assertion is
    ///     asked of thousands of lives, and every failure carries the trail that
    ///     produced it so the life can be answered again by hand.
    ///
    ///     What cannot be asked here is anything that needs the running game:
    ///     skill levels, unspent pools, what is worn against what is carried. Those
    ///     sit behind engine types this project cannot reference, and they
    ///     are asserted by the scratchpad harness that links the real assemblies.
    /// </summary>
    public class EndStateTests
    {
        private static IReadOnlyList<Life> Lives => Walked.Value;

        private static readonly Lazy<IReadOnlyList<Life>> Walked = new(() =>
            LifeHarness.Lives().Concat(LifeHarness.LifePerAnswer()).ToList());

        [Fact]
        public void Every_life_the_route_can_tell_is_told_to_its_end()
        {
            // A gate that shuts the last answer to a scene strands the player on a
            // screen with nothing to press. The walk throws rather than returning
            // a short life, so this asserting the length is what makes that visible
            foreach (var life in Lives)
                life.Chosen.Should().HaveCount(SceneCatalog.All.Count,
                    $"a life stopped short of the end: {life.Trail}");
        }

        [Fact]
        public void Every_life_leaves_something_on_the_character()
        {
            // A route that can be answered thirteen times and leave nobody changed
            // is a route the player spent twenty minutes on for nothing
            foreach (var life in Lives)
                life.Left.Should().NotBeEmpty($"nothing at all came of this life: {life.Trail}");
        }

        #region What the panel promised against what the character got

        [Fact]
        public void Everything_the_panel_promised_reaches_the_character_exactly_as_often_as_promised()
        {
            // The panel states the option's consequences one line at a time, and
            // the outcome is that same list sorted into the kinds the steps apply.
            // Between those two is the only place a promise can be quietly folded,
            // dropped or doubled, and the player is holding the sentence either
            // way. Items are the case that has actually broken: two of a thing is
            // two lines in the panel and has to be two grants
            foreach (var life in Lives)
            {
                foreach (var kind in new[]
                         {
                             ConsequenceKind.Item, ConsequenceKind.Ally,
                             ConsequenceKind.Place, ConsequenceKind.Title
                         })
                {
                    Carried(life.Outcome, kind).Should().BeEquivalentTo(Promised(life, kind),
                        $"{kind} promised and {kind} carried differ: {life.Trail}");
                }

                life.Outcome.Debt.Should().Be(Sum(life, ConsequenceKind.Debt),
                    $"the debt the panel stated is not the debt the character owes: {life.Trail}");

                life.Outcome.LostYears.Should().Be(Sum(life, ConsequenceKind.LostYears),
                    $"the years the panel said were lost are not the years lost: {life.Trail}");
            }
        }

        [Fact]
        public void Nothing_reaches_the_character_that_no_answer_ever_promised()
        {
            // The other direction, which no amount of reading the apply code would
            // find: an outcome that invented an entry would hand the player
            // something they were never told about, and that is ruled out as
            // firmly as a report with no result behind it
            foreach (var life in Lives)
            {
                var promised = life.Left.Where(c => c.Target != null)
                    .Select(c => c.Target!)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (string carried in life.Outcome.Items
                             .Concat(life.Outcome.Allies)
                             .Concat(life.Outcome.Places)
                             .Concat(life.Outcome.Titles)
                             .Concat(life.Outcome.Traits.Keys))
                    promised.Should().Contain(carried,
                        $"{carried} arrived and no answer promised it: {life.Trail}");
            }
        }

        [Fact]
        public void Goodwill_and_enmity_with_one_people_cancel_rather_than_both_standing()
        {
            // A life that stood by the headmen twice and crossed them twice leaves
            // them undecided. Carrying both would have the pipeline move one
            // relation up and then the same relation down, and whichever ran last
            // would decide what the player got
            foreach (var life in Lives)
            foreach (var pair in life.Outcome.Relations)
            {
                int net = life.Left
                    .Where(c => Group(c.Target) == pair.Key)
                    .Sum(c => c.Kind switch
                    {
                        ConsequenceKind.Goodwill => Amount(c),
                        ConsequenceKind.Enmity => -Amount(c),
                        _ => 0
                    });

                pair.Value.Should().Be(net,
                    $"what {pair.Key} think of this character is not what the answers add up to: {life.Trail}");
            }
        }

        [Fact]
        public void No_life_is_promised_more_of_a_trait_than_a_character_can_hold()
        {
            // The apply step clamps to the trait's own range, so the CHARACTER is
            // always right. The player is not: a life told four times that it is
            // counted more honorable and given two steps of honor was told
            // something untrue twice
            foreach (var life in Lives)
            foreach (var pair in life.Outcome.Traits)
                Math.Abs(pair.Value).Should().BeLessThanOrEqualTo(TraitRange,
                    $"{pair.Key} is promised {pair.Value} and the game holds {TraitRange}: {life.Trail}");
        }

        [Fact]
        public void No_answer_promises_the_same_thing_twice_in_one_breath()
        {
            // Two of one item inside a single answer is the panel printing the same
            // line twice to itself, and the character carrying a spare of something
            // they were told about once. Across two scenes it is a life that came by
            // the thing twice, which is a different claim and a legitimate one
            foreach (var scene in SceneCatalog.All)
            foreach (var option in scene.Options)
            {
                var named = option.Consequences
                    .Where(c => c.Kind != ConsequenceKind.Trait && c.Target != null)
                    .Select(c => $"{c.Kind}:{c.Target}")
                    .ToList();

                named.Should().OnlyHaveUniqueItems($"{option.Id} says the same thing twice");
            }
        }

        #endregion

        #region The life the player actually lived

        [Fact]
        public void Two_players_who_answered_alike_arrive_as_the_same_character()
        {
            // Everything downstream of the route reads one list, so a life read
            // twice has to read the same twice. The station and the age are written
            // onto the session on EVERY answer, and the chapters below branch on
            // them: a reading that moved would take a player through one set of
            // chapters and build another character at the end of them
            foreach (var life in Lives.Take(400))
            {
                var again = Replay(life);

                again.Reading.Station.Should().Be(life.Reading.Station, $"the station moved: {life.Trail}");
                again.Reading.Age.Should().Be(life.Reading.Age, $"the age moved: {life.Trail}");
                Carried(again.Outcome, ConsequenceKind.Item)
                    .Should().Equal(Carried(life.Outcome, ConsequenceKind.Item), $"the gear moved: {life.Trail}");
                again.Outcome.Debt.Should().Be(life.Outcome.Debt, $"the debt moved: {life.Trail}");
            }
        }

        [Fact]
        public void Answering_a_scene_again_leaves_nothing_of_the_life_abandoned()
        {
            // The game's own Back button reaches every scene behind the player, and
            // a scene answered differently is a different life from there on: some
            // of the scenes that followed may not even be put again. An answer left
            // behind would have the character keep something from a life the player
            // walked away from, which is the defect that made a run abandoned into
            // the Start Editor still carry the scenes' age
            foreach (var life in Lives.Take(400))
            {
                for (int at = 0; at < life.Chosen.Count; at += 4)
                {
                    var answers = new SceneAnswers();
                    foreach (var option in life.Chosen)
                        answers.Record(SceneOf(option).Id, option.Id);

                    var scene = SceneOf(life.Chosen[at]);
                    var instead = scene.Options.First(option => option.Id != life.Chosen[at].Id);
                    answers.Replace(scene.Id, instead.Id);

                    answers.InOrder.Should().HaveCount(at + 1,
                        $"answering {scene.Id} again kept answers from the life it replaced: {life.Trail}");

                    SceneReading.Consequences(SceneCatalog.All, answers)
                        .Should().BeEquivalentTo(
                            life.Chosen.Take(at).SelectMany(option => option.Consequences)
                                .Concat(instead.Consequences),
                            $"the abandoned life left something behind: {life.Trail}");
                }
            }
        }

        #endregion

        #region Coverage

        [Fact]
        public void Every_answer_the_catalog_holds_is_one_some_life_can_actually_give()
        {
            // An answer gated shut in every life that could reach it is content the
            // player is never offered, and nothing about the catalog says so: it
            // reads exactly like an answer that works. Four options on one screen
            // were unreachable for this reason and the build was green throughout
            var given = Lives.SelectMany(life => life.Chosen).Select(option => option.Id)
                .ToHashSet(StringComparer.Ordinal);

            SceneCatalog.All.SelectMany(scene => scene.Options).Select(option => option.Id)
                .Where(id => !given.Contains(id))
                .Should().BeEmpty("an answer no life can give is an answer nobody will ever read");
        }

        [Fact]
        public void Every_station_the_mod_builds_is_the_end_of_some_life()
        {
            // The eight scenarios are a closed set the mod promises, and the guided
            // route is the only way most players will ever meet them. A station no
            // life reads as is eight scenario appliers' worth of code the route
            // cannot reach
            var reached = Lives.Select(life => life.Reading.Station).ToHashSet();

            reached.Should().BeEquivalentTo(Enum.GetValues(typeof(StartType)).Cast<StartType>(),
                "every station the mod can build has to be somewhere a written life can arrive");
        }

        [Fact]
        public void Every_age_the_game_offers_is_the_end_of_some_life()
        {
            Lives.Select(life => life.Reading.Age).ToHashSet()
                .Should().BeEquivalentTo(Enum.GetValues(typeof(StartingAge)).Cast<StartingAge>(),
                    "an age band no life reaches is a band the player is shown and cannot have");
        }

        #endregion

        #region Reading the lives

        /// <summary>What a personality trait runs to in the game, either way from nothing.</summary>
        private const int TraitRange = 2;

        private static int Amount(ChoiceConsequence consequence) =>
            consequence.Amount == 0 ? 1 : consequence.Amount;

        private static int Sum(Life life, ConsequenceKind kind) =>
            life.Left.Where(c => c.Kind == kind).Sum(Amount);

        /// <summary>What the panel stated, as one entry per thing the player was promised.</summary>
        private static IReadOnlyList<string> Promised(Life life, ConsequenceKind kind)
        {
            var named = new List<string>();
            foreach (var consequence in life.Left)
            {
                if (consequence.Kind != kind || consequence.Target == null) continue;

                // An item states a count and the rest state themselves once
                int times = kind == ConsequenceKind.Item ? Amount(consequence) : 1;
                for (int i = 0; i < times; i++) named.Add(consequence.Target);
            }

            return named;
        }

        private static IReadOnlyList<string> Carried(SceneOutcome outcome, ConsequenceKind kind) => kind switch
        {
            ConsequenceKind.Item => outcome.Items,
            ConsequenceKind.Ally => outcome.Allies,
            ConsequenceKind.Place => outcome.Places,
            ConsequenceKind.Title => outcome.Titles,
            _ => Array.Empty<string>()
        };

        /// <summary>
        ///     The group an id names, asked of the outcome itself rather than
        ///     restated, so this cannot disagree with the mapping under test.
        /// </summary>
        private static RelationEffect Group(string? target)
        {
            if (target == null) return RelationEffect.None;

            var read = SceneOutcome.From(new[]
            {
                new ChoiceConsequence(ConsequenceKind.Goodwill, target, 1)
            });

            return read.Relations.Count == 0 ? RelationEffect.None : read.Relations.Keys.First();
        }

        private static Scene SceneOf(SceneOption option) =>
            SceneCatalog.All.First(scene => scene.Find(option.Id) != null);

        /// <summary>The same answers given again, walked through the real run rather than copied.</summary>
        private static Life Replay(Life life)
        {
            int at = 0;
            return LifeHarness.Walk((_, offered) =>
            {
                var wanted = life.Chosen[at++];
                return offered.First(option => option.Id == wanted.Id);
            });
        }

        #endregion
    }
}
