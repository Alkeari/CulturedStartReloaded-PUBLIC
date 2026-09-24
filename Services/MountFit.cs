using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Whether a harness belongs on a mount. The game keys both to the same number: a mount's
    ///     <c>HorseComponent.Monster.FamilyType</c> and a harness's <c>ArmorComponent.FamilyType</c>,
    ///     which is what the inventory screen compares before it lets a player put one on the other.
    ///     Nothing in an item's name or tier says it, so a draw that ignores the number will sooner
    ///     or later saddle a horse with elephant armor, which renders as a torn mess rather than as a
    ///     mistake anyone can see coming.
    ///
    ///     Mounts a conversion adds are covered by the same rule, because the rule is the game's own.
    /// </summary>
    public static class MountFit
    {
        /// <summary>The family a mount belongs to, or null when the item is not a mount.</summary>
        public static int? FamilyOfMount(ItemObject? mount) =>
            mount?.HorseComponent?.Monster?.FamilyType;

        /// <summary>The family a harness is cut for, or null when the item is not a harness.</summary>
        public static int? FamilyOfHarness(ItemObject? harness) =>
            harness?.ArmorComponent?.FamilyType;

        /// <summary>
        ///     Whether this harness may be worn by this mount. An empty slot fits anything, and a
        ///     harness with no mount under it is handled where the pair is assembled rather than here.
        /// </summary>
        public static bool Fits(ItemObject? mount, ItemObject? harness)
        {
            if (harness == null) return true;
            if (mount == null) return false;

            var mountFamily = FamilyOfMount(mount);
            var harnessFamily = FamilyOfHarness(harness);
            return mountFamily != null && harnessFamily != null && mountFamily == harnessFamily;
        }

        /// <summary>Only the harnesses that fit this mount, in the order they were given.</summary>
        public static List<ItemObject> FittingHarnesses(ItemObject? mount, IEnumerable<ItemObject> harnesses)
        {
            var family = FamilyOfMount(mount);
            return family == null
                ? new List<ItemObject>()
                : harnesses.Where(h => FamilyOfHarness(h) == family).ToList();
        }

        /// <summary>
        ///     True when the game holds any harness at all for this mount's family. A mount with none
        ///     is ridden bare, which is what its own people do; it is only worth knowing so that a
        ///     draw can prefer a mount it can actually finish dressing.
        /// </summary>
        public static bool HasAnyHarness(ItemObject? mount)
        {
            var family = FamilyOfMount(mount);
            if (family == null) return false;

            return TaleWorlds.ObjectSystem.MBObjectManager.Instance
                .GetObjectTypeList<ItemObject>()
                .Any(i => i.ItemType == ItemObject.ItemTypeEnum.HorseHarness &&
                          FamilyOfHarness(i) == family);
        }

        /// <summary>
        ///     Takes off a harness the mount cannot wear, and one left behind with no mount at all.
        ///     Returns what it removed for the log, or null when the pair was already sound.
        /// </summary>
        public static string? Reconcile(Equipment? equipment)
        {
            if (equipment == null) return null;

            var harness = equipment[EquipmentIndex.HorseHarness].Item;
            if (harness == null) return null;

            var mount = equipment[EquipmentIndex.Horse].Item;
            if (Fits(mount, harness)) return null;

            equipment[EquipmentIndex.HorseHarness] = default;
            return mount == null
                ? $"{harness.Name} was taken off: there is no mount to put it on"
                : $"{harness.Name} was taken off {mount.Name}: it is cut for another kind of animal";
        }
    }
}
