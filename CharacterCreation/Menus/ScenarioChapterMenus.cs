using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Scenarios;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The scenario chapters: each start type's own story beats after the
    ///     shared origin, every choice writing the same session fields the Start
    ///     Editor writes, with magnitudes derived from the running game.
    ///
    ///     The chapters that ask for a degree rather than a kind are keyed on the
    ///     life that reached them, through <see cref="LifeGate"/>. Until they were,
    ///     the eight scenes before them decided nothing here: a poacher's life and
    ///     a warlord's life were handed the same four crimes, and the contract a
    ///     nobody could sign was the contract a name could.
    /// </summary>
    public static class ScenarioChapterMenus
    {
        private static readonly Func<LifeProfile, int> Name =
            profile => profile.Score(LifeProfile.Lean.Standing);

        private static readonly Func<LifeProfile, int> Trade =
            profile => profile.Score(LifeProfile.Lean.Commerce);

        private static readonly Func<LifeProfile, int> Martial =
            profile => profile.Score(LifeProfile.Lean.Martial);

        /// <summary>Birth and a following together: the great names that would answer a summons.</summary>
        private static readonly Func<LifeProfile, int> Houses =
            profile => profile.Score(LifeProfile.Lean.Standing) + profile.Score(LifeProfile.Lean.Following);

        /// <summary>
        ///     What an outlaw's name is worth to the people who hunt it. Men and
        ///     violence raise it; anything left to lose holds it down, which is why
        ///     standing is subtracted rather than ignored.
        /// </summary>
        private static readonly Func<LifeProfile, int> Notoriety =
            profile => profile.Score(LifeProfile.Lean.Martial) + profile.Score(LifeProfile.Lean.Following)
                       - profile.Score(LifeProfile.Lean.Standing);

        public static void AddChapterMenus(CharacterCreationManager manager)
        {
            AddContractMenu(manager);
            AddCrimeMenu(manager);
            AddFiefMenu(manager);
            AddRisingMenu(manager);
            AddTraditionsMenu(manager);
            AddSwornHousesMenu(manager);
            AddFirstWarMenu(manager);
            AddTradeMenu(manager);
            AddLedgerMenu(manager);
            AddBeastsMenu(manager);
        }

        private static NarrativeMenu Menu(string id, string titleKey, string descKey)
        {
            return new NarrativeMenu(
                id,
                CreationFlow.DeclaredPrevious(id),
                CreationFlow.DeclaredNext(id),
                new TextObject(titleKey),
                new TextObject(descKey),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs);
        }

        /// <summary>A chapter whose text can say why the missing degrees are missing.</summary>
        private static NarrativeMenu GatedMenu(string id, string titleKey, TextObject description)
        {
            return new NarrativeMenu(
                id,
                CreationFlow.DeclaredPrevious(id),
                CreationFlow.DeclaredNext(id),
                new TextObject(titleKey),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs);
        }

        /// <summary>
        ///     One answer of a chapter: the fiction on the button, the prose when it
        ///     is selected, and what it does, which is the third text an option
        ///     always carries and the one no catalog can hold for these chapters.
        ///
        ///     <paramref name="effect"/> is asked when the panel renders rather than
        ///     now, because every figure in it is read off a session still being
        ///     filled in. It is declared beside the pick so the two read the same
        ///     value: an effect line that recomputes what the pick computed is a
        ///     second copy of the rule, and a second copy is how a panel comes to
        ///     promise a number the character never receives.
        /// </summary>
        private static void Option(NarrativeMenu menu, string id, string titleKey, string descKey,
            Action onPick, Func<string> effect, Func<bool>? condition = null)
        {
            string chapterId = menu.StringId;
            var title = new TextObject(titleKey);

            ChoiceEffects.Declare(id, effect);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                title,
                new TextObject(descKey),
                args => { },
                m => condition?.Invoke() ?? true,
                m =>
                {
                    onPick();
                    // Keyed by chapter, so re-deciding replaces rather than
                    // stacking, and the epilogue can recite the real choices
                    CreationSession.Current.StoryBeats[chapterId] = title.ToString();
                },
                m => { }));
        }

        /// <summary>
        ///     One degree of a gated chapter. The band carries the option's id, so a
        ///     degree and the window that opens it cannot drift apart.
        ///
        ///     <paramref name="supplied"/> is the second question, and a different
        ///     one: the gate asks whether this life may claim the degree, and this
        ///     asks whether the world has anything to hand over if it does. A life
        ///     that cannot claim a degree is told so in the chapter's own text,
        ///     while a degree the world cannot supply is simply not
        ///     put, because there is nothing to say about it and nothing to grant.
        /// </summary>
        private static void Degree(NarrativeMenu menu, TextObject description, IReadOnlyList<LifeBand> bands,
            LifeBand band, Func<LifeProfile, int> read, string titleKey, string descKey, Action onPick,
            Func<string> effect, Func<bool>? supplied = null)
        {
            var captured = band;
            Option(menu, band.Id, titleKey, descKey, onPick, effect,
                () => LifeGate.Offers(description, bands, captured, read) && (supplied?.Invoke() ?? true));
        }

        /// <summary>The realm this start answers to, by name, or null before one is named.</summary>
        private static string? Realm() =>
            CreationSession.Current.SelectedKingdom?.Name?.ToString();

        // There is no realm-wars chapter, and there must not be one. Whether a
        // realm fights or rests is its monarch's decision: a vassal, a landless
        // lord and a mercenary take the wars of the realm they join. The one
        // start that may decide is the Monarch, whose new crown has no wars until
        // it makes them, and cs_first_war_menu below is where it does.

        #region Mercenary: the contract

        /// <summary>
        ///     The going rate is never shut: a company of your standing is offered
        ///     what a company of your standing is offered, whatever that is. What
        ///     the life has to earn is the right to ask for less or for more.
        /// </summary>
        private static readonly LifeBand[] ContractBands =
        {
            new("cs_contract_modest", 2, LifeGate.Open, 7,
                "{=CSR_Contract_Modest_Shut}Signing for little: your name is known well enough that asking for scraps would be read as a trick, and looked into."),
            new("cs_contract_fair", 6, LifeGate.Open, LifeGate.NoTop, "{=!}"),
            new("cs_contract_princely", 10, 5, LifeGate.NoTop,
                "{=CSR_Contract_Princely_Shut}Haggling for a princely sum: nobody at that table has heard of you, and the clerks do not haggle with strangers.")
        };

        private static void AddContractMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Contract_Desc}The terms are read aloud before the seal is pressed. What did your name command at the table?");
            var menu = GatedMenu("cs_contract_menu", "{=CSR_Contract_Title}The Contract", description);

            // One expression per rate, read by the pick and by the panel. Writing
            // the arithmetic twice is how the panel came to quote a rate the
            // contract did not carry
            Func<int> modest = () => Math.Max(1,
                GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom) * 3 / 4);
            Func<int> going = () => GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom);
            Func<int> princely = () => GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom) * 2;

            Degree(menu, description, ContractBands, ContractBands[0], Name,
                "{=CSR_Contract_Modest}A Modest Retainer",
                "{=CSR_Contract_Modest_Desc}You asked little, and were signed without a second glance. Small pay, but nobody watches you too closely.",
                () => CreationSession.Current.CustomContractPay = modest(),
                () => ContractEffect(modest(), false));

            Degree(menu, description, ContractBands, ContractBands[1], Name,
                "{=CSR_Contract_Fair}A Fair Wage",
                "{=CSR_Contract_Fair_Desc}The clerks offered what the realm offers any company of your standing, and you took it.",
                () => CreationSession.Current.CustomContractPay = null,
                () => ContractEffect(going(), true));

            Degree(menu, description, ContractBands, ContractBands[2], Name,
                "{=CSR_Contract_Princely}A Princely Sum",
                "{=CSR_Contract_Princely_Desc}You haggled like a horse trader and won double the going rate. The paymaster will remember your face, and not fondly.",
                () => CreationSession.Current.CustomContractPay = princely(),
                () => ContractEffect(princely(), false));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     What the seal is worth, in the unit the game pays it in: a mercenary
        ///     award is denars for each point of influence the company earns for the
        ///     realm it serves.
        ///
        ///     A rate the chapter computes is written into the session and never
        ///     asked again, so it is stated as the figure it is. The going rate is
        ///     not written at all, and the scenario asks the same model for it when
        ///     the campaign opens, so it is stated as what stands today.
        /// </summary>
        private static string ContractEffect(int pay, bool readAgain)
        {
            var realm = Realm();
            var terms = new TextObject(realm == null
                ? "{=CSR_Panel_Contract_PayNoRealm}Gold: +{PAY} denars for every point of influence you earn for the realm you sign with"
                : "{=CSR_Panel_Contract_Pay}Gold: +{PAY} denars for every point of influence you earn for {REALM}");
            terms.SetTextVariable("PAY", pay);
            if (realm != null) terms.SetTextVariable("REALM", realm);

            var source = new TextObject(readAgain
                ? "{=CSR_Panel_Contract_Going}Why: that is the going rate for a company of your standing, and it is asked again when the campaign opens"
                : "{=CSR_Panel_Contract_Fixed}Why: the rate is written into the contract at that figure, whatever the clerks would have offered");

            return ChoiceEffects.Stated(terms.ToString(), source.ToString());
        }

        #endregion

        #region Outlaw: the crime

        private static readonly LifeBand[] CrimeBands =
        {
            new("cs_crime_poacher", -3, LifeGate.Open, 4,
                "{=CSR_Crime_Poacher_Shut}A poacher's tale: too many men ride with you for anyone to believe the worst of you is a deer."),
            new("cs_crime_brigand", 3, LifeGate.Open, 9,
                "{=CSR_Crime_Brigand_Shut}A brigand's name: either there is nothing on you worth a file, or there is a great deal more than one."),
            new("cs_crime_scourge", 8, 2, LifeGate.NoTop,
                "{=CSR_Crime_Scourge_Shut}Scourge of the roads: you never had the men or the stomach to close a road, and the magistrates know it."),
            new("cs_crime_infamous", 13, 8, LifeGate.NoTop,
                "{=CSR_Crime_Infamous_Shut}Enemy of all crowns: you have neither done enough nor lost enough for every realm to want you.")
        };

        private static void AddCrimeMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Crime_Desc}Every outlaw's tale begins with the thing they did, or the thing they were blamed for. What is yours?");
            var menu = GatedMenu("cs_crime_menu", "{=CSR_Crime_Title}The Crime", description);

            Crime(menu, description, CrimeBands[0],
                "{=CSR_Crime_Poacher}A Poacher's Tale",
                "{=CSR_Crime_Poacher_Desc}You hunted a lord's forest to feed your own. A small crime with a small price, but enough to put you outside the law.",
                20, false);

            Crime(menu, description, CrimeBands[1],
                "{=CSR_Crime_Brigand}A Brigand's Name",
                "{=CSR_Crime_Brigand_Desc}Roadside toll collecting of the unsanctioned kind. Merchants curse your name and the magistrates keep a file.",
                45, false);

            Crime(menu, description, CrimeBands[2],
                "{=CSR_Crime_Scourge}Scourge of the Roads",
                "{=CSR_Crime_Scourge_Desc}Caravans rerouted to avoid you. Garrisons doubled their watches. The bounty on your head could buy a farm.",
                70, false);

            Crime(menu, description, CrimeBands[3],
                "{=CSR_Crime_Infamous}Enemy of All Crowns",
                "{=CSR_Crime_Infamous_Desc}What you did is spoken of in every realm, and every realm wants you answering for it. There is nowhere left that does not know your face.",
                90, true);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One crime, whose rating and reach are named once and read by both the
        ///     pick and the panel.
        /// </summary>
        private static void Crime(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, int rating, bool everywhere)
        {
            Degree(menu, description, CrimeBands, band, Notoriety, titleKey, descKey,
                () => SetCrime(rating, everywhere),
                () => CrimeEffect(rating, everywhere));
        }

        /// <summary>
        ///     What a rating costs, including the door it shuts.
        ///
        ///     The holding chapter comes after this one and offers an outlawed lord
        ///     a hall only while the rating stays under the threshold at which the
        ///     game's own crime model sends armies. That is the same call the next
        ///     chapter makes, not a forecast of it, so the answer is settled here
        ///     and the player is owed it before they choose.
        /// </summary>
        private static string CrimeEffect(int rating, bool everywhere)
        {
            var realm = Realm();
            var wanted = new TextObject(realm == null
                ? "{=CSR_Panel_Crime_RatingNoRealm}Crime: {RATING} with the realm that outlawed you"
                : "{=CSR_Panel_Crime_Rating}Crime: {RATING} with {REALM} when the campaign opens");
            wanted.SetTextVariable("RATING", rating);
            if (realm != null) wanted.SetTextVariable("REALM", realm);

            string? abroad = null;
            if (everywhere)
            {
                var others = Kingdom.All
                    .Where(k => !k.IsEliminated && k != CreationSession.Current.SelectedKingdom)
                    .Select(k => k.Name?.ToString() ?? k.StringId)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (others.Count > 0)
                {
                    var everywhereText = new TextObject(
                        "{=CSR_Panel_Crime_Everywhere}Crime: {RATING} with every other realm too: {REALMS}");
                    everywhereText.SetTextVariable("RATING", rating);
                    everywhereText.SetTextVariable("REALMS", string.Join(", ", others));
                    abroad = everywhereText.ToString();
                }
            }

            var hall = new TextObject(ContextualMenus.OutlawMayHoldAHall(rating)
                ? "{=CSR_Panel_Crime_HallOpen}Hall: still offered you in the next chapter at that rating"
                : "{=CSR_Panel_Crime_HallShut}Hall: none at that rating, since a realm marches on any you sat in, and one already taken is given up");

            return ChoiceEffects.Stated(wanted.ToString(), abroad, hall.ToString());
        }

        private static void SetCrime(int rating, bool everywhere)
        {
            var session = CreationSession.Current;
            session.CustomCrimeRating = rating;
            session.OutlawWantedBy = everywhere
                ? Kingdom.All.Where(k => !k.IsEliminated && k != session.SelectedKingdom)
                    .Select(k => k.StringId).ToList()
                : null;
        }

        #endregion

        #region Holding starts: the garrison

        private static void AddFiefMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_fief_menu",
                "{=CSR_Fief_Title}The Garrison",
                "{=CSR_Fief_Desc}Walls are only as strong as the soldiers on them. How well is your holding manned when your story begins?");

            Option(menu, "cs_fief_asfound",
                "{=CSR_Fief_AsFound}As You Found It",
                "{=CSR_Fief_AsFound_Desc}The garrison stands as the last steward left it, for better or worse.",
                () => CreationSession.Current.CustomGarrison = null,
                AsFoundEffect);

            Garrison(menu, "cs_fief_skeleton",
                "{=CSR_Fief_Skeleton}A Skeleton Watch",
                "{=CSR_Fief_Skeleton_Desc}A handful of gray veterans walk the walls. Cheap to keep, and a temptation to every rival with a ladder.",
                4, 1);

            Garrison(menu, "cs_fief_standing",
                "{=CSR_Fief_Standing}A Standing Garrison",
                "{=CSR_Fief_Standing_Desc}The walls are properly manned and the watch rotations full. A respectable strength for a holding of this size.",
                2, 1);

            Garrison(menu, "cs_fief_host",
                "{=CSR_Fief_Host}A Fortified Host",
                "{=CSR_Fief_Host_Desc}Your holding bristles with spears; the wage bill will bristle too. Nobody besieges this place lightly.",
                4, 3);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One share of the walls. The share is named once; the pick spends it
        ///     and the panel states it.
        /// </summary>
        private static void Garrison(NarrativeMenu menu, string id, string titleKey, string descKey,
            int denominator, int numerator)
        {
            Option(menu, id, titleKey, descKey,
                () => CreationSession.Current.CustomGarrison = GarrisonShare(denominator, numerator),
                () => GarrisonEffect(GarrisonShare(denominator, numerator)));
        }

        private static int GarrisonShare(int denominator, int numerator = 1)
        {
            int limit = GameCaps.MaxGarrison(CreationSession.Current.SelectedSettlement,
                CreationSession.Current.SelectedKingdom);
            return Math.Max(0, limit * numerator / denominator);
        }

        /// <summary>
        ///     How many stand on the walls, against how many the walls hold. The
        ///     ceiling is worth as much as the figure: the same share of a castle
        ///     and of a great town are two different garrisons, and the holding
        ///     chapter has already been answered by the time this one is put.
        /// </summary>
        private static string GarrisonEffect(int size)
        {
            var holding = CreationSession.Current.SelectedSettlement;
            int limit = GameCaps.MaxGarrison(holding, CreationSession.Current.SelectedKingdom);

            var text = new TextObject(holding == null
                ? "{=CSR_Panel_Fief_GarrisonAuto}Troops: {COUNT} in the garrison of the holding you are granted, of the {LIMIT} it can hold"
                : "{=CSR_Panel_Fief_Garrison}Troops: {COUNT} in the garrison of {HOLDING}, of the {LIMIT} it can hold");
            text.SetTextVariable("COUNT", size);
            text.SetTextVariable("LIMIT", limit);
            if (holding != null) text.SetTextVariable("HOLDING", holding.Name);

            var fill = new TextObject(
                "{=CSR_Panel_Fief_Fill}Type: recruits of the holding's own culture make up the difference, and anything over that number is mustered out");

            return ChoiceEffects.Stated(text.ToString(), fill.ToString());
        }

        /// <summary>
        ///     The one answer that changes nothing, said as the number it leaves
        ///     standing. Where the holding is already named its garrison is a figure
        ///     the world holds today, so the panel reads it rather than calling it
        ///     whatever is there.
        /// </summary>
        private static string AsFoundEffect()
        {
            var holding = CreationSession.Current.SelectedSettlement;
            var garrison = holding?.Parties?.FirstOrDefault(p => p.IsGarrison);
            if (holding == null || garrison == null)
                return new TextObject(
                    "{=CSR_Panel_Fief_AsFoundAuto}Troops: whatever the holding you are granted already garrisons, with nothing raised and nothing mustered out").ToString();

            var text = new TextObject(
                "{=CSR_Panel_Fief_AsFound}Troops: the {COUNT} {HOLDING} garrisons today, with nothing raised and nothing mustered out");
            text.SetTextVariable("HOLDING", holding.Name);
            text.SetTextVariable("COUNT", garrison.MemberRoster.TotalManCount);
            return text.ToString();
        }

        #endregion

        #region Rebel clan: the rising

        private static readonly LifeBand[] RisingBands =
        {
            new("cs_rising_alone", 5, LifeGate.Open, 10,
                "{=CSR_Rising_Alone_Shut}Rising alone: you have too many friends among the great names for all of them to have stayed home."),
            new("cs_rising_one", 10, LifeGate.Open, 15,
                "{=CSR_Rising_One_Shut}One house sworn: either no house owes you a thing, or a good many more than one does."),
            new("cs_rising_two", 13, 8, LifeGate.NoTop,
                "{=CSR_Rising_Two_Shut}Two houses sworn: your life never put you in front of two houses willing to hang for you."),
            new("cs_rising_league", 18, 14, LifeGate.NoTop,
                "{=CSR_Rising_League_Shut}A league of rebels: four houses do not sign under a name they have not heard.")
        };

        private static void AddRisingMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Rising_Desc}A rebellion is measured in the houses that dare to join it. Who rose with you?");
            var menu = GatedMenu("cs_rising_menu", "{=CSR_Rising_Title}The Rising", description);

            Rising(menu, description, RisingBands[0],
                "{=CSR_Rising_Alone}We Rise Alone",
                "{=CSR_Rising_Alone_Desc}No other house had the stomach for it. Your name alone carries the rebellion, and yours alone will answer for it.",
                0);

            Rising(menu, description, RisingBands[1],
                "{=CSR_Rising_One}One House Sworn",
                "{=CSR_Rising_One_Desc}A single house took your hand and your cause. Two banners against a realm is thin company, but it is company.",
                1);

            Rising(menu, description, RisingBands[2],
                "{=CSR_Rising_Two}Two Houses Sworn",
                "{=CSR_Rising_Two_Desc}Two noble houses rose beside you, with their own parties and their own grudges against the crown.",
                2);

            Rising(menu, description, RisingBands[3],
                "{=CSR_Rising_League}A League of Rebels",
                "{=CSR_Rising_League_Desc}Four houses signed their names beneath yours. The realm calls it a conspiracy; you call it the beginning of something.",
                4);

            manager.AddNewMenu(menu);
        }

        private static void Rising(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, int houses)
        {
            Degree(menu, description, RisingBands, band, Houses, titleKey, descKey,
                () => CreationSession.Current.RebelAllyCount = houses,
                () => RisingEffect(houses));
        }

        /// <summary>
        ///     Who else is in the rebellion. Each house is a whole clan the start
        ///     raises: a lord, a party and the same war, seated near the castle the
        ///     rising seizes.
        /// </summary>
        private static string RisingEffect(int houses)
        {
            if (houses == 0)
                return new TextObject(
                    "{=CSR_Panel_Rising_Alone}Allies: no house rises with you, and your clan is the whole of the rebellion").ToString();

            var realm = Realm();
            var text = new TextObject(realm == null
                ? "{=CSR_Panel_Rising_HousesNoRealm}Allies: {HOUSES}, at war with the realm you rebel against"
                : "{=CSR_Panel_Rising_Houses}Allies: {HOUSES}, at war with {REALM}");
            text.SetTextVariable("HOUSES", MenuText.Count(houses,
                "{=CSR_Panel_Rising_One}{COUNT} noble house, a full clan with its own lord and party",
                "{=CSR_Panel_Rising_Many}{COUNT} noble houses, each a full clan with its own lord and party"));
            if (realm != null) text.SetTextVariable("REALM", realm);
            return text.ToString();
        }

        #endregion

        #region Monarch: traditions

        /// <summary>
        ///     Five flavors rather than a ladder, so each is keyed on the lean it
        ///     actually claims about the life instead of on a shared degree. The old
        ///     ways are never shut: a realm can always decline to write any law down.
        /// </summary>
        private static readonly LifeBand[] TraditionBands =
        {
            new("cs_traditions_none", 0, LifeGate.Open, LifeGate.NoTop, "{=!}"),
            new("cs_traditions_crown", 0, 9, LifeGate.NoTop,
                "{=CSR_Traditions_Crown_Shut}An iron crown: nothing in your life taught anyone to do as you say without being asked twice.",
                Name),
            new("cs_traditions_council", 0, 15, LifeGate.NoTop,
                "{=CSR_Traditions_Council_Shut}A council of nobles: there are not enough great names who owe you anything to seat one.",
                Houses),
            new("cs_traditions_commons", 0, LifeGate.Open, 11,
                "{=CSR_Traditions_Commons_Shut}The common weal: you were never one of the people, and nobody has forgotten which house you came from.",
                Name),
            new("cs_traditions_war", 0, 5, LifeGate.NoTop,
                "{=CSR_Traditions_War_Shut}A realm at arms: you have not spent enough of your life under arms to govern one like a camp.",
                Martial)
        };

        private static void AddTraditionsMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Traditions_Desc}A new crown still stands on old customs. Which traditions does your realm carry from its first day?");
            var menu = GatedMenu("cs_traditions_menu", "{=CSR_Traditions_Title}Traditions of the Realm", description);

            Tradition(menu, description, TraditionBands[0],
                "{=CSR_Traditions_None}The Old Ways",
                "{=CSR_Traditions_None_Desc}No laws beyond custom; the realm begins unburdened, and every policy is a debate yet to come.",
                () => new PolicyObject?[0]);

            Tradition(menu, description, TraditionBands[1],
                "{=CSR_Traditions_Crown}An Iron Crown",
                "{=CSR_Traditions_Crown_Desc}The throne holds what the throne takes: sacred majesty, a royal guard, privilege and duty flowing to the crown.",
                () => new PolicyObject?[]
                {
                    DefaultPolicies.SacredMajesty, DefaultPolicies.RoyalGuard,
                    DefaultPolicies.RoyalPrivilege, DefaultPolicies.CrownDuty
                });

            Tradition(menu, description, TraditionBands[2],
                "{=CSR_Traditions_Council}A Council of Nobles",
                "{=CSR_Traditions_Council_Desc}The great houses share the burden and the spoils: a senate, a privy council, inheritance and retinues secured by law.",
                () => new PolicyObject?[]
                {
                    DefaultPolicies.Senate, DefaultPolicies.LordsPrivyCouncil,
                    DefaultPolicies.FeudalInheritance, DefaultPolicies.NobleRetinues
                });

            Tradition(menu, description, TraditionBands[3],
                "{=CSR_Traditions_Commons}The Common Weal",
                "{=CSR_Traditions_Commons_Desc}The realm is its people: citizenship, tribunes, trial by jury, and the forgiveness of debts.",
                () => new PolicyObject?[]
                {
                    DefaultPolicies.Citizenship, DefaultPolicies.TribunesOfThePeople,
                    DefaultPolicies.TrialByJury, DefaultPolicies.ForgivenessOfDebts
                });

            Tradition(menu, description, TraditionBands[4],
                "{=CSR_Traditions_War}A Realm at Arms",
                "{=CSR_Traditions_War_Desc}Forged for war and governed like a camp: marshals, military coronae, war taxes, and land for veterans.",
                () => new PolicyObject?[]
                {
                    DefaultPolicies.Marshals, DefaultPolicies.MilitaryCoronae,
                    DefaultPolicies.WarTax, Services.VersionedGameApi.LandGrantsForVeterans
                });

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One set of traditions, declared once. The set is what the realm is
        ///     founded under and what the panel names, and the panel names the
        ///     policies by the game's own names for them rather than by the words
        ///     the chapter wrapped them in.
        /// </summary>
        private static void Tradition(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, Func<PolicyObject?[]> policies)
        {
            Degree(menu, description, TraditionBands, band, Name, titleKey, descKey,
                () => SetPolicies(policies()),
                () => PolicyEffect(policies()));
        }

        private static void SetPolicies(params PolicyObject?[] policies)
        {
            var session = CreationSession.Current;
            session.SelectedPolicies.Clear();
            foreach (var policy in policies)
                if (policy != null)
                    session.SelectedPolicies.Add(policy.StringId);
        }

        private static string PolicyEffect(PolicyObject?[] policies)
        {
            var named = policies
                .Where(p => p != null)
                .Select(p => p!.Name?.ToString() ?? p!.StringId)
                .ToList();

            if (named.Count == 0)
                return new TextObject(
                    "{=CSR_Panel_Traditions_None}Laws: none in force at the founding, and every policy a vote still to be held").ToString();

            var text = new TextObject(
                "{=CSR_Panel_Traditions_Laws}Laws: {POLICIES}, in force from the day your realm is founded");
            text.SetTextVariable("POLICIES", string.Join(", ", named));
            return text.ToString();
        }

        #endregion

        #region Monarch: the sworn houses

        private static readonly LifeBand[] SwornBands =
        {
            new("cs_sworn_none", 9, LifeGate.Open, 15,
                "{=CSR_SwornHouses_None_Shut}The crown standing alone: too many houses already owe you to pretend that none of them knelt."),
            new("cs_sworn_two", 14, LifeGate.Open, 19,
                "{=CSR_SwornHouses_Two_Shut}Two loyal houses: either no house knows your name, or rather more than two of them do."),
            new("cs_sworn_four", 18, 13, LifeGate.NoTop,
                "{=CSR_SwornHouses_Four_Shut}Four houses given lands: your life never gathered four houses willing to kneel to it."),
            new("cs_sworn_six", 23, 19, LifeGate.NoTop,
                "{=CSR_SwornHouses_Six_Shut}A court of six: a court that size assembles around a name, and yours is not yet one.")
        };

        private static void AddSwornHousesMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_SwornHouses_Desc}A crown without vassals is a hat. Which houses knelt at your founding, and what did their oaths cost you?");
            var menu = GatedMenu("cs_sworn_houses_menu", "{=CSR_SwornHouses_Title}The Sworn Houses", description);

            Sworn(menu, description, SwornBands[0],
                "{=CSR_SwornHouses_None}The Crown Stands Alone",
                "{=CSR_SwornHouses_None_Desc}No house has knelt yet. Every castle stays in your hand, and every sword is one you pay for yourself.",
                0, false);

            Sworn(menu, description, SwornBands[1],
                "{=CSR_SwornHouses_Two}Two Loyal Houses",
                "{=CSR_SwornHouses_Two_Desc}Two noble houses swore at your coronation. The granted castles remain crown land; their loyalty rests on your promise of more.",
                2, false);

            Sworn(menu, description, SwornBands[2],
                "{=CSR_SwornHouses_Four}Four Houses, Lands Given",
                "{=CSR_SwornHouses_Four_Desc}Four houses knelt and rose as lords: the castles you seized passed to them as the price of their banners.",
                4, true);

            Sworn(menu, description, SwornBands[3],
                "{=CSR_SwornHouses_Six}A Court of Six",
                "{=CSR_SwornHouses_Six_Desc}Six houses swore, and the granted castles went with the oaths. A real court from the first day, fed by land that is no longer yours.",
                6, true);

            manager.AddNewMenu(menu);
        }

        private static void Sworn(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, int count, bool grantLands)
        {
            Degree(menu, description, SwornBands, band, Houses, titleKey, descKey,
                () => SetVassals(count, grantLands),
                () => SwornEffect(count, grantLands));
        }

        private static void SetVassals(int count, bool grantLands)
        {
            var session = CreationSession.Current;
            session.VassalClanCount = count;
            session.GrantLandsToVassals = grantLands;
        }

        /// <summary>
        ///     Who kneels and what it costs the crown in land.
        ///
        ///     A seat is taken for every house either way; this answer decides who
        ///     ends up holding it. How many castles the founding seizes in total is
        ///     the crown's own standing as well as this answer, and that is the
        ///     means chapter's figure, so it is not restated as though this chapter
        ///     settled it.
        /// </summary>
        private static string SwornEffect(int count, bool grantLands)
        {
            if (count == 0)
                return new TextObject(
                    "{=CSR_Panel_Sworn_None}Allies: no house swears to you, so every holding of the realm stays in your hand and every soldier in it is one you pay for").ToString();

            var swear = new TextObject("{=CSR_Panel_Sworn_Houses}Allies: {HOUSES}, sworn to you at your founding");
            swear.SetTextVariable("HOUSES", MenuText.Count(count,
                "{=CSR_Panel_Sworn_One}{COUNT} noble house, a full clan with its own lord and party",
                "{=CSR_Panel_Sworn_Many}{COUNT} noble houses, each a full clan with its own lord and party"));

            var land = new TextObject(grantLands
                ? "{=CSR_Panel_Sworn_Lands}Hall: a castle of its own to every house that knelt, taken in your founding and passed to it"
                : "{=CSR_Panel_Sworn_Crown}Hall: none to them, so the castles taken in your founding stay in your hand and the houses are seated at your capital");

            return ChoiceEffects.Stated(swear.ToString(), land.ToString());
        }

        #endregion

        #region Monarch: the first war

        private static void AddFirstWarMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_first_war_menu",
                "{=CSR_FirstWar_Title}The First War",
                "{=CSR_FirstWar_Desc}New crowns are tested early. Who marches against yours as the banners are raised?");

            Option(menu, "cs_first_war_founding",
                "{=CSR_FirstWar_Founding}As the Founding Demands",
                "{=CSR_FirstWar_Founding_Desc}Your founding story decides: a settler's quiet grant stays quiet, a claimant's seizure brings the dispossessed to war.",
                () => CreationSession.Current.CustomWars = null,
                FoundingWarEffect);

            Option(menu, "cs_first_war_peace",
                "{=CSR_FirstWar_Peace}An Uneasy Peace",
                "{=CSR_FirstWar_Peace_Desc}The realms watch and wait. No army marches on you yet; every border is a held breath.",
                () => CreationSession.Current.CustomWars = new List<string>(),
                () => new TextObject(
                    "{=CSR_Panel_War_Peace}Wars: none, whatever land your founding took").ToString());

            War(menu, "cs_first_war_unseated",
                "{=CSR_FirstWar_Unseated}The Crown You Unseated",
                "{=CSR_FirstWar_Unseated_Desc}The hall you raised your banner over was somebody else's that morning, and the somebody is still alive to say so.",
                TheCrownYouUnseat, WhyUnseated);

            War(menu, "cs_first_war_neighbor",
                "{=CSR_FirstWar_Neighbor}The Crown Next Door",
                "{=CSR_FirstWar_Neighbor_Desc}Close enough that their riders reach your gate before the news of your founding reaches their court.",
                TheCrownNextDoor, WhyNextDoor);

            War(menu, "cs_first_war_strongest",
                "{=CSR_FirstWar_Strongest}The Strongest Crown",
                "{=CSR_FirstWar_Strongest_Desc}The greatest power on the map looked at your banners and decided to settle the question while it was still small.",
                TheStrongestCrown, WhyStrongest);

            War(menu, "cs_first_war_kin",
                "{=CSR_FirstWar_Kin}Those Who Call You Usurper",
                "{=CSR_FirstWar_Kin_Desc}Your own people under an older crown, to whom a second one is not a realm at all but a rebellion with a flag.",
                ContextualMenus.OfYourOwnPeople, WhyKin);

            War(menu, "cs_first_war_land",
                "{=CSR_FirstWar_Land}A crown that wants the land",
                "{=CSR_FirstWar_Land_Desc}No grievance, no claim, no insult given. There is only good ground under your new banners and somebody who would rather hold it.",
                WantsTheLand, WhyWantsLand);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One written framing of the first war: a kind of crown, resolved to a
        ///     real one behind the scenes and named in the panel.
        ///
        ///     The realm menu's machinery does the resolving, because this chapter asks
        ///     the same question of the same map and two implementations of it would
        ///     answer differently. What is this chapter's own is the fiction on the
        ///     button, the band that says which crowns answer it, and
        ///     <paramref name="why"/>, the fact about the resolved crown that the
        ///     framing rests on.
        ///
        ///     A framing the map cannot answer is not offered at all, which is the rule
        ///     the realm and holding menus keep: a monarch who left his capital to the
        ///     stewards has no crown to unseat and no border to be next to, so those two
        ///     framings are absent rather than empty.
        /// </summary>
        private static void War(NarrativeMenu menu, string id, string titleKey, string descKey,
            Func<IReadOnlyList<Kingdom>, CultureObject?, IReadOnlyList<Kingdom>> band,
            Func<Kingdom, string?> why)
        {
            var description = new TextObject(descKey);
            var captured = band;
            var capturedWhy = why;

            ChoiceEffects.Declare(id, () =>
            {
                var offered = ContextualMenus.OfferRealm(id, captured, false, out int choices);
                return WarEffect(offered, choices, capturedWhy);
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description,
                    // The description quotes nothing, but this is still the only
                    // hook the API runs on every render, and what it runs is what
                    // reserves this framing's crown so no other lands on it
                    unused => ContextualMenus.OfferRealm(id, captured, false, out _),
                    () => ContextualMenus.OfferRealm(id, captured, false, out _) != null),
                m => DeclareWar(id, captured),
                m => { }));
        }

        /// <summary>
        ///     A click on a first war framing.
        ///
        ///     The description quotes nothing now, so what a re-click has to move is
        ///     the crown this framing is holding and the war written from it. The
        ///     panel is rebuilt from that reservation on the next tick of the stage
        ///     and follows on its own.
        ///
        ///     The beat is written from the realm rather than from the framing, so the
        ///     epilogue reads back which crown marched instead of which sentence the
        ///     player clicked.
        /// </summary>
        private static void DeclareWar(string id,
            Func<IReadOnlyList<Kingdom>, CultureObject?, IReadOnlyList<Kingdom>> band)
        {
            var realm = ContextualMenus.OfferRealm(id, band, ContextualMenus.AskingAgain(id), out _);
            if (realm == null) return;

            CreationSession.Current.CustomWars = new List<string> { realm.StringId };

            var beat = new TextObject("{=CSR_FirstWar_Against}{KINGDOM_NAME} Marches");
            beat.SetTextVariable("KINGDOM_NAME", realm.Name);
            CreationSession.Current.StoryBeats["cs_first_war_menu"] = beat.ToString();
            CSLogger.Info($"First war declared: {realm.Name}");
        }

        /// <summary>
        ///     What leaving the wars to the founding actually means, which the
        ///     founding chapter has already settled by the time this one is put.
        /// </summary>
        private static string FoundingWarEffect()
        {
            return new TextObject(
                CreationSession.Current.SelectedFounding == MonarchFounding.Claimant
                    ? "{=CSR_Panel_War_Claimant}Wars: every realm your seizure takes land from, from the first day, since you founded as a claimant"
                    : "{=CSR_Panel_War_Settler}Wars: none, since you founded as a settler and no realm declares on you").ToString();
        }

        /// <summary>
        ///     What declaring this war does, in the crown the framing came out as: the
        ///     same call reading the click writes that crown into the session, so the
        ///     panel cannot name one realm and the campaign open against another.
        ///
        ///     The second line is the fact the framing rests on, because a player
        ///     picking a war by a sentence is owed the reason that sentence landed on
        ///     this crown and not another.
        /// </summary>
        private static string WarEffect(Kingdom? realm, int choices, Func<Kingdom, string?> why)
        {
            if (realm == null)
                return new TextObject(
                    "{=CSR_Panel_War_NoneAnswers}Wars: none, since no realm on the map answers to this").ToString();

            var declared = new TextObject(
                "{=CSR_Panel_War_One}Wars: {KINGDOM}, and peace with every other realm");
            declared.SetTextVariable("KINGDOM", realm.Name);

            return ChoiceEffects.Stated(declared.ToString(), why(realm),
                choices > 1 ? ContextualMenus.AgainRealm() : null);
        }

        private static readonly Kingdom[] NoRealms = Array.Empty<Kingdom>();

        /// <summary>
        ///     The crown holding the town the founding takes. MonarchScenario reads the
        ///     owner off the same settlement before it grants it, so this is the realm
        ///     the founding actually unseats rather than a guess at one.
        /// </summary>
        private static Kingdom? CrownOfTheCapital() =>
            CreationSession.Current.SelectedSettlement?.OwnerClan?.Kingdom;

        /// <summary>One crown: the hand your capital comes out of.</summary>
        private static IReadOnlyList<Kingdom> TheCrownYouUnseat(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var unseated = CrownOfTheCapital();
            return unseated != null && pool.Contains(unseated) ? new[] { unseated } : NoRealms;
        }

        /// <summary>
        ///     One crown: the one whose land runs closest to your capital, the crown you
        ///     unseat aside. Taking a realm's town puts you inside it rather than beside
        ///     it, and this framing is about the border you will be sharing afterward.
        /// </summary>
        private static IReadOnlyList<Kingdom> TheCrownNextDoor(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var realm = NearestHolding()?.OwnerClan?.Kingdom;
            return realm != null && pool.Contains(realm) ? new[] { realm } : NoRealms;
        }

        /// <summary>
        ///     The holding of another crown that sits nearest the capital, measured over
        ///     the whole map rather than over what is left once the other framings have
        ///     taken theirs: a nearest that meant the nearest remaining would put a
        ///     closer crown on the same screen under a different sentence.
        /// </summary>
        private static Settlement? NearestHolding()
        {
            var capital = CreationSession.Current.SelectedSettlement;
            if (capital == null) return null;

            var unseated = CrownOfTheCapital();
            var seat = capital.GetPosition2D;

            return Settlement.All
                .Where(s =>
                {
                    if (!s.IsTown && !s.IsCastle) return false;
                    var owner = s.OwnerClan?.Kingdom;
                    return owner != null && owner != unseated;
                })
                .OrderBy(s => s.GetPosition2D.Distance(seat))
                .FirstOrDefault();
        }

        /// <summary>
        ///     One crown: the greatest on the map. Measured over every realm rather than
        ///     over the pool, so this can never name a second best as the strongest;
        ///     when another framing is already showing that realm, this one answers
        ///     nothing and is not offered.
        /// </summary>
        private static IReadOnlyList<Kingdom> TheStrongestCrown(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var top = ContextualMenus.EligibleRealms()
                .OrderByDescending(ContextualMenus.Holdings)
                .FirstOrDefault();
            return top != null && pool.Contains(top) ? new[] { top } : NoRealms;
        }

        /// <summary>
        ///     A kind of crown: one that is neither your own people nor the hand your
        ///     capital comes out of. Nothing it can hold against you, and a border it
        ///     would rather hold itself.
        /// </summary>
        private static IReadOnlyList<Kingdom> WantsTheLand(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var unseated = CrownOfTheCapital();
            return pool.Where(k => k != unseated && (culture == null || k.Culture != culture)).ToList();
        }

        private static string? WhyUnseated(Kingdom realm)
        {
            var capital = CreationSession.Current.SelectedSettlement;
            if (capital == null) return null;

            var text = new TextObject(
                "{=CSR_Panel_War_WhyUnseated}Why: your founding takes {HALL} out of its hand");
            text.SetTextVariable("HALL", capital.Name);
            return text.ToString();
        }

        private static string? WhyNextDoor(Kingdom realm)
        {
            var capital = CreationSession.Current.SelectedSettlement;
            var near = NearestHolding();
            if (capital == null || near == null) return null;

            var text = new TextObject(
                "{=CSR_Panel_War_WhyNextDoor}Why: {HALL} is its nearest holding to {SEAT}");
            text.SetTextVariable("HALL", near.Name);
            text.SetTextVariable("SEAT", capital.Name);
            return text.ToString();
        }

        private static string? WhyStrongest(Kingdom realm)
        {
            int fiefs = ContextualMenus.Holdings(realm);
            if (fiefs <= 0) return null;

            var text = new TextObject(
                "{=CSR_Panel_War_WhyStrongest}Why: no crown on the map holds more than its {COUNT} fiefs");
            text.SetTextVariable("COUNT", fiefs);
            return text.ToString();
        }

        private static string? WhyKin(Kingdom realm)
        {
            var people = realm.Culture?.Name;
            if (people == null) return null;

            var text = new TextObject("{=CSR_Panel_War_WhyKin}Why: its people are {CULTURE}, your own");
            text.SetTextVariable("CULTURE", people);
            return text.ToString();
        }

        private static string? WhyWantsLand(Kingdom realm)
        {
            var people = realm.Culture?.Name;
            if (people == null || CreationSession.Current.SelectedCulture == null) return null;

            var text = new TextObject(
                "{=CSR_Panel_War_WhyLand}Why: its people are {CULTURE} rather than your own");
            text.SetTextVariable("CULTURE", people);
            return text.ToString();
        }

        #endregion

        #region Commoner and caravan master: the trade

        private static void AddTradeMenu(CharacterCreationManager manager)
        {
            // A caravan master is asked this chapter too, so nothing here may name
            // a commoner or deny a ledger the run fills two chapters later
            var menu = Menu("cs_trade_menu",
                "{=CSR_Trade_Title}The Trade",
                "{=CSR_Trade_Property}A fortune of your own is built on something you hold the keys to. What do you own when the story begins?");

            Func<int> concern = () => Math.Min(2,
                Math.Max(1, GameCaps.MaxWorkshops(CreationSession.Current.EffectiveClanTier)));

            Option(menu, "cs_trade_labor",
                "{=CSR_Trade_Labor}Your Labor Alone",
                "{=CSR_Trade_Labor_Deed}Nothing with your name on the deed, and no rent going out. Everything you come to own is still ahead of you.",
                () => CreationSession.Current.StartingWorkshops = 0,
                () => TradeEffect(0));

            Option(menu, "cs_trade_workshop",
                "{=CSR_Trade_Workshop}The Family Workshop",
                "{=CSR_Trade_Workshop_Desc}A workshop in a nearby town carries your family's name, and now its keys are yours.",
                () => CreationSession.Current.StartingWorkshops = 1,
                () => TradeEffect(1));

            Option(menu, "cs_trade_concern",
                "{=CSR_Trade_Concern}A Going Concern",
                "{=CSR_Trade_Concern_Desc}Two establishments and a reputation to match. The town knows your name before you draw a sword.",
                () => CreationSession.Current.StartingWorkshops = concern(),
                () => TradeEffect(concern()),
                () => GameCaps.MaxWorkshops(CreationSession.Current.EffectiveClanTier) >= 2);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     What is on the deed, and how close to the clan's limit that is. The
        ///     shops are bought around where the character begins, which the start
        ///     resolves before it buys them.
        /// </summary>
        private static string TradeEffect(int workshops)
        {
            int tier = CreationSession.Current.EffectiveClanTier;
            int most = GameCaps.MaxWorkshops(tier);
            var cap = new TextObject(most == 1
                ? "{=CSR_Panel_Trade_CapOne}Clan: tier {TIER} may hold {MAX} workshop at once"
                : "{=CSR_Panel_Trade_Cap}Clan: tier {TIER} may hold {MAX} workshops at once");
            cap.SetTextVariable("TIER", tier);
            cap.SetTextVariable("MAX", most);

            if (workshops == 0)
                return ChoiceEffects.Stated(new TextObject(
                        "{=CSR_Panel_Trade_None}Workshop: none when the campaign opens, and no rent goes out")
                    .ToString(), cap.ToString());

            var owned = new TextObject(
                "{=CSR_Panel_Trade_Own}Workshop: {SHOPS}, taken over in the towns nearest the place you begin");
            owned.SetTextVariable("SHOPS", MenuText.Count(workshops,
                "{=CSR_Panel_Trade_One}{COUNT} when the campaign opens",
                "{=CSR_Panel_Trade_Many}{COUNT} when the campaign opens"));

            return ChoiceEffects.Stated(owned.ToString(), cap.ToString());
        }

        #endregion

        #region Caravan master: the ledger and the beasts

        /// <summary>
        ///     Rolling out with nothing on the axles is never shut. Every other
        ///     degree is a claim about the life behind it, which a life can be
        ///     caught out in; declining to load is a decision taken at the gate,
        ///     and no merchant, however large, is barred from taking it.
        /// </summary>
        private static readonly LifeBand[] LedgerBands =
        {
            new("cs_ledger_none", 0, LifeGate.Open, LifeGate.NoTop, "{=!}"),
            new("cs_ledger_empty", 4, LifeGate.Open, 8,
                "{=CSR_Ledger_Empty_Shut}Leaving the load to your factor: you have moved goods for too long to roll out of a town with a cargo you did not choose."),
            new("cs_ledger_hides", 8, LifeGate.Open, 12,
                "{=CSR_Ledger_Hides_Shut}Hides and wool: either nobody will front you a bale, or your credit is worth a great deal more than bulk."),
            new("cs_ledger_cloth", 11, 7, LifeGate.NoTop,
                "{=CSR_Ledger_Cloth_Shut}Cloth and wine: no merchant in your life ever trusted you with a load that could be stolen at a profit."),
            new("cs_ledger_silk", 15, 11, LifeGate.NoTop,
                "{=CSR_Ledger_Silk_Shut}Silk and spice: a fortune in fine goods goes out on a name, and yours has not carried one yet.")
        };

        private static void AddLedgerMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Ledger_Desc}A caravan is its cargo. What fills your wagons on the first page of the ledger?");
            var menu = GatedMenu("cs_ledger_menu", "{=CSR_Ledger_Title}The Ledger", description);

            Degree(menu, description, LedgerBands, LedgerBands[0], Trade,
                "{=CSR_Ledger_None}Nothing at All",
                "{=CSR_Ledger_None_Desc}You sign for no cargo at all. The wagons roll out of the gate empty, and whatever you carry you will buy on the road.",
                Nothing,
                NothingEffect);

            Degree(menu, description, LedgerBands, LedgerBands[1], Trade,
                "{=CSR_Ledger_Empty_Revamped}Whatever the Market Had",
                "{=CSR_Ledger_Empty_Desc_Revamped}You name no cargo, so your factor loads the wagons out of whatever the market had that morning.",
                Factor,
                FactorEffect,
                () => CaravanMasterScenario.MarketStock().Count >= CaravanMasterScenario.LedgerStacks);

            Cargo(menu, description, LedgerBands[2],
                "{=CSR_Ledger_Hides}Hides and Wool",
                "{=CSR_Ledger_Hides_Desc}Honest bulk goods: cheap to buy, steady to sell, heavy on the axles. A safe first run.",
                2, 12);

            Cargo(menu, description, LedgerBands[3],
                "{=CSR_Ledger_Cloth}Cloth and Wine",
                "{=CSR_Ledger_Cloth_Desc}Middling goods with real margins, the bread and butter of a working caravan.",
                1, 8);

            Cargo(menu, description, LedgerBands[4],
                "{=CSR_Ledger_Silk}Silk and Spice",
                "{=CSR_Ledger_Silk_Desc}A small fortune in fine goods, light on the wagons and heavy on every bandit's mind.",
                0, 4);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     The load nobody named, as a count rather than a list, because the
        ///     goods are drawn when the campaign opens and not while the chapter is
        ///     read. The number is the one the wagons are filled to, and the answer
        ///     is only offered where the markets carry that many kinds, so the line
        ///     cannot promise a stack the start will not put in the roster.
        /// </summary>
        private static string FactorEffect()
        {
            var text = new TextObject(
                "{=CSR_Panel_Ledger_Empty}Item: {COUNT} stacks of trade goods in your wagons, drawn at random from what the markets carry, in quantities that follow the purse you chose");
            text.SetTextVariable("COUNT", CaravanMasterScenario.LedgerStacks);
            return text.ToString();
        }

        /// <summary>
        ///     Wagons answered as empty. The list alone cannot say it: an unloaded
        ///     list is exactly what a chapter nobody answered leaves behind, and the
        ///     caravan start reads that as leave to buy stock, which is how this
        ///     answer came to hand out three stacks of cargo.
        /// </summary>
        private static void Nothing()
        {
            CreationSession.Current.CustomTradeGoods.Clear();
            CreationSession.Current.NoTradeGoods = true;
        }

        /// <summary>The load left to the factor: no cargo named, and none refused.</summary>
        private static void Factor()
        {
            CreationSession.Current.CustomTradeGoods.Clear();
            CreationSession.Current.NoTradeGoods = false;
        }

        /// <summary>
        ///     Empty wagons, said as the thing the player receives rather than as
        ///     the absence of a line. A panel with nothing in it is an option whose
        ///     effect the player has to infer, which a panel may never leave.
        /// </summary>
        private static string NothingEffect() =>
            new TextObject(
                    "{=CSR_Panel_Ledger_None}Item: nothing in your wagons at all, and the start buys no stock of its own to fill them")
                .ToString();

        /// <summary>
        ///     One load. The band and the count are named once: the pick writes the
        ///     wagons from them and the panel names the very items it wrote, which
        ///     is the whole difference between "middling goods" and the three
        ///     stacks the player will find in the inventory.
        ///
        ///     A world whose markets carry no trade goods has nothing to put in a
        ///     wagon, so the load is not offered. That is not a life being told it
        ///     may not claim a cargo, which the chapter's text would have to
        ///     explain: it is an answer with nothing behind it, and the same call
        ///     that would fill the wagons is what decides it, so the offer and the
        ///     load cannot disagree.
        /// </summary>
        private static void Cargo(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, int valueBand, int countPerItem)
        {
            Degree(menu, description, LedgerBands, band, Trade, titleKey, descKey,
                () => LoadCargoBand(valueBand, countPerItem),
                () => CargoEffect(CargoFor(valueBand, countPerItem)),
                () => CargoFor(valueBand, countPerItem).Count > 0);
        }

        /// <summary>
        ///     Loads three goods from a value band of the live item list (0 the
        ///     richest third, 2 the cheapest), so trade overhauls reshape the
        ///     cargo options automatically.
        /// </summary>
        private static void LoadCargoBand(int band, int countPerItem)
        {
            var session = CreationSession.Current;
            session.CustomTradeGoods.Clear();
            session.NoTradeGoods = false;

            foreach (var entry in CargoFor(band, countPerItem))
                session.CustomTradeGoods.Add(entry);
        }

        /// <summary>
        ///     What a band comes to, as the entries themselves. Whoever asks gets
        ///     the same three items: the pick puts them in the wagons and the panel
        ///     reads their names off, so the two cannot name different goods.
        /// </summary>
        private static List<InventoryEntry> CargoFor(int band, int countPerItem)
        {
            var loaded = new List<InventoryEntry>();

            var goods = ArmorQuery.TradeGoodItems();
            if (goods.Count == 0) return loaded;

            int third = Math.Max(1, goods.Count / 3);
            var slice = goods.Skip(band * third).Take(third).ToList();
            if (slice.Count == 0) slice = goods;

            for (int i = 0; i < Math.Min(3, slice.Count); i++)
            {
                int step = Math.Max(1, slice.Count / 3);
                var item = slice[Math.Min(slice.Count - 1, i * step)];
                loaded.Add(new InventoryEntry(item, countPerItem));
            }

            return loaded;
        }

        /// <summary>
        ///     The load by name. An empty one cannot reach here: the option that
        ///     carries this is only put where the same call returns goods.
        /// </summary>
        private static string CargoEffect(List<InventoryEntry> cargo)
        {
            var named = cargo.Select(entry =>
            {
                var one = new TextObject("{=CSR_Panel_Ledger_Stack}{COUNT} x {ITEM}");
                one.SetTextVariable("COUNT", entry.Count);
                one.SetTextVariable("ITEM", entry.Item.Name?.ToString() ?? entry.Item.StringId);
                return one.ToString();
            });

            var text = new TextObject(
                "{=CSR_Panel_Ledger_Cargo}Item: {CARGO} in your wagons, and the start adds no stock of its own on top");
            text.SetTextVariable("CARGO", string.Join(", ", named));
            return text.ToString();
        }

        /// <summary>
        ///     A proper train is never shut: a caravan that cannot put eight beasts
        ///     on the road is not a caravan, whatever the life behind it was.
        /// </summary>
        private static readonly LifeBand[] BeastsBands =
        {
            new("cs_beasts_few", 5, LifeGate.Open, 11,
                "{=CSR_Beasts_Few_Shut}A few sturdy mules: you have more goods promised than four animals could carry out of the gate."),
            new("cs_beasts_train", 9, LifeGate.Open, LifeGate.NoTop, "{=!}"),
            new("cs_beasts_caravan", 14, 9, LifeGate.NoTop,
                "{=CSR_Beasts_Caravan_Shut}A rolling caravan: nobody has fronted you enough trade to need fourteen beasts under load.")
        };

        private static void AddBeastsMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Beasts_Desc}Wagons move on legs. How many beasts of burden walk in your train?");
            var menu = GatedMenu("cs_beasts_menu", "{=CSR_Beasts_Title}The Beasts", description);

            Beasts(menu, description, BeastsBands[0],
                "{=CSR_Beasts_Few}A Few Sturdy Mules",
                "{=CSR_Beasts_Few_Desc}Four dependable animals. Enough to carry a living, not enough to slow you down.",
                4);

            Beasts(menu, description, BeastsBands[1],
                "{=CSR_Beasts_Train}A Proper Train",
                "{=CSR_Beasts_Train_Desc}Eight beasts under load, the shape of a serious operation.",
                8);

            Beasts(menu, description, BeastsBands[2],
                "{=CSR_Beasts_Caravan}A Rolling Caravan",
                "{=CSR_Beasts_Caravan_Desc}Fourteen animals and a dust cloud you can see from the walls. Everything you own moves with you.",
                14);

            manager.AddNewMenu(menu);
        }

        private static void Beasts(NarrativeMenu menu, TextObject description, LifeBand band,
            string titleKey, string descKey, int animals)
        {
            Degree(menu, description, BeastsBands, band, Trade, titleKey, descKey,
                () => CreationSession.Current.CustomPackAnimals = animals,
                () => MenuText.Count(animals,
                    "{=CSR_Panel_Beasts_One}Item: {COUNT} pack animal, in your train when the campaign opens",
                    "{=CSR_Panel_Beasts_Many}Item: {COUNT} pack animals, in your train when the campaign opens"));
        }

        #endregion

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
