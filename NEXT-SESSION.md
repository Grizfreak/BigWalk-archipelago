# Where this stands, and what to do next

*Written at the end of the session of 2026-09-18. The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is the short version and the
to-do list.*

## Co-op is done

Everything that needed a second machine has been exercised, on 2026-09-20,
with both installs deployed from the same build (`tools/deploy-mod.ps1`
prints the hash and says whether they match — use it, three tests were
wasted in one weekend on a guest running older code).

Confirmed with a second player:

- A gourd the host receives appears **in the host's hands** on the guest's
  screen, with the right colour and gravity.
- A gourd arriving while the host's hands are full goes to the nearest
  player with free hands, and both players see it in the right pair.
- **Ctrl+R works while the guest is holding one**, from either side.
- The guest **deposits into a monument** and the host records it
  (`ap_home_monoumentIntroSlot0 = 1`), the server credits the check, and
  an item comes back for it.
- The guest **leaves and rejoins**: their gourds are rebuilt.
- The **Archipelago server dying** does not break the Mirror session — the
  host simply gets a notice on screen.
- A gourd taken from a sealed box by the guest is released from their
  hands by `StaleHeldPropReleaser`.

## Everything on the list has now been tested

The solo half, on 2026-09-21:

- **Radio stations fire.** `[Check] FmStationBreathwork` — seven locations
  that had never triggered once in the life of the project, working first
  try on code that had never executed.
- **`Archipelago/Enabled = false`** falls back cleanly:
  `Archipelago disabled in the config; checks are logged locally only`,
  zero connection attempts, zero errors, and every other component still
  running.
- **The `deposits` goal** reports. Worth noting what the counting showed:
  only four deposits happened in that session for a threshold of five —
  the fifth was the guest's, from the day before, restored at load. The
  count is cumulative across sessions, which is the intended model and had
  never been demonstrated.
- **The `gauntlet` goal** reports — the apworld's DEFAULT, and the path
  most players will take, reached legitimately for the first time.
  `GauntletComplete changed during play (unknown -> 1)`, latched in the
  middle of a Ctrl+G, right after `NHoldLogic 2` was forced: the bell's
  N-hold, which is exactly what DebugPeckCombinatorForce was written for.

All three goals are now confirmed end to end — `ending` (EndingGate 1 -> 2),
`deposits`, and `gauntlet`.

## Decided, not yet built: radio music as an Archipelago item

Turning a station on reports the check AND grants the station. Every other
check in this world grants nothing locally — gourds are hidden and
unspawned, big keys arrive from Archipelago — so the radio is the only one
that rewards itself. Not a bug: nothing breaks and the seed stays
beatable. An inconsistency.

The work is two halves that must ship together, because it changes the
contract in `protocol.md`: seven `Radio Station: …` items in the apworld
with seven fewer filler to balance (the stations are already clean data,
`data.py` RADIO_STATIONS), and on the mod side suppressing the local
unlock the way GourdStatePatch suppresses a gourd, plus writing the flag
when the item arrives.

**Start in Ghidra, not in-game**: who reads `FmStation*`, and does writing
it back to 0 actually stop the music and leave the radio in a sane state?
That is one decompilation against one full test cycle — see the lesson at
the bottom of this file.

## Why two local instances do not work

Tried on 2026-09-18: the game authenticates through EOS with the Steam
identity, and two sessions of the same identity on one machine do not both
get a ticket (`Failed to get auth ticket`, then `Failed to Init Host Menu`).

The lead, if this ever becomes worth an hour or two: **`KcpTransport
initialized!` appears in every log**, on both machines. The game carries a
direct UDP transport besides EpicTransport. Forcing the NetworkManager onto
it with a direct address would let two local instances talk over 127.0.0.1
without EOS at all — but the hosting screen is gated behind EOS auth, so
that would have to be bypassed too. It is a mod of its own, not a setting.

A solo mod that swaps one connection between two PlayerCharacters
(`ReplacePlayerForConnection`) cannot help either: every remaining question
is "does this replicate to a second client", and it has only one.

## Open questions that could still bite

- **Does the tutorial drawbridge really gate the way out?** The world
  precollects the Tutorial Key on that assumption
  (`start_with_tutorial_key`). If it is wrong, the key can be shuffled.
- **Are all 58 puzzles reachable without any big key?** The region graph
  assumes the map is open apart from the ending.
- **Do `FmStation7/8/9` exist at all?** Assumed not, and left out.
- **`ap_reported_*` is not scoped to a seed**, so a save reconnected to a
  *different* seed resends checks earned elsewhere. Harmless in real use,
  where a save belongs to one seed — but it will skew a count during
  testing. This is why `coop-test.yaml` uses a slot name of its own.
- **Radio stations** (seven locations) have still never fired once.

## Leads deliberately not taken

- **Monument fill state in Archipelago's `DataStorage`**, so a new save
  recovers its deposits instead of only its items.
- **A real in-world button for the gourd resync** instead of Ctrl+R.
- **Traps.** The item and the YAML option exist, the effect does not.
- **Per-tower monument locations (Option C/D)**, the big-key decomposition,
  and Key Cutters as checks.

## Lessons worth carrying over

**A successful build does not prove an edit was applied.** An edit script
failed an assertion and wrote nothing; `dotnet build` then compiled the
unchanged source and reported success, which read as confirmation. The
change was believed done for two days. Check the file, not the build status.

**A tool that says nothing when it finds nothing costs more than it saves.**
`DebugPeckCombinatorForce` returned silently when everything was out of
range, which is indistinguishable from a key that does not work — an hour
went into remapping keys for a problem that did not exist.

**Debug hotkeys are unreliable, and not only through collisions.** BigTV
owns F7 through F11 and wins every one, so tools bound there are never
reached. Beyond that, four plain keys — End, Keypad1, Keypad2, F1 — have
never once registered, while Keypad0, Keypad4, F2, F4, F5, Delete and
Ctrl+R all work. NumLock was the obvious suspect and is ruled out, since
the keypad answers. No explanation, and it is not worth inventing one:
**`<letter> + LeftControl` is the proven format** (it is what Ctrl+R uses),
so bind new tools that way. Hours went into this, twice, because "the tool
printed nothing" and "the key was never seen" look identical from outside
— which is why both combinator keys now log before and after the call.

**Ctrl+G is a sledgehammer.** At the 120m radius it forced fifteen
combinators in one press, `PosesBlockedSystem` and `TurnStileSystem`
included. Fine on a test room, wrong on a save anyone cares about.

**When the question is "what does this function do", it has a static
answer — go and read it.** A full day went into inferring the pick-up
chain from method names (`PickUp`, `Drop`, `SetLoose`, `SetHeld`), one
in-game two-machine round trip per guess. Ghidra is in this repo, the
project is already analysed, and `analyzeHeadless` with a Java script
(this Ghidra has no Python) decompiled the answer in twenty minutes:
`PlayerHands.PickUp` and `Prop.SetHeld` are both purely local, and the
server half of the game's own Command, `UserCode_CmdPickUp`, writes
`NetworkplayerHeldInformation` — the only field another machine sees. The
guess being tested at the time would have changed nothing.

**When something only misbehaves for the second player, suspect authority
before suspecting the network.** Three separate bugs this weekend were the
same mistake in different clothes: a write performed on the machine that
does not own the state — `hands.Drop` host-side on a remote player,
`PickUp` client-side on oneself, and a scene-object clone spawned under an
assetId no client could resolve.
