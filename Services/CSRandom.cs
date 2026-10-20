using System;
using System.Collections.Generic;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The mod's single random source. One generator per process, reseeded per
    ///     campaign start, so no two call sites can collide on time-seeded instances.
    /// </summary>
    public static class CSRandom
    {
        private static Random _rng = new();

        /// <summary>Reseeds the generator; called once per campaign start.</summary>
        public static void Reseed()
        {
            _rng = new Random(unchecked(Environment.TickCount * 31 + 17));
        }

        public static int Next(int maxExclusive) => _rng.Next(maxExclusive);

        /// <summary>
        ///     A draw settled by the run rather than by when it is asked: one seed
        ///     and one key always answer the same number, and asking costs the
        ///     shared sequence above nothing.
        ///
        ///     The effect panel needs this. It is drawn before anything is applied
        ///     and redrawn every time the player moves, so a panel that took its
        ///     number from <see cref="Next(int)" /> would shift every later draw in
        ///     the run and shift it again on each redraw. Here the panel, the
        ///     household and the apply pipeline can each ask as often as they like
        ///     and read the same answer.
        /// </summary>
        public static int Stable(int seed, string key, int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;

            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char c in key ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                // FNV leaves most of its variation in the high bits and the modulus
                // keeps only the low ones, so the two carry the spread downward
                hash ^= hash >> 16;
                hash *= 2246822507u;
                hash ^= hash >> 13;

                return (int)(hash % (uint)maxExclusive);
            }
        }

        public static int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        /// <summary>Returns a random element, or default when the list is empty.</summary>
        public static T? Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) return default;
            return items[_rng.Next(items.Count)];
        }
    }
}
