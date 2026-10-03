# Changelog

The mod and the apworld move together: `Plugin.PluginVersion`, the `.csproj`,
`archipelago.json` and `WORLD_VERSION` carry the same number. A seed made by
an older apworld plays with a newer mod (ids never change); the other way
round, an older mod ignores what it does not know and can make a seed
unwinnable, which is why every player of a session should run the same build.

## 0.1.3 (in progress)

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
  - `gauntlet_stages_local` (on by default): keeps the seven items in your own
    world.
  - `lock_gauntlet_needs` (on by default, in `vanilla` too): with
    `lock_puzzle_needs`, the `big_goodbye` goal, and with `locked_stages` each
    stage's check, also need the parts its puzzle
    is built from, and the mod hides those parts in the seven stages until
    their item arrives. The finale and the entrance are left alone.
- **`require_arch_doors`** (on by default): what lies past the Left and Right Arch
  Doors is behind them in logic, so that nobody walks to a place and is sent its
  shortcut afterwards. Left: the Yellow, Blue and Black towers, the chapel and the
  Green Dome. Right: the Green Tower and what is past the chairlift. Told by the
  player; a list too short asks for fewer doors, never more than a seed holds. The
  two doors are also asked for early in your own world, so they are easy to find,
  while the First Arch Door is open and `lock_puzzle_needs` is off; otherwise the
  first locations are too few for them and generation can fail.
- **`joke_filler_percentage`** (20 by default): the community's thirteen wrong names for
  a Gourd (Baby, Boid, Bouba, Boyo, Butternut Squash, Child, Doodad, Jelly Baby, Peanut,
  Peg and Head, Plumbus, Red Nub, Thing) join the filler. They do nothing beyond a line
  in the item feed.
- **`hint_keys: from_start`**: the seven big keys are hinted from the first
  minute.
- **A new save gets its deposits back.** The number of gourds placed in the
  monuments is kept on the Archipelago server, per slot, and only ever goes up.
  A new save on the same slot puts that many back as the monuments load, in
  whichever slots come first, instead of leaving them to be carried over by
  hand. If the server does not answer, nothing is restored and nothing breaks.

### Fixed

- A save connected to a different seed or slot than it knew no longer sends the checks
  it reported to the previous one (U5). Its first connection still sends the ones made
  while offline.
- A big key no longer flies out of its stone when its monument fills before its
  item has arrived (the drawbridge's, then the others); it stays in place until
  the item comes. Found in play, twice.
- With `lock_puzzle_needs`, the "skip this challenge" panels (the game's own
  accessibility setting) are no longer hidden with the buttons: each shows once the
  parts of its own puzzle have arrived.

### Changed

- The channel between the host and its guests is now version 3: everyone
  needs 0.1.3.

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
