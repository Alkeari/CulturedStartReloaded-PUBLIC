using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     Runs the start steps in order: all validations first, then each
    ///     surviving step with its own failure boundary. One in-game message
    ///     reports the outcome.
    /// </summary>
    public sealed class StartOrchestrator
    {
        private readonly IStartStep[] _steps =
        {
            new CultureStep(),
            new NarrativeStep(),
            new ResourceStep(),
            // Perks before companions: the companion limit is perk-dependent
            new PerkStep(),
            new ScenarioStep(),
            new EquipmentStep(),
            new TroopStep(),
            new CompanionStep(),
            new FamilyStep(),
            new LocationStep(),
            new ConsequenceStep(),
            new StoryModeStep(),
            new ConversionStep()
        };

        public void ApplyStart()
        {
            CSLogger.Info(">>> START: ApplyStart");

            var hero = Hero.MainHero;
            if (hero == null)
            {
                CSLogger.Error("ApplyStart aborted: MainHero is null.");
                return;
            }

            if (CreationSession.Current.Mode == Models.SetupMode.Vanilla)
            {
                CSLogger.Info("ApplyStart: vanilla route chosen; the start is left untouched.");
                return;
            }

            var context = new StartContext(
                hero,
                CreationSession.Current,
                GlobalSettings<CSSettings>.Instance,
                new StartReport());

            CSLogger.Info($"Hero: {hero.Name}, Culture: {hero.Culture?.StringId}, Clan: {hero.Clan?.Name}");
            CSLogger.Info(context.Session.DumpState());

            var skipped = new HashSet<IStartStep>();
            foreach (var step in _steps)
            {
                try
                {
                    var problem = step.Validate(context);
                    if (problem != null)
                    {
                        skipped.Add(step);
                        context.Report.AddProblem($"{step.Name} skipped: {problem}");
                    }
                }
                catch (Exception ex)
                {
                    skipped.Add(step);
                    context.Report.AddProblem($"{step.Name} skipped: validation threw {ex.GetType().Name}");
                    CSLogger.Error($"Validation of {step.Name} failed.", ex);
                }
            }

            foreach (var step in _steps)
            {
                if (skipped.Contains(step)) continue;

                try
                {
                    step.Apply(context);
                    CSLogger.Info($"Step {step.Name}: applied.");
                }
                catch (Exception ex)
                {
                    context.Report.AddProblem($"{step.Name} failed: {ex.GetType().Name}");
                    CSLogger.Error($"Step {step.Name} failed; continuing with remaining steps.", ex);
                }
            }

            context.Report.ShowInGame(context.Session);
            CSLogger.Info("<<< END: ApplyStart");
        }
    }
}
