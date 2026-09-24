using System;

namespace CulturedStartReloaded.Models
{
    /// <summary>
    ///     How much a chapter matters. Options within one chapter are always of
    ///     equal severity to each other, because a chapter asks one question and
    ///     every answer to it should cost the same to give. Chapters differ from
    ///     one another, and a chapter that decides a life should visibly outweigh
    ///     one that decides a habit.
    /// </summary>
    public enum Severity
    {
        /// <summary>A coloring: it shows in the telling and barely in the sheet.</summary>
        Habit,

        /// <summary>An ordinary turn of a life.</summary>
        Formative,

        /// <summary>A hinge. What follows is different because of it.</summary>
        Defining
    }

    /// <summary>What a life-path choice can leave behind.</summary>
    public enum ConsequenceKind
    {
        /// <summary>Coin owed to someone who expects it back.</summary>
        Debt,

        /// <summary>A faction, clan or house that remembers you badly.</summary>
        Enmity,

        /// <summary>A faction, clan or house that remembers you well.</summary>
        Goodwill,

        /// <summary>A named person who comes with you.</summary>
        Ally,

        /// <summary>A place the story ties you to.</summary>
        Place,

        /// <summary>A style you carry, and that others use.</summary>
        Title,

        /// <summary>Years taken out of your life and not spent on skill.</summary>
        LostYears,

        /// <summary>
        ///     The age the character starts at, in years, said outright rather than
        ///     arrived at. Cultured Start's age chapter is the only thing that
        ///     carries one: on that route the age is the player's answer and nothing
        ///     else reads into it, which is what the route always did. Cultured
        ///     Start Revamped reads the age off the whole life instead and declares
        ///     none of these.
        /// </summary>
        Age,

        /// <summary>Something in your hands when you ride out.</summary>
        Item,

        /// <summary>A movement in a personality trait, with a size.</summary>
        Trait,

        /// <summary>An option elsewhere this life rules out.</summary>
        Gate,

        /// <summary>
        ///     Somebody the life settles in the character's household, or takes
        ///     out of it: a parent in the ground, a sibling raised beside them, a
        ///     spouse. The household chapters compose the house out of these, so
        ///     an answer that decides one says so here rather than being read back
        ///     off its own option id, which no panel could state.
        /// </summary>
        Household
    }

    /// <summary>
    ///     One thing a choice leaves behind. This is the record the old model could
    ///     not carry: it could express skills, one attribute, a focus weight, a
    ///     trait pair, a relation bucket and an heirloom, and nothing else, so a
    ///     debt, an enemy, a place, a title or a lost year had nowhere to live and
    ///     no chapter could change what a later chapter offered.
    ///
    ///     Deliberately one flat shape with a kind rather than a hierarchy. A
    ///     consequence is data the catalog declares and the apply pipeline reads;
    ///     it never behaves, so nothing here needs to be polymorphic.
    /// </summary>
    public sealed class ChoiceConsequence
    {
        public ChoiceConsequence(ConsequenceKind kind, string? target = null, int amount = 0,
            string? note = null)
        {
            Kind = kind;
            Target = target;
            Amount = amount;
            Note = note;
        }

        public ConsequenceKind Kind { get; }

        /// <summary>
        ///     What the consequence is about, in the vocabulary of its kind: a trait
        ///     id, an item id, a culture or faction id, an option id for a gate. Null
        ///     where the kind needs no target, as a debt to nobody in particular does.
        /// </summary>
        public string? Target { get; }

        /// <summary>
        ///     How much, in the units of its kind: denars for a debt, years for lost
        ///     years, a signed delta for a trait, a count for items. Zero where the
        ///     kind carries no amount.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        ///     One sentence the epilogue and the option description can use, so a
        ///     consequence is never applied without the player having been told.
        ///     A localization id plus fallback, or null to say nothing.
        /// </summary>
        public string? Note { get; }

        public override string ToString() =>
            $"{Kind}({Target ?? "-"}, {Amount})";
    }
}
