using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Whether the modded-item setting reaches everything that hands the player
    ///     an item.
    ///
    ///     The setting promises that items from other mods stay out of the pickers
    ///     and out of what the start grants, and the only thing that keeps that
    ///     promise is OfficialItemRegistry.FilterAllowed. A query that asks the
    ///     object manager for items and never calls it fails silently in the worst
    ///     way available: the player gets a mod item they asked not to be offered,
    ///     no log line is written, and nothing about the result looks wrong. Two
    ///     queries were in that state, one loading a caravan and one crowning a
    ///     monarch.
    ///
    ///     None of these files compile here, all of them naming engine types, so
    ///     the call is read out of the source as text. The unit is the member: a
    ///     query that reads the item list and filters ten lines later is doing the
    ///     right thing, and only a member holding the one without the other is a
    ///     defect.
    /// </summary>
    public class ModdedItemFilterTests
    {
        private const string ItemList = "GetObjectTypeList<ItemObject>";
        private const string Filter = "OfficialItemRegistry.FilterAllowed";

        public static IEnumerable<object[]> EveryQueryingFile() => new[]
        {
            new object[] { "Services/GearQuery.cs" },
            new object[] { "Services/ArmorQuery.cs" },
            new object[] { "Services/EquipmentGenerator.cs" },
            new object[] { "Services/Application/Scenarios/CaravanMasterScenario.cs" }
        };

        [Theory]
        [MemberData(nameof(EveryQueryingFile))]
        public void Every_member_that_reads_the_item_list_filters_it(string file)
        {
            var unfiltered = Members(Read(file))
                .Where(body => body.Contains(ItemList) && !body.Contains(Filter))
                .ToList();

            unfiltered.Should().BeEmpty(
                $"{file} asks the object manager for items in a member that never calls " +
                "FilterAllowed, so the player's modded-item setting does not reach it");
        }

        /// <summary>
        ///     The crown is the one item query that reads equipment rosters rather
        ///     than the item list, so no rule over the item list can see it.
        /// </summary>
        [Fact]
        public void The_crown_is_filtered_too()
        {
            Read("Services/RegaliaQuery.cs").Should().Contain(Filter,
                "a ruler roster can name another mod's head piece and the setting has to reach it");
        }

        /// <summary>
        ///     The source cut at its member declarations, which at class scope are
        ///     the lines indented eight spaces and opening with a modifier. Cutting
        ///     rather than brace-matching is what survives a signature written over
        ///     two lines, and a cut that lands early only ever makes a slice
        ///     smaller, so a query can never borrow the next member's filter.
        /// </summary>
        private static IEnumerable<string> Members(string source)
        {
            var starts = Regex.Matches(source, @"\n        (?:public|private|internal|protected)\b")
                .Cast<Match>()
                .Select(match => match.Index)
                .ToList();

            starts.Should().NotBeEmpty("the file should still declare members at class scope");

            for (int i = 0; i < starts.Count; i++)
            {
                int end = i + 1 < starts.Count ? starts[i + 1] : source.Length;
                yield return source.Substring(starts[i], end - starts[i]);
            }
        }

        private static string Read(string file) => File.ReadAllText(ModSource.Path(file.Split('/')));
    }
}
