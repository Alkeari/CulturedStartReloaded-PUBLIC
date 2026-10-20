using System;
using System.Collections.Generic;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Menus
{
    /// <summary>
    ///     The Monarch's founding story: settle peacefully into weakly held land,
    ///     or press a claim and start the reign at war with the dispossessed.
    /// </summary>
    public static class FoundingMenu
    {
        public static void AddFoundingMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_founding_menu",
                CreationFlow.DeclaredPrevious("cs_founding_menu"),
                CreationFlow.DeclaredNext("cs_founding_menu"),
                new TextObject("{=CSR_Founding_Title}Your Founding"),
                new TextObject("{=CSR_Founding_Desc}How did your holdings come into your hands?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            ChoiceEffects.Declare("cs_founding_settler", () => FoundingEffect(MonarchFounding.Settler));
            ChoiceEffects.Declare("cs_founding_claimant", () => FoundingEffect(MonarchFounding.Claimant));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_founding_settler",
                new TextObject("{=CSR_Founding_Settler}Settler"),
                new TextObject(
                    "{=CSR_Founding_Settler_Desc}Your holdings come from a weak realm's neglected lands. No one declares war on you today."),
                args => { },
                m => true,
                m => CreationSession.Current.SelectedFounding = MonarchFounding.Settler,
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_founding_claimant",
                new TextObject("{=CSR_Founding_Claimant}Claimant"),
                new TextObject(
                    "{=CSR_Founding_Claimant_Desc}You pressed an old claim by force. The dispossessed realm declares war on your new kingdom."),
                args => { },
                m => true,
                m => CreationSession.Current.SelectedFounding = MonarchFounding.Claimant,
                m => { }
            ));

            manager.AddNewMenu(menu);
            AddKingdomNameMenu(manager);
        }

        /// <summary>
        ///     Its own page, so naming never competes with the founding choice:
        ///     Settler and Claimant both walk through it.
        /// </summary>
        private static void AddKingdomNameMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_kingdom_name_menu",
                CreationFlow.DeclaredPrevious("cs_kingdom_name_menu"),
                CreationFlow.DeclaredNext("cs_kingdom_name_menu"),
                new TextObject("{=CSR_KingdomName_Menu_Title}The Kingdom's Name"),
                new TextObject("{=CSR_KingdomName_Menu_Desc}A realm is spoken into being. What will the heralds proclaim?"),
                CharacterPreviewHelper.CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            // The heralds' three, offered as the names themselves rather than as a
            // button that hides what it will do
            for (int slot = 0; slot < ComposedOptionCount; slot++)
            {
                int index = slot;
                var title = new TextObject("{=CSR_RealmName_Option}{REALM}");
                var description = new TextObject(
                    "{=CSR_KingdomName_Heralds_Desc}Your heralds would have it so, and the name would settle on the realm like a seal on wax.");

                ChoiceEffects.Declare("cs_kingdom_name_composed_" + index,
                    () => RealmEffect(Composed(index)));

                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    "cs_kingdom_name_composed_" + index,
                    title,
                    description,
                    args => { },
                    MenuText.Live(description,
                        _ => title.SetTextVariable("REALM", ComposedName(index) ?? string.Empty),
                        () => ComposedName(index) != null),
                    m =>
                    {
                        CreationSession.Current.KingdomName = ComposedName(index);
                        CreationSession.Current.KingdomNameStyle = KingdomNameStyle.Automatic;
                        CreationSession.Current.KingdomNameDecided = true;
                    },
                    m => { }
                ));
            }

            ChoiceEffects.Declare("cs_kingdom_name_clan", ClanNamedEffect);
            ChoiceEffects.Declare("cs_kingdom_name_custom", CustomNamedEffect);

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_kingdom_name_clan",
                new TextObject("{=CSR_KingdomName_Clan}Named for Your Clan"),
                new TextObject("{=CSR_KingdomName_Clan_Desc}The realm carries your clan's name, as realms have always done."),
                args => { },
                m => true,
                m =>
                {
                    CreationSession.Current.KingdomName = null;
                    CreationSession.Current.KingdomNameStyle = KingdomNameStyle.Clan;
                    CreationSession.Current.KingdomNameDecided = true;
                },
                m => { }
            ));

            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_kingdom_name_custom",
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom"),
                new TextObject("{=CSR_Founding_Name_Desc}Set your kingdom's name now, before the campaign begins."),
                args => { },
                m => true,
                m => PromptForKingdomName(),
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        /// <summary>
        ///     What a founding costs in wars, which is the whole of what the answer
        ///     decides: both foundings take the same capital and the same castles,
        ///     and only a Claimant's are taken from somebody who objects.
        ///
        ///     The realm named is the one the hall chosen a chapter earlier belongs
        ///     to, which is the first realm
        ///     <see cref="Services.Application.Scenarios.MonarchScenario"/> records
        ///     as dispossessed. The castles the crown seizes are not chosen yet, so
        ///     what is said about them is that they count too, rather than which
        ///     ones they will be.
        /// </summary>
        private static string FoundingEffect(MonarchFounding founding)
        {
            if (founding != MonarchFounding.Claimant)
                return new TextObject(
                    "{=CSR_Panel_Founding_Settler}Wars: none, and your kingdom is proclaimed at peace with every realm on the map").ToString();

            var dispossessed = CreationSession.Current.SelectedSettlement?.OwnerClan?.Kingdom;
            var war = new TextObject(dispossessed == null
                ? "{=CSR_Panel_Founding_ClaimantUnnamed}Wars: the realm your capital is taken from, from the day your kingdom is proclaimed (which realm that is settles when your capital does)"
                : "{=CSR_Panel_Founding_Claimant}Wars: {REALM}, from the day your kingdom is proclaimed, since your capital is taken from it");
            if (dispossessed != null) war.SetTextVariable("REALM", dispossessed.Name);

            return ChoiceEffects.Stated(war.ToString(), new TextObject(
                    "{=CSR_Panel_Founding_ClaimantMore}Wars: every other realm a castle of your founding is taken from, as well")
                .ToString());
        }

        /// <summary>
        ///     The name the realm is proclaimed under, built by the generator the
        ///     founding itself calls, so the style read here is the style written
        ///     on the kingdom.
        /// </summary>
        private static string RealmEffect(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new TextObject(
                    "{=CSR_Panel_Realm_None}Name: none, since your heralds have none to offer here").ToString();

            var realm = Built(name!);

            var proclaimed = new TextObject(
                "{=CSR_Panel_Realm_Name}Name: {NAME}, your kingdom's when the campaign opens");
            proclaimed.SetTextVariable("NAME", realm?.Name ?? name!);
            if (realm == null) return proclaimed.ToString();

            var styled = new TextObject(
                "{=CSR_Panel_Realm_Title}Title: {TITLE}, and you are its {RULER}");
            styled.SetTextVariable("TITLE", realm.Title);
            styled.SetTextVariable("RULER", realm.RulerTitle);

            return ChoiceEffects.Stated(proclaimed.ToString(), styled.ToString());
        }

        /// <summary>
        ///     The heralds' name at one slot, and the full set of names built from
        ///     it, with whatever the generator cannot answer read as no name at all.
        ///
        ///     The panel is drawn on every tick of the stage and may not throw out
        ///     of one, because a throw there leaves the panel blank.
        /// </summary>
        private static string? Composed(int index)
        {
            try
            {
                return ComposedName(index);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"FoundingMenu: reading the heralds' name at {index} failed: {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc cref="Composed"/>
        private static KingdomNameGenerator.RealmName? Built(string name)
        {
            try
            {
                return KingdomNameGenerator.BuildRealm(CreationSession.Current, name);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"FoundingMenu: building the realm's names from '{name}' failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     The realm named for the clan. The clan is named in the last chapter
        ///     of creation, so before that the answer is the rule rather than a
        ///     name, and after it the name itself.
        /// </summary>
        private static string ClanNamedEffect()
        {
            var clan = CreationSession.Current.PlayerClanName;
            if (string.IsNullOrWhiteSpace(clan))
                return new TextObject(
                    "{=CSR_Panel_Realm_Clan}Name: your clan's, which the last chapter of creation asks you for").ToString();

            var named = new TextObject("{=CSR_Panel_Realm_ClanNamed}Name: {CLAN}, your clan's, and your kingdom's with it");
            named.SetTextVariable("CLAN", clan!);
            return named.ToString();
        }

        /// <summary>
        ///     The name typed by hand, stated as what stands now once it is typed
        ///     so a player who walks back onto this answer reads their own words
        ///     rather than the invitation to write them.
        /// </summary>
        private static string CustomNamedEffect()
        {
            var session = CreationSession.Current;
            if (session.KingdomNameStyle == KingdomNameStyle.Custom &&
                !string.IsNullOrWhiteSpace(session.KingdomName))
                return RealmEffect(session.KingdomName);

            return new TextObject(
                "{=CSR_Panel_Realm_Custom}Name: whatever you type, and your kingdom is proclaimed exactly so").ToString();
        }

        private const int ComposedOptionCount = 3;

        private static string? ComposedName(int index)
        {
            var names = KingdomNameGenerator.Candidates(CreationSession.Current);
            return index >= 0 && index < names.Count ? names[index] : null;
        }

        private static void PromptForKingdomName()
        {
            Editor.EditorPopups.ShowPrompt(
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(),
                new TextObject("{=CSR_KingdomName_Desc}Choose the name your realm will carry into history.").ToString(),
                null,
                new TextObject("{=CSR_KingdomName_Confirm}Proclaim").ToString(),
                new TextObject("{=CSR_KingdomName_Cancel}Keep Current").ToString(),
                input =>
                {
                    var cleaned = (input ?? string.Empty).Replace("{", "").Replace("}", "").Trim();
                    if (cleaned.Length == 0) return;

                    CreationSession.Current.KingdomName = cleaned;
                    CreationSession.Current.KingdomNameStyle = KingdomNameStyle.Custom;
                    CreationSession.Current.KingdomNameDecided = true;
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        new TextObject("{=CSR_KingdomName_Set}Your realm will be proclaimed as {KINGDOM_NAME}.")
                            .SetTextVariable("KINGDOM_NAME", cleaned)
                            .ToString()));
                });
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
