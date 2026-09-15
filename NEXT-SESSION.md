# Where this stands, and what to do next

*Written at the end of the session of 2026-09-15, so none of it has to be
rediscovered. The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is only the short version and the
to-do list.*

## Done, and confirmed in-game

The apworld generates (Archipelago 0.6.7 and 0.6.8, all option combinations)
and the mod plays it. Every path has been watched working in the real game:
the mod loads, connects from the hosting screen, reports a check from a
puzzle actually solved, receives gourds and big keys, reports monument
deposits and the goal, rebuilds the player's gourds at session start,
survives the server being killed mid-session, and recovers onto a brand-new
save.

Run a room in one command: `python tools/testroom.py` (add `--resume` to
re-host the previous seed with its history, which is what a save needs in
order to reconnect to it).

## Still to test, solo — all of it is code that has never run

1. **Radio stations.** Seven locations, and the detection widened into
   `SaveValuePatch` for them has never executed once. Turn a station on,
   expect `[Check] FmStation…`.
2. **The `gauntlet` and `ending` goals** — and note `gauntlet` is the
   *default*, so the untested path is the one most players will take. Both
   go through `ApGoalFlags` latching `GauntletComplete`/`EndingGate`, which
   has never latched anything; only the `deposits` goal was exercised. The
   **PageDown** debug key forces both flags. **Do this last on any given
   room**: reaching the goal triggers the server's auto-release, which
   checks every location and makes that room useless for measuring
   anything else.
3. **Minor**: the warning flash when the hosting screen's connection test
   fails (only the success path has been seen in-game), and
   `Archipelago/Enabled = false`.

## Still to test, co-op — nothing here has met a second player

Both players need BepInEx and all three DLLs, the same mod build and the
same game version. The guest configures nothing; only the host connects.
Keep the same host for the whole run — the save holds the checks, the items
and the deposits.

Watch three things, in this order of interest:

1. Does the guest **see** the cosmetic gourds the host receives? (Network
   spawn, should replicate.)
2. Is a **deposit made by two players** detected host-side?
3. Does **Ctrl+R** clear a gourd held by the *guest*? The sweep looks at
   every player, and the gourd will certainly be destroyed, but
   `hands.heldProp` for a remote player and `Drop()` called host-side on
   their hands are both unverified. Worst case their hands briefly believe
   they still hold it; the fix would be to let the guest's own mod do the
   drop.

## Open questions that could still bite

- **Does the tutorial drawbridge really gate the way out?** The world
  precollects the Tutorial Key on that assumption
  (`start_with_tutorial_key`). If it is wrong, the key can be shuffled and
  the option turned off.
- **Are all 58 puzzles reachable without any big key?** The region graph
  assumes the map is open apart from the ending. If some tower turns out to
  be genuinely locked, its puzzles need their own region — which needs the
  puzzle → tower mapping nobody has established.
- **Do `FmStation7/8/9` exist at all?** Assumed not, and left out. Three
  missing checks if wrong, not a broken seed.
- **`ap_reported_*` is not scoped to a seed**, so a save reconnected to a
  *different* seed resends checks earned elsewhere. Harmless in real use,
  where a save belongs to one seed — but it will skew a check count during
  testing.

## Leads deliberately not taken

- **Monument fill state in Archipelago's `DataStorage`**, so a new save
  recovers its deposits instead of only its items. Reasoned through in
  `design-decisions.md`; left alone because it moves game state out of the
  save file and nobody knows what should happen when the two disagree.
- **A real in-world button for the gourd resync** instead of Ctrl+R. Would
  mean cloning a `PeckSwitch` and intercepting its peck — feasible, the mod
  already clones UI and props, but a project of its own. The hotkey is not
  discoverable and only works for the host.
- **Traps.** The item and the YAML option exist, the effect does not, which
  is why the default is 0%.
- **Per-tower monument locations (Option C/D)**, the big-key decomposition,
  and Key Cutters as checks — all still recorded in
  `design-decisions.md`, none started.

## The lesson worth carrying over

Seven bugs were found in one play session, and **not one of them was
visible from reading the code** — several sat in behaviour I had described
confidently and never watched. "It reconnects on its own" was wrong three
times in a row.

Two of them were the same mistake: attaching to the network session state
that actually describes the world. When something is reset, ask whether a
world reload or a reconnection is the event that invalidates it.

And one measurement trap: never judge an outgoing check on a room that has
been goaled. The auto-release checks everything, so the question answers
itself.
