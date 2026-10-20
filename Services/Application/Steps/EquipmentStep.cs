using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Settings;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     Applies the previewed equipment verbatim (what you saw is what you get),
    ///     falling back to fresh generation when no preview exists, then resolves
    ///     the four weapon slot choices.
    /// </summary>
    public sealed class EquipmentStep : IStartStep
    {
        private readonly IEquipmentGenerator _generator = new EquipmentGenerator();

        public string Name => "Equipment";

        public string? Validate(StartContext context)
        {
            return context.Hero.BattleEquipment == null
                ? "hero has no battle equipment"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var session = context.Session;
            int effectiveTier = EffectiveTier(session, context.Settings);

            if (session.PreviewEquipment != null && !session.PreviewEquipment[EquipmentIndex.Body].IsEmpty)
            {
                ApplyPreviewArmor(hero, session.PreviewEquipment);
                CSLogger.Info("EquipmentStep: applied preview equipment.");
            }
            else
            {
                _generator.GenerateHeroEquipment(hero, effectiveTier);
                CSLogger.Info($"EquipmentStep: generated fresh equipment at tier {effectiveTier}.");
            }

            EquipmentGenerator.ClearHarnessWithoutMount(hero.BattleEquipment);

            _generator.GenerateCivilianEquipment(hero, effectiveTier);
            _generator.GenerateStealthEquipment(hero, effectiveTier);

            ApplyExactPicks(hero, session, context);

            // One item per slot: two slots asking for the same weapon class used to
            // be answered with the same weapon twice
            var carried = CarriedIds(hero.BattleEquipment);
            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
                ApplyWeaponSlot(hero, session, EquipmentIndex.Weapon0 + slotIndex,
                    session.WeaponChoices[slotIndex], effectiveTier, carried);

            SmartQuartermaster.FillAutoSlots(hero, session, effectiveTier);

            ScheduleExclusivitySweep(hero, session);
        }

        /// <summary>Every item id the character is already carrying in a weapon slot.</summary>
        internal static HashSet<string> CarriedIds(Equipment? equipment)
        {
            var carried = new HashSet<string>(StringComparer.Ordinal);
            if (equipment == null) return carried;

            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Weapon3; slot++)
            {
                var item = equipment[slot].Item;
                if (item != null) carried.Add(item.StringId);
            }

            return carried;
        }

        /// <summary>
        ///     Exact player picks overlay whatever was generated; they always win.
        ///     A null pick means the player wants that slot empty.
        /// </summary>
        private static void ApplyExactPicks(Hero hero, CharacterCreationSession session, StartContext context)
        {
            foreach (var pair in session.ExactOutfit)
            {
                var (kind, slot) = pair.Key;
                var equipment = kind switch
                {
                    OutfitKind.Battle => hero.BattleEquipment,
                    OutfitKind.Civilian => hero.CivilianEquipment,
                    OutfitKind.Stealth => VersionedGameApi.StealthEquipment(hero),
                    _ => null
                };

                if (equipment == null)
                {
                    context.Report.AddProblem($"Gear: the {kind} outfit is unavailable");
                    continue;
                }

                if (pair.Value == null)
                {
                    equipment[slot] = default;
                    CSLogger.Info($"EquipmentStep: {kind} [{slot}] left empty by choice.");
                    continue;
                }

                equipment[slot] = new EquipmentElement(pair.Value);
                CSLogger.Info($"EquipmentStep: exact {kind} [{slot}] {pair.Value.Name}.");
            }

            if (session.ExactBanner != null && hero.BattleEquipment != null)
            {
                hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(session.ExactBanner);
                CSLogger.Info($"EquipmentStep: banner {session.ExactBanner.Name}.");
            }
        }

        internal static int EffectiveTier(CharacterCreationSession session, CSSettings? settings) =>
            TierFor(session, settings, session.Bearing);

        /// <summary>
        ///     The tier the stores would dress this character in under a given
        ///     bearing. The gear chapter asks for a bearing the session does not
        ///     hold yet, so the bearing is a parameter rather than read from it.
        /// </summary>
        internal static int TierFor(CharacterCreationSession session, CSSettings? settings,
            Models.ArmorBearing bearing)
        {
            var range = settings?.GetEquipmentTierRange(session.SelectedStartType) ?? (0, 2);
            int tier = session.EffectiveClanTier + bearing switch
            {
                Models.ArmorBearing.Plain => -1,
                Models.ArmorBearing.Finest => 1,
                _ => 0
            };
            return Math.Max(range.min, Math.Min(tier, range.max));
        }

        /// <summary>
        ///     Dresses the hero in the gear the player chose, before the vanilla
        ///     screens that follow this mod's flow are built.
        ///
        ///     The banner editor and the difficulty screen both read
        ///     `Hero.MainHero.BattleEquipment` ONCE, when their view is constructed,
        ///     and never look again. Applying the start's gear at the end of the
        ///     pipeline is too late for both of them, which is why they showed a
        ///     character in whatever the game had put on them rather than in what
        ///     the player spent the whole flow choosing. Nothing here is the
        ///     authoritative apply: this step runs again later and wins.
        /// </summary>
        public static void DressForTheRemainingScreens(CharacterCreationSession session)
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null) return;

                var preview = session.PreviewEquipment;
                if (preview != null && !preview[EquipmentIndex.Body].IsEmpty)
                {
                    ApplyPreviewArmor(hero, preview);
                }
                else
                {
                    // No battle armor was staged, as when the Start Editor's battle slots were left
                    // to the quartermaster or emptied, so the apply would generate fresh armor and
                    // these screens would show whatever the game had put on. The armor is drawn
                    // now and kept as the preview, so the apply hands over the very set shown here.
                    int tier = EffectiveTier(session, MCM.Abstractions.Base.Global.GlobalSettings<CSSettings>.Instance);
                    new EquipmentGenerator().GenerateHeroEquipment(hero, tier);
                    session.PreviewEquipment = new Equipment(hero.BattleEquipment);
                    CSLogger.Info($"EquipmentStep: no staged battle armor, so tier {tier} armor was drawn for these screens and kept.");
                }

                CSLogger.Info("EquipmentStep: the hero is dressed for the screens that follow.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("EquipmentStep: dressing the hero early failed; the later apply still stands.", ex);
            }
        }

        private static void ApplyPreviewArmor(Hero hero, Equipment preview)
        {
            foreach (var slot in EquipmentGenerator.ArmorSlots)
            {
                var element = preview[slot];
                if (!element.IsEmpty)
                    hero.BattleEquipment[slot] = element;

                // The preview is built before the realm exists, so it cannot know
                // it is dressing a monarch
                var crown = EquipmentGenerator.Regalia(hero, hero.Culture, slot, OutfitKind.Battle);
                if (crown != null)
                    hero.BattleEquipment[slot] = new EquipmentElement(crown);
            }

            var horse = preview[EquipmentIndex.Horse];
            if (!horse.IsEmpty)
            {
                hero.BattleEquipment[EquipmentIndex.Horse] = horse;

                var harness = preview[EquipmentIndex.HorseHarness];
                if (!harness.IsEmpty)
                    hero.BattleEquipment[EquipmentIndex.HorseHarness] = harness;
            }

            // Last word on the pair, whatever put them together: the preview, a saved preset, a
            // conversion's own outfit or the quartermaster. A harness cut for another animal
            // renders as a torn mess on the one wearing it
            foreach (var outfit in new[] { hero.BattleEquipment, hero.CivilianEquipment })
            {
                string? removed = MountFit.Reconcile(outfit);
                if (removed != null) CSLogger.Info($"  {removed}.");
            }
        }

        // The sweep has to run after ConsequenceStep, which is four steps further
        // down the orchestrator, so it is handed to the first campaign tick and
        // taken off again the moment it fires.
        private static readonly object SweepOwner = new();
        private static Hero? _sweepHero;
        private static CharacterCreationSession? _sweepSession;

        private static void ScheduleExclusivitySweep(Hero hero, CharacterCreationSession session)
        {
            try
            {
                if (Campaign.Current == null) return;

                // Cultured Start's keepsakes are carried, never put on, as that route
                // has always handed them over
                if (session.Mode == Models.SetupMode.LifePath) return;

                _sweepHero = hero;
                _sweepSession = session;
                CampaignEventDispatcher.Instance?.RemoveListeners(SweepOwner);
                CampaignEvents.TickEvent.AddNonSerializedListener(SweepOwner, OnSweepTick);
            }
            catch (Exception ex)
            {
                CSLogger.Error("EquipmentStep: the exclusivity sweep could not be scheduled.", ex);
            }
        }

        private static void OnSweepTick(float dt)
        {
            var hero = _sweepHero;
            var session = _sweepSession;
            _sweepHero = null;
            _sweepSession = null;

            try
            {
                CampaignEventDispatcher.Instance?.RemoveListeners(SweepOwner);
            }
            catch (Exception ex)
            {
                CSLogger.Error("EquipmentStep: the exclusivity sweep could not be unhooked.", ex);
            }

            if (hero == null || session == null) return;

            try
            {
                SweepExclusiveGrants(hero, session);
            }
            catch (Exception ex)
            {
                CSLogger.Error("EquipmentStep: the exclusivity sweep failed; the start still stands.", ex);
            }
        }

        /// <summary>
        ///     One item per slot, after every step has had its say.
        ///
        ///     Gear arrives from places that cannot see each other: this step
        ///     dresses the character, the quartermaster fills the weapon slots, and
        ///     the life path's own consequences drop what the story handed over
        ///     into the party stores. That is how a character rode out wearing the
        ///     Saddle of Aeneas with a Cataphract Scale Barding in the baggage: two
        ///     harnesses, one horse, and no use for the second. What the stores hold
        ///     that a slot could take is worn when it is the better piece, so the
        ///     character ends up with one answer per slot and it is the best one
        ///     they were given.
        ///
        ///     Nothing is destroyed to get there. The piece that loses a slot goes
        ///     into the baggage, whichever side it came from: the panel names what
        ///     an answer hands the player, and an item that arrives and is then
        ///     deleted was never handed over at all. One piece leaves
        ///     the stores per slot won, so stores holding three of a thing end up
        ///     with one worn and two carried rather than one worn and none.
        ///
        ///     Food, cargo and mounts are left alone: those are stores, not a second
        ///     answer to a slot, and a spare horse pulls its weight in a party.
        /// </summary>
        internal static void SweepExclusiveGrants(Hero hero, CharacterCreationSession session)
        {
            var battle = hero.BattleEquipment;
            var roster = hero.PartyBelongedTo?.ItemRoster;
            if (battle == null || roster == null) return;

            // What the character was wearing before a store item took the slot.
            // Held back and returned after the walk rather than during it, so the
            // roster is never grown while it is being indexed
            var displaced = new List<ItemObject>();

            for (int index = roster.Count - 1; index >= 0; index--)
            {
                var element = roster.GetElementCopyAtIndex(index);
                var item = element.EquipmentElement.Item;
                if (item == null) continue;

                var slot = ContestedSlot(battle, session, item);
                if (slot == null) continue;

                var worn = battle[slot.Value].Item;
                if (worn != null && !Outranks(item, worn))
                {
                    // It stays exactly where it is. The character wears the better
                    // piece and still owns this one
                    CSLogger.Info($"EquipmentStep: {item.Name} stays in the baggage; " +
                                  $"{worn.Name} in [{slot.Value}] is the better piece.");
                    continue;
                }

                battle[slot.Value] = new EquipmentElement(item);
                roster.AddToCounts(element.EquipmentElement, -1);
                if (worn != null) displaced.Add(worn);

                CSLogger.Info(worn == null
                    ? $"EquipmentStep: {item.Name} from the stores is worn in [{slot.Value}]."
                    : $"EquipmentStep: {item.Name} replaces {worn.Name} in [{slot.Value}]; " +
                      "the piece it replaces goes to the baggage.");
            }

            foreach (var piece in displaced)
                roster.AddToCounts(new EquipmentElement(piece), 1);
        }

        /// <summary>
        ///     The slot this item would compete for, or null when it is not a second
        ///     answer to anything: stores, ammunition, mounts and banners all stay.
        ///     A slot the player settled themselves is nobody else's to fill.
        /// </summary>
        private static EquipmentIndex? ContestedSlot(Equipment battle,
            CharacterCreationSession session, ItemObject item)
        {
            var armor = ArmorSlotFor(item.ItemType);
            if (armor != null)
            {
                if (session.ExactOutfit.ContainsKey((OutfitKind.Battle, armor.Value)))
                    return null;

                // A harness is no use worn or stored while there is nothing to put
                // it on, so it waits in the stores for a horse
                if (armor.Value == EquipmentIndex.HorseHarness &&
                    !MountFit.Fits(battle[EquipmentIndex.Horse].Item, item))
                    return null;

                return armor;
            }

            var weaponClass = GearQuery.ClassifyChoice(item);
            if (weaponClass == null || IsAmmunition(weaponClass.Value)) return null;
            if (item.ItemType == ItemObject.ItemTypeEnum.Banner) return null;

            EquipmentIndex? firstFree = null;
            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
            {
                var slot = EquipmentIndex.Weapon0 + slotIndex;
                var choice = session.WeaponChoices[slotIndex];
                if (choice.ExplicitlyEmpty || choice.ExactItem != null) continue;

                var carried = battle[slot].Item;
                if (carried == null)
                {
                    firstFree ??= slot;
                    continue;
                }

                if (GearQuery.ClassifyChoice(carried) == weaponClass.Value)
                    return slot;
            }

            // Nothing of its kind is carried: an empty hand takes it, and a full
            // loadout of other kinds leaves it alone, because it duplicates nothing
            return firstFree;
        }

        private static bool IsAmmunition(WeaponClassChoice choice) =>
            choice is WeaponClassChoice.Arrows or WeaponClassChoice.Bolts or WeaponClassChoice.Stone;

        private static EquipmentIndex? ArmorSlotFor(ItemObject.ItemTypeEnum type) => type switch
        {
            ItemObject.ItemTypeEnum.HeadArmor => EquipmentIndex.Head,
            ItemObject.ItemTypeEnum.BodyArmor => EquipmentIndex.Body,
            ItemObject.ItemTypeEnum.LegArmor => EquipmentIndex.Leg,
            ItemObject.ItemTypeEnum.HandArmor => EquipmentIndex.Gloves,
            ItemObject.ItemTypeEnum.Cape => EquipmentIndex.Cape,
            ItemObject.ItemTypeEnum.HorseHarness => EquipmentIndex.HorseHarness,
            _ => null
        };

        /// <summary>The game's own reading of which piece is the better one.</summary>
        private static bool Outranks(ItemObject candidate, ItemObject worn)
        {
            int candidateTier = (int)candidate.Tier;
            int wornTier = (int)worn.Tier;
            return candidateTier != wornTier
                ? candidateTier > wornTier
                : candidate.Value > worn.Value;
        }

        private static void ApplyWeaponSlot(Hero hero, CharacterCreationSession session,
            EquipmentIndex slot, GearChoice choice, int effectiveTier, HashSet<string> carried)
        {
            if (choice.ExplicitlyEmpty)
            {
                hero.BattleEquipment[slot] = default;
                CSLogger.Info($"EquipmentStep: [{slot}] left empty by choice.");
                return;
            }

            if (choice.IsKeepAuto)
                return;

            // The exact pick always wins; the previewed roll wins over a fresh one
            var item = choice.ExactItem;
            if (item == null)
            {
                var preview = session.PreviewEquipment;
                if (preview != null)
                {
                    var element = preview[slot];
                    if (!element.IsEmpty && element.Item != null)
                        item = element.Item;
                }
            }

            // A stacking class is exempt from the no-duplicates rule: a set that
            // names javelins twice is asking for two sheaves, and excluding the
            // first one answered the second slot with nothing at all
            item ??= GearQuery.Resolve(choice, hero.Culture, effectiveTier, session,
                GearQuery.Stacks(choice.WeaponClass) ? null : carried);

            if (item != null)
            {
                hero.BattleEquipment[slot] = new EquipmentElement(item);
                carried.Add(item.StringId);
                CSLogger.Info($"EquipmentStep: [{slot}] {item.Name} ({choice}).");
            }
            else
            {
                CSLogger.Warn($"EquipmentStep: no item found for {choice} in slot {slot}.");
            }
        }
    }
}
