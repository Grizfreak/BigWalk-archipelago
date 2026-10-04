# Changelog

The mod and the apworld move together: `Plugin.PluginVersion`, the `.csproj`,
`archipelago.json` and `WORLD_VERSION` carry the same number. A seed made by
an older apworld plays with a newer mod (ids never change); the other way
round, an older mod ignores what it does not know and can make a seed
unwinnable, which is why every player of a session should run the same build.

## 0.3.0 (2026-10-05)

Numbered 0.3.0 rather than 0.1.3: a release this size moves the minor number; the next ones are 0.4, 0.5...

### Changed

- **Options sorted into five groups**: Goal, Sanity, Logic, QoL, Puzzle QoL.
- **`lock_puzzle_needs` is on by default**: the parts puzzles are built from are items to find.
- Puzzle needs checked against what the mod really hides in each puzzle's place: the validators'
  buttons for Fielding, both Obbies, Blindfold Catwalk, Optical Telegraph and Indoor Semaphore, the
  Timed Tomato for the Obby, icon panels for Indoor Semaphore, pose panels for the Charades Rooms.
  A seed made before this may put one of them in logic a little early.
- With `lock_puzzle_needs`, Buttons, Synchronized Buttons and Icon Panels are asked for early
  (in any world), so that a solo seed with the defaults always generates (1 in 23 failed).
- **Renamed**: `gourd_slot_checks` is now `gourd_sanity`, `radio_checks` is now `radio_sanity`.
  A YAML with the old names falls back to the defaults: rename them.
- **The towers' keys and checks carry the names the players use** (ROADMAP U9): **Red
  Funnel Tower Key**, **Green Cup Tower Key**, **Blue Castle Tower Key**, **Yellow Twist
  Tower Key**, **Black Monolith Tower Key** and **Green Dome Tower Key**, with their
  cuts and deposits (**Red Funnel Tower Key Cut 1**, **Red Funnel Tower Key Deposit**).
  Ids did not move, so a seed made by 0.1.2 still plays, but a YAML or a plando that
  names one of these locations or items needs the new name.
- The channel between the host and its guests is now version 8: everyone
  needs 0.3.0.
- Ctrl+R is gone: the resync stations do it.

### New

- **The Silent Gauntlet** (`gauntlet_mode`, `vanilla` by default). With
  `locked_stages`, each of the seven stages is a check (solving its puzzle)
  and its way up is an item: **Gauntlet Stage 1 Door** to **Stage 7 Door**.
  The buttons that opened the stairways when everyone held them are gone, and a
  stage's wall and stairway stay shut until its item arrives. The `big_goodbye` goal needs all
  seven.
  - `gauntlet_puzzles_required` (on by default): on, the puzzle is still needed
    to open the wall inside the stage, which then also waits for the stage's
    item. Off, the item opens the wall at once, so the puzzles can be done in
    any order, or skipped; they stay checks.
  - `gauntlet_stage_items` (`progressive` by default): seven **Progressive Gauntlet Doors**, each
    opening the next stage up; `individual` keeps one item per stage, found in any order.
  - `gauntlet_stages_local` (on by default): keeps the seven items in your own
    world.
  - `lock_gauntlet_needs` (on by default, in `vanilla` too): with
    `lock_puzzle_needs`, the `big_goodbye` goal, and with `locked_stages` each
    stage's check, also need the parts its puzzle
    is built from, and the mod hides those parts in the seven stages until
    their item arrives. The finale and the entrance are left alone.
- **`require_arch_doors`** (on by default): what lies past the Left and Right Arch
  Doors is behind them in logic, so that nobody walks to a place and is sent its
  shortcut afterwards. Left: the Yellow, Blue and Black towers and the Green
  Dome; never the chapel, so the end of the game needs only the Big Wall Door. Right: the Green Tower and what is past the chairlift. Told by the
  player; a list too short asks for fewer doors, never more than a seed holds. The
  two doors are also asked for early in your own world, so they are easy to find,
  while the First Arch Door is open and `lock_puzzle_needs` is off; otherwise the
  first locations are too few for them and generation can fail.
- **`hint_keys: from_start`**: the seven big keys are hinted from the first
  minute.
- **Teleport buttons** (`teleport_buttons`, `off` by default). Buttons in the world, set into
  the walls: in the hub one for each of the Red Funnel, Green Cup, Blue Castle, Yellow Twist and
  Black towers and the Silent Gauntlet, and one back to the hub inside each of them. The
  Green Dome has none, being next to the spawn. `free`: all there from the start.
  `with_towers`: a tower's button once its door has been opened by the button at its foot (the
  Black Tower's once the two buttons at its top have been held together, the Gauntlet's once the chapel has been opened). `items`: the same, and each also
  needs its own **Teleporter** item, six of them, useful and never required. A teleport can skip a lock the
  logic does not count on, so none of this is logic. Guests can press them too. `teleport_back_to_hub` (off by default) adds a way back in each destination.
- **`tile_thief`** (`vanilla` by default): a button beside Tile Thief that makes new peg tiles in
  front of it, for every player: `easy` the ones the puzzle expects, `chaos` one of every kind it
  draws from. With `lock_puzzle_needs` it waits for the puzzle's panels.
- **`shuffle_peg_tiles`** (off by default): the island's peg tiles swap places on their stands,
  within each place, differently for each seed.
- **`open_black_tower`** (on by default): the Black Tower's door at its foot is open from the
  start, instead of waiting for the monuments of the hub and the four towers to be full.
- **Cabin Fever waits** (`cabin_fever_time`, `cabin_fever_long_time`: `vanilla` by default,
  `reduced`, `random_between`, `fixed`, with `_seconds_min`, `_seconds_max` and `_seconds`), each
  with a hidden help button (`cabin_fever_help`, `cabin_fever_long_help`) that takes ten seconds
  off a running wait.
- **Resync stations replace Ctrl+R**: in the hub and in each of the five towers, a button that
  takes back every gourd, key and gadget of yours that is lying around and puts them in front of
  itself, a toggle beside it to leave the gadgets out, and a sign saying what the button does.
  Host only unless `guests_can_resync` (off by default), and one resync at a time on the whole
  map. `tower_resync_stations` (off by default) adds the five towers' to the hub's.
- **A new save gets its deposits back.** The number of gourds placed in the
  monuments is kept on the Archipelago server, per slot, and only ever goes up.
  A new save on the same slot puts that many back as the monuments load, in
  whichever slots come first, instead of leaving them to be carried over by
  hand. If the server does not answer, nothing is restored and nothing breaks.

### Fixed

- Universal Tracker rebuilds a seed with its `gauntlet_stage_items` and `teleport_buttons`, and a
  seed made before progressive Gauntlet doors with one door per stage (it was never in go mode).
- A save connected to a different seed or slot than it knew no longer sends the checks
  it reported to the previous one (U5). Its first connection still sends the ones made
  while offline.
- A big key already put in its plinth stays there when its item arrives later (it was taken
  out to be handed over, and the drawbridge went back up at the end of a run).
- A big key no longer flies out of its stone when its monument fills before its
  item has arrived (the drawbridge's, then the others); it stays in place until
  the item comes. Found in play, twice.
- With `lock_puzzle_needs`, the "skip this challenge" panels (the game's own
  accessibility setting) are no longer hidden with the buttons: each shows once the
  parts of its own puzzle have arrived.

### Dropped

- "All Radio Stations" as a check (it is an objective, for 0.1.5) and "<Tower>:
  All Gourds Deposited" (gourds go in any tower in any order, so it cannot be
  kept in logic).

## 0.1.2

### New

- **Puzzle parts** (`lock_puzzle_needs`, off by default). What a puzzle is
  built from becomes an item: Buttons, Synchronized Buttons, Icon, Drawing,
  Pose, Sound and Point Panels, Speakers, Lights, Teapots, Timed Tomato, Golf
  Ball, Big Head Mask, Ink Viewer, Counter, Coordinates Computer and Eggs
  (17). A part is not on the map until its item arrives, and then appears for
  every player. Nothing blocks a puzzle itself, and the generator counts on a
  puzzle only once it holds every part it needs.
  - `start_with_random_puzzle_need` (on by default): one random part at the
    start, so a tutorial puzzle is always doable. `start_with_puzzle_needs`
    lists parts to start with. Turning the random one off without listing a
    part a tutorial puzzle can use stops generation with a clear message.
  - Left alone on purpose: door handles, the train's controls, the hub gates,
    the lookouts, the radio stations, the Silent Gauntlet and the end of the
    game.
  - A line in the corner says which parts are still missing, for the host and
    its guests.
- **`start_with_arch_doors_open`**: any set of the hub's three arch doors can
  be held closed until their item arrives, the First Arch Door alone included.
  Replaces `lock_arch_doors`, which is still read from old YAMLs and seeds.
- **Big keys arriving during play go into somebody's hands**, like a gourd
  does, instead of landing two metres ahead of whoever the mod picked.
- **A session journal**, `BepInEx/session-journal.tsv`: one line per event
  (connection, items received and from whom, checks sent, parts opened, world
  loaded or left) with the time and where the player stood. It is only ever
  added to, rotates at 4 MB and survives restarts, unlike `LogOutput.log`.
- Debug keys (with `Debug.Enabled`): Ctrl+E names what is under the crosshair,
  Ctrl+Z logs the state of every part, Ctrl+Q and Ctrl+W lock and unlock parts
  by hand, Ctrl+D fixes the time at noon, Ctrl+P sends a DeathLink.

### Changed

- **The towers' keys and checks carry the names the players use** (ROADMAP U9): **Red
  Funnel Tower Key**, **Green Cup Tower Key**, **Blue Castle Tower Key**, **Yellow Twist
  Tower Key**, **Black Monolith Tower Key** and **Green Dome Tower Key**, with their
  cuts and deposits (**Red Funnel Tower Key Cut 1**, **Red Funnel Tower Key Deposit**).
  Ids did not move, so a seed made by 0.1.2 still plays, but a YAML or a plando that
  names one of these locations or items needs the new name.
- The co-op channel between a host and its guests is protocol v2. **A guest on
  another build ignores its host.** Everyone in a session needs the same
  build.

### Removed

- `lock_puzzles` (a per-puzzle Unlock item), which was only ever in
  development builds: no puzzle sits behind an item of its own any more, only
  behind the parts it is built from (`lock_puzzle_needs`). A YAML that still
  sets it is only warned about.

### Fixed

- The `.apworld` lacked `version` and `compatible_version` in its manifest,
  which Archipelago 0.6.7 requires: it loaded the world as version 0.0.0 and
  refused any YAML with a `requires` line. `apworld/build.py` now writes them
  into the package.

## 0.1.1

The first alpha's feedback: sealed-box pick-ups, belts, gourd colours, items
going to every player in turn, arch doors as items, co-op testable on one PC
with a second local instance (Ctrl+L). Test 24 of `docs/COOP-TESTS.md` (a
puzzle gourd stowed in a bag) was built and never run.

## 0.1.0

The first alpha.
