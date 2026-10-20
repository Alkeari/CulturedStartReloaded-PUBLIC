using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>Places the party at the chosen (or fate-decided) starting settlement.</summary>
    public sealed class LocationStep : IStartStep
    {
        public string Name => "Location";

        public string? Validate(StartContext context)
        {
            return context.Hero.PartyBelongedTo == null
                ? "hero has no party"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var session = context.Session;
            var mainParty = hero.PartyBelongedTo;

            var target = session.SelectedSettlement
                         ?? session.SelectedLocation
                         ?? WhereTheLifeLeftYou(hero.Culture)
                         ?? SettlementFinder.RandomCultureTown(hero.Culture);

            if (target == null)
            {
                context.Report.AddProblem("Location: no settlement found to start at");
                return;
            }

            // Record the resolved town so later steps (relations, siblings) can
            // anchor to it even when fate decided
            if (session.SelectedSettlement == null && session.SelectedLocation == null)
                session.SelectedLocation = target;

            // Say so when the start begins among strangers. A culture that holds no land of its own
            // lands wherever fate reached, and the player should hear that from the start rather
            // than work it out from the map.
            if (hero.Culture != null && target.Culture != hero.Culture &&
                !Settlement.All.Any(s => s.IsTown && s.Culture == hero.Culture))
                context.Report.AddProblem(
                    $"Location: your people hold no town, so the story begins at {target.Name}");

            VersionedGameApi.PlaceParty(mainParty, target);
            CSLogger.Info($"LocationStep: party placed at {target.Name}.");

            // A sea start writes this party's position twice, and the order is
            // decided here rather than left to whichever step happens to run last.
            // This step decides WHERE the story begins; SetSailAtPosition decides
            // that it begins afloat, moving the party onto the port's own water and
            // putting it into the naval state in one call. So it goes last: run
            // first, the line above would drop a party that is at sea by state back
            // onto a road by position. The game's own naval start writes them in
            // this order too. A start that stayed ashore returns at once.
            Scenarios.SeaGrants.PutToSea(context, target);

            if (GameStateManager.Current?.ActiveState is MapState mapState)
            {
                mapState.Handler.ResetCamera(true, true);
                mapState.Handler.TeleportCameraToMainParty();
            }
        }

        /// <summary>
        ///     Where a guided life would have left the character standing, when
        ///     nothing was picked and the start would otherwise land at a random
        ///     town of the culture.
        ///
        ///     Only the kind of place is read, never a particular one: the scenes
        ///     name a village or a hall in the abstract and the map they are played
        ///     on is not the one they were written against. Null whenever the life
        ///     said nothing about where it was lived, and the caller then falls
        ///     back the way it always did.
        /// </summary>
        private static Settlement? WhereTheLifeLeftYou(CultureObject? culture)
        {
            try
            {
                if (!GuidedRun.WasWalked) return null;
                if (!BeginsInAVillage(GuidedRun.Outcome().Places)) return null;

                var villages = Settlement.All
                    .Where(s => s.IsVillage && culture != null && s.Culture == culture)
                    .ToList();
                var chosen = CSRandom.Pick(villages);

                if (chosen != null)
                    CSLogger.Info($"LocationStep: the life was lived out of doors, so it begins at {chosen.Name}.");

                return chosen;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"LocationStep: reading where the life left you failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Whether a life that named these places begins in a village of its
        ///     own culture, which is the whole of the rule and the only expression
        ///     of it.
        ///
        ///     A hall, a holding, a town and the open water all mean the same thing
        ///     here: the life reached past the countryside, so the beginning is left
        ///     to the station instead. The effect panel asks this of the answers
        ///     already given plus the one it is describing, so what a player is told
        ///     and what this step does cannot come apart. A second copy of the list
        ///     is exactly what let the panel promise a village to nine lives in ten
        ///     that never got one.
        /// </summary>
        internal static bool BeginsInAVillage(IEnumerable<string>? places)
        {
            if (places == null) return false;

            bool countryside = false;
            foreach (string place in places)
                switch (place)
                {
                    case "home_village":
                    case "open_country":
                    case "home_woodland":
                    case "nearest_hideout":
                        countryside = true;
                        break;
                    default:
                        return false;
                }

            return countryside;
        }
    }
}
