using System;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Routes the mod's menus through <see cref="CreationFlow"/> instead of the
    ///     menus' declared neighbor ids. The route is computed BEFORE any side
    ///     effect: when this patch handles the switch it fires the selected option's
    ///     consequence itself and suppresses vanilla; when it falls through, vanilla
    ///     runs unmodified and fires the consequence exactly once.
    ///
    ///     It falls through for the game's own menus only. A menu of this mod is always
    ///     answered here, in both directions and at both ends of the flow, because the
    ///     declared neighbor ids vanilla would follow are the flow table read without its
    ///     conditions: the row beside a route's first menu belongs to another route, so a
    ///     single fall-through is enough to hand the player a route they did not choose.
    /// </summary>
    [HarmonyPatch]
    public static class MenuRoutingPatch
    {
        [HarmonyPatch(typeof(CharacterCreationManager), "TrySwitchToNextMenu")]
        [HarmonyPrefix]
        public static bool TrySwitchToNextMenuPrefix(CharacterCreationManager __instance, ref bool __result)
        {
            try
            {
                var currentMenu = __instance.CurrentMenu;
                if (currentMenu == null || !currentMenu.StringId.StartsWith("cs_"))
                    return true;

                // The vanilla route hands over to the game's own narrative menus,
                // which stay registered behind this mod's; from there the vanilla
                // chain runs untouched, as if the mod were not installed
                if (currentMenu.StringId == "cs_mode_menu" &&
                    CreationSession.Current.Mode == Models.SetupMode.Vanilla)
                {
                    var vanillaFirst = __instance.NarrativeMenus.FirstOrDefault(m =>
                        !m.StringId.StartsWith("cs_") && m.InputMenuId == "start");

                    if (__instance.SelectedOptions.TryGetValue(currentMenu, out var modeOption))
                        modeOption.OnConsequence(__instance);

                    if (vanillaFirst != null)
                    {
                        SwitchTo(__instance, vanillaFirst);
                        TaomCompat.ContinueCareerFastPath(__instance);
                        __result = true;
                    }
                    else
                    {
                        CSLogger.Warn("MenuRoutingPatch: no vanilla narrative menus found; ending the stage.");
                        __result = false;
                    }

                    return false;
                }

                return ForwardFromAModMenu(__instance, currentMenu, ref __result);
            }
            catch (Exception ex)
            {
                CSLogger.Error("Error in MenuRoutingPatch (Next).", ex);
                return true;
            }
        }

        [HarmonyPatch(typeof(CharacterCreationManager), "TrySwitchToPreviousMenu")]
        [HarmonyPrefix]
        public static bool TrySwitchToPreviousMenuPrefix(CharacterCreationManager __instance, ref bool __result)
        {
            try
            {
                var currentMenu = __instance.CurrentMenu;
                if (currentMenu == null) return true;

                return currentMenu.StringId.StartsWith("cs_")
                    ? BackFromAModMenu(__instance, currentMenu, ref __result)
                    : BackFromAVanillaMenu(__instance, currentMenu, ref __result);
            }
            catch (Exception ex)
            {
                CSLogger.Error("Error in MenuRoutingPatch (Prev).", ex);
                return true;
            }
        }

        /// <summary>
        ///     Next from one of this mod's menus, which is this mod's to answer in every case.
        ///     Vanilla walks the menus' declared neighbor ids, and those are the flow table's
        ///     own rows read without its conditions, so letting it answer for a mod menu hands
        ///     the player a menu belonging to a route they did not choose.
        /// </summary>
        private static bool ForwardFromAModMenu(
            CharacterCreationManager manager, NarrativeMenu currentMenu, ref bool result)
        {
            // The route was just chosen, so its own menus take the ids both routes share
            // before the next one is looked up by id
            if (currentMenu.StringId == "cs_mode_menu")
                CharacterCreation.CulturedStart.CulturedStartMenus.Activate(manager, CreationSession.Current.Mode);

            var nextMenu = NextRegistered(manager, currentMenu.StringId, forward: true);

            // Vanilla fires the selected option's consequence itself, so it is fired here
            // exactly once whether the flow continues or the menu stage ends
            if (manager.SelectedOptions.TryGetValue(currentMenu, out var selectedOption))
                selectedOption.OnConsequence(manager);

            if (nextMenu == null)
            {
                result = false;
                return false;
            }

            SwitchTo(manager, nextMenu);
            result = true;
            return false;
        }

        /// <summary>
        ///     Back from one of this mod's menus. Where the session's route has an earlier menu
        ///     the stage returns to it; where it has none this reports "no previous menu", which
        ///     leaves the narrative stage exactly as backing out of the game's own first menu
        ///     does. It never hands the answer to vanilla, whose notion of "previous" is the
        ///     flow table's preceding row with none of its conditions applied: from the guided
        ///     route's first scene that row is the Start Editor's own menu, and from there it is
        ///     the route choice, so two presses of Back walked a player who chose Cultured Start
        ///     into both of the routes they did not choose.
        /// </summary>
        private static bool BackFromAModMenu(
            CharacterCreationManager manager, NarrativeMenu currentMenu, ref bool result)
        {
            var prevMenu = NextRegistered(manager, currentMenu.StringId, forward: false);
            if (prevMenu == null)
            {
                result = false;
                return false;
            }

            SwitchTo(manager, prevMenu);
            result = true;
            return false;
        }

        /// <summary>
        ///     Back from one of the game's own narrative menus, which the vanilla route walks.
        ///     Backing out of its first menu returns to this mod's route choice, but only where
        ///     this mod owns that choice: when the game's Advanced Starting Options named the
        ///     route there is no choice of ours to return to, and offering one would hand the
        ///     player the two routes they did not pick.
        /// </summary>
        private static bool BackFromAVanillaMenu(
            CharacterCreationManager manager, NarrativeMenu currentMenu, ref bool result)
        {
            var session = CreationSession.Current;
            if (currentMenu.InputMenuId != "start" || session.Mode != Models.SetupMode.Vanilla ||
                session.RouteDecidedExternally)
                return true;

            var modeMenu = manager.GetNarrativeMenuWithId("cs_mode_menu");
            if (modeMenu == null) return true;

            SwitchTo(manager, modeMenu);
            result = true;
            return false;
        }

        /// <summary>
        ///     The next menu of the session's own route in the given direction that is actually
        ///     registered, or null at that route's end. A flow menu that failed to register is
        ///     stepped over rather than handed back to vanilla, since vanilla's neighbor for it
        ///     belongs to whichever route happens to sit beside it in the table.
        /// </summary>
        private static NarrativeMenu? NextRegistered(
            CharacterCreationManager manager, string fromMenuId, bool forward)
        {
            var session = CreationSession.Current;
            string id = forward
                ? CreationFlow.GetNext(session, fromMenuId)
                : CreationFlow.GetPrevious(session, fromMenuId);

            while (!string.IsNullOrEmpty(id))
            {
                var menu = manager.GetNarrativeMenuWithId(id);
                if (menu != null) return menu;

                CSLogger.Error($"MenuRoutingPatch: flow menu '{id}' is not registered; it is stepped over.");
                id = forward ? CreationFlow.GetNext(session, id) : CreationFlow.GetPrevious(session, id);
            }

            return null;
        }

        /// <summary>
        ///     The stage opens on the first menu whose InputMenuId is "start", which is
        ///     the mode menu, not whichever menu leads the list. When the game's own
        ///     Advanced Starting Options already named the route, the mode menu has
        ///     nothing left to ask, so the stage is moved straight onto that route's
        ///     own first menu.
        /// </summary>
        [HarmonyPatch(typeof(CharacterCreationManager), "StartNarrativeStage")]
        [HarmonyPostfix]
        public static void StartNarrativeStagePostfix(CharacterCreationManager __instance)
        {
            try
            {
                var session = CreationSession.Current;

                // The stage restarts on its first menu whenever it is activated, including by a
                // step back from the banner editor after it, which threw away where the player
                // was. A route of this mod resumes on the chapter the stage was left from.
                if (session.Mode != Models.SetupMode.Vanilla && !string.IsNullOrEmpty(session.ResumeMenuId))
                {
                    var resume = __instance.GetNarrativeMenuWithId(session.ResumeMenuId!);
                    if (resume != null)
                    {
                        SwitchTo(__instance, resume);
                        CSLogger.Info($"MenuRoutingPatch: the stage resumed on {session.ResumeMenuId}.");
                        return;
                    }
                }

                if (!session.RouteDecidedExternally || session.Mode == Models.SetupMode.Vanilla)
                    return;

                var firstId = CreationFlow.GetFirst(session);
                if (string.IsNullOrEmpty(firstId)) return;
                if (__instance.CurrentMenu?.StringId == firstId) return;

                var target = __instance.GetNarrativeMenuWithId(firstId);
                if (target == null)
                {
                    CSLogger.Warn($"MenuRoutingPatch: '{firstId}' is not registered; the mode menu still opens.");
                    return;
                }

                SwitchTo(__instance, target);
                CSLogger.Info($"MenuRoutingPatch: route already decided as {session.Mode}; opened on {firstId}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("MenuRoutingPatch: opening the decided route failed; the mode menu stands.", ex);
            }
        }

        private static void SwitchTo(CharacterCreationManager manager, NarrativeMenu menu)
        {
            AccessTools.PropertySetter(typeof(CharacterCreationManager), "CurrentMenu")
                ?.Invoke(manager, new object[] { menu });

            AccessTools.Method(typeof(CharacterCreationManager), "ModifyMenuCharacters")
                ?.Invoke(manager, null);
        }

        /// <summary>
        ///     After vanilla refreshes menu characters, update body properties from the live
        ///     player character so appearance edits (face customization, Character Reload, etc.)
        ///     are reflected in narrative menu previews instead of showing a stale default.
        ///
        ///     A menu is registered with a seed character built from the face the
        ///     player had at registration time, and vanilla's own refresh ages
        ///     THAT body (<c>NarrativeMenuCharacter.ChangeAge</c> off the matching
        ///     argument) rather than a fresh one. Composing the cast on every
        ///     render replaces the seed and so answers this for every chapter that
        ///     composes successfully, but a chapter that fails to compose keeps the
        ///     seed and its stale face, which is what this still covers.
        ///
        ///     It re-ages what it replaces because replacing the body throws away
        ///     the age vanilla just applied to it, and it takes that age from the
        ///     same <see cref="CharacterPreviewHelper.PlayerPreviewAge"/> the
        ///     composed arguments used. Asking a second source would let this
        ///     overwrite the scene's own year with a different one, which is what
        ///     four hard-coded age buckets here did to every scene the script
        ///     wrote a distinct age for.
        /// </summary>
        [HarmonyPatch(typeof(CharacterCreationManager), "ModifyMenuCharacters")]
        [HarmonyPostfix]
        public static void ModifyMenuCharactersPostfix(CharacterCreationManager __instance)
        {
            try
            {
                var currentMenu = __instance.CurrentMenu;
                if (currentMenu == null || !currentMenu.StringId.StartsWith("cs_"))
                    return;

                var bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(null);
                var previewAge = CharacterPreviewHelper.PlayerPreviewAge(currentMenu.StringId);
                bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, previewAge);

                foreach (var character in currentMenu.Characters)
                    if (string.Equals(character.StringId, CharacterPreviewHelper.PlayerCharacterId,
                            StringComparison.Ordinal))
                    {
                        character.UpdateBodyProperties(
                            bodyProperties,
                            CharacterObject.PlayerCharacter.Race,
                            CharacterObject.PlayerCharacter.IsFemale);
                        break;
                    }

                // On the weapon menus, put the previewed weapon in hand
                CharacterPreviewHelper.ApplyWeaponInHand(currentMenu);
            }
            catch (Exception ex)
            {
                CSLogger.Error("Error updating player body properties in menu.", ex);
            }
        }
    }
}
