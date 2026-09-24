using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.ModuleManager;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Whether a total conversion is running, and which modules belong to it.
    ///
    ///     A conversion does not have to replace the game's object ids, and the one this was written
    ///     against does not: Realm of Thrones keeps `empire`, `battania`, `town_EW4` and the rest,
    ///     and changes what they mean. Braavos answers to `empire`; King's Landing answers to
    ///     `town_EW4`. Nothing goes missing to hint that anything changed, StringId and Name no
    ///     longer agree, and no manifest declares a conversion at all: it is done by overriding
    ///     native XML paths and by patching at runtime. So the only honest test is whether the
    ///     conversion's own module is switched on, and the only safe rule afterwards is that a
    ///     vanilla id is no longer evidence of the vanilla thing.
    ///
    ///     More conversions are intended, so this is a table rather than a Realm of Thrones check.
    /// </summary>
    public static class TotalConversionService
    {
        private sealed class Conversion
        {
            public Conversion(string name, string moduleId, params string[] contentModules)
            {
                Name = name;
                ModuleId = moduleId;
                ContentModules = contentModules;
            }

            /// <summary>What to call it in a log or a message.</summary>
            public string Name { get; }

            /// <summary>The module whose presence means the conversion is running.</summary>
            public string ModuleId { get; }

            /// <summary>Every module whose content is the conversion's own, not a third party's.</summary>
            public IReadOnlyList<string> ContentModules { get; }
        }

        // Match on declared module ids, never on folder names: Realm of Thrones ships a folder
        // called ROT-Map whose manifest declares the id ROT_Map.
        private static readonly Conversion[] Known =
        {
            new("Realm of Thrones", "ROT-Core", "ROT-Core", "ROT-Content", "ROT_Map", "ROT-Dragon"),
            new("Tales from the Age of Men", "TAOM", "TAOM", "TAOM_Map", "LOTRLOME_Armory")
        };

        private static Conversion? _active;
        private static bool _resolved;

        /// <summary>True while a known total conversion is switched on.</summary>
        public static bool IsActive => Active() != null;

        /// <summary>The running conversion's name, or null when there is none.</summary>
        public static string? ActiveName => Active()?.Name;

        /// <summary>
        ///     True when the module belongs to the running conversion. Its content is the game the
        ///     player chose to play, so it is treated as the game's own rather than as a mod's.
        /// </summary>
        public static bool OwnsModule(string moduleId)
        {
            var active = Active();
            return active != null && active.ContentModules.Any(
                id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Forgets the answer so the next question re-reads the module list.</summary>
        public static void Reset()
        {
            _active = null;
            _resolved = false;
        }

        private static Conversion? Active()
        {
            if (_resolved) return _active;
            _resolved = true;

            try
            {
                // IsModuleActive reads ModuleInfo.IsActive, which is the enabled state. GetModules
                // answers "loaded" and would report a conversion the player has switched off in the
                // launcher, which would put this mod into conversion mode for a vanilla game.
                _active = Array.Find(Known, c => VersionedGameApi.IsModuleActive(c.ModuleId));
                CSLogger.Info(_active == null
                    ? "TotalConversionService: no total conversion is active."
                    : $"TotalConversionService: {_active.Name} is active ({_active.ModuleId}).");
            }
            catch (Exception ex)
            {
                _active = null;
                CSLogger.Warn($"TotalConversionService: reading the module list failed: {ex.Message}");
            }

            return _active;
        }
    }
}
