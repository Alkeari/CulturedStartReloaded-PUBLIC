namespace CulturedStartReloaded.Models
{
    /// <summary>
    ///     How the character carries their station on their back. It moves the
    ///     quartermaster one step either side of the tier the clan would suggest,
    ///     never outside the range the start type allows.
    /// </summary>
    public enum ArmorBearing
    {
        /// <summary>Below your station: hard wearing, and nothing worth stealing.</summary>
        Plain,

        /// <summary>Exactly what someone of your standing would be expected to wear.</summary>
        Station,

        /// <summary>Above your station: everything you own is on your back.</summary>
        Finest
    }
}
