using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Editor;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     What the party carries onto the map: a sensible plan by default, or
    ///     exact food and mounts through the Start Editor's pickers. Exact
    ///     additions replace the plan entirely.
    /// </summary>
    public static class ProvisionsMenu
    {
        public static void AddProvisionsMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_provisions_menu",
                CreationFlow.DeclaredPrevious("cs_provisions_menu"),
                CreationFlow.DeclaredNext("cs_provisions_menu"),
                new TextObject("{=CSR_Provisions_Title}Provisions"),
                new TextObject(
                    "{=CSR_Provisions_Desc}An army marches on its stomach. What do your wagons carry when the story begins?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            AddPlanOption(menu, "cs_provisions_sensible", ProvisionPlan.Sensible,
                "{=CSR_Provisions_Sensible}A Sensible Larder",
                "{=CSR_Provisions_Sensible_Desc}Grain scaled to your muster and a pack animal per fifteen mouths; the quartermaster knows the arithmetic.");

            AddPlanOption(menu, "cs_provisions_light", ProvisionPlan.Light,
                "{=CSR_Provisions_Light}Traveling Light",
                "{=CSR_Provisions_Light_Desc}A satchel of grain and nothing slowing you down.");

            AddPlanOption(menu, "cs_provisions_bare", ProvisionPlan.Bare,
                "{=CSR_Provisions_Bare}An Empty Larder",
                "{=CSR_Provisions_Bare_Desc}Not a crumb; the first market decides whether you eat.");

            // Adding, listing and clearing exact stock is three commands living
            // in a screen of choices. The editor's Party Stores tab does all
            // three with a list the player can see and undo, and it is scoped to
            // the wagons alone so this question cannot reach the purse
            var loadDescription = new TextObject(
                "{=CSR_Provisions_Load_Desc}Choose exact food and herd animals, and how much of each. Anything you load here replaces the plan. {NOW}");

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_provisions_load",
                new TextObject("{=CSR_Provisions_Load}Load the Wagons Yourself"),
                loadDescription,
                args => { },
                MenuText.Live(loadDescription, d => d.SetTextVariable("NOW", Summary())),
                m => Editor.StartEditorScreen.Open(
                    Editor.EditorScope.For("{=CSR_Scope_Provisions}Loading the Wagons", "stores"), false),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>What is actually on the wagons right now.</summary>
        private static string Summary()
        {
            var session = CreationSession.Current;
            int stacks = session.CustomFood.Count + session.CustomMounts.Count;

            return stacks == 0
                ? new TextObject("{=CSR_Provisions_NowPlan}Nothing exact is loaded, so the plan above stands.")
                    .ToString()
                : MenuText.Count(stacks,
                    "{=CSR_Provisions_NowOne}Now: {COUNT} exact stack loaded.",
                    "{=CSR_Provisions_NowMany}Now: {COUNT} exact stacks loaded.");
        }

        private static void AddPlanOption(NarrativeMenu menu, string id, ProvisionPlan plan,
            string titleKey, string descKey)
        {
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                new TextObject(descKey),
                args => { },
                m => true,
                m => CreationSession.Current.Provisions = plan,
                m => { }
            ));
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
