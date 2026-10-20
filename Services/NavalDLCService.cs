using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Whether War Sails is loaded, and the only place in this mod that names
    ///     the three skills it adds.
    ///
    ///     The ids are string literals because the game gives us nothing else to
    ///     hold: War Sails declares its skills in code rather than in data, in
    ///     <c>NavalDLC.dll</c>'s <c>NavalDLC.CharacterDevelopment.NavalSkills</c>,
    ///     which registers them against the object manager under exactly these
    ///     three literals. There is no <c>DefaultSkills</c>-style member to bind
    ///     to, and no module XML declares them, so a literal here is the game's own
    ///     hard-coding rather than one of ours. Anything asking whether a skill is
    ///     a War Sails skill asks here, so the three names exist once.
    /// </summary>
    public static class NavalDLCService
    {
        private static bool? _isLoaded;

        public const string SkillMariner = "Mariner";
        public const string SkillBoatswain = "Boatswain";
        public const string SkillShipmaster = "Shipmaster";

        /// <summary>The three skills War Sails adds, and no others.</summary>
        public static readonly IReadOnlyList<string> NavalSkillIds =
            new[] { SkillMariner, SkillBoatswain, SkillShipmaster };

        public static bool IsNavalDLCLoaded()
        {
            if (_isLoaded.HasValue) return _isLoaded.Value;

            try
            {
                // Exact module id: a substring match also hits third-party addons
                // like "Tavernmaids - NavalDLC" and misreports the DLC as present.
                var modules = TaleWorlds.ModuleManager.ModuleHelper.GetModules();
                _isLoaded = modules.Any(m =>
                    string.Equals(m.Id, "NavalDLC", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                _isLoaded = false;
            }

            return _isLoaded.Value;
        }

        /// <summary>
        ///     Whether this skill is one War Sails brought. Answered from the id
        ///     rather than from whether the DLC is loaded, because a skill object
        ///     only exists at all when the DLC registered it: a caller holding one
        ///     is asking what it is, not whether it is there.
        /// </summary>
        public static bool IsNavalSkill(SkillObject? skill)
        {
            if (skill == null) return false;

            try
            {
                return IsNavalSkillId(skill.StringId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Whether this skill id is one of the three War Sails registers.</summary>
        public static bool IsNavalSkillId(string? skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;

            for (int i = 0; i < NavalSkillIds.Count; i++)
                if (string.Equals(NavalSkillIds[i], skillId, StringComparison.Ordinal))
                    return true;

            return false;
        }

        public static SkillObject? GetNavalSkill(string skillId)
        {
            if (!IsNavalSkillId(skillId)) return null;
            if (!IsNavalDLCLoaded()) return null;

            try
            {
                return Game.Current?.ObjectManager?.GetObject<SkillObject>(skillId);
            }
            catch
            {
                return null;
            }
        }
    }
}
