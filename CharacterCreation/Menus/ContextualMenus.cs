using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Which realm your story is bound to, and which hall you hold. Both used to
    ///     be the whole game world in alphabetical order, every entry described with
    ///     the same sentence, which is a database on a stage that can only be read by
    ///     clicking. Each is a handful of written choices now, and the choice decides
    ///     the place rather than the player naming it: the panel names which place the
    ///     answer came out as, and the last option ranges over whatever the other
    ///     framings are not naming, so nothing on the map is out of reach of the
    ///     screen even though no single option reaches all of it.
    ///
    ///     On both menus an option names either one place or a kind of place, and
    ///     that decides whether asking again moves it. "The Richest Prize in Reach" is
    ///     one hall and stays that hall; "The Seat of Your Fathers" is any hall of your
    ///     own people, so choosing it again offers a different one of them, and the
    ///     last option ranges over everything left so nothing is out of reach. Both are
    ///     the same code path, and the same one the location menu uses: a candidate list
    ///     walked by <see cref="SettlementFinder.KeepLooking{T}"/>, where a list of one
    ///     never moves. An option whose list holds more than one says so in its own
    ///     panel, because a player is told what asking again will do rather than left
    ///     to discover it by clicking.
    ///
    ///     TWO FRAMINGS MAY NAME ONE PLACE, AND AN OPTION IS NEVER TAKEN OFF THE
    ///     SCREEN TO STOP THEM. Keeping them apart is worth doing and is tried first,
    ///     but it is a preference and never contradicting the life is a rule: a framing the reservations
    ///     have starved reads the whole map again and states what it landed on, which
    ///     another framing may also be naming. On a map holding one realm of your own
    ///     people, an empire monarch's first war otherwise lost the strongest crown and
    ///     his own kin at once, because the crown he unseated had answered both first.
    ///     The panel is composed from what the framing resolved to, so it follows.
    ///
    ///     NO NUMBER, NAME OR COUNT IS RENDERED IN AN OPTION'S DESCRIPTION. The
    ///     description is the fiction and the panel is the data, and every fact
    ///     that used to be appended to the prose (which realm the framing came out
    ///     as, what it is fighting, how many halls follow, why no hall is offered,
    ///     that clicking again moves the answer) is a labeled entry in the panel
    ///     instead. The descriptions still refresh on every render, because that
    ///     refresh is what reserves a realm or a hall so no two framings land on
    ///     one; it simply writes no variables any more.
    /// </summary>
    public static class ContextualMenus
    {
        // What each realm option is offering right now. Read so that no two
        // framings land on the same realm; written as each one resolves.
        private static readonly Dictionary<string, Kingdom> ShowingRealms = new();

        // Which realm option the player is standing on. Clicking the one already
        // selected is what asks it for another realm; arriving on it from another
        // option shows what it was already offering.
        private static string? _lastRealm;

        // Realm options whose kind of realm the map cannot answer, so the reason is
        // logged once rather than on every render.
        private static readonly HashSet<string> WarnedRealms = new();

        // What each holding option is offering right now. Read so that no two
        // framings land on the same hall; written as each one resolves.
        private static readonly Dictionary<string, Settlement> ShowingHoldings = new();

        // Which holding option the player is standing on. Clicking the one already
        // selected is what asks it for somewhere else; arriving on it from another
        // option shows what it was already offering.
        private static string? _lastHolding;

        // Holding options whose kind of hall the map cannot answer, so the reason is
        // logged once rather than on every render.
        private static readonly HashSet<string> WarnedHoldings = new();

        public static void AddContextualMenus(CharacterCreationManager manager)
        {
            AddKingdomMenu(manager);
            AddSettlementMenu(manager);
        }

        /// <summary>
        ///     Forgets what was offered, so a new session starts clean. Both menus walk
        ///     through <see cref="SettlementFinder.KeepLooking{T}"/>, so the walks
        ///     themselves are forgotten by <see cref="SettlementFinder.Reset"/>, which
        ///     the session calls alongside this.
        /// </summary>
        public static void Reset()
        {
            ShowingRealms.Clear();
            WarnedRealms.Clear();
            _lastRealm = null;
            ShowingHoldings.Clear();
            WarnedHoldings.Clear();
            _lastHolding = null;
        }

        #region Realm

        private static void AddKingdomMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_kingdom_select",
                CreationFlow.DeclaredPrevious("cs_kingdom_select"),
                CreationFlow.DeclaredNext("cs_kingdom_select"),
                new TextObject("{=CSR_Kingdom_Title_Revamped}The realm you are bound to"),
                new TextObject(
                    "{=CSR_Kingdom_Desc_Revamped}Every road ends at somebody's border. Whose banner does your story hang from: your liege, your patron, or your enemy?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddRealm(menu, "cs_kingdom_own",
                "{=CSR_Kingdom_Own}Your Own People",
                "{=CSR_Kingdom_Own_Desc}You speak their language without an accent and you know what their lords expect. That is worth more than it sounds.",
                OfYourOwnPeople);

            AddRealm(menu, "cs_kingdom_strong",
                "{=CSR_Kingdom_Strong}The strongest banner in the field",
                "{=CSR_Kingdom_Strong_Desc}If you are going to be somebody's man, be the winning somebody's man.",
                Strongest);

            AddRealm(menu, "cs_kingdom_weak",
                "{=CSR_Kingdom_Weak}The one that needs you most",
                "{=CSR_Kingdom_Weak_Desc}A realm with its back to the wall does not ask many questions about where you came from.",
                Weakest);

            AddRealm(menu, "cs_kingdom_strangers",
                "{=CSR_Kingdom_Strangers}A Realm of Strangers",
                "{=CSR_Kingdom_Strangers_Desc}Nobody there knows your family or what it did. That is the point.",
                OfStrangers);

            // The option that ranges over every realm the four above are not naming,
            // which is what lets them stay written rather than becoming a list of
            // every banner in the world
            AddRealm(menu, "cs_kingdom_roll",
                "{=CSR_Kingdom_Roll}Whoever Will Have You",
                "{=CSR_Kingdom_Roll_Line}Ask around, and take the next name you hear.",
                AnyRealmAtAll);

            manager.AddNewMenu(menu);
        }

        private static void AddRealm(NarrativeMenu menu, string id, string titleKey, string descKey,
            Func<IReadOnlyList<Kingdom>, CultureObject?, IReadOnlyList<Kingdom>> band)
        {
            var description = new TextObject(descKey);
            var captured = band;

            ChoiceEffects.Declare(id, () =>
            {
                var offered = OfferRealm(id, captured, false, out int choices);
                return RealmEffect(offered, choices);
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description,
                    // The description quotes nothing, but this is still the only
                    // hook the API runs on every render, and what it runs is what
                    // reserves this framing's realm so no other lands on it
                    unused => OfferRealm(id, captured, false, out _),
                    () => OfferRealm(id, captured, false, out _) != null),
                m => ChooseRealm(id, captured),
                m => { }
            ));
        }

        /// <summary>
        ///     What binding this realm does to the character, in the realm the
        ///     framing came out as: the same <see cref="OfferRealm"/> reading the
        ///     click will bind, so the panel cannot name one banner and the session
        ///     carry another.
        ///
        ///     What "bound" means is the start's own, because a vassal swears to a
        ///     realm and an outlaw is hunted by one. The wars are the realm's and
        ///     are read off the campaign here, which is where a player weighing the
        ///     answer meets them; whether the halls offered later are this realm's
        ///     is asked of the flow table rather than restated.
        /// </summary>
        private static string RealmEffect(Kingdom? kingdom, int choices)
        {
            if (kingdom == null)
                return new TextObject(
                    "{=CSR_Panel_Realm_NoneAnswers}Realm: none on the map answers to this").ToString();

            var bound = new TextObject(BindingKey());
            bound.SetTextVariable("REALM", kingdom.Name);

            string? hall = CreationFlow.Includes(CreationSession.Current, "cs_settlement_select")
                ? HallsUnder(kingdom)
                : null;

            return ChoiceEffects.Stated(bound.ToString(), Wars(kingdom), hall,
                choices > 1 ? Again(true) : null);
        }

        /// <summary>
        ///     The realm's wars as one entry, and whether they become the
        ///     character's own. A start that joins a realm takes the wars it
        ///     already has; a start that stands against one is its enemy outright
        ///     and the Realm entry has already said so.
        /// </summary>
        private static string? Wars(Kingdom kingdom)
        {
            bool inherited = CreationSession.Current.SelectedStartType
                is StartType.LandedVassal or StartType.LandlessVassal or StartType.Mercenary;

            string? enemies = WarSummary(kingdom);
            if (enemies == null)
                // An unread war is not a peace, so nobody is named. That the wars
                // become the character's own is a property of the start rather than
                // of the stances, and is still owed
                return inherited
                    ? new TextObject(
                        "{=CSR_Panel_Realm_WarsUnread}Wars: whatever it is fighting, yours from the day the campaign opens").ToString()
                    : null;

            var text = new TextObject(inherited
                ? "{=CSR_Panel_Realm_WarsYours}Wars: {ENEMIES}, yours from the day the campaign opens"
                : "{=CSR_Panel_Realm_WarsTheirs}Wars: {ENEMIES}");
            text.SetTextVariable("ENEMIES", enemies);
            return text.ToString();
        }

        /// <summary>
        ///     What the holding chapter will have to offer once this realm is bound,
        ///     asked of the same pool that chapter reads rather than restated here:
        ///     the realm is put on the session for the length of the question and
        ///     put back.
        ///
        ///     Whether those halls are this realm's own is a property of the pool, so
        ///     it is read off the pool. A landed vassal takes a hall of the realm he
        ///     swears to and an outlawed lord may be sitting in anybody's, and a
        ///     sentence that promised one realm's halls to both was false for the
        ///     outlaw every time.
        /// </summary>
        private static string? HallsUnder(Kingdom kingdom)
        {
            var session = CreationSession.Current;
            var was = session.SelectedKingdom;
            IReadOnlyList<Settlement> pool;

            try
            {
                session.SelectedKingdom = kingdom;
                pool = EligibleHoldings();
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ContextualMenus: reading the halls under {kingdom.StringId} failed: {ex.Message}");
                return null;
            }
            finally
            {
                session.SelectedKingdom = was;
            }

            if (pool.Count == 0)
                return new TextObject(
                    "{=CSR_Panel_Realm_NoHalls}Halls next: none, and any hall you had chosen is given up").ToString();

            return pool.All(s => s.OwnerClan?.Kingdom == kingdom)
                ? MenuText.Count(pool.Count,
                    "{=CSR_Panel_Realm_HallOwn}Halls next: {COUNT}, this realm's own, and any hall you had chosen is given up",
                    "{=CSR_Panel_Realm_HallsOwn}Halls next: {COUNT}, all this realm's own, and any hall you had chosen is given up")
                : MenuText.Count(pool.Count,
                    "{=CSR_Panel_Realm_HallAny}Halls next: {COUNT}, and any hall you had chosen is given up",
                    "{=CSR_Panel_Realm_HallsAny}Halls next: {COUNT}, and any hall you had chosen is given up");
        }

        /// <summary>What this start's binding to a realm actually is.</summary>
        private static string BindingKey() =>
            CreationSession.Current.SelectedStartType switch
            {
                StartType.LandedVassal =>
                    "{=CSR_Panel_Realm_Vassal}Realm: {REALM}, sworn to it, and your fief is one of its own",
                StartType.LandlessVassal =>
                    "{=CSR_Panel_Realm_Landless}Realm: {REALM}, sworn to it with no land of your own",
                StartType.Mercenary =>
                    "{=CSR_Panel_Realm_Mercenary}Realm: {REALM}, your company under contract to it",
                StartType.Outlaw =>
                    "{=CSR_Panel_Realm_Outlaw}Realm: {REALM}, the crown that outlawed you and holds your writ",
                StartType.RebelClan =>
                    "{=CSR_Panel_Realm_Rebel}Realm: {REALM}, in open revolt against it, holding a castle of its own",
                _ => "{=CSR_Panel_Realm_Bound}Realm: {REALM}"
            };

        /// <summary>
        ///     A click on a realm option.
        ///
        ///     The engine runs onCondition only while it is building the menu's option
        ///     list, which happens once on entry, so a description that quotes a live
        ///     value from there is frozen for as long as the player stands on the
        ///     screen. It is pushed again here because the option's own RefreshValues
        ///     runs immediately after onSelect and re-reads the same TextObject, which
        ///     makes this the only place a re-click can change what the player reads.
        ///     Without it the roll bound a fresh realm on every click and went on
        ///     naming the first one it ever offered, so the panel named one realm and
        ///     the character was bound to another.
        ///
        ///     The description quotes nothing now, so what a re-click has to move is
        ///     the reservation and the binding. The panel is rebuilt from the
        ///     reservation on the next tick of the stage and follows on its own.
        /// </summary>
        private static void ChooseRealm(string id,
            Func<IReadOnlyList<Kingdom>, CultureObject?, IReadOnlyList<Kingdom>> band)
        {
            BindRealm(OfferRealm(id, band, AskingAgain(id), out _));
        }

        /// <summary>
        ///     Whether this click is the player asking a realm option for another realm
        ///     rather than arriving on it from somewhere else. Clicking the option
        ///     already selected is what moves it, so the answer is which option was
        ///     standing selected before this one.
        ///
        ///     Internal because the monarch's first war asks the same question of the
        ///     same options: two records of where the player is standing would answer
        ///     it differently the moment the player moved between the two menus.
        /// </summary>
        internal static bool AskingAgain(string id)
        {
            bool asking = _lastRealm == id;
            _lastRealm = id;
            return asking;
        }

        /// <summary>
        ///     What this option is offering, and with <paramref name="advance"/> the
        ///     next realm that answers it instead. <paramref name="choices"/> is how
        ///     many realms answer the framing at all, which is what decides whether
        ///     the panel promises the player another one.
        /// </summary>
        internal static Kingdom? OfferRealm(string id,
            Func<IReadOnlyList<Kingdom>, CultureObject?, IReadOnlyList<Kingdom>> band, bool advance,
            out int choices)
        {
            choices = 0;
            try
            {
                var eligible = EligibleRealms();
                if (eligible.Count == 0) return null;

                var culture = CreationSession.Current.SelectedCulture;

                // No two framings land on the same realm where the map can avoid it:
                // the strongest banner in the field and a realm of strangers were both
                // the Aserai for every culture but their own, since the largest realm
                // on the map is foreign to almost everybody. What the other options
                // hold comes out of the pool before this framing reads it
                var taken = ShowingRealms.Where(p => p.Key != id).Select(p => p.Value).ToList();
                var unheld = taken.Count == 0
                    ? eligible
                    : eligible.Where(k => !taken.Contains(k)).ToList();

                var candidates = unheld.Count == 0
                    ? NoRealms
                    : band(unheld, culture);

                // Where it cannot, the framing reads the whole map instead and names a
                // realm another framing is already naming. That is a small map rather
                // than a contradiction, and the other trade is never made: an
                // empire monarch lost both the strongest crown and his own kin at
                // cs_first_war_menu because one realm answered all three framings
                bool shared = false;
                if (candidates.Count == 0)
                {
                    candidates = band(eligible, culture);
                    shared = candidates.Count > 0;
                }

                if (candidates.Count == 0) WarnRealm(id);
                choices = candidates.Count;

                var picked = SettlementFinder.KeepLooking(id, candidates, advance);

                // A framing that only found its answer by reading past the ledger does
                // not then write to it. Recording a shared realm would shrink the
                // neighbor's own candidate list on the next render, and the neighbor
                // would jump to somewhere else under a player who had touched nothing
                if (picked == null || shared) ShowingRealms.Remove(id);
                else ShowingRealms[id] = picked;
                return picked;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"ContextualMenus: resolving {id} failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Says once why an option is not on the screen. A culture no realm on the
        ///     map shares is ordinary under a total conversion, and an option silently
        ///     missing is indistinguishable from one that was never written.
        ///
        ///     It fires only for a framing the WHOLE map cannot answer, never for one
        ///     another framing had merely got to first. A warning that fires on correct
        ///     behavior teaches a reader to skip warnings, and this one used to fire
        ///     twice on an ordinary empire monarch.
        /// </summary>
        private static void WarnRealm(string id)
        {
            if (!WarnedRealms.Add(id)) return;

            string culture = CreationSession.Current.SelectedCulture?.StringId ?? "no culture";
            CSLogger.Warn($"ContextualMenus: no realm on the map answers {id} " +
                          $"for {culture}; the option is not offered.");
        }

        private static void BindRealm(Kingdom? kingdom)
        {
            if (kingdom == null) return;

            CreationSession.Current.SelectedKingdom = kingdom;
            CreationSession.Current.SelectedSettlement = null;
            CSLogger.Info($"Realm selected: {kingdom.Name}");
        }

        /// <summary>
        ///     Every realm a player could take the throne of: one that still
        ///     stands and still holds land.
        ///
        ///     The land is the point rather than a nicety. A crown inherited comes
        ///     with the realm's own holdings, and the seat the start grants is
        ///     taken out of them, so a realm holding nothing would be a row the
        ///     player can select and nothing can answer. That is the shape that
        ///     once offered a monarch "A Castle and Its Garrison" on a pool of
        ///     towns only.
        /// </summary>
        internal static IReadOnlyList<Kingdom> RealmsWithAThrone()
        {
            return Kingdom.All
                .Where(k => !k.IsEliminated && Settlement.All.Any(
                    s => (s.IsTown || s.IsCastle) && s.OwnerClan?.Kingdom == k))
                .OrderBy(k => k.StringId, StringComparer.Ordinal)
                .ToList();
        }

        internal static IReadOnlyList<Kingdom> EligibleRealms()
        {
            var session = CreationSession.Current;
            return Kingdom.All
                .Where(k => !k.IsEliminated && SettlementFinder.RealmCanServe(k, session.SelectedStartType) &&
                            ServesTheSameSide(k))
                .OrderBy(k => k.StringId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        ///     Whether a realm stands on the same side of the world as the character does. A total
        ///     conversion that divides its factions enforces that division after the campaign opens,
        ///     refusing recruitment and marriage across it, so a start that swore a Gondorian to
        ///     Mordor would be a start the world then fights. Without such a conversion every realm
        ///     answers the same and nothing is withheld.
        /// </summary>
        internal static bool ServesTheSameSide(Kingdom kingdom) =>
            Services.TaomBridge.SameSide(
                CreationSession.Current.SelectedCulture?.StringId, kingdom.Culture?.StringId);

        /// <summary>How much of the map the realm holds, which is what makes it strong.</summary>
        internal static int Holdings(Kingdom kingdom) =>
            kingdom.Fiefs?.Count ?? 0;

        private static readonly Kingdom[] NoRealms = Array.Empty<Kingdom>();

        /// <summary>
        ///     A kind of realm: any realm of your own people. This used to rank them by
        ///     how much they hold and hand back the largest, which answered a question
        ///     about whose realm it is with an answer about how big it is, so two of the
        ///     three Imperial realms could never be offered here at all.
        /// </summary>
        internal static IReadOnlyList<Kingdom> OfYourOwnPeople(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            if (culture == null) return NoRealms;
            return pool.Where(k => k.Culture == culture).ToList();
        }

        /// <summary>
        ///     One realm: the largest on the map, which is what the wording names.
        ///     Measured over every realm this start can reach rather than over what is
        ///     left once the other framings have taken theirs, because the second
        ///     largest is not the strongest banner in the field and saying so is a
        ///     false panel. When the largest is already spoken for this hands back
        ///     nothing and <see cref="OfferRealm"/> asks again over the whole map.
        /// </summary>
        private static IReadOnlyList<Kingdom> Strongest(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var top = EligibleRealms().OrderByDescending(Holdings).FirstOrDefault();
            return top != null && pool.Contains(top) ? new[] { top } : NoRealms;
        }

        /// <summary>One realm: the smallest on the map, read as <see cref="Strongest"/> is.</summary>
        private static IReadOnlyList<Kingdom> Weakest(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            var bottom = EligibleRealms().OrderBy(Holdings).FirstOrDefault();
            return bottom != null && pool.Contains(bottom) ? new[] { bottom } : NoRealms;
        }

        /// <summary>
        ///     A kind of realm: any whose people are not yours. How much it holds has
        ///     nothing to do with the framing, and ranking by it made this the largest
        ///     foreign realm and nothing else, which is the realm the option above it
        ///     was already offering.
        /// </summary>
        private static IReadOnlyList<Kingdom> OfStrangers(
            IReadOnlyList<Kingdom> pool, CultureObject? culture)
        {
            if (culture == null) return NoRealms;
            return pool.Where(k => k.Culture != culture).ToList();
        }

        /// <summary>A kind of realm: any at all that no other option is naming.</summary>
        private static IReadOnlyList<Kingdom> AnyRealmAtAll(
            IReadOnlyList<Kingdom> pool, CultureObject? culture) => pool;

        /// <summary>
        ///     Who the realm this option resolves to is fighting, read from the
        ///     campaign on every render, as the bare value of a Wars entry. A
        ///     vassal, a mercenary and a landless lord do not decide their realm's
        ///     wars: the realm they pick here is the last word on them, so the wars
        ///     have to be an input to that pick rather than news after it, and the
        ///     panel is where that pick is weighed.
        ///
        ///     Past two enemies the value states how many rather than who. Five
        ///     options each carrying a list of banners is the map in prose, which is
        ///     the thing the written framings exist to avoid, and a realm fighting
        ///     four wars has already said what it needs the player for.
        ///
        ///     Null when the stances cannot be read, and the panel then carries no
        ///     Wars entry at all: an unread war is not a peace, and printing one as
        ///     the other is worse than saying nothing about it.
        /// </summary>
        internal static string? WarSummary(Kingdom kingdom)
        {
            try
            {
                var enemies = Kingdom.All
                    .Where(k => k != kingdom && !k.IsEliminated
                                && FactionManager.IsAtWarAgainstFaction(kingdom, k))
                    .OrderBy(k => k.StringId, StringComparer.Ordinal)
                    .ToList();

                switch (enemies.Count)
                {
                    case 0:
                        return new TextObject("{=CSR_Kingdom_WarsNone}none").ToString();

                    case 1:
                        return enemies[0].Name?.ToString() ?? enemies[0].StringId;

                    case 2:
                        var pair = new TextObject("{=CSR_Kingdom_WarsTwo}{ENEMY} and {OTHER}");
                        pair.SetTextVariable("ENEMY", enemies[0].Name);
                        pair.SetTextVariable("OTHER", enemies[1].Name);
                        return pair.ToString();

                    default:
                        var many = new TextObject("{=CSR_Kingdom_WarsMany}{COUNT} realms at once");
                        many.SetTextVariable("COUNT", enemies.Count);
                        return many.ToString();
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error($"ContextualMenus: the wars of {kingdom.StringId} could not be read.", ex);
                return null;
            }
        }

        #endregion

        #region Holding

        private static void AddSettlementMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_settlement_select",
                CreationFlow.DeclaredPrevious("cs_settlement_select"),
                CreationFlow.DeclaredNext("cs_settlement_select"),
                new TextObject("{=CSR_Settlement_Title}Choose Your Holding"),
                new TextObject(
                    "{=CSR_Settlement_Desc_Revamped}A hall, its walls, and everyone inside them. What kind of place did your story leave you sitting in?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddHolding(menu, "cs_holding_fathers",
                "{=CSR_Holding_Fathers}The Seat of Your Fathers",
                "{=CSR_Holding_Fathers_Desc}Your own people, your own tongue, and a hall that has had your name on it for longer than anyone can prove.",
                OfYourOwnPeople, HoldsByRight);

            AddHolding(menu, "cs_holding_prize",
                "{=CSR_Holding_Prize}The Richest Prize in Reach",
                "{=CSR_Holding_Prize_Desc}Full granaries, busy streets, and a great many people with opinions about you.",
                TheRichest, HoldsByRight);

            AddHolding(menu, "cs_holding_quiet",
                "{=CSR_Holding_Quiet}A Hall Nobody Wanted",
                "{=CSR_Holding_Quiet_Desc}Thin walls, thinner coffers, and no one important watching what you do with it.",
                NobodyWanted, HoldsByRight);

            AddHolding(menu, "cs_holding_taken",
                "{=CSR_Holding_Taken}Taken from Another People",
                "{=CSR_Holding_Taken_Desc}They were not yours a generation ago and they have not forgotten it. Neither have you.",
                TakenFromAnother, HoldsByRight);

            // The two kinds of hall the game actually has, so a player who wants a
            // town or wants walls can ask for one instead of hoping the framing
            // above lands on it. They sit under the written framings and above the
            // catch-all, which goes on ranging over whatever is left
            AddHolding(menu, "cs_holding_town",
                "{=CSR_Holding_Town}A Town and Its Market",
                "{=CSR_Holding_Town_Desc}Merchants, a market, and more people than you will ever learn the names of. What your story leaves you holding is a town.",
                AnyTown, HoldsByRight);

            AddHolding(menu, "cs_holding_castle",
                "{=CSR_Holding_Castle}A Castle and Its Garrison",
                "{=CSR_Holding_Castle_Desc}Walls, a gate, and a garrison that eats whether or not there is a war on. What your story leaves you holding is a castle.",
                AnyCastle, HoldsByRight);

            AddHolding(menu, "cs_holding_roll",
                "{=CSR_Holding_Roll}Somewhere You Have Never Been",
                "{=CSR_Holding_Roll_Line}Somewhere you have never been, chosen off a map by someone who has not been there either.",
                AnyHallAtAll, HoldsByRight);

            // An outlawed lord: a lord with a hall and a price on his head, which is not
            // the brigand the rest of this start is written for. His framings are his own
            // rather than the ones above, because a vassal's hall was granted to him and
            // an outlaw's is one he is still sitting in. The road stands first, since
            // holding nothing is what an outlaw ordinarily holds.
            AddNoHall(menu);

            AddHolding(menu, "cs_holding_outlaw_seat",
                "{=CSR_Holding_OutlawSeat}The Seat You Never Left",
                "{=CSR_Holding_OutlawSeat_Desc}The hall your house held before the writ went out, and you are sitting in it yet. The crown that named you knows the road to your gate.",
                TheHallTheyWantBack, OutlawMayHoldAHall);

            AddHolding(menu, "cs_holding_outlaw_far",
                "{=CSR_Holding_OutlawFar}A Hall Beyond Their Reach",
                "{=CSR_Holding_OutlawFar_Desc}Walls under a crown that has no writ against you and no reason to honor anyone else's. Distance is the whole of what recommends it.",
                WhereTheWritDoesNotRun, OutlawMayHoldAHall);

            AddHolding(menu, "cs_holding_outlaw_kin",
                "{=CSR_Holding_OutlawKin}Walls of Your Own People",
                "{=CSR_Holding_OutlawKin_Desc}Your own tongue on the walls and your own people in the yard. Whatever the law calls you, here you are still theirs.",
                OfYourOwnPeople, OutlawMayHoldAHall);

            AddHolding(menu, "cs_holding_outlaw_ruin",
                "{=CSR_Holding_OutlawRuin}The Ruin Nobody Garrisons",
                "{=CSR_Holding_OutlawRuin_Desc}A hold too poor to be worth a siege and too broken to be worth a governor. Nobody has counted its stones in years.",
                NobodyWanted, OutlawMayHoldAHall);

            AddHolding(menu, "cs_holding_outlaw_anywhere",
                "{=CSR_Holding_OutlawAnywhere}Wherever the Law Is Not",
                "{=CSR_Holding_OutlawAnywhere_Desc}Any wall still standing that no writ has reached, picked off a map by someone who has never been there either.",
                AnyHallAtAll, OutlawMayHoldAHall);

            // What the stewards will be choosing FROM is the same pool every
            // framing above reads, so the count is the reach of this answer and
            // states what "somewhere suitable" is actually ranging over
            ChoiceEffects.Declare("cs_settlement_auto", () =>
            {
                int reach;
                try
                {
                    reach = StewardsReach();
                }
                catch (Exception ex)
                {
                    // The panel is drawn on every tick of the stage and may not
                    // throw out of one: a failure here is the blank panel the rule
                    // forbids, so an unreadable map states the rule without a count
                    CSLogger.Warn($"ContextualMenus: counting the halls the stewards could assign failed: {ex.Message}");
                    return ChoiceEffects.Stated(
                        new TextObject(
                            "{=CSR_Panel_Holding_AutoUnknown}Hall: one fitting your beginning and your culture, chosen for you when the campaign opens").ToString(),
                        AtItsGate());
                }

                if (reach == 0)
                    return new TextObject(
                        "{=CSR_Panel_Holding_AutoNone}Hall: none, since no hall on the map suits your beginning").ToString();

                return ChoiceEffects.Stated(
                    MenuText.Count(reach,
                        "{=CSR_Panel_Holding_AutoOne}Hall: the one your beginning can hold, chosen for you when the campaign opens",
                        "{=CSR_Panel_Holding_Auto}Hall: one of the {COUNT} your beginning can hold, chosen for you when the campaign opens"),
                    AtItsGate());
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_settlement_auto",
                new TextObject("{=CSR_Settlement_Auto}Let Stewards Assign"),
                new TextObject(
                    "{=CSR_Settlement_Auto_Desc_Revamped}Somewhere suitable for your station and your culture. You will find out where when you get there."),
                args => { },
                m => CreationSession.Current.SelectedStartType
                    is StartType.Monarch or StartType.LandedVassal or StartType.RebelClan,
                m =>
                {
                    _lastHolding = "cs_settlement_auto";
                    CreationSession.Current.SelectedSettlement = null;
                    CSLogger.Info("Settlement selection set to auto");
                },
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One written framing on the holding menu. <paramref name="shows"/> is which
        ///     starts the framing was written for, asked on every render because the start
        ///     type is not known when the menu is built.
        ///
        ///     A framing whose start is not this one resolves nothing at all rather than
        ///     resolving invisibly: <see cref="OfferHolding"/> records what every framing is
        ///     holding so no two land on one hall, so a hidden framing that still resolved
        ///     would reserve halls away from the framings the player can actually see.
        /// </summary>
        /// <summary>
        ///     The outlaw's way off this screen, and his ordinary answer: no walls at all.
        ///     The panel says WHY no halls stand beside it when the crime the player chose
        ///     is what rules them out, because a screen holding one option and no reason
        ///     reads as a screen that failed to load. That reason used to be appended to
        ///     the prose, which is the defect this pass exists to end: it is a condition of
        ///     the character, so it is an entry.
        ///
        ///     The render also drops a hall taken earlier in the same session. The crime
        ///     chapter sits ahead of this menu, so a player who walks back, raises the
        ///     crime past the point a realm marches, and walks forward again would
        ///     otherwise arrive here still holding a hall no framing would now offer him.
        /// </summary>
        private static void AddNoHall(NarrativeMenu menu)
        {
            var description = new TextObject(
                "{=CSR_Holding_OutlawNone_Desc}No walls, no gate, and no rents to collect. What you hold is what rides with you.");

            ChoiceEffects.Declare("cs_holding_outlaw_none", () => ChoiceEffects.Stated(
                new TextObject(
                    "{=CSR_Panel_Holding_None}Hall: none, so no walls, no garrison to feed and no rents to collect").ToString(),
                new TextObject(OutlawMayHoldAHall()
                    ? "{=CSR_Panel_Holding_NoneChoice}Instead: any hall below, and taking one gives this up"
                    : "{=CSR_Panel_Holding_NoneHunted}Why: at the crime rating you chose a crown would march on any hall you sat in").ToString()));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_holding_outlaw_none",
                new TextObject("{=CSR_Holding_OutlawNone}Nothing but the Road"),
                description,
                args => { },
                MenuText.Live(description,
                    // The description quotes nothing; what this render still owes is
                    // the hall a raised crime rating has just taken off the table
                    unused =>
                    {
                        if (!OutlawMayHoldAHall() &&
                            CreationSession.Current.SelectedStartType == StartType.Outlaw)
                            CreationSession.Current.SelectedSettlement = null;
                    },
                    () => CreationSession.Current.SelectedStartType == StartType.Outlaw),
                m =>
                {
                    _lastHolding = "cs_holding_outlaw_none";
                    CreationSession.Current.SelectedSettlement = null;
                    CSLogger.Info("Holding selected: none; the outlaw keeps the road.");
                },
                m => { }
            ));
        }

        /// <summary>
        ///     Whether this start holds its hall by right: a crown's own seat, a fief a
        ///     liege granted, or a castle seized in open rebellion. Every standing framing
        ///     on this menu is worded for one of those three.
        /// </summary>
        private static bool HoldsByRight() =>
            CreationSession.Current.SelectedStartType != StartType.Outlaw;

        /// <summary>
        ///     Whether an outlawed lord may be offered a hall at all, and the single rule
        ///     that decides it, asked here and again by
        ///     <see cref="Services.Application.Scenarios.OutlawScenario"/> so the screen and
        ///     the grant cannot disagree.
        ///
        ///     The bar is the game's own. At the threshold the engine's
        ///     <c>ChangeCrimeRatingAction</c> declares war on the realm's behalf whenever the
        ///     player leads their own map faction, which an outlaw in no kingdom always does.
        ///     An independent clan at war with the kingdom whose fiefs ring its gate is
        ///     besieged rather than played, so under the bar a hall is a start and at or over
        ///     it a hall is an hour of campaign. Strictly under, because v1.3.15 fires above
        ///     the threshold and v1.5.2 fires at it.
        /// </summary>
        public static bool OutlawMayHoldAHall(int crimeRating)
        {
            float threshold = WarThreshold();
            return threshold > 0f && crimeRating < threshold;
        }

        private static bool OutlawMayHoldAHall() =>
            CreationSession.Current.SelectedStartType == StartType.Outlaw &&
            OutlawMayHoldAHall(OutlawCrime(CreationSession.Current));

        /// <summary>
        ///     The crime rating this start will carry, read the way
        ///     <see cref="Services.Application.Scenarios.OutlawScenario"/> reads it: the
        ///     chapter's answer, else the configured default.
        /// </summary>
        private static int OutlawCrime(CharacterCreationSession session) =>
            Math.Max(0, session.CustomCrimeRating
                        ?? GlobalSettings<CSSettings>.Instance?.OutlawCrimeRating ?? 50);

        /// <summary>
        ///     The rating at which the game goes to war on the player's own faction, read
        ///     from the installed model so a conversion that moves it is obeyed. Zero when
        ///     it cannot be read, which every caller treats as do not risk it.
        /// </summary>
        private static float WarThreshold()
        {
            try
            {
                return Campaign.Current?.Models?.CrimeModel?.DeclareWarCrimeRatingThreshold ?? 0f;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ContextualMenus: reading the crime war threshold failed: {ex.Message}");
                return 0f;
            }
        }

        private static void AddHolding(NarrativeMenu menu, string id, string titleKey, string descKey,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band,
            Func<bool> shows)
        {
            var description = new TextObject(descKey);
            var captured = band;
            var capturedShows = shows;

            ChoiceEffects.Declare(id, () =>
            {
                if (!capturedShows())
                    return new TextObject(
                        "{=CSR_Panel_Holding_NotOffered}Hall: none of this kind is offered the beginning you have").ToString();

                var offered = OfferHolding(id, captured, false, out int choices);
                return HoldingEffect(offered, choices);
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description,
                    // The description quotes nothing, but this render is what
                    // reserves this framing's hall, and what releases it again when
                    // the framing is not one this start is offered
                    unused =>
                    {
                        if (!shows()) ShowingHoldings.Remove(id);
                        else OfferHolding(id, captured, false, out _);
                    },
                    () => shows() && OfferHolding(id, captured, false, out _) != null),
                m => ChooseHolding(id, captured),
                m => { }
            ));
        }

        /// <summary>
        ///     A click on a holding option.
        ///
        ///     Clicking the option already selected is what asks this framing for
        ///     somewhere else, so which option was standing selected before this one
        ///     is the whole of the question. The panel is rebuilt from the
        ///     reservation on the next tick of the stage and follows the move on its
        ///     own; nothing here writes the description.
        /// </summary>
        private static void ChooseHolding(string id,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band)
        {
            bool asking = _lastHolding == id;
            _lastHolding = id;

            BindHolding(OfferHolding(id, band, asking, out _));
        }

        /// <summary>
        ///     What this option is offering, and with <paramref name="advance"/> the
        ///     next hall that answers it instead. <paramref name="choices"/> is how many
        ///     halls answer the framing at all, which is what decides whether the panel
        ///     promises the player another one.
        /// </summary>
        private static Settlement? OfferHolding(string id,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band, bool advance,
            out int choices)
        {
            choices = 0;
            try
            {
                var eligible = EligibleHoldings();
                if (eligible.Count == 0) return null;

                var culture = CreationSession.Current.SelectedCulture;

                // No two framings land on the same hall where the map can avoid it:
                // the seat of your fathers and the richest prize in reach were both
                // Ortysia for an Imperial start, since the richest eligible town was
                // also one of your own people. What the other options hold comes out
                // of the pool before this framing reads it
                var taken = ShowingHoldings.Where(p => p.Key != id).Select(p => p.Value).ToList();
                var unheld = taken.Count == 0
                    ? eligible
                    : eligible.Where(s => !taken.Contains(s)).ToList();

                var candidates = unheld.Count == 0
                    ? NoHalls
                    : band(unheld, culture);

                // Where it cannot, the framing reads the whole pool instead and names
                // a hall another framing is already naming, which is better than
                // taking the option off the screen: a realm whose fiefs are all of one
                // people otherwise loses "Taken from Another People" outright
                bool shared = false;
                if (candidates.Count == 0)
                {
                    candidates = band(eligible, culture);
                    shared = candidates.Count > 0;
                }

                if (candidates.Count == 0) WarnHolding(id);
                choices = candidates.Count;

                var picked = SettlementFinder.KeepLooking(id, candidates, advance);

                // A framing that only found its answer by reading past the ledger does
                // not then write to it, so the neighbor already showing that hall keeps
                // it rather than being moved off it on the next render
                if (picked == null || shared) ShowingHoldings.Remove(id);
                else ShowingHoldings[id] = picked;
                return picked;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"ContextualMenus: resolving {id} failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Says once why an option is not on the screen. A culture whose people
        ///     hold no castle is ordinary under a total conversion, and an option
        ///     silently missing is indistinguishable from one that was never written.
        ///
        ///     It fires only for a framing this start's WHOLE pool cannot answer, never
        ///     for one another framing had merely got to first.
        /// </summary>
        private static void WarnHolding(string id)
        {
            if (!WarnedHoldings.Add(id)) return;

            string culture = CreationSession.Current.SelectedCulture?.StringId ?? "no culture";
            CSLogger.Warn($"ContextualMenus: no hall this start can hold answers {id} " +
                          $"for {culture}; the option is not offered.");
        }

        private static void BindHolding(Settlement? settlement)
        {
            if (settlement == null) return;

            CreationSession.Current.SelectedSettlement = settlement;
            CSLogger.Info($"Holding selected: {settlement.Name}");
        }

        /// <summary>
        ///     Every hall this start type could legitimately be sitting in. The
        ///     rules are the ones the per-settlement list used to apply one option
        ///     at a time: a monarch takes a town of their own people, a vassal takes
        ///     one of their liege's fiefs, and a rebel seizes a castle of the realm
        ///     they rose against.
        ///
        ///     Internal rather than private because the Start Editor's holding picker
        ///     draws from the same pool, and restating the rules there let the two
        ///     disagree: the editor's copy compared a null kingdom by equality, so a
        ///     vassal with no realm chosen was offered every unowned settlement.
        /// </summary>
        internal static IReadOnlyList<Settlement> EligibleHoldings()
        {
            var session = CreationSession.Current;

            IEnumerable<Settlement> candidates = Settlement.All.Where(s => s.IsTown || s.IsCastle);

            // Only the hard rules live here. Culture is a question the written
            // framings ask, and pre-filtering by it left two of them nothing to
            // choose between and made a third impossible
            switch (session.SelectedStartType)
            {
                case StartType.Monarch:
                    // A crown inherited is seated inside the realm it rules, so the
                    // halls on offer are that realm's own. A realm holding none of
                    // them is never offered as a throne in the first place, and the
                    // whole map stands here if one somehow is, because a row nothing
                    // can answer is worse than a seat in a neighbor's county
                    var throne = session.SelectedKingdom;
                    if (throne != null)
                    {
                        var itsOwn = candidates
                            .Where(s => s.OwnerClan?.Kingdom == throne).ToList();
                        if (itsOwn.Count > 0) return Ordered(itsOwn);
                    }

                    // Towns AND castles, because a crown raised over a fortress is a
                    // start the player may ask for and MonarchScenario grants whatever
                    // this chapter bound, either kind alike. Towns only was an authored
                    // taste rather than a rule, and it made "A Castle and Its Garrison"
                    // impossible to offer any monarch on any map
                    return Ordered(candidates.ToList());

                case StartType.LandedVassal:
                    var kingdom = session.SelectedKingdom;
                    if (kingdom == null) return Array.Empty<Settlement>();
                    return Ordered(candidates.Where(s => s.OwnerClan?.Kingdom == kingdom).ToList());

                case StartType.RebelClan:
                    return Ordered(candidates
                        .Where(s => Services.Application.Scenarios.RebelClanScenario.MayBeSeized(
                            s, session.SelectedKingdom)).ToList());

                case StartType.Outlaw:
                    // Walls and a gate, never a town: a town is a crown's tax base with a
                    // governor's seat in it, and a man holding one against the crown is in
                    // rebellion rather than outlawry. Whether any of these is offered at all
                    // is the framings' question, not the pool's.
                    return Ordered(candidates.Where(s => s.IsCastle).ToList());

                default:
                    return Array.Empty<Settlement>();
            }
        }

        /// <summary>
        ///     How many halls the stewards are actually choosing between, which is not
        ///     the whole pool the written framings read. A crown that leaves its seat
        ///     to them is seated in a town by
        ///     <see cref="Services.Application.Scenarios.MonarchScenario"/>; the castles
        ///     in its pool are a thing the player may ask for by name and never a thing
        ///     a steward assigns, so counting them here would promise a reach this
        ///     answer does not have.
        /// </summary>
        private static int StewardsReach()
        {
            var pool = EligibleHoldings();
            return CreationSession.Current.SelectedStartType == StartType.Monarch
                ? pool.Count(s => s.IsTown)
                : pool.Count;
        }

        private static IReadOnlyList<Settlement> Ordered(List<Settlement> settlements)
        {
            settlements.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            return settlements;
        }

        /// <summary>
        ///     How big and rich the place is. Towns and castles both carry a Town
        ///     component; anything without one sorts to the bottom rather than
        ///     throwing.
        /// </summary>
        private static float Wealth(Settlement settlement) => settlement.Town?.Prosperity ?? 0f;

        private static readonly Settlement[] NoHalls = Array.Empty<Settlement>();

        /// <summary>
        ///     A kind of hall: any hall of your own people. This used to rank them by
        ///     prosperity and hand back the single richest, which answered a question
        ///     about whose hall it is with an answer about how rich it is.
        /// </summary>
        private static IReadOnlyList<Settlement> OfYourOwnPeople(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (culture == null) return NoHalls;
            return pool.Where(s => s.Culture == culture).ToList();
        }

        /// <summary>
        ///     One hall: the richest this start can reach, which is what the wording
        ///     names. Measured over the whole pool rather than over what the other
        ///     framings have left, since the second richest is not the richest prize
        ///     in reach. When it is already spoken for this hands back nothing and
        ///     <see cref="OfferHolding"/> asks again over the whole pool.
        /// </summary>
        private static IReadOnlyList<Settlement> TheRichest(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            var top = EligibleHoldings().OrderByDescending(Wealth).FirstOrDefault();
            return top != null && pool.Contains(top) ? new[] { top } : NoHalls;
        }

        /// <summary>
        ///     A kind of hall: one nobody would fight over, which is every hall poorer
        ///     than the average of the ones this start can reach. The bar is read off
        ///     the live map rather than fixed, so it still means something under a
        ///     conversion that rescales prosperity. It used to mean the single poorest
        ///     hall on the map, which the wording never said.
        ///
        ///     Nothing is below the average of a pool of one, and nothing is below the
        ///     average of a pool whose halls are all worth the same, so the poorest of
        ///     them answers instead: it is still the hall nobody would fight over, and
        ///     an option withdrawn because the arithmetic degenerated is the trade
        ///     that is never made.
        /// </summary>
        private static IReadOnlyList<Settlement> NobodyWanted(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (pool.Count == 0) return NoHalls;

            float average = pool.Average(Wealth);
            var poorer = pool.Where(s => Wealth(s) < average).ToList();
            if (poorer.Count > 0) return poorer;

            return new[] { pool.OrderBy(Wealth).First() };
        }

        /// <summary>
        ///     A kind of hall for an outlawed lord: one of the realm that outlawed him, held
        ///     while its writ names him. The framing is about whose law is after you, so it
        ///     reads the realm the start is bound to and nothing about the place.
        /// </summary>
        private static IReadOnlyList<Settlement> TheHallTheyWantBack(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            var realm = CreationSession.Current.SelectedKingdom;
            if (realm == null) return NoHalls;
            return pool.Where(s => s.OwnerClan?.Kingdom == realm).ToList();
        }

        /// <summary>
        ///     A kind of hall for an outlawed lord: one under a crown that is not hunting
        ///     him. The crime chapter can put every realm on that list, and then this framing
        ///     answers nothing and is not offered, which is exactly what the wording promises.
        /// </summary>
        private static IReadOnlyList<Settlement> WhereTheWritDoesNotRun(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            var session = CreationSession.Current;
            var realm = session.SelectedKingdom;
            var wanted = session.OutlawWantedBy;
            return pool.Where(s =>
            {
                var owner = s.OwnerClan?.Kingdom;
                if (owner == null || owner == realm) return false;
                return wanted == null || !wanted.Contains(owner.StringId);
            }).ToList();
        }

        /// <summary>
        ///     A kind of hall: any whose people are not yours. Wealth has nothing to do
        ///     with the framing, and ranking by it made this the richest foreign hall
        ///     and nothing else.
        /// </summary>
        private static IReadOnlyList<Settlement> TakenFromAnother(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (culture == null) return NoHalls;
            return pool.Where(s => s.Culture != culture).ToList();
        }

        /// <summary>
        ///     A kind of hall: any town. The band is the only thing the framing
        ///     promises, so it reads the settlement's own kind rather than anything
        ///     about its size, and a start whose eligible halls hold no town hands
        ///     back nothing and the option is not offered.
        /// </summary>
        private static IReadOnlyList<Settlement> AnyTown(
            IReadOnlyList<Settlement> pool, CultureObject? culture) =>
            pool.Where(s => s.IsTown).ToList();

        /// <summary>A kind of hall: any castle, read the same way as <see cref="AnyTown"/>.</summary>
        private static IReadOnlyList<Settlement> AnyCastle(
            IReadOnlyList<Settlement> pool, CultureObject? culture) =>
            pool.Where(s => s.IsCastle).ToList();

        /// <summary>A kind of hall: anywhere at all that no other option is naming.</summary>
        private static IReadOnlyList<Settlement> AnyHallAtAll(
            IReadOnlyList<Settlement> pool, CultureObject? culture) => pool;

        /// <summary>
        ///     What holding this hall does: the same <see cref="OfferHolding"/>
        ///     reading the click will bind, said as the thing this start's hall
        ///     actually is, what kind of place it is, whose people live there,
        ///     whose it is today, and where the campaign opens.
        ///
        ///     That last entry is the one a player is most likely to be deciding
        ///     on, because <see cref="Services.Application.Steps.LocationStep"/>
        ///     starts the party at the settlement this chapter bound before it
        ///     reads anything else.
        /// </summary>
        private static string HoldingEffect(Settlement? settlement, int choices)
        {
            if (settlement == null)
                return new TextObject(
                    "{=CSR_Panel_Holding_NoneAnswers}Hall: none, since nothing on the map fits this").ToString();

            var held = new TextObject(HoldingKey());
            held.SetTextVariable("HALL", settlement.Name);

            var people = settlement.Culture?.Name;
            string? whose = people == null
                ? null
                : Framed("{=CSR_Panel_Holding_People}People: {CULTURE}", "CULTURE", people.ToString());

            var owner = settlement.OwnerClan?.Kingdom;
            string? taken = owner == null ? null : Taken(owner);

            return ChoiceEffects.Stated(held.ToString(), Kind(settlement), whose, taken,
                AtItsGate(), choices > 1 ? Again(false) : null);
        }

        /// <summary>What the place is, which decides what holding it costs and pays.</summary>
        private static string Kind(Settlement settlement) =>
            new TextObject(settlement.IsTown
                ? "{=CSR_Panel_Holding_Town}Type: town, with a market and a militia"
                : "{=CSR_Panel_Holding_Castle}Type: castle, with walls and a garrison to feed").ToString();

        /// <summary>Where the campaign opens, worded the same for a hall chosen and a hall assigned.</summary>
        private static string AtItsGate() =>
            new TextObject("{=CSR_Panel_Holding_Begins}Begins: at its gate").ToString();

        /// <summary>What this start's hall is to it, which is not one thing.</summary>
        private static string HoldingKey() =>
            CreationSession.Current.SelectedStartType switch
            {
                StartType.Monarch =>
                    "{=CSR_Panel_Holding_Capital}Hall: {HALL}, yours when the campaign opens as the capital of your new realm",
                StartType.LandedVassal =>
                    "{=CSR_Panel_Holding_Fief}Hall: {HALL}, yours when the campaign opens, held of your liege",
                StartType.RebelClan =>
                    "{=CSR_Panel_Holding_Rebel}Hall: {HALL}, yours when the campaign opens, held in open revolt",
                StartType.Outlaw =>
                    "{=CSR_Panel_Holding_Outlaw}Hall: {HALL}, yours when the campaign opens, the hall you never left",
                _ => "{=CSR_Panel_Holding_Held}Hall: {HALL}, yours when the campaign opens"
            };

        /// <summary>
        ///     Whose hall it is today, which is who loses it. A hall of the realm
        ///     this start already answers to changes hands inside that realm; any
        ///     other is taken off a crown that will notice.
        /// </summary>
        private static string Taken(Kingdom owner) =>
            Framed(owner == CreationSession.Current.SelectedKingdom
                    ? "{=CSR_Panel_Holding_OwnRealm}Held by: {REALM}, the realm your story is already bound to"
                    : "{=CSR_Panel_Holding_OtherRealm}Held by: {REALM}, which loses it to you",
                "REALM", owner.Name?.ToString() ?? owner.StringId);

        #endregion

        /// <summary>
        ///     The panel entry an option carries when its answer will move, which is
        ///     the whole of how a player learns that clicking again cycles the pick.
        ///     It used to be a sentence on the end of the prose, which is the defect
        ///     this pass exists to end.
        ///
        ///     Read from how many realms or halls answer the framing rather than from
        ///     a flag beside the option, so it cannot promise a second answer where
        ///     only one exists, nor stay silent where several do.
        /// </summary>
        private static string Again(bool realm) =>
            new TextObject(realm
                ? "{=CSR_Panel_AgainRealm}Choose again: a different realm is offered"
                : "{=CSR_Panel_AgainHall}Choose again: a different hall is offered").ToString();

        private static string Framed(string key, string variable, string value)
        {
            var text = new TextObject(key);
            text.SetTextVariable(variable, value);
            return text.ToString();
        }

        /// <summary>
        ///     The same entry for the monarch's first war, which resolves its crowns
        ///     through this menu's own machinery and owes the player the same promise
        ///     in the same words.
        /// </summary>
        internal static string AgainRealm() => Again(true);

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
