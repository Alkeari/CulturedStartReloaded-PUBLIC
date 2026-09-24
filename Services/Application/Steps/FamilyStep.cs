using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Builds the composed family: parents at ages that fit the whole brood,
    ///     one or more spouses (extras marry only when the game's marriage model
    ///     allows polygamy; otherwise they join the clan unmarried), any
    ///     combination of siblings and children at chosen or sensible ages, and
    ///     marriages for adult kin, kept in the clan or married into another
    ///     realm's clan by vanilla's own clan-after-marriage rules. Dead
    ///     relatives are created and then passed away so they exist in the
    ///     family tree. Every stage degrades with a report rather than aborting.
    ///
    ///     On the guided route it also builds the kin the SCENES named. A life
    ///     that sat with an aunt nobody else would sit with, or carried a sister
    ///     out of a burning store, has to produce that person: a story that names
    ///     a relative who does not exist is the story disagreeing with the
    ///     character it just made.
    /// </summary>
    public sealed class FamilyStep : IStartStep
    {
        /// <summary>
        ///     Where blood starts: firmly on the player's side rather than at
        ///     trait-noise neutral, which is the relation the game itself counts as
        ///     the edge of friendship. One reading of that, shared with the step
        ///     that spends it on strangers and with the companion generator, so a
        ///     relation model that rescales the world rescales the family with it.
        /// </summary>
        private static int FamilyRelationValue => ConsequenceStep.FriendThreshold();

        /// <summary>
        ///     The scene-route ally id that is only ever produced by the aunt
        ///     option, and so is the one relative the catalog leaves a
        ///     machine-readable trace of. Taken from the one ally roster, which
        ///     also tells the companion step to leave her alone.
        /// </summary>
        private const string AuntAllyId = "the_one_nobody_sat_with";

        private StoryKin? _kin;

        /// <summary>One dead couple per side, so a parent's siblings have parents to share.</summary>
        private readonly Dictionary<KinSide, ForebearCouple> _forebears = new();

        public string Name => "Family";

        public string? Validate(StartContext context)
        {
            _kin = StoryKin.Read(context.Session);

            if (!NeedsWork(context.Session.FamilyMembers) && _kin.IsEmpty)
                return null;

            return context.Hero.Clan == null
                ? "hero has no clan"
                : null;
        }

        public void Apply(StartContext context)
        {
            var kin = _kin ??= StoryKin.Read(context.Session);
            var members = context.Session.FamilyMembers;

            // Before anything is measured: the three household chapters settle
            // parents, siblings and a hearth, and the scenes name relatives none of
            // those three axes carries, so the spec is a floor the life adds to
            // rather than the whole account
            AddStorySiblings(context, kin);

            if (!NeedsWork(members) && !kin.NeedsLineage)
            {
                CSLogger.Info("FamilyStep: default family (deceased parents); nothing to build.");
                return;
            }

            var hero = context.Hero;
            var session = context.Session;

            MaterializeAges(session);

            // Parents first: siblings and the family tree hang off them. They are
            // created whenever they should live, or when kin need the lineage links.
            bool fatherAlive = IsAlive(members, FamilyRelation.Father);
            bool motherAlive = IsAlive(members, FamilyRelation.Mother);
            bool kinNeedParents = members.Any(m => m.Relation is FamilyRelation.Brother or FamilyRelation.Sister);

            Hero? father = null;
            Hero? mother = null;
            if (fatherAlive || motherAlive || kinNeedParents || kin.NeedsLineage)
            {
                var fatherSpec = members.FirstOrDefault(m => m.Relation == FamilyRelation.Father)?.Advanced;
                var motherSpec = members.FirstOrDefault(m => m.Relation == FamilyRelation.Mother)?.Advanced;
                father = CreateRelative(context, false, FamilyAges.ParentAge(session, true), fatherSpec);
                mother = CreateRelative(context, true, FamilyAges.ParentAge(session, false), motherSpec);

                if (father != null && mother != null)
                {
                    hero.Father = father;
                    hero.Mother = mother;
                    TryMarry(father, mother);

                    // The forebears and the parents' own siblings are attached
                    // while both parents are still alive objects, because a
                    // lineage link set after the pass-away reads the same to the
                    // game but not to anyone following the code
                    BuildLineage(context, kin, father, mother);

                    if (!fatherAlive) PassAway(context, father);
                    else PlaceRelative(context, father, StaysInSettlement(members, FamilyRelation.Father), true);
                    if (!motherAlive) PassAway(context, mother);
                    else PlaceRelative(context, mother, StaysInSettlement(members, FamilyRelation.Mother), true);
                    CSLogger.Info(
                        $"FamilyStep: parents created (father {(fatherAlive ? "alive" : "dead")}, mother {(motherAlive ? "alive" : "dead")}).");
                }
                else
                {
                    context.Report.AddProblem("Family: parent creation failed");
                }
            }

            // Spouses: the first is the primary; extras marry only if the game's
            // marriage model permits it (polygamy mods), else they join unmarried
            var spouses = new List<Hero>();
            var spouseSpecs = members.Where(m => m.Relation == FamilyRelation.Spouse).ToList();
            bool childrenRequested = members.Any(m => m.Relation is FamilyRelation.Son or FamilyRelation.Daughter);
            if (spouseSpecs.Count == 0 && childrenRequested)
                spouseSpecs.Add(new FamilyMemberSpec(FamilyRelation.Spouse, true));

            for (int index = 0; index < spouseSpecs.Count; index++)
            {
                var spec = spouseSpecs[index];
                var spouse = CreateRelative(context, !hero.IsFemale, FamilyAges.MemberAge(session, spec),
                    spec.Advanced);
                if (spouse == null)
                {
                    context.Report.AddProblem("Family: no suitable spouse could be created");
                    continue;
                }

                bool married = index == 0
                    ? TryMarry(hero, spouse, allowDirectBind: true)
                    : TryMarry(hero, spouse, allowDirectBind: false);

                if (married)
                {
                    CSLogger.Info($"FamilyStep: married {spouse.Name}.");
                }
                else if (index > 0)
                {
                    context.Report.AddProblem(
                        $"Family: {spouse.Name} joined the clan unmarried; the game or its mods forbid another marriage");
                }

                if (!spec.IsAlive)
                {
                    PassAway(context, spouse);
                }
                else
                {
                    PlaceRelative(context, spouse, spec.StaysInSettlement, true);
                    spouses.Add(spouse);
                }
            }

            // Children: parented by a spouse old enough for each child's age
            foreach (var childSpec in members.Where(m =>
                         m.Relation is FamilyRelation.Son or FamilyRelation.Daughter))
            {
                var parent = PickChildParent(session, spouses, childSpec);
                if (parent == null)
                {
                    context.Report.AddProblem("Family: children need a living spouse; none could be created");
                    break;
                }

                CreateChild(context, hero, parent, childSpec);
            }

            // Siblings: any combination of brothers and sisters
            foreach (var sibling in members.Where(m =>
                         m.Relation is FamilyRelation.Brother or FamilyRelation.Sister))
                CreateSibling(context, hero, father, mother, sibling);
        }

        // ---------------------------------------------------------------------
        // What the scenes claimed
        // ---------------------------------------------------------------------

        /// <summary>Which parent an aunt descends from.</summary>
        private enum KinSide
        {
            Father,
            Mother
        }

        /// <summary>One sibling a scene named, and where the story puts them in the birth order.</summary>
        private sealed class SiblingClaim
        {
            public SiblingClaim(FamilyRelation relation, int yearsOlder, string source)
            {
                Relation = relation;
                YearsOlder = yearsOlder;
                Source = source;
            }

            public FamilyRelation Relation { get; }

            /// <summary>Years older than the player; negative is younger.</summary>
            public int YearsOlder { get; }

            /// <summary>The answer that claimed them, for the log.</summary>
            public string Source { get; }
        }

        /// <summary>An aunt: a sister of one of the player's parents.</summary>
        private sealed class ParentSiblingClaim
        {
            public ParentSiblingClaim(KinSide side, string source)
            {
                Side = side;
                Source = source;
            }

            public KinSide Side { get; }
            public string Source { get; }
        }

        /// <summary>
        ///     The dead grandparents on one side. The grandmother is absent only
        ///     where she could not be created.
        /// </summary>
        private sealed class ForebearCouple
        {
            public ForebearCouple(Hero grandfather, Hero? grandmother)
            {
                Grandfather = grandfather;
                Grandmother = grandmother;
            }

            public Hero Grandfather { get; }
            public Hero? Grandmother { get; }
        }

        /// <summary>
        ///     Every relative the guided run's answers claim, gathered once.
        ///
        ///     A fact this step builds that the player can be told about is
        ///     DECLARED by the answer, as <see cref="ConsequenceKind.Household" />,
        ///     so the effect panel and this read one statement and neither can
        ///     invent a relative the other never promised. A sibling whose kind
        ///     the scene leaves open declares one too: the run settles the kind
        ///     and the birth order when the answer is given, through
        ///     <see cref="StorySibling" />, and this asks the same question over
        ///     again rather than rolling one of its own. What is still matched on
        ///     option id is who the relative was in the telling, and an id the
        ///     catalog no longer carries simply claims nothing rather than failing
        ///     the start. The aunt is taken from her ally id first, because that
        ///     id is produced by no other option and so survives a rewording.
        /// </summary>
        private sealed class StoryKin
        {
            /// <summary>
            ///     What an answer declares when it points at whoever held the house
            ///     before the father did. The household chapters ask about parents,
            ///     siblings and a hearth, so a generation above all three is read
            ///     here rather than there.
            /// </summary>
            public const string ForebearsBuried = "forebears_buried";

            public List<SiblingClaim> Siblings { get; } = new();
            public List<ParentSiblingClaim> ParentSiblings { get; } = new();

            /// <summary>A generation above the parents the story pointed at.</summary>
            public bool ForebearsNamed { get; private set; }

            public bool IsEmpty =>
                Siblings.Count == 0 && ParentSiblings.Count == 0 && !ForebearsNamed;

            /// <summary>True when somebody has to be hung off a grandparent.</summary>
            public bool NeedsLineage => ParentSiblings.Count > 0 || ForebearsNamed;

            public static StoryKin Read(CharacterCreationSession session)
            {
                var kin = new StoryKin();

                try
                {
                    if (!GuidedRun.WasWalked) return kin;

                    var answers = SceneMenus.Answered;
                    if (answers == null) return kin;

                    var allies = GuidedRun.Outcome().Allies;

                    if (allies.Contains(AuntAllyId, StringComparer.Ordinal) ||
                        answers.Chose("cs_opt_your_aunt_and_she_was_not"))
                        kin.ParentSiblings.Add(new ParentSiblingClaim(
                            CSRandom.Next(2) == 0 ? KinSide.Father : KinSide.Mother,
                            "the one nobody sat with"));

                    // "He took what he wanted and the house arranged itself around
                    // that": a house that defers to him is a house where he came
                    // first
                    if (answers.Chose("cs_opt_your_brother_and_he_was"))
                        kin.Siblings.Add(new SiblingClaim(FamilyRelation.Brother,
                            3 + CSRandom.Next(6), "your brother, and he was"));

                    // She was carried out of the burning store, so she is younger
                    // and she came out of it alive
                    if (answers.Chose("cs_opt_nothing_your_hands_were_full"))
                        kin.Siblings.Add(new SiblingClaim(FamilyRelation.Sister,
                            -(2 + CSRandom.Next(5)), "nothing, your hands were full"));

                    // The whole of it was about somebody else under that roof.
                    // Which of them it was is not rolled here: the answer declares
                    // a sibling and the run settled the kind and the birth order
                    // when the player gave it, which is the only reason the panel
                    // beside that answer could state who they were getting
                    if (answers.Chose(StorySibling.DeclaredBy))
                    {
                        var settled = StorySibling.Of(session);
                        kin.Siblings.Add(new SiblingClaim(settled.Relation, settled.YearsOlder,
                            "they said nothing about you"));
                    }

                    // "The house was smaller than it had been handed to him", and
                    // "there had not been coin in it for two generations": both
                    // point at somebody who held the house before the father did,
                    // and both say so where the panel can read it
                    if (SceneReading.Consequences(GuidedRoute.ScenesOf(session), answers)
                        .Any(c => c.Kind == ConsequenceKind.Household &&
                                  string.Equals(c.Target, ForebearsBuried, StringComparison.Ordinal)))
                        kin.ForebearsNamed = true;
                }
                catch (Exception ex)
                {
                    CSLogger.Error("FamilyStep: reading the kin the scenes named failed.", ex);
                }

                return kin;
            }
        }

        /// <summary>
        ///     Makes sure every sibling the scenes named exists, without
        ///     contradicting the household the player composed.
        ///
        ///     The household chapter now asks for exact counts, so a living
        ///     sibling it carries ANSWERS a claim rather than sitting beside one:
        ///     a player who said two brothers and told a story about a brother has
        ///     two brothers, not three. A claim the household has nobody left for
        ///     is built and then buried. The relative is in the family tree, which
        ///     is what the story said, and the living count is exactly the one the
        ///     player chose, which is what the chapter said. Both accounts hold and
        ///     neither is quietly dropped.
        /// </summary>
        private static void AddStorySiblings(StartContext context, StoryKin kin)
        {
            if (kin.Siblings.Count == 0) return;

            var members = context.Session.FamilyMembers;

            // One living relative answers one claim and no more, or a life that
            // named a brother twice would be satisfied by a single brother
            var spoken = new HashSet<FamilyMemberSpec>();

            foreach (var claim in kin.Siblings)
            {
                var covered = members.FirstOrDefault(m =>
                    m.Relation == claim.Relation && m.IsAlive && !spoken.Contains(m));
                if (covered != null)
                {
                    spoken.Add(covered);
                    CSLogger.Info(
                        $"FamilyStep: \"{claim.Source}\" is the {claim.Relation} the household already carries.");
                    continue;
                }

                var spec = new FamilyMemberSpec(claim.Relation, false)
                {
                    Aging = SiblingAging.Smart,
                    SmartOffset = claim.YearsOlder,
                    StaysInSettlement = true
                };
                members.Add(spec);
                spoken.Add(spec);
                CSLogger.Info(
                    $"FamilyStep: \"{claim.Source}\" adds a deceased {claim.Relation} " +
                    $"({Math.Abs(claim.YearsOlder)} years {(claim.YearsOlder >= 0 ? "older" : "younger")}); " +
                    "the household the player composed has no living one to answer for them.");
            }
        }

        /// <summary>
        ///     The generation above the parents, and the parents' own brothers and
        ///     sisters.
        ///
        ///     The game models an aunt as a sibling of a parent, so one dead
        ///     married couple per side makes the link real: the aunt then appears
        ///     on the parent's own encyclopedia page, one step from the player's,
        ///     which is exactly where the base game puts one.
        /// </summary>
        private void BuildLineage(StartContext context, StoryKin kin, Hero father, Hero mother)
        {
            if (!kin.NeedsLineage) return;

            try
            {
                if (kin.ForebearsNamed)
                {
                    // Named but unpeopled: the answer pointed at whoever held the
                    // house before, and a father with parents is the whole claim
                    Forebears(context, KinSide.Father, father, mother);
                }

                foreach (var claim in kin.ParentSiblings)
                {
                    var parent = claim.Side == KinSide.Father ? father : mother;
                    var forebears = Forebears(context, claim.Side, father, mother);
                    if (forebears == null)
                    {
                        context.Report.AddProblem($"Family: no lineage could be built for {claim.Source}");
                        continue;
                    }

                    int age = FamilyAges.ParentSiblingAge(context.Session, claim.Side == KinSide.Father);
                    var relative = CreateRelative(context, true, age);
                    if (relative == null)
                    {
                        context.Report.AddProblem($"Family: {claim.Source} could not be created");
                        continue;
                    }

                    relative.Father = forebears.Grandfather;
                    if (forebears.Grandmother != null) relative.Mother = forebears.Grandmother;

                    // Alive, always. The household chapter asks about parents,
                    // siblings, a spouse and children and says nothing at all
                    // about a parent's sister, so no answer the player gave is
                    // contradicted by her being here. Old enough to have taught
                    // the player and never asked to ride: she keeps the door she
                    // always had
                    PlaceRelative(context, relative, true, true);

                    CSLogger.Info(
                        $"FamilyStep: aunt {relative.Name} (age {age}, alive) " +
                        $"on the {(claim.Side == KinSide.Father ? "father's" : "mother's")} side, " +
                        $"from \"{claim.Source}\"; sibling to {parent?.Name.ToString() ?? "that parent"}.");
                }
            }
            catch (Exception ex)
            {
                context.Report.AddProblem("Family: the lineage above your parents could not be built");
                CSLogger.Error("FamilyStep: building the lineage failed.", ex);
            }
        }

        /// <summary>
        ///     The dead grandparents on one side, created once and reused, with the
        ///     matching parent hung off them both. Two generations up is always
        ///     behind the character, so the couple is created, married and then
        ///     passed away, which is the shape every lord's tree in the base game
        ///     carries. A grandmother the game cannot build is left out rather than
        ///     half-built: an unmarried mother nobody descends from would read
        ///     worse than the grandfather alone.
        /// </summary>
        private ForebearCouple? Forebears(StartContext context, KinSide side, Hero father, Hero mother)
        {
            if (_forebears.TryGetValue(side, out var existing)) return existing;

            bool onFathersSide = side == KinSide.Father;
            var parent = onFathersSide ? father : mother;
            int age = FamilyAges.ForebearAge(context.Session, onFathersSide);
            var grandfather = CreateRelative(context, false, age);
            if (grandfather == null)
            {
                CSLogger.Warn($"FamilyStep: the forebear on the {side} side could not be created.");
                return null;
            }

            // The same years the mod puts between a husband and wife one
            // generation down, so the couple reads the way the parents do
            int spread = FamilyAges.ParentAge(context.Session, true) -
                         FamilyAges.ParentAge(context.Session, false);
            int grandmotherAge = age - spread;
            var grandmother = CreateRelative(context, true, grandmotherAge);
            if (grandmother == null)
                CSLogger.Warn($"FamilyStep: the forebear's wife on the {side} side could not be created.");

            parent.Father = grandfather;
            if (grandmother != null)
            {
                parent.Mother = grandmother;
                // Married while both are still living objects, the way the
                // player's own parents are, because the marriage model has
                // nothing to join once either of them is dead
                TryMarry(grandfather, grandmother);
            }

            PassAway(context, grandfather);
            if (grandmother != null) PassAway(context, grandmother);

            var couple = new ForebearCouple(grandfather, grandmother);
            _forebears[side] = couple;
            CSLogger.Info(
                $"FamilyStep: {grandfather.Name} held the house before {parent.Name} " +
                $"(age {age}, deceased, {side} side)" +
                (grandmother == null
                    ? "; no wife could be created for him."
                    : $", beside {grandmother.Name} (age {grandmotherAge}, deceased)."));
            return couple;
        }

        /// <summary>
        ///     Turns every unset age into a concrete one BEFORE anyone is created,
        ///     so the parents' ages can account for the whole brood's actual ages
        ///     instead of estimates. Spouses first, because the children's cap
        ///     depends on the spouse's age.
        /// </summary>
        private static void MaterializeAges(CharacterCreation.Session.CharacterCreationSession session)
        {
            int comesOfAge = GameCaps.MinAdultAge();
            int playerAge = session.EffectiveAge;

            foreach (var member in session.FamilyMembers)
                if (member.Age == null && member.Relation == FamilyRelation.Spouse)
                    member.Age = Math.Max(comesOfAge, playerAge - 4 + CSRandom.Next(6));

            foreach (var member in session.FamilyMembers)
            {
                if (member.Age != null) continue;
                switch (member.Relation)
                {
                    case FamilyRelation.Brother:
                    case FamilyRelation.Sister:
                        // Smart and Twin modes roll or fix the age themselves
                        member.Age = FamilyAges.MemberAge(session, member);
                        break;
                    case FamilyRelation.Son:
                    case FamilyRelation.Daughter:
                        // The same roll the automatic age uses, so a seeded family and a
                        // composed one age their children by one rule
                        member.Age = FamilyAges.MemberAge(session, member);
                        break;
                }
            }
        }

        private static bool NeedsWork(List<FamilyMemberSpec> members)
        {
            // The default is two dead parents and nobody else: vanilla-equivalent
            return members.Any(m =>
                m.Relation is not (FamilyRelation.Father or FamilyRelation.Mother) || m.IsAlive);
        }

        private static bool IsAlive(List<FamilyMemberSpec> members, FamilyRelation relation)
        {
            return members.FirstOrDefault(m => m.Relation == relation)?.IsAlive == true;
        }

        private static bool StaysInSettlement(List<FamilyMemberSpec> members, FamilyRelation relation)
        {
            return members.FirstOrDefault(m => m.Relation == relation)?.StaysInSettlement == true;
        }

        /// <summary>
        ///     Puts an alive relative WHERE the player composed them: adults ride
        ///     in the party unless kept at a holding; children and homebodies
        ///     settle at the best owned holding, falling back to their home town.
        /// </summary>
        private static void PlaceRelative(StartContext context, Hero relative, bool staysInSettlement,
            bool isAdult)
        {
            if (!relative.IsAlive || relative.Clan != Clan.PlayerClan) return;

            try
            {
                if (isAdult && !staysInSettlement && context.Hero.PartyBelongedTo != null)
                {
                    AddHeroToPartyAction.Apply(relative, context.Hero.PartyBelongedTo, false);
                    CSLogger.Info($"FamilyStep: {relative.Name} rides in the party.");
                    return;
                }

                var home = Clan.PlayerClan.Settlements?.FirstOrDefault(s => s.IsTown)
                           ?? Clan.PlayerClan.Settlements?.FirstOrDefault()
                           ?? relative.HomeSettlement
                           ?? context.Session.SelectedLocation;
                if (home != null)
                {
                    TaleWorlds.CampaignSystem.Actions.EnterSettlementAction
                        .ApplyForCharacterOnly(relative, home);
                    CSLogger.Info($"FamilyStep: {relative.Name} settles at {home.Name}.");
                }
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Family: placing {relative.Name} failed");
                CSLogger.Error("FamilyStep: relative placement failed.", ex);
            }
        }

        private static Hero? PickChildParent(CharacterCreation.Session.CharacterCreationSession session,
            List<Hero> spouses, FamilyMemberSpec childSpec)
        {
            int childAge = FamilyAges.MemberAge(session, childSpec);
            int comesOfAge = GameCaps.MinAdultAge();
            return spouses.FirstOrDefault(s => (int)s.Age >= childAge + comesOfAge)
                   ?? spouses.FirstOrDefault();
        }

        /// <summary>
        ///     Marries through the game's action first, so any modded marriage
        ///     model has its say. When the model silently refuses a marriage the
        ///     player composed on purpose, the spouses are bound directly, but
        ///     only where the player's design demands it.
        /// </summary>
        private static bool TryMarry(Hero first, Hero second, bool allowDirectBind = true)
        {
            try
            {
                MarriageAction.Apply(first, second, false);
                if (first.Spouse == second) return true;
                if (!allowDirectBind) return false;

                var spouseSetter = HarmonyLib.AccessTools.PropertySetter(typeof(Hero), nameof(Hero.Spouse));
                if (spouseSetter == null)
                {
                    CSLogger.Warn("FamilyStep: Hero.Spouse setter not found; marriage skipped.");
                    return false;
                }

                spouseSetter.Invoke(first, new object[] { second });
                spouseSetter.Invoke(second, new object[] { first });
                CSLogger.Info("FamilyStep: marriage model refused; spouses bound directly.");
                return first.Spouse == second;
            }
            catch (Exception ex)
            {
                CSLogger.Error("FamilyStep: marriage failed.", ex);
                return false;
            }
        }

        private static void PassAway(StartContext context, Hero relative)
        {
            try
            {
                KillCharacterAction.ApplyByOldAge(relative, false);
                CSLogger.Info($"FamilyStep: {relative.Name} passed away before the campaign.");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Family: could not mark {relative.Name} as deceased");
                CSLogger.Error("FamilyStep: pass-away failed.", ex);
            }
        }

        private static void CreateChild(StartContext context, Hero hero, Hero spouse, FamilyMemberSpec spec)
        {
            try
            {
                var mother = hero.IsFemale ? hero : spouse;
                var father = hero.IsFemale ? spouse : hero;
                bool isDaughter = spec.Relation == FamilyRelation.Daughter;
                var child = HeroCreator.DeliverOffSpring(mother, father, isDaughter);
                if (child == null)
                {
                    context.Report.AddProblem("Family: child creation returned nothing");
                    return;
                }

                // A parent built from a wanderer template can pass an epithet
                // ("the Exile") into the child's generated name; children of the
                // house carry a plain name
                NameGenerator.Current.GenerateHeroNameAndHeroFullName(child,
                    out var childFirstName, out var childFullName, false);
                child.SetName(childFullName, childFirstName);

                int childAge = FamilyAges.MemberAge(context.Session, spec);
                child.SetBirthDay(BirthDates.ForAge(childAge));
                child.SetPersonalRelation(hero, FamilyRelationValue);
                MarkKnown(context, child);

                // Born adults keep what the game gave them; the young are
                // dressed their age and carry nothing they could fight with
                if (childAge < GameCaps.MinAdultAge())
                    HeroOutfitter.OutfitChild(child,
                        EquipmentStep.EffectiveTier(context.Session, context.Settings));
                if (!spec.IsAlive)
                {
                    PassAway(context, child);
                }
                else
                {
                    ApplyKinMarriage(context, child, spec);
                    if (spec.Marriage != FamilyMarriage.MarriedAway)
                        PlaceRelative(context, child, spec.StaysInSettlement,
                            (int)child.Age >= GameCaps.MinAdultAge());
                }

                CSLogger.Info(
                    $"FamilyStep: {spec.Relation} {child.Name} (age {childAge}, {(spec.IsAlive ? "alive" : "dead")}).");
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Family: child creation failed ({ex.GetType().Name})");
                CSLogger.Error("FamilyStep: child creation failed.", ex);
            }
        }

        private static void CreateSibling(StartContext context, Hero hero, Hero? father, Hero? mother,
            FamilyMemberSpec spec)
        {
            int age = FamilyAges.MemberAge(context.Session, spec);
            bool isFemale = spec.Relation == FamilyRelation.Sister;
            var sibling = CreateRelative(context, isFemale, age, spec.Advanced);
            if (sibling == null)
            {
                context.Report.AddProblem("Family: sibling creation failed");
                return;
            }

            if (father != null) sibling.Father = father;
            if (mother != null) sibling.Mother = mother;
            if (!spec.IsAlive)
            {
                PassAway(context, sibling);
            }
            else
            {
                ApplyKinMarriage(context, sibling, spec);
                if (spec.Marriage != FamilyMarriage.MarriedAway)
                    PlaceRelative(context, sibling, spec.StaysInSettlement,
                        (int)sibling.Age >= GameCaps.MinAdultAge());
            }

            CSLogger.Info(
                $"FamilyStep: {spec.Relation} {sibling.Name} (age {age}, {(spec.IsAlive ? "alive" : "dead")}).");
        }

        /// <summary>
        ///     Marries an adult brother, sister, son, or daughter as composed:
        ///     to a created partner who joins the clan, or to an existing lord of
        ///     another realm, letting vanilla's clan-after-marriage rules decide
        ///     who moves where.
        /// </summary>
        private static void ApplyKinMarriage(StartContext context, Hero kin, FamilyMemberSpec spec)
        {
            if (spec.Marriage == FamilyMarriage.Single) return;
            if ((int)kin.Age < GameCaps.MinAdultAge()) return;

            // The game's clan-after-marriage rules never move a man to his
            // bride's clan, so married-away only exists for female kin; a male
            // spec that still says so marries within the clan instead
            var marriage = spec.Marriage;
            if (marriage == FamilyMarriage.MarriedAway && !kin.IsFemale)
            {
                CSLogger.Info($"FamilyStep: {kin.Name} cannot marry out (male); marrying within the clan.");
                marriage = FamilyMarriage.MarriedInClan;
            }

            if (marriage == FamilyMarriage.MarriedInClan)
            {
                var partner = CreateRelative(context, !kin.IsFemale, Math.Max(GameCaps.MinAdultAge(),
                    (int)kin.Age - 2 + CSRandom.Next(5)));
                if (partner == null || !TryMarry(kin, partner))
                {
                    context.Report.AddProblem($"Family: no partner could be married to {kin.Name}");
                }
                else
                {
                    // The couple stays together: the in-law follows the kin's
                    // placement instead of drifting in limbo
                    PlaceRelative(context, partner, spec.StaysInSettlement, true);
                    CSLogger.Info($"FamilyStep: {kin.Name} married {partner.Name} within the clan.");
                }

                return;
            }

            // Married away: an existing unmarried adult lord of another clan;
            // vanilla decides which spouse changes clans
            var candidates = Hero.AllAliveHeroes
                .Where(h => h.IsLord && h.Spouse == null && h.IsFemale != kin.IsFemale)
                .Where(h => h.Clan != null && h.Clan != Clan.PlayerClan && h.Clan.Kingdom != null)
                .Where(h => (int)h.Age >= GameCaps.MinAdultAge() &&
                            Math.Abs((int)h.Age - (int)kin.Age) <= 12)
                .ToList();

            var match = CSRandom.Pick(candidates);
            if (match == null)
            {
                context.Report.AddProblem($"Family: no realm lord was free to marry {kin.Name}");
                return;
            }

            if (TryMarry(kin, match, allowDirectBind: false))
            {
                // An in-law is not blood, so he stops short of a relative: four
                // steps of goodwill rather than the ceiling. Derived rather than
                // typed, and deliberately the same figure this carried as a
                // literal, since the defect was the hard-coded number and not the
                // amount, which is a design call rather than a fix.
                match.SetPersonalRelation(context.Hero, ConsequenceStep.RelationAmount(4));
                MarkKnown(context, kin);
                MarkKnown(context, match);
                var houseLeader = match.Clan?.Leader ?? kin.Clan?.Leader;
                if (houseLeader != null)
                    MarkKnown(context, houseLeader);
                CSLogger.Info($"FamilyStep: {kin.Name} married {match.Name} of {match.Clan?.Name}.");
            }
            else
            {
                context.Report.AddProblem($"Family: the marriage of {kin.Name} to {match.Name} was refused");
            }
        }

        /// <summary>
        ///     A hero of the requested gender and age, active, met, joined to the
        ///     player clan as a lord, and on good terms with the player. Lord
        ///     templates are tried first; the game ships no female lord templates,
        ///     so the wanderer roster fills that gap and the hero is promoted to
        ///     lord afterward.
        /// </summary>
        private static Hero? CreateRelative(StartContext context, bool isFemale, int age,
            HeroSpec? spec = null)
        {
            var hero = context.Hero;
            var culture = hero.Culture;

            var templates = culture?.LordTemplates?
                .Where(t => t.Occupation == Occupation.Lord && t.IsFemale == isFemale)
                .ToList();

            if (templates == null || templates.Count == 0)
            {
                templates = Kingdom.All
                    .Select(k => k.Culture)
                    .Where(c => c?.LordTemplates != null)
                    .Distinct()
                    .SelectMany(c => c!.LordTemplates)
                    .Where(t => t.Occupation == Occupation.Lord && t.IsFemale == isFemale)
                    .ToList();
            }

            bool fromWandererRoster = false;
            if (templates.Count == 0)
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

            var home = context.Session.SelectedSettlement
                       ?? context.Session.SelectedLocation
                       ?? SettlementFinder.RandomCultureTown(culture);

            try
            {
                var relative = HeroCreator.CreateSpecialHero(template, home, Clan.PlayerClan, null, age);
                if (fromWandererRoster)
                {
                    relative.SetNewOccupation(Occupation.Lord);
                    // The name was generated while the hero still counted as a
                    // wanderer, which carries epithets like "the Dispossessed";
                    // regenerating after the promotion gives a plain family name
                    NameGenerator.Current.GenerateHeroNameAndHeroFullName(relative,
                        out var firstName, out var fullName, false);
                    relative.SetName(fullName, firstName);
                }
                // The hero creation model IGNORES the requested age for wanderer
                // templates (it reuses the template's own age), so the birthday is
                // forced to make every relative exactly as old as designed
                relative.SetBirthDay(BirthDates.ForAge(age));
                relative.ChangeState(Hero.CharacterStates.Active);
                relative.SetPersonalRelation(hero, FamilyRelationValue);
                MarkKnown(context, relative);
                HeroOutfitter.Outfit(relative, context.Session, context.Settings, spec);
                return relative;
            }
            catch (Exception ex)
            {
                CSLogger.Error("FamilyStep: relative creation failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Every relative is somebody the player has met. One reading of what
        ///     that means, shared with the steps that build companions and
        ///     relations, because two readings would disagree.
        /// </summary>
        private static void MarkKnown(StartContext context, Hero hero) =>
            CompanionGenerator.MarkMet(hero, context.Session);
    }
}
