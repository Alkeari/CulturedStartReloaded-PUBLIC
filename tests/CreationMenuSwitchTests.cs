using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Whether the set of chapters a player can switch off is actually closed.
    ///
    ///     The flow table decides which menus are hideable and the settings class
    ///     decides what each hideable id answers. Nothing joins the two: an id
    ///     declared hideable with no arm in ShowsMenu falls through to the default
    ///     and answers true forever, so the Creation Menus section silently omits a
    ///     switch for a chapter that says it has one. That is exactly what happened
    ///     to the sea chapter, and nothing failed when it did: the chapter rendered,
    ///     the flow worked, and the one thing missing was a row in a settings screen
    ///     nobody can diff against a table in another file.
    ///
    ///     Neither file compiles here, both naming engine types, so the two tables
    ///     are read as text out of the files that declare them (the shape ModSource
    ///     established). The check is one direction only: an arm naming an id the
    ///     flow no longer carries is dead weight rather than a broken promise, and
    ///     seven of those are standing today from the chapters the scenes replaced.
    /// </summary>
    public class CreationMenuSwitchTests
    {
        [Fact]
        public void Every_hideable_menu_has_a_switch_of_its_own()
        {
            var hideable = HideableFlowIds();
            var answered = SwitchedIds();

            hideable.Should().HaveCountGreaterThan(20,
                "the flow table should still be carrying its chapters and scenes");

            hideable.Where(id => !answered.Contains(id)).Should().BeEmpty(
                "a hideable menu with no arm in CSSettings.ShowsMenu answers true forever, " +
                "so MCM shows no switch for it while the flow claims one exists");
        }

        /// <summary>
        ///     Every id in the flow table not marked <c>hideable: false</c>. Entries
        ///     run over several lines, so each one is taken as the text from its own
        ///     <c>new(</c> up to the next.
        /// </summary>
        private static IReadOnlyList<string> HideableFlowIds()
        {
            string table = Between(Read("CharacterCreation", "Flow", "CreationFlow.cs"),
                "private static readonly Node[] Order", "\n        };");

            var ids = new List<string>();
            foreach (string entry in Regex.Split(table, @"\n(?=\s*new\()"))
            {
                var named = Regex.Match(entry, @"new\(""([a-z][a-z0-9_]*)""");
                if (!named.Success) continue;
                if (Regex.IsMatch(entry, @"hideable:\s*false")) continue;
                ids.Add(named.Groups[1].Value);
            }

            return ids;
        }

        /// <summary>Every menu id named by an arm of the ShowsMenu switch.</summary>
        private static HashSet<string> SwitchedIds()
        {
            string source = Read("Settings", "CSSettings.cs");
            int at = source.IndexOf("public static bool ShowsMenu", System.StringComparison.Ordinal);
            at.Should().BeGreaterThan(0, "CSSettings should still declare ShowsMenu");

            int open = source.IndexOf('{', at);
            int depth = 0;
            int close = open;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                {
                    close = i;
                    break;
                }
            }

            return Regex.Matches(source.Substring(open, close - open + 1), @"""([a-z][a-z0-9_]*)""")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToHashSet();
        }

        private static string Between(string source, string from, string to)
        {
            int start = source.IndexOf(from, System.StringComparison.Ordinal);
            start.Should().BeGreaterThan(0, $"the source should still hold {from}");
            int end = source.IndexOf(to, start, System.StringComparison.Ordinal);
            end.Should().BeGreaterThan(start, $"{from} should still be closed by {to}");
            return source.Substring(start, end - start);
        }

        private static string Read(params string[] parts) => File.ReadAllText(ModSource.Path(parts));
    }
}
