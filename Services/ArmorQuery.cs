using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Item lookups for armor, mount, outfit, and banner pickers: live database
    ///     queries so DLC and modded items qualify automatically, sorted highest
    ///     tier first then highest value, mounts gated by actual riding skill.
    /// </summary>
    public static class ArmorQuery
    {
        public static ItemObject.ItemTypeEnum? ItemTypeForSlot(EquipmentIndex slot) => slot switch
        {
            EquipmentIndex.Head => ItemObject.ItemTypeEnum.HeadArmor,
            EquipmentIndex.Body => ItemObject.ItemTypeEnum.BodyArmor,
            EquipmentIndex.Leg => ItemObject.ItemTypeEnum.LegArmor,
            EquipmentIndex.Gloves => ItemObject.ItemTypeEnum.HandArmor,
            EquipmentIndex.Cape => ItemObject.ItemTypeEnum.Cape,
            EquipmentIndex.Horse => ItemObject.ItemTypeEnum.Horse,
            EquipmentIndex.HorseHarness => ItemObject.ItemTypeEnum.HorseHarness,
            _ => null
        };

        /// <summary>
        ///     Every qualifying item for the slot and outfit kind. Stealth prefers
        ///     stealth-flagged items and falls back to civilian ones; mounts are
        ///     limited to what the character's riding skill can actually ride.
        /// </summary>
        /// <summary>True when the outfit kind has this slot at all (stealth has no mounts).</summary>
        public static bool SlotAllowed(EquipmentIndex slot, OutfitKind kind)
        {
            if (kind == OutfitKind.Stealth &&
                slot is EquipmentIndex.Horse or EquipmentIndex.HorseHarness)
                return false;
            return true;
        }

        public static List<ItemObject> QualifyingItems(EquipmentIndex slot, CultureObject? culture,
            int targetTier, CharacterCreationSession session, OutfitKind kind = OutfitKind.Battle,
            ItemObject? mount = null)
        {
            var itemType = ItemTypeForSlot(slot);
            if (itemType == null || !SlotAllowed(slot, kind)) return new List<ItemObject>();

            int ridingSkill = slot == EquipmentIndex.Horse
                ? NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Riding)
                : int.MaxValue;

            var candidates = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.ItemType == itemType.Value && !GameCompat.IsUniqueItem(i) && !i.IsCraftedByPlayer)
                .Where(i => i.Difficulty <= 0 || slot != EquipmentIndex.Horse || i.Difficulty <= ridingSkill)
                .ToList();

            // Battle takes anything; civilian takes civilian-flagged items only.
            // Stealth takes stealth-flagged items and civilian-legal ones together:
            // ItemFlags.Stealth is base game, carried by 96 SandBoxCore items on
            // v1.5.2 with 8 more from War Sails, but those 96 lean heavily to armor,
            // so the flag alone left slots with nothing to put in them.
            if (kind == OutfitKind.Civilian)
                candidates = candidates.Where(i => i.IsCivilian).ToList();
            else if (kind == OutfitKind.Stealth)
                candidates = candidates
                    .Where(i => i.IsCivilian || VersionedGameApi.IsStealthGear(i))
                    .ToList();

            // Every picker sorts the same way, best first: highest tier, then highest
            // value within a tier. Stealth briefly sorted by weight instead, which
            // stood the list on its head against every other tab.
            // A harness belongs to one kind of animal, and the game refuses the pairing the
            // moment a player opens the inventory, so the list never offers one that does not fit
            if (slot == EquipmentIndex.HorseHarness && mount != null)
                candidates = MountFit.FittingHarnesses(mount, candidates);

            return OfficialItemRegistry.FilterAllowed(candidates
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>
        ///     Every mount the inventory can hold: speed horses and the pack
        ///     animals that add carrying capacity, best first.
        /// </summary>
        public static List<ItemObject> MountItems()
        {
            return OfficialItemRegistry.FilterAllowed(MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Horse && !GameCompat.IsUniqueItem(i))
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>Every purchasable food item, best first.</summary>
        public static List<ItemObject> FoodItems()
        {
            return OfficialItemRegistry.FilterAllowed(MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.IsFood && !GameCompat.IsUniqueItem(i))
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>
        ///     Every good a market will actually trade as cargo, dearest first, and
        ///     the one list everything that names cargo reads: the ledger chapter,
        ///     the wagons the caravan start fills, the editor's trade goods picker
        ///     and the panel that counts the kinds the markets carry.
        ///
        ///     Three kinds of item look like cargo and are not. Food is the
        ///     provisions pool, bought and eaten rather than hauled; an item the
        ///     game marks NotMerchandise is carried by no market at all, which is
        ///     what stolen goods are; and the game's own trash item is a
        ///     placeholder worth a denar that no town has a category for.
        /// </summary>
        public static List<ItemObject> TradeGoodItems()
        {
            var trash = Campaign.Current != null ? DefaultItems.Trash : null;

            return OfficialItemRegistry.FilterAllowed(MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.IsTradeGood && !i.NotMerchandise && !i.IsFood && !GameCompat.IsUniqueItem(i) && i != trash)
                .OrderByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>Every choosable banner item (campaign-unique banners excluded).</summary>
        public static List<ItemObject> BannerItems(CultureObject? culture)
        {
            return OfficialItemRegistry.FilterAllowed(MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Banner && !GameCompat.IsUniqueItem(i))
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
    }
}
