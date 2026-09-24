using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Settings;
using TaleWorlds.CampaignSystem;

namespace CulturedStartReloaded.Services.Application
{
    /// <summary>Everything a start step needs, bundled once.</summary>
    public sealed class StartContext
    {
        public StartContext(Hero hero, CharacterCreationSession session, CSSettings? settings, StartReport report)
        {
            Hero = hero;
            Session = session;
            Settings = settings;
            Report = report;
        }

        public Hero Hero { get; }
        public CharacterCreationSession Session { get; }
        public CSSettings? Settings { get; }
        public StartReport Report { get; }
    }
}
