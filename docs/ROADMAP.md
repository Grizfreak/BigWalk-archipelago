# Road to 1.0

The to-do list for the rest of the project, adopted on 2026-09-26. Worked
through one step at a time: pick the next item, build it, test it, tick it.
The top half is the list; the bottom half is the detail behind each item,
found by its id (X1, T2, …).

Sources: the community document "Big Walk Archipelago details" (proposed
options, items and locations, custom tiles, 0.1.0 notes), and the player's
own wishes (traps, colours, where things are in the world, crossplay).

---

## The list

Order is a proposal: crossplay first, because it changes how everything after
it has to be built. Every option of the community document has an id here —
the table at the very bottom maps each one — and ideas of our own come on top
of it (S4–S9, U1–U7).

**The rule for defaults** (from the document): "vanilla + AP". The players'
movements are untouched and places stay locked as in the vanilla game;
unlocks are not linear unless being linear saves tedium; a sanity that gives
nothing in the vanilla game is off (`pedestal_sanity` off, `cut_key_sanity`
on); custom tiles only when a matching game is in the multiworld.

**Done** — 0.1.1: every point of the 0.1.0 notes (test 24 built, not run);
co-op testable on one PC (Ctrl+L). 0.1.2 so far: `start_with_arch_doors_open`
(any set of arch doors locked, the First one alone included).

**Step 1 — Players without the mod (crossplay: PlayStation, Xbox)**
*Goal: they can play. Fewer features is acceptable; being blocked is not.*
*Rule: a session where everyone runs the mod plays exactly as 0.1.1. A change
that would cost modded players anything becomes an off-by-default option, or
is dropped.*
- [x] X1 Find which objects a vanilla game can build — only the player and the
  corpse: no gourd, no gadget
- [x] X2 A loopback guest without the mod, to test as a console player would
  (Ctrl+Y on the host)
- [ ] X3 Nothing blocks a player without the mod (items in someone's hands
  first, then the true-ending sphere…)
- [ ] X4 Received items visible to them: reuse the island's own objects
- [ ] X5 A real session with a console

**Step 2 — Quality of life** *(new: small, and felt on every seed)*
- [ ] U1 Hints for keys from the start (`hint_keys: from_start`) — apworld only
- [ ] U2 The map shows which puzzles still hold a check
- [ ] U3 A new save gets its deposits back, not only its items (DataStorage)
- [ ] U4 A button in the world for the gourd resync, instead of Ctrl+R
- [ ] U5 Checks remembered per seed, not per save (`ap_reported_*`)
- [ ] U6 A PopTracker pack with the island's map (Universal Tracker works today)
- [ ] U7 The game's own skip aids (`SkipAidToggler`) as an option (research)

**Step 3 — Traps**
- [ ] T1 Trap weights as options (`trap_percent` exists, hidden)
- [ ] T2 Drop Everything, Butterfingers, Heavy Items
- [ ] T3 A door slams shut for a while
- [ ] T4 Gourds scattered
- [ ] T5 Teleport, colour chaos, radio hijack (modded players only)

**Step 4 — Colours and the world**
- [ ] C1 A colour palette per seed, the same on every screen
- [ ] C2 Colours for keys, buoys and the island's objects
- [ ] C3 Objects moved to other places per seed (research)

**Step 5 — Goals**
- [ ] G1 `big_walk`: the tutorial key in the drawbridge keyhole
- [ ] G2 Keys and radio stations as collectible goals (`keys_to_goal`,
  `radio_stations_to_goal`)
- [ ] G3 Collectibles AND an ending, or collectibles THEN an ending
- [ ] G4 Towers to fill (`towers_to_goal`, `limit_green_dome_deposit_boxes`)
- [ ] G5 Objectives: pick any endings and collectibles for a seed, add or
  remove each one
- [ ] G6 `big_bell` accepted as a name of `big_wall`; the gourd goal capped by
  the gourds a seed really has (38 without purple, 36 with a small Green Dome)

**Step 6 — Hints**
- [ ] H1 Deposit boxes hinted when a tower is reached
  (`hint_discovered_deposit_boxes`)
- [ ] H2 Keys hinted when their tower is reached (`hint_keys: when_discovered`)
- [ ] H3 Key cutters and deposits hinted likewise
  (`hint_discovered_key_locations`)

**Step 7 — Vanilla behaviours back, as options**
- [ ] L1 Keys earned by filling their tower (`linear_towers`)
- [ ] L2 A placed key opens its door (`linear_keys`)
- [ ] L3 Map Room open from the start (`lock_map_room`)
- [ ] L4 Purple gourds: from the start, when unlocked, or never
  (`include_big_game_puzzles`)
- [ ] L5 Key cuts as an option (`cut_key_sanity`)
- [ ] L6 Arch doors opened by their buttons, as in the vanilla game
  (`linear_arch_doors`)
- [ ] L7 Places past the Left and Right Arch Doors need their door in logic
  (`require_arch_doors`)
- [ ] L8 Puzzles give their own gourd (`linear_puzzles`, see question 1)

**Step 8 — More locations**
- [ ] S1 Backpacks and hip packs (`backpack_sanity`, with its `vanilla` mode)
- [ ] S2 Flares and flare stations (`flare_sanity`)
- [ ] S3 "All Radio Stations", "<Tower>: All Gourds Deposited"
- [ ] S4 The four lookout lights (`LookoutLight*`, saved like the radio)
- [ ] S5 The endings as checks when they are not the goal: the Big Wall bell,
  the Gauntlet, the secret ending's gourd
- [ ] S6 The game's other saved switches: the Black Tower's inner door, the
  Poet and Priest / Poet and Pontiff doors (research what each is)
- [ ] S7 The island's other objects (cowbells, compass, binoculars, …: every
  one has a `savablePropGuid`)
- [ ] S8 A check for reaching each tower (the host sees every position)
- [ ] S9 `pedestal_sanity`: glow orbs on pedestals, `track_only` included
  (research)

**Later — research first**
- [ ] R1 Lock puzzles / towers behind items (`lock_puzzles`, `lock_towers`)
- [ ] R2 Lock picking things up (`lock_pickups`)
- [ ] R3 Lock abilities: jump, crouch, gestures (`lock_abilities`)
- [ ] R4 One-player mode; player limit as items
- [ ] R5 `big_climb`, `big_club`
- [ ] R6 Golf, Cabin Fever timers, black sphere puzzles
- [ ] R7 Whiteboards: hints, and the rewrite trap
- [ ] R8 Custom tiles
- [ ] R9 The other traps (No Comms, Cutscene, The Mask, Whiteboard Rewrite)

**Before calling it 1.0**
- [ ] Q1 A seed played from the first check to the goal
- [ ] Q2 Every option described on the game page and in the YAML template
- [ ] Q3 Every default checked against "vanilla + AP" (above)
- [ ] Q4 Test 24 (puzzle gourds stowed in a bag), with a save that has some

**Tooling, when it gets in the way**
- [ ] D1 Loopback guest: keyboard for the host, gamepad for the guest

## Open questions

Answers go here, and into the detail below, as they come.

1. **`linear_puzzles`**: `true` = the puzzle gives its own gourd as in the
   vanilla game (no Archipelago item on it)? Or something else?
2. **Filler names** (Baby, Boid, Bouba…): joke items with no effect, or names
   for the gourd item?
3. **Objectives (G5)**: must a seed finish every objective listed, or only a
   number of them?

Answered (2026-09-26):

- **Order**: crossplay first, as listed.
- **Colours**: the island's objects, and many more things later ("we'll
  see"). A trap that changes the players' colours is one example: colour can
  take many forms, in C and in T5 alike.
- **Randomizing items in the world**: ideas in the air, not a commitment. C3
  stays a Research idea at the bottom of its step.
- **Traps**: a mix, tunable — a weight per trap in the YAML (T1), so each
  group picks the tone.

---

## Detail

**Status:** **Done** · **Partial** · **To do** (feasible, understood) ·
**Research** (measure or decompile first) · **Decision** (a design choice
first).

**The rule behind most designs here:** an effect reaches a player without the
mod only if it travels through the game's own replication — held items,
peck states, positions of networked objects, registered prefabs. Anything the
mod paints, places or plays locally is for modded players only. Only the host
talks to Archipelago; guests learn anything else through the `ModChannel`.

### Step 1 — Players without the mod, and crossplay

A console cannot run the mod: every console player is a guest without it,
and the host has to be a modded PC.

**Definition of done (player, 2026-09-26):** a player without the mod can
play a seed to the goal alongside modded players — see what they are handed,
carry it, deposit it, reach every place the seed needs. Losing features on
their side (colours, the overlay, the radio mirror, the map reveal) is
acceptable; being blocked, or seeing nothing where others see an item, is not.

**No regression for modded players (player, 2026-09-26), and a hard rule:**
a session where everyone runs the mod must play exactly as 0.1.1. So:
anything done for players without the mod is either invisible to modded ones
(an island gadget instead of a clone looks the same) or applies only while a
player without the mod is connected (the `ModChannel` knows who said hello).
Every X4 change re-runs the modded co-op tests (6, 16 to 22) before it stays.
A change that would cost modded players something becomes an off-by-default
option, or is dropped.

Known (tested 2026-09-25): a player without the mod joins a modded host and
plays; every check they make is reported, because the host detects checks on
its side. They miss (`SETUP.md`): received gourds and gadgets (spawned under
the mod's own asset ids, which a vanilla client cannot build, so invisible to
them), key tints, the overlay, the radio mirror, the purple gourds on the map,
and the sphere before the true ending, which the mod removes only where it
runs.

- **X1 — Done (2026-09-26).** `NetworkManager.spawnPrefabs` holds ONE
  prefab, `Corpse` (a Prop with homes for a pack and a belt), and the player
  prefab is `PlayerCharacter` — dumped by Ctrl+N and once per launch in debug.
  A vanilla game never creates a prop: the save's inventory is a list of
  `savablePropGuid`s, and loading MOVES those scene props to the
  InventorySpawn. So every object a player without the mod can see is a scene
  object their own game already has, spawned by sceneId. The mod's clones,
  spawned under asset ids of its own, can never be built there.
- **X2 — Done (2026-09-26), joining a host untested.** A loopback guest that
  runs none of the mod, to test as a console player would. A guest without
  BepInEx cannot join by address (the vanilla menus join through EOS lobbies,
  and both instances share one EOS identity), so it is a `--bwap-vanilla`
  guest: `Plugin.Load` returns after the join and its two guest patches
  (identifier, `OnClientConnect`), the connection monitor and three debug keys
  (Ctrl+N, Ctrl+L to join, Ctrl+T) — no `PatchAll`, none of the mod's
  components, no `ModChannel`, so it never says hello. Started by **Ctrl+Y** on
  the host, or `tools/launch-guest.ps1 -Vanilla`. Smoke-tested alone: only
  `LoopbackGuestMonitor` and `DebugHotkeys` are registered. The join path is
  the modded guest's, which is tested.
- **X3 — Research, and the likely blocker.** Since 0.1.1 received items go
  into players' hands. A held item is published in `PlayerHeldInformation`, a
  SyncVar carrying the object's netId; a client that cannot build that object
  resolves it to null, and the game's own hook dereferences it inside
  `DeserializeSyncVars` — measured on 2026-09-22 with a missing spawn handler:
  `OnDeserialize failed`, then an exception every frame and that player's
  network state broken for the rest of the session. A player without the mod
  would hit this whenever anyone holds a received item. Measure it with X2;
  the cure is X4, and meanwhile a host could hand items only to players whose
  connection said hello (the `ModChannel` knows) — which does not help when a
  modded player holds one in front of them.
  **Test for X3 (next session):** host a world (Archipelago on or off), press
  Ctrl+Y and let the guest join. Then Ctrl+I several times, both hands empty
  first, then with things held. Watch: on the guest's screen, does it see the
  items (expected: no) and can it still move and pick up the island's own
  objects; in `LogOutput.1.log` and `Player-guest.log`, `Could not spawn
  assetId` (expected, harmless) against `OnDeserialize failed` or a
  NullReferenceException repeating (the blocker); in the host's log, no
  `runs the mod` line for that guest. Then the true-ending sphere:
  `SecondEndingSphereUnlocker` disables `Spawn_SecondEnding_Sphere_Whole`
  locally; find what drives it and whether the server can open it for
  everyone. Then walk every goal with an X2 guest.
- **X4 — Research.** Received items built from the island's own scene
  objects rather than clones:
  - **gadgets**: the 70 vanilla instances the mod hides (3 megaphones, 8
    walkie-talkies, 6 backpacks, 5 belts, 32 lamps, …) would be switched back
    on and moved instead of cloned, so a vanilla client spawns them by
    sceneId. Past a kind's vanilla count, clones remain: invisible to players
    without the mod, and never to be handed to one (X3).
  - **gourds**: the island has 45 puzzle gourds and 45 monument slots. A
    solved puzzle's gourd is taken out of play (`PuzzleGourdRetirer`); it could
    come back as a received gourd instead of a clone. A seed can hand out
    more gourds than have been solved locally, so a clone is still needed
    past that — same rules.
  - to keep: neutralized progression on the host (a received gourd must not
    count as a puzzle, `NeutralizeProgression`), the hand-over, Ctrl+R, the
    session rebuild, the colours for modded players.
- **X5 — To do, needs a console.** Consoles join over EOS P2P, relayed; the
  loopback guest talks Kcp. Check EpicTransport behaviour (packet size,
  fragmentation), the crossplay setting (`settings_crossplay`, the lobby's
  `crossplay`/`platform` attributes) and the same game version on both.

### Step 2 — Quality of life

Not in the document; gathered from the leads set aside in `NEXT-SESSION.md`
and from what a whole seed will ask of players.

- **U1 — To do, apworld only.** `hint_keys: from_start`: the seven key
  locations go into `start_location_hints`. The other two values are H2.
- **U2 — To do.** The map already shows the purple gourds
  (`VariantGourdMapUnlocker`); it could mark each puzzle whose check is not
  yet sent, from the host's check list. Modded players only.
- **U3 — To do.** Monument fill state in Archipelago's `DataStorage`, so a new
  save recovers its deposits as it recovers its items (today they are
  re-deposited by hand: the new-save recovery plan).
- **U4 — To do.** A real in-world button for the gourd resync, instead of the
  Ctrl+R that only the host knows about.
- **U5 — To do.** `ap_reported_*` is not scoped to a seed, so a save
  reconnected to another seed resends checks earned elsewhere. Scope it by
  the seed name the room sends.
- **U6 — To do.** A PopTracker pack: the island's map with every location on
  it, items and doors. Universal Tracker already works without a YAML.
- **U7 — Research.** The game has skip aids of its own (`SkipAidToggler`,
  `SaveData.skipAidsActive`): find what they skip, and whether an option
  turning them on from the start is worth it.

### Step 3 — Traps

`trap_fill_percentage` exists (hidden) and nothing happens when a trap arrives.

| Id | Trap | Who sees it | What it rests on |
|---|---|---|---|
| T1 | Weights per trap (`*_trap_weight`, as the document proposes) | — | apworld options; the item pool already makes room for traps. |
| T2 | Drop Everything, Butterfingers (hands only), Heavy Items (worn packs) | everyone | The host clearing `PlayerHeldInformation`, as the hand-over and Ctrl+R do (`ReceivedItemSpawner.ClearServerSideHold`). "Cannot hold for N seconds" means refusing pick-ups meanwhile: `UserCode_CmdPickUp` runs on the host. |
| T3 | A door slams shut for a while | everyone | Peck states are server-side; `ArchDoors`/`KeyFeatures` drive them. Must never trap a player out of logic: reopen on a timer, and never a door the seed needs shut forever. |
| T4 | Gourds scattered | everyone | Loose gourds are server-owned; `ReceivedItemSpawner.ResolveSpawnPosition` places them. Needs a list of safe spots (reachable, not in a sealed puzzle room). |
| T5 | Teleport, colour chaos (the world, or the players themselves), radio hijack | modded players | `PlayerGrease.Teleport` on the player's own machine through the `ModChannel` (as Ctrl+C); the colour writes of C1/C2 — a player's colour needs its renderer and shader property measured like any other object; `FmRadioManager` is local. |
| R9 | No Comms, Cutscene, The Mask, Whiteboard Rewrite | — | Research each: where walkie-talkies live, how the monolith cutscene is started, what The Mask is. |

### Step 4 — Colours and the world

Received gourds take one of six colours from their netId; the five yellow
keys are tinted.

- **C1 — To do.** A palette per seed. The colour must come from something
  every machine shares — the seed, sent to guests in the `ModChannel`
  snapshot, or a netId — never drawn on each machine.
- **C2 — To do, measure first.** For each kind of object, the shader property
  that carries its colour (`househouse/VertexColors`: `_TintColor`; gourds:
  `_RColor` through `PropertyBlockHelper.colorSettings[0]`). A
  `MaterialPropertyBlock` accepts a property the shader lacks and changes
  nothing (the `_RColor` lesson of 2026-09-21); a value the game copies in
  `Awake` must be written where the game reads it (0.1.1's red gourds). Buoys:
  the "freely random colours for received lamps" lead in `NEXT-SESSION.md`.
- **C3 — Research.** Moving the island's objects per seed. The mechanism
  exists (vanilla gadgets hidden, clones spawned where the mod wants); missing
  are safe spots and X2 (a moved object must be visible to everyone).

### Step 5 — Goals

Today a goal is one of four: `big_wall`, `big_goodbye`, `big_game`,
`big_collection` (sent on the wire under their old names, `GOAL_ON_THE_WIRE`).

- **G1 — To do.** `big_walk`: the Drawbridge Key Deposit is already detected.
- **G2 — To do.** `keys_to_goal` (deposits are checks already),
  `radio_stations_to_goal` (stations are tracked).
- **G3 — To do.** The document's `goal`: `collectibles`,
  `collectibles_and_ending`, `collectibles_then_ending`. "Then" also needs the
  ending held shut until the collectibles are in (an ending is started by a
  peck: `PeckEffectEndingTransition.OnPeck`).
- **G4 — To do, bigger.** `towers_to_goal` and
  `limit_green_dome_deposit_boxes` need per-tower deposits: Option C/D in
  `apworld/design-decisions.md`, set aside for the alpha.
- **G5 — To do, Decision first.** The user's idea (2026-09-26): instead of one
  goal, a list of objectives the YAML adds or removes one by one, the way
  `start_with_arch_doors_open` lists doors — endings (`big_wall` — the
  document's `big_bell`, worth an alias —, `big_goodbye`, `big_game`, then
  G1's `big_walk` and R5's `big_climb`, `big_club`) and collectibles
  (`big_collection`, then G2's keys and radio stations). G3's "and" is then
  just a list with both kinds; its "then" stays an extra ordering. The mod
  can detect each of today's four goals but watches only the chosen one
  (`ApRuntime`, `switch (slotData.Goal)`): it would watch every listed one
  and send the goal once they are done. To decide: all of them, or a number
  of them (like `gourds_required`)? And the old `goal` kept hidden and read
  into the list, as `lock_arch_doors` was.
- **G6 — To do, apworld only.** `big_bell` as an alias of `big_wall` (the
  document's name). `gourds_to_goal` is capped in the document by what a seed
  really holds: 38 gourds without the purple ones (L4), 36 with a six-slot
  Green Dome (G4). Today `gourds_required` stops at 45 because every gourd is
  always in; the cap comes with L4 and G4.

### Step 6 — Hints

- **H1, H2, H3 — To do.** `hint_discovered_deposit_boxes` (and the tower's
  clear reward), `hint_keys: when_discovered`, `hint_discovered_key_locations`:
  when a player reaches a tower, the host sends location scouts as hints (the
  `create_as_hint` flag of `LocationScouts`). Tower positions are measured
  (`DebugWorldLayoutDump`); the host sees every player's position. The same
  trigger gives S8. `hint_keys: from_start` is U1; with `linear_towers` on,
  `hint_keys` has nothing to hint and is off.

### Step 7 — Vanilla behaviours back

The mod suppresses each of these; an option turns the suppression off.

- **L1 — Partial.** `linear_towers`: keys are Archipelago items today
  (`disabled`). `all_deposit_boxes` is the vanilla behaviour `KeyCustody`
  holds back; `one_deposit_box` is new.
- **L2 — Partial.** `linear_keys`: features are items and deposits are checks
  (`false`). `true` is the vanilla door, held back by `PropPinDoorPatch`.
- **L3 — To do.** `lock_map_room: false`: the Map Room starts open, like
  `start_with_drawbridge_open`.
- **L4 — Partial.** `include_big_game_puzzles`: the purple gourds are
  locations behind the Chairlift and shown on the map from the start
  (`VariantGourdMapUnlocker`). `when_unlocked`, `disabled` to do.
- **L5 — Partial.** `cut_key_sanity`: the 25 cuts are always locations;
  `filler` (item rules) and `disabled` (no locations) to do.
- **L6 — Research.** `linear_arch_doors`: in the vanilla game the arch doors
  are opened by buttons; the mod opens all three on a save's first session
  (`ArchDoorUnlocker`) or holds them for their items (`ArchDoors`). `true`
  would leave them to their buttons. To measure first: where each button is,
  and whether the start zone can still be left (the reason the mod opens the
  First one: regions.py, "the way out of the starting zone").
- **L7 — Decision, then measure.** `require_arch_doors`: today only the First
  Arch Door gates logic, Left and Right are shortcuts, so a seed may send a
  player the long way round. `true` puts the places past each far door behind
  that door in logic — which needs those places listed: which puzzles, keys
  and stations are "past" the Left and Right doors. Only meaningful for doors
  that start closed.
- **L8 — Decision.** `linear_puzzles`: open question 1. `false` is today
  (a puzzle is a check, its gourd an item).

### Step 8 — More locations

- **S1, S2 — To do.** Task 2 in `NEXT-SESSION.md`: every such prop carries a
  `savablePropGuid`, readable with `SaveManager.GetIsInInventory(guid)` — what
  a location needs. The item side exists (the island's objects are filler).
  Open design question there: what "obtaining" an object lying on the ground
  means.
- **S1** also has a `vanilla` mode in the document: each backpack is a
  location that gives itself, and no other location gives one. **S2** covers
  the flare stations as well as the flares.
- **S3 — To do.** "All Radio Stations" (seven are tracked); "<Tower>: All
  Gourds Deposited" goes with G4.
- **S4 — To do.** `LookoutLightRed/Green/Blue/Yellow` (`SavableSystem` 21–24)
  are saved flags like the radio stations (30–36), so `SaveValuePatch` sees
  them written. Check in game what lights one, and that any player count can.
- **S5 — To do.** `EndingGate` (the Big Wall bell) and `GauntletComplete` are
  already detected for the goals; when they are not the goal they could be
  checks. Same for the 46th `RewardGourd` of the secret ending. On a seed that
  goals on one of them, it stays the goal event, not a check.
- **S6 — Research.** `BlackTowerInteriorDoor` (26), `PoetAndPriestDoors` (60),
  `PoetAndPontiffDoors` (61): saved switches, the last two next to known
  puzzles (`gourdPoetAndPreist`, `gourdPoetAndPontiff`). Find what each one is
  before deciding whether it is a check.
- **S7 — Decision.** Task 2 in `NEXT-SESSION.md`, beyond backpacks and flares:
  every object has a `savablePropGuid`. Same open question: what "obtaining"
  an object lying there means (picked up once? taken home?).
- **S8 — To do.** A check for reaching each tower, on H1's trigger.
- **S9 — Research.** `pedestal_sanity`: the glow orbs on pedestals, not looked
  at. `track_only` (no location, but the multiworld remembers which are lit)
  needs DataStorage, like U3.

### Later — research first

- **R1.** `lock_puzzles` (`open`, `anchored`), `lock_towers`
  (`progressive_vanilla`, `progressive_closest`, `random`): how to hold a
  puzzle or a tower shut (its vise, its peck states) is unmeasured.
- **R2.** `lock_pickups`: refuse in `UserCode_CmdPickUp` on the host; reaches
  every player, modded or not.
- **R3.** `lock_abilities`: input is Rewired (measured with Ctrl+N); blocking
  an action is local, so every player would need the mod — conflicts with
  Step 1.
- **R4.** `single_player_mode` (every co-op mechanism needs a one-player
  path; the debug tools that force peck states show it is possible),
  `lock_number_of_players` (player-count screen and `maxConnections` known).
- **R5.** `big_climb`, `big_club`: the host sees every position; the places
  must be measured.
- **R6.** `fast_golf`, `cabin_fever_time`, `cabin_fever_long_time`,
  `linear_big_goodbye` (black sphere puzzles): not looked at.
- **R7.** `whiteboard_hints` and the Whiteboard Rewrite trap: not looked at.
  (`pedestal_sanity` moved to S9.)
- **R8.** Custom tiles (`random_basic_tiles` and the tile dictionary):
  replacing tile textures, and drawing them. The document notes the copyright
  question: fan-art tiles rule out ever becoming a core world.

### Before 1.0

- **Q1.** Nobody has played a seed from the first check to the goal yet.
- **Q2.** The game page (`apworld/bigwalk/docs/en_Big Walk.md`), the setup
  guides and the YAML template describe every option.
- **Q3.** Every default against the rule at the top. The document also sets
  `accessibility: minimal` as its default; Archipelago's own default is
  `full`, which is safer for a world this young — keep `full` until Q1.
- **Q4.** Test 24 of `COOP-TESTS.md`: a puzzle gourd stowed in a bag must not
  come back. Needs a save that has one.

### Tooling

- **D1.** Rewired measured on 2026-09-26: both instances listen to keyboard,
  mouse and the gamepad, and ignore input while unfocused. The split: the
  guest sets `ignoreInputWhenAppNotInFocus` off and drops keyboard and mouse
  from its player; the host drops the gamepad while a loopback guest is
  connected.

---

## The document's options, one by one

Every option of "Big Walk Archipelago details", and where it lives. Status as
of 2026-09-26.

| Option | Id | Status |
|---|---|---|
| `goal` (`ending`, `collectibles`, `…_and_ending`, `…_then_ending`) | G3, G5 | `goal` exists, one value per objective |
| `ending`: `big_goodbye`, `big_game` | — | Done |
| `ending`: `big_bell` | G6 | Done as `big_wall`; alias to add |
| `ending`: `big_walk` | G1 | To do |
| `ending`: `big_climb`, `big_club` | R5 | Research |
| `gourds_to_goal` | G6 | Done as `gourds_required`; caps to come |
| `towers_to_goal`, `limit_green_dome_deposit_boxes` | G4 | To do |
| `keys_to_goal`, `radio_stations_to_goal` | G2 | To do |
| `linear_puzzles` | L8 | Question 1 |
| `linear_arch_doors` | L6 | Research |
| `linear_towers` | L1 | `disabled` only |
| `linear_keys` | L2 | `false` only |
| `linear_big_goodbye` | R6 | Research |
| `single_player_mode`, `lock_number_of_players` | R4 | Research |
| `lock_arch_doors` | — | Done, as `start_with_arch_doors_open` (0.1.2) |
| `require_arch_doors` | L7 | `false` only |
| `lock_map_room` | L3 | `true` only |
| `lock_puzzles`, `lock_towers` | R1 | Research |
| `fast_golf`, `cabin_fever_time`, `cabin_fever_long_time` | R6 | Research |
| `include_big_game_puzzles` | L4 | `from_start` only |
| `pedestal_sanity` | S9 | Research |
| `backpack_sanity` | S1 | To do |
| `flare_sanity` | S2 | To do |
| `cut_key_sanity` | L5 | `all` only |
| `lock_abilities` | R3 | Research; conflicts with Step 1 |
| `lock_pickups` | R2 | Research |
| `random_basic_tiles`, `…_from_other_games`, `…_dict` | R8 | Research |
| `hint_discovered_deposit_boxes` | H1 | To do |
| `hint_keys` | U1, H2 | To do |
| `hint_discovered_key_locations` | H3 | To do |
| `whiteboard_hints` | R7 | Research |
| `trap_percent` | T1 | Exists, hidden (`trap_fill_percentage`) |
| `drop_everything`, `butterfingers`, `heavy_items` trap weights | T1, T2 | To do |
| `no_comms`, `whiteboard_rewrite`, `cutscene`, `the_mask` trap weights | T1, R9 | Research |
| `accessibility`, `progression_balancing` defaults | Q3 | Archipelago's defaults kept |
