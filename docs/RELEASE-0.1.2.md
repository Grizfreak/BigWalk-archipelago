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

**Big Walk Archipelago 0.1.2**

New: the parts a puzzle is built from can be items.

With `lock_puzzle_needs` on (off by default), a puzzle's buttons, panels,
speakers, lights, timer and so on are not on the island until their item
reaches you. There are 17 of them: Buttons, Synchronized Buttons, Icon /
Drawing / Pose / Sound / Point Panels, Speakers, Lights, Teapots, Timed Tomato,
Golf Ball, Big Head Mask, Ink Viewer, Counter, Coordinates Computer and Eggs.
They appear for every player at once. No puzzle is ever blocked behind an item
of its own: it is simply missing parts, and the generator only counts on it
once you hold them all. You start with one random part so a tutorial puzzle is
always doable (`start_with_random_puzzle_need`); `start_with_puzzle_needs`
adds more. A line in the corner says which are still missing.

Also:

- `start_with_arch_doors_open`: hold any of the hub's three arch doors closed,
  the First one included. It replaces `lock_arch_doors`, which old YAMLs and
  seeds still use.
- A big key that arrives while you play goes into somebody's hands, like a
  gourd, instead of landing in front of whoever the mod picked.
- A session journal (`BepInEx/session-journal.tsv`): every item, check and
  opened part with the time and where you stood. Handy for bug reports.
- Debug keys, behind `Debug.Enabled`: Ctrl+E (what am I looking at), Ctrl+Z
  (state of every part), Ctrl+Q / Ctrl+W (lock or unlock parts), Ctrl+D (noon),
  Ctrl+P (send a DeathLink).

Fixed:

- The `.apworld` now carries `version` / `compatible_version` in its manifest.
  Without them Archipelago 0.6.7 read the world as 0.0.0 and rejected any YAML
  with a `requires` line.
- A radio station's button could be hidden with the puzzle buttons near it.
- The slot's settings survive a trip back to the menu.

**Upgrading**

- Replace the mod and the `.apworld`. Old seeds still play (ids never change).
- **Everyone in a session must run the same build.** The channel between host
  and guests changed: a guest on another build ignores its host and sees
  every part.
- Old YAMLs keep working; unknown options are only warned about.

**Known limits**

- The Silent Gauntlet and the end of the game are left alone: their parts are
  never hidden.
- 12 buttons in the train cars are not hidden (we do not know if they are
  puzzle buttons or train controls).
- Test 24 (a puzzle gourd stowed in a bag) has still never been run.
- Tested on one PC with a second local instance, not on two machines.

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
