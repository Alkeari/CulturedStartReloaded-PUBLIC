using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Where the numbers on your character come from. This was six commands
    ///     that each opened a popup and then a second popup, in a screen whose
    ///     list can only be read by clicking. The editor's own tabs already do
    ///     the same work with a visible list, per-row control, multi-select and
    ///     set-all, so each option here simply opens the right one.
    /// </summary>
    public static class StatCustomizationMenu
    {
        public static void AddStatCustomizationMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_stats_menu",
                CreationFlow.DeclaredPrevious("cs_stats_menu"),
                CreationFlow.DeclaredNext("cs_stats_menu"),
                new TextObject("{=CSR_Stats_Title}What Your Life Made of You"),
                new TextObject(
                    "{=CSR_Stats_Desc}Everything you chose has already shaped you. Keep that, or set any of it exactly."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            var keepDescription = new TextObject(
                "{=CSR_Stats_Keep_Desc}Your childhood, your schooling and your turning point decide your attributes, focus and character. {NOW}");

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_stats_keep",
                new TextObject("{=CSR_Stats_Keep}Let Your Life Decide"),
                keepDescription,
                args => { },
                MenuText.Live(keepDescription, d => d.SetTextVariable("NOW", OverrideSummary())),
                m =>
                {
                    var session = CreationSession.Current;
                    session.CustomAttributes.Clear();
                    session.CustomFocus.Clear();
                    session.CustomTraits.Clear();
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=CSR_Stats_Cleared}Stat customizations cleared.").ToString()));
                },
                m => { }
            ));

            AddEditorOption(menu, "cs_stats_attributes", "attributes",
                "{=CSR_Scope_Attributes}Your Attributes",
                "{=CSR_Stats_Attributes}Set Your Attributes",
                "{=CSR_Stats_Attributes_Desc}Vigor, control, endurance, cunning, social and intelligence, one by one or all together. {NOW}",
                () => Customized(CreationSession.Current.CustomAttributes.Count));

            AddEditorOption(menu, "cs_stats_focus", "focus",
                "{=CSR_Scope_Focus}Your Skill Focus",
                "{=CSR_Stats_Focus}Set Your Skill Focus",
                "{=CSR_Stats_Focus_Desc}Where your attention goes, skill by skill, within the points the game allows. {NOW}",
                () => Customized(CreationSession.Current.CustomFocus.Count));

            AddEditorOption(menu, "cs_stats_traits", "traits",
                "{=CSR_Scope_Traits}Who You Are",
                "{=CSR_Stats_Traits}Set Who You Are",
                "{=CSR_Stats_Traits_Desc}Mercy, valor, honor, generosity and calculation, each within its own bounds. {NOW}",
                () => Customized(CreationSession.Current.CustomTraits.Count));

            manager.AddNewMenu(menu);
        }

        private static void AddEditorOption(NarrativeMenu menu, string id, string tabKey,
            string scopeTitleKey, string titleKey, string descKey, System.Func<string> now)
        {
            var description = new TextObject(descKey);
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("NOW", now())),
                m => Editor.StartEditorScreen.Open(
                    Editor.EditorScope.For(scopeTitleKey, tabKey), false),
                m => { }
            ));
        }

        private static string Customized(int count)
        {
            return count == 0
                ? new TextObject("{=CSR_Stats_NoneSet}None set by hand yet.").ToString()
                : MenuText.Count(count,
                    "{=CSR_Stats_SetOne}{COUNT} set by hand.",
                    "{=CSR_Stats_SetMany}{COUNT} set by hand.");
        }

        private static string OverrideSummary()
        {
            var session = CreationSession.Current;
            int total = session.CustomAttributes.Count + session.CustomFocus.Count + session.CustomTraits.Count;

            return total == 0
                ? new TextObject("{=CSR_Stats_NowClean}Nothing is overridden, so this is already what you have.")
                    .ToString()
                : MenuText.Count(total,
                    "{=CSR_Stats_NowOverridden}Choosing this clears {COUNT} value you set by hand.",
                    "{=CSR_Stats_NowOverriddenMany}Choosing this clears {COUNT} values you set by hand.");
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
