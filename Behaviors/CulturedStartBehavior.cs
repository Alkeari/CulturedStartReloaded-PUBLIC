using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Behaviors
{
    public class CulturedStartBehavior : CampaignBehaviorBase
    {
        // Roughly ten seconds of frames; the game grants its post-creation
        // level-up points well within the first moments on the map
        private const int CleanupTickBudget = 600;

        private static readonly System.Reflection.MethodInfo? CheckLevelMethod =
            HarmonyLib.AccessTools.Method(
                typeof(TaleWorlds.CampaignSystem.CharacterDevelopment.HeroDeveloper), "CheckLevel");

        private readonly StartOrchestrator _orchestrator;
        private bool _hasAppliedStart;
        private int _cleanupTicksRemaining;
        private int _fixupTicksRemaining;

        public CulturedStartBehavior(StartOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public override void RegisterEvents()
        {
            // v1.5.0 made the event carry an int the mod has no use for, so the listener is
            // built to whatever shape this game's event takes.
            Services.GameCompat.ListenForCharacterCreationOver(this, OnCharacterCreationIsOver);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_hasAppliedStart", ref _hasAppliedStart);
        }

        private void OnCharacterCreationIsOver()
        {
            if (_hasAppliedStart)
            {
                CSLogger.Info("CulturedStartBehavior: start already applied; skipping.");
                return;
            }

            // Mark first: the orchestrator has its own per-step failure boundaries,
            // and re-running the whole pipeline on a later event would double-apply
            // everything that did succeed.
            _hasAppliedStart = true;

            try
            {
                _orchestrator.ApplyStart();
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartBehavior: start application failed.", ex);
            }

            // Both mod routes hand over a finished character, and on both
            // the game can mint level-up points AFTER creation ends: the custom route
            // because it specified every value itself, the guided route because the
            // skills its story set imply a level the game only settles on the map.
            // The guided sheet itself is settled during the apply pipeline, which
            // leaves both pools untouched; this window catches only what the map
            // mints on top, and enforces zero there instead of at finalization.
            // Cultured Start is left out: its age chapter hands the player unspent
            // points to place on the map, and clearing them would take that away.
            if (CreationSession.Current.Mode is not (SetupMode.Vanilla or SetupMode.LifePath))
                _cleanupTicksRemaining = CleanupTickBudget;

            // Every mode: heal the freshly leveled heroes (their maximums grew
            // after their health was set) and make the whole clan known faces.
            // The roster is NOT policed here: the vanilla grain seed arrives
            // before the pipeline's clear, the stray mule was the mod's own
            // heirloom, and a tick-time reset wipes what the player earns.
            if (CreationSession.Current.Mode != SetupMode.Vanilla)
                _fixupTicksRemaining = CleanupTickBudget;
        }

        private void OnTick(float dt)
        {
            TickPointCleanup();
            TickHeroFixup();
        }

        private void TickPointCleanup()
        {
            if (_cleanupTicksRemaining <= 0) return;
            _cleanupTicksRemaining--;

            try
            {
                var hero = Hero.MainHero;
                var developer = hero?.HeroDeveloper;
                if (hero == null || developer == null) return;

                if (developer.UnspentFocusPoints <= 0 && developer.UnspentAttributePoints <= 0) return;

                // CheckLevel is public through v1.4.8 and private from v1.5.0; reflection binds both.
                // It settles every level the start's skills have already earned, so what the pools
                // hold afterward is the whole of what is pending rather than the first installment.
                CheckLevelMethod?.Invoke(developer, new object[] { false });

                // Either guided route: both tell a life and both leave it to the
                // pipeline to spend what the life earned, so a character from either
                // has to arrive with nothing pending
                if (Services.Application.GuidedRoute.IsGuided(CreationSession.Current.Mode))
                    SpendWhatTheLifeEarned(hero);

                if (developer.UnspentFocusPoints <= 0 && developer.UnspentAttributePoints <= 0) return;

                CSLogger.Info("CulturedStartBehavior: post-creation level-up points cleared " +
                              $"({developer.UnspentAttributePoints} attribute, {developer.UnspentFocusPoints} focus, " +
                              $"{CreationSession.Current.Mode} path).");
                developer.ClearUnspentPoints();
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartBehavior: point cleanup failed.", ex);
                _cleanupTicksRemaining = 0;
            }
        }

        /// <summary>
        ///     Spends the points the game minted after creation by the life the
        ///     player told, the same reading the apply pipeline settled the sheet
        ///     with. Reaching that pass also re-checks the sheet against what the
        ///     life is worth, which is written as a target rather than an amount,
        ///     so a character already settled during creation is granted nothing a
        ///     second time and only the newly minted pool is spent. Anything the
        ///     reading cannot place, because every attribute and skill it argues
        ///     for is already at the game's cap, falls through to the caller's
        ///     clear, since nothing may be left pending either way.
        ///
        ///     Its own boundary, so a failure to spend still ends in a clear rather
        ///     than in points left waiting on the character sheet.
        /// </summary>
        private static void SpendWhatTheLifeEarned(Hero hero)
        {
            try
            {
                var developer = hero.HeroDeveloper;
                if (!GuidedRun.WasWalked)
                {
                    CSLogger.Info("CulturedStartBehavior: the guided route answered nothing, " +
                                  "so there is no life to spend by and the points are cleared.");
                    return;
                }

                CSLogger.Info($"CulturedStartBehavior: the map granted {developer.UnspentAttributePoints} attribute " +
                              $"and {developer.UnspentFocusPoints} focus point(s); spending them by the life told.");
                // Reached rather than repeated: a second reading of the same life here would
                // drift from the pipeline's the moment a scene changed, and the two would
                // then disagree about the same character
                Services.Application.Steps.NarrativeStep.SpendTheLifesPoints(hero, CreationSession.Current);
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartBehavior: spending the post-creation points by the life failed.", ex);
            }
        }

        /// <summary>
        ///     Two flags, and the fixup used to re-assert only the first.
        ///
        ///     <c>HasMet</c> is what the mod sets while the start is applied;
        ///     <c>IsKnownToPlayer</c> is what
        ///     <c>DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf</c>
        ///     actually reads, so without it the encyclopedia answers "You haven't
        ///     met this hero yet." and hides the portrait, the relation and every
        ///     value. The old fixup skipped a hero entirely once <c>HasMet</c> was
        ///     true, which is every hero it was written to repair, so the flag the
        ///     player can actually see was never put back.
        /// </summary>
        private static void KeepKnown(Hero hero)
        {
            if (!hero.HasMet) hero.SetHasMet();
            if (!hero.IsKnownToPlayer) hero.IsKnownToPlayer = true;
        }

        private void TickHeroFixup()
        {
            if (_fixupTicksRemaining <= 0) return;
            _fixupTicksRemaining--;

            try
            {
                var player = Hero.MainHero;
                if (player == null) return;

                if (player.HitPoints < player.MaxHitPoints)
                    player.HitPoints = player.MaxHitPoints;

                if (Clan.PlayerClan?.Heroes == null) return;
                foreach (var member in Clan.PlayerClan.Heroes)
                {
                    if (!member.IsAlive) continue;
                    KeepKnown(member);
                    if (member.HitPoints < member.MaxHitPoints)
                        member.HitPoints = member.MaxHitPoints;
                }

                // Everyone the life put in front of the player: the friends its
                // answers earned, kin who married out of the clan, their spouses,
                // and the heads of the houses that received them
                foreach (var face in CreationSession.Current.KnownFaces)
                    if (face.IsAlive)
                        KeepKnown(face);
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartBehavior: hero fixup failed.", ex);
                _fixupTicksRemaining = 0;
            }
        }
    }
}
