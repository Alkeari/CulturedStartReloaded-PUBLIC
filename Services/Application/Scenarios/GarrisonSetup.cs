using System;
using System.Linq;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem.Settlements;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Staffs the starting holding's garrison to the composed size, in both
    ///     directions: recruits of the holding's culture fill it up, and an
    ///     inherited garrison larger than asked musters out the difference.
    /// </summary>
    public static class GarrisonSetup
    {
        public static void Apply(StartContext context)
        {
            int? wanted = context.Session.CustomGarrison;
            if (wanted == null) return;

            // The holding the start granted, not whichever the clan happens to list first. Only
            // the Monarch owns more than one here, capital plus granted castles, and the number the
            // player chose was sized against the capital; garrisoning a castle instead put the
            // troops somewhere else and named a holding they never picked in the trim message.
            var granted = context.Session.SelectedSettlement;
            var owned = context.Hero.Clan?.Settlements;
            var holding = owned != null && granted != null && owned.Contains(granted)
                ? granted
                : owned?.FirstOrDefault(s => s.IsTown || s.IsCastle);
            if (holding == null)
            {
                context.Report.AddProblem("Garrison: no holding to garrison");
                return;
            }

            try
            {
                var garrison = holding.Parties?.FirstOrDefault(p => p.IsGarrison);
                if (garrison == null)
                {
                    holding.AddGarrisonParty();
                    garrison = holding.Parties?.FirstOrDefault(p => p.IsGarrison);
                }

                if (garrison == null)
                {
                    context.Report.AddProblem($"Garrison: no garrison party could be raised at {holding.Name}");
                    return;
                }

                // The offer and the grant can disagree, and only this end knows the truth. When the
                // holding is still undecided the editor has no settlement to size against, so it
                // offers the largest garrison limit in the world; the start then grants a specific
                // fief, which is usually smaller. Asking for 700 and being handed a fief that holds
                // 499 produced a garrison of 700/499. Clamped here, and said out loud rather than
                // silently trimmed, because the number the player chose is not the number they got.
                int target = Math.Max(0, wanted.Value);
                int limit = GarrisonLimit(garrison);
                if (limit > 0 && target > limit)
                {
                    context.Report.AddProblem(
                        $"Garrison: {holding.Name} holds {limit}, not {target}; the rest was not raised");
                    CSLogger.Info($"GarrisonSetup: requested {target} clamped to {limit} at {holding.Name}.");
                    target = limit;
                }

                int current = garrison.MemberRoster.TotalManCount;
                if (target > current)
                {
                    TroopStep.FillRoster(garrison, holding.Culture ?? context.Hero.Culture,
                        target - current, context.Session.EffectiveClanTier);
                }
                else if (target < current)
                {
                    int toRemove = current - target;
                    for (int i = garrison.MemberRoster.Count - 1; i >= 0 && toRemove > 0; i--)
                    {
                        var element = garrison.MemberRoster.GetElementCopyAtIndex(i);
                        if (element.Character == null || element.Character.IsHero) continue;
                        int removed = Math.Min(element.Number, toRemove);
                        garrison.MemberRoster.AddToCounts(element.Character, -removed);
                        toRemove -= removed;
                    }
                }

                CSLogger.Info(
                    $"GarrisonSetup: {holding.Name} garrison {current} -> {garrison.MemberRoster.TotalManCount}.");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Garrison staffing failed: {ex.GetType().Name}");
                CSLogger.Error("GarrisonSetup: staffing failed.", ex);
            }
        }

        /// <summary>
        ///     What this garrison can actually hold, from the installed party size model, so a mod
        ///     that changes garrison capacity answers for itself. Zero when it cannot be read, which
        ///     the caller treats as no clamp rather than as a limit of nothing.
        /// </summary>
        private static int GarrisonLimit(TaleWorlds.CampaignSystem.Party.MobileParty garrison)
        {
            try
            {
                var model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.PartySizeLimitModel;
                return model == null
                    ? 0
                    : Math.Max(0, (int)model.GetPartyMemberSizeLimit(garrison.Party).ResultNumber);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GarrisonSetup: reading the garrison limit failed: {ex.Message}");
                return 0;
            }
        }
    }
}
