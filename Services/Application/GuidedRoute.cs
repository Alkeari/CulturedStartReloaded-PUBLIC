using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>
    ///     Which route tells a life through scenes, and the scenes it tells.
    ///
    ///     Only Cultured Start Revamped does. Cultured Start asks its seven chapters
    ///     through <c>CharacterCreation/CulturedStart</c>, which grants what each
    ///     answer names through the game's own narrative args and the steps that read
    ///     <see cref="LifePathCatalog"/>, so nothing that reads a scene answer may
    ///     treat that route as having told one. Everything that used to name
    ///     <c>SceneCatalog.All</c> asks here, and every reader that is about a told
    ///     life asks <see cref="IsGuided"/> first.
    ///
    ///     A route that tells no scenes answers with the Revamped set rather than with
    ///     nothing. Nothing downstream reads a set without first asking whether a
    ///     guided run was walked at all, and a null here would make every one of
    ///     those readers ask the same question twice.
    /// </summary>
    public static class GuidedRoute
    {
        /// <summary>The scenes this run puts, for the live session.</summary>
        public static IReadOnlyList<Scene> Scenes => SceneCatalog.All;

        /// <summary>The same question asked of one session rather than the live one.</summary>
        public static IReadOnlyList<Scene> ScenesOf(CharacterCreationSession? session) => SceneCatalog.All;

        /// <summary>Every scene there is, for whatever has to be registered once.</summary>
        public static IReadOnlyList<Scene> EveryScene => SceneCatalog.All;

        /// <summary>Whether this route tells a life through scenes at all.</summary>
        public static bool IsGuided(SetupMode mode) => mode == SetupMode.Narrative;
    }
}
