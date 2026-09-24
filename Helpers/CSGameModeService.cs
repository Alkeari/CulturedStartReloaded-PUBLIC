using StoryMode;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Helpers
{
    public static class CSGameModeService
    {
        public static bool IsStoryMode()
        {
            return Game.Current?.GameType is CampaignStoryMode;
        }
    }
}
