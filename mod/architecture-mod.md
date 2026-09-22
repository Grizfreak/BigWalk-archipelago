# Architecture — BigWalkArchipelago (BepInEx IL2CPP mod)

## The guiding principle

Three responsibilities are kept strictly apart, because nothing about them
has any reason to be coupled:

1. **Detection** — noticing that a check has just been validated in game
   (Harmony hooks).
2. **Reporting** — deciding what to do about it: log it locally, or tell an
   Archipelago server.
3. **Debug tooling** — the conveniences used while developing (flight,
   dumps, forced unlocks), entirely independent of the first two.

The point was that the day a real Archipelago network client arrived, **not
one line in the game's hooks would need touching** — only a new
implementation of the reporting interface. That is what happened:
`Core/Net/ApReporter` replaced `Core/LocalLogReporter` and `Patches/` did not
change.

## Folder structure

```
mod/
├── BigWalkArchipelago.sln
├── src/
│   ├── Plugin.cs             # BasePlugin entry point: loads Harmony, binds the
│   │                         #   config, adds every component below
│   ├── Config.cs             # BepInEx.Configuration — the [Archipelago] and
│   │                         #   [Debug] sections
│   │
│   ├── Core/                 # What the mod does, independent of any patch
│   │   ├── ICheckReporter.cs         # void ReportCheck(string locationId)
│   │   ├── LocalLogReporter.cs       # Implementation #1: writes to the log
│   │   ├── GourdRegistry.cs          # SaveablePropName <-> Archipelago location
│   │   ├── CheckTracker.cs           # What this save has already reported
│   │   ├── ItemApplier.cs            # One received item -> one effect in game
│   │   ├── ReceivedItemSpawner.cs    # Cosmetic gourds: clone, neutralize, spawn
│   │   ├── GadgetItemSpawner.cs      # The same, for the filler hand props
│   │   ├── VanillaGadgetRemover.cs   # ...and their vanilla copies, removed
│   │   ├── KeyCustody.cs             # Big keys: granted, held back, resynced
│   │   ├── KeyFeatures.cs            # The doors those keys used to open
│   │   ├── KeyColours.cs             # Telling five identical yellow keys apart
│   │   ├── RadioStations.cs          # A station's music as an item
│   │   ├── ArchDoorUnlocker.cs       # Hub shortcuts, open from the first session
│   │   ├── SecondEndingSphereUnlocker.cs
│   │   ├── VariantGourdRevealer.cs   # Purple postgame gourds, shown
│   │   ├── VariantGourdMapUnlocker.cs#   ...and on the map too
│   │   ├── CosmeticMonumentFillTracker.cs  # Deposits, counted globally
│   │   ├── StaleHeldPropReleaser.cs  # Hands that think they hold a dead prop
│   │   └── Net/                      # The Archipelago client proper
│   │       ├── ApRuntime.cs          # The main loop; the only place network
│   │       │                         #   state meets the game
│   │       ├── ApConnection.cs       # The socket, on its own threads
│   │       ├── ApConnectionTest.cs   # The throwaway login the menu runs
│   │       ├── ApEndpoint.cs         # host:port + slot + password, parsed
│   │       ├── ApSlotData.cs         # What the apworld sent (../apworld/protocol.md)
│   │       ├── ApLocationIds.cs      # Id arithmetic, both directions
│   │       ├── ApItemCursor.cs       # What this SAVE has already materialized
│   │       ├── ApGoalFlags.cs        # Watching for the goal
│   │       ├── ApReporter.cs         # Implementation #2 of ICheckReporter
│   │       ├── ApStatusOverlay.cs    # The corner of the screen (IMGUI)
│   │       └── ApNotices.cs          # The lines under it
│   │
│   ├── Patches/              # Harmony hooks. These know only ICheckReporter.
│   │   ├── GourdStatePatch.cs             # RewardGourd.ServerSetGourdState
│   │   ├── SaveValuePatch.cs              # SaveManager.SetIntValue — the safety net
│   │   ├── KeyBlankCutPatch.cs            # The 25 cut segments
│   │   ├── PropPinDoorPatch.cs            # A placed key opening nothing
│   │   ├── PropHomeEnablePatch.cs
│   │   ├── BroadcastStationUnlockPatch.cs # A station's check, without its music
│   │   ├── HostMenuConfirmPatch.cs        # The host:port field on the hosting screen
│   │   ├── HostMenuArchipelagoControls.cs # ...and the toggle and test button
│   │   ├── HostMenuConfirmStartPatch.cs   # The probe run on Continue
│   │   └── PlayerArmsScreenSpacePatch.cs
│   │
│   └── Debug/                # Off by default (Debug.Enabled). Dumps, forced
│                             #   unlocks, flight — never load-bearing.
│
└── lib/                      # Local references, NOT committed (lib/README.md)
```

## Why the separation

- **`Patches/` only ever knows `ICheckReporter`**, never a concrete
  implementation. A patch calls `Plugin.Reporter.ReportCheck(id)` without
  knowing what happens next.
- **`GourdRegistry` and `Net/ApLocationIds`** own the translation between the
  game's internal identifier (`saveablePropName`, e.g. `gourdCabinFever`) and
  the Archipelago location. Keeping it in one place is what made it a
  one-line change to drop the thirteen puzzles that turned out not to exist
  in the shipped build.
- **`Debug/` depends on nothing else**, so the whole module can be switched
  off with a single bool in `Config.cs` without touching anything, and a
  release can ship with no cheats reachable.
- **The `SaveValuePatch` safety net** exists because the reverse-engineering
  sessions confirmed we do not know every code path that can reach
  `ServerSetGourdState` — the exact trigger from solving a puzzle is a
  UnityEvent and cannot be traced statically. Two detection points
  converging on the same `ICheckReporter` is insurance against a path nobody
  anticipated.
- **`Core/Net/ApRuntime` is the only place** where network state meets the
  game, and everything it does runs on Unity's main thread. `ApConnection`
  parks incoming items in a concurrent queue and flips a status flag; the
  runtime reads them. That split is not stylistic — touching a game object
  from one of the client library's threads is a native crash with no managed
  stack.

## Where the work stands

The build order this file used to prescribe has been carried out. What is
done and what is not is tracked in [README.md](README.md), and the contract
between this mod and the Python world is
[`../apworld/protocol.md`](../apworld/protocol.md).
