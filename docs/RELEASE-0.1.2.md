# Release 0.1.2: drafts

The tag `0.1.2` is pushed (no "v", like `0.1.0` and `0.1.1`). The release page and the Discord post are made by
hand from the texts below. The assets are in `dist/`:

| File | What | SHA-256 |
|---|---|---|
| `BigWalkArchipelago-0.1.2.zip` | the mod, BepInEx included, for every player | `36b5be003e31fea6e573c85e4fe2f3e206da37ffb405360bbca9aa8b64d5a599` |
| `bigwalk.apworld` | the world, for whoever generates | `6cb6240a393b9d9a87084f2c01d7e00e8bf2661c41b41a463a2ee0a265ff3331` |

The tag points at the commit that carries these checksums. The version number is
already 0.1.2 in the plugin, the `.csproj`, `archipelago.json` and
`WORLD_VERSION`.

---

## GitHub release notes

Same shape as the 0.1.1 page.

```markdown
## Big Walk Archipelago 0.1.2 (alpha)

Update both files: the mod zip for every player, the apworld for whoever generates. **Everyone in a session must run the same build of the mod.**

### Added
- `lock_puzzle_needs` option (off by default): what a puzzle is built from becomes an item. Buttons, Synchronized Buttons, Icon / Drawing / Pose / Sound / Point Panels, Speakers, Lights, Teapots, Timed Tomato, Golf Ball, Big Head Mask, Ink Viewer, Counter, Coordinates Computer and Eggs are not on the island until their item arrives, then they appear for every player. No puzzle is ever behind an item of its own.
- `start_with_random_puzzle_need` (on by default) gives one random part at the start so a tutorial puzzle is always doable, and `start_with_puzzle_needs` lists parts to start with.
- A line in the corner says which puzzle parts are still missing, for the host and its guests.
- `start_with_arch_doors_open` option: any of the hub's arch doors, the first one included, can be an item. It replaces `lock_arch_doors`.
- A session journal, `BepInEx/session-journal.tsv`: every item, check and opened part with the time and where you stood. Send it with a bug report.
- Debug keys (with `Debug.Enabled`): Ctrl+E names what is under the crosshair, Ctrl+Z logs the state of every part, Ctrl+Q / Ctrl+W lock and unlock parts, Ctrl+D fixes noon, Ctrl+P sends a DeathLink.

### Changed
- A big key received while you play goes into somebody's hands, like a gourd, instead of landing in front of whoever the mod picked.
- The channel between a host and its guests changed: a guest on another build ignores its host and sees every part.

### Fixed
- Archipelago 0.6.7 read the apworld as version 0.0.0 and rejected any YAML with a `requires` line.

### Known limits
- The Silent Gauntlet and the end of the game are left alone: their parts are never hidden.
- 12 buttons in the train cars are not hidden (we do not know whether they are puzzle buttons or train controls).
- Tested on one PC with a second local instance, not on two machines. A puzzle gourd stowed in a bag (0.1.1's fix) is still untested.

0.1.1 seeds still work with this mod; the new options need a seed generated with the 0.1.2 apworld.
```

---

## Discord

Same shape as the 0.1.1 post.

```markdown
# Big Walk AP - 0.1.2 (alpha)
**THE APWORLD AND MOD** -> https://github.com/Grizfreak/BigWalk-archipelago/releases/tag/0.1.2
Setup Guide is available here -> https://github.com/Grizfreak/BigWalk-archipelago/blob/master/SETUP.md
The big one: the parts of a puzzle can now be items. And everyone needs the same build this time, see **Updating**.

## Added
-   `lock_puzzle_needs` option (off by default): buttons, panels, speakers, lights, teapots, the tomato, the golf ball, the mask, the ink viewer, the counter, the coordinates computer and the eggs are not on the island until their item reaches you, then they show up for everyone. 17 items. No puzzle is ever locked behind an item of its own.
-   `start_with_random_puzzle_need` (on by default) gives you one random part to begin with, so a tutorial puzzle is always doable. `start_with_puzzle_needs` lists more.
-   A line in the corner tells you which puzzle parts are still missing.
-   `start_with_arch_doors_open` option: any of the hub's arch doors, the first one included, can be an item. It replaces `lock_arch_doors`.
-   A session journal, `BepInEx/session-journal.tsv`: every item, check and opened part with the time and where you stood.

## Changed
-   A big key received mid-game goes into somebody's hands, like a gourd, instead of landing in front of whoever the mod picked.
-   The channel between the host and its guests changed: a guest on another build ignores its host.

## Fixed
-   Archipelago 0.6.7 read the apworld as version 0.0.0 and rejected any YAML with a `requires` line.

## Known limits
-   The Silent Gauntlet and the end of the game are left alone.
-   12 buttons in the train cars are not hidden (we don't know if they are puzzle buttons or train controls).
-   Only tested on one PC with a second local instance. Tell us if you see anything odd on two machines!

## Updating
-   **Everyone** reinstalls the mod zip, **and everyone must run the same build**; the host's generator gets the new `bigwalk.apworld`.
-   A 0.1.1 seed keeps working with the new mod. The new options need a new seed.
-   Bug reports: `BepInEx/LogOutput.log` and `BepInEx/session-journal.tsv` from the host, and from any player who saw the problem. Press **Ctrl+Z** (needs `Debug.Enabled`) the moment something looks wrong: it leaves a timestamped marker. <-- it will help me to debug your case if you have any problems
```
