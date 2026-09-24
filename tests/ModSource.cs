using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The mod's own source, read as text.
    ///
    ///     Most of what turns a promise into a character lives in files this
    ///     project cannot compile: they name engine types, which keeps them
    ///     out. What those files hold, though, is mostly a table of the ids the
    ///     guided route is allowed to promise, written out by hand, and an id the
    ///     catalog grants that no table knows is a promise the player is made and
    ///     never kept. Nothing throws when it happens; a line is logged and the
    ///     thing quietly does not arrive.
    ///
    ///     So the tables are read out of the files that declare them rather than
    ///     copied here. A copy would be a second declaration of the same closed
    ///     set and would drift from the first, which is the defect being guarded
    ///     against. Reading the real file means a rename breaks the test loudly
    ///     instead of leaving it asserting about a table nobody uses any more,
    ///     which is why every reader below throws when it finds nothing.
    /// </summary>
    internal static class ModSource
    {
        /// <summary>An id in the guided route's vocabulary: lower case, words joined by underscores.</summary>
        private static readonly Regex Id = new(@"""([a-z][a-z0-9]*(?:_[a-z0-9]+)*)""", RegexOptions.Compiled);

        /// <summary>One arm of a table that maps an id to a localized sentence.</summary>
        private static readonly Regex Sentence = new(
            @"""(?<id>[a-z][a-z0-9_]*)""\s*=>\s*""\{=(?<key>[A-Za-z0-9_]+)\}(?<english>[^""]*)""",
            RegexOptions.Compiled);

        public static string Path(params string[] parts) =>
            System.IO.Path.Combine(Root(), System.IO.Path.Combine(parts));

        /// <summary>
        ///     Every id named inside one member's body, brace-matched from its
        ///     declaration so a neighbor's table is never read as this one's.
        /// </summary>
        public static IReadOnlyCollection<string> IdsIn(string file, string member)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Id.Matches(Body(file, member)))
                ids.Add(match.Groups[1].Value);

            if (ids.Count == 0)
                throw new InvalidOperationException($"{member} in {file} names no ids; has it been rewritten?");

            return ids;
        }

        /// <summary>The same, over a whole file, for a table that is a field rather than a member body.</summary>
        public static IReadOnlyCollection<string> IdsMatching(string file, string pattern)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(Read(file), pattern))
                ids.Add(match.Groups[1].Value);

            if (ids.Count == 0)
                throw new InvalidOperationException($"nothing in {file} matches {pattern}; has it been rewritten?");

            return ids;
        }

        /// <summary>An id-to-sentence table, as the id, its localization key and its English.</summary>
        public static IReadOnlyDictionary<string, string> SentencesIn(string file, string member)
        {
            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Sentence.Matches(Body(file, member)))
                lines[match.Groups["id"].Value] =
                    "{=" + match.Groups["key"].Value + "}" + match.Groups["english"].Value;

            if (lines.Count == 0)
                throw new InvalidOperationException($"{member} in {file} holds no sentences; has it been rewritten?");

            return lines;
        }

        /// <summary>
        ///     One member's body, verbatim, for a rule that is written as code
        ///     rather than as a table: a switch whose arms ARE the rule, where the
        ///     ids alone say nothing about what each one does.
        /// </summary>
        public static string MemberBody(string file, string member) => Body(file, member);

        /// <summary>
        ///     One member's body. Found by its declaration and closed by counting
        ///     braces, because a member is the smallest unit whose text is
        ///     meaningful: ids from the file at large would mix five tables into
        ///     one.
        /// </summary>
        private static string Body(string file, string member)
        {
            string text = Read(file);
            int at = text.IndexOf(member, StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException($"{file} has no {member}; has it been renamed?");

            int open = text.IndexOf('{', at);
            if (open < 0) throw new InvalidOperationException($"{member} in {file} has no body");

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return text.Substring(open, i - open + 1);
            }

            throw new InvalidOperationException($"{member} in {file} is never closed");
        }

        private static string Read(string file) => File.ReadAllText(Path(file.Split('/')));

        private static string Root([CallerFilePath] string here = "") =>
            System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(here)!)!;
    }
}
