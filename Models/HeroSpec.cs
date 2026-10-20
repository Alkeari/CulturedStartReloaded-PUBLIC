using System.Collections.Generic;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Models
{
    /// <summary>
    ///     Advanced customization for one generated hero (a companion or a
    ///     family member): everything unset stays with the intelligent
    ///     generator's own rolls.
    /// </summary>
    public sealed class HeroSpec
    {
        /// <summary>The outfitter role name; null rolls randomly.</summary>
        public string? Role { get; set; }

        /// <summary>The name the character is called by; null keeps the generated one.</summary>
        public string? Name { get; set; }

        /// <summary>Female or male; null lets the template decide. A relative's relation decides it instead.</summary>
        public bool? IsFemale { get; set; }

        /// <summary>The culture id the character is drawn from; null draws from the player's own.</summary>
        public string? CultureId { get; set; }

        /// <summary>Exact level; null trails the player automatically.</summary>
        public int? Level { get; set; }

        /// <summary>Exact skill levels layered over the role's own.</summary>
        public Dictionary<string, int> SkillLevels { get; } = new();

        /// <summary>
        ///     Exact battle gear per slot: an item wins over the role loadout, a
        ///     null value empties the slot, a missing slot stays with the role.
        /// </summary>
        public Dictionary<EquipmentIndex, ItemObject?> Gear { get; } = new();

        /// <summary>Exact attribute values by attribute id; a missing one stays as generated.</summary>
        public Dictionary<string, int> Attributes { get; } = new();

        /// <summary>Exact trait levels by trait id; a missing one keeps the generated personality.</summary>
        public Dictionary<string, int> Traits { get; } = new();

        /// <summary>Exact focus points by skill id; a missing one stays as the role set it.</summary>
        public Dictionary<string, int> Focus { get; } = new();

        /// <summary>
        ///     Chosen perk ids by skill id: a skill listed takes exactly those perks, an empty list
        ///     none, and a skill not listed fills automatically.
        /// </summary>
        public Dictionary<string, List<string>> Perks { get; } = new();

        public bool HasCustomization =>
            Role != null || Name != null || IsFemale != null || CultureId != null || Level != null ||
            SkillLevels.Count > 0 || Gear.Count > 0 || Attributes.Count > 0 || Traits.Count > 0 ||
            Focus.Count > 0 || Perks.Count > 0;

        public void Clear()
        {
            Role = null;
            Name = null;
            IsFemale = null;
            CultureId = null;
            Level = null;
            SkillLevels.Clear();
            Gear.Clear();
            Attributes.Clear();
            Traits.Clear();
            Focus.Clear();
            Perks.Clear();
        }
    }
}
