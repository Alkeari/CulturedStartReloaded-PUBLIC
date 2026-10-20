using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Library;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Cultured Start's own menus, as the route shipped them in v3.28.2, standing
    ///     beside Cultured Start Revamped's on one stage.
    ///
    ///     Most chapters below the life carry the same menu id on both routes, and
    ///     the game resolves a menu by id with the first match in its list
    ///     (<c>CharacterCreationManager.GetNarrativeMenuWithId</c>). So only one
    ///     menu per id is ever in that list: the route the session is walking puts
    ///     its own in place, and the other route's waits here until it is chosen.
    ///     The flow table keeps one row per id, and which menu answers that row is
    ///     decided by the route alone.
    ///
    ///     The game also replays the narrative args of every option it holds as
    ///     selected, for every menu, whether or not the finished route walked it
    ///     (<c>CharacterCreationManager.ApplyFinalEffects</c>). This route's chapters
    ///     are the only menus that declare args, so their answers are dropped from
    ///     that replay whenever the session ends on another route.
    /// </summary>
    public static class CulturedStartMenus
    {
        private static readonly Dictionary<string, NarrativeMenu> Ours = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, NarrativeMenu> Theirs = new(StringComparer.Ordinal);
        private static readonly HashSet<NarrativeMenu> Chapters = new();

        private static readonly HashSet<string> ChapterIds = new(StringComparer.Ordinal)
        {
            LifePathCatalog.FamilyMenuId, LifePathCatalog.ChildhoodMenuId, LifePathCatalog.EducationMenuId,
            LifePathCatalog.YouthMenuId, LifePathCatalog.TurningMenuId, LifePathCatalog.ReasonMenuId,
            LifePathCatalog.AgeMenuId
        };

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            MeansMenu.SetPreviewService(service);
            GearSelectionMenus.SetPreviewService(service);
            ArmsMenu.SetPreviewService(service);
            GearCustomizationMenu.SetPreviewService(service);
        }

        /// <summary>
        ///     Builds every menu of the route after the other routes have registered
        ///     theirs, and takes back out of the stage's list each one whose id is
        ///     already there.
        /// </summary>
        public static void Register(CharacterCreationManager manager)
        {
            Ours.Clear();
            Theirs.Clear();
            Chapters.Clear();

            var list = MenuList(manager);
            if (list == null) return;

            int before = list.Count;
            var registered = new Dictionary<string, NarrativeMenu>(StringComparer.Ordinal);
            foreach (var menu in list)
                if (!registered.ContainsKey(menu.StringId))
                    registered[menu.StringId] = menu;

            LifePathMenus.AddLifePathMenus(manager);
            ScenarioSelectMenu.AddScenarioMenu(manager);
            MeansMenu.AddMeansMenu(manager);
            CompanionSelectMenu.AddCompanionMenu(manager);
            HouseholdMenu.AddHouseholdMenu(manager);
            WarbandMenu.AddWarbandMenu(manager);
            ContextualMenus.AddContextualMenus(manager);
            FoundingMenu.AddFoundingMenu(manager);
            StartLocationMenu.AddStartLocationMenu(manager);
            ScenarioChapterMenus.AddChapterMenus(manager);
            GearCustomizationMenu.AddGearCustomizationMenu(manager);
            ProvisionsMenu.AddProvisionsMenu(manager);
            StatCustomizationMenu.AddStatCustomizationMenu(manager);
            ArmsMenu.AddArmsMenu(manager);
            GearSelectionMenus.AddGearMenus(manager);
            EpilogueMenu.AddEpilogueMenu(manager);

            for (int index = list.Count - 1; index >= before; index--)
            {
                var menu = list[index];
                if (ChapterIds.Contains(menu.StringId)) Chapters.Add(menu);

                if (!registered.TryGetValue(menu.StringId, out var other)) continue;

                Ours[menu.StringId] = menu;
                Theirs[menu.StringId] = other;
                list.RemoveAt(index);
            }

            CSLogger.Info($"CulturedStartMenus: {list.Count - before} menus of its own, " +
                          $"{Ours.Count} sharing an id with Cultured Start Revamped.");
        }

        /// <summary>
        ///     Puts the given route's menus in the stage's list wherever the two routes
        ///     share an id. Each menu keeps its index, so the stage's order is untouched.
        /// </summary>
        public static void Activate(CharacterCreationManager manager, SetupMode mode)
        {
            try
            {
                var list = MenuList(manager);
                if (list == null || Ours.Count == 0) return;

                var wanted = mode == SetupMode.LifePath ? Ours : Theirs;
                int swapped = 0;
                for (int index = 0; index < list.Count; index++)
                {
                    if (!wanted.TryGetValue(list[index].StringId, out var menu)) continue;
                    if (ReferenceEquals(list[index], menu)) continue;

                    list[index] = menu;
                    swapped++;
                }

                if (swapped > 0)
                    CSLogger.Info($"CulturedStartMenus: {swapped} shared menus now answer for {mode}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartMenus: putting the route's menus in place failed.", ex);
            }
        }

        /// <summary>
        ///     Takes out of the game's final replay every selection the finished route
        ///     did not make: a menu no longer in the stage's list, and this route's
        ///     chapters when the session ended on another route.
        /// </summary>
        public static void DropAnswersOfOtherRoutes(CharacterCreationManager manager)
        {
            try
            {
                var list = MenuList(manager);
                if (list == null) return;

                var session = CreationSession.Current;
                var current = new HashSet<NarrativeMenu>(list);
                var dropped = manager.SelectedOptions.Keys
                    .Where(menu => !current.Contains(menu) ||
                                   (Chapters.Contains(menu) && session.Mode != SetupMode.LifePath))
                    .ToList();

                foreach (var menu in dropped)
                    manager.SelectedOptions.Remove(menu);

                if (dropped.Count > 0)
                    CSLogger.Info($"CulturedStartMenus: {dropped.Count} answers of a route not taken dropped " +
                                  $"({string.Join(", ", dropped.Select(m => m.StringId))}).");
            }
            catch (Exception ex)
            {
                CSLogger.Error("CulturedStartMenus: dropping the answers of other routes failed.", ex);
            }
        }

        private static MBList<NarrativeMenu>? MenuList(CharacterCreationManager manager) =>
            AccessTools.Field(typeof(CharacterCreationManager), "_narrativeMenus")?.GetValue(manager)
                as MBList<NarrativeMenu>;
    }
}
