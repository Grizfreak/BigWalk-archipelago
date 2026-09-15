# Big Walk — mod ↔ Archipelago contract

*Written for whoever implements the mod's Archipelago network client (see
`../mod/reverse-engineering-notes.md`, "Archipelago network client" — still the
one missing piece). Companion to the Python world in [`bigwalk/`](bigwalk/):
this file and `bigwalk/world.py`'s `fill_slot_data` are one unit, change them
together.*

**Status (2026-09-15): implemented, and connecting from the running game.**
The client lives in `../mod/src/Core/Net/`. The contract below is validated
against a real Archipelago room — login as game `Big Walk`, slot_data
round-trip, id arithmetic, a check accepted by the server, and the
starting-inventory item delivered on connect — and the mod has since been
confirmed to load under BepInEx IL2CPP and reach a server from inside the
game.

Not yet exercised in-game, worth knowing before trusting a real run: the
received-items cursor across a reconnect (§5), deposit thresholds (§7) and
goal reporting (§6).

## 1. Connecting

Only the **host** connects. Big Walk's save data belongs to the host (every
write goes through a Mirror `[Server]` method), so the host is the only
process that can honestly report checks or apply items.

| Connect field | Value |
|---|---|
| `game` | `Big Walk` |
| `name` | the hosting screen's **Slot name** field (`SaveData.slotName`) |
| `password` | the hosting screen's **Archipelago password** field |
| server | `ApSessionConfig.HostAndPort` |
| `version` | 0.6.0 or above (`required_client_version` in `world.py`) |
| `items_handling` | `0b111` — other worlds, own world, and starting inventory |
| `tags` | `[]` |

`items_handling` must include the starting-inventory bit: the Tutorial Key is
precollected by default and arrives that way.

## 2. slot_data

Sent on every connection. Read it before applying anything.

| Field | Type | Meaning |
|---|---|---|
| `world_version` | str | apworld version, e.g. `"0.1.0"`. Log a warning on a mismatch with what the mod was built against; do not refuse to connect. |
| `goal` | str | `"gauntlet"`, `"ending"` or `"deposits"`. See §6. |
| `deposit_goal_amount` | int | Deposits needed to win when `goal == "deposits"`. Already clamped to what exists; use it as-is. |
| `deposit_locations` | str | `"none"`, `"milestones"` or `"all"`. Informational — `deposit_location_amounts` is the authoritative list. |
| `deposit_location_amounts` | int[] | Ascending deposit counts that are checks, e.g. `[5, 10, ..., 45]`. Possibly empty. |
| `green_dome_deposits` | str | `"full"`, `"limited"` or `"excluded"`. |
| `radio_station_checks` | bool | Whether the seven radio stations are checks. |
| `total_monument_slots` | int | Gourd items in circulation for this slot (30, 36 or 45). |
| `big_keys_in_play` | str[] | `SaveablePropName` names of the big keys this slot uses. Six entries when the Green Dome is excluded. |
| `location_id_base` | int | 8600000. See §3. |
| `radio_id_offset` | int | 1000. |
| `deposit_id_offset` | int | 2000. |
| `gourd_item_id` | int | 8600001. |

## 3. Location ids

Ids are derived from the game's own enum values on purpose, so the mod does
arithmetic instead of shipping a table that would drift from `bigwalk/data.py`.
`B` below is `location_id_base`.

| Category | Id | Count |
|---|---|---|
| Puzzle | `B + (int)SaveablePropName` (100–159) | 58 |
| Big key deposit | `B + (int)SaveablePropName` (300–306) | 7 |
| Radio station | `B + 1000 + (int)SavableSystem` (30–36) | 7 |
| Gourd deposit N | `B + 2000 + N`, N from 1 | up to 45 |

In C#, starting from what `ICheckReporter.ReportCheck(string)` already
receives:

```csharp
if (Enum.TryParse<SaveablePropName>(locationId, out var prop))
    id = Base + (int)prop;
else if (Enum.TryParse<SavableSystem>(locationId, out var system))
    id = Base + RadioOffset + (int)system;
```

**Never invent ids.** The `Connected` packet carries `missing_locations` and
`checked_locations`; drop any id that is in neither. That single rule covers
every case where the world has fewer locations than the game can report:
radio stations turned off, the Green Dome excluded, and the three unused
`FmStation7/8/9` values.

**`gourdSecretZoneVice` (290) is not a location.** It is the only
gourd-prefixed value with no matching `valetXxx` home and it was never seen in
play, so the world leaves it out. `GourdRegistry.BuildLocationIds` currently
still includes it — exclude it there too, or the mod will try to report an id
nothing is listening for.

## 4. Item ids

| Item | Id | Count |
|---|---|---|
| `Gourd` | 8600001 | = `total_monument_slots` |
| Big keys | `B + (int)SaveablePropName` (300–306) | 6 or 7 |
| Filler (Postcard, Souvenir Pebble, Novelty Keychain) | 8609001–8609003 | rest of the pool |
| Trap (Untied Shoelace) | 8609101 | 0 by default |

What to do on receipt:

- **`Gourd`** → `ItemApplier.ApplyGourdItem()`. Spawns a cosmetic prop at the
  hub and nothing else: no `SaveManager` write, no check reported. This is the
  only currency that fills monuments.
- **A big key** → `ItemApplier.ApplyBigKeyItem(propName)`, where `propName`
  comes back from `id - B`. Writes the save, pins the key live into its plinth,
  and reports its own location (§7).
- **Anything else, known or unknown** → ignore silently. Filler has no effect
  by design, traps have no implementation yet, and an unknown id is a newer
  apworld talking to an older mod.

## 5. Replaying items on reconnect — the one thing that must not be naive

On connection the server resends **every item the slot has ever received**.
Big keys survive that fine: re-applying one rewrites the same key with the same
value and re-pins the same prop. **Gourds do not.** Replaying them the naive
way spawns a second copy of every gourd the slot ever received, which wrecks
both the deposit count and the item economy.

Persist a cursor in the save file instead:

```
SaveManager["ap_items_received"]  // how many items of the ordered list are applied
SaveManager["ap_seed_name"]       // the server's seed_name, as a string entry
SaveManager["ap_slot_name"]       // the connected slot name
```

On `ReceivedItems`, apply only entries at index ≥ `ap_items_received`, then
advance it. If `seed_name` or the slot name does not match what the save
records, reset the cursor to 0 and replay everything — that is exactly the
"physical softlock → new save → reconnect to the same slot" recovery decided
on 2026-09-15 (`design-decisions.md`), and it falls out of this scheme for
free, because a brand-new save starts with the cursor at 0.

Known and accepted consequence of that recovery path: replayed gourds land at
the hub, they do not go back into the monuments they were deposited in. The
players redeposit them by hand. Harmless while deposits are counted globally;
it would need rethinking if monuments ever became individually meaningful.

## 6. Reporting the goal

Send a `StatusUpdate` with `ClientStatus.CLIENT_GOAL`. Not a location id — the
goal is an event in the Python world, with no address.

| `goal` | Trigger |
|---|---|
| `gauntlet` | first non-zero write of `SaveManager["GauntletComplete"]` |
| `ending` | first non-zero write of `SaveManager["EndingGate"]` |
| `deposits` | `CosmeticMonumentFillTracker.GetFilledMonumentCount() >= deposit_goal_amount` |

`EndingGate` is the chapel door's live open/closed state, not a latching flag:
it goes back to 0 while the door animates. Trigger on the **first** non-zero
write and never read it back to ask whether the goal is done — the same rule
`CheckTracker` already applies to it.

## 7. Reporting checks

Send `LocationChecks` with the ids from §3. The server deduplicates, so
re-sending is safe; `CheckTracker` (persisted under `ap_reported_<id>`) already
prevents most repeats anyway.

**Puzzles and big keys** are already detected today by `GourdStatePatch` and
`SaveValuePatch` and arrive through `Plugin.Reporter.ReportCheck`. Nothing to
add beyond turning the name into an id.

**Radio stations** need `SaveValuePatch` widened to also try
`Enum.TryParse<SavableSystem>` alongside `SaveablePropName`. Report only when
`slot_data.radio_station_checks` is true.

**Gourd deposits** have no detection wired yet.
`CosmeticMonumentFillTracker.GetFilledMonumentCount()` returns the aggregated
count and has no caller. On every change, report every amount in
`deposit_location_amounts` that is ≤ the current count and not yet reported.
Report on thresholds crossed, never un-report: the count **can go down** when
players take a gourd back out of a monument, and a check that has been sent is
sent for good.

Deposits are counted across every monument together. The mod must never care
which monument a gourd went into — that is the whole point of the model, and
what makes it impossible for a bad distribution to lock a seed.

## 8. A deliberate quirk: big keys check their own location

`ItemApplier.ApplyBigKeyItem` reports the key's own location when it applies
the item. That looks wrong for a multiworld and is not: pinning the key into
its plinth consumes the plinth, so the players could never place that key by
hand afterwards, and without the self-report the location would be dead
forever — its item lost to whoever was waiting for it. Keep the behaviour.

The Python world accounts for it. When the Tutorial Key is precollected (the
default), its deposit location is given no requirement at all, because the mod
checks it the moment the client connects.

## 9. How the mod implements this

All of it lives in `../mod/src/Core/Net/`, plus small edits elsewhere.

| File | Role |
|---|---|
| `ApConnection.cs` | The only file that touches the client library. Connects on a background task, parks incoming items in a per-session concurrent queue, exposes the slot's location set. Never touches the game. |
| `ApRuntime.cs` | `MonoBehaviour` pump. Everything that reaches IL2CPP happens here, on the main thread: applying items, sending checks, deposit thresholds, goal. Also reconnects. |
| `ApEndpoint.cs` | Resolves host:port (config) + slot name and password (read back from `SaveData`, so loading an existing save works without retyping). |
| `ApSlotData.cs` | Typed view of §2. |
| `ApLocationIds.cs` | The §3/§4 arithmetic, with the offsets taken from slot_data. |
| `ApItemCursor.cs` | The §5 cursor. |
| `ApGoalFlags.cs` | Latches `EndingGate`/`GauntletComplete` on first non-zero write. |
| `ApReporter.cs` | `ICheckReporter` that still logs, and queues for `ApRuntime`. |
| `CheckTracker.GetReportedLocationNames()` | Lets §7's reconnect resend everything this save already validated. |
| `SaveValuePatch` | Widened to `SavableSystem`: radio stations and the goal latch. |
| `GourdRegistry` | `gourdSecretZoneVice` dropped, per §3. |
| `ModConfig.ArchipelagoEnabled` | Off switch, to keep playing with the mod's other features and local-only check logging. |
| `ApConnectionTest.cs` + `HostMenuConfirmStartPatch.cs` | Verifies the details on the hosting screen before the session starts (a throwaway `NoItems` login, closed immediately). Never blocks hosting: a failure flashes the field and the next press goes through. |

`ICheckReporter` did **not** need an id-based overload after all: deposit
thresholds have no enum name, but `ApRuntime` derives their ids from the
deposit count itself, so nothing has to route a raw id through the reporter.

## 10. Assumptions the logic makes that nobody has verified in-game

The world generates correctly either way; these decide whether a generated
seed is actually beatable. Worth settling during the first real test session.

*Settled since: a gourd deposited in a monument **cannot be taken back out** —
not possible in the base game, confirmed by the player on 2026-09-15. Big key
requirements are cumulative for good, and the `deposit_logic` option that
existed only to hedge this has been removed.*

- **Does the tutorial drawbridge really gate the way out?** If it does not,
  `start_with_tutorial_key: false` becomes safe and the key can be shuffled.
- **Can the Green Dome's monument be completed with fewer than 15 gourds?**
  `green_dome_deposits: limited` assumes 6 works. If it does not, that setting
  makes the Green Dome Key unobtainable. `full` and `excluded` are both safe.
- **Do `FmStation7/8/9` exist in the game at all?** The world assumes not
  (only seven were ever observed being written). If they turn out to be real,
  they are three missing checks — annoying, not seed-breaking.
- **Are all 58 puzzles reachable without any big key?** The world assumes the
  map is open apart from the ending. If some tower turns out to be genuinely
  locked behind a key, its puzzles need a region of their own, which in turn
  needs the puzzle → tower mapping nobody has established yet.
