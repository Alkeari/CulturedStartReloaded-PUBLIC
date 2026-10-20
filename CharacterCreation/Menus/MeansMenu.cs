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
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     One degree on a chapter's ladder, and the stretch of life that reaches it.
    ///
    ///     The window has two sides on purpose. A ladder with floors only would let
    ///     a life loud with a name still claim it left with nothing in a saddlebag,
    ///     which is the same lie in the other direction as a beggar claiming a great
    ///     house. Both ends of every ladder are shut by the life that cannot honestly
    ///     stand there.
    /// </summary>
    public sealed class LifeBand
    {
        public LifeBand(string id, int center, int low, int high, string closedKey,
            Func<LifeProfile, int>? read = null)
        {
            Id = id;
            Center = center;
            Low = low;
            High = high;
            ClosedKey = closedKey;
            Read = read;
        }

        public string Id { get; }

        /// <summary>Where on the signal this degree sits, for the option that asks nothing.</summary>
        public int Center { get; }

        public int Low { get; }
        public int High { get; }

        /// <summary>The sentence a player reads where this degree is shut to them.</summary>
        public string ClosedKey { get; }

        /// <summary>
        ///     The measure this degree is keyed on, where it is not the chapter's own.
        ///     A ladder shares one measure; a chapter of flavors rather than degrees
        ///     keys each option on the lean it actually claims, and asks for no
        ///     nearest rung, because distances on different measures do not compare.
        /// </summary>
        public Func<LifeProfile, int>? Read { get; }

        public bool Holds(int signal) => signal >= Low && signal <= High;
    }

    /// <summary>
    ///     What the life so far leaves open on a chapter's ladder.
    ///
    ///     Every degree chapter used to offer all of its options to everybody, so a
    ///     beggar's life and a great house's life were handed the identical five
    ///     buttons and the eight scenes before them decided nothing here. A chapter
    ///     now names a measure of the life and a window per degree, and the engine
    ///     asks this while it builds the option list.
    ///
    ///     Read, never forecast: the measure comes off the answers as they stand at
    ///     the moment the chapter is put, which is after every scene has been
    ///     answered, so nothing here is a guess about a life still being told.
    /// </summary>
    public static class LifeGate
    {
        /// <summary>A degree with no floor.</summary>
        public const int Open = int.MinValue;

        /// <summary>A degree with no ceiling.</summary>
        public const int NoTop = int.MaxValue;

        /// <summary>
        ///     The life as it stands, or null when it cannot be read. A null offers
        ///     every degree: a life with one choice too many is playable, a chapter
        ///     with no choices at all is not.
        /// </summary>
        public static LifeProfile? Life()
        {
            try
            {
                return LifeProfile.From(CreationSession.Current);
            }
            catch (Exception ex)
            {
                CSLogger.Error("LifeGate: reading the life for a chapter's gates failed.", ex);
                return null;
            }
        }

        private static int Signal(LifeBand band, LifeProfile life, Func<LifeProfile, int> chapterRead) =>
            (band.Read ?? chapterRead)(life);

        /// <summary>
        ///     Whether this degree is offered, and, on the way past, the note for
        ///     every degree that is not. This runs from an option's condition, which
        ///     the engine evaluates for every option while it builds the list and
        ///     before it reads the chapter's description, so the notes are in place
        ///     by the time the player sees the screen.
        /// </summary>
        public static bool Offers(TextObject chapterDescription, IReadOnlyList<LifeBand> bands,
            LifeBand band, Func<LifeProfile, int> read)
        {
            try
            {
                var life = Life();
                WriteClosedNotes(chapterDescription, bands, life, read);
                return Allows(bands, band, life, read);
            }
            catch (Exception ex)
            {
                // A condition runs on every render of every option, so this cannot
                // throw and cannot leave a chapter empty
                CSLogger.Error($"LifeGate: gating {band.Id} failed, so it is offered.", ex);
                return true;
            }
        }

        private static bool Allows(IReadOnlyList<LifeBand> bands, LifeBand band, LifeProfile? life,
            Func<LifeProfile, int> read)
        {
            if (life == null) return true;
            if (band.Holds(Signal(band, life, read))) return true;

            // The windows are built to keep at least two degrees open at every
            // value a life can reach, so this never fires; it is here because a
            // settings change or a rewritten catalog could move a life outside
            // every window, and a chapter with nothing on it cannot be played
            foreach (var other in bands)
                if (other.Holds(Signal(other, life, read)))
                    return false;

            CSLogger.Warn($"LifeGate: no degree fits this life, so every degree of {band.Id} is offered.");
            return true;
        }

        /// <summary>
        ///     The degree this life sits nearest, among those it leaves open. This is
        ///     what the option that asks nothing takes, so that option and the gates
        ///     are one reading of the life rather than two that can disagree.
        /// </summary>
        public static LifeBand Nearest(IReadOnlyList<LifeBand> bands, Func<LifeProfile, int> read)
        {
            var life = Life();
            LifeBand nearest = bands[0];
            if (life == null) return nearest;

            int signal = read(life);
            int closest = int.MaxValue;
            foreach (var band in bands)
            {
                if (!Allows(bands, band, life, read)) continue;

                int distance = Math.Abs(signal - band.Center);
                if (distance >= closest) continue;

                closest = distance;
                nearest = band;
            }

            return nearest;
        }

        /// <summary>
        ///     A chapter description that can carry the notes for its shut degrees.
        ///     The prompt is a key of its own so it stays translatable.
        /// </summary>
        public static TextObject Describe(string promptKey)
        {
            var description = new TextObject("{=!}{PROMPT}{CLOSED}");
            description.SetTextVariable("PROMPT", new TextObject(promptKey).ToString());
            description.SetTextVariable("CLOSED", string.Empty);
            return description;
        }

        /// <summary>
        ///     Writes why the missing degrees are missing into the chapter's own text.
        ///
        ///     A list row draws only its title and an option's description binds to
        ///     the selected option, so a degree that is not offered has no surface of
        ///     its own to speak from. The chapter's text is the only place left, and
        ///     it is the same answer the scenes give for a shut option.
        /// </summary>
        private static void WriteClosedNotes(TextObject chapterDescription,
            IReadOnlyList<LifeBand> bands, LifeProfile? life, Func<LifeProfile, int> read)
        {
            var notes = new List<string>();
            foreach (var band in bands)
                if (!Allows(bands, band, life, read))
                    notes.Add(new TextObject(band.ClosedKey).ToString());

            if (notes.Count == 0)
            {
                chapterDescription.SetTextVariable("CLOSED", string.Empty);
                return;
            }

            var header = new TextObject(
                "{=CSR_Gate_ClosedHeader}What a different life would have let you choose here:");

            chapterDescription.SetTextVariable("CLOSED",
                "\n\n" + header.ToString() + "\n" + string.Join("\n", notes.ToArray()));
        }
    }

    /// <summary>
    ///     What the player rides out with, as one chapter instead of five screens
    ///     of presets. Each band sets coin, influence and standing together and
    ///     says in plain numbers what that means, because the old screens asked for
    ///     five separate decisions and described all twenty options with the same
    ///     sentence. Level is deliberately absent: how seasoned you are belongs to
    ///     your life path. Exact figures belong to the Start Editor; this route
    ///     only ever asks which life you led.
    ///
    ///     The warband left this chapter with the men in it. One button that set
    ///     coin, standing AND soldiers at once was the reason a company of many
    ///     men in poor harness could not exist: every crossing of the three was
    ///     welded to a single rung. Troops are the warband chapter's, harness is
    ///     the gear chapter's, and what stays here is the purse and the name.
    /// </summary>
    public static class MeansMenu
    {
        private static EquipmentPreviewService? _previewService;

        /// <summary>
        ///     What a life is worth and how widely it is known. Both ends of the
        ///     ladder are keyed on it: the bottom rung is shut to a life that
        ///     plainly has means, the top to one that plainly has none.
        /// </summary>
        private static readonly Func<LifeProfile, int> Purse =
            profile => profile.Score(LifeProfile.Lean.Standing) + profile.Score(LifeProfile.Lean.Commerce);

        private static readonly LifeBand[] Windows =
        {
            new("cs_means_saddlebag", 6, LifeGate.Open, 13,
                "{=CSR_Means_Saddlebag_Shut}Leaving with nothing but a saddlebag: your life put too much in your hands for that to be true."),
            new("cs_means_modest", 10, LifeGate.Open, 16,
                "{=CSR_Means_Modest_Shut}A modest purse: what you are carrying is past modest, and the people who counted it know."),
            new("cs_means_fair", 13, 7, 20,
                "{=CSR_Means_Fair_Shut}A fair standing: either the towns have never heard of you, or they have heard far more than that."),
            new("cs_means_warchest", 17, 11, LifeGate.NoTop,
                "{=CSR_Means_WarChest_Shut}A war chest: nothing in your life ever put that much silver in one place."),
            new("cs_means_greathouse", 21, 17, LifeGate.NoTop,
                "{=CSR_Means_GreatHouse_Shut}The means of a great house: no house stands behind you, and this life bought none.")
        };

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        private static readonly (string Id, string TitleKey, string DescKey, RangePreset Preset, int Step)[] Bands =
        {
            ("cs_means_saddlebag", "{=CSR_Means_Saddlebag}What Fit in a Saddlebag",
                "{=CSR_Means_Saddlebag_Desc_Revamped}You left with what you could carry, and nothing waiting behind you.",
                RangePreset.Minimum, 0),
            ("cs_means_modest", "{=CSR_Means_Modest}A Modest Purse",
                "{=CSR_Means_Modest_Coin}Coin enough to eat while you find your feet, and a name nobody has to look up.",
                RangePreset.Low, 1),
            ("cs_means_fair", "{=CSR_Means_Fair}A Fair Standing",
                "{=CSR_Means_Fair_Desc_Revamped}A name the nearer towns already know, and the means to trade on it.",
                RangePreset.Standard, 2),
            ("cs_means_warchest", "{=CSR_Means_Strongroom}A war chest and a sealed room",
                "{=CSR_Means_Strongroom_Silver}Enough silver in one place that it is kept behind a door with a lock on it.",
                RangePreset.High, 3),
            ("cs_means_greathouse", "{=CSR_Means_GreatHouse_Revamped}The means of a great house",
                "{=CSR_Means_GreatHouse_Standing}Wealth and standing enough that lesser lords weigh their words with you.",
                RangePreset.Maximum, 4)
        };

        public static void AddMeansMenu(CharacterCreationManager manager)
        {
            var description = LifeGate.Describe(
                "{=CSR_Means_Purse}Every road starts with what is in your purse and what your name is worth.");

            var menu = new NarrativeMenu(
                "cs_means_menu",
                CreationFlow.DeclaredPrevious("cs_means_menu"),
                CreationFlow.DeclaredNext("cs_means_menu"),
                new TextObject("{=CSR_Means_Title}What You Carry"),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            for (int i = 0; i < Bands.Length; i++)
            {
                var (id, titleKey, descKey, preset, step) = Bands[i];
                var capturedPreset = preset;
                var capturedStep = step;
                var capturedTitle = titleKey;
                var capturedWindow = Windows[i];
                var optionText = new TextObject(descKey);

                ChoiceEffects.Declare(id, () => Effect(Worth(capturedPreset, capturedStep)));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    id,
                    new TextObject(titleKey),
                    optionText,
                    args => { },
                    // The description quotes nothing now, so the render owes only
                    // the gate, which also writes the chapter's note for every
                    // rung this life has shut
                    m => LifeGate.Offers(description, Windows, capturedWindow, Purse),
                    m =>
                    {
                        Apply(capturedPreset, capturedStep);
                        MenuText.Remember("cs_means_menu", capturedTitle);
                    },
                    m => { }
                ));
            }

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     What one rung is actually worth, worked out once.
        ///
        ///     Three surfaces quote these figures: the option's own description,
        ///     the effect panel beside the character, and the apply that hands
        ///     them over. Three readings of one computation cannot disagree with
        ///     each other; three computations of one rule can, and the panel
        ///     disagreeing with what the player is handed is the defect the panel
        ///     exists to prevent.
        /// </summary>
        private readonly struct Means
        {
            public Means(int gold, int tier, int floor, int top, int? influence)
            {
                Gold = gold;
                Tier = tier;
                Floor = floor;
                Top = top;
                Influence = influence;
            }

            public int Gold { get; }

            public int Tier { get; }

            /// <summary>The lowest standing this start is allowed.</summary>
            public int Floor { get; }

            /// <summary>The highest standing this start is allowed.</summary>
            public int Top { get; }

            /// <summary>Null where the start type has no use for influence.</summary>
            public int? Influence { get; }
        }

        private static Means Worth(RangePreset preset, int step)
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            int floor = settings?.GetMinClanTier(session.SelectedStartType) ?? 0;
            int top = settings?.GetMaxClanTier(session.SelectedStartType) ?? GameCaps.MaxClanTier();

            // Asked of the step that grants them rather than worked out again here:
            // the step also clamps gold against GameCaps, and a panel that skipped
            // that clamp would agree with the character only while the configured
            // range stayed under it.
            int gold = ResourceStep.GoldFor(session, settings, preset);

            int? influence = CSSettings.UsesInfluence(session.SelectedStartType)
                ? ResourceStep.InfluenceFor(session, settings, preset)
                : (int?)null;

            return new Means(gold, TierFor(step, floor, top), floor, top, influence);
        }

        /// <summary>
        ///     Standing scales with means, between the floor the start type
        ///     demands and the ceiling it is allowed. Without that ceiling the
        ///     top band ran to the highest tier in the game for every start, so
        ///     a commoner of "humble means" could open with a great house.
        /// </summary>
        private static int TierFor(int step, int floor, int top)
        {
            if (floor >= top) return top;

            return Math.Min(top, floor + step * (top - floor) / (Bands.Length - 1));
        }

        /// <summary>
        ///     What this rung does, in the figures the player is deciding on. The
        ///     standing is worth stating twice over, because what a tier is FOR is
        ///     the party and the companions it lets the clan hold, and the two
        ///     chapters that spend that room come later.
        /// </summary>
        private static string Effect(Means means)
        {
            var session = CreationSession.Current;

            var purse = new TextObject(
                "{=CSR_Panel_Means_Gold}Gold: +{GOLD} denars when the campaign opens");
            purse.SetTextVariable("GOLD", means.Gold);

            var standing = new TextObject(
                "{=CSR_Panel_Means_Tier}Clan: tier {TIER}, of the {MIN} to {MAX} this start allows");
            standing.SetTextVariable("TIER", means.Tier);
            standing.SetTextVariable("MIN", means.Floor);
            standing.SetTextVariable("MAX", means.Top);

            string? influence = null;
            if (means.Influence != null)
            {
                var text = new TextObject(
                    "{=CSR_Panel_Means_Influence}Influence: +{INFLUENCE} when the campaign opens");
                text.SetTextVariable("INFLUENCE", means.Influence.Value);
                influence = text.ToString();
            }

            var room = new TextObject(
                "{=CSR_Panel_Means_Room}Clan: room for {TROOPS} and {COMPANIONS} at that tier");
            room.SetTextVariable("TROOPS", MenuText.Count(GameCaps.MaxTroopsLive(means.Tier, session),
                "{=CSR_Panel_Means_Soldier}{COUNT} soldier", "{=CSR_Panel_Means_Soldiers}{COUNT} soldiers"));
            room.SetTextVariable("COMPANIONS", MenuText.Count(GameCaps.MaxCompanions(means.Tier, session),
                "{=CSR_Panel_Means_Companion}{COUNT} companion",
                "{=CSR_Panel_Means_Companions}{COUNT} companions"));

            // The conversion's own store, where the culture deals in one. It follows the tier this
            // answer settles, so it is stated by the answer that settles it rather than anywhere else
            string? store = null;
            var resource = Services.TaomBridge.ResourceFor(session.SelectedCulture?.StringId);
            if (resource != null)
            {
                int amount = ConversionStep.FactionResourceForTier(session, means.Tier);
                var text = amount > 0
                    ? new TextObject("{=CSR_Panel_Means_Store}Item: {COUNT} {RESOURCE} in your house's stores")
                    : new TextObject("{=CSR_Panel_Means_StoreNone}Item: no {RESOURCE} at all, which this tier keeps none of");
                text.SetTextVariable("COUNT", amount);
                text.SetTextVariable("RESOURCE", resource.Name);
                store = text.ToString();
            }

            return ChoiceEffects.Stated(purse.ToString(), standing.ToString(), influence, room.ToString(), store);
        }

        private static void Apply(RangePreset preset, int step)
        {
            var session = CreationSession.Current;

            session.SelectedGold = preset;
            session.CustomGold = null;
            session.SelectedInfluence = preset;
            session.CustomInfluence = null;
            session.SelectedClanTier = Worth(preset, step).Tier;
            session.CustomRenown = null;

            // How seasoned you are is the life path's business, not the purse's
            session.CustomLevel = null;

            RefreshArmorPreview();
        }

        private static void RefreshArmorPreview()
        {
            if (_previewService == null) return;

            var session = CreationSession.Current;
            _previewService.GenerateArmorPreview(session.SelectedCulture,
                Services.Application.Steps.EquipmentStep.EffectiveTier(
                    session, GlobalSettings<CSSettings>.Instance));
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
