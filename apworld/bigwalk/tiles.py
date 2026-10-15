"""
R8, the custom tiles (the community document's `random_basic_tiles`), by category, each a folder of mod/assets/tiles
(how to add one: its README.md; CC BY 4.0, credited in its LICENSE.md):

- icons: the nine basic peg tiles (the game's set A, the icon and sound panels) drawn for the seed among the game's
  own and the community's icons (`custom_tiles`).
- drawings: the scribbles of the drawing panels (sets C and Priest, the Poet's and the Pontiff's) replaced, a few of
  them, by the community's own scribbles (`custom_drawings`).

The mod repaints each tile with the drawing it is given; what a puzzle asks for (the tile's kind) does not change,
only what the tile shows.
"""

from __future__ import annotations

from random import Random
from typing import Iterable, Mapping, NamedTuple


class CustomTile(NamedTuple):
    key: str
    """The drawing's name: its file in mod/assets/tiles, and its key in `custom_tile_weights`."""
    games: tuple[str, ...]
    """It belongs to a multiworld holding a game whose name contains one of these (case-sensitive, anywhere)."""
    artist: str


class CustomDrawing(NamedTuple):
    key: str
    """Its file in mod/assets/tiles/drawings (every file name is unique across the categories: the mod finds it by it)."""
    artist: str


VANILLA_TILES: tuple[str, ...] = (
    "triangle", "asterisk", "diagonal_line", "G", "dot_and_line", "vertical_ellipsis", "S", "spiral", "e",
)
"""The game's nine basic tiles, PegTileA1 to PegTileA9 (the atlas, dumped 2026-10-15), by the document's names."""

CUSTOM_TILES: tuple[CustomTile, ...] = (
    CustomTile("pelly", ("AP", "Archipelago"), "Jack5"),
    CustomTile("jack", ("The Witness",), "Jack5"),
    CustomTile("strawberry", ("Celeste",), "Jack5"),
    CustomTile("triforce", ("Zelda",), "Jack5"),
    CustomTile("semibot", ("R.E.P.O.",), "Jack5"),
    CustomTile("star", ("Mario",), "Jack5"),
    CustomTile("smile", ("Lingo",), "Jack5"),
    CustomTile("five", ("Dice",), "Jack5"),
    CustomTile("arrow", ("Grapple Dog",), "Jack5"),
    CustomTile("sword_in_stone", ("Dark Souls",), "Don Chalant"),
    CustomTile("lodo", ("StarCraft",), "Don Chalant"),
    CustomTile("stones_above_banner", ("RuneScape",), "Don Chalant"),
    CustomTile("pokeball", ("Pokémon", "Pokemon"), "Don Chalant"),
    CustomTile("tree", ("Terraria",), "Don Chalant"),
    CustomTile("creeper", ("Minecraft",), "Don Chalant"),
    CustomTile("sigil", ("Blasphemous",), "Don Chalant"),
    CustomTile("chicken", ("Stardew Valley",), "Don Chalant"),
    CustomTile("planets", ("Risk of Rain",), "Don Chalant"),
    CustomTile("monster", ("Slay the Spire",), "Don Chalant"),
    CustomTile("joker", ("Balatro",), "Don Chalant"),
    CustomTile("rune", ("DOOM",), "Don Chalant"),
    CustomTile("knight", ("Hollow Knight",), "GameFraek"),
    CustomTile("blob_with_legs", ("Animal Well",), "GameFraek"),
    CustomTile("gear", ("Factorio",), "GameFraek"),
    CustomTile("hat", ("A Hat in Time",), "GameFraek"),
    CustomTile("heart", ("Undertale", "Deltarune"), "GameFraek"),
    CustomTile("tunic", ("Tunic",), "GameFraek"),
    CustomTile("portal", ("Portal",), "GameFraek"),
    CustomTile("cat", ("Rain World",), "Kensufox"),
)

ALL_TILES: tuple[str, ...] = (*VANILLA_TILES, *(tile.key for tile in CUSTOM_TILES))

CUSTOM_DRAWINGS: tuple[CustomDrawing, ...] = ()
"""The community's scribbles for the drawing panels (mod/assets/tiles/drawings). None yet."""

DRAWING_PLACES: tuple[str, ...] = (
    *(f"PegTileC{i}" for i in range(1, 10)),
    *(f"PegTilePriest{letter}{i}" for letter in "ABCD" for i in range(1, 10)),
)
"""The drawing panels' tile kinds (the mod's PropGroup names): C1..C9, Priest A1..D9 (the tile dump, 2026-10-15)."""


def matching(games: Iterable[str]) -> list[str]:
    """The drawings whose game is in the multiworld."""
    names = list(games)
    return [tile.key for tile in CUSTOM_TILES if any(part in name for part in tile.games for name in names)]


def draw(random: Random, candidates: Iterable[str], weights: Mapping[str, int],
         chosen: Iterable[str] = ()) -> tuple[str, ...]:
    """
    The nine basic tiles' drawings, in the order PegTileA1 to PegTileA9: the chosen ones first (whatever their weight
    and whether they are candidates, nine at most, drawn among them if more), then drawn by weight among the candidates
    (a weight of 0 leaves a tile out). A basic tile drawn keeps its own place; the drawings take the others, in a drawn
    order. A place left without a drawing (fewer than nine to draw from) keeps its own tile.
    """
    drawn = list(dict.fromkeys(key for key in chosen if key in ALL_TILES))
    if len(drawn) > len(VANILLA_TILES):
        drawn = random.sample(drawn, len(VANILLA_TILES))
    pool = [key for key in dict.fromkeys(candidates) if weights.get(key, 1) > 0 and key not in drawn]
    while pool and len(drawn) < len(VANILLA_TILES):
        pick = random.choices(pool, weights=[weights.get(key, 1) for key in pool])[0]
        pool.remove(pick)
        drawn.append(pick)

    result = list(VANILLA_TILES)
    free = [i for i, key in enumerate(VANILLA_TILES) if key not in drawn]
    random.shuffle(free)
    for key, place in zip([key for key in drawn if key not in VANILLA_TILES], free):
        result[place] = key
    return tuple(result)


def draw_drawings(random: Random, drawings: Iterable[str]) -> dict[str, str]:
    """`custom_drawings`: every drawing given takes the place of a scribble drawn among DRAWING_PLACES (45 at most)."""
    names = list(dict.fromkeys(drawings))
    random.shuffle(names)
    places = random.sample(DRAWING_PLACES, min(len(names), len(DRAWING_PLACES)))
    return dict(zip(places, names))
