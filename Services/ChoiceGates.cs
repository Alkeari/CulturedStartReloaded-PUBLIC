using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     What earlier scenes have shut for later ones. A scene's answer can
    ///     carry a Gate consequence naming an option elsewhere, and the menu
    ///     builder asks here before offering it.
    ///
    ///     A gate only ever shuts. There is no opener, because every option is
    ///     offered until something in the life rules it out, so an opener would
    ///     have nothing to act on. If a scene ever needs to unlock rather than
    ///     lock, that is a different idea and gets its own name rather than a sign
    ///     bit on this one.
    /// </summary>
    public static class ChoiceGates
    {
        /// <summary>
        ///     Whether an option is offered, given everything already chosen. An
        ///     option nobody has spoken about is offered: gates are an exception
        ///     the catalog declares, never a permission it has to grant.
        /// </summary>
        public static bool Allows(string optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return true;

            foreach (var gate in GatesFor())
                if (string.Equals(gate.Target, optionId, StringComparison.Ordinal))
                    return false;

            return true;
        }

        /// <summary>
        ///     The sentence to show against an option a gate has shut, so a missing
        ///     option is explained rather than silently absent. Null when nothing
        ///     shut it.
        ///
        ///     The gate answered with is one this life actually set, so an edge is
        ///     free to give its own reason: nobody reads the sentence behind an
        ///     answer they did not give. A door shut twice reads the earlier of the
        ///     two reasons, which is the one that shut it, and the later answer
        ///     found it shut already.
        /// </summary>
        public static string? ClosedBecause(string optionId)
        {
            foreach (var gate in GatesFor())
                if (string.Equals(gate.Target, optionId, StringComparison.Ordinal))
                    return SceneCatalog.NoteFor(gate, LifeSoFar());

            return null;
        }

        /// <summary>
        ///     What the answers given so far have left the character holding, for
        ///     the notes whose reason turns on it. Read at the moment the note is
        ///     composed rather than kept, because the run records an answer as the
        ///     player lands on it and a walk back changes what the life holds.
        /// </summary>
        private static IReadOnlyList<ChoiceConsequence> LifeSoFar() =>
            CharacterCreation.Scenes.SceneReading.Consequences(
                Application.GuidedRoute.Scenes, CharacterCreation.Menus.SceneMenus.Answered);

        /// <summary>
        ///     The gates carried by the scenes answered so far, which live in the
        ///     scene menus' own store rather than on the session.
        /// </summary>
        private static IEnumerable<ChoiceConsequence> GatesFor()
        {
            var answered = CharacterCreation.Menus.SceneMenus.Answered;

            foreach (var scene in Application.GuidedRoute.Scenes)
            foreach (var option in scene.Options)
            {
                if (!answered.Chose(option.Id)) continue;

                foreach (var consequence in option.Consequences)
                    if (consequence.Kind == ConsequenceKind.Gate && consequence.Target != null)
                        yield return consequence;
            }
        }
    }
}
