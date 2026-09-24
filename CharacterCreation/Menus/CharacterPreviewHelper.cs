using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     What stands on the narrative stage for a chapter, and what it is wearing.
    ///
    ///     The engine matches a <see cref="NarrativeMenuCharacter"/> to its
    ///     <see cref="NarrativeMenuCharacterArgs"/> BY ID and silently skips any
    ///     arg whose id is not in the menu's character list
    ///     (<c>CharacterCreationManager.ModifyMenuCharacters</c>). A character that
    ///     matches nothing keeps its constructor defaults: the
    ///     <c>player_char_creation_default</c> roster, <c>act_inventory_idle_start</c>
    ///     and its registration-time body. That is what "the same character in the
    ///     same equipment on every menu" was.
    ///
    ///     So the cast and the args are composed TOGETHER, on every render, by
    ///     <see cref="GetPlayerCharacterArgs"/>, which rewrites the menu's own
    ///     character list in place. The list a menu is registered with is only a
    ///     seed; nothing is decided at registration time, because at registration
    ///     time the player has no face, no family and no answers yet.
    /// </summary>
    public static class CharacterPreviewHelper
    {
        public const string PlayerCharacterId = "cs_player_character";

        /// <summary>
        ///     The age the generated parent bodies are BUILT at, which decides
        ///     nothing on screen.
        ///
        ///     The engine re-ages every staged body from the age its own argument
        ///     carries (<c>NarrativeMenuCharacter.ChangeAge</c>), and a parent's
        ///     argument carries the year of the scene they are standing in. This
        ///     is here because a <see cref="BodyProperties"/> has to be built with
        ///     some age, and these two are cached against the player's FACE rather
        ///     than against any one scene.
        /// </summary>
        private const int ParentBodyAge = 33;

        /// <summary>
        ///     Nobody younger than this stands on the stage. The game's childhood
        ///     equipment rosters and standing poses are written for a child on
        ///     their feet, and every mark in the scene is an upright one.
        /// </summary>
        private const int YoungestRendered = 5;

        private const string MotherCharacterId = "cs_mother_character";
        private const string FatherCharacterId = "cs_father_character";
        private const string HorseCharacterId = "cs_narrative_horse";

        /// <summary>
        ///     Spawn points that exist in <c>character_menu_new</c>, the scene the
        ///     narrative stage reads. A tag the scene does not carry drops the
        ///     character at the world origin and off camera, so nothing here is
        ///     invented.
        ///
        ///     The three human marks are NOT a row three people fit in. Measured
        ///     off the scene, they stand at x 6.057, 6.425 and 7.046 on one line:
        ///     0.37 m and 0.62 m from the middle one, which is inside a person.
        ///     Vanilla never uses more than two of them at once and the pair it
        ///     does use (the outer two, 0.99 m apart) is the only spacing the
        ///     scene is known to hold. Putting a third between them is what stood
        ///     the household inside the character.
        /// </summary>
        private const string CenterSpawn = "spawnpoint_player_1";
        private const string LeftSpawn = "spawnpoint_player_brother_stage";
        private const string RightSpawn = "spawnpoint_brother_brother_stage";
        private const string MountSpawn = "spawnpoint_mount_1";

        /// <summary>
        ///     How far apart the character stands from himself when a chapter
        ///     shows him in more than one outfit.
        ///
        ///     0.99 m, which is the gap between the outer two human marks and the
        ///     only spacing the scene is known to hold: vanilla stages two people
        ///     on exactly those marks. Three renders therefore stand across
        ///     1.98 m, twice the width vanilla ever asks the shot to cover, which
        ///     is what <see cref="Editor.StagePreviewControls.FitSpan"/> opens the
        ///     camera for.
        /// </summary>
        private const float OutfitSpacing = 0.99f;

        /// <summary>
        ///     How far behind the front rank everyone who is not the player
        ///     stands.
        ///
        ///     The horizontal band the scene is known to frame is narrow, so the
        ///     cast is separated in DEPTH instead of being crowded across it:
        ///     each mark keeps the x the scene gave it and steps back along its
        ///     own facing. At 0.9 m that leaves every pair of people at least
        ///     1.02 m apart while the widest of them sits at x 5.82, inside the
        ///     x 5.83 the scene's own npc mark occupies nearer the camera.
        /// </summary>
        private const float BackRankDepth = 0.9f;

        /// <summary>Where a scene's own text says someone hangs back, they do.</summary>
        private const float WellBackDepth = 1.4f;

        /// <summary>
        ///     A rank behind the back rank, for a cast larger than the scene has
        ///     marks for. Nothing stages that many people today.
        /// </summary>
        private const float DeepRankDepth = 1.9f;

        private const string PadTagPrefix = "csr_stage_stand_";

        /// <summary>One row of people, so the stage shows a household and not a crowd.</summary>
        private const int StageCompanionLimit = 2;

        private static EquipmentPreviewService? _previewService;

        /// <summary>
        ///     Whether the chapter on screen is standing the character up in every
        ///     outfit at once.
        ///
        ///     The Start Editor reads it before switching which single outfit the
        ///     stage shows: with all of them already standing there, swapping the
        ///     one on display would dress the middle render in the tab's clothes
        ///     and the player would be shown the same outfit twice.
        /// </summary>
        public static bool ShowsEveryOutfit { get; private set; }

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        /// <summary>
        ///     Forces the game to re-read the preview roster immediately.
        ///
        ///     Vanilla runs <c>ModifyMenuCharacters</c> only when the menu itself
        ///     changes, so anything decided inside a menu (an answer, the armory
        ///     picker, the Start Editor) would otherwise show up one click late.
        ///     The stage view re-spawns its visuals on its own the moment an option
        ///     is selected, so calling this from an option's select handler restages
        ///     the character before the frame that draws it.
        /// </summary>
        public static void RefreshMenuCharacters()
        {
            try
            {
                var manager = Session.CreationStage.Manager;
                if (manager == null) return;

                HarmonyLib.AccessTools.Method(typeof(CharacterCreationManager), "ModifyMenuCharacters")
                    ?.Invoke(manager, null);

                // ModifyMenuCharacters updates the DATA; the stage view rebuilds
                // its 3D visuals only when told the data changed
                Editor.StageViewBridge.MarkAgentVisualsDirty();
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: menu character refresh failed.", ex);
            }
        }

        /// <summary>
        ///     Shows the first weapon of the chosen loadout in the character's
        ///     hand, after vanilla has refreshed the menu characters.
        /// </summary>
        public static void ApplyWeaponInHand(NarrativeMenu menu)
        {
            if (_previewService == null) return;

            // The arms chapter holds the first weapon; each of Cultured Start's four
            // slot chapters holds the weapon of its own slot
            EquipmentIndex slot = menu.StringId switch
            {
                "cs_arms_menu" => EquipmentIndex.Weapon0,
                "cs_weapon1_menu" => EquipmentIndex.Weapon0,
                "cs_weapon2_menu" => EquipmentIndex.Weapon1,
                "cs_weapon3_menu" => EquipmentIndex.Weapon2,
                "cs_weapon4_menu" => EquipmentIndex.Weapon3,
                _ => EquipmentIndex.None
            };
            if (slot == EquipmentIndex.None) return;

            foreach (var character in menu.Characters)
                if (string.Equals(character.StringId, PlayerCharacterId, StringComparison.Ordinal))
                {
                    _previewService.ApplyWeaponToHand(character, slot);
                    break;
                }
        }

        public enum PreviewStage
        {
            Childhood,
            Education,
            Youth,
            Reason,
            Adult,
            AdultWithPreview,
            Weapon
        }

        /// <summary>Who stands on the stage for a chapter. Combinable, because a
        /// chapter about a house has the house AND the child in it.</summary>
        [Flags]
        public enum Cast
        {
            None = 0,
            Player = 1,
            Parents = 2,

            /// <summary>Whatever brothers and sisters the household holds, and
            /// nobody if it holds none: there, the list IS the answer.</summary>
            Siblings = 4,

            Mount = 8,

            /// <summary>
            ///     A brother or sister the scene's own text asserts. The household
            ///     is composed in a later chapter, so a scene written around a
            ///     sibling would otherwise stage the child alone and contradict the
            ///     paragraph beside them. The household's own, where it has one.
            /// </summary>
            Kin = 16
        }

        /// <summary>
        ///     A pose, as the pair of ids that mean it.
        ///
        ///     The stage picks its action set by sex
        ///     (<c>MBGlobals.GetActionSetWithSuffix(monster, isFemale, "_facegen")</c>),
        ///     and the two sets are not symmetric: <c>as_human_facegen</c> carries
        ///     103 actions including the whole <c>act_childhood_*</c> family, while
        ///     <c>as_human_female_facegen</c> carries 24 and none of them. A single
        ///     id is therefore a pose for one sex and nothing at all for the other,
        ///     which is why every pose below is declared as a pair and why no id is
        ///     used outside the set that defines it.
        /// </summary>
        private readonly struct Pose
        {
            public Pose(string male, string female)
            {
                Male = male;
                Female = female;
            }

            public string Male { get; }
            public string Female { get; }

            public string For(bool isFemale) => isFemale ? Female : Male;
        }

        private static readonly Pose Standing = new Pose(
            "act_character_creation_male_default_standing",
            "act_character_creation_female_default_standing");

        private static readonly Pose Idle = new Pose(
            "act_inventory_idle_start", "act_inventory_idle_start");

        private static readonly Pose Listening = new Pose(
            "act_childhood_schooled", "act_conversation_normal_loop");

        private static readonly Pose Guarded = new Pose(
            "act_childhood_tough", "act_conversation_closed_loop");

        private static readonly Pose Assured = new Pose(
            "act_childhood_sharp", "act_conversation_confident_loop");

        private static readonly Pose Ready = new Pose(
            "act_childhood_ready", "act_character_creation_female_default_side_to_side_1");

        private const string HorsePose = "act_horse_stand_1";

        /// <summary>
        ///     What a chapter shows: how old, who else is in the room, how they
        ///     stand and what they are dressed in. One table, read on every render,
        ///     so a chapter's staging is decided in exactly one place.
        /// </summary>
        private sealed class Scene
        {
            public Scene(PreviewStage stage, Cast cast, Pose pose,
                OutfitKind dress = OutfitKind.Civilian, bool householdSettledHere = false,
                OutfitKind[]? gearsets = null)
            {
                Stage = stage;
                Who = cast;
                Attitude = pose;
                Dress = dress;
                HouseholdSettledHere = householdSettledHere;
                Gearsets = gearsets ?? new[] { dress };
            }

            public PreviewStage Stage { get; }
            public Cast Who { get; }
            public Pose Attitude { get; }

            /// <summary>
            ///     Whether what an answer settles about the household is already
            ///     true at the moment this chapter shows.
            ///
            ///     A chapter asking what the house had when the child was small is
            ///     answered about that morning, so an answer burying the parents
            ///     buries them before the stage's own moment and they are not in
            ///     the room. A chapter judging a whole life out of a room the child
            ///     is ten in is not: the parent an answer there buries is still
            ///     standing in that argument, and taking them out of it would stage
            ///     a death the answer never put at that age.
            /// </summary>
            public bool HouseholdSettledHere { get; }

            /// <summary>
            ///     Armor where the chapter is about war or the road, town clothes
            ///     where it is about people. This is also what keeps the rendered
            ///     kit still: the battle preview is re-rolled by the chapters that
            ///     own gear, so only the chapters that ask about gear render it.
            /// </summary>
            public OutfitKind Dress { get; }

            /// <summary>
            ///     Every outfit the chapter's own answers move, one render of the
            ///     character each. A chapter whose choice changes what the
            ///     character is given across more than one gearset shows all of
            ///     them standing together, because the full scope of a grant is
            ///     not something the player should have to take on trust.
            /// </summary>
            public OutfitKind[] Gearsets { get; }

            public bool Shows(Cast who) => (Who & who) != 0;
        }

        /// <summary>
        ///     Every outfit a character carries, in the order they are read left
        ///     to right: the clothes they live in, the kit they fight in, and what
        ///     they wear when they do not want to be seen. A chapter that decides
        ///     all three shows all three, because the full scope of a grant is not
        ///     something the player should have to take on trust.
        /// </summary>
        private static readonly OutfitKind[] EveryOutfit =
            { OutfitKind.Civilian, OutfitKind.Battle, OutfitKind.Stealth };

        private static Scene SceneFor(string? menuId)
        {
            return menuId switch
            {
                // The scene route. Each scene's written stage direction names an
                // age, who else is in the room and what the character is dressed
                // in. Where a direction puts family in the room, the player is
                // still in it: a chapter about the people who raised you is not a
                // chapter you are absent from.

                // What the house had is the two people who kept it, and the answer
                // that says there was no house says they were already buried
                "cs_scene_what_the_house_had" =>
                    new Scene(PreviewStage.Childhood, Cast.Player | Cast.Parents, Listening,
                        OutfitKind.Civilian, householdSettledHere: true),

                // The argument is theirs and the child is at the edge of it
                "cs_scene_the_one_always_in_the_wrong" =>
                    new Scene(PreviewStage.Education, Cast.Player | Cast.Parents, Guarded),

                // The sibling who stood back while it burned is the other person there
                "cs_scene_the_store_burned" =>
                    new Scene(PreviewStage.Education, Cast.Player | Cast.Kin, Guarded),

                // What was said through the door was said by a parent
                "cs_scene_what_they_called_you_then" =>
                    new Scene(PreviewStage.Youth, Cast.Player | Cast.Parents, Listening),

                // Some of the men who took you on taught you from a saddle
                "cs_scene_the_man_who_taught_you" =>
                    new Scene(PreviewStage.Youth, Cast.Player | Cast.Mount, Ready, OutfitKind.Battle),

                // The one thing you could do is a thing some of them do mounted
                "cs_scene_what_you_could_do" =>
                    new Scene(PreviewStage.Reason, Cast.Player | Cast.Mount, Ready, OutfitKind.Battle),

                // The morning after a night attack, still where the fighting was
                "cs_scene_what_the_yard_said" =>
                    new Scene(PreviewStage.Reason, Cast.Player, Guarded, OutfitKind.Battle),

                // A square and a judgement: the scene is about people, not about war
                "cs_scene_the_verdict" => new Scene(PreviewStage.Adult, Cast.Player, Standing),

                // Years of standing out in the same town, in that town's clothes
                "cs_scene_what_the_town_said" => new Scene(PreviewStage.Adult, Cast.Player, Assured),

                // An offer across a table is answered in the best they own
                "cs_scene_the_seat" => new Scene(PreviewStage.Adult, Cast.Player, Standing),

                // The purse is the scene, so what it bought has to be on the
                // character and on the animal standing next to them
                "cs_scene_the_purse" => new Scene(PreviewStage.AdultWithPreview,
                    Cast.Player | Cast.Mount, Assured, OutfitKind.Battle),

                // Seasons spent indoors, with no road and nobody else in them
                "cs_scene_the_winter_between" => new Scene(PreviewStage.Adult, Cast.Player, Idle),

                // The last scene names the finished character
                "cs_scene_the_name_they_use" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing),

                // Cultured Start's seven chapters, each at the age its stage of a
                // life was always shown at
                "cs_family_menu" or "cs_childhood_menu" =>
                    new Scene(PreviewStage.Childhood, Cast.Player, Standing),
                "cs_education_menu" => new Scene(PreviewStage.Education, Cast.Player, Standing),
                "cs_youth_menu" => new Scene(PreviewStage.Youth, Cast.Player, Standing),
                "cs_turning_menu" or "cs_reason_menu" =>
                    new Scene(PreviewStage.Reason, Cast.Player, Standing),
                "cs_age_menu" => new Scene(PreviewStage.Adult, Cast.Player, Standing),

                // Cultured Start's slot chapters put the slot's weapon in hand
                "cs_weapon1_menu" or "cs_weapon2_menu" or "cs_weapon3_menu" or "cs_weapon4_menu" =>
                    new Scene(PreviewStage.Weapon, Cast.Player, Ready, OutfitKind.Battle),

                // Cultured Start asks the whole household in one chapter, so both
                // halves of the house stand in it
                "cs_household_menu" when CreationSession.Current?.Mode == SetupMode.LifePath =>
                    new Scene(PreviewStage.Adult, Cast.Player | Cast.Parents | Cast.Siblings, Standing),

                // Each household chapter shows the half of the house it asks about
                "cs_household_parents_menu" =>
                    new Scene(PreviewStage.Adult, Cast.Player | Cast.Parents, Standing),

                "cs_household_menu" =>
                    new Scene(PreviewStage.Adult, Cast.Player | Cast.Siblings, Standing),

                "cs_household_hearth_menu" =>
                    new Scene(PreviewStage.Adult, Cast.Player, Standing),

                // What moves with you: the animal belongs in shot. These chapters
                // are about coin and stores rather than about war, so they show
                // the clothes the character lives in
                "cs_means_menu" => new Scene(PreviewStage.Adult, Cast.Player | Cast.Mount, Standing),
                "cs_provisions_menu" =>
                    new Scene(PreviewStage.Adult, Cast.Player | Cast.Mount, Standing),

                // The chapters that own the war kit are the only ones that render it
                // The armor chapter's answer dresses the character for war AND
                // for town, so it stands them in both
                "cs_gear_menu" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Ready, OutfitKind.Battle,
                        gearsets: EveryOutfit),
                "cs_arms_menu" =>
                    new Scene(PreviewStage.Weapon, Cast.Player, Ready, OutfitKind.Battle),
                "cs_warband_menu" => new Scene(PreviewStage.AdultWithPreview,
                    Cast.Player | Cast.Mount, Standing, OutfitKind.Battle),
                "cs_first_war_menu" => new Scene(PreviewStage.AdultWithPreview,
                    Cast.Player, Ready, OutfitKind.Battle),
                "cs_beasts_menu" => new Scene(PreviewStage.AdultWithPreview,
                    Cast.Player | Cast.Mount, Standing, OutfitKind.Battle),

                // A chapter about what you did in the dark shows what you did it in
                "cs_crime_menu" =>
                    new Scene(PreviewStage.Adult, Cast.Player, Guarded, OutfitKind.Stealth),

                // The Start Editor is opened over this menu and edits all three
                // outfits in its own tabs, so all three stand on the stage behind
                // it and a change to any of them is seen where it was made
                "cs_custom_menu" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing,
                        OutfitKind.Battle, gearsets: EveryOutfit),

                // The screen that tells the player who their answers made, so it
                // shows exactly that person: alone, because the verdict is about
                // nobody else; at the age the life came to; in the clothes the run
                // has generated for them rather than a culture's default kit
                "cs_standing_menu" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing),

                // The same person at whatever age the player moves them to
                "cs_age_adjust_menu" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing),

                // The same person, on the screen where Cultured Start asks the
                // question its own way rather than answering it
                "cs_scenario_select" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing),
                "cs_epilogue_menu" =>
                    new Scene(PreviewStage.AdultWithPreview, Cast.Player, Standing),

                _ => new Scene(PreviewStage.Adult, Cast.Player, Standing)
            };
        }

        /// <summary>
        ///     The list a menu is REGISTERED with. It is a seed and nothing more:
        ///     at registration the player has no face, no family and no answers, so
        ///     anything decided here would be wrong by the time it was drawn. The
        ///     real cast is composed on every render by
        ///     <see cref="GetPlayerCharacterArgs"/>, which rewrites this list.
        /// </summary>
        public static List<NarrativeMenuCharacter> CreatePlayerCharacter(string? menuId = null)
        {
            var characters = new List<NarrativeMenuCharacter>();
            try
            {
                characters.Add(NewPlayerCharacter());
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: Failed to create player character.", ex);
            }
            return characters;
        }

        private static NarrativeMenuCharacter NewPlayerCharacter(string id = PlayerCharacterId)
        {
            var player = CharacterObject.PlayerCharacter;
            return new NarrativeMenuCharacter(
                id, player.GetBodyProperties(null), player.Race, player.IsFemale);
        }

        /// <summary>
        ///     The chapter's cast and its staging, composed together so an id can
        ///     never go unmatched, and written back into the menu's own character
        ///     list before the args are returned.
        ///
        ///     <c>ModifyMenuCharacters</c> takes the list by reference and then
        ///     walks the args, so replacing the list's CONTENTS here is seen by the
        ///     very loop that consumes what this returns. Replacing the list object
        ///     would not be: the field is readonly.
        /// </summary>
        public static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            var args = new List<NarrativeMenuCharacterArgs>();
            try
            {
                var menu = manager?.CurrentMenu;
                var menuId = menu?.StringId;
                var scene = SceneFor(menuId);

                var cast = new List<NarrativeMenuCharacter>();
                Compose(scene, menuId, culture, cast, args);

                var live = menu?.Characters;
                if (live != null && cast.Count > 0)
                {
                    live.Clear();
                    live.AddRange(cast);
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: Failed to get player character args.", ex);
            }
            return args;
        }

        private static void Compose(Scene scene, string? menuId, CultureObject? culture,
            List<NarrativeMenuCharacter> cast, List<NarrativeMenuCharacterArgs> args)
        {
            BeginStaging();

            var player = CharacterObject.PlayerCharacter;
            bool playerIsFemale = player.IsFemale;

            // The year the scene happens in, which everybody standing in it is
            // aged against: a house around a child of seven is not the house
            // around the same character at thirty-six
            int sceneAge = PlayerPreviewAge(menuId);

            string playerRoster = GetEquipmentRosterId(culture, scene.Stage, scene.Dress);
            var chosen = ChoiceFor(menuId);

            // The character holds the front rank and everyone else steps back
            // behind them, so a house reads as a house around the person it is
            // about without anybody standing in anybody. A chapter showing the
            // character in more than one outfit spends the front rank on those
            // instead, abreast on either side of the middle mark, in the order
            // the chapter declares them: clothes on the left, war kit in the
            // middle, what they wear unseen on the right.
            bool manyOutfits = scene.Gearsets.Length > 1;
            ShowsEveryOutfit = manyOutfits;
            var front = new Queue<Stand>(manyOutfits
                ? Abreast(scene.Gearsets.Length)
                : new[] { new Stand(CenterSpawn, 0f) });

            // Three abreast fill the whole of the scene's front, so anyone else
            // steps back past all of them rather than into the middle one
            var behind = new Queue<Stand>(manyOutfits
                ? new[] { new Stand(CenterSpawn, DeepRankDepth) }
                : new[]
                {
                    new Stand(LeftSpawn, BackRankDepth), new Stand(RightSpawn, BackRankDepth)
                });

            // The shot has to open wide enough to hold whatever was just staged,
            // before the player touches anything
            if (!manyOutfits)
                Editor.StagePreviewControls.FitSpan(0f);
            else if (string.Equals(menuId, "cs_custom_menu", StringComparison.Ordinal))
                Editor.StagePreviewControls.FitSpan((scene.Gearsets.Length - 1) * OutfitSpacing,
                    panPresses: -2, rotatePresses: 1, zoomPresses: -1);
            else
                Editor.StagePreviewControls.FitSpan((scene.Gearsets.Length - 1) * OutfitSpacing,
                    panPresses: -1, rotatePresses: 1, zoomPresses: -1);

            // Two living parents behind a character whose answer has just told the
            // player both of them are in the ground is the panel and the stage
            // saying opposite things about the same house
            bool buried = scene.HouseholdSettledHere && chosen?.ParentsBuried == true;

            if (scene.Shows(Cast.Parents) && !buried)
                AddParents(cast, args, culture, behind, sceneAge);

            if (scene.Shows(Cast.Siblings | Cast.Kin))
                AddSiblings(cast, args, culture, behind, scene.Shows(Cast.Kin), sceneAge);

            if (scene.Shows(Cast.Player))
            {
                var pose = chosen?.HandItem != null ? Ready : scene.Attitude;

                // A weapon an answer put in the hand belongs on the render that is
                // dressed to carry one. It used to go to whichever outfit the
                // chapter happened to declare first, which with the row read left
                // to right is the town clothes
                int armed = Array.IndexOf(scene.Gearsets, OutfitKind.Battle);
                if (armed < 0) armed = 0;

                for (int outfit = 0; outfit < scene.Gearsets.Length; outfit++)
                {
                    string id = outfit == 0 ? PlayerCharacterId : $"{PlayerCharacterId}_{outfit}";

                    cast.Add(NewPlayerCharacter(id));
                    args.Add(new NarrativeMenuCharacterArgs(
                        id,
                        sceneAge,
                        GetEquipmentRosterId(culture, scene.Stage, scene.Gearsets[outfit]),
                        pose.For(playerIsFemale),
                        Resolve(front.Count > 0 ? front.Dequeue() : Next(behind)),
                        string.Empty,
                        outfit == armed ? chosen?.HandItem ?? string.Empty : string.Empty,
                        null,
                        true,
                        playerIsFemale));
                }
            }

            bool wantsMount = scene.Shows(Cast.Mount) || chosen?.WantsMount == true;
            if (wantsMount)
                AddMount(cast, args, culture, playerRoster);

            ReportStaging(menuId, args);
        }

        /// <summary>
        ///     Where one of the cast stands: a mark of the scene's, how far behind
        ///     it, and how far to one side of it. A positive side is the
        ///     viewer's right.
        /// </summary>
        private readonly struct Stand
        {
            public Stand(string mark, float back, float side = 0f)
            {
                Mark = mark;
                Back = back;
                Side = side;
            }

            public string Mark { get; }
            public float Back { get; }
            public float Side { get; }

            public bool IsTheMarkItself => Back <= 0f && Side == 0f;
        }

        /// <summary>
        ///     A row of places across the middle mark, evenly spaced and centered
        ///     on it, read left to right.
        ///
        ///     The scene's own three human marks are not a row three people fit
        ///     in: they sit 0.37 m and 0.62 m from the middle one, which is inside
        ///     a person. So the row is built out of the mod's own marks at the one
        ///     spacing the scene is known to hold.
        /// </summary>
        private static Stand[] Abreast(int count)
        {
            var row = new Stand[count];
            float first = -(count - 1) * OutfitSpacing / 2f;
            for (int index = 0; index < count; index++)
                row[index] = new Stand(CenterSpawn, 0f, first + index * OutfitSpacing);

            return row;
        }

        /// <summary>
        ///     The next place in the rank, and a deeper one once the rank is
        ///     full, so a cast larger than the scene has marks for still spreads
        ///     instead of piling onto the last mark taken.
        /// </summary>
        private static Stand Next(Queue<Stand> places)
            => places.Count > 0 ? places.Dequeue() : new Stand(RightSpawn, DeepRankDepth);

        private static TaleWorlds.Engine.Scene? _padScene;
        private static readonly List<TaleWorlds.Engine.GameEntity?> _pads =
            new List<TaleWorlds.Engine.GameEntity?>();
        private static int _padsUsed;
        private static bool _padsUnreachable;

        private static void BeginStaging() => _padsUsed = 0;

        /// <summary>The tag the engine should look up for this place.</summary>
        private static string Resolve(Stand stand)
            => stand.IsTheMarkItself
                ? stand.Mark
                : PadNear(stand.Mark, stand.Back, stand.Side) ?? stand.Mark;

        /// <summary>
        ///     A mark of the mod's own, the given distance behind and to one side
        ///     of one of the scene's, facing the way that one faces.
        ///
        ///     The stage resolves a spawn point by TAG out of the live scene
        ///     (<c>Scene.FindEntityWithTag</c>) and drops anyone whose tag it
        ///     cannot find at the world origin, off camera. So a tag is handed
        ///     back only after the scene has confirmed it holds one, and a
        ///     failure anywhere here returns the scene's own mark: the chapter
        ///     then stages exactly as it did before rather than emptying.
        ///
        ///     The marks are the mod's own empty entities rather than the
        ///     scene's spare ones because the Vanilla Start route walks this
        ///     same scene and has to find it as the game left it.
        /// </summary>
        private static string? PadNear(string mark, float back, float side)
        {
            try
            {
                var scene = Editor.StageViewBridge.GetStageScene();
                if (scene == null) return null;

                if (!ReferenceEquals(scene, _padScene))
                {
                    _pads.Clear();
                    _padScene = scene;
                }

                var anchor = scene.FindEntityWithTag(mark);
                if (anchor == null) return null;

                int index = _padsUsed++;
                string tag = PadTagPrefix + index;

                while (_pads.Count <= index) _pads.Add(null);
                if (_pads[index] == null)
                {
                    var created = Services.VersionedGameApi.EmptyEntity(scene);
                    created.AddTag(tag);
                    _pads[index] = created;
                }

                // The marks all face the camera, so their own forward is the way
                // out of the shot and stepping back is a step along its negative.
                // An actor facing the camera has their own right hand on the
                // viewer's left, so a step to the viewer's right is a step along
                // the mark's negative side axis.
                var frame = anchor.GetGlobalFrame();
                frame.origin -= frame.rotation.f * back;
                frame.origin -= frame.rotation.s * side;
                _pads[index]!.SetGlobalFrame(frame);

                if (scene.FindEntityWithTag(tag) != null) return tag;

                if (!_padsUnreachable)
                {
                    _padsUnreachable = true;
                    CSLogger.Warn("CharacterPreviewHelper: the stage would not take the mod's own " +
                                  "standing marks, so the cast keeps the scene's three.");
                }
                return null;
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: placing the standing marks failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Writes out who the chapter just staged, where, and how old, because
        ///     whether the cast stands apart and whether each of them is the age
        ///     their scene was written for is settled by the arguments, and those
        ///     are readable in a log while the picture is not.
        /// </summary>
        private static void ReportStaging(string? menuId, List<NarrativeMenuCharacterArgs> args)
        {
            try
            {
                var row = string.Join(", ", args.Select(a => a.IsHuman
                    ? $"{a.CharacterId}@{a.SpawnPointEntityId} aged {a.Age}"
                    : $"{a.CharacterId}@{a.SpawnPointEntityId}"));
                CSLogger.Debug($"Stage {menuId ?? "(none)"}: {row}");
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: reporting the staging failed.", ex);
            }
        }

        /// <summary>
        ///     Mother and father, generated once per face and then reused, so every
        ///     chapter that puts them on stage puts the SAME two people there. They
        ///     were regenerated per menu before, which is why the parents in one
        ///     scene did not match the parents in the next.
        /// </summary>
        private static void AddParents(List<NarrativeMenuCharacter> cast,
            List<NarrativeMenuCharacterArgs> args, CultureObject? culture, Queue<Stand> places,
            int sceneAge)
        {
            try
            {
                var player = CharacterObject.PlayerCharacter;
                var cultureId = culture?.StringId ?? "empire";
                string occupation = ParentOccupation(HowTheyWereRaised(), cultureId);

                string? motherRoster = FindExistingRosterId(new[]
                {
                    $"mother_char_creation_{occupation}_{cultureId}",
                    $"mother_char_creation_none_{cultureId}",
                    "mother_char_creation_none_empire"
                });
                string? fatherRoster = FindExistingRosterId(new[]
                {
                    $"father_char_creation_{occupation}_{cultureId}",
                    $"father_char_creation_none_{cultureId}",
                    "father_char_creation_none_empire"
                });
                if (motherRoster == null || fatherRoster == null)
                {
                    CSLogger.Warn("CharacterPreviewHelper: no parent rosters for this culture, " +
                                  "so the chapter stages the character alone.");
                    return;
                }

                var (mother, father) = ParentBodies();

                cast.Add(new NarrativeMenuCharacter(
                    MotherCharacterId, mother, player.Race, isFemale: true));
                args.Add(new NarrativeMenuCharacterArgs(MotherCharacterId,
                    ParentAgeAt(sceneAge, isFather: false),
                    motherRoster, Standing.For(true), Resolve(Next(places)),
                    string.Empty, string.Empty, null, true, true));

                cast.Add(new NarrativeMenuCharacter(
                    FatherCharacterId, father, player.Race, isFemale: false));
                args.Add(new NarrativeMenuCharacterArgs(FatherCharacterId,
                    ParentAgeAt(sceneAge, isFather: true),
                    fatherRoster, Standing.For(false), Resolve(Next(places)),
                    string.Empty, string.Empty, null, true, false));
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: generating the parents failed.", ex);
            }
        }

        /// <summary>
        ///     How old a parent is in the year this scene happens in.
        ///
        ///     The game's own family model says how far ahead of their oldest
        ///     child a parent stands, and that distance is fixed for life: the
        ///     two people behind a child of seven are the two who were that much
        ///     older than them THEN, not the two standing behind the finished
        ///     character. Staging every chapter's parents at one age is what put
        ///     a mother of thirty-three behind a son of thirty-six.
        /// </summary>
        private static int ParentAgeAt(int sceneAge, bool isFather)
        {
            var session = CreationSession.Current;
            if (session == null) return sceneAge + GameCaps.MinAdultAge();

            return sceneAge + (FamilyAges.ParentAge(session, isFather) - session.EffectiveAge);
        }

        /// <summary>
        ///     How old a brother or sister is in that same year. They were born a
        ///     fixed number of years from the player and stay that far away, so
        ///     the sibling on a childhood stage is a child.
        /// </summary>
        private static int RelativeAgeAt(int sceneAge, int ageNow)
        {
            int lived = LivedAge();
            return Math.Max(YoungestRendered, sceneAge + (ageNow - lived));
        }

        private static string? _parentKeySeed;
        private static BodyProperties _motherBody;
        private static BodyProperties _fatherBody;

        /// <summary>
        ///     The parents this face implies. Cached against the face that produced
        ///     them, so the same life shows the same parents in every chapter and a
        ///     player who goes back to the face generator gets parents that still
        ///     look like their new face.
        /// </summary>
        private static (BodyProperties Mother, BodyProperties Father) ParentBodies()
        {
            var player = CharacterObject.PlayerCharacter;
            var child = player.GetBodyProperties(player.Equipment);

            // The face and not the age: the same person at seven and at thirty is
            // the same person, and their parents do not change when they grow up
            string seed = child.StaticProperties.ToString();

            if (string.Equals(seed, _parentKeySeed, StringComparison.Ordinal))
                return (_motherBody, _fatherBody);

            var mother = child;
            var father = child;
            FaceGen.GenerateParentKey(child, player.Race, ref mother, ref father);

            _motherBody = new BodyProperties(
                new DynamicBodyProperties(ParentBodyAge, 0.3f, 0.2f), mother.StaticProperties);
            _fatherBody = new BodyProperties(
                new DynamicBodyProperties(ParentBodyAge, 0.5f, 0.5f), father.StaticProperties);
            _parentKeySeed = seed;

            return (_motherBody, _fatherBody);
        }

        /// <summary>
        ///     Which brothers and sisters the stage shows, in one place. The bodies
        ///     and the args are built from this one answer, so a second selection
        ///     could not put a face on stage that nothing dresses.
        /// </summary>
        private static List<FamilyMemberSpec> WantedSiblings()
        {
            var session = CreationSession.Current;
            if (session == null) return new List<FamilyMemberSpec>();

            return session.FamilyMembers
                .Where(m => m.IsAlive &&
                            m.Relation is FamilyRelation.Brother or FamilyRelation.Sister)
                .Take(StageCompanionLimit)
                .ToList();
        }

        /// <summary>
        ///     The brothers and sisters the household has composed, at their own
        ///     ages and in their own culture's clothes rather than in the player's
        ///     generated war kit. Silent when the household has none.
        /// </summary>
        private static void AddSiblings(List<NarrativeMenuCharacter> cast,
            List<NarrativeMenuCharacterArgs> args, CultureObject? culture,
            Queue<Stand> places, bool sceneAssertsOne, int sceneAge)
        {
            try
            {
                var player = CharacterObject.PlayerCharacter;
                var (mother, father) = ParentBodies();

                var ages = new List<(int Age, bool Female)>();
                foreach (var spec in WantedSiblings())
                    ages.Add((RelativeAgeAt(sceneAge, FamilyAges.MemberAge(CreationSession.Current, spec)),
                        spec.Relation == FamilyRelation.Sister));

                // The one scene that asserts a sibling describes a YOUNGER one
                // watching from a distance, so where the stand-in is used it is
                // younger than the child and a rank further back than the
                // household stands. Two people the same size at the same depth
                // is what read as two of the same child
                bool standIn = ages.Count == 0;
                if (standIn)
                {
                    if (!sceneAssertsOne) return;

                    // Settled by the face rather than by chance: a render re-runs
                    // on every selection, and a sibling who changed sex between
                    // two clicks is not a sibling
                    bool female = (player.GetBodyProperties(null).StaticProperties
                        .ToString().GetHashCode() & 1) == 0;

                    // The years between them are the run's own, and the very ones
                    // the household will put between these two once it composes
                    // this person. A stand-in carrying a distance of its own is
                    // what made one sister four years younger as a child and a
                    // year older as an adult
                    int offset = SiblingOffsets.For(CreationSession.Current?.RunSeed ?? 0, 0);
                    ages.Add((Math.Max(YoungestRendered, sceneAge + offset), female));
                }

                int index = 0;
                foreach (var (age, female) in ages)
                {
                    var seedFrom = female ? mother : father;

                    var body = new BodyProperties(
                        new DynamicBodyProperties(age, 0.4f, 0.35f), seedFrom.StaticProperties);

                    string id = $"cs_sibling_{index++}";
                    var place = Next(places);
                    if (standIn) place = new Stand(place.Mark, WellBackDepth);

                    cast.Add(new NarrativeMenuCharacter(id, body, player.Race, female));
                    args.Add(new NarrativeMenuCharacterArgs(
                        id,
                        age,
                        RelativeRosterId(culture, age, female),
                        Standing.For(female),
                        Resolve(place),
                        string.Empty,
                        string.Empty,
                        null,
                        true,
                        female));
                }
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: generating the siblings failed.", ex);
            }
        }

        /// <summary>
        ///     The animal that travels with this start. The one the character will
        ///     actually have wins; failing that the rendered roster's own; failing
        ///     that the best mount their culture and standing allow, because a
        ///     chapter about what moves with you is not a chapter to stage an empty
        ///     patch of ground in.
        /// </summary>
        private static void AddMount(List<NarrativeMenuCharacter> cast,
            List<NarrativeMenuCharacterArgs> args, CultureObject? culture, string rosterId)
        {
            try
            {
                var horse = ResolveMount(culture, rosterId);
                if (horse == null) return;

                // The mount and the harness can come from different places, so the pair is checked
                // before the stage draws it: a harness cut for another animal renders as a mess
                var harness = ResolveHarness(culture, rosterId);
                if (!Services.MountFit.Fits(horse, harness)) harness = null;

                var key = MountCreationKey.GetRandomMountKey(
                    horse, CharacterObject.PlayerCharacter.GetMountKeySeed());

                cast.Add(new NarrativeMenuCharacter(HorseCharacterId));
                args.Add(new NarrativeMenuCharacterArgs(
                    HorseCharacterId, -1, string.Empty, HorsePose, MountSpawn,
                    horse.StringId, harness?.StringId ?? string.Empty, key, false));
            }
            catch (Exception ex)
            {
                CSLogger.Error("CharacterPreviewHelper: the mount could not be staged.", ex);
            }
        }

        private static ItemObject? ResolveMount(CultureObject? culture, string rosterId)
        {
            var session = CreationSession.Current;

            var preview = session?.PreviewEquipment?[EquipmentIndex.Horse] ?? EquipmentElement.Invalid;
            if (preview.Item != null) return preview.Item;

            var roster = TryGetRoster(rosterId);
            var fromRoster = roster?.DefaultEquipment[EquipmentIndex.Horse] ?? EquipmentElement.Invalid;
            if (fromRoster.Item != null) return fromRoster.Item;

            return BestForSlot(EquipmentIndex.Horse, culture, session);
        }

        private static ItemObject? ResolveHarness(CultureObject? culture, string rosterId)
        {
            var session = CreationSession.Current;

            var preview = session?.PreviewEquipment?[EquipmentIndex.HorseHarness]
                          ?? EquipmentElement.Invalid;
            if (preview.Item != null) return preview.Item;

            var roster = TryGetRoster(rosterId);
            var fromRoster = roster?.DefaultEquipment[EquipmentIndex.HorseHarness]
                             ?? EquipmentElement.Invalid;
            return fromRoster.Item;
        }

        private static ItemObject? BestForSlot(EquipmentIndex slot, CultureObject? culture,
            CharacterCreationSession? session)
        {
            if (session == null) return null;

            int tier = PreviewTier(session);
            return ArmorQuery.QualifyingItems(slot, culture, tier, session)
                .OrderByDescending(i => i.Culture == culture)
                .ThenBy(i => Math.Abs((int)i.Tier - tier))
                .FirstOrDefault();
        }

        private static int PreviewTier(CharacterCreationSession session) =>
            Services.Application.Steps.EquipmentStep.EffectiveTier(
                session, MCM.Abstractions.Base.Global.GlobalSettings<Settings.CSSettings>.Instance);

        /// <summary>What one selected answer puts on the stage.</summary>
        private sealed class ChosenRender
        {
            public ChosenRender(string? handItem, bool wantsMount, bool parentsBuried = false)
            {
                HandItem = handItem;
                WantsMount = wantsMount;
                ParentsBuried = parentsBuried;
            }

            /// <summary>An item id to put in the character's right hand, or null.</summary>
            public string? HandItem { get; }

            /// <summary>Whether the answer put an animal beside them.</summary>
            public bool WantsMount { get; }

            /// <summary>Whether the answer says both parents are in the ground.</summary>
            public bool ParentsBuried { get; }
        }

        private static readonly Dictionary<string, ChosenRender> Chosen =
            new Dictionary<string, ChosenRender>(StringComparer.Ordinal);

        private static ChosenRender? ChoiceFor(string? menuId)
        {
            if (string.IsNullOrEmpty(menuId)) return null;
            return Chosen.TryGetValue(menuId!, out var chosen) ? chosen : null;
        }

        /// <summary>
        ///     Records what the answer just selected puts on stage, and restages.
        ///
        ///     Selecting an option is a per-render hook in everything but name: the
        ///     stage view re-spawns its visuals from the menu's characters the
        ///     instant the selection changes, and the engine runs an option's
        ///     select handler first. So an answer taken here is drawn on the same
        ///     frame, without a patch and without waiting for the player to commit.
        ///
        ///     Resolved once per answer and remembered, because the item queries
        ///     pick among the best few at random and a render that re-rolled would
        ///     hand the character a different sword every time the stage refreshed.
        /// </summary>
        public static void ChoiceSelected(string menuId, string optionId,
            IReadOnlyList<ChoiceConsequence>? consequences)
        {
            try
            {
                if (string.IsNullOrEmpty(menuId)) return;

                string key = menuId + "|" + optionId;
                if (!_resolvedChoices.TryGetValue(key, out var render))
                {
                    render = Resolve(consequences);
                    _resolvedChoices[key] = render;
                }

                Chosen[menuId] = render;
                RefreshMenuCharacters();
            }
            catch (Exception ex)
            {
                CSLogger.Error($"CharacterPreviewHelper: staging the answer to {menuId} failed.", ex);
            }
        }

        /// <summary>Forgets every answered render, for a creation run that starts over.</summary>
        public static void ForgetChoices()
        {
            Chosen.Clear();
            _resolvedChoices.Clear();
            _parentKeySeed = null;

            // The marks belong to a scene that goes with the run that opened it
            _pads.Clear();
            _padScene = null;
            _padsUsed = 0;
            _padsUnreachable = false;
        }

        private static readonly Dictionary<string, ChosenRender> _resolvedChoices =
            new Dictionary<string, ChosenRender>(StringComparer.Ordinal);

        private static ChosenRender Resolve(IReadOnlyList<ChoiceConsequence>? consequences)
        {
            string? hand = null;
            bool mount = false;
            bool buried = false;

            if (consequences == null) return new ChosenRender(null, false);

            var session = CreationSession.Current;
            var culture = session?.SelectedCulture;
            int tier = session != null ? PreviewTier(session) : 0;

            foreach (var consequence in consequences)
            {
                if (consequence.Kind == ConsequenceKind.Household)
                {
                    if (string.Equals(consequence.Target, BothParentsBuried, StringComparison.Ordinal))
                        buried = true;
                    continue;
                }

                if (consequence.Kind != ConsequenceKind.Item) continue;
                if (consequence.Target == null) continue;

                if (IsMountToken(consequence.Target))
                {
                    mount = true;
                    continue;
                }

                if (hand != null) continue;

                var weaponClass = WeaponClassFor(consequence.Target);
                if (weaponClass == null || session == null) continue;

                var item = GearQuery.QuartermasterPick(weaponClass.Value, culture, tier, session);
                if (item != null) hand = item.StringId;
            }

            return new ChosenRender(hand, mount, buried);
        }

        /// <summary>
        ///     The one household fact that empties a stage. The catalog's own
        ///     target, read rather than guessed at: an answer saying both parents
        ///     are in the ground is the only declaration that contradicts two
        ///     people standing behind the character.
        /// </summary>
        private const string BothParentsBuried = "both_parents_buried";

        private static bool IsMountToken(string target) =>
            string.Equals(target, "riding_horse", StringComparison.Ordinal) ||
            string.Equals(target, "war_horse", StringComparison.Ordinal) ||
            string.Equals(target, "pack_mule", StringComparison.Ordinal);

        /// <summary>
        ///     The class of weapon a scene's symbolic item names. The same reading
        ///     the apply pipeline uses, so what the render puts in the hand is what
        ///     the character will be carrying.
        /// </summary>
        private static WeaponClassChoice? WeaponClassFor(string target)
        {
            return target switch
            {
                "family_sword" or "one_handed_sword" => WeaponClassChoice.OneHandedSword,
                "spear" => WeaponClassChoice.Spear,
                "two_handed_axe" => WeaponClassChoice.TwoHandedAxe,
                "boarding_axe" => WeaponClassChoice.OneHandedAxe,
                "lance" => WeaponClassChoice.Lance,
                "hunting_bow" => WeaponClassChoice.Bow,
                _ => null
            };
        }

        /// <summary>
        ///     How old the character is rendered on this menu.
        ///
        ///     A scene is a moment in a life and the script's stage direction
        ///     names the year it happens in, so that is what the character is
        ///     drawn at. Everything after the scenes is asked of the finished
        ///     character, so those are drawn at the age the life came to, which
        ///     is what the last scene's own direction asks for as well.
        ///
        ///     One answer, read by the composed arguments and by the postfix that
        ///     re-ages the body after them, so the two cannot disagree about the
        ///     same character on the same frame.
        /// </summary>
        public static int PlayerPreviewAge(string? menuId) => ScriptAge(menuId) ?? ChapterAge(menuId) ?? LivedAge();

        /// <summary>
        ///     The age Cultured Start's chapters have always shown the character at:
        ///     a child for the family and childhood chapters, older for schooling,
        ///     youth and the turning point, and the life's own age after that.
        /// </summary>
        private static int? ChapterAge(string? menuId)
        {
            return menuId switch
            {
                "cs_family_menu" or "cs_childhood_menu" => 7,
                "cs_education_menu" => 12,
                "cs_youth_menu" => 17,
                "cs_turning_menu" or "cs_reason_menu" => 20,
                _ => null
            };
        }

        /// <summary>
        ///     The year of the life the script puts this scene in, or null for a
        ///     menu the script does not write.
        ///
        ///     Read off the catalog by the menu's own id, which IS the scene's id,
        ///     so the number the render uses is the number the scene was written
        ///     around. A table of ages keyed by menu id here would be a second
        ///     declaration of the same thing, which is the shape that once gave a
        ///     scene standing in another's place that scene's heading.
        /// </summary>
        private static int? ScriptAge(string? menuId)
        {
            if (string.IsNullOrEmpty(menuId)) return null;

            foreach (var scene in Catalog.SceneCatalog.All)
                if (string.Equals(scene.Id, menuId, StringComparison.Ordinal))
                    return scene.Age;

            return null;
        }

        /// <summary>The age the life has reached, and the age a life starts at before it has told any of itself.</summary>
        private static int LivedAge() =>
            CreationSession.Current?.EffectiveAge ?? (int)StartingAge.Young;

        /// <summary>
        /// Gets the equipment roster ID for the given culture and stage.
        /// For AdultWithPreview and Weapon stages, uses the generated preview roster.
        /// For earlier stages, uses vanilla culture+occupation rosters.
        /// </summary>
        public static string GetEquipmentRosterId(CultureObject? culture, PreviewStage stage,
            OutfitKind dress = OutfitKind.Battle)
        {
            // Once the character is grown, what they are wearing is theirs and it
            // stays on. Which OUTFIT is shown is the chapter's business: the battle
            // preview is re-rolled by the chapters that own gear, so a chapter that
            // renders it while asking about something else is what made the kit
            // appear to change on its own.
            bool grown = stage is PreviewStage.Adult or PreviewStage.AdultWithPreview
                or PreviewStage.Weapon;
            if (grown && _previewService != null && _previewService.HasPreviewEquipment)
            {
                var session = CreationSession.Current;
                if (dress == OutfitKind.Battle) return _previewService.GetPreviewRosterId();
                if (session != null)
                    return _previewService.GetPreviewRosterId(dress, session, culture,
                        PreviewTier(session));
            }

            // Pre-tier menus: use vanilla culture+occupation rosters
            var cultureId = culture?.StringId ?? "empire";
            var isFemale = CharacterObject.PlayerCharacter.IsFemale;
            var occupation = GetOccupationForFamily(HowTheyWereRaised(), cultureId);

            return stage switch
            {
                PreviewStage.Childhood => ResolveAgeRoster("player_char_creation_childhood_age",
                    cultureId, occupation, isFemale),
                PreviewStage.Education => ResolveAgeRoster("player_char_creation_education_age",
                    cultureId, occupation, isFemale),
                _ => ResolveAdultRoster(cultureId, occupation, isFemale)
            };
        }

        /// <summary>
        ///     What a relative of this age wears: the game's own age rosters for the
        ///     household's trade, never the player's generated kit, because a
        ///     brother is not wearing his sibling's armor.
        /// </summary>
        private static string RelativeRosterId(CultureObject? culture, int age, bool isFemale)
        {
            var cultureId = culture?.StringId ?? "empire";
            var occupation = GetOccupationForFamily(HowTheyWereRaised(), cultureId);

            if (age < 10)
                return ResolveAgeRoster("player_char_creation_childhood_age",
                    cultureId, occupation, isFemale);
            if (age < 15)
                return ResolveAgeRoster("player_char_creation_education_age",
                    cultureId, occupation, isFemale);

            return ResolveAdultRoster(cultureId, occupation, isFemale);
        }

        /// <summary>
        ///     What kind of household to dress the child in.
        ///
        ///     Nothing asks the player outright, so the strongest leaning of the
        ///     life told so far stands in for the answer, which is the same reading
        ///     every other derived amount uses.
        /// </summary>
        private static FamilyBackground HowTheyWereRaised()
        {
            // Cultured Start asks outright, in its family chapter
            var session = CreationSession.Current;
            if (session?.Mode == SetupMode.LifePath)
                return session.SelectedFamily ?? FamilyBackground.Farmers;

            return LifeProfile.From(CreationSession.Current).Dominant switch
            {
                LifeProfile.Lean.Martial => FamilyBackground.Retainers,
                LifeProfile.Lean.Standing => FamilyBackground.Retainers,
                LifeProfile.Lean.Commerce => FamilyBackground.Merchants,
                LifeProfile.Lean.Craft => FamilyBackground.Artisans,
                LifeProfile.Lean.Wilds => FamilyBackground.Hunters,
                LifeProfile.Lean.Sea => FamilyBackground.Seafarers,
                _ => FamilyBackground.Farmers
            };
        }

        /// <summary>
        /// Maps family background to vanilla equipment occupation for roster resolution.
        /// </summary>
        public static string GetOccupationForFamily(FamilyBackground family, string cultureId)
        {
            bool useHerder = family == FamilyBackground.Farmers &&
                (string.Equals(cultureId, "aserai", StringComparison.Ordinal) ||
                 string.Equals(cultureId, "khuzait", StringComparison.Ordinal));

            return family switch
            {
                FamilyBackground.Retainers => "retainer",
                FamilyBackground.Merchants => "merchant_urban",
                FamilyBackground.Farmers => useHerder ? "herder" : "farmer",
                FamilyBackground.Artisans => "artisan_urban",
                FamilyBackground.Hunters => "hunter",
                FamilyBackground.Vagabonds => "vagabond_urban",
                FamilyBackground.Seafarers => "seafarer",
                FamilyBackground.Dockworkers => "shipmaster_urban",
                FamilyBackground.Shipwrights => "shipmaster_urban",
                _ => "guard"
            };
        }

        /// <summary>
        ///     The same household, in the vocabulary the parent rosters are filed
        ///     under. It is a shorter list than the player's: there is no "_urban"
        ///     suffix and no guard, and "none" is the game's own default parent.
        /// </summary>
        private static string ParentOccupation(FamilyBackground family, string cultureId)
        {
            bool useHerder = family == FamilyBackground.Farmers &&
                (string.Equals(cultureId, "aserai", StringComparison.Ordinal) ||
                 string.Equals(cultureId, "khuzait", StringComparison.Ordinal));

            return family switch
            {
                FamilyBackground.Retainers => "retainer",
                FamilyBackground.Merchants => "merchant",
                FamilyBackground.Farmers => useHerder ? "herder" : "farmer",
                FamilyBackground.Artisans => "artisan",
                FamilyBackground.Hunters => "hunter",
                FamilyBackground.Vagabonds => "vagabond",
                FamilyBackground.Seafarers => "seafarer",
                FamilyBackground.Dockworkers => "shipmaster",
                FamilyBackground.Shipwrights => "shipmaster",
                _ => "none"
            };
        }

        private static string ResolveAgeRoster(string prefix, string cultureId,
            string occupation, bool isFemale)
        {
            var gender = isFemale ? "f" : "m";
            var altGender = isFemale ? "m" : "f";

            var candidates = new[]
            {
                $"{prefix}_{cultureId}_{occupation}_{gender}",
                $"{prefix}_{cultureId}_{occupation}_{altGender}",
                $"{prefix}_{cultureId}_guard_{gender}",
                $"{prefix}_{cultureId}_guard_{altGender}",
                $"{prefix}_{cultureId}_farmer_{gender}",
                $"{prefix}_{cultureId}_farmer_{altGender}"
            };

            return FindExistingRosterId(candidates)
                ?? ResolveAdultRoster(cultureId, occupation, isFemale);
        }

        private static string ResolveAdultRoster(string cultureId, string occupation, bool isFemale)
        {
            var gender = isFemale ? "f" : "m";
            var altGender = isFemale ? "m" : "f";

            var candidates = new[]
            {
                $"player_char_creation_{cultureId}_{occupation}_{gender}",
                $"player_char_creation_{cultureId}_{occupation}_{altGender}",
                $"player_char_creation_{cultureId}_guard_{gender}",
                $"player_char_creation_{cultureId}_guard_{altGender}",
                $"player_char_creation_{cultureId}_farmer_{gender}",
                $"player_char_creation_{cultureId}_farmer_{altGender}",
                $"player_char_creation_empire_guard_{gender}",
                $"player_char_creation_empire_guard_{altGender}"
            };

            return FindExistingRosterId(candidates) ?? "player_char_creation_default";
        }

        private static string? FindExistingRosterId(IEnumerable<string> candidates)
        {
            foreach (var rosterId in candidates)
            {
                if (TryGetRoster(rosterId) != null)
                    return rosterId;
            }
            return null;
        }

        private static MBEquipmentRoster? TryGetRoster(string rosterId)
        {
            if (string.IsNullOrEmpty(rosterId)) return null;

            if (Game.Current != null)
            {
                var roster = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(rosterId);
                if (roster != null) return roster;
            }
            return MBObjectManager.Instance.GetObject<MBEquipmentRoster>(rosterId);
        }
    }
}
