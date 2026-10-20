using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Scenarios;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The chapter that puts a story on the water, and the only door to it.
    ///
    ///     It is a degree of a start rather than a start of its own: an outlaw who
    ///     raids from shallow hulls, a landed vassal seated on a castle with a
    ///     port, and a caravan master whose caravan sails. Which of the three is on
    ///     offer is decided by the start type the scenes arrived at, so the chapter
    ///     never asks the player to choose between them; it asks whether this life,
    ///     which has already been on the water, takes its story there.
    ///
    ///     Every figure in the panel comes from <see cref="SeaGrants"/>, which is
    ///     also what grants them, so the ship named here is the ship in the harbor.
    ///     Without War Sails <see cref="SeaGrants.Offered"/> answers None, the
    ///     chapter never joins the flow, and nothing is disabled or explained away.
    /// </summary>
    public static class FleetMenu
    {
        public static void AddFleetMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_fleet_menu",
                CreationFlow.DeclaredPrevious("cs_fleet_menu"),
                CreationFlow.DeclaredNext("cs_fleet_menu"),
                new TextObject("{=CSR_Fleet_Title}The Water"),
                new TextObject(
                    "{=CSR_Fleet_Desc}You have been out on it before, and it did not finish with you. Does your story go to sea, or does it keep to the roads?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs);

            Option(menu, "cs_fleet_raider", SeaDegree.Raider,
                "{=CSR_Fleet_Raider}Take to the Water",
                "{=CSR_Fleet_Raider_Desc}The roads are watched and the coast is not. You take what you take from the sea side, off hulls shallow enough to come ashore where no road runs.",
                RaiderEffect);

            Option(menu, "cs_fleet_admiral", SeaDegree.Admiral,
                "{=CSR_Fleet_Admiral}A Seat on the Water",
                "{=CSR_Fleet_Admiral_Desc}The holding your service earned you sits on the water, a harbor under its walls, and the hulls tied up in it answer to whoever sits there.",
                AdmiralEffect);

            Option(menu, "cs_fleet_venturer", SeaDegree.Venturer,
                "{=CSR_Fleet_Venturer}The Sea Caravan",
                "{=CSR_Fleet_Venturer_Desc}Wagons are for people who have never counted what an axle costs. Your goods go by water, out of a town whose whole business is the quay.",
                VenturerEffect);

            Ashore(menu);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One degree, shown only where it is the degree this start can really
        ///     be offered. The pick and the panel both go through
        ///     <see cref="SeaGrants"/>, so neither can name a ship the other does
        ///     not.
        /// </summary>
        private static void Option(NarrativeMenu menu, string id, SeaDegree degree,
            string titleKey, string descKey, Func<string> effect)
        {
            ChoiceEffects.Declare(id, effect);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                new TextObject(descKey),
                args => { },
                m => SeaGrants.Offered(CreationSession.Current) == degree,
                m =>
                {
                    SeaGrants.Choose(CreationSession.Current, degree);
                    MenuText.Remember("cs_fleet_menu", titleKey);
                },
                m => { }));
        }

        /// <summary>
        ///     The dry answer, which every start reaching this chapter may give. It
        ///     is the one that changes nothing, so its panel says what stands rather
        ///     than what was declined.
        /// </summary>
        private static void Ashore(NarrativeMenu menu)
        {
            ChoiceEffects.Declare("cs_fleet_ashore", AshoreEffect);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_fleet_ashore",
                new TextObject("{=CSR_Fleet_Ashore}Stay on Dry Land"),
                new TextObject(
                    "{=CSR_Fleet_Ashore_Desc}You have seen what the water does to people who trust it twice. Whatever you are going to be, you are going to be it standing up."),
                args => { },
                m => true,
                m =>
                {
                    SeaGrants.Choose(CreationSession.Current, SeaDegree.None);
                    MenuText.Remember("cs_fleet_menu", "{=CSR_Fleet_Ashore}Stay on Dry Land");
                },
                m => { }));
        }

        /// <summary>
        ///     What taking to the water comes to. The hulls are read rather than
        ///     checked: <see cref="SeaGrants.Offered"/> answers Raider only after
        ///     asking this same cached list for a hull, and it is that answer the
        ///     option's condition compares against, so a panel is never composed
        ///     for a degree whose ships do not exist. A guard here would be code
        ///     that cannot run, and the empty panel it returned would read as an
        ///     option that does nothing.
        /// </summary>
        private static string RaiderEffect()
        {
            var session = CreationSession.Current;
            var hulls = SeaGrants.Hulls(session, SeaDegree.Raider);

            var owned = new TextObject(
                "{=CSR_Panel_Fleet_RaiderShips}Ships: {LIST}, yours when the campaign opens, shallow enough to come ashore where no deep keel can follow");
            owned.SetTextVariable("LIST", Listed(hulls));

            return ChoiceEffects.Stated(
                owned.ToString(),
                Crewed(session, hulls,
                    "{=CSR_Panel_Fleet_RaiderCrew}Troops: {CREW} sea raiders aboard, the fewest those hulls need to leave harbor"),
                Cargo(hulls),
                new TextObject(
                    "{=CSR_Panel_Fleet_RaiderAfloat}Begins: afloat, off whatever coast is nearest the place your story starts you").ToString());
        }

        /// <inheritdoc cref="RaiderEffect"/>
        /// <remarks>
        ///     The seat is asserted for the same reason: Offered answers Admiral
        ///     only after asking for a port and getting one, off a cache keyed on
        ///     the culture and the realm, neither of which the narrative stage can
        ///     change under this chapter.
        /// </remarks>
        private static string AdmiralEffect()
        {
            var session = CreationSession.Current;
            var seat = SeaGrants.Seat(session, SeaDegree.Admiral)!;
            var hulls = SeaGrants.Hulls(session, SeaDegree.Admiral);

            // The seat is whatever the map can actually hand over. A castle with a
            // port is what it is asked for first, but a world that holds none is
            // answered with another port of the realm, and a line that named a
            // castle anyway would promise a keep the player is never given
            var seated = new TextObject(seat.IsCastle
                ? "{=CSR_Panel_Fleet_AdmiralSeat}Hall: {SEAT}, a castle with a port of its own, which settles the holding you are granted"
                : "{=CSR_Panel_Fleet_AdmiralPort}Hall: {SEAT}, a seat with a port of its own, which settles the holding you are granted");
            seated.SetTextVariable("SEAT", seat.Name);

            var quay = new TextObject(
                "{=CSR_Panel_Fleet_AdmiralShips}Ships: {LIST}, at its quay");
            quay.SetTextVariable("LIST", Listed(hulls));

            var afloat = new TextObject(
                "{=CSR_Panel_Fleet_AdmiralAfloat}Begins: afloat, off {SEAT}");
            afloat.SetTextVariable("SEAT", seat.Name);

            return ChoiceEffects.Stated(
                seated.ToString(),
                quay.ToString(),
                Crewed(session, hulls,
                    "{=CSR_Panel_Fleet_AdmiralCrew}Troops: {CREW} marines aboard, the fewest those hulls need to leave harbor"),
                Cargo(hulls),
                afloat.ToString());
        }

        /// <inheritdoc cref="AdmiralEffect"/>
        private static string VenturerEffect()
        {
            var session = CreationSession.Current;
            var town = SeaGrants.Seat(session, SeaDegree.Venturer)!;
            var hulls = SeaGrants.Hulls(session, SeaDegree.Venturer);

            var sailing = new TextObject(
                "{=CSR_Panel_Fleet_VenturerShips}Ships: {LIST}, what a sea caravan of your people sails with");
            sailing.SetTextVariable("LIST", Listed(hulls));

            var afloat = new TextObject(
                "{=CSR_Panel_Fleet_VenturerAfloat}Begins: afloat, off {TOWN}, a port town");
            afloat.SetTextVariable("TOWN", town.Name);

            return ChoiceEffects.Stated(
                sailing.ToString(),
                Crewed(session, hulls,
                    "{=CSR_Panel_Fleet_VenturerCrew}Troops: {CREW} of the caravan's own guards aboard, the fewest those hulls need to leave harbor"),
                new TextObject(
                    "{=CSR_Panel_Fleet_VenturerOwnParty}Troops: they ride in your own party, and no separate caravan is put on the map for you to lose").ToString(),
                Cargo(hulls),
                afloat.ToString(),
                new TextObject(
                    "{=CSR_Panel_Fleet_VenturerClosed}Closes: the chapter that asks where you begin").ToString());
        }

        /// <summary>
        ///     What staying ashore comes to, which is nothing, said as the thing
        ///     that stands rather than the thing refused. The caravan master is told
        ///     that the question of where they begin comes back, because this
        ///     chapter is the only reason it would not have.
        /// </summary>
        private static string AshoreEffect()
        {
            var session = CreationSession.Current;
            string dry = new TextObject(
                "{=CSR_Panel_Fleet_Ashore}Ships: none, and your story begins on land").ToString();

            if (SeaGrants.Offered(session) == SeaDegree.Venturer)
                return ChoiceEffects.Stated(dry, new TextObject(
                        "{=CSR_Panel_Fleet_AshoreWhere}Asks: where you begin, in a later chapter, as it would have been if the water had never come up")
                    .ToString());

            return dry;
        }

        /// <summary>
        ///     The hands and where they come from. The crew is counted into the
        ///     muster rather than added to it, because the muster runs after this
        ///     grant and clamps itself to the room the party limit leaves; saying
        ///     so is the difference between a promise and a surprise.
        /// </summary>
        private static string Crewed(CharacterCreationSession session,
            IReadOnlyList<ShipHull> hulls, string key)
        {
            var crew = new TextObject(key);
            crew.SetTextVariable("CREW", SeaGrants.Crew(session, hulls));

            var counted = new TextObject(
                "{=CSR_Panel_Fleet_Counted}{CREWLINE}, counted into the muster your means bought rather than added on top of it");
            counted.SetTextVariable("CREWLINE", crew.ToString());

            return counted.ToString();
        }

        /// <summary>
        ///     What the hulls carry, with the condition the game puts on it. The
        ///     capacity is added only while the party is at sea, so the figure on
        ///     its own is false on land.
        /// </summary>
        private static string Cargo(IReadOnlyList<ShipHull> hulls)
        {
            var cargo = new TextObject(
                "{=CSR_Panel_Fleet_Cargo}Ships: +{CARGO} to what your party can carry while you are at sea, and nothing on land");
            cargo.SetTextVariable("CARGO", hulls.Sum(hull => Math.Max(0, hull.InventoryCapacity)));
            return cargo.ToString();
        }

        /// <summary>
        ///     The hulls by name and count, so the player reads the ships they will
        ///     find in the harbor rather than a total.
        /// </summary>
        private static string Listed(IReadOnlyList<ShipHull> hulls)
        {
            var named = new List<string>();

            foreach (var group in hulls.GroupBy(hull => hull.StringId))
            {
                var one = new TextObject("{=CSR_Panel_Fleet_Hull}{COUNT} x {HULL}");
                one.SetTextVariable("COUNT", group.Count());
                one.SetTextVariable("HULL", group.First().Name ?? new TextObject(group.Key));
                named.Add(one.ToString());
            }

            return string.Join(", ", named);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
