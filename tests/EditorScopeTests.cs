using System;
using CulturedStartReloaded.CharacterCreation.Editor;
using FluentAssertions;
using Xunit;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The Start Editor stands in for a single narrative option, and the scope is what stops it
    ///     answering more than the question that was asked. Getting this wrong is not cosmetic: an
    ///     unscoped editor opened from one chapter lets the player rewrite everything the story just
    ///     decided, and one click on Presets replaces the character outright.
    /// </summary>
    public class EditorScopeTests
    {
        [Fact]
        public void Full_allows_every_tab_and_is_not_scoped()
        {
            EditorScope.Full.IsScoped.Should().BeFalse();
            EditorScope.Full.Tabs.Should().BeNull();
            EditorScope.Full.Allows("presets").Should().BeTrue();
            EditorScope.Full.Allows("anything at all").Should().BeTrue();
        }

        [Fact]
        public void A_scoped_editor_allows_only_the_tabs_it_names()
        {
            var scope = EditorScope.For("{=title}Loading the Wagons", "stores");

            scope.IsScoped.Should().BeTrue();
            scope.Allows("stores").Should().BeTrue();
            scope.Allows("presets").Should().BeFalse();
            scope.Allows("gear").Should().BeFalse();
        }

        [Fact]
        public void Tab_matching_is_case_sensitive_so_a_mistyped_key_fails_closed()
        {
            var scope = EditorScope.For("{=title}Stores", "stores");

            scope.Allows("Stores").Should().BeFalse();
            scope.Allows("STORES").Should().BeFalse();
        }

        [Fact]
        public void A_scoped_editor_carries_the_title_that_names_the_question()
        {
            EditorScope.For("{=title}The Warband", "warband").TitleKey.Should().Be("{=title}The Warband");
            EditorScope.Full.TitleKey.Should().BeNull();
        }

        [Fact]
        public void Several_tabs_are_all_allowed()
        {
            var scope = EditorScope.For("{=title}Stats", "attributes", "skills", "perks");

            scope.Allows("attributes").Should().BeTrue();
            scope.Allows("skills").Should().BeTrue();
            scope.Allows("perks").Should().BeTrue();
            scope.Allows("traits").Should().BeFalse();
        }

        [Fact]
        public void A_scope_with_no_tabs_is_refused_rather_than_silently_empty()
        {
            // An empty scope would render an editor with no tabs at all, which is a dead screen.
            Action noTabs = () => EditorScope.For("{=title}Nothing");
            Action nullTabs = () => EditorScope.For("{=title}Nothing", null!);

            noTabs.Should().Throw<ArgumentException>();
            nullTabs.Should().Throw<ArgumentException>();
        }
    }
}
