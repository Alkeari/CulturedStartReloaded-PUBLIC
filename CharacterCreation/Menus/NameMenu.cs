using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The two names the player must be offered whatever route they walk: their
    ///     own and their clan's. Both are drawn from the culture's declared name
    ///     pools, so they belong to whatever world is loaded, and both end with a
    ///     way to type one instead. Neither chapter can be switched off: a name
    ///     decided for you is not a name you chose.
    /// </summary>
    public static class NameMenu
    {
        /// <summary>
        ///     The pool each chapter's rows were last built from, by menu id.
        ///
        ///     The rows are rebuilt only when the pool itself changes, so walking
        ///     back into a chapter finds the row the player had highlighted still
        ///     highlighted, while a culture change, which retires the composed
        ///     offers, replaces them.
        /// </summary>
        private static readonly Dictionary<string, string> Built =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private const string PersonalMenu = "cs_name_menu";

        private const string HouseMenu = "cs_clan_name_menu";

        public static void AddNameMenus(CharacterCreationManager manager)
        {
            // A creation run registers fresh menus holding no rows at all, so what
            // an earlier run composed is not what these menus carry
            Built.Clear();

            AddPersonalNameMenu(manager);
            AddClanNameMenu(manager);
        }

        private static void AddPersonalNameMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                PersonalMenu,
                CreationFlow.DeclaredPrevious(PersonalMenu),
                CreationFlow.DeclaredNext(PersonalMenu),
                new TextObject("{=CSR_Name_Title}What They Call You"),
                new TextObject("{=CSR_Name_Desc}A name given at birth, or one you took for yourself. Either way it is the one people will use."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                (culture, occupationType, asked) =>
                {
                    Compose(asked, PersonalMenu,
                        HeroNameGenerator.FirstNameCandidates(CreationSession.Current),
                        PersonalOffer, PersonalOwn);
                    return GetPlayerCharacterArgs(culture, occupationType, asked);
                }
            );

            menu.AddNarrativeMenuOption(PersonalOwn());
            manager.AddNewMenu(menu);
        }

        private static void AddClanNameMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                HouseMenu,
                CreationFlow.DeclaredPrevious(HouseMenu),
                CreationFlow.DeclaredNext(HouseMenu),
                new TextObject("{=CSR_ClanName_Title}The Name of Your House"),
                new TextObject("{=CSR_ClanName_Desc}What your line is called, and what will be said of it after you."),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                (culture, occupationType, asked) =>
                {
                    Compose(asked, HouseMenu,
                        HeroNameGenerator.ClanNameCandidates(CreationSession.Current),
                        HouseOffer, HouseOwn);
                    return GetPlayerCharacterArgs(culture, occupationType, asked);
                }
            );

            menu.AddNarrativeMenuOption(HouseOwn());
            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     Builds a chapter's rows out of the pool it offers from, at the first
        ///     moment that pool can be read.
        ///
        ///     The menus are registered before a culture is chosen, so how many
        ///     names the pool will hold is not knowable then. Registering five rows
        ///     and hiding the ones with nothing behind them leaves the game holding
        ///     an option that grants nothing, and a row the pool cannot fill is not
        ///     an answer that contradicts the life: it is an answer
        ///     with nothing behind it, so it is not built at all.
        ///
        ///     This runs from the menu's own character arguments, which the game
        ///     asks for once on every entry to a menu and before it reads that
        ///     menu's option list, so the rows stand by the time they are rendered.
        /// </summary>
        private static void Compose(CharacterCreationManager manager, string menuId,
            IReadOnlyList<string> names, Func<int, string, NarrativeMenuOption> offer,
            Func<NarrativeMenuOption> own)
        {
            try
            {
                var menu = manager.GetNarrativeMenuWithId(menuId);
                if (menu == null) return;

                string pool = string.Join("|", names);
                if (Built.TryGetValue(menuId, out var last) &&
                    string.Equals(last, pool, StringComparison.Ordinal)) return;

                Built[menuId] = pool;

                foreach (var option in menu.CharacterCreationMenuOptions.ToList())
                    menu.RemoveNarrativeMenuOption(option);

                // Every row is a new object, so whatever the game had selected on
                // this chapter names one of the rows that just left
                manager.SelectedOptions.Remove(menu);

                for (int index = 0; index < names.Count; index++)
                    menu.AddNarrativeMenuOption(offer(index, names[index]));

                menu.AddNarrativeMenuOption(own());

                CSLogger.Info($"NameMenu: {menuId} offers {names.Count} name(s) and a way to type one.");
            }
            catch (Exception ex)
            {
                CSLogger.Error($"NameMenu: composing the rows of {menuId} failed.", ex);
            }
        }

        /// <summary>
        ///     One name the culture's own pool offered. The row holds the name it
        ///     was built for rather than a slot number it reads back out of the
        ///     pool, so the button, the pick and the panel are three readings of
        ///     one string and none of them can come back empty.
        /// </summary>
        private static NarrativeMenuOption PersonalOffer(int index, string name)
        {
            string id = "cs_name_offered_" + index;

            ChoiceEffects.Declare(id, () =>
                Offered("{=CSR_Panel_Name_Offered}Name: {NAME}, from your culture's own names", name));

            return new NarrativeMenuOption(
                id,
                Named(name),
                new TextObject("{=CSR_Name_Option_Desc}A name your people have always used."),
                args => { },
                m => true,
                m => CreationSession.Current.PlayerFirstName = name,
                m => { }
            );
        }

        /// <inheritdoc cref="PersonalOffer"/>
        private static NarrativeMenuOption HouseOffer(int index, string name)
        {
            string id = "cs_clan_name_offered_" + index;

            ChoiceEffects.Declare(id, () =>
                Offered("{=CSR_Panel_ClanName_Offered}Name: {NAME}, your house's, in the manner of your people",
                    name));

            return new NarrativeMenuOption(
                id,
                Named(name),
                new TextObject("{=CSR_ClanName_Option_Desc}A house name in the manner of your people."),
                args => { },
                m => true,
                m => CreationSession.Current.PlayerClanName = name,
                m => { }
            );
        }

        /// <summary>
        ///     The row that opens the text box, which every world offers however
        ///     little its naming customs hand over.
        /// </summary>
        private static NarrativeMenuOption PersonalOwn()
        {
            ChoiceEffects.Declare("cs_name_own", () => Typed(
                CreationSession.Current.PlayerFirstName,
                HeroNameGenerator.FirstNamePreview(CreationSession.Current),
                "{=CSR_Panel_Name_Typed}Name: {NAME}, as you typed it",
                "{=CSR_Panel_Name_Untyped}Name: {NAME}, drawn for your culture until you type another",
                "{=CSR_Panel_Name_Whatever}Name: whatever you type, and the campaign uses it exactly so"));

            return new NarrativeMenuOption(
                "cs_name_own",
                new TextObject("{=CSR_Name_Own}Give Your Own Name"),
                new TextObject("{=CSR_Name_Own_Desc}Set it yourself."),
                args => { },
                m => true,
                m => Prompt(
                    "{=CSR_Name_Prompt_Title}Your Name",
                    "{=CSR_Name_Prompt_Desc}What are you called?",
                    CreationSession.Current.PlayerFirstName
                        ?? HeroNameGenerator.FirstNamePreview(CreationSession.Current),
                    typed => CreationSession.Current.PlayerFirstName = typed),
                m => { }
            );
        }

        /// <inheritdoc cref="PersonalOwn"/>
        private static NarrativeMenuOption HouseOwn()
        {
            ChoiceEffects.Declare("cs_clan_name_own", () => Typed(
                CreationSession.Current.PlayerClanName,
                HeroNameGenerator.ClanNamePreview(CreationSession.Current),
                "{=CSR_Panel_ClanName_Typed}Name: {NAME}, your house's, as you typed it",
                "{=CSR_Panel_ClanName_Untyped}Name: {NAME}, your house's, drawn for your culture until you type another",
                "{=CSR_Panel_ClanName_Whatever}Name: whatever you type, and your house carries it exactly so"));

            return new NarrativeMenuOption(
                "cs_clan_name_own",
                new TextObject("{=CSR_ClanName_Own}Name Your House Yourself"),
                new TextObject("{=CSR_ClanName_Own_Desc}Set it yourself."),
                args => { },
                m => true,
                m => Prompt(
                    "{=CSR_ClanName_Prompt_Title}Your House",
                    "{=CSR_ClanName_Prompt_Desc}What is your line called?",
                    CreationSession.Current.PlayerClanName
                        ?? HeroNameGenerator.ClanNamePreview(CreationSession.Current),
                    typed => CreationSession.Current.PlayerClanName = typed),
                m => { }
            );
        }

        private static TextObject Named(string name)
        {
            var title = new TextObject("{=CSR_RealmName_Option}{REALM}");
            title.SetTextVariable("REALM", name);
            return title;
        }

        /// <summary>One name the culture's own pools offered, as the panel entry the row carries.</summary>
        private static string Offered(string key, string name)
        {
            var text = new TextObject(key);
            text.SetTextVariable("NAME", name);
            return text.ToString();
        }

        /// <summary>
        ///     What the character is actually called on the option that opens the
        ///     text box: what the player typed, or, until they type, the name the
        ///     box would open on and the campaign would use.
        ///
        ///     A world whose naming customs offer nothing leaves no name to quote,
        ///     and the panel then states the rule rather than going blank, because
        ///     an option with an empty panel is the one thing not allowed here.
        /// </summary>
        private static string Typed(string? chosen, string? preview, string typedKey,
            string untypedKey, string unknownKey)
        {
            string? name = chosen ?? preview;
            if (name == null) return new TextObject(unknownKey).ToString();

            var text = new TextObject(chosen == null ? untypedKey : typedKey);
            text.SetTextVariable("NAME", name);
            return text.ToString();
        }

        private static void Prompt(string titleKey, string descriptionKey, string? seed,
            System.Action<string> accept)
        {
            Editor.EditorPopups.ShowText(
                new TextObject(titleKey).ToString(),
                new TextObject(descriptionKey).ToString(),
                seed,
                accept);
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
