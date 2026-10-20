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
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     What you wear. This screen used to be eleven commands, one per slot,
    ///     each opening a picker and some of them opening a chooser first. On a
    ///     stage whose list can only be read by clicking, that made browsing
    ///     destructive and dressing a chore. The editor's gear tabs already show
    ///     every slot at once with Set All Slots and a per-row way back to the
    ///     quartermaster, so each option here opens the right tab.
    /// </summary>
    public static class GearCustomizationMenu
    {
        private static EquipmentPreviewService? _previewService;

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        /// <summary>The live preview service, for the Start Editor's gear rows.</summary>
        public static EquipmentPreviewService? PreviewService => _previewService;

        public static void AddGearCustomizationMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_gear_menu",
                CreationFlow.DeclaredPrevious("cs_gear_menu"),
                CreationFlow.DeclaredNext("cs_gear_menu"),
                new TextObject("{=CSR_GearMenu_Title}Armor and Mount"),
                new TextObject(
                    "{=CSR_GearMenu_Desc}The quartermaster has outfitted you for your station. Keep it, or choose any piece exactly."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            var keepDescription = new TextObject(
                "{=CSR_Gear_KeepQuartermaster_Desc}Everything you wear is chosen for your culture and standing, and changes if your standing does. {NOW}");

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_gear_keep_auto",
                new TextObject("{=CSR_Gear_KeepQuartermaster}Keep the Quartermaster's Gear"),
                keepDescription,
                args => { },
                MenuText.Live(keepDescription, d => d.SetTextVariable("NOW", Summary())),
                m => ResetPicks(),
                m => { }
            ));

            AddTabOption(menu, "cs_gear_battle", "battle",
                "{=CSR_Scope_BattleGear}Your Armor and Mount",
                "{=CSR_Gear_Battle}Choose Your Armor and Mount",
                "{=CSR_Gear_Battle_Desc}Every battle slot at once: helm, body, legs, gloves, cape, horse, harness and banner. {NOW}",
                OutfitKind.Battle, null);

            AddTabOption(menu, "cs_gear_civilian", "civilian",
                "{=CSR_Scope_Civilian}Your Town Clothes",
                "{=CSR_Gear_Civilian}Choose Your Town Clothes",
                "{=CSR_Gear_Civilian_Desc}What you wear where armor would be an insult, or a confession. {NOW}",
                OutfitKind.Civilian, null);

            AddTabOption(menu, "cs_gear_stealth", "stealth",
                "{=CSR_Scope_Stealth}Your Stealth Outfit",
                "{=CSR_Gear_Stealth}Choose Your Stealth Outfit",
                "{=CSR_Gear_Stealth_Desc}What you wear when nobody is meant to know you were there. {NOW}",
                OutfitKind.Stealth, null);

            manager.AddNewMenu(menu);
        }

        private static void AddTabOption(NarrativeMenu menu, string id, string tabKey, string scopeTitleKey,
            string titleKey, string descKey, OutfitKind kind, System.Func<bool>? visible)
        {
            var description = new TextObject(descKey);
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("NOW", OutfitSummary(kind)), visible),
                m => Editor.StartEditorScreen.Open(
                    Editor.EditorScope.For(scopeTitleKey, tabKey), false),
                m => { }
            ));
        }

        private static string Summary()
        {
            int picks = CreationSession.Current.ExactOutfit.Count(e => e.Value != null)
                        + (CreationSession.Current.ExactBanner != null ? 1 : 0);

            return picks == 0
                ? new TextObject("{=CSR_Gear_NowAuto}Nothing is chosen by hand, so this is already what you have.")
                    .ToString()
                : MenuText.Count(picks,
                    "{=CSR_Gear_NowOne}Choosing this clears {COUNT} piece you picked yourself.",
                    "{=CSR_Gear_NowMany}Choosing this clears {COUNT} pieces you picked yourself.");
        }

        private static string OutfitSummary(OutfitKind kind)
        {
            int picks = CreationSession.Current.ExactOutfit.Count(e => e.Key.Kind == kind && e.Value != null);

            return picks == 0
                ? new TextObject("{=CSR_Gear_SlotsAuto}Every slot is the quartermaster's for now.").ToString()
                : MenuText.Count(picks,
                    "{=CSR_Gear_SlotsOne}{COUNT} slot chosen by hand.",
                    "{=CSR_Gear_SlotsMany}{COUNT} slots chosen by hand.");
        }

        private static void ResetPicks()
        {
            var session = CreationSession.Current;
            session.ExactOutfit.Clear();
            session.ExactBanner = null;

            var settings = GlobalSettings<CSSettings>.Instance;
            _previewService?.GenerateArmorPreview(
                session.SelectedCulture, EquipmentStep.EffectiveTier(session, settings));

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=CSR_Gear_ResetDone}All gear picks cleared; the quartermaster decides again.")
                    .ToString()));
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
