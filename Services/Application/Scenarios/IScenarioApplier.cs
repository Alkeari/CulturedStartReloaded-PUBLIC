namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>World-state changes for one start type (kingdom, fiefs, contracts).</summary>
    public interface IScenarioApplier
    {
        string Name { get; }

        /// <summary>Returns null when the scenario can run, or a problem description to skip it.</summary>
        string? Validate(StartContext context);

        void Apply(StartContext context);
    }
}
