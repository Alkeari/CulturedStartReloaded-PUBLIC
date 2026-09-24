using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Puts the people who came with the player in the party: first the ones
    ///     the told life named, then the band the player asked for, up to what the
    ///     clan will actually hold.
    ///
    ///     The order is the point. A guided run can earn a specific person, and
    ///     that person outranks a count chosen from a menu, so when the clan has
    ///     fewer seats than both want it is the anonymous band that comes up short
    ///     and the player is told so.
    /// </summary>
    public sealed class CompanionStep : IStartStep
    {
        private readonly CompanionGenerator _generator = new();

        public string Name => "Companions";

        public string? Validate(StartContext context)
        {
            if (context.Session.StartingCompanions <= 0 && NamedAllies().Count == 0)
                return null;

            return context.Hero.PartyBelongedTo == null
                ? "hero has no party"
                : null;
        }

        public void Apply(StartContext context)
        {
            int requested = context.Session.StartingCompanions;
            int seats = LiveLimit(requested);

            var allies = NamedAllies();
            int allied = allies.Count == 0 ? 0 : _generator.GenerateAllies(context.Hero, allies);

            int granted = Math.Max(0, Math.Min(requested, seats - allied));

            if (allied > seats)
                CSLogger.Info(
                    $"CompanionStep: the life named {allied} people and the clan holds {seats}; " +
                    "they all came, which leaves the clan over its limit until it grows.");

            if (granted < requested)
                context.Report.AddProblem(
                    $"Companions: {requested} chosen, {granted} raised; the clan holds {seats} " +
                    $"and {allied} of those went to the people your life named");

            _generator.GenerateCompanions(context.Hero, granted);
        }

        /// <summary>
        ///     The people a guided run's answers said left with the player, less
        ///     the ones who are blood: an aunt belongs in the family tree, and
        ///     FamilyStep puts her there.
        ///
        ///     Public because the chapter that asks how many the player raises has
        ///     to say who rides besides them. A second count of the same people in
        ///     the menu would be a second copy of the rule, and the panel would be
        ///     free to promise a party the step never assembles.
        /// </summary>
        public static List<string> NamedAllies()
        {
            try
            {
                return GuidedRun.Outcome().Allies
                    .Where(id => !CompanionGenerator.IsKinAlly(id))
                    .ToList();
            }
            catch (Exception ex)
            {
                CSLogger.Error("CompanionStep: reading the allies the life earned failed.", ex);
                return new List<string>();
            }
        }

        /// <summary>
        ///     What the clan will actually hold right now. The editor offers a limit
        ///     probed under the planned tier, skills and perks; this is the same model
        ///     asked of the character that really exists, so a start never asks for a
        ///     companion the game has no seat for.
        /// </summary>
        private static int LiveLimit(int fallback)
        {
            try
            {
                var model = Campaign.Current?.Models?.ClanTierModel;
                var clan = Clan.PlayerClan;
                if (model == null || clan == null) return fallback;

                int limit = model.GetCompanionLimit(clan);
                CSLogger.Info(
                    $"CompanionStep: clan tier {clan.Tier}, renown {clan.Renown:F0}, " +
                    $"companion limit {limit}.");
                return limit;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"CompanionStep: reading the companion limit failed: {ex.Message}");
                return fallback;
            }
        }
    }
}
