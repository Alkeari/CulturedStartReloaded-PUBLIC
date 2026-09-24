# Changelog

What changed in Cultured Start Reloaded, newest first. Only changes you can actually notice are listed:
internal and build work is deliberately left out. This file is the source for the release
notes published on the Nexus Mods page and the change note attached to the Steam Workshop item.

Entries begin on 2026-08-14, when this file was created. This repository's earlier history
was intentionally cleared, so there is nothing before that to record.

## v4.0.2 - 2026-09-23

- Added: Both guided routes ask which calling you begin in, and the Start Editor gains a Career tab with the board picks your starting level allows.
- Added: Your faction's own store, such as Gondor's Castar, is part of a start: the means you choose stock it and the Start Editor sets it exactly.
- Added: The gear tabs can dress you in the conversion's own background and career outfits, and the Companions tab can call its named characters into your clan.
- Changed: Cultured Start TAOM runs with Tales from the Age of Men v2 on Bannerlord v1.4.8 and carries the main download's full feature set, using the Harmony, ButterLib and MCM that conversion bundles.
- Changed: The conversion's own items count as the game's content in the gear pickers, and the realms a start may serve are those on your own side of the world.
- Changed: Picking a hero on the conversion's hero list still offers the route choice, and Vanilla Start then skips the backstory questions as that conversion does.
- Changed: Parents, siblings and children generated for you are of your own race, and a named character of the conversion keeps the race it gives them.
- Changed: An automatic child's age is drawn from the whole band the couple could have parented instead of always its oldest year.
- Fixed: Children, siblings, companions and generated nobles are the age they were given; their birthdays were measured against a 365-day year, so a two-year-old could arrive fourteen.
- Fixed: No character is given a saddle or barding cut for another kind of animal, and a mount the game has no barding for is chosen last.
- Fixed: Career and rank names read as their own words, and the career board no longer offers the pick the career already grants, so choosing thirty picks gives thirty.
- Removed: Cultured Start TAOM no longer supports the older Tales from the Age of Men on Bannerlord v1.2.12.
## v4.0.1 - 2026-09-15

- Fixed: In the Cultured Start TAOM download, the Start Editor's navigation rail and every other list in the mod's screens read top to bottom instead of upside down.

## v4.0.0 - 2026-09-14

- Added: Cultured Start Revamped, a new way to begin, tells your life in thirteen scenes, lets your answers decide which start you arrive at, and lets you make yourself younger or older before you set out.
- Added: Every Revamped option states in a panel beside you exactly what it does to your character and names any start it closes off.
- Changed: The route choice offers Vanilla, the Cultured Start you already know with its chapters and start type pick unchanged, Revamped and the Start Editor, in the game's own Advanced Starting Options from v1.5.0.
- Added: The Start Editor can seat you on the throne of a realm already on the map, which then flies your banner in your colors, sets each companion's name, gender, culture, attributes, focus, skill levels, perks and traits, shows every skill's perks on one board laid out like the game's own perk screen, and sets attributes, focus and traits by clicking marks, with skills grouped under their attributes.
- Added: The 3D stage shows your battle, civilian and stealth outfits side by side, already framed, and can pan as well as rotate and zoom, and every list, question and name prompt the mod opens now shares the Start Editor's dark and gold look, with realms and places listed alongside who holds them.
- Added: Cultured Start RoT and Cultured Start TAOM downloads bring the mod to the Realm of Thrones and Tales from the Age of Men total conversions.
- Added: A Creation Menus section in the settings carries a switch for each optional chapter of the guided starts.
- Changed: The mod runs on Bannerlord v1.3.4 through v1.5.2.
- Fixed: You Ride Alone and An Empty Wagon now give you nothing, and the provisions answer that only declines food is titled An Empty Larder.
- Fixed: A companion set in the Start Editor no longer keeps the level, skills and perks of the wanderer it came from, and an Automatic realm now gives a mercenary, vassal or outlaw a realm.
- Fixed: A start now opens at the holding it granted with a garrison sized for that fief, and a founding no longer erases another realm or takes castles from across a sea.
- Fixed: Backing out of a route's first screen no longer drops you into a different route, and the relatives and friends your start created are met in the encyclopedia.

## v3.28.2 - 2026-09-08

- Changed: Every multi-select popup in the Start Editor now reads the same way: select nothing and
  you get the default, select something and you get what you selected. No option locks the others
  any more, so choosing a class, a culture, a realm or a policy takes one click instead of first
  clearing whatever was selected for you.
- Removed: The rows that only restated the default (All Classes, All Cultures, Founding Default,
  Current Wars, Selected Realm Only, and None for policies). Selecting nothing says the same thing,
  and the row on the tab still names what you have.
- Added: The mod's log now records every bound it offers you and the values it was computed from,
  along with the full set of choices before a start is applied, so a number that looks wrong can be
  explained from the log.

## v3.28.1 - 2026-09-08

- Fixed: On game v1.5.0 and newer, Cultured Start and Start Editor were missing from the game's own
  Player Start list, so the only way into the mod was its first menu. They are added again.
- Fixed: A life-path chapter you never answered still gave you its first option. Most visibly, every
  character could pick up Valor from the turning point without choosing it, so a trait appeared that
  you never picked and the traits you did pick looked wrong.
- Fixed: The same fault quietly handed out the first option's skills, focus, attribute, heirloom and
  goodwill for any chapter left unanswered.
- Fixed: The character creation screen no longer risks an error every frame while the Start Editor is
  open; if one happens it is reported once and the screen keeps working.
- Fixed: Naming a preset with characters a filename cannot hold did nothing at all and said nothing;
  the Start Editor now tells you the preset was not saved.
- Fixed: On game v1.5.0 and newer, starting a Story Mode campaign ran the tutorial skip ten times and
  left the map's parties and settlements without their first visibility pass.
- Fixed: Turning all eight starts off in the settings was supposed to bring them all back, and instead
  left the scenario screen and the editor's start type list empty with no way forward.

## v3.28.0 - 2026-09-08

- Added: Both downloads now run on every supported game version, v1.3.15 through v1.5.2. They differ
  only in which game channel they are built and verified against, so picking the wrong one can no
  longer leave you with a mod that loads but does not work.
- Fixed: Installing the BETA download on game v1.4.8 or older gave a mod that loaded and then broke,
  because it was built against APIs those games do not have. Everything that differs between game
  versions is now looked up while the game runs instead of being fixed at build time.
- Fixed: Opening the Start Editor on an older game with the BETA download drew an unstyled,
  text-only screen that could not be used.
- Fixed: A single patch that could not be applied stopped every other patch from being applied with
  it, which left the mod loaded but doing nothing. Each patch is now applied on its own.
- Changed: The game's Advanced Starting Options entries this mod adds appear only where that screen
  exists, and are skipped in silence everywhere else.
- Fixed: The Start Editor could offer one more companion than the game then granted. Where a perk
  has an alternative, the game picks between them at random, and the editor and the start were
  asking separately and getting different answers; the choice is now made once and kept.

## v3.27.0 - 2026-09-07

- Added: A separate BETA download for the game's Beta branch v1.5.2, alongside the existing v1.3.15 through v1.4.8 file.
- Fixed: Founding a kingdom, renaming it, and clearing the level-up points the game grants after character creation all work on game v1.5.x.
- Added: On game v1.5.x, Cultured Start and the Start Editor appear as Player Start choices on the game's own Advanced Starting Options screen, and choosing one begins that route straight away.
- Fixed: On game v1.5.x in Sandbox, the game no longer lays its own starting package over the one this mod already gave you.
- Added: The Stealth outfit is offered to every player and put together automatically at your tier and culture, for you and for your companions and family.
- Fixed: The Stealth gear lists were empty without the War Sails DLC and sorted lowest tier first; civilian-legal pieces now qualify and every list reads best first.
- Fixed: Setting Custom Renown high now raises everything it should, from troop counts and companions to workshops and the gear your standing affords.
- Added: A chapter of its own, Those Who Ride With You, for the size of your warband; it follows your household and companions, so it offers only the seats your party really has.
- Changed: Questions come in the order they depend on each other: renown before the figures it bounds, and in the Start Editor, Standing above Holdings and Property.
- Fixed: Set All Slots then None on the Battle Gear tab now empties the four weapon slots and the banner as well as the armor.
- Fixed: Banners no longer appear among the choices in the weapon slot pickers.
- Fixed: The Start Editor could offer one more companion than the clan would hold; the start now grants only what there is a seat for.

## v3.26.0 - 2026-09-04

- Changed: The Start Editor, the item, option and perk pickers and the character stage controls are drawn in the Alkeari palette: black scrim, graphite panels with a hairline edge, gold titles and champagne labels.
- Changed: Buttons, list rows and scrollbars react on hover and press in the new palette, and the confirming button on each screen is the only gold-filled one.
- Changed: The active tab in the Start Editor is marked by a gold bar rather than only a change of fill.

## v3.25.0 - 2026-08-28

- Added: Support for game version v1.3.15. The supported range is now v1.3.15 through v1.4.8,
  verified by real compilation and API checks against each game version rather than assumption.
- Changed: The Nexus Mods download is now the file group named simply "Cultured Start Reloaded"
  (previously "Cultured Start Reloaded - v1.4.X"); the group always carries the newest release
  for the game's current Stable version range.
- Note: The game's Beta branch (v1.5.x) is not yet supported; Bannerlord changed several
  APIs there (kingdom creation and renaming among them) that this mod uses.

## v3.24.1 - 2026-08-26

- Fixed: The v3.24.0 rewrite had narrowed supported game versions to v1.4.8 only; a reference-assembly
  API diff against v1.4.0 found no dependency on anything newer, so support is restored back to v1.4.0-v1.4.8.

## v3.24.0 - 2026-08-25

### Before You Update
- Changed: This release targets Bannerlord v1.4.8 only; stay on v2.0.x for older game versions.
- Changed: Start type ranges were retuned and some settings no longer exist, so look over the mod's settings once after updating.

### Three Ways to Begin
- Added: Character creation now opens on a choice of three routes: Vanilla Start, Cultured Start, and the Start Editor.
- Changed: Vanilla Start walks the game's own narrative menus exactly as if this mod were not installed.

### The Start Editor
- Added: A full-control editor for your start, with tabs for your path and realm, attributes, skill focus, skill levels, perks, traits, gear, family, companions and resources.
- Added: An armory picker for every gear slot, with a search box, culture and class filters, tier toggles and item counts per option.
- Added: A perk picker per skill that preselects your current choices and respects the game's one-per-pair rule.
- Added: Named presets that save, load and overwrite a complete setup.
- Added: Per-hero editing for every companion and relative: role, level, exact skills and exact gear.
- Added: A 3D preview you can rotate, zoom, drag by the character and reset, showing your gear as you choose it.
- Added: Bulk editing, so you can pick any set of attributes, skill focuses or traits and set one value for all of them.
- Added: A Set All Slots row on each gear tab, and an Automatic button on any row you have customized.
- Added: Exact figures for gold, influence, troops, renown and level, with every bound read from your running game.
- Changed: The editor is transactional: it takes a snapshot when it opens, and closing restores exactly what you had.
- Changed: Destructive actions ask first and name what they will do, and Reset touches only what is on screen.

### The Cultured Start
- Added: A turning point in your past: nine defining moments that shape who you became.
- Added: Scenario chapters after you choose your station, each start type telling its own story in its own terms.
- Added: An epilogue, Your Story So Far, that reads your composed life back to you before you ride out.
- Added: A household choice of five complete families, from alone in the world to a wide clan.
- Added: A provisions choice for what your wagons carry, from a sensible larder to empty wagons.
- Added: Six culture-specific warrior idioms, one for each of the game's cultures.
- Changed: Clan tier, gold, influence and troops are now one written chapter, What You Carry, which states in plain numbers what each choice gives you.
- Changed: The four weapon screens are now one choice, How You Fight, offering seven complete loadouts that always include their ammunition.
- Changed: Life path choices leave visible traces: personality leanings, keepsakes, and the goodwill of the people you grew up among.
- Changed: The campaign now opens on your own story rather than on the mod announcing itself.

### Start Types
- Added: The Outlaw start, where a realm wants your head and you keep a band of your own.
- Added: The Caravan Master start, with pack animals, trade goods, guards and workshops you can already own.
- Added: The Rebel Clan start, a castle held in open rebellion with fellow rebel houses at your side.
- Added: Deep setup for every non-monarch start, including the wars your realm is already fighting, your contract pay, your garrison and your holdings.
- Changed: A Landed Vassal start now costs ten relation with the clan that lost the fief to you, and the Mercenary start's influence works the way the game intends.

### Kingdoms and Realms
- Added: Monarch kingdom setup: how many vassal clans are founded, which policies they enact, and which realms you begin at war with.
- Added: Name your kingdom during character creation, on its own page after the founding choice.
- Added: Founding stories, so you can settle empty land quietly or press a claim and take it from the realm that held it.

### Gear and Equipment
- Added: Exact item choice for every battle armor slot, mount and harness, plus separate civilian and stealth outfits and your banner.
- Added: An intelligent quartermaster that arms the slots you leave to suit your character, culture and standing.
- Added: A None option on every slot, so anything can be left deliberately empty.
- Changed: Weapon selection is explicit about class: spears and lances are different things, and shields present as one class unless a mod separates them.
- Changed: A civilian outfit is generated to match your station, so you no longer walk into town in armor.
- Changed: Item tiers now read the way the game shows them, 0 to 6.

### Family and Companions
- Added: Compose your family in full: how many relatives, their ages, whether they live, and whether they marry within the clan or away from it.
- Changed: Companions, relatives and vassal lords arrive as believable people scaled to your standing, with coherent skills, perks and loadouts.
- Changed: Adult relatives ride with you by default, and every family member is a known face from the first moment.
- Changed: Family members start on friendly terms with you and companions above neutral, instead of whatever their traits happened to roll.
- Changed: Starting troops are drawn from what your culture's settlements actually recruit.

### Settings
- Changed: The settings page was rebuilt, with each start type nested under one Start Types heading in the order the game offers them.
- Changed: Labels and hints now say what each setting does, which of the mod's routes it affects, and that the bands are rungs rather than caps.
- Added: A clan tier ceiling to go with the floor, so a humble start can be kept humble.
- Removed: Settings that could not change anything, including sixteen starting level values that always produced the same result.

### Fixed
- Fixed: Life path skill and attribute bonuses were applied inconsistently, so the same choices could produce different characters.
- Fixed: Focus and attribute points were left unspent when the game began.
- Fixed: Mothers, sisters and spouses could fail to generate at all.
- Fixed: Relatives ignored the age you asked for, and female relatives carried wanderer epithets like "the Dispossessed".
- Fixed: Children of the clan could inherit a stranger's epithet in their name.
- Fixed: Generated companions, relatives and vassals all wore the same outfit as one another.
- Fixed: Relatives below the game's coming of age were handed adult weapons, armor and a soldier's skills.
- Fixed: Starting companions were built from multiplayer templates and could not govern, lead parties or be left in a settlement.
- Fixed: Starting renown did not match the clan tier you chose, and "Let Fate Decide" always picked the same town.
- Fixed: The Monarch start ignored its Additional Castles setting, and the Enable Mod and per start type toggles did nothing.
- Fixed: The Naval DLC is now detected by its exact module id, so other mods no longer trigger its content.
- Fixed: The stray mule and grain the game seeds after creation are gone, and items that ship packed rather than loose, including most food and the War Sails armor, now appear in the pickers.
