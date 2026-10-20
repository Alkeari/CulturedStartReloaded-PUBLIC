using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Where the first morning finds you. This was every town in the world in
    ///     alphabetical order, each described with the same sentence. It asks what
    ///     kind of place you woke up in instead, and says which town that came out
    ///     as.
    ///
    ///     An option names either one place or a kind of place, and that decides
    ///     whether asking again moves it. "The largest town you had heard of" is one
    ///     town and stays that town; "Close to where you were raised" is any town of
    ///     your own people, so choosing it again offers a different one of them, and
    ///     the last option ranges over everything left, so no town is out of reach.
    ///     Both are the same code path, and the same one the realm and holding menus
    ///     use: a candidate list walked by <see cref="SettlementFinder.KeepLooking{T}"/>,
    ///     where a list of one never moves. An option whose list holds more than one
    ///     says so in its own panel, in the same words the realm and holding menus use,
    ///     because a player is told what asking again will do rather than left to
    ///     discover it by clicking.
    /// </summary>
    public static class StartLocationMenu
    {
        // What each option is offering right now. Read so that no two framings land
        // on the same town; written as each one resolves.
        private static readonly Dictionary<string, Settlement> Showing = new();

        // Which option the player is standing on. Clicking the one already selected
        // is what asks it for somewhere else; arriving on it from another option
        // shows what it was already offering.
        private static string? _lastSelected;

        // Options whose kind of place the map cannot answer, so the reason is logged
        // once rather than on every render.
        private static readonly HashSet<string> Warned = new();

        // How many towns each option answers with, recorded as the framing
        // resolves. The panel is redrawn on every tick of the stage and asking the
        // framing again there is what would move it, so the count is kept beside
        // the town rather than worked out a second time.
        private static readonly Dictionary<string, int> Answering = new();

        public static void AddStartLocationMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_location_menu",
                CreationFlow.DeclaredPrevious("cs_location_menu"),
                CreationFlow.DeclaredNext("cs_location_menu"),
                new TextObject("{=CSR_Location_Title_Revamped}Where the Road Starts"),
                new TextObject(
                    "{=CSR_Location_Desc_Revamped}You have to be standing somewhere when the story begins. What kind of place is it?"),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddPlace(menu, "cs_location_home",
                "{=CSR_Location_Home}Close to where you were raised",
                "{=CSR_Location_Home_Desc}Your own people, streets you half remember, and everyone who knew your family before it came to this.",
                AmongYourOwn);

            AddPlace(menu, "cs_location_great",
                "{=CSR_Location_Great}The largest town you had heard of",
                "{=CSR_Location_Great_Desc}Crowds, coin changing hands, and enough going on that one more stranger changes nothing.",
                TheLargest);

            AddPlace(menu, "cs_location_quiet",
                "{=CSR_Location_Quiet}Somewhere Too Small to Matter",
                "{=CSR_Location_Quiet_Desc}A market that closes at noon and nobody worth answering to. It is a good place to be forgotten in.",
                TooSmallToMatter);

            AddPlace(menu, "cs_location_far",
                "{=CSR_Location_Far}As far from your own people as you could get",
                "{=CSR_Location_Far_Desc}A foreign tongue, foreign food, and not one person who can name your father.",
                TheFarthest);

            AddPlace(menu, "cs_location_roll",
                "{=CSR_Location_Roll}Somewhere you had never heard of",
                "{=CSR_Location_Roll_Line}Somewhere you picked off a map with your eyes shut.",
                Anywhere);

            ChoiceEffects.Declare("cs_location_auto", () => new TextObject(
                "{=CSR_Panel_Location_Auto}Begins: the kind of place your life left you in, which is a village of your own people where it was lived out of doors, and a town of your own people where the life says nothing").ToString());

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_location_auto",
                new TextObject("{=CSR_Location_Auto}Let Fate Decide"),
                new TextObject(
                    "{=CSR_Location_Auto_Desc_Revamped}You will find out where you are when you open your eyes."),
                args => { },
                m => true,
                m =>
                {
                    _lastSelected = "cs_location_auto";
                    CreationSession.Current.SelectedLocation = null;
                    CreationSession.Current.UseRandomLocation = true;
                },
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>Forgets what was offered, so a new session starts clean.</summary>
        public static void Reset()
        {
            Showing.Clear();
            Answering.Clear();
            Warned.Clear();
            _lastSelected = null;
        }

        private static void AddPlace(NarrativeMenu menu, string id, string titleKey, string descKey,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band)
        {
            var description = new TextObject(descKey);
            var captured = band;

            ChoiceEffects.Declare(id, () => Effect(id));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                MenuText.Live(description,
                    // The description quotes nothing, but this is still the only
                    // hook the API runs on every render, and what it runs is what
                    // reserves this option a town no other option can land on
                    unused => Offer(id, captured, false, out _),
                    () => Offer(id, captured, false, out _) != null),
                m => Choose(id, captured),
                m => { }
            ));
        }

        /// <summary>
        ///     A click on this option.
        ///
        ///     The engine runs onCondition only while it is building the menu's option
        ///     list, which happens once on entry, so a description that quotes a live
        ///     value from there is frozen for as long as the player stands on the
        ///     screen. It is pushed again here because the option's own RefreshValues
        ///     runs immediately after onSelect and re-reads the same TextObject, which
        ///     makes this the only place a re-click can change what the player reads.
        ///     Without it the option moved on internally and went on displaying the
        ///     first answer it ever gave.
        ///
        ///     The description quotes nothing now, so what a re-click has to move is
        ///     the reservation and the binding. The panel is rebuilt from the
        ///     reservation on the next tick of the stage and follows on its own.
        /// </summary>
        private static void Choose(string id,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band)
        {
            bool asking = _lastSelected == id;
            _lastSelected = id;

            Bind(Offer(id, band, asking, out _));
        }

        private static void Bind(Settlement? town)
        {
            if (town == null) return;

            CreationSession.Current.SelectedLocation = town;
            CreationSession.Current.UseRandomLocation = false;
            CSLogger.Info($"Starting location selected: {town.Name}");
        }

        /// <summary>
        ///     What this option is offering, and with <paramref name="advance"/> the
        ///     next place that answers it instead. <paramref name="choices"/> is how
        ///     many towns answer the framing at all, which is what decides whether the
        ///     panel promises the player another one.
        /// </summary>
        private static Settlement? Offer(string id,
            Func<IReadOnlyList<Settlement>, CultureObject?, IReadOnlyList<Settlement>> band, bool advance,
            out int choices)
        {
            choices = 0;
            try
            {
                var eligible = Eligible();
                if (eligible.Count == 0) return null;

                // No two framings may land on the same town: two written choices with
                // one answer is the map offered twice under different sentences. The
                // towns other options hold come out of the pool before the framing
                // reads it, so a framing naming one place names the best one left
                var taken = Showing.Where(p => p.Key != id).Select(p => p.Value).ToList();
                var pool = taken.Count == 0
                    ? eligible
                    : eligible.Where(s => !taken.Contains(s)).ToList();

                var candidates = pool.Count == 0
                    ? (IReadOnlyList<Settlement>)Array.Empty<Settlement>()
                    : band(pool, CreationSession.Current.SelectedCulture);

                if (candidates.Count == 0) Warn(id);
                choices = candidates.Count;
                Answering[id] = choices;

                var picked = SettlementFinder.KeepLooking(id, candidates, advance);
                if (picked == null) Showing.Remove(id);
                else Showing[id] = picked;
                return picked;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StartLocationMenu: resolving {id} failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Says once why an option is not on the screen. A culture that holds no
        ///     town of its own is ordinary under a total conversion, and an option
        ///     silently missing is indistinguishable from one that was never written.
        /// </summary>
        private static void Warn(string id)
        {
            if (!Warned.Add(id)) return;

            string culture = CreationSession.Current.SelectedCulture?.StringId ?? "no culture";
            CSLogger.Warn($"StartLocationMenu: no town this start can reach answers {id} " +
                          $"for {culture}; the option is not offered.");
        }

        /// <summary>
        ///     Every town this start could plausibly begin in. A start bound to a
        ///     realm begins inside it; everyone else begins among their own people
        ///     where any town of that culture exists.
        /// </summary>
        private static IReadOnlyList<Settlement> Eligible()
        {
            var session = CreationSession.Current;
            var kingdom = session.SelectedKingdom;

            var towns = Settlement.All.Where(s => s.IsTown).ToList();

            if (session.SelectedStartType != StartType.Commoner && kingdom != null)
            {
                var realmTowns = towns.Where(s => s.OwnerClan?.Kingdom == kingdom).ToList();
                if (realmTowns.Count > 0) towns = realmTowns;
            }

            // Deliberately not culture-filtered here: two of the framings are
            // about culture, and a filter at this level would leave them nothing
            // to choose between
            towns.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            return towns;
        }

        /// <summary>
        ///     How big and rich the place is. Towns and castles both carry a Town
        ///     component; anything without one sorts to the bottom rather than
        ///     throwing.
        /// </summary>
        private static float Size(Settlement settlement) => settlement.Town?.Prosperity ?? 0f;

        private static readonly Settlement[] None = Array.Empty<Settlement>();

        /// <summary>A kind of place: any town of your own people.</summary>
        private static IReadOnlyList<Settlement> AmongYourOwn(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (culture == null) return None;
            return pool.Where(s => s.Culture == culture).ToList();
        }

        /// <summary>One place: the richest town on offer, which is what the wording names.</summary>
        private static IReadOnlyList<Settlement> TheLargest(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            var top = pool.OrderByDescending(Size).FirstOrDefault();
            return top == null ? None : new[] { top };
        }

        /// <summary>
        ///     A kind of place: a town nobody would count, which is every town poorer
        ///     than the average of the ones this start can reach. The bar is read off
        ///     the live map rather than fixed, so it still means something under a
        ///     conversion that rescales prosperity.
        /// </summary>
        private static IReadOnlyList<Settlement> TooSmallToMatter(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (pool.Count == 0) return None;

            float average = pool.Average(Size);
            return pool.Where(s => Size(s) < average).ToList();
        }

        /// <summary>
        ///     One place: the town farthest from where your people actually live. This
        ///     used to rank strangers by prosperity, which answered a question about
        ///     distance with the richest foreign town instead of the most distant one.
        /// </summary>
        private static IReadOnlyList<Settlement> TheFarthest(
            IReadOnlyList<Settlement> pool, CultureObject? culture)
        {
            if (culture == null) return None;

            var strangers = pool.Where(s => s.Culture != culture).ToList();
            if (strangers.Count == 0) return None;

            var heartland = SettlementFinder.Heartland(culture);
            if (heartland == null)
            {
                // A culture holding nothing anywhere has no country to be far from,
                // so nothing can answer this framing. It used to hand over the
                // greatest foreign town and say so only to the log, which answers a
                // question about distance with one about prosperity; an option with
                // no honest answer is not put at all
                CSLogger.Warn($"StartLocationMenu: {culture.StringId} holds no settlement, so " +
                              "there is no distance to measure and the framing is not offered.");
                return None;
            }

            var far = strangers
                .OrderByDescending(s => s.GetPosition2D.Distance(heartland.Value))
                .First();
            return new[] { far };
        }

        /// <summary>A kind of place: anywhere at all that no other option is naming.</summary>
        private static IReadOnlyList<Settlement> Anywhere(
            IReadOnlyList<Settlement> pool, CultureObject? culture) => pool;

        /// <summary>
        ///     Which town this framing has come out as, and what kind of place that
        ///     is: whose people live there and whose realm holds it, since the
        ///     framings ask about exactly those two things.
        ///
        ///     Read out of <see cref="Showing"/> rather than resolved again. The
        ///     panel redraws every frame the stage ticks and resolving is what moves
        ///     a framing on to the next town, so asking it here would be the panel
        ///     quietly changing the answer it is describing.
        /// </summary>
        private static string Effect(string id)
        {
            if (!Showing.TryGetValue(id, out var town) || town == null)
                return new TextObject(
                    "{=CSR_Panel_Location_NoneAnswers}Begins: nowhere, since no town on the map fits this").ToString();

            var realm = town.OwnerClan?.Kingdom;
            var where = new TextObject(realm == null
                ? "{=CSR_Panel_Location_Free}Begins: at the gates of {TOWN}, which no crown holds"
                : "{=CSR_Panel_Location_Town}Begins: at the gates of {TOWN}, held by {REALM}");
            where.SetTextVariable("TOWN", town.Name);
            if (realm != null) where.SetTextVariable("REALM", realm.Name);

            string? people = null;
            var culture = town.Culture;
            if (culture != null)
            {
                var text = new TextObject(culture == CreationSession.Current.SelectedCulture
                    ? "{=CSR_Panel_Location_Kin}People: your own"
                    : "{=CSR_Panel_Location_Strangers}People: {CULTURE}, not your own");
                if (culture != CreationSession.Current.SelectedCulture)
                    text.SetTextVariable("CULTURE", culture.Name);
                people = text.ToString();
            }

            string? again = Answering.TryGetValue(id, out int choices) && choices > 1
                ? new TextObject(
                    "{=CSR_Panel_AgainTown}Choose again: a different town is offered").ToString()
                : null;

            return ChoiceEffects.Stated(where.ToString(), people, again);
        }

        private static List<NarrativeMenuCharacter> CreatePlayerCharacter()
        {
            return CharacterPreviewHelper.CreatePlayerCharacter();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
