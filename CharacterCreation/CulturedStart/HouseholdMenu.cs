using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     The household as one readable choice. This was a cumulative builder,
    ///     which the stage cannot support: the list shows titles only, so reading
    ///     an option means selecting it, and selecting it added a relative. A
    ///     player who browsed ended up with a family they never chose, and the
    ///     only way back also killed their parents. Each option here states the
    ///     whole household and replaces whatever came before, so every click is
    ///     reversible by the next one. Per-member composition belongs to the
    ///     Start Editor, and the last option opens it.
    /// </summary>
    public static class HouseholdMenu
    {
        private sealed class Household
        {
            public Household(string id, string titleKey, string descKey, bool parentsAlive,
                bool spouse, int sons, int daughters, int brothers, int sisters)
            {
                Id = id;
                TitleKey = titleKey;
                DescKey = descKey;
                ParentsAlive = parentsAlive;
                Spouse = spouse;
                Sons = sons;
                Daughters = daughters;
                Brothers = brothers;
                Sisters = sisters;
            }

            public string Id { get; }
            public string TitleKey { get; }
            public string DescKey { get; }
            public bool ParentsAlive { get; }
            public bool Spouse { get; }
            public int Sons { get; }
            public int Daughters { get; }
            public int Brothers { get; }
            public int Sisters { get; }
        }

        private static readonly Household[] Households =
        {
            new("cs_household_alone", "{=CSR_Household_Alone}Alone in the World",
                "{=CSR_Household_Alone_Desc}Your parents are in the ground and no one else shares your name. Nothing holds you anywhere.",
                false, false, 0, 0, 0, 0),
            new("cs_household_parents", "{=CSR_Household_Parents}Your Parents Live",
                "{=CSR_Household_Parents_Desc}Your mother and father are alive and stand with your clan. There is still a door that opens to you.",
                true, false, 0, 0, 0, 0),
            new("cs_household_hearth", "{=CSR_Household_Hearth}A Spouse and a Hearth",
                "{=CSR_Household_Hearth_Desc}You married before the road took you. Your parents are gone, and the two of you are the whole of the house.",
                false, true, 0, 0, 0, 0),
            new("cs_household_full", "{=CSR_Household_Full}A Full House",
                "{=CSR_Household_Full_Desc}A spouse, a son and a daughter, and grandparents who dote on them. Everything you do now is for someone.",
                true, true, 1, 1, 0, 0),
            new("cs_household_wide", "{=CSR_Household_Wide}A Wide Clan",
                "{=CSR_Household_Wide_Desc}Parents, a spouse, two children, and a brother and sister who ride with you. A crowded hall and a long table.",
                true, true, 1, 1, 1, 1)
        };

        public static void AddHouseholdMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_household_menu",
                CreationFlow.DeclaredPrevious("cs_household_menu"),
                CreationFlow.DeclaredNext("cs_household_menu"),
                new TextObject("{=CSR_Household_Title}Your Household"),
                new TextObject("{=CSR_Household_Desc}Who shares your name when the story begins?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var household in Households)
            {
                var captured = household;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    captured.Id,
                    new TextObject(captured.TitleKey),
                    new TextObject(captured.DescKey),
                    args => { },
                    m => true,
                    m => Compose(captured),
                    m => { }
                ));
            }

            var customDescription = new TextObject(
                "{=CSR_Household_Custom_Desc}Set every relative yourself: how many, how old, alive or dead, married or single. {NOW}");

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_household_custom",
                new TextObject("{=CSR_Household_Custom}Compose It Yourself"),
                customDescription,
                args => { },
                MenuText.Live(customDescription, d => d.SetTextVariable("NOW", Summary())),
                m => Editor.StartEditorScreen.Open(
                    Editor.EditorScope.For("{=CSR_Scope_Household}Your Household", "family"), false),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     Replaces the household outright, so picking a second time undoes
        ///     the first rather than stacking on top of it.
        /// </summary>
        private static void Compose(Household household)
        {
            var members = CreationSession.Current.FamilyMembers;
            members.RemoveAll(m => m.Relation is not (FamilyRelation.Father or FamilyRelation.Mother));

            foreach (var parent in members)
                parent.IsAlive = household.ParentsAlive;

            // Children need a living parent alongside the player to be born to
            if (household.Spouse || household.Sons > 0 || household.Daughters > 0)
                members.Add(new FamilyMemberSpec(FamilyRelation.Spouse, true));

            Add(members, FamilyRelation.Son, household.Sons);
            Add(members, FamilyRelation.Daughter, household.Daughters);
            Add(members, FamilyRelation.Brother, household.Brothers);
            Add(members, FamilyRelation.Sister, household.Sisters);
        }

        private static void Add(List<FamilyMemberSpec> members, FamilyRelation relation, int count)
        {
            for (int i = 0; i < count; i++)
                members.Add(new FamilyMemberSpec(relation, true));
        }

        /// <summary>The household as it actually stands, for the editor option.</summary>
        private static string Summary()
        {
            var members = CreationSession.Current.FamilyMembers;
            int Living(FamilyRelation relation) => members.Count(m => m.Relation == relation && m.IsAlive);

            int kin = Living(FamilyRelation.Spouse) + Living(FamilyRelation.Son) + Living(FamilyRelation.Daughter)
                      + Living(FamilyRelation.Brother) + Living(FamilyRelation.Sister);
            bool parents = Living(FamilyRelation.Father) + Living(FamilyRelation.Mother) > 0;

            if (kin == 0)
                return new TextObject(parents
                    ? "{=CSR_Household_NowParents}Now: your parents, and no one else."
                    : "{=CSR_Household_NowAlone}Now: no living family.").ToString();

            var text = new TextObject(parents
                ? "{=CSR_Household_NowBoth}Now: your parents and {KIN}."
                : "{=CSR_Household_NowKin}Now: {KIN}.");
            text.SetTextVariable("KIN", MenuText.Count(kin,
                "{=CSR_Household_KinOne}{COUNT} other living relative",
                "{=CSR_Household_KinMany}{COUNT} other living relatives"));
            return text.ToString();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
