using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Composes realm names from parts rather than picking a written one, so a
    ///     player who founds ten kingdoms is not handed the same name twice. A name
    ///     is a pattern plus an adjective and a realm noun, over the seat or a
    ///     stretch of country. A culture's name is never used: it is a noun, and a
    ///     noun in an adjective's slot reads as "Empire Kingdom". Names a living
    ///     realm already carries, and names this install has used before, are never
    ///     offered.
    ///
    ///     The candidates are composed once and held on the session, because a name
    ///     that changed every time the row redrew would be a name nobody chose.
    ///     Deliberately never reads the clan name: the clan is named after the
    ///     editor runs, and the preview would then disagree with the campaign.
    /// </summary>
    public static class KingdomNameGenerator
    {
        private const int CandidateCount = 3;
        private const int MemoryDepth = 200;
        private const int MaxAttempts = 240;

        private enum Flavor
        {
            Regal,
            Martial,
            Free,
            Devout,
            Elder,
            Dark
        }

        private sealed class Pattern
        {
            public Pattern(string template, int weight, bool needsAdjective, bool needsPlace,
                bool needsDomain = false)
            {
                Template = template;
                Weight = weight;
                NeedsAdjective = needsAdjective;
                NeedsPlace = needsPlace;
                NeedsDomain = needsDomain;
            }

            public string Template { get; }
            public int Weight { get; }
            public bool NeedsAdjective { get; }
            public bool NeedsPlace { get; }
            /// <summary>Takes a stretch of country rather than the seat's own name.</summary>
            public bool NeedsDomain { get; }
        }

        private static readonly Dictionary<Flavor, string[]> Adjectives = new()
        {
            [Flavor.Regal] = new[]
            {
                "{=CSR_RealmAdj_Golden}Golden",
                "{=CSR_RealmAdj_Sovereign}Sovereign",
                "{=CSR_RealmAdj_Imperial}Imperial",
                "{=CSR_RealmAdj_Radiant}Radiant",
                "{=CSR_RealmAdj_Crowned}Crowned",
                "{=CSR_RealmAdj_Argent}Argent",
                "{=CSR_RealmAdj_Resplendent}Resplendent",
                "{=CSR_RealmAdj_Exalted}Exalted",
                "{=CSR_RealmAdj_Gilded}Gilded",
                "{=CSR_RealmAdj_Serene}Serene",
                "{=CSR_RealmAdj_Illustrious}Illustrious",
                "{=CSR_RealmAdj_Highborn}Highborn"
            },
            [Flavor.Martial] = new[]
            {
                "{=CSR_RealmAdj_Iron}Iron",
                "{=CSR_RealmAdj_Unbroken}Unbroken",
                "{=CSR_RealmAdj_Steadfast}Steadfast",
                "{=CSR_RealmAdj_Vigilant}Vigilant",
                "{=CSR_RealmAdj_Adamant}Adamant",
                "{=CSR_RealmAdj_Undaunted}Undaunted",
                "{=CSR_RealmAdj_Tempered}Tempered",
                "{=CSR_RealmAdj_Stalwart}Stalwart",
                "{=CSR_RealmAdj_Unyielding}Unyielding",
                "{=CSR_RealmAdj_Indomitable}Indomitable",
                "{=CSR_RealmAdj_Bannered}Bannered",
                "{=CSR_RealmAdj_Embattled}Embattled"
            },
            [Flavor.Free] = new[]
            {
                "{=CSR_RealmAdj_Free}Free",
                "{=CSR_RealmAdj_Open}Open",
                "{=CSR_RealmAdj_Common}Common",
                "{=CSR_RealmAdj_Unbound}Unbound",
                "{=CSR_RealmAdj_Untethered}Untethered",
                "{=CSR_RealmAdj_Elected}Elected",
                "{=CSR_RealmAdj_Concordant}Concordant",
                "{=CSR_RealmAdj_Peaceable}Peaceable",
                "{=CSR_RealmAdj_Unfettered}Unfettered",
                "{=CSR_RealmAdj_Plainspoken}Plainspoken",
                "{=CSR_RealmAdj_Equal}Equal",
                "{=CSR_RealmAdj_Willing}Willing"
            },
            [Flavor.Devout] = new[]
            {
                "{=CSR_RealmAdj_Hallowed}Hallowed",
                "{=CSR_RealmAdj_Sacred}Sacred",
                "{=CSR_RealmAdj_Blessed}Blessed",
                "{=CSR_RealmAdj_Faithful}Faithful",
                "{=CSR_RealmAdj_Devout}Devout",
                "{=CSR_RealmAdj_Consecrated}Consecrated",
                "{=CSR_RealmAdj_Solemn}Solemn",
                "{=CSR_RealmAdj_Reverent}Reverent",
                "{=CSR_RealmAdj_Anointed}Anointed",
                "{=CSR_RealmAdj_Pious}Pious",
                "{=CSR_RealmAdj_Sanctified}Sanctified",
                "{=CSR_RealmAdj_Abiding}Abiding"
            },
            [Flavor.Elder] = new[]
            {
                "{=CSR_RealmAdj_Elder}Elder",
                "{=CSR_RealmAdj_Ancient}Ancient",
                "{=CSR_RealmAdj_Everlasting}Everlasting",
                "{=CSR_RealmAdj_Enduring}Enduring",
                "{=CSR_RealmAdj_Undying}Undying",
                "{=CSR_RealmAdj_Timeless}Timeless",
                "{=CSR_RealmAdj_Primeval}Primeval",
                "{=CSR_RealmAdj_Immemorial}Immemorial",
                "{=CSR_RealmAdj_Venerable}Venerable",
                "{=CSR_RealmAdj_Perennial}Perennial",
                "{=CSR_RealmAdj_Storied}Storied",
                "{=CSR_RealmAdj_Hoary}Hoary"
            },
            [Flavor.Dark] = new[]
            {
                "{=CSR_RealmAdj_Gray}Gray",
                "{=CSR_RealmAdj_Crimson}Crimson",
                "{=CSR_RealmAdj_Silent}Silent",
                "{=CSR_RealmAdj_Shadowed}Shadowed",
                "{=CSR_RealmAdj_Sable}Sable",
                "{=CSR_RealmAdj_Ashen}Ashen",
                "{=CSR_RealmAdj_Riven}Riven",
                "{=CSR_RealmAdj_Grim}Grim",
                "{=CSR_RealmAdj_Stark}Stark",
                "{=CSR_RealmAdj_Bleak}Bleak",
                "{=CSR_RealmAdj_Wintered}Wintered",
                "{=CSR_RealmAdj_Thorned}Thorned"
            }
        };

        private static readonly string[] Nouns =
        {
            "{=CSR_RealmNoun_Kingdom}Kingdom",
            "{=CSR_RealmNoun_Realm}Realm",
            "{=CSR_RealmNoun_Crown}Crown",
            "{=CSR_RealmNoun_Dominion}Dominion",
            "{=CSR_RealmNoun_Throne}Throne",
            "{=CSR_RealmNoun_Reach}Reach",
            "{=CSR_RealmNoun_March}March",
            "{=CSR_RealmNoun_Sovereignty}Sovereignty",
            "{=CSR_RealmNoun_Hegemony}Hegemony",
            "{=CSR_RealmNoun_Ascendancy}Ascendancy",
            "{=CSR_RealmNoun_Concord}Concord",
            "{=CSR_RealmNoun_Accord}Accord",
            "{=CSR_RealmNoun_Banner}Banner",
            "{=CSR_RealmNoun_Host}Host",
            "{=CSR_RealmNoun_Demesne}Demesne",
            "{=CSR_RealmNoun_Principality}Principality",
            "{=CSR_RealmNoun_Protectorate}Protectorate",
            "{=CSR_RealmNoun_Commonwealth}Commonwealth"
        };

        /// <summary>
        ///     A stretch of country, so a realm can be named for the land it holds
        ///     rather than for the one hall it happens to sit in. Every earlier
        ///     offer read "something of {the seat}", which is one idea repeated.
        /// </summary>
        private static readonly string[] Domains =
        {
            "{=CSR_RealmDom_Rivers}Rivers",
            "{=CSR_RealmDom_Marches}Marches",
            "{=CSR_RealmDom_Highlands}Highlands",
            "{=CSR_RealmDom_Coast}Coast",
            "{=CSR_RealmDom_Plains}Plains",
            "{=CSR_RealmDom_Hills}Hills",
            "{=CSR_RealmDom_Wolds}Wolds",
            "{=CSR_RealmDom_Fens}Fens",
            "{=CSR_RealmDom_Steppe}Steppe",
            "{=CSR_RealmDom_Reaches}Reaches",
            "{=CSR_RealmDom_Vales}Vales",
            "{=CSR_RealmDom_Downs}Downs",
            "{=CSR_RealmDom_Moors}Moors",
            "{=CSR_RealmDom_Straits}Straits",
            "{=CSR_RealmDom_Shores}Shores",
            "{=CSR_RealmDom_Wilds}Wilds",
            "{=CSR_RealmDom_Passes}Passes",
            "{=CSR_RealmDom_Headlands}Headlands",
            "{=CSR_RealmDom_Frontier}Frontier",
            "{=CSR_RealmDom_Waters}Waters"
        };

        private static readonly Pattern[] Patterns =
        {
            new("{=CSR_RealmPat_AdjNoun}{ADJ} {NOUN}", 3, true, false),
            new("{=CSR_RealmPat_AdjNounPlace}{ADJ} {NOUN} of {PLACE}", 2, true, true),
            new("{=CSR_RealmPat_NounPlace}{NOUN} of {PLACE}", 2, false, true),
            new("{=CSR_RealmPat_PlaceNoun}{PLACE} {NOUN}", 2, false, true),
            new("{=CSR_RealmPat_NounDomain}{NOUN} of the {DOMAIN}", 3, false, false, true),
            new("{=CSR_RealmPat_AdjNounDomain}{NOUN} of the {ADJ} {DOMAIN}", 3, true, false, true),
            new("{=CSR_RealmPat_AdjDomain}{ADJ} {DOMAIN}", 2, true, false, true),
            new("{=CSR_RealmPat_DomainNoun}{DOMAIN} {NOUN}", 2, false, false, true)
        };

        /// <summary>
        ///     The names on offer for this character, composed once and then stable.
        ///     Empty when creation has settled nothing to build one from.
        /// </summary>
        public static IReadOnlyList<string> Candidates(CharacterCreationSession? session)
        {
            if (session == null) return Array.Empty<string>();
            if (session.ComposedRealmNames.Count > 0) return session.ComposedRealmNames;

            foreach (var name in Compose(session))
                session.ComposedRealmNames.Add(name);

            return session.ComposedRealmNames;
        }

        /// <summary>
        ///     A realm's four names, the shape the base game gives every kingdom:
        ///     a bare name, the informal form, the fuller title, and what its
        ///     monarch is called. Vanilla reads "Vlandia" / "Vlandians" /
        ///     "Kingdom of Vlandia" / "King", so one string was never enough.
        /// </summary>
        public sealed class RealmName
        {
            public RealmName(string name, string informal, string title, string rulerTitle)
            {
                Name = name;
                Informal = informal;
                Title = title;
                RulerTitle = rulerTitle;
            }

            public string Name { get; }
            public string Informal { get; }
            public string Title { get; }
            public string RulerTitle { get; }

            public override string ToString() => Name;
        }

        /// <summary>
        ///     The four names for this founding. Pass <paramref name="chosenName"/>
        ///     to keep a name the player settled and still get the rest of the set.
        /// </summary>
        public static RealmName? BuildRealm(CharacterCreationSession? session, string? chosenName = null)
        {
            var name = string.IsNullOrWhiteSpace(chosenName) ? Preview(session) : chosenName!.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;

            var place = session?.SelectedSettlement?.Name?.ToString();
            bool namesTheSeat = !string.IsNullOrWhiteSpace(place) &&
                                name!.IndexOf(place!, StringComparison.OrdinalIgnoreCase) >= 0;

            TextObject title;
            if (!namesTheSeat && !string.IsNullOrWhiteSpace(place))
            {
                title = new TextObject("{=CSR_RealmTitle_OfPlace}The {NAME} of {PLACE}");
                title.SetTextVariable("PLACE", place);
            }
            else
            {
                title = new TextObject("{=CSR_RealmTitle_The}The {NAME}");
            }

            title.SetTextVariable("NAME", name);
            return new RealmName(name!, name!, title.ToString(), RulerTitleFor(session));
        }

        /// <summary>
        ///     What this culture calls its monarch, taken from a realm of the same
        ///     culture that already exists, because a conversion redefines those
        ///     realms and its own word is therefore the right one.
        /// </summary>
        private static string RulerTitleFor(CharacterCreationSession? session)
        {
            try
            {
                var culture = session?.SelectedCulture ?? Hero.MainHero?.Culture;
                if (culture != null)
                {
                    foreach (var kingdom in Kingdom.All)
                    {
                        if (kingdom == null || kingdom.IsEliminated || kingdom.Culture != culture) continue;

                        var borrowed = kingdom.EncyclopediaRulerTitle?.ToString();
                        if (!string.IsNullOrWhiteSpace(borrowed)) return borrowed!;
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"KingdomNameGenerator: ruler titles unreadable ({ex.GetType().Name}).");
            }

            bool female = Hero.MainHero?.IsFemale ?? false;
            return new TextObject(female
                ? "{=CSR_RulerTitle_Queen}Queen"
                : "{=CSR_RulerTitle_King}King").ToString();
        }

        /// <summary>The name a realm takes when nothing else was chosen.</summary>
        public static string? Preview(CharacterCreationSession? session)
        {
            return Candidates(session).FirstOrDefault();
        }

        /// <summary>
        ///     Records a name this install has now used, so no later founding is
        ///     offered it again.
        /// </summary>
        public static void Remember(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var used = Used();
                if (used.Any(u => string.Equals(u, name, StringComparison.OrdinalIgnoreCase))) return;

                used.Add(name!.Trim());
                while (used.Count > MemoryDepth) used.RemoveAt(0);
                Directory.CreateDirectory(Folder());
                File.WriteAllLines(MemoryFile(), used);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"KingdomNameGenerator: could not record the realm name ({ex.GetType().Name}).");
            }
        }

        private static List<string> Compose(CharacterCreationSession session)
        {
            var names = new List<string>();

            try
            {
                var place = session.SelectedSettlement?.Name?.ToString();

                var patterns = Patterns
                    .Where(p => !p.NeedsPlace || !string.IsNullOrWhiteSpace(place))
                    .ToList();
                if (patterns.Count == 0) return names;

                var taken = TakenNames();
                foreach (var used in Used()) taken.Add(used);

                var weights = FlavorWeights(session);

                // Three rolls of the same weighted bag gave three names of the same
                // shape, and with the seat in most of them: every offer read
                // "something of {the seat}". Each candidate takes a pattern the
                // others have not, at most one names the seat, and at most two
                // carry an adjective, so the three read as three ideas.
                var unused = new List<Pattern>(patterns);
                int namedTheSeat = 0;
                int carriedAdjective = 0;

                for (int attempt = 0; attempt < MaxAttempts && names.Count < CandidateCount; attempt++)
                {
                    if (unused.Count == 0) unused.AddRange(patterns);

                    var allowed = unused
                        .Where(p => !p.NeedsPlace || namedTheSeat < 1)
                        .Where(p => !p.NeedsAdjective || carriedAdjective < CandidateCount - 1)
                        .ToList();
                    if (allowed.Count == 0) allowed = unused;

                    var pattern = PickWeighted(allowed);
                    if (pattern == null) break;

                    var text = new TextObject(pattern.Template);
                    if (pattern.NeedsAdjective)
                        text.SetTextVariable("ADJ", Localized(PickAdjective(weights)));
                    text.SetTextVariable("NOUN", Localized(CSRandom.Pick(Nouns) ?? Nouns[0]));
                    if (pattern.NeedsDomain)
                        text.SetTextVariable("DOMAIN", Localized(CSRandom.Pick(Domains) ?? Domains[0]));
                    if (!string.IsNullOrWhiteSpace(place)) text.SetTextVariable("PLACE", place);

                    var name = text.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (taken.Contains(name)) continue;
                    if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) continue;

                    names.Add(name);
                    unused.Remove(pattern);
                    if (pattern.NeedsPlace) namedTheSeat++;
                    if (pattern.NeedsAdjective) carriedAdjective++;
                }

                CSLogger.Info($"KingdomNameGenerator: composed {names.Count} realm name(s) " +
                              $"from seat [{place ?? "none"}].");
            }
            catch (Exception ex)
            {
                CSLogger.Error("KingdomNameGenerator: composing realm names failed.", ex);
            }

            return names;
        }

        private static Dictionary<Flavor, int> FlavorWeights(CharacterCreationSession session)
        {
            var weights = new Dictionary<Flavor, int>();
            foreach (Flavor flavor in Enum.GetValues(typeof(Flavor)))
                weights[flavor] = 2;

            // A crown that was settled and a crown that was seized do not sound alike
            if (session.SelectedFounding == MonarchFounding.Settler)
            {
                weights[Flavor.Free] += 3;
                weights[Flavor.Elder] += 2;
            }
            else
            {
                weights[Flavor.Martial] += 3;
                weights[Flavor.Regal] += 2;
            }

            return weights;
        }

        private static string PickAdjective(Dictionary<Flavor, int> weights)
        {
            int total = weights.Values.Sum();
            int roll = CSRandom.Next(Math.Max(1, total));
            foreach (var pair in weights)
            {
                roll -= pair.Value;
                if (roll < 0)
                {
                    var pool = Adjectives[pair.Key];
                    return CSRandom.Pick(pool) ?? pool[0];
                }
            }

            var fallback = Adjectives[Flavor.Regal];
            return CSRandom.Pick(fallback) ?? fallback[0];
        }

        private static Pattern? PickWeighted(IReadOnlyList<Pattern> patterns)
        {
            if (patterns.Count == 0) return null;

            int total = patterns.Sum(p => p.Weight);
            if (total <= 0) return patterns[0];

            int roll = CSRandom.Next(total);
            foreach (var pattern in patterns)
            {
                roll -= pattern.Weight;
                if (roll < 0) return pattern;
            }

            return patterns[patterns.Count - 1];
        }

        private static string Localized(string template)
        {
            return new TextObject(template).ToString();
        }

        private static HashSet<string> TakenNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // The editor runs mid-creation, where the campaign's object lists are
                // not guaranteed; an unreadable roster only costs collision avoidance
                foreach (var kingdom in Kingdom.All)
                {
                    var name = kingdom?.Name?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name!);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"KingdomNameGenerator: realm roster unreadable ({ex.GetType().Name}).");
            }

            return names;
        }

        private static List<string> Used()
        {
            try
            {
                var path = MemoryFile();
                if (!File.Exists(path)) return new List<string>();

                return File.ReadAllLines(path)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"KingdomNameGenerator: used-name list unreadable ({ex.GetType().Name}).");
                return new List<string>();
            }
        }

        private static string Folder()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "CulturedStartReloaded");
        }

        private static string MemoryFile()
        {
            return Path.Combine(Folder(), "realm-names.txt");
        }

        /// <summary>
        ///     A conversion may hand a culture a name that already carries an
        ///     article, and "The The North Kingdom" is what pasting one in produces.
        /// </summary>
        private static string? StripArticle(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var trimmed = name!.Trim();
            return trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(4).Trim()
                : trimmed;
        }
    }
}
