using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Flow;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.CulturedStart
{
    /// <summary>
    ///     Kingdom and holding pickers. One option per settlement (not one per
    ///     start type per settlement), with culture availability precomputed at
    ///     build time so render-time conditions stay cheap.
    /// </summary>
    public static class ContextualMenus
    {
        public static void AddContextualMenus(CharacterCreationManager manager)
        {
            AddKingdomMenu(manager);
            AddSettlementMenu(manager);
        }

        private static void AddKingdomMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_kingdom_select",
                CreationFlow.DeclaredPrevious("cs_kingdom_select"),
                CreationFlow.DeclaredNext("cs_kingdom_select"),
                new TextObject("{=CSR_Kingdom_Title}Select Realm"),
                new TextObject("{=CSR_Kingdom_Desc}Choose the realm your story is bound to: your liege, your patron, or your enemy."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            // A landed vassal is given one of the realm's holdings and a rebel seizes one of its
            // castles, so a realm holding neither leads to a holding screen with nothing on it.
            // Precomputed with the rest, since ownership does not move during character creation.
            var realmsWithHoldings = new HashSet<Kingdom>(
                Settlement.All
                    .Where(s => (s.IsTown || s.IsCastle) && s.OwnerClan?.Kingdom != null)
                    .Select(s => s.OwnerClan!.Kingdom!));
            var realmsWithCastles = new HashSet<Kingdom>(
                Settlement.All
                    .Where(s => s.IsCastle && s.OwnerClan?.Kingdom != null)
                    .Select(s => s.OwnerClan!.Kingdom!));

            // The same yield the start types get: filtering every realm away would strand the
            // player, so a filter that empties the screen is no filter at all.
            bool Offers(Kingdom kingdom, StartType startType) => startType switch
            {
                StartType.RebelClan => realmsWithCastles.Count == 0 || realmsWithCastles.Contains(kingdom),
                StartType.LandedVassal => realmsWithHoldings.Count == 0 || realmsWithHoldings.Contains(kingdom),
                _ => true
            };

            foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated).OrderBy(k => k.Name?.ToString() ?? ""))
            {
                var k = kingdom;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"cs_kingdom_{k.StringId}",
                    k.Name,
                    new TextObject("{=CSR_Kingdom_Option_Desc}Bind your fate to {KINGDOM_NAME}.")
                        .SetTextVariable("KINGDOM_NAME", k.Name),
                    args => { },
                    m => Offers(k, CreationSession.Current.SelectedStartType),
                    m =>
                    {
                        CreationSession.Current.SelectedKingdom = k;
                        CreationSession.Current.SelectedSettlement = null;
                    },
                    m => { }
                ));
            }

            manager.AddNewMenu(menu);
        }

        private static void AddSettlementMenu(CharacterCreationManager manager)
        {
            var menu = new NarrativeMenu(
                "cs_settlement_select",
                CreationFlow.DeclaredPrevious("cs_settlement_select"),
                CreationFlow.DeclaredNext("cs_settlement_select"),
                new TextObject("{=CSR_Settlement_Title}Choose Your Holding"),
                new TextObject("{=CSR_Settlement_Desc}Select the holding from which you will begin."),
                CreatePlayerCharacter(),
                GetPlayerCharacterArgs
            );

            // Precomputed availability: cheap render-time conditions
            var culturesWithTowns = new HashSet<CultureObject>(
                Settlement.All.Where(s => s.IsTown && s.Culture != null).Select(s => s.Culture!));
            var kingdomCultureFiefs = new HashSet<(Kingdom, CultureObject)>(
                Settlement.All
                    .Where(s => (s.IsTown || s.IsCastle) && s.Culture != null && s.OwnerClan?.Kingdom != null)
                    .Select(s => (s.OwnerClan!.Kingdom!, s.Culture!)));

            foreach (var settlement in Settlement.All
                         .Where(s => s.IsTown || s.IsCastle)
                         .OrderBy(s => s.Name?.ToString() ?? ""))
            {
                var s = settlement;
                menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                    $"cs_holding_{s.StringId}",
                    s.Name,
                    new TextObject("{=CSR_Holding_Select}Claim {SETTLEMENT} as your own.")
                        .SetTextVariable("SETTLEMENT", s.Name),
                    args => { },
                    m =>
                    {
                        var session = CreationSession.Current;
                        var culture = session.SelectedCulture;

                        if (session.SelectedStartType == StartType.Monarch)
                        {
                            if (!s.IsTown) return false;
                            // Culture-filtered, showing all when the culture has no towns
                            return culture == null || s.Culture == culture ||
                                   !culturesWithTowns.Contains(culture);
                        }

                        if (session.SelectedStartType == StartType.LandedVassal)
                        {
                            var kingdom = session.SelectedKingdom;
                            if (kingdom == null || s.OwnerClan?.Kingdom != kingdom) return false;
                            return culture == null || s.Culture == culture ||
                                   !kingdomCultureFiefs.Contains((kingdom, culture));
                        }

                        if (session.SelectedStartType == StartType.RebelClan)
                        {
                            // Rebels seize a castle of the realm they rise against
                            var kingdom = session.SelectedKingdom;
                            return s.IsCastle && kingdom != null && s.OwnerClan?.Kingdom == kingdom;
                        }

                        return false;
                    },
                    m =>
                    {
                        CreationSession.Current.SelectedSettlement = s;
                        CSLogger.Info($"Holding selected: {s.Name}");
                    },
                    m => { }
                ));
            }

            // Auto-assign option
            menu.AddNarrativeMenuOption(new NarrativeMenuOption(
                "cs_settlement_auto",
                new TextObject("{=CSR_Settlement_Auto}Let Stewards Assign"),
                new TextObject("{=CSR_Settlement_Auto_Desc}Assign a suitable holding for your station and culture automatically."),
                args => { },
                m =>
                {
                    var session = CreationSession.Current;
                    return session.SelectedStartType is StartType.Monarch
                        or StartType.LandedVassal or StartType.RebelClan;
                },
                m =>
                {
                    CreationSession.Current.SelectedSettlement = null;
                    CSLogger.Info("Settlement selection set to auto");
                },
                m => { }
            ));

            manager.AddNewMenu(menu);
        }

        private static List<NarrativeMenuCharacter> CreatePlayerCharacter()
        {
            return CharacterPreviewHelper.CreatePlayerCharacter();
        }

        private static List<NarrativeMenuCharacterArgs> GetPlayerCharacterArgs(
            CultureObject? culture, string occupationType, CharacterCreationManager manager)
        {
            return CharacterPreviewHelper.GetPlayerCharacterArgs(culture, occupationType, manager);
        }
    }
}
