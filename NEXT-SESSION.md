# Where this stands, and what to do next

*Written at the end of the session of 2026-09-21 (second half). The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is the short version and the
to-do list.*

**Start here: co-op.** The big keys are built AND exercised in game — Task 1
below, with the list of what the session of 2026-09-21 confirmed. What none of
it has ever met is a second machine, and that is now the most valuable hour
available: [`COOP-TESTS.md`](COOP-TESTS.md) is the plan, ordered by risk, and
its first test decides whether a co-op seed can be silently unwinnable.
Everything else in this file is either untouched (Task 2) or a decision rather
than work.

**Before touching anything in game**: `tools/deploy-mod.ps1` builds and
deploys to every install, then prints one hash and says whether they match.
Three co-op tests were wasted in one weekend on a guest running older code.

**A lesson from 2026-09-21 that will save an hour**: the game instantiates
every gourd, every broadcast station and every key blank **everywhere**.
Presence tells you nothing about location — two dumps taken in two different
zones came back byte-identical. Every dump that asks "what is in this area"
must sort by distance to the player. Ctrl+V and Ctrl+B already do, and Ctrl+V
now also names the puzzles it could not read at all — presence, distance and
absence had stopped being told apart, and the thirteen it never instantiated
went unnoticed until they were counted by hand. They turned out not to be in
the game at all.

**And the lesson that would have saved the most**: the answer was sitting in
plain JSON in `AppData/LocalLow/House House/Big Walk/user_data/save_games`. A
save lists what the game really contains — `gourdXxx = <SaveableHomeName id>`,
so `gourdHighButton = 1012` is a gourd still at `valetHighButton` and a
monument slot id is one that was deposited. A finished save answers "what
exists" in seconds, with no session in game at all. Read one before planning
the next dump.

## Task 1 — big keys: BUILT AND EXERCISED IN GAME

The model (`apworld/design-decisions.md`, `apworld/protocol.md` §12):

- **Locations**: the 25 cut segments, plus the 7 deposit locations. 32 in all.
- **Item**: the feature itself — Drawbridge, Map Room, Chairlift, Train,
  Tunnels, Dam, Green Dome.
- **The key**: a check carrier. Placed in its receptacle it is inert.

### What opens a door — ANSWERED, and it was none of the three candidates

All three eliminated candidates were components of the **plinth**, and that is
why they all came back empty. The wiring is on the **key**:
`Prop.taggedPinSystems` is a `(PropGroup, TrackedPeckState)[]` carried by the
prop, and `Prop.SetPinDirectControlSystem` fires the entry whose group matches
the home's `pinGroup`. Decompiled at `0x1803C6B10` — twenty minutes, against
a lead this file had been recommending since September that would have taken
far longer to reach the same place. Full detail in
`mod/reverse-engineering-notes.md`, "what DOES open them".

### What was built

| Piece | Where |
|---|---|
| Grant a feature, ledger it under `ap_feature_*`, re-apply on every world | `mod/src/Core/KeyFeatures.cs` |
| Stop a placed key opening its door, and nothing else | `mod/src/Patches/PropTaggedPinPatch.cs` |
| The 25 cut checks | `mod/src/Patches/KeyBlankCutPatch.cs` |
| `big_key_features` + `cut_id_offset` in slot_data | `ApSlotData.cs`, `world.py` |
| 25 locations, 7 renamed items, cut rules, no more Tutorial Key special case | `apworld/bigwalk/` |
| Ctrl+D: open a door with no key (the same call the item makes) | `mod/src/Debug/DebugBigKeyDoorForce.cs` |

Two findings worth keeping, both of which would have cost a session each:

- **`ServerCutSegment` is un-patchable.** No code address in the export while
  every neighbour in `KeyBlank` has one — inlined into its caller. This file
  named it as the check hook; it would have bound to something nothing calls
  and reported nothing, in silence. The hook is `OnCutsUpdated`.
- **The connection-time burst trap dissolved** rather than needing a
  workaround. It existed because the mod pinned the precollected Tutorial Key;
  the mod now pins nothing, so a key in its plinth is one the players carried
  there and its five cuts are genuinely theirs.

### What the session of 2026-09-21 actually confirmed

All of it in play, on a real server, with the log to match:

- the door opens from its item (`[KeyFeatures] bigKeyIntro is now open.`);
- the five cut checks fire one per segment;
- placing a key does NOT open its door (`its door was held back`);
- a key item unlocks its key, unpins it and drops it at the spawn point;
- the keys come out tinted, one colour each.

**Four traps were sprung and are worth carrying forward.**

1. **The door is on the key's HOME, not on the key.** A decompiled reading
   of `Prop.SetPinDirectControlSystem` put the feature in
   `Prop.taggedPinSystems`; measurement found that array EMPTY on all seven
   keys. Of the three states that function drives, only the home's
   `pinDirectControlSystem` is non-null for a big key — and driving it
   opened the drawbridge with no key in the plinth.
2. **`systemRefences` predicts nothing.** The intro plinth's state reported
   ZERO effects registered and opened its door anyway. A diagnostic written
   that day claimed otherwise and would have talked the next reader out of
   the right answer.
3. **A `MaterialPropertyBlock` never complains about a property the shader
   does not declare.** Writing `_RColor` — a real name, measured on the
   Black Monolith key — painted five keys, reported success, and changed
   nothing. The keys run `househouse/VertexColors`, whose tint is
   `_TintColor`. And since that tint MULTIPLIES, white is the identity and
   grey merely dims: only a saturated hue moves a yellow key off yellow.
4. **Three static accessors THROW instead of returning null** when no world
   is loaded — `FmRadioManager.instance`, `PropHome.GetSaveableHome` and
   `Prop.allProps` — and connection time is exactly when there is no world.
   Treat it as the rule for this game, not as a surprise.

### What is left to watch

- **The key is fetched home once after the unpin**, and putting it back on
   the next frame settles it — measured as `put back 1 time(s)`, and `0`
   for a key already loose. `PropHome.CheckShepherd` calling
   `PropShepherd.DoShepherd` is the named suspect; Ghidra resolves no
   caller, because IL2CPP dispatches indirectly.
- **Co-op has not been tried** for any of this, and it is now the largest
   unknown left in the project. The test plan is in
   [`COOP-TESTS.md`](COOP-TESTS.md), ordered by risk, with four predictions
   stated up front — two of which are expected to FAIL: a guest can probably
   pick up a key the mod has locked (`PropHome.blockGrabbing` is a plain bool
   with no `[SyncVar]`), and a key delivered to the hub probably stays at its
   tower on their screen (a Prop's physics is local to each machine, as the
   mid-air gourd of 2026-09-16 established). The one that must hold is the
   first test in that file: if a guest cutting a segment does not fire the
   check on the host, it is lost forever and the seed is silently
   unwinnable.

### The rest of what to do in game, in order

The five solo steps this section used to list — Ctrl+K at a tower, Ctrl+D on a
door, placing a key with `big_key_features` on, cutting a segment, reloading
with a feature granted — were all run on 2026-09-21 and are the confirmations
above. Nothing solo is waiting. What is left is short:

1. **Co-op**, all of [`COOP-TESTS.md`](COOP-TESTS.md), hardest first.
2. **Does the tutorial drawbridge gate the way out?** Two minutes: stand in
   the tutorial on a save where the Drawbridge feature has NOT been granted
   and try to walk out. It matters more than it looks — see the open question
   below.
3. *(optional — the question it was written for is answered)* **Ctrl+V behind
   the chairlift and in the tunnels.** The dump now names the puzzles it could
   not read instead of printing only what it found. The thirteen were settled
   from a save file rather than in game, so a press here would only be a fourth
   confirmation — worth it if you are standing there anyway.

### The assumption that was left unmeasured, and no longer is

**Is every key-cutting station reachable without a big key? Yes** — all five
trails, confirmed in play on 2026-09-21. The 25 cut locations stay in the
overworld, and this was the last thing left in this world that could still
have made a seed unbeatable.

## Task 2 — the island's objects as items

Inventoried on 2026-09-21 (`Debug.DumpPropsKey`, Ctrl+J; the table is in the
notes). Four flare guns, walkie-talkies, binoculars, x-ray goggles, lasers,
torches, megaphones, three cowbells, a folding map, a compass.

The finding that opens the design: **every one carries a `savablePropGuid`**,
the game's own per-prop identity, readable with
`SaveManager.GetIsInInventory(guid)`. So these props can be locations as well
as items — a guid tells one apart from its siblings across sessions. None has
a `SaveablePropName`, so none of the puzzle machinery applies.

Nothing is designed yet. The open questions are what "obtaining" one means for
an object that is simply lying there, and whether an item should spawn a copy
(the clone technique `ReceivedItemSpawner` already uses for gourds) or unlock
something.

## Then: the release

`tools/package-mod.ps1` produces the players' zip, the debug module is off by
default (`Debug.Enabled = false`), and `apworld/build.py` packages the world.
Both halves are rebuilt after any change; the artefacts sitting in `dist/` and
`apworld/dist/` are only as fresh as the last run of those two scripts.

**The version is settled (2026-09-21): 0.1.0, and there is nothing to do.**
`Plugin.PluginVersion`, the `.csproj`, `archipelago.json` and `WORLD_VERSION`
already agree, and the alpha is the first thing to ship publicly, so 0.1.0 is
what it is called. What matters from here is that the two halves move
together: a mod older than its apworld ignores `big_key_features`, so it pins
keys as before AND never reports the 25 cut checks, which makes a seed
unbeatable if anything needed sits on one. The mismatch only warns; bumping
both at once is what keeps that warning readable.

**The item names are settled too (2026-09-21): the seven bare nouns stay** —
`Drawbridge`, `Map Room`, `Chairlift`, `Train`, `Tunnels`, `Dam`, `Green
Dome`. A prefix earns its place only where it disambiguates: `Radio Music:
Bobby` needs one because `Bobby` alone says nothing in a multiworld feed,
`Chairlift` does not.

Reviewing that question turned up a real mismatch one step away, and it is
fixed: three deposit locations were named after their **plinth** rather than
their feature — `Train Station Key Deposit`, `Tunnel Key Deposit` and
`Goodbye Keyhole Key Deposit`, inherited from the third-party document that
first listed them — while the items and the 25 cuts were named after the
feature. They now read `Train Key Deposit`, `Tunnels Key Deposit` and `Green
Dome Key Deposit`, so all seven towers spell one name three ways and no
other: `<feature> Key`, `<feature> Key Cut 1..5`, `<feature> Key Deposit`.
Renamed before the datapackage was ever published, which was the whole window
for doing it cheaply. (The cross-validation table in
`mod/reverse-engineering-notes.md` still quotes the document's own names, on
purpose — it is a record of what that document said.)

**The radio in co-op** no longer needs a session of its own: it is test 9 in
[`COOP-TESTS.md`](COOP-TESTS.md), watched during the co-op pass rather than
arranged separately. The accepted wrinkle is that a guest hears a station as
soon as it is switched on, because only the host runs an Archipelago client.
It is documented in the option text; it has never been watched happening, and
the only result that would be news is it being worse than documented.

## What has been confirmed

### With a second player (2026-09-20)

- A gourd the host receives appears **in the host's hands** on the guest's
  screen, with the right colour and gravity.
- A gourd arriving while the host's hands are full goes to the nearest
  player with free hands, and both players see it in the right pair.
- **Ctrl+R works while the guest is holding one**, from either side.
- The guest **deposits into a monument** and the host records it
  (`ap_home_monoumentIntroSlot0 = 1`), the server credits the check, and
  an item comes back for it.
- The guest **leaves and rejoins**: their gourds are rebuilt.
- The **Archipelago server dying** does not break the Mirror session — the
  host simply gets a notice on screen.
- A gourd taken from a sealed box by the guest is released from their
  hands by `StaleHeldPropReleaser`.

### Solo (2026-09-21)

- **Radio stations fire.** `[Check] FmStationBreathwork` — seven locations
  that had never triggered once in the life of the project, working first
  try on code that had never executed.
- **`Archipelago/Enabled = false`** falls back cleanly:
  `Archipelago disabled in the config; checks are logged locally only`,
  zero connection attempts, zero errors, and every other component still
  running.
- **The `deposits` goal** reports. Worth noting what the counting showed:
  only four deposits happened in that session for a threshold of five —
  the fifth was the guest's, from the day before, restored at load. The
  count is cumulative across sessions, which is the intended model and had
  never been demonstrated.
- **The `gauntlet` goal** reports — the apworld's DEFAULT, and the path
  most players will take, reached legitimately for the first time.
  `GauntletComplete changed during play (unknown -> 1)`, latched in the
  middle of a Ctrl+G, right after `NHoldLogic 2` was forced: the bell's
  N-hold, which is exactly what DebugPeckCombinatorForce was written for.

All three goals are now confirmed end to end — `ending` (EndingGate 1 -> 2),
`deposits`, and `gauntlet`.

## Why two local instances do not work

Tried on 2026-09-18: the game authenticates through EOS with the Steam
identity, and two sessions of the same identity on one machine do not both
get a ticket (`Failed to get auth ticket`, then `Failed to Init Host Menu`).

The lead, if this ever becomes worth an hour or two: **`KcpTransport
initialized!` appears in every log**, on both machines. The game carries a
direct UDP transport besides EpicTransport. Forcing the NetworkManager onto
it with a direct address would let two local instances talk over 127.0.0.1
without EOS at all — but the hosting screen is gated behind EOS auth, so
that would have to be bypassed too. It is a mod of its own, not a setting.

A solo mod that swaps one connection between two PlayerCharacters
(`ReplacePlayerForConnection`) cannot help either: every remaining question
is "does this replicate to a second client", and it has only one.

## Open questions that could still bite

- ~~**Is the radio dial ordered like the enum?**~~ **Settled (2026-09-21): no.**
  Six of the seven disagree. The enum-order fallback was removed and the dial
  position is learned from the world instead, per save.
- ~~**Are all 58 puzzles reachable without any big key?**~~ **Settled
  (2026-09-21): no**, and it was a seed-breaking bug. Seven purple gourds and
  one station sit past the chairlift, one station past the tunnels; both are
  now regions gated on their key. One residual unknown, answered by the player
  rather than measured: four ordinary gourds in the same band of distance are
  reachable without the chairlift, so they stay in the overworld.
- ~~**And the thirteen puzzles no dump ever instantiated?**~~ **Settled
  (2026-09-21), and it was a second seed-breaking bug, worse than the first.**
  They are not in the build at all: a finished save of 2026-08-23 holds exactly
  45 `gourd*` entries filling every monument slot in the game (4/5/5/5/5/6/15),
  and none of the thirteen. 45 gourds for 45 slots is the whole economy. They
  were locations until that day, so generation could put progression on a check
  that can never be sent — and no region could have rescued it. Cut from
  `PUZZLES`, kept as `data.ABSENT_FROM_THE_BUILD`. The world now ships 45 puzzle
  locations.
- ~~**Does the tutorial drawbridge really gate the way out?**~~ **Settled by
  the player (2026-09-22): no, it does not.** The mod opens the three hub arch
  doors — `SpawnHubGate`, `HubTunnel`, `HubShortcutToSportsCreek` — from a
  save's first session (`Core/ArchDoorUnlocker.cs`, added unconditionally and
  not a config toggle), and with those open the tutorial is not sealed. So
  `start_with_tutorial_key: false` is safe, and there is nothing to model: the
  absence of a Tutorial region in `regions.py` is correct rather than a gap.
  Worth remembering the day ArchDoorUnlocker is ever made optional — it is
  what holds this up, not the option's default.
- ~~**Do `FmStation7/8/9` exist at all?**~~ **Settled (2026-09-21)**: they
  exist in `SavableSystem` (37–39), and nothing suggests they are wired to
  anything. Left out, on both sides.
- **`ap_reported_*` is not scoped to a seed**, so a save reconnected to a
  *different* seed resends checks earned elsewhere. Harmless in real use,
  where a save belongs to one seed — but it will skew a count during
  testing. This is why `coop-test.yaml` uses a slot name of its own. Note
  that `ap_radio_*` **is** handled: the ledger is cleared when the item
  cursor resets.

## Leads deliberately not taken

- **Monument fill state in Archipelago's `DataStorage`**, so a new save
  recovers its deposits instead of only its items.
- **A real in-world button for the gourd resync** instead of Ctrl+R.
- **Traps.** The item and the YAML option exist, the effect does not.
- **Per-tower monument locations (Option C/D)**.
- ~~**The big-key decomposition, and Key Cutters as checks**~~ — both built on
  2026-09-21, as Task 1 above. The five cuts per tower ARE the Key Cutters
  lead, reached from the other end.
- ~~**Props as checks**~~ — promoted to Task 2 above, now that the inventory
  exists and every candidate turns out to carry a `savablePropGuid`.

## Lessons worth carrying over

**A successful build does not prove an edit was applied.** An edit script
failed an assertion and wrote nothing; `dotnet build` then compiled the
unchanged source and reported success, which read as confirmation. The
change was believed done for two days. Check the file, not the build status.

**A build does not prove a Harmony patch will bind, either.** A patch
targeting a method by string name compiles whatever the string says and
throws at `PatchAll`, taking the whole plugin down. Il2CppInterop re-emits
the game's private methods as public, so `nameof(BroadcastStation.Unlock)`
works and the compiler checks it. Arguments go by index (`__0`) rather than
by name, since parameter names survive into the interop assembly only by
convention. There is a metadata probe for settling this kind of question
without launching anything — `System.Reflection.Metadata` over
`mod/lib/Assembly-CSharp.dll` lists a type's real members in seconds.

**A tool that says nothing when it finds nothing costs more than it saves.**
`DebugPeckCombinatorForce` returned silently when everything was out of
range, which is indistinguishable from a key that does not work — an hour
went into remapping keys for a problem that did not exist.

**Debug hotkeys are unreliable, and not only through collisions.** BigTV
owns F7 through F11 and wins every one, so tools bound there are never
reached. Beyond that, four plain keys — End, Keypad1, Keypad2, F1 — have
never once registered, while Keypad0, Keypad4, F2, F4, F5, Delete and
Ctrl+R all work. NumLock was the obvious suspect and is ruled out, since
the keypad answers. No explanation, and it is not worth inventing one:
**`<letter> + LeftControl` is the proven format** (it is what Ctrl+R uses,
and now Ctrl+B), so bind new tools that way. Hours went into this, twice,
because "the tool printed nothing" and "the key was never seen" look
identical from outside — which is why both combinator keys, and the radio
dump, log before and after the call.

**Ctrl+G is a sledgehammer.** At the 120m radius it forced fifteen
combinators in one press, `PosesBlockedSystem` and `TurnStileSystem`
included. Fine on a test room, wrong on a save anyone cares about.

**When the question is "what does this function do", it has a static
answer — go and read it.** A full day went into inferring the pick-up
chain from method names (`PickUp`, `Drop`, `SetLoose`, `SetHeld`), one
in-game two-machine round trip per guess. Ghidra is in this repo, the
project is already analysed, and `analyzeHeadless` with a Java script
(this Ghidra has no Python) decompiled the answer in twenty minutes:
`PlayerHands.PickUp` and `Prop.SetHeld` are both purely local, and the
server half of the game's own Command, `UserCode_CmdPickUp`, writes
`NetworkplayerHeldInformation` — the only field another machine sees. The
guess being tested at the time would have changed nothing.

**The radio proved the same point twice over.** The question this session
opened with was "does writing `FmStation*` back to 0 stop the music?" — an
hour of decompilation answered *no*, and more usefully explained why: the
manager never reads the save and has no relock at all. A full test cycle
would have returned "it does not work" and nothing else. It also caught a
trap no amount of testing would have named: `BroadcastStation.Unlock` is an
**inlined copy** of `FmRadioManager.Unlock`, so patching the obvious target
would have suppressed nothing, silently.

**When something only misbehaves for the second player, suspect authority
before suspecting the network.** Three separate bugs this weekend were the
same mistake in different clothes: a write performed on the machine that
does not own the state — `hands.Drop` host-side on a remote player,
`PickUp` client-side on oneself, and a scene-object clone spawned under an
assetId no client could resolve.
