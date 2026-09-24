using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Whether the chapters that cannot be read out of a catalog still say what
    ///     their options do.
    ///
    ///     The panel promise is kept for a scene by the scene catalog, which carries
    ///     each option's consequences and is read straight out. A chapter carries an
    ///     Action instead, and the only thing that can state what that Action writes
    ///     is the chapter itself, through ChoiceEffects.Declare. Miss one and
    ///     nothing breaks: the option renders, the pick works, and the panel beside
    ///     the character is blank, which is the exact failure the rule exists to
    ///     stop. Five whole files were in that state.
    ///
    ///     None of this can be executed here. Every one of these files names
    ///     TaleWorlds types in its signatures, which keeps the lot out of this
    ///     project, and what is left is the declaration as the file writes it. The
    ///     shape asked for is the one the fix established: one place that registers
    ///     an option is one place that declares its effect, so the two are written
    ///     side by side and read the same values. A registration added without a
    ///     declaration beside it moves the count and fails here.
    /// </summary>
    public class PanelDeclarationTests
    {
        [Theory]
        [InlineData("CompanionSelectMenu.cs")]
        [InlineData("HouseholdMenu.cs")]
        [InlineData("StandingMenu.cs")]
        [InlineData("FoundingMenu.cs")]
        [InlineData("ContextualMenus.cs")]
        [InlineData("FleetMenu.cs")]
        [InlineData("StoryProgressMenu.cs")]
        [InlineData("AgeAdjustMenu.cs")]
        public void Every_place_a_chapter_registers_an_option_declares_what_it_does(string file)
        {
            string source = Source(file);

            int registered = Count(source, @"new NarrativeMenuOption\(");
            int declared = Count(source, @"ChoiceEffects\.Declare\(");

            registered.Should().BeGreaterThan(0,
                $"{file} is a chapter file and should still be registering options");
            declared.Should().Be(registered,
                $"{file} registers {registered} kinds of option and declares {declared} effects; " +
                "an option with no declaration draws a blank panel beside the character");
        }

        /// <summary>
        ///     A row the world cannot supply is not built.
        ///
        ///     Both chapters below registered an option whatever the world held and
        ///     left a render condition, or an empty panel, to cover for it. That is
        ///     how the panel came to be asked for a name slot the culture's pool
        ///     could not fill and for a wagon load in a world with no trade goods.
        ///     Neither is a life being contradicted, where an option must stay
        ///     on offer and behave differently; both are an answer with nothing
        ///     behind it. The fix in each is the same shape: the call that would
        ///     supply the row is the one that decides whether it is put.
        /// </summary>
        [Fact]
        public void A_name_row_is_built_for_each_name_the_pool_holds_and_no_others()
        {
            string source = Source("NameMenu.cs");

            source.Should().NotContain("OfferedNames",
                "a fixed count of rows is what built slots the culture's pool could not fill");
            source.Should().Contain("for (int index = 0; index < names.Count; index++)",
                "the rows come from the pool itself, so every one of them has a name behind it");
        }

        /// <inheritdoc cref="A_name_row_is_built_for_each_name_the_pool_holds_and_no_others"/>
        [Fact]
        public void A_cargo_is_offered_only_where_the_markets_carry_one()
        {
            Source("ScenarioChapterMenus.cs").Should()
                .Contain("() => CargoFor(valueBand, countPerItem).Count > 0",
                    "the ledger offers a load only where the same call that fills the wagons finds goods");
        }

        private static int Count(string source, string pattern) =>
            Regex.Matches(source, pattern).Count;

        private static string Source(string file) =>
            File.ReadAllText(ModSource.Path("CharacterCreation", "Menus", file));
    }
}
