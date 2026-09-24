using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Names a clan the way the game names its own. The engine's generator reads
    ///     the culture's declared clan names, which are a naming convention rather
    ///     than a word list: the Empire's are Greek patronymics, Battania's are
    ///     prefixed "fen", and Vlandia's single entry is the template "dey
    ///     {ORIGIN_SETTLEMENT}". A conversion supplies its own, so this is correct
    ///     in Westeros for the same reason it is correct in Calradia.
    ///
    ///     A small culture pool runs out, and Vlandia's one template repeats for
    ///     every clan raised from the same seat, so a name already in use is
    ///     refused and the house-of form is the last resort rather than the first.
    /// </summary>
    public static class ClanNameGenerator
    {
        private const int MaxAttempts = 12;

        public static TextObject Compose(CultureObject? culture, Settlement? home, Hero leader)
        {
            var taken = TakenNames();

            for (int attempt = 0; attempt < MaxAttempts && culture != null; attempt++)
            {
                var candidate = FromEngine(culture, home);
                if (candidate == null) break;

                var rendered = candidate.ToString();
                if (string.IsNullOrWhiteSpace(rendered)) break;
                if (taken.Contains(rendered)) continue;

                CSLogger.Info($"ClanNameGenerator: named a clan '{rendered}' " +
                              $"({culture.StringId}, seat {home?.Name?.ToString() ?? "none"}).");
                return candidate;
            }

            var fallback = new TextObject("{=CSR_VassalClanName}House of {LEADER}");
            fallback.SetTextVariable("LEADER", leader.FirstName ?? leader.Name);
            CSLogger.Info($"ClanNameGenerator: fell back to '{fallback}'; the culture's clan names " +
                          "were exhausted or unavailable.");
            return fallback;
        }

        private static TextObject? FromEngine(CultureObject culture, Settlement? home)
        {
            try
            {
                return NameGenerator.Current?.GenerateClanName(culture, home);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ClanNameGenerator: the game's clan naming refused " +
                              $"({ex.GetType().Name}); using the house-of form.");
                return null;
            }
        }

        private static HashSet<string> TakenNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var clan in Clan.All)
                {
                    var name = clan?.Name?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name!);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ClanNameGenerator: clan roster unreadable ({ex.GetType().Name}).");
            }

            return names;
        }
    }
}
