using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     How far a start goes onto the water. A degree of an existing start
    ///     rather than a start of its own: the start list already crosses kinds
    ///     with degrees, and a ninth <see cref="StartType"/> would buy nothing but
    ///     a wider reachability matrix.
    /// </summary>
    public enum SeaDegree
    {
        /// <summary>The start stays on land, which is every start that is not offered the water.</summary>
        None,

        /// <summary>An outlaw who raids from shallow-draft hulls.</summary>
        Raider,

        /// <summary>A landed vassal seated on a castle with a port.</summary>
        Admiral,

        /// <summary>A caravan master whose caravan sails.</summary>
        Venturer
    }

    /// <summary>
    ///     Which start type may be offered the water, and how much of a life at sea
    ///     it takes to be offered it.
    ///
    ///     Kept free of engine types so the test project can compile it and hold
    ///     the reachability claim to real walked lives. The guided route scores
    ///     <see cref="LifeProfile.Lean.Sea"/> from exactly one target, so a walked
    ///     life comes out at 0, 3 or 5 and nothing else; the floors below are set
    ///     against those three values and not against a scale that does not exist.
    /// </summary>
    public static class SeaDegrees
    {
        /// <summary>
        ///     One voyage is enough to turn an outlaw's crimes seaward, and enough
        ///     for a trader to send their goods by water instead of by road.
        /// </summary>
        public const int OneVoyage = 3;

        /// <summary>
        ///     A crown does not put three hulls under a man who was once on a boat,
        ///     so the seat on the water asks for the life that kept going back.
        /// </summary>
        public const int ALifeOnIt = 5;

        /// <summary>The degree this start type can be offered, or None where it has none.</summary>
        public static SeaDegree For(StartType startType)
        {
            switch (startType)
            {
                case StartType.Outlaw:
                    return SeaDegree.Raider;
                case StartType.LandedVassal:
                    return SeaDegree.Admiral;
                case StartType.CaravanMaster:
                    return SeaDegree.Venturer;
                default:
                    return SeaDegree.None;
            }
        }

        /// <summary>How much sea in a life this degree asks for.</summary>
        public static int Floor(SeaDegree degree)
        {
            switch (degree)
            {
                case SeaDegree.Raider:
                    return OneVoyage;
                case SeaDegree.Admiral:
                    return ALifeOnIt;
                case SeaDegree.Venturer:
                    return OneVoyage;
                default:
                    return int.MaxValue;
            }
        }

        /// <summary>
        ///     Whether this life went to sea enough to be offered this degree. A
        ///     life that cannot be read is offered nothing: the chapter is the
        ///     whole of the water, so a failure to read has to fail shut rather
        ///     than hand a landlocked character three hulls.
        /// </summary>
        public static bool LifeReaches(LifeProfile? life, SeaDegree degree) =>
            degree != SeaDegree.None && life != null &&
            life.Score(LifeProfile.Lean.Sea) >= Floor(degree);
    }
}
