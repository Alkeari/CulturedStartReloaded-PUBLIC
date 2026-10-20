using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     The last page of creation: the composed life read back as one story,
    ///     confirmed onto the map.
    /// </summary>
    public static class EpilogueMenu
    {
        private static TextObject? _description;

        public static void AddEpilogueMenu(CharacterCreationManager manager)
        {
            _description = new TextObject("{=!}{CSR_STORY_SO_FAR}");
            RefreshStory();

            var menu = new NarrativeMenu(
                "cs_epilogue_menu",
                CreationFlow.DeclaredPrevious("cs_epilogue_menu"),
                CreationFlow.DeclaredNext("cs_epilogue_menu"),
                new TextObject("{=CSR_Epilogue_Title}Your Story So Far"),
                _description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_epilogue_begin",
                new TextObject("{=CSR_Epilogue_Begin}So Begins Your Tale"),
                new TextObject("{=CSR_Epilogue_Begin_Desc}Your story is set. What remains is your banner and your clan's name."),
                args => { },
                m =>
                {
                    // Conditions run when the menu renders: the freshest moment
                    // to read the whole composed session back as prose
                    RefreshStory();
                    return true;
                },
                m => { },
                m => { }));

            manager.AddNewMenu(menu);
        }

        private static void RefreshStory()
        {
            _description?.SetTextVariable("CSR_STORY_SO_FAR",
                Services.StorySummary.Build(CreationSession.Current));
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
