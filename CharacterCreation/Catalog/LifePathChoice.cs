using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.Core;

namespace CulturedStartReloaded.CharacterCreation.Catalog
{
    /// <summary>
    ///     One selectable life-path option, defined exactly once. The menu builder
    ///     renders it and the apply pipeline reads it back, so the two can never
    ///     disagree about what a choice grants.
    /// </summary>
    public sealed class LifePathChoice
    {
        public LifePathChoice(string optionId, string title, string description)
        {
            OptionId = optionId;
            Title = title;
            Description = description;
        }

        public string OptionId { get; }

        /// <summary>Localized title: a localization tag followed by its English fallback.</summary>
        public string Title { get; }

        /// <summary>Localized description: a localization tag followed by its English fallback.</summary>
        public string Description { get; }

        /// <summary>
        ///     Skills this choice grants focus in. Resolved lazily because naval
        ///     skills only exist once the game objects are loaded. May resolve to
        ///     fewer skills when the naval DLC is absent.
        /// </summary>
        public Func<SkillObject[]> Skills { get; set; } = () => Array.Empty<SkillObject>();

        /// <summary>Attribute this choice grants one point in, if any.</summary>
        public Func<CharacterAttribute?> Attribute { get; set; } = () => null;

        /// <summary>Focus points granted per skill in <see cref="Skills"/>.</summary>
        public int FocusWeight { get; set; } = 1;

        /// <summary>Unspent focus points granted (age menu only).</summary>
        public int UnspentFocus { get; set; }

        /// <summary>Unspent attribute points granted (age menu only).</summary>
        public int UnspentAttribute { get; set; }

        /// <summary>
        ///     Naval skill id that must resolve for this option to appear, or null
        ///     when the option is always available.
        /// </summary>
        public string? RequiredNavalSkill { get; set; }

        /// <summary>
        ///     Session predicate deciding whether the option is currently shown
        ///     (evaluated per render), or null when always shown. Used for
        ///     culture-specific options.
        /// </summary>
        public Func<CharacterCreationSession, bool>? Condition { get; set; }

        /// <summary>Personality trait leanings this choice grants: (trait id, delta).</summary>
        public (string traitId, int delta)[] Traits { get; set; } = Array.Empty<(string, int)>();

        /// <summary>Who this background leaves an impression on at campaign start.</summary>
        public Models.RelationEffect Relations { get; set; } = Models.RelationEffect.None;

        /// <summary>The keepsake this background places in the starting inventory.</summary>
        public Models.HeirloomKind Heirloom { get; set; } = Models.HeirloomKind.None;

        /// <summary>Writes this selection into the session.</summary>
        public Action<CharacterCreationSession> Select { get; set; } = _ => { };

        /// <summary>True when the session currently holds this selection.</summary>
        public Func<CharacterCreationSession, bool> IsSelected { get; set; } = _ => false;
    }

    /// <summary>One life-path menu: its identity, text, and choices in order.</summary>
    public sealed class LifePathMenuDef
    {
        public LifePathMenuDef(string menuId, string title, string description, IReadOnlyList<LifePathChoice> choices)
        {
            MenuId = menuId;
            Title = title;
            Description = description;
            Choices = choices;
        }

        public string MenuId { get; }
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<LifePathChoice> Choices { get; }
    }
}
