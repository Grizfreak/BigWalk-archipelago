from .. import data
from .bases import BigWalkTestBase


class TestGauntletGoal(BigWalkTestBase):
    """The default goal: everything past the Black Monolith Key."""

    options = {"goal": "gauntlet"}

    def test_victory_needs_the_black_monolith_key(self) -> None:
        self.collect_all_but(data.BLACK_MONOLITH.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertTrue(self.can_reach_location("Victory"))


class TestEndingGoal(BigWalkTestBase):
    options = {"goal": "ending"}
    run_default_tests = False

    def test_victory_needs_the_black_monolith_key(self) -> None:
        self.collect_all_but(data.BLACK_MONOLITH.item_name)
        self.assertFalse(self.can_reach_location("Victory"))


class TestDepositGoal(BigWalkTestBase):
    options = {"goal": "deposits", "deposit_goal_amount": 20}
    run_default_tests = False

    def test_victory_needs_gourds_not_keys(self) -> None:
        # A deposit goal must be winnable without ever opening the ending, so
        # every big key in the world should be irrelevant to it.
        for tower in data.TOWERS:
            self.collect_by_name(tower.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_gourds(20)
        self.assertTrue(self.can_reach_location("Victory"))


class TestDepositGoalClampedToAvailableSlots(BigWalkTestBase):
    """
    Asking for more deposits than there are slots must not produce an
    unwinnable seed: the goal is lowered to what exists instead.
    """

    options = {
        "goal": "deposits",
        "deposit_goal_amount": 45,
        "green_dome_deposits": "excluded",  # only 30 slots remain
    }
    run_default_tests = False

    def test_goal_is_clamped(self) -> None:
        self.assertEqual(self.world.deposit_goal, 30)
        self.collect_gourds(30)
        self.assertTrue(self.can_reach_location("Victory"))


class TestSecondEndingGoal(BigWalkTestBase):
    """The ending behind the Hub Secret Door, and only its item opens it."""

    options = {"goal": "second_ending"}
    run_default_tests = False

    def test_victory_needs_the_green_dome(self) -> None:
        self.collect_all_but(data.GREEN_DOME.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertTrue(self.can_reach_location("Victory"))

    def test_victory_does_not_need_the_black_monolith_key(self) -> None:
        # Deliberate, and the assumption most likely to be wrong: in vanilla
        # this zone is sealed until the game has been finished once, and the
        # mod removes that seal from the first session. If an in-game test
        # ever shows otherwise, this is the test that should fail first.
        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertTrue(self.can_reach_location("Victory"))


class TestSecondEndingGoalRepairsAnExcludedGreenDome(BigWalkTestBase):
    """
    Asking for the second ending while excluding the tower that opens it must
    not produce an unfinishable seed: the tower comes back, without its
    fifteen deposit slots.
    """

    options = {"goal": "second_ending", "green_dome_deposits": "excluded"}
    run_default_tests = False

    def test_the_green_dome_comes_back(self) -> None:
        self.assertEqual(self.world.options.green_dome_deposits.current_key, "key_only")
        self.assertIn(data.GREEN_DOME, self.world.towers)
        self.assertTrue(self.get_items_by_name(data.HUB_SECRET_DOOR_ITEM_NAME))

    def test_the_grind_does_not(self) -> None:
        self.assertEqual(len(self.get_items_by_name(data.GOURD_ITEM_NAME)),
                         data.BASE_MONUMENT_SLOTS)

    def test_victory_is_reachable(self) -> None:
        self.collect_by_name(data.HUB_SECRET_DOOR_ITEM_NAME)
        self.assertTrue(self.can_reach_location("Victory"))
