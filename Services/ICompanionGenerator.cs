using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services
{
    public interface ICompanionGenerator
    {
        /// <summary>
        /// Generates the specified number of companions for the main hero.
        /// </summary>
        void GenerateCompanions(Hero mainHero, int count);
    }
}