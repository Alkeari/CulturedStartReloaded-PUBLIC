using System.Collections.Generic;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What a told life adds up to. The guided route asks only written
    ///     questions, so everything downstream that wants a number reads its
    ///     answer from here instead: how many would follow you, how you carry
    ///     yourself, what your hands were trained to hold, and what kind of seat
    ///     suits the life you led.
    ///
    ///     These are relative weights, not game values. Nothing here is ever
    ///     applied to a hero. It decides which written option a menu leads with
    ///     and how far along its own scale a menu's bands reach.
    /// </summary>
    public sealed partial class LifeProfile
    {
        /// <summary>What a choice says about the person who made it.</summary>
        public enum Lean
        {
            /// <summary>Time spent under arms.</summary>
            Martial,

            /// <summary>Coin, goods, and the people who move them.</summary>
            Commerce,

            /// <summary>Birth, court, and the assumption of being obeyed.</summary>
            Standing,

            /// <summary>People who would come when called.</summary>
            Following,

            /// <summary>Open country, and being at ease alone in it.</summary>
            Wilds,

            /// <summary>Making and mending.</summary>
            Craft,

            /// <summary>The sea.</summary>
            Sea
        }

        /// <summary>The weapon family a life trained the hands for.</summary>
        public enum Trained
        {
            None,
            Blade,
            Spear,
            Bow,
            Crossbow,
            Thrown,
            Lance,
            GreatWeapon
        }

        private readonly Dictionary<Lean, int> _scores = new();

        private LifeProfile()
        {
        }

        public int Score(Lean lean) => _scores.TryGetValue(lean, out int value) ? value : 0;

        /// <summary>The weapon family the path trained for, or None when it trained for none.</summary>
        public Trained Weapon { get; private set; }

        /// <summary>
        ///     The strongest lean, or <see cref="Lean.Following"/> before anything
        ///     is chosen. An untold life is an ordinary one, and the household the
        ///     preview stages for a character nobody has said anything about yet
        ///     reads this answer: a claim to arms would dress the family as
        ///     retainers before a single question has been asked.
        /// </summary>
        public Lean Dominant
        {
            get
            {
                var best = Lean.Following;
                int bestScore = int.MinValue;
                foreach (var pair in _scores)
                    if (pair.Value > bestScore)
                    {
                        bestScore = pair.Value;
                        best = pair.Key;
                    }

                return best;
            }
        }

        /// <summary>
        ///     A zero to four band for a menu that scales an amount. Four is the
        ///     top band, which a life has to have leaned hard to reach.
        /// </summary>
        public int Band(Lean lean)
        {
            int score = Score(lean);
            if (score <= 0) return 0;
            if (score <= 2) return 1;
            if (score <= 4) return 2;
            if (score <= 6) return 3;
            return 4;
        }

        private void Add(Lean lean, int amount)
        {
            _scores.TryGetValue(lean, out int current);
            _scores[lean] = current + amount;
        }

        // A later answer overrules an earlier one: the last thing the hands
        // learned is the thing they reach for
        private void Train(Trained weapon) => Weapon = weapon;
    }
}
