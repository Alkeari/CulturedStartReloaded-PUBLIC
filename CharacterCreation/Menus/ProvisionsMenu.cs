using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     What the party carries onto the map, as a plan rather than a
    ///     manifest. Exact food and herd animals belong to the Start Editor's
    ///     Party Stores tab, and anything loaded there replaces the plan.
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
                "{=CSR_Provisions_Bare_Revamped}An Empty Larder",
                "{=CSR_Provisions_Bare_Desc}Not a crumb; the first market decides whether you eat.");

            manager.AddNewMenu(menu);
        }

        private static void AddPlanOption(NarrativeMenu menu, string id, ProvisionPlan plan,
            string titleKey, string descKey)
        {
            ChoiceEffects.Declare(id, () => Effect(plan));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                id,
                new TextObject(titleKey),
                new TextObject(descKey),
                args => { },
                m => true,
                m =>
                {
                    CreationSession.Current.Provisions = plan;
                    MenuText.Remember("cs_provisions_menu", titleKey);
                },
                m => { }
            ));
        }

        /// <summary>
        ///     What the wagons actually leave with, asked of the step that loads
        ///     them rather than worked out a second time here. Every plan is asked
        ///     of the same overload, so the empty wagons and the satchel are the
        ///     step's own figures rather than this file's copies of them.
        /// </summary>
        private static string Effect(ProvisionPlan plan)
        {
            var load = ResourceStep.PlanLoad(CreationSession.Current,
                GlobalSettings<CSSettings>.Instance, plan);

            if (load.Grain <= 0 && load.PackAnimals <= 0)
                return new TextObject(
                    "{=CSR_Panel_Provisions_Bare}Item: no food at all, so the party eats at the first market it reaches or it starves").ToString();

            var grain = new TextObject("{=CSR_Panel_Provisions_Grain}Item: {GRAIN} grain, in your wagons");
            grain.SetTextVariable("GRAIN", load.Grain);

            string? animals = load.PackAnimals <= 0
                ? null
                : MenuText.Count(load.PackAnimals,
                    "{=CSR_Panel_Provisions_Animal}Item: {COUNT} pack animal, in your train",
                    "{=CSR_Panel_Provisions_Animals}Item: {COUNT} pack animals, in your train");

            // Only a load the step scales says what it was scaled to; the satchel
            // is the same five whatever the column comes to
            string? muster = null;
            if (plan == ProvisionPlan.Sensible)
            {
                var scaled = new TextObject(
                    "{=CSR_Panel_Provisions_Muster}Why: scaled to a muster of {MUSTER}, before the party limit trims your column");
                scaled.SetTextVariable("MUSTER", load.Muster);
                muster = scaled.ToString();
            }

            return ChoiceEffects.Stated(grain.ToString(), animals, muster);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
