using System.Collections.Generic;
using CulturedStartReloaded.Helpers;
using StoryMode.StoryModeObjects;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services.Application.Steps
{
    /// <summary>Propagates the player's culture to the immediate family, cosmetically.</summary>
    public sealed class CultureStep : IStartStep
    {
        public string Name => "Culture";

        public string? Validate(StartContext context)
        {
            return context.Hero.Culture == null
                ? "hero has no culture"
                : null;
        }

        public void Apply(StartContext context)
        {
            var hero = context.Hero;
            var culture = hero.Culture;
            var family = new List<Hero>();

            void Add(Hero? member)
            {
                if (member != null && member != hero && !family.Contains(member))
                    family.Add(member);
            }

            Add(hero.Father);
            Add(hero.Mother);
            Add(hero.Spouse);

            if (hero.Children != null)
                foreach (var child in hero.Children)
                    Add(child);

            if (hero.Siblings != null)
                foreach (var sibling in hero.Siblings)
                    Add(sibling);

            if (CSGameModeService.IsStoryMode())
            {
                Add(StoryModeHeroes.LittleBrother);
                Add(StoryModeHeroes.LittleSister);
            }

            int updated = 0;
            foreach (var member in family)
                if (member.Culture != culture)
                {
                    member.Culture = culture;
                    updated++;
                }

            CSLogger.Info($"CultureStep: updated culture on {updated}/{family.Count} family members.");
        }
    }
}
