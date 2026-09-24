using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Creates starting companions the same way the vanilla tavern hire does:
    ///     a hero from the culture's wanderer templates, added to the player clan
    ///     as a companion, then added to the party. This keeps them assignable as
    ///     governors, party leaders, and settlement guests.
    /// </summary>
    public class CompanionGenerator : ICompanionGenerator
    {
        /// <summary>
        ///     Where a hired companion starts with the player. Trait alignment
        ///     alone leaves some of them mildly negative, which is not what being
        ///     asked along means.
        /// </summary>
        private const int CompanionRelationValue = 10;

        /// <summary>
        ///     Who a person the story handed over turns out to be.
        ///
        ///     A guided run can name five, and until now none of them existed: the
        ///     ally was a string the encyclopedia paragraph read back and nothing
        ///     else. A friend the player cannot open, place or send anywhere is not
        ///     a friend, so each one is built as the person the scene described.
        /// </summary>
        internal sealed class AllyProfile
        {
            public AllyProfile(string id, bool isKin, HeroOutfitter.Role? role,
                int yearsFromPlayer, bool friend, int count)
            {
                Id = id;
                IsKin = isKin;
                Role = role;
                YearsFromPlayer = yearsFromPlayer;
                Friend = friend;
                Count = count;
            }

            public string Id { get; }

            /// <summary>Blood, so FamilyStep builds them into the family tree instead.</summary>
            public bool IsKin { get; }

            /// <summary>What they can do, where the scene says; null rolls.</summary>
            public HeroOutfitter.Role? Role { get; }

            /// <summary>Years older than the player; negative is younger.</summary>
            public int YearsFromPlayer { get; }

            /// <summary>
            ///     Whether the game should count them a friend. Somebody who "has
            ///     not gone anywhere since" is one; men paid a season in advance are
            ///     warm and are not.
            /// </summary>
            public bool Friend { get; }

            /// <summary>How many people the entry is. The paid men are plural.</summary>
            public int Count { get; }
        }

        /// <summary>
        ///     Every ally the scenes can hand over. One table, read by the step that
        ///     builds the companions AND by the step that builds the family, so the
        ///     aunt cannot be built twice or missed by both.
        /// </summary>
        internal static readonly IReadOnlyList<AllyProfile> AllyRoster = new[]
        {
            // "She taught you things nobody else under that roof knew": the
            // player's aunt, and the only ally who is blood
            new AllyProfile("the_one_nobody_sat_with", true, null, 0, true, 1),

            // Carried out of a room nobody else would go into, and alive now
            // because of it
            new AllyProfile("the_one_you_carried", false, HeroOutfitter.Role.HeavyInfantry, -1, true, 1),

            // A voice in a town square that put a price on what the player was
            // worth, and came and found them that evening
            new AllyProfile("the_one_who_spoke_for_you", false, HeroOutfitter.Role.Skirmisher, 1, true, 1),

            // Paid a season in advance, which nobody does. They came for the coin
            // and the life still owes for it
            new AllyProfile("the_paid_men", false, HeroOutfitter.Role.LightCavalry, 0, false, 2),

            // Somebody younger who wanted to know what the player knew, and asks
            // better questions now
            new AllyProfile("the_apprentice", false, HeroOutfitter.Role.Archer, -10, false, 1)
        };

        internal static AllyProfile? AllyFor(string id) =>
            AllyRoster.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

        /// <summary>True where the family step owns this ally rather than this one.</summary>
        internal static bool IsKinAlly(string id) => AllyFor(id)?.IsKin == true;

        public void GenerateCompanions(Hero mainHero, int count)
        {
            try
            {
                if (count <= 0) return;

                // The clan's own companion limit is the ceiling for a band chosen
                // from a menu: past it the game blocks further hiring and warns,
                // and a start that opened already blocked is not what the chapter
                // offered. The people the life NAMED are counted by the caller and
                // are not held back by this, because the panel promised them
                var tierModel = Campaign.Current?.Models?.ClanTierModel;
                if (tierModel != null && Clan.PlayerClan != null)
                    count = Math.Min(count, Math.Max(0, tierModel.GetCompanionLimit(Clan.PlayerClan)));
                if (count <= 0) return;

                var party = mainHero.PartyBelongedTo;
                if (party == null)
                {
                    CSLogger.Warn("CompanionGenerator: no party; skipping companions.");
                    return;
                }

                var templates = FindWandererTemplates(mainHero.Culture);
                if (templates.Count == 0)
                {
                    CSLogger.Warn("CompanionGenerator: no wanderer templates found; skipping companions.");
                    return;
                }

                var bornSettlement = mainHero.HomeSettlement
                                     ?? SettlementFinder.RandomCultureTown(mainHero.Culture);
                var session = CreationSession.Current;

                for (int i = 0; i < count; i++)
                {
                    // A party of wanderers all born the same year reads as a batch;
                    // the game's own wanderers are spread across their working lives
                    int companionAge = GameCaps.MinAdultAge() + 4 + CSRandom.Next(18);
                    var spec = i < session.CompanionSpecs.Count ? session.CompanionSpecs[i] : null;

                    var companion = Recruit(mainHero, party, templates, bornSettlement, session,
                        companionAge, CompanionRelationValue, spec);
                    if (companion == null) break;

                    CSLogger.Info(
                        $"CompanionGenerator: created {companion.Name}, age {companionAge}, " +
                        $"met, relation {CompanionRelationValue}.");
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("CompanionGenerator: failed to generate companions.", ex);
            }
        }

        /// <summary>
        ///     Builds the people a guided run's answers said left with the player,
        ///     and returns how many seats they took.
        ///
        ///     They come before the companions the player asked for, because a
        ///     person the story earned outranks a number chosen from a menu. Kin
        ///     are skipped: the aunt is built into the family tree by FamilyStep,
        ///     where a parent's sister actually belongs.
        ///
        ///     The clan's companion limit does not hold them back. The panel told
        ///     the player, at the moment they chose, that this person joins their
        ///     party, so this person joins their party. Nothing in the
        ///     game removes a companion for being over the limit: the limit blocks
        ///     further hiring and warns, and that is all it does, so the cost of
        ///     honoring the promise is a warning rather than somebody walking out.
        ///     The band chosen from a menu is what gives way instead.
        /// </summary>
        public int GenerateAllies(Hero mainHero, IReadOnlyList<string> allyIds)
        {
            int taken = 0;

            try
            {
                if (allyIds == null || allyIds.Count == 0) return 0;

                var party = mainHero.PartyBelongedTo;
                if (party == null)
                {
                    CSLogger.Warn("CompanionGenerator: no party; the allies the life earned cannot join.");
                    return 0;
                }

                var templates = FindWandererTemplates(mainHero.Culture);
                if (templates.Count == 0)
                {
                    CSLogger.Warn("CompanionGenerator: no wanderer templates found; skipping allies.");
                    return 0;
                }

                var bornSettlement = mainHero.HomeSettlement
                                     ?? SettlementFinder.RandomCultureTown(mainHero.Culture);
                var session = CreationSession.Current;
                int playerAge = Math.Max(GameCaps.MinAdultAge(), (int)mainHero.Age);

                foreach (string id in allyIds)
                {
                    var profile = AllyFor(id);
                    if (profile == null)
                    {
                        CSLogger.Warn($"CompanionGenerator: no profile for ally '{id}'; nobody was built.");
                        continue;
                    }

                    if (profile.IsKin) continue;

                    for (int i = 0; i < profile.Count; i++)
                    {
                        int age = Math.Max(GameCaps.MinAdultAge(),
                            playerAge + profile.YearsFromPlayer - 2 + CSRandom.Next(5));

                        // Somebody who has not gone anywhere since is a friend by the
                        // game's own reckoning, so they are put just past the line it
                        // draws rather than at a figure chosen here. Men paid a season
                        // in advance stop at the same ceiling every other start grant
                        // stops at: warm, and not owed anything
                        int relation = profile.Friend
                            ? Application.Steps.ConsequenceStep.FriendThreshold() +
                              Application.Steps.ConsequenceStep.RelationBonus
                            : Application.Steps.ConsequenceStep.RelationCeiling();

                        var spec = new Models.HeroSpec { Role = profile.Role?.ToString() };
                        var ally = Recruit(mainHero, party, templates, bornSettlement, session,
                            age, relation, spec);
                        if (ally == null) break;

                        taken++;
                        CSLogger.Info(
                            $"CompanionGenerator: \"{profile.Id}\" is {ally.Name}, age {age}, " +
                            $"{profile.Role?.ToString() ?? "rolled"}, met, relation {relation}" +
                            $"{(profile.Friend ? " (a friend by the game's own threshold)" : "")}.");
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("CompanionGenerator: building the allies the life earned failed.", ex);
            }

            return taken;
        }

        /// <summary>
        ///     One hero out of the wanderer roster, activated, met, related, joined
        ///     to the clan and put in the party, and dressed. Every companion this
        ///     mod makes comes through here, so what a generic hire gets and what a
        ///     person the story named gets differ only in the arguments.
        /// </summary>
        private static Hero? Recruit(Hero mainHero, MobileParty party,
            IReadOnlyList<CharacterObject> templates, Settlement? bornSettlement,
            CharacterCreationSession session, int age, int relation, Models.HeroSpec? spec)
        {
            // A companion set to a gender or a culture is drawn from the wanderers who have it;
            // where none do, the ordinary pool stands rather than no companion at all
            var pool = templates;
            if (spec?.IsFemale != null || spec?.CultureId != null)
            {
                var culture = spec.CultureId != null
                    ? TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CultureObject>(spec.CultureId)
                    : mainHero.Culture;
                var wanted = FindWandererTemplates(culture)
                    .Where(t => spec.IsFemale == null || t.IsFemale == spec.IsFemale.Value)
                    .ToList();
                if (wanted.Count > 0) pool = wanted;
            }

            var template = CSRandom.Pick(pool);
            if (template == null) return null;

            var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, age);
            hero.SetBirthDay(BirthDates.ForAge(age));
            // Vanilla activates spawned wanderers explicitly; without this the
            // hero is excluded from governor and party-leader candidate lists
            hero.ChangeState(Hero.CharacterStates.Active);
            hero.SetPersonalRelation(mainHero, relation);
            MarkMet(hero, session);
            AddCompanionAction.Apply(Clan.PlayerClan, hero);
            AddHeroToPartyAction.Apply(hero, party, true);
            HeroOutfitter.Outfit(hero, session,
                MCM.Abstractions.Base.Global.GlobalSettings<Settings.CSSettings>.Instance, spec);
            return hero;
        }

        /// <summary>
        ///     Marks a hero as somebody the player has actually met.
        ///
        ///     Two flags, not one. <c>SetHasMet</c> raises <c>HasMet</c> and lets
        ///     the game's own <c>HeroKnownInformationCampaignBehavior</c> raise
        ///     <c>IsKnownToPlayer</c> from the event that follows, and
        ///     <c>IsKnownToPlayer</c> is the flag
        ///     <c>DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf</c>
        ///     reads: without it the encyclopedia answers "You haven't met this
        ///     hero yet." and hides the portrait, the relation and every value.
        ///     That event only fires while the flag actually CHANGES and only
        ///     while that behavior is listening, so the second flag is set here
        ///     rather than assumed. Known faces carry the hero into the post-start
        ///     fixup, which re-asserts it once the campaign is really running.
        ///
        ///     Done BEFORE the companion joins the clan on purpose. Clan
        ///     membership makes that model answer true on its own, so a companion
        ///     who later leaves the clan would otherwise become a stranger again.
        ///
        ///     It lives here, and every step that puts a person in front of the
        ///     player calls it, because a second copy would be a second answer to
        ///     "has the player met this person" and the two would disagree. Its
        ///     proper home is a service of its own once one can be added.
        /// </summary>
        internal static void MarkMet(Hero hero, CharacterCreationSession session)
        {
            try
            {
                if (!hero.HasMet) hero.SetHasMet();
                hero.IsKnownToPlayer = true;
                if (!session.KnownFaces.Contains(hero))
                    session.KnownFaces.Add(hero);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"CompanionGenerator: marking {hero.Name} as met failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Wanderer templates exactly as vanilla's CompanionsCampaignBehavior
        ///     selects them: IsTemplate and Occupation.Wanderer. Filtering by
        ///     !IsHero instead admits multiplayer characters ("-MP-" names) that
        ///     produce broken campaign heroes. Culture-matched first, all cultures
        ///     as the fallback.
        /// </summary>
        private static List<CharacterObject> FindWandererTemplates(CultureObject? culture)
        {
            var all = CharacterObject.All
                .Where(c => c.IsTemplate && c.Occupation == Occupation.Wanderer)
                .ToList();

            var fromCulture = all.Where(t => t.Culture == culture).ToList();
            return fromCulture.Count > 0 ? fromCulture : all;
        }
    }
}
