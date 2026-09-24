using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services.Application;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     What the panel promises against what the pipeline can actually hand
    ///     over.
    ///
    ///     The player never infers anything: the panel states the
    ///     exact effect of the answer they are on, and the mod never
    ///     reports a result that is not real. Between the two of them sits the
    ///     failure this file exists for, and it is the one that has broken four
    ///     separate ways in one playtest: the panel states an effect correctly,
    ///     derived from the option's own consequences, and then the step that
    ///     applies it has never heard of the id. Nothing throws. A line goes in
    ///     the log. The player reads a promise and the character arrives without
    ///     it.
    ///
    ///     Every id the catalog can promise is checked against the table of the
    ///     step that owns that kind, read out of that step's own file.
    /// </summary>
    public class GrantVocabularyTests
    {
        private const string ConsequenceStep = "Services/Application/Steps/ConsequenceStep.cs";
        private const string CompanionGenerator = "Services/CompanionGenerator.cs";
        private const string ScenarioStep = "Services/Application/Steps/ScenarioStep.cs";
        private const string HeroLore = "Services/HeroLore.cs";
        private const string ChoiceEffects = "Services/ChoiceEffects.cs";
        private const string LifeProfileScenes = "Services/LifeProfileScenes.cs";

        private static IEnumerable<ChoiceConsequence> Promised =>
            SceneCatalog.All.SelectMany(scene => scene.Options).SelectMany(option => option.Consequences);

        private static IReadOnlyCollection<string> Targets(ConsequenceKind kind) =>
            Promised.Where(c => c.Kind == kind && c.Target != null)
                .Select(c => c.Target!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        [Fact]
        public void Every_item_the_panel_promises_is_one_the_pipeline_can_hand_over()
        {
            // ConsequenceStep.ApplyItems resolves an id through three tables and
            // logs "no item found" for anything none of them knows, which is the
            // promised thing dropped on the floor with the player still holding
            // the sentence that promised it
            var known = new HashSet<string>(StringComparer.Ordinal);
            known.UnionWith(ModSource.IdsIn(ConsequenceStep, "WeaponClassChoice? WeaponClassFor"));
            known.UnionWith(ModSource.IdsIn(ConsequenceStep, "ItemObject? ResolveItem"));
            known.UnionWith(ModSource.IdsIn(ConsequenceStep, "private static void ApplyItems"));

            Targets(ConsequenceKind.Item).Where(id => !known.Contains(id))
                .Should().BeEmpty("every item an answer promises has to be one ConsequenceStep can resolve");
        }

        [Fact]
        public void Every_ally_the_panel_promises_is_somebody_the_pipeline_builds()
        {
            // CompanionGenerator.GenerateAllies logs "no profile for ally" and
            // builds nobody. The player was told this person left with them
            var roster = ModSource.IdsMatching(CompanionGenerator, @"new AllyProfile\(""([a-z_]+)""");

            Targets(ConsequenceKind.Ally).Where(id => !roster.Contains(id))
                .Should().BeEmpty("an ally with no profile is collected by the outcome and never built");
        }

        [Fact]
        public void Every_impression_the_panel_promises_reaches_somebody()
        {
            // SceneOutcome maps an unknown group to RelationEffect.None and drops
            // it without a word, so a group id nobody has wired up is goodwill the
            // panel states and nothing ever applies
            foreach (string group in Targets(ConsequenceKind.Goodwill).Concat(Targets(ConsequenceKind.Enmity)))
            {
                var outcome = SceneOutcome.From(new[]
                {
                    new ChoiceConsequence(ConsequenceKind.Goodwill, group, 1)
                });

                outcome.Relations.Should().NotBeEmpty($"goodwill with {group} reaches nobody");
            }
        }

        [Fact]
        public void Every_impression_the_pipeline_can_carry_is_one_it_knows_where_to_find()
        {
            // The other direction: a relation the outcome carries and
            // ConsequenceStep.FindRelationTargets has no case for falls through to
            // an empty list, which is the same silent nothing
            var found = ModSource.IdsMatching(ConsequenceStep,
                @"case RelationEffect\.(\w+)").Select(name => name).ToList();

            foreach (RelationEffect effect in Enum.GetValues(typeof(RelationEffect)))
            {
                if (effect == RelationEffect.None) continue;
                found.Should().Contain(effect.ToString(),
                    $"{effect} is a relation the outcome can carry and nothing looks for the people");
            }
        }

        [Fact]
        public void Every_station_a_life_can_reach_is_one_the_pipeline_can_build()
        {
            // ScenarioStep.Validate returns "unknown start type" and the whole
            // start is skipped, which is a character who finished the route and
            // arrived as nobody
            var built = ModSource.IdsMatching(ScenarioStep, @"\[StartType\.(\w+)\]");

            foreach (var life in LifeHarness.Lives())
                built.Should().Contain(life.Reading.Station.ToString(),
                    $"a life reads as {life.Reading.Station} and no applier builds one: {life.Trail}");
        }

        [Fact]
        public void Every_place_whose_amount_the_reading_takes_is_one_the_panel_measures()
        {
            // Two answers of the port scene both print "Place: the open water" and
            // carry five and three, and the seat on the water is gated at five, so
            // one identical line stood in front of two different answers and the
            // player had to guess which. The line says how much now, and the rule
            // that keeps it honest runs both ways: a place the reading weighs and
            // the panel does not is the same silent guess by another name, and a
            // place the panel weighs and the reading does not is a figure beside
            // something nothing downstream reads
            var weighed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arm in Arms(ModSource.MemberBody(LifeProfileScenes, "void AddPlace")))
                if (arm.Value.IndexOf("amount", StringComparison.Ordinal) >= 0)
                    foreach (string id in arm.Key)
                        weighed.Add(id);

            var said = new HashSet<string>(
                System.Text.RegularExpressions.Regex
                    .Matches(ModSource.MemberBody(HeroLore, "string? WaterText"), @"""([a-z_]+)""")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => match.Groups[1].Value),
                StringComparer.Ordinal);

            said.Should().BeEquivalentTo(weighed,
                "the panel says how much of a life a place is for exactly the places the " +
                "reading takes an amount from, and for no others");

            var carried = new HashSet<string>(Promised
                .Where(c => c.Kind == ConsequenceKind.Place && c.Amount != 0)
                .Select(c => c.Target!), StringComparer.Ordinal);

            carried.Should().BeSubsetOf(weighed,
                "the catalog writes an amount beside a place that nothing reads, so two answers " +
                "the author meant to differ are the same answer");
        }

        /// <summary>
        ///     A switch body as its arms: the ids that fall through to one block,
        ///     against that block's text. Several labels share one arm, and which
        ///     ids those are is the whole of what the rule above asks.
        /// </summary>
        private static IEnumerable<KeyValuePair<List<string>, string>> Arms(string body)
        {
            var labels = new List<string>();
            var arm = new System.Text.StringBuilder();

            foreach (string raw in body.Split('\n'))
            {
                string line = raw.Trim();
                var label = System.Text.RegularExpressions.Regex.Match(line, @"^case ""([a-z_]+)"":$");

                if (label.Success)
                {
                    if (arm.Length > 0)
                    {
                        yield return new KeyValuePair<List<string>, string>(labels, arm.ToString());
                        labels = new List<string>();
                        arm.Clear();
                    }

                    labels.Add(label.Groups[1].Value);
                    continue;
                }

                // A comment sits above the case it explains rather than inside the
                // one before it, so reading it as the previous arm's text puts the
                // next arm's word in the wrong arm
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;

                if (labels.Count > 0) arm.Append(line).Append('\n');
            }

            if (arm.Length > 0) yield return new KeyValuePair<List<string>, string>(labels, arm.ToString());
        }

        [Theory]
        [InlineData("string? ItemText", ConsequenceKind.Item)]
        [InlineData("string? PlaceText", ConsequenceKind.Place)]
        [InlineData("string? AllyText", ConsequenceKind.Ally)]
        [InlineData("string? TitleText", ConsequenceKind.Title)]
        public void Everything_a_life_carries_has_a_sentence_on_the_encyclopedia_page(
            string table, ConsequenceKind kind)
        {
            // HeroLore composes the character's own encyclopedia entry out of what
            // the life left behind, and silently leaves out anything its tables
            // have no English for. A blank line on that page is the plainest sign
            // the character was made rather than found, which is the whole reason
            // the entry is written
            var written = ModSource.SentencesIn(HeroLore, table);

            Targets(kind).Where(id => !written.ContainsKey(id))
                .Should().BeEmpty($"HeroLore.{table} says nothing about it");
        }

    }
}
