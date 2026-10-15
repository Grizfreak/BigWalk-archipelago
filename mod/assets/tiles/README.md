# Custom tiles

Drawings the mod can show on Big Walk's peg tiles, by category, one folder each. A tile only changes what it shows:
every puzzle asks for the same tiles, and every player of a session sees the same drawing (the drawings travel inside
the mod, never from one machine to another).

| Folder | Replaces | Option |
|---|---|---|
| `icons/` | the nine basic tiles (the triangle, the asterisk, the G...), on the icon and sound panels | `custom_tiles`, `custom_tile_choices`, `custom_tile_weights` |
| `drawings/` | a few of the scribbles of the drawing panels (the Poet's and the Pontiff's puzzles) | `custom_drawings` |

The pose and point tiles are not replaced: their puzzles depend on what they show (a pose to copy, squares to count).

## Proposing a tile

1. **The drawing**: a PNG of exactly 512 × 512, RGBA with 8 bits per channel and not interlaced (what GIMP, Krita
   or Photoshop save by default), white ink on a transparent background, as the files already here. Only the
   transparency counts: the mod draws the ink white whatever its colour, and the tile's own colour shows through
   the rest. Leave a margin round the drawing, as the files here do: the game's tiles do not reach their edges.
   Draw it as the game's tiles are drawn: one brush, no ruler, no shape or fill tool, slightly uneven lines, a
   thickness of its own (a similar tile's thickness makes them harder to tell apart). An icon must
   not look like any other tile, the game's included; a scribble should be as hard to put into words as the
   game's are.
2. **The file**: in the folder of its category, named in lowercase with underscores (`sword_in_stone.png`). The
   name must be unique across every folder: the mod finds a drawing by it.
3. **The world**: one line in `apworld/bigwalk/tiles.py`, in `CUSTOM_TILES` for an icon (its name, the games that
   bring it in with `custom_tiles: matching_games`, matched anywhere in a game's name with the same capitals, and its
   artist) or in `CUSTOM_DRAWINGS` for a scribble (its name and its artist).
4. **The credit**: a line in [`LICENSE.md`](LICENSE.md). Every tile here is under CC BY 4.0: send one only if you
   drew it, or have its artist's agreement to that license.

The tests (`apworld/bigwalk/test/test_tiles.py`) check that every tile listed has its file and that no two files
share a name.
