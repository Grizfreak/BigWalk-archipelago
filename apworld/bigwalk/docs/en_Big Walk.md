# Big Walk

## Where is the options page?

The [player options page](../player-options) lets you configure your personal
options and export a config file from them.

## What does randomization do to this game?

Solving a puzzle no longer hands you its gourd. The puzzle still opens, and
solving it still sends a check out to the multiworld, but the gourd itself is
gone — the only gourds you will ever hold are the ones other players send
you. They arrive at the hub as ordinary, pickable props, and they are
generic: any gourd fits any slot of any tower.

That changes what the monuments are for. Filling one is no longer the reward
for clearing a tower's puzzles, and it no longer releases that tower's key
either: it is simply how you spend the gourds Archipelago gives you, and
every deposit is a check.

**The big keys are items too.** A key arrives from the multiworld and drops
at the spawn point like a gourd — uncut, and tinted so you can tell one from
another. What a key is FOR is unchanged: you carry it along its cutting
trail, and every one of the five segments you cut is a check, as is placing
the finished key in its receptacle. That is 32 checks across the seven
towers, all of them earned by hand.

**And the key no longer opens anything.** The drawbridge, the map room, the
chairlift, the train, the tunnels, the Big Wall Door and the Spawn Secret
Door are separate items. So the two halves come apart: you might be riding the chairlift long
before its key reaches you, or cut all five segments of a key and still be
waiting on someone else to send you the ride.

The radio works like the puzzles rather than like the keys. Switching a
station on at its tower sends the check, but the music stays off until the
matching Radio Music item reaches you — so a station, like a gourd, is
something another player gives you. Turn `shuffle_radio_music` off in your
YAML to keep the vanilla radio, where a station plays the moment it is
switched on.

If you are not the host: only the host's game talks to Archipelago, and the
host's mod tells yours which stations have arrived, so your radio waits for
the same items and plays the same music. A guest without the mod gets the
vanilla radio.

The radio's dial has no labels, only a row of lights, so every station and
its music carry a number: its place on the dial, counted from the first
light. `Radio Station 01: Bobby` is the first light, `Radio Station 07:
Bristol` the last.

A few things are handed to you for free so a randomized run does not start
behind a wall: purple postgame gourds show on the map from the start, and the
sphere that normally blocks the true-ending path until you have beaten the
game once is removed.

The hub's three arch doors follow `start_with_arch_doors_open`, the list of
those open from the start; every other one is an item. By default only the
First Arch Door, the tutorial's way back to the hub, is open, and the two far
ones, left towards Sports Creek and right, are items: shortcuts that save long
detours. With `require_arch_doors` (on by default) what lies past them is behind
them in logic, so that you are not asked for a place on foot and sent the shortcut
afterwards: past the left door the Yellow, Blue and Black towers and the Green
Dome (never the chapel: the end of the game needs only the Big Wall Door), past the
right one the Green Tower and what is beyond the chairlift. A door that starts open
asks for nothing.

The doors are shortcuts, not walls: the island can still be walked round them.
But they do hold the logic. Until a door arrives, what lies past it is out of
logic, even once you have walked there: the generator never expects those checks of
you, and Universal Tracker shows them as out of reach. Turn `require_arch_doors` off
for logic that ignores the doors. Remove the First Arch Door from the list for a real
early game: you then leave the starting area through the Drawbridge or the
First Arch Door, one of which is always found early. Left open, most of the
island is reachable from the start.

### The Silent Gauntlet

`gauntlet_mode` is `vanilla` by default: the Gauntlet is as the game has it, and
has no checks. With `locked_stages` each of its seven stages is a check, and
each way up is an item.

In the game, a stage has two doors: its puzzle opens a wall inside the stage,
and the stairway to the next stage opens when every player holds its buttons
together. With `locked_stages` those buttons are gone. Solving a stage's puzzle
is still its check, but the wall and the stairway both stay shut until that
stage's item, **Gauntlet Stage 1 Door** to **Gauntlet Stage 7 Door**, has
arrived: nothing opens that does not lead on. The `big_goodbye` goal needs all
seven.

- It applies to every goal. With **Big Goodbye** the seven items are needed to
  win. With another goal the Gauntlet is extra: its checks are behind the
  chapel, so with the usual accessibility the Black Monolith Key and the seven
  items are always findable, but the goal does not ask for them.
- `gauntlet_puzzles_required` (on by default): the puzzle is still needed to
  open the wall; with the item, the wall opens as soon as the puzzle is solved,
  or at once if it already was. Turn it off and the item opens the wall too: you
  can do the puzzles in any order, or none, and walk straight to the top with the
  seven items. The puzzles stay checks.
- `lock_gauntlet_needs` (on by default, in `vanilla` too): with `lock_puzzle_needs`
  on as well, the parts inside the Gauntlet's stages (their buttons, panels and so
  on) are items as everywhere else, and the Big Goodbye goal asks for them, since
  finishing the Gauntlet means solving its stages. With `locked_stages` the stages'
  checks ask for them too. Turn it off to leave the Gauntlet's parts on the map and
  ask for none.
- `gauntlet_stages_local` (on by default): the seven items stay in your own
  world, so a stage is never held up by another game's item.

Everyone needs the same build of the mod.

### Puzzle parts

Off by default. With `lock_puzzle_needs` on, what a puzzle is built from stops
being scenery and becomes an item: its buttons, its panels, its speakers, its
timer. A puzzle is never behind an item of its own, and nothing stops you
walking in; it is simply missing the parts you have not been sent yet. They
are not on the map until their item arrives, and then they appear, for
everybody, wherever they are.

There are 17: **Buttons**, **Synchronized Buttons**, **Icon Panels**,
**Drawing Panels**, **Pose Panels**, **Sound Panels**, **Point Panels**,
**Speakers**, **Lights**, **Teapots**, **Timed Tomato**, **Golf Ball**,
**Big Head Mask**, **Ink Viewer**, **Counter**, **Coordinates Computer** and
**Terrain Pegs**. A part is global: Buttons turns on every puzzle button on the
island, for every player. The generator only counts on a puzzle once you
hold every part it needs, so a puzzle may be possible earlier than the logic
expects (you found a way round) but never the other way.

The tutorial's puzzles need parts too. So that you are never left with nothing
to do, you start with one of them, picked at random among the ones a tutorial
puzzle can be solved with (`start_with_random_puzzle_need`, on by default);
`start_with_puzzle_needs` lists parts you want from the start as well.

Left alone on purpose: door handles, the train's controls, the hub gates, the
lookouts, the radio stations, the Silent Gauntlet and everything at the end of
the game. Putting a puzzle's parts back is shown in the corner of the screen:
`Puzzle parts missing (12): buttons, icon panels, ...`.

**Everyone in a co-op group must run the same build of the mod** for this: the
host tells each guest what is still missing, and a guest on another build
sees every part and can use it.

## Checks and items added in 0.4

Three things on the island can now be checks, each with its own option, **on by default**:

- **`pack_sanity`**: the island's 6 backpacks, 5 belts and its gourd carton stay on the map, and
  the first time one is picked up it is a check (12 locations); the pack then disappears from the
  map for everybody. Named by color (**Sky Blue Backpack**, **Purple Belt**, ...), the carton
  **Gourd Carton Pickup**. The backpacks, belts and cartons you can wear or use are still items of
  the pool (`backpacks_in_pool`, `belts_in_pool`, `gourd_cartons_in_pool`). Off, they are on the map as
  in the game, free to take, and no check. `junk`: the same 12 checks, holding only filler or a trap. `vanilla`: each of the 12 checks gives the pack
  itself, which stays in the hands of whoever picked it up; no other location gives a pack, and the `*_in_pool`
  options do nothing.
- **`firework_sanity`**: firing each of the 8 firework launchers, with the button at its foot, for
  the first time is a check (8 locations): **Hub Fireworks**, **Peak Fireworks**, and so on.
- **`flare_gun_sanity`**: picking up each of the island's 4 flare guns is a check (4 locations,
  **Red**, **Green**, **Yellow** and **Blue Flare Gun Pickup**). The four guns come back as items of
  the pool, as many of each as `island_object_limits` says (1 by default). Off, the guns stay on the
  island as in the game and none is an item. `junk`: the same 4 checks, holding only filler or a trap.

The **Rainbow Flare Gun** is an item of the pool, once, in every seed: a white flare gun whose
shots flicker through every color. `island_object_limits` says how many of each flare gun the pool holds
(1 each by default; 0 leaves the Rainbow out); they are never random filler.

### The walkie-talkies in `island_object_limits`

`island_object_limits` says how many of each of the island's objects the filler draws first; the
walkie-talkie's default of **8** is how many the island itself has. The pool only draws as many as it
has room for (traps and bonuses, the gear above and the flare guns take their share first). A big pool
(Object or Light Sanity) goes past the limits once every object has reached its own, the object the
pool holds fewest of each time; an object at 0 stays out. Like every other filler
prop, a walkie-talkie does nothing for the logic: none is ever needed, it lands beside you when you
receive it, and its copies on the island are removed so one cannot also be found for free. Lower the
number, or put it at 0 to leave them out, for a pool with fewer of them.

## Added in 0.6

Every option below is **off by default**, except `monument_sanity` (7 checks, on): set it off for a 0.5 YAML's count.

### The players' bodies (`lock_abilities`)

With **`lock_abilities`** set to `handed` or `ambidextrous`, the players' bodies are items, and each one reaches
**every player of the slot at once**:

- **Legs**: jumping and crouching. The island past the tutorial needs both, so Legs is asked for to leave the
  starting zone (if the starting zone has no room left for it, you start with it).
- **Seat**: sitting on a chair, a couch, a bench or a stump, and on the floor. Turnstiles, telescopes, the
  chairlift, the train and the ending are never locked.
- **Progressive Arm**: raising the arm, then pointing, then extending it sideways. `handed`: three for the left
  arm, three for the right; `ambidextrous`: three **Progressive Arms**, both arms at once.
- **Throwing** and **Carrying** another player. Big Throw still throws while Throwing is locked.
- **Running**, with **`lock_running`** (useful only: the logic never asks for it).

**`start_with_abilities`** lists those you already have: `legs`, `seat`, `arms`, `throwing`, `carrying` (none by
default, as `start_with_puzzle_needs` for the puzzles' parts). One not listed is an item to find.

The gesture puzzles ask for what they use: Concert, Indoor Semaphore, Optical Telegraph, Charades Rooms,
Pointer's Paradise, Dancer and Selector and the Gauntlet's last stage the Seat and both arms extended,
Invisible Ink both arms raised, the Carousel and Cannonball Commute the Seat; throwing for Fielding, Window
Labyrinth, Basketball and Kick Up Pits; carrying for High Button, the two obbies and Pointer's Paradise.

### The island's objects and lights

- **`object_sanity`**: the island's gadgets stay on the map, and picking them up sends checks: `first_pickup`, the
  first of each kind (16 by default); `every`, each one (144 by default), which then leaves the map like a pack.
  **`object_sanity_kinds`** lists the kinds: walkie_talkie, radio, megaphone, binoculars, laser, torch, scanner,
  clock, compass, map, cowbell, lamp, paintbrush (the paintbrushes are left out by default and never leave the
  island). A kind left out leaves the island, except the lamps. **`object_sanity_items`**: `any`, `local` (this
  world's items that nothing needs) or `junk`. With `any`, **`object_sanity_junk_percentage`** (75 by default)
  of the checks, drawn at random, still hold only junk.
- **`light_sanity`**: a check the first time each light is switched on: `lamps` (83: those on the pedestals, in the
  towers, the tutorial...) or `lamps_and_switches` (with 19 wall switches: the Doof Chamber, the salons, the tutorial,
  the train and its stops). The puzzles' own lights are not counted. With it on, the lamps are lights, not objects.
  **`light_sanity_items`**: `any`, `local` or `junk`, and **`light_sanity_junk_percentage`**, as for the objects.
- **`zone_locks`**: with Object or Light Sanity, the island's lamps and gadgets locked by zone, the zone of the
  nearest coloured tower: 8 items (`Red Zone Lamps`, `Red Zone Gadgets`, and the same for green, blue and yellow). A
  zone's gadgets are off the map until its item arrives; its lamps and light switches stay usable but are out of logic
  until then. The tutorial's, the Map Room's, the chairlift's, the tunnels' and the train's keep the lock they have.
  **`start_with_zone_locks`** lists the zones you start with (`red_lamps`, `blue_gadgets`...), and
  **`zone_locks_local`** keeps the 8 items in your own world.

### Monuments and the game's own ways

- **`monument_sanity`** (on by default): a check when each monument is full (**Drawbridge Monument Full** and so on).
- **`linear_towers`**: each tower's key comes from its monument, as in the game. `all_deposit_boxes`: when the
  monument is full (the checks above exist then). `one_deposit_box`: once the monument holds a number of gourds
  drawn for each tower, between one and full (**Drawbridge Monument Key** and so on, which always hold the key).
- **`linear_keys`**: a placed key opens its door, as in the game (each key deposit holds its own door).
- **`linear_puzzles`**: every puzzle holds a gourd (it still arrives beside you, as an item), except the seven purple
  ones past the chairlift.
- **`start_with_map_room_open`**: the Map Room is open from the start.
- **`include_big_game_puzzles`**: the seven purple gourds' puzzles: `from_start` (as before, shown on the map),
  `when_unlocked` (in logic once an ending is reached, not on the map) or `disabled` (no checks).
- **`cut_key_sanity`**: the 25 key cuts: `on` (as before), `junk` or `off`.
- **`fast_golf`**: the golf ball (the cannonball) is no longer too heavy.

A short line on the overlay now says each check sent: what it was, what it gave, and to whom.

## Added in 0.7

Every option below is **off by default**, except `hint_board` (on). None of them changes the checks: a 0.6 YAML
generates the same seed's count.

- **`hint_board`** (on): the centre sign of the hub's notice board shows your hint points and a hint's cost, and lists
  the hints you asked for that are not found yet. Write an item's name on it to ask for a hint (it costs points, as
  `!hint`).
- **`tracker_board`**: the right sign of the notice board counts the checks you can do now, in logic, against those not
  done yet, and names a few: those a hint points to first, then those holding a progression item.
- **`map_tracker`**: the big map of the Map Room tracks the seed. Every check stands on it, a small copy of its object
  on a pin, and leaves it once done: `in_logic` shows only what you can do now, `all` every check not done, those out
  of logic greyed.
- **`linear_arch_doors`**: the hub's three arch doors are as in the game, opened by their push buttons on their far
  side; no item. You then leave the starting zone through the Drawbridge.
- **`cabin_fever_hide_timer`** and **`cabin_fever_long_hide_timer`**: the Cabin Fever rooms' dial reads "??:??" while
  the wait runs, now and then flashing one digit. The game's sound still plays over the last ten seconds.
- **Twelve new traps** (see Traps and bonuses below), and the Gourd Carton follows `gourd_name`.

## Added in 1.0

Every option below is **off by default**, except `custom_tiles` (`matching_games`), and none changes a 0.7 YAML's
checks.

- **`custom_tiles`**, **`custom_tile_choices`**, **`custom_tile_weights`**, **`custom_drawings`**: the puzzles' basic
  tiles shown as the community's drawings (see Custom tiles below).
- **`pack_sanity: junk`** and **`flare_gun_sanity: junk`**: the same checks, holding only filler or a trap.
  **`pack_sanity: vanilla`**: each pack's check gives the pack itself.
- **`linear_towers: one_deposit_box`**: a tower's key comes at a gourd drawn for it, at its **Monument Key** check.

## Other options

- **`start_with_drawbridge_open`**: the Drawbridge is open from the start (its key deposit stays a check).
- **`open_black_tower`** (on): the Black Tower's door is open from the start, instead of waiting for the five
  monuments.
- **`hint_keys`**: `from_start` hints the seven big keys at the start, as if they were in your Start Hints.
- **`tower_resync_stations`**: a resync station in each of the five towers (a button that brings your gourds, keys
  and gadgets lying around back in front of it; the hub's is always there). **`guests_can_resync`** lets the guests
  use them too.
- **`teleport_buttons`**: buttons in the hub to the five towers and the Silent Gauntlet: `free`, `with_towers` (once
  the place was opened on foot) or `items` (each also needs its Teleporter item). **`teleport_back_to_hub`** adds a
  button back in each destination.
- **`cabin_fever_seconds`** (300) and **`cabin_fever_long_seconds`** (1800): the two Cabin Fever waits, in seconds.
  **`cabin_fever_help`** and **`cabin_fever_long_help`** hide a button in each room that takes ten seconds off.
- **`tile_thief`**: a button beside the Tile Thief puzzle that makes peg tiles: `easy` (the tiles it expects) or
  `chaos` (one of every kind it draws from).
- **`gauntlet_stage_items`**, with `gauntlet_mode: locked_stages`: `progressive` (seven Progressive Gauntlet Doors)
  or `individual` (one door item per stage, in any order).
- **`death_link_roulette_hard_traps`** (on): the DeathLink roulette may draw the hard traps; off keeps them out.

## Colors

With **`random_colors`** (on by default) the seed draws between five and ten colors from the
mod's ten (red, orange, yellow, lime, green, cyan, blue, purple, pink, white), and the island's
**buoys** and **flare guns** are painted with them:

- a buoy: its body, its halo and the light it throws, one color per buoy, the same on every
  player's screen;
- a flare gun: its body and barrel, and its shot (the flare, its light and its smoke). The four
  island guns take the first four colors of the draw, and a gun you receive is painted like its
  kind. The item feed names a flare gun by its color: "Received: Pink Flare Gun".

**Only objects of the map are painted.** The players, the gourds and the keys are not touched. Turn
`random_colors` off to keep the game's own colors.

## Custom tiles

The nine basic peg tiles (the triangle, the asterisk, the diagonal line, the G, the dot and line, the three dots,
the S, the spiral and the e) can show other drawings for the seed. A tile only changes what it shows: every peg
tile, split-flap and display of that kind follows, on every player's screen, and the puzzles ask for the same tiles.

- **`custom_tiles: matching_games`** (the default): the drawings of the games in the multiworld join the nine basic
  tiles, and nine are drawn among them; nothing is forced. Without such a game, the game's tiles stay.
- **`all`**: nine drawn among every tile. **`custom_only`**: nine drawn among the drawings alone, none of the game's
  tiles stays. **`off`**: the game's tiles.
- **`custom_tile_choices`**: the tiles you want for sure (nine at most), whatever the mode and the weights:
  `custom_tile_choices: [pokeball, heart, cat]`.
- **`custom_tile_weights`**: how often each tile is drawn (0 leaves it out).
- **`custom_drawings`** (off): the community's scribbles for the drawing panels (the Poet's and the Pontiff's
  puzzles), each one in the place of one of the game's 45, drawn for the seed. None are in this version yet.

The drawings and the games that bring them: pelly (AP, Archipelago), jack (The Witness), strawberry (Celeste),
triforce (Zelda), semibot (R.E.P.O.), star (Mario), smile (Lingo), five (Dice), arrow (Grapple Dog), sword_in_stone
(Dark Souls), lodo (StarCraft), stones_above_banner (RuneScape), pokeball (Pokémon), tree (Terraria), creeper
(Minecraft), sigil (Blasphemous), chicken (Stardew Valley), planets (Risk of Rain), monster (Slay the Spire), joker
(Balatro), rune (DOOM), knight (Hollow Knight), blob_with_legs (Animal Well), gear (Factorio), hat (A Hat in Time),
heart (Undertale, Deltarune), tunic (Tunic), portal (Portal), cat (Rain World). A game matches when its name holds
one of these, with the same capitals. The spoiler log lists the seed's tiles.

The drawings are by **Jack5**, **Don Chalant**, **GameFraek** and **Kensufox**, under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); who drew which one is in the mod's repository,
`mod/assets/tiles/LICENSE.md`. They are fan art: the games belong to their owners. To propose a tile of your own:
`mod/assets/tiles/README.md` in the mod's repository.

## The gourds' name

**`gourd_name`** renames the gourds for the run, up to 24 characters: `gourd_name: Plumbus du
Destin`. Every game of the multiworld, the Text Client, Universal Tracker, the spoiler log and the
mod's overlay say that name, and `!hint Plumbus du Destin` finds them (`!hint Gourd` too). The
Gourd Carton follows them and becomes the **Plumbus du Destin Carton**; `!hint Gourd Carton`,
`!hint Gourd Carrier` and `!hint Plumbus du Destin Carrier` find it too. Only the names change: the
items are the same. The locations keep their names (Gourd Deposit 10, Gourd Carton Pickup).

The name belongs to the game, not to a slot: every Big Walk slot of a multiworld has to ask for the
same one, or they all keep "Gourd". A name that already belongs to another Big Walk item or item
group (Backpack, Traps...) keeps "Gourd" too.

## Traps and bonuses

Traps and bonuses are items that hit **every player of the session**, and they replace part of the
filler. By default **no trap** is in the pool (`trap_fill_percentage: 0`) and a quarter of the
filler is bonuses (`bonus_fill_percentage: 25`).

| Trap | What it does |
|---|---|
| **Big Drop** | everyone drops what they hold |
| **Big Throw** (hard) | everyone winds up, then throws what they hold; it cannot be cancelled |
| **Big Trip** (hard) | everyone is sent somewhere else on the map, each to a different place the logic has opened: the hub, a tower, a radio station, a solved puzzle, a launcher fired, a pack picked up. A timed tomato ticks, then pops as you go |
| **Big Meeting** (hard) | everyone is sent around one player drawn at random, in a ring, facing them |
| **Big Night** | the clock fast-forwards to midnight, the same hour for everyone, and runs on from there |
| **Big Sleep** (hard) | for `trap_duration` seconds, everyone falls asleep where they stand for two seconds out of seven |
| **Big Load** (hard) | every pack worn comes off and what it holds falls out |
| **Big Mask** (hard) | for `trap_duration` seconds, everyone wears the blindfold puzzles' helmet and nobody can take it off. A player already wearing a puzzle's mask is spared |
| **Big Flare** | a flare gun shot lands on every player, in the Archipelago colors. Each player can hide it on their own screen, see Gentle effects below |
| **Big Silence** | for `trap_duration` seconds, every player is muted, and the mute key cannot lift it |
| **Big Slide** | for `trap_duration` seconds, the ground is slippery: starting, stopping and turning slide |
| **Big Time** | every player gets a clock to keep, showing the real time of their own computer |
| **Big Ghost** | for `trap_duration` seconds, every player is white, as in the Silent Gauntlet (the color, not the silence) |
| **Big Paint** | for `trap_duration` seconds, the players swap colors (two swap, more go round) |
| **Big Air** (hard) | a timed tomato ticks in every player's hands, then everyone shoots up into the air (stopping under a ceiling) |
| **Big Goggles** | for `trap_duration` seconds, the field of view narrows |
| **Big Pixels** | for `trap_duration` seconds, the island turns to big square pixels |
| **Big Head** | for `trap_duration` seconds, the other players' heads grow |
| **Big Brain** | for `trap_duration` seconds, the other players' heads are a lit lamp |
| **Big Arms** | for `trap_duration` seconds, the players' hands grow (seen on the others) |
| **Big Smell** | for `trap_duration` seconds, every player gives off a green gas and the players are pushed apart |
| **Big Brother** | for `trap_duration` seconds, every player is small: body, view and collisions |
| **Big Reset** (hard) | the puzzle still holding its gourd nearest to each group of players starts again (its room's door shuts and opens) |
| **Big Break** | a folding chair appears under every player, who is sat on it |

| Bonus | What it does |
|---|---|
| **Big Speed** | everyone walks and runs faster for `trap_duration` seconds |
| **Big Jump** | everyone jumps higher for `trap_duration` seconds |
| **Big Day** | the clock fast-forwards to noon, and runs on from there |

`trap_weights` and `bonus_weights` set how often each comes (0 takes one out), `hard_traps` off keeps
only the standard traps, `trap_duration` sets how long the lasting ones last (20 seconds by default), and
`traps_spare_the_gauntlet` (on) keeps Big Trip and Big Meeting away from players inside the Silent
Gauntlet.

They reach you through the host. They go off one after another, about four seconds apart; one still
waiting when the game closes is kept in the save; a player who joins during a lasting one gets it for
the time that is left; and for 20 seconds after a world is ready or a player joins, Big Trip and Big
Meeting are let go, so nobody is moved while the others are still arriving.

## DeathLink

`death_link` is `off` by default; `send`, `receive` and `both` turn it on.

- **What sends one** (`death_link_triggers`): by default `puzzle_failed` — a wrong tile validated,
  buttons pressed out of order, a count gone wrong, a synchronised button let go, a dispenser's timed
  tomato run out, 29 puzzles in all. Add `big_fall` for a fall that leaves a player dazed. The message
  the other games read says what happened: "*slot* failed the Blindfold Catwalk Puzzle (buttons
  pressed out of order)". `death_link_amnesty` forgives that many triggers before one is sent.
- **What one does when it arrives** (`death_link_effect`): `knockout` by default, everyone drops what
  they hold, takes off their backpacks and belts (what they hold falls out) and is dazed as after a big
  fall; `drop` only drops; `roulette` plays a trap drawn from `death_link_trap_weights`.
- **Who it hits** (`death_link_target`): `everyone` by default, or `one_player`, the player who has
  been hit the fewest times so far this session (ties at random). With `one_player` and the roulette,
  the traps that need several players or hit the whole session are left out of the draw: **Big
  Meeting** (it gathers everyone around one player) and **Big Night** (the clock is the same for
  everyone). The others play on the one player: Big Drop, Big Throw, Big Trip, Big Sleep, Big Load,
  Big Mask and Big Flare.
- **`death_link_trap_on_send`**: with `death_link` on `send` or `both`, when Big Walk sends a DeathLink
  its own players take the effect of a death too (`death_link_effect`, on `death_link_target`).
  Normally a game only receives the link of the others' deaths; this makes the one who dies pay for it
  as well.

A knock-out the mod gives is not counted as a fall: it never sends a DeathLink back.

### TrapLink

`trap_link` (off by default) joins the TrapLink of the multiworld: the traps of the pool that reach you
are sent to every other game that has it on, and theirs play here. A trap another game sends plays as
the Big Walk trap of the same name when it is one, otherwise as the nearest by its name (a freeze is
**Big Sleep**, a banana is **Big Throw**, a bomb is **Big Flare**, a teleport is **Big Trip**, ...), and
as one drawn from the DeathLink roulette (`death_link_trap_weights`) when none comes near. Bonuses are
never sent. A trap that came from a DeathLink or from another game's TrapLink is not sent on, so
none loops, and a trap you send is not played again on your own screen.

## In-game settings

The mod adds a category to the game's own Settings, last in the list: **Settings > Archipelago**,
from the main menu or the pause menu. They are kept in the mod's `.cfg` on the machine that sets
them, and nothing there is in the YAML.

| Setting | Who | What it does |
|---|---|---|
| **Archipelago text** | each player | the size of the overlay's text, top left: Small (16), Medium (22), Large (28), Huge (36) |
| **DeathLink** | the host | On follows the YAML; Off sends and receives none for this session, whatever the YAML says. It reads "On (not in this seed)" when the seed has no DeathLink |
| **DeathLink amnesty** | the host | "Seed's" keeps the YAML's amnesty; 0 to 10 replaces it |
| **Gentle effects** | each player | Off by default. On, the traps that are hard to look at are softened on that screen (no Big Flare, Big Goggles or Big Pixels, Big Smell without its gas, Big Brain's lamps without their light), for players who find them hard to look at. The others keep theirs, and nothing else changes, the clock included |
| **Gourd name** | the host | free text, up to 24 characters, applied with Enter or by leaving the field. The item feed and the goal line say it instead of "Gourd" on every player's overlay, and "<name> Carton" for the Gourd Carton |

On a guest, the host's settings are greyed ("Set by the host") and show the host's value. The
**gourd name** lives in the mod only: the Archipelago server, the Text Client and the other games of
a multiworld still say "Gourd", because an item's name is fixed in the apworld.

## What is the goal of Big Walk when randomized?

Either one goal, or a list of **objectives** (below). The single goals, whichever you picked in your YAML:

- **Big Wall** — break the bell inside the Big Wall.
- **Big Goodbye** (default) — pass behind the Big Wall and complete the
  Silent Gauntlet. Saying farewell afterwards is up to you.
- **Big Game** — reach the secret ending, behind the Spawn Secret Door.
  Nothing else is in the way: the vanilla game seals that path until you
  have finished it once, and the mod removes the seal from your first
  session.
- **Big Collection** — place a set number of gourds in the towers' slots.

Whichever you picked, the corner of the screen names it for the whole session,
under the connection line, with the running count for **Big Collection**.

### Objectives

**`objectives`** adds things to do besides the `goal`, in any order: the game is won once the goal **and every
objective listed** are done. The goal is then the run's **ending**: `big_wall` (the chapel bell, a short seed without
the Gauntlet), `big_goodbye` (the Gauntlet, the default) or `big_game` (the secret ending); `big_collection` works too.
Left empty (the default), the goal alone applies, exactly as before, so an older YAML plays as it did.

**`random_objectives`** draws that many at random: among the objectives listed, or, if none are, among all of those
the other options allow (`big_radio` needs `radio_sanity`, `big_towers` a `tower_mode`, `big_objects`
`object_sanity`, `big_body` `lock_abilities`). 0 (the default) asks for the
whole list.

```yaml
Big Walk:
  goal: big_wall
  objectives: [big_keys, big_radio, big_climb, big_fiesta]
  random_objectives: 2   # two of these four, drawn per seed
```

- **big_collection**: place `gourds_required` gourds.
- **big_keys**: place `keys_required` big keys, the drawbridge's included.
- **big_radio**: switch on `radio_stations_required` radio stations (needs `radio_sanity`).
- **big_climb**: stand on the highest point of the island.
- **big_fiesta** (also read as big_club): the party, on the beach: plug a radio into the big speakers, with a music playing. A
  **Radio** is certain to be in the pool, and with `shuffle_radio_music` the Radio Music items (any one will do) become required.
- **big_lights**: switch on `lights_required` percent of the lights, each at least once. Without `light_sanity`, the
  lamps count for it, with no check of their own.
- **big_objects**: send `objects_required` percent of the object checks (needs `object_sanity`).
- **big_body**: receive `body_required` percent of the ability items, Running aside (needs `lock_abilities`).
- **big_towers**: complete `towers_required` towers, a tower being complete when its monument is full, in the
  seed's order (needs `tower_mode` `in_order` or `by_item`).

An ending plays **once per save**, so each objective is remembered when it happens, and the goal goes out
on the last of them, whatever the order. The objectives are on a **board in the hub**, above the resync
station's sign, ticked as they are done; **Settings > Archipelago > Objectives on screen** keeps them on the
overlay too. With **`lock_endings_until_objectives`** (on by default), the entry of the ending stays shut on every screen
until the other objectives are done: the Gauntlet's last stairway (the door to its bell) and the secret
room's printer.

### The towers, one after the other

**`tower_mode`** decides how the monuments take gourds:

- **all_open** (the default): every monument takes gourds from the start, in any order.
- **in_order**: **one monument at a time**, in an order the seed draws from all seven towers (the drawbridge
  and the Green Dome included). The next opens once the one before is full. A monument that is not the
  next refuses a gourd, and the button that opens its tower stays still.
- **by_item**: the same order, and the next monument also waits for a **Progressive Monument**, an item of
  the pool: the first monument is open from the start, and the k-th item opens the monument at position k + 1.

**`first_tower_open`** (on by default), with `by_item`: off, and the first monument waits for a Progressive Monument too, so the
seed starts with no monument open (seven items instead of six).

**`everyone_gathers`** (on by default): Big Climb and Big Fiesta count their ten seconds only while every player is there (at the
highest point; near the big speakers on the beach). Off, one player is enough.

With one monument at a time, the logic is exact: a gourd placed costs the doors of the towers before it and,
with `by_item`, their items. The monuments in the order, with each tower full, open or waiting, are on the
same board in the hub.

**`tower_sanity`** (on by default) adds **a check for reaching each of the seven towers**: made when the
tower's opening button is pressed (the two buttons at the top of the Black Tower held together), and for the
drawbridge and the Green Dome, which have none, when a player gets there.
**`hint_discovered_key_locations`** hints, at that moment, what sits at the tower's five key cuts and its key
deposit. **`hint_discovered_deposit_boxes`**, with `tower_mode`, hints the gourd deposits that sit in
its monument.

Both bells need two players hitting two buttons at once, and placing a gourd
in a tower's slot is a two-player action too. This world is not playable
solo, and a whole co-op group shares one slot — see the setup guide.

## What items and locations can get shuffled?

Locations, 131 of them on the default options:

- Each of the 45 puzzles, when you solve it.
- Each of the 25 key segments, as you cut it (`cut_key_sanity`). Five each on the drawbridge and
  the four colored towers; the Black Tower and Green Dome keys come
  already finished and have none.
- Each of the 7 big keys, when it goes into its plinth.
- Each of the 7 radio stations, when you turn it on (`radio_sanity`).
- Placing gourds in the towers' slots — every gourd, every fifth one, or
  none (`gourd_sanity`). Gourds are counted across all towers together,
  so it never matters which tower you walk to.
- Each of the 7 towers, when you reach it (`tower_sanity`), and each monument, when it is full
  (`monument_sanity`); with `linear_towers: one_deposit_box`, each tower's **Monument Key** too, which always
  holds its key.
- Each of the 12 packs, when first picked up (`pack_sanity`), each of the 8 firework launchers,
  when first fired (`firework_sanity`), and each of the 4 flare guns, when first picked up
  (`flare_gun_sanity`): see "Checks and items added in 0.4".
- Off by default: the island's gadgets (`object_sanity`), its lights (`light_sanity`), and the
  Silent Gauntlet's seven stages (`gauntlet_mode: locked_stages`).

Items:

- **Gourd** — fits any slot of any tower. There are exactly as many as
  there are slots: 45.
- The 7 features a big key used to open: **Drawbridge**, **Map Room**,
  **Chairlift**, **Train**, **Tunnels**, **Big Wall Door** and **Spawn Secret
  Door**. These are what actually open the island.
- The 7 big keys themselves — **Drawbridge Key**, **Red Funnel Tower Key**,
  **Green Cup Tower Key**, **Blue Castle Tower Key**, **Yellow Twist Tower Key**,
  **Black Monolith Tower Key** and **Green Dome Tower Key**, each named after the
  tower it belongs to, as the players call them, like its checks (**Red Funnel
  Tower Key Cut 1**, **Red Funnel Tower Key Deposit**). A key opens nothing; it is
  worth six checks, and it is the only way to reach them.
- The 7 Radio Music items, each of which starts one station playing
  (optional). They are named after the music itself, which the game never
  names anywhere, and numbered by the station's light on the dial.
- **First Arch Door**, **Left Arch Door** and **Right Arch Door**, each of
  which opens that door of the hub, for the doors `start_with_arch_doors_open`
  does not list.
- With `gauntlet_mode: locked_stages`, **Gauntlet Stage 1 Door** to **Gauntlet
  Stage 7 Door** (optional), one per stairway of the Silent Gauntlet.
- With `lock_puzzle_needs`, the 17 **puzzle parts** above (optional). Each is
  named after the part with "Unlock": **Buttons Unlock**, **Icon Panels
  Unlock**, and so on.
- Gear, set apart from the filler: **Backpack**, **Belt** and **Gourd Carton**
  (`backpacks_in_pool`, `belts_in_pool`, `gourd_cartons_in_pool`; with `pack_sanity: vanilla`, only the
  island's own, each on its own check), and the island's four flare guns
  (**Flare Gun**, **Blue Flare Gun**, **Green Flare Gun**, **Yellow Flare Gun**, with `flare_gun_sanity`)
  and the **Rainbow Flare Gun** (`island_object_limits`).
- Filler: a megaphone, a walkie-talkie, a laser, binoculars, a compass, a folding
  map, a portable radio, a torch or a scanner — the island's own hand props
  (how many of each: `island_object_limits`), dropped near you when you
  receive one. They are cosmetic only, and their vanilla copies are removed
  from the map so they can't also be found lying around for free.
- **Traps and bonuses**, in place of part of the filler: see "Traps and bonuses".
- **Lamp**, which is a marine buoy light. Same as above, except its vanilla
  copies stay in the world — this one is common enough scenery that removing
  it would be missed.

## Which items can be in another player's world?

Any of them.

## What does another world's item look like in Big Walk?

Nothing, in the world itself. Big Walk has no way to show you someone else's
item. The host's screen names the check as it goes out — "Check: Cabin
Fever" — but not what was inside it or who it went to; for that you need a
text client kept open beside the game, or the BepInEx log.

## When the player receives an item, what happens?

A gourd drops at the hub as a physical prop you can pick up and carry — or
straight into your hands, if you are already playing and they are free — and
so does a big key, uncut and tinted to tell it from the others. Whose hands
it goes to is decided the same way for both: a player whose hands are free first,
and if nobody's are, someone who then puts down what they hold. A feature
opens on the spot, wherever you are and whatever the matching key is doing:
the chairlift simply starts running. A Radio Music item starts its station
playing, and it stays available for the rest of the run. A filler prop lands
beside you; a trap or a bonus plays on every player at once. A puzzle part puts its objects back on the map, everywhere at
once. Everything else is silent.

`Ctrl+R` brings back anything of yours that has ended up somewhere you
cannot reach — gourds, keys and filler props alike, including whatever is in
someone's hands at the time. A key already placed in its receptacle and a
gourd already deposited are left where they are, because those checks have
already been sent.

Both the features and the radio are re-applied every time you load the world,
because the game has nowhere of its own to record them. If a door you own is
shut for a moment after a load, give it a second.
