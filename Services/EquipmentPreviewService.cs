using System;
using System.Collections.Generic;
using System.Reflection;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application.Steps;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    /// Manages equipment preview during character creation.
    /// Creates a runtime MBEquipmentRoster so the 3D character model accurately
    /// depicts the equipment the player will receive.
    /// </summary>
    public class EquipmentPreviewService
    {
        private const string PreviewRosterId = "cs_preview_roster";

        /// <summary>
        ///     One roster per outfit, so the stage can put the character up more
        ///     than once and show every outfit an option decides at the same time.
        ///     Sharing a single roster between civilian and stealth meant only one
        ///     of them could ever be on screen.
        /// </summary>
        private static readonly Dictionary<OutfitKind, string> RosterIds = new()
        {
            { OutfitKind.Battle, PreviewRosterId },
            { OutfitKind.Civilian, "cs_preview_roster_civilian" },
            { OutfitKind.Stealth, "cs_preview_roster_stealth" }
        };

        private readonly Dictionary<OutfitKind, MBEquipmentRoster> _rosters = new();

        /// <summary>
        ///     What this draw dressed the character in, per outfit. Every render
        ///     reads this; no render draws. A draw that happened inside a render
        ///     is what made the kit appear to change on its own, and a draw held
        ///     back from a render is what made re-clicking an option do nothing.
        /// </summary>
        private readonly Dictionary<OutfitKind, Equipment> _drawn = new();

        /// <summary>
        ///     The battle slots the player set by hand. A re-draw leaves these
        ///     alone: cycling is for what the stores chose, never for what the
        ///     player chose.
        /// </summary>
        private readonly HashSet<EquipmentIndex> _pinned = new();

        private MBEquipmentRoster? _previewRoster;
        private Equipment? _previewEquipment;
        private Equipment? _displayEquipment;
        private FieldInfo? _equipmentsField;

        public void Initialize()
        {
            CSLogger.Info(">>> START: EquipmentPreviewService.Initialize");
            try
            {
                // Cache the reflection field for MBEquipmentRoster's internal equipment list
                _equipmentsField = AccessTools.Field(typeof(MBEquipmentRoster), "_equipments");
                if (_equipmentsField == null)
                {
                    CSLogger.Warn("  Could not find _equipments field on MBEquipmentRoster: will use fallback.");
                }

                _previewEquipment = new Equipment();
                CSLogger.Info("<<< END: EquipmentPreviewService.Initialize [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: EquipmentPreviewService.Initialize [FAILED]", ex);
            }
        }

        /// <summary>
        ///     Dresses the character, in every outfit at once, and stores what it
        ///     drew so the renders can read it.
        ///
        ///     Called from the options that decide gear, and called again whenever
        ///     one of them is clicked again. Each call is a fresh draw, which is
        ///     the whole point: one option can decide the battle kit, the town
        ///     clothes and the quiet clothes together, and a player who does not
        ///     like what they were handed clicks the same option again and is
        ///     handed something else. A slot only repeats once the choices have
        ///     narrowed it to one thing worth wearing.
        /// </summary>
        public void GenerateArmorPreview(CultureObject? culture, int tier)
        {
            CSLogger.Info($">>> START: GenerateArmorPreview (culture={culture?.StringId}, tier={tier})");
            try
            {
                _previewEquipment ??= new Equipment();

                foreach (var slot in EquipmentGenerator.ArmorSlots)
                {
                    if (_pinned.Contains(slot)) continue;

                    var item = EquipmentGenerator.FindArmorItem(slot, tier, culture, -1, false,
                        _previewEquipment[slot].Item);
                    _previewEquipment[slot] = item != null
                        ? new EquipmentElement(item)
                        : EquipmentElement.Invalid;

                    if (item != null)
                        CSLogger.Debug($"  Preview [{EquipmentGenerator.SlotName(slot)}]: {item.Name} (Tier={item.Tier})");
                }

                DrawMount(culture, tier);

                _drawn[OutfitKind.Battle] = new Equipment(_previewEquipment);
                DrawSocialOutfit(OutfitKind.Civilian, culture, tier);
                DrawSocialOutfit(OutfitKind.Stealth, culture, tier);

                CreationSession.Current.PreviewEquipment = _previewEquipment;
                UpdateRoster();

                CSLogger.Info("<<< END: GenerateArmorPreview [SUCCESS]");
            }
            catch (Exception ex)
            {
                CSLogger.Error("<<< END: GenerateArmorPreview [FAILED]", ex);
            }
        }

        /// <summary>
        ///     The mount and its harness, which only a standing that can keep a
        ///     horse gets at all.
        /// </summary>
        private void DrawMount(CultureObject? culture, int tier)
        {
            if (_previewEquipment == null) return;

            if (tier < 3)
            {
                if (!_pinned.Contains(EquipmentIndex.Horse))
                    _previewEquipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                if (!_pinned.Contains(EquipmentIndex.HorseHarness))
                    _previewEquipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

                return;
            }

            if (!_pinned.Contains(EquipmentIndex.Horse))
            {
                int ridingSkill = EstimateRidingSkill();
                CSLogger.Info($"  Estimated riding skill: {ridingSkill}");

                var horse = EquipmentGenerator.FindArmorItem(EquipmentIndex.Horse, tier, culture,
                    ridingSkill, false, _previewEquipment[EquipmentIndex.Horse].Item);
                _previewEquipment[EquipmentIndex.Horse] = horse != null
                    ? new EquipmentElement(horse) : EquipmentElement.Invalid;
            }

            if (_previewEquipment[EquipmentIndex.Horse].IsEmpty ||
                _pinned.Contains(EquipmentIndex.HorseHarness)) return;

            var harness = EquipmentGenerator.FindArmorItem(EquipmentIndex.HorseHarness, tier, culture,
                -1, false, _previewEquipment[EquipmentIndex.HorseHarness].Item);
            _previewEquipment[EquipmentIndex.HorseHarness] = harness != null
                ? new EquipmentElement(harness) : EquipmentElement.Invalid;
        }

        /// <summary>
        ///     The town clothes and the quiet clothes, drawn by the same functions
        ///     that hand them over at the end of creation, so what the stage shows
        ///     is what the character will be wearing.
        /// </summary>
        private void DrawSocialOutfit(OutfitKind kind, CultureObject? culture, int tier)
        {
            _drawn.TryGetValue(kind, out var previous);
            var outfit = new Equipment();

            foreach (var slot in EquipmentGenerator.ArmorSlots)
            {
                var worn = previous?[slot].Item;
                var item = kind == OutfitKind.Stealth
                    ? EquipmentGenerator.FindStealthItem(slot, tier, culture, worn)
                    : EquipmentGenerator.FindArmorItem(slot, tier, culture, -1, true, worn);

                outfit[slot] = item != null
                    ? new EquipmentElement(item)
                    : EquipmentElement.Invalid;

                if (item != null)
                    CSLogger.Debug($"  Preview {kind} [{EquipmentGenerator.SlotName(slot)}]: " +
                                   $"{item.Name} (Tier={item.Tier})");
            }

            _drawn[kind] = outfit;
        }

        /// <summary>
        ///     Previews the given slot's gear choice. An exact pick shows that item;
        ///     a class-only choice re-rolls a quartermaster pick on each call.
        /// </summary>
        public void GenerateWeaponPreview(GearChoice choice, EquipmentIndex slot,
            CultureObject? culture, int tier)
        {
            try
            {
                if (_previewEquipment == null)
                    _previewEquipment = new Equipment();

                if (choice.IsKeepAuto)
                {
                    _previewEquipment[slot] = EquipmentElement.Invalid;
                }
                else
                {
                    // What the slot holds now is excluded, so a player who asks
                    // the quartermaster again is handed a different weapon rather
                    // than the same one back
                    var worn = _previewEquipment[slot].Item;
                    var carried = worn != null
                        ? new HashSet<string>(StringComparer.Ordinal) { worn.StringId }
                        : null;

                    var item = GearQuery.Resolve(choice, culture, tier, CreationSession.Current, carried)
                               ?? GearQuery.Resolve(choice, culture, tier, CreationSession.Current);
                    _previewEquipment[slot] = item != null
                        ? new EquipmentElement(item)
                        : EquipmentElement.Invalid;

                    if (item != null)
                        CSLogger.Info($"Preview weapon [{slot}]: {item.Name} (Tier={item.Tier}).");
                    else
                        CSLogger.Warn($"Preview: no weapon found for {choice}.");
                }

                CreationSession.Current.PreviewEquipment = _previewEquipment;
                UpdateRoster();
            }
            catch (Exception ex)
            {
                CSLogger.Error("GenerateWeaponPreview failed.", ex);
            }
        }

        /// <summary>
        ///     Places one exactly chosen item into the preview (and therefore into
        ///     what the player will receive, via the session's PreviewEquipment).
        /// </summary>
        public void SetExactItem(EquipmentIndex slot, ItemObject item)
        {
            try
            {
                _previewEquipment ??= new Equipment();
                _previewEquipment[slot] = new EquipmentElement(item);
                _pinned.Add(slot);
                CreationSession.Current.PreviewEquipment = _previewEquipment;
                UpdateRoster();
                CSLogger.Info($"Preview: exact item [{slot}] {item.Name}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("SetExactItem failed.", ex);
            }
        }

        /// <summary>
        ///     Pins exactly the battle slots the player set by hand and releases every other one. A
        ///     pick or an emptying pins its slot against regeneration, and a pin the picks no longer
        ///     called for kept a slot handed back to the quartermaster empty on every later draw.
        /// </summary>
        public void PinExactly(IEnumerable<EquipmentIndex> slots)
        {
            _pinned.Clear();
            _pinned.UnionWith(slots);
        }

        /// <summary>Empties one battle slot the player explicitly wants bare.</summary>
        public void SetSlotEmpty(EquipmentIndex slot)
        {
            try
            {
                _previewEquipment ??= new Equipment();
                _previewEquipment[slot] = EquipmentElement.Invalid;
                _pinned.Add(slot);
                CreationSession.Current.PreviewEquipment = _previewEquipment;
                UpdateRoster();
                CSLogger.Info($"Preview: [{slot}] emptied by choice.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("SetSlotEmpty failed.", ex);
            }
        }

        /// <summary>
        ///     Switches which outfit the single on-stage character wears. Every
        ///     outfit was drawn together, so this only chooses which of them is
        ///     showing; nothing is drawn here, and the battle kit is never
        ///     mutated, so what gets applied at finalization is unaffected by
        ///     what is currently displayed.
        /// </summary>
        public void ShowOutfit(OutfitKind kind, CharacterCreationSession session,
            CultureObject? culture, int tier)
        {
            try
            {
                if (kind == OutfitKind.Battle)
                {
                    _displayEquipment = null;
                    UpdateRoster();
                    return;
                }

                EnsureDrawn(culture, tier);
                _displayEquipment = Dressed(kind, session);
                UpdateRoster();
                CSLogger.Info($"Preview: showing the {kind} outfit.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("ShowOutfit failed.", ex);
            }
        }

        /// <summary>
        ///     What the character is wearing in one outfit: the draw, with the
        ///     player's own picks for that outfit laid over it.
        /// </summary>
        private Equipment Dressed(OutfitKind kind, CharacterCreationSession? session)
        {
            var dressed = _drawn.TryGetValue(kind, out var drawn)
                ? new Equipment(drawn)
                : new Equipment();

            if (session == null) return dressed;

            foreach (var pair in session.ExactOutfit)
            {
                if (pair.Key.Kind != kind) continue;
                dressed[pair.Key.Slot] = pair.Value != null
                    ? new EquipmentElement(pair.Value)
                    : EquipmentElement.Invalid;
            }

            return dressed;
        }

        /// <summary>
        ///     Every outfit this draw dressed, in the order a player reads them.
        ///     The stage puts the character up once per entry, so an option that
        ///     decides several outfits at once shows all of them and the player is
        ///     never choosing on a glimpse of one third of what they are getting.
        /// </summary>
        public IReadOnlyList<OutfitKind> DressedOutfits
        {
            get
            {
                var kinds = new List<OutfitKind>();
                foreach (var kind in new[] { OutfitKind.Battle, OutfitKind.Civilian, OutfitKind.Stealth })
                    if (_drawn.TryGetValue(kind, out var outfit) && AnythingOn(outfit))
                        kinds.Add(kind);

                return kinds;
            }
        }

        private static bool AnythingOn(Equipment? outfit)
        {
            if (outfit == null) return false;
            foreach (var slot in EquipmentGenerator.ArmorSlots)
                if (!outfit[slot].IsEmpty)
                    return true;

            return false;
        }

        /// <summary>
        ///     The roster showing one outfit, ready to render. Each outfit has its
        ///     own, so several can be on stage at once.
        /// </summary>
        public string RosterIdFor(OutfitKind kind)
        {
            return _rosters.ContainsKey(kind) && RosterIds.TryGetValue(kind, out var id)
                ? id
                : GetPreviewRosterId();
        }

        /// <summary>
        ///     A roster showing the character in the outfit a chapter calls for.
        ///     Asked during a render, so it reads the draw rather than making one:
        ///     drawing inside a render is how the gear came and went between
        ///     chapters, and a cache keyed on how MANY picks the player had made
        ///     is how changing one of them showed the old outfit.
        /// </summary>
        public string GetPreviewRosterId(OutfitKind kind, CharacterCreationSession session,
            CultureObject? culture, int tier)
        {
            if (kind == OutfitKind.Battle) return GetPreviewRosterId();

            try
            {
                EnsureDrawn(culture, tier);
                WriteRoster(kind, Dressed(kind, session));
                return RosterIdFor(kind);
            }
            catch (Exception ex)
            {
                CSLogger.Error("EquipmentPreviewService: the social outfit roster failed; battle gear stands.", ex);
                return GetPreviewRosterId();
            }
        }

        /// <summary>
        ///     A chapter can ask to show the town clothes before any gear option
        ///     has been touched, so the first ask draws rather than showing a
        ///     character in nothing.
        /// </summary>
        private void EnsureDrawn(CultureObject? culture, int tier)
        {
            if (_drawn.Count > 0) return;
            GenerateArmorPreview(culture, tier);
        }

        /// <summary>
        /// Returns the preview roster ID for use in NarrativeMenuCharacterArgs.
        /// Falls back to "player_char_creation_default" if roster creation failed.
        /// </summary>
        public string GetPreviewRosterId()
        {
            return _previewRoster != null ? PreviewRosterId : "player_char_creation_default";
        }

        /// <summary>
        ///     True once the preview roster exists or preview equipment was
        ///     generated. Checking the body slot alone breaks when the player
        ///     explicitly empties it: the character must stay bound to the
        ///     preview roster even while wearing nothing.
        /// </summary>
        public bool HasPreviewEquipment => _previewRoster != null ||
            (_previewEquipment != null && !_previewEquipment[EquipmentIndex.Body].IsEmpty);

        /// <summary>
        /// Applies the held weapon to the NarrativeMenuCharacter for display in hand.
        /// Call after the character's equipment has been set via the roster.
        /// </summary>
        public void ApplyWeaponToHand(NarrativeMenuCharacter character, EquipmentIndex weaponSlot)
        {
            if (_previewEquipment == null) return;

            var element = _previewEquipment[weaponSlot];
            if (element.IsEmpty || element.Item == null) return;

            var item = element.Item;
            if (item.ItemType == ItemObject.ItemTypeEnum.Shield)
                character.SetLeftHandItem(item.StringId);
            else
                character.SetRightHandItem(item.StringId);
        }

        public void Cleanup()
        {
            foreach (var roster in _rosters.Values)
            {
                try
                {
                    MBObjectManager.Instance.UnregisterObject(roster);
                }
                catch (Exception ex)
                {
                    CSLogger.Warn($"  Failed to unregister preview roster: {ex.Message}");
                }
            }

            if (_rosters.Count > 0) CSLogger.Info($"  {_rosters.Count} preview roster(s) unregistered.");

            _rosters.Clear();
            _previewRoster = null;
            _previewEquipment = null;
            _displayEquipment = null;
            _drawn.Clear();
            _pinned.Clear();
        }

        /// <summary>
        ///     The riding skill the character will actually start with, computed by
        ///     the same catalog-based formula the apply pipeline uses, so the
        ///     preview can never show a horse the player cannot ride.
        /// </summary>
        private static int EstimateRidingSkill()
        {
            return NarrativeStep.ExpectedSkillValue(CreationSession.Current, DefaultSkills.Riding);
        }

        /// <summary>
        ///     Writes one outfit into its own roster, creating it on first use.
        ///     Reuses a roster left behind by a backed-out creation run instead of
        ///     colliding on a duplicate object id.
        /// </summary>
        private void WriteRoster(OutfitKind kind, Equipment outfit)
        {
            if (!RosterIds.TryGetValue(kind, out var rosterId)) return;

            if (!_rosters.TryGetValue(kind, out var roster))
            {
                roster = MBObjectManager.Instance.GetObject<MBEquipmentRoster>(rosterId)
                         ?? MBObjectManager.Instance.CreateObject<MBEquipmentRoster>(rosterId);
                _rosters[kind] = roster;
                if (kind == OutfitKind.Battle) _previewRoster = roster;
                CSLogger.Info($"  Runtime roster ready: {rosterId}");
            }

            if (_equipmentsField == null)
            {
                CSLogger.Warn("  Cannot update roster: _equipments field not found.");
                return;
            }

            var copy = new Equipment(outfit);
            if (_equipmentsField.GetValue(roster) is MBList<Equipment> list)
            {
                list.Clear();
                list.Add(copy);
            }
            else
            {
                _equipmentsField.SetValue(roster, new MBList<Equipment> { copy });
            }
        }

        /// <summary>
        ///     Refreshes every roster the stage can render, so the character on
        ///     stage and every other copy of them agree without the stage having
        ///     to ask which outfit changed.
        /// </summary>
        private void UpdateRoster()
        {
            var shown = _displayEquipment ?? _previewEquipment;
            if (shown == null) return;

            try
            {
                WriteRoster(OutfitKind.Battle, shown);

                var session = CreationSession.Current;
                foreach (var kind in new[] { OutfitKind.Civilian, OutfitKind.Stealth })
                    if (_drawn.ContainsKey(kind))
                        WriteRoster(kind, Dressed(kind, session));

                CSLogger.Debug("  Rosters updated with preview equipment via reflection.");

                // Make the 3D character re-read the roster right now instead of
                // on the next menu interaction
                CharacterCreation.Menus.CharacterPreviewHelper.RefreshMenuCharacters();
            }
            catch (Exception ex)
            {
                CSLogger.Error($"  Failed to update preview roster: {ex.Message}");
            }
        }
    }
}
