using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Starting town picker with culture availability precomputed at build
    ///     time; settlement names pass through as TextObjects, never concatenated.
    /// </summary>
    public static class StartLocationMenu
    {
        public static void AddStartLocationMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_location_menu",
                CreationFlow.DeclaredPrevious("cs_location_menu"),
                CreationFlow.DeclaredNext("cs_location_menu"),
                new TextObject("{=CSR_Location_Title}Starting Location"),
                new TextObject("{=CSR_Location_Desc}Choose where your journey begins."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            try
            {
                var culturesWithTowns = new HashSet<CultureObject>(
                    Settlement.All.Where(s => s.IsTown && s.Culture != null).Select(s => s.Culture!));
                var kingdomCultureTowns = new HashSet<(Kingdom, CultureObject)>(
                    Settlement.All
                        .Where(s => s.IsTown && s.Culture != null && s.OwnerClan?.Kingdom != null)
                        .Select(s => (s.OwnerClan!.Kingdom!, s.Culture!)));

                foreach (var town in Settlement.All
                             .Where(s => s.IsTown)
                             .OrderBy(s => s.Name?.ToString() ?? ""))
                {
                    var t = town;
                    menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                        $"cs_location_{t.StringId}",
                        t.Name,
                        new TextObject("{=CSR_Location_Town_Desc}Begin your journey in {TOWN_NAME}.")
                            .SetTextVariable("TOWN_NAME", t.Name),
                        args => { },
                        m => IsValidStartLocation(t, culturesWithTowns, kingdomCultureTowns),
                        m =>
                        {
                            CreationSession.Current.SelectedLocation = t;
                            CreationSession.Current.UseRandomLocation = false;
                        },
                        m => { }
                    ));
                }

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    "cs_location_auto",
                    new TextObject("{=CSR_Location_Auto}Let Fate Decide"),
                    new TextObject("{=CSR_Location_Auto_Desc}A suitable starting location will be chosen for you."),
                    args => { }, m => true,
                    m =>
                    {
                        CreationSession.Current.SelectedLocation = null;
                        CreationSession.Current.UseRandomLocation = true;
                    },
                    m => { }
                ));
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartLocationMenu: Failed to populate locations.", ex);
            }

            manager.AddNewMenu(menu);
        }

        private static bool IsValidStartLocation(Settlement town,
            HashSet<CultureObject> culturesWithTowns,
            HashSet<(Kingdom, CultureObject)> kingdomCultureTowns)
        {
            if (!town.IsTown) return false;

            var session = CreationSession.Current;
            var culture = session.SelectedCulture;

            // Vassal/mercenary with a chosen kingdom: towns of that kingdom,
            // culture-filtered with a show-all fallback
            var kingdom = session.SelectedKingdom;
            if (session.SelectedStartType != StartType.Commoner && kingdom != null)
            {
                if (town.OwnerClan?.Kingdom != kingdom)
                    return false;

                return culture == null || town.Culture == culture ||
                       !kingdomCultureTowns.Contains((kingdom, culture));
            }

            // Commoner (or no kingdom picked): culture-filtered with fallback
            return culture == null || town.Culture == culture ||
                   !culturesWithTowns.Contains(culture);
        }

        private static List<NarrativeMenuCharacter> CreatePlayerCharacter()
        {
            return CharacterPreviewHelper.CreatePlayerCharacter();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
