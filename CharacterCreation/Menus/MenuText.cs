using System;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Helpers for menu copy that has to quote live numbers.
    /// </summary>
    public static class MenuText
    {
        /// <summary>
        ///     The menu API takes an option's description once, at registration,
        ///     but the numbers worth quoting are not known until the player has
        ///     picked a culture and a start type. The option's condition is the
        ///     only hook the API evaluates on every render, so the live values
        ///     are pushed into the TextObject from there and the description
        ///     reads correctly whenever it is shown.
        /// </summary>
        public static NarrativeMenuOptionOnConditionDelegate Live(TextObject description,
            Action<TextObject> refresh, Func<bool>? visible = null)
        {
            return _ =>
            {
                try
                {
                    refresh(description);
                }
                catch (Exception ex)
                {
                    CSLogger.Error("MenuText: live description refresh failed.", ex);
                }

                return visible?.Invoke() ?? true;
            };
        }

        /// <summary>
        ///     Records the choice a chapter settled, so the epilogue can read the
        ///     whole composed life back rather than only the scenario beats. The
        ///     key is the chapter's own menu id, which is what the epilogue walks.
        /// </summary>
        public static void Remember(string menuId, string titleKey)
        {
            Session.CreationSession.Current.StoryBeats[menuId] = new TextObject(titleKey).ToString();
        }

        /// <summary>A count with its own singular and plural wording.</summary>
        public static string Count(int value, string singularKey, string pluralKey)
        {
            var text = new TextObject(value == 1 ? singularKey : pluralKey);
            text.SetTextVariable("COUNT", value);
            return text.ToString();
        }

    }
}
