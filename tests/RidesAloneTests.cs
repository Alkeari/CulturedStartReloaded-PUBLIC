using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     An answer that means nobody grants nobody.
    ///
    ///     The warband chapter is five rungs, and four of them are degrees of a
    ///     muster: the chapter asks the settings for the start type's range and
    ///     spends a band of it. The first rung is not a degree. It says the player
    ///     rides out alone, which is the absence of a muster and not the smallest
    ///     one the settings allow, and it was wired to the same arithmetic as its
    ///     neighbors: RangePreset.Minimum, which resolves to the range's own floor.
    ///     A monarch who chose to ride alone was handed the sixty men that start
    ///     type's Lowest Band is set to, and the panel beside him agreed, because
    ///     the panel is composed from the very figure the pick spends. Nothing on
    ///     screen ever contradicted itself; only the answer was not honored. An
    ///     earlier pass cured one start type by moving its Lowest Band default to
    ///     zero, which left the other seven and any player who ever raised the
    ///     setting.
    ///
    ///     None of it can be executed here. WarbandMenu names the narrative menu
    ///     types, ResourceStep names the campaign actions and CSSettings names MCM,
    ///     and that keeps all three out of this project and what is left is the
    ///     table as the file writes it. That is enough for this defect, because the
    ///     defect IS the table: the rung is mapped to a band or it is not.
    /// </summary>
    public class RidesAloneTests
    {
        private const string Warband = "CharacterCreation/Menus/WarbandMenu.cs";

        private sealed record Rung(string Id, string Title, string Band);

        [Fact]
        public void The_rung_that_rides_out_alone_spends_no_band()
        {
            var rungs = Rungs();

            rungs[0].Id.Should().Be("cs_warband_none",
                "the first rung of the chapter is the one that takes nobody along");
            rungs[0].Title.Should().Be("You Ride Alone");
            rungs[0].Band.Should().Be("null",
                "a band resolves through the start type's settings range, whose floor is a column of men; " +
                "an answer that means nobody has no band to spend");
        }

        [Fact]
        public void Every_other_rung_is_still_a_degree_of_a_muster()
        {
            var rungs = Rungs();

            rungs.Should().HaveCount(5, "the chapter offers five rungs beside the one that reads the life");
            rungs.Skip(1).Should().OnlyContain(rung => rung.Band.StartsWith("RangePreset.", StringComparison.Ordinal),
                "every rung that does mean a number takes it from the settings range");
        }

        [Fact]
        public void A_rung_with_no_band_writes_an_exact_nothing()
        {
            string chosen = ModSource.MemberBody(Warband, "void Choose");

            chosen.Should().Contain("CustomTroops = 0",
                "the pick has to leave a figure the apply pipeline reads as none; leaving the band alone " +
                "would let whatever band was chosen before stand");
        }

        /// <summary>
        ///     The chapter's three parallel tables, zipped back into the rungs they
        ///     describe between them. Read out of the file rather than copied here,
        ///     so a rung renamed or reordered fails loudly instead of leaving this
        ///     asserting about a table nobody uses.
        /// </summary>
        private static IReadOnlyList<Rung> Rungs()
        {
            var ids = Matches(ModSource.MemberBody(Warband, "LifeBand[] Windows"),
                @"new\(""(cs_warband_[a-z]+)""");
            var titles = Matches(ModSource.MemberBody(Warband, "Copy ="),
                @"\(""\{=CSR_Warband_\w+\}([^""]+)"",");
            var bands = ModSource.MemberBody(Warband, "Presets =")
                .Trim('{', '}', '\r', '\n', ' ')
                .Split(',')
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .ToList();

            ids.Should().HaveSameCount(titles, "every rung has a window and a caption");
            ids.Should().HaveSameCount(bands, "every rung says which band it spends, or that it spends none");

            return ids.Zip(titles, (id, title) => (id, title))
                .Zip(bands, (pair, band) => new Rung(pair.id, pair.title, band))
                .ToList();
        }

        private static List<string> Matches(string body, string pattern)
        {
            var found = Regex.Matches(body, pattern).Cast<Match>()
                .Select(match => match.Groups[1].Value).ToList();

            if (found.Count == 0)
                throw new InvalidOperationException(
                    $"{Warband} matches nothing for {pattern}; has the chapter been rewritten?");

            return found;
        }
    }
}
