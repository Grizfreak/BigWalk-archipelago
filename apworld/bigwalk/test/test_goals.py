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
