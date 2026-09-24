using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Every editor limit, read from the running game's models instead of
    ///     hard-coded numbers, so mods that change a cap are honored
    ///     automatically. Probed values are cached per campaign session; values
    ///     with no in-game model get generous engine-safe ceilings.
    /// </summary>
    public static class GameCaps
    {
        public const int MaxGold = 1000000000;
        public const int MaxInfluence = 1000000;
        public const int MaxTroops = 5000;

        // Settings sliders span these instead of the absolute ceilings above,
        // which no shipped default comes close to: on a 0 to one billion track
        // a 500 denar default sits at 0.00005 percent and the slider cannot
        // express its own value. The exact pickers still allow the real maximum.
        public const int SettingsMaxGold = 10000000;
        public const int SettingsMaxInfluence = 10000;

        private static int? _maxSkillLevel;
        private static int? _maxHeroLevel;

        /// <summary>
        ///     A bound that could not be read from the running game falls back to a fixed number.
        ///     That is the right behavior, but a silent fallback is a wrong ceiling nobody can see:
        ///     it offers the player something the start cannot deliver, which is exactly what made
        ///     the companion-limit disagreement hard to place. Reported once per bound so a log
        ///     shows it without repeating every time a menu redraws.
        /// </summary>
        private static readonly HashSet<string> _reportedFallbacks = new();

        private static int Fallback(string bound, int value, Exception? ex = null)
        {
            if (_reportedFallbacks.Add(bound))
                CSLogger.Warn($"GameCaps: {bound} could not be read from the game; using {value}." +
                              (ex != null ? $" ({ex.GetType().Name}: {ex.Message})" : string.Empty));
            return value;
        }

        /// <summary>
        ///     Every bound this class hands a menu, written down with the inputs it was computed
        ///     from. Deduplicated on bound, inputs and answer together, so a value that never
        ///     changes is logged once however many times a tab redraws, and a value that moves is
        ///     logged again the moment it does.
        ///
        ///     The point is that a number the player disputes can be settled from the log instead
        ///     of from memory: a companion offer of five is either the clan tier model answering
        ///     for tier 2, which is correct, or something else, and only the log can say which.
        /// </summary>
        private static readonly HashSet<string> _reportedValues = new();

        private static int Report(string bound, int value, string? inputs = null)
        {
            if (_reportedValues.Add($"{bound}|{inputs}|{value}"))
                CSLogger.Info($"GameCaps: {bound} = {value}" +
                              (inputs != null ? $" [{inputs}]" : string.Empty));
            return value;
        }

        private static bool Report(string bound, bool value, string? inputs = null)
        {
            if (_reportedValues.Add($"{bound}|{inputs}|{value}"))
                CSLogger.Info($"GameCaps: {bound} = {value}" +
                              (inputs != null ? $" [{inputs}]" : string.Empty));
            return value;
        }

        public static int MaxAttribute()
        {
            try
            {
                return Report(nameof(MaxAttribute), Campaign.Current?.Models?.CharacterDevelopmentModel?.MaxAttribute ?? 10);
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxAttribute), 10, ex);
            }
        }

        public static int MaxFocus()
        {
            try
            {
                return Report(nameof(MaxFocus), Campaign.Current?.Models?.CharacterDevelopmentModel?.MaxFocusPerSkill ?? 5);
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxFocus), 5, ex);
            }
        }

        /// <summary>The model-driven coming-of-age year; also the fertility floor.</summary>
        public static int MinAdultAge()
        {
            try
            {
                return Report(nameof(MinAdultAge), Campaign.Current?.Models?.AgeModel?.HeroComesOfAge ?? 18);
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MinAdultAge), 18, ex);
            }
        }

        public static int MaxAge()
        {
            try
            {
                return Report(nameof(MaxAge), Campaign.Current?.Models?.AgeModel?.MaxAge ?? 128);
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxAge), 128, ex);
            }
        }

        public static int MaxClanTier()
        {
            try
            {
                return Report(nameof(MaxClanTier), Campaign.Current?.Models?.ClanTierModel?.MaxClanTier ?? 6);
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxClanTier), 6, ex);
            }
        }

        /// <summary>
        ///     Twice the renown the game demands for its highest clan tier, so the
        ///     slider covers max tier with headroom under any renown model.
        /// </summary>
        public static int MaxRenown()
        {
            try
            {
                var model = Campaign.Current?.Models?.ClanTierModel;
                if (model == null) return 10000;
                return Report(nameof(MaxRenown), Math.Max(10000, model.GetRequiredRenownForTier(model.MaxClanTier) * 2));
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxRenown), 10000, ex);
            }
        }

        /// <summary>
        ///     The clan tier the given renown actually earns, asked of the installed
        ///     clan tier model so renown mods answer for themselves. The game derives
        ///     the tier from renown, so a start granted renown for tier 6 arrives at
        ///     tier 6 whatever band the purse chapter picked.
        /// </summary>
        public static int TierForRenown(int renown)
        {
            try
            {
                var model = Campaign.Current?.Models?.ClanTierModel;
                if (model == null) return 0;

                for (int tier = model.MaxClanTier; tier >= model.MinClanTier; tier--)
                    if (renown >= model.GetRequiredRenownForTier(tier))
                        return Report(nameof(TierForRenown), tier, $"renown {renown}");

                return Report(nameof(TierForRenown), model.MinClanTier, $"renown {renown}");
            }
            catch (Exception ex)
            {
                return Fallback(nameof(TierForRenown), 0, ex);
            }
        }

        /// <summary>
        ///     The companion limit the clan will actually have at the chosen tier,
        ///     asked of the installed clan tier model under the character the
        ///     start will actually produce (tier, perks, and skills probed in,
        ///     then restored), so any mod's own limit formula answers exactly.
        /// </summary>
        public static int MaxCompanions(int selectedClanTier,
            CharacterCreation.Session.CharacterCreationSession? session = null)
        {
            string signature = ProbeSignature(selectedClanTier, session);
            if (_companionLimitCache.TryGetValue(signature, out int cached))
                return cached;

            int result = Math.Max(1, ProbeWithPlannedCharacter(selectedClanTier, session, () =>
            {
                var model = Campaign.Current?.Models?.ClanTierModel;
                var clan = Clan.PlayerClan;
                return model != null && clan != null ? model.GetCompanionLimit(clan) : 10;
            }, 10));
            _companionLimitCache[signature] = result;
            return Report(nameof(MaxCompanions), result, $"clan tier {selectedClanTier}");
        }

        /// <summary>
        ///     The party size limit the start will actually have, from the
        ///     installed party size model under the planned tier, perks, and
        ///     skills. Cached per planning state, so the number only ever moves
        ///     when something that truly feeds it changes.
        /// </summary>
        public static int MaxTroopsLive(int selectedClanTier,
            CharacterCreation.Session.CharacterCreationSession? session = null)
        {
            string signature = ProbeSignature(selectedClanTier, session);
            if (_partyLimitCache.TryGetValue(signature, out int cached))
                return cached;

            int result = Math.Max(1, ProbeWithPlannedCharacter(selectedClanTier, session, () =>
            {
                var model = Campaign.Current?.Models?.PartySizeLimitModel;
                var party = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty?.Party;
                if (model == null || party == null) return MaxTroops;
                return (int)model.GetPartyMemberSizeLimit(party).ResultNumber;
            }, MaxTroops));
            _partyLimitCache[signature] = result;
            return Report(nameof(MaxTroopsLive), result, $"clan tier {selectedClanTier}");
        }

        private static readonly Dictionary<string, int> _companionLimitCache = new();
        private static readonly Dictionary<string, int> _partyLimitCache = new();

        /// <summary>
        ///     Everything the probe's answer can depend on: the tier, the mode,
        ///     the custom skill levels, and the perk configuration. Identical
        ///     signatures always return the first probe's answer, which keeps the
        ///     ceilings rock steady across tab switches and stepper spam.
        /// </summary>
        private static string ProbeSignature(int tier,
            CharacterCreation.Session.CharacterCreationSession? session)
        {
            if (session == null) return $"t{tier}";

            var skills = string.Join(",", session.CustomSkillLevels
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}:{p.Value}"));
            var perks = string.Join(",", session.CustomPerks
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={string.Join("|", p.Value)}"));
            return $"t{tier}|m{session.Mode}|s{skills}|p{perks}";
        }

        /// <summary>
        ///     Runs one read against the game's models with the player probed into
        ///     the state the start will produce: the chosen clan tier, the planned
        ///     perks, and the expected skill levels. Everything is restored in
        ///     reverse before returning, and during creation the hero carries no
        ///     perks yet, so the perk grant-and-clear is lossless.
        /// </summary>
        private static int ProbeWithPlannedCharacter(int selectedClanTier,
            CharacterCreation.Session.CharacterCreationSession? session, Func<int> read, int fallback)
        {
            try
            {
                var clan = Clan.PlayerClan;
                var hero = Hero.MainHero;
                var tierModel = Campaign.Current?.Models?.ClanTierModel;
                if (clan == null || hero?.HeroDeveloper == null || tierModel == null) return fallback;

                var tierField = HarmonyLib.AccessTools.Field(typeof(Clan), "_tier");
                var clearPerks = HarmonyLib.AccessTools.Method(typeof(Hero), "ClearPerks");
                if (tierField == null) return read();

                int probeTier = Math.Max(tierModel.MinClanTier,
                    Math.Min(tierModel.MaxClanTier, selectedClanTier));
                object? savedTier = tierField.GetValue(clan);
                bool perksApplied = false;
                var savedSkills = new List<(TaleWorlds.Core.SkillObject Skill, int Value)>();
                try
                {
                    tierField.SetValue(clan, probeTier);

                    if (session != null)
                    {
                        foreach (var skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                        {
                            int expected = Application.Steps.NarrativeStep.ExpectedSkillValue(session, skill);
                            int current = hero.GetSkillValue(skill);
                            if (expected == current) continue;
                            savedSkills.Add((skill, current));
                            hero.HeroDeveloper.SetInitialSkillLevel(skill, expected);
                        }

                        if (clearPerks != null)
                            foreach (var perk in PerkPlanner.Plan(hero, session))
                            {
                                hero.HeroDeveloper.AddPerk(perk);
                                perksApplied = true;
                            }
                    }

                    return read();
                }
                finally
                {
                    if (perksApplied)
                        clearPerks!.Invoke(hero, null);
                    foreach (var (skill, value) in savedSkills)
                        hero.HeroDeveloper.SetInitialSkillLevel(skill, value);
                    tierField.SetValue(clan, savedTier);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCaps: planned-character probe failed: {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        ///     The last skill level whose experience requirement still grows; the
        ///     default model plateaus at 1024, and mods that extend the table are
        ///     followed automatically.
        /// </summary>
        public static int MaxSkillLevel()
        {
            if (_maxSkillLevel.HasValue) return _maxSkillLevel.Value;

            int result = 1024;
            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model != null)
                {
                    int level = 1;
                    while (level < 8192 &&
                           model.GetXpRequiredForSkillLevel(level + 1) > model.GetXpRequiredForSkillLevel(level))
                        level++;
                    result = Math.Max(330, level);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCaps: skill level probe failed, using {result}: {ex.Message}");
            }

            _maxSkillLevel = result;
            return Report(nameof(MaxSkillLevel), result);
        }

        /// <summary>
        ///     The last character level the development model's requirement table
        ///     defines, probed so level-cap mods are honored.
        /// </summary>
        public static int MaxHeroLevel()
        {
            if (_maxHeroLevel.HasValue) return _maxHeroLevel.Value;

            int result = 62;
            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model != null)
                {
                    int cap = model.GetMaxSkillPoint();
                    int level = 1;
                    while (level < 4096 && model.SkillsRequiredForLevel(level + 1) < cap)
                        level++;
                    result = Math.Max(1, level);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCaps: hero level probe failed, using {result}: {ex.Message}");
            }

            _maxHeroLevel = result;
            return Report(nameof(MaxHeroLevel), result);
        }

        /// <summary>
        ///     What the game's own model would offer the player clan to fight
        ///     for this realm: the live default for a composed contract.
        /// </summary>
        public static int ContractPayOffer(TaleWorlds.CampaignSystem.Kingdom? kingdom)
        {
            try
            {
                if (kingdom == null || Clan.PlayerClan == null)
                    return Report(nameof(ContractPayOffer), 50, "no realm chosen");
                return Report(nameof(ContractPayOffer),
                    Math.Max(1, Campaign.Current?.Models?.MinorFactionsModel?
                        .GetMercenaryAwardFactorToJoinKingdom(Clan.PlayerClan, kingdom) ?? 50),
                    kingdom.Name?.ToString());
            }
            catch (Exception ex)
            {
                return Fallback(nameof(ContractPayOffer), 50, ex);
            }
        }

        /// <summary>Four times the model's live offer: room to compose a princely contract.</summary>
        public static int ContractPayCeiling(TaleWorlds.CampaignSystem.Kingdom? kingdom)
        {
            return Report(nameof(ContractPayCeiling), Math.Max(200, ContractPayOffer(kingdom) * 4),
                kingdom?.Name?.ToString() ?? "no realm chosen");
        }

        /// <summary>
        ///     The garrison size the game's party model allows: the chosen
        ///     holding's own limit, or the largest limit any live garrison has
        ///     when the holding is still undecided.
        /// </summary>
        public static int MaxGarrison(TaleWorlds.CampaignSystem.Settlements.Settlement? holding,
            TaleWorlds.CampaignSystem.Kingdom? grantingRealm = null)
        {
            try
            {
                var model = Campaign.Current?.Models?.PartySizeLimitModel;
                if (model == null) return 0;

                var chosen = holding?.Parties?.FirstOrDefault(p => p.IsGarrison);
                if (chosen != null)
                    return Report(nameof(MaxGarrison),
                        Math.Max(0, (int)model.GetPartyMemberSizeLimit(chosen.Party).ResultNumber),
                        holding?.Name?.ToString());

                // No holding chosen yet. Offering the largest garrison in the world is a number the
                // start usually cannot deliver: it grants one specific fief, and a player who asked
                // for 700 into a fief holding 499 got exactly that. When the realm IS known, the
                // offer is the SMALLEST of its holdings, which whatever it grants can always hold.
                // Choosing a fief explicitly still unlocks that fief's own real ceiling.
                var candidates = TaleWorlds.CampaignSystem.Settlements.Settlement.All
                    .Where(s => (s.IsTown || s.IsCastle) &&
                                (grantingRealm == null || s.OwnerClan?.Kingdom == grantingRealm))
                    .Select(s => s.Parties?.FirstOrDefault(p => p.IsGarrison))
                    .Where(g => g != null)
                    .Select(g => (int)model.GetPartyMemberSizeLimit(g!.Party).ResultNumber)
                    .ToList();

                if (candidates.Count == 0)
                    return Report(nameof(MaxGarrison), 0, "no garrison to size against");

                return grantingRealm == null
                    ? Report(nameof(MaxGarrison), candidates.Max(), "no holding chosen; largest anywhere")
                    : Report(nameof(MaxGarrison), candidates.Min(),
                        $"any holding of {grantingRealm.Name}; smallest is deliverable");
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxGarrison), 0, ex);
            }
        }

        /// <summary>The workshop cap the game itself imposes at the chosen tier.</summary>
        public static int MaxWorkshops(int selectedClanTier)
        {
            try
            {
                return Report(nameof(MaxWorkshops),
                    Math.Max(0,
                        Campaign.Current?.Models?.WorkshopModel?.GetMaxWorkshopCountForClanTier(selectedClanTier)
                        ?? 1),
                    $"clan tier {selectedClanTier}");
            }
            catch (Exception ex)
            {
                return Fallback(nameof(MaxWorkshops), 1, ex);
            }
        }

        /// <summary>
        ///     True when the installed marriage model would marry someone who is
        ///     already married: the definition of a polygamy mod. Probed against a
        ///     real married hero, so any mod's model answers for itself.
        /// </summary>
        public static bool AllowsPolygamy()
        {
            if (_allowsPolygamy.HasValue) return _allowsPolygamy.Value;

            bool result = false;
            try
            {
                var model = Campaign.Current?.Models?.MarriageModel;
                if (model != null)
                {
                    Hero? married = null;
                    foreach (var hero in Hero.AllAliveHeroes)
                        if (hero.Spouse != null && hero.IsLord)
                        {
                            married = hero;
                            break;
                        }

                    if (married != null)
                        result = model.IsSuitableForMarriage(married);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCaps: polygamy probe failed: {ex.Message}");
            }

            _allowsPolygamy = result;
            return Report(nameof(AllowsPolygamy), result);
        }

        /// <summary>
        ///     True when a loaded module advertises taking both perks of a pair.
        ///     Those mods patch the perk UI rather than any model, so the only
        ///     signal is the module list itself.
        /// </summary>
        public static bool AllowsBothPerks()
        {
            if (_allowsBothPerks.HasValue) return _allowsBothPerks.Value;

            bool result = false;
            try
            {
                foreach (var moduleName in TaleWorlds.Engine.Utilities.GetModulesNames())
                    if (System.Text.RegularExpressions.Regex.IsMatch(moduleName,
                            "(both|dual|double|all).{0,12}perk|perk.{0,12}(both|dual|double|all)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        result = true;
                        break;
                    }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCaps: both-perks probe failed: {ex.Message}");
            }

            _allowsBothPerks = result;
            return Report(nameof(AllowsBothPerks), result);
        }

        /// <summary>Clears cached probes; call when a campaign session begins.</summary>
        public static void Reset()
        {
            _maxSkillLevel = null;
            _maxHeroLevel = null;
            _allowsPolygamy = null;
            _allowsBothPerks = null;
            _companionLimitCache.Clear();
            _partyLimitCache.Clear();

            // A new creation session logs its own bounds from scratch, so one session's log
            // never leans on what an earlier one happened to print.
            _reportedValues.Clear();
            _reportedFallbacks.Clear();
        }

        private static bool? _allowsPolygamy;
        private static bool? _allowsBothPerks;
    }
}
