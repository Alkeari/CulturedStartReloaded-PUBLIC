using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>Joins the chosen kingdom as a vassal and grants a fief within it.</summary>
    public sealed class LandedVassalScenario : IScenarioApplier
    {
        public string Name => "Landed Vassal";

        public string? Validate(StartContext context)
        {
            return context.Session.SelectedKingdom == null
                ? "no kingdom selected"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var kingdom = context.Session.SelectedKingdom!;

            ChangeKingdomAction.ApplyByJoinToKingdom(hero.Clan, kingdom, showNotification: false);
            CSLogger.Info($"LandedVassalScenario: joined {kingdom.Name}.");

            var fief = context.Session.SelectedSettlement
                       ?? SettlementFinder.RandomVassalFief(hero.Culture, kingdom);

            if (fief != null)
            {
                // Record the holding the start actually granted. When the player left it on
                // Automatic the session still says nothing, and LocationStep then falls through to
                // a random town of the culture: granted Goldgrass, spawned at Mormont Keep. Every
                // later step that anchors to the holding reads it from here.
                context.Session.SelectedSettlement = fief;

                var dispossessed = fief.OwnerClan;
                ChangeOwnerOfSettlementAction.ApplyByKingDecision(hero, fief);
                CSLogger.Info($"LandedVassalScenario: granted fief {fief.Name}.");

                // The clan that lost the fief remembers who it went to, and it
                // remembers a town harder than a castle: the loss is what the
                // game's own diplomacy model prices handing that holding over at.
                var dispossessedLeader = dispossessed?.Leader;
                if (dispossessed != null && dispossessed != hero.Clan && dispossessedLeader != null)
                {
                    // Cultured Start prices every fief as a castle, as it always has
                    int loss = Steps.ConsequenceStep.WorthOfHolding(
                        context.Session.Mode == Models.SetupMode.LifePath ? null : fief);
                    ChangeRelationAction.ApplyPlayerRelation(dispossessedLeader, -loss, false, false);
                    CSLogger.Info(
                        $"LandedVassalScenario: -{loss} relation with {dispossessedLeader.Name} (dispossessed).");
                }
            }
            else
            {
                context.Report.AddProblem("Landed Vassal: no fief available; starting without holdings");
            }
        }
    }
}
