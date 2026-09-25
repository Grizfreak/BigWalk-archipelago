from .. import data, regions
from ..locations import starting_zone_locations
from .bases import BigWalkTestBase

ARCH_DOOR_NAMES = {door.item_name for door in data.ARCH_DOORS}


class TestAllArchDoorsLocked(BigWalkTestBase):
    """`all`: the starting zone is left through the Drawbridge or the First Arch Door."""

    options = {"lock_arch_doors": "all"}

    def test_all_three_doors_are_in_the_pool(self) -> None:
        pool = {item.name for item in self.multiworld.itempool}
        self.assertTrue(ARCH_DOOR_NAMES <= pool)

    def test_the_starting_zone_holds_its_locations(self) -> None:
        for name in starting_zone_locations():
            if name in self.multiworld.regions.location_cache[self.player]:
                location = self.multiworld.get_location(name, self.player)
                self.assertEqual(location.parent_region.name, regions.STARTING_ZONE, name)

    def test_its_puzzles_need_nothing(self) -> None:
        by_prop = {puzzle.prop_name: puzzle.location_name for puzzle in data.PUZZLES}
        for prop_name in data.START_ZONE_PUZZLES:
            self.assertTrue(self.can_reach_location(by_prop[prop_name]), prop_name)

    def test_the_way_out_is_either_item(self) -> None:
        exit_ = self.multiworld.get_entrance(regions.STARTING_EXIT, self.player)
        self.collect_all_but([data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name])
        self.assertFalse(exit_.can_reach(self.multiworld.state))

        self.collect_by_name(data.FIRST_ARCH_DOOR.item_name)
        self.assertTrue(exit_.can_reach(self.multiworld.state))

    def test_the_drawbridge_alone_is_enough(self) -> None:
        exit_ = self.multiworld.get_entrance(regions.STARTING_EXIT, self.player)
        self.collect_all_but([data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name])
        self.collect_by_name(data.DRAWBRIDGE_ITEM_NAME)
        self.assertTrue(exit_.can_reach(self.multiworld.state))

    def test_one_way_out_is_asked_for_early(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertEqual(
            sum(early.get(name, 0) for name in (data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name)), 1)

    def test_slot_data_names_the_doors(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["lock_arch_doors"], "all")
        self.assertEqual(
            set(slot_data["locked_arch_doors"]), {door.system_name for door in data.ARCH_DOORS})


class TestFarArchDoorsLocked(BigWalkTestBase):
    """The default: the two shortcuts are items, and the way out is free."""

    options = {"lock_arch_doors": "far"}

    def test_only_the_far_doors_are_in_the_pool(self) -> None:
        pool = {item.name for item in self.multiworld.itempool}
        self.assertIn(data.LEFT_ARCH_DOOR.item_name, pool)
        self.assertIn(data.RIGHT_ARCH_DOOR.item_name, pool)
        self.assertNotIn(data.FIRST_ARCH_DOOR.item_name, pool)

    def test_the_way_out_is_free(self) -> None:
        exit_ = self.multiworld.get_entrance(regions.STARTING_EXIT, self.player)
        self.assertTrue(exit_.can_reach(self.multiworld.state))

    def test_nothing_is_asked_for_early(self) -> None:
        self.assertFalse(self.multiworld.local_early_items[self.player])


class TestNoArchDoorLocked(BigWalkTestBase):
    options = {"lock_arch_doors": "disabled"}
    run_default_tests = False

    def test_no_door_is_in_the_pool(self) -> None:
        pool = {item.name for item in self.multiworld.itempool}
        self.assertFalse(ARCH_DOOR_NAMES & pool)
        self.assertEqual(self.world.fill_slot_data()["locked_arch_doors"], [])
