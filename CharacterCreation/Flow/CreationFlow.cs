using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Helpers;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Settings;

namespace CulturedStartReloaded.CharacterCreation.Flow
{
    /// <summary>
    ///     The character creation menu flow as one ordered table. Each node carries
    ///     an inclusion predicate over the session; next/previous walk to the
    ///     adjacent included node. The menus' declared neighbor ids are generated
    ///     from the same table, so they cannot drift from the real flow.
    /// </summary>
    public static class CreationFlow
    {
        private sealed class Node
        {
            private readonly Func<CharacterCreationSession, bool> _include;

            public Node(string id, Func<CharacterCreationSession, bool>? include = null,
                bool hideable = true, bool scene = false)
            {
                Id = id;
                _include = include ?? (_ => true);
                Hideable = hideable;
                IsScene = scene;
            }

            public string Id { get; }

            /// <summary>
            ///     False for the menus that carry the flow itself: the route choice, the realm and
            ///     holding pickers that the start types needing them cannot do without, and the
            ///     standing the scenes arrived at, which is the one place the guided route tells
            ///     the player what their life came to. Hiding one of those does not shorten the
            ///     flow, it breaks it.
            /// </summary>
            public bool Hideable { get; }

            /// <summary>
            ///     A guided-route scene, which additionally has to be one this life
            ///     reaches. The node asks that itself because only the node knows
            ///     its own id, and the question is asked per walk rather than once,
            ///     since an answer changes what comes after it and a scene decided
            ///     in advance would be the wrong scene.
            /// </summary>
            public bool IsScene { get; }

            /// <summary>
            ///     Whether this menu is part of the session's route AND the player has not switched
            ///     it off. One place, so every walk of the flow agrees: asking the settings inside
            ///     each declared predicate would have meant thirty-two chances to forget.
            /// </summary>
            public bool Applies(CharacterCreationSession session)
            {
                if (Hideable && !CSSettings.ShowsMenu(Id)) return false;
                if (IsScene && !Menus.SceneMenus.Appears(Id)) return false;
                return _include(session);
            }
        }

        private static bool UsesKingdom(CharacterCreationSession s) =>
            s.SelectedStartType is StartType.LandedVassal or StartType.LandlessVassal or StartType.Mercenary
                or StartType.Outlaw or StartType.RebelClan;

        /// <summary>
        ///     The starts that hold a fief by right and are therefore asked how it is
        ///     manned. An outlawed lord is not one of them: nothing applies the garrison
        ///     he would choose, so asking him would be a chapter with no effect.
        /// </summary>
        private static bool UsesSettlement(CharacterCreationSession s) =>
            s.SelectedStartType is StartType.Monarch or StartType.LandedVassal or StartType.RebelClan;

        /// <summary>
        ///     The starts that answer WHERE, which is a wider set than the starts that hold
        ///     by right. An outlaw is asked because "outlawed lord" is a man with a hall and
        ///     a price on his head, and the answer he is offered is a hall or the road.
        /// </summary>
        private static bool PicksHolding(CharacterCreationSession s) =>
            UsesSettlement(s) || s.SelectedStartType == StartType.Outlaw;

        /// <summary>
        ///     Whether this life and this start can be offered the water at all.
        ///     Asked of the same service that grants the ships, so a chapter is put
        ///     only where there is a hull, a crew and, where the degree needs one, a
        ///     port behind it. Without War Sails the answer is None and the chapter
        ///     is simply not in the flow.
        /// </summary>
        private static bool GoesToSea(CharacterCreationSession s) =>
            Services.Application.Scenarios.SeaGrants.Offered(s)
                != Services.Application.Scenarios.SeaDegree.None;

        private static bool UsesLocation(CharacterCreationSession s) =>
            s.SelectedStartType is StartType.Commoner or StartType.LandlessVassal or StartType.Mercenary ||
            // A caravan master who sent the goods by water has answered this: the
            // sea chapter seats them at a named port town and the panel said so, so
            // asking again would offer a choice that chapter has already made
            (s.SelectedStartType == StartType.CaravanMaster &&
             s.SelectedSeaDegree != Services.Application.Scenarios.SeaDegree.Venturer) ||
            // An outlaw who took a hall has answered this already. LocationStep starts the
            // party at the holding ahead of anything this chapter writes, so asking again
            // would offer him a choice the start then overrules.
            (s.SelectedStartType == StartType.Outlaw && s.SelectedSettlement == null);

        /// <summary>
        ///     Where Cultured Start asks where the road starts, exactly as that route
        ///     always has: every start that is not handed a holding by right.
        /// </summary>
        private static bool UsesLocationAsPublished(CharacterCreationSession s) =>
            s.SelectedStartType is StartType.Commoner or StartType.LandlessVassal or StartType.Mercenary
                or StartType.Outlaw or StartType.CaravanMaster;

        /// <summary>Cultured Start Revamped: the thirteen scenes.</summary>
        private static bool Narrative(CharacterCreationSession s) => s.Mode == SetupMode.Narrative;

        /// <summary>Cultured Start: the seven life-path chapters.</summary>
        private static bool LifePath(CharacterCreationSession s) => s.Mode == SetupMode.LifePath;

        /// <summary>
        ///     Either guided route. The chapters below the life are asked by both of
        ///     them under one id, and each route answers that id with its own menu:
        ///     <c>CulturedStart/CulturedStartMenus</c> puts the route's menu in place
        ///     when the route is chosen, so a row here is a question both routes ask
        ///     and never a promise that they ask it the same way. Naming both is what
        ///     keeps this out of the third route: a node with no route named at all
        ///     would stand in the Start Editor's flow as well, and a walk from the
        ///     editor would reach it and then walk on into a guided chapter.
        /// </summary>
        private static bool Guided(CharacterCreationSession s) => Narrative(s) || LifePath(s);

        private static bool Custom(CharacterCreationSession s) => s.Mode == SetupMode.Custom;

        /// <summary>Whether this run has told any of its life, which is what an age can be moved from.</summary>
        private static bool Told(CharacterCreationSession s) => Services.Application.GuidedRun.WasWalkedBy(s);


        private static readonly Node[] Order =
        {
            // The first choice of the flow: guided narrative or full custom. Dropped
            // when the game's own starting-options screen already named the route.
            new("cs_mode_menu", s => !s.RouteDecidedExternally, hideable: false),
            new("cs_custom_menu", Custom, hideable: false),

            // Cultured Start: the seven life-path chapters, in the order a life is
            // told. War Sails decides how long a chapter's list of answers is, never
            // whether the chapter is asked
            new("cs_family_menu", LifePath),
            new("cs_childhood_menu", LifePath),
            new("cs_education_menu", LifePath),
            new("cs_youth_menu", LifePath),
            new("cs_turning_menu", LifePath),
            new("cs_reason_menu", LifePath),
            new("cs_age_menu", LifePath),

            // Cultured Start Revamped: thirteen scenes from the house you were small in
            // to the name people use for you now. A scene is skipped where an
            // earlier answer ruled its situation out of this life, which is a
            // different thing from the player switching its stage off
            new("cs_scene_what_the_house_had", Narrative, scene: true),
            new("cs_scene_the_one_always_in_the_wrong", Narrative, scene: true),
            new("cs_scene_the_store_burned", Narrative, scene: true),
            new("cs_scene_what_they_called_you_then", Narrative, scene: true),
            new("cs_scene_the_man_who_taught_you", Narrative, scene: true),
            new("cs_scene_what_you_could_do", Narrative, scene: true),
            new("cs_scene_what_the_yard_said", Narrative, scene: true),
            new("cs_scene_the_verdict", Narrative, scene: true),
            new("cs_scene_what_the_town_said", Narrative, scene: true),
            new("cs_scene_the_seat", Narrative, scene: true),
            new("cs_scene_the_purse", Narrative, scene: true),
            new("cs_scene_the_winter_between", Narrative, scene: true),
            new("cs_scene_the_name_they_use", Narrative, scene: true),
            new("cs_story_progress_menu", s => Guided(s) && CSGameModeService.IsStoryMode()),
            // What the scenes came to, read back before anything acts on it. The
            // station is not asked here and is not asked anywhere on this route:
            // the answers decided it, and this is where the player is told. It
            // stands where the start-type pick used to, which is ahead of every
            // chapter below that branches on the station, so nobody is asked to
            // name a realm before learning their life ended up holding one
            new("cs_standing_menu", Narrative, hideable: false),

            // The years the life came to, kept or moved, once the player has been
            // told what the life came to and before anything is sized by the level
            new("cs_age_adjust_menu", s => Narrative(s) && Told(s), hideable: false),

            // Cultured Start asks the same question its own way: the player picks
            // the beginning their tale starts from, which is what that route has
            // always done. The two screens are exclusive, and this table is what
            // makes them so
            new("cs_scenario_select", LifePath, hideable: false),

            // Standing and means come before the chapters, because several of
            // them read the clan tier to decide what they can offer
            new("cs_means_menu", Guided),

            // Revamped tells the household in the order a person tells it: the two
            // who raised you, the ones raised beside you, then the house you made
            // yourself. Cultured Start asks the whole household in one chapter, the
            // middle one, whose id both routes share
            new("cs_household_parents_menu", Narrative),
            new("cs_household_menu", Guided),
            new("cs_household_hearth_menu", Narrative),

            // After the tier is settled, so the offer never exceeds what the
            // clan can actually hold
            new("cs_companion_select", Guided),

            // The warband count is bounded by what the clan can hold less the
            // companions and relatives riding along, so it is asked once both are
            // settled rather than in the means chapter before either exists
            new("cs_warband_menu", Guided),
            new("cs_kingdom_select", s => Guided(s) && UsesKingdom(s), hideable: false),

            // Scenario chapters: each start type's own story beats, all writing
            // the same session fields the Start Editor writes. Cultured Start asks a
            // vassal or a mercenary about the joined realm's wars; Revamped leaves
            // those wars as the age gave them and asks only the Monarch, at
            // cs_first_war_menu, because a crown founded today has no wars until it
            // makes them
            new("cs_realm_wars_menu", s => LifePath(s) && s.SelectedStartType
                is StartType.LandedVassal or StartType.LandlessVassal or StartType.Mercenary, hideable: false),
            new("cs_contract_menu", s => Guided(s) && s.SelectedStartType == StartType.Mercenary),
            new("cs_crime_menu", s => Guided(s) && s.SelectedStartType == StartType.Outlaw),
            new("cs_settlement_select",
                s => (LifePath(s) && UsesSettlement(s)) || (Narrative(s) && PicksHolding(s)),
                hideable: false),

            // The water comes between the holding and its garrison on purpose. A
            // seat on the water settles WHICH holding, and the garrison chapter
            // quotes that holding's own ceiling, so asking after it would leave the
            // player a garrison figure measured against a castle they no longer hold
            new("cs_fleet_menu", s => Narrative(s) && GoesToSea(s)),
            new("cs_fief_menu", s => Guided(s) && UsesSettlement(s)),
            new("cs_rising_menu", s => Guided(s) && s.SelectedStartType == StartType.RebelClan),
            new("cs_founding_menu", s => Guided(s) && s.SelectedStartType == StartType.Monarch),
            new("cs_kingdom_name_menu", s => Guided(s) && s.SelectedStartType == StartType.Monarch),
            new("cs_traditions_menu", s => Guided(s) && s.SelectedStartType == StartType.Monarch),
            new("cs_sworn_houses_menu", s => Guided(s) && s.SelectedStartType == StartType.Monarch),
            new("cs_first_war_menu", s => Guided(s) && s.SelectedStartType == StartType.Monarch),
            new("cs_location_menu",
                s => (LifePath(s) && UsesLocationAsPublished(s)) || (Narrative(s) && UsesLocation(s))),
            // Revamped asks a caravan master this too. WorkshopGrants already runs
            // for every start type and this chapter is the only thing that writes
            // the number it reads, so the chapter's own gate was the whole of what
            // kept a trader from owning the shop their caravan supplies
            new("cs_trade_menu",
                s => (LifePath(s) && s.SelectedStartType == StartType.Commoner) ||
                     (Narrative(s) && s.SelectedStartType is StartType.Commoner or StartType.CaravanMaster)),
            new("cs_ledger_menu", s => Guided(s) && s.SelectedStartType == StartType.CaravanMaster),
            new("cs_beasts_menu", s => Guided(s) && s.SelectedStartType == StartType.CaravanMaster),
            // The conversion's own calling, asked after the standing that bounds which callings
            // are open and before the gear, since a career decides what a fighting life carries
            new(Menus.CareerMenu.MenuId, s => Guided(s) && Services.TaomBridge.Careers().Count > 0),
            new("cs_gear_menu", Guided),

            // One written choice of loadout. Cultured Start adds the four slot
            // screens for the player who asked to fill them by hand; on Revamped
            // that is the Start Editor's job. Cultured Start's own chapters that
            // no switch was ever written for stay where that route put them
            new("cs_arms_menu", Guided),
            new("cs_weapon1_menu", s => LifePath(s) && s.ChooseWeaponsIndividually, hideable: false),
            new("cs_weapon2_menu", s => LifePath(s) && s.ChooseWeaponsIndividually, hideable: false),
            new("cs_weapon3_menu", s => LifePath(s) && s.ChooseWeaponsIndividually, hideable: false),
            new("cs_weapon4_menu", s => LifePath(s) && s.ChooseWeaponsIndividually, hideable: false),
            new("cs_provisions_menu", Guided),
            new("cs_stats_menu", LifePath, hideable: false),

            // Revamped asks for the names last, when every context they read is
            // settled, and neither can be switched off: a name decided for you is
            // not a choice. Cultured Start leaves both to the game's own screens
            new("cs_name_menu", Narrative, hideable: false),
            new("cs_clan_name_menu", Narrative, hideable: false),
            new("cs_epilogue_menu", Guided)
        };

        /// <summary>True while this menu is part of the session's current route.</summary>
        public static bool Includes(CharacterCreationSession session, string menuId)
        {
            int index = IndexOf(menuId);
            return index >= 0 && Order[index].Applies(session);
        }

        /// <summary>First included menu id for the session's route, or "" when it has none.</summary>
        public static string GetFirst(CharacterCreationSession session)
        {
            foreach (var node in Order)
                if (node.Applies(session))
                    return node.Id;

            return string.Empty;
        }

        /// <summary>Next included menu id, or "" at the end of the mod's flow.</summary>
        public static string GetNext(CharacterCreationSession session, string currentMenuId)
        {
            int index = IndexOf(currentMenuId);
            if (index < 0) return string.Empty;

            for (int i = index + 1; i < Order.Length; i++)
                if (Order[i].Applies(session))
                    return Order[i].Id;

            return string.Empty;
        }

        /// <summary>Previous included menu id, or "" at the start of the mod's flow.</summary>
        public static string GetPrevious(CharacterCreationSession session, string currentMenuId)
        {
            int index = IndexOf(currentMenuId);
            if (index < 0) return string.Empty;

            for (int i = index - 1; i >= 0; i--)
                if (Order[i].Applies(session))
                    return Order[i].Id;

            return string.Empty;
        }

        /// <summary>
        ///     Static neighbor for menu constructor decoration: the unconditional
        ///     linear chain. Runtime routing overrides this, but an unpatched game
        ///     still walks a sane linear path.
        ///
        ///     Being unconditional is what makes it wrong for a route: the row before
        ///     a route's first menu belongs to another route, so the id declared here
        ///     for the guided route's first scene is the Start Editor's own menu. A
        ///     patched game never follows it for one of this mod's menus, because
        ///     MenuRoutingPatch answers those itself in both directions rather than
        ///     letting the game read this. Its own answer comes from the walks above,
        ///     which can only return a node whose predicate holds for this session and
        ///     therefore can only stay inside the session's route.
        /// </summary>
        public static string DeclaredPrevious(string menuId)
        {
            int index = IndexOf(menuId);
            return index <= 0 ? "start" : Order[index - 1].Id;
        }

        /// <inheritdoc cref="DeclaredPrevious"/>
        public static string DeclaredNext(string menuId)
        {
            int index = IndexOf(menuId);
            if (index < 0 || index == Order.Length - 1) return string.Empty;
            return Order[index + 1].Id;
        }

        private static int IndexOf(string menuId)
        {
            for (int i = 0; i < Order.Length; i++)
                if (string.Equals(Order[i].Id, menuId, StringComparison.Ordinal))
                    return i;
            return -1;
        }
    }
}
