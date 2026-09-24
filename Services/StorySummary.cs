using System.Linq;
using System.Text;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Composes the player's chosen life into prose: the epilogue menu shows
    ///     it before the map, and the start report echoes it after.
    /// </summary>
    public static class StorySummary
    {
        public static string Build(CharacterCreationSession session)
        {
            var story = new StringBuilder();

            AppendOrigin(story, session);
            AppendPath(story, session);
            AppendChapters(story, session);
            AppendKin(story, session);

            return story.ToString().TrimEnd();
        }

        private static void AppendOrigin(StringBuilder story, CharacterCreationSession session)
        {
            if (session.Mode == SetupMode.LifePath)
            {
                AppendChapterOrigin(story, session);
                return;
            }

            if (AppendLifeAsTold(story, session)) return;

            // A start that told no life still takes the road at an age
            story.AppendLine(Line("{=CSR_Story_Age}You take the road at {AGE}.",
                "AGE", session.EffectiveAge.ToString()));
            story.AppendLine();
        }

        /// <summary>
        ///     Cultured Start's life, one sentence per chapter answered, each
        ///     completed by the answer's own title.
        /// </summary>
        private static void AppendChapterOrigin(StringBuilder story, CharacterCreationSession session)
        {
            string? family = null, childhood = null, education = null, youth = null,
                turning = null, reason = null;

            foreach (var menu in LifePathCatalog.BuildMenus())
            {
                var picked = menu.Choices.FirstOrDefault(c => c.IsSelected(session));
                if (picked == null) continue;
                var title = new TextObject(picked.Title).ToString();
                switch (menu.MenuId)
                {
                    case LifePathCatalog.FamilyMenuId: family = title; break;
                    case LifePathCatalog.ChildhoodMenuId: childhood = title; break;
                    case LifePathCatalog.EducationMenuId: education = title; break;
                    case LifePathCatalog.YouthMenuId: youth = title; break;
                    case LifePathCatalog.TurningMenuId: turning = title; break;
                    case LifePathCatalog.ReasonMenuId: reason = title; break;
                }
            }

            if (family != null)
                story.AppendLine(Line("{=CSR_Story_Family}You were born into a family of {FAMILY}.",
                    "FAMILY", family));
            if (childhood != null)
                story.AppendLine(Line("{=CSR_Story_Childhood}As a child, you {CHILDHOOD}.",
                    "CHILDHOOD", childhood));
            if (education != null)
                story.AppendLine(Line("{=CSR_Story_Education}Coming of age, you {EDUCATION}.",
                    "EDUCATION", education));
            if (youth != null)
                story.AppendLine(Line("{=CSR_Story_Youth}In your youth, you {YOUTH}.",
                    "YOUTH", youth));
            if (turning != null)
                story.AppendLine(Line("{=CSR_Story_Turning}Then came the hinge of your life: {TURNING}.",
                    "TURNING", turning));
            if (reason != null)
                story.AppendLine(Line("{=CSR_Story_Reason}And so you set out {REASON}",
                    "REASON", reason));
            story.AppendLine(Line("{=CSR_Story_Age}You take the road at {AGE}.",
                "AGE", session.EffectiveAge.ToString()));
            story.AppendLine();
        }

        /// <summary>
        ///     The guided route's own life, read back in the order it was lived.
        ///
        ///     Prose frames cannot serve it: a sentence like "you were born into a
        ///     family of" wants a noun, and a scene's answer is a verdict on a
        ///     moment. So the life is listed as it happened, each line the answer
        ///     the player gave, which is the thing they would recognize. False when
        ///     this run is not on the guided route: a character built by hand in
        ///     the Start Editor told no life, whatever scenes were answered before
        ///     the player backed out of that route and picked this one.
        /// </summary>
        private static bool AppendLifeAsTold(StringBuilder story, CharacterCreationSession session)
        {
            if (!Application.GuidedRun.WasWalkedBy(session)) return false;

            var chosen = SceneReading.Chosen(GuidedRoute.ScenesOf(session), SceneMenus.Answered);
            if (chosen.Count == 0) return false;

            story.AppendLine(new TextObject("{=CSR_Story_LifeAsTold}This is the life you told:").ToString());

            foreach (var option in chosen)
                story.AppendLine(Line("{=CSR_Story_LifeBeat}  {BEAT}",
                    "BEAT", new TextObject(option.Title).ToString()));

            story.AppendLine(Line("{=CSR_Story_Age}You take the road at {AGE}.",
                "AGE", session.EffectiveAge.ToString()));
            story.AppendLine();
            return true;
        }

        private static void AppendPath(StringBuilder story, CharacterCreationSession session)
        {
            var pathText = session.SelectedStartType switch
            {
                StartType.Monarch =>
                    new TextObject("{=CSR_Story_Monarch}Today, a crown is raised: your own realm, proclaimed before the world."),
                StartType.LandedVassal =>
                    new TextObject("{=CSR_Story_Landed}Today, you hold land and owe fealty: a lord of the realm with walls to keep."),
                StartType.LandlessVassal =>
                    new TextObject("{=CSR_Story_Landless}Today, you ride as a sworn vassal with no land yet to your name, only a banner and a debt of service."),
                StartType.Mercenary =>
                    new TextObject("{=CSR_Story_Mercenary}Today, your sword is under contract, and the realm that pays you is not your home."),
                StartType.Outlaw =>
                    new TextObject("{=CSR_Story_Outlaw}Today, you live outside the law, and the law knows it."),
                StartType.CaravanMaster =>
                    new TextObject("{=CSR_Story_Caravan}Today, your fortune rolls on wagon wheels between markets."),
                StartType.RebelClan =>
                    new TextObject("{=CSR_Story_Rebel}Today, your house stands in open rebellion, a seized castle at your back."),
                _ =>
                    new TextObject("{=CSR_Story_Commoner}Today, you begin with your name and little else, which has been enough for greater tales than this.")
            };
            story.AppendLine(pathText.ToString());

            var realm = session.SelectedKingdom?.Name?.ToString();
            if (realm != null)
                story.AppendLine(Line("{=CSR_Story_Realm}Your fate is bound to {REALM}.", "REALM", realm));

            var holding = session.SelectedSettlement?.Name?.ToString();
            if (holding != null)
                story.AppendLine(Line("{=CSR_Story_Holding}{HOLDING} is yours to keep.", "HOLDING", holding));

            // Cultured Start's epilogue never named the road's first town
            var town = session.Mode == SetupMode.LifePath ? null : session.SelectedLocation?.Name?.ToString();
            if (town != null)
                story.AppendLine(Line("{=CSR_Story_Location}The road starts in {TOWN_NAME}.",
                    "TOWN_NAME", town));
            else if (session.UseRandomLocation && session.Mode != SetupMode.LifePath)
                story.AppendLine(new TextObject(
                    "{=CSR_Story_LocationFate}Where the road starts, you will find out when you open your eyes.")
                    .ToString());
            story.AppendLine();
        }

        /// <summary>
        ///     The scenario chapters, in the order the player walked them. Each
        ///     chapter supplies a lead-in and the choice's own title completes it,
        ///     so the middle act appears in the summary named after it.
        /// </summary>
        private static readonly (string ChapterId, string Template)[] ChapterLines =
        {
            ("cs_realm_wars_menu", "{=CSR_Beat_RealmWars}The realm you joined answers for {CHOICE}."),
            ("cs_means_menu", "{=CSR_Beat_Means}You ride out with {CHOICE}."),
            ("cs_warband_menu", "{=CSR_Beat_Warband}At your back rides {CHOICE}."),
            ("cs_contract_menu", "{=CSR_Beat_Contract}Your sword was signed for {CHOICE}."),
            ("cs_crime_menu", "{=CSR_Beat_Crime}The law remembers you for {CHOICE}."),
            ("cs_fief_menu", "{=CSR_Beat_Fief}Your walls are held by {CHOICE}."),
            ("cs_rising_menu", "{=CSR_Beat_Rising}The rising came with {CHOICE}."),
            ("cs_founding_menu", "{=CSR_Beat_Founding}Your crown was founded as {CHOICE}."),
            ("cs_traditions_menu", "{=CSR_Beat_Traditions}Your realm stands on {CHOICE}."),
            ("cs_sworn_houses_menu", "{=CSR_Beat_SwornHouses}At your coronation stood {CHOICE}."),
            ("cs_first_war_menu", "{=CSR_Beat_FirstWar}Your reign opens with {CHOICE}."),
            ("cs_trade_menu", "{=CSR_Beat_Trade}Your living comes from {CHOICE}."),
            ("cs_ledger_menu", "{=CSR_Beat_Ledger}Your wagons carry {CHOICE}."),
            ("cs_beasts_menu", "{=CSR_Beat_Beasts}Your train walks on {CHOICE}."),
            ("cs_gear_menu", "{=CSR_Beat_Gear}You are turned out {CHOICE}."),
            ("cs_arms_menu", "{=CSR_Beat_Arms}You fight with {CHOICE}."),
            ("cs_provisions_menu", "{=CSR_Beat_Provisions}Your wagons leave with {CHOICE}.")
        };

        private static void AppendChapters(StringBuilder story, CharacterCreationSession session)
        {
            bool wroteAny = false;
            foreach (var (chapterId, template) in ChapterLines)
            {
                if (!session.StoryBeats.TryGetValue(chapterId, out var choice)) continue;

                // Second guard behind ResetScenarioState: a beat belonging to a
                // route this session no longer walks is not part of this story
                if (!CreationFlow.Includes(session, chapterId)) continue;

                story.AppendLine(Line(template, "CHOICE", choice));
                wroteAny = true;
            }

            if (wroteAny) story.AppendLine();
        }

        private static void AppendKin(StringBuilder story, CharacterCreationSession session)
        {
            int living = session.FamilyMembers.Count(m => m.IsAlive);
            int riders = FamilyAges.InPartyCount(session);

            if (living == 0)
                story.AppendLine(new TextObject(
                    "{=CSR_Story_NoKin}You carry your family only in memory; the road ahead is yours alone.").ToString());
            else if (riders == 1)
                story.AppendLine(new TextObject(
                    "{=CSR_Story_KinRidingOne}One of your kin rides at your side as the tale begins.").ToString());
            else if (riders > 1)
                story.AppendLine(Line(
                    "{=CSR_Story_KinRiding}Of your kin, {COUNT} ride at your side as the tale begins.",
                    "COUNT", riders.ToString()));
            else
                story.AppendLine(new TextObject(
                    "{=CSR_Story_KinHome}Your kin keep the hearth while you take the road.").ToString());

            if (session.StartingCompanions == 1)
                story.AppendLine(new TextObject(
                    "{=CSR_Story_CompanionOne}One companion has thrown in their lot with yours.").ToString());
            else if (session.StartingCompanions > 1)
                story.AppendLine(Line(
                    "{=CSR_Story_Companions}{COUNT} companions have thrown in their lot with yours.",
                    "COUNT", session.StartingCompanions.ToString()));
        }

        private static string Line(string template, string variable, string value)
        {
            var text = new TextObject(template);
            text.SetTextVariable(variable, value);
            return text.ToString();
        }
    }
}
