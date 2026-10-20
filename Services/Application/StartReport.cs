using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     Collects per-step problems during start application and reports the
    ///     outcome to the player once, at the end, instead of failing silently or
    ///     leaving a half-applied campaign unexplained.
    /// </summary>
    public sealed class StartReport
    {
        private readonly List<string> _problems = new();

        public bool HasProblems => _problems.Count > 0;

        public void AddProblem(string detail)
        {
            _problems.Add(detail);
            CSLogger.Warn($"StartReport: {detail}");
        }

        /// <summary>
        ///     What the player lost, per area, in their own words. The stored
        ///     detail is written for the log; a player cannot act on
        ///     "child creation returned nothing" and should not be handed it.
        /// </summary>
        private static readonly Dictionary<string, string> AreaMessages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Family"] = "{=CSR_Fallback_Family}Some of your relatives could not be created.",
            ["Companion"] = "{=CSR_Fallback_Companion}Some of your companions could not join you.",
            ["Troop"] = "{=CSR_Fallback_Troop}Part of your warband could not be raised.",
            ["Equipment"] = "{=CSR_Fallback_Equipment}Some of your equipment could not be issued.",
            ["Resource"] = "{=CSR_Fallback_Resource}Some of your starting resources could not be granted.",
            ["Location"] = "{=CSR_Fallback_Location}You could not be placed where you chose, so you begin nearby.",
            ["Garrison"] = "{=CSR_Fallback_Garrison}Your holding could not be garrisoned.",
            ["Monarch"] = "{=CSR_Fallback_Realm}Part of your realm could not be founded as you set it.",
            ["Landed Vassal"] = "{=CSR_Fallback_Standing}Part of your standing could not be granted.",
            ["Rebel Clan"] = "{=CSR_Fallback_Rising}Part of your rising could not be arranged.",
            ["Scenario"] = "{=CSR_Fallback_Standing}Part of your standing could not be granted.",
            ["Culture"] = "{=CSR_Fallback_Culture}Your culture could not be applied in full.",
            ["Narrative"] = "{=CSR_Fallback_Narrative}Some of your life path could not be applied.",
            ["Consequence"] = "{=CSR_Fallback_Narrative}Some of your life path could not be applied.",
            ["StoryMode"] = "{=CSR_Fallback_Story}The main story could not be advanced to the point you chose."
        };

        /// <summary>
        ///     On success the map opens on the player's own story, which is what
        ///     the epilogue promised them. On failure it names what fell back,
        ///     because a count and a pointer at an unnamed log file is not
        ///     something anyone can act on.
        /// </summary>
        public void ShowInGame(CharacterCreationSession session)
        {
            if (!HasProblems)
            {
                foreach (var line in SummaryLines(session))
                    InformationManager.DisplayMessage(new InformationMessage(line));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=CSR_StartPartial}Your start was applied, with some parts falling back:")
                    .ToString(),
                Colors.Yellow));

            foreach (var message in PlayerMessages())
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
        }

        private static IEnumerable<string> SummaryLines(CharacterCreationSession session)
        {
            string summary;
            try
            {
                summary = StorySummary.Build(session);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartReport: story summary failed; falling back to the plain line.", ex);
                return new[] { new TextObject("{=CSR_StartApplied}Cultured Start applied successfully.").ToString() };
            }

            var lines = summary
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            return lines.Count > 0
                ? lines
                : new List<string>
                    { new TextObject("{=CSR_StartApplied}Cultured Start applied successfully.").ToString() };
        }

        // One line per affected area, however many times that area failed
        private IEnumerable<string> PlayerMessages()
        {
            return _problems
                .Select(AreaOf)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(area => AreaMessages.TryGetValue(area, out var key)
                    ? key
                    : "{=CSR_Fallback_Other}Part of your start could not be applied.")
                .Distinct(StringComparer.Ordinal)
                .Select(key => new TextObject(key).ToString())
                .ToList();
        }

        private static string AreaOf(string detail)
        {
            int colon = detail.IndexOf(':');
            if (colon > 0) return detail.Substring(0, colon).Trim();

            int space = detail.IndexOf(' ');
            return space > 0 ? detail.Substring(0, space).Trim() : detail.Trim();
        }
    }
}
