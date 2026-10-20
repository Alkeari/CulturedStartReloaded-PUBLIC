using System;
using CulturedStartReloaded.CharacterCreation;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace CulturedStartReloaded.Behaviors
{
    public class CharacterCreationBehavior : CampaignBehaviorBase
    {
        private const int HandlerPriority = 850;

        private readonly CharacterCreationProvider _provider;
        private bool _isRegistered;

        public CharacterCreationBehavior(CharacterCreationProvider provider)
        {
            _provider = provider;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(
                this, OnCharacterCreationInitialized);
            CSLogger.Info("CharacterCreationBehavior: Registered OnCharacterCreationInitializedEvent listener.");
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnCharacterCreationInitialized(CharacterCreationManager manager)
        {
            CSLogger.Info(">>> START: OnCharacterCreationInitialized");

            if (_isRegistered)
            {
                CSLogger.Info("  Content handler already registered, skipping.");
                CSLogger.Info("<<< END: OnCharacterCreationInitialized [SKIPPED]");
                return;
            }

            try
            {
                CSLogger.Info($"  Registering content handler with priority {HandlerPriority}...");

                try
                {
                    manager.RegisterCharacterCreationContentHandler(_provider, HandlerPriority);
                    _isRegistered = true;
                    CSLogger.Info($"  Registered successfully at priority {HandlerPriority}.");
                }
                catch (ArgumentException ex)
                {
                    CSLogger.Warn($"  Priority collision at {HandlerPriority}: {ex.Message}");
                    CSLogger.Info($"  Retrying with priority {HandlerPriority + 7}...");
                    manager.RegisterCharacterCreationContentHandler(_provider, HandlerPriority + 7);
                    _isRegistered = true;
                    CSLogger.Info($"  Registered successfully at priority {HandlerPriority + 7}.");
                }

                CSLogger.Info("<<< END: OnCharacterCreationInitialized [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: OnCharacterCreationInitialized [FAILED]", ex);
            }
        }
    }
}
