using System;
using System.Collections.Generic;
using CulturedStartReloaded.Services;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>Hosts the perk board as a modal Gauntlet layer above the Start Editor.</summary>
    public static class PerkPickerScreen
    {
        private const string MovieName = "CSRPerkPicker";
        private const int LayerOrder = 4700;

        /// <summary>
        ///     The perk icons live in the character screen's sprite sheets, which the game loads only
        ///     while that screen is open. War Sails keeps its perks' icons in a sheet of its own.
        /// </summary>
        private static readonly string[] IconCategories = { "ui_characterdeveloper", "ui_naval_character_developer" };

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static PerkPickerVM? _viewModel;
        private static ScreenBase? _host;
        private static readonly List<string> LoadedCategories = new();

        public static bool IsOpen => _layer != null;

        public static void Open(IReadOnlyList<PerkSkillEntry> skills, string? initialSkillId,
            IReadOnlyDictionary<string, List<string>> saved, bool allowBoth,
            Action<IReadOnlyDictionary<string, List<string>?>> onConfirm)
        {
            if (_layer != null) Close();

            try
            {
                var host = ScreenManager.TopScreen;
                if (host == null) return;

                LoadIcons();
                _viewModel = new PerkPickerVM(skills, initialSkillId, saved, allowBoth, onConfirm, Close);
                _layer = new GauntletLayer(nameof(PerkPickerScreen), LayerOrder);
                _movie = _layer.LoadMovie(MovieName, _viewModel);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _host = host;
            }
            catch (Exception ex)
            {
                CSLogger.Error("PerkPickerScreen: failed to open.", ex);
                Close();
            }
        }

        /// <inheritdoc cref="ItemPickerScreen.TickEscape"/>
        public static bool TickEscape()
        {
            try
            {
                if (_layer == null) return false;
                if (_layer.Input.IsKeyReleased(InputKey.Escape)) Close();
                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("PerkPickerScreen: escape handling failed.", ex);
                return true;
            }
        }

        public static void Close()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _layer.IsFocusLayer = false;
                    // Removing a layer never clears the screen manager's focus
                    ScreenManager.TryLoseFocus(_layer);

                    if (_movie != null)
                        _layer.ReleaseMovie(_movie);

                    _host?.RemoveLayer(_layer);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("PerkPickerScreen: close failed.", ex);
            }
            finally
            {
                _viewModel?.OnFinalize();
                _viewModel = null;
                _movie = null;
                _layer = null;
                _host = null;
                UnloadIcons();
            }
        }

        /// <summary>Loads each icon sheet not already loaded, remembering which, so closing unloads only those.</summary>
        private static void LoadIcons()
        {
            foreach (var name in IconCategories)
            {
                try
                {
                    if (!UIResourceManager.SpriteData.SpriteCategories.TryGetValue(name, out var category) ||
                        category.IsLoaded)
                        continue;

                    VersionedGameApi.LoadSpriteCategory(name);
                    LoadedCategories.Add(name);
                }
                catch (Exception ex)
                {
                    CSLogger.Warn($"PerkPickerScreen: perk icons in '{name}' could not load: {ex.Message}");
                }
            }
        }

        private static void UnloadIcons()
        {
            foreach (var name in LoadedCategories)
            {
                try
                {
                    if (UIResourceManager.SpriteData.SpriteCategories.TryGetValue(name, out var category))
                        category.Unload();
                }
                catch (Exception ex)
                {
                    CSLogger.Warn($"PerkPickerScreen: perk icons in '{name}' could not unload: {ex.Message}");
                }
            }

            LoadedCategories.Clear();
        }
    }
}
