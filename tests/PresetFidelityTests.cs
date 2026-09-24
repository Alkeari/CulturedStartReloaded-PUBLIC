using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     A preset is written and read field by field, so a field nobody added is
    ///     dropped without a word and the player is handed a character they did not
    ///     compose. The service names engine types throughout and cannot
    ///     be compiled in here, so what is held here is that each field is
    ///     declared, written and read back, field by named field. That the set of
    ///     fields is complete, and that none of them is read behind a guard an
    ///     older preset cannot pass, is held by PresetCoverageTests. Neither runs
    ///     the real CaptureState over a real session: only the game can.
    /// </summary>
    public class PresetFidelityTests
    {
        /// <summary>
        ///     The two names the editor settles. Dropping them cost twice over: the
        ///     name vanished on load, and the unsaved-changes prompt, which diffs the
        ///     captured shape against the shape the editor opened on, could not see a
        ///     name change at all, so Discard and Close did not undo one either.
        /// </summary>
        [Fact]
        public void A_preset_carries_the_character_and_the_house_by_name()
        {
            string presets = Source();

            presets.Should().Contain("PlayerFirstName = session.PlayerFirstName",
                "the character's name is written into the saved shape");
            presets.Should().Contain("PlayerClanName = session.PlayerClanName",
                "and the house's name with it");

            string apply = Body(presets, "void ApplyData");
            apply.Should().Contain("session.PlayerFirstName = data.PlayerFirstName;");
            apply.Should().Contain("session.PlayerClanName = data.PlayerClanName;");
        }

        /// <summary>
        ///     The realm's name was the worst of the three: the text was carried and
        ///     neither the style nor whether the question was settled, so a preset
        ///     restored half a decision and the other half came from whatever the
        ///     session loading it happened to be carrying.
        /// </summary>
        [Fact]
        public void A_preset_carries_how_the_realm_was_named_and_not_only_the_name()
        {
            string presets = Source();

            presets.Should().Contain("KingdomNameStyle = session.KingdomNameStyle.ToString()");
            presets.Should().Contain("KingdomNameDecided = session.KingdomNameDecided");

            string apply = Body(presets, "void ApplyData");
            apply.Should().Contain("session.KingdomNameDecided = data.KingdomNameDecided ??",
                "and an older preset is read as the answer it was composed with");
            apply.Should().Contain("session.KingdomNameStyle =");
        }

        /// <summary>
        ///     An older preset carries the realm's name text and nothing else about
        ///     it. Every menu that writes that text settles the question in the same
        ///     breath, so text is the evidence a decision was made and no text is the
        ///     undecided state. Both must be assigned rather than left alone, or the
        ///     loading session's own answer stands in for one the file never held.
        /// </summary>
        [Fact]
        public void An_older_preset_reads_its_realm_naming_out_of_the_text_it_does_carry()
        {
            string apply = Body(Source(), "void ApplyData");

            apply.Should().Contain("bool namedInFile = !string.IsNullOrWhiteSpace(data.KingdomName);");
            apply.Should().Contain("KingdomNameStyle.Custom",
                "stored words are read as the player's own");
            apply.Should().Contain("KingdomNameStyle.Automatic",
                "and no text at all is the undecided state");
        }

        /// <summary>
        ///     The culture is the game's own creation stage to decide, and this mod
        ///     only mirrors it. A preset that reassigned it would leave the session
        ///     picking gear, troops and names for one culture while the character is
        ///     built as another, which is that failure exactly. So it is recorded, never
        ///     restored, and a difference is said out loud.
        /// </summary>
        [Fact]
        public void A_preset_records_the_culture_it_was_composed_on_and_never_puts_it_back()
        {
            string presets = Source();

            presets.Should().Contain("CultureId = session.SelectedCulture?.StringId",
                "what it was composed on is recorded");

            Body(presets, "void ApplyData").Should().NotContain("session.SelectedCulture",
                "restoring it would disagree with the character the game builds");

            presets.Should().Contain("AnnounceComposedCulture(data.CultureId, session)",
                "and a load onto another culture says so");
            Body(presets, "void AnnounceComposedCulture").Should()
                .Contain("CSR_Preset_OtherCulture");
        }

        /// <summary>
        ///     The five figures the editor can now set. Each is read at apply time
        ///     wherever the exact row above it is left automatic, so a preset that
        ///     dropped them handed back a character sized by the session that loaded
        ///     it rather than by the one that saved it. The band travels beside the
        ///     exact figure and not instead of it: a caravan master's cargo stacks
        ///     are sized off the gold band whatever the purse holds.
        /// </summary>
        [Fact]
        public void A_preset_carries_the_bands_the_bearing_and_the_larder()
        {
            string presets = Source();

            presets.Should().Contain("GoldBand = session.SelectedGold.ToString()");
            presets.Should().Contain("InfluenceBand = session.SelectedInfluence.ToString()");
            presets.Should().Contain("TroopsBand = session.SelectedTroops.ToString()");
            presets.Should().Contain("Bearing = session.Bearing.ToString()");
            presets.Should().Contain("Provisions = session.Provisions.ToString()");

            presets.Should().Contain("Gold = session.CustomGold",
                "the exact purse still travels beside the band it overrides");
        }

        /// <summary>
        ///     Assigned whatever the file holds. The conditional shape used for the
        ///     founding, <c>if (Enum.TryParse(...)) session.X = parsed;</c>, is the
        ///     leak itself: a field the preset never named leaves the session's own
        ///     answer standing, which is the trap the sea degree was caught by.
        /// </summary>
        [Fact]
        public void An_older_preset_loads_the_figures_it_was_composed_with()
        {
            string apply = Body(Source(), "void ApplyData");

            apply.Should().Contain("session.SelectedGold = Enum.TryParse(data.GoldBand, out RangePreset");
            apply.Should().Contain("session.SelectedInfluence = Enum.TryParse(data.InfluenceBand, out RangePreset");
            apply.Should().Contain("session.SelectedTroops = Enum.TryParse(data.TroopsBand, out RangePreset");
            apply.Should().Contain("session.Bearing = Enum.TryParse(data.Bearing, out ArmorBearing");
            apply.Should().Contain("session.Provisions = Enum.TryParse(data.Provisions, out ProvisionPlan");

            apply.Should().Contain(": RangePreset.Standard;", "the band a session starts on");
            apply.Should().Contain(": ArmorBearing.Station;", "dressed for the station and no more");
            apply.Should().Contain(": ProvisionPlan.Sensible;", "and a sensible larder");
        }

        /// <summary>
        ///     Every preset written before this change must still load, so no field
        ///     added here may be one the reader refuses a file for. Each is nullable
        ///     or defaulted, and the version says which shape was written.
        /// </summary>
        /// <summary>
        ///     A companion's or relative's exact attributes and traits travel inside
        ///     their own saved sheet. A preset written before the sheet carried them
        ///     holds neither, and reads back as both left automatic, which is what
        ///     it was composed with.
        /// </summary>
        [Fact]
        public void A_preset_carries_a_generated_heros_attributes_and_traits()
        {
            string presets = Source();

            Body(presets, "HeroSpecDto? ToSpecDto").Should()
                .Contain("Attributes = new Dictionary<string, int>(spec.Attributes)")
                .And.Contain("Traits = new Dictionary<string, int>(spec.Traits)");

            string apply = Body(presets, "void ApplySpecDto");
            apply.Should().Contain("spec.Clear();", "a loaded sheet never keeps the loading session's values");
            apply.Should().Contain("spec.Attributes[pair.Key] = pair.Value;");
            apply.Should().Contain("spec.Traits[pair.Key] = pair.Value;");
        }

        [Fact]
        public void The_saved_shape_says_which_version_wrote_it()
        {
            Source().Should().Contain("public int Version = 6;",
                "the shape changed, so the stamp on it does too");
        }

        private static string Source() =>
            File.ReadAllText(ModSource.Path("Services", "StartPresetService.cs"));

        /// <summary>One member's body, brace matched, so a neighbor is never read as this one.</summary>
        private static string Body(string source, string member)
        {
            int at = source.IndexOf(member + "(", StringComparison.Ordinal);
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
    }
}
