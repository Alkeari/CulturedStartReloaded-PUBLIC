using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>A career Tales from the Age of Men offers, read out of its own registry.</summary>
    public sealed class TaomCareer
    {
        public TaomCareer(string id, string name, string description, int minClanTier,
            IReadOnlyList<string> cultureIds, IReadOnlyList<string> groupIds, string firstRankName)
        {
            Id = id;
            Name = name;
            Description = description;
            MinClanTier = minClanTier;
            CultureIds = cultureIds;
            GroupIds = groupIds;
            FirstRankName = firstRankName;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MinClanTier { get; }
        public IReadOnlyList<string> CultureIds { get; }
        public IReadOnlyList<string> GroupIds { get; }
        public string FirstRankName { get; }
    }

    /// <summary>One pick on a career's own advancement board.</summary>
    public sealed class TaomCareerChoice
    {
        public TaomCareerChoice(string id, string groupName, string description, int tier)
        {
            Id = id;
            GroupName = groupName;
            Description = description;
            Tier = tier;
        }

        public string Id { get; }
        public string GroupName { get; }
        public string Description { get; }
        public int Tier { get; }
    }

    /// <summary>A faction's own resource, such as Gondor's Castar.</summary>
    public sealed class TaomResource
    {
        public TaomResource(string id, string name, int cap)
        {
            Id = id;
            Name = name;
            Cap = cap;
        }

        public string Id { get; }
        public string Name { get; }
        public int Cap { get; }
    }

    /// <summary>
    ///     Everything this mod reads from or writes to Tales from the Age of Men, through its own
    ///     service container. Every type is named as a string and resolved at runtime: the assembly
    ///     binds nothing of TAOM, so one build serves every download, and without the conversion each
    ///     lookup finds nothing and every call here is a no-op that answers empty.
    ///
    ///     Everything is read once per campaign; <see cref="Reset"/> drops the answers when a creation
    ///     session starts, because a career registry belongs to the campaign that loaded it.
    /// </summary>
    public static class TaomBridge
    {
        private const string Ns = "TAOM.";

        private static bool _probed;
        private static Type? _ioC;
        private static Assembly? _assembly;
        private static List<TaomCareer>? _careers;
        private static Dictionary<string, string>? _namedCompanions;
        private static readonly Dictionary<string, List<TaomCareerChoice>> ChoiceCache = new();

        /// <summary>True while Tales from the Age of Men is loaded beside this mod.</summary>
        public static bool IsLoaded => IoCType() != null;

        public static void Reset()
        {
            _probed = false;
            _ioC = null;
            _assembly = null;
            _careers = null;
            _namedCompanions = null;
            ChoiceCache.Clear();
        }

        private static Type? IoCType()
        {
            if (_probed) return _ioC;
            _probed = true;
#pragma warning disable BHA0003
            _ioC = AccessTools.TypeByName(Ns + "IoC");
#pragma warning restore BHA0003
            _assembly = _ioC?.Assembly;
            return _ioC;
        }

        /// <summary>The conversion's own singleton for an interface, or null when it is not loaded.</summary>
        private static object? Service(string interfaceName)
        {
            try
            {
                var ioC = IoCType();
                var contract = _assembly?.GetType(Ns + interfaceName);
                if (ioC == null || contract == null) return null;

                var resolve = ioC.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
                return resolve?.MakeGenericMethod(contract).Invoke(null, null);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"TaomBridge: resolving {interfaceName} failed: {ex.Message}");
                return null;
            }
        }

        private static object? Call(object? target, string method, params object?[] args)
        {
            if (target == null) return null;

            try
            {
                var types = new Type[args.Length];
                for (int i = 0; i < args.Length; i++) types[i] = args[i]?.GetType() ?? typeof(object);
                var info = AccessTools.Method(target.GetType(), method, types) ??
                           AccessTools.Method(target.GetType(), method);
                return info?.Invoke(target, args);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"TaomBridge: {method} failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     A string the conversion stores as its own localization token, read as the player sees
        ///     it. Its names arrive with the localization key still on the front, which is text only
        ///     once the game's text manager has resolved it; printing it raw puts the key on screen.
        /// </summary>
        private static string Localized(string raw) =>
            string.IsNullOrEmpty(raw) ? raw : new TextObject(raw).ToString();

        private static T Read<T>(object? source, string property, T fallback)
        {
            if (source == null) return fallback;
            var value = AccessTools.Property(source.GetType(), property)?.GetValue(source);
            return value is T typed ? typed : fallback;
        }

        private static List<string> ReadIds(object? source, string property)
        {
            var list = new List<string>();
            if (AccessTools.Property(source?.GetType(), property)?.GetValue(source) is IEnumerable values)
                foreach (var value in values)
                    if (value is string id)
                        list.Add(id);
            return list;
        }

        /// <summary>Every career the conversion declares, in its own order.</summary>
        public static IReadOnlyList<TaomCareer> Careers()
        {
            if (_careers != null) return _careers;
            _careers = new List<TaomCareer>();

            if (Call(Service("Features.CareerSystem.ICareerRegistry"), "GetAllCareers") is IEnumerable careers)
                foreach (var career in careers)
                    _careers.Add(new TaomCareer(
                        Read(career, "Id", string.Empty),
                        Localized(Read(career, "DisplayName", string.Empty)),
                        Localized(Read(career, "Description", string.Empty)),
                        Read(career, "MinClanTier", 0),
                        ReadIds(career, "EligibleCultureIds"),
                        ReadIds(career, "ChoiceGroupIds"),
                        Localized(Read(career, "Rank1Name", string.Empty))));

            CSLogger.Info($"TaomBridge: {_careers.Count} careers read from the conversion.");
            return _careers;
        }

        /// <summary>
        ///     The careers a character of this culture and clan tier may take. An empty culture list on
        ///     a career means every culture, which is the conversion's own reading of it.
        /// </summary>
        public static IReadOnlyList<TaomCareer> CareersFor(string? cultureId, int clanTier)
        {
            var offered = new List<TaomCareer>();
            if (string.IsNullOrEmpty(cultureId)) return offered;

            foreach (var career in Careers())
            {
                if (career.MinClanTier > clanTier) continue;
                if (career.CultureIds.Count > 0 &&
                    !career.CultureIds.Contains(cultureId!, StringComparer.OrdinalIgnoreCase))
                    continue;
                offered.Add(career);
            }

            return offered;
        }

        /// <summary>Every pick on a career's board, lowest tier first.</summary>
        public static IReadOnlyList<TaomCareerChoice> ChoicesFor(string careerId)
        {
            if (ChoiceCache.TryGetValue(careerId, out var cached)) return cached;

            var choices = new List<TaomCareerChoice>();
            var registry = Service("Features.CareerSystem.ICareerRegistry");
            var career = Careers().FirstOrDefault(c => c.Id == careerId);
            string rootId = Read(RawCareer(careerId), "RootChoiceId", string.Empty);

            if (registry != null && career != null)
                foreach (var groupId in career.GroupIds)
                {
                    var group = Call(registry, "GetGroup", groupId);
                    int tier = Read(group, "Tier", 1);
                    string groupName = Localized(Read(group, "DisplayName", string.Empty));

                    if (Call(registry, "GetChoicesForGroup", groupId) is IEnumerable groupChoices)
                        foreach (var choice in groupChoices)
                        {
                            string id = Read(choice, "Id", string.Empty);

                            // The career brings its own first pick, so offering it again spends a
                            // point on something the character already holds, and the board then
                            // comes up one short of what was chosen
                            if (string.Equals(id, rootId, StringComparison.Ordinal)) continue;

                            choices.Add(new TaomCareerChoice(id, groupName,
                                Localized(Read(choice, "Description", string.Empty)), tier));
                        }
                }

            choices.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            ChoiceCache[careerId] = choices;
            return choices;
        }

        /// <summary>What the career's own first pick does, which comes with the career.</summary>
        public static string RootChoiceDescription(string careerId)
        {
            var career = Careers().FirstOrDefault(c => c.Id == careerId);
            if (career == null) return string.Empty;

            var rootId = Read(RawCareer(careerId), "RootChoiceId", string.Empty);
            if (string.IsNullOrEmpty(rootId)) return string.Empty;

            var choice = Call(Service("Features.CareerSystem.ICareerRegistry"), "GetChoice", rootId);
            return Localized(Read(choice, "Description", string.Empty));
        }

        private static object? RawCareer(string careerId) =>
            Call(Service("Features.CareerSystem.ICareerRegistry"), "GetCareer", careerId);

        /// <summary>How many picks a character of this level may hold, the free first one included.</summary>
        public static int MaxCareerChoices(int heroLevel)
        {
            var registry = Service("Features.CareerSystem.ICareerRegistry");
            return Call(registry, "GetMaxChoicesForHero", heroLevel) is int max ? max : 0;
        }

        /// <summary>The level a tier of the board opens at.</summary>
        public static int TierUnlockLevel(int tier)
        {
            var registry = Service("Features.CareerSystem.ICareerRegistry");
            return Call(registry, "GetTierUnlockLevel", tier) is int level ? level : 1;
        }

        /// <summary>
        ///     Gives the hero the career and its first rank, exactly as the conversion's own career
        ///     menu does, then adds each further pick within what the level allows.
        /// </summary>
        public static void GrantCareer(string heroId, string careerId, IReadOnlyList<string> choiceIds, int heroLevel)
        {
            if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(careerId)) return;

            Call(Service("Features.CareerSystem.ICareerCreationHandler"), "OnCareerSelected", heroId, careerId);

            int max = MaxCareerChoices(heroLevel);
            var data = Service("Features.CareerSystem.ICareerDataService");
            string ownRoot = Read(RawCareer(careerId), "RootChoiceId", string.Empty);
            var held = new HashSet<string>(StringComparer.Ordinal) { ownRoot };
            int taken = 0;
            foreach (var choiceId in choiceIds)
            {
                if (!held.Add(choiceId)) continue;
                if (Call(data, "TryAddChoice", heroId, choiceId, max) is true) taken++;
            }

            CSLogger.Info($"TaomBridge: career {careerId} granted with {taken} of {choiceIds.Count} board picks (max {max}).");
        }

        /// <summary>The faction resource this culture deals in, or null where it has none.</summary>
        public static TaomResource? ResourceFor(string? cultureId)
        {
            if (string.IsNullOrEmpty(cultureId)) return null;

            var resource = Call(Service("Features.SpecialResources.ISpecialResourceConfigProvider"),
                "GetByCultureId", cultureId!);
            if (resource == null) return null;

            return new TaomResource(
                Read(resource, "Id", string.Empty),
                Localized(Read(resource, "DisplayName", string.Empty)),
                (int)Read(resource, "Cap", 0f));
        }

        /// <summary>Sets the hero's store of a faction resource to an exact amount.</summary>
        public static void GrantResource(string heroId, string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(resourceId)) return;

            Call(Service("Features.SpecialResources.ISpecialResourceStorageService"),
                "Set", heroId, resourceId, (float)amount);
            CSLogger.Info($"TaomBridge: {resourceId} set to {amount}.");
        }

        /// <summary>The side the conversion puts a culture on: Free, Evil or Neutral.</summary>
        public static string? SideOfCulture(string? cultureId)
        {
            if (string.IsNullOrEmpty(cultureId)) return null;
            return Call(Service("Features.Execution.IAlignmentService"), "GetCultureSide", cultureId!)?.ToString();
        }

        /// <summary>True where both cultures stand on the same side, or where the conversion is absent.</summary>
        public static bool SameSide(string? cultureA, string? cultureB)
        {
            var a = SideOfCulture(cultureA);
            var b = SideOfCulture(cultureB);
            return a == null || b == null || a == b;
        }

        /// <summary>The conversion's race id for a hero, or null where it does not track one.</summary>
        public static int? RaceOf(string heroId)
        {
            return Call(Service("Adapters.IHeroRosterAdapter"), "GetHeroRace", heroId) as int?;
        }

        /// <summary>Puts a hero on the same race as the player, so a generated family matches.</summary>
        public static void SetRace(string heroId, int race)
        {
            if (string.IsNullOrEmpty(heroId)) return;
            Call(Service("Adapters.IHeroRosterAdapter"), "SetHeroRace", heroId, race);
        }

        /// <summary>The character ids of the conversion's own named companions.</summary>
        public static IReadOnlyList<string> NamedCompanionIds() => new List<string>(NamedCompanionRaces().Keys);

        /// <summary>
        ///     Each named character the conversion places, against the race it declares for them. The
        ///     race is part of who they are: a dwarven smith rendered as a man is a broken character
        ///     rather than a variation, so nothing this mod does may decide a race for one of them.
        /// </summary>
        public static IReadOnlyDictionary<string, string> NamedCompanionRaces()
        {
            if (_namedCompanions != null) return _namedCompanions;
            _namedCompanions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (Call(Service("Features.NamedCompanions.INamedCompanionConfigProvider"), "GetCompanions")
                is IEnumerable companions)
                foreach (var companion in companions)
                    if (Read(companion, "Enabled", true))
                    {
                        string id = Read(companion, "CharacterId", string.Empty);
                        if (!string.IsNullOrEmpty(id))
                            _namedCompanions[id] = Read(companion, "Race", string.Empty);
                    }

            return _namedCompanions;
        }

        /// <summary>The conversion's own id for a race it names, or null where it knows none.</summary>
        public static int? RaceIdByName(string raceName)
        {
            if (string.IsNullOrEmpty(raceName)) return null;
            return Call(Service("Adapters.IRaceManager"), "GetRaceIdFromName", raceName) as int?;
        }
    }
}
