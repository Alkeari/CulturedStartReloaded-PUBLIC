using System;
using System.Collections.Generic;
using System.Reflection;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;

namespace CulturedStartReloaded.CharacterCreation.Catalog
{
    /// <summary>
    ///     The guided route's scenes, transcribed from <c>SceneScript.md</c>.
    ///     Nothing here is asked of the player: every scene puts a situation, and
    ///     what the answer says about the character is inferred from the
    ///     consequences the option carries.
    ///
    ///     All thirteen of the script's scenes are carried, in the order it puts
    ///     them and under the ids it gives them, and every scene is put to every
    ///     player. What the script calls a reveal is not transcribed: the line the
    ///     player reads beside an option is derived from that option's own
    ///     consequences, so a second hand-written declaration of the same fact
    ///     would drift from the first.
    /// </summary>
    public static class SceneCatalog
    {
        /// <summary>Every scene, in the order the run puts them.</summary>
        public static IReadOnlyList<Scene> All { get; } = Build(NavalLoaded());

        /// <summary>
        ///     The run, as the installed game gives it. Thirteen scenes either
        ///     way: War Sails does not lengthen the route, it changes what the
        ///     ninth situation is. The waterfront scene stands in the town
        ///     scene's place and under the same id, so the flow table and the
        ///     stage direction that name that id keep naming the scene the player
        ///     is actually put in front of. Its heading is the one thing it does
        ///     not take from the scene it replaces: a title is carried on the
        ///     scene itself, so standing in another's place never means wearing
        ///     that scene's name.
        ///
        ///     Taken as an argument rather than asked for inside, so both runs can
        ///     be built and weighed against each other without a game to load.
        /// </summary>
        internal static IReadOnlyList<Scene> Build(bool warSails)
        {
            return new[]
            {
                BuildWhatTheHouseHad(),
                BuildTheOneAlwaysInTheWrong(),
                BuildTheStoreBurned(),
                BuildWhatTheyCalledYouThen(),
                BuildTheManWhoTaughtYou(),
                BuildWhatYouCouldDo(),
                BuildWhatTheYardSaid(),
                BuildTheVerdict(),
                warSails ? BuildWhatThePortSaid() : BuildWhatTheTownSaid(),
                BuildTheSeat(),
                BuildThePurse(),
                BuildTheWinterBetween(),
                BuildTheNameTheyUse()
            };
        }

        /// <summary>
        ///     Whether War Sails is loaded, asked of
        ///     <c>Services/NavalDLCService</c> by name rather than by reference.
        ///
        ///     That service reads the game's own module list, so naming it here
        ///     would pull TaleWorlds types into this file, and this catalog and
        ///     the whole of <c>CharacterCreation/Scenes</c> are deliberately free
        ///     of them: it is the tier the tests and the measuring harnesses
        ///     compile on their own, with no game anywhere. An assembly that does
        ///     not carry the service is an assembly with no game in it, which is
        ///     the same answer as no DLC, so the lookup coming back empty is not a
        ///     fault to report. The name is pinned by a test that reads the
        ///     service's own file, because a rename here would otherwise be
        ///     silent and would take the sea out of the run without a word.
        ///
        ///     Asked once. Which modules are loaded is settled before the game
        ///     window opens and cannot change while a character is being made.
        /// </summary>
        private static bool NavalLoaded()
        {
            try
            {
                var service = typeof(SceneCatalog).Assembly
                    .GetType("CulturedStartReloaded.Services.NavalDLCService", throwOnError: false);

                var ask = service?.GetMethod("IsNavalDLCLoaded",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

                return ask?.Invoke(null, null) is true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Construction helpers

        private static SceneOption Option(string id, string title, string prose,
            params ChoiceConsequence[] consequences) =>
            new SceneOption(id, title, prose, consequences);

        /// <summary>An answer whose writing changes with the life reading it.</summary>
        private static SceneOption Option(string id, string title, string prose,
            TextVariant[] written, params ChoiceConsequence[] consequences) =>
            new SceneOption(id, title, prose, consequences, written);

        private static TextVariant[] Written(params TextVariant[] variants) => variants;

        private static TextVariant Instead(string when, Func<ISceneAnswers, bool> holds, string text) =>
            new TextVariant(when, holds, text);

        #endregion

        #region The states the writing is varied for

        /// <summary>
        ///     The one answer that leaves this character with no house at all. Two
        ///     later scenes put a situation that assumes one, so both read this.
        /// </summary>
        private const string TheRoadAndNoRoof = "cs_opt_a_road_and_no_roof";

        private static bool NoHouseToSpeakOf(ISceneAnswers life) => life.Chose(TheRoadAndNoRoof);

        /// <summary>
        ///     The life has said in so many words that both parents are living,
        ///     which is the one reading under which the last scene's answer puts
        ///     neither of them in the ground.
        /// </summary>
        private static bool BothParentsLiving(ISceneAnswers life) => StoryGraves.Of(life) == null;

        private static bool BuriedTheFather(ISceneAnswers life)
        {
            var (father, mother) = StoryHome.Standing(life);
            return !father && mother;
        }

        private static bool BuriedTheMother(ISceneAnswers life)
        {
            var (father, mother) = StoryHome.Standing(life);
            return father && !mother;
        }

        private static bool BuriedThemBoth(ISceneAnswers life)
        {
            var (father, mother) = StoryHome.Standing(life);
            return !father && !mother;
        }

        #endregion

        #region Construction helpers, continued

        private static ChoiceConsequence Goodwill(string target, int amount) =>
            new ChoiceConsequence(ConsequenceKind.Goodwill, target, amount);

        private static ChoiceConsequence Enmity(string target, int amount) =>
            new ChoiceConsequence(ConsequenceKind.Enmity, target, amount);

        private static ChoiceConsequence Trait(string target, int amount) =>
            new ChoiceConsequence(ConsequenceKind.Trait, target, amount);

        private static ChoiceConsequence Item(string target, int amount) =>
            new ChoiceConsequence(ConsequenceKind.Item, target, amount);

        private static ChoiceConsequence Place(string target) =>
            new ChoiceConsequence(ConsequenceKind.Place, target);

        /// <summary>
        ///     A place with how much of the life went into it. Only the water
        ///     takes one. Every other place in the script is somewhere a life was
        ///     tied to, which is a yes or a no, where the difference between a man
        ///     who was out every season and a man who went out when nothing else
        ///     was hiring is the whole of what that answer is worth.
        /// </summary>
        private static ChoiceConsequence Place(string target, int amount) =>
            new ChoiceConsequence(ConsequenceKind.Place, target, amount);

        private static ChoiceConsequence Ally(string target) =>
            new ChoiceConsequence(ConsequenceKind.Ally, target);

        private static ChoiceConsequence Title(string target) =>
            new ChoiceConsequence(ConsequenceKind.Title, target);

        private static ChoiceConsequence Debt(int amount) =>
            new ChoiceConsequence(ConsequenceKind.Debt, null, amount);

        private static ChoiceConsequence LostYears(int years) =>
            new ChoiceConsequence(ConsequenceKind.LostYears, null, years);

        /// <summary>
        ///     Who this answer settles in the character's own house. The three
        ///     household chapters compose the house out of these, and the panel
        ///     states each one, so a scene that leaves somebody at that table says
        ///     it once and both halves read the same declaration.
        /// </summary>
        private static ChoiceConsequence Household(string target) =>
            new ChoiceConsequence(ConsequenceKind.Household, target);

        /// <summary>
        ///     One gate edge, carrying the sentence the shut option shows to a life
        ///     that came by this edge.
        ///
        ///     A target shut from several places writes its reason per edge
        ///     wherever the edges shut it for different reasons, because
        ///     <see cref="ChoiceGates.ClosedBecause"/> hands back the note of a
        ///     gate this life actually set, so an edge's sentence is only ever read
        ///     by somebody who gave that answer. One sentence naming both causes is
        ///     half false for everybody who gave one of the two answers and not the
        ///     other. Where the reason holds whatever shut the door, the same
        ///     sentence goes on every edge and names no cause at all.
        /// </summary>
        private static ChoiceConsequence Gate(string optionId, string note) =>
            new ChoiceConsequence(ConsequenceKind.Gate, optionId, 0, note);

        private const string ClosedTheStewardWhoTookYouIn =
            "{=CSR_Scene_Closed_TheStewardWhoTookYouIn}No household takes on the boy his own house had already given up on, and that steward asked in the village before he asked you.";

        private const string ClosedTheCrewThatAskedNoNames =
            "{=CSR_Scene_Closed_TheCrewThatAskedNoNames}A crew that asks no names does not take the one boy in the square that everybody can place.";

        private const string ClosedYouTookItUpward =
            "{=CSR_Scene_Closed_YouTookItUpward}Your word does not carry in that hall, and the reason is written on a shelf in it.";

        private const string ClosedYouCouldRideAtAMan =
            "{=CSR_Scene_Closed_YouCouldRideAtAMan}Nobody teaches themselves this, and you never had the animal to learn it on.";

        private const string ClosedYouCouldRideAtAManWithAnAnimal =
            "{=CSR_Scene_Closed_YouCouldRideAtAManWithAnAnimal}Nobody teaches themselves this, and the animal was never the part you were missing.";

        private const string ClosedTookItAndKeptItLeftAWall =
            "{=CSR_Scene_Closed_TookItAndKeptItLeftAWall}Nobody offers this across a table to a man who left a wall.";

        private const string ClosedTookItAndKeptItWrittenDown =
            "{=CSR_Scene_Closed_TookItAndKeptItWrittenDown}Nobody offers this across a table to a man already written down in a great house for the wrong reason.";

        private const string ClosedNobodyTaughtYou =
            "{=CSR_Scene_Closed_NobodyTaughtYou}A sum that house did not have was found for you, and whoever was paid it stood over you until you had it right.";

        private const string ClosedTheNameOfAHouse =
            "{=CSR_Scene_Closed_TheNameOfAHouse}No house is putting its name in front of yours.";

        /// <summary>The animals a scene can leave in the character's hands.</summary>
        private static readonly string[] Animals = { "riding_horse", "pack_mule", "war_horse" };

        /// <summary>
        ///     The sentence a shut door shows, for the life standing in front of it.
        ///
        ///     An edge's own note is what most lives read, and the one gate whose
        ///     reason turns on what the character is already holding says the other
        ///     thing instead: the door stays shut either way and only
        ///     the reason moves, because two earlier answers hand over an animal
        ///     and state that they do, so telling that player he never had one
        ///     contradicts a panel he read scenes ago.
        ///
        ///     The life is passed in rather than read here, so the reason is
        ///     composed from what has been answered by the time the note goes up
        ///     and never from a scene still ahead of it.
        /// </summary>
        public static string? NoteFor(ChoiceConsequence gate, IReadOnlyList<ChoiceConsequence>? lifeSoFar)
        {
            if (gate == null) return null;

            if (string.Equals(gate.Target, "cs_opt_you_could_ride_at_a_man", StringComparison.Ordinal) &&
                HoldsAnAnimal(lifeSoFar))
                return ClosedYouCouldRideAtAManWithAnAnimal;

            return gate.Note;
        }

        private static bool HoldsAnAnimal(IReadOnlyList<ChoiceConsequence>? lifeSoFar)
        {
            if (lifeSoFar == null) return false;

            foreach (var consequence in lifeSoFar)
                if (consequence != null && consequence.Kind == ConsequenceKind.Item &&
                    Array.IndexOf(Animals, consequence.Target ?? string.Empty) >= 0)
                    return true;

            return false;
        }

        #endregion

        #region Scene 1: What the House Had

        /// <summary>Targets: Origins. Severity: Formative.</summary>
        private static Scene BuildWhatTheHouseHad()
        {
            return new Scene("cs_scene_what_the_house_had",
                "{=CSR_Scene_WhatTheHouseHad_Title}What the House Had",
                "{=CSR_Scene_WhatTheHouseHad_Prompt}Before any of the rest of it there was a house, and the house had what it had. Some of what it had you only understood the worth of much later, and some of it you have never put down. What did your house have, when you were small?",
                new[]
                {
                    Option("cs_opt_a_name_people_knew",
                        "{=CSR_Scene_ANamePeopleKnew_Title}A Name People Knew",
                        "{=CSR_Scene_ANamePeopleKnew_Prose}There was little coin in it and there had not been for two generations, but the name went in front of you at every door in the valley and the doors opened on it. Your father's own father and mother are in the ground and the valley still says the name they kept, and you found out much later what they gave up to keep it worth that.",
                        Goodwill("culture_lords", 2),
                        Place("family_seat"),
                        Household("forebears_buried")),
                    Option("cs_opt_land_and_no_coin",
                        "{=CSR_Scene_LandAndNoCoin_Title}Ground, and Nothing to Spend",
                        "{=CSR_Scene_LandAndNoCoin_Prose}There was land and it was yours and there was never anything in the box. You ate what you grew and wore what you grew, and everything else was a matter of waiting for a season to turn.",
                        Place("home_village"),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_a_bench_and_a_trade",
                        "{=CSR_Scene_ABenchAndATrade_Title}A bench and what came off it",
                        "{=CSR_Scene_ABenchAndATrade_Prose}The living came out of one room and one pair of hands, and everybody under that roof knew what those hands were doing at any hour of the day. You could tell a good piece from a poor one long before you could make either.",
                        Goodwill("village_headmen", 2),
                        Place("home_workshop"),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_debts_in_other_peoples_books",
                        "{=CSR_Scene_DebtsInOtherPeoplesBooks_Title}Other people's books, with your name in them",
                        "{=CSR_Scene_DebtsInOtherPeoplesBooks_Prose}What your house had was owed. It was owed in three places and had been since before you were born, and the shape of the whole year was decided by which of them was visited when.",
                        Debt(600),
                        Goodwill("town_merchants", 2)),
                    Option("cs_opt_arms_over_the_door",
                        "{=CSR_Scene_ArmsOverTheDoor_Title}What Hung Over the Door",
                        "{=CSR_Scene_ArmsOverTheDoor_Prose}There was steel on the wall and it was not decoration, and twice in your childhood it came down. Nobody in that house would have sold it to eat, and everybody knew that without it ever being said aloud.",
                        Goodwill("town_gang_leaders", 2),
                        Goodwill("culture_lords", 1)),
                    Option("cs_opt_a_road_and_no_roof",
                        "{=CSR_Scene_ARoadAndNoRoof_Title}A road, and whatever was on it",
                        "{=CSR_Scene_ARoadAndNoRoof_Prose}There was no house to speak of. There was a cart, a route and the people walking beside it, and your mother and father went into the ground at the side of that route before you were old enough to take the reins. The rest of them fed you and kept moving, and you slept where the light ran out, and you knew four towns before you knew one of them properly.",
                        Place("open_country"),
                        Item("pack_mule", 1),
                        Goodwill("town_merchants", 1),
                        Household("both_parents_buried"))
                }, age: 7, severity: Severity.Formative);
        }

        #endregion

        #region Scene 2: The One Always in the Wrong

        /// <summary>Targets: Origins, Inclination. Severity: Formative. Carries a gate.</summary>
        private static Scene BuildTheOneAlwaysInTheWrong()
        {
            return new Scene("cs_scene_the_one_always_in_the_wrong",
                "{=CSR_Scene_TheOneAlwaysInTheWrong_Title}The one always in the wrong",
                "{=CSR_Scene_TheOneAlwaysInTheWrong_Prompt}Every house has one and yours was no different. There was somebody under that roof who was always the one in the wrong, and the household had settled on it long before you were old enough to be asked. Who was it, and were they?",
                new[]
                {
                    Option("cs_opt_your_brother_and_he_was",
                        "{=CSR_Scene_YourBrotherAndHeWas_Title}Your Brother, and He Was",
                        "{=CSR_Scene_YourBrotherAndHeWas_Prose}He took what he wanted and the house arranged itself around that, and every time it was explained to you why this once did not count. You stopped believing the explanation earlier than anybody in there realized.",
                        Trait("Calculating", 1),
                        Trait("Generosity", -1),
                        Household("a_brother")),
                    Option("cs_opt_your_aunt_and_she_was_not",
                        "{=CSR_Scene_YourAuntAndSheWasNot_Title}Your aunt, and she was not",
                        "{=CSR_Scene_YourAuntAndSheWasNot_Prose}It came to her door whatever it was, because it always had, and you were the only one who went and sat with her afterward. She taught you things nobody else under that roof knew, out of nothing but the company.",
                        Written(
                            Instead("the-life-had-no-house", NoHouseToSpeakOf,
                                "{=CSR_Scene_YourAuntAndSheWasNot_Prose_Road}It came to her whatever it was, because it always had, and you were the only one who went and sat with her afterward. She taught you things nobody else on that road knew, out of nothing but the company.")),
                        Ally("the_one_nobody_sat_with"),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_your_father_and_he_was",
                        "{=CSR_Scene_YourFatherAndHeWas_Title}Your Father, and He Was",
                        "{=CSR_Scene_YourFatherAndHeWas_Prose}He was wrong about most of it, and being told so made it worse, and what was handed to him was smaller when it left his hands than when it came into them. He is in the ground now, and so are the father and mother who handed it to him, and none of that has made him right about any of it. You have never since assumed a man is right because he is the one standing in front of you.",
                        Debt(400),
                        Place("family_seat"),
                        Household("father_buried"),
                        Household("forebears_buried")),
                    Option("cs_opt_your_mother_and_she_was_not",
                        "{=CSR_Scene_YourMotherAndSheWasNot_Title}Your mother, and she was not",
                        "{=CSR_Scene_YourMotherAndSheWasNot_Prose}The house blamed her for a decision she had made alone because nobody else would make it, and it was the right decision, and it held. She has been in the ground for years with the blame still on her, because the house has never taken it back, and you have gone on saying it for her in rooms she is not in.",
                        Trait("Honor", 1),
                        Goodwill("village_headmen", 2),
                        Household("mother_buried")),
                    Option("cs_opt_it_was_you_and_they_were_right",
                        "{=CSR_Scene_ItWasYouAndTheyWereRight_Title}It was you, and they were right",
                        "{=CSR_Scene_ItWasYouAndTheyWereRight_Prose}It was you, and what they said about you was true and most of it still is. You cost that house money it did not have, twice, before you were old enough to be sent anywhere, and you have not paid it back.",
                        Debt(500),
                        Goodwill("town_gang_leaders", 1),
                        Trait("Honor", -1),
                        Gate("cs_opt_the_steward_who_took_you_in",
                            ClosedTheStewardWhoTookYouIn),
                        Gate("cs_opt_you_took_it_upward",
                            ClosedYouTookItUpward)),
                    Option("cs_opt_it_was_you_and_they_were_not",
                        "{=CSR_Scene_ItWasYouAndTheyWereNot_Title}It was you, and they were not",
                        "{=CSR_Scene_ItWasYouAndTheyWereNot_Prose}You were the one it landed on and you had not done any of it, and you stopped explaining yourself somewhere around your ninth year. You got very good at being somewhere else when a thing was being decided.",
                        Trait("Valor", -1),
                        Place("home_woodland"))
                }, age: 10, severity: Severity.Formative,
                promptVariants: Written(
                    Instead("the-life-had-no-house", NoHouseToSpeakOf,
                        "{=CSR_Scene_TheOneAlwaysInTheWrong_Prompt_Road}Every household has one and yours was no different, and yours had no roof over it. There was somebody among the people who fed you and kept you moving who was always the one in the wrong, and they had settled on it long before you were old enough to be asked. Who was it, and were they?")));
        }

        #endregion

        #region Scene 3: The Store Burned

        /// <summary>Targets: Origins (starting means), Inclination. Severity: Formative.</summary>
        private static Scene BuildTheStoreBurned()
        {
            return new Scene("cs_scene_the_store_burned",
                "{=CSR_Scene_TheStoreBurned_Title}The Store Burned",
                "{=CSR_Scene_TheStoreBurned_Prompt}The store shed went up in the small hours of a dry summer, and everything the household owned that was not on its back was inside it. You got in twice before the roof came down. One thing came out of that fire with you and it is in your kit now. What is it?",
                new[]
                {
                    Option("cs_opt_the_sword_off_the_hooks",
                        "{=CSR_Scene_TheSwordOffTheHooks_Title}What Was on the Hooks",
                        "{=CSR_Scene_TheSwordOffTheHooks_Prose}You went for the pegs over the door and came out with everything hanging on them. It was the oldest thing in that house and the only thing nobody had ever once suggested selling.",
                        Item("family_sword", 1),
                        Place("family_seat")),
                    Option("cs_opt_the_box_of_papers",
                        "{=CSR_Scene_TheBoxOfPapers_Title}The box from under the board",
                        "{=CSR_Scene_TheBoxOfPapers_Prose}You knew which board it was under because you had watched it go under there. You came out with the coin and with the papers saying what was owed to whom, and the papers turned out to matter more than the coin did.",
                        Item("coin_pouch", 1),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_the_bench_chest",
                        "{=CSR_Scene_TheBenchChest_Title}The Bench Chest",
                        "{=CSR_Scene_TheBenchChest_Prose}You dragged it out by one handle with your shirt over your face. Everything else in there could be bought again by people who owned tools like these, and you were not going to be the reason they could not.",
                        Item("craft_tools", 1),
                        Place("home_workshop")),
                    Option("cs_opt_the_halter_and_what_was_on_it",
                        "{=CSR_Scene_TheHalterAndWhatWasOnIt_Title}The halter, and what was on the end of it",
                        "{=CSR_Scene_TheHalterAndWhatWasOnIt_Prose}The byre wall was already taking the fire and you went along it with a knife until the animals in there were loose. One of them would not go and you led it out yourself, and you have had that rope on your saddle since.",
                        Item("riding_horse", 1),
                        Goodwill("village_headmen", 2)),
                    Option("cs_opt_the_stores_off_the_back_wall",
                        "{=CSR_Scene_TheStoresOffTheBackWall_Title}What was stacked at the back",
                        "{=CSR_Scene_TheStoresOffTheBackWall_Prose}You came out under the winter stores and nothing else, and you were laughed at for it that night. You fed four households out of that load before the spring and nobody laughed the second time.",
                        Item("trade_goods", 1),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_nothing_your_hands_were_full",
                        "{=CSR_Scene_NothingYourHandsWereFull_Title}Nothing. Your Hands Were Full",
                        "{=CSR_Scene_NothingYourHandsWereFull_Prose}Your sister had gone back in after something that did not matter and you carried her out and nothing else. The household lost everything it owned that night, and you have never once had to think about whether it was right.",
                        Trait("Mercy", 1),
                        Goodwill("village_headmen", 1),
                        Place("home_village"),
                        Household("a_sister"))
                }, age: 13, severity: Severity.Formative,
                promptVariants: Written(
                    Instead("the-life-had-no-house", NoHouseToSpeakOf,
                        "{=CSR_Scene_TheStoreBurned_Prompt_Road}The store the people you travelled with were wintering in went up in the small hours of a dry summer, and everything any of you owned that was not on a back or a cart was inside it. You got in twice before the roof came down. One thing came out of that fire with you and it is in your kit now. What is it?")));
        }

        #endregion

        #region Scene 4: What They Called You Then

        /// <summary>Targets: Inclination, Schooling. Severity: Formative. Carries a gate.</summary>
        private static Scene BuildWhatTheyCalledYouThen()
        {
            return new Scene("cs_scene_what_they_called_you_then",
                "{=CSR_Scene_WhatTheyCalledYouThen_Title}What They Called You Then",
                "{=CSR_Scene_WhatTheyCalledYouThen_Prompt}You came back for something you had left behind and stopped outside the door, because they were talking about you in there and had not heard you come up the path. You stood and listened to the whole of it. What did they say about you, when you were not in the room?",
                new[]
                {
                    Option("cs_opt_they_said_you_were_your_fathers_son",
                        "{=CSR_Scene_TheySaidYouWereYourFathersSon_Title}That you were your father again",
                        "{=CSR_Scene_TheySaidYouWereYourFathersSon_Prose}They said it as a settled thing, half of it approving and half of it a warning, and everybody in that room knew which half was meant. It walked into every room in the valley ahead of you for years afterward.",
                        Goodwill("village_headmen", 2),
                        Place("home_village"),
                        Gate("cs_opt_the_crew_that_asked_no_names",
                            ClosedTheCrewThatAskedNoNames)),
                    Option("cs_opt_they_said_you_would_not_stay",
                        "{=CSR_Scene_TheySaidYouWouldNotStay_Title}That you would not be here long",
                        "{=CSR_Scene_TheySaidYouWouldNotStay_Prose}They were not angry about it. They said it the way people say a thing about the weather and then went on to what would be done about the roof, and standing out there you understood that you had already gone.",
                        LostYears(1),
                        Place("open_country"),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_they_said_you_were_soft",
                        "{=CSR_Scene_TheySaidYouWereSoft_Title}That there was no hardness in you",
                        "{=CSR_Scene_TheySaidYouWereSoft_Prose}One of them said it kindly and the other did not, and the unkind one won the argument. You have been answering it ever since without telling anybody that is what you are doing.",
                        Trait("Valor", 1),
                        LostYears(2),
                        Enmity("village_headmen", 1)),
                    Option("cs_opt_they_said_you_never_forgot_anything",
                        "{=CSR_Scene_TheySaidYouNeverForgotAnything_Title}That you never let a thing go",
                        "{=CSR_Scene_TheySaidYouNeverForgotAnything_Prose}They meant it as a complaint. They listed three things you had not let go of and they were right about all three, and until that evening you had not known anyone else remembered them at all.",
                        Goodwill("town_merchants", 2),
                        Enmity("village_headmen", 1)),
                    Option("cs_opt_they_said_you_should_be_taught",
                        "{=CSR_Scene_TheySaidYouShouldBeTaught_Title}That you should be sent somewhere and taught",
                        "{=CSR_Scene_TheySaidYouShouldBeTaught_Prose}One of them had been saying it for a year and the other had finally stopped arguing, and a sum was named that the house did not have. It was found anyway. You have never asked from where.",
                        Debt(400),
                        Goodwill("culture_lords", 1),
                        Gate("cs_opt_nobody_taught_you",
                            ClosedNobodyTaughtYou)),
                    Option("cs_opt_they_said_nothing_about_you",
                        "{=CSR_Scene_TheySaidNothingAboutYou_Title}That they were not talking about you at all",
                        "{=CSR_Scene_TheySaidNothingAboutYou_Prose}You stood there a while waiting for your name and it did not come, and the whole of it was about somebody else. You went in, took what you had come for, and said nothing to anyone.",
                        Trait("Calculating", -1),
                        Goodwill("town_gang_leaders", 1),
                        LostYears(1),
                        Household("a_sibling"))
                }, age: 15, severity: Severity.Formative);
        }

        #endregion

        #region Scene 5: The Man Who Taught You

        /// <summary>Targets: Schooling. Severity: Defining. Carries gates.</summary>
        private static Scene BuildTheManWhoTaughtYou()
        {
            return new Scene("cs_scene_the_man_who_taught_you",
                "{=CSR_Scene_TheManWhoTaughtYou_Title}The Man Who Taught You",
                "{=CSR_Scene_TheManWhoTaughtYou_Prompt}Somebody took you on and made you into whatever you are, and the people who knew you both had an opinion about him that they did not keep to themselves. They said he was wasting you, or that he was the best thing that had ever happened to you, and they said it where you could hear. Who was he, and were they right about him?",
                new[]
                {
                    Option("cs_opt_the_steward_who_took_you_in",
                        "{=CSR_Scene_TheStewardWhoTookYouIn_Title}A household man, and they were right about him",
                        "{=CSR_Scene_TheStewardWhoTookYouIn_Prose}He stopped in front of you in a crowded square, asked one question, and put you behind him without waiting for you to gather anything. They said he had done you the largest favor anybody ever would, and you have not found an argument against it.",
                        Goodwill("culture_lords", 2),
                        Place("nearest_castle"),
                        LostYears(1)),
                    Option("cs_opt_the_caravan_master",
                        "{=CSR_Scene_TheCaravanMaster_Title}A caravan master, and they were wrong about him",
                        "{=CSR_Scene_TheCaravanMaster_Prose}They said he was using you up on a road for the price of your food, and not one of them had ever been more than a day from where they were born. He showed you four towns and how the price of the same sack moves between them.",
                        Goodwill("town_merchants", 2),
                        Place("nearest_town"),
                        Item("pack_mule", 1)),
                    Option("cs_opt_the_sergeant",
                        "{=CSR_Scene_TheSergeant_Title}A sergeant, and they were right about him",
                        "{=CSR_Scene_TheSergeant_Prose}They said he would get you killed and they said it to his face, and he agreed with them and drilled you until dark anyway. Of the ones he took out of that square, few are still alive, and you are one of them.",
                        Goodwill("culture_lords", 2),
                        Goodwill("town_gang_leaders", 1)),
                    Option("cs_opt_the_craftsman",
                        "{=CSR_Scene_TheCraftsman_Title}A craftsman, and they were wrong about him",
                        "{=CSR_Scene_TheCraftsman_Prose}They said he kept you at the bench long past when you were any use to him, which was true, and that it amounted to theft, which was not. He put his name near nothing of yours until it was worth his name being near it.",
                        Goodwill("town_merchants", 2),
                        Place("home_workshop"),
                        LostYears(2)),
                    Option("cs_opt_the_crew_that_asked_no_names",
                        "{=CSR_Scene_TheCrewThatAskedNoNames_Title}A man who never gave you his name, and they were right about him",
                        "{=CSR_Scene_TheCrewThatAskedNoNames_Prose}He walked a line of you and picked without speaking and never once said what the work was in aid of. Every person who warned you about him was correct, and not one of them offered you anything instead.",
                        Goodwill("town_gang_leaders", 2),
                        Debt(400),
                        Gate("cs_opt_you_took_it_upward",
                            ClosedYouTookItUpward)),
                    Option("cs_opt_nobody_taught_you",
                        "{=CSR_Scene_NobodyTaughtYou_Title}Nobody, and they were right to say so",
                        "{=CSR_Scene_NobodyTaughtYou_Prose}You were not taken on by anyone, and the people saying you would come to nothing were saying the obvious. Everything you can do you found out yourself, slowly, with nobody standing there to tell you when you had it wrong.",
                        Place("home_woodland"),
                        LostYears(3),
                        Gate("cs_opt_you_could_ride_at_a_man",
                            ClosedYouCouldRideAtAMan))
                }, age: 17, severity: Severity.Defining);
        }

        #endregion

        #region Scene 6: What You Could Do

        /// <summary>Targets: Mastery. Severity: Formative.</summary>
        private static Scene BuildWhatYouCouldDo()
        {
            return new Scene("cs_scene_what_you_could_do",
                "{=CSR_Scene_WhatYouCouldDo_Title}What You Could Do",
                "{=CSR_Scene_WhatYouCouldDo_Prompt}By the time you were grown there was one thing you could do that the people around you could not, and it was the thing they came and got you for. It was not always the thing you would have picked for yourself. What was it?",
                new[]
                {
                    Option("cs_opt_you_could_shoot",
                        "{=CSR_Scene_YouCouldShoot_Title}You could put it where you were looking",
                        "{=CSR_Scene_YouCouldShoot_Prose}Other people aimed and you simply looked at the thing and it was hit, and you could never explain the difference to anybody who asked. It made you welcome in places that had nothing else to offer you.",
                        Item("hunting_bow", 1),
                        Goodwill("village_headmen", 2)),
                    Option("cs_opt_you_could_hold_a_doorway",
                        "{=CSR_Scene_YouCouldHoldADoorway_Title}You Could Hold a Doorway",
                        "{=CSR_Scene_YouCouldHoldADoorway_Prose}You could put your back somewhere narrow and stay there long after the argument for staying had run out, and men who would not stand anywhere else would stand next to you.",
                        Item("spear", 1),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_you_could_ride_at_a_man",
                        "{=CSR_Scene_YouCouldRideAtAMan_Title}You could ride at a man and not turn",
                        "{=CSR_Scene_YouCouldRideAtAMan_Prose}The animal knew before you did and neither of you ever came out of it, and people who had seen it once described it to people who had not. It is the only thing you have ever been vain about.",
                        Item("lance", 1),
                        Goodwill("culture_lords", 2)),
                    Option("cs_opt_you_could_price_a_load",
                        "{=CSR_Scene_YouCouldPriceALoad_Title}You could price a load by looking at it",
                        "{=CSR_Scene_YouCouldPriceALoad_Prose}You walked past a cart and knew what was on it and what it would fetch two valleys over, and you were right often enough that men started walking you past their carts on purpose.",
                        Goodwill("town_merchants", 2),
                        Place("nearest_town")),
                    Option("cs_opt_you_could_get_into_a_shut_room",
                        "{=CSR_Scene_YouCouldGetIntoAShutRoom_Title}You could get into a room that was shut",
                        "{=CSR_Scene_YouCouldGetIntoAShutRoom_Prose}Locks, walls, the hour of the night the watch changes: you learned all of it without meaning to and then found out what it was worth. You have never had to ask twice for work.",
                        Goodwill("town_gang_leaders", 2),
                        Place("nearest_hideout")),
                    Option("cs_opt_you_could_keep_a_hurt_man_alive",
                        "{=CSR_Scene_YouCouldKeepAHurtManAlive_Title}You could keep a hurt man alive",
                        "{=CSR_Scene_YouCouldKeepAHurtManAlive_Prose}You were the one sent for when somebody was opened up, and you did not faint and you did not hurry, and a good number of them are walking around now. None of them ever quite know how to speak to you.",
                        Goodwill("village_headmen", 2),
                        Trait("Mercy", 1))
                }, age: 22, severity: Severity.Formative);
        }

        #endregion

        #region Scene 7: What the Yard Said

        /// <summary>
        ///     Targets: Mastery. Severity: Defining. Carries a gate.
        ///
        ///     A yard describing you the morning after settles your standing, your
        ///     nerve and who thinks they owe you; it does not settle what you carry.
        ///     It used to hand out a weapon in five answers of six, and because the
        ///     life profile keeps the last weapon reached for, that made this scene
        ///     rather than scene 6 decide what the character's hands knew. The one
        ///     object left here is the thing off the bench, which is what that
        ///     answer is about rather than a weapon anybody chose.
        /// </summary>
        private static Scene BuildWhatTheYardSaid()
        {
            return new Scene("cs_scene_what_the_yard_said",
                "{=CSR_Scene_WhatTheYardSaid_Title}What the Yard Said",
                "{=CSR_Scene_WhatTheYardSaid_Prompt}It came at night and there was no order to any of it. In the morning the people you had been standing with had already settled on what you were, and you heard it across a yard, told to somebody who had not been there. What did they say about you?",
                new[]
                {
                    Option("cs_opt_he_does_not_stop",
                        "{=CSR_Scene_HeDoesNotStop_Title}That You Did Not Stop",
                        "{=CSR_Scene_HeDoesNotStop_Prose}They said it with something in their voices that was not entirely admiration, and one of them said it twice. You had not known until you heard it that you had gone further into that passage than anybody else would have.",
                        Trait("Valor", 1),
                        Trait("Mercy", -1),
                        Goodwill("town_gang_leaders", 2)),
                    Option("cs_opt_he_held_the_passage",
                        "{=CSR_Scene_HeHeldThePassage_Title}That Nothing Got Past You",
                        "{=CSR_Scene_HeHeldThePassage_Prose}They described the doorway and how long it had been and who had been behind it, and they did not mention your name until the end, when they did not need to. It is the only time anyone has said that about you.",
                        Goodwill("culture_lords", 2),
                        Goodwill("town_gang_leaders", 2)),
                    Option("cs_opt_he_was_up_where_it_was_safe",
                        "{=CSR_Scene_HeWasUpWhereItWasSafe_Title}That you had been up where it was safe",
                        "{=CSR_Scene_HeWasUpWhereItWasSafe_Prose}They were right about where you were and wrong about everything you did from there, and you stood in the shadow and let them finish. You have never bothered to correct that one anywhere.",
                        Trait("Calculating", 1),
                        Enmity("culture_lords", 1),
                        Place("nearest_castle")),
                    Option("cs_opt_he_came_round_the_outside_mounted",
                        "{=CSR_Scene_HeCameRoundTheOutsideMounted_Title}That you had come around the outside",
                        "{=CSR_Scene_HeCameRoundTheOutsideMounted_Prose}They told it as the part that ended the thing, which it was, and the telling had gotten better already by the morning. Somebody in that yard repeated it where it did you good.",
                        Goodwill("culture_lords", 1),
                        Place("open_country"),
                        Goodwill("town_merchants", 2)),
                    Option("cs_opt_he_fought_with_what_was_on_the_bench",
                        "{=CSR_Scene_HeFoughtWithWhatWasOnTheBench_Title}That you had fought with something that was not a weapon",
                        "{=CSR_Scene_HeFoughtWithWhatWasOnTheBench_Prose}They laughed about it and then stopped laughing, because two of them had seen what it did. You had picked up the heaviest thing within reach and it had only needed to work once each time.",
                        Item("two_handed_axe", 1),
                        Goodwill("town_gang_leaders", 1)),
                    Option("cs_opt_he_was_not_in_the_yard",
                        "{=CSR_Scene_HeWasNotInTheYard_Title}That you had not been in the yard at all",
                        "{=CSR_Scene_HeWasNotInTheYard_Prose}You had been in the rooms carrying out the ones who could not carry themselves, and the men who count fights do not count that. Some of those people are alive now and none of them were in the yard to say so.",
                        Ally("the_one_you_carried"),
                        Trait("Valor", -1),
                        Gate("cs_opt_took_it_and_kept_it",
                            ClosedTookItAndKeptItLeftAWall))
                }, age: 23, severity: Severity.Defining);
        }

        #endregion

        #region Scene 8: The Verdict

        /// <summary>Targets: Hinge. Severity: Defining. Carries gates.</summary>
        private static Scene BuildTheVerdict()
        {
            return new Scene("cs_scene_the_verdict",
                "{=CSR_Scene_TheVerdict_Title}The Verdict",
                "{=CSR_Scene_TheVerdict_Prompt}A man you knew was judged in front of you for something he had not done, and it was carried out the same afternoon. Everybody in that square who knew the truth of it said nothing, including the one man who could have ended it in a sentence. You have had years to decide who was actually in the wrong that day.",
                new[]
                {
                    Option("cs_opt_you_took_it_upward",
                        "{=CSR_Scene_YouTookItUpward_Title}The man who passed it, and you said so upward",
                        "{=CSR_Scene_YouTookItUpward_Prose}You put it on the man in the chair and you took it over his head, and then over that head, and it cost you the better part of a year and most of what you had. It worked, partly, and far too late to be worth it.",
                        Trait("Honor", 1),
                        Goodwill("culture_lords", 1),
                        Debt(400),
                        LostYears(1)),
                    Option("cs_opt_the_silent_one_was_the_worst_of_them",
                        "{=CSR_Scene_TheSilentOneWasTheWorstOfThem_Title}The one who said nothing, and you never spoke to him again",
                        "{=CSR_Scene_TheSilentOneWasTheWorstOfThem_Prose}You decided the man who could have ended it and did not was worse than the man who passed it, and you said so in front of the people whose opinion he lived on. You have not spoken a word to him since and you will not.",
                        Enmity("culture_lords", 1),
                        Goodwill("village_headmen", 2)),
                    Option("cs_opt_the_crowd_was_in_the_wrong",
                        "{=CSR_Scene_TheCrowdWasInTheWrong_Title}The crowd, and you were standing in it",
                        "{=CSR_Scene_TheCrowdWasInTheWrong_Prose}You have never decided the man in the chair was the problem, because a whole square watched it and you were one of them. You have a list now: everybody on it was standing there, and none of them know they are on it.",
                        Enmity("town_merchants", 1),
                        Goodwill("town_gang_leaders", 1),
                        Place("nearest_town")),
                    Option("cs_opt_he_had_earned_something_and_not_this",
                        "{=CSR_Scene_HeHadEarnedSomethingAndNotThis_Title}The Dead Man, Partly",
                        "{=CSR_Scene_HeHadEarnedSomethingAndNotThis_Prose}He had not done the thing they judged him for and he had done others, and you are the only person who says both halves of that out loud. You carried his household through two winters anyway, and you borrowed to do it.",
                        Goodwill("village_headmen", 1),
                        Debt(800),
                        Trait("Generosity", 1)),
                    Option("cs_opt_you_were_in_the_wrong_for_standing_there",
                        "{=CSR_Scene_YouWereInTheWrongForStandingThere_Title}You were, for standing there, and you did not stand there twice",
                        "{=CSR_Scene_YouWereInTheWrongForStandingThere_Prose}You went through the people in front of you and took him down off it, hours too late for it to matter to him. You did not sleep under a roof you were welcome in for a long time after that.",
                        Enmity("culture_lords", 2),
                        Place("nearest_hideout"),
                        Gate("cs_opt_took_it_and_kept_it",
                            ClosedTookItAndKeptItWrittenDown),
                        Gate("cs_opt_the_name_of_a_house",
                            ClosedTheNameOfAHouse)),
                    Option("cs_opt_nobody_was_that_is_how_it_works",
                        "{=CSR_Scene_NobodyWasThatIsHowItWorks_Title}Nobody was. That is how it works, and you left",
                        "{=CSR_Scene_NobodyWasThatIsHowItWorks_Prose}You decided nothing had gone wrong that afternoon except that you had been standing close enough to see it. You were on a road inside the hour and you have not stayed anywhere long enough to see a second one since.",
                        LostYears(3),
                        Place("open_country"))
                }, age: 26, severity: Severity.Defining);
        }

        #endregion

        #region Scene 9: What the Town Said

        /// <summary>
        ///     Targets: Hinge, Intent. Severity: Defining.
        ///
        ///     Every answer here is a whole town's settled verdict on a man who
        ///     lived in it for years, so each carries a standing with somebody as
        ///     well as what it says about him. It used to weigh less than scene 1,
        ///     which asks what furniture a house had.
        /// </summary>
        private static Scene BuildWhatTheTownSaid()
        {
            return new Scene("cs_scene_what_the_town_said",
                "{=CSR_Scene_WhatTheTownSaid_Title}What the Town Said",
                "{=CSR_Scene_WhatTheTownSaid_Prompt}You had been years in the same town by then, standing out in the same cold with the same people every morning, waiting on work. A man came through looking for somebody who could do a particular kind of thing, and the ones you stood beside every morning answered him at length, close enough to touch, believing you had already gone out with the early cart. What had the place you were living in decided you were for?",
                new[]
                {
                    Option("cs_opt_they_said_you_were_the_one_they_sent",
                        "{=CSR_Scene_TheySaidYouWereTheOneTheySent_Title}That you were the one they sent",
                        "{=CSR_Scene_TheySaidYouWereTheOneTheySent_Prose}They went through the things that had left that town in your hands and come back settled, in order, and the man asking stopped them before they were finished because he had heard enough. None of it was work you had ever put yourself forward for.",
                        Goodwill("town_merchants", 2),
                        Place("nearest_castle")),
                    Option("cs_opt_they_said_you_were_already_spoken_for",
                        "{=CSR_Scene_TheySaidYouWereAlreadySpokenFor_Title}That you were already spoken for",
                        "{=CSR_Scene_TheySaidYouWereAlreadySpokenFor_Prose}They told him not to waste the morning on you, because whoever stands the way you stand is being kept by somebody further up, and that keeping is never written anywhere you can read it. You had been nobody's for years and had not known you were wearing it.",
                        Goodwill("culture_lords", 2),
                        Place("nearest_castle")),
                    Option("cs_opt_they_said_you_were_not_to_be_pushed",
                        "{=CSR_Scene_TheySaidYouWereNotToBePushed_Title}That you were not to be pushed",
                        "{=CSR_Scene_TheySaidYouWereNotToBePushed_Prose}They warned him about you the way people warn a stranger about a dog, with the same care in it and about as much liking, and one of them had a story ready that had never happened. You have let that story go around for years because of what it saves you.",
                        Goodwill("town_gang_leaders", 1),
                        Trait("Mercy", -1),
                        Enmity("town_merchants", 1)),
                    Option("cs_opt_they_said_you_were_owed",
                        "{=CSR_Scene_TheySaidYouWereOwed_Title}That You Were Owed",
                        "{=CSR_Scene_TheySaidYouWereOwed_Prose}One voice came over the top of all the others with an account of a winter nobody else had thought to bring up, and would not be talked down off it until the rest had gone quiet. The one who said it came and found you that evening and has not gone anywhere since.",
                        Ally("the_one_who_spoke_for_you"),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_they_said_you_came_cheap",
                        "{=CSR_Scene_TheySaidYouCameCheap_Title}That You Came Cheap",
                        "{=CSR_Scene_TheySaidYouCameCheap_Prose}They put a price on you out loud, the way a price is said over a thing lying on a bench, and it was under what you had been quietly telling yourself for years. He paid it without arguing, and that was the worse half of the morning.",
                        Item("coin_pouch", 1),
                        Trait("Honor", -1),
                        Place("nearest_town")),
                    Option("cs_opt_they_said_it_had_come_in_with_you",
                        "{=CSR_Scene_TheySaidItHadComeInWithYou_Title}That whatever you had left had come in with you",
                        "{=CSR_Scene_TheySaidItHadComeInWithYou_Prose}They told him the town had been a quieter place in the years before you walked into it, and then they told him why they thought so, and most of the reasons they gave belonged to other people. You stood where you were until the crowd moved and went out with it.",
                        Enmity("town_merchants", 2),
                        Place("nearest_hideout"),
                        Goodwill("town_gang_leaders", 2))
                }, age: 29, severity: Severity.Defining);
        }

        #endregion

        #region Scene 9, with War Sails: What the Port Said

        /// <summary>
        ///     Targets: Hinge, Intent. Severity: Defining.
        ///
        ///     The same slot, the same question and the same weight as the town
        ///     scene above, put in a world that has water in it. It is the one
        ///     place the guided route can spend years on the sea, and two of its
        ///     six answers do: <c>the_open_water</c> is the only target in the
        ///     whole catalog that scores <see cref="Services.LifeProfile.Lean.Sea"/>,
        ///     which is what keeps a life answered any other way exactly as far
        ///     from the three War Sails skills as it was before the DLC was
        ///     installed.
        ///
        ///     It replaces rather than joins. A fourteenth scene would have to be
        ///     named in the flow table to be reached at all, and every owner of
        ///     the DLC would walk one situation more than everybody else for the
        ///     sake of a question the ninth scene was already asking.
        /// </summary>
        private static Scene BuildWhatThePortSaid()
        {
            return new Scene("cs_scene_what_the_town_said",
                "{=CSR_Scene_WhatThePortSaid_Title}What the Port Said",
                "{=CSR_Scene_WhatThePortSaid_Prompt}The season's hiring was done off the same stretch of boards every year, and you had stood on it long enough that nobody looked at you twice. A master came down it wanting one particular kind of man, and the ones you had stood beside all those mornings told him what you were at length, with you close enough behind them to touch, because they had you out on the tide already. What had that front decided you were for?",
                new[]
                {
                    Option("cs_opt_they_said_you_were_worth_keeping",
                        "{=CSR_Scene_TheySaidYouWereWorthKeeping_Title}That You Were Worth Keeping",
                        "{=CSR_Scene_TheySaidYouWereWorthKeeping_Prose}They counted off the seasons you had been out and what had come back with you each time, in order, and the master stopped them halfway down the list because he had heard the part he came for. Not one of them mentioned a crossing you had turned back from, because in all those years there had not been one. Nobody mentioned the villages either, which had stopped counting you as one of theirs somewhere in the middle of it.",
                        Place("the_open_water", 5),
                        Trait("Mercy", -1),
                        Enmity("village_headmen", 1)),
                    Option("cs_opt_they_said_you_went_out_anyway",
                        "{=CSR_Scene_TheySaidYouWentOutAnyway_Title}That you went out when nobody else would",
                        "{=CSR_Scene_TheySaidYouWentOutAnyway_Prose}They named two runs from that year that nobody else on those boards had put a mark against, and then they named what had been aboard, and the second name was said quieter than the first. You had not asked either time what was under the covers, and you had been paid as though you had.",
                        Place("the_open_water", 3),
                        Goodwill("town_gang_leaders", 2)),
                    Option("cs_opt_they_said_you_had_come_ashore",
                        "{=CSR_Scene_TheySaidYouHadComeAshore_Title}That You Had Come Ashore",
                        "{=CSR_Scene_TheySaidYouHadComeAshore_Prose}They told him you had been worth something once and gave the year you stopped going out, and then they gave a reason for it that was not the reason. You have let that stand for years, because the true one is worth less to you than what you came ashore holding.",
                        Item("coin_pouch", 2),
                        Trait("Honor", -1),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_they_said_you_priced_everything",
                        "{=CSR_Scene_TheySaidYouPricedEverything_Title}That You Priced Everything",
                        "{=CSR_Scene_TheySaidYouPricedEverything_Prose}One of them said you could walk the length of a deck and put a figure on what was under the covers without lifting one of them, and nobody in that crowd argued, and two of them looked at the boards. You have said the figure wrong on purpose twice, both times for yourself, and the men who need a figure said quietly have never once been able to buy one off you.",
                        Goodwill("town_merchants", 2),
                        Item("trade_goods", 1),
                        Goodwill("culture_lords", 1)),
                    Option("cs_opt_they_said_somebody_there_owed_you",
                        "{=CSR_Scene_TheySaidSomebodyThereOwedYou_Title}That Somebody There Owed You",
                        "{=CSR_Scene_TheySaidSomebodyThereOwedYou_Prose}One voice came up over the others with an account of a night three winters back that none of the rest had thought worth raising, and would not be argued down off it until they went quiet. He found you that evening, he has not gone anywhere since, and the men who run that front have not forgotten which way he spoke.",
                        Ally("the_one_who_spoke_for_you"),
                        Enmity("town_gang_leaders", 1)),
                    Option("cs_opt_they_said_the_front_had_changed",
                        "{=CSR_Scene_TheySaidTheFrontHadChanged_Title}That the front had been quieter before you",
                        "{=CSR_Scene_TheySaidTheFrontHadChanged_Prose}They told him what those boards had been like in the years before you first came up them, and then they went through why they thought so, and most of what they listed belonged to other men. You stood where you were until the line moved, and then you went up it with everybody else.",
                        Enmity("town_merchants", 2),
                        Place("nearest_hideout"),
                        Goodwill("town_gang_leaders", 1))
                }, age: 29, severity: Severity.Defining);
        }

        #endregion

        #region Scene 10: The Seat

        /// <summary>Targets: Intent. Severity: Defining.</summary>
        private static Scene BuildTheSeat()
        {
            return new Scene("cs_scene_the_seat",
                "{=CSR_Scene_TheSeat_Title}The Seat",
                "{=CSR_Scene_TheSeat_Prompt}A man with something real to give offered it to you across a table, with a condition attached that he did not say out loud and you both understood. Everybody who heard about it afterward had a view of him, and most of them gave you theirs whether or not you asked. What was he, and what did you do about it?",
                new[]
                {
                    Option("cs_opt_took_it_and_kept_it",
                        "{=CSR_Scene_TookItAndKeptIt_Title}He was exactly what he said he was, and you are still his",
                        "{=CSR_Scene_TookItAndKeptIt_Prose}You said yes and then you said the words that went with the yes, and you have not broken them once in the years since. It closed every other road you might have taken and you have not looked down one of them.",
                        Title("sworn_of_the_house"),
                        Place("granted_holding")),
                    Option("cs_opt_he_was_buying_cheap",
                        "{=CSR_Scene_HeWasBuyingCheap_Title}He was buying a man cheap, and you let him think he had",
                        "{=CSR_Scene_HeWasBuyingCheap_Prose}You said yes and took what came with the yes, and then you did the other thing anyway. He found out in his own time and there is nothing left for him to do about it except tell the story his way.",
                        Title("oathbreaker"),
                        Enmity("culture_lords", 2),
                        Item("coin_pouch", 2),
                        Gate("cs_opt_the_name_of_a_house",
                            ClosedTheNameOfAHouse)),
                    Option("cs_opt_he_was_honest_and_you_said_the_part_he_had_not",
                        "{=CSR_Scene_HeWasHonestAndYouSaidThePartHeHadNot_Title}He was honest, which is why you said the part he had not",
                        "{=CSR_Scene_HeWasHonestAndYouSaidThePartHeHadNot_Prose}You named the condition out loud across his own table, and then you thanked him and went out. He has told that story a number of times since and he does not tell it against you.",
                        Goodwill("culture_lords", 1),
                        Trait("Calculating", -1),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_he_was_smaller_than_he_thought",
                        "{=CSR_Scene_HeWasSmallerThanHeThought_Title}He was smaller than he thought he was",
                        "{=CSR_Scene_HeWasSmallerThanHeThought_Prose}You let him finish and then told him what you actually wanted, which was a long way above what he had brought to the table. He laughed, and then he stopped laughing, and then he asked you to say it again.",
                        Title("the_one_who_asked"),
                        Enmity("culture_lords", 1)),
                    Option("cs_opt_he_was_a_fool_and_you_took_the_portable_part",
                        "{=CSR_Scene_HeWasAFoolAndYouTookThePortablePart_Title}He was a fool, and you took the part you could carry",
                        "{=CSR_Scene_HeWasAFoolAndYouTookThePortablePart_Prose}You turned down the thing he was actually offering and asked instead for the small movable version of it, and he agreed, a little insulted. You were three valleys away before the season turned.",
                        Item("coin_pouch", 2)),
                    Option("cs_opt_he_was_nothing_to_you",
                        "{=CSR_Scene_HeWasNothingToYou_Title}He was nobody, and you did not answer him",
                        "{=CSR_Scene_HeWasNothingToYou_Prose}You finished the cup, stood up, and went out without giving him a word either way. He has not made the offer a second time and you have not been back through that town.",
                        LostYears(1),
                        Place("open_country"))
                }, age: 32, severity: Severity.Defining);
        }

        #endregion

        #region Scene 11: The Purse

        /// <summary>Targets: Intent (starting means). Severity: Defining.</summary>
        private static Scene BuildThePurse()
        {
            return new Scene("cs_scene_the_purse",
                "{=CSR_Scene_ThePurse_Title}The Purse",
                "{=CSR_Scene_ThePurse_Prompt}You came into more coin at once than you had ever held, honestly enough, and it was gone inside a month. One thing you turned it into is still with you and you would not part with it. What is it?",
                new[]
                {
                    Option("cs_opt_a_horse_and_a_harness",
                        "{=CSR_Scene_AHorseAndAHarness_Title}The Animal",
                        "{=CSR_Scene_AHorseAndAHarness_Prose}You spent it on one horse and the gear to keep it, and everybody told you it was far too much money for a horse. It was, and you would do it again tomorrow.",
                        Item("war_horse", 1),
                        Item("riding_tack", 1)),
                    Option("cs_opt_arms_and_mail",
                        "{=CSR_Scene_ArmsAndMail_Title}The Iron",
                        "{=CSR_Scene_ArmsAndMail_Prose}You bought what you would be wearing and what you would be holding, and you bought it good rather than plentiful. You have not had to replace any of it and you do not expect to.",
                        Item("mail_hauberk", 1),
                        Item("one_handed_sword", 1)),
                    Option("cs_opt_a_wagon_and_a_load",
                        "{=CSR_Scene_AWagonAndALoad_Title}The Load",
                        "{=CSR_Scene_AWagonAndALoad_Prose}You put every coin of it into goods and the animals to carry them, which left you poorer that night than you had been that morning. You were not poorer by the end of the season.",
                        Item("pack_mule", 2),
                        Item("trade_goods", 1),
                        Goodwill("town_merchants", 2)),
                    Option("cs_opt_men_who_would_come",
                        "{=CSR_Scene_MenWhoWouldCome_Title}The men, and what they carry",
                        "{=CSR_Scene_MenWhoWouldCome_Prose}You paid men a season in advance, which nobody does, and they came, which nobody expected. You have owed somebody else for that season ever since and they have not forgotten it.",
                        Ally("the_paid_men"),
                        Debt(600),
                        Goodwill("town_gang_leaders", 1)),
                    Option("cs_opt_a_writ_and_a_name",
                        "{=CSR_Scene_AWritAndAName_Title}The Paper",
                        "{=CSR_Scene_AWritAndAName_Prose}You spent it on a document and on the men whose signatures make a document mean anything. It looks like nothing in your hand and it opens gates that iron does not.",
                        Title("holder_of_the_writ"),
                        Goodwill("culture_lords", 2)),
                    Option("cs_opt_you_sent_it_home",
                        "{=CSR_Scene_YouSentItHome_Title}Nothing. It went where it was needed",
                        "{=CSR_Scene_YouSentItHome_Prose}You kept enough to eat on and sent the rest home to your mother and father, who are both living and both still in the house you were small in, and you attached no letter because there was nothing in it to explain. They have not spent it and they will not.",
                        Written(
                            Instead("the-life-buried-the-father", BuriedTheFather,
                                "{=CSR_Scene_YouSentItHome_Prose_Mother}You kept enough to eat on and sent the rest home to your mother, who has held that house on her own since your father went into the ground, and you attached no letter because there was nothing in it to explain. She has not spent it and she will not."),
                            Instead("the-life-buried-the-mother", BuriedTheMother,
                                "{=CSR_Scene_YouSentItHome_Prose_Father}You kept enough to eat on and sent the rest home to your father, who has been alone in that house since your mother went into the ground, and you attached no letter because there was nothing in it to explain. He has not spent it and he will not."),
                            Instead("the-life-buried-them-both", BuriedThemBoth,
                                "{=CSR_Scene_YouSentItHome_Prose_Both}You kept enough to eat on and sent the rest back to the ones who raised you after your mother and your father went into the ground, and you attached no letter because there was nothing in it to explain. It has not been spent and it will not be.")),
                        Goodwill("village_headmen", 1),
                        Trait("Generosity", 1),
                        Place("home_village"),
                        Household("parents_still_standing"))
                }, age: 34, severity: Severity.Defining);
        }

        #endregion

        #region Scene 12: The Winter Between

        /// <summary>
        ///     Targets: Seasoning. Severity: Habit.
        ///
        ///     The lightest scene in the run and the only one that answers to that
        ///     label: what a man did with seasons nobody was paying for colors the
        ///     telling and costs him time, and settles less of him than any scene
        ///     around it. Light is not the same as flat, though, and it used to be
        ///     both: the narrow answer and the broad one were one trait point apart
        ///     in the scene whose whole job is to tell them apart.
        ///
        ///     One answer costs no years at all. Every one of the six used to, which
        ///     put a floor under how old a finished character could be and made
        ///     <see cref="Models.StartingAge.Young"/> unreachable however carefully a
        ///     player answered the rest: a scene charging the same kind on every
        ///     answer is not asking about that kind, it is levying it. A winter
        ///     somebody else paid for takes nothing out of a life, and pays instead
        ///     in an open account and in what a man is while he is being kept.
        ///
        ///     The marriage is here and nowhere else. Three household chapters ask
        ///     who raised this character, who was raised beside them and who is
        ///     theirs now, and every one of those questions the player leaves alone
        ///     is answered by the life: the first two the run could always answer
        ///     and the third it never could, so a spouse on this route only ever
        ///     came from a player overruling their own life.
        ///     <see cref="Menus.HouseholdMenu"/> reads this one answer for it. It
        ///     belongs to a scene rather than to a chapter because the chapters ask
        ///     and the scenes tell, and it belongs to THIS scene because these are
        ///     the only seasons in the run that are the character's own: everywhere
        ///     else they are being taught, judged, paid or named. One answer says
        ///     it, since a run where every life married would be as untrue as the
        ///     run where none of them could.
        /// </summary>
        private static Scene BuildTheWinterBetween()
        {
            return new Scene("cs_scene_the_winter_between",
                "{=CSR_Scene_TheWinterBetween_Title}The Winter Between",
                "{=CSR_Scene_TheWinterBetween_Prompt}There came seasons with nothing in them: no employer, no war, no road you had to be on. They were entirely yours and they came around more than once. What did you do with them?",
                new[]
                {
                    Option("cs_opt_one_thing_until_it_was_right",
                        "{=CSR_Scene_OneThingUntilItWasRight_Title}One Thing, Every Day",
                        "{=CSR_Scene_OneThingUntilItWasRight_Prose}You picked the single thing you were best at and did it every day until you were better at it than the man who taught it to you. You got worse at everything else in the same months and you knew it at the time.",
                        LostYears(2),
                        Trait("Generosity", -1),
                        Goodwill("town_merchants", 1)),
                    Option("cs_opt_whatever_work_came",
                        "{=CSR_Scene_WhateverWorkCame_Title}Whatever Was Offered",
                        "{=CSR_Scene_WhateverWorkCame_Prose}You said yes to everything that came that season and none of it was related to any of the rest. You can now begin most jobs competently and finish none of them beautifully.",
                        LostYears(1),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_you_taught_it",
                        "{=CSR_Scene_YouTaughtIt_Title}You spent them on somebody else",
                        "{=CSR_Scene_YouTaughtIt_Prose}You spent the months on somebody younger who wanted to know what you knew, and you found out how much of it you had never put into words. They are still with you and they ask better questions now.",
                        Ally("the_apprentice"),
                        LostYears(1)),
                    Option("cs_opt_you_married_that_winter",
                        "{=CSR_Scene_YouMarriedThatWinter_Title}You Married That Winter",
                        "{=CSR_Scene_YouMarriedThatWinter_Prose}You went into one of those seasons with nobody to answer to and came out of it with a household, and the whole of it was settled between two families in the weeks the weather took to turn. Somebody has been waiting on word from you ever since, and nobody was before.",
                        Goodwill("village_headmen", 2),
                        Debt(400),
                        Household("a_spouse")),
                    Option("cs_opt_you_drank_them",
                        "{=CSR_Scene_YouDrankThem_Title}You Spent Them Badly",
                        "{=CSR_Scene_YouDrankThem_Prose}They went, several of them, and there is very little to say about where. You came out of it owing money to people who are patient in a way you do not like.",
                        LostYears(3),
                        Debt(500),
                        Goodwill("town_gang_leaders", 1)),
                    Option("cs_opt_you_walked",
                        "{=CSR_Scene_YouWalked_Title}You Spent Them on Roads",
                        "{=CSR_Scene_YouWalked_Prose}You went out with no destination and came back when the weather turned, more than once. You know the ground between four towns better than the couriers who are paid to know it.",
                        LostYears(2),
                        Place("open_country")),
                    Option("cs_opt_you_wintered_at_another_mans_fire",
                        "{=CSR_Scene_YouWinteredAtAnotherMansFire_Title}You spent them at another man's fire",
                        "{=CSR_Scene_YouWinteredAtAnotherMansFire_Prose}There was a hall that took you in whenever the weather turned, and you went in with nothing and came out in the spring no further behind than you had gone in. Nobody in that house ever said out loud what the arrangement was, and you never asked, and it is open still.",
                        Goodwill("culture_lords", 1),
                        Debt(400)),
                    Option("cs_opt_you_healed",
                        "{=CSR_Scene_YouHealed_Title}You Spent Them Getting Well",
                        "{=CSR_Scene_YouHealed_Prose}Something took the whole of one season and most of the next, and for a while it was not certain you were getting up from it. You did, slower, and with a better idea of what a body will take.",
                        LostYears(3),
                        Place("home_village"))
                }, age: 36, severity: Severity.Habit);
        }

        #endregion

        #region Scene 13: The Name They Use

        /// <summary>Targets: Seasoning. Severity: Defining. Final scene.</summary>
        private static Scene BuildTheNameTheyUse()
        {
            return new Scene("cs_scene_the_name_they_use",
                "{=CSR_Scene_TheNameTheyUse_Title}The Name They Use",
                "{=CSR_Scene_TheNameTheyUse_Prompt}In a common room where nobody had noticed you come in behind them, you heard yourself described. They did not use your given name for it. You had evidently been called that for a while without once hearing anybody say it.",
                new[]
                {
                    Option("cs_opt_the_name_of_a_trade",
                        "{=CSR_Scene_TheNameOfATrade_Title}They named you by the work",
                        "{=CSR_Scene_TheNameOfATrade_Prose}They used the thing you do rather than the family you come from, and they used it as though there were no question who was meant. Nobody in that room could have told you where you were born.",
                        Title("by_the_trade"),
                        Goodwill("town_merchants", 2),
                        LostYears(1)),
                    Option("cs_opt_the_name_of_a_house",
                        "{=CSR_Scene_TheNameOfAHouse_Title}They put a house in front of it",
                        "{=CSR_Scene_TheNameOfAHouse_Prose}They said a great name and then yours after it, in that order, and nobody at the table found the pairing strange. You had never heard the two spoken together before that evening.",
                        Title("of_the_house"),
                        Goodwill("culture_lords", 2)),
                    Option("cs_opt_the_name_with_a_price_on_it",
                        "{=CSR_Scene_TheNameWithAPriceOnIt_Title}They named the price on you",
                        "{=CSR_Scene_TheNameWithAPriceOnIt_Prose}They were not describing you so much as quoting you, and the figure had gone up since the last time it was posted. Two of them wanted to go looking and the third talked them out of it.",
                        Title("wanted"),
                        Enmity("culture_lords", 2),
                        Goodwill("town_gang_leaders", 2)),
                    Option("cs_opt_the_old_one",
                        "{=CSR_Scene_TheOldOne_Title}They Called You Old",
                        "{=CSR_Scene_TheOldOne_Prose}They meant it kindly and they were not wrong, and it was the first time anyone had said it where you could hear. You had buried your mother and your father both by then and had still not counted yourself among the old, and you thought about that on the road for a week and then stopped thinking about it.",
                        Written(
                            Instead("the-life-says-both-parents-living", BothParentsLiving,
                                "{=CSR_Scene_TheOldOne_Prose_Living}They meant it kindly and they were not wrong, and it was the first time anyone had said it where you could hear. Your mother and your father were both living still, in the house you were small in, and you had gone grey ahead of the pair of them, and you thought about that on the road for a week and then stopped thinking about it.")),
                        Title("the_old"),
                        Goodwill("village_headmen", 2),
                        LostYears(4),
                        Household("parents_by_then")),
                    Option("cs_opt_the_name_of_a_place",
                        "{=CSR_Scene_TheNameOfAPlace_Title}They named you by where you are from",
                        "{=CSR_Scene_TheNameOfAPlace_Prose}They used the valley rather than the man, and they said it the way people say a place they have heard things about. Nobody in that room had been within four days of it.",
                        Title("of_the_place"),
                        Place("home_village"),
                        Goodwill("village_headmen", 1)),
                    Option("cs_opt_no_name_at_all",
                        "{=CSR_Scene_NoNameAtAll_Title}They could not finish the sentence",
                        "{=CSR_Scene_NoNameAtAll_Prose}They described your coat, and then the road you had come in off, and then they gave up, because nobody had settled on anything for you yet. You stood in the doorway a moment longer than you needed to.",
                        LostYears(2),
                        Goodwill("town_gang_leaders", 1),
                        Place("open_country"))
                }, age: null, severity: Severity.Defining);
        }

        #endregion
    }

    /// <summary>
    ///     The graves "They Called You Old" finds already dug, and the ones it
    ///     digs itself.
    ///
    ///     The answer is about being called old, which stands whatever the life
    ///     behind it says, so it is never taken off the table; what it SETTLES is
    ///     read off that life instead. A run that has already said both parents
    ///     are living is not corrected by it, a run that buried one of them has
    ///     the other settled, and a run that buried both or never spoke of them
    ///     has both settled, which is what the answer always did.
    ///
    ///     Pure, and asked rather than rolled, for the reason <c>StorySibling</c>
    ///     is: the effect panel states the fact this answer is about to settle,
    ///     the household composes that same fact, and the two cannot be allowed
    ///     to reach different readings of one life. It is also why the catalog
    ///     declares one target that stands for the three rather than three
    ///     answers the player would have to choose between.
    /// </summary>
    public static class StoryGraves
    {
        /// <summary>The one answer whose parent fact is read off the life that reached it.</summary>
        public const string DeclaredBy = "cs_opt_the_old_one";

        /// <summary>The household fact it declares, which stands for the three below and for none of them.</summary>
        public const string Declares = "parents_by_then";

        /// <summary>The same question asked of a whole run rather than of the facts it left.</summary>
        public static string? Of(ISceneAnswers? answers) => Settled(HouseOfTheLife.Said(answers));

        /// <summary>
        ///     What this answer settles, given everything the life has said about
        ///     the house, in the order it said it. Null where the life has already
        ///     said both parents are living, which is the one reading under which
        ///     this answer speaks to neither of them.
        /// </summary>
        public static string? Settled(IEnumerable<string>? said)
        {
            var (father, mother, spoken) = HouseOfTheLife.Parents(said, upToTheCoin: false);

            if (!spoken) return "both_parents_buried";
            if (father && mother) return null;
            if (!father && !mother) return "both_parents_buried";

            return father ? "father_buried" : "mother_buried";
        }
    }

    /// <summary>
    ///     The parents the coin was sent home to, which is the second fact read off
    ///     the life rather than carried by the answer that declares it.
    ///
    ///     "Nothing. It went where it was needed" used to declare a flat
    ///     <c>both_parents_living</c>, and the fold takes a later answer as
    ///     overruling an earlier one, so on any life that had already buried a
    ///     parent it stood them back up: the scene that put them in the ground went
    ///     quietly untrue and nothing on screen disagreed, which is the worse
    ///     direction of a panel reporting what is not real. Taking the answer off the
    ///     table is the one thing not allowed, so what it settles moves instead. Where
    ///     nothing has been said about the parents it says both are living, which
    ///     is what the answer always did; where the life has already buried one of
    ///     them or both, it speaks to neither and the coin goes to whoever is left.
    ///
    ///     Its prose is written once per state beside this, because an answer that
    ///     no longer declares two living parents may not go on describing them.
    /// </summary>
    public static class StoryHome
    {
        /// <summary>The one answer whose parent fact is read off the life that reached it.</summary>
        public const string DeclaredBy = "cs_opt_you_sent_it_home";

        /// <summary>The household fact it declares, which stands for one fact and for none.</summary>
        public const string Declares = "parents_still_standing";

        /// <summary>The same question asked of a whole run rather than of the facts it left.</summary>
        public static string? Of(ISceneAnswers? answers) => Settled(HouseOfTheLife.Said(answers));

        /// <summary>
        ///     What this answer settles, given what the life said about the house
        ///     before it. Null where the life has already spoken about the parents,
        ///     which is the reading under which this answer speaks to neither.
        /// </summary>
        public static string? Settled(IEnumerable<string>? said)
        {
            var (_, _, spoken) = HouseOfTheLife.Parents(said, upToTheCoin: true);
            return spoken ? null : "both_parents_living";
        }

        /// <summary>Whether each parent was still above ground when the coin was sent.</summary>
        public static (bool Father, bool Mother) Standing(ISceneAnswers? answers) =>
            Standing(HouseOfTheLife.Said(answers));

        public static (bool Father, bool Mother) Standing(IEnumerable<string>? said)
        {
            var (father, mother, _) = HouseOfTheLife.Parents(said, upToTheCoin: true);
            return (father, mother);
        }
    }

    /// <summary>
    ///     One reading of what a life has said about its parents, so the two
    ///     answers that ask and the household that composes them cannot reach
    ///     different readings of one life.
    /// </summary>
    internal static class HouseOfTheLife
    {
        /// <summary>
        ///     Everything a run has said about the house, in the order it said it.
        /// </summary>
        public static IReadOnlyList<string> Said(ISceneAnswers? answers)
        {
            var said = new List<string>();
            if (answers == null) return said;

            foreach (var consequence in SceneReading.Consequences(SceneCatalog.All, answers))
                if (consequence.Kind == ConsequenceKind.Household && consequence.Target != null)
                    said.Add(consequence.Target);

            return said;
        }

        /// <summary>
        ///     Whether each parent is above ground and whether the life has spoken
        ///     about them at all, read in the order the facts were declared.
        ///
        ///     <paramref name="upToTheCoin"/> stops at the answer that sends the
        ///     coin home, which is the only reading that answer is entitled to: a
        ///     fact declared after it belongs to a scene the player had not reached
        ///     when the coin went, and resolving against it would be reading its own
        ///     future. The last scene's grave marker is not read here at all, since
        ///     it is the last thing any life declares and <see cref="StoryGraves"/>
        ///     is what works it out.
        /// </summary>
        public static (bool Father, bool Mother, bool Spoken) Parents(
            IEnumerable<string>? said, bool upToTheCoin)
        {
            bool father = true;
            bool mother = true;
            bool spoken = false;

            if (said == null) return (father, mother, spoken);

            foreach (string target in said)
                switch (target)
                {
                    case "both_parents_living":
                        father = true;
                        mother = true;
                        spoken = true;
                        break;

                    case "both_parents_buried":
                        father = false;
                        mother = false;
                        spoken = true;
                        break;

                    case "father_buried":
                        father = false;
                        spoken = true;
                        break;

                    case "mother_buried":
                        mother = false;
                        spoken = true;
                        break;

                    case StoryHome.Declares:
                        if (upToTheCoin) return (father, mother, spoken);
                        if (spoken) break;

                        father = true;
                        mother = true;
                        spoken = true;
                        break;
                }

            return (father, mother, spoken);
        }
    }
}
