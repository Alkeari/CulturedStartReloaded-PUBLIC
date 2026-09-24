using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Builds companions and adult relatives into believable people scaled to
    ///     the start's power: each rolls a battlefield role (heavy infantry,
    ///     skirmisher, archer, crossbowman, light or heavy cavalry, horse
    ///     archer), gets role-focused skills and perks at a level trailing the
    ///     player's, a coherent tier-bound loadout for that role (a bow always
    ///     brings arrows, cavalry always brings a mount they can ride), and a
    ///     sprinkling of support skills so no two feel identical. A pauper's kin
    ///     wear rags; a monarch's ride in plate.
    /// </summary>
    public static class HeroOutfitter
    {
        public enum Role
        {
            HeavyInfantry,
            Skirmisher,
            Archer,
            Crossbowman,
            LightCavalry,
            HeavyCavalry,
            HorseArcher
        }

        /// <summary>What a role focuses, and what it merely carries.</summary>
        private const int PrimaryFocus = 4;

        private const int SecondaryFocus = 2;

        private static readonly SkillObject[] SupportSkills =
        {
            DefaultSkills.Medicine, DefaultSkills.Steward, DefaultSkills.Scouting, DefaultSkills.Trade,
            DefaultSkills.Engineering, DefaultSkills.Leadership, DefaultSkills.Tactics, DefaultSkills.Roguery,
            DefaultSkills.Charm, DefaultSkills.Crafting
        };

        public static void Outfit(Hero hero, CharacterCreationSession session, CSSettings? settings,
            HeroSpec? spec = null)
        {
            try
            {
                if (hero.HeroDeveloper == null) return;

                // Who the spec says the character is. The gender and the culture already chose the
                // template the hero was drawn from; a culture no template carried is set here, and
                // a gender no template carried can only be logged, since a drawn body keeps its sex
                if (spec?.Name != null)
                    hero.SetName(HeroNameGenerator.AsText(spec.Name), HeroNameGenerator.AsText(spec.Name));
                if (spec?.CultureId != null && hero.Culture?.StringId != spec.CultureId)
                {
                    var culture = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CultureObject>(spec.CultureId);
                    if (culture != null) hero.Culture = culture;
                }
                if (spec?.IsFemale != null && hero.IsFemale != spec.IsFemale.Value)
                    CSLogger.Warn($"HeroOutfitter: no {(spec.IsFemale.Value ? "female" : "male")} " +
                                  $"template could be drawn for {hero.Name}.");

                int tier = EquipmentStep.EffectiveTier(session, settings);

                if ((int)hero.Age < GameCaps.MinAdultAge())
                {
                    OutfitChild(hero, tier);
                    return;
                }

                int playerLevel = Math.Max(5, Hero.MainHero?.Level ?? 5);
                int level = spec?.Level ?? Math.Max(1, playerLevel * (45 + CSRandom.Next(46)) / 100);
                var role = spec?.Role != null && Enum.TryParse(spec.Role, out Role chosenRole)
                    ? chosenRole
                    : PickRole(tier);

                // Read before the template's sheet is taken off, since that sheet is the floor
                var floor = TemplateFloor(hero, session, spec);
                if (floor != null) level = Math.Max(level, floor.Value.Level);

                hero.HeroDeveloper.SetInitialLevel(level);
                BlankTheTemplateSheet(hero);
                ApplySkills(hero, role, level, floor?.Best ?? 0);

                // Exact skill levels layered over the role's rolls
                if (spec != null)
                    foreach (var pair in spec.SkillLevels)
                    {
                        var skill = TaleWorlds.CampaignSystem.Extensions.Skills.All
                            .FirstOrDefault(s => s.StringId == pair.Key);
                        if (skill != null)
                            hero.HeroDeveloper.SetInitialSkillLevel(skill, pair.Value);
                    }

                // Through the same writers the player's own exact values go through.
                // Neither is floored: only a value the editor names is written, and
                // one it leaves out stays as the template made the hero
                if (spec != null)
                {
                    NarrativeStep.WriteAttributes(hero, spec.Attributes);
                    NarrativeStep.WriteFocus(hero, spec.Focus);
                    ConsequenceStep.WriteTraits(hero, spec.Traits);
                }

                AutoFillPerks(hero, spec?.Perks);

                // The level LAST, and written as the number itself.
                // SetInitialLevel above only prices the experience a hero of this
                // level would have paid; the level a campaign reads is a field of
                // its own, and the only thing that ever moves it is CheckLevel,
                // which walks it UP one step at a time and never down. So a
                // companion asked to be level 3 kept the level 15 the wanderer
                // template arrived at, and one asked for level 30 showed nothing
                // until the first experience they earned on the map.
                hero.Level = level;
                ApplyGear(hero, role, session, tier);

                // Exact gear picks win over the role loadout; null empties the slot
                if (spec != null && hero.BattleEquipment != null)
                    foreach (var pair in spec.Gear)
                        hero.BattleEquipment[pair.Key] = pair.Value != null
                            ? new EquipmentElement(pair.Value)
                            : default;

                EquipmentGenerator.ClearHarnessWithoutMount(hero.BattleEquipment);

                // Raising level and skills raises maximum health; without this the
                // hero spawns at a sliver of the new maximum
                hero.HitPoints = hero.MaxHitPoints;

                CSLogger.Info($"HeroOutfitter: {hero.Name} outfitted as {role} at level {level} (tier {tier}).");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"HeroOutfitter: outfitting {hero?.Name} failed.", ex);
            }
        }

        private static readonly EquipmentIndex[] WarKitSlots =
        {
            EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3,
            EquipmentIndex.ExtraWeaponSlot, EquipmentIndex.Horse, EquipmentIndex.HorseHarness
        };

        /// <summary>
        ///     Anyone below the game's own coming-of-age line is not a soldier:
        ///     no level, no combat skills, no perks, and no war kit. Relatives
        ///     are built from adult lord templates and then aged down, so
        ///     without this a young sister inherits plate, a warhorse and a
        ///     lance she is far too small to be holding.
        /// </summary>
        public static void OutfitChild(Hero hero, int tier)
        {
            var battle = hero.BattleEquipment;
            if (battle == null) return;

            // Well dressed for the family's standing, never armored
            int clothingTier = Math.Max(0, Math.Min(tier, 2));

            foreach (var slot in EquipmentGenerator.ArmorSlots)
            {
                var item = EquipmentGenerator.FindArmorItem(slot, clothingTier, hero.Culture, -1, true);
                battle[slot] = item != null ? new EquipmentElement(item) : default;
            }

            foreach (var slot in WarKitSlots)
                battle[slot] = default;

            var childOutfitter = new EquipmentGenerator();
            childOutfitter.GenerateCivilianEquipment(hero, clothingTier);
            childOutfitter.GenerateStealthEquipment(hero, clothingTier);
            hero.HitPoints = hero.MaxHitPoints;

            CSLogger.Info(
                $"HeroOutfitter: {hero.Name} is {(int)hero.Age}, below the adult age of " +
                $"{GameCaps.MinAdultAge()}; dressed as a child with no war kit.");
        }

        /// <summary>Mounted roles need the tier to actually afford a mount.</summary>
        private static Role PickRole(int tier)
        {
            var roles = new List<Role> { Role.HeavyInfantry, Role.Skirmisher, Role.Archer, Role.Crossbowman };
            if (tier >= 2)
            {
                roles.Add(Role.LightCavalry);
                roles.Add(Role.HorseArcher);
            }

            if (tier >= 4)
                roles.Add(Role.HeavyCavalry);

            return roles[CSRandom.Next(roles.Count)];
        }

        /// <summary>
        ///     The role's own sheet, at the level this hero was asked for.
        ///
        ///     Nothing here is a figure this mod chose. Which skills a role writes
        ///     is the role; how far the sheet falls away from its best skill is the
        ///     shape this world's own adults have; and how high the best of them
        ///     stands is whatever the game's development model prices that level at.
        ///     A level-one companion therefore reads as a novice, which is what the
        ///     editor was told it would be.
        /// </summary>
        /// <summary>
        ///     What the template a Cultured Start character was drawn from already
        ///     made of them: its level and its best skill.
        ///
        ///     That route shipped its companions, relatives and founding lords with
        ///     the template's own sheet as the floor under everything written on top,
        ///     so a sheet rebuilt from the role must not come out weaker than the
        ///     template was. The editor's own level is the player's word and is never
        ///     floored, which is what still lets a genuine novice be built there. Null
        ///     on every other route, and wherever a level was set by hand.
        /// </summary>
        private static (int Level, int Best)? TemplateFloor(Hero hero, CharacterCreationSession session,
            HeroSpec? spec)
        {
            if (session.Mode != SetupMode.LifePath || spec?.Level != null) return null;

            int best = 0;
            foreach (var skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                if (skill != null)
                    best = Math.Max(best, hero.GetSkillValue(skill));

            CSLogger.Info($"HeroOutfitter: {hero.Name}'s template stands at level {hero.Level} " +
                          $"with a best skill of {best}; Cultured Start keeps both as the floor.");
            return (hero.Level, best);
        }

        private static void ApplySkills(Hero hero, Role role, int level, int floorBest)
        {
            var primaries = new List<SkillObject>();
            var secondaries = new List<SkillObject> { DefaultSkills.Athletics };
            switch (role)
            {
                case Role.HeavyInfantry:
                    primaries.Add(CSRandom.Next(2) == 0 ? DefaultSkills.OneHanded : DefaultSkills.TwoHanded);
                    secondaries.Add(DefaultSkills.Polearm);
                    break;
                case Role.Skirmisher:
                    primaries.Add(DefaultSkills.Throwing);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
                case Role.Archer:
                    primaries.Add(DefaultSkills.Bow);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
                case Role.Crossbowman:
                    primaries.Add(DefaultSkills.Crossbow);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
                case Role.LightCavalry:
                    primaries.Add(DefaultSkills.Polearm);
                    primaries.Add(DefaultSkills.Riding);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
                case Role.HeavyCavalry:
                    primaries.Add(DefaultSkills.Polearm);
                    primaries.Add(DefaultSkills.Riding);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
                case Role.HorseArcher:
                    primaries.Add(DefaultSkills.Bow);
                    primaries.Add(DefaultSkills.Riding);
                    secondaries.Add(DefaultSkills.OneHanded);
                    break;
            }

            // One or two support talents so companions differ beyond the battlefield
            int supportCount = 1 + CSRandom.Next(2);
            var support = SupportSkills.OrderBy(_ => CSRandom.Next(1000)).Take(supportCount).ToList();

            var sheet = new List<(SkillObject Skill, int Focus)>();
            foreach (var skill in primaries) sheet.Add((skill, PrimaryFocus));
            foreach (var skill in secondaries) sheet.Add((skill, SecondaryFocus));
            foreach (var skill in support) sheet.Add((skill, SecondaryFocus));

            var shape = StorySkills.SheetShape();
            if (shape.Count < sheet.Count)
            {
                CSLogger.Warn($"HeroOutfitter: the world holds nobody to shape {hero.Name}'s sheet against; " +
                              "their skills are left as the blank sheet.");
                return;
            }

            int top = Math.Max(StorySkills.TopOfSheet(level, shape.Take(sheet.Count)),
                Math.Min(GameCaps.MaxSkillLevel(), floorBest));

            for (int rank = 0; rank < sheet.Count; rank++)
                SetSkill(hero, sheet[rank].Skill, (int)Math.Round(top * shape[rank]), sheet[rank].Focus);
        }

        /// <summary>
        ///     Takes the template's own sheet off before the mod writes its own.
        ///
        ///     A generated hero is copied from a wanderer or lord template, which
        ///     arrives around level fifteen with skills up past a hundred and the
        ///     perks those skills reach already selected. Everything after this
        ///     used to be layered ON TOP of that, and every write was one-way, so
        ///     the template was a floor: no skill could be put below it, the perks
        ///     it had bought stayed bought, and a novice was unbuildable however
        ///     low the player set the figure. What the mod says it granted is now
        ///     the whole of what the hero carries.
        ///
        ///     Attributes and traits are deliberately left alone. The mod writes
        ///     them only where the Start Editor names an exact value, and blanking
        ///     the rest would hand the campaign a hero with no personality and no
        ///     attributes at all.
        /// </summary>
        private static void BlankTheTemplateSheet(Hero hero)
        {
            hero.ClearPerks();

            foreach (var skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                hero.HeroDeveloper.SetInitialSkillLevel(skill, 0);
                SetFocus(hero, skill, 0);
            }
        }

        private static void SetSkill(Hero hero, SkillObject skill, int value, int focus)
        {
            // Absolute, not a raise. The guard that used to stand here made every
            // figure below whatever the hero already had unreachable
            hero.HeroDeveloper.SetInitialSkillLevel(skill, value);
            SetFocus(hero, skill, Math.Min(GameCaps.MaxFocus(), focus));
        }

        /// <summary>
        ///     Focus as a target rather than an amount added. The developer's own
        ///     absolute setter is private, so the target is reached from wherever
        ///     the hero stands, in whichever direction that is.
        /// </summary>
        private static void SetFocus(Hero hero, SkillObject skill, int target)
        {
            int current = hero.HeroDeveloper.GetFocus(skill);
            if (target > current)
                hero.HeroDeveloper.AddFocus(skill, target - current, false);
            else if (target < current)
                hero.HeroDeveloper.RemoveFocus(skill, current - target);
        }

        /// <summary>Every reachable perk slot filled with the model's own pick.</summary>
        private static void AutoFillPerks(Hero hero, IReadOnlyDictionary<string, List<string>>? chosen)
        {
            // A skill the editor names perks for takes exactly those it reaches, and fills nothing more
            bool allowBoth = GameCaps.AllowsBothPerks();
            if (chosen != null)
                foreach (var pair in chosen)
                foreach (var perkId in pair.Value)
                {
                    var perk = PerkObject.All.FirstOrDefault(p => p.StringId == perkId);
                    if (perk == null || hero.GetSkillValue(perk.Skill) < perk.RequiredSkillValue) continue;
                    if (hero.GetPerkValue(perk)) continue;
                    if (!allowBoth && perk.AlternativePerk != null && hero.GetPerkValue(perk.AlternativePerk)) continue;
                    hero.HeroDeveloper.AddPerk(perk);
                }

            var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
            foreach (var perk in PerkObject.All)
            {
                if (chosen != null && chosen.ContainsKey(perk.Skill.StringId)) continue;
                if (hero.GetSkillValue(perk.Skill) < perk.RequiredSkillValue) continue;
                if (hero.GetPerkValue(perk)) continue;
                if (perk.AlternativePerk != null && hero.GetPerkValue(perk.AlternativePerk)) continue;

                var pick = model?.GetNextPerkToChoose(hero, perk) ?? perk;
                if (!hero.GetPerkValue(pick))
                    hero.HeroDeveloper.AddPerk(pick);
            }

            hero.HeroDeveloper.ClearUnspentPoints();
        }

        private static void ApplyGear(Hero hero, Role role, CharacterCreationSession session, int tier)
        {
            if (hero.BattleEquipment == null) return;

            var culture = hero.Culture;

            // Light roles dress a tier down so archers do not clank around in plate
            bool lightArmor = role is Role.Skirmisher or Role.Archer or Role.HorseArcher;
            int armorTier = lightArmor ? Math.Max(0, tier - 1) : tier;

            foreach (var slot in EquipmentGenerator.ArmorSlots)
            {
                var item = EquipmentGenerator.FindArmorItem(slot, armorTier, culture);
                if (item != null)
                    hero.BattleEquipment[slot] = new EquipmentElement(item);
            }

            if (role is Role.LightCavalry or Role.HeavyCavalry or Role.HorseArcher)
            {
                int riding = hero.GetSkillValue(DefaultSkills.Riding);

                // A mount the game has barding for is preferred, and the barding is then drawn
                // from that animal's own family: the two are one choice, never two
                var horse = EquipmentGenerator.FindArmorItem(EquipmentIndex.Horse, tier, culture, riding,
                                mustFit: MountFit.HasAnyHarness)
                            ?? EquipmentGenerator.FindArmorItem(EquipmentIndex.Horse, tier, culture, riding);
                if (horse != null)
                {
                    hero.BattleEquipment[EquipmentIndex.Horse] = new EquipmentElement(horse);
                    hero.BattleEquipment[EquipmentIndex.HorseHarness] = default;

                    if (role == Role.HeavyCavalry)
                    {
                        var harness = EquipmentGenerator.FindArmorItem(EquipmentIndex.HorseHarness, tier,
                            culture, mustFit: item => MountFit.Fits(horse, item));
                        if (harness != null)
                            hero.BattleEquipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness);
                    }
                }
            }

            var loadout = WeaponsFor(role);
            var slots = new[]
            {
                EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3
            };
            int slotIndex = 0;
            foreach (var choice in loadout)
            {
                if (slotIndex >= slots.Length) break;
                var item = GearQuery.QuartermasterPick(choice, culture, tier, session);
                if (item == null) continue;
                hero.BattleEquipment[slots[slotIndex]] = new EquipmentElement(item);
                slotIndex++;
            }

            EquipmentGenerator.ClearHarnessWithoutMount(hero.BattleEquipment);

            var outfitter = new EquipmentGenerator();
            outfitter.GenerateCivilianEquipment(hero, armorTier);
            outfitter.GenerateStealthEquipment(hero, armorTier);
        }

        /// <summary>Coherent weapon sets per role; ranged weapons always bring ammunition.</summary>
        private static List<WeaponClassChoice> WeaponsFor(Role role)
        {
            return role switch
            {
                Role.HeavyInfantry => new List<WeaponClassChoice>
                {
                    CSRandom.Next(2) == 0 ? WeaponClassChoice.OneHandedSword : WeaponClassChoice.OneHandedAxe,
                    WeaponClassChoice.LargeShield,
                    CSRandom.Next(2) == 0 ? WeaponClassChoice.TwoHandedAxe : WeaponClassChoice.Javelin
                },
                Role.Skirmisher => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Javelin, WeaponClassChoice.Javelin,
                    WeaponClassChoice.OneHandedSword, WeaponClassChoice.SmallShield
                },
                Role.Archer => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Bow, WeaponClassChoice.Arrows, WeaponClassChoice.Arrows,
                    WeaponClassChoice.OneHandedSword
                },
                Role.Crossbowman => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Crossbow, WeaponClassChoice.Bolts, WeaponClassChoice.Bolts,
                    WeaponClassChoice.OneHandedSword
                },
                Role.LightCavalry => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Spear, WeaponClassChoice.OneHandedSword,
                    WeaponClassChoice.SmallShield, WeaponClassChoice.Javelin
                },
                Role.HeavyCavalry => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Lance, WeaponClassChoice.OneHandedSword, WeaponClassChoice.LargeShield
                },
                Role.HorseArcher => new List<WeaponClassChoice>
                {
                    WeaponClassChoice.Bow, WeaponClassChoice.Arrows, WeaponClassChoice.Arrows,
                    WeaponClassChoice.OneHandedSword
                },
                _ => new List<WeaponClassChoice> { WeaponClassChoice.OneHandedSword, WeaponClassChoice.LargeShield }
            };
        }
    }
}
