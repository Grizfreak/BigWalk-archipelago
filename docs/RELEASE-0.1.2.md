# Release 0.1.2: drafts

The tag `v0.1.2` is pushed. The release page and the Discord post are made by
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

> **Big Walk Archipelago 0.1.2 is out** 🎉
>
> The big one: **puzzle parts as items**. Turn on `lock_puzzle_needs` and the
> buttons, panels, speakers, lights, teapots, tomatoes... of the puzzles are
> not on the island until someone sends them to you. 17 items, they show up
> for everyone at once, and no puzzle is ever locked behind an item of its own.
> You start with one random part so you are never stuck in the tutorial.
>
> Also: hold any arch door closed (`start_with_arch_doors_open`), big keys land
> in your hands like gourds, a session journal for bug reports, and a fix for
> Archipelago 0.6.7 rejecting `requires` lines.
>
> ⚠️ **Everyone in a session needs the same build** this time (the co-op
> channel changed). Mod + `.apworld` are on the release page; old seeds still
> play.
>
> Known limits: the Gauntlet and the ending are untouched, and we have only
> tested on one PC with a local second instance. If something looks wrong, press
> **Ctrl+Z** right then and send us `BepInEx/session-journal.tsv`.
