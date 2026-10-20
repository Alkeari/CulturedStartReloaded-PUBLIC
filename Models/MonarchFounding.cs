namespace CulturedStartReloaded.Models
{
    /// <summary>How a Monarch start came into its holdings.</summary>
    public enum MonarchFounding
    {
        /// <summary>Holdings granted from a weak realm's territory; no immediate war.</summary>
        Settler,

        /// <summary>Holdings taken as a pressed claim; the dispossessed realm declares war.</summary>
        Claimant
    }
}
