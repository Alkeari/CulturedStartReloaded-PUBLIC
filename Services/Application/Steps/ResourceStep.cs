using System;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Settings;
using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>Gold, clan renown, influence, and the optional level override.</summary>
    public sealed class ResourceStep : IStartStep
    {
        public string Name => "Resources";

        public string? Validate(StartContext context)
        {
            return context.Hero.Clan == null
                ? "hero has no clan"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var session = context.Session;
            var settings = context.Settings;

            // Every value is clamped against the same live caps the Start Editor
            // uses, whatever its source: MCM ranges, a preset file, or an old
            // session. An over-cap level wraps the developer to level 0 with
            // negative experience, so this is a save-integrity boundary.
            // The figure the mod states is what the character HAS, and a total conversion may have
            // handed the hero its own starting purse before this runs, so the gold is set rather
            // than added wherever one is loaded. Consequences after this step still move it.
            int gold = EffectiveGold(session, settings);
            int carried = TaomBridge.IsLoaded ? hero.Gold : 0;
            if (gold > carried)
                GiveGoldAction.ApplyBetweenCharacters(null, hero, gold - carried, true);
            else if (gold < carried)
                GiveGoldAction.ApplyBetweenCharacters(hero, null, carried - gold, true);
            CSLogger.Info($"ResourceStep: gold={gold} (the conversion's own purse of {carried} folded in).");

            if (session.CustomRenown.HasValue || session.SelectedClanTier > 0)
            {
                int targetRenown = session.CustomRenown
                                   ?? CSSettings.GetRenownForTier(session.SelectedClanTier);
                targetRenown = Math.Max(0, Math.Min(GameCaps.MaxRenown(), targetRenown));
                float delta = targetRenown - hero.Clan.Renown;
                if (delta > 0)
                    hero.Clan.AddRenown(delta);
                CSLogger.Info($"ResourceStep: renown set to {hero.Clan.Renown}.");
            }

            int influence = EffectiveInfluence(session, settings);
            if (influence > 0)
                hero.Clan.Influence += influence;
            CSLogger.Info($"ResourceStep: influence={influence}.");

            // An exact level is the only override there is; a preset ladder used
            // to live here too, and a stale flag from it could silently install a
            // level the player never chose
            if (session.CustomLevel.HasValue)
            {
                int level = Math.Max(1, Math.Min(GameCaps.MaxHeroLevel(), session.CustomLevel.Value));
                int before = hero.Level;
                hero.HeroDeveloper.SetInitialLevel(level);
                CSLogger.Info($"ResourceStep: level override {before} -> {hero.Level} (target {level}).");
            }

            var party = hero.PartyBelongedTo;
            if (party != null)
            {
                // The game seeds the party with default provisions (a grain, a
                // mule); a composed start defines its inventory itself. The
                // vanilla route never reaches this step, so it keeps them.
                if (party.ItemRoster.Count > 0)
                {
                    for (int i = 0; i < party.ItemRoster.Count; i++)
                    {
                        var element = party.ItemRoster.GetElementCopyAtIndex(i);
                        CSLogger.Info(
                            $"ResourceStep: removed default {element.Amount}x {element.EquipmentElement.Item?.Name}.");
                    }

                    party.ItemRoster.Clear();
                }

                foreach (var food in session.CustomFood)
                {
                    party.ItemRoster.AddToCounts(food.Item, food.Count);
                    CSLogger.Info($"ResourceStep: food {food.Count}x {food.Item.Name}.");
                }

                foreach (var mount in session.CustomMounts)
                {
                    party.ItemRoster.AddToCounts(mount.Item, mount.Count);
                    CSLogger.Info($"ResourceStep: mounts {mount.Count}x {mount.Item.Name}.");
                }

                foreach (var cargo in session.CustomTradeGoods)
                {
                    party.ItemRoster.AddToCounts(cargo.Item, cargo.Count);
                    CSLogger.Info($"ResourceStep: cargo {cargo.Count}x {cargo.Item.Name}.");
                }

                // Either composed route feeds the party by plan when nothing exact
                // was loaded; an exact composition always stands alone. The plan was
                // once read on the narrative route only, which left the Start
                // Editor's own wagons empty with no way to say otherwise
                if (session.CustomFood.Count == 0 && session.CustomMounts.Count == 0)
                    ApplyProvisionPlan(party, session, settings);
            }
        }

        /// <summary>
        ///     The purse an automatic band opens, before any exact override. The
        ///     Start Editor's band row asks for a band the session does not hold
        ///     yet, so the band is a parameter rather than read from it.
        /// </summary>
        public static int GoldFor(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings, RangePreset band)
        {
            var range = settings?.GetGoldRange(session.SelectedStartType) ?? (500, 3000);
            return Math.Max(0, Math.Min(GameCaps.MaxGold, CSSettings.GetRangeValue(range, band)));
        }

        /// <summary>The purse this start really opens with.</summary>
        public static int EffectiveGold(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings) =>
            session.CustomGold.HasValue
                ? Math.Max(0, Math.Min(GameCaps.MaxGold, session.CustomGold.Value))
                : GoldFor(session, settings, session.SelectedGold);

        /// <summary>The influence an automatic band opens, before any exact override.</summary>
        public static int InfluenceFor(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings, RangePreset band)
        {
            var range = settings?.GetInfluenceRange(session.SelectedStartType) ?? (0, 0);
            return Math.Max(0, Math.Min(GameCaps.MaxInfluence, CSSettings.GetRangeValue(range, band)));
        }

        /// <summary>
        ///     The influence this start really opens with. A start type that does
        ///     not deal in influence is granted none whatever the band says, so the
        ///     row that states the figure and the apply that grants it agree.
        /// </summary>
        public static int EffectiveInfluence(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings)
        {
            if (session.CustomInfluence.HasValue)
                return Math.Max(0, Math.Min(GameCaps.MaxInfluence, session.CustomInfluence.Value));
            return CSSettings.UsesInfluence(session.SelectedStartType)
                ? InfluenceFor(session, settings, session.SelectedInfluence)
                : 0;
        }

        /// <summary>The muster an automatic band raises, before any exact override.</summary>
        public static int TroopsFor(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings, RangePreset band)
        {
            var range = settings?.GetTroopsRange(session.SelectedStartType) ?? (0, 20);
            return CSSettings.GetRangeValue(range, band);
        }

        /// <summary>
        ///     The muster this start really raises, before the party limit trims the
        ///     column, which is the figure the provision load is sized against.
        /// </summary>
        public static int EffectiveTroops(CharacterCreation.Session.CharacterCreationSession session,
            CSSettings? settings) =>
            session.CustomTroops ?? TroopsFor(session, settings, session.SelectedTroops);

        /// <summary>
        ///     What a provision plan actually loads, as one answer both the chapter
        ///     that offers the plan and the step that applies it read.
        ///
        ///     The muster is the rung's own figure, BEFORE the party limit trims the
        ///     column, which is why it can stand above the soldier count the warband
        ///     chapter showed. The panel says so rather than leaving the player to
        ///     reconcile two numbers.
        /// </summary>
        public readonly struct ProvisionLoad
        {
            public ProvisionLoad(int grain, int packAnimals, int muster)
            {
                Grain = grain;
                PackAnimals = packAnimals;
                Muster = muster;
            }

            public int Grain { get; }
            public int PackAnimals { get; }
            public int Muster { get; }
        }

        /// <summary>
        ///     Grain scaled to the expected muster and a pack animal per fifteen
        ///     mouths; Light packs a satchel, Bare packs nothing at all.
        /// </summary>
        public static ProvisionLoad PlanLoad(
            CharacterCreation.Session.CharacterCreationSession session, CSSettings? settings) =>
            PlanLoad(session, settings, session.Provisions);

        /// <summary>
        ///     The same, for a plan the session does not hold yet, so the Start
        ///     Editor's row can state what each plan loads without setting one.
        /// </summary>
        public static ProvisionLoad PlanLoad(
            CharacterCreation.Session.CharacterCreationSession session, CSSettings? settings,
            Models.ProvisionPlan plan)
        {
            int muster = EffectiveTroops(session, settings);

            switch (plan)
            {
                case Models.ProvisionPlan.Bare:
                    return new ProvisionLoad(0, 0, muster);
                case Models.ProvisionPlan.Light:
                    return new ProvisionLoad(5, 0, muster);
                default:
                    return new ProvisionLoad(Math.Max(10, muster), Math.Max(1, muster / 15), muster);
            }
        }

        private static void ApplyProvisionPlan(TaleWorlds.CampaignSystem.Party.MobileParty party,
            CharacterCreation.Session.CharacterCreationSession session, CSSettings? settings)
        {
            if (session.Provisions == Models.ProvisionPlan.Bare) return;

            var load = PlanLoad(session, settings);

            var grain = TaleWorlds.ObjectSystem.MBObjectManager.Instance
                .GetObject<TaleWorlds.Core.ItemObject>("grain");
            if (grain != null)
                party.ItemRoster.AddToCounts(grain, load.Grain);

            if (load.PackAnimals > 0)
            {
                var packAnimal = TaleWorlds.ObjectSystem.MBObjectManager.Instance
                                     .GetObject<TaleWorlds.Core.ItemObject>("mule")
                                 ?? TaleWorlds.ObjectSystem.MBObjectManager.Instance
                                     .GetObject<TaleWorlds.Core.ItemObject>("sumpter_horse");
                if (packAnimal != null)
                    party.ItemRoster.AddToCounts(packAnimal, load.PackAnimals);
            }

            CSLogger.Info($"ResourceStep: provision plan {session.Provisions} applied ({load.Grain} grain).");
        }
    }
}
