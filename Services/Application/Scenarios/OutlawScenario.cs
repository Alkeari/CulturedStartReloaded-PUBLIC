using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Menus;
using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Starts the player as a wanted rogue: a configurable criminal rating with
    ///     the realm that outlawed them. Troops come from the bandit pool (handled
    ///     in TroopStep) and equipment tiers lean low via the Outlaw settings.
    ///
    ///     An outlawed lord is the same start holding a hall. The hall is granted only
    ///     while the crime rating stays under the game's own war threshold, for the
    ///     reason recorded on <see cref="ContextualMenus.OutlawMayHoldAHall(int)"/>.
    /// </summary>
    public sealed class OutlawScenario : IScenarioApplier
    {
        public string Name => "Outlaw";

        public string? Validate(StartContext context)
        {
            return context.Session.SelectedKingdom == null
                ? "no realm selected to be outlawed from"
                : null;
        }

        public void Apply(StartContext context)
        {
            var kingdom = context.Session.SelectedKingdom!;
            int rating = Math.Max(0,
                context.Session.CustomCrimeRating ?? context.Settings?.OutlawCrimeRating ?? 50);

            TakeTheHall(context, rating);

            if (rating == 0) return;

            ChangeCrimeRatingAction.Apply(kingdom, rating, false);
            CSLogger.Info($"OutlawScenario: crime rating {rating} with {kingdom.Name}.");

            // The composed extra realms share the same rating: a rogue wanted
            // across borders, not just at home
            if (context.Session.OutlawWantedBy == null) return;
            foreach (var id in context.Session.OutlawWantedBy)
            {
                var realm = TaleWorlds.CampaignSystem.Kingdom.All
                    .FirstOrDefault(k => k.StringId == id && k != kingdom && !k.IsEliminated);
                if (realm == null) continue;
                try
                {
                    ChangeCrimeRatingAction.Apply(realm, rating, false);
                    CSLogger.Info($"OutlawScenario: crime rating {rating} with {realm.Name}.");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"Outlaw: crime rating failed for {realm.Name}");
                    CSLogger.Error($"OutlawScenario: crime rating failed for {realm.Name}.", ex);
                }
            }
        }

        /// <summary>
        ///     The hall an outlawed lord is still sitting in, transferred to his clan the
        ///     way every other holding start transfers one. The clan is in no kingdom, which
        ///     the base game supports: a settlement's rebel clan holds its town outside every
        ///     realm, keeps it when the rebellion is over, and no AI crown ever proposes war
        ///     on anything that is not itself a kingdom.
        ///
        ///     The rating is checked here as well as on the menu because the crime chapter
        ///     sits ahead of the holding chapter: a player can take a hall, walk back, and
        ///     raise the crime past the point the realm marches. This end is the one that
        ///     decides, and it says so rather than quietly handing over a hall that brings
        ///     an army with it.
        /// </summary>
        private static void TakeTheHall(StartContext context, int rating)
        {
            var hall = context.Session.SelectedSettlement;
            if (hall == null) return;

            try
            {
                if (!ContextualMenus.OutlawMayHoldAHall(rating))
                {
                    context.Session.SelectedSettlement = null;
                    context.Report.AddProblem(
                        $"Outlaw: a crime rating of {rating} brings the realm's armies, so {hall.Name} was not taken");
                    CSLogger.Info($"OutlawScenario: {hall.Name} withheld; crime {rating} reaches the war threshold.");
                    return;
                }

                ChangeOwnerOfSettlementAction.ApplyByKingDecision(context.Hero, hall);
                CSLogger.Info($"OutlawScenario: holds {hall.Name} with a crime rating of {rating}.");
            }
            catch (Exception ex)
            {
                context.Session.SelectedSettlement = null;
                context.Report.AddProblem($"Outlaw: {hall.Name} could not be taken ({ex.GetType().Name})");
                CSLogger.Error($"OutlawScenario: taking {hall.Name} failed.", ex);
            }
        }
    }
}
