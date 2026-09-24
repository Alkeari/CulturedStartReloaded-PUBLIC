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
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    public static class CompanionSelectMenu
    {
        public static void AddCompanionMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_companion_select",
                CreationFlow.DeclaredPrevious("cs_companion_select"),
                CreationFlow.DeclaredNext("cs_companion_select"),
                new TextObject("{=CSR_Companion_Title}Select Companions"),
                new TextObject("{=CSR_Companion_Desc}Choose your starting party."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            // Built out to the highest tier's limit once, then filtered per
            // render to the tier actually chosen, so the screen never offers
            // companions the clan cannot hold
            int minCompanions = 0;
            int maxCompanions = GameCaps.MaxCompanions(GameCaps.MaxClanTier());

            for (int i = minCompanions; i <= maxCompanions; i++)
            {
                var count = i;
                var text = new TextObject(count == 1 ? "{=CSR_Companion_Singular}Start with {COUNT} companion" : "{=CSR_Companion_Plural}Start with {COUNT} companions");
                text.SetTextVariable("COUNT", count);

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"cs_companion_{count}",
                    text,
                    new TextObject("{=CSR_Companion_Option_Desc}Begin your journey with trusted allies."),
                    (args) => { },
                    (m) => count <= GameCaps.MaxCompanions(CreationSession.Current.EffectiveClanTier),
                    (m) => CreationSession.Current.StartingCompanions = count,
                    (m) => { }
                ));
            }

            manager.AddNewMenu(menu);
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