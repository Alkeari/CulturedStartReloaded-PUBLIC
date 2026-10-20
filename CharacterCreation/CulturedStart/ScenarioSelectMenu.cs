using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Settings;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    public static class ScenarioSelectMenu
    {
        public static void AddScenarioMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_scenario_select",
                CreationFlow.DeclaredPrevious("cs_scenario_select"),
                CreationFlow.DeclaredNext("cs_scenario_select"),
                new TextObject("{=CSR_Scenario_Title}Your Standing"),
                new TextObject("{=CSR_Scenario_Desc}Choose the station from which your tale begins."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            if (CSSettings.ShowsStartType(StartType.Commoner))
            {
                AddStartOption(menu, "cs_start_commoner", StartType.Commoner,
                    "{=CSR_Commoner_Option}Start as a Commoner",
                    "{=CSR_Commoner_Desc}You begin your journey alone, with humble means.");
            }

            if (CSSettings.ShowsStartType(StartType.Monarch))
            {
                AddStartOption(menu, "cs_start_monarch", StartType.Monarch,
                    "{=CSR_Monarch_Option}Rule as Monarch",
                    "{=CSR_Monarch_Desc}You are the ruler of your own kingdom.");
            }

            if (CSSettings.ShowsStartType(StartType.LandedVassal))
            {
                AddStartOption(menu, "cs_start_landed_vassal", StartType.LandedVassal,
                    "{=CSR_LandedVassal_Option}Serve as Landed Vassal",
                    "{=CSR_LandedVassal_Desc}You serve a liege and govern a fief.");
            }

            if (CSSettings.ShowsStartType(StartType.LandlessVassal))
            {
                AddStartOption(menu, "cs_start_landless_vassal", StartType.LandlessVassal,
                    "{=CSR_LandlessVassal_Option}Serve as Landless Vassal",
                    "{=CSR_LandlessVassal_Desc}You serve a liege but hold no lands.");
            }

            if (CSSettings.ShowsStartType(StartType.Mercenary))
            {
                AddStartOption(menu, "cs_start_mercenary", StartType.Mercenary,
                    "{=CSR_Mercenary_Option}Serve as Mercenary",
                    "{=CSR_Mercenary_Desc}You sell your sword to the highest bidder.");
            }

            if (CSSettings.ShowsStartType(StartType.Outlaw))
            {
                AddStartOption(menu, "cs_start_outlaw", StartType.Outlaw,
                    "{=CSR_Outlaw_Option}Live as an Outlaw",
                    "{=CSR_Outlaw_Desc}A realm wants your head; your band lives outside its law.");
            }

            if (CSSettings.ShowsStartType(StartType.CaravanMaster))
            {
                AddStartOption(menu, "cs_start_caravan", StartType.CaravanMaster,
                    "{=CSR_Caravan_Option}Lead a Caravan",
                    "{=CSR_Caravan_Desc}You begin with pack animals, trade goods, and guards on a trade route.");
            }

            if (CSSettings.ShowsStartType(StartType.RebelClan))
            {
                AddStartOption(menu, "cs_start_rebel", StartType.RebelClan,
                    "{=CSR_Rebel_Option}Rise as a Rebel Clan",
                    "{=CSR_Rebel_Desc}You hold a castle in open rebellion against its realm.");
            }

            manager.AddNewMenu(menu);
        }

        private static void AddStartOption(NarrativeMenu menu, string id, StartType startType,
            string titleKey, string descKey)
        {
            var type = startType;
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                new TextObject(descKey),
                (args) => { },
                (m) => CSSettings.ShowsStartType(type),
                (m) =>
                {
                    CreationSession.Current.SelectedStartType = type;
                    // The chapters behind the old start type are abandoned here;
                    // everything they wrote goes with them
                    CreationSession.Current.ResetScenarioState();
                },
                (m) => { }
            ));
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
