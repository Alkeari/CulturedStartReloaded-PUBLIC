using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     Who came with you. This chapter used to be one option per number, up to
    ///     the highest tier's companion limit, which is a slider wearing a menu's
    ///     clothes. It asks how you left instead, and the number falls out of the
    ///     answer and the clan's live companion limit. The first option asks for
    ///     nothing at all: it reads the life already chosen.
    /// </summary>
    public static class CompanionSelectMenu
    {
        public static void AddCompanionMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_companion_select",
                CreationFlow.DeclaredPrevious("cs_companion_select"),
                CreationFlow.DeclaredNext("cs_companion_select"),
                new TextObject("{=CSR_Companion_Title_Revamped}Who Came with You"),
                new TextObject(
                    "{=CSR_Companion_Desc_Revamped}Nobody rides out of their old life alone unless they mean to. Who was at your shoulder on the first morning?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddDerivedOption(menu);

            AddBand(menu, "cs_companion_alone",
                "{=CSR_Companion_Alone}You Told No One",
                "{=CSR_Companion_Alone_Desc}You left before dawn and did not say goodbye. Whatever you build now, you build from nothing.",
                _ => 0);

            AddBand(menu, "cs_companion_one",
                "{=CSR_Companion_One}One Would Not Stay Behind",
                "{=CSR_Companion_One_Desc}You meant to go alone. Someone was waiting at the road with their own horse and would not be argued with.",
                cap => Math.Min(1, cap));

            AddBand(menu, "cs_companion_few",
                "{=CSR_Companion_Few}A few old debts came due",
                "{=CSR_Companion_Few_Desc}You called in what you were owed, and enough of it walked in on two legs.",
                cap => Math.Max(1, cap / 2));

            AddBand(menu, "cs_companion_full",
                "{=CSR_Companion_Full}Everyone Who Answered",
                "{=CSR_Companion_Full_Desc}You put the word out and did not turn anybody away. Every seat your name can hold is filled.",
                cap => cap);

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     The option that asks nothing: the life path already said how many
        ///     people would come, and this reads it back in the words of the
        ///     lean that earned them.
        /// </summary>
        private static void AddDerivedOption(NarrativeMenu menu)
        {
            var description = new TextObject("{=CSR_Companion_Life_Desc}{REASON}");

            ChoiceEffects.Declare("cs_companion_life", () => Effect(DerivedCount()));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_companion_life",
                new TextObject("{=CSR_Companion_Life}Those Your Life Gathered"),
                description,
                args => { },
                MenuText.Live(description, d => d.SetTextVariable("REASON", DerivedReason())),
                m => CreationSession.Current.StartingCompanions = DerivedCount(),
                m => { }
            ));
        }

        private static void AddBand(NarrativeMenu menu, string id, string titleKey, string descKey,
            Func<int, int> fromCap)
        {
            var description = new TextObject(descKey);
            var captured = fromCap;

            ChoiceEffects.Declare(id, () => Effect(Clamped(captured)));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                description,
                args => { },
                // The description quotes nothing now, so nothing has to be
                // refreshed into it on a render
                m => true,
                m => CreationSession.Current.StartingCompanions = Clamped(captured),
                m => { }
            ));
        }

        /// <summary>Every seat the clan can actually hold at the tier now chosen.</summary>
        private static int Ceiling()
        {
            var session = CreationSession.Current;
            return Math.Max(0, GameCaps.MaxCompanions(session.EffectiveClanTier, session));
        }

        private static int Clamped(Func<int, int> fromCap)
        {
            int cap = Ceiling();
            return Math.Max(0, Math.Min(cap, fromCap(cap)));
        }

        /// <summary>
        ///     How many the life path gathered, as a share of the seats available:
        ///     a life spent among people fills them, a solitary one does not.
        /// </summary>
        private static int DerivedCount()
        {
            var profile = LifeProfile.From(CreationSession.Current);
            int cap = Ceiling();
            return Math.Max(0, Math.Min(cap, profile.Band(LifeProfile.Lean.Following) * cap / 4));
        }

        /// <summary>Why they came, in the words of whatever the life leaned hardest on.</summary>
        private static string DerivedReason()
        {
            var profile = LifeProfile.From(CreationSession.Current);

            string key = profile.Dominant switch
            {
                LifeProfile.Lean.Martial =>
                    "{=CSR_Companion_Why_Martial}You served beside these people, and none of them asked where you were going.",
                LifeProfile.Lean.Commerce =>
                    "{=CSR_Companion_Why_Commerce}You paid them honestly for years, so when you went they came, and they still expect to be paid.",
                LifeProfile.Lean.Standing =>
                    "{=CSR_Companion_Why_Standing}Your house has always had people around it. They came because that is what they have always done.",
                LifeProfile.Lean.Following =>
                    "{=CSR_Companion_Why_Following}You have never had trouble getting people to follow you, and you did not have trouble that morning either.",
                LifeProfile.Lean.Wilds =>
                    "{=CSR_Companion_Why_Wilds}You kept your own company for most of your life. The few who came had to insist.",
                LifeProfile.Lean.Craft =>
                    "{=CSR_Companion_Why_Craft}Word travels in a trade. The ones who came had worked beside you and trusted your hands.",
                _ =>
                    "{=CSR_Companion_Why_Sea}You crewed with them. A crew that has been through weather together does not scatter easily."
            };

            return new TextObject(key).ToString();
        }

        /// <summary>
        ///     What this answer puts in the party, in the count the pick spends, so
        ///     the panel and the roster cannot disagree.
        ///
        ///     The seats are stated twice over. A companion seat is the same seat a
        ///     soldier would have taken, and the chapter that spends what is left of
        ///     the party comes after this one, so the cost of filling the column
        ///     belongs where the column is filled.
        /// </summary>
        private static string Effect(int count)
        {
            int ceiling = Ceiling();

            // The people earlier scenes named come whatever is answered here: the
            // step builds them first and spends the clan's seats on them, and the
            // scene that earned each one promised them by name. A line reading
            // "no companion when the campaign opens" was true only for a life that
            // had named nobody, and false for every life that had
            int named = Named();
            int raised = Math.Max(0, Math.Min(count, ceiling - named));

            string? besides = named <= 0
                ? null
                : MenuText.Count(named,
                    "{=CSR_Panel_Companion_NamedOne}Allies: {COUNT} besides, the person your life already named, who rides whatever you answer here",
                    "{=CSR_Panel_Companion_NamedMany}Allies: {COUNT} besides, the people your life already named, who ride whatever you answer here");

            if (raised == 0)
            {
                var alone = new TextObject(named > 0
                    ? "{=CSR_Panel_Companion_NoneCalled}Allies: nobody you called, of the {CEILING} seats your clan can hold"
                    : "{=CSR_Panel_Companion_NoOne}Allies: no companion when the campaign opens, of the {CEILING} seats your clan can hold");
                alone.SetTextVariable("CEILING", ceiling);
                return ChoiceEffects.Stated(alone.ToString(), besides);
            }

            var riding = new TextObject(
                "{=CSR_Panel_Companion_Riding}Allies: {COMPANIONS} when the campaign opens, of the {CEILING} seats your clan can hold");
            riding.SetTextVariable("COMPANIONS", MenuText.Count(raised,
                "{=CSR_Panel_Companion_One}{COUNT} companion",
                "{=CSR_Panel_Companion_Many}{COUNT} companions"));
            riding.SetTextVariable("CEILING", ceiling);

            var cost = new TextObject(raised == 1
                ? "{=CSR_Panel_Companion_CostOne}Troops: one fewer your party can carry, for the seat that companion takes"
                : "{=CSR_Panel_Companion_CostMany}Troops: {COUNT} fewer your party can carry, one for each seat they take");
            cost.SetTextVariable("COUNT", raised);

            return ChoiceEffects.Stated(
                riding.ToString(),
                besides,
                new TextObject(
                    "{=CSR_Panel_Companion_Who}Type: wanderers of your own culture, already known to you").ToString(),
                cost.ToString());
        }

        /// <summary>
        ///     How many the life already named, asked of the step that builds them
        ///     rather than counted a second time here.
        /// </summary>
        private static int Named()
        {
            try
            {
                return Services.Application.Steps.CompanionStep.NamedAllies().Count;
            }
            catch (Exception ex)
            {
                CSLogger.Error("CompanionSelectMenu: counting the people the life named failed.", ex);
                return 0;
            }
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
