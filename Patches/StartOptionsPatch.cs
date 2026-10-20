using System;
using CulturedStartReloaded.Services;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Puts this mod's two routes into the game's own Advanced Starting Options screen instead
    ///     of competing with it, and keeps exactly one system granting the start.
    ///
    ///     The game's documented extension point, a static method carrying [StartOptionsProvider],
    ///     cannot reach this mod: AdvancedStartOptionsManager scans
    ///     ModuleHelper.GetActiveGameAssemblies(), which only ever sees assemblies named by a module
    ///     manifest and their static references, and the BUTR module loader opens the versioned mod
    ///     assembly by filename at runtime. Patching the public factory reaches the same object the
    ///     providers are handed.
    ///
    ///     None of these carry [HarmonyPatch]. The screen exists only from game v1.5.0, and naming
    ///     an absent type in an attribute stops the whole class from loading, which stopped Harmony
    ///     from applying any patch in the assembly. <see cref="Services.GameCompat"/> resolves the
    ///     targets and applies these by hand, or skips them where the screen is not there.
    ///     Everything below is written in types every supported game has.
    /// </summary>
    public static class StartOptionsPatch
    {
        private const int VanillaApplyPass = 8;

        /// <summary>Postfix on AdvancedStartOptionsManager.CreateCampaignStartOptions.</summary>
        public static void AddModStartEntries(object __result)
        {
            try
            {
                if (GameCompat.AddStartTypeEntries(__result, AdvancedStartBridge.EntryIds))
                    CSLogger.Info("StartOptionsPatch: added the Cultured Start, " +
                                  "Cultured Start Revamped and Start Editor entries.");
                else
                    CSLogger.Warn("StartOptionsPatch: no start-type list to add to; leaving the screen alone.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartOptionsPatch: adding the start entries failed; the game's own screen is left as it was.", ex);
            }
        }

        /// <summary>
        ///     Postfix on ListAdvancedStartOption.GetListItemName. The game resolves an entry's
        ///     label through its global text manager, keyed
        ///     str_campaign_starting_options_item_name.&lt;identifier&gt;, and a mod's own ModuleData
        ///     GameText file is not loaded when this screen is drawn at the main menu, which showed
        ///     both entries as "ERROR: Text with id ...". Answering the resolver directly keeps
        ///     every string this mod owns in sta_strings.xml.
        /// </summary>
        public static void NameModStartEntry(string identifier, ref TextObject __result)
        {
            try
            {
                var key = identifier switch
                {
                    AdvancedStartBridge.CulturedStartId => "{=CSR_StartOption_Cultured}Cultured Start",
                    AdvancedStartBridge.RevampedStartId =>
                        "{=CSR_StartOption_Revamped}Cultured Start Revamped",
                    AdvancedStartBridge.StartEditorId => "{=CSR_StartOption_Editor}Start Editor",
                    _ => null
                };
                if (key != null) __result = new TextObject(key);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartOptionsPatch: naming a start entry failed; the game keeps its own label.", ex);
            }
        }

        /// <summary>Postfix on ListAdvancedStartOption.GetListItemDescription.</summary>
        public static void DescribeModStartEntry(string identifier, ref TextObject __result)
        {
            try
            {
                var key = identifier switch
                {
                    AdvancedStartBridge.CulturedStartId =>
                        "{=CSR_StartOption_Cultured_Desc}Answer seven chapters, from the family you were born into to the reason you took the road, and begin with the life you composed.",
                    AdvancedStartBridge.RevampedStartId =>
                        "{=CSR_StartOption_Revamped_Desc}Forge the character choice by choice, from the house you were small in to the name people use for you now, and begin with the life you composed.",
                    AdvancedStartBridge.StartEditorId =>
                        "{=CSR_StartOption_Editor_Desc}Set every detail directly: attributes, skills, perks, traits, gear, stores, family and companions.",
                    _ => null
                };
                if (key != null) __result = new TextObject(key);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartOptionsPatch: describing a start entry failed; the game keeps its own label.", ex);
            }
        }

        /// <summary>
        ///     Prefix on CampaignAdvancedStartingPlayerOptionsCampaignBehavior.OnCharacterCreationIsOver.
        ///
        ///     The game raises OnCharacterCreationIsOverEvent ten times, index 0 through 9, as an
        ///     ordering ladder. Its own starting-options behavior grants the start on index 8; this
        ///     mod grants it on the first pass. Without this, a mod start was applied and then
        ///     partly overwritten eight passes later.
        /// </summary>
        public static bool SuppressVanillaStart(int index) => GrantIsTheirs(index, "the game's own");

        /// <summary>
        ///     Prefix on War Sails' NavalAdvancedStartingPlayerOptionsCampaignBehavior.OnCharacterCreationIsOver,
        ///     which the DLC runs on that same pass 8.
        ///
        ///     Its two start-type branches already go inert for a mod start, since the chosen start
        ///     type is one of this mod's own ids, but the personal ship is not gated on the start
        ///     type at all: the method reads IsPersonalShipEnabled and hands over an
        ///     eastern_trade_ship or northern_trade_ship. A player who ticked that option and then
        ///     chose Cultured Start or the Start Editor was given a ship no panel of this mod named,
        ///     which this mod must never do. The only other thing the method does for a mod start is a
        ///     trailing MemberRoster.UpdateVersion, which the roster's own mutators have already
        ///     done, so suppressing the whole method costs nothing else.
        ///
        ///     A Vanilla Start keeps its ship: ModOwnsTheStart is false there, so this answers true
        ///     and War Sails runs exactly as it would with this mod absent.
        /// </summary>
        public static bool SuppressNavalStart(int index) => GrantIsTheirs(index, "War Sails'");

        /// <summary>
        ///     Whether the system being patched still owns the grant on this pass. False only on
        ///     the pass those systems grant on, and only when this mod has already granted.
        /// </summary>
        private static bool GrantIsTheirs(int index, string system)
        {
            try
            {
                if (index != VanillaApplyPass) return true;
                if (!AdvancedStartBridge.ModOwnsTheStart()) return true;

                CSLogger.Info($"StartOptionsPatch: this mod owns the start; {system} grant is skipped.");
                return false;
            }
            catch (Exception ex)
            {
                CSLogger.Error($"StartOptionsPatch: deciding ownership failed; {system} start is kept.", ex);
                return true;
            }
        }
    }
}
