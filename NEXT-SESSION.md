# Where this stands, and what to do next

*Written at the end of the session of 2026-09-18. The detail lives in
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md) (how
the game works and what the mod does to it) and
[`apworld/design-decisions.md`](apworld/design-decisions.md) (why the world
is shaped the way it is); [`apworld/protocol.md`](apworld/protocol.md) is
the contract between the two. This file is the short version and the
to-do list.*

## The one thing left: a second co-op session

Everything that can be verified by one person has been. What remains needs
a second machine, and **only** a second machine — see "Why two local
instances do not work" below.

Before asking someone to sit down for it, make sure of three things, or the
session will answer the wrong questions:

1. **They have the current zip.** `tools/package-mod.ps1` produces
   `dist/BigWalkArchipelago-<version>-<date>.zip` in one command. A build
   older than 2026-09-18 10:20 still contains `CosmeticGourdAutoPickup`,
   which steals the host's gourds and reproduces a bug that is already
   fixed.
2. **Same game version on both sides.** The mod is compiled against a newer
   interop than 1.48 and resolves by name at runtime; a mismatch surfaces as
   MissingMethodException, not as a compile error.
3. **A room that is not goaled.** The server's auto-release checks every
   location, so a goaled room makes every measurement meaningless.
   `python tools/testroom.py --yaml coop-test.yaml` gives a fresh one on the
   slot name `CoopWalk`.

### T1 — Does the guest see a gourd in the HOST's hands?

Host's hands free, `/send CoopWalk Gourd` in the room console.

Expected: the guest sees it in the host's hands, and **no**
`PlayerNetworking.OnSetHeld` NullReferenceException or `OnDeserialize size
mismatch` in their log.

If the guest sees it in their OWN hands instead, they are running a build
older than 2026-09-18 — that was `CosmeticGourdAutoPickup`, since removed.

If the exception is still there, compare the two logs:
`Cosmetic gourd spawned on the network (netId N)` on the host against
`Cosmetic gourd built at (…)` on the guest. If the guest's line comes
first, the object is there in time and the problem is what it is missing,
not when it arrives — the one-frame deferral of the hand-over would then be
the wrong fix, and should be reconsidered rather than extended.

### T2 — Does the gourd fall, on the guest's screen?

Same spawn, watched by the guest. It must drop under gravity, not hang in
mid-air.

This is the client-side `Prop.SetLoose()` added on 2026-09-18 and **never
executed once**: the host never runs its own spawn handler (host mode
instantiates nothing), so the entire guest-side build path is unexercised.
Treat a failure here as new ground, not a regression.

### T3 — Does a gourd reach a player who is not the host?

Host's hands FULL, guest standing within 8m (`HandoverRadius`),
`/send CoopWalk Gourd`.

Expected on the host:
`Hands full, so the gourd went to another player 3.2m away.`
and the gourd in the guest's hands, seen by both.

**This is the only item whose answer is genuinely unknown.** The reasoning
is that `playerHeldInformation` is a SyncVar written by the server, so a
host-side `PickUp` on a remote player should replicate. That is a
reasoning, not an observation — and the symmetric reasoning about `Drop`
turned out to be wrong on 2026-09-15, probably because the object was being
unspawned in the same breath, which confounded that test.

If it fails, there is no channel left to ask the guest's own machine to do
it. A custom network message is closed to us (Il2CppInterop will not
marshal a delegate taking a non-blittable struct, which is what sank the
first spawn handler), and a client-side `PickUp` only convinces itself. The
honest move would be to drop the feature and let the gourd lie on the
ground, rather than keep a desync.

### T4 — Physics on release, guest side

The guest holds a gourd and drops it. Suspected to be a consequence of the
SyncVar stream desynchronising after the `OnSetHeld` exception in T1; if T1
passes and this still fails, it is a separate lead and needs its own
investigation.

## Done, and confirmed in-game

Solo, on the 2026-09-18 build: the startup burst lands at the spawn and
never in anyone's hands; a gourd received during play is handed over a
frame later; a sealed-box puzzle reports its check and the gourd leaves the
player's hands.

Co-op, 2026-09-15/16: the guest's mod registers the spawn handler and the
gourds now spawn for them; a gourd taken from a sealed box by the GUEST is
released from their hands by `StaleHeldPropReleaser`.

The `ending` goal, end to end: `EndingGate` goes **1 -> 2** when the chapel
opens, and that transition is what reports the goal. Its resting value is
already 1, which is why the old "first non-zero write" rule fired on the
first frame of every session.

The hosting screen works on game build 1.48, where `passwordRequired`,
`gameNameFlasher` and `InputWarningFlasher` do not exist.

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

- **Does the tutorial drawbridge really gate the way out?** The world
  precollects the Tutorial Key on that assumption
  (`start_with_tutorial_key`). If it is wrong, the key can be shuffled.
- **Are all 58 puzzles reachable without any big key?** The region graph
  assumes the map is open apart from the ending.
- **Do `FmStation7/8/9` exist at all?** Assumed not, and left out.
- **`ap_reported_*` is not scoped to a seed**, so a save reconnected to a
  *different* seed resends checks earned elsewhere. Harmless in real use,
  where a save belongs to one seed — but it will skew a count during
  testing. This is why `coop-test.yaml` uses a slot name of its own.
- **Radio stations** (seven locations) have still never fired once.

## Leads deliberately not taken

- **Monument fill state in Archipelago's `DataStorage`**, so a new save
  recovers its deposits instead of only its items.
- **A real in-world button for the gourd resync** instead of Ctrl+R.
- **Traps.** The item and the YAML option exist, the effect does not.
- **Per-tower monument locations (Option C/D)**, the big-key decomposition,
  and Key Cutters as checks.

## Lessons worth carrying over

**A successful build does not prove an edit was applied.** An edit script
failed an assertion and wrote nothing; `dotnet build` then compiled the
unchanged source and reported success, which read as confirmation. The
change was believed done for two days. Check the file, not the build status.

**A tool that says nothing when it finds nothing costs more than it saves.**
`DebugPeckCombinatorForce` returned silently when everything was out of
range, which is indistinguishable from a key that does not work — an hour
went into remapping keys for a problem that did not exist.

**Debug hotkeys collide across mods.** BigTV owns F7 through F11 and wins;
our tools bound to those keys were simply never reached. The numeric keypad
is free and proven to work here.

**When something only misbehaves for the second player, suspect authority
before suspecting the network.** Three separate bugs this weekend were the
same mistake in different clothes: a write performed on the machine that
does not own the state — `hands.Drop` host-side on a remote player,
`PickUp` client-side on oneself, and a scene-object clone spawned under an
assetId no client could resolve.
