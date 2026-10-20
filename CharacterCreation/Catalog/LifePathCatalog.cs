using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Catalog
{
    /// <summary>
    ///     The single source of truth for every life-path choice: menu text, skills,
    ///     attributes, focus weights, naval gating, and session wiring. Menus are
    ///     built from this data and the apply pipeline reads totals back from it.
    /// </summary>
    public static class LifePathCatalog
    {
        public const string FamilyMenuId = "cs_family_menu";
        public const string ChildhoodMenuId = "cs_childhood_menu";
        public const string EducationMenuId = "cs_education_menu";
        public const string YouthMenuId = "cs_youth_menu";
        public const string TurningMenuId = "cs_turning_menu";
        public const string ReasonMenuId = "cs_reason_menu";
        public const string AgeMenuId = "cs_age_menu";

        /// <summary>
        ///     Builds the life-path menu definitions. Called at character
        ///     creation init, when game objects (skills, naval DLC) are loaded.
        /// </summary>
        public static IReadOnlyList<LifePathMenuDef> BuildMenus()
        {
            return new[]
            {
                FamilyMenu(),
                ChildhoodMenu(),
                EducationMenu(),
                YouthMenu(),
                TurningMenu(),
                ReasonMenu(),
                AgeMenu()
            };
        }

        /// <summary>
        ///     Focus points per skill for the session's current selections. This is
        ///     what the apply pipeline uses to compute initial skill levels; focus
        ///     and attribute points themselves are applied by vanilla finalization
        ///     from the same choice data via the menu option args.
        /// </summary>
        public static Dictionary<SkillObject, int> GetFocusTotals(CharacterCreationSession session)
        {
            var totals = new Dictionary<SkillObject, int>();
            foreach (var menu in BuildMenus())
            foreach (var choice in menu.Choices)
            {
                if (!choice.IsSelected(session)) continue;
                foreach (var skill in choice.Skills())
                {
                    if (skill == null) continue;
                    totals.TryGetValue(skill, out var current);
                    totals[skill] = current + choice.FocusWeight;
                }
            }

            return totals;
        }

        /// <summary>
        ///     The trait vocabulary the catalog writes in. Shared so the creation
        ///     menus and the apply pipeline resolve the same ids the same way.
        /// </summary>
        public static TraitObject? ResolveTrait(string traitId)
        {
            return traitId switch
            {
                "Mercy" => DefaultTraits.Mercy,
                "Valor" => DefaultTraits.Valor,
                "Honor" => DefaultTraits.Honor,
                "Generosity" => DefaultTraits.Generosity,
                "Calculating" => DefaultTraits.Calculating,
                _ => null
            };
        }

        /// <summary>
        ///     One sentence naming the keepsake a background puts in the player's
        ///     inventory, so a free item is never a silent surprise.
        /// </summary>
        public static string? HeirloomNote(HeirloomKind heirloom)
        {
            return heirloom switch
            {
                HeirloomKind.CultureSword =>
                    new TextObject("{=CSR_Heirloom_Sword}An old family blade comes with you.").ToString(),
                HeirloomKind.CultureBow =>
                    new TextObject("{=CSR_Heirloom_Bow}An old family bow comes with you.").ToString(),
                HeirloomKind.CraftedMace =>
                    new TextObject("{=CSR_Heirloom_Mace}A mace from the family workshop comes with you.").ToString(),
                HeirloomKind.ThrowingKnives =>
                    new TextObject("{=CSR_Heirloom_Knives}A set of throwing knives comes with you.").ToString(),
                HeirloomKind.Jewelry =>
                    new TextObject("{=CSR_Heirloom_Jewelry}A piece of family jewelry comes with you.").ToString(),
                HeirloomKind.Mule =>
                    new TextObject("{=CSR_Heirloom_Mule}The family mule comes with you.").ToString(),
                HeirloomKind.BoardingAxe =>
                    new TextObject("{=CSR_Heirloom_Axe}A boarding axe comes with you.").ToString(),
                _ => null
            };
        }

        /// <summary>One sentence naming who already thinks well of you.</summary>
        public static string? RelationNote(RelationEffect relation)
        {
            return relation switch
            {
                RelationEffect.CultureLords =>
                    new TextObject("{=CSR_Relation_Lords}Lords of your culture know your family.").ToString(),
                RelationEffect.TownMerchants =>
                    new TextObject("{=CSR_Relation_Merchants}Town merchants know your family.").ToString(),
                RelationEffect.TownGangLeaders =>
                    new TextObject("{=CSR_Relation_Gangs}Certain gang leaders remember you.").ToString(),
                RelationEffect.VillageHeadmen =>
                    new TextObject("{=CSR_Relation_Headmen}Village headmen know your family.").ToString(),
                _ => null
            };
        }

        private static LifePathChoice Choice<T>(string id, string title, string desc, T value,
            Action<CharacterCreationSession, T> set, Func<CharacterCreationSession, T?> get,
            Func<SkillObject[]> skills, Func<CharacterAttribute?> attribute,
            int weight = 1, string? naval = null) where T : struct, Enum
        {
            return new LifePathChoice(id, title, desc)
            {
                Skills = skills,
                Attribute = attribute,
                FocusWeight = weight,
                RequiredNavalSkill = naval,
                Select = s => set(s, value),
                IsSelected = s => get(s) is T chosen && EqualityComparer<T>.Default.Equals(chosen, value)
            };
        }

        private static void Effects(List<LifePathChoice> choices, string optionId, Action<LifePathChoice> configure)
        {
            var choice = choices.Find(c => c.OptionId == optionId);
            if (choice != null) configure(choice);
        }
        /// <summary>Naval skill plus a fallback secondary; degrades to the secondary alone.</summary>
        private static Func<SkillObject[]> Naval(string navalId, Func<SkillObject> secondary)
        {
            return () =>
            {
                var navalSkill = NavalDLCService.GetNavalSkill(navalId);
                return navalSkill != null
                    ? new[] { navalSkill, secondary() }
                    : new[] { secondary() };
            };
        }

        #region Family

        private static LifePathMenuDef FamilyMenu()
        {
            LifePathChoice F(string id, string title, string desc, FamilyBackground v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr, string? naval = null) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedFamily = x, s => s.SelectedFamily,
                    skills, attr, 1, naval);

            var choices = new List<LifePathChoice>
            {
                F("cs_family_retainers",
                    "{=CSR_Family_Retainers}Landed retainers",
                    "{=CSR_Family_Retainers_Desc}Your family served a local lord as trusted lieutenants, riding with cavalry and fighting as armored lancers.",
                    FamilyBackground.Retainers,
                    () => new[] { DefaultSkills.Riding, DefaultSkills.Polearm },
                    () => DefaultCharacterAttributes.Vigor),
                F("cs_family_merchants",
                    "{=CSR_Family_Merchants}Urban merchants",
                    "{=CSR_Family_Merchants_Desc}Your family were merchants in a great city, organizing caravans and trading goods across the land.",
                    FamilyBackground.Merchants,
                    () => new[] { DefaultSkills.Trade, DefaultSkills.Charm },
                    () => DefaultCharacterAttributes.Social),
                F("cs_family_farmers",
                    "{=CSR_Family_Farmers}Rural freeholders",
                    "{=CSR_Family_Farmers_Desc}Your family were small farmers with just enough land to feed themselves. They formed the backbone of the levy.",
                    FamilyBackground.Farmers,
                    () => new[] { DefaultSkills.Athletics, DefaultSkills.Polearm },
                    () => DefaultCharacterAttributes.Endurance),
                F("cs_family_artisans",
                    "{=CSR_Family_Artisans}Urban artisans",
                    "{=CSR_Family_Artisans_Desc}Your family owned a workshop in a city, crafting goods and serving in the town militia.",
                    FamilyBackground.Artisans,
                    () => new[] { DefaultSkills.Crafting, DefaultSkills.Crossbow },
                    () => DefaultCharacterAttributes.Intelligence),
                F("cs_family_hunters",
                    "{=CSR_Family_Hunters}Foresters and hunters",
                    "{=CSR_Family_Hunters_Desc}Your family lived off the land, hunting game and trapping animals in the wilderness.",
                    FamilyBackground.Hunters,
                    () => new[] { DefaultSkills.Scouting, DefaultSkills.Bow },
                    () => DefaultCharacterAttributes.Control),
                F("cs_family_vagabonds",
                    "{=CSR_Family_Vagabonds}Urban vagabonds",
                    "{=CSR_Family_Vagabonds_Desc}Your family lived in the slums, doing odd jobs and occasionally working for criminal gangs.",
                    FamilyBackground.Vagabonds,
                    () => new[] { DefaultSkills.Roguery, DefaultSkills.OneHanded },
                    () => DefaultCharacterAttributes.Cunning),
                F("cs_family_seafarers",
                    "{=CSR_Family_Seafarers}Coastal seafarers",
                    "{=CSR_Family_Seafarers_Desc}Your family has been fishing these waters for generations, hauling in catches and dreaming of adventure on the high seas.",
                    FamilyBackground.Seafarers,
                    Naval(NavalDLCService.SkillBoatswain, () => DefaultSkills.Scouting),
                    () => DefaultCharacterAttributes.Endurance,
                    NavalDLCService.SkillBoatswain),
                F("cs_family_dockworkers",
                    "{=CSR_Family_Dockworkers}Dockworkers",
                    "{=CSR_Family_Dockworkers_Desc}Your family toiled on the docks, loading and unloading ships, learning the rhythm of the tides and the languages of foreign sailors.",
                    FamilyBackground.Dockworkers,
                    Naval(NavalDLCService.SkillShipmaster, () => DefaultSkills.Athletics),
                    () => DefaultCharacterAttributes.Vigor,
                    NavalDLCService.SkillShipmaster),
                F("cs_family_shipwrights",
                    "{=CSR_Family_Shipwrights}Shipwrights",
                    "{=CSR_Family_Shipwrights_Desc}Your family built vessels for lords and merchants, and you grew up amidst the sounds of hammering and the smell of tar.",
                    FamilyBackground.Shipwrights,
                    Naval(NavalDLCService.SkillShipmaster, () => DefaultSkills.Crafting),
                    () => DefaultCharacterAttributes.Intelligence,
                    NavalDLCService.SkillShipmaster)
            };

            Effects(choices, "cs_family_retainers", c =>
            {
                c.Traits = new[] { ("Honor", 1) };
                c.Relations = RelationEffect.CultureLords;
                c.Heirloom = HeirloomKind.CultureSword;
            });
            Effects(choices, "cs_family_merchants", c =>
            {
                c.Relations = RelationEffect.TownMerchants;
                c.Heirloom = HeirloomKind.Jewelry;
            });
            Effects(choices, "cs_family_farmers", c =>
            {
                c.Relations = RelationEffect.VillageHeadmen;
                c.Heirloom = HeirloomKind.Mule;
            });
            Effects(choices, "cs_family_artisans", c => c.Heirloom = HeirloomKind.CraftedMace);
            Effects(choices, "cs_family_hunters", c => c.Heirloom = HeirloomKind.CultureBow);
            Effects(choices, "cs_family_vagabonds", c =>
            {
                c.Relations = RelationEffect.TownGangLeaders;
                c.Heirloom = HeirloomKind.ThrowingKnives;
            });
            Effects(choices, "cs_family_seafarers", c => c.Heirloom = HeirloomKind.BoardingAxe);
            Effects(choices, "cs_family_dockworkers", c => c.Heirloom = HeirloomKind.BoardingAxe);
            Effects(choices, "cs_family_shipwrights", c => c.Heirloom = HeirloomKind.BoardingAxe);
            return new LifePathMenuDef(FamilyMenuId,
                "{=CSR_Family}Your Family",
                "{=CSR_Family_Desc}You were born into a family of...",
                choices);
        }

        #endregion

        #region Childhood

        private static LifePathMenuDef ChildhoodMenu()
        {
            LifePathChoice C(string id, string title, string desc, ChildhoodActivity v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr, string? naval = null) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedChildhood = x, s => s.SelectedChildhood,
                    skills, attr, 1, naval);

            var choices = new List<LifePathChoice>
            {
                C("cs_child_herd",
                    "{=CSR_Child_Herd}herded the sheep",
                    "{=CSR_Child_Herd_Desc}You took the village livestock to pasture, chasing strays and throwing stones at predators.",
                    ChildhoodActivity.HerdedSheep,
                    () => new[] { DefaultSkills.Athletics, DefaultSkills.Throwing },
                    () => DefaultCharacterAttributes.Control),
                C("cs_child_smith",
                    "{=CSR_Child_Smith}worked in the smithy",
                    "{=CSR_Child_Smith_Desc}You were apprenticed to the local smith, learning to heat and forge metal.",
                    ChildhoodActivity.WorkedSmith,
                    () => new[] { DefaultSkills.TwoHanded, DefaultSkills.Crafting },
                    () => DefaultCharacterAttributes.Vigor),
                C("cs_child_repair",
                    "{=CSR_Child_Repair}repaired things around the village",
                    "{=CSR_Child_Repair_Desc}You helped dig wells, fix roofs, and repair broken tools, learning the basics of construction.",
                    ChildhoodActivity.RepairedProjects,
                    () => new[] { DefaultSkills.Crafting, DefaultSkills.Engineering },
                    () => DefaultCharacterAttributes.Intelligence),
                C("cs_child_herbs",
                    "{=CSR_Child_Herbs}gathered herbs in the wild",
                    "{=CSR_Child_Herbs_Desc}The village healer sent you into the hills to find medicinal plants and learn their uses.",
                    ChildhoodActivity.GatheredHerbs,
                    () => new[] { DefaultSkills.Medicine, DefaultSkills.Scouting },
                    () => DefaultCharacterAttributes.Endurance),
                C("cs_child_hunt",
                    "{=CSR_Child_Hunt}hunted small game",
                    "{=CSR_Child_Hunt_Desc}You accompanied a hunter into the wilderness, setting traps and learning to track prey.",
                    ChildhoodActivity.HuntedSmallGame,
                    () => new[] { DefaultSkills.Bow, DefaultSkills.Tactics },
                    () => DefaultCharacterAttributes.Cunning),
                C("cs_child_sell",
                    "{=CSR_Child_Sell}sold products at the market",
                    "{=CSR_Child_Sell_Desc}You took family goods to market, learning to haggle and deal with customers.",
                    ChildhoodActivity.SoldProducts,
                    () => new[] { DefaultSkills.Trade, DefaultSkills.Charm },
                    () => DefaultCharacterAttributes.Social),
                C("cs_child_militia",
                    "{=CSR_Child_Militia}watched the militia train",
                    "{=CSR_Child_Militia_Desc}You spent hours watching the town guard practice their drills and defensive tactics.",
                    ChildhoodActivity.WatchedMilitia,
                    () => new[] { DefaultSkills.Polearm, DefaultSkills.Tactics },
                    () => DefaultCharacterAttributes.Control),
                C("cs_child_gangs",
                    "{=CSR_Child_Gangs}hung out with street gangs",
                    "{=CSR_Child_Gangs_Desc}You ran with the gangs in the alleys, learning to survive by your wits and quick blade.",
                    ChildhoodActivity.HungWithGangs,
                    () => new[] { DefaultSkills.Roguery, DefaultSkills.OneHanded },
                    () => DefaultCharacterAttributes.Cunning),
                C("cs_child_build",
                    "{=CSR_Child_Build}helped at building sites",
                    "{=CSR_Child_Build_Desc}You worked on construction projects, learning about hoists, scaffolds, and hard labor.",
                    ChildhoodActivity.HelpedBuilding,
                    () => new[] { DefaultSkills.Athletics, DefaultSkills.Crafting },
                    () => DefaultCharacterAttributes.Vigor),
                C("cs_child_tutor",
                    "{=CSR_Child_Tutor}studied with a tutor",
                    "{=CSR_Child_Tutor_Desc}Your family hired a tutor, and you studied history, mathematics, and philosophy.",
                    ChildhoodActivity.StudiedWithTutor,
                    () => new[] { DefaultSkills.Engineering, DefaultSkills.Leadership },
                    () => DefaultCharacterAttributes.Intelligence),
                C("cs_child_horses",
                    "{=CSR_Child_Horses}cared for horses",
                    "{=CSR_Child_Horses_Desc}You spent your days at the stables, grooming horses and riding whenever you could.",
                    ChildhoodActivity.CaredForHorses,
                    () => new[] { DefaultSkills.Riding, DefaultSkills.Polearm },
                    () => DefaultCharacterAttributes.Endurance),
                C("cs_child_boats",
                    "{=CSR_Child_Boats}helped on fishing boats",
                    "{=CSR_Child_Boats_Desc}You spent your childhood helping on family boats, learning to haul nets and read the weather.",
                    ChildhoodActivity.HelpedOnBoats,
                    Naval(NavalDLCService.SkillBoatswain, () => DefaultSkills.Athletics),
                    () => DefaultCharacterAttributes.Endurance,
                    NavalDLCService.SkillBoatswain),
                C("cs_child_docks",
                    "{=CSR_Child_Docks}ran errands at the docks",
                    "{=CSR_Child_Docks_Desc}You spent your days at the bustling harbor, carrying messages and learning about ships and trade.",
                    ChildhoodActivity.RanDocksErrands,
                    Naval(NavalDLCService.SkillShipmaster, () => DefaultSkills.Scouting),
                    () => DefaultCharacterAttributes.Cunning,
                    NavalDLCService.SkillShipmaster),
                C("cs_child_harbor",
                    "{=CSR_Child_Harbor}watched the harbor guards",
                    "{=CSR_Child_Harbor_Desc}You spent hours watching the coastal guards patrol and drill, dreaming of naval battles.",
                    ChildhoodActivity.WatchedHarbor,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.Tactics),
                    () => DefaultCharacterAttributes.Control,
                    NavalDLCService.SkillMariner)
            };

            return new LifePathMenuDef(ChildhoodMenuId,
                "{=CSR_Childhood}Your Childhood",
                "{=CSR_Childhood_Desc}As a child, you...",
                choices);
        }

        #endregion

        #region Education

        private static LifePathMenuDef EducationMenu()
        {
            LifePathChoice E(string id, string title, string desc, EducationType v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr, string? naval = null) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedEducation = x, s => s.SelectedEducation,
                    skills, attr, 1, naval);

            var choices = new List<LifePathChoice>
            {
                E("cs_edu_squire",
                    "{=CSR_Edu_Squire}trained as a squire",
                    "{=CSR_Edu_Squire_Desc}You served a knight, learning the arts of war with sword and shield.",
                    EducationType.SquireMilitary,
                    () => new[] { DefaultSkills.OneHanded, DefaultSkills.TwoHanded },
                    () => DefaultCharacterAttributes.Vigor),
                E("cs_edu_scholar",
                    "{=CSR_Edu_Scholar}pursued scholarly studies",
                    "{=CSR_Edu_Scholar_Desc}You studied at a monastery or academy, learning engineering, medicine, and philosophy.",
                    EducationType.ScholarStudent,
                    () => new[] { DefaultSkills.Engineering, DefaultSkills.Medicine },
                    () => DefaultCharacterAttributes.Intelligence),
                E("cs_edu_trader",
                    "{=CSR_Edu_Trader}apprenticed to a merchant",
                    "{=CSR_Edu_Trader_Desc}You learned the merchant trade, traveling with caravans and learning to negotiate.",
                    EducationType.TraderApprentice,
                    () => new[] { DefaultSkills.Trade, DefaultSkills.Charm },
                    () => DefaultCharacterAttributes.Social),
                E("cs_edu_scout",
                    "{=CSR_Edu_Scout}explored the wilderness",
                    "{=CSR_Edu_Scout_Desc}You spent years ranging through forests and mountains, learning to survive in the wild.",
                    EducationType.ScoutExplorer,
                    () => new[] { DefaultSkills.Scouting, DefaultSkills.Athletics },
                    () => DefaultCharacterAttributes.Endurance),
                E("cs_edu_street",
                    "{=CSR_Edu_Street}learned the ways of the streets",
                    "{=CSR_Edu_Street_Desc}You survived by your wits in the urban underworld, learning tricks of the criminal trade.",
                    EducationType.StreetSmart,
                    () => new[] { DefaultSkills.Roguery, DefaultSkills.Throwing },
                    () => DefaultCharacterAttributes.Cunning),
                E("cs_edu_cavalry",
                    "{=CSR_Edu_Cavalry}trained with cavalry",
                    "{=CSR_Edu_Cavalry_Desc}You learned mounted combat, riding warhorses and fighting with lance and spear.",
                    EducationType.CavalryTraining,
                    () => new[] { DefaultSkills.Riding, DefaultSkills.Polearm },
                    () => DefaultCharacterAttributes.Control),
                E("cs_edu_sailor",
                    "{=CSR_Edu_Sailor}apprenticed to a ship captain",
                    "{=CSR_Edu_Sailor_Desc}You learned seamanship aboard a vessel, mastering navigation, weather reading, and naval combat.",
                    EducationType.SailorApprentice,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.Scouting),
                    () => DefaultCharacterAttributes.Cunning,
                    NavalDLCService.SkillMariner),
                E("cs_edu_shipwright",
                    "{=CSR_Edu_Shipwright}trained as a shipwright",
                    "{=CSR_Edu_Shipwright_Desc}You learned the craft of building and repairing vessels, understanding their structure and maintenance.",
                    EducationType.ShipwrightTrainee,
                    Naval(NavalDLCService.SkillShipmaster, () => DefaultSkills.Engineering),
                    () => DefaultCharacterAttributes.Intelligence,
                    NavalDLCService.SkillShipmaster),
                E("cs_edu_docks_worker",
                    "{=CSR_Edu_DocksWorker}worked at the harbor",
                    "{=CSR_Edu_DocksWorker_Desc}You labored at the docks, loading cargo and maintaining ships, building strength and learning crew discipline.",
                    EducationType.DocksWorker,
                    Naval(NavalDLCService.SkillBoatswain, () => DefaultSkills.Athletics),
                    () => DefaultCharacterAttributes.Endurance,
                    NavalDLCService.SkillBoatswain)
            };

            return new LifePathMenuDef(EducationMenuId,
                "{=CSR_Education}Your Education",
                "{=CSR_Education_Desc}As you grew older, you...",
                choices);
        }

        #endregion

        #region Youth

        private static LifePathMenuDef YouthMenu()
        {
            LifePathChoice Y(string id, string title, string desc, YouthActivity v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr, string? naval = null) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedYouth = x, s => s.SelectedYouth,
                    skills, attr, 1, naval);

            var choices = new List<LifePathChoice>
            {
                Y("cs_youth_military",
                    "{=CSR_Youth_Military}served in the military",
                    "{=CSR_Youth_Military_Desc}You joined an army, learning discipline and the art of command.",
                    YouthActivity.MilitaryService,
                    () => new[] { DefaultSkills.OneHanded, DefaultSkills.Leadership },
                    () => DefaultCharacterAttributes.Vigor),
                Y("cs_youth_merchant",
                    "{=CSR_Youth_Merchant}traveled as a merchant",
                    "{=CSR_Youth_Merchant_Desc}You journeyed with caravans, buying and selling goods across many lands.",
                    YouthActivity.MerchantTravels,
                    () => new[] { DefaultSkills.Trade, DefaultSkills.Charm },
                    () => DefaultCharacterAttributes.Social),
                Y("cs_youth_scholar",
                    "{=CSR_Youth_Scholar}pursued scholarly knowledge",
                    "{=CSR_Youth_Scholar_Desc}You devoted yourself to learning, studying medicine, engineering, and philosophy.",
                    YouthActivity.ScholarlyPursuits,
                    () => new[] { DefaultSkills.Medicine, DefaultSkills.Engineering },
                    () => DefaultCharacterAttributes.Intelligence),
                Y("cs_youth_outlaw",
                    "{=CSR_Youth_Outlaw}lived as an outlaw",
                    "{=CSR_Youth_Outlaw_Desc}You survived outside the law, raiding and evading authorities.",
                    YouthActivity.OutlawLife,
                    () => new[] { DefaultSkills.Roguery, DefaultSkills.Tactics },
                    () => DefaultCharacterAttributes.Cunning),
                Y("cs_youth_estate",
                    "{=CSR_Youth_Estate}managed an estate",
                    "{=CSR_Youth_Estate_Desc}You oversaw lands and workers, learning administration and command.",
                    YouthActivity.EstateManagement,
                    () => new[] { DefaultSkills.Steward, DefaultSkills.Leadership },
                    () => DefaultCharacterAttributes.Social),
                Y("cs_youth_guide",
                    "{=CSR_Youth_Guide}worked as a wilderness guide",
                    "{=CSR_Youth_Guide_Desc}You led expeditions through untamed lands, mastering survival and navigation.",
                    YouthActivity.WildernessGuide,
                    () => new[] { DefaultSkills.Scouting, DefaultSkills.Riding },
                    () => DefaultCharacterAttributes.Endurance),
                Y("cs_youth_galley",
                    "{=CSR_Youth_Galley}crewed a galley in coastal raids",
                    "{=CSR_Youth_Galley_Desc}You spent your youth on raiding vessels, learning to fight in naval battles and board enemy ships.",
                    YouthActivity.CrewedGalley,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.OneHanded),
                    () => DefaultCharacterAttributes.Vigor,
                    NavalDLCService.SkillMariner),
                Y("cs_youth_coastal",
                    "{=CSR_Youth_Coastal}served as a coastal defender",
                    "{=CSR_Youth_Coastal_Desc}You defended your homeland's shores from raiders, learning naval tactics and leadership.",
                    YouthActivity.CoastalDefender,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.Leadership),
                    () => DefaultCharacterAttributes.Vigor,
                    NavalDLCService.SkillMariner),
                Y("cs_youth_corsair",
                    "{=CSR_Youth_Corsair}served as a corsair deckhand",
                    "{=CSR_Youth_Corsair_Desc}You joined a corsair crew, learning the brutal art of piracy and naval raiding.",
                    YouthActivity.Corsair,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.Roguery),
                    () => DefaultCharacterAttributes.Cunning,
                    NavalDLCService.SkillMariner),
                Y("cs_youth_river",
                    "{=CSR_Youth_River}rowed on a river trader",
                    "{=CSR_Youth_River_Desc}You worked aboard river vessels, transporting goods and learning the ways of maritime commerce.",
                    YouthActivity.RiverTrader,
                    Naval(NavalDLCService.SkillBoatswain, () => DefaultSkills.Trade),
                    () => DefaultCharacterAttributes.Social,
                    NavalDLCService.SkillBoatswain)
            };

            Effects(choices, "cs_youth_outlaw", c => c.Traits = new[] { ("Honor", -1) });
            Effects(choices, "cs_youth_estate", c => c.Traits = new[] { ("Calculating", 1) });
            Effects(choices, "cs_youth_military", c => c.Relations = RelationEffect.CultureLords);
            return new LifePathMenuDef(YouthMenuId,
                "{=CSR_Youth}Your Early Adulthood",
                "{=CSR_Youth_Desc}In your twenties, you...",
                choices);
        }

        #endregion

        #region Turning point

        private static LifePathMenuDef TurningMenu()
        {
            LifePathChoice T(string id, string title, string desc, TurningPoint v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr, string? naval = null) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedTurning = x, s => s.SelectedTurning,
                    skills, attr, 1, naval);

            var choices = new List<LifePathChoice>
            {
                T("cs_turning_duel",
                    "{=CSR_Turning_Duel}A duel answered",
                    "{=CSR_Turning_Duel_Desc}A boastful champion insulted your name before a crowd, and you answered with steel. The scar still aches in the cold; so does the memory of the cheering.",
                    TurningPoint.Duel,
                    () => new[] { DefaultSkills.OneHanded, DefaultSkills.Athletics },
                    () => DefaultCharacterAttributes.Vigor),
                T("cs_turning_debt",
                    "{=CSR_Turning_Debt}A debt repaid",
                    "{=CSR_Turning_Debt_Desc}Your family owed more than it owned, and you spent hard years working it off coin by coin. Every ledger you have kept since balances to the copper.",
                    TurningPoint.Debt,
                    () => new[] { DefaultSkills.Trade, DefaultSkills.Steward },
                    () => DefaultCharacterAttributes.Social),
                T("cs_turning_wilds",
                    "{=CSR_Turning_Wilds}Lost in the wilds",
                    "{=CSR_Turning_Wilds_Desc}A journey went wrong and a season alone in the wilderness followed. You came back lean, quiet, and impossible to lose on any road since.",
                    TurningPoint.Wilds,
                    () => new[] { DefaultSkills.Scouting, DefaultSkills.Athletics },
                    () => DefaultCharacterAttributes.Endurance),
                T("cs_turning_sickness",
                    "{=CSR_Turning_Sickness}The sickness year",
                    "{=CSR_Turning_Sickness_Desc}When the fever came through, you stayed. You nursed the dying, buried the lost, and kept the stores from spoiling while others fled.",
                    TurningPoint.Sickness,
                    () => new[] { DefaultSkills.Medicine, DefaultSkills.Steward },
                    () => DefaultCharacterAttributes.Intelligence),
                T("cs_turning_raid",
                    "{=CSR_Turning_Raid}The night of fire",
                    "{=CSR_Turning_Raid_Desc}Raiders came for your home in the dark. You stood on the wall with whatever you could draw and loose, and you were still standing at dawn.",
                    TurningPoint.Raid,
                    () => new[] { DefaultSkills.Bow, DefaultSkills.OneHanded },
                    () => DefaultCharacterAttributes.Control),
                T("cs_turning_smuggle",
                    "{=CSR_Turning_Smuggle}A smuggler's run",
                    "{=CSR_Turning_Smuggle_Desc}One desperate season, you ran contraband past the toll posts. The coin was good, the friends were better, and your name is still whispered at certain gates.",
                    TurningPoint.Smuggle,
                    () => new[] { DefaultSkills.Roguery, DefaultSkills.Riding },
                    () => DefaultCharacterAttributes.Cunning),
                T("cs_turning_court",
                    "{=CSR_Turning_Court}A season at court",
                    "{=CSR_Turning_Court_Desc}A visiting lord took you into their retinue for a season. You learned which words open doors, and which silences close them.",
                    TurningPoint.Court,
                    () => new[] { DefaultSkills.Charm, DefaultSkills.Leadership },
                    () => DefaultCharacterAttributes.Social),
                T("cs_turning_masterwork",
                    "{=CSR_Turning_Masterwork}The masterwork",
                    "{=CSR_Turning_Masterwork_Desc}A year of your life went into a single blade, folded and ground and ruined and begun again. When it was done, the smith called you by your name.",
                    TurningPoint.Masterwork,
                    () => new[] { DefaultSkills.Crafting, DefaultSkills.TwoHanded },
                    () => DefaultCharacterAttributes.Intelligence),
                T("cs_turning_storm",
                    "{=CSR_Turning_Storm}The storm",
                    "{=CSR_Turning_Storm_Desc}The sea took your ship and most of your crew, and gave you back three days later clinging to a spar. You have not feared weather, or much else, since.",
                    TurningPoint.Storm,
                    Naval(NavalDLCService.SkillMariner, () => DefaultSkills.Athletics),
                    () => DefaultCharacterAttributes.Endurance,
                    NavalDLCService.SkillMariner)
            };

            Effects(choices, "cs_turning_duel", c => c.Traits = new[] { ("Valor", 1) });
            Effects(choices, "cs_turning_debt", c => c.Traits = new[] { ("Honor", 1) });
            Effects(choices, "cs_turning_sickness", c => c.Traits = new[] { ("Mercy", 1) });
            Effects(choices, "cs_turning_smuggle", c =>
            {
                c.Traits = new[] { ("Honor", -1) };
                c.Relations = RelationEffect.TownGangLeaders;
            });
            Effects(choices, "cs_turning_raid", c => c.Traits = new[] { ("Valor", 1) });

            return new LifePathMenuDef(TurningMenuId,
                "{=CSR_Turning}The Turning Point",
                "{=CSR_Turning_Desc}Every life has a hinge: the day that decided who you would be...",
                choices);
        }

        #endregion

        #region Reason

        private static LifePathMenuDef ReasonMenu()
        {
            LifePathChoice R(string id, string title, string desc, AdventuringReason v,
                Func<SkillObject[]> skills, Func<CharacterAttribute?> attr) =>
                Choice(id, title, desc, v, (s, x) => s.SelectedReason = x, s => s.SelectedReason,
                    skills, attr, 2);

            var choices = new List<LifePathChoice>
            {
                R("cs_reason_travel",
                    "{=CSR_Reason_Travel}to discover the world.",
                    "{=CSR_Reason_Travel_Desc}The pull of distant horizons never left you.",
                    AdventuringReason.Travel,
                    () => new[] { DefaultSkills.Scouting },
                    () => DefaultCharacterAttributes.Endurance),
                R("cs_reason_revenge",
                    "{=CSR_Reason_Revenge}to take revenge.",
                    "{=CSR_Reason_Revenge_Desc}A wrong burned hot enough to drive you from home.",
                    AdventuringReason.Revenge,
                    () => new[] { DefaultSkills.Tactics },
                    () => DefaultCharacterAttributes.Vigor),
                R("cs_reason_forced_out",
                    "{=CSR_Reason_ForcedOut}after being forced out.",
                    "{=CSR_Reason_ForcedOut_Desc}With nowhere else to go, you chose the road.",
                    AdventuringReason.ForcedOut,
                    () => new[] { DefaultSkills.Roguery },
                    () => DefaultCharacterAttributes.Cunning),
                R("cs_reason_money",
                    "{=CSR_Reason_Money}in search of money.",
                    "{=CSR_Reason_Money_Desc}Prosperity promised more than staying behind ever could.",
                    AdventuringReason.Money,
                    () => new[] { DefaultSkills.Trade },
                    () => DefaultCharacterAttributes.Cunning),
                R("cs_reason_power",
                    "{=CSR_Reason_Power}to become one of the powerful.",
                    "{=CSR_Reason_Power_Desc}You chased influence, whatever the cost.",
                    AdventuringReason.Power,
                    () => new[] { DefaultSkills.Leadership },
                    () => DefaultCharacterAttributes.Cunning),
                R("cs_reason_history",
                    "{=CSR_Reason_History}to mark history.",
                    "{=CSR_Reason_History_Desc}You longed to be remembered in the chronicles.",
                    AdventuringReason.History,
                    () => new[] { DefaultSkills.Charm },
                    () => DefaultCharacterAttributes.Social),
                R("cs_reason_loss",
                    "{=CSR_Reason_Loss}after the loss of a loved one.",
                    "{=CSR_Reason_Loss_Desc}You left to find purpose beyond your grief.",
                    AdventuringReason.Loss,
                    () => new[] { DefaultSkills.Steward },
                    () => DefaultCharacterAttributes.Social),
                R("cs_reason_crafting",
                    "{=CSR_Reason_Crafting}to practice your trade.",
                    "{=CSR_Reason_Crafting_Desc}You set out to hone your craft in the wider world.",
                    AdventuringReason.Crafting,
                    () => new[] { DefaultSkills.Crafting },
                    () => DefaultCharacterAttributes.Endurance),
                R("cs_reason_helping",
                    "{=CSR_Reason_Helping}to help those in need.",
                    "{=CSR_Reason_Helping_Desc}War left wounds you could not ignore.",
                    AdventuringReason.Helping,
                    () => new[] { DefaultSkills.Medicine },
                    () => DefaultCharacterAttributes.Intelligence),
                R("cs_reason_prove_worth",
                    "{=CSR_Reason_ProveWorth}to prove your fighting skills.",
                    "{=CSR_Reason_ProveWorth_Desc}The wider world seemed the truest test of your mettle.",
                    AdventuringReason.ProveWorth,
                    () => new[] { DefaultSkills.OneHanded, DefaultSkills.TwoHanded },
                    () => DefaultCharacterAttributes.Vigor)
            };

            Effects(choices, "cs_reason_helping", c => c.Traits = new[] { ("Mercy", 1) });
            Effects(choices, "cs_reason_revenge", c => c.Traits = new[] { ("Valor", 1) });
            Effects(choices, "cs_reason_history", c => c.Traits = new[] { ("Calculating", 1) });
            return new LifePathMenuDef(ReasonMenuId,
                "{=CSR_Reason_Title}Reason for Adventuring",
                "{=CSR_Reason_Desc}You started adventuring...",
                choices);
        }

        #endregion

        #region Age

        private static LifePathMenuDef AgeMenu()
        {
            LifePathChoice A(string id, string title, string desc, StartingAge v,
                int unspentFocus, int unspentAttribute)
            {
                var choice = Choice(id, title, desc, v,
                    (s, x) => s.SelectedAge = x, s => s.SelectedAge,
                    () => Array.Empty<SkillObject>(), () => null);
                choice.UnspentFocus = unspentFocus;
                choice.UnspentAttribute = unspentAttribute;
                return choice;
            }

            var choices = new List<LifePathChoice>
            {
                A("cs_age_young",
                    "{=CSR_Age_Young}Young (20 years)",
                    "{=CSR_Age_Young_Desc}You are young and have your whole life ahead of you. Less experience, but more time.",
                    StartingAge.Young, 2, 1),
                A("cs_age_adult",
                    "{=CSR_Age_Adult}Adult (30 years)",
                    "{=CSR_Age_Adult_Desc}You are in your prime. A balanced start with moderate experience.",
                    StartingAge.Adult, 4, 2),
                A("cs_age_mature",
                    "{=CSR_Age_Mature}Mature (40 years)",
                    "{=CSR_Age_Mature_Desc}Years of experience have honed your skills. More capable, but less time remaining.",
                    StartingAge.Mature, 6, 3),
                A("cs_age_veteran",
                    "{=CSR_Age_Veteran}Veteran (50 years)",
                    "{=CSR_Age_Veteran_Desc}A lifetime of experience. Highly skilled, but your best years are behind you.",
                    StartingAge.Veteran, 8, 4)
            };

            return new LifePathMenuDef(AgeMenuId,
                "{=CSR_Age}Your Age",
                "{=CSR_Age_Desc}How old are you now?",
                choices);
        }

        #endregion
    }
}
