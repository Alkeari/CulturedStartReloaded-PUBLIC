using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The first choice of the whole flow, and the only place this mod asks it:
    ///     a plain vanilla start, one of the two guided routes, or the Start
    ///     Editor's full custom setup. Everything downstream branches on it.
    ///
    ///     Four options and never three. Cultured Start is the seven chapters the
    ///     mod asked before the rewrite and Cultured Start Revamped is the thirteen
    ///     scenes that replaced them, and a player who knows the first must be able
    ///     to choose it rather than be moved onto the second. Vanilla stands beside
    ///     them as an option rather than as the absence of one, because leaving the
    ///     screen is not a choice a player should have to work out.
    ///
    ///     From game v1.5.0 up on a Sandbox start this menu is not shown at all: the
    ///     game's own Advanced Starting Options carries the same three mod routes and
    ///     answers vanilla for anyone who names none of them.
    /// </summary>
    public static class ModeMenu
    {
        public static void AddModeMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_mode_menu",
                CreationFlow.DeclaredPrevious("cs_mode_menu"),
                CreationFlow.DeclaredNext("cs_mode_menu"),
                new TextObject("{=CSR_Mode_Title}Your Path"),
                new TextObject("{=CSR_Mode_Desc}How do you want to shape this beginning?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_mode_vanilla",
                new TextObject("{=CSR_Mode_Vanilla}Vanilla Start"),
                new TextObject(
                    "{=CSR_Mode_Vanilla_Desc}Skip everything this mod adds and begin a plain, untouched start."),
                args => { },
                m => true,
                m => CreationSession.Current.Mode = SetupMode.Vanilla,
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_mode_lifepath",
                new TextObject("{=CSR_Mode_LifePath}Cultured Start"),
                new TextObject(
                    "{=CSR_Mode_LifePath_Desc}Answer seven chapters, from the family you were born into to the reason you took the road."),
                args => { },
                m => true,
                m => CreationSession.Current.Mode = SetupMode.LifePath,
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_mode_narrative",
                new TextObject("{=CSR_Mode_Narrative}Cultured Start Revamped"),
                new TextObject(
                    "{=CSR_Mode_Narrative_Desc}Tell your story choice by choice; your background shapes your start."),
                args => { },
                m => true,
                m => CreationSession.Current.Mode = SetupMode.Narrative,
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_mode_custom",
                new TextObject("{=CSR_Mode_Custom}Start Editor"),
                new TextObject(
                    "{=CSR_Mode_Custom_Desc}Open the Start Editor and set every detail of your start directly."),
                args => { },
                m => true,
                m => CreationSession.Current.Mode = SetupMode.Custom,
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
