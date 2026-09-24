using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     One brother or sister stands the same number of years from the player
    ///     wherever the run looks at them. A childhood scene stages that person
    ///     chapters before the household chapter composes them, so the two asks
    ///     have to answer the same: a sister four years younger at thirteen was a
    ///     year OLDER at thirty, because the stage assumed a distance and the
    ///     household drew one.
    /// </summary>
    public class SiblingOffsetTests
    {
        [Fact]
        public void The_same_sibling_of_the_same_run_is_always_the_same_years_away()
        {
            for (int seed = -200; seed < 200; seed++)
                for (int ordinal = 0; ordinal < 5; ordinal++)
                    SiblingOffsets.For(seed, ordinal)
                        .Should().Be(SiblingOffsets.For(seed, ordinal));
        }

        [Fact]
        public void The_scene_and_the_household_ask_the_first_sibling_the_same_question()
        {
            // What the two call sites do: the scene's stand-in asks for the first
            // sibling with nothing settled yet, and the household settles that
            // same first sibling before any other is taken
            for (int seed = -200; seed < 200; seed++)
            {
                int stagedBeforeTheHouseholdExists = SiblingOffsets.For(seed, 0);
                int settledWhenTheHouseholdComposes = SiblingOffsets.For(seed, 0, new HashSet<int>());

                settledWhenTheHouseholdComposes.Should().Be(stagedBeforeTheHouseholdExists);
            }
        }

        [Fact]
        public void The_first_sibling_is_always_the_younger_one()
        {
            // The childhood scenes stand that sibling behind a child of thirteen
            // and the script names a younger one, so a positive distance would put
            // an adult in a room written for a child
            for (int seed = -500; seed < 500; seed++)
                SiblingOffsets.For(seed, 0).Should().BeNegative();
        }

        [Fact]
        public void Nobody_is_further_off_than_one_household_reaches()
        {
            for (int seed = -500; seed < 500; seed++)
                for (int ordinal = 0; ordinal < 6; ordinal++)
                    SiblingOffsets.For(seed, ordinal)
                        .Should().BeInRange(-SiblingOffsets.MaxDistance, SiblingOffsets.MaxDistance);
        }

        [Fact]
        public void A_distance_already_taken_is_not_handed_out_twice()
        {
            // Five children of one house born in five different years, which is
            // what stops two of them rendering as the same person
            for (int seed = -100; seed < 100; seed++)
            {
                var taken = new HashSet<int>();
                for (int ordinal = 0; ordinal < 5; ordinal++)
                {
                    int offset = SiblingOffsets.For(seed, ordinal, taken);
                    taken.Should().NotContain(offset);
                    taken.Add(offset);
                }

                taken.Should().HaveCount(5);
            }
        }

        [Fact]
        public void Only_a_third_child_or_later_can_be_a_twin()
        {
            // Whether one of them was born with the player cannot depend on how
            // many the household ends up holding, or the sibling a scene stages
            // before the household exists would settle at a different distance
            for (int seed = -500; seed < 500; seed++)
            {
                SiblingOffsets.For(seed, 0).Should().NotBe(0);
                SiblingOffsets.For(seed, 1).Should().NotBe(0);
            }
        }

        [Fact]
        public void A_house_still_reaches_a_twin()
        {
            int twins = Enumerable.Range(0, 2000).Count(seed => SiblingOffsets.For(seed, 2) == 0);

            twins.Should().BeGreaterThan(0, "no house could ever hold a pair born together");
            twins.Should().BeLessThan(400, "a pair born together is meant to be the rare case");
        }

        [Fact]
        public void Two_runs_can_put_different_years_between_the_same_pair()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 500; seed++) seen.Add(SiblingOffsets.For(seed, 0));

            seen.Count.Should().BeGreaterThan(1, "every run would compose the same house");
        }
    }
}
