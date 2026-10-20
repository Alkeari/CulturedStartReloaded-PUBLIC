using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Which of the eight stations a part-told life can still finish as.
    ///
    ///     Nothing declares a station closed. <see cref="Station"/> is whichever
    ///     of the eight the finished life stands nearest, so "this answer closed
    ///     the crown" is an arithmetic fact about the answers still to come: no
    ///     combination of them brings that station back to first place. That is
    ///     what is worked out here, and it is worked out EXACTLY rather than
    ///     sampled, because the panel states it to the player as a fact.
    ///
    ///     Exactness comes from the shape of a score. A facet's raw weight splits
    ///     in two: a part that is a plain sum over the answers, which every scene
    ///     contributes to whatever the others were answered, and a part that
    ///     counts returns to a thing already named, which only ever grows as
    ///     answers are added because every row that counts a return weighs
    ///     positive. So the scenes still to come land each facet inside a known
    ///     interval; the interval gives a ceiling on how far ahead of any rival a
    ///     station could possibly finish; and a station whose ceiling is below
    ///     nought cannot finish first however the rest is answered. The walk
    ///     drives that ceiling down the tree of remaining answers, and at a leaf
    ///     the interval has closed to a point, so the ceiling is no longer a bound
    ///     on the answer but the answer: what the walk reports is what the
    ///     finished life reads as.
    ///
    ///     The bound is one-sided by construction. Where it is loose it is loose
    ///     towards OPEN: a station it cannot rule out is reported still reachable,
    ///     so the panel under-states what an answer closed rather than claiming a
    ///     closure that is not real. The node budget and a scene set this cannot
    ///     walk err the same way.
    /// </summary>
    public sealed partial class Portrait
    {
        /// <summary>
        ///     Nodes one station's walk may open before it gives up and reports
        ///     that station still reachable. A ceiling on the work rather than on
        ///     the answer: every station the walk settles is settled exactly, and
        ///     giving up errs towards saying nothing was closed.
        /// </summary>
        private const int WalkBudget = 300000;

        /// <summary>
        ///     How far below nought a ceiling has to sit before a station is called
        ///     closed. Everything here is floating point and a station that ties
        ///     its rival can still take the tie, so the slack is spent on the side
        ///     that keeps a station open.
        /// </summary>
        private const double Slack = 1e-9;

        private static readonly Facet[] FacetOrder = (Facet[])Enum.GetValues(typeof(Facet));

        private static readonly StartType[] StationOrder = (StartType[])Enum.GetValues(typeof(StartType));

        /// <summary>Every station, which is what a walk that settled nothing reports.</summary>
        private static readonly IReadOnlyList<StartType> EveryStation = Array.AsReadOnly(StationOrder);

        /// <summary>
        ///     What each station asks of each facet against what every other one
        ///     asks, laid out per pair so the walk reads a row of numbers rather
        ///     than a table of shapes.
        /// </summary>
        private static readonly Rivalry[][] Rivalries = MeasureRivalries();

        /// <summary>
        ///     The rows of <see cref="Weights"/> said again for a return, summed
        ///     per kind and facet. Every one of them weighs positive, which is what
        ///     makes a return something that can only ever add: a facet's repeated
        ///     part never falls as answers are given, so what the answers so far
        ///     have earned is a floor under what the finished life earns.
        /// </summary>
        private static readonly Dictionary<ConsequenceKind, double[]> Returns = MeasureReturns();

        /// <summary>The scene set most recently walked, measured once and held.</summary>
        private static Terrain? _terrain;

        /// <summary>
        ///     Every station a life that has answered <paramref name="answered"/>
        ///     can still finish as, in declaration order. Never empty: the station
        ///     the life is nearest right now is always among them.
        /// </summary>
        public static IReadOnlyList<StartType> StillOpen(
            IReadOnlyList<Scene>? scenes, IReadOnlyList<string>? answered)
        {
            if (scenes == null || scenes.Count <= 0) return EveryStation;

            var terrain = TerrainFor(scenes);
            string key = answered == null ? string.Empty : string.Join(">", answered);
            if (terrain.Remembered.TryGetValue(key, out var held)) return held;

            var walk = new Walk(terrain);
            StartType[] open;

            if (!walk.Follow(answered))
            {
                open = StationOrder;
            }
            else
            {
                var reached = new List<StartType>();
                foreach (var station in StationOrder)
                    if (walk.Reaches(station))
                        reached.Add(station);

                open = reached.Count > 0 ? reached.ToArray() : StationOrder;
            }

            if (terrain.Remembered.Count > 512) terrain.Remembered.Clear();
            terrain.Remembered[key] = open;

            return open;
        }

        /// <summary>
        ///     The stations landing on <paramref name="optionId"/> takes off the
        ///     table: reachable with the answers already given, and unreachable
        ///     once this one is among them.
        ///
        ///     The option's own scene is held out of the answers, the way
        ///     <see cref="Scene.LifeBefore"/> holds it out, because the stage
        ///     records an answer the moment the selection lands on it: read without
        ///     that, every option would be weighed against a life that already
        ///     contains it.
        /// </summary>
        public static IReadOnlyList<StartType> ClosedBy(
            IReadOnlyList<Scene>? scenes, IReadOnlyList<string>? answered, string? optionId)
        {
            var none = Array.Empty<StartType>();
            if (scenes == null || string.IsNullOrEmpty(optionId)) return none;

            Scene? owner = null;
            foreach (var scene in scenes)
                if (scene.Find(optionId!) != null)
                {
                    owner = scene;
                    break;
                }

            if (owner == null) return none;

            var before = new List<string>();
            if (answered != null)
                foreach (string chosen in answered)
                    if (owner.Find(chosen) == null)
                        before.Add(chosen);

            var wasOpen = StillOpen(scenes, before);
            before.Add(optionId!);
            var stillOpen = StillOpen(scenes, before);

            List<StartType>? closed = null;
            foreach (var station in wasOpen)
            {
                if (Holds(stillOpen, station)) continue;

                closed ??= new List<StartType>();
                closed.Add(station);
            }

            return (IReadOnlyList<StartType>?)closed ?? none;
        }

        private static bool Holds(IReadOnlyList<StartType> stations, StartType station)
        {
            foreach (var held in stations)
                if (held == station)
                    return true;

            return false;
        }

        /// <summary>
        ///     One station weighed against one other, facet by facet: what a miss
        ///     on that facet costs each of them and what each of them wants there.
        ///     A facet neither of them names is left out, since it can put nothing
        ///     between them.
        /// </summary>
        private sealed class Rivalry
        {
            internal Rivalry(int[] facets, double[] theirs, double[] theirWant,
                double[] ours, double[] ourWant)
            {
                Facets = facets;
                Theirs = theirs;
                TheirWant = theirWant;
                Ours = ours;
                OurWant = ourWant;
            }

            internal int[] Facets { get; }

            internal double[] Theirs { get; }

            internal double[] TheirWant { get; }

            internal double[] Ours { get; }

            internal double[] OurWant { get; }
        }

        private static Rivalry[][] MeasureRivalries()
        {
            int facets = FacetOrder.Length;
            var wants = new double[StationOrder.Length][];
            var names = new bool[StationOrder.Length][];
            var each = new double[StationOrder.Length];

            foreach (var station in StationOrder)
            {
                var shape = Shape(station);
                wants[(int)station] = new double[facets];
                names[(int)station] = new bool[facets];
                each[(int)station] = shape.Rows.Length <= 0 ? 0 : 1.0 / shape.Rows.Length;

                foreach (var row in shape.Rows)
                {
                    wants[(int)station][(int)row.Facet] = Reads(row.Way * shape.Swings);
                    names[(int)station][(int)row.Facet] = true;
                }
            }

            var rivalries = new Rivalry[StationOrder.Length][];

            foreach (var station in StationOrder)
            {
                var against = new List<Rivalry>();

                foreach (var rival in StationOrder)
                {
                    if (rival == station) continue;

                    var matters = new List<int>();
                    for (int facet = 0; facet < facets; facet++)
                        if (names[(int)station][facet] || names[(int)rival][facet])
                            matters.Add(facet);

                    var theirs = new double[matters.Count];
                    var theirWant = new double[matters.Count];
                    var ours = new double[matters.Count];
                    var ourWant = new double[matters.Count];

                    for (int row = 0; row < matters.Count; row++)
                    {
                        int facet = matters[row];
                        theirs[row] = names[(int)rival][facet] ? each[(int)rival] : 0;
                        theirWant[row] = wants[(int)rival][facet];
                        ours[row] = names[(int)station][facet] ? each[(int)station] : 0;
                        ourWant[row] = wants[(int)station][facet];
                    }

                    against.Add(new Rivalry(matters.ToArray(), theirs, theirWant, ours, ourWant));
                }

                rivalries[(int)station] = against.ToArray();
            }

            return rivalries;
        }

        private static Dictionary<ConsequenceKind, double[]> MeasureReturns()
        {
            var said = new Dictionary<ConsequenceKind, double[]>();

            foreach (var pair in Weights)
            {
                double[]? again = null;

                foreach (var row in pair.Value)
                {
                    if (!row.Again) continue;

                    again ??= new double[FacetOrder.Length];
                    again[(int)row.Facet] += row.Weight;
                }

                if (again != null) said[pair.Key] = again;
            }

            return said;
        }

        /// <summary>
        ///     One consequence as the walk carries it: which occurrence it counts
        ///     towards, what a return to that occurrence is worth, and the answer
        ///     it shuts. Worked out once for the scene set so a node costs the walk
        ///     no lookups by name.
        /// </summary>
        private readonly struct Mark
        {
            internal Mark(int occurrence, double[]? again, int shuts)
            {
                Occurrence = occurrence;
                Again = again;
                Shuts = shuts;
            }

            internal int Occurrence { get; }

            /// <summary>What a second one of these adds per facet, or null where nothing can return to it.</summary>
            internal double[]? Again { get; }

            /// <summary>The answer this shuts, or minus one for a consequence that shuts nothing.</summary>
            internal int Shuts { get; }
        }

        /// <summary>
        ///     One scene set, measured: what each answer lands, what the scenes
        ///     from any point on can land between them at the very least and the
        ///     very most, and what one facet's raw weight reads as.
        /// </summary>
        private sealed class Terrain
        {
            private readonly Dictionary<string, int> _numbered =
                new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>This set's own ordinary life, which every reading here is against.</summary>
            internal Reference Measured { get; }

            internal Terrain(IReadOnlyList<Scene> scenes)
            {
                Scenes = scenes;
                Measured = Portrait.Measured(scenes);
                Remembered = new Dictionary<string, StartType[]>(StringComparer.Ordinal);

                var reference = Measured;

                int facets = FacetOrder.Length;
                Whole = Math.Max(reference.Length, scenes.Count);
                SoFar = Math.Max(1, Math.Min(Whole, scenes.Count));

                // The two references Normalize reads a finished life against, held
                // as numbers rather than asked for per node: the walk converts a
                // raw weight to a share some tens of millions of times
                Ordinary = new double[facets];
                Swing = new double[facets];
                for (int facet = 0; facet < facets; facet++)
                {
                    int length = AskHowMuch.Contains(FacetOrder[facet]) ? Whole : SoFar;
                    reference.Scene.TryGetValue(FacetOrder[facet], out Baseline baseline);
                    Ordinary[facet] = baseline.Lands * length;
                    Swing[facet] = Math.Sqrt(baseline.Swing * length);
                }

                Starts = new int[scenes.Count + 1];
                for (int at = 0; at < scenes.Count; at++)
                    Starts[at + 1] = Starts[at] + scenes[at].Options.Count;

                int answers = Starts[scenes.Count];
                Lands = new double[answers][];
                Marks = new Mark[answers][];

                var occurrences = new Dictionary<Occurrence, int>();
                var returned = new double[answers][];

                for (int at = 0; at < scenes.Count; at++)
                    for (int option = 0; option < scenes[at].Options.Count; option++)
                    {
                        int answer = Starts[at] + option;
                        _numbered[scenes[at].Options[option].Id] = answer;
                        Lands[answer] = Alone(scenes[at].Options[option]);
                        returned[answer] = CouldReturn(scenes[at].Options[option]);
                    }

                for (int at = 0; at < scenes.Count; at++)
                    for (int option = 0; option < scenes[at].Options.Count; option++)
                        Marks[Starts[at] + option] = Marked(scenes[at].Options[option], occurrences);

                Occurrences = occurrences.Count;

                // A scene some lives never meet lands nothing on them, so nought
                // is a candidate in both directions unless every scene is put to
                // everybody, which is what the run this was written for does
                bool everyone = true;
                foreach (var scene in scenes)
                    if (!scene.Everyone)
                        everyone = false;

                Least = new double[scenes.Count + 1][];
                Most = new double[scenes.Count + 1][];
                Again = new double[scenes.Count + 1][];
                Least[scenes.Count] = new double[facets];
                Most[scenes.Count] = new double[facets];
                Again[scenes.Count] = new double[facets];

                for (int at = scenes.Count - 1; at >= 0; at--)
                {
                    var least = new double[facets];
                    var most = new double[facets];
                    var again = new double[facets];

                    for (int facet = 0; facet < facets; facet++)
                    {
                        least[facet] = everyone ? double.MaxValue : 0;
                        most[facet] = everyone ? double.MinValue : 0;
                    }

                    for (int option = 0; option < scenes[at].Options.Count; option++)
                    {
                        var lands = Lands[Starts[at] + option];
                        var may = returned[Starts[at] + option];

                        for (int facet = 0; facet < facets; facet++)
                        {
                            if (lands[facet] < least[facet]) least[facet] = lands[facet];
                            if (lands[facet] > most[facet]) most[facet] = lands[facet];
                            if (may[facet] > again[facet]) again[facet] = may[facet];
                        }
                    }

                    for (int facet = 0; facet < facets; facet++)
                    {
                        least[facet] += Least[at + 1][facet];
                        most[facet] += Most[at + 1][facet];
                        again[facet] += Again[at + 1][facet];
                    }

                    Least[at] = least;
                    Most[at] = most;
                    Again[at] = again;
                }
            }

            internal IReadOnlyList<Scene> Scenes { get; }

            internal Dictionary<string, StartType[]> Remembered { get; }

            /// <summary>
            ///     The two lengths a finished life in this scene set is read
            ///     against, taken exactly as <see cref="From"/> takes them, so the
            ///     walk and the finished reading measure one life one way.
            /// </summary>
            internal int Whole { get; }

            internal int SoFar { get; }

            internal double[] Ordinary { get; }

            internal double[] Swing { get; }

            /// <summary>Per scene, where its answers begin in the flat numbering of them.</summary>
            internal int[] Starts { get; }

            internal int Occurrences { get; }

            /// <summary>Per answer, what it lands on each facet on its own.</summary>
            internal double[][] Lands { get; }

            internal Mark[][] Marks { get; }

            /// <summary>Per scene index, the least the scenes from there on can land on each facet.</summary>
            internal double[][] Least { get; }

            internal double[][] Most { get; }

            /// <summary>Per scene index, the most the scenes from there on can add by returning.</summary>
            internal double[][] Again { get; }

            internal int Numbered(string optionId) =>
                _numbered.TryGetValue(optionId, out int answer) ? answer : -1;

            private Mark[] Marked(SceneOption option, Dictionary<Occurrence, int> occurrences)
            {
                var marks = new List<Mark>();

                foreach (var consequence in option.Consequences)
                {
                    if (consequence == null) continue;

                    var key = new Occurrence(consequence.Kind, consequence.Target,
                        Math.Sign(consequence.Amount));

                    if (!occurrences.TryGetValue(key, out int numbered))
                    {
                        numbered = occurrences.Count;
                        occurrences[key] = numbered;
                    }

                    double[]? again = null;
                    if (!string.IsNullOrEmpty(consequence.Target))
                        Returns.TryGetValue(consequence.Kind, out again);

                    int shuts = consequence.Kind == ConsequenceKind.Gate && consequence.Target != null
                        ? Numbered(consequence.Target!)
                        : -1;

                    marks.Add(new Mark(numbered, again, shuts));
                }

                return marks.ToArray();
            }
        }

        /// <summary>
        ///     What one answer lands on each facet on its own: every row of its
        ///     kind said once per consequence, plus the mark a consequence the life
        ///     moved against leaves on <see cref="Facet.Hinge"/>. This is the part
        ///     of a score that is a plain sum, so a scene contributes it whatever
        ///     the other scenes were answered.
        /// </summary>
        private static double[] Alone(SceneOption option)
        {
            var lands = new double[FacetOrder.Length];

            foreach (var consequence in option.Consequences)
            {
                if (consequence == null) continue;

                if (Weights.TryGetValue(consequence.Kind, out var rows))
                    foreach (var row in rows)
                        lands[(int)row.Facet] += row.Weight;

                if (Math.Sign(consequence.Amount) < 0) lands[(int)Facet.Hinge] += Slight;
            }

            return lands;
        }

        /// <summary>
        ///     The most this answer could add by returning to something named
        ///     elsewhere. Only a consequence with a named target can be returned
        ///     to, which is why a debt and a lost year never are.
        /// </summary>
        private static double[] CouldReturn(SceneOption option)
        {
            var most = new double[FacetOrder.Length];

            foreach (var consequence in option.Consequences)
            {
                if (consequence == null || string.IsNullOrEmpty(consequence.Target)) continue;
                if (!Returns.TryGetValue(consequence.Kind, out var again)) continue;

                for (int facet = 0; facet < most.Length; facet++) most[facet] += again[facet];
            }

            return most;
        }

        private static Terrain TerrainFor(IReadOnlyList<Scene> scenes)
        {
            var held = _terrain;
            if (held != null && ReferenceEquals(held.Scenes, scenes)) return held;

            var measured = new Terrain(scenes);
            _terrain = measured;
            return measured;
        }

        /// <summary>
        ///     One life being told, and the tree of the answers it has not given
        ///     yet. The scores are carried forward rather than recomputed, so an
        ///     answer costs the walk its own consequences and nothing else.
        /// </summary>
        private sealed class Walk : ISceneAnswers
        {
            private readonly Terrain _terrain;
            private readonly int[] _counts;
            private readonly int[] _shut;
            private readonly int[] _taken;
            private readonly List<string> _chosen = new List<string>();
            private readonly bool[] _witnessed;
            private readonly double[] _lands;
            private readonly double[] _returned;
            private readonly double[] _low;
            private readonly double[] _high;
            private int _at;
            private int _opened;
            private StartType _wanted;

            internal Walk(Terrain terrain)
            {
                _terrain = terrain;
                int facets = FacetOrder.Length;
                _counts = new int[terrain.Occurrences];
                _shut = new int[terrain.Starts[terrain.Scenes.Count]];
                _taken = new int[terrain.Scenes.Count];
                _witnessed = new bool[StationOrder.Length];
                _lands = new double[facets];
                _returned = new double[facets];
                _low = new double[facets];
                _high = new double[facets];
            }

            public IReadOnlyList<string> ChosenOptionIds => _chosen;

            public bool Chose(string optionId)
            {
                foreach (string chosen in _chosen)
                    if (string.Equals(chosen, optionId, StringComparison.Ordinal))
                        return true;

                return false;
            }

            /// <summary>
            ///     Replays the answers given so far. False when they cannot be
            ///     placed in this scene set, which is a caller asking about a life
            ///     these scenes could not have told.
            /// </summary>
            internal bool Follow(IReadOnlyList<string>? answered)
            {
                int given = 0;
                var placed = new HashSet<string>(StringComparer.Ordinal);

                if (answered != null)
                    foreach (string id in answered)
                        if (placed.Add(id))
                            given++;

                while (_at < _terrain.Scenes.Count && _chosen.Count < given)
                {
                    var scene = _terrain.Scenes[_at];
                    if (!scene.Appears(this)) return false;

                    int taken = -1;
                    for (int option = 0; option < scene.Options.Count; option++)
                        if (placed.Contains(scene.Options[option].Id))
                        {
                            taken = option;
                            break;
                        }

                    if (taken < 0) return false;

                    Take(_at, taken);
                    _at++;
                }

                return _chosen.Count == given;
            }

            /// <summary>Whether some set of the answers still to come finishes this life on that station.</summary>
            internal bool Reaches(StartType station)
            {
                if (_witnessed[(int)station]) return true;

                _wanted = station;
                _opened = 0;

                // One greedy telling first. An answer that closes nothing is the
                // common case and a witness settles it outright, which leaves the
                // whole tree to be walked only where a closure is genuinely in
                // question
                return Glimpse() || Search(_at);
            }

            /// <summary>
            ///     One life told greedily to its end, each scene answered with
            ///     whichever answer leaves the wanted station furthest ahead. A
            ///     witness when it lands there, and nothing either way when it does
            ///     not.
            /// </summary>
            private bool Glimpse()
            {
                int told = 0;

                while (_at + told < _terrain.Scenes.Count)
                {
                    int at = _at + told;
                    var scene = _terrain.Scenes[at];
                    if (!scene.Appears(this)) break;

                    int best = -1;
                    double furthest = double.MinValue;

                    for (int option = 0; option < scene.Options.Count; option++)
                    {
                        if (_shut[_terrain.Starts[at] + option] > 0) continue;

                        Take(at, option);
                        double ahead = Ceiling(at + 1);
                        Drop(at, option);

                        if (ahead <= furthest) continue;

                        furthest = ahead;
                        best = option;
                    }

                    if (best < 0) break;

                    Take(at, best);
                    told++;
                }

                bool landed = _at + told >= _terrain.Scenes.Count && Settles() == _wanted;

                while (told > 0)
                {
                    told--;
                    Drop(_at + told, _taken[_at + told]);
                }

                return landed;
            }

            private bool Search(int at)
            {
                if (_opened++ > WalkBudget) return true;

                double ceiling = Ceiling(at);
                if (ceiling < -Slack) return false;

                if (at >= _terrain.Scenes.Count)
                {
                    if (ceiling > Slack) return true;

                    // A life whose nearness ties its rival's exactly is the one
                    // case the ceiling cannot settle, and the tie-break decides it
                    return Settles() == _wanted;
                }

                var scene = _terrain.Scenes[at];

                // A scene this walk cannot put in front of the player is a scene
                // set it does not understand, and a station it does not understand
                // is one it may not call closed
                if (!scene.Everyone && !scene.Appears(this)) return true;

                for (int option = 0; option < scene.Options.Count; option++)
                {
                    if (_shut[_terrain.Starts[at] + option] > 0) continue;

                    Take(at, option);
                    bool reached = Search(at + 1);
                    Drop(at, option);

                    if (reached) return true;
                }

                return false;
            }

            /// <summary>
            ///     The most the wanted station can finish ahead of its nearest
            ///     rival, given what the scenes from <paramref name="at"/> on can
            ///     still land. Below nought and no answer can put it first.
            ///
            ///     A station's miss is a sum of one term per facet, so the ceiling
            ///     is too: each facet's term is taken where it reads worst for the
            ///     rival and best for the wanted station, which is an end of that
            ///     facet's interval or one of the two shares the pair asks for. At
            ///     a leaf the interval is a point and every candidate is that
            ///     point, so the ceiling is the difference itself.
            /// </summary>
            private double Ceiling(int at)
            {
                var least = _terrain.Least[at];
                var most = _terrain.Most[at];
                var again = _terrain.Again[at];
                var ordinary = _terrain.Ordinary;
                var swing = _terrain.Swing;

                for (int facet = 0; facet < _low.Length; facet++)
                {
                    double held = _lands[facet] + _returned[facet];
                    double span = swing[facet];

                    _low[facet] = span <= 0 ? Reads(0) : Reads((held + least[facet] - ordinary[facet]) / span);
                    _high[facet] = span <= 0
                        ? Reads(0)
                        : Reads((held + most[facet] + again[facet] - ordinary[facet]) / span);
                }

                double ceiling = double.MaxValue;

                foreach (var rivalry in Rivalries[(int)_wanted])
                {
                    var facets = rivalry.Facets;
                    double ahead = 0;

                    for (int row = 0; row < facets.Length; row++)
                    {
                        int facet = facets[row];
                        ahead += Widest(_low[facet], _high[facet],
                            rivalry.Theirs[row], rivalry.TheirWant[row],
                            rivalry.Ours[row], rivalry.OurWant[row]);
                    }

                    if (ahead >= ceiling) continue;

                    ceiling = ahead;
                    if (ceiling < -Slack) break;
                }

                return ceiling;
            }

            /// <summary>
            ///     The largest that one facet can put the wanted station ahead of a
            ///     rival: the rival's miss there less our own. Both are bends in a
            ///     line, so the largest sits at an end of the interval or at one of
            ///     the two bends.
            /// </summary>
            private static double Widest(double low, double high,
                double theirs, double theirWant, double ours, double ourWant)
            {
                double widest = Apart(low, theirs, theirWant, ours, ourWant);

                double at = Apart(high, theirs, theirWant, ours, ourWant);
                if (at > widest) widest = at;

                if (ourWant > low && ourWant < high)
                {
                    at = Apart(ourWant, theirs, theirWant, ours, ourWant);
                    if (at > widest) widest = at;
                }

                if (theirWant > low && theirWant < high)
                {
                    at = Apart(theirWant, theirs, theirWant, ours, ourWant);
                    if (at > widest) widest = at;
                }

                return widest;
            }

            private static double Apart(double share,
                double theirs, double theirWant, double ours, double ourWant) =>
                theirs * Math.Abs(share - theirWant) - ours * Math.Abs(share - ourWant);

            /// <summary>
            ///     What the finished life reads as, asked of the ranking itself, and
            ///     remembered so a station some other walk of this life already
            ///     landed on is never searched for again.
            /// </summary>
            private StartType Settles()
            {
                var portrait = new Portrait();

                for (int facet = 0; facet < _lands.Length; facet++)
                    portrait._scores[FacetOrder[facet]] = _lands[facet] + _returned[facet];

                portrait.Normalize(_terrain.Measured, _terrain.SoFar, _terrain.Whole);
                portrait.Standings = portrait.Rank();

                var station = portrait.Standings[0].Station;
                _witnessed[(int)station] = true;

                return station;
            }

            private void Take(int at, int option)
            {
                int answer = _terrain.Starts[at] + option;
                var lands = _terrain.Lands[answer];

                for (int facet = 0; facet < _lands.Length; facet++) _lands[facet] += lands[facet];

                foreach (var mark in _terrain.Marks[answer])
                {
                    if (_counts[mark.Occurrence]++ >= 1 && mark.Again != null)
                        for (int facet = 0; facet < _returned.Length; facet++)
                            _returned[facet] += mark.Again[facet];

                    if (mark.Shuts >= 0) _shut[mark.Shuts]++;
                }

                _taken[at] = option;
                _chosen.Add(_terrain.Scenes[at].Options[option].Id);
            }

            private void Drop(int at, int option)
            {
                int answer = _terrain.Starts[at] + option;
                var lands = _terrain.Lands[answer];

                for (int facet = 0; facet < _lands.Length; facet++) _lands[facet] -= lands[facet];

                foreach (var mark in _terrain.Marks[answer])
                {
                    if (--_counts[mark.Occurrence] >= 1 && mark.Again != null)
                        for (int facet = 0; facet < _returned.Length; facet++)
                            _returned[facet] -= mark.Again[facet];

                    if (mark.Shuts >= 0) _shut[mark.Shuts]--;
                }

                _chosen.RemoveAt(_chosen.Count - 1);
            }
        }
    }
}
