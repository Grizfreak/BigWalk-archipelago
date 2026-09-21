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
reward for clearing that tower's puzzles; it is how you spend the gourds
Archipelago gives you. A full monument still releases its big key, and placing
that key still opens what it always opened — the map room, the chairlift, the
train, the tunnels, the way to the ending.

Big keys work the other way around from gourds: each one is still itself. A
"Red Funnel Key" received from the multiworld really is the Red Funnel's key,
and the mod places it in its plinth for you, opening the door on the spot.

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

- Each of the 58 puzzles, when you solve it.
- Each of the 7 big keys, when it goes into its plinth.
- Each of the 7 radio stations, when you turn it on (optional).
- Depositing gourds into monuments — every deposit, every fifth one, or none
  (optional). Deposits are counted across all monuments together, so it never
  matters which tower you walk to.

Items:

- **Gourd** — the generic monument currency. There are exactly as many as there
  are monument slots in play.
- The 7 big keys.
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

A gourd drops at the hub as a physical prop you can pick up and carry. A big
key goes straight into its plinth, and whatever that key opens opens
immediately. A Radio Music item starts its station playing, wherever you are — and it
stays available for the rest of the run. Everything else is silent.
