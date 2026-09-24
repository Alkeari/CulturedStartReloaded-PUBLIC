using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>How a skill's perks are decided.</summary>
    public enum PerkPickMode
    {
        Auto,
        None,
        Picks
    }

    /// <summary>One skill the perk board shows: the level the start gives it and every perk it has.</summary>
    public sealed class PerkSkillEntry
    {
        public PerkSkillEntry(string skillId, string name, int level, IReadOnlyList<PerkObject> perks)
        {
            SkillId = skillId;
            Name = name;
            Level = level;
            Perks = perks;
        }

        public string SkillId { get; }
        public string Name { get; }
        public int Level { get; }
        public IReadOnlyList<PerkObject> Perks { get; }
    }

    /// <summary>
    ///     Every skill's perks on one board: the skills down the left, and the chosen skill's perks
    ///     as the game lays them out, one tier per required level with the pair side by side.
    ///
    ///     Taking a perk releases its partner, as the game's own perk screen does, unless a
    ///     both-perks mod is loaded. The partner is dimmed rather than disabled, so choosing the
    ///     other side of a pair is one click. Tiers above the skill's level show what the skill
    ///     would open and cannot be taken. Automatic and No Perks are states of the skill rather
    ///     than entries that could contradict the picks, and nothing is written until Set.
    /// </summary>
    public class PerkPickerVM : ViewModel
    {
        [DataSourceProperty]
        public HintViewModel AutoHint { get; } =
            new(new TextObject("{=CSR_Hint_PerkAuto}Let this skill's perks be chosen automatically."));

        [DataSourceProperty]
        public HintViewModel NoneHint { get; } =
            new(new TextObject("{=CSR_Hint_PerkNone}Take no perks in this skill."));

        [DataSourceProperty]
        public HintViewModel ConfirmHint { get; } =
            new(new TextObject("{=CSR_Hint_PerkConfirm}Apply the chosen perks."));

        [DataSourceProperty]
        public HintViewModel CancelHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerCancel}Close without changing anything."));

        private readonly bool _allowBoth;
        private readonly Action<IReadOnlyDictionary<string, List<string>?>> _onConfirm;
        private readonly Action _onClose;

        private PerkSkillRowVM? _current;
        private string _skillName = string.Empty;
        private string _levelText = string.Empty;
        private bool _isAutoSelected;
        private bool _isNoneSelected;
        private MBBindingList<PerkTierVM> _tiers = new();

        public PerkPickerVM(IReadOnlyList<PerkSkillEntry> skills, string? initialSkillId,
            IReadOnlyDictionary<string, List<string>> saved, bool allowBoth,
            Action<IReadOnlyDictionary<string, List<string>?>> onConfirm, Action onClose)
        {
            _allowBoth = allowBoth;
            _onConfirm = onConfirm;
            _onClose = onClose;

            Title = new TextObject("{=CSR_Editor_TabPerks}Skill Perks").ToString();
            AutoText = new TextObject("{=CSR_Editor_PerksAuto}Automatic").ToString();
            NoneText = new TextObject("{=CSR_Editor_PerksNone}No Perks").ToString();
            ConfirmText = new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString();
            CancelText = new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString();
            PairRuleText = allowBoth
                ? new TextObject("{=CSR_Perks_PairRuleBoth}Both perks of a pair can be taken.").ToString()
                : new TextObject(
                    "{=CSR_Perks_PairRule}Take one perk from each pair, or leave the skill on Automatic.").ToString();

            foreach (var entry in skills)
            {
                saved.TryGetValue(entry.SkillId, out var picks);
                Skills.Add(new PerkSkillRowVM(entry, picks, Select));
            }

            Select(Skills.FirstOrDefault(s => s.Entry.SkillId == initialSkillId) ?? Skills.FirstOrDefault());
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string AutoText { get; }
        [DataSourceProperty] public string NoneText { get; }
        [DataSourceProperty] public string ConfirmText { get; }
        [DataSourceProperty] public string CancelText { get; }
        [DataSourceProperty] public string PairRuleText { get; }
        [DataSourceProperty] public MBBindingList<PerkSkillRowVM> Skills { get; } = new();

        [DataSourceProperty]
        public string SkillName
        {
            get => _skillName;
            set
            {
                if (value == _skillName) return;
                _skillName = value;
                OnPropertyChangedWithValue(value, nameof(SkillName));
            }
        }

        [DataSourceProperty]
        public string LevelText
        {
            get => _levelText;
            set
            {
                if (value == _levelText) return;
                _levelText = value;
                OnPropertyChangedWithValue(value, nameof(LevelText));
            }
        }

        [DataSourceProperty]
        public bool IsAutoSelected
        {
            get => _isAutoSelected;
            set
            {
                if (value == _isAutoSelected) return;
                _isAutoSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsAutoSelected));
            }
        }

        [DataSourceProperty]
        public bool IsNoneSelected
        {
            get => _isNoneSelected;
            set
            {
                if (value == _isNoneSelected) return;
                _isNoneSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsNoneSelected));
            }
        }

        [DataSourceProperty]
        public MBBindingList<PerkTierVM> Tiers
        {
            get => _tiers;
            set
            {
                if (value == _tiers) return;
                _tiers = value;
                OnPropertyChangedWithValue(value, nameof(Tiers));
            }
        }

        public void ExecuteAuto()
        {
            if (_current == null) return;
            _current.SetMode(PerkPickMode.Auto);
            Refresh();
        }

        public void ExecuteNone()
        {
            if (_current == null) return;
            _current.SetMode(PerkPickMode.None);
            Refresh();
        }

        public void ExecuteConfirm()
        {
            var changes = Skills
                .Where(s => s.IsDirty)
                .ToDictionary(s => s.Entry.SkillId, s => s.Result());
            _onClose();
            _onConfirm(changes);
        }

        public void ExecuteCancel() => _onClose();

        private void Select(PerkSkillRowVM? row)
        {
            if (row == null) return;

            _current = row;
            foreach (var other in Skills)
                other.IsSelected = other == row;

            SkillName = row.Entry.Name;
            var level = new TextObject("{=CSR_Perks_Level}Skill level {LEVEL}");
            level.SetTextVariable("LEVEL", row.Entry.Level);
            LevelText = level.ToString();

            var tiers = new MBBindingList<PerkTierVM>();
            foreach (var group in row.Entry.Perks.GroupBy(p => (int)p.RequiredSkillValue).OrderBy(g => g.Key))
            {
                var remaining = group.ToList();
                while (remaining.Count > 0)
                {
                    var left = remaining[0];
                    remaining.RemoveAt(0);
                    var right = left.AlternativePerk != null && remaining.Contains(left.AlternativePerk)
                        ? left.AlternativePerk
                        : null;
                    if (right != null) remaining.Remove(right);

                    // The game's perk screen draws the pair member whose id sorts first on top,
                    // by this same comparison, so that one is the left card here
                    if (right != null && left.StringId.CompareTo(right.StringId) > 0)
                        (left, right) = (right, left);

                    bool locked = group.Key > row.Entry.Level;
                    tiers.Add(new PerkTierVM(group.Key, locked,
                        new PerkCardVM(left, locked, _allowBoth, Toggle),
                        right == null ? PerkCardVM.Empty : new PerkCardVM(right, locked, _allowBoth, Toggle)));
                }
            }

            Tiers = tiers;
            Refresh();
        }

        private void Toggle(PerkCardVM card)
        {
            if (_current == null || card.Perk == null || card.IsLocked) return;

            _current.Toggle(card.Perk, _allowBoth);
            Refresh();
        }

        private void Refresh()
        {
            if (_current == null) return;

            IsAutoSelected = _current.Mode == PerkPickMode.Auto;
            IsNoneSelected = _current.Mode == PerkPickMode.None;
            foreach (var tier in Tiers)
            {
                tier.Left.Refresh(_current.Picks);
                tier.Right.Refresh(_current.Picks);
            }
        }
    }

    /// <summary>A skill down the board's left side, holding that skill's unsaved decision.</summary>
    public class PerkSkillRowVM : ViewModel
    {
        private readonly Action<PerkSkillRowVM> _onSelect;
        private bool _isSelected;
        private string _statusText = string.Empty;

        public PerkSkillRowVM(PerkSkillEntry entry, List<string>? saved, Action<PerkSkillRowVM> onSelect)
        {
            Entry = entry;
            _onSelect = onSelect;
            Mode = saved == null ? PerkPickMode.Auto : saved.Count == 0 ? PerkPickMode.None : PerkPickMode.Picks;
            if (saved != null)
                foreach (var id in saved)
                    Picks.Add(id);
            LevelText = entry.Level.ToString();
            RefreshStatus();
        }

        public PerkSkillEntry Entry { get; }
        public PerkPickMode Mode { get; private set; }
        public HashSet<string> Picks { get; } = new();
        public bool IsDirty { get; private set; }

        [DataSourceProperty]
        public HintViewModel Hint { get; } =
            new(new TextObject("{=CSR_Hint_PerkSkill}Show this skill's perks."));

        [DataSourceProperty] public string Name => Entry.Name;
        [DataSourceProperty] public string LevelText { get; }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (value == _statusText) return;
                _statusText = value;
                OnPropertyChangedWithValue(value, nameof(StatusText));
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

        public void ExecuteSelect() => _onSelect(this);

        public void SetMode(PerkPickMode mode)
        {
            Mode = mode;
            Picks.Clear();
            IsDirty = true;
            RefreshStatus();
        }

        public void Toggle(PerkObject perk, bool allowBoth)
        {
            if (!Picks.Remove(perk.StringId))
            {
                Picks.Add(perk.StringId);
                if (!allowBoth && perk.AlternativePerk != null)
                    Picks.Remove(perk.AlternativePerk.StringId);
            }

            Mode = Picks.Count == 0 ? PerkPickMode.Auto : PerkPickMode.Picks;
            IsDirty = true;
            RefreshStatus();
        }

        /// <summary>Null for Automatic, empty for No Perks, otherwise the picks the skill's level reaches.</summary>
        public List<string>? Result() => Mode switch
        {
            PerkPickMode.Auto => null,
            PerkPickMode.None => new List<string>(),
            _ => Entry.Perks
                .Where(p => p.RequiredSkillValue <= Entry.Level && Picks.Contains(p.StringId))
                .Select(p => p.StringId)
                .ToList()
        };

        private void RefreshStatus()
        {
            if (Mode == PerkPickMode.Auto)
            {
                StatusText = new TextObject("{=CSR_Editor_PerksAuto}Automatic").ToString();
                return;
            }

            if (Mode == PerkPickMode.None)
            {
                StatusText = new TextObject("{=CSR_Editor_PerksNone}No Perks").ToString();
                return;
            }

            var chosen = new TextObject("{=CSR_Editor_PerksChosen}{COUNT} chosen");
            chosen.SetTextVariable("COUNT", Picks.Count);
            StatusText = chosen.ToString();
        }
    }

    /// <summary>One required level of a skill: a pair of perks side by side, or a perk alone.</summary>
    public class PerkTierVM : ViewModel
    {
        public PerkTierVM(int level, bool isLocked, PerkCardVM left, PerkCardVM right)
        {
            IsLocked = isLocked;
            LevelText = level.ToString();
            var needs = new TextObject("{=CSR_Perks_Needs}Needs {LEVEL}");
            needs.SetTextVariable("LEVEL", level);
            NeedsText = needs.ToString();
            Left = left;
            Right = right;
            Height = Math.Max(76f, Math.Max(left.EstimatedHeight, right.EstimatedHeight));
            left.Height = Height;
            right.Height = Height;
        }

        /// <summary>The taller card's height, so both sides of a pair line up.</summary>
        [DataSourceProperty] public float Height { get; }

        /// <summary>A perk with no pair, drawn centered between where the two sides would sit.</summary>
        [DataSourceProperty] public bool IsSingle => !Right.IsPresent;
        [DataSourceProperty] public bool IsLocked { get; }
        [DataSourceProperty] public string LevelText { get; }
        [DataSourceProperty] public string NeedsText { get; }
        [DataSourceProperty] public PerkCardVM Left { get; }
        [DataSourceProperty] public PerkCardVM Right { get; }
    }

    /// <summary>A perk drawn as the game draws it: its icon, name, the roles it serves and what it does.</summary>
    public class PerkCardVM : ViewModel
    {
        public static PerkCardVM Empty => new(null, true, false, _ => { });

        private readonly Action<PerkCardVM> _onToggle;
        private bool _isSelected;
        private bool _isPartnerChosen;

        public PerkCardVM(PerkObject? perk, bool isLocked, bool allowBoth, Action<PerkCardVM> onToggle)
        {
            Perk = perk;
            IsLocked = isLocked;
            _onToggle = onToggle;
            AllowBoth = allowBoth;

            if (perk == null)
            {
                Hint = new HintViewModel(new TextObject("{=!}"));
                return;
            }

            Name = perk.Name?.ToString() ?? perk.StringId;
            IconSprite = "SPPerks\\" + perk.StringId;
            RoleText = CampaignUIHelper.GetPerkRoleText(perk, false)?.ToString() ?? string.Empty;
            Description = perk.PrimaryDescription?.ToString() ?? string.Empty;
            SecondaryRoleText = CampaignUIHelper.GetPerkRoleText(perk, true)?.ToString() ?? string.Empty;
            SecondaryDescription = SecondaryRoleText.Length > 0
                ? perk.SecondaryDescription?.ToString() ?? string.Empty
                : string.Empty;

            TextObject hint;
            if (isLocked)
            {
                hint = new TextObject("{=CSR_Hint_PerkLocked}Raise this skill to {LEVEL} to take this perk.");
                hint.SetTextVariable("LEVEL", (int)perk.RequiredSkillValue);
            }
            else
            {
                hint = allowBoth || perk.AlternativePerk == null
                    ? new TextObject("{=CSR_Hint_PerkCardBoth}Take or release this perk.")
                    : new TextObject("{=CSR_Hint_PerkCard}Take or release this perk; taking it releases its pair.");
            }

            Hint = new HintViewModel(hint);
        }

        public PerkObject? Perk { get; }
        public bool AllowBoth { get; }

        /// <summary>
        ///     The card's height from the lines its texts wrap to in the 376 units beside the icon,
        ///     counting characters per line conservatively so a card may end tall but never clips.
        /// </summary>
        public float EstimatedHeight
        {
            get
            {
                if (Perk == null) return 0f;
                const float line = 24f;
                float height = 12f + 30f + 14f;
                if (HasRole) height += line * Lines(RoleText, 38);
                height += 2f + line * Lines(Description, 34);
                if (HasSecondary)
                    height += 8f + line * Lines(SecondaryRoleText, 38) + 2f + line * Lines(SecondaryDescription, 34);
                return height;
            }
        }

        /// <summary>Set by the tier, which gives both cards of a pair the taller one's height.</summary>
        [DataSourceProperty] public float Height { get; set; }

        private static int Lines(string text, int charactersPerLine) =>
            Math.Max(1, (int)Math.Ceiling(text.Length / (double)charactersPerLine));

        [DataSourceProperty] public bool IsPresent => Perk != null;
        [DataSourceProperty] public bool IsLocked { get; }
        [DataSourceProperty] public bool IsUnlocked => !IsLocked;
        [DataSourceProperty] public string Name { get; } = string.Empty;
        [DataSourceProperty] public string IconSprite { get; } = string.Empty;
        [DataSourceProperty] public string RoleText { get; } = string.Empty;
        [DataSourceProperty] public bool HasRole => RoleText.Length > 0;
        [DataSourceProperty] public string Description { get; } = string.Empty;
        [DataSourceProperty] public string SecondaryRoleText { get; } = string.Empty;
        [DataSourceProperty] public string SecondaryDescription { get; } = string.Empty;
        [DataSourceProperty] public bool HasSecondary => SecondaryRoleText.Length > 0;
        [DataSourceProperty] public HintViewModel Hint { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == _isSelected) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
                OnPropertyChanged(nameof(IsDimmed));
            }
        }

        [DataSourceProperty]
        public bool IsPartnerChosen
        {
            get => _isPartnerChosen;
            set
            {
                if (value == _isPartnerChosen) return;
                _isPartnerChosen = value;
                OnPropertyChangedWithValue(value, nameof(IsPartnerChosen));
                OnPropertyChanged(nameof(IsDimmed));
            }
        }

        /// <summary>Out of reach, or passed over for the other side of its pair.</summary>
        [DataSourceProperty]
        public bool IsDimmed => Perk != null && (IsLocked || IsPartnerChosen);

        public void ExecuteToggle() => _onToggle(this);

        public void Refresh(HashSet<string> picks)
        {
            if (Perk == null) return;
            IsSelected = picks.Contains(Perk.StringId);
            IsPartnerChosen = !AllowBoth && !IsSelected && Perk.AlternativePerk != null &&
                              picks.Contains(Perk.AlternativePerk.StringId);
        }
    }
}
