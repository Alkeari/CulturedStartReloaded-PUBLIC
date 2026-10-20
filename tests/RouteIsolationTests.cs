using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     Whether a route can be left by accident.
    ///
    ///     Four routes share one menu list, and which of them a menu belongs to is
    ///     written only as that menu's include predicate in the flow table. Back and
    ///     Next walk that table and so cannot leave a route. The game's own walk
    ///     cannot read the predicates at all: it follows the neighbor ids baked into
    ///     each menu at registration, which are the table's rows with the conditions
    ///     stripped off, so the row before a route's first menu belongs to whichever
    ///     route happens to sit above it. One press of Back that the mod declined to
    ///     answer therefore offered the Start Editor to a player who had chosen a
    ///     guided route, and a second press offered the route choice on a game whose
    ///     own starting-options screen had already made it.
    ///
    ///     Two routes now tell a life rather than one, which adds a way to get this
    ///     wrong that a three-route table did not have. The chapters below the life
    ///     are shared: they read the standing, the tier and the start type the life
    ///     came to, and both routes write those, so one node stands in both guided
    ///     routes and in neither of the other two. A node naming ONE of them is the
    ///     defect that shape invites, and it is silent, because the route that keeps
    ///     the node still walks correctly while the other loses the menus that name
    ///     the character and finish them.
    ///
    ///     Three things are held here, because any one alone lets something back in.
    ///     Every menu must name the routes it belongs to and no menu may stand in
    ///     every route at once; the shared tail must be shared, so neither guided
    ///     route can finish a character the other cannot; and the patch must answer
    ///     every Back and Next from a menu of this mod rather than handing one to the
    ///     condition-blind chain. Neither file compiles in this project, both naming
    ///     engine types, so both are read as text out of the files that declare them.
    /// </summary>
    public class RouteIsolationTests
    {
        private const string FlowFile = "CharacterCreation/Flow/CreationFlow.cs";
        private const string PatchFile = "Patches/MenuRoutingPatch.cs";
        private const string ModeMenuFile = "CharacterCreation/Menus/ModeMenu.cs";
        private const string Picker = "cs_mode_menu";

        /// <summary>One of the mod's creation paths, as a flow predicate names it.</summary>
        public enum Route
        {
            /// <summary>Cultured Start: the seven life-path chapters.</summary>
            LifePath,

            /// <summary>Cultured Start Revamped: the thirteen scenes.</summary>
            Narrative,

            /// <summary>The Start Editor.</summary>
            Custom
        }

        /// <summary>The three routes a player can be walking when the flow is walked.</summary>
        public static IEnumerable<object[]> EveryRouteAndRegime() =>
            from route in new[] { Route.LifePath, Route.Narrative, Route.Custom }
            from decidedExternally in new[] { false, true }
            select new object[] { route, decidedExternally };

        /// <summary>
        ///     One row of the flow table: its menu id, the routes its predicate names,
        ///     and whether it is the route choice itself.
        ///
        ///     The route choice belongs to no route and is offered to all of them, so
        ///     it is held apart rather than given a route of its own: a row that named
        ///     every route would be indistinguishable from a row that named none.
        /// </summary>
        private sealed record Node(string Id, IReadOnlyCollection<Route> Routes, bool IsChoice);

        /// <summary>
        ///     The menus that finish a character rather than tell their life, asked by
        ///     both guided routes. Both must reach every one of them: a route that
        ///     cannot pick the gear and cannot read the life back is not a route, and
        ///     nothing about the flow table makes that visible.
        /// </summary>
        private static readonly string[] SharedTail =
        {
            "cs_means_menu", "cs_household_menu", "cs_companion_select", "cs_warband_menu", "cs_gear_menu",
            "cs_arms_menu", "cs_provisions_menu", "cs_epilogue_menu"
        };

        /// <summary>
        ///     The menus only Cultured Start Revamped asks. Cultured Start asks the
        ///     household in one chapter and names the character and the clan on the
        ///     game's own screens, as it did when it shipped, so a walk of that route
        ///     reaching one of these would ask a question twice.
        /// </summary>
        private static readonly string[] RevampedTail =
        {
            "cs_household_parents_menu", "cs_household_hearth_menu", "cs_name_menu", "cs_clan_name_menu"
        };

        /// <summary>
        ///     The two screens that settle which beginning a character starts from,
        ///     and the route each of them belongs to.
        ///
        ///     They answer one question two different ways: Cultured Start asks the
        ///     player outright, Cultured Start Revamped reads it off the life and
        ///     tells them. A life that met both would be asked the same thing twice
        ///     and would answer it twice, so neither may stand in the other's route.
        /// </summary>
        private static readonly (string Id, Route Route)[] TheBeginningIsSettledBy =
        {
            ("cs_scenario_select", Route.LifePath),
            ("cs_standing_menu", Route.Narrative)
        };

        [Fact]
        public void Every_flow_menu_names_the_routes_it_belongs_to()
        {
            var order = Order();

            order.Should().HaveCountGreaterThan(30, "the flow table should still hold the whole flow");
            order.Count(node => node.IsChoice).Should().Be(1,
                "the route choice is one menu, and a second unconditioned on the route would be reachable from every route");
            order.Where(node => !node.IsChoice && node.Routes.Count == 0).Should().BeEmpty(
                "a node with no route in its predicate stands in every route, so a walk from one route " +
                "reaches it and then walks on into whichever route the next row belongs to");
        }

        [Fact]
        public void No_menu_stands_in_the_start_editor_and_in_a_guided_route_at_once()
        {
            var shared = Order()
                .Where(node => node.Routes.Contains(Route.Custom) && node.Routes.Count > 1)
                .Select(node => node.Id)
                .ToList();

            shared.Should().BeEmpty(
                "the Start Editor is one screen and a guided route is a walk through menus, so a menu " +
                "in both would be reached from the editor's own flow and would walk on into a chapter");
        }

        [Fact]
        public void Both_guided_routes_reach_every_menu_that_finishes_a_character()
        {
            var order = Order();

            foreach (string id in SharedTail)
            {
                var node = order.SingleOrDefault(row => row.Id == id);
                node.Should().NotBeNull("{0} should still be in the flow table", id);

                node!.Routes.Should().Contain(Route.LifePath,
                    "{0} finishes a character and Cultured Start has to reach it", id);
                node.Routes.Should().Contain(Route.Narrative,
                    "{0} finishes a character and Cultured Start Revamped has to reach it", id);
            }
        }

        [Fact]
        public void Only_cultured_start_revamped_reaches_its_own_household_and_naming_chapters()
        {
            var order = Order();

            foreach (string id in RevampedTail)
            {
                var node = order.SingleOrDefault(row => row.Id == id);
                node.Should().NotBeNull("{0} should still be in the flow table", id);

                node!.Routes.Should().Equal(new[] { Route.Narrative },
                    "{0} belongs to Cultured Start Revamped alone", id);
            }
        }

        [Fact]
        public void Each_route_settles_the_beginning_its_own_way_and_only_its_own_way()
        {
            var order = Order();

            foreach (var (id, owner) in TheBeginningIsSettledBy)
            {
                var node = order.SingleOrDefault(row => row.Id == id);
                node.Should().NotBeNull("{0} settles which beginning a character starts from", id);

                node!.Routes.Should().Equal(new[] { owner },
                    "{0} belongs to {1} alone: the other route settles the beginning its own way, " +
                    "and a life that met both screens would be asked the same question twice", id, owner);
            }
        }

        [Fact]
        public void Each_guided_route_has_menus_of_its_own()
        {
            var order = Order();

            foreach (var route in new[] { Route.LifePath, Route.Narrative })
                order.Where(node => node.Routes.Count == 1 && node.Routes.Contains(route))
                    .Should().HaveCountGreaterThan(5,
                        "{0} tells a life through chapters nothing else may reach", route);
        }

        [Fact]
        public void The_route_choice_is_reachable_exactly_when_no_starting_options_screen_chose_the_route()
        {
            var choice = Order().Single(node => node.IsChoice);

            choice.Id.Should().Be(Picker);
            Entry(choice.Id).Should().Contain("RouteDecidedExternally",
                "the route choice appears on the game version and game mode where the game's own " +
                "Advanced Starting Options did not run, which is what that flag records");

            foreach (var route in new[] { Route.LifePath, Route.Narrative, Route.Custom })
            {
                Included(choice, route, routeDecidedExternally: false).Should().BeTrue();
                Included(choice, route, routeDecidedExternally: true).Should().BeFalse();
            }
        }

        [Fact]
        public void The_route_choice_offers_vanilla_and_all_three_mod_routes()
        {
            string source = Read(ModeMenuFile);

            var options = Regex.Matches(source, @"new NarrativeMenuOption\(\s*""(cs_mode_[a-z_]+)""")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToList();

            options.Should().BeEquivalentTo(
                new[] { "cs_mode_vanilla", "cs_mode_lifepath", "cs_mode_narrative", "cs_mode_custom" },
                "the route choice is the only place the mod itself asks, so every route is on it, and " +
                "vanilla is one of them rather than what a player gets for leaving the screen");
            Regex.Matches(source, @"m => true").Count.Should().Be(4,
                "none of the four is ever withheld once this menu is shown at all");
        }

        /// <summary>
        ///     Each option writes a different route. Four buttons that set three
        ///     routes would read as four choices and behave as three, and the one a
        ///     player came for would be the one silently missing.
        /// </summary>
        [Fact]
        public void Each_option_of_the_route_choice_writes_a_route_of_its_own()
        {
            var written = Regex.Matches(Read(ModeMenuFile), @"Mode = SetupMode\.([A-Za-z]+)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToList();

            written.Should().BeEquivalentTo(new[] { "Vanilla", "LifePath", "Narrative", "Custom" });
            written.Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [MemberData(nameof(EveryRouteAndRegime))]
        public void Back_never_leaves_the_chosen_route_at_any_depth(Route route, bool routeDecidedExternally)
        {
            var included = Order().Where(node => Included(node, route, routeDecidedExternally)).ToList();

            included.Should().NotBeEmpty("every route has menus of its own");

            foreach (var node in included)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var at = node; at != null; at = Previous(included, at))
                {
                    seen.Add(at.Id).Should().BeTrue("the table is walked backwards and cannot loop");

                    at.Routes.Contains(route).Should().Be(!at.IsChoice,
                        "backing up from {0} reached {1}, which belongs to another route", node.Id, at.Id);

                    if (routeDecidedExternally)
                        at.IsChoice.Should().BeFalse(
                            "backing up from {0} reached the route choice, and with the route already " +
                            "chosen outside this mod that menu does not exist", node.Id);
                }
            }
        }

        [Theory]
        [MemberData(nameof(EveryRouteAndRegime))]
        public void The_first_menu_of_a_chosen_route_backs_out_of_nothing_but_the_route_choice(
            Route route, bool routeDecidedExternally)
        {
            var included = Order().Where(node => Included(node, route, routeDecidedExternally)).ToList();
            var first = included.First(node => !node.IsChoice);

            var previous = Previous(included, first);

            if (routeDecidedExternally)
                previous.Should().BeNull(
                    "with the route chosen on the game's own screen there is nothing before this route's " +
                    "first menu, so Back must leave the menu stage rather than find a neighbor");
            else
                previous!.Id.Should().Be(Picker,
                    "where this mod owns the route choice, Back from a route's first menu returns to it");
        }

        [Fact]
        public void Back_and_next_from_a_menu_of_this_mod_are_never_handed_to_the_game()
        {
            foreach (string member in new[] { "BackFromAModMenu", "ForwardFromAModMenu" })
                ModSource.MemberBody(PatchFile, "private static bool " + member).Should().NotContain("return true",
                    "{0} answers for one of this mod's menus, and letting the game answer instead means " +
                    "the menu's declared neighbor, which is the flow table's preceding row with none of " +
                    "its conditions applied and so belongs to whichever route sits beside it", member);
        }

        [Fact]
        public void Back_from_the_vanilla_route_offers_the_route_choice_only_where_this_mod_owns_it()
        {
            ModSource.MemberBody(PatchFile, "private static bool BackFromAVanillaMenu")
                .Should().Contain("RouteDecidedExternally",
                "a vanilla start named on the game's own starting-options screen has no route choice of " +
                "ours to return to, and offering one hands the player the routes they did not pick");
        }

        /// <summary>
        ///     Whether this node is in the flow for a session on the given route.
        ///     Hideable chapters and scenes drop out of it too, but only ever leaving
        ///     a route shorter: what is asserted above holds for every subset, since a
        ///     node's routes are fixed by its own predicate and dropping neighbors
        ///     cannot change them.
        /// </summary>
        private static bool Included(Node node, Route route, bool routeDecidedExternally) =>
            node.IsChoice ? !routeDecidedExternally : node.Routes.Contains(route);

        private static Node? Previous(IReadOnlyList<Node> included, Node node)
        {
            for (int at = 1; at < included.Count; at++)
                if (included[at].Id == node.Id)
                    return included[at - 1];

            return null;
        }

        /// <summary>The flow table, in order, as the id of each node and the routes it names.</summary>
        private static IReadOnlyList<Node> Order() =>
            Entries().Select(entry => new Node(IdOf(entry), RoutesOf(entry), IsChoice(entry))).ToList();

        private static string Entry(string menuId) => Entries().Single(entry => IdOf(entry) == menuId);

        /// <summary>
        ///     One table entry per element, each closed by counting parentheses from its own
        ///     <c>new(</c>. An entry runs over several lines and the comments that introduce
        ///     the next one sit between them, so anything shorter than the real call reads a
        ///     neighbor's prose as this entry's predicate.
        /// </summary>
        private static IReadOnlyList<string> Entries()
        {
            string source = Read(FlowFile);
            int at = source.IndexOf("private static readonly Node[] Order", StringComparison.Ordinal);
            at.Should().BeGreaterThan(0, "CreationFlow should still declare the Order table");

            int end = source.IndexOf("\n        };", at, StringComparison.Ordinal);
            end.Should().BeGreaterThan(at, "the Order table should still be closed");

            string table = source.Substring(at, end - at);
            var entries = new List<string>();
            foreach (Match start in Regex.Matches(table, @"new\(""[a-z]"))
            {
                int open = table.IndexOf('(', start.Index);
                int depth = 0;
                for (int i = open; i < table.Length; i++)
                {
                    if (table[i] == '(') depth++;
                    else if (table[i] == ')' && --depth == 0)
                    {
                        entries.Add(table.Substring(open, i - open + 1));
                        break;
                    }
                }
            }

            entries.Should().NotBeEmpty("the Order table should still hold entries");
            return entries;
        }

        private static string IdOf(string entry) => Regex.Match(entry, @"""([a-z][a-z0-9_]*)""").Groups[1].Value;

        private static bool IsChoice(string entry) => Predicate(entry).Contains("RouteDecidedExternally");

        /// <summary>
        ///     The routes an entry names, read from everything after its id so that a menu
        ///     called <c>cs_custom_menu</c> is not taken for the Start Editor by its name.
        ///
        ///     <c>Guided</c> is the two routes that tell a life, named once in the table
        ///     rather than spelled out per row: the rows below the life are shared and
        ///     writing both on each of them thirty times over is how one of them comes to
        ///     be dropped from one row.
        /// </summary>
        private static IReadOnlyCollection<Route> RoutesOf(string entry)
        {
            string predicate = Predicate(entry);
            var routes = new HashSet<Route>();

            if (predicate.Contains("Guided"))
            {
                routes.Add(Route.LifePath);
                routes.Add(Route.Narrative);
            }

            if (predicate.Contains("LifePath")) routes.Add(Route.LifePath);
            if (predicate.Contains("Narrative")) routes.Add(Route.Narrative);
            if (predicate.Contains("Custom")) routes.Add(Route.Custom);

            return routes;
        }

        private static string Predicate(string entry)
        {
            string id = IdOf(entry);
            return entry.Substring(entry.IndexOf(id, StringComparison.Ordinal) + id.Length);
        }

        private static string Read(string file) => File.ReadAllText(ModSource.Path(file.Split('/')));
    }
}
