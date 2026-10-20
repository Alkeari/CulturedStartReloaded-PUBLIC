using System;
using System.Collections.Generic;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Which candidate a slot lands on.
    ///
    ///     Free of engine types, because the question this answers cannot be
    ///     answered inside one: how many different outfits a standing can produce,
    ///     and how often the character's own people supply them, is a property of
    ///     the draw and has to be measurable against the real item list without a
    ///     running game.
    /// </summary>
    public static class GearDraw
    {
        /// <summary>
        ///     How much of the draw the character's own wardrobe keeps. Culture is
        ///     a lean and never a gate: three draws in four come from the
        ///     character's own people or from gear that belongs to no people at
        ///     all, and the fourth reaches the rest of the world. A share of the
        ///     whole draw rather than a weight on each piece, so the lean reads the
        ///     same whether home is one piece against forty or forty against one.
        ///     An empty side gives its share to the other, so a thin culture is
        ///     dressed rather than left bare and a rich one never has to borrow.
        /// </summary>
        private const double HomeShare = 0.75;

        /// <summary>
        ///     How many candidates a slot needs before the draw stops reaching a
        ///     tier further down. A pool of one is a uniform.
        /// </summary>
        public const int MinimumVariety = 6;

        /// <summary>
        ///     One thing a slot could be filled with, carrying only what the draw
        ///     reads: what tier it is, whose people it belongs to (null for gear
        ///     that belongs to none), and how well it answers the story, which is
        ///     the caller's own ordering expressed as a number, higher first.
        /// </summary>
        public readonly struct Candidate<T> where T : class
        {
            public Candidate(T item, string id, int tier, string? cultureId, double rank)
            {
                Item = item;
                Id = id;
                Tier = tier;
                CultureId = cultureId;
                Rank = rank;
            }

            public T Item { get; }

            public string Id { get; }

            public int Tier { get; }

            public string? CultureId { get; }

            public double Rank { get; }
        }

        /// <summary>
        ///     The candidates someone of this standing would actually be wearing:
        ///     the nearest tier the pool holds, widened one tier at a time only
        ///     while there is too little there to vary. Tier is what the story
        ///     bought and is never traded away for variety, so the widening stops
        ///     the moment the band can vary on its own.
        ///
        ///     One thing does keep it widening: a band holding nothing of the
        ///     character's own while their people made something for this slot
        ///     further down. Leaning towards their own wardrobe means nothing if
        ///     the band never reaches it, so the search goes on until it does, or
        ///     until it has read the whole pool and found there is nothing of
        ///     theirs to find.
        /// </summary>
        public static List<Candidate<T>> Band<T>(IReadOnlyList<Candidate<T>> pool, int targetTier,
            int minimumVariety, string? homeCultureId) where T : class
        {
            var band = new List<Candidate<T>>();
            if (pool == null || pool.Count == 0) return band;

            bool homeExists = false;
            var distances = new SortedSet<int>();
            foreach (var candidate in pool)
            {
                distances.Add(Math.Abs(candidate.Tier - targetTier));
                if (IsHome(candidate.CultureId, homeCultureId)) homeExists = true;
            }

            bool homeFound = false;
            foreach (int distance in distances)
            {
                foreach (var candidate in pool)
                {
                    if (Math.Abs(candidate.Tier - targetTier) != distance) continue;
                    band.Add(candidate);
                    if (IsHome(candidate.CultureId, homeCultureId)) homeFound = true;
                }

                if (band.Count >= minimumVariety && (homeFound || !homeExists)) break;
            }

            return band;
        }

        /// <summary>
        ///     One candidate, drawn rather than chosen.
        ///
        ///     The story's best answer is the likeliest and every other one in the
        ///     band stays reachable, so asking again hands back a different
        ///     character. <paramref name="avoidId" /> is what the slot is already
        ///     wearing: while the band holds anything else, the draw will not
        ///     return it, which is what makes a second click cycle rather than
        ///     re-roll the same piece. A band of one repeats, because by then the
        ///     choices really have narrowed it to one.
        /// </summary>
        public static T? Pick<T>(IReadOnlyList<Candidate<T>> pool, int targetTier,
            string? homeCultureId, int minimumVariety, string? avoidId, Func<int, int> roll)
            where T : class
        {
            var band = Band(pool, targetTier, minimumVariety, homeCultureId);
            if (band.Count == 0) return null;

            if (avoidId != null && band.Count > 1)
            {
                var without = new List<Candidate<T>>(band.Count);
                foreach (var candidate in band)
                    if (!string.Equals(candidate.Id, avoidId, StringComparison.Ordinal))
                        without.Add(candidate);

                if (without.Count > 0) band = without;
            }

            band.Sort((left, right) => right.Rank.CompareTo(left.Rank));

            var weights = new double[band.Count];
            double homeTotal = 0;
            double foreignTotal = 0;
            for (int position = 0; position < band.Count; position++)
            {
                // Harmonic preference: the best answer leads by a wide margin and
                // the tail never falls out of reach
                weights[position] = 1.0 / (position + 1);
                if (IsHome(band[position].CultureId, homeCultureId))
                    homeTotal += weights[position];
                else
                    foreignTotal += weights[position];
            }

            if (homeTotal > 0 && foreignTotal > 0)
                for (int position = 0; position < band.Count; position++)
                    weights[position] *= IsHome(band[position].CultureId, homeCultureId)
                        ? HomeShare / homeTotal
                        : (1.0 - HomeShare) / foreignTotal;

            double total = 0;
            foreach (double weight in weights) total += weight;
            if (total <= 0) return band[0].Item;

            double target = total * roll(int.MaxValue) / int.MaxValue;
            double running = 0;
            for (int position = 0; position < band.Count; position++)
            {
                running += weights[position];
                if (running > target) return band[position].Item;
            }

            return band[band.Count - 1].Item;
        }

        /// <summary>
        ///     Whose wardrobe a piece counts as. Gear belonging to no people is the
        ///     character's as much as their own culture's, which is the case a
        ///     culture filter gets wrong by treating a missing culture as a
        ///     mismatch. A character with no culture of their own has no lean to
        ///     apply, so the whole world is home to them.
        /// </summary>
        private static bool IsHome(string? itemCultureId, string? homeCultureId)
        {
            if (homeCultureId == null) return true;
            if (itemCultureId == null) return true;
            return string.Equals(itemCultureId, homeCultureId, StringComparison.Ordinal);
        }
    }
}
