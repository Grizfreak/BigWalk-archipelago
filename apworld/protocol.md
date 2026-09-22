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

The mod is not the only reader. Universal Tracker hands this same slot_data
back to the world and has it rebuild the slot from it (`world.TRACKER_OPTIONS`
lists the fields that matter for that). Dropping an option field here because
the mod stopped reading it would quietly put the tracker back on whatever YAML
the tracking player happens to have.

| Field | Type | Meaning |
|---|---|---|
| `world_version` | str | apworld version, e.g. `"0.1.0"`. Log a warning on a mismatch with what the mod was built against; do not refuse to connect. |
| `goal` | str | `"gauntlet"`, `"ending"`, `"second_ending"` or `"deposits"`. See §6. A goal string the mod does not recognize disarms goal detection and warns loudly rather than guessing. |
| `deposit_goal_amount` | int | Deposits needed to win when `goal == "deposits"`. Already clamped to what exists; use it as-is. |
| `deposit_locations` | str | `"none"`, `"milestones"` or `"all"`. Informational — `deposit_location_amounts` is the authoritative list. |
| `deposit_location_amounts` | int[] | Ascending deposit counts that are checks, e.g. `[5, 10, ..., 45]`. Possibly empty. |
| `green_dome_deposits` | str | `"full"`, `"key_only"` or `"excluded"`. Informational: `total_monument_slots` and `big_keys_in_play` already say everything the mod acts on. It reports what was GENERATED, which is not always what was asked for — `second_ending` + `"excluded"` generates as `"key_only"`. |
| `radio_station_checks` | bool | Whether the seven radio stations are checks. |
| `radio_station_items` | bool | Whether the music is shuffled too. While true the mod suppresses the game's own station unlock until the matching item arrives (§11). **Default to false** when the field is absent: an apworld too old to send it has no Radio Music items in its pool, so suppressing would make seven stations permanently silent. |
| `big_key_features` | bool | Whether what a big key opens is an Archipelago item. While true the mod stops a placed key from opening its door and grants the feature on receipt instead (§12). Always `true` from this apworld — it is the model, not an option. **Default to false** when the field is absent, for the same reason as the radio and more sharply: suppressing without items in the pool leaves seven doors that can never open. |
| `big_key_items` | bool | Whether the big keys themselves are Archipelago items. While true the mod holds every key locked in its stone (`blockGrabbing`) until its item arrives, and a full monument no longer releases one. Always `true` from this apworld. **Default to false** when absent: locking keys that nothing can send leaves all seven unobtainable. |
| `total_monument_slots` | int | Gourd items in circulation for this slot (30 or 45). |
| `big_keys_in_play` | str[] | `SaveablePropName` names of the big keys this slot uses. Six entries when the Green Dome is excluded. |
| `location_id_base` | int | 8600000. See §3. |
| `radio_id_offset` | int | 1000. |
| `deposit_id_offset` | int | 2000. |
| `cut_id_offset` | int | 3000. See §3. |
| `key_item_id_offset` | int | 4000. See §4. |
| `gourd_item_id` | int | 8600001. |

## 3. Location ids

Ids are derived from the game's own enum values on purpose, so the mod does
arithmetic instead of shipping a table that would drift from `bigwalk/data.py`.
`B` below is `location_id_base`.

| Category | Id | Count |
|---|---|---|
| Puzzle | `B + (int)SaveablePropName` (100–159) | 45 |
| Big key deposit | `B + (int)SaveablePropName` (300–306) | 7 |
| Radio station | `B + 1000 + (int)SavableSystem` (30–36) | 7 |
| Gourd deposit N | `B + 2000 + N`, N from 1 | up to 45 |
| Big key cut segment | `B + 3000 + (int)SaveablePropName * 10 + index` | 25 |

In C#, starting from what `ICheckReporter.ReportCheck(string)` already
receives:

```csharp
if (Enum.TryParse<SaveablePropName>(locationId, out var prop))
    id = Base + (int)prop;
else if (Enum.TryParse<SavableSystem>(locationId, out var system))
    id = Base + RadioOffset + (int)system;
```

**A cut segment has no identifier of the game's own.** `KeyBlank.cuts` is an
anonymous Mirror `SyncList<bool>`, so both sides build one out of the two
things that do identify a segment — the key it belongs to and its index in
that list. The mod reports and remembers it as `<SaveablePropName>#cut<index>`
(`ApLocationIds.CutLocationName`); `#` cannot occur in a C# enum name, which
is what keeps the parse unambiguous against the key's own deposit location.
The stride of 10 is twice the widest key's five segments, so a longer key
would not push every id below it.

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
| Features (Drawbridge, Map Room, Chairlift, Train, Tunnels, Chapel Door, Hub Secret Door) | `B + (int)SaveablePropName` (300–306) | 6 or 7 |
| Big Keys (`<Feature> Key`) | `B + 4000 + (int)SaveablePropName` | 6 or 7 |
| Radio Music | `B + 1000 + (int)SavableSystem` (30–36) | 7, or 0 when `radio_station_items` is false |
| Filler (Megaphone, Walkie-Talkie, Backpack, Belt, Flare Gun, Laser, Binoculars, Compass, Folding Map, Radio, Gourd Carton, Torch, Lamp, X-Ray Goggles, Blue/Green/Yellow Flare Gun) | 8609001–8609017 | rest of the pool |
| Trap (Untied Shoelace) | 8609101 | 0 by default |

What to do on receipt:

- **`Gourd`** → `ItemApplier.ApplyGourdItem(toPlayer: true)`. Spawns a
  cosmetic prop and nothing else: no `SaveManager` write, no check reported.
  This is the only currency that fills monuments. One arriving during play
  goes into the player's hands, or in front of them; the batch rebuilt at the
  start of a session goes to the hub instead (`toPlayer: false`).
- **A big key** → `KeyCustody.Grant(propName)`, where `propName` comes back
  from `id - B - 4000`. Records the grant under `ap_key_<SaveablePropName>`,
  clears `blockGrabbing` on the stone the key sits in, unpins it and drops
  it at the spawn point — the same place a gourd would land. It opens
  nothing: what a key is for is being cut five times and placed once.
  **Resolve this range BEFORE the feature range**, since the two are one
  offset apart and a key id would otherwise never be reached.
- **A feature** → `ItemApplier.ApplyBigKeyItem(propName)`, where `propName`
  comes back from `id - B`. With `big_key_features` on this is
  `KeyFeatures.Grant(propName)`: it records the grant under
  `ap_feature_<SaveablePropName>` and opens the door (§12). It writes no game
  save key, pins nothing and reports no location. With the flag off it falls
  back to the original behaviour — write the save, pin the key live into its
  plinth, report its own location — which is what keeps an older seed
  beatable (§8).
- **A Radio Music item** → `RadioStations.Grant(system)`, where `system` comes back
  from `id - B - 1000`. Records the grant in the save and starts the music.
  See §11 for why it needs both halves.
- **A filler gadget item** → `ItemApplier.ApplyGadgetItem(kind, toPlayer: true)`,
  where `kind` comes back from `id - B - 9001` indexing into a fixed order
  (`GadgetItemSpawner`/`ApLocationIds.GadgetItemOrder`) that has to match
  `FILLER_ITEMS`' own order exactly, since neither side ships the other a
  table. Same cosmetic-spawn-only shape as a gourd — no `SaveManager` write,
  no location — cloned from a template captured locally on each machine
  before the mod removes every vanilla instance of that prop from the map
  (§4 above, `VanillaGadgetRemover`).
- **Anything else, known or unknown** → ignore silently. A trap has no
  implementation yet, and an unknown id is a newer apworld talking to an
  older mod.

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

### Filler gadgets get the same treatment, minus the deposit sink

Player request, 2026-09-22: a filler gadget (megaphone, walkie-talkie, …)
should survive a world reload the same way a gourd does, not just the first
time it is received. Same shape as the gourd ledger, one counter per
`GadgetKind` (`SaveManager["ap_gadgets_received_<kind>"]`), counted the same
way — before the item is applied, regardless of whether the spawn actually
lands. The only real difference is that a filler gadget has no deposit sink
at all (there is nowhere to "spend" a megaphone), so the reconciliation
drops the middle term entirely:

```
loose gadgets to spawn (per kind) = that kind ever received
                                   - that kind already spawned this session
```

Meaning every filler gadget ever received is owed a loose copy somewhere in
the world, forever, at every session start — there is no equivalent of a
monument absorbing it. This is intentionally simpler than the gourd ledger:
no equivalent of the "rebuild from replay" repair, since there is no legacy
save that could have received a filler gadget before this counter existed.

## 6. Reporting the goal

Send a `StatusUpdate` with `ClientStatus.CLIENT_GOAL`. Not a location id — the
goal is an event in the Python world, with no address.

| `goal` | Trigger |
|---|---|
| `gauntlet` | first non-zero write of `SaveManager["GauntletComplete"]` |
| `ending` | first non-zero write of `SaveManager["EndingGate"]` |
| `second_ending` | `PeckEffectEndingTransition.OnPeck` on an object whose hierarchy path contains `SecondGoodbye` |
| `deposits` | `CosmeticMonumentFillTracker.GetFilledMonumentCount() >= deposit_goal_amount` |

`EndingGate` is the chapel door's live open/closed state, not a latching flag:
it goes back to 0 while the door animates. Trigger on the **first** non-zero
write and never read it back to ask whether the goal is done — the same rule
`CheckTracker` already applies to it.

`second_ending` has no flag at all to trigger on. Two buttons held together
behind the Hub Secret Door start an ending transition and the game returns to
the main menu, writing nothing anywhere: the whole chain was decompiled on
2026-09-10 and touches `SaveManager` nowhere. So the mod latches the event
itself under `ap_flag_SecondEnding` (`Core/Net/ApSecondEnding.cs`) and reads
that back. **Confirmed end to end in game on 2026-09-22.**

**Hook the START of the transition, not its end.** Two hooks failed first, and
both failures generalise:

- `EndingTransition.SetActive()` carries no address in the IL2CPP dump, the
  signature of an inlined method.
- `EndingTransition.OnTransitionEnd()` *does* carry one, Harmony patches it
  without complaint, and it is **never called** — watched through a complete
  ending. An address is not a call site; `Update()` holds an inlined copy of
  the body. `EndingTransition.entryMode` and
  `WorldMenuManager.secondEndingTransition` were the obvious way to tell the
  two endings apart, and they are never reached.

What works is `PeckEffectEndingTransition.OnPeck`, the peck-driven entry
point, which the buttons go through. `AutomaticDisconnector
.StartEndingTransition` is the zone-driven one; the second ending does not use
it.

**Identify the ending by hierarchy path.** The run that worked printed
`LandmarksPlayerCount2/Contents/SecondGoodbye PlayerCount2/Positioner/
GoToVoidSystem`. `SecondGoodbye` is the game's own word for this ending —
`GoodbyeChapel` and `GoodbyeVoid` sit on the first one's path — and matching
it is what stops the Gauntlet's ending, which plays a cinematic too, from
goaling a `second_ending` slot.

**Report it on the spot, not on the next tick.** The transition stops Mirror,
and a client whose pump is gated on `NetworkServer.active` has no tick left
afterwards. A goal that misses its window is not lost, though: the latch is in
the save, so the next connection reports it.

**The 46th gourd is scenery.** There is one more `RewardGourd` in the game
than this world has locations, sitting right there, and it looked like the
zone's conclusion. It is not: the buttons unlock its vise and it stays in
place, `ServerSetGourdState` is never called for it, and a roster taken
afterwards still reads `state=Locked`. Worth stating because the obvious
implementation — "the gourd with `saveablePropName = notSavable`" — is also
dangerous: a client's own cosmetic gourds are `notSavable` too.

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

## 8. The quirk that used to be here, and why it is gone

*Until 2026-09-21 `ItemApplier.ApplyBigKeyItem` reported the key's own
location when it applied the item. That looked wrong for a multiworld and
was not: pinning the key into its plinth consumed the plinth, so the players
could never place that key by hand afterwards, and without the self-report
the location would have been dead forever — its item lost to whoever was
waiting for it. The Python world accounted for it by giving a precollected
Tutorial Key's deposit no gourd requirement at all.*

**Both halves are now removed**, because §12 took away the thing that forced
them. The item no longer consumes the plinth — it is the feature, not the pin
— so placing the key is a genuine player act again, and `SaveValuePatch`
already sees it: the game's own `Prop.SavePropHome` writes the plinth under
the key's `SaveablePropName`, which is a check like any other. The deposit
location therefore takes the same gourd requirement as every other tower,
precollected or not.

**The fallback still needs the old behaviour.** A mod connecting to an
apworld too old to send `big_key_features` keeps the pin, the save write and
the self-report, all three together. Dropping only some of them would leave
those seeds charging gourds for a location the old logic says is free.

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
| `ApSecondEnding.cs` | §6: latches `ap_flag_SecondEnding` when the Hub Secret Door's gourd comes loose, since the game records nothing. |
| `../../Patches/EndingStartPatch.cs` | §6: where an ending starts — `PeckEffectEndingTransition.OnPeck` (the trigger) and `AutomaticDisconnector.StartEndingTransition` (logged). |
| `ApStatusOverlay.cs` | The corner of the screen: connection, what the slot is playing for, the item feed, the resync hint. |
| `../RadioStations.cs` | §11: the station ledger (`ap_radio_*`), the learned dial (`ap_radio_dial_*`), and the live unlock. |
| `../../Patches/BroadcastStationUnlockPatch.cs` | §11: suppresses the game's own unlock. |
| `../KeyFeatures.cs` | §12: the feature ledger (`ap_feature_*`) and the live door. |
| `../../Patches/PropTaggedPinPatch.cs` | §12: stops a placed key opening its own door, and nothing else. |
| `../../Patches/KeyBlankCutPatch.cs` | §12: the 25 cut checks, off `KeyBlank.OnCutsUpdated`. |
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
seed is actually beatable.

**Nothing seed-breaking is left open here (2026-09-22).** Every entry below is
struck through and measured, except `FmStation7/8/9` — which can only ever
cost three checks, never a seed.

*Settled since: a gourd deposited in a monument **cannot be taken back out** —
not possible in the base game, confirmed by the player on 2026-09-15. Big key
requirements are cumulative for good, and the `deposit_logic` option that
existed only to hedge this has been removed.*

- ~~**Does the tutorial drawbridge really gate the way out?**~~ **Settled by
  the player (2026-09-22): it does not.** The mod opens the hub's three arch
  doors from a save's first session (`Core/ArchDoorUnlocker.cs`), and with
  those open the tutorial is not sealed. `start_with_drawbridge_open: false` is
  therefore safe, and no Tutorial region is needed in `regions.py`. The
  guarantee comes from ArchDoorUnlocker being unconditional, so making it
  optional would reopen this.
- ~~**Is every key-cutting station reachable without a big key?**~~
  **Settled in play (2026-09-21): yes**, all five trails. The 25 cut
  locations therefore stay in the overworld. This was the last assumption
  left in this world that could still have made a seed unbeatable.
- ~~Can the Green Dome's monument be completed with fewer than 15 gourds?~~
  **Settled by removing the question (2026-09-15)**: the `limited` option
  claimed 6 would do, and nothing mod-side made that true — the monument is
  a `PropHomeBlock` requiring every one of its homes filled. The option was
  a promise the mod did not keep, so it is gone. `full` and `excluded` cover
  the two real needs.
- **Do `FmStation7/8/9` exist in the game at all?** The world assumes not
  (only seven were ever observed being written). If they turn out to be real,
  they are three missing checks — annoying, not seed-breaking.
- ~~**Are all the puzzles reachable without any big key?**~~ **ANSWERED, AND
  THE ANSWER IS NO (2026-09-21, found in play) — AND FIXED THE SAME DAY.**
  Seven purple gourds and one radio station sit past the chairlift, which
  needs the Green Cup Key (`bigKeyGreenZone`), and one more station past the
  tunnels. Until then this world claimed the island was open apart from the
  ending, so nothing stopped generation putting the Green Cup Key behind the
  chairlift that key opens — an unbeatable seed, and the worst bug this world
  has had. `regions.py` now carries `Past the Chairlift` and `Past the
  Tunnels`, each gated on its own feature item, and what sits inside them was
  measured rather than reasoned: `data.CHAIRLIFT_PUZZLES`, `CHAIRLIFT_RADIO`
  and `TUNNEL_RADIO`, read off the Ctrl+V and Ctrl+B dumps sorted by distance
  to the player. Nothing is left open here.
- ~~**Is the Hub Secret Door the only thing between a player and the second
  ending?**~~ **Settled in play (2026-09-22): yes.** The ending was reached
  and reported on a save whose log read `EndingGate latched: False,
  GauntletComplete latched: False` — neither bell had been rung. In vanilla
  that path is sealed by a sphere at the hub that only breaks once the game
  has been finished, and `Core/SecondEndingSphereUnlocker.cs` disables it from
  a save's first session, which is what leaves the door as the only
  requirement. The zone was then walked on foot and finished that way, so
  nothing rests on the debug flight the first run used. The Victory event for
  `second_ending` therefore sits in a region gated on `Has("Hub Secret Door")`
  and nothing else.
  - The trigger itself was never an assumption: what ends that zone is the
    46th gourd, and the mod watches for its release.

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

## 12. Big keys: forage checks, and an item that is the feature

*Added 2026-09-21. Until then one physical act carried three roles at once:
pinning a key in its plinth was the location, the effect of the item, and what
opened the door. That made the key item redundant — the monument released the
key locally as soon as the gourds were there, so the door opened whether or not
Archipelago ever sent anything — and it forced §8's self-report. The three are
now separate.*

| Role | Act |
|---|---|
| Locations | the 25 cut segments, **plus** placing the key in its receptacle (the 7 deposit locations) — 32 in all |
| Item | the **feature**: Drawbridge, Map Room, Chairlift, Train, Tunnels, Chapel Door, Hub Secret Door |
| The key | a check carrier. Placing it in its receptacle is a check and **nothing else** |

**Almost nothing needs suppressing.** The players fore the key and place it
freely, exactly as in the vanilla game, and both are checks — the same shape as
a puzzle whose gourd is a check. The only thing that must stop happening is the
door opening when the key goes in.

### What actually opens a door

Three candidates were eliminated by dumping the game in play, and all three
were eliminated for the same reason: they were all looking at the socket.

- No `PropHomeBlock` watches a big-key plinth. All seven report none.
- No plinth fires a `PeckSwitch` on pin. All seven report `onPin <none>`.
- No switch in the game wants a big key. Of 2833 `PeckSwitch` instances
  loaded, 49 carry `needsKey`; 48 are `SalonBrush` and one is `StickyCurse`.

**The wiring is on the plug.** `Prop.SetPinDirectControlSystem(PropHome home,
bool pinned)`, decompiled rather than guessed, drives three `TrackedPeckState`s
in order:

1. `home.pinDirectControlSystem` — the plinth is occupied
2. `this.pinDirectControlSystem` — the prop is pinned
3. every entry of `this.taggedPinSystems` whose `propGroup` equals
   `home.pinGroup` — **the feature**

`Prop.taggedPinSystems` is a `PropGroupPeckSystemPair[]` carried by the key
itself, mapping "the group of the home I was placed into" to "the state to
drive". So a big key's door is found without touching the plinth's own
components at all, and granting it is one `SetState(1)` — the same call
`ArchDoorUnlocker` already makes for the hub shortcuts.

Suppression is the mirror image: a Harmony prefix empties `taggedPinSystems`
for the duration of the call and a postfix puts it back, so (1) and (2) still
run — the key visibly sits in its socket, with its sounds and its pose — and
(3) finds nothing. **Empty, not null**: the game's loop has no null guard of
its own, and the disassembly's null path jumps straight to a throw.

### The ledger, and why it is needed

`SavableSystem` has no entry for the map room, the chairlift, the train or the
tunnels. Their persistence **is** "the key is in its plinth", re-asserted by
`Prop.Start()` on every load — which is exactly what this design stops. With
the key inert, nothing carries a door across a reload, so the grant is
persisted under `ap_feature_<SaveablePropName>` and replayed on the same two
occasions as the radio's (§11): a world becoming ready, and a connection
established while a world is already loaded.

### Co-op is simpler here than for the radio

`FmRadioManager` is local to each machine and never replicated, which is why a
guest hears a station the host is still waiting for. Peck state is **not**
local, and `SetPinDirectControlSystem` is server-side to begin with. The host
suppresses, the host grants, the guest sees the door open. Nothing for the
guest to run and no wrinkle to document.

### The keys themselves are items too

*Decided 2026-09-21, correcting the first pass of this design. That pass
left the keys exactly as the vanilla game hands them out — fill a tower's
monument and its key is released — and made only the feature an item. The
player's correction: a key should arrive from the multiworld and spawn like
a gourd. So a monument now buys nothing but its own deposit checks.*

**What holds a key back is one bool.** Measured in game: all seven sit in a
`PropHome` — six of them in a `KeyStoneHome` — with `blockGrabbing = true`.
The game's own release is a `PeckEffectPropHomeSettings` whose
`settingsPerState[2]` writes `blockGrabbing := false`, driven by a
TrackedPeckState whose label spells the whole mechanism out:
`0 - locked, 1 - animating, 2 - grabbable`, on `KeyScrewLogic`.

So both halves write that same field in opposite directions, and **neither
needs a Harmony patch** — which matters, because
`PeckEffectPropHomeSettings.Apply` and `OnPeck` have no code address in the
export while `Awake` does. They are inlined, so a patch would bind to
nothing and suppress nothing, in silence. Re-asserting the bool on a poll
is also robust to *not knowing* what frees the other six keys: only
`bigKeyOverflow`'s releaser was ever found in a dump, the rest being
streamed out at 300m to 900m, and it makes no difference.

Delivery is `Prop.ServerSetUnpinned()` then `SetLoose()` and a position at
the spawn point. `ServerSetUnpinned` has no code address either and was
therefore called under a guard, with "the key stays unlocked in its stone"
as the fallback — in the event it works, verified in play.

`Ctrl+R` brings a stranded key back, exactly as it does a gourd. A key
already in its plinth is left alone: that is a check already sent.

**Logic consequence.** A tower's six locations — five cuts and a placement
— all rest on `Has(<Feature> Key)` and on nothing else. No gourd is
involved anywhere near a key any more, and `rules.gourd_requirements`, the
cumulative count that used to charge each key deposit, is gone.

### The 25 cut checks

Five segments each on the drawbridge and the four coloured towers, measured in
game (Ctrl+K, 2026-09-21) rather than taken from the third-party document. The
Black Monolith and Green Dome keys are born finished, with `covers = 0` and
their Complete `PropGroup` already in `propGroups`, so they have no cut
locations — only their deposit and their feature item.

**Not `ServerCutSegment`.** It is the obvious hook, it is public, and it has
**no code address** in the il2cpp export while every one of its neighbours in
the same class has one — it was inlined into its caller. That is the same trap
`BroadcastStation.Unlock` set for the radio, where patching the obvious target
would have suppressed nothing, silently. The hook is `KeyBlank.OnCutsUpdated`,
which has a body and is the game's own notification for this exact change.

Report only on a `false -> true` transition: `cuts` is a `SyncList` sized at
runtime from scene data, so a world load appends its entries one at a time and
the callback fires once per append.

**The trap that dissolved.** Pinning a key marks its blank complete, so a naive
hook would have fired the drawbridge's five checks the moment the mod pinned
the precollected Tutorial Key at connection time. Under this design the mod
pins nothing, so a key sitting in its plinth is one the players carried there,
and the five cuts behind it are genuinely theirs.
