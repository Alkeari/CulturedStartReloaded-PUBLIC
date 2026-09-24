using TaleWorlds.Core;

namespace CulturedStartReloaded.Models
{
    /// <summary>One item stack the player composed for the starting inventory.</summary>
    public sealed class InventoryEntry
    {
        public InventoryEntry(ItemObject item, int count)
        {
            Item = item;
            Count = count;
        }

        public ItemObject Item { get; }
        public int Count { get; set; }
    }
}
