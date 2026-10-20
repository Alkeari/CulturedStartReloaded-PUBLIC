using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Cultured Start Revamped's age: the chapter says how old the character is
    ///     and six steps move it, each click applied the moment it lands.
    ///
    ///     The age the answers came to is where the chapter opens, and a player who
    ///     clicks nothing takes it on. A step that would carry the age past coming of
    ///     age or past <see cref="AgeCurve.Ceiling" /> is not offered while it would,
    ///     rather than offered and cut short.
    ///
    ///     Two things about the stage shape this file. A click on an option that is
    ///     already selected runs its select handler again (the option button calls
    ///     <c>CharacterCreationOptionVM.ExecuteSelect</c>, which selects without asking
    ///     whether it already is), so one option clicked five times is five steps.
    ///     And entering the menu re-selects whichever option was recorded for it
    ///     (<c>CharacterCreationNarrativeStageVM.RefreshMenu</c>), which would step the
    ///     age again every time the player walked back in. The stage evaluates every
    ///     option's condition before that replay and asks whether it can advance after
    ///     it, so a flag raised by the conditions and lowered by
    ///     <see cref="Patches.AgeAdjustStagePatch" /> tells the replay from a click.
    /// </summary>
    public static class AgeAdjustMenu
    {
        public const string MenuId = "cs_age_adjust_menu";

        /// <summary>The six steps, in the order the chapter lists them.</summary>
        private static readonly (string Id, int Years, string TitleKey)[] Steps =
        {
            ("cs_age_adjust_younger_1", -1, "{=CSR_AgeAdjust_Younger1}Become a Year Younger"),
            ("cs_age_adjust_older_1", 1, "{=CSR_AgeAdjust_Older1}Grow a Year Older"),
            ("cs_age_adjust_younger_5", -5, "{=CSR_AgeAdjust_Younger5}Become 5 Years Younger"),
            ("cs_age_adjust_older_5", 5, "{=CSR_AgeAdjust_Older5}Grow 5 Years Older"),
            ("cs_age_adjust_younger_10", -10, "{=CSR_AgeAdjust_Younger10}Become 10 Years Younger"),
            ("cs_age_adjust_older_10", 10, "{=CSR_AgeAdjust_Older10}Grow 10 Years Older")
        };

        private static TextObject? _description;

        /// <summary>True from the stage reading the options on entry until it asks whether it can advance.</summary>
        private static bool _entering;

        /// <summary>A step landed, so the options and the age line are read again on the next frame.</summary>
        private static bool _refreshPending;

        public static void AddAgeAdjustMenu(CharacterCreationManager manager)
        {
            _description = new TextObject("{=CSR_AgeAdjust_Now}Your age is currently: {AGE}");
            RefreshDescription();

            var menu = new NarrativeMenu(
                MenuId,
                CreationFlow.DeclaredPrevious(MenuId),
                CreationFlow.DeclaredNext(MenuId),
                new TextObject("{=CSR_AgeAdjust_Title}Your Years"),
                _description,
                CharacterPreviewHelper.CreatePlayerCharacter(MenuId),
                GetPlayerCharacterArgs
            );

            foreach (var step in Steps)
                AddStep(menu, step.Id, step.Years, step.TitleKey);

            manager.AddNewMenu(menu);
        }

        private static void AddStep(NarrativeMenu menu, string id, int years, string titleKey)
        {
            ChoiceEffects.Declare(id, () => Effect(years));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                new TextObject(years < 0
                    ? "{=CSR_AgeAdjust_Younger_Desc}Fewer years behind you, and less learned in them."
                    : "{=CSR_AgeAdjust_Older_Desc}More years behind you, and more learned in them."),
                args => { },
                m => Offered(years),
                m => Take(years),
                m => { }
            ));
        }

        private static int Floor() => GameCaps.MinAdultAge();

        /// <summary>The age the life came to, held to the ages this chapter moves between.</summary>
        private static int Derived(CharacterCreationSession session) =>
            Math.Max(Floor(), Math.Min(AgeCurve.Ceiling, (int)session.SelectedAge));

        private static int Current(CharacterCreationSession session) => session.EffectiveAge;

        /// <summary>Whether this step lands inside the bounds from the age the character is now.</summary>
        private static bool Offered(int years)
        {
            _entering = true;
            RefreshDescription();

            int to = Current(CreationSession.Current) + years;
            return to >= Floor() && to <= AgeCurve.Ceiling;
        }

        private static void Take(int years)
        {
            if (_entering) return;

            try
            {
                var session = CreationSession.Current;
                int from = Current(session);
                int to = from + years;

                // Only reachable in the frame between a step landing and the options being read again
                if (to < Floor() || to > AgeCurve.Ceiling)
                {
                    CSLogger.Info($"AgeAdjustMenu: {years:+0;-0} from {from} leaves the ages this chapter offers.");
                    _refreshPending = true;
                    return;
                }

                session.AdjustedAge = to == Derived(session) ? (int?)null : to;

                NarrativeStep.ShowTheLifeSoFar();
                RefreshDescription();
                CharacterPreviewHelper.RefreshMenuCharacters();
                _refreshPending = true;

                CSLogger.Info($"AgeAdjustMenu: the character is now {session.EffectiveAge}, " +
                              $"where the life came to {Derived(session)}.");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"AgeAdjustMenu: stepping the age by {years} failed.", ex);
            }
        }

        private static void RefreshDescription()
        {
            try
            {
                _description?.SetTextVariable("AGE", Current(CreationSession.Current));
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"AgeAdjustMenu: reading the age for the chapter failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Called by the stage asking whether it can advance: the chapter always
        ///     can, since clicking nothing keeps the age the life came to, and the ask
        ///     ends the entry the conditions began.
        /// </summary>
        internal static bool Settles(NarrativeMenu? menu)
        {
            if (menu == null || menu.StringId != MenuId) return false;

            _entering = false;
            return true;
        }

        /// <summary>
        ///     Reads the options and the age line again once a step has landed, from the
        ///     frame after the click rather than inside it, so the list is never rebuilt
        ///     under the button that is still handling its own click.
        /// </summary>
        internal static void RefreshIfPending(CharacterCreationNarrativeStageVM? stage)
        {
            if (!_refreshPending) return;
            _refreshPending = false;

            try
            {
                if (stage == null || Session.CreationStage.Manager?.CurrentMenu?.StringId != MenuId) return;

                stage.RefreshMenu();
            }
            catch (Exception ex)
            {
                CSLogger.Error("AgeAdjustMenu: reading the steps again after an age change failed.", ex);
            }
        }

        /// <summary>What this step does to the character now: the age it moves to, and the level it moves between.</summary>
        private static string Effect(int years)
        {
            var session = CreationSession.Current;
            int from = Current(session);
            int to = from + years;

            var age = new TextObject(
                "{=CSR_Panel_AgeAdjust_AgeMoved}Age: {FROM} to {TO} years when the campaign opens");
            age.SetTextVariable("FROM", from);
            age.SetTextVariable("TO", to);

            var profile = LifeProfile.From(session);
            int before = StorySkills.LevelAtAge(profile, session.AdjustedAge);
            int after = StorySkills.LevelAtAge(profile, to);

            string? level = null;
            if (before != after)
            {
                var text = new TextObject("{=CSR_Panel_AgeAdjust_Level}Level: {FROM} to {TO}");
                text.SetTextVariable("FROM", before);
                text.SetTextVariable("TO", after);
                level = text.ToString();
            }

            return ChoiceEffects.Stated(age.ToString(), level);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
