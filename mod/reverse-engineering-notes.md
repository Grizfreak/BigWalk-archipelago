# Big Walk — Reverse-Engineering Notes for the Mod

*Last updated: session of September 10, 2026 (source material); split into this file on September 15, 2026.*

This file collects everything about how *Big Walk*'s internals work and how
the BepInEx/Harmony mod (`mod/`) hooks them. For decisions about what the
Archipelago world itself should look like (goal, items, locations, options),
see [`../apworld/design-decisions.md`](../apworld/design-decisions.md).

## Mod to-do (as of 2026-09-15)

### To implement on the mod side

- **RESOLVED (2026-09-15) — Archipelago network client**, `src/Core/Net/`.
  The long-standing "nothing connects to an AP server" blocker is closed.
  Built on the official `Archipelago.MultiClient.Net` NuGet (6.7.1), which
  turned out to have zero NuGet dependencies and to bundle its own
  `Newtonsoft.Json` — so exactly 2 extra DLLs to deploy next to the plugin.
  Full contract and file-by-file map: `apworld/protocol.md`.
  - **Threading is the whole design constraint.** The client library raises
    its events on its own network threads; every game touch is IL2CPP and
    must be on Unity's main thread, where a violation is a native crash with
    no managed stack. So `ApConnection` is deliberately inert (managed types
    and concurrent queues only, never a game object) and `ApRuntime.Update()`
    is the single place anything reaches the game. `TryConnectAndLogin` is
    blocking too, hence connecting on a `Task` with the pump polling a
    status flag — inline, it would freeze the game for the whole handshake
    or the whole timeout on a bad address.
  - **Per-session item queue**: the queue is recreated per connection and
    the `ItemReceived` handler captures *its own* instance rather than
    reading a field. A dying session dropping one stale item into the new
    session's queue would shift the received-items count by one and make
    `ApItemCursor` skip a real item permanently.
  - **Reconnect resends every check this save knows** (`CheckTracker.
    GetReportedLocationNames()`, new). `CheckTracker` is one-way by design,
    so a check validated while the client was offline would otherwise never
    be sent again and its multiworld item would be lost.
  - **Validated end-to-end against a real Archipelago room** (local
    `MultiServer.py` hosting a generated 2-slot Big Walk seed): login as game
    `Big Walk`, slot_data round-trip with the expected CLR types (`Int64`/
    `String`/`Boolean`/`JArray`), the 81 locations of a default slot, id
    arithmetic confirmed on a puzzle/big key/radio/deposit, a check accepted
    (81 missing → 80), and the precollected `Tutorial Key` delivered on
    connect as item `8600300` — which also confirms
    `ItemsHandlingFlags.AllItems` is required. Both deliberate exclusions
    confirmed absent from the slot: `gourdSecretZoneVice` (8600290) and
    `FmStation7` (8601037).
  - **CONFIRMED IN-GAME (2026-09-15, player): the mod loads and connects.**
    The one genuine unknown is closed — BepInEx IL2CPP resolves
    `Archipelago.MultiClient.Net` and its bundled `Newtonsoft.Json` from the
    plugin folder, the managed client runs inside the game process, and a
    session reaches the server from the hosting screen.
  - **BUG FOUND AND FIXED in that very first session (2026-09-15, player):
    receiving a big key dropped a GOURD at the hub.** `ItemApplier.
    ApplyBigKeyItem` ended with a `ReceivedItemSpawner.SpawnCosmeticPickup()`
    call, left over from 2026-09-11 when that spawn was a generic "you
    received something" notification for any item. Later the *same day*, the
    design flipped and cosmetic gourds stopped being decoration to become
    the only way to fill a monument — which silently turned that line into
    a real bug: every big key handed the player a free unit of the one
    currency the Archipelago world balances exactly (its pool holds one
    `Gourd` per monument slot and no more). Not seed-breaking, since extra
    gourds only ever make monuments easier to fill, but it breaks the model
    and it is visibly confusing. Call removed; a big key needs no
    notification anyway, it materializes in its own plinth and opens what it
    opens on the spot. Exactly the leftover the 2026-09-07 note warned
    about ("keep this in mind to avoid accidentally reintroducing the old
    idea of a generic hand-carried gourd").
  - **SECOND BUG, the serious one, found and fixed in the same session
    (2026-09-15, player): gourds vanished across a reconnect.** Two
    individually-correct decisions that destroy data together.
    1. A cosmetic gourd has no save identity on purpose
       (`saveablePropName = notSavable`, `startHome = null`) so it can never
       collide with a real check — so the game persists one **only** once it
       is pinned in a monument (`ap_home_<slot>`). Loose or carried, it is
       gone on restart.
    2. The new item cursor suppresses the server's replay, to stop a
       reconnection duplicating every gourd.
    Before (2), the replay quietly papered over (1) by respawning
    everything. After (2), any gourd not deposited before quitting is lost
    **permanently** — and since the pool holds exactly one `Gourd` per
    monument slot, losing one puts the last big key out of reach: a seed
    made unfinishable, silently.
    - **Fix, in `Core/Net/ApRuntime.RestoreLooseGourds`**: stop trying to
      remember individual props. Gourds are fungible — the same property
      that makes the global deposit model softlock-proof — so the mod
      remembers the *count* (`ap_gourds_received`) and rebuilds the world
      from it at every session start: `received - deposited - spawned this
      session` loose gourds at the hub.
    - **`GetFilledMonumentCount()` rewritten to scan the save's `ap_home_*`
      entries** instead of walking `MonumentHomes`. The old version
      under-reported for as long as a monument had not streamed in, which
      also quietly degraded deposit checks and the goal — and would have
      made the reconciliation above spawn duplicates for every monument not
      yet loaded. The save always knows; the scene does not.
    - **The ledger is rebuilt from the server's replay** rather than merely
      trusted, so it also repairs a save written by the buggy build (which
      kept no count at all). Hence the wait for the replay to go quiet
      before reconciling: an empty queue right after connecting means "not
      yet", not "nothing".
  - **CONFIRMED IN-GAME (2026-09-15)** — deposits, deposit checks and the
    goal all work: a session reached `ap_gourds_received=33` with five
    `ap_home_*` slots filled, and the `deposits` goal (threshold 5) was
    reported to the server. The reconciliation was then confirmed on the
    next session, exactly as predicted: `Restored 28 gourd(s) received but
    never deposited.` (33 received − 5 deposited), with no warning.
  - **Performance, RESOLVED (2026-09-15)**: restoring 28 gourds stuttered
    badly, and it outlasted the spawning. Two causes, both fixed.
    `FindTemplate()` ran a full `FindObjectsByType<RewardGourd>()` scan of
    the scene on **every** spawn — now cached, invalidated implicitly when
    the object dies so a world reload re-scans once. And the props were all
    born inside `InventorySpawn`'s scatter radius (measured well under a
    metre from the logged positions), interpenetrating, never settling,
    never sleeping, with Mirror replicating the lot.
  - **Placement, RESOLVED (2026-09-15) after getting it wrong twice.** The
    fix for the pile was first a sunflower spiral of computed offsets: it
    killed the stutter but **11 gourds of 28 ended up outside the playable
    area** (inside geometry or over an edge), while the log cheerfully
    reported 28 spawned. Raycasting each offset onto the ground still lost
    8 of 28. Both attempts shared one mistake — deciding where a valid
    position is, which is the game's job.
    - **What works** (player's suggestion): drop them **one at a time**, on
      `InventorySpawn.GetNextSpawnPosition()`, one per
      `GourdRestoreInterval` (default 0.5s, configurable). Out-of-bounds is
      impossible by construction, and pacing replaces spreading — each
      gourd lands on ones that have already settled and rolls off, instead
      of dozens being born interpenetrating, which was the actual cause of
      the stutter rather than the density. Confirmed in-game: 28 of 28, no
      lag. The spiral, the ground check and the `UnityEngine.PhysicsModule`
      reference it needed are all gone.
    - Nothing was ever permanently lost to those two attempts: the ledger
      recomputes received-minus-deposited every session, so gourds dropped
      into the void came back on the next connection.
  - **Appearance, RESOLVED (2026-09-15)**: it used to be arbitrary —
    `FindTemplate` takes the first `RewardGourd` `FindObjectsByType`
    returns, in unspecified order, so clones inherited whatever that one
    looked like (observed purple in-game, i.e. an `isVariantChallenge`
    template). Now deliberate and configurable (`Archipelago/GourdColor`,
    default `#FFA62B`), applied through the game's own mechanism:
    `RewardGourd.isVariantChallenge` + `variantChallengeColor` set before
    activation, plus `PropertyBlockHelper.Refresh()` after. Confirmed
    working in-game. The shader property the game actually drives is
    **`_RColor`** (logged once per session from
    `PropertyBlockHelper.colorSettings`) — worth knowing if the colour ever
    needs targeting directly.
  - **Surviving a dropped connection, RESOLVED (2026-09-15) — three bugs,
    none of which would have shown up any other way than by killing the
    server mid-session.** Everything previously claimed about automatic
    recovery was deduced from the code, never observed; the first real test
    demolished it in thirty seconds.
    1. **The drop was not noticed at all.** It raised `ErrorReceived` and
       never `SocketClosed`, which was the only signal being watched, so
       the status stayed `Connected`: no warning, no retry, checks going
       nowhere while the mod believed all was well. Which event a WebSocket
       stack raises on an abrupt drop is not dependable — `Status` now asks
       `IArchipelagoSocketHelper.Connected` directly.
    2. **The retry loop wedged after one attempt.** Against a dead port,
       `TryConnectAndLogin` does not return, so the status sat on
       `Connecting` — which `ApRuntime` reads as "wait" — for minutes.
       An attempt now has 15s to answer before being written off. It cannot
       be cancelled, so it is fenced off by generation: a stale attempt
       coming back later finds itself superseded and writes nothing, which
       stops it resurrecting a dead connection.
    3. **Reconnecting spawned a second full set of gourds** (28, then 28
       more). A scope mistake: `OnJustConnected` called
       `OnWorldBecameReady`, so every *connection* cleared state describing
       the *world*. Only a world reload destroys loose gourds, so only that
       may clear it. Nothing was lost — the extras are not persisted, so
       the next reload recomputed the right number from the ledger.
    - **Validated in-game after the fixes**: amber on-screen warning, retry
      cycles every 10s throughout the outage, automatic reconnection when
      the server returned, `Resending 7 check(s)`, goal re-asserted, `105
      item(s) already applied` — and crucially the counters unchanged
      across the whole cycle (1 reconciliation, 28 spawns before and
      after), so no duplicated gourd and no replayed item.
  - **New-save replay, CONFIRMED (2026-09-15)**: a brand-new save connected
    to the same slot reported `0 item(s) already applied`, replayed the
    slot's whole history and put all 33 gourds back. Monument deposits were
    not restored, as documented and accepted.
    - **Bug it exposed, fixed the same day**: the new save got neither its
      hub shortcuts nor the true-ending sphere. `ArchDoorUnlocker` and
      `SecondEndingSphereUnlocker` each latched a `_done` on their first
      opportunity and never looked again for the life of the process, so a
      second world loaded without relaunching the game was simply skipped.
      Both now arm on the transition into a ready world. Predates the
      network client entirely — it could not show up until switching saves
      mid-process became part of testing.
  - **Outgoing check from a real puzzle, CONFIRMED (2026-09-15)** — the
    last unexercised path. Solving a puzzle in-game logged
    `[Check] gourdHighButton` and the server independently showed exactly
    8 of 81 locations checked: High Button (the puzzle just solved), plus
    the 7 the save already knew about and resent on connecting. Every one
    accounted for.
    - **Measure it on a room that has not been goaled.** A first attempt
      proved nothing: the goal had triggered the server's auto-release, so
      all 101 locations of that slot were already checked and any question
      of the form "is this one checked?" answered yes. The save was fine;
      the room was saturated.
    - **Minor, noted not fixed**: `ap_reported_*` is not scoped to a seed,
      so a save reconnected to a *different* seed resends checks it earned
      elsewhere (seen here: 7 checks carried into a fresh room). Harmless
      in real use, where a save belongs to one seed — a testing artifact
      worth knowing about before reading a check count.
- **RESOLVED (2026-09-15)** — `ItemApplier` split into two paths, confirmed
  by reading `mod/src/Core/ItemApplier.cs`:
  - `ApplyGourdItem()` (no parameter) does nothing but
    `ReceivedItemSpawner.SpawnCosmeticPickup()` — no `SaveManager` write, no
    live-effect pin, no check report. This reflects a correction made on
    2026-09-11: a received gourd item must never validate a physical puzzle.
    Gourds are a *pure* progression currency for monuments, entirely
    decoupled from puzzle resolution (see
    `apworld/design-decisions.md`'s gourds-vs-big-keys decision for why).
    Solving a puzzle in-game keeps reporting its own check normally
    (`GourdStatePatch`/`SaveValuePatch`, unaffected by received items).
  - `ApplyBigKeyItem(SaveablePropName)` keeps the old 1:1 model as-is.
  - `GourdRegistry.IsBigKey` was added to route the two existing callers
    (`DebugItemSimulator`, `DebugForceEndingFlags`) to the right path —
    confirmed present in `mod/src/Core/GourdRegistry.cs` and used in
    `mod/src/Debug/DebugItemSimulator.cs`.
  - Build validated (0 errors/warnings); not retested in-game (judged
    unnecessary — both sub-paths were already individually proven in-game
    separately, only the routing is new and trivial).
  - See `apworld/design-decisions.md` for the still-open discussion about a
    possibly finer decomposition of big keys — not affected by this fix.
- **RESOLVED (2026-09-11) — cosmetic spawn of the received gourd** — a
  physical, pickable object appearing at the hub, now spawned on every item
  applied via `ItemApplier` (`Core/ReceivedItemSpawner.cs`, new file).
  Clones a `RewardGourd` already loaded in the scene
  (`InventorySpawn.GetNextSpawnPosition()` for the position, the closest one
  to the player if several are loaded). A test session rich in Mirror/engine
  pitfalls, all resolved (see the comments at the top of
  `ReceivedItemSpawner.cs` for the full technical detail of each):
  1. Cloning an object placed in the scene (not a real dynamic prefab)
     inherits the template's `NetworkIdentity.sceneId`/`hasSpawned` →
     engine warning "already spawned" during `Awake()` (called
     synchronously BY `Instantiate()`, so too late to fix after the fact) —
     worked around by disabling the template before `Instantiate` (`Awake`
     deferred until `SetActive(true)`).
  2. `NetworkIdentity.SpawnedFromInstantiate` (set to `true` by `Awake()`)
     must also be reset to `false` — both of these
     (`hasSpawned`/`SpawnedFromInstantiate`) are exposed as PROPERTIES
     generated by Il2CppInterop, not real reflectable fields despite being
     declared as private fields on the game side (Harmony's `Traverse.Field`
     silently fails on them) — reflection on the property's setter instead.
  3. `saveablePropName` neutralized (`notSavable`) **before** activation/
     `ServerSetGourdState`, never after — tested in the other order,
     `GourdStatePatch` then treated the call as a real puzzle resolution and
     reported a fake check (`gourdTellerWindow` in that test).
  4. The real cause of "invisibility" (all Unity indicators green —
     `enabled`/`isVisible`/`activeInHierarchy` — yet nothing on screen): the
     clone **teleported** elsewhere on the map a few instants after spawn,
     via `Prop.Start()` (the load-time restoration mechanism, see dedicated
     section below) which read `startHome` (the template's original home,
     copied as-is by `Instantiate`) — neutralized (`startHome = null`)
     alongside `saveablePropName`. Diagnosed by comparing the clone's
     position right after spawn vs. 3s later (`DebugHotkeys`, P key).
  5. `Prop.SetLoose()` must be called explicitly — `RewardGourd.
     ServerSetGourdState(Loose)` only drives the puzzle's visual/logical
     SyncVar, not the `Prop`'s physical state (without this, the clone
     stays "fixed"/without gravity as if still in its clamp).
  6. The template's per-instance `MaterialPropertyBlock` (not copied by
     `Instantiate`, unlike serialized references) captured and reapplied to
     the clone as a precaution.
  Confirmed in-game: visible, normally pickable/depositable, correct
  physics, no check or `SaveManager` collision. Lifetime = until picked up
  (no timer); visible to all players (standard network spawn). Still open
  (minor, not investigated): the exact appearance (color/texture specific to
  each puzzle) is not guaranteed to match the real puzzle represented —
  `MaterialPropertyBlock` copied from the template but never visually
  verified whether it matches a specific puzzle or stays generic-looking.
  - **DESIGN REVERSAL (2026-09-11, player, question asked after the fact)** —
    the first iteration explicitly cleared `prop.propGroups` on the clone to
    prevent it from being pinned into a real monument (fear of a false
    fill-in, `PropHomeBlock.pinnedProp` being a live reference independent
    of `SaveManager`). **The player then clarified the real intent: this IS
    wanted** — since puzzles no longer grant a directly usable gourd in this
    design (the real donation goes through the AP network), cosmetic clones
    must become the **only** way to fill monuments, and that filling must
    **persist** (see `apworld/design-decisions.md` for why gourds work this
    way). `propGroups.Clear()` removed; the clone's `RewardGourd`/`Prop`
    remain fully pin-able in any real `PropHome`.
  - **RESOLVED (2026-09-11) — persistence of the fill-in, new file
    `Core/CosmeticMonumentFillTracker.cs`**: Ghidra/static investigation of
    the real mechanism before choosing an approach (the player explicitly
    requested this rather than a guessed solution). Confirmed:
    `GourdPinAudioBehaviour.GetNumberOfFilledHomes()` (and therefore the
    whole "monument filled" effect) is based entirely on **live** references
    (`PropHome.pinnedProp != null`, counted on the fly) — no counter stored
    anywhere. Load-time restoration is fundamentally **identity-based**: each
    prop finds its place again by re-reading ITS OWN `SaveManager[
    saveablePropName]` key in `Prop.Start()`. A cosmetic clone has no stable
    reusable identity without going back to the check-collision risk already
    solved (see above). The idea "reuse `gourdTesting00-39`" (40
    `SaveablePropName` values never used in normal gameplay, already
    confirmed excluded from the check registry by
    `GourdRegistry`/`GourdStatePatch`/`SaveValuePatch`) was considered then
    discarded: insufficient capacity against the ~45 real monument slots in
    the game (F6 count, if the fill-in must be possible anywhere) — and
    extending with the 6 `bigKeyTesting*` values was also explicitly
    discarded (player's question): semantically these are keys, not gourds,
    an unaudited risk that some other game system scans `SaveManager` keys
    by the `bigKey*` prefix for a reason unrelated to `PropGroup`/pin.
    - **Solution adopted: index by `PropHome` (the SLOT), not by `Prop` (the
      occupant)** — dedicated key `SaveManager["ap_home_" + saveableHomeName]
      = 1`, no possible collision with the game's `SaveablePropName`/
      `SaveableHomeName` keys, no capacity limit (one slot = one key, as many
      slots as needed).
    - **Pin detection**: `PropHome.onAnyChangeServer` — a **global static**
      event already exposed by the game (not a Harmony patch), fired for
      every pin change on every `PropHome` in the game. Il2CppInterop pitfall
      encountered subscribing to it: a direct `+=` on a method group fails
      (`PropHomeChangeEvent` is not a normal .NET delegate); `new PropHome.
      PropHomeChangeEvent(methodGroup)` also fails (the only real
      constructors are `(Il2CppSystem.Object, IntPtr)`/`(IntPtr)`, not usable
      from managed C#). Resolved by inspecting the compiled type's real
      methods (`System.Reflection.MetadataLoadContext` on
      `Assembly-CSharp.dll`, not the static Il2CppInspectorRedux dump which
      doesn't necessarily show the same thing): an implicit conversion
      operator `op_Implicit(System.Action<PropHome,Prop,Prop>)` exists — cast
      the method group to `Action<PropHome,Prop,Prop>` first, implicit
      conversion afterward. A cosmetic clone is unambiguously recognized by
      `saveablePropName == notSavable` (never true for a normal game prop) +
      the clone's name suffix.
    - **Load-time restoration**: at world-ready (same timing as
      `ArchDoorUnlocker`), iterates `PropHome.allPropHomes`, and for each home
      with an `ap_home_*` flag set and `pinnedProp == null`, directly clones
      and pins a cosmetic gourd into it (new method
      `ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo`, reuses the whole
      already-validated cloning core, `GourdState.Stashed` instead of
      `Loose`).
    - **Unverified-in-game hypothesis**: restoration runs only **once** per
      session (like `ArchDoorUnlocker`), assuming all `PropHome`s in the game
      are loaded simultaneously (open world without real zone streaming). If
      distant monuments only load as the player approaches, this single-pass
      restoration would miss ones outside the starting zone — to be
      confirmed in-game (next test session), not yet done.
    - **RESOLVED, confirmed end-to-end in-game (2026-09-11)** — two bugs
      found and fixed while testing:
      1. **`DebugCosmeticPinForce` was pinning into the wrong kind of
         `PropHome`** — `PropHome` is also used for slots carried by the
         player (e.g. an inventory "belt"), not just monuments; without a
         filter, the nearest empty gourd home was almost always this
         player-carried slot (~0 distance, attached to the player), not a
         real monument (log: "pinned in notSavable", and visually a "belt"
         appeared on the player instead of a deposit). Fix:
         `ReceivedItemSpawner.IsMonumentHome` (prefix `saveableHomeName` ==
         "monoument"), used to filter both `DebugCosmeticPinForce` AND
         `CosmeticMonumentFillTracker` (detection AND restoration).
      2. **`PropHome.onAnyChangeServer` (the global static event) never
         fires in practice** — subscription confirmed successful, but zero
         trigger despite a pin otherwise confirmed successful
         (`Prop.ServerSetPinned`) — probably a field never wired up by the
         game. The real signal is `PropHome.onChangeServer`, the
         **per-instance** version of the same event type: must subscribe
         individually to each of the ~536 loaded `PropHome`s (no working
         global shortcut). `PropHome.onPinServer` (simpler `Action<Prop>`)
         also works but doesn't cover unpinning, left aside.
      - Full test validated: cosmetic gourd spawned (P) → force-pinned into
        `monoumentIntroSlot0` (N, `DebugCosmeticPinForce`) →
        `[CosmeticMonumentFillTracker] ap_home_monoumentIntroSlot0 = 1`
        confirmed in the logs → game fully closed and relaunched →
        `[CosmeticMonumentFillTracker] 1 cosmetic gourd(s) restored to their
        monuments.` → **gourd found physically pinned in the monument**,
        confirmed visually by the player. Full chain (spawn → pin →
        persistence → reload → restoration) validated end-to-end.
      - **Hypothesis disproven then fixed (2026-09-11, same session)**: "a
        single restoration pass at startup is enough" turned out false when
        testing a monument far from the hub (Black Tower) — `onChangeServer`
        subscription absent for that PropHome (total log silence despite a
        confirmed successful pin), so no persistence, so nothing restored on
        the next reload. **Fix: `Patches/PropHomeEnablePatch.cs`** (new,
        Harmony postfix on `PropHome.OnEnable()`) — subscribes EVERY
        PropHome as soon as it becomes active, whether at initial load or
        later, rather than a single fixed pass over `PropHome.allPropHomes`
        at one instant. `CosmeticMonumentFillTracker` rewritten accordingly:
        no more single scan, a queue (`PendingRestoreCheck`) fed
        continuously by the patch and drained continuously in `Update()`
        once `WorldManager.isReadyForEffects`. **Revalidated in-game after
        the fix**: pin in `monoumentFinalSlot2` (Black Tower) →
        `ap_home_monoumentFinalSlot2 = 1` confirmed this time → full reload
        → `1 cosmetic gourd(s) restored` confirmed, without even having to
        go back there first. The full mechanism (spawn → pin → persistence →
        restart → restoration) is now confirmed independent of the
        monument's distance/zone.
    - **Debug tool added**: manual deposit into a real monument requires the
      game's normal peck/hold interaction (same friction as the "N-hold"
      bells already encountered) — `Debug/DebugCosmeticPinForce.cs`, **N**
      key, directly pins (`Prop.ServerSetPinned`, bypassing the interaction)
      the nearest cosmetic gourd into the nearest empty `PropHome` (30m).
      Triggers `PropHome.onAnyChangeServer` exactly like a real pin, so
      `CosmeticMonumentFillTracker` persists normally without special
      handling. `ReceivedItemSpawner.IsCosmeticClone` made `internal` (shared
      between `CosmeticMonumentFillTracker` and this tool).
- **Gourd deposit box (monument) detection** — at minimum the "global
  progressive counter" mode (the simplest,
  `GourdPinAudioBehaviour.GetNumberOfFilledHomes()` already spotted), meant
  to become a setting received from the AP world/slot rather than a
  hardcoded fixed mode (see `apworld/design-decisions.md` for the design
  discussion; **RESOLVED for Option A** — see
  `CosmeticMonumentFillTracker.GetFilledMonumentCount()` below).
- **Radio stations as locations** — extend `SaveValuePatch` to also try
  `Enum.TryParse<SavableSystem>` (in addition to `SaveablePropName` already
  handled), report a check when it matches. No item materialization needed
  (detection only). (Design decision: see `apworld/design-decisions.md`.)
  **RESOLVED (2026-09-21)**: the seven checks fired in game on 2026-09-20, and
  the stations have since become items as well — see "The radio" below.
- **`Config.cs`** — add the AP server config entries (address, slot name,
  password) once the network client exists; possibly expose this in the
  in-game hosting menu rather than a `.cfg` file — **RESOLVED (2026-09-15)**,
  see the dedicated section below.
- **RESOLVED (2026-09-11) — the "black sphere" at the hub/spawn, distinct
  from the Gauntlet entrance** (which had already been fixed earlier).
  Investigated in-game on a save where the vanilla game has already been
  finished once: the object is not an intact sphere that disappears, but
  visually splits into pieces (`Sphere`/`ModelOrb Colliders` +
  `_WallInterior`/`_WallRing`/`_WallAngle`/`_WallShort`/`_WallLong`/
  `_WallEnd` pieces). **None of these objects carry a
  `TrackedPeckState`/`PeckSwitch`** (just `Transform`+`Collider`) — so this
  is **not** a Peck mechanism like everything else in the game, but probably
  a decision made once at scene load (which variant spawns) by reading an
  existing flag.
  - **CORRECTION (2026-09-10)** — initial hypothesis wrong: `<steamId>
    keyWalkingProven` (seen at `=1` in the `SaveManager` dump of the
    completed save) **is not** a "vanilla game finished" flag. Decompilation
    of `PlayerTeacher` (`il2cpp.cs`): it's the game's **walking tutorial**
    system (`LearnState { Uninitialized, Observing, Teaching, Proven }`,
    `EnterTeachingZone()`/`ExitTeachingZone()`, `ServerSaveWalkingProven()`)
    — consistent with the title "Big Walk", it just materializes "this
    player has learned to walk" very early in the game, unrelated to the
    real ending. Also explains the observation: on a heavily played save,
    `keyWalkingProven` is obviously already `=1` for a long time (the
    tutorial is done at the very start), so seeing it at 1 on a "completed"
    save proves nothing specific about the real ending.
  - **So still unresolved**: either the sphere/wall found is actually the
    walking-tutorial gate itself (not a "real ending" gate), or there is
    another distinct object further along the path to the real ending that
    hasn't been located yet. **Do not force `keyWalkingProven` thinking it
    unlocks the real ending** — no useful effect for that (although probably
    harmless otherwise, an independent system from the
    goal/`EndingGate`/`GauntletComplete`). Suggested next step: compare the
    same area on a **fresh** save (never done the tutorial) against the
    completed save, to see if `keyWalkingProven` is really what makes the
    difference or something else.
  - **`skipAidsActive` lead also discarded (2026-09-10)**: F11 (`unlocks=
    true`) tested directly on the sphere — no effect, doors open but sphere
    unchanged. `SaveData.skipAidsActive` added to the F8 dump
    (`Debug/DebugSaveDump.cs`) and tested on the save where the sphere is
    already broken: **`skipAidsActive=False`** — so also not the "challenge
    already beaten, offer to skip" pattern used by the Gauntlet (`SkipAid
    Guantlet`/`SkipAidToggler`). Widened F9 keywords
    (`skip`/`proven`/`reward`/`postgame`/`complet`) found nothing new near
    `Sphere`/`_Wall*`/`ModelOrb Colliders`/`playerblocker` — still only
    `Transform`+`Collider`, no script.
  - **Ghidra decompilation of the "end of game" chain (2026-09-10)** —
    statically located: `AutomaticDisconnector.StartEndingTransition
    (PlayerCharacter)` (a `PlayerZone` that starts the transition),
    `EndingTransition.SetActive()`/`OnTransitionEnd()`, and
    `PeckEffectEndingTransition.OnPeck` (that one *is* wired to the Peck
    system, via `PeckSystemReference`/`stateFilter`). Real decompilation of
    the 4 functions (PyGhidra venv, project already labeled, cf.
    `reference-bigwalk-dev-environment`): **none of them writes to
    `SaveManager`** (neither `SetIntValue` nor `SetStringValue`). The whole
    path only: filters on the received Peck state
    (`stateFilter.specificStates`), stops the Mirror network
    (`NetworkManager.StopClient/StopServer/OnStopHost`), sets the main
    menu's entry mode (`entryMode`), and starts the fade. **Conclusion:
    there is probably no dedicated "game finished" flag** —
    `WorldMenuManager` does have two `EndingTransition`s (`endingTransition`/
    `secondEndingTransition`, standard ending vs. real ending) but neither
    of them persists anything on its own.
  - **Implication for the hub sphere**: if it really checks a notion of
    "game already finished", it probably does so by reading a **combination
    of already-known flags** (e.g. `EndingGate` + `GauntletComplete` + the 7
    big-key plinths) rather than a dedicated key we missed. Good news for
    the mod if confirmed: these flags are already the ones `ItemApplier`/
    `SaveValuePatch` handle natively, so the sphere would unlock itself for
    an Archipelago player who has done the real prerequisite checks — no
    special code expected a priori. **Still unconfirmed in-game** (didn't
    find the script reading this combination, nor tested whether setting all
    these flags together on a save that never saw the ending screen is
    enough to break the sphere).
  - **Actual state (2026-09-10, end of session)**: mechanism not identified
    with certainty, but strongly refocused on "combination of existing
    flags" rather than "dedicated flag not found". To resume: compare a
    100% fresh save at the same spot, or test forcing
    `EndingGate`+`GauntletComplete`+the 7 big keys on a save that has never
    seen the ending screen to see if the sphere breaks.
  - **NEW (2026-09-11, re-reading the 2026-09-10 `LogOutput.log`, not yet
    exploited)** — an F9 dump (`DumpNearbyByKeyword`, 30m) done right at the
    hub, on a save where only `EndingGate=1` is set (no big keys, no
    `GauntletComplete`), reveals two objects never noted until now:
    **`Spawn_SecondEnding_Sphere_Whole`** (`MeshFilter`+`MeshRenderer`+
    `SphereCollider`, 3.5m from the player — almost certainly THE intact
    sphere itself, the "Whole" name strongly implying a "broken" counterpart
    somewhere) and, in a second cluster further away (~21m, the one already
    hosting the known `ModelOrb Colliders`/`_WallInterior`/`_WallRing`/
    `_WallAngle`) an object **`SecondEndingDoorLogic`** carrying a
    `PeckEffectAnimancer`. **This contradicts the 2026-09-10 conclusion**
    ("none of these objects carry a Peck"): that's true of the `Sphere`/
    `_Wall*` objects themselves, but not further away —
    `SecondEndingDoorLogic` is indeed Peck-driven. Right next to the sphere
    (4.8m), an object **`OpenSystem`** directly carries `TrackedPeckState`+
    `PeckSystemBlock`+`PeckBusConnection`+2×`PeckEffectToggle`+
    `PeckEffectAudio` — a profile that looks a lot like the real trigger (2
    `PeckEffectToggle`, consistent with "toggle between intact and broken
    variant" + resolution sound). Also spotted:
    `Spawn_SecondEnding_Door (1)` (5.1m, a `BoxCollider` — probably a second
    door/segment of the same set) and `EndingGateIndicator` (4.9m, just
    `LODGroup`, purely visual). **Not yet confirmed**:
    `OpenSystem.savableSystem` (the real `SaveManager` key) hasn't been read
    — `DebugPeckCombinatorLookup` (F7) doesn't cover it (`OpenSystem` has no
    `PeckCombinator`), and `DebugTrackedPeckStateLookup` (Insert) is
    hardwired to `SavableSystem.SpawnHubGate` only. **Fix made this day**:
    `DebugComponentLookup` (F9) now logs, for every object found, its full
    hierarchical path (parent chain) and, if it carries a `TrackedPeckState`,
    its `savableSystem`/`saveGuid`/current `SaveManager` value (same format
    as `DebugPeckCombinatorLookup`) — build deployed (DLL copied into
    `BepInEx/plugins/`), but **not yet retested in-game**. Next session:
    stand at the hub, press F9, read the `OpenSystem` line to get the real
    key, and the hierarchical path to confirm whether
    `Spawn_SecondEnding_Sphere_Whole`/`OpenSystem`/
    `Spawn_SecondEnding_Door (1)` share the same parent (a single "second
    ending gate" set) rather than two independent mechanisms.
  - **Player's idea (2026-09-11), fallback if the flag remains hard to
    confirm**: rather than looking for the real flag/Peck mechanism, simply
    disable the blocking object (`Spawn_SecondEnding_Sphere_Whole` and/or
    its collider) at every session start — same philosophy as
    `ArchDoorUnlocker` ("no point staying closed for an Archipelago world"),
    but simpler: no dependency on `SaveManager`/persistence, just a
    `SetActive(false)`/collider disable replayed every session (unlike
    `ArchDoorUnlocker`, cannot rely on a "done once" flag since nothing would
    be persisted). Kept as a safety net if `OpenSystem` turns out to be a
    broad-broadcast trigger (like `PeckDevHelper.Trigger(unlocks=true)` was
    for the Arch doors) rather than a targeted switch.
  - **LEAD EXPLORED THEN INVALIDATED (2026-09-11)** — initial hypothesis: the
    `bigKeyPlinthGoodbye2` plinth (right behind the sphere) would be watched
    by `OpenSystem` (`PropHomeBlock`-like) and its filling would trigger the
    sphere breaking. **Corrected by the player (game knowledge): it's the
    opposite** — the `bigKeyPlinthGoodbye2` plinth is only **physically
    accessible if the sphere is already broken** (so only after a first
    vanilla completion of the game). Filling this plinth is a
    **consequence** of the broken sphere, not its **cause** —
    `bigKeyOverflow`/`Goodbye2` is a postgame bonus/collectible positioned
    behind the door, not this door's trigger. This invalidates the reasoning
    "`ItemApplier` already does a live pin so the sphere would unlock
    itself": the other way around, pinning `bigKeyOverflow` via the mod
    would not break the sphere (no causal link), even though
    `ItemApplier.TryApplyLiveEffect` can technically still pin this prop
    remotely (bypass code, without walking to the plinth) — it just has
    nothing to do with the sphere itself. **Back to square one on the
    sphere's real condition**: very probably still the original notion "game
    already finished once" (maybe just `GauntletComplete`, the real ending's
    check, given that `Goodbye2` thematically follows `GoodbyeChapel`/
    `GoodbyeVoid` on the path to the real ending) — unconfirmed, and the
    debug key **O** added this day (`ApplyBigKeyOverflowKey`) no longer
    tests a hypothesis relevant to the sphere (kept in the code, harmless,
    but don't expect an effect on the sphere from it).
  - **DECISION (2026-09-11)** — given the difficulty of confirming the real
    condition without more reverse-engineering sessions, and given that the
    mod's actual need is just "the sphere must never block an Archipelago
    player on their very first session" (regardless of the real vanilla
    condition), **the "find the real flag/Peck mechanism" lead is abandoned
    in favor of the idea already noted as a safety net**: directly disable
    the blocking object at every session start, without depending on
    `SaveManager`/any flag. Implemented: `Core/SecondEndingSphereUnlocker.cs`
    (same timing as `ArchDoorUnlocker` — polling on
    `WorldManager.isReadyForEffects`/`NetworkServer.active`), disables
    (`SetActive(false)`) every loaded GameObject whose name starts with
    `Spawn_SecondEnding` (covers `Spawn_SecondEnding_Sphere_Whole` AND
    `Spawn_SecondEnding_Door (1)`/its clones, without having to list every
    exact suffix) — prefix chosen precisely (not a generic keyword like
    "wall"/"block") to avoid any side effect elsewhere on the map. Unlike
    `ArchDoorUnlocker`, **no** "done once" `SaveManager` flag: since nothing
    is persisted by a local `SetActive`, the component must replay this
    disable at **every** session (intended behavior, per the player's
    initial request).
  - **Bug found on first test (2026-09-11)**: first deployment confirmed
    in-game — the sphere was indeed disabled, but the "second ending"
    entrance was **also** open from the start of the game, not intended
    (`Spawn_SecondEnding` prefix too broad, also caught
    `Spawn_SecondEnding_Door (1)`, the real entrance door — real progression
    toward the second ending must stay intact, only the sphere should
    disappear). **Fix**: prefix tightened to `Spawn_SecondEnding_Sphere`
    (`Core/SecondEndingSphereUnlocker.cs`), which now only touches
    `Spawn_SecondEnding_Sphere_Whole`.
  - **RESOLVED, confirmed in-game (2026-09-11, after the fix)** — sphere
    still disabled, second-ending door properly closed (progression intact).
    `SecondEndingSphereUnlocker` does exactly the expected job. End of this
    investigation — no more need to dig into `OpenSystem`/the real flag for
    this specific need (would remain of purely academic interest if ever
    useful elsewhere, but no longer blocking for the mod).
- **Idea noted (2026-09-10, player)** — color each received big key to match
  its associated tower's color (RedZone/YellowZone/GreenZone/BlueZone),
  useful for a future cosmetic spawn; `bigKeyBoss` and `bigKeyOverflow` are
  already visually consistent (black/purple), no need to touch them. Not yet
  investigated technically (which material/shader on the prop to target).
- **CORRECTION (2026-09-10, player, flow clarification) — the "black
  sphere" is the Gauntlet entrance, not a first-run gate**: the initial
  hypothesis ("obstacle conditioned on a first game completion") was wrong.
  Real flow confirmed by the player: `bigKeyBoss` obtained → the chapel
  (`GoodbyeChapel`) opens, with a bell inside (**a single bell**, not two —
  corrects the entry below); breaking the bell with 2 players
  (`NHoldLogic`/`EndingGate`, already resolved) opens a door to a huge empty
  area; at the end of that area, the famous black sphere = **the Gauntlet
  entrance** (not a re-progression obstacle); inside, 6-7 puzzle rooms
  (`GauntletChamber0..6` in `SavableSystem`, 7 values — matches), each
  opening the staircase to the next. So the "second, far-away location that
  could count as goal completion" the player initially mentioned ==
  **Gauntlet completion**, not a literal second bell.

### To test/validate in-game

- **TOP PRIORITY NEXT STEP (2026-09-10) — test the "flag combination"
  hypothesis for the hub's black sphere.** New debug key **PageDown**
  (`Debug/DebugForceEndingFlags.cs`, `ForceEndingFlagsKey`) already coded,
  built and deployed: forces all 7 big keys at once (via `ItemApplier`, no
  need to find them physically) + `SaveManager[EndingGate]=1` +
  `SaveManager[GauntletComplete]=1`. Procedure:
  1. Start a **strictly fresh** game (never seen the ending screen — otherwise
     the test proves nothing).
  2. Note the sphere's state at the hub beforehand.
  3. Press **PageDown**, reload the zone if needed.
  4. Recheck the sphere: broken into pieces → hypothesis confirmed (no
     special code needed mod-side, the sphere unlocks itself via the
     already-handled checks); still intact → hypothesis invalidated, lead to
     abandon and look for another one (see full context in the "black
     sphere" section above).
  - Full context (why this hypothesis, what's already been ruled out): see
    the "IN PROGRESS — black sphere at the hub/spawn" entry above and the
    accompanying Ghidra sub-section.
- **The chapel bell (a single one, not two — see correction above)** —
  resolved (`NHoldLogic` → `EndingGate`, minimumMatches=2). No more test
  needed for the bell itself.
- **The Gauntlet (6-7 puzzle rooms, `GauntletChamber0..6`)** — entrance = the
  black sphere at the end of the empty area past the chapel.
  `VictoryLogic`/`NHoldSucess`/`ChallengeCompletedSystem` (seen near the
  entrance/victory room) are all `NotSavable`: not the right objects for
  per-chamber persistence. Remaining: go F7/Delete **inside each individual
  chamber** (not just at the entrance) to find the `TrackedPeckState`s with
  `savableSystem=GauntletChamber0` (then 1, 2, ... 6) — they must exist
  somewhere since these values are in the `SavableSystem` enum. Also check
  if `SaveData.skipAidsActive` changes after a full Gauntlet completion
  ("already beaten, offer to skip" lead rather than a real per-chamber
  check).
- **RESOLVED (2026-09-10)** — second bell at the top of the Gauntlet = real
  "end of game" check, formally confirmed in-game. After solving the 6-7
  chambers, a **second bell** at the top of the Gauntlet (surrounded by 2
  buttons, same `NHoldLogic 2`/`PeckCombinator` mechanism, minimumMatches=2)
  does write **`SaveManager[GauntletComplete]=1`** (`SavableSystem.
  GauntletComplete`, value 28) once broken — confirmed by a full
  `SaveManager` dump (F8) right after. This is the check that corresponds to
  the "second location, goal completion" initially mentioned, not the
  Gauntlet itself nor its individual chambers.
  - **Full technical chain confirmed**: the `PeckCombinator`'s
    `rule.systems[]` was empty (pitfall encountered while testing) — the 2
    real `TrackedPeckState`s are not in `PeckRule.systems[]` but in
    `PeckRule.block.systems[]` (`rule.block` = a `PeckSystemBlock`, e.g.
    `ButtonNHoldTwo`/`NHoldLogic 2`), a field `DebugPeckCombinatorLookup`
    didn't log initially — added since. Both slots point to a
    `TrackedPeckState` named `ButtonSystem` (one per physical button,
    generic name reused everywhere — no `SaveIdentity`/`savableSystem` on
    it, only `NHoldSucess`→`onConditionMet` propagates downstream to the
    real write).
  - **Solo resolution method**: simulating button presses (`PeckSwitch.
    Peck()`, `Debug/DebugPeckFire.cs`, PageUp key) wasn't enough — either
    because "N-hold" buttons require a continuous hold (not a simple tap),
    or because the only `UpSwitch` in range was the player's own (toggled
    back instead of simulating the second player). The solution that
    worked: force the state directly (`TrackedPeckState.SetState(int)`,
    short-circuits the whole press/hold system) on the nearby
    `PeckCombinator`'s `systems[]`/`block.systems[]` — new tool
    `Debug/DebugPeckCombinatorForce.cs`, **End** key. Broke the top bell
    solo, without a second player.
  - **Chapel bell → `EndingGate` cross-checked too**: the `SaveManager` dump
    (F8) after breaking the top bell also shows `EndingGate=1` (already
    acquired earlier in the session), confirming the two bells as two
    distinct, independent checks in the same generic key/value store — no
    special mechanism needed mod-side to handle them, the already-in-place
    generic `SaveValuePatch` should capture them like any other check.
  - Still open: the Gauntlet itself (6-7 chambers, `GauntletChamber0..6`)
    still has no confirmed individual persistence — see the dedicated entry
    above, still to be investigated chamber by chamber.
  - **IMPORTANT CORRECTION (2026-09-10, player)** — `EndingGate` is **not** a
    stable "check accomplished" flag like the gourds/big keys: it's
    literally the door's live open/closed state, reused by
    `SpeechlessFieldLogic` to drive its animation (`GoToOff`=0/closed,
    `GoToOn`=1/open, `GoToAnimating`=2/in progress). Observed in-game: while
    activating the chapel door via End (`DebugPeckCombinatorForce`),
    `EndingGate` went back to **0** at the moment the door opened (probably
    a transient `GoToOff`/reset in the animation sequence, not a check
    regression). This does **not** break check detection mod-side:
    `SaveValuePatch` triggers the check report on the **first** non-zero
    write, and `CheckTracker` marks the location as permanently reported
    afterward — so even if `EndingGate` later goes back to 0, the
    already-triggered check stays acquired (no loss, no re-trigger). Just
    keep in mind: don't use "`EndingGate` is currently non-zero" as a way to
    check the door's state afterward, unlike the `gourdX`/`bigKeyXxx` keys
    which stay stable once written.
- **`DebugPeckCombinatorLookup` (F7) caused a freeze (2026-09-10)** —
  "Application Hang" on Windows (no crash/logged exception) when pressing
  F7 near `VictoryLogic`, in the Gauntlet zone (denser Peck wiring than the
  bell rooms already tested: `ChallengeCompletedSystem` carries 3
  `PeckRelay`, `VictoryDoorsBlock`/`PrecedingDoorsBlock`/`LiveSignalLogic`
  nearby). Tool hardened as a result (try/catch per combinator/rule/
  reference + log the name before reading any field, to at least know which
  object was being processed if it crashes again) — but a real native
  IL2CPP crash wouldn't be catchable by C# try/catch. **Retested
  successfully after hardening** (no freeze), but a second, totally
  independent "Application Hang" (no debug key pressed right before, normal
  engine ticking right up to the freeze) happened shortly after — probably a
  pre-existing environment issue (Steam/EOS, cf. `LogEOSRTC ... Ticks have
  been delayed` already visible in normal use, or Dissonance voice chat),
  not caused by the mod.
- **The Gauntlet does NOT persist via `SaveManager`/`TrackedPeckState`
  (2026-09-10)** — `VictoryLogic`, `NHoldSucess` and everything depending on
  them all have `savableSystem=NotSavable` and no `SaveIdentity` (confirmed
  via F7/Delete directly on them). Unlike the gourds/big keys/the
  `EndingGate` bell, nothing in the local chain writes to the generic
  key/value store. Most likely lead: `SaveData.skipAidsActive` (raw `bool`
  field found in the static dump) + the `SkipAid Guantlet` GameObject
  (`SkipAidToggler`, `PeckRelay`) spotted nearby — classic "challenge
  already beaten once → offer to skip next time" pattern. If confirmed,
  detecting the Gauntlet as an Archipelago location would need a different
  approach (watching `SaveData.skipAidsActive` or hooking `SkipAidToggler`
  directly) rather than the generic `SaveValuePatch` used everywhere else.
  Not yet verified in-game.
- `gourdTelescopeToBox` (cooperative puzzle without a clamp, needs 2
  players) — never tested: presence or not of a `RewardGourd`, exact moment
  of the `SaveManager` write, interaction with `TryApplyLiveEffect`. (See
  the dedicated "To test next session" section below.)
- Non-regression of a gourd check resolved **normally** (outside of a
  received item) after the `DebugGourdLookup.FindNearestUncollectedProp`
  fix.
- Behavior in real multiplayer with a **non-host** client (the host
  authority model is coded everywhere but never proven in real co-op with
  someone other than the host).

### Explicitly set aside / unidentified mechanisms

- **`lock_map_room`** and **Poet&Priest/Poet&Pontiff** doors — set aside for
  lack of understanding what they are; to revisit only if one of us stumbles
  on it while playing. (The decision not to explore "Key Cutters" as a
  separate check is an apworld design decision — see
  `apworld/design-decisions.md`.)

## History — initial big-key investigation (2026-09-07, resolved)

1. **Make a gourd visible at spawn/hub on item receipt.**
   `Core/ItemApplier.ApplyGourdItem` (implemented) already handles all the
   real persistence/materialization without spawning anything — the chosen
   design does **not** need a physical object carried by the player to work
   (see the "Design adopted" section below). This spawn would therefore be
   purely cosmetic/notification ("hey, you just received gourdX"), not the
   delivery mechanism itself — keep this in mind to avoid accidentally
   reintroducing the old idea of a generic hand-carried gourd.
   Technical leads already identified (unverified):
   - `InventorySpawn.GetNextSpawnPosition()`: an anchor point with random
     scatter within a radius (`spawnRadius`) — a natural candidate for the
     spawn position.
   - Cloning an existing `RewardGourd`/`Prop` in the scene
     (`UnityEngine.Object.Instantiate` on an already-present instance, no
     need to find a prefab reference) + `NetworkServer.Spawn` to network it.
     Careful: a clone keeps the original's `saveablePropName` — make sure
     this cosmetic gourd never writes to `SaveManager` under that name (or
     neutralize/detach it from the save system), otherwise it collides with
     the real received-gourd entry.
   - **RESOLVED (2026-09-11)**: lifetime = until picked up (no timer);
     visible to all players (standard network spawn, no per-connection
     scoping). Implemented in `Core/ReceivedItemSpawner.cs`, see the
     dedicated entry at the very top of this document ("To implement on the
     mod side") for the full technical detail (several Mirror/engine
     pitfalls encountered and resolved).
2. **Key system (big keys): decouple the unlocked feature from the key's
   usage.** (See `apworld/design-decisions.md` for the apworld-side
   "decouple the check from the key's usage" idea that grew out of this
   investigation.)
   A key is unlocked with a number of associated gourds (pins) and unlocks a
   game feature. Player's idea: treat separately (a) the **check** "the key
   has been used/placed" and (b) the **item** "we own the key" — same
   decoupling philosophy as for gourds (see "Design adopted" below), not yet
   applied to big keys.
   Not yet deeply investigated this session — starting leads from what's
   already known:
   - `SaveablePropName` has a dedicated family: `bigKeyIntro`,
     `bigKeyRedZone`, `bigKeyGreenZone`, `bigKeyBlueZone`,
     `bigKeyYellowZone`, `bigKeyBoss`, `bigKeyOverflow`.
   - `SaveableHomeName` has the corresponding "plinths":
     `bigKeyPlinthIntro`, `bigKeyPlinthMapRoom`, `bigKeyPlinthTrain`,
     `bigKeyPlinthSkiLift`, `bigKeyPlinthTunnels`, `bigKeyPlinthEnding`,
     `bigKeyPlinthGoodbye2` — not a 1:1 name correspondence like
     `gourdX`/`valetX` (7 keys vs. 7 plinths but different names), to
     clarify which goes where.
   - Confirmed earlier: no dedicated `BigKey` class, big keys go through the
     same generic `RewardGourd`/`Prop` pipeline as gourds — so
     `GourdStatePatch`/`SaveValuePatch`/`ItemApplier` probably already cover
     them mechanically, but the link "N pinned gourds → usable key → feature
     unlocked" (the real key-specific game behavior) hadn't been decompiled/
     traced.
   - To investigate: which function reads/counts the gourds associated with
     a key, which function represents "using the key" (the check to
     detect), and which function represents "the unlocked feature" (the
     effect to materialize on item receipt, separately from the check).

   **Ghidra investigation of 2026-09-07** — targeted decompilation
   (`PropHomeBlock`, `PeckEffectSavableHome`, `GourdPinAudioBehaviour`) via
   the local Ghidra project (`gh-pj/`, unzipped from
   `BigWalk-ghidra-project.zip`). Two extra setup pitfalls encountered (no
   PyGhidra venv found on the machine despite the 2026-09-03 note —
   rebuilt from scratch this session):
   - `NotOwnerException` when opening the project headless:
     `Big Walk.rep/project.prp` stores the creating Windows user (`OWNER`)
     and Ghidra refuses to open the project for a different local user.
     Fix: directly edit this XML (`VALUE="a26auber"` → current user) — plain
     text file, no risk.
   - Invocation that works:
     `ghidra/support/pyghidraRun.bat -H "<project folder>" "Big Walk"
     -process GameAssembly.dll -noanalysis -scriptPath <script folder>
     -postScript my_script.py` (script marked `# @runtime PyGhidra` at the
     top). The launcher installs the embedded `pyghidra` wheel by itself (no
     internet needed) in a venv under
     `%APPDATA%\ghidra\ghidra_<version>\venv` on first launch.

   Decompilation results:
   - **`PeckEffectSavableHome.Peck()` is the same generic function
     documented above for gourds, reused as-is for big keys**: the body
     contains no "big key"-specific branch — it just reads
     `prop.saveablePropName`/`propHome.saveableHomeName` (serialized
     references to the instance, configured in the Unity inspector) and
     calls `SaveManager.SetIntValue(prop.saveablePropName.ToString(),
     propHome.saveableHomeName)`. So whether the prop is a gourd or a big
     key, the generic check (`SaveValuePatch`) already detects it
     mechanically — now confirmed at the function-body level, not just by
     shared-pipeline hypothesis.
   - **`PropHomeBlock` confirmed as the generic "N homes filled → effect"
     mechanism**: `Awake()` wires `OnPinChange` to each `PropHome` in the
     group's (`homes[]`) `onChangeServer` event; `OnPinChange` recomputes
     whether all `homes[].pinnedProp` are non-null and, if the "complete"
     state changes, calls `SetFull(bool)`, which pushes `0`/`1` into
     `TrackedPeckState.SetState` (`isFullDirectControlSystem`). No hardcoded
     "BigKey" reference in these function bodies — this is probably the
     mechanism that reveals/unlocks a big key once N gourds of a zone are
     pinned, but the rest of the chain goes into the generic peck system
     reused everywhere in the game. **Not worth tracing further: the design
     adopted for item receipt (pure `SaveManager` write, never touching the
     live object) makes this part irrelevant for the mod** — whether a key
     is revealed or not in-game doesn't change how its save state is
     written remotely.
   - **Conclusion: no big-key-specific class/logic exists in the code.**
     Everything decompiled (`RewardGourd`, `Prop`, `PeckEffectSavableHome`,
     `PropHomeBlock`) is the same generic pipeline as gourds. The current
     architecture (`GourdStatePatch`, `SaveValuePatch`, `ItemApplier`'s
     "pure `SaveManager` write" design) needs **no mechanical change** for
     big keys.
   - **Only real gap identified: the `bigKeyXxx → bigKeyPlinthYyy` mapping
     table** (`GourdRegistry.TryGetHomeName` today only handles the
     `gourd`→`valet` prefix). This correspondence is a Unity *scene wiring*
     fact (which `PropHome` with which `saveableHomeName` is physically
     placed where, and which big-key vise/switch is wired to it) —
     **invisible in `GameAssembly.dll`/Ghidra**, it only exists in the
     serialized scene assets, not in the code. `bigKeyIntro →
     bigKeyPlinthIntro` was the only obvious pair by name.
   - **RESOLVED (2026-09-07)** — full mapping confirmed directly by the
     player (game knowledge, not reverse-engineering) and hardcoded in
     `GourdRegistry.BigKeyHomesByProp`:
     `bigKeyIntro→bigKeyPlinthIntro`, `bigKeyRedZone→bigKeyPlinthMapRoom`,
     `bigKeyGreenZone→bigKeyPlinthSkiLift`,
     `bigKeyBlueZone→bigKeyPlinthTrain`,
     `bigKeyYellowZone→bigKeyPlinthTunnels`,
     `bigKeyBoss→bigKeyPlinthEnding`,
     `bigKeyOverflow→bigKeyPlinthGoodbye2`.
     With this table, `ItemApplier.ApplyGourdItem` now also works for big
     keys (not just gourds) — no other mechanical change needed (see the
     Ghidra investigation above: generic pipeline, already covered by
     `GourdStatePatch`/`SaveValuePatch` for check detection).
     **Confirmed a second time, independently, later in the session**: a
     third-party document ("Big Walk Archipelago details.pdf", see the
     dedicated section near the bottom of this file) names the real towers
     (Red Funnel/Green Cup/Blue Castle/Yellow Twist/Black Monolith/Green
     Dome) and their key-deposit locations, which exactly match this table
     AND the F6 monument counts — very high confidence in this mapping now.

   **IMPORTANT CORRECTION (in-game test of 2026-09-07)** — the hypothesis
   "big keys go through the same RewardGourd/Prop pipeline as gourds"
   (confirmed "earlier" per a prior note, never verified in-game) turned out
   **false on one specific point**: a big key has **no `RewardGourd`
   component at all**. Discovered via `Debug/DebugGourdLookup.LogNearby`
   (new F5 tool, scans `FindObjectsByType<RewardGourd>` with
   `FindObjectsInactive.Include`): standing in front of a visible big key
   in-game, 0 of the 46 `RewardGourd`s in the zone (active and inactive
   combined) matched a big key — only normal gourds. So no `GourdState`
   (Locked/Loose/Stashed/Hidden) for a big key: it's a bare `Prop`, probably
   driven by `Prop.onUseAsKey` (`PeckSwitch`) and `PropGroup.BigKey` (see
   Ghidra section above), not by the gourds' clamp/basket.
   - **Consequence for check detection**: none, `SaveValuePatch` (generic
     over `SaveManager.SetIntValue`) still catches the event regardless of
     the absence of `RewardGourd` — only `GourdStatePatch` (which
     specifically patches `RewardGourd.ServerSetGourdState`) will never
     trigger for a big key, but that's not a problem since `SaveValuePatch`
     already does the job as a safety net.
   - **Consequence for debug tools**: `DebugGourdLookup.FindNearestLocked`
     (based on `RewardGourd.gourdState == Locked`) can never find a big key,
     only gourds — which is what made F4 (`DebugItemSimulator`)
     systematically blind to big keys in testing. **Fix**: new lookup
     `DebugGourdLookup.FindNearestUncollectedProp`, based on `Prop.allProps`
     (a static registry covering both gourds AND big keys) and on the
     canonical signal "`SaveManager` has no value yet for this
     `saveablePropName`" — the same signal `CheckTracker`/`ItemApplier`
     already use, rather than `gourdState` which doesn't exist for every
     prop. `DebugItemSimulator` now uses this lookup (it never needed
     `RewardGourd` anyway, only `saveablePropName`). `DebugGourdUnlocker`/F3
     stays based on `RewardGourd` for now (it simulates a real local unlock
     via `ServerSetGourdState`, a concept that only applies to gourds) —
     untested on big keys, probably not relevant for them.

## Idea noted for later — AP server config in the hosting menu rather than a BepInEx file (2026-09-09)

When the real Archipelago network client is implemented, it will need a
config (server address, slot name, password) — the obvious path: a
`BepInEx` config (`.cfg`, a pattern already used everywhere in the mod, cf.
`Config.cs`), but that forces the host to edit a text file outside the game.

Player's idea: instead handle this in the game's in-game hosting screen,
which already has a name/password flow (seen in-game: `SetGameName`,
"password is correct", cf. session logs). If AP "slot name"/"password"
semantically overlap what the player already types to host, it's possible
to reuse these fields rather than adding a dedicated screen — the AP server
address would still need to go somewhere. Would require injecting real
Unity UI (not just hotkeys/logs like the rest of the mod today) or extending
`WorldMenuManager` — a project of its own, not yet started, not yet
investigated in detail (which fields exist precisely on this screen, where
to intercept them).

**RESOLVED (2026-09-15)** — work done in-session, end-to-end in-game
(screenshots confirmed by the player):

- **Layout confirmed** via a new tool `Debug/DebugMenuLookup.cs` (M key,
  dumps the full Unity hierarchy of a screen) rather than guessing:
  `HostMenuConfirm` (`MainMenu/HostMenu_Setup/Right/GameSlotCard_Editable`)
  positions everything by hand (no LayoutGroup) — `GameName` full-width
  (900) on its own row, `Password`/`LastPlayed` side by side below (399.42
  wide each, 489.4 offset).
- **Host:port field added** (`Patches/HostMenuConfirmPatch.cs`, Harmony
  postfix on `HostMenuConfirm.OnEnable`): clone of `GameName` (so of its
  real `MultiPlatformInputField : TMP_InputField`, not a hand-assembled
  TMP_InputField) rather than building from scratch — same philosophy as
  the `ReceivedItemSpawner` cloning for cosmetic gourds. `GameName` shrunk
  to 399.42 to make room for the clone to its right (X+489.4, player
  decision to place it "facing the save name"). Pitfall encountered: a
  direct cast `(RectTransform)transform` is invalid under Il2CppInterop
  (`InvalidCastException`) — fixed with `GetComponent<RectTransform>()` as
  everywhere else in the mod. The labels (`GameNameTitle`/`Placeholder`)
  carry a `LocalizedText` that would rewrite the text on every language
  refresh — disabled on the clones, text set directly hardcoded.
- **Password made optional** — targeted Ghidra decompilation
  (`HouseAuthenticator.OnPasswordResponseMessage`, `HostMenuConfirm.
  RequiredInputsHaveValues`/`IsReadyToContinue`/`ActionStart`, via an ad hoc
  PyGhidra script rather than the full labeling script — labels already
  applied to this project, just a name-based search + targeted
  decompilation): `HouseAuthenticator` only does an ordinary string
  comparison (empty == empty passes fine, Mirror requires nothing special);
  the "non-empty" constraint comes solely from the serialized boolean
  `HostMenuConfirm.passwordRequired`, which gates the 3 methods. One-line
  fix: `passwordRequired = false` in the same patch — the field becomes
  optional, but if filled in, its value is still written/transmitted
  normally.
- **Reuse of existing fields as AP identifiers** ("hybrid" scheme discussed
  on 2026-09-15): `GameName`→"SLOT NAME :", `Password`→"ARCHIPELAGO
  PASSWORD :" (labels renamed in place, `LocalizedText` disabled the same
  way; the underlying local save logic — `SaveData.slotName`/`password`,
  `NetworkMinder.SetServerPassword` — stays unchanged, it's the future
  network client that will read these same fields differently).
- **`characterLimit`** of the cloned host:port field removed (`= 0`,
  inherited from `GameNameInput` where it limits a save name's length — too
  short for `archipelago.gg:38281`, spotted by the player while testing).
- **Persistence of the host:port value** (`Core/ApSessionConfig.cs` for
  runtime reading + `ModConfig.ArchipelagoHostPort`, new `[Archipelago]`
  section of the `.cfg`, for disk persistence): pre-filled with
  `archipelago.gg:` on the very first launch, then remembers the last value
  entered from one session to the next (player request).
- **RESOLVED (2026-09-15) — connection tested before "Continue"**, now that
  the network client exists: `Patches/HostMenuConfirmStartPatch.cs` +
  `Core/Net/ApConnectionTest.cs`. A Harmony prefix on `ActionStart` fires a
  throwaway login (`ItemsHandlingFlags.NoItems`, `requestSlotData: false`,
  disconnected immediately so it never competes with `ApRuntime`'s real
  session) and holds the screen; a postfix on `Update` continues
  automatically once it answers. Uses the game's own `InputWarningFlasher.
  Flash()` on failure, as planned.
  - **Design rule it is built around: it can never prevent hosting.** A
    failed probe flashes, logs the reason, and the next press hosts anyway
    — an unreachable AP server is not a reason to be unable to launch the
    game, and neither is a bug in this patch. `OnEnable` resets the verdict
    (including a previous bypass, which was consent for that attempt only).
  - Both paths validated against a live local room: a correct slot logs in
    in ~0.2s, a mistyped one comes back with the genuinely useful *"The slot
    name did not match any slot on the server."* rather than a timeout.

Nothing to reopen UI-side for now, everything holds until the real AP
network client exists and needs to read `ApSessionConfig.HostAndPort`/the
slot name/the password when connecting.

## Technical context

- **Game**: Big Walk (House House / Panic), released August 4, 2026
- **Engine**: Unity, **IL2CPP** backend (not Mono)
- **Multiplayer networking**: **Mirror** (`NetworkBehaviour`, `[SyncVar]`,
  `[Server]`, `[Client]`)
- **Authority model**: the **host is the source of truth** for save data and
  game state (confirmed by the official FAQ and by the code: every save
  write goes through `[Server]` methods)

## Tools used

| Tool | Role |
|---|---|
| [Il2CppInspectorRedux](https://github.com/LukeFZ/Il2CppInspectorRedux) | Extracts `GameAssembly.dll` + `global-metadata.dat` → C# prototypes (structure) + Ghidra metadata |
| Ghidra 11.3+ (**PyGhidra** mode, via `support/pyghidraRun.bat`) | Disassembly/decompilation of the native IL2CPP binary, importing the `il2cpp.h` header (**empty C profile**, `-D_GHIDRA_` option) |

**Pitfall encountered**: the `il2cpp.py` script crashes at the "Processing
constructed generic methods" phase (an obfuscated namespace name invalid for
Ghidra). Doesn't matter for our needs: the previous phase ("Processing
method definitions", the non-generic methods — the bulk of the gameplay)
completes successfully **before** the crash, and labels are already applied
at that point.

**Update (2026-09-03)** — two additional pitfalls encountered while
replaying this workflow in **headless** PyGhidra (not the interactive
console):
1. Despite the sentence above, the provided `BW_metadata_ghidra/Big Walk`
   project had **in fact never had its labels applied/persisted** (144k
   generic `FUN_xxxxxxxx` functions found at the start of this session) —
   the original note described the intent/an earlier unsaved run, not the
   actual state of the project on disk.
2. `analyzeHeadless.bat -postScript foo.py` fails on any script marked
   `# @runtime PyGhidra` (like `il2cpp.py`) with *"Ghidra was not started
   with PyGhidra. Python is not available"* — must go through the
   `pyghidra` pip package (venv at
   `%APPDATA%\ghidra\ghidra_<version>\venv`, already installed on this
   machine) and its `pyghidra.run_script(...)` function/manual execution,
   not the classic `analyzeHeadless.bat`.
3. In this "synthetic" execution mode (project opened via
   `pyghidra.core._setup_project`/`_setup_script`, not a real script
   launched from Ghidra's script manager), `getSourceFile()` returns `None`
   — `il2cpp.py` silently crashes in `get_script_directory()`
   (`AttributeError` on `.getParentFile()`) trying to locate `il2cpp.json`
   next to itself. **No trace of this error appears in the logs** (the
   `PyGhidraScript.run()` wrapper swallows the exception and prints it via a
   Java `PrintWriter` that never seems to flush before the process ends) —
   only a direct Python `exec()` with native exception capture reveals the
   real traceback. Minimal fix: patch `get_script_directory()` to return the
   hardcoded path instead of depending on `getSourceFile()`, without
   touching the labeling logic itself.

Once these two pitfalls were worked around, importing the header (92 MB,
2.3M lines) took **~99s**, and the full script run (up to the expected crash
on generic methods) **~136s** — much faster than feared (initially estimated
30-60+ min and this investigation set aside for that reason). Result:
**96,933 named functions** (vs. 98 before, which were just the PE's native
exports).

## Chain from the player's action to the disk save

```
Player pins/deposits a gourd
        │
        ▼
RewardGourd.ServerSetGourdState(GourdState)   ← [Server], authoritative network entry point
        │  (updates the SyncVar + also invokes the hook locally on the server)
        ▼
OnChangeGourdState(old, new)                  ← Mirror (SyncVar) hook, runs on server AND clients
        │  → invokes GourdMap.refreshFlag(saveablePropName, newState)  [PURELY VISUAL: updates the small map flag]
        │
        ▼ (separate path, inside ServerSetGourdState)
Prop.SetSaveType(Always | Never)              ← Always if Loose/Stashed, Never otherwise
        │
        ▼
Prop.SavePropHome(propHome, isPinned)
        │  key   = SaveablePropName.ToString()   (e.g. "gourdCabinFever")
        │  value = propHome.saveableHomeName if isPinned, else 0
        ▼
SaveManager.SetIntValue(key, value)           ← PERSISTENT WRITE (static, wraps SaveData)
        │
        ▼
SaveData.entries : List<SaveEntry>            ← string key / int value pairs, LINEAR scan (no dictionary)
```

**Read** (at load time): `SaveManager.GetIntValue(key)` → `SaveData.
GetIntValue()` does the same linear scan in reverse.

**Correction (2026-09-07, typed decompilation of `RewardGourd.
ServerSetGourdState` itself — never verified before, the diagram above dated
from a hypothesis made in the very first session)**:
- `ServerSetGourdState` does **not** act on `this.prop`, but iterates
  `propsToMakeSavable` (a `PropBlock[]`, each `PropBlock` containing its own
  `Prop[]`) and calls `Prop.SetSaveType(Always)` on each if `newGourdState`
  is `Loose` or `Stashed`.
- `Prop.SetSaveType(Always)` only calls `Prop.SavePropHome` (so
  `SaveManager.SetIntValue`) **if the prop is already pinned to a valid
  `PropHome`** (`propHomeShellReference` resolved). Just released from the
  clamp, a prop isn't pinned anywhere — so `ServerSetGourdState(Loose)`
  alone, **with no complementary action, writes nothing to the save**.
- This confirms and explains the "3rd write path" found on 2026-09-03: it's
  `PeckEffectSavableHome.Peck()` (the "vice launch switch") that does the
  real work by calling `Prop.ServerSetPinned(fallbackBasket)` right after
  resolution — `ServerSetGourdState` alone never suffices to persist,
  whether in normal gameplay or a forced call.
- **Consequence for the mod**: `Plugin.DebugGourdUnlocker` (debug unlock,
  F3) initially just called `ServerSetGourdState(Loose)` — this did trigger
  the check (via `GourdStatePatch`) but didn't survive a reload (confirmed
  in-game: the gourd reappeared in its clamp). Fix: also call
  `Prop.ServerSetPinned(prop.startHome)` right after, to simulate the
  pinning that `PeckEffectSavableHome` does for real.

**RESOLVED (2026-09-03, typed Ghidra decompilation)** — the exact point
where state is re-read at load time to reposition a `Prop`/`RewardGourd`:
**`Prop.Start()`**. Unity calls `Start()` once per instance when its
GameObject becomes active in the loaded scene — this is a **per-prop**
mechanism, not a global scan done by some manager. Logic (server/host side
only, `netIdentity.isServer` checked first):

```
Prop.Start()
  │  (only if isServer == true)
  ▼
key = saveablePropName.ToString()  (or savablePropGuid if canSaveHomeWithGuid)
  ▼
savedValue = SaveManager.GetIntValue(key, 0, false)
  │
  ├─ savedValue == 0 → home = startHome (prefab's default position)
  │
  └─ savedValue != 0 → home = <PropHome found by iterating the static
  │                              PropHome registry, the one whose
  │                              .saveableHomeName == savedValue>
  ▼
Prop.ServerSetPinned(home)        ← actually pins the prop to this home
  │  (unpins the previous home if any, updates NetworkpropHomeShellReference,
  │   triggers home.onPin / home.onPinServer / home.onChangeServer)
  ▼
Prop.LocallySetPinned(home, ...)  ← called downstream, rewrites
                                      SaveManager.SetIntValue(key, home.saveableHomeName)
                                      (so idempotent: re-pinning an already-saved home
                                      rewrites the same value)
```

`Prop.SavePropHome(propHome, isPinned)` (already documented above) is the
"pure" write: `SetIntValue(key, isPinned ? home.saveableHomeName : 0)`,
called notably from `Prop.SetSaveType` when the save type changes.

**Direct consequence for receiving an Archipelago item** (replaces
recommendation #2 further below, now validated rather than hypothetical):
- **Case: zone already loaded**: call `SaveManager.SetIntValue(key,
  homeValue)` (persistence) **and** find the live `Prop` instance (via the
  `PropHome`/`GourdMap.GetFlag` registry) to call
  `Prop.ServerSetPinned(propHome)` directly — this exactly replicates what
  `Prop.Start()` does, so no need to know a hidden API.
- **Case: zone NOT loaded**: it's enough to write
  `SaveManager.SetIntValue(key, homeValue)` **and nothing else** — the next
  time this zone loads, that prop's `Prop.Start()` will read this value
  itself and self-pin. **No need to keep a reference in memory, no need for
  "catch-up on zone load" logic mod-side**: the game already does this
  natively. This fully answers the "we can't keep a reference after a quit"
  concern.

**Design question — where to materialize a received item?** (**SETTLED on
2026-09-07**, after in-depth investigation; the initial 2026-09-03
discussion below is kept for the record)

Two leads considered then discarded before finding the right one:
- **Generic gourd spawned at the hub, carried to any free slot**: discarded.
  `SaveableHomeName` has exactly 141 entries, **all** dedicated to a
  specific puzzle/monument/big-key (confirmed by a full reflection dump of
  the enum) — no generic/free `valetXxx` slot exists at all. A generic
  gourd therefore has physically nowhere to pin without colliding with a
  real puzzle's slot (`PropHome.pinnedProp` is a singular field — only one
  prop per home at a time). Moreover, the game's progression seems to be
  counted by filled home (`GourdPinAudioBehaviour.GetNumberOfFilledHomes()`),
  so a generic gourd not routed to its real `valetXxx` wouldn't advance
  vanilla progression at all.
- **1:1 named item, immediate live pin via `Prop.ServerSetPinned`**:
  discarded too, for a different reason — this would physically move the
  live object out of its clamp (repositioning done by `LocallySetPinned`)
  while `gourdState` would stay `Locked` (untouched), creating a visually
  inconsistent state, and above all it reuses the **same scene object**
  that must stay available for the player to trigger the real check later.

**Design adopted: 1:1 named item, pure `SaveManager` write, never touching
the live scene object.**

```
Receiving an Archipelago "gourdX" item
        │
        ▼
SaveManager.SetIntValue("gourdX", valetX)   ← direct write, NO call to ServerSetPinned,
                                               NO interaction with the live RewardGourd/Prop
```

Why this solves both problems at once:
- **The puzzle stays intact and solvable**: the scene object (clamp + gourd)
  is never touched by item receipt, so the local check (`GourdStatePatch` on
  `Loose`) always stays normally triggerable by the player, whether or not
  the item has already been received.
- **Progression stays correct, no hack needed**: on the **next** load of the
  relevant scene, `Prop.Start()` (the already-validated restoration
  mechanism, see the "RESOLVED" section above) reads this value and does the
  real pin (position, `PropHome.pinnedProp`, triggering `onPin`/`onPinServer`
  events) exactly as for a normal resolution from a previous session — no
  need to hack `ProgressTracker`/`EndingTransition`, the game's native
  progression system sees a perfectly consistent state.
- **Idempotent regardless of whether the physical resolution happens before
  or after item receipt**: the real deposit
  (`PeckEffectSavableHome.Peck()` → `Prop.ServerSetPinned`) writes the
  **same key with the same value** (same gourd, same 1:1 target) — so
  regardless of the order of the two events, the final result converges on
  the same correct persistent state, with the live state
  (`PropHome.pinnedProp`) additionally updated immediately by the player's
  real action.

**Assumed trade-off**: no instant visual update if the player is already in
the puzzle's scene when the item arrives — the scene must be left/reloaded
for the clamp to empty in front of them. Acceptable and common in
Archipelago clients ("applies on next load").

**Leads explored and discarded along the way** (kept in mind in case a
future need makes them relevant again):
- `PropInventory`/`InventoryZone`/`SaveData.inventory` (a `List<string>` of
  GUIDs, separate from `entries`): a real generic "prop carried without a
  fixed home" system, with physical trigger zones
  (`InventoryZone.OnPropEnterZone`). Useful for objects without a dedicated
  slot, but doesn't seem to trigger vanilla progression (no link found with
  `GetNumberOfFilledHomes`) — not adopted here since Archipelago gourds are
  named 1:1 and do have a target `valetXxx`.
- `InventorySpawn.GetNextSpawnPosition()`: an anchor point with random
  scatter within a radius (`spawnRadius`) — probably the physical respawn
  point of "inventory" props on scene load. Relevant if we ever need to make
  a genuinely new physical object appear (not our case here, we reuse the
  existing puzzle object).
- `ProgressTracker.currentValue` (public get/set, no `NetworkBehaviour` so
  no Mirror guard): directly modifiable, but turned out to be a generic
  component reused by mini-puzzles (`PressInOrder`), not a global end-game
  counter — so not the right lever to "cheat" the global victory condition.
  The real ending condition is still to be precisely identified (candidates:
  the 9 `monoumentFinalSlot0-8` slots, `EndingTransition`/
  `EndingFadeBlind`) — not needed with the adopted design, which lets native
  progression do its job normally.

<details>
<summary>Initial discussion of 2026-09-03 (superseded, kept for the record)</summary>

The Archipelago community seems to agree on making the received item appear
**at the players' hub** rather than at its original `PropHome` (tied to the
puzzle). Leads to check before settling/implementing:
- The already-observed behavior "gourd carried in hand → sent back to the
  players' base on disconnect" (see next section) suggests a hub/deposit
  zone system already exists natively in the game — probably more
  appropriate to reuse than to reinvent a homemade spawn point.
- `SaveManager.SetInventory()` / `SaveData.inventory : List<string>` is a
  list **separate** from the `SaveData.entries` key/value dictionary — lead
  to check: maybe this is the real "object carried/owned by the player"
  mechanism (rather than `PropHome`), which would change the approach for
  materializing a received item.
- Persistence while not stashed: `Prop.Start()` re-reads `SaveManager` and
  re-pins the prop on **every** load (not just at grant time), so as long as
  the save entry stays non-zero — regardless of the exact home (hub,
  basket, or final position) — the item is never lost between two sessions,
  even if the player hasn't "put it away" for good. The precise detail of
  "where exactly it lands if not stashed" (fallback logic on disconnect/
  reload) remains unidentified precisely in code — probably logic separate
  from `Prop.Start()`, to dig into via Ghidra the day this design point
  needs to be settled.

</details>

**Update (in-game test session of 2026-09-03)** — behavior of the "fallback
basket" for a loose gourd, confirmed by observation + user explanation (not
static reverse-engineering):
- Solving a puzzle (a peck that releases the gourd from the clamp) triggers
  `PeckEffectSavableHome.Peck()` on the associated "vice launch switch"
  GameObject, which calls `Prop.SavePropHome` **directly**, without going
  through `RewardGourd.ServerSetGourdState`. Observed Unity log:
  `GourdViceLaunchSwitch (PeckEffectSavableHome) has saved prop
  gourdTellerWindow with new home valetTellerWindow`.
- This home (`valetTellerWindow` in the example) is a fallback point close
  to the puzzle — not the final/trophy position — created specifically for
  reconnection: if nobody picks it up, the loose gourd must be findable
  rather than lost on reload.
- Three cases on disconnect (observed behavior, not yet verified in code):
  1. Gourd carried in a player's hand → sent back to the players' base
     (hub).
  2. Gourd abandoned in the world (dropped, not carried) → sent back to the
     basket near its original puzzle.
  3. Gourd already stashed at its final home → stays in place (normal
     behavior).
- Consequence for the mod: `PeckEffectSavableHome` is thus a **third write
  path** to `SaveManager.SetIntValue`, distinct from
  `RewardGourd.ServerSetGourdState` (`Loose`/`Stashed`), confirming the need
  for the generic safety net already recommended below (`SaveValuePatch`,
  implemented mod-side). It also partly answers the "unresolved" point
  above: this fallback-basket system is probably related to the load-time
  restoration mechanism, though the exact startup re-read function remains
  unidentified.

## Key classes and structures

### `SaveablePropName` (enum) — the location/item table

64 unique identifiers excluding test values (`gourdTesting00-39`):
- **57 gourds** (`gourdCabinFever`, `gourdHighButton`, `gourdFielding`, ...
  `gourdPoetAndPontiff`) — one per named puzzle/challenge in the game
- **7 big keys** (`bigKeyIntro`, `bigKeyRedZone`, `bigKeyGreenZone`,
  `bigKeyBlueZone`, `bigKeyYellowZone`, `bigKeyBoss`, `bigKeyOverflow`) —
  major per-zone unlocks
- `notSavable = 0` (null value)

This is the most direct candidate table for defining Archipelago
**locations** (see the provided `types.cs`/`il2cpp.cs` file for the full
list with integer values).

### `GourdFlag.GourdState` (enum)
```csharp
Locked = 0
Loose  = 1
Stashed = 2
Hidden  = 4
```

### `RewardGourd : NetworkBehaviour`
- `gourdState`: Mirror SyncVar, hook `OnChangeGourdState`
- `ServerSetGourdState(GourdState)`: `[Server]`, the only authorized write
  point for the state
- `prop`: reference to the associated physical `Prop`
- `propsToMakeSavable`: secondary props whose `SaveType` follows the main
  gourd's

### `GourdMap : MonoBehaviour`
- Central registry of every active `GourdFlag` in the loaded zone
  (`List<GourdFlag> flags`)
- `static Action<SaveablePropName, GourdFlag.GourdState> refreshFlag` —
  static event, a good hook point for observing state changes live without
  having to hook each `RewardGourd` individually
- `GetFlag(SaveablePropName)` — direct lookup by identifier

### `Prop : NetworkBehaviour`
- `SetSaveType(PropSaveType)`: triggers `SavePropHome` if the type changes
  and a valid `PropHome` exists
- `SavePropHome(PropHome, bool isPinned)`: writes to `SaveManager` (see
  chain above)
- `saveablePropName`, `savablePropGuid`, `canSaveHomeWithGuid`: two possible
  identification systems (named enum, or GUID for unnamed/dynamic props)

### `SaveManager` (static)
```csharp
static SaveData currentData
static void SetIntValue(string key, int value)
static int? GetIntValue(string key)
static void SetStringValue(string key, string value)
static string GetStringValue(string key)
static void SetInventory()
static bool GetIsInInventory(string savablePropGuid)
```

### `SaveData`
```csharp
List<SaveEntry> entries          // string key / int value pairs, linear scan
List<SaveEntryString> stringEntries
List<string> inventory           // carried/transportable inventory items
```

### `PropHome : MonoBehaviour`
- "Storage position" system for objects (different from the reward system —
  used to remember where an object was left)
- `saveableHomeName` (`SaveableHomeName` enum, 141 values): slot identifier,
  including the `valetXxx` challenges corresponding in fact to the **same
  puzzles** as the `gourdXxx` values of `SaveablePropName` (both enums use
  parallel names for two different aspects — the reward-gourd vs. the slot
  to store it in)
- `onPinServer`, `onChangeServer`: server-side events triggered when a prop
  is placed/removed

## Recommendations for the Archipelago hook

1. **Check detection**: Harmony **postfix** hook on
   `SaveManager.SetIntValue(string key, int value)`, filtered on keys
   prefixed with `gourd`/`bigKey` and `value != 0`. A single generic
   interception point instead of hooking 57+ individual `RewardGourd`
   instances.

2. **Sending a remote item** (unlocking a gourd sent by another player) —
   **settled on 2026-09-07** (see "Design adopted" above), replaces the
   initial `ServerSetGourdState(Stashed)` hypothesis **and** the previous
   version of this recommendation (which wrongly suggested a live pin in a
   loaded zone):
   - **Always**: `SaveManager.SetIntValue(key, homeValue)` alone, regardless
     of whether the zone is loaded — **never** a direct
     `Prop.ServerSetPinned` on the live instance. A live pin would
     physically move the object out of its clamp without `gourdState`
     following (visual inconsistency) and would make the associated local
     check impossible to re-trigger.
   - The relevant prop's `Prop.Start()` will read the value and self-pin on
     its next scene load — no additional mod code needed. Assumed
     trade-off: no instant effect if the player is already in the zone at
     receipt time.

3. **Respecting the host authority model**: every write must happen
   **host-side** (the `[Server]` methods already guarantee this — a
   client-side hook on a non-host client would be ignored or risk a
   desync). The Archipelago mod will need to run host-side, or transmit its
   orders to the host through a separate channel if the mod's architecture
   separates it from the game process.

3bis. **Making the physical gourd disappear after a check** (validated
in-game on 2026-09-03, solo): `RewardGourd.ServerSetGourdState(GourdState.
Hidden)` alone only refreshes the map icon (`GourdMap.refreshFlag`) — the 3D
model stays visible and pickable. `GameObject.SetActive(false)` alone would
be purely local and would desync other clients in co-op (Mirror doesn't
replicate this). The combination that works: `Mirror.NetworkServer.
UnSpawn(gameObject)` (removes the networked object from every client's view
without destroying it permanently, unlike `NetworkServer.Destroy`) **then**
`gameObject.SetActive(false)` locally, host-side.

   **Persistence confirmed** (same test session, full game restart
   afterward): after a full restart, the gourd does **not** reappear in the
   basket near the puzzle (basket visible but empty) — the "already
   collected" state (`SaveManager` did write `gourdTellerWindow`/
   `gourdHighButton` with a non-zero value) survives a full reload, with no
   extra mod code. This suggests the load-time restoration logic (still not
   precisely identified, see "unresolved" above) already respects the
   persistent state and doesn't reinstantiate a `RewardGourd` already
   marked as collected — good news for a future Archipelago session
   reload's fidelity. Not yet tested: reconnecting mid co-op session (only
   full restart tested), and the case of a gourd that would this time have
   actually been stashed.

4. **RESOLVED (2026-09-07)** — formerly "next research lead": load-time
   state restoration did re-trigger an already-validated check. Two
   discoveries while implementing item receipt (`Core/ItemApplier.cs`):
   - `RewardGourd.ServerSetGourdState(Loose)` is **genuinely called again**
     (not just `Prop.Start()`) on zone load for an already-resolved gourd —
     confirmed in-game via a temporary diagnostic flag. A real, legitimate
     restoration of the gourd's state, not a re-resolution by the player.
   - `CheckTracker` (dedup shared between `GourdStatePatch` and
     `SaveValuePatch`) was a plain **in-memory** `HashSet`, empty on every
     process restart — so unable to prevent this recall from re-triggering
     a check-report on **every** game launch, for **any** already-resolved
     gourd (a general bug in the detection system, not specific to item
     receipt — just never noticed before, for lack of testing a restart
     with an already-acquired check in the log history).
   - **Fix**: `CheckTracker.TryMarkReported` now persists in `SaveManager`
     (key `ap_reported_<locationId>`, value `1`) instead of a `HashSet`.
     Tested in-game: the check no longer re-triggers after a second
     restart. Positive side effect: this also explains why the gourd never
     visually reappears in its basket after restoration —
     `GourdStatePatch` runs its "hide the object" part
     (`UnSpawn`+`SetActive(false)`) every time `Loose` is recalled,
     including during restoration; only the check *report* is deduplicated,
     not the hiding. Consistent with the 2026-09-03 observation ("basket
     visible but empty").
   - A discarded lead along the way: comparing the old value to the new one
     in `SaveValuePatch` (`SaveManager.GetIntValue(key, 0, false)` before
     the write) — logically correct but insufficient alone, since the real
     trigger of the phantom check also went through `GourdStatePatch` (not
     just `SaveValuePatch`). The lesson: deduplicate centrally
     (`CheckTracker`) rather than in each individual patch.

## Receiving an Archipelago item (implemented on 2026-09-07)

See "Design adopted" above for the reasoning. Implementation:
`Core/ItemApplier.cs` (`ApplyGourdItem(SaveablePropName)`), simulable
in-game via the F4 debug hotkey (`Debug/DebugItemSimulator.cs`), without a
real Archipelago connection.

Full test cycle validated in-game:
1. F4 on a locked gourd → direct `SaveManager` write, no immediate
   check-report (`CheckTracker` pre-marked before the write), scene object
   intact.
2. Full restart → `Prop.Start()` genuinely re-pins the prop,
   `SaveManager` confirmed on disk (`.sav` inspected directly).
3. Attempted physical resolution of the same gourd afterward → clamp
   already open (the prop has been relocated, nothing left to resolve), no
   duplicate check.
4. Second restart → still no phantom check (`CheckTracker` persistence fix
   confirmed).

Still-open point (minor, cosmetic): the gourd's 3D model isn't visible
either in its clamp or on the basket after restoration — explained above
(hidden by `GourdStatePatch`), consistent with a genuinely completed
check's behavior. Not yet verified: a real check normally resolved in-game
(outside of a received item) keeps working without regression after this
fix.

## Receiving an Archipelago item — in-game validation for big keys (2026-09-07)

Full cycle tested in-game for `bigKeyIntro` (after the
`DebugGourdLookup.FindNearestUncollectedProp` fix, see big-key section
above):
1. F4 in front of `bigKeyIntro` (still uncollected) → `[ItemApplier] Item
   applied: bigKeyIntro -> bigKeyPlinthIntro.` — immediate `SaveManager`
   write, no interaction with the live object.
2. The associated bridge (the feature unlocked by this key) **does not open
   immediately** — expected behavior, identical to the trade-off already
   documented for gourds ("no instant visual update until the zone is
   reloaded").
3. Full game restart → on reload, **the bridge is indeed open**. Confirms
   that `Prop.Start()` does the real pin (`ServerSetPinned`) for a big key
   exactly like for a gourd, and that this pin triggers the real native
   game effect (`onPinServer`/`onChangeServer` on the plinth's `PropHome`)
   with no extra mod code.

Conclusion: the "pure `SaveManager` write" design works identically for
gourds and big keys, no behavioral divergence — the only real difference
between the two was the absence of a `RewardGourd` component on big keys
(already documented above), which only affects debug/detection tools, not
the item-receipt mechanism itself.

A colored key was tested right after (combined with the instant effect
below) and the player confirmed it worked ("it worked") — but without a log
capture specifying which one or the exact pair obtained, so **not a formal
confirmation** at the same level as `bigKeyIntro`. Non-regression of a real
gourd check resolved normally (outside a received item) after the
`FindNearestUncollectedProp` fix: still not tested.

## Formal confirmation of the last 4 big-key mappings (2026-09-10)

Fresh game (`New Game`), the 4 remaining mappings tested one by one via F4,
log confirmed (`BepInEx/LogOutput.log`) for each:

- `bigKeyRedZone -> bigKeyPlinthMapRoom` — opens the map room. **Immediate**
  effect (no wait for reload — see `ItemApplier.TryApplyLiveEffect`, live
  pin for any prop without `RewardGourd`).
- `bigKeyYellowZone -> bigKeyPlinthTunnels` — opens the tunnel. Immediate.
- `bigKeyBoss -> bigKeyPlinthEnding` (the "black" key, **Black Monolith**
  tower in the third-party doc) — opens the ending zone. Immediate.
- `bigKeyOverflow -> bigKeyPlinthGoodbye2` — opens the **true ending** zone.
  Immediate.

All 7 big-key mappings are now formally confirmed in-game (`bigKeyIntro`,
`bigKeyGreenZone`, `bigKeyBlueZone` previously + these 4). Full table in
`GourdRegistry.BigKeyHomesByProp`, unchanged.

**Non-regression confirmed**: save & quit after the 4 tests, game relaunch
→ the 7 keys/doors already opened stay open (no regression, `Prop.Start()`
correctly restores the persistent state from cold).

Two observations noted by the player during this test, reported in the "To
implement mod-side" section at the top of this document: idea of cosmetic
key coloring by tower, and a black sphere physically blocking access to the
true ending (conditioned on a first game completion, mechanism unidentified
in code).

**RESOLVED (2026-09-09)** — `bigKeyGreenZone→bigKeyPlinthSkiLift`
(chairlift) and `bigKeyBlueZone→bigKeyPlinthTrain` (train) confirmed
individually in-game, each via a dedicated debug key (F7/F8, cf.
`Config.cs`/`DebugHotkeys.cs`, no longer needing
`FindNearestUncollectedProp` so testable without standing in front of the
object). Instant effect (`TryApplyLiveEffect`) validated for both: the
player confirms the chairlift and the train activated immediately after
F7/F8, without a reload. Detail observed during testing: the key visually
disappears from the monument's socket at the moment of the live pin —
expected, not a bug (the execution order in `ItemApplier.ApplyGourdItem`
already reports the check *before* `TryApplyLiveEffect` moves the object to
its deposit plinth; the socket's disappearance is just the visual
consequence of the `ServerSetPinned` toward the plinth, not a state loss).
With `bigKeyIntro` (2026-09-07), 3 color/plinth mappings are thus formally
confirmed to date; the remaining 4 (`bigKeyRedZone`, `bigKeyYellowZone`,
`bigKeyBoss`, `bigKeyOverflow`) only have indirect confirmation
(cross-validation from the third-party document, see section further
below) — to test individually if a full in-game confirmation is needed.

**Instant effect for props without `RewardGourd` (2026-09-07)** — following
an in-game test where the bridge associated with `bigKeyIntro` didn't open
until the zone was reloaded ("assumed trade-off" behavior above), a
question was raised: for a gourd, never pinning live is justified by two
reasons (`GourdState`/map icon desync, keeping the local puzzle
replayable) — neither applies to a big key (no `GourdState` at all, no
repeatable puzzle, per the discovery above). `ItemApplier.TryApplyLiveEffect`
therefore calls `Prop.ServerSetPinned` live, but **only if the target prop
has no `RewardGourd` component** AND the zone is already loaded (otherwise,
unchanged behavior: `Prop.Start()` will take over on the next load).
Validated in-game: the bridge opens immediately, without a reload, for a
big key.

## Open point — the location/item logic deserves rethinking (2026-09-07)

While testing the instant effect above, a question was raised by the
player: does applying an item cancel the possibility of using the same
entry as an Archipelago location? Investigated during the session, and the
problem turned out deeper than expected — noted here as-is, **not
resolved**, just documented to revisit with more perspective.

**The real bug found (fixed)**: `ItemApplier` marked the location as
"already reported" in `CheckTracker` **without ever calling
`Plugin.Reporter.ReportCheck`**. So if a location's item arrived via
Archipelago before the player had resolved that location themselves
in-game, the check was **never reported anywhere** — neither immediately,
nor later (`CheckTracker` blocks any future report for that key).
Concretely: the randomized item that *should* leave this location for
whoever is waiting for it in the multiworld never left. Fix applied:
`ItemApplier` now calls `Plugin.Reporter.ReportCheck` itself if it's the one
marking the location first (same pattern as `GourdStatePatch`/
`SaveValuePatch`: whichever comes first — real resolution or received item
— reports the check once).

(The underlying question — whether `GourdRegistry` should keep using the
same identifier as both location id and item id, a true multiworld
decoupling question — is an apworld design question; see
`apworld/design-decisions.md` for the full discussion and its resolution.)

## Point to test next session: cooperative puzzles without a clamp

Following the discussion above, the player clarified how these "no clamp"
puzzles actually work: it's in fact a **cooperative** mechanic — one player
presses a button elsewhere (which unlocks a crate), the other retrieves the
object inside. A safety net already observed: if the object is thrown far
from its crate, the game automatically puts it back inside.

`gourdTelescopeToBox`/`valetTelescopeToBox` (one of the 45 gourds that
exist in the build) is very
likely the exact example cited by trinity in the Discord thread — so
directly testable with our current tools, no extra code needed. Requires
two players (confirmed by the player). Questions to settle at the next
test:
1. Does this puzzle have a `RewardGourd` component or not (F5 near the
   crate)?
2. At what exact moment does `SaveManager` receive the write for this key —
   when the button is pressed (crate opened) or only when the object is
   retrieved/stored?
3. Does the "reloads into its crate if lost" behavior cause repeated
   `SaveManager` writes, and does it interact with
   `ItemApplier.TryApplyLiveEffect` (live pin for props without
   `RewardGourd`) if an AP item arrives before the crate is opened?

Working hypothesis (unverified): `SaveValuePatch` (the generic safety net,
independent of `RewardGourd`) should catch the check regardless of the exact
physical mechanism — consistent with its documented purpose above. To
confirm.

## Third-party document — "Big Walk Archipelago details.pdf" (2026-09-07): reference mapping table

(See `apworld/design-decisions.md` for the community design content of this
document and the accompanying Discord thread — goal/items/locations model
comparison, the "45 puzzles" historical note, the problem the third party
hit and how this mod's design avoided it, and the later "bells" community
feedback.)

The one piece of this document that is pure mod reference data: the
big-key/tower/plinth cross-validation table, which is literally the
justification/reference data behind `GourdRegistry.BigKeyHomesByProp`.

The document names the 6 towers + the tutorial explicitly, with their own
key-deposit locations ("Key Deposit"). Cross-referencing the document's
section order (`Completion Rewards`) with our `monoumentX`/
`bigKeyPlinthYyy` and the color→plinth mapping the player gave earlier this
session, everything lines up perfectly:

| Tower (real name) | Big key (`SaveablePropName`) | Plinth (`SaveableHomeName`) | Deposit location (doc name) | Deposit boxes (doc) | Real slots (F6) |
|---|---|---|---|---|---|
| Tutorial | `bigKeyIntro` | `bigKeyPlinthIntro` | Drawbridge Key Deposit | 4 | **4** ✓ |
| Red Funnel Tower | `bigKeyRedZone` | `bigKeyPlinthMapRoom` | Map Room Key Deposit | 5 | **5** ✓ |
| Green Cup Tower | `bigKeyGreenZone` | `bigKeyPlinthSkiLift` | Chairlift Station Key Deposit | 5 | **5** ✓ |
| Blue Castle Tower | `bigKeyBlueZone` | `bigKeyPlinthTrain` | Train Station Key Deposit | 5 | **5** ✓ |
| Yellow Twist Tower | `bigKeyYellowZone` | `bigKeyPlinthTunnels` | Underground Tunnel Key Deposit | 5 | **5** ✓ |
| Black Monolith Tower | `bigKeyBoss` | `bigKeyPlinthEnding` | Dam Key Deposit | 6 | **6** ✓ |
| Green Dome Tower | `bigKeyOverflow` | `bigKeyPlinthGoodbye2` | Tutorial Key Deposit (keyhole, "big_game" ending) | 15 (reducible to 6 in-game, `limit_green_dome_deposit_boxes`) | **15** ✓ |

Each "Deposit boxes (doc)" column value matches **exactly** the real counts
found via F6 this session (`monoumentIntro`=4, `monoument0-3`=5 each,
`monoumentFinal`=6, `monoumentOverflow`=15) — so `monoument0/1/2/3` = the 4
"5-slot" towers (Red Funnel/Green Cup/Blue Castle/Yellow Twist, exact order
among them not yet determined), `monoumentFinal` = Black Monolith Tower,
`monoumentOverflow` = Green Dome Tower. Also independently confirms (so with
much higher confidence now) the color→plinth mapping given by the player:
Red→MapRoom, Green→SkiLift, Blue→Train, Yellow→Tunnels, Boss→Ending,
Overflow→Goodbye2, already coded in `GourdRegistry.BigKeyHomesByProp`.

## Gourds "variant challenge" (purple/postgame) — automatic map reveal (implemented on 2026-09-09)

**Investigated and resolved.** User request: purple gourds
(`RewardGourd.isVariantChallenge`, normally unlocked after a first game
ending) must be made visible on the map from the start, for an Archipelago
world where they're accessible from the beginning.

**Mechanism discovered via Ghidra decompilation**
(`GourdMap.Initialize`/`Start`/`RevealHiddenGourds`, `RewardGourd.Awake` —
see script `BW_export/decompile_variant_gourds.py`, output in
`BW_export/variant_gourds_decompiled.txt`):
- `GourdMap.Initialize()` (called once per `GourdMap` instance, so on every
  zone load) instantiates each gourd's map icon (`flagPrefab` or
  `flagPrefabVariantChallenge` depending on `isVariantChallenge`) and
  **systematically** forces `GourdFlag.SetState(Hidden)` for every gourd
  with `isVariantChallenge == true` — a purely local/session state,
  **never read from or written to `SaveManager`**.
- `GourdMap.RevealHiddenGourds(PeckContext)` (private method, triggered in
  real gameplay via `GourdMap.revealSystem`, a `PeckSystemReference`,
  probably tied to an end-of-game event) does the opposite: for every
  currently-`Hidden` `GourdFlag`, `SetState(Locked)` — reveals the icon on
  the map.

**Solution adopted**: rather than touching the private method, the same
result is obtained via the **public** static event `GourdMap.refreshFlag`
(`Action<SaveablePropName, GourdFlag.GourdState>`, already used by the game
to refresh the icon live):
```csharp
GourdMap.refreshFlag.Invoke(prop.saveablePropName, GourdFlag.GourdState.Locked);
```
called for every `RewardGourd.isVariantChallenge == true` found via
`FindObjectsByType<RewardGourd>()`.

**Difference from the Arch doors**: since this state is never persisted and
is reset to `Hidden` on **every new `GourdMap` instance** (so on every zone
load, not once per save), the automation cannot be a "done once per save"
flag — `Core/VariantGourdMapUnlocker.cs` (a `MonoBehaviour` added
unconditionally, cf. `Plugin.Load()`) polls every 2s
(`WorldManager.isReadyForEffects` as a guard) and continuously recalls
`Core/VariantGourdRevealer.RevealAll()`. No need for a
`NetworkServer.active` guard: this is a purely local/client map-display
state, not networked, not persisted.

**SERIOUS BUG FOUND AND FIXED (2026-09-09) — total game freeze tied to
directly invoking a static IL2CPP delegate**:

The very first implementation directly called
`GourdMap.refreshFlag.Invoke(propName, GourdState.Locked)` (the static
`Action<SaveablePropName, GourdFlag.GourdState>` event the game uses
internally to refresh the icon live). Two complete game freezes occurred in
the same session (no exception, no log, process "Responding" on the OS side
but frozen on screen, confirmed by the Windows "not responding" dialog) —
in both cases, several dozen seconds *after* revealing then resolving a
purple gourd, on the next alt-tab.

**A/B isolation testing in-game** (the user confirmed "this problem didn't
happen before"):
- Normal (non-purple) gourd resolved + alt-tab → no freeze.
- Fully disabling `VariantGourdMapUnlocker` (the polling) → freeze still
  reproduced on a purple gourd revealed via the `Home` hotkey (which calls
  the same `Invoke()` logic) then resolved.
- Purple gourd discovered/resolved **without ever calling
  `refreshFlag.Invoke()`** (never pressing `Home`) + alt-tab → no freeze.

Conclusion: neither the polling itself, nor purple gourds as such, are the
problem — it's specifically the `Invoke()` call on the static IL2CPP
delegate that corrupts some internal state (probably on the IL2CPP interop
side) which only manifests on focus return (alt-tab), with no usable trace
in any log.

**Fix**: `VariantGourdRevealer.RevealAll()` no longer touches
`GourdMap.refreshFlag` at all. It directly finds the relevant `GourdFlag`
instance (`FindObjectsByType<GourdFlag>()`, filtered on
`gourdState == Hidden` and cross-referenced with the found
`RewardGourd.isVariantChallenge`) and calls `GourdFlag.SetState(Locked)`
**directly on it** — a normal C# method call on a component, not a static
delegate invocation. This is exactly what the private method
`GourdMap.RefreshFlag` does internally (it too calls `GourdFlag.SetState`
directly, never via the delegate), so just as safe game-side. Revalidated
in-game: no more freezes across several reveal → resolve → alt-tab cycles.

**General lesson for the rest of the mod**: never call `.Invoke()` on a
static IL2CPP delegate field of the game (even a public one) from external
managed code — always prefer a direct call to the underlying instance
method when accessible, even if that requires finding the relevant instance
yourself rather than going through the event.

**Validated in-game (final behavior)**:
- Purple gourd icons clearly visible on the map from the start, with no
  manual action (the `Home` hotkey kept for manual triggering via
  `Debug/DebugVariantGourdReveal.cs`).
- The physical prop itself is a perfectly normal `RewardGourd`
  (`gourdState=Locked`, `active=True`, no hidden second lock) — confirmed
  via F5 (`DebugGourdLookup.LogNearby`) at 3m from a purple gourd.
- Check detected and reported normally after physical resolution
  (`[Check] gourdCharadesRooms`/`[Check] gourdSpeedObby` observed in-game,
  `GourdStatePatch`/`SaveValuePatch`/`CheckTracker` pipeline unchanged).
- Once a purple gourd is resolved, its icon disappears from the map like
  any classic gourd — normal behavior, not a bug.
- No freeze reproduced after the fix, across several test cycles with
  alt-tab.

## Arch doors / hub shortcuts — automatic opening on a save's first launch (implemented on 2026-09-09)

**Investigated and resolved.** Internal name: not "Arch" at all — the real
component is called `HubGate` (`HubGate_Plinth`, `HubGate_Gate`,
`HubGate_DoorPieceL/R1-3`, `HubGate_DoorBlocker_Left/Right`,
`GateMainSystem`). User decision: these shortcuts have no value staying
closed for an Archipelago world (unlike gourds/big keys, which are the real
randomized content) — so they're opened automatically from the very first
session on a save.

**Mechanism discovered via Ghidra decompilation** (`TrackedPeckState.
SetState`, `PeckDevHelper.Trigger`, `PeckRelay`, `PeckSwitch` — see script
`BW_export/decompile_peck_chain.py`, output in
`BW_export/peck_chain_decompiled.txt`):
- `TrackedPeckState.SetState(PeckContext)` writes
  `SaveManager.SetIntValue(key, compressedState)`, where `key` =
  `saveIdentity.saveGuid` if `savableSystem == NotSavable`, otherwise
  **`Enum.ToString(savableSystem)`** (the `SavableSystem` enum's name
  itself, literally, not a per-instance GUID).
- `PeckDevHelper.Trigger(UnlockRules)` (a dev cheat already built into the
  game, probably for House House's internal testing): iterates every loaded
  `PeckDevHelper`, and for each whose rules match (either
  `unlockRules.Matches(rules)`, or the simplified fields
  `fireWithUnlocks`/`fireWithLights`/`fireWithTrain`), calls
  `PeckSwitch.Peck()` on the `PeckSwitch` attached to the same GameObject —
  which reads `trackedStateSystem` (a reference assigned in the Unity
  editor, **not necessarily the visually closest object**) and calls
  `SetState` on it.

**Pitfall encountered**: `Trigger(new UnlockRules { unlocks = true })` does
open the Arch doors, but broadcasts to the *entire* "unlocks" category —
confirmed by directly inspecting several `.sav` files (`grep -o
'"key":"[^"]*","value":[0-9-]*'` on the JSON): also persists `EndingGate=1`
and the 7 `FmStation*`/4 `LookoutLight*` at `1`, in addition to the 3
intended hub keys. The "it closes again after reload" observed on the first
visual test was misleading — actually only part of the effect is purely
visual/transient (the switches without a "hub" category), the rest (the 3
hub keys + incidentally Ending/FmStations/LookoutLights) is indeed
persisted.

**Solution adopted**: direct write of the `SaveManager` keys, without ever
calling `PeckDevHelper.Trigger` or touching a live object — same philosophy
as `ItemApplier` for gourds/big keys. Confirmed by A/B testing on several
fresh saves that went only through the automatic hook (no manual F11
mixed in): `Trigger(unlocks:true)` writes together exactly `SpawnHubGate`,
`HubTunnel`, `HubShortcutToSportsCreek` (never `EndingGate`/`FmStation*`/
`LookoutLight*` in this specific subset — these 3 are the real "hub
shortcuts" category). `Core/ArchDoorUnlocker.cs` therefore writes directly:
```csharp
SaveManager.SetIntValue("SpawnHubGate", 1);
SaveManager.SetIntValue("HubTunnel", 1);
SaveManager.SetIntValue("HubShortcutToSportsCreek", 1);
```
**Confirmed behavior in-game**: unlike `Trigger()` (immediate visual effect
via the real Peck mechanic), a raw `SaveManager` write alone opens nothing
live in the current session — a full reload is needed for load-time
restoration (mechanism not precisely identified, probably the
`TrackedPeckState` equivalent of `Prop.Start()`) to re-read the value and
actually open the door. Validated: after a full restart, the 3 hub
shortcuts are open, no other key (FmStations/LookoutLights) was touched.

**RESOLVED for the live effect (2026-09-09)**: `Core/ArchDoorUnlocker.cs`
now looks, for each of the 3 keys, for the actually-loaded corresponding
`TrackedPeckState` in the scene (`FindObjectsByType<TrackedPeckState>()`,
filtered on `savableSystem == key`) and calls `SetState(1)` directly on it
(same `SaveManager` write internally, confirmed by Ghidra, but also
triggers the visual/animation callbacks — without `Trigger()`'s broad
broadcast). If the object isn't loaded in the current zone, falls back to
the raw `SaveManager` write alone (same philosophy as
`ItemApplier.TryApplyLiveEffect` for gourds/big keys): load-time
restoration will take over on the next reload. Validated in-game: the 3
shortcuts open immediately, in the very session where the save is created,
without a reload.

**Hook timing**: `WorldManager.OnWorldManagerStart` (static event) and
`WorldManager.instance.onLocalPlayerCharcterStart` both fire **too early**
(before the game's "peck manager" exists — confirmed in-game by a burst of
`no peck manager instance. this is maybe too early` logs right after
firing, well before `"[id] World Started"`/`"world manager started"`). No
impact for a raw `SaveManager` write (it doesn't need the peck manager),
but the final component still uses `WorldManager.isReadyForEffects` (a
static game property, designed to signal "safe to trigger effects") via
polling in `Update()`, out of caution/consistency.

**Debug tools added along the way** (`Debug/DebugComponentLookup.cs`,
`Debug/DebugPeckDevHelper.cs`, `Debug/DebugTrackedPeckStateLookup.cs`,
`Debug/DebugPeckSwitchTarget.cs`, F9-F12/Insert/Delete keys): useful for
any future investigation of a scene mechanism with no C# class findable by
name — an approach that worked here after several false starts (keyword
search on nearby components, then inspecting `PeckDevHelper.
unlockRules`/`fireWith*` fields, then `TrackedPeckState.savableSystem`/
`saveIdentity`, then `PeckSwitch.trackedStateSystem` — the Unity reference
assigned in the editor, not deducible by proximity).

## The radio — how a station is unlocked (decompiled 2026-09-21)

Written before touching the game, for once, and it paid: the question this
started from ("does writing `FmStation*` back to 0 stop the music?") has a flat
answer of **no**, and one decompilation session was cheaper than the full test
cycle that would have found that out.

The cast:

| Class | Role |
|---|---|
| `BroadcastStation` | The tower. Holds `musicGroup`, a `peckSystemReference`, and the tower's own radio prop. |
| `FmRadioManager` | Scene singleton. `stationTrackGroups` (the dial), `_stationStates`, `_stationUnlockTimes`, the static `onChange`/`onUnlock` actions. |
| `FmRadioPlayer` | The portable radio: tuning (`OnPeck`), static, and `OnUnlock(int stationIndex)`. |
| `TrackedPeckState` | The generic "peck state that persists". Its `savableSystem` field is what writes `FmStationXxx` to `SaveManager`. |

`BroadcastStation.Awake` subscribes `Unlock` to `peckSystemReference` and
`OnSpawn` to the radio prop; `OnSpawn` finds this station's index in
`stationTrackGroups` and tunes the tower's radio to it.

Three findings, each of which changed the design:

1. **`BroadcastStation.Unlock(PeckContext)` is an inlined copy of
   `FmRadioManager.Unlock(MusicGroup)`** — the compiler inlined it, so it does
   not call it. Both walk `stationTrackGroups` for the matching `MusicGroup`,
   set `_stationStates[i] = 1` and `_stationUnlockTimes[i] = NetworkTime.time`,
   then fire `onChange` and `onUnlock(i)`. Patching `FmRadioManager.Unlock`
   would therefore suppress nothing. The mirror image is useful: the mod can
   call `FmRadioManager.Unlock` to grant a station without re-entering its own
   patch on `BroadcastStation.Unlock`.

2. **Neither writes to the save**, and `FmRadioManager.Initialize` only
   allocates the two arrays — it reads nothing back. The persistence is one
   level up: the station's `TrackedPeckState` writes `FmStationXxx` through
   `SaveManager.SetIntValue`, which is exactly where `SaveValuePatch` already
   sees the check. A station comes back after a restart because that peck state
   re-asserts its saved value on load and re-fires its effects.

3. **There is no relock.** `_stationStates[i]` is only ever set to true.
   Nothing anywhere turns a station off, which is why writing the save key back
   to 0 does nothing at all and why suppression has to happen *before* the
   unlock — a Harmony prefix on `BroadcastStation.Unlock` returning false.

Consequence worth keeping in mind for anything else built on this manager: the
unlock state lives in RAM only. It does not survive a world reload, and a world
reload does not need the process to restart.

`FmStation7/8/9` do exist in `SavableSystem` (37–39). Nothing suggests they are
wired to anything, and the apworld leaves them out.

Addresses (Steam build of 2026-09-07, `mod/gh-pj/`; the game played is 1.48, so
treat them as indicative and confirm members against the interop assembly):

```
FmRadioManager.Unlock         0x000000018046EA00
FmRadioManager.Initialize     0x000000018046E660
FmRadioManager.GetUnlockState 0x000000018046E7A0
BroadcastStation.Awake        0x000000018046E040
BroadcastStation.OnSpawn      0x000000018046E270
BroadcastStation.Unlock       0x000000018046E370
```

**Settled in game on 2026-09-21 (Ctrl+B): `stationTrackGroups` is NOT ordered
like the `FmStation*` enum**, and the names do not match either.

| dial | MusicGroup | station that owns it |
|---|---|---|
| 0 | `musicGroup_bobby` | FmStationBreathwork |
| 1 | `musicGroup_FourthSpace` | FmStationSleuthFm |
| 2 | `musicGroup_breathwork` | FmStationFourthSpace |
| 3 | `musicGroup_JourneyBeat` | FmStationJourneyBeat |
| 4 | `musicGroup_DanceFM` | FmStationDanceFm |
| 5 | `musicGroup_Mallets` | FmStationAFJ |
| 6 | `musicGroup_Bristol` | FmStationKosmische |

One of the seven lines up. The enum names are internal labels that do not
describe the music the station plays, and the game shows no station names to
the player at all — the dial displays numbers. The index fallback was removed
and replaced by a dial position learned from the world and kept per save.

All seven `BroadcastStation` instances were loaded simultaneously in that
session, so the authoritative path may in practice always be available; the
learned cache exists because "may in practice" is not a guarantee.

**`FmRadioManager.instance` throws rather than returning null** when there is
no instance — an `Il2CppException` wrapping a `NullReferenceException` from
`get_instance`. It crossed the IL2CPP-to-managed trampoline out of
`ApRuntime.Update()` and aborted the rest of `OnJustConnected`. Every access
now goes through `RadioStations.TryGetManager`. Assume the same of any other
IL2CPP singleton property until proven otherwise.

## The big keys — cutting, and what actually opens a door (decompiled 2026-09-21)

Investigated the same way as the radio, and it changed the design twice.

`KeyBlank : NetworkBehaviour` (dump l.212816) is the cutting mechanism:

```
SyncList<bool> cuts;              // one entry per segment to cut
KeyBlankCover[] covers;           // the visual husk, per station
Prop prop;                        // the key itself -> saveablePropName
PropGroup finishedPropGroup;
bool startFinished;               // debug
void OnBite(int stationIndex);    // cosmetic: advances that cover's stage
void ServerCutSegment(int index); // the cut, server-side
void RefreshPropGroup();
```

- **`RefreshPropGroup` is the single gate.** Its body looks for a `false` in
  `cuts` and **returns if it finds one**; only when every segment is cut does
  it add `finishedPropGroup` to `prop.propGroups`. That list is what a
  `PropHome` matches against its own `pinGroup`, so an unfinished key cannot
  be placed.
- **`PropGroup` backs this up**: `BigKey = 32` vs `BigKeyComplete = 36`, and
  `BigKeyEnding = 35` vs `BigKeyEndingComplete = 37`. `BigKeyOverflow = 38`
  has no Complete variant, which suggests the Green Dome key is not cut at
  all.
- **`cuts` is not persisted.** No `SaveManager` call anywhere in `KeyBlank`,
  `OnStartClient` included — it is pure Mirror runtime state. A key cut but
  not placed comes back blank after a reload. Vanilla behaviour, and harmless
  for checks, which `CheckTracker` keeps to once per save.
- ~~**`ServerCutSegment(int)` is the check hook**~~ — **it is not, and it
  cannot be (2026-09-21).** It has **no code address** in the il2cpp export,
  while `OnBite`, `OnPinUpdated`, `OnCutsUpdated`, `RefreshPropGroup` and
  `OnStartClient` all have one and the 32 bytes between the two nearest are
  padding, not a body. It was inlined into its caller
  (`UnlockTrailStation.OnCutPeck`, which does have one). A Harmony patch on
  it would have bound to something nothing calls and reported nothing, in
  silence — the same trap `BroadcastStation.Unlock` set for the radio. The
  hook used instead is **`OnCutsUpdated(SyncList<bool>.Operation, int, bool,
  bool)`**: it has a body, it is the game's own notification for this exact
  change, and `__instance.prop.saveablePropName` still names the tower while
  the index names the segment. Report on `false -> true` only — `cuts` is
  sized at runtime, so a world load appends its entries one at a time and the
  callback fires once per append.

### What opens a door, and why there is no feature flag

`SavableSystem` has **no entry** for the map room, the chairlift, the train or
the tunnels. Their persistence *is* "the key is in its plinth"
(`SaveManager[bigKeyRedZone] = bigKeyPlinthMapRoom`, restored by
`Prop.Start()` on the next load). So granting a feature without a key means
the mod keeps its own ledger and re-applies on every world — the shape
`Core/RadioStations.cs` already has.

*Superseded — see "what DOES open them" below.* The two candidates this
paragraph proposed, `PropHome.pinDirectControlSystem` and
`PropHomeBlock.isFullDirectControlSystem`, are both on the **plinth**, and
that is precisely why neither is the answer: the feature hangs off the key,
in `Prop.taggedPinSystems`. The conclusion above about a mod-side ledger
stands, and for the reason given — no `SavableSystem` entry exists for these
features, so a granted one must be re-applied to every world that loads.

**Do not reach for `PeckDevHelper.Trigger(UnlockRules{map:true})`.** The
rules struct does carry `unlocks`/`lights`/`chairlift`/`train`/`bell`/
`tunnel`/`map`/`gourd`, which map cleanly onto the four coloured towers — but
`Trigger` broadcasts to a whole category. `ArchDoorUnlocker` documents
`Trigger(unlocks)` also writing `EndingGate`, the seven `FmStation*` and the
four `LookoutLight*`, confirmed across several save files. Target the
`TrackedPeckState` and call `SetState(1)`, as `ArchDoorUnlocker` does.

Addresses (Steam build of 2026-09-07, indicative — confirm against the
interop assembly):

```
KeyBlank.OnBite            0x000000018048FD50
KeyBlank.OnPinUpdated      0x000000018048FE20
KeyBlank.OnCutsUpdated     0x000000018048FF40
KeyBlank.RefreshPropGroup  0x000000018048FFC0
KeyBlank.OnStartClient     0x000000018048F660
```

**Measured in game on 2026-09-21 (Ctrl+K).** Nine `KeyBlank` instances, all
loaded at once — no streaming to work around.

| key | segments | finishedPropGroup |
|---|---|---|
| `bigKeyIntro` (drawbridge) | **5** | BigKeyComplete |
| `bigKeyRedZone` / `Green` / `Blue` / `Yellow` | **5** each | BigKeyComplete |
| `bigKeyBoss` (Black Monolith) | **none**, `covers=0`, born finished | BigKeyEndingComplete |
| `bigKeyOverflow` (Green Dome) | **none**, born finished | BigKeyOverflow |

So exactly **25 cut steps**, on the drawbridge and the four coloured towers.
The `BigKeyEndingComplete` group exists but the ending key starts with it
already in `propGroups`, so it is not cut.

**Every plinth's `pinGroup` is the Complete variant** (`BigKeyComplete` for
the intro and the four colours, `BigKeyEndingComplete` for the ending,
`BigKeyOverflow` for the Green Dome), and an uncut key carries
`[BigKey, GoesInBackpack, PoseLimitBig]`. Skipping `RefreshPropGroup` is
therefore suppression enough: the socket simply does not accept the key.

**No `PropHomeBlock` watches a big-key plinth** — all seven reported none. The
monument blocks are elsewhere. What each plinth does have is a
`PropHome.pinDirectControlSystem`: non-null, but with an empty label and
`savableSystem = NotSavable`, which confirms a mod-side ledger would be needed
to persist a feature unlock.

**A trap for whoever implements this**: on a brand-new save `bigKeyIntro`
already reads `[XXXXX] 5 cut` with `BigKeyComplete` in `propGroups`. That is
`ItemApplier.ApplyBigKeyItem` pinning the precollected Tutorial Key —
**pinning a key marks its blank complete**. Hook the cut checks naively and
the drawbridge's five would all fire at connection time.

## The objects lying around the island (inventoried 2026-09-21)

For the question "which of these could be an Archipelago item instead of the
inert Postcard and friends". It had to be an in-game dump (`Debug.DumpPropsKey`,
Ctrl+J): `il2cpp.cs` has **no class** for any of them — no FlareGun, no
WalkieTalkie. They are plain `Prop` prefabs told apart by their prefab and by
what their `useHeldSwitch` is wired to. `FlareDriver` is a lens-flare renderer
and has nothing to do with flares.

540 props loaded at the hub, 111 distinct names. The ones worth something:

| Prop | Count | Usable held | Note |
|---|---|---|---|
| `FlareGunProp` + `…Blue` / `…Green` / `…Yellow` | 1 each | yes | four distinct flare guns |
| `WalkieTalkieProp` | 8 | yes | the only gadget the code names, via `Prop.radioVoiceAssigner` |
| `BinocularsProp` | 7 | yes | |
| `XrayGogglesProp` | 7 | yes | |
| `LaserProp` | 7 | yes | |
| `TorchProp` | 9 | yes | |
| `MegaphoneProp` | 3 | yes | |
| `CowBellLarge/Medium/SmallProp` | 1 each | yes | three sizes |
| `FoldingMapProp`, `CompassProp`, `CoordinateTrackerProp`, `FootyProp` | 1 each | yes | |
| `BackpackProp`, `HolsterProp` | 6, 5 | no | carrying gear |
| `BlindfoldProp`, `ClockProp`, `KettleProp`, `Pomodoro`, `CueCard` | few | no | |

`PegTileProp*`, `BuoyProp`, `BuoyLight`, `PermoSignProp` and `foldingChair` are
puzzle furniture and scenery, not objects, despite carrying guids.

**Every one of them carries a `savablePropGuid`**, which is the game's own
per-prop identity — used with `SaveManager.GetIsInInventory(guid)` and
`SaveData.inventory`. That was the open question and the answer is the good
one: these props can be *locations* as well as items, because a guid tells one
apart from its siblings across sessions. None has a `SaveablePropName`, so the
puzzle machinery does not apply to them at all.

## The big-key doors — what does NOT open them (2026-09-21)

Measured with Ctrl+K, and all three candidates died:

- **No `PropHomeBlock` watches a big-key plinth.** All seven report none.
- **No plinth fires a `PeckSwitch` on pin.** All seven report
  `onPin <none> | onUnpin <none>`.
- **No switch in the game wants a big key.** Of 2833 `PeckSwitch` instances
  loaded, 49 carry `needsKey`; 48 are `SalonBrush` and one is `StickyCurse`.
  Not one is keyed on `BigKey`, `BigKeyComplete` or any variant.

All three were eliminated for the same reason, and it is worth naming because
it is what cost the time: **they were all looking at the socket.**

## The big-key doors — what DOES open them (2026-09-21, ANSWERED)

**The wiring is on the plug, not on the socket.**
`Prop.SetPinDirectControlSystem(PropHome home, bool pinned)` — decompiled
(`0x1803C6B10`), not inferred — drives three `TrackedPeckState`s in order:

```
1. home.pinDirectControlSystem        // the plinth is occupied
2. this.pinDirectControlSystem        // the prop is pinned
3. foreach (pair in this.taggedPinSystems)
       if (pair.propGroup == home.pinGroup)
           pair.peckSystem.SetState(context)   // THE FEATURE
```

```
Prop.pinDirectControlSystem   TrackedPeckState            // 0x268
Prop.taggedPinSystems         PropGroupPeckSystemPair[]   // 0x270

struct PropGroupPeckSystemPair { PropGroup propGroup; TrackedPeckState peckSystem; }
```

That third loop is the door, and it explains every negative result above at
once: nothing needs to hang off the plinth, because the key carries its own
table of "which state do I drive when placed into a home of group X". It
also explains why the plinth's `pinDirectControlSystem` is a dead end — empty
label, `NotSavable`, identical on all seven — it is the generic "something is
in me" state, not the feature.

**Implications, both directions:**

- **Granting** a feature without a key is `SetState(1)` on that pair's
  `peckSystem`, found from the key prop and the plinth's `pinGroup`. Same call
  `ArchDoorUnlocker` already makes for the hub shortcuts.
- **Suppressing** it is emptying `taggedPinSystems` around the call and
  restoring it after, which leaves (1) and (2) running so the key still
  visibly sits in its socket. **Empty, not null**: the loop has no null guard
  and the disassembly's null path jumps straight to a throw.
- **Co-op needs nothing extra.** Peck state is networked and
  `SetPinDirectControlSystem` is server-side already, so the host grants and
  the guest sees it. This is strictly better than the radio, whose
  `FmRadioManager` is local to each machine.

See `Core/KeyFeatures.cs`, `Patches/PropTaggedPinPatch.cs` and
`../apworld/protocol.md` §12.

### Two leads that died on the way, recorded so nobody walks them again

- **`KeyDependantPeckSwitch`** (`station` / `propHome` / `onPlaceBlank` /
  `onPlaceCut`) reads exactly like the answer, and is not one: its `Awake`
  and `OnPin` have no code address in the export while `UnlockTrailStation`'s
  `Awake` — its immediate neighbour — has one. Unused in any shipped scene.
- **`PropHome.onPinServer` / `onChangeServer`**, the lead this document
  recommended following first. They are real, but they are not where the
  feature hangs, and enumerating a delegate's subscribers in IL2CPP would
  have been a great deal of work to arrive at the same place. Reading the one
  function that fires on a pin was twenty minutes.

## What holds a big key back (measured 2026-09-21)

For the question "how can Archipelago hand a player a key", once the keys
became items rather than a reward for filling a monument.

**One bool, on the home the key sits in.** All seven report
`blockGrabbing = true` — six of them in a `PropHome` called `KeyStoneHome`,
and `bigKeyIntro` in a `BigKeyHome`. The game's own release is a
`PeckEffectPropHomeSettings`:

```
PeckEffectPropHomeSettings
    PropHome        propHome;          // or propHomeBlock
    PeckSystemReference systemReference;
    PropHomeSetting[] settingsPerState;

struct PropHomeSetting { bool blockPlacingMask, blockPlacingValue,
                              blockGrabbingMask, blockGrabbingValue; }
```

For `bigKeyOverflow` the dump named the whole mechanism in one line: the
effect is `KeyStoneGrabbableState`, driven from a TrackedPeckState whose
label IS its documentation — `0 - locked, 1 - animating, 2 - grabbable`, on
`KeyScrewLogic` — with `settingsPerState[2]` writing
`blockGrabbing := false`.

The other six matched nothing, by `propHome` or by `propHomeBlock`. Most
likely streaming: the dump was taken from the tutorial and those towers are
300m to 900m away. **It does not matter**, and that is the point of the
approach chosen: the mod re-asserts the bool on a poll rather than
intercepting whatever flips it. Which is just as well, because
`PeckEffectPropHomeSettings.Apply` and `OnPeck` have **no code address** in
the export while `Awake` does — inlined, so a patch would bind to nothing,
exactly like `ServerCutSegment`.

`Prop.ServerSetUnpinned()` is in the same category — named in the 1.48
interop assembly, no address in the export — and was therefore called under
a guard with a playable fallback. **It works**, verified in play: the key
unpinned, moved to the spawn point, settled under gravity 1.3m lower and
stayed there, `activeInHierarchy` and `isVisible` both true. One earlier
attempt had it vanish on the spot and that remains unexplained — nothing
about the delivery changed between the two runs, only the diagnostic
around it.

## Two game APIs that THROW instead of returning null

Both found the same way, both having crossed the IL2CPP-to-managed
trampoline and killed the rest of a method on the way out:

- **`FmRadioManager.instance`** (2026-09-21) — took `ResendKnownChecks` with
  it out of `OnJustConnected`.
- **`PropHome.GetSaveableHome(name)`** (2026-09-21) — raises a
  `NullReferenceException` when no world is loaded, which is the NORMAL
  state at connection time, not an edge case. Everything in the mod now
  goes through `GourdRegistry.TryGetHome`.

Worth assuming of any `static` accessor in this game until proven
otherwise: a scene singleton reached before its scene exists is a throw,
not a null.

## Reference files

- `il2cpp.cs` ("C# prototypes" export): full structure of every class in
  the game, useful for quick grep of new class/enum names without going
  back through Ghidra.
- Local Ghidra project (not provided here, on the Windows machine): contains
  the full decompilations, to consult for any function not covered by this
  document.
