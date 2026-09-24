using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CulturedStartReloaded.Models;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     What can honestly be asked of the effect panel from a project that
    ///     cannot link the engine.
    ///
    ///     This file used to answer that question by carrying a private Line()
    ///     that reimplemented the service's branching, and then asserting against
    ///     that. Every test passed whatever ChoiceEffects did, which is how four
    ///     panel defects reached a playtest under a green suite: a debt said to
    ///     "be asked for" that is taken immediately, an ally said to join a party
    ///     that nothing joins, a raw "coin_pouch" printed at the player, and a
    ///     count dropped so two of a thing read as one. A test that asserts
    ///     against a copy of the code cannot see any of those, so the copy and
    ///     everything asserted against it are gone.
    ///
    ///     Nothing of ChoiceEffects is executed here, and nothing can be. Every
    ///     method that produces a line goes through TextObject, and the ones that
    ///     name a grant walk the item database through GearQuery, ArmorQuery and
    ///     ConsequenceStep, none of which this project can reference. The
    ///     pure part is the naming, and it now lives in LifeVocabulary inside
    ///     Services/HeroLore.cs, which names Hero, Settlement and TextObject and
    ///     so cannot be compiled in by path either. Executing the panel needs the
    ///     scratchpad harness that links the real assemblies.
    ///
    ///     What is left is what the mod's own source declares, read out of the
    ///     real file the way ModSource reads every other table this project
    ///     cannot compile. That is weaker than running the code and it is stated
    ///     as such on each test, but it is about the real declaration rather than
    ///     a second one written here.
    ///
    ///     Two halves of the panel are already covered elsewhere and are not
    ///     repeated: GrantVocabularyTests asks whether everything an answer can
    ///     promise has English and whether the pipeline can hand it over, and
    ///     EndStateTests walks whole lives and asks whether what the panel stated
    ///     reaches the character as often as it was stated.
    /// </summary>
    public class ChoiceEffectsTests
    {
        private const string ChoiceEffects = "Services/ChoiceEffects.cs";

        private static readonly string[] Tables =
            { "ItemText", "PlaceText", "AllyText", "AllyOpening", "TitleText", "WhoText", "HouseText" };

        [Fact]
        public void Every_kind_of_consequence_a_life_can_leave_is_one_the_panel_names()
        {
            // ChoiceEffects.Describe ends in "default: return null", so a kind it
            // has no arm for produces no line at all and the player reads a panel
            // one promise short of what the answer actually does. Naming a kind is
            // not the same as describing it well, which this cannot see; what it
            // can see is a kind added to the enum and never wired to the panel,
            // which is the failure that leaves the promise silently unstated
            var named = ModSource.IdsMatching(ChoiceEffects, @"ConsequenceKind\.(\w+)");

            foreach (ConsequenceKind kind in Enum.GetValues(typeof(ConsequenceKind)))
                named.Should().Contain(kind.ToString(),
                    $"{kind} is a consequence an answer can leave and the panel never mentions it");
        }

        [Theory]
        [InlineData("ItemText")]
        [InlineData("AllyOpening")]
        [InlineData("TitleText")]
        [InlineData("WhoText")]
        [InlineData("HouseText")]
        public void The_panel_names_a_thing_out_of_the_one_vocabulary(string table)
        {
            // The panel and the encyclopedia page describe one life, and each used
            // to hold its own copy of these five tables. They agreed only because
            // a test compared them; the build never had an opinion. There is one
            // table now, so what is worth guarding is that the panel still reads
            // it rather than quietly growing a second answer of its own
            Source().Should().Contain($"LifeVocabulary.{table}",
                $"the panel has stopped naming things through LifeVocabulary.{table}");
        }

        [Fact]
        public void The_panel_keeps_no_vocabulary_of_its_own()
        {
            // The other direction, and the one that actually regressed: a copy of
            // the table reintroduced beside the caller. Every entry of the shared
            // vocabulary carries a CSR_HeroLore_ key, so one appearing in this
            // file is a second declaration of English that already exists, whether
            // or not it agrees today
            var copied = Regex.Matches(Source(), @"""[a-z][a-z0-9_]*""\s*=>\s*""\{=CSR_HeroLore_(\w+)\}")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToList();

            copied.Should().BeEmpty(
                "the panel has grown a second copy of the shared vocabulary, which is the defect " +
                "that let one thing be named two ways with nothing in the build to say which was wrong");
        }

        [Fact]
        public void Every_table_the_panel_reads_is_one_the_vocabulary_declares()
        {
            // A name that is not there fails as a build error in the mod and never
            // reaches here, but this project compiles neither file, so a table
            // renamed on one side and not the other would leave the two tests
            // above asserting about a vocabulary nobody has. Reading both files
            // is what makes them mean anything
            string lore = File.ReadAllText(ModSource.Path("Services", "HeroLore.cs"));

            foreach (string table in Tables)
                lore.Should().Contain($"public static string? {table}(string id)",
                    $"LifeVocabulary no longer declares {table}, so the panel is calling nothing");
        }

        [Fact]
        public void The_panel_states_no_percentage_and_no_nearness()
        {
            // The defect this replaced: every option printed the eight beginnings
            // ranked with a figure beside each of them. A ranking is a reading of
            // the life and not a change to the character, and the figure made it
            // worse by looking exact
            string source = Source();

            foreach (string banned in new[]
                         { "Standings", "Nearness", "Resemblance", "Nearest", "%" })
                source.Should().NotContain(banned,
                    $"the panel has grown a reading of the life again ({banned}), which is what " +
                    "it was rewritten to stop stating");

            // One question may be put to the reading, and the answer to it is not a
            // reading of the life: which beginnings this answer has taken off the
            // table, which the panel is required to state. Everything else
            // Portrait can say is a ranking or a figure and stays out
            Regex.Matches(source, @"Portrait\.(\w+)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .Should().BeEquivalentTo(new[] { "ClosedBy" },
                    "the panel asks the reading one question, and a second one would be the " +
                    "ranking coming back under another name");
        }

        [Fact]
        public void The_panel_promises_nothing_about_where_the_campaign_opens()
        {
            // A place reaches LocationStep only through a village-or-not test over
            // the whole list of places, and every start type is later asked where
            // it begins by a chapter whose answer that step prefers. So a beginning
            // stated against a scene option is a forecast, which is ruled
            // out, and the chapter that settles it is what states it
            string source = Source();

            source.Should().NotContain("BeginsInAVillage",
                "the panel is reading the beginning rule again, which it can only ever " +
                "answer as a forecast");

            foreach (string label in new[] { "}Begins: ", "}Place: " })
                source.Should().NotContain(label,
                    "the panel has grown an entry about where the campaign opens, which no " +
                    "answer to a scene decides");
        }

        /// <summary>
        ///     The options that only navigate take no panel at all, and the set of
        ///     them is named in one place.
        ///
        ///     A route pick, the editor door and the confirm button change nothing
        ///     about the character, and this panel is a list of changes, so an
        ///     empty block is what they are owed. What they were getting instead
        ///     was an empty block AND a warning apiece, four of them on the mode
        ///     menu that every player of the Stable download walks through, which
        ///     is exactly what teaches a reader that these warnings are noise. The
        ///     set is asserted in both directions: a sixth navigation option has to
        ///     be added here deliberately, and an option that stops navigating has
        ///     to leave, or it would keep its silence after growing an effect.
        /// </summary>
        [Fact]
        public void The_options_that_only_navigate_are_named_once_and_take_no_panel()
        {
            var navigation = ModSource.IdsIn(ChoiceEffects, "OnlyNavigates");

            navigation.Should().BeEquivalentTo(new[]
            {
                "cs_mode_vanilla",
                "cs_mode_narrative",
                "cs_mode_custom",
                "cs_custom_open_editor",
                "cs_epilogue_begin"
            }, "these five are the whole set of options that only navigate");

            ModSource.MemberBody(ChoiceEffects, "public static string BlockFor")
                .Should().Contain("OnlyNavigates.Contains",
                    "the panel has stopped consulting the set, so all five would compose and warn again");
        }

        private static string Source() => File.ReadAllText(ModSource.Path("Services", "ChoiceEffects.cs"));
    }
}
