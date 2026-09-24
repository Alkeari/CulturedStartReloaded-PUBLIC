using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;

namespace CulturedStartReloaded.CharacterCreation.Session
{
    /// <summary>The creation manager of the character creation in progress, or null when none is.</summary>
    public static class CreationStage
    {
        public static CharacterCreationManager? Manager =>
            (GameStateManager.Current?.ActiveState as CharacterCreationState)?.CharacterCreationManager;
    }
}
