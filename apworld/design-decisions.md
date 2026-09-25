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
- **Gourd pool size** → one `Gourd` item per monument slot (45; 30 was
  possible via `green_dome_deposits` until it was removed), which is what the "enough generic gourds"
  constraint below asked for.
- **`green_dome_deposits: limited` REMOVED (2026-09-15)**, after the player
  asked the right question about it. The option claimed the Green Dome's
  monument could be completed with 6 gourds instead of 15 — an idea taken
  from the third-party doc's `limit_green_dome_deposit_boxes`, which was a
  setting of *their* apworld, not of the game. Nothing mod-side makes it
  true: a monument is a `PropHomeBlock` that requires every one of its
  homes filled. So the option did not limit anything, it only told the
  logic to assume something false, and a seed could place a needed item in
  a deposit location the players could never physically reach.
  - Worth keeping in mind *why* it was harmless-looking: receiving a big
    key auto-reports its own deposit location, so that location stays
    checkable without ever filling the monument. The danger was narrow —
    it only bites when the item sitting there is one you need — but narrow
    is not safe.
  - Moot under Option A anyway, as the player pointed out: deposits are
    counted globally, so nothing ever requires filling one specific
    monument. `full` and `excluded` cover the two real needs (all seven
    towers, or skip the postgame grind).
- **`green_dome_deposits: key_only` ADDED (2026-09-22)**, as value 3 — value
  1 is left burnt where `limited` was, so a YAML written with the number
  cannot come back meaning something else. The tower's key, its feature and
  its key deposit stay; its fifteen monument slots go, for 30 gourds.
  - The two were only ever bundled by the option. Since `big_key_items`, a
    full monument releases nothing: the Green Dome's fifteen slots buy its
    own deposit checks and not one thing more. Splitting them costs a value
    on an enum and nothing else.
  - The occasion was `goal: second_ending`, which is won behind the Green
    Dome door and so cannot be played with `excluded`. That combination is
    **repaired in `generate_early`, not refused**, and repaired to
    `key_only` rather than `full`: the goal needs the key, not the grind.
    The repair is written back to the option so `current_key` — what travels
    in slot_data and what the spoiler prints — says what was generated.
- **`green_dome_deposits` REMOVED ENTIRELY (2026-09-25)**, before the first
  public release, on the player's call: "it does nothing and loses the player
  more than anything". The Green Dome is always in play: 45 gourds, the Hub
  Secret Door, its key and its zone.
  - `full` vs `key_only` only ever changed fifteen gourds and the deposit
    checks they buy, and `deposit_locations` / `deposit_goal_amount` already
    decide how much of that grind there is.
  - `excluded` took the Hub Secret Door zone out, which stopped being a
    postgame grind once the mod removed the sphere: it cut content for nothing,
    and forced the `second_ending` repair above.
  - It also made Universal Tracker disagree whenever co-op YAMLs differed on
    it. Removed before release so no published YAML carries it.
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
  - **A fourth added (2026-09-22): `second_ending`**, the ending behind the
    Hub Secret Door, at the player's request. It needed detection that did
    not exist — the endings persist nothing at all — and got it from the
    IL2CPP dump rather than another Ghidra session: see
    `../mod/reverse-engineering-notes.md`, "RESOLVED AS A DETECTION, NOT AS
    A FLAG". The mod latches the transition itself.
  - Its logic requires `Has("Hub Secret Door")` **and nothing else**, which
    is an
    assumption and the riskiest thing in this world right now — see §10 of
    `protocol.md`. The vanilla seal on that path is a sphere the mod already
    removes from a save's first session, so the door should be all that is
    left, but nobody has walked the zone.
  - It also forced a third value on `green_dome_deposits`, below.
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

## DECISION SETTLED (2026-09-21) — the radio stations become items too

The problem, in one line: **switching a station on reported the check AND
granted the station**. Every other check in this world grants nothing locally
— gourds are hidden and unspawned, big keys arrive from Archipelago — so the
radio was the only one that rewarded itself. Nothing broke, no seed was ever
unbeatable, and it stayed that way for a fortnight. It was an inconsistency,
not a bug, and it is now fixed.

**What was decided**

- Seven `Radio Music NN: …` items, one per station, ids
  `BASE_ID + RADIO_ID_OFFSET + system_value` — the same numbers as the
  stations' own locations, exactly as the big keys already do it. They
  displace seven filler rather than growing the pool.
- Classified **filler**, not useful. A Radio Music item starts a piece of
  music and nothing else; no rule in this world or any other could ever
  require one.
- **Named after the music, not after the enum** (2026-09-21, measured in
  game). `FmStationBreathwork` broadcasts `musicGroup_bobby`,
  `FmStationFourthSpace` broadcasts `musicGroup_breathwork`,
  `FmStationSleuthFm` broadcasts `musicGroup_FourthSpace` — only DanceFm and
  JourneyBeat match their own label. The game shows no station names at
  all, so nothing in the world contradicts the choice, and naming an item
  after music it does not play would have been a plain lie. The `Radio Music`
  prefix is there so a player reading the item in a multiworld feed knows it
  is a tune and not a key.
- **Numbered by their place on the dial** (first alpha's feedback,
  2026-09-25): `Radio Station 01: Bobby` through `Radio Station 07: Bristol`,
  the same for `Radio Music`. The dial is a row of unlabelled lights, so a
  station's place on it is the only thing a player can go by; the music name
  alone told them nothing about which station to switch on. Counted from 1,
  two digits, as the player asked. The places are the ones the mod learns
  from the world (`ap_radio_dial_*`), identical in two of our saves and in
  the alpha's guest log. Ids are unchanged.
- A **separate option**, `radio_station_items`, rather than folding it into
  `radio_station_checks`. The two are genuinely different wishes: a player may
  want seven more checks without losing their music, or the music shuffled
  without the extra checks. Both default on.

**Why a separate option and not simply always on**

Because this is the one place the mod *takes something away* from the player.
That deserves a switch, and it also decides the safe default on the mod side:
`ApSlotData.RadioStationItems` falls back to **false** when the field is
absent, where every other flag falls back to its common value. An apworld too
old to send the field has no Radio Music items in its pool, so suppressing the
radio for it would leave seven stations permanently silent with nothing able
to unlock them.

**The co-op wrinkle, accepted knowingly**

`FmRadioManager` is local to each machine and never replicated; a guest's
station lights up because the *peck state* is networked, not the unlock. Only
the host runs an Archipelago client, so only the host can know which stations
the slot has received. The guest's radio therefore stays vanilla: they hear a
station the moment somebody switches it on, while the host waits for the item.

Considered and rejected: suppressing on every machine and having the host
re-drive the networked peck state to re-fire the effect. It would need the
tower loaded, the peck state is already at its unlocked value so the write may
be swallowed as a repeat, and the failure mode — a guest whose radio can never
play anything at all — is much worse than a cosmetic difference between two
players' speakers. Stated in the option text so no one meets it in play.

## DECISION SETTLED (2026-09-21) — big keys: forage checks, and an item that is the feature

Raised by the player, and it resolves a collision this world has carried from
the start. **One physical act carried three roles**: pinning the key in its
plinth was the location, the effect of the item, and what opened the door.
Because applying the item consumed the plinth, the location could only ever be
checked by the mod itself — §8 of `protocol.md` calls that a deliberate quirk.
It also made the key item redundant: the monument released the key locally as
soon as the gourds were there, so the door opened whether or not Archipelago
ever sent the key.

**What was decided**

| Role | Act |
|---|---|
| Locations | the 25 cut segments, **plus** placing the key in its receptacle (the 7 existing deposit locations) — 32 in all |
| Item | the **feature** itself: the map room, the chairlift, the train, the tunnels, the drawbridge, the dam, the Hub Secret Door |
| The key | a check carrier. Placing it in its receptacle is a check and **nothing else** — the key becomes an inert object once placed |

The consequence that makes this simple: **nothing about cutting or placing
needs suppressing.** The player fores the key and places it freely, exactly as
in the vanilla game, and both are checks — the same shape as a puzzle whose
gourd is a check. The only thing that must stop happening is the door opening
when the key goes in.

**What it removes**

- §8's self-report: the deposit location becomes a genuine player act again,
  so `ItemApplier` stops reporting it on the mod's behalf.
- The special case in `rules.py` for a precollected Tutorial Key. Its deposit
  location gets the same gourd requirement as any other, because the mod no
  longer pins it at connection time.
- The key item's redundancy. Without it the door stays shut, whatever the
  player does with the key.

**Logic: no new risk.** A forage location physically needs that tower's
monument full, which is the same property the existing deposit location
already has — the rule there is the global cumulative count, sorted
cheapest-first. The forage locations take the same rule as their tower's
deposit. This is **not** Option C reopening: nothing requires a *specific*
monument that the current world does not already require.

**Counts, measured in game rather than assumed** (Ctrl+K, 2026-09-21): five
segments each on the drawbridge and the four coloured towers. The Black
Monolith and Green Dome keys are born finished and have no cutting, so they
have no forage locations — only their deposit, and their feature item.

**The one open question — ANSWERED (2026-09-21).** Which switch actually opens
a door? None of the ones anybody looked for. Every candidate was a component
of the *plinth*, and all of them came back empty: no `PropHomeBlock` watches a
big-key plinth, no plinth fires a `PeckSwitch` on pin, and of 2833 loaded
`PeckSwitch` instances not one is keyed on a big key.

**The wiring is on the key.** `Prop.taggedPinSystems` is a
`(PropGroup, TrackedPeckState)[]` carried by the prop itself, and
`Prop.SetPinDirectControlSystem` fires the entry whose group matches the
home's `pinGroup`. One answer settles both halves exactly as hoped: that is
what must stop firing on pin, and it is what the item drives instead. Full
detail in `../mod/reverse-engineering-notes.md` and `protocol.md` §12.

**Implemented 2026-09-21**: `Core/KeyFeatures.cs` (grant + `ap_feature_*`
ledger), `Patches/PropTaggedPinPatch.cs` (suppression),
`Patches/KeyBlankCutPatch.cs` (the 25 cut checks, off `OnCutsUpdated` —
`ServerCutSegment` turned out to be inlined and unpatchable), and the apworld
side in `bigwalk/`. The item names are the features: Drawbridge, Map Room,
Chairlift, Train, Tunnels, Chapel Door, Hub Secret Door.

## CORRECTION (2026-09-21, same day) — the keys are items, not a reward for gourds

The decision above made the FEATURE an item and left the KEY exactly where
the vanilla game puts it: released by filling that tower's monument. The
player corrected it the moment it was playable — *"ce n'est pas parce que
les clés reviennent à leur emplacement initiaux qu'elles doivent être
débloquées par l'action de ranger n gourdes"*: a key going back to where it
started is no reason for it to be unlocked by stowing n gourds. A key should
arrive from
the multiworld and spawn like a gourd, with the same physics and the same
Ctrl+R recovery.

**What changed**

- **Seven more items**, the keys themselves, named after the feature they
  fit (`Chairlift Key`, `Map Room Key`) so that they match their own
  locations and nothing has to be memorised. Ids at `B + 4000 + prop_value`,
  because `B + prop_value` was already the feature's.
- **A monument buys nothing but its own deposit checks.** The mod holds
  every key locked (`blockGrabbing`) until its item arrives.
- **The rules change kind.** A tower's five cuts and its placement all rest
  on `Has(<Feature> Key)`. `gourd_requirements` — the cumulative count that
  charged each key deposit — is deleted rather than left computed and
  unread.
- **`start_with_drawbridge_open` precollects the Drawbridge, not the key.**
  Leaving the tutorial is the feature's job; the key is six checks, and
  handing it over would be handing over checks.
  - **Default flipped to OFF (2026-09-22).** It defaulted ON only as a hedge
    against the drawbridge being the one way out of the tutorial, which the
    player settled that day: it is not, and the mod opens the hub's arch
    doors on a save's first session anyway. With nothing left to hedge, the
    Drawbridge is a feature like the other six and is found rather than
    given — one more thing in the pool instead of one fewer.

**The naming pass that followed (2026-09-21, before the first release).**
Reviewing whether the seven features should read `Feature: Chairlift` rather
than `Chairlift` settled that question with a no — a prefix earns its place
only where it disambiguates, which `Radio Music 01: Bobby` needs and `Chairlift`
does not — and turned up a genuine mismatch next to it. Three deposit
locations were named after their PLINTH (`Train Station Key Deposit`,
`Tunnel Key Deposit`, `Goodbye Keyhole Key Deposit`, inherited from the
third-party document) while their items and their 25 cuts were named after
the feature, so a player holding a `Tunnels Key` had to learn it belonged in
the `Tunnel Key Deposit`. All seven now derive from one name: `<feature>
Key`, `<feature> Key Cut 1..5`, `<feature> Key Deposit`. Done before the
datapackage was published, which is the only cheap moment for it.

**What did NOT change, and that is the point of asking rather than
assuming**: the key stays a check carrier. Placing it opens nothing, the
door is still its own item, and the 25 cut checks survive because the key
arrives uncut. Both were put to the player as explicit choices, and both
were kept — so the suppression, the ledger and the cut hook built for the
first pass all stand.

## SETTLED (2026-09-22) — Universal Tracker support

Asked by the player: is the world compatible? It loads and it tracks — nothing
in the logic is random beyond which filler gets picked, so there is no entrance
rando and no random spawn for the tracker to guess at, and the graph it
rebuilds is the seed's graph. What it had no way of knowing was the seed's
OPTIONS: UT generates from the tracking player's own YAML, and in a co-op
group that YAML is usually somebody else's. Measured against the default seed
before deciding anything: `green_dome_deposits: excluded` on its own gives the
tracker 89 locations and 4 regions instead of 93 and 5 — the Hub Secret Door
zone simply does not exist for it — `deposit_locations: all` invents 36
locations, and turning the radio checks off loses 7.

Implemented as the two hooks UT documents, and they cost almost nothing
because the work had already been done for the mod: `fill_slot_data` has
carried every option that shapes this world since the world was written, and
the two derived values are written back into the options before it reads them
— the clamped deposit goal, and the `second_ending` green-dome repair. So a
re-generation re-applies the same repairs to the same numbers and lands on the
world that exists rather than the one that was asked for. Adding
`ut_can_gen_without_yaml` on top means a player tracking a seed they did not
generate needs no YAML at all.

Which fields those are lives in `world.TRACKER_OPTIONS`, kept in step with
`fill_slot_data` by a test rather than by care. Another test rebuilds a whole
seed the way UT does (`build_like_universal_tracker` in `test/bases.py`,
mirroring `TrackerCore.TMain`) and compares it location by location, with a
control that fails the moment the passthrough stops working.

## Open points carried forward

**That assumption is now measured.** The 25 cut locations sit in the
overworld because every one of the five cutting trails is reachable without
a big key — confirmed in play on 2026-09-21. It was worth checking rather
than assuming: the identical claim about the puzzles turned out to be false
earlier the same day and had made seeds unbeatable.

## Explicitly set aside

- ~~**Key Cutters**~~ — **reconsidered and taken (2026-09-21)**, exactly as
  that note allowed for: the decomposition moved forward, so the five cuts per
  tower are checks after all. Twenty-five of them, and what made them worth
  having is not the cutting itself but that they let the key become a pure
  check carrier — which is what freed the item to be the feature.

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
player.

**REVISED (2026-09-15, player, after playing with it)**: in front of the
player after all, whenever there is one standing in the world. In practice
the hub rule meant walking back across the island to collect something the
server had just handed over. The hub stays the fallback for the moments
when nobody is really in the world yet (loading, menus), which is also the
only checkable form of "are the players connected". Configurable
(`Archipelago/SpawnGourdAtPlayer`), and kept to a short step ahead of the
player rather than a generous one: placing props at computed offsets has
already lost gourds in geometry twice, and the player is by definition
standing somewhere valid. It goes straight into their hands when those are
free and the game's own `PlayerHands.IsSafeToPickUp` agrees
(`Archipelago/PutGourdInHands`).

**Scope, settled the same day**: this applies only to a gourd arriving
**during play**. The batch rebuilt at the start of a session still goes to
the hub — that is stock, not a gift, it can run to dozens, and the players
are not necessarily anywhere near the hub when a world loads. Raining it on
whoever just loaded in would bury them.

**Known and not solved by any of this**: a gourd received inside a sealed
puzzle room is stranded there for the session, in hand or on the floor
alike, because the game will not let it be carried out. Recovered on the
next world load, when the ledger recomputes received-minus-deposited. This is consistent with the same day's "gourds = generic fillers"
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

### 45 puzzles, and the thirteen that are not there (CORRECTED 2026-09-21)

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
`gourdScoutCounting`).

**The hypothesis written here on 2026-09-07 was that the game had received a
content update adding those 13 since release, and that the real total was
58. It was backwards, and the world shipped 13 locations that can never be
checked because of it.** The thirteen are in the enum, each with its own
`valetXxx` home, and nothing in the build produces them. Corrected on
2026-09-21 from a finished save of 2026-08-23: it holds exactly 45 `gourd*`
entries, each pinned to a monument slot, spread 4/5/5/5/5/6/15 — every slot
in the game, the Green Dome's fifteen included. 45 gourds for 45 slots is
also the game's whole economy; a 58-puzzle game would leave thirteen gourds
with nowhere to go. Every Ctrl+V dump instantiated the same 45 and never
these, and the player recognises none of the thirteen names. The document's
45 were right all along, and reading its shorter list as "older" rather than
as "the truth" is the trap: an independent inventory that disagrees with an
enum is evidence about the build, not about the date.

The list now lives in `bigwalk/data.py` as `ABSENT_FROM_THE_BUILD`, ready to
paste back into `PUZZLES` if a patch ever ships them.

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
