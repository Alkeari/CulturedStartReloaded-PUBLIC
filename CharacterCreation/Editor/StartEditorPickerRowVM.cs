using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     A selection line in the Start Editor: the value is a button that opens
    ///     the appropriate popup (options list or item list with images). No
    ///     cycling; one click, one choice.
    /// </summary>
    public class StartEditorPickerRowVM : ViewModel
    {
        private readonly Action _openPicker;
        private readonly Func<string> _currentText;
        private readonly Func<bool>? _isCustomized;
        private readonly Action? _revert;
        private string _label;
        private string _valueText = string.Empty;

        public StartEditorPickerRowVM(string label, Func<string> currentText, Action openPicker,
            string? hint = null, Func<bool>? isCustomized = null, Action? revert = null,
            bool isTitle = false, bool isDestructive = false)
        {
            IsTitle = isTitle;
            IsDestructive = isDestructive;
            _label = label;
            _currentText = currentText;
            _openPicker = openPicker;
            _isCustomized = isCustomized;
            _revert = revert;
            RowHint = new HintViewModel(hint != null
                ? new TextObject("{=!}" + hint)
                : new TextObject("{=CSR_Hint_PickerRow}Click to choose."));
            DecreaseHint = RowHint;
            IncreaseHint = RowHint;
            RevertHint = _revert != null
                ? new HintViewModel(new TextObject("{=CSR_Hint_Revert}Reset this line to its default."))
                : RowHint;
            RevertText = _revert != null
                ? new TextObject("{=CSR_Editor_ResetLine}Reset").ToString()
                : string.Empty;
            RefreshValues();
        }

        [DataSourceProperty]
        public string Label
        {
            get => _label;
            set
            {
                if (value == _label) return;
                _label = value;
                OnPropertyChangedWithValue(value, nameof(Label));
            }
        }

        [DataSourceProperty]
        public string ValueText
        {
            get => _valueText;
            set
            {
                if (value == _valueText) return;
                _valueText = value;
                OnPropertyChangedWithValue(value, nameof(ValueText));
            }
        }

        [DataSourceProperty]
        public bool HasSteppers => false;

        [DataSourceProperty]
        public bool HasPips => false;

        /// <summary>The label is a page's title, drawn larger than the rows beneath it.</summary>
        [DataSourceProperty]
        public bool IsTitle { get; }

        /// <summary>The button destroys settings, so it is drawn apart from every harmless one.</summary>
        [DataSourceProperty]
        public bool IsDestructive { get; }

        [DataSourceProperty]
        public bool ShowsPlainValue => !IsDestructive;

        [DataSourceProperty]
        public MBBindingList<StartEditorPipVM> Pips { get; } = new();

        [DataSourceProperty]
        public bool HasBar => false;

        [DataSourceProperty]
        public float BarWidth => 0f;

        [DataSourceProperty]
        public bool IsHeader => false;

        [DataSourceProperty]
        public bool IsRow => true;

        /// <summary>True while the row holds an override the player can hand back.</summary>
        [DataSourceProperty]
        public bool IsCustomized => _revert != null && (_isCustomized?.Invoke() ?? false);

        [DataSourceProperty]
        public string RevertText { get; }

        [DataSourceProperty]
        public HintViewModel RowHint { get; }

        [DataSourceProperty]
        public HintViewModel DecreaseHint { get; }

        [DataSourceProperty]
        public HintViewModel IncreaseHint { get; }

        [DataSourceProperty]
        public HintViewModel RevertHint { get; }

        public void ExecutePick() => _openPicker();

        public void ExecuteIncrease() => _openPicker();

        public void ExecuteDecrease() => _openPicker();

        public void ExecuteRevert()
        {
            if (_revert == null) return;
            _revert();
            RefreshValues();
        }

        public sealed override void RefreshValues()
        {
            base.RefreshValues();
            ValueText = _currentText();
            // Without this the Automatic button keeps its old visibility until
            // the whole tab is rebuilt
            OnPropertyChanged(nameof(IsCustomized));
        }
    }
}
