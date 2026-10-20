using System;
using System.Collections.Generic;
using System.Linq;
using CulturedStartReloaded.CharacterCreation.Menus;
using CulturedStartReloaded.CharacterCreation.Session;
using CulturedStartReloaded.Models;
using CulturedStartReloaded.Services;
using CulturedStartReloaded.Services.Application;
using CulturedStartReloaded.Services.Application.Scenarios;
using CulturedStartReloaded.Services.Application.Steps;
using CulturedStartReloaded.Settings;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.CharacterCreation.Editor
{
    /// <summary>
    ///     The Start Editor: category navigation on the left, rows on the right,
    ///     the vanilla 3D character untouched in the middle. Every row opens a
    ///     popup picker or exact-number prompt; rows appear only when they make
    ///     sense for the current selections. Gear tabs dress the 3D character in
    ///     the outfit being edited, and every stat change updates the vanilla
    ///     stat panel immediately. Done finishes character creation.
    /// </summary>
    public class StartEditorVM : ViewModel
    {
        private readonly Action _onClose;
        // The session as the editor found it; Close and Escape put it back
        private readonly string? _openSnapshot;
        private MBBindingList<StartEditorTabVM> _tabs = new();
        private MBBindingList<ViewModel> _rows = new();
        private string _title = string.Empty;
        private string _doneText = string.Empty;
        private string _resetText = string.Empty;
        private string _closeText = string.Empty;

        // False when the editor was opened as an escape hatch from a narrative
        // menu: Done then returns to that menu rather than ending creation
        private readonly bool _advanceOnDone;
        private readonly EditorScope _scope;

        public StartEditorVM(Action onClose, EditorScope? scope = null, bool advanceOnDone = true)
        {
            _onClose = onClose;
            _scope = scope ?? EditorScope.Full;
            _advanceOnDone = advanceOnDone;

            // Outside the scope means absent, not grayed out
            void Tab(string key, string nameKey, string hintKey)
            {
                if (!_scope.Allows(key)) return;
                Tabs.Add(new StartEditorTabVM(
                    key, new TextObject(nameKey).ToString(), SelectTab, new TextObject(hintKey).ToString()));
            }

            // A rail of one idea needs no headings over it
            void Group(string nameKey)
            {
                if (_scope.IsScoped) return;
                Tabs.Add(StartEditorTabVM.Header(new TextObject(nameKey).ToString()));
            }

            // The rail reads as five ideas, not thirteen equal buttons
            Tab("identity", "{=CSR_Editor_TabIdentity}Path and Realm",
                "{=CSR_Hint_TabIdentity}Your start type, realm, holdings, and the levers that come with them.");
            // The careers a conversion offers are bounded by the culture and the clan tier the
            // tab above settles, so this tab reads them and stands after it. Absent that
            // conversion there are no careers to show and the tab is not drawn at all
            if (Services.TaomBridge.IsLoaded)
                Tab("career", "{=CSR_Editor_TabCareer}Career",
                    "{=CSR_Hint_TabCareer}The career you begin in and the picks on its board.");
            Group("{=CSR_Group_Character}Character");
            Tab("attributes", "{=CSR_Editor_TabAttributes}Attributes",
                "{=CSR_Hint_TabAttributes}Exact attribute points.");
            Tab("focus", "{=CSR_Editor_TabSkills}Skill Focus",
                "{=CSR_Hint_TabSkills}Exact focus points per skill.");
            Tab("levels", "{=CSR_Editor_TabSkillLevels}Skill Levels",
                "{=CSR_Hint_TabSkillLevels}Exact skill levels.");
            Tab("perks", "{=CSR_Editor_TabPerks}Skill Perks",
                "{=CSR_Hint_TabPerks}Choose perks per skill; unset skills fill automatically.");
            Tab("traits", "{=CSR_Editor_TabTraits}Traits",
                "{=CSR_Hint_TabTraits}Personality trait levels.");
            Group("{=CSR_Group_Gear}Gear");
            Tab("battle", "{=CSR_Editor_TabBattleGear}Battle Gear",
                "{=CSR_Hint_TabBattleGear}Exact battle equipment, weapons, and banner.");
            Tab("civilian", "{=CSR_Editor_TabCivilianGear}Civilian Gear",
                "{=CSR_Hint_TabCivilianGear}Exact civilian outfit.");
            Tab("stealth", "{=CSR_Editor_TabStealthGear}Stealth Gear",
                "{=CSR_Hint_TabStealthGear}Exact stealth outfit.");
            Group("{=CSR_Group_People}People");
            Tab("family", "{=CSR_Editor_TabFamily}Family",
                "{=CSR_Hint_TabFamily}Compose your starting family, member by member.");
            Tab("companions", "{=CSR_Editor_TabCompanions}Companions",
                "{=CSR_Hint_TabCompanions}How many companions ride with you, and who they are.");
            if (!_scope.IsScoped) Tabs.Add(StartEditorTabVM.Header(string.Empty));
            Tab("resources", "{=CSR_Editor_TabResources}Resources",
                "{=CSR_Hint_TabResources}Gold, influence, troops, level, and the party's inventory.");

            // The wagons without the purse. The Resources tab holds both, and a
            // provisions question must not open the treasury
            if (_scope.IsScoped)
                Tab("stores", "{=CSR_Editor_TabStores}Party Stores",
                    "{=CSR_Hint_TabStores}Food, mounts and trade goods loaded onto the wagons.");

            // Never inside a scope: one click here would replace the whole
            // character, including everything the story just settled
            if (!_scope.IsScoped)
                Tab("presets", "{=CSR_Editor_TabPresets}Presets",
                    "{=CSR_Hint_TabPresets}Save and load complete setups.");

            Title = new TextObject(_scope.TitleKey ?? "{=CSR_Editor_Title}Start Editor").ToString();
            DoneText = advanceOnDone
                ? new TextObject("{=CSR_Editor_Done}Begin Campaign").ToString()
                : new TextObject("{=CSR_Editor_DoneReturn}Save and Return").ToString();
            ResetText = new TextObject("{=CSR_Editor_Reset}Reset Tab").ToString();
            CloseText = new TextObject("{=CSR_Editor_Close}Close").ToString();
            DoneHint = new HintViewModel(advanceOnDone
                ? new TextObject("{=CSR_Hint_Done}Begin the campaign with this start; asks before it commits.")
                : new TextObject("{=CSR_Hint_DoneReturn}Keep these changes and go back to the menu you came from."));
            ResetHint = new HintViewModel(
                new TextObject("{=CSR_Hint_Reset}Reset what is on screen; asks first."));
            CloseHint = new HintViewModel(
                new TextObject("{=CSR_Hint_Close}Close the editor and restore everything as it found it."));

            // Snapshot BEFORE the editor changes anything, including its own
            // None-by-default gear pass, so Close restores what the player had
            // when they opened it rather than what the editor made of it
            _openSnapshot = StartPresetService.CaptureState(CreationSession.Current);

            // Only the custom path starts from nothing equipped. Opened as an
            // escape hatch mid-narrative, this would strip the gear the story
            // just gave the player
            if (advanceOnDone) ApplyCustomGearDefaults();
            EnsureBattlePreview();
            StageViewBridge.RefreshGainedProperties();
            SelectTab(Tabs.First(t => t.IsRow));
        }

        [DataSourceProperty]
        public MBBindingList<StartEditorTabVM> Tabs
        {
            get => _tabs;
            set
            {
                if (value == _tabs) return;
                _tabs = value;
                OnPropertyChangedWithValue(value, nameof(Tabs));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ViewModel> Rows
        {
            get => _rows;
            set
            {
                if (value == _rows) return;
                _rows = value;
                OnPropertyChangedWithValue(value, nameof(Rows));
            }
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
        public string DoneText
        {
            get => _doneText;
            set
            {
                if (value == _doneText) return;
                _doneText = value;
                OnPropertyChangedWithValue(value, nameof(DoneText));
            }
        }

        [DataSourceProperty]
        public string ResetText
        {
            get => _resetText;
            set
            {
                if (value == _resetText) return;
                _resetText = value;
                OnPropertyChangedWithValue(value, nameof(ResetText));
            }
        }

        [DataSourceProperty]
        public string CloseText
        {
            get => _closeText;
            set
            {
                if (value == _closeText) return;
                _closeText = value;
                OnPropertyChangedWithValue(value, nameof(CloseText));
            }
        }

        [DataSourceProperty]
        public HintViewModel DoneHint { get; }

        [DataSourceProperty]
        public HintViewModel ResetHint { get; }

        [DataSourceProperty]
        public HintViewModel CloseHint { get; }

        /// <summary>
        ///     Finishes the whole character creation flow, not just the editor;
        ///     confirmed, because there is no way back once the campaign starts.
        /// </summary>
        public void ExecuteDone()
        {
            // Nothing irreversible happens when the editor only hands the player
            // back to the menu they opened it from, so it does not ask
            if (!_advanceOnDone)
            {
                ShowOutfit(OutfitKind.Battle);
                _onClose();
                return;
            }

            SettleAutomaticRealm();

            EditorPopups.ShowConfirm(
                new TextObject("{=CSR_Editor_Done}Begin Campaign").ToString(),
                new TextObject("{=CSR_Editor_Done_Confirm}Begin the campaign with this start? Character creation ends here.")
                    .ToString(),
                new TextObject("{=CSR_Editor_Done_Yes}Begin").ToString(),
                new TextObject("{=CSR_Editor_Done_No}Not Yet").ToString(),
                () =>
                {
                    ShowOutfit(OutfitKind.Battle);
                    _onClose();
                    StageViewBridge.AdvanceStage();
                });
        }

        /// <summary>
        ///     Turns an Automatic realm into a real one before the campaign starts.
        ///
        ///     Automatic reads as "choose one for me" and left the session holding
        ///     nothing, which is the one thing the four starts that need a realm
        ///     refuse: each of their appliers fails validation on a null kingdom,
        ///     the orchestrator skips the whole step, and a mercenary who never
        ///     touched that row began the campaign with no contract, no realm and
        ///     no explanation. An option that can grant nothing is a defect in the
        ///     option, so the choice is made here instead of being reported as a
        ///     missing one afterwards.
        ///
        ///     A realm of the character's own people first, since that is what a
        ///     player who did not choose most likely expects, and any realm that
        ///     can serve this start rather than none.
        /// </summary>
        private static void SettleAutomaticRealm()
        {
            var session = CreationSession.Current;
            if (session.SelectedKingdom != null) return;

            var startType = session.SelectedStartType;
            if (startType is not (StartType.LandedVassal or StartType.LandlessVassal
                or StartType.Mercenary or StartType.Outlaw)) return;

            var eligible = Kingdom.All
                .Where(k => !k.IsEliminated && SettlementFinder.RealmCanServe(k, startType) &&
                            ContextualMenus.ServesTheSameSide(k))
                .ToList();
            if (eligible.Count == 0)
            {
                CSLogger.Warn($"StartEditorVM: no realm on the map can serve a {startType} start, " +
                              "so Automatic has nothing to settle on.");
                return;
            }

            var ownPeople = eligible.Where(k => k.Culture == session.SelectedCulture).ToList();
            session.SelectedKingdom = CSRandom.Pick(ownPeople.Count > 0 ? ownPeople : eligible);
            CSLogger.Info($"StartEditorVM: Automatic settled the realm on " +
                          $"{session.SelectedKingdom?.Name}.");
        }

        /// <summary>
        ///     Backs out to the menu behind the editor. Close means cancel: the
        ///     session goes back to exactly how the editor found it, and when
        ///     that would discard real edits, it asks first.
        /// </summary>
        public void ExecuteClose()
        {
            bool hasChanges = _openSnapshot != null &&
                              StartPresetService.CaptureState(CreationSession.Current) != _openSnapshot;
            if (!hasChanges)
            {
                ShowOutfit(OutfitKind.Battle);
                _onClose();
                return;
            }

            EditorPopups.ShowConfirm(
                CloseText,
                new TextObject("{=CSR_Editor_Close_Confirm}Close and discard the changes made in this editor session?")
                    .ToString(),
                new TextObject("{=CSR_Editor_Close_Yes}Discard and Close").ToString(),
                new TextObject("{=CSR_Editor_Close_No}Keep Editing").ToString(),
                () =>
                {
                    if (StartPresetService.RestoreState(_openSnapshot!, CreationSession.Current))
                    {
                        RegenerateBattlePreview();
                        StageViewBridge.RefreshGainedProperties();
                    }

                    ShowOutfit(OutfitKind.Battle);
                    _onClose();
                },
                destructive: true);
        }

        /// <summary>
        ///     Reset touches only what is on screen, and the button says which
        ///     of the three it is about to do. The distinction matters: on the
        ///     gear tabs it empties slots, everywhere else it hands them back to
        ///     automatic, and those are opposite outcomes to live with.
        /// </summary>
        public void ExecuteReset()
        {
            if (_heroSpecEditing != null && _heroPage != null)
            {
                var values = _heroPage switch
                {
                    "attributes" => _heroSpecEditing.Attributes,
                    "focus" => _heroSpecEditing.Focus,
                    "levels" => _heroSpecEditing.SkillLevels,
                    _ => _heroSpecEditing.Traits
                };
                ConfirmReset(
                    new TextObject("{=CSR_Editor_ResetPage_Confirm}Return every setting on this page to automatic?")
                        .ToString(),
                    () =>
                    {
                        values.Clear();
                        RebuildCurrentTab();
                    });
                return;
            }

            if (_heroSpecEditing != null)
            {
                var spec = _heroSpecEditing;
                ConfirmReset(
                    new TextObject("{=CSR_Editor_ResetSheet_Confirm}Reset this character back to fully generated?")
                        .ToString(),
                    () =>
                    {
                        spec.Clear();
                        StageViewBridge.RefreshGainedProperties();
                        RebuildCurrentTab();
                    });
                return;
            }

            if (_memberEditing != null)
            {
                var member = _memberEditing;
                ConfirmReset(
                    new TextObject("{=CSR_Editor_ResetMember_Confirm}Reset this family member's whole setup?")
                        .ToString(),
                    () =>
                    {
                        member.Age = null;
                        member.Aging = SiblingAging.Smart;
                        member.SmartOffset = null;
                        member.Marriage = FamilyMarriage.Single;
                        member.StaysInSettlement = false;
                        member.Advanced.Clear();
                        StageViewBridge.RefreshGainedProperties();
                        RebuildCurrentTab();
                    });
                return;
            }

            var key = SelectedTabKey();
            ConfirmReset(ResetQuestion(key), () => ApplyTabReset(key));
        }

        /// <summary>The word on the button, matched to what pressing it does.</summary>
        private string ResetLabel(string tabKey)
        {
            if (_heroSpecEditing != null && _heroPage != null)
                return new TextObject("{=CSR_Editor_ResetPage}Reset Page").ToString();
            if (_heroSpecEditing != null)
                return new TextObject("{=CSR_Editor_ResetHero}Reset Character").ToString();
            if (_memberEditing != null)
                return new TextObject("{=CSR_Editor_ResetMemberBtn}Reset Member").ToString();

            return tabKey switch
            {
                "battle" or "civilian" or "stealth" =>
                    new TextObject("{=CSR_Editor_ResetGear}Clear Gear").ToString(),
                "presets" => new TextObject("{=CSR_Editor_ResetAll}Reset Everything").ToString(),
                _ => new TextObject("{=CSR_Editor_Reset}Reset Tab").ToString()
            };
        }

        private static string ResetQuestion(string tabKey) => tabKey switch
        {
            "battle" or "civilian" or "stealth" => new TextObject(
                    "{=CSR_Editor_ResetGear_Confirm}Empty every slot on this tab? Set All Slots hands them back to the quartermaster instead.")
                .ToString(),
            "presets" => new TextObject(
                    "{=CSR_Editor_ResetAll_Confirm}Return every tab to automatic and clear the whole setup? Saved presets are kept.")
                .ToString(),
            _ => new TextObject("{=CSR_Editor_Reset_Confirm}Return every setting on this tab to automatic?")
                .ToString()
        };

        private void RefreshResetAffordance()
        {
            ResetText = ResetLabel(SelectedTabKey());
        }

        private void ConfirmReset(string question, Action onConfirm)
        {
            EditorPopups.ShowConfirm(
                ResetText,
                question,
                new TextObject("{=CSR_Editor_Reset_Yes}Reset").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                onConfirm,
                destructive: true);
        }

        private void ApplyTabReset(string tabKey)
        {
            var session = CreationSession.Current;

            // The Presets tab owns no settings of its own, so its Reset is the
            // one bulk action the editor otherwise lacked: every tab at once.
            // Taken from the rail, so a tab the build does not show is skipped
            if (tabKey == "presets")
                foreach (var key in Tabs.Where(t => t.IsRow && t.Key != "presets").Select(t => t.Key).ToList())
                    ResetTabState(key, session);
            else
                ResetTabState(tabKey, session);

            StageViewBridge.RefreshGainedProperties();
            RebuildCurrentTab();
        }

        private void ResetTabState(string tabKey, CharacterCreationSession session)
        {
            switch (tabKey)
            {
                case "identity":
                    session.SelectedKingdom = null;
                    session.SelectedSettlement = null;
                    session.SelectedLocation = null;
                    session.KingdomName = null;
                    session.CustomRenown = null;
                    session.CustomAge = null;
                    session.VassalClanCount = 0;
                    session.GrantLandsToVassals = false;
                    session.SelectedPolicies.Clear();
                    session.CustomWars = null;
                    session.StartingWorkshops = 0;
                    session.CustomCrimeRating = null;
                    session.CustomPackAnimals = null;
                    session.RealmWars = null;
                    session.CustomGarrison = null;
                    session.CustomContractPay = null;
                    session.OutlawWantedBy = null;
                    session.RebelAllyCount = 0;
                    break;
                case "attributes": session.CustomAttributes.Clear(); break;
                case "focus": session.CustomFocus.Clear(); break;
                case "levels": session.CustomSkillLevels.Clear(); break;
                case "perks": session.CustomPerks.Clear(); break;
                case "traits": session.CustomTraits.Clear(); break;
                case "career":
                    session.SelectedCareerId = null;
                    session.CareerChoiceIds.Clear();
                    break;
                case "resources":
                    session.CustomFactionResource = null;
                    session.CustomGold = null;
                    session.CustomInfluence = null;
                    session.CustomTroops = null;
                    session.CustomLevel = null;
                    session.CustomFood.Clear();
                    session.CustomMounts.Clear();
                    session.CustomTradeGoods.Clear();
                    // The plan is loaded into the lists again when the wagons are next drawn
                    session.ProvisionsMaterialized = false;
                    break;
                case "stores":
                    session.CustomFood.Clear();
                    session.CustomMounts.Clear();
                    session.CustomTradeGoods.Clear();
                    session.ProvisionsMaterialized = false;
                    break;
                case "battle":
                    ResetOutfitToNone(session, OutfitKind.Battle);
                    session.ExactBanner = null;
                    foreach (var weapon in session.WeaponChoices)
                    {
                        weapon.Reset();
                        weapon.ExplicitlyEmpty = true;
                    }

                    RegenerateBattlePreview();
                    break;
                case "civilian":
                    ResetOutfitToNone(session, OutfitKind.Civilian);
                    ShowOutfit(OutfitKind.Civilian);
                    break;
                case "stealth":
                    ResetOutfitToNone(session, OutfitKind.Stealth);
                    ShowOutfit(OutfitKind.Stealth);
                    break;
                case "family":
                    session.FamilyMembers.RemoveAll(m =>
                        m.Relation is not (FamilyRelation.Father or FamilyRelation.Mother));
                    foreach (var member in session.FamilyMembers)
                    {
                        member.IsAlive = false;
                        member.Advanced.Clear();
                    }

                    break;
                case "companions":
                    foreach (var companionSpec in session.CompanionSpecs) companionSpec.Clear();
                    break;
            }
        }

        // The custom path's default is explicit None (a null entry), not a
        // removed entry, which would mean the quartermaster picks again
        private static void ResetOutfitToNone(CharacterCreationSession session, OutfitKind kind)
        {
            foreach (var key in session.ExactOutfit.Keys.Where(k => k.Kind == kind).ToList())
                session.ExactOutfit[key] = null;

            var slots = new[]
            {
                EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves,
                EquipmentIndex.Cape, EquipmentIndex.Horse, EquipmentIndex.HorseHarness
            };
            foreach (var slot in slots)
                if (ArmorQuery.SlotAllowed(slot, kind))
                    session.ExactOutfit[(kind, slot)] = null;

            if (kind != OutfitKind.Battle)
            {
                for (int slotIndex = 0; slotIndex < 4; slotIndex++)
                    session.ExactOutfit[(kind, EquipmentIndex.Weapon0 + slotIndex)] = null;
                return;
            }

            // The battle set keeps its weapons in WeaponChoices and its banner in
            // ExactBanner rather than in ExactOutfit, so emptying every slot on the
            // tab has to reach those too. Returning early here left the four weapon
            // rows and the banner untouched while the armor rows read None.
            foreach (var weapon in session.WeaponChoices)
            {
                weapon.Reset();
                weapon.ExplicitlyEmpty = true;
            }

            session.ExactBanner = null;
        }

        private string SelectedTabKey()
        {
            var selected = Tabs.FirstOrDefault(t => t.IsSelected);
            return selected != null && selected.Key.Length > 0 ? selected.Key : "identity";
        }

        // Non-null while a companion's or relative's own sheet is on the panel
        private HeroSpec? _heroSpecEditing;
        private string _heroEditTitle = string.Empty;

        // "attributes", "focus", "levels" or "traits" while one page of that sheet is on the panel
        private string? _heroPage;
        private FamilyMemberSpec? _memberEditing;
        private string _memberEditTitle = string.Empty;

        private void RebuildCurrentTab()
        {
            RefreshResetAffordance();

            if (_heroSpecEditing != null)
            {
                var heroRows = new MBBindingList<ViewModel>();
                if (_heroPage == null)
                    BuildHeroEditRows(heroRows, _heroSpecEditing, _heroEditTitle);
                else
                    BuildHeroPageRows(heroRows, _heroSpecEditing, _heroEditTitle, _heroPage);
                Rows = heroRows;
                return;
            }

            if (_memberEditing != null)
            {
                var memberRows = new MBBindingList<ViewModel>();
                BuildMemberEditRows(memberRows, _memberEditing, _memberEditTitle);
                Rows = memberRows;
                return;
            }

            var selected = Tabs.FirstOrDefault(t => t.IsSelected) ?? Tabs.First(t => t.IsRow);
            SelectTab(selected);
        }

        private void EnterHeroEdit(HeroSpec spec, string title)
        {
            _heroSpecEditing = spec;
            _heroEditTitle = title;
            _heroPage = null;
            RebuildCurrentTab();
        }

        private void ExitHeroEdit()
        {
            _heroSpecEditing = null;
            _heroEditTitle = string.Empty;
            _heroPage = null;
            RebuildCurrentTab();
        }

        private void EnterHeroPage(string page)
        {
            _heroPage = page;
            RebuildCurrentTab();
        }

        private void ExitHeroPage()
        {
            _heroPage = null;
            RebuildCurrentTab();
        }

        private void EnterMemberEdit(FamilyMemberSpec member, string title)
        {
            _memberEditing = member;
            _memberEditTitle = title;
            _heroSpecEditing = null;
            _heroPage = null;
            RebuildCurrentTab();
        }

        private void ExitMemberEdit()
        {
            _memberEditing = null;
            _memberEditTitle = string.Empty;
            RebuildCurrentTab();
        }

        private void SelectTab(StartEditorTabVM tab)
        {
            _heroSpecEditing = null;
            _heroEditTitle = string.Empty;
            _heroPage = null;
            _memberEditing = null;
            _memberEditTitle = string.Empty;

            // Read before the selection moves, so only arriving on Skill Perks opens the board,
            // never a rebuild of a tab already showing
            bool arriving = !tab.IsSelected;

            foreach (var t in Tabs)
                t.IsSelected = t == tab;

            var rows = new MBBindingList<ViewModel>();
            switch (tab.Key)
            {
                case "identity": BuildIdentityRows(rows); break;
                case "attributes": BuildAttributeRows(rows); break;
                case "focus": BuildFocusRows(rows); break;
                case "levels": BuildSkillLevelRows(rows); break;
                case "perks": BuildPerkRows(rows); break;
                case "traits": BuildTraitRows(rows); break;
                case "career": BuildCareerRows(rows); break;
                case "resources": BuildResourceRows(rows); break;
                case "stores": BuildFoodRows(rows); break;
                case "battle": BuildGearRows(rows, OutfitKind.Battle); break;
                case "civilian": BuildGearRows(rows, OutfitKind.Civilian); break;
                case "stealth": BuildGearRows(rows, OutfitKind.Stealth); break;
                case "family": BuildFamilyRows(rows); break;
                case "companions": BuildCompanionRows(rows); break;
                case "presets": BuildPresetRows(rows); break;
            }

            // Dress the character in the outfit being edited
            switch (tab.Key)
            {
                case "civilian": ShowOutfit(OutfitKind.Civilian); break;
                case "stealth": ShowOutfit(OutfitKind.Stealth); break;
                default: ShowOutfit(OutfitKind.Battle); break;
            }

            Rows = rows;
            RefreshResetAffordance();

            if (arriving && tab.Key == "perks")
                OpenPerkBoard();
        }

        /// <summary>
        ///     The custom path starts with nothing equipped: every slot is set to
        ///     None the first time the editor opens, and the player opts INTO the
        ///     quartermaster per slot instead of opting out of it.
        /// </summary>
        private static void ApplyCustomGearDefaults()
        {
            var session = CreationSession.Current;
            if (session.CustomGearDefaultsApplied) return;
            session.CustomGearDefaultsApplied = true;

            var slots = new[]
            {
                EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves,
                EquipmentIndex.Cape, EquipmentIndex.Horse, EquipmentIndex.HorseHarness
            };
            var kinds = new List<OutfitKind>
                { OutfitKind.Battle, OutfitKind.Civilian, OutfitKind.Stealth };

            foreach (var kind in kinds)
            {
                foreach (var slot in slots)
                    if (ArmorQuery.SlotAllowed(slot, kind) && !session.ExactOutfit.ContainsKey((kind, slot)))
                        session.ExactOutfit[(kind, slot)] = null;

                // Weapon slots too; the battle set lives in WeaponChoices instead
                if (kind == OutfitKind.Battle) continue;
                for (int slotIndex = 0; slotIndex < 4; slotIndex++)
                {
                    var weaponSlot = EquipmentIndex.Weapon0 + slotIndex;
                    if (!session.ExactOutfit.ContainsKey((kind, weaponSlot)))
                        session.ExactOutfit[(kind, weaponSlot)] = null;
                }
            }

            foreach (var choice in session.WeaponChoices)
                if (choice.IsKeepAuto)
                    choice.ExplicitlyEmpty = true;
        }

        #region Preview

        /// <summary>
        ///     Guarantees the roster-backed battle preview exists (the custom path
        ///     never walks the narrative gear menus that would create it) and
        ///     overlays every exact pick already made.
        /// </summary>
        private static void EnsureBattlePreview()
        {
            var service = GearCustomizationMenu.PreviewService;
            if (service == null) return;

            var session = CreationSession.Current;
            if (!service.HasPreviewEquipment)
                service.GenerateArmorPreview(session.SelectedCulture,
                    EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance));

            ApplyBattleExactsToPreview();
        }

        /// <summary>Re-rolls the generated battle baseline, then re-applies exact picks.</summary>
        private static void RegenerateBattlePreview()
        {
            var service = GearCustomizationMenu.PreviewService;
            if (service == null) return;

            var session = CreationSession.Current;
            service.PinExactly(BattleSlotsSetByHand(session));
            service.GenerateArmorPreview(session.SelectedCulture,
                EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance));
            ApplyBattleExactsToPreview();
        }

        /// <summary>The battle slots holding an exact pick or a deliberate empty, which a draw leaves alone.</summary>
        private static IEnumerable<EquipmentIndex> BattleSlotsSetByHand(CharacterCreationSession session)
        {
            foreach (var key in session.ExactOutfit.Keys)
                if (key.Kind == OutfitKind.Battle)
                    yield return key.Slot;

            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
            {
                var weaponChoice = session.WeaponChoices[slotIndex];
                if (weaponChoice.ExactItem != null || weaponChoice.ExplicitlyEmpty)
                    yield return EquipmentIndex.Weapon0 + slotIndex;
            }
        }

        private static void ApplyBattleExactsToPreview()
        {
            var service = GearCustomizationMenu.PreviewService;
            if (service == null) return;

            var session = CreationSession.Current;
            foreach (var pair in session.ExactOutfit)
            {
                if (pair.Key.Kind != OutfitKind.Battle) continue;
                if (pair.Value != null)
                    service.SetExactItem(pair.Key.Slot, pair.Value);
                else
                    service.SetSlotEmpty(pair.Key.Slot);
            }

            for (int slotIndex = 0; slotIndex < session.WeaponChoices.Length; slotIndex++)
            {
                var weaponChoice = session.WeaponChoices[slotIndex];
                if (weaponChoice.ExplicitlyEmpty)
                    service.SetSlotEmpty(EquipmentIndex.Weapon0 + slotIndex);
                else if (weaponChoice.ExactItem != null)
                    service.SetExactItem(EquipmentIndex.Weapon0 + slotIndex, weaponChoice.ExactItem);
            }

            // The banner is carried into battle but never shown on the render;
            // an equipped banner item hangs off the character's hip
            service.SetSlotEmpty(EquipmentIndex.ExtraWeaponSlot);
        }

        /// <summary>
        ///     Switches which single outfit the character behind the editor is
        ///     wearing, for a stage that shows one of them.
        ///
        ///     Where the stage already has all three standing side by side it
        ///     restages instead of switching: the one on display there is the
        ///     battle render in the middle, so dressing it in the tab's clothes
        ///     would put the same outfit on screen twice and take the war kit off
        ///     entirely. Restaging still has to happen, because it is what rewrites
        ///     the outfit's own roster from the picks just made.
        /// </summary>
        private static void ShowOutfit(OutfitKind kind)
        {
            if (CharacterPreviewHelper.ShowsEveryOutfit)
            {
                CharacterPreviewHelper.RefreshMenuCharacters();
                return;
            }

            var session = CreationSession.Current;
            GearCustomizationMenu.PreviewService?.ShowOutfit(kind, session, session.SelectedCulture,
                EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance));
        }

        #endregion

        #region Identity

        /// <summary>
        ///     Automatic, the names the culture's own pools offer, or one typed in.
        ///     Automatic is the absence of a stored name, so clearing it puts the
        ///     decision back where it was rather than storing a generated string.
        /// </summary>
        private void ShowNamePicker(string title, string prompt, IReadOnlyList<string> candidates,
            Func<string?> current, Action<string?> apply)
        {
            var autoText = new TextObject("{=CSR_Editor_Auto}Automatic").ToString();
            var typeText = new TextObject("{=CSR_Editor_TypeAName}Type a Name").ToString();

            var options = new List<(string, object?)> { (autoText, null) };
            options.AddRange(candidates.Select(name => (name, (object?)name)));
            options.Add((typeText, "cs_type"));

            EditorPopups.ShowOptions(title, options, picked =>
            {
                if (Equals(picked, "cs_type"))
                {
                    EditorPopups.ShowText(title, prompt, current(), typed =>
                    {
                        apply(typed);
                        RebuildCurrentTab();
                    });
                    return;
                }

                apply(picked as string);
                RebuildCurrentTab();
            });
        }

        private void BuildIdentityRows(MBBindingList<ViewModel> rows)
        {
            var autoText = new TextObject("{=CSR_Editor_Auto}Automatic").ToString();
            int markPath = rows.Count;

            // The two names, first, because they are the only rows on this tab a
            // player is guaranteed to want. Automatic means the culture's own name
            // pools decide, which is what the guided route offers as well
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_YourName}Your Name").ToString(),
                () => CreationSession.Current.PlayerFirstName
                      ?? HeroNameGenerator.FirstNamePreview(CreationSession.Current)
                      ?? autoText,
                () => ShowNamePicker(
                    new TextObject("{=CSR_Name_Prompt_Title}Your Name").ToString(),
                    new TextObject("{=CSR_Name_Prompt_Desc}What are you called?").ToString(),
                    HeroNameGenerator.FirstNameCandidates(CreationSession.Current),
                    () => CreationSession.Current.PlayerFirstName,
                    name => CreationSession.Current.PlayerFirstName = name)));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_ClanName}The Name of Your House").ToString(),
                () => CreationSession.Current.PlayerClanName
                      ?? HeroNameGenerator.ClanNamePreview(CreationSession.Current)
                      ?? autoText,
                () => ShowNamePicker(
                    new TextObject("{=CSR_ClanName_Prompt_Title}Your House").ToString(),
                    new TextObject("{=CSR_ClanName_Prompt_Desc}What is your line called?").ToString(),
                    HeroNameGenerator.ClanNameCandidates(CreationSession.Current),
                    () => CreationSession.Current.PlayerClanName,
                    name => CreationSession.Current.PlayerClanName = name)));

            // Start type
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_StartType}Start Type").ToString(),
                () => CreationSession.Current.SelectedStartType.ToString(),
                () =>
                {
                    var options = Enum.GetValues(typeof(StartType)).Cast<StartType>()
                        .Where(CSSettings.ShowsStartType)
                        .Select(t => (t.ToString(), (object?)t))
                        .ToList();
                    EditorPopups.ShowOptions(
                        new TextObject("{=CSR_Editor_StartType}Start Type").ToString(), options, picked =>
                        {
                            if (picked is not StartType type) return;
                            var session = CreationSession.Current;
                            session.SelectedStartType = type;
                            // The water is a degree of the start type being abandoned, so it
                            // goes with it, and ahead of the nulls below because letting it go
                            // puts back whatever holding it had taken over
                            SeaGrants.Choose(session, SeaDegree.None);
                            session.SelectedKingdom = null;
                            session.SelectedSettlement = null;
                            session.SelectedLocation = null;
                            RebuildCurrentTab();
                        });
                }));

            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Editor_YourAge}Your Age").ToString(),
                GameCaps.MinAdultAge(), GameCaps.MaxAge(), 1,
                () => CreationSession.Current.EffectiveAge,
                v => CreationSession.Current.CustomAge = v,
                isCustomized: () => CreationSession.Current.CustomAge != null,
                revert: () => CreationSession.Current.CustomAge = null));

            // Story progress (story mode only)
            if (Helpers.CSGameModeService.IsStoryMode())
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_StoryProgress_Title}Story Progress").ToString(),
                    () => CreationSession.Current.SelectedQuestProgress.ToString(),
                    () =>
                    {
                        var options = Enum.GetValues(typeof(StoryQuestProgress)).Cast<StoryQuestProgress>()
                            .Select(p => (p.ToString(), (object?)p))
                            .ToList();
                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_StoryProgress_Title}Story Progress").ToString(), options, picked =>
                            {
                                if (picked is StoryQuestProgress progress)
                                    CreationSession.Current.SelectedQuestProgress = progress;
                                RebuildCurrentTab();
                            });
                    }));
            }

            var startType = CreationSession.Current.SelectedStartType;
            int markRealm = rows.Count;

            // A crown is either raised or inherited, and the rows under it belong
            // to one or the other: a realm that already exists has its own name,
            // its own founding story and its own sworn houses, so none of those
            // questions is put to the monarch who takes its throne
            if (startType == StartType.Monarch)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Crown}Your Crown").ToString(),
                    () => CreationSession.Current.SelectedKingdom?.Name?.ToString()
                          ?? new TextObject("{=CSR_Editor_CrownFound}A Realm of Your Own").ToString(),
                    () =>
                    {
                        var options = new List<PickerOption>
                        {
                            new(new TextObject("{=CSR_Editor_CrownFound}A Realm of Your Own").ToString(), null,
                                detail: new TextObject(
                                    "{=CSR_Picker_CrownFoundDetail}A new realm, founded and named by you").ToString())
                        };

                        // Only a realm that holds something, because the throne
                        // comes with that realm's own land and a realm holding
                        // nothing would seat the player on nothing
                        options.AddRange(ContextualMenus.RealmsWithAThrone()
                            .OrderBy(k => k.Name?.ToString() ?? string.Empty)
                            .Select(k => new PickerOption(k.Name?.ToString() ?? k.StringId, k,
                                detail: RealmDetail(k))));

                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Crown}Your Crown").ToString(), options,
                            picked =>
                            {
                                var session = CreationSession.Current;
                                session.SelectedKingdom = picked as Kingdom;
                                // The seat has to come out of the realm now being
                                // ruled, and the one already chosen may be in
                                // somebody else's
                                session.SelectedSettlement = null;
                                RebuildCurrentTab();
                            });
                    }));
            }

            // Founding + kingdom name: a crown being raised, not one inherited
            if (startType == StartType.Monarch && CreationSession.Current.SelectedKingdom == null)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Founding_Title}Your Founding").ToString(),
                    () => CreationSession.Current.SelectedFounding.ToString(),
                    () => EditorPopups.ShowOptions(
                        new TextObject("{=CSR_Founding_Title}Your Founding").ToString(),
                        new List<PickerOption>
                        {
                            new(new TextObject("{=CSR_Founding_Settler}Settler").ToString(), MonarchFounding.Settler,
                                detail: new TextObject(
                                        "{=CSR_Founding_Settler_Desc}Your holdings come from a weak realm's neglected lands. No one declares war on you today.")
                                    .ToString()),
                            new(new TextObject("{=CSR_Founding_Claimant}Claimant").ToString(), MonarchFounding.Claimant,
                                detail: new TextObject(
                                        "{=CSR_Founding_Claimant_Desc}You pressed an old claim by force. The dispossessed realm declares war on your new kingdom.")
                                    .ToString())
                        },
                        picked =>
                        {
                            if (picked is MonarchFounding founding)
                                CreationSession.Current.SelectedFounding = founding;
                            RebuildCurrentTab();
                        })));

                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(),
                    () => CreationSession.Current.KingdomName
                          ?? KingdomNameGenerator.Preview(CreationSession.Current)
                          ?? new TextObject("{=CSR_Editor_Auto}Automatic").ToString(),
                    ShowKingdomNamePicker));

                BuildKingdomSetupRows(rows);
            }

            // Realm: only for start types bound to one
            bool usesRealm = startType is StartType.LandedVassal or StartType.LandlessVassal
                or StartType.Mercenary or StartType.Outlaw or StartType.RebelClan;
            if (usesRealm)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Kingdom}Realm").ToString(),
                    () => CreationSession.Current.SelectedKingdom?.Name?.ToString() ?? autoText,
                    () =>
                    {
                        var options = new List<PickerOption>
                        {
                            new(autoText, null,
                                detail: new TextObject(
                                    "{=CSR_Picker_RealmAutoDetail}A realm that can serve this start, chosen as the campaign begins").ToString())
                        };
                        // Same rule the creation menu uses: a realm that cannot serve this start
                        // is not offered, because choosing one fails the scenario's validation and
                        // the orchestrator then skips the whole step, garrison and workshops with it.
                        options.AddRange(Kingdom.All
                            .Where(k => !k.IsEliminated &&
                                        SettlementFinder.RealmCanServe(k, startType) &&
                                        ContextualMenus.ServesTheSameSide(k))
                            .OrderBy(k => k.Name?.ToString() ?? "")
                            .Select(k => new PickerOption(k.Name?.ToString() ?? k.StringId, k,
                                detail: RealmDetail(k))));
                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Kingdom}Realm").ToString(), options, picked =>
                            {
                                var session = CreationSession.Current;
                                session.SelectedKingdom = picked as Kingdom;
                                session.SelectedSettlement = null;
                                // The outlaw's garrison figure was sized against the hall
                                // that just went with it, and no later grant replaces his
                                if (session.SelectedStartType == StartType.Outlaw)
                                    DropTheHall(session);
                                RebuildCurrentTab();
                            });
                    }));
            }

            // The realm's diplomacy is the vassal's and the mercenary's war
            if (startType is StartType.LandedVassal or StartType.LandlessVassal or StartType.Mercenary)
                BuildRealmWarsRow(rows);

            if (startType == StartType.Mercenary)
            {
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_ContractPay}Contract Pay").ToString(), 0,
                    GameCaps.ContractPayCeiling(CreationSession.Current.SelectedKingdom), 5,
                    () => CreationSession.Current.CustomContractPay
                          ?? GameCaps.ContractPayOffer(CreationSession.Current.SelectedKingdom),
                    v => CreationSession.Current.CustomContractPay = v,
                    isCustomized: () => CreationSession.Current.CustomContractPay != null,
                    revert: () => CreationSession.Current.CustomContractPay = null));
            }

            if (startType == StartType.Outlaw)
            {
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_CrimeRating}Crime Rating").ToString(), 0, 100, 5,
                    () => OutlawCrimeRating(CreationSession.Current.CustomCrimeRating),
                    v => SetOutlawCrimeRating(v),
                    isCustomized: () => CreationSession.Current.CustomCrimeRating != null,
                    // Reverting can raise the rating as easily as lower it, so it goes
                    // through the same gate rather than around it
                    revert: () => SetOutlawCrimeRating(null)));
                BuildWantedByRow(rows);
            }

            int markStanding = rows.Count;
            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Editor_ClanTier}Clan Tier").ToString(), 0, GameCaps.MaxClanTier(), 1,
                () => CreationSession.Current.SelectedClanTier,
                v =>
                {
                    CreationSession.Current.SelectedClanTier = v;
                    // The companion row's ceiling depends on the tier
                    RebuildCurrentTab();
                }));

            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Editor_Renown}Renown").ToString(), 0, GameCaps.MaxRenown(), 50,
                () => CreationSession.Current.CustomRenown
                      ?? CSSettings.GetRenownForTier(CreationSession.Current.SelectedClanTier),
                v =>
                {
                    CreationSession.Current.CustomRenown = v;
                    // Renown decides the effective clan tier, which the workshop,
                    // companion and troop ceilings all read
                    RebuildCurrentTab();
                },
                isCustomized: () => CreationSession.Current.CustomRenown != null,
                revert: () =>
                {
                    CreationSession.Current.CustomRenown = null;
                    RebuildCurrentTab();
                }));

            int markHoldings = rows.Count;

            // The water sits ahead of the holding and the starting location because a
            // degree that goes to sea settles one of them
            BuildSeaDegreeRow(rows);

            // Holding: only for start types that own one. The outlawed lord is here too,
            // holding his by still sitting in it rather than by any grant, and only while
            // the price on his head stays under the bar ContextualMenus reads off the
            // live crime model.
            bool outlaw = startType == StartType.Outlaw;
            bool mayHold = !outlaw || OutlawMayHoldAHall(CreationSession.Current.CustomCrimeRating);
            bool usesHolding = outlaw
                               || startType is StartType.Monarch or StartType.LandedVassal
                                   or StartType.RebelClan;

            // A preset can be loaded with a hall and a crime rating that no longer allows
            // one. The screen and the session agree here rather than showing a hall the
            // start would then withhold
            if (outlaw && !mayHold && CreationSession.Current.SelectedSettlement != null)
            {
                CSLogger.Info("StartEditorVM: the crime rating reaches the war threshold, " +
                              "so the outlaw's hall was let go.");
                DropTheHall(CreationSession.Current);
            }

            // "Automatic" means a steward finds you one. An outlaw has no steward and no
            // grant, so his empty value is no walls at all, and where the crime rating is
            // what closed the list the row says that instead of showing a value
            string holdingEmptyText = !outlaw
                ? autoText
                : mayHold
                    ? new TextObject("{=CSR_Editor_OutlawNoHall}No Walls at All").ToString()
                    : new TextObject("{=CSR_Editor_OutlawHunted}No hall while you are hunted").ToString();

            if (usesHolding)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Holding}Holding").ToString(),
                    // The seat on the water is read live rather than off the session, because
                    // a realm picked after the water was taken moves it and the start applies
                    // the moved one
                    () => (SeatOnTheWater() ?? CreationSession.Current.SelectedSettlement)
                          ?.Name?.ToString() ?? holdingEmptyText,
                    () =>
                    {
                        if (SeatOnTheWater() != null)
                        {
                            ShowTheWaterSettlesIt(
                                new TextObject("{=CSR_Editor_Holding}Holding").ToString(),
                                "{=CSR_Editor_Sea_SeatSettled}A seat on the water is the holding you are granted, so no other is offered. Put the water back to dry land and the halls come back.");
                            return;
                        }

                        if (!mayHold)
                        {
                            ShowHuntedHasNoHall();
                            return;
                        }

                        var holdings = ContextualMenus.EligibleHoldings();

                        var options = new List<PickerOption> { new(holdingEmptyText, null) };
                        options.AddRange(holdings
                            .OrderBy(s => s.OwnerClan?.Kingdom?.Name?.ToString() ?? "")
                            .ThenBy(s => s.Name?.ToString() ?? "")
                            .Select(s => new PickerOption(s.Name?.ToString() ?? s.StringId, s,
                                detail: SettlementDetail(s))));
                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Holding}Holding").ToString(), options, picked =>
                            {
                                CreationSession.Current.SelectedSettlement = picked as Settlement;
                                if (outlaw && CreationSession.Current.SelectedSettlement == null)
                                    DropTheHall(CreationSession.Current);
                                RebuildCurrentTab();
                            });
                    }));

                // The other three are granted a hall whether or not one is named here, so a
                // garrison figure always has somewhere to go. The outlaw's only holding is
                // the one he picked, so without it there is nothing to man
                if (!outlaw || CreationSession.Current.SelectedSettlement != null)
                {
                    rows.Add(new StartEditorRowVM(
                        new TextObject("{=CSR_Editor_Garrison}Garrison").ToString(), 0,
                        GameCaps.MaxGarrison(CreationSession.Current.SelectedSettlement,
                            CreationSession.Current.SelectedKingdom), 5,
                        () => CreationSession.Current.CustomGarrison
                              ?? CurrentGarrisonCount(CreationSession.Current.SelectedSettlement),
                        v => CreationSession.Current.CustomGarrison = v,
                        isCustomized: () => CreationSession.Current.CustomGarrison != null,
                        revert: () => CreationSession.Current.CustomGarrison = null));
                }
            }

            if (startType == StartType.RebelClan)
            {
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_RebelAllies}Fellow Rebel Clans").ToString(), 0, 8, 1,
                    () => CreationSession.Current.RebelAllyCount,
                    v => CreationSession.Current.RebelAllyCount = v));
            }

            // Starting location: only when the start does not already fix one
            bool usesLocation = startType is StartType.Commoner or StartType.LandlessVassal
                or StartType.Mercenary or StartType.Outlaw or StartType.CaravanMaster;
            if (usesLocation)
            {
                var fateText = new TextObject("{=CSR_Location_Auto}Let Fate Decide").ToString();
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Location}Starting Location").ToString(),
                    () => (PortOfTheSeaCaravan() ?? CreationSession.Current.SelectedLocation)
                          ?.Name?.ToString() ?? fateText,
                    () =>
                    {
                        if (PortOfTheSeaCaravan() != null)
                        {
                            ShowTheWaterSettlesIt(
                                new TextObject("{=CSR_Editor_Location}Starting Location").ToString(),
                                "{=CSR_Editor_Sea_PortSettled}A sea caravan sails out of a port town, so where you begin is already settled. Put the water back to dry land and the choice comes back.");
                            return;
                        }

                        var options = new List<PickerOption> { new(fateText, null) };
                        options.AddRange(Settlement.All.Where(s => s.IsTown)
                            .OrderBy(s => s.OwnerClan?.Kingdom?.Name?.ToString() ?? "")
                            .ThenBy(s => s.Name?.ToString() ?? "")
                            .Select(s => new PickerOption(s.Name?.ToString() ?? s.StringId, s,
                                detail: SettlementDetail(s))));
                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Location}Starting Location").ToString(), options, picked =>
                            {
                                var session = CreationSession.Current;
                                session.SelectedLocation = picked as Settlement;
                                session.UseRandomLocation = picked == null;
                                RebuildCurrentTab();
                            });
                    }));
            }

            if (startType is StartType.Monarch or StartType.LandedVassal or StartType.Commoner
                or StartType.CaravanMaster)
            {
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_Workshops}Workshops").ToString(), 0,
                    GameCaps.MaxWorkshops(CreationSession.Current.EffectiveClanTier), 1,
                    () => CreationSession.Current.StartingWorkshops,
                    v => CreationSession.Current.StartingWorkshops = v));
            }

            if (startType == StartType.CaravanMaster)
            {
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_PackAnimals}Pack Animals").ToString(), 0, 50, 1,
                    () => CreationSession.Current.CustomPackAnimals
                          ?? GlobalSettings<CSSettings>.Instance?.CaravanPackAnimals ?? 6,
                    v => CreationSession.Current.CustomPackAnimals = v,
                    isCustomized: () => CreationSession.Current.CustomPackAnimals != null,
                    revert: () => CreationSession.Current.CustomPackAnimals = null));
            }

            // Headers slide in from the back so earlier marks stay valid; a
            // section with no rows for this start type gets no header at all.
            // Standing sits above Holdings and Property because the clan tier and
            // renown it holds are what bound the workshop row below.
            InsertSectionHeader(rows, markHoldings, rows.Count,
                "{=CSR_Section_Holdings}Holdings and Property");
            InsertSectionHeader(rows, markStanding, markHoldings,
                "{=CSR_Section_Standing}Standing");
            InsertSectionHeader(rows, markRealm, markStanding,
                "{=CSR_Section_Realm}The Realm");
            InsertSectionHeader(rows, markPath, markRealm,
                "{=CSR_Section_Path}Your Path");
        }

        private static void InsertSectionHeader(MBBindingList<ViewModel> rows, int mark, int nextMark,
            string titleKey)
        {
            if (nextMark > mark)
                rows.Insert(mark, new StartEditorHeaderVM(new TextObject(titleKey).ToString()));
        }

        /// <summary>
        ///     A monarch's realm at birth: how many vassal clans are founded with
        ///     it, whether the granted castles pass to them, which realms start at
        ///     war with the crown, and which policies stand enacted on day one.
        /// </summary>
        /// <summary>
        ///     The heralds' three, the clan's own name, or your words. Picking a
        ///     composed name writes it out rather than leaving the row to resolve
        ///     later, so what the row shows is what the realm is proclaimed as.
        /// </summary>
        private void ShowKingdomNamePicker()
        {
            var options = new List<(string, object?)>();
            foreach (var name in KingdomNameGenerator.Candidates(CreationSession.Current))
                options.Add((name, name));

            options.Add((new TextObject("{=CSR_KingdomName_Clan}Named for Your Clan").ToString(),
                KingdomNameStyle.Clan));
            options.Add((new TextObject("{=CSR_KingdomName_Own}Name It Yourself").ToString(),
                KingdomNameStyle.Custom));

            EditorPopups.ShowOptions(
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(), options, picked =>
                {
                    var session = CreationSession.Current;
                    if (picked is string composed)
                    {
                        session.KingdomName = composed;
                        session.KingdomNameStyle = KingdomNameStyle.Automatic;
                        session.KingdomNameDecided = true;
                        RebuildCurrentTab();
                        return;
                    }

                    if (picked is KingdomNameStyle.Clan)
                    {
                        session.KingdomName = null;
                        session.KingdomNameStyle = KingdomNameStyle.Clan;
                        session.KingdomNameDecided = true;
                        RebuildCurrentTab();
                        return;
                    }

                    PromptForOwnKingdomName();
                });
        }

        private void PromptForOwnKingdomName()
        {
            EditorPopups.ShowPrompt(
                new TextObject("{=CSR_KingdomName_Title}Name Your Kingdom").ToString(),
                new TextObject("{=CSR_KingdomName_Desc}Choose the name your realm will carry into history.")
                    .ToString(),
                null,
                new TextObject("{=CSR_KingdomName_Confirm}Proclaim").ToString(),
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                input =>
                {
                    var cleaned = (input ?? string.Empty).Replace("{", "").Replace("}", "").Trim();
                    var session = CreationSession.Current;
                    session.KingdomName = cleaned.Length == 0 ? null : cleaned;
                    // Clearing the box is a decision too: it asks for a composed name,
                    // not for a prompt after the campaign has begun
                    session.KingdomNameStyle = cleaned.Length == 0
                        ? KingdomNameStyle.Automatic
                        : KingdomNameStyle.Custom;
                    session.KingdomNameDecided = true;
                    RebuildCurrentTab();
                });
        }

        private void BuildKingdomSetupRows(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;

            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Editor_Vassals}Vassal Clans").ToString(), 0, 8, 1,
                () => session.VassalClanCount,
                v => session.VassalClanCount = v));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_GrantedLands}Granted Castles").ToString(),
                () => session.GrantLandsToVassals
                    ? new TextObject("{=CSR_Editor_LandsToVassals}Given to Vassals").ToString()
                    : new TextObject("{=CSR_Editor_LandsKept}Kept by You").ToString(),
                () => EditorPopups.ShowOptions(
                    new TextObject("{=CSR_Editor_GrantedLands}Granted Castles").ToString(),
                    new List<(string, object?)>
                    {
                        (new TextObject("{=CSR_Editor_LandsKept}Kept by You").ToString(), "keep"),
                        (new TextObject("{=CSR_Editor_LandsToVassals}Given to Vassals").ToString(), "grant")
                    }, picked =>
                    {
                        session.GrantLandsToVassals = Equals(picked, "grant");
                        RebuildCurrentTab();
                    })));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_Wars}Starting Wars").ToString(),
                () =>
                {
                    if (session.CustomWars == null)
                        return new TextObject("{=CSR_Editor_Wars_Default}Founding Default").ToString();
                    if (session.CustomWars.Count == 0)
                        return new TextObject("{=CSR_Editor_Wars_None}At Peace").ToString();
                    var value = new TextObject("{=CSR_Editor_Wars_Count}{COUNT} Realms");
                    value.SetTextVariable("COUNT", session.CustomWars.Count);
                    return value.ToString();
                },
                () =>
                {
                    // No "Founding Default" row: selecting nothing already means it, and the row
                    // said so only by being exclusive, which locked every realm behind it. "At
                    // Peace" stays, because an empty selection cannot express it. The tab row
                    // still names whichever state is current.
                    var options = new List<PickerOption>
                    {
                        new(new TextObject("{=CSR_Editor_Wars_None}At Peace").ToString(), "peace",
                            true, session.CustomWars is { Count: 0 })
                    };
                    options.AddRange(Kingdom.All.Where(k => !k.IsEliminated)
                        .OrderBy(k => k.Name?.ToString() ?? "")
                        .Select(k => new PickerOption(k.Name?.ToString() ?? k.StringId, k,
                            false, session.CustomWars?.Contains(k.StringId) == true, detail: RealmDetail(k))));

                    OptionPickerScreen.Open(
                        new TextObject("{=CSR_Editor_Wars}Starting Wars").ToString(), options, false, picked =>
                        {
                            if (picked.Count == 0)
                                session.CustomWars = null;
                            else if (picked.Contains("peace"))
                                session.CustomWars = new List<string>();
                            else
                                session.CustomWars = picked.OfType<Kingdom>()
                                    .Select(k => k.StringId)
                                    .ToList();
                            RebuildCurrentTab();
                        });
                }));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_Policies}Initial Policies").ToString(),
                () =>
                {
                    if (session.SelectedPolicies.Count == 0)
                        return new TextObject("{=CSR_Picker_None}None").ToString();
                    var value = new TextObject("{=CSR_Editor_Policies_Count}{COUNT} Enacted");
                    value.SetTextVariable("COUNT", session.SelectedPolicies.Count);
                    return value.ToString();
                },
                () =>
                {
                    // No "None" row: selecting nothing is None, and the row only locked the list.
                    var options = new List<PickerOption>();
                    options.AddRange(TaleWorlds.ObjectSystem.MBObjectManager.Instance
                        .GetObjectTypeList<PolicyObject>()
                        .OrderBy(p => p.Name?.ToString() ?? "")
                        .Select(p => new PickerOption(p.Name?.ToString() ?? p.StringId, p,
                            false, session.SelectedPolicies.Contains(p.StringId),
                            detail: p.Description?.ToString())));

                    OptionPickerScreen.Open(
                        new TextObject("{=CSR_Editor_Policies}Initial Policies").ToString(), options, true,
                        picked =>
                        {
                            session.SelectedPolicies.Clear();
                            foreach (var payload in picked)
                                if (payload is PolicyObject policy)
                                    session.SelectedPolicies.Add(policy.StringId);
                            RebuildCurrentTab();
                        });
                }));
        }

        /// <summary>
        ///     The joined realm's diplomacy: the exact set of realms it starts
        ///     at war with, or the campaign's own wars, or peace everywhere.
        /// </summary>
        private void BuildRealmWarsRow(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_RealmWars}Realm's Wars").ToString(),
                () =>
                {
                    if (session.RealmWars == null)
                        return new TextObject("{=CSR_Editor_RealmWars_Default}Current Wars").ToString();
                    if (session.RealmWars.Count == 0)
                        return new TextObject("{=CSR_Editor_Wars_None}At Peace").ToString();
                    var value = new TextObject("{=CSR_Editor_Wars_Count}{COUNT} Realms");
                    value.SetTextVariable("COUNT", session.RealmWars.Count);
                    return value.ToString();
                },
                () =>
                {
                    var options = new List<PickerOption>
                    {
                        new(new TextObject("{=CSR_Editor_Wars_None}At Peace").ToString(),
                            "peace", true, session.RealmWars is { Count: 0 })
                    };
                    options.AddRange(Kingdom.All
                        .Where(k => !k.IsEliminated && k != session.SelectedKingdom)
                        .OrderBy(k => k.Name?.ToString() ?? "")
                        .Select(k => new PickerOption(k.Name?.ToString() ?? k.StringId, k,
                            false, session.RealmWars?.Contains(k.StringId) == true, detail: RealmDetail(k))));

                    OptionPickerScreen.Open(
                        new TextObject("{=CSR_Editor_RealmWars}Realm's Wars").ToString(), options, false,
                        picked =>
                        {
                            if (picked.Count == 0)
                                session.RealmWars = null;
                            else if (picked.Contains("peace"))
                                session.RealmWars = new List<string>();
                            else
                                session.RealmWars = picked.OfType<Kingdom>()
                                    .Select(k => k.StringId)
                                    .ToList();
                            RebuildCurrentTab();
                        });
                }));
        }

        /// <summary>The realms whose law wants the outlaw, beyond the one that cast them out.</summary>
        private void BuildWantedByRow(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_WantedBy}Wanted By").ToString(),
                () =>
                {
                    if (session.OutlawWantedBy is not { Count: > 0 })
                        return new TextObject("{=CSR_Editor_WantedBy_Default}Selected Realm Only").ToString();
                    var value = new TextObject("{=CSR_Editor_WantedBy_Count}Selected Realm +{COUNT}");
                    value.SetTextVariable("COUNT", session.OutlawWantedBy.Count);
                    return value.ToString();
                },
                () =>
                {
                    // No "Selected Realm Only" row: selecting nothing is exactly that.
                    var options = new List<PickerOption>();
                    options.AddRange(Kingdom.All
                        .Where(k => !k.IsEliminated && k != session.SelectedKingdom)
                        .OrderBy(k => k.Name?.ToString() ?? "")
                        .Select(k => new PickerOption(k.Name?.ToString() ?? k.StringId, k,
                            false, session.OutlawWantedBy?.Contains(k.StringId) == true, detail: RealmDetail(k))));

                    OptionPickerScreen.Open(
                        new TextObject("{=CSR_Editor_WantedBy}Wanted By").ToString(), options, true,
                        picked =>
                        {
                            session.OutlawWantedBy = picked.Count == 0
                                ? null
                                : picked.OfType<Kingdom>().Select(k => k.StringId).ToList();
                            RebuildCurrentTab();
                        });
                }));
        }

        /// <summary>
        ///     A settlement label the popup's search can filter by name, realm,
        ///     or culture, and that reads grouped when sorted by realm.
        /// </summary>
        /// <summary>Who rules a realm and how much it holds, under its name in a list of realms.</summary>
        private static string RealmDetail(Kingdom kingdom)
        {
            var detail = kingdom.Leader?.Name != null
                ? new TextObject("{=CSR_Picker_RealmDetail}Ruled by {RULER}; {HOUSES} houses and {FIEFS} fiefs")
                : new TextObject("{=CSR_Picker_RealmDetailNoRuler}{HOUSES} houses and {FIEFS} fiefs");
            if (kingdom.Leader?.Name != null)
                detail.SetTextVariable("RULER", kingdom.Leader.Name);
            detail.SetTextVariable("HOUSES", kingdom.Clans.Count);
            detail.SetTextVariable("FIEFS", kingdom.Fiefs.Count);
            return detail.ToString();
        }

        /// <summary>What a settlement is, whose it is and its culture, under its name in a list of places.</summary>
        private static string SettlementDetail(Settlement settlement)
        {
            var kind = settlement.IsTown
                ? new TextObject("{=CSR_Picker_Town}Town")
                : settlement.IsCastle
                    ? new TextObject("{=CSR_Picker_Castle}Castle")
                    : new TextObject("{=CSR_Picker_Village}Village");
            string? kingdom = settlement.OwnerClan?.Kingdom?.Name?.ToString();
            string? culture = settlement.Culture?.Name?.ToString();

            TextObject detail;
            if (kingdom != null)
            {
                detail = new TextObject("{=CSR_Picker_SettlementOfRealm}{KIND} of {REALM}");
                detail.SetTextVariable("REALM", kingdom);
            }
            else
            {
                detail = new TextObject("{=CSR_Picker_SettlementUnclaimed}{KIND} held by no realm");
            }

            detail.SetTextVariable("KIND", kind);
            return culture == null ? detail.ToString() : $"{detail}, {culture}";
        }

        private static int CurrentGarrisonCount(Settlement? holding)
        {
            var garrison = holding?.Parties?.FirstOrDefault(p => p.IsGarrison);
            return garrison?.MemberRoster?.TotalManCount ?? 0;
        }

        /// <summary>
        ///     How far this start goes onto the water. The guided route asks it as a
        ///     chapter of its own, so the editor asks it as a row of its own: a start
        ///     type carries exactly one degree, which makes the question whether this
        ///     start takes it rather than which one it takes.
        /// </summary>
        private void BuildSeaDegreeRow(MBBindingList<ViewModel> rows)
        {
            var degree = DegreeOnOffer();
            if (degree == SeaDegree.None) return;

            string title = new TextObject("{=CSR_Fleet_Title}The Water").ToString();
            string ashore = new TextObject("{=CSR_Fleet_Ashore}Stay on Dry Land").ToString();
            string afloat = new TextObject(SeaDegreeTitleKey(degree)).ToString();

            rows.Add(new StartEditorPickerRowVM(
                title,
                () => CreationSession.Current.SelectedSeaDegree == degree ? afloat : ashore,
                () => EditorPopups.ShowOptions(
                    title,
                    new List<(string, object?)> { (ashore, SeaDegree.None), (afloat, degree) },
                    picked =>
                    {
                        if (picked is SeaDegree chosen)
                            SeaGrants.Choose(CreationSession.Current, chosen);
                        // The holding and the starting location are decided by this answer
                        RebuildCurrentTab();
                    }),
                hint: new TextObject(SeaDegreeHintKey(degree)).ToString()));
        }

        /// <summary>
        ///     The degree this start could really be granted: a start type that carries
        ///     one, a campaign with hulls in it, and, where the degree seats the start
        ///     somewhere, a port to seat it at. Without War Sails no hull is registered
        ///     and no settlement reports a port, so this is None and the row is not
        ///     there at all rather than there and disabled.
        ///
        ///     The life gate <see cref="SeaGrants.Offered"/> puts in front of the guided
        ///     route is deliberately not asked. It reads the scenes a character walked,
        ///     and a start composed here walked none, so asking it would close the water
        ///     to every start this screen can build.
        /// </summary>
        private static SeaDegree DegreeOnOffer()
        {
            try
            {
                var session = CreationSession.Current;
                var degree = SeaDegrees.For(session.SelectedStartType);
                if (degree == SeaDegree.None || !SeaGrants.ContentPresent()) return SeaDegree.None;
                if (SeaGrants.Hulls(session, degree).Count == 0) return SeaDegree.None;
                if (degree != SeaDegree.Raider && SeaGrants.Seat(session, degree) == null)
                    return SeaDegree.None;

                return degree;
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartEditorVM: reading whether this start can go to sea failed.", ex);
                return SeaDegree.None;
            }
        }

        /// <summary>The port castle a seat on the water grants, or null while the start stays ashore.</summary>
        private static Settlement? SeatOnTheWater() =>
            CreationSession.Current.SelectedSeaDegree == SeaDegree.Admiral
                ? SeaGrants.Seat(CreationSession.Current, SeaDegree.Admiral)
                : null;

        /// <summary>The port town a sea caravan sails from, or null while the start stays ashore.</summary>
        private static Settlement? PortOfTheSeaCaravan() =>
            CreationSession.Current.SelectedSeaDegree == SeaDegree.Venturer
                ? SeaGrants.Seat(CreationSession.Current, SeaDegree.Venturer)
                : null;

        /// <summary>
        ///     Why a row the water decided is not open to a second answer, said where
        ///     the player clicked and naming the one lever that reopens it. The start
        ///     re-states this holding when it applies, so a pick made here would be
        ///     overwritten without a word.
        /// </summary>
        private static void ShowTheWaterSettlesIt(string title, string bodyKey)
        {
            EditorPopups.ShowConfirm(
                title,
                new TextObject(bodyKey).ToString(),
                new TextObject("{=CSR_Editor_VeryWell}Very Well").ToString(),
                null,
                () => { });
        }

        /// <summary>
        ///     The fallbacks are FleetMenu's own, so the two routes name the water
        ///     alike. Only the three degrees reach here, and the last of them is the
        ///     default arm rather than a fourth line nothing would ever read.
        /// </summary>
        private static string SeaDegreeTitleKey(SeaDegree degree) => degree switch
        {
            SeaDegree.Raider => "{=CSR_Fleet_Raider}Take to the Water",
            SeaDegree.Admiral => "{=CSR_Fleet_Admiral}A Seat on the Water",
            _ => "{=CSR_Fleet_Venturer}The Sea Caravan"
        };

        private static string SeaDegreeHintKey(SeaDegree degree) => degree switch
        {
            SeaDegree.Raider =>
                "{=CSR_Editor_Sea_Raider_Hint}Shallow hulls and the crew to sail them, and a story that begins afloat off the nearest coast.",
            SeaDegree.Admiral =>
                "{=CSR_Editor_Sea_Admiral_Hint}A castle with a port of its own, the ships at its quay and marines to sail them. It is the holding you are granted.",
            _ =>
                "{=CSR_Editor_Sea_Venturer_Hint}The ships a sea caravan of your people sails with, out of the port town your story then begins at."
        };

        /// <summary>
        ///     The crime rating a start carrying <paramref name="custom"/> would run with,
        ///     read the way <see cref="Services.Application.Scenarios.OutlawScenario"/> reads
        ///     it: the row's own answer, else the configured default.
        /// </summary>
        private static int OutlawCrimeRating(int? custom) =>
            Math.Max(0, custom ?? GlobalSettings<CSSettings>.Instance?.OutlawCrimeRating ?? 50);

        /// <summary>
        ///     Whether an outlaw carrying <paramref name="custom"/> may hold a hall. The rule
        ///     itself lives on <see cref="ContextualMenus.OutlawMayHoldAHall(int)"/> and is
        ///     asked rather than restated, so the editor, the guided route's holding screen
        ///     and the grant cannot come apart.
        /// </summary>
        private static bool OutlawMayHoldAHall(int? custom) =>
            ContextualMenus.OutlawMayHoldAHall(OutlawCrimeRating(custom));

        /// <summary>
        ///     Lets go of the hall and the garrison figure together: the figure was sized
        ///     against a specific castle, and an outlaw holding nothing has no walls to man,
        ///     so leaving it set would ask the start for a garrison it cannot raise anywhere.
        /// </summary>
        private static void DropTheHall(CharacterCreationSession session)
        {
            session.SelectedSettlement = null;
            session.CustomGarrison = null;
        }

        /// <summary>
        ///     Sets the outlaw's crime rating, and where the new rating is what costs him his
        ///     hall, says so and lets him decide. The editor's contract is that what the
        ///     player sets is what he gets, so neither value is overruled behind his back:
        ///     this is the same inquiry the companion count raises before a lower count
        ///     discards customized companions.
        /// </summary>
        private void SetOutlawCrimeRating(int? rating)
        {
            try
            {
                var session = CreationSession.Current;
                var hall = session.SelectedSettlement;

                if (hall == null || OutlawMayHoldAHall(rating))
                {
                    session.CustomCrimeRating = rating;
                    RebuildCurrentTab();
                    return;
                }

                var question = new TextObject(
                    "{=CSR_Editor_OutlawHallLost_Confirm}Wanted this badly, you would have a crown marching on any hall you sat in, so {HALL} is given up. Continue?");
                question.SetTextVariable("HALL", hall.Name);
                EditorPopups.ShowConfirm(
                    new TextObject("{=CSR_Editor_CrimeRating}Crime Rating").ToString(),
                    question.ToString(),
                    new TextObject("{=CSR_Editor_OutlawHallLost_Yes}Give Up the Hall").ToString(),
                    new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                    () =>
                    {
                        session.CustomCrimeRating = rating;
                        DropTheHall(session);
                        CSLogger.Info($"StartEditorVM: a crime rating of {OutlawCrimeRating(rating)} " +
                                      $"gives up {hall.Name}.");
                        RebuildCurrentTab();
                    },
                    // Nothing was written, so the tab is rebuilt to read the session back
                    RebuildCurrentTab,
                    destructive: true);
            }
            catch (Exception ex)
            {
                CSLogger.Error("StartEditorVM: setting the outlaw's crime rating failed.", ex);
            }
        }

        /// <summary>
        ///     Why the hall list is closed, said in the row the player clicked rather than
        ///     left to be found when the campaign opens, and naming the one lever that
        ///     reopens it. The guided route says the same thing on its own holding screen.
        /// </summary>
        private static void ShowHuntedHasNoHall()
        {
            EditorPopups.ShowConfirm(
                new TextObject("{=CSR_Editor_Holding}Holding").ToString(),
                new TextObject(
                        "{=CSR_Editor_OutlawHunted_Desc}You are wanted badly enough that a crown would march on any hall you sat in, so none is offered. Lower the crime rating and the halls come back.")
                    .ToString(),
                new TextObject("{=CSR_Editor_OutlawHunted_Ok}Very Well").ToString(),
                null,
                () => { });
        }

        #endregion

        #region Hero editing

        /// <summary>
        ///     The count first, then one row per companion slot, each opening that
        ///     companion's own sheet. The count lives here, after every stat and
        ///     perk tab, so its ceiling reflects the whole composed character.
        /// </summary>
        private void BuildCompanionRows(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;

            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Editor_Companions}Companions").ToString(), 0,
                GameCaps.MaxCompanions(session.EffectiveClanTier, session), 1,
                () => session.StartingCompanions,
                v =>
                {
                    int customizedLost = CustomizedSpecsLost(session, v);
                    if (customizedLost > 0)
                    {
                        var question = new TextObject(
                            "{=CSR_Editor_CompanionTrim_Confirm}Lowering the count discards {COUNT} customized companions. Continue?");
                        question.SetTextVariable("COUNT", customizedLost);
                        EditorPopups.ShowConfirm(
                            new TextObject("{=CSR_Editor_Companions}Companions").ToString(),
                            question.ToString(),
                            new TextObject("{=CSR_Editor_CompanionTrim_Yes}Discard Them").ToString(),
                            new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                            () =>
                            {
                                session.StartingCompanions = v;
                                SyncCompanionSpecs(session);
                                RebuildCurrentTab();
                            },
                            RebuildCurrentTab,
                            destructive: true);
                        return;
                    }

                    session.StartingCompanions = v;
                    SyncCompanionSpecs(session);
                    // The slot rows below and the troop ceiling follow the count
                    RebuildCurrentTab();
                }));

            // The conversion's own named people stand in the world already, so they are asked for by
            // name rather than counted: they join the clan beside the companions raised above
            var named = Services.TaomBridge.NamedCompanionIds();
            if (named.Count > 0)
            {
                var namedLabel = new TextObject("{=CSR_Editor_NamedCompanions}Named Companions").ToString();
                rows.Add(new StartEditorPickerRowVM(
                    namedLabel,
                    () =>
                    {
                        if (session.NamedCompanionIds.Count == 0)
                            return new TextObject("{=CSR_Picker_None}None").ToString();

                        var count = new TextObject("{=CSR_Editor_NamedCompanionsCount}{COUNT} chosen");
                        count.SetTextVariable("COUNT", session.NamedCompanionIds.Count);
                        return count.ToString();
                    },
                    () =>
                    {
                        var options = named
                            .Select(id => Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id))
                            .Where(h => h != null && h!.Clan != Clan.PlayerClan)
                            .OrderBy(h => h!.Name?.ToString() ?? h!.StringId)
                            .Select(h => (h!.Name?.ToString() ?? h.StringId, (object?)h.StringId))
                            .ToList();
                        if (options.Count == 0) return;

                        EditorPopups.ShowMultiOptions(namedLabel,
                            new TextObject(
                                    "{=CSR_Editor_NamedCompanionsDesc}People of this world who ride with you from the first day.")
                                .ToString(),
                            options, picked =>
                            {
                                session.NamedCompanionIds.Clear();
                                foreach (var id in picked.OfType<string>())
                                    session.NamedCompanionIds.Add(id);
                                RebuildCurrentTab();
                            });
                    },
                    new TextObject("{=CSR_Hint_NamedCompanions}Lore characters the conversion places in the world, taken into your clan.").ToString(),
                    isCustomized: () => session.NamedCompanionIds.Count > 0,
                    revert: () =>
                    {
                        session.NamedCompanionIds.Clear();
                        RebuildCurrentTab();
                    }));
            }

            SyncCompanionSpecs(session);

            for (int index = 0; index < session.CompanionSpecs.Count; index++)
            {
                var spec = session.CompanionSpecs[index];
                var label = new TextObject("{=CSR_Editor_CompanionN}Companion {NUMBER}");
                label.SetTextVariable("NUMBER", index + 1);
                string labelText = label.ToString();

                rows.Add(new StartEditorPickerRowVM(
                    labelText,
                    () => HeroSpecSummary(spec),
                    () => EnterHeroEdit(spec, labelText)));
            }
        }

        /// <summary>Uncustomized slots leave first; customized sheets go last.</summary>
        private static void SyncCompanionSpecs(CharacterCreationSession session)
        {
            var specs = session.CompanionSpecs;
            while (specs.Count < session.StartingCompanions)
                specs.Add(new HeroSpec());
            while (specs.Count > session.StartingCompanions)
            {
                int victim = specs.FindLastIndex(s => !s.HasCustomization);
                specs.RemoveAt(victim >= 0 ? victim : specs.Count - 1);
            }
        }

        private static int CustomizedSpecsLost(CharacterCreationSession session, int target)
        {
            int excess = session.CompanionSpecs.Count - target;
            if (excess <= 0) return 0;
            int uncustomized = session.CompanionSpecs.Count(s => !s.HasCustomization);
            return Math.Max(0, excess - uncustomized);
        }

        private static string HeroSpecSummary(HeroSpec spec)
        {
            if (!spec.HasCustomization)
                return new TextObject("{=CSR_Editor_HeroRandom}Fully Generated").ToString();

            var parts = new List<string>();
            if (spec.Name != null)
                parts.Add(spec.Name);
            if (spec.Role != null)
                parts.Add(EditorPopups.SpacedName(spec.Role));
            if (spec.Level.HasValue)
            {
                var level = new TextObject("{=CSR_Editor_HeroLevel}Level {LEVEL}");
                level.SetTextVariable("LEVEL", spec.Level.Value);
                parts.Add(level.ToString());
            }

            if (spec.SkillLevels.Count > 0 || spec.Gear.Count > 0 || spec.Attributes.Count > 0 ||
                spec.Traits.Count > 0 || spec.Focus.Count > 0 || spec.Perks.Count > 0)
                parts.Add(new TextObject("{=CSR_Editor_HeroCustomized}Customized").ToString());
            return parts.Count > 0
                ? string.Join(", ", parts)
                : new TextObject("{=CSR_Editor_HeroCustomized}Customized").ToString();
        }

        /// <summary>
        ///     One hero's full-control sheet: role, level, exact skills, attributes
        ///     and traits, and exact battle gear per slot, all layered over the
        ///     intelligent generator.
        /// </summary>
        private void BuildHeroEditRows(MBBindingList<ViewModel> rows, HeroSpec spec, string title)
        {
            var session = CreationSession.Current;
            var autoText = new TextObject("{=CSR_Editor_Auto}Automatic").ToString();

            string SetCount(int count)
            {
                if (count == 0) return autoText;
                var value = new TextObject("{=CSR_Editor_HeroSkillsSet}{COUNT} set");
                value.SetTextVariable("COUNT", count);
                return value.ToString();
            }

            rows.Add(BackRow(title, ExitHeroEdit));

            // A relative's relation already fixes who they are born as, so only a companion is
            // given a gender and a culture; everyone can be given a name
            bool isCompanion = _memberEditing == null;
            var nameLabel = new TextObject("{=CSR_Editor_HeroName}Name").ToString();
            rows.Add(new StartEditorPickerRowVM(
                nameLabel,
                () => spec.Name ?? autoText,
                () => ShowHeroNamePicker(spec, nameLabel),
                new TextObject("{=CSR_Hint_HeroName}Choose a name drawn from this character's culture, or type one; automatic gives them a generated one.")
                    .ToString(),
                () => spec.Name != null,
                () =>
                {
                    spec.Name = null;
                    RebuildCurrentTab();
                }));

            if (isCompanion)
            {
                var genderLabel = new TextObject("{=CSR_Editor_Gender}Gender").ToString();
                string GenderText(bool? female) => female == null
                    ? autoText
                    : female.Value
                        ? new TextObject("{=CSR_Editor_Female}Female").ToString()
                        : new TextObject("{=CSR_Editor_Male}Male").ToString();
                rows.Add(new StartEditorPickerRowVM(
                    genderLabel,
                    () => GenderText(spec.IsFemale),
                    () => EditorPopups.ShowOptions(genderLabel, new List<PickerOption>
                    {
                        new(autoText, "auto", startsSelected: spec.IsFemale == null),
                        new(GenderText(true), true, startsSelected: spec.IsFemale == true),
                        new(GenderText(false), false, startsSelected: spec.IsFemale == false)
                    }, picked =>
                    {
                        spec.IsFemale = picked as bool?;
                        RebuildCurrentTab();
                    })));

                var cultureLabel = new TextObject("{=CSR_Editor_Culture}Culture").ToString();
                var cultures = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<CultureObject>()
                    .Where(c => c.IsMainCulture)
                    .OrderBy(c => c.Name?.ToString() ?? c.StringId)
                    .ToList();
                rows.Add(new StartEditorPickerRowVM(
                    cultureLabel,
                    () => cultures.FirstOrDefault(c => c.StringId == spec.CultureId)?.Name?.ToString() ?? autoText,
                    () =>
                    {
                        var options = new List<PickerOption>
                        {
                            new(autoText, null, startsSelected: spec.CultureId == null,
                                detail: new TextObject("{=CSR_Editor_CultureAuto_Detail}Your own culture").ToString())
                        };
                        options.AddRange(cultures.Select(c => new PickerOption(c.Name?.ToString() ?? c.StringId,
                            c.StringId, startsSelected: c.StringId == spec.CultureId)));
                        EditorPopups.ShowOptions(cultureLabel, options, picked =>
                        {
                            spec.CultureId = picked as string;
                            RebuildCurrentTab();
                        });
                    }));
            }

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_HeroRole}Role").ToString(),
                () => spec.Role != null ? EditorPopups.SpacedName(spec.Role) : autoText,
                () =>
                {
                    var options = new List<(string, object?)> { (autoText, null) };
                    options.AddRange(Enum.GetValues(typeof(HeroOutfitter.Role))
                        .Cast<HeroOutfitter.Role>()
                        .Select(r => (EditorPopups.SpacedName(r.ToString()), (object?)r)));
                    EditorPopups.ShowOptions(
                        new TextObject("{=CSR_Editor_HeroRole}Role").ToString(), options, picked =>
                        {
                            spec.Role = picked is HeroOutfitter.Role role ? role.ToString() : null;
                            RebuildCurrentTab();
                        });
                }));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Resource_Level}Level").ToString(),
                () => spec.Level?.ToString() ?? autoText,
                () => EditorPopups.ShowOptions(
                    new TextObject("{=CSR_Resource_Level}Level").ToString(),
                    new List<(string, object?)>
                    {
                        (autoText, "auto"),
                        (new TextObject("{=CSR_Editor_HeroSetLevel}Set Level").ToString(), "set")
                    }, picked =>
                    {
                        if (Equals(picked, "auto"))
                        {
                            spec.Level = null;
                            RebuildCurrentTab();
                            return;
                        }

                        EditorPopups.ShowNumber(
                            new TextObject("{=CSR_Resource_Level}Level").ToString(), 1,
                            GameCaps.MaxHeroLevel(), level =>
                            {
                                spec.Level = level;
                                RebuildCurrentTab();
                            });
                    })));

            // The player's own tabs, in the player's own order
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabAttributes}Attributes").ToString(),
                () => SetCount(spec.Attributes.Count),
                () => EnterHeroPage("attributes"),
                new TextObject(
                        "{=CSR_Hint_HeroAttributes}Exact attribute points for this character; any left automatic stay as generated.")
                    .ToString()));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabSkills}Skill Focus").ToString(),
                () => SetCount(spec.Focus.Count),
                () => EnterHeroPage("focus"),
                new TextObject(
                        "{=CSR_Hint_HeroFocus}Exact focus points per skill for this character; any left automatic stay as generated.")
                    .ToString()));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabSkillLevels}Skill Levels").ToString(),
                () => SetCount(spec.SkillLevels.Count),
                () => EnterHeroPage("levels"),
                new TextObject(
                        "{=CSR_Hint_HeroSkillLevels}Exact skill levels for this character; any left automatic stay as generated.")
                    .ToString()));

            // A generated character's level in a skill is unknown until they exist, so the board
            // offers only the skills given an exact level, and until one reaches a perk the row
            // leads to the Skill Levels page instead
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabPerks}Skill Perks").ToString(),
                () => HeroPerkEntries(spec).Count == 0
                    ? new TextObject("{=CSR_Editor_HeroPerksNeedLevels}Set Skill Levels First").ToString()
                    : SetCount(spec.Perks.Count),
                () => OpenHeroPerkBoard(spec),
                new TextObject(
                        "{=CSR_Hint_HeroPerks}This character's perks in every skill given an exact level; skills left automatic fill as generated.")
                    .ToString()));

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabTraits}Traits").ToString(),
                () => SetCount(spec.Traits.Count),
                () => EnterHeroPage("traits"),
                new TextObject(
                        "{=CSR_Hint_HeroTraits}Exact personality trait levels for this character; any left automatic keep the personality they were generated with.")
                    .ToString()));

            var gearSlots = new (EquipmentIndex Slot, string LabelKey)[]
            {
                (EquipmentIndex.Head, "{=CSR_SlotName_Head}Head Armor"),
                (EquipmentIndex.Body, "{=CSR_SlotName_Body}Body Armor"),
                (EquipmentIndex.Leg, "{=CSR_SlotName_Leg}Leg Armor"),
                (EquipmentIndex.Gloves, "{=CSR_SlotName_Gloves}Gloves"),
                (EquipmentIndex.Cape, "{=CSR_SlotName_Cape}Cape"),
                (EquipmentIndex.Horse, "{=CSR_SlotName_Horse}Horse"),
                (EquipmentIndex.HorseHarness, "{=CSR_SlotName_Harness}Horse Harness")
            };
            foreach (var (slot, labelKey) in gearSlots)
                AddHeroGearRow(rows, spec, slot, new TextObject(labelKey).ToString(), false);

            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
            {
                var weaponLabel = new TextObject("{=CSR_Gear_SlotTitle}Weapon Slot {SLOT}");
                weaponLabel.SetTextVariable("SLOT", slotIndex + 1);
                AddHeroGearRow(rows, spec, EquipmentIndex.Weapon0 + slotIndex, weaponLabel.ToString(), true);
            }

            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_HeroReset}Reset This Character").ToString(),
                () => new TextObject("{=CSR_Editor_HeroReset_Value}Back to Fully Generated").ToString(),
                () => EditorPopups.ShowConfirm(
                    title,
                    new TextObject(
                            "{=CSR_Editor_HeroReset_Confirm}Return this character to fully generated? Every setting made for them goes.")
                        .ToString(),
                    new TextObject("{=CSR_Editor_Reset_Yes}Reset").ToString(),
                    new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                    () =>
                    {
                        spec.Clear();
                        RebuildCurrentTab();
                    },
                    destructive: true),
                isDestructive: true));
        }

        // Drawn once per character, culture and sex and then kept, so the list does not reshuffle between looks
        private readonly Dictionary<(HeroSpec Spec, string Culture, bool Female), List<string>> _heroNames = new();

        /// <summary>
        ///     Names from the character's own culture, or the player's while that is automatic, for the
        ///     sex the character is born as: a relative's relation fixes it, a companion's Gender row
        ///     sets it, and while that row is automatic both pools are offered and the name picked
        ///     settles the gender with it.
        /// </summary>
        private void ShowHeroNamePicker(HeroSpec spec, string title)
        {
            var session = CreationSession.Current;
            var autoText = new TextObject("{=CSR_Editor_Auto}Automatic").ToString();
            var typeText = new TextObject("{=CSR_Editor_TypeAName}Type a Name").ToString();
            var culture = (spec.CultureId != null
                              ? TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CultureObject>(spec.CultureId)
                              : null)
                          ?? session.SelectedCulture;
            bool isCompanion = _memberEditing == null;
            bool? female = isCompanion ? spec.IsFemale : RelativeIsFemale(_memberEditing!.Relation);

            var options = new List<PickerOption> { new(autoText, "cs_auto", startsSelected: spec.Name == null) };
            if (culture != null)
            {
                foreach (bool pool in female.HasValue ? new[] { female.Value } : new[] { false, true })
                {
                    var key = (spec, culture.StringId, pool);
                    if (!_heroNames.TryGetValue(key, out var names))
                        _heroNames[key] = names = HeroNameGenerator.ComposeFirstNames(culture, pool);

                    string? detail = female.HasValue
                        ? null
                        : pool
                            ? new TextObject("{=CSR_Editor_Female}Female").ToString()
                            : new TextObject("{=CSR_Editor_Male}Male").ToString();
                    foreach (var name in names)
                        options.Add(new PickerOption(name, (name, pool), startsSelected: name == spec.Name,
                            detail: detail));
                }
            }

            options.Add(new PickerOption(typeText, "cs_type"));

            EditorPopups.ShowOptions(title, options, picked =>
            {
                if (Equals(picked, "cs_type"))
                {
                    EditorPopups.ShowText(title,
                        new TextObject("{=CSR_Editor_HeroName_Desc}What this character is called.").ToString(),
                        spec.Name, typed =>
                        {
                            spec.Name = typed;
                            RebuildCurrentTab();
                        });
                    return;
                }

                if (picked is ValueTuple<string, bool> chosen)
                {
                    spec.Name = chosen.Item1;
                    if (isCompanion && spec.IsFemale == null)
                        spec.IsFemale = chosen.Item2;
                }
                else
                {
                    spec.Name = null;
                }

                RebuildCurrentTab();
            });
        }

        private static bool RelativeIsFemale(FamilyRelation relation) => relation switch
        {
            FamilyRelation.Mother or FamilyRelation.Sister or FamilyRelation.Daughter => true,
            FamilyRelation.Spouse => !(Hero.MainHero?.IsFemale ?? false),
            _ => false
        };

        /// <summary>
        ///     A page's first line: its title, drawn larger, beside the button that leaves it. The
        ///     button reads what it does rather than where it goes.
        /// </summary>
        private static StartEditorPickerRowVM BackRow(string title, Action back) =>
            new(title,
                () => new TextObject("{=CSR_Editor_HeroBack}Back").ToString(),
                back,
                new TextObject("{=CSR_Hint_Back}Return to the list this page was opened from.").ToString(),
                isTitle: true);

        /// <summary>
        ///     One page of a hero's sheet, drawn by the rows the player's own tab
        ///     draws. A value left unset reads Automatic rather than a number,
        ///     because what the hero's template carries is not known until the hero
        ///     exists, and a figure shown here would be a guess.
        /// </summary>
        private void BuildHeroPageRows(MBBindingList<ViewModel> rows, HeroSpec spec, string title, string page)
        {
            rows.Add(BackRow(title, ExitHeroPage));

            var autoText = new TextObject("{=CSR_Editor_Auto}Automatic").ToString();
            if (page == "attributes")
                AddAttributeRows(rows, spec.Attributes, StartingAttribute, autoText, () => { });
            else if (page == "focus")
                AddHeroSkillRows(rows, spec.Focus, GameCaps.MaxFocus(), 1,
                    "{=CSR_Editor_Bulk_AllFocus}Set All Skill Focus", true, autoText);
            else if (page == "levels")
                AddHeroSkillRows(rows, spec.SkillLevels, GameCaps.MaxSkillLevel(), 5,
                    "{=CSR_Editor_Bulk_AllLevels}Set All Skill Levels", false, autoText);
            else
                AddTraitRows(rows, spec.Traits, autoText);
        }

        /// <summary>
        ///     A character's focus or skill levels on the rows the player's own tab draws: one line
        ///     per skill under its attribute, a click on the value to type it exactly, and a single
        ///     line to set them all at once. Focus is drawn as marks and levels as a bar.
        /// </summary>
        private void AddHeroSkillRows(MBBindingList<ViewModel> rows, Dictionary<string, int> values, int max,
            int step, string allLabelKey, bool pips, string autoText)
        {
            var allLabel = new TextObject(allLabelKey).ToString();
            rows.Add(new StartEditorPickerRowVM(allLabel,
                () => new TextObject("{=CSR_Editor_Bulk_AllValue}Every Skill at Once").ToString(),
                () => EditorPopups.ShowNumber(allLabel, 0, max, value =>
                {
                    foreach (var skill in Skills.All)
                        values[skill.StringId] = value;
                    RebuildCurrentTab();
                })));

            AddSkillsByAttribute(rows, captured =>
                rows.Add(new StartEditorRowVM(
                    captured.Name.ToString(), 0, max, step,
                    () => values.TryGetValue(captured.StringId, out int v) ? v : 0,
                    v => values[captured.StringId] = v,
                    format: UnsetReads(values, captured.StringId, autoText),
                    isCustomized: () => values.ContainsKey(captured.StringId),
                    revert: () => values.Remove(captured.StringId),
                    pips: pips,
                    bar: !pips)));
        }

        private void AddHeroGearRow(MBBindingList<ViewModel> rows, HeroSpec spec, EquipmentIndex slot,
            string label, bool isWeapon)
        {
            rows.Add(new StartEditorPickerRowVM(
                label,
                () => spec.Gear.TryGetValue(slot, out var item)
                    ? item?.Name?.ToString() ?? new TextObject("{=CSR_Picker_None}None").ToString()
                    : new TextObject("{=CSR_Editor_Auto}Automatic").ToString(),
                () =>
                {
                    var session = CreationSession.Current;
                    int tier = EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance);
                    var items = isWeapon
                        ? GearQuery.AllWeapons(session.SelectedCulture, tier, session)
                        : ArmorQuery.QualifyingItems(slot, session.SelectedCulture, tier, session);
                    spec.Gear.TryGetValue(slot, out var currentPick);
                    ItemPickerScreen.Open(label, items, (choice, item) =>
                    {
                        switch (choice)
                        {
                            case ItemPickChoice.Auto:
                                spec.Gear.Remove(slot);
                                break;
                            case ItemPickChoice.None:
                                spec.Gear[slot] = null;
                                break;
                            default:
                                spec.Gear[slot] = item;
                                break;
                        }

                        RebuildCurrentTab();
                    }, currentItem: currentPick);
                }));
        }

        #endregion

        #region Stats

        private void AddBulkStatRows(MBBindingList<ViewModel> rows, string allLabelKey, int min, int max,
            Action<SkillObject, int> apply)
        {
            string allLabel = new TextObject(allLabelKey).ToString();

            void ApplyToAll(int value)
            {
                foreach (var skill in Skills.All)
                    apply(skill, value);
                StageViewBridge.RefreshGainedProperties();
                RebuildCurrentTab();
            }

            rows.Add(new StartEditorPickerRowVM(allLabel,
                () => new TextObject("{=CSR_Editor_Bulk_AllValue}Every Skill at Once").ToString(),
                () => EditorPopups.ShowNumber(allLabel, min, max, ApplyToAll)));

        }

        private void BuildAttributeRows(MBBindingList<ViewModel> rows) =>
            AddAttributeRows(rows, CreationSession.Current.CustomAttributes, EstimateAttribute, null,
                StageViewBridge.RefreshGainedProperties);

        /// <summary>
        ///     The attribute rows over one sheet's exact values, the player's own or
        ///     a generated hero's. <paramref name="unsetText"/> replaces the figure
        ///     while a value is unset; null shows <paramref name="unsetValue"/>,
        ///     which is also where the steppers start from.
        /// </summary>
        private void AddAttributeRows(MBBindingList<ViewModel> rows, Dictionary<string, int> values,
            Func<CharacterAttribute, int> unsetValue, string? unsetText, Action onChange)
        {
            int max = GameCaps.MaxAttribute();

            var allLabel = new TextObject("{=CSR_Editor_Bulk_AllAttributes}Set All Attributes").ToString();
            rows.Add(new StartEditorPickerRowVM(allLabel,
                () => new TextObject("{=CSR_Editor_Bulk_AllAttributesValue}Every Attribute at Once").ToString(),
                () => EditorPopups.ShowNumber(allLabel, 0, max, value =>
                {
                    foreach (var attribute in Attributes.All)
                        values[attribute.StringId] = value;
                    onChange();
                    RebuildCurrentTab();
                })));

            foreach (var attribute in Attributes.All)
            {
                var captured = attribute;
                rows.Add(new StartEditorRowVM(
                    captured.Name.ToString(), 0, max, 1,
                    () => values.TryGetValue(captured.StringId, out int v) ? v : unsetValue(captured),
                    v =>
                    {
                        values[captured.StringId] = v;
                        onChange();
                    },
                    format: UnsetReads(values, captured.StringId, unsetText),
                    isCustomized: () => values.ContainsKey(captured.StringId),
                    revert: () =>
                    {
                        values.Remove(captured.StringId);
                        onChange();
                    },
                    pips: true));
            }
        }

        /// <summary>
        ///     One header per attribute with the skills it governs beneath, the way the
        ///     game's character sheet groups them, and any skill no attribute claims last.
        /// </summary>
        private static void AddSkillsByAttribute(MBBindingList<ViewModel> rows, Action<SkillObject> addRow)
        {
            var placed = new HashSet<SkillObject>();
            foreach (var attribute in Attributes.All)
            {
                // As the game's own skill panel does, War Sails' skills stand under no attribute
                var governed = Skills.All
                    .Where(s => !NavalDLCService.IsNavalSkill(s) &&
                                VersionedGameApi.AttributesOf(s).FirstOrDefault() == attribute)
                    .ToList();
                if (governed.Count == 0) continue;

                rows.Add(new StartEditorHeaderVM(attribute.Name.ToString()));
                foreach (var skill in governed)
                {
                    addRow(skill);
                    placed.Add(skill);
                }
            }

            var rest = Skills.All.Where(s => !placed.Contains(s)).ToList();
            if (rest.Count == 0) return;

            rows.Add(new StartEditorHeaderVM(new TextObject("{=CSR_Editor_OtherSkills}Other Skills").ToString()));
            foreach (var skill in rest)
                addRow(skill);
        }

        private static Func<int, string>? UnsetReads(Dictionary<string, int> values, string id, string? unsetText) =>
            unsetText == null ? null : v => values.ContainsKey(id) ? v.ToString() : unsetText;

        private void BuildFocusRows(MBBindingList<ViewModel> rows)
        {
            int max = GameCaps.MaxFocus();

            AddBulkStatRows(rows,
                "{=CSR_Editor_Bulk_AllFocus}Set All Skill Focus",
                0, max,
                (skill, value) => CreationSession.Current.CustomFocus[skill.StringId] = value);

            AddSkillsByAttribute(rows, captured =>
                rows.Add(new StartEditorRowVM(
                    captured.Name.ToString(), 0, max, 1,
                    () => CreationSession.Current.CustomFocus.TryGetValue(captured.StringId, out int v)
                        ? v
                        : EstimateFocus(captured),
                    v =>
                    {
                        CreationSession.Current.CustomFocus[captured.StringId] = v;
                        StageViewBridge.RefreshGainedProperties();
                    },
                    isCustomized: () => CreationSession.Current.CustomFocus.ContainsKey(captured.StringId),
                    revert: () =>
                    {
                        CreationSession.Current.CustomFocus.Remove(captured.StringId);
                        StageViewBridge.RefreshGainedProperties();
                    },
                    pips: true)));
        }

        private void BuildSkillLevelRows(MBBindingList<ViewModel> rows)
        {
            int max = GameCaps.MaxSkillLevel();
            AddBulkStatRows(rows,
                "{=CSR_Editor_Bulk_AllLevels}Set All Skill Levels",
                0, max,
                (skill, value) => CreationSession.Current.CustomSkillLevels[skill.StringId] = value);

            AddSkillsByAttribute(rows, captured =>
                rows.Add(new StartEditorRowVM(
                    captured.Name.ToString(), 0, max, 5,
                    () => CreationSession.Current.CustomSkillLevels.TryGetValue(captured.StringId, out int v)
                        ? v
                        : NarrativeStep.ExpectedSkillValue(CreationSession.Current, captured),
                    v =>
                    {
                        CreationSession.Current.CustomSkillLevels[captured.StringId] = v;
                        StageViewBridge.RefreshGainedProperties();
                    },
                    isCustomized: () => CreationSession.Current.CustomSkillLevels.ContainsKey(captured.StringId),
                    revert: () =>
                    {
                        CreationSession.Current.CustomSkillLevels.Remove(captured.StringId);
                        StageViewBridge.RefreshGainedProperties();
                    },
                    bar: true)));
        }

        /// <summary>
        ///     One row per skill that has perks reachable at its starting level.
        ///     Unconfigured skills auto-fill on the custom path; a mod that allows
        ///     taking both perks of a pair is honored, since any subset can be
        ///     selected here.
        /// </summary>
        private void BuildPerkRows(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            var autoText = new TextObject("{=CSR_Editor_PerksAuto}Automatic").ToString();
            var noneText = new TextObject("{=CSR_Editor_PerksNone}No Perks").ToString();

            // The board holds every skill, so the tab is one way into it rather than a row per
            // skill; selecting the tab opens it, and this row reopens it after it is closed
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_TabPerks}Skill Perks").ToString(),
                () => new TextObject("{=CSR_Editor_PerksOpen}Open the Menu").ToString(),
                OpenPerkBoard,
                new TextObject("{=CSR_Hint_PerksOpen}Open the perk board for every skill.").ToString()));
        }

        /// <summary>
        ///     Every skill with a perk its starting level reaches is a page of the one perk board. A
        ///     page holds the skill's whole ladder, so the tiers above its level show what it would open.
        /// </summary>
        private void OpenPerkBoard()
        {
            var session = CreationSession.Current;
            var entries = PerkEntries(skill => NarrativeStep.ExpectedSkillValue(session, skill));
            if (entries.Count == 0) return;

            PerkPickerScreen.Open(entries, null, session.CustomPerks, GameCaps.AllowsBothPerks(), changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Value == null)
                        session.CustomPerks.Remove(change.Key);
                    else
                        session.CustomPerks[change.Key] = change.Value;
                }

                RebuildCurrentTab();
            });
        }

        /// <summary>A companion's or relative's perk board, over the skills given an exact level.</summary>
        private void OpenHeroPerkBoard(HeroSpec spec)
        {
            var entries = HeroPerkEntries(spec);
            if (entries.Count == 0)
            {
                EnterHeroPage("levels");
                return;
            }

            PerkPickerScreen.Open(entries, null, spec.Perks, GameCaps.AllowsBothPerks(), changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Value == null)
                        spec.Perks.Remove(change.Key);
                    else
                        spec.Perks[change.Key] = change.Value;
                }

                RebuildCurrentTab();
            });
        }

        private static List<PerkSkillEntry> HeroPerkEntries(HeroSpec spec) =>
            PerkEntries(skill => spec.SkillLevels.TryGetValue(skill.StringId, out int level) ? level : (int?)null);

        /// <summary>
        ///     One page per skill with a perk its level reaches; a skill whose level is null is left
        ///     off. A page holds the skill's whole ladder, so the tiers above show what it would open.
        /// </summary>
        private static List<PerkSkillEntry> PerkEntries(Func<SkillObject, int?> levelOf)
        {
            var entries = new List<PerkSkillEntry>();
            foreach (var skill in Skills.All)
            {
                int? level = levelOf(skill);
                if (level == null) continue;
                var perks = PerkObject.All
                    .Where(p => p.Skill == skill && !p.IsTrash)
                    .OrderBy(p => p.RequiredSkillValue)
                    .ToList();
                if (!perks.Any(p => p.RequiredSkillValue <= level.Value)) continue;
                entries.Add(new PerkSkillEntry(skill.StringId, skill.Name.ToString(), level.Value, perks));
            }

            return entries;
        }

        /// <summary>
        ///     The conversion's own career, and the picks on its board. Both are bounded by what the
        ///     rest of the start already settled: the careers offered are the ones this culture and
        ///     clan tier admit, and the picks allowed are what this starting level allows.
        /// </summary>
        private void BuildCareerRows(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            string cultureId = session.SelectedCulture?.StringId ?? string.Empty;
            var offered = Services.TaomBridge.CareersFor(cultureId, session.SelectedClanTier);
            var noneText = new TextObject("{=CSR_Editor_CareerNone}No Career").ToString();
            var careerLabel = new TextObject("{=CSR_Editor_Career}Career").ToString();

            if (offered.Count == 0)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_CareerNoneOffered}No career is open to this culture at this clan tier").ToString(),
                    () => string.Empty, () => { },
                    new TextObject("{=CSR_Hint_CareerNoneOffered}Raise the clan tier or change culture on the Path and Realm tab to open careers.").ToString(),
                    isTitle: true));
                return;
            }

            rows.Add(new StartEditorPickerRowVM(
                careerLabel,
                () => Services.TaomBridge.Careers()
                          .FirstOrDefault(c => c.Id == session.SelectedCareerId)?.Name ?? noneText,
                () =>
                {
                    var options = new List<(string, object?)> { (noneText, null) };
                    foreach (var career in offered) options.Add((career.Name, career.Id));

                    EditorPopups.ShowOptions(careerLabel, options, picked =>
                    {
                        session.SelectedCareerId = picked as string;
                        session.CareerChoiceIds.Clear();
                        RebuildCurrentTab();
                    });
                },
                new TextObject("{=CSR_Hint_Career}The career you begin in; its first rank comes with it.").ToString(),
                isCustomized: () => session.SelectedCareerId != null,
                revert: () =>
                {
                    session.SelectedCareerId = null;
                    session.CareerChoiceIds.Clear();
                    RebuildCurrentTab();
                }));

            if (string.IsNullOrEmpty(session.SelectedCareerId)) return;

            var chosen = Services.TaomBridge.Careers().FirstOrDefault(c => c.Id == session.SelectedCareerId);
            if (chosen != null && !string.IsNullOrEmpty(chosen.FirstRankName))
            {
                var rank = new TextObject("{=CSR_Editor_CareerRank}Starting Rank");
                rows.Add(new StartEditorPickerRowVM(
                    rank.ToString(), () => chosen.FirstRankName, () => { },
                    new TextObject("{=CSR_Hint_CareerRank}The rank this career begins at; it comes with the career.").ToString(),
                    isTitle: true));
            }

            // The board's picks are capped by the starting level, so the row states the count it
            // allows rather than letting the player choose picks the campaign would drop
            int level = System.Math.Max(1, session.CustomLevel ?? 1);
            int allowed = System.Math.Max(0, Services.TaomBridge.MaxCareerChoices(level) - 1);
            var choices = Services.TaomBridge.ChoicesFor(session.SelectedCareerId!);

            var boardLabel = new TextObject("{=CSR_Editor_CareerBoard}Career Board").ToString();
            rows.Add(new StartEditorPickerRowVM(
                boardLabel,
                () =>
                {
                    var taken = new TextObject("{=CSR_Editor_CareerTaken}{COUNT} of {ALLOWED} picks");
                    taken.SetTextVariable("COUNT", session.CareerChoiceIds.Count);
                    taken.SetTextVariable("ALLOWED", allowed);
                    return taken.ToString();
                },
                () =>
                {
                    if (allowed == 0 || choices.Count == 0) return;

                    var options = choices
                        .Select(c => (CareerChoiceLabel(c), (object?)c.Id))
                        .ToList();
                    var description = new TextObject(
                        "{=CSR_Editor_CareerBoardDesc}Your starting level allows {ALLOWED} picks beyond the first rank.");
                    description.SetTextVariable("ALLOWED", allowed);

                    EditorPopups.ShowMultiOptions(boardLabel, description.ToString(), options, picked =>
                    {
                        session.CareerChoiceIds.Clear();
                        foreach (var id in picked.OfType<string>().Take(allowed))
                            session.CareerChoiceIds.Add(id);
                        RebuildCurrentTab();
                    });
                },
                new TextObject("{=CSR_Hint_CareerBoard}Picks on the career's own board, within what your level allows.").ToString(),
                isCustomized: () => session.CareerChoiceIds.Count > 0,
                revert: () =>
                {
                    session.CareerChoiceIds.Clear();
                    RebuildCurrentTab();
                }));
        }

        private static string CareerChoiceLabel(Services.TaomCareerChoice choice)
        {
            if (string.IsNullOrEmpty(choice.GroupName)) return choice.Description;

            var label = new TextObject("{=CSR_Editor_CareerChoice}{GROUP}: {CHOICE}");
            label.SetTextVariable("GROUP", choice.GroupName);
            label.SetTextVariable("CHOICE", choice.Description);
            return label.ToString();
        }
        private void BuildTraitRows(MBBindingList<ViewModel> rows) =>
            AddTraitRows(rows, CreationSession.Current.CustomTraits, null);

        /// <summary>
        ///     The trait rows over one sheet's exact levels, the player's own or a
        ///     generated hero's. <paramref name="unsetText"/> replaces the level
        ///     while it is unset; null shows the neutral level the steppers start at.
        /// </summary>
        private void AddTraitRows(MBBindingList<ViewModel> rows, Dictionary<string, int> levels, string? unsetText)
        {
            var traits = ConsequenceStep.EditableTraits();

            // Each trait keeps its own bounds; bulk entries clamp per trait
            void ApplyTrait(TraitObject trait, int value) =>
                levels[trait.StringId] = Math.Max(trait.MinValue, Math.Min(trait.MaxValue, value));

            int bulkMin = traits.Min(t => t.MinValue);
            int bulkMax = traits.Max(t => t.MaxValue);

            var allLabel = new TextObject("{=CSR_Editor_Bulk_AllTraits}Set All Traits").ToString();
            rows.Add(new StartEditorPickerRowVM(allLabel,
                () => new TextObject("{=CSR_Editor_Bulk_AllTraitsValue}Every Trait at Once").ToString(),
                () => EditorPopups.ShowNumber(allLabel, bulkMin, bulkMax, value =>
                {
                    foreach (var trait in traits) ApplyTrait(trait, value);
                    RebuildCurrentTab();
                })));

            foreach (var trait in traits)
            {
                var captured = trait;
                // The trait itself declares its range, so trait-range mods are honored
                rows.Add(new StartEditorRowVM(
                    captured.Name.ToString(), captured.MinValue, captured.MaxValue, 1,
                    () => levels.TryGetValue(captured.StringId, out int v) ? v : 0,
                    v => levels[captured.StringId] = v,
                    format: UnsetReads(levels, captured.StringId, unsetText),
                    isCustomized: () => levels.ContainsKey(captured.StringId),
                    revert: () => levels.Remove(captured.StringId),
                    pips: true));
            }
        }

        private void BuildResourceRows(MBBindingList<ViewModel> rows)
        {
            var settings = GlobalSettings<CSSettings>.Instance;
            int maxLevel = GameCaps.MaxHeroLevel();

            int markWealth = rows.Count;
            // The conversion's own faction resource, where the culture deals in one. It is a store
            // rather than a currency the game knows, so it sits beside the purse and is stated in
            // the conversion's own name for it
            var factionResource = Services.TaomBridge.ResourceFor(CreationSession.Current.SelectedCulture?.StringId);
            if (factionResource != null)
                rows.Add(new StartEditorRowVM(
                    factionResource.Name, 0, factionResource.Cap, 100,
                    () => CreationSession.Current.CustomFactionResource
                          ?? ConversionStep.FactionResourceFor(CreationSession.Current),
                    v => CreationSession.Current.CustomFactionResource = v,
                    isCustomized: () => CreationSession.Current.CustomFactionResource != null,
                    revert: () =>
                    {
                        CreationSession.Current.CustomFactionResource = null;
                        RebuildCurrentTab();
                    }));
            var goldLabel = new TextObject("{=CSR_Resource_Gold}Gold").ToString();
            rows.Add(new StartEditorRowVM(
                goldLabel, 0, GameCaps.MaxGold, 500,
                () => ResourceStep.EffectiveGold(CreationSession.Current, settings),
                v =>
                {
                    CreationSession.Current.CustomGold = v;
                    RebuildCurrentTab();
                },
                chooseAutomatic: () => ChooseBand(goldLabel,
                    () => CreationSession.Current.CustomGold == null ? CreationSession.Current.SelectedGold : null,
                    band =>
                    {
                        CreationSession.Current.SelectedGold = band;
                        CreationSession.Current.CustomGold = null;
                    },
                    band => ResourceStep.GoldFor(CreationSession.Current, settings, band))));

            // A start type that does not deal in influence is granted none whatever
            // the band holds, so offering a band there would be a dead control
            var influenceLabel = new TextObject("{=CSR_Resource_Influence}Influence").ToString();
            bool usesInfluence = CSSettings.UsesInfluence(CreationSession.Current.SelectedStartType);
            rows.Add(new StartEditorRowVM(
                influenceLabel, 0, GameCaps.MaxInfluence, 50,
                () => ResourceStep.EffectiveInfluence(CreationSession.Current, settings),
                v =>
                {
                    CreationSession.Current.CustomInfluence = v;
                    RebuildCurrentTab();
                },
                isCustomized: () => CreationSession.Current.CustomInfluence != null,
                revert: () =>
                {
                    CreationSession.Current.CustomInfluence = null;
                    RebuildCurrentTab();
                },
                chooseAutomatic: usesInfluence
                    ? () => ChooseBand(influenceLabel,
                        () => CreationSession.Current.CustomInfluence == null
                            ? CreationSession.Current.SelectedInfluence
                            : null,
                        band =>
                        {
                            CreationSession.Current.SelectedInfluence = band;
                            CreationSession.Current.CustomInfluence = null;
                        },
                        band => ResourceStep.InfluenceFor(CreationSession.Current, settings, band))
                    : null));

            int markMuster = rows.Count;
            // The party size limit covers everyone: the limit minus yourself,
            // your companions, and the family riding with you is troop room
            int troopCeiling = Math.Max(0,
                GameCaps.MaxTroopsLive(CreationSession.Current.EffectiveClanTier, CreationSession.Current)
                - 1 - CreationSession.Current.StartingCompanions
                - FamilyAges.InPartyCount(CreationSession.Current));
            // A ceiling that shrank since the number was typed re-clamps it,
            // so the row can never display more than it can deliver
            if (CreationSession.Current.CustomTroops > troopCeiling)
                CreationSession.Current.CustomTroops = troopCeiling;
            var troopsLabel = new TextObject("{=CSR_Resource_Troops}Troops").ToString();
            rows.Add(new StartEditorRowVM(
                troopsLabel, 0, troopCeiling, 5,
                () => Math.Min(troopCeiling, ResourceStep.EffectiveTroops(CreationSession.Current, settings)),
                v =>
                {
                    CreationSession.Current.CustomTroops = v;
                    RebuildCurrentTab();
                },
                chooseAutomatic: () => ChooseBand(troopsLabel,
                    () => CreationSession.Current.CustomTroops == null ? CreationSession.Current.SelectedTroops : null,
                    band =>
                    {
                        CreationSession.Current.SelectedTroops = band;
                        CreationSession.Current.CustomTroops = null;
                    },
                    // Trimmed by the same ceiling the row and the apply both use, so each band
                    // states the column that actually rides out
                    band => Math.Min(troopCeiling, ResourceStep.TroopsFor(CreationSession.Current, settings, band)))));

            rows.Add(new StartEditorRowVM(
                new TextObject("{=CSR_Resource_Level}Level").ToString(), 1, maxLevel, 1,
                () => Math.Min(maxLevel, CreationSession.Current.CustomLevel ?? 1),
                v => CreationSession.Current.CustomLevel = v,
                isCustomized: () => CreationSession.Current.CustomLevel != null,
                revert: () => CreationSession.Current.CustomLevel = null));

            int markInventory = rows.Count;
            BuildFoodRows(rows);

            InsertSectionHeader(rows, markInventory, rows.Count,
                "{=CSR_Section_Inventory}Inventory");
            InsertSectionHeader(rows, markMuster, markInventory,
                "{=CSR_Section_Muster}Muster");
            InsertSectionHeader(rows, markWealth, markMuster,
                "{=CSR_Section_Wealth}Wealth");
        }

        /// <summary>
        ///     The band behind a resource line's Automatic button, which the guided route settles
        ///     in a chapter the editor never walks. Each band shows the figure it gives, the band
        ///     in force comes preselected, and choosing one hands the value back to it at once.
        /// </summary>
        private void ChooseBand(string label, Func<RangePreset?> inForce, Action<RangePreset> set,
            Func<RangePreset, int> figure)
        {
            var current = inForce();
            EditorPopups.ShowOptions(label,
                Bands.Select(b => new PickerOption(BandName(b), b, startsSelected: b == current,
                    detail: figure(b).ToString("N0"))).ToList(),
                picked =>
                {
                    if (picked is not RangePreset band) return;
                    set(band);
                    RebuildCurrentTab();
                });
        }

        private static readonly RangePreset[] Bands =
        {
            RangePreset.Minimum, RangePreset.Low, RangePreset.Standard,
            RangePreset.High, RangePreset.Maximum
        };

        private static string BandName(RangePreset band) => new TextObject(band switch
        {
            RangePreset.Minimum => "{=CSR_Band_Minimum}Least",
            RangePreset.Low => "{=CSR_Band_Low}Low",
            RangePreset.High => "{=CSR_Band_High}High",
            RangePreset.Maximum => "{=CSR_Band_Maximum}Most",
            _ => "{=CSR_Band_Standard}Standard"
        }).ToString();

        /// <summary>Compose extra food and mount stacks for the starting inventory.</summary>
        private void BuildFoodRows(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            BuildProvisionsRow(rows, session);
            BuildInventoryRows(rows, session.CustomFood,
                new TextObject("{=CSR_Food_Add}Add Food").ToString(),
                new TextObject("{=CSR_Food_Add_Value}Choose Food").ToString(),
                ArmorQuery.FoodItems);
            BuildInventoryRows(rows, session.CustomMounts,
                new TextObject("{=CSR_Mounts_Add}Add Mounts").ToString(),
                new TextObject("{=CSR_Mounts_Add_Value}Choose Mounts").ToString(),
                ArmorQuery.MountItems);

            // A caravan master composes cargo, not just provisions
            if (session.SelectedStartType == StartType.CaravanMaster)
                BuildInventoryRows(rows, session.CustomTradeGoods,
                    new TextObject("{=CSR_TradeGoods_Add}Add Trade Goods").ToString(),
                    new TextObject("{=CSR_TradeGoods_Add_Value}Choose Trade Goods").ToString(),
                    ArmorQuery.TradeGoodItems);
        }

        /// <summary>
        ///     A head start on the wagons: choosing a plan loads its grain and pack animals into Add
        ///     Food and Add Mounts, where every stack can then be changed or removed. The lists are
        ///     what the start loads, and the plan only fills them.
        /// </summary>
        private void BuildProvisionsRow(MBBindingList<ViewModel> rows, CharacterCreationSession session)
        {
            var settings = GlobalSettings<CSSettings>.Instance;
            var label = new TextObject("{=CSR_Editor_Provisions}Provisions").ToString();

            // The plan the start would load is shown as the stacks it loads the first time the
            // wagons are drawn, so nothing is loaded that the lists below do not show
            if (!session.ProvisionsMaterialized)
            {
                session.ProvisionsMaterialized = true;
                if (session.CustomFood.Count == 0 && session.CustomMounts.Count == 0)
                    LoadPlan(session, settings, session.Provisions);
            }

            string Value(ProvisionPlan plan)
            {
                var load = ResourceStep.PlanLoad(session, settings, plan);
                if (load.Grain == 0 && load.PackAnimals == 0)
                    return new TextObject("{=CSR_Editor_Provisions_Nothing}Nothing at All").ToString();

                if (load.PackAnimals == 0)
                {
                    var grainOnly = new TextObject("{=CSR_Editor_Provisions_Grain}{GRAIN} grain");
                    grainOnly.SetTextVariable("GRAIN", load.Grain);
                    return grainOnly.ToString();
                }

                var text = new TextObject("{=CSR_Editor_Provisions_Load}{GRAIN} grain and {ANIMALS}");
                text.SetTextVariable("GRAIN", load.Grain);
                text.SetTextVariable("ANIMALS", MenuText.Count(load.PackAnimals,
                    "{=CSR_Editor_Provisions_Animal}{COUNT} pack animal",
                    "{=CSR_Editor_Provisions_Animals}{COUNT} pack animals"));
                return text.ToString();
            }

            rows.Add(new StartEditorPickerRowVM(
                label,
                () => PlanTitle(session.Provisions),
                () => EditorPopups.ShowOptions(label,
                    Plans.Select(p => new PickerOption(PlanTitle(p), p, startsSelected: p == session.Provisions,
                        detail: Value(p))).ToList(),
                    picked =>
                    {
                        if (picked is not ProvisionPlan plan) return;
                        session.Provisions = plan;
                        LoadPlan(session, settings, plan);
                        RebuildCurrentTab();
                    }),
                new TextObject(
                        "{=CSR_Hint_Provisions}A head start on the wagons: a plan loads its grain and pack animals into Add Food and Add Mounts below, where each stack can be changed.")
                    .ToString()));
        }

        /// <summary>Replaces the food and mount lists with what a plan loads, sized against the muster.</summary>
        private static void LoadPlan(CharacterCreationSession session, CSSettings? settings, ProvisionPlan plan)
        {
            session.CustomFood.Clear();
            session.CustomMounts.Clear();
            if (plan == ProvisionPlan.Bare) return;

            var load = ResourceStep.PlanLoad(session, settings, plan);
            var objects = TaleWorlds.ObjectSystem.MBObjectManager.Instance;

            var grain = objects.GetObject<ItemObject>("grain");
            if (grain != null && load.Grain > 0)
                session.CustomFood.Add(new InventoryEntry(grain, load.Grain));

            var packAnimal = objects.GetObject<ItemObject>("mule") ?? objects.GetObject<ItemObject>("sumpter_horse");
            if (packAnimal != null && load.PackAnimals > 0)
                session.CustomMounts.Add(new InventoryEntry(packAnimal, load.PackAnimals));
        }

        private static readonly ProvisionPlan[] Plans =
        {
            ProvisionPlan.Sensible, ProvisionPlan.Light, ProvisionPlan.Bare
        };

        /// <summary>The plan by the name the Provisions chapter gives it; its load goes on the line beneath.</summary>
        private static string PlanTitle(ProvisionPlan plan) => plan switch
        {
            ProvisionPlan.Light => new TextObject("{=CSR_Provisions_Light}Traveling Light").ToString(),
            ProvisionPlan.Bare => new TextObject("{=CSR_Provisions_Bare}An Empty Larder").ToString(),
            _ => new TextObject("{=CSR_Provisions_Sensible}A Sensible Larder").ToString()
        };

        private void BuildInventoryRows(MBBindingList<ViewModel> rows, List<InventoryEntry> entries,
            string addLabel, string addValue, Func<List<ItemObject>> itemsProvider)
        {
            foreach (var entry in entries.ToList())
            {
                var captured = entry;
                rows.Add(new StartEditorPickerRowVM(
                    captured.Item.Name?.ToString() ?? captured.Item.StringId,
                    () =>
                    {
                        var value = new TextObject("{=CSR_Food_Count}x{COUNT}");
                        value.SetTextVariable("COUNT", captured.Count);
                        return value.ToString();
                    },
                    () => EditorPopups.ShowOptions(
                        captured.Item.Name?.ToString() ?? captured.Item.StringId,
                        new List<(string, object?)>
                        {
                            (new TextObject("{=CSR_Food_SetAmount}Set Amount").ToString(), "amount"),
                            (new TextObject("{=CSR_Editor_Remove}Remove").ToString(), "remove")
                        }, picked =>
                        {
                            switch (picked as string)
                            {
                                case "amount":
                                    EditorPopups.ShowNumber(
                                        captured.Item.Name?.ToString() ?? captured.Item.StringId, 1, 10000,
                                        amount =>
                                        {
                                            captured.Count = amount;
                                            RebuildCurrentTab();
                                        });
                                    return;
                                case "remove":
                                    entries.Remove(captured);
                                    // Empty lists fall back to the plan when the start is applied, so
                                    // emptying the wagons by hand has to leave the plan empty too
                                    var wagons = CreationSession.Current;
                                    if (wagons.CustomFood.Count == 0 && wagons.CustomMounts.Count == 0)
                                        wagons.Provisions = ProvisionPlan.Bare;
                                    break;
                            }

                            RebuildCurrentTab();
                        })));
            }

            rows.Add(new StartEditorPickerRowVM(
                addLabel,
                () => addValue,
                () =>
                {
                    // Already-added items hide from the list; their amounts are
                    // adjusted on their own rows instead
                    var chosen = entries.Select(e => e.Item).ToHashSet();
                    var remaining = itemsProvider().Where(i => !chosen.Contains(i)).ToList();
                    ItemPickerScreen.Open(addLabel, remaining,
                        (choice, item) =>
                        {
                            if (choice != ItemPickChoice.Item || item == null) return;
                            EditorPopups.ShowNumber(item.Name?.ToString() ?? item.StringId, 1, 10000, amount =>
                            {
                                entries.Add(new InventoryEntry(item, amount));
                                RebuildCurrentTab();
                            });
                        }, includeAutoOption: false, includeNoneOption: false);
                }));
        }

        #endregion

        #region Gear

        private void BuildGearRows(MBBindingList<ViewModel> rows, OutfitKind kind)
        {
            BuildBearingRow(rows);
            AddOutfitBulkRow(rows, kind);

            var armorSlots = new (EquipmentIndex Slot, string LabelKey)[]
            {
                (EquipmentIndex.Head, "{=CSR_SlotName_Head}Head Armor"),
                (EquipmentIndex.Body, "{=CSR_SlotName_Body}Body Armor"),
                (EquipmentIndex.Leg, "{=CSR_SlotName_Leg}Leg Armor"),
                (EquipmentIndex.Gloves, "{=CSR_SlotName_Gloves}Gloves"),
                (EquipmentIndex.Cape, "{=CSR_SlotName_Cape}Cape")
            };

            foreach (var (slot, labelKey) in armorSlots)
                AddArmorRow(rows, kind, slot, new TextObject(labelKey).ToString());

            // Mounts: battle and civilian only; stealth has no mount slots
            if (kind != OutfitKind.Stealth)
            {
                AddArmorRow(rows, kind, EquipmentIndex.Horse,
                    new TextObject("{=CSR_SlotName_Horse}Horse").ToString());
                AddArmorRow(rows, kind, EquipmentIndex.HorseHarness,
                    new TextObject("{=CSR_SlotName_Harness}Horse Harness").ToString());
            }

            // Weapon slots, every outfit
            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
                AddWeaponRow(rows, kind, slotIndex);

            if (kind == OutfitKind.Battle)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Banner}Banner").ToString(),
                    () => CreationSession.Current.ExactBanner?.Name?.ToString()
                          ?? new TextObject("{=CSR_Picker_None}None").ToString(),
                    () =>
                    {
                        var banners = ArmorQuery.BannerItems(CreationSession.Current.SelectedCulture);
                        ItemPickerScreen.Open(
                            new TextObject("{=CSR_Editor_Banner}Banner").ToString(), banners, (choice, item) =>
                            {
                                CreationSession.Current.ExactBanner =
                                    choice == ItemPickChoice.Item ? item : null;
                                // Carried into battle, never drawn on the render:
                                // an equipped banner hangs off the character's hip
                                GearCustomizationMenu.PreviewService?.SetSlotEmpty(
                                    EquipmentIndex.ExtraWeaponSlot);
                                RebuildCurrentTab();
                            }, includeAutoOption: false, includeNoneOption: true,
                            currentItem: CreationSession.Current.ExactBanner);
                    },
                    new TextObject(
                            "{=CSR_Hint_BannerRow}Carried with the party rather than worn, so it does not show on the character.")
                        .ToString()));
            }
        }

        /// <summary>
        ///     How the character carries their station, which is the tier every
        ///     slot left to the quartermaster is filled at. The guided route asks
        ///     this in a chapter the editor never walks, so without this row the
        ///     stores were tiered by a value the editor gave no way to set.
        /// </summary>
        private void BuildBearingRow(MBBindingList<ViewModel> rows)
        {
            var session = CreationSession.Current;
            var settings = GlobalSettings<CSSettings>.Instance;
            var label = new TextObject("{=CSR_Editor_Bearing}Bearing").ToString();

            string Value(ArmorBearing bearing)
            {
                var text = new TextObject("{=CSR_Editor_BearingTier}{BEARING}: tier {TIER}");
                text.SetTextVariable("BEARING", BearingName(bearing));
                text.SetTextVariable("TIER", EquipmentStep.TierFor(session, settings, bearing));
                return text.ToString();
            }

            void Take(ArmorBearing bearing)
            {
                session.Bearing = bearing;
                // The tier moved, so the generated baseline behind the stage is stale
                RegenerateBattlePreview();
                RebuildCurrentTab();
            }

            rows.Add(new StartEditorPickerRowVM(
                label,
                () => Value(session.Bearing),
                () => EditorPopups.ShowOptions(label,
                    Bearings.Select(b => (Value(b), (object?)b)).ToList(),
                    picked =>
                    {
                        if (picked is ArmorBearing bearing) Take(bearing);
                    }),
                new TextObject(
                        "{=CSR_Hint_Bearing}The tier the stores fill every slot you leave to the quartermaster at, one step either side of your standing.")
                    .ToString(),
                isCustomized: () => session.Bearing != ArmorBearing.Station,
                revert: () => Take(ArmorBearing.Station)));
        }

        private static readonly ArmorBearing[] Bearings =
        {
            ArmorBearing.Plain, ArmorBearing.Station, ArmorBearing.Finest
        };

        private static string BearingName(ArmorBearing bearing) => new TextObject(bearing switch
        {
            ArmorBearing.Plain => "{=CSR_Bearing_Plain}Below Your Station",
            ArmorBearing.Finest => "{=CSR_Bearing_Finest}Above Your Station",
            _ => "{=CSR_Bearing_Station}Your Station"
        }).ToString();

        /// <summary>
        ///     The bulk way in and out of the quartermaster's hands. Without it,
        ///     dressing a character who starts with nothing is one popup per
        ///     slot, a dozen times over, and there was no path back to automatic
        ///     at all.
        /// </summary>
        private void AddOutfitBulkRow(MBBindingList<ViewModel> rows, OutfitKind kind)
        {
            var label = new TextObject("{=CSR_Editor_SetAllSlots}Set All Slots").ToString();

            rows.Add(new StartEditorPickerRowVM(
                label,
                () => OutfitSummary(kind),
                () =>
                {
                    var options = new List<(string, object?)>
                    {
                        (new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString(), "auto"),
                        (new TextObject("{=CSR_Picker_None}None").ToString(), "none")
                    };

                    // The outfits the conversion itself would have dressed this character in,
                    // offered whole rather than left for the player to rebuild slot by slot
                    foreach (var kit in ConversionKits.KitsFor(
                                 CreationSession.Current.SelectedCulture, Hero.MainHero?.IsFemale ?? false))
                        options.Add((kit.Name, kit.RosterId));

                    EditorPopups.ShowOptions(label, options, picked =>
                {
                    var session = CreationSession.Current;
                    if (Equals(picked, "auto")) ResetOutfitToAutomatic(session, kind);
                    else if (picked is string rosterId && rosterId != "none")
                        ConversionKits.Apply(session, kind, rosterId);
                    else ResetOutfitToNone(session, kind);

                    if (kind == OutfitKind.Battle) RegenerateBattlePreview();
                    else ShowOutfit(kind);
                    RebuildCurrentTab();
                    });
                },
                new TextObject(
                        "{=CSR_Hint_SetAllSlots}Hand every slot on this tab back to the quartermaster, dress it in one of the conversion's own outfits, or empty it all at once.")
                    .ToString()));
        }

        /// <summary>
        ///     The mount this outfit will actually ride with: the one chosen by hand where there is
        ///     one, and otherwise the one the quartermaster has drawn into the preview.
        /// </summary>
        private static ItemObject? ChosenMount(CharacterCreationSession session, OutfitKind kind)
        {
            if (session.ExactOutfit.TryGetValue((kind, EquipmentIndex.Horse), out var picked))
                return picked;

            return session.PreviewEquipment?[EquipmentIndex.Horse].Item;
        }

        /// <summary>Takes off a harness the newly chosen mount cannot wear.</summary>
        private static void DropHarnessThatNoLongerFits(CharacterCreationSession session, OutfitKind kind)
        {
            if (!session.ExactOutfit.TryGetValue((kind, EquipmentIndex.HorseHarness), out var harness) ||
                harness == null)
                return;

            if (MountFit.Fits(ChosenMount(session, kind), harness)) return;

            session.ExactOutfit.Remove((kind, EquipmentIndex.HorseHarness));
            CSLogger.Info($"StartEditor: {harness.Name} was taken off; the mount now chosen cannot wear it.");
        }

        /// <summary>Reads the tab back as one line: chosen, empty, or automatic.</summary>
        private static string OutfitSummary(OutfitKind kind)
        {
            var session = CreationSession.Current;
            var entries = session.ExactOutfit.Where(e => e.Key.Kind == kind).ToList();

            int chosen = entries.Count(e => e.Value != null);
            if (kind == OutfitKind.Battle)
                chosen += session.WeaponChoices.Count(w => w.ExactItem != null);

            if (chosen > 0)
            {
                var text = new TextObject("{=CSR_Editor_SlotsChosen}{COUNT} chosen");
                text.SetTextVariable("COUNT", chosen);
                return text.ToString();
            }

            bool anyEmpty = entries.Any(e => e.Value == null);
            if (kind == OutfitKind.Battle)
                anyEmpty = anyEmpty || session.WeaponChoices.Any(w => w.ExplicitlyEmpty);

            return anyEmpty
                ? new TextObject("{=CSR_Picker_None}None").ToString()
                : new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString();
        }

        // Automatic is the absence of an entry, so the quartermaster picks the
        // slot again; an entry set to null would mean a deliberate empty slot
        private static void ResetOutfitToAutomatic(CharacterCreationSession session, OutfitKind kind)
        {
            foreach (var key in session.ExactOutfit.Keys.Where(k => k.Kind == kind).ToList())
                session.ExactOutfit.Remove(key);

            if (kind != OutfitKind.Battle) return;

            session.ExactBanner = null;
            foreach (var weapon in session.WeaponChoices)
            {
                weapon.Reset();
                weapon.ExplicitlyEmpty = false;
            }
        }

        private void AddArmorRow(MBBindingList<ViewModel> rows, OutfitKind kind, EquipmentIndex slot, string label)
        {
            rows.Add(new StartEditorPickerRowVM(
                label,
                () => CreationSession.Current.ExactOutfit.TryGetValue((kind, slot), out var item)
                    ? item?.Name?.ToString() ?? new TextObject("{=CSR_Picker_None}None").ToString()
                    : new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString(),
                () =>
                {
                    var session = CreationSession.Current;
                    int tier = EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance);
                    // A harness is offered only for the mount on this tab, because the game
                    // itself refuses a pairing across animal kinds and renders it as a mess
                    var mount = slot == EquipmentIndex.HorseHarness ? ChosenMount(session, kind) : null;
                    var items = ArmorQuery.QualifyingItems(slot, session.SelectedCulture, tier, session, kind, mount);
                    session.ExactOutfit.TryGetValue((kind, slot), out var currentPick);
                    ItemPickerScreen.Open(label, items, (choice, item) =>
                    {
                        switch (choice)
                        {
                            case ItemPickChoice.Auto:
                                session.ExactOutfit.Remove((kind, slot));
                                break;
                            case ItemPickChoice.None:
                                session.ExactOutfit[(kind, slot)] = null;
                                break;
                            default:
                                session.ExactOutfit[(kind, slot)] = item;
                                break;
                        }

                        // Changing the mount can leave a harness that no longer belongs on it
                        if (slot == EquipmentIndex.Horse) DropHarnessThatNoLongerFits(session, kind);

                        ReflectGearPick(kind, slot, choice, item);
                        RebuildCurrentTab();
                    }, currentItem: currentPick);
                },
                new TextObject(
                        "{=CSR_Hint_ArmorRow}The list holds what your culture and equipment tier allow for this slot.")
                    .ToString(),
                () => CreationSession.Current.ExactOutfit.ContainsKey((kind, slot)),
                () =>
                {
                    CreationSession.Current.ExactOutfit.Remove((kind, slot));
                    if (kind == OutfitKind.Battle)
                    {
                        RegenerateBattlePreview();
                    }
                    else ShowOutfit(kind);
                    RebuildCurrentTab();
                }));
        }

        private void AddWeaponRow(MBBindingList<ViewModel> rows, OutfitKind kind, int slotIndex)
        {
            var slot = EquipmentIndex.Weapon0 + slotIndex;
            var label = new TextObject("{=CSR_Gear_SlotTitle}Weapon Slot {SLOT}");
            label.SetTextVariable("SLOT", slotIndex + 1);
            string labelText = label.ToString();

            rows.Add(new StartEditorPickerRowVM(
                labelText,
                () =>
                {
                    var session = CreationSession.Current;
                    var noneText = new TextObject("{=CSR_Picker_None}None").ToString();
                    if (kind == OutfitKind.Battle)
                    {
                        var weaponChoice = session.WeaponChoices[slotIndex];
                        if (weaponChoice.ExplicitlyEmpty) return noneText;
                        return weaponChoice.ExactItem?.Name?.ToString()
                               ?? new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString();
                    }

                    return session.ExactOutfit.TryGetValue((kind, slot), out var item)
                        ? item?.Name?.ToString() ?? noneText
                        : new TextObject("{=CSR_Editor_Quartermaster}Quartermaster's Pick").ToString();
                },
                () => PickWeapon(kind, slotIndex, slot, labelText),
                new TextObject(
                        "{=CSR_Hint_WeaponRow}Filter the armory by weapon class, or leave the slot for the quartermaster.")
                    .ToString(),
                () =>
                {
                    var session = CreationSession.Current;
                    if (kind != OutfitKind.Battle) return session.ExactOutfit.ContainsKey((kind, slot));
                    var weaponChoice = session.WeaponChoices[slotIndex];
                    return weaponChoice.ExactItem != null || weaponChoice.ExplicitlyEmpty;
                },
                () =>
                {
                    var session = CreationSession.Current;
                    if (kind == OutfitKind.Battle)
                    {
                        var weaponChoice = session.WeaponChoices[slotIndex];
                        weaponChoice.Reset();
                        weaponChoice.ExplicitlyEmpty = false;
                        RegenerateBattlePreview();
                    }
                    else
                    {
                        session.ExactOutfit.Remove((kind, slot));
                        ShowOutfit(kind);
                    }

                    RebuildCurrentTab();
                }));
        }

        /// <summary>
        ///     One armory picker over every usable weapon; the class narrowing is
        ///     a filter inside it, so there is no submenu to back out of.
        /// </summary>
        private void PickWeapon(OutfitKind kind, int slotIndex, EquipmentIndex slot, string labelText)
        {
            var session = CreationSession.Current;
            int tier = EquipmentStep.EffectiveTier(session, GlobalSettings<CSSettings>.Instance);
            var items = GearQuery.AllWeapons(session.SelectedCulture, tier, session, kind);
            ItemObject? currentPick;
            if (kind == OutfitKind.Battle)
            {
                currentPick = session.WeaponChoices[slotIndex].ExactItem;
            }
            else
            {
                session.ExactOutfit.TryGetValue((kind, slot), out currentPick);
            }

            ItemPickerScreen.Open(labelText, items, (choice, item) =>
            {
                if (kind == OutfitKind.Battle)
                {
                    var weaponChoice = session.WeaponChoices[slotIndex];
                    switch (choice)
                    {
                        case ItemPickChoice.Auto:
                            weaponChoice.Reset();
                            RegenerateBattlePreview();
                            break;
                        case ItemPickChoice.None:
                            weaponChoice.Reset();
                            weaponChoice.ExplicitlyEmpty = true;
                            GearCustomizationMenu.PreviewService?.SetSlotEmpty(slot);
                            break;
                        default:
                            weaponChoice.ExplicitlyEmpty = false;
                            weaponChoice.ExactItem = item;
                            if (item != null)
                                GearCustomizationMenu.PreviewService?.SetExactItem(slot, item);
                            break;
                    }
                }
                else
                {
                    switch (choice)
                    {
                        case ItemPickChoice.Auto:
                            session.ExactOutfit.Remove((kind, slot));
                            break;
                        case ItemPickChoice.None:
                            session.ExactOutfit[(kind, slot)] = null;
                            break;
                        default:
                            session.ExactOutfit[(kind, slot)] = item;
                            break;
                    }

                    ShowOutfit(kind);
                }

                RebuildCurrentTab();
            }, currentItem: currentPick);
        }

        /// <summary>Puts the picked item on the 3D character for the edited outfit.</summary>
        private static void ReflectGearPick(OutfitKind kind, EquipmentIndex slot, ItemPickChoice choice,
            ItemObject? item)
        {
            if (kind == OutfitKind.Battle)
            {
                switch (choice)
                {
                    case ItemPickChoice.Item when item != null:
                        GearCustomizationMenu.PreviewService?.SetExactItem(slot, item);
                        break;
                    case ItemPickChoice.None:
                        GearCustomizationMenu.PreviewService?.SetSlotEmpty(slot);
                        break;
                    default:
                        RegenerateBattlePreview();
                        break;
                }
            }
            else
            {
                ShowOutfit(kind);
            }
        }

        #endregion

        #region Family

        private void BuildFamilyRows(MBBindingList<ViewModel> rows)
        {
            var members = CreationSession.Current.FamilyMembers;
            var aliveText = new TextObject("{=CSR_Editor_Alive}Alive").ToString();
            var deadText = new TextObject("{=CSR_Editor_Dead}Dead").ToString();

            string RelationName(FamilyRelation relation) => relation switch
            {
                FamilyRelation.Father => new TextObject("{=CSR_Editor_Father}Father").ToString(),
                FamilyRelation.Mother => new TextObject("{=CSR_Editor_Mother}Mother").ToString(),
                FamilyRelation.Spouse => new TextObject("{=CSR_Editor_Spouse}Spouse").ToString(),
                FamilyRelation.Brother => new TextObject("{=CSR_Editor_Brother}Brother").ToString(),
                FamilyRelation.Sister => new TextObject("{=CSR_Editor_Sister}Sister").ToString(),
                FamilyRelation.Son => new TextObject("{=CSR_Editor_Son}Son").ToString(),
                FamilyRelation.Daughter => new TextObject("{=CSR_Editor_Daughter}Daughter").ToString(),
                _ => relation.ToString()
            };

            var seenPerRelation = new Dictionary<FamilyRelation, int>();
            foreach (var member in members.ToList())
            {
                var captured = member;
                bool isParent = captured.Relation is FamilyRelation.Father or FamilyRelation.Mother;

                seenPerRelation.TryGetValue(captured.Relation, out int seen);
                seenPerRelation[captured.Relation] = seen + 1;

                string label = RelationName(captured.Relation);
                if (!isParent && members.Count(m => m.Relation == captured.Relation) > 1)
                    label = $"{label} {seen + 1}";

                rows.Add(new StartEditorPickerRowVM(
                    label,
                    () => MemberValueText(captured, aliveText, deadText),
                    () => EnterMemberEdit(captured, label)));
            }

            // Add-member row; a second spouse only exists when the installed
            // marriage model allows polygamy
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_AddMember}Add Family Member").ToString(),
                () => new TextObject("{=CSR_Editor_AddMember_Value}Choose a Relation").ToString(),
                () =>
                {
                    var options = new List<(string, object?)>();
                    if (members.All(m => m.Relation != FamilyRelation.Spouse) || GameCaps.AllowsPolygamy())
                        options.Add((RelationName(FamilyRelation.Spouse), FamilyRelation.Spouse));
                    options.Add((RelationName(FamilyRelation.Brother), FamilyRelation.Brother));
                    options.Add((RelationName(FamilyRelation.Sister), FamilyRelation.Sister));
                    options.Add((RelationName(FamilyRelation.Son), FamilyRelation.Son));
                    options.Add((RelationName(FamilyRelation.Daughter), FamilyRelation.Daughter));

                    EditorPopups.ShowOptions(
                        new TextObject("{=CSR_Editor_AddMember}Add Family Member").ToString(), options, picked =>
                        {
                            if (picked is not FamilyRelation relation) return;
                            var added = new FamilyMemberSpec(relation, true);
                            members.Add(added);

                            // Children need a spouse to exist
                            if (relation is FamilyRelation.Son or FamilyRelation.Daughter &&
                                members.All(m => m.Relation != FamilyRelation.Spouse))
                                members.Add(new FamilyMemberSpec(FamilyRelation.Spouse, true));

                            // Straight onto the new member's own sheet
                            EnterMemberEdit(added, RelationName(relation));
                        });
                }));
        }

        /// <summary>
        ///     Save the whole current setup under a name, or load, overwrite, and
        ///     delete saved presets. Items or places a preset names that no longer
        ///     exist are skipped on load rather than breaking anything.
        /// </summary>
        private void BuildPresetRows(MBBindingList<ViewModel> rows)
        {
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Preset_Save}Save Current Setup").ToString(),
                () => new TextObject("{=CSR_Preset_Save_Value}Name a New Preset").ToString(),
                () => EditorPopups.ShowPrompt(
                    new TextObject("{=CSR_Preset_Save}Save Current Setup").ToString(),
                    new TextObject("{=CSR_Preset_Save_Desc}Name this preset.").ToString(),
                    null,
                    new TextObject("{=CSR_CustomAmount_Confirm}Set").ToString(),
                    new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                    input =>
                    {
                        var name = StartPresetService.SanitizeName(input ?? string.Empty);
                        // A name of nothing but characters a filename cannot hold sanitizes away
                        // to nothing; saying so beats a button that appears to do nothing.
                        if (name.Length == 0)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=CSR_Preset_SaveFailed}The preset could not be saved.")
                                    .ToString()));
                            return;
                        }
                        InformationManager.DisplayMessage(new InformationMessage(
                            StartPresetService.Save(name, CreationSession.Current)
                                ? new TextObject("{=CSR_Preset_Saved}Preset saved.").ToString()
                                : new TextObject("{=CSR_Preset_SaveFailed}The preset could not be saved.")
                                    .ToString()));
                        RebuildCurrentTab();
                    })));

            foreach (var presetName in StartPresetService.ListNames())
            {
                var captured = presetName;
                rows.Add(new StartEditorPickerRowVM(
                    captured,
                    () => new TextObject("{=CSR_Preset_Value}Saved Preset").ToString(),
                    () => EditorPopups.ShowOptions(captured, new List<(string, object?)>
                    {
                        (new TextObject("{=CSR_Preset_Load}Load").ToString(), "load"),
                        (new TextObject("{=CSR_Preset_Overwrite}Overwrite with Current Setup").ToString(),
                            "overwrite"),
                        (new TextObject("{=CSR_Editor_Remove}Remove").ToString(), "delete")
                    }, picked =>
                    {
                        switch (picked as string)
                        {
                            case "load":
                                if (StartPresetService.Load(captured, CreationSession.Current))
                                {
                                    RegenerateBattlePreview();
                                    StageViewBridge.RefreshGainedProperties();
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        new TextObject("{=CSR_Preset_Loaded}Preset loaded.").ToString()));
                                }
                                else
                                {
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        new TextObject("{=CSR_Preset_LoadFailed}The preset could not be loaded.")
                                            .ToString()));
                                }

                                break;
                            case "overwrite":
                                ConfirmPresetAction(captured,
                                    new TextObject("{=CSR_Preset_Overwrite_Confirm}Overwrite '{NAME}' with the current setup?"),
                                    new TextObject("{=CSR_Preset_Overwrite_Yes}Overwrite").ToString(),
                                    () => InformationManager.DisplayMessage(new InformationMessage(
                                        StartPresetService.Save(captured, CreationSession.Current)
                                            ? new TextObject("{=CSR_Preset_Saved}Preset saved.").ToString()
                                            : new TextObject("{=CSR_Preset_SaveFailed}The preset could not be saved.")
                                                .ToString())));
                                break;
                            case "delete":
                                ConfirmPresetAction(captured,
                                    new TextObject("{=CSR_Preset_Delete_Confirm}Delete the preset '{NAME}'? This cannot be undone."),
                                    new TextObject("{=CSR_Editor_Remove}Remove").ToString(),
                                    () => StartPresetService.Delete(captured));
                                break;
                        }

                        RebuildCurrentTab();
                    })));
            }
        }

        private void ConfirmPresetAction(string presetName, TextObject question, string affirmative,
            Action onConfirm)
        {
            question.SetTextVariable("NAME", presetName);
            EditorPopups.ShowConfirm(
                presetName,
                question.ToString(),
                affirmative,
                new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                () =>
                {
                    onConfirm();
                    RebuildCurrentTab();
                },
                destructive: true);
        }

        /// <summary>
        ///     One family member's whole setup on one page: every row is a single
        ///     direct control, and rows that would contradict the member's state
        ///     (a dead parent at a holding, a married-away sister in the party, a
        ///     child with a war kit) simply do not exist.
        /// </summary>
        private void BuildMemberEditRows(MBBindingList<ViewModel> rows, FamilyMemberSpec member, string title)
        {
            var session = CreationSession.Current;
            bool isParent = member.Relation is FamilyRelation.Father or FamilyRelation.Mother;
            bool isKin = member.Relation is FamilyRelation.Brother or FamilyRelation.Sister
                or FamilyRelation.Son or FamilyRelation.Daughter;
            bool isFemaleKin = member.Relation is FamilyRelation.Sister or FamilyRelation.Daughter;
            bool isAdult = FamilyAges.MemberAge(session, member) >= GameCaps.MinAdultAge();

            rows.Add(BackRow(title, ExitMemberEdit));

            // One click flips it
            rows.Add(new StartEditorPickerRowVM(
                new TextObject("{=CSR_Editor_Status}Status").ToString(),
                () => member.IsAlive
                    ? new TextObject("{=CSR_Editor_Alive}Alive").ToString()
                    : new TextObject("{=CSR_Editor_Dead}Dead").ToString(),
                () =>
                {
                    member.IsAlive = !member.IsAlive;
                    RebuildCurrentTab();
                }));

            bool isSibling = member.Relation is FamilyRelation.Brother or FamilyRelation.Sister;
            if (isSibling)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Aging}Aging").ToString(),
                    () => AgingValueText(session, member),
                    () =>
                    {
                        var options = new List<(string, object?)>
                        {
                            (new TextObject("{=CSR_Editor_AgingSmart}Smart").ToString(),
                                SiblingAging.Smart),
                            (BirthGroupText(session, member), SiblingAging.Twin),
                            (new TextObject("{=CSR_Editor_AgingExact}Exact Age").ToString(),
                                SiblingAging.Exact)
                        };
                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Aging}Aging").ToString(), options, picked =>
                            {
                                if (picked is not SiblingAging aging) return;
                                member.Aging = aging;
                                member.SmartOffset = null;
                                if (aging == SiblingAging.Exact)
                                    member.Age ??= FamilyAges.MemberAge(session, member);
                                else
                                    // A leftover exact number must not shadow
                                    // the mode's own age anywhere it is shown
                                    member.Age = null;
                                RebuildCurrentTab();
                            });
                    }));
            }

            // Parents' ages derive from the whole brood; everyone else steps freely
            if (!isParent)
            {
                var (minAge, maxAge) = FamilyAges.AgeBounds(session, member);
                rows.Add(new StartEditorRowVM(
                    new TextObject("{=CSR_Editor_Age}Age").ToString(), minAge, maxAge, 1,
                    () => FamilyAges.MemberAge(session, member),
                    v =>
                    {
                        member.Age = v;
                        if (isSibling) member.Aging = SiblingAging.Exact;
                        // Crossing adulthood changes which rows exist
                        RebuildCurrentTab();
                    }));
            }

            if (member.IsAlive && isKin && isAdult)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Marriage}Marriage").ToString(),
                    () => member.Marriage switch
                    {
                        FamilyMarriage.MarriedInClan =>
                            new TextObject("{=CSR_Editor_MarriedInClan}Married, Stays in the Clan").ToString(),
                        FamilyMarriage.MarriedAway =>
                            new TextObject("{=CSR_Editor_MarriedAway}Married into Another Clan").ToString(),
                        _ => new TextObject("{=CSR_Editor_Single}Unmarried").ToString()
                    },
                    () =>
                    {
                        var options = new List<(string, object?)>
                        {
                            (new TextObject("{=CSR_Editor_Single}Unmarried").ToString(),
                                FamilyMarriage.Single),
                            (new TextObject("{=CSR_Editor_MarriedInClan}Married, Stays in the Clan").ToString(),
                                FamilyMarriage.MarriedInClan)
                        };
                        // The game's marriage rules never move a man to his
                        // bride's clan, so only women can marry out
                        if (isFemaleKin)
                            options.Add((new TextObject("{=CSR_Editor_MarriedAway}Married into Another Clan")
                                .ToString(), FamilyMarriage.MarriedAway));

                        EditorPopups.ShowOptions(
                            new TextObject("{=CSR_Editor_Marriage}Marriage").ToString(), options, picked =>
                            {
                                if (picked is not FamilyMarriage marriage) return;
                                member.Marriage = marriage;
                                // Someone who leaves the clan cannot hold a place in it
                                if (marriage == FamilyMarriage.MarriedAway)
                                    member.StaysInSettlement = false;
                                RebuildCurrentTab();
                            });
                    }));
            }

            // The dead hold nothing, the married-away belong to another clan,
            // and children always settle; only living adult clan members choose
            if (member.IsAlive && isAdult && member.Marriage != FamilyMarriage.MarriedAway)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Placement}Placement").ToString(),
                    () => member.StaysInSettlement
                        ? new TextObject("{=CSR_Editor_AtHolding}Stay at Your Holding").ToString()
                        : new TextObject("{=CSR_Editor_InParty}Ride in Your Party").ToString(),
                    () =>
                    {
                        member.StaysInSettlement = !member.StaysInSettlement;
                        RebuildCurrentTab();
                    }));
            }

            if (member.IsAlive && isAdult)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_HeroEdit}Edit Character").ToString(),
                    () => HeroSpecSummary(member.Advanced),
                    () => EnterHeroEdit(member.Advanced, title)));
            }

            if (!isParent)
            {
                rows.Add(new StartEditorPickerRowVM(
                    new TextObject("{=CSR_Editor_Remove}Remove").ToString(),
                    () => new TextObject("{=CSR_Editor_Remove_Value}Leave the Family").ToString(),
                    () => EditorPopups.ShowConfirm(
                        title,
                        new TextObject("{=CSR_Editor_RemoveMember_Confirm}Remove this family member? Their whole setup goes with them.")
                            .ToString(),
                        new TextObject("{=CSR_Editor_Remove}Remove").ToString(),
                        new TextObject("{=CSR_Gear_Picker_Cancel}Never Mind").ToString(),
                        () =>
                        {
                            session.FamilyMembers.Remove(member);
                            ExitMemberEdit();
                        },
                        destructive: true),
                    isDestructive: true));
            }
        }

        private static string AgingValueText(CharacterCreationSession session, FamilyMemberSpec member)
        {
            int age = FamilyAges.MemberAge(session, member);
            string mode = member.Aging switch
            {
                SiblingAging.Twin => BirthGroupText(session, member),
                SiblingAging.Exact => new TextObject("{=CSR_Editor_AgingExact}Exact Age").ToString(),
                _ => new TextObject("{=CSR_Editor_AgingSmart}Smart").ToString()
            };
            return $"{mode} ({age})";
        }

        /// <summary>Twin grows into Triplet and beyond as more siblings share the player's birth.</summary>
        private static string BirthGroupText(CharacterCreationSession session, FamilyMemberSpec member)
        {
            int bornTogether = 1 + session.FamilyMembers.Count(m =>
                m.Relation is FamilyRelation.Brother or FamilyRelation.Sister &&
                m.Aging == SiblingAging.Twin);
            if (member.Aging != SiblingAging.Twin)
                bornTogether++;

            return bornTogether switch
            {
                <= 2 => new TextObject("{=CSR_Editor_Twin}Twin").ToString(),
                3 => new TextObject("{=CSR_Editor_Triplet}Triplet").ToString(),
                4 => new TextObject("{=CSR_Editor_Quadruplet}Quadruplet").ToString(),
                5 => new TextObject("{=CSR_Editor_Quintuplet}Quintuplet").ToString(),
                _ => new TextObject("{=CSR_Editor_Sextuplet}Sextuplet").ToString()
            };
        }

        private static string MemberValueText(FamilyMemberSpec member, string aliveText, string deadText)
        {
            var parts = new List<string> { member.IsAlive ? aliveText : deadText };
            // The effective age, not the stored number: a sibling on Smart or
            // Twin aging ignores any exact age left behind by an earlier mode
            bool isSibling = member.Relation is FamilyRelation.Brother or FamilyRelation.Sister;
            if (isSibling || member.Age.HasValue)
                parts.Add(FamilyAges.MemberAge(CreationSession.Current, member).ToString());
            if (member.Marriage == FamilyMarriage.MarriedInClan)
                parts.Add(new TextObject("{=CSR_Editor_MarriedShort}Married").ToString());
            else if (member.Marriage == FamilyMarriage.MarriedAway)
                parts.Add(new TextObject("{=CSR_Editor_MarriedAwayShort}Married Away").ToString());
            if (member.IsAlive && member.StaysInSettlement)
                parts.Add(new TextObject("{=CSR_Editor_AtHoldingShort}At Holding").ToString());
            return string.Join(", ", parts);
        }

        #endregion

        // What the game's own character creation puts every attribute at before a
        // point is spent, used only when there is no hero to read it from
        private const int BaseAttribute = 2;

        /// <summary>
        ///     Where an attribute stands before this editor changes anything. The
        ///     live hero is the authority, because the apply pipeline measures its
        ///     own room from exactly that; the constant is the answer when no hero
        ///     exists to ask.
        /// </summary>
        private static int StartingAttribute(CharacterAttribute attribute)
        {
            try
            {
                return Hero.MainHero?.GetAttributeValue(attribute) ?? BaseAttribute;
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"StartEditorVM: {attribute.StringId} would not say where it stands " +
                              $"({ex.GetType().Name}); the editor reads it as {BaseAttribute}.");
                return BaseAttribute;
            }
        }

        /// <summary>
        ///     Where the attribute stands, plus what a guided life will add. A
        ///     guided run names no attribute anywhere, so it reads what the apply
        ///     pipeline will do with the points the hero is actually holding.
        /// </summary>
        private static int EstimateAttribute(CharacterAttribute attribute)
        {
            var session = CreationSession.Current;

            // Cultured Start names an attribute on each answer: the base two, plus
            // one point for every answered chapter that names this one
            if (session.Mode == SetupMode.LifePath)
            {
                int named = 2;
                foreach (var menu in CharacterCreation.Catalog.LifePathCatalog.BuildMenus())
                foreach (var choice in menu.Choices)
                    if (choice.IsSelected(session) && choice.Attribute() == attribute)
                        named++;
                return Math.Min(GameCaps.MaxAttribute(), named);
            }

            int total = StartingAttribute(attribute);

            if (GuidedRun.WasWalked)
            {
                GuidedAttributePlan(session).TryGetValue(attribute, out int granted);
                total += granted;
            }

            return Math.Min(GameCaps.MaxAttribute(), total);
        }

        /// <summary>
        ///     The focus the life bought. A guided run names no skill, so its share
        ///     is whatever the apply pipeline will make of the focus points the hero
        ///     is holding, asked of the pipeline rather than worked out a second
        ///     time here.
        /// </summary>
        private static int EstimateFocus(SkillObject skill)
        {
            if (CreationSession.Current.Mode == SetupMode.LifePath)
            {
                CharacterCreation.Catalog.LifePathCatalog.GetFocusTotals(CreationSession.Current)
                    .TryGetValue(skill, out int points);
                return Math.Min(GameCaps.MaxFocus(), points);
            }

            if (!GuidedRun.WasWalked) return 0;

            GuidedFocusPlan(CreationSession.Current).TryGetValue(skill, out int focus);
            return Math.Min(GameCaps.MaxFocus(), focus);
        }

        /// <summary>
        ///     What the apply pipeline will do with a guided life's attribute
        ///     points: its own planner, over the budget it will actually spend and
        ///     the values it will measure its room from, so the estimate and the
        ///     grant are one computation rather than two that agree today.
        ///
        ///     The budget, never the hero's unspent pool: the guided route fills
        ///     no pool, because its menus hand the game no allocation of their
        ///     own, so reading the pool showed a flat sheet while the start went
        ///     on building somebody else.
        /// </summary>
        private static Dictionary<CharacterAttribute, int> GuidedAttributePlan(CharacterCreationSession session)
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero?.HeroDeveloper == null) return new Dictionary<CharacterAttribute, int>();

                return NarrativeStep.PlanAttributePoints(
                    LifeProfile.From(session),
                    NarrativeStep.AttributeBudgetFor(hero, StorySkills.LevelFor(session)),
                    StartingAttribute,
                    GameCaps.MaxAttribute());
            }
            catch (Exception ex)
            {
                CSLogger.Warn("StartEditorVM: the guided run's attribute estimate could not be read " +
                              $"({ex.GetType().Name}: {ex.Message}); the base stands.");
                return new Dictionary<CharacterAttribute, int>();
            }
        }

        /// <summary>
        ///     What the apply pipeline will do with a guided life's focus points,
        ///     asked of the pipeline itself over the budget it will actually spend.
        /// </summary>
        private static Dictionary<SkillObject, int> GuidedFocusPlan(CharacterCreationSession session)
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero?.HeroDeveloper == null) return new Dictionary<SkillObject, int>();

                return NarrativeStep.PlanFocusPoints(
                    session,
                    NarrativeStep.FocusBudgetFor(hero, StorySkills.LevelFor(session)),
                    hero.HeroDeveloper.GetFocus,
                    GameCaps.MaxFocus());
            }
            catch (Exception ex)
            {
                CSLogger.Warn("StartEditorVM: the guided run's focus estimate could not be read " +
                              $"({ex.GetType().Name}: {ex.Message}); the skill reads as unfocused.");
                return new Dictionary<SkillObject, int>();
            }
        }
    }
}
