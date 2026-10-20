using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Cultured Start is the route v3.28.2 shipped, and these hold it there.
    ///
    ///     The route must behave exactly as that release did, with bug fixes and
    ///     tweaks the only difference. Behavior cannot be run here: the menus, the
    ///     flow table and the apply pipeline all name engine types, and that
    ///     keeps them out of this project. What CAN be read is the source that
    ///     decides the behavior, and the release's own source is in the repository
    ///     at <see cref="PublishedSource.Commit" />. So each test reads both, takes
    ///     the published text, applies exactly the changes this route is allowed to
    ///     carry, and requires the result to be what the working tree holds. Every
    ///     permitted difference is therefore written out below, one pair at a time,
    ///     and anything not written out fails.
    /// </summary>
    public class CulturedStartBaselineTests
    {
        private const string Catalog = "CharacterCreation/Catalog/LifePathCatalog.cs";
        private const string Strings = "_Module/ModuleData/Languages/EN/sta_strings.xml";

        /// <summary>
        ///     The route's menus, each copied out of the release. The published file
        ///     lived in <c>CharacterCreation/Menus</c> under the same name.
        /// </summary>
        private static readonly string[] Menus =
        {
            "LifePathMenus", "ScenarioSelectMenu", "MeansMenu", "CompanionSelectMenu", "HouseholdMenu",
            "WarbandMenu", "ContextualMenus", "FoundingMenu", "StartLocationMenu", "ScenarioChapterMenus",
            "GearCustomizationMenu", "ProvisionsMenu", "StatCustomizationMenu", "ArmsMenu",
            "GearSelectionMenus", "EpilogueMenu", "MenuText"
        };

        /// <summary>
        ///     The differences a menu file may carry beyond the move itself, as the
        ///     published text and what replaced it. Each is a bug fix or a tweak.
        /// </summary>
        private static readonly Dictionary<string, (string Published, string Now)[]> MenuDeltas = new()
        {
            // The stage decides a chapter's staging from its id now, so nothing
            // hands it a stage of its own
            ["LifePathMenus"] = new[]
            {
                ("var menuId = manager.CurrentMenu?.StringId;\n" +
                 "var stage = CharacterPreviewHelper.GetStageForMenu(menuId);\n" +
                 "return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager, stage);",
                    "return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);")
            },
            ["WarbandMenu"] = new[]
            {
                // Title case keeps "with" lower
                ("{=CSR_Warband_Title}Those Who Ride With You", "{=CSR_Warband_Title}Those Who Ride with You"),

                // Fixed: "You Ride Alone" handed over the lowest band of men the
                // settings allow instead of nobody
                ("AddBand(menu, RangePreset.Minimum, \"cs_warband_none\",",
                    "AddBand(menu, null, \"cs_warband_none\","),
                ("private static void AddBand(NarrativeMenu menu, RangePreset preset, string id,",
                    "private static void AddBand(NarrativeMenu menu, RangePreset? preset, string id,"),
                ("m =>\n{\nCreationSession.Current.SelectedTroops = preset;\nCreationSession.Current.CustomTroops = null;\n},",
                    "m =>\n{\n// Riding alone is the absence of a muster, not the smallest band the\n" +
                    "// settings allow, whose floor can be a column of men\nif (!preset.HasValue)\n{\n" +
                    "CreationSession.Current.CustomTroops = 0;\nreturn;\n}\n\n" +
                    "CreationSession.Current.SelectedTroops = preset.Value;\nCreationSession.Current.CustomTroops = null;\n},")
            },
            ["FoundingMenu"] = new[]
            {
                // The kingdom-name prompt is drawn in the mod's own palette like every other
                // question the mod asks, the game's inquiry kept only as its fallback
                ("TaleWorlds.Library.InformationManager.ShowTextInquiry(new TaleWorlds.Library.TextInquiryData(",
                    "Editor.EditorPopups.ShowPrompt("),
                ("true,\ntrue,\nnew TextObject(\"{=CSR_KingdomName_Confirm}Proclaim\")",
                    "null,\nnew TextObject(\"{=CSR_KingdomName_Confirm}Proclaim\")"),
                (".ToString()));\n},\nnull));\n}", ".ToString()));\n});\n}")
            },
            ["ProvisionsMenu"] = new[]
            {
                // Fixed: the answer declines food and never emptied the wagons
                ("\"{=CSR_Provisions_Bare}Empty Wagons\",", "\"{=CSR_Provisions_Bare}An Empty Larder\",")
            },
            ["ScenarioChapterMenus"] = new[]
            {
                // The veterans' land grant is named through Services.VersionedGameApi
                ("DefaultPolicies.LandGrantsForVeteran));", "Services.VersionedGameApi.LandGrantsForVeterans));"),

                // Fixed: "An Empty Wagon" still loaded three random stacks, because an
                // empty cargo list reads the same as a chapter never answered
                ("() => CreationSession.Current.CustomTradeGoods.Clear());",
                    "() =>\n{\n// An empty cargo list reads as an unanswered chapter, which the\n" +
                    "// scenario fills with three random stacks; the refusal is its own fact\n" +
                    "CreationSession.Current.CustomTradeGoods.Clear();\nCreationSession.Current.NoTradeGoods = true;\n});"),
                ("session.CustomTradeGoods.Clear();\n\nvar goods = ArmorQuery.TradeGoodItems();",
                    "session.CustomTradeGoods.Clear();\nsession.NoTradeGoods = false;\n\nvar goods = ArmorQuery.TradeGoodItems();")
            }
        };

        /// <summary>Strings whose published text was corrected, and what they read now.</summary>
        private static readonly Dictionary<string, string> StringTweaks = new(StringComparer.Ordinal)
        {
            ["CSR_Reason_History_Desc"] = "You longed to be remembered in the chronicles.",
            ["CSR_Reason_ProveWorth_Desc"] = "The wider world seemed the truest test of your mettle.",
            ["CSR_Warband_Title"] = "Those Who Ride with You",
            ["CSR_Provisions_Bare"] = "An Empty Larder"
        };

        private static readonly string[] StartTypes =
        {
            "Commoner", "Monarch", "LandedVassal", "LandlessVassal", "Mercenary", "Outlaw", "CaravanMaster",
            "RebelClan"
        };

        #region The catalog

        [Fact]
        public void The_catalog_is_the_published_catalog_without_the_idiom_chapter()
        {
            var published = Lines(PublishedSource.Read(Catalog));

            int from = published.FindIndex(line => line == "#region Idiom");
            int to = published.FindIndex(line => line == "#region Youth");
            from.Should().BeGreaterThan(0, "the published catalog carried the idiom chapter");
            to.Should().BeGreaterThan(from);
            published.RemoveRange(from, to - from);

            published.Remove("public const string IdiomMenuId = \"cs_idiom_menu\";").Should().BeTrue();
            published.Remove("IdiomMenu(),").Should().BeTrue();

            var expected = published
                .Select(line => line
                    .Replace("You longed to be remembered in the chronicles of Calradia.",
                        StringTweaks["CSR_Reason_History_Desc"])
                    .Replace("Calradia seemed the truest test of your mettle.",
                        StringTweaks["CSR_Reason_ProveWorth_Desc"]))
                .ToList();

            SameLines(Lines(PublishedSource.Current(Catalog)), expected, Catalog);
        }

        /// <summary>
        ///     Every answer, read out of both catalogs as the call that declares it
        ///     and the effects written against it, is the same answer. The text test
        ///     above already implies this; this states it per answer, so a failure
        ///     names the answer that grants something different.
        /// </summary>
        [Fact]
        public void Every_answer_grants_what_it_granted_in_the_release()
        {
            var published = Answers(PublishedSource.Read(Catalog))
                .Where(pair => !pair.Key.StartsWith("cs_idiom_", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var current = Answers(PublishedSource.Current(Catalog));

            current.Keys.Should().BeEquivalentTo(published.Keys);
            current.Should().HaveCount(65, "seven chapters: 9, 14, 9, 10, 9, 10 and 4 answers");

            foreach (var pair in published)
                Tweaked(current[pair.Key]).Should().Be(Tweaked(pair.Value),
                    "{0} must grant exactly what the release granted", pair.Key);
        }

        [Fact]
        public void The_street_gangs_still_teach_roguery_and_the_blade()
        {
            var gangs = Answers(PublishedSource.Current(Catalog))["cs_child_gangs"];

            gangs.Should().Contain("DefaultSkills.Roguery").And.Contain("DefaultSkills.OneHanded")
                .And.Contain("DefaultCharacterAttributes.Cunning");
        }

        [Fact]
        public void The_sea_answers_are_offered_only_where_war_sails_is()
        {
            var answers = Answers(PublishedSource.Current(Catalog));
            // The line end is matched as a class rather than typed into the pattern, so a
            // checkout that writes CRLF counts the same answers as one that writes LF
            answers.Values.Count(call => Regex.IsMatch(call, @",\s*NavalDLCService\.Skill\w+\)(?:\r?\n|$)"))
                .Should().Be(14, "fourteen answers put a character on the water and name the skill they need");

            Normalized(PublishedSource.Current("CharacterCreation/CulturedStart/LifePathMenus.cs"))
                .Should().Contain("if (choice.RequiredNavalSkill != null &&\nNavalDLCService.GetNavalSkill(choice.RequiredNavalSkill) == null)\ncontinue;",
                    "an answer whose skill the game does not have is never put on the stage");
        }

        #endregion

        #region The menus

        public static IEnumerable<object[]> EveryMenu() => Menus.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(EveryMenu))]
        public void Each_menu_is_the_published_menu(string name)
        {
            string published = Normalized(PublishedSource.Read($"CharacterCreation/Menus/{name}.cs"));

            published = published.Replace("namespace CulturedStartReloaded.CharacterCreation.Menus",
                "namespace CulturedStartReloaded.CharacterCreation.CulturedStart");
            published = published.Replace("using CulturedStartReloaded.CharacterCreation.Flow;",
                "using CulturedStartReloaded.CharacterCreation.Flow;\nusing CulturedStartReloaded.CharacterCreation.Menus;");
            published = Regex.Replace(published, @",\s*CharacterPreviewHelper\.PreviewStage\.\w+\s*\)", ")");

            if (MenuDeltas.TryGetValue(name, out var deltas))
                foreach (var (was, now) in deltas)
                {
                    published.Should().Contain(was, "{0} carried that text when it shipped", name);
                    published = published.Replace(was, now);
                }

            string current = Normalized(PublishedSource.Current($"CharacterCreation/CulturedStart/{name}.cs"));
            SameLines(current.Split('\n').ToList(), published.Split('\n').ToList(), name);
        }

        #endregion

        #region The strings

        [Fact]
        public void Every_string_the_route_shows_is_the_published_string()
        {
            var sources = Menus.Select(name => $"CharacterCreation/CulturedStart/{name}.cs")
                .Append(Catalog)
                .ToList();

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string source in sources)
            foreach (Match match in Regex.Matches(PublishedSource.Current(source), @"\{=([A-Za-z0-9_]+)\}"))
                keys.Add(match.Groups[1].Value);

            keys.Should().HaveCountGreaterThan(300, "the route shows a few hundred lines of its own");

            var shipped = Table(PublishedSource.Read(Strings));
            var now = Table(PublishedSource.Current(Strings));

            foreach (string key in keys)
            {
                shipped.Should().ContainKey(key, "{0} is shown by the route and must have shipped", key);
                now.Should().ContainKey(key, "{0} is shown by the route and must be declared", key);

                string expected = StringTweaks.TryGetValue(key, out var tweak) ? tweak : shipped[key];
                now[key].Should().Be(expected, "{0} reads as it shipped", key);
            }
        }

        #endregion

        #region The flow

        /// <summary>
        ///     For every start type, with the weapon slots asked for or not and in
        ///     story mode or sandbox, the route asks the same chapters in the same
        ///     order the release asked them, less the idiom chapter.
        /// </summary>
        [Fact]
        public void The_route_asks_the_published_chapters_in_the_published_order()
        {
            var published = new Flow(PublishedSource.Read("CharacterCreation/Flow/CreationFlow.cs"), "Narrative");
            var current = new Flow(PublishedSource.Current("CharacterCreation/Flow/CreationFlow.cs"), "LifePath");

            int walks = 0;
            foreach (string startType in StartTypes)
            foreach (bool bySlot in new[] { false, true })
            foreach (bool storyMode in new[] { false, true })
            {
                var situation = new Situation(startType, bySlot, storyMode);

                var asked = current.Walk(situation);
                var shipped = published.Walk(situation).Where(id => id != "cs_idiom_menu").ToList();

                asked.Should().Equal(shipped,
                    "a {0} (slots {1}, story mode {2}) walks the chapters the release walked", startType, bySlot,
                    storyMode);
                walks++;
            }

            walks.Should().Be(32);
        }

        #endregion

        #region The pipeline

        [Fact]
        public void The_skill_levels_the_answers_stand_for_are_the_published_ones()
        {
            const string Step = "Services/Application/Steps/NarrativeStep.cs";
            string shipped = PublishedSource.Read(Step);
            string now = PublishedSource.Current(Step);

            foreach (string constant in new[] { "FocusedBaseLevel", "LevelPerFocusPoint", "UnfocusedBaseLevel" })
                Constant(now, constant).Should().Be(Constant(shipped, constant), "{0} prices a chapter's focus", constant);

            string lifePath = ModSource.MemberBody(Step, "private static void ApplyLifePath");
            lifePath.Should().Contain("LifePathCatalog.GetFocusTotals(session)")
                .And.Contain("SetInitialSkillLevel(skill, LifePathLevel(points))")
                .And.Contain("ApplyCustomStats(hero, session)");

            ModSource.MemberBody(Step, "public void Apply").Should()
                .Contain("if (session.Mode == SetupMode.LifePath)",
                    "the route's sheet is written by its own arithmetic and nothing else in the step");
        }

        [Fact]
        public void The_traces_of_a_life_are_the_published_ones()
        {
            const string Step = "Services/Application/Steps/ConsequenceStep.cs";
            string shipped = PublishedSource.Read(Step);
            string now = PublishedSource.Current(Step);

            foreach (string constant in new[] { "RelationBonus", "MaxRelationTargets" })
                Constant(now, constant).Should().Be(Constant(shipped, constant), "{0} sizes a chapter's goodwill", constant);

            Normalized(Body(now, "private static ItemObject? ResolveHeirloom"))
                .Should().Be(Normalized(Body(shipped, "private static ItemObject? ResolveHeirloom")),
                    "a keepsake resolves to the item it always did");

            string chapters = Normalized(Body(now, "private static void ApplyChapters"));
            chapters.Should().Contain("int level = Math.Max(-1, Math.Min(1, pair.Value));",
                "a chapter's leaning moves a trait one step, as it did");
            chapters.Should().Contain("ApplyPlayerRelation(target, RelationBonus, false, false);");
        }

        /// <summary>
        ///     The companions, relatives and founding lords Cultured Start makes are
        ///     rebuilt from their role the way every route's are, and never come out
        ///     weaker than the template they were drawn from, which is what that
        ///     route's characters were. A level set in the Start Editor is the
        ///     player's word and is never floored.
        /// </summary>
        [Fact]
        public void The_routes_own_characters_never_fall_below_their_template()
        {
            const string Outfitter = "Services/HeroOutfitter.cs";

            ModSource.MemberBody(Outfitter, "private static (int Level, int Best)? TemplateFloor")
                .Should().Contain("if (session.Mode != SetupMode.LifePath || spec?.Level != null) return null;");

            string outfit = ModSource.MemberBody(Outfitter, "public static void Outfit");
            outfit.IndexOf("TemplateFloor(hero, session, spec)", StringComparison.Ordinal)
                .Should().BeLessThan(outfit.IndexOf("BlankTheTemplateSheet(hero)", StringComparison.Ordinal),
                    "the floor is read off the template before its sheet is taken off");
            outfit.Should().Contain("level = Math.Max(level, floor.Value.Level)");

            ModSource.MemberBody(Outfitter, "private static void ApplySkills")
                .Should().Contain("Math.Min(GameCaps.MaxSkillLevel(), floorBest)");
        }

        #endregion

        #region Reading

        /// <summary>The source with its layout taken out: line endings, byte order mark and indentation.</summary>
        private static string Normalized(string text) =>
            string.Join("\n", Lines(text));

        private static List<string> Lines(string text) =>
            text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n')
                .Select(line => line.Trim())
                .ToList();

        private static void SameLines(List<string> actual, List<string> expected, string what)
        {
            while (actual.Count > 0 && actual[^1].Length == 0) actual.RemoveAt(actual.Count - 1);
            while (expected.Count > 0 && expected[^1].Length == 0) expected.RemoveAt(expected.Count - 1);

            for (int line = 0; line < Math.Min(actual.Count, expected.Count); line++)
                actual[line].Should().Be(expected[line], "{0} line {1} differs from the release", what, line + 1);

            actual.Count.Should().Be(expected.Count, "{0} holds exactly the release's lines", what);
        }

        private static string Tweaked(string text) =>
            text.Replace("You longed to be remembered in the chronicles of Calradia.",
                    StringTweaks["CSR_Reason_History_Desc"])
                .Replace("Calradia seemed the truest test of your mettle.", StringTweaks["CSR_Reason_ProveWorth_Desc"]);

        /// <summary>
        ///     Each answer's declaring call, closed by counting parentheses, with the
        ///     effects written against the same id appended, keyed by the answer's id.
        /// </summary>
        private static Dictionary<string, string> Answers(string source)
        {
            string text = Normalized(source);
            var answers = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match call in Regex.Matches(text, @"\b[FCIWEYTRA]\(\s*""(cs_[a-z_]+)"""))
                answers[call.Groups[1].Value] = Balanced(text, call.Index + 1);

            foreach (Match effect in Regex.Matches(text, @"Effects\(choices, ""(cs_[a-z_]+)"""))
                if (answers.ContainsKey(effect.Groups[1].Value))
                    answers[effect.Groups[1].Value] += "\n" + Balanced(text, effect.Index + "Effects".Length);

            return answers;
        }

        private static string Balanced(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')' && --depth == 0) return text.Substring(open, i - open + 1);
            }

            throw new InvalidOperationException("an unclosed call in the catalog");
        }

        private static string Body(string source, string member)
        {
            int at = source.IndexOf(member, StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException($"no {member}");

            int open = source.IndexOf('{', at);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }

            throw new InvalidOperationException($"{member} is never closed");
        }

        private static int Constant(string source, string name)
        {
            var match = Regex.Match(source, $@"const int {name} = (\d+);");
            if (!match.Success) throw new InvalidOperationException($"no constant {name}");
            return int.Parse(match.Groups[1].Value);
        }

        private static Dictionary<string, string> Table(string xml)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(xml, @"<string\s+id=""([^""]+)""\s+text=""([^""]*)""\s*/>"))
                table[match.Groups[1].Value] = WebUtility.HtmlDecode(match.Groups[2].Value);
            return table;
        }

        #endregion

        #region The flow table, evaluated

        private sealed record Situation(string StartType, bool BySlot, bool StoryMode);

        /// <summary>
        ///     One version's flow table, with its predicates evaluated for one route
        ///     over a situation. The table names its predicates as small helpers and
        ///     simple conditions on the start type, the slot flag and the game mode,
        ///     and this reads exactly those; a condition it does not recognize throws
        ///     rather than guessing, so a new kind of gate fails here loudly.
        /// </summary>
        private sealed class Flow
        {
            private readonly string _source;
            private readonly string _route;
            private readonly List<(string Id, string Predicate)> _rows = new();

            public Flow(string source, string route)
            {
                _source = Regex.Replace(source.Replace("\r\n", "\n"), @"//[^\n]*", "");
                _route = route;

                int at = _source.IndexOf("private static readonly Node[] Order", StringComparison.Ordinal);
                int end = _source.IndexOf("\n        };", at, StringComparison.Ordinal);
                string table = _source.Substring(at, end - at);

                foreach (Match start in Regex.Matches(table, @"new\(""([a-z][a-z0-9_]*)"""))
                {
                    int open = table.IndexOf('(', start.Index);
                    string entry = Inner(table, open);
                    var parts = TopLevel(entry, ",");
                    string predicate = parts.Count > 1 && !parts[1].Contains(':') ? parts[1] : "true";
                    _rows.Add((start.Groups[1].Value, predicate));
                }
            }

            public List<string> Walk(Situation situation) =>
                _rows.Where(row => Eval(row.Predicate, situation)).Select(row => row.Id).ToList();

            private bool Eval(string expression, Situation situation)
            {
                string e = Collapse(expression);
                if (e.StartsWith("s =>", StringComparison.Ordinal)) e = e.Substring(4).Trim();

                while (e.StartsWith("(", StringComparison.Ordinal) && Inner(e, 0).Length == e.Length - 2)
                    e = Inner(e, 0).Trim();

                var any = TopLevel(e, "||");
                if (any.Count > 1) return any.Any(part => Eval(part, situation));

                var all = TopLevel(e, "&&");
                if (all.Count > 1) return all.All(part => Eval(part, situation));

                if (e == "true" || e == "!s.RouteDecidedExternally") return true;
                if (e == "CSGameModeService.IsStoryMode()") return situation.StoryMode;
                if (e == "s.ChooseWeaponsIndividually") return situation.BySlot;

                if (e.Contains("SelectedStartType"))
                    return Regex.Matches(e, @"StartType\.(\w+)").Cast<Match>()
                        .Any(match => match.Groups[1].Value == situation.StartType);

                var helper = Regex.Match(e, @"^(\w+)(\(s\))?$");
                if (!helper.Success) throw new InvalidOperationException($"an unrecognized gate: {e}");

                string name = helper.Groups[1].Value;
                if (name == _route) return true;
                if (name is "Narrative" or "LifePath" or "Custom") return false;

                var body = Regex.Match(_source,
                    $@"private static bool {name}\(CharacterCreationSession s\) =>\s*(?<body>[^;]*);");
                if (!body.Success) throw new InvalidOperationException($"no helper {name}");

                return Eval(body.Groups["body"].Value, situation);
            }

            private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

            private static string Inner(string text, int open)
            {
                int depth = 0;
                for (int i = open; i < text.Length; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')' && --depth == 0) return text.Substring(open + 1, i - open - 1);
                }

                throw new InvalidOperationException("an unclosed parenthesis in the flow table");
            }

            private static List<string> TopLevel(string text, string separator)
            {
                var parts = new List<string>();
                int depth = 0;
                int last = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;
                    else if (depth == 0 && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
                    {
                        parts.Add(text.Substring(last, i - last).Trim());
                        last = i + separator.Length;
                        i += separator.Length - 1;
                    }
                }

                parts.Add(text.Substring(last).Trim());
                return parts;
            }
        }

        #endregion
    }
}
