using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Item lookups for gear selection, filtered by the game's actual
    ///     WeaponClass (spears and lances separated by couchability) so a
    ///     "spear" choice never hands out a pitchfork-to-lance grab bag.
    ///     Lists are generated live from the item database, so DLC and
    ///     modded items qualify automatically.
    /// </summary>
    public static class GearQuery
    {
        /// <summary>
        ///     Shields present as one class; small and large only separate when
        ///     item mods are installed AND modded selections are turned on, where
        ///     the distinction can actually matter.
        /// </summary>
        public static bool ShieldsMerged =>
            !(OfficialItemRegistry.AnyModdedItems &&
              (MCM.Abstractions.Base.Global.GlobalSettings<Settings.CSSettings>.Instance
                  ?.AllowModdedItems ?? false));

        /// <summary>
        ///     A weapon a stealth outfit may carry: stealth-flagged, or civilian-legal
        ///     as the fallback. ArmorQuery and EquipmentGenerator already read the same
        ///     question that way, and only twenty of the base game's stealth-flagged
        ///     items are weapons, all of them bows, crossbows, slings or a single axe,
        ///     so requiring the flag left the stealth outfit unable to carry a blade.
        ///     The flag itself is base game, declared in TaleWorlds.Core and carried by
        ///     96 SandBoxCore items on v1.5.2; War Sails adds 8 more.
        /// </summary>
        private static bool IsQuiet(ItemObject item) =>
            item.IsCivilian || VersionedGameApi.IsStealthGear(item);

        /// <summary>True when the item's primary weapon mode matches the choice.</summary>
        public static bool Matches(ItemObject item, WeaponClassChoice choice)
        {
            if (!item.HasWeaponComponent || item.PrimaryWeapon == null)
                return false;

            var weaponClass = item.PrimaryWeapon.WeaponClass;
            return choice switch
            {
                WeaponClassChoice.OneHandedSword => weaponClass == WeaponClass.OneHandedSword,
                WeaponClassChoice.OneHandedAxe => weaponClass == WeaponClass.OneHandedAxe,
                WeaponClassChoice.Mace => weaponClass is WeaponClass.Mace or WeaponClass.Pick,
                WeaponClassChoice.TwoHandedSword => weaponClass == WeaponClass.TwoHandedSword,
                WeaponClassChoice.TwoHandedAxe => weaponClass is WeaponClass.TwoHandedAxe or WeaponClass.TwoHandedMace,
                WeaponClassChoice.Spear => IsPolearm(weaponClass) && !IsCouchable(item),
                WeaponClassChoice.Lance => IsPolearm(weaponClass) && IsCouchable(item),
                WeaponClassChoice.ThrowingAxe => weaponClass == WeaponClass.ThrowingAxe,
                WeaponClassChoice.Javelin => weaponClass == WeaponClass.Javelin,
                WeaponClassChoice.ThrowingKnife => weaponClass == WeaponClass.ThrowingKnife,
                WeaponClassChoice.Bow => weaponClass == WeaponClass.Bow,
                WeaponClassChoice.Crossbow => weaponClass == WeaponClass.Crossbow,
                WeaponClassChoice.SmallShield => ShieldsMerged
                    ? weaponClass is WeaponClass.SmallShield or WeaponClass.LargeShield
                    : weaponClass == WeaponClass.SmallShield,
                WeaponClassChoice.LargeShield => ShieldsMerged
                    ? weaponClass is WeaponClass.SmallShield or WeaponClass.LargeShield
                    : weaponClass == WeaponClass.LargeShield,
                WeaponClassChoice.Arrows => weaponClass == WeaponClass.Arrow,
                WeaponClassChoice.Bolts => weaponClass == WeaponClass.Bolt,
                WeaponClassChoice.Stone => weaponClass == WeaponClass.Stone,
                _ => false
            };
        }

        /// <summary>
        ///     Every item qualifying for the choice that the character could
        ///     actually use, sorted highest tier first, then highest value.
        /// </summary>
        public static List<ItemObject> QualifyingItems(WeaponClassChoice choice,
            CultureObject? culture, int targetTier, CharacterCreationSession session,
            OutfitKind kind = OutfitKind.Battle)
        {
            if (choice == WeaponClassChoice.KeepAuto)
                return new List<ItemObject>();

            // Stones are never merchandise, so the shop filter would hide them
            bool requireMerchandise = choice != WeaponClassChoice.Stone;
            var candidates = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => (!requireMerchandise || !i.NotMerchandise) && !GameCompat.IsUniqueItem(i) && !i.IsCraftedByPlayer)
                .Where(i => Matches(i, choice))
                .Where(i => MeetsSkillRequirement(i, session));

            if (kind == OutfitKind.Civilian)
                candidates = candidates.Where(i => i.IsCivilian);
            else if (kind == OutfitKind.Stealth)
                candidates = candidates.Where(IsQuiet);

            return OfficialItemRegistry.FilterAllowed(candidates
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>
        ///     Every weapon the character can use, for the editor's per-slot
        ///     pickers: highest tier first, outfit-kind filters applied (a civilian
        ///     outfit carries civilian weapons, a stealth outfit those plus the
        ///     stealth-flagged ones).
        /// </summary>
        public static List<ItemObject> AllWeapons(CultureObject? culture, int targetTier,
            CharacterCreationSession session, OutfitKind kind = OutfitKind.Battle)
        {
            var candidates = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.HasWeaponComponent && i.PrimaryWeapon != null)
                // A banner is carried in a weapon slot and so has a weapon component,
                // which put every banner in the weapon pickers. It has its own row.
                .Where(i => i.ItemType != ItemObject.ItemTypeEnum.Banner)
                .Where(i => !GameCompat.IsUniqueItem(i) && !i.IsCraftedByPlayer)
                .Where(i => MeetsSkillRequirement(i, session));

            if (kind == OutfitKind.Civilian)
                candidates = candidates.Where(i => i.IsCivilian);
            else if (kind == OutfitKind.Stealth)
                candidates = candidates.Where(IsQuiet);

            return OfficialItemRegistry.FilterAllowed(candidates
                .OrderByDescending(i => (int)i.Tier)
                .ThenByDescending(i => i.Value)
                .ThenBy(i => i.Name?.ToString() ?? i.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        // Lance before Spear so couchable polearms classify by how they are used
        private static readonly WeaponClassChoice[] ClassifyOrder =
        {
            WeaponClassChoice.Lance, WeaponClassChoice.Spear, WeaponClassChoice.OneHandedSword,
            WeaponClassChoice.OneHandedAxe, WeaponClassChoice.Mace, WeaponClassChoice.TwoHandedSword,
            WeaponClassChoice.TwoHandedAxe, WeaponClassChoice.ThrowingAxe, WeaponClassChoice.Javelin,
            WeaponClassChoice.ThrowingKnife, WeaponClassChoice.Bow, WeaponClassChoice.Crossbow,
            WeaponClassChoice.SmallShield, WeaponClassChoice.LargeShield, WeaponClassChoice.Arrows,
            WeaponClassChoice.Bolts, WeaponClassChoice.Stone
        };

        /// <summary>
        ///     The player-facing usage class of a weapon: lances are couchable
        ///     polearms, spears the rest, no matter what technical class the item
        ///     database gives them. Null when no usage class fits.
        /// </summary>
        public static WeaponClassChoice? ClassifyChoice(ItemObject item)
        {
            foreach (var choice in ClassifyOrder)
                if (Matches(item, choice))
                    // Merged shields classify as one class, so filters and
                    // labels show a single Shield entry
                    return ShieldsMerged && choice == WeaponClassChoice.LargeShield
                        ? WeaponClassChoice.SmallShield
                        : choice;
            return null;
        }

        /// <summary>
        ///     A quality random draw from the tier-appropriate items, the best of
        ///     them likeliest and none of them out of reach, so asking twice gives
        ///     two answers. Culture is a lean the draw applies and never a filter:
        ///     the character's own smiths lead, a weapon belonging to no people is
        ///     theirs as much as one of their own, and the rest of the world is
        ///     still reachable rather than shut out.
        ///
        ///     `alreadyCarried` holds the item ids the character is already
        ///     carrying, so a second slot asking for the same class is answered
        ///     with a different weapon instead of a duplicate of the first.
        /// </summary>
        public static ItemObject? QuartermasterPick(WeaponClassChoice choice,
            CultureObject? culture, int targetTier, CharacterCreationSession session,
            ISet<string>? alreadyCarried = null)
        {
            var ranked = QualifyingItems(choice, culture, targetTier, session)
                .Where(i => alreadyCarried == null || !alreadyCarried.Contains(i.StringId))
                .ToList();
            if (ranked.Count == 0) return null;

            return GearDraw.Pick(EquipmentGenerator.Candidates(ranked), targetTier,
                EquipmentGenerator.PeopleOf(culture), GearDraw.MinimumVariety, null, CSRandom.Next);
        }

        /// <summary>
        ///     Classes carried as stacks, where a second identical one is the point.
        ///
        ///     Declared once and read by everything that fills a weapon slot. The
        ///     no-duplicates rule exists so a second slot asking for a sword is not
        ///     answered with the same sword; a second sheaf of javelins is a second
        ///     sheaf of javelins, and a set that names one class twice means it. The
        ///     apply pipeline had no such exemption, so a set that repeats a class
        ///     resolved the repeat to nothing whenever no preview stood behind it.
        /// </summary>
        public static bool Stacks(WeaponClassChoice choice) =>
            choice is WeaponClassChoice.Arrows or WeaponClassChoice.Bolts
                or WeaponClassChoice.Stone or WeaponClassChoice.Javelin
                or WeaponClassChoice.ThrowingAxe or WeaponClassChoice.ThrowingKnife;

        /// <summary>
        ///     Resolves the item the choice yields: the exact pick when the player
        ///     made one (and it still qualifies), else the quartermaster's pick.
        /// </summary>
        public static ItemObject? Resolve(GearChoice choice, CultureObject? culture,
            int targetTier, CharacterCreationSession session,
            ISet<string>? alreadyCarried = null)
        {
            if (choice.IsKeepAuto) return null;

            if (choice.ExactItem != null)
                return choice.ExactItem;

            return QuartermasterPick(choice.WeaponClass, culture, targetTier, session, alreadyCarried);
        }

        private static bool IsPolearm(WeaponClass weaponClass)
        {
            return weaponClass is WeaponClass.OneHandedPolearm
                or WeaponClass.TwoHandedPolearm
                or WeaponClass.LowGripPolearm;
        }

        /// <summary>Couchable in any weapon mode, the vanilla "couch" usage marker.</summary>
        private static bool IsCouchable(ItemObject item)
        {
            if (item.Weapons == null) return false;

            foreach (var weapon in item.Weapons)
            {
                var usage = weapon?.ItemUsage;
                if (usage != null &&
                    usage.IndexOf("couch", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool MeetsSkillRequirement(ItemObject item, CharacterCreationSession session)
        {
            if (item.Difficulty <= 0 || item.RelevantSkill == null)
                return true;

            return NarrativeStep.ExpectedSkillValue(session, item.RelevantSkill) >= item.Difficulty;
        }
    }
}
