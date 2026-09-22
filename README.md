# Big Walk (Archipelago)

An [Archipelago](https://archipelago.gg) randomizer for *Big Walk*
(House House), in two halves that ship together:

- **the mod** — a BepInEx plugin that connects the game to an Archipelago
  server by itself. There is no separate client to run while you play.
- **the apworld** — the Python world (`bigwalk`) that generates the seed.

→ **[How to install and play](SETUP.md)**

**Version 0.1.0 — alpha.** Every path has been exercised in game: checks
going out, items coming back, big keys cut and placed, and all three goals
reached. What is thin is mileage. No one has played a seed from first check
to goal, and the big-key half has only ever run solo. Expect to find things,
and please report them.

**Big Walk is co-op, and so is this.** A whole co-op group shares one
Archipelago slot, and no goal here can be reached alone — see
[Playing together](SETUP.md#playing-together).

## Where is the options page?

Big Walk is not on the Archipelago website, so there is no hosted options
page. Install the apworld and use **Generate Template Options** in the
Archipelago Launcher instead: it writes `Players/Templates/Big Walk.yaml`,
with every option documented in the comments. [SETUP.md](SETUP.md) walks
through it, and the table below is the short version.

## What does randomization do to this game?

Solving a puzzle no longer hands you its gourd. The puzzle still opens, and
solving it still sends a check out to the multiworld, but the gourd itself is
gone — the only gourds you will ever hold are the ones other players send
you. They arrive at the hub as ordinary, pickable props, and they are
generic: any gourd fits any monument slot.

That changes what the monuments are for. Filling one is no longer the reward
for clearing a tower's puzzles, and it no longer releases that tower's key
either: it is simply how you spend the gourds Archipelago gives you, and
every deposit is a check.

**The big keys are items too.** A key arrives from the multiworld and drops
at the spawn point like a gourd — uncut, and tinted so you can tell one from
another. What a key is *for* is unchanged: you carry it along its cutting
trail, and every one of the five segments you cut is a check, as is placing
the finished key in its receptacle. That is 32 checks across the seven
towers, all of them earned by hand.

**And the key no longer opens anything.** The drawbridge, the map room, the
chairlift, the train, the tunnels, the dam and the Hub Secret Door are
separate
items. So the two halves come apart: you might be riding the chairlift long
before its key reaches you, or cut all five segments of a key and still be
waiting on someone else to send you the ride.

The radio works like the puzzles rather than like the keys. Switching a
station on at its tower sends the check, but the music stays off until the
matching **Radio Music** item reaches you. Turn `radio_station_items` off in
your YAML to keep the vanilla radio, where a station plays the moment it is
switched on. One wrinkle if you are not the host: only the host's game talks
to Archipelago, so only the host's radio waits for the item.

A few things are handed over for free so a randomized run does not start
behind a wall — the hub shortcuts are open from a save's first session, the
purple postgame gourds show on the map from the start, and the sphere that
normally blocks the true-ending path until you have beaten the game once is
removed.

## What is the goal?

Whichever of these you picked in your YAML:

- **gauntlet** (default) — break the bell at the top of the Gauntlet.
- **ending** — break the chapel bell.
- **second_ending** — reach the game's other ending, the one behind the Hub
  Secret Door. Nothing else stands in the way: in the vanilla game that path is
  sealed until you have finished the game once, and the mod removes the seal
  from your first session.
- **deposits** — deposit a set number of gourds into the towers' monuments.

Whichever you picked, the corner of the screen says so for the whole session,
under the connection line — with the running count for `deposits`, and in
orange if the mod is too old to detect the goal your seed asks for.

Both bells need two players hitting two buttons at once, and depositing a
gourd in a monument is a two-player action as well. Nothing here is playable
solo.

`second_ending` needs the Hub Secret Door, so it cannot be played with
`green_dome_deposits: excluded`. Asking for both generates as `key_only`
instead, and says so.

## What items and locations get shuffled?

Locations — 93 of them on the default options:

- each of the **45 puzzles**, when you solve it;
- each of the **25 key segments**, as you cut it. Five each on the drawbridge
  and the four coloured towers; the Black Monolith and Green Dome keys arrive
  already finished and have none;
- each of the **7 big keys**, when it goes into its plinth;
- each of the **7 radio stations**, when you turn it on (optional);
- **depositing gourds** into monuments — every deposit, every fifth one, or
  none (optional). Deposits are counted across all monuments together, so it
  never matters which tower you walk to.

Items:

- **Gourd** — the generic monument currency. There are exactly as many as
  there are monument slots in play: 45, or 30 without the Green Dome's.
- the **7 features** a big key used to open — Drawbridge, Map Room,
  Chairlift, Train, Tunnels, Chapel Door and Hub Secret Door. These are what
  actually open the island.
- the **7 big keys** themselves, each named after the feature it fits. A key
  opens nothing; it is worth six checks, and it is the only way to reach them.
- the **7 Radio Music** items, one per station (optional). They are named
  after the music itself: the game's dial shows numbers and no names, so this
  is the only place a station is ever named.
- **filler** — the island's own hand props. A megaphone, a walkie-talkie, a
  backpack, a belt, a flare gun (plain, blue, green or yellow), a laser,
  binoculars, a compass, a folding map, a portable radio, a gourd carton, a
  torch or X-ray goggles, dropped near you when one arrives. They are
  cosmetic, and their vanilla copies are taken off the map so they cannot
  also be found lying around for free. **Lamp** — a marine buoy light — is
  the exception: its vanilla copies stay, being common enough scenery that
  removing them would be missed.

## Which items can be in another player's world?

Any of them.

## What does another world's item look like in Big Walk?

Nothing, in the world itself. Big Walk has no way to show you someone else's
item. The host's screen names the check as it goes out — "Check: Cabin
Fever" — but not what was inside it or who it went to; for that you need a
text client kept open beside the game, or `BepInEx/LogOutput.log`.

## When the player receives an item, what happens?

A gourd drops at the hub as a physical prop you can pick up and carry — or
straight into your hands, if you are already playing and they are free — and
so does a big key, uncut and tinted to tell it from the others. A feature
opens on the spot, wherever you are and whatever the matching key is doing:
the chairlift simply starts running. A Radio Music item starts its station
playing, and it stays available for the rest of the run. A filler prop lands
beside you. Everything else is silent.

**Ctrl+R** brings back anything of yours that has ended up somewhere you
cannot reach — gourds, keys and filler props alike, including whatever is in
someone's hands at the time. A key already placed in its receptacle and a
gourd already deposited are left where they are, because those checks have
already been sent.

Both the features and the radio are re-applied every time you load the world,
because the game has nowhere of its own to record them. If a door you own is
shut for a moment after a load, give it a second.

## Options

| YAML key | What it does | Default |
| --- | --- | --- |
| `goal` | `gauntlet`, `ending`, `second_ending` or `deposits` | `gauntlet` |
| `deposit_goal_amount` | Gourds to deposit for the `deposits` goal | `30` |
| `green_dome_deposits` | `full`, `key_only` to drop the postgame tower's 15 slots but keep its key, or `excluded` to drop both | `full` |
| `deposit_locations` | `all`, `milestones` (every fifth) or `none` | `milestones` |
| `radio_station_checks` | Switching a station on awards a check | on |
| `radio_station_items` | The music itself is shuffled into the pool | on |
| `start_with_drawbridge_open` | Start with the Drawbridge already open instead of finding it | off |
| `trap_fill_percentage` | Share of filler replaced by traps. No trap does anything yet | `0` |

Two presets ship with the world: **Full Run** (everything on) and **Short**
(no postgame tower, the chapel bell, milestone deposits only).

## What is in this repository

- [`mod/`](mod/) — the BepInEx plugin (IL2CPP, Harmony): check detection,
  received items materialized in the world, and the Archipelago fields added
  to the hosting screen.
- [`apworld/`](apworld/) — the Python world: locations, items, logic, options,
  and [`protocol.md`](apworld/protocol.md), the contract between the two
  halves.
- [`tools/`](tools/) — build, deploy and test-room scripts.
- [`SETUP.md`](SETUP.md) — installing and playing.
- [`DEVELOPMENT.md`](DEVELOPMENT.md) — building both halves, the design notes
  and the state of the work (in French).

## Reporting something

Open an issue on
[GitHub](https://github.com/Grizfreak/BigWalk-archipelago/issues) and attach
`BepInEx/LogOutput.log` from the **host's** machine — that is the only one
that talks to Archipelago.

## AI disclosure

Some of the code, data and documentation here was produced with an LLM. It
was not vibe-coded: it was used to speed up the slow parts — reading the
decompiled game to find hooks and function implementations, and assisting
with the mod and client code — and to keep the documentation in step with
what the code does. Everything it produced was reviewed and tested in game.
