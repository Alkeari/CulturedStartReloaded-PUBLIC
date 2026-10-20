using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Grants a capital town plus the castles the answers add up to, founds a kingdom,
    ///     and honors the chosen founding story: a Settler takes land from the weakest realm
    ///     with no immediate war, a Claimant starts at war with everyone dispossessed.
    ///     Afterward the player is asked to name the kingdom.
    ///
    ///     The size of the crown comes from two answers and nothing else. Standing, which the
    ///     means chapter sets, buys the castles the crown keeps in its own hand. The sworn
    ///     houses chapter buys a castle for every house that knelt, and decides whether that
    ///     castle is the house's seat or crown land. Between them a realm runs from a single
    ///     town held alone to a capital, the crown's own castles, and a seat for each of six
    ///     sworn houses.
    /// </summary>
    public sealed class MonarchScenario : IScenarioApplier
    {
        public string Name => "Monarch";

        public string? Validate(StartContext context)
        {
            var throne = context.Session.SelectedKingdom;
            if (throne != null)
                return throne.IsEliminated ? "the chosen realm no longer stands" : null;

            if (context.Session.SelectedSettlement == null &&
                SettlementFinder.RandomCultureTown(context.Hero.Culture) == null)
                return "no town exists to grant";

            return null;
        }

        public void Apply(StartContext context)
        {
            // A crown is either raised or inherited. A realm named on the session
            // is the throne the player asked to take, and everything below this
            // line is about raising one: a founding story, a name, sworn houses,
            // castles taken off whoever held them. None of that is put to the
            // monarch of a realm that already has its own
            // Cultured Start has only ever raised a crown
            bool culturedStart = context.Session.Mode == SetupMode.LifePath;

            if (!culturedStart && context.Session.SelectedKingdom != null)
            {
                TakeTheThrone(context, context.Session.SelectedKingdom);
                return;
            }

            var hero = context.Hero;
            var session = context.Session;
            var founding = session.SelectedFounding;

            var town = session.SelectedSettlement
                       ?? (founding == MonarchFounding.Settler
                           ? PickSettlerTown(hero)
                           : SettlementFinder.RandomCultureTown(hero.Culture));
            if (town == null)
                throw new InvalidOperationException("MonarchScenario: no town available.");

            // Record the holding the start actually granted. When the player left it on Automatic
            // the session still says nothing, and LocationStep then falls through to a random town
            // of the culture: granted Goldgrass, spawned at Mormont Keep. Every later step that
            // anchors to the holding reads it from here.
            session.SelectedSettlement = town;

            var dispossessed = new HashSet<Kingdom>();

            void RecordOwner(Settlement settlement)
            {
                var owner = settlement.OwnerClan?.Kingdom;
                if (owner != null) dispossessed.Add(owner);
            }

            RecordOwner(town);
            // Read before the grant, not after. Afterward the capital belongs to the player's clan,
            // whose kingdom does not exist yet, so asking then answers null and the same-realm rule
            // below never fires: every castle came nearest-first from wherever, and a Claimant then
            // declared war on each realm he had taken one from.
            var dispossessedRealm = town.OwnerClan?.Kingdom;
            ChangeOwnerOfSettlementAction.ApplyByKingDecision(hero, town);
            CSLogger.Info($"MonarchScenario: granted capital {town.Name} ({founding}).");

            var plan = culturedStart ? PublishedCrown(context) : PlanCrown(context, hero);
            CSLogger.Info($"MonarchScenario: a crown of standing {plan.Standing} takes {plan.Total} castles " +
                          $"({plan.Demesne} kept by the crown, {plan.Seats} as seats for the sworn houses).");

            var seized = SeizeCastles(context, hero, town, dispossessedRealm, plan.Total, RecordOwner);

            // The nearest castles are the crown's own core and the rest are the seats, so the
            // demesne stays contiguous with the seat of the realm and the houses ring it.
            int kept = Math.Min(plan.Demesne, seized.Count);

            // "Lands given" is what that answer is titled, and the castles were seized
            // to pay for the oaths, so a map that cannot spare enough of them takes the
            // shortfall out of the crown's own demesne before any sworn house is seated
            // landless. The crown never falls below the holdings the running game
            // demands of a realm, which is the one thing it cannot trade away.
            if (!culturedStart && session.GrantLandsToVassals && seized.Count < plan.Total)
            {
                int floor = Math.Min(kept, Math.Max(0, Math.Max(1, HoldingsACrownNeeds()) - 1));
                int forTheHouses = Math.Max(0, session.VassalClanCount);
                kept = Math.Max(floor, seized.Count - forTheHouses);
                CSLogger.Info($"MonarchScenario: the map spared {seized.Count} of {plan.Total} castles, " +
                              $"so the crown keeps {kept} and the rest seat the houses that knelt.");
            }

            var seatCastles = seized.Skip(kept).ToList();

            // The player's own words win; then the clan template for the player who
            // asked for it by name; then a name composed from the seat, the culture
            // and the founding. Whichever wins, the realm is given the full set the
            // base game gives its own kingdoms: a bare name, an informal form, a
            // fuller title and what this culture calls its monarch.
            bool namedDuringCreation = !string.IsNullOrWhiteSpace(session.KingdomName);
            if (culturedStart)
            {
                FoundAsPublished(context, town, founding, dispossessed, seatCastles, namedDuringCreation);
                return;
            }

            var composed = session.KingdomNameStyle == KingdomNameStyle.Clan
                ? null
                : KingdomNameGenerator.Preview(session);

            string? settled = namedDuringCreation
                ? session.KingdomName
                : session.KingdomNameStyle == KingdomNameStyle.Clan
                    ? ClanRealmName(hero)
                    : composed;
            var realm = KingdomNameGenerator.BuildRealm(session, settled)
                        ?? KingdomNameGenerator.BuildRealm(session, ClanRealmName(hero));

            var kingdomName = new TextObject("{=!}" + (realm?.Name ?? ClanRealmName(hero)));
            var informalName = new TextObject("{=!}" + (realm?.Informal ?? kingdomName.ToString()));
            var kingdomTitle = new TextObject("{=!}" + (realm?.Title ?? kingdomName.ToString()));
            CSLogger.Info($"MonarchScenario: realm named '{kingdomName}' " +
                          $"(informal '{informalName}', title '{kingdomTitle}', " +
                          $"ruler '{realm?.RulerTitle ?? "unset"}').");

            // v1.5.0 added a required formal name between the founder clan and the optional
            // tail, so the call is shaped to whichever overload this game has.
            var rulerTitleText = realm != null ? new TextObject("{=!}" + realm.RulerTitle) : null;
            var realmHistory = RealmLore.Compose(session, hero, realm?.Name);
            bool rulerTitleBound = GameCompat.CreateKingdom(
                Campaign.Current.KingdomManager, kingdomName, informalName, kingdomTitle,
                hero.Culture, hero.Clan, realmHistory, rulerTitleText);

            var newKingdom = hero.Clan.Kingdom;
            CSLogger.Info($"MonarchScenario: kingdom created ({newKingdom?.Name}).");

            // Remembered whatever its source, so no later founding is offered it again
            KingdomNameGenerator.Remember(newKingdom?.Name?.ToString() ?? kingdomName.ToString());

            // Only where the founding call had no parameter for it, so nothing is set twice
            if (newKingdom != null && rulerTitleText != null && !rulerTitleBound)
                GameCompat.SetRulerTitle(newKingdom, rulerTitleText);

            if (newKingdom != null)
            {
                ApplyPolicies(context, newKingdom);
                CreateVassals(context, newKingdom, town, seatCastles);
                ApplyWars(context, newKingdom, founding, dispossessed);

                int crownHolds = hero.Clan?.Settlements?.Count(s => s.IsTown || s.IsCastle) ?? 0;
                int realmHolds = Settlement.All.Count(s => (s.IsTown || s.IsCastle) &&
                                                           s.OwnerClan?.Kingdom == newKingdom);
                CSLogger.Info($"MonarchScenario: the crown holds {crownHolds} of the realm's {realmHolds}, " +
                              $"with {session.VassalClanCount} sworn houses, taken from " +
                              $"{dispossessed.Count} realm(s).");
            }
            else
            {
                // Founding is the whole point of this start. Failing it silently handed the player a
                // town and no crown, with the policies, sworn houses and wars simply absent.
                context.Report.AddProblem(
                    "Monarch: the kingdom could not be founded; policies, sworn houses and wars were skipped");
            }

            // Post-spawn prompt only when creation never settled the question and
            // there was nothing to compose a name from either
            if (newKingdom != null && !namedDuringCreation && composed == null &&
                !session.KingdomNameDecided)
                PromptForKingdomName(newKingdom);
        }

        /// <summary>
        ///     Cultured Start's crown: the castle count the settings name, two where
        ///     they leave it to the founding, every one of them a seat the sworn houses
        ///     take in turn and the crown keeps whatever no house was given. The castles
        ///     themselves are still seized under the rules that keep a realm from being
        ///     erased and a claimant from warring on realms he never touched.
        /// </summary>
        private static CrownPlan PublishedCrown(StartContext context)
        {
            const int PublishedCastleCount = 2;

            int setting = context.Settings?.MonarchCastleCount ?? -1;
            int castles = setting < 0 ? PublishedCastleCount : setting;
            return new CrownPlan(Math.Max(0, context.Session.EffectiveClanTier), 0, castles);
        }

        /// <summary>
        ///     Cultured Start's founding: the name the kingdom name chapter settled, or
        ///     the clan's own, with the game asking once the map opens when the chapter
        ///     was never answered.
        /// </summary>
        private static void FoundAsPublished(StartContext context, Settlement capital, MonarchFounding founding,
            HashSet<Kingdom> dispossessed, List<Settlement> seats, bool namedDuringCreation)
        {
            var hero = context.Hero;
            var session = context.Session;

            var kingdomName = namedDuringCreation
                ? new TextObject("{=!}" + session.KingdomName)
                : new TextObject("{=!}" + ClanRealmName(hero));

            GameCompat.CreateKingdom(
                Campaign.Current.KingdomManager, kingdomName, kingdomName, kingdomName,
                hero.Culture, hero.Clan, null, null);

            var newKingdom = hero.Clan.Kingdom;
            CSLogger.Info($"MonarchScenario: kingdom created ({newKingdom?.Name}).");

            if (newKingdom != null)
            {
                ApplyPolicies(context, newKingdom);
                CreateVassals(context, newKingdom, capital, seats, reportLandlessHouses: false);
                ApplyWars(context, newKingdom, founding, dispossessed);
            }
            else
            {
                context.Report.AddProblem(
                    "Monarch: the kingdom could not be founded; policies, sworn houses and wars were skipped");
            }

            if (newKingdom != null && !namedDuringCreation && !session.KingdomNameDecided)
                PromptForKingdomName(newKingdom);
        }

        /// <summary>
        ///     Seats the player on the throne of a realm that already exists.
        ///
        ///     This is what the base game itself does for its own King start, read out of
        ///     v1.5.2's <c>CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsRuler</c>
        ///     rather than invented: the ruling clan and the clan's kingdom are written
        ///     directly, and the realm's previous ruler is left exactly where they stand.
        ///     They are not killed, not demoted and not absorbed; their clan keeps its
        ///     heroes and its fiefs and becomes an ordinary vassal house under the new
        ///     crown. <c>ChangeRulingClanAction</c> would do the same and raise
        ///     <c>RulingClanChanged</c> as well, which the game deliberately does not do
        ///     at the moment character creation ends, because nothing listening to it has
        ///     started yet.
        ///
        ///     One thing the base game skips is put back. It never adjusts the joining
        ///     clan's war and peace stances, which its own vassal and mercenary starts
        ///     both do, so a player seated this way can be the ruler of a realm whose
        ///     wars they are not in. The start report says the realm's wars come with the
        ///     crown, so they do.
        /// </summary>
        /// <summary>
        ///     Gives the realm its new ruler's colors, the way the game colors a realm it
        ///     founds: the realm's two colors are the ruling clan's, its banner colors follow
        ///     them, and every sworn house flies its banner in them. The four setters are
        ///     private, so they are written through reflection.
        /// </summary>
        private static void AdoptTheRulersColors(Kingdom kingdom, Clan ruler)
        {
            try
            {
                void Set(string property, uint value) =>
                    typeof(Kingdom).GetProperty(property)?.SetValue(kingdom, value);

                Set(nameof(Kingdom.Color), ruler.Color);
                Set(nameof(Kingdom.Color2), ruler.Color2);
                Set(nameof(Kingdom.PrimaryBannerColor), ruler.Color);
                Set(nameof(Kingdom.SecondaryBannerColor), ruler.Color2);

                foreach (var house in kingdom.Clans.ToList())
                {
                    if (house == ruler) continue;
                    house.Banner?.ChangePrimaryColor(kingdom.PrimaryBannerColor);
                    house.Banner?.ChangeIconColors(kingdom.SecondaryBannerColor);
                    foreach (var party in house.WarPartyComponents)
                        party.Party?.SetVisualAsDirty();
                }

                CSLogger.Info($"MonarchScenario: {kingdom.Name} takes the colors of {ruler.Name}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"MonarchScenario: recoloring {kingdom.Name} for its new ruler failed.", ex);
            }
        }

        private static void TakeTheThrone(StartContext context, Kingdom kingdom)
        {
            var hero = context.Hero;
            var session = context.Session;
            var clan = hero.Clan;
            if (clan == null)
            {
                context.Report.AddProblem("Monarch: the player has no clan to seat on the throne");
                return;
            }

            var deposed = kingdom.RulingClan;

            // Read before seating: once the clan rules, Clan.Banner answers with the
            // realm's banner instead of the clan's, and joining a realm recolors the
            // clan's own banner to the realm's colors
            string? ownBanner = clan.Banner?.Serialize();

            VersionedGameApi.ReleaseKingdomStay(clan);
            kingdom.RulingClan = clan;
            clan.Kingdom = kingdom;

            if (ownBanner != null)
            {
                var restored = new TaleWorlds.Core.Banner();
                restored.Deserialize(ownBanner);
                VersionedGameApi.SetBanner(clan, restored);
                VersionedGameApi.SetBanner(kingdom, new TaleWorlds.Core.Banner(restored));
            }

            AdoptTheRulersColors(kingdom, clan);
            // Fully qualified from the root: this mod has a Helpers namespace of
            // its own, and the game's is a sibling of it rather than inside it
            global::Helpers.FactionHelper.AdjustFactionStancesForClanJoiningKingdom(clan, kingdom);

            CSLogger.Info($"MonarchScenario: {hero.Name} takes the throne of {kingdom.Name}" +
                          (deposed != null && deposed != clan
                              ? $", which {deposed.Name} held and now serves under."
                              : "."));

            var seat = session.SelectedSettlement ?? SeatWithin(kingdom, hero);
            if (seat == null)
            {
                context.Report.AddProblem(
                    $"Monarch: {kingdom.Name} holds nothing the crown could be seated in");
                return;
            }

            session.SelectedSettlement = seat;
            GrantWithin(context, hero, seat);

            // The crown's own holdings beyond its seat come off the realm's own
            // map, never off a neighbor's: taking a castle from outside would
            // start the reign with a war the player never chose
            var plan = PlanCrown(context, hero);
            var demesne = Settlement.All
                .Where(s => s.IsCastle && s.OwnerClan != clan && s.OwnerClan?.Kingdom == kingdom)
                .OrderBy(s => VersionedGameApi.DistanceSquared(seat, s))
                .Take(Math.Max(0, plan.Demesne))
                .ToList();

            foreach (var castle in demesne)
                GrantWithin(context, hero, castle);

            CSLogger.Info($"MonarchScenario: the crown of {kingdom.Name} is seated at {seat.Name} " +
                          $"and holds {demesne.Count} castle(s) of its own.");

            // A realm that already exists already has its wars. Only a war the
            // player composed themselves is declared on top of them
            if (session.CustomWars != null)
                ApplyWars(context, kingdom, MonarchFounding.Settler, new HashSet<Kingdom>());
        }

        /// <summary>
        ///     Where a crown inherited sits: a town of the realm first, since a realm's
        ///     seat is a town, and any hall it holds rather than nothing.
        /// </summary>
        private static Settlement? SeatWithin(Kingdom kingdom, Hero hero)
        {
            var held = Settlement.All
                .Where(s => (s.IsTown || s.IsCastle) && s.OwnerClan?.Kingdom == kingdom)
                .ToList();
            if (held.Count == 0) return null;

            var towns = held.Where(s => s.IsTown).ToList();
            var pool = towns.Count > 0 ? towns : held;

            var ownCulture = pool.Where(s => s.Culture == hero.Culture).ToList();
            return CSRandom.Pick(ownCulture.Count > 0 ? ownCulture : pool);
        }

        /// <summary>
        ///     Moves a holding of the realm into the crown's own hand. Silent when it is
        ///     already there, which it is for anything the player's clan somehow holds.
        /// </summary>
        private static void GrantWithin(StartContext context, Hero hero, Settlement settlement)
        {
            if (settlement.OwnerClan == hero.Clan) return;

            try
            {
                ChangeOwnerOfSettlementAction.ApplyByKingDecision(hero, settlement);
                CSLogger.Info($"MonarchScenario: {settlement.Name} passes to the crown.");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem(
                    $"Monarch holding grant failed for {settlement.Name}: {ex.GetType().Name}");
                CSLogger.Error($"MonarchScenario: failed to grant {settlement.Name}.", ex);
            }
        }

        /// <summary>
        ///     How large a crown the answers add up to: the castles it keeps for itself, and the
        ///     castles it takes as seats for the houses that knelt.
        ///
        ///     A house is a seat whether or not the land went with the oath. That is what the
        ///     chapter says: the houses who took no land swore on castles that "remain crown
        ///     land", and until now no such castle was ever taken, so two landless houses cost
        ///     the map nothing and left the crown exactly the size it would have been with none.
        ///     Reading the answer this way is what lets one chapter move a realm from a single
        ///     town to a full kingdom, and it moves the crown's OWN holdings with it, because a
        ///     landless house's seat stays in the crown's hand.
        /// </summary>
        private readonly struct CrownPlan
        {
            public CrownPlan(int standing, int demesne, int seats)
            {
                Standing = standing;
                Demesne = demesne;
                Seats = seats;
            }

            public int Standing { get; }

            /// <summary>Castles the crown keeps, beyond the capital.</summary>
            public int Demesne { get; }

            /// <summary>Castles taken as seats, held by the houses or by the crown.</summary>
            public int Seats { get; }

            public int Total => Demesne + Seats;
        }

        private static CrownPlan PlanCrown(StartContext context, Hero hero)
        {
            var session = context.Session;
            int standing = Math.Max(0, session.EffectiveClanTier);
            int houses = Math.Max(0, session.VassalClanCount);
            int demesne = DemesneCastles(hero, standing);

            int setting = context.Settings?.MonarchCastleCount ?? -1;
            if (setting < 0) return new CrownPlan(standing, demesne, houses);

            // The setting is a blunt total and still overrules the answers. The crown's own
            // holdings come out of it first, because a crown with no land of its own is a guest
            // in its own realm; whatever is left seats the houses.
            int total = Math.Max(0, setting);
            int kept = Math.Min(demesne, total);
            return new CrownPlan(standing, kept, total - kept);
        }

        /// <summary>
        ///     The crown's own castles, beyond the capital: one for every step of standing above
        ///     the standing at which the running game allows a kingdom to be founded at all,
        ///     since that is where a realm stops being an ambition; never more than the clan can
        ///     field parties to hold; and never so few that the realm falls short of what
        ///     founding one requires. All three numbers are asked of a live model.
        /// </summary>
        private static int DemesneCastles(Hero hero, int standing)
        {
            int steps = Math.Max(0, standing - StandingACrownNeeds());

            int canHold = PartiesAClanCanField(hero.Clan, standing);
            if (canHold > 0) steps = Math.Min(steps, canHold);

            // The capital covers the first of them
            int required = Math.Max(1, HoldingsACrownNeeds()) - 1;
            return Math.Max(required, steps);
        }

        /// <summary>The clan tier the running game demands before a kingdom may be founded.</summary>
        private static int StandingACrownNeeds()
        {
            try
            {
                return Math.Max(0,
                    Campaign.Current?.Models?.KingdomCreationModel?.MinimumClanTierToCreateKingdom ?? 4);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"MonarchScenario: reading the founding standing failed: {ex.Message}");
                return 4;
            }
        }

        /// <summary>
        ///     The holdings the running game demands before a kingdom may be founded, which is
        ///     also the floor under this one: a realm that holds nothing is not a realm.
        /// </summary>
        private static int HoldingsACrownNeeds()
        {
            try
            {
                return Math.Max(1,
                    Campaign.Current?.Models?.KingdomCreationModel
                        ?.MinimumNumberOfSettlementsOwnedToCreateKingdom ?? 1);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"MonarchScenario: reading the founding holdings failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        ///     What the clan tier model says a clan of this standing can put in the field, which
        ///     is the price it sets on how much that clan can hold. Zero when it cannot be read,
        ///     which the caller treats as no ceiling rather than as a ceiling of nothing.
        /// </summary>
        private static int PartiesAClanCanField(Clan? clan, int standing)
        {
            try
            {
                var model = Campaign.Current?.Models?.ClanTierModel;
                if (model == null || clan == null) return 0;
                return Math.Max(0, model.GetPartyLimitForTier(clan, standing));
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"MonarchScenario: reading what the clan can field failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        ///     The castles the crown takes, nearest first, out of the realm the capital came from
        ///     before anywhere else.
        ///
        ///     Two rules bind this and neither is traded for size. The dispossessed realm is
        ///     drained first because a Claimant declares war on every realm he took from, and a
        ///     contiguous block out of that one realm keeps the war count at the single war the
        ///     player actually provoked. And no realm is taken down to nothing: a realm stripped
        ///     bare is destroyed by the game's own kingdom logic within days, so a large crown
        ///     founded by erasing a kingdom would change the map in a way the player never chose
        ///     and could never see the cause of. The realm the capital came from is counted after
        ///     that loss, so its last castle is as protected as anyone else's.
        /// </summary>
        private static List<Settlement> SeizeCastles(StartContext context, Hero hero, Settlement capital,
            Kingdom? dispossessedRealm, int wanted, Action<Settlement> recordOwner)
        {
            var taken = new List<Settlement>();
            if (wanted <= 0) return taken;

            try
            {
                var held = new Dictionary<Kingdom, int>();
                foreach (var settlement in Settlement.All)
                {
                    if (!settlement.IsTown && !settlement.IsCastle) continue;
                    var realm = settlement.OwnerClan?.Kingdom;
                    if (realm == null) continue;
                    held.TryGetValue(realm, out int count);
                    held[realm] = count + 1;
                }

                bool NotOurs(Settlement s) => s.IsCastle && s.OwnerClan != hero.Clan;

                var sameRealm = dispossessedRealm == null
                    ? Enumerable.Empty<Settlement>()
                    : Settlement.All
                        .Where(s => NotOurs(s) && s.OwnerClan?.Kingdom == dispossessedRealm)
                        .OrderBy(s => VersionedGameApi.DistanceSquared(capital, s));

                var elsewhere = Settlement.All
                    .Where(s => NotOurs(s) &&
                                (dispossessedRealm == null || s.OwnerClan?.Kingdom != dispossessedRealm))
                    .OrderBy(s => VersionedGameApi.DistanceSquared(capital, s));

                foreach (var castle in sameRealm.Concat(elsewhere))
                {
                    if (taken.Count >= wanted) break;

                    var realm = castle.OwnerClan?.Kingdom;
                    if (realm != null && (!held.TryGetValue(realm, out int remaining) || remaining <= 1))
                    {
                        CSLogger.Info($"MonarchScenario: {castle.Name} left alone; it is all {realm.Name} holds.");
                        continue;
                    }

                    try
                    {
                        recordOwner(castle);
                        ChangeOwnerOfSettlementAction.ApplyByKingDecision(hero, castle);
                        taken.Add(castle);
                        if (realm != null) held[realm] = held[realm] - 1;
                        CSLogger.Info($"MonarchScenario: granted castle {castle.Name}.");
                    }
                    catch (Exception ex)
                    {
                        context.Report.AddProblem(
                            $"Monarch castle grant failed for {castle.Name}: {ex.GetType().Name}");
                        CSLogger.Error($"MonarchScenario: failed to grant castle {castle.Name}.", ex);
                    }
                }

                if (taken.Count < wanted)
                    CSLogger.Info($"MonarchScenario: {taken.Count} of {wanted} castles taken; " +
                                  "the map had no more to spare.");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Monarch castle seizure failed: {ex.GetType().Name}");
                CSLogger.Error("MonarchScenario: seizing castles failed.", ex);
            }

            return taken;
        }

        /// <summary>
        ///     The last resort, and the explicit choice of the player who asked for
        ///     the realm to carry the clan's name.
        /// </summary>
        private static string ClanRealmName(Hero hero)
        {
            var name = new TextObject("{=CSR_KingdomNameTemplate}{CLAN_NAME} Kingdom");
            name.SetTextVariable("CLAN_NAME", hero.Clan?.Name?.ToString() ?? string.Empty);
            return name.ToString();
        }

        /// <summary>The chosen initial policies, straight onto the new kingdom.</summary>
        private static void ApplyPolicies(StartContext context, Kingdom kingdom)
        {
            foreach (var policyId in context.Session.SelectedPolicies)
            {
                try
                {
                    var policy = TaleWorlds.ObjectSystem.MBObjectManager.Instance
                        .GetObject<PolicyObject>(policyId);
                    if (policy == null || kingdom.ActivePolicies.Contains(policy)) continue;
                    kingdom.AddPolicy(policy);
                    CSLogger.Info($"MonarchScenario: policy {policy.Name} enacted.");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"Policy enactment failed: {ex.GetType().Name}");
                    CSLogger.Error($"MonarchScenario: policy {policyId} failed.", ex);
                }
            }
        }

        /// <summary>
        ///     The founding vassal clans, each a fully outfitted lord with a party. A seat was
        ///     taken for every house either way; this decides whether the house holds it or the
        ///     crown does, and a house holding nothing is seated at the capital.
        /// </summary>
        private static void CreateVassals(StartContext context, Kingdom kingdom, Settlement capital,
            List<Settlement> seats, bool reportLandlessHouses = true)
        {
            var session = context.Session;
            int count = Math.Max(0, session.VassalClanCount);
            if (count == 0) return;

            int castleIndex = 0;
            for (int index = 0; index < count; index++)
            {
                bool grantCastle = session.GrantLandsToVassals && castleIndex < seats.Count;
                var home = grantCastle ? seats[castleIndex] : capital;

                // The answer promised a castle to every house that knelt; the map
                // could not spare one, and a promise the pipeline cannot keep is
                // said out loud rather than left as a house sitting at the capital.
                // Cultured Start seats its houses on the castles the settings grant and
                // the rest at the capital, and has always done so without a word
                if (reportLandlessHouses && session.GrantLandsToVassals && !grantCastle)
                    context.Report.AddProblem(
                        "Monarch: the map had no castle left for a sworn house, " +
                        "so it is seated at your capital with no land of its own");

                var leader = VassalGenerator.CreateVassalClan(kingdom, home, index, session, context.Settings);
                if (leader == null)
                {
                    context.Report.AddProblem("Monarch: a vassal clan could not be created");
                    continue;
                }

                if (grantCastle)
                {
                    try
                    {
                        ChangeOwnerOfSettlementAction.ApplyByKingDecision(leader, seats[castleIndex]);
                        CSLogger.Info(
                            $"MonarchScenario: {seats[castleIndex].Name} granted to {leader.Clan?.Name}.");
                        castleIndex++;
                    }
                    catch (Exception ex)
                    {
                        context.Report.AddProblem($"Vassal land grant failed: {ex.GetType().Name}");
                        CSLogger.Error("MonarchScenario: vassal land grant failed.", ex);
                    }
                }
            }
        }

        /// <summary>
        ///     Composed wars override the founding default: null follows the
        ///     founding story, an empty list means peace, and each chosen realm
        ///     otherwise declares against the new kingdom.
        /// </summary>
        private static void ApplyWars(StartContext context, Kingdom kingdom, MonarchFounding founding,
            HashSet<Kingdom> dispossessed)
        {
            IEnumerable<Kingdom> enemies;
            if (context.Session.CustomWars == null)
            {
                if (founding != MonarchFounding.Claimant) return;
                enemies = dispossessed;
            }
            else
            {
                enemies = context.Session.CustomWars
                    .Select(id => Kingdom.All.FirstOrDefault(k => k.StringId == id))
                    .Where(k => k != null && k != kingdom)
                    .Cast<Kingdom>();
            }

            foreach (var enemy in enemies)
            {
                try
                {
                    DeclareWarAction.ApplyByDefault(enemy, kingdom);
                    CSLogger.Info($"MonarchScenario: {enemy.Name} at war with the new kingdom.");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"War declaration failed for {enemy.Name}: {ex.GetType().Name}");
                    CSLogger.Error($"MonarchScenario: war declaration failed for {enemy.Name}.", ex);
                }
            }
        }

        /// <summary>
        ///     A culture town from the weakest realm holding one, so a Settler's
        ///     grant plausibly comes from land nobody could defend.
        /// </summary>
        /// <summary>
        ///     True while taking this settlement would leave its realm with nothing. A settler takes
        ///     land from someone who could not hold it, not the last acre of a kingdom: a realm
        ///     stripped bare is destroyed by the game's own kingdom logic within days, so the player
        ///     founds his kingdom by erasing one and never learns why the map changed.
        ///
        ///     Rare in the base game, where realms hold many fiefs. Under a total conversion it is
        ///     ordinary: Realm of Thrones has three realms holding a single settlement each, and for
        ///     three of its cultures that settlement is the only town of the culture, so it was the
        ///     guaranteed pick.
        /// </summary>
        private static bool IsLastHoldingOfRealm(Settlement settlement)
        {
            var realm = settlement.OwnerClan?.Kingdom;
            if (realm == null) return false;

            return !Settlement.All.Any(s => s != settlement && (s.IsTown || s.IsCastle) &&
                                            s.OwnerClan?.Kingdom == realm);
        }

        private static Settlement? PickSettlerTown(Hero hero)
        {
            var cultureTowns = Settlement.All
                .Where(s => s.IsTown && s.Culture == hero.Culture)
                .ToList();
            if (cultureTowns.Count == 0)
                return SettlementFinder.RandomCultureTown(hero.Culture);

            // Yields rather than stranding: if every candidate is its realm's last holding, the
            // original list stands, because handing the player no town at all is worse.
            var sparable = cultureTowns.Where(s => !IsLastHoldingOfRealm(s)).ToList();
            if (sparable.Count > 0) cultureTowns = sparable;

            var weakestKingdom = cultureTowns
                .Select(s => s.OwnerClan?.Kingdom)
                .Where(k => k != null)
                .Distinct()
                .OrderBy(k => VersionedGameApi.Strength(k!))
                .FirstOrDefault();

            if (weakestKingdom == null)
                return CSRandom.Pick(cultureTowns);

            var weakestTowns = cultureTowns
                .Where(s => s.OwnerClan?.Kingdom == weakestKingdom)
                .ToList();
            return CSRandom.Pick(weakestTowns) ?? CSRandom.Pick(cultureTowns);
        }

        private static void PromptForKingdomName(Kingdom kingdom)
        {
            try
            {
                CulturedStartReloaded.CharacterCreation.Editor.EditorPopups.ShowPrompt(
                    new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(),
                    new TextObject("{=CSR_KingdomName_Desc}Choose the name your realm will carry into history.").ToString(),
                    null,
                    new TextObject("{=CSR_KingdomName_Confirm}Proclaim").ToString(),
                    new TextObject("{=CSR_KingdomName_Cancel}Keep Current").ToString(),
                    input =>
                    {
                        try
                        {
                            var cleaned = (input ?? string.Empty).Replace("{", "").Replace("}", "").Trim();
                            if (cleaned.Length == 0) return;

                            var nameText = new TextObject("{=!}" + cleaned);
                            // v1.5.0 added a required title alongside the name and informal name
                            GameCompat.ChangeKingdomName(kingdom, nameText);
                            CSLogger.Info($"MonarchScenario: kingdom renamed to '{cleaned}'.");
                        }
                        catch (Exception ex)
                        {
                            CSLogger.Error("MonarchScenario: kingdom rename failed.", ex);
                        }
                    });
            }
            catch (Exception ex)
            {
                // Non-fatal: the templated name stands
                CSLogger.Warn($"MonarchScenario: could not show kingdom name inquiry: {ex.Message}");
            }
        }
    }
}
