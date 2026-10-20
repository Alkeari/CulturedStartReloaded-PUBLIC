using System.Collections.Generic;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Scenarios;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>Dispatches to the scenario applier for the selected start type.</summary>
    public sealed class ScenarioStep : IStartStep
    {
        private static readonly Dictionary<StartType, IScenarioApplier> Appliers = new()
        {
            [StartType.Monarch] = new MonarchScenario(),
            [StartType.LandedVassal] = new LandedVassalScenario(),
            [StartType.LandlessVassal] = new LandlessVassalScenario(),
            [StartType.Mercenary] = new MercenaryScenario(),
            [StartType.Commoner] = new CommonerScenario(),
            [StartType.Outlaw] = new OutlawScenario(),
            [StartType.CaravanMaster] = new CaravanMasterScenario(),
            [StartType.RebelClan] = new RebelClanScenario()
        };

        public string Name => "Scenario";

        public string? Validate(StartContext context)
        {
            if (!Appliers.TryGetValue(context.Session.SelectedStartType, out var applier))
                return $"unknown start type {context.Session.SelectedStartType}";

            var problem = applier.Validate(context);
            return problem == null ? null : $"{applier.Name}: {problem}";
        }

        public void Apply(StartContext context)
        {
            var session = context.Session;
            // Ahead of the applier, which hands over whatever holding the session
            // names: a start that took the water is seated at its port, and the
            // applier has to be handed that holding rather than the one the earlier
            // chapter left behind.
            SeaGrants.Settle(context);

            Appliers[session.SelectedStartType].Apply(context);

            // Ahead of the muster on purpose. TroopStep clamps what it raises to the
            // room the party limit leaves after what is already in the roster, so
            // hands that board here are counted into the muster the purse bought
            // rather than added on top of it, which is what the panel states.
            SeaGrants.Apply(context);

            WorkshopGrants.Apply(context);

            // The outlaw is on this list for the Start Editor's sake: it can hand him a
            // castle and an exact number of men to hold it, and a row nothing honors is a
            // dead control. GarrisonSetup returns at once when no figure was set, so the
            // guided route is untouched: its fief chapter stays closed to an outlawed lord,
            // who goes on inheriting whatever garrison the hall he never left already had.
            if (session.SelectedStartType is StartType.Monarch or StartType.LandedVassal
                or StartType.RebelClan or StartType.Outlaw)
                GarrisonSetup.Apply(context);

            if (session.SelectedKingdom != null && session.SelectedStartType is StartType.LandedVassal
                or StartType.LandlessVassal or StartType.Mercenary)
                RealmWarSetup.Apply(context, session.SelectedKingdom);
        }
    }
}
