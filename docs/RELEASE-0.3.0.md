# Release 0.3.0: drafts

The tag `0.3.0` (no "v", like the earlier ones) goes on the commit that carries the checksums
below, once E2 has passed. The release page and the Discord post are made by hand from the
texts below. The assets are in `dist/` (the mod) and `apworld/dist/` (the world):

| File | What | SHA-256 |
|---|---|---|
| `BigWalkArchipelago-0.3.0.zip` | the mod, BepInEx included, for every player | *to fill after E2* |
| `bigwalk.apworld` | the world, for whoever generates | *to fill after E2* |

The version number is already 0.3.0 in the plugin, the `.csproj`, `archipelago.json` and
`WORLD_VERSION`.

---

## GitHub release notes

```markdown
## Big Walk Archipelago 0.3.0 (alpha)

Update both files: the mod zip for every player, the apworld for whoever generates. **Everyone in a session must run the same build of the mod.**

### Heads-up for your YAML
- `lock_puzzle_needs` is now **on by default**: the parts puzzles are built from are items to find.
- `gourd_slot_checks` is now `gourd_sanity`, `radio_checks` is now `radio_sanity`. The old names fall back to the defaults: rename them.
- The towers' keys and checks use the names players use: Red Funnel, Green Cup, Blue Castle, Yellow Twist, Black Monolith, Green Dome. A YAML or plando naming them needs the new names.
- The options are sorted into five groups: Goal, Sanity, Logic, QoL, Puzzle QoL.

### Added
- **The Silent Gauntlet** (`gauntlet_mode: locked_stages`): each of the seven stages is a check, and its way up is an item (Gauntlet Stage 1 to 7 Door). `gauntlet_puzzles_required`, `gauntlet_stages_local` and `lock_gauntlet_needs` shape it; the puzzle parts inside the stages are hidden like the rest of the island's.
- **Teleport buttons** (`teleport_buttons`: off, free, with_towers, items): buttons in the hub to the Red, Green, Blue, Yellow and Black towers and the Silent Gauntlet. `with_towers` opens one when you have opened the place on foot; `items` also needs its Teleporter. `teleport_back_to_hub` adds a way back in each place.
- **Resync stations replace Ctrl+R**: a button in the hub (and in each tower with `tower_resync_stations`) brings back your gourds, keys and gadgets lying around, with a light switch to leave the gadgets out and a sign saying what it does. Host only unless `guests_can_resync`; one resync at a time.
- **Cabin Fever waits**: `cabin_fever_time` and `cabin_fever_long_time` (vanilla, reduced, random_between, fixed), and a hidden help button in each house that takes ten seconds off.
- **`open_black_tower`** (on by default): the Black Tower's door is open from the start.
- **`require_arch_doors`** (on by default): what lies past the Left and Right Arch Doors needs that door in logic.
- **`hint_keys: from_start`**: the seven big keys are hinted from the first minute.
- **A new save gets its deposits back**: the server remembers how many gourds you placed.

### Fixed
- A save connected to another seed or slot no longer sends the previous one's checks.
- A big key no longer flies out of its stone when its monument fills before its item arrives.
- The "skip this challenge" panels show once their puzzle's parts have arrived.
- A guest now sees what a player who picked something up before it joined is holding.

### Known limits
- Three and four players were tried with a second local instance only.

0.1.2 seeds still play with this mod; the new options need a seed generated with the 0.3.0 apworld.
```

---

## Discord

```markdown
# Big Walk AP - 0.3.0 (alpha)
**THE APWORLD AND MOD** -> https://github.com/Grizfreak/BigWalk-archipelago/releases/tag/0.3.0
Setup Guide is available here -> https://github.com/Grizfreak/BigWalk-archipelago/blob/master/SETUP.md
The Silent Gauntlet is in, there are buttons in the world now, and your YAML needs a look: see **Heads-up**.

## Heads-up for your YAML
-   `lock_puzzle_needs` is now **on by default**.
-   `gourd_slot_checks` -> `gourd_sanity`, `radio_checks` -> `radio_sanity`. Old names fall back to the defaults.
-   The towers' keys and checks use the names you use (Red Funnel, Green Cup, Blue Castle, Yellow Twist...).

## Added
-   **The Silent Gauntlet** as seven checks and seven items (`gauntlet_mode: locked_stages`).
-   **Teleport buttons** in the hub to every tower and the Gauntlet (`teleport_buttons`), unlocked for free, by opening the place, or by item.
-   **Resync stations instead of Ctrl+R**: a button in the hub (and the towers if you want) that brings your stuff back, with a switch for the gadgets.
-   **Cabin Fever**: shorten, randomise or fix the waits, plus a hidden help button in each house.
-   `open_black_tower`, `require_arch_doors`, `hint_keys: from_start`, and a new save gets its deposits back.

## Fixed
-   A guest sees what you were already holding when it joined.
-   A big key no longer flies out of its stone early.
-   A save moved to another seed doesn't send the old seed's checks.

## Updating
-   **Everyone** reinstalls the mod zip, **and everyone must run the same build**; the host's generator gets the new `bigwalk.apworld`.
-   A 0.1.2 seed keeps working with the new mod. The new options need a new seed.
-   Bug reports: `BepInEx/LogOutput.log` and `BepInEx/session-journal.tsv` from the host, and from any player who saw the problem.
```
