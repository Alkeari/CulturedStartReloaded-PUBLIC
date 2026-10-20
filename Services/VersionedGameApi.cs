using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ModuleManager;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Game members this mod reaches through one place, so each call site names the member the same
    ///     way on every supported game version.
    /// </summary>
    public static class VersionedGameApi
    {
        public static float DistanceSquared(Settlement from, Settlement to) =>
            from.Position.DistanceSquared(to.Position);

        public static void PlaceParty(MobileParty party, Settlement target)
        {
            party.Position = target.GatePosition;
            party.SetMoveModeHold();
        }

        public static float Strength(Kingdom kingdom) => kingdom.CurrentTotalStrength;

        public static void SetBanner(Clan clan, Banner banner) => clan.Banner = banner;

        public static void SetBanner(Kingdom kingdom, Banner banner) => kingdom.Banner = banner;

        public static void ReleaseKingdomStay(Clan clan) => clan.ShouldStayInKingdomUntil = CampaignTime.Zero;

        public static void SetHomeSettlement(Clan clan, Settlement home) => clan.SetInitialHomeSettlement(home);

        public static void JoinAsMercenary(Clan clan, Kingdom kingdom, int pay) =>
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom, default(CampaignTime), pay);

        public static PolicyObject? LandGrantsForVeterans => DefaultPolicies.LandGrantsForVeteran;

        public static Equipment? StealthEquipment(Hero hero) => hero.StealthEquipment;

        public static bool IsStealthGear(ItemObject item) => (item.ItemFlags & ItemFlags.Stealth) != 0;

        public static CharacterAttribute[] AttributesOf(SkillObject skill) => skill.Attributes;

        public static GameEntity EmptyEntity(Scene scene) =>
            GameEntity.CreateEmpty(scene, isModifiableFromEditor: false, createPhysics: false,
                callScriptCallbacks: false);

        public static void ActivateStealthTutorial() => StoryModeEvents.Instance?.OnStealthTutorialActivated();

        public static bool IsModuleActive(string moduleId) => ModuleHelper.IsModuleActive(moduleId);

        public static void LoadSpriteCategory(string name) => UIResourceManager.LoadSpriteCategory(name);
    }
}
