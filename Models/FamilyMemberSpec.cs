namespace CulturedStartReloaded.Models
{
    /// <summary>A relation the player can compose their starting family from.</summary>
    public enum FamilyRelation
    {
        Father,
        Mother,
        Spouse,
        Brother,
        Sister,
        Son,
        Daughter
    }

    /// <summary>Whether and how an adult relative is married at campaign start.</summary>
    public enum FamilyMarriage
    {
        Single,
        MarriedInClan,
        MarriedAway
    }

    /// <summary>
    ///     How a sibling's age is decided: a realistic spread around the player,
    ///     born together with the player (twins, triplets, and so on), or a
    ///     hand-set number.
    /// </summary>
    public enum SiblingAging
    {
        Smart,
        Twin,
        Exact
    }

    /// <summary>
    ///     One requested family member. Everyone has parents; the choice is
    ///     whether each relative is alive or dead at campaign start, how old they
    ///     are, and whether adults are married into or out of the clan.
    /// </summary>
    public sealed class FamilyMemberSpec
    {
        public FamilyMemberSpec(FamilyRelation relation, bool isAlive)
        {
            Relation = relation;
            IsAlive = isAlive;
        }

        public FamilyRelation Relation { get; }
        public bool IsAlive { get; set; }

        /// <summary>Exact age; null means a sensible age is chosen automatically.</summary>
        public int? Age { get; set; }

        /// <summary>Siblings only; other relations ignore it.</summary>
        public SiblingAging Aging { get; set; } = SiblingAging.Smart;

        /// <summary>
        ///     The smart mode's rolled offset from the player's age, held so
        ///     reads are stable and the roll follows a later age change.
        /// </summary>
        public int? SmartOffset { get; set; }

        public FamilyMarriage Marriage { get; set; } = FamilyMarriage.Single;

        /// <summary>
        ///     Adults ride in the party by default; set to keep them at one of
        ///     your holdings instead. Children always stay in a settlement.
        /// </summary>
        public bool StaysInSettlement { get; set; }

        /// <summary>Per-hero advanced customization; empty means fully generated.</summary>
        public HeroSpec Advanced { get; } = new();

        public override string ToString() =>
            $"{Relation}:{(IsAlive ? "alive" : "dead")}:{Age?.ToString() ?? "auto"}:{Marriage}";
    }
}
