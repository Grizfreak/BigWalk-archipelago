# Co-op tests — what has to hold before the apworld alpha ships

*Written 2026-09-21, at the end of the session that built the big-key feature.
Everything below was built and exercised **solo**. Not one line of it has run
with a second player, and this file exists because that is the largest
remaining unknown in the project — not because anything is known to be broken.*

Read `NEXT-SESSION.md` first for what the feature is. This file is only about
the second machine.

---

## Why co-op is a separate question at all

Three facts shape every test here, and all three are already established:

- **Only the host runs an Archipelago client.** Big Walk's save data belongs to
  the host — every write goes through a Mirror `[Server]` method — so the host
  is the only process that can honestly report a check or apply an item.
- **Peck state IS networked.** That is why the doors were expected to work in
  co-op with nothing extra, unlike the radio, whose `FmRadioManager` is local to
  each machine and gives a guest music the host is still waiting for.
- **A Prop's physics is NOT networked.** Established in co-op on 2026-09-16: a
  cosmetic gourd hung in mid-air on the second player's screen, ignoring gravity
  and refusing to be picked up, until that client called `Prop.SetLoose()` for
  its own copy. *"The Prop's physics is local to each machine and no part of it
  travels over the wire."*

The third fact is the one the new work has not reckoned with.

---

## Setup, and the part that is not optional

**One PC is enough for most of this (since 2026-09-26): the loopback
guest.** With `Debug.Enabled` on, host a world as usual through Steam and
press **Ctrl+L**: a second instance of the same install starts windowed and
joins as a real Mirror client over Kcp on 127.0.0.1 — every replication bug
a real guest meets is there to be met. `tools/launch-guest.ps1` does the same
from a terminal. Then:

- **Ctrl+T**, in either window, hands the keyboard to the other one;
- **Ctrl+C**, on the host, teleports the guest beside you;
- **Ctrl+N** dumps either side's network setup, connections and input;
- **Ctrl+L** in the guest joins again, if the host was not hosting yet.

The guest's log is `BepInEx/LogOutput.1.log`, its Unity log
`Player-guest.log`. Both instances run the same install, so there is no
fingerprint to compare. How it works, and why a second instance used not to,
is in `NEXT-SESSION.md`, "Two instances on one PC".

**What still takes two machines:** real latency; anything specific to
EpicTransport (packet size, fragmentation, relays), since the loopback guest
talks Kcp; joining through an EOS lobby or a Steam friend, which it skips;
two genuinely different accounts; and a last pass before a release.

**With two machines, run `tools/deploy-mod.ps1` and check the hash matches on
both.** It prints one fingerprint and says whether the installs agree. Three
co-op tests were wasted in one weekend on a guest running older code; the
script exists because of it.

**Hosting a test room: pick a port nobody holds.** `tools/testroom.py`
refuses a port that is already taken, since another room on it (another
project's, say) would be the one the game connects to.

**The host must be the one hosting the Big Walk session AND the one with the
Archipelago details filled in.** The slot name is the save name.

**Use a slot name of its own** — `tools/players/coop-test.yaml` exists for this.
`ap_reported_*` is not scoped to a seed, so a save that has already reported a
check will never report it again, and a test written against it silently does
nothing. That is not a bug to report; it is the documented model.

**Read `BepInEx/LogOutput.log` on the HOST** for everything a check touches, and
watch the GUEST's screen for everything visual. Most of the tests below are
exactly a disagreement between those two.

---

## Four predictions that are reasoned, not measured

State these up front so that a failure reads as a confirmed prediction rather
than a surprise. **Two of the four are expected to fail.**

| # | Prediction | Basis |
|---|---|---|
| A | A granted door opens on the guest's screen too | Peck state is networked and `SetPinDirectControlSystem` is server-side. Reasoned. |
| B | **The guest can pick up a key the mod has locked** | `PropHome.blockGrabbing` is a plain `bool` with no `[SyncVar]`. The mod writes it host-side only. **Expected to fail.** |
| C | **A key delivered to the hub stays at its tower on the guest's screen** | `Prop.SetLoose()` and the transform move are local. Exactly the 2026-09-16 mid-air gourd. **Expected to fail.** |
| D | The guest sees the keys uncoloured | `MaterialPropertyBlock` is a local rendering call. Almost certain, and probably acceptable. |

If B and C hold up instead, that is a real result and worth recording as
loudly as a failure.

---

## The tests, hardest first

### 1. A guest cuts a segment — does the check fire? (**the critical one**)

Guest carries a big key to a cutting trail and cuts **one** segment. Host reads
the log.

- **Pass:** `[KeyBlankCutPatch] Segment N cut out of bigKeyXxx` on the HOST.
- **Fail:** nothing on the host, and the guest sees the cut happen.

**Why this is first.** The hook is `KeyBlank.OnCutsUpdated`, guarded by
`NetworkServer.active`, and `cuts` is a Mirror `SyncList`. If the cut is
performed client-side and never reaches the server, the check is **lost
forever** — `CheckTracker` is one-way, so a second attempt at the same segment
reports nothing either. That is a silently unwinnable seed whenever a key is
cut by the guest, which in a co-op game is half the time.

If it fails, do not work around it in the patch: find out where the cut is
actually performed. `KeyBlank.ServerCutSegment` is inlined and unpatchable (no
code address); its caller `UnlockTrailStation.OnCutPeck` has one.

**Then cut the remaining four from the guest** and confirm all five arrive.

### 2. A guest places a key in its plinth

- **Pass on the host log:** `[PropPinDoorPatch] bigKeyXxx placed in
  bigKeyPlinthYyy: its door was held back`, and the deposit check reported.
- **Pass on both screens:** the door does **not** open.
- **Fail:** the door opens on the guest's screen only. That would mean client
  prediction runs the effect locally, and the suppression needs a counterpart on
  the guest.

### 3. A door granted by an item, seen from the guest (prediction A)

Host receives a feature item — `Ctrl+F` simulates it through the real path,
writing the ledger, unlike `Ctrl+D` which drives the state directly and
bypasses it. Guest stands where they can see that door.

- **Pass:** it opens on both screens.
- **Fail:** open on the host, shut for the guest — and then the door is a
  second radio wrinkle, to be documented in the option text rather than fixed,
  or driven per-machine.

**Then reload the world with the guest still connected** and check it reopens
on both. Nothing in the game persists these; it is the mod's ledger every time.

### 4. A key the slot does not own (prediction B)

Guest walks to a tower whose key has NOT been granted and tries to take it.

- **Expected fail:** they can pick it up. `blockGrabbing` is host-local.
- **Why it matters:** the guest could then cut and place a key the slot was
  never given, firing six checks out of logical order. Not unwinnable —
  Archipelago is happy to receive a check early — but it breaks the premise that
  a key is progression.
- **If it fails:** the lock has to be re-asserted on every machine, which means
  `KeyCustody` needs a client half, or the key needs hiding rather than locking.

### 5. A key delivered to the spawn point (prediction C)

Host receives a key item (`Ctrl+G`). Guest watches the spawn point, then the
tower.

- **Expected fail:** the guest sees it still at its tower, or hanging in mid-air
  where it was.
- **If it fails:** the fix has a precedent — `CosmeticGourdSpawnHandler` already
  makes each client call `SetLoose()` on its own copy, deferred by a frame so
  Mirror's spawn payload does not overwrite it. The same shape applies.
- **Also check:** once the guest sees it wherever it is, can they pick it up and
  carry it? A key nobody but the host can touch is worse than one in the wrong
  place.

### 6. `Ctrl+R` with a guest holding a key

Host presses `Ctrl+R` while the guest is carrying a granted key.

- **Pass:** it leaves their hands and returns to the spawn point, and a key
  already in its plinth is left alone.
- The gourd equivalent is known to work from either side (2026-09-20), and
  `StaleHeldPropReleaser` exists precisely because a host cannot empty a remote
  player's hands.

### 7. The guest leaves and rejoins

With keys delivered, doors granted and segments cut: guest disconnects and
reconnects.

- **Pass:** they see the same world the host does — doors open, keys where the
  host sees them.
- Gourds are known to survive this. Keys and doors are not.

### 8. Colour (prediction D)

- **Expected:** the guest sees all five keys yellow.
- **Verdict needed from a human, not a log:** is that acceptable as a
  documented wrinkle, like the radio's, or does it defeat the purpose? The tint
  exists only so a heap of identical keys at the hub can be told apart, and the
  guest is exactly the player most likely to be standing in that heap.

### 9. The radio wrinkle, watched instead of reasoned

Only the host runs an Archipelago client, so a guest hears a station the
moment it is switched on, whether or not the host has been sent its Radio
Music item. That is written into the option text already
(`RadioStationItems` in `apworld/bigwalk/options.py`) and has never once been
watched happening.

Guest switches on a station whose Radio Music item the host does **not** have.

- **Expected, and it is the documented wrinkle:** the check fires on the
  host's log, the host hears nothing, the guest hears the music.
- **Also worth catching:** whether the guest still hears it after a world
  reload, and whether the host later receiving that item changes anything on
  the guest's side. It should not — `FmRadioManager` is local to each machine
  and the mod only ever writes the host's.
- **The only result that is news** is it being WORSE than documented: the
  guest switching a station on unlocking it host-side too, which would mean
  the suppression is not where it is believed to be.

This costs no time of its own — a station gets switched on during any session.

---

## Results of the first full pass (2026-09-22/23)

Tests 10 and 11 have their own accounts above. The rest, as measured:

| # | What | Result |
|---|---|---|
| 1 | A guest cuts a segment | **PASSES.** `[Check] bigKeyBlueZone#cut0` through `#cut4` — all five, from the guest's hands. This was the one that could make a seed silently unwinnable, and it does not. |
| 2 | A guest places a key in its plinth | **PASSES** end to end: `its door was held back`, then `[Check] bigKeyBlueZone`. The door stays shut on both screens. |
| 3 | A door granted by an item | **PASSES.** Prediction A holds: it opens on both screens, and again after a world reload. |
| 4 | A key the slot does not own | **CANNOT HAPPEN.** Keys in a plinth or a monument are not grabbable by anyone, mod or no mod (player, 2026-09-23). Prediction B is moot and `KeyCustody` needs no client half. |
| 5 | A key delivered to the spawn point | **PREDICTION C DOES NOT HOLD**, and that is the good direction: the guest sees a delivered key exactly where the host does. |
| 6 | `Ctrl+R` with a guest holding a key | **FAILED TWICE; second fix untested.** The key leaves the host's world and stays in the guest's hands. First fix (guest-side, `StaleHeldPropReleaser` waiting for the server to stop naming the key) was retested 2026-09-23 and did nothing — because nothing ever told the server: its `PlayerHeldInformation` SyncVar kept naming the key. Its counter-test passed, though: nothing fell out of anyone's hands on its own over 15 seconds, so the guest-side half is safe to keep. Second fix: `ReceivedItemSpawner.ClearServerSideHold` makes the host write that SyncVar itself, which reaches every client. |
| 7 | The guest leaves and rejoins | **PASSES** once the gadget fix of test 10 landed. |
| 8 | Colour | **FAILED, fixed 2026-09-23, verified.** The guest saw five identical yellow keys. Prediction D was right that a `MaterialPropertyBlock` is local, but the cause was narrower: `KeyColours.PaintOnce` was only ever called from `KeyCustody.Tick`, which returns early unless `NetworkServer.active`. It had never run anywhere but the host. `KeyColourPainter` paints on every machine, and the player confirms both screens agree. |
| 9 | The radio wrinkle | **MEASURED 2026-09-23, and the option text is wrong.** It claims a guest hears a station the moment it is switched on. What happens: the station's light comes on on the guest's radio and not on the host's, and **neither** hears anything — correct for the host, whose music waits for the item. Once `Radio Music` arrives the host hears it (`FmStationSleuthFm granted and playing`) and **the guest still hears nothing, ever**. The guest also cannot operate a received radio properly, while the host can. Only the host runs a client, so only the host ever learns a station was granted: bringing guests in needs the host to tell them, i.e. the network channel. |
| 12 | Gadget persistence across a reload | **PASSES** since the join-burst fix. |
| 13 | Key spawn position | **PASSES.** `ApRuntime` grants with `toPlayer: _looseGourdsRestored`: a key sent live goes to the player, a key replayed during a reconnection goes to the hub — exactly the gourd rule. The log shows both. |
| 14 | Key and gourd colours | **FAILED, fixed 2026-09-23.** Two causes stacked: the painter above, and a stale config. Both installs still carried `GourdColor = #FFA62B`, the value measured on 2026-09-22 to render as a featureless glowing blob, because BepInEx never rewrites a key already present in a `.cfg`. The setting is gone; the colour is a constant in the code now. |
| 15 | `Ctrl+R` must not strip a guest | **PASSES, closed 2026-09-23.** Worn packs and their contents survive, gourds included since the gourd sweep was given the rule too (it never had been: Ctrl+R took a gourd out of a guest's pack, and the wearer's `PlayerShepherd` threw on the dead collider every physics tick — 1790 exceptions in one `Player.log`, zero once fixed). Host log after the fix: `Gourd resync: 2 loose gourd(s) cleared and 1 left where they were stowed`; totals unchanged, so no duplicate is restocked any more. **And the question the test ended on is answered: a loaded backpack cannot lie on the ground** (player). The pass that keeps a pack because something is homed in it therefore guards nothing for backpacks. It stays anyway: the gourd carton and the belts were not tried, and it costs one lookup per sweep. |

### Found on the way, still open

**A gourd handed to the GUEST hangs in mid-air on the guest's own screen** —
fixed 2026-09-23, **verified the same day**: in the guest's hands on both
screens. The guest's log shows the repair firing on a handed-over radio
(`netId 586 was handed to this player by the server but never reached their
hands; picking it up locally`); the gourd itself arrived properly this time
without it, so the original failure is intermittent rather than systematic. In the host's view it is properly in the guest's
hands. The log had been naming it all along: `claimed by <player> (via
SyncVar)` means the server says that player holds it while that machine's own
`hands.heldProp` does not. For a remote player that is ordinary; for the local
one it is the bug. `CosmeticGourdSpawnHandler.TryAttachToLocalHands` runs the
pickup locally now, and the gadget handler does the same.

**A cloned backpack accepts nothing stowed into it — FIXED 2026-09-23 and
verified the same day:** three backpacks and two gourd cartons, all of them
accepting a gourd, on both screens. Each clone is now built from a vanilla
instance of its own (`Cosmetic Backpack #0`, `#1`, `#2`), and past the vanilla
count — the island has one carton — an extra slot gets invented tickets from
the top of the range. The account of how it was found, kept below:

**Mechanism confirmed 2026-09-23, and it depends on ORDER.** Retested on a fresh save, the received
backpack accepted a gourd. The dump (`Ctrl+M`) says why: the hidden vanilla
backpack's home carries ticket 43035, and the office maps 43035 to *somebody
else* — the clone. `PropHome` registers its ticket in `OnEnable` and withdraws
it in `OnDisable`, so hiding the vanilla freed 43035 and the clone, built
afterwards, took it. The night before, the clones were built while the
vanilla still held it, got `Duplicate ticket`, and never retried.

Two consequences follow, the second one certain to bite in a real seed:

- a clone built before the hiding sweep is dead for the session, even once
  the ticket is freed — nothing makes it register again;
- **only one clone per ticket can live at a time.** Every clone of a kind is
  built from the same template and inherits the same tickets, so the second
  backpack a slot receives gets `Duplicate ticket` and accepts nothing. With
  17 filler kinds and 30 to 60 filler items per seed, duplicates are the
  norm, not the edge case. Same for anything else with a home or a switch in
  it — belts, the gourd carton, and very likely the radio's dial.

## Second pass, 2026-09-23 afternoon: the channel, and what a reconnection does

| What | Result |
|---|---|
| The host's overlay on the guest's screen | **PASSES.** `Archipelago (host): connected`, the goal, and the feed as items arrive. |
| The radio on a guest | **PASSES.** The guest can operate a received radio, a station switched on no longer lights up for them before its item, and a granted station plays for both. Test 9 is closed. |
| A guest deposits into a monument | **PASSES** — the check is credited on the host. |
| The Archipelago server dies mid-session | **PASSES**, and better than asked: the Mirror session carries on, a check made while disconnected is sent on reconnection, and so is one made by a host who quit and came back in the meantime. |
| A guest without the mod joins a modded host | **COULD NOT CONNECT — but not because of the channel.** The host's `Player.log` shows no connection attempt at all between the guest leaving (15:05:46) and coming back with the mod (15:07:40), and the guest's own log without the DLL holds no EOS line whatsoever, where every other log of that machine starts with dozens. The vanilla game does not seem to have reached online services at all. What the guest saw on screen is still to be asked. |
| A guest reconnects with gourds deposited and stowed | **FAILED, fixed the same day, untested.** On the guest's screen every gourd came out of its monument and out of every backpack — dropped, not lost; the host's world was untouched — and a pack worn before leaving no longer accepted anything. Of 28 gourds rebuilt, 27 ended on `unclaimed after 3s; dropping it`, and 83 gadgets of 86. That rule was written for a prop that has just arrived; a reconnection rebuilds props that are already pinned somewhere, and `SetLoose()` takes a prop out of its home. A homed prop is now left where the server put it (`CosmeticGourdSpawnHandler.FindHomeName`). |

## Third pass, 2026-09-25: the packaged build, with a real second player

Installed from the zip, on the world's default options (`package-test.yaml`).
Everything held, and three things were found and fixed on the way:

| What | Result |
|---|---|
| A friend with no mod at all joins a modded host | **PASSES.** Connects and plays a vanilla world. The earlier "stuck connecting" came from an install with BepInEx and its other plugins still active and only this mod's DLL removed — not a case a real vanilla player meets. |
| A guest reconnecting keeps stowed things in place | **PASSES** since `FindHomeName`: gourds stay in monuments and packs, a worn pack keeps working. |
| Universal Tracker against a real room | **PASSES.** Items given with `/send` show as normal because the server builds them with no flags (`NetworkItem(item, -1, 0)`, for every game); a gourd from `/send_location` shows as progression, which the seed file confirms for all 30. |
| Round lamps | **FIXED AND VERIFIED.** They desynced and were only ever red. The island keeps its lamps, so their tickets were never free, and the capture had filled every index with buoys, leaving no extra slot; then the `Ctrl+M` census showed the yellow lamp is another prefab (`BuoyProp`, 44 of them, light `#FFB766`) beside `BuoyLight` (65) and `BuoyRedProp` (3). A received lamp now draws its look from all three, with invented tickets. |
| A new Archipelago game without a working connection | **FIXED AND VERIFIED.** It stops on the first screen's Continue with the reason under the fields; Play on the second screen no longer sticks (the verdict used to be wiped by `OnEnable` between the two). Probes show moving dots, and long answers wrap and shrink to fit. |
| The mod's version | Shown in a corner of every menu. |

## Fourth pass, 2026-09-25 evening: the first alpha's feedback

The alpha's players sent a list of problems (sealed-box pick-ups, belts,
gourd colours, the host getting every item, arch doors). What was verified
with two machines the same day, then what is built and still waits for a
session.

### Verified

| What | Result |
|---|---|
| A guest takes a sealed-box puzzle's gourd (Telescope to Box) | **PASSES** since `PuzzleGourdRetirer`. The check goes out, the guest's hands are cleared, nothing throws, and the guest can pick something else up. It used to leave the guest unable to pick anything up for the rest of the session (`NullReferenceException` in `Prop.SetHeld`, mid-Command). |
| A received belt, on the ground and worn | **PASSES** on both screens, both ways: closed on the ground, a single belt when worn, a flare gun still holsters. It showed a second, hanging belt; the clone's "placed on hanger" state came from the vanilla belt's save key and from the server's copy of its ticket. |

### To test

Deploy with `tools/deploy-mod.ps1` first; the other machine takes
`F:\shared\BigWalkArchipelago.dll` and its log's first line must show the same
`loaded (build ...)` fingerprint as the host's.

| # | What | How | Expected |
|---|---|---|---|
| 16 | Arch doors held closed, and opened by their item | Solo. Seed from `tools/players/door-test.yaml` (`lock_arch_doors: all`, Left Arch Door at start), new save. | The first arch door (at the hub, beside the keyhole) and the right one stay **closed**; the left one (Sports Creek) is **open**. The log names each door held or opened (`ArchDoors`, `ArchDoorHoldPatch`). |
| 17 | Arch door opening live | Same room, with the server: `/send DoorWalk First Arch Door`. | The first door opens on the spot, without a reload, and stays open after one. |
| 18 | Arch doors seen by a guest | Two machines, same room. | The guest sees the same doors open and shut as the host. |
| 19 | Items go to every player in turn | Two machines, debug on, both with empty hands. Press `Ctrl+I` (simulated gadget) several times. | Gadgets alternate between the two players, appear in front of the one receiving, and land in their hands **on both screens**. |
| 20 | Nobody's hands are free | Both players hold something, then `Ctrl+I`. | One of them drops what they held and gets the gadget half a second later. The log reads "had full hands; they drop what they held". |
| 21 | Several items at once | Hands empty, `Ctrl+I` twice quickly. | Both players get one, not one player twice or only one item handed over. |
| 22 | Gourd colours | Two machines, a seed from `tools/players/belt-test.yaml` (3 gourds at start). | The gourds come in different colours among red, orange, yellow, green, blue and purple; each gourd has the **same colour on both screens**; none is a glowing white blob, including after walking far from the hub and back. |
| 23 | No `ap_` keys in a vanilla save | Solo. Host the `LGM` save with the Archipelago switch off, walk a little, quit. Back the save up first. | `save_LGM_*.sav` holds no `"key":"ap_` entry. Restore the backup afterwards either way. |
| 24 | Puzzle gourds stowed in a bag | Only with a save that has one (the alpha host's). | Host log: `PuzzleGourdRetirer ... inside BackpackProp...; taking it out of play`; nothing left at the spawn to pick up. |

### Results, 2026-09-26: the first loopback pass

All run on one PC with the loopback guest, on the build that became 0.1.1.

| # | Result |
|---|---|
| 16 | **PASSES.** `Held closed until their item arrives: SpawnHubGate, HubShortcutToSportsCreek, HubTunnel`, then the Left door (in the start inventory) opened. |
| 17 | **PASSES.** `/send DoorWalk First Arch Door`: `SpawnHubGate granted`, then `opened`, on the spot. |
| 18 | **PASSES.** The guest, started by Ctrl+L, sees the same doors. |
| 19 | **FAILED, fixed, then PASSES.** Items alternated between the two players but never reached anyone's hands: the hand-over queue was drained below the early return taken when Archipelago is off in the config, so only the debug simulators (Ctrl+I) ever met it. A real session was not affected. After the fix, `Item handed to` alternates between the host and `GRIZ_TWR-1`. |
| 20 | **PASSES.** `had full hands; they drop what they held and take the new item next`, for both players. |
| 21 | **PASSES.** Two quick items, one each. |
| 22 | **FAILED, fixed, then PASSES.** All three gourds red, on both screens. `RewardGourd.Awake` copies `variantChallengeColor` into `propertyBlockHelper.colorSettings[0]` once, before the clone has a netId — netId 0, the first of the six colours, every time — and setting the field again later changed nothing, since `Refresh()` pushes `colorSettings`. The colour is now written where Awake writes it, and the log says `took` on both sides. |
| 23 | **PASSES**, twice: the `LGM` save came back with no `ap_` key. Restored from a copy afterwards. |
| 24 | **NOT RUN**: it needs a save with a puzzle gourd already in a bag, which only the old sealed-box bug could make. Shipped untested, with a note in the release. The opposite case (nothing touched on ordinary saves) was checked when it was built. |
| 6 | **PASSES.** Ctrl+R cleared the key from the guest's hands (`GRIZ_TWR-1 was still holding netId 219 ... cleared`) and it went back to the spawn point, on both screens. Note: on its first delivery the key was put back 5 times in 2 s (`tug of war`) rather than the usual one or two, and still settled. |

**Found on the way, and the worst bug of the release: a check made in one save
was sent for another.** An Archipelago save, then a finished vanilla save
loaded with the switch off, then the Archipelago save again, all in one run of
the game: the second load sent the room 59 checks at once — every puzzle, key
and station of the vanilla save — and got 59 items back. Every check the host
saw went into one in-memory queue, whatever the switch and whatever the save,
and the queue went to the next room the process was connected to; the socket
outlives a return to the menu, so that was the old save's. Now a check made
with the switch off is never queued, and a world that goes away takes its
leftovers with it (to its own room if still connected, dropped otherwise: the
save's `ap_reported_*` ledger replays them on its next connection). Re-run the
same way: 59 checks in the local log, 0 sent.

**Also checked:** returning to the main menu from a world raises nothing with
this build. The first alpha host's log showed a NullReferenceException there
(and three IndexOutOfRangeExceptions mid-play, never seen here); that host ran
Wine and two movement mods, so neither is attributed to this mod.

---

## Regressions to re-check while two players are available

These worked in co-op before this session and touch code that changed:

- a gourd received by the host appears in the host's hands on the guest's
  screen, with the right colour and gravity;
- a gourd arriving while the host's hands are full goes to a player with free
  hands (since 2026-09-25 this is test 19's rule, for every arrival);
- the guest deposits into a monument and the host records it and credits the
  check;
- the Archipelago server dying does not break the Mirror session.

`ApRuntime` was modified in five places. `ItemApplier.ApplyBigKeyItem` now
branches on slot_data before doing anything.

---

## Filler gadgets — untested in co-op (added 2026-09-22)

*Everything below was built and exercised solo today (see `NEXT-SESSION.md`
for the day's fuller account): 17 filler gadgets, their vanilla-instance
removal, their persistence across a world reload, plus fixes to key
spawn-position and two colour bugs. None of it has run with a second player.
Same shape as the section above — `GadgetItemSpawner`/`GadgetSpawnHandler`
were written by deliberately mirroring `ReceivedItemSpawner`/
`CosmeticGourdSpawnHandler`'s already-proven co-op patterns, but "mirrors a
pattern that worked" is a reasoned prediction, not a measurement.*

### 10. A gadget received by the host, seen by the guest

**RUN 2026-09-22/23. FAILED HARD, TWICE, AND NOW PASSES.** The guest sees the
right gadget, correctly shaped and textured, falling normally. What it took is
below, because both failures were worth more than the pass.

**First failure — the removal replicated.** The guest's log opened with 16
`No X (XProp) found in the scene to capture as a template` and exactly one
success, `Lamp template captured from 'BuoyLight'`. The Lamp is the only kind
`KeptInWorld` spares, which is what turned a guess into a measurement: the
scene was loaded, and what was missing was precisely what the host had
destroyed. `VanillaGadgetRemover` was host-only and used
`NetworkServer.Destroy`, so a guest arrives in a world whose gadget props are
already gone and captures nothing.

Then the damage, which was not cosmetic at all: with no template,
`GadgetSpawnHandler.Spawn` returned `null`, Mirror logged `Spawn Handler
returned null` / `Could not spawn assetId=186030849`, and the host's
`PlayerHeldInformation` — a SyncVar holding that very netId — dereferenced a
null identity inside `DeserializeSyncVars`. `OnDeserialize failed` +
`PlayerNetworking OnDeserialize size mismatch`, then a NullReferenceException
every frame for the rest of the session, gourds no longer claimed
(`unclaimed after 3s; dropping it`), the lot. **A missing cosmetic broke a
player's whole network state.** Fixed by never returning null: a stand-in
cloned from another template, or failing that an object carrying nothing but
a `NetworkIdentity`, so the netId always resolves.

**Second failure — the props were there, switched off.** With the hiding made
local and symmetric, the guest still captured nothing but the Lamp. Same
control, one step further in: on a client these props are *inactive*, because
Mirror sends no spawn message for an object deactivated on the server, and
`FindObjectsByType` skips inactive objects by default. Fixed by scanning with
`FindObjectsInactive.Include`.

That second finding carries a bonus worth keeping in mind: since Mirror will
not spawn a deactivated object, **hiding locally on the host is enough to hide
it from every guest too**. The networked destroy was never needed.

**What the guest's log reads now:** 17 `template captured from`, then `Hid 0
vanilla gadget prop(s) from this machine's map, and is keeping 70 that were
already off`, then `Cosmetic Megaphone built at (...)`. No `stood a Lamp`, no
`Spawn Handler returned null`, no `OnDeserialize failed`.

**STILL OPEN, found in the same log — the join burst.** Before the guest's
world was ready, 43 gadgets the host already had were spawned to it, and every
one got an empty placeholder: the templates did not exist yet. Their network
state is sound, which is the placeholder doing its job, but all 43 are
invisible for that guest for the rest of the session. It will happen to any
guest joining a world that already has gadgets lying around, which after ten
minutes of play is every guest. Not yet fixed; the lead is to swap the real
clone onto the placeholder's netId once the templates are captured (the mod
already reaches into `NetworkIdentity`'s private setters elsewhere).

*The original plan, kept for what it asked:*

Host receives a filler gadget (`Ctrl+I` on the host cycles through all 17).
Guest watches the hub.

- **Pass:** the guest sees it appear, correctly coloured/shaped (not the
  pink/magenta or invisible states chased and fixed solo today), with normal
  physics — not hanging in mid-air.
- **Basis for expecting this to work:** `GadgetSpawnHandler` is a structural
  copy of `CosmeticGourdSpawnHandler` (own `RegisterSpawnHandler` per
  `GadgetKind`'s assetId, own `SetLoose()` call on the guest's own copy,
  deferred a frame). If it fails the same way the gourd once did — correct
  netId, wrong physics — the fix has the same precedent already applied here
  in `GadgetSpawnHandler.DrainPendingLoose`.
- **Also check:** does the guest's OWN locally-built clone come out correctly
  coloured? The three render fixes (`ClearLightmapReferences`,
  `FixMaterialessRenderers`, `RefreshPropertyBlockHelpers`) run inside
  `GadgetItemSpawner.BuildNeutralizedClone`, which `GadgetSpawnHandler.Spawn`
  calls on the guest's machine too — so in principle yes, but this was only
  ever measured on the host's own screen.

### 11. Vanilla gadget removal, seen by the guest

**RUN 2026-09-23. PASSES** — the guest's world is clear of them too, and by a
different mechanism than this test assumed. Nothing is destroyed any more:
each machine hides its own copies, and a guest's copies are never switched on
at all because Mirror does not spawn a deactivated object. The guest's log
says `keeping 70 that were already off`, which is that sentence measured. See
test 10 for why the destroy had to go.

At world load, `VanillaGadgetRemover` destroys every vanilla instance of the
17 gadget prefabs (host-authoritative `NetworkServer.Destroy`, chosen
specifically because a plain `SetActive(false)` does not replicate — see the
comments in `GadgetItemSpawner.cs`).

- **Pass:** the guest's world is also clear of them — no megaphones,
  walkie-talkies, etc. lying around as scenery, same as the host's.
- **Expected to pass:** `NetworkServer.Destroy` is a proper networked call,
  unlike the `SetActive` it deliberately avoids. Still unverified with a
  second machine watching at the moment of world load.
- **The Lamp exception:** `BuoyLight` is the one kind left in the world on
  purpose (player decision). Confirm the guest still sees buoy lights too,
  not just the host.

### 12. Gadget persistence across a world reload, with a guest present

Host receives several gadgets, then the **guest** disconnects/rejoins, or the
world reloads with both connected.

- **Pass:** the reconciliation (`RestoreLooseGadgets`, same shape as the
  gourd one) runs on the host and the respawned gadgets appear for the guest
  too, same as test 10.
- This is the solo-confirmed "keep them, spawn them on restart" feature
  (2026-09-22) meeting a second machine for the first time.

### 13. Key spawn position, seen by the guest (fixed today)

`KeyCustody.Grant` no longer hardcodes `toPlayer: true` — it now reads the
same settled flag as gourds, so keys arriving during the startup burst land
at the hub instead of on the player. Host receives several keys at once
(e.g. starting inventory). Guest watches.

- **Pass:** the guest also sees the keys land at the hub, not converge on
  the host's position.
- Untested with a second machine — the fix was verified solo only.

### 14. Key and gourd colours, seen by the guest (existing prediction D,
    plus today's repaints)

Prediction D from the section above still applies, now with three additional
wrinkles from today:

- The blue key's colour was repainted **twice** today (ending on a
  near-neutral grey/steel, `#8898A0`) after two other attempts read as green
  on the host's own screen. If colour is local per machine (prediction D says
  it is), the guest needs their own separate look before this can be called
  settled for real.
- The gourd colour default changed from `#FFA62B` (confirmed today to render
  as a featureless glowing white blob — bloom/overexposure, not a hue
  problem) to `#805020`. Confirm the guest's own gourd clone also renders
  correctly and not as a blob — this is a per-machine `MaterialPropertyBlock`
  write, so nothing guarantees the fix looks the same on a different GPU/
  display without checking.
- If prediction D turns out true (the guest sees keys plain yellow), that is
  now a bigger loss than it was solo: seven keys are hard enough to tell
  apart already, and the fixes above assume the tint is visible at all.

### 15. Ctrl+R must not strip a guest (added 2026-09-22)

The resync sweep now clears the filler gadgets too, and the host is the only
one who can press the key. So for the first time a host keypress can reach
into what a **guest** is wearing and carrying — player's own objection, the
same day the sweep was written: *"the backpacks and belts worn by players
must not answer this command, and neither must the objects hanging inside
them."*

`GadgetItemSpawner.DestroyLooseCosmeticGadgets` therefore sweeps only props
with no `PropHome` at all, and keeps any prop that is somebody else's home.
Solo, that was verified only in the trivial direction: a gadget dropped on
the ground is swept, one worn is not.

**NONE of this can be pre-cleared solo, and that is a property of the game,
not an omission.** Established by the player on 2026-09-22 while trying:
putting a pack on takes a second player to place it on your back, *and*
nothing can be stowed in a pack that nobody is wearing. So the worn half and
the stowed half are both unreachable alone, and every line of the rule
guarding them is written from the decompiled `PropHome` and has never once
executed against a real case.

A consequence worth checking at the same time: the sweep's second pass keeps
a pack that is somebody's home, which was written for a pack lying on the
ground with something inside it. If a full pack cannot be dropped, that
state does not exist and the pass is guarding nothing. Try dropping a loaded
pack while both players are there — the answer decides whether that code
stays.

Host wears a received backpack and stows something in it. Guest does the
same with their own. Host presses **Ctrl+R**.

- **Pass:** both packs stay on both backs, both contents stay inside, and the
  log reads `N cosmetic gadget(s) left alone: worn, stowed or holding
  something that is`. Only gadgets lying on the ground come back to the hub.
- **Fail worth catching early:** the guest's pack vanishes from their back.
  That would mean `PropHome.parentCharacter` is not populated on the host for
  a home belonging to a remote player — i.e. the host cannot see who is
  wearing what, and the rule has to move from "does it have a home" to
  something the server can answer.
- **Also watch:** a gadget stowed in a pack that is lying on the ground. The
  second pass is supposed to keep that pack because something is homed in it;
  solo there was never a second player to leave one lying about.

---

## What to do with the results

Record them in `mod/reverse-engineering-notes.md` under co-op, and update
`apworld/protocol.md` §12, which currently claims — on reasoning alone — that
co-op needs nothing extra:

> Peck state is not local, and `SetPinDirectControlSystem` is server-side to
> begin with. The host suppresses, the host grants, the guest sees the door
> open. Nothing for the guest to run and no wrinkle to document.

If test 3 fails, that paragraph is wrong and should be corrected rather than
softened. If tests 4 and 5 fail as expected, they are not wrinkles to document
— they are work, and the alpha should say so or wait for it.
