using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.ModuleManager;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Knows which items come from OTHER mods. The game's own items are
    ///     largely packed rather than shipped as loose XML, so the reliable test
    ///     is inverted: mods must define their items in loose ModuleData XML, so
    ///     any item id found in a non-official module's XML counts as modded and
    ///     everything else counts as official. Mod-added items often clip or
    ///     leave body parts invisible, so the pickers and the quartermaster use
    ///     official items only unless the player enables modded content.
    /// </summary>
    public static class OfficialItemRegistry
    {
        private static readonly HashSet<string> OfficialModules = new(StringComparer.OrdinalIgnoreCase)
        {
            "Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle", "BirthAndDeath", "Multiplayer",
            "NavalDLC", "CulturedStartReloaded", "Cultured Start Reloaded",
            // The Realm of Thrones download is this same mod under another
            // identity, so anything it ever ships counts the way the main
            // download's would. It ships no item XML today; the day it does,
            // leaving these out would have its own items read as modded.
            "CulturedStartReloadedROT", "Cultured Start Reloaded - RoT"
        };

        private static HashSet<string>? _moddedIds;

        /// <summary>
        ///     True when the module's content counts as the game's own. A running total conversion
        ///     IS the game the player chose, so its modules answer yes: otherwise every one of its
        ///     items reads as third-party and is filtered out, and since a conversion can leave the
        ///     base game's items in place, what survives the filter is the gear of the world the
        ///     player replaced. Realm of Thrones is the case in hand, where that left Calradian kit
        ///     being offered in Westeros.
        /// </summary>
        private static bool IsOfficialModule(string moduleName)
        {
            return OfficialModules.Contains(moduleName) || TotalConversionService.OwnsModule(moduleName);
        }

        public static bool IsOfficial(ItemObject item)
        {
            return !ModdedIds().Contains(item.StringId);
        }

        /// <summary>True when any installed mod ships items of its own.</summary>
        public static bool AnyModdedItems => ModdedIds().Count > 0;

        /// <summary>
        ///     The list with modded items removed when the setting is off. If the
        ///     filter would empty the list, the original list is kept so pickers
        ///     never break.
        /// </summary>
        public static List<ItemObject> FilterAllowed(List<ItemObject> items)
        {
            if (GlobalSettings<CSSettings>.Instance?.AllowModdedItems == true)
                return items;

            var official = items.Where(IsOfficial).ToList();
            return official.Count > 0 ? official : items;
        }

        private static HashSet<string> ModdedIds()
        {
            if (_moddedIds != null) return _moddedIds;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var idPattern = new Regex("<(?:Item|CraftedItem)\\b[^>]*?\\bid=\"([^\"]+)\"",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            try
            {
                foreach (var moduleName in Utilities.GetModulesNames())
                {
                    if (IsOfficialModule(moduleName)) continue;

                    try
                    {
                        var modulePath = ModuleHelper.GetModuleFullPath(moduleName);
                        if (string.IsNullOrEmpty(modulePath)) continue;

                        var dataPath = System.IO.Path.Combine(modulePath, "ModuleData");
                        if (!Directory.Exists(dataPath)) continue;

                        foreach (var file in Directory.EnumerateFiles(dataPath, "*.xml",
                                     SearchOption.AllDirectories))
                        {
                            // Language folders hold no item definitions; skip the bulk
                            if (file.IndexOf("Languages", StringComparison.OrdinalIgnoreCase) >= 0)
                                continue;

                            foreach (Match match in idPattern.Matches(File.ReadAllText(file)))
                                ids.Add(match.Groups[1].Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        CSLogger.Warn($"OfficialItemRegistry: scanning {moduleName} failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"OfficialItemRegistry: module enumeration failed: {ex.Message}");
            }

            CSLogger.Info($"OfficialItemRegistry: {ids.Count} modded item ids indexed" +
                          (TotalConversionService.IsActive
                              ? $"; {TotalConversionService.ActiveName} counts as official content."
                              : "."));
            _moddedIds = ids;
            return ids;
        }
    }
}
