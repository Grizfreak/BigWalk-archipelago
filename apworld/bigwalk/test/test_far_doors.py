"""
`require_arch_doors` puts what lies past the Left and Right Arch Doors behind them in
logic, so that the generator does not ask for a place on foot and then send the shortcut.
"""

from .. import data, locations, regions
from .bases import BigWalkTestBase

LEFT = data.LEFT_ARCH_DOOR
RIGHT = data.RIGHT_ARCH_DOOR

LEFT_TOWERS = ("bigKeyBlueZone", "bigKeyYellowZone", "bigKeyBoss", "bigKeyOverflow")
RIGHT_TOWERS = ("bigKeyGreenZone",)


def tower(prop_name: str) -> data.Tower:
    return next(t for t in data.TOWERS if t.prop_name == prop_name)


class TestTheDefault(BigWalkTestBase):
    """The option is on unless a YAML turns it off."""

    def test_on_by_default(self) -> None:
        from ..options import RequireArchDoors

        self.assertTrue(RequireArchDoors.default)


class TestFarDoorsInLogic(BigWalkTestBase):
    options = {"require_arch_doors": True, "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name]}
    run_default_tests = False

    def test_the_slot_says_so(self) -> None:
        self.assertIs(self.world.fill_slot_data()["require_arch_doors"], True)

    def test_the_doors_are_progression(self) -> None:
        self.assertTrue(self.world.create_item(LEFT.item_name).advancement)
        self.assertTrue(self.world.create_item(RIGHT.item_name).advancement)

    def test_a_tower_past_a_door_needs_it(self) -> None:
        for door, props in ((LEFT, LEFT_TOWERS), (RIGHT, RIGHT_TOWERS)):
            for prop in props:
                with self.subTest(tower=prop, door=door.item_name):
                    t = tower(prop)
                    self.multiworld.state = self.multiworld.state.__class__(self.multiworld)
                    self.collect_by_name(data.key_item_name(t))
                    self.assertFalse(self.can_reach_location(t.location_name))
                    self.collect_by_name(door.item_name)
                    self.assertTrue(self.can_reach_location(t.location_name))

    def test_a_cut_needs_the_door_too(self) -> None:
        t = tower("bigKeyBlueZone")
        self.collect_by_name(data.key_item_name(t))
        self.assertFalse(self.can_reach_location(data.cut_location_name(t, 0)))
        self.collect_by_name(LEFT.item_name)
        self.assertTrue(self.can_reach_location(data.cut_location_name(t, 0)))

    def test_the_drawbridge_and_the_red_tower_need_no_far_door(self) -> None:
        for prop in ("bigKeyIntro", "bigKeyRedZone"):
            with self.subTest(tower=prop):
                self.multiworld.state = self.multiworld.state.__class__(self.multiworld)
                t = tower(prop)
                self.collect_by_name(data.key_item_name(t))
                self.assertTrue(self.can_reach_location(t.location_name))

    def test_the_chairlift_zone_needs_the_right_door(self) -> None:
        entrance = self.multiworld.get_entrance(regions.CHAIRLIFT_ENTRANCE, self.player)
        self.collect_by_name(data.GREEN_CUP.item_name)
        self.assertFalse(entrance.can_reach(self.multiworld.state))
        self.collect_by_name(RIGHT.item_name)
        self.assertTrue(entrance.can_reach(self.multiworld.state))

    def test_the_chapel_needs_only_its_key(self) -> None:
        entrance = self.multiworld.get_entrance(regions.ENDING_ENTRANCE, self.player)
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(entrance.can_reach(self.multiworld.state))

    def test_the_tunnels_and_the_green_dome_need_the_left_door(self) -> None:
        for entrance_name, key in ((regions.TUNNEL_ENTRANCE, data.YELLOW_TWIST.item_name),
                                   (regions.GREEN_DOME_ENTRANCE, data.GREEN_DOME.item_name)):
            with self.subTest(entrance=entrance_name):
                self.multiworld.state = self.multiworld.state.__class__(self.multiworld)
                entrance = self.multiworld.get_entrance(entrance_name, self.player)
                self.collect_by_name(key)
                self.assertFalse(entrance.can_reach(self.multiworld.state))
                self.collect_by_name(LEFT.item_name)
                self.assertTrue(entrance.can_reach(self.multiworld.state))


class TestAnOpenDoorAsksNothing(BigWalkTestBase):
    """A door that starts open adds no requirement, whatever lies past it."""

    options = {
        "require_arch_doors": True,
        "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name, LEFT.item_name],
    }
    run_default_tests = False

    def test_the_left_towers_need_only_their_key(self) -> None:
        t = tower("bigKeyBlueZone")
        self.collect_by_name(data.key_item_name(t))
        self.assertTrue(self.can_reach_location(t.location_name))

    def test_the_right_towers_still_need_their_door(self) -> None:
        t = tower("bigKeyGreenZone")
        self.collect_by_name(data.key_item_name(t))
        self.assertFalse(self.can_reach_location(t.location_name))
        self.collect_by_name(RIGHT.item_name)
        self.assertTrue(self.can_reach_location(t.location_name))


class TestTheOptionOff(BigWalkTestBase):
    options = {"require_arch_doors": False}
    run_default_tests = False

    def test_the_doors_gate_nothing_and_are_only_useful(self) -> None:
        self.assertFalse(self.world.create_item(LEFT.item_name).advancement)
        t = tower("bigKeyBlueZone")
        self.collect_by_name(data.key_item_name(t))
        self.assertTrue(self.can_reach_location(t.location_name))

    def test_the_slot_says_so(self) -> None:
        self.assertIs(self.world.fill_slot_data()["require_arch_doors"], False)


class TestTheFarDoorsComeEarly(BigWalkTestBase):
    """A door in logic is only worth having if it comes first, so it is asked for as an early item."""

    options = {"require_arch_doors": True, "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name]}
    run_default_tests = False

    def test_both_far_doors_are_early_local_items(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertEqual(early.get(LEFT.item_name), 1)
        self.assertEqual(early.get(RIGHT.item_name), 1)


class TestNoEarlyDoorsWhenThePartsAreLocked(BigWalkTestBase):
    """With the puzzles' parts locked the starting locations are too few for early doors."""

    options = {"require_arch_doors": True, "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name],
               "lock_puzzle_needs": True}
    run_default_tests = False

    def test_the_far_doors_are_not_asked_for_early(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertNotIn(LEFT.item_name, early)
        self.assertNotIn(RIGHT.item_name, early)

    def test_they_are_still_in_logic(self) -> None:
        t = tower("bigKeyBlueZone")
        self.collect_by_name(data.key_item_name(t))
        self.assertFalse(self.can_reach_location(t.location_name))


class TestNoEarlyDoorsWhenTheFirstDoorIsShut(BigWalkTestBase):
    """With the first door shut the starting zone is too small to hold them as well as the way out."""

    options = {"require_arch_doors": True, "start_with_arch_doors_open": []}
    run_default_tests = False

    def test_the_far_doors_are_not_asked_for_early(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertNotIn(LEFT.item_name, early)
        self.assertNotIn(RIGHT.item_name, early)

    def test_the_way_out_still_is(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertEqual(sum(early.get(name, 0) for name in (data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name)), 1)


class TestAnOpenDoorIsNotAskedFor(BigWalkTestBase):
    options = {
        "require_arch_doors": True,
        "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name, LEFT.item_name],
    }
    run_default_tests = False

    def test_only_the_closed_door_is_early(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertNotIn(LEFT.item_name, early)
        self.assertEqual(early.get(RIGHT.item_name), 1)


class TestNothingEarlyWithTheOptionOff(BigWalkTestBase):
    options = {"require_arch_doors": False}
    run_default_tests = False

    def test_no_door_is_asked_for(self) -> None:
        early = self.multiworld.local_early_items[self.player]
        self.assertNotIn(LEFT.item_name, early)
        self.assertNotIn(RIGHT.item_name, early)
