using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CulturedStartReloaded.CharacterCreation.Catalog;
using CulturedStartReloaded.CharacterCreation.Scenes;
using CulturedStartReloaded.Models;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The household chapters answer every question the player leaves alone out
    ///     of the life, and <c>HouseholdMenu.DerivedHouse</c> is the whole of how:
    ///     one switch over what the scenes DECLARED about the house.
    ///
    ///     It used to be three tables of option ids, which the effect panel could
    ///     not read, so an answer that buried both parents or left the character
    ///     married said nothing beside itself. The catalog declares a
    ///     <see cref="ConsequenceKind.Household"/> now and both halves read it.
    ///
    ///     That file cannot be compiled here, since it names engine types
    ///     throughout, and this project has to stay clear of them. A target it
    ///     names that the catalog never declares fails silently: the arm is never
    ///     taken, the question falls through to the shape it would have had anyway,
    ///     and the player is handed a household their life did not produce. Nothing
    ///     throws and nothing is logged. So the targets are read back out of the
    ///     file itself and checked against the catalog, in both directions.
    /// </summary>
    public class HouseholdDerivationTests
    {
        private const string HouseholdMenu = "CharacterCreation/Menus/HouseholdMenu.cs";

        private const string FamilyStep = "Services/Application/Steps/FamilyStep.cs";

        /// <summary>Every household fact the chapters know how to compose.</summary>
        private static IReadOnlyCollection<string> Composed =>
            ModSource.IdsMatching(HouseholdMenu, @"case ""([a-z0-9_]+)"":");

        /// <summary>
        ///     Every household fact the apply pipeline builds instead. The three
        ///     chapters ask about parents, siblings and a hearth, so a fact above
        ///     all three has nowhere to be composed and lands in the family tree.
        /// </summary>
        private static IReadOnlyCollection<string> Built =>
            ModSource.IdsMatching(FamilyStep, @"Forebears\w* = ""([a-z0-9_]+)""");

        /// <summary>Every household fact an answer in the run can declare.</summary>
        private static HashSet<string> Declared =>
            SceneCatalog.All.SelectMany(scene => scene.Options)
                .SelectMany(option => option.Consequences)
                .Where(c => c.Kind == ConsequenceKind.Household && c.Target != null)
                .Select(c => c.Target!)
                .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        ///     The same, with the two markers opened out into the facts they stand
        ///     for. A marker is what an answer writes down; the facts below it are
        ///     what the answer settles once it has read the life, and the household
        ///     folds those rather than the marker.
        /// </summary>
        private static HashSet<string> DeclaredOrStoodFor =>
            Declared.SelectMany(target =>
                    StandsFor.TryGetValue(target, out var either)
                        ? either.Concat(new[] { target })
                        : new[] { target })
                .ToHashSet(StringComparer.Ordinal);

        [Fact]
        public void Every_fact_the_household_composes_is_one_an_answer_can_declare()
        {
            Composed.Where(target => !DeclaredOrStoodFor.Contains(target))
                .Should().BeEmpty("the household would compose a fact no answer in the run ever leaves");
        }

        [Fact]
        public void Every_fact_the_family_step_builds_is_one_an_answer_can_declare()
        {
            Built.Where(target => !Declared.Contains(target))
                .Should().BeEmpty("the pipeline builds a household fact no answer in the run ever leaves");
        }

        [Fact]
        public void Every_fact_an_answer_declares_is_one_something_acts_on()
        {
            // The direction that actually breaks a player's start: the panel states
            // the declaration, so a target nothing reads is a sentence read and a
            // character it never reaches. The chapters read the three questions
            // they ask and the family step reads what stands above them, and
            // between them they have to cover every fact an answer can declare
            Declared.Where(target => !Composed.Contains(target) && !Built.Contains(target))
                .Should().BeEmpty("an answer states a household nothing downstream of it builds");
        }

        [Fact]
        public void The_life_can_say_this_character_married()
        {
            // The gap this closed: the hearth was the one question of the three
            // that no life could ever answer, so every spouse and every child on
            // the guided route came from a player overruling their own life
            Declared.Should().Contain("a_spouse");
            Composed.Should().Contain("a_spouse",
                "nothing else in the household file puts a person in the character's own house");
        }

        [Fact]
        public void A_marriage_is_something_a_life_can_leave_out()
        {
            // A marriage the life implies is the point; one every character is
            // handed is not. The answer sits in a scene with seven other answers,
            // so most lives walk past it
            var scene = SceneCatalog.All.Single(s => s.Find("cs_opt_you_married_that_winter") != null);
            scene.Options.Should().HaveCountGreaterThan(2);

            var lives = LifeHarness.Lives();
            int married = lives.Count(life =>
                life.Chosen.Any(option => option.Id == "cs_opt_you_married_that_winter"));

            married.Should().BeGreaterThan(0, "no walked life ever reaches the answer");
            married.Should().BeLessThan(lives.Count, "every walked life marries, which is not a choice");
        }

        /// <summary>
        ///     What a burial reads as in an answer's own prose. A grave is the one
        ///     household fact a scene can settle without the player having met it
        ///     in the writing, because a sibling and a marriage are things the
        ///     prose is about and a death is a thing the prose can simply omit.
        /// </summary>
        private static readonly string[] SaidPlainly =
        {
            "buried", "in the ground", "into the ground", "living", "alive"
        };

        /// <summary>
        ///     Everything one answer can put in front of a player: its title and
        ///     each writing of its prose, since a variant is what some share of the
        ///     lives that give the answer actually read.
        /// </summary>
        private static IEnumerable<string> Writings(SceneOption option) =>
            new[] { option.Prose }
                .Concat(option.Variants.Select(variant => variant.Text))
                .Select(prose => option.Title + " " + prose);

        [Fact]
        public void An_answer_that_buries_a_parent_says_so_in_its_own_writing()
        {
            // Three answers declared a grave the prose beside them never dug, so
            // the panel line arrived out of nowhere and read as a bug. The deaths
            // are the answer's to own: whichever parent an answer settles, the
            // writing the player reads on that answer settles them too. Every
            // writing of it, since an answer written a second time for the lives
            // that buried somebody already is exactly where a grave goes quiet
            var parents = new[]
            {
                "father_buried", "mother_buried", "both_parents_buried",
                "forebears_buried", "both_parents_living",
                StoryGraves.Declares, StoryHome.Declares
            };

            var silent = SceneCatalog.All.SelectMany(scene => scene.Options)
                .Where(option => option.Consequences.Any(c =>
                    c.Kind == ConsequenceKind.Household && parents.Contains(c.Target)))
                .SelectMany(option => Writings(option).Select(writing => (option.Id, Writing: writing)))
                .Where(written => !SaidPlainly.Any(said =>
                    written.Writing.ToLowerInvariant().Contains(said)))
                .Select(written => written.Id);

            silent.Should().BeEmpty("the answer states who is in the ground, so its own writing has to");
        }

        /// <summary>
        ///     The one declared fact that is not itself a line in the vocabulary.
        ///     Its answer names a sibling and keeps back which kind, so the run
        ///     settles that and the panel prints whichever of the two it settled
        ///     on; both therefore have to be written.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> StandsFor =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["a_sibling"] = new[] { "a_brother", "a_sister" },
                [StoryGraves.Declares] = new[]
                {
                    "both_parents_buried", "father_buried", "mother_buried"
                },
                [StoryHome.Declares] = new[] { "both_parents_living" }
            };

        [Fact]
        public void The_house_an_answer_settles_is_stated_beside_it()
        {
            // The panel promise in the one place it was broken: an answer that decides who
            // is in the character's house has a sentence for it in the shared
            // vocabulary, so the panel can print what the answer is about to do
            var written = ModSource.SentencesIn("Services/HeroLore.cs", "HouseText");

            Declared.SelectMany(target =>
                    StandsFor.TryGetValue(target, out var either) ? either : new[] { target })
                .Where(target => !written.ContainsKey(target))
                .Should().BeEmpty("LifeVocabulary.HouseText says nothing about it, so the panel prints nothing");
        }

        [Fact]
        public void The_answer_that_leaves_a_sibling_unnamed_keeps_its_writing_that_way()
        {
            // The counterpart to the burial check above, and the reason the fact it
            // declares stands for two: this answer is ABOUT a person it refuses to
            // name, which is the whole of what the player is answering. The panel
            // names them, because the panel is where a player is told exactly what
            // they are getting; the prose does not, and an edit that made it name
            // them would be writing a different answer
            var option = SceneCatalog.All.SelectMany(scene => scene.Options)
                .Single(o => o.Consequences.Any(c =>
                    c.Kind == ConsequenceKind.Household && c.Target == "a_sibling"));

            string writing = (option.Title + " " + option.Prose).ToLowerInvariant();

            writing.Should().NotContain("brother");
            writing.Should().NotContain("sister");
        }

        #region The graves the last scene finds already dug

        [Fact]
        public void The_answer_that_reads_the_parents_is_the_one_the_catalog_writes()
        {
            // Two constants and a literal in the catalog have to name one answer
            // and one fact. The catalog is a transcription, so it carries the
            // literal the script does; a rename on either side that missed the
            // other would leave the fold reading a target nothing declares, and
            // the arm would simply never be taken
            var option = SceneCatalog.All.SelectMany(scene => scene.Options)
                .Single(o => o.Consequences.Any(c =>
                    c.Kind == ConsequenceKind.Household && c.Target == StoryGraves.Declares));

            option.Id.Should().Be(StoryGraves.DeclaredBy);
            StoryGraves.Declares.Should().Be("parents_by_then");
        }

        [Fact]
        public void The_answer_that_sends_the_coin_home_is_the_one_the_catalog_writes()
        {
            // The same pinning for the second answer that reads its own life
            var option = SceneCatalog.All.SelectMany(scene => scene.Options)
                .Single(o => o.Consequences.Any(c =>
                    c.Kind == ConsequenceKind.Household && c.Target == StoryHome.Declares));

            option.Id.Should().Be(StoryHome.DeclaredBy);
            StoryHome.Declares.Should().Be("parents_still_standing");
        }

        [Theory]
        // Nothing said about them, so the coin finds them both living, which is
        // what that answer always said
        [InlineData("", "both_parents_living")]
        // The life has already buried one of them or both, so the answer speaks to
        // neither and the coin goes to whoever is left
        [InlineData("father_buried", null)]
        [InlineData("mother_buried", null)]
        [InlineData("both_parents_buried", null)]
        // Facts about other people in the house say nothing about the parents
        [InlineData("a_brother>a_spouse>forebears_buried", "both_parents_living")]
        // What the life declares AFTER the coin went is no part of what the coin
        // found: the last scene's marker sits behind this one in every life
        [InlineData("parents_still_standing>parents_by_then", "both_parents_living")]
        public void The_coin_sent_home_finds_them_living_only_where_the_life_is_silent(
            string said, string? settled)
        {
            StoryHome.Settled(said.Length == 0 ? new string[0] : said.Split('>'))
                .Should().Be(settled);
        }

        [Theory]
        // The pair of them, as the life left them when the coin went
        [InlineData("", true, true)]
        [InlineData("father_buried", false, true)]
        [InlineData("mother_buried", true, false)]
        [InlineData("both_parents_buried", false, false)]
        [InlineData("both_parents_buried>father_buried", false, false)]
        public void The_coin_goes_to_whichever_of_them_the_life_left_standing(
            string said, bool father, bool mother)
        {
            StoryHome.Standing(said.Length == 0 ? new string[0] : said.Split('>'))
                .Should().Be((father, mother));
        }

        [Theory]
        // Nothing said about them, which is what the answer was written for
        [InlineData("", "both_parents_buried")]
        // The life buried both already, so this is the same two graves
        [InlineData("both_parents_buried", "both_parents_buried")]
        // One of them is already down, so the answer settles the other
        [InlineData("father_buried", "mother_buried")]
        [InlineData("mother_buried", "father_buried")]
        // The life says in so many words that both are living, so this settles
        // neither: being called old stands either way
        [InlineData("both_parents_living", null)]
        [InlineData("father_buried>both_parents_living", null)]
        // Facts about other people in the house say nothing about the parents
        [InlineData("a_brother>a_spouse>forebears_buried", "both_parents_buried")]
        public void The_last_scene_settles_whoever_the_life_left_above_ground(string said, string? settled)
        {
            StoryGraves.Settled(said.Length == 0 ? new string[0] : said.Split('>'))
                .Should().Be(settled);
        }

        /// <summary>
        ///     The parent arms of <c>HouseholdMenu.DerivedHouse</c>, read out of the
        ///     file. The fold below is a transcription of them, and a transcription
        ///     is worth nothing if the original can move under it: these two
        ///     assertions are what make the walked lives an assertion about the
        ///     shipped fold rather than about a copy of it.
        /// </summary>
        private static string ParentArm(string target)
        {
            string source = File.ReadAllText(ModSource.Path(HouseholdMenu.Split('/')));
            var arm = Regex.Match(source, @"case """ + target + @""":(?<body>.*?)break;",
                RegexOptions.Singleline);

            arm.Success.Should().BeTrue($"{HouseholdMenu} no longer folds {target}");
            return arm.Groups["body"].Value;
        }

        [Fact]
        public void Burying_one_parent_says_nothing_at_all_about_the_other()
        {
            // The defect this closed: both arms wrote both flags, so an answer
            // that buried a father asserted a living mother, and a life that had
            // already buried her ended with her alive and the scene that put her
            // in the ground quietly untrue
            ParentArm("father_buried").Should().NotContain("mother");
            ParentArm("mother_buried").Should().NotContain("father");
        }

        /// <summary>
        ///     The parents as the composed household ends up holding them,
        ///     transcribed from <c>HouseholdMenu.DerivedHouse</c>, which names
        ///     engine types and so cannot be compiled here.
        /// </summary>
        /// <summary>
        ///     A marker resolved against the life that declared it. Two of the
        ///     facts are read off the run rather than carried, and that reading is
        ///     real logic, so it is asked of the real code rather than restated.
        /// </summary>
        private static string? Resolved(string fact, IReadOnlyList<string> said)
        {
            if (fact == StoryGraves.Declares) return StoryGraves.Settled(said);
            if (fact == StoryHome.Declares) return StoryHome.Settled(said);

            return fact;
        }

        private static (bool Father, bool Mother) Parents(IReadOnlyList<string> said)
        {
            bool father = true;
            bool mother = true;

            foreach (string settled in said)
                switch (Resolved(settled, said))
                {
                    case "both_parents_living":
                        father = true;
                        mother = true;
                        break;

                    case "both_parents_buried":
                        father = false;
                        mother = false;
                        break;

                    case "father_buried":
                        father = false;
                        break;

                    case "mother_buried":
                        mother = false;
                        break;
                }

            return (father, mother);
        }

        /// <summary>Every parent fact one life was told, in the order it was told.</summary>
        private static IReadOnlyList<string> ParentFacts(Life life) =>
            life.Left.Where(c => c.Kind == ConsequenceKind.Household && c.Target != null)
                .Select(c => c.Target!)
                .ToList();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void No_life_is_told_a_parent_fact_its_finished_household_disproves(bool warSails)
        {
            // A panel line the finished character contradicts is the route lying
            // to the player, and the silent direction of it is the worse one:
            // nothing on screen disagrees, the earlier promise simply stops being
            // true. Every life in the run, with nothing left out: "Nothing. It
            // went where it was needed" used to state both parents living over
            // whatever the life had already buried and was excluded here for it,
            // and it reads the life now the way the last scene does
            var broken = new List<string>();

            foreach (var life in LifeHarness.Lives(SceneCatalog.Build(warSails)))
            {
                var said = ParentFacts(life);
                var (father, mother) = Parents(said);

                foreach (string fact in said)
                    switch (Resolved(fact, said))
                    {
                        case "both_parents_living" when !father || !mother:
                        case "both_parents_buried" when father || mother:
                        case "father_buried" when father:
                        case "mother_buried" when mother:
                            broken.Add($"{fact}: {life.Trail}");
                            break;
                    }
            }

            broken.Take(3).Should().BeEmpty();
        }

        #endregion
    }
}
