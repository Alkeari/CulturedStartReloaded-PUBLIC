using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>One selectable entry: exclusive options stand alone by construction.</summary>
    public sealed class PickerOption
    {
        public PickerOption(string label, object? payload, bool isExclusive = false, bool startsSelected = false,
            string? hint = null, string? detail = null)
        {
            Label = label;
            Payload = payload;
            IsExclusive = isExclusive;
            StartsSelected = startsSelected;
            Hint = hint;
            Detail = detail;
        }

        public string Label { get; }
        public object? Payload { get; }
        public bool IsExclusive { get; }
        public bool StartsSelected { get; }
        public string? Hint { get; }

        /// <summary>A second line under the label saying what the option is, where the label alone does not.</summary>
        public string? Detail { get; }
    }

    /// <summary>
    ///     The generic picker. As a multi-select, choosing an exclusive option
    ///     deselects and locks out everything else, choosing a normal option
    ///     clears any exclusive, and Select All takes every normal option at
    ///     once. As a single-select, choosing an option clears the rest.
    ///     Contradictions cannot be expressed, only prevented.
    /// </summary>
    public class OptionPickerVM : ViewModel
    {
        [DataSourceProperty]
        public HintViewModel SelectAllHint { get; } =
            new(new TextObject("{=CSR_Hint_SelectAll}Select every option at once."));

        [DataSourceProperty]
        public HintViewModel ConfirmHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerConfirm}Apply the selection."));

        [DataSourceProperty]
        public HintViewModel CancelHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerCancel}Close without changing anything."));

        private readonly Action<List<object?>> _onConfirm;
        private readonly Action _onClose;
        private readonly bool _singleSelect;
        private readonly bool _requireSelection;

        private string _title = string.Empty;
        private string _selectAllText = string.Empty;
        private string _confirmText = string.Empty;
        private string _cancelText = string.Empty;
        private string _searchText = string.Empty;
        private bool _hasSelectAll;
        private bool _canConfirm;
        private MBBindingList<OptionPickerRowVM> _rows = new();

        public OptionPickerVM(string title, IReadOnlyList<PickerOption> options, bool allowSelectAll,
            Action<List<object?>> onConfirm, Action onClose, string? description = null, bool singleSelect = false,
            bool searchable = false, bool requireSelection = false)
        {
            _onConfirm = onConfirm;
            _onClose = onClose;
            _singleSelect = singleSelect;
            _requireSelection = requireSelection;

            Title = title;
            Description = description ?? string.Empty;
            HasSearch = searchable;
            SearchPlaceholderText = new TextObject("{=CSR_Picker_SearchPlain}Search...").ToString();
            SelectAllText = new TextObject("{=CSR_Picker_SelectAll}Select All").ToString();
            ConfirmText = new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString();
            CancelText = new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString();
            HasSelectAll = allowSelectAll && !singleSelect;

            foreach (var option in options)
            {
                var row = new OptionPickerRowVM(option, singleSelect, OnRowToggled);
                if (option.StartsSelected)
                    row.IsSelected = true;
                Rows.Add(row);
            }

            RefreshCanConfirm();
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChangedWithValue(value, nameof(Title));
            }
        }

        [DataSourceProperty] public string Description { get; }
        [DataSourceProperty] public bool HasDescription => Description.Length > 0;
        [DataSourceProperty] public bool HasSearch { get; }
        [DataSourceProperty] public string SearchPlaceholderText { get; }

        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value == _searchText) return;
                _searchText = value;
                OnPropertyChangedWithValue(value, nameof(SearchText));
                ApplySearch();
            }
        }

        [DataSourceProperty]
        public string SelectAllText
        {
            get => _selectAllText;
            set
            {
                if (value == _selectAllText) return;
                _selectAllText = value;
                OnPropertyChangedWithValue(value, nameof(SelectAllText));
            }
        }

        [DataSourceProperty]
        public string ConfirmText
        {
            get => _confirmText;
            set
            {
                if (value == _confirmText) return;
                _confirmText = value;
                OnPropertyChangedWithValue(value, nameof(ConfirmText));
            }
        }

        [DataSourceProperty]
        public string CancelText
        {
            get => _cancelText;
            set
            {
                if (value == _cancelText) return;
                _cancelText = value;
                OnPropertyChangedWithValue(value, nameof(CancelText));
            }
        }

        [DataSourceProperty]
        public bool HasSelectAll
        {
            get => _hasSelectAll;
            set
            {
                if (value == _hasSelectAll) return;
                _hasSelectAll = value;
                OnPropertyChangedWithValue(value, nameof(HasSelectAll));
            }
        }

        [DataSourceProperty]
        public bool CanConfirm
        {
            get => _canConfirm;
            set
            {
                if (value == _canConfirm) return;
                _canConfirm = value;
                OnPropertyChangedWithValue(value, nameof(CanConfirm));
            }
        }

        [DataSourceProperty]
        public MBBindingList<OptionPickerRowVM> Rows
        {
            get => _rows;
            set
            {
                if (value == _rows) return;
                _rows = value;
                OnPropertyChangedWithValue(value, nameof(Rows));
            }
        }

        public void ExecuteSelectAll()
        {
            foreach (var row in Rows)
                row.IsSelected = !row.Option.IsExclusive;
            RefreshCanConfirm();
        }

        public void ExecuteConfirm()
        {
            if (!CanConfirm) return;
            var picked = Rows.Where(r => r.IsSelected).Select(r => r.Option.Payload).ToList();
            _onClose();
            _onConfirm(picked);
        }

        public void ExecuteCancel() => _onClose();

        /// <summary>
        ///     Selecting an exclusive option clears everything else, and selecting an ordinary one
        ///     clears the exclusives, so the two can never be held at once. A single-select clears
        ///     every other row instead.
        ///
        ///     Nothing is ever disabled. Rows used to be locked out while an exclusive was
        ///     selected, which meant the popup opened on its default and every other row refused
        ///     the click until the default was deselected by hand: two clicks and a discovery to
        ///     choose one realm. Clearing on toggle does the same job in one click, and every
        ///     caller reads an empty selection as its own default anyway.
        /// </summary>
        private void OnRowToggled(OptionPickerRowVM row)
        {
            if (row.IsSelected)
            {
                foreach (var other in Rows)
                    if (other != row && (_singleSelect || row.Option.IsExclusive || other.Option.IsExclusive))
                        other.IsSelected = false;
            }

            RefreshCanConfirm();
        }

        private void RefreshCanConfirm() => CanConfirm = !_requireSelection || Rows.Any(r => r.IsSelected);

        private void ApplySearch()
        {
            var query = (_searchText ?? string.Empty).Trim();
            foreach (var row in Rows)
                row.IsFilteredOut = query.Length > 0 &&
                                    row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                                    row.Detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0;
            OnPropertyChanged(nameof(ListHeight));
        }

        /// <summary>
        ///     The list's height: every shown row and its gap, inside the list's own padding, so a
        ///     popup of three options is three rows tall. Past the ceiling the list scrolls instead,
        ///     and it never closes below one row so an emptied search leaves a visible list.
        /// </summary>
        [DataSourceProperty]
        public float ListHeight
        {
            get
            {
                const float padding = 20f, gap = 5f, ceiling = 600f;
                float rows = Rows.Where(r => !r.IsFilteredOut).Sum(r => r.RowHeight + gap);
                return Math.Min(ceiling, padding + Math.Max(52f + gap, rows));
            }
        }
    }

    public class OptionPickerRowVM : ViewModel
    {
        private readonly Action<OptionPickerRowVM> _onToggle;
        private bool _isSelected;
        private bool _isEnabled = true;
        private bool _isFilteredOut;

        public OptionPickerRowVM(PickerOption option, bool singleSelect, Action<OptionPickerRowVM> onToggle)
        {
            Option = option;
            _onToggle = onToggle;
            Hint = new HintViewModel(option.Hint != null
                ? new TextObject("{=!}" + option.Hint)
                : singleSelect
                    ? new TextObject("{=CSR_Hint_OptionRowSingle}Click to choose this option.")
                    : new TextObject("{=CSR_Hint_OptionRow}Click to select or deselect."));
        }

        [DataSourceProperty] public HintViewModel Hint { get; }

        public PickerOption Option { get; }

        [DataSourceProperty]
        public string Label => Option.Label;

        [DataSourceProperty]
        public string Detail => Option.Detail ?? string.Empty;

        [DataSourceProperty]
        public bool HasDetail => !string.IsNullOrEmpty(Option.Detail);

        /// <summary>
        ///     The row's height, from the lines its text wraps to at the row's width: one label line is
        ///     the 52 units every plain row has always had, and each further line of either text adds
        ///     its own line height. Characters per line are counted conservatively, so a row may end a
        ///     little tall but never clips its text.
        /// </summary>
        [DataSourceProperty]
        public float RowHeight
        {
            get
            {
                const float padding = 22f, labelLine = 30f, detailLine = 24f;
                float height = padding + labelLine * Lines(Label, 38);
                if (HasDetail) height += 2f + detailLine * Lines(Detail, 44);
                return height;
            }
        }

        private static int Lines(string text, int charactersPerLine) =>
            Math.Max(1, (int)Math.Ceiling(text.Length / (double)charactersPerLine));

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == _isSelected) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled) return;
                _isEnabled = value;
                OnPropertyChangedWithValue(value, nameof(IsEnabled));
            }
        }

        [DataSourceProperty]
        public bool IsFilteredOut
        {
            get => _isFilteredOut;
            set
            {
                if (value == _isFilteredOut) return;
                _isFilteredOut = value;
                OnPropertyChangedWithValue(value, nameof(IsFilteredOut));
            }
        }

        public void ExecuteToggle()
        {
            if (!IsEnabled && !IsSelected) return;
            IsSelected = !IsSelected;
            _onToggle(this);
        }
    }
}
