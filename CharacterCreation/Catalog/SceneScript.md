# Scene Script

The guided route's thirteen scenes, written as content for a later pass to turn into catalog code. This file replaces
the questionnaire (Your Family, Your Childhood, Your Education, Idioms, Your Early Adulthood, The Turning Point,
Reason for Adventuring, Your Age) with scenes. Nothing here is asked of the player as a form. Everything here is
inferred from what they say about a life.

## The four forms

A run that asks the same shape of question thirteen times is a questionnaire with better prose on it. Every scene
takes one of four forms, and the set is closed: a scene that is none of them is a defect, and two scenes of the same
form that read as one menu reworded are the same defect.

| Form | What the scene does | What the player is choosing | Scenes | Count |
|---|---|---|---|---|
| C | A judgment about other people: the scene names a person or a room and asks who was in the wrong | A side | 2, 5, 8, 10 | 4 |
| E | What was said about you, overheard, by people who did not know you could hear | Somebody else's verdict on them | 4, 7, 9, 13 | 4 |
| A | A plain question with no fiction around it | A fact about their life | 1, 6, 12 | 3 |
| D | An object that came out of something and is still in their kit | A thing | 3, 11 | 2 |

C and E carry the run. A is the change of pace, and states plainly what the fiction elsewhere leaves implied. D is
used only where the scene is genuinely about objects, and both D scenes exist to put something in the player's hands
that no other scene would have produced: the fire decides what came out of the household, the windfall decides what
was bought with the one sum they ever held at once.

Each C scene judges a different kind of person, so the four cannot blur into each other: family (2), the man who made
you (5), power (8), a patron (10). Each E scene has a different room talking: the village (4), the men you served
with (7), the town you worked in (9), strangers (13).

## How to read this

Each scene carries an id, its form letter, the information targets it serves, a severity, a title, a prompt of two
or three sentences, and a stage direction. Each option carries an id, a title, prose shown on selection, one plain line on what
it reveals, and its consequences.

A prompt or a piece of prose may be written more than once, for lives a named state holds of:

```
**Prompt when `the-life-had-no-house`:** ...
**Prose when `the-life-buried-the-father`:** ...
```

The label in backticks names the state, the catalog carries the predicate that reads it, and the first label whose
state holds wins; a life no label holds of reads the plain `**Prompt:**` or `**Prose:**` above them. The labels and
their order are transcribed into `SceneCatalog.cs` and `SceneCatalogTests` compares the two lists, so a variant added
on one side and not the other fails the run.

Seven targets are covered across the thirteen:

| Target | What it decides | Scenes |
|---|---|---|
| Origins | Social access, who already knows your name, starting means | 1, 2, 3 |
| Inclination | What they reach for by preference | 2, 3, 4 |
| Schooling | Which circles they can move in | 4, 5 |
| Mastery | What they actually got good at, which outweighs inclination | 6, 7 |
| Hinge | The change in how they see the world | 8, 9 |
| Intent | Why they are doing any of this | 9, 10, 11 |
| Seasoning | Specialist against generalist, and how long it took | 12, 13 |

Station and age are never on a button and never in a prompt. Station falls out of scenes 1, 5, 10 and 13. Age falls
out of accumulated `LostYears` and out of scenes 12 and 13. Ages appear only in stage directions, which the player
does not read. The age a stage direction opens with is the age the 3D render draws the character at: it is carried on
`Scene.Age`, and `SceneCatalogTests` reads the word back out of the direction and fails the run when the two differ,
so a scene cannot be written for a child of ten and drawn at seventeen. The last scene names no year because it
happens at whatever age the life has reached, and it is the only one that may.

### Rules the writing keeps

- No option states a number to the player, names a stat, or names a station. Narrative amounts stay narrative, and
  the exact effect of an option is shown beside it by machinery that derives it from the consequences listed here.
- Prose is second person past tense, plain.
- Every scene has to earn its place. If a scene could be cut and the character would come out the same, it is a bad
  scene, and the fix is to replace it rather than to soften it.
- Nothing is steered. No option is the correct answer to its scene, and no kind of character has only one road to it:
  a mounted fighter can arrive through the fire, through what they could do, through what the yard said, or through
  what they bought, and a reader of hands can arrive through the house they came from, the man who taught them, or
  the work they ended up named for.
- The run keeps the order of a life, because a life told out of order reads as broken rather than as artful. What was
  thrown out is the questionnaire, not the chronology.
- Options within a scene are equal in weight to each other. Scenes differ from each other, and the severity line on
  each scene says how much. Scene 12 varies its `LostYears` on purpose, one answer down to none: how many seasons
  went by is the thing that scene exists to learn, and a winter that took none of them has to cost something else.
- No scene charges the same cost on every one of its answers. A debt, an enemy, a lost year and a shut road only
  ever take, so a scene that puts one of those on all of its answers is not asking about it, it is levying it: the
  player picks how much and never whether. Scene 12 charged `LostYears` six times out of six, which is what made a
  character who wasted nothing impossible to finish young. A trait is exempt and always will be, because its target
  and its sign vary between the answers, so what it does is still the player's to choose.
- The severity line is not a label, it is a claim, so `Scene.Severity` carries it and `SceneCatalogTests` weighs the
  consequences and fails the run when a Defining scene settles no more of a character than a Formative one. Write the
  severity the scene deserves and then give it the consequences that earn the word.
- The title is the scene's own, written here and carried on `Scene.Title`, never a name looked up by the scene's id.
  The ninth scene is two different scenes under one id, and a heading fetched by that id gave a War Sails owner the
  land scene's name over the waterfront. Titles of five words or fewer are Title Case; six or more read as a sentence.
- A trait is spent within what the game can hold. A personality trait runs from minus two to plus two, and the apply
  pipeline clamps to that, so a run that hands out eight steps of valor is promising six of them to nobody. Add up,
  for one trait, the largest movement each scene can give it: that sum is what the run may promise, and it may not
  pass two in either direction. In practice a trait has at most two homes per direction, one step each, and no scene
  moves the same trait the same way on two of its answers. A trait is therefore the scarcest thing an answer can
  carry, which is why it is worth reading and why `LifeProfileScenes` weighs one step against three of anything else.
- An effect line the player has read already says nothing. The panel line is derived from the consequence, so two
  answers carrying the same consequence read identically however differently they are written: the same line may
  stand on at most two answers of one scene and at most an eighth of the answers in the whole catalog, which is what
  stops a player walking thirteen scenes past the same sentence seventeen times.
- The run says whether this character married, and scene 12 is where it says it. Three later chapters ask about the
  house a character came from and the house they have now, and every question none of them answers is answered by
  the life instead: with no scene saying it, the hearth was the one question no life could ever fill, so every spouse
  and every child on this route came from a player overruling their own life. One answer says it, not six, because a
  run where every character is married is as untrue as a run where none is.
- An answer that settles who is in that house carries a `Household` consequence saying so. The household chapters
  used to read those answers back by option id, which no panel could state, so an answer that left a character
  married or an answer that buried both parents did the largest thing in the scene and said nothing about it. The
  declaration is the one copy: the panel prints it and the chapters compose the house out of it.
- A line the life has already contradicted is rewritten rather than withdrawn. An answer whose prose asserts a fact a
  later reading of the life disproves, and a prompt that assumes a house the first scene said there was none of, are
  the same defect: the player is reading a sentence about a character they are not playing. This is what to
  do about it, since taking the answer away is the one thing that is not allowed, so the writing moves instead and the
  answer stays on the table. A variant is read against the answers already given with its own scene held out, because
  the stage records an answer the moment the player lands on it and an answer that read its own scene as settled would
  describe a life the player has not chosen yet.
- A variant states the facts its answer settles in that state, exactly as the plain writing does. A grave is the one
  household fact the prose can quietly omit, so an answer that buries somebody in one state and nobody in another
  says which in the writing for each; that is what `HouseholdDerivationTests` reads, over every variant as well as
  over the plain line.
- No two answers in the run leave the same life behind, and no answer is another answer of its own scene plus
  something. Both are checked rather than read for: the first is a scene that took the player's time and changed
  nothing, and the second is an option there is no state in which to pick.
- The trained weapon is decided in scene 6, which is the scene about what a pair of hands can do. The profile keeps
  the LAST weapon an answer put in them, so a later scene that hands one out quietly overrules it: scene 7 says what
  a yard thought of a man and hands out no weapon but the thing he picked up off a bench, and scene 11 hands out one
  because buying arms is what that answer is. A new weapon anywhere later takes the decision away from scene 6.
- Making is the thinnest thing the run can say about a life: four answers reach it, in scenes 1, 3, 5 and 13, and
  every one of them is behind the character by the time the run is half over. Scenes 6, 9 and 11 each have a place a
  maker's answer belongs in and none of them has one written: the one thing the hands could do, the town's verdict on
  what a man is for, and what the only sum he ever held at once was spent on.
- The stage renders only what the mod can render: the player at any age, the player's parents, siblings, and a mount.
- A stage direction is the moment the scene happens in, and an answer buries somebody either before that moment or
  after it. Scene 1 asks what the house had when the child was small, so the answer that buries the parents buries
  them before the stage's own moment and takes them off it. Scene 2 judges a whole life out of a room the child is
  ten in, so a parent buried by one of its answers is still standing in that room, and taking them out of it would
  be staging a death that answer never put there.

### Consequence vocabulary

Kinds are only those in `Models\ChoiceConsequence.cs`: Debt, Enmity, Goodwill, Ally, Place, Title, LostYears, Item,
Trait, Gate, Household. Traits are only Mercy, Valor, Honor, Generosity, Calculating.

`Goodwill` and `Enmity` targets are held to the four buckets `RelationEffect` already carries: `culture_lords`,
`town_merchants`, `town_gang_leaders`, `village_headmen`. `Item`, `Place`, `Title` and `Ally` targets are written as
plain ids for the code pass to resolve against the game's objects and the mod's own generators; they are descriptions
of the thing wanted, not object ids that exist yet. `Gate` targets are option ids in this file. A place is a yes or a
no and carries no amount, with one exception written as `Place(id, n)`: `the_open_water`, where how much of the life
went out there is the thing the answer is for.

`Household` is the closed set `father_buried`, `mother_buried`, `both_parents_buried`, `both_parents_living`,
`parents_by_then`, `parents_still_standing`, `a_brother`, `a_sister`, `a_sibling`, `a_spouse`, `forebears_buried`, and carries no amount. It says who this answer settles in
the character's own house, which the three household chapters read for every question the player leaves to their
life. A later answer that speaks to the same parent overrules an earlier one, so an answer may say a parent is
living as well as say one is buried, and every one of these is a whole sentence on the panel rather than a noun the
panel frames. One parent is never settled by an answer about the other: `father_buried` says where the father is and
nothing at all about the mother, so a life that buried her earlier does not find her standing again.

`parents_by_then` is the one read off the life rather than carried by the answer. The last scene's `the_old_one` is
about being called old, which stands whatever came before it, so it is never taken off the table and what it settles
moves instead: where the life has already said both parents are living it settles neither, where the life buried one
of them it settles the other, and where the life buried both or never spoke of them it settles both, which is what
the answer always did. The run reads it once, when the answer is given, so the panel states the fact the household
is about to compose.

`parents_still_standing` is the second one read off the life. Scene 11's `you_sent_it_home` sends the coin home to
the people who raised this character, and it used to declare `both_parents_living` flat, which stood a father the
second scene had buried back up on his feet and left that earlier scene quietly untrue. The answer is about where the
money went and stands whatever the life says, so it stays on the table and what it settles moves instead: where
nothing has been said about the parents it says both are living, and where the life has already buried one of them or
both it speaks to neither and the coin goes to whoever is left. Its prose is written four times for the four states,
since an answer that sends money to a living mother may not go on describing a living father.

`a_sibling` is the one written where the answer is about a person it deliberately never names. The household
was talking about somebody else under that roof and the writing keeps who they were back, which is the answer; the
character still gets them. The RUN settles whether that is a brother or a sister and whether they came before or
after, once, when the answer is given, so the panel states the person the player is getting rather than hedging
between two. Nothing is rolled at apply time and nothing here names the kind: an answer rewritten to name it would
be a different answer, and would want `a_brother` or `a_sister` instead.

`forebears_buried` is the one the chapters do not read. The three of them ask about parents, siblings and a hearth,
and a generation above the father is above all three, so `FamilyStep` is what reads it and what it builds is the
father's own dead mother and father, married, in the family tree. It is declared here for the same reason the rest
are: an answer that puts two people in the tree and says nothing beside itself is the defect `Household` exists to
close.

### The run War Sails is loaded for

Thirteen scenes either way. The DLC does not add a scene, it replaces one: a second scene 9 is written below, under
the same id, and the catalog puts whichever of the two the installed game calls for. That is not a saving of length,
it is the only way in: a fourteenth scene has to be named in `CharacterCreation\Flow\CreationFlow.cs` to be reached,
and the stage direction, the menu title and the settings row that switches a scene off are all keyed on the scene id,
so a scene standing in another's place under its own id is one the whole flow already knows how to put.

Nothing naval is written anywhere else. A player without the DLC meets the same thirteen scenes, word for word, that
they met before it existed, and `WarSailsGatingTests` reads the catalog that player gets and fails on a naval word in
it. What a War Sails owner gets that a land life does not is `Place(`the_open_water`, n)`, the only target in the
catalog that scores the sea, and through it the three skills the DLC adds.

---

## Scene 1: `cs_scene_what_the_house_had`

**Form:** A. **Targets:** Origins. **Severity:** Formative.

**Title:** What the House Had

**Prompt:** Before any of the rest of it there was a house, and the house had what it had. Some of what it had you
only understood the worth of much later, and some of it you have never put down. What did your house have, when you
were small?

**Stage:** The player at seven, in the household's own undyed cloth, standing square and facing front with nothing in
their hands. Both parents behind at a distance, neither of them doing anything, and neither of them on that stage at
all under the answer that buried them before this age. No mount.

### `cs_opt_a_name_people_knew`
**Title:** A Name People Knew
**Prose:** There was little coin in it and there had not been for two generations, but the name went in front of you
at every door in the valley and the doors opened on it. Your father's own father and mother are in the ground and the
valley still says the name they kept, and you found out much later what they gave up to keep it worth that.
**Reveals:** Your household's worth was its standing, and you have had the use of that since before you could speak.
**Consequences:** Goodwill(`culture_lords`, 2); Place(`family_seat`); Household(`forebears_buried`)

### `cs_opt_land_and_no_coin`
**Title:** Ground, and Nothing to Spend
**Prose:** There was land and it was yours and there was never anything in the box. You ate what you grew and wore
what you grew, and everything else was a matter of waiting for a season to turn.
**Reveals:** You came from property rather than money, and you learned to wait before you learned anything else.
**Consequences:** Place(`home_village`); Goodwill(`village_headmen`, 1)

### `cs_opt_a_bench_and_a_trade`
**Title:** A bench and what came off it
**Prose:** The living came out of one room and one pair of hands, and everybody under that roof knew what those hands
were doing at any hour of the day. You could tell a good piece from a poor one long before you could make either.
**Reveals:** Your household lived by a trade, and you were raised inside the standards of it.
**Consequences:** Goodwill(`village_headmen`, 2); Place(`home_workshop`); Goodwill(`town_merchants`, 1)

### `cs_opt_debts_in_other_peoples_books`
**Title:** Other people's books, with your name in them
**Prose:** What your house had was owed. It was owed in three places and had been since before you were born, and the
shape of the whole year was decided by which of them was visited when.
**Reveals:** You were raised inside an obligation you did not make and have not finished.
**Consequences:** Debt(null, 600); Goodwill(`town_merchants`, 2)

### `cs_opt_arms_over_the_door`
**Title:** What Hung Over the Door
**Prose:** There was steel on the wall and it was not decoration, and twice in your childhood it came down. Nobody in
that house would have sold it to eat, and everybody knew that without it ever being said aloud.
**Reveals:** Your household kept steel it would never have sold, and the standing that came with it was yours
before you had a say.
**Consequences:** Goodwill(`town_gang_leaders`, 2); Goodwill(`culture_lords`, 1)

### `cs_opt_a_road_and_no_roof`
**Title:** A road, and whatever was on it
**Prose:** There was no house to speak of. There was a cart, a route and the people walking beside it, and your mother
and father went into the ground at the side of that route before you were old enough to take the reins. The rest of
them fed you and kept moving, and you slept where the light ran out, and you knew four towns before you knew one of
them properly.
**Reveals:** You had no fixed place, the road took both the people who made you, and the country between places is the
thing you actually come from.
**Consequences:** Place(`open_country`); Item(`pack_mule`, 1); Goodwill(`town_merchants`, 1); Household(`both_parents_buried`)

---

## Scene 2: `cs_scene_the_one_always_in_the_wrong`

**Form:** C. **Targets:** Origins, Inclination. **Severity:** Formative. **Carries a gate.**

**Title:** The one always in the wrong

**Prompt:** Every house has one and yours was no different. There was somebody under that roof who was always the one
in the wrong, and the household had settled on it long before you were old enough to be asked. Who was it, and were
they?

**Prompt when `the-life-had-no-house`:** Every household has one and yours was no different, and yours had no roof
over it. There was somebody among the people who fed you and kept you moving who was always the one in the wrong,
and they had settled on it long before you were old enough to be asked. Who was it, and were they?

**Stage:** The player at ten, indoors in house clothes, sitting on the floor at the edge of a room. Both parents on
stage mid-argument, facing each other rather than the player. A sibling in the doorway, listening.

### `cs_opt_your_brother_and_he_was`
**Title:** Your Brother, and He Was
**Prose:** He took what he wanted and the house arranged itself around that, and every time it was explained to you
why this once did not count. You stopped believing the explanation earlier than anybody in there realized.
**Reveals:** You were the one keeping the account in a household that had agreed not to keep one.
**Consequences:** Trait(`Calculating`, 1); Trait(`Generosity`, -1); Household(`a_brother`)

### `cs_opt_your_aunt_and_she_was_not`
**Title:** Your aunt, and she was not
**Prose:** It came to her door whatever it was, because it always had, and you were the only one who went and sat
with her afterward. She taught you things nobody else under that roof knew, out of nothing but the company.
**Prose when `the-life-had-no-house`:** It came to her whatever it was, because it always had, and you were the only
one who went and sat with her afterward. She taught you things nobody else on that road knew, out of nothing but the
company.
**Reveals:** You went to the one everyone else had written off, and you were paid for it in knowledge.
**Consequences:** Ally(`the_one_nobody_sat_with`); Goodwill(`village_headmen`, 1)

### `cs_opt_your_father_and_he_was`
**Title:** Your Father, and He Was
**Prose:** He was wrong about most of it, and being told so made it worse, and what was handed to him was smaller
when it left his hands than when it came into them. He is in the ground now, and so are the father and mother who
handed it to him, and none of that has made him right about any of it. You have never since assumed a man is right
because he is the one standing in front of you.
**Reveals:** You learned what a run of bad decisions costs by living inside one.
**Consequences:** Debt(null, 400); Place(`family_seat`); Household(`father_buried`); Household(`forebears_buried`)

### `cs_opt_your_mother_and_she_was_not`
**Title:** Your mother, and she was not
**Prose:** The house blamed her for a decision she had made alone because nobody else would make it, and it was the
right decision, and it held. She has been in the ground for years with the blame still on her, because the house has
never taken it back, and you have gone on saying it for her in rooms she is not in.
**Reveals:** You will take the unpopular side of somebody who was correct, and you do it when they cannot hear you.
**Consequences:** Trait(`Honor`, 1); Goodwill(`village_headmen`, 2); Household(`mother_buried`)

### `cs_opt_it_was_you_and_they_were_right`
**Title:** It was you, and they were right
**Prose:** It was you, and what they said about you was true and most of it still is. You cost that house money it
did not have, twice, before you were old enough to be sent anywhere, and you have not paid it back.
**Reveals:** Your own household had given up on you, and their reason for it was a good one.
**Consequences:** Debt(null, 500); Goodwill(`town_gang_leaders`, 1); Trait(`Honor`, -1); Gate(`cs_opt_the_steward_who_took_you_in`); Gate(`cs_opt_you_took_it_upward`)

### `cs_opt_it_was_you_and_they_were_not`
**Title:** It was you, and they were not
**Prose:** You were the one it landed on and you had not done any of it, and you stopped explaining yourself
somewhere around your ninth year. You got very good at being somewhere else when a thing was being decided.
**Reveals:** You carried other people's blame young, and what you learned from it was how not to be found.
**Consequences:** Trait(`Valor`, -1); Place(`home_woodland`)

---

## Scene 3: `cs_scene_the_store_burned`

**Form:** D. **Targets:** Origins (starting means), Inclination. **Severity:** Formative.

**Title:** The Store Burned

**Prompt:** The store shed went up in the small hours of a dry summer, and everything the household owned that was
not on its back was inside it. You got in twice before the roof came down. One thing came out of that fire with you
and it is in your kit now. What is it?

**Prompt when `the-life-had-no-house`:** The store the people you travelled with were wintering in went up in the
small hours of a dry summer, and everything any of you owned that was not on a back or a cart was inside it. You got
in twice before the roof came down. One thing came out of that fire with you and it is in your kit now. What is it?

**Stage:** The player at thirteen, soot to the elbows, one sleeve burned through, night clothes, holding the thing
itself. A younger sibling standing well back, watching and not helping. Firelight from off stage.

### `cs_opt_the_sword_off_the_hooks`
**Title:** What Was on the Hooks
**Prose:** You went for the pegs over the door and came out with everything hanging on them. It was the oldest thing
in that house and the only thing nobody had ever once suggested selling.
**Reveals:** What your household counted as its worth was made of steel, and you went back through a burning
door for it without deciding to.
**Consequences:** Item(`family_sword`, 1); Place(`family_seat`)

### `cs_opt_the_box_of_papers`
**Title:** The box from under the board
**Prose:** You knew which board it was under because you had watched it go under there. You came out with the coin
and with the papers saying what was owed to whom, and the papers turned out to matter more than the coin did.
**Reveals:** Your household kept written accounts of what it owed and was owed, and you had been paying
attention to them.
**Consequences:** Item(`coin_pouch`, 1); Goodwill(`town_merchants`, 1)

### `cs_opt_the_bench_chest`
**Title:** The Bench Chest
**Prose:** You dragged it out by one handle with your shirt over your face. Everything else in there could be bought
again by people who owned tools like these, and you were not going to be the reason they could not.
**Reveals:** Your household's living came out of its hands, and you already understood that a trade travels.
**Consequences:** Item(`craft_tools`, 1); Place(`home_workshop`)

### `cs_opt_the_halter_and_what_was_on_it`
**Title:** The halter, and what was on the end of it
**Prose:** The byre wall was already taking the fire and you went along it with a knife until the animals in there were
loose. One of them would not go and you led it out yourself, and you have had that rope on your saddle since.
**Reveals:** You are steadier with animals in a panic than you are with people in one.
**Consequences:** Item(`riding_horse`, 1); Goodwill(`village_headmen`, 2)

### `cs_opt_the_stores_off_the_back_wall`
**Title:** What was stacked at the back
**Prose:** You came out under the winter stores and nothing else, and you were laughed at for it that night. You fed
four households out of that load before the spring and nobody laughed the second time.
**Reveals:** You took the thing that would still be feeding people next season over the thing worth most that
night.
**Consequences:** Item(`trade_goods`, 1); Goodwill(`village_headmen`, 1)

### `cs_opt_nothing_your_hands_were_full`
**Title:** Nothing. Your Hands Were Full
**Prose:** Your sister had gone back in after something that did not matter and you carried her out and nothing else.
The household lost everything it owned that night, and you have never once had to think about whether it was right.
**Reveals:** You counted people first, and it cost your family every object it had.
**Consequences:** Trait(`Mercy`, 1); Goodwill(`village_headmen`, 1); Place(`home_village`); Household(`a_sister`)

---

## Scene 4: `cs_scene_what_they_called_you_then`

**Form:** E. **Targets:** Inclination, Schooling. **Severity:** Formative. **Carries a gate.**

**Title:** What They Called You Then

**Prompt:** You came back for something you had left behind and stopped outside the door, because they were talking
about you in there and had not heard you come up the path. You stood and listened to the whole of it. What did they
say about you, when you were not in the room?

**Stage:** The player at fifteen, in outgrown clothes, standing very still just outside a doorway with their back to
it. A parent inside, seen from behind, mid-sentence. No mount.

### `cs_opt_they_said_you_were_your_fathers_son`
**Title:** That you were your father again
**Prose:** They said it as a settled thing, half of it approving and half of it a warning, and everybody in that room
knew which half was meant. It walked into every room in the valley ahead of you for years afterward.
**Reveals:** You are placed, and everyone who meets you has decided most of it before you have spoken.
**Consequences:** Goodwill(`village_headmen`, 2); Place(`home_village`); Gate(`cs_opt_the_crew_that_asked_no_names`)

### `cs_opt_they_said_you_would_not_stay`
**Title:** That you would not be here long
**Prose:** They were not angry about it. They said it the way people say a thing about the weather and then went on
to what would be done about the roof, and standing out there you understood that you had already gone.
**Reveals:** The people who raised you had stopped counting on you being there, and the road had you before you
had decided on it.
**Consequences:** LostYears(1); Place(`open_country`); Goodwill(`town_merchants`, 1)

### `cs_opt_they_said_you_were_soft`
**Title:** That there was no hardness in you
**Prose:** One of them said it kindly and the other did not, and the unkind one won the argument. You have been
answering it ever since without telling anybody that is what you are doing.
**Reveals:** You were marked gentle young, and a great deal of what you have done since was aimed at that.
**Consequences:** Trait(`Valor`, 1); LostYears(2); Enmity(`village_headmen`, 1)

### `cs_opt_they_said_you_never_forgot_anything`
**Title:** That you never let a thing go
**Prose:** They meant it as a complaint. They listed three things you had not let go of and they were right about all
three, and until that evening you had not known anyone else remembered them at all.
**Reveals:** You keep accounts of people, and the ones nearest to you find it hard to live beside.
**Consequences:** Goodwill(`town_merchants`, 2); Enmity(`village_headmen`, 1)

### `cs_opt_they_said_you_should_be_taught`
**Title:** That you should be sent somewhere and taught
**Prose:** One of them had been saying it for a year and the other had finally stopped arguing, and a sum was named
that the house did not have. It was found anyway. You have never asked from where.
**Reveals:** Your household spent something it could not spare on getting you taught.
**Consequences:** Debt(null, 400); Goodwill(`culture_lords`, 1); Gate(`cs_opt_nobody_taught_you`)

### `cs_opt_they_said_nothing_about_you`
**Title:** That they were not talking about you at all
**Prose:** You stood there a while waiting for your name and it did not come, and the whole of it was about somebody
else. You went in, took what you had come for, and said nothing to anyone.
**Reveals:** Nobody had formed a view of you, which left you nothing to trade on and left you useful to the
people who prefer a man nobody can place.
**Consequences:** Trait(`Calculating`, -1); Goodwill(`town_gang_leaders`, 1); LostYears(1); Household(`a_sibling`)

---

## Scene 5: `cs_scene_the_man_who_taught_you`

**Form:** C. **Targets:** Schooling. **Severity:** Defining. **Carries gates.**

**Title:** The Man Who Taught You

**Prompt:** Somebody took you on and made you into whatever you are, and the people who knew you both had an opinion
about him that they did not keep to themselves. They said he was wasting you, or that he was the best thing that had
ever happened to you, and they said it where you could hear. Who was he, and were they right about him?

**Stage:** The player at seventeen, in whatever that work put them in, standing with their hands turned out where
they can be seen. No family on stage. A mount saddled at the edge or no mount at all, depending on the choice.

### `cs_opt_the_steward_who_took_you_in`
**Title:** A household man, and they were right about him
**Prose:** He stopped in front of you in a crowded square, asked one question, and put you behind him without waiting
for you to gather anything. They said he had done you the largest favor anybody ever would, and you have not found an
argument against it.
**Reveals:** You were raised inside a great household, and you know how one runs from underneath.
**Consequences:** Goodwill(`culture_lords`, 2); Place(`nearest_castle`); LostYears(1)

### `cs_opt_the_caravan_master`
**Title:** A caravan master, and they were wrong about him
**Prose:** They said he was using you up on a road for the price of your food, and not one of them had ever been more
than a day from where they were born. He showed you four towns and how the price of the same sack moves between them.
**Reveals:** You were trained on roads by people who move goods, and you still defend the man who did it.
**Consequences:** Goodwill(`town_merchants`, 2); Place(`nearest_town`); Item(`pack_mule`, 1)

### `cs_opt_the_sergeant`
**Title:** A sergeant, and they were right about him
**Prose:** They said he would get you killed and they said it to his face, and he agreed with them and drilled you
until dark anyway. Of the ones he took out of that square, few are still alive, and you are one of them.
**Reveals:** You were made in a drill yard and you know exactly what it cost the people beside you.
**Consequences:** Goodwill(`culture_lords`, 2); Goodwill(`town_gang_leaders`, 1)

### `cs_opt_the_craftsman`
**Title:** A craftsman, and they were wrong about him
**Prose:** They said he kept you at the bench long past when you were any use to him, which was true, and that it
amounted to theft, which was not. He put his name near nothing of yours until it was worth his name being near it.
**Reveals:** You served the whole of an apprenticeship, and you are not sorry about the years it ate.
**Consequences:** Goodwill(`town_merchants`, 2); Place(`home_workshop`); LostYears(2)

### `cs_opt_the_crew_that_asked_no_names`
**Title:** A man who never gave you his name, and they were right about him
**Prose:** He walked a line of you and picked without speaking and never once said what the work was in aid of. Every
person who warned you about him was correct, and not one of them offered you anything instead.
**Reveals:** You came up inside something unlawful, and you have not settled your account with it.
**Consequences:** Goodwill(`town_gang_leaders`, 2); Debt(null, 400); Gate(`cs_opt_you_took_it_upward`)

### `cs_opt_nobody_taught_you`
**Title:** Nobody, and they were right to say so
**Prose:** You were not taken on by anyone, and the people saying you would come to nothing were saying the obvious.
Everything you can do you found out yourself, slowly, with nobody standing there to tell you when you had it wrong.
**Reveals:** Nothing about you was given to you, and it took years longer than it needed to.
**Consequences:** Place(`home_woodland`); LostYears(3); Gate(`cs_opt_you_could_ride_at_a_man`)

---

## Scene 6: `cs_scene_what_you_could_do`

**Form:** A. **Targets:** Mastery. **Severity:** Formative.

**Title:** What You Could Do

**Prompt:** By the time you were grown there was one thing you could do that the people around you could not, and it
was the thing they came and got you for. It was not always the thing you would have picked for yourself. What was it?

**Stage:** The player at twenty-two, working clothes, sleeves shoved back past the elbow, doing the thing with their
hands. No family. A mount standing loose behind on a dropped rein.

### `cs_opt_you_could_shoot`
**Title:** You could put it where you were looking
**Prose:** Other people aimed and you simply looked at the thing and it was hit, and you could never explain the
difference to anybody who asked. It made you welcome in places that had nothing else to offer you.
**Reveals:** You settle a problem from a distance, and the villages with nothing else to offer keep a place for
you.
**Consequences:** Item(`hunting_bow`, 1); Goodwill(`village_headmen`, 2)

### `cs_opt_you_could_hold_a_doorway`
**Title:** You Could Hold a Doorway
**Prose:** You could put your back somewhere narrow and stay there long after the argument for staying had run out,
and men who would not stand anywhere else would stand next to you.
**Reveals:** You would rather hold a line than break one, and other people steady up when you are on it.
**Consequences:** Item(`spear`, 1); Goodwill(`village_headmen`, 1)

### `cs_opt_you_could_ride_at_a_man`
**Title:** You could ride at a man and not turn
**Prose:** The animal knew before you did and neither of you ever came out of it, and people who had seen it once
described it to people who had not. It is the only thing you have ever been vain about.
**Reveals:** You fight from a horse or you do not fight, and there was a horse in your life early enough to matter.
**Consequences:** Item(`lance`, 1); Goodwill(`culture_lords`, 2)

### `cs_opt_you_could_price_a_load`
**Title:** You could price a load by looking at it
**Prose:** You walked past a cart and knew what was on it and what it would fetch two valleys over, and you were
right often enough that men started walking you past their carts on purpose.
**Reveals:** You read goods and margins the way other people read weather, and it is known where goods change hands.
**Consequences:** Goodwill(`town_merchants`, 2); Place(`nearest_town`)

### `cs_opt_you_could_get_into_a_shut_room`
**Title:** You could get into a room that was shut
**Prose:** Locks, walls, the hour of the night the watch changes: you learned all of it without meaning to and then
found out what it was worth. You have never had to ask twice for work.
**Reveals:** A closed thing is an invitation to you, and the people who need that done know your name.
**Consequences:** Goodwill(`town_gang_leaders`, 2); Place(`nearest_hideout`)

### `cs_opt_you_could_keep_a_hurt_man_alive`
**Title:** You could keep a hurt man alive
**Prose:** You were the one sent for when somebody was opened up, and you did not faint and you did not hurry, and a
good number of them are walking around now. None of them ever quite know how to speak to you.
**Reveals:** You are the one who is steady over a wound, which is rarer in a crowd than anybody expects.
**Consequences:** Goodwill(`village_headmen`, 2); Trait(`Mercy`, 1)

---

## Scene 7: `cs_scene_what_the_yard_said`

**Form:** E. **Targets:** Mastery. **Severity:** Defining. **Carries a gate.**

**Title:** What the Yard Said

**Prompt:** It came at night and there was no order to any of it. In the morning the people you had been standing
with had already settled on what you were, and you heard it across a yard, told to somebody who had not been there.
What did they say about you?

**Stage:** The player at twenty-three, half dressed, barefoot on stone, one hand flat on a wall, listening. Nobody
else on stage.

### `cs_opt_he_does_not_stop`
**Title:** That You Did Not Stop
**Prose:** They said it with something in their voices that was not entirely admiration, and one of them said it
twice. You had not known until you heard it that you had gone further into that passage than anybody else would have.
**Reveals:** Your instinct is to close, and it is the people who trade in that kind of nerve who remembered it.
**Consequences:** Trait(`Valor`, 1); Trait(`Mercy`, -1); Goodwill(`town_gang_leaders`, 2)

### `cs_opt_he_held_the_passage`
**Title:** That Nothing Got Past You
**Prose:** They described the doorway and how long it had been and who had been behind it, and they did not mention
your name until the end, when they did not need to. It is the only time anyone has said that about you.
**Reveals:** You stood where nothing could pass, and the people who were behind you say so where it counts.
**Consequences:** Goodwill(`culture_lords`, 2); Goodwill(`town_gang_leaders`, 2)

### `cs_opt_he_was_up_where_it_was_safe`
**Title:** That you had been up where it was safe
**Prose:** They were right about where you were and wrong about everything you did from there, and you stood in the
shadow and let them finish. You have never bothered to correct that one anywhere.
**Reveals:** You went up and found the view, and the yard settled on a version of you that you have never
bothered to correct.
**Consequences:** Trait(`Calculating`, 1); Enmity(`culture_lords`, 1); Place(`nearest_castle`)

### `cs_opt_he_came_round_the_outside_mounted`
**Title:** That you had come around the outside
**Prose:** They told it as the part that ended the thing, which it was, and the telling had gotten better already by
the morning. Somebody in that yard repeated it where it did you good.
**Reveals:** You went for the animal before you went for anything to hold, and it was seen by the right people.
**Consequences:** Goodwill(`culture_lords`, 1); Place(`open_country`); Goodwill(`town_merchants`, 2)

### `cs_opt_he_fought_with_what_was_on_the_bench`
**Title:** That you had fought with something that was not a weapon
**Prose:** They laughed about it and then stopped laughing, because two of them had seen what it did. You had picked
up the heaviest thing within reach and it had only needed to work once each time.
**Reveals:** You bring weight to a fight rather than skill, and it has never yet been the wrong answer.
**Consequences:** Item(`two_handed_axe`, 1); Goodwill(`town_gang_leaders`, 1)

### `cs_opt_he_was_not_in_the_yard`
**Title:** That you had not been in the yard at all
**Prose:** You had been in the rooms carrying out the ones who could not carry themselves, and the men who count
fights do not count that. Some of those people are alive now and none of them were in the yard to say so.
**Reveals:** You were not in the fight, and the people who keep score know it and always will.
**Consequences:** Ally(`the_one_you_carried`); Trait(`Valor`, -1); Gate(`cs_opt_took_it_and_kept_it`)

---

## Scene 8: `cs_scene_the_verdict`

**Form:** C. **Targets:** Hinge. **Severity:** Defining. **Carries gates.**

**Title:** The Verdict

**Prompt:** A man you knew was judged in front of you for something he had not done, and it was carried out the same
afternoon. Everybody in that square who knew the truth of it said nothing, including the one man who could have ended
it in a sentence. You have had years to decide who was actually in the wrong that day.

**Stage:** The player at twenty-six, travel-worn clothes, standing very still with empty hands, facing off stage.
Nobody else on stage.

### `cs_opt_you_took_it_upward`
**Title:** The man who passed it, and you said so upward
**Prose:** You put it on the man in the chair and you took it over his head, and then over that head, and it cost you
the better part of a year and most of what you had. It worked, partly, and far too late to be worth it.
**Reveals:** You believe the structure can be made to answer, and you spent a year proving it half true.
**Consequences:** Trait(`Honor`, 1); Goodwill(`culture_lords`, 1); Debt(null, 400); LostYears(1)

### `cs_opt_the_silent_one_was_the_worst_of_them`
**Title:** The one who said nothing, and you never spoke to him again
**Prose:** You decided the man who could have ended it and did not was worse than the man who passed it, and you said
so in front of the people whose opinion he lived on. You have not spoken a word to him since and you will not.
**Reveals:** You judge the ones who stood by harder than the one who acted, and you say so out loud.
**Consequences:** Enmity(`culture_lords`, 1); Goodwill(`village_headmen`, 2)

### `cs_opt_the_crowd_was_in_the_wrong`
**Title:** The crowd, and you were standing in it
**Prose:** You have never decided the man in the chair was the problem, because a whole square watched it and you
were one of them. You have a list now: everybody on it was standing there, and none of them know they are on it.
**Reveals:** You went quiet instead of loud, and nothing about that afternoon is finished.
**Consequences:** Enmity(`town_merchants`, 1); Goodwill(`town_gang_leaders`, 1); Place(`nearest_town`)

### `cs_opt_he_had_earned_something_and_not_this`
**Title:** The Dead Man, Partly
**Prose:** He had not done the thing they judged him for and he had done others, and you are the only person who says
both halves of that out loud. You carried his household through two winters anyway, and you borrowed to do it.
**Reveals:** You can hold a man guilty and still take on what he was carrying, and you did not count the cost first.
**Consequences:** Goodwill(`village_headmen`, 1); Debt(null, 800); Trait(`Generosity`, 1)

### `cs_opt_you_were_in_the_wrong_for_standing_there`
**Title:** You were, for standing there, and you did not stand there twice
**Prose:** You went through the people in front of you and took him down off it, hours too late for it to matter to
him. You did not sleep under a roof you were welcome in for a long time after that.
**Reveals:** You answer an injustice with your hands in front of witnesses, and a great house has your name written.
**Consequences:** Enmity(`culture_lords`, 2); Place(`nearest_hideout`); Gate(`cs_opt_took_it_and_kept_it`); Gate(`cs_opt_the_name_of_a_house`)

### `cs_opt_nobody_was_that_is_how_it_works`
**Title:** Nobody was. That is how it works, and you left
**Prose:** You decided nothing had gone wrong that afternoon except that you had been standing close enough to see
it. You were on a road inside the hour and you have not stayed anywhere long enough to see a second one since.
**Reveals:** You stopped believing a place would hold, and you have been paying for that in years.
**Consequences:** LostYears(3); Place(`open_country`)

---

## Scene 9: `cs_scene_what_the_town_said`

**Form:** E. **Targets:** Hinge, Intent. **Severity:** Defining.

**Title:** What the Town Said

**Prompt:** You had been years in the same town by then, standing out in the same cold with the same people every
morning, waiting on work. A man came through looking for somebody who could do a particular kind of thing, and the
ones you stood beside every morning answered him at length, close enough to touch, believing you had already gone out
with the early cart. What had the place you were living in decided you were for?

**Stage:** The player at twenty-nine, in working clothes gone the shape of the work, hood up, standing still in a
crowd with their back to the speakers. No mount.

### `cs_opt_they_said_you_were_the_one_they_sent`
**Title:** That you were the one they sent
**Prose:** They went through the things that had left that town in your hands and come back settled, in order, and
the man asking stopped them before they were finished because he had heard enough. None of it was work you had ever
put yourself forward for.
**Reveals:** The place you lived in used you for the road rather than for the bench, and it never asked you for
anything else.
**Consequences:** Goodwill(`town_merchants`, 2); Place(`nearest_castle`)

### `cs_opt_they_said_you_were_already_spoken_for`
**Title:** That you were already spoken for
**Prose:** They told him not to waste the morning on you, because whoever stands the way you stand is being kept by
somebody further up, and that keeping is never written anywhere you can read it. You had been nobody's for years and
had not known you were wearing it.
**Reveals:** You read as retained, and doors have been opening ahead of you for a reason you never arranged.
**Consequences:** Goodwill(`culture_lords`, 2); Place(`nearest_castle`)

### `cs_opt_they_said_you_were_not_to_be_pushed`
**Title:** That you were not to be pushed
**Prose:** They warned him about you the way people warn a stranger about a dog, with the same care in it and about
as much liking, and one of them had a story ready that had never happened. You have let that story go around for
years because of what it saves you.
**Reveals:** The town kept its distance from you on purpose, and the people who trade in distance had found you
before you noticed them.
**Consequences:** Goodwill(`town_gang_leaders`, 1); Trait(`Mercy`, -1); Enmity(`town_merchants`, 1)

### `cs_opt_they_said_you_were_owed`
**Title:** That You Were Owed
**Prose:** One voice came over the top of all the others with an account of a winter nobody else had thought to bring
up, and would not be talked down off it until the rest had gone quiet. The one who said it came and found you that
evening and has not gone anywhere since.
**Reveals:** Somebody there had been keeping your account while you were not keeping it, and they left with you.
**Consequences:** Ally(`the_one_who_spoke_for_you`); Goodwill(`town_merchants`, 1)

### `cs_opt_they_said_you_came_cheap`
**Title:** That You Came Cheap
**Prose:** They put a price on you out loud, the way a price is said over a thing lying on a bench, and it was under
what you had been quietly telling yourself for years. He paid it without arguing, and that was the worse half of the
morning.
**Reveals:** You heard what you had let a place have you for, and you have not felt you owed that town a day since.
**Consequences:** Item(`coin_pouch`, 1); Trait(`Honor`, -1); Place(`nearest_town`)

### `cs_opt_they_said_it_had_come_in_with_you`
**Title:** That whatever you had left had come in with you
**Prose:** They told him the town had been a quieter place in the years before you walked into it, and then they told
him why they thought so, and most of the reasons they gave belonged to other people. You stood where you were until
the crowd moved and went out with it.
**Reveals:** The place you settled in counts you as the thing it has to get through, and you keep somewhere else
ready now.
**Consequences:** Enmity(`town_merchants`, 2); Place(`nearest_hideout`); Goodwill(`town_gang_leaders`, 2)

---

## Scene 9 (War Sails): `cs_scene_what_the_town_said`

**Form:** E. **Targets:** Hinge, Intent. **Severity:** Defining. **Stands in scene 9's place when the naval DLC is
loaded, under the same id.**

**Title:** What the Port Said

The run is thirteen scenes either way. War Sails does not make it longer, it makes the ninth situation a different
one: the same question, the same weight and the same slot, asked on a waterfront instead of in a square. A player
without the DLC never meets a word of this and the scene above is their ninth; a player with it never meets the scene
above. Nothing else in the run changes, which is checked rather than claimed.

Two of the six answers put years on the water, and `Place(`the_open_water`, n)` is the only target in the catalog
that scores the sea. The amount on it is how much of the life went out there, which is the one exception to places
being a yes or a no: the difference between a man who was out every season and a man who went out when nothing else
was hiring is the whole of what those two answers are worth, and the three skills the DLC adds are drawn from it.
The other four answers say the life stayed ashore, and a life that stayed ashore comes out of the run exactly as it
does without the DLC installed.

**Prompt:** The season's hiring was done off the same stretch of boards every year, and you had stood on it long
enough that nobody looked at you twice. A master came down it wanting one particular kind of man, and the ones you
had stood beside all those mornings told him what you were at length, with you close enough behind them to touch,
because they had you out on the tide already. What had that front decided you were for?

**Stage:** The player at twenty-nine, in working clothes gone the shape of the work, hood up, standing still in a
crowd with their back to the speakers. No mount.

### `cs_opt_they_said_you_were_worth_keeping`
**Title:** That You Were Worth Keeping
**Prose:** They counted off the seasons you had been out and what had come back with you each time, in order, and the
master stopped them halfway down the list because he had heard the part he came for. Not one of them mentioned a
crossing you had turned back from, because in all those years there had not been one. Nobody mentioned the villages
either, which had stopped counting you as one of theirs somewhere in the middle of it.
**Reveals:** The water is where your working life went, and the ground you came off has closed behind you.
**Consequences:** Place(`the_open_water`, 5); Trait(`Mercy`, -1); Enmity(`village_headmen`, 1)

### `cs_opt_they_said_you_went_out_anyway`
**Title:** That you went out when nobody else would
**Prose:** They named two runs from that year that nobody else on those boards had put a mark against, and then they
named what had been aboard, and the second name was said quieter than the first. You had not asked either time what
was under the covers, and you had been paid as though you had.
**Reveals:** You have been out there, and the work that took you out is the work nobody writes down.
**Consequences:** Place(`the_open_water`, 3); Goodwill(`town_gang_leaders`, 2)

### `cs_opt_they_said_you_had_come_ashore`
**Title:** That You Had Come Ashore
**Prose:** They told him you had been worth something once and gave the year you stopped going out, and then they
gave a reason for it that was not the reason. You have let that stand for years, because the true one is worth less
to you than what you came ashore holding.
**Reveals:** You took the money and stayed on the land, and you have let a story you know to be false pay for it.
**Consequences:** Item(`coin_pouch`, 2); Trait(`Honor`, -1); Goodwill(`town_merchants`, 1)

### `cs_opt_they_said_you_priced_everything`
**Title:** That You Priced Everything
**Prose:** One of them said you could walk the length of a deck and put a figure on what was under the covers without
lifting one of them, and nobody in that crowd argued, and two of them looked at the boards. You have said the figure
wrong on purpose twice, both times for yourself, and the men who need a figure said quietly have never once been able
to buy one off you.
**Reveals:** What the front decided you were for was the ledger rather than the crossing, and it is not only
merchants who send for a man who cannot be bought a number from.
**Consequences:** Goodwill(`town_merchants`, 2); Item(`trade_goods`, 1); Goodwill(`culture_lords`, 1)

### `cs_opt_they_said_somebody_there_owed_you`
**Title:** That Somebody There Owed You
**Prose:** One voice came up over the others with an account of a night three winters back that none of the rest had
thought worth raising, and would not be argued down off it until they went quiet. He found you that evening, he has
not gone anywhere since, and the men who run that front have not forgotten which way he spoke.
**Reveals:** One man there kept your account when nobody else was keeping it, and he left with you and paid for
saying so.
**Consequences:** Ally(`the_one_who_spoke_for_you`); Enmity(`town_gang_leaders`, 1)

### `cs_opt_they_said_the_front_had_changed`
**Title:** That the front had been quieter before you
**Prose:** They told him what those boards had been like in the years before you first came up them, and then they
went through why they thought so, and most of what they listed belonged to other men. You stood where you were until
the line moved, and then you went up it with everybody else.
**Reveals:** The place you worked out of counts you as the thing it has to get through, and you keep somewhere else
ready now.
**Consequences:** Enmity(`town_merchants`, 2); Place(`nearest_hideout`); Goodwill(`town_gang_leaders`, 1)

---

## Scene 10: `cs_scene_the_seat`

**Form:** C. **Targets:** Intent. **Severity:** Defining.

**Title:** The Seat

**Prompt:** A man with something real to give offered it to you across a table, with a condition attached that he did
not say out loud and you both understood. Everybody who heard about it afterward had a view of him, and most of them
gave you theirs whether or not you asked. What was he, and what did you do about it?

**Stage:** The player at thirty-two, in the best they own, seated, both hands flat on the table. Nobody else on
stage.

### `cs_opt_took_it_and_kept_it`
**Title:** He was exactly what he said he was, and you are still his
**Prose:** You said yes and then you said the words that went with the yes, and you have not broken them once in the
years since. It closed every other road you might have taken and you have not looked down one of them.
**Reveals:** You are bound to somebody above you, publicly, and that binding is the largest fact about you.
**Consequences:** Title(`sworn_of_the_house`); Place(`granted_holding`)

### `cs_opt_he_was_buying_cheap`
**Title:** He was buying a man cheap, and you let him think he had
**Prose:** You said yes and took what came with the yes, and then you did the other thing anyway. He found out in his
own time and there is nothing left for him to do about it except tell the story his way.
**Reveals:** You hold what you were given and owe nothing for it, and everyone who matters knows how you got it.
**Consequences:** Title(`oathbreaker`); Enmity(`culture_lords`, 2); Item(`coin_pouch`, 2); Gate(`cs_opt_the_name_of_a_house`)

### `cs_opt_he_was_honest_and_you_said_the_part_he_had_not`
**Title:** He was honest, which is why you said the part he had not
**Prose:** You named the condition out loud across his own table, and then you thanked him and went out. He has told
that story a number of times since and he does not tell it against you.
**Reveals:** You will give up a real thing rather than hold it on terms nobody will say aloud.
**Consequences:** Goodwill(`culture_lords`, 1); Trait(`Calculating`, -1); Goodwill(`town_merchants`, 1)

### `cs_opt_he_was_smaller_than_he_thought`
**Title:** He was smaller than he thought he was
**Prose:** You let him finish and then told him what you actually wanted, which was a long way above what he had
brought to the table. He laughed, and then he stopped laughing, and then he asked you to say it again.
**Reveals:** You would not be placed, and you told a powerful man so to his face and let it stand.
**Consequences:** Title(`the_one_who_asked`); Enmity(`culture_lords`, 1)

### `cs_opt_he_was_a_fool_and_you_took_the_portable_part`
**Title:** He was a fool, and you took the part you could carry
**Prose:** You turned down the thing he was actually offering and asked instead for the small movable version of it,
and he agreed, a little insulted. You were three valleys away before the season turned.
**Reveals:** You will trade a great deal of standing for the freedom to be somewhere else next year.
**Consequences:** Item(`coin_pouch`, 2)

### `cs_opt_he_was_nothing_to_you`
**Title:** He was nobody, and you did not answer him
**Prose:** You finished the cup, stood up, and went out without giving him a word either way. He has not made the
offer a second time and you have not been back through that town.
**Reveals:** You will not be held to an answer, and you cost yourself a place rather than give one.
**Consequences:** LostYears(1); Place(`open_country`)

---

## Scene 11: `cs_scene_the_purse`

**Form:** D. **Targets:** Intent (starting means). **Severity:** Defining.

**Title:** The Purse

**Prompt:** You came into more coin at once than you had ever held, honestly enough, and it was gone inside a month.
One thing you turned it into is still with you and you would not part with it. What is it?

**Stage:** The player at thirty-four, standing beside a saddled mount, the thing itself in hand or on the animal.
Nobody else on stage.

### `cs_opt_a_horse_and_a_harness`
**Title:** The Animal
**Prose:** You spent it on one horse and the gear to keep it, and everybody told you it was far too much money for a
horse. It was, and you would do it again tomorrow.
**Reveals:** You intend to be somewhere quickly and to arrive above everyone already standing there.
**Consequences:** Item(`war_horse`, 1); Item(`riding_tack`, 1)

### `cs_opt_arms_and_mail`
**Title:** The Iron
**Prose:** You bought what you would be wearing and what you would be holding, and you bought it good rather than
plentiful. You have not had to replace any of it and you do not expect to.
**Reveals:** You expect to be fighting, and you expect to survive being wrong about something.
**Consequences:** Item(`mail_hauberk`, 1); Item(`one_handed_sword`, 1)

### `cs_opt_a_wagon_and_a_load`
**Title:** The Load
**Prose:** You put every coin of it into goods and the animals to carry them, which left you poorer that night than
you had been that morning. You were not poorer by the end of the season.
**Reveals:** You put money to work rather than into your hands, and you are known where goods change hands.
**Consequences:** Item(`pack_mule`, 2); Item(`trade_goods`, 1); Goodwill(`town_merchants`, 2)

### `cs_opt_men_who_would_come`
**Title:** The men, and what they carry
**Prose:** You paid men a season in advance, which nobody does, and they came, which nobody expected. You have owed
somebody else for that season ever since and they have not forgotten it.
**Reveals:** You spent it on other people rather than on equipment, and you went into debt to do it.
**Consequences:** Ally(`the_paid_men`); Debt(null, 600); Goodwill(`town_gang_leaders`, 1)

### `cs_opt_a_writ_and_a_name`
**Title:** The Paper
**Prose:** You spent it on a document and on the men whose signatures make a document mean anything. It looks like
nothing in your hand and it opens gates that iron does not.
**Reveals:** You went for standing rather than goods, and you know exactly what standing costs.
**Consequences:** Title(`holder_of_the_writ`); Goodwill(`culture_lords`, 2)

### `cs_opt_you_sent_it_home`
**Title:** Nothing. It went where it was needed
**Prose:** You kept enough to eat on and sent the rest home to your mother and father, who are both living and both
still in the house you were small in, and you attached no letter because there was nothing in it to explain. They have
not spent it and they will not.
**Prose when `the-life-buried-the-father`:** You kept enough to eat on and sent the rest home to your mother, who has
held that house on her own since your father went into the ground, and you attached no letter because there was
nothing in it to explain. She has not spent it and she will not.
**Prose when `the-life-buried-the-mother`:** You kept enough to eat on and sent the rest home to your father, who has
been alone in that house since your mother went into the ground, and you attached no letter because there was nothing
in it to explain. He has not spent it and he will not.
**Prose when `the-life-buried-them-both`:** You kept enough to eat on and sent the rest back to the ones who raised
you after your mother and your father went into the ground, and you attached no letter because there was nothing in
it to explain. It has not been spent and it will not be.
**Reveals:** You are riding out with nothing on purpose, and there is a household behind you that owes you.
**Consequences:** Goodwill(`village_headmen`, 1); Trait(`Generosity`, 1); Place(`home_village`); Household(`parents_still_standing`)

---

## Scene 12: `cs_scene_the_winter_between`

**Form:** A. **Targets:** Seasoning. **Severity:** Habit.

**Title:** The Winter Between

**Prompt:** There came seasons with nothing in them: no employer, no war, no road you had to be on. They were
entirely yours and they came around more than once. What did you do with them?

**Stage:** The player at thirty-six, indoor clothes, seated by a fire, hands occupied or empty depending on the
choice. Nobody else on stage.

### `cs_opt_one_thing_until_it_was_right`
**Title:** One Thing, Every Day
**Prose:** You picked the single thing you were best at and did it every day until you were better at it than the man
who taught it to you. You got worse at everything else in the same months and you knew it at the time.
**Reveals:** You are narrow and you chose to be, it took more seasons than anything else on this page, and
nobody around you got a share of them.
**Consequences:** LostYears(2); Trait(`Generosity`, -1); Goodwill(`town_merchants`, 1)

### `cs_opt_whatever_work_came`
**Title:** Whatever Was Offered
**Prose:** You said yes to everything that came that season and none of it was related to any of the rest. You can
now begin most jobs competently and finish none of them beautifully.
**Reveals:** You spread yourself across everything available, it shows in both directions, and half a valley
has had a day of your work.
**Consequences:** LostYears(1); Goodwill(`village_headmen`, 1)

### `cs_opt_you_taught_it`
**Title:** You spent them on somebody else
**Prose:** You spent the months on somebody younger who wanted to know what you knew, and you found out how much of
it you had never put into words. They are still with you and they ask better questions now.
**Reveals:** You had enough to give away, and you gained a person by giving it.
**Consequences:** Ally(`the_apprentice`); LostYears(1)

### `cs_opt_you_married_that_winter`
**Title:** You Married That Winter
**Prose:** You went into one of those seasons with nobody to answer to and came out of it with a household, and the
whole of it was settled between two families in the weeks the weather took to turn. Somebody has been waiting on word
from you ever since, and nobody was before.
**Reveals:** Those seasons left a person in your house rather than years behind you, and the people of the place that
person came from count you as one of their own.
**Consequences:** Goodwill(`village_headmen`, 2); Debt(null, 400); Household(`a_spouse`)

### `cs_opt_you_drank_them`
**Title:** You Spent Them Badly
**Prose:** They went, several of them, and there is very little to say about where. You came out of it owing money to
people who are patient in a way you do not like.
**Reveals:** Years came out of your life and left nothing behind them but the account, and the people holding
it are patient for a living.
**Consequences:** LostYears(3); Debt(null, 500); Goodwill(`town_gang_leaders`, 1)

### `cs_opt_you_walked`
**Title:** You Spent Them on Roads
**Prose:** You went out with no destination and came back when the weather turned, more than once. You know the
ground between four towns better than the couriers who are paid to know it.
**Reveals:** You gave the time to country rather than to craft, and it is in your legs and your eyes.
**Consequences:** LostYears(2); Place(`open_country`)

### `cs_opt_you_wintered_at_another_mans_fire`
**Title:** You spent them at another man's fire
**Prose:** There was a hall that took you in whenever the weather turned, and you went in with nothing and came out
in the spring no further behind than you had gone in. Nobody in that house ever said out loud what the arrangement
was, and you never asked, and it is open still.
**Reveals:** Those seasons took nothing out of your life, because a house with room in it carried them for you, and
that account has never been settled or spoken about.
**Consequences:** Goodwill(`culture_lords`, 1); Debt(null, 400)

### `cs_opt_you_healed`
**Title:** You Spent Them Getting Well
**Prose:** Something took the whole of one season and most of the next, and for a while it was not certain you were
getting up from it. You did, slower, and with a better idea of what a body will take.
**Reveals:** A long stretch went by that was never yours to spend, and you came out of it patient.
**Consequences:** LostYears(3); Place(`home_village`)

---

## Scene 13: `cs_scene_the_name_they_use`

**Form:** E. **Targets:** Seasoning. **Severity:** Defining. Final scene.

**Title:** The Name They Use

**Prompt:** In a common room where nobody had noticed you come in behind them, you heard yourself described. They did
not use your given name for it. You had evidently been called that for a while without once hearing anybody say it.

**Stage:** The player at whatever age the life has reached, in the clothes the life has given them, standing in a
doorway with their back to the light, listening. Nobody else on stage.

### `cs_opt_the_name_of_a_trade`
**Title:** They named you by the work
**Prose:** They used the thing you do rather than the family you come from, and they used it as though there were no
question who was meant. Nobody in that room could have told you where you were born.
**Reveals:** You are known for one skill and for nothing else, and that took a long time to arrange.
**Consequences:** Title(`by_the_trade`); Goodwill(`town_merchants`, 2); LostYears(1)

### `cs_opt_the_name_of_a_house`
**Title:** They put a house in front of it
**Prose:** They said a great name and then yours after it, in that order, and nobody at the table found the pairing
strange. You had never heard the two spoken together before that evening.
**Reveals:** Your name travels attached to somebody else's, which is worth more than it costs you.
**Consequences:** Title(`of_the_house`); Goodwill(`culture_lords`, 2)

### `cs_opt_the_name_with_a_price_on_it`
**Title:** They named the price on you
**Prose:** They were not describing you so much as quoting you, and the figure had gone up since the last time it was
posted. Two of them wanted to go looking and the third talked them out of it.
**Reveals:** You are wanted, and the people who deal in wanted men consider you worth knowing.
**Consequences:** Title(`wanted`); Enmity(`culture_lords`, 2); Goodwill(`town_gang_leaders`, 2)

### `cs_opt_the_old_one`
**Title:** They Called You Old
**Prose:** They meant it kindly and they were not wrong, and it was the first time anyone had said it where you could
hear. You had buried your mother and your father both by then and had still not counted yourself among the old, and
you thought about that on the road for a week and then stopped thinking about it.
**Prose when `the-life-says-both-parents-living`:** They meant it kindly and they were not wrong, and it was the
first time anyone had said it where you could hear. Your mother and your father were both living still, in the house
you were small in, and you had gone grey ahead of the pair of them, and you thought about that on the road for a week
and then stopped thinking about it.
**Reveals:** More of your life is behind you than in front of it, and other people have started to notice.
**Consequences:** Title(`the_old`); Goodwill(`village_headmen`, 2); LostYears(4); Household(`parents_by_then`)

### `cs_opt_the_name_of_a_place`
**Title:** They named you by where you are from
**Prose:** They used the valley rather than the man, and they said it the way people say a place they have heard
things about. Nobody in that room had been within four days of it.
**Reveals:** Somewhere small is carrying your reputation for you, and you will be measured against it.
**Consequences:** Title(`of_the_place`); Place(`home_village`); Goodwill(`village_headmen`, 1)

### `cs_opt_no_name_at_all`
**Title:** They could not finish the sentence
**Prose:** They described your coat, and then the road you had come in off, and then they gave up, because
nobody had settled on anything for you yet. You stood in the doorway a moment longer than you needed to.
**Reveals:** Nothing has stuck to you but the animal you arrived on, because there has not been time for
anything else to.
**Consequences:** LostYears(2); Goodwill(`town_gang_leaders`, 1); Place(`open_country`)

---

## Gate index

Eight options set a gate, closing seven named options through ten edges. Three of those seven are shut from more
than one place, and that is deliberate: a gate is the only kind of consequence whose repetition the reader counts as
schooling, so a road shut twice reads as a commitment made twice. A gate also carries a little of the hinge, which
is why the run does not simply add more of them: an answer shut from everywhere reads as a life that broke rather
than a life that was taught.

| Gate set in | Option | Closes | In scene |
|---|---|---|---|
| Scene 2 | `cs_opt_it_was_you_and_they_were_right` | `cs_opt_the_steward_who_took_you_in` | 5 |
| Scene 2 | `cs_opt_it_was_you_and_they_were_right` | `cs_opt_you_took_it_upward` | 8 |
| Scene 4 | `cs_opt_they_said_you_were_your_fathers_son` | `cs_opt_the_crew_that_asked_no_names` | 5 |
| Scene 4 | `cs_opt_they_said_you_should_be_taught` | `cs_opt_nobody_taught_you` | 5 |
| Scene 5 | `cs_opt_the_crew_that_asked_no_names` | `cs_opt_you_took_it_upward` | 8 |
| Scene 5 | `cs_opt_nobody_taught_you` | `cs_opt_you_could_ride_at_a_man` | 6 |
| Scene 7 | `cs_opt_he_was_not_in_the_yard` | `cs_opt_took_it_and_kept_it` | 10 |
| Scene 8 | `cs_opt_you_were_in_the_wrong_for_standing_there` | `cs_opt_took_it_and_kept_it` | 10 |
| Scene 8 | `cs_opt_you_were_in_the_wrong_for_standing_there` | `cs_opt_the_name_of_a_house` | 13 |
| Scene 10 | `cs_opt_he_was_buying_cheap` | `cs_opt_the_name_of_a_house` | 13 |

`cs_opt_took_it_and_kept_it`, `cs_opt_you_took_it_upward` and `cs_opt_the_name_of_a_house` can each be closed by
either of two earlier choices, so the gating code must treat a gate as a set membership test rather than a single
flag. Scene 5 is both a gate setter and a gate receiver, and `cs_opt_nobody_taught_you` is both at once, which the
code pass must handle in one direction only: a gate always closes an option in a later scene, never an earlier one.

The sentence printed beside a shut option is written against the edge that shut it, not against the option, because
the code hands back the note of a gate this life actually set and nobody reads the reason behind an answer they did
not give. So `cs_opt_took_it_and_kept_it` gives the man who was not in the yard one reason and the man who took a
body down off a gallows another; where a reason holds whatever shut the door, as with `cs_opt_you_took_it_upward`
and `cs_opt_the_name_of_a_house`, the same sentence goes on every edge and names no cause at all. One reason reads
the life rather than the edge: `cs_opt_you_could_ride_at_a_man` is shut for want of a teacher, and a character an
earlier scene already handed an animal is told the animal was never the part he was missing, since telling him he
never had one contradicts a panel he read scenes ago.
