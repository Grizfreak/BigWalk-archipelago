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
