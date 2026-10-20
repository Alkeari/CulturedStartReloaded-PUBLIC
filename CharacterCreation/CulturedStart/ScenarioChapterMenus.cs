using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     The scenario chapters: each start type's own story beats after the
    ///     shared origin, every choice writing the same session fields the Start
    ///     Editor writes, with magnitudes derived from the running game.
    /// </summary>
    public static class ScenarioChapterMenus
    {
        public static void AddChapterMenus(CharacterCreationManager manager)
        {
            AddRealmWarsMenu(manager);
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

        private static void Option(NarrativeMenu menu, string id, string titleKey, string descKey,
            Action onPick, Func<bool>? condition = null)
        {
            string chapterId = menu.StringId;
            var title = new TextObject(titleKey);
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

        #region Vassals and mercenaries: the realm's wars

        private static void AddRealmWarsMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_realm_wars_menu",
                "{=CSR_RealmWars_Title}The Realm's Wars",
                "{=CSR_RealmWars_Desc}The realm you serve has its own quarrels. What do its banners answer to on the day you join?");

            Option(menu, "cs_realm_wars_current",
                "{=CSR_RealmWars_Current}The Wars It Already Fights",
                "{=CSR_RealmWars_Current_Desc}History stands as it is: the realm keeps whatever wars the age has given it.",
                () => CreationSession.Current.RealmWars = null);

            Option(menu, "cs_realm_wars_peace",
                "{=CSR_RealmWars_Peace}A Rare Peace",
                "{=CSR_RealmWars_Peace_Desc}By treaty and exhaustion, the realm is at peace with every neighbor. It will not last, but it holds today.",
                () => CreationSession.Current.RealmWars = new List<string>());

            foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated)
                         .OrderBy(k => k.Name?.ToString() ?? ""))
            {
                var k = kingdom;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"cs_realm_wars_{k.StringId}",
                    new TextObject("{=CSR_RealmWars_Against}War with {KINGDOM_NAME}")
                        .SetTextVariable("KINGDOM_NAME", k.Name),
                    new TextObject("{=CSR_RealmWars_Against_Desc}Beyond its standing quarrels, the realm has taken up arms against {KINGDOM_NAME}. You arrive to a war camp, not a court.")
                        .SetTextVariable("KINGDOM_NAME", k.Name),
                    args => { },
                    m => k != CreationSession.Current.SelectedKingdom,
                    m =>
                    {
                        var session = CreationSession.Current;
                        var realm = session.SelectedKingdom;
                        var enemies = realm == null
                            ? new List<string>()
                            : Kingdom.All
                                .Where(other => other != realm && !other.IsEliminated &&
                                                FactionManager.IsAtWarAgainstFaction(realm, other))
                                .Select(other => other.StringId)
                                .ToList();
                        if (!enemies.Contains(k.StringId)) enemies.Add(k.StringId);
                        session.RealmWars = enemies;
                        var beat = new TextObject("{=CSR_RealmWars_Against}War with {KINGDOM_NAME}");
                        beat.SetTextVariable("KINGDOM_NAME", k.Name);
                        session.StoryBeats["cs_realm_wars_menu"] = beat.ToString();
                    },
                    m => { }));
            }

            manager.AddNewMenu(menu);
        }

        #endregion

        #region Mercenary: the contract

        private static void AddContractMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_contract_menu",
                "{=CSR_Contract_Title}The Contract",
                "{=CSR_Contract_Desc}The terms are read aloud before the seal is pressed. What did your name command at the table?");

            Option(menu, "cs_contract_modest",
                "{=CSR_Contract_Modest}A Modest Retainer",
                "{=CSR_Contract_Modest_Desc}You asked little, and were signed without a second glance. Small pay, but nobody watches you too closely.",
                () => CreationSession.Current.CustomContractPay = Math.Max(1,
                    GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom) * 3 / 4));

            Option(menu, "cs_contract_fair",
                "{=CSR_Contract_Fair}A Fair Wage",
                "{=CSR_Contract_Fair_Desc}The clerks offered what the realm offers any company of your standing, and you took it.",
                () => CreationSession.Current.CustomContractPay = null);

            Option(menu, "cs_contract_princely",
                "{=CSR_Contract_Princely}A Princely Sum",
                "{=CSR_Contract_Princely_Desc}You haggled like a horse trader and won double the going rate. The paymaster will remember your face, and not fondly.",
                () => CreationSession.Current.CustomContractPay =
                    GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom) * 2);

            manager.AddNewMenu(menu);
        }

        #endregion

        #region Outlaw: the crime

        private static void AddCrimeMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_crime_menu",
                "{=CSR_Crime_Title}The Crime",
                "{=CSR_Crime_Desc}Every outlaw's tale begins with the thing they did, or the thing they were blamed for. What is yours?");

            Option(menu, "cs_crime_poacher",
                "{=CSR_Crime_Poacher}A Poacher's Tale",
                "{=CSR_Crime_Poacher_Desc}You hunted a lord's forest to feed your own. A small crime with a small price, but enough to put you outside the law.",
                () => SetCrime(20, false));

            Option(menu, "cs_crime_brigand",
                "{=CSR_Crime_Brigand}A Brigand's Name",
                "{=CSR_Crime_Brigand_Desc}Roadside toll collecting of the unsanctioned kind. Merchants curse your name and the magistrates keep a file.",
                () => SetCrime(45, false));

            Option(menu, "cs_crime_scourge",
                "{=CSR_Crime_Scourge}Scourge of the Roads",
                "{=CSR_Crime_Scourge_Desc}Caravans rerouted to avoid you. Garrisons doubled their watches. The bounty on your head could buy a farm.",
                () => SetCrime(70, false));

            Option(menu, "cs_crime_infamous",
                "{=CSR_Crime_Infamous}Enemy of All Crowns",
                "{=CSR_Crime_Infamous_Desc}What you did is spoken of in every realm, and every realm wants you answering for it. There is nowhere left that does not know your face.",
                () => SetCrime(90, true));

            manager.AddNewMenu(menu);
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
                () => CreationSession.Current.CustomGarrison = null);

            Option(menu, "cs_fief_skeleton",
                "{=CSR_Fief_Skeleton}A Skeleton Watch",
                "{=CSR_Fief_Skeleton_Desc}A handful of gray veterans walk the walls. Cheap to keep, and a temptation to every rival with a ladder.",
                () => CreationSession.Current.CustomGarrison = GarrisonShare(4));

            Option(menu, "cs_fief_standing",
                "{=CSR_Fief_Standing}A Standing Garrison",
                "{=CSR_Fief_Standing_Desc}The walls are properly manned and the watch rotations full. A respectable strength for a holding of this size.",
                () => CreationSession.Current.CustomGarrison = GarrisonShare(2));

            Option(menu, "cs_fief_host",
                "{=CSR_Fief_Host}A Fortified Host",
                "{=CSR_Fief_Host_Desc}Your holding bristles with spears; the wage bill will bristle too. Nobody besieges this place lightly.",
                () => CreationSession.Current.CustomGarrison = GarrisonShare(4, 3));

            manager.AddNewMenu(menu);
        }

        private static int GarrisonShare(int denominator, int numerator = 1)
        {
            int limit = GameCaps.MaxGarrison(CreationSession.Current.SelectedSettlement);
            return Math.Max(0, limit * numerator / denominator);
        }

        #endregion

        #region Rebel clan: the rising

        private static void AddRisingMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_rising_menu",
                "{=CSR_Rising_Title}The Rising",
                "{=CSR_Rising_Desc}A rebellion is measured in the houses that dare to join it. Who rose with you?");

            Option(menu, "cs_rising_alone",
                "{=CSR_Rising_Alone}We Rise Alone",
                "{=CSR_Rising_Alone_Desc}No other house had the stomach for it. Your name alone carries the rebellion, and yours alone will answer for it.",
                () => CreationSession.Current.RebelAllyCount = 0);

            Option(menu, "cs_rising_one",
                "{=CSR_Rising_One}One House Sworn",
                "{=CSR_Rising_One_Desc}A single house took your hand and your cause. Two banners against a realm is thin company, but it is company.",
                () => CreationSession.Current.RebelAllyCount = 1);

            Option(menu, "cs_rising_two",
                "{=CSR_Rising_Two}Two Houses Sworn",
                "{=CSR_Rising_Two_Desc}Two noble houses rose beside you, with their own parties and their own grudges against the crown.",
                () => CreationSession.Current.RebelAllyCount = 2);

            Option(menu, "cs_rising_league",
                "{=CSR_Rising_League}A League of Rebels",
                "{=CSR_Rising_League_Desc}Four houses signed their names beneath yours. The realm calls it a conspiracy; you call it the beginning of something.",
                () => CreationSession.Current.RebelAllyCount = 4);

            manager.AddNewMenu(menu);
        }

        #endregion

        #region Monarch: traditions

        private static void AddTraditionsMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_traditions_menu",
                "{=CSR_Traditions_Title}Traditions of the Realm",
                "{=CSR_Traditions_Desc}A new crown still stands on old customs. Which traditions does your realm carry from its first day?");

            Option(menu, "cs_traditions_none",
                "{=CSR_Traditions_None}The Old Ways",
                "{=CSR_Traditions_None_Desc}No laws beyond custom; the realm begins unburdened, and every policy is a debate yet to come.",
                () => CreationSession.Current.SelectedPolicies.Clear());

            Option(menu, "cs_traditions_crown",
                "{=CSR_Traditions_Crown}An Iron Crown",
                "{=CSR_Traditions_Crown_Desc}The throne holds what the throne takes: sacred majesty, a royal guard, privilege and duty flowing to the crown.",
                () => SetPolicies(DefaultPolicies.SacredMajesty, DefaultPolicies.RoyalGuard,
                    DefaultPolicies.RoyalPrivilege, DefaultPolicies.CrownDuty));

            Option(menu, "cs_traditions_council",
                "{=CSR_Traditions_Council}A Council of Nobles",
                "{=CSR_Traditions_Council_Desc}The great houses share the burden and the spoils: a senate, a privy council, inheritance and retinues secured by law.",
                () => SetPolicies(DefaultPolicies.Senate, DefaultPolicies.LordsPrivyCouncil,
                    DefaultPolicies.FeudalInheritance, DefaultPolicies.NobleRetinues));

            Option(menu, "cs_traditions_commons",
                "{=CSR_Traditions_Commons}The Common Weal",
                "{=CSR_Traditions_Commons_Desc}The realm is its people: citizenship, tribunes, trial by jury, and the forgiveness of debts.",
                () => SetPolicies(DefaultPolicies.Citizenship, DefaultPolicies.TribunesOfThePeople,
                    DefaultPolicies.TrialByJury, DefaultPolicies.ForgivenessOfDebts));

            Option(menu, "cs_traditions_war",
                "{=CSR_Traditions_War}A Realm at Arms",
                "{=CSR_Traditions_War_Desc}Forged for war and governed like a camp: marshals, military coronae, war taxes, and land for veterans.",
                () => SetPolicies(DefaultPolicies.Marshals, DefaultPolicies.MilitaryCoronae,
                    DefaultPolicies.WarTax, Services.VersionedGameApi.LandGrantsForVeterans));

            manager.AddNewMenu(menu);
        }

        private static void SetPolicies(params PolicyObject?[] policies)
        {
            var session = CreationSession.Current;
            session.SelectedPolicies.Clear();
            foreach (var policy in policies)
                if (policy != null)
                    session.SelectedPolicies.Add(policy.StringId);
        }

        #endregion

        #region Monarch: the sworn houses

        private static void AddSwornHousesMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_sworn_houses_menu",
                "{=CSR_SwornHouses_Title}The Sworn Houses",
                "{=CSR_SwornHouses_Desc}A crown without vassals is a hat. Which houses knelt at your founding, and what did their oaths cost you?");

            Option(menu, "cs_sworn_none",
                "{=CSR_SwornHouses_None}The Crown Stands Alone",
                "{=CSR_SwornHouses_None_Desc}No house has knelt yet. Every castle stays in your hand, and every sword is one you pay for yourself.",
                () => SetVassals(0, false));

            Option(menu, "cs_sworn_two",
                "{=CSR_SwornHouses_Two}Two Loyal Houses",
                "{=CSR_SwornHouses_Two_Desc}Two noble houses swore at your coronation. The granted castles remain crown land; their loyalty rests on your promise of more.",
                () => SetVassals(2, false));

            Option(menu, "cs_sworn_four",
                "{=CSR_SwornHouses_Four}Four Houses, Lands Given",
                "{=CSR_SwornHouses_Four_Desc}Four houses knelt and rose as lords: the castles you seized passed to them as the price of their banners.",
                () => SetVassals(4, true));

            Option(menu, "cs_sworn_six",
                "{=CSR_SwornHouses_Six}A Court of Six",
                "{=CSR_SwornHouses_Six_Desc}Six houses swore, and the granted castles went with the oaths. A real court from the first day, fed by land that is no longer yours.",
                () => SetVassals(6, true));

            manager.AddNewMenu(menu);
        }

        private static void SetVassals(int count, bool grantLands)
        {
            var session = CreationSession.Current;
            session.VassalClanCount = count;
            session.GrantLandsToVassals = grantLands;
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
                () => CreationSession.Current.CustomWars = null);

            Option(menu, "cs_first_war_peace",
                "{=CSR_FirstWar_Peace}An Uneasy Peace",
                "{=CSR_FirstWar_Peace_Desc}The realms watch and wait. No army marches on you yet; every border is a held breath.",
                () => CreationSession.Current.CustomWars = new List<string>());

            foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated)
                         .OrderBy(k => k.Name?.ToString() ?? ""))
            {
                var k = kingdom;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"cs_first_war_{k.StringId}",
                    new TextObject("{=CSR_FirstWar_Against}{KINGDOM_NAME} Marches")
                        .SetTextVariable("KINGDOM_NAME", k.Name),
                    new TextObject("{=CSR_FirstWar_Against_Desc}{KINGDOM_NAME} has named your crown an insult and your land a prize. Your first war begins with your reign.")
                        .SetTextVariable("KINGDOM_NAME", k.Name),
                    args => { },
                    m => true,
                    m =>
                    {
                        CreationSession.Current.CustomWars = new List<string> { k.StringId };
                        var beat = new TextObject("{=CSR_FirstWar_Against}{KINGDOM_NAME} Marches");
                        beat.SetTextVariable("KINGDOM_NAME", k.Name);
                        CreationSession.Current.StoryBeats["cs_first_war_menu"] = beat.ToString();
                    },
                    m => { }));
            }

            manager.AddNewMenu(menu);
        }

        #endregion

        #region Commoner: the trade

        private static void AddTradeMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_trade_menu",
                "{=CSR_Trade_Title}The Trade",
                "{=CSR_Trade_Desc}A commoner's fortune is built with their hands. What do yours own when the story begins?");

            Option(menu, "cs_trade_labor",
                "{=CSR_Trade_Labor}Your Labor Alone",
                "{=CSR_Trade_Labor_Desc}No property, no ledger, no landlord's due. Everything you will own is still ahead of you.",
                () => CreationSession.Current.StartingWorkshops = 0);

            Option(menu, "cs_trade_workshop",
                "{=CSR_Trade_Workshop}The Family Workshop",
                "{=CSR_Trade_Workshop_Desc}A workshop in a nearby town carries your family's name, and now its keys are yours.",
                () => CreationSession.Current.StartingWorkshops = 1);

            Option(menu, "cs_trade_concern",
                "{=CSR_Trade_Concern}A Going Concern",
                "{=CSR_Trade_Concern_Desc}Two establishments and a reputation to match. The town knows your name before you draw a sword.",
                () => CreationSession.Current.StartingWorkshops = Math.Min(2,
                    Math.Max(1, GameCaps.MaxWorkshops(CreationSession.Current.EffectiveClanTier))),
                () => GameCaps.MaxWorkshops(CreationSession.Current.EffectiveClanTier) >= 2);

            manager.AddNewMenu(menu);
        }

        #endregion

        #region Caravan master: the ledger and the beasts

        private static void AddLedgerMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_ledger_menu",
                "{=CSR_Ledger_Title}The Ledger",
                "{=CSR_Ledger_Desc}A caravan is its cargo. What fills your wagons on the first page of the ledger?");

            Option(menu, "cs_ledger_empty",
                "{=CSR_Ledger_Empty}An Empty Wagon",
                "{=CSR_Ledger_Empty_Desc}You start with space and ambition. The first cargo will be bought with your own coin at the first market.",
                () =>
                {
                    // An empty cargo list reads as an unanswered chapter, which the
                    // scenario fills with three random stacks; the refusal is its own fact
                    CreationSession.Current.CustomTradeGoods.Clear();
                    CreationSession.Current.NoTradeGoods = true;
                });

            Option(menu, "cs_ledger_hides",
                "{=CSR_Ledger_Hides}Hides and Wool",
                "{=CSR_Ledger_Hides_Desc}Honest bulk goods: cheap to buy, steady to sell, heavy on the axles. A safe first run.",
                () => LoadCargoBand(2, 12));

            Option(menu, "cs_ledger_cloth",
                "{=CSR_Ledger_Cloth}Cloth and Wine",
                "{=CSR_Ledger_Cloth_Desc}Middling goods with real margins, the bread and butter of a working caravan.",
                () => LoadCargoBand(1, 8));

            Option(menu, "cs_ledger_silk",
                "{=CSR_Ledger_Silk}Silk and Spice",
                "{=CSR_Ledger_Silk_Desc}A small fortune in fine goods, light on the wagons and heavy on every bandit's mind.",
                () => LoadCargoBand(0, 4));

            manager.AddNewMenu(menu);
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

            var goods = ArmorQuery.TradeGoodItems();
            if (goods.Count == 0) return;

            int third = Math.Max(1, goods.Count / 3);
            var slice = goods.Skip(band * third).Take(third).ToList();
            if (slice.Count == 0) slice = goods;

            for (int i = 0; i < Math.Min(3, slice.Count); i++)
            {
                int step = Math.Max(1, slice.Count / 3);
                var item = slice[Math.Min(slice.Count - 1, i * step)];
                session.CustomTradeGoods.Add(new InventoryEntry(item, countPerItem));
            }
        }

        private static void AddBeastsMenu(CharacterCreationManager manager)
        {
            var menu = Menu("cs_beasts_menu",
                "{=CSR_Beasts_Title}The Beasts",
                "{=CSR_Beasts_Desc}Wagons move on legs. How many beasts of burden walk in your train?");

            Option(menu, "cs_beasts_few",
                "{=CSR_Beasts_Few}A Few Sturdy Mules",
                "{=CSR_Beasts_Few_Desc}Four dependable animals. Enough to carry a living, not enough to slow you down.",
                () => CreationSession.Current.CustomPackAnimals = 4);

            Option(menu, "cs_beasts_train",
                "{=CSR_Beasts_Train}A Proper Train",
                "{=CSR_Beasts_Train_Desc}Eight beasts under load, the shape of a serious operation.",
                () => CreationSession.Current.CustomPackAnimals = 8);

            Option(menu, "cs_beasts_caravan",
                "{=CSR_Beasts_Caravan}A Rolling Caravan",
                "{=CSR_Beasts_Caravan_Desc}Fourteen animals and a dust cloud you can see from the walls. Everything you own moves with you.",
                () => CreationSession.Current.CustomPackAnimals = 14);

            manager.AddNewMenu(menu);
        }

        #endregion

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
