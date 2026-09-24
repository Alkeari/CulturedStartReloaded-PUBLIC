using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     A birth date for a hero of a given age. `CampaignTime.YearsFromNow(-age)`
    ///     is measured from the moment the campaign begins, so every hero born that
    ///     way shares a birthday: open the encyclopedia on a clan built at creation
    ///     and the whole family, and every sworn house, celebrates on the same day.
    ///     The day is therefore scattered across the year before that mark, which
    ///     leaves the hero the age that was asked for or a few months more.
    ///
    ///     The scatter is a FRACTION OF A YEAR and never a count of days. A campaign
    ///     year is 84 days in the base game and a conversion may make it shorter
    ///     still, so an earlier version that subtracted up to 364 days was
    ///     subtracting years: a child asked to be two was born fourteen years back.
    /// </summary>
    public static class BirthDates
    {
        private const int Steps = 1000;

        public static CampaignTime ForAge(int age)
        {
            float withinTheYear = CSRandom.Next(Steps) / (float)Steps;
            return CampaignTime.YearsFromNow(-(age + withinTheYear));
        }
    }
}