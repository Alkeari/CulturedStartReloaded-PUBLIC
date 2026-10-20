using System;
using System.Text;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The paragraph a realm carries on its encyclopedia page. Every kingdom the
    ///     base game ships has one, five hundred to seven hundred characters of
    ///     chronicle: who founded it, on what claim, how its crown and its nobility
    ///     regard one another, and what is unresolved about it. A realm with a blank
    ///     page is the clearest sign a player's kingdom was not part of the world,
    ///     so this composes one in the same register out of what the founding
    ///     actually was.
    /// </summary>
    public static class RealmLore
    {
        private static readonly string[] ClaimantOpenings =
        {
            "{=CSR_Lore_Claim1}The crown of {REALM} descends from {RULER} {FOUNDER}, who pressed an old claim to {SEAT} and took it by force of arms rather than by any court's leave.",
            "{=CSR_Lore_Claim2}{REALM} was proclaimed over {SEAT} by {RULER} {FOUNDER}, whose claim was older than the holding of it and was settled, in the end, with steel.",
            "{=CSR_Lore_Claim3}{RULER} {FOUNDER} of the {CLAN} took {SEAT} on a claim the dispossessed have never accepted, and was crowned there before the year was out."
        };

        private static readonly string[] SettlerOpenings =
        {
            "{=CSR_Lore_Settle1}The crown of {REALM} was raised at {SEAT} by {RULER} {FOUNDER}, in lands a weakening realm had let slip from its grasp, and few were placed to object.",
            "{=CSR_Lore_Settle2}{REALM} began as a holding at {SEAT} that {RULER} {FOUNDER} governed in all but name, until the name was taken as well.",
            "{=CSR_Lore_Settle3}{RULER} {FOUNDER} of the {CLAN} declared {REALM} from {SEAT}, on ground no one else was defending, and dared the neighbors to say otherwise."
        };

        private static readonly string[] WithVassals =
        {
            "{=CSR_Lore_Sworn1}The houses that rode with the founder were recognized as lords in the same season, holding their lands in exchange for rallying to the banner in war.",
            "{=CSR_Lore_Sworn2}Those who swore early were granted lands and titles at once, and their descendants have never let the crown forget which came first.",
            "{=CSR_Lore_Sworn3}Its nobility was made rather than inherited, raised in a single season from the companions of the founding."
        };

        private static readonly string[] WithoutVassals =
        {
            "{=CSR_Lore_Alone1}No great house stood with the founder at the proclamation; the realm rests on one clan's strength and on whoever can be persuaded to join it.",
            "{=CSR_Lore_Alone2}It has no old nobility to speak of, which its ruler counts a freedom and its neighbors count a weakness.",
            "{=CSR_Lore_Alone3}The founding clan holds the realm alone, and every lord it gains will have to be won rather than inherited."
        };

        private static readonly string[] CultureLines =
        {
            "{=CSR_Lore_Culture1}Its people keep the customs of the {CULTURE}, and its court keeps them more strictly than most.",
            "{=CSR_Lore_Culture2}It inherited the law and the quarrels of the {CULTURE} along with the land.",
            "{=CSR_Lore_Culture3}In speech, in worship and in the ordering of a hall it is {CULTURE} through and through, whatever the map now says."
        };

        private static readonly string[] Closings =
        {
            "{=CSR_Lore_Close1}Whether it outlives its founder is a question its neighbors have already begun to ask aloud.",
            "{=CSR_Lore_Close2}What holds it together is recent, and everyone within it knows the difference between a crown and a custom.",
            "{=CSR_Lore_Close3}Its borders are drawn in the ink of a single reign, and no one yet knows how deeply that ink will set."
        };

        /// <summary>
        ///     The realm's page, or null when the founding is too bare to describe
        ///     honestly, in which case the game's own default is better than a
        ///     paragraph of hedging.
        /// </summary>
        public static TextObject? Compose(CharacterCreationSession? session, Hero founder,
            string? realmName)
        {
            if (session == null || string.IsNullOrWhiteSpace(realmName)) return null;

            try
            {
                var seat = session.SelectedSettlement?.Name?.ToString();
                if (string.IsNullOrWhiteSpace(seat)) return null;

                var culture = (session.SelectedCulture ?? founder.Culture)?.Name?.ToString();
                var rulerTitle = KingdomNameGenerator.BuildRealm(session, realmName)?.RulerTitle
                                 ?? string.Empty;

                bool claimed = session.SelectedFounding == MonarchFounding.Claimant;
                var opening = CSRandom.Pick(claimed ? ClaimantOpenings : SettlerOpenings)
                              ?? (claimed ? ClaimantOpenings[0] : SettlerOpenings[0]);
                bool sworn = session.VassalClanCount > 0;
                var nobility = CSRandom.Pick(sworn ? WithVassals : WithoutVassals)
                               ?? (sworn ? WithVassals[0] : WithoutVassals[0]);

                var story = new StringBuilder();
                story.Append(Line(opening, realmName!, seat!, founder, rulerTitle, culture));
                story.Append(' ');
                story.Append(Line(nobility, realmName!, seat!, founder, rulerTitle, culture));

                if (!string.IsNullOrWhiteSpace(culture))
                {
                    var cultureLine = CSRandom.Pick(CultureLines) ?? CultureLines[0];
                    story.Append(' ');
                    story.Append(Line(cultureLine, realmName!, seat!, founder, rulerTitle, culture));
                }

                var closing = CSRandom.Pick(Closings) ?? Closings[0];
                story.Append(' ');
                story.Append(Line(closing, realmName!, seat!, founder, rulerTitle, culture));

                var text = story.ToString();
                CSLogger.Info($"RealmLore: composed {text.Length} characters for {realmName}.");
                return new TextObject("{=!}" + text);
            }
            catch (Exception ex)
            {
                CSLogger.Error("RealmLore: composing the realm's history failed.", ex);
                return null;
            }
        }

        private static string Line(string template, string realm, string seat, Hero founder,
            string rulerTitle, string? culture)
        {
            var text = new TextObject(template);
            text.SetTextVariable("REALM", realm);
            text.SetTextVariable("SEAT", seat);
            text.SetTextVariable("RULER", rulerTitle);
            text.SetTextVariable("FOUNDER", founder.FirstName?.ToString() ?? founder.Name?.ToString() ?? string.Empty);
            text.SetTextVariable("CLAN", founder.Clan?.Name?.ToString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(culture)) text.SetTextVariable("CULTURE", culture);
            return text.ToString();
        }
    }
}
