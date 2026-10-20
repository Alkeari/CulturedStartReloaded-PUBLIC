using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What a ruler wears, taken from the game's own answer rather than a table
    ///     written here.
    ///
    ///     The item data cannot support a table anyway. No head armor carries a
    ///     price or a tier, `difficulty` is zero on every one of them, and not one
    ///     head item in the game is marked for a gender, so "a crown, but the
    ///     feminine one, but the Empire's, but the Northern Empire's" cannot be
    ///     answered from item attributes at all. The game answers it in a different
    ///     place: thirty-two equipment rosters named `&lt;culture&gt;_king_template_&lt;civ
    ///     or bat&gt;_&lt;m or f&gt;`, which already encode culture, gender and whether the
    ///     set is civilian or battle. Northern Empire is the one pair where the
    ///     king and the queen differ: he takes the Golden Laurel Crown, she takes
    ///     the Imperial Jeweled Band.
    ///
    ///     Reading those rosters rather than naming items also survives a total
    ///     conversion. Realm of Thrones reuses the base culture ids for entirely
    ///     different peoples, so `empire` means something else there and any
    ///     hard-coded culture-to-crown mapping would be wrong; it ships its own
    ///     rosters, and this reads those instead without knowing it happened.
    /// </summary>
    public static class RegaliaQuery
    {
        /// <summary>
        ///     The head piece a ruler of this culture and sex wears, or null when
        ///     the running game has no ruler roster for them. Never invents one:
        ///     a culture with no royal set gets whatever the quartermaster would
        ///     have picked, which is the honest answer rather than another
        ///     culture's crown.
        /// </summary>
        public static ItemObject? Crown(CultureObject? culture, bool isFemale, OutfitKind kind)
        {
            var crowns = new List<ItemObject>();

            foreach (var roster in RulerRosters(culture, isFemale, kind))
            {
                var head = roster.DefaultEquipment[EquipmentIndex.Head];
                if (head.Item != null) crowns.Add(head.Item);
            }

            if (crowns.Count == 0) return null;

            // A ruler roster can name another mod's head piece, which the player may have
            // asked not to be offered. FilterAllowed keeps the order, most specific first,
            // and hands back the unfiltered list rather than an empty one, so filtering
            // moves a crown down the list and never leaves a monarch bare-headed
            return OfficialItemRegistry.FilterAllowed(crowns).FirstOrDefault();
        }

        /// <summary>
        ///     Every ruler roster that fits, most specific first. The prefixes are
        ///     the game's own abbreviations, and the Empire has three because it is
        ///     three realms; without a way to tell which, the Northern set leads
        ///     because it is the one carrying the laurel.
        /// </summary>
        private static IEnumerable<MBEquipmentRoster> RulerRosters(CultureObject? culture,
            bool isFemale, OutfitKind kind)
        {
            string sex = isFemale ? "f" : "m";
            string set = kind == OutfitKind.Battle ? "bat" : "civ";
            string? cultureId = culture?.StringId;

            foreach (var prefix in Prefixes(cultureId))
            {
                var roster = Roster($"{prefix}_king_template_{set}_{sex}")
                             ?? Roster($"{prefix}_king_template_{set}_{(isFemale ? "m" : "f")}");
                if (roster != null) yield return roster;
            }

            // A conversion may name its royal sets anything at all, so fall back to
            // scanning for a roster that is both a ruler set and this culture's
            foreach (var roster in ScanForRulerRosters(cultureId, set, sex))
                yield return roster;
        }

        private static IEnumerable<string> Prefixes(string? cultureId)
        {
            switch (cultureId)
            {
                case "empire":
                    yield return "n_emp";
                    yield return "w_emp";
                    yield return "s_emp";
                    break;
                case "aserai": yield return "ase"; break;
                case "battania": yield return "bat"; break;
                case "khuzait": yield return "khu"; break;
                case "sturgia": yield return "stu"; break;
                case "vlandia": yield return "vla"; break;
                default:
                    if (!string.IsNullOrEmpty(cultureId))
                    {
                        yield return cultureId!;
                        yield return cultureId!.Substring(0, Math.Min(3, cultureId.Length));
                    }

                    break;
            }
        }

        /// <summary>
        ///     Last resort: any roster whose id reads as a ruler set for this
        ///     culture. Matches on the id rather than on an item name, because item
        ///     names lie here: the Khuzait crown is called an Ornate Silk Cap and
        ///     two distinct Empire crowns share one display name.
        /// </summary>
        private static IEnumerable<MBEquipmentRoster> ScanForRulerRosters(string? cultureId,
            string set, string sex)
        {
            if (string.IsNullOrEmpty(cultureId)) yield break;

            List<MBEquipmentRoster> all;
            try
            {
                all = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>().ToList();
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"RegaliaQuery: the roster list was unreadable ({ex.GetType().Name}).");
                yield break;
            }

            foreach (var roster in all)
            {
                var id = roster?.StringId;
                if (id == null || roster == null) continue;
                if (id.IndexOf("king", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (id.IndexOf(cultureId!, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (id.IndexOf("_" + set + "_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!id.EndsWith("_" + sex, StringComparison.OrdinalIgnoreCase)) continue;

                yield return roster;
            }
        }

        private static MBEquipmentRoster? Roster(string id)
        {
            try
            {
                if (Game.Current != null)
                {
                    var found = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(id);
                    if (found != null) return found;
                }

                return MBObjectManager.Instance.GetObject<MBEquipmentRoster>(id);
            }
            catch
            {
                return null;
            }
        }
    }
}
