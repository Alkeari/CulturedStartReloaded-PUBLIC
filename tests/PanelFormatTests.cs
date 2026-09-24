using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The effect panel is a list, and the prose beside it is not.
    ///
    ///     Both halves of that had drifted in the same direction and for the same
    ///     reason: a fact is easiest to add where the sentence already is. The
    ///     panel grew sentences that explained ("2 years gone, which starts you
    ///     that much older"), and the descriptions grew figures the panel exists to
    ///     carry ("Now: Jogurys Castle. Choose this again for another."). A reader
    ///     who wanted the numbers had to read the story to find them and then read
    ///     a story in the panel to confirm them.
    ///
    ///     So two rules, and both are read out of the real files the way
    ///     <see cref="ModSource"/> reads every other table this project cannot
    ///     compile: every one of these files names engine types, which this project
    ///     keeps out. A panel entry is "Label: value" and never a sentence; an
    ///     option's description quotes no live value at all.
    ///
    ///     Every file that writes a panel entry is read, not the two the first
    ///     pass converted. A closed label set enforced over half the panel is not
    ///     a closed set: the fourteen chapters wrote the other half, and the whole
    ///     point of few labels reused is that a reader who learns where Troops
    ///     sits finds it in the same place on every screen.
    ///
    ///     WHAT THIS CANNOT SEE. Whether a value is terse, whether it is the right
    ///     value, and whether the label is the one a reader would look under. Those
    ///     are editorial and no assertion reaches them. What it can see is the
    ///     shape that regressed, in both directions, which is enough to stop it
    ///     regressing quietly.
    /// </summary>
    public class PanelFormatTests
    {
        /// <summary>
        ///     Every file that writes a panel entry. Named rather than globbed, so
        ///     a new chapter has to be added here deliberately and a file that
        ///     stops writing entries fails loudly instead of dropping out.
        /// </summary>
        public static readonly string[] PanelFiles =
        {
            "Services/ChoiceEffects.cs",
            "CharacterCreation/Menus/AgeAdjustMenu.cs",
            "CharacterCreation/Menus/ArmsMenu.cs",
            "CharacterCreation/Menus/CompanionSelectMenu.cs",
            "CharacterCreation/Menus/ContextualMenus.cs",
            "CharacterCreation/Menus/FleetMenu.cs",
            "CharacterCreation/Menus/FoundingMenu.cs",
            "CharacterCreation/Menus/GearCustomizationMenu.cs",
            "CharacterCreation/Menus/HouseholdMenu.cs",
            "CharacterCreation/Menus/MeansMenu.cs",
            "CharacterCreation/Menus/NameMenu.cs",
            "CharacterCreation/Menus/ProvisionsMenu.cs",
            "CharacterCreation/Menus/ScenarioChapterMenus.cs",
            "CharacterCreation/Menus/StandingMenu.cs",
            "CharacterCreation/Menus/StartLocationMenu.cs",
            "CharacterCreation/Menus/StoryProgressMenu.cs",
            "CharacterCreation/Menus/WarbandMenu.cs"
        };

        public static IEnumerable<object[]> EveryPanelFile =>
            PanelFiles.Select(file => new object[] { file });

        /// <summary>
        ///     A localized literal, as its key and its English. Every one of these
        ///     files writes them the same way and nothing else in any of them looks
        ///     like this.
        /// </summary>
        private static readonly Regex Literal = new(
            @"""\{=(?<key>CSR_[A-Za-z0-9_]+)\}(?<english>[^""]*)""", RegexOptions.Compiled);

        /// <summary>
        ///     The labels the panel is allowed to open an entry with.
        ///
        ///     Few and reused rather than many and exact, because a reader who
        ///     learns where Relation sits reads every later panel faster, and a
        ///     label used once teaches nothing. Both directions are asserted, so
        ///     the list cleans itself: a label that stops being used has to leave,
        ///     and a new one has to be added here deliberately rather than invented
        ///     at a call site.
        /// </summary>
        private static readonly string[] Labels =
        {
            "Age", "Allies", "Ally", "Asks", "Begins", "Choose again", "Clan", "Closes", "Crime",
            "Family", "Gear", "Gold", "Hall", "Halls next", "Held by", "Influence", "Instead",
            "Item", "Laws", "Level", "Name", "People", "Quests", "Realm", "Relation",
            "Ships", "Title", "Trait", "Troops", "Type", "Wars", "Why", "Workshop"
        };

        private static readonly Regex Labeled = new(@"^(?<label>[A-Z][A-Za-z ]{0,14}): \S",
            RegexOptions.Compiled);

        /// <summary>
        ///     The one variable a description may still carry: a whole authored
        ///     sentence, chosen by the life, that is fiction rather than data. Four
        ///     chapters offer an option that asks nothing and says in prose why the
        ///     life answers it the way it does.
        /// </summary>
        private const string Narrative = "{REASON}";

        /// <summary>Every panel string one file declares, by key.</summary>
        private static IReadOnlyDictionary<string, string> Panel(string file)
        {
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Literal.Matches(Read(file)))
            {
                string key = match.Groups["key"].Value;
                if (key.StartsWith("CSR_Panel_", StringComparison.Ordinal) ||
                    key.StartsWith("CSR_Effect_", StringComparison.Ordinal))
                    found[key] = match.Groups["english"].Value;
            }

            found.Should().NotBeEmpty($"{file} declares no panel strings; has the prefix changed?");
            return found;
        }

        private static IReadOnlyDictionary<string, string> WholePanel()
        {
            var all = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in PanelFiles)
            foreach (var line in Panel(file))
                all[line.Key] = line.Value;

            return all;
        }

        [Theory]
        [MemberData(nameof(EveryPanelFile))]
        public void No_panel_string_is_written_as_a_sentence(string file)
        {
            // A full stop is the one mark that says this was meant to be read as
            // prose. Several entries carry a comma, because a value can be
            // qualified without becoming a sentence, and several are fragments a
            // caller sets into an entry of its own; none of them ends in a stop
            Panel(file).Where(line => line.Value.TrimEnd().EndsWith(".", StringComparison.Ordinal))
                .Select(line => line.Key + ": " + line.Value)
                .Should().BeEmpty("the panel lists what a choice does and never narrates it");
        }

        [Fact]
        public void Every_label_the_panel_opens_with_is_one_of_the_few()
        {
            Used().Except(Labels, StringComparer.Ordinal)
                .Should().BeEmpty("a panel entry opens with a label nothing else uses, so a reader has " +
                                  "to learn where to look for it and will not find it twice");
        }

        [Fact]
        public void Every_label_this_allows_is_one_the_panel_still_uses()
        {
            // The other direction, and the one that rots. A label left here after
            // its entry is gone is cover for the next person to reintroduce it
            Labels.Except(Used(), StringComparer.Ordinal)
                .Should().BeEmpty("a label is allowed here that no entry opens with any more");
        }

        private static HashSet<string> Used()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in WholePanel())
            {
                var match = Labeled.Match(line.Value);
                if (match.Success) used.Add(match.Groups["label"].Value);
            }

            return used;
        }

        [Theory]
        [MemberData(nameof(EveryPanelFile))]
        public void No_option_description_quotes_a_live_value(string file)
        {
            // The mirror of the rule above, and the reason the panel exists. A
            // description that ends in "Now: Jogurys Castle. Choose this again for
            // another." is the hard data rendered in the one place that was
            // supposed to be the story. A live value reaches a description exactly
            // one way, through a {VARIABLE} the render fills in, so a description
            // whose only variable is the authored sentence cannot carry one
            var quoting = new List<string>();
            foreach (Match match in Literal.Matches(Read(file)))
            {
                string key = match.Groups["key"].Value;
                if (!key.EndsWith("_Desc", StringComparison.Ordinal) &&
                    !key.EndsWith("_Line", StringComparison.Ordinal) &&
                    !key.EndsWith("_Prose", StringComparison.Ordinal))
                    continue;

                string english = match.Groups["english"].Value.Replace(Narrative, string.Empty);
                if (english.IndexOf('{') >= 0) quoting.Add(key);
            }

            quoting.Should().BeEmpty("an option's description is the fiction and carries no figure, " +
                                     "no name and no count; those belong in the panel beside it");
        }

        private static string Read(string file) => File.ReadAllText(ModSource.Path(file.Split('/')));
    }
}
