using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Fills the starting party from the culture's upgrade trees with a
    ///     role-balanced, tier-banded composition scaled by clan tier.
    /// </summary>
    public sealed class TroopStep : IStartStep
    {
        private enum TroopRole
        {
            Infantry,
            Archer,
            Cavalry,
            HorseArcher
        }

        // Target army composition by role
        private static readonly (TroopRole role, int pct)[] RoleTargets =
        {
            (TroopRole.Infantry, 40),
            (TroopRole.Archer, 35),
            (TroopRole.Cavalry, 15),
            (TroopRole.HorseArcher, 10)
        };

        public string Name => "Troops";

        public string? Validate(StartContext context)
        {
            return context.Hero.PartyBelongedTo == null
                ? "hero has no party"
                : null;
        }

        public void Apply(StartContext context)
        {
            var session = context.Session;
            int count = ResourceStep.EffectiveTroops(session, context.Settings);

            // The live party size limit is the ceiling, leaving room for the
            // player and the companions still to come
            try
            {
                var party = context.Hero.PartyBelongedTo;
                var model = Campaign.Current?.Models?.PartySizeLimitModel;
                if (party != null && model != null)
                {
                    int limit = (int)model.GetPartyMemberSizeLimit(party.Party).ResultNumber;
                    int room = limit - party.MemberRoster.TotalManCount - session.StartingCompanions
                               - FamilyAges.InPartyCount(session);
                    if (count > room)
                    {
                        CSLogger.Info($"TroopStep: requested {count} clamped to {room} (party limit {limit}).");
                        count = Math.Max(0, room);
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"TroopStep: party limit clamp failed: {ex.Message}");
            }

            if (count <= 0)
            {
                CSLogger.Info("TroopStep: no troops requested.");
                return;
            }

            switch (session.SelectedStartType)
            {
                case StartType.Outlaw:
                    AddBanditTroops(context.Hero, count, session.EffectiveClanTier);
                    break;
                case StartType.CaravanMaster:
                    AddCaravanGuards(context.Hero, count, session.EffectiveClanTier);
                    break;
                default:
                    AddTroops(context.Hero, count, session.EffectiveClanTier);
                    break;
            }
        }

        /// <summary>
        ///     Fills any hero's party with the same role-balanced, recruitable
        ///     composition the player gets; used for generated vassal lords.
        /// </summary>
        public static void FillPartyForHero(Hero hero, int count, int clanTier)
        {
            if (hero.PartyBelongedTo == null || count <= 0) return;
            AddTroops(hero.PartyBelongedTo, hero.Culture, count, clanTier);
        }

        /// <summary>
        ///     Fills any party's roster (a garrison included) with the same
        ///     role-balanced, recruitable composition.
        /// </summary>
        public static void FillRoster(TaleWorlds.CampaignSystem.Party.MobileParty party,
            CultureObject? culture, int count, int clanTier)
        {
            if (party == null || count <= 0) return;
            AddTroops(party, culture, count, clanTier);
        }

        private static void AddTroops(Hero hero, int count, int clanTier)
        {
            AddTroops(hero.PartyBelongedTo, hero.Culture, count, clanTier);
        }

        private static void AddTroops(TaleWorlds.CampaignSystem.Party.MobileParty party,
            CultureObject? culture, int count, int clanTier)
        {
            if (culture?.BasicTroop == null)
            {
                var fallback =
                    CharacterObject.All.FirstOrDefault(c => c.Occupation == Occupation.Soldier && !c.IsHero);
                if (fallback != null)
                {
                    party.MemberRoster.AddToCounts(fallback, count);
                    CSLogger.Info($"TroopStep: added {count}x {fallback.Name} (no culture troops).");
                }

                return;
            }

            // Collect troops from what the culture's settlements ACTUALLY offer:
            // the volunteer model's basic recruits and their promotion trees, so
            // troop-overhaul mods produce a party the player can really rebuild.
            // The culture's static trees are only the fallback.
            var allTroops = new List<CharacterObject>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in FindRecruitableRoots(culture))
                CollectTroopTree(root, allTroops, visited);

            if (allTroops.Count == 0)
            {
                CollectTroopTree(culture.BasicTroop, allTroops, visited);
                if (culture.EliteBasicTroop != null)
                    CollectTroopTree(culture.EliteBasicTroop, allTroops, visited);
            }

            // Classify troops by (role, tier)
            var pool = new Dictionary<TroopRole, Dictionary<int, List<CharacterObject>>>();
            foreach (TroopRole role in Enum.GetValues(typeof(TroopRole)))
                pool[role] = new Dictionary<int, List<CharacterObject>>();

            foreach (var troop in allTroops)
            {
                var role = ClassifyTroop(troop);
                var tier = troop.Tier;
                if (!pool[role].ContainsKey(tier))
                    pool[role][tier] = new List<CharacterObject>();
                pool[role][tier].Add(troop);
            }

            var tierDist = BuildTierDistribution(clanTier, TopTierAvailable(allTroops, clanTier));
            var totalAdded = 0;

            foreach (var (role, pct) in RoleTargets)
            {
                var roleCount = (int)Math.Round(count * pct / 100.0);
                var roleTroops = pool[role];

                // If no troops exist for this role, redistribute to Infantry
                if (roleTroops.Count == 0)
                {
                    roleTroops = pool[TroopRole.Infantry];
                    if (roleTroops.Count == 0)
                    {
                        party.MemberRoster.AddToCounts(culture.BasicTroop, roleCount);
                        totalAdded += roleCount;
                        continue;
                    }
                }

                var roleAdded = 0;
                foreach (var kvp in tierDist.OrderByDescending(k => k.Key))
                {
                    var tier = kvp.Key;
                    var tierPct = kvp.Value;
                    var tierCount = (int)Math.Round(roleCount * tierPct / 100.0);
                    tierCount = Math.Min(tierCount, roleCount - roleAdded);
                    if (tierCount <= 0) continue;

                    var candidates = FindNearestCandidates(roleTroops, tier);
                    if (candidates == null || candidates.Count == 0) continue;

                    var troop = candidates[CSRandom.Next(candidates.Count)];
                    party.MemberRoster.AddToCounts(troop, tierCount);
                    roleAdded += tierCount;
                    totalAdded += tierCount;
                }

                // Rounding remainder goes to the lowest available tier
                if (roleAdded < roleCount)
                {
                    var remaining = roleCount - roleAdded;
                    var lowestTier = roleTroops.Keys.DefaultIfEmpty(0).Min();
                    if (roleTroops.TryGetValue(lowestTier, out var lowCandidates) && lowCandidates.Count > 0)
                    {
                        var troop = lowCandidates[CSRandom.Next(lowCandidates.Count)];
                        party.MemberRoster.AddToCounts(troop, remaining);
                        totalAdded += remaining;
                    }
                }
            }

            // Final safety net for any total rounding shortfall
            if (totalAdded < count)
            {
                party.MemberRoster.AddToCounts(culture.BasicTroop, count - totalAdded);
                totalAdded = count;
            }

            CSLogger.Info($"TroopStep: added {totalAdded} troops. Party size: {party.MemberRoster.TotalManCount}.");
        }

        /// <summary>
        ///     An outlaw band: mostly bandits, some raiders, a few chiefs. A culture
        ///     with no outlaws of its own falls back to regulars, and they are the
        ///     regulars the player's own standing pays for: falling back is no reason
        ///     to hand a high-standing rogue a column of the rawest recruits alive.
        /// </summary>
        private static void AddBanditTroops(Hero hero, int count, int clanTier)
        {
            var party = hero.PartyBelongedTo;
            var culture = hero.Culture;

            var bandit = culture?.BanditBandit;
            var raider = culture?.BanditRaider;
            var chief = culture?.BanditChief;

            if (bandit == null && raider == null && chief == null)
            {
                CSLogger.Warn("TroopStep: no bandit troops for culture; falling back to regulars.");
                AddTroops(hero, count, clanTier);
                return;
            }

            int chiefs = chief != null ? Math.Max(0, count / 10) : 0;
            int raiders = raider != null ? Math.Max(0, count * 3 / 10) : 0;
            int bandits = count - chiefs - raiders;

            if (bandit != null && bandits > 0) party.MemberRoster.AddToCounts(bandit, bandits);
            else if (raider != null) raiders += bandits;

            if (raider != null && raiders > 0) party.MemberRoster.AddToCounts(raider, raiders);
            if (chief != null && chiefs > 0) party.MemberRoster.AddToCounts(chief, chiefs);

            CSLogger.Info($"TroopStep: outlaw band of {count} added ({bandits}/{raiders}/{chiefs}).");
        }

        /// <summary>
        ///     A caravan escort from the culture's caravan guard troop, falling back
        ///     to the regulars the player's standing pays for where the culture has
        ///     no guard of its own.
        /// </summary>
        private static void AddCaravanGuards(Hero hero, int count, int clanTier)
        {
            var party = hero.PartyBelongedTo;
            var guard = hero.Culture?.CaravanGuard;

            if (guard == null)
            {
                CSLogger.Warn("TroopStep: no caravan guard for culture; falling back to regulars.");
                AddTroops(hero, count, clanTier);
                return;
            }

            party.MemberRoster.AddToCounts(guard, count);
            CSLogger.Info($"TroopStep: {count}x {guard.Name} caravan guards added.");
        }

        /// <summary>
        ///     The basic volunteers the culture's settlements actually produce,
        ///     asked of the installed volunteer model per notable, so a modded
        ///     recruitment overhaul defines the starting party's lineage.
        /// </summary>
        private static HashSet<CharacterObject> FindRecruitableRoots(CultureObject culture)
        {
            var roots = new HashSet<CharacterObject>();
            try
            {
                var model = Campaign.Current?.Models?.VolunteerModel;
                if (model == null) return roots;

                foreach (var settlement in TaleWorlds.CampaignSystem.Settlements.Settlement.All)
                {
                    if (settlement.Culture != culture) continue;
                    if (!settlement.IsTown && !settlement.IsVillage) continue;

                    foreach (var notable in settlement.Notables)
                    {
                        var basic = model.GetBasicVolunteer(notable);
                        if (basic != null)
                            roots.Add(basic);
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"TroopStep: recruitable-roots lookup failed: {ex.Message}");
            }

            return roots;
        }

        private static void CollectTroopTree(CharacterObject? root, List<CharacterObject> troops,
            HashSet<string> visited)
        {
            if (root == null || string.IsNullOrEmpty(root.StringId) || !visited.Add(root.StringId))
                return;

            if (!root.IsHero && root.Occupation == Occupation.Soldier)
                troops.Add(root);

            foreach (var upgrade in root.UpgradeTargets)
                CollectTroopTree(upgrade, troops, visited);
        }

        private static TroopRole ClassifyTroop(CharacterObject troop)
        {
            var formation = troop.DefaultFormationClass;
            if (formation == FormationClass.HorseArcher) return TroopRole.HorseArcher;
            if (formation == FormationClass.Cavalry) return TroopRole.Cavalry;
            if (formation == FormationClass.Ranged) return TroopRole.Archer;
            return TroopRole.Infantry;
        }

        /// <summary>
        ///     The highest tier these troop trees actually reach, which is the ceiling
        ///     on what any standing can buy. Read off the trees rather than written
        ///     down, because the base game's six is the base game's answer: a troop
        ///     overhaul that promotes further is reached by a clan standing high
        ///     enough for it, and one that stops short never has a tier invented for
        ///     it. An empty pool answers with the standing, which is what the caller
        ///     falls back to filling anyway.
        /// </summary>
        private static int TopTierAvailable(List<CharacterObject> troops, int clanTier) =>
            troops.Count == 0 ? clanTier : troops.Max(troop => troop.Tier);

        /// <summary>
        ///     Builds a tier percentage distribution based on clan tier.
        ///     Bannerlord troops start at T1 (no T0 exists). Highest accessible tier is
        ///     the standing, or the best the trees hold where that is lower.
        ///     Top 3 tiers get capped: Top: 10%, Top-1: 7.5%, Top-2: 5%.
        ///     Remaining percentage is split evenly among lower tiers (T1+).
        /// </summary>
        private static Dictionary<int, double> BuildTierDistribution(int clanTier, int topTierAvailable)
        {
            const int minTier = 1;
            var maxTier = Math.Min(Math.Max(clanTier, minTier), Math.Max(minTier, topTierAvailable));
            var dist = new Dictionary<int, double>();

            if (maxTier <= minTier)
            {
                dist[minTier] = 100.0;
                return dist;
            }

            double[] eliteCaps = { 10.0, 7.5, 5.0 };
            double eliteTotal = 0;

            for (var i = 0; i < eliteCaps.Length; i++)
            {
                var tier = maxTier - i;
                if (tier < minTier) break;
                dist[tier] = eliteCaps[i];
                eliteTotal += eliteCaps[i];
            }

            var remaining = 100.0 - eliteTotal;
            var lowestEliteTier = Math.Max(minTier, maxTier - 2);
            var lowerTierCount = lowestEliteTier - minTier;

            if (lowerTierCount > 0)
            {
                var each = remaining / lowerTierCount;
                for (var t = minTier; t < lowestEliteTier; t++)
                    dist[t] = each;
            }
            else
            {
                dist[minTier] = dist.ContainsKey(minTier) ? dist[minTier] + remaining : remaining;
            }

            return dist;
        }

        /// <summary>
        ///     Finds candidates at the target tier, or falls to the nearest available
        ///     tier, searching downward first, then upward.
        /// </summary>
        private static List<CharacterObject>? FindNearestCandidates(
            Dictionary<int, List<CharacterObject>> roleTroops, int targetTier)
        {
            if (roleTroops.TryGetValue(targetTier, out var exact) && exact.Count > 0)
                return exact;

            for (var offset = 1; offset <= 6; offset++)
            {
                if (roleTroops.TryGetValue(targetTier - offset, out var lower) && lower.Count > 0)
                    return lower;
                if (roleTroops.TryGetValue(targetTier + offset, out var upper) && upper.Count > 0)
                    return upper;
            }

            return null;
        }
    }
}
