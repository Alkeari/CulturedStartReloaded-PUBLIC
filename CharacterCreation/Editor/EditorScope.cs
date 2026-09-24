using System;
using System.Linq;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     What a player is allowed to touch when the Start Editor stands in for
    ///     one narrative option. The story path asks a specific question, so the
    ///     editor must answer only that question: opened whole, it would let the
    ///     player rewrite everything the story just decided, and a single click
    ///     on the Presets tab would replace the character outright. Tabs outside
    ///     the scope are not rendered rather than disabled, because a control
    ///     that cannot apply should not be on screen.
    /// </summary>
    public sealed class EditorScope
    {
        /// <summary>The custom path: the whole editor, nothing withheld.</summary>
        public static readonly EditorScope Full = new(null, null);

        private EditorScope(string[]? tabs, string? titleKey)
        {
            Tabs = tabs;
            TitleKey = titleKey;
        }

        public static EditorScope For(string titleKey, params string[] tabs)
        {
            if (tabs == null || tabs.Length == 0)
                throw new ArgumentException("A scoped editor needs at least one tab.", nameof(tabs));
            return new EditorScope(tabs, titleKey);
        }

        /// <summary>Null while unrestricted; otherwise the only tabs that exist.</summary>
        public string[]? Tabs { get; }

        /// <summary>Names the question being answered, in place of "Start Editor".</summary>
        public string? TitleKey { get; }

        public bool IsScoped => Tabs != null;

        public bool Allows(string key) => Tabs == null || Tabs.Contains(key, StringComparer.Ordinal);
    }
}
