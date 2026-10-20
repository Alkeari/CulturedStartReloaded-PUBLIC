using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Saves and loads Start Editor setups as named JSON presets under the
    ///     game's config folder. Objects travel as string ids; anything that no
    ///     longer exists on load (an uninstalled item mod, a renamed settlement)
    ///     is skipped quietly so a preset never breaks the editor.
    /// </summary>
    public static class StartPresetService
    {
        private sealed class WeaponDto
        {
            public string Class = nameof(WeaponClassChoice.KeepAuto);
            public string? ItemId;
            public bool Empty;
        }

        private sealed class OutfitDto
        {
            public string Kind = nameof(OutfitKind.Battle);
            public int Slot;
            public string? ItemId;
        }

        private sealed class GearSlotDto
        {
            public int Slot;
            public string? ItemId;
        }

        private sealed class HeroSpecDto
        {
            public string? Role;
            public string? Name;
            public bool? IsFemale;
            public string? Culture;
            public int? Level;
            public Dictionary<string, int> Skills = new();
            public List<GearSlotDto> Gear = new();
            public Dictionary<string, int> Attributes = new();
            public Dictionary<string, int> Traits = new();
            public Dictionary<string, int> Focus = new();
            public Dictionary<string, List<string>> Perks = new();
        }

        private sealed class FamilyDto
        {
            public string Relation = nameof(FamilyRelation.Father);
            public bool Alive;
            public int? Age;
            public string Marriage = nameof(FamilyMarriage.Single);
            public bool AtHolding;
            public string Aging = nameof(SiblingAging.Smart);
            public int? SmartOffset;
            public HeroSpecDto? Advanced;
        }

        private sealed class ItemCountDto
        {
            public string ItemId = string.Empty;
            public int Count;
        }

        private sealed class PresetData
        {
            public int Version = 6;
            public string StartType = nameof(Models.StartType.Commoner);
            public string Founding = nameof(MonarchFounding.Settler);
            public string QuestProgress = nameof(StoryQuestProgress.FirstPhaseStart);
            public string? KingdomId;
            public string? SettlementId;
            public string? LocationId;
            // False, matching the session's own default, because the answer for a
            // preset written before this field is whatever else the file holds: it
            // carries a LocationId, and defaulting to true threw that away and sent
            // the character somewhere the preset never named.
            public bool UseRandomLocation;
            public string? KingdomName;

            // Null on a preset written before the realm's name carried anything but
            // its text. What the text alone is worth is settled in ApplyData.
            public string? KingdomNameStyle;
            public bool? KingdomNameDecided;

            // The character's own name and their house's. Null is what a preset
            // written before these were carried was composed with: nothing settled,
            // and the culture's own name pools standing in.
            public string? PlayerFirstName;
            public string? PlayerClanName;

            // What culture the setup was composed against. Recorded and never put
            // back: the culture belongs to the game's own creation stage, and a
            // preset that reassigned it would leave the session describing gear and
            // troops the character is never given. Null on an older preset, which is
            // nothing known rather than a difference, so nothing is said.
            public string? CultureId;
            public int ClanTier;
            public int? Renown;
            public int Companions;
            public int? Age;
            public int? Gold;
            public int? Influence;
            public int? Troops;

            // The band each resource falls back to where no exact figure is set, and
            // carried alongside the exact figure rather than instead of it: the
            // provision plan reads the troop band to scale the grain whatever exact
            // muster is set. The defaults below are the answers a preset written
            // before the bands were carried was composed with, since Standard is
            // what a session starts on.
            public string GoldBand = nameof(RangePreset.Standard);
            public string InfluenceBand = nameof(RangePreset.Standard);
            public string TroopsBand = nameof(RangePreset.Standard);

            // How the character carries their station on their back, and what they set
            // out with in the larder. Provisions is skipped outright where an exact
            // stack of food or mounts is set, so it too is carried beside them.
            public string Bearing = nameof(ArmorBearing.Station);
            public string Provisions = nameof(ProvisionPlan.Sensible);
            public int? Level;
            public Dictionary<string, int> Attributes = new();
            public Dictionary<string, int> Focus = new();
            public Dictionary<string, int> SkillLevels = new();
            public Dictionary<string, int> Traits = new();
            public Dictionary<string, List<string>> Perks = new();
            public List<WeaponDto> Weapons = new();
            public List<OutfitDto> Outfit = new();
            public string? BannerId;
            public bool GearDefaultsApplied;
            public List<FamilyDto> Family = new();
            public List<HeroSpecDto> CompanionSpecs = new();
            public List<ItemCountDto> Food = new();
            public List<ItemCountDto> Mounts = new();
            public List<ItemCountDto> TradeGoods = new();

            // False on a preset written before the wagons could be answered as
            // empty, which is the answer those presets were composed with: an
            // empty TradeGoods meant nothing was named rather than nothing loaded.
            public bool NoTradeGoods;
            public int VassalClans;
            public bool GrantLands;
            public List<string> Policies = new();
            public List<string>? Wars;
            public List<string>? RealmWars;
            public int Workshops;
            public int? CrimeRating;
            public int? PackAnimals;
            public string? CareerId;
            public List<string>? NamedCompanions;
            public List<string>? CareerChoices;
            public int? FactionResource;
            public int? Garrison;
            public int? ContractPay;
            public List<string>? WantedBy;
            public int RebelAllies;

            // How far the start goes onto the water. A preset written before the
            // water existed carries no such field, and the default below is the
            // answer it was composed with: the start stays ashore.
            public string SeaDegree = nameof(Application.Scenarios.SeaDegree.None);
        }

        public static IReadOnlyList<string> ListNames()
        {
            try
            {
                var folder = PresetFolder();
                if (!Directory.Exists(folder)) return Array.Empty<string>();
                return Directory.EnumerateFiles(folder, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList()!;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartPresetService: listing presets failed.", ex);
                return Array.Empty<string>();
            }
        }

        public static bool Save(string name, CharacterCreationSession session)
        {
            try
            {
                Directory.CreateDirectory(PresetFolder());
                File.WriteAllText(PathFor(name),
                    JsonConvert.SerializeObject(BuildData(session), Formatting.Indented));
                CSLogger.Info($"StartPresetService: preset '{name}' saved.");
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StartPresetService: saving '{name}' failed.", ex);
                return false;
            }
        }

        /// <summary>
        ///     The whole editor setup as one string, so the editor can snapshot
        ///     the session when it opens and put everything back on Close.
        /// </summary>
        public static string CaptureState(CharacterCreationSession session)
        {
            return JsonConvert.SerializeObject(BuildData(session));
        }

        public static bool RestoreState(string state, CharacterCreationSession session)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<PresetData>(state);
                if (data == null) return false;
                ApplyData(data, session);
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartPresetService: session restore failed.", ex);
                return false;
            }
        }

        private static PresetData BuildData(CharacterCreationSession session)
        {
                var data = new PresetData
                {
                    StartType = session.SelectedStartType.ToString(),
                    Founding = session.SelectedFounding.ToString(),
                    KingdomId = session.SelectedKingdom?.StringId,
                    SettlementId = session.SelectedSettlement?.StringId,
                    LocationId = session.SelectedLocation?.StringId,
                    UseRandomLocation = session.UseRandomLocation,
                    KingdomName = session.KingdomName,
                    KingdomNameStyle = session.KingdomNameStyle.ToString(),
                    KingdomNameDecided = session.KingdomNameDecided,
                    PlayerFirstName = session.PlayerFirstName,
                    PlayerClanName = session.PlayerClanName,
                    CultureId = session.SelectedCulture?.StringId,
                    ClanTier = session.SelectedClanTier,
                    Renown = session.CustomRenown,
                    Companions = session.StartingCompanions,
                    Age = session.CustomAge,
                    Gold = session.CustomGold,
                    Influence = session.CustomInfluence,
                    Troops = session.CustomTroops,
                    GoldBand = session.SelectedGold.ToString(),
                    InfluenceBand = session.SelectedInfluence.ToString(),
                    TroopsBand = session.SelectedTroops.ToString(),
                    Bearing = session.Bearing.ToString(),
                    Provisions = session.Provisions.ToString(),
                    Level = session.CustomLevel,
                    Attributes = new Dictionary<string, int>(session.CustomAttributes),
                    Focus = new Dictionary<string, int>(session.CustomFocus),
                    SkillLevels = new Dictionary<string, int>(session.CustomSkillLevels),
                    Traits = new Dictionary<string, int>(session.CustomTraits),
                    Perks = session.CustomPerks.ToDictionary(p => p.Key, p => new List<string>(p.Value)),
                    BannerId = session.ExactBanner?.StringId,
                    QuestProgress = session.SelectedQuestProgress.ToString(),
                    GearDefaultsApplied = session.CustomGearDefaultsApplied,
                    Food = ToItemCounts(session.CustomFood),
                    Mounts = ToItemCounts(session.CustomMounts),
                    TradeGoods = ToItemCounts(session.CustomTradeGoods),
                    NoTradeGoods = session.NoTradeGoods,
                    VassalClans = session.VassalClanCount,
                    GrantLands = session.GrantLandsToVassals,
                    Policies = new List<string>(session.SelectedPolicies),
                    Wars = session.CustomWars == null ? null : new List<string>(session.CustomWars),
                    RealmWars = session.RealmWars == null ? null : new List<string>(session.RealmWars),
                    Workshops = session.StartingWorkshops,
                    CrimeRating = session.CustomCrimeRating,
                    PackAnimals = session.CustomPackAnimals,
                    CareerId = session.SelectedCareerId,
                    NamedCompanions = new List<string>(session.NamedCompanionIds),
                    CareerChoices = new List<string>(session.CareerChoiceIds),
                    FactionResource = session.CustomFactionResource,
                    Garrison = session.CustomGarrison,
                    ContractPay = session.CustomContractPay,
                    WantedBy = session.OutlawWantedBy == null
                        ? null
                        : new List<string>(session.OutlawWantedBy),
                    RebelAllies = session.RebelAllyCount,
                    SeaDegree = session.SelectedSeaDegree.ToString()
                };

                foreach (var spec in session.CompanionSpecs)
                    data.CompanionSpecs.Add(ToSpecDto(spec) ?? new HeroSpecDto());

                foreach (var choice in session.WeaponChoices)
                    data.Weapons.Add(new WeaponDto
                    {
                        Class = choice.WeaponClass.ToString(),
                        ItemId = choice.ExactItem?.StringId,
                        Empty = choice.ExplicitlyEmpty
                    });

                foreach (var pair in session.ExactOutfit)
                    data.Outfit.Add(new OutfitDto
                    {
                        Kind = pair.Key.Kind.ToString(),
                        Slot = (int)pair.Key.Slot,
                        ItemId = pair.Value?.StringId
                    });

                foreach (var member in session.FamilyMembers)
                    data.Family.Add(new FamilyDto
                    {
                        Relation = member.Relation.ToString(),
                        Alive = member.IsAlive,
                        Age = member.Age,
                        Marriage = member.Marriage.ToString(),
                        AtHolding = member.StaysInSettlement,
                        Aging = member.Aging.ToString(),
                        SmartOffset = member.SmartOffset,
                        Advanced = ToSpecDto(member.Advanced)
                    });

                return data;
        }

        public static bool Load(string name, CharacterCreationSession session)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<PresetData>(File.ReadAllText(PathFor(name)));
                if (data == null) return false;
                ApplyData(data, session);
                AnnounceComposedCulture(data.CultureId, session);
                CSLogger.Info($"StartPresetService: preset '{name}' loaded.");
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StartPresetService: loading '{name}' failed.", ex);
                return false;
            }
        }

        private static void ApplyData(PresetData data, CharacterCreationSession session)
        {
                // Assigned whatever the file says, never left alone: a guarded write
                // leaves the loading session's own answer standing, which is how a
                // preset comes to load as something it was never saved as.
                session.SelectedStartType = Enum.TryParse(data.StartType, out StartType startType)
                    ? startType
                    : StartType.Commoner;
                session.SelectedFounding = Enum.TryParse(data.Founding, out MonarchFounding founding)
                    ? founding
                    : MonarchFounding.Settler;

                session.SelectedKingdom = data.KingdomId == null
                    ? null
                    : Kingdom.All.FirstOrDefault(k => k.StringId == data.KingdomId);
                session.SelectedSettlement = FindSettlement(data.SettlementId);
                session.SelectedLocation = FindSettlement(data.LocationId);
                session.UseRandomLocation = data.UseRandomLocation;
                session.KingdomName = data.KingdomName;
                session.PlayerFirstName = data.PlayerFirstName;
                session.PlayerClanName = data.PlayerClanName;

                // An older preset carries the realm's name text and nothing else about
                // it. Every menu that writes that text settles the question in the same
                // breath, so text in the file is the evidence a decision was made, and
                // stored words are read as the player's own. No text is the undecided
                // state, which is what the preset was composed as. Assigned either way
                // rather than left alone, so the session's own answer cannot stand in
                // for one the preset never held.
                bool namedInFile = !string.IsNullOrWhiteSpace(data.KingdomName);
                session.KingdomNameDecided = data.KingdomNameDecided ?? namedInFile;
                session.KingdomNameStyle =
                    data.KingdomNameStyle != null &&
                    Enum.TryParse(data.KingdomNameStyle, out KingdomNameStyle nameStyle)
                        ? nameStyle
                        : namedInFile
                            ? KingdomNameStyle.Custom
                            : KingdomNameStyle.Automatic;

                session.SelectedClanTier = data.ClanTier;
                session.CustomRenown = data.Renown;
                session.StartingCompanions = data.Companions;
                session.CustomAge = data.Age;
                session.CustomGold = data.Gold;
                session.CustomInfluence = data.Influence;
                session.CustomTroops = data.Troops;

                // Assigned whatever the file holds, never left alone on a value that
                // will not parse: a band or a plan the preset never named would
                // otherwise be answered by the session the player is loading into.
                session.SelectedGold = Enum.TryParse(data.GoldBand, out RangePreset goldBand)
                    ? goldBand
                    : RangePreset.Standard;
                session.SelectedInfluence = Enum.TryParse(data.InfluenceBand, out RangePreset influenceBand)
                    ? influenceBand
                    : RangePreset.Standard;
                session.SelectedTroops = Enum.TryParse(data.TroopsBand, out RangePreset troopsBand)
                    ? troopsBand
                    : RangePreset.Standard;
                session.Bearing = Enum.TryParse(data.Bearing, out ArmorBearing bearing)
                    ? bearing
                    : ArmorBearing.Station;
                session.Provisions = Enum.TryParse(data.Provisions, out ProvisionPlan provisions)
                    ? provisions
                    : ProvisionPlan.Sensible;

                session.CustomLevel = data.Level;

                CopyInto(session.CustomAttributes, data.Attributes);
                CopyInto(session.CustomFocus, data.Focus);
                CopyInto(session.CustomSkillLevels, data.SkillLevels);
                CopyInto(session.CustomTraits, data.Traits);

                session.CustomPerks.Clear();
                foreach (var pair in data.Perks)
                    session.CustomPerks[pair.Key] = new List<string>(pair.Value);

                for (int index = 0; index < session.WeaponChoices.Length; index++)
                {
                    var choice = session.WeaponChoices[index];
                    choice.Reset();
                    if (index >= data.Weapons.Count) continue;
                    var dto = data.Weapons[index];
                    if (Enum.TryParse(dto.Class, out WeaponClassChoice weaponClass))
                        choice.WeaponClass = weaponClass;
                    choice.ExactItem = FindItem(dto.ItemId);
                    choice.ExplicitlyEmpty = dto.Empty;
                }

                session.ExactOutfit.Clear();
                foreach (var dto in data.Outfit)
                {
                    if (!Enum.TryParse(dto.Kind, out OutfitKind kind)) continue;
                    var item = FindItem(dto.ItemId);
                    // A null id means "explicitly empty"; a missing item means skip
                    if (dto.ItemId != null && item == null) continue;
                    session.ExactOutfit[(kind, (EquipmentIndex)dto.Slot)] = item;
                }

                session.ExactBanner = FindItem(data.BannerId);
                session.CustomGearDefaultsApplied = data.GearDefaultsApplied;
                session.SelectedQuestProgress =
                    Enum.TryParse(data.QuestProgress, out StoryQuestProgress questProgress)
                        ? questProgress
                        : StoryQuestProgress.FirstPhaseStart;

                session.FamilyMembers.Clear();
                foreach (var dto in data.Family)
                {
                    if (!Enum.TryParse(dto.Relation, out FamilyRelation relation)) continue;
                    var member = new FamilyMemberSpec(relation, dto.Alive)
                    {
                        Age = dto.Age,
                        StaysInSettlement = dto.AtHolding,
                        SmartOffset = dto.SmartOffset
                    };
                    if (Enum.TryParse(dto.Marriage, out FamilyMarriage marriage))
                        member.Marriage = marriage;
                    if (Enum.TryParse(dto.Aging, out SiblingAging aging))
                        member.Aging = aging;
                    ApplySpecDto(member.Advanced, dto.Advanced);
                    session.FamilyMembers.Add(member);
                }

                session.CompanionSpecs.Clear();
                foreach (var dto in data.CompanionSpecs)
                {
                    var spec = new HeroSpec();
                    ApplySpecDto(spec, dto);
                    session.CompanionSpecs.Add(spec);
                }

                LoadItemCounts(session.CustomFood, data.Food);
                LoadItemCounts(session.CustomMounts, data.Mounts);
                LoadItemCounts(session.CustomTradeGoods, data.TradeGoods);
                session.NoTradeGoods = data.NoTradeGoods;

                session.VassalClanCount = data.VassalClans;
                session.GrantLandsToVassals = data.GrantLands;
                session.SelectedPolicies.Clear();
                session.SelectedPolicies.AddRange(data.Policies);
                session.CustomWars = data.Wars == null ? null : new List<string>(data.Wars);
                session.RealmWars = data.RealmWars == null ? null : new List<string>(data.RealmWars);
                session.StartingWorkshops = data.Workshops;
                session.CustomCrimeRating = data.CrimeRating;
                session.CustomPackAnimals = data.PackAnimals;
                session.SelectedCareerId = data.CareerId;
                session.NamedCompanionIds.Clear();
                if (data.NamedCompanions != null) session.NamedCompanionIds.AddRange(data.NamedCompanions);
                session.CareerChoiceIds.Clear();
                if (data.CareerChoices != null) session.CareerChoiceIds.AddRange(data.CareerChoices);
                session.CustomFactionResource = data.FactionResource;
                session.CustomGarrison = data.Garrison;
                session.CustomContractPay = data.ContractPay;
                session.OutlawWantedBy = data.WantedBy == null ? null : new List<string>(data.WantedBy);
                session.RebelAllyCount = data.RebelAllies;

                // Assigned rather than run through SeaGrants.Choose: the holding and the
                // starting location the degree decides were saved alongside it and have
                // just been put back, and Choose would recompute them over the top.
                // Anything unreadable lands ashore rather than leaving the degree the
                // session happened to be carrying, which would grant ships no preset named
                session.SelectedSeaDegree =
                    Enum.TryParse(data.SeaDegree, out Application.Scenarios.SeaDegree seaDegree)
                        ? seaDegree
                        : Application.Scenarios.SeaDegree.None;
        }

        /// <summary>
        ///     A preset composed on one culture picks gear, troops, hulls and names
        ///     that a character of another culture is not given, and the culture is
        ///     the game's own creation stage to decide rather than this one's. So the
        ///     setup loads, the culture is left exactly as the player chose it, and
        ///     what the preset was composed on is stated plainly instead of being
        ///     quietly disagreed with.
        /// </summary>
        private static void AnnounceComposedCulture(string? composedOn, CharacterCreationSession session)
        {
            try
            {
                var current = session.SelectedCulture;
                if (composedOn == null || current == null || composedOn == current.StringId) return;

                var composed = MBObjectManager.Instance?.GetObject<CultureObject>(composedOn);
                string currentName = current.Name?.ToString() ?? current.StringId;
                var text = new TextObject(
                    "{=CSR_Preset_OtherCulture}This preset was composed for {COMPOSED}; this character is {CURRENT}. Anything it left automatic is chosen for {CURRENT}.");
                text.SetTextVariable("COMPOSED", composed?.Name?.ToString() ?? composedOn);
                text.SetTextVariable("CURRENT", currentName);
                InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Yellow));
                CSLogger.Info($"StartPresetService: preset composed on '{composedOn}', character is '{current.StringId}'.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartPresetService: reporting the preset's culture failed.", ex);
            }
        }

        public static bool Delete(string name)
        {
            try
            {
                File.Delete(PathFor(name));
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StartPresetService: deleting '{name}' failed.", ex);
                return false;
            }
        }

        public static string SanitizeName(string raw)
        {
            var cleaned = new string(raw.Trim()
                .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
                .ToArray());
            return cleaned.Length > 40 ? cleaned.Substring(0, 40) : cleaned;
        }

        /// <summary>Null when the spec is fully generated, so the JSON stays lean.</summary>
        private static HeroSpecDto? ToSpecDto(HeroSpec spec)
        {
            if (!spec.HasCustomization) return null;

            var dto = new HeroSpecDto
            {
                Role = spec.Role,
                Name = spec.Name,
                IsFemale = spec.IsFemale,
                Culture = spec.CultureId,
                Level = spec.Level,
                Skills = new Dictionary<string, int>(spec.SkillLevels),
                Attributes = new Dictionary<string, int>(spec.Attributes),
                Traits = new Dictionary<string, int>(spec.Traits),
                Focus = new Dictionary<string, int>(spec.Focus),
                Perks = spec.Perks.ToDictionary(p => p.Key, p => new List<string>(p.Value))
            };
            foreach (var pair in spec.Gear)
                dto.Gear.Add(new GearSlotDto { Slot = (int)pair.Key, ItemId = pair.Value?.StringId });
            return dto;
        }

        private static void ApplySpecDto(HeroSpec spec, HeroSpecDto? dto)
        {
            spec.Clear();
            if (dto == null) return;

            spec.Role = dto.Role;
            spec.Name = dto.Name;
            spec.IsFemale = dto.IsFemale;
            spec.CultureId = dto.Culture;
            spec.Level = dto.Level;
            foreach (var pair in dto.Skills)
                spec.SkillLevels[pair.Key] = pair.Value;
            foreach (var pair in dto.Attributes)
                spec.Attributes[pair.Key] = pair.Value;
            foreach (var pair in dto.Traits)
                spec.Traits[pair.Key] = pair.Value;
            foreach (var pair in dto.Focus)
                spec.Focus[pair.Key] = pair.Value;
            foreach (var pair in dto.Perks)
                spec.Perks[pair.Key] = new List<string>(pair.Value);
            foreach (var slot in dto.Gear)
            {
                var item = FindItem(slot.ItemId);
                // A null id means "explicitly empty"; a missing item means skip
                if (slot.ItemId != null && item == null) continue;
                spec.Gear[(EquipmentIndex)slot.Slot] = item;
            }
        }

        private static List<ItemCountDto> ToItemCounts(List<InventoryEntry> entries)
        {
            return entries
                .Select(e => new ItemCountDto { ItemId = e.Item.StringId, Count = e.Count })
                .ToList();
        }

        private static void LoadItemCounts(List<InventoryEntry> target, List<ItemCountDto> source)
        {
            target.Clear();
            foreach (var dto in source)
            {
                var item = FindItem(dto.ItemId);
                if (item == null || dto.Count <= 0) continue;
                target.Add(new InventoryEntry(item, dto.Count));
            }
        }

        private static void CopyInto(Dictionary<string, int> target, Dictionary<string, int> source)
        {
            target.Clear();
            foreach (var pair in source)
                target[pair.Key] = pair.Value;
        }

        private static Settlement? FindSettlement(string? id)
        {
            return id == null ? null : Settlement.All.FirstOrDefault(s => s.StringId == id);
        }

        private static ItemObject? FindItem(string? id)
        {
            return id == null ? null : MBObjectManager.Instance.GetObject<ItemObject>(id);
        }

        private static string PresetFolder()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "CulturedStartReloaded",
                "Presets");
        }

        private static string PathFor(string name)
        {
            return Path.Combine(PresetFolder(), SanitizeName(name) + ".json");
        }
    }
}
