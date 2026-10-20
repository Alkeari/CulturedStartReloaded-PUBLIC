using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;
using CulturedStartReloaded.Services.Application.Steps;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Core;

namespace CulturedStartReloaded.Patches
{
    /// <summary>
    ///     Makes the vanilla left-side stat panel tell the truth about both mod
    ///     routes.
    ///
    ///     The panel has one contract, and it is the game's own: the dim half of
    ///     every reading is what the character already had, and the lit half is
    ///     what the option the player is standing on does to them. Vanilla fills
    ///     both from the args an option declares, which this mod's guided scenes
    ///     do not carry: declaring them would hand the character the same grant
    ///     twice, because the engine replays every selected option's args at
    ///     ApplyFinalEffects on top of everything the pipeline has applied. So the
    ///     two halves are measured here instead, from the life itself: the sheet
    ///     the answers add up to, against the sheet the same answers add up to
    ///     with the scene in front of the player held out. The difference is
    ///     exactly what that one choice did, which is what the player is owed and
    ///     what the panel used to have no way of saying.
    ///
    ///     Nothing here is a projection. The skill figure on each icon is the
    ///     character's own skill value, which the scenes really write as they are
    ///     answered; it used to be the value the FINISHED run would reach, painted
    ///     over a sheet that still held nothing, a forecast the sheet must never show.
    ///     On the Vanilla Start route that same figure used to be a sheet the mod
    ///     was never going to apply at all, since the pipeline does not run there.
    ///
    ///     The Start Editor's exact values still win over all of it: a value the
    ///     player typed is the one promise that route makes.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreationGainedPropertiesVM), "UpdateValues")]
    public static class GainedPropertiesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCreationGainedPropertiesVM __instance)
        {
            try
            {
                if (GameStateManager.Current?.ActiveState is not CharacterCreationState state)
                    return;

                var session = CreationSession.Current;
                var life = TheLifeAndTheChoice(state);

                foreach (var group in __instance.GainGroups)
                {
                    ShowAttribute(session, life, group);

                    foreach (var skillItem in group.Skills)
                        ShowSkill(session, life, skillItem);
                }

                foreach (var skillItem in __instance.OtherSkills)
                    ShowSkill(session, life, skillItem);
            }
            catch (Exception ex)
            {
                CSLogger.Error("GainedPropertiesPatch: stat panel override failed.", ex);
            }
        }

        private static void ShowAttribute(CharacterCreationSession session, Reading? life,
            CharacterCreationGainGroupItemVM group)
        {
            if (group.AttributeObj == null || group.Attribute == null) return;

            if (session.CustomAttributes.TryGetValue(group.AttributeObj.StringId, out int exact))
            {
                group.Attribute.SetValue(exact, 0);
                return;
            }

            if (life?.Attributes == null || life.AttributesBefore == null) return;
            if (!life.Attributes.TryGetValue(group.AttributeObj, out int now)) return;

            life.AttributesBefore.TryGetValue(group.AttributeObj, out int before);
            Show(now, before, group.Attribute.SetValue);
        }

        private static void ShowSkill(CharacterCreationSession session, Reading? life,
            CharacterCreationGainedSkillItemVM skillItem)
        {
            if (skillItem.SkillObj == null) return;

            if (session.CustomFocus.TryGetValue(skillItem.SkillObj.StringId, out int exact))
            {
                skillItem.SetValue(exact, 0);
            }
            else if (life?.Focus != null && life.FocusBefore != null &&
                     life.Focus.TryGetValue(skillItem.SkillObj, out int now))
            {
                life.FocusBefore.TryGetValue(skillItem.SkillObj, out int before);
                Show(now, before, skillItem.SetValue);
            }

            if (skillItem.Skill != null)
                skillItem.Skill.SkillValue = SkillValue(session, skillItem.SkillObj);
        }

        /// <summary>
        ///     One reading, split the way the widget splits it: what the character
        ///     held before this choice, and what this choice added.
        ///
        ///     A choice can also take something away, because answering reorders
        ///     which skills sit at the top of the sheet. The widget has no way to
        ///     draw a loss, so that case shows the character where they now stand
        ///     with nothing lit: a dim reading that is correct beats a lit one that
        ///     counts backwards.
        /// </summary>
        private static void Show(int now, int before, Action<int, int> setValue)
        {
            int gained = now - before;
            if (gained > 0) setValue(before, gained);
            else setValue(now, 0);
        }

        /// <summary>
        ///     The skill value the character carries at this moment. The editor's
        ///     exact value where the player set one; otherwise the hero's own, which
        ///     both routes really write.
        /// </summary>
        private static int SkillValue(CharacterCreationSession session, SkillObject skill)
        {
            if (session.CustomSkillLevels.TryGetValue(skill.StringId, out int exact))
                return exact;

            // Cultured Start writes no sheet while it is being answered; its icons
            // carry the levels its answers will be applied at
            if (session.Mode == SetupMode.LifePath)
                return NarrativeStep.ExpectedSkillValue(session, skill);

            return TaleWorlds.CampaignSystem.Hero.MainHero?.GetSkillValue(skill) ?? 0;
        }

        /// <summary>
        ///     The life as it stands, and the same life with the scene in front of
        ///     the player held out of it, so the difference between them is what
        ///     the option they are standing on did.
        ///
        ///     The scene is held out WHOLE rather than the newest answer taken off,
        ///     which is how <c>Scene.LifeBefore</c> and <c>Services/ChoiceEffects</c>
        ///     read the same run: the game's Back button walks into an earlier
        ///     scene without dropping the answers that came after it, so the newest
        ///     answer is not always the one the player is looking at.
        ///
        ///     Nothing is lit until the player lands on an option in this menu,
        ///     which is how vanilla draws a stage nobody has answered yet.
        ///
        ///     Null off the guided route, where there are no answers to difference
        ///     and the panel has nothing to add to what vanilla already drew.
        /// </summary>
        private static Reading? TheLifeAndTheChoice(CharacterCreationState state)
        {
            if (!GuidedRun.WasWalked) return null;

            var manager = state.CharacterCreationManager;
            if (manager == null) return null;

            var menu = manager.CurrentMenu;
            if (menu == null) return null;

            var answered = SceneReading.Chosen(GuidedRoute.Scenes, SceneMenus.Answered);
            if (answered.Count == 0) return null;

            Scene? standingIn = null;
            if (manager.SelectedOptions.ContainsKey(menu))
                foreach (var scene in GuidedRoute.Scenes)
                    if (string.Equals(scene.Id, menu.StringId, StringComparison.Ordinal))
                    {
                        standingIn = scene;
                        break;
                    }

            var whole = new List<ChoiceConsequence>();
            var earlier = new List<ChoiceConsequence>();
            foreach (var option in answered)
            {
                whole.AddRange(option.Consequences);
                if (standingIn?.Find(option.Id) == null) earlier.AddRange(option.Consequences);
            }

            var told = LifeProfile.From(whole);
            var before = LifeProfile.From(earlier);

            return new Reading
            {
                Attributes = NarrativeStep.AttributesTheLifeHolds(told),
                AttributesBefore = NarrativeStep.AttributesTheLifeHolds(before),
                Focus = NarrativeStep.FocusTheLifeHolds(told),
                FocusBefore = NarrativeStep.FocusTheLifeHolds(before)
            };
        }

        private sealed class Reading
        {
            public Dictionary<CharacterAttribute, int>? Attributes;
            public Dictionary<CharacterAttribute, int>? AttributesBefore;
            public Dictionary<SkillObject, int>? Focus;
            public Dictionary<SkillObject, int>? FocusBefore;
        }
    }
}
