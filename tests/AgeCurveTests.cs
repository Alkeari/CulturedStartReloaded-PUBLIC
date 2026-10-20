using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The rule Cultured Start Revamped's age menu moves a life by: the world's
    ///     own curve below the age the life came to, a straight close toward a
    ///     whole life above it, the age the life came to changing nothing, and the
    ///     menu's own bounds holding.
    /// </summary>
    public class AgeCurveTests
    {
        private const int Floor = 18;

        /// <summary>A small world whose people get better to thirty and no better after.</summary>
        private static readonly (int Age, int Best)[] World =
        {
            (18, 130), (19, 150), (21, 160), (23, 190), (25, 200), (27, 215), (29, 220), (31, 230),
            (35, 220), (40, 225), (45, 215), (50, 230)
        };

        private static readonly double[] Curve = AgeCurve.Measure(World, Floor, AgeCurve.Ceiling);

        private static double BestAt(int age) => AgeCurve.At(Curve, Floor, age);

        [Fact]
        public void The_curve_never_falls_as_age_rises()
        {
            Curve.Should().HaveCount(AgeCurve.Ceiling - Floor + 1);
            Curve.Zip(Curve.Skip(1), (younger, older) => older >= younger).Should().OnlyContain(rises => rises);
            Curve[0].Should().BeGreaterThan(0, "the youngest age is read off the people nearest it");
        }

        [Fact]
        public void The_age_the_life_came_to_changes_nothing()
        {
            AgeCurve.Share(0.74, 31, 31, BestAt, Floor, AgeCurve.Ceiling).Should().Be(0.74);
        }

        [Fact]
        public void A_younger_life_carries_what_the_world_carries_at_that_age()
        {
            double share = AgeCurve.Share(0.74, 31, 21, BestAt, Floor, AgeCurve.Ceiling);

            share.Should().BeApproximately(0.74 * BestAt(21) / BestAt(31), 1e-9);
            share.Should().BeLessThan(0.74);
        }

        [Fact]
        public void An_older_life_closes_toward_a_whole_life_in_step_with_the_years_added()
        {
            AgeCurve.Share(0.74, 31, 41, BestAt, Floor, AgeCurve.Ceiling)
                .Should().BeApproximately(0.74 + (1 - 0.74) * 10 / 19.0, 1e-9);
            AgeCurve.Share(0.74, 31, AgeCurve.Ceiling, BestAt, Floor, AgeCurve.Ceiling)
                .Should().Be(1.0, "the ceiling is a whole life, the most capable adult alive");
        }

        [Fact]
        public void The_bounds_hold()
        {
            AgeCurve.Share(0.74, 31, 5, BestAt, Floor, AgeCurve.Ceiling)
                .Should().Be(AgeCurve.Share(0.74, 31, Floor, BestAt, Floor, AgeCurve.Ceiling));
            AgeCurve.Share(0.74, 31, 90, BestAt, Floor, AgeCurve.Ceiling).Should().Be(1.0);
        }

        [Fact]
        public void Every_step_younger_carries_no_more_and_every_step_older_no_less()
        {
            var shares = Enumerable.Range(Floor, AgeCurve.Ceiling - Floor + 1)
                .Select(age => AgeCurve.Share(0.74, 31, age, BestAt, Floor, AgeCurve.Ceiling))
                .ToList();

            shares.Zip(shares.Skip(1), (younger, older) => older >= younger).Should().OnlyContain(rises => rises);
        }
    }
}
