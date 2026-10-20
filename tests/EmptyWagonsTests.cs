using System;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     An answer that loads nothing loads nothing.
    ///
    ///     The ledger chapter's first rung used to clear the cargo list and say so,
    ///     which is exactly the state a chapter nobody answered leaves behind. The
    ///     caravan start reads that state as leave to fill the wagons itself, so the
    ///     answer that meant empty was the one answer guaranteed to arrive with three
    ///     random stacks in it. It was reported from Nexus and repaired by renaming
    ///     the rung to describe the cargo it was handing out, which honored the grant
    ///     at the cost of the answer: a trader could no longer start empty at all.
    ///     The intended trade is the other way round, so the rung is back and the
    ///     list now says which of the two states it is in.
    ///
    ///     None of it can be executed here. The chapter names the narrative menu
    ///     types and the scenario names the campaign roster, and that keeps both
    ///     out of this project and what is left is the wiring as the files write it.
    ///     That is enough, because the defect IS the wiring: the answer is told apart
    ///     from silence or it is not.
    /// </summary>
    public class EmptyWagonsTests
    {
        private const string Chapters = "CharacterCreation/Menus/ScenarioChapterMenus.cs";
        private const string Caravan = "Services/Application/Scenarios/CaravanMasterScenario.cs";
        private const string Provisions = "CharacterCreation/Menus/ProvisionsMenu.cs";

        [Fact]
        public void The_ledger_offers_a_rung_that_loads_nothing()
        {
            string menu = ModSource.MemberBody(Chapters, "void AddLedgerMenu");

            menu.Should().Contain("{=CSR_Ledger_None}Nothing at All",
                "the chapter has to put an answer whose caption says the wagons leave empty");
            menu.Should().Contain("{=CSR_Ledger_Empty_Revamped}Whatever the Market Had",
                "the factor's load is an answer in its own right and the empty one does not replace it");
        }

        [Fact]
        public void The_empty_rung_is_offered_to_every_life_and_needs_nothing_of_the_world()
        {
            string bands = ModSource.MemberBody(Chapters, "LifeBand[] LedgerBands");

            bands.Should().Contain("new(\"cs_ledger_none\", 0, LifeGate.Open, LifeGate.NoTop",
                "declining to load claims nothing about the life behind it, so no life is shut out of it");

            string menu = ModSource.MemberBody(Chapters, "void AddLedgerMenu");
            int rung = menu.IndexOf("{=CSR_Ledger_None}", StringComparison.Ordinal);
            int next = menu.IndexOf("LedgerBands[1]", StringComparison.Ordinal);
            rung.Should().BeGreaterThan(0);
            next.Should().BeGreaterThan(rung);

            menu.Substring(rung, next - rung).Should().Contain("NothingEffect);",
                "empty wagons are always available: an answer with nothing to hand over needs no supply check");
        }

        [Fact]
        public void The_empty_rung_writes_a_refusal_rather_than_an_empty_list()
        {
            ModSource.MemberBody(Chapters, "void Nothing").Should()
                .Contain("NoTradeGoods = true",
                    "clearing the list alone is what an unanswered chapter leaves, and the start buys stock on it");
        }

        [Fact]
        public void Every_other_ledger_answer_takes_the_refusal_back()
        {
            ModSource.MemberBody(Chapters, "void Factor").Should()
                .Contain("NoTradeGoods = false",
                    "re-deciding onto the factor's load has to undo the refusal or the wagons stay empty");

            ModSource.MemberBody(Chapters, "void LoadCargoBand").Should()
                .Contain("NoTradeGoods = false",
                    "re-deciding onto a named cargo has to undo the refusal");
        }

        [Fact]
        public void The_caravan_start_honors_the_refusal_before_it_reads_the_list()
        {
            string apply = ModSource.MemberBody(Caravan, "void Apply");

            int refusal = apply.IndexOf("NoTradeGoods", StringComparison.Ordinal);
            int list = apply.IndexOf("CustomTradeGoods.Count > 0", StringComparison.Ordinal);
            int stock = apply.IndexOf("MarketStock()", StringComparison.Ordinal);

            refusal.Should().BeGreaterThan(0, "the start has to know the wagons were answered as empty");
            list.Should().BeGreaterThan(refusal,
                "both states leave the same empty list, so the refusal has to be read first");
            stock.Should().BeGreaterThan(refusal, "nothing is bought once the refusal is read");
        }

        [Fact]
        public void A_saved_setup_carries_the_refusal()
        {
            string preset = ModSource.MemberBody("Services/StartPresetService.cs", "PresetData BuildData");
            preset.Should().Contain("NoTradeGoods = session.NoTradeGoods",
                "an empty list and answered-empty wagons load back as different setups");
        }

        [Fact]
        public void The_bare_provisions_answer_is_titled_for_the_larder_it_empties()
        {
            string menu = ModSource.MemberBody(Provisions, "void AddProvisionsMenu");

            menu.Should().Contain("{=CSR_Provisions_Bare_Revamped}An Empty Larder",
                "the plan withholds food and nothing else; the chapter asks about the larder and its " +
                "neighbor is A Sensible Larder");

            int bare = menu.IndexOf("{=CSR_Provisions_Bare_Revamped}", StringComparison.Ordinal);
            int caption = menu.IndexOf('"', menu.IndexOf('}', bare));
            menu.Substring(bare, caption - bare).Should().NotContain("Wagon",
                "a caravan master reaches this chapter holding pack animals and cargo the ledger and the " +
                "beasts already granted, so a title claiming the wagons would be false for that start");
        }
    }
}
