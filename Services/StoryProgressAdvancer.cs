using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using StoryMode;
using StoryMode.Quests.FirstPhase;
using StoryMode.Quests.PlayerClanQuests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Advances the story beyond the tutorial based on the player's selected quest
    ///     progress. Called after the tutorial is already completed by TutorialSkipPatch
    ///     and FinalizeTutorialPhasePatch.
    ///
    ///     Vanilla phase progression:
    ///       Tutorial: CompleteTutorialPhase(isSkipped) fires OnStoryModeTutorialEnded
    ///       Phase 1: CompleteFirstPhase() requires MainStoryLineSide set, fires banner collection
    ///       Phase 2: CompleteSecondPhase() requires first phase done
    ///       Phase 3: ThirdPhase.CompleteThirdPhase(detail) requires second phase done
    /// </summary>
    public static class StoryProgressAdvancer
    {
        public static void AdvanceToSelectedProgress(StoryQuestProgress progress)
        {
            CSLogger.Info($"StoryProgressAdvancer: advancing to {progress}.");

            try
            {
                var mainStoryLine = StoryModeManager.Current?.MainStoryLine;
                if (mainStoryLine == null)
                {
                    CSLogger.Warn("StoryProgressAdvancer: MainStoryLine is null; cannot advance story.");
                    return;
                }

                // Tutorial is already completed by TutorialSkipPatch + FinalizeTutorialPhasePatch.
                // FirstPhaseStart is the default; vanilla FirstPhase quests begin naturally.

                if (progress >= StoryQuestProgress.SecondPhaseStart)
                    EnsureFirstPhaseCompleted(mainStoryLine);

                if (progress >= StoryQuestProgress.ThirdPhaseStart)
                    EnsureSecondPhaseCompleted(mainStoryLine);

                if (progress >= StoryQuestProgress.StoryComplete)
                    EnsureThirdPhaseCompleted(mainStoryLine);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StoryProgressAdvancer: failed to advance story.", ex);
            }
        }

        /// <summary>
        ///     Completes Phase 1 (Dragon Banner). This requires:
        ///     1. Setting the MainStoryLineSide based on culture/kingdom
        ///     2. Calling CompleteFirstPhase() which marks banner investigation as done
        /// </summary>
        private static void EnsureFirstPhaseCompleted(MainStoryLine mainStoryLine)
        {
            if (mainStoryLine.IsFirstPhaseCompleted)
            {
                CSLogger.Info("  FirstPhase already completed, skipping.");
                return;
            }

            CSLogger.Info("  Completing FirstPhase (Dragon Banner)...");

            // Resolve and set the story line side before completing
            var side = ResolveStoryLineSide();
            mainStoryLine.SetStoryLineSide(side);
            CSLogger.Info($"  MainStoryLineSide set to: {side}");

            // Mark family as rescued (normally done during tutorial hideout quest)
            mainStoryLine.FamilyRescued = true;

            // Complete the first phase; this triggers the transition to Phase 2
            mainStoryLine.CompleteFirstPhase();
            CSLogger.Info("  FirstPhase completed.");

            // Cancel orphaned Phase 1 quests. CompleteTutorialPhase fires OnStoryModeTutorialEnded
            // which starts RebuildPlayerClanQuest and BannerInvestigationQuest. CompleteFirstPhase
            // removes FirstPhaseCampaignBehavior (which handles their completion chain), leaving
            // them as permanent active quests in the journal. Cancel them cleanly.
            CancelOrphanedFirstPhaseQuests();
        }

        private static void EnsureSecondPhaseCompleted(MainStoryLine mainStoryLine)
        {
            if (mainStoryLine.IsSecondPhaseCompleted)
            {
                CSLogger.Info("  SecondPhase already completed, skipping.");
                return;
            }

            mainStoryLine.CompleteSecondPhase();
            CSLogger.Info("  SecondPhase completed.");
        }

        private static void EnsureThirdPhaseCompleted(MainStoryLine mainStoryLine)
        {
            if (mainStoryLine.ThirdPhase == null)
            {
                CSLogger.Warn("  ThirdPhase is null; cannot complete.");
                return;
            }

            if (mainStoryLine.ThirdPhase.IsCompleted)
            {
                CSLogger.Info("  ThirdPhase already completed, skipping.");
                return;
            }

            mainStoryLine.ThirdPhase.CompleteThirdPhase(
                QuestBase.QuestCompleteDetails.Success);
            CSLogger.Info("  ThirdPhase completed; story is done.");
        }

        /// <summary>
        ///     Cancels Phase 1 quests that were started by OnStoryModeTutorialEnded but are now
        ///     orphaned because CompleteFirstPhase removed FirstPhaseCampaignBehavior (which would
        ///     have handled their completion chain). Without cancellation, these quests remain
        ///     permanently active in the journal for Phase 2+ starts.
        /// </summary>
        private static void CancelOrphanedFirstPhaseQuests()
        {
            try
            {
                var cancelReason = new TextObject(
                    "{=CSR_QuestSkipped}Skipped by Cultured Start: story advanced past this phase.");

                var quests = Campaign.Current?.QuestManager?.Quests;
                if (quests == null) return;

                foreach (var quest in quests.ToList())
                {
                    if (quest.IsFinalized) continue;

                    if (quest is RebuildPlayerClanQuest || quest is BannerInvestigationQuest)
                    {
                        quest.CompleteQuestWithCancel(cancelReason);
                        CSLogger.Info($"  Canceled orphaned quest: {quest.GetType().Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"  Failed to cancel orphaned Phase 1 quests: {ex.Message}");
            }
        }

        /// <summary>
        ///     Determines the MainStoryLineSide based on the player's culture and start type.
        ///     Imperial culture ("empire") means the imperial side, all others anti-imperial.
        ///     Monarch/LandedVassal create a kingdom, others support one.
        /// </summary>
        private static MainStoryLineSide ResolveStoryLineSide()
        {
            var session = CreationSession.Current;
            var hero = Hero.MainHero;

            // The imperial side is the culture whose id is "empire", which is only meaningful in
            // the base game: a total conversion keeps that id and makes it somewhere else, so under
            // Realm of Thrones this would have made Braavos the Empire. With a conversion running
            // nobody is imperial, which is the honest answer when the Empire is not in the world.
            var cultureId = hero.Culture?.StringId ?? "empire";
            bool isImperial = !TotalConversionService.IsActive &&
                              string.Equals(cultureId, "empire", StringComparison.OrdinalIgnoreCase);

            bool isRuler = session.SelectedStartType == StartType.Monarch
                           || session.SelectedStartType == StartType.LandedVassal;

            CSLogger.Info($"  ResolveStoryLineSide: culture={cultureId}, isImperial={isImperial}, isRuler={isRuler}");

            if (isRuler)
            {
                return isImperial
                    ? MainStoryLineSide.CreateImperialKingdom
                    : MainStoryLineSide.CreateAntiImperialKingdom;
            }

            return isImperial
                ? MainStoryLineSide.SupportImperialKingdom
                : MainStoryLineSide.SupportAntiImperialKingdom;
        }
    }
}
