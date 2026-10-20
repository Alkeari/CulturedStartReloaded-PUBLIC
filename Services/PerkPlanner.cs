using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Computes the perks a start will grant: the player's explicit choices
    ///     first, then, on the custom path, the game model's own pick for every
    ///     remaining reachable slot. Pure planning, so the editor can probe
    ///     perk-dependent limits with the same list the finalizer applies.
    /// </summary>
    public static class PerkPlanner
    {
        private static readonly Dictionary<string, PerkObject> _pairChoices = new();

        /// <summary>
        ///     Forgets the perk-pair choices so the next start rolls its own. Called when a
        ///     character creation session begins.
        /// </summary>
        public static void ResetPairChoices() => _pairChoices.Clear();

        /// <summary>
        ///     The model's pick between a perk and its alternative, remembered for the rest of the
        ///     session.
        ///
        ///     This has to be remembered because the game's own answer is a coin toss:
        ///     DefaultCharacterDevelopmentModel.GetNextPerkToChoose returns the alternative when
        ///     MBRandom.RandomFloat is under 0.5. Planning is done at least twice for one start,
        ///     once when the editor probes a perk-dependent bound and again when the start is
        ///     applied, so an unremembered answer meant the two runs planned different perks. The
        ///     visible symptom was the editor offering eleven companions and the game granting ten,
        ///     because the companion limit adds a point for each of WePledgeOurSwords and
        ///     Camaraderie, and both of those are one half of a pair.
        ///
        ///     The model is still asked, once per pair, so a mod that replaces it is still obeyed.
        /// </summary>
        private static PerkObject ChoosePerk(CharacterDevelopmentModel? model, Hero hero, PerkObject perk)
        {
            if (_pairChoices.TryGetValue(perk.StringId, out var remembered)) return remembered;

            var pick = model?.GetNextPerkToChoose(hero, perk) ?? perk;
            _pairChoices[perk.StringId] = pick;
            return pick;
        }

        public static List<PerkObject> Plan(Hero hero, CharacterCreationSession session)
        {
            var planned = new List<PerkObject>();
            var set = new HashSet<PerkObject>();

            bool Has(PerkObject perk) => set.Contains(perk) || hero.GetPerkValue(perk);

            bool allowBoth = GameCaps.AllowsBothPerks();
            foreach (var pair in session.CustomPerks)
            foreach (var perkId in pair.Value)
            {
                var perk = PerkObject.All.FirstOrDefault(p => p.StringId == perkId);
                if (perk == null) continue;
                if (Application.Steps.NarrativeStep.ExpectedSkillValue(session, perk.Skill) < perk.RequiredSkillValue) continue;
                if (Has(perk)) continue;
                if (!allowBoth && perk.AlternativePerk != null && Has(perk.AlternativePerk)) continue;

                planned.Add(perk);
                set.Add(perk);
            }

            if (session.Mode != SetupMode.Custom)
                return planned;

            var configured = new HashSet<string>(session.CustomPerks.Keys);
            var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
            foreach (var perk in PerkObject.All)
            {
                // A skill the player configured explicitly is final; empty means
                // "no perks for this skill" by their choice
                if (configured.Contains(perk.Skill.StringId)) continue;
                if (Application.Steps.NarrativeStep.ExpectedSkillValue(session, perk.Skill) < perk.RequiredSkillValue) continue;
                if (Has(perk)) continue;
                if (perk.AlternativePerk != null && Has(perk.AlternativePerk)) continue;

                var pick = ChoosePerk(model, hero, perk);
                if (Has(pick)) pick = perk;
                if (Has(pick)) continue;

                planned.Add(pick);
                set.Add(pick);
            }

            return planned;
        }
    }
}
