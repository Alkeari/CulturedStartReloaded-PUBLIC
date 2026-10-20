using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     The paragraph a hero carries on the encyclopedia page. The base game is
    ///     specific about who gets one: of 397 lords in its roster only 27 carry
    ///     any prose, and every one of those is the head of a clan or the ruler of
    ///     a kingdom. Spouses, children and siblings carry none.
    ///
    ///     So this is written for clan heads and nobody else. Giving a generated
    ///     cousin a biography would make the clan read as authored, which is the
    ///     opposite of the point, and the same reason no clan here gets a motto:
    ///     no clan in the base game has one.
    ///
    ///     Third person, because vanilla's is. A scene's answer is a verdict on a
    ///     moment and fits no sentence frame at all, so the paragraph composes
    ///     from what the life left behind rather than from the answers.
    /// </summary>
    public static class HeroLore
    {
        /// <summary>
        ///     How many entries of one kind a sentence will carry. A life can end
        ///     up holding nine things, and an encyclopedia sentence that lists all
        ///     nine stops being prose; the rest are still on the character, they
        ///     are just not read back here.
        /// </summary>
        private const int MostListed = 3;

        /// <summary>
        ///     The player's own page, composed from the life they chose. An editor
        ///     start told no life, so it falls back to culture and station, which
        ///     is still more than a blank page.
        /// </summary>
        public static TextObject? ComposeForPlayer(CharacterCreationSession? session, Hero hero)
        {
            if (session == null) return null;

            try
            {
                var story = new StringBuilder();

                ComposeFromLife(story, hero, session);

                story.Append(' ').Append(Line(StationLine(session.SelectedStartType), hero, session));

                var text = story.ToString();
                CSLogger.Info($"HeroLore: composed {text.Length} characters for {hero.Name}.");
                return new TextObject("{=!}" + text);
            }
            catch (Exception ex)
            {
                CSLogger.Error("HeroLore: composing the player's history failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     The scene route, composed from what the life left behind rather than
        ///     from the answers themselves.
        ///
        ///     A scene's answer is a verdict on a moment ("That You Were the One
        ///     They Sent"), not a noun phrase, so there is no frame it can be
        ///     dropped into. The outcome is the same life stated as structured
        ///     values, and those DO make sentences: who thinks well of the
        ///     character and who does not, what the name carries, who left with
        ///     them, what they still hold, and what they owe.
        ///
        ///     Lost years are deliberately not read back here. They are already
        ///     visible on the character as age, and saying them twice would make a
        ///     short paragraph longer for nothing.
        /// </summary>
        private static void ComposeFromLife(StringBuilder story, Hero hero, CharacterCreationSession session)
        {
            var life = GuidedRun.Outcome();

            story.Append(Line("{=CSR_HeroLore_BornPlain}{NAME} is of {CULTURE} stock.", hero, session));

            var titles = Listed(life.Titles, LifeVocabulary.TitleText);
            if (titles != null)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_Called}{POSSESSIVE_CAP} name carries {TITLES}.",
                    hero, session, ("TITLES", titles)));

            AppendStanding(story, hero, session, life);

            var allies = Listed(life.Allies, LifeVocabulary.AllyText);
            if (allies != null)
                story.Append(' ').Append(Line("{=CSR_HeroLore_Followed}{SUBJECT} left with {ALLIES}.",
                    hero, session, ("ALLIES", allies)));

            var items = Listed(life.Items, LifeVocabulary.ItemText);
            if (items != null)
                story.Append(' ').Append(Line("{=CSR_HeroLore_Carries}{SUBJECT} still has {ITEMS}.",
                    hero, session, ("ITEMS", items)));

            var places = Listed(life.Places, LifeVocabulary.PlaceText);
            if (places != null)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_Ground}{POSSESSIVE_CAP} story is tied to {PLACES}.",
                    hero, session, ("PLACES", places)));

            if (life.Debt > 0)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_Owes}{SUBJECT} owes {DEBT} denars, and it will be asked for.",
                    hero, session, ("DEBT", life.Debt.ToString())));
        }

        /// <summary>
        ///     Who thinks what. Ordered by the relation enum rather than by however
        ///     the dictionary happened to fill, so the same life reads the same way
        ///     every time it is composed.
        /// </summary>
        private static void AppendStanding(StringBuilder story, Hero hero,
            CharacterCreationSession session, SceneOutcome life)
        {
            var ordered = life.Relations.OrderBy(pair => (int)pair.Key).ToList();
            var well = Listed(ordered.Where(pair => pair.Value > 0).Select(pair => pair.Key), Who);
            var badly = Listed(ordered.Where(pair => pair.Value < 0).Select(pair => pair.Key), Who);

            if (well != null && badly != null)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_ThoughtOfBoth}{GOODWILL} think well of {OBJECT}, and {ENMITY} do not.",
                    hero, session, ("GOODWILL", well), ("ENMITY", badly)));
            else if (well != null)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_ThoughtWell}{GOODWILL} think well of {OBJECT}.",
                    hero, session, ("GOODWILL", well)));
            else if (badly != null)
                story.Append(' ').Append(Line(
                    "{=CSR_HeroLore_ThoughtBadly}{ENMITY} have not forgotten {OBJECT}.",
                    hero, session, ("ENMITY", badly)));
        }

        /// <summary>
        ///     The people a relation reaches, named out of the one vocabulary.
        ///     That vocabulary is keyed by the catalog id, as the other four
        ///     tables are, so a relation the outcome has already sorted has to be
        ///     said back as the id it came from. SceneOutcome owns the outward map
        ///     and this is the only return trip anything makes.
        /// </summary>
        private static string? Who(RelationEffect who) => LifeVocabulary.WhoText(who switch
        {
            RelationEffect.CultureLords => "culture_lords",
            RelationEffect.TownMerchants => "town_merchants",
            RelationEffect.TownGangLeaders => "town_gang_leaders",
            RelationEffect.VillageHeadmen => "village_headmen",
            _ => string.Empty
        });

        /// <summary>
        ///     A readable list of whatever English the mapping gives back. An id
        ///     with no English is left OUT rather than printed: a code word in an
        ///     encyclopedia entry is worse than a shorter sentence, and the warning
        ///     is what says the mapping needs a line added.
        /// </summary>
        private static string? Listed<T>(IEnumerable<T> values, Func<T, string?> english)
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                if (parts.Count >= MostListed) break;

                var text = english(value);
                if (text == null)
                {
                    CSLogger.Warn($"HeroLore: no English for '{value}', so it is left off the page.");
                    continue;
                }

                if (!parts.Contains(text)) parts.Add(text);
            }

            return parts.Count == 0 ? null : Join(parts);
        }

        /// <summary>A readable list: "a", "a and b", "a, b and c".</summary>
        private static string Join(IReadOnlyList<string> parts)
        {
            if (parts.Count == 1) return parts[0];

            var pair = new TextObject("{=CSR_Effect_And}{FIRST} and {LAST}");
            pair.SetTextVariable("FIRST", string.Join(", ", parts.Take(parts.Count - 1)));
            pair.SetTextVariable("LAST", parts[parts.Count - 1]);
            return pair.ToString();
        }

        /// <summary>
        ///     A sworn house's head. One sentence, in the register vanilla uses for
        ///     the clan founders that carry any text at all.
        /// </summary>
        public static TextObject? ComposeForVassal(Hero leader, Settlement? seat, bool rebel)
        {
            try
            {
                var template = rebel
                    ? CSRandom.Pick(RebelLines)
                    : CSRandom.Pick(SwornLines);
                if (template == null) return null;

                var text = new TextObject(template);
                text.SetTextVariable("NAME", leader.Name?.ToString() ?? string.Empty);
                text.SetTextVariable("CLAN", leader.Clan?.Name?.ToString() ?? string.Empty);
                text.SetTextVariable("SEAT", seat?.Name?.ToString() ?? string.Empty);
                return new TextObject("{=!}" + text);
            }
            catch (Exception ex)
            {
                CSLogger.Error("HeroLore: composing a sworn house's history failed.", ex);
                return null;
            }
        }

        private static readonly string[] SwornLines =
        {
            "{=CSR_HeroLore_Sworn1}{NAME} heads the {CLAN}, raised to lordship at the founding and seated at {SEAT}. Whether the honor was earned or merely timely is argued in other halls.",
            "{=CSR_HeroLore_Sworn2}{NAME} of the {CLAN} swore early and was rewarded with {SEAT}, and has never let the crown forget which of those came first.",
            "{=CSR_HeroLore_Sworn3}The {CLAN} hold {SEAT} on an oath sworn at the proclamation. {NAME} keeps it, so far, better than some expected."
        };

        private static readonly string[] RebelLines =
        {
            "{=CSR_HeroLore_Rebel1}{NAME} brought the {CLAN} into the rising, wagering a modest name on an immodest cause.",
            "{=CSR_HeroLore_Rebel2}The {CLAN} threw in with the rebellion under {NAME}, having rather less to lose than those who did not.",
            "{=CSR_HeroLore_Rebel3}{NAME} of the {CLAN} was among the first to declare, which will read as courage or as recklessness depending on how it ends."
        };

        private static string StationLine(StartType start)
        {
            return start switch
            {
                StartType.Monarch => "{=CSR_HeroLore_Station_Monarch}{SUBJECT} now wears a crown of {POSSESSIVE} own raising.",
                StartType.RebelClan => "{=CSR_HeroLore_Station_Rebel}{SUBJECT} now leads a clan in open revolt.",
                StartType.LandedVassal => "{=CSR_HeroLore_Station_Landed}{SUBJECT} now holds land as a sworn lord.",
                StartType.LandlessVassal => "{=CSR_HeroLore_Station_Landless}{SUBJECT} now serves a crown, landless and waiting on one.",
                StartType.Mercenary => "{=CSR_HeroLore_Station_Merc}{SUBJECT} now sells a company's swords by the season.",
                StartType.Outlaw => "{=CSR_HeroLore_Station_Outlaw}{SUBJECT} now keeps to the roads, and the roads keep {POSSESSIVE} name.",
                StartType.CaravanMaster => "{=CSR_HeroLore_Station_Caravan}{SUBJECT} now moves goods across the map for a living.",
                _ => "{=CSR_HeroLore_Station_Commoner}{SUBJECT} now rides out with little more than what {SUBJECT_LOWER} can carry."
            };
        }

        private static string Line(string template, Hero hero, CharacterCreationSession session,
            params (string Key, string Value)[] variables)
        {
            bool female = hero.IsFemale;
            var text = new TextObject(template);
            text.SetTextVariable("NAME", hero.Name?.ToString() ?? string.Empty);
            text.SetTextVariable("SUBJECT", Localized(female
                ? "{=CSR_HeroLore_SheCap}She"
                : "{=CSR_HeroLore_HeCap}He"));
            text.SetTextVariable("SUBJECT_LOWER", Localized(female
                ? "{=CSR_HeroLore_She}she"
                : "{=CSR_HeroLore_He}he"));
            text.SetTextVariable("OBJECT", Localized(female
                ? "{=CSR_HeroLore_Her}her"
                : "{=CSR_HeroLore_Him}him"));
            text.SetTextVariable("POSSESSIVE", Localized(female
                ? "{=CSR_HeroLore_Her}her"
                : "{=CSR_HeroLore_His}his"));
            text.SetTextVariable("POSSESSIVE_CAP", Localized(female
                ? "{=CSR_HeroLore_HerCap}Her"
                : "{=CSR_HeroLore_HisCap}His"));
            text.SetTextVariable("CULTURE",
                (session.SelectedCulture ?? hero.Culture)?.Name?.ToString() ?? string.Empty);

            foreach (var (key, value) in variables) text.SetTextVariable(key, value);
            return text.ToString();
        }

        private static string Localized(string template)
        {
            return new TextObject(template).ToString();
        }
    }

    /// <summary>
    ///     The English for every id a life can leave behind.
    ///
    ///     A guided run hands the pipeline code words: "coin_pouch",
    ///     "family_seat", "the_paid_men". Two surfaces read the same life back to
    ///     the player, the effect panel while an answer is being weighed and the
    ///     encyclopedia page once the campaign is open, and each used to carry its
    ///     own copy of these five tables. Two spellings of one vocabulary is two
    ///     chances to name a thing differently, and nothing in a build says which
    ///     of them is the wrong one.
    ///
    ///     What the two surfaces genuinely do not share is grammar: the panel
    ///     states an effect ("In your inventory: a purse.") and the page describes
    ///     a person ("He still has a purse."). So the sentences stay with whoever
    ///     is speaking and only the naming lives here, which is why every entry is
    ///     a bare noun phrase either of them can set into its own frame.
    ///
    ///     An id with no English gives back null rather than the id, because a
    ///     code word in front of the player is worse than a shorter sentence. The
    ///     caller leaves the line out and logs which id needs one.
    /// </summary>
    public static class LifeVocabulary
    {
        /// <summary>What the life put in the player's hands.</summary>
        public static string? ItemText(string id)
        {
            string? key = id switch
            {
                "boarding_axe" => "{=CSR_HeroLore_Item_BoardingAxe}a boarding axe",
                "coin_pouch" => "{=CSR_HeroLore_Item_Purse}a purse",
                "craft_tools" => "{=CSR_HeroLore_Item_Tools}a set of tools with a load of raw material",
                "family_sword" => "{=CSR_HeroLore_Item_FamilySword}the family sword",
                "hunting_bow" => "{=CSR_HeroLore_Item_Bow}a hunting bow",
                "lance" => "{=CSR_HeroLore_Item_Lance}a lance",
                "mail_hauberk" => "{=CSR_HeroLore_Item_Mail}a mail hauberk",
                "one_handed_sword" => "{=CSR_HeroLore_Item_Sword}a sword",
                "pack_mule" => "{=CSR_HeroLore_Item_Mule}a pack mule",
                "riding_horse" => "{=CSR_HeroLore_Item_Horse}a riding horse",
                "riding_tack" => "{=CSR_HeroLore_Item_Tack}a saddle and tack",
                "spear" => "{=CSR_HeroLore_Item_Spear}a spear",
                "trade_goods" => "{=CSR_HeroLore_Item_Goods}goods to sell",
                "two_handed_axe" => "{=CSR_HeroLore_Item_Axe}a two-handed axe",
                "war_horse" => "{=CSR_HeroLore_Item_WarHorse}a warhorse",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>Ground the life gave a claim on or a reason to be near.</summary>
        public static string? PlaceText(string id)
        {
            string? key = id switch
            {
                "family_seat" => "{=CSR_HeroLore_Place_Seat}the family seat",
                "granted_holding" => "{=CSR_HeroLore_Place_Granted}a holding granted rather than inherited",
                "home_village" => "{=CSR_HeroLore_Place_Village}the village that raised them",
                "home_woodland" => "{=CSR_HeroLore_Place_Woodland}the woods above that village",
                "home_workshop" => "{=CSR_HeroLore_Place_Workshop}a workshop with one bench in it",
                "nearest_castle" => "{=CSR_HeroLore_Place_Castle}the castle that watches that ground",
                "nearest_hideout" => "{=CSR_HeroLore_Place_Hideout}a hideout nobody puts on a map",
                "the_open_water" => "{=CSR_HeroLore_Place_Water}the open water",
                "nearest_town" => "{=CSR_HeroLore_Place_Town}the town down the road from it",
                "open_country" => "{=CSR_HeroLore_Place_Country}the open country between all of it",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     The same place, carrying how much of the life went into it wherever
        ///     the reading measures that rather than taking the place as a yes or a
        ///     no.
        ///
        ///     Two answers of the port scene both tie a life to the open water and
        ///     are not the same answer: one is a season every year and one is a run
        ///     nobody else would take, and the seat on the water is gated on the
        ///     difference. Printed as one identical line they asked the player to
        ///     guess, and a player playing for a fleet, taking a water answer at
        ///     every chance and reading the standings everywhere else, reached one
        ///     16 times in a hundred over 2,000 lives. Reading how much water the
        ///     answer is, the same player reaches it every time.
        /// </summary>
        public static string? PlaceText(string id, int amount)
        {
            var named = PlaceText(id);
            var measured = named == null ? null : WaterText(id, amount);
            if (measured == null) return named;

            var text = new TextObject("{=CSR_HeroLore_Place_Measured}{TARGET} ({AMOUNT})");
            text.SetTextVariable("TARGET", named);
            text.SetTextVariable("AMOUNT", measured);
            return text.ToString();
        }

        /// <summary>
        ///     How much water an answer is, said in the degrees' own measures so the
        ///     line cannot call a voyage enough for a thing the gate refuses. The
        ///     open water is the only place the reading takes an amount from, so it
        ///     is the only one qualified: a figure beside any other place would be a
        ///     number nothing downstream reads.
        /// </summary>
        private static string? WaterText(string id, int amount)
        {
            if (!string.Equals(id, "the_open_water", StringComparison.Ordinal)) return null;

            if (amount >= Application.Scenarios.SeaDegrees.ALifeOnIt)
                return Localized("{=CSR_HeroLore_Water_Life}a life on it");

            return Localized(amount >= Application.Scenarios.SeaDegrees.OneVoyage
                ? "{=CSR_HeroLore_Water_Voyage}a voyage"
                : "{=CSR_HeroLore_Water_Passage}a passage");
        }

        /// <summary>Who left with the player.</summary>
        public static string? AllyText(string id)
        {
            string? key = id switch
            {
                "the_apprentice" => "{=CSR_HeroLore_Ally_Apprentice}an apprentice with nowhere else to be",
                "the_one_nobody_sat_with" => "{=CSR_HeroLore_Ally_Unwanted}the one nobody else would sit with",
                "the_one_who_spoke_for_you" => "{=CSR_HeroLore_Ally_Spoke}the one who spoke up when it cost something",
                "the_one_you_carried" => "{=CSR_HeroLore_Ally_Carried}the one carried off the field",
                "the_paid_men" => "{=CSR_HeroLore_Ally_Paid}men who were paid to come",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     The same five people, as a sentence can open on them.
        ///
        ///     Every other entry here is a bare noun phrase because its two readers
        ///     set it into the middle of their own frame. An ally is the one thing
        ///     the effect panel puts FIRST in a sentence ("The one carried off the
        ///     field joins your party"), so the mid-sentence form led a line with a
        ///     lowercase word while every other line in the product is capitalized.
        ///     The difference is not only the capital: a count in front of the paid
        ///     men needs the article the mid-sentence form leaves off, and "2 of men
        ///     who were paid to come" is no sentence either. Two forms that differ
        ///     by more than a case cannot be made from one, so the second is written
        ///     out beside the first rather than derived at the call site.
        /// </summary>
        public static string? AllyOpening(string id)
        {
            string? key = id switch
            {
                "the_apprentice" => "{=CSR_HeroLore_AllyOpen_Apprentice}An apprentice with nowhere else to be",
                "the_one_nobody_sat_with" => "{=CSR_HeroLore_AllyOpen_Unwanted}The one nobody else would sit with",
                "the_one_who_spoke_for_you" => "{=CSR_HeroLore_AllyOpen_Spoke}The one who spoke up when it cost something",
                "the_one_you_carried" => "{=CSR_HeroLore_AllyOpen_Carried}The one carried off the field",
                "the_paid_men" => "{=CSR_HeroLore_AllyOpen_Paid}The men who were paid to come",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     What a title means, as something a name can be said to carry. The
        ///     ids are code words; the scene that grants each one is what decides
        ///     the English, which is why this is written out rather than derived
        ///     from the id's own words.
        /// </summary>
        public static string? TitleText(string id)
        {
            string? key = id switch
            {
                "by_the_trade" => "{=CSR_HeroLore_Title_Trade}a trade said where a family should be",
                "holder_of_the_writ" => "{=CSR_HeroLore_Title_Writ}a writ nobody has revoked",
                "oathbreaker" => "{=CSR_HeroLore_Title_Oathbreaker}an oath taken and broken",
                "of_the_house" => "{=CSR_HeroLore_Title_House}a house's name standing in front of it",
                "of_the_place" => "{=CSR_HeroLore_Title_Place}the name of the place it came from",
                "sworn_of_the_house" => "{=CSR_HeroLore_Title_Sworn}an oath sworn to a great house",
                "the_old" => "{=CSR_HeroLore_Title_Old}the word old, said far too early",
                "the_one_who_asked" => "{=CSR_HeroLore_Title_Asked}a question a lord could not answer",
                "wanted" => "{=CSR_HeroLore_Title_Wanted}a price",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     The people an impression reaches. Keyed by the catalog's own group
        ///     id, as the tables above are; SceneOutcome turns those same ids
        ///     into RelationEffect for the step that applies them.
        ///
        ///     Two words each, because the panel counts them: "up to 2 gang
        ///     notables" is a figure a reader takes in at a glance where "up to 2
        ///     of the gangs of the back streets" is a clause they have to parse.
        ///     The encyclopedia page sets the same phrase into its own sentence and
        ///     reads no worse for the shorter one.
        /// </summary>
        public static string? WhoText(string id)
        {
            string? key = id switch
            {
                "culture_lords" => "{=CSR_HeroLore_Who_Lords}realm lords",
                "town_gang_leaders" => "{=CSR_HeroLore_Who_Gangs}gang notables",
                "town_merchants" => "{=CSR_HeroLore_Who_Merchants}town merchants",
                "village_headmen" => "{=CSR_HeroLore_Who_Headmen}village headmen",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     Who the life puts in the character's household, or takes out of it,
        ///     as the value of one "Family:" entry rather than as a sentence.
        ///
        ///     These used to be whole sentences, on the reasoning that a grave, a
        ///     sibling and a spouse are three different statements no one frame
        ///     holds. One frame does hold them, and it is the label: what is owed
        ///     is who the person is and where they stand, which every entry says in
        ///     six words or fewer. Nothing else reads this table, so the panel's
        ///     frame is the only one it has to fit.
        /// </summary>
        public static string? HouseText(string id)
        {
            // The one fact that stands for two. Which of them this run settled on
            // was decided when the answer was given, so the sentence names the
            // person the player is getting rather than hedging between a brother
            // and a sister
            if (string.Equals(id, StorySibling.Declares, StringComparison.Ordinal))
                id = StorySibling.Of(CreationSession.Current).House;

            // The two facts read off the life rather than carried by the answer.
            // The last scene puts the parents in the ground by then and names
            // whoever is left; the coin sent home finds them living, and only on a
            // life that has said nothing about them. Either one can settle nobody,
            // and then the panel is one entry shorter rather than wrong
            if (string.Equals(id, StoryGraves.Declares, StringComparison.Ordinal))
            {
                string? settled = StoryGraves.Of(SceneMenus.Answered);
                if (settled == null) return null;

                id = settled;
            }

            if (string.Equals(id, StoryHome.Declares, StringComparison.Ordinal))
            {
                string? settled = StoryHome.Of(SceneMenus.Answered);
                if (settled == null) return null;

                id = settled;
            }

            string? key = id switch
            {
                "a_brother" => "{=CSR_HeroLore_House_Brother}a brother, grown and in your clan",
                "a_sister" => "{=CSR_HeroLore_House_Sister}a sister, grown and in your clan",
                "a_spouse" => "{=CSR_HeroLore_House_Spouse}a spouse, married to you and in your clan",
                "both_parents_buried" => "{=CSR_HeroLore_House_BothBuried}both parents dead, in your family tree",
                "both_parents_living" => "{=CSR_HeroLore_House_BothLiving}both parents alive, in your clan",
                "father_buried" => "{=CSR_HeroLore_House_FatherBuried}your father dead, in your family tree",
                "forebears_buried" => "{=CSR_HeroLore_House_ForebearsBuried}your father's parents dead, in your family tree",
                "mother_buried" => "{=CSR_HeroLore_House_MotherBuried}your mother dead, in your family tree",
                _ => null
            };

            return key == null ? null : Localized(key);
        }

        /// <summary>
        ///     Whether a household id gives back nothing BY DESIGN rather than
        ///     because nobody has written its English.
        ///
        ///     Two answers read the life instead of carrying a fact: one speaks to
        ///     whichever parent the life left standing, and on a life that has
        ///     already said both are living it speaks to neither; the other finds
        ///     both living, and only where the life has said nothing about them.
        ///     That is a shorter panel and not a missing line, so the caller asks
        ///     here before warning about an id nobody has taught the vocabulary.
        /// </summary>
        public static bool SettlesNothing(string? id) =>
            string.Equals(id, StoryGraves.Declares, StringComparison.Ordinal) ||
            string.Equals(id, StoryHome.Declares, StringComparison.Ordinal);

        private static string Localized(string template) => new TextObject(template).ToString();
    }
}
