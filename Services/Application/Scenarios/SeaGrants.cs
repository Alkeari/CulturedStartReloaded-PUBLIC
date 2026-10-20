using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     What a start that goes to sea is actually given, and the one place the
    ///     figures come from.
    ///
    ///     Every quantity here is asked of the running game: the hulls are the
    ///     culture's own, the number of them is the ship limit model's ideal, the
    ///     crew is the fewest hands those hulls need to leave harbor, and the port
    ///     is a settlement that reports one. Nothing is rolled, because the panel
    ///     beside the character states these numbers before the campaign opens and
    ///     a second roll at apply time would make the panel a guess.
    ///
    ///     Without War Sails the types are still here and the DATA is not: no
    ///     <see cref="ShipHull"/> is registered and no settlement reports a port,
    ///     so <see cref="Offered"/> comes back None, the chapter never joins the
    ///     flow, and nothing warns, disables or fails.
    /// </summary>
    public static class SeaGrants
    {
        private static bool? _contentPresent;
        private static readonly Dictionary<string, IReadOnlyList<ShipHull>> HullCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Settlement?> SeatCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PartyTemplateObject?> SeaCaravanCache = new(StringComparer.Ordinal);

        /// <summary>
        ///     The holding the player had picked before a sea degree moved them to a
        ///     port. Stepping back to the dry answer has to put it back, or a player
        ///     who looked at the water and declined keeps the seat it chose for them.
        /// </summary>
        private static Settlement? _seatBeforeTheWater;
        private static bool _seatRemembered;
        private static Settlement? _locationBeforeTheWater;
        private static bool _locationRemembered;

        /// <summary>Forgets everything read off the world, for a fresh creation session.</summary>
        public static void Reset()
        {
            _contentPresent = null;
            HullCache.Clear();
            SeatCache.Clear();
            SeaCaravanCache.Clear();
            _seatBeforeTheWater = null;
            _seatRemembered = false;
            _locationBeforeTheWater = null;
            _locationRemembered = false;
        }

        /// <summary>
        ///     Whether the water exists in this campaign at all: the DLC loaded, at
        ///     least one hull registered, and at least one settlement with a port.
        ///     The DLC is asked of <see cref="NavalDLCService"/>, which is the one
        ///     place in this mod that answers that question.
        /// </summary>
        public static bool ContentPresent()
        {
            if (_contentPresent.HasValue) return _contentPresent.Value;

            try
            {
                _contentPresent = NavalDLCService.IsNavalDLCLoaded()
                                  && MBObjectManager.Instance?.GetObjectTypeList<ShipHull>()?.Count > 0
                                  && Settlement.All.Any(settlement => settlement.HasPort);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SeaGrants: reading whether this campaign has a sea failed: {ex.Message}");
                _contentPresent = false;
            }

            return _contentPresent.Value;
        }

        /// <summary>
        ///     The degree this session can really be offered, or None. Everything the
        ///     chapter would state has to exist before the chapter is put: a life that
        ///     went to sea, hulls to grant, and where the degree needs one, a port to
        ///     grant them at.
        /// </summary>
        public static SeaDegree Offered(CharacterCreationSession? session)
        {
            if (session == null) return SeaDegree.None;

            try
            {
                var degree = SeaDegrees.For(session.SelectedStartType);
                if (degree == SeaDegree.None) return SeaDegree.None;
                if (!ContentPresent()) return SeaDegree.None;
                if (!SeaDegrees.LifeReaches(LifeProfile.From(session), degree)) return SeaDegree.None;
                if (Hulls(session, degree).Count == 0) return SeaDegree.None;
                if (degree != SeaDegree.Raider && Seat(session, degree) == null) return SeaDegree.None;

                return degree;
            }
            catch (Exception ex)
            {
                CSLogger.Error("SeaGrants: deciding whether this start reaches the water failed, " +
                               "so it stays ashore.", ex);
                return SeaDegree.None;
            }
        }

        /// <summary>
        ///     Exactly the hulls this degree grants, in the order they are granted.
        ///     One list, read by the panel and by the grant, so the two cannot name
        ///     different ships.
        /// </summary>
        public static IReadOnlyList<ShipHull> Hulls(CharacterCreationSession session, SeaDegree degree)
        {
            var culture = CultureOf(session);
            string key = $"{degree}|{culture?.StringId ?? "none"}";
            if (HullCache.TryGetValue(key, out var cached)) return cached;

            IReadOnlyList<ShipHull> hulls;
            try
            {
                hulls = BuildHulls(session, degree, culture);
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SeaGrants: choosing the hulls for {degree} failed.", ex);
                hulls = Array.Empty<ShipHull>();
            }

            HullCache[key] = hulls;
            return hulls;
        }

        /// <summary>
        ///     The hands that board. The hulls' own skeletal crew capacity is the
        ///     fewest people the game says can sail them, which makes it the one
        ///     figure a panel can state without inventing anything.
        ///
        ///     It is trimmed to what the party will hold, because the muster runs
        ///     after this grant and clamps itself to the room left over: the crew
        ///     comes out of the muster the purse bought rather than on top of it,
        ///     and the panel says so.
        /// </summary>
        public static int Crew(CharacterCreationSession session, IReadOnlyList<ShipHull> hulls)
        {
            int hands = hulls.Sum(hull => Math.Max(0, hull.SkeletalCrewCapacity));

            try
            {
                int room = GameCaps.MaxTroopsLive(session.EffectiveClanTier, session)
                           - session.StartingCompanions
                           - FamilyAges.InPartyCount(session);
                return Math.Max(0, Math.Min(hands, room));
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SeaGrants: reading the party limit for the crew failed: {ex.Message}");
                return hands;
            }
        }

        /// <summary>
        ///     The port this degree seats the start at: a castle with a port for the
        ///     seat on the water, a port town for the sea caravan, and nothing for a
        ///     raider, who answers to no harbor.
        /// </summary>
        public static Settlement? Seat(CharacterCreationSession session, SeaDegree degree)
        {
            if (degree == SeaDegree.Raider || degree == SeaDegree.None) return null;

            var culture = CultureOf(session);
            string key = $"{degree}|{culture?.StringId ?? "none"}|{session.SelectedKingdom?.StringId ?? "none"}";
            if (SeatCache.TryGetValue(key, out var cached)) return cached;

            Settlement? seat;
            try
            {
                seat = degree == SeaDegree.Admiral
                    ? AdmiralSeat(session, culture)
                    : Preferred(Settlement.All.Where(s => s.IsTown && s.HasPort), culture);
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SeaGrants: finding the port for {degree} failed.", ex);
                seat = null;
            }

            SeatCache[key] = seat;
            return seat;
        }

        /// <summary>
        ///     Writes the answer onto the session, including the holding or starting
        ///     town the degree decides. Stepping back to the dry answer puts back
        ///     whatever the player had picked before the water moved it.
        /// </summary>
        public static void Choose(CharacterCreationSession session, SeaDegree degree)
        {
            if (degree == SeaDegree.None)
            {
                session.SelectedSeaDegree = SeaDegree.None;
                if (_seatRemembered)
                {
                    session.SelectedSettlement = _seatBeforeTheWater;
                    _seatRemembered = false;
                }

                if (_locationRemembered)
                {
                    session.SelectedLocation = _locationBeforeTheWater;
                    _locationRemembered = false;
                }

                return;
            }

            session.SelectedSeaDegree = degree;
            var seat = Seat(session, degree);
            if (seat == null) return;

            if (degree == SeaDegree.Admiral)
            {
                if (!_seatRemembered)
                {
                    _seatBeforeTheWater = session.SelectedSettlement;
                    _seatRemembered = true;
                }

                session.SelectedSettlement = seat;
            }
            else if (degree == SeaDegree.Venturer)
            {
                if (!_locationRemembered)
                {
                    _locationBeforeTheWater = session.SelectedLocation;
                    _locationRemembered = true;
                }

                session.SelectedLocation = seat;
                session.UseRandomLocation = false;
            }
        }

        /// <summary>
        ///     Re-states the holding or starting town the degree decided, ahead of the
        ///     scenario applier that hands the fief over.
        ///
        ///     The chapter writes this when the answer is given, but the holding
        ///     chapter sits ahead of it in the flow, so a player can take the water,
        ///     walk back and pick an inland castle. The degree is the later answer
        ///     and its panel says it settles which holding is granted, so it is the
        ///     one that stands; without this the character would be seated inland
        ///     while the panel that put them at a quay was still on the screen.
        /// </summary>
        public static void Settle(StartContext context)
        {
            var session = context.Session;
            var degree = session.SelectedSeaDegree;
            if (degree == SeaDegree.None) return;

            var seat = Seat(session, degree);
            if (seat == null) return;

            if (degree == SeaDegree.Admiral && session.SelectedSettlement != seat)
            {
                session.SelectedSettlement = seat;
                CSLogger.Info($"SeaGrants: the seat on the water stands, so the holding is {seat.Name}.");
            }
            else if (degree == SeaDegree.Venturer && session.SelectedLocation != seat)
            {
                session.SelectedLocation = seat;
                session.UseRandomLocation = false;
                CSLogger.Info($"SeaGrants: the sea caravan sails from {seat.Name}.");
            }
        }

        /// <summary>
        ///     Puts the ships and their crew under the character. Runs from
        ///     <c>ScenarioStep</c>, which is ahead of the muster, so the hands that
        ///     board are already in the roster when the muster works out how much
        ///     room the party limit leaves it.
        /// </summary>
        public static void Apply(StartContext context)
        {
            var session = context.Session;
            var degree = session.SelectedSeaDegree;
            if (degree == SeaDegree.None) return;

            var party = context.Hero.PartyBelongedTo;
            if (party == null)
            {
                context.Report.AddProblem("Sea: the hero has no party, so no ship was granted");
                return;
            }

            var hulls = Hulls(session, degree);
            if (hulls.Count == 0)
            {
                context.Report.AddProblem("Sea: no hull could be found, so the start begins on land");
                CSLogger.Warn($"SeaGrants: {degree} reached apply with no hull to grant.");
                return;
            }

            foreach (var hull in hulls)
            {
                try
                {
                    ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(hull));
                    CSLogger.Info($"SeaGrants: granted {hull.Name} ({hull.StringId}).");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"Sea: {hull.Name} could not be granted");
                    CSLogger.Error($"SeaGrants: granting {hull.StringId} failed.", ex);
                }
            }

            try
            {
                party.SetNavalVisualAsDirty();
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SeaGrants: refreshing the naval visual failed: {ex.Message}");
            }

            Board(context, degree, Crew(session, hulls));
        }

        /// <summary>
        ///     Sets sail, which is the LAST write to the party's position in the
        ///     whole pipeline and has to be.
        ///
        ///     <c>LocationStep</c> decides WHERE the story begins and this decides
        ///     that it begins afloat. <c>SetSailAtPosition</c> moves the party onto
        ///     the port's own water and puts it into the naval state together; run
        ///     ahead of the position write it would leave a party at sea by state
        ///     and standing on a road by position, which is the same defect in the
        ///     other direction. The game's own naval start writes them in this
        ///     order too.
        /// </summary>
        public static void PutToSea(StartContext context, Settlement target)
        {
            if (context.Session.SelectedSeaDegree == SeaDegree.None) return;

            var party = context.Hero.PartyBelongedTo;
            if (party == null) return;

            try
            {
                var port = NearestPort(target);
                if (port == null)
                {
                    CSLogger.Info("SeaGrants: no port could be reached, so the start stays on land with its ships.");
                    return;
                }

                party.SetSailAtPosition(port.PortPosition);
                CSLogger.Info($"SeaGrants: the story begins afloat, off {port.Name}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("SeaGrants: setting sail failed, so the start stays on land.", ex);
            }
        }

        /// <summary>
        ///     The port a start beginning at this settlement sails from: its own
        ///     where it has one, otherwise the nearest that does. Read by the panel
        ///     as well as by the grant, so the place the player is told about is the
        ///     place they wake up off.
        /// </summary>
        public static Settlement? NearestPort(Settlement? target)
        {
            if (target == null) return null;
            if (target.HasPort) return target;

            return SettlementHelper.FindNearestSettlementToPoint(target.GatePosition, s => s.HasPort);
        }

        /// <summary>
        ///     The sea caravan of this culture: the party template the game itself
        ///     uses for a caravan that sails. Asked once per culture and held,
        ///     because the game's own answer is a random one among the templates
        ///     that qualify and the panel has to name what the grant will use.
        /// </summary>
        public static PartyTemplateObject? SeaCaravan(CultureObject? culture)
        {
            if (culture == null) return null;
            if (SeaCaravanCache.TryGetValue(culture.StringId, out var cached)) return cached;

            PartyTemplateObject? template = null;
            try
            {
                template = CaravanHelper.GetRandomCaravanTemplate(culture, false, false);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SeaGrants: asking {culture.StringId} for a sea caravan failed: {ex.Message}");
            }

            SeaCaravanCache[culture.StringId] = template;
            return template;
        }

        private static CultureObject? CultureOf(CharacterCreationSession session) =>
            session.SelectedCulture ?? Hero.MainHero?.Culture;

        private static IReadOnlyList<ShipHull> BuildHulls(CharacterCreationSession session,
            SeaDegree degree, CultureObject? culture)
        {
            int limit = ShipLimit();
            if (limit <= 0) return Array.Empty<ShipHull>();

            if (degree == SeaDegree.Venturer) return SeaCaravanHulls(culture, limit);

            var available = Available(culture);
            if (available.Count == 0) return Array.Empty<ShipHull>();

            if (degree == SeaDegree.Raider)
            {
                // Shallow draft is the whole of a raider's advantage: a hull that can
                // run up a beach reaches the villages the deep keels cannot. One short
                // of what a lord's party can hold, because nobody gives an outlaw a
                // squadron; the cheapest such hull, because he did not buy it either.
                var shallow = available.Where(h => h.CanNavigateShallowWater).ToList();
                var beachable = shallow.Where(h => h.Type == ShipHull.ShipType.Light).ToList();
                var pick = First(beachable.Count > 0 ? beachable : shallow, h => h.Value);
                if (pick == null) return Array.Empty<ShipHull>();

                return Enumerable.Repeat(pick, Math.Max(1, limit - 1)).ToList();
            }

            // The seat on the water: the squadron the ship limit model calls ideal
            // for a lord's party, weighted to the heavier hulls and ending in one
            // light one, which is the shape the game's own naval start grants.
            var medium = available.Where(h => h.Type == ShipHull.ShipType.Medium).ToList();
            var light = available.Where(h => h.Type == ShipHull.ShipType.Light).ToList();
            var heavier = First(medium.Count > 0 ? medium : available, h => -h.SeaWorthiness);
            var lighter = First(light.Count > 0 ? light : available, h => -h.SeaWorthiness);
            if (heavier == null || lighter == null) return Array.Empty<ShipHull>();

            var squadron = new List<ShipHull>();
            for (int i = 0; i < Math.Max(1, limit - 1); i++) squadron.Add(heavier);
            if (limit > 1) squadron.Add(lighter);
            return squadron;
        }

        /// <summary>
        ///     What a sea caravan of this culture sails with, taken off the template
        ///     itself rather than named here: the stacks declare how many of each
        ///     hull, and the smallest declared complement is the one a start can
        ///     promise without overshooting what the party may hold.
        /// </summary>
        private static IReadOnlyList<ShipHull> SeaCaravanHulls(CultureObject? culture, int limit)
        {
            var template = SeaCaravan(culture);
            var hulls = new List<ShipHull>();
            if (template?.ShipHulls == null) return hulls;

            foreach (var stack in template.ShipHulls.OrderBy(s => s.ShipHull?.StringId ?? "",
                         StringComparer.Ordinal))
            {
                if (stack.ShipHull == null) continue;
                for (int i = 0; i < Math.Max(1, stack.MinValue) && hulls.Count < limit; i++)
                    hulls.Add(stack.ShipHull);
            }

            return hulls;
        }

        private static IReadOnlyList<ShipHull> Available(CultureObject? culture)
        {
            var hulls = culture?.AvailableShipHulls;
            if (hulls != null && hulls.Count > 0) return hulls.ToList();

            return MBObjectManager.Instance?.GetObjectTypeList<ShipHull>()?.ToList()
                   ?? (IReadOnlyList<ShipHull>)Array.Empty<ShipHull>();
        }

        /// <summary>
        ///     The best of a set by one measure, with the id breaking ties, so the
        ///     same world always yields the same ship. A roll here would put a hull
        ///     in the panel and a different hull in the harbor.
        /// </summary>
        private static ShipHull? First(IEnumerable<ShipHull> hulls, Func<ShipHull, int> by) =>
            hulls.OrderBy(by).ThenBy(h => h.StringId, StringComparer.Ordinal).FirstOrDefault();

        private static Settlement? AdmiralSeat(CharacterCreationSession session, CultureObject? culture)
        {
            var kingdom = session.SelectedKingdom;
            var inRealm = Settlement.All.Where(s => s.IsCastle && s.HasPort &&
                                                    s.OwnerClan?.MapFaction == kingdom);
            return Preferred(inRealm, culture)
                   ?? Preferred(Settlement.All.Where(s => s.IsCastle && s.HasPort), culture)
                   ?? Preferred(Settlement.All.Where(s => s.HasPort &&
                                                          s.OwnerClan?.MapFaction == kingdom), culture);
        }

        private static Settlement? Preferred(IEnumerable<Settlement> candidates, CultureObject? culture)
        {
            var all = candidates.ToList();
            if (all.Count == 0) return null;

            var own = all.Where(s => culture != null && s.Culture == culture).ToList();
            return (own.Count > 0 ? own : all)
                .OrderBy(s => s.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        ///     How many hulls one party may sail, asked of the model that decides it.
        ///     Zero when the model cannot be read, which takes the whole chapter off
        ///     the flow rather than guessing a number at a player.
        /// </summary>
        private static int ShipLimit()
        {
            try
            {
                var model = Campaign.Current?.Models?.PartyShipLimitModel;
                var party = MobileParty.MainParty;
                if (model == null || party == null) return 0;

                return Math.Max(0, model.GetIdealShipNumber(party));
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"SeaGrants: reading the ship limit failed: {ex.Message}");
                return 0;
            }
        }

        private static void Board(StartContext context, SeaDegree degree, int crew)
        {
            if (crew <= 0) return;

            var party = context.Hero.PartyBelongedTo;
            var pool = CrewPool(context.Session, degree);
            if (party == null || pool.Count == 0)
            {
                CSLogger.Warn($"SeaGrants: no crew could be found for {degree}, so the ships sail undermanned.");
                return;
            }

            for (int i = 0; i < crew; i++)
                party.MemberRoster.AddToCounts(pool[i % pool.Count], 1);

            CSLogger.Info($"SeaGrants: {crew} hands boarded for {degree}.");
        }

        /// <summary>
        ///     Who boards: sea raiders for the raider, the culture's marines for the
        ///     seat on the water, and the sea caravan's own guards for the venturer.
        ///     Ordered by id so the same world always fills the same roster.
        /// </summary>
        private static IReadOnlyList<CharacterObject> CrewPool(CharacterCreationSession session, SeaDegree degree)
        {
            var culture = CultureOf(session);
            int top = Math.Max(2, session.EffectiveClanTier);

            try
            {
                if (degree == SeaDegree.Venturer)
                {
                    var template = SeaCaravan(culture);
                    var guards = template?.Stacks?
                        .Select(stack => stack.Character)
                        .Where(character => character != null)
                        .Distinct()
                        .OrderBy(character => character.StringId, StringComparer.Ordinal)
                        .ToList();
                    if (guards != null && guards.Count > 0) return guards;
                }

                if (degree == SeaDegree.Raider)
                {
                    var outlaws = Mariners(BanditRoots(culture), top);
                    if (outlaws.Count > 0) return outlaws;
                }

                var marines = Mariners(new[] { culture?.BasicTroop, culture?.EliteBasicTroop }, top);
                if (marines.Count > 0) return marines;

                // No marines anywhere means a world with ships and nobody trained for
                // them. The ships are still real, so the deck is manned by the
                // ordinary soldiers the culture has rather than left empty.
                var soldiers = new[] { culture?.BasicTroop, culture?.EliteBasicTroop }
                    .Where(troop => troop != null)
                    .ToList();
                return soldiers!;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SeaGrants: gathering a crew for {degree} failed.", ex);
                return Array.Empty<CharacterObject>();
            }
        }

        private static IReadOnlyList<CharacterObject?> BanditRoots(CultureObject? culture)
        {
            var roots = new List<CharacterObject?>();

            foreach (var clan in Clan.BanditFactions)
            {
                if (clan?.Culture == null) continue;
                roots.Add(clan.Culture.BasicTroop);
                roots.Add(clan.Culture.EliteBasicTroop);
            }

            roots.Add(culture?.BanditRaider);
            roots.Add(culture?.BanditBandit);
            return roots;
        }

        /// <summary>
        ///     The naval soldiers in these troop trees, up to the tier the clan can
        ///     field. <c>IsMariner</c> is the game's own marker for a troop trained
        ///     for a deck, set from the NavalSoldier trait, so nothing here decides
        ///     what a marine is. It is read through <c>GameCompat</c> because games
        ///     older than v1.3.12 have no such marker to read.
        /// </summary>
        private static IReadOnlyList<CharacterObject> Mariners(IEnumerable<CharacterObject?> roots, int top)
        {
            var found = new Dictionary<string, CharacterObject>(StringComparer.Ordinal);

            foreach (var root in roots)
            {
                if (root == null) continue;

                foreach (var troop in CharacterHelper.GetTroopTree(root, 1f, top))
                    if (troop != null && GameCompat.IsMariner(troop))
                        found[troop.StringId] = troop;
            }

            return found.Values.OrderBy(t => t.StringId, StringComparer.Ordinal).ToList();
        }
    }
}
