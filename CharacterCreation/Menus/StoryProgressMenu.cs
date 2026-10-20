using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    public static class StoryProgressMenu
    {
        /// <summary>
        ///     The four rungs, each carrying the one value its pick writes. The
        ///     effect panel and the pick are built from that same value here, so
        ///     the screen cannot state a phase it does not set.
        /// </summary>
        private static readonly (string Id, string TitleKey, string ProseKey, StoryQuestProgress Progress)[] Rungs =
        {
            ("cs_story_progress_first_phase", "{=CSR_Story_Phase1}Dragon Banner: Start",
                "{=CSR_Story_Phase1_Desc}Begin with the tutorial completed. You will start the Dragon Banner investigation quest, rebuilding your clan and tracking down banner pieces.",
                StoryQuestProgress.FirstPhaseStart),
            ("cs_story_progress_second_phase", "{=CSR_Story_Phase2}Empire vs Anti-Empire: Start",
                "{=CSR_Story_Phase2_Desc}Begin with the Dragon Banner assembled and a side chosen. The faction war between the Empire and its enemies is underway.",
                StoryQuestProgress.SecondPhaseStart),
            ("cs_story_progress_third_phase", "{=CSR_Story_Phase3}Final War: Start",
                "{=CSR_Story_Phase3_Desc}Begin with the conspiracy revealed. The final war against opposition kingdoms is about to begin.",
                StoryQuestProgress.ThirdPhaseStart),
            ("cs_story_progress_complete", "{=CSR_Story_Complete}Story Complete",
                "{=CSR_Story_Complete_Desc}Begin with the entire main questline already completed. Play freely in the sandbox with no active story quests.",
                StoryQuestProgress.StoryComplete)
        };

        public static void AddStoryProgressMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_story_progress_menu",
                CreationFlow.DeclaredPrevious("cs_story_progress_menu"),
                CreationFlow.DeclaredNext("cs_story_progress_menu"),
                new TextObject("{=CSR_StoryProgress_Title}Story Progress"),
                new TextObject("{=CSR_StoryProgress_Desc}Choose how far into the main questline you wish to begin. The tutorial is always skipped. All prior quest steps will be completed for you."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var rung in Rungs)
            {
                var chosen = rung;

                ChoiceEffects.Declare(chosen.Id, () => Effect(chosen.Progress));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    chosen.Id,
                    new TextObject(chosen.TitleKey),
                    new TextObject(chosen.ProseKey),
                    args => { }, m => true,
                    m => CreationSession.Current.SelectedQuestProgress = chosen.Progress,
                    m => { }
                ));
            }

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     What this rung does to the story, read off the same thresholds
        ///     StoryModeStep and StoryProgressAdvancer test the chosen value
        ///     against. Every line states what has already happened when the
        ///     campaign opens, never what the player will be asked to do next.
        /// </summary>
        private static string Effect(StoryQuestProgress progress)
        {
            return ChoiceEffects.Stated(
                Text("{=CSR_Panel_Story_Tutorial}Quests: the tutorial is behind you, whichever of these you take"),
                Text(progress >= StoryQuestProgress.SecondPhaseStart
                    ? "{=CSR_Panel_Story_BannerDone}Quests: the Dragon Banner is assembled and your side chosen by your culture and your station, so neither quest is left running"
                    : "{=CSR_Panel_Story_BannerOpen}Quests: the Dragon Banner investigation and the rebuilding of your clan are left to run, with nothing done for you ahead of them"),
                progress > StoryQuestProgress.FirstPhaseStart
                    ? Text("{=CSR_Panel_Story_Family}Family: your elder brother in your party, your younger brother and sister in the town nearest where you begin")
                    : null,
                progress >= StoryQuestProgress.ThirdPhaseStart
                    ? Text("{=CSR_Panel_Story_WarDone}Quests: the war between the Empire and its enemies is played out as far as the conspiracy being revealed")
                    : null,
                progress >= StoryQuestProgress.StoryComplete
                    ? Text("{=CSR_Panel_Story_AllDone}Quests: the final war is settled too, and none is left running at all")
                    : null);
        }

        private static string Text(string key) => new TextObject(key).ToString();

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
