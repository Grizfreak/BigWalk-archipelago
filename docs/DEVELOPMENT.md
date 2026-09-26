# Development

Working notes on the Archipelago integration for *Big Walk*. The player
documentation is elsewhere: [`README.md`](../README.md) to start,
[`SETUP.md`](../SETUP.md) for installing and playing, and the game page
[`apworld/bigwalk/docs/en_Big Walk.md`](../apworld/bigwalk/docs/en_Big%20Walk.md)
for what the randomization does.

## The two halves

- [`mod/`](../mod/README.md) — the BepInEx mod (IL2CPP, Harmony) that installs
  into the game: check detection, received items materialized in the world,
  and the hosting screen extended with the Archipelago fields. Design:
  [`mod/architecture-mod.md`](../mod/architecture-mod.md).
- [`apworld/`](../apworld/README.md) — the Python Archipelago world
  (`bigwalk`): locations, items, logic, options.
- [`apworld/protocol.md`](../apworld/protocol.md) — **the contract between the
  two**: ids, `slot_data`, what the C# client sends and applies. Read it
  before touching either half.
- [`tools/`](../tools/) — the tooling. `tools/deploy-mod.ps1` builds and deploys
  to every install, then prints a fingerprint and says whether they agree;
  `tools/package-mod.ps1` produces the player zip; `python tools/testroom.py`
  rebuilds the apworld, generates a seed from `tools/players/` and hosts the
  room, printing the details to type in game (it refuses a port another room
  already holds). `solo-smoke.yaml` validates the chain in a few minutes,
  `solo-full.yaml` is a real game, and `coop-test.yaml` has a slot name of
  its own so it does not pollute the counters.
- **Co-op on one PC** (2026-09-26): with `Debug.Enabled` on, host a world and
  press Ctrl+L — a second instance of the game starts windowed and joins as a
  real guest over 127.0.0.1. Ctrl+T switches window, Ctrl+C calls the guest
  over. `tools/launch-guest.ps1` starts the same guest from a terminal. See
  [`COOP-TESTS.md`](COOP-TESTS.md), Setup, for what still takes two machines.

Reverse engineering and design decisions:
[`mod/reverse-engineering-notes.md`](../mod/reverse-engineering-notes.md) (how
the game works and how the mod hooks into it) and
[`apworld/design-decisions.md`](../apworld/design-decisions.md) (why the world
is shaped the way it is).

**What to do next**: [`ROADMAP.md`](ROADMAP.md) — the to-do list toward 1.0, a short list on top and the detail behind each item below it.

**To pick the work back up after a break**:
[`NEXT-SESSION.md`](NEXT-SESSION.md) — where the project stands, what is left
to test, the open questions and the leads deliberately not taken.
[`COOP-TESTS.md`](COOP-TESTS.md) — the two-machine test plan, ordered by
risk.

## Building

```
dotnet build mod/BigWalkArchipelago.sln     # the mod
python apworld/build.py                     # dist/bigwalk.apworld
pwsh tools/package-mod.ps1                  # dist/BigWalkArchipelago-*.zip
```

The artefacts in `dist/` and `apworld/dist/` are only as fresh as the last
run of those scripts. The two halves are versioned together
(`Plugin.PluginVersion`, the `.csproj`, `archipelago.json`,
`WORLD_VERSION`): a mod older than its apworld ignores fields in `slot_data`,
which makes a seed unplayable rather than crashing.

World tests, from a source checkout of Archipelago where `worlds/bigwalk` is
a junction to `apworld/bigwalk`:

```
set AP_TEST_WORLDS=bigwalk
python -m pytest worlds/bigwalk/test -q     # the world's own tests
python -m pytest test/general -q            # Archipelago conformance
```

## State of the work

- [x] Mod: scaffolding, check detection, received items materialized, the
      hosting screen with the Archipelago fields
- [x] apworld: the world is complete, generation validated on Archipelago
      0.6.7 and 0.6.8, `.apworld` packaged
- [x] Archipelago network client (`mod/src/Core/Net/`) — connection, items,
      check reporting, goal; validated against a real room
- [x] Every path exercised in game: an outgoing check from a real puzzle, an
      incoming item, deposits, goal, gourds rebuilt, surviving a network
      drop, recovering on a fresh save
- [x] Two-player session (2026-09-20) and all three goals confirmed end to
      end (2026-09-21): `gauntlet`, `ending`, `deposits`
- [x] Radio stations as items (2026-09-21), exercised in game end to end
      (see `apworld/protocol.md` §11)
- [x] Big keys (2026-09-21): the door and the key are two separate items, 25
      cut checks and 7 deposits. Built and exercised **solo only**
- [x] Filler is the island's own objects, their vanilla copies removed from
      the map (2026-09-22)
- [x] On-screen Archipelago status and item feed, an Archipelago switch and
      a connection test on the hosting screen, and the slot name locked to
      the save's own name (2026-09-22). All exercised in game the same day,
      except the rule that keeps Ctrl+R off worn and stowed props, which
      cannot be exercised alone — see [`COOP-TESTS.md`](COOP-TESTS.md) test 15
- [x] The big keys on two machines — [`COOP-TESTS.md`](COOP-TESTS.md)
- [x] The first alpha's feedback (0.1.1, 2026-09-26), tested with the
      loopback guest except test 24, which needs the alpha host's save
- [ ] A seed played from the first check to the goal
