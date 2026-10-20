using System.Collections.Generic;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     How many years lie between the player and each brother or sister,
    ///     settled off the run's own seed rather than drawn when somebody happens
    ///     to ask.
    ///
    ///     A scene stages a sibling chapters before the household chapter that
    ///     composes them, so the distance has to be answerable before anybody
    ///     exists and has to answer the same on every later ask. Drawn from the
    ///     shared generator it was neither: the childhood stage assumed four years
    ///     younger while the household rolled a year older, so one sister was a
    ///     child of nine beside a boy of thirteen and then thirty-one beside a man
    ///     of thirty.
    /// </summary>
    public static class SiblingOffsets
    {
        /// <summary>The widest gap this puts between two children of one house.</summary>
        public const int MaxDistance = 8;

        /// <summary>Distances tried before the house settles for the usual one.</summary>
        private const int Attempts = 24;

        /// <summary>How often a house large enough holds a pair born together, per hundred.</summary>
        private const int TwinChance = 8;

        private const int UsualDistance = -4;

        /// <summary>
        ///     The years between the player and the sibling who comes Nth in the
        ///     household, negative where that sibling was born after them.
        ///
        ///     The first of them is always the younger: the childhood scenes stage
        ///     that sibling behind a child of thirteen and the script they are
        ///     written from names a younger one, so an older first sibling would
        ///     stand an adult in a room the scene wrote a child into.
        /// </summary>
        public static int For(int runSeed, int ordinal, ICollection<int>? taken = null)
        {
            // A house may hold one pair born together, and only a third child or
            // later can be one. Which of them it is must not depend on how many
            // the household ends up holding, or the sibling a scene stages before
            // the household exists would settle at one distance and the composed
            // person at another
            if (ordinal >= 2 && taken?.Contains(0) != true &&
                CSRandom.Stable(runSeed, Key(ordinal, "twin"), 100) < TwinChance)
                return 0;

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                int distance =
                    1 + CSRandom.Stable(runSeed, Key(ordinal, "years" + attempt), MaxDistance);

                int candidate = ordinal == 0 ||
                                CSRandom.Stable(runSeed, Key(ordinal, "order" + attempt), 2) == 0
                    ? -distance
                    : distance;

                if (taken?.Contains(candidate) != true) return candidate;
            }

            return UsualDistance;
        }

        private static string Key(int ordinal, string part) => "sibling:" + ordinal + ":" + part;
    }
}
