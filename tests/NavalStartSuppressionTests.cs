using System.IO;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     War Sails runs its own starting-options behavior on the same pass 8 the game's own
    ///     behavior grants on, and the personal ship it hands out there is not gated on the chosen
    ///     start type. A Cultured Start or Start Editor character therefore collected a trade ship
    ///     no panel of this mod named, which must never happen.
    ///
    ///     The suppression lives in files this project cannot compile, since they name engine
    ///     types, so it is read here as source. Two things must not drift: the DLC behavior is the
    ///     patch target, and both suppressions run through the one ownership gate, because a
    ///     Vanilla Start promises the game behaves as if this mod were absent and must keep its
    ///     ship.
    /// </summary>
    public class NavalStartSuppressionTests
    {
        private const string NavalBehavior =
            "NavalDLC.CampaignBehaviors.NavalAdvancedStartingPlayerOptionsCampaignBehavior";

        private static string Source(params string[] parts) => File.ReadAllText(ModSource.Path(parts));

        [Fact]
        public void War_Sails_own_start_grant_is_patched_out()
        {
            string compat = Source("Services", "GameCompat.cs");

            compat.Should().Contain($"\"{NavalBehavior}\"",
                "that behavior is what grants the personal ship, and naming it as a string is what " +
                "lets the patch be skipped on a game without the DLC");
            compat.Should().MatchRegex(
                @"AccessTools\.Method\(navalBehavior,\s*""OnCharacterCreationIsOver""\)[\s\S]{0,160}?""SuppressNavalStart""",
                "OnCharacterCreationIsOver is the method that reads IsPersonalShipEnabled");
        }

        [Fact]
        public void Both_suppressions_run_through_the_one_ownership_gate()
        {
            string patch = Source("Patches", "StartOptionsPatch.cs");

            patch.Should().MatchRegex(@"SuppressVanillaStart\(int index\)\s*=>\s*GrantIsTheirs\(index,");
            patch.Should().MatchRegex(@"SuppressNavalStart\(int index\)\s*=>\s*GrantIsTheirs\(index,");
            patch.Should().Contain("AdvancedStartBridge.ModOwnsTheStart()",
                "a Vanilla Start keeps whatever the game's own screen was told to grant, the personal ship included");
        }
    }
}
