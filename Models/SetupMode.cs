namespace CulturedStartReloaded.Models
{
    /// <summary>How the narrative path stocks the party's starting inventory.</summary>
    public enum ProvisionPlan
    {
        Sensible,
        Light,
        Bare
    }

    /// <summary>
    ///     The player's chosen creation path, picked on the first menu or on the
    ///     game's own starting-options screen from v1.5.0 up.
    ///
    ///     Two of the four are guided routes and the player meets them by name:
    ///     <see cref="LifePath"/> is Cultured Start, the seven chapters the mod
    ///     asked before the rewrite, and <see cref="Narrative"/> is Cultured Start
    ///     Revamped, the thirteen scenes that replaced them. They ask different
    ///     questions and nothing else about them differs: both write the same
    ///     session fields and are applied by the same twelve-step pipeline.
    /// </summary>
    public enum SetupMode
    {
        /// <summary>Cultured Start Revamped: the thirteen branching scenes.</summary>
        Narrative,

        /// <summary>Cultured Start: the seven life-path chapters.</summary>
        LifePath,

        /// <summary>The Start Editor: every option, one logical surface.</summary>
        Custom,

        /// <summary>No mod menus and no mod changes; a plain vanilla start.</summary>
        Vanilla
    }
}
