using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CulturedStartReloaded.CharacterCreation.Session
{
    /// <summary>
    ///     All player selections made during one run of character creation.
    ///     A fresh instance is created per campaign start; nothing here survives
    ///     into the campaign once the start has been applied.
    /// </summary>
    public class CharacterCreationSession
    {
        // The creation path chosen on the first menu
        public SetupMode Mode { get; set; } = SetupMode.Narrative;

        // True when the game's own Advanced Starting Options already named the route,
        // so the first menu is not asked again and the chosen route begins at once
        public bool RouteDecidedExternally { get; set; }

        // The chapter the narrative stage was left from, so a step back from the screen that
        // follows it returns there rather than to the route's first chapter
        public string? ResumeMenuId { get; set; }

        /// <summary>
        ///     What this run leaves to chance, decided once when the run begins.
        ///
        ///     An answer can name somebody without settling every detail of them,
        ///     and the panel beside the character has to state the person the
        ///     player is about to get. Drawing that from
        ///     <see cref="Services.CSRandom" /> at apply time would leave the panel
        ///     with nothing to state, and drawing it in the panel would move the
        ///     shared sequence every time the screen redrew. So the run carries a
        ///     number instead and the detail is derived from it: pure, asked as
        ///     often as anything likes, and the same answer to the panel, the
        ///     household and the apply pipeline alike.
        /// </summary>
        public int RunSeed { get; } = Guid.NewGuid().GetHashCode();

        // Cultured Start's chapter answers. Null until the player answers that
        // chapter, so an unanswered chapter grants nothing rather than its first option
        public FamilyBackground? SelectedFamily { get; set; }
        public ChildhoodActivity? SelectedChildhood { get; set; }
        public EducationType? SelectedEducation { get; set; }
        public YouthActivity? SelectedYouth { get; set; }
        public TurningPoint? SelectedTurning { get; set; }
        public AdventuringReason? SelectedReason { get; set; }

        public StartingAge SelectedAge { get; set; } = DefaultAge;

        // Exact age from the Start Editor; the narrative age presets otherwise
        public int? CustomAge { get; set; }

        // The age a Cultured Start Revamped life was moved to on its age chapter;
        // null keeps the age the answers came to
        public int? AdjustedAge { get; set; }

        // The age a start begins at when neither route has said otherwise
        private const StartingAge DefaultAge = StartingAge.Young;

        /// <summary>
        ///     The age this start really begins at.
        ///
        ///     The guided route writes <see cref="SelectedAge"/> on every scene it
        ///     answers and never takes it back, so a player who answers a scene,
        ///     backs out to the route menu and builds the character by hand instead
        ///     would be aged by scenes that character never lived. The route decides
        ///     whether those years belong to this life, asked through
        ///     <see cref="Services.Application.GuidedRun.WasWalkedBy"/> so that
        ///     "was this life told through the scenes" has one answer here and not
        ///     a second one that can disagree with it.
        /// </summary>
        public int EffectiveAge => CustomAge ??
            (Mode == SetupMode.LifePath
                ? (int)SelectedAge
                : Services.Application.GuidedRun.WasWalkedBy(this)
                    ? AdjustedAge ?? (int)SelectedAge
                    : (int)DefaultAge);
        private CultureObject? _selectedCulture;

        public CultureObject? SelectedCulture
        {
            get => _selectedCulture;
            set
            {
                if (_selectedCulture == value) return;
                _selectedCulture = value;
                // Every composed name reads the culture, so all three offers retire
                ComposedRealmNames.Clear();
                ComposedFirstNames.Clear();
                ComposedClanNames.Clear();
            }
        }

        // Story mode
        public StoryQuestProgress SelectedQuestProgress { get; set; } = StoryQuestProgress.FirstPhaseStart;

        // Start type and faction
        public StartType SelectedStartType { get; set; } = StartType.Commoner;
        private MonarchFounding _selectedFounding = MonarchFounding.Settler;

        public MonarchFounding SelectedFounding
        {
            get => _selectedFounding;
            set
            {
                if (_selectedFounding == value) return;
                _selectedFounding = value;
                ComposedRealmNames.Clear();
            }
        }
        public Kingdom? SelectedKingdom { get; set; }
        private Settlement? _selectedSettlement;

        // The seat feeds the composed realm names, so changing it retires the offer
        // rather than leaving names built around a hold the player no longer takes
        public Settlement? SelectedSettlement
        {
            get => _selectedSettlement;
            set
            {
                if (_selectedSettlement == value) return;
                _selectedSettlement = value;
                // A culture's clan names can be templates keyed on the seat
                ComposedRealmNames.Clear();
                ComposedClanNames.Clear();
            }
        }
        public Settlement? SelectedLocation { get; set; }
        public bool UseRandomLocation { get; set; }

        // How far onto the water this start goes. A degree of the start type rather
        // than a start of its own; None on every start that stayed ashore and on
        // every start at all without War Sails, which registers no ship and no port.
        public Services.Application.Scenarios.SeaDegree SelectedSeaDegree { get; set; } =
            Services.Application.Scenarios.SeaDegree.None;

        // Companions
        public int StartingCompanions { get; set; }

        // Family composition: everyone has parents; the choice per relative is
        // alive or dead at start. Vanilla-equivalent default: both parents dead.
        public List<FamilyMemberSpec> FamilyMembers { get; } = new()
        {
            new FamilyMemberSpec(FamilyRelation.Father, false),
            new FamilyMemberSpec(FamilyRelation.Mother, false)
        };

        // Resources and progression
        public int SelectedClanTier { get; set; }

        /// <summary>
        ///     The clan tier this start will really begin at: the band the purse
        ///     chapter picked, or the tier the chosen renown earns, whichever is
        ///     higher. The game derives clan tier from renown, so naming a renown
        ///     of tier 6 and then offering tier 2's party size was a lie about
        ///     what the start would actually be.
        /// </summary>
        public int EffectiveClanTier =>
            CustomRenown.HasValue
                ? System.Math.Max(SelectedClanTier, Services.GameCaps.TierForRenown(CustomRenown.Value))
                : SelectedClanTier;
        public RangePreset SelectedGold { get; set; } = RangePreset.Standard;
        public RangePreset SelectedInfluence { get; set; } = RangePreset.Standard;
        public RangePreset SelectedTroops { get; set; } = RangePreset.Standard;

        // Weapon loadout: one gear choice per weapon slot (Weapon0..Weapon3)
        public GearChoice[] WeaponChoices { get; } = { new(), new(), new(), new() };

        // Set from Cultured Start's arms chapter when the player asks to fill the
        // four slots by hand; the per-slot menus join that route only then
        public bool ChooseWeaponsIndividually { get; set; }

        // How the character wears their station: one step either side of the tier
        // the clan would suggest, clamped to what the start type allows
        public ArmorBearing Bearing { get; set; } = ArmorBearing.Station;

        // Full-freedom overrides: exact gear picks per outfit and slot, banner,
        // kingdom name, exact resource numbers, and direct stat customization.
        // Empty/null means "use the automatic result".
        public Dictionary<string, int> CustomSkillLevels { get; } = new();

        // A null value means the player wants that slot explicitly EMPTY;
        // a missing key means the quartermaster decides
        public Dictionary<(OutfitKind Kind, EquipmentIndex Slot), ItemObject?> ExactOutfit { get; } = new();
        public ItemObject? ExactBanner { get; set; }
        public string? KingdomName { get; set; }
        public int? CustomGold { get; set; }
        public int? CustomInfluence { get; set; }
        public int? CustomTroops { get; set; }
        public int? CustomLevel { get; set; }
        public int? CustomRenown { get; set; }
        public Dictionary<string, int> CustomAttributes { get; } = new();
        public Dictionary<string, int> CustomFocus { get; } = new();
        public Dictionary<string, int> CustomTraits { get; } = new();

        // Chosen perks per skill (skill id to perk ids). A missing skill means
        // "pick automatically up to the skill's level" on the custom path.
        public Dictionary<string, List<string>> CustomPerks { get; } = new();

        // The custom path starts every gear slot at None; set once when the
        // Start Editor first opens so later Auto choices are respected
        public bool CustomGearDefaultsApplied { get; set; }

        // Food and mount stacks added to the starting inventory on top of the
        // defaults; mounts cover speed horses and carry-capacity pack animals
        public List<InventoryEntry> CustomFood { get; } = new();

        // Whether the Start Editor has already loaded the provisions plan into the food and mount
        // lists, so the plan is shown as the stacks it loads the first time the wagons are drawn
        public bool ProvisionsMaterialized { get; set; }
        public List<InventoryEntry> CustomMounts { get; } = new();

        // Narrative provisioning: the plan feeds the party when nothing exact
        // was composed; exact additions always win over the plan
        public ProvisionPlan Provisions { get; set; } = ProvisionPlan.Sensible;

        // Monarch kingdom setup: generated vassal clans, initial policies,
        // starting wars (null = founding default, empty = none, else kingdom
        // ids), and whether granted castles go to the vassals
        public int VassalClanCount { get; set; }

        // True when a creation menu settled the kingdom's name question, so the
        // post-spawn naming prompt stays quiet either way
        public bool KingdomNameDecided { get; set; }

        // How the realm's name is arrived at when KingdomName carries no text:
        // Automatic composes one from the capital, the culture and the founding;
        // Clan is the explicit "named for your clan" choice; Custom means
        // KingdomName holds the player's own words.
        public Models.KingdomNameStyle KingdomNameStyle { get; set; } = Models.KingdomNameStyle.Automatic;

        // The realm names composed for this character, held so the offer stays put
        // between redraws instead of rolling again every time a row is rebuilt
        public List<string> ComposedRealmNames { get; } = new();

        // The player's own name and their clan's. Null means nothing has been
        // settled yet and the first composed candidate stands in; both routes
        // must offer a way to type one, so neither is ever decided for the player
        public string? PlayerFirstName { get; set; }
        public string? PlayerClanName { get; set; }
        public List<string> ComposedFirstNames { get; } = new();
        public List<string> ComposedClanNames { get; } = new();
        public List<string> SelectedPolicies { get; } = new();
        public List<string>? CustomWars { get; set; }
        public bool GrantLandsToVassals { get; set; }

        // Property and flavor per start type: workshops for the propertied
        // starts, exact crime rating for outlaws, pack animals for caravans
        public int StartingWorkshops { get; set; }
        /// <summary>The career the character begins in, where the conversion offers careers.</summary>
        public string? SelectedCareerId { get; set; }

        /// <summary>The picks taken on that career's board, the free first rank aside.</summary>
        public List<string> CareerChoiceIds { get; } = new();

        /// <summary>The conversion's own named people who ride with the character from the first day.</summary>
        public List<string> NamedCompanionIds { get; } = new();

        /// <summary>The store of the culture's own faction resource, such as Castar.</summary>
        public int? CustomFactionResource { get; set; }

        public int? CustomCrimeRating { get; set; }
        public int? CustomPackAnimals { get; set; }

        // Deep setup for the non-monarch starts. RealmWars reshapes the joined
        // realm's diplomacy (null = current wars, empty = at peace, else the
        // exact enemy set); CustomGarrison staffs the starting holding; the
        // contract pay is the mercenary award multiplier; OutlawWantedBy adds
        // realms sharing the crime rating (null = the outlawing realm only);
        // trade goods load the caravan; rebel allies are fellow rebel houses.
        // What the player chose in each scenario chapter, keyed by chapter menu
        // id so re-deciding replaces rather than stacking; the epilogue recites
        // these so the story's middle act is not missing from its own summary
        public Dictionary<string, string> StoryBeats { get; } = new();

        public List<string>? RealmWars { get; set; }
        public int? CustomGarrison { get; set; }
        public int? CustomContractPay { get; set; }
        public List<string>? OutlawWantedBy { get; set; }
        public List<InventoryEntry> CustomTradeGoods { get; } = new();

        // The list alone cannot tell wagons nobody loaded from wagons somebody
        // answered as empty, and the caravan start reads an unloaded list as the
        // former and buys stock. RealmWars carries the same distinction as a null
        // against an empty list; this one cannot, so it says so beside the list.
        public bool NoTradeGoods { get; set; }

        public int RebelAllyCount { get; set; }

        // Per-companion advanced customization, index-aligned with the count
        public List<HeroSpec> CompanionSpecs { get; } = new();

        // Heroes outside the player clan the player must nonetheless know:
        // married-away kin, their spouses, and the heads of those houses. The
        // post-spawn fixup marks them met; they leave clan-wide coverage when
        // the marriage moves them out.
        public List<Hero> KnownFaces { get; } = new();

        // Household builder

        // Equipment preview: what the 3D preview shows and what the player receives
        public Equipment? PreviewEquipment { get; set; }

        /// <summary>
        ///     Clears everything the scenario chapters wrote. Changing the start
        ///     type abandons those chapters, and without this their beats and
        ///     settings survive into the new branch: a commoner's epilogue
        ///     reciting a coronation that never happened.
        /// </summary>
        public void ResetScenarioState()
        {
            SelectedKingdom = null;
            SelectedSettlement = null;
            SelectedLocation = null;
            UseRandomLocation = false;
            SelectedSeaDegree = Services.Application.Scenarios.SeaDegree.None;
            SelectedFounding = MonarchFounding.Settler;
            KingdomName = null;
            KingdomNameDecided = false;
            KingdomNameStyle = Models.KingdomNameStyle.Automatic;
            ComposedRealmNames.Clear();
            PlayerFirstName = null;
            PlayerClanName = null;
            ComposedFirstNames.Clear();
            ComposedClanNames.Clear();
            VassalClanCount = 0;
            GrantLandsToVassals = false;
            SelectedPolicies.Clear();
            CustomWars = null;
            StartingWorkshops = 0;
            CustomCrimeRating = null;
            CustomPackAnimals = null;
            RealmWars = null;
            CustomGarrison = null;
            CustomContractPay = null;
            OutlawWantedBy = null;
            CustomTradeGoods.Clear();
            NoTradeGoods = false;
            RebelAllyCount = 0;
            StoryBeats.Clear();
        }

        private static string List(List<string>? values) =>
            values == null ? "default" : values.Count == 0 ? "none" : string.Join(",", values);

        /// <summary>
        ///     Everything the player decided, in one block, written to the log before the start is
        ///     applied. It carries every field the pipeline reads rather than a chosen few, because
        ///     the questions that get asked afterwards are about the fields nobody thought to
        ///     print: what the clan tier really was, whether a war list was empty or absent, how
        ///     many stats were overridden by hand.
        /// </summary>
        public string DumpState()
        {
            return $@"  Session:
    Mode={Mode}, RouteDecidedExternally={RouteDecidedExternally}
    Family={SelectedFamily?.ToString() ?? "unanswered"}, Childhood={SelectedChildhood?.ToString() ?? "unanswered"}, Education={SelectedEducation?.ToString() ?? "unanswered"}
    Youth={SelectedYouth?.ToString() ?? "unanswered"}, Turning={SelectedTurning?.ToString() ?? "unanswered"}, Reason={SelectedReason?.ToString() ?? "unanswered"}
    Age={SelectedAge}, CustomAge={CustomAge?.ToString() ?? "none"}, AdjustedAge={AdjustedAge?.ToString() ?? "none"}, EffectiveAge={EffectiveAge}, Culture={SelectedCulture?.StringId ?? "null"}
    QuestProgress={SelectedQuestProgress}
    StartType={SelectedStartType}, Founding={SelectedFounding}
    Kingdom={SelectedKingdom?.Name?.ToString() ?? "null"}, Settlement={SelectedSettlement?.Name?.ToString() ?? "null"}
    Location={SelectedLocation?.Name?.ToString() ?? "null"}, UseRandomLocation={UseRandomLocation}, SeaDegree={SelectedSeaDegree}
    Companions={StartingCompanions} ({CompanionSpecs.Count} customized)
    ClanTier={SelectedClanTier}, EffectiveClanTier={EffectiveClanTier}, CustomRenown={CustomRenown?.ToString() ?? "none"}
    Gold={SelectedGold}/{CustomGold?.ToString() ?? "band"}, Influence={SelectedInfluence}/{CustomInfluence?.ToString() ?? "band"}, Troops={SelectedTroops}/{CustomTroops?.ToString() ?? "band"}
    CustomLevel={CustomLevel?.ToString() ?? "none"}
    Overrides: attributes={CustomAttributes.Count}, focus={CustomFocus.Count}, skills={CustomSkillLevels.Count}, traits={CustomTraits.Count}, perked skills={CustomPerks.Count}
    Weapons=[{WeaponChoices[0]}, {WeaponChoices[1]}, {WeaponChoices[2]}, {WeaponChoices[3]}], ByWeaponSlot={ChooseWeaponsIndividually}
    ExactOutfit={ExactOutfit.Count} slots, Banner={ExactBanner?.StringId ?? "auto"}
    Provisions={Provisions}, Food={CustomFood.Count}, Mounts={CustomMounts.Count}, TradeGoods={(NoTradeGoods ? "none" : CustomTradeGoods.Count.ToString())}
    Family={FamilyMembers.Count} members, KnownFaces={KnownFaces.Count}
    Monarch: VassalClans={VassalClanCount}, GrantLands={GrantLandsToVassals}, KingdomName={KingdomName ?? "undecided"}, Policies={(SelectedPolicies.Count == 0 ? "none" : string.Join(",", SelectedPolicies))}
    Wars={List(CustomWars)}, RealmWars={List(RealmWars)}, WantedBy={List(OutlawWantedBy)}
    Workshops={StartingWorkshops}, Garrison={CustomGarrison?.ToString() ?? "default"}, ContractPay={CustomContractPay?.ToString() ?? "default"}
    CrimeRating={CustomCrimeRating?.ToString() ?? "default"}, PackAnimals={CustomPackAnimals?.ToString() ?? "default"}, RebelAllies={RebelAllyCount}
    StoryBeats={(StoryBeats.Count == 0 ? "none" : string.Join("; ", StoryBeats.Select(b => $"{b.Key}={b.Value}")))}";
        }
    }

    /// <summary>
    ///     The brother or sister an answer names without saying which.
    ///
    ///     "They said nothing about you" is the household talking about somebody
    ///     else under that roof and never once saying who, which is the writing
    ///     doing what it was written to do. The character still gets that person,
    ///     so which kind they are and whether they came before or after is the
    ///     RUN's to settle rather than the script's, and it is settled here, off
    ///     <see cref="CharacterCreationSession.RunSeed" />, at the moment anything
    ///     first asks.
    ///
    ///     Pure, and that is the whole point: the effect panel states a person
    ///     already decided rather than a coin the apply pipeline has yet to toss,
    ///     the household counts that same person, the panel can redraw as often
    ///     as it likes, the player can walk back to the scene and answer it again,
    ///     and the family step asks once more at the end and gets the same
    ///     brother or sister every time.
    /// </summary>
    public readonly struct StorySibling
    {
        /// <summary>The one answer that leaves a sibling without naming them.</summary>
        public const string DeclaredBy = "cs_opt_they_said_nothing_about_you";

        /// <summary>The household fact it declares, which stands for the two below.</summary>
        public const string Declares = "a_sibling";

        private StorySibling(FamilyRelation relation, int yearsOlder)
        {
            Relation = relation;
            YearsOlder = yearsOlder;
        }

        public FamilyRelation Relation { get; }

        /// <summary>Years older than the character, negative where they came after.</summary>
        public int YearsOlder { get; }

        /// <summary>The settled fact, in the vocabulary the panel and the house read.</summary>
        public string House =>
            Relation == FamilyRelation.Brother ? "a_brother" : "a_sister";

        public static StorySibling Of(CharacterCreationSession? session)
        {
            int seed = session?.RunSeed ?? 0;

            var relation = Services.CSRandom.Stable(seed, DeclaredBy + ":kind", 2) == 0
                ? FamilyRelation.Brother
                : FamilyRelation.Sister;

            int years = 2 + Services.CSRandom.Stable(seed, DeclaredBy + ":years", 5);
            if (Services.CSRandom.Stable(seed, DeclaredBy + ":order", 2) != 0) years = -years;

            return new StorySibling(relation, years);
        }
    }

    /// <summary>
    ///     Holds the live session. Menu option delegates run inside the game's menu
    ///     system with no injection seam, so they reach the session through this
    ///     holder; everything else receives the session as a parameter.
    /// </summary>
    public static class CreationSession
    {
        public static CharacterCreationSession Current { get; private set; } = new();

        /// <summary>Discards all selections and starts a fresh session.</summary>
        public static void StartNew()
        {
            Current = new CharacterCreationSession();
            Services.GameCaps.Reset();

            // The world a character is measured against is the one that existed before
            // their own start was applied, so it is read once per run and dropped here
            Services.StorySkills.ForgetTheWorld();

            Services.Application.Scenarios.SeaGrants.Reset();
            Services.PerkPlanner.ResetPairChoices();

            // Whether a total conversion is running cannot change inside one launch, but the answer
            // is cached on first ask, and the first ask has moved before now. Clearing it with the
            // other per-session caches means a stale answer can never outlive the session that
            // produced it, whatever asks first next time.
            Services.TotalConversionService.Reset();
            Services.TaomBridge.Reset();
            Services.SettlementFinder.Reset();
            Menus.ContextualMenus.Reset();
            Menus.StartLocationMenu.Reset();

            // The scene answers outlive nothing: they belong to the session that
            // was told them, and a character built on a later one never lived them
            Menus.SceneMenus.Reset();

            // The game's own starting-options screen decides the route outright when it
            // is there to ask, so the first menu is skipped rather than pre-answered.
            var chosen = Services.AdvancedStartBridge.ChosenStartType();
            var route = Services.AdvancedStartBridge.RouteFromAdvancedStart();
            Services.CSLogger.Info(
                $"CreationSession: advanced start type is {chosen ?? "none"}; " +
                $"route decided: {(route == null ? "none, the first menu asks" : route.Value.ToString())}.");
            if (route != null)
            {
                Current.Mode = route.Value;
                Current.RouteDecidedExternally = true;
            }
        }
    }
}
