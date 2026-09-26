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
it has to be built.

**Done** — 0.1.1: every point of the 0.1.0 notes (test 24 built, not run);
co-op testable on one PC (Ctrl+L).

**Step 1 — Players without the mod (crossplay: PlayStation, Xbox)**
*Goal: they can play. Fewer features is acceptable; being blocked is not.*
- [x] X1 Find which objects a vanilla game can build — only the player and the
  corpse: no gourd, no gadget
- [x] X2 A loopback guest without the mod, to test as a console player would
  (Ctrl+Y on the host)
- [ ] X3 Nothing blocks a player without the mod (items in someone's hands
  first, then the true-ending sphere…)
- [ ] X4 Received items visible to them: reuse the island's own objects
- [ ] X5 A real session with a console

**Step 2 — Traps (first batch)**
- [ ] T1 Trap weights as options
- [ ] T2 Drop Everything, Butterfingers, Heavy Items
- [ ] T3 A door slams shut for a while
- [ ] T4 Gourds scattered
- [ ] T5 Teleport, colour chaos, radio hijack (modded players only)

**Step 3 — Colours and the world**
- [ ] C1 A colour palette per seed, the same on every screen
- [ ] C2 Colours for keys, buoys and the island's objects
- [ ] C3 Objects moved to other places per seed (research)

**Step 4 — Goals**
- [ ] G1 `big_walk`: the tutorial key in the drawbridge keyhole
- [ ] G2 Keys and radio stations as collectible goals
- [ ] G3 Collectibles AND an ending, or collectibles THEN an ending
- [ ] G4 Towers to fill (per-tower deposits)

**Step 5 — Hints**
- [ ] H1 Hints when players reach a tower

**Step 6 — Vanilla behaviours back, as options**
- [ ] L1 Keys earned by filling their tower (`linear_towers`)
- [ ] L2 A placed key opens its door (`linear_keys`)
- [ ] L3 Map Room open from the start (`lock_map_room`)
- [ ] L4 Purple gourds: from the start, when unlocked, or never
- [ ] L5 Key cuts as an option (`cut_key_sanity`)

**Step 7 — More locations**
- [ ] S1 Backpacks and hip packs (`backpack_sanity`)
- [ ] S2 Flares (`flare_sanity`)
- [ ] S3 "All Radio Stations", "<Tower>: All Gourds Deposited"

**Later — research first**
- [ ] R1 Lock puzzles / towers behind items
- [ ] R2 Lock picking things up (`lock_pickups`)
- [ ] R3 Lock abilities: jump, crouch, gestures (`lock_abilities`)
- [ ] R4 One-player mode; player limit as items
- [ ] R5 `big_climb`, `big_club`
- [ ] R6 Golf, Cabin Fever timers, black sphere puzzles
- [ ] R7 Pedestals, whiteboards (hints, trap)
- [ ] R8 Custom tiles
- [ ] R9 The other traps (No Comms, Cutscene, The Mask, Whiteboard Rewrite)

**Before calling it 1.0**
- [ ] Q1 A seed played from the first check to the goal
- [ ] Q2 Every option described on the game page and in the YAML template

**Tooling, when it gets in the way**
- [ ] D1 Loopback guest: keyboard for the host, gamepad for the guest

## Open questions

Answers go here, and into the detail below, as they come.

1. **`linear_puzzles`**: `true` = the puzzle gives its own gourd as in the
   vanilla game (no Archipelago item on it)? Or something else?
2. **Filler names** (Baby, Boid, Bouba…): joke items with no effect, or names
   for the gourd item?

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

### Step 2 — Traps

`trap_fill_percentage` exists and nothing happens when a trap arrives.

| Id | Trap | Who sees it | What it rests on |
|---|---|---|---|
| T1 | Weights per trap (`*_trap_weight`, as the document proposes) | — | apworld options; the item pool already makes room for traps. |
| T2 | Drop Everything, Butterfingers (hands only), Heavy Items (worn packs) | everyone | The host clearing `PlayerHeldInformation`, as the hand-over and Ctrl+R do (`ReceivedItemSpawner.ClearServerSideHold`). "Cannot hold for N seconds" means refusing pick-ups meanwhile: `UserCode_CmdPickUp` runs on the host. |
| T3 | A door slams shut for a while | everyone | Peck states are server-side; `ArchDoors`/`KeyFeatures` drive them. Must never trap a player out of logic: reopen on a timer, and never a door the seed needs shut forever. |
| T4 | Gourds scattered | everyone | Loose gourds are server-owned; `ReceivedItemSpawner.ResolveSpawnPosition` places them. Needs a list of safe spots (reachable, not in a sealed puzzle room). |
| T5 | Teleport, colour chaos (the world, or the players themselves), radio hijack | modded players | `PlayerGrease.Teleport` on the player's own machine through the `ModChannel` (as Ctrl+C); the colour writes of C1/C2 — a player's colour needs its renderer and shader property measured like any other object; `FmRadioManager` is local. |
| R9 | No Comms, Cutscene, The Mask, Whiteboard Rewrite | — | Research each: where walkie-talkies live, how the monolith cutscene is started, what The Mask is. |

### Step 3 — Colours and the world

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

### Step 4 — Goals

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

### Step 5 — Hints

- **H1 — To do.** `hint_discovered_deposit_boxes`, `hint_keys`,
  `hint_discovered_key_locations`: when a player reaches a tower, the host
  sends location scouts as hints. Tower positions are measured
  (`DebugWorldLayoutDump`); the host sees every player's position.

### Step 6 — Vanilla behaviours back

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
- Related, Partial: `require_arch_doors` — only the First Arch Door gates
  logic, Left and Right are shortcuts. `linear_arch_doors` — the vanilla
  buttons are `lock_arch_doors: disabled`.

### Step 7 — More locations

- **S1, S2 — To do.** Task 2 in `NEXT-SESSION.md`: every such prop carries a
  `savablePropGuid`, readable with `SaveManager.GetIsInInventory(guid)` — what
  a location needs. The item side exists (the island's objects are filler).
  Open design question there: what "obtaining" an object lying on the ground
  means.
- **S3 — To do.** "All Radio Stations" (seven are tracked); "<Tower>: All
  Gourds Deposited" goes with G4.

### Later — research first

- **R1.** `lock_puzzles` (`open`, `anchored`), `lock_towers`: how to hold a
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
- **R7.** `pedestal_sanity` (glow orbs), `whiteboard_hints` and the
  Whiteboard Rewrite trap: not looked at.
- **R8.** Custom tiles (`random_basic_tiles` and the tile dictionary):
  replacing tile textures, and drawing them. The document notes the copyright
  question: fan-art tiles rule out ever becoming a core world.

### Before 1.0

- **Q1.** Nobody has played a seed from the first check to the goal yet.
- **Q2.** The game page (`apworld/bigwalk/docs/en_Big Walk.md`), the setup
  guides and the YAML template describe every option.

### Tooling

- **D1.** Rewired measured on 2026-09-26: both instances listen to keyboard,
  mouse and the gamepad, and ignore input while unfocused. The split: the
  guest sets `ignoreInputWhenAppNotInFocus` off and drops keyboard and mouse
  from its player; the host drops the gamepad while a loopback guest is
  connected.
