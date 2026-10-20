using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The career the character begins in, where the total conversion running has careers of its
    ///     own. Both guided routes ask it, because the conversion's own creation asks it and a start
    ///     that skips it is handed whichever career the culture lists first.
    ///
    ///     Every career the conversion declares gets an option here, because the culture is not
    ///     settled when the menus are registered; what a run actually sees is decided per option by
    ///     the culture it ended up with and the clan tier its standing settled, so the list is the
    ///     conversion's own eligibility rather than a copy of it.
    /// </summary>
    public static class CareerMenu
    {
        public const string MenuId = "cs_career_menu";

        public static void AddCareerMenu(CharacterCreationManager manager)
        {
            var careers = TaomBridge.Careers();
            if (careers.Count == 0) return;

            var menu = new NarrativeMenu(
                MenuId,
                CreationFlow.DeclaredPrevious(MenuId),
                CreationFlow.DeclaredNext(MenuId),
                new TextObject("{=CSR_Career_Title}Your Calling"),
                new TextObject(
                    "{=CSR_Career_Desc}Every fighting life answers to a calling, and yours is known by the company you keep. Which is it?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var career in careers) AddCareerOption(menu, career);

            manager.AddNewMenu(menu);
            CSLogger.Info($"CareerMenu: {careers.Count} careers offered by the conversion.");
        }

        private static void AddCareerOption(NarrativeMenu menu, TaomCareer career)
        {
            string optionId = "cs_career_" + career.Id;
            ChoiceEffects.Declare(optionId, () => Effect(career));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                optionId,
                new TextObject("{=!}" + career.Name),
                new TextObject("{=!}" + career.Description),
                args => { },
                m => IsOffered(career),
                m =>
                {
                    CreationSession.Current.SelectedCareerId = career.Id;
                    CreationSession.Current.CareerChoiceIds.Clear();
                    MenuText.Remember(MenuId, "{=!}" + career.Name);
                },
                m => { }
            ));
        }

        /// <summary>
        ///     Whether this run may take the career: the conversion admits it for the culture chosen,
        ///     and the clan tier the start settled reaches the tier it asks for.
        /// </summary>
        private static bool IsOffered(TaomCareer career)
        {
            var session = CreationSession.Current;
            string? cultureId = session.SelectedCulture?.StringId;
            if (string.IsNullOrEmpty(cultureId)) return false;
            if (career.MinClanTier > session.SelectedClanTier) return false;

            if (career.CultureIds.Count == 0) return true;

            foreach (var id in career.CultureIds)
                if (string.Equals(id, cultureId, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        ///     What taking the career does to the character: the career itself, the rank it begins at
        ///     and the first pick on its board, which comes with it.
        /// </summary>
        private static string Effect(TaomCareer career)
        {
            var taken = new TextObject("{=CSR_Panel_Career}Career: {CAREER}, from the day you begin");
            taken.SetTextVariable("CAREER", career.Name);

            string? rank = null;
            if (!string.IsNullOrEmpty(career.FirstRankName))
            {
                var line = new TextObject("{=CSR_Panel_CareerRank}Rank: {RANK}, the first of three");
                line.SetTextVariable("RANK", career.FirstRankName);
                rank = line.ToString();
            }

            string? root = null;
            string rootText = TaomBridge.RootChoiceDescription(career.Id);
            if (!string.IsNullOrEmpty(rootText))
            {
                var line = new TextObject("{=CSR_Panel_CareerRoot}Career pick: {PICK}");
                line.SetTextVariable("PICK", rootText);
                root = line.ToString();
            }

            return ChoiceEffects.Stated(taken.ToString(), rank, root);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
