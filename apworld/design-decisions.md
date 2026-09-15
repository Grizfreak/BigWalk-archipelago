# Big Walk — Archipelago World Design Decisions

*Last updated: session of September 10, 2026 (source material); split into this file on September 15, 2026.*

This file collects decisions and open questions about what the Archipelago
world for *Big Walk* should look like: goal, items, locations, options,
softlock risk, and community design input. For how the game's internals
work and how the mod (`mod/`) hooks them — the mechanisms these decisions
rely on — see
[`../mod/reverse-engineering-notes.md`](../mod/reverse-engineering-notes.md).

## Status (2026-09-15) — the Python world is written

The world exists: `bigwalk/`, generation validated on Archipelago 0.6.7 and
0.6.8. What it actually implements, and the contract the mod must now meet, is
in [`protocol.md`](protocol.md) — that file supersedes this one wherever the
two disagree about the *current* design. This file stays what it has always
been: the record of how each decision was reached, and what is still open.

Decisions settled while writing the world, all of them as YAML options rather
than as fixed choices (the preference already stated on 2026-09-09):

- **Goal** → the `goal` option, `gauntlet` (default) / `ending` / `deposits`.
- **Monument deposits** → Option A confirmed. Big key requirements are
  cumulative in the logic, which follows from the point settled just below.
  (A `deposit_logic` option briefly existed to hedge the question; it was
  removed the same day once the answer was known.)
- **A deposited gourd cannot be retrieved — RESOLVED (2026-09-15, player,
  game knowledge)**: taking a gourd back out of a monument is simply not
  possible in the base game. Same category of fact as the big-key/plinth
  mapping: not decompilable, only knowable by someone who has played.
  Consequences, all of them now baked in:
  - Gourds are **spent**, not lent. A tower's key costs its own monument's
    slots *on top of* every monument already filled, so the logic sorts the
    towers by slot count and charges the running total (4, 9, 14, 19, 24, 30,
    45). The last key costs the entire pool by construction.
  - The looser "each key only costs its own monument" reading is not a
    trade-off, it is wrong, and would generate unbeatable seeds.
  - `CosmeticMonumentFillTracker.OnHomeChanged` still handles the unpin case
    (writing `ap_home_<slot> = 0`). Harmless and worth keeping as defensive
    code — but nothing in the world's logic may rely on it ever firing.
  - **Reopen if the mod ever adds a way to retrieve a deposited gourd**
    (nothing suggests it should): that would invalidate the cumulative
    requirements above, not just relax them.
- **Gourd pool size** → one `Gourd` item per monument slot in play (45, 36 or
  30 via `green_dome_deposits`), which is what the "enough generic gourds"
  constraint below asked for.
- **Radio stations** → locations, behind `radio_station_checks`.
- **Key Cutters, bells as separate checks, per-tower monument locations** →
  still not implemented, still recorded below as leads.

## Open design decisions (block writing the Python world, not the C# mod)

- **Goal** — **RESOLVED (2026-09-15)**: not a single choice but the `goal`
  YAML option, exactly as the 2026-09-09 Jack5 reply suggested it need not be
  exclusive. `gauntlet` (default, `GauntletComplete`), `ending` (`EndingGate`)
  and `deposits` (N gourds deposited, aggregated). The first two need
  `SaveValuePatch` widened to `SavableSystem`; the third needs no new
  detection at all. Combining several goals with AND/OR was considered and
  left out of the alpha.
- **Gourd deposit box model** — likely a YAML option (global progressive /
  sequential per tower / per specific slot with no critical item), to be
  chosen while designing the Python world.
  - **SOFTLOCK RISK IDENTIFIED (2026-09-11, player's question), to be
    settled before using monuments as AP locations.** Now that monument
    filling is implemented mod-side
    (`mod/src/Core/CosmeticMonumentFillTracker.cs` — see
    `mod/reverse-engineering-notes.md` for the mechanism): a received
    cosmetic gourd is **generic and freely placeable in ANY slot of ANY
    monument** (the "gourds = generic fillers" decision of 2026-09-09, see
    the dedicated section below — nothing mod-side prevents or directs
    where a player deposits a gourd). If the AP world's logic ends up
    depending on **filling a specific tower precisely** (e.g. "5 gourds
    must be deposited in the Red Tower's monument to unlock
    `bigKeyRedZone`"), a player could — by mistake or by choice — deposit
    all their gourds into a SINGLE monument (e.g. the Red Tower) instead of
    spreading them out, making the requirements of the OTHER towers
    **impossible to satisfy**: a real logic softlock, not just a cosmetic
    problem. Nothing in the game or the mod physically prevents this bad
    distribution — unlike the real vanilla deposit box, which only accepts
    gourds/valets of its own category by construction (level design
    guarantees correct distribution), a post-AP cosmetic gourd no longer
    has this physical constraint once generated.
    - **Option A — no per-monument/per-tower locations, a single aggregated
      global count** (reprises the "option 1" already noted on 08/23: "a
      global progressive counter, across all monuments"). Structurally
      eliminates the risk: the logic never depends on WHERE a gourd is
      deposited, only on the cumulative total — no matter how the player
      distributes their gourds among monuments, no per-tower requirement
      can ever become unsatisfiable.
      `mod/src/Core/CosmeticMonumentFillTracker.cs` already provides all the
      needed data (an `ap_home_<slot>` key per filled slot).
      **RESOLVED (2026-09-15)**:
      `CosmeticMonumentFillTracker.GetFilledMonumentCount()` added —
      iterates the already-known monument `PropHome`s (`RegisterHome`) and
      counts those whose `ap_home_<slot> == 1`, queried on the fly (no
      cached counter, to stay a single source of truth alongside
      `OnHomeChanged`/`TryRestoreHome`). Build validated. No caller wired to
      it yet (waiting on the AP network client to know how/when to report
      the threshold check).
    - **Option B — no monument location at all** (monument filling stays a
      purely cosmetic/comfort feature, no check is ever reported there).
      Also eliminates the risk, radically: AP logic then depends ONLY on
      item receipt (already entirely controlled by the AP server, so
      deterministic and never subject to a local player placement choice).
      Directly connects with the player's idea ("gourds could be progressive
      items") — if what matters for logic is **how many gourds have been
      received** (an increasing count, in the standard Archipelago
      "Progressive Item" style), not where they physically landed, then
      monument filling only needs to exist for the feel/immersion
      (satisfying to watch fill up), never as a real progression mechanism.
      This is the safest and simplest option to reason about, but loses the
      thematic link "each tower unlocks by filling ITS monument".
    - **Option C — per-slot/per-tower locations kept, but then placement must
      be constrained** to avoid the softlock — e.g. a cosmetic gourd "knows"
      which monument it's meant for (back to a form of per-gourd identity,
      which recreates exactly the capacity/collision problem already
      discarded on 2026-09-11 for persistence — see the dedicated entry in
      `mod/reverse-engineering-notes.md`, "gourdTesting00-39 too small
      against ~45 slots") or else the mod must actively **refuse/undo** a
      deposit into the wrong monument (a new gameplay constraint absent from
      the vanilla game, could feel arbitrary/frustrating to the player). The
      only one of the three options that preserves the feeling "each tower
      has its own collection challenge", but the most complex and riskiest
      to implement correctly.
    - **DECISION SETTLED (2026-09-15, player) — Option A adopted for the
      alpha.** Aggregated global count (across all monuments) for the
      first playable version of the Python world: eliminates the softlock
      risk with no extra mod-side code
      (`CosmeticMonumentFillTracker` already provides the data). **Option D
      kept in mind, not abandoned** — explicitly set aside for a future use
      beyond the alpha (probably a V2 with a stronger per-tower thematic
      link), not because of this specific softlock risk since A already
      solves it for the alpha. Do not start work on locating tower entrance
      doors (see Option D below) until the alpha has shipped.
    - **Option D — new idea from the player (2026-09-11): move the lock onto
      the tower's ENTRANCE, not the monument.** Each tower has an entrance
      door, currently opened freely by a button in front of it (visible
      in-game, mechanism not yet precisely located mod-side — probably a
      variant of `HubGate`/`GateMainSystem`, the same kind of
      `TrackedPeckState`+button+door-pieces already solved for the hub
      shortcuts, see `mod/src/Core/ArchDoorUnlocker.cs` and the "Arch doors"
      section of `mod/reverse-engineering-notes.md`). Idea: remove/disable
      this button and only open the door via the mod, once a dedicated AP
      item is received (e.g. a "Red Tower Access" item separate from that
      tower's big key, which stays the end-of-tower reward as it is today)
      — exactly the same already-proven, safe scheme as
      `ArchDoorUnlocker`/big keys (`ItemApplier.TryApplyLiveEffect`), not a
      new mechanic.
      - **Direct implication on the softlock risk above: the problem
        disappears, it no longer needs solving.** If access to a tower
        depends on a received item (entirely controlled by the AP server,
        deterministic, never subject to a local player placement choice)
        rather than on filling ITS monument, progression no longer ever
        depends on where/how gourds are distributed. This effectively lands
        on the same conclusion as Option B (monument filling = pure
        cosmetic/comfort, no check reported there) but moves the lock
        elsewhere (the door) rather than simply giving up a per-monument
        lock.
      - **Technical work required, not yet done**: locate in-game each
        tower's entrance mechanism (F9/F7 in front of each door, same
        method as for `HubGate`) — probably 4 to 6 similar mechanisms to
        identify one by one, but a scheme already solved once, not a new
        investigation from scratch.
      - **Decisions still to make**: which item triggers each door's
        opening (a new dedicated "Tower X Access" item per tower, the
        cleanest and closest to the player's idea, vs. reusing an existing
        item); disable the button (`SetActive(false)`, same philosophy as
        `SecondEndingSphereUnlocker`) or remove it more radically. Not yet
        investigated or decided — just noted as the currently most
        promising lead to avoid the softlock risk above.
    - **DECISION (2026-09-15) — recovery from a physical softlock via a new
      save + AP reconnect replay, monuments unaffected as long as Option A
      holds.** If a player ends up physically softlocked (an unreachable
      place), the solution is to start a new save and reconnect to the SAME
      AP slot: the server keeps the full history of items already received
      by that slot (not by local save), so a reconnect must replay all of
      them (not just future new items) — to be built into the network
      client's design from the start, not added afterward. Idempotent by
      nature: replaying a big key just rewrites the same `SaveManager` key/
      re-pins the same prop (no side effect), and re-reporting an
      already-validated check is a no-op server-side (the AP protocol is
      designed for this). **Consequence for monuments accepted as-is**:
      replayed cosmetic gourds reappear at the hub
      (`ReceivedItemSpawner.SpawnCosmeticPickup`) but do not automatically
      refill `ap_home_<slot>` — the player must redeposit them by hand.
      Judged inconsequential as long as Option A holds (monument slots are
      interchangeable, only the aggregated total matters) — **to
      reconsider if the model ever switches to Option C/D** (unique
      per-tower slots), where a manual redeposit after a reset would
      recreate exactly the logic softlock this reset mechanism is meant to
      avoid (fill-in state would then need to be saved/restored outside the
      save file, keyed by AP slot).
    - **LEAD, NOT IMPLEMENTED (2026-09-15) — restore deposits through
      Archipelago's DataStorage.** Confirmed in-game that a reload of the
      *same* save does put deposited gourds back in their slots
      (`ap_home_<slot>`, `CosmeticMonumentFillTracker`); what a new save
      cannot recover is that those keys live in the save file that was just
      abandoned. The server knows what the slot *received*, never what was
      done with it.
      - The client library exposes `ArchipelagoSession.DataStorage`, a
        server-side key/value store scoped to the slot, which by definition
        survives starting a new save. Writing the deposited count there
        would let a fresh save re-pin that many gourds — and under Option A
        *which* monuments they go into is irrelevant, so only the count is
        needed. `ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo` already
        does the pinning.
      - **Deliberately deferred**: it moves part of the game state out of
        the save file, which raises a question with no obvious answer —
        what to do when the local `ap_home_*` keys and the server's count
        disagree (two machines, a restored backup, a save copied by hand).
        Under Option A the cost of not having it is a few minutes of
        walking, so the trade is not worth making blind.
      - **Reconsider first** if Option C/D lands (the entry above already
        says this state would then have to leave the save file), or if
        players hit the new-save recovery path often enough for the manual
        redeposit to grate.
- **NEW LEAD, NOT SETTLED (2026-09-11, player) — decompose the big-key
  system into several checks decoupled from the real game effects they
  trigger, same principle as the location/item decoupling already done for
  gourds.** Idea: replace the single "key placed in plinth" check with a
  multi-step chain:
  1. **Filling a tower's monument** (N gourds deposited, see Option A/the
     global count already discussed above) → a single check for the tower
     (or no check at all, to be decided) — decoupled from the key itself.
  2. **The key becomes a progressive item** (same logic as gourds for
     monuments), rather than a unique 1:1 item per tower as today.
  3. **Cutting the key, 5 times** (`Key Cutters` — decided not to explore on
     2026-09-XX as a separate check, see the "Explicitly set aside" section
     below; **to reconsider** if this lead moves forward) → 5 distinct
     checks/locations.
  4. **Placing the cut key in its plinth** → one more check (the 6th in the
     chain).
  - **Central tension, explicitly unresolved (the player themselves isn't
    sure it's feasible)**: the real game effects currently triggered by the
    plinth pin (unlocking the train, zone access, etc., via
    `TryApplyLiveEffect`/the generic `PropHome` pin — see
    `mod/reverse-engineering-notes.md`) must keep working for the world to
    stay playable — but if the check chain above is decoupled from AP
    progression (as for gourds), the player must still be able to
    *physically* trigger these game effects (placing a key in a plinth)
    independently of AP logic, exactly as a gourd puzzle stays physically
    solvable regardless of received items.
  - **Not yet investigated**: whether the game persists an intermediate
    cutting state per key (no known granularity below "whole key placed or
    not" to date — `SaveablePropName`/`SaveableHomeName` of big keys only
    show a binary per-key state, never checked for a notion of "cuts
    remaining"); whether the game effects (train, etc.) can be triggered by
    a physical key placement without depending on whether the corresponding
    AP item has already been received by that player. No Ghidra
    decompilation nor in-game test done on these two points.
  - **Current state, nothing broken**: the existing 1:1 model
    (`ItemApplier.ApplyGourdItem` for `bigKeyXxx`, see the correction in
    `mod/reverse-engineering-notes.md`) stays in place as-is for now — this
    entry documents a future improvement lead to explore, not a decision
    made.
- **Implementation of the last pre-ending zone, right before GoodbyeVoid
  (2026-09-10)** — probably `GoodbyeChapel` (see `Enviro.LobbyLighting.
  AreaType`, right before `GoodbyeVoid` in the enum); still to define how
  this zone fits into the Archipelago model (final goal vs. simple
  intermediate step toward `bigKeyPlinthGoodbye2`/`bigKeyOverflow`, see the
  two bells discussed below which might be tied to it). Not yet
  investigated technically — to examine during the bell test session, once
  on site.

## Explicitly set aside

- **Key Cutters** — decided not to explore, an uninteresting intermediate
  step as a separate check. **To reconsider (2026-09-11)** if the big-key
  decomposition lead (5 cuts + 1 placement) above moves forward.

(`lock_map_room` and the Poet&Priest/Poet&Pontiff doors are an unidentified
mod mechanism, not an apworld design question — see
`mod/reverse-engineering-notes.md`.)

## History — initial big-key investigation: decoupling the check from the key's usage

*(See `mod/reverse-engineering-notes.md`'s "History — initial big-key
investigation" section for the full reverse-engineering narrative this idea
grew out of.)*

A key is unlocked with a number of associated gourds (pins) and unlocks a
game feature. Player's idea (2026-09-07): treat separately (a) the
**check** "the key has been used/placed" and (b) the **item** "we own the
key" — same decoupling philosophy as for gourds (see the "Locations/items
model" decision below), not yet applied to big keys at the time. This idea
directly foreshadows the later (2026-09-11) "decompose big keys into
decoupled checks" lead documented above, which is the current, more
developed version of this same question.

## Idea noted for later — AP server config in the hosting menu

*(Implemented — see `mod/reverse-engineering-notes.md` for the full
mod-side implementation, "Idea noted for later — AP server config in the
hosting menu" section: `Core/ApSessionConfig.cs`, `HostMenuConfirmPatch.cs`,
the `[Archipelago]` `.cfg` section. That work exposes a slot name, an AP
password, and a host:port field on the in-game hosting screen for the
future network client to read.)*

## Open point — the location/item logic deserves rethinking

*(See `mod/reverse-engineering-notes.md` for the mod-side bug this
discussion surfaced and fixed: `ItemApplier` was marking a location
"already reported" without ever calling `Plugin.Reporter.ReportCheck`,
silently losing that location's outgoing item in the multiworld. Fixed by
2026-09-07.)*

The real, unresolved design question (raised 2026-09-07): `GourdRegistry`
uses the **same identifier** (`propName.ToString()`, e.g.
`"bigKeyRedZone"`) both as a **location** id (what the player accomplishes
in their own world) and as an **item** id (what `ItemApplier` materializes
when received). In a real Archipelago multiworld, these are two independent
spaces: this player's "bigKeyRedZone" location could contain any item from
the multiworld (not necessarily "bigKeyRedZone"), and the "bigKeyRedZone"
item this player receives can come from any location, from anyone. Merging
them as currently done is only correct if the Big Walk Archipelago world is
designed as an "in-place shuffle" (each named slot is both its own location
and its own item, only the link between the two is shuffled) rather than a
true multiworld with decoupled item/location spaces.

**Why this isn't just an arbitrary choice to settle easily**: a physical
game constraint, already well documented in `mod/reverse-engineering-notes.md`
— a single `PropHome`/save slot per `SaveablePropName`, reused both to
detect local resolution AND to materialize a received item (no generic
inventory mechanism found that triggers the game's real unlock logic, see
the "Design adopted"/discarded-leads section there). Result: once an item is
materialized, the physical object is consumed (empty clamp / key already
placed) — the player can no longer ever "really" resolve that location
themselves afterward anyway. So fully decoupling location and item (in the
true-multiworld sense) would require finding a materialization mechanism
that doesn't consume the same slot as check detection — and that lead has
already been explored and abandoned (see the discarded-leads section in
`mod/reverse-engineering-notes.md`).

**To reopen later**: is the "each named slot = its own location AND its own
item, only the link is randomized" model acceptable for this Archipelago
world, or is a true decoupling required? This depends mostly on how the
(not yet written) Python world will define locations/items — not purely a
mod-side question.

*(This question is picked up, discussed further, and ultimately resolved
below — see "DECISION SETTLED (2026-09-09) — final location/item model,
gourds vs. big keys".)*

## Regions, towers, and the locations/items model — investigation of 2026-09-07

Follow-up to the "open point" above, triggered by the same question.
Summary of everything discovered/discussed — none of this is implemented
mod-side yet (only the existing puzzle and key checks are), this is
preparation for designing the Python world.

*(For the mod-side debug tools used to gather this data — F5/F6 hotkeys in
`Debug/DebugGourdLookup.cs` — and the reference tables of monument slot
counts and gourd/valet naming, see `mod/reverse-engineering-notes.md`'s
"Regions, towers and locations/items model" section.)*

**Étau (clamp) vs. valet vs. monument — mechanics summary**: three distinct
stages exist in a gourd's lifecycle — the clamp holding it during the
puzzle, an automatic "valet" fallback position written the instant the
puzzle is solved (not a separate step, not the "two players" stage), and
the monument/trophy stash (`GourdState.Stashed`) which genuinely requires
two players to coordinate. See `mod/reverse-engineering-notes.md` for the
full mechanical detail (log evidence, `PropHomeBlock`/`GourdPinAudioBehaviour`
decompilation). This directly informs the design question below: since
`valetXxx` is written automatically at puzzle-resolution time, a
`PropHomeBlock` grouping specific `valetXxx` values would fill up simply by
solving the right puzzles, independent of the monument-stash step — which
would be a separate progression track (a trophy room), not a prerequisite
for unlocking a tower.

### Community feedback (third-party message via Discord, not verified in-game)

Someone who apparently already worked on an Archipelago world for Big Walk
(the "Manual" framework) shared their approach:
- Did not make the item blocking the tutorial's exit (probably
  `bigKeyIntro`) a randomized AP item — combined with door randomization,
  the logic would become inexpressible in Manual ("just doesn't work in a
  manual"). Worth keeping in mind if doors are ever randomized.
- To unlock towers: assigning **regions** to gourds — specific gourds must
  be brought to the blue tower, others to the red one, etc., rather than a
  generic "any 24 gourds" counter. Confirms/refines the `PropHomeBlock`
  hypothesis (a fixed group of homes per tower, not a generic total) found
  via Ghidra (see `mod/reverse-engineering-notes.md`).
- Cosmetic suggestion: recolor AP-shuffled gourds "purple gourd" style
  (a variant that already exists in the game) to visually distinguish them
  from vanilla gourds.

### Open point — location granularity (puzzles / pins / keys)

Question raised during the session: could Archipelago locations be (a) the
puzzles, (b) the gourd-to-valet pins, and (c) the key placements, as three
separate categories?
- (a) and (c) are already what's detected today (`Loose` for gourds via
  `GourdStatePatch`, peck-toward-plinth for keys for lack of an alternative
  since they have no `RewardGourd`/`GourdState`).
- (b) is actually **redundant with (a)** once the clamp/valet/monument
  clarification above is taken into account: the valet is written
  automatically at the moment of resolution, so (a) and (b) always coincide
  in time — not a real temporal distinction, contrary to what was thought
  before cross-referencing with the 2026-09-03 notes.
- The real second, distinct tier would rather be the **monument stash**
  (requires two players, separated in time from resolution) — but it isn't
  specific to one particular gourd (generic shared slots), so modeling it
  as a location would require a different approach ("a gourd was stashed at
  monument N slot M", not "gourdX was stashed").
- The world isn't meant to be playable solo (confirmed by the player) — so
  the two-player constraint for stashing isn't a design problem to avoid,
  just something to keep in mind for the Python world's accessibility logic
  (a stash location would never be reachable solo, but that's not an issue
  if the world assumes multiplayer).

Nothing of this is implemented mod-side for now — to reopen when the Python
world is designed.

**Discord thread follow-up, "Big Walk" (2026-08-23, third party, topic "How
to deal with gourd deposit boxes")** — directly picks up this open point,
three design options proposed for the "gourd deposit boxes" (= the monument
stash above):

1. **Progressive, a single location type**: all monuments contribute to a
   **single** global progressive location (e.g. "N gourds deposited in
   total, across all towers"), **no** tower-specific completion reward.
2. **Non-progressive, sequential unlock**: only one tower "active" at a
   time — the next tower only unlocks once **all** gourds are deposited in
   the current tower.
3. **Non-progressive, but with no critical item inside**: each deposit box
   is a location like any other (independent per tower/slot), but the world
   excludes critical progression items from being placeable there — avoids
   a multiworld-blocking item depending on a late, two-player-specific
   accomplishment.

**DECISION (2026-09-09)**: not locked into a single option — the player
points out that this choice should rather be a **YAML option of the AP
world** (same family as `lock_puzzles`/`lock_pickups` already seen in the
third-party doc), not something fixed once and for all mod-side.
Implementation consequence: the mod will need to receive this setting from
the AP world/slot (on connection) and adapt its detection behavior
accordingly, rather than hardcoding a single model. Practical priority
suggested by already-known technical feasibility: **option 1** (global
progressive counter) is the simplest to detect right now —
`GourdPinAudioBehaviour.GetNumberOfFilledHomes()` (already spotted via
Ghidra, see `mod/reverse-engineering-notes.md`) gives this count directly,
no further investigation needed. Options 2 and 3 remain valid as
alternative settings to support later, option 3 in particular requiring
verification of whether the game exposes a way to tell WHICH specific slot
was filled (not confirmed).

*(This 2026-08-23/2026-09-09 discussion is what later crystallizes into the
formal Option A/B/C/D softlock-risk analysis and the 2026-09-15 "Option A
for the alpha" decision documented at the top of this file — option 1 here
== Option A there.)*

### On the appearance of the received gourd item

*(For the technical how-to — `InventorySpawn.GetNextSpawnPosition()`,
cloning a `RewardGourd`/`Prop`, and the `saveablePropName` collision risk
to neutralize — see `mod/reverse-engineering-notes.md`'s `ReceivedItemSpawner`
section, which fully implements this.)*

A third party recommends that the materialized gourd be both **visible/
spawned in front of the player** AND a portable **inventory object** — not
just an invisible write as `ItemApplier` did originally.

**DECISION (2026-09-09)**: spawn at the **spawn zone/hub** (not dropped in
front of the player wherever they are in the world) — settled by the
player. This is consistent with the same day's "gourds = generic fillers"
decision (see below): a received generic filler item can logically appear
as a physical, pickable object at the hub, rather than remaining a pure
`SaveManager` abstraction.

## Third-party document — "Big Walk Archipelago details.pdf" + Discord thread (2026-09-07)

The player shared an apworld design document (YAML options, item/location
list, custom tile guide) and excerpts from a Discord thread, very likely
from the same third-party author ("trinity") mentioned above ("Community
feedback"). Neither the document nor the thread are affiliated with this
mod — but they strongly overlap with (and sometimes correct) what was
deduced today through pure reverse-engineering, on top of bringing new
information. Everything below comes from third parties, not verified by us
in-game, unless stated otherwise.

*(The big-key/tower/plinth cross-validation mapping table from this
document — used to validate `GourdRegistry.BigKeyHomesByProp` — is mod
reference data; see `mod/reverse-engineering-notes.md`.)*

### New: 45 puzzles at launch, 58 today

The Discord thread dates from about a week after the game's release ("only
having released a few days ago", posts dated 08/07-08/2026; game released
August 4, 2026 per our notes). The doc and the thread talk about **45
puzzles total**, with a goal of "any 30 of 45". Comparing the document's
list of 45 puzzles with our current 58 `gourdXxx` enum values, **13 gourds
present today are absent from the document's list**
(`gourdBunker`, `gourdHighPegBoard`, `gourdFirstPegBoard`,
`gourdMagiciansTrick`, `gourdButtonBoothChallenge`, `gourdTileSoup`,
`gourdPanopticon`, `gourdMaypole`, `gourdBlindfoldCircus`,
`gourdMessengerRun`, `gourdHotPotato`, `gourdScoutTiles`,
`gourdScoutCounting`). Most likely hypothesis: **the game received a
content update adding 13 puzzles since release**, between this document's
writing (~August 2026) and today (2026-09-07). Worth keeping in mind if
this document or a future version of the Python world relies on the old
total of 45 — the real current total is 58.

### Terminology confirmed independently

A remark from the same author, inspecting the game's files on their own
side: *"the name for the puzzle reward object... is internally called
'Gourd'"* — confirms, from a source completely independent of our Ghidra
decompilation, that `SaveablePropName.gourdXxx` is indeed the exact
internal structure, not a naming coincidence on our part.

### The problem they hit, that we avoided by design

Very relevant excerpts from the thread (paraphrased) — the author spent at
least a month (posts from 08/07 to 08/18/2026) stuck on exactly the problem
solved this session, but approached differently:
- Initial approach considered: **preventing the "gourd clamp" from
  unclamping until an AP item is received**, or being able to unclamp it at
  will via code. Never achieved: *"I haven't figured out how to accomplish
  stopping the gourd clamp from unclamping on puzzle solution, or being
  able to arbitrarily unclamp the gourd whenever I need to"*.
- Additional complication noted: **some puzzles have no clamp at all** (the
  gourd is already "open" with no clamp), e.g. the tutorial's "Telescope to
  Box" puzzle — so a clamp-blocking approach wouldn't work uniformly across
  all puzzles anyway.
- Pivot suggested by another participant (Kimtroverted): **lock the
  *deposit zones* (the monuments/valets) instead of the clamp itself**.
  Adopted ("that might just be better yeah"), but with an acknowledged
  cost: it turns "any 30 gourds out of 45" into "these 30 specific deposit
  slots out of 45" — a loss of pool flexibility.
- List of unresolved problems they identified with this last approach:
  excessive backtracking (going to collect an already-resolved gourd then
  bring it back), it encourages splitting from the group (to go fetch
  gourds) when puzzles often require staying together, state-tracking
  complexity (resolved? picked up? deposited?), more programming work (not
  all puzzles have a clamp to affect), exploitable via the game's "lost
  items" feature, and altogether tedious to play.

**Comparison with our design**: our approach ("pure `SaveManager` write,
never touch the live object", see `mod/reverse-engineering-notes.md`'s
"Design adopted" section) structurally avoids almost all of these problems
— no need to lock or unlock the clamp (we never touch it), works uniformly
even for clampless puzzles (we just write the expected final value into
`SaveManager`, regardless of the physical resolution mechanism), no
excessive backtracking nor forced group splitting
(`Prop.Start()`/the live pin do the work automatically). A good indirect
confirmation that the design adopted this session (through a completely
different path: empirical testing + Ghidra, no coordination with this
Discord thread before writing it up) is solid.

### Checks/items model proposed by this third party (to compare with ours)

- **Checks**: puzzle completed, key obtained from a tower, radio station
  found.
- **Items**: *unlock* of a puzzle's reward (not the reward itself — a
  generic item "you can now solve/collect this type of puzzle"), key
  unlock (except for the last tower, kept as a goal rather than
  progression), radio station activation.

Notable difference from our current implementation: in this model, the
received item isn't literally "gourd X" but an **indirect unlock**
(consistent with the `lock_puzzles`/`lock_pickups` YAML options:
`individual` = one unlock item per object type, `all` = a single generic
item that unlocks everything, `disabled` = everything available from the
start). Our mod currently does the opposite: the received item IS directly
`gourdX`/`bigKeyX` (materialized via a `SaveManager` write), with no notion
of a separate prior "unlock". Neither approach is settled as the right one
for our world — to cross-reference with the "Open point — location/item
logic" discussion above once the Python world is designed.

*(This comparison is superseded by the "DECISION SETTLED" section below,
which resolves it for gourds vs. big keys.)*

### New community feedback (Discord, 2026-09-09) — refines the checks/items model, mentions "bells"

New reply from Jack5 (the same third-party author already cited, shared
Google doc: "Big Walk Archipelago details", same content as the PDF
already referenced above) following a direct question from the player
("how to handle goal/items/locations, and gourds — locations for vessel or
to remove entirely?"):

- **Goal**: "both or either a certain number of each collectible and
  specific endings or areas to reach" — a number of collectibles
  (gourds/keys) AND/OR reaching specific endings/areas. Directly
  cross-references our "Open point — Goal" above (standard ending vs. N
  towers vs. other): this reply suggests it's not exclusive, an AP world
  can combine both approaches depending on YAML options.
- **Items**: character abilities (many tied to being able to carry certain
  object types), inventory objects (spawn), **arch doors**, **towers**,
  keys, traps, and **fillers** ("misnamed gourds" — generic/junk items
  with no 1:1 link to a real gourd location).
- **Locations**: first pickup of an object, resolving each puzzle, lighting
  up radio stations (confirms what was just decided, see "Autres pistes"
  below), depositing gourds and keys (the monument stash — see the
  clamp/valet/monument distinction above), and **destroying bells**.

**Important cross-reference**: "destroying bells" very likely corresponds
to the `bell` field in `PeckDevHelper.UnlockRules` (found during the Arch
doors investigation, see `mod/reverse-engineering-notes.md` — never
followed up until now, alongside `chairlift`/`train`/`tunnel`/`map`/
`gourd`). A concrete lead to dig into the day this category is tackled:
probably the same technique as the Arch doors (a dedicated
`TrackedPeckState` category in `SavableSystem`, to identify).

**Player's clarification (2026-09-10)**: two bells precisely in the ending
zone — one behind `bigKeyBoss` (Black Monolith), one further that could
count as goal completion. Both are triggered by **two buttons pressed
simultaneously** (so unplayable solo, requires 2 players) — consistent with
the `bell` field of `PeckDevHelper.UnlockRules`, but the real "simultaneous"
mechanism (probably a shared `PeckContext`/`PeckSystemReference` between two
`PeckSwitch`es, to verify) isn't located yet. (Both bells were subsequently
confirmed in-game — see `mod/reverse-engineering-notes.md`'s "black sphere"
narrative: the chapel bell resolves to `EndingGate`, and the Gauntlet's
final bell resolves to `GauntletComplete`, the real "goal completion"
check.)

## DECISION SETTLED (2026-09-09) — final location/item model, gourds vs. big keys

Definitively answers the "Open point" above:

- **Gourds — decoupled items (generic fillers)**: a "gourd" item received
  via Archipelago is **no longer** tied to a specific `SaveablePropName` —
  it lands on **any** locally unresolved gourd (whichever one). Confirmed
  by the player: «of course generic gourds are needed to place in the
  monuments». **Locations**: unchanged, still 1:1 (each locally resolved
  gourd = its own precise location, as today).
- **World-design constraint flagged by the player** (Python-world side, not
  the C# mod): it will be necessary to ensure there are **enough generic
  gourds in the item pool** to satisfy the number of slots in the actually
  active monuments (see the F6 counts: `monoumentIntro`=4,
  `monoument0-3`=5 each, `monoumentFinal`=6, `monoumentOverflow`=15) —
  otherwise a player could end up with monuments that can never be filled
  for lack of enough gourd items in their pool. **Player's immediate
  nuance**: this constraint is judged low-risk in practice — the AP item
  pool doesn't need to be made *only* of generic gourds; traps and other
  effect-less items can fill the rest of the pool without breaking balance.
  Keep in mind when designing the world's YAML options, but not anticipated
  as a major blocker.
- **Big keys — stay 1:1**: each received big key remains precisely the one
  expected (`bigKeyRedZone` stays `bigKeyRedZone`), **no** filler treatment.
  Player's reasoning: «keys can be progression items so it seems normal to
  keep them 1:1» — a random key would be of no use if it isn't the one for
  the tower the player needs to progress in. No change from the current
  implementation (`ItemApplier.ApplyGourdItem(bigKeyXxx)` already 1:1).
- **Edge case — a filler gourd received while all local gourds are already
  resolved**: **silent no-op** (player's decision, no log even in debug
  mode) — the item is simply absorbed with no effect, like a classic AP
  filler already "maxed out".

**Concrete implication for the code** (not yet implemented, to do once the
network client exists or to prepare/test ahead of time):
`ItemApplier.ApplyGourdItem(SaveablePropName)` stays unchanged and continues
to serve as-is for big keys (1:1) and internally for gourds once a target
is chosen. A new function needs to be added, e.g.
`ItemApplier.ApplyGenericGourdFiller()`, which:
1. Scans gourds (not big keys) unresolved locally (same signal as
   `DebugGourdLookup.FindNearestUncollectedProp`, but filtered to only
   `gourdXxx` and with no "nearest" notion — any one works, materialization
   doesn't need proximity).
2. If one is found, calls `ApplyGourdItem` on it (reuses all existing
   plumbing: `SaveManager` write, live effect, check anti-duplicate guard).
3. If none is found (all already resolved): does nothing, silently.

The future Python world will therefore need to distinguish, item-side, a
generic `"Gourd"`-type item (count = N, where N depends on options) from
the named 1:1 items `"bigKeyRedZone"` etc. — to keep in mind when designing
the apworld's `item_table`/`location_table`.

**Status update (2026-09-15)**: this decision is **confirmed implemented**
in `mod/src/Core/ItemApplier.cs` — see the RESOLVED entry at the top of
`mod/reverse-engineering-notes.md`. `ApplyGourdItem()` no longer takes the
`SaveablePropName` parameter described above and no longer writes to
`SaveManager` at all for gourds: a later correction (2026-09-11) established
that a received gourd item must *never* validate a physical puzzle — it is
purely a cosmetic/monument-filling currency (`ReceivedItemSpawner.
SpawnCosmeticPickup()` only). This is a refinement of, not a reversal of,
the "gourds are generic fillers" decision above: gourds remain decoupled
generic items, but the mechanism for materializing them turned out to need
to be even more hands-off (no `SaveManager` write of any kind) than
originally planned here. `ApplyBigKeyItem(SaveablePropName)` keeps the 1:1
model exactly as decided above.

## Point to test next session: cooperative puzzles without a clamp

*(See `mod/reverse-engineering-notes.md` for the mod-side test plan for
`gourdTelescopeToBox` — this is an in-game test/validation task, not a
design decision.)*

## Goal (AP victory condition) — depends first on a design decision (2026-09-09)

No technical lead adopted yet, and deliberately not yet investigated:
before looking for *which game function/state represents "finished"*, the
**which** goal the AP world offers must first be decided — this isn't just
a technical question. Options mentioned: finishing a standard ending of the
game (which one — there may be several), or requiring N completed towers
(the 5-6 towers + tutorial), or some other variant. The choice completely
changes what to hook in-game (candidates already spotted if needed:
`EndingGate`, `GauntletComplete`, the 9 `monoumentFinalSlot0-8`) — so to
reopen once this design decision is made, not before.

*(The 2026-09-09 Jack5/Discord reply above suggests the goal need not be
exclusive — "both or either a certain number of each collectible and
specific endings or areas to reach" — i.e. a YAML option could combine a
collectible-count goal with a specific-ending goal.)*

## Other unexplored leads, noted for later

- **Key Cutters**: 5 per tower (25 total across the first 5 towers) —
  **decided not to explore** (2026-09-09), same decision as documented in
  "Explicitly set aside" above: just a mechanical intermediate step toward
  "key obtained/completed", not a distinct player accomplishment that would
  make sense as a separate check. Will not be turned into a check.
- **Radio stations** (`SavableSystem.FmStation*`, 10 entries in the enum) —
  decided (2026-09-09) as **checks/locations** (the player turns on the
  radio in-game → check reported), not as items to materialize. Judged
  easy: directly reuses `SaveValuePatch` mod-side (see
  `mod/reverse-engineering-notes.md`), which is already generic over
  `SaveManager.SetIntValue` — just needs widening its key recognition to
  also try `Enum.TryParse<SavableSystem>` (besides `SaveablePropName`
  already handled), no new detection plumbing to write.
- Full list of the game's "vanilla" items (abilities, carrying unlocks,
  inventory objects) given in the third-party document — useful as a
  reference if the mod ever needs to extend beyond gourds/big keys, and
  for designing a future item table for the apworld.

(`lock_map_room` and the `PoetAndPriestDoors`/`PoetAndPontiffDoors` are an
unidentified mod mechanism, not an apworld design question — see
`mod/reverse-engineering-notes.md`.)
