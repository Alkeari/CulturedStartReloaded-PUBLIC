using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The one place family ages are reasoned about: sensible defaults per
    ///     relation, and the logical bounds for setting them. Children are capped
    ///     by what the player and spouse could feasibly have parented, using the
    ///     game's own coming-of-age year as the fertility floor so age mods are
    ///     honored.
    /// </summary>
    public static class FamilyAges
    {
        public static int MemberAge(CharacterCreationSession session, FamilyMemberSpec spec)
        {
            if (spec.Relation is FamilyRelation.Brother or FamilyRelation.Sister)
                return SiblingAge(session, spec);
            if (spec.Age.HasValue) return spec.Age.Value;

            int playerAge = session.EffectiveAge;
            return spec.Relation switch
            {
                FamilyRelation.Spouse => Math.Max(GameCaps.MinAdultAge(), playerAge - 2),
                FamilyRelation.Son or FamilyRelation.Daughter => ChildAge(session, spec),
                FamilyRelation.Father => ParentAge(session, true),
                FamilyRelation.Mother => ParentAge(session, false),
                _ => playerAge
            };
        }

        /// <summary>
        ///     How old an automatic child is: anywhere between one and the oldest this couple
        ///     could have parented, rather than that oldest year every time. A young couple can
        ///     only have babies, so the band is narrow and taking its top always read as the
        ///     answer being fixed rather than chosen.
        ///
        ///     The roll belongs to the RUN and to the child's place among their siblings, so the
        ///     age is the same wherever it is read: the household chapter, the editor's row, the
        ///     panel beside the character and the apply pipeline all ask separately.
        /// </summary>
        private static int ChildAge(CharacterCreationSession session, FamilyMemberSpec spec)
        {
            int oldest = MaxChildAge(session);
            if (oldest <= 1) return 1;

            int ordinal = 0;
            foreach (var member in session.FamilyMembers)
            {
                if (member == spec) break;
                if (member.Relation is FamilyRelation.Son or FamilyRelation.Daughter) ordinal++;
            }

            return 1 + CSRandom.Stable(session.RunSeed, $"child-age-{ordinal}", oldest);
        }

        private static int SiblingAge(CharacterCreationSession session, FamilyMemberSpec spec)
        {
            int playerAge = session.EffectiveAge;
            switch (spec.Aging)
            {
                case SiblingAging.Twin:
                    return playerAge;
                case SiblingAging.Exact:
                    return spec.Age ?? Math.Max(GameCaps.MinAdultAge(), playerAge - 4);
                default:
                    if (spec.SmartOffset == null)
                        RollSmartSiblingAges(session);
                    var bounds = AgeBounds(session, spec);
                    return Math.Max(bounds.Min,
                        Math.Min(bounds.Max, playerAge + (spec.SmartOffset ?? -4)));
            }
        }

        /// <summary>
        ///     Every smart-aged sibling gets a distinct offset from the player's
        ///     age so the family reads as real births years apart, with a rare
        ///     twin among larger families.
        ///
        ///     The distances belong to the RUN rather than to the moment they are
        ///     asked for (<see cref="SiblingOffsets" />), so the same brother or
        ///     sister keeps the same remove from the player wherever they are
        ///     read: a scene that stages one before this list holds anybody, a
        ///     household chapter that rebuilds the list whenever a count changes,
        ///     and the apply pipeline at the end.
        /// </summary>
        private static void RollSmartSiblingAges(CharacterCreationSession session)
        {
            int playerAge = session.EffectiveAge;
            var smartSiblings = session.FamilyMembers
                .Where(m => m.Relation is FamilyRelation.Brother or FamilyRelation.Sister &&
                            m.Aging == SiblingAging.Smart)
                .ToList();
            if (smartSiblings.Count == 0) return;

            var takenOffsets = session.FamilyMembers
                .Where(m => m.Relation is FamilyRelation.Brother or FamilyRelation.Sister &&
                            m.Aging != SiblingAging.Smart)
                .Select(m => m.Aging == SiblingAging.Twin ? 0 : (m.Age ?? playerAge - 4) - playerAge)
                .ToHashSet();

            for (int ordinal = 0; ordinal < smartSiblings.Count; ordinal++)
            {
                var sibling = smartSiblings[ordinal];
                int offset = sibling.SmartOffset
                             ?? SiblingOffsets.For(session.RunSeed, ordinal, takenOffsets);

                sibling.SmartOffset = offset;
                takenOffsets.Add(offset);
            }
        }

        public static (int Min, int Max) AgeBounds(CharacterCreationSession session, FamilyMemberSpec spec)
        {
            return spec.Relation switch
            {
                FamilyRelation.Spouse => (GameCaps.MinAdultAge(), GameCaps.MaxAge()),
                FamilyRelation.Brother or FamilyRelation.Sister => (6, GameCaps.MaxAge()),
                FamilyRelation.Son or FamilyRelation.Daughter => (1, MaxChildAge(session)),
                _ => (GameCaps.MinAdultAge(), GameCaps.MaxAge())
            };
        }

        /// <summary>
        ///     The oldest a child can be: both parents must have come of age by
        ///     the child's birth. A 40-year-old player with a 20-year-old spouse
        ///     can have children of at most 2 when adulthood starts at 18.
        /// </summary>
        public static int MaxChildAge(CharacterCreationSession session)
        {
            int comesOfAge = GameCaps.MinAdultAge();
            int playerAge = session.EffectiveAge;

            var primarySpouse = session.FamilyMembers.FirstOrDefault(m => m.Relation == FamilyRelation.Spouse);
            int spouseAge = primarySpouse?.Age ?? Math.Max(comesOfAge, playerAge - 2);

            int max = Math.Min(playerAge, spouseAge) - comesOfAge;
            return Math.Max(1, max);
        }

        /// <summary>
        ///     How many family members will ride in the player's party: alive,
        ///     adult, not married into another clan, and not kept at a holding.
        /// </summary>
        public static int InPartyCount(CharacterCreationSession session)
        {
            int count = 0;
            foreach (var member in session.FamilyMembers)
            {
                if (!member.IsAlive || member.StaysInSettlement ||
                    member.Marriage == FamilyMarriage.MarriedAway ||
                    MemberAge(session, member) < GameCaps.MinAdultAge())
                    continue;

                count++;
                // A married-in partner rides beside their spouse
                if (member.Marriage == FamilyMarriage.MarriedInClan)
                    count++;
            }

            return count;
        }

        /// <summary>
        ///     Parents must have come of age before their oldest child was born,
        ///     the player and every sibling included.
        /// </summary>
        public static int ParentAge(CharacterCreationSession session, bool isFather)
        {
            int comesOfAge = GameCaps.MinAdultAge();
            int oldestChild = session.EffectiveAge;
            foreach (var member in session.FamilyMembers)
                if (member.Relation is FamilyRelation.Brother or FamilyRelation.Sister)
                    oldestChild = Math.Max(oldestChild, MemberAge(session, member));

            return oldestChild + comesOfAge + (isFather ? 6 : 4);
        }

        /// <summary>
        ///     A brother or sister of one of the player's parents: near that
        ///     parent's own age, and never so old that their shared father would
        ///     have been a child when they were born.
        /// </summary>
        public static int ParentSiblingAge(CharacterCreationSession session, bool onFathersSide)
        {
            int comesOfAge = GameCaps.MinAdultAge();
            int parentAge = ParentAge(session, onFathersSide);
            int forebearAge = ForebearAge(session, onFathersSide);

            int spread = parentAge - 8 + CSRandom.Next(17);
            int oldest = Math.Max(comesOfAge, forebearAge - comesOfAge);
            return Math.Max(comesOfAge, Math.Min(Math.Min(oldest, GameCaps.MaxAge() - 1), spread));
        }

        /// <summary>
        ///     Whoever held the house before the player's parents did. Two
        ///     generations up is always behind the character, so this is only ever
        ///     asked for someone the story has already buried.
        ///
        ///     Rolled from nothing, because the aunt's own bound is read off this
        ///     and a second call that answered differently would put her older
        ///     than the father she is supposed to share.
        /// </summary>
        public static int ForebearAge(CharacterCreationSession session, bool onFathersSide)
        {
            int comesOfAge = GameCaps.MinAdultAge();
            int parentAge = ParentAge(session, onFathersSide);
            return Math.Min(GameCaps.MaxAge(), parentAge + comesOfAge + (onFathersSide ? 6 : 4));
        }
    }
}
