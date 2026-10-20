namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     One unit of post-creation world mutation. Validate runs for every step
    ///     before any Apply mutates the world; a step whose validation reports a
    ///     problem is skipped entirely and the player is told, so the campaign is
    ///     never left half-applied by a predictable failure.
    /// </summary>
    public interface IStartStep
    {
        string Name { get; }

        /// <summary>Returns null when the step can run, or a problem description to skip it.</summary>
        string? Validate(StartContext context);

        void Apply(StartContext context);
    }
}
