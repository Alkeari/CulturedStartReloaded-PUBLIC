using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>
    ///     What a start owes the total conversion it is played in. Runs last, because it reads the
    ///     clan the earlier steps composed: the career board is bounded by the level the resource
    ///     step set, and the family whose race it matches is the one the family step generated.
    ///
    ///     Without a conversion loaded every call here finds nothing and the step reports it did
    ///     nothing, which is also what a conversion with no career or resource system would give.
    /// </summary>
    public sealed class ConversionStep : IStartStep
    {
        public string Name => "Conversion";

        public string? Validate(StartContext context) =>
            context.Hero.Clan == null ? "hero has no clan" : null;

        public void Apply(StartContext context)
        {
            if (!TaomBridge.IsLoaded)
            {
                CSLogger.Info("ConversionStep: no conversion of ours is loaded; nothing to apply.");
                return;
            }

            var hero = context.Hero;
            var session = context.Session;

            GrantCareer(hero, session);
            GrantFactionResource(hero, session);
            CallNamedCompanions(hero, session);
            MatchClanRaces(hero);
        }

        private static void GrantCareer(Hero hero, CharacterCreation.Session.CharacterCreationSession session)
        {
            if (string.IsNullOrEmpty(session.SelectedCareerId)) return;

            TaomBridge.GrantCareer(hero.StringId, session.SelectedCareerId!, session.CareerChoiceIds, hero.Level);
        }

        private static void GrantFactionResource(Hero hero, CharacterCreation.Session.CharacterCreationSession session)
        {
            int amount = session.CustomFactionResource ?? FactionResourceFor(session);
            if (amount <= 0) return;

            var resource = TaomBridge.ResourceFor(hero.Culture?.StringId);
            if (resource == null) return;

            TaomBridge.GrantResource(hero.StringId, resource.Id, System.Math.Min(amount, resource.Cap));
        }

        /// <summary>
        ///     What the life itself is worth in the faction's own resource. It is a store a house
        ///     keeps, so it follows the standing and the means the start already settled rather than
        ///     any figure of its own, and a commoner's house keeps none.
        /// </summary>
        public static int FactionResourceFor(CharacterCreation.Session.CharacterCreationSession session) =>
            FactionResourceForTier(session, session.SelectedClanTier);

        /// <summary>The same store, for a tier a chapter is still only offering.</summary>
        public static int FactionResourceForTier(
            CharacterCreation.Session.CharacterCreationSession session, int tier)
        {
            if (tier <= 1) return 0;

            var resource = TaomBridge.ResourceFor(session.SelectedCulture?.StringId);
            if (resource == null) return 0;

            // A tenth of the cap per tier above the first, so the ceiling belongs to the
            // conversion's own configuration rather than to a number written here
            return System.Math.Min(resource.Cap, resource.Cap / 10 * (tier - 1));
        }

        /// <summary>
        ///     The conversion's own named people the start asked for. They already stand in the
        ///     world, so they are taken into the clan rather than generated: a lore character built
        ///     from a template would be a stranger wearing the name.
        /// </summary>
        private static void CallNamedCompanions(Hero hero, CharacterCreation.Session.CharacterCreationSession session)
        {
            if (session.NamedCompanionIds.Count == 0) return;

            var party = hero.PartyBelongedTo;
            if (party == null)
            {
                CSLogger.Warn("ConversionStep: no party, so the conversion's named companions cannot join.");
                return;
            }

            foreach (var id in session.NamedCompanionIds)
            {
                var companion = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);
                if (companion == null || companion.Clan == Clan.PlayerClan)
                {
                    CSLogger.Warn($"ConversionStep: '{id}' is not a hero this campaign has alive; skipped.");
                    continue;
                }

                AddCompanionAction.Apply(Clan.PlayerClan, companion);
                AddHeroToPartyAction.Apply(companion, party, true);
                CSLogger.Info($"ConversionStep: {companion.Name} rides with the clan from the first day.");
            }
        }
        /// <summary>
        ///     Everyone this start put in the clan is of the player's own race. They were built from
        ///     the culture's templates, which carry whatever race the template was written as, so an
        ///     elf could be handed human parents and a human orc siblings.
        /// </summary>
        private static void MatchClanRaces(Hero hero)
        {
            int? race = TaomBridge.RaceOf(hero.StringId);
            if (race == null) return;

            var named = TaomBridge.NamedCompanionRaces();
            var matched = new List<string>();
            foreach (var member in Clan.PlayerClan.Heroes)
            {
                if (member == hero) continue;

                // A character the conversion wrote keeps the race it wrote them as, whoever they
                // ride with. The smith of Erebor is a dwarf; putting him on the player's race makes
                // him a man with a dwarf's name, and the conversion's own config is the only thing
                // that knows which he is
                if (named.TryGetValue(member.StringId, out var writtenRace))
                {
                    RestoreWrittenRace(member, writtenRace);
                    continue;
                }

                if (TaomBridge.RaceOf(member.StringId) == race) continue;

                TaomBridge.SetRace(member.StringId, race.Value);
                matched.Add(member.Name?.ToString() ?? member.StringId);
            }

            if (matched.Count > 0)
                CSLogger.Info($"ConversionStep: {matched.Count} clan member(s) put on the player's race: {string.Join(", ", matched)}.");
        }

        /// <summary>
        ///     Puts a named character back on the race the conversion declares for them, which also
        ///     repairs one an earlier start changed.
        /// </summary>
        private static void RestoreWrittenRace(Hero member, string writtenRace)
        {
            int? race = TaomBridge.RaceIdByName(writtenRace);
            if (race == null || TaomBridge.RaceOf(member.StringId) == race) return;

            TaomBridge.SetRace(member.StringId, race.Value);
            CSLogger.Info($"ConversionStep: {member.Name} put back on the conversion's own race for them, {writtenRace}.");
        }
    }
}