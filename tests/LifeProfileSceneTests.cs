using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The guided route sets none of the chapter fields the profile used to
    ///     read, so every later chapter that scales an amount now scales it off
    ///     these consequences. A wrong reading here is not a crash: it is a
    ///     character who answered like a soldier and is offered a merchant's
    ///     choices.
    ///
    ///     Ordering rather than exact scores: the weights are tuning and will
    ///     move, but a life that leaned one way must never read as leaning the
    ///     other.
    /// </summary>
    public class LifeProfileSceneTests
    {
        private static ChoiceConsequence C(ConsequenceKind kind, string? target = null, int amount = 0) =>
            new(kind, target, amount);

        [Fact]
        public void Nothing_answered_leans_nowhere()
        {
            var profile = LifeProfile.From(new List<ChoiceConsequence>());

            foreach (LifeProfile.Lean lean in System.Enum.GetValues(typeof(LifeProfile.Lean)))
                profile.Score(lean).Should().Be(0);
        }

        [Fact]
        public void A_life_under_arms_outleans_it_in_commerce()
        {
            var soldier = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Item, "mail_hauberk", 1),
                C(ConsequenceKind.Item, "spear", 1),
                C(ConsequenceKind.Trait, "Valor", 1)
            });

            soldier.Score(LifeProfile.Lean.Martial)
                .Should().BeGreaterThan(soldier.Score(LifeProfile.Lean.Commerce));
        }

        [Fact]
        public void A_life_of_ledgers_outleans_it_under_arms()
        {
            var trader = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Goodwill, "town_merchants", 1),
                C(ConsequenceKind.Item, "trade_goods", 1),
                C(ConsequenceKind.Title, "by_the_trade")
            });

            trader.Score(LifeProfile.Lean.Commerce)
                .Should().BeGreaterThan(trader.Score(LifeProfile.Lean.Martial));
        }

        [Fact]
        public void The_last_weapon_a_life_put_in_your_hands_is_the_one_you_reach_for()
        {
            var profile = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Item, "hunting_bow", 1),
                C(ConsequenceKind.Item, "two_handed_axe", 1)
            });

            profile.Weapon.Should().Be(LifeProfile.Trained.GreatWeapon);
        }

        [Fact]
        public void A_life_that_carried_no_weapon_trained_for_none()
        {
            var profile = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Goodwill, "village_headmen", 1),
                C(ConsequenceKind.Place, "home_village")
            });

            profile.Weapon.Should().Be(LifeProfile.Trained.None);
        }

        [Fact]
        public void People_who_would_come_when_called_are_counted_as_a_following()
        {
            var alone = LifeProfile.From(new[] { C(ConsequenceKind.Place, "open_country") });
            var followed = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Ally, "the_one_you_carried"),
                C(ConsequenceKind.Ally, "the_apprentice")
            });

            followed.Score(LifeProfile.Lean.Following)
                .Should().BeGreaterThan(alone.Score(LifeProfile.Lean.Following));
        }

        [Fact]
        public void Being_hated_by_lords_is_not_a_way_of_standing_among_them()
        {
            var hated = LifeProfile.From(new[]
            {
                C(ConsequenceKind.Enmity, "culture_lords", 3)
            });

            hated.Score(LifeProfile.Lean.Standing).Should().Be(0);
        }

        [Fact]
        public void A_name_the_law_gave_you_is_not_a_title()
        {
            var outlaw = LifeProfile.From(new[] { C(ConsequenceKind.Title, "wanted") });
            var sworn = LifeProfile.From(new[] { C(ConsequenceKind.Title, "sworn_of_the_house") });

            outlaw.Score(LifeProfile.Lean.Standing).Should().Be(0);
            sworn.Score(LifeProfile.Lean.Standing).Should().BeGreaterThan(0);
        }

        [Fact]
        public void An_unanswered_run_reads_nothing_from_the_scenes()
        {
            var answers = new SceneAnswers();

            SceneReading.Answered(answers).Should().Be(0);
            SceneReading.Consequences(new List<Scene>(), answers).Should().BeEmpty();
        }

        [Fact]
        public void What_was_answered_is_gathered_in_the_order_it_was_chosen()
        {
            var scene = new Scene("s1", "{=!}title", "{=!}prompt", new[]
            {
                new SceneOption("o1", "{=!}first", "{=!}prose",
                    new[] { C(ConsequenceKind.Trait, "Valor", 1) }),
                new SceneOption("o2", "{=!}second", "{=!}prose",
                    new[] { C(ConsequenceKind.Trait, "Mercy", 1) })
            }, age: null);

            var answers = new SceneAnswers();
            answers.Replace("s1", "o2");

            var gathered = SceneReading.Consequences(new[] { scene }, answers);

            gathered.Should().ContainSingle().Which.Target.Should().Be("Mercy");
        }
    }
}
