using System;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     An option says what it does, and what it does is what arrives.
    ///
    ///     A sweep of every answer the guided route puts on screen turned up three
    ///     more places where a caption or the panel beside it named something the
    ///     pipeline does not hand over: a castle that can resolve to a port town, a
    ///     town that can resolve to a village, a distance measured against a people
    ///     who hold nothing, and a count of companions that left out the ones
    ///     earlier scenes had already promised by name. None of it can be executed
    ///     here, since all four files name engine types, so what is checked is the
    ///     wiring as the files write it.
    /// </summary>
    public class HonoredTitlesTests
    {
        [Fact]
        public void The_seat_on_the_water_calls_a_castle_a_castle_only_when_it_is_one()
        {
            string admiral = ModSource.MemberBody("CharacterCreation/Menus/FleetMenu.cs", "string AdmiralEffect");

            admiral.Should().Contain("seat.IsCastle",
                "the seat falls back to another port of the realm where no castle holds one, and the line " +
                "beside it has to follow the settlement rather than the wish");
            admiral.Should().Contain("CSR_Panel_Fleet_AdmiralPort",
                "a seat that is not a castle needs a line of its own");
        }

        [Fact]
        public void The_farthest_place_is_not_offered_where_there_is_no_distance_to_measure()
        {
            string farthest = ModSource.MemberBody(
                "CharacterCreation/Menus/StartLocationMenu.cs", "IReadOnlyList<Settlement> TheFarthest");

            int heartland = farthest.IndexOf("heartland == null", StringComparison.Ordinal);
            heartland.Should().BeGreaterThan(0);
            farthest.Substring(heartland).Should().Contain("return None;",
                "a people who hold nothing anywhere have no country to be far from, so the framing has " +
                "no honest answer and is withheld rather than answered with the richest foreign town");
            farthest.Should().NotContain("strangers.OrderByDescending(Size)",
                "ranking strangers by prosperity answers a question about distance with one about wealth");
        }

        [Fact]
        public void Fate_names_every_kind_of_place_it_can_actually_leave_you_in()
        {
            string menu = ModSource.MemberBody(
                "CharacterCreation/Menus/StartLocationMenu.cs", "void AddStartLocationMenu");
            string step = ModSource.MemberBody(
                "Services/Application/Steps/LocationStep.cs", "Settlement? WhereTheLifeLeftYou");

            step.Should().Contain("s.IsVillage",
                "the life read off the scenes resolves to a village; this test is about the panel agreeing " +
                "with it, so it fails loudly if the step ever stops returning one");

            int auto = menu.IndexOf("CSR_Panel_Location_Auto", StringComparison.Ordinal);
            auto.Should().BeGreaterThan(0);
            menu.Substring(auto, menu.IndexOf('"', auto + 30) - auto).Should().Contain("village",
                "a panel promising a town for an answer that can land on a village is a promise the start breaks");
        }

        [Fact]
        public void The_companion_panel_counts_the_people_the_life_already_named()
        {
            string effect = ModSource.MemberBody(
                "CharacterCreation/Menus/CompanionSelectMenu.cs", "string Effect(int count)");

            effect.Should().Contain("Named()",
                "the allies earlier scenes promised ride whatever this chapter is answered, so the panel " +
                "cannot state a party that leaves them out");
            effect.Should().Contain("CSR_Panel_Companion_NoneCalled",
                "a life that named people cannot be told it opens with no companion at all");
        }

        [Fact]
        public void The_people_the_life_named_are_counted_in_one_place()
        {
            string step = ModSource.MemberBody(
                "Services/Application/Steps/CompanionStep.cs", "List<string> NamedAllies");

            step.Should().Contain("GuidedRun.Outcome().Allies",
                "the panel and the party read the same list, so neither can name people the other does not");
            step.Should().Contain("IsKinAlly",
                "blood belongs in the family tree and is not a companion seat, in the count as in the party");
        }
    }
}
