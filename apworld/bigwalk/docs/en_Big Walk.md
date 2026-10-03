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
afterwards: past the left door the Yellow, Blue and Black towers, the chapel and the
Green Dome, past the right one the Green Tower and what is beyond the chairlift. A
door that starts open asks for nothing, and the island can still be walked round
either way. Remove the First Arch Door from the list for a real
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

Locations, 93 of them on the default options:

- Each of the 45 puzzles, when you solve it.
- Each of the 25 key segments, as you cut it. Five each on the drawbridge and
  the four coloured towers; the Black Tower and Green Dome keys come
  already finished and have none.
- Each of the 7 big keys, when it goes into its plinth.
- Each of the 7 radio stations, when you turn it on (optional).
- Placing gourds in the towers' slots — every gourd, every fifth one, or
  none (`gourd_slot_checks`). Gourds are counted across all towers together,
  so it never matters which tower you walk to.

Items:

- **Gourd** — fits any slot of any tower. There are exactly as many as
  there are slots: 45.
- The 7 features a big key used to open: **Drawbridge**, **Map Room**,
  **Chairlift**, **Train**, **Tunnels**, **Big Wall Door** and **Spawn Secret
  Door**. These are what actually open the island.
- The 7 big keys themselves — **Drawbridge Key**, **Red Tower Key**,
  **Green Tower Key**, **Blue Tower Key**, **Yellow Tower Key**, **Black
  Tower Key** and **Green Dome Key**, each named after the tower it belongs
  to, like its checks (**Red Tower Key Cut 1**, **Red Tower Key
  Deposit**). A key opens nothing; it is worth six
  checks, and it is the only way to reach them.
- The 7 Radio Music items, each of which starts one station playing
  (optional). They are named after the music itself, which the game never
  names anywhere, and numbered by the station's light on the dial.
- **First Arch Door**, **Left Arch Door** and **Right Arch Door**, each of
  which opens that door of the hub, for the doors `lock_arch_doors` keeps
  closed.
- With `gauntlet_mode: locked_stages`, **Gauntlet Stage 1 Door** to **Gauntlet
  Stage 7 Door** (optional), one per stairway of the Silent Gauntlet.
- With `lock_puzzle_needs`, the 17 **puzzle parts** above (optional). Each is
  named after the part with "Unlock": **Buttons Unlock**, **Icon Panels
  Unlock**, and so on.
- Filler: a megaphone, a walkie-talkie, a backpack, a belt, a flare gun
  (plain, blue, green or yellow), a laser, binoculars, a compass, a folding
  map, a portable radio, a gourd carton, a torch or X-ray goggles — the
  island's own hand props, dropped near you when you receive one. They are
  cosmetic only, and their vanilla copies are removed from the map so they
  can't also be found lying around for free.
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
beside you. A puzzle part puts its objects back on the map, everywhere at
once. Everything else is silent.

`Ctrl+R` brings back anything of yours that has ended up somewhere you
cannot reach — gourds, keys and filler props alike, including whatever is in
someone's hands at the time. A key already placed in its receptacle and a
gourd already deposited are left where they are, because those checks have
already been sent.

Both the features and the radio are re-applied every time you load the world,
because the game has nowhere of its own to record them. If a door you own is
shut for a moment after a load, give it a second.
