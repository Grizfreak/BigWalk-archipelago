"""
`lock_abilities` (ROADMAP R3, 0.6): the players' bodies behind items. Legs is the way out of the
starting zone; the Seat and the arms are asked for by the gesture puzzles (puzzle tour, 2026-10-12).
"""

from .. import data, items, regions
from .bases import BigWalkTestBase


def item_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


def ability_names(test: BigWalkTestBase) -> list[str]:
    return sorted(n for n in item_names(test) if n in items.ITEM_NAME_GROUPS["Abilities"])


def puzzle_location(prop_name: str) -> str:
    return next(p.location_name for p in data.PUZZLES if p.prop_name == prop_name)


class TestAbilitiesOffByDefault(BigWalkTestBase):
    options = {"lock_puzzle_needs": False}

    def test_no_ability_in_the_pool(self) -> None:
        self.assertEqual(ability_names(self), [])

    def test_no_rule_on_the_gesture_puzzles(self) -> None:
        self.collect_all_but([])
        self.assertTrue(self.can_reach_location(puzzle_location("gourdCharadesRooms")))

    def test_slot_data_says_disabled(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["lock_abilities"], "disabled")
        self.assertEqual(slot["ability_keys"], [a.key for a in data.ABILITIES])


class TestAbilitiesHanded(BigWalkTestBase):
    options = {"lock_abilities": "handed", "lock_puzzle_needs": False}

    def test_ten_items_running_left_out(self) -> None:
        self.assertEqual(ability_names(self), sorted(
            ["Legs", "Seat", "Throwing", "Carrying"] + [data.LEFT_ARM_ITEM_NAME] * 3 + [data.RIGHT_ARM_ITEM_NAME] * 3))

    def test_throwing_and_carrying(self) -> None:
        self.collect_all_but(["Throwing", "Carrying"])
        self.assertFalse(self.can_reach_location(puzzle_location("gourdBasketball")))
        self.assertFalse(self.can_reach_location(puzzle_location("gourdObby")))
        self.collect_by_name("Throwing")
        self.assertTrue(self.can_reach_location(puzzle_location("gourdBasketball")))
        self.assertFalse(self.can_reach_location(puzzle_location("gourdObby")))

    def test_the_way_out_needs_legs(self) -> None:
        exit_ = self.multiworld.get_entrance(regions.STARTING_EXIT, self.player)
        self.collect_all_but(["Legs"])
        self.assertFalse(exit_.can_reach(self.multiworld.state))
        self.collect_by_name("Legs")
        self.assertTrue(exit_.can_reach(self.multiworld.state))

    def test_invisible_ink_needs_one_copy_of_both_arms(self) -> None:
        location = puzzle_location("gourdInvisibleInk")
        self.collect_all_but([data.LEFT_ARM_ITEM_NAME, data.RIGHT_ARM_ITEM_NAME])
        self.collect(self.get_item_by_name(data.LEFT_ARM_ITEM_NAME))
        self.assertFalse(self.can_reach_location(location))
        self.collect(self.get_item_by_name(data.RIGHT_ARM_ITEM_NAME))
        self.assertTrue(self.can_reach_location(location))

    def test_pointers_paradise_needs_carrying(self) -> None:
        self.collect_all_but(["Carrying"])
        self.assertFalse(self.can_reach_location(puzzle_location("gourdPointersParadise")))

    def test_poses_need_the_seat(self) -> None:
        self.collect_all_but(["Seat"])
        self.assertFalse(self.can_reach_location(puzzle_location("gourdConcert")))
        self.assertFalse(self.can_reach_location(puzzle_location("gourdCharadesRooms")))
        self.assertTrue(self.can_reach_location(puzzle_location("gourdInvisibleInk")))

    def test_every_ability_is_progression(self) -> None:
        for ability in data.ABILITIES:
            if ability.item_name == data.RUNNING_ITEM_NAME:
                continue
            self.assertTrue(self.world.create_item(ability.item_name).advancement, ability.item_name)


class TestAbilitiesAmbidextrous(BigWalkTestBase):
    options = {"lock_abilities": "ambidextrous"}

    def test_seven_items(self) -> None:
        self.assertEqual(ability_names(self), sorted(["Legs", "Seat", "Throwing", "Carrying"] + [data.ARMS_ITEM_NAME] * 3))


class TestLockRunning(BigWalkTestBase):
    options = {"lock_abilities": "ambidextrous", "lock_running": True}

    def test_running_in_the_pool_and_only_useful(self) -> None:
        self.assertIn(data.RUNNING_ITEM_NAME, ability_names(self))
        self.assertFalse(self.world.create_item(data.RUNNING_ITEM_NAME).advancement)


class TestAbilitiesWithEverythingLocked(BigWalkTestBase):
    """The tightest start: the first arch door, the needs, the Gauntlet's stages and the body all items."""

    options = {"lock_abilities": "handed", "lock_running": True, "start_with_arch_doors_open": [], "lock_puzzle_needs": True,
               "gauntlet_mode": "locked_stages", "goal": "big_goodbye"}


class TestAbilityData(BigWalkTestBase):
    run_default_tests = False

    def test_ids_in_their_own_block(self) -> None:
        ids = [data.ability_item_id(a) for a in data.ABILITIES]
        self.assertEqual(ids, list(range(data.BASE_ID + 13_400, data.BASE_ID + 13_400 + len(ids))))
        self.assertEqual(len(set(items.ITEM_NAME_TO_ID.values())), len(items.ITEM_NAME_TO_ID))

    def test_gesture_puzzles_exist(self) -> None:
        self.assertFalse(set(data.ABILITY_PUZZLES) - {p.prop_name for p in data.PUZZLES})
        self.assertEqual(len(data.GAUNTLET_STAGE_ABILITIES), len(data.GAUNTLET_STAGES))


class TestLegsWithNoRoomAtTheStart(BigWalkTestBase):
    """The tutorial's puzzles hold their gourds and its monument waits for an item: Legs is handed over at the start
    (generation failed 4 in 4 before, 2026-10-13)."""

    # The default tests fill the world (pre_fill included): that the fill succeeds is the test.
    options = {"lock_abilities": "handed", "linear_puzzles": True, "tower_mode": "by_item", "first_tower_open": False}


class TestSomeAbilitiesFree(BigWalkTestBase):
    """An ability listed in start_with_abilities is handed over at the start and leaves the pool."""

    options = {"lock_abilities": "handed", "start_with_abilities": ["legs", "seat", "throwing"]}

    def test_legs_seat_throwing_are_free(self) -> None:
        start = [item.name for item in self.multiworld.precollected_items[self.player]]
        for name in ("Legs", "Seat", "Throwing"):
            self.assertIn(name, start)
            self.assertNotIn(name, ability_names(self))
        self.assertEqual(ability_names(self).count(data.LEFT_ARM_ITEM_NAME), 3)
        self.assertIn("Carrying", ability_names(self))

    def test_the_way_out_is_open(self) -> None:
        self.assertTrue(self.multiworld.get_entrance(regions.STARTING_EXIT, self.player).can_reach(self.multiworld.state))


class TestMegaphonesNeedCarrying(BigWalkTestBase):
    """Every megaphone hangs above one player's reach (Tyrope, 2026-10-27)."""

    options = {"lock_abilities": "handed", "lock_puzzle_needs": False, "object_sanity": "every"}

    def test_every_megaphone(self) -> None:
        megaphones = [obj.location_name for obj in data.ISLAND_OBJECTS if obj.kind == "MegaphoneProp"]
        self.assertEqual(len(megaphones), 3)
        self.collect_all_but(["Carrying"])
        for location in megaphones:
            self.assertFalse(self.can_reach_location(location), location)
        self.collect_by_name("Carrying")
        for location in megaphones:
            self.assertTrue(self.can_reach_location(location), location)


class TestChairliftFreeByDefault(BigWalkTestBase):
    options = {"lock_abilities": "handed", "lock_puzzle_needs": False}

    def test_the_chairlift_asks_no_seat(self) -> None:
        self.collect_all_but(["Seat"])
        self.assertTrue(self.can_reach_region(regions.CHAIRLIFT_ZONE))
        self.assertFalse(self.world.fill_slot_data()["rides_require_seat"])


class TestRidesRequireSeat(BigWalkTestBase):
    options = {"lock_abilities": "handed", "lock_puzzle_needs": False, "rides_require_seat": True}

    def test_the_chairlift_asks_for_the_seat(self) -> None:
        self.collect_all_but(["Seat"])
        self.assertFalse(self.can_reach_region(regions.CHAIRLIFT_ZONE))
        self.collect_by_name("Seat")
        self.assertTrue(self.can_reach_region(regions.CHAIRLIFT_ZONE))
        self.assertTrue(self.world.fill_slot_data()["rides_require_seat"])


class TestTrainRequiresSeat(BigWalkTestBase):
    options = {
        "lock_abilities": "handed", "lock_puzzle_needs": False, "rides_require_seat": True,
        "light_sanity": "lamps_and_switches",
    }

    def test_the_headlights_ask_for_the_seat(self) -> None:
        self.collect_all_but(["Seat"])
        self.assertFalse(self.can_reach_location("Light Switch (Train, Front Car) On"))
        self.collect_by_name("Seat")
        self.assertTrue(self.can_reach_location("Light Switch (Train, Front Car) On"))


class TestRidesRequireSeatWithoutLocks(BigWalkTestBase):
    """Without `lock_abilities` there is no Seat item: the option does nothing."""

    options = {"lock_puzzle_needs": False, "rides_require_seat": True}

    def test_the_chairlift_asks_no_seat(self) -> None:
        self.collect_all_but([])
        self.assertTrue(self.can_reach_region(regions.CHAIRLIFT_ZONE))
