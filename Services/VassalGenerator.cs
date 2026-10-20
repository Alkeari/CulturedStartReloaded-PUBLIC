using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Creates the founding vassal clans of a new monarch's realm as real
    ///     noble houses, not lone riders: a mounted lord (lords ride, always)
    ///     outfitted by the same generator that builds companions, a spouse and
    ///     children as their ages permit, siblings, aged parents of whom one or
    ///     both have often already passed, standing relations with other lords,
    ///     a filled party of recruitable troops, provisions, pack animals, and a
    ///     purse. The household settles at the clan's home.
    /// </summary>
    public static class VassalGenerator
    {
        private static readonly string[] MountedRoles =
        {
            nameof(HeroOutfitter.Role.LightCavalry),
            nameof(HeroOutfitter.Role.HeavyCavalry),
            nameof(HeroOutfitter.Role.HorseArcher)
        };

        public static Hero? CreateVassalClan(Kingdom kingdom, Settlement home, int index,
            CharacterCreationSession session, CSSettings? settings)
        {
            return CreateNobleClan(kingdom, null, home, $"csr_vassal_{index}", session, settings);
        }

        /// <summary>
        ///     A fellow rebel house: the same full noble clan, sworn to no realm,
        ///     at war with the realm the rebellion rose against, and standing
        ///     firmly with the player.
        /// </summary>
        public static Hero? CreateRebelAllyClan(Kingdom warTarget, Settlement home, int index,
            CharacterCreationSession session, CSSettings? settings)
        {
            return CreateNobleClan(null, warTarget, home, $"csr_rebel_{index}", session, settings);
        }

        private static Hero? CreateNobleClan(Kingdom? kingdom, Kingdom? warTarget, Settlement home,
            string idStem, CharacterCreationSession session, CSSettings? settings)
        {
            try
            {
                var culture = kingdom?.Culture ?? home.Culture ?? Hero.MainHero?.Culture;
                if (culture == null) return null;

                bool isFemale = CSRandom.Next(4) == 0;
                int leaderAge = 25 + CSRandom.Next(21);
                var leader = CreateNoble(culture, home, isFemale, leaderAge);
                if (leader == null) return null;

                // Cultured Start's houses are named for their founders and carry no
                // written history, as that route has always raised them
                bool culturedStart = session.Mode == Models.SetupMode.LifePath;
                var clanName = culturedStart
                    ? new TextObject("{=CSR_VassalClanName}House of {LEADER}")
                        .SetTextVariable("LEADER", leader.FirstName ?? leader.Name)
                    : ClanNameGenerator.Compose(culture, home, leader);

                var clan = Clan.CreateClan($"{idStem}_{leader.StringId}");
                clan.ChangeClanName(clanName, clanName);
                clan.Culture = culture;
                VersionedGameApi.SetBanner(clan, kingdom != null
                    ? Banner.CreateOneColoredBannerWithOneIcon(
                        kingdom.Banner.GetFirstIconColor(), kingdom.Banner.GetPrimaryColor(), -1)
                    : Banner.CreateRandomClanBanner(leader.StringId?.GetHashCode() ?? -1));
                clan.IsNoble = true;
                VersionedGameApi.SetHomeSettlement(clan, home);
                leader.Clan = clan;
                clan.SetLeader(leader);

                // A clan head, so it carries an entry; the household below does not
                var lore = culturedStart ? null : HeroLore.ComposeForVassal(leader, home, kingdom == null);
                if (lore != null) leader.EncyclopediaText = lore;
                // Renown drives the tier; AddRenown recalculates it properly
                int targetTier = Math.Max(2, session.EffectiveClanTier - 2);
                clan.AddRenown(CSSettings.GetRenownForTier(targetTier), false);
                if (kingdom != null)
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);

                // Lords ride: the leader is always one of the cavalry roles
                var leaderSpec = new HeroSpec { Role = MountedRoles[CSRandom.Next(MountedRoles.Length)] };
                HeroOutfitter.Outfit(leader, session, settings, leaderSpec);
                GiveGoldAction.ApplyBetweenCharacters(null, leader, 5000 + clan.Tier * 5000, true);

                CreateHousehold(leader, clan, culture, home, session, settings);
                SeedRelations(leader);

                if (warTarget != null)
                {
                    try
                    {
                        DeclareWarAction.ApplyByRebellion(warTarget, clan);
                    }
                    catch (Exception ex)
                    {
                        CSLogger.Warn($"VassalGenerator: rebel war declaration failed: {ex.Message}");
                    }

                    if (Hero.MainHero != null)
                        leader.SetPersonalRelation(Hero.MainHero, 40);
                }

                var party = global::Helpers.MobilePartyHelper.SpawnLordParty(leader, home);
                if (party != null)
                {
                    TroopStep.FillPartyForHero(leader, 20 + clan.Tier * 12 + CSRandom.Next(15), clan.Tier);
                    Provision(party);
                }

                CSLogger.Info(kingdom != null
                    ? $"VassalGenerator: {clanName} founded under {kingdom.Name} at {home.Name}."
                    : $"VassalGenerator: rebel house {clanName} risen at {home.Name}.");
                return leader;
            }
            catch (Exception ex)
            {
                CSLogger.Error("VassalGenerator: noble clan creation failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     The noble household: a spouse (usually), children as the couple's
        ///     ages permit, a sibling or two, and parents old enough for it all,
        ///     of whom one or both have often already died. Everyone alive
        ///     settles at the clan's home so nobody drifts in limbo.
        /// </summary>
        private static void CreateHousehold(Hero leader, Clan clan, CultureObject culture, Settlement home,
            CharacterCreationSession session, CSSettings? settings)
        {
            int comesOfAge = GameCaps.MinAdultAge();

            // Parents: aged to fit, each with their own odds of having passed
            int fatherAge = (int)leader.Age + comesOfAge + 4 + CSRandom.Next(8);
            int motherAge = (int)leader.Age + comesOfAge + 2 + CSRandom.Next(6);
            var father = CreateNoble(culture, home, false, fatherAge, clan);
            var mother = CreateNoble(culture, home, true, motherAge, clan);
            if (father != null && mother != null)
            {
                leader.Father = father;
                leader.Mother = mother;
                Marry(father, mother);
                if (CSRandom.Next(100) < 60) Kill(father);
                else Settle(father, home);
                if (CSRandom.Next(100) < 45) Kill(mother);
                else Settle(mother, home);
            }

            // Spouse and children
            Hero? spouse = null;
            if (CSRandom.Next(100) < 85)
            {
                int spouseAge = Math.Max(comesOfAge, (int)leader.Age - 3 + CSRandom.Next(7));
                spouse = CreateNoble(culture, home, !leader.IsFemale, spouseAge, clan);
                if (spouse != null)
                {
                    Marry(leader, spouse);
                    HeroOutfitter.Outfit(spouse, session, settings);
                    Settle(spouse, home);
                }
            }

            if (spouse != null)
            {
                int maxChildAge = Math.Max(0, Math.Min((int)leader.Age, (int)spouse.Age) - comesOfAge);
                int childCount = maxChildAge > 0 ? CSRandom.Next(4) : 0;
                for (int i = 0; i < childCount; i++)
                {
                    var motherHero = leader.IsFemale ? leader : spouse;
                    var fatherHero = leader.IsFemale ? spouse : leader;
                    var child = HeroCreator.DeliverOffSpring(motherHero, fatherHero, CSRandom.Next(2) == 0);
                    if (child == null) continue;
                    // Wanderer-templated parents pass epithets into generated
                    // child names; noble children carry plain names
                    NameGenerator.Current.GenerateHeroNameAndHeroFullName(child,
                        out var childFirstName, out var childFullName, false);
                    child.SetName(childFullName, childFirstName);
                    child.SetBirthDay(BirthDates.ForAge(Math.Max(1, 1 + CSRandom.Next(maxChildAge))));
                    Settle(child, home);
                }
            }

            // A sibling or two, linked into the tree
            int siblingCount = CSRandom.Next(3);
            for (int i = 0; i < siblingCount; i++)
            {
                int age = Math.Max(comesOfAge, (int)leader.Age - 6 + CSRandom.Next(13));
                var sibling = CreateNoble(culture, home, CSRandom.Next(2) == 0, age, clan);
                if (sibling == null) continue;
                if (father != null) sibling.Father = father;
                if (mother != null) sibling.Mother = mother;
                HeroOutfitter.Outfit(sibling, session, settings);
                Settle(sibling, home);
            }
        }

        /// <summary>Lords have histories: standing likes and grudges with other lords.</summary>
        private static void SeedRelations(Hero leader)
        {
            var others = Hero.AllAliveHeroes
                .Where(h => h.IsLord && h != leader && h.Clan != leader.Clan && h.Clan?.Kingdom != null)
                .ToList();

            int count = 2 + CSRandom.Next(3);
            for (int i = 0; i < count; i++)
            {
                var other = CSRandom.Pick(others);
                if (other == null) break;
                leader.SetPersonalRelation(other, -30 + CSRandom.Next(61));
            }
        }

        private static void Marry(Hero first, Hero second)
        {
            try
            {
                MarriageAction.Apply(first, second, false);
                if (first.Spouse == second) return;

                var spouseSetter = HarmonyLib.AccessTools.PropertySetter(typeof(Hero), nameof(Hero.Spouse));
                if (spouseSetter == null) return;
                spouseSetter.Invoke(first, new object[] { second });
                spouseSetter.Invoke(second, new object[] { first });
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"VassalGenerator: marriage failed: {ex.Message}");
            }
        }

        private static void Kill(Hero hero)
        {
            try
            {
                KillCharacterAction.ApplyByOldAge(hero, false);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"VassalGenerator: pass-away failed: {ex.Message}");
            }
        }

        private static void Settle(Hero hero, Settlement home)
        {
            try
            {
                if (hero.IsAlive)
                    EnterSettlementAction.ApplyForCharacterOnly(hero, home);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"VassalGenerator: settling {hero.Name} failed: {ex.Message}");
            }
        }

        private static Hero? CreateNoble(CultureObject culture, Settlement home, bool isFemale, int age,
            Clan? clan = null)
        {
            var templates = culture.LordTemplates?
                .Where(t => t.Occupation == Occupation.Lord && t.IsFemale == isFemale)
                .ToList();

            bool fromWandererRoster = false;
            if (templates == null || templates.Count == 0)
            {
                var wanderers = CharacterObject.All
                    .Where(c => c.IsTemplate && c.Occupation == Occupation.Wanderer && c.IsFemale == isFemale)
                    .ToList();
                templates = wanderers.Where(t => t.Culture == culture).ToList();
                if (templates.Count == 0) templates = wanderers;
                fromWandererRoster = templates.Count > 0;
            }

            var template = CSRandom.Pick(templates);
            if (template == null) return null;

            var noble = HeroCreator.CreateSpecialHero(template, home, clan, null, age);
            if (fromWandererRoster)
            {
                noble.SetNewOccupation(Occupation.Lord);
                NameGenerator.Current.GenerateHeroNameAndHeroFullName(noble,
                    out var firstName, out var fullName, false);
                noble.SetName(fullName, firstName);
            }

            noble.SetBirthDay(BirthDates.ForAge(age));
            noble.ChangeState(Hero.CharacterStates.Active);
            noble.SetHasMet();
            if (Hero.MainHero != null)
                noble.SetPersonalRelation(Hero.MainHero, 20);
            return noble;
        }

        /// <summary>Grain to march on and pack animals to carry it.</summary>
        private static void Provision(TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            var grain = MBObjectManager.Instance.GetObject<ItemObject>("grain");
            if (grain != null)
                party.ItemRoster.AddToCounts(grain, 15 + CSRandom.Next(16));

            var packAnimal = MBObjectManager.Instance.GetObject<ItemObject>("mule")
                             ?? MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse");
            if (packAnimal != null)
                party.ItemRoster.AddToCounts(packAnimal, 2 + CSRandom.Next(3));
        }
    }
}
