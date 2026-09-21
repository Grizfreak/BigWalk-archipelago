# Big Walk — mod ↔ Archipelago contract

*The contract between the Python world in [`bigwalk/`](bigwalk/) and the mod's
Archipelago client in `../mod/src/Core/Net/`. This file and
`bigwalk/world.py`'s `fill_slot_data` are one unit — change them together.*

**Status (2026-09-15): implemented, and connecting from the running game.**
The client lives in `../mod/src/Core/Net/`. The contract below is validated
against a real Archipelago room — login as game `Big Walk`, slot_data
round-trip, id arithmetic, a check accepted by the server, and the
starting-inventory item delivered on connect — and the mod has since been
confirmed to load under BepInEx IL2CPP and reach a server from inside the
game.

Every path here has since been exercised in-game (2026-09-15): outgoing
checks from real puzzles, item receipt, big keys, deposit thresholds, the
`deposits` goal, the received-items cursor across a reconnect, surviving a
dropped connection, and recovery on a brand-new save. What remains untested
is listed in §10.

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
| `green_dome_deposits` | str | `"full"` or `"excluded"`. |
| `radio_station_checks` | bool | Whether the seven radio stations are checks. |
| `radio_station_items` | bool | Whether the music is shuffled too. While true the mod suppresses the game's own station unlock until the matching item arrives (§11). **Default to false** when the field is absent: an apworld too old to send it has no Radio Music items in its pool, so suppressing would make seven stations permanently silent. |
| `total_monument_slots` | int | Gourd items in circulation for this slot (30 or 45). |
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
| Radio Music | `B + 1000 + (int)SavableSystem` (30–36) | 7, or 0 when `radio_station_items` is false |
| Filler (Postcard, Souvenir Pebble, Novelty Keychain) | 8609001–8609003 | rest of the pool |
| Trap (Untied Shoelace) | 8609101 | 0 by default |

What to do on receipt:

- **`Gourd`** → `ItemApplier.ApplyGourdItem(toPlayer: true)`. Spawns a
  cosmetic prop and nothing else: no `SaveManager` write, no check reported.
  This is the only currency that fills monuments. One arriving during play
  goes into the player's hands, or in front of them; the batch rebuilt at the
  start of a session goes to the hub instead (`toPlayer: false`).
- **A big key** → `ItemApplier.ApplyBigKeyItem(propName)`, where `propName`
  comes back from `id - B`. Writes the save, pins the key live into its plinth,
  and reports its own location (§7).
- **A Radio Music item** → `RadioStations.Grant(system)`, where `system` comes back
  from `id - B - 1000`. Records the grant in the save and starts the music.
  See §11 for why it needs both halves.
- **Anything else, known or unknown** → ignore silently. Filler has no effect
  by design, traps have no implementation yet, and an unknown id is a newer
  apworld talking to an older mod.

Note that a Radio Music item's id is the same number as its location id, exactly
as a big key's is. Archipelago keeps the two namespaces apart, and reusing the
game's own enum value in both is what lets the mod derive either with one rule
instead of shipping a table.

## 5. Replaying items on reconnect — the one thing that must not be naive

On connection the server resends **every item the slot has ever received**.
Big keys survive that fine: re-applying one rewrites the same key with the same
value and re-pins the same prop. **Gourds do not.** Replaying them the naive
way spawns a second copy of every gourd the slot ever received, which wrecks
both the deposit count and the item economy.

Persist a cursor in the save file instead:

```
SaveManager["ap_items_received"]   // how many items of the ordered list are applied
SaveManager["ap_gourds_received"]  // how many of those were gourds (see below)
SaveManager["ap_seed_name"]        // the server's seed_name, as a string entry
SaveManager["ap_slot_name"]        // the connected slot name
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

Note this is specific to a *new* save. Reloading the same save does put
deposited gourds back in their slots, from the `ap_home_*` keys — which is
exactly why a new save cannot: those keys live in the save that was
abandoned, and the server only knows what the slot received, never what was
done with it. Storing the count in Archipelago's `DataStorage` would fix
that; deliberately not done for the alpha, see `design-decisions.md`.

### Suppressing the replay is not enough — gourds must be reconciled

Found in the first in-game session (2026-09-15) and worth spelling out,
because the two halves are each correct and together they lose data.

A cosmetic gourd deliberately has **no save identity** (`saveablePropName =
notSavable`, `startHome = null`) so it can never collide with a real check.
The consequence is that the game persists one only once it is pinned in a
monument, through `ap_home_<slot>`. A gourd lying at the hub, or in someone's
hands, is gone after a restart. Before the cursor existed this was invisible:
the server's replay respawned everything on every connection. Suppressing the
replay made it permanent — and losing a single gourd puts the last big key
out of reach, because the pool holds exactly one `Gourd` per monument slot.

So the client does not try to remember individual props. Gourds are
fungible, so it remembers the count and rebuilds the world from it at every
session start:

```
loose gourds to spawn = gourds ever received
                        - slots filled (count of ap_home_* set)
                        - gourds already spawned this session
```

Two details that are not optional:

- **Count the deposited slots from the save, not from loaded `PropHome`s.**
  Monuments stream in as the players approach, so a scene-based count
  under-reports at session start and the reconciliation would spawn
  duplicates for every monument not yet loaded.
- **Rebuild the ledger from the replay, do not merely trust it.** Counting
  the gourds in the replayed history is authoritative, and it repairs a save
  written by a build that kept no count at all. That means waiting for the
  replay to arrive and go quiet before reconciling; an empty queue right
  after connecting means "not yet", not "nothing".

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

**Radio stations** go through `SaveValuePatch`, widened to try
`Enum.TryParse<SavableSystem>` alongside `SaveablePropName`. Report only when
`slot_data.radio_station_checks` is true.

The check is unaffected by the music being suppressed (§11), and that is not a
coincidence: the save write it reads comes from the station's
`TrackedPeckState`, one level above the unlock the mod skips.

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
| `../RadioStations.cs` | §11: the station ledger (`ap_radio_*`), the learned dial (`ap_radio_dial_*`), and the live unlock. |
| `../../Patches/BroadcastStationUnlockPatch.cs` | §11: suppresses the game's own unlock. |
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
- ~~Can the Green Dome's monument be completed with fewer than 15 gourds?~~
  **Settled by removing the question (2026-09-15)**: the `limited` option
  claimed 6 would do, and nothing mod-side made that true — the monument is
  a `PropHomeBlock` requiring every one of its homes filled. The option was
  a promise the mod did not keep, so it is gone. `full` and `excluded` cover
  the two real needs.
- **Do `FmStation7/8/9` exist in the game at all?** The world assumes not
  (only seven were ever observed being written). If they turn out to be real,
  they are three missing checks — annoying, not seed-breaking.
- ~~**Are all 58 puzzles reachable without any big key?**~~ **ANSWERED, AND
  THE ANSWER IS NO (2026-09-21, found in play).** The purple gourds and one
  radio station sit past the chairlift, which needs the Green Cup Key
  (`bigKeyGreenZone`). The region graph assumes the island is open apart from
  the ending, so nothing stops generation placing the Green Cup Key itself —
  or any other progression item — behind the chairlift, which makes the seed
  unbeatable. **This is a release blocker**, not a rough edge. The fix is a
  region gated on `Has(Green Cup Key)`; what it still needs is the list of
  locations inside it (`Debug.DumpGourdRosterKey`, Ctrl+V, prints the purple
  gourds wherever it is pressed).

## 11. Radio stations as items

*Added 2026-09-21, together with the `radio_station_items` option. Turning a
station on used to report its check **and** grant the station — the only check
in this world that rewarded itself, since gourds are hidden and unspawned and
big keys arrive from Archipelago. Nothing broke and no seed was ever
unbeatable; it was an inconsistency, and this is the fix.*

The mod's half rests on three facts about the game, decompiled rather than
guessed (Ghidra, `../mod/reverse-engineering-notes.md`):

1. **`BroadcastStation.Unlock(PeckContext)` is an inlined copy of
   `FmRadioManager.Unlock(MusicGroup)`**, not a call to it. Patching
   `FmRadioManager.Unlock` would suppress nothing — and, usefully, the mod can
   call it to grant a station without re-entering its own patch.
2. **Neither writes to the save.** Persistence is one level up, in the
   station's `TrackedPeckState` (`savableSystem = FmStationXxx`), which is
   exactly where `SaveValuePatch` reads the check. Suppressing the unlock
   therefore costs no check.
3. **There is no relock.** `FmRadioManager._stationStates[i]` is only ever set
   to true and the manager never reads the save back. Writing `FmStationXxx`
   to 0 — the obvious first idea, and the one this work started from — stops
   nothing.

So suppression is a Harmony prefix on `BroadcastStation.Unlock` that returns
false, and the grant is a direct `FmRadioManager.Unlock(musicGroup)`.

Fact 3 has a second consequence that is easy to miss: **the unlock lives in RAM
and nowhere else**, so a granted station has to be re-applied to every world
that loads. A world reload does not need the process to restart — quitting to
the menu and hosting again is enough. The grant is therefore persisted under
`ap_radio_<SavableSystem>` and replayed from that ledger on two occasions: a
world becoming ready, and a connection established while a world is *already*
loaded. The second is not optional, because the server's replay of those items
is precisely what the §5 cursor skips.

Finding the `MusicGroup` for a station has an authoritative answer and a
fallback:

- the `BroadcastStation` in the world holds both the `MusicGroup` and, through
  `peckSystemReference.peckSystem.savableSystem`, the save key — the pairing
  comes from the game's own data. It is also the one that may not be loaded:
  the towers stream in with the world.
- otherwise the dial position **this save has learned**, kept under
  `ap_radio_dial_<SavableSystem>` as position + 1 (0 meaning never learned).
  Every time the towers are loaded the mod writes down where each one sits, so
  a later session can unlock a station with no tower in sight.

**What used to be there, and why it is gone.** The fallback was
`stationTrackGroups[(int)system - 30]`, assuming the dial is ordered like the
`FmStation*` block. Settled in game on 2026-09-21: **it is not**. The dial
reads bobby / FourthSpace / breathwork / JourneyBeat / DanceFM / Mallets /
Bristol, while the stations owning those groups are Breathwork / SleuthFm /
FourthSpace / JourneyBeat / DanceFm / AFJ / Kosmische — one of seven lines up.
The fallback would have unlocked a different station, silently, and only in
the case it existed for. Never derive a dial position from the enum.

**Only the host suppresses.** Nobody else in the session runs an Archipelago
client, so a guest cannot know which stations the slot has received. Their
radio stays vanilla: they hear a station as soon as it is switched on, while
the host waits for the item. The alternative — a guest whose radio can never
play anything at all — is worse. The apworld option says so, so that nobody
meets it for the first time in play.
