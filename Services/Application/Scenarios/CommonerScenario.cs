namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>The commoner start changes no world state by design.</summary>
    public sealed class CommonerScenario : IScenarioApplier
    {
        public string Name => "Commoner";

        public string? Validate(StartContext context) => null;

        public void Apply(StartContext context)
        {
            CSLogger.Info("CommonerScenario: no world-state changes.");
        }
    }
}
