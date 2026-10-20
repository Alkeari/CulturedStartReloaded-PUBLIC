using System.Collections.Generic;
using System.IO;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Scenarios;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The three starts nobody could reach, and the DLC gate that has to stand
    ///     over all three of them.
    ///
    ///     A measurement over 20,000 walked lives found every named start type
    ///     reached except a seafaring plunderer, a fleet admiral and a merchant
    ///     venturer, each of them exactly zero. The cause was not a missing start
    ///     type: <see cref="LifeProfile.Lean.Sea"/> was real and a third of War
    ///     Sails lives scored it, and nothing anywhere read it. So the fix is three
    ///     degrees keyed on that lean, and what has to be held is that a life can
    ///     still reach each of them. A window nudged a point too high puts one of
    ///     these back at zero and nothing else in the suite would notice.
    ///
    ///     The other half is the DLC gate. Every one of these degrees hands out a
    ///     War Sails object, so the chapter must not exist at all for a player
    ///     without the DLC, and it must not exist by being silently empty either.
    /// </summary>
    public class SeaStartTests
    {
        /// <summary>
        ///     The run a War Sails owner walks. The installed catalog is the land
        ///     run when no game is loaded, and the sea is reachable only in the
        ///     other one, so every reachability claim here is made against it.
        /// </summary>
        private static IReadOnlyList<Scene> Afloat() => SceneCatalog.Build(true);

        private static int Sea(Life life) => LifeProfile.From(life.Left).Score(LifeProfile.Lean.Sea);

        public static IEnumerable<object[]> EveryDegree() => new[]
        {
            new object[] { SeaDegree.Raider, StartType.Outlaw },
            new object[] { SeaDegree.Admiral, StartType.LandedVassal },
            new object[] { SeaDegree.Venturer, StartType.CaravanMaster }
        };

        /// <summary>
        ///     The claim that matters: a player can actually get here. A degree is
        ///     offered only where the life scored enough sea AND the scenes landed
        ///     the character on the start type that degree belongs to, so both have
        ///     to happen in one life or the degree is decoration.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryDegree))]
        public void A_walked_life_reaches_every_sea_degree(SeaDegree degree, StartType station)
        {
            var lives = LifeHarness.Lives(Afloat(), LifeHarness.Sweep, 20260910);

            var reached = lives
                .Where(life => life.Reading.Station == station &&
                               Sea(life) >= SeaDegrees.Floor(degree))
                .ToList();

            reached.Should().NotBeEmpty(
                $"{degree} is offered to a {station} whose life scored {SeaDegrees.Floor(degree)} at sea, " +
                $"and no life out of {lives.Count} walked reaches both, which is the zero this chapter exists to fix");
        }

        /// <summary>
        ///     And a life that never went near the water is never offered one. The
        ///     degree is a claim about the life, so a landlocked character reaching
        ///     it would make the chapter a free gift rather than an earned one.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryDegree))]
        public void A_life_that_never_went_to_sea_reaches_no_degree(SeaDegree degree, StartType station)
        {
            _ = station;

            foreach (var life in LifeHarness.Lives(Afloat(), LifeHarness.Sweep, 20260910))
            {
                if (Sea(life) > 0) continue;

                SeaDegrees.LifeReaches(LifeProfile.From(life.Left), degree).Should().BeFalse(
                    $"a life with nothing at sea reached {degree}: {life.Trail}");
            }
        }

        /// <summary>
        ///     A life that cannot be read is not a life that went to sea. The gate
        ///     has to fail shut, because failing open hands three hulls and a port
        ///     castle to a character whose story never left the road.
        /// </summary>
        [Fact]
        public void An_unreadable_life_is_offered_nothing()
        {
            foreach (SeaDegree degree in System.Enum.GetValues(typeof(SeaDegree)))
                SeaDegrees.LifeReaches(null, degree).Should().BeFalse();
        }

        /// <summary>Only three start types are offered the water.</summary>
        [Fact]
        public void No_other_start_type_is_offered_the_water()
        {
            var seagoing = System.Enum.GetValues(typeof(StartType)).Cast<StartType>()
                .Where(start => SeaDegrees.For(start) != SeaDegree.None)
                .ToList();

            seagoing.Should().BeEquivalentTo(new[]
            {
                StartType.Outlaw, StartType.LandedVassal, StartType.CaravanMaster
            });
        }

        /// <summary>
        ///     Nothing that stays ashore carries a degree, which is what lets the
        ///     rest of the pipeline ask one question instead of three.
        /// </summary>
        [Fact]
        public void Staying_ashore_is_the_absence_of_a_degree()
        {
            SeaDegrees.Floor(SeaDegree.None).Should().Be(int.MaxValue);
            SeaDegrees.LifeReaches(LifeProfile.From(new List<ChoiceConsequence>()), SeaDegree.None)
                .Should().BeFalse();
        }

        #region The DLC gate

        private static string Source(params string[] parts) => File.ReadAllText(ModSource.Path(parts));

        /// <summary>
        ///     The chapter joins the flow through one predicate and no other, and
        ///     that predicate asks the service that grants the ships. A second door
        ///     into the chapter is how a player without War Sails meets it.
        /// </summary>
        [Fact]
        public void The_chapter_is_in_the_flow_only_behind_the_sea_predicate()
        {
            string flow = Source("CharacterCreation", "Flow", "CreationFlow.cs");

            Regex(flow, @"new\(""cs_fleet_menu""[^)]*\)").Should().HaveCount(1,
                "the sea chapter is declared once in the flow table");
            flow.Should().Contain("new(\"cs_fleet_menu\", s => Narrative(s) && GoesToSea(s))",
                "the chapter belongs to Cultured Start Revamped, which Cultured Start never asked it of, " +
                "and to neither route without the water");
            flow.Should().MatchRegex(@"bool GoesToSea\([^)]*\)\s*=>[^;]*SeaGrants\.Offered",
                "the flow asks the service that grants the ships whether there are any");
        }

        /// <summary>
        ///     And that service refuses before anything else when the DLC is absent.
        ///     Without War Sails no hull is registered and no settlement reports a
        ///     port, so this is a gate that fails to nothing rather than to an
        ///     exception, which is the shape a DLC gate has to have.
        /// </summary>
        [Fact]
        public void The_service_refuses_the_water_without_the_dlc()
        {
            string grants = Source("Services", "Application", "Scenarios", "SeaGrants.cs");

            Body(grants, "SeaDegree Offered").Should().Contain("if (!ContentPresent()) return SeaDegree.None;",
                "every degree is behind the content check");
            Body(grants, "bool ContentPresent").Should().Contain("NavalDLCService.IsNavalDLCLoaded()");
            Body(grants, "bool ContentPresent").Should().Contain("GetObjectTypeList<ShipHull>()",
                "the DLC being loaded is not the same as its ship data being registered");
            Body(grants, "bool ContentPresent").Should().Contain("settlement.HasPort");

            string service = Source("Services", "NavalDLCService.cs");
            service.Should().Contain("public static bool IsNavalDLCLoaded()",
                "the gate above binds to this name");
        }

        /// <summary>
        ///     A chapter with one answer is not a chapter. Every degree is offered
        ///     beside a dry answer that changes nothing, so the water is a decision
        ///     rather than an announcement.
        /// </summary>
        [Fact]
        public void The_chapter_always_offers_a_way_to_stay_ashore()
        {
            string menu = Source("CharacterCreation", "Menus", "FleetMenu.cs");

            menu.Should().Contain("\"cs_fleet_ashore\"");
            Regex(menu, @"m => true,").Should().NotBeEmpty(
                "the dry answer is offered unconditionally wherever the chapter appears");

            foreach (string id in new[] { "cs_fleet_raider", "cs_fleet_admiral", "cs_fleet_venturer" })
                menu.Should().Contain($"\"{id}\"", $"{id} is one of the three degrees the chapter carries");
        }

        /// <summary>
        ///     Built and never registered is a dead control, so the registration is
        ///     read out of the file that would have to carry it.
        /// </summary>
        [Fact]
        public void The_chapter_is_registered_with_the_game()
        {
            Source("CharacterCreation", "CharacterCreationProvider.cs")
                .Should().Contain("FleetMenu.AddFleetMenu(characterCreationManager);");
        }

        /// <summary>
        ///     No degree hides behind an empty panel.
        ///
        ///     SeaGrants.Offered answers a degree only after asking the very lists
        ///     these three read, off caches keyed on the culture and the realm, and
        ///     the option's condition compares against that answer. So a panel
        ///     composed for one of these degrees always has hulls and, where the
        ///     degree needs one, a port. The empty returns that stood here could
        ///     not fire, and what they would have produced is the one thing a panel
        ///     may not produce for an answer: an option that reads as doing nothing.
        /// </summary>
        [Fact]
        public void No_degree_of_the_water_hides_behind_an_empty_panel()
        {
            string menu = Source("CharacterCreation", "Menus", "FleetMenu.cs");

            foreach (string effect in new[] { "RaiderEffect", "AdmiralEffect", "VenturerEffect" })
                Body(menu, effect).Should().NotContain("string.Empty",
                    $"{effect} is only ever asked for a degree SeaGrants.Offered has already supplied");
        }

        /// <summary>
        ///     A cargo figure is false on land: the DLC's inventory model adds a
        ///     ship's capacity only while the party is at sea. So the number may
        ///     never be stated without the condition on it.
        /// </summary>
        [Fact]
        public void The_cargo_figure_is_never_stated_without_saying_at_sea()
        {
            string menu = Source("CharacterCreation", "Menus", "FleetMenu.cs");

            var cargo = Regex(menu, @"\{=CSR_Panel_Fleet_Cargo\}[^""]*").ToList();
            cargo.Should().HaveCount(1, "there is one cargo sentence and it is the only place the figure is stated");
            cargo[0].Should().Contain("at sea");
            cargo[0].Should().Contain("{CARGO}");
        }

        private static IReadOnlyList<string> Regex(string source, string pattern) =>
            System.Text.RegularExpressions.Regex.Matches(source, pattern)
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Value)
                .ToList();

        /// <summary>One member's body, brace matched, so a neighbor is never read as this one.</summary>
        private static string Body(string source, string member)
        {
            int at = source.IndexOf(member + "(", System.StringComparison.Ordinal);
            at.Should().BeGreaterThan(-1, $"{member} should still exist");

            int open = source.IndexOf('{', at);
            open.Should().BeGreaterThan(-1);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }

            return source.Substring(open);
        }

        #endregion

        #region Both routes reach the water

        /// <summary>
        ///     The Start Editor is a full-control editor over the start,
        ///     so something the guided route can grant and it cannot is exactly the
        ///     asymmetry this file exists to stop. The row has to be built into the
        ///     tab and not merely declared, and picking on it has to go through the
        ///     service that grants the ships, or the screen and the harbor disagree.
        /// </summary>
        [Fact]
        public void The_editor_can_put_a_ship_under_a_character()
        {
            string editor = Source("CharacterCreation", "Editor", "StartEditorVM.cs");

            editor.Should().Contain("BuildSeaDegreeRow(rows);",
                "the row is built into the Path and Realm tab rather than only declared");
            Body(editor, "void BuildSeaDegreeRow").Should().Contain("SeaGrants.Choose(",
                "the pick goes through the service that grants the ships");
        }

        /// <summary>
        ///     And it is absent without War Sails rather than present and dead.
        ///     The life gate in front of the guided route is the one
        ///     thing it must NOT ask: that gate reads the scenes a character walked,
        ///     and a start composed in the editor walked none, so asking it would
        ///     close the water to every start this screen can build.
        /// </summary>
        [Fact]
        public void The_editors_row_is_absent_without_the_dlc_and_never_disabled()
        {
            string editor = Source("CharacterCreation", "Editor", "StartEditorVM.cs");
            string row = Body(editor, "void BuildSeaDegreeRow");
            string offer = Body(editor, "SeaDegree DegreeOnOffer");

            row.Should().Contain("DegreeOnOffer()");
            row.Should().Contain("if (degree == SeaDegree.None) return;",
                "no degree means no row at all");
            offer.Should().Contain("SeaGrants.ContentPresent()");
            offer.Should().NotContain("SeaGrants.Offered(",
                "the guided route's life gate would close the water to every editor start");
        }

        /// <summary>
        ///     A preset that drops the degree hands back a character with no ships
        ///     and no port and says nothing about it, which is the promise broken by
        ///     omission. Both directions, and a default that reads an older preset
        ///     as the answer it was composed with.
        /// </summary>
        [Fact]
        public void A_preset_carries_the_water_in_both_directions()
        {
            string presets = Source("Services", "StartPresetService.cs");

            presets.Should().Contain("SeaDegree = session.SelectedSeaDegree.ToString()",
                "the degree is written into the saved shape");
            Body(presets, "void ApplyData").Should().Contain("session.SelectedSeaDegree =",
                "and read back out of it");
            presets.Should().Contain("public string SeaDegree = nameof(Application.Scenarios.SeaDegree.None);",
                "a preset written before the water carries no such field, and ashore is what it was composed as");
        }

        /// <summary>
        ///     The degree belongs to the start type, so abandoning the start type
        ///     abandons it. Left behind, it would put three hulls under a commoner
        ///     the moment the campaign opened, since the grant reads the degree and
        ///     never the start type.
        /// </summary>
        [Fact]
        public void Changing_the_start_type_in_the_editor_lets_go_of_the_water()
        {
            Source("CharacterCreation", "Editor", "StartEditorVM.cs")
                .Should().MatchRegex(
                    @"session\.SelectedStartType = type;[\s\S]{0,400}?SeaGrants\.Choose\(session, SeaDegree\.None\);");
        }

        /// <summary>
        ///     Where the degree decides the holding or the starting town, the editor
        ///     says so in the row rather than taking a second answer it would then
        ///     overwrite: <c>SeaGrants.Settle</c> re-states both at apply time, so a
        ///     pick allowed here would vanish without a word.
        /// </summary>
        [Fact]
        public void The_editor_never_offers_a_holding_the_water_has_already_settled()
        {
            string editor = Source("CharacterCreation", "Editor", "StartEditorVM.cs");

            Regex(editor, @"if \((?:SeatOnTheWater|PortOfTheSeaCaravan)\(\) != null\)")
                .Should().HaveCount(2,
                    "the holding row and the starting location row are both guarded");
            editor.Should().Contain("{=CSR_Editor_Sea_SeatSettled}");
            editor.Should().Contain("{=CSR_Editor_Sea_PortSettled}");
        }

        #endregion
    }
}
