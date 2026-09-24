using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The guided route branches: which situation comes next depends on what
    ///     has been answered. These cover the properties a player would notice
    ///     breaking, and one that only shows up in code review: the walk holds no
    ///     cursor, so going back and answering differently must reroute rather
    ///     than resume where it was.
    /// </summary>
    public class SceneRunTests
    {
        private static SceneOption Option(string id, params ChoiceConsequence[] consequences) =>
            new(id, "{=t}" + id, "{=p}" + id, consequences);

        private static Scene Scene(string id, Func<ISceneAnswers, bool>? appears, params string[] optionIds) =>
            new(id, "{=h}" + id, "{=q}" + id, optionIds.Select(o => Option(o)).ToList(),
                age: null, appears);

        [Fact]
        public void An_unanswered_run_starts_at_the_first_scene()
        {
            var run = new SceneRun(new[] { Scene("a", null, "a1", "a2"), Scene("b", null, "b1") });

            run.Current!.Id.Should().Be("a");
            run.IsFinished.Should().BeFalse();
            run.CanGoBack.Should().BeFalse();
        }

        [Fact]
        public void Answering_moves_on_and_a_told_life_finishes()
        {
            var run = new SceneRun(new[] { Scene("a", null, "a1"), Scene("b", null, "b1") });

            run.Answer("a1");
            run.Current!.Id.Should().Be("b");

            run.Answer("b1");
            run.Current.Should().BeNull();
            run.IsFinished.Should().BeTrue();
        }

        [Fact]
        public void A_scene_that_this_life_never_reaches_is_never_put()
        {
            var run = new SceneRun(new[]
            {
                Scene("a", null, "a1", "a2"),
                Scene("only_for_a2", answers => answers.Chose("a2"), "x1"),
                Scene("z", null, "z1")
            });

            run.Answer("a1");

            run.Current!.Id.Should().Be("z");
        }

        [Fact]
        public void Going_back_and_answering_differently_reroutes_rather_than_resumes()
        {
            var run = new SceneRun(new[]
            {
                Scene("a", null, "a1", "a2"),
                Scene("only_for_a2", answers => answers.Chose("a2"), "x1"),
                Scene("z", null, "z1")
            });

            run.Answer("a1");
            run.Current!.Id.Should().Be("z");

            run.Back();
            run.Current!.Id.Should().Be("a");

            // The branch that was closed a moment ago is now the next thing asked,
            // which is only true because the walk is a function of the answers
            run.Answer("a2");
            run.Current!.Id.Should().Be("only_for_a2");
        }

        [Fact]
        public void The_same_answers_always_give_the_same_walk()
        {
            Scene[] Scenes() => new[]
            {
                Scene("a", null, "a1", "a2"),
                Scene("b", answers => answers.Chose("a2"), "b1"),
                Scene("c", null, "c1")
            };

            var first = new SceneRun(Scenes());
            var second = new SceneRun(Scenes());

            foreach (var run in new[] { first, second })
            {
                run.Answer("a2");
                run.Answer("b1");
            }

            first.Answers.ChosenOptionIds.Should().Equal(second.Answers.ChosenOptionIds);
            first.Current!.Id.Should().Be(second.Current!.Id);
        }

        [Fact]
        public void An_option_from_another_scene_is_refused_and_changes_nothing()
        {
            var run = new SceneRun(new[] { Scene("a", null, "a1"), Scene("b", null, "b1") });

            // Refused rather than thrown: an answer to a question that was not
            // asked is a caller mistake, not a broken run, and the walk must be
            // left exactly as it was
            run.Answer("b1").Should().BeFalse();

            run.Current!.Id.Should().Be("a");
            run.Answers.ChosenOptionIds.Should().BeEmpty();
            run.CanGoBack.Should().BeFalse();
        }

        [Fact]
        public void Changing_an_earlier_answer_discards_what_followed_it()
        {
            var answers = new SceneAnswers();
            answers.Record("a", "a1");
            answers.Record("b", "b1");
            answers.Record("c", "c1");

            // Backing up to the first scene and choosing differently: the answers
            // that came after belong to a life no longer being lived, and some of
            // those scenes may not even be put again
            answers.Replace("a", "a2");

            answers.ChosenOptionIds.Should().Equal("a2");
            answers.Answered("b").Should().BeFalse();
            answers.Answered("c").Should().BeFalse();
        }

        [Fact]
        public void Consequences_travel_with_the_option_that_was_chosen()
        {
            var gate = new ChoiceConsequence(ConsequenceKind.Gate, "x1");
            var scene = new Scene("a", "{=t}a", "{=q}a", new List<SceneOption> { Option("a1", gate) }, null);
            var run = new SceneRun(new[] { scene });

            run.Answer("a1");

            scene.Find("a1")!.Consequences.Should().ContainSingle()
                .Which.Kind.Should().Be(ConsequenceKind.Gate);
        }
    }
}
