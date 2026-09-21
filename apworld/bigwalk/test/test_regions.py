"""
The island is not as open as this world assumed until 2026-09-21.

The chairlift and the tunnels are doors, and the world modelled neither — so
nothing stopped generation placing the Green Cup Key past the chairlift that
key opens, which is an unbeatable seed. These tests are what stops that
coming back.

Two of them are deliberately vacuous while `data.CHAIRLIFT_PUZZLES` and the
two radio constants are still empty: the membership is measured in game and
has not been yet. They bite the moment it is filled, which is the point of
writing them now rather than then.
"""

from .. import data, regions
from .bases import BigWalkTestBase


class TestGatedRegions(BigWalkTestBase):
    options: dict = {}
    run_default_tests = False

    def test_the_doors_exist_and_cost_their_key(self) -> None:
        # Non-vacuous today: it guards the mechanism itself, whatever the
        # membership turns out to be.
        for entrance_name, item_name in (
            (regions.CHAIRLIFT_ENTRANCE, data.GREEN_CUP.item_name),
            (regions.TUNNEL_ENTRANCE, data.YELLOW_TWIST.item_name),
            (regions.ENDING_ENTRANCE, data.BLACK_MONOLITH.item_name),
        ):
            entrance = self.multiworld.get_entrance(entrance_name, self.player)
            self.assertFalse(entrance.can_reach(self.multiworld.state), entrance_name)
            self.collect_by_name(item_name)
            self.assertTrue(entrance.can_reach(self.multiworld.state), entrance_name)

    def test_nothing_past_the_chairlift_is_reachable_without_the_key(self) -> None:
        for location_name in data.chairlift_locations():
            self.assertFalse(self.can_reach_location(location_name), location_name)

        self.collect_by_name(data.GREEN_CUP.item_name)

        for location_name in data.chairlift_locations():
            self.assertTrue(self.can_reach_location(location_name), location_name)

    def test_nothing_past_the_tunnels_is_reachable_without_the_key(self) -> None:
        for location_name in data.tunnel_locations():
            self.assertFalse(self.can_reach_location(location_name), location_name)

        self.collect_by_name(data.YELLOW_TWIST.item_name)

        for location_name in data.tunnel_locations():
            self.assertTrue(self.can_reach_location(location_name), location_name)

    def test_a_gated_location_is_gated_once(self) -> None:
        # A name in both lists would be placed twice and the second call would
        # win silently, which is exactly the kind of drift the single source in
        # data.py exists to prevent.
        overlap = set(data.chairlift_locations()) & set(data.tunnel_locations())
        self.assertFalse(overlap, overlap)

    def test_every_gated_name_is_a_real_location(self) -> None:
        # A typo in the measured lists would otherwise gate nothing at all, and
        # look exactly like a correct list on a world where the option that
        # creates that location is off.
        from ..locations import LOCATION_NAME_TO_ID

        for location_name in data.chairlift_locations() + data.tunnel_locations():
            self.assertIn(location_name, LOCATION_NAME_TO_ID, location_name)
