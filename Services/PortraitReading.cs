using System;
using System.Collections.Generic;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     A <see cref="Portrait"/> read back to the player in their own second
    ///     person, so the guided route can end by telling them who they turned out
    ///     to be without ever having asked.
    ///
    ///     Every sentence is derived, never authored per chapter, for the reason
    ///     <c>ChoiceEffects</c> derives its effect lines: an authored summary
    ///     goes stale the first time a chapter changes and nobody notices.
    ///
    ///     These are localization ids with their English fallback, in the mod's
    ///     usual "{=CSR_Key}English" form, and they are returned as strings rather
    ///     than resolved here: nothing in this file may touch a game type, because
    ///     it has to compile into the test project. The caller wraps each line in a
    ///     TextObject. Every id below needs its matching entry in
    ///     _Module/ModuleData/Languages/EN/sta_strings.xml, which is held at exact
    ///     parity with the code.
    /// </summary>
    public static class PortraitReading
    {
        /// <summary>
        ///     The seven facets in order, as the life ran. The station it earned
        ///     and the age it implies are asked for separately, by
        ///     <see cref="StationLine"/> and <see cref="AgeLine"/>, because those
        ///     two are what the player is about to see on the character and a
        ///     caller that wants to set them apart from the life that produced
        ///     them should not have to know which rows of one list they are.
        /// </summary>
        public static IReadOnlyList<string> Facets(Portrait? portrait)
        {
            var lines = new List<string>();
            if (portrait == null) return lines;

            foreach (Portrait.Facet facet in Enum.GetValues(typeof(Portrait.Facet)))
            {
                string line = Of(portrait, facet);
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }

            return lines;
        }

        /// <summary>What the answers said about one facet, in one sentence.</summary>
        public static string Of(Portrait? portrait, Portrait.Facet facet)
        {
            if (portrait == null) return string.Empty;

            var reach = portrait.Read(facet).Reach;

            switch (facet)
            {
                case Portrait.Facet.Origins:
                    return Band(reach,
                        "{=CSR_Portrait_Origins_Faint}You came from nothing anyone would think to name.",
                        "{=CSR_Portrait_Origins_Present}Your people were known in their own street and no further.",
                        "{=CSR_Portrait_Origins_Strong}You were born into a name that opened doors before you spoke.");

                case Portrait.Facet.Inclination:
                    return Band(reach,
                        "{=CSR_Portrait_Inclination_Faint}Nothing in you pulls much harder than anything else.",
                        "{=CSR_Portrait_Inclination_Present}You have a bent, and the people who know you can name it.",
                        "{=CSR_Portrait_Inclination_Strong}You are one thing all the way through, and everyone sees it coming.");

                case Portrait.Facet.Schooling:
                    return Band(reach,
                        "{=CSR_Portrait_Schooling_Faint}Nobody ever sat you down and taught you anything.",
                        "{=CSR_Portrait_Schooling_Present}You were taught enough to be useful and left to work out the rest.",
                        "{=CSR_Portrait_Schooling_Strong}You were taught properly, by someone who expected you to be good at it.");

                case Portrait.Facet.Mastery:
                    return Band(reach,
                        "{=CSR_Portrait_Mastery_Faint}You have done a little of everything and mastered none of it.",
                        "{=CSR_Portrait_Mastery_Present}There is work you do better than most of the people around you.",
                        "{=CSR_Portrait_Mastery_Strong}There is one thing you do better than almost anyone, and you know it.");

                case Portrait.Facet.Hinge:
                    return Band(reach,
                        "{=CSR_Portrait_Hinge_Faint}Your life ran on much as it began.",
                        "{=CSR_Portrait_Hinge_Present}Something happened that you still measure the years against.",
                        "{=CSR_Portrait_Hinge_Strong}Your life broke in half, and what came after has little to do with what came before.");

                case Portrait.Facet.Intent:
                    return Band(reach,
                        "{=CSR_Portrait_Intent_Faint}You ride out because staying was worse, not because you want anything.",
                        "{=CSR_Portrait_Intent_Present}You want something, though you would have to think before naming it.",
                        "{=CSR_Portrait_Intent_Strong}You know exactly what you are riding out to take.");

                case Portrait.Facet.Seasoning:
                    return Band(reach,
                        "{=CSR_Portrait_Seasoning_Faint}You have barely begun living.",
                        "{=CSR_Portrait_Seasoning_Present}You have lived enough to have stories worth the telling.",
                        "{=CSR_Portrait_Seasoning_Strong}You have lived a great deal of life, and it shows in your face.");

                default:
                    return string.Empty;
            }
        }

        /// <summary>The station the life earned, said as a starting position rather than a title.</summary>
        public static string StationLine(Portrait? portrait) =>
            portrait == null ? string.Empty : StationLine(portrait.Station);

        /// <summary>
        ///     The same line for a station the caller chose off
        ///     <see cref="Portrait.Standings"/> rather than the one the life earned,
        ///     which is what a caller needs when the earned station is one it cannot
        ///     offer: the sentence still has to be the one that station says of
        ///     itself, so there is one place it is written.
        /// </summary>
        public static string StationLine(StartType station)
        {
            switch (station)
            {
                case StartType.Monarch:
                    return "{=CSR_Portrait_Station_Monarch}You begin with a crown of your own making and people who answer to it.";
                case StartType.LandedVassal:
                    return "{=CSR_Portrait_Station_LandedVassal}You begin sworn to a king, with land under your name.";
                case StartType.LandlessVassal:
                    return "{=CSR_Portrait_Station_LandlessVassal}You begin sworn to a king, with nothing to offer but your service.";
                case StartType.Mercenary:
                    return "{=CSR_Portrait_Station_Mercenary}You begin with a sword for hire and no flag you are obliged to keep.";
                case StartType.Outlaw:
                    return "{=CSR_Portrait_Station_Outlaw}You begin outside the law, wanted by people who have not forgotten you.";
                case StartType.CaravanMaster:
                    return "{=CSR_Portrait_Station_CaravanMaster}You begin with goods on the road and everything you own moving with them.";
                case StartType.RebelClan:
                    return "{=CSR_Portrait_Station_RebelClan}You begin in open revolt, under a banner nobody gave you leave to fly.";
                default:
                    return "{=CSR_Portrait_Station_Commoner}You begin with no land, no lord and no banner: what you make of it now, you make with your own two hands.";
            }
        }

        /// <summary>
        ///     The age the life implies. Said in years lived rather than in a number,
        ///     because the route never puts a figure in front of the player.
        /// </summary>
        public static string AgeLine(Portrait? portrait)
        {
            if (portrait == null) return string.Empty;

            switch (portrait.Age)
            {
                case StartingAge.Adult:
                    return "{=CSR_Portrait_Age_Adult}You are grown, with a few hard years behind you.";
                case StartingAge.Mature:
                    return "{=CSR_Portrait_Age_Mature}You are past your first strength and well into your best judgment.";
                case StartingAge.Veteran:
                    return "{=CSR_Portrait_Age_Veteran}You are old for this work, and there is very little you have not already seen.";
                default:
                    return "{=CSR_Portrait_Age_Young}You are young enough that most of your life is still in front of you.";
            }
        }

        private static string Band(Portrait.Depth reach, string faint, string present, string strong)
        {
            switch (reach)
            {
                case Portrait.Depth.Strong: return strong;
                case Portrait.Depth.Present: return present;
                default: return faint;
            }
        }
    }
}
