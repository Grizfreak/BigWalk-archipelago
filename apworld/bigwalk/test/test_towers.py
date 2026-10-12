from .. import data
from .bases import BigWalkTestBase


class TestTowersReached(BigWalkTestBase):
    """tower_sanity is on by default: a check for each of the seven towers (the drawbridge and the dome by position)."""

    def test_every_tower_has_its_check(self) -> None:
        names = {location.name for location in self.multiworld.get_locations(self.player)}
        for tower in data.reached_towers():
            self.assertIn(data.tower_reached_location_name(tower), names)

    def test_the_five_first_keep_their_ids(self) -> None:
        # The drawbridge and the dome came later (0.5.0): appended, so the five with a button keep their ids.
        self.assertEqual(7, len(data.reached_towers()))
        self.assertEqual(data.reachable_towers(), data.reached_towers()[:5])
        self.assertEqual(["bigKeyIntro", "bigKeyOverflow"], [tower.prop_name for tower in data.reached_towers()[5:]])

    def test_the_drawbridge_is_reached_in_the_starting_zone(self) -> None:
        drawbridge = data.tower_by_prop("bigKeyIntro")
        location = self.multiworld.get_location(data.tower_reached_location_name(drawbridge), self.player)
        self.assertEqual("Starting Zone", location.parent_region.name)

    def test_a_tower_reached_asks_for_no_item(self) -> None:
        # The far doors aside, walking up to a tower is free: nothing needs collecting.
        for tower in data.reachable_towers():
            if tower.prop_name == "bigKeyRedZone":
                self.assertTrue(self.can_reach_location(data.tower_reached_location_name(tower)))


class TestTowerSanityOff(BigWalkTestBase):
    options = {"tower_sanity": False}
    run_default_tests = False

    def test_no_check_for_the_towers(self) -> None:
        names = {location.name for location in self.multiworld.get_locations(self.player)}
        for tower in data.reached_towers():
            self.assertNotIn(data.tower_reached_location_name(tower), names)


class TestTheHintsWhenATowerIsReached(BigWalkTestBase):
    options = {"hint_discovered_key_locations": True}
    run_default_tests = False

    def test_each_tower_lists_its_cuts_and_its_deposit(self) -> None:
        hints = self.world.fill_slot_data()["tower_reach_hints"]
        self.assertEqual({tower.prop_name for tower in data.TOWERS}, set(hints))
        for tower in data.TOWERS:
            self.assertEqual(tower.segments + 1, len(hints[tower.prop_name]))
            self.assertIn(data.key_deposit_location_id(tower), hints[tower.prop_name])


class TestNoHintsByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_the_hint_list_is_empty(self) -> None:
        self.assertEqual({}, self.world.fill_slot_data()["tower_reach_hints"])
