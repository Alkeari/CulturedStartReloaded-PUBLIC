using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Puts scenes on the game's narrative stage, for both guided routes.
    ///
    ///     Two scene sets are written and every one of their menus is registered,
    ///     because the stage is built before the player has chosen a route. Which
    ///     set is being walked is the session's, asked of
    ///     <c>Services/Application/GuidedRoute</c>; the flow table is what keeps the
    ///     other route's menus out of this run, and one store of answers serves both
    ///     because a run walks one route.
    ///
    ///     This is the only file that knows both halves. `CharacterCreation/Scenes`
    ///     is deliberately free of engine types so it can be tested without a game,
    ///     and the menu API is entirely engine. Everything specific to one or the
    ///     other stays on its own side of this bridge.
    ///
    ///     Each option carries the three texts the route promises: the thematic
    ///     line on the button, the prose shown once it is landed on, and the exact
    ///     effect, which is not written here at all. That third one is derived from
    ///     the option's own consequences by `Services/ChoiceEffects` and drawn in
    ///     the panel beside the character, so it can never say one thing while the
    ///     option does another.
    /// </summary>
    public static class SceneMenus
    {
        private static readonly SceneAnswers Answers = new();

        /// <summary>
        ///     Each scene's description text, kept because it is the only surface
        ///     that can say why an option is missing. See <see cref="ClosedNotes"/>.
        /// </summary>
        private static readonly Dictionary<string, TextObject> Descriptions = new();

        /// <summary>What has been answered so far, for the flow and the pipeline.</summary>
        public static ISceneAnswers Answered => Answers;

        /// <summary>Forgets every answer, for a creation run that starts over.</summary>
        public static void Reset()
        {
            Answers.Clear();
            RefreshDescriptions();
            CharacterPreviewHelper.ForgetChoices();
            NarrativeStep.ForgetTheLifeSoFar();
        }

        /// <summary>
        ///     Whether this scene is put to this player at all. The flow asks per
        ///     walk rather than once, because an answer changes what appears after
        ///     it and a scene decided in advance would be the wrong scene.
        /// </summary>
        public static bool Appears(string sceneId)
        {
            // The route's own set, so a scene of the route this run is not on is
            // never in the flow even before the flow table's own predicate is asked
            foreach (var scene in GuidedRoute.Scenes)
                if (string.Equals(scene.Id, sceneId, StringComparison.Ordinal))
                    return scene.Appears(Answers);

            return false;
        }

        public static void AddSceneMenus(CharacterCreationManager manager)
        {
            foreach (var scene in GuidedRoute.EveryScene)
                manager.AddNewMenu(Build(scene));
        }

        private static NarrativeMenu Build(Scene scene)
        {
            var description = new TextObject("{=!}{PROMPT}{CLOSED}");
            description.SetTextVariable("PROMPT", Prompt(scene));
            description.SetTextVariable("CLOSED", string.Empty);
            Descriptions[scene.Id] = description;

            var menu = new NarrativeMenu(
                scene.Id,
                Flow.CreationFlow.DeclaredPrevious(scene.Id),
                Flow.CreationFlow.DeclaredNext(scene.Id),
                new TextObject(scene.Title),
                description,
                CharacterPreviewHelper.CreatePlayerCharacter(scene.Id),
                GetPlayerCharacterArgs
            );

            foreach (var option in scene.Options)
            {
                var captured = option;
                var carrier = scene;
                var sceneId = scene.Id;
                var prose = new TextObject("{=!}{PROSE}");
                prose.SetTextVariable("PROSE", Prose(carrier, captured));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    captured.Id,
                    new TextObject(captured.Title),
                    prose,
                    // Declaring effects here would grant them twice: the game
                    // replays every selected option's args once creation ends,
                    // on top of what the pipeline has already applied. The stat
                    // panel is filled from the life itself by
                    // Patches/GainedPropertiesPatch instead, and the empty line
                    // the game would draw from these is taken off the stage by
                    // StageCameraControls.SilenceEmptyEffectLines
                    args => { },
                    // The description is taken once, at registration, and the
                    // writing an answer shows depends on the life reading it, so
                    // it is pushed in from the condition, which is the one hook
                    // the menu API runs on every render
                    MenuText.Live(prose,
                        d => d.SetTextVariable("PROSE", Prose(carrier, captured)),
                        () => IsOffered(captured.Id)),
                    m => Record(sceneId, captured),
                    m => { }
                ));
            }

            return menu;
        }

        /// <summary>
        ///     The situation this life meets, and the writing this life reads under
        ///     one answer.
        ///
        ///     Both are read against the answers already given with the scene itself
        ///     held out. The stage records an answer the moment the selection lands
        ///     on an option, so the run holds this scene's own answer by the time
        ///     either is composed, and a walk back leaves an earlier answer to it in
        ///     as well; a variant that read either would be describing a life the
        ///     player has not chosen.
        /// </summary>
        private static string Prompt(Scene scene)
        {
            try
            {
                return new TextObject(scene.PromptFor(scene.LifeBefore(Answers))).ToString();
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SceneMenus: choosing the writing of {scene.Id} failed.", ex);
                return new TextObject(scene.Prompt).ToString();
            }
        }

        private static string Prose(Scene scene, SceneOption option)
        {
            try
            {
                return new TextObject(option.ProseFor(scene.LifeBefore(Answers))).ToString();
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SceneMenus: choosing the writing of {option.Id} failed.", ex);
                return new TextObject(option.Prose).ToString();
            }
        }

        /// <summary>
        ///     Whether this option is still on the table. An earlier answer can
        ///     shut one, and the catalog names the option it shuts rather than the
        ///     scene, so the question is asked per option and per render: which
        ///     gates are standing changes as the life is told.
        /// </summary>
        private static bool IsOffered(string optionId)
        {
            try
            {
                return ChoiceGates.Allows(optionId);
            }
            catch (Exception ex)
            {
                // A condition runs on every render, so this cannot throw. An
                // unreadable gate offers the option: a life with one choice too
                // many is playable, a scene with no answers is not
                CSLogger.Error($"SceneMenus: reading the gates for {optionId} failed.", ex);
                return true;
            }
        }

        /// <summary>
        ///     Writes each scene's own text: the situation as this life meets it,
        ///     and the reasons the shut options are shut.
        ///
        ///     A list row renders only its title and the option's own description
        ///     binds to the selected option, so an option that is not offered has
        ///     no surface of its own to explain itself with: the scene's text is
        ///     the only place left. Both halves are written when an answer is
        ///     recorded rather than while the scene renders, because the answer
        ///     that shuts an option or that makes a prompt untrue is always given
        ///     in an earlier scene, and the stage reads a menu's description when
        ///     it puts that menu up.
        /// </summary>
        private static void RefreshDescriptions()
        {
            try
            {
                foreach (var scene in GuidedRoute.EveryScene)
                {
                    if (!Descriptions.TryGetValue(scene.Id, out var description)) continue;

                    description.SetTextVariable("PROMPT", Prompt(scene));
                    description.SetTextVariable("CLOSED", ClosedNotes(scene));
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("SceneMenus: writing the scene descriptions failed.", ex);
            }
        }

        private static string ClosedNotes(Scene scene)
        {
            var notes = new List<string>();
            foreach (var option in scene.Options)
            {
                // Null is the same answer as "nothing shut it", so one question
                // rather than two: the gates are walked from scratch per call
                string? note = ChoiceGates.ClosedBecause(option.Id);
                if (note == null) continue;

                notes.Add(new TextObject(note).ToString());
            }

            if (notes.Count == 0) return string.Empty;

            var header = new TextObject(
                "{=CSR_Scene_ClosedHeader}What this moment might have offered you, and no longer does:");

            return "\n\n" + header.ToString() + "\n" + string.Join("\n", notes);
        }

        /// <summary>
        ///     Records the answer. Answering the same scene twice replaces the
        ///     first answer rather than stacking on it, because a player who backs
        ///     up and changes their mind has changed their mind.
        ///
        ///     This runs the moment the player lands on an option, not when they
        ///     commit: the engine calls an option's select handler and then tells
        ///     the stage view to re-spawn its 3D visuals, in that order. So the
        ///     answer is written, the run's age is read back out of it, the sheet
        ///     is brought up to the life it now tells, and the character is
        ///     restaged, all before the frame that draws them. Moving the selection
        ///     along the list therefore walks the character through the lives each
        ///     answer would have given them.
        ///
        ///     The stage's own left-hand panel needs no nudge from here. It caches
        ///     what it reads off the hero, but the engine refreshes it at the end
        ///     of the very click that ran this: `OnOptionSelected` calls the
        ///     option's select handler first and `GainedPropertiesController.
        ///     UpdateValues()` last, so anything written to the character here is
        ///     on screen before the player sees the option land.
        /// </summary>
        private static void Record(string sceneId, SceneOption option)
        {
            try
            {
                Answers.Replace(sceneId, option.Id);
                CSLogger.Info($"SceneMenus: {sceneId} answered with {option.Id}.");
                ReadTheLifeBack();
                NarrativeStep.ShowTheLifeSoFar();
                RefreshDescriptions();
                CharacterPreviewHelper.ChoiceSelected(sceneId, option.Id, option.Consequences);
            }
            catch (Exception ex)
            {
                CSLogger.Error($"SceneMenus: recording the answer to {sceneId} failed.", ex);
            }
        }

        /// <summary>
        ///     The life as the answers read right now.
        ///
        ///     One account, asked for rather than rebuilt: the station screen shows
        ///     this reading and the session is written from it, so the person the
        ///     player is told about and the person the pipeline builds cannot
        ///     disagree.
        /// </summary>
        public static Portrait Reading()
        {
            var walked = GuidedRoute.Scenes;

            // Measured against the route's own ordinary life. The two sets are
            // different lengths and are made of different consequences, so a life
            // read against the other route's reference is a life nobody on this
            // route could have lived
            return Portrait.From(
                SceneReading.Consequences(walked, Answers),
                SceneReading.Answered(Answers),
                walked);
        }

        /// <summary>
        ///     Writes the run's age and station back out of the life it has told so
        ///     far, for the route that asks neither.
        ///
        ///     Cultured Start Revamped asks neither. Nothing would set the age, so
        ///     every character would start at twenty whatever they had lived
        ///     through, and nothing would set the station, so every character would
        ///     start a commoner however they had risen. Both come off the same
        ///     portrait, because two readings of one life are two lives.
        ///
        ///     Cultured Start asks both outright, as it always did: its age chapter
        ///     declares an age and <c>cs_scenario_select</c> declares a station, so
        ///     neither is read into here. Doing it anyway is what took those two
        ///     answers away from that route without anything on screen saying so:
        ///     whatever the age chapter wrote, the next answer overwrote from the
        ///     reading, and the station screen was replaced by one that told the
        ///     player what their life had come to.
        ///
        ///     Written on every answer rather than once at the end, for two
        ///     reasons. The stage shows the character at this age while the scenes
        ///     are still running, so a character who spent years somewhere should
        ///     look it by the time the scene after says so. And the menus below the
        ///     scenes branch on the station, while the game's own Back button lets
        ///     the player walk back into a scene and answer it differently: a
        ///     station written once would be the station of a life the player has
        ///     since changed, and the chapters would be the wrong chapters.
        ///
        ///     An age the player set for themselves in the editor is left alone:
        ///     the session keeps that separately and it wins.
        /// </summary>
        private static void ReadTheLifeBack()
        {
            try
            {
                var session = CreationSession.Current;
                if (session == null) return;

                // An age an answer stated outright is the age. Only Cultured Start's
                // age chapter states one, and on that route it is the whole account
                var declared = DeclaredAge();
                var portrait = Reading();

                session.SelectedAge = declared ?? portrait.Age;

                // The station is the player's own on Cultured Start, taken at
                // cs_scenario_select, so no answer of that route reads into it
                if (session.Mode == SetupMode.LifePath) return;

                if (session.SelectedStartType == portrait.Station) return;

                CSLogger.Info(
                    $"SceneMenus: the life now reads as {portrait.Station} rather than {session.SelectedStartType}.");

                session.SelectedStartType = portrait.Station;

                // The chapters behind the old station are abandoned with it, the
                // same way changing the station by hand once abandoned them:
                // without this a life that turned away from a crown still names
                // the realm it was going to found
                session.ResetScenarioState();
            }
            catch (Exception ex)
            {
                CSLogger.Error("SceneMenus: reading the run's age and station back failed.", ex);
            }
        }

        /// <summary>
        ///     The age this run has been told outright, or nothing where no answer
        ///     said one. The last such answer wins, so a player who walks back to
        ///     the age chapter and answers it again gets the age they just chose.
        /// </summary>
        private static StartingAge? DeclaredAge()
        {
            StartingAge? said = null;

            foreach (var consequence in SceneReading.Consequences(GuidedRoute.Scenes, Answers))
                if (consequence != null && consequence.Kind == ConsequenceKind.Age)
                    said = (StartingAge)consequence.Amount;

            return said;
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
