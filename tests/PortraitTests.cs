using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The route infers the character instead of asking for one, so the
    ///     inference is the part that fails silently. A wrong station is not a
    ///     crash, it is a player who answered like a king and started as a beggar.
    ///
    ///     These are properties rather than fixed expectations: the exact station
    ///     for a given set of answers is a tuning decision that will move, but the
    ///     ORDERING must not. A life that reached harder must never come out lower.
    ///
    ///     The properties that are about the whole space rather than about one life
    ///     are asserted over real walks of <see cref="SceneCatalog"/>, because that
    ///     is the only place they can fail: every defect this file is written
    ///     against compiled, read correctly, and was still wrong over the answers a
    ///     player can actually give. A station nobody can reach, a facet that says
    ///     the same thing about everyone and an age that never moves are all invisible
    ///     to a test of one hand-built life.
    ///
    ///     A life written FOR a station is walked out of the catalog too, for the
    ///     same reason. Every facet is a share of an ordinary life and the ordinary
    ///     life is measured from the catalog itself, so rewriting the scenes moves
    ///     what ordinary means: a hand-written life keeps its raw score and loses
    ///     its meaning, which is how five lives written for five stations came to
    ///     read as commoners without one number in them changing.
    /// </summary>
    public class PortraitTests
    {
        /// <summary>Enough walks that a station reachable by one life in fifty shows up.</summary>
        private const int Lives = 600;

        /// <summary>
        ///     How many times <see cref="LifeLeaningToward"/> re-answers every scene
        ///     before it takes the life it has. It stops early when a whole pass
        ///     changes nothing, which is what usually happens.
        /// </summary>
        private const int Passes = 6;

        private static ChoiceConsequence C(ConsequenceKind kind, string? target = null, int amount = 0) =>
            new(kind, target, amount);

        /// <summary>
        ///     A written-out set of consequences read as a life told to the end.
        ///
        ///     The lives written out below are only ever weighed against each other
        ///     on what the length does not move: a raw score, an ordering, a range.
        ///     Anything read as a SHARE of a life is built out of the catalog by
        ///     <see cref="LifeLeaningToward"/> instead, because a share is measured
        ///     against an ordinary life and a written one is a third of the length
        ///     of a played one however carefully it is written.
        /// </summary>
        private static Portrait Read(IReadOnlyList<ChoiceConsequence> consequences) =>
            Portrait.From(consequences, SceneCatalog.All.Count);

        private static Portrait Read(IReadOnlyList<ChoiceConsequence> consequences, int scenes) =>
            Portrait.From(consequences, scenes);

        [Fact]
        public void Nothing_answered_reads_as_a_commoner()
        {
            var portrait = Read(new List<ChoiceConsequence>(), scenes: 0);

            portrait.Station.Should().Be(StartType.Commoner);
        }

        /// <summary>
        ///     Both lives are answered out of the catalog, because this reads a
        ///     station and a station is decided on shares. Written by hand, the life
        ///     of titles held every title its author could think of and still said
        ///     less about a name than an ordinary life does, so it read as a commoner
        ///     and this went on passing on the strength of the other life reading as
        ///     something lower still.
        /// </summary>
        [Fact]
        public void A_life_of_titles_land_and_a_following_outranks_a_life_of_none()
        {
            // Everything a name, a seat and people disposed to you can be answered
            // into, with nothing behind it that broke
            var risen = ReadWhole(LifeLeaningToward(portrait =>
                portrait.Read(Portrait.Facet.Origins).Share
                - portrait.Read(Portrait.Facet.Hinge).Share));

            // The reverse of it: debts, enemies and years that went nowhere, with
            // nothing handed to it and nobody who ever taught it anything
            var nobody = ReadWhole(LifeLeaningToward(portrait =>
                portrait.Read(Portrait.Facet.Hinge).Share
                - portrait.Read(Portrait.Facet.Origins).Share
                - portrait.Read(Portrait.Facet.Schooling).Share));

            // Ordering, not identity: which station each lands on is tuning, but
            // the risen life must never read as the lower of the two
            Rank(risen.Station).Should().BeGreaterThan(Rank(nobody.Station));
        }

        [Fact]
        public void Lost_years_age_a_character_and_nothing_else_has_to()
        {
            var young = Read(new List<ChoiceConsequence>());
            var spent = Read(new[]
            {
                C(ConsequenceKind.LostYears, null, 8),
                C(ConsequenceKind.LostYears, null, 6)
            });

            ((int)spent.Age).Should().BeGreaterThan((int)young.Age);
        }

        [Fact]
        public void The_same_answers_always_read_the_same_way()
        {
            var answers = new[]
            {
                C(ConsequenceKind.Title, "lord"),
                C(ConsequenceKind.Item, "sword"),
                C(ConsequenceKind.Trait, "Valor", 1)
            };

            var first = Read(answers);
            var second = Read(answers);

            first.Station.Should().Be(second.Station);
            first.Age.Should().Be(second.Age);
            first.Specialization.Should().Be(second.Specialization);

            for (int place = 0; place < first.Standings.Count; place++)
            {
                first.Standings[place].Station.Should().Be(second.Standings[place].Station);
                first.Standings[place].Nearness.Should().Be(second.Standings[place].Nearness);
            }
        }

        [Fact]
        public void Specialisation_stays_within_its_own_scale()
        {
            var scattered = Read(new[]
            {
                C(ConsequenceKind.Item, "a"), C(ConsequenceKind.Place, "b"),
                C(ConsequenceKind.Ally, "c"), C(ConsequenceKind.Title, "d"),
                C(ConsequenceKind.Goodwill, "e", 1), C(ConsequenceKind.Enmity, "f", 1)
            });

            scattered.Specialization.Should().BeInRange(0.0, 1.0);
        }

        [Fact]
        public void Every_facet_is_readable_without_any_answers()
        {
            var portrait = Read(new List<ChoiceConsequence>(), scenes: 0);

            portrait.Origins.Score.Should().BeGreaterOrEqualTo(0);
            portrait.Intent.Score.Should().BeGreaterOrEqualTo(0);
            portrait.Seasoning.Score.Should().BeGreaterOrEqualTo(0);
        }

        /// <summary>
        ///     Returning to the same trade is depth. Returning to the same scruple is
        ///     a bent, and reading it as depth made a life of six honorable answers
        ///     come out as a mastered trade.
        /// </summary>
        [Fact]
        public void A_repeated_trade_is_mastery_and_a_repeated_scruple_is_not()
        {
            var tradesman = Read(new[]
            {
                C(ConsequenceKind.Item, "spear", 1), C(ConsequenceKind.Item, "spear", 1),
                C(ConsequenceKind.Item, "spear", 1), C(ConsequenceKind.Item, "spear", 1)
            });

            var honest = Read(new[]
            {
                C(ConsequenceKind.Trait, "Honor", 1), C(ConsequenceKind.Trait, "Honor", 1),
                C(ConsequenceKind.Trait, "Honor", 1), C(ConsequenceKind.Trait, "Honor", 1)
            });

            tradesman.Mastery.Score.Should().BeGreaterThan(honest.Mastery.Score);
            honest.Mastery.Score.Should().Be(0);
            honest.Inclination.Score.Should().BeGreaterThan(tradesman.Inclination.Score);
        }

        /// <summary>
        ///     Eight lives, each answered toward one station the whole way through
        ///     the real catalog, and each read back to see which station it earned.
        ///
        ///     This is not circular. The answering is steered by how near ONE named
        ///     station stands; the assertion is which station stands NEAREST. A
        ///     station no set of answers can reach fails here even though the answers
        ///     reached at it as hard as the catalog allows, and a reading that
        ///     collapses every life onto one station fails on the other seven.
        ///
        ///     Walked rather than written, for the reason at the top of this file: a
        ///     written life is a third of the length of a played one, so it reads as
        ///     a character the answers have barely described whatever it was written
        ///     to be, and it reads differently again every time the scenes are
        ///     rewritten without one number in it changing.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryStation))]
        public void A_life_built_for_a_station_reads_as_that_station(StartType station)
        {
            var life = LifeLeaningToward(portrait => Nearness(portrait, station));

            ReadWhole(life).Station.Should().Be(station,
                "answering every scene toward {0} has to arrive at {0}", station);
        }

        public static TheoryData<StartType> EveryStation()
        {
            var stations = new TheoryData<StartType>();

            foreach (StartType station in Enum.GetValues(typeof(StartType)))
                stations.Add(station);

            return stations;
        }

        /// <summary>
        ///     And each of those has to be a whole life, because a facet is a share
        ///     of one. A life that stops short reads as a character the answers have
        ///     barely described whatever it was answered toward, which would let the
        ///     test above pass on commoners forever.
        /// </summary>
        [Fact]
        public void A_life_built_for_a_station_is_told_to_the_end()
        {
            var walked = new HashSet<int>();
            foreach (var answers in Walks()) walked.Add(answers.Count);

            foreach (StartType station in Enum.GetValues(typeof(StartType)))
            {
                var life = LifeLeaningToward(portrait => Nearness(portrait, station));

                walked.Should().Contain(life.Count,
                    "a life answered toward {0} answers scenes no walked life answers", station);
            }
        }

        /// <summary>
        ///     A station no answering can reach is a promise the route does not keep.
        ///     Two of them were unreachable by construction and nothing said so.
        /// </summary>
        [Fact]
        public void Every_station_is_reached_by_some_life()
        {
            var reached = new Dictionary<StartType, int>();

            foreach (var answers in Walks())
            {
                var station = Portrait.From(Left(answers, answers.Count), answers.Count).Station;
                reached.TryGetValue(station, out int seen);
                reached[station] = seen + 1;
            }

            foreach (StartType station in Enum.GetValues(typeof(StartType)))
                reached.Should().ContainKey(station, "no answering reaches {0}", station);
        }

        /// <summary>
        ///     The eight are not equally likely and are not meant to be. A life
        ///     answered without reading falls to Commoner about half the time and to
        ///     each of the other seven about one time in twelve, which is a designed
        ///     spread and not a tuning accident: rare by accident, and reachable on
        ///     purpose.
        ///
        ///     This used to say that no station took half the space, written when one
        ///     station took three quarters of it and every other test stayed green.
        ///     What it was guarding against was a contest with only one entrant, and
        ///     that is what it still guards: the half belongs to Commoner by design
        ///     and nothing else may come near it, so a reading that collapses onto
        ///     any one station fails here exactly as it did before.
        ///
        ///     Bounds rather than the figures themselves, because six hundred walks
        ///     place a half to within a few points and the figures are re-measured
        ///     over twenty thousand lives in both catalogs when the table moves.
        /// </summary>
        [Fact]
        public void Commoner_is_about_half_the_space_and_no_other_station_is_near_it()
        {
            var reached = new Dictionary<StartType, int>();
            int lives = 0;

            foreach (var answers in Walks())
            {
                lives++;
                var station = Portrait.From(Left(answers, answers.Count), answers.Count).Station;
                reached.TryGetValue(station, out int seen);
                reached[station] = seen + 1;
            }

            reached.TryGetValue(StartType.Commoner, out int common);

            common.Should().BeGreaterThan(lives / 3,
                "a life that said nothing in particular has to have somewhere to land");
            common.Should().BeLessThan(lives * 2 / 3,
                "Commoner is where a life falls, not the only place it can fall");

            foreach (var pair in reached)
            {
                if (pair.Key == StartType.Commoner) continue;

                pair.Value.Should().BeLessThan(lives / 6,
                    "{0} takes too much of the space beside Commoner", pair.Key);
                pair.Value.Should().BeLessThan(common,
                    "{0} is commoner than Commoner", pair.Key);
            }
        }

        [Fact]
        public void The_age_walks_the_ladder_instead_of_pinning_to_one_band()
        {
            var ages = new Dictionary<StartingAge, int>();
            int lives = 0;

            foreach (var answers in Walks())
            {
                lives++;
                var age = Portrait.From(Left(answers, answers.Count), answers.Count).Age;
                ages.TryGetValue(age, out int seen);
                ages[age] = seen + 1;
            }

            ages.Count.Should().BeGreaterThan(2);
            foreach (var pair in ages)
                pair.Value.Should().BeLessThan(lives * 3 / 4, "{0} is nearly every life", pair.Key);
        }

        /// <summary>
        ///     The scenes run from a childhood to a name people use now, so the
        ///     character has to age through the telling. Reading the whole age off
        ///     the first answers put a fifty year old on the stage through chapters
        ///     written for a child.
        ///
        ///     Never backward, rather than always forward. A life can legitimately
        ///     finish young: the winter chapter offers an answer that costs no years
        ///     at all, and a player who takes it and spends nothing anywhere else
        ///     has told a whole life and is still the age they started. What must
        ///     never happen is a character who was older three answers ago.
        /// </summary>
        [Fact]
        public void The_character_never_ages_backward_as_the_life_is_told()
        {
            foreach (var answers in Walks())
            {
                var early = Portrait.From(Left(answers, 3), 3);
                var middle = Portrait.From(Left(answers, 8), 8);
                var told = Portrait.From(Left(answers, answers.Count), answers.Count);

                ((int)middle.Age).Should().BeGreaterOrEqualTo((int)early.Age);
                ((int)told.Age).Should().BeGreaterOrEqualTo((int)middle.Age);
            }
        }

        /// <summary>
        ///     And the telling must be able to age somebody, or the clause above is
        ///     satisfied by an age that never moves at all.
        /// </summary>
        [Fact]
        public void The_telling_ages_most_characters_forward()
        {
            int aged = 0;
            int lives = 0;

            foreach (var answers in Walks())
            {
                lives++;
                var early = Portrait.From(Left(answers, 3), 3);
                var told = Portrait.From(Left(answers, answers.Count), answers.Count);

                if ((int)told.Age > (int)early.Age) aged++;
            }

            aged.Should().BeGreaterThan(lives / 2, "the telling has to be what ages a character");
        }

        /// <summary>
        ///     A facet that reads the same for everybody is a sentence the player is
        ///     told whatever they answered, which is the same as telling them
        ///     nothing. Every facet must reach both ends of its own scale somewhere
        ///     in the space.
        /// </summary>
        [Fact]
        public void No_facet_says_the_same_thing_about_every_life()
        {
            var seen = new Dictionary<Portrait.Facet, HashSet<Portrait.Depth>>();
            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
                seen[facet] = new HashSet<Portrait.Depth>();

            foreach (var answers in Walks())
            {
                var portrait = Portrait.From(Left(answers, answers.Count), answers.Count);
                foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
                    seen[facet].Add(portrait.Read(facet).Reach);
            }

            foreach (var pair in seen)
                pair.Value.Count.Should().Be(3, "{0} never leaves one band", pair.Key);
        }

        /// <summary>
        ///     What each station is taken to be, written out a second time, the way
        ///     the weights are: which facets it asks to be loud and which it asks to
        ///     be quiet. Nothing above reads the station table, so a station quietly
        ///     redrawn into something else goes red here.
        ///
        ///     Commoner names neither side on any facet, and that is the claim: a
        ///     commoner is a life with nothing about it that stands out, not a life
        ///     that lacks something in particular.
        /// </summary>
        private readonly struct Stand
        {
            internal Stand(StartType station, Portrait.Facet facet, Portrait.Depth reach)
            {
                Station = station;
                Facet = facet;
                Reach = reach;
            }

            internal StartType Station { get; }
            internal Portrait.Facet Facet { get; }

            /// <summary>The band a life would read in if it met this station squarely.</summary>
            internal Portrait.Depth Reach { get; }
        }

        private static readonly Stand[] Stands =
        {
            // Born to something, taught, aimed at one thing hard enough to build it,
            // old because the founding took the years, and unbroken, because years
            // spent building a thing are years not spent falling out of one. The
            // break is what tells a crown made from a banner raised against one
            new(StartType.Monarch, Portrait.Facet.Origins, Portrait.Depth.Strong),
            new(StartType.Monarch, Portrait.Facet.Intent, Portrait.Depth.Strong),
            new(StartType.Monarch, Portrait.Facet.Schooling, Portrait.Depth.Strong),
            new(StartType.Monarch, Portrait.Facet.Seasoning, Portrait.Depth.Strong),
            new(StartType.Monarch, Portrait.Facet.Hinge, Portrait.Depth.Faint),

            // The birth and the teaching of a monarch, without the reaching, without
            // the years a founding takes, and without the break
            new(StartType.LandedVassal, Portrait.Facet.Origins, Portrait.Depth.Strong),
            new(StartType.LandedVassal, Portrait.Facet.Schooling, Portrait.Depth.Strong),
            new(StartType.LandedVassal, Portrait.Facet.Hinge, Portrait.Depth.Faint),
            new(StartType.LandedVassal, Portrait.Facet.Intent, Portrait.Depth.Faint),
            new(StartType.LandedVassal, Portrait.Facet.Seasoning, Portrait.Depth.Faint),

            // Trained into service and wholly pointed at it, with no name behind it
            // and no trade of its own: what a sworn man has is what he was taught
            // rather than the one thing he kept going back to
            new(StartType.LandlessVassal, Portrait.Facet.Schooling, Portrait.Depth.Strong),
            new(StartType.LandlessVassal, Portrait.Facet.Intent, Portrait.Depth.Strong),
            new(StartType.LandlessVassal, Portrait.Facet.Origins, Portrait.Depth.Faint),
            new(StartType.LandlessVassal, Portrait.Facet.Mastery, Portrait.Depth.Faint),

            // A skill worth money, the years of selling it, a break with settled
            // life, no name behind either and nobody's instruction
            new(StartType.Mercenary, Portrait.Facet.Mastery, Portrait.Depth.Strong),
            new(StartType.Mercenary, Portrait.Facet.Seasoning, Portrait.Depth.Strong),
            new(StartType.Mercenary, Portrait.Facet.Hinge, Portrait.Depth.Strong),
            new(StartType.Mercenary, Portrait.Facet.Origins, Portrait.Depth.Faint),
            new(StartType.Mercenary, Portrait.Facet.Schooling, Portrait.Depth.Faint),

            // Fell out of everything: no name, nobody's teaching, and no trade kept
            // at long enough to be good at
            new(StartType.Outlaw, Portrait.Facet.Hinge, Portrait.Depth.Strong),
            new(StartType.Outlaw, Portrait.Facet.Origins, Portrait.Depth.Faint),
            new(StartType.Outlaw, Portrait.Facet.Schooling, Portrait.Depth.Faint),
            new(StartType.Outlaw, Portrait.Facet.Mastery, Portrait.Depth.Faint),

            // A trade learned, kept at, and wholly aimed at, by somebody with no
            // name behind them: the trade is what they have instead of a birth
            new(StartType.CaravanMaster, Portrait.Facet.Mastery, Portrait.Depth.Strong),
            new(StartType.CaravanMaster, Portrait.Facet.Schooling, Portrait.Depth.Strong),
            new(StartType.CaravanMaster, Portrait.Facet.Intent, Portrait.Depth.Strong),
            new(StartType.CaravanMaster, Portrait.Facet.Origins, Portrait.Depth.Faint),

            // A break, something of its own to break away with, the house it broke
            // away from, and none of the years a founding takes: a banner goes up
            // the season the house can no longer hold it in
            new(StartType.RebelClan, Portrait.Facet.Hinge, Portrait.Depth.Strong),
            new(StartType.RebelClan, Portrait.Facet.Intent, Portrait.Depth.Strong),
            new(StartType.RebelClan, Portrait.Facet.Origins, Portrait.Depth.Strong),
            new(StartType.RebelClan, Portrait.Facet.Schooling, Portrait.Depth.Strong),
            new(StartType.RebelClan, Portrait.Facet.Seasoning, Portrait.Depth.Faint)
        };

        /// <summary>
        ///     The screen prints the seven sentences and then the station, so a
        ///     player reads both at once and a pair that cannot both be true tells
        ///     them only that the mod does not know what it thinks.
        ///
        ///     This asked one pair of that question before: a life told it was born
        ///     to a name that opened doors and then told it began as nobody in
        ///     particular. The pair could not happen because Commoner demanded a
        ///     birth that was not there, and that demand is exactly what put a life
        ///     answered at random on Commoner one time in forty. The eight shares are
        ///     a settled decision, so the demand had to go, and what replaces
        ///     the one pair is every pair: a station that asks a facet to be loud
        ///     standing beside a sentence saying it is barely there, or the reverse.
        ///
        ///     A ceiling rather than nought, because no station table reaches nought:
        ///     eight stations drawn over seven facets that move together leave every
        ///     life resembling its own station imperfectly somewhere. It was one life
        ///     in four while Commoner demanded a birth, and it is nearer one in
        ///     twenty-five now, measured over twenty thousand lives in both catalogs.
        ///     What must never come back is a reading that contradicts itself for a
        ///     quarter of players, and what must never hide inside the total is one
        ///     station doing all of it, which is why the worst single pair is held
        ///     down on its own.
        /// </summary>
        [Fact]
        public void The_station_a_life_earns_rarely_denies_one_of_its_own_sentences()
        {
            var denied = new Dictionary<string, int>(StringComparer.Ordinal);
            int against = 0;
            int lives = 0;

            foreach (var answers in Walks())
            {
                lives++;
                var portrait = Portrait.From(Left(answers, answers.Count), answers.Count);

                foreach (var stand in Stands)
                {
                    if (stand.Station != portrait.Station) continue;
                    if (portrait.Read(stand.Facet).Reach != Opposite(stand.Reach)) continue;

                    against++;
                    string pair = stand.Station + "/" + stand.Facet;
                    denied.TryGetValue(pair, out int seen);
                    denied[pair] = seen + 1;
                    break;
                }
            }

            against.Should().BeLessThan(lives / 10,
                "the reading contradicts itself too often to be worth printing");

            foreach (var pair in denied)
                pair.Value.Should().BeLessThan(lives / 25,
                    "{0} is where the reading disagrees with itself", pair.Key);
        }

        /// <summary>The band on the far side of ordinary from this one.</summary>
        private static Portrait.Depth Opposite(Portrait.Depth reach) =>
            reach == Portrait.Depth.Strong ? Portrait.Depth.Faint : Portrait.Depth.Strong;

        /// <summary>
        ///     Every station says what it stands on here, or one could be redrawn
        ///     into something else with nothing noticing. Commoner is the one that
        ///     names no facet, and it says so rather than being left out.
        /// </summary>
        [Fact]
        public void Every_station_says_which_facets_it_stands_on()
        {
            foreach (StartType station in Enum.GetValues(typeof(StartType)))
            {
                int named = 0;
                foreach (var stand in Stands)
                    if (stand.Station == station)
                        named++;

                if (station == StartType.Commoner)
                {
                    named.Should().Be(0, "a commoner stands out in no respect at all");
                    continue;
                }

                named.Should().BeGreaterThan(2,
                    "{0} is not written out here, so it could be redrawn quietly", station);
            }
        }

        /// <summary>
        ///     Every pair of stations has to contradict each other on some facet:
        ///     one asking it to stand above the ordinary life while the other asks
        ///     it to stand below.
        ///
        ///     Silence is not separation. Monarch asked for a name, teaching, an aim
        ///     and years; the caravan master asked for a trade, teaching and an aim,
        ///     and neither denied one thing the other wanted, so a life that was
        ///     well born, taught, driven, old AND masterful met both tables squarely
        ///     and which start it got was settled by how far each of them happened
        ///     to stand from the ordinary life. That is arithmetic deciding a
        ///     question the player answered, and a kingdom founded and a caravan run
        ///     are not one life scored twice.
        ///
        ///     Commoner is held to the same rule said the other way round. It names
        ///     all seven facets and asks every one of them to read exactly as an
        ///     ordinary life does, so any facet another station names at all is a
        ///     facet Commoner denies it. What it may never become is a station that
        ///     names nothing, because a station asking for nothing is met by every
        ///     life there is.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryPairOfStations))]
        public void No_two_stations_are_separated_by_distance_alone(StartType one, StartType other)
        {
            if (one == StartType.Commoner || other == StartType.Commoner)
            {
                var shaped = one == StartType.Commoner ? other : one;

                Sides(shaped).Should().NotBeEmpty(
                    "{0} names no facet, so nothing about it denies an ordinary life", shaped);
                return;
            }

            var theirs = Sides(other);
            var opposed = new List<Portrait.Facet>();

            foreach (var side in Sides(one))
                if (theirs.TryGetValue(side.Key, out var there) && side.Value != there)
                    opposed.Add(side.Key);

            opposed.Should().NotBeEmpty(
                "{0} and {1} name no facet on opposite sides of the ordinary life, so a life " +
                "meeting both is sorted by how far each of them stands rather than by anything " +
                "the player answered", one, other);
        }

        public static TheoryData<StartType, StartType> EveryPairOfStations()
        {
            var pairs = new TheoryData<StartType, StartType>();
            var stations = (StartType[])Enum.GetValues(typeof(StartType));

            for (int a = 0; a < stations.Length; a++)
            for (int b = a + 1; b < stations.Length; b++)
                pairs.Add(stations[a], stations[b]);

            return pairs;
        }

        /// <summary>Which side of the ordinary life a station asks each facet it names to stand on.</summary>
        private static Dictionary<Portrait.Facet, Portrait.Depth> Sides(StartType station)
        {
            var sides = new Dictionary<Portrait.Facet, Portrait.Depth>();

            foreach (var stand in Stands)
                if (stand.Station == station)
                    sides[stand.Facet] = stand.Reach;

            return sides;
        }

        /// <summary>
        ///     The life that reaches hardest for a name, which is where the pair
        ///     above can fail without any life being told it was born to one: that
        ///     test skips a life whose origins are not Strong, so a catalog no life
        ///     can be loud with a name in satisfies it by saying nothing at all.
        /// </summary>
        [Fact]
        public void A_life_loud_with_a_name_is_not_read_as_a_commoner()
        {
            var named = ReadWhole(LifeLeaningToward(
                portrait => portrait.Read(Portrait.Facet.Origins).Share));

            named.Origins.Reach.Should().Be(Portrait.Depth.Strong,
                "no set of answers reads as a life born to a name");
            named.Station.Should().NotBe(StartType.Commoner);
        }

        /// <summary>
        ///     The ranking is what a caller reads when it cannot use the station the
        ///     life earned. It has to hold every station, in one order, with the
        ///     earned one at its head, or a caller asking for the next nearest is
        ///     asking a different question than the one the station answered.
        /// </summary>
        [Fact]
        public void The_ranking_holds_every_station_once_nearest_first()
        {
            foreach (var answers in Walks())
            {
                var portrait = Portrait.From(Left(answers, answers.Count), answers.Count);
                var standings = portrait.Standings;

                standings.Count.Should().Be(Enum.GetValues(typeof(StartType)).Length);

                var named = new HashSet<StartType>();
                for (int place = 0; place < standings.Count; place++)
                {
                    named.Add(standings[place].Station).Should().BeTrue(
                        "{0} is in the ranking twice", standings[place].Station);

                    if (place > 0)
                        standings[place].Nearness.Should()
                            .BeLessOrEqualTo(standings[place - 1].Nearness);
                }

                standings[0].Station.Should().Be(portrait.Station);
            }
        }

        /// <summary>
        ///     Nothing answered resembles no station, so a caller reading the
        ///     ranking of an empty life is told that rather than handed a confident
        ///     second choice.
        /// </summary>
        [Fact]
        public void Nothing_answered_resembles_no_station_at_all()
        {
            var portrait = Read(new List<ChoiceConsequence>(), scenes: 0);

            foreach (var standing in portrait.Standings)
                standing.Nearness.Should().Be(0);

            portrait.Standings[0].Station.Should().Be(StartType.Commoner);
        }

        /// <summary>
        ///     The point of the ranking: a caller that cannot offer the station the
        ///     life earned takes the next one the life is nearest, and what it gets
        ///     is still an answer about that life. If the second choice were a
        ///     constant, this would come back as one station however the lives
        ///     differed.
        /// </summary>
        [Fact]
        public void The_station_after_the_earned_one_is_still_read_from_the_life()
        {
            var seconds = new HashSet<StartType>();

            foreach (var answers in Walks())
            {
                var portrait = Portrait.From(Left(answers, answers.Count), answers.Count);

                foreach (var standing in portrait.Standings)
                {
                    if (standing.Station == portrait.Station) continue;

                    seconds.Add(standing.Station);
                    break;
                }
            }

            seconds.Count.Should().BeGreaterThan(1, "the station after the earned one is a constant");
        }

        /// <summary>
        ///     What the weights in <see cref="Portrait"/> mean, written out a second
        ///     time.
        ///
        ///     That table is the mod's opinion about what a life adds up to: a gate
        ///     is schooling, an ally is a following, lost years are seasoning.
        ///     Nothing above reads it. Debt was turned from a slight mark against a
        ///     name into decisive evidence FOR one, so that a life of borrowing read
        ///     as a great house, and every test in this file stayed green: the
        ///     stations were all still reachable, the distribution still healthy,
        ///     and the reading simply wrong.
        ///
        ///     Written out here rather than read off the table, because a test that
        ///     reads the table proves only that the table agrees with itself. It
        ///     says the three things that may not change quietly: which WAY a kind
        ///     argues about a facet, which of two kinds argues LOUDER about the same
        ///     one, and which single facet a RETURN to the same named thing says
        ///     again. The weights themselves are tuning and are never asserted, so
        ///     moving one inside its band, or moving a whole facet's weights
        ///     together, leaves all of this green.
        ///
        ///     The return is named here because it is now a decision. Three kinds
        ///     speak equally loudly about two facets at once, so for those three the
        ///     weights alone cannot say which facet a return deepens, and while the
        ///     answer came from whichever row happened to be written first there was
        ///     nothing to write down: reordering two lines that say the same thing
        ///     changed what returning to the same place meant, and no assertion
        ///     could see it.
        /// </summary>
        /// <summary>
        ///     The kinds that say nothing about the PERSON, and are meant to.
        ///
        ///     The portrait reads which beginning a character is nearest, and a
        ///     household is not evidence of that: who is left alive at home is
        ///     settled by three chapters of its own, and a life that buried its
        ///     parents is no nearer being an outlaw than a monarch for it. Written
        ///     down as a claim of its own rather than left out, because a kind
        ///     nobody claims for is a kind whose weighting could be wired up
        ///     without a word of this going red, and the silence is asserted as
        ///     firmly as any weight: this run would read every station differently
        ///     the day a household started arguing about a facet.
        ///
        ///     An age said outright is the second, and its silence is the whole
        ///     point of it. Only Cultured Start declares one, that route derives
        ///     nothing, and the age it names is the player's answer rather than a
        ///     reading of anything: a facet that moved with it would put the answer
        ///     back into the reading through the side door.
        /// </summary>
        private static readonly ConsequenceKind[] SayNothingAboutThePerson =
            { ConsequenceKind.Household, ConsequenceKind.Age };

        private static readonly Claim[] Claims =
        {
            // A style is handed down or handed over, and either way somebody above
            // had already decided who you were. Carrying it is a little of what the
            // life is aimed at and a little proof that time passed, and a second
            // style is one more house saying the same thing about your birth
            new(ConsequenceKind.Title, Portrait.Facet.Origins, Force.Decisive, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Title, Portrait.Facet.Intent, Force.Slight),
            new(ConsequenceKind.Title, Portrait.Facet.Seasoning, Force.Slight),

            // A place ties a life both ways: where it came from and where it is
            // going, equally. Returning is what separates the two, and it separates
            // them toward the aim: a life that keeps naming one place is pointed at
            // it, and nobody is born somewhere twice
            new(ConsequenceKind.Place, Portrait.Facet.Origins, Force.Telling),
            new(ConsequenceKind.Place, Portrait.Facet.Intent, Force.Telling, onReturn: Return.SaysItAgain),

            // People disposed to help you are standing, and one of them taught you
            new(ConsequenceKind.Goodwill, Portrait.Facet.Origins, Force.Telling, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Goodwill, Portrait.Facet.Schooling, Force.Slight),

            // Somebody who rides with you is a following, and a little of the bent
            // that gathered them
            new(ConsequenceKind.Ally, Portrait.Facet.Inclination, Force.Slight),
            new(ConsequenceKind.Ally, Portrait.Facet.Intent, Force.Telling, onReturn: Return.SaysItAgain),

            // A thing in your hands is the tool of a trade, lightly a thing you were
            // handed and lightly a thing you were shown. Reaching for the same tool
            // again is the trade, which is the whole of what depth can be read from
            new(ConsequenceKind.Item, Portrait.Facet.Mastery, Force.Telling, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Item, Portrait.Facet.Origins, Force.Slight),
            new(ConsequenceKind.Item, Portrait.Facet.Schooling, Force.Slight),

            // A movement in a trait is a bent and nothing else, and the same
            // movement made twice is what makes a person one thing all through
            new(ConsequenceKind.Trait, Portrait.Facet.Inclination, Force.Telling, onReturn: Return.SaysItAgain),

            // Shutting a road elsewhere is commitment, which is what teaches and
            // what aims, plus a small turn in the life that did the shutting. The
            // two loud ones tie, and shutting the same road again is that discipline
            // kept to, which is teaching rather than aiming
            new(ConsequenceKind.Gate, Portrait.Facet.Schooling, Force.Telling, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Gate, Portrait.Facet.Intent, Force.Telling),
            new(ConsequenceKind.Gate, Portrait.Facet.Hinge, Force.Slight),

            // Borrowing turns a life and says a little about what it is aimed at
            // now, and it argues AGAINST a name: people with something behind them
            // are not usually the ones borrowing
            new(ConsequenceKind.Debt, Portrait.Facet.Hinge, Force.Telling, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Debt, Portrait.Facet.Intent, Force.Slight),
            new(ConsequenceKind.Debt, Portrait.Facet.Origins, Force.Slight, Reading.EvidenceAgainst),

            // An enemy is a turn, a thing to ride toward, and long enough in the
            // making to have taken years. Who hates you now says nothing about what
            // you were born into, which is why Origins is not on this list. These
            // two tie as well, and one enemy the life keeps colliding with is the
            // break it keeps breaking on rather than a second thing to want
            new(ConsequenceKind.Enmity, Portrait.Facet.Hinge, Force.Telling, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.Enmity, Portrait.Facet.Intent, Force.Telling),
            new(ConsequenceKind.Enmity, Portrait.Facet.Seasoning, Force.Slight),

            // Years that went nowhere are the whole of what seasons a person, they
            // turn the life that lost them, and they argue against both kinds of
            // skill: nobody was teaching and nothing was being practiced
            new(ConsequenceKind.LostYears, Portrait.Facet.Seasoning, Force.Decisive, onReturn: Return.SaysItAgain),
            new(ConsequenceKind.LostYears, Portrait.Facet.Hinge, Force.Telling),
            new(ConsequenceKind.LostYears, Portrait.Facet.Schooling, Force.Telling, Reading.EvidenceAgainst),
            new(ConsequenceKind.LostYears, Portrait.Facet.Mastery, Force.Slight, Reading.EvidenceAgainst)
        };

        /// <summary>
        ///     Every kind argues for what it is evidence of, against what it is
        ///     evidence against, and says NOTHING about the rest.
        ///
        ///     The silence is half of the claim and is asserted as firmly as the
        ///     rest: a facet every kind touches a little says the same thing about
        ///     every life and therefore says nothing, which is what Seasoning did
        ///     when it counted one for every consequence and made every character
        ///     fifty years old.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryKind))]
        public void A_kind_argues_only_about_the_facets_it_is_evidence_of(ConsequenceKind kind)
        {
            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
            {
                int claimed = Math.Sign(Toward(kind, facet));

                Math.Sign(Lands(kind, facet)).Should().Be(claimed,
                    "{0} {1} {2}", kind, Argues(claimed), facet);
            }
        }

        /// <summary>
        ///     Rank rather than weight, because the weights will be tuned again and
        ///     a test that pinned them would go red on every legitimate change until
        ///     somebody weakened it into saying nothing.
        ///
        ///     Two bands apart is the table saying one kind decides a facet and the
        ///     other barely marks it, and that has to hold strictly. One band apart
        ///     is a tuning distance: levelling the two is a decision somebody could
        ///     legitimately make, so only an inversion fails.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryFacet))]
        public void A_kind_that_argues_harder_about_a_facet_is_never_outweighed(Portrait.Facet facet)
        {
            foreach (var louder in Claims)
            {
                if (louder.Facet != facet) continue;

                foreach (var quieter in Claims)
                {
                    if (quieter.Facet != facet) continue;

                    int gap = Toward(louder) - Toward(quieter);
                    if (gap <= 0) continue;

                    int loud = Lands(louder.Kind, facet);
                    int quiet = Lands(quieter.Kind, facet);

                    if (gap >= 2)
                        loud.Should().BeGreaterThan(quiet,
                            "{0} {1} {2} and {3} only {4} it",
                            louder.Kind, Argues(Toward(louder)), facet,
                            quieter.Kind, Argues(Toward(quieter)));
                    else
                        loud.Should().BeGreaterOrEqualTo(quiet,
                            "{0} argues at least as hard about {1} as {2} does",
                            louder.Kind, facet, quieter.Kind);
                }
            }
        }

        /// <summary>
        ///     The break that went unnoticed, named on its own because it is the one
        ///     a reader of this file should be able to find: a life of debts must
        ///     never read as better evidence of a great name than a life that
        ///     answered nothing at all.
        /// </summary>
        [Fact]
        public void A_life_of_debts_is_not_evidence_of_a_great_name()
        {
            var borrowed = Read(new[]
            {
                C(ConsequenceKind.Debt, "a caravan master", 500),
                C(ConsequenceKind.Debt, "a moneylender", 900),
                C(ConsequenceKind.Debt, "a garrison captain", 200)
            });

            var silent = Read(new List<ChoiceConsequence>());

            borrowed.Origins.Score.Should().BeLessThan(silent.Origins.Score,
                "borrowing is what somebody with nothing behind them does");
        }

        /// <summary>
        ///     Returning to the same thing deepens the one facet the claims name for
        ///     that kind, and only that: doing a thing twice is the same statement
        ///     made again rather than a new one. Counting any repeat as depth made
        ///     six honorable answers read as a mastered trade.
        ///
        ///     Named rather than inferred, because three kinds speak equally loudly
        ///     about two facets and for those three the weights cannot say which is
        ///     deepened. This used to ask only that the deepened facet was one of
        ///     the loudest, which those three kinds satisfy either way, so the
        ///     choice could change without a word of this going red. What the two
        ///     assertions say now is that the deepened facet is the one named, and
        ///     that the named one is still a facet the kind is loudest about: a
        ///     return cannot say something the kind barely says in the first place.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryKind))]
        public void Returning_to_the_same_thing_deepens_the_facet_that_kind_names(ConsequenceKind kind)
        {
            const int returns = 4;
            var deepened = new List<Portrait.Facet>();

            if (Array.IndexOf(SayNothingAboutThePerson, kind) >= 0)
            {
                // Saying nothing again is still nothing. Asserted rather than
                // skipped: this is what goes red the day the kind is given a weight
                foreach (Portrait.Facet quiet in Enum.GetValues(typeof(Portrait.Facet)))
                    Lands(kind, quiet, returns).Should().Be(0,
                        "{0} is claimed to say nothing about the person and argues about {1}", kind, quiet);

                return;
            }

            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
            {
                int once = Lands(kind, facet);
                int again = Lands(kind, facet, returns);

                if (again == once * returns) continue;

                again.Should().BeGreaterThan(once * returns,
                    "returning to the same {0} cannot argue less about {1} than doing it once did",
                    kind, facet);

                deepened.Add(facet);
            }

            deepened.Should().HaveCount(1,
                "a life that keeps naming one thing says one thing again, not several");

            var named = Deepened(kind);

            named.Should().NotBeNull(
                "nothing here says what returning to the same {0} says again", kind);

            Loudest(kind).Should().Contain(named!.Value,
                "{0} is claimed to deepen {1}, which is not what it says loudest", kind, named);

            deepened[0].Should().Be(named,
                "returning to the same {0} deepens {1}, not {2}", kind, deepened[0], named);
        }

        /// <summary>
        ///     A kind nothing claims for is a kind whose weighting could be rewired
        ///     without one assertion above noticing, which is the hole all of this
        ///     was written to close. Adding a kind to the enum has to mean saying
        ///     what it is evidence of.
        /// </summary>
        [Fact]
        public void Every_consequence_kind_says_what_it_is_evidence_of()
        {
            foreach (ConsequenceKind kind in Enum.GetValues(typeof(ConsequenceKind)))
            {
                bool spoken = Array.IndexOf(SayNothingAboutThePerson, kind) >= 0;
                foreach (var claim in Claims)
                    if (claim.Kind == kind)
                        spoken = true;

                spoken.Should().BeTrue(
                    "{0} is not written out here, so nothing would notice it being rewired", kind);
            }
        }

        /// <summary>
        ///     And no facet may be argued by all of them, whatever each one argues:
        ///     a facet every consequence touches moves with the LENGTH of a life
        ///     rather than with what the life said, so it tells every player the
        ///     same thing.
        /// </summary>
        [Fact]
        public void No_facet_is_argued_by_every_kind()
        {
            int kinds = Enum.GetValues(typeof(ConsequenceKind)).Length;

            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
            {
                int spoke = 0;
                foreach (ConsequenceKind kind in Enum.GetValues(typeof(ConsequenceKind)))
                    if (Lands(kind, facet) != 0)
                        spoke++;

                spoke.Should().BeLessThan(kinds,
                    "{0} is touched by every kind, so it says the same thing about every life", facet);
            }
        }

        public static TheoryData<ConsequenceKind> EveryKind()
        {
            var kinds = new TheoryData<ConsequenceKind>();

            foreach (ConsequenceKind kind in Enum.GetValues(typeof(ConsequenceKind)))
                kinds.Add(kind);

            return kinds;
        }

        public static TheoryData<Portrait.Facet> EveryFacet()
        {
            var facets = new TheoryData<Portrait.Facet>();

            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
                facets.Add(facet);

            return facets;
        }

        /// <summary>How hard a kind argues, in the vocabulary the weights are written in.</summary>
        private enum Force
        {
            Slight = 1,
            Telling = 2,
            Decisive = 3
        }

        /// <summary>Which way a kind argues about a facet.</summary>
        private enum Reading
        {
            EvidenceFor,
            EvidenceAgainst
        }

        /// <summary>
        ///     Whether returning to the same named thing says this claim over again.
        ///     Exactly one claim per kind says it does, and a return says that one
        ///     and nothing else: doing a thing twice is the same statement made
        ///     again rather than a new one.
        /// </summary>
        private enum Return
        {
            SaysNothingNew,
            SaysItAgain
        }

        /// <summary>One thing a kind is taken to be evidence of, and how strongly.</summary>
        private readonly struct Claim
        {
            internal Claim(ConsequenceKind kind, Portrait.Facet facet, Force force,
                Reading reading = Reading.EvidenceFor,
                Return onReturn = Return.SaysNothingNew)
            {
                Kind = kind;
                Facet = facet;
                Force = force;
                Reading = reading;
                OnReturn = onReturn;
            }

            internal ConsequenceKind Kind { get; }
            internal Portrait.Facet Facet { get; }
            internal Force Force { get; }
            internal Reading Reading { get; }
            internal Return OnReturn { get; }
        }

        /// <summary>
        ///     What one thing of this kind, happening <paramref name="times"/> times
        ///     to the same named thing, lands on one facet.
        ///
        ///     A raw score rather than a share, because a share is measured against
        ///     an ordinary life and moves whenever the catalog is rewritten, while
        ///     what a kind is evidence of must not. The amount is left at zero so
        ///     the probe reads the kind alone: a consequence that moved something
        ///     backward adds a turn of its own, whatever kind it is.
        /// </summary>
        private static int Lands(ConsequenceKind kind, Portrait.Facet facet, int times = 1)
        {
            var happened = new List<ChoiceConsequence>();
            for (int time = 0; time < times; time++)
                happened.Add(C(kind, "the same thing"));

            return Read(happened).Read(facet).Score;
        }

        /// <summary>How hard, and which way, the claims say a kind argues about a facet.</summary>
        private static int Toward(ConsequenceKind kind, Portrait.Facet facet)
        {
            foreach (var claim in Claims)
                if (claim.Kind == kind && claim.Facet == facet)
                    return Toward(claim);

            return 0;
        }

        private static int Toward(Claim claim) =>
            (int)claim.Force * (claim.Reading == Reading.EvidenceAgainst ? -1 : 1);

        /// <summary>
        ///     The facets a kind argues hardest FOR, which is what a return to the
        ///     same thing says again. More than one where the claims tie, because a
        ///     tie is the table declining to say which.
        /// </summary>
        private static List<Portrait.Facet> Loudest(ConsequenceKind kind)
        {
            int hardest = 0;
            foreach (var claim in Claims)
                if (claim.Kind == kind && Toward(claim) > hardest)
                    hardest = Toward(claim);

            var loudest = new List<Portrait.Facet>();
            if (hardest <= 0) return loudest;

            foreach (var claim in Claims)
                if (claim.Kind == kind && Toward(claim) == hardest)
                    loudest.Add(claim.Facet);

            return loudest;
        }

        /// <summary>
        ///     The one facet the claims say a return to the same named thing says
        ///     again, or null where a kind has not been given one. Null is a failure
        ///     rather than a default: a kind whose return nothing names is a kind
        ///     whose depth could be moved to another facet quietly.
        /// </summary>
        private static Portrait.Facet? Deepened(ConsequenceKind kind)
        {
            Portrait.Facet? named = null;

            foreach (var claim in Claims)
            {
                if (claim.Kind != kind || claim.OnReturn != Return.SaysItAgain) continue;

                named.Should().BeNull(
                    "{0} is claimed to say two different things again on a return", kind);
                named = claim.Facet;
            }

            return named;
        }

        /// <summary>The claim in words, so a failure reads as the opinion it broke.</summary>
        private static string Argues(int toward) =>
            toward > 0 ? "is evidence of"
            : toward < 0 ? "is evidence against"
            : "says nothing about";

        /// <summary>Where a station sits on the ladder a player would recognize.</summary>
        private static int Rank(StartType station) => station switch
        {
            StartType.Monarch => 7,
            StartType.LandedVassal => 6,
            StartType.RebelClan => 5,
            StartType.LandlessVassal => 4,
            StartType.CaravanMaster => 3,
            StartType.Mercenary => 2,
            StartType.Commoner => 1,
            StartType.Outlaw => 0,
            _ => 0
        };

        /// <summary>
        ///     Whole lives walked through the real catalog, gates honored, from a
        ///     fixed seed so the sample is the same every run and a failure is a
        ///     failure rather than a bad afternoon. Each is the answers in the order
        ///     the scenes put them, so a test can read a life part told as well as a
        ///     life finished.
        /// </summary>
        private static IEnumerable<IReadOnlyList<SceneOption>> Walks()
        {
            var random = new Random(1066);

            for (int life = 0; life < Lives; life++)
            {
                var answers = new List<SceneOption>();
                var shut = new HashSet<string>(StringComparer.Ordinal);

                foreach (var scene in SceneCatalog.All)
                {
                    var open = new List<SceneOption>();
                    foreach (var option in scene.Options)
                        if (!shut.Contains(option.Id))
                            open.Add(option);

                    if (open.Count == 0) continue;

                    var chosen = open[random.Next(open.Count)];
                    answers.Add(chosen);

                    foreach (var consequence in chosen.Consequences)
                        if (consequence.Kind == ConsequenceKind.Gate && consequence.Target != null)
                            shut.Add(consequence.Target);
                }

                yield return answers;
            }
        }

        /// <summary>
        ///     A life a player could answer their way into, leaning toward one thing
        ///     the whole way: every scene is re-answered in turn and the answer that
        ///     carries <paramref name="toward"/> furthest is kept, until a pass over
        ///     the scenes changes nothing.
        ///
        ///     Only whole lives are ever weighed. A facet is a share of a life told
        ///     to the end, so a part-told one reads as part of a person however it
        ///     was answered, and weighing those would steer the answers by how much
        ///     had been said rather than by what it said.
        /// </summary>
        private static IReadOnlyList<SceneOption> LifeLeaningToward(Func<Portrait, double> toward)
        {
            var preferred = new string?[SceneCatalog.All.Count];
            double furthest = toward(ReadWhole(Answered(preferred)));

            for (int pass = 0; pass < Passes; pass++)
            {
                bool moved = false;

                for (int scene = 0; scene < preferred.Length; scene++)
                {
                    var held = preferred[scene];

                    foreach (var option in SceneCatalog.All[scene].Options)
                    {
                        preferred[scene] = option.Id;
                        double reached = toward(ReadWhole(Answered(preferred)));

                        if (reached <= furthest) continue;

                        furthest = reached;
                        held = option.Id;
                        moved = true;
                    }

                    preferred[scene] = held;
                }

                if (!moved) break;
            }

            return Answered(preferred);
        }

        /// <summary>
        ///     One life told to the end: the preferred answer to each scene where
        ///     nothing earlier has shut it, and the first answer still open where
        ///     something has. Gates are honored as <see cref="Walks"/> honors them,
        ///     so what comes back is a life the run could put in front of a player.
        /// </summary>
        private static List<SceneOption> Answered(IReadOnlyList<string?> preferred)
        {
            var answers = new List<SceneOption>();
            var shut = new HashSet<string>(StringComparer.Ordinal);

            for (int scene = 0; scene < SceneCatalog.All.Count; scene++)
            {
                SceneOption? taken = null;

                foreach (var option in SceneCatalog.All[scene].Options)
                {
                    if (shut.Contains(option.Id)) continue;

                    taken ??= option;

                    if (!string.Equals(option.Id, preferred[scene], StringComparison.Ordinal)) continue;

                    taken = option;
                    break;
                }

                if (taken == null) continue;

                answers.Add(taken);

                foreach (var consequence in taken.Consequences)
                    if (consequence.Kind == ConsequenceKind.Gate && consequence.Target != null)
                        shut.Add(consequence.Target);
            }

            return answers;
        }

        /// <summary>The portrait of a life told to the end.</summary>
        private static Portrait ReadWhole(IReadOnlyList<SceneOption> answers) =>
            Portrait.From(Left(answers, answers.Count), answers.Count);

        /// <summary>How near one named station stands in a portrait's own ranking.</summary>
        private static double Nearness(Portrait portrait, StartType station)
        {
            foreach (var standing in portrait.Standings)
                if (standing.Station == station)
                    return standing.Nearness;

            return 0;
        }

        /// <summary>What the first <paramref name="scenes"/> answers of a walked life left behind.</summary>
        private static List<ChoiceConsequence> Left(IReadOnlyList<SceneOption> answers, int scenes)
        {
            var consequences = new List<ChoiceConsequence>();
            int taken = Math.Min(scenes, answers.Count);

            for (int answer = 0; answer < taken; answer++)
                consequences.AddRange(answers[answer].Consequences);

            return consequences;
        }
    }
}
