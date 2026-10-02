# Changelog

The mod and the apworld move together: `Plugin.PluginVersion`, the `.csproj`,
`archipelago.json` and `WORLD_VERSION` carry the same number. A seed made by
an older apworld plays with a newer mod (ids never change); the other way
round, an older mod ignores what it does not know and can make a seed
unwinnable, which is why every player of a session should run the same build.

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
