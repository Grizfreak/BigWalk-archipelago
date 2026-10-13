"""
The vanilla behaviours back as options (ROADMAP L2 to L5, L8; 0.6): the Map Room open from the start, keys that
open their own door, puzzles that hold their own gourd, the purple puzzles as in the game, the key cuts.
"""

from BaseClasses import LocationProgressType

from .. import data
from .bases import BigWalkTestBase


def precollected(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.precollected_items[test.player]]


def pool(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestMapRoomOpen(BigWalkTestBase):
    options = {"start_with_map_room_open": True}

    def test_map_room_precollected(self) -> None:
        self.assertIn(data.MAP_ROOM_ITEM_NAME, precollected(self))
        self.assertNotIn(data.MAP_ROOM_ITEM_NAME, pool(self))


class TestLinearKeys(BigWalkTestBase):
    options = {"linear_keys": True}

    def test_each_deposit_holds_its_door(self) -> None:
        for tower in data.TOWERS:
            item = self.multiworld.get_location(tower.location_name, self.player).item
            self.assertEqual(item.name, tower.item_name)
            self.assertNotIn(tower.item_name, pool(self))


class TestLinearKeysTight(BigWalkTestBase):
    options = {"linear_keys": True, "start_with_arch_doors_open": [], "lock_puzzle_needs": True,
               "start_with_drawbridge_open": False}


class TestLinearKeysWithDrawbridgeOpen(BigWalkTestBase):
    options = {"linear_keys": True, "start_with_drawbridge_open": True}

    def test_drawbridge_precollected_not_locked(self) -> None:
        self.assertIn(data.DRAWBRIDGE_ITEM_NAME, precollected(self))
        deposit = data.tower_by_prop("bigKeyIntro").location_name
        self.assertIsNone(self.multiworld.get_location(deposit, self.player).item)


class TestLinearPuzzles(BigWalkTestBase):
    options = {"linear_puzzles": True}

    def test_every_puzzle_but_the_purple_ones_holds_a_gourd(self) -> None:
        for puzzle in data.PUZZLES:
            item = self.multiworld.get_location(puzzle.location_name, self.player).item
            if puzzle.prop_name in data.CHAIRLIFT_PUZZLES:
                self.assertIsNone(item, puzzle.location_name)
            else:
                self.assertEqual(item.name, data.GOURD_ITEM_NAME, puzzle.location_name)
        self.assertEqual(pool(self).count(data.GOURD_ITEM_NAME), len(data.CHAIRLIFT_PUZZLES))


class TestBigGamePuzzlesDisabled(BigWalkTestBase):
    options = {"include_big_game_puzzles": "disabled", "linear_puzzles": True}

    def test_no_purple_puzzle(self) -> None:
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        for puzzle in data.PUZZLES:
            self.assertEqual(puzzle.prop_name not in data.CHAIRLIFT_PUZZLES, puzzle.location_name in names)
        # The purple puzzles hold no gourd anyway: those gourds stay in the pool.
        self.assertEqual(pool(self).count(data.GOURD_ITEM_NAME), len(data.CHAIRLIFT_PUZZLES))


class TestBigGamePuzzlesWhenUnlocked(BigWalkTestBase):
    options = {"include_big_game_puzzles": "when_unlocked", "lock_puzzle_needs": False}

    def test_purple_puzzle_needs_the_ending(self) -> None:
        name = next(p.location_name for p in data.PUZZLES if p.prop_name == "gourdSpeedObby")
        self.collect_all_but([data.BLACK_MONOLITH.item_name])
        self.assertFalse(self.can_reach_location(name))
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(self.can_reach_location(name))


class TestCutKeySanityOff(BigWalkTestBase):
    options = {"cut_key_sanity": "off"}

    def test_no_cut(self) -> None:
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        for tower in data.TOWERS:
            for name in data.cut_locations(tower):
                self.assertNotIn(name, names)


class TestCutKeySanityJunk(BigWalkTestBase):
    options = {"cut_key_sanity": "junk"}

    def test_cuts_excluded(self) -> None:
        excluded = [name for tower in data.TOWERS for name in data.cut_locations(tower)
                    if self.multiworld.get_location(name, self.player).progress_type == LocationProgressType.EXCLUDED]
        self.assertTrue(excluded)


class TestEveryJunkAtOnce(BigWalkTestBase):
    """Two options asking for junk share one budget: the seed still fills."""

    options = {"cut_key_sanity": "junk", "object_sanity": "every", "object_sanity_items": "junk", "linear_puzzles": True}


class TestMonumentSanity(BigWalkTestBase):
    options = {"monument_sanity": True}

    def test_seven_checks_and_their_gourds(self) -> None:
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        for tower in data.TOWERS:
            self.assertIn(data.monument_full_location_name(tower), names)
        red = data.monument_full_location_name(data.tower_by_prop("bigKeyRedZone"))
        self.collect_all_but([data.GOURD_ITEM_NAME])
        self.assertFalse(self.can_reach_location(red))
        for _ in range(data.tower_by_prop("bigKeyRedZone").slots):
            self.collect(self.get_item_by_name(data.GOURD_ITEM_NAME))
        self.assertTrue(self.can_reach_location(red))
        self.assertEqual(self.world.fill_slot_data()["monument_full_keys"], [t.prop_name for t in data.TOWERS])


class TestLinearTowers(BigWalkTestBase):
    options = {"linear_towers": True}

    def test_each_monument_holds_its_key(self) -> None:
        for tower in data.TOWERS:
            item = self.multiworld.get_location(data.monument_full_location_name(tower), self.player).item
            self.assertEqual(item.name, data.key_item_name(tower))
            self.assertNotIn(data.key_item_name(tower), pool(self))


class TestLinearTowersInOrder(BigWalkTestBase):
    options = {"linear_towers": True, "tower_mode": "in_order"}

    def test_a_later_monument_needs_the_ones_before(self) -> None:
        last = data.tower_by_prop(self.world.tower_order[-1])
        self.collect_all_but([data.GOURD_ITEM_NAME])
        for _ in range(self.world.tower_slots(last)):
            self.collect(self.get_item_by_name(data.GOURD_ITEM_NAME))
        self.assertFalse(self.can_reach_location(data.monument_full_location_name(last)))


class TestLinearTowersByItemTight(BigWalkTestBase):
    options = {"linear_towers": True, "linear_keys": True, "tower_mode": "by_item", "start_with_arch_doors_open": [],
               "lock_puzzle_needs": True}


class TestFastGolf(BigWalkTestBase):
    options = {"fast_golf": True}

    def test_slot_data(self) -> None:
        self.assertIs(self.world.fill_slot_data()["fast_golf"], True)
