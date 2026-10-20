using TaleWorlds.Core;

namespace CulturedStartReloaded.Models
{
    /// <summary>
    ///     One gear slot's selection: a weapon class, optionally narrowed to an
    ///     exact item the player picked from the qualifying list.
    /// </summary>
    public sealed class GearChoice
    {
        public WeaponClassChoice WeaponClass { get; set; } = WeaponClassChoice.KeepAuto;

        /// <summary>The exact item chosen in the picker; null means quartermaster's pick.</summary>
        public ItemObject? ExactItem { get; set; }

        /// <summary>The player wants this slot left empty; overrides everything else.</summary>
        public bool ExplicitlyEmpty { get; set; }

        public bool IsKeepAuto => WeaponClass == WeaponClassChoice.KeepAuto && ExactItem == null && !ExplicitlyEmpty;

        public void Reset()
        {
            WeaponClass = WeaponClassChoice.KeepAuto;
            ExactItem = null;
            ExplicitlyEmpty = false;
        }

        public override string ToString()
        {
            return ExactItem != null
                ? $"{WeaponClass}:{ExactItem.StringId}"
                : WeaponClass.ToString();
        }
    }
}
