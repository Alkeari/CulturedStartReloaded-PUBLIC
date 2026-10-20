using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     The mod's selection surfaces and questions: searchable option lists,
    ///     multi-selection lists, exact-number and free-text prompts, and
    ///     confirmations, all drawn in the mod's own palette. The game's own
    ///     inquiry stands in for each only when the mod's layer could not open,
    ///     so a question is never lost to a UI failure.
    /// </summary>
    public static class EditorPopups
    {
        /// <summary>A searchable list of labeled options; the pick returns its payload.</summary>
        public static void ShowOptions(string title, IReadOnlyList<(string Label, object? Payload)> options,
            Action<object?> onPick) =>
            ShowOptions(title, options.Select(o => new PickerOption(o.Label, o.Payload)).ToList(), onPick);

        /// <summary>A searchable list of options that may each carry a line saying what they are.</summary>
        public static void ShowOptions(string title, IReadOnlyList<PickerOption> options, Action<object?> onPick)
        {
            if (OptionPickerScreen.Open(title, options, false, picked =>
                {
                    if (picked.Count > 0) onPick(picked[0]);
                }, singleSelect: true, searchable: true, requireSelection: true))
                return;

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title,
                string.Empty,
                options.Select(o => new InquiryElement(o.Payload, o.Label, null, true, o.Detail ?? string.Empty))
                    .ToList(),
                true,
                1,
                1,
                new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                selected =>
                {
                    if (selected == null || selected.Count == 0) return;
                    onPick(selected[0].Identifier);
                },
                _ => { },
                isSeachAvailable: true));
        }

        /// <summary>
        ///     A searchable list where several options can be chosen at once;
        ///     the pick returns every selected payload.
        /// </summary>
        public static void ShowMultiOptions(string title, string description,
            IReadOnlyList<(string Label, object? Payload)> options, Action<List<object?>> onPick)
        {
            var rows = options.Select(o => new PickerOption(o.Label, o.Payload)).ToList();
            if (OptionPickerScreen.Open(title, rows, true, picked =>
                {
                    if (picked.Count > 0) onPick(picked);
                }, description, searchable: true, requireSelection: true))
                return;

            var elements = Elements(options);
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title,
                description,
                elements,
                true,
                1,
                elements.Count,
                new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                selected =>
                {
                    if (selected == null || selected.Count == 0) return;
                    onPick(selected.Select(e => (object?)e.Identifier).ToList());
                },
                _ => { },
                isSeachAvailable: true));
        }

        /// <summary>
        ///     The fallback item popup used when the armory picker screen cannot
        ///     open: searchable, with images, tier-tagged names, and stat hints.
        ///     The armory's own layer failing means the mod's layers cannot be
        ///     trusted either, so this one stays the game's.
        /// </summary>
        public static void ShowItems(string title, IReadOnlyList<ItemObject> items,
            Action<ItemPickChoice, ItemObject?> onPick, bool includeAutoOption = true,
            bool includeNoneOption = true)
        {
            var elements = new List<InquiryElement>();
            if (includeAutoOption)
                elements.Add(new InquiryElement("auto",
                    new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString(), null));
            if (includeNoneOption)
                elements.Add(new InquiryElement("none",
                    new TextObject("{=CSR_Picker_None}None").ToString(), null));

            foreach (var item in items)
                elements.Add(new InquiryElement(item, ItemLabel(item), new ItemImageIdentifier(item),
                    true, ItemHint(item)));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title,
                new TextObject(
                        "{=CSR_Gear_Picker_Desc}Every qualifying item, highest tier first; search by name, class, or tier.")
                    .ToString(),
                elements,
                true,
                1,
                1,
                new TextObject("{=CSR_Gear_Picker_Confirm}Take It").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                selected =>
                {
                    if (selected == null || selected.Count == 0) return;
                    switch (selected[0].Identifier)
                    {
                        case ItemObject item:
                            onPick(ItemPickChoice.Item, item);
                            break;
                        case "none":
                            onPick(ItemPickChoice.None, null);
                            break;
                        default:
                            onPick(ItemPickChoice.Auto, null);
                            break;
                    }
                },
                _ => { },
                isSeachAvailable: true));
        }

        /// <summary>An exact-number prompt with range validation.</summary>
        public static void ShowNumber(string title, int min, int max, Action<int> apply)
        {
            var prompt = new TextObject("{=CSR_Editor_NumberPrompt}Enter a whole number between {MIN} and {MAX}.");
            prompt.SetTextVariable("MIN", min);
            prompt.SetTextVariable("MAX", max);

            ShowPrompt(
                title,
                prompt.ToString(),
                null,
                new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                input =>
                {
                    // Out-of-range entries snap to the nearest bound instead of
                    // being rejected; entries past even long range (int.TryParse
                    // FAILS on overflow, it does not clamp) snap by their sign.
                    // Every snap is announced, never silent.
                    var trimmed = input.Trim();
                    if (long.TryParse(trimmed, out long value))
                    {
                        int clamped = (int)Math.Max(min, Math.Min(max, value));
                        apply(clamped);
                        if (clamped != value)
                            AnnounceSnap(clamped, min, max);
                    }
                    else if (trimmed.Length > 0 && trimmed.TrimStart('-').All(char.IsDigit) &&
                             trimmed.TrimStart('-').Length > 0)
                    {
                        int clamped = trimmed.StartsWith("-", StringComparison.Ordinal) ? min : max;
                        apply(clamped);
                        AnnounceSnap(clamped, min, max);
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=CSR_CustomAmount_Invalid}That is not a usable number.").ToString()));
                    }
                });
        }

        /// <summary>
        ///     A free-text name prompt, seeded with whatever is set now. An empty or
        ///     whitespace-only entry is treated as no answer and changes nothing,
        ///     so a stray Enter cannot blank a name.
        /// </summary>
        public static void ShowText(string title, string description, string? seed, Action<string> apply)
        {
            ShowPrompt(
                title,
                description,
                seed,
                new TextObject("{=CSR_Name_Confirm}Take It").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                input =>
                {
                    var cleaned = Services.HeroNameGenerator.Clean(input);
                    if (cleaned != null) apply(cleaned);
                });
        }

        /// <summary>A text field seeded with <paramref name="seed" />; the affirmative hands back what was typed, never null.</summary>
        public static void ShowPrompt(string title, string description, string? seed, string affirmativeText,
            string negativeText, Action<string> onConfirm)
        {
            if (DialogScreen.Show(DialogVM.Prompt(title, description, seed, affirmativeText, negativeText, onConfirm)))
                return;

            InformationManager.ShowTextInquiry(new TextInquiryData(
                title,
                description,
                true,
                true,
                affirmativeText,
                negativeText,
                input => onConfirm(input ?? string.Empty),
                null,
                false,
                null,
                string.Empty,
                seed ?? string.Empty));
        }

        /// <summary>
        ///     A question with one or two answers. With no <paramref name="negativeText" /> it is a
        ///     notice with a single acknowledgment.
        /// </summary>
        public static void ShowConfirm(string title, string body, string affirmativeText, string? negativeText,
            Action onConfirm, Action? onDecline = null, bool destructive = false)
        {
            if (DialogScreen.Show(DialogVM.Confirm(title, body, affirmativeText, negativeText, onConfirm, onDecline,
                    destructive)))
                return;

            InformationManager.ShowInquiry(new InquiryData(
                title,
                body,
                true,
                negativeText != null,
                affirmativeText,
                negativeText ?? string.Empty,
                onConfirm,
                onDecline));
        }

        private static List<InquiryElement> Elements(IReadOnlyList<(string Label, object? Payload)> options) =>
            options.Select(o => new InquiryElement(o.Payload, o.Label, null)).ToList();

        private static void AnnounceSnap(int clamped, int min, int max)
        {
            var message = new TextObject(
                "{=CSR_CustomAmount_Snapped}Snapped to {VALUE}; the allowed range is {MIN} to {MAX}.");
            message.SetTextVariable("VALUE", clamped);
            message.SetTextVariable("MIN", min);
            message.SetTextVariable("MAX", max);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
        }

        /// <summary>
        ///     The tier number the game shows players: internal tiers run -1 to 5,
        ///     the user-facing scale runs 0 to 6.
        /// </summary>
        public static int DisplayTier(ItemObject item) => (int)item.Tier + 1;

        /// <summary>The player-facing class name of a weapon: usage first, raw class as fallback.</summary>
        public static string WeaponClassName(ItemObject item)
        {
            var choice = Services.GearQuery.ClassifyChoice(item);
            if (choice.HasValue)
                return Menus.WeaponClasses.ClassTitle(choice.Value);
            return SpacedName(item.PrimaryWeapon?.WeaponClass.ToString() ?? string.Empty);
        }

        public static string ItemLabel(ItemObject item)
        {
            if (item.HasWeaponComponent && item.PrimaryWeapon != null)
            {
                var label = new TextObject("{=CSR_Gear_ItemLabelClass}{ITEM_NAME} (Tier {TIER}, {CLASS})");
                label.SetTextVariable("ITEM_NAME", item.Name);
                label.SetTextVariable("TIER", DisplayTier(item));
                label.SetTextVariable("CLASS", WeaponClassName(item));
                return label.ToString();
            }

            var plain = new TextObject("{=CSR_Gear_ItemLabel}{ITEM_NAME} (Tier {TIER})");
            plain.SetTextVariable("ITEM_NAME", item.Name);
            plain.SetTextVariable("TIER", DisplayTier(item));
            return plain.ToString();
        }

        public static string ItemHint(ItemObject item)
        {
            var hint = new TextObject(
                "{=CSR_Gear_ItemHint}Tier {TIER} {TYPE}. Culture: {CULTURE}. Value: {VALUE} gold. Weight: {WEIGHT}.");
            hint.SetTextVariable("TIER", DisplayTier(item));
            hint.SetTextVariable("TYPE", SpacedName(item.ItemType.ToString()));
            hint.SetTextVariable("CULTURE", item.Culture?.Name?.ToString()
                ?? new TextObject("{=CSR_Gear_NoCulture}None").ToString());
            hint.SetTextVariable("VALUE", item.Value);
            hint.SetTextVariable("WEIGHT", item.Weight.ToString("0.#"));
            return hint.ToString();
        }

        public static string SpacedName(string pascalCase)
        {
            return Regex.Replace(pascalCase, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}
