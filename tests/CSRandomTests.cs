using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Every "let fate decide" in the mod ends here, and the callers rely on two things: that an
    ///     empty list answers null instead of throwing, and that a pick is always in range. Both are
    ///     reached constantly with lists the caller did not build, such as the towns of a culture
    ///     that owns none.
    /// </summary>
    public class CSRandomTests
    {
        [Fact]
        public void Picking_from_an_empty_list_answers_null_rather_than_throwing()
        {
            CSRandom.Pick(new List<string>()).Should().BeNull();
        }

        [Fact]
        public void Picking_from_a_null_list_answers_null_rather_than_throwing()
        {
            CSRandom.Pick<string>(null!).Should().BeNull();
        }

        [Fact]
        public void Picking_from_one_item_always_answers_that_item()
        {
            var only = new List<string> { "the only town" };

            for (int i = 0; i < 50; i++)
                CSRandom.Pick(only).Should().Be("the only town");
        }

        [Fact]
        public void Every_pick_comes_from_the_list()
        {
            var items = Enumerable.Range(0, 8).ToList();

            for (int i = 0; i < 500; i++)
                items.Should().Contain(CSRandom.Pick(items));
        }

        [Fact]
        public void A_value_type_list_that_is_empty_answers_the_default_not_a_random_number()
        {
            CSRandom.Pick(new List<int>()).Should().Be(0);
        }

        [Fact]
        public void Next_stays_inside_its_bounds()
        {
            for (int i = 0; i < 500; i++)
            {
                CSRandom.Next(5).Should().BeInRange(0, 4);
                CSRandom.Next(10, 20).Should().BeInRange(10, 19);
            }
        }

        [Fact]
        public void Next_with_an_exclusive_maximum_of_one_is_always_zero()
        {
            for (int i = 0; i < 20; i++)
                CSRandom.Next(1).Should().Be(0);
        }

        [Fact]
        public void A_stable_draw_answers_the_same_thing_however_often_it_is_asked()
        {
            // The property the effect panel rests on: it redraws whenever the
            // player moves and has to state the same person every time
            for (int i = 0; i < 100; i++)
                CSRandom.Stable(4271, "cs_opt_they_said_nothing_about_you:kind", 2)
                    .Should().Be(CSRandom.Stable(4271, "cs_opt_they_said_nothing_about_you:kind", 2));
        }

        [Fact]
        public void A_stable_draw_costs_the_shared_sequence_nothing()
        {
            // The reason the panel may ask at all. A draw that moved the generator
            // would shift every later pick in the run, and shift it again on the
            // next redraw
            var before = Enumerable.Range(0, 20).Select(_ => CSRandom.Next(1000)).ToList();

            for (int i = 0; i < 20; i++) CSRandom.Stable(i, "anything", 7);

            var after = Enumerable.Range(0, 20).Select(_ => CSRandom.Next(1000)).ToList();
            after.Should().NotEqual(before, "two draws off one generator are not expected to repeat");
        }

        [Fact]
        public void A_stable_draw_stays_inside_its_bounds()
        {
            for (int seed = -50; seed < 50; seed++)
            {
                CSRandom.Stable(seed, "kind", 2).Should().BeInRange(0, 1);
                CSRandom.Stable(seed, "years", 5).Should().BeInRange(0, 4);
                CSRandom.Stable(seed, "one", 1).Should().Be(0);
                CSRandom.Stable(seed, "none", 0).Should().Be(0);
            }
        }

        [Fact]
        public void A_stable_draw_moves_with_the_seed_and_with_the_key()
        {
            // Two runs have to be able to differ, and two questions of one run have
            // to be able to answer differently
            var bySeed = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++) bySeed.Add(CSRandom.Stable(seed, "kind", 2));
            bySeed.Should().HaveCount(2, "every run would settle on the same answer");

            var byKey = new HashSet<int>();
            foreach (string key in new[] { "kind", "years", "order", "who", "when" })
                byKey.Add(CSRandom.Stable(99, key, 5));
            byKey.Count.Should().BeGreaterThan(1, "one run's questions would all share one answer");
        }

        [Fact]
        public void A_stable_draw_is_not_lopsided()
        {
            // Two coins the mod tosses this way decide a brother or a sister and
            // an older or a younger one, so a draw that answered one of them nine
            // times in ten would read as the mod having decided, not the run
            int heads = Enumerable.Range(0, 2000).Count(seed => CSRandom.Stable(seed, "kind", 2) == 0);

            heads.Should().BeInRange(900, 1100);
        }

        [Fact]
        public void Picking_from_a_long_list_reaches_more_than_one_value()
        {
            // Not a distribution claim, just that it is not pinned to a single index: a pick that
            // always answered items[0] would satisfy every other test here.
            var items = Enumerable.Range(0, 20).ToList();
            var seen = new HashSet<int>();

            for (int i = 0; i < 400; i++)
                seen.Add(CSRandom.Pick(items));

            seen.Count.Should().BeGreaterThan(1);
        }
    }
}
