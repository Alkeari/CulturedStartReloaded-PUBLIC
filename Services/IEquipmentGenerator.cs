using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services
{
    public interface IEquipmentGenerator
    {
        /// <summary>
        /// Generates a full battle equipment set for a hero based on tier and culture.
        /// </summary>
        void GenerateHeroEquipment(Hero hero, int targetTier);

        /// <summary>
        /// Generates a civilian outfit (civilian-flagged items) matching tier and culture.
        /// </summary>
        void GenerateCivilianEquipment(Hero hero, int targetTier);

        /// <summary>
        /// Generates a stealth outfit: light, unmarked pieces and no mount.
        /// </summary>
        void GenerateStealthEquipment(Hero hero, int targetTier);
    }
}
