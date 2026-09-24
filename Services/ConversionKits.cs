using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>An outfit the running conversion dresses a new character in.</summary>
    public sealed class ConversionKit
    {
        public ConversionKit(string name, string rosterId)
        {
            Name = name;
            RosterId = rosterId;
        }

        public string Name { get; }
        public string RosterId { get; }
    }

    /// <summary>
    ///     The outfits a total conversion writes for its own character creation: one per background
    ///     it offers and one per kind of fighter a career makes. They are the gear a player who never
    ///     opened this mod would have begun in, so this mod offers them beside its own quartermaster
    ///     rather than replacing them silently.
    ///
    ///     They are read from the game's own object manager by the ids the conversion builds, so a
    ///     conversion that writes no such rosters simply offers none.
    /// </summary>
    public static class ConversionKits
    {
        private const string BackgroundPrefix = "player_char_creation_";
        private const string CareerPrefix = "player_career_";

        /// <summary>Every kit the conversion writes for this culture and sex.</summary>
        public static IReadOnlyList<ConversionKit> KitsFor(CultureObject? culture, bool isFemale)
        {
            var kits = new List<ConversionKit>();
            if (culture == null || !TaomBridge.IsLoaded) return kits;

            string suffix = isFemale ? "_f" : "_m";
            string background = BackgroundPrefix + culture.StringId + "_";
            string career = CareerPrefix + culture.StringId + "_";

            try
            {
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                foreach (var roster in rosters)
                {
                    string id = roster.StringId;
                    if (!id.EndsWith(suffix, StringComparison.Ordinal)) continue;

                    if (id.StartsWith(background, StringComparison.Ordinal))
                        kits.Add(new ConversionKit(Title(id, background, suffix, false), id));
                    else if (id.StartsWith(career, StringComparison.Ordinal))
                        kits.Add(new ConversionKit(Title(id, career, suffix, true), id));
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"ConversionKits: reading the conversion's outfits failed: {ex.Message}");
            }

            kits.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return kits;
        }

        /// <summary>
        ///     The kit's own name for a player: what it dresses, in the conversion's own words for
        ///     the background or the kind of fighter, rather than the roster id.
        /// </summary>
        private static string Title(string rosterId, string prefix, string suffix, bool isCareer)
        {
            string middle = rosterId.Substring(prefix.Length, rosterId.Length - prefix.Length - suffix.Length);
            string spaced = middle.Replace('_', ' ');

            var label = new TextObject(isCareer
                ? "{=CSR_Kit_Career}{KIND} kit"
                : "{=CSR_Kit_Background}{KIND} background");
            label.SetTextVariable("KIND", spaced);
            return label.ToString();
        }

        /// <summary>
        ///     Dresses the outfit from the kit, slot by slot, exactly as choosing each item by hand
        ///     would: what the kit carries becomes an exact choice, and a slot it leaves empty is
        ///     left empty rather than handed back to the quartermaster, since the kit is a whole
        ///     outfit and half of one is neither.
        /// </summary>
        public static bool Apply(CharacterCreationSession session, OutfitKind kind, string rosterId)
        {
            var roster = MBObjectManager.Instance.GetObject<MBEquipmentRoster>(rosterId);
            var equipment = roster?.AllEquipments?.FirstOrDefault();
            if (equipment == null)
            {
                CSLogger.Warn($"ConversionKits: outfit '{rosterId}' holds no equipment; nothing was dressed.");
                return false;
            }

            var armorSlots = new[]
            {
                EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg,
                EquipmentIndex.Gloves, EquipmentIndex.Cape
            };

            foreach (var slot in armorSlots)
                session.ExactOutfit[(kind, slot)] = equipment[slot].Item;

            if (kind != OutfitKind.Stealth)
            {
                session.ExactOutfit[(kind, EquipmentIndex.Horse)] = equipment[EquipmentIndex.Horse].Item;
                session.ExactOutfit[(kind, EquipmentIndex.HorseHarness)] =
                    equipment[EquipmentIndex.HorseHarness].Item;
            }

            if (kind == OutfitKind.Battle)
                for (int slot = 0; slot < 4; slot++)
                {
                    var item = equipment[(EquipmentIndex)slot].Item;
                    var choice = session.WeaponChoices[slot];
                    choice.Reset();
                    choice.ExactItem = item;
                    choice.ExplicitlyEmpty = item == null;
                }

            CSLogger.Info($"ConversionKits: {kind} outfit dressed from the conversion's '{rosterId}'.");
            return true;
        }
    }
}
