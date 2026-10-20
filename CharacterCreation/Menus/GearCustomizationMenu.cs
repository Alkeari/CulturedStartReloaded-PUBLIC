using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     What you wear. This screen used to be eleven commands, one per slot, and
    ///     then three doors into the Start Editor's gear tabs. Neither is a chapter:
    ///     the guided route asks how you carry your station and hands the answer to
    ///     the quartermaster, who already dresses for culture and standing. Picking
    ///     a helm by name belongs to the Start Editor, which does it with a visible
    ///     list this stage cannot draw.
    ///
    ///     The bearing is the only thing here that moves the harness, and it moves
    ///     it independently of the purse and the warband, which is what lets a
    ///     company of many men ride out in poor mail and a lone rider ride out in
    ///     good. Whether the bottom of that span is worth having is a start type's
    ///     own equipment tier floor, in the settings.
    /// </summary>
    public static class GearCustomizationMenu
    {
        private static EquipmentPreviewService? _previewService;

        /// <summary>
        ///     What a name is worth, which is what decides how a life dressed. A
        ///     life nobody has heard of never learned to wear a fortune, and a life
        ///     spoken of in halls cannot turn up looking like it was not.
        /// </summary>
        private static readonly Func<LifeProfile, int> Name =
            profile => profile.Score(LifeProfile.Lean.Standing);

        /// <summary>
        ///     Dressing your own station is never shut. Every life has one, however
        ///     small, and a chapter that can offer nothing is not a chapter; the two
        ///     claims either side of it are what the life has to earn.
        /// </summary>
        private static readonly LifeBand[] Windows =
        {
            new("cs_gear_plain", 2, LifeGate.Open, 8,
                "{=CSR_Gear_Plain_Shut}Plain and hard wearing: your name is spoken in rooms where arriving in that would be an insult of its own."),
            new("cs_gear_station", 6, LifeGate.Open, LifeGate.NoTop, "{=!}"),
            new("cs_gear_finest", 11, 6, LifeGate.NoTop,
                "{=CSR_Gear_Finest_Shut}Everything you own on your back: you never owned enough for that to be worth the doing.")
        };

        private static readonly ArmorBearing[] Bearings =
            { ArmorBearing.Plain, ArmorBearing.Station, ArmorBearing.Finest };

        private static readonly (string TitleKey, string DescKey)[] Copy =
        {
            ("{=CSR_Gear_Plain}Plain and Hard Wearing",
                "{=CSR_Gear_Plain_Desc}Nothing on you is worth cutting off a corpse. It has kept you alive in places where that mattered."),
            ("{=CSR_Gear_Station}As Your Station Demands",
                "{=CSR_Gear_Station_Desc}What someone of your standing is expected to be seen in, and no more than that."),
            ("{=CSR_Gear_Finest}Everything you own, on your back",
                "{=CSR_Gear_Finest_Desc}You spent it on harness and horse. There is nothing left in the strongbox, and everyone can see there did not need to be.")
        };

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        /// <summary>The live preview service, for the Start Editor's gear rows.</summary>
        public static EquipmentPreviewService? PreviewService => _previewService;

        public static void AddGearCustomizationMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_GearMenu_Desc_Revamped}The stores will dress you for your culture and your standing. The only question is how much of your standing you want on your back.");

            var menu = new NarrativeMenu(
                "cs_gear_menu",
                CreationFlow.DeclaredPrevious("cs_gear_menu"),
                CreationFlow.DeclaredNext("cs_gear_menu"),
                new TextObject("{=CSR_GearMenu_Title}Armor and Mount"),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddDerivedOption(menu);

            for (int i = 0; i < Windows.Length; i++)
                AddBearing(menu, description, Windows[i], Bearings[i], Copy[i].TitleKey, Copy[i].DescKey);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     The option that asks nothing: a life at court dresses like it, a life
        ///     in the wilds or the alleys does not. It takes the bearing the life
        ///     sits nearest among those it leaves open, so this row can never name a
        ///     bearing the gates beside it have shut.
        /// </summary>
        private static void AddDerivedOption(NarrativeMenu menu)
        {
            var description = new TextObject("{=CSR_Gear_Life_Desc}{REASON}");

            ChoiceEffects.Declare("cs_gear_life", () =>
            {
                var read = new TextObject("{=CSR_Panel_Gear_Read}Why: your life reads as {BEARING}");
                read.SetTextVariable("BEARING", new TextObject(Copy[DerivedIndex()].TitleKey).ToString());
                return ChoiceEffects.Stated(read.ToString(), Effect(Derived()));
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_gear_life",
                new TextObject("{=CSR_Gear_Life}However Your Life Dressed You"),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("REASON", DerivedReason())),
                m =>
                {
                    Apply(Derived());
                    MenuText.Remember("cs_gear_menu", "{=CSR_Gear_Life}However Your Life Dressed You");
                },
                m => { }
            ));
        }

        private static void AddBearing(NarrativeMenu menu, TextObject chapterDescription, LifeBand window,
            ArmorBearing bearing, string titleKey, string descKey)
        {
            var description = new TextObject(descKey);
            var captured = bearing;
            var capturedTitle = titleKey;
            var capturedWindow = window;

            ChoiceEffects.Declare(window.Id, () => Effect(captured));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                window.Id,
                new TextObject(titleKey),
                description,
                args => { },
                // The description quotes nothing now, so the render owes only the
                // gate, which also writes the chapter's note for every shut bearing
                m => LifeGate.Offers(chapterDescription, Windows, capturedWindow, Name),
                m =>
                {
                    Apply(captured);
                    MenuText.Remember("cs_gear_menu", capturedTitle);
                },
                m => { }
            ));
        }

        private static ArmorBearing Derived() => Bearings[DerivedIndex()];

        /// <summary>
        ///     Which bearing the life sits nearest. The index rather than the
        ///     bearing, because the panel names the row and the pick takes its
        ///     bearing, and both are one reading of the life.
        /// </summary>
        private static int DerivedIndex()
        {
            var nearest = LifeGate.Nearest(Windows, Name);
            for (int i = 0; i < Windows.Length; i++)
                if (ReferenceEquals(Windows[i], nearest))
                    return i;

            return 1;
        }

        /// <summary>
        ///     What this bearing dresses the character in, in the one figure that
        ///     decides it. The tier comes from the same
        ///     <see cref="EquipmentStep.TierFor"/> the apply reads, so the panel
        ///     cannot promise a standing the stores will not hand over.
        ///
        ///     The span is stated only where the settings can be read. Printing a
        ///     fallback span would be this file's own copy of another file's
        ///     default, which is the shape of a number that goes quietly wrong.
        /// </summary>
        private static string Effect(ArmorBearing bearing)
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;
            int tier = EquipmentStep.TierFor(session, settings, bearing);

            var range = settings?.GetEquipmentTierRange(session.SelectedStartType);
            TextObject dressed;
            if (range == null)
            {
                dressed = new TextObject("{=CSR_Panel_Gear_Tier}Gear: tier {TIER}, from the stores");
            }
            else
            {
                dressed = new TextObject(
                    "{=CSR_Panel_Gear_TierSpan}Gear: tier {TIER}, of the {MIN} to {MAX} this start allows");
                dressed.SetTextVariable("MIN", range.Value.min);
                dressed.SetTextVariable("MAX", range.Value.max);
            }

            dressed.SetTextVariable("TIER", tier);

            var reach = new TextObject(
                "{=CSR_Panel_Gear_Reach}Gear: that tier sets your armor, your mount, your civilian clothes and every weapon the quartermaster fills");

            return ChoiceEffects.Stated(dressed.ToString(), reach.ToString());
        }

        private static string DerivedReason()
        {
            string key = Derived() switch
            {
                ArmorBearing.Finest =>
                    "{=CSR_Gear_Why_Finest}You were raised where appearing poor was its own kind of failure, and it never left you.",
                ArmorBearing.Plain =>
                    "{=CSR_Gear_Why_Plain}Nothing in your life ever rewarded looking wealthy, and several parts of it punished it.",
                _ =>
                    "{=CSR_Gear_Why_Station}You dress the way people of your standing dress, without thinking about it much."
            };

            return new TextObject(key).ToString();
        }

        private static void Apply(ArmorBearing bearing)
        {
            var session = CreationSession.Current;
            session.Bearing = bearing;

            var settings = GlobalSettings<CSSettings>.Instance;
            _previewService?.GenerateArmorPreview(
                session.SelectedCulture, EquipmentStep.EffectiveTier(session, settings));
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
