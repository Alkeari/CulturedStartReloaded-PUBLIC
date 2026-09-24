using System;
using System.Collections.Generic;
using System.Linq;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What moving a Revamped character's age does to how much of a life they
    ///     carry.
    ///
    ///     The game's own adults say a younger person is less capable and give no
    ///     support at all for an older one being more so: the best skill of the
    ///     lords it ships rises steeply to about twenty-nine and is flat after. So
    ///     below the age a life came to, the share of a life follows that curve,
    ///     measured from the running world; above it, the share closes toward a
    ///     whole life in proportion to the years added, which is a design choice
    ///     rather than a reading and caps at the most capable adult alive.
    ///
    ///     Free of engine types, so the arithmetic can be held to its rule without
    ///     a running game; the population is handed in by whoever read it.
    /// </summary>
    public static class AgeCurve
    {
        /// <summary>
        ///     The oldest a Revamped character can be moved to. Not a threshold the
        ///     game's own age model declares; it is where a moved age reaches a whole
        ///     life.
        /// </summary>
        public const int Ceiling = 50;

        /// <summary>Years either side of an age whose people are read as that age.</summary>
        private const int HalfWindow = 2;

        /// <summary>
        ///     The best skill the world's people carry at every age from the floor to
        ///     the ceiling, averaged over the ages beside it and never falling as age
        ///     rises. An age nobody in the world stands near takes the nearest
        ///     measured value below it, or above it at the young end.
        /// </summary>
        public static double[] Measure(IEnumerable<(int Age, int Best)> people, int floor, int ceiling)
        {
            var read = people.Where(person => person.Best > 0).ToList();
            var curve = new double[Math.Max(0, ceiling - floor + 1)];

            double running = 0;
            for (int age = floor; age <= ceiling; age++)
            {
                var near = read.Where(person => Math.Abs(person.Age - age) <= HalfWindow).ToList();
                if (near.Count > 0) running = Math.Max(running, near.Average(person => (double)person.Best));
                curve[age - floor] = running;
            }

            double first = curve.FirstOrDefault(value => value > 0);
            for (int index = 0; index < curve.Length && curve[index] <= 0; index++)
                curve[index] = first;

            return curve;
        }

        /// <summary>One age read off a measured curve, held to the ages it covers.</summary>
        public static double At(double[] curve, int floor, int age)
        {
            if (curve.Length == 0) return 0;
            return curve[Math.Max(0, Math.Min(curve.Length - 1, age - floor))];
        }

        /// <summary>
        ///     The share of a life a character carries at <paramref name="target" />
        ///     years when the life itself came to <paramref name="derived" /> years
        ///     and <paramref name="share" />. The target is held between the floor
        ///     and the ceiling, and the derived age gives the share back unchanged.
        /// </summary>
        public static double Share(double share, int derived, int target, Func<int, double> bestAt,
            int floor, int ceiling)
        {
            int age = Math.Max(floor, Math.Min(ceiling, target));
            if (age == derived) return share;

            if (age < derived)
            {
                double then = bestAt(age);
                double now = bestAt(derived);
                return now <= 0 ? share : share * Math.Min(1.0, then / now);
            }

            if (derived >= ceiling) return share;

            double toward = (age - derived) / (double)(ceiling - derived);
            return Math.Min(1.0, share + (1.0 - share) * toward);
        }
    }
}
