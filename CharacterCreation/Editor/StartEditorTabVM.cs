using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     One entry in the Start Editor's left rail: a selectable tab, or a
    ///     non-interactive group header above a run of related tabs.
    /// </summary>
    public class StartEditorTabVM : ViewModel
    {
        private readonly Action<StartEditorTabVM>? _onSelect;
        private string _name;
        private bool _isSelected;

        public StartEditorTabVM(string key, string name, Action<StartEditorTabVM> onSelect,
            string? hint = null)
        {
            Key = key;
            _name = name;
            _onSelect = onSelect;
            Hint = new HintViewModel(new TextObject("{=!}" + (hint ?? name)));
        }

        private StartEditorTabVM(string headerName)
        {
            Key = string.Empty;
            _name = headerName;
            IsHeader = true;
            Hint = new HintViewModel(new TextObject("{=!}"));
        }

        public static StartEditorTabVM Header(string name) => new(name);

        /// <summary>Stable tab identity; indices shift as headers are added.</summary>
        public string Key { get; }

        [DataSourceProperty]
        public bool IsHeader { get; }

        [DataSourceProperty]
        public bool IsRow => !IsHeader;

        [DataSourceProperty]
        public HintViewModel Hint { get; }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value == _name) return;
                _name = value;
                OnPropertyChangedWithValue(value, nameof(Name));
            }
        }

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

        public void ExecuteSelect()
        {
            if (!IsHeader) _onSelect?.Invoke(this);
        }
    }
}
