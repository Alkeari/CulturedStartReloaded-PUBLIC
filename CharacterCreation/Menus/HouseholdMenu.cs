using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The house you came from and the house you have now, asked as three
    ///     chapters in the order a person tells it: the two who raised you, the
    ///     ones raised beside you, then the house you made yourself.
    ///
    ///     Five fixed households could not express what a family is. Each row
    ///     pinned parents, siblings, spouse and children at once, so most real
    ///     shapes were unreachable: a father buried while the mother lives, two
    ///     sons and no daughters, a long table, or any crossing of the three.
    ///     Every option here answers ONE of those questions and leaves the other
    ///     two exactly as they were, so answers compose across the three screens.
    ///     Re-answering replaces within that question and touches nothing else,
    ///     which is what makes browsing safe on a stage where reading an option
    ///     means selecting it.
    ///
    ///     One screen held all three, which is nineteen rows in a route that asks
    ///     one question per screen. The three questions were already independent,
    ///     so they are three chapters and the state that composes them is held
    ///     here rather than per screen: the whole household is read back on every
    ///     one of them, and every question the player has not answered themselves
    ///     is answered by their life on every entry to any of them. That is also
    ///     what keeps a partly switched-off chapter coherent, since one chapter
    ///     still standing derives all three.
    ///
    ///     Nothing here asks for a figure. The option says what kind of house it
    ///     was and the SIZE of it comes from the life: the standing the scenes
    ///     gave this character decides how many were at that table, and the
    ///     game's own coming-of-age year decides how many children the years
    ///     could have held, which is also what removes the shapes those years
    ///     cannot hold rather than quietly handing out fewer.
    /// </summary>
    public static class HouseholdMenu
    {
        /// <summary>One of the three questions, and one chapter each.</summary>
        private enum Axis
        {
            /// <summary>Who raised you, and which of them is still living.</summary>
            Parents,

            /// <summary>Who was raised beside you.</summary>
            Kin,

            /// <summary>Who is yours now.</summary>
            Hearth
        }

        private static readonly Axis[] AllAxes = { Axis.Parents, Axis.Kin, Axis.Hearth };

        /// <summary>
        ///     The siblings chapter keeps the id the single chapter had, because
        ///     <see cref="CharacterPreviewHelper" /> stages the composed brothers
        ///     and sisters against that id and is the one place that decides who
        ///     stands beside the character.
        /// </summary>
        private const string ParentsMenuId = "cs_household_parents_menu";

        private const string KinMenuId = "cs_household_menu";
        private const string HearthMenuId = "cs_household_hearth_menu";

        /// <summary>One screen: its own question, and the axis its answers write.</summary>
        private sealed class Chapter
        {
            public Chapter(string id, Axis axis, string titleKey, string askKey)
            {
                Id = id;
                Axis = axis;
                TitleKey = titleKey;
                AskKey = askKey;
            }

            public string Id { get; }
            public Axis Axis { get; }
            public string TitleKey { get; }
            public string AskKey { get; }
        }

        private static readonly Chapter[] Chapters =
        {
            new(ParentsMenuId, Axis.Parents,
                "{=CSR_Household_ParentsTitle}The House You Came From",
                "{=CSR_House_AskParents}Somebody asks after the house you were small in, and whether the two who kept it are still in it."),

            new(KinMenuId, Axis.Kin,
                "{=CSR_Household_KinTitle}Raised Beside You",
                "{=CSR_House_AskKin}They ask who else was under that roof, and how many of you there were when the food went round."),

            new(HearthMenuId, Axis.Hearth,
                "{=CSR_Household_HearthTitle}The House You Made",
                "{=CSR_House_AskHearth}They ask whether anyone is waiting on word from you now, and whether any of them are yours.")
        };

        /// <summary>
        ///     What one answer makes of its own axis, and nothing about the other
        ///     two: the whole of what that answer writes, worked out once.
        ///
        ///     The answer used to carry the write itself, which left the panel with
        ///     no way to say what it was about to do except by working the same
        ///     numbers a second time. A house of five at one standing and four at
        ///     another is arithmetic, and a second copy of it is how a panel comes
        ///     to promise a brother the pipeline never builds.
        /// </summary>
        private readonly struct Shape
        {
            private Shape(bool father, bool mother, int brothers, int sisters,
                bool spouse, int sons, int daughters)
            {
                Father = father;
                Mother = mother;
                Brothers = brothers;
                Sisters = sisters;
                Spouse = spouse;
                Sons = sons;
                Daughters = daughters;
            }

            public bool Father { get; }
            public bool Mother { get; }
            public int Brothers { get; }
            public int Sisters { get; }
            public bool Spouse { get; }
            public int Sons { get; }
            public int Daughters { get; }

            public static Shape Parents(bool father, bool mother) =>
                new(father, mother, 0, 0, false, 0, 0);

            public static Shape Kin(int brothers, int sisters) =>
                new(false, false, brothers, sisters, false, 0, 0);

            /// <summary>
            ///     Children arrive with a living parent beside the player to be born
            ///     to, which the apply pipeline requires of them, so asking for
            ///     children is asking for a spouse and the fold is done here rather
            ///     than in the write, where the panel could not see it.
            /// </summary>
            public static Shape Hearth(bool spouse, int sons, int daughters) =>
                new(false, false, 0, 0, spouse || sons > 0 || daughters > 0, sons, daughters);

            /// <summary>All three questions at once, for reading the house back.</summary>
            public static Shape Whole(bool father, bool mother, int brothers, int sisters,
                bool spouse, int sons, int daughters) =>
                new(father, mother, brothers, sisters, spouse, sons, daughters);
        }

        /// <summary>
        ///     One answer. <see cref="Reverts" /> marks the answer that hands its
        ///     own question back to the life, which is the state every chapter
        ///     opens in and the only way back out of an answer once given.
        ///     <see cref="Offered" /> is null when the answer is always on the
        ///     table.
        /// </summary>
        private sealed class Answer
        {
            public Answer(string id, string titleKey, string proseKey, Axis axis,
                Func<CharacterCreationSession, Shape> made,
                Func<CharacterCreationSession, bool>? offered = null,
                bool reverts = false)
            {
                Id = id;
                TitleKey = titleKey;
                ProseKey = proseKey;
                Axis = axis;
                Made = made;
                Offered = offered;
                Reverts = reverts;
            }

            public string Id { get; }
            public string TitleKey { get; }
            public string ProseKey { get; }
            public Axis Axis { get; }
            public Func<CharacterCreationSession, Shape> Made { get; }
            public Func<CharacterCreationSession, bool>? Offered { get; }
            public bool Reverts { get; }
        }

        private static readonly Answer[] Answers =
        {
            // The ones who raised you
            new("cs_household_parents_life", "{=CSR_House_Life}What Your Life Left You",
                "{=CSR_House_Life_Prose}You say it the way it happened and leave the rest of it standing as it is.",
                Axis.Parents, LifeParents, reverts: true),
            new("cs_household_parents_both", "{=CSR_House_ParentsBoth}Both Parents Living",
                "{=CSR_House_ParentsBoth_Prose}They are old and they are still in the house you grew up in, and there is a door there that opens to you whatever you have done since.",
                Axis.Parents, _ => Shape.Parents(true, true)),
            new("cs_household_parents_mother", "{=CSR_House_ParentsMother}A Widowed Mother",
                "{=CSR_House_ParentsMother_Prose}Your father went first, and she has held what is left of that house on her own since, and held it better than he did.",
                Axis.Parents, _ => Shape.Parents(false, true)),
            new("cs_household_parents_father", "{=CSR_House_ParentsFather}A Widowed Father",
                "{=CSR_House_ParentsFather_Prose}He has been alone in that house longer now than he was not, and he has not moved a thing of hers since the day she died.",
                Axis.Parents, _ => Shape.Parents(true, false)),
            new("cs_household_parents_neither", "{=CSR_House_ParentsNeither}Two Graves",
                "{=CSR_House_ParentsNeither_Prose}You buried them both, and there is nobody left in that house to send word to.",
                Axis.Parents, _ => Shape.Parents(false, false)),

            // The ones raised beside you
            new("cs_household_kin_life", "{=CSR_House_Life}What Your Life Left You",
                "{=CSR_House_Life_Prose}You say it the way it happened and leave the rest of it standing as it is.",
                Axis.Kin, LifeKin, reverts: true),
            new("cs_household_kin_none", "{=CSR_House_KinNone}An Only Child",
                "{=CSR_House_KinNone_Prose}There was one child under that roof and it was you, and you have never once had to share anything you did not choose to.",
                Axis.Kin, _ => Shape.Kin(0, 0),
                s => NobodyNamed(s, FamilyRelation.Brother) && NobodyNamed(s, FamilyRelation.Sister)),
            new("cs_household_kin_brothers", "{=CSR_House_KinBrothers}No Sisters",
                "{=CSR_House_KinBrothers_Prose}It was a house of boys, and what happened in it was settled every week by whoever was loudest that week.",
                Axis.Kin, s => Shape.Kin(Brood(s), 0),
                s => NobodyNamed(s, FamilyRelation.Sister)),
            new("cs_household_kin_sisters", "{=CSR_House_KinSisters}No Brothers",
                "{=CSR_House_KinSisters_Prose}It was a house of girls, and you learned early that being right and being heard are two different errands.",
                Axis.Kin, s => Shape.Kin(0, Brood(s)),
                s => NobodyNamed(s, FamilyRelation.Brother)),
            new("cs_household_kin_one_sister", "{=CSR_House_KinOneSister}One Sister Among Them",
                "{=CSR_House_KinOneSister_Prose}She was outnumbered from the day she was born and she has never once behaved as though she were.",
                Axis.Kin, s => Shape.Kin(Math.Max(1, Brood(s) - 1), 1)),
            new("cs_household_kin_one_brother", "{=CSR_House_KinOneBrother}One Brother Among Them",
                "{=CSR_House_KinOneBrother_Prose}He was the only one of his kind under that roof, which made him careful or unbearable depending on the year.",
                Axis.Kin, s => Shape.Kin(1, Math.Max(1, Brood(s) - 1))),
            new("cs_household_kin_table", "{=CSR_House_KinTable}A Long Table",
                "{=CSR_House_KinTable_Prose}There were enough of you that meals went in shifts, and nobody under that roof was ever alone in a room.",
                Axis.Kin, LongTable),

            // The ones who are yours now
            new("cs_household_hearth_life", "{=CSR_House_Life}What Your Life Left You",
                "{=CSR_House_Life_Prose}You say it the way it happened and leave the rest of it standing as it is.",
                Axis.Hearth, LifeHearth, reverts: true),
            new("cs_household_hearth_none", "{=CSR_House_HearthNone}No House of Your Own",
                "{=CSR_House_HearthNone_Prose}You never married, and there is nobody waiting on word from you, so what you do next you do without asking anyone.",
                Axis.Hearth, _ => Shape.Hearth(false, 0, 0)),
            new("cs_household_hearth_spouse", "{=CSR_House_HearthSpouse}A Spouse, and No Children",
                "{=CSR_House_HearthSpouse_Prose}You married before the road took you, and it has been the two of you since, which is how you both wanted it.",
                Axis.Hearth, _ => Shape.Hearth(true, 0, 0)),
            new("cs_household_hearth_sons", "{=CSR_House_HearthSons}No Daughters",
                "{=CSR_House_HearthSons_Prose}You have boys and no girls, and every one of them has been told what the name he carries is for.",
                Axis.Hearth, s => Shape.Hearth(true, ChildBrood(s), 0)),
            new("cs_household_hearth_daughters", "{=CSR_House_HearthDaughters}No Sons",
                "{=CSR_House_HearthDaughters_Prose}You have girls and no boys, and anybody who has called that a pity in your hearing has called it that once.",
                Axis.Hearth, s => Shape.Hearth(true, 0, ChildBrood(s))),
            new("cs_household_hearth_pair", "{=CSR_House_HearthPair}A Son and a Daughter",
                "{=CSR_House_HearthPair_Prose}One of each, near enough in age that they were raised as a pair, and neither has ever conceded a thing to the other.",
                Axis.Hearth, _ => Shape.Hearth(true, 1, 1),
                s => ChildYears(s) >= 2),
            new("cs_household_hearth_one_daughter", "{=CSR_House_HearthOneDaughter}One Daughter Among Them",
                "{=CSR_House_HearthOneDaughter_Prose}She is the only girl at that table, and her brothers worked out early that being older than her settles nothing at all.",
                Axis.Hearth, s => Shape.Hearth(true, ChildBrood(s) - 1, 1),
                Outnumbered),
            new("cs_household_hearth_one_son", "{=CSR_House_HearthOneSon}One Son Among Them",
                "{=CSR_House_HearthOneSon_Prose}He is the only boy in a house of sisters, and what he is allowed to think about that was settled before he could walk.",
                Axis.Hearth, s => Shape.Hearth(true, 1, ChildBrood(s) - 1),
                Outnumbered),
            new("cs_household_hearth_houseful", "{=CSR_House_HearthHouseful}A Houseful",
                "{=CSR_House_HearthHouseful_Prose}There are more of them than you had planned on, and the house is louder than the road ever was.",
                Axis.Hearth, Houseful,
                s => ChildYears(s) >= 3)
        };

        /// <summary>
        ///     Which of the three questions the player has answered in their own
        ///     words. Held for the whole run rather than per chapter, so walking
        ///     back to an earlier screen re-derives nothing a later one settled.
        ///     The rest are answered by the life on every entry, so a scene
        ///     changed after these chapters still moves them.
        /// </summary>
        private static readonly Dictionary<Axis, Answer> Chosen = new();

        private static CharacterCreationSession? _lastSession;

        public static void AddHouseholdMenu(CharacterCreationManager manager)
        {
            foreach (var chapter in Chapters)
                AddChapter(manager, chapter);
        }

        private static void AddChapter(CharacterCreationManager manager, Chapter chapter)
        {
            var menu = new NarrativeMenu(
                chapter.Id,
                CreationFlow.DeclaredPrevious(chapter.Id),
                CreationFlow.DeclaredNext(chapter.Id),
                new TextObject(chapter.TitleKey),
                new TextObject(chapter.AskKey),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var answer in Answers.Where(a => a.Axis == chapter.Axis))
            {
                var captured = answer;
                var description = new TextObject(captured.ProseKey);

                ChoiceEffects.Declare(captured.Id, () => Effect(captured));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    captured.Id,
                    new TextObject(captured.TitleKey),
                    description,
                    args => { },
                    MenuText.Live(description,
                        // The description quotes nothing now, but this is still
                        // the only hook the API runs on every render, and the
                        // panel below is read against all three questions rather
                        // than this chapter's: whatever the player has not
                        // answered themselves, their life answers, so no screen
                        // is ever read against a household nobody composed and a
                        // chapter switched off still has an answer behind it
                        unused => EnsureDerived(CreationSession.Current),
                        () => IsOffered(captured)),
                    m => Select(captured),
                    m => { }
                ));
            }

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     Whether the years this life has behind it can hold that shape at
        ///     all. A start that came of age two winters ago cannot have a son
        ///     and a daughter, so the answer is not on the table rather than
        ///     being taken and quietly cut down to one child.
        /// </summary>
        private static bool IsOffered(Answer answer)
        {
            try
            {
                return answer.Offered?.Invoke(CreationSession.Current) ?? true;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"HouseholdMenu: deciding whether {answer.Id} is on the table failed.", ex);
                return true;
            }
        }

        /// <summary>
        ///     Answers one of the three questions and leaves the other two alone.
        ///     The house it composes is read back in the panel, which the stage
        ///     rebuilds on its own tick, so nothing here writes any text.
        /// </summary>
        private static void Select(Answer answer)
        {
            try
            {
                var session = CreationSession.Current;
                Sync(session);

                if (answer.Reverts) Chosen.Remove(answer.Axis);
                else Chosen[answer.Axis] = answer;

                Write(session, answer.Axis, answer.Made(session));
                CSLogger.Info($"HouseholdMenu: {answer.Id} -> {Census(session)}");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"HouseholdMenu: answering {answer.Id} failed.", ex);
            }
        }

        #region Composition

        /// <summary>Writes one answer's shape into the household, one axis at a time.</summary>
        private static void Write(CharacterCreationSession session, Axis axis, Shape shape)
        {
            switch (axis)
            {
                case Axis.Parents:
                    SetParents(session, shape.Father, shape.Mother);
                    break;

                case Axis.Kin:
                    SetKin(session, shape.Brothers, shape.Sisters);
                    break;

                default:
                    SetHearth(session, shape.Spouse, shape.Sons, shape.Daughters);
                    break;
            }
        }

        /// <summary>What the life says about one question, with the other two untouched.</summary>
        private static void Derive(CharacterCreationSession session, Axis axis) =>
            Write(session, axis, Life(axis)(session));

        /// <summary>
        ///     The answer the life gives to one question, which is the same function
        ///     the reverting answer on that chapter carries: one reading of the life
        ///     per axis, so a chapter switched off and a chapter answered "what your
        ///     life left you" compose the identical household.
        /// </summary>
        private static Func<CharacterCreationSession, Shape> Life(Axis axis) =>
            axis switch
            {
                Axis.Parents => LifeParents,
                Axis.Kin => LifeKin,
                _ => LifeHearth
            };

        private static Shape LifeParents(CharacterCreationSession session)
        {
            var house = DerivedHouse(session);
            return Shape.Parents(house.Father, house.Mother);
        }

        private static Shape LifeKin(CharacterCreationSession session)
        {
            var house = DerivedHouse(session);
            return Shape.Kin(house.Brothers, house.Sisters);
        }

        /// <summary>
        ///     What the scenes left in this character's own house. One answer in
        ///     the run says they married and none says a child came of it, so the
        ///     life fills the spouse and leaves the rest of this question standing
        ///     empty rather than inventing children the story never mentioned.
        /// </summary>
        private static Shape LifeHearth(CharacterCreationSession session) =>
            Shape.Hearth(DerivedHouse(session).Spouse, 0, 0);

        private static void EnsureDerived(CharacterCreationSession session)
        {
            try
            {
                Sync(session);

                foreach (var axis in AllAxes)
                {
                    // An answer the life has since walked off the table hands its
                    // question back rather than standing on a house the player
                    // could no longer choose: the scenes are told before these
                    // chapters and can be answered again behind them, so a life
                    // that names a brother after "An Only Child" was taken would
                    // otherwise keep a title its own family tree disagrees with
                    if (Chosen.TryGetValue(axis, out var answer) && !IsOffered(answer))
                    {
                        Chosen.Remove(axis);
                        CSLogger.Info($"HouseholdMenu: {answer.Id} is no longer on the table; " +
                                      "that question goes back to the life.");
                    }

                    if (!Chosen.ContainsKey(axis))
                        Derive(session, axis);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: deriving the unanswered part of the house failed.", ex);
            }
        }

        /// <summary>A new creation run has nothing answered in it.</summary>
        private static void Sync(CharacterCreationSession session)
        {
            if (ReferenceEquals(_lastSession, session)) return;

            _lastSession = session;
            Chosen.Clear();
        }

        /// <summary>
        ///     Writes only when the answer differs from what is already composed.
        ///     Siblings hold a rolled age offset, and rewriting the list on every
        ///     render would re-roll it, which the stage shows as brothers and
        ///     sisters changing age between one draw and the next.
        /// </summary>
        private static void SetParents(CharacterCreationSession session, bool father, bool mother)
        {
            var fatherSpec = Parent(session, FamilyRelation.Father);
            var motherSpec = Parent(session, FamilyRelation.Mother);
            if (fatherSpec.IsAlive == father && motherSpec.IsAlive == mother) return;

            fatherSpec.IsAlive = father;
            motherSpec.IsAlive = mother;
        }

        private static FamilyMemberSpec Parent(CharacterCreationSession session, FamilyRelation relation)
        {
            var spec = session.FamilyMembers.FirstOrDefault(m => m.Relation == relation);
            if (spec != null) return spec;

            spec = new FamilyMemberSpec(relation, false);
            session.FamilyMembers.Add(spec);
            return spec;
        }

        private static void SetKin(CharacterCreationSession session, int brothers, int sisters)
        {
            if (Count(session, FamilyRelation.Brother) == brothers &&
                Count(session, FamilyRelation.Sister) == sisters)
                return;

            session.FamilyMembers.RemoveAll(m =>
                m.Relation is FamilyRelation.Brother or FamilyRelation.Sister);
            Fill(session, FamilyRelation.Brother, brothers);
            Fill(session, FamilyRelation.Sister, sisters);
        }

        /// <summary>
        ///     A table longer than any other answer's, and never shorter than
        ///     three, because two children at it is not what the words say.
        /// </summary>
        private static Shape LongTable(CharacterCreationSession session)
        {
            int total = Math.Max(3, Brood(session) + 1);
            return Shape.Kin((total + 1) / 2, total / 2);
        }

        private static void SetHearth(CharacterCreationSession session, bool spouse,
            int sons, int daughters)
        {
            if ((Count(session, FamilyRelation.Spouse) > 0) == spouse &&
                Count(session, FamilyRelation.Son) == sons &&
                Count(session, FamilyRelation.Daughter) == daughters)
                return;

            session.FamilyMembers.RemoveAll(m => m.Relation is FamilyRelation.Spouse
                or FamilyRelation.Son or FamilyRelation.Daughter);
            if (!spouse) return;

            session.FamilyMembers.Add(new FamilyMemberSpec(FamilyRelation.Spouse, true));
            Fill(session, FamilyRelation.Son, sons);
            Fill(session, FamilyRelation.Daughter, daughters);
        }

        private static Shape Houseful(CharacterCreationSession session)
        {
            int total = Math.Max(3, Math.Min(ChildBrood(session) + 1, ChildYears(session)));
            return Shape.Hearth(true, (total + 1) / 2, total / 2);
        }

        private static void Fill(CharacterCreationSession session, FamilyRelation relation, int count)
        {
            for (int i = 0; i < count; i++)
                session.FamilyMembers.Add(new FamilyMemberSpec(relation, true));
        }

        private static int Count(CharacterCreationSession session, FamilyRelation relation) =>
            session.FamilyMembers.Count(m => m.Relation == relation);

        #endregion

        #region What the life already answered

        /// <summary>
        ///     The house the scenes settled, read off what the answers DECLARE
        ///     rather than off their ids.
        ///
        ///     Three tables of option ids used to live here, and an answer that
        ///     buried both parents or left the character married did the largest
        ///     thing in its scene with nothing beside it saying so: the panel is
        ///     composed from an option's own consequences, and the household was
        ///     the one effect no consequence could carry. The catalog declares it
        ///     now, so one declaration is printed to the player and composed here,
        ///     and neither can drift from the other.
        ///
        ///     The run is told in the order a life is lived, so a later scene
        ///     correcting an earlier one is a life told straight; the table this
        ///     replaced ranked the five answers instead, which no player could
        ///     have read off the screens they were given.
        ///
        ///     Each parent is settled only by an answer that speaks to THAT
        ///     parent. There was no such state once: every arm below wrote both
        ///     flags, so an answer burying a father silently stood the mother back
        ///     up, and a life that had already buried her ended with her living
        ///     and the scene that put her in the ground quietly untrue. Nothing on
        ///     screen disagreed, which is what made it worse than a visible
        ///     contradiction. Saying nothing about the other parent is now a state
        ///     the fold can be in, and it is the state every one-parent answer
        ///     leaves the other one in.
        ///
        ///     Siblings add up, because two answers naming two different people
        ///     name two people, and a marriage once made is not unmade by a later
        ///     scene that says nothing about it.
        /// </summary>
        private static (bool Father, bool Mother, int Brothers, int Sisters, bool Spouse) DerivedHouse(
            CharacterCreationSession session)
        {
            bool father = true;
            bool mother = true;
            bool spouse = false;
            int brothers = 0;
            int sisters = 0;

            var said = HouseSaid();

            foreach (string settled in said)
                switch (settled)
                {
                    case "both_parents_living":
                        father = true;
                        mother = true;
                        break;

                    case "both_parents_buried":
                        father = false;
                        mother = false;
                        break;

                    case "father_buried":
                        father = false;
                        break;

                    case "mother_buried":
                        mother = false;
                        break;

                    // The coin sent home, which says both are living only where
                    // this life has said nothing about them: a later answer
                    // overrules an earlier one, so declaring it flat stood a
                    // father the second scene had buried back on his feet
                    case "parents_still_standing":
                        if (StoryHome.Settled(said) == "both_parents_living")
                        {
                            father = true;
                            mother = true;
                        }

                        break;

                    // The last scene's answer for the parents, which is whichever
                    // of them this life has not already settled: it buries the one
                    // still above ground, buries both where nothing was ever said,
                    // and speaks to neither where the life says both are living
                    case "parents_by_then":
                        switch (StoryGraves.Settled(said))
                        {
                            case "both_parents_buried":
                                father = false;
                                mother = false;
                                break;

                            case "father_buried":
                                father = false;
                                break;

                            case "mother_buried":
                                mother = false;
                                break;
                        }

                        break;

                    case "a_brother":
                        brothers++;
                        break;

                    case "a_sister":
                        sisters++;
                        break;

                    // The answer names somebody under that roof and never which
                    // kind; the run settled that when the answer was given, so the
                    // house counts the very person the panel named beside it
                    case "a_sibling":
                        if (StorySibling.Of(session).Relation == FamilyRelation.Brother) brothers++;
                        else sisters++;
                        break;

                    case "a_spouse":
                        spouse = true;
                        break;
                }

            return (father, mother, brothers, sisters, spouse);
        }

        /// <summary>
        ///     Everything this life has said about the house so far, in the order
        ///     it said it. An answer that names a sibling without saying which kind
        ///     declares nothing here on purpose: the apply pipeline settles that
        ///     one, and guessing would put two people in the family where the story
        ///     claimed one.
        /// </summary>
        private static IReadOnlyList<string> HouseSaid()
        {
            var said = new List<string>();

            try
            {
                foreach (var consequence in SceneReading.Consequences(
                         Services.Application.GuidedRoute.Scenes, SceneMenus.Answered))
                    if (consequence.Kind == ConsequenceKind.Household && consequence.Target != null)
                        said.Add(consequence.Target);
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: reading what the life said about the house failed.", ex);
            }

            return said;
        }

        /// <summary>
        ///     Whether this life has said nothing about a brother, or nothing about
        ///     a sister.
        ///
        ///     The three answers that DENY one of them are on the table only for a
        ///     life that never named one. The family step builds every sibling the
        ///     scenes named whether or not the household the player composed has a
        ///     living seat for them, so "An Only Child" told over a life with a
        ///     brother in it leaves a brother in the family tree and a title that
        ///     denies him. The answer is withheld rather than quietly reinterpreted
        ///     into a house with somebody in it after all.
        /// </summary>
        private static bool NobodyNamed(CharacterCreationSession session, FamilyRelation relation)
        {
            try
            {
                var house = DerivedHouse(session);
                return (relation == FamilyRelation.Brother ? house.Brothers : house.Sisters) == 0;
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: reading the siblings the life named failed.", ex);
                return true;
            }
        }

        /// <summary>
        ///     How many were at that table. The standing the life reached decides
        ///     it: a house with a name in front of it kept more children than a
        ///     cart on a road did, and one different answer in the run moves the
        ///     number.
        /// </summary>
        private static int Brood(CharacterCreationSession session)
        {
            try
            {
                return 1 + LifeProfile.From(session).Band(LifeProfile.Lean.Standing);
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: reading the life's standing failed.", ex);
                return 1;
            }
        }

        /// <summary>
        ///     The years this character could have been a parent for: both
        ///     parents must have come of age before a child was born, which the
        ///     game's own age model decides. It is the only ceiling on how many
        ///     children an answer here can produce.
        /// </summary>
        private static int ChildYears(CharacterCreationSession session)
        {
            try
            {
                return FamilyAges.MaxChildAge(session);
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: reading how many years could hold children failed.", ex);
                return 1;
            }
        }

        /// <summary>The same house size, cut down to the years that could hold it.</summary>
        private static int ChildBrood(CharacterCreationSession session) =>
            Math.Max(1, Math.Min(Brood(session), ChildYears(session)));

        /// <summary>
        ///     Whether this life could hold a house with one of a kind in it.
        ///
        ///     Three is the floor because the answer is about being outnumbered:
        ///     at two the odd one out has a single sibling, which is the pair
        ///     answer worded differently, and at one there is nobody to be the odd
        ///     one out from. Below three the shape is off the table rather than
        ///     quietly cut down, the same way the pair and the houseful are, and
        ///     the majority in those two answers is therefore never less than two
        ///     and never needs a floor of its own.
        /// </summary>
        private static bool Outnumbered(CharacterCreationSession session) =>
            ChildBrood(session) >= 3;

        #endregion

        #region What this answer does

        /// <summary>
        ///     What one answer does, read off the same shape the write takes, so
        ///     the panel cannot promise a brother the pipeline never builds.
        ///
        ///     This answer's own question first, then the house the three
        ///     questions come to together. The composed house used to be appended
        ///     to the option's prose, which is the defect this pass exists to
        ///     end: how many people stand in a clan and how many seats they take
        ///     out of the column are figures, and figures belong here.
        ///
        ///     The house is composed WITH this answer taken rather than as it
        ///     stands. Reading it off the session would print the house this
        ///     answer is about to change, so an option offering a living father
        ///     would state a living father and then, a line below, a house with
        ///     both parents buried. Every entry on this panel is what the player
        ///     gets by taking the option, which is the only reading of it that is
        ///     still true a click later.
        /// </summary>
        private static string Effect(Answer answer)
        {
            var session = CreationSession.Current;
            var shape = answer.Made(session);

            return ChoiceEffects.Stated(
                answer.Reverts
                    ? Text("{=CSR_Panel_House_Life}Why: this hands the question back to the life you have told")
                    : null,
                Settled(answer.Axis, shape),
                Riding(answer.Axis, shape),
                Readback(Whole(session, answer.Axis, shape), RidersIn(answer.Axis, shape)));
        }

        /// <summary>
        ///     The whole household as this answer would leave it: the two
        ///     questions it does not speak to as they stand, and its own as it
        ///     settles it. The axes are disjoint, which is what lets the three
        ///     chapters be three chapters, so the fold is a field at a time.
        /// </summary>
        private static Shape Whole(CharacterCreationSession session, Axis axis, Shape shape)
        {
            bool father = axis == Axis.Parents
                ? shape.Father
                : Parent(session, FamilyRelation.Father).IsAlive;
            bool mother = axis == Axis.Parents
                ? shape.Mother
                : Parent(session, FamilyRelation.Mother).IsAlive;
            int brothers = axis == Axis.Kin ? shape.Brothers : Count(session, FamilyRelation.Brother);
            int sisters = axis == Axis.Kin ? shape.Sisters : Count(session, FamilyRelation.Sister);
            bool spouse = axis == Axis.Hearth
                ? shape.Spouse
                : Count(session, FamilyRelation.Spouse) > 0;
            int sons = axis == Axis.Hearth ? shape.Sons : Count(session, FamilyRelation.Son);
            int daughters = axis == Axis.Hearth ? shape.Daughters : Count(session, FamilyRelation.Daughter);

            return Shape.Whole(father, mother, brothers, sisters, spouse, sons, daughters);
        }

        /// <summary>Who this answer puts in the family, by relation and by count.</summary>
        private static string Settled(Axis axis, Shape shape)
        {
            switch (axis)
            {
                case Axis.Parents:
                    if (shape.Father && shape.Mother)
                        return Text(
                            "{=CSR_Panel_House_ParentsBoth}Family: your father and your mother both alive, and both in your clan");
                    if (shape.Mother)
                        return Text(
                            "{=CSR_Panel_House_ParentsMother}Family: your mother alive and in your clan, your father dead and in your family tree");
                    if (shape.Father)
                        return Text(
                            "{=CSR_Panel_House_ParentsFather}Family: your father alive and in your clan, your mother dead and in your family tree");

                    return Text(
                        "{=CSR_Panel_House_ParentsNeither}Family: both your parents dead, and both in your family tree");

                case Axis.Kin:
                    int siblings = shape.Brothers + shape.Sisters;
                    if (siblings == 0)
                        return Text("{=CSR_Panel_House_KinNone}Family: no brother and no sister");

                    var kin = new TextObject(
                        "{=CSR_Panel_House_Kin}Family: {KIN}, grown and in your clan");
                    kin.SetTextVariable("KIN", Both(
                        shape.Brothers == 0
                            ? null
                            : MenuText.Count(shape.Brothers,
                                "{=CSR_House_BrotherOne}{COUNT} brother",
                                "{=CSR_House_BrotherMany}{COUNT} brothers"),
                        shape.Sisters == 0
                            ? null
                            : MenuText.Count(shape.Sisters,
                                "{=CSR_House_SisterOne}{COUNT} sister",
                                "{=CSR_House_SisterMany}{COUNT} sisters")));
                    return kin.ToString();

                default:
                    if (!shape.Spouse)
                        return Text("{=CSR_Panel_House_HearthNone}Family: unmarried, and no children");

                    int children = shape.Sons + shape.Daughters;
                    if (children == 0)
                        return Text(
                            "{=CSR_Panel_House_HearthSpouse}Family: a spouse, married to you and in your clan");

                    var hearth = new TextObject(
                        "{=CSR_Panel_House_Hearth}Family: a spouse, and {CHILDREN} born to the two of you");
                    hearth.SetTextVariable("CHILDREN", Both(
                        shape.Sons == 0
                            ? null
                            : MenuText.Count(shape.Sons, "{=CSR_House_SonOne}{COUNT} son",
                                "{=CSR_House_SonMany}{COUNT} sons"),
                        shape.Daughters == 0
                            ? null
                            : MenuText.Count(shape.Daughters, "{=CSR_House_DaughterOne}{COUNT} daughter",
                                "{=CSR_House_DaughterMany}{COUNT} daughters")));
                    return hearth.ToString();
            }
        }

        /// <summary>
        ///     What the seats this answer fills cost, in the figure the warband
        ///     chapter subtracts from the soldiers it may offer, so the price of a
        ///     crowded house is stated where the house is chosen. Children are
        ///     never in it: the game's own coming of age is years ahead of them,
        ///     and that is its own entry rather than a silence.
        /// </summary>
        private static string? Riding(Axis axis, Shape shape)
        {
            int riders = RidersIn(axis, shape);

            if (axis != Axis.Hearth) return riders == 0 ? null : Seats(riders);

            return ChoiceEffects.Stated(
                riders == 0 ? null : Seats(riders),
                shape.Sons + shape.Daughters > 0
                    ? Text(
                        "{=CSR_Panel_House_ChildrenStay}Family: your children are years short of riding out, and stay at home")
                    : null);
        }

        /// <summary>
        ///     Who one question puts in the column. Children are never in it: the
        ///     game's own coming of age is years ahead of them. Asked once and used
        ///     twice, by this answer's own seat cost and by the whole house's, so
        ///     the two figures on one panel are one rule rather than two.
        /// </summary>
        private static int RidersIn(Axis axis, Shape shape) =>
            axis switch
            {
                Axis.Parents => (shape.Father ? 1 : 0) + (shape.Mother ? 1 : 0),
                Axis.Kin => shape.Brothers + shape.Sisters,
                _ => shape.Spouse ? 1 : 0
            };

        /// <summary>
        ///     Seats out of the column, said once for every relation that takes
        ///     one. Which of them is riding is the entry above; what it costs is
        ///     the same arithmetic whoever they are.
        /// </summary>
        private static string Seats(int riders) => MenuText.Count(riders,
            "{=CSR_Panel_House_SeatOne}Troops: 1 fewer your party can carry, for the one of them riding with you",
            "{=CSR_Panel_House_SeatMany}Troops: {COUNT} fewer your party can carry, for the {COUNT} of them riding with you");

        #endregion

        #region What the house now is

        /// <summary>
        ///     The whole household, so any answer read on any of the three screens
        ///     states the composed house and not only that screen's third of it.
        ///     Every number here is the number the pipeline will build, and the
        ///     riders are the same count the warband chapter takes off the column.
        /// </summary>
        private static string Readback(Shape house, int ownRiders)
        {
            try
            {
                var line = new TextObject(
                    "{=CSR_House_Now}Family: the house in all is {PARENTS}, {KIN}, {HEARTH}");
                line.SetTextVariable("PARENTS", Parents(house));
                line.SetTextVariable("KIN", Kin(house));
                line.SetTextVariable("HEARTH", Hearth(house));
                return ChoiceEffects.Stated(line.ToString(), Riders(house, ownRiders));
            }
            catch (Exception ex)
            {
                CSLogger.Error("HouseholdMenu: reading the composed house back failed.", ex);
                return string.Empty;
            }
        }

        private static string Parents(Shape house)
        {
            bool father = house.Father;
            bool mother = house.Mother;

            if (father && mother)
                return Text("{=CSR_House_NowParentsBoth}your mother and father both living");
            if (mother)
                return Text("{=CSR_House_NowParentsMother}your mother living and your father in the ground");
            if (father)
                return Text("{=CSR_House_NowParentsFather}your father living and your mother in the ground");

            return Text("{=CSR_House_NowParentsNeither}both your parents in the ground");
        }

        private static string Kin(Shape house)
        {
            int brothers = house.Brothers;
            int sisters = house.Sisters;
            if (brothers == 0 && sisters == 0)
                return Text("{=CSR_House_NowKinNone}no brother or sister");

            var line = new TextObject("{=CSR_House_NowKin}{KIN} raised beside you");
            line.SetTextVariable("KIN", Both(
                brothers == 0
                    ? null
                    : MenuText.Count(brothers, "{=CSR_House_BrotherOne}{COUNT} brother",
                        "{=CSR_House_BrotherMany}{COUNT} brothers"),
                sisters == 0
                    ? null
                    : MenuText.Count(sisters, "{=CSR_House_SisterOne}{COUNT} sister",
                        "{=CSR_House_SisterMany}{COUNT} sisters")));
            return line.ToString();
        }

        private static string Hearth(Shape house)
        {
            int sons = house.Sons;
            int daughters = house.Daughters;

            if (!house.Spouse)
                return Text("{=CSR_House_NowHearthNone}and no house of your own yet");
            if (sons == 0 && daughters == 0)
                return Text("{=CSR_House_NowHearthSpouse}and a spouse, with no children yet");

            var line = new TextObject("{=CSR_House_NowHearth}and a spouse, with {CHILDREN}");
            line.SetTextVariable("CHILDREN", Both(
                sons == 0
                    ? null
                    : MenuText.Count(sons, "{=CSR_House_SonOne}{COUNT} son",
                        "{=CSR_House_SonMany}{COUNT} sons"),
                daughters == 0
                    ? null
                    : MenuText.Count(daughters, "{=CSR_House_DaughterOne}{COUNT} daughter",
                        "{=CSR_House_DaughterMany}{COUNT} daughters")));
            return line.ToString();
        }

        /// <summary>
        ///     How many of the whole house take a seat in the column, which is
        ///     what the warband chapter subtracts from the soldiers it may offer.
        ///     Summed from the same <see cref="RidersIn"/> the answer's own seat
        ///     cost is read from, so the two figures on one panel cannot disagree.
        ///
        ///     Said only where the rest of the house adds to it. On a house whose
        ///     other two questions put nobody in the column this is the figure the
        ///     entry above already carries, and one panel stating one number twice
        ///     reads as two costs.
        /// </summary>
        private static string? Riders(Shape house, int ownRiders)
        {
            int riders = RidersIn(Axis.Parents, house) + RidersIn(Axis.Kin, house) +
                         RidersIn(Axis.Hearth, house);
            if (riders == ownRiders) return null;
            if (riders == 0) return Text("{=CSR_House_NowRidersNone}Troops: none of the house rides with you");

            return MenuText.Count(riders,
                "{=CSR_House_NowRidersOne}Troops: 1 fewer in all, once the rest of the house is counted",
                "{=CSR_House_NowRidersMany}Troops: {COUNT} fewer in all, once the rest of the house is counted");
        }

        private static string Both(string? first, string? second)
        {
            if (first == null) return second ?? string.Empty;
            if (second == null) return first;

            var line = new TextObject("{=CSR_House_Both}{FIRST} and {SECOND}");
            line.SetTextVariable("FIRST", first);
            line.SetTextVariable("SECOND", second);
            return line.ToString();
        }

        private static string Text(string key) => new TextObject(key).ToString();

        /// <summary>The composed house as one line for the log.</summary>
        private static string Census(CharacterCreationSession session) =>
            $"father {(Parent(session, FamilyRelation.Father).IsAlive ? "alive" : "dead")}, " +
            $"mother {(Parent(session, FamilyRelation.Mother).IsAlive ? "alive" : "dead")}, " +
            $"{Count(session, FamilyRelation.Brother)} brothers, {Count(session, FamilyRelation.Sister)} sisters, " +
            $"{Count(session, FamilyRelation.Spouse)} spouse, " +
            $"{Count(session, FamilyRelation.Son)} sons, {Count(session, FamilyRelation.Daughter)} daughters";

        #endregion

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
