using System;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace CulturedStartReloaded.Settings
{
    public class CSSettings : AttributeGlobalSettings<CSSettings>
    {
        public override string Id => "CulturedStartReloaded";
        public override string DisplayName => "Cultured Start Reloaded";
        public override string FolderName => "CulturedStartReloaded";
        public override string FormatType => "json";

        #region General

        [SettingPropertyBool("{=CSR_EnableMod}Enable Mod", Order = 0, RequireRestart = false,
            HintText = "{=CSR_EnableMod_Hint}Offers the Vanilla Start, Cultured Start and Start Editor routes when on; the change takes effect the next time you start a campaign.")]
        [SettingPropertyGroup("{=CSR_General}General", GroupOrder = 100)]
        public bool EnableMod { get; set; } = true;

        [SettingPropertyBool("{=CSR_VanillaSkills}Use Vanilla Skill Levels", Order = 1, RequireRestart = false,
            HintText = "{=CSR_VanillaSkills_Hint}When on, your life path grants focus and attributes only and your skill levels stay exactly as vanilla sets them.")]
        [SettingPropertyGroup("{=CSR_General}General", GroupOrder = 100)]
        public bool VanillaSkillLevels { get; set; } = false;

        [SettingPropertyBool("{=CSR_ModdedItems}Choose from Modded Content", Order = 2, RequireRestart = false,
            HintText = "{=CSR_ModdedItems_Hint}Include items added by other mods in the gear pickers and generated equipment; modded items can clip or render incorrectly.")]
        [SettingPropertyGroup("{=CSR_General}General", GroupOrder = 100)]
        public bool AllowModdedItems { get; set; } = false;

        #endregion

        #region Monarch

        [SettingPropertyBool("{=CSR_EnableMonarch}Enable Monarch", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableMonarch_Hint}Offer the Monarch start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public bool EnableMonarch { get; set; } = true;

        [SettingPropertyInteger("{=CSR_MonarchClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_MonarchClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchClanTierMin { get; set; } = 4;

        [SettingPropertyInteger("{=CSR_MonarchClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_MonarchClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchClanTierMax { get; set; } = 6;

        [SettingPropertyInteger("{=CSR_MonarchEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_MonarchEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchEquipmentTierMin { get; set; } = 5;

        [SettingPropertyInteger("{=CSR_MonarchEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_MonarchEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchEquipmentTierMax { get; set; } = 6;

        [SettingPropertyInteger("{=CSR_MonarchGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_MonarchGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchGoldMin { get; set; } = 100000;

        [SettingPropertyInteger("{=CSR_MonarchGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_MonarchGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchGoldMax { get; set; } = 150000;

        [SettingPropertyInteger("{=CSR_MonarchInfluenceMin}Influence: Lowest Band", 0, GameCaps.SettingsMaxInfluence, Order = 7, RequireRestart = false,
            HintText = "{=CSR_MonarchInfluenceMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchInfluenceMin { get; set; } = 500;

        [SettingPropertyInteger("{=CSR_MonarchInfluenceMax}Influence: Highest Band", 0, GameCaps.SettingsMaxInfluence, Order = 8, RequireRestart = false,
            HintText = "{=CSR_MonarchInfluenceMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchInfluenceMax { get; set; } = 1000;

        [SettingPropertyInteger("{=CSR_MonarchTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 9, RequireRestart = false,
            HintText = "{=CSR_MonarchTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchTroopsMin { get; set; } = 60;

        [SettingPropertyInteger("{=CSR_MonarchTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 10, RequireRestart = false,
            HintText = "{=CSR_MonarchTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchTroopsMax { get; set; } = 120;

        [SettingPropertyInteger("{=CSR_MonarchCastleCount}Additional Castles", -1, 5, Order = 11, RequireRestart = false,
            HintText = "{=CSR_MonarchCastleCount_Hint}Castles seized from their current owners alongside the capital; every realm you take one from begins the game dispossessed. At -1 your founding decides: a seat for each house you granted land, plus your own holdings for high standing.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Monarch}Monarch", GroupOrder = 17)]
        public int MonarchCastleCount { get; set; } = -1;

        #endregion

        #region Landed Vassal

        [SettingPropertyBool("{=CSR_EnableLandedVassal}Enable Landed Vassal", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableLandedVassal_Hint}Offer the Landed Vassal start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public bool EnableLandedVassal { get; set; } = true;

        [SettingPropertyInteger("{=CSR_LVClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_LVClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalClanTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_LVClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_LVClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalClanTierMax { get; set; } = 5;

        [SettingPropertyInteger("{=CSR_LVEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_LVEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalEquipmentTierMin { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_LVEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_LVEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalEquipmentTierMax { get; set; } = 5;

        [SettingPropertyInteger("{=CSR_LVGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_LVGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalGoldMin { get; set; } = 20000;

        [SettingPropertyInteger("{=CSR_LVGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_LVGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalGoldMax { get; set; } = 40000;

        [SettingPropertyInteger("{=CSR_LVInfluenceMin}Influence: Lowest Band", 0, GameCaps.SettingsMaxInfluence, Order = 7, RequireRestart = false,
            HintText = "{=CSR_LVInfluenceMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalInfluenceMin { get; set; } = 100;

        [SettingPropertyInteger("{=CSR_LVInfluenceMax}Influence: Highest Band", 0, GameCaps.SettingsMaxInfluence, Order = 8, RequireRestart = false,
            HintText = "{=CSR_LVInfluenceMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalInfluenceMax { get; set; } = 200;

        [SettingPropertyInteger("{=CSR_LVTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 9, RequireRestart = false,
            HintText = "{=CSR_LVTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalTroopsMin { get; set; } = 30;

        [SettingPropertyInteger("{=CSR_LVTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 10, RequireRestart = false,
            HintText = "{=CSR_LVTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandedVassal}Landed Vassal", GroupOrder = 16)]
        public int LandedVassalTroopsMax { get; set; } = 60;

        #endregion

        #region Landless Vassal

        [SettingPropertyBool("{=CSR_EnableLandlessVassal}Enable Landless Vassal", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableLandlessVassal_Hint}Offer the Landless Vassal start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public bool EnableLandlessVassal { get; set; } = true;

        [SettingPropertyInteger("{=CSR_LLVClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_LLVClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalClanTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_LLVClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_LLVClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalClanTierMax { get; set; } = 5;

        [SettingPropertyInteger("{=CSR_LLVEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_LLVEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalEquipmentTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_LLVEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_LLVEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalEquipmentTierMax { get; set; } = 4;

        [SettingPropertyInteger("{=CSR_LLVGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_LLVGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalGoldMin { get; set; } = 10000;

        [SettingPropertyInteger("{=CSR_LLVGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_LLVGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalGoldMax { get; set; } = 20000;

        [SettingPropertyInteger("{=CSR_LLVInfluenceMin}Influence: Lowest Band", 0, GameCaps.SettingsMaxInfluence, Order = 7, RequireRestart = false,
            HintText = "{=CSR_LLVInfluenceMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalInfluenceMin { get; set; } = 50;

        [SettingPropertyInteger("{=CSR_LLVInfluenceMax}Influence: Highest Band", 0, GameCaps.SettingsMaxInfluence, Order = 8, RequireRestart = false,
            HintText = "{=CSR_LLVInfluenceMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalInfluenceMax { get; set; } = 100;

        [SettingPropertyInteger("{=CSR_LLVTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 9, RequireRestart = false,
            HintText = "{=CSR_LLVTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalTroopsMin { get; set; } = 20;

        [SettingPropertyInteger("{=CSR_LLVTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 10, RequireRestart = false,
            HintText = "{=CSR_LLVTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_LandlessVassal}Landless Vassal", GroupOrder = 15)]
        public int LandlessVassalTroopsMax { get; set; } = 40;

        #endregion

        #region Mercenary

        [SettingPropertyBool("{=CSR_EnableMercenary}Enable Mercenary", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableMercenary_Hint}Offer the Mercenary start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public bool EnableMercenary { get; set; } = true;

        [SettingPropertyInteger("{=CSR_MercClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_MercClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryClanTierMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_MercClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_MercClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryClanTierMax { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_MercEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_MercEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryEquipmentTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_MercEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_MercEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryEquipmentTierMax { get; set; } = 4;

        [SettingPropertyInteger("{=CSR_MercGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_MercGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryGoldMin { get; set; } = 5000;

        [SettingPropertyInteger("{=CSR_MercGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_MercGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryGoldMax { get; set; } = 10000;

        [SettingPropertyInteger("{=CSR_MercInfluenceMin}Influence: Lowest Band", 0, GameCaps.SettingsMaxInfluence, Order = 7, RequireRestart = false,
            HintText = "{=CSR_MercInfluenceMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryInfluenceMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_MercInfluenceMax}Influence: Highest Band", 0, GameCaps.SettingsMaxInfluence, Order = 8, RequireRestart = false,
            HintText = "{=CSR_MercInfluenceMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryInfluenceMax { get; set; } = 50;

        [SettingPropertyInteger("{=CSR_MercTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 9, RequireRestart = false,
            HintText = "{=CSR_MercTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryTroopsMin { get; set; } = 20;

        [SettingPropertyInteger("{=CSR_MercTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 10, RequireRestart = false,
            HintText = "{=CSR_MercTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Mercenary}Mercenary", GroupOrder = 14)]
        public int MercenaryTroopsMax { get; set; } = 40;

        #endregion

        #region Commoner

        [SettingPropertyBool("{=CSR_EnableCommoner}Enable Commoner", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableCommoner_Hint}Offer the Commoner start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public bool EnableCommoner { get; set; } = true;

        [SettingPropertyInteger("{=CSR_CommClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_CommClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerClanTierMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_CommClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_CommClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerClanTierMax { get; set; } = 1;

        [SettingPropertyInteger("{=CSR_CommEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_CommEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerEquipmentTierMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_CommEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_CommEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerEquipmentTierMax { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_CommGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_CommGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerGoldMin { get; set; } = 500;

        [SettingPropertyInteger("{=CSR_CommGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_CommGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerGoldMax { get; set; } = 3000;

        [SettingPropertyInteger("{=CSR_CommTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 7, RequireRestart = false,
            HintText = "{=CSR_CommTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerTroopsMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_CommTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 8, RequireRestart = false,
            HintText = "{=CSR_CommTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Commoner}Commoner", GroupOrder = 18)]
        public int CommonerTroopsMax { get; set; } = 15;

        #endregion

        #region Outlaw

        [SettingPropertyBool("{=CSR_EnableOutlaw}Enable Outlaw", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableOutlaw_Hint}Offer the Outlaw start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public bool EnableOutlaw { get; set; } = true;

        [SettingPropertyInteger("{=CSR_OutlawClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_OutlawClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawClanTierMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_OutlawClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_OutlawClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawClanTierMax { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_OutlawEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_OutlawEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawEquipmentTierMin { get; set; } = 1;

        [SettingPropertyInteger("{=CSR_OutlawEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_OutlawEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawEquipmentTierMax { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_OutlawGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_OutlawGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawGoldMin { get; set; } = 1000;

        [SettingPropertyInteger("{=CSR_OutlawGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_OutlawGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawGoldMax { get; set; } = 5000;

        [SettingPropertyInteger("{=CSR_OutlawTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 7, RequireRestart = false,
            HintText = "{=CSR_OutlawTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawTroopsMin { get; set; } = 15;

        [SettingPropertyInteger("{=CSR_OutlawTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 8, RequireRestart = false,
            HintText = "{=CSR_OutlawTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawTroopsMax { get; set; } = 35;

        [SettingPropertyInteger("{=CSR_OutlawCrime}Starting Crime Rating", 0, 100, Order = 9, RequireRestart = false,
            HintText = "{=CSR_OutlawCrime_Hint}Crime rating with the realm that outlawed you; on the story path the Crime chapter sets this instead.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Outlaw}Outlaw", GroupOrder = 13)]
        public int OutlawCrimeRating { get; set; } = 50;

        #endregion

        #region Caravan Master

        [SettingPropertyBool("{=CSR_EnableCaravan}Enable Caravan Master", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableCaravan_Hint}Offer the Caravan Master start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public bool EnableCaravanMaster { get; set; } = true;

        [SettingPropertyInteger("{=CSR_CaravanClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_CaravanClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanClanTierMin { get; set; } = 0;

        [SettingPropertyInteger("{=CSR_CaravanClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_CaravanClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanClanTierMax { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_CaravanEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_CaravanEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanEquipmentTierMin { get; set; } = 1;

        [SettingPropertyInteger("{=CSR_CaravanEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_CaravanEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanEquipmentTierMax { get; set; } = 3;

        [SettingPropertyInteger("{=CSR_CaravanGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_CaravanGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanGoldMin { get; set; } = 8000;

        [SettingPropertyInteger("{=CSR_CaravanGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_CaravanGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanGoldMax { get; set; } = 15000;

        [SettingPropertyInteger("{=CSR_CaravanTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 7, RequireRestart = false,
            HintText = "{=CSR_CaravanTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanTroopsMin { get; set; } = 15;

        [SettingPropertyInteger("{=CSR_CaravanTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 8, RequireRestart = false,
            HintText = "{=CSR_CaravanTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanTroopsMax { get; set; } = 30;

        [SettingPropertyInteger("{=CSR_CaravanMules}Pack Animals", 0, 20, Order = 9, RequireRestart = false,
            HintText = "{=CSR_CaravanMules_Hint}Pack animals in the starting caravan; on the story path the Beasts chapter sets this instead.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Caravan}Caravan Master", GroupOrder = 12)]
        public int CaravanPackAnimals { get; set; } = 6;

        #endregion

        #region Rebel Clan

        [SettingPropertyBool("{=CSR_EnableRebel}Enable Rebel Clan", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "{=CSR_EnableRebel_Hint}Offer the Rebel Clan start in character creation; turning every start off offers all of them instead, since a start has to exist.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public bool EnableRebelClan { get; set; } = true;

        [SettingPropertyInteger("{=CSR_RebelClanTierMin}Clan Tier: Lowest", 0, 6, Order = 1, RequireRestart = false,
            HintText = "{=CSR_RebelClanTierMin_Hint}The lowest clan tier this start may begin at; the story path raises a lower choice to meet it, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelClanTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_RebelClanTierMax}Clan Tier: Highest", 0, 6, Order = 2, RequireRestart = false,
            HintText = "{=CSR_RebelClanTierMax_Hint}The highest clan tier this start may reach; the story path scales your standing between the two, and the Start Editor does not.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelClanTierMax { get; set; } = 5;

        [SettingPropertyInteger("{=CSR_RebelEquipTierMin}Equipment Tier: Lowest", 0, 6, Order = 3, RequireRestart = false,
            HintText = "{=CSR_RebelEquipTierMin_Hint}The lowest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelEquipmentTierMin { get; set; } = 2;

        [SettingPropertyInteger("{=CSR_RebelEquipTierMax}Equipment Tier: Highest", 0, 6, Order = 4, RequireRestart = false,
            HintText = "{=CSR_RebelEquipTierMax_Hint}The highest gear tier this start is outfitted at; your clan tier is clamped into this band, and the Start Editor picks gear itself.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelEquipmentTierMax { get; set; } = 4;

        [SettingPropertyInteger("{=CSR_RebelGoldMin}Gold: Lowest Band", 0, GameCaps.SettingsMaxGold, Order = 5, RequireRestart = false,
            HintText = "{=CSR_RebelGoldMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelGoldMin { get; set; } = 15000;

        [SettingPropertyInteger("{=CSR_RebelGoldMax}Gold: Highest Band", 0, GameCaps.SettingsMaxGold, Order = 6, RequireRestart = false,
            HintText = "{=CSR_RebelGoldMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelGoldMax { get; set; } = 30000;

        [SettingPropertyInteger("{=CSR_RebelTroopsMin}Troops: Lowest Band", 0, GameCaps.MaxTroops, Order = 7, RequireRestart = false,
            HintText = "{=CSR_RebelTroopsMin_Hint}What the Minimum band grants; the other bands space evenly up to the Maximum, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelTroopsMin { get; set; } = 40;

        [SettingPropertyInteger("{=CSR_RebelTroopsMax}Troops: Highest Band", 0, GameCaps.MaxTroops, Order = 8, RequireRestart = false,
            HintText = "{=CSR_RebelTroopsMax_Hint}What the Maximum band grants; it is a rung on the ladder, not a cap, and the Start Editor opens on it too.")]
        [SettingPropertyGroup("{=CSR_StartTypes}Start Types/{=CSR_Rebel}Rebel Clan", GroupOrder = 11)]
        public int RebelTroopsMax { get; set; } = 80;

        #endregion

        #region Helper Methods

        /// <summary>
        ///     MCM entries define the Cultured Start preset ladder (Minimum
        ///     through Maximum), but the live game caps are the law: every range
        ///     handed out is clamped to them and kept min-below-max.
        /// </summary>
        private static (int min, int max) Normalize((int min, int max) range, int floor, int ceiling)
        {
            int min = Math.Max(floor, Math.Min(ceiling, range.min));
            int max = Math.Max(min, Math.Min(ceiling, range.max));
            return (min, max);
        }

        public (int min, int max) GetGoldRange(StartType startType)
        {
            return Normalize(GetGoldRangeRaw(startType), 0, GameCaps.MaxGold);
        }

        private (int min, int max) GetGoldRangeRaw(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => (MonarchGoldMin, MonarchGoldMax),
                StartType.LandedVassal => (LandedVassalGoldMin, LandedVassalGoldMax),
                StartType.LandlessVassal => (LandlessVassalGoldMin, LandlessVassalGoldMax),
                StartType.Mercenary => (MercenaryGoldMin, MercenaryGoldMax),
                StartType.Commoner => (CommonerGoldMin, CommonerGoldMax),
                StartType.Outlaw => (OutlawGoldMin, OutlawGoldMax),
                StartType.CaravanMaster => (CaravanGoldMin, CaravanGoldMax),
                StartType.RebelClan => (RebelGoldMin, RebelGoldMax),
                _ => (500, 3000)
            };
        }

        public (int min, int max) GetInfluenceRange(StartType startType)
        {
            return Normalize(GetInfluenceRangeRaw(startType), 0, GameCaps.MaxInfluence);
        }

        private (int min, int max) GetInfluenceRangeRaw(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => (MonarchInfluenceMin, MonarchInfluenceMax),
                StartType.LandedVassal => (LandedVassalInfluenceMin, LandedVassalInfluenceMax),
                StartType.LandlessVassal => (LandlessVassalInfluenceMin, LandlessVassalInfluenceMax),
                StartType.Mercenary => (MercenaryInfluenceMin, MercenaryInfluenceMax),
                _ => (0, 0)
            };
        }

        public (int min, int max) GetTroopsRange(StartType startType)
        {
            return Normalize(GetTroopsRangeRaw(startType), 0, GameCaps.MaxTroops);
        }

        private (int min, int max) GetTroopsRangeRaw(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => (MonarchTroopsMin, MonarchTroopsMax),
                StartType.LandedVassal => (LandedVassalTroopsMin, LandedVassalTroopsMax),
                StartType.LandlessVassal => (LandlessVassalTroopsMin, LandlessVassalTroopsMax),
                StartType.Mercenary => (MercenaryTroopsMin, MercenaryTroopsMax),
                StartType.Commoner => (CommonerTroopsMin, CommonerTroopsMax),
                StartType.Outlaw => (OutlawTroopsMin, OutlawTroopsMax),
                StartType.CaravanMaster => (CaravanTroopsMin, CaravanTroopsMax),
                StartType.RebelClan => (RebelTroopsMin, RebelTroopsMax),
                _ => (0, 20)
            };
        }

        public (int min, int max) GetEquipmentTierRange(StartType startType)
        {
            return Normalize(GetEquipmentTierRangeRaw(startType), 0, GameCaps.MaxClanTier());
        }

        private (int min, int max) GetEquipmentTierRangeRaw(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => (MonarchEquipmentTierMin, MonarchEquipmentTierMax),
                StartType.LandedVassal => (LandedVassalEquipmentTierMin, LandedVassalEquipmentTierMax),
                StartType.LandlessVassal => (LandlessVassalEquipmentTierMin, LandlessVassalEquipmentTierMax),
                StartType.Mercenary => (MercenaryEquipmentTierMin, MercenaryEquipmentTierMax),
                StartType.Commoner => (CommonerEquipmentTierMin, CommonerEquipmentTierMax),
                StartType.Outlaw => (OutlawEquipmentTierMin, OutlawEquipmentTierMax),
                StartType.CaravanMaster => (CaravanEquipmentTierMin, CaravanEquipmentTierMax),
                StartType.RebelClan => (RebelEquipmentTierMin, RebelEquipmentTierMax),
                _ => (0, 2)
            };
        }

        public int GetMaxClanTier(StartType startType)
        {
            int ceiling = startType switch
            {
                StartType.Monarch => MonarchClanTierMax,
                StartType.LandedVassal => LandedVassalClanTierMax,
                StartType.LandlessVassal => LandlessVassalClanTierMax,
                StartType.Mercenary => MercenaryClanTierMax,
                StartType.Commoner => CommonerClanTierMax,
                StartType.Outlaw => OutlawClanTierMax,
                StartType.CaravanMaster => CaravanClanTierMax,
                StartType.RebelClan => RebelClanTierMax,
                _ => GameCaps.MaxClanTier()
            };
            return Math.Max(GetMinClanTier(startType), Math.Min(GameCaps.MaxClanTier(), ceiling));
        }

        public int GetMinClanTier(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => MonarchClanTierMin,
                StartType.LandedVassal => LandedVassalClanTierMin,
                StartType.LandlessVassal => LandlessVassalClanTierMin,
                StartType.Mercenary => MercenaryClanTierMin,
                StartType.Commoner => CommonerClanTierMin,
                StartType.Outlaw => OutlawClanTierMin,
                StartType.CaravanMaster => CaravanClanTierMin,
                StartType.RebelClan => RebelClanTierMin,
                _ => 0
            };
        }

        #region Creation Menus

        // One switch per menu the guided flow can skip. Every one defaults ON, so a player who never
        // opens this section walks exactly the flow the mod has always had. The menus that carry the
        // flow itself have no switch: the route choice, the scenario pick, and the realm and holding
        // pickers the start types needing them cannot do without.
        //
        // A skipped menu is not a hole. Each one's session fields keep the value the pipeline already
        // treats as "not asked", which is what the hint on each switch describes.

        [SettingPropertyBool("{=CSR_ShowMenu_Family}Family Background", Order = 0, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Family_Hint}Put the scenes about the house you came from. Off: they are not put, and nothing they would have left behind reaches your character.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowFamily { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Childhood}Childhood", Order = 1, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Childhood_Hint}Put the scene about the year the fire took what your house had. Off: it is not put and leaves nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowChildhood { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Education}Education", Order = 2, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Education_Hint}Put the scene about whoever taught you what you know. Off: it is not put and leaves nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowEducation { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Youth}Youth", Order = 4, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Youth_Hint}Put the scenes about what the place you grew up in made of you. Off: they are not put and leave nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowYouth { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Turning}Turning Point", Order = 5, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Turning_Hint}Put the scenes about the day it went wrong and what was decided afterward. Off: they are not put and leave nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowTurning { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Reason}Reason for Leaving", Order = 6, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Reason_Hint}Put the scenes about what the people you lived among decided you were for. Off: they are not put and leave nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowReason { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Age}The Years Since", Order = 7, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Age_Hint}Put the scenes about the years just behind you and the name you carry now. Off: they are not put and leave nothing behind.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowAge { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_StoryProgress}Story Progress", Order = 8, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_StoryProgress_Hint}Ask this chapter during a guided start. Off: how far the main story has run is not asked and it begins at the start.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_LifeStory}Life Story", GroupOrder = 20)]
        public bool ShowStoryProgress { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Means}Standing and Means", Order = 0, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Means_Hint}Ask this chapter during a guided start. Off: your standing is not asked and you begin on the standard purse for your start.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowMeans { get; set; } = true;

        // The household is three chapters and takes three switches, because the
        // three questions are independent of each other: a player who wants to be
        // asked who raised them and not who is at their own table can say so. Each
        // one left on derives every question the others would have asked, so any
        // subset is a coherent house.

        [SettingPropertyBool("{=CSR_ShowMenu_HouseholdParents}Parents", Order = 1, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_HouseholdParents_Hint}Ask this chapter during a guided start. Off: your life story decides which of your parents are living. With all three household chapters off, both are dead as in vanilla.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowHouseholdParents { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_HouseholdKin}Brothers and Sisters", Order = 2, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_HouseholdKin_Hint}Ask this chapter during a guided start. Off: your life story decides who was raised beside you. With all three household chapters off, you begin with none.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowHouseholdKin { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_HouseholdHearth}Spouse and Children", Order = 3, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_HouseholdHearth_Hint}Ask this chapter during a guided start. Off: your spouse and children are not asked and you begin with neither.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowHouseholdHearth { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Companions}Companions", Order = 4, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Companions_Hint}Ask this chapter during a guided start. Off: companions are not asked and you begin with none.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowCompanions { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Warband}Warband", Order = 5, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Warband_Hint}Ask this chapter during a guided start. Off: your warband is not asked and you begin on the standard troop band.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_Standing}Standing", GroupOrder = 21)]
        public bool ShowWarband { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Contract}Mercenary Contract", Order = 1, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Contract_Hint}Ask this chapter during a guided start. Off: your pay is not asked and the realm's own offer stands.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowContract { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Crime}The Crime", Order = 2, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Crime_Hint}Ask this chapter during a guided start. Off: your crime is not asked and the default rating stands.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowCrime { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Fleet}The Water", Order = 3, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Fleet_Hint}Ask this chapter during a guided start. Off: the water is not asked, you own no ship and your story begins on land.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowFleet { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Fief}Your Holding", Order = 4, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Fief_Hint}Ask this chapter during a guided start. Off: your holding's garrison and workshops are not asked and are left as found.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowFief { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Rising}The Rising", Order = 5, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Rising_Hint}Ask this chapter during a guided start. Off: your fellow rebel houses are not asked and you rise alone.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowRising { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Founding}Founding Story", Order = 6, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Founding_Hint}Ask this chapter during a guided start. Off: how your kingdom was founded is not asked and you found it as a settler.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowFounding { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_KingdomName}Kingdom Name", Order = 7, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_KingdomName_Hint}Ask this chapter during a guided start. Off: your kingdom is not named during creation and the game asks once the map opens.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowKingdomName { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Traditions}Traditions of the Realm", Order = 8, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Traditions_Hint}Ask this chapter during a guided start. Off: your realm's traditions are not asked and it begins with no policies.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowTraditions { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_SwornHouses}Sworn Houses", Order = 9, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_SwornHouses_Hint}Ask this chapter during a guided start. Off: sworn houses are not asked and your kingdom begins with none.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowSwornHouses { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_FirstWar}First War", Order = 10, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_FirstWar_Hint}Ask this chapter during a guided start. Off: your first war is not asked and the founding default stands.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowFirstWar { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Workshops}Workshops", Order = 11, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Workshops_Hint}Ask this chapter during a guided start. Off: no workshop is asked for and none is granted.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowTrade { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Ledger}Caravan Ledger", Order = 12, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Ledger_Hint}Ask this chapter during a guided start. Off: your caravan's load is not asked and none is added.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowLedger { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Beasts}Pack Animals", Order = 13, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Beasts_Hint}Ask this chapter during a guided start. Off: pack animals are not asked and the default count stands.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_ScenarioChapters}Scenario Chapters", GroupOrder = 22)]
        public bool ShowBeasts { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Location}Starting Location", Order = 0, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Location_Hint}Ask this chapter during a guided start. Off: where you begin is not asked and fate decides.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_GearandFinish}Gear and Finish", GroupOrder = 23)]
        public bool ShowLocation { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Gear}Gear", Order = 1, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Gear_Hint}Ask this chapter during a guided start. Off: your outfits are not asked and the quartermaster dresses you.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_GearandFinish}Gear and Finish", GroupOrder = 23)]
        public bool ShowGear { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Arms}Arms", Order = 2, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Arms_Hint}Ask this chapter during a guided start. Off: your weapons are not asked and the quartermaster arms you.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_GearandFinish}Gear and Finish", GroupOrder = 23)]
        public bool ShowArms { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Provisions}Provisions", Order = 3, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Provisions_Hint}Ask this chapter during a guided start. Off: your stores are not asked and a sensible larder is packed.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_GearandFinish}Gear and Finish", GroupOrder = 23)]
        public bool ShowProvisions { get; set; } = true;

        [SettingPropertyBool("{=CSR_ShowMenu_Epilogue}Epilogue", Order = 4, RequireRestart = false,
            HintText = "{=CSR_ShowMenu_Epilogue_Hint}Ask this chapter during a guided start. Off: the story is not read back before you ride out.")]
        [SettingPropertyGroup("{=CSR_CreationMenus}Creation Menus/{=CSR_CreationMenus_GearandFinish}Gear and Finish", GroupOrder = 23)]
        public bool ShowEpilogue { get; set; } = true;
        #endregion

        /// <summary>
        ///     Whether a creation menu is shown. Unknown ids answer true, so a menu added later is
        ///     visible until someone gives it a switch rather than vanishing silently.
        /// </summary>
        public static bool ShowsMenu(string menuId)
        {
            var s = GlobalSettings<CSSettings>.Instance;
            if (s == null) return true;

            return menuId switch
            {
                // One switch per stage of a life, and both guided routes answer to
                // it. Cultured Start has one chapter per stage and Cultured Start
                // Revamped has the scenes that stand where those chapters did, so a
                // player who switched a stage off gets what they switched off on
                // whichever route they are walking
                "cs_family_menu" => s.ShowFamily,
                "cs_childhood_menu" => s.ShowChildhood,
                "cs_education_menu" => s.ShowEducation,
                "cs_youth_menu" => s.ShowYouth,
                "cs_turning_menu" => s.ShowTurning,
                "cs_reason_menu" => s.ShowReason,
                "cs_age_menu" => s.ShowAge,

                "cs_scene_what_the_house_had" or "cs_scene_the_one_always_in_the_wrong" => s.ShowFamily,
                "cs_scene_the_store_burned" => s.ShowChildhood,
                "cs_scene_the_man_who_taught_you" => s.ShowEducation,
                "cs_scene_what_they_called_you_then" or "cs_scene_what_you_could_do" => s.ShowYouth,
                "cs_scene_what_the_yard_said" or "cs_scene_the_verdict" => s.ShowTurning,
                "cs_scene_what_the_town_said" or "cs_scene_the_seat" => s.ShowReason,
                "cs_scene_the_purse" or "cs_scene_the_winter_between"
                    or "cs_scene_the_name_they_use" => s.ShowAge,

                "cs_story_progress_menu" => s.ShowStoryProgress,
                "cs_means_menu" => s.ShowMeans,
                "cs_household_parents_menu" => s.ShowHouseholdParents,
                "cs_household_menu" => s.ShowHouseholdKin,
                "cs_household_hearth_menu" => s.ShowHouseholdHearth,
                "cs_companion_select" => s.ShowCompanions,
                "cs_warband_menu" => s.ShowWarband,
                "cs_contract_menu" => s.ShowContract,
                "cs_crime_menu" => s.ShowCrime,
                "cs_fleet_menu" => s.ShowFleet,
                "cs_fief_menu" => s.ShowFief,
                "cs_rising_menu" => s.ShowRising,
                "cs_founding_menu" => s.ShowFounding,
                "cs_kingdom_name_menu" => s.ShowKingdomName,
                "cs_traditions_menu" => s.ShowTraditions,
                "cs_sworn_houses_menu" => s.ShowSwornHouses,
                "cs_first_war_menu" => s.ShowFirstWar,
                "cs_trade_menu" => s.ShowTrade,
                "cs_ledger_menu" => s.ShowLedger,
                "cs_beasts_menu" => s.ShowBeasts,
                "cs_location_menu" => s.ShowLocation,
                "cs_gear_menu" => s.ShowGear,
                "cs_arms_menu" => s.ShowArms,
                "cs_provisions_menu" => s.ShowProvisions,
                "cs_epilogue_menu" => s.ShowEpilogue,
                _ => true
            };
        }

        /// <summary>The master switch; a missing settings instance counts as enabled.</summary>
        public static bool IsModEnabled => GlobalSettings<CSSettings>.Instance?.EnableMod ?? true;

        /// <summary>Start type availability; a missing settings instance enables all.</summary>
        public static bool IsStartTypeAvailable(StartType startType)
        {
            var settings = GlobalSettings<CSSettings>.Instance;
            return settings == null || settings.IsStartTypeEnabled(startType);
        }

        /// <summary>
        ///     True while at least one start type can be chosen. Turning all of
        ///     them off is not a configuration, it is a dead end, so the callers
        ///     treat it as no filter at all.
        /// </summary>
        public static bool AnyStartTypeAvailable()
        {
            foreach (StartType startType in Enum.GetValues(typeof(StartType)))
                if (IsStartTypeAvailable(startType))
                    return true;
            return false;
        }

        /// <summary>
        ///     Whether a start type is offered at all. This is the one answer every offer asks,
        ///     because asking <see cref="IsStartTypeAvailable"/> on its own is what stranded the
        ///     player: the scenario menu built an option only for an enabled start and the editor
        ///     listed only enabled starts, so turning all eight off left an empty screen with no
        ///     way forward, which is precisely what the all-off yield exists to prevent.
        /// </summary>
        public static bool ShowsStartType(StartType startType) =>
            IsStartTypeAvailable(startType) || !AnyStartTypeAvailable();

        private bool IsStartTypeEnabled(StartType startType)
        {
            return startType switch
            {
                StartType.Monarch => EnableMonarch,
                StartType.LandedVassal => EnableLandedVassal,
                StartType.LandlessVassal => EnableLandlessVassal,
                StartType.Mercenary => EnableMercenary,
                StartType.Commoner => EnableCommoner,
                StartType.Outlaw => EnableOutlaw,
                StartType.CaravanMaster => EnableCaravanMaster,
                StartType.RebelClan => EnableRebelClan,
                _ => false
            };
        }

        public static bool UsesInfluence(StartType startType)
        {
            // Mercenaries included: vanilla converts mercenary influence to denars,
            // so a starting balance is meaningful pay
            return startType == StartType.Monarch ||
                   startType == StartType.LandedVassal ||
                   startType == StartType.LandlessVassal ||
                   startType == StartType.Mercenary;
        }

        public static int GetRangeValue((int min, int max) range, RangePreset preset)
        {
            int span = range.max - range.min;
            return preset switch
            {
                RangePreset.Minimum => range.min,
                RangePreset.Low => range.min + (int)(span * 0.25),
                RangePreset.Standard => range.min + (int)(span * 0.50),
                RangePreset.High => range.min + (int)(span * 0.75),
                RangePreset.Maximum => range.max,
                _ => range.min + (int)(span * 0.50)
            };
        }

        /// <summary>
        ///     The renown a tier requires, asked of the installed clan tier model
        ///     so renown mods answer for themselves; the vanilla table is only the
        ///     fallback when no campaign is live.
        /// </summary>
        public static int GetRenownForTier(int tier)
        {
            try
            {
                var model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.ClanTierModel;
                if (model != null && tier >= model.MinClanTier && tier <= model.MaxClanTier)
                    return model.GetRequiredRenownForTier(tier);
            }
            catch
            {
                // fall through to the vanilla table
            }

            return tier switch
            {
                1 => 50,
                2 => 150,
                3 => 350,
                4 => 900,
                5 => 2350,
                6 => 6150,
                _ => 0
            };
        }

        #endregion
    }
}
