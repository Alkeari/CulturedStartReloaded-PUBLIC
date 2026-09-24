using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     How the player fights, as one written choice instead of four slot
    ///     screens of twenty classes each. Every set is coherent by construction,
    ///     so a bow always arrives with arrows and a crossbow with bolts, which
    ///     the per-slot screens could never guarantee. Choosing each slot by hand
    ///     is still here, one option down.
    /// </summary>
    public static class ArmsMenu
    {
        private static EquipmentPreviewService? _previewService;

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        private static readonly (string Id, string TitleKey, string DescKey, WeaponClassChoice[] Loadout)[]
            Archetypes =
            {
                ("cs_arms_sword_shield", "{=CSR_Arms_SwordShield}Sword and Shield",
                    "{=CSR_Arms_SwordShield_Desc}The line soldier's answer: a blade, a broad shield, and a javelin to open with.",
                    new[]
                    {
                        WeaponClassChoice.OneHandedSword, WeaponClassChoice.LargeShield,
                        WeaponClassChoice.Javelin
                    }),
                ("cs_arms_spear", "{=CSR_Arms_Spear}The Long Spear",
                    "{=CSR_Arms_Spear_Desc}Reach over the shield wall, with a sword and shield for when they close.",
                    new[]
                    {
                        WeaponClassChoice.Spear, WeaponClassChoice.OneHandedSword,
                        WeaponClassChoice.LargeShield
                    }),
                ("cs_arms_two_handed", "{=CSR_Arms_TwoHanded}Both Hands on the Haft",
                    "{=CSR_Arms_TwoHanded_Desc}No shield, no hesitation: a great blade and something to throw before the charge.",
                    new[] { WeaponClassChoice.TwoHandedSword, WeaponClassChoice.Javelin }),
                ("cs_arms_thrown", "{=CSR_Arms_Thrown}Thrown Steel",
                    "{=CSR_Arms_Thrown_Desc}Two sheaves of javelins spent before the lines meet, then sword and buckler.",
                    new[]
                    {
                        WeaponClassChoice.Javelin, WeaponClassChoice.Javelin,
                        WeaponClassChoice.OneHandedSword, WeaponClassChoice.SmallShield
                    }),
                ("cs_arms_bow", "{=CSR_Arms_Bow}The Bow",
                    "{=CSR_Arms_Bow_Desc}A bow and two quivers, with a blade for the moment they reach you.",
                    new[]
                    {
                        WeaponClassChoice.Bow, WeaponClassChoice.Arrows, WeaponClassChoice.Arrows,
                        WeaponClassChoice.OneHandedSword
                    }),
                ("cs_arms_crossbow", "{=CSR_Arms_Crossbow}The Crossbow",
                    "{=CSR_Arms_Crossbow_Desc}A crossbow and two cases of bolts; slow to load, and it does not care about armor.",
                    new[]
                    {
                        WeaponClassChoice.Crossbow, WeaponClassChoice.Bolts, WeaponClassChoice.Bolts,
                        WeaponClassChoice.OneHandedSword
                    }),
                ("cs_arms_lance", "{=CSR_Arms_Lance}The Charge",
                    "{=CSR_Arms_Lance_Desc}A couched lance for the first pass, sword and shield for every pass after.",
                    new[]
                    {
                        WeaponClassChoice.Lance, WeaponClassChoice.OneHandedSword,
                        WeaponClassChoice.LargeShield
                    })
            };

        public static void AddArmsMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_arms_menu",
                CreationFlow.DeclaredPrevious("cs_arms_menu"),
                CreationFlow.DeclaredNext("cs_arms_menu"),
                new TextObject("{=CSR_Arms_Title}How You Fight"),
                new TextObject("{=CSR_Arms_Desc}Take down what you know how to use. The quartermaster finds the rest."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var (id, titleKey, descKey, loadout) in Archetypes)
            {
                var captured = loadout;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    id,
                    new TextObject(titleKey),
                    new TextObject(descKey),
                    args => { },
                    m => true,
                    m => ApplyLoadout(captured),
                    m => { }
                ));
            }

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_arms_quartermaster",
                new TextObject("{=CSR_Arms_Quartermaster}Let the Quartermaster Arm You"),
                new TextObject(
                    "{=CSR_Arms_Quartermaster_Desc}Take whatever the stores hand out, chosen to suit your culture and standing."),
                args => { },
                m => true,
                m => ApplyLoadout(new WeaponClassChoice[0]),
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_arms_by_slot",
                new TextObject("{=CSR_Arms_BySlot}Choose Each Weapon Yourself"),
                new TextObject(
                    "{=CSR_Arms_BySlot_Desc}Open the four weapon slots one at a time and pick the class, or the exact item, for each."),
                args => { },
                m => true,
                m => CreationSession.Current.ChooseWeaponsIndividually = true,
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     Fills the slots the set names and leaves the rest to the stores.
        ///     An empty set is the pure quartermaster loadout.
        /// </summary>
        private static void ApplyLoadout(WeaponClassChoice[] loadout)
        {
            var session = CreationSession.Current;
            session.ChooseWeaponsIndividually = false;

            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
            {
                var gear = session.WeaponChoices[slotIndex];
                gear.Reset();
                gear.ExplicitlyEmpty = false;

                if (slotIndex < loadout.Length)
                    gear.WeaponClass = loadout[slotIndex];

                _previewService?.GenerateWeaponPreview(gear, EquipmentIndex.Weapon0 + slotIndex,
                    session.SelectedCulture, GearSelectionMenus.PreviewTier(session));
            }
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
