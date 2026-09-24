using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     What the player meets when they click an option again, and whose gear
    ///     they are offered.
    /// </summary>
    public class GearDrawTests
    {
        private sealed class Piece
        {
            public Piece(string id, int tier, string? cultureId)
            {
                Id = id;
                Tier = tier;
                CultureId = cultureId;
            }

            public string Id { get; }

            public int Tier { get; }

            public string? CultureId { get; }
        }

        private static List<GearDraw.Candidate<Piece>> Pool(params Piece[] pieces)
        {
            var candidates = new List<GearDraw.Candidate<Piece>>();
            for (int position = 0; position < pieces.Length; position++)
                candidates.Add(new GearDraw.Candidate<Piece>(pieces[position], pieces[position].Id,
                    pieces[position].Tier, pieces[position].CultureId, pieces.Length - position));

            return candidates;
        }

        private static List<Piece> Wardrobe(string prefix, int tier, string? cultureId, int count)
        {
            var pieces = new List<Piece>();
            for (int index = 0; index < count; index++)
                pieces.Add(new Piece($"{prefix}_{index}", tier, cultureId));

            return pieces;
        }

        private static Func<int, int> Rolls(Random random) => max => random.Next(max);

        [Fact]
        public void Clicking_again_hands_back_a_different_piece()
        {
            var pool = Pool(Wardrobe("home", 5, "empire", 8).ToArray());
            var roll = Rolls(new Random(1));

            Piece? worn = null;
            for (int click = 0; click < 50; click++)
            {
                var drawn = GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, worn?.Id, roll);
                drawn.Should().NotBeNull();
                drawn!.Id.Should().NotBe(worn?.Id);
                worn = drawn;
            }
        }

        [Fact]
        public void A_slot_with_one_thing_left_repeats_it()
        {
            var pool = Pool(new Piece("only", 5, "empire"));
            var roll = Rolls(new Random(2));

            var first = GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, null, roll);
            var second = GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, first!.Id, roll);

            second.Should().BeSameAs(first);
        }

        [Fact]
        public void Fifty_clicks_reach_most_of_what_the_standing_allows()
        {
            var pool = Pool(Wardrobe("home", 5, "empire", 20).ToArray());
            var roll = Rolls(new Random(3));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            Piece? worn = null;
            for (int click = 0; click < 50; click++)
            {
                worn = GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, worn?.Id, roll);
                seen.Add(worn!.Id);
            }

            seen.Count.Should().BeGreaterThan(5);
        }

        [Fact]
        public void Gear_belonging_to_no_people_is_never_shut_out()
        {
            var pieces = Wardrobe("home", 5, "empire", 10);
            pieces.AddRange(Wardrobe("common", 5, null, 3));
            var pool = Pool(pieces.ToArray());
            var roll = Rolls(new Random(4));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int draw = 0; draw < 500; draw++)
                seen.Add(GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, null, roll)!.Id);

            seen.Should().Contain("common_0");
            seen.Should().Contain("common_1");
            seen.Should().Contain("common_2");
        }

        [Fact]
        public void Another_peoples_gear_is_reachable_but_stays_the_minority()
        {
            var pieces = Wardrobe("home", 5, "empire", 10);
            pieces.AddRange(Wardrobe("away", 5, "vlandia", 10));
            var pool = Pool(pieces.ToArray());
            var roll = Rolls(new Random(5));

            int home = 0;
            int away = 0;
            for (int draw = 0; draw < 2000; draw++)
            {
                var drawn = GearDraw.Pick(pool, 5, "empire", GearDraw.MinimumVariety, null, roll)!;
                if (drawn.CultureId == "empire") home++;
                else away++;
            }

            away.Should().BeGreaterThan(0, "another people's gear must stay reachable");
            home.Should().BeGreaterThan(away, "the character's own people must lead");
        }

        [Fact]
        public void A_character_with_no_people_of_their_own_leans_nowhere()
        {
            var pieces = Wardrobe("a", 5, "empire", 5);
            pieces.AddRange(Wardrobe("b", 5, "vlandia", 5));
            var pool = Pool(pieces.ToArray());
            var roll = Rolls(new Random(6));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int draw = 0; draw < 500; draw++)
                seen.Add(GearDraw.Pick(pool, 5, null, GearDraw.MinimumVariety, null, roll)!.Id);

            seen.Count.Should().Be(10);
        }

        [Fact]
        public void The_draw_reaches_down_a_tier_to_find_the_characters_own_people()
        {
            var pieces = Wardrobe("away", 5, "vlandia", 20);
            pieces.AddRange(Wardrobe("home", 4, "empire", 2));
            var pool = Pool(pieces.ToArray());

            var band = GearDraw.Band(pool, 5, GearDraw.MinimumVariety, "empire");

            band.Select(c => c.Id).Should().Contain("home_0");
        }

        [Fact]
        public void Tier_is_not_traded_away_for_variety()
        {
            var pieces = Wardrobe("best", 5, "empire", 8);
            pieces.AddRange(Wardrobe("worse", 2, "empire", 40));
            var pool = Pool(pieces.ToArray());

            var band = GearDraw.Band(pool, 5, GearDraw.MinimumVariety, "empire");

            band.Should().HaveCount(8);
            band.Should().OnlyContain(c => c.Tier == 5);
        }

        [Fact]
        public void A_thin_tier_widens_until_the_slot_can_vary()
        {
            var pieces = Wardrobe("best", 5, "empire", 2);
            pieces.AddRange(Wardrobe("next", 4, "empire", 9));
            var pool = Pool(pieces.ToArray());

            var band = GearDraw.Band(pool, 5, GearDraw.MinimumVariety, "empire");

            band.Should().HaveCount(11);
        }

        [Fact]
        public void An_empty_pool_draws_nothing_rather_than_throwing()
        {
            GearDraw.Pick(new List<GearDraw.Candidate<Piece>>(), 5, "empire",
                GearDraw.MinimumVariety, null, Rolls(new Random(7))).Should().BeNull();
        }
    }
}
