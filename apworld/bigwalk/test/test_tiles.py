"""R8, the custom tiles: `custom_tiles` and `custom_tile_weights` (tiles.draw)."""

from random import Random

from .. import tiles
from .bases import BigWalkTestBase


class TestDraw(BigWalkTestBase):
    def test_vanilla_alone_stays_in_place(self) -> None:
        self.assertEqual(tiles.draw(Random(1), tiles.VANILLA_TILES, {}), tiles.VANILLA_TILES)

    def test_nine_distinct_and_a_kept_basic_tile_stays_in_place(self) -> None:
        for seed in range(200):
            drawn = tiles.draw(Random(seed), tiles.ALL_TILES, {})
            self.assertEqual(len(set(drawn)), 9)
            for place, key in enumerate(drawn):
                if key in tiles.VANILLA_TILES:
                    self.assertEqual(tiles.VANILLA_TILES[place], key)

    def test_a_weight_of_zero_leaves_a_drawing_out(self) -> None:
        weights = {key: 1 for key in tiles.ALL_TILES} | {"pelly": 0}
        for seed in range(200):
            self.assertNotIn("pelly", tiles.draw(Random(seed), tiles.ALL_TILES, weights))

    def test_only_drawings_when_the_basic_tiles_weigh_nothing(self) -> None:
        weights = {key: 0 for key in tiles.VANILLA_TILES}
        drawn = tiles.draw(Random(3), tiles.ALL_TILES, weights)
        self.assertFalse(set(drawn) & set(tiles.VANILLA_TILES))

    def test_matching_is_anywhere_whatever_the_capitals(self) -> None:
        self.assertEqual(tiles.matching(["Celeste 64", "The Legend of Zelda", "doom"]),
                         ["pelly", "strawberry", "triforce", "rune"])
        self.assertIn("pokeball", tiles.matching(["Pokemon Red and Blue"]))
        # The names the main Archipelago repository gives them (2026-10-15).
        self.assertEqual(tiles.matching(["Starcraft 2", "Old School Runescape", "TUNIC"]),
                         ["pelly", "lodo", "stones_above_banner", "tunic"])
        for game in ("A Link to the Past", "Ocarina of Time", "Links Awakening DX", "The Wind Waker", "SMZ3"):
            self.assertIn("triforce", tiles.matching([game]), game)
        self.assertIn("star", tiles.matching(["Yoshi's Island"]))
        self.assertIn("star", tiles.matching(["Luigi's Mansion"]))
        self.assertIn("star", tiles.matching(["Manual_SuperMarioBros3_Someone"]))

    def test_pelly_in_every_multiworld(self) -> None:
        self.assertEqual(tiles.matching([]), ["pelly"])

    def test_preferred_first(self) -> None:
        preferred = ["star", "tree", "gear"]
        for seed in range(200):
            drawn = tiles.draw(Random(seed), [*tiles.VANILLA_TILES, *preferred], {}, preferred=preferred)
            self.assertTrue(set(preferred) <= set(drawn))
            self.assertEqual(sum(1 for key in drawn if key in tiles.VANILLA_TILES), 6)

    def test_preferred_nine_at_most_after_the_chosen_and_weight_zero_out(self) -> None:
        preferred = [tile.key for tile in tiles.CUSTOM_TILES][:12]
        for seed in range(100):
            drawn = tiles.draw(Random(seed), [*tiles.VANILLA_TILES, *preferred], {preferred[0]: 0}, ["cat"], preferred)
            self.assertIn("cat", drawn)
            self.assertNotIn(preferred[0], drawn)
            self.assertEqual(len(set(drawn) & set(preferred)), 8)

    def test_no_preferred_draws_as_before(self) -> None:
        for seed in range(50):
            self.assertEqual(tiles.draw(Random(seed), tiles.ALL_TILES, {}, ["cat"]),
                             tiles.draw(Random(seed), tiles.ALL_TILES, {}, ["cat"], []))

    def test_every_drawing_has_its_file(self) -> None:
        from pathlib import Path
        folder = Path(__file__).resolve().parents[3] / "mod" / "assets" / "tiles"
        if not folder.is_dir():
            self.skipTest("not run from the repository")
        for tile in tiles.CUSTOM_TILES:
            self.assertTrue((folder / "icons" / f"{tile.key}.png").is_file(), tile.key)
        for drawing in tiles.CUSTOM_DRAWINGS:
            self.assertTrue((folder / "drawings" / f"{drawing.key}.png").is_file(), drawing.key)
        # The mod finds a drawing by its file name alone: unique across the folders.
        files = [path for path in folder.glob("*/*.png") if path.parent.name != "source"]
        names = [path.stem for path in files]
        self.assertEqual(len(names), len(set(names)))
        # What the mod's PNG reader takes (mod/src/Core/PngReader.cs): 512 x 512, 8-bit RGBA, not interlaced.
        import struct
        for path in files:
            header = path.read_bytes()[16:29]
            width, height, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", header)
            self.assertEqual((width, height, depth, colour, interlace), (512, 512, 8, 6, 0), path.name)

    def test_drawings_take_distinct_places(self) -> None:
        names = [f"scribble{i}" for i in range(50)]
        drawn = tiles.draw_drawings(Random(2), names)
        self.assertEqual(len(drawn), len(tiles.DRAWING_PLACES))
        self.assertEqual(len(set(drawn.values())), len(drawn))
        self.assertEqual(tiles.draw_drawings(Random(2), []), {})


class TestChosen(BigWalkTestBase):
    def test_chosen_first_whatever_their_weight(self) -> None:
        weights = {"cat": 0}
        for seed in range(100):
            drawn = tiles.draw(Random(seed), tiles.VANILLA_TILES, weights, ["cat", "heart"])
            self.assertIn("cat", drawn)
            self.assertIn("heart", drawn)

    def test_nine_at_most(self) -> None:
        chosen = [tile.key for tile in tiles.CUSTOM_TILES][:12]
        drawn = tiles.draw(Random(5), tiles.VANILLA_TILES, {}, chosen)
        self.assertEqual(len(set(drawn) & set(chosen)), 9)


class TestCustomOnly(BigWalkTestBase):
    options = {"custom_tiles": "custom_only", "custom_tile_choices": ["pelly"]}

    def test_no_basic_tile_left(self) -> None:
        self.assertFalse(set(self.world.peg_tiles) & set(tiles.VANILLA_TILES))
        self.assertIn("pelly", self.world.peg_tiles)


class TestChoicesWithMatchingGames(BigWalkTestBase):
    """The default mode, Big Walk alone: no game matches, but the chosen drawing is shown (and pelly may be drawn)."""

    options = {"custom_tile_choices": ["knight"]}

    def test_the_choice_alone(self) -> None:
        self.assertIn("knight", self.world.peg_tiles)
        self.assertLessEqual({key for key in self.world.peg_tiles if key not in tiles.VANILLA_TILES}, {"knight", "pelly"})


class TestMatchingGamesAlone(BigWalkTestBase):
    """The default, in a multiworld of Big Walk alone: only pelly joins the game's tiles."""

    def test_vanilla_or_pelly(self) -> None:
        self.assertLessEqual(set(self.world.peg_tiles), {*tiles.VANILLA_TILES, "pelly"})
        self.assertEqual(self.world.fill_slot_data()["peg_tiles"], list(self.world.peg_tiles))


class TestMatchingFirstAlone(BigWalkTestBase):
    """`matching_first`, Big Walk alone: pelly for sure, the game's tiles in the places left."""

    options = {"custom_tiles": "matching_first"}

    def test_pelly_and_the_game_tiles(self) -> None:
        self.assertIn("pelly", self.world.peg_tiles)
        self.assertEqual(sum(1 for key in self.world.peg_tiles if key in tiles.VANILLA_TILES), 8)


class TestAllTiles(BigWalkTestBase):
    options = {"custom_tiles": "all", "custom_tile_weights": {key: 0 for key in tiles.VANILLA_TILES}}

    def test_nine_drawings(self) -> None:
        drawn = self.world.fill_slot_data()["peg_tiles"]
        self.assertEqual(len(drawn), 9)
        self.assertFalse(set(drawn) & set(tiles.VANILLA_TILES))


class TestOff(BigWalkTestBase):
    options = {"custom_tiles": "off"}

    def test_vanilla(self) -> None:
        self.assertEqual(self.world.peg_tiles, tiles.VANILLA_TILES)
