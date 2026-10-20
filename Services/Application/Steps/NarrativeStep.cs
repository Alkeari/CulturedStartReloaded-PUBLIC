using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Applies age, the chosen names, and initial skill levels, then settles
    ///     the character's attributes and focus.
    ///
    ///     The guided route's scenes pass no args to the narrative menus, so
    ///     vanilla finalization adds nothing to either pool and both are empty
    ///     when this runs. Waiting for a pool therefore granted nothing at all:
    ///     a character the story called old and high-standing arrived with every
    ///     attribute at 2 and no focus anywhere, under skill levels that nothing
    ///     on the sheet supported. The character has to arrive finished,
    ///     so what the life is worth is placed directly and only a pool that
    ///     genuinely exists is spent on top of it. The custom route is left
    ///     alone, because its exact values are its whole promise.
    ///
    ///     The same sheet is written while the scenes are still being answered,
    ///     by <see cref="ShowTheLifeSoFar"/>, so the player watches the character
    ///     fill in instead of meeting them finished. By the time this step runs
    ///     the life is already placed and every budget below measures out at
    ///     none, which is what makes the two safe to have both.
    /// </summary>
    public sealed class NarrativeStep : IStartStep
    {
        // Cultured Start's own arithmetic, as that route has always priced a life:
        // every answer names the skills it trained, and the game's replay of the
        // same answers' args grants the focus and attributes behind those numbers
        private const int FocusedBaseLevel = 50;
        private const int LevelPerFocusPoint = 10;
        private const int UnfocusedBaseLevel = 25;

        public string Name => "Narrative";

        public string? Validate(StartContext context)
        {
            return context.Hero.HeroDeveloper == null
                ? "hero has no developer"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var session = context.Session;

            int targetAge = session.EffectiveAge;
            hero.SetBirthDay(CampaignTime.YearsFromNow(-targetAge));
            CSLogger.Info($"NarrativeStep: age set to {targetAge} (Hero.Age={hero.Age:F1}).");

            if (session.Mode == SetupMode.LifePath)
            {
                ApplyLifePath(hero, session, context.Settings?.VanillaSkillLevels == true);
                return;
            }

            ApplyChosenNames(context);

            if (context.Settings?.VanillaSkillLevels == true)
            {
                CSLogger.Info("NarrativeStep: vanilla skill levels enabled; initial levels untouched.");
            }
            else
            {
                var levels = StorySkills.LevelsFor(session);
                foreach (var skill in Skills.All)
                {
                    levels.TryGetValue(skill, out int level);
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, level);
                }

                CSLogger.Info($"NarrativeStep: the life reads as level {StorySkills.PriceOf(levels.Values)}, " +
                              $"worth {levels.Values.Sum()} skill across {levels.Count} skills, " +
                              $"the best of them {(levels.Count == 0 ? 0 : levels.Values.Max())}.");
            }

            SpendTheLifesPoints(hero, session);

            ApplyCustomStats(hero, session);

            if (context.Settings?.VanillaSkillLevels != true)
                SettleTheLevel(hero);
        }

        /// <summary>
        ///     Makes the game agree with the sheet about what level this character
        ///     is.
        ///
        ///     Writing a skill level does not move a hero's level, and nothing else
        ///     in this path does either: <c>SetInitialSkillLevel</c> only writes the
        ///     skill's own experience, and the running total the game reads a level
        ///     off is set in one place, <c>SetInitialLevelFromSkills</c>, which runs
        ///     once inside <c>InitializeHeroDeveloper</c> and is long past by the
        ///     time a start is applied. So the level the player saw was the price of
        ///     the sheet the game's own creation left, not of the one written over
        ///     it: a character carrying skills worth level thirty-five was shown as
        ///     level ten, and every number the game derives from a level, from party
        ///     size to what the sheet is worth to read, followed the wrong one.
        ///
        ///     The price is the game's own, asked of the hero's real sheet after
        ///     every route has finished writing it, so the editor's exact values are
        ///     priced as truly as the guided route's. Both halves are written
        ///     together, because a total that disagrees with the level would be
        ///     walked straight back off by the game's next check.
        /// </summary>
        private static void SettleTheLevel(Hero hero)
        {
            try
            {
                int worth = StorySkills.PriceOf(
                    Skills.All.Where(skill => skill != null).Select(hero.GetSkillValue));

                int before = hero.Level;
                hero.HeroDeveloper.SetInitialLevel(worth);
                hero.Level = worth;

                CSLogger.Info($"NarrativeStep: the game prices this sheet at level {worth}; " +
                              $"the character was carrying level {before}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("NarrativeStep: settling the character's level against their own sheet failed.", ex);
            }
        }

        /// <summary>
        ///     Brings the sheet up to what the life is worth, then spends anything
        ///     the start really did leave in the pools on top of it. Runs before
        ///     <see cref="ApplyCustomStats"/> so a value the player set by hand
        ///     still overrides everything derived here.
        /// </summary>
        internal static void SpendTheLifesPoints(Hero hero, CharacterCreationSession session)
        {
            // The custom route sets exact values; there is nothing to derive there
            if (!GuidedRun.WasWalked) return;

            try
            {
                GrantWhatTheLifeIsWorth(hero, session);
                SpendAttributePoints(hero, session);
                SpendFocusPoints(hero, session);
            }
            catch (Exception ex)
            {
                CSLogger.Error("NarrativeStep: settling the life's attributes and focus failed.", ex);
            }
        }

        /// <summary>
        ///     The sheet each attribute and skill held before the first scene was
        ///     answered. Every target below is measured from here rather than from
        ///     wherever the previous answer left the character, which is what makes
        ///     one life read as one sheet however many times it is written.
        /// </summary>
        private static Dictionary<string, int>? _pristineAttributes;

        private static Dictionary<string, int>? _pristineFocus;

        /// <summary>Forgets the pristine sheet, for a creation run that starts over.</summary>
        internal static void ForgetTheLifeSoFar()
        {
            _pristineAttributes = null;
            _pristineFocus = null;
        }

        /// <summary>
        ///     Writes the sheet the life has earned SO FAR onto the live hero, so
        ///     the stage's own panel fills in while the scenes are being answered
        ///     instead of after the player can no longer watch it.
        ///
        ///     The life so far, never a projection. The level is priced off the
        ///     answers that exist at this moment, so a character three scenes in
        ///     carries what three scenes earned and nothing else; a child early in
        ///     a run holds very little, and that is the true reading.
        ///
        ///     Every value goes in as a TARGET rather than as an amount added,
        ///     which is what makes this safe to run as often as it runs. The engine
        ///     fires an option's select handler every time the player lands on it,
        ///     so the same answer is written again and again, and a target written
        ///     twice is one sheet. It is also what lets the player walk back: a
        ///     scene answered differently drops the answers that followed it, which
        ///     prices a smaller life, and the sheet comes down to meet it rather
        ///     than ratcheting.
        ///
        ///     Neither pool is touched. AddAttribute and AddFocus leave both alone
        ///     when they are told not to charge for the point, and RemoveAttribute
        ///     and RemoveFocus refund nothing, so the character never shows a point
        ///     as pending that nothing is going to spend. Nor does the pipeline
        ///     grant the life a second time: it measures its own budget as what the
        ///     sheet still lacks, and by then the sheet lacks nothing.
        ///
        ///     The skill levels go on here too, not only in the apply pass. The
        ///     number on each skill icon is the character's own skill value, and
        ///     the only way for that to be true while the scenes are running is for
        ///     the character to really carry it: the panel used to paint the value
        ///     the finished run would reach onto a sheet that still held nothing,
        ///     which is a forecast the sheet must never show.
        /// </summary>
        internal static void ShowTheLifeSoFar()
        {
            try
            {
                if (!GuidedRun.WasWalked) return;

                var session = CreationSession.Current;
                var hero = Hero.MainHero;
                if (session == null || hero?.HeroDeveloper == null) return;

                RememberThePristineSheet(hero);

                var profile = LifeProfile.From(session);
                WriteTheAttributes(hero, profile);
                WriteTheFocus(hero, profile);
                WriteTheSkills(hero, profile);
            }
            catch (Exception ex)
            {
                CSLogger.Error("NarrativeStep: showing the life so far failed; the sheet stands where it was.", ex);
            }
        }

        /// <summary>
        ///     What the sheet held before any of this, taken the first time a life
        ///     is written rather than when the run begins: the game clears the
        ///     player and puts its own opening value in every attribute before the
        ///     first stage, and that is the floor every later target is measured
        ///     from.
        ///
        ///     The floor is the whole level-one sheet, not only the part the engine
        ///     had already placed. Every target below is what a life of that level
        ///     carries in full, and the development model puts AttributePointsAtStart
        ///     and FocusPointsAtStart in that sheet for existing rather than for
        ///     anything the player answered. Measured from the engine's own leavings
        ///     instead, the first answer was credited with that whole block on top of
        ///     what its level earned, and the panel lit it as one option's doing: on
        ///     the installed model the block is five focus, which is the entire
        ///     per-skill cap, so the opening scene put five bars on one skill.
        /// </summary>
        private static void RememberThePristineSheet(Hero hero)
        {
            if (_pristineAttributes != null && _pristineFocus != null) return;

            var attributes = new Dictionary<string, int>();
            foreach (var attribute in Attributes.All)
                if (attribute != null)
                    attributes[attribute.StringId] = hero.GetAttributeValue(attribute);

            var focus = new Dictionary<string, int>();
            foreach (var skill in Skills.All)
                if (skill != null)
                    focus[skill.StringId] = hero.HeroDeveloper.GetFocus(skill);

            PlaceTheBirthright(attributes, focus);

            _pristineAttributes = attributes;
            _pristineFocus = focus;

            CSLogger.Info($"NarrativeStep: the untold life holds {attributes.Values.Sum()} across " +
                          $"{attributes.Count} attributes and {focus.Values.Sum()} in focus; " +
                          "every target the scenes write is measured from there.");
        }

        /// <summary>
        ///     Brings the floor up to the sheet the game prices a level-one
        ///     character at, so what an answer is credited with is only what its
        ///     level moved.
        ///
        ///     Placed by the planner every other point goes through, over a life
        ///     nothing has been said about, and then left where it was put. A
        ///     birthright that moved with the life would land on whichever answer
        ///     first gave the life a leaning, which is the defect this exists to
        ///     end rather than relocate.
        /// </summary>
        private static void PlaceTheBirthright(Dictionary<string, int> attributes,
            Dictionary<string, int> focus)
        {
            var untold = LifeProfile.From((IReadOnlyList<Models.ChoiceConsequence>?)null);

            int? attributeWorth = AttributeWorthAt(1);
            if (attributeWorth != null)
                foreach (var pair in PlanAttributePoints(untold, attributeWorth.Value - attributes.Values.Sum(),
                             attribute => Held(attributes, attribute.StringId), GameCaps.MaxAttribute()))
                    attributes[pair.Key.StringId] = Held(attributes, pair.Key.StringId) + pair.Value;

            int? focusWorth = FocusWorthAt(1);
            if (focusWorth != null)
                foreach (var pair in PlanFocusPoints(untold, focusWorth.Value - focus.Values.Sum(),
                             skill => Held(focus, skill.StringId), GameCaps.MaxFocus()))
                    focus[pair.Key.StringId] = Held(focus, pair.Key.StringId) + pair.Value;
        }

        private static int Held(Dictionary<string, int> sheet, string id) =>
            sheet.TryGetValue(id, out int value) ? value : 0;

        /// <summary>Brings the character's attributes to what the life so far holds.</summary>
        private static void WriteTheAttributes(Hero hero, LifeProfile profile)
        {
            var held = AttributesTheLifeHolds(profile);
            if (held == null) return;

            var moved = new List<string>();
            foreach (var pair in held)
            {
                int live = hero.GetAttributeValue(pair.Key);
                if (pair.Value == live) continue;

                if (pair.Value > live) hero.HeroDeveloper.AddAttribute(pair.Key, pair.Value - live, false);
                else hero.HeroDeveloper.RemoveAttribute(pair.Key, live - pair.Value);

                moved.Add($"{pair.Key.StringId} {live} to {hero.GetAttributeValue(pair.Key)}");
            }

            if (moved.Count > 0)
                CSLogger.Info($"NarrativeStep: the life so far reads as level {StorySkills.LevelFor(profile)} " +
                              "and moves " + string.Join(", ", moved) + ".");
        }

        /// <summary>The focus half, brought to the same reading.</summary>
        private static void WriteTheFocus(Hero hero, LifeProfile profile)
        {
            var held = FocusTheLifeHolds(profile);
            if (held == null) return;

            var developer = hero.HeroDeveloper;
            int sharpened = 0;
            int dulled = 0;

            foreach (var pair in held)
            {
                int live = developer.GetFocus(pair.Key);
                if (pair.Value == live) continue;

                if (pair.Value > live)
                {
                    developer.AddFocus(pair.Key, pair.Value - live, false);
                    sharpened += pair.Value - live;
                }
                else
                {
                    developer.RemoveFocus(pair.Key, live - pair.Value);
                    dulled += live - pair.Value;
                }
            }

            if (sharpened > 0 || dulled > 0)
                CSLogger.Info($"NarrativeStep: the life so far is worth {held.Values.Sum()} focus; " +
                              $"{sharpened} point(s) went on and {dulled} came off.");
        }

        /// <summary>
        ///     The skill levels the life so far has earned, put on the character
        ///     while the scenes are still running so the figure on each icon is
        ///     read off the character rather than predicted for them.
        ///
        ///     Written as targets, like everything else here, so answering again or
        ///     walking back brings the sheet to the life that is actually told.
        ///     Silent under the Vanilla Skill Levels setting, which is a promise
        ///     that the mod never sets a level at all.
        /// </summary>
        private static void WriteTheSkills(Hero hero, LifeProfile profile)
        {
            if (MCM.Abstractions.Base.Global.GlobalSettings<Settings.CSSettings>.Instance?.VanillaSkillLevels == true)
                return;

            var levels = StorySkills.LevelsFor(profile);
            if (levels.Count == 0) return;

            foreach (var skill in Skills.All)
            {
                if (skill == null) continue;

                levels.TryGetValue(skill, out int level);
                if (hero.GetSkillValue(skill) != level)
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, level);
            }
        }

        /// <summary>
        ///     Every attribute this life holds, as an absolute value: what the
        ///     untold sheet started at plus the share of the level's budget the
        ///     same planner the pipeline uses puts there. Measuring the budget from
        ///     the pristine sheet rather than from the live one is what stops a run
        ///     of answers compounding into a character the life never earned.
        ///
        ///     Null, not an empty sheet, when there is nothing to measure against:
        ///     "this life holds nothing" and "nothing could be read" are different
        ///     answers and only one of them should move a character or a panel.
        /// </summary>
        internal static Dictionary<CharacterAttribute, int>? AttributesTheLifeHolds(LifeProfile profile)
        {
            int? worth = AttributeWorthAt(StorySkills.LevelFor(profile));
            var pristine = _pristineAttributes;
            if (worth == null || pristine == null) return null;

            int Was(CharacterAttribute attribute) =>
                pristine.TryGetValue(attribute.StringId, out int value) ? value : 0;

            int placed = 0;
            foreach (var attribute in Attributes.All)
                if (attribute != null)
                    placed += Was(attribute);

            var plan = PlanAttributePoints(profile, Math.Max(0, worth.Value - placed),
                Was, GameCaps.MaxAttribute());

            var held = new Dictionary<CharacterAttribute, int>();
            foreach (var attribute in Attributes.All)
            {
                if (attribute == null) continue;

                plan.TryGetValue(attribute, out int earned);
                held[attribute] = Was(attribute) + earned;
            }

            return held;
        }

        /// <summary>The focus half, read the same way and null for the same reason.</summary>
        internal static Dictionary<SkillObject, int>? FocusTheLifeHolds(LifeProfile profile)
        {
            int? worth = FocusWorthAt(StorySkills.LevelFor(profile));
            var pristine = _pristineFocus;
            if (worth == null || pristine == null) return null;

            int Was(SkillObject skill) =>
                pristine.TryGetValue(skill.StringId, out int value) ? value : 0;

            int placed = 0;
            foreach (var skill in Skills.All)
                if (skill != null)
                    placed += Was(skill);

            var plan = PlanFocusPoints(profile, Math.Max(0, worth.Value - placed), Was, GameCaps.MaxFocus());

            var held = new Dictionary<SkillObject, int>();
            foreach (var skill in Skills.All)
            {
                if (skill == null) continue;

                plan.TryGetValue(skill, out int earned);
                held[skill] = Was(skill) + earned;
            }

            return held;
        }

        /// <summary>
        ///     The sheet a character of the story's level carries, placed rather
        ///     than waited for.
        ///
        ///     What that is worth is the game's own answer, not one of ours.
        ///     HeroDeveloper prices a hero of a given level in SetupDefaultPoints,
        ///     then in SetInitialFocusAndAttributePoints subtracts whatever the
        ///     sheet already holds; this is those same two readings, over the very
        ///     level <see cref="StorySkills"/> derives the skill levels from, so
        ///     the attributes cannot disagree with the skills and a mod that
        ///     rescales progression rescales this with it.
        ///
        ///     Written as a target rather than an amount, which makes it
        ///     idempotent: a second run finds the sheet already at the target and
        ///     grants nothing, so the post-creation pass on the map can call it
        ///     without granting the life twice.
        /// </summary>
        private static void GrantWhatTheLifeIsWorth(Hero hero, CharacterCreationSession session)
        {
            int level = StorySkills.LevelFor(session);
            CSLogger.Info($"NarrativeStep: at level {level} the game prices the sheet at " +
                          $"{AttributeBudgetFor(hero, level)} attribute and {FocusBudgetFor(hero, level)} focus " +
                          "point(s) beyond what it already holds.");

            GrantAttributes(hero, session, level);
            GrantFocus(hero, session, level);
        }

        /// <summary>
        ///     What a hero of that level carries in attributes, priced by the
        ///     installed development model, less what the sheet already holds.
        ///     Vanilla's own creation leaves 2 in each attribute, so a thin life
        ///     still has the remainder of a level-one hero's sheet to place.
        /// </summary>
        internal static int AttributeBudgetFor(Hero hero, int level)
        {
            int? worth = AttributeWorthAt(level);
            if (worth == null)
            {
                CSLogger.Warn("NarrativeStep: no development model to price the life's attributes; none granted.");
                return 0;
            }

            int placed = 0;
            foreach (var attribute in Attributes.All)
                if (attribute != null)
                    placed += hero.GetAttributeValue(attribute);

            return Math.Max(0, worth.Value - placed);
        }

        /// <summary>
        ///     The whole attribute sheet a hero of that level carries, before
        ///     anything already placed is taken off it. Null rather than zero when
        ///     there is no model to ask, because "the game prices this at nothing"
        ///     and "nothing could be asked" are different answers and only one of
        ///     them should move a character.
        /// </summary>
        internal static int? AttributeWorthAt(int level)
        {
            var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
            if (model == null) return null;

            return model.AttributePointsAtStart
                   + Math.Max(0, level - 1) / Math.Max(1, model.LevelsPerAttributePoint);
        }

        /// <summary>
        ///     What a hero of that level carries in focus, priced by the installed
        ///     development model, less what the sheet already holds.
        /// </summary>
        internal static int FocusBudgetFor(Hero hero, int level)
        {
            int? worth = FocusWorthAt(level);
            if (worth == null)
            {
                CSLogger.Warn("NarrativeStep: no development model to price the life's focus; none granted.");
                return 0;
            }

            int placed = 0;
            foreach (var skill in Skills.All)
                if (skill != null)
                    placed += hero.HeroDeveloper.GetFocus(skill);

            return Math.Max(0, worth.Value - placed);
        }

        /// <summary>
        ///     The whole focus sheet a hero of that level carries, read the same
        ///     way and null for the same reason as <see cref="AttributeWorthAt"/>.
        /// </summary>
        internal static int? FocusWorthAt(int level)
        {
            var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
            if (model == null) return null;

            return model.FocusPointsAtStart + Math.Max(0, level - 1) * model.FocusPointsPerLevel;
        }

        /// <summary>
        ///     The life's attributes, placed by the same planner the pool spend
        ///     uses, so the estimate and the grant read one decision.
        /// </summary>
        private static void GrantAttributes(Hero hero, CharacterCreationSession session, int level)
        {
            int budget = AttributeBudgetFor(hero, level);
            if (budget <= 0) return;

            int cap = GameCaps.MaxAttribute();
            var plan = PlanAttributePoints(LifeProfile.From(session), budget, hero.GetAttributeValue, cap);

            // A direct grant, the way vanilla makes one. checkUnspentPoints false
            // skips the pool on both sides: it neither demands a point that is not
            // there nor spends one that is, which is the only form that places
            // anything while both pools are empty. The engine still refuses any
            // amount that would carry an attribute past MaxAttribute, and refuses
            // it silently, so the planner's own cap is what keeps every point
            // placeable. The whole share goes in one call, because nothing is
            // being charged for it.
            foreach (var pair in plan)
                hero.HeroDeveloper.AddAttribute(pair.Key, pair.Value, false);

            foreach (var pair in plan)
                CSLogger.Info($"NarrativeStep: the life is worth {pair.Value} in {pair.Key.StringId}, " +
                              $"now {hero.GetAttributeValue(pair.Key)}.");
        }

        /// <summary>
        ///     The life's focus, placed the same way, and following the same
        ///     reading of the life the skill levels came from.
        /// </summary>
        private static void GrantFocus(Hero hero, CharacterCreationSession session, int level)
        {
            var developer = hero.HeroDeveloper;
            int budget = FocusBudgetFor(hero, level);
            if (budget <= 0) return;

            int cap = GameCaps.MaxFocus();
            var plan = PlanFocusPoints(session, budget, developer.GetFocus, cap);

            // AddFocus enforces no ceiling of its own in either form, so the
            // planner's cap is the only thing holding a skill under MaxFocusPerSkill
            foreach (var pair in plan)
                developer.AddFocus(pair.Key, pair.Value, false);

            foreach (var pair in plan)
                CSLogger.Info($"NarrativeStep: the life sharpened {pair.Key.StringId} by {pair.Value}, " +
                              $"now {developer.GetFocus(pair.Key)}.");
        }

        /// <summary>
        ///     A pool that genuinely exists, spent on top of the grant: vanilla's
        ///     own creation stages fill one, and so does the map once a level is
        ///     settled. Attributes follow the leanings, one point at a time to
        ///     whichever attribute the life argues hardest for and still has room
        ///     for. Empty on the guided route as it stands, which is the whole
        ///     reason the grant above exists.
        /// </summary>
        private static void SpendAttributePoints(Hero hero, CharacterCreationSession session)
        {
            var developer = hero.HeroDeveloper;
            int pool = developer.UnspentAttributePoints;
            if (pool <= 0) return;

            int cap = GameCaps.MaxAttribute();
            var given = PlanAttributePoints(LifeProfile.From(session), pool, hero.GetAttributeValue, cap);

            foreach (var pair in given)
                // One point per call: AddAttribute charges a single point however
                // many it adds, so a batched call would leave the rest in the pool
                for (int i = 0; i < pair.Value && developer.UnspentAttributePoints > 0; i++)
                    developer.AddAttribute(pair.Key, 1, true);

            foreach (var pair in given)
                CSLogger.Info($"NarrativeStep: {pair.Value} attribute point(s) into {pair.Key.StringId}, " +
                              $"now {hero.GetAttributeValue(pair.Key)}.");

            // Every attribute the game registers is at the cap by the time the
            // planner runs out of room, so these cannot be placed by anyone,
            // player included. The character has to arrive finished, and a pool
            // nobody can ever spend is the plainest case of something left pending
            if (developer.UnspentAttributePoints > 0)
            {
                CSLogger.Warn($"NarrativeStep: {developer.UnspentAttributePoints} attribute point(s) cannot be " +
                              $"placed anywhere; every attribute is at the cap of {cap}, so they are let go " +
                              "rather than left pending on the character.");
                developer.UnspentAttributePoints = 0;
            }
        }

        /// <summary>
        ///     Where a guided life's attribute points go, decided without a hero:
        ///     the weights the leanings argue for, handed out one at a time by
        ///     <see cref="NextInLine{T}"/>. The one copy of that decision, so the
        ///     estimate the Start Editor shows and the grant the pipeline makes
        ///     cannot disagree about the same life.
        ///
        ///     Two rings, widest last, because what the life argues for can be
        ///     narrower than what the life is worth. Six attributes at a cap of ten
        ///     leave so much headroom that the outer ring has never been needed
        ///     here, but it is the same shape the focus half trips over, and an
        ///     overhaul that lowers the cap or raises the pricing would bring it
        ///     within reach.
        ///
        ///     <paramref name="valueOf"/> is where each attribute stands before any
        ///     of this is spent; the plan adds what it has already placed, which is
        ///     what a live hero would have been showing by then.
        /// </summary>
        internal static Dictionary<CharacterAttribute, int> PlanAttributePoints(
            LifeProfile profile, int pool, Func<CharacterAttribute, int> valueOf, int cap)
        {
            var everything = new Dictionary<CharacterAttribute, double>();
            foreach (var attribute in Attributes.All)
                if (attribute != null)
                    everything[attribute] = 1;

            return Spread(
                new[]
                {
                    ("what the life leaned toward", AttributeWeights(profile)),
                    ("whatever the character can still hold", everything)
                },
                pool, valueOf, cap, "attribute", a => a.StringId);
        }

        /// <summary>
        ///     The focus half of the same pool spend, following the same reading
        ///     of the life the skill levels came from, so the character is focused
        ///     in what their years went into. Focus follows what the life argued
        ///     for rather than the finished sheet: every skill on that sheet carries
        ///     something now, the way every person in the game does, and focusing a
        ///     trade the character never practiced because it is not at zero would
        ///     say the life went somewhere it did not.
        /// </summary>
        private static void SpendFocusPoints(Hero hero, CharacterCreationSession session)
        {
            var developer = hero.HeroDeveloper;
            int pool = developer.UnspentFocusPoints;
            if (pool <= 0) return;

            int cap = GameCaps.MaxFocus();
            var given = PlanFocusPoints(session, pool, developer.GetFocus, cap);

            foreach (var pair in given)
                // One point per call: AddFocus charges exactly one however many it adds
                for (int i = 0; i < pair.Value && developer.UnspentFocusPoints > 0; i++)
                    developer.AddFocus(pair.Key, 1, true);

            foreach (var pair in given)
                CSLogger.Info($"NarrativeStep: {pair.Value} focus point(s) into {pair.Key.StringId}, " +
                              $"now {developer.GetFocus(pair.Key)}.");

            // Let go for the same reason as the attribute pool above
            if (developer.UnspentFocusPoints > 0)
            {
                CSLogger.Warn($"NarrativeStep: {developer.UnspentFocusPoints} focus point(s) cannot be placed " +
                              $"anywhere; every skill is at the focus cap of {cap}, so they are let go rather " +
                              "than left pending on the character.");
                developer.UnspentFocusPoints = 0;
            }
        }

        /// <summary>
        ///     Where a guided life's focus points go, decided without a hero. The
        ///     weights are the skill levels the same life earns, so focus follows
        ///     what the years went into; the ordering is the pipeline's own, which
        ///     is what stops a tie landing on a different skill in an estimate than
        ///     it does on the character.
        ///
        ///
        ///     Three rings, each tried only once the one inside it is full, because
        ///     a life can be worth more focus than the trades it names can hold. A
        ///     life that leaned two ways names five skills, five focus each is a
        ///     ceiling of twenty five, and the level such a life reaches is priced
        ///     at twenty eight: three points used to reach the end of the weighted
        ///     skills, find every one of them capped, and quietly disappear, which
        ///     is exactly the unspent pool a finished character must not have.
        ///
        ///     Widening to every skill at once would have spent those three on
        ///     trades the life never went near, and the panel beside the character
        ///     says exactly what the life bought. The middle ring is the honest
        ///     answer: the rest of what the same faculties do. A life of ledgers
        ///     and open country built a person's Social, Intelligence, Cunning and
        ///     Endurance, and the skills those faculties also carry are ones that
        ///     person could plausibly have picked up. Which faculties those are is
        ///     the same reading <see cref="AttributeWeights"/> gives the attribute
        ///     half, and which skills hang off each is the game's own registration,
        ///     so an overhaul that adds a skill places it here without being asked.
        /// </summary>
        internal static Dictionary<SkillObject, int> PlanFocusPoints(
            CharacterCreationSession session, int pool, Func<SkillObject, int> focusOf, int cap) =>
            PlanFocusPoints(LifeProfile.From(session), pool, focusOf, cap);

        /// <summary>The same plan, for a life read off a profile rather than a session.</summary>
        internal static Dictionary<SkillObject, int> PlanFocusPoints(
            LifeProfile profile, int pool, Func<SkillObject, int> focusOf, int cap)
        {
            var named = new Dictionary<SkillObject, double>();
            foreach (var pair in StorySkills.WeightsFor(profile))
                if (pair.Key != null && pair.Value > 0)
                    named[pair.Key] = pair.Value;

            var faculties = AttributeWeights(profile);
            var sameFaculties = new Dictionary<SkillObject, double>(named);
            var everything = new Dictionary<SkillObject, double>(named);

            foreach (var skill in Skills.All)
            {
                if (skill == null) continue;

                if (!sameFaculties.ContainsKey(skill))
                {
                    double argued = FacultyWeight(skill, faculties);
                    if (argued > 0) sameFaculties[skill] = argued;
                }

                if (!everything.ContainsKey(skill)) everything[skill] = 1;
            }

            // AddFocus does not enforce the per-skill ceiling itself, so the
            // candidate filter is the only thing keeping a skill under it
            return Spread(
                new[]
                {
                    ("what the life named or leaned toward", named),
                    ("the rest of what the same faculties do", sameFaculties),
                    ("whatever the character can still hold", everything)
                },
                pool, focusOf, cap, "skill", s => s.StringId);
        }

        /// <summary>
        ///     How hard the life argues for the faculties a skill is carried by.
        ///     Zero when none of them, which is what keeps the middle ring from
        ///     becoming the widest one.
        /// </summary>
        private static double FacultyWeight(SkillObject skill,
            Dictionary<CharacterAttribute, double> faculties)
        {
            var carriedBy = VersionedGameApi.AttributesOf(skill);
            if (carriedBy == null) return 0;

            double argued = 0;
            foreach (var attribute in carriedBy)
                if (attribute != null && faculties.TryGetValue(attribute, out double weight))
                    argued += weight;

            return argued;
        }

        /// <summary>
        ///     Hands a budget out one point at a time, over the narrowest ring of
        ///     candidates that still has room.
        ///
        ///     A ring is only left once every candidate in it is at the cap, so the
        ///     next one out is reached only when the character genuinely cannot
        ///     hold any more of what the life argued for. Each widening says in the
        ///     log what it took and why, because a point landing somewhere the
        ///     story did not send it is exactly the kind of thing that should be
        ///     readable afterwards rather than inferred from a sheet.
        ///
        ///     An empty ring is dropped rather than walked, so a life that leaned
        ///     nowhere starts at the widest one and is not reported as having
        ///     overflowed anything.
        /// </summary>
        private static Dictionary<T, int> Spread<T>(
            (string Why, Dictionary<T, double> Weights)[] rings, int pool,
            Func<T, int> valueOf, int cap, string kind, Func<T, string> nameOf) where T : class
        {
            var plan = new Dictionary<T, int>();
            if (pool <= 0) return plan;

            var walked = rings.Where(ring => ring.Weights.Count > 0).ToList();
            int placed = 0;

            for (int ring = 0; ring < walked.Count && placed < pool; ring++)
            {
                var widened = new List<string>();

                while (placed < pool)
                {
                    var next = NextInLine(walked[ring].Weights, plan, candidate =>
                    {
                        plan.TryGetValue(candidate, out int already);
                        return valueOf(candidate) + already < cap;
                    });
                    if (next == null) break;

                    plan.TryGetValue(next, out int carried);
                    plan[next] = carried + 1;
                    placed++;
                    if (ring > 0) widened.Add(nameOf(next));
                }

                if (widened.Count > 0)
                    CSLogger.Info($"NarrativeStep: every {kind} in the ring inside this one was at the cap " +
                                  $"of {cap}, so {widened.Count} point(s) went to {walked[ring].Why}: " +
                                  string.Join(", ", widened) + ".");
            }

            if (placed < pool)
                CSLogger.Warn($"NarrativeStep: {pool - placed} of the life's {pool} {kind} point(s) have " +
                              $"nowhere left to go; every {kind} the game registers is at the cap of {cap}.");

            return plan;
        }

        /// <summary>
        ///     Whichever candidate is furthest behind what its weight argues for.
        ///     Dividing the weight by what the candidate already holds is what
        ///     makes a run of single points converge on the weight ratios, so no
        ///     share has to be chosen anywhere; one with no room left is simply
        ///     never offered, and a null answer means every candidate is capped.
        /// </summary>
        private static T? NextInLine<T>(Dictionary<T, double> weights, Dictionary<T, int> given,
            Func<T, bool> hasRoom) where T : class
        {
            T? best = null;
            double bestShare = 0;

            foreach (var pair in weights)
            {
                if (pair.Value <= 0 || !hasRoom(pair.Key)) continue;

                given.TryGetValue(pair.Key, out int already);
                double share = pair.Value / (already + 1.0);
                if (best != null && share <= bestShare) continue;

                best = pair.Key;
                bestShare = share;
            }

            return best;
        }

        /// <summary>
        ///     What each lean argues an attribute is. Vigor is what a weapon is
        ///     swung with and Control what one is aimed with; Endurance is the
        ///     body that carries a long day of either, and it is the saddle and
        ///     the forge as well; Cunning reads open country and other people's
        ///     business; Social is how people are moved; Intelligence is what was
        ///     studied and what gets planned. The band is the whole weight, so an
        ///     attribute two leans both argue for gets both bands.
        /// </summary>
        private static Dictionary<CharacterAttribute, double> AttributeWeights(LifeProfile profile)
        {
            var argues = new (LifeProfile.Lean Lean, CharacterAttribute[] Attributes)[]
            {
                (LifeProfile.Lean.Martial,
                    new[] { DefaultCharacterAttributes.Vigor, DefaultCharacterAttributes.Endurance }),
                (LifeProfile.Lean.Commerce,
                    new[] { DefaultCharacterAttributes.Social, DefaultCharacterAttributes.Intelligence }),
                (LifeProfile.Lean.Standing,
                    new[] { DefaultCharacterAttributes.Social, DefaultCharacterAttributes.Intelligence }),
                (LifeProfile.Lean.Following,
                    new[] { DefaultCharacterAttributes.Social, DefaultCharacterAttributes.Cunning }),
                (LifeProfile.Lean.Wilds,
                    new[] { DefaultCharacterAttributes.Cunning, DefaultCharacterAttributes.Endurance }),
                (LifeProfile.Lean.Craft,
                    new[] { DefaultCharacterAttributes.Endurance, DefaultCharacterAttributes.Intelligence }),
                (LifeProfile.Lean.Sea,
                    new[] { DefaultCharacterAttributes.Endurance, DefaultCharacterAttributes.Control })
            };

            var weights = new Dictionary<CharacterAttribute, double>();

            foreach (var row in argues)
            {
                int band = profile.Band(row.Lean);
                if (band <= 0) continue;

                foreach (var attribute in row.Attributes)
                    AddWeight(weights, attribute, band);
            }

            // The hands are their own argument: a life that trained a bow is not
            // the life that trained a shield, and no lean can tell those apart.
            // The weight is how martial the life was, floored at the one that
            // training the weapon at all already counts for.
            AddWeight(weights, WeaponAttribute(profile.Weapon),
                Math.Max(1, profile.Band(LifeProfile.Lean.Martial)));

            return weights;
        }

        private static void AddWeight(Dictionary<CharacterAttribute, double> weights,
            CharacterAttribute? attribute, double amount)
        {
            if (attribute == null || amount <= 0) return;

            weights.TryGetValue(attribute, out double current);
            weights[attribute] = current + amount;
        }

        /// <summary>The attribute the weapon a life trained for is held with.</summary>
        private static CharacterAttribute? WeaponAttribute(LifeProfile.Trained weapon)
        {
            return weapon switch
            {
                LifeProfile.Trained.Blade or LifeProfile.Trained.Spear or LifeProfile.Trained.GreatWeapon
                    => DefaultCharacterAttributes.Vigor,
                LifeProfile.Trained.Bow or LifeProfile.Trained.Crossbow or LifeProfile.Trained.Thrown
                    => DefaultCharacterAttributes.Control,
                // A lance is a horse before it is a weapon, and the saddle is Endurance
                LifeProfile.Trained.Lance => DefaultCharacterAttributes.Endurance,
                _ => null
            };
        }

        /// <summary>
        ///     Exact attribute values written as targets, for the player and for
        ///     every generated hero the Start Editor sets them on: the delta brings
        ///     the live value to the target in whichever direction that is, so a
        ///     value is never added on top of what the hero already carries.
        /// </summary>
        public static void WriteAttributes(Hero hero, IReadOnlyDictionary<string, int> targets)
        {
            foreach (var pair in targets)
            {
                var attribute = Attributes.All.FirstOrDefault(a => a.StringId == pair.Key);
                if (attribute == null) continue;

                int delta = pair.Value - hero.GetAttributeValue(attribute);
                if (delta > 0)
                    hero.HeroDeveloper.AddAttribute(attribute, delta, false);
                else if (delta < 0)
                    hero.HeroDeveloper.RemoveAttribute(attribute, -delta);
                CSLogger.Info($"NarrativeStep: {hero.Name}'s attribute {attribute.StringId} set to {pair.Value}.");
            }
        }

        /// <summary>Exact focus written as targets, the way <see cref="WriteAttributes"/> writes attributes.</summary>
        public static void WriteFocus(Hero hero, IReadOnlyDictionary<string, int> targets)
        {
            foreach (var pair in targets)
            {
                var skill = Skills.All.FirstOrDefault(s => s.StringId == pair.Key);
                if (skill == null) continue;

                int current = hero.HeroDeveloper.GetFocus(skill);
                if (current > pair.Value)
                    hero.HeroDeveloper.RemoveFocus(skill, current - pair.Value);
                else if (current < pair.Value)
                    hero.HeroDeveloper.AddFocus(skill, pair.Value - current, false);
                CSLogger.Info($"NarrativeStep: {hero.Name}'s focus {skill.StringId} set to {pair.Value}.");
            }
        }

        /// <summary>
        ///     Player-set exact values override whatever vanilla finalization and
        ///     the narrative pass produced. Deltas bring the live value to the
        ///     target so nothing is double-counted.
        /// </summary>
        private static void ApplyCustomStats(Hero hero, CharacterCreationSession session)
        {
            WriteAttributes(hero, session.CustomAttributes);
            WriteFocus(hero, session.CustomFocus);

            // Exact skill levels win over both the narrative formula and vanilla
            foreach (var pair in session.CustomSkillLevels)
            {
                var skill = Skills.All.FirstOrDefault(s => s.StringId == pair.Key);
                if (skill == null) continue;

                hero.HeroDeveloper.SetInitialSkillLevel(skill, Math.Max(0, pair.Value));
                CSLogger.Info($"NarrativeStep: skill level {skill.StringId} set to {pair.Value}.");
            }
        }

        /// <summary>
        ///     The one formula for narrative skill levels; the equipment preview
        ///     estimates against this same computation.
        /// </summary>
        public static int ComputeInitialLevel(CharacterCreationSession session, SkillObject skill)
        {
            if (session.Mode != SetupMode.LifePath) return StorySkills.LevelFor(session, skill);

            LifePathCatalog.GetFocusTotals(session).TryGetValue(skill, out int points);
            return LifePathLevel(points);
        }

        /// <summary>
        ///     Cultured Start's sheet. The focus and attribute points its answers
        ///     declare, and the unspent points its age chapter declares, reach the
        ///     character through the game's own replay of those answers' args, so
        ///     this step writes only the skill levels those points stand for and
        ///     leaves both pools for the player, nothing granted here a second time.
        /// </summary>
        private static void ApplyLifePath(Hero hero, CharacterCreationSession session, bool vanillaSkillLevels)
        {
            if (vanillaSkillLevels)
            {
                CSLogger.Info("NarrativeStep: vanilla skill levels enabled; initial levels untouched.");
            }
            else
            {
                var totals = LifePathCatalog.GetFocusTotals(session);
                int focused = 0;
                foreach (var skill in Skills.All)
                {
                    totals.TryGetValue(skill, out int points);
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, LifePathLevel(points));
                    if (points > 0) focused++;
                }

                CSLogger.Info($"NarrativeStep: {focused} focused skills, all others at base {UnfocusedBaseLevel}.");
            }

            ApplyCustomStats(hero, session);
        }

        private static int LifePathLevel(int focusPoints) =>
            focusPoints > 0 ? FocusedBaseLevel + focusPoints * LevelPerFocusPoint : UnfocusedBaseLevel;

        /// <summary>
        ///     The skill value the hero will actually start with, honoring the
        ///     Vanilla Skill Levels toggle: with it on, the mod never sets levels,
        ///     so gear gating must read the hero's live vanilla value instead of
        ///     the narrative formula.
        /// </summary>
        /// <summary>
        ///     Only a name the player settled in this mod's own chapters is written.
        ///     A null means they never answered here, and whatever the game's own
        ///     naming screen took from them stands untouched.
        /// </summary>
        private static void ApplyChosenNames(StartContext context)
        {
            var session = context.Session;

            try
            {
                if (session.PlayerFirstName != null)
                {
                    var name = HeroNameGenerator.AsText(session.PlayerFirstName);
                    context.Hero.SetName(name, name);
                    CSLogger.Info($"NarrativeStep: the character was named {session.PlayerFirstName}.");
                }

                if (session.PlayerClanName != null && context.Hero.Clan != null)
                {
                    var name = HeroNameGenerator.AsText(session.PlayerClanName);
                    context.Hero.Clan.ChangeClanName(name, name);
                    CSLogger.Info($"NarrativeStep: the clan was named {session.PlayerClanName}.");
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("NarrativeStep: applying the chosen names failed.", ex);
            }
        }

        public static int ExpectedSkillValue(CharacterCreationSession session, SkillObject skill)
        {
            if (session.CustomSkillLevels.TryGetValue(skill.StringId, out int custom))
                return custom;

            if (MCM.Abstractions.Base.Global.GlobalSettings<Settings.CSSettings>.Instance?.VanillaSkillLevels == true)
                return Hero.MainHero?.GetSkillValue(skill) ?? 0;

            return ComputeInitialLevel(session, skill);
        }

    }
}
