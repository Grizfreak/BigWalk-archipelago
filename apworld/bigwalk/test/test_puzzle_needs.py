"""
`lock_puzzle_needs` turns what a puzzle is built from into items, and gates
the puzzle behind them in logic only. The mod never blocks a puzzle itself: it
hides the parts until their item arrives.
"""

from Options import OptionError

from .. import data, items
from .bases import BigWalkTestBase


def item_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


def puzzle(prop_name: str) -> data.Puzzle:
    return next(p for p in data.PUZZLES if p.prop_name == prop_name)


class TestPuzzleNeedsData(BigWalkTestBase):
    """The table itself: nothing forgotten, nothing misspelled."""

    run_default_tests = False

    def test_every_puzzle_is_categorised(self) -> None:
        self.assertEqual({p.prop_name for p in data.PUZZLES}, set(data.PUZZLE_TAGS))

    def test_every_tag_is_known(self) -> None:
        known = data.PUZZLE_NEED_KEYS | data.NON_ITEM_TAGS
        for prop_name, tags in data.PUZZLE_TAGS.items():
            self.assertFalse(tags - known, prop_name)

    def test_every_need_is_used(self) -> None:
        used = set().union(*data.PUZZLE_TAGS.values()) | set().union(*data.GAUNTLET_STAGE_TAGS)
        self.assertFalse(data.PUZZLE_NEED_KEYS - used)

    def test_gauntlet_tags_are_known(self) -> None:
        self.assertEqual(len(data.GAUNTLET_STAGE_TAGS), 7)
        for tags in data.GAUNTLET_STAGE_TAGS:
            self.assertFalse(tags - (data.PUZZLE_NEED_KEYS | data.NON_ITEM_TAGS))

    def test_ids_are_unique_and_clear(self) -> None:
        ids = [items.ITEM_NAME_TO_ID[need.item_name] for need in data.PUZZLE_NEEDS]
        self.assertEqual(len(set(ids)), len(ids))
        others = [i for n, i in items.ITEM_NAME_TO_ID.items() if n not in items.PUZZLE_NEED_ITEM_NAMES]
        self.assertFalse(set(ids) & set(others))


class TestPuzzleNeedsDisabled(BigWalkTestBase):
    options: dict = {}

    def test_no_need_item_in_the_pool(self) -> None:
        self.assertFalse(set(item_names(self)) & self.world.item_name_groups["Puzzle Needs"])

    def test_a_puzzle_needs_nothing(self) -> None:
        for p in data.PUZZLES:
            self.assertTrue(self.world.get_location(p.location_name).access_rule(self.multiworld.state))


class TestPuzzleNeedsEnabled(BigWalkTestBase):
    # Sync buttons alone open Easy Simultaneous Press, one of the tutorial's,
    # which is the foothold generation insists on.
    options = {"lock_puzzle_needs": True, "gourd_slot_checks": "every_gourd",
               "start_with_random_puzzle_need": False, "start_with_puzzle_needs": ["sync_buttons"]}
    run_default_tests = False

    def test_one_item_per_need_but_the_started_one(self) -> None:
        pool = item_names(self)
        for need in data.PUZZLE_NEEDS:
            expected = 0 if need.key == "sync_buttons" else 1
            self.assertEqual(pool.count(need.item_name), expected, need.item_name)

    def test_a_puzzle_needs_all_of_its_items_and_nothing_else(self) -> None:
        p = puzzle("gourdRingRoom")
        location = self.world.get_location(p.location_name)
        needs = data.puzzle_needs(p)
        self.assertGreater(len(needs), 1)

        for need in needs[:-1]:
            self.collect(self.world.create_item(need.item_name))
        self.assertFalse(location.access_rule(self.multiworld.state))

        self.collect(self.world.create_item(needs[-1].item_name))
        self.assertTrue(location.access_rule(self.multiworld.state))

    def test_a_puzzle_with_no_item_need_stays_free(self) -> None:
        for prop_name in ("gourdKickUpPits", "gourdBasketball"):
            location = self.world.get_location(puzzle(prop_name).location_name)
            self.assertTrue(location.access_rule(self.multiworld.state), prop_name)

    def test_the_tutorials_puzzles_need_their_items_too(self) -> None:
        for prop_name in ("gourdHighButton", "gourdTelescopeToBox", "gourdTellerWindow"):
            location = self.world.get_location(puzzle(prop_name).location_name)
            self.assertFalse(location.access_rule(self.multiworld.state), prop_name)

        self.collect(self.world.create_item(data.PUZZLE_NEEDS_BY_KEY["buttons"].item_name))
        high_button = self.world.get_location(puzzle("gourdHighButton").location_name)
        self.assertTrue(high_button.access_rule(self.multiworld.state))

    def test_slot_data_names_the_needs_in_id_order(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["puzzle_need_keys"], [need.key for need in data.PUZZLE_NEEDS])
        for position, need in enumerate(data.PUZZLE_NEEDS):
            self.assertEqual(
                items.ITEM_NAME_TO_ID[need.item_name],
                data.BASE_ID + slot_data["puzzle_need_id_offset"] + position)

    def test_slot_data(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertTrue(slot_data["lock_puzzle_needs"])
        self.assertEqual(slot_data["start_with_puzzle_needs"], ["sync_buttons"])


class TestStartWithRandomPuzzleNeed(BigWalkTestBase):
    options = {"lock_puzzle_needs": True, "gourd_slot_checks": "every_gourd"}
    run_default_tests = False

    def test_one_need_is_handed_over_and_opens_a_tutorial_puzzle(self) -> None:
        started = self.world.options.start_with_puzzle_needs.value
        self.assertEqual(len(started), 1)
        self.assertTrue(started <= {"buttons", "sync_buttons"})
        self.assertIn(data.PUZZLE_NEEDS_BY_KEY[next(iter(started))].item_name,
                      {item.name for item in self.multiworld.precollected_items[self.player]})
        in_pool = [name for name in item_names(self) if name in items.PUZZLE_NEED_ITEM_NAMES]
        self.assertEqual(len(in_pool), len(data.PUZZLE_NEEDS) - 1)

        opened = [p for p in data.PUZZLES if p.prop_name in data.START_ZONE_PUZZLES
                  and self.world.get_location(p.location_name).access_rule(self.multiworld.state)]
        self.assertTrue(opened)

    def test_slot_data_carries_the_pick(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(len(slot_data["start_with_puzzle_needs"]), 1)


class TestStartWithRandomPuzzleNeedOff(BigWalkTestBase):
    """Off with nothing to start from cannot generate: it must say so, not hang."""

    auto_construct = False

    def test_raises_a_clear_error(self) -> None:
        self.options = {"lock_puzzle_needs": True, "start_with_random_puzzle_need": False}
        with self.assertRaises(OptionError):
            self.world_setup()


class TestStartWithRandomPuzzleNeedSkipped(BigWalkTestBase):
    """A start list that already opens a tutorial puzzle gets nothing more."""

    options = {"lock_puzzle_needs": True, "gourd_slot_checks": "every_gourd",
               "start_with_puzzle_needs": ["sync_buttons"]}
    run_default_tests = False

    def test_nothing_is_added(self) -> None:
        self.assertEqual(self.world.options.start_with_puzzle_needs.value, {"sync_buttons"})


class TestPuzzleNeedsStartWith(BigWalkTestBase):
    options = {"lock_puzzle_needs": True, "gourd_slot_checks": "every_gourd",
               "start_with_puzzle_needs": ["buttons", "lights"]}
    run_default_tests = False

    def test_started_needs_are_handed_over_not_shuffled(self) -> None:
        pool = item_names(self)
        precollected = {item.name for item in self.multiworld.precollected_items[self.player]}
        for key in ("buttons", "lights"):
            name = data.PUZZLE_NEEDS_BY_KEY[key].item_name
            self.assertIn(name, precollected)
            self.assertNotIn(name, pool)
        self.assertEqual(pool.count(data.PUZZLE_NEEDS_BY_KEY["speakers"].item_name), 1)

    def test_a_started_need_is_not_asked_for_again(self) -> None:
        # Cabin Fever needs buttons and nothing else we can lack.
        location = self.world.get_location(puzzle("gourdCabinFever").location_name)
        self.assertTrue(location.access_rule(self.multiworld.state))
