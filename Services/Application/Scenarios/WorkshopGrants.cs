using System;
using System.Linq;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     The composed number of workshops, taken over in the nearest towns,
    ///     starting from where the character begins. Shared by every propertied
    ///     start type.
    /// </summary>
    public static class WorkshopGrants
    {
        public static void Apply(StartContext context)
        {
            int wanted = Math.Max(0, context.Session.StartingWorkshops);
            if (wanted == 0) return;

            var hero = context.Hero;

            // Workshops are bought around where the player starts, but this runs inside the scenario
            // step and LocationStep does not resolve "let fate decide" until five steps later.
            // Resolving it here and recording it makes both agree: without this the shops were
            // bought around the hero's home settlement and the party was placed somewhere else, on a
            // large map a different continent away.
            if (context.Session.SelectedSettlement == null && context.Session.SelectedLocation == null)
                context.Session.SelectedLocation = SettlementFinder.RandomCultureTown(hero.Culture);

            var origin = context.Session.SelectedSettlement
                         ?? context.Session.SelectedLocation
                         ?? hero.HomeSettlement;

            var candidates = Settlement.All
                .Where(s => s.IsTown && s.Town?.Workshops != null)
                .OrderBy(s => origin == null ? 0f : VersionedGameApi.DistanceSquared(origin, s))
                .SelectMany(s => s.Town.Workshops)
                .Where(w => w.WorkshopType != null && w.Owner != hero && !w.WorkshopType.IsHidden)
                .Take(wanted)
                .ToList();

            int granted = 0;
            foreach (var workshop in candidates)
            {
                try
                {
                    ChangeOwnerOfWorkshopAction.ApplyByBankruptcy(workshop, hero, workshop.WorkshopType, 0);
                    granted++;
                    CSLogger.Info(
                        $"WorkshopGrants: {workshop.WorkshopType.Name} in {workshop.Settlement?.Name} acquired.");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"Workshop acquisition failed: {ex.GetType().Name}");
                    CSLogger.Error("WorkshopGrants: workshop acquisition failed.", ex);
                }
            }

            if (granted < wanted)
                context.Report.AddProblem($"Workshops: only {granted} of {wanted} could be acquired");
        }
    }
}
