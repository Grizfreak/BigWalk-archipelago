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

**Two machines.** Two local instances do not work: the game authenticates
through EOS with the Steam identity, and two sessions of one identity on one
machine do not both get a ticket (`Failed to get auth ticket`). See
`NEXT-SESSION.md`, "Why two local instances do not work".

**Run `tools/deploy-mod.ps1` and check the hash matches on both machines.** It
prints one fingerprint and says whether the installs agree. Three co-op tests
were wasted in one weekend on a guest running older code; the script exists
because of it.

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

---

## Regressions to re-check while two players are available

These worked in co-op before this session and touch code that changed:

- a gourd received by the host appears in the host's hands on the guest's
  screen, with the right colour and gravity;
- a gourd arriving while the host's hands are full goes to the nearest player
  with free hands;
- the guest deposits into a monument and the host records it and credits the
  check;
- the Archipelago server dying does not break the Mirror session.

`ApRuntime` was modified in five places. `ItemApplier.ApplyBigKeyItem` now
branches on slot_data before doing anything.

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
