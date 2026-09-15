from .. import data
from .bases import BigWalkTestBase


class TestDefaultPool(BigWalkTestBase):
    options: dict = {}

    def test_one_gourd_per_monument_slot(self) -> None:
        # The pool must be able to fill every monument in play, or a big key
        # becomes impossible to reach. 45 with the Green Dome included.
        self.assertEqual(len(self.get_items_by_name(data.GOURD_ITEM_NAME)),
                         data.MAX_MONUMENT_SLOTS)

    def test_tutorial_key_is_precollected_not_shuffled(self) -> None:
        self.assertFalse(self.get_items_by_name(data.TUTORIAL_KEY_ITEM_NAME))
        precollected = [item.name for item in self.multiworld.precollected_items[self.player]]
        self.assertIn(data.TUTORIAL_KEY_ITEM_NAME, precollected)

    def test_drawbridge_deposit_is_free_when_the_key_is_precollected(self) -> None:
        # The mod pins a received key into its plinth and reports that location
        # itself, so this one is already checked at connection time.
        self.assertTrue(self.can_reach_location("Drawbridge Key Deposit"))

    def test_puzzles_need_nothing(self) -> None:
        # No received item ever gates a puzzle: solving one is always possible.
        self.assertTrue(self.can_reach_location("Cabin Fever"))
        self.assertTrue(self.can_reach_location("Poet and Pontiff"))


class TestGreenDomeExcluded(BigWalkTestBase):
    options = {"green_dome_deposits": "excluded"}
    run_default_tests = False

    def test_key_and_location_are_gone(self) -> None:
        self.assertFalse(self.get_items_by_name(data.GREEN_DOME_KEY_ITEM_NAME))
        self.assertNotIn("Goodbye Keyhole Key Deposit",
                         [location.name for location in self.multiworld.get_locations(self.player)])

    def test_pool_shrinks_to_the_remaining_slots(self) -> None:
        self.assertEqual(len(self.get_items_by_name(data.GOURD_ITEM_NAME)),
                         data.BASE_MONUMENT_SLOTS)


class TestKeyRequirementsAreCumulative(BigWalkTestBase):
    """
    A deposited gourd cannot be taken back out of a monument in the base game,
    so gourds are spent rather than lent and every key is charged on top of
    the monuments already filled.
    """

    options: dict = {}
    run_default_tests = False

    def test_cheapest_monument_costs_its_own_slots(self) -> None:
        self.assertEqual(self.world.key_requirements["bigKeyIntro"], 4)

    def test_last_key_costs_the_whole_pool(self) -> None:
        self.assertEqual(self.world.key_requirements[data.GREEN_DOME.prop_name],
                         data.MAX_MONUMENT_SLOTS)

    def test_requirements_never_decrease_with_slot_count(self) -> None:
        by_cost = sorted(self.world.towers, key=self.world.slots_for)
        costs = [self.world.key_requirements[tower.prop_name] for tower in by_cost]
        self.assertEqual(costs, sorted(costs))
        self.assertEqual(costs[-1], sum(self.world.slots_for(t) for t in self.world.towers))

    def test_dam_deposit_requires_thirty_gourds(self) -> None:
        self.collect_gourds(29)
        self.assertFalse(self.can_reach_location("Dam Key Deposit"))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location("Dam Key Deposit"))


class TestShuffledTutorialKey(BigWalkTestBase):
    options = {"start_with_tutorial_key": False}
    run_default_tests = False

    def test_key_is_in_the_pool_and_its_deposit_costs_gourds(self) -> None:
        self.assertTrue(self.get_items_by_name(data.TUTORIAL_KEY_ITEM_NAME))
        self.assertFalse(self.can_reach_location("Drawbridge Key Deposit"))
        self.collect_gourds(4)
        self.assertTrue(self.can_reach_location("Drawbridge Key Deposit"))
