using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    public class EquipmentGenerator : IEquipmentGenerator
    {
        public static readonly EquipmentIndex[] ArmorSlots =
        {
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Leg,
            EquipmentIndex.Gloves,
            EquipmentIndex.Cape
        };

        public static string SlotName(EquipmentIndex slot) => slot switch
        {
            EquipmentIndex.Head => "Head",
            EquipmentIndex.Body => "Body",
            EquipmentIndex.Leg => "Leg",
            EquipmentIndex.Gloves => "Gloves",
            EquipmentIndex.Cape => "Cape",
            EquipmentIndex.Horse => "Horse",
            EquipmentIndex.HorseHarness => "HorseHarness",
            _ => slot.ToString()
        };

        public void GenerateHeroEquipment(Hero hero, int targetTier)
        {
            CSLogger.Info(">>> START: GenerateHeroEquipment");
            CSLogger.Info($"  Hero={hero.Name}, Culture={hero.Culture?.StringId}, TargetTier={targetTier}");

            try
            {
                if (hero.BattleEquipment == null)
                {
                    CSLogger.Warn("  BattleEquipment is null; skipping.");
                    CSLogger.Info("<<< END: GenerateHeroEquipment [SKIPPED]");
                    return;
                }

                var culture = hero.Culture;

                foreach (var slot in ArmorSlots)
                {
                    var item = Regalia(hero, culture, slot, Models.OutfitKind.Battle)
                               ?? FindArmorItem(slot, targetTier, culture, -1, false,
                                   hero.BattleEquipment[slot].Item);
                    if (item != null)
                    {
                        hero.BattleEquipment[slot] = new EquipmentElement(item);
                        CSLogger.Info($"  Equipped [{SlotName(slot)}]: {item.Name} (Tier={item.Tier}, Difficulty={item.Difficulty})");
                    }
                    else
                    {
                        CSLogger.Warn($"  No item found for slot [{SlotName(slot)}]");
                    }
                }

                if (targetTier >= 3)
                {
                    int ridingSkill = hero.GetSkillValue(DefaultSkills.Riding);
                    CSLogger.Info($"  Riding skill: {ridingSkill}");
                    // A mount the game has no harness for is ridden bare, so one that can be
                    // dressed is preferred; the harness that follows is then drawn from that
                    // mount's own family, which is the only thing that makes the two go together
                    var horse = FindArmorItem(EquipmentIndex.Horse, targetTier, culture, ridingSkill,
                                   false, hero.BattleEquipment[EquipmentIndex.Horse].Item,
                                   MountFit.HasAnyHarness)
                                ?? FindArmorItem(EquipmentIndex.Horse, targetTier, culture, ridingSkill,
                                    false, hero.BattleEquipment[EquipmentIndex.Horse].Item);
                    if (horse != null)
                    {
                        hero.BattleEquipment[EquipmentIndex.Horse] = new EquipmentElement(horse);
                        CSLogger.Info($"  Equipped [Horse]: {horse.Name} (Difficulty={horse.Difficulty})");

                        var harness = FindArmorItem(EquipmentIndex.HorseHarness, targetTier, culture,
                            -1, false, hero.BattleEquipment[EquipmentIndex.HorseHarness].Item,
                            item => MountFit.Fits(horse, item));
                        if (harness != null)
                        {
                            hero.BattleEquipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness);
                            CSLogger.Info($"  Equipped [HorseHarness]: {harness.Name}");
                        }
                        else
                        {
                            hero.BattleEquipment[EquipmentIndex.HorseHarness] = default;
                            CSLogger.Info($"  No harness exists for {horse.Name}'s kind; it is ridden bare.");
                        }
                    }
                    else
                    {
                        CSLogger.Warn("  No usable horse found (skill too low or none available).");
                    }
                }

                CSLogger.Info("<<< END: GenerateHeroEquipment [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: GenerateHeroEquipment [FAILED]", ex);
                throw;
            }
        }

        /// <summary>
        /// Generates a civilian outfit from civilian-flagged items so the hero is
        /// not left in default rags (or battle armor) when walking into town.
        /// </summary>
        public void GenerateCivilianEquipment(Hero hero, int targetTier)
        {
            try
            {
                if (hero.CivilianEquipment == null)
                {
                    CSLogger.Warn("  CivilianEquipment is null; skipping civilian outfit.");
                    return;
                }

                var culture = hero.Culture;
                int equipped = 0;
                foreach (var slot in ArmorSlots)
                {
                    var item = Regalia(hero, culture, slot, Models.OutfitKind.Civilian)
                               ?? FindArmorItem(slot, targetTier, culture, -1, true,
                                   hero.CivilianEquipment[slot].Item);
                    if (item == null) continue;

                    hero.CivilianEquipment[slot] = new EquipmentElement(item);
                    equipped++;
                }

                CSLogger.Info($"  Civilian outfit: {equipped} slots equipped at tier {targetTier}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("GenerateCivilianEquipment failed.", ex);
            }
        }

        /// <summary>
        ///     Generates a stealth outfit. The whole of it is base game: every
        ///     culture carries a default_stealth_equipment_roster, Hero.StealthEquipment
        ///     exists without any DLC, and ItemFlags.Stealth is declared in
        ///     TaleWorlds.Core and carried by 96 SandBoxCore items on v1.5.2, with 8
        ///     more from War Sails. The flag is still a preference and never a
        ///     requirement, because those 96 do not cover every slot.
        ///     No mount: ArmorSlots holds no mount slots.
        /// </summary>
        public void GenerateStealthEquipment(Hero hero, int targetTier)
        {
            try
            {
                var stealth = VersionedGameApi.StealthEquipment(hero);
                if (stealth == null)
                {
                    CSLogger.Warn("  StealthEquipment is null; skipping stealth outfit.");
                    return;
                }

                var culture = hero.Culture;
                int equipped = 0;
                foreach (var slot in ArmorSlots)
                {
                    var item = FindStealthItem(slot, targetTier, culture,
                        stealth[slot].Item);
                    if (item == null) continue;

                    stealth[slot] = new EquipmentElement(item);
                    equipped++;
                }

                CSLogger.Info($"  Stealth outfit: {equipped} slots equipped at tier {targetTier}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("GenerateStealthEquipment failed.", ex);
            }
        }

        /// <summary>
        ///     Light and unmarked: stealth-flagged pieces first, then civilian-legal
        ///     ones, lighter ahead of heavier within each group.
        /// </summary>
        public static ItemObject? FindStealthItem(EquipmentIndex slot, int targetTier,
            CultureObject? culture, ItemObject? avoid = null)
        {
            var ranked = LockedToCulture(QueryArmorItems(slot, targetTier, -1, false), culture)
                .Where(IsQuiet)
                .OrderByDescending(i => VersionedGameApi.IsStealthGear(i))
                .ThenBy(i => i.Weight)
                .ToList();

            return Draw(ranked, targetTier, culture, avoid);
        }

        private static bool CulturedStartRoute() =>
            CreationSession.Current?.Mode == Models.SetupMode.LifePath;

        /// <summary>
        ///     On Cultured Start only a piece of the character's own culture, or one
        ///     with no culture at all, is worn; a character with no culture wears only
        ///     the latter. Every other route leaves culture to the draw's lean.
        /// </summary>
        private static IEnumerable<ItemObject> LockedToCulture(IEnumerable<ItemObject> items, CultureObject? culture)
        {
            if (!CulturedStartRoute()) return items;

            return items.Where(i => i.Culture == null || (culture != null && i.Culture == culture));
        }

        private static bool IsQuiet(ItemObject item) =>
            item.IsCivilian || VersionedGameApi.IsStealthGear(item);

        /// <summary>
        ///     Hands the ranked pool to the draw, which settles the tier band, the
        ///     lean towards the character's own people, and which of the band they
        ///     end up in. <paramref name="avoid" /> is what the slot is wearing
        ///     now, so asking again dresses them differently while there is
        ///     anything else at that standing to wear.
        /// </summary>
        private static ItemObject? Draw(IReadOnlyList<ItemObject> ranked, int targetTier,
            CultureObject? culture, ItemObject? avoid)
        {
            return GearDraw.Pick(Candidates(ranked), targetTier, PeopleOf(culture),
                GearDraw.MinimumVariety, avoid?.StringId, CSRandom.Next);
        }

        /// <summary>The ranked pool in the shape the draw reads, best first.</summary>
        public static List<GearDraw.Candidate<ItemObject>> Candidates(IReadOnlyList<ItemObject> ranked)
        {
            var candidates = new List<GearDraw.Candidate<ItemObject>>(ranked.Count);
            for (int position = 0; position < ranked.Count; position++)
            {
                var item = ranked[position];
                candidates.Add(new GearDraw.Candidate<ItemObject>(item, item.StringId,
                    (int)item.Tier, PeopleOf(item.Culture), ranked.Count - position));
            }

            return candidates;
        }

        /// <summary>
        ///     Whose people a piece of gear belongs to, or null when it belongs to
        ///     none.
        ///
        ///     The game says "belongs to nobody" two ways, and reading only the
        ///     first is the whole of the bug: a missing culture, and a culture that
        ///     is not a people. Calradian, the common wardrobe every character in
        ///     the game wears, is the second kind, and counting it as some other
        ///     nation's is how ordinary clothes came to be treated as foreign. A
        ///     people is one that rules, raids or holds a settlement, which is the
        ///     game's own three-way answer rather than a named exception here, so a
        ///     conversion that calls its common culture something else is followed.
        /// </summary>
        public static string? PeopleOf(BasicCultureObject? culture)
        {
            if (culture == null) return null;
            return culture.IsMainCulture || culture.IsBandit || culture.CanHaveSettlement
                ? culture.StringId
                : null;
        }

        /// <summary>
        ///     A harness with nothing under it is dead weight, and one cut for another animal is a
        ///     broken render, so both are taken off here. Every caller gets both checks, because the
        ///     pair is one question and asking half of it is what put elephant armor on a horse.
        /// </summary>
        public static void ClearHarnessWithoutMount(Equipment? equipment)
        {
            string? removed = MountFit.Reconcile(equipment);
            if (removed != null) CSLogger.Info($"  {removed}.");
        }

        /// <summary>
        ///     A ruler wears what a ruler wears. The quartermaster picks by culture
        ///     and tier and has no idea the character is a monarch, which is how a
        ///     crowned head ended up in an ordinary helmet; a crown is not a better
        ///     helmet, it is a different kind of thing, and no attribute on the item
        ///     says so. Null for anyone who is not a ruler, and null for a ruler
        ///     whose culture has no royal set, which leaves the quartermaster's
        ///     answer standing rather than borrowing another people's crown.
        /// </summary>
        public static ItemObject? Regalia(Hero hero, CultureObject? culture,
            EquipmentIndex slot, Models.OutfitKind kind)
        {
            if (slot != EquipmentIndex.Head || hero == null) return null;

            // Cultured Start's rulers wear what the quartermaster hands every lord
            if (CulturedStartRoute()) return null;
            if (hero.Clan?.Kingdom?.Leader != hero) return null;

            var crown = RegaliaQuery.Crown(culture, hero.IsFemale, kind);
            if (crown != null)
                CSLogger.Info($"  Regalia: {hero.Name} wears {crown.StringId} ({kind}).");

            return crown;
        }

        public static ItemObject? FindArmorItem(EquipmentIndex slot, int targetTier,
            CultureObject? culture, int maxDifficulty = -1, bool civilianOnly = false,
            ItemObject? avoid = null, System.Func<ItemObject, bool>? mustFit = null)
        {
            // What someone of this standing would actually be wearing. Standing is
            // why a landed lord out-dresses a landless one at the same tier, and
            // why an outlaw does not turn up in something no fence could have sold
            // them. Tier and culture are the draw's business, not this ordering's.
            var pool = QueryArmorItems(slot, targetTier, maxDifficulty, civilianOnly);
            if (mustFit != null) pool = pool.Where(mustFit).ToList();
            if (pool.Count == 0) return null;
            List<ItemObject> ranked;
            if (CulturedStartRoute())
            {
                // Cultured Start dresses a character in their own people's gear or in
                // gear that belongs to nobody, their own ahead of the rest
                ranked = LockedToCulture(pool, culture)
                    .OrderByDescending(i => culture != null && i.Culture == culture)
                    .ToList();
            }
            else
            {
                double standing = StoryGear.Standing(CreationSession.Current);
                ranked = pool.OrderByDescending(i => StoryGear.Rank(i, standing)).ToList();
            }

            return Draw(ranked, targetTier, culture, avoid);
        }

        private static List<ItemObject> QueryArmorItems(EquipmentIndex slot, int targetTier,
            int maxDifficulty, bool civilianOnly)
        {
            var allItems = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            var candidates = allItems.Where(item =>
            {
                if (item.ItemType == ItemObject.ItemTypeEnum.Invalid) return false;
                if (!MatchesSlot(item, slot)) return false;

                // At or below the target, so a low-tier hero is never over-geared
                int tier = (int)item.Tier;
                if (tier < 0 || tier > targetTier) return false;

                // No culture test here at all. Culture is a lean the draw applies
                // as a share of the outcome, never a gate on the pool: excluding
                // another people's gear collapsed most slots to a handful of items,
                // and excluding gear that belongs to NO people, which is what
                // reading a null culture as a mismatch did, threw away the pieces
                // that suit every character alive.

                // Mounts carry a riding requirement
                if (maxDifficulty >= 0 && item.Difficulty > maxDifficulty) return false;

                if (civilianOnly && !item.IsCivilian) return false;

                return true;
            }).ToList();

            return OfficialItemRegistry.FilterAllowed(candidates);
        }

        private static bool MatchesSlot(ItemObject item, EquipmentIndex slot)
        {
            return slot switch
            {
                EquipmentIndex.Head => item.ItemType == ItemObject.ItemTypeEnum.HeadArmor,
                EquipmentIndex.Body => item.ItemType == ItemObject.ItemTypeEnum.BodyArmor,
                EquipmentIndex.Leg => item.ItemType == ItemObject.ItemTypeEnum.LegArmor,
                EquipmentIndex.Gloves => item.ItemType == ItemObject.ItemTypeEnum.HandArmor,
                EquipmentIndex.Cape => item.ItemType == ItemObject.ItemTypeEnum.Cape,
                EquipmentIndex.Horse => item.ItemType == ItemObject.ItemTypeEnum.Horse,
                EquipmentIndex.HorseHarness => item.ItemType == ItemObject.ItemTypeEnum.HorseHarness,
                _ => false
            };
        }

    }
}
