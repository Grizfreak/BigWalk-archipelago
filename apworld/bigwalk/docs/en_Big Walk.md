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
**Eggs**. A part is global: Buttons turns on every puzzle button on the
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
  in the game, free to take, and no check.
- **`firework_sanity`**: firing each of the 8 firework launchers, with the button at its foot, for
  the first time is a check (8 locations): **Hub Fireworks**, **Peak Fireworks**, and so on.
- **`flare_gun_sanity`**: picking up each of the island's 4 flare guns is a check (4 locations,
  **Red**, **Green**, **Yellow** and **Blue Flare Gun Pickup**). The four guns come back as items of
  the pool, as many of each as `island_object_limits` says (1 by default). Off, the guns stay on the
  island as in the game and none is an item.

The **Rainbow Flare Gun** is an item of the pool, once, in every seed: a white flare gun whose
shots flicker through every color. `island_object_limits` says how many of each flare gun the pool holds
(1 each by default; 0 leaves the Rainbow out); they are never random filler.

### The walkie-talkies in `island_object_limits`

`island_object_limits` caps how many of each of the island's objects the filler holds, and the
walkie-talkie's default of **8** can look like a lot to ask. It is not: the island itself has 8, so
that is the most the filler can ever draw of them, and the pool only draws as many as it has room for
(traps and bonuses, the gear above and the flare guns take their share first). Like every other filler
prop, a walkie-talkie does nothing for the logic: none is ever needed, it lands beside you when you
receive it, and its copies on the island are removed so one cannot also be found for free. Lower the
number, or put it at 0 to leave them out, for a pool with fewer of them.

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

## The gourds' name

**`gourd_name`** renames the gourds for the run, up to 24 characters: `gourd_name: Plumbus du
Destin`. Every game of the multiworld, the Text Client, Universal Tracker, the spoiler log and the
mod's overlay say that name, and `!hint Plumbus du Destin` finds them (`!hint Gourd` too). Only the
name changes: the gourds are the same items.

The name belongs to the game, not to a slot: every Big Walk slot of a multiworld has to ask for the
same one, or they all keep "Gourd". A name that already belongs to another Big Walk item or item
group (Backpack, Plumbus, Traps...) keeps "Gourd" too.

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
| **Gentle effects** | each player | Off by default. On, the flashes of the traps are not shown on that screen (today Big Flare's), for players who find them hard to look at. The others keep theirs, and nothing else changes, the clock included |
| **Gourd name** | the host | free text, up to 24 characters, applied with Enter or by leaving the field. The item feed and the goal line say it instead of "Gourd" on every player's overlay |

On a guest, the host's settings are greyed ("Set by the host") and show the host's value. The
**gourd name** lives in the mod only: the Archipelago server, the Text Client and the other games of
a multiworld still say "Gourd", because an item's name is fixed in the apworld.

## What is the goal of Big Walk when randomized?

Whichever of these you picked in your YAML:

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

Both bells need two players hitting two buttons at once, and placing a gourd
in a tower's slot is a two-player action too. This world is not playable
solo, and a whole co-op group shares one slot — see the setup guide.

## What items and locations can get shuffled?

Locations, 117 of them on the default options:

- Each of the 45 puzzles, when you solve it.
- Each of the 25 key segments, as you cut it. Five each on the drawbridge and
  the four colored towers; the Black Tower and Green Dome keys come
  already finished and have none.
- Each of the 7 big keys, when it goes into its plinth.
- Each of the 7 radio stations, when you turn it on (optional).
- Placing gourds in the towers' slots — every gourd, every fifth one, or
  none (`gourd_sanity`). Gourds are counted across all towers together,
  so it never matters which tower you walk to.
- Each of the 12 packs, when first picked up (`pack_sanity`), each of the 8 firework launchers,
  when first fired (`firework_sanity`), and each of the 4 flare guns, when first picked up
  (`flare_gun_sanity`): see "Checks and items added in 0.4".

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
  (`backpacks_in_pool`, `belts_in_pool`, `gourd_cartons_in_pool`), and the island's four flare guns
  (**Flare Gun**, **Blue Flare Gun**, **Green Flare Gun**, **Yellow Flare Gun**, with `flare_gun_sanity`)
  and the **Rainbow Flare Gun** (`island_object_limits`).
- Filler: a megaphone, a walkie-talkie, a laser, binoculars, a compass, a folding
  map, a portable radio, a torch or X-ray goggles — the island's own hand props
  (the most of each is `island_object_limits`), dropped near you when you
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
