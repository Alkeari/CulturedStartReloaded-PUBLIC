using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     How many fighting men ride out with you. This is its own chapter, and it
    ///     comes after the household and the companions, because the party size the
    ///     bound may offer is what the clan can hold less the companions and relatives
    ///     already riding along. Asked in the means chapter, before either was chosen,
    ///     it offered seats that were later taken and the count was quietly cut down.
    ///
    ///     It owns the count outright now. The means chapter used to set it as well
    ///     and this one overrode it afterwards, which meant coin, standing and
    ///     soldiers all came off one rung: a company of many men in poor harness had
    ///     nowhere to come from. What a life gathered in men and what it gathered in
    ///     silver are two different questions, and they are asked in two chapters.
    /// </summary>
    public static class WarbandMenu
    {
        /// <summary>
        ///     Everyone a life left owing you a season under arms. Soldiering and a
        ///     following both bring men, and a life that had both brings more of
        ///     them, so the two are added rather than compared.
        /// </summary>
        private static readonly Func<LifeProfile, int> Arms =
            profile => profile.Score(LifeProfile.Lean.Martial) + profile.Score(LifeProfile.Lean.Following);

        private static readonly LifeBand[] Windows =
        {
            new("cs_warband_none", 6, LifeGate.Open, 12,
                "{=CSR_Warband_None_Shut}Riding out alone: too many men owe you a winter to let you leave without them."),
            new("cs_warband_few", 10, LifeGate.Open, 15,
                "{=CSR_Warband_Few_Shut}A handful of hands: what follows you is either nobody at all or a good deal more than a handful."),
            new("cs_warband_company", 13, 7, 18,
                "{=CSR_Warband_Company_Shut}A company: you never stood with enough men to raise one, or you stood with far more than one."),
            new("cs_warband_warband", 15, 11, LifeGate.NoTop,
                "{=CSR_Warband_Warband_Shut}A warband: no season of your life ever gathered that many spears in one place."),
            new("cs_warband_host", 19, 15, LifeGate.NoTop,
                "{=CSR_Warband_Host_Shut}Every seat you can feed: nothing you did ever put that many men under one banner.")
        };

        /// <summary>
        ///     Which band of the start type's settings range each rung spends.
        ///     Riding out alone spends none of it: an answer that means nobody is
        ///     the absence of a muster, not the smallest one the settings allow,
        ///     and running it through the range handed the player whatever the
        ///     Lowest Band happened to be set to.
        /// </summary>
        private static readonly RangePreset?[] Presets =
        {
            null, RangePreset.Low, RangePreset.Standard, RangePreset.High, RangePreset.Maximum
        };

        private static readonly (string TitleKey, string DescKey)[] Copy =
        {
            ("{=CSR_Warband_None}You Ride Alone",
                "{=CSR_Warband_None_Desc}No one at your back but your own resolve."),
            ("{=CSR_Warband_Few}A Handful of Hands",
                "{=CSR_Warband_Few_Desc}Enough to hold a camp, not enough to hold a line."),
            ("{=CSR_Warband_Company}A Company Worth the Name",
                "{=CSR_Warband_Company_Desc}A body of men who can take the field and come home."),
            ("{=CSR_Warband_Warband}A Warband",
                "{=CSR_Warband_Warband_Desc}Numbers enough that villages count them as they pass."),
            ("{=CSR_Warband_Host_Revamped}As many as you can feed",
                "{=CSR_Warband_Host_Desc}Every seat your standing allows, and the mouths that come with them.")
        };

        public static void AddWarbandMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Warband_Desc_Revamped}Companions are one thing. Spears are another. Who else is on the road?");

            var menu = new NarrativeMenu(
                "cs_warband_menu",
                CreationFlow.DeclaredPrevious("cs_warband_menu"),
                CreationFlow.DeclaredNext("cs_warband_menu"),
                new TextObject("{=CSR_Warband_Title}Those Who Ride with You"),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddDerivedOption(menu);

            for (int i = 0; i < Windows.Length; i++)
                AddBand(menu, description, Windows[i], Presets[i], Copy[i].TitleKey, Copy[i].DescKey);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     The option that asks nothing. A life spent under arms or among
        ///     people arrives with men already; a solitary one does not. It takes
        ///     the rung the life sits nearest among those it leaves open, so this
        ///     row and the gates beside it are one reading rather than two.
        /// </summary>
        private static void AddDerivedOption(NarrativeMenu menu)
        {
            var description = new TextObject("{=CSR_Warband_Life_Desc}{REASON}");

            ChoiceEffects.Declare("cs_warband_life", () =>
            {
                var rung = new TextObject("{=CSR_Panel_Warband_Read}Why: your life reads as {RUNG}");
                rung.SetTextVariable("RUNG", new TextObject(Copy[DerivedIndex()].TitleKey).ToString());
                return ChoiceEffects.Stated(rung.ToString(), Effect(CountFor(DerivedPreset())));
            });

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_warband_life",
                new TextObject("{=CSR_Warband_Life}However many your life owed you"),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("REASON", DerivedReason())),
                m =>
                {
                    Choose(DerivedPreset());
                    MenuText.Remember("cs_warband_menu", "{=CSR_Warband_Life}However many your life owed you");
                },
                m => { }
            ));
        }

        private static void AddBand(NarrativeMenu menu, TextObject chapterDescription, LifeBand window,
            RangePreset? preset, string titleKey, string descriptionKey)
        {
            var description = new TextObject(descriptionKey);
            var captured = preset;
            var capturedTitle = titleKey;
            var capturedWindow = window;

            ChoiceEffects.Declare(window.Id, () => Effect(CountFor(captured)));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                window.Id,
                new TextObject(titleKey),
                description,
                args => { },
                // The description quotes nothing now, so the render owes only the
                // gate, which also writes the chapter's note for every shut rung
                m => LifeGate.Offers(chapterDescription, Windows, capturedWindow, Arms),
                m =>
                {
                    Choose(captured);
                    MenuText.Remember("cs_warband_menu", capturedTitle);
                },
                m => { }
            ));
        }

        /// <summary>
        ///     The rung this chapter spends. A rung with no band behind it writes an
        ///     exact nothing rather than a band, because every band resolves through
        ///     the settings range and the lowest rung of that range is still a column
        ///     of men.
        /// </summary>
        private static void Choose(RangePreset? preset)
        {
            var session = CreationSession.Current;
            if (!preset.HasValue)
            {
                session.CustomTroops = 0;
                return;
            }

            session.SelectedTroops = preset.Value;
            session.CustomTroops = null;
        }

        /// <summary>
        ///     Every seat the clan can hold, less the one you occupy and the companions
        ///     and relatives already riding with you. Both are settled by now.
        /// </summary>
        public static int Ceiling(CharacterCreationSession session)
        {
            int ceiling = GameCaps.MaxTroopsLive(session.EffectiveClanTier, session) - 1
                          - session.StartingCompanions - FamilyAges.InPartyCount(session);
            return Math.Max(0, ceiling);
        }

        private static int CountFor(RangePreset? preset)
        {
            if (!preset.HasValue) return 0;

            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            int troops = ResourceStep.TroopsFor(session, settings, preset.Value);
            return Math.Max(0, Math.Min(troops, Ceiling(session)));
        }

        private static RangePreset? DerivedPreset() => Presets[DerivedIndex()];

        /// <summary>
        ///     Which rung the life sits nearest. The index rather than the preset,
        ///     because the panel names the rung and the pick takes its preset, and
        ///     two lookups of one nearest reading is one lookup too many.
        /// </summary>
        private static int DerivedIndex()
        {
            var nearest = LifeGate.Nearest(Windows, Arms);
            for (int i = 0; i < Windows.Length; i++)
                if (ReferenceEquals(Windows[i], nearest))
                    return i;

            return 2;
        }

        /// <summary>
        ///     What this rung puts on the road, and who those men are. The count is
        ///     the one <see cref="CountFor"/> the pick spends, so the panel and the
        ///     party cannot disagree; who they are follows the start type, which
        ///     the standing chapter settled before this one was put.
        /// </summary>
        private static string Effect(int count)
        {
            if (count == 0)
                return new TextObject(
                    "{=CSR_Panel_Warband_NoOne}Troops: none when the campaign opens").ToString();

            var men = new TextObject(
                "{=CSR_Panel_Warband_Men}Troops: {WARBAND} when the campaign opens, of the {CEILING} seats your clan can fill");
            men.SetTextVariable("WARBAND", MenuText.Count(count,
                "{=CSR_Panel_Warband_One}{COUNT} soldier",
                "{=CSR_Panel_Warband_Many}{COUNT} soldiers"));
            men.SetTextVariable("CEILING", Ceiling(CreationSession.Current));

            return ChoiceEffects.Stated(men.ToString(), Recruits());
        }

        /// <summary>
        ///     Where the men come from, which is <see cref="Services.Application.Steps.TroopStep"/>
        ///     reading the start type. How many of each is that step's own mix and
        ///     is not restated here: a second copy of a split is a second thing to
        ///     keep true.
        /// </summary>
        private static string Recruits()
        {
            string key = CreationSession.Current.SelectedStartType switch
            {
                StartType.Outlaw =>
                    "{=CSR_Panel_Warband_Bandits}Type: your culture's bandits and raiders, not soldiers of any realm",
                StartType.CaravanMaster =>
                    "{=CSR_Panel_Warband_Guards}Type: your culture's caravan guards",
                _ =>
                    "{=CSR_Panel_Warband_Levies}Type: recruits of your culture, and the promotions your standing pays for"
            };

            return new TextObject(key).ToString();
        }

        private static string DerivedReason()
        {
            var profile = LifeProfile.From(CreationSession.Current);
            bool soldiered = profile.Score(LifeProfile.Lean.Martial)
                             >= profile.Score(LifeProfile.Lean.Following);

            string key = soldiered
                ? "{=CSR_Warband_Why_Martial}Men you fought beside, who would rather follow you than find a new captain."
                : "{=CSR_Warband_Why_Following}Men who owe you, or owe someone who owes you, and were told where to be.";

            return new TextObject(key).ToString();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
