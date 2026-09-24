using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     A numeric line in the Start Editor: steppers for small nudges, a
    ///     click on the value for exact entry, and, where an automatic state
    ///     exists, an Auto button that marks and reverts a customized value.
    ///
    ///     A small range reads better as marks than as a number, so with
    ///     <c>pips</c> a range of ten or fewer steps is drawn as one mark per
    ///     step, filled out from zero, and a click on a mark sets the value
    ///     there. A range that crosses zero, as a trait's does, fills toward
    ///     either side. With <c>bar</c> a wide range keeps its steppers and
    ///     underlines the value with how far along the range it sits.
    /// </summary>
    public class StartEditorRowVM : ViewModel
    {
        private const int MaxPips = 10;
        private const float BarSpan = 240f;

        private readonly Func<int> _get;
        private readonly Action<int> _set;
        private readonly Func<int, string> _format;
        private readonly Func<bool>? _isCustomized;
        private readonly Action? _revert;
        private readonly int _min;
        private readonly int _max;
        private readonly int _step;
        private readonly bool _bar;
        private readonly Action? _chooseAutomatic;
        private string _label;
        private string _valueText = string.Empty;
        private float _barWidth;

        /// <param name="chooseAutomatic">
        ///     For a value an automatic setting decides (a band), the Automatic button stays on the
        ///     line and opens that choice, rather than appearing only to revert an exact value.
        /// </param>
        public StartEditorRowVM(string label, int min, int max, int step,
            Func<int> get, Action<int> set, Func<int, string>? format = null,
            Func<bool>? isCustomized = null, Action? revert = null, bool pips = false, bool bar = false,
            Action? chooseAutomatic = null)
        {
            _chooseAutomatic = chooseAutomatic;
            _label = label;
            _min = min;
            _max = max;
            _step = step;
            _get = get;
            _set = set;
            _format = format ?? (v => v.ToString());
            _isCustomized = isCustomized;
            _revert = revert;
            _bar = bar;
            HasPips = pips && max - min <= MaxPips && max > min;
            RowHint = new HintViewModel(
                new TextObject("{=CSR_Hint_NumericRow}Click the value to type an exact number."));
            DecreaseHint = new HintViewModel(new TextObject("{=CSR_Hint_Decrease}Lower this value."));
            IncreaseHint = new HintViewModel(new TextObject("{=CSR_Hint_Increase}Raise this value."));
            RevertHint = new HintViewModel(chooseAutomatic != null
                ? new TextObject("{=CSR_Hint_ChooseAutomatic}Choose how much this is when it is set automatically; the choice applies at once.")
                : new TextObject("{=CSR_Hint_Revert}Reset this line to its default."));
            // Automatic names a button that decides the value; one that only clears what was set is a reset
            RevertText = chooseAutomatic != null
                ? new TextObject("{=CSR_Editor_Auto}Automatic").ToString()
                : new TextObject("{=CSR_Editor_ResetLine}Reset").ToString();

            if (HasPips)
            {
                for (int value = Math.Min(min, 0); value <= max; value++)
                {
                    if (value == 0)
                    {
                        if (min < 0) Pips.Add(StartEditorPipVM.Gap());
                        continue;
                    }

                    if (value < min) continue;
                    Pips.Add(new StartEditorPipVM(value, SetFromPip));
                }
            }

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
        public bool HasSteppers => !HasPips;

        [DataSourceProperty]
        public bool HasPips { get; }

        [DataSourceProperty]
        public bool IsTitle => false;

        [DataSourceProperty]
        public bool IsDestructive => false;

        [DataSourceProperty]
        public bool ShowsPlainValue => !HasPips;

        [DataSourceProperty]
        public MBBindingList<StartEditorPipVM> Pips { get; } = new();

        [DataSourceProperty]
        public bool HasBar => _bar && !HasPips;

        [DataSourceProperty]
        public float BarWidth
        {
            get => _barWidth;
            set
            {
                if (Math.Abs(value - _barWidth) < 0.01f) return;
                _barWidth = value;
                OnPropertyChangedWithValue(value, nameof(BarWidth));
            }
        }

        [DataSourceProperty]
        public bool IsHeader => false;

        [DataSourceProperty]
        public bool IsRow => true;

        /// <summary>True while the value is an override rather than an estimate.</summary>
        [DataSourceProperty]
        public bool IsCustomized =>
            _chooseAutomatic != null || (_revert != null && (_isCustomized?.Invoke() ?? false));

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

        public void ExecuteIncrease()
        {
            _set(Clamp(_get() + _step));
            RefreshValues();
        }

        public void ExecuteDecrease()
        {
            _set(Clamp(_get() - _step));
            RefreshValues();
        }

        /// <summary>Clicking the value opens exact entry.</summary>
        public void ExecutePick()
        {
            EditorPopups.ShowNumber(Label, _min, _max, value =>
            {
                _set(Clamp(value));
                RefreshValues();
            });
        }

        public void ExecuteRevert()
        {
            if (_chooseAutomatic != null)
            {
                _chooseAutomatic();
                return;
            }

            if (_revert == null) return;
            _revert();
            RefreshValues();
        }

        public sealed override void RefreshValues()
        {
            base.RefreshValues();
            int value = _get();
            ValueText = _format(value);
            foreach (var pip in Pips)
                pip.Refresh(value);
            if (HasBar)
                BarWidth = BarSpan * (Clamp(value) - _min) / Math.Max(1, _max - _min);
            OnPropertyChanged(nameof(IsCustomized));
        }

        /// <summary>
        ///     A mark sets the value to itself, except the mark the value already
        ///     sits on, which steps one back toward zero, so the first mark can
        ///     empty the row again.
        /// </summary>
        private void SetFromPip(int pip)
        {
            int current = _get();
            _set(Clamp(current == pip ? pip - Math.Sign(pip) : pip));
            RefreshValues();
        }

        private int Clamp(int value) => Math.Max(_min, Math.Min(_max, value));
    }

    /// <summary>One mark of a pip row, or the gap that stands for zero on a range crossing it.</summary>
    public class StartEditorPipVM : ViewModel
    {
        private readonly int _value;
        private readonly Action<int>? _onSet;
        private bool _isFilled;

        public StartEditorPipVM(int value, Action<int>? onSet)
        {
            _value = value;
            _onSet = onSet;
            var hint = new TextObject("{=CSR_Hint_Pip}Set this to {VALUE}; the mark it already sits on steps it back.");
            hint.SetTextVariable("VALUE", value);
            Hint = new HintViewModel(onSet == null ? new TextObject("{=!}") : hint);
        }

        public static StartEditorPipVM Gap() => new(0, null);

        [DataSourceProperty] public bool IsGap => _onSet == null;
        [DataSourceProperty] public HintViewModel Hint { get; }

        [DataSourceProperty]
        public bool IsFilled
        {
            get => _isFilled;
            set
            {
                if (value == _isFilled) return;
                _isFilled = value;
                OnPropertyChangedWithValue(value, nameof(IsFilled));
            }
        }

        public void ExecuteSet() => _onSet?.Invoke(_value);

        public void Refresh(int current) =>
            IsFilled = !IsGap && (_value > 0 ? current >= _value : current <= _value);
    }
}
