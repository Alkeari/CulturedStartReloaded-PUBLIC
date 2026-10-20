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
    ///     The gate over the preset's coverage of the session.
    ///
    ///     A preset is written and read one field at a time, so a field added to
    ///     <c>CharacterCreationSession</c> and not added to <c>PresetData</c> is
    ///     dropped in silence: the build is green, the tests are green, and the
    ///     player finds out when a saved setup loads back as a character they did
    ///     not compose. It blinds the editor's unsaved-changes prompt
    ///     in the same stroke, because Close diffs the serialized shape and a field
    ///     outside that shape cannot show as a change. Six fields were found
    ///     dropped this way in one evening, every one of them the same shape.
    ///
    ///     So the session's own state is enumerated here and every piece of it must
    ///     be either carried by a preset or named in <see cref="NotCarried"/> with
    ///     the reason it is not. Both files name engine types and cannot be
    ///     compiled into this project, which rules out reflecting over
    ///     the real type: loading the built module would need every TaleWorlds
    ///     assembly resolvable just to read a property's type. They are read as
    ///     source text instead, the way <see cref="ModSource"/> already reads the
    ///     tables that declare the guided route's vocabulary. Reading the real file
    ///     is the point: a copy of the type here would be a second declaration of
    ///     the same closed set, which is the defect being guarded against.
    /// </summary>
    public class PresetCoverageTests
    {
        /// <summary>
        ///     State a preset deliberately does not carry, each with the one line
        ///     saying why. Nothing else may be missing, and nothing here may be
        ///     carried after all: a stale entry is how a real gap gets excused.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> NotCarried =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Mode"] =
                    "the route, chosen on cs_mode_menu or by the game's own starting options; a preset is a " +
                    "Start Editor artifact and loading one cannot move the player onto another route",
                ["RunSeed"] =
                    "what this run leaves to chance, drawn once when the session is made. Nothing composed it and " +
                    "nothing reads it back as an answer: it exists so a detail an answer leaves open is settled " +
                    "the moment the answer is given, which is what lets the effect panel state the person the " +
                    "player is getting instead of a coin the apply pipeline has yet to toss. Carrying it in a " +
                    "preset would move one run's tossed coin onto another run that never tossed it, and a preset " +
                    "is a Start Editor artifact besides, where no scene is answered and nothing asks",
                ["ProvisionsMaterialized"] =
                    "whether this editor session has already loaded the provisions plan into the food and " +
                    "mount lists; the lists themselves are what a preset carries",
                ["ResumeMenuId"] =
                    "where this launch's narrative stage was left, so a step back onto it resumes there; " +
                    "a place in one walk, not a choice anyone composed",
                ["RouteDecidedExternally"] =
                    "a fact about this launch, written by CreationSession.StartNew from the game's own " +
                    "Advanced Starting Options, not a choice anyone composed",
                ["SelectedAge"] =
                    "written by the guided route on every scene it answers and never taken back, which is why " +
                    "LifeProfile.From refuses to read it off the session; CustomAge is the age a preset settles",
                ["SelectedFamily"] = Chapter,
                ["SelectedChildhood"] = Chapter,
                ["SelectedEducation"] = Chapter,
                ["SelectedYouth"] = Chapter,
                ["SelectedTurning"] = Chapter,
                ["SelectedReason"] = Chapter,
                ["ChooseWeaponsIndividually"] =
                    "whether Cultured Start's arms chapter opens its four slot chapters; which menus a route " +
                    "walks, and the slots themselves are carried as WeaponChoices",
                ["AdjustedAge"] =
                    "the age a Revamped life was moved to on its own age chapter, read off the answers that " +
                    "chapter moves it from; the Start Editor's exact age is CustomAge, which a preset carries",
                ["StoryBeats"] =
                    "what the scenario chapters were told, recited by the epilogue; the story rather than a " +
                    "grant, and written only by menus the Start Editor never opens",
                ["SelectedCulture"] =
                    "recorded as CultureId and never put back: the culture is the game's own creation stage to " +
                    "decide, and restoring it would leave the mod describing one culture while the character is " +
                    "another. A load onto a different culture says so through AnnounceComposedCulture",
                ["ComposedRealmNames"] = Offer,
                ["ComposedFirstNames"] = Offer,
                ["ComposedClanNames"] = Offer,
                ["KnownFaces"] =
                    "live Hero objects the generators add while the start is applied, so there is nothing to " +
                    "save and nothing that would still resolve on a later campaign",
                ["PreviewEquipment"] =
                    "what the 3D preview is currently showing, composed by EquipmentPreviewService from the " +
                    "choices themselves; a rendering of the setup rather than part of it"
            };

        private const string Chapter =
            "an answer to one of Cultured Start's seven chapters, which the Start Editor never asks; what the " +
            "answer grants reaches the character through the game's replay of that chapter's own args, so a " +
            "preset has nothing to put back";

        private const string Offer =
            "names offered for the player to pick from, not an answer; the session clears all three whenever " +
            "the culture, the founding or the seat changes";

        /// <summary>
        ///     Saved fields that are deliberately written and never read. Kept
        ///     separate from <see cref="NotCarried"/> because this is the other
        ///     direction: a field in the file that nothing puts back.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> WriteOnly =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PresetData.Version"] =
                    "the stamp saying which shape wrote the file. Nothing branches on it today; every field " +
                    "added since is nullable or defaulted, so an older preset is read by its defaults instead"
            };

        /// <summary>
        ///     The members that compose the saved shape. Everything else in the
        ///     service reads it. Named rather than guessed so a rename breaks this
        ///     loudly instead of leaving the check quietly asserting nothing.
        /// </summary>
        private static readonly string[] Writers =
        {
            "PresetData BuildData", "HeroSpecDto? ToSpecDto", "List<ItemCountDto> ToItemCounts"
        };

        [Fact]
        public void Every_piece_of_session_state_is_carried_by_a_preset_or_named_here()
        {
            string build = Member("PresetData BuildData");
            string apply = Member("void ApplyData");

            var missing = new List<string>();
            foreach (var property in SessionState())
            {
                if (NotCarried.ContainsKey(property.Name)) continue;

                bool written = Regex.IsMatch(build, $@"session\.{property.Name}\b");
                bool read = property.HasSetter
                    ? Regex.IsMatch(apply, $@"session\.{property.Name}\s*=(?!=)")
                    : Regex.IsMatch(apply, $@"session\.{property.Name}\b");

                if (written && read) continue;

                missing.Add(
                    $"CharacterCreationSession.{property.Name} is " +
                    (written ? "written into the saved shape but never put back."
                             : read ? "put back on load but never saved." : "not carried at all.") +
                    Instructions(property));
            }

            missing.Should().BeEmpty(
                "a preset must answer for every piece of state the session holds:\n" +
                string.Join("\n", missing));
        }

        [Fact]
        public void Nothing_is_excused_that_the_session_no_longer_holds_or_the_preset_already_carries()
        {
            var state = SessionState().ToDictionary(p => p.Name, p => p);
            string build = Member("PresetData BuildData");
            string apply = Member("void ApplyData");

            var stale = new List<string>();
            foreach (var pair in NotCarried)
            {
                pair.Value.Should().NotBeNullOrWhiteSpace(
                    $"{pair.Key} is excused, so it carries the line saying why");

                if (!state.ContainsKey(pair.Key))
                {
                    stale.Add($"{pair.Key} is excused here and the session no longer holds it; delete the entry.");
                    continue;
                }

                // SelectedCulture is the one entry that is half carried on purpose:
                // recorded so a load can say what the setup was composed on, never
                // assigned back. Only a field that is both written and read is
                // genuinely covered and no longer needs excusing.
                bool written = Regex.IsMatch(build, $@"session\.{pair.Key}\b");
                bool read = Regex.IsMatch(apply, $@"session\.{pair.Key}\b");
                if (written && read)
                    stale.Add($"{pair.Key} is excused here but the preset now carries it; delete the entry.");
            }

            stale.Should().BeEmpty("an exclusion that stopped being true is how a real gap gets excused:\n" +
                                   string.Join("\n", stale));
        }

        /// <summary>
        ///     The recurring trap, in the shape it keeps taking:
        ///     <c>if (Enum.TryParse(data.X, out var parsed)) session.Y = parsed;</c>
        ///     leaves the loading session's own value standing when the file does
        ///     not hold the field, so an older preset loads with whatever the editor
        ///     happened to be carrying instead of the answer it was saved with.
        ///
        ///     A guarded write is safe only where the guard cannot fail on a file
        ///     that simply predates the field, which means the field must be
        ///     non-nullable with a literal default: absence then reads as that
        ///     default and the guard always passes. A nullable field read this way
        ///     is the leak itself.
        /// </summary>
        [Fact]
        public void A_field_read_behind_a_guard_can_never_be_absent_from_an_older_preset()
        {
            string apply = Member("void ApplyData");
            var saved = SavedFields().ToDictionary(f => f.Owner + "." + f.Name, f => f);

            var unconditional = new HashSet<string>(StringComparer.Ordinal);
            foreach (string statement in TopLevelStatements(apply))
            {
                var match = Regex.Match(statement, @"^\s*session\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)");
                if (match.Success) unconditional.Add(match.Groups["name"].Value);
            }

            var leaks = new List<string>();
            foreach (string statement in TopLevelStatements(apply))
            {
                var writes = Regex.Matches(statement, @"session\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)")
                    .Cast<Match>()
                    .Select(m => m.Groups["name"].Value)
                    .Where(name => !unconditional.Contains(name))
                    .Distinct()
                    .ToList();
                if (writes.Count == 0) continue;

                var fields = Regex.Matches(statement, @"data\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")
                    .Cast<Match>()
                    .Select(m => m.Groups["name"].Value)
                    .Distinct()
                    .ToList();

                foreach (string name in writes)
                foreach (string field in fields)
                {
                    if (!saved.TryGetValue("PresetData." + field, out var declared)) continue;
                    if (!declared.Nullable && declared.HasDefault) continue;

                    leaks.Add(
                        $"session.{name} is assigned only where PresetData.{field} reads, and that field " +
                        (declared.Nullable ? "is nullable" : "carries no default") +
                        ", so a preset written before it existed leaves the loading session's own answer " +
                        "standing. Assign session." + name + " unconditionally, with the value an older " +
                        "preset was composed with as the fallback.");
                }
            }

            leaks.Should().BeEmpty("a guarded read is how a dropped field hides:\n" + string.Join("\n", leaks));
        }

        [Fact]
        public void Every_saved_field_is_both_written_and_read_back()
        {
            string source = PresetSource();
            string writers = string.Join("\n", Writers.Select(Member));
            string readers = Writers.Aggregate(source, (text, writer) => text.Replace(Member(writer), ""));

            var half = new List<string>();
            foreach (var field in SavedFields())
            {
                string full = field.Owner + "." + field.Name;
                if (WriteOnly.ContainsKey(full)) continue;

                bool written = Regex.IsMatch(writers, $@"(\b{field.Name}\s*=(?!=))|(\.{field.Name}\b)");
                bool read = Regex.IsMatch(readers, $@"\.{field.Name}\b");

                if (!written) half.Add($"{full} is read back but nothing ever writes it.");
                if (!read) half.Add($"{full} is written into the file and nothing ever reads it.");
            }

            half.Should().BeEmpty("a field carried in one direction only is a saved setup that half survives:\n" +
                                  string.Join("\n", half));
        }

        [Fact]
        public void A_write_only_field_is_still_a_field_the_saved_shape_declares()
        {
            var declared = SavedFields().Select(f => f.Owner + "." + f.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var pair in WriteOnly)
            {
                declared.Should().Contain(pair.Key, "it is excused as write-only, so it should still exist");
                pair.Value.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        ///     The same gate one level down. A companion's or relative's own sheet
        ///     is a <c>HeroSpec</c>, and a relative's place in the family is a
        ///     <c>FamilyMemberSpec</c>; a field added to either and not to the saved
        ///     shape drops out of a preset exactly as silently as a session field.
        /// </summary>
        [Fact]
        public void Every_piece_of_a_generated_heros_own_state_is_carried_by_a_preset()
        {
            string toDto = Member("HeroSpecDto? ToSpecDto");
            string fromDto = Member("void ApplySpecDto");

            var missing = new List<string>();
            foreach (var property in StateOf(2, "public sealed class HeroSpec", "Models", "HeroSpec.cs"))
            {
                if (!Regex.IsMatch(toDto, $@"spec\.{property.Name}\b"))
                    missing.Add($"HeroSpec.{property.Name} is never written by ToSpecDto.");
                if (!Regex.IsMatch(fromDto, $@"spec\.{property.Name}\b"))
                    missing.Add($"HeroSpec.{property.Name} is never put back by ApplySpecDto.");
            }

            string memberFile = Strip(File.ReadAllText(ModSource.Path("Models", "FamilyMemberSpec.cs")));
            string constructor = Block(memberFile, "public FamilyMemberSpec(");
            string buildFamily = Block(Member("PresetData BuildData"), "foreach (var member in session.FamilyMembers)");
            string applyFamily = Block(Member("void ApplyData"), "foreach (var dto in data.Family)");

            foreach (var property in StateOf(2, "public sealed class FamilyMemberSpec", "Models", "FamilyMemberSpec.cs"))
            {
                if (!Regex.IsMatch(buildFamily, $@"member\.{property.Name}\b"))
                    missing.Add($"FamilyMemberSpec.{property.Name} is never written into a saved family member.");

                bool byConstructor = Regex.IsMatch(constructor, $@"\b{property.Name}\s*=\s*[a-z][A-Za-z0-9_]*\s*;") &&
                                     applyFamily.Contains("new FamilyMemberSpec(");
                bool read = byConstructor ||
                            Regex.IsMatch(applyFamily, $@"member\.{property.Name}\b") ||
                            Regex.IsMatch(applyFamily, $@"\b{property.Name}\s*=(?!=)");
                if (!read)
                    missing.Add($"FamilyMemberSpec.{property.Name} is never put back on a loaded family member.");
            }

            missing.Should().BeEmpty("a preset must answer for every hero the editor lets the player compose:\n" +
                                     string.Join("\n", missing));
        }

        /// <summary>
        ///     ToSpecDto writes nothing for a spec that says it is not customized,
        ///     so a field HasCustomization forgets is dropped from the file whenever
        ///     it is the only thing set, and a field Clear forgets survives a Reset
        ///     the player was told returned the character to fully generated.
        /// </summary>
        [Fact]
        public void A_hero_spec_counts_and_clears_every_field_it_holds()
        {
            string source = Strip(File.ReadAllText(ModSource.Path("Models", "HeroSpec.cs")));
            int at = source.IndexOf("public bool HasCustomization =>", StringComparison.Ordinal);
            at.Should().BeGreaterThan(-1, "HeroSpec should still say whether it is customized");
            string counted = source.Substring(at, source.IndexOf(';', at) - at);
            string cleared = Block(source, "public void Clear()");

            var gaps = new List<string>();
            foreach (var property in StateOf(2, "public sealed class HeroSpec", "Models", "HeroSpec.cs"))
            {
                if (!Regex.IsMatch(counted, $@"\b{property.Name}\b"))
                    gaps.Add($"HeroSpec.HasCustomization never looks at {property.Name}.");
                if (!Regex.IsMatch(cleared, $@"\b{property.Name}\b"))
                    gaps.Add($"HeroSpec.Clear never resets {property.Name}.");
            }

            gaps.Should().BeEmpty(string.Join("\n", gaps));
        }

        /// <summary>
        ///     A field carried by a preset and never read by the outfitter is a
        ///     saved setup that loads back and does nothing to the hero. The
        ///     outfitter is the one door companions, relatives and vassal lords are
        ///     built through, so it must read every field the spec holds.
        /// </summary>
        [Fact]
        public void Every_field_a_hero_spec_holds_reaches_the_hero()
        {
            string outfit = Block(Strip(File.ReadAllText(ModSource.Path("Services", "HeroOutfitter.cs"))),
                "public static void Outfit(");

            var unread = StateOf(2, "public sealed class HeroSpec", "Models", "HeroSpec.cs")
                .Where(p => !Regex.IsMatch(outfit, $@"spec\??\.{p.Name}\b"))
                .Select(p => $"HeroOutfitter.Outfit never reads HeroSpec.{p.Name}.")
                .ToList();

            unread.Should().BeEmpty(string.Join("\n", unread));
        }

        private static string Instructions(SessionProperty property) =>
            "\n  Carry it:" +
            "\n    1. add a field for it to PresetData, nullable or with a literal default, so a preset written " +
            "before it existed still loads;" +
            $"\n    2. write it in BuildData from session.{property.Name};" +
            $"\n    3. assign session.{property.Name} UNCONDITIONALLY in ApplyData, stating the value an older " +
            "preset was composed with as the fallback. 'if (Enum.TryParse(...)) session.X = parsed;' is the trap: " +
            "it leaves the loading session's own answer standing;" +
            "\n    4. bump PresetData.Version;" +
            "\n    5. name it in PresetFidelityTests, which holds each carried field by name." +
            $"\n  Or, if a preset must not carry it, add \"{property.Name}\" to NotCarried in this file with the " +
            "one line saying why.";

        private sealed class SessionProperty
        {
            public SessionProperty(string name, bool hasSetter)
            {
                Name = name;
                HasSetter = hasSetter;
            }

            public string Name { get; }

            /// <summary>
            ///     A property with a setter must be assigned on load. One without is
            ///     a collection the session owns, which is filled in place instead.
            /// </summary>
            public bool HasSetter { get; }
        }

        /// <summary>
        ///     Every public property of the session that holds state: one with a
        ///     setter, or a get-only one whose value the session creates and hands
        ///     out to be filled. An expression-bodied property is computed from
        ///     others and stores nothing, so it is not state and is not listed.
        /// </summary>
        private static IReadOnlyList<SessionProperty> SessionState()
        {
            var found = StateOf(50, "public class CharacterCreationSession",
                "CharacterCreation", "Session", "CharacterCreationSession.cs");
            return found;
        }

        /// <summary>The state-holding properties of one class, read out of its real file.</summary>
        private static IReadOnlyList<SessionProperty> StateOf(int fewerThanThisMeansBroken, string declaration,
            params string[] path)
        {
            string body = Block(Strip(File.ReadAllText(ModSource.Path(path))), declaration);

            var found = new List<SessionProperty>();
            foreach (Match match in Regex.Matches(body,
                         @"public\s+(?<decl>[^;{}=]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<tail>\{|=>)"))
            {
                if (Regex.IsMatch(match.Groups["decl"].Value, @"\bclass\b")) continue;
                if (match.Groups["tail"].Value == "=>") continue;

                string accessors = Braced(body, match.Groups["tail"].Index);
                found.Add(new SessionProperty(match.Groups["name"].Value, Regex.IsMatch(accessors, @"\bset\b")));
            }

            found.Should().HaveCountGreaterThan(fewerThanThisMeansBroken,
                $"{declaration} holds more state than this; finding less means this stopped parsing it");
            return found;
        }

        private sealed class SavedField
        {
            public SavedField(string owner, string name, string type, bool hasDefault)
            {
                Owner = owner;
                Name = name;
                Nullable = type.EndsWith("?", StringComparison.Ordinal);
                HasDefault = hasDefault;
            }

            public string Owner { get; }
            public string Name { get; }
            public bool Nullable { get; }
            public bool HasDefault { get; }
        }

        /// <summary>
        ///     Every field of the saved shape: PresetData and the nested records it
        ///     is built from, which drop a field exactly as silently as it does.
        /// </summary>
        private static IReadOnlyList<SavedField> SavedFields()
        {
            string source = PresetSource();
            var found = new List<SavedField>();

            foreach (Match owner in Regex.Matches(source, @"sealed class (?<name>[A-Za-z_][A-Za-z0-9_]*)"))
            {
                string body = Braced(source, source.IndexOf('{', owner.Index));
                foreach (Match field in Regex.Matches(body,
                             @"public\s+(?<decl>[^;{}=]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<init>=[^;]*)?;"))
                    found.Add(new SavedField(
                        owner.Groups["name"].Value,
                        field.Groups["name"].Value,
                        field.Groups["decl"].Value.Trim(),
                        field.Groups["init"].Success));
            }

            found.Should().HaveCountGreaterThan(40,
                "the saved shape holds dozens of fields; finding a handful means this stopped parsing it");
            return found;
        }

        /// <summary>
        ///     The statements of a method body that stand on their own. A write
        ///     nested inside one of them is a write behind a guard.
        /// </summary>
        private static IReadOnlyList<string> TopLevelStatements(string body)
        {
            string inner = body.Substring(1, body.Length - 2);
            var statements = new List<string>();
            int depth = 0, parens = 0, start = 0;

            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    statements.Add(inner.Substring(start, i - start + 1));
                    start = i + 1;
                }
                else if (c == '(') parens++;
                else if (c == ')') parens--;
                else if (c == ';' && depth == 0 && parens == 0)
                {
                    statements.Add(inner.Substring(start, i - start + 1));
                    start = i + 1;
                }
            }

            return statements;
        }

        private static string PresetSource() =>
            Strip(File.ReadAllText(ModSource.Path("Services", "StartPresetService.cs")));

        private static string Member(string signature) => Block(PresetSource(), signature);

        /// <summary>One declaration's body, brace matched, so a neighbor is never read as this one.</summary>
        private static string Block(string source, string declaration)
        {
            int at = source.IndexOf(declaration, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    $"nothing declares '{declaration}' any more; has it been renamed? This check reads the " +
                    "real files, so it cannot run against a name that moved.");

            return Braced(source, source.IndexOf('{', at));
        }

        private static string Braced(string source, int open)
        {
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }

            throw new InvalidOperationException($"the block at {open} is never closed");
        }

        /// <summary>
        ///     Comments out, so a property named in prose is never read as a
        ///     declaration and a commented-out write is never read as a write.
        /// </summary>
        private static string Strip(string source) =>
            Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\r\n]*", "");
    }
}
