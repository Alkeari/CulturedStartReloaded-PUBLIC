using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The same profile, read from what a guided run left behind instead of
    ///     from seven chapter answers.
    ///
    ///     The scene route never asks what kind of family or schooling a character
    ///     had, so there is no enum for this half to switch on. What it has is the
    ///     consequences the answers carried, which is also what the player was
    ///     shown when they chose, so the leaning a later chapter reads and the
    ///     effect the panel stated cannot come apart.
    ///
    ///     <see cref="LifeProfile.Lean.Sea"/> is reached from exactly one target,
    ///     <c>the_open_water</c>, which exists on two answers of one scene and
    ///     only in the run War Sails is loaded for. Nothing that merely resembles
    ///     the sea scores it: a coastal town, a cargo and a man who sold his
    ///     passage are a town, a cargo and a man, and a lean filled in from those
    ///     would put a player who never left the road into the three skills the
    ///     DLC adds. One target, named once, is what makes "this life went to sea"
    ///     a thing the reading can be sure of rather than infer.
    /// </summary>
    public sealed partial class LifeProfile
    {
        /// <summary>The profile a guided run's answers add up to.</summary>
        public static LifeProfile From(IReadOnlyList<ChoiceConsequence>? consequences)
        {
            var profile = new LifeProfile();
            if (consequences == null) return profile;

            foreach (var consequence in consequences)
            {
                switch (consequence.Kind)
                {
                    case ConsequenceKind.Goodwill:
                        profile.AddStanding(consequence.Target, Weight(consequence.Amount));
                        break;
                    case ConsequenceKind.Ally:
                        profile.Add(Lean.Following, 2);
                        break;
                    case ConsequenceKind.Title:
                        profile.AddTitle(consequence.Target);
                        break;
                    case ConsequenceKind.Place:
                        profile.AddPlace(consequence.Target, consequence.Amount);
                        break;
                    case ConsequenceKind.Item:
                        profile.AddItem(consequence.Target);
                        break;
                    case ConsequenceKind.Trait:
                        profile.AddTrait(consequence.Target, Weight(consequence.Amount));
                        break;
                }
            }

            return profile;
        }

        /// <summary>An amount of zero is one, because a consequence always counts once.</summary>
        private static int Weight(int amount) => amount == 0 ? 1 : amount;

        /// <summary>
        ///     Who thinks well of you. Enmity is deliberately not read here: being
        ///     hated by lords is a relation the pipeline applies, not time spent
        ///     among them, and counting it would let a life lean noble by offending
        ///     enough of them.
        /// </summary>
        private void AddStanding(string? target, int amount)
        {
            switch (target)
            {
                case "culture_lords":
                    Add(Lean.Standing, amount);
                    break;
                case "town_merchants":
                    Add(Lean.Commerce, amount);
                    break;
                case "village_headmen":
                    Add(Lean.Following, amount);
                    break;
                case "town_gang_leaders":
                    Add(Lean.Martial, amount);
                    break;
            }
        }

        /// <summary>
        ///     What people call you. A name the law gave you is not standing, so
        ///     the two that are held against a character add nothing rather than
        ///     counting as rank.
        /// </summary>
        private void AddTitle(string? target)
        {
            switch (target)
            {
                case "wanted":
                case "oathbreaker":
                    break;
                // A trade is what the hands do, not what the stall sells. Reading
                // this one as commerce left the single option in the run named for
                // a trade unable to say anything about making
                case "by_the_trade":
                    Add(Lean.Craft, 2);
                    break;
                case "of_the_place":
                    Add(Lean.Following, 2);
                    break;
                default:
                    Add(Lean.Standing, 2);
                    break;
            }
        }

        /// <summary>
        ///     Where a life was spent. Every place but one is a yes or a no, so
        ///     the amount beside it is ignored the way it always was; the water
        ///     carries how much of the life went into it, because the scene that
        ///     offers it is asking exactly that and the answer decides whether a
        ///     character reads as a sailor or as a man who has been on a boat.
        /// </summary>
        private void AddPlace(string? target, int amount)
        {
            switch (target)
            {
                case "family_seat":
                case "granted_holding":
                case "nearest_castle":
                    Add(Lean.Standing, 2);
                    break;
                case "home_village":
                    Add(Lean.Following, 1);
                    break;
                case "nearest_town":
                    Add(Lean.Commerce, 1);
                    break;
                case "home_workshop":
                    Add(Lean.Craft, 2);
                    break;
                case "open_country":
                case "home_woodland":
                case "nearest_hideout":
                    Add(Lean.Wilds, 2);
                    break;
                // The only road to the sea in the whole catalog, and the only
                // place that reads its own amount
                case "the_open_water":
                    Add(Lean.Sea, Weight(amount));
                    break;
            }
        }

        /// <summary>
        ///     What a life put in your hands. A weapon trains the hands as well as
        ///     leaning the life, and the last one reached for is the one they know,
        ///     which is the same rule the chapters used.
        /// </summary>
        private void AddItem(string? target)
        {
            switch (target)
            {
                case "family_sword":
                case "one_handed_sword":
                    Add(Lean.Martial, 2);
                    Train(Trained.Blade);
                    break;
                case "spear":
                    Add(Lean.Martial, 2);
                    Train(Trained.Spear);
                    break;
                case "two_handed_axe":
                    Add(Lean.Martial, 2);
                    Train(Trained.GreatWeapon);
                    break;
                case "lance":
                    Add(Lean.Martial, 2);
                    Add(Lean.Standing, 1);
                    Train(Trained.Lance);
                    break;
                case "hunting_bow":
                    Add(Lean.Wilds, 2);
                    Train(Trained.Bow);
                    break;
                case "mail_hauberk":
                case "war_horse":
                    Add(Lean.Martial, 2);
                    break;
                case "riding_horse":
                case "riding_tack":
                    Add(Lean.Wilds, 1);
                    break;
                case "coin_pouch":
                case "trade_goods":
                case "pack_mule":
                    Add(Lean.Commerce, 2);
                    break;
                // A set of tools is a whole living, where a sword is one of the
                // several things a martial life collects along the way. Only four
                // answers in the run reach making at all, so each has to carry it
                case "craft_tools":
                    Add(Lean.Craft, 3);
                    break;
            }
        }

        /// <summary>
        ///     Mercy moves no lean. It says what a character will not do rather
        ///     than what they spent their years at, and the pipeline applies it as
        ///     a trait either way.
        /// </summary>
        private void AddTrait(string? target, int amount)
        {
            int weight = amount * TraitWeight;

            switch (target)
            {
                case "Valor":
                    Add(Lean.Martial, weight);
                    break;
                case "Calculating":
                    Add(Lean.Commerce, weight);
                    break;
                case "Honor":
                    Add(Lean.Standing, weight);
                    break;
                case "Generosity":
                    Add(Lean.Following, weight);
                    break;
            }
        }

        /// <summary>
        ///     What one step of a trait is worth against an object or a standing.
        ///
        ///     A step used to be worth one, which was right when a life could
        ///     collect eight of them: a run that moved valor at ten of its thirteen
        ///     answers was making a claim about time under arms as loudly as a
        ///     sword in the hand did. The catalog now spends a trait at most twice
        ///     in each direction across the whole run, because a life cannot be
        ///     promised more of one than the game holds, so the same step is a far
        ///     scarcer statement and has to weigh like one. Three keeps the leans a
        ///     run produces where they were: without it a trait says almost
        ///     nothing and the life reads off its furniture.
        /// </summary>
        private const int TraitWeight = 3;
    }
}
