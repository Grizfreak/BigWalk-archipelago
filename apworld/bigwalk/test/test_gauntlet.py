"""
`gauntlet_mode: locked_stages` makes the Silent Gauntlet seven checks and seven
items: a stage's puzzle is a check, and the stairway out of the stage is an item.
`vanilla` leaves it exactly as it was.
"""

from .. import data, items, locations, regions
from .bases import BigWalkTestBase, build_like_universal_tracker, world_shape

STAGE_LOCATIONS = {stage.location_name for stage in data.GAUNTLET_STAGES}
STAGE_ITEMS = {stage.item_name for stage in data.GAUNTLET_STAGES}


def pool_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


def player_locations(test: BigWalkTestBase) -> set[str]:
    return {location.name for location in test.multiworld.get_locations(test.player)}


class TestVanilla(BigWalkTestBase):
    """The default: the Gauntlet has no checks and no items."""

    options: dict = {}

    def test_no_stage_location_or_item(self) -> None:
        self.assertFalse(player_locations(self) & STAGE_LOCATIONS)
        self.assertFalse(set(pool_names(self)) & STAGE_ITEMS)

    def test_the_mod_is_told_to_leave_it_alone(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["gauntlet_mode"], "vanilla")

    def test_the_goal_asks_for_nothing_more(self) -> None:
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(self.can_reach_location(locations.VICTORY_EVENT_NAME))


class TestLockedStages(BigWalkTestBase):
    options = {"gauntlet_mode": "locked_stages"}
    run_default_tests = False

    def test_seven_checks_in_the_ending_zone(self) -> None:
        for stage in data.GAUNTLET_STAGES:
            location = self.multiworld.get_location(stage.location_name, self.player)
            self.assertEqual(location.parent_region.name, regions.ENDING_ZONE, stage.location_name)

    def test_one_item_each_in_the_pool(self) -> None:
        pool = pool_names(self)
        for stage in data.GAUNTLET_STAGES:
            self.assertEqual(pool.count(stage.item_name), 1, stage.item_name)

    def test_the_pool_still_fits_the_locations(self) -> None:
        real = [location for location in self.multiworld.get_locations(self.player) if location.address is not None]
        self.assertEqual(len(pool_names(self)), len(real))

    def test_ids_are_the_chamber_systems(self) -> None:
        for stage in data.GAUNTLET_STAGES:
            expected = data.BASE_ID + data.GAUNTLET_ID_OFFSET + stage.system_value
            self.assertEqual(locations.LOCATION_NAME_TO_ID[stage.location_name], expected)
            self.assertEqual(items.ITEM_NAME_TO_ID[stage.item_name], expected)

    def test_a_stage_needs_the_stairways_below_it(self) -> None:
        """Stage k is reached through the k-1 stairways before it, and no other."""
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        for stage in data.GAUNTLET_STAGES:
            self.assertTrue(self.can_reach_location(stage.location_name), stage.location_name)
            self.collect_by_name(stage.item_name)

    def test_the_second_stage_is_shut_until_the_first_stairway(self) -> None:
        self.collect_all_but([data.GAUNTLET_STAGES[0].item_name])
        self.assertFalse(self.can_reach_location(data.GAUNTLET_STAGES[1].location_name))
        self.assertTrue(self.can_reach_location(data.GAUNTLET_STAGES[0].location_name))

    def test_leaving_the_gauntlet_needs_all_seven(self) -> None:
        for stage in data.GAUNTLET_STAGES:
            with self.subTest(missing=stage.item_name):
                self.multiworld.state = self.multiworld.state.__class__(self.multiworld)
                self.collect_all_but([stage.item_name])
                self.assertFalse(self.can_reach_location(locations.VICTORY_EVENT_NAME))

    def test_slot_data_tells_the_mod(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["gauntlet_mode"], "locked_stages")
        self.assertEqual(slot_data["gauntlet_id_offset"], data.GAUNTLET_ID_OFFSET)
        self.assertEqual(slot_data["gauntlet_stage_systems"], [f"GauntletChamber{n}" for n in range(7)])


class TestTheStagesStayLocal(BigWalkTestBase):
    options = {"gauntlet_mode": "locked_stages"}
    run_default_tests = False

    def test_they_are_local_by_default(self) -> None:
        self.assertTrue(STAGE_ITEMS <= set(self.world.options.local_items.value))


class TestTheStagesMayTravel(BigWalkTestBase):
    options = {"gauntlet_mode": "locked_stages", "gauntlet_stages_local": False}
    run_default_tests = False

    def test_they_are_not_forced_home(self) -> None:
        self.assertFalse(STAGE_ITEMS & set(self.world.options.local_items.value))


class TestWithPuzzleNeeds(BigWalkTestBase):
    """A stage also asks for what its puzzle, and the ones below it, are built from."""

    options = {"gauntlet_mode": "locked_stages", "lock_puzzle_needs": True}
    run_default_tests = False

    def test_the_first_stage_needs_its_own_parts(self) -> None:
        first = data.GAUNTLET_STAGES[0]
        wanted = {need.item_name for need in data.gauntlet_needs_through(first)}
        self.assertTrue(wanted)
        self.collect_all_but(sorted(wanted))
        self.assertFalse(self.can_reach_location(first.location_name))
        self.collect_by_name(sorted(wanted))
        self.assertTrue(self.can_reach_location(first.location_name))

    def test_a_later_stage_adds_the_parts_of_every_stage_below(self) -> None:
        last = data.GAUNTLET_STAGES[-1]
        through_last = {need.key for need in data.gauntlet_needs_through(last)}
        through_first = {need.key for need in data.gauntlet_needs_through(data.GAUNTLET_STAGES[0])}
        self.assertTrue(through_first < through_last)


class TestTheGoalIsSomethingElse(BigWalkTestBase):
    """Only the Gauntlet goal asks for the stairways."""

    options = {"gauntlet_mode": "locked_stages", "goal": "big_wall"}
    run_default_tests = False

    def test_the_bell_asks_for_no_stairway(self) -> None:
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(self.can_reach_location(locations.VICTORY_EVENT_NAME))


class TestTheTrackerSeesTheMode(BigWalkTestBase):
    options = {"gauntlet_mode": "locked_stages"}
    run_default_tests = False

    def test_it_lands_on_this_exact_world(self) -> None:
        tracked = build_like_universal_tracker({}, self.world.fill_slot_data())
        self.assertEqual(world_shape(tracked), world_shape(self.multiworld))
        self.assertTrue(STAGE_LOCATIONS <= {
            name for region in world_shape(tracked)["regions"].values() for name in region})


class TestPuzzlesSkippable(BigWalkTestBase):
    """Without `gauntlet_puzzles_required` an item opens its stage whole, so a puzzle needs only itself."""

    options = {"gauntlet_mode": "locked_stages", "gauntlet_puzzles_required": False, "lock_puzzle_needs": True}
    run_default_tests = False

    def test_the_slot_says_so(self) -> None:
        self.assertIs(self.world.fill_slot_data()["gauntlet_puzzles_required"], False)

    def test_a_late_stage_asks_only_for_its_own_parts(self) -> None:
        last = data.GAUNTLET_STAGES[-1]
        own = {need.item_name for need in data.gauntlet_needs_of(last)}
        stairways = {stage.item_name for stage in data.GAUNTLET_STAGES[:-1]}
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.collect_by_name(sorted(stairways | own))
        self.assertTrue(self.can_reach_location(last.location_name))

    def test_a_stage_still_needs_every_stairway_below_it(self) -> None:
        """Skipping puzzles never skips the way there: stage 4 is shut while any of 1, 2 or 3 is missing."""
        fourth = data.GAUNTLET_STAGES[3]
        own = sorted(need.item_name for need in data.gauntlet_needs_of(fourth))
        for missing in data.GAUNTLET_STAGES[:3]:
            with self.subTest(missing=missing.item_name):
                self.multiworld.state = self.multiworld.state.__class__(self.multiworld)
                self.collect_by_name(data.BLACK_MONOLITH.item_name)
                self.collect_by_name(own)
                self.collect_by_name(sorted(
                    stage.item_name for stage in data.GAUNTLET_STAGES[:3] if stage != missing))
                self.assertFalse(self.can_reach_location(fourth.location_name))
                self.collect_by_name(missing.item_name)
                self.assertTrue(self.can_reach_location(fourth.location_name))

    def test_a_late_stage_is_cheaper_than_when_puzzles_are_required(self) -> None:
        last = data.GAUNTLET_STAGES[-1]
        self.assertLess(len(data.gauntlet_needs_of(last)), len(data.gauntlet_needs_through(last)))

    def test_the_goal_asks_for_the_stairways_and_no_puzzle_part(self) -> None:
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.collect_by_name(sorted(STAGE_ITEMS))
        self.assertTrue(self.can_reach_location(locations.VICTORY_EVENT_NAME))


class TestPuzzlesRequiredByDefault(BigWalkTestBase):
    options = {"gauntlet_mode": "locked_stages"}
    run_default_tests = False

    def test_the_slot_says_so(self) -> None:
        self.assertIs(self.world.fill_slot_data()["gauntlet_puzzles_required"], True)


class TestAnotherGoalStillGetsTheStages(BigWalkTestBase):
    """
    With a goal that is not the Gauntlet, `locked_stages` still adds its seven
    checks and items, behind the chapel, and the goal asks for none of them.
    """

    options = {"gauntlet_mode": "locked_stages", "goal": "big_collection"}
    run_default_tests = False

    def test_the_stages_are_there_behind_the_chapel(self) -> None:
        for stage in data.GAUNTLET_STAGES:
            location = self.multiworld.get_location(stage.location_name, self.player)
            self.assertEqual(location.parent_region.name, regions.ENDING_ZONE)

    def test_a_stage_is_out_of_reach_without_the_chapel(self) -> None:
        self.assertFalse(self.can_reach_location(data.GAUNTLET_STAGES[0].location_name))
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(self.can_reach_location(data.GAUNTLET_STAGES[0].location_name))

    def test_the_goal_does_not_ask_for_them(self) -> None:
        self.collect_gourds(self.world.deposit_goal)
        self.assertTrue(self.can_reach_location(locations.VICTORY_EVENT_NAME))
