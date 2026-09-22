# BigWalkArchipelago

BepInEx mod (IL2CPP, Harmony) for *Big Walk* (House House): it plugs an
Archipelago multiworld client into the game loop. See
[architecture-mod.md](architecture-mod.md) for the design.

## Build

Requirements: the .NET SDK (tested with 10.0.301) and the local DLLs in
`lib/` — [lib/README.md](lib/README.md) says where each one comes from.

```
dotnet build BigWalkArchipelago.sln
```

Targets `net6.0` to match the BepInEx IL2CPP runtime shipped inside the game
(`.NET 6.0.7`, confirmed in `BepInEx/LogOutput.log`).

## Deployment

Copy **three** DLLs from `src/bin/Debug/net6.0/` into
`<game folder>/BepInEx/plugins/BigWalkArchipelago/`:

- `BigWalkArchipelago.dll` — the mod
- `Archipelago.MultiClient.Net.dll` — the official Archipelago client
- `Newtonsoft.Json.dll` — pulled in by the one above

The last two come from the `Archipelago.MultiClient.Net` NuGet package and
are copied into the build output automatically. BepInEx resolves a plugin's
dependencies inside that plugin's own folder, so leaving either of them out
fails the load of the whole mod, not just the connection.

`tools/deploy-mod.ps1` does all of that and prints a fingerprint that says
whether the installs agree; `tools/package-mod.ps1` produces the player zip
(BepInEx included) described in [`../SETUP.md`](../SETUP.md).

On this machine the game is installed at:
```
F:\SteamLibrary\steamapps\common\Big Walk\
```
(launch through Steam, `steam://run/1478500` — running the executable
directly does not load BepInEx on this machine).

## State of the work

- [x] 1. Scaffolding and a minimal `Plugin.cs` loading Harmony
- [x] 2. `ICheckReporter`, `LocalLogReporter`, `GourdRegistry`
- [x] 3. `GourdStatePatch` wired to `ICheckReporter`
- [x] 4. `Debug/DebugFlightTool` (`PlayerCheater`/`CameraCheatMover`
      investigation)
- [x] 5. `SaveValuePatch` as a safety net
- [x] 6. Archipelago network client (`src/Core/Net/`) — connection, incoming
      items, check reporting, goal. Contract and validation:
      [`../apworld/protocol.md`](../apworld/protocol.md)
- [x] 7. Loading and connecting from inside the game confirmed (2026-09-15)
- [x] 8. Every path exercised in game (2026-09-15): outgoing check, items,
      deposits, goal, gourds rebuilt, network drop, fresh save
- [x] 9. Two-player session (2026-09-20): gourds received, a deposit made by
      the guest, Ctrl+R from both sides, the Archipelago server dying
- [x] 10. Radio stations as items (2026-09-21) and big keys (2026-09-21) —
      the door and the key are two separate items, 25 cuts and 7 deposits.
      The big keys have only ever been exercised **solo**
- [x] 11. Filler: the island's own hand props, materialized on receipt and
      removed from the map (2026-09-22)
- [ ] 12. The big keys on two machines ([`../COOP-TESTS.md`](../COOP-TESTS.md))
- [ ] 13. A seed played from the first check to the goal

## Archipelago configuration

The `.cfg`'s `[Archipelago]` section: `Enabled` (drops the connection and
falls back to logging checks locally, as before the network client existed)
and `HostPort` (remembers the last address typed). The slot name and the
password are the two fields on the hosting screen, read back from the save.

`Enabled` also has a switch on the hosting screen itself, beside a test
button (`Patches/HostMenuArchipelagoControls.cs`, both cloned from
`ContinueButton` and positioned relative to it rather than absolutely).
Switching it off greys the host field and restores the game's own labels on
the two repurposed fields, via `LocalizedText.Refresh()`. The test button
runs the same probe Continue has always run, and shows the result in words;
it never blocks hosting. Its result must not auto-continue the session,
which is what `ProbeFromTestButton` guards — `HostMenuConfirmStartPatch`
starts the game on `TestStatus.Ok`, which is right for a probe Continue
started and wrong for one a button did.

Comfort: `SpawnGourdAtPlayer` and `PutGourdInHands` (a gourd received during
play lands in your hands or in front of you; the restock at the start of a
session always goes to the hub), `GourdColor`, `GourdRestoreInterval`.

On-screen display (`Core/Net/ApStatusOverlay.cs`, IMGUI, host only):
`ShowConnectionStatus` is the master switch — the status line stays up for
the whole session rather than appearing only when something is wrong, and
reads `Archipelago: off` when the switch is off rather than showing nothing,
since silence and "all is well" used to look identical. `StatusFontSize`
sizes it, `ShowItemFeed` adds the last checks sent and items received
underneath (`Core/Net/ApNotices.cs`, where repeats collapse into `x N` so the
burst replayed on connect does not fill the screen), `NoticeSeconds` sets how
long those live, and `ShowResyncHint` shows the resync shortcut as it is
actually bound — hidden while disconnected, where the key would only answer
"refused".

The slot name field is locked once the save exists, because it is the save's
own name and renaming it repoints the save at another Archipelago slot.
Switching the toggle off unlocks it, and an edit made there is discarded if
Archipelago is switched back on. A rename that was genuinely committed is
reported instead of prevented: the screen reads the slot the save last
connected under, which `Core/Net/ApItemCursor` has always recorded beside the
seed, and says so.

`ResyncGourdsKey` (**Ctrl+R** by default, connected host only) puts back
within reach everything Archipelago has given you and that is lying around:
gourds outside a monument, big keys not yet in their plinth
(`Core/KeyCustody.ResyncToSpawn`) and filler props
(`Core/GadgetItemSpawner.DestroyLooseCosmeticGadgets`, plus the per-session
tally reset in `ApRuntime.ResyncGadgets`). Anything in a player's hands is
dropped before it is destroyed — a prop destroyed inside a hand leaves that
hand believing it still holds it, which is exactly the stuck object this is
meant to clear. Use it when something has become unreachable: stranded in a
sealed puzzle room, say. Monument deposits and placed keys are never
touched, so nothing Archipelago counts can be lost by pressing it.
