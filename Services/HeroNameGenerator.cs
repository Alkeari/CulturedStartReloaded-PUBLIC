using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Names for the player and their clan, drawn from the game's own per-culture
    ///     pools rather than a list of this mod's own invention. The base game
    ///     declares 1017 male, 593 female and 149 clan names across its cultures, and
    ///     a conversion declares its own, so a name composed this way belongs to
    ///     whatever world is loaded. Both are offered as candidates and both can be
    ///     overruled by hand, in either route, because a name is the one thing
    ///     nobody should be made to accept.
    /// </summary>
    public static class HeroNameGenerator
    {
        private const int CandidateCount = 5;
        private const int MaxAttempts = 40;

        /// <summary>
        ///     Personal names on offer for this character, composed once and then
        ///     held, so the list does not reshuffle under the player between looks.
        /// </summary>
        public static IReadOnlyList<string> FirstNameCandidates(CharacterCreationSession? session)
        {
            if (session == null) return Array.Empty<string>();
            if (session.ComposedFirstNames.Count > 0) return session.ComposedFirstNames;

            foreach (var name in ComposeFirstNames(session))
                session.ComposedFirstNames.Add(name);

            return session.ComposedFirstNames;
        }

        /// <summary>Clan names on offer, from the culture's own naming customs.</summary>
        public static IReadOnlyList<string> ClanNameCandidates(CharacterCreationSession? session)
        {
            if (session == null) return Array.Empty<string>();
            if (session.ComposedClanNames.Count > 0) return session.ComposedClanNames;

            foreach (var name in ComposeClanNames(session))
                session.ComposedClanNames.Add(name);

            return session.ComposedClanNames;
        }

        public static string? FirstNamePreview(CharacterCreationSession? session)
        {
            return session?.PlayerFirstName ?? FirstNameCandidates(session).FirstOrDefault();
        }

        public static string? ClanNamePreview(CharacterCreationSession? session)
        {
            return session?.PlayerClanName ?? ClanNameCandidates(session).FirstOrDefault();
        }

        private static List<string> ComposeFirstNames(CharacterCreationSession session)
        {
            var culture = session.SelectedCulture ?? Hero.MainHero?.Culture;
            return culture == null
                ? new List<string>()
                : ComposeFirstNames(culture, Hero.MainHero?.IsFemale ?? false);
        }

        /// <summary>Personal names from one culture's pool for one sex, for any character.</summary>
        public static List<string> ComposeFirstNames(CultureObject culture, bool female)
        {
            var names = new List<string>();

            try
            {
                for (int attempt = 0; attempt < MaxAttempts && names.Count < CandidateCount; attempt++)
                {
                    var candidate = NameGenerator.Current?
                        .GenerateFirstNameForPlayer(culture, female)?.ToString();
                    if (string.IsNullOrWhiteSpace(candidate)) break;
                    if (names.Any(n => string.Equals(n, candidate, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    names.Add(candidate!);
                }

                CSLogger.Info($"HeroNameGenerator: {names.Count} personal name(s) for " +
                              $"{culture.StringId} ({(female ? "female" : "male")}).");
            }
            catch (Exception ex)
            {
                CSLogger.Error("HeroNameGenerator: composing personal names failed.", ex);
            }

            return names;
        }

        private static List<string> ComposeClanNames(CharacterCreationSession session)
        {
            var names = new List<string>();

            try
            {
                var culture = session.SelectedCulture ?? Hero.MainHero?.Culture;
                if (culture == null) return names;

                var seat = Seat(session);
                var taken = TakenClanNames();

                for (int attempt = 0; attempt < MaxAttempts && names.Count < CandidateCount; attempt++)
                {
                    var candidate = NameGenerator.Current?.GenerateClanName(culture, seat)?.ToString();
                    if (string.IsNullOrWhiteSpace(candidate)) break;
                    if (taken.Contains(candidate!)) continue;
                    if (names.Any(n => string.Equals(n, candidate, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    names.Add(candidate!);
                }

                CSLogger.Info($"HeroNameGenerator: {names.Count} clan name(s) for {culture.StringId} " +
                              $"(seat {seat?.Name?.ToString() ?? "none"}).");
            }
            catch (Exception ex)
            {
                CSLogger.Error("HeroNameGenerator: composing clan names failed.", ex);
            }

            return names;
        }

        /// <summary>
        ///     A culture's clan names can be templates keyed on where the clan came
        ///     from, so the seat matters: Vlandia's single entry is "dey
        ///     {ORIGIN_SETTLEMENT}". The holding wins, then wherever the start puts
        ///     the player, so the name reads as belonging somewhere real.
        /// </summary>
        private static Settlement? Seat(CharacterCreationSession session)
        {
            return session.SelectedSettlement
                   ?? session.SelectedLocation
                   ?? Hero.MainHero?.HomeSettlement;
        }

        private static HashSet<string> TakenClanNames()
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
                CSLogger.Warn($"HeroNameGenerator: clan roster unreadable ({ex.GetType().Name}).");
            }

            return names;
        }

        /// <summary>Strips what a name may not carry, since these reach TextObject.</summary>
        public static string? Clean(string? typed)
        {
            var cleaned = (typed ?? string.Empty).Replace("{", "").Replace("}", "").Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        public static TextObject AsText(string name) => new TextObject("{=!}" + name);
    }
}
