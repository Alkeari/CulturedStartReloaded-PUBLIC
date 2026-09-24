using System;
using System.Collections.Generic;
using System.Text;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Your Standing: what the thirteen scenes came to, said plainly, one
    ///     screen before anything acts on it.
    ///
    ///     This screen used to be where the player picked Monarch or Commoner off
    ///     a button after telling a whole life, which made the life decorative. The
    ///     station is now read out of the answers by <c>Services/Portrait</c> and
    ///     written as the scenes are answered, so nothing is asked here. What is
    ///     owed instead is the telling: a route that decides a station silently and
    ///     then drops the player into a kingdom-naming chapter breaks the promise
    ///     that they never have to infer anything, exactly as an empty effect panel
    ///     would. Every sentence below is derived from the same portrait the
    ///     session was written from, so the screen cannot say one thing while the
    ///     character is another. The one station it settles rather than reads is
    ///     the one the player's settings rule out, and it settles that by writing
    ///     the session as well as the panel, for the same reason.
    ///
    ///     One exception, and it is the second half of this file. The seven Life
    ///     Story switches can all be off, and then the route asks nothing and there
    ///     is no life to read: a station read out of silence would be a station the
    ///     player neither chose nor earned, and switching the scenes off would have
    ///     quietly taken away a choice they used to have outright. So the screen
    ///     asks instead, offering the eight stations the old picker offered under
    ///     the same filter. One screen, two behaviors, and which one runs is
    ///     decided by whether there is a life to read.
    ///
    ///     The player still picks a station outright on the custom route, in the
    ///     Start Editor, which is untouched by this.
    /// </summary>
    public static class StandingMenu
    {
        private const string MenuId = "cs_standing_menu";

        /// <summary>
        ///     Introduces the station the answers came to, said only when the
        ///     player is not going to get it. On the ordinary run the station line
        ///     stands on its own and needs no label, because nothing is competing
        ///     with it.
        /// </summary>
        private const string Earned =
            "{=CSR_Standing_Earned}Your answers earned this beginning:";

        /// <summary>
        ///     Said when the station the life earned is one the player switched off
        ///     in the settings.
        ///
        ///     This screen once printed the earned station and told the player
        ///     their switch did not apply to it, which was honest about the switch
        ///     and wrong about the setting: they had said they did not want that
        ///     beginning and got it anyway. It now gives them the nearest station
        ///     their settings left open, which <c>Portrait.Standings</c> can name
        ///     and nothing here has to invent. Saying so is the whole of the
        ///     difference between that and the route quietly handing them a
        ///     character they did not earn: they read which station their life came
        ///     to, that their own switch rules it out, and which one they are
        ///     getting in its place, in that order, before anything acts on it.
        /// </summary>
        // One literal rather than a joined one: the parity check reads the first
        // string beside a key and would declare a truncated sentence
        private const string RuledOut =
            "{=CSR_Standing_RuledOut}You have that beginning switched off in your settings, so it is not yours to take. Of the beginnings you left open, your answers point nearest to this one, and it is where you begin:";

        /// <summary>
        ///     Said to the player who answered nothing, in place of a reading. It
        ///     names why they are being asked rather than told, because a screen
        ///     that behaves two ways has to say which way it is behaving.
        /// </summary>
        private const string Unwritten =
            "{=CSR_Standing_Unwritten}You have told no part of this life, so nothing in it has earned you a station. Choose the one your tale begins from.";

        /// <summary>
        ///     The chapters a beginning is asked for, by the id the flow table knows
        ///     them under and the name they are called in a sentence.
        ///
        ///     Only the chapters one beginning is asked and another is not: the
        ///     household, the means and the gear are put to everybody, and listing
        ///     them under all eight would say nothing about any of them. Which of
        ///     these a station opens is never decided here, it is asked of
        ///     <see cref="CreationFlow"/>, so a chapter that moves to another start
        ///     moves in this list on its own.
        /// </summary>
        private static readonly (string Id, string LabelKey)[] Chapters =
        {
            // Whose banner, rather than whose service: this one chapter serves the
            // vassal who swears to a realm and the outlaw the same realm is hunting
            ("cs_kingdom_select", "{=CSR_Panel_Chapter_Realm}the realm your story hangs from"),
            ("cs_contract_menu", "{=CSR_Panel_Chapter_Contract}the terms of your contract"),
            ("cs_crime_menu", "{=CSR_Panel_Chapter_Crime}the crime they want you for"),
            ("cs_settlement_select", "{=CSR_Panel_Chapter_Holding}the hall you hold"),
            ("cs_fief_menu", "{=CSR_Panel_Chapter_Fief}how that hall is manned"),
            ("cs_rising_menu", "{=CSR_Panel_Chapter_Rising}how your rising began"),
            ("cs_founding_menu", "{=CSR_Panel_Chapter_Founding}how your realm was founded"),
            ("cs_kingdom_name_menu", "{=CSR_Panel_Chapter_KingdomName}the name your realm carries"),
            ("cs_traditions_menu", "{=CSR_Panel_Chapter_Traditions}the traditions of your realm"),
            ("cs_sworn_houses_menu", "{=CSR_Panel_Chapter_SwornHouses}the houses sworn to you"),
            ("cs_first_war_menu", "{=CSR_Panel_Chapter_FirstWar}your first war"),
            ("cs_location_menu", "{=CSR_Panel_Chapter_Location}where you begin"),
            ("cs_trade_menu", "{=CSR_Panel_Chapter_Trade}the workshop you own"),
            ("cs_ledger_menu", "{=CSR_Panel_Chapter_Ledger}what your caravan carries"),
            ("cs_beasts_menu", "{=CSR_Panel_Chapter_Beasts}the beasts that carry it")
        };

        private static TextObject? _description;

        public static void AddStandingMenu(CharacterCreationManager manager)
        {
            _description = new TextObject("{=!}{CSR_STANDING}");
            Refresh();

            var menu = new NarrativeMenu(
                MenuId,
                CreationFlow.DeclaredPrevious(MenuId),
                CreationFlow.DeclaredNext(MenuId),
                new TextObject("{=CSR_Standing_Title}Your Standing"),
                _description,
                CharacterPreviewHelper.CreatePlayerCharacter(MenuId),
                GetPlayerCharacterArgs
            );

            ChoiceEffects.Declare("cs_standing_accept", Verdict);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_standing_accept",
                new TextObject("{=CSR_Standing_Option}This Is Who You Are"),
                new TextObject(
                    "{=CSR_Standing_Option_Desc}What follows is asked of the person your answers made."),
                args => { },
                m =>
                {
                    // Conditions run when the menu renders, which is the freshest
                    // moment there is: the player can walk back into a scene,
                    // answer it differently and come forward again, and the
                    // reading has to be of the life they have now
                    Refresh();
                    return Told();
                },
                m => { },
                m => { }
            ));

            // The eight stations, on the table only for a run with nothing to read
            foreach (var station in Stations)
                AddStation(menu, "cs_start_" + station.Suffix, station);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     One beginning a player can be offered outright, in the order the old
        ///     picker offered them and in its own words, which are the words that
        ///     state each one's exact effect.
        /// </summary>
        internal readonly struct Beginning
        {
            internal Beginning(string suffix, StartType type, string titleKey, string descKey)
            {
                Suffix = suffix;
                Type = type;
                TitleKey = titleKey;
                DescKey = descKey;
            }

            /// <summary>The tail of the option id, so each screen owns its own ids.</summary>
            internal string Suffix { get; }

            internal StartType Type { get; }
            internal string TitleKey { get; }
            internal string DescKey { get; }
        }

        /// <summary>
        ///     The eight beginnings, offered for a Revamped run that told no life at
        ///     all, in the order and the words the old picker used.
        /// </summary>
        internal static readonly Beginning[] Stations =
        {
            new("commoner", StartType.Commoner,
                "{=CSR_Commoner_Option}Start as a Commoner",
                "{=CSR_Commoner_Desc}You begin your journey alone, with humble means."),
            new("monarch", StartType.Monarch,
                "{=CSR_Monarch_Option}Rule as Monarch",
                "{=CSR_Monarch_Desc}You are the ruler of your own kingdom."),
            new("landed_vassal", StartType.LandedVassal,
                "{=CSR_LandedVassal_Option}Serve as Landed Vassal",
                "{=CSR_LandedVassal_Desc}You serve a liege and govern a fief."),
            new("landless_vassal", StartType.LandlessVassal,
                "{=CSR_LandlessVassal_Option}Serve as Landless Vassal",
                "{=CSR_LandlessVassal_Desc}You serve a liege but hold no lands."),
            new("mercenary", StartType.Mercenary,
                "{=CSR_Mercenary_Option}Serve as Mercenary",
                "{=CSR_Mercenary_Desc}You sell your sword to the highest bidder."),
            new("outlaw", StartType.Outlaw,
                "{=CSR_Outlaw_Option}Live as an Outlaw",
                "{=CSR_Outlaw_Desc}A realm wants your head; your band lives outside its law."),
            new("caravan", StartType.CaravanMaster,
                "{=CSR_Caravan_Option}Lead a Caravan",
                "{=CSR_Caravan_Desc}You begin with pack animals, trade goods, and guards on a trade route."),
            new("rebel", StartType.RebelClan,
                "{=CSR_Rebel_Option}Rise as a Rebel Clan",
                "{=CSR_Rebel_Desc}You hold a castle in open rebellion against its realm.")
        };

        /// <summary>Whether the player's settings leave this beginning open.</summary>
        private static bool Allows(StartType station)
        {
            try
            {
                return CSSettings.ShowsStartType(station);
            }
            catch (Exception ex)
            {
                // A condition runs on every render, so this cannot throw, and an
                // unreadable setting offers the station: a screen with no way
                // forward is worse than one offering a start that was switched off
                CSLogger.Error($"StandingMenu: reading whether {station} is offered failed.", ex);
                return true;
            }
        }

        private static void AddStation(NarrativeMenu menu, string id, Beginning station)
        {
            var chosen = station.Type;

            ChoiceEffects.Declare(id, () => Effect(chosen));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(station.TitleKey),
                new TextObject(station.DescKey),
                args => { },
                m => Offers(chosen),
                m => Take(chosen, "the player chose it for a life that told nothing"),
                m => { }
            ));
        }

        /// <summary>
        ///     Whether this station is on the table. Nothing is, for a run with a
        ///     life: that life has already answered this question, and a row beside
        ///     the verdict offering to overrule it would be the old picker back.
        /// </summary>
        private static bool Offers(StartType station)
        {
            return !Told() && Allows(station);
        }

        private static void Take(StartType station, string why)
        {
            try
            {
                if (CreationSession.Current.SelectedStartType == station) return;

                CreationSession.Current.SelectedStartType = station;

                // The chapters behind the old station are abandoned here;
                // everything they wrote goes with them
                CreationSession.Current.ResetScenarioState();
                CSLogger.Info($"StandingMenu: {station} is now the station, because {why}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StandingMenu: taking {station} failed.", ex);
            }
        }

        /// <summary>
        ///     The station this run will actually begin in: the nearest of
        ///     <see cref="Portrait.Standings"/> the player's settings leave open.
        ///
        ///     Ordinarily that is the first of them, which is the station the life
        ///     earned, and nothing here does anything. When the earned station is
        ///     switched off the next one down is a station chosen by the same
        ///     measure the first was, off the same life, so it is still what the
        ///     answers said rather than a constant this file picked.
        ///
        ///     Turning every station off cannot reach the second entry:
        ///     <see cref="CSSettings.ShowsStartType"/> answers true for all eight
        ///     in that case, by design, because all-off is a dead end rather than a
        ///     configuration and is read as no filter at all. So the first entry
        ///     passes and the life is honored, which is the same thing an all-off
        ///     run gets from every other screen.
        /// </summary>
        private static StartType Offered(Portrait portrait)
        {
            foreach (var standing in portrait.Standings)
                if (CSSettings.ShowsStartType(standing.Station))
                    return standing.Station;

            // Unreachable: Standings holds every station and ShowsStartType cannot
            // answer false for all of them. The compiler is owed a return, and the
            // station the life earned is the only honest thing to put here
            return portrait.Station;
        }

        /// <summary>
        ///     Whether this run has a life to read back at all.
        ///
        ///     One answered scene is enough, and the line is drawn there rather
        ///     than at a threshold: the station is read off the whole life rather
        ///     than off a quota of it, so a run that answered anything has earned
        ///     whatever it earned and is owed the verdict. A threshold would take
        ///     that verdict away from lives that did say something, which is the
        ///     route deciding for itself that some answers do not count.
        /// </summary>
        private static bool Told()
        {
            try
            {
                return SceneMenus.Answered.ChosenOptionIds.Count > 0;
            }
            catch (Exception ex)
            {
                // Declaring is the ordinary behavior of this screen, so an
                // unreadable count falls back to it rather than to asking a
                // player who has already answered
                CSLogger.Error("StandingMenu: counting the answers failed, so the screen declares.", ex);
                return true;
            }
        }

        /// <summary>
        ///     What accepting the verdict does: the beginning this run will really
        ///     open in and the years behind it, both read off the portrait the
        ///     screen itself is written from, and the chapters that beginning is
        ///     about to put to the player.
        ///
        ///     Nothing here is a second reading. <see cref="Offered"/> is the same
        ///     call <see cref="Refresh"/> makes when it writes the station into the
        ///     session, so the panel names the station the rest of creation will
        ///     act on rather than the one the answers earned, which can differ by
        ///     one switched-off setting.
        /// </summary>
        private static string Verdict()
        {
            var portrait = SceneMenus.Reading();
            var offered = Offered(portrait);

            return ChoiceEffects.Stated(Station(offered), Years(), Opens(offered));
        }

        /// <summary>
        ///     The beginning itself, named with the vocabulary the ranked list
        ///     beside every scene already taught the player, so the station they
        ///     watched climb that list and the station they are handed are one word.
        ///     The station's own sentence is the screen's reading above and the
        ///     option's own prose beside it; what the panel owes is the bare fact.
        /// </summary>
        private static string Station(StartType station)
        {
            var text = new TextObject("{=CSR_Panel_Standing_Station}Type: {STATION}");
            text.SetTextVariable("STATION", ChoiceEffects.StationText(station));
            return text.ToString();
        }

        /// <summary>
        ///     How old the character actually is when the campaign opens, which is
        ///     the figure <see cref="Services.Application.Steps.NarrativeStep"/>
        ///     sets the birthday from rather than the band the reading says it in.
        /// </summary>
        private static string? Years()
        {
            try
            {
                var text = new TextObject("{=CSR_Panel_Standing_Age}Age: {AGE} years when the campaign opens");
                text.SetTextVariable("AGE", CreationSession.Current.EffectiveAge);
                return text.ToString();
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StandingMenu: reading the age the run will open at failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     What one station begins as, and what it will ask for. The same three
        ///     entries the verdict carries, so a station a life earns and a station
        ///     picked off this screen are stated on the same terms.
        /// </summary>
        private static string Effect(StartType station) =>
            ChoiceEffects.Stated(Station(station), Years(), Opens(station));

        /// <summary>
        ///     The chapters this beginning opens, asked of the flow table rather
        ///     than restated: the station is put on the session for the length of
        ///     the question and put back, so the answer is the one the route will
        ///     really give, the player's own switched-off chapters included.
        /// </summary>
        private static string? Opens(StartType station)
        {
            var session = CreationSession.Current;
            var was = session.SelectedStartType;
            var asked = new List<string>();

            try
            {
                session.SelectedStartType = station;
                foreach (var chapter in Chapters)
                    if (CreationFlow.Includes(session, chapter.Id))
                        asked.Add(Text(chapter.LabelKey));
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StandingMenu: reading what {station} asks for failed: {ex.Message}");
                return null;
            }
            finally
            {
                session.SelectedStartType = was;
            }

            if (asked.Count == 0)
                return Text("{=CSR_Panel_Standing_AsksNothing}Asks: nothing the other beginnings are not asked");

            var opens = new TextObject("{=CSR_Panel_Standing_Opens}Asks: {CHAPTERS}");
            opens.SetTextVariable("CHAPTERS", Listed(asked));
            return opens.ToString();
        }

        /// <summary>A list that reads as a sentence: "a", "a and b", "a, b, and c".</summary>
        private static string Listed(List<string> parts)
        {
            if (parts.Count <= 1) return parts.Count == 0 ? string.Empty : parts[0];

            var joined = new TextObject(parts.Count == 2
                ? "{=CSR_Panel_Standing_ListTwo}{FIRST} and {LAST}"
                : "{=CSR_Panel_Standing_List}{FIRST}, and {LAST}");
            joined.SetTextVariable("FIRST", string.Join(", ", parts.GetRange(0, parts.Count - 1)));
            joined.SetTextVariable("LAST", parts[parts.Count - 1]);
            return joined.ToString();
        }

        private static string Text(string key) => new TextObject(key).ToString();

        private static void Refresh()
        {
            try
            {
                if (!Told())
                {
                    _description?.SetTextVariable("CSR_STANDING", new TextObject(Unwritten).ToString());
                    return;
                }

                var portrait = SceneMenus.Reading();
                var offered = Offered(portrait);

                // Written here rather than only read out, because the screen would
                // otherwise announce one station while the chapters below it, which
                // branch on the session, went on serving the switched-off one. The
                // scenes write the earned station back on every answer, so this is
                // the last and freshest place the correction can be made, and it is
                // ahead of every chapter that reads it
                Take(offered, offered == portrait.Station
                    ? "it is the station the answers earned"
                    : "the station the answers earned is switched off in the settings");

                _description?.SetTextVariable("CSR_STANDING", Reading(portrait, offered));
            }
            catch (Exception ex)
            {
                // The description is the whole screen, so a failure here leaves
                // the player looking at an empty panel rather than at no game:
                // the options below still carry them forward
                CSLogger.Error("StandingMenu: reading the life back failed.", ex);
            }
        }

        /// <summary>
        ///     The life in the player's own second person: what the answers made of
        ///     them, then the station they begin in and the age it implies, which
        ///     are the two things the rest of creation is about to act on.
        ///
        ///     Where <paramref name="offered"/> is not the station the life earned,
        ///     the earned one is printed first and named as earned, so the player
        ///     is never left to work out that a substitution happened or which of
        ///     the two the game is going to act on. Both sentences come from
        ///     <see cref="PortraitReading.StationLine(StartType)"/>, the one place a
        ///     station's own words are written, so the station they lost and the
        ///     station they get are described on the same terms.
        /// </summary>
        private static string Reading(Portrait portrait, StartType offered)
        {
            var reading = new StringBuilder();

            foreach (string line in PortraitReading.Facets(portrait))
            {
                if (reading.Length > 0) reading.Append(' ');
                reading.Append(new TextObject(line).ToString());
            }

            reading.Append("\n\n");

            if (offered != portrait.Station)
            {
                reading.Append(new TextObject(Earned).ToString()).Append('\n');
                reading.Append(new TextObject(PortraitReading.StationLine(portrait.Station)).ToString());
                reading.Append("\n\n").Append(new TextObject(RuledOut).ToString()).Append('\n');
            }

            reading.Append(new TextObject(PortraitReading.StationLine(offered)).ToString());
            reading.Append('\n').Append(new TextObject(PortraitReading.AgeLine(portrait)).ToString());

            return reading.ToString();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
