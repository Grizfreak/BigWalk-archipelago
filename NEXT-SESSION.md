# Where this stands, and what to do next

*Written at the end of the session of 2026-09-21. The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is the short version and the
to-do list.*

**Start here: one in-game session settles two things at once.** The
radio-as-an-item feature is written on both sides and nothing about it has
run in the game yet — written-but-unverified is the riskiest state anything
in this repo can be in. In the same session, one press of **Ctrl+K** near
the drawbridge and a coloured tower grounds the whole big-key design in real
numbers instead of a third-party document. Do both, then decide whether to
package or keep building.

**Before touching anything in game**: `tools/deploy-mod.ps1` builds and
deploys to every install, then prints one hash and says whether they match.
Three co-op tests were wasted in one weekend on a guest running older code.

## Written, not yet tested: the radio as an Archipelago item

Switching a station on used to report its check AND grant the station — the
only check in this world that rewarded itself. Both halves now exist:

- **apworld** — seven `Radio Music: …` items behind the new
  `radio_station_items` option (default on), displacing seven filler. 148
  tests green, and a real seed generates and places all seven.
- **mod** — `Core/RadioStations.cs` (the `ap_radio_*` ledger and the live
  unlock) and `Patches/BroadcastStationUnlockPatch.cs` (a prefix that skips
  the game's own unlock). Wired into `ApRuntime`.

The design rests on three decompiled facts, all written up in
[`apworld/protocol.md`](apworld/protocol.md) §11. The one that matters most
for testing: **the unlock lives in RAM only** — nothing writes it to the
save, and nothing can turn a station off. So a granted station has to be
re-applied to every world that loads, which is the part most likely to be
wrong.

### The test, in order

Run a room with `tools/testroom.py`, then in game:

1. **Ctrl+B first** (`Debug.Enabled = true` required). It prints the dial,
   every loaded `BroadcastStation` with the `SavableSystem` its peck system
   writes, and the ledger. This is what answers **the one open question**:
   is `FmRadioManager.stationTrackGroups` ordered like the `FmStation*`
   enum? The mod prefers the `BroadcastStation` pairing, which is
   authoritative, and falls back on that ordering when no tower is loaded.
   If the two columns disagree, the fallback has to go and the mapping has
   to be learned and cached in the save instead.
2. **Switch a station on.** Expected: the check goes out, the music does
   **not** start, and the log says `… switched on; its music waits for the
   Archipelago item`.
3. **`/send <slot> Radio Music: Bobby`** from the server console.
   Expected: the music starts, wherever you are standing.
4. **Quit to the menu and host the same save again.** Expected: the station
   is still playing. This is the RAM-only fact being exercised —
   `RearmFromLedger` on the world becoming ready.
5. **Restart the process and reconnect.** Expected: still playing. Different
   code path from 4: the server replays the item but the cursor skips it, so
   the second `RearmFromLedger` call — the one in `OnJustConnected` — is the
   only thing that puts it back.
6. **With a second player**, confirm the accepted wrinkle rather than being
   surprised by it: the guest hears a station as soon as it is switched on,
   because only the host runs an Archipelago client. Documented in the
   option text and in `design-decisions.md`.

### While you are in there: Ctrl+K, twice

Thirty seconds, and it unblocks the whole next feature. Press it once near
the **drawbridge** and once near a **coloured tower**, and read off:

- how many segments each `KeyBlank` has, and which towers have one at all
  (the player recalls the drawbridge plus the four coloured towers, four or
  five holes; `PropGroup` hints the black key may be cut too);
- whether each plinth's `pinGroup` is the `Complete` variant — which decides
  whether skipping `RefreshPropGroup` is suppression enough;
- what `TrackedPeckState` each plinth drives, and whether it carries a
  `savableSystem` of its own (expected: it does not, which is what forces a
  mod-side ledger).

The design these answer is written up in `apworld/design-decisions.md`
("LEAD UNDER DESIGN, 2026-09-21") and the mechanism in
`mod/reverse-engineering-notes.md`.

## Then: the release

Nothing else is known to be missing. `tools/package-mod.ps1` already
produces the players' zip, the debug module is off by default
(`Debug.Enabled = false`), and `apworld/build.py` packages the world.

One decision left, and it is yours: **the version numbers**. Everything
currently says 0.1.0 — `Plugin.PluginVersion`, the `.csproj`, and both
`archipelago.json` and `WORLD_VERSION`. Nothing has shipped publicly yet, so
0.1.0 as the first release is coherent; the alternative is calling the radio
work a 0.2.0. The mod only *warns* on a `world_version` mismatch, so the two
do not have to move together, but they have so far.

## After that: big keys as forage checks

Designed on 2026-09-21, nothing written yet beyond the Ctrl+K dump. The full
reasoning is in `apworld/design-decisions.md`; in four lines:

- **Locations** = cutting each segment (postfix on
  `KeyBlank.ServerCutSegment`), roughly 20–25 of them.
- **Suppression** = prefix on `KeyBlank.RefreshPropGroup` while the item is
  missing, so an uncut key cannot enter its plinth.
- **Item** = the *feature* unlock (map room, chairlift, train, tunnels) via
  the plinth's `TrackedPeckState` plus an `ap_*` ledger re-applied on every
  world — the shape `Core/RadioStations.cs` now has.
- **Never** anchor a location on "this tower's monument is full": that is
  Option C, and the arithmetic makes every key cost the whole gourd pool.

It removes the §8 self-report quirk and the key item's redundancy. It is also
a content change big enough to deserve its own test cycle — after a release,
not before.

## The idea raised on 2026-09-21: other objects as items

The question was whether the flare guns, walkie-talkies and other objects
lying around the island could become real filler, instead of the inert
Postcard / Souvenir Pebble / Novelty Keychain. What the dump says:

- **There is no class for them.** No `FlareGun`, no `WalkieTalkie`.
  (`FlareDriver` is a lens-flare renderer, nothing to do with it.) They are
  plain `Prop` prefabs, told apart by their prefab and by what their
  `useHeldSwitch` is wired to — so `il2cpp.cs` cannot enumerate them. Only
  an in-game dump can.
- **The walkie-talkie is identifiable**: `Prop.radioVoiceAssigner` is a
  first-class field on `Prop`, so any prop carrying one is a walkie-talkie.
- **They have no `SaveablePropName`.** That enum holds only gourds, big keys,
  valets and dev-test values. So there is no "you have unlocked the flare
  gun" flag anywhere, and nothing to key a *location* on the way the puzzles
  are keyed.
- **But there is a per-prop identity**: `Prop.savablePropGuid`, with
  `canSaveHomeWithGuid`, `SaveData.inventory` (a `List<string>`) and
  `SaveManager.GetIsInInventory(guid)`. That is how the game remembers which
  props are in the hub's inventory zone — a stable id per prop, which is
  exactly what a location would need.

So, two very different amounts of work:

- **As filler items — cheap and plausible.** Receiving one spawns a copy,
  using the same clone-an-existing-instance technique
  `ReceivedItemSpawner` already uses for gourds. Same constraint as gourds:
  an instance has to be loaded to clone from. Copies would accumulate, which
  for a party object is arguably the point.
- **As checks — a project of its own.** It needs the guids enumerated in
  game, a decision about what "found it" means for an object that is simply
  lying there, and a way to remove the original so the check is not free.
  Not before a release.

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

- **Is the radio dial ordered like the enum?** See the test above. It is the
  one assumption the radio feature makes that the binary could not settle.
- **Does the tutorial drawbridge really gate the way out?** The world
  precollects the Tutorial Key on that assumption
  (`start_with_tutorial_key`). If it is wrong, the key can be shuffled.
- **Are all 58 puzzles reachable without any big key?** The region graph
  assumes the map is open apart from the ending.
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
- **Per-tower monument locations (Option C/D)**, the big-key decomposition,
  and Key Cutters as checks.
- **Props as checks** (flare guns, walkie-talkies) — see above.

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
