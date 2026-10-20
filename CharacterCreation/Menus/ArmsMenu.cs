using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     How the player fights, as one written choice instead of four slot
    ///     screens of twenty classes each. Every set is coherent by construction,
    ///     so a bow always arrives with arrows and a crossbow with bolts, which
    ///     the per-slot screens could never guarantee. Filling the four slots by
    ///     hand belongs to the Start Editor.
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

            AddTrainedOption(menu);

            foreach (var (id, titleKey, descKey, loadout) in Archetypes)
            {
                var captured = loadout;
                var capturedTitle = titleKey;

                ChoiceEffects.Declare(id, () => Effect(captured));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    id,
                    new TextObject(titleKey),
                    new TextObject(descKey),
                    args => { },
                    m => true,
                    m =>
                    {
                        ApplyLoadout(captured);
                        MenuText.Remember("cs_arms_menu", capturedTitle);
                    },
                    m => { }
                ));
            }

            ChoiceEffects.Declare("cs_arms_quartermaster", () => Effect(new WeaponClassChoice[0]));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_arms_quartermaster",
                new TextObject("{=CSR_Arms_Quartermaster}Let the Quartermaster Arm You"),
                new TextObject(
                    "{=CSR_Arms_Quartermaster_Desc}Take whatever the stores hand out, chosen to suit your culture and standing."),
                args => { },
                m => true,
                m =>
                {
                    ApplyLoadout(new WeaponClassChoice[0]);
                    MenuText.Remember("cs_arms_menu",
                        "{=CSR_Arms_Quartermaster}Let the Quartermaster Arm You");
                },
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     The option that asks nothing: the life path already put something in
        ///     these hands, and this takes down whichever set is built around it.
        /// </summary>
        private static void AddTrainedOption(NarrativeMenu menu)
        {
            var description = new TextObject("{=CSR_Arms_Trained_Desc}{REASON}");

            ChoiceEffects.Declare("cs_arms_trained", () =>
            {
                var set = TrainedSet();
                var read = new TextObject("{=CSR_Panel_Arms_Read}Why: your life reads as {SET}");
                read.SetTextVariable("SET", new TextObject(set.TitleKey).ToString());
                return ChoiceEffects.Stated(read.ToString(), Effect(set.Loadout));
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_arms_trained",
                new TextObject("{=CSR_Arms_Trained}What Your Hands Already Know"),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("REASON", TrainedReason())),
                m =>
                {
                    ApplyLoadout(TrainedSet().Loadout);
                    MenuText.Remember("cs_arms_menu", TrainedSet().TitleKey);
                },
                m => { }
            ));
        }

        /// <summary>
        ///     The archetype built around the weapon family the life path trained
        ///     for. An untrained path falls back to the line soldier's answer,
        ///     which is the first entry and the one the stores would hand out.
        /// </summary>
        private static (string TitleKey, WeaponClassChoice[] Loadout) TrainedSet()
        {
            string wanted = LifeProfile.From(CreationSession.Current).Weapon switch
            {
                LifeProfile.Trained.Spear => "cs_arms_spear",
                LifeProfile.Trained.Bow => "cs_arms_bow",
                LifeProfile.Trained.Crossbow => "cs_arms_crossbow",
                LifeProfile.Trained.Thrown => "cs_arms_thrown",
                LifeProfile.Trained.Lance => "cs_arms_lance",
                LifeProfile.Trained.GreatWeapon => "cs_arms_two_handed",
                _ => "cs_arms_sword_shield"
            };

            foreach (var (id, titleKey, _, loadout) in Archetypes)
                if (string.Equals(id, wanted, System.StringComparison.Ordinal))
                    return (titleKey, loadout);

            return (Archetypes[0].TitleKey, Archetypes[0].Loadout);
        }

        private static string TrainedReason()
        {
            string key = LifeProfile.From(CreationSession.Current).Weapon switch
            {
                LifeProfile.Trained.Spear =>
                    "{=CSR_Arms_Why_Spear}Everything you were ever taught started with a shaft and a point on the end of it.",
                LifeProfile.Trained.Bow =>
                    "{=CSR_Arms_Why_Bow}You have been drawing a bow since before you were strong enough to hold one at full draw.",
                LifeProfile.Trained.Crossbow =>
                    "{=CSR_Arms_Why_Crossbow}You learned on a windlass and a stirrup, the way townsmen do.",
                LifeProfile.Trained.Thrown =>
                    "{=CSR_Arms_Why_Thrown}You have thrown things at things that needed hitting your whole life.",
                LifeProfile.Trained.Lance =>
                    "{=CSR_Arms_Why_Lance}You were put on a horse young, and everything you know assumes there is one under you.",
                LifeProfile.Trained.GreatWeapon =>
                    "{=CSR_Arms_Why_Great}You never learned to fight from behind anything, and you never wanted to.",
                _ =>
                    "{=CSR_Arms_Why_Blade}Sword and shield: the first thing anybody teaches, and the last thing that fails you."
            };

            return new TextObject(key).ToString();
        }

        /// <summary>
        ///     What goes in the four slots, class by class, and what the stores
        ///     decide instead.
        ///
        ///     A class rather than an item, because the item is a draw out of the
        ///     several nearest the standing and naming one of them would be a
        ///     promise the stores do not make. The class and the tier ARE settled,
        ///     and the panel says exactly that far and no further.
        /// </summary>
        private static string Effect(WeaponClassChoice[] loadout)
        {
            var session = CreationSession.Current;
            int slots = session.WeaponChoices.Length;
            int tier = WeaponClasses.PreviewTier(session);

            if (loadout.Length == 0)
            {
                var auto = new TextObject(
                    "{=CSR_Panel_Arms_Auto}Gear: all {SLOTS} weapon slots, filled by the quartermaster at tier {TIER} to suit your culture");
                auto.SetTextVariable("SLOTS", slots);
                auto.SetTextVariable("TIER", tier);
                return auto.ToString();
            }

            var carried = new TextObject(
                "{=CSR_Panel_Arms_Carried}Gear: {WEAPONS}, each drawn at random from those of your culture nearest tier {TIER}");
            carried.SetTextVariable("WEAPONS",
                string.Join(", ", loadout.Select(WeaponClasses.ClassTitle)));
            carried.SetTextVariable("TIER", tier);

            int rest = slots - loadout.Length;
            if (rest <= 0) return carried.ToString();

            var filled = new TextObject("{=CSR_Panel_Arms_Rest}Gear: {SLOTS} left, filled by the quartermaster");
            filled.SetTextVariable("SLOTS", MenuText.Count(rest,
                "{=CSR_Panel_Arms_RestOne}{COUNT} weapon slot",
                "{=CSR_Panel_Arms_RestMany}{COUNT} weapon slots"));

            return ChoiceEffects.Stated(carried.ToString(), filled.ToString());
        }

        /// <summary>
        ///     Fills the slots the set names and leaves the rest to the stores.
        ///     An empty set is the pure quartermaster loadout.
        /// </summary>
        private static void ApplyLoadout(WeaponClassChoice[] loadout)
        {
            var session = CreationSession.Current;
            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
            {
                var gear = session.WeaponChoices[slotIndex];
                gear.Reset();
                gear.ExplicitlyEmpty = false;

                if (slotIndex < loadout.Length)
                    gear.WeaponClass = loadout[slotIndex];

                _previewService?.GenerateWeaponPreview(gear, EquipmentIndex.Weapon0 + slotIndex,
                    session.SelectedCulture, WeaponClasses.PreviewTier(session));
            }
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
