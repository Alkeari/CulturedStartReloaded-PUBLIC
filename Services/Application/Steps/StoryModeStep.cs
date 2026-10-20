using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Helpers;
using CulturedStartReloaded.Models;
using HarmonyLib;
using StoryMode;
using StoryMode.GameComponents.CampaignBehaviors;
using StoryMode.StoryModeObjects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Story mode initialization after the tutorial skip: encyclopedia
    ///     visibility, sibling placement for Phase 2+ starts, phase advancement,
    ///     popup suppression, and the War Sails quest bootstrap. A quiet no-op in
    ///     sandbox mode.
    /// </summary>
    public sealed class StoryModeStep : IStartStep
    {
        public string Name => "Story Mode";

        public string? Validate(StartContext context) => null;

        public void Apply(StartContext context)
        {
            if (!CSGameModeService.IsStoryMode())
            {
                CSLogger.Info("StoryModeStep: sandbox mode; nothing to do.");
                return;
            }

            var hero = context.Hero;
            var session = context.Session;
            var progress = session.SelectedQuestProgress;

            // Mark all story mode family heroes as met so they appear in the encyclopedia.
            MarkStoryHeroesAsMet();

            // Tutorial skip is handled by TutorialSkipPatch and FinalizeTutorialPhasePatch.
            // FirstPhaseStart is the default: tutorial done, Dragon Banner quests begin
            // naturally, and the siblings remain to be rescued through the quests.
            if (progress > StoryQuestProgress.FirstPhaseStart)
            {
                var mainStoryLine = StoryModeManager.Current?.MainStoryLine;
                if (mainStoryLine != null && !mainStoryLine.FamilyRescued)
                {
                    mainStoryLine.FamilyRescued = true;
                    CSLogger.Info("StoryModeStep: FamilyRescued set (skipping past Phase 1).");
                }

                AddElderBrotherToParty(hero);
                TeleportYoungerSiblingsToNearestTown(hero, session);

                StoryProgressAdvancer.AdvanceToSelectedProgress(progress);
            }

            SuppressTrainingFieldPopup();

            // Fire the stealth tutorial event so the Naval DLC (War Sails) can trigger its
            // "Troubled Waters" popup and start the InquireAtOstican quest. In vanilla, this
            // event fires when VillagersInNeed starts late in the tutorial chain; since we
            // skip the entire tutorial, the Naval DLC never gets its trigger without this.
            if (NavalDLCService.IsNavalDLCLoaded())
                FireStealthTutorialEvent();
        }

        private static void MarkStoryHeroesAsMet()
        {
            var heroes = new[]
            {
                StoryModeHeroes.ElderBrother,
                StoryModeHeroes.LittleBrother,
                StoryModeHeroes.LittleSister
            };

            foreach (var h in heroes)
                if (h != null && !h.HasMet)
                    h.SetHasMet();
        }

        private static void AddElderBrotherToParty(Hero hero)
        {
            var brother = StoryModeHeroes.ElderBrother;
            if (brother == null)
            {
                CSLogger.Warn("StoryModeStep: ElderBrother is null; cannot add to party.");
                return;
            }

            var mainParty = hero.PartyBelongedTo;
            if (mainParty == null)
            {
                CSLogger.Warn("StoryModeStep: MainParty is null; cannot add ElderBrother.");
                return;
            }

            AddHeroToPartyAction.Apply(brother, mainParty, false);
            CSLogger.Info($"StoryModeStep: ElderBrother ({brother.Name}) added to the party.");
        }

        /// <summary>
        ///     Teleports the younger siblings (LittleBrother, LittleSister) to the town
        ///     nearest the player's start location.
        /// </summary>
        private static void TeleportYoungerSiblingsToNearestTown(Hero hero, CharacterCreation.Session.CharacterCreationSession session)
        {
            var startSettlement = session.SelectedSettlement
                                  ?? session.SelectedLocation
                                  ?? SettlementFinder.RandomCultureTown(hero.Culture);

            Settlement? nearestTown = null;
            if (startSettlement != null)
            {
                nearestTown = startSettlement.IsTown
                    ? startSettlement
                    : Settlement.All
                        .Where(s => s.IsTown)
                        .OrderBy(s => s.GatePosition.DistanceSquared(startSettlement.GatePosition))
                        .FirstOrDefault();
            }

            nearestTown ??= SettlementFinder.RandomCultureTown(hero.Culture);
            if (nearestTown == null)
            {
                CSLogger.Warn("StoryModeStep: no town found for younger siblings.");
                return;
            }

            var siblings = new[] { StoryModeHeroes.LittleBrother, StoryModeHeroes.LittleSister };
            foreach (var sibling in siblings)
                if (sibling != null)
                {
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(sibling, nearestTown);
                    CSLogger.Info($"StoryModeStep: {sibling.Name} teleported to {nearestTown.Name}.");
                }
        }

        /// <summary>
        ///     Sets _popUpShowed = true on FirstPhaseCampaignBehavior via reflection.
        ///     In vanilla, when the tutorial is skipped, leaving the training field triggers
        ///     a chain: banner notification, clan name, banner editor, stealth tutorial,
        ///     VillagersInNeed quest. We never teleport to the training field, but if the
        ///     player visits it later the chain would fire unexpectedly without this.
        /// </summary>
        private static void SuppressTrainingFieldPopup()
        {
            try
            {
                var behavior = Campaign.Current?.GetCampaignBehavior<FirstPhaseCampaignBehavior>();
                if (behavior == null)
                {
                    CSLogger.Info("StoryModeStep: FirstPhaseCampaignBehavior not found; popup suppression skipped.");
                    return;
                }

                var field = AccessTools.Field(typeof(FirstPhaseCampaignBehavior), "_popUpShowed");
                if (field != null)
                {
                    field.SetValue(behavior, true);
                    CSLogger.Info("StoryModeStep: training field popup suppressed.");
                }
                else
                {
                    CSLogger.Warn("StoryModeStep: _popUpShowed field not found on FirstPhaseCampaignBehavior.");
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: worst case the player gets an extra popup at the training field
                CSLogger.Warn($"StoryModeStep: failed to suppress training field popup: {ex.Message}");
            }
        }

        private static void FireStealthTutorialEvent()
        {
            try
            {
                VersionedGameApi.ActivateStealthTutorial();
                CSLogger.Info("StoryModeStep: OnStealthTutorialActivated fired (Naval DLC trigger).");
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StoryModeStep: failed to fire OnStealthTutorialActivated: {ex.Message}");
            }
        }
    }
}
