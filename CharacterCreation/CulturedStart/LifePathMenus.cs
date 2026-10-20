using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Builds the life-path menus from <see cref="LifePathCatalog"/>.
    ///     All choice data (text, skills, attributes, gating) lives in the catalog;
    ///     this class only translates it into the game's menu objects.
    /// </summary>
    public static class LifePathMenus
    {
        public static void AddLifePathMenus(CharacterCreationManager manager)
        {
            foreach (var def in LifePathCatalog.BuildMenus())
            {
                var menu = new NarrativeMenu(
                    def.MenuId,
                    CreationFlow.DeclaredPrevious(def.MenuId),
                    CreationFlow.DeclaredNext(def.MenuId),
                    new TextObject(def.Title),
                    new TextObject(def.Description),
                    CharacterPreviewHelper.CreatePlayerCharacter(),
                    GetPlayerCharacterArgs
                );

                foreach (var choice in def.Choices)
                {
                    if (choice.RequiredNavalSkill != null &&
                        NavalDLCService.GetNavalSkill(choice.RequiredNavalSkill) == null)
                        continue;

                    var captured = choice;
                    menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                        captured.OptionId,
                        new TextObject(captured.Title),
                        new TextObject("{=!}" + DescriptionWithEffects(captured)),
                        args =>
                        {
                            var skills = captured.Skills();
                            if (skills.Length > 0)
                            {
                                args.SetAffectedSkills(skills);
                                args.SetFocusToSkills(captured.FocusWeight);
                            }

                            var attribute = captured.Attribute();
                            if (attribute != null)
                                args.SetLevelToAttribute(attribute, 1);

                            // The stage already renders a trait row and effect
                            // text from these; a cost the player cannot see is
                            // not a tradeoff
                            ApplyTraits(args, captured);

                            if (captured.UnspentFocus > 0)
                                args.SetUnspentFocusToAdd(captured.UnspentFocus);
                            if (captured.UnspentAttribute > 0)
                                args.SetUnspentAttributeToAdd(captured.UnspentAttribute);
                        },
                        m => captured.Condition?.Invoke(CreationSession.Current) ?? true,
                        m => captured.Select(CreationSession.Current),
                        m => { }
                    ));
                }

                manager.AddNewMenu(menu);
            }
        }

        /// <summary>
        ///     The args API carries one level for the whole trait set, and every
        ///     catalog choice moves a single trait by one, so the traits are
        ///     grouped by delta and the first group is sent.
        /// </summary>
        private static void ApplyTraits(NarrativeMenuOptionArgs args, LifePathChoice choice)
        {
            if (choice.Traits.Length == 0) return;

            var groups = choice.Traits
                .Select(t => (Trait: LifePathCatalog.ResolveTrait(t.traitId), t.delta))
                .Where(t => t.Trait != null)
                .GroupBy(t => t.delta)
                .OrderByDescending(g => Math.Abs(g.Key))
                .ToList();
            if (groups.Count == 0) return;

            // The game's narrative API takes ONE level for a SET of traits, so a choice moving two
            // traits by different amounts cannot be previewed whole and only the largest movement is
            // shown. Every catalog choice moves exactly one trait today, so nothing is lost; this
            // says so out loud rather than dropping it in silence if that ever changes. The start
            // itself is unaffected: ConsequenceStep applies every trait from its own totals.
            if (groups.Count > 1)
                CSLogger.Warn(
                    $"LifePathMenus: choice {choice.OptionId} moves traits by {groups.Count} different " +
                    "amounts; the creation screen can only preview the largest. The applied start is correct.");

            var shown = groups[0];
            args.SetAffectedTraits(shown.Select(t => t.Trait!).ToArray());
            args.SetLevelToTraits(shown.Key);
        }

        /// <summary>
        ///     The written description plus one sentence for each consequence the
        ///     prose only gestures at: the keepsake and the goodwill.
        /// </summary>
        private static string DescriptionWithEffects(LifePathChoice choice)
        {
            var parts = new List<string> { new TextObject(choice.Description).ToString() };

            var heirloom = LifePathCatalog.HeirloomNote(choice.Heirloom);
            if (heirloom != null) parts.Add(heirloom);

            var relation = LifePathCatalog.RelationNote(choice.Relations);
            if (relation != null) parts.Add(relation);

            return string.Join(" ", parts);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
