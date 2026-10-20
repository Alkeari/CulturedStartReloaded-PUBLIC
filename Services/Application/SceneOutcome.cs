using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     What a guided run comes to, sorted into the kinds the pipeline knows how
    ///     to apply.
    ///
    ///     The consequences themselves are the player's receipt: the effect panel
    ///     states them one by one as the option is landed on. This is the same list
    ///     added up, so what a step grants and what the player was told cannot come
    ///     apart. Nothing here resolves to a game object; that is the step's job,
    ///     and keeping it out means this can be walked without a running game.
    /// </summary>
    public sealed class SceneOutcome
    {
        private readonly Dictionary<string, int> _traits = new();
        private readonly Dictionary<RelationEffect, int> _relations = new();
        private readonly List<string> _items = new();
        private readonly List<string> _allies = new();
        private readonly List<string> _places = new();
        private readonly List<string> _titles = new();

        private SceneOutcome()
        {
        }

        /// <summary>Trait string id to the level the whole life adds up to.</summary>
        public IReadOnlyDictionary<string, int> Traits => _traits;

        /// <summary>
        ///     Who thinks what of you, net. Goodwill and enmity with the same people
        ///     cancel, because a life that did both is a life they are undecided
        ///     about.
        /// </summary>
        public IReadOnlyDictionary<RelationEffect, int> Relations => _relations;

        /// <summary>What the life put in your hands, in the order it arrived.</summary>
        public IReadOnlyList<string> Items => _items;

        /// <summary>People who left with you.</summary>
        public IReadOnlyList<string> Allies => _allies;

        /// <summary>Ground the life gave you a claim on or a reason to be near.</summary>
        public IReadOnlyList<string> Places => _places;

        /// <summary>What people call you.</summary>
        public IReadOnlyList<string> Titles => _titles;

        /// <summary>What you owe, in denars.</summary>
        public int Debt { get; private set; }

        /// <summary>Years the life spent on something that did not advance it.</summary>
        public int LostYears { get; private set; }

        public static SceneOutcome From(IReadOnlyList<ChoiceConsequence>? consequences)
        {
            var outcome = new SceneOutcome();
            if (consequences == null) return outcome;

            foreach (var consequence in consequences)
            {
                int amount = consequence.Amount == 0 ? 1 : consequence.Amount;

                switch (consequence.Kind)
                {
                    case ConsequenceKind.Trait when consequence.Target != null:
                        outcome._traits.TryGetValue(consequence.Target, out int level);
                        outcome._traits[consequence.Target] = level + amount;
                        break;

                    case ConsequenceKind.Goodwill:
                        outcome.Relate(consequence.Target, amount);
                        break;

                    case ConsequenceKind.Enmity:
                        outcome.Relate(consequence.Target, -amount);
                        break;

                    // Once per unit rather than once per consequence. The step that
                    // grants these reads the list as one item per entry, and the panel
                    // states the count to the player, so dropping it here would grant
                    // one of a thing the player was promised two of
                    case ConsequenceKind.Item when consequence.Target != null:
                        for (int i = 0; i < amount; i++)
                            outcome._items.Add(consequence.Target);
                        break;

                    case ConsequenceKind.Ally when consequence.Target != null:
                        outcome._allies.Add(consequence.Target);
                        break;

                    case ConsequenceKind.Place when consequence.Target != null:
                        outcome._places.Add(consequence.Target);
                        break;

                    case ConsequenceKind.Title when consequence.Target != null:
                        outcome._titles.Add(consequence.Target);
                        break;

                    case ConsequenceKind.Debt:
                        outcome.Debt += amount;
                        break;

                    case ConsequenceKind.LostYears:
                        outcome.LostYears += amount;
                        break;
                }
            }

            return outcome;
        }

        private void Relate(string? target, int amount)
        {
            var effect = Effect(target);
            if (effect == RelationEffect.None) return;

            _relations.TryGetValue(effect, out int current);
            _relations[effect] = current + amount;
        }

        private static RelationEffect Effect(string? target) => target switch
        {
            "town_merchants" => RelationEffect.TownMerchants,
            "town_gang_leaders" => RelationEffect.TownGangLeaders,
            "village_headmen" => RelationEffect.VillageHeadmen,
            "culture_lords" => RelationEffect.CultureLords,
            _ => RelationEffect.None
        };
    }
}
