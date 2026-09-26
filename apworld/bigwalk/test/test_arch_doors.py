from .. import data, regions
from ..locations import starting_zone_locations
from .bases import BigWalkTestBase, build_like_universal_tracker, world_shape

ARCH_DOOR_NAMES = {door.item_name for door in data.ARCH_DOORS}


class TestAllArchDoorsLocked(BigWalkTestBase):
    """None open: the starting zone is left through the Drawbridge or the First Arch Door."""

    options = {"start_with_arch_doors_open": []}

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
        self.assertEqual(slot_data["start_with_arch_doors_open"], [])
        self.assertEqual(
            set(slot_data["locked_arch_doors"]), {door.system_name for door in data.ARCH_DOORS})


class TestFarArchDoorsLocked(BigWalkTestBase):
    """The default: the two shortcuts are items, and the way out is free."""

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
    options = {"start_with_arch_doors_open": sorted(ARCH_DOOR_NAMES)}
    run_default_tests = False

    def test_no_door_is_in_the_pool(self) -> None:
        pool = {item.name for item in self.multiworld.itempool}
        self.assertFalse(ARCH_DOOR_NAMES & pool)
        self.assertEqual(self.world.fill_slot_data()["locked_arch_doors"], [])


class TestOnlyTheFirstArchDoorLocked(BigWalkTestBase):
    """The locked start without the longer walk, which the old Choice could not name."""

    options = {"start_with_arch_doors_open": [data.LEFT_ARCH_DOOR.item_name, data.RIGHT_ARCH_DOOR.item_name]}

    def test_only_the_first_door_is_in_the_pool(self) -> None:
        pool = {item.name for item in self.multiworld.itempool}
        self.assertIn(data.FIRST_ARCH_DOOR.item_name, pool)
        self.assertNotIn(data.LEFT_ARCH_DOOR.item_name, pool)
        self.assertNotIn(data.RIGHT_ARCH_DOOR.item_name, pool)

    def test_the_way_out_is_gated(self) -> None:
        exit_ = self.multiworld.get_entrance(regions.STARTING_EXIT, self.player)
        self.assertFalse(exit_.can_reach(self.multiworld.state))

    def test_one_way_out_is_asked_for_early(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertEqual(
            sum(early.get(name, 0) for name in (data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name)), 1)

    def test_slot_data_names_the_door(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["locked_arch_doors"], [data.FIRST_ARCH_DOOR.system_name])


class TestA011YamlStillLocksItsDoors(BigWalkTestBase):
    """`lock_arch_doors` from 0.1.1 is read into the option that replaced it."""

    options = {"lock_arch_doors": "far_left"}
    run_default_tests = False

    def test_the_old_value_lands_on_the_new_option(self) -> None:
        self.assertEqual(
            self.world.options.start_with_arch_doors_open.value,
            {data.FIRST_ARCH_DOOR.item_name, data.RIGHT_ARCH_DOOR.item_name})
        self.assertEqual(self.world.locked_arch_doors, (data.LEFT_ARCH_DOOR,))

    def test_slot_data_carries_the_new_option(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertNotIn("lock_arch_doors", slot_data)
        self.assertEqual(
            slot_data["start_with_arch_doors_open"], sorted([data.FIRST_ARCH_DOOR.item_name, data.RIGHT_ARCH_DOOR.item_name]))


class TestTheTrackerRebuildsA011Seed(BigWalkTestBase):
    """A 0.1.1 seed's slot_data has `lock_arch_doors` and no `start_with_arch_doors_open`."""

    options = {"lock_arch_doors": "all"}
    run_default_tests = False

    def test_it_lands_on_that_seed(self) -> None:
        slot_data = dict(self.world.fill_slot_data())
        del slot_data["start_with_arch_doors_open"]
        slot_data["lock_arch_doors"] = "all"
        tracked = build_like_universal_tracker({}, slot_data)
        self.assertEqual(world_shape(tracked), world_shape(self.multiworld))
