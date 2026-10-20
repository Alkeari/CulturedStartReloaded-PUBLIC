using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Cultured Start Revamped's age chapter: six steps in the order they are
    ///     asked for, each one applied on every click, and a step that would cross a
    ///     bound withheld rather than cut short. Read out of the file, which names
    ///     engine types and so cannot be compiled here.
    /// </summary>
    public class AgeAdjustMenuTests
    {
        private const string Menu = "CharacterCreation/Menus/AgeAdjustMenu.cs";

        [Fact]
        public void The_six_steps_alternate_younger_and_older_by_one_five_and_ten_years()
        {
            var steps = Regex.Matches(ModSource.MemberBody(Menu, "Steps ="),
                    @"\(""(?<id>cs_age_adjust_[a-z0-9_]+)"", (?<years>-?\d+), ""\{=\w+\}(?<title>[^""]+)""\)")
                .Cast<Match>()
                .Select(match => (match.Groups["years"].Value, match.Groups["title"].Value))
                .ToList();

            steps.Should().Equal(
                ("-1", "Become a Year Younger"), ("1", "Grow a Year Older"),
                ("-5", "Become 5 Years Younger"), ("5", "Grow 5 Years Older"),
                ("-10", "Become 10 Years Younger"), ("10", "Grow 10 Years Older"));
        }

        [Fact]
        public void A_step_that_would_cross_a_bound_is_withheld_rather_than_cut_short()
        {
            string offered = ModSource.MemberBody(Menu, "private static bool Offered");

            offered.Should().Contain("return to >= Floor() && to <= AgeCurve.Ceiling;");
            offered.Should().NotContain("Math.", "nothing is clamped: a step either lands whole or is not offered");
        }

        [Fact]
        public void The_stage_replaying_its_recorded_option_on_entry_is_not_a_click()
        {
            ModSource.MemberBody(Menu, "private static void Take").Should().MatchRegex(@"^\{\s*if \(_entering\) return;",
                "a select handler that runs during the stage's own entry changes nothing");
            ModSource.MemberBody(Menu, "private static bool Offered").Should().Contain("_entering = true;");
            ModSource.MemberBody(Menu, "internal static bool Settles").Should().Contain("_entering = false;");
            ModSource.MemberBody("Patches/AgeAdjustStagePatch.cs", "public static void Postfix")
                .Should().Contain("AgeAdjustMenu.Settles(");
        }

        [Fact]
        public void The_chapter_says_the_age_the_character_is_now()
        {
            ModSource.MemberBody(Menu, "public static void AddAgeAdjustMenu")
                .Should().Contain("{=CSR_AgeAdjust_Now}Your age is currently: {AGE}");
            ModSource.MemberBody(Menu, "private static void Take").Should().Contain("RefreshDescription();");
        }
    }
}
