using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The custom path's single stop: the Start Editor over the live character.
    ///     When the player is done, Next continues to vanilla naming and the map.
    /// </summary>
    public static class CustomHubMenu
    {
        public static void AddCustomHubMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_custom_menu",
                CreationFlow.DeclaredPrevious("cs_custom_menu"),
                CreationFlow.DeclaredNext("cs_custom_menu"),
                new TextObject("{=CSR_CustomHub_Title}Custom Setup"),
                new TextObject(
                    "{=CSR_CustomHub_Desc}Everything about your start is set in the Start Editor. Open it, shape your beginning, then continue when satisfied."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_custom_open_editor",
                new TextObject("{=CSR_Editor_Open}Open the Start Editor"),
                new TextObject(
                    "{=CSR_CustomHub_Open_Desc}Every stat, item, faction choice, and family member, on one surface."),
                args => { },
                m => true,
                m => Editor.StartEditorScreen.Open(),
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
