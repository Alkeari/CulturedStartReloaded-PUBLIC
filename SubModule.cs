using System;
using CulturedStartReloaded.Behaviors;
using CulturedStartReloaded.CharacterCreation;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using CulturedStartReloaded.Services.Application;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CulturedStartReloaded
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "mod.bannerlord.culturedstartreloaded";
        private Harmony? _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            CSLogger.Initialize(this);
            CSLogger.Info(">>> START: OnSubModuleLoad");

            try
            {
                _harmony = new Harmony(HarmonyId);
                ApplyPatches(_harmony);
                CSLogger.Info("<<< END: OnSubModuleLoad [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: OnSubModuleLoad [FAILED]", ex);
            }
        }

        /// <summary>
        ///     Applies each patch class on its own. Harmony's PatchAll stops at the first class it
        ///     cannot process, so one patch whose target is missing took every other patch with it,
        ///     including the menu routing the mod cannot work without. That is not hypothetical: the
        ///     BETA build's Advanced Starting Options patches target a type no v1.4.x game has, and
        ///     a player who installed the BETA download on v1.4.x got a mod that loaded and patched
        ///     nothing at all. A patch the mod can live without now fails alone and says so.
        /// </summary>
        private static void ApplyPatches(Harmony harmony)
        {
            int applied = 0, failed = 0;
            foreach (var type in LoadablePatchTypes())
            {
                try
                {
                    var processor = harmony.CreateClassProcessor(type);
                    if (processor.Patch() is { Count: > 0 }) applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    CSLogger.Error($"  Harmony patch class {type.Name} could not be applied.", ex);
                }
            }

            // Targets that exist only on some game versions cannot be named in an attribute, so
            // they are resolved and applied after the classes that can be.
            GameCompat.PatchAdvancedStartOptions(harmony, typeof(Patches.StartOptionsPatch));
            TaomCompat.PatchCareerFastPath(harmony, typeof(Patches.TaomCareerFastPathPatch));

            CSLogger.Info($"  Harmony patches applied: {applied} class(es), {failed} failed.");
        }

        /// <summary>
        ///     The patch classes in this assembly that the runtime can actually load. GetTypes
        ///     throws outright when any type in the assembly cannot be resolved, which is the very
        ///     case this needs to survive: on a game missing an API one patch binds, the whole
        ///     listing would fail and no patch would be applied. The exception carries the types it
        ///     did load, so those are used and the rest are named in the log.
        ///
        ///     Only types actually carrying [HarmonyPatch] are handed on. Harmony treats a method
        ///     named Prepare, Cleanup or TargetMethod on any class it is given as its own lifecycle
        ///     hook and calls it statically, so handing it every type in the assembly made it try to
        ///     call this mod's own EquipmentPreviewService.Cleanup(), an ordinary instance method,
        ///     and report the class as unpatchable. Nothing was lost, because that class declares no
        ///     patch, but a class of ours could have been run at load time for no reason.
        /// </summary>
        private static Type[] LoadablePatchTypes()
        {
            Type[] types;
            try
            {
                types = typeof(SubModule).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                CSLogger.Warn(
                    $"  {ex.LoaderExceptions?.Length ?? 0} type(s) in this build could not be loaded " +
                    "against the running game; the patches that do fit are still applied.");
                types = Array.FindAll(ex.Types, t => t != null)!;
            }

            return Array.FindAll(types, t => t.IsDefined(typeof(HarmonyPatch), false));
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            CSLogger.Info(">>> START: OnBeforeInitialModuleScreenSetAsRoot");
            CSLogger.ResolveLogger(this);
            CSLogger.Info("<<< END: OnBeforeInitialModuleScreenSetAsRoot [SUCCESS]");
        }

        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {
            base.InitializeGameStarter(game, gameStarterObject);

            CSLogger.Info(">>> START: InitializeGameStarter");
            CSLogger.Info($"  GameType: {game.GameType?.GetType().Name ?? "null"}");

            if (!CSSettings.IsModEnabled)
            {
                CSLogger.Info("  Mod disabled in MCM; behaviors not registered.");
                CSLogger.Info("<<< END: InitializeGameStarter [DISABLED]");
                return;
            }

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
            {
                CSLogger.Info("  Campaign game detected, registering behaviors...");

                try
                {
                    // Fresh session and RNG for the new game
                    CreationSession.StartNew();
                    CSRandom.Reseed();
                    CSLogger.Info("  New creation session started.");

                    var provider = new CharacterCreationProvider();
                    campaignStarter.AddBehavior(new CharacterCreationBehavior(provider));
                    CSLogger.Info("  CharacterCreationBehavior added.");

                    campaignStarter.AddBehavior(new CulturedStartBehavior(new StartOrchestrator()));
                    CSLogger.Info("  CulturedStartBehavior added.");

                    CSLogger.Info("<<< END: InitializeGameStarter [SUCCESS]");
                }
                catch (Exception ex)
                {
                    CSLogger.Error("<<< END: InitializeGameStarter [FAILED]", ex);
                }
            }
            else
            {
                CSLogger.Info($"  Not a Campaign game: skipping behavior registration.");
                CSLogger.Info("<<< END: InitializeGameStarter [SKIPPED]");
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            CSLogger.Info(">>> START: OnSubModuleUnloaded");
            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                CSLogger.Info("  Harmony patches removed.");
            }
            CSLogger.Info("<<< END: OnSubModuleUnloaded [SUCCESS]");
        }
    }
}
