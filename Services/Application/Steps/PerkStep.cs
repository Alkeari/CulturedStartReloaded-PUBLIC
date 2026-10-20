using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Grants the planned perks: the player's explicit choices, plus, on the
    ///     custom path, the game model's pick for every remaining reachable slot,
    ///     so the character boots in with nothing left to click through. Runs
    ///     before companions because the companion limit is perk-dependent.
    ///     Leftover-point cleanup happens after the map loads, in
    ///     <see cref="Behaviors.CulturedStartBehavior"/>, because the game grants
    ///     level-up points after creation ends.
    /// </summary>
    public sealed class PerkStep : IStartStep
    {
        public string Name => "Perks";

        public string? Validate(StartContext context)
        {
            return context.Hero.HeroDeveloper == null
                ? "hero has no developer"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var planned = PerkPlanner.Plan(hero, context.Session);

            foreach (var perk in planned)
                hero.HeroDeveloper.AddPerk(perk);

            if (planned.Count > 0)
                CSLogger.Info($"PerkStep: {planned.Count} perks applied.");

            // The only two perks that move the companion limit, logged because the editor's
            // offer is probed with them planned and a disagreement here is what a player sees
            // as being offered one companion more than the game grants.
            CSLogger.Info(
                "PerkStep: companion-limit perks: " +
                $"WePledgeOurSwords={hero.GetPerkValue(DefaultPerks.Leadership.WePledgeOurSwords)}, " +
                $"Camaraderie={hero.GetPerkValue(DefaultPerks.Charm.Camaraderie)}.");
        }
    }
}
