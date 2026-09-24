using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The weapon usage classes and their player-facing titles. Spears and
    ///     lances are different things here. The guided route picks a whole
    ///     loadout in one written choice; the Start Editor fills slots one at a
    ///     time, and both read these titles.
    /// </summary>
    public static class WeaponClasses
    {
        private static readonly (WeaponClassChoice choice, string title, string desc)[] ClassOptions =
        {
            (WeaponClassChoice.KeepAuto,
                "{=CSR_Gear_KeepAuto}Keep Auto Equipment",
                "{=CSR_Gear_KeepAuto_Desc}Keep whatever gear is generated for this slot."),
            (WeaponClassChoice.OneHandedSword,
                "{=CSR_Gear_OneHandedSword}One-Handed Sword",
                "{=CSR_Gear_OneHandedSword_Desc}A single-handed blade for sword and shield work."),
            (WeaponClassChoice.OneHandedAxe,
                "{=CSR_Gear_OneHandedAxe}One-Handed Axe",
                "{=CSR_Gear_OneHandedAxe_Desc}A single-handed axe that punishes shields."),
            (WeaponClassChoice.Mace,
                "{=CSR_Gear_Mace}Mace",
                "{=CSR_Gear_Mace_Desc}A blunt weapon that batters armor and knocks men out."),
            (WeaponClassChoice.TwoHandedSword,
                "{=CSR_Gear_TwoHandedSword}Two-Handed Sword",
                "{=CSR_Gear_TwoHandedSword_Desc}A great blade with reach and sweeping power."),
            (WeaponClassChoice.TwoHandedAxe,
                "{=CSR_Gear_TwoHandedAxe}Two-Handed Axe",
                "{=CSR_Gear_TwoHandedAxe_Desc}A heavy two-handed axe or maul for devastating blows."),
            (WeaponClassChoice.Spear,
                "{=CSR_Gear_Spear}Spear",
                "{=CSR_Gear_Spear_Desc}A thrusting polearm for the battle line; not couchable."),
            (WeaponClassChoice.Lance,
                "{=CSR_Gear_Lance}Lance",
                "{=CSR_Gear_Lance_Desc}A couchable cavalry lance for the charge."),
            (WeaponClassChoice.ThrowingAxe,
                "{=CSR_Gear_ThrowingAxe}Throwing Axe",
                "{=CSR_Gear_ThrowingAxe_Desc}Heavy thrown axes with brutal short-range impact."),
            (WeaponClassChoice.Javelin,
                "{=CSR_Gear_Javelin}Javelin",
                "{=CSR_Gear_Javelin_Desc}Thrown spears that punch through armor and shields."),
            (WeaponClassChoice.ThrowingKnife,
                "{=CSR_Gear_ThrowingKnife}Throwing Knife",
                "{=CSR_Gear_ThrowingKnife_Desc}Light, fast thrown blades carried in numbers."),
            (WeaponClassChoice.Bow,
                "{=CSR_Gear_Bow}Bow",
                "{=CSR_Gear_Bow_Desc}A bow; pair it with a quiver of arrows."),
            (WeaponClassChoice.Crossbow,
                "{=CSR_Gear_Crossbow}Crossbow",
                "{=CSR_Gear_Crossbow_Desc}A crossbow; pair it with a case of bolts."),
            (WeaponClassChoice.SmallShield,
                "{=CSR_Gear_SmallShield}Small Shield",
                "{=CSR_Gear_SmallShield_Desc}A light, quick buckler or round shield."),
            (WeaponClassChoice.LargeShield,
                "{=CSR_Gear_LargeShield}Large Shield",
                "{=CSR_Gear_LargeShield_Desc}A broad shield with heavy coverage."),
            (WeaponClassChoice.Arrows,
                "{=CSR_Gear_Arrows}Arrows",
                "{=CSR_Gear_Arrows_Desc}A quiver of arrows for your bow."),
            (WeaponClassChoice.Bolts,
                "{=CSR_Gear_Bolts}Bolts",
                "{=CSR_Gear_Bolts_Desc}A case of bolts for your crossbow."),
            (WeaponClassChoice.Stone,
                "{=CSR_Gear_Stone}Stones",
                "{=CSR_Gear_Stone_Desc}A pouch of throwing stones; humble, silent, and free.")
        };

        /// <summary>The localized player-facing title of a usage class.</summary>
        public static string ClassTitle(WeaponClassChoice choice)
        {
            if (GearQuery.ShieldsMerged &&
                choice is WeaponClassChoice.SmallShield or WeaponClassChoice.LargeShield)
                return new TextObject("{=CSR_Gear_Shield}Shield").ToString();

            foreach (var (optionChoice, title, _) in ClassOptions)
                if (optionChoice == choice)
                    return new TextObject(title).ToString();
            return choice.ToString();
        }

        /// <summary>The tier the preview and the armory lists are built against.</summary>
        public static int PreviewTier(CharacterCreationSession session)
        {
            return EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance);
        }

    }
}
