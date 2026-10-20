using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What a set of answers adds up to.
    ///
    ///     The guided route asks about situations and never asks what the character
    ///     is, so everything the rest of the mod needs about them has to be read
    ///     back out of what their answers left behind. <c>LifeProfile</c>
    ///     does the narrow version of this over eight enum answers; this does it
    ///     over a flat list of consequences, so a chapter can be added, rewritten
    ///     or removed without anything here knowing about it.
    ///
    ///     Seven facets, then three things inferred from them: the station the life
    ///     earned, the age it implies, and whether it made a specialist.
    ///
    ///     Nothing here is a game value. A facet is a share of what the answers
    ///     said, and the only number that leaves this class in game units is the
    ///     age, which comes from <see cref="StartingAge"/>'s own members.
    /// </summary>
    public sealed partial class Portrait
    {
        /// <summary>The seven things a set of answers can tell us about a person.</summary>
        public enum Facet
        {
            /// <summary>What the character was handed before they had done anything.</summary>
            Origins,

            /// <summary>The bent of them: what they reach for without being asked.</summary>
            Inclination,

            /// <summary>How much of them was put there deliberately, by someone teaching.</summary>
            Schooling,

            /// <summary>Depth rather than breadth: the thing they returned to.</summary>
            Mastery,

            /// <summary>How sharply the life turned, and how much it cost to turn.</summary>
            Hinge,

            /// <summary>What the life is aimed at now, rather than what made it.</summary>
            Intent,

            /// <summary>How much living the answers describe.</summary>
            Seasoning
        }

        /// <summary>How far a facet reaches. Three bands, because a sentence can only be one of a few.</summary>
        public enum Depth
        {
            Faint,
            Present,
            Strong
        }

        // The weight vocabulary <see cref="Weights"/> is written in, so no row of it
        // is a bare number. It mirrors Severity deliberately: "how much does this
        // matter" has one answer shape in this mod.

        /// <summary>A mark that shows in the telling and barely anywhere else.</summary>
        private const int Slight = 1;

        /// <summary>A mark that colors a life without deciding it.</summary>
        private const int Telling = 2;

        /// <summary>A mark that decides the shape of what follows.</summary>
        private const int Decisive = 3;

        // Whether a row is said once for each time its kind happened, or said again
        // for every return to the same named thing. It is marked on the row it
        // repeats rather than found by scanning the rows for the loudest, because
        // three kinds speak equally loudly about two facets at once: taking the
        // first strictly greatest row gave the choice to whichever line happened to
        // be written first, so swapping two lines that say the same thing silently
        // changed what returning to the same place MEANS.

        /// <summary>Said again for every return to the same named thing.</summary>
        private const bool Again = true;

        /// <summary>Said once for each time it happened and no more, return or not.</summary>
        private const bool Once = false;

        // Which side of the ordinary life a station asks a facet to stand. How FAR
        // it asks is the station's own distance, written once per station in
        // <see cref="Shape"/> rather than once per row, because a station stands a
        // distance from the ordinary life and its facets do not stand different
        // distances from each other.

        /// <summary>Further from the ordinary life than an ordinary life is.</summary>
        private const int Above = 1;

        /// <summary>Exactly as the ordinary life has it, whatever the station's distance.</summary>
        private const int Level = 0;

        /// <summary>Less than an ordinary life has.</summary>
        private const int Below = -1;

        /// <summary>
        ///     What a life that was answered at random reads as, on every scale here.
        ///     It is the midpoint because <see cref="Share"/> measures a life against
        ///     the ordinary one and maps "the same as ordinary" to the middle, so a
        ///     facet only leaves the middle by being answered for or against.
        /// </summary>
        private const double Ordinary = 0.5;

        /// <summary>
        ///     Neither a specialist nor a generalist. The midpoint of
        ///     <see cref="Specialization"/>, and what an empty portrait reads as,
        ///     because no answers is not the same as a scattered life.
        /// </summary>
        private const double Undecided = 0.5;

        /// <summary>
        ///     The width of a depth band, read from <see cref="Depth"/> itself so
        ///     adding a band re-divides the scale. The bands are even because
        ///     nothing in the model privileges one of them.
        /// </summary>
        private static readonly double DepthStep = 1.0 / Enum.GetValues(typeof(Depth)).Length;

        /// <summary>
        ///     What one consequence says about the person it happened to.
        ///
        ///     Only the kind is read. A consequence's amount is in the units of its
        ///     own kind, so a debt is denars, a lost year is a year and a trait is a
        ///     step on a scale of its own; adding those together would be adding
        ///     denars to years. The one place an amount is read is
        ///     <see cref="ConsequenceKind.LostYears"/>, whose unit is the age's own,
        ///     and the sign of a trait, which says whether the life pushed back.
        ///
        ///     A kind speaks only to what it is evidence of. Nothing here may take a
        ///     little of every facet: a facet every consequence touches says the same
        ///     thing about every life and therefore says nothing, which is what
        ///     <see cref="Facet.Seasoning"/> did when it counted one for every
        ///     consequence of every kind and made every character fifty years old.
        ///
        ///     Exactly one row of each kind carries <see cref="Again"/>, and that is
        ///     what keeps repetition honest: counting any repeated target as depth
        ///     made six honorable answers read as a mastered trade, because a trait's
        ///     target is a trait name. A row marked so is never a quieter row than
        ///     the loudest of its kind: a return is the same statement made again,
        ///     so it cannot say something the kind barely says in the first place.
        /// </summary>
        private static readonly Dictionary<ConsequenceKind, (Facet Facet, int Weight, bool Again)[]> Weights =
            new Dictionary<ConsequenceKind, (Facet, int, bool)[]>
            {
                // A style is either inherited or granted, and both say someone above
                // you had already decided who you were. It is also the one kind that
                // has to have been carried a while: a name only sticks with use.
                // Styles that keep coming back are one house saying it again
                {
                    ConsequenceKind.Title, new[]
                    {
                        (Facet.Origins, Decisive, Again), (Facet.Intent, Slight, Once),
                        (Facet.Seasoning, Slight, Once)
                    }
                },

                // A place ties a life at both ends, and the two ends are equally
                // loud, so which of them a return deepens is a decision. Returning
                // is what separates them: a life that keeps naming the same place is
                // more firmly aimed at it, not better born, since nobody is born
                // somewhere repeatedly
                {
                    ConsequenceKind.Place, new[]
                    {
                        (Facet.Origins, Telling, Once), (Facet.Intent, Telling, Again)
                    }
                },

                // People disposed to help you are standing, and somebody with
                // standing took an interest in you early enough to teach you
                {
                    ConsequenceKind.Goodwill, new[]
                    {
                        (Facet.Origins, Telling, Again), (Facet.Schooling, Slight, Once)
                    }
                },

                {
                    ConsequenceKind.Ally, new[]
                    {
                        (Facet.Inclination, Slight, Once), (Facet.Intent, Telling, Again)
                    }
                },

                // Something in your hands when you ride out is usually the tool of
                // whatever you were taught to do with it
                {
                    ConsequenceKind.Item, new[]
                    {
                        (Facet.Mastery, Telling, Again), (Facet.Origins, Slight, Once),
                        (Facet.Schooling, Slight, Once)
                    }
                },

                // One movement in a trait is evidence of a bent and not the whole of
                // one. What makes a person one thing all the way through is doing it
                // again, which is what the mark on that row counts
                {
                    ConsequenceKind.Trait, new[]
                    {
                        (Facet.Inclination, Telling, Again)
                    }
                },

                // Shutting a road elsewhere is the only evidence of commitment a
                // consequence list carries, and commitment is what teaches and what
                // aims. It is not depth: committing to a road is not the same as
                // having walked it, which is what Mastery counts. Shutting the same
                // road twice is that discipline kept to, and keeping to a discipline
                // is what teaches, so the return deepens the teaching rather than
                // the aim
                {
                    ConsequenceKind.Gate, new[]
                    {
                        (Facet.Schooling, Telling, Again), (Facet.Intent, Telling, Once),
                        (Facet.Hinge, Slight, Once)
                    }
                },

                // People with something behind them are not usually the ones
                // borrowing, which is the whole of what a debt says about a name
                {
                    ConsequenceKind.Debt, new[]
                    {
                        (Facet.Hinge, Telling, Again), (Facet.Intent, Slight, Once),
                        (Facet.Origins, -Slight, Once)
                    }
                },

                // Origins are deliberately untouched: who hates you now says nothing
                // about what you were born into. An enemy who has not forgotten you,
                // though, is an enemy of some standing by now, and one the life keeps
                // colliding with is the break it keeps breaking on rather than a
                // second thing to ride toward
                {
                    ConsequenceKind.Enmity, new[]
                    {
                        (Facet.Hinge, Telling, Again), (Facet.Intent, Telling, Once),
                        (Facet.Seasoning, Slight, Once)
                    }
                },

                // Years that went nowhere are still years lived: they season a person
                // and they teach nothing
                {
                    ConsequenceKind.LostYears, new[]
                    {
                        (Facet.Seasoning, Decisive, Again), (Facet.Hinge, Telling, Once),
                        (Facet.Schooling, -Telling, Once), (Facet.Mastery, -Slight, Once)
                    }
                }
            };

        /// <summary>
        ///     What one scene of an ordinary life lands on each facet, and how far a
        ///     scene ordinarily swings it, where an ordinary life is one answered at
        ///     random: every option of every scene weighed equally and the whole read
        ///     exactly the way a played life is read, repeats included.
        ///
        ///     This is what every facet is measured against, and it is what makes the
        ///     seven comparable. Measured against a fixed ceiling they are not: a
        ///     facet fed by the commonest kind of consequence would top every life
        ///     and a facet fed by the rarest could never top any, so the station
        ///     contest would be decided by how many of each kind an author happened
        ///     to write rather than by what the player answered. Against the ordinary
        ///     life, a facet is loud only when this player reached for it harder than
        ///     a random player would have, and the swing says how much harder is
        ///     hard: a facet the scenes barely argue about needs a smaller difference
        ///     to be remarkable than one every scene fights over.
        ///
        ///     Held per scene set rather than once. Two routes are written, they are
        ///     different lengths and their answers are made of different consequences,
        ///     so one reference measured from either would read every life of the other
        ///     against a life nobody on that route can live.
        /// </summary>
        private sealed class Reference
        {
            internal Reference(IReadOnlyList<Scene> scenes)
            {
                Scene = MeasureOrdinaryScene(scenes);
                Length = scenes.Count;
            }

            /// <summary>What one scene of an ordinary life of this set lands and swings.</summary>
            internal Dictionary<Facet, Baseline> Scene { get; }

            /// <summary>How many scenes an ordinary life of this set is.</summary>
            internal int Length { get; }
        }

        /// <summary>
        ///     One reference per set, kept because measuring one walks every option of
        ///     every scene and the panel asks for a reading on every keystroke. Keyed
        ///     by the list itself, which is a static built once per route, so two asks
        ///     for one route are one measurement and the two routes never share one.
        /// </summary>
        private static readonly Dictionary<object, Reference> References = new();

        /// <summary>
        ///     Guarded because the measuring harnesses walk several routes at once.
        ///     A creation run never does, but a dictionary written from two threads
        ///     corrupts rather than races, and the cost here is one uncontended lock
        ///     against a measurement over every option of every scene.
        /// </summary>
        private static Reference Measured(IReadOnlyList<Scene>? scenes)
        {
            var set = scenes ?? SceneCatalog.All;

            lock (References)
            {
                if (References.TryGetValue(set, out var held)) return held;

                var measured = new Reference(set);
                References[set] = measured;
                return measured;
            }
        }

        /// <summary>
        ///     The facets that ask how MUCH living the answers describe rather than
        ///     what KIND of person they describe, which is the only thing that
        ///     decides which ordinary life a facet is measured against in
        ///     <see cref="Normalize"/>.
        ///
        ///     One facet asks that question and it is the one whose own summary says
        ///     so. A quality is as true of a life six scenes in as of a finished one;
        ///     a quantity is not, and measuring one against the other is the
        ///     difference between a character who is young because they have lived
        ///     three scenes and a character who is nobody because they have.
        /// </summary>
        private static readonly HashSet<Facet> AskHowMuch = new() { Facet.Seasoning };

        private readonly Dictionary<Facet, double> _scores = new();
        private readonly Dictionary<Facet, double> _shares = new();

        private Portrait()
        {
        }

        /// <summary>
        ///     The portrait of one set of answers. <paramref name="scenesAnswered"/>
        ///     is how many scenes the player has actually been through, which is
        ///     what the two references in <see cref="Normalize"/> are measured from:
        ///     what KIND of person the answers describe is measured against an
        ///     ordinary life of the length told so far, and how MUCH living they
        ///     describe against a whole one, so a life three scenes in is already a
        ///     recognizable person and is still only three scenes old.
        /// </summary>
        public static Portrait From(IReadOnlyList<ChoiceConsequence>? consequences, int scenesAnswered,
            IReadOnlyList<Scene>? measuredAgainst = null)
        {
            var reference = Measured(measuredAgainst);
            var portrait = new Portrait();
            int lostYears = 0;
            var counts = new Dictionary<Occurrence, double>();

            if (consequences != null)
                foreach (var consequence in consequences)
                {
                    if (consequence == null) continue;

                    var key = new Occurrence(consequence.Kind, consequence.Target, Math.Sign(consequence.Amount));
                    counts.TryGetValue(key, out double seen);
                    counts[key] = seen + 1;

                    if (consequence.Kind == ConsequenceKind.LostYears)
                        lostYears += Math.Max(0, consequence.Amount);
                }

            foreach (var pair in Tally(counts))
                portrait._scores[pair.Key] = pair.Value;

            int whole = Math.Max(reference.Length, Math.Max(0, scenesAnswered));
            int soFar = Math.Max(1, Math.Min(whole, Math.Max(0, scenesAnswered)));

            portrait.Normalize(reference, soFar, whole);
            portrait.Standings = portrait.Rank();
            portrait.Station = portrait.Standings[0].Station;
            portrait.Age = InferAge(portrait.Share(Facet.Seasoning), lostYears);
            portrait.Specialization = portrait.InferSpecialization();

            return portrait;
        }

        /// <summary>
        ///     The station the life earned, which is the nearest of
        ///     <see cref="Standings"/>. See <see cref="Shape"/> for what each one is
        ///     taken to look like.
        /// </summary>
        public StartType Station { get; private set; }

        /// <summary>
        ///     Every station, nearest first, so a caller that cannot use the one
        ///     the life earned can take the next one the life is nearest instead of
        ///     falling back on a constant. Never empty, and its first entry is
        ///     <see cref="Station"/>.
        ///
        ///     The shapes stay in here. What leaves is an order and one number per
        ///     station, and <see cref="Standing.Nearness"/> is a single average over
        ///     the facets that one station names: which facets those are, how many
        ///     there are and what each one wants are all unrecoverable from it. A
        ///     caller cannot rebuild <see cref="Shape"/> out of this, and a second
        ///     copy of that table would be a second account of what a station is.
        /// </summary>
        public IReadOnlyList<Standing> Standings { get; private set; } = Array.Empty<Standing>();

        /// <summary>The inferred age, snapped to the nearest band the game offers.</summary>
        public StartingAge Age { get; private set; }

        /// <summary>
        ///     Zero for a jack of all trades, one for a specialist, and
        ///     <see cref="Undecided"/> for neither. This is what the years really
        ///     decide: age does not make a character stronger here, it makes them
        ///     more of whatever they were already becoming.
        /// </summary>
        public double Specialization { get; private set; }

        /// <summary>What the answers said about one facet.</summary>
        public Measure Read(Facet facet) => new((int)Math.Round(RawScore(facet)), Share(facet));

        public Measure Origins => Read(Facet.Origins);
        public Measure Inclination => Read(Facet.Inclination);
        public Measure Schooling => Read(Facet.Schooling);
        public Measure Mastery => Read(Facet.Mastery);
        public Measure Hinge => Read(Facet.Hinge);
        public Measure Intent => Read(Facet.Intent);
        public Measure Seasoning => Read(Facet.Seasoning);

        /// <summary>One facet's reading: what it counted, and what that is worth as a share of the life.</summary>
        public readonly struct Measure
        {
            internal Measure(int score, double share)
            {
                Score = score;
                Share = share;
            }

            /// <summary>The raw weight the answers landed here. Signed: a facet can be argued against.</summary>
            public int Score { get; }

            /// <summary>The same weight against what an ordinary life lands there, zero to one.</summary>
            public double Share { get; }

            /// <summary>Which of the three bands the share falls in.</summary>
            public Depth Reach =>
                Share >= DepthStep * 2 ? Depth.Strong
                : Share >= DepthStep ? Depth.Present
                : Depth.Faint;
        }

        /// <summary>One station this life resembles, and how closely it resembles it.</summary>
        public readonly struct Standing
        {
            internal Standing(StartType station, double nearness)
            {
                Station = station;
                Nearness = nearness;
            }

            public StartType Station { get; }

            /// <summary>
            ///     How closely the life resembles this station, zero to one, on the
            ///     same scale for every station, so two of them can be compared and
            ///     a caller can say how close the call was. Zero for every station
            ///     when the answers have said nothing, because nothing resembles
            ///     nothing in particular.
            /// </summary>
            public double Nearness { get; }
        }

        /// <summary>
        ///     One thing that happened, as often as it happened. The sign is part of
        ///     the identity because a trait moved one way twice is a bent and a trait
        ///     moved both ways is a life that could not make its mind up.
        /// </summary>
        private readonly struct Occurrence : IEquatable<Occurrence>
        {
            internal Occurrence(ConsequenceKind kind, string? target, int direction)
            {
                Kind = kind;
                Target = target;
                Direction = direction;
            }

            internal ConsequenceKind Kind { get; }
            internal string? Target { get; }
            internal int Direction { get; }

            public bool Equals(Occurrence other) =>
                Kind == other.Kind && Direction == other.Direction &&
                string.Equals(Target, other.Target, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is Occurrence other && Equals(other);

            public override int GetHashCode()
            {
                int hash = (int)Kind * 397;
                hash = (hash + Direction) * 397;
                return hash + (Target == null ? 0 : Target.GetHashCode());
            }
        }

        /// <summary>
        ///     What a set of occurrences lands on each facet: every occurrence says
        ///     its kind's piece once, and returning to the same named thing says the
        ///     row that kind marked <see cref="Again"/> once more for every return.
        ///
        ///     Counts are fractional so that the ordinary life, which is every option
        ///     of every scene weighed against the number of options it was offered
        ///     beside, goes through exactly this and not through a second reading of
        ///     it that could disagree.
        /// </summary>
        private static Dictionary<Facet, double> Tally(Dictionary<Occurrence, double> counts)
        {
            var totals = new Dictionary<Facet, double>();

            foreach (var pair in counts)
            {
                if (!Weights.TryGetValue(pair.Key.Kind, out var rows)) continue;

                // A life that keeps naming the same thing went deep into it, and that
                // is the only evidence of depth a flat list can carry. Something the
                // answers never named is not a thing to return to
                bool returned = pair.Value > 1 && !string.IsNullOrEmpty(pair.Key.Target);

                foreach (var row in rows)
                {
                    // Once for each time it happened, and once more for each return
                    double said = returned && row.Again
                        ? pair.Value + (pair.Value - 1)
                        : pair.Value;

                    totals.TryGetValue(row.Facet, out double running);
                    totals[row.Facet] = running + row.Weight * said;
                }

                // Something the life moved AGAINST is a life that pushed back on the
                // person living it, which is the shape of a hinge and not of a bent.
                // It is the one thing a direction says on its own, whatever the kind
                if (pair.Key.Direction >= 0) continue;

                totals.TryGetValue(Facet.Hinge, out double pushed);
                totals[Facet.Hinge] = pushed + Slight * pair.Value;
            }

            return totals;
        }

        /// <summary>What one scene of an ordinary life lands on a facet, and how far one scene swings it.</summary>
        private readonly struct Baseline
        {
            internal Baseline(double lands, double swing)
            {
                Lands = lands;
                Swing = swing;
            }

            /// <summary>What a scene answered at random adds here.</summary>
            internal double Lands { get; }

            /// <summary>How much one scene's answers differ from each other here, squared.</summary>
            internal double Swing { get; }
        }

        /// <summary>
        ///     The life a player would have if they answered without reading: every
        ///     option of a scene is as likely as its neighbors, so a scene contributes
        ///     each of its options divided by how many it offered. Per scene, so a
        ///     life is measured against an ordinary life of its own length.
        ///
        ///     What one scene LANDS is read through <see cref="Tally"/>, the same
        ///     reading a played life gets, so the reference cannot disagree with the
        ///     thing it is the reference for. What one scene SWINGS is read from the
        ///     options alone, because a return to something named earlier belongs to
        ///     a whole life and not to the scene that offered it.
        /// </summary>
        private static Dictionary<Facet, Baseline> MeasureOrdinaryScene(IReadOnlyList<Scene> scenes)
        {
            var counts = new Dictionary<Occurrence, double>();
            var swing = new Dictionary<Facet, double>();
            int measured = 0;

            foreach (var scene in scenes)
            {
                if (scene.Options.Count <= 0) continue;

                measured++;
                double each = 1.0 / scene.Options.Count;
                var offered = new List<Dictionary<Facet, double>>();

                foreach (var option in scene.Options)
                {
                    var alone = new Dictionary<Occurrence, double>();

                    foreach (var consequence in option.Consequences)
                    {
                        if (consequence == null) continue;

                        var key = new Occurrence(consequence.Kind, consequence.Target, Math.Sign(consequence.Amount));
                        counts.TryGetValue(key, out double seen);
                        counts[key] = seen + each;

                        alone.TryGetValue(key, out double here);
                        alone[key] = here + 1;
                    }

                    offered.Add(Tally(alone));
                }

                foreach (Facet facet in Enum.GetValues(typeof(Facet)))
                {
                    double middle = 0;
                    foreach (var option in offered) middle += Landed(option, facet) * each;

                    double spread = 0;
                    foreach (var option in offered)
                    {
                        double away = Landed(option, facet) - middle;
                        spread += away * away * each;
                    }

                    swing.TryGetValue(facet, out double running);
                    swing[facet] = running + spread;
                }
            }

            var ordinary = new Dictionary<Facet, Baseline>();
            if (measured <= 0) return ordinary;

            var lands = Tally(counts);
            foreach (Facet facet in Enum.GetValues(typeof(Facet)))
            {
                swing.TryGetValue(facet, out double total);
                ordinary[facet] = new Baseline(Landed(lands, facet) / measured, total / measured);
            }

            return ordinary;
        }

        private static double Landed(Dictionary<Facet, double> totals, Facet facet) =>
            totals.TryGetValue(facet, out double landed) ? landed : 0;

        private double RawScore(Facet facet) => _scores.TryGetValue(facet, out double score) ? score : 0;

        private double Share(Facet facet) => _shares.TryGetValue(facet, out double share) ? share : 0;

        /// <summary>Whether the answers said anything at all about anything.</summary>
        private bool Spoke { get; set; }

        /// <summary>
        ///     The one reading of the scores, which every sentence, the station and
        ///     the age are all taken from.
        ///
        ///     A SHARE is how much of the life a facet is, measured against what an
        ///     ordinary life lands there and scaled by how far the scenes can swing
        ///     it. A life exactly like the ordinary one reads <see cref="Ordinary"/>
        ///     on every facet, one that reached a swing further reads three quarters
        ///     of the way up, and no life reaches either end, because there is
        ///     always an answer that would have said more.
        ///
        ///     Which ordinary life a facet is measured against is the facet's own
        ///     question, not the caller's: <see cref="AskHowMuch"/> is measured
        ///     against a whole one and everything else against one of the length
        ///     told so far. This is one reading with two references and not two
        ///     readings. A facet asking what KIND of person this is has its answer
        ///     the moment there are answers, so a life six scenes in that has said
        ///     one thing loudly has said it loudly; measured against a whole
        ///     ordinary life instead, every facet of a part-told life landed barely
        ///     there, <see cref="Shape"/> for <see cref="StartType.Commoner"/> is
        ///     three absences, and every life read as a commoner until scene seven
        ///     however it was answered. A facet asking HOW MUCH living there has
        ///     been is answered by the length itself, so measuring it against the
        ///     whole is what ages a character forward through the telling rather
        ///     than handing them their final age after the first answer.
        ///
        ///     There was a second reading here, an emphasis, which stretched each
        ///     facet across the distance between the loudest and the quietest facet
        ///     of the same life and so threw away how much had been said at all. The
        ///     station was decided on that while every sentence was banded on the
        ///     share, and the screen prints both, so a quarter of all lives were told
        ///     they had been born to a name that opened doors and then, three lines
        ///     later, that they began as nobody in particular. Two readings of one
        ///     life are two lives.
        /// </summary>
        private void Normalize(Reference reference, int soFar, int whole)
        {
            foreach (Facet facet in Enum.GetValues(typeof(Facet)))
            {
                double raw = RawScore(facet);
                _shares[facet] = ShareOf(reference, facet, raw, AskHowMuch.Contains(facet) ? whole : soFar);

                if (Math.Abs(raw) > 0) Spoke = true;
            }
        }

        /// <summary>
        ///     What one facet's raw weight reads as against an ordinary life of
        ///     that many scenes. Held apart from <see cref="Normalize"/> because
        ///     the reachability walk asks the same question of a score no life has
        ///     reached yet, and two readings of one score would be two lives.
        /// </summary>
        private static double ShareOf(Reference reference, Facet facet, double raw, int length)
        {
            reference.Scene.TryGetValue(facet, out Baseline baseline);
            double ordinary = baseline.Lands * length;
            double swing = Math.Sqrt(baseline.Swing * length);

            return Reads(swing <= 0 ? 0 : (raw - ordinary) / swing);
        }

        /// <summary>
        ///     What a life that stood <paramref name="swings"/> scene-swings from the
        ///     ordinary life reads as. This is the one place a distance becomes a
        ///     share: <see cref="Normalize"/> reads a life through it and
        ///     <see cref="Shape"/> states what a station wants through it, so a
        ///     station's demand and a life's reading are the same measurement and
        ///     cannot drift apart.
        /// </summary>
        private static double Reads(double swings) =>
            Ordinary * (1 + swings / (1 + Math.Abs(swings)));

        /// <summary>
        ///     What each station looks like: how far from the ordinary life it
        ///     stands, and which side of it each facet it cares about stands on.
        ///     <see cref="Level"/> is a real answer here and not a missing one: a
        ///     station can require that a facet is exactly ordinary as firmly as it
        ///     requires that another one is not.
        ///
        ///     A station is never chosen. Every one of them, Commoner included, is
        ///     scored against what the answers said and the closest wins, so there
        ///     is no threshold and no default.
        ///
        ///     Commoner earns its place like the rest, and must never go back to
        ///     being a fallback under a threshold: one number cannot answer both "is
        ///     this life none of the shaped stations" and "did this life say very
        ///     much", and a life that says little but says all of it in one
        ///     direction fails the second having passed the first. A commoner is not
        ///     someone who said little, however few answers there are.
        ///
        ///     The distance is per station and not per row because a station stands
        ///     a distance from the ordinary life and its facets do not stand
        ///     different distances from each other. It sets how OFTEN each station is
        ///     read, which the sides alone cannot: eight stations drawn at one
        ///     distance cannot both keep Commoner the commonest reading of a life
        ///     answered at random and Monarch the rarest. What it may never do is
        ///     tell two stations APART, which is the rows' work and is held to below.
        ///     The distances here put a randomly answered life on Commoner a little
        ///     over half the time and on a crown one time in twenty-two to
        ///     twenty-five, measured over twenty thousand such lives in both
        ///     catalogs; changing a row or a distance moves every other station too,
        ///     so the census is re-measured whenever one of them moves.
        ///
        ///     A demand of one swing is barely a demand and a demand of ten is one
        ///     no life can meet, so the distances sit where a life that reached for
        ///     the station plainly meets them and a life that did not plainly does
        ///     not.
        ///
        ///     Every pair of stations contradicts the other somewhere: one asks a
        ///     facet to stand <see cref="Above"/> the ordinary life where the other
        ///     asks for <see cref="Below"/>, and <see cref="StartType.Commoner"/>
        ///     names all seven at <see cref="Level"/>, which denies whichever side
        ///     any other station takes. Silence is not separation, and a distance is
        ///     not a reason. Monarch asked for a name, teaching, an aim and years;
        ///     the caravan master asked for a trade, teaching and an aim, and neither
        ///     denied one thing the other wanted, so a life that was well born,
        ///     taught, driven, old AND masterful met both squarely and went to
        ///     whichever of them happened to stand further out. That is arithmetic
        ///     answering a question the player answered. Two stations no answer can
        ///     tell apart are one life scored twice, so a station that shares every
        ///     demand it makes with another one is not a second station.
        /// </summary>
        private static (int Swings, (Facet Facet, int Way)[] Rows) Shape(StartType station)
        {
            switch (station)
            {
                // Nobody in particular, which is what this station says of itself:
                // a birth like anyone's, a trade like anyone's, an aim like
                // anyone's, and nothing that broke. It names all seven facets
                // because that is the claim: not that this life lacks something,
                // but that there is nothing about it that stands out. Its distance
                // is none, so every row wants exactly what an ordinary life reads,
                // whichever way the row is written
                case StartType.Commoner:
                    return (0, new[]
                    {
                        (Facet.Origins, Level), (Facet.Inclination, Level),
                        (Facet.Schooling, Level), (Facet.Mastery, Level),
                        (Facet.Hinge, Level), (Facet.Intent, Level),
                        (Facet.Seasoning, Level)
                    });

                // This station is a kingdom FOUNDED rather than a crown worn, so
                // the road behind it is part of the shape: born to something,
                // taught, aimed at one thing hard enough to build it, old because
                // the founding took the years rather than wasted them, and unbroken,
                // because years spent building a thing are years not spent falling
                // out of one.
                //
                // The break is what tells a crown made from a banner raised against
                // one, and it is said here because it is the rebel's whole identity:
                // without it the two asked for a birth, a teaching and an aim alike
                // and denied each other nothing, so a life reaching for either went
                // to whichever of them stood further from the ordinary life
                case StartType.Monarch:
                    return (3, new[]
                    {
                        (Facet.Origins, Above), (Facet.Intent, Above),
                        (Facet.Schooling, Above), (Facet.Seasoning, Above),
                        (Facet.Hinge, Below)
                    });

                // Born or raised into something and content to hold it under someone
                // else's crown: the birth and the teaching of a monarch without the
                // reaching, without the years a founding takes, and without the
                // break that would make it a rebellion
                case StartType.LandedVassal:
                    return (5, new[]
                    {
                        (Facet.Origins, Above), (Facet.Schooling, Above),
                        (Facet.Hinge, Below), (Facet.Intent, Below),
                        (Facet.Seasoning, Below)
                    });

                // Trained into service and wholly pointed at it, with no name behind
                // you to make land come with the oath and no trade of your own: what
                // a sworn man has is what he was taught rather than the one thing he
                // kept going back to, which is what tells him from the caravan
                // master, who was taught and aimed exactly as hard. Years are not
                // named: an oath is sworn young as readily as late
                case StartType.LandlessVassal:
                    return (2, new[]
                    {
                        (Facet.Schooling, Above), (Facet.Intent, Above),
                        (Facet.Origins, Below), (Facet.Mastery, Below)
                    });

                // A break, and something of your own to break away with. The origins
                // and the teaching are what separate this from an outlaw doing the
                // same thing: a house in revolt was a house first.
                //
                // The years are what separate it from a crown. A kingdom founded is
                // the work of a lifetime; a banner goes up the season the house can
                // no longer hold, and whoever raises it raises it then rather than
                // when they are ready. Without that the rebel came out commoner than
                // the landed vassal however far it was made to stand, because four
                // facets all wanted above ordinary and nothing denied
                case StartType.RebelClan:
                    return (6, new[]
                    {
                        (Facet.Hinge, Above), (Facet.Intent, Above),
                        (Facet.Origins, Above), (Facet.Schooling, Above),
                        (Facet.Seasoning, Below)
                    });

                // A trade learned, kept at, and wholly aimed at, by somebody with no
                // name behind them. The trade is what they have INSTEAD of a birth,
                // and saying so is what stops a life that was well born, taught,
                // driven and old from reading as a founder of kingdoms and a carrier
                // of goods at the same time. The mastery of one trade is what tells
                // it from the landless vassal
                case StartType.CaravanMaster:
                    return (4, new[]
                    {
                        (Facet.Mastery, Above), (Facet.Schooling, Above),
                        (Facet.Intent, Above), (Facet.Origins, Below)
                    });

                // A skill worth money and the years of selling it, no name behind
                // either, and a break with settled life. What separates this from
                // the caravan master is what taught the skill: the years and the
                // break, rather than anybody's instruction, and the teaching has to
                // be named or a life aimed at this one reads as sworn service
                // instead, which is what it did when only the mastery and the years
                // were asked for. Five modest marks rather than one extreme one,
                // which is why it stands as near the ordinary life as any station
                // here does
                case StartType.Mercenary:
                    return (2, new[]
                    {
                        (Facet.Mastery, Above), (Facet.Seasoning, Above),
                        (Facet.Origins, Below), (Facet.Schooling, Below),
                        (Facet.Hinge, Above)
                    });

                // Fell out of everything, and the fall is nearly the whole of it:
                // no name, nobody's teaching, and no trade kept at long enough to
                // be good at
                case StartType.Outlaw:
                    return (4, new[]
                    {
                        (Facet.Hinge, Above), (Facet.Origins, Below),
                        (Facet.Schooling, Below), (Facet.Mastery, Below)
                    });

                default:
                    return (0, Array.Empty<(Facet, int)>());
            }
        }

        /// <summary>
        ///     How closely this life resembles a station, from zero to one. Distance
        ///     rather than a weighted sum, because a sum lets a station win on the
        ///     facets it happens to share while ignoring the one it is missing: a
        ///     life with no intent in it at all could still come out a monarch on
        ///     origins and years alone. Here a miss on any facet a station names
        ///     costs it, whether the miss is too little or too much.
        ///
        ///     Read off the same share the sentences are banded on, which is what
        ///     keeps the two from contradicting each other, and what a station wants
        ///     is put on that scale by <see cref="Reads"/>, the very function the
        ///     share came out of. A station asking for a facet a long way above the
        ///     ordinary life is asking for the sentence that says the facet is the
        ///     whole of the person, because that is what a life standing there
        ///     reads as.
        ///
        ///     A demand nothing can meet is worse than no demand at all, which is
        ///     what a want of nought or one used to be: no life reads as either, so
        ///     a station asking for one was penalised on that facet however the
        ///     player answered, and the contest fell to whichever station happened
        ///     to want what an unremarkable life already had. Everything
        ///     <see cref="Reads"/> returns is reachable, because it is what some
        ///     life reads as.
        /// </summary>
        private double Match(StartType station)
        {
            var shape = Shape(station);
            double distance = 0;
            int named = 0;

            foreach (var row in shape.Rows)
            {
                distance += Math.Abs(Share(row.Facet) - Reads(row.Way * shape.Swings));
                named++;
            }

            return named <= 0 ? 0 : 1 - distance / named;
        }

        /// <summary>
        ///     Every station, nearest first.
        ///
        ///     Ranked in one pass rather than won and then rematched, because the
        ///     station a caller falls back on when it cannot use the first has to be
        ///     chosen by the same measure the first was, or the second answer is a
        ///     different opinion of the same life.
        ///
        ///     Answers that said nothing are not a shape to be matched: every facet
        ///     would read as ordinary and the winner would be an accident of which
        ///     station happens to want that. A life like that resembles nothing, so
        ///     every station reads nought and the tie-break below decides, which
        ///     puts <see cref="StartType.Commoner"/> first because it is the station
        ///     standing no distance from an ordinary life.
        ///
        ///     A tie between two stations a life actually resembles goes to the one
        ///     standing FURTHER from the ordinary life. Declaration order used to
        ///     decide it, and <see cref="StartType"/> declares Commoner first, so the
        ///     commonest reading silently collected every tie it had not earned: the
        ///     one case the player cannot be shown a reason for is the one case the
        ///     fallback reading won. A life that resembles two stations equally has
        ///     earned the more particular of them.
        /// </summary>
        private List<Standing> Rank()
        {
            var ranked = new List<Standing>();

            foreach (StartType station in Enum.GetValues(typeof(StartType)))
            {
                double nearness = Spoke ? Match(station) : 0;
                int distance = Shape(station).Swings;

                int place = ranked.Count;
                while (place > 0 && Beats(nearness, distance, ranked[place - 1])) place--;

                ranked.Insert(place, new Standing(station, nearness));
            }

            return ranked;
        }

        /// <summary>
        ///     Whether a station displaces one already placed: nearer, or equally
        ///     near and standing further from the ordinary life. Two stations equally
        ///     near AND equally far apart keep the order they were declared in, which
        ///     is the only case nothing in the model can separate.
        ///
        ///     A life that has said nothing is every station's equal at nought, and
        ///     that is not a tie between stations it resembles: it resembles none of
        ///     them. Nothing has been earned there, so nothing is awarded, and the
        ///     list stays in declaration order.
        /// </summary>
        private bool Beats(double nearness, int distance, Standing placed) =>
            placed.Nearness < nearness ||
            (Spoke && placed.Nearness <= nearness &&
             Shape(placed.Station).Swings < distance);

        /// <summary>
        ///     The age the answers imply. The ladder is read from
        ///     <see cref="StartingAge"/> itself, whose members are the years, so
        ///     adding or moving a band moves this with it and no year is written
        ///     here. Seasoning walks the span, lost years are added on top because
        ///     their unit is already years, and the result snaps to the nearest band.
        /// </summary>
        private static StartingAge InferAge(double seasoning, int lostYears)
        {
            var ladder = (StartingAge[])Enum.GetValues(typeof(StartingAge));
            int youngest = (int)ladder[0];
            int oldest = (int)ladder[ladder.Length - 1];
            double years = youngest + seasoning * (oldest - youngest) + lostYears;

            var nearest = ladder[0];
            double closest = double.MaxValue;
            foreach (var band in ladder)
            {
                double distance = Math.Abs(years - (int)band);
                if (distance >= closest) continue;

                closest = distance;
                nearest = band;
            }

            return nearest;
        }

        /// <summary>
        ///     Whether the answers describe one thing done deeply or several done
        ///     passably.
        ///
        ///     Read as depth against breadth, which is what the facets already are:
        ///     <see cref="Facet.Mastery"/> is the thing the life kept returning to,
        ///     and the number of facets the life reached further into than an
        ///     ordinary life does is how widely it spread. Years then exaggerate
        ///     whichever it already was, which is the whole claim about age: time
        ///     does not broaden a focused life or focus a broad one, it makes each
        ///     more so.
        /// </summary>
        private double InferSpecialization()
        {
            int others = 0;
            int lit = 0;

            foreach (Facet facet in Enum.GetValues(typeof(Facet)))
            {
                if (facet == Facet.Mastery) continue;

                others++;
                if (Share(facet) >= Ordinary) lit++;
            }

            // A life that never brought any facet past ordinary is not a narrow life
            // or a wide one, it is a life the answers have not described yet
            if (others <= 0 || (lit == 0 && Share(Facet.Mastery) < Ordinary)) return Undecided;

            double depth = Share(Facet.Mastery);
            double breadth = lit / (double)others;
            double reading = (depth + (1 - breadth)) / 2;

            double sharpened = Undecided + (reading - Undecided) * (1 + Share(Facet.Seasoning));
            return Math.Max(0.0, Math.Min(1.0, sharpened));
        }
    }
}
