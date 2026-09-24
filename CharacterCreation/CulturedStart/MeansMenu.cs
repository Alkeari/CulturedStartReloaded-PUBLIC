using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     What the player rides out with, as one chapter instead of five screens
    ///     of presets. Each band sets coin, influence, warband and standing
    ///     together and says in plain numbers what that means, because the old
    ///     screens asked for five separate decisions and described all twenty
    ///     options with the same sentence. Level is deliberately absent: how
    ///     seasoned you are belongs to your life path, and the exact figures live
    ///     behind the last option for anyone who wants to count them out.
    /// </summary>
    public static class MeansMenu
    {
        private static EquipmentPreviewService? _previewService;

        public static void SetPreviewService(EquipmentPreviewService? service)
        {
            _previewService = service;
        }

        private static readonly (string Id, string TitleKey, string DescKey, RangePreset Preset, int Step)[] Bands =
        {
            ("cs_means_saddlebag", "{=CSR_Means_Saddlebag}What Fit in a Saddlebag",
                "{=CSR_Means_Saddlebag_Desc}You left with what you could carry, and nothing waiting behind you. {FACTS}",
                RangePreset.Minimum, 0),
            ("cs_means_modest", "{=CSR_Means_Modest}A Modest Purse",
                "{=CSR_Means_Modest_Desc}Coin enough to eat while you find your feet, and a few hands willing to follow. {FACTS}",
                RangePreset.Low, 1),
            ("cs_means_fair", "{=CSR_Means_Fair}A Fair Standing",
                "{=CSR_Means_Fair_Desc}A name the nearer towns already know, and the means to trade on it. {FACTS}",
                RangePreset.Standard, 2),
            ("cs_means_warchest", "{=CSR_Means_WarChest}A War Chest and a Company",
                "{=CSR_Means_WarChest_Desc}Enough silver to pay a company, and the company to spend it on. {FACTS}",
                RangePreset.High, 3),
            ("cs_means_greathouse", "{=CSR_Means_GreatHouse}The Means of a Great House",
                "{=CSR_Means_GreatHouse_Desc}Wealth, standing and swords enough that lesser lords weigh their words with you. {FACTS}",
                RangePreset.Maximum, 4)
        };

        public static void AddMeansMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_means_menu",
                CreationFlow.DeclaredPrevious("cs_means_menu"),
                CreationFlow.DeclaredNext("cs_means_menu"),
                new TextObject("{=CSR_Means_Title}What You Carry"),
                new TextObject("{=CSR_Means_Desc}Every road starts with what is in your purse and who rides at your back."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            foreach (var (id, titleKey, descKey, preset, step) in Bands)
            {
                var capturedPreset = preset;
                var capturedStep = step;
                var description = new TextObject(descKey);

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    id,
                    new TextObject(titleKey),
                    description,
                    args => { },
                    MenuText.Live(description, d => d.SetTextVariable("FACTS", Facts(capturedPreset, capturedStep))),
                    m => Apply(capturedPreset, capturedStep),
                    m => { }
                ));
            }

            var exactDescription = new TextObject(
                "{=CSR_Means_Exact_Desc}Set the coin, influence, warband, renown and level yourself, to the figure. {FACTS}");

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_means_exact",
                new TextObject("{=CSR_Means_Exact}Count It Out Yourself"),
                exactDescription,
                args => { },
                MenuText.Live(exactDescription, d => d.SetTextVariable("FACTS", CurrentFacts())),
                m => OpenExactPicker(),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     Standing scales with means, between the floor the start type
        ///     demands and the ceiling it is allowed. Without that ceiling the
        ///     top band ran to the highest tier in the game for every start, so
        ///     a commoner of "humble means" could open with a great house.
        /// </summary>
        private static int TierFor(int step)
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            int floor = settings?.GetMinClanTier(session.SelectedStartType) ?? 0;
            int top = settings?.GetMaxClanTier(session.SelectedStartType) ?? GameCaps.MaxClanTier();
            if (floor >= top) return top;

            return Math.Min(top, floor + step * (top - floor) / (Bands.Length - 1));
        }

        private static string Facts(RangePreset preset, int step)
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;
            int tier = TierFor(step);

            int gold = CSSettings.GetRangeValue(
                settings?.GetGoldRange(session.SelectedStartType) ?? (500, 3000), preset);
            int troops = CSSettings.GetRangeValue(
                settings?.GetTroopsRange(session.SelectedStartType) ?? (0, 20), preset);

            var facts = new TextObject("{=CSR_Means_Facts}{GOLD} denars, {WARBAND}, clan tier {TIER}.");
            facts.SetTextVariable("GOLD", gold);
            facts.SetTextVariable("WARBAND", MenuText.Count(troops,
                "{=CSR_Means_WarbandOne}{COUNT} man at your back",
                "{=CSR_Means_WarbandMany}{COUNT} men at your back"));
            facts.SetTextVariable("TIER", tier);

            if (!CSSettings.UsesInfluence(session.SelectedStartType)) return facts.ToString();

            int influence = CSSettings.GetRangeValue(
                settings?.GetInfluenceRange(session.SelectedStartType) ?? (0, 0), preset);
            var withInfluence = new TextObject("{=CSR_Means_FactsInfluence}{FACTS} {INFLUENCE} influence.");
            withInfluence.SetTextVariable("FACTS", facts.ToString());
            withInfluence.SetTextVariable("INFLUENCE", influence);
            return withInfluence.ToString();
        }

        /// <summary>Whatever the session actually holds, exact overrides included.</summary>
        private static string CurrentFacts()
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            int gold = session.CustomGold ?? CSSettings.GetRangeValue(
                settings?.GetGoldRange(session.SelectedStartType) ?? (500, 3000), session.SelectedGold);
            int troops = session.CustomTroops ?? CSSettings.GetRangeValue(
                settings?.GetTroopsRange(session.SelectedStartType) ?? (0, 20), session.SelectedTroops);

            var facts = new TextObject("{=CSR_Means_Now}Now: {GOLD} denars, {WARBAND}, clan tier {TIER}.");
            facts.SetTextVariable("GOLD", gold);
            facts.SetTextVariable("WARBAND", MenuText.Count(troops,
                "{=CSR_Means_WarbandOne}{COUNT} man at your back",
                "{=CSR_Means_WarbandMany}{COUNT} men at your back"));
            facts.SetTextVariable("TIER", session.EffectiveClanTier);
            return facts.ToString();
        }

        private static void Apply(RangePreset preset, int step)
        {
            var session = CreationSession.Current;

            session.SelectedGold = preset;
            session.CustomGold = null;
            session.SelectedInfluence = preset;
            session.CustomInfluence = null;
            session.SelectedTroops = preset;
            session.CustomTroops = null;
            session.SelectedClanTier = TierFor(step);
            session.CustomRenown = null;

            // How seasoned you are is the life path's business, not the purse's
            session.CustomLevel = null;

            RefreshArmorPreview();
        }

        private static void RefreshArmorPreview()
        {
            if (_previewService == null) return;

            var session = CreationSession.Current;
            var range = GlobalSettings<CSSettings>.Instance?.GetEquipmentTierRange(session.SelectedStartType)
                        ?? (0, 2);
            int equipTier = Math.Max(range.min, Math.Min(session.EffectiveClanTier, range.max));
            _previewService.GenerateArmorPreview(session.SelectedCulture, equipTier);
        }

        /// <summary>
        ///     Everything the bands set, plus renown and level, to the exact
        ///     figure. Every bound comes from the same live models the Start
        ///     Editor reads.
        /// </summary>
        private static void OpenExactPicker()
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;

            // Dependency order: renown decides the clan tier, which several later
            // chapters bound off, so it is asked first. The warband count is not here:
            // it belongs after the companions and relatives that take its seats.
            var options = new List<(string, object?)>
            {
                (new TextObject("{=CSR_CustomRenown}Custom Renown").ToString(), "renown"),
                (new TextObject("{=CSR_Resource_Gold}Gold").ToString(), "gold"),
                (new TextObject("{=CSR_Resource_Level}Level").ToString(), "level")
            };

            if (CSSettings.UsesInfluence(session.SelectedStartType))
                options.Insert(2, (new TextObject("{=CSR_Resource_Influence}Influence").ToString(), "influence"));

            Editor.EditorPopups.ShowOptions(
                new TextObject("{=CSR_Means_Exact}Count It Out Yourself").ToString(), options, picked =>
                {
                    switch (picked as string)
                    {
                        case "gold":
                            Prompt("{=CSR_Resource_Gold}Gold", 0, GameCaps.MaxGold,
                                v => session.CustomGold = v);
                            break;
                        case "influence":
                            Prompt("{=CSR_Resource_Influence}Influence", 0, GameCaps.MaxInfluence,
                                v => session.CustomInfluence = v);
                            break;
                        case "renown":
                            Prompt("{=CSR_CustomRenown}Custom Renown", 0, GameCaps.MaxRenown(),
                                v => session.CustomRenown = v);
                            break;
                        case "level":
                            Prompt("{=CSR_Resource_Level}Level", 1, GameCaps.MaxHeroLevel(), v =>
                            {
                                session.CustomLevel = v;
                            });
                            break;
                    }
                });
        }

        private static void Prompt(string titleKey, int min, int max, Action<int> apply)
        {
            var title = new TextObject(titleKey).ToString();
            Editor.EditorPopups.ShowNumber(title, min, max, value =>
            {
                apply(value);
                var confirmation = new TextObject("{=CSR_Means_Set}{LABEL} set to {VALUE}.");
                confirmation.SetTextVariable("LABEL", title);
                confirmation.SetTextVariable("VALUE", value);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(confirmation.ToString()));
            });
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
