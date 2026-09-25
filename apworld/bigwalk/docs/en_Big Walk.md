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

One wrinkle if you are not the host: only the host's game talks to
Archipelago, so only the host's radio waits for the item. A guest hears a
station as soon as somebody switches it on.

A few things are handed to you for free so a randomized run does not start
behind a wall: the hub shortcuts are open from the first session on a save,
purple postgame gourds show on the map from the start, and the sphere that
normally blocks the true-ending path until you have beaten the game once is
removed.

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
  (optional). They are named after the music itself: the game's dial shows
  numbers and no names, so this is the only place a station is ever named.
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
so does a big key, uncut and tinted to tell it from the others. A feature
opens on the spot, wherever you are and whatever the matching key is doing:
the chairlift simply starts running. A Radio Music item starts its station
playing, and it stays available for the rest of the run. A filler prop lands
beside you. Everything else is silent.

`Ctrl+R` brings back anything of yours that has ended up somewhere you
cannot reach — gourds, keys and filler props alike, including whatever is in
someone's hands at the time. A key already placed in its receptacle and a
gourd already deposited are left where they are, because those checks have
already been sent.

Both the features and the radio are re-applied every time you load the world,
because the game has nowhere of its own to record them. If a door you own is
shut for a moment after a load, give it a second.
