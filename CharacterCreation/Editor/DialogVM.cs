using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     One confirmation or text prompt: a title, a sentence, an optional field, and one or two
    ///     actions. Every action closes the dialog before it runs, so an action may open the next one.
    /// </summary>
    public sealed class DialogVM : ViewModel
    {
        private readonly Action<string>? _onAffirm;
        private readonly Action? _onNegative;
        private Action<DialogVM>? _close;
        private string _inputText;

        private DialogVM(string title, string body, string affirmativeText, string? negativeText, bool hasInput,
            string seed, Action<string>? onAffirm, Action? onNegative, bool isDestructive = false)
        {
            IsDestructive = isDestructive;
            Title = title;
            Body = body;
            AffirmativeText = affirmativeText;
            NegativeText = negativeText ?? string.Empty;
            HasNegative = negativeText != null;
            HasInput = hasInput;
            _inputText = seed;
            _onAffirm = onAffirm;
            _onNegative = onNegative;
        }

        /// <summary>A question with a yes and, when <paramref name="negativeText" /> is given, a no.</summary>
        public static DialogVM Confirm(string title, string body, string affirmativeText, string? negativeText,
            Action onAffirm, Action? onNegative, bool isDestructive = false) =>
            new(title, body, affirmativeText, negativeText, false, string.Empty, _ => onAffirm(), onNegative,
                isDestructive);

        /// <summary>The affirmative throws settings away, so it is drawn apart from a harmless one.</summary>
        [DataSourceProperty] public bool IsDestructive { get; }

        [DataSourceProperty] public bool IsHarmless => !IsDestructive;

        /// <summary>A text field seeded with <paramref name="seed" />; the affirmative hands back what was typed.</summary>
        public static DialogVM Prompt(string title, string body, string? seed, string affirmativeText,
            string negativeText, Action<string> onAffirm) =>
            new(title, body, affirmativeText, negativeText, true, seed ?? string.Empty, onAffirm, null);

        [DataSourceProperty]
        public HintViewModel AffirmativeHint { get; } =
            new(new TextObject("{=CSR_Hint_DialogAffirm}Carry out the choice this button names."));

        [DataSourceProperty]
        public HintViewModel NegativeHint { get; } =
            new(new TextObject("{=CSR_Hint_PickerCancel}Close without changing anything."));

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string Body { get; }
        [DataSourceProperty] public bool HasBody => Body.Length > 0;
        [DataSourceProperty] public string AffirmativeText { get; }
        [DataSourceProperty] public string NegativeText { get; }
        [DataSourceProperty] public bool HasNegative { get; }
        [DataSourceProperty] public bool HasInput { get; }

        [DataSourceProperty]
        public string InputText
        {
            get => _inputText;
            set
            {
                if (value == _inputText) return;
                _inputText = value;
                OnPropertyChangedWithValue(value, nameof(InputText));
            }
        }

        internal void BindClose(Action<DialogVM> close) => _close = close;

        public void ExecuteAffirmative()
        {
            string typed = _inputText ?? string.Empty;
            _close?.Invoke(this);
            _onAffirm?.Invoke(typed);
        }

        public void ExecuteNegative()
        {
            _close?.Invoke(this);
            _onNegative?.Invoke();
        }
    }
}
