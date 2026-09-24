using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Grants the chosen hall of the target realm and puts the player clan at
    ///     war with it: a holdout in open rebellion. Degrades to a peaceful grant
    ///     with a report when the war declaration is refused.
    /// </summary>
    public sealed class RebelClanScenario : IScenarioApplier
    {
        public string Name => "Rebel Clan";

        /// <summary>
        ///     Whether this hall is one a rising can seize: a town or a castle of the realm
        ///     it rose against. A rebel clan taking a town of its former liege is the
        ///     ordinary shape of a revolt rather than an exception to it, and the game owns
        ///     a town for a clan outside every kingdom the same way it owns a castle.
        ///
        ///     Asked here and again by the holding chapter, so the hall the screen offers
        ///     and the hall this end grants cannot be two different kinds of place.
        /// </summary>
        public static bool MayBeSeized(Settlement? hall, Kingdom? realm) =>
            realm != null && IsHall(hall) && hall!.OwnerClan?.Kingdom == realm;

        public string? Validate(StartContext context)
        {
            var kingdom = context.Session.SelectedKingdom;
            if (kingdom == null)
                return "no realm selected to rebel against";

            if (IsHall(context.Session.SelectedSettlement))
                return null;

            return Settlement.All.Any(s => MayBeSeized(s, kingdom))
                ? null
                : "the realm holds no towns or castles to seize";
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var kingdom = context.Session.SelectedKingdom!;

            // Whatever the chapter or the editor bound is what is granted, town or castle
            // alike and whoever holds it today: the panel named that hall before the click,
            // so handing over a different one is the promise broken. Only an answer that is
            // no hall at all is replaced, and that is said out loud below rather than swapped.
            var chosen = context.Session.SelectedSettlement;
            var hall = IsHall(chosen)
                ? chosen
                : PickHolding(kingdom, context.Session.Mode == Models.SetupMode.LifePath);

            if (hall == null)
                throw new InvalidOperationException("RebelClanScenario: no town or castle available.");

            if (chosen != null && chosen != hall)
                context.Report.AddProblem(
                    $"Rebel Clan: {chosen.Name} is no hall to hold, so the rising seized {hall.Name} instead");

            // Record the holding the start actually granted. When the player left it on
            // Automatic the session still says nothing, and LocationStep then falls through to
            // a random town of the culture: granted Goldgrass, spawned at Mormont Keep. Every
            // later step that anchors to the holding reads it from here.
            context.Session.SelectedSettlement = hall;

            ChangeOwnerOfSettlementAction.ApplyByKingDecision(hero, hall);
            CSLogger.Info($"RebelClanScenario: seized {hall.Name} from {kingdom.Name}.");

            try
            {
                DeclareWarAction.ApplyByDefault(kingdom, hero.Clan);
                CSLogger.Info($"RebelClanScenario: {kingdom.Name} is at war with the player clan.");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Rebel Clan: war declaration failed ({ex.GetType().Name}); holding {hall.Name} in peace");
                CSLogger.Error("RebelClanScenario: war declaration failed.", ex);
            }

            CreateAllies(context, kingdom, hall);
        }

        /// <summary>
        ///     The composed fellow rebel houses: full noble clans risen near the
        ///     seized hall, sworn to nobody, at war with the same realm.
        /// </summary>
        private static void CreateAllies(StartContext context, Kingdom kingdom, Settlement hall)
        {
            int count = Math.Max(0, context.Session.RebelAllyCount);
            for (int index = 0; index < count; index++)
            {
                var leader = CulturedStartReloaded.Services.VassalGenerator.CreateRebelAllyClan(
                    kingdom, hall, index, context.Session, context.Settings);
                if (leader == null)
                    context.Report.AddProblem("Rebel Clan: a fellow rebel house could not be raised");
            }
        }

        /// <summary>
        ///     The hall the stewards find when the player named none, ranging over exactly
        ///     what the chapter would have offered so the count its panel states and the
        ///     grant are the same set. Cultured Start's rising has only ever seized a castle.
        /// </summary>
        private static Settlement? PickHolding(Kingdom kingdom, bool castlesOnly) =>
            CSRandom.Pick(Settlement.All
                .Where(s => MayBeSeized(s, kingdom) && (!castlesOnly || s.IsCastle))
                .ToList());

        private static bool IsHall(Settlement? settlement) =>
            settlement != null && (settlement.IsTown || settlement.IsCastle);
    }
}
