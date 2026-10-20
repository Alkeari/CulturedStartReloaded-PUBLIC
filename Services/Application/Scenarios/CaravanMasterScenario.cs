using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Stocks the starting party like a working caravan: pack animals and a
    ///     spread of trade goods bought out of the starting purse. Guards come from the
    ///     culture's caravan guard troop (handled in TroopStep).
    /// </summary>
    public sealed class CaravanMasterScenario : IScenarioApplier
    {
        /// <summary>
        ///     How many stacks a factor fills when the ledger is left to him. The
        ///     chapter promises this number and names no goods, so the load has to
        ///     come to exactly this many distinct stacks: a good drawn twice merges
        ///     into one stack in the roster and the promise is quietly short.
        /// </summary>
        public const int LedgerStacks = 3;

        public string Name => "Caravan Master";

        /// <summary>
        ///     What a factor can buy off the markets. The ledger chapter asks this
        ///     before it offers the answer and this fills the wagons from it, so the
        ///     promise and the load are one pool rather than two that can differ.
        /// </summary>
        public static List<ItemObject> MarketStock() => ArmorQuery.TradeGoodItems();

        public string? Validate(StartContext context)
        {
            return context.Hero.PartyBelongedTo == null
                ? "hero has no party"
                : null;
        }

        public void Apply(StartContext context)
        {
            var party = context.Hero.PartyBelongedTo;
            var roster = party.ItemRoster;

            int packAnimals = Math.Max(0,
                context.Session.CustomPackAnimals ?? context.Settings?.CaravanPackAnimals ?? 6);
            var mule = MBObjectManager.Instance.GetObject<ItemObject>("mule")
                       ?? MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse");
            if (mule != null && packAnimals > 0)
            {
                roster.AddToCounts(new EquipmentElement(mule), packAnimals);
                CSLogger.Info($"CaravanMasterScenario: {packAnimals}x {mule.Name} added.");
            }

            // An answer that loads nothing is honored as it is titled, however
            // odd a trader with empty wagons looks. This is checked before the
            // list, because the answer leaves the same empty list a chapter
            // nobody answered leaves, and the list cannot tell them apart
            if (context.Session.NoTradeGoods)
            {
                CSLogger.Info("CaravanMasterScenario: the wagons were answered as empty, so no stock is added.");
                return;
            }

            // Skipped entirely when the wagons were composed by hand, because the
            // ledger chapter and the editor both promise exactly what they listed
            // and three random stacks on top of it makes that promise false
            if (context.Session.CustomTradeGoods.Count > 0)
            {
                CSLogger.Info("CaravanMasterScenario: the wagons were loaded by hand, so no stock is added.");
                return;
            }

            var goods = MarketStock();
            if (goods.Count < LedgerStacks)
            {
                CSLogger.Info($"CaravanMasterScenario: the markets carry {goods.Count} kinds of cargo, " +
                              $"fewer than the {LedgerStacks} stacks the ledger promises, so no stock is added.");
                return;
            }

            // The chapter that offers this promises quantities that follow the
            // purse, so the load is bought out of the purse rather than scaled
            // by the band's ordinal: a sixth of the coin per stack, priced at
            // the good's own market value.
            int purse = Steps.ResourceStep.EffectiveGold(context.Session, context.Settings);
            int perStack = Math.Max(1, purse / (LedgerStacks * 2));

            // Drawn without replacement, because two draws of the same good come
            // back as one stack and the panel promised three
            var unbought = new List<ItemObject>(goods);
            for (int i = 0; i < LedgerStacks; i++)
            {
                int drawn = CSRandom.Next(unbought.Count);
                var item = unbought[drawn];
                unbought.RemoveAt(drawn);

                // Cultured Start's ledger scales its load with the purse band it read,
                // as that route always has, rather than buying it out of the coin
                int units = context.Session.Mode == Models.SetupMode.LifePath
                    ? 8 + 4 * (int)context.Session.SelectedGold
                    : Math.Max(1, perStack / Math.Max(1, item.Value));
                roster.AddToCounts(new EquipmentElement(item), units);
                CSLogger.Info($"CaravanMasterScenario: {units}x {item.Name} added.");
            }
        }
    }
}
