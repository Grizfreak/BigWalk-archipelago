# Road to 1.0

The to-do list for the rest of the project, adopted on 2026-09-26. Worked
through one step at a time: pick the next item, build it, test it, tick it.
The top half is the list; the bottom half is the detail behind each item,
found by its id (X1, T2, …).

Sources: the community document "Big Walk Archipelago details" (its options
kept verbatim in [`community-options.yaml`](community-options.yaml), its items
and locations in [`community-items-locations.txt`](community-items-locations.txt); proposed
options, items and locations, custom tiles, 0.1.0 notes), and the player's
own wishes (traps, colours, where things are in the world).

---

## The list

Order is a proposal. Every option of the community document has an id here —
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

**Step 1 — Players without the mod: dropped (2026-10-02).** Every player of a
session runs the mod; a guest without it is no longer supported, so the work
that was to make one playable (X3 nothing blocks them, X4 received items they
can see, X5 a real session with a console) is deleted. What was learned stays
below: X1 (a vanilla game can only build the player and the corpse, no gourd,
no gadget) and X2 (a loopback guest without the mod, Ctrl+Y on the host).

**The rule for modded players** (kept from that step): a session where
everyone runs the mod plays as the previous version unless an option is turned
on. A change that would cost modded players something becomes an off-by-default
option, or is dropped.

## Versions planned (player, 2026-10-02)

Agreed on 2026-10-02. An item that is not named here is parked until after 0.1.8.

| Version | The core | Rides along (why) | To settle first |
|---|---|---|---|
| **0.1.3** | The Silent Gauntlet as real checks and locations, with items that open the floors (S5 and the seven stages; their needs are already in `data.GAUNTLET_STAGE_TAGS`). *Done 2026-10-03, tested solo: `gauntlet_mode`, `gauntlet_puzzles_required`, `gauntlet_stages_local`. A stage's check is the `GauntletChamberN` write, a stairway is `GourdTower_Gate All Hold`. Co-op, reload and `vanilla` tested; the Gauntlet's puzzle parts are hidden by `PuzzleNeedHider` under `locked_stages` and still to check in game against `GAUNTLET_STAGE_TAGS` (the stage lines of the log).* | U7 the game's skip aids (debug key Keypad 6 to see what they do, still to check). U1 hints for keys from the start, L7 `require_arch_doors`: apworld only, no risk. U3 a new save gets its deposits back: felt on every seed. | How a stage is detected: settled, `GauntletChamber0..6` are written when a puzzle is solved (2026-10-03). The option from the document that decouples opening a door from clearing the stage: `gauntlet_puzzles_required`. `PuzzleNeedHider` stops leaving the Gauntlet alone once its stages are in the logic. Testing: the debug keys (Keypad 4, End) fake the held buttons; only the synchronized buttons (stage 4) and the timed tomato (stage 3) need real timing. |
| **0.1.4** | Traps (T1 to T5, R9) | U8 joke filler (same pool as the traps), C1 a palette per seed and C2 colours for keys and buoys (T5's colour chaos is a colour trap) | The trap item exists (`Untied Shoelace`, hidden `trap_fill_percentage`) with no effect. An effect is local to each machine, like the hider. Whether to receive DeathLinks too (sending is on Ctrl+P). |
| **0.1.5** | Objectives to spice up a run (G1 to G6, G5 first) | H1 to H3 hints and S8 a check per tower reached (one trigger, "tower reached"), U9 the names players use for the towers | G5 lists what a seed asks for, the way `start_with_arch_doors_open` lists doors. |
| **0.1.6** | Locking the players' ways to communicate: R3 `lock_abilities` (body) and R2 `lock_pickups` (megaphone, walkie-talkie, radio) | R9's "No Comms" is the trap sibling | Input is Rewired and blocked locally, which every player can do. R2 refuses a pick-up on the host (`UserCode_CmdPickUp`). |
| **0.1.7** | More locations: S1 backpacks, S2 flares, S7 the first pick-up of each kind, S11 the objects the filler lacks | They reuse R2's host-side pick-up hook | S1, S2 and S7 overlap: settle the three together. |
| **0.1.8** | The vanilla behaviours back as options: L1 to L6, L8 | | Defaults stay "vanilla + AP" (the rule above). |

After 0.1.8, without a date: quality of life U2, U4, U5 (placed in the version that needs them), research R1b `lock_towers`, R4 one-player mode, R5, R6, R7, R8, S9, S10, and U6 PopTracker (an external pack, any time). Before 1.0: Q1 a seed played from the first check to the goal (the multiworld run in progress, if it gets there), Q2 every option described (kept up to date with each version), Q3 the defaults, Q4 test 24 (when a save has one). Tooling D1 (keyboard for the host, gamepad for the guest) only when it gets in the way.

**Step 2 — Quality of life** *(new: small, and felt on every seed)*
- [x] U1 Hints for keys from the start (`hint_keys: from_start`) — apworld only
- [ ] U2 The map shows which puzzles still hold a check
- [x] U3 A new save gets its deposits back, not only its items (DataStorage)
- [x] U4 A button in the world for the gourd resync (hub, host only); Ctrl+R stays bound. Its "nearest tower" change is the gather buttons, U11
- [x] U5 Checks remembered per seed, not per save (`ap_reported_*`)
- [ ] U6 A PopTracker pack with the island's map (Universal Tracker works today)
- [ ] U7 The game's own skip aids (`SkipAidToggler`) as an option (research)
- [x] U8 Joke filler items (the document's 13 misnamings of gourds), no effect
- [x] U9 Names players use: towers by their full name (Red Funnel Tower, …),
  the document's spellings
- [x] U10 Teleport buttons in the world (`teleport_buttons`: off / free / with_towers / items); six in the hub, a way back in each destination; solo and loopback guest tested 2026-10-04. To check: the `with_towers` and `items` modes in play, and whether the Black Tower and the Gauntlet should open with the Big Wall Door together
- [x] U11 Resync stations in the hub and each tower (button, gadgets switch, sign), `tower_resync_stations`, `guests_can_resync`; done in 0.1.3. Later: more exclusion settings (keys), a picture instead of the sign's words (U12)
- [ ] U12 Less text on screen (player, 2026-10-05): a small sign (a picture, not words) above the Resync and gather buttons; the overlay keeps "Archipelago: Connected" and, for now, the lines of received items (player: text stays for now); the resync hint goes; the goal line goes with the objectives update
- [x] Q5 Three and four players (tested 2026-10-05, docs/TESTS-0.1.3.md F): the game loads other versions of many landmarks for 3 and 4 (PlayerCountSwapper); the Gauntlet recon, the hidden puzzle parts, skip aids, Cabin Fever help buttons and the apworld's puzzle needs were all made on the 2-player version (docs/TESTS-0.1.3.md, F). Player, 2026-10-05: more players only change the layout of buttons and add walls, and give fewer tools, so the 2-player classification should hold; to check in one pass

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
- [x] L7 Places past the Left and Right Arch Doors need their door in logic
  (`require_arch_doors`)
- [ ] L8 Puzzles give their own gourd (`linear_puzzles`)

**Step 8 — More locations**
- [ ] S1 Backpacks and hip packs (`backpack_sanity`, with its `vanilla` mode)
- [ ] S2 Flares and flare stations (`flare_sanity`)
- [x] S3 Dropped (2026-10-02): "All Radio Stations" is an objective (G5), not a check; a per-tower "All Gourds Deposited" cannot be kept in logic
- [x] S4 Dropped as a location (2026-10-03): the lookout lights are the towers' door buttons, an item for R1b, not a check
- [ ] S5 The endings as checks when they are not the goal: the Big Wall bell,
  the Gauntlet, the secret ending's gourd
- [ ] S6 The game's other saved switches: the Black Tower's inner door, the
  Poet and Priest / Poet and Pontiff doors (research what each is)
- [ ] S7 A check for the first pick-up of each kind of object (the
  document's 22 "Pickups": cowbells, compass, clock, …)
- [ ] S8 A check for reaching each tower (the host sees every position)
- [ ] S9 `pedestal_sanity`: glow orbs on pedestals, `track_only` included
  (research)
- [ ] S10 Firework launchers (research: where they are, what firing one saves)
- [ ] S11 The objects the document lists and the filler lacks: clock,
  paintbrush, glow orb, scanner, cowbells

**Later — research first**
- [x] R1a `lock_puzzles: open` — built (2026-09-28), **removed 2026-10-02**: no
  puzzle sits behind an item of its own; see R1c for what replaced it
- [x] R1c `lock_puzzle_needs` — the parts a puzzle is built from are items
- [ ] R1b `lock_towers`
- [ ] R2 Lock picking things up (`lock_pickups`)
- [ ] R3 Lock abilities: jump, crouch, gestures (`lock_abilities`)
- [ ] R4 One-player mode; player limit as items
- [ ] R5 `big_climb`, `big_club`
- [ ] R6 Golf, Cabin Fever timers, black sphere puzzles (Cabin Fever done in 0.1.3: `cabin_fever_*`, help buttons; golf and the black spheres remain)
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

Answers go here, and into the detail below, as they come. None open today.

Answered (2026-09-26):

- **`linear_puzzles`, `linear_towers`**: gourds and keys stay where the
  vanilla game puts them. A cleared puzzle holds its own gourd; a filled
  tower monument gives its own key. Neither is randomized (L8, L1).
- **Filler names** (Baby, Boid, Bouba…): joke items, no effect (U8).
- **Objectives (G5)**: an option listing what the seed asks for; players add
  or remove each objective, the way `start_with_arch_doors_open` lists doors.
  Every listed objective must be done.

- **Order**: as listed. The crossplay step was dropped on 2026-10-02.
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

### Step 1 — Players without the mod: dropped

Closed on 2026-10-02: every player runs the mod, and a guest without it is not
supported any more (a console cannot run it, so there is no console player
either). X3, X4 and X5 are deleted with their notes. What was measured and is
still true:

- **X1.** A vanilla game can only build the player and the corpse: no gourd, no
  gadget. Everything a modded host spawns for its guests is for modded guests.
- **X2.** `tools/launch-guest.ps1 -Vanilla` and Ctrl+Y on the host start a
  loopback guest without the mod, joining over Kcp.
- A player without the mod that joins a modded host is still tolerated by the
  code (the `ModChannel` handshake never writes to a peer that did not say
  hello), but nothing is tested or promised for them.

### Step 2 — Quality of life

Not in the document; gathered from the leads set aside in `NEXT-SESSION.md`
and from what a whole seed will ask of players.

- **U1 — Done (0.1.3), apworld only.** `hint_keys: from_start`: the seven key
  locations go into `start_location_hints`. The other two values are H2.
- **U2 — To do.** The map already shows the purple gourds
  (`VariantGourdMapUnlocker`); it could mark each puzzle whose check is not
  yet sent, from the host's check list. Modded players only.
- **U3 — To do, scoped 2026-10-03 (design below, not coded).** Monument fill
  state in Archipelago's `DataStorage`, so a new save recovers its deposits as it
  recovers its items (today they are re-deposited by hand: the new-save recovery
  plan).
  - *What the code does today.* A deposit is `ap_home_<slot>` = 1 in the save, one
    key per monument slot (`CosmeticMonumentFillTracker`); `GetFilledMonumentCount`
    counts those keys and feeds the deposit checks, the goal and the loose-gourd
    reconciliation (`gourds received - deposited - spawned`). When a monument's
    `PropHome` streams in, `TryRestoreHome` spawns a cosmetic gourd into every
    slot whose key is set. A new save has no keys, so nothing is restored, and
    the reconciliation spawns all the gourds loose at the hub.
  - *Design.* Any gourd fits any slot (Option A), so only the NUMBER of deposits
    has to travel. (1) The host writes it to `DataStorage` under the slot, key
    `bigwalk_deposits`, with a `Max` operation: it can only go up, and a room is a
    seed, so a new seed starts empty (which also answers part of U5). (2) At
    connection it reads it; the missing amount, `stored - GetFilledMonumentCount`,
    becomes a **budget**. (3) `TryRestoreHome` spends it: an empty monument slot
    that streams in gets its `ap_home_` key set and a gourd pinned, the budget goes
    down by one. The slots chosen are whichever load first, which Option A allows.
    (4) The reconciliation counts `filled + remaining budget` as deposited, or it
    would spawn those gourds loose too, the duplication bug
    `GetFilledMonumentCount` was written to avoid. Co-op needs nothing: only the
    host holds the save and talks to the server.
  - *Why not read the checked `Gourd Deposit N` locations instead.* They are
    milestones (every 5, or none with `gourd_slot_checks: off`), so the count
    would be approximate or absent.
  - *Size.* About 150 lines: a small `DataStorage` wrapper in `ApConnection`, the
    budget in the tracker, one line in the reconciliation. No apworld change, no
    protocol change.
  - *Risks and what to check in game.* (a) The stored number must never exceed
    the gourds received: it comes from this slot's own deposits, so it cannot.
    (b) A slot that streams in late: the budget survives until it is spent, and a
    session ending first keeps the keys already written. (c) A save that deposits
    while the budget is pending: the count only goes up, so `Max` stays right.
    (d) MultiClient 6.7.1's `DataStorage` API on a room with no storage yet.
    Test: deposit N gourds, start a NEW save on the same slot, reconnect, and walk
    to the monuments; N slots must be full and no extra gourd loose.
- **U4 — To do, refined 2026-10-03.** A real in-world button for the gourd resync,
  instead of the Ctrl+R that only the host knows about. First, a change to Ctrl+R
  itself (player's wish): the gourds come back at the tower NEAREST to where it is
  pressed, not always at the hub; later the key goes away for a reset button.
  Both ride on `Core/WorldButtons`, the mod's own buttons (U10), which do not use the
  game's press system: a copy of one of its buttons keeps the original's network
  reference (`ShellReference.ticket`) and every press reaches the original.
- **U5 — To do.** `ap_reported_*` is not scoped to a seed, so a save
  reconnected to another seed resends checks earned elsewhere. Scope it by
  the seed name the room sends.
- **U6 — To do.** A PopTracker pack: the island's map with every location on
  it, items and doors. Universal Tracker already works without a YAML.
- **U7 — Answered (2026-10-03).** The game's skip aids are the host's accessibility
  setting "Sauter les défis: Avec / Sans" (`SaveData.skipAidsActive`, sent to guests
  in the welcome message). With it on, 51 places (almost every puzzle, and each of
  the Gauntlet's seven chambers) show a "Passer ce défi ?" panel with two buttons
  held together. Measured: holding them sets the puzzle's `GourdValetNetworkObject`
  and sends its check, as solving it does. No Archipelago option is needed: the
  player picks it in the menu. A pole is no longer hidden with the "buttons" (their
  names match some puzzles' buttons): it waits for the needs of ITS OWN puzzle, found
  from the parts in the same folder, and shows when the logic counts on that puzzle
  (`PuzzleNeedHider.BuildSkipNeeds`). A puzzle split over several folders may show its
  pole early, never late. Debug: Keypad 6 switches the aids on in a running game.
- **U8 — To do.** Joke filler items (player, 2026-09-26), the document's
  "misnamings of Gourds": Baby, Boid, Bouba, Boyo, Butternut Squash, Child,
  Doodad, Jelly Baby, Peanut, Peg and Head, Plumbus, Red Nub, Thing. No effect
  beyond their line in the item feed. Mixed with today's filler, the island's objects; their share could be
  an option. apworld: new ids and names; mod: a feed line, nothing spawned.
- **U10 — Prototype under test (2026-10-03).** The player's spec: in each tower (Red,
  Green, Blue, Yellow, the Green Dome) and in the Gauntlet a button back to the hub,
  and in the hub one button per destination. A copied game button does not work (it
  shares the original's `ShellReference`); `Core/WorldButtons` builds its own (two
  primitives, a game material, the game's "use" input while aimed at, the mod's
  existing `PlayerGrease.Teleport`), nothing networked, every machine builds the same
  table. Open: whether a destination unlocks on a first visit on foot (recommended, so a
  teleport never skips a lock the logic assumes) or is free; the twelve positions.
  Original idea: Physical buttons placed in the world, for
  the players to teleport between the island's zones, and the place for U4's
  resync button too. The mod already puts objects the game has none of into the
  world by cloning vanilla ones (the cosmetic gourds, the gadget items, the
  Archipelago templates of `GadgetItemSpawner`), and a button is a `PeckSwitch`
  on a prefab like `BasicPushButton`, so a clone whose peck calls the game's
  own player teleport (what the loopback summon, Ctrl+C, uses) looks possible.
  To settle: that a cloned switch works in co-op (the object must exist on every
  machine and the peck reach the host), which zones, where each one stands, and
  whether a teleport may skip a locked region (it must not: the logic assumes
  the player walked there). Modded players only, off by default.
- **U9 — Partly done (0.1.3): the towers' keys, cuts and deposits take the document's tower names (Red Funnel Tower Key, ...). Key deposits are still named by tower, not by what they open, and the three puzzle spellings are untouched.** Original note: The document's names against ours. Towers: it says Red
  Funnel, Green Cup, Blue Castle, Yellow Twist, Black Monolith, Green Dome
  Tower; our locations say "Red Tower Key Deposit" and so on. Key deposits:
  it names them by what they open (Map Room Key Deposit, Chairlift Station,
  Train Station, Underground Tunnel, Wall, Tutorial); ours by tower. Puzzles
  match but for three spellings (Sim Press, Pointers', Fish Trap). Ids do not
  move, so the mod is untouched; a YAML naming a location (exclude_locations,
  plando) would need the new name, so do it once, before 1.0, with the old
  names accepted where Archipelago allows.

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
- **G5 — To do.** The user's idea (2026-09-26): instead of one
  goal, a list of objectives the YAML adds or removes one by one, the way
  `start_with_arch_doors_open` lists doors — endings (`big_wall` — the
  document's `big_bell`, worth an alias —, `big_goodbye`, `big_game`, then
  G1's `big_walk` and R5's `big_climb`, `big_club`) and collectibles
  (`big_collection`, then G2's keys and radio stations). G3's "and" is then
  just a list with both kinds; its "then" stays an extra ordering. The mod
  can detect each of today's four goals but watches only the chosen one
  (`ApRuntime`, `switch (slotData.Goal)`): it would watch every listed one
  and send the goal once they are done. Every listed objective is required
  (answered 2026-09-26): choosing is done by adding or removing. The old
  `goal` is kept hidden and read into the list, as `lock_arch_doors` was.
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
  (`disabled`). On (answered 2026-09-26): filling a tower's monument gives
  its own key, as in the vanilla game, and the key is not randomized — the
  vanilla behaviour `KeyCustody` holds back today. The document's
  `one_deposit_box` (the key after one random slot) is a variant to weigh.
  apworld: the key items leave the pool and the monument's last deposit
  holds its key as a locked item, so the location count does not move.
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
- **L7 — Done (0.1.3), on by default.** `require_arch_doors`, from the player's
  account of the map: the Left door gates the Yellow, Blue and Black towers, the
  chapel (`ending`) and the Green Dome; the Right door the Green Tower and the
  chairlift zone (purple gourds). The Red Tower is not behind the Right door: it is
  reachable early, only the tunnel route is hard. Puzzles and radio stations past a
  door beyond those zones are still unmeasured. **Nothing is technically blocked**
  (player, 2026-10-03): without a door some routes are only very long, so the rule is
  a comfort and a wrong classification can never make a seed unbeatable. A page to
  classify the 45 puzzles (tower and door each) was tried and the player did not like
  the method; 42 answers are kept in its database (artifact
  `8V5tv8RhXXEcJPEhy7d2Pj`, collection `puzzles`: Left for Breadcrumb Loop, Carousel,
  Concert, Egg Hunt, Kick Up Pits, Microphone Array, Observation Room, Coordinates;
  Right for Cabin Fever Long, Cannonball Commute, Centurion Seance, Charades Rooms,
  Dancer and Selecter, Poet and Pontiff, Speed Obby; the rest free). To redo later, on a
  MAP rather than a list: the puzzles, towers, lookouts and doors at their coordinates,
  zones drawn on it, every point inheriting its zone. The same map would carry the
  lights and objects of the lightsanity and objectsanity. Earlier plan: Putting
  the far doors in logic spares the walk back when a shortcut's item arrives late.
  Plan: `require_arch_doors`, off by default, starting with whole towers (their
  deposits, cuts and keys) and leaving puzzles and stations for a later measure; a
  short list never makes a seed harder than needed. *Asked of the player, to be
  reminded of: which towers lie past the Left door (towards Sports Creek) and
  past the Right door (the tunnel).* Earlier note: `require_arch_doors`: today only the First
  Arch Door gates logic, Left and Right are shortcuts, so a seed may send a
  player the long way round. `true` puts the places past each far door behind
  that door in logic — which needs those places listed: which puzzles, keys
  and stations are "past" the Left and Right doors. Only meaningful for doors
  that start closed.
- **L8 — To do.** `linear_puzzles` (answered 2026-09-26): on, a cleared
  puzzle holds its own gourd, as in the vanilla game, and gourds are not
  randomized. `false` is today (a puzzle is a check, its gourd an item).
  apworld: each puzzle location holds its gourd as a locked item; mod: let
  the game release the puzzle's gourd instead of retiring it
  (`PuzzleGourdRetirer`) and spawning a received one.

### Step 8 — More locations

- **S1, S2 — To do.** Task 2 in `NEXT-SESSION.md`: every such prop carries a
  `savablePropGuid`, readable with `SaveManager.GetIsInInventory(guid)` — what
  a location needs. The item side exists (the island's objects are filler).
  Open design question there: what "obtaining" an object lying on the ground
  means.
- **S1** also has a `vanilla` mode in the document: each backpack is a
  location that gives itself, and no other location gives one. **S2** covers
  the flare stations as well as the flares.
- **S3 — Dropped (2026-10-02).** "All Radio Stations" would be an objective, a G5 candidate, not a check. "<Tower>: All
  Gourds Deposited" cannot be kept in logic: gourds go in any tower in any
  order and never come out.
- **S4 — Dropped as a location (2026-10-03), kept for R1b.**
  `LookoutLightRed/Green/Blue/Yellow` (`SavableSystem` 21–24) are the buttons at
  the door of four of the towers, which light the tower and open its door.
  Measured: pressing the Red Cone's wrote `LookoutLightRed (21) = 1` at
  (-87, 86, -617), next to its `lighting and door/BasicPokeButton`
  (`PokeButtonPeckLogic`, `savableSystem` = LookoutLightRed) and `BasicDoor`.
  The player does not want opening a tower to be a location, but an
  Archipelago item: so these flags are what `lock_towers` (R1b) holds shut
  until its item arrives, the way `ArchDoors` does for the hub's doors (the flag
  is a TrackedPeckState, and refusing its state going up covers every way of
  opening it). The towers, by their `Lookouts/Lookout_<name>` folder and the
  middle of their objects (x, y, z): Red Cone (-89, 101, -611), Yellow Zigzag
  (-229, 136, -238), Blue Tube (139, 116, -204), Green Hourglass (456, 117, -447).
  The firework launchers on the roofs are not these: their state is not saved.
  To do for R1b: the other three towers' buttons, and the three towers that have
  no `LookoutLight` flag.
- **S5 — To do.** `EndingGate` (the Big Wall bell) and `GauntletComplete` are
  already detected for the goals; when they are not the goal they could be
  checks. Same for the 46th `RewardGourd` of the secret ending. On a seed that
  goals on one of them, it stays the goal event, not a check.
- **S6 — Moved out of 0.1.3 (2026-10-03), with the doors and towers.** Probably
  doors that open once, so items for R1b and the arch doors rather than checks (the
  player does not want an opening as a location). To settle by playing the three
  places with the `[SystemWrites]` log, which named the lookout lights in a minute.
  `BlackTowerInteriorDoor` (26), `PoetAndPriestDoors` (60),
  `PoetAndPontiffDoors` (61): saved switches, the last two next to known
  puzzles (`gourdPoetAndPreist`, `gourdPoetAndPontiff`). Find what each one is
  before deciding whether it is a check.
- **S7 — Decision, with a proposal.** Task 2 in `NEXT-SESSION.md`: every
  object has a `savablePropGuid`. The document answers what "obtaining" one
  means: one location per KIND, the first pick-up of any of them — Backpack,
  Binoculars, Clock, Compass, the three Cowbells, Flare Gun, Flashlight, Glow
  Orb, Gourd Holder, Hip Pack, Key, Laser Pointer, Map, Megaphone, Paintbrush,
  Purple Gourd, Radio, Red Gourd, Scanner, Walkie Talkie Pickup. Pick-ups go
  through `UserCode_CmdPickUp` on the host (R2), so every player counts. Overlaps S1/S2: settle the three together.
- **S8 — To do.** A check for reaching each tower, on H1's trigger.
- **S9 — Research.** `pedestal_sanity`: the glow orbs on pedestals, not looked
  at. `track_only` (no location, but the multiworld remembers which are lit)
  needs DataStorage, like U3.
- **S10 — Research.** Firework launchers, listed by the document with no
  detail. No class and no `SavableSystem` value names them, so they are scene
  objects: find them (Ctrl+J away from the hub) and what firing one changes.
- **S11 — Research.** The document's inventory items against today's filler
  (`data.FILLER_ITEMS`). Already there under other names: Flashlight (Torch),
  Hip Pack (Belt), Gourd Holder (Gourd Carton), Map (Folding Map), Laser
  Pointer (Laser). Missing: Clock (`ClockProp`, not usable held), Paintbrush
  and Glow Orb (not in the hub's dump — the Ctrl+J inventory only covered
  what the hub loads), Scanner (perhaps `CoordinateTrackerProp`, or the X-Ray
  Goggles), the cowbells. Each needs a vanilla instance to clone.

### Later — research first

- **R1a — Built (2026-09-28), removed (2026-10-02).** `lock_puzzles: open`
  gave every puzzle an Unlock item of its own, logic-only. The decision, once
  it was running: no puzzle is blocked behind an Archipelago item, only by the
  objects it needs to be solved — which is R1c. The 41 items, the option and
  its tests are gone; ids 10000 to 10159 stay unused so that no id a seed may
  carry ever changes meaning. A YAML that still sets `lock_puzzles` is only
  warned about by Archipelago.
- **R1c.** `lock_puzzle_needs` + `start_with_puzzle_needs`, logic-only like
  R1: what a puzzle is built from (17 needs: buttons, synchronized buttons,
  icon/drawing/pose/sound/point panels, speakers, lights, teapots, timed
  tomato, golf ball, big head mask, ink viewer, counter, coordinates
  computer, eggs) becomes an item, and a puzzle needs all of its own. `data.PUZZLE_TAGS`
  holds the player's 2026-10-02 pass per puzzle, `coop`/`throwable`/`item_pickup`
  included as tags without items (a head count and player abilities, for
  R2/R3/R4); the Gauntlet's stages are recorded in `GAUNTLET_STAGE_TAGS`,
  unused. The tutorial's four stay free. Interpretations to confirm in
  `data.py`: Carousel's "panel buttons", Tile Thief's "speaker panels",
  "in logic with coordinates computer".
  **Mod side, 2026-10-02 (works solo and with a guest):** `Core/PuzzleNeeds` +
  `Core/PuzzleNeedHider` hide the objects of every need whose item has not
  arrived (`SetActive(false)` locally on each machine, rechecked every second),
  classified by prefab name read off the game (Ctrl+E). The host keeps the
  ledger (`ap_need_<key>`) and tells guests the locked list in its snapshot
  (ModChannel protocol v2). Debug: Ctrl+Q locks/unlocks everything, Ctrl+W the
  next need, Ctrl+E names what is under the crosshair, Ctrl+D noon.
  `tools/players/needs-test.yaml` is the seed; "Icon Panels Unlock" etc. can be
  cheated in with `!getitem` from a second TextOnly client.
  Still to check: 8 `Light_Table_Red` found where 5 were expected, 37 eggs for
  36, the `Intercoms` container hidden whole, the 12 train-car buttons (not
  hidden: unknown if puzzle or train), Tile Thief's mixed tiles, and the
  Gauntlet (left alone: its needs are not in the logic). The tiles are hidden,
  the boards and the split-flap displays stay.
- **R1b.** `lock_towers`
  (`progressive_vanilla`, `progressive_closest`, `random`): `anchored`
  needs a puzzle-to-nearest-tower mapping that does not exist yet in
  data.py. The document's items: one "<Tower> Unlock" per tower, the
  tutorial's deposit boxes included.
- **R2.** `lock_pickups`: refuse in `UserCode_CmdPickUp` on the host; reaches
  every player, modded or not. The document's 19 "Unlock" items (`individual`):
  Backpacks, Binoculars, Clocks, Compasses, Flare Guns, Flashlights, Glow
  Orbs, Gourd Holder, Hip Packs, Keys, Laser Pointers, Map, Megaphones,
  Paintbrushes, Purple Gourds, Radios, Red Gourds, Scanners, Walkie Talkies.
- **R3.** `lock_abilities`: input is Rewired (measured with Ctrl+N); blocking
  an action is local, so every player needs the mod (they all run it). The
  document's ten: Jumping, Crouching, Chair Sitting, Floor
  Sitting, and Pointing, Raising, Extending for each hand (`handed`; one item
  per pair with `ambidextrous`).
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

Every option of "Big Walk Archipelago details" (kept verbatim in
[`community-options.yaml`](community-options.yaml)), and where it lives. Status as
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
| `linear_puzzles` | L8 | To do |
| `linear_arch_doors` | L6 | Research |
| `linear_towers` | L1 | `disabled` only |
| `linear_keys` | L2 | `false` only |
| `linear_big_goodbye` | R6 | Research |
| `single_player_mode`, `lock_number_of_players` | R4 | Research |
| `lock_arch_doors` | — | Done, as `start_with_arch_doors_open` (0.1.2) |
| `require_arch_doors` | L7 | `false` only |
| `lock_map_room` | L3 | `true` only |
| `lock_puzzles` | R1a, R1b | Dropped: puzzles are not locked behind items (R1c instead) |
| `lock_towers` | R1b | Research |
| `fast_golf`, `cabin_fever_time`, `cabin_fever_long_time` | R6 | Research |
| `include_big_game_puzzles` | L4 | `from_start` only |
| `pedestal_sanity` | S9 | Research |
| `backpack_sanity` | S1 | To do |
| `flare_sanity` | S2 | To do |
| `cut_key_sanity` | L5 | `all` only |
| `lock_abilities` | R3 | Research |
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

### The items and locations tab

From [`community-items-locations.txt`](community-items-locations.txt).

| Group | Id | Status |
|---|---|---|
| Abilities (10) | R3 | Research |
| Item Unlocks (19) | R2 | Research |
| Inventory Items (16) | S11 | Filler has 11 of them under other names |
| Arch Doors (3) | — | Done (0.1.2) |
| Tower Unlocks: "<Tower> Unlock" | R1 | Research |
| Tower Unlocks: "<Tower> Key" | — | Done (the big keys: door and key, two items) |
| Traps (7) | T2, R9 | To do / Research |
| Filler, misnamings of Gourds (13) | U8 | To do |
| Item Pickups (22) | S7 (S1, S2) | Decision |
| Puzzles (45) | — | Done, all 45; three spellings differ (U9) |
| Gourd Deposit Boxes, per tower | G4 | Aggregated today (45 deposit checks, one counter) |
| "<Tower>: All Gourds Deposited" | S3 | To do |
| Key Cutters (25) | L5 | Done |
| Key Deposits (7) | — | Done; named by tower, not by what they open (U9) |
| Radio Stations (7) | — | Done, named from the save data as asked |
| All Radio Stations | S3 | To do |
| Firework Launchers | S10 | Research |
