using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The single place settlements are looked up for fallbacks. Random picks
    ///     via <see cref="CSRandom"/> so "let fate decide" is not the same town
    ///     every campaign.
    ///
    ///     It also holds the one walk every written menu option asks again through,
    ///     in <see cref="KeepLooking{T}"/>. That walk is not about settlements: the
    ///     realm menu offers realms and the other two offer places, and a second
    ///     copy of the rule for a second type is one divergent edit away from the
    ///     three screens promising the player different things.
    /// </summary>
    public static class SettlementFinder
    {
        private static HashSet<Kingdom>? _realmsWithHoldings;
        private static HashSet<Kingdom>? _realmsWithCastles;

        /// <summary>Forgets which realms hold what, so a new session reads the world again.</summary>
        public static void Reset()
        {
            _realmsWithHoldings = null;
            _realmsWithCastles = null;
            Walks.Clear();
        }

        /// <summary>
        ///     Whether this realm can actually serve this start. A landed vassal is given one of the
        ///     realm's holdings and a rebel seizes one of its castles, so a realm holding neither
        ///     leads nowhere; every other start type only needs the realm itself.
        ///
        ///     One rule, asked by both the creation menu and the Start Editor. They used to disagree:
        ///     the menu filtered and the editor did not, so the editor could point a Rebel Clan at a
        ///     realm with no castle, the scenario failed validation, and the orchestrator skipped the
        ///     entire step, taking the workshops and the garrison with it. Three Realm of Thrones
        ///     realms own no castle.
        ///
        ///     Yields rather than emptying the list: if no realm qualifies, every realm is offered,
        ///     because a screen with nothing on it strands the player worse than a bad choice does.
        /// </summary>
        public static bool RealmCanServe(Kingdom? kingdom, Models.StartType startType)
        {
            if (kingdom == null) return true;

            if (_realmsWithHoldings == null || _realmsWithCastles == null)
            {
                _realmsWithHoldings = new HashSet<Kingdom>();
                _realmsWithCastles = new HashSet<Kingdom>();
                foreach (var s in Settlement.All)
                {
                    var realm = s.OwnerClan?.Kingdom;
                    if (realm == null) continue;
                    if (s.IsTown || s.IsCastle) _realmsWithHoldings.Add(realm);
                    if (s.IsCastle) _realmsWithCastles.Add(realm);
                }
            }

            return startType switch
            {
                Models.StartType.RebelClan =>
                    _realmsWithCastles.Count == 0 || _realmsWithCastles.Contains(kingdom),
                Models.StartType.LandedVassal =>
                    _realmsWithHoldings.Count == 0 || _realmsWithHoldings.Contains(kingdom),
                _ => true
            };
        }

        /// <summary>A random town of the culture, else any town, else null.</summary>
        public static Settlement? RandomCultureTown(CultureObject? culture)
        {
            if (culture != null)
            {
                var cultureTowns = Settlement.All.Where(s => s.IsTown && s.Culture == culture).ToList();
                if (cultureTowns.Count > 0)
                    return CSRandom.Pick(cultureTowns);
            }

            // A culture that holds no town anywhere. Rare in the base game, ordinary under a total
            // conversion: Realm of Thrones makes a culture selectable that owns no settlement of any
            // kind. Falling back to any town is right, doing it silently is not, because every
            // settlement decision for that character then lands somewhere unrelated to their people.
            var towns = Settlement.All.Where(s => s.IsTown).ToList();
            var chosen = CSRandom.Pick(towns);
            if (culture != null)
                CSLogger.Warn($"SettlementFinder: {culture.StringId} holds no town; " +
                              $"fell back to {chosen?.Name?.ToString() ?? "nothing"}.");
            return chosen;
        }

        /// <summary>A random fief inside the kingdom, culture-preferred, else null.</summary>
        public static Settlement? RandomVassalFief(CultureObject? culture, Kingdom kingdom)
        {
            var kingdomFiefs = Settlement.All.Where(s =>
                (s.IsTown || s.IsCastle) &&
                s.OwnerClan?.Kingdom == kingdom).ToList();

            if (culture != null)
            {
                var cultureFiefs = kingdomFiefs.Where(s => s.Culture == culture).ToList();
                if (cultureFiefs.Count > 0)
                    return CSRandom.Pick(cultureFiefs);
            }

            return CSRandom.Pick(kingdomFiefs);
        }

        /// <summary>
        ///     Where the center of a culture's country sits, averaged over every settlement its
        ///     people hold. Null for a culture that holds nothing, which a total conversion makes
        ///     selectable, so the caller can say so rather than measure against the map origin.
        /// </summary>
        public static TaleWorlds.Library.Vec2? Heartland(CultureObject? culture)
        {
            if (culture == null) return null;

            var own = Settlement.All.Where(s => s.Culture == culture).ToList();
            if (own.Count == 0) return null;

            var sum = TaleWorlds.Library.Vec2.Zero;
            foreach (var s in own) sum += s.GetPosition2D;
            return sum * (1f / own.Count);
        }

        #region Keep looking

        /// <summary>
        ///     One option's walk through the candidates that answer it. The order is
        ///     shuffled rather than sorted, so two players are not handed the same
        ///     first answer, and the cursor steps rather than rerolls, so nothing is
        ///     offered twice until every candidate has had its turn.
        /// </summary>
        private sealed class Walk<T> where T : class
        {
            private int _index;

            internal Walk(List<T> order)
            {
                Order = order;
            }

            internal List<T> Order { get; }

            internal T Showing => Order[_index];

            internal void MoveTo(T candidate)
            {
                int at = Order.IndexOf(candidate);
                if (at >= 0) _index = at;
            }

            internal void Step()
            {
                if (Order.Count == 1) return;

                _index++;
                if (_index < Order.Count) return;

                // Every candidate has been offered. Shuffle again rather than repeat
                // the same round, and never open the new round on the one still on
                // screen, which would read as the option refusing to move
                var last = Order[Order.Count - 1];
                Shuffle(Order);
                if (ReferenceEquals(Order[0], last))
                {
                    Order[0] = Order[1];
                    Order[1] = last;
                }

                _index = 0;
            }
        }

        // Every option's walk, whatever kind of thing it walks over, held as object
        // because one dictionary serves realms, halls and towns alike. A key whose
        // stored walk turns out to be over another type is rebuilt rather than cast,
        // so a reused key can never hand an option somebody else's candidates.
        private static readonly Dictionary<string, object> Walks = new();

        /// <summary>
        ///     What this option is offering right now, and with <paramref name="advance"/> the
        ///     next one instead. A written option that names a kind of thing rather than one
        ///     thing hands its whole candidate list here and gets a different answer every time
        ///     the player asks again; an option that names exactly one hands a list of one and
        ///     this returns it forever, which is the same code path and needs no special case.
        ///
        ///     The realm menu walks realms and the holding and location menus walk places, so
        ///     this is written once over whatever the option offers. Three screens teaching the
        ///     player one promise cannot be three implementations of it.
        ///
        ///     Keyed by the option, so each option walks its own order. The list is re-read on every
        ///     call because the candidates move under it: another option takes a town, or the realm
        ///     or culture behind the list changes. A rebuild keeps what is already on screen where
        ///     it is, so a neighboring option moving does not reshuffle this one under the player.
        /// </summary>
        public static T? KeepLooking<T>(string key, IReadOnlyList<T>? candidates, bool advance)
            where T : class
        {
            try
            {
                if (candidates == null || candidates.Count == 0)
                {
                    Walks.Remove(key);
                    return null;
                }

                Walks.TryGetValue(key, out object? stored);
                var walk = stored as Walk<T>;
                var showing = walk?.Showing;

                if (walk == null || !SameSet(walk.Order, candidates))
                {
                    var order = new List<T>(candidates);
                    Shuffle(order);
                    walk = new Walk<T>(order);
                    Walks[key] = walk;
                    if (showing != null) walk.MoveTo(showing);
                }

                if (advance) walk.Step();
                return walk.Showing;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SettlementFinder: keeping looking for {key} failed: {ex.Message}");
                return candidates != null && candidates.Count > 0 ? candidates[0] : null;
            }
        }

        private static bool SameSet<T>(List<T> order, IReadOnlyList<T> candidates) where T : class
        {
            if (order.Count != candidates.Count) return false;
            for (int i = 0; i < candidates.Count; i++)
                if (!order.Contains(candidates[i]))
                    return false;
            return true;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = CSRandom.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}
