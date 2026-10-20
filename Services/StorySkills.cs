using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What the life the player wrote is actually worth, in skill.
    ///
    ///     Nothing here is a chosen number, and nothing here is a number invented
    ///     out of the development model's arithmetic either. The world is already
    ///     full of people the game itself wrote, and how capable a character is
    ///     only means anything measured against them, so this reads the living
    ///     population: how high their best skill runs, how steeply a sheet falls
    ///     away from it, and which skills a person in this world normally carries.
    ///     The life decides how far up toward the strongest person alive this
    ///     character stands, and which skills sit at the top of their own sheet.
    ///
    ///     The range used to START at the weakest adult alive rather than at
    ///     nothing, and that constant was what made the sheet unreadable while it
    ///     was being written. A life with one answer in it is 7 percent of a life,
    ///     but 7 percent of the way from a complete adult to the strongest person
    ///     in the world is still a complete adult: the first answer of thirteen
    ///     handed the character 22 of the 29 focus points the finished life ever
    ///     carries, and the twelve answers after it moved 7 between them. The
    ///     range now runs from nothing, so how much of a life has been told is
    ///     what decides the sheet at every point of the telling rather than only
    ///     at the end of it. A life told in full still comes out far above the
    ///     weakest adult; it is no longer handed that standing for answering once.
    ///     A character nobody has written a life for is a separate question with a
    ///     separate answer, and that answer is still the ordinary adult the Start
    ///     Editor opens on.
    ///
    ///     The road used to run the other way: a level was derived first and the
    ///     sheet was sized to cost it. Both halves of that were wrong. The level
    ///     came from an invented reading of the focus grant, which put a written
    ///     life as high as the mid thirties, above every lord the game ships; and
    ///     the sheet that cost that level came out as sixteen skills at the same
    ///     value, which no person in the game looks like. The level is now the
    ///     last thing computed rather than the first: the sheet is drawn against
    ///     the people, and the game's own pricing says what level it is.
    /// </summary>
    public static class StorySkills
    {
        // HeroDeveloper.SetInitialLevelFromSkills prices a sheet as
        // sum(2 * value^2.2) - 2000 and hands that to TotalXp, which CheckLevel then
        // walks up against SkillsRequiredForLevel. Those three live in a private
        // method body rather than on the development model, so there is nothing to
        // ask at runtime and they are transcribed. Everything else in this file is
        // read from a model, from the living world, or from the life.
        private const double XpPerSkill = 2.0;
        private const double XpCurve = 2.2;
        private const int SheetOffset = 2000;

        /// <summary>
        ///     The fullest life each route's own written material can produce, kept
        ///     per route. One cached number would measure a life told on one route
        ///     against the ceiling of the other, which is the difference between a
        ///     full life and a third of one.
        /// </summary>
        private static readonly Dictionary<object, int> RichestLives = new Dictionary<object, int>();
        private static People? _people;
        private static object? _peopleOf;

        /// <summary>
        ///     Forgets the world's reading, for a creation run that starts over, so
        ///     a second character written in the same launch is measured against
        ///     its own world rather than against the one the first was.
        /// </summary>
        internal static void ForgetTheWorld()
        {
            _people = null;
            _peopleOf = null;
        }

        /// <summary>
        ///     Every skill's starting level, for the route this run is on: what the
        ///     life is worth where one is being told, and an ordinary adult where
        ///     none is.
        ///
        ///     The life says how far along the world's own range this character
        ///     stands; the people say what a sheet at that height looks like; the
        ///     life says again which skills are at the top of it.
        ///
        ///     A skill the life never went near is not zero, it is the tail of the
        ///     shape, which is the few points a lord carries in a trade they never
        ///     learned.
        ///
        ///     With War Sails loaded that tail is where the three naval skills
        ///     land, and it is zero: the DLC adds nine named heroes who carry a
        ///     naval skill against roughly four hundred who do not, so the world's
        ///     average standing in them is a rounding error, and no scene in the
        ///     guided route puts a character on the water to argue otherwise. A
        ///     full guided run against the v1.5.2 population comes out at Mariner,
        ///     Boatswain and Shipmaster 0 against a best skill around 220. That is
        ///     the honest reading of a life that never went to sea, but it is not a
        ///     reading the DLC's owner should be stuck with: the route has to be
        ///     able to send them there before this can say anything else.
        /// </summary>
        public static Dictionary<SkillObject, int> LevelsFor(CharacterCreationSession session) =>
            Application.GuidedRun.WasWalkedBy(session)
                ? LevelsFor(LifeProfile.From(session))
                : OrdinaryAdult();

        /// <summary>
        ///     The sheet of a life that is being told. Taken off a profile rather
        ///     than a session because the panel beside the character needs the
        ///     sheet of the life MINUS its newest answer as well as the sheet of
        ///     the life itself, so that what a choice did can be shown apart from
        ///     what the life already held, and only one of those two is the
        ///     session's own.
        /// </summary>
        public static Dictionary<SkillObject, int> LevelsFor(LifeProfile profile) =>
            LevelsAtAge(profile, CreationSession.Current?.AdjustedAge);

        /// <summary>
        ///     The same sheet at a given age, or at the age the life came to where
        ///     none is given. Revamped's age chapter moves how much of a life the
        ///     character carries through <see cref="AgeCurve" />, as one factor on
        ///     the best skill, so which skills the life chose and how the sheet
        ///     falls away from its best are untouched and the level, focus and
        ///     attributes follow the sheet through <see cref="PriceOf" />.
        /// </summary>
        internal static Dictionary<SkillObject, int> LevelsAtAge(LifeProfile profile, int? age)
        {
            var people = Everyone();
            if (people == null) return new Dictionary<SkillObject, int>();

            return Draw(people, WeightsFor(profile), AgedShare(people, ShareOfALife(profile), age) * people.StrongestBest);
        }

        /// <summary>The level the sheet at that age is priced at.</summary>
        internal static int LevelAtAge(LifeProfile profile, int? age) => PriceOf(LevelsAtAge(profile, age).Values);

        private static double AgedShare(People people, double share, int? age)
        {
            var session = CreationSession.Current;
            if (age == null || session == null || session.Mode != SetupMode.Narrative) return share;

            int floor = GameCaps.MinAdultAge();
            int derived = Math.Max(floor, Math.Min(AgeCurve.Ceiling, (int)session.SelectedAge));
            return AgeCurve.Share(share, derived, age.Value, people.BestAt, floor, AgeCurve.Ceiling);
        }

        /// <summary>
        ///     The sheet of a character nobody has written a life for, which is the
        ///     weakest adult alive: an ordinary person, in the shape an ordinary
        ///     person's sheet has.
        ///
        ///     This is what the Start Editor hands the player to change, and what
        ///     every reader that asks how capable a character will be gets for a
        ///     route with no story behind it. Nothing has been told, so nothing
        ///     argues for one trade over another and the ranking falls to what this
        ///     world's people carry highest.
        /// </summary>
        private static Dictionary<SkillObject, int> OrdinaryAdult()
        {
            var people = Everyone();
            if (people == null) return new Dictionary<SkillObject, int>();

            return Draw(people, new Dictionary<SkillObject, double>(), people.WeakestBest);
        }

        /// <summary>
        ///     One sheet: the skills ranked by what argues for them, each taking
        ///     its rank's share of the best skill on it.
        /// </summary>
        private static Dictionary<SkillObject, int> Draw(People people,
            Dictionary<SkillObject, double> weights, double best)
        {
            var levels = new Dictionary<SkillObject, int>();
            int cap = GameCaps.MaxSkillLevel();

            var ranked = Ranked(weights, people);
            for (int i = 0; i < ranked.Count && i < people.Shape.Length; i++)
                levels[ranked[i]] = Math.Max(0, Math.Min(cap, (int)Math.Round(best * people.Shape[i])));

            return levels;
        }

        /// <summary>
        ///     The character level the life adds up to, which is the level the game
        ///     itself would price the finished sheet at. Asked of the sheet rather
        ///     than decided ahead of it, so the two can never disagree.
        /// </summary>
        public static int LevelFor(CharacterCreationSession session) =>
            PriceOf(LevelsFor(session).Values);

        /// <summary>The same, for a life read off a profile rather than a session.</summary>
        public static int LevelFor(LifeProfile profile) => PriceOf(LevelsFor(profile).Values);

        /// <summary>One skill's starting level, for the previews that ask about one.</summary>
        public static int LevelFor(CharacterCreationSession session, SkillObject skill)
        {
            return LevelsFor(session).TryGetValue(skill, out int level) ? level : 0;
        }

        /// <summary>
        ///     The level the game's own arithmetic puts on a sheet: what the sheet
        ///     costs in the experience the game counts, walked up the installed
        ///     model's own requirement table. This is the one place the round trip
        ///     is closed, and every caller that wants a level goes through it.
        /// </summary>
        internal static int PriceOf(IEnumerable<int> sheet)
        {
            double carried = 0;
            foreach (int value in sheet) carried += XpOf(value);

            int worth = Math.Max(1, (int)carried - SheetOffset);

            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model == null)
                {
                    CSLogger.Warn("StorySkills: no development model to price the sheet; it reads as level 1.");
                    return 1;
                }

                int ceiling = Math.Max(1, GameCaps.MaxHeroLevel());
                int level = 0;
                while (level < ceiling && worth >= model.SkillsRequiredForLevel(level + 1))
                    level++;

                return Math.Max(1, level);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StorySkills: the development model would not price the sheet ({ex.GetType().Name}).");
                return 1;
            }
        }

        /// <summary>
        ///     The top of a sheet the game's own arithmetic would price at that
        ///     level, given the shape the rest of the sheet takes under it.
        ///
        ///     The inverse of <see cref="PriceOf"/>, and it exists because one
        ///     reader works the other way round: a generated companion is asked for
        ///     a LEVEL and the sheet under it has to come out of the same pricing
        ///     the level was read from, or the two disagree. That reader used to
        ///     hand its role skill 30 + level*4 + a roll, three figures nobody can
        ///     point at, which put a level-one novice in the mid forties.
        ///
        ///     <paramref name="shares" /> is the sheet's shape, each skill as a
        ///     fraction of the top one. The price of a sheet is a sum of the same
        ///     power of each skill, so the whole of it costs the top skill's own
        ///     price times the sum of those shares raised to that power, and the top
        ///     falls out in one step.
        /// </summary>
        internal static int TopOfSheet(int level, IEnumerable<double> shares)
        {
            double weight = 0;
            foreach (double share in shares) weight += Math.Pow(Math.Max(0.0, share), XpCurve);
            if (weight <= 0) return 0;

            double carried = WorthOfLevel(level) + SheetOffset;
            double top = Math.Pow(carried / (XpPerSkill * weight), 1.0 / XpCurve);

            return Math.Max(0, Math.Min(GameCaps.MaxSkillLevel(), (int)Math.Round(top)));
        }

        /// <summary>
        ///     How a sheet in this world falls away from its own best skill, rank by
        ///     rank. Empty where the world holds nobody to read, which every caller
        ///     treats as having no answer rather than as a shape of nothing.
        /// </summary>
        internal static IReadOnlyList<double> SheetShape() => Everyone()?.Shape ?? Array.Empty<double>();

        /// <summary>What the running game's own model says a hero of that level has paid.</summary>
        private static int WorthOfLevel(int level)
        {
            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model != null) return Math.Max(0, model.SkillsRequiredForLevel(Math.Max(1, level)));

                CSLogger.Warn("StorySkills: no development model to say what a level has cost; " +
                              "a sheet is drawn at what the game prices as level one.");
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StorySkills: the development model would not price a level ({ex.GetType().Name}).");
            }

            return 0;
        }

        /// <summary>What one skill at that level is worth, in the game's own pricing.</summary>
        private static double XpOf(int value) => XpPerSkill * Math.Pow(Math.Max(0, value), XpCurve);

        /// <summary>
        ///     How much of a life was lived, from nothing said to everything the
        ///     written material can say. Every point in the profile is something
        ///     the story says was done or endured, measured against the fullest
        ///     life the catalog can write.
        /// </summary>
        private static double ShareOfALife(LifeProfile profile) =>
            Math.Max(0.0, Math.Min(1.0, Earned(profile) / (double)RichestLife()));

        /// <summary>How much of a life this profile is, counting only what it leaned toward.</summary>
        private static int Earned(LifeProfile profile)
        {
            int earned = 0;
            foreach (LifeProfile.Lean lean in Enum.GetValues(typeof(LifeProfile.Lean)))
                earned += Math.Max(0, profile.Score(lean));

            return earned;
        }

        /// <summary>
        ///     The fullest life the written material can produce, read off the
        ///     catalog rather than guessed at. A denominator typed by hand is a claim
        ///     about content that stops being true the first time a scene is added or
        ///     an option reweighted.
        ///
        ///     Gates are not walked, so this is the ceiling of what any run could
        ///     gather rather than of what one run can. That is the right side to err
        ///     on: it costs a full life a little of the top of the range, where
        ///     understating it would hand the top of the range to lives that are not
        ///     full.
        /// </summary>
        internal static int RichestLife() => RichestLife(Application.GuidedRoute.Scenes);

        /// <inheritdoc cref="RichestLife()"/>
        internal static int RichestLife(IReadOnlyList<CharacterCreation.Scenes.Scene> set)
        {
            if (RichestLives.TryGetValue(set, out int held)) return held;

            int scenes = 0;
            try
            {
                foreach (var scene in set)
                {
                    int best = 0;
                    foreach (var option in scene.Options)
                        best = Math.Max(best, Earned(LifeProfile.From(option.Consequences)));

                    scenes += best;
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StorySkills: the catalog would not say what a full life is worth ({ex.GetType().Name}).");
            }

            int richest = Math.Max(1, scenes);
            RichestLives[set] = richest;
            return richest;
        }

        /// <summary>
        ///     Which skill sits at which height on this character's own sheet. What
        ///     the life argues for comes first; among skills it argues for equally,
        ///     what the world's own people carry higher decides, so a life that
        ///     leaned martial and nothing finer still reads like a person rather
        ///     than like ten identical numbers. The skill's name settles the rest,
        ///     so the same life always draws the same sheet.
        /// </summary>
        private static List<SkillObject> Ranked(Dictionary<SkillObject, double> weights, People people)
        {
            return Skills.All
                .Where(skill => skill != null)
                .OrderByDescending(skill => weights.TryGetValue(skill, out double weight) ? weight : 0.0)
                .ThenByDescending(people.Standing)
                .ThenBy(skill => skill.StringId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        ///     How much of the life each skill accounts for. Training is what the
        ///     life taught by name; the floor is what it leaned toward, which is
        ///     why an untrained skill outranks one the life never went near.
        ///
        ///     No scene names a skill anywhere, so the training list is the one
        ///     weapon the answers put in the character's hands. Without that every
        ///     skill inside a lean would come out identical, and a life spent
        ///     drawing a bow would read exactly like one spent behind a shield.
        ///
        ///     Also the weights the pipeline's focus spend follows, so what the
        ///     years went into and what the character is focused in are one reading.
        /// </summary>
        internal static Dictionary<SkillObject, double> WeightsFor(CharacterCreationSession session) =>
            WeightsFor(LifeProfile.From(session));

        /// <summary>The same weights, taken straight off a profile.</summary>
        internal static Dictionary<SkillObject, double> WeightsFor(LifeProfile profile)
        {
            var trained = WhatTheHandsKnow(profile);

            var weights = new Dictionary<SkillObject, double>();

            foreach (var skill in Skills.All)
            {
                if (skill == null) continue;

                trained.TryGetValue(skill, out int taught);
                var lean = LeanFor(skill);
                double floor = lean.HasValue ? profile.Band(lean.Value) : 0;

                // Training counts for more than proximity, which is what makes the
                // weapon the hands learned visibly better than the rest of its lean
                double weight = taught * (1 + floor) + floor;
                if (weight > 0) weights[skill] = weight;
            }

            return weights;
        }

        /// <summary>
        ///     The weapon the life trained for, as the skill that holds it. One
        ///     entry, weighted as a named skill would be, so the hands are better
        ///     at their own weapon without the rest of the life collapsing into it.
        /// </summary>
        private static Dictionary<SkillObject, int> WhatTheHandsKnow(LifeProfile profile)
        {
            var totals = new Dictionary<SkillObject, int>();

            string? id = profile.Weapon switch
            {
                LifeProfile.Trained.Blade => "OneHanded",
                LifeProfile.Trained.GreatWeapon => "TwoHanded",
                LifeProfile.Trained.Spear => "Polearm",
                LifeProfile.Trained.Lance => "Polearm",
                LifeProfile.Trained.Bow => "Bow",
                LifeProfile.Trained.Crossbow => "Crossbow",
                LifeProfile.Trained.Thrown => "Throwing",
                _ => null
            };
            if (id == null) return totals;

            foreach (var skill in Skills.All)
                if (skill != null && skill.StringId == id)
                {
                    totals[skill] = 1;
                    break;
                }

            // A lance is a horse before it is a weapon
            if (profile.Weapon == LifeProfile.Trained.Lance)
                foreach (var skill in Skills.All)
                    if (skill != null && skill.StringId == "Riding")
                    {
                        totals[skill] = 1;
                        break;
                    }

            return totals;
        }

        /// <summary>
        ///     Which side of a life a skill belongs to, or nothing when no side of
        ///     a life speaks for it.
        ///
        ///     Every arm is named, the three naval skills included. A catch-all
        ///     arm was what put them at sea, and it put every skill this switch had
        ///     never heard of there with them: one skill added by an overhaul mod
        ///     and the whole of that mod's content read as seafaring. A skill no
        ///     lean claims now simply goes unclaimed, and takes the tail of the
        ///     sheet the way a skill nobody practiced does.
        /// </summary>
        private static LifeProfile.Lean? LeanFor(SkillObject skill)
        {
            // Asked before the switch so the three War Sails ids are named in one
            // place, next to the check for whether the DLC is there at all
            if (NavalDLCService.IsNavalSkill(skill)) return LifeProfile.Lean.Sea;

            return skill.StringId switch
            {
                "OneHanded" or "TwoHanded" or "Polearm" or "Bow" or "Crossbow" or "Throwing"
                    or "Riding" or "Athletics" or "Tactics" or "Leadership"
                    => LifeProfile.Lean.Martial,
                "Trade" or "Steward" => LifeProfile.Lean.Commerce,
                "Charm" => LifeProfile.Lean.Standing,
                "Roguery" => LifeProfile.Lean.Following,
                "Scouting" or "Medicine" => LifeProfile.Lean.Wilds,
                "Crafting" or "Engineering" => LifeProfile.Lean.Craft,
                _ => null
            };
        }

        /// <summary>
        ///     What the world's living adults look like on paper. Read once and
        ///     held for the whole creation run, because the yardstick has to be the
        ///     world as it stood BEFORE this character's start was applied. The
        ///     pipeline creates their companions, vassals, spouse and kin, and a
        ///     reading taken again after that measures the character against people
        ///     the mod just made for them: the reading used to be retaken whenever
        ///     the world's roll changed size, which is exactly what those people do
        ///     to it, and a single apply moved the population it was measured
        ///     against by more than thirty while it ran.
        ///
        ///     The first ask comes from the first surface that draws a sheet, which
        ///     is the opening scene of a guided run or the Start Editor opening on
        ///     an ordinary adult: long after the world is built, and long before
        ///     the pipeline creates anybody. <see cref="ForgetTheWorld"/> drops it
        ///     when a creation run starts over, and a reading is never kept across
        ///     campaigns.
        ///
        ///     Only the skills the running game actually registers are read, which
        ///     is what keeps a download that does not have War Sails loaded from
        ///     ever meeting a naval skill: the three of them are in
        ///     <c>Skills.All</c> only when the DLC put them there.
        /// </summary>
        private static People? Everyone()
        {
            var campaign = Campaign.Current;
            if (campaign == null) return null;

            if (_people != null && ReferenceEquals(_peopleOf, campaign)) return _people;

            try
            {
                var skills = Skills.All.Where(skill => skill != null).ToList();
                if (skills.Count == 0) return null;

                Hero? self = null;
                try
                {
                    self = Hero.MainHero;
                }
                catch (Exception)
                {
                    // The character being written is not evidence about the world,
                    // but a world with nobody in it yet is not a failure either
                }

                var shape = new double[skills.Count];
                var standing = new Dictionary<SkillObject, double>();
                var lordsByAge = new List<(int Age, int Best)>();
                var sheet = new int[skills.Count];
                int read = 0;
                int weakest = int.MaxValue;
                int strongest = 0;

                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == self || hero.IsChild) continue;

                    int best = 0;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        sheet[i] = Math.Max(0, hero.GetSkillValue(skills[i]));
                        if (sheet[i] > best) best = sheet[i];
                    }

                    if (best <= 0) continue;

                    // Lords alone carry an authored age and sheet, so they alone say
                    // what the years are worth
                    if (hero.IsLord) lordsByAge.Add(((int)hero.Age, best));

                    for (int i = 0; i < skills.Count; i++)
                    {
                        standing.TryGetValue(skills[i], out double carried);
                        standing[skills[i]] = carried + sheet[i];
                    }

                    Array.Sort(sheet);
                    for (int i = 0; i < skills.Count; i++)
                        shape[i] += sheet[skills.Count - 1 - i] / (double)best;

                    read++;
                    if (best < weakest) weakest = best;
                    if (best > strongest) strongest = best;
                }

                if (read == 0)
                {
                    CSLogger.Warn("StorySkills: the world holds nobody to measure this character against; " +
                                  "no skill is set, and whatever the game's own creation left stands.");
                    return null;
                }

                for (int i = 0; i < shape.Length; i++) shape[i] /= read;
                foreach (var skill in skills) standing[skill] = standing[skill] / read;

                int floor = GameCaps.MinAdultAge();
                _people = new People(shape, weakest, strongest, standing,
                    AgeCurve.Measure(lordsByAge, floor, AgeCurve.Ceiling), floor);
                _peopleOf = campaign;

                CSLogger.Info($"StorySkills: measured {read} of the world's adults over {skills.Count} skills; " +
                              $"their best skill runs {_people.WeakestBest} to {_people.StrongestBest}, " +
                              "and a sheet falls to " +
                              $"{shape[shape.Length - 1]:F2} of its own best by the last of them.");
                return _people;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StorySkills: the world's people could not be read ({ex.GetType().Name}: {ex.Message}).");
                return null;
            }
        }

        /// <summary>
        ///     The living population, as the three things a sheet has to be drawn
        ///     against: how high a best skill goes at either end of the world, the
        ///     average shape of a sheet from its own best down to its own least,
        ///     and how much of a skill a person here normally carries.
        /// </summary>
        private sealed class People
        {
            private readonly Dictionary<SkillObject, double> _standing;

            private readonly double[] _byAge;
            private readonly int _youngest;

            internal People(double[] shape, int weakestBest, int strongestBest,
                Dictionary<SkillObject, double> standing, double[] byAge, int youngest)
            {
                Shape = shape;
                WeakestBest = weakestBest;
                StrongestBest = strongestBest;
                _standing = standing;
                _byAge = byAge;
                _youngest = youngest;
            }

            /// <summary>The best skill this world's lords carry at an age, never less than at a younger one.</summary>
            internal double BestAt(int age) => AgeCurve.At(_byAge, _youngest, age);

            /// <summary>Each rank of a sheet as a share of that sheet's own best skill.</summary>
            internal double[] Shape { get; }

            /// <summary>The best skill of the least capable adult alive.</summary>
            internal int WeakestBest { get; }

            /// <summary>The best skill of the most capable adult alive.</summary>
            internal int StrongestBest { get; }

            /// <summary>What this world's people average in one skill.</summary>
            internal double Standing(SkillObject skill) =>
                _standing.TryGetValue(skill, out double value) ? value : 0.0;
        }
    }
}
