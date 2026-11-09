# Your own tiles and sounds

Put your own drawings on Big Walk's tiles, and your own sounds on the sound puzzles, for a session
with your friends. No coding, no YAML: you drop files in a folder.

**Only the host needs the files.** The other players get them from the host automatically.

## In three steps

1. **Open the folder** `BepInEx/config/BigWalkArchipelago/tiles/` in your Big Walk game folder
   (Steam > Big Walk > Manage > Browse local files). The mod makes it the first time you play an
   Archipelago world; if it is not there yet, play once, or create it yourself.
2. **Drop a picture in it, named after the tile it replaces**: `PegTileA4.png` replaces the **G**.
   The names are in the table below.
3. **Play.** You must be connected to Archipelago: tick the **Archipelago** box on the hosting
   screen. The tile changes within a few seconds, even mid-game, on everyone's screen.

That's it. To undo, delete the file.

## Which name for which tile

The nine **icon tiles**, the ones on the icon panels, Tile Thief and the sound puzzles:

| File | Replaces | | File | Replaces |
|---|---|---|---|---|
| `PegTileA1.png` | the triangle | | `PegTileA6.png` | the vertical dots (⋮) |
| `PegTileA2.png` | the asterisk | | `PegTileA7.png` | the S |
| `PegTileA3.png` | the diagonal line | | `PegTileA8.png` | the spiral |
| `PegTileA4.png` | the G | | `PegTileA9.png` | the e |
| `PegTileA5.png` | the dot and line | | | |

The **scribbles** of the drawing panels can be replaced too: `PegTileC1.png` to `PegTileC9.png`
(the Poet's panels) and `PegTilePriestA1.png` to `PegTilePriestD9.png` (the Pontiff's: A, B, C, D,
each 1 to 9). Their order is not listed anywhere: try one and look.

You can also replace one of the **community drawings** the mod already has (the ones `custom_tiles`
puts in): name your file after it, for example `pelly.png` or `triforce.png`. The names are in
[`apworld/bigwalk/tiles.py`](apworld/bigwalk/tiles.py).

**Not replaceable**: the poses (Indoor Semaphore, Optical Telegraph, Charades) and the counting
tiles, because their puzzles depend on what they show.

## A pool of drawings

Got more drawings than tiles? Put them all in the **`pool`** folder, inside `tiles/`, with any
names. The mod picks some of them for the nine icons, at random but **the same each time you
reconnect** to the same seed, and different with the next seed.

A file named after a tile (`PegTileA4.png`) always wins over the pool.

In game, **Settings > Archipelago > Tile pool** (the host) chooses how the pool works with the
YAML's `custom_tiles`:

| Setting | What it does |
|---|---|
| **Over the seed** (default) | The pool covers every icon not named by a file of its own, over what `custom_tiles` drew. |
| **Game's tiles only** | The pool only covers the icons the seed left as the game's (triangle, G...). The community drawings `custom_tiles` drew stay. |
| **Off** | The pool is not used. Files named after a tile still are. |

## Your own sounds

The nine icon tiles also have a **sound**, heard on the speakers of **Singer and Selector**,
**Dancer and Selector** and **Tile Thief**. Put a WAV named after the tile, in the same folder:
`PegTileA1.wav` replaces the triangle's sound, and so on to `PegTileA9.wav`.

- Each tile's sound is one part of a piece that loops every 28 seconds. A **short sound** (a second
  or two) is repeated to fill it: that is what works best. A longer one is cut at 28 seconds.
- The sound is the clue of those puzzles: pick sounds that are easy to tell apart.
- A speaker already playing changes within a few seconds.

## What the pictures can be

- **PNG**, any size (up to 4096 × 4096 pixels): it is resized to fit the tile, and centred if it is
  not square.
- **Best**: a white or black drawing on a **transparent** background. Only the shape counts: the
  colour you drew in is ignored, the tile keeps its own colours.
- **Without transparency** it works too: the mod takes what stands out from the background as the
  drawing (black on white, or white on black).
- Leave a **margin** round the drawing: the game's tiles do not touch their edges.
- **Thick, simple lines** read best from a distance. Avoid drawings that look like another tile.

The sounds: **WAV**, uncompressed (what most editors save by default; not MP3 or OGG), mono or
stereo, any quality.

## Playing with others

- **Everyone sees and hears the host's files.** The host's mod sends them to the other players
  when they join and whenever a file changes. A guest's own files are not used in someone else's
  world (they are when that player hosts).
- Only the picture's shape and the sound's samples are sent, a few kilobytes each; nothing is
  saved on the other players' computers.
- **Nothing about the puzzles changes**: they still ask for the same tiles, only what the tiles
  look and sound like is yours.

## It does not work?

- **Is the Archipelago box ticked** on the hosting screen? Without a connection, the game shows
  its own tiles and sounds.
- **The name**: `PegTileA4.png`, with the number of the tile (capitals do not matter), in
  `tiles/` itself, not in a subfolder (only the pool goes in `pool/`). Watch for a hidden second
  extension, like `PegTileA4.png.png` when Windows hides extensions.
- **The format**: an "interlaced" PNG is refused (save it again with interlacing off); a WAV must
  be uncompressed.
- **The log** says what happened to each file: open `BepInEx/LogOutput.log` and search for
  `[CustomTiles]` (pictures) or `[TileSounds]` (sounds).

## Sharing a drawing with everyone

Drawings in your folder are for your sessions. To have one included in the mod for every player,
see [`mod/assets/tiles/README.md`](mod/assets/tiles/README.md).
