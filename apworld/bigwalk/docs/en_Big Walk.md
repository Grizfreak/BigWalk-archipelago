# Big Walk

## Where is the options page?

The [player options page](../player-options) lets you configure your personal
options and export a config file from them.

## What does randomization do to this game?

Solving a puzzle no longer hands you its gourd. The puzzle still opens, and
solving it sends a check out to the multiworld, but the gourd itself is gone —
the only gourds you will ever hold are the ones other players send you. They
arrive at the hub as ordinary, pickable props, and they are generic: any gourd
fits any monument slot.

That changes what the towers' monuments are for. Filling one is no longer the
reward for clearing that tower's puzzles, and it no longer releases that
tower's key either: it is simply how you spend the gourds Archipelago gives
you, and every deposit is a check.

**The keys are items too.** A big key arrives from the multiworld and drops
at the spawn point like a gourd, uncut, tinted so you can tell one from
another — they are otherwise the same yellow blank. What a key is FOR is
unchanged: you carry it along its cutting trail, and every one of the five
segments you cut is a check, as is placing the finished key in its
receptacle. That is 32 checks across the seven towers, all of them earned by
hand.

**And the key no longer opens anything.** The chairlift, the train, the map
room, the tunnels, the drawbridge, the dam and the Green Dome are separate
items. So the two halves come apart: you might be riding the chairlift long
before its key reaches you, or cut all five of that key's segments and still
be waiting on someone else to send you the ride.

The radio works like the puzzles rather than like the keys. Switching a station
on at its tower sends the check, but the music stays off until the matching
Radio Music item reaches you — so a station, like a gourd, is something another
player gives you. Turn `radio_station_items` off in your YAML to keep the
vanilla radio, where switching a station on plays it straight away.

One wrinkle if you are not the host: only the host's game talks to Archipelago,
so only the host's radio waits for the item. A guest hears a station as soon as
somebody switches it on.

A few things are handed to you for free so a randomized run does not start
behind a wall: the hub shortcuts are open from the first session on a save,
purple postgame gourds show on the map from the start, and the sphere that
normally blocks the true-ending path until you have beaten the game once is
removed.

## What is the goal?

Whichever of these you picked in your YAML:

- **Gauntlet** (default) — break the bell at the top of the Gauntlet, the
  game's true ending.
- **Ending** — break the chapel bell.
- **Deposits** — deposit a set number of gourds into the towers' monuments.

Both bells need two players hitting two buttons at once, and depositing a gourd
in a monument is a two-player action too. This world is not playable solo.

## What items and locations get shuffled?

Locations:

- Each of the 45 puzzles, when you solve it.
- Each of the 7 big keys, when it goes into its plinth.
- Each of the 25 key segments, as you cut it. Five each on the drawbridge and
  the four coloured towers; the Black Monolith and Green Dome keys come
  already finished and have none.
- Each of the 7 radio stations, when you turn it on (optional).
- Depositing gourds into monuments — every deposit, every fifth one, or none
  (optional). Deposits are counted across all monuments together, so it never
  matters which tower you walk to.

Items:

- **Gourd** — the generic monument currency. There are exactly as many as there
  are monument slots in play.
- The 7 features a big key used to open: **Drawbridge**, **Map Room**,
  **Chairlift**, **Train**, **Tunnels**, **Dam** and **Green Dome**. These
  are what actually open the island.
- The 7 big keys themselves — **Chairlift Key**, **Map Room Key** and so on,
  each named after the feature it fits. A key opens nothing; it is worth six
  checks, and it is the only way to reach them.
- The 7 Radio Music items, each of which starts one station playing
  (optional). They are named after the music itself: the game's dial shows
  numbers and no names, so this is the only place a station is ever named.
- Postcards, souvenir pebbles and novelty keychains, which do nothing at all.

## Which items can be in another player's world?

Any of them.

## What does another world's item look like in Big Walk?

Nothing, in the world itself. Big Walk has no way to show you someone else's
item, so you will only see it in the BepInEx log, or in a text client if you
keep one open.

## When the player receives an item, what happens?

A gourd drops at the hub as a physical prop you can pick up and carry, and so
does a big key — uncut, and tinted to tell it from the others. A feature
opens on the spot, wherever you are and whatever the matching key is doing:
the chairlift simply starts running. A Radio Music item starts its station
playing, wherever you are, and it stays available for the rest of the run.
Everything else is silent.

`Ctrl+R` brings back anything of yours that has ended up somewhere you
cannot reach — gourds and keys alike. A key already placed in its receptacle
is left where it is, because that check has already been sent.

Both the features and the radio are re-applied every time you load the world,
because the game has nowhere of its own to record them. If a door you own is
shut for a moment after a load, give it a second.
