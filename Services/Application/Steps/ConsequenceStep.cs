using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Applies the visible traces of the life that was told: personality trait
    ///     leanings, starting relations with the right people, what the life put in
    ///     the character's hands, and what it left them owing. Runs after
    ///     LocationStep so relations can anchor to the actual starting town.
    ///
    ///     Allies, places, titles and lost years are read from the same outcome by
    ///     the steps that own them, and are deliberately untouched here.
    ///
    ///     The figures and the resolutions below are internal rather than private
    ///     because the effect panel has to state what this step will do before it
    ///     does it. A second copy of any of them in the panel would drift, and the
    ///     first drift would have the panel promise one item while the game handed
    ///     over another, which is the failure the panel exists to prevent. There is
    ///     one copy, it lives here beside the code that applies it, and the panel
    ///     reads it.
    /// </summary>
    public sealed class ConsequenceStep : IStartStep
    {
        /// <summary>What one step of goodwill or enmity moves a relation by.</summary>
        internal const int RelationBonus = 5;

        /// <summary>The fewest of a group any impression reaches.</summary>
        internal const int MaxRelationTargets = 2;

        /// <summary>What the game files a mount under when it is ridden to war.</summary>
        private static readonly string[] WarMounts = { "war_horse", "noble_horse" };

        private static readonly string[] RidingMounts = { "horse" };

        private static readonly string[] PackAnimals = { "sumpter_horse" };

        public string Name => "Consequences";

        public string? Validate(StartContext context) => null;

        public void Apply(StartContext context)
        {
            if (context.Session.Mode == SetupMode.LifePath)
            {
                ApplyChapters(context);
                return;
            }

            // A run that answered no scenes is the custom route, and its outcome is
            // empty rather than absent, so nothing below has to ask which route it
            // is on. The player's own trait settings apply either way.
            SceneOutcome outcome;
            try
            {
                outcome = GuidedRun.Outcome();
            }
            catch (Exception ex)
            {
                // An unreadable life still leaves a character who can be played
                CSLogger.Error("ConsequenceStep: reading the guided run failed.", ex);
                outcome = SceneOutcome.From(null);
            }

            ApplyTraits(context, outcome.Traits);
            ApplyRelations(context, outcome.Relations);

            // Items before the debt, so a purse the life handed over can be what
            // pays down what the life borrowed
            ApplyItems(context, outcome.Items);
            ApplyDebt(context, outcome.Debt);

            // Both routes: a clan head with a blank encyclopedia entry is the plainest
            // sign the character was made rather than found. Of the base game's 397
            // lords the 27 that carry prose are all clan heads, so this goes here and
            // nowhere else in the family
            ApplyEncyclopediaEntry(context);
        }

        private static void ApplyEncyclopediaEntry(StartContext context)
        {
            try
            {
                var entry = HeroLore.ComposeForPlayer(context.Session, context.Hero);
                if (entry == null) return;

                context.Hero.EncyclopediaText = entry;
            }
            catch (Exception ex)
            {
                CSLogger.Error("ConsequenceStep: writing the encyclopedia entry failed.", ex);
            }
        }

        private static void ApplyTraits(StartContext context, IReadOnlyDictionary<string, int> leanings)
        {
            var totals = new Dictionary<TraitObject, int>();
            foreach (var leaning in leanings)
            {
                var trait = ChoiceEffects.ResolveTrait(leaning.Key);
                if (trait == null) continue;
                totals.TryGetValue(trait, out var current);
                totals[trait] = current + leaning.Value;
            }

            foreach (var pair in totals)
            {
                // Traits accumulate across the whole life rather than latching at the first
                // choice that moves them: a character who answered honestly at four turns is more
                // honest than one who did it once. The bound is the trait's own declared range,
                // read from the live object rather than assumed, and opposite choices still cancel
                // to nothing, which is the point.
                int level = Math.Max(pair.Key.MinValue, Math.Min(pair.Key.MaxValue, pair.Value));
                if (level == 0) continue;

                context.Hero.SetTraitLevel(pair.Key, level);
                CSLogger.Info($"ConsequenceStep: trait {pair.Key.StringId} set to {level}.");
            }

            ApplyCustomTraits(context);
        }

        /// <summary>Player-set trait levels, which override every leaning a life gave.</summary>
        private static void ApplyCustomTraits(StartContext context) =>
            WriteTraits(context.Hero, context.Session.CustomTraits);

        /// <summary>
        ///     The personality traits the Start Editor sets, for the player and a
        ///     generated hero alike. Built on each call because the trait objects
        ///     exist only once the game has registered them.
        /// </summary>
        public static TraitObject[] EditableTraits() => new[]
        {
            DefaultTraits.Mercy, DefaultTraits.Valor, DefaultTraits.Honor,
            DefaultTraits.Generosity, DefaultTraits.Calculating
        };

        /// <summary>Exact trait levels, written as the level itself rather than a leaning added.</summary>
        public static void WriteTraits(Hero hero, IReadOnlyDictionary<string, int> levels)
        {
            var knownTraits = EditableTraits();
            foreach (var pair in levels)
            {
                var trait = knownTraits.FirstOrDefault(t => t.StringId == pair.Key);
                if (trait == null) continue;

                // The trait's own declared range, the same bound the life-path
                // leanings above are held to. Typed as -2 to 2 it was the base
                // game's range written down twice, and an overhaul that widens a
                // trait would have had the editor's exact value silently trimmed
                // to a level the player never set.
                int level = Math.Max(trait.MinValue, Math.Min(trait.MaxValue, pair.Value));
                hero.SetTraitLevel(trait, level);
                CSLogger.Info($"ConsequenceStep: {hero.Name}'s trait {trait.StringId} set to {level}.");
            }
        }

        /// <summary>
        ///     Cultured Start's traces of a life: each answered chapter's trait
        ///     leaning, the people it left an impression on, and the keepsake it
        ///     put in the stores, exactly as that route has always granted them.
        /// </summary>
        private static void ApplyChapters(StartContext context)
        {
            var selected = new List<LifePathChoice>();
            foreach (var menu in LifePathCatalog.BuildMenus())
            foreach (var choice in menu.Choices)
                if (choice.IsSelected(context.Session))
                    selected.Add(choice);

            var totals = new Dictionary<TraitObject, int>();
            foreach (var choice in selected)
            foreach (var (traitId, delta) in choice.Traits)
            {
                var trait = LifePathCatalog.ResolveTrait(traitId);
                if (trait == null) continue;
                totals.TryGetValue(trait, out var current);
                totals[trait] = current + delta;
            }

            // One step either way however many chapters lean the same way: the
            // chapters describe a disposition, not a lifetime's practice of it
            foreach (var pair in totals)
            {
                int level = Math.Max(-1, Math.Min(1, pair.Value));
                if (level == 0) continue;

                context.Hero.SetTraitLevel(pair.Key, level);
                CSLogger.Info($"ConsequenceStep: trait {pair.Key.StringId} set to {level}.");
            }

            ApplyCustomTraits(context);

            foreach (var effect in selected.Select(c => c.Relations).Where(r => r != RelationEffect.None).Distinct())
            {
                try
                {
                    foreach (var target in FindRelationTargets(context, effect, MaxRelationTargets)
                                 .Take(MaxRelationTargets))
                    {
                        ChangeRelationAction.ApplyPlayerRelation(target, RelationBonus, false, false);
                        CompanionGenerator.MarkMet(target, context.Session);
                        CSLogger.Info($"ConsequenceStep: +{RelationBonus} relation with {target.Name} ({effect}), met.");
                    }
                }
                catch (Exception ex)
                {
                    CSLogger.Error($"ConsequenceStep: applying {effect} relations failed.", ex);
                }
            }

            var party = context.Hero.PartyBelongedTo;
            if (party == null) return;

            foreach (var choice in selected)
            {
                if (choice.Heirloom == HeirloomKind.None) continue;

                var item = ResolveHeirloom(choice.Heirloom, context);
                if (item == null)
                {
                    CSLogger.Warn($"ConsequenceStep: no item found for heirloom {choice.Heirloom}.");
                    continue;
                }

                party.ItemRoster.AddToCounts(new EquipmentElement(item), 1);
                CSLogger.Info($"ConsequenceStep: heirloom {item.Name} added to inventory.");
            }
        }

        /// <summary>
        ///     A keepsake as Cultured Start resolves it: the first qualifying piece of
        ///     its class at the tier the chapter names, or the game's own jewelry and mule.
        /// </summary>
        private static ItemObject? ResolveHeirloom(HeirloomKind kind, StartContext context)
        {
            var culture = context.Hero.Culture;
            var session = context.Session;

            ItemObject? FromClass(WeaponClassChoice weaponClass, int tier) =>
                GearQuery.QualifyingItems(weaponClass, culture, tier, session).FirstOrDefault();

            return kind switch
            {
                HeirloomKind.CultureSword => FromClass(WeaponClassChoice.OneHandedSword, 3),
                HeirloomKind.CultureBow => FromClass(WeaponClassChoice.Bow, 3),
                HeirloomKind.CraftedMace => FromClass(WeaponClassChoice.Mace, 2),
                HeirloomKind.ThrowingKnives => FromClass(WeaponClassChoice.ThrowingKnife, 2),
                HeirloomKind.BoardingAxe => FromClass(WeaponClassChoice.OneHandedAxe, 2),
                HeirloomKind.Jewelry => MBObjectManager.Instance.GetObject<ItemObject>("jewelry"),
                HeirloomKind.Mule => MBObjectManager.Instance.GetObject<ItemObject>("mule"),
                _ => null
            };
        }

        /// <summary>
        ///     The relation the game itself counts as friendship, read off the live
        ///     diplomacy model rather than typed, so an overhaul that rescales the
        ///     relation range rescales this with it.
        /// </summary>
        internal static int FriendThreshold()
        {
            try
            {
                int threshold = Campaign.Current?.Models?.DiplomacyModel?.MaxNeutralRelationLimit ?? 0;
                if (threshold > 0) return threshold;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ConsequenceStep: reading the friendship threshold failed: {ex.Message}");
            }

            CSLogger.Warn("ConsequenceStep: no diplomacy model; the friendship threshold falls back to 50.");
            return 50;
        }

        /// <summary>
        ///     The most a start will move one person's relation: half the distance
        ///     the game itself puts between neutral and a friend.
        ///
        ///     A start that spends the whole of that distance leaves the player
        ///     holding a friendship the campaign was supposed to earn, and one that
        ///     reaches the threshold outright puts a stranger the story never named
        ///     into the player's own list of friends. Half is warm, reads in the
        ///     tooltip, and leaves the rest to play for.
        /// </summary>
        internal static int RelationCeiling() => Math.Max(RelationBonus, FriendThreshold() / 2);

        /// <summary>
        ///     What the game itself prices a holding at in relation: the bonus its
        ///     own diplomacy model pays a lord who is handed one.
        ///
        ///     A start that takes a holding off a clan costs that clan exactly what
        ///     the game says handing one over is worth, so a town and a castle are
        ///     not the same loss. Written down as one figure they were, and the lord
        ///     who lost a town to the newcomer was as forgiving as the one who lost
        ///     a castle.
        /// </summary>
        internal static int WorthOfHolding(Settlement? holding)
        {
            try
            {
                var model = Campaign.Current?.Models?.DiplomacyModel;
                if (model != null)
                    return Math.Max(0, holding != null && holding.IsTown
                        ? model.GiftingTownRelationshipBonus
                        : model.GiftingCastleRelationshipBonus);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ConsequenceStep: reading what a holding is worth failed: {ex.Message}");
            }

            return RelationAmount(2);
        }

        /// <summary>What one person's relation moves by, for an impression of this many steps.</summary>
        internal static int RelationAmount(int steps) =>
            Math.Min(RelationBonus * Math.Max(1, Math.Abs(steps)), RelationCeiling());

        /// <summary>
        ///     How many of the group the impression reaches.
        ///
        ///     Goodwill deepens first and widens afterward: once one person sits at
        ///     the ceiling, a further step reaches one MORE person instead of
        ///     stacking on the same two. What the scene claimed is that a people
        ///     think well of the character, so a life that stood by the headmen
        ///     eight times is known to eight of them rather than adored by two.
        /// </summary>
        internal static int RelationReach(int steps)
        {
            int taken = Math.Max(1, Math.Abs(steps));
            int deepensUntil = Math.Max(1, RelationCeiling() / RelationBonus);
            return MaxRelationTargets + Math.Max(0, taken - deepensUntil);
        }

        private static void ApplyRelations(StartContext context,
            IReadOnlyDictionary<RelationEffect, int> relations)
        {
            foreach (var pair in relations)
            {
                // Nothing at zero: a life that won these people over and then crossed
                // them leaves them undecided, not won over
                if (pair.Key == RelationEffect.None || pair.Value == 0) continue;

                try
                {
                    int amount = RelationAmount(pair.Value) * Math.Sign(pair.Value);
                    int reach = RelationReach(pair.Value);

                    foreach (var target in FindRelationTargets(context, pair.Key, reach).Take(reach))
                    {
                        ChangeRelationAction.ApplyPlayerRelation(target, amount, false, false);

                        // A number beside a face the player has never seen is not a
                        // relationship, it is a puzzle: the encyclopedia answers
                        // "You haven't met this hero yet." and hides the portrait,
                        // the relation and every value behind it. The life said
                        // these people know the character, so the game is told so
                        CompanionGenerator.MarkMet(target, context.Session);

                        CSLogger.Info(
                            $"ConsequenceStep: {amount} relation with {target.Name} ({pair.Key}), " +
                            $"met, {reach} reached at {RelationCeiling()} ceiling.");
                    }
                }
                catch (Exception ex)
                {
                    CSLogger.Error($"ConsequenceStep: applying {pair.Key} relations failed.", ex);
                }
            }
        }

        private static IEnumerable<Hero> FindRelationTargets(StartContext context, RelationEffect effect,
            int reach)
        {
            var town = context.Session.SelectedSettlement ?? context.Session.SelectedLocation;

            switch (effect)
            {
                case RelationEffect.TownMerchants:
                    return town?.Notables?.Where(n => n.IsMerchant) ?? Enumerable.Empty<Hero>();

                case RelationEffect.TownGangLeaders:
                    return town?.Notables?.Where(n => n.IsGangLeader) ?? Enumerable.Empty<Hero>();

                case RelationEffect.VillageHeadmen:
                    if (town?.BoundVillages == null) return Enumerable.Empty<Hero>();
                    return town.BoundVillages
                        .SelectMany(v => v.Settlement?.Notables ?? Enumerable.Empty<Hero>())
                        .Where(n => n.IsHeadman);

                case RelationEffect.CultureLords:
                    var culture = context.Hero.Culture;
                    var lords = Hero.AllAliveHeroes
                        .Where(h => h.IsLord && h != context.Hero && h.Culture == culture &&
                                    h.Clan?.Kingdom != null)
                        .ToList();
                    var picked = new List<Hero>();
                    for (int i = 0; i < reach && lords.Count > 0; i++)
                    {
                        var lord = CSRandom.Pick(lords);
                        if (lord == null) break;
                        lords.Remove(lord);
                        picked.Add(lord);
                    }

                    return picked;

                default:
                    return Enumerable.Empty<Hero>();
            }
        }

        /// <summary>
        ///     What the life handed over, one grant per entry. The same thing twice
        ///     means the life got it twice, so nothing here is folded together.
        ///
        ///     Everything lands in the stores, and anything that answers a slot is
        ///     then WORN, because a story grant is something the character kept
        ///     rather than freight. Nothing is lost either way: a piece that loses
        ///     its slot stays in the baggage. The decision of which piece wins, and
        ///     of which slots the player
        ///     settled by hand and nobody else may fill, belongs to EquipmentStep's
        ///     sweep and is called rather than copied: a second reading of that
        ///     would drift, and the first drift would dress the character
        ///     differently from what the panel promised. Calling it here instead of
        ///     leaving it to the first campaign tick means the character is dressed
        ///     correctly before the map ever opens.
        /// </summary>
        private static void ApplyItems(StartContext context, IReadOnlyList<string> items)
        {
            if (items.Count == 0) return;

            var party = context.Hero.PartyBelongedTo;
            int tier = EquipmentStep.EffectiveTier(context.Session, context.Settings);

            foreach (string target in items)
            {
                try
                {
                    if (target == "coin_pouch")
                    {
                        GrantPurse(context);
                        continue;
                    }

                    if (party == null) continue;

                    if (target == "craft_tools")
                    {
                        GrantCraftKit(context, party.ItemRoster, tier);
                        continue;
                    }

                    var item = ResolveItem(target, context.Hero.Culture, tier, context.Session);
                    if (item == null)
                    {
                        CSLogger.Warn($"ConsequenceStep: no item found for {target}.");
                        continue;
                    }

                    party.ItemRoster.AddToCounts(new EquipmentElement(item), 1);
                    CSLogger.Info($"ConsequenceStep: {item.Name} granted for {target}.");
                }
                catch (Exception ex)
                {
                    CSLogger.Error($"ConsequenceStep: granting {target} failed.", ex);
                }
            }

            try
            {
                EquipmentStep.SweepExclusiveGrants(context.Hero, context.Session);
            }
            catch (Exception ex)
            {
                CSLogger.Error(
                    "ConsequenceStep: wearing what the life granted failed; it stays in the stores.", ex);
            }
        }

        /// <summary>
        ///     Whether a grant is something the character puts ON rather than
        ///     carries. The panel says where a grant ends up, and armor and weapons
        ///     end up on the character whenever they beat what the start already
        ///     dressed them in, so those two cannot be described as inventory.
        /// </summary>
        internal static bool IsWorn(string target) =>
            WeaponClassFor(target) != null ||
            string.Equals(target, "mail_hauberk", StringComparison.Ordinal) ||
            string.Equals(target, "riding_tack", StringComparison.Ordinal);

        /// <summary>
        ///     A purse is coin rather than cargo. The game prices a lot of portable
        ///     valuables itself, so the pouch is worth what the item database says
        ///     it is worth and not a figure chosen here.
        /// </summary>
        private static void GrantPurse(StartContext context)
        {
            int denars = PurseValue();
            if (denars <= 0)
            {
                CSLogger.Warn("ConsequenceStep: no item found for coin_pouch.");
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(null, context.Hero, denars, true);
            CSLogger.Info($"ConsequenceStep: a purse of {denars} denars.");
        }

        /// <summary>What a purse is worth, which is what the item database prices it at.</summary>
        internal static int PurseValue() =>
            MBObjectManager.Instance?.GetObject<ItemObject>("jewelry")?.Value ?? 0;

        /// <summary>
        ///     A working bench rather than a souvenir: the game's own tools, plus a
        ///     load of the raw material this character's country yields.
        ///
        ///     Both halves are resolved before either is granted. The panel and the
        ///     encyclopedia page both promise tools AND material, so handing over
        ///     one of the two would be the same defect as handing over neither while
        ///     still saying so.
        /// </summary>
        private static void GrantCraftKit(StartContext context, ItemRoster roster, int tier)
        {
            var tools = DefaultItems.Tools;
            var stock = CraftStock(context.Hero.Culture, tier, tools);
            if (tools == null || stock == null)
            {
                CSLogger.Warn("ConsequenceStep: no item found for craft_tools.");
                return;
            }

            roster.AddToCounts(new EquipmentElement(tools), 1);
            roster.AddToCounts(new EquipmentElement(stock), 1);
            CSLogger.Info($"ConsequenceStep: {tools.Name} and {stock.Name} added to inventory for craft_tools.");
        }

        /// <summary>
        ///     What a craftsman of this culture would have on the bench. The pool is
        ///     what this culture's own villages actually yield, read off the live map
        ///     rather than listed here, with food left out because food is not stock.
        ///     Standing then decides how dear the material is, so a village hand
        ///     works clay where a well-placed one works silver.
        ///
        ///     A culture whose land yields nothing workable falls back to the goods
        ///     any market carries. That is still raw material, not something standing
        ///     in for the tools.
        /// </summary>
        internal static ItemObject? CraftStock(CultureObject? culture, int tier, ItemObject? tools)
        {
            var pool = Village.All
                .Where(v => culture == null || v.Settlement?.Culture == culture)
                .Select(v => v.VillageType?.PrimaryProduction)
                .OfType<ItemObject>()
                .Where(i => i != tools && i.IsTradeGood && !i.IsFood)
                .Distinct()
                .ToList();

            if (pool.Count == 0)
                pool = ArmorQuery.TradeGoodItems().Where(i => i != tools).ToList();
            if (pool.Count == 0) return null;

            var ranked = pool
                .OrderBy(i => i.Value)
                .ThenBy(i => i.StringId, StringComparer.Ordinal)
                .ToList();

            int top = Math.Max(1, GameCaps.MaxClanTier());
            int standing = Math.Max(0, Math.Min(tier, top));
            return ranked[standing * (ranked.Count - 1) / top];
        }

        /// <summary>
        ///     What the life borrowed comes out of the purse, and a character with
        ///     nothing left simply has nothing left rather than going negative.
        /// </summary>
        private static void ApplyDebt(StartContext context, int debt)
        {
            if (debt <= 0) return;

            try
            {
                int taken = Math.Min(debt, context.Hero.Gold);
                if (taken > 0)
                    GiveGoldAction.ApplyBetweenCharacters(context.Hero, null, taken, true);

                CSLogger.Info($"ConsequenceStep: {taken} of {debt} denars owed taken from the purse.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("ConsequenceStep: settling the debt failed.", ex);
            }
        }

        /// <summary>
        ///     The weapon class a grant is asked for as, or null where the grant is
        ///     not a weapon. An heirloom blade and a blade bought yesterday are the
        ///     same object to the game, and the life profile already reads them as
        ///     one.
        /// </summary>
        internal static WeaponClassChoice? WeaponClassFor(string target) => target switch
        {
            "family_sword" => WeaponClassChoice.OneHandedSword,
            "one_handed_sword" => WeaponClassChoice.OneHandedSword,
            "spear" => WeaponClassChoice.Spear,
            "two_handed_axe" => WeaponClassChoice.TwoHandedAxe,
            "boarding_axe" => WeaponClassChoice.OneHandedAxe,
            "lance" => WeaponClassChoice.Lance,
            "hunting_bow" => WeaponClassChoice.Bow,
            _ => null
        };

        /// <summary>
        ///     What a grant comes out as, for a character of this culture at this
        ///     standing. Culture and standing are passed rather than read off a
        ///     context so the effect panel can ask the same question mid-creation,
        ///     before there is a hero to read them from.
        ///
        ///     Two of these answer differently every time they are asked: a weapon
        ///     is drawn from the best few that fit, and a trade good from whatever
        ///     the markets carry. A caller that must not spend a draw asks
        ///     <see cref="WeaponClassFor"/> and the item queries for the pool
        ///     instead, which is what the panel does.
        /// </summary>
        internal static ItemObject? ResolveItem(string target, CultureObject? culture, int tier,
            CharacterCreationSession session)
        {
            var weaponClass = WeaponClassFor(target);
            if (weaponClass != null)
                return GearQuery.QuartermasterPick(weaponClass.Value, culture, tier, session);

            return target switch
            {
                "mail_hauberk" => Mail(culture, tier, session),
                "riding_tack" => Best(
                    ArmorQuery.QualifyingItems(EquipmentIndex.HorseHarness, culture, tier, session),
                    culture, tier),
                "war_horse" => Mount(culture, tier, session, WarMounts),
                "riding_horse" => Mount(culture, tier, session, RidingMounts),
                "pack_mule" => Mount(culture, tier, session, PackAnimals),
                "trade_goods" => CSRandom.Pick(ArmorQuery.TradeGoodItems()),
                _ => null
            };
        }

        /// <summary>
        ///     Mail, by the material the game itself records on the piece, so the
        ///     hauberk the scene named cannot come back as a padded jacket.
        /// </summary>
        private static ItemObject? Mail(CultureObject? culture, int tier, CharacterCreationSession session) =>
            Best(
                ArmorQuery.QualifyingItems(EquipmentIndex.Body, culture, tier, session)
                    .Where(i => i.ArmorComponent != null &&
                                i.ArmorComponent.MaterialType ==
                                ArmorComponent.ArmorMaterialTypes.Chainmail),
                culture, tier);

        /// <summary>
        ///     A mount of the kind the game files it under, so a war horse and a
        ///     pack animal can never come back as each other.
        /// </summary>
        private static ItemObject? Mount(CultureObject? culture, int tier,
            CharacterCreationSession session, string[] categories) =>
            Best(
                ArmorQuery.QualifyingItems(EquipmentIndex.Horse, culture, tier, session)
                    .Where(i => i.ItemCategory != null &&
                                categories.Contains(i.ItemCategory.StringId, StringComparer.Ordinal)),
                culture, tier);

        /// <summary>Nearest the start's own standing, and the character's own culture ahead of a stranger's.</summary>
        private static ItemObject? Best(IEnumerable<ItemObject> candidates, CultureObject? culture, int tier) =>
            candidates
                .OrderBy(i => Math.Abs((int)i.Tier - tier))
                .ThenByDescending(i => culture != null && i.Culture == culture)
                .ThenByDescending(i => i.Value)
                .FirstOrDefault();
    }
}
