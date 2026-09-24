using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>What the player decided in a picker.</summary>
    public enum ItemPickChoice
    {
        Auto,
        None,
        Item
    }

    /// <summary>
    ///     The armory picker: a searchable, filterable list of every qualifying
    ///     item with images and stat tooltips. The search box understands
    ///     semicolons as AND and colons as OR; cultures and classes multi-select;
    ///     the strap filter appears only while shields are the filtered class.
    ///     Tiers everywhere use the game's user-facing 0 to 6 scale.
    /// </summary>
    public class ItemPickerVM : ViewModel
    {
        private enum StrapMode
        {
            All,
            Only,
            Exclude
        }

        private const string CulturelessKey = "csr_cultureless";

        [DataSourceProperty]
        public HintViewModel CultureFilterHint { get; } =
            new(new TextObject("{=CSR_Hint_CultureFilter}Filter the list by culture."));

        [DataSourceProperty]
        public HintViewModel ClassFilterHint { get; } =
            new(new TextObject("{=CSR_Hint_ClassFilter}Filter the list by item class."));

        [DataSourceProperty]
        public HintViewModel StrapFilterHint { get; } =
            new(new TextObject("{=CSR_Hint_StrapFilter}Show, hide, or require shoulder-strapped shields."));

        [DataSourceProperty]
        public HintViewModel AutoHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerAuto}Let the quartermaster pick for this slot."));

        [DataSourceProperty]
        public HintViewModel NoneHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerNone}Leave this slot empty."));

        [DataSourceProperty]
        public HintViewModel TakeHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerTake}Take the selected item."));

        [DataSourceProperty]
        public HintViewModel CancelHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerCancel}Close without changing anything."));

        private readonly Action<ItemPickChoice, ItemObject?> _onPick;
        private readonly Action _onClose;
        private readonly bool _hasAuto;
        private readonly bool _hasNone;

        private string _title = string.Empty;
        private string _searchText = string.Empty;
        private string _searchPlaceholderText = string.Empty;
        private string _cultureText = string.Empty;
        private string _classText = string.Empty;
        private string _strapText = string.Empty;
        private string _countText = string.Empty;
        private string _autoText = string.Empty;
        private string _noneText = string.Empty;
        private string _takeText = string.Empty;
        private string _cancelText = string.Empty;
        private bool _hasClassFilter;
        private bool _hasStrapFilter;
        private bool _canTake;
        private MBBindingList<ItemPickerRowVM> _items = new();
        private MBBindingList<ItemPickerToggleVM> _tiers = new();

        private readonly HashSet<object> _cultureFilter = new();
        private readonly HashSet<string> _classFilter = new();
        private StrapMode _strapMode = StrapMode.All;
        private ItemPickerRowVM? _selected;

        public ItemPickerVM(string title, IReadOnlyList<ItemObject> items, bool includeAuto, bool includeNone,
            Action<ItemPickChoice, ItemObject?> onPick, Action onClose, ItemObject? currentItem = null)
        {
            _onPick = onPick;
            _onClose = onClose;
            _hasAuto = includeAuto;
            _hasNone = includeNone;

            Title = title;
            SearchPlaceholderText =
                new TextObject("{=CSR_Picker_SearchHint}Search... (; means AND, : means OR)").ToString();
            AutoText = new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString();
            NoneText = new TextObject("{=CSR_Picker_None}None").ToString();
            TakeText = new TextObject("{=CSR_Gear_Picker_Confirm}Take It").ToString();
            CancelText = new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString();
            _cultureText = FilterLabel("{=CSR_Picker_Culture}Culture", null);
            _classText = FilterLabel("{=CSR_Picker_Class}Class", null);
            _strapText = StrapLabel();

            foreach (var item in items)
            {
                var row = new ItemPickerRowVM(item, OnRowSelected);
                Items.Add(row);
                // The current choice comes in preselected so reopening a picker
                // continues from where it left off
                if (currentItem != null && item == currentItem)
                    OnRowSelected(row);
            }

            HasClassFilter = Items.Select(r => r.ClassKey).Where(k => k != null).Distinct().Count() > 1;

            foreach (int tier in Items.Select(r => r.DisplayTier).Distinct().OrderBy(t => t))
            {
                var label = new TextObject("{=CSR_Picker_TierToggle}T{TIER}");
                label.SetTextVariable("TIER", tier);
                Tiers.Add(new ItemPickerToggleVM(label.ToString(), tier, _ => ApplyFilters()));
            }

            ApplyFilters();
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

        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value == _searchText) return;
                _searchText = value;
                OnPropertyChangedWithValue(value, nameof(SearchText));
                ApplyFilters();
            }
        }

        [DataSourceProperty]
        public string SearchPlaceholderText
        {
            get => _searchPlaceholderText;
            set
            {
                if (value == _searchPlaceholderText) return;
                _searchPlaceholderText = value;
                OnPropertyChangedWithValue(value, nameof(SearchPlaceholderText));
            }
        }

        [DataSourceProperty]
        public string CultureText
        {
            get => _cultureText;
            set
            {
                if (value == _cultureText) return;
                _cultureText = value;
                OnPropertyChangedWithValue(value, nameof(CultureText));
            }
        }

        [DataSourceProperty]
        public string ClassText
        {
            get => _classText;
            set
            {
                if (value == _classText) return;
                _classText = value;
                OnPropertyChangedWithValue(value, nameof(ClassText));
            }
        }

        [DataSourceProperty]
        public string StrapText
        {
            get => _strapText;
            set
            {
                if (value == _strapText) return;
                _strapText = value;
                OnPropertyChangedWithValue(value, nameof(StrapText));
            }
        }

        [DataSourceProperty]
        public string CountText
        {
            get => _countText;
            set
            {
                if (value == _countText) return;
                _countText = value;
                OnPropertyChangedWithValue(value, nameof(CountText));
            }
        }

        [DataSourceProperty]
        public string AutoText
        {
            get => _autoText;
            set
            {
                if (value == _autoText) return;
                _autoText = value;
                OnPropertyChangedWithValue(value, nameof(AutoText));
            }
        }

        [DataSourceProperty]
        public string NoneText
        {
            get => _noneText;
            set
            {
                if (value == _noneText) return;
                _noneText = value;
                OnPropertyChangedWithValue(value, nameof(NoneText));
            }
        }

        [DataSourceProperty]
        public string TakeText
        {
            get => _takeText;
            set
            {
                if (value == _takeText) return;
                _takeText = value;
                OnPropertyChangedWithValue(value, nameof(TakeText));
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
        public bool HasAuto => _hasAuto;

        [DataSourceProperty]
        public bool HasNone => _hasNone;

        [DataSourceProperty]
        public bool HasClassFilter
        {
            get => _hasClassFilter;
            set
            {
                if (value == _hasClassFilter) return;
                _hasClassFilter = value;
                OnPropertyChangedWithValue(value, nameof(HasClassFilter));
            }
        }

        [DataSourceProperty]
        public bool HasStrapFilter
        {
            get => _hasStrapFilter;
            set
            {
                if (value == _hasStrapFilter) return;
                _hasStrapFilter = value;
                OnPropertyChangedWithValue(value, nameof(HasStrapFilter));
            }
        }

        [DataSourceProperty]
        public bool CanTake
        {
            get => _canTake;
            set
            {
                if (value == _canTake) return;
                _canTake = value;
                OnPropertyChangedWithValue(value, nameof(CanTake));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ItemPickerRowVM> Items
        {
            get => _items;
            set
            {
                if (value == _items) return;
                _items = value;
                OnPropertyChangedWithValue(value, nameof(Items));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ItemPickerToggleVM> Tiers
        {
            get => _tiers;
            set
            {
                if (value == _tiers) return;
                _tiers = value;
                OnPropertyChangedWithValue(value, nameof(Tiers));
            }
        }

        public void ExecuteCultureFilter()
        {
            var noneText = new TextObject("{=CSR_Gear_NoCulture}None").ToString();

            // No "All Cultures" row, for the reason the class filter has none: an exclusive row
            // disables every other one while it is selected, and choosing nothing already means
            // every culture. See ExecuteClassFilter.
            var options = Items
                .GroupBy(r => r.Item.Culture)
                .OrderBy(g => g.Key?.Name?.ToString() ?? "")
                .Select(g =>
                {
                    object payload = g.Key ?? (object)CulturelessKey;
                    return new PickerOption(CountedLabel(g.Key?.Name?.ToString() ?? noneText, g.Count()),
                        payload, false, _cultureFilter.Contains(payload));
                })
                .ToList();

            OptionPickerScreen.Open(
                new TextObject("{=CSR_Picker_Culture}Culture").ToString(), options, false, picked =>
                {
                    _cultureFilter.Clear();
                    foreach (var payload in picked)
                        if (payload != null)
                            _cultureFilter.Add(payload);

                    CultureText = FilterLabel("{=CSR_Picker_Culture}Culture", SetLabel(_cultureFilter.Count,
                        _cultureFilter.Count == 1
                            ? (_cultureFilter.First() as CultureObject)?.Name?.ToString() ?? noneText
                            : null));
                    ApplyFilters();
                });
        }

        public void ExecuteClassFilter()
        {
            var groups = Items
                .Where(r => r.ClassKey != null)
                .GroupBy(r => r.ClassKey!)
                .ToList();

            // When only one kind of shield exists in the list, "Large Shield" or
            // "Small Shield" would be a distinction without a difference
            bool singleShieldKind = groups.Count(g =>
                g.Key is "c:SmallShield" or "c:LargeShield") == 1;
            string LabelFor(IGrouping<string, ItemPickerRowVM> group)
            {
                if (singleShieldKind && group.Key is "c:SmallShield" or "c:LargeShield")
                    return new TextObject("{=CSR_Gear_Shield}Shield").ToString();
                return group.First().ClassLabel;
            }

            // No "All Classes" row. Choosing nothing already means every class, which is what the
            // filter has always meant inside: ApplyFilters only narrows while the set is non-empty.
            // The row that used to say so was exclusive, and an exclusive selection locks every
            // other row, so opening this popup and wanting one class meant deselecting All first,
            // every single time. Now the popup opens on whatever is filtered, nothing by default,
            // and a class can be clicked straight away.
            var options = groups
                .OrderBy(LabelFor)
                .Select(g => new PickerOption(CountedLabel(LabelFor(g), g.Count()), g.Key,
                    false, _classFilter.Contains(g.Key)))
                .ToList();

            OptionPickerScreen.Open(
                new TextObject("{=CSR_Picker_Class}Class").ToString(), options, false, picked =>
                {
                    _classFilter.Clear();
                    foreach (var payload in picked)
                        if (payload is string key)
                            _classFilter.Add(key);

                    ClassText = FilterLabel("{=CSR_Picker_Class}Class", SetLabel(_classFilter.Count,
                        _classFilter.Count == 1
                            ? Items.FirstOrDefault(r => r.ClassKey == _classFilter.First())?.ClassLabel
                            : null));
                    RefreshStrapFilterVisibility();
                    ApplyFilters();
                });
        }

        public void ExecuteCycleStrap()
        {
            _strapMode = _strapMode switch
            {
                StrapMode.All => StrapMode.Only,
                StrapMode.Only => StrapMode.Exclude,
                _ => StrapMode.All
            };
            StrapText = StrapLabel();
            ApplyFilters();
        }

        public void ExecuteAuto()
        {
            _onPick(ItemPickChoice.Auto, null);
            _onClose();
        }

        public void ExecuteNone()
        {
            _onPick(ItemPickChoice.None, null);
            _onClose();
        }

        public void ExecuteTake()
        {
            if (_selected == null) return;
            _onPick(ItemPickChoice.Item, _selected.Item);
            _onClose();
        }

        public void ExecuteCancel() => _onClose();

        private void OnRowSelected(ItemPickerRowVM row)
        {
            if (_selected != null && _selected != row)
                _selected.IsSelected = false;
            _selected = row;
            _selected.IsSelected = true;
            CanTake = true;
        }

        /// <summary>The strap filter exists only while shields are what is being filtered.</summary>
        private void RefreshStrapFilterVisibility()
        {
            bool shieldsOnly = _classFilter.Count > 0 &&
                               _classFilter.All(key => key is "c:SmallShield" or "c:LargeShield");
            HasStrapFilter = shieldsOnly;
            if (!shieldsOnly && _strapMode != StrapMode.All)
            {
                _strapMode = StrapMode.All;
                StrapText = StrapLabel();
            }
        }

        private void ApplyFilters()
        {
            var selectedTiers = Tiers.Where(t => t.IsSelected).Select(t => t.Value).ToHashSet();
            var query = (_searchText ?? string.Empty).Trim().ToLowerInvariant();

            int shown = 0;
            foreach (var row in Items)
            {
                bool visible = Matches(row, selectedTiers, query);
                row.IsFilteredOut = !visible;
                if (visible) shown++;
            }

            if (_selected != null && _selected.IsFilteredOut)
            {
                _selected.IsSelected = false;
                _selected = null;
                CanTake = false;
            }

            var count = new TextObject("{=CSR_Picker_Count}{SHOWN} of {TOTAL} items");
            count.SetTextVariable("SHOWN", shown);
            count.SetTextVariable("TOTAL", Items.Count);
            CountText = count.ToString();
        }

        private bool Matches(ItemPickerRowVM row, HashSet<int> selectedTiers, string query)
        {
            if (_cultureFilter.Count > 0)
            {
                object cultureKey = row.Item.Culture ?? (object)CulturelessKey;
                if (!_cultureFilter.Contains(cultureKey)) return false;
            }

            if (_classFilter.Count > 0 &&
                (row.ClassKey == null || !_classFilter.Contains(row.ClassKey)))
                return false;

            if (selectedTiers.Count > 0 && !selectedTiers.Contains(row.DisplayTier)) return false;

            if (row.IsShield && HasStrapFilter)
            {
                if (_strapMode == StrapMode.Only && !row.IsShoulderStrapped) return false;
                if (_strapMode == StrapMode.Exclude && row.IsShoulderStrapped) return false;
            }

            return MatchesQuery(row.SearchHaystack, query);
        }

        /// <summary>Colon-separated groups are OR; semicolon-separated terms are AND.</summary>
        private static bool MatchesQuery(string haystack, string query)
        {
            if (query.Length == 0) return true;

            foreach (var orGroup in query.Split(':'))
            {
                var terms = orGroup.Split(';')
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .ToList();
                if (terms.Count == 0) continue;
                if (terms.All(haystack.Contains)) return true;
            }

            return false;
        }

        private static string CountedLabel(string name, int count)
        {
            var label = new TextObject("{=CSR_Picker_CountedLabel}{NAME} ({COUNT})");
            label.SetTextVariable("NAME", name);
            label.SetTextVariable("COUNT", count);
            return label.ToString();
        }

        private static string? SetLabel(int count, string? single)
        {
            if (count == 0) return null;
            if (count == 1) return single;
            var label = new TextObject("{=CSR_Picker_SetLabel}{COUNT} selected");
            label.SetTextVariable("COUNT", count);
            return label.ToString();
        }

        private static string FilterLabel(string nameKey, string? value)
        {
            var label = new TextObject("{=CSR_Picker_FilterLabel}{NAME}: {VALUE}");
            label.SetTextVariable("NAME", new TextObject(nameKey).ToString());
            label.SetTextVariable("VALUE", value ?? new TextObject("{=CSR_Picker_All}All").ToString());
            return label.ToString();
        }

        private string StrapLabel()
        {
            var valueKey = _strapMode switch
            {
                StrapMode.Only => "{=CSR_Picker_StrapOnly}Only",
                StrapMode.Exclude => "{=CSR_Picker_StrapNone}None",
                _ => "{=CSR_Picker_All}All"
            };
            var label = new TextObject("{=CSR_Picker_FilterLabel}{NAME}: {VALUE}");
            label.SetTextVariable("NAME", new TextObject("{=CSR_Picker_Straps}Straps").ToString());
            label.SetTextVariable("VALUE", new TextObject(valueKey).ToString());
            return label.ToString();
        }
    }

    public class ItemPickerRowVM : ViewModel
    {
        private readonly Action<ItemPickerRowVM> _onSelect;
        private bool _isSelected;
        private bool _isFilteredOut;

        public ItemPickerRowVM(ItemObject item, Action<ItemPickerRowVM> onSelect)
        {
            Item = item;
            _onSelect = onSelect;
            DisplayTier = EditorPopups.DisplayTier(item);
            Name = EditorPopups.ItemLabel(item);
            Image = new GenericImageIdentifierVM(new ItemImageIdentifier(item));
            Hint = new HintViewModel(new TextObject("{=!}" + EditorPopups.ItemHint(item)));

            // Usage class first (lances vs spears, and so on); raw class fallback
            bool isWeapon = item.HasWeaponComponent && item.PrimaryWeapon != null;
            UsageClass = isWeapon ? Services.GearQuery.ClassifyChoice(item) : null;
            if (UsageClass.HasValue)
                ClassKey = "c:" + UsageClass.Value;
            else if (isWeapon)
                ClassKey = "w:" + item.PrimaryWeapon!.WeaponClass;
            ClassLabel = isWeapon ? EditorPopups.WeaponClassName(item) : string.Empty;

            // Mounts class by what they DO: pack animals carry, the rest ride
            if (item.ItemType == ItemObject.ItemTypeEnum.Horse)
            {
                var category = item.ItemCategory;
                ClassKey = "h:" + (category?.StringId ?? "horse");
                if (category == DefaultItemCategories.PackAnimal)
                    ClassLabel = new TextObject("{=CSR_MountClass_Pack}Pack Mount").ToString();
                else if (category == DefaultItemCategories.WarHorse)
                    ClassLabel = new TextObject("{=CSR_MountClass_War}War Mount").ToString();
                else if (category == DefaultItemCategories.NobleHorse)
                    ClassLabel = new TextObject("{=CSR_MountClass_Noble}Noble Mount").ToString();
                else
                    ClassLabel = new TextObject("{=CSR_MountClass_Plain}Mount").ToString();
            }

            IsShield = UsageClass is WeaponClassChoice.SmallShield or WeaponClassChoice.LargeShield;
            IsShoulderStrapped = IsShield &&
                                 ((item.Name?.ToString() ?? "").IndexOf("shoulder", StringComparison.OrdinalIgnoreCase) >= 0
                                  || item.StringId.IndexOf("shoulder", StringComparison.OrdinalIgnoreCase) >= 0);

            SearchHaystack = string.Join(" ",
                    Name,
                    EditorPopups.ItemHint(item),
                    "t" + DisplayTier,
                    item.StringId)
                .ToLowerInvariant();
        }

        public ItemObject Item { get; }
        public int DisplayTier { get; }
        public WeaponClassChoice? UsageClass { get; }
        public string? ClassKey { get; }
        public string ClassLabel { get; }
        public bool IsShield { get; }
        public bool IsShoulderStrapped { get; }
        public string SearchHaystack { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public GenericImageIdentifierVM Image { get; }

        [DataSourceProperty]
        public HintViewModel Hint { get; }

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

        public void ExecuteSelect() => _onSelect(this);
    }

    public class ItemPickerToggleVM : ViewModel
    {
        private readonly Action<ItemPickerToggleVM> _onToggle;
        private bool _isSelected;

        public ItemPickerToggleVM(string label, int value, Action<ItemPickerToggleVM> onToggle)
        {
            Label = label;
            Value = value;
            _onToggle = onToggle;
        }

        [DataSourceProperty]
        public HintViewModel Hint { get; } =
            new(new TextObject("{=CSR_Hint_TierToggle}Show or hide this tier."));

        public int Value { get; }

        [DataSourceProperty]
        public string Label { get; }

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

        public void ExecuteToggle()
        {
            IsSelected = !IsSelected;
            _onToggle(this);
        }
    }
}
