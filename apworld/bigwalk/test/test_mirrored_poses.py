from random import Random

from .. import tiles
from .bases import BigWalkTestBase


class TestTheDraw(BigWalkTestBase):
    run_default_tests = False

    def test_no_weight_no_mirror(self) -> None:
        self.assertEqual({}, tiles.draw_poses(Random(1), 0))

    def test_a_draw_shows_nine_different_poses(self) -> None:
        seen_together = False
        for seed in range(200):
            changed = tiles.draw_poses(Random(seed), 100)
            shown = [changed.get(place, place) for place in tiles.POSE_PLACES]
            self.assertEqual(len(shown), len(set(shown)))
            for place, name in changed.items():
                self.assertIn(place, tiles.POSE_PLACES)
                self.assertNotIn(place, tiles.KEPT_POSES)
                self.assertIn(name, [tiles.mirror_name(pose) for pose in tiles.ASYMMETRIC_POSES])
            # A pose and its own mirror, both shown: the draw allows it.
            seen_together |= any(tiles.mirror_name(pose) in shown and pose in shown for pose in tiles.ASYMMETRIC_POSES)
        self.assertTrue(seen_together)

    def test_a_heavy_weight_draws_every_mirror(self) -> None:
        changed = tiles.draw_poses(Random(3), 100_000)
        self.assertEqual({tiles.mirror_name(pose) for pose in tiles.ASYMMETRIC_POSES}, set(changed.values()))


class TestTheSlotData(BigWalkTestBase):
    options = {"mirrored_poses": 100}
    run_default_tests = False

    def test_the_mirrors_travel_as_drawings(self) -> None:
        drawings = self.world.fill_slot_data()["peg_drawings"]
        for entry in drawings:
            place, name = entry.split("=")
            if place in tiles.POSE_PLACES:
                self.assertTrue(name.startswith("mirror_PegTileD"))


class TestOffByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_no_mirror(self) -> None:
        drawings = self.world.fill_slot_data()["peg_drawings"]
        self.assertFalse([entry for entry in drawings if "mirror_" in entry])
