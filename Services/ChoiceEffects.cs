using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What a choice will actually do to you, listed rather than explained.
    ///
    ///     Every option carries three texts: a thematic line on the button, prose
    ///     when it is selected, and this. The first two are written; this one is
    ///     DERIVED from the choice's own effects, so it cannot drift from them. An
    ///     authored effect line would go stale the first time a skill changed and
    ///     nobody would notice, which is the same defect as a string file that
    ///     disagrees with the code.
    ///
    ///     EVERY LINE IS A TANGIBLE CHANGE TO THE CHARACTER AND NOTHING ELSE, as
    ///     "Label: value", never a sentence. A line names one thing the option does
    ///     and what it does to it: coin, an item, a relation with a party named in
    ///     full, a family member, a trait, a year of age, a style written on the
    ///     character, or an answer this one shuts. Forbidden whatever the code can
    ///     derive: a percentage, a nearness, a standing, a resemblance, a ranking
    ///     of stations, anything phrased as a reading of the life rather than as a
    ///     change to the person, any line whose meaning has to be decoded out of
    ///     the fiction, and any restatement of the option's own prose. A
    ///     consequence that changes nothing about the character gets NO line.
    ///     Labels are few and reused, so the same fact is always found in the same
    ///     place: Age, Gold, Relation, Family, Allies, Ally, Item, Gear, Title,
    ///     Trait, Closes.
    ///
    ///     Derived is not the same as exact, and the panel used to stop at the
    ///     first. It rendered the catalog's own vocabulary: a purse, a saddle and
    ///     tack, the one who spoke up when it cost something. Those name
    ///     CATEGORIES, and the pipeline resolves each one to a real object with a
    ///     real name and a real number, so the player read one thing and was
    ///     handed another. Everything below names what the pipeline will actually
    ///     produce, and where the pipeline genuinely has not decided yet it says
    ///     what IS decided rather than falling back on the category word.
    ///
    ///     Every value here is bound in the strongest reading: a line may only
    ///     claim what is STILL TRUE AT THE END of the run. That is what rules out
    ///     any line about where the campaign opens: <see cref="ConsequenceKind.Place"/>
    ///     reaches <c>LocationStep</c> only through a village-or-not test taken over
    ///     the WHOLE list of places, and every start type is later asked where it
    ///     begins by <c>cs_location_menu</c> or <c>cs_settlement_select</c>, whose
    ///     answer the step prefers over anything the life's places say. A beginning
    ///     stated here would be a forecast, so the chapter that settles it states
    ///     it and this does not.
    ///
    ///     Derived from the choice's own effects, not from a catalog: a scene's
    ///     answer carries consequences and is read here, and a chapter's answer
    ///     carries an Action that writes the session, so the chapter states what
    ///     it wrote through <see cref="Declare"/>. Both halves are the same rule.
    ///     The half that is neither is the defect the warning names.
    /// </summary>
    public static class ChoiceEffects
    {
        // The panel is rebuilt on every frame the stage ticks, and naming a grant
        // walks the item database. One answer against one session state is one
        // answer, so it is worked out once and held until either changes.
        private static string _cachedKey = string.Empty;

        private static string _cachedBlock = string.Empty;

        /// <summary>
        ///     What the chapters say about their own options, by option id.
        ///
        ///     Half the guided route cannot be read out of a catalog. The means,
        ///     the warband, the gear and the ten scenario chapters carry an
        ///     <c>Action</c> that writes session fields directly, and a contract
        ///     rate, a cargo band, a policy set and a clan tier are not any of the
        ///     ten consequence kinds. Adding kinds the apply pipeline would have to
        ///     ignore buys nothing: the chapter already knows the exact number,
        ///     because it is the thing that applies it. So the chapter states it,
        ///     here, and the panel asks.
        ///
        ///     Each entry is asked at render time rather than at registration,
        ///     because every one of these numbers is read off a session that is
        ///     still being filled in. Nothing captures a session or a game object
        ///     for the same reason: a stale entry from an earlier creation run is
        ///     overwritten when its chapter is rebuilt, and one whose option no
        ///     longer exists can never be asked.
        /// </summary>
        private static readonly Dictionary<string, Func<string>> Declared =
            new Dictionary<string, Func<string>>(StringComparer.Ordinal);

        /// <summary>
        ///     The options that only navigate, and the whole set of them.
        ///
        ///     A route pick, the editor door and the confirm button change nothing
        ///     about the character, and this panel is a list of changes, so these
        ///     take no panel AT ALL rather than an empty one. They are named here,
        ///     in the one place the panel is composed, because the alternative is a
        ///     check at each call site and a sixth navigation option that lands
        ///     beside none of them.
        ///
        ///     Nothing warns for them either. A warning that fires on an option
        ///     working exactly as designed is what teaches a reader to skip the
        ///     warnings, and four of these five sit on the mode menu, which every
        ///     player on the Stable download walks through.
        /// </summary>
        private static readonly HashSet<string> OnlyNavigates =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "cs_mode_vanilla",
                "cs_mode_narrative",
                "cs_mode_custom",
                "cs_custom_open_editor",
                "cs_epilogue_begin"
            };

        /// <summary>
        ///     The effect block for a menu option id, from what its chapter
        ///     declared or from the scene catalog.
        ///
        ///     Every option that is an answer in a life must produce lines here.
        ///     The panel is the whole reason the route can be written as fiction: a
        ///     player who does not want to read a story never has to work anything
        ///     out, because the exact effect of the option they are on is stated
        ///     for them. An option that returns nothing silently breaks that
        ///     promise, so a miss is logged rather than swallowed.
        ///
        ///     The exception is an option that only navigates, which is answered
        ///     with nothing before anything is composed: an empty block leaves the
        ///     whole column hidden, which is what those five are owed, and nothing
        ///     is logged because nothing is wrong.
        /// </summary>
        public static string BlockFor(string? optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return string.Empty;
            if (OnlyNavigates.Contains(optionId!)) return string.Empty;

            string key = optionId + "|" + Stamp();
            if (string.Equals(key, _cachedKey, StringComparison.Ordinal)) return _cachedBlock;

            string block = Compose(optionId!);
            _cachedKey = key;
            _cachedBlock = block;
            return block;
        }

        /// <summary>
        ///     A chapter declaring what one of its own options does. Called while
        ///     the chapter builds that option, so the declaration and the pick are
        ///     written side by side and read the same values.
        /// </summary>
        public static void Declare(string? optionId, Func<string>? effect)
        {
            if (string.IsNullOrEmpty(optionId) || effect == null)
            {
                CSLogger.Warn("ChoiceEffects: a chapter declared an effect with no option or no text.");
                return;
            }

            Declared[optionId!] = effect;
        }

        /// <summary>One block out of the lines a chapter has to say, empty ones dropped.</summary>
        public static string Stated(params string?[] lines) =>
            string.Join(Environment.NewLine,
                lines.Where(line => !string.IsNullOrWhiteSpace(line)));

        /// <summary>
        ///     Every line of effect for a bare consequence list.
        ///
        ///     The worn grants are gathered rather than listed one by one, because
        ///     they share one qualifier: an answer handing over mail and a sword
        ///     printed it twice and read as a bug in the panel. They are composed
        ///     where the first of them stood, so the answer still reads in the
        ///     order it was written.
        /// </summary>
        public static IReadOnlyList<string> Lines(IReadOnlyList<ChoiceConsequence>? consequences)
        {
            var lines = new List<string>();
            if (consequences == null) return lines;

            var stores = Stores.Current();
            var worn = new List<string>();
            int wornAt = -1;

            foreach (var consequence in consequences)
            {
                if (consequence.Kind == ConsequenceKind.Item)
                {
                    var grant = Granted(consequence, stores);
                    if (grant == null) continue;

                    if (grant.Value.Whole != null)
                    {
                        lines.Add(grant.Value.Whole!);
                        continue;
                    }

                    if (!grant.Value.Worn)
                    {
                        lines.Add(Carried(grant.Value.What!));
                        continue;
                    }

                    if (wornAt < 0)
                    {
                        wornAt = lines.Count;
                        lines.Add(string.Empty);
                    }

                    worn.Add(grant.Value.What!);
                    continue;
                }

                var line = Describe(consequence);
                if (line != null) lines.Add(line);
            }

            if (wornAt >= 0) lines[wornAt] = Worn(worn);

            return lines;
        }

        /// <summary>The whole effect block as one string, a line each.</summary>
        public static string Block(IReadOnlyList<ChoiceConsequence>? consequences) =>
            string.Join(Environment.NewLine, Lines(consequences));

        private static string Compose(string optionId)
        {
            if (Declared.TryGetValue(optionId, out var declared))
            {
                try
                {
                    string stated = declared();
                    if (!string.IsNullOrWhiteSpace(stated)) return stated;

                    CSLogger.Warn($"ChoiceEffects: the chapter that owns {optionId} stated nothing, " +
                                  "so the player would be shown an empty panel.");
                }
                catch (Exception ex)
                {
                    CSLogger.Error($"ChoiceEffects: stating what {optionId} does failed.", ex);
                }
            }

            // Both routes, because an option id is unique across the two and a
            // panel asked for one of them while the session says the other is a
            // question asked before the route was settled, not a defect
            foreach (var scene in Application.GuidedRoute.EveryScene)
            foreach (var option in scene.Options)
                if (string.Equals(option.Id, optionId, StringComparison.Ordinal))
                    return Stated(Block(option.Consequences), ClosedStarts(optionId));

            CSLogger.Warn($"ChoiceEffects: no effect text for option {optionId}; " +
                          "the player would be shown an empty panel.");
            return string.Empty;
        }

        /// <summary>
        ///     What the panel depends on besides the option: which answers the life
        ///     has given, the stores the grants are resolved out of, and the
        ///     answers the chapters state their own numbers off.
        ///
        ///     The answers are named rather than counted, because the place line
        ///     reads WHICH of them were given: a player can walk back one scene,
        ///     answer it differently and come forward onto the same option, and the
        ///     count is the same on both sides of that while the life is not.
        ///
        ///     The second half is what the chapters added. A player can walk back,
        ///     name a different realm, and walk forward onto the same option, and
        ///     the contract, the garrison and the crime all read that realm; a
        ///     stamp that ignored it would hand back the line composed for the
        ///     realm before. Everything read here is a field the chapters read.
        /// </summary>
        private static string Stamp()
        {
            var stores = Stores.Current();
            string answered;
            try
            {
                answered = string.Join(">", SceneMenus.Answered.ChosenOptionIds);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ChoiceEffects: reading the answers for the panel failed: {ex.Message}");
                answered = "?";
            }

            return answered + "|" + (stores.Culture?.StringId ?? "-") + "|" + stores.Tier +
                   "|" + Answers(stores.Session);
        }

        private static string Answers(CharacterCreationSession? session)
        {
            if (session == null) return "-";

            try
            {
                return (int)session.SelectedStartType + "/" + session.EffectiveClanTier + "/" +
                       (session.SelectedKingdom?.StringId ?? "-") + "/" +
                       (session.SelectedSettlement?.StringId ?? "-") + "/" +
                       (session.SelectedLocation?.StringId ?? "-") + "/" +
                       session.StartingCompanions + "/" + FamilyAges.InPartyCount(session) + "/" +
                       (int)session.SelectedGold + "/" + (int)session.SelectedFounding + "/" +
                       (int)session.SelectedTroops + "/" + (session.CustomTroops?.ToString() ?? "-") + "/" +
                       (session.AdjustedAge?.ToString() ?? "-");
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ChoiceEffects: reading the answers the chapters quote failed: {ex.Message}");
                return "?";
            }
        }

        /// <summary>
        ///     One consequence as one labeled entry, naming what the pipeline will
        ///     do rather than the kind of thing it will do. Item is not here: its
        ///     entries are composed together by
        ///     <see cref="Lines(IReadOnlyList{ChoiceConsequence})"/>.
        ///
        ///     An authored Note is never rendered. A note is the option's own
        ///     fiction and the panel is the list beside it, so printing one here
        ///     would put the prose in both halves and state no change at all.
        /// </summary>
        private static string? Describe(ChoiceConsequence consequence)
        {
            if (consequence.Kind == ConsequenceKind.Gate) return Gate(consequence);

            switch (consequence.Kind)
            {
                // A place changes nothing about the character that can be stated
                // and still be true at the end of the run. Its only reach into the
                // pipeline is LocationStep's village-or-not test over the whole
                // list of places, and every start type is asked where it begins by
                // a later chapter whose answer that step prefers. The chapter
                // states the beginning; a line here would be a forecast
                case ConsequenceKind.Place:
                    return null;

                case ConsequenceKind.Trait:
                    return Trait(consequence);

                case ConsequenceKind.Debt:
                    return Amounted("{=CSR_Effect_Debt}Gold: -{AMOUNT} denars when the campaign opens",
                        consequence);

                case ConsequenceKind.Enmity:
                case ConsequenceKind.Goodwill:
                    return Relation(consequence);

                case ConsequenceKind.Ally:
                    return Ally(consequence);

                // Where the style lands is the tangible half of it: ConsequenceStep
                // composes the titles into the hero's encyclopedia text, and the
                // page keeps them. Naming the page is what stops "Title: a price"
                // reading as a riddle about what kind of change it is
                case ConsequenceKind.Title:
                    return Targeted(
                        "{=CSR_Effect_Title}Title: {TARGET}, written on your encyclopedia page",
                        consequence, LifeVocabulary.TitleText);

                case ConsequenceKind.LostYears:
                    return Amounted("{=CSR_Effect_Age}Age: +{AMOUNT}", consequence);

                case ConsequenceKind.Age:
                    return Amounted(
                        "{=CSR_Effect_AgeAt}Age: {AMOUNT} years old when the campaign opens", consequence);

                case ConsequenceKind.Household:
                    return House(consequence);

                default:
                    return null;
            }
        }

        /// <summary>
        ///     What a gate takes off the table, by the name of the answer it shuts.
        ///     "A road you will be offered later" is the player being asked to work
        ///     out the one thing the panel exists to tell them.
        ///
        ///     A gate shuts an ANSWER. What an answer shuts among the eight
        ///     BEGINNINGS is a separate line and a separate reading, because no
        ///     consequence declares a station closed: the station is the nearest of
        ///     the eight to the finished life, so a beginning is only ever closed
        ///     by arithmetic. <see cref="ClosedStarts"/> states that half.
        /// </summary>
        private static string? Gate(ChoiceConsequence consequence)
        {
            var title = ClosedAnswer(consequence.Target);
            if (title == null)
            {
                // "One answer offered later" is the riddle this panel exists to
                // end, so an unresolvable gate leaves the line off and warns
                CSLogger.Warn($"ChoiceEffects: gate target '{consequence.Target ?? "(none)"}' " +
                              "belongs to no scene, so the panel cannot name what it closes.");
                return null;
            }

            var text = new TextObject("{=CSR_Effect_GateNamed}Closes: {TARGET}");
            text.SetTextVariable("TARGET", title);
            return text.ToString();
        }

        private static string? ClosedAnswer(string? optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return null;

            foreach (var scene in Application.GuidedRoute.EveryScene)
            {
                var option = scene.Find(optionId!);
                if (option != null) return new TextObject(option.Title).ToString();
            }

            return null;
        }

        /// <summary>
        ///     The beginnings landing on this answer takes off the table.
        ///
        ///     Nothing in a scene rules a station out by declaring it: the station
        ///     is whichever of the eight the finished life stands nearest, so what
        ///     an answer closes is an arithmetic fact about the answers still to
        ///     come, and <see cref="Portrait.ClosedBy"/> settles it exactly rather
        ///     than guessing at it. This is not a forecast and the panel's promise is not
        ///     bent by it: a beginning named here is one no answer the player can
        ///     still give brings back, which is as true at the end of the run as it
        ///     is the moment they land on the option.
        ///
        ///     Nothing closed is the ordinary case and gets no line, the way a
        ///     consequence that changes nothing gets none.
        /// </summary>
        private static string? ClosedStarts(string optionId)
        {
            IReadOnlyList<StartType> closed;

            try
            {
                closed = Portrait.ClosedBy(Application.GuidedRoute.Scenes,
                    SceneMenus.Answered.ChosenOptionIds, optionId);
            }
            catch (Exception ex)
            {
                CSLogger.Error($"ChoiceEffects: working out what {optionId} closes failed, " +
                               "so the panel states no closure.", ex);
                return null;
            }

            if (closed.Count <= 0) return null;

            var named = new List<string>();
            foreach (var station in closed) named.Add(StationText(station));

            var text = new TextObject(closed.Count == 1
                ? "{=CSR_Effect_ClosesStart}Closes: the {TARGET} beginning"
                : "{=CSR_Effect_ClosesStarts}Closes: the {TARGET} beginnings");
            text.SetTextVariable("TARGET", Join(named));
            return text.ToString();
        }

        /// <summary>
        ///     A trait movement as the sheet will show it. The word for how far it
        ///     moved was a category standing in for a figure the sheet prints
        ///     anyway, and four options that each read "more valorous" are four
        ///     options the player cannot tell apart.
        /// </summary>
        private static string? Trait(ChoiceConsequence consequence)
        {
            string? id = consequence.Target;
            if (string.IsNullOrEmpty(id)) return null;

            int amount = consequence.Amount == 0 ? 1 : consequence.Amount;
            var trait = ResolveTrait(id!);
            if (trait != null)
            {
                var exact = new TextObject(
                    "{=CSR_Effect_TraitExact}Trait: {TARGET} {AMOUNT} (life total, {MIN} to {MAX})");
                exact.SetTextVariable("TARGET", trait.Name?.ToString() ?? id!);
                exact.SetTextVariable("AMOUNT", Signed(amount));
                exact.SetTextVariable("MIN", trait.MinValue);
                exact.SetTextVariable("MAX", trait.MaxValue);
                return exact.ToString();
            }

            var word = TraitText(id!);
            if (word == null)
            {
                CSLogger.Warn($"ChoiceEffects: no English for trait '{id}', so the line is left off the panel.");
                return null;
            }

            // The direction is carried by the word rather than by a sign, because
            // "Trait: -1 more merciful" says the opposite of what it does
            var plain = new TextObject(amount >= 0
                ? "{=CSR_Effect_TraitPlain}Trait: {AMOUNT} more {TARGET}"
                : "{=CSR_Effect_TraitLess}Trait: {AMOUNT} less {TARGET}");
            plain.SetTextVariable("AMOUNT", Math.Abs(amount));
            plain.SetTextVariable("TARGET", word);
            return plain.ToString();
        }

        /// <summary>
        ///     Who this answer puts in front of the player, said as what the
        ///     pipeline will really do with them.
        ///
        ///     Three outcomes and three sentences, because they are three different
        ///     things: blood becomes a relative in the family tree, one person
        ///     becomes a companion in the party, and the paid men become several.
        ///     The roster the companion step and the family step both read is what
        ///     decides which, so the panel cannot promise a companion for somebody
        ///     the pipeline builds as an aunt.
        ///
        ///     All three sentences OPEN with the person, so all three ask the
        ///     vocabulary for its opening form. The mid-sentence form the
        ///     encyclopedia page sets into "{ALLIES} left with him" is the same
        ///     phrase without a capital and, for the paid men, without the article
        ///     a count needs in front of it, which is why the second form is
        ///     written out beside the first rather than made here.
        /// </summary>
        private static string? Ally(ChoiceConsequence consequence)
        {
            var target = consequence.Target;
            var who = string.IsNullOrEmpty(target) ? null : LifeVocabulary.AllyOpening(target!);
            if (who == null)
            {
                CSLogger.Warn($"ChoiceEffects: no English for Ally target " +
                              $"'{target ?? "(none)"}', so the line is left off the panel.");
                return null;
            }

            var profile = CompanionGenerator.AllyFor(target!);
            if (profile == null)
            {
                CSLogger.Warn($"ChoiceEffects: no ally profile for '{target}', so nobody is built " +
                              "and the panel says nothing rather than promising a person.");
                return null;
            }

            if (profile.IsKin)
            {
                var kin = new TextObject(
                    "{=CSR_Effect_AllyKin}Family: {TARGET}, a living relative in your clan");
                kin.SetTextVariable("TARGET", who);
                return kin.ToString();
            }

            if (profile.Count > 1)
            {
                var band = new TextObject(
                    "{=CSR_Effect_AllyBand}Allies: {TARGET}, {COUNT} of them, as companions in your party");
                band.SetTextVariable("COUNT", profile.Count);
                band.SetTextVariable("TARGET", who);
                return band.ToString();
            }

            var one = new TextObject(profile.Friend
                ? "{=CSR_Effect_AllyFriend}Ally: {TARGET}, a companion in your party and a friend"
                : "{=CSR_Effect_AllyJoins}Ally: {TARGET}, a companion in your party");
            one.SetTextVariable("TARGET", who);
            return one.ToString();
        }

        /// <summary>
        ///     Who this answer settles in the character's house, said as the house
        ///     stands the moment the player lands on it.
        ///
        ///     The three household chapters compose the whole house and each of
        ///     them states its own third of it; this states the one thing THIS
        ///     answer decides, which is what the chapters read when the player
        ///     leaves a question to their life.
        ///
        ///     Two targets settle nothing at all on some lives: the last scene's
        ///     answer speaks to whichever parent the life left standing, and the
        ///     coin sent home finds both living only where the life has said
        ///     nothing about them. The vocabulary declines those on purpose and the
        ///     panel is one entry shorter, so they are asked apart from the ids
        ///     that have no English, which are a defect and still warn.
        /// </summary>
        private static string? House(ChoiceConsequence consequence)
        {
            var target = consequence.Target;
            var said = string.IsNullOrEmpty(target) ? null : LifeVocabulary.HouseText(target!);
            if (said != null) return Framed("{=CSR_Effect_Family}Family: {TARGET}", said);

            if (!LifeVocabulary.SettlesNothing(target))
                CSLogger.Warn($"ChoiceEffects: no English for Household target " +
                              $"'{target ?? "(none)"}', so the line is left off the panel.");

            return null;
        }

        /// <summary>
        ///     What an impression is actually worth, asked of the same two methods
        ///     that spend it: <see cref="ConsequenceStep.RelationAmount"/> for how
        ///     far one person moves, and <see cref="ConsequenceStep.RelationReach"/>
        ///     for how many of them there are. WHICH members is not settled here,
        ///     because they are the notables of a town the run has not reached yet
        ///     and, for the lords, a random pick at apply time.
        ///
        ///     "Up to" is a bound rather than a hedge: a town can hold fewer
        ///     merchants than the impression reaches, and then it reaches all of
        ///     them.
        /// </summary>
        private static string? Relation(ChoiceConsequence consequence)
        {
            var who = string.IsNullOrEmpty(consequence.Target) ? null : LifeVocabulary.WhoText(consequence.Target!);
            if (who == null)
            {
                CSLogger.Warn($"ChoiceEffects: no English for {consequence.Kind} target " +
                              $"'{consequence.Target ?? "(none)"}', so the line is left off the panel.");
                return null;
            }

            int steps = Math.Max(1, Math.Abs(consequence.Amount));
            int amount = ConsequenceStep.RelationAmount(steps) *
                         (consequence.Kind == ConsequenceKind.Enmity ? -1 : 1);

            var text = new TextObject(
                "{=CSR_Effect_Relation}Relation: {AMOUNT} with up to {COUNT} {TARGET}");
            text.SetTextVariable("AMOUNT", Signed(amount));
            text.SetTextVariable("COUNT", ConsequenceStep.RelationReach(steps));
            text.SetTextVariable("TARGET", who);
            return text.ToString();
        }

        /// <summary>
        ///     What ends up in the inventory or the purse, by its own name.
        ///
        ///     A purse is a figure the item database already holds, a craft kit is
        ///     two named items, and the mounts, the mail and the harness are each
        ///     the single best fit for this culture at this standing, so all of
        ///     those are named outright. A weapon is a random pick out of the few
        ///     nearest the standing and a trade good a random pick out of the whole
        ///     market list, so those say the kind and the standing, which is what
        ///     IS decided, and say plainly that the rest is not.
        /// </summary>
        private static Grant? Granted(ChoiceConsequence consequence, Stores stores)
        {
            string? target = consequence.Target;
            if (string.IsNullOrEmpty(target)) return null;

            int count = Math.Max(1, Math.Abs(consequence.Amount));

            try
            {
                var named = Named(target!, stores, count);
                if (named != null) return named;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ChoiceEffects: naming what '{target}' resolves to failed: {ex.Message}");
            }

            // The stores could not be read, which is a running game away rather
            // than a defect in the catalog. The category still beats an empty line
            var category = LifeVocabulary.ItemText(target!);
            if (category == null)
            {
                CSLogger.Warn($"ChoiceEffects: no English for Item target '{target}', " +
                              "so the line is left off the panel.");
                return null;
            }

            return Thing(target!, Times(category, count));
        }

        private static Grant? Named(string target, Stores stores, int count)
        {
            var session = stores.Session;
            if (session == null) return null;

            if (string.Equals(target, "coin_pouch", StringComparison.Ordinal))
            {
                int denars = ConsequenceStep.PurseValue();
                if (denars <= 0) return null;

                var gold = new TextObject("{=CSR_Effect_Gold}Gold: +{AMOUNT} denars");
                gold.SetTextVariable("AMOUNT", denars * count);
                return Grant.Said(gold.ToString());
            }

            if (string.Equals(target, "craft_tools", StringComparison.Ordinal))
            {
                var tools = DefaultItems.Tools;
                var stock = ConsequenceStep.CraftStock(stores.Culture, stores.Tier, tools);
                if (tools == null || stock == null) return null;

                var pair = new TextObject("{=CSR_Effect_And}{FIRST} and {LAST}");
                pair.SetTextVariable("FIRST", Name(tools));
                pair.SetTextVariable("LAST", Name(stock));
                return Thing(target, Times(pair.ToString(), count));
            }

            if (string.Equals(target, "trade_goods", StringComparison.Ordinal))
            {
                var goods = ArmorQuery.TradeGoodItems();
                if (goods.Count == 0) return null;
                if (goods.Count == 1) return Thing(target, Times(Name(goods[0]), count));

                var category = LifeVocabulary.ItemText(target);
                if (category == null) return null;

                var any = new TextObject("{=CSR_Effect_AnyOf}{TARGET}, one of the {COUNT} kinds the markets carry");
                any.SetTextVariable("TARGET", category);
                any.SetTextVariable("COUNT", goods.Count);
                return Thing(target, Times(any.ToString(), count));
            }

            var weaponClass = ConsequenceStep.WeaponClassFor(target);
            if (weaponClass != null)
            {
                var pool = GearQuery.QualifyingItems(weaponClass.Value, stores.Culture, stores.Tier, session);
                if (pool.Count == 0) return null;
                if (pool.Count == 1) return Thing(target, Times(Name(pool[0]), count));

                var category = LifeVocabulary.ItemText(target);
                if (category == null) return null;

                var picked = new TextObject(
                    "{=CSR_Effect_Picked}{TARGET}, drawn at random from those nearest tier {TIER}");
                picked.SetTextVariable("TARGET", category);
                picked.SetTextVariable("TIER", stores.Tier);
                return Thing(target, Times(picked.ToString(), count));
            }

            // Everything the pipeline answers differently on each asking has been
            // answered above by naming its pool, so what is left resolves to one
            // item and asking for it here cannot spend a draw the player will get
            var item = ConsequenceStep.ResolveItem(target, stores.Culture, stores.Tier, session);
            return item == null ? null : Thing(target, Times(Name(item), count));
        }

        private static string Name(ItemObject item) => item.Name?.ToString() ?? item.StringId;

        private static Grant Thing(string target, string what) =>
            Grant.Held(what, ConsequenceStep.IsWorn(target));

        /// <summary>Where a grant the character does not wear ends up.</summary>
        private static string Carried(string what) =>
            Framed("{=CSR_Effect_Carry}Item: {TARGET}, in your inventory", what);

        /// <summary>
        ///     Where the worn grants end up, which the player is owed as plainly as
        ///     what they are.
        ///
        ///     Armor and weapons are put ON when they beat what the start already
        ///     dressed the character in, so calling those inventory was wrong. Both
        ///     halves of that sentence are certainties: the piece arrives either
        ///     way, and only which of the two places it sits in is decided by the
        ///     comparison. Nothing here is a forecast of a later choice, and the
        ///     comparison is made piece by piece, which is why several of them said
        ///     together still say "each".
        /// </summary>
        private static string Worn(IReadOnlyList<string> things)
        {
            var text = new TextObject(things.Count == 1
                ? "{=CSR_Effect_Kept}Gear: {TARGET}, worn if it beats your starting gear, else in your baggage"
                : "{=CSR_Effect_KeptEach}Gear: {TARGET}, each worn if it beats your starting gear, else in your baggage");
            text.SetTextVariable("TARGET", Join(things));
            return text.ToString();
        }

        /// <summary>
        ///     Several of one thing. The count follows the thing rather than
        ///     leading it, because everything this is given carries its own article
        ///     and "2 x a purse" reads as a price.
        /// </summary>
        private static string Times(string what, int count)
        {
            if (count <= 1) return what;

            var text = new TextObject("{=CSR_Effect_Times}{TARGET} x{AMOUNT}");
            text.SetTextVariable("AMOUNT", count);
            text.SetTextVariable("TARGET", what);
            return text.ToString();
        }

        /// <summary>
        ///     One Item consequence resolved: either a whole line with a frame of
        ///     its own, as the purse has, or the thing itself and whether it is
        ///     worn, which the caller frames once for all of them.
        /// </summary>
        private readonly struct Grant
        {
            private Grant(string? whole, string? what, bool worn)
            {
                Whole = whole;
                What = what;
                Worn = worn;
            }

            public string? Whole { get; }

            public string? What { get; }

            public bool Worn { get; }

            public static Grant Said(string line) => new Grant(line, null, false);

            public static Grant Held(string what, bool worn) => new Grant(null, what, worn);
        }

        private static string Signed(int amount) =>
            (amount >= 0 ? "+" : "-") +
            Math.Abs(amount).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        ///     A line whose subject is a catalog id, rendered into English first.
        ///
        ///     An id with no English is left OUT rather than substituted. A panel
        ///     that reads "You keep coin_pouch" is worse than one line shorter: it
        ///     is the promise that the player never has to work anything out,
        ///     broken in front of them. The warning is what says the mapping needs
        ///     a line added.
        /// </summary>
        private static string? Targeted(string key, ChoiceConsequence consequence,
            Func<string, string?> english)
        {
            var target = consequence.Target;
            var rendered = string.IsNullOrEmpty(target) ? null : english(target!);
            if (rendered == null)
            {
                CSLogger.Warn($"ChoiceEffects: no English for {consequence.Kind} target " +
                              $"'{target ?? "(none)"}', so the line is left off the panel.");
                return null;
            }

            var text = new TextObject(key);
            text.SetTextVariable("TARGET", rendered);
            text.SetTextVariable("AMOUNT", Math.Abs(consequence.Amount));
            return text.ToString();
        }

        /// <summary>One entry whose value is already English.</summary>
        private static string Framed(string key, string value)
        {
            var text = new TextObject(key);
            text.SetTextVariable("TARGET", value);
            return text.ToString();
        }

        private static string Amounted(string key, ChoiceConsequence consequence)
        {
            var text = new TextObject(key);
            text.SetTextVariable("AMOUNT", Math.Abs(consequence.Amount));
            return text.ToString();
        }

        /// <summary>
        ///     The trait vocabulary, shared with the apply pipeline so the panel
        ///     and the grant can never read one id as two different traits.
        /// </summary>
        internal static TraitObject? ResolveTrait(string id)
        {
            try
            {
                return id switch
                {
                    "Mercy" => DefaultTraits.Mercy,
                    "Valor" => DefaultTraits.Valor,
                    "Honor" => DefaultTraits.Honor,
                    "Generosity" => DefaultTraits.Generosity,
                    "Calculating" => DefaultTraits.Calculating,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ChoiceEffects: resolving trait '{id}' failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     The trait as something a person is counted, for the one case where
        ///     the game's own trait object cannot be reached.
        /// </summary>
        private static string? TraitText(string id)
        {
            string? key = id switch
            {
                "Calculating" => "{=CSR_Effect_Trait_Calculating}calculating",
                "Generosity" => "{=CSR_Effect_Trait_Generosity}generous",
                "Honor" => "{=CSR_Effect_Trait_Honor}honorable",
                "Mercy" => "{=CSR_Effect_Trait_Mercy}merciful",
                "Valor" => "{=CSR_Effect_Trait_Valor}valorous",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     The eight beginnings, bare, because they sit in a ranked list rather
        ///     than in a sentence and "a landed vassal, then a monarch" reads as two
        ///     people the life met.
        ///
        ///     Internal because the standing chapter names the station the run will
        ///     really open in, and a second vocabulary there would let the ranked
        ///     list and the verdict call one beginning two different things.
        /// </summary>
        internal static string StationText(StartType station)
        {
            string key = station switch
            {
                StartType.Monarch => "{=CSR_Effect_Station_Monarch}monarch",
                StartType.LandedVassal => "{=CSR_Effect_Station_Landed}landed vassal",
                StartType.LandlessVassal => "{=CSR_Effect_Station_Landless}landless vassal",
                StartType.Mercenary => "{=CSR_Effect_Station_Mercenary}mercenary",
                StartType.Outlaw => "{=CSR_Effect_Station_Outlaw}outlaw",
                StartType.CaravanMaster => "{=CSR_Effect_Station_Caravan}caravan master",
                StartType.RebelClan => "{=CSR_Effect_Station_Rebel}rebel clan",
                _ => "{=CSR_Effect_Station_Commoner}commoner"
            };

            return Localized(key);
        }

        private static string Localized(string template) => new TextObject(template).ToString();

        /// <summary>A readable list: "a", "a and b", "a, b and c".</summary>
        private static string Join(IReadOnlyList<string> parts)
        {
            if (parts.Count == 0) return string.Empty;
            if (parts.Count == 1) return parts[0];

            var pair = new TextObject("{=CSR_Effect_And}{FIRST} and {LAST}");
            pair.SetTextVariable("FIRST", string.Join(", ", parts.Take(parts.Count - 1)));
            pair.SetTextVariable("LAST", parts[parts.Count - 1]);
            return pair.ToString();
        }

        /// <summary>
        ///     What the run would dress this character out of right now: their
        ///     culture and the standing their clan tier and bearing come to. Both
        ///     move as the run goes on, and the panel is read while they are still
        ///     moving, so this is the resolution as the life stands rather than a
        ///     promise about the one the pipeline will make at the end.
        /// </summary>
        private readonly struct Stores
        {
            private Stores(CharacterCreationSession? session, CultureObject? culture, int tier)
            {
                Session = session;
                Culture = culture;
                Tier = tier;
            }

            public CharacterCreationSession? Session { get; }

            public CultureObject? Culture { get; }

            public int Tier { get; }

            public static Stores Current()
            {
                try
                {
                    var session = CreationSession.Current;
                    if (session == null) return new Stores(null, null, 0);

                    return new Stores(session, session.SelectedCulture,
                        EquipmentStep.EffectiveTier(session, GlobalSettings<Settings.CSSettings>.Instance));
                }
                catch (Exception ex)
                {
                    CSLogger.Warn($"ChoiceEffects: reading the stores for the panel failed: {ex.Message}");
                    return new Stores(null, null, 0);
                }
            }
        }
    }
}
