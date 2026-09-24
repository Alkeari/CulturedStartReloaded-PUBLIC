using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     The Monarch's founding story: settle peacefully into weakly held land,
    ///     or press a claim and start the reign at war with the dispossessed.
    /// </summary>
    public static class FoundingMenu
    {
        public static void AddFoundingMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_founding_menu",
                CreationFlow.DeclaredPrevious("cs_founding_menu"),
                CreationFlow.DeclaredNext("cs_founding_menu"),
                new TextObject("{=CSR_Founding_Title}Your Founding"),
                new TextObject("{=CSR_Founding_Desc}How did your holdings come into your hands?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_founding_settler",
                new TextObject("{=CSR_Founding_Settler}Settler"),
                new TextObject(
                    "{=CSR_Founding_Settler_Desc}Your holdings come from a weak realm's neglected lands. No one declares war on you today."),
                args => { },
                m => true,
                m => CreationSession.Current.SelectedFounding = MonarchFounding.Settler,
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_founding_claimant",
                new TextObject("{=CSR_Founding_Claimant}Claimant"),
                new TextObject(
                    "{=CSR_Founding_Claimant_Desc}You pressed an old claim by force. The dispossessed realm declares war on your new kingdom."),
                args => { },
                m => true,
                m => CreationSession.Current.SelectedFounding = MonarchFounding.Claimant,
                m => { }
            ));

            manager.AddNewMenu(menu);
            AddKingdomNameMenu(manager);
        }

        /// <summary>
        ///     Its own page, so naming never competes with the founding choice:
        ///     Settler and Claimant both walk through it.
        /// </summary>
        private static void AddKingdomNameMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_kingdom_name_menu",
                CreationFlow.DeclaredPrevious("cs_kingdom_name_menu"),
                CreationFlow.DeclaredNext("cs_kingdom_name_menu"),
                new TextObject("{=CSR_KingdomName_Menu_Title}The Kingdom's Name"),
                new TextObject("{=CSR_KingdomName_Menu_Desc}A realm is spoken into being. What will the heralds proclaim?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_kingdom_name_clan",
                new TextObject("{=CSR_KingdomName_Clan}Named for Your Clan"),
                new TextObject("{=CSR_KingdomName_Clan_Desc}The realm carries your clan's name, as realms have always done."),
                args => { },
                m => true,
                m =>
                {
                    CreationSession.Current.KingdomName = null;
                    CreationSession.Current.KingdomNameDecided = true;
                },
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_kingdom_name_custom",
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom"),
                new TextObject("{=CSR_Founding_Name_Desc}Set your kingdom's name now, before the campaign begins."),
                args => { },
                m => true,
                m => PromptForKingdomName(),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        private static void PromptForKingdomName()
        {
            Editor.EditorPopups.ShowPrompt(
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(),
                new TextObject("{=CSR_KingdomName_Desc}Choose the name your realm will carry into history.").ToString(),
                null,
                new TextObject("{=CSR_KingdomName_Confirm}Proclaim").ToString(),
                new TextObject("{=CSR_KingdomName_Cancel}Keep Current").ToString(),
                input =>
                {
                    var cleaned = (input ?? string.Empty).Replace("{", "").Replace("}", "").Trim();
                    if (cleaned.Length == 0) return;

                    CreationSession.Current.KingdomName = cleaned;
                    CreationSession.Current.KingdomNameDecided = true;
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        new TextObject("{=CSR_KingdomName_Set}Your realm will be proclaimed as {KINGDOM_NAME}.")
                            .SetTextVariable("KINGDOM_NAME", cleaned)
                            .ToString()));
                });
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
