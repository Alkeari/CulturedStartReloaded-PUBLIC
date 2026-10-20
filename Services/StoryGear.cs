using System;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     How well the story says this character is dressed.
    ///
    ///     A landed lord outranks a landless one, who outranks a merchant, who
    ///     outranks a commoner or a bandit, and none of that was expressible before:
    ///     gear came from a per-start-type pair of numbers in the settings, so a
    ///     commoner was capped and a monarch floored by constants that no choice
    ///     could move.
    ///
    ///     The item XML authors no price and no tier, and `difficulty` is zero on
    ///     every armor piece, which is why a survey of the data alone concludes
    ///     rank cannot be read. At RUNTIME it can: `ItemObject` exposes `Value`,
    ///     `Tierf` and `Appearance` as live computed properties, and `NotMerchandise`
    ///     marks the pieces the economy cannot produce. This reads those rather
    ///     than the XML, so a conversion that reprices its own items is followed
    ///     rather than second-guessed.
    /// </summary>
    public static class StoryGear
    {
        /// <summary>
        ///     Where the start type sits on the ladder the player would recognize,
        ///     as a share from nothing to everything. This is the only part of the
        ///     answer the story does not supply, because it is the station itself.
        /// </summary>
        private static double StationShare(StartType start)
        {
            return start switch
            {
                StartType.Monarch => 1.0,
                StartType.LandedVassal => 0.8,
                StartType.RebelClan => 0.65,
                StartType.LandlessVassal => 0.55,
                StartType.CaravanMaster => 0.45,
                StartType.Mercenary => 0.4,
                StartType.Commoner => 0.2,
                StartType.Outlaw => 0.15,
                _ => 0.3
            };
        }

        /// <summary>
        ///     How well dressed this character should be, from nothing to
        ///     everything. Station sets the floor of expectation and the life moves
        ///     it: a merchant's life dresses better than a forester's at the same
        ///     station, and a life that reached for standing dresses better again.
        /// </summary>
        public static double Standing(CharacterCreationSession session)
        {
            var profile = LifeProfile.From(session);

            double station = StationShare(session.SelectedStartType);
            double bearing = profile.Band(LifeProfile.Lean.Standing) / 4.0;
            double purse = profile.Band(LifeProfile.Lean.Commerce) / 4.0;

            // Station is what people expect of you; the life is what you actually
            // have. Neither alone is the answer, and the life can only move it so far
            double share = station * 0.6 + bearing * 0.25 + purse * 0.15;

            return Math.Max(0.0, Math.Min(1.0, share));
        }

        /// <summary>
        ///     How much better one piece is than another for someone of this
        ///     standing. High standing wants what is expensive and looks it; low
        ///     standing wants what a person could actually have come by, which is
        ///     why the merchandise flag matters at the bottom of the ladder and not
        ///     at the top.
        /// </summary>
        public static double Rank(ItemObject? item, double standing)
        {
            if (item == null) return 0;

            double show = item.Appearance;
            double worth = Math.Log(Math.Max(1, item.Value));
            double unbuyable = item.NotMerchandise ? 1 : 0;

            // Wanting what you could never have bought is a high-standing taste
            return standing * (worth + show + unbuyable * show)
                   + (1 - standing) * (-worth);
        }
    }
}
