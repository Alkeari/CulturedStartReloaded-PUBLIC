using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Fills unchosen weapon slots with a loadout that fits the character:
    ///     the melee weapon follows the highest melee skill, a ranged weapon and
    ///     its ammunition appear only when the ranged skill earns them, a shield
    ///     backs a one-handed weapon, and ammunition is added for any ranged
    ///     weapon the player picked themselves. Item quality stays tier-bound, so
    ///     a humble start never carries royal steel.
    /// </summary>
    public static class SmartQuartermaster
    {
        private const int RangedSkillFloor = 30;

        public static void FillAutoSlots(Hero hero, CharacterCreationSession session, int tier)
        {
            var equipment = hero.BattleEquipment;
            if (equipment == null) return;

            var culture = hero.Culture;
            var autoSlots = new List<EquipmentIndex>();
            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
                if (session.WeaponChoices[slotIndex].IsKeepAuto)
                    autoSlots.Add(EquipmentIndex.Weapon0 + slotIndex);

            if (autoSlots.Count == 0) return;

            // Auto slots start clean; whatever vanilla left there is not a choice
            foreach (var slot in autoSlots)
                equipment[slot] = default;

            // What the player already chose is carried, so a fill-in never hands
            // over a second copy of a weapon that is already in another slot. A
            // repeated class in the plan is deliberate, though: two stacks of
            // javelins are two stacks, so ammunition and throwing weapons are
            // allowed to come back the same.
            var carried = EquipmentStep.CarriedIds(equipment);

            foreach (var choice in PlanLoadout(hero, session, equipment, autoSlots.Count))
            {
                if (autoSlots.Count == 0) break;

                // A slot the player was promised comes back with something in it,
                // so a class the stores cannot answer without repeating themselves
                // is asked again allowing the repeat before the slot is given up
                var item = GearQuery.QuartermasterPick(choice, culture, tier, session,
                               GearQuery.Stacks(choice) ? null : carried)
                           ?? GearQuery.QuartermasterPick(choice, culture, tier, session);
                if (item == null) continue;

                var slot = autoSlots[0];
                autoSlots.RemoveAt(0);
                equipment[slot] = new EquipmentElement(item);
                carried.Add(item.StringId);
                CSLogger.Info($"SmartQuartermaster: [{slot}] {item.Name} ({choice}).");
            }

            foreach (var slot in autoSlots)
                CSLogger.Warn($"SmartQuartermaster: [{slot}] left empty; " +
                              "nothing this culture and tier can reach fits any class the plan named.");
        }

        /// <summary>
        ///     The desired classes in fill order, given what is already carried, and
        ///     never fewer of them than the slots this fill was handed.
        ///
        ///     Both the option titled for the quartermaster and every archetype rung
        ///     state how many slots the stores fill, so the plan owes an entry for
        ///     each of them. It used to owe nothing: every branch below is gated on
        ///     what the character already carries, so a set that arrived complete
        ///     planned nothing at all and the slots it left over came back empty
        ///     under a panel that had counted them.
        /// </summary>
        private static List<WeaponClassChoice> PlanLoadout(Hero hero, CharacterCreationSession session,
            Equipment equipment, int wanted)
        {
            var plan = new List<WeaponClassChoice>();

            bool hasMelee = AnyWeapon(equipment, IsMelee);
            bool hasOneHanded = AnyWeapon(equipment, w => IsMelee(w) && IsOneHanded(w));
            bool hasShield = AnyWeapon(equipment, w => w is WeaponClass.SmallShield or WeaponClass.LargeShield);
            bool hasBow = AnyWeapon(equipment, w => w == WeaponClass.Bow);
            bool hasCrossbow = AnyWeapon(equipment, w => w == WeaponClass.Crossbow);
            bool hasArrows = AnyWeapon(equipment, w => w == WeaponClass.Arrow);
            bool hasBolts = AnyWeapon(equipment, w => w == WeaponClass.Bolt);
            bool hasThrowing = AnyWeapon(equipment,
                w => w is WeaponClass.ThrowingAxe or WeaponClass.Javelin or WeaponClass.ThrowingKnife
                    or WeaponClass.Stone);

            // Ammunition for a chosen ranged weapon always comes first
            if (hasBow && !hasArrows) plan.Add(WeaponClassChoice.Arrows);
            if (hasCrossbow && !hasBolts) plan.Add(WeaponClassChoice.Bolts);

            int oneHanded = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.OneHanded);
            int twoHanded = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.TwoHanded);
            int polearm = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Polearm);
            int bow = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Bow);
            int crossbow = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Crossbow);
            int throwing = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Throwing);
            int riding = NarrativeStep.ExpectedSkillValue(session, DefaultSkills.Riding);

            int topMelee = System.Math.Max(oneHanded, System.Math.Max(twoHanded, polearm));
            bool meleeIsOneHanded = false;

            if (!hasMelee)
            {
                if (polearm >= oneHanded && polearm >= twoHanded)
                {
                    bool mounted = riding >= 50 && !equipment[EquipmentIndex.Horse].IsEmpty;
                    plan.Add(mounted ? WeaponClassChoice.Lance : WeaponClassChoice.Spear);
                }
                else if (twoHanded > oneHanded)
                {
                    plan.Add(CSRandom.Next(2) == 0
                        ? WeaponClassChoice.TwoHandedSword
                        : WeaponClassChoice.TwoHandedAxe);
                }
                else
                {
                    plan.Add(PickOneHanded());
                    meleeIsOneHanded = true;
                }
            }

            // A ranged sidearm only when the skill genuinely supports one
            int topRanged = System.Math.Max(bow, System.Math.Max(crossbow, throwing));
            bool wantsRanged = topRanged >= RangedSkillFloor && topRanged * 2 >= topMelee;
            if (wantsRanged && !hasBow && !hasCrossbow && !hasThrowing)
            {
                if (bow >= crossbow && bow >= throwing)
                {
                    plan.Add(WeaponClassChoice.Bow);
                    plan.Add(WeaponClassChoice.Arrows);
                }
                else if (crossbow >= throwing)
                {
                    plan.Add(WeaponClassChoice.Crossbow);
                    plan.Add(WeaponClassChoice.Bolts);
                }
                else
                {
                    plan.Add(WeaponClassChoice.Javelin);
                }
            }

            if ((hasOneHanded || meleeIsOneHanded) && !hasShield)
                plan.Add(WeaponClassChoice.LargeShield);

            // Throwing-focused characters carry a second stack
            if (throwing >= topMelee && throwing >= RangedSkillFloor)
                plan.Add(WeaponClassChoice.Javelin);

            Reserve(plan, equipment, wanted, hasOneHanded || meleeIsOneHanded, hasShield,
                hasBow, hasCrossbow);

            return plan;
        }

        /// <summary>
        ///     What a store hands over once the kit itself is complete, until every
        ///     slot the fill was given has a class against it.
        ///
        ///     A sidearm first, because a soldier with no blade in reach is the one
        ///     gap worth closing before any second stack; then a shield for the hand
        ///     that blade frees; then more shot for whatever this character shoots,
        ///     which is the one thing a quartermaster can always hand out twice.
        /// </summary>
        private static void Reserve(List<WeaponClassChoice> plan, Equipment equipment, int wanted,
            bool oneHandedInHand, bool shieldInHand, bool bowInHand, bool crossbowInHand)
        {
            if (plan.Count >= wanted) return;

            bool blade = oneHandedInHand || plan.Exists(IsOneHandedChoice);
            if (!blade && plan.Count < wanted)
            {
                plan.Add(PickOneHanded());
                blade = true;
            }

            bool shield = shieldInHand || plan.Contains(WeaponClassChoice.LargeShield)
                                       || plan.Contains(WeaponClassChoice.SmallShield);
            if (blade && !shield && plan.Count < wanted)
                plan.Add(WeaponClassChoice.LargeShield);

            var spare = bowInHand || plan.Contains(WeaponClassChoice.Bow)
                ? WeaponClassChoice.Arrows
                : crossbowInHand || plan.Contains(WeaponClassChoice.Crossbow)
                    ? WeaponClassChoice.Bolts
                    : WeaponClassChoice.Javelin;

            while (plan.Count < wanted)
                plan.Add(spare);
        }

        private static bool IsOneHandedChoice(WeaponClassChoice choice) =>
            choice is WeaponClassChoice.OneHandedSword or WeaponClassChoice.OneHandedAxe
                or WeaponClassChoice.Mace;

        private static WeaponClassChoice PickOneHanded()
        {
            return CSRandom.Next(3) switch
            {
                0 => WeaponClassChoice.OneHandedSword,
                1 => WeaponClassChoice.OneHandedAxe,
                _ => WeaponClassChoice.Mace
            };
        }

        private static bool AnyWeapon(Equipment equipment, System.Func<WeaponClass, bool> predicate)
        {
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Weapon3; slot++)
            {
                var item = equipment[slot].Item;
                if (item?.PrimaryWeapon != null && predicate(item.PrimaryWeapon.WeaponClass))
                    return true;
            }

            return false;
        }

        private static bool IsMelee(WeaponClass weaponClass)
        {
            return weaponClass is WeaponClass.OneHandedSword or WeaponClass.OneHandedAxe or WeaponClass.Mace
                or WeaponClass.Pick or WeaponClass.Dagger or WeaponClass.TwoHandedSword
                or WeaponClass.TwoHandedAxe or WeaponClass.TwoHandedMace or WeaponClass.OneHandedPolearm
                or WeaponClass.TwoHandedPolearm or WeaponClass.LowGripPolearm;
        }

        private static bool IsOneHanded(WeaponClass weaponClass)
        {
            return weaponClass is WeaponClass.OneHandedSword or WeaponClass.OneHandedAxe or WeaponClass.Mace
                or WeaponClass.Pick or WeaponClass.Dagger or WeaponClass.OneHandedPolearm;
        }
    }
}
