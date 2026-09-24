using System;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     How many fighting men ride out with you. This is its own chapter, and it
    ///     comes after the household and the companions, because the party size the
    ///     bound may offer is what the clan can hold less the companions and relatives
    ///     already riding along. Asked in the means chapter, before either was chosen,
    ///     it offered seats that were later taken and the count was quietly cut down.
    /// </summary>
    public static class WarbandMenu
    {
        public static void AddWarbandMenu(CharacterCreationManager manager)
        {
            var description = new TextObject("{=CSR_Warband_Desc}Who rides out at your back? {NOW}");

            var menu = new NarrativeMenu(
                "cs_warband_menu",
                CreationFlow.DeclaredPrevious("cs_warband_menu"),
                CreationFlow.DeclaredNext("cs_warband_menu"),
                new TextObject("{=CSR_Warband_Title}Those Who Ride with You"),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddBand(menu, null, "cs_warband_none",
                "{=CSR_Warband_None}You Ride Alone",
                "{=CSR_Warband_None_Desc}No one at your back but your own resolve.");
            AddBand(menu, RangePreset.Low, "cs_warband_few",
                "{=CSR_Warband_Few}A Handful of Hands",
                "{=CSR_Warband_Few_Desc}Enough to hold a camp, not enough to hold a line.");
            AddBand(menu, RangePreset.Standard, "cs_warband_company",
                "{=CSR_Warband_Company}A Company Worth the Name",
                "{=CSR_Warband_Company_Desc}A body of men who can take the field and come home.");
            AddBand(menu, RangePreset.High, "cs_warband_warband",
                "{=CSR_Warband_Warband}A Warband",
                "{=CSR_Warband_Warband_Desc}Numbers enough that villages count them as they pass.");
            AddBand(menu, RangePreset.Maximum, "cs_warband_host",
                "{=CSR_Warband_Host}As Many As You Can Feed",
                "{=CSR_Warband_Host_Desc}Every seat your standing allows, and the mouths that come with them.");

            var exactDescription = new TextObject("{=CSR_Warband_Exact_Desc}Name the number yourself. {NOW}");
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_warband_exact",
                new TextObject("{=CSR_Warband_Exact}Count Them Yourself"),
                exactDescription,
                args => { },
                MenuText.Live(exactDescription, d => d.SetTextVariable("NOW", Summary())),
                m => PromptForCount(),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        private static void AddBand(NarrativeMenu menu, RangePreset? preset, string id,
            string titleKey, string descriptionKey)
        {
            var description = new TextObject(descriptionKey);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                m => true,
                m =>
                {
                    // Riding alone is the absence of a muster, not the smallest band the
                    // settings allow, whose floor can be a column of men
                    if (!preset.HasValue)
                    {
                        CreationSession.Current.CustomTroops = 0;
                        return;
                    }

                    CreationSession.Current.SelectedTroops = preset.Value;
                    CreationSession.Current.CustomTroops = null;
                },
                m => { }
            ));
        }

        /// <summary>
        ///     Every seat the clan can hold, less the one you occupy and the companions
        ///     and relatives already riding with you. Both are settled by now.
        /// </summary>
        public static int Ceiling(CharacterCreationSession session)
        {
            int ceiling = GameCaps.MaxTroopsLive(session.EffectiveClanTier, session) - 1
                          - session.StartingCompanions - FamilyAges.InPartyCount(session);
            return Math.Max(0, ceiling);
        }

        private static void PromptForCount()
        {
            var session = CreationSession.Current;
            var title = new TextObject("{=CSR_Resource_Troops}Troops").ToString();

            Editor.EditorPopups.ShowNumber(title, 0, Ceiling(session), value =>
            {
                session.CustomTroops = value;
                var confirmation = new TextObject("{=CSR_Means_Set}{LABEL} set to {VALUE}.");
                confirmation.SetTextVariable("LABEL", title);
                confirmation.SetTextVariable("VALUE", value);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(confirmation.ToString()));
            });
        }

        private static string Summary()
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            int troops = session.CustomTroops ?? CSSettings.GetRangeValue(
                settings?.GetTroopsRange(session.SelectedStartType) ?? (0, 20), session.SelectedTroops);
            troops = Math.Min(troops, Ceiling(session));

            var facts = new TextObject("{=CSR_Warband_Now}Now: {WARBAND}, of at most {CEILING}.");
            facts.SetTextVariable("WARBAND", MenuText.Count(troops,
                "{=CSR_Means_WarbandOne}{COUNT} man at your back",
                "{=CSR_Means_WarbandMany}{COUNT} men at your back"));
            facts.SetTextVariable("CEILING", Ceiling(session));
            return facts.ToString();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
