using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     A non-interactive section header inside a tab's row list. Carries the
    ///     shared row template's full binding surface so the one template renders
    ///     headers and rows alike; an empty label renders as a spacer.
    /// </summary>
    public class StartEditorHeaderVM : ViewModel
    {
        public StartEditorHeaderVM(string label)
        {
            Label = label;
            var empty = new HintViewModel(new TextObject("{=!}"));
            RowHint = empty;
            DecreaseHint = empty;
            IncreaseHint = empty;
            RevertHint = empty;
        }

        [DataSourceProperty]
        public bool IsHeader => true;

        [DataSourceProperty]
        public bool IsRow => false;

        [DataSourceProperty]
        public string Label { get; }

        [DataSourceProperty]
        public string ValueText => string.Empty;

        [DataSourceProperty]
        public bool HasSteppers => false;

        [DataSourceProperty]
        public bool HasPips => false;

        [DataSourceProperty]
        public bool IsTitle => false;

        [DataSourceProperty]
        public bool IsDestructive => false;

        [DataSourceProperty]
        public bool ShowsPlainValue => false;

        [DataSourceProperty]
        public MBBindingList<StartEditorPipVM> Pips { get; } = new();

        [DataSourceProperty]
        public bool HasBar => false;

        [DataSourceProperty]
        public float BarWidth => 0f;

        [DataSourceProperty]
        public bool IsCustomized => false;

        [DataSourceProperty]
        public string RevertText => string.Empty;

        [DataSourceProperty]
        public HintViewModel RowHint { get; }

        [DataSourceProperty]
        public HintViewModel DecreaseHint { get; }

        [DataSourceProperty]
        public HintViewModel IncreaseHint { get; }

        [DataSourceProperty]
        public HintViewModel RevertHint { get; }

        public void ExecutePick()
        {
        }

        public void ExecuteIncrease()
        {
        }

        public void ExecuteDecrease()
        {
        }

        public void ExecuteRevert()
        {
        }
    }
}
