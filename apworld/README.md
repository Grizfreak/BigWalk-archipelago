# apworld/

The Python Archipelago world for *Big Walk*. The code lives in
[`bigwalk/`](bigwalk/) and packages into `dist/bigwalk.apworld`.

- [`protocol.md`](protocol.md) — **the contract with the mod**: location and
  item ids, the contents of `slot_data`, what the C# client has to send and
  apply. Read it before touching either half.
- [`design-decisions.md`](design-decisions.md) — the history of the design
  decisions (goal, monument model, big keys, softlocks, community feedback).
- [`bigwalk/docs/`](bigwalk/docs/) — the player documentation as the
  Archipelago webhost renders it: the game's page and the setup guide. The
  two files at the root of the repository ([`../README.md`](../README.md),
  [`../SETUP.md`](../SETUP.md)) say the same thing; all four move together.
- [`../mod/reverse-engineering-notes.md`](../mod/reverse-engineering-notes.md)
  — how the game works internally, which everything below depends on.

## State

- [x] The Python world is complete (locations, items, logic, options, docs,
      tests)
- [x] Real generation validated: 3 slots, Archipelago 0.6.8 (source) and
      0.6.7 (local install), with the packaged `.apworld` included
- [x] The mod's Archipelago network client — connected and played both solo
      and with two players (see `protocol.md`)
- [x] Radio stations as items (`radio_station_items`, 2026-09-21): both
      halves written, generation validated, and the whole chain exercised in
      game (see `protocol.md` §11)
- [x] Big keys (2026-09-21): the door and the key are two separate items, 25
      cut checks and 7 deposits. Exercised in game, but solo only (see
      [`../COOP-TESTS.md`](../COOP-TESTS.md))
- [x] Thirteen puzzle locations removed (2026-09-21): they exist in the
      game's metadata but nothing in the shipped build produces them, so
      generation could place progression on a check that can never be sent.
      45 puzzles remain (`data.ABSENT_FROM_THE_BUILD`)

## Build

```
python build.py
```

Produces `dist/bigwalk.apworld`, to be copied into the `custom_worlds/`
folder of an Archipelago installation (0.6.7 or newer).

## Development and tests

To work with Archipelago's own tests, link the world's folder into a source
checkout of Archipelago rather than copying it:

```
mklink /J "<checkout>\worlds\bigwalk" "<here>\apworld\bigwalk"
```

Then, from the checkout:

```
set AP_TEST_WORLDS=bigwalk
python -m pytest worlds/bigwalk/test -q     # the world's own tests
python -m pytest test/general -q            # Archipelago conformance
```

(The `test/webhost` tests need Flask, which is not on this machine.)

## What the world does

**Locations** — 45 puzzles, the 25 big-key cut segments, 7 big-key deposits,
7 radio stations (optional), and the gourd deposits at the monuments (none,
every fifth, or all — an option). 93 locations on the default options. Gourd
deposits are counted globally and never per tower, which is what makes the
model immune to the softlock identified on 2026-09-11 (Option A).

**Items** — one generic `Gourd` item, in as many copies as there are monument
slots in play (30 or 45); the 7 features the big keys used to open
(`Drawbridge`, `Map Room`, `Chairlift`, `Train`, `Tunnels`, `Dam`, `Green
Dome`); the 7 big keys themselves; the 7 `Radio Music: …` items that make
each station audible (optional); and filler — the island's own hand props,
materialized on receipt and taken off the map so they cannot be picked up for
free. The Drawbridge is handed over at the start by default.

**Logic** — the key and the door are two separate items. A big key opens
nothing: it carries six checks (five cuts and a deposit), and those six
locations depend on it and nothing else. The features are what open the
island, and three of them really guard something: `Chairlift` and `Tunnels`
each enclose locations (measured in game on 2026-09-21), and `Dam` — the
Black Monolith tower's feature — opens the ending zone. No puzzle is locked
by the mod. The number of gourds received now gates only the gourd deposits
themselves.

**Goal** — `gauntlet` (default), `ending` or `deposits`.

The assumptions the logic makes that have never been verified in game are
listed at the end of [`protocol.md`](protocol.md) — they are what decides
whether a generated seed is actually finishable.
