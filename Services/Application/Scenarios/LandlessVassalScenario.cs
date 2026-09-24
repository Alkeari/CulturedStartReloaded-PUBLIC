using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>Joins the chosen kingdom as a vassal without holdings.</summary>
    public sealed class LandlessVassalScenario : IScenarioApplier
    {
        public string Name => "Landless Vassal";

        public string? Validate(StartContext context)
        {
            return context.Session.SelectedKingdom == null
                ? "no kingdom selected"
                : null;
        }

        public void Apply(StartContext context)
        {
            var kingdom = context.Session.SelectedKingdom!;
            ChangeKingdomAction.ApplyByJoinToKingdom(context.Hero.Clan, kingdom, showNotification: false);
            CSLogger.Info($"LandlessVassalScenario: joined {kingdom.Name}.");
        }
    }
}
