using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.Services;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     War Sails content must not reach a player who does not own the DLC
    ///     at all. This is about Cultured Start Revamped and nothing else: no
    ///     scene of it names the sea, and it carries no DLC gate of its own, so the
    ///     guarantee rests entirely on what the scenes say. These lock it down so
    ///     that writing a naval scene fails here rather than shipping to a player
    ///     without a ship.
    ///
    ///     If that route ever should send a character to sea, that is a good change
    ///     and these tests are what tells you it needs a DLC gate first.
    ///
    ///     The other guided route does send them there, and holds its end of
    ///     that gate the other way: its fourteen sea answers are put on the stage
    ///     only where War Sails has the skill they name. That is asserted in
    ///     <c>CulturedStartBaselineTests</c>, since
    ///     a route with a gate and a route with nothing to gate are two different
    ///     claims and one file asserting both would read as neither.
    /// </summary>
    public class WarSailsGatingTests
    {
        /// <summary>
        ///     Words that only mean something with War Sails loaded. Deliberately
        ///     narrow: "crew", "port" and "tide" are ordinary English and appear in
        ///     land-bound prose, so matching them would train an author to work
        ///     around this test rather than to read it.
        /// </summary>
        private static readonly string[] NavalWords =
        {
            "mariner", "boatswain", "shipmaster", "seafarer", "shipwright",
            "sail", "ship", "boat", "galley", "corsair", "harbour", "harbor",
            "naval", "seamanship", "dockworker"
        };

        public static IEnumerable<object[]> EveryOption() =>
            SceneCatalog.All.SelectMany(s => s.Options.Select(o => new object[] { s.Id, o.Id }));

        [Theory]
        [MemberData(nameof(EveryOption))]
        public void No_guided_option_shows_text_about_content_the_player_may_not_own(
            string sceneId, string optionId)
        {
            var scene = SceneCatalog.All.Single(s => s.Id == sceneId);
            var option = scene.Options.Single(o => o.Id == optionId);

            Naval(option.Title).Should().BeEmpty(
                $"{optionId}'s button names War Sails content, which a player without the DLC would still be shown");
            Naval(option.Prose).Should().BeEmpty(
                $"{optionId}'s prose names War Sails content, which a player without the DLC would still be shown");
        }

        [Theory]
        [MemberData(nameof(EveryScene))]
        public void No_guided_prompt_asks_about_content_the_player_may_not_own(string sceneId)
        {
            var scene = SceneCatalog.All.Single(s => s.Id == sceneId);

            Naval(scene.Prompt).Should().BeEmpty(
                $"{sceneId} asks a question about War Sails content");
        }

        public static IEnumerable<object[]> EveryScene() =>
            SceneCatalog.All.Select(s => new object[] { s.Id });

        /// <summary>
        ///     A sea lean is what would push a character's skill toward Mariner,
        ///     Boatswain and Shipmaster. No single answer may contribute one, which
        ///     proves no combination of answers can: the profile sums its answers,
        ///     so a total can only hold what some answer put there.
        /// </summary>
        [Fact]
        public void No_guided_answer_can_score_a_sea_lean()
        {
            var offenders = SceneCatalog.All
                .SelectMany(s => s.Options)
                .Where(o => LifeProfile.From(o.Consequences).Score(LifeProfile.Lean.Sea) != 0)
                .Select(o => o.Id)
                .ToList();

            offenders.Should().BeEmpty(
                "a guided answer that leans toward the sea would push skill into the three War Sails " +
                "skills, which a player without the DLC does not have");
        }

        /// <summary>
        ///     The whole route, walked to the end, still comes to nothing at sea.
        ///     The per-answer test above proves this, but this is the statement a
        ///     reader actually cares about, so it is made directly.
        /// </summary>
        [Fact]
        public void A_walked_life_never_comes_out_leaning_to_sea()
        {
            var everyConsequence = SceneCatalog.All
                .SelectMany(s => s.Options)
                .SelectMany(o => o.Consequences)
                .ToList();

            LifeProfile.From(everyConsequence).Score(LifeProfile.Lean.Sea).Should().Be(0);
        }

        /// <summary>
        ///     The detector is what the tests above rest on, so it is checked
        ///     against real naval copy rather than trusted. These four are lines the
        ///     other route actually ships, in the localization id plus fallback form
        ///     both routes use: if this one ever grows a sea scene it will read like
        ///     one of these, and the tests above have to notice.
        /// </summary>
        [Theory]
        [InlineData("{=CSR_Family_Seafarers}Coastal seafarers")]
        [InlineData("{=CSR_Child_Boats}helped on fishing boats")]
        [InlineData("{=CSR_Edu_Sailor}apprenticed to a ship captain")]
        [InlineData("{=CSR_Turning_Storm_Desc}The sea took your ship and most of your crew.")]
        public void The_detector_notices_naval_copy(string naval)
        {
            Naval(naval).Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("{=CSR_Family_Retainers}Landed retainers")]
        [InlineData("{=CSR_Scene_TheVerdict}The judgement went against your house.")]
        public void The_detector_leaves_land_bound_copy_alone(string landBound)
        {
            Naval(landBound).Should().BeEmpty();
        }

        private static IEnumerable<string> Naval(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

            // The localization id carries the English fallback after "}", and the
            // player reads the fallback, so the whole string is searched
            string lowered = text!.ToLowerInvariant();
            return NavalWords.Where(w => lowered.IndexOf(w, StringComparison.Ordinal) >= 0).ToList();
        }
    }
}
