using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Signs a mercenary contract with the chosen kingdom at the composed
    ///     pay, defaulting to what the game's own model would offer.
    /// </summary>
    public sealed class MercenaryScenario : IScenarioApplier
    {
        public string Name => "Mercenary";

        public string? Validate(StartContext context)
        {
            return context.Session.SelectedKingdom == null
                ? "no kingdom selected"
                : null;
        }

        public void Apply(StartContext context)
        {
            var kingdom = context.Session.SelectedKingdom!;
            int pay = context.Session.CustomContractPay ?? GameCaps.ContractPayOffer(kingdom);
            VersionedGameApi.JoinAsMercenary(context.Hero.Clan, kingdom, pay);
            CSLogger.Info($"MercenaryScenario: contracted with {kingdom.Name} at {pay} denars per influence.");
        }
    }
}
