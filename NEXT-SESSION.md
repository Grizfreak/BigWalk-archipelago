# Where this stands, and what to do next

*Written at the end of the session of 2026-09-21. The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is the short version and the
to-do list.*

**Start here: two features, both measured, neither built.** Everything that
was in flight has landed — the radio is an item and was exercised in game, and
the seed-breaking gate found in play on 2026-09-21 is fixed and verified. What
remains before a release is the work below, and it is implementation rather
than exploration: the hard questions were answered with dumps, and the answers
are in `mod/reverse-engineering-notes.md`.

**Before touching anything in game**: `tools/deploy-mod.ps1` builds and
deploys to every install, then prints one hash and says whether they match.
Three co-op tests were wasted in one weekend on a guest running older code.

**A lesson from 2026-09-21 that will save an hour**: the game instantiates
every gourd, every broadcast station and every key blank **everywhere**.
Presence tells you nothing about location — two dumps taken in two different
zones came back byte-identical. Every dump that asks "what is in this area"
must sort by distance to the player. Ctrl+V and Ctrl+B already do.

## Task 1 — big keys: forage checks, and an item that is the feature

Decided in full (`apworld/design-decisions.md`, "DECISION SETTLED (2026-09-21)
— big keys"). The model:

- **Locations**: the 25 cut segments, plus the 7 existing deposit locations.
- **Item**: the feature itself — map room, chairlift, train, tunnels,
  drawbridge, dam, Green Dome.
- **The key**: a check carrier. Placed in its receptacle it is inert.

Because placing is inert, **nothing about cutting or placing needs
suppressing**. The player does both freely and both are checks. The only thing
that must stop happening is the door opening when the key goes in.

Measured, so no longer in doubt: five segments each on the drawbridge and the
four coloured towers (25); the Black Monolith and Green Dome keys are born
finished and have none; every plinth's `pinGroup` is the `Complete` variant.
One trap recorded — pinning a key marks its blank complete, so a naive hook on
`ServerCutSegment` would fire the drawbridge's five checks at connection time.

**The one thing still unknown: what actually opens a door.** Three candidates
are eliminated (no `PropHomeBlock` on the plinths, no `onPin` PeckSwitch, no
switch anywhere keyed on a big key — see the notes). The lead to follow is
`PropHome.onPinServer` / `onChangeServer`, plain C# delegates that this
document has named as the plinth's effect point since September and that the
Ctrl+K dump never looked at, because it only read `PeckSwitch` fields.

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

One decision left, and it is yours: **the version numbers**. Everything says
0.1.0 — `Plugin.PluginVersion`, the `.csproj`, `archipelago.json` and
`WORLD_VERSION`. Nothing has shipped publicly, so 0.1.0 as the first release
is coherent; the alternative is calling this a 0.2.0.

Not done and worth one session if you want it before shipping: **the radio in
co-op**. The accepted wrinkle is that a guest hears a station as soon as it is
switched on, because only the host runs an Archipelago client. It is
documented in the option text; it has never been watched happening.

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
  now regions gated on their key. Two residual unknowns, both answered by the
  player rather than measured: four ordinary gourds in the same band of
  distance are reachable without the chairlift, and thirteen puzzles were
  never instantiated anywhere visited so their status was never read at all.
  If a seed ever turns out unbeatable, look there first.
- **Does the tutorial drawbridge really gate the way out?** The world
  precollects the Tutorial Key on that assumption
  (`start_with_tutorial_key`). If it is wrong, the key can be shuffled.
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
