using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Weapon slot menus with explicit weapon classes (spears and lances are
    ///     different things here) and an exact-item picker: a popup armory list of
    ///     every qualifying item, generated live from the item database.
    /// </summary>
    public static class GearSelectionMenus
    {
        private static EquipmentPreviewService? _previewService;

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

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

        public static void AddGearMenus(CharacterCreationManager manager)
        {
            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
                AddWeaponSlotMenu(manager, slotIndex);
        }

        private static void AddWeaponSlotMenu(CharacterCreationManager manager, int slotIndex)
        {
            string menuId = $"cs_weapon{slotIndex + 1}_menu";
            var weaponSlot = EquipmentIndex.Weapon0 + slotIndex;

            var title = new TextObject("{=CSR_Gear_SlotTitle}Weapon Slot {SLOT}");
            title.SetTextVariable("SLOT", slotIndex + 1);
            var description = new TextObject(
                "{=CSR_Gear_SlotDesc}Choose an item class for weapon slot {SLOT}, then optionally pick the exact item. Clicking a class again re-rolls the quartermaster's pick.");
            description.SetTextVariable("SLOT", slotIndex + 1);

            var menu = new NarrativeMenu(
                menuId,
                CreationFlow.DeclaredPrevious(menuId),
                CreationFlow.DeclaredNext(menuId),
                title,
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                $"{menuId}_none",
                new TextObject("{=CSR_Gear_SlotNone}None (Empty Slot)"),
                new TextObject("{=CSR_Gear_SlotNone_Desc}Carry nothing in this slot."),
                args => { },
                m => true,
                m =>
                {
                    var gear = CreationSession.Current.WeaponChoices[slotIndex];
                    gear.Reset();
                    gear.ExplicitlyEmpty = true;
                    _previewService?.SetSlotEmpty(weaponSlot);
                },
                m => { }
            ));

            foreach (var (choice, optionTitle, optionDesc) in ClassOptions)
            {
                var captured = choice;
                bool isShield = captured is WeaponClassChoice.SmallShield or WeaponClassChoice.LargeShield;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"{menuId}_{captured.ToString().ToLowerInvariant()}",
                    isShield
                        ? new TextObject("{=!}" + ClassTitle(captured))
                        : new TextObject(optionTitle),
                    isShield && GearQuery.ShieldsMerged
                        ? new TextObject("{=CSR_Gear_Shield_Desc}A shield for your off hand.")
                        : new TextObject(optionDesc),
                    args => { },
                    // Merged shields fold into one option; the large entry hides
                    m => captured != WeaponClassChoice.LargeShield || !GearQuery.ShieldsMerged,
                    m => SelectClass(slotIndex, weaponSlot, captured),
                    m => { }
                ));
            }

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                $"{menuId}_pick_exact",
                new TextObject("{=CSR_Gear_PickExact}Choose the Exact Item"),
                new TextObject(
                    "{=CSR_Gear_PickExact_Desc}Open the armory list and pick a specific qualifying item by name."),
                args => { },
                // Unavailable until a class is chosen, so the slot can never be
                // skipped past with a dead selection
                m => !CreationSession.Current.WeaponChoices[slotIndex].IsKeepAuto,
                m => OpenExactItemPicker(slotIndex, weaponSlot),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        private static void SelectClass(int slotIndex, EquipmentIndex weaponSlot, WeaponClassChoice choice)
        {
            var gear = CreationSession.Current.WeaponChoices[slotIndex];
            gear.WeaponClass = choice;
            gear.ExactItem = null;
            gear.ExplicitlyEmpty = false;
            UpdatePreview(weaponSlot, gear);
        }

        /// <summary>
        ///     The Start Editor's armory picker, search bar, filters, tier
        ///     toggles, and shield strap filter included.
        /// </summary>
        private static void OpenExactItemPicker(int slotIndex, EquipmentIndex weaponSlot)
        {
            var session = CreationSession.Current;
            var gear = session.WeaponChoices[slotIndex];

            if (gear.IsKeepAuto)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject(
                            "{=CSR_Gear_PickClassFirst}Choose a weapon class first, then pick the exact item.")
                        .ToString()));
                return;
            }

            int tier = PreviewTier(session);
            var items = GearQuery.QualifyingItems(gear.WeaponClass, session.SelectedCulture, tier, session);
            if (items.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=CSR_Gear_NoItems}No qualifying items found for that class.").ToString()));
                return;
            }

            Editor.ItemPickerScreen.Open(
                new TextObject("{=CSR_Gear_Picker_Title}Armory").ToString(), items,
                (choice, item) =>
                {
                    switch (choice)
                    {
                        case Editor.ItemPickChoice.Auto:
                            gear.ExactItem = null;
                            break;
                        case Editor.ItemPickChoice.None:
                            gear.Reset();
                            gear.ExplicitlyEmpty = true;
                            _previewService?.SetSlotEmpty(weaponSlot);
                            return;
                        case Editor.ItemPickChoice.Item when item != null:
                            gear.ExactItem = item;
                            break;
                        default:
                            return;
                    }

                    UpdatePreview(weaponSlot, gear);
                }, currentItem: gear.ExactItem);
        }

        private static void UpdatePreview(EquipmentIndex weaponSlot, GearChoice gear)
        {
            if (_previewService == null) return;

            var session = CreationSession.Current;
            _previewService.GenerateWeaponPreview(gear, weaponSlot, session.SelectedCulture, PreviewTier(session));
        }

        /// <summary>The tier the preview and the armory lists are built against.</summary>
        public static int PreviewTier(CharacterCreationSession session)
        {
            return EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
