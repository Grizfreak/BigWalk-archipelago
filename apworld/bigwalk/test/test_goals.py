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
    options = {"goal": "gourds", "gourds_required": 20}
    run_default_tests = False

    def test_victory_needs_gourds_not_keys(self) -> None:
        # A deposit goal must be winnable without ever opening the ending, so
        # every big key in the world should be irrelevant to it.
        for tower in data.TOWERS:
            self.collect_by_name(tower.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_gourds(20)
        self.assertTrue(self.can_reach_location("Victory"))


class TestDepositGoalAtEverySlot(BigWalkTestBase):
    """The largest deposit goal the option allows is every slot on the island."""

    options = {"goal": "gourds", "gourds_required": data.MAX_MONUMENT_SLOTS}
    run_default_tests = False

    def test_every_gourd_is_needed_and_enough(self) -> None:
        self.collect_gourds(data.MAX_MONUMENT_SLOTS - 1)
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location("Victory"))


class TestSecondEndingGoal(BigWalkTestBase):
    """The ending behind the Hub Secret Door, and only its item opens it."""

    options = {"goal": "secret_ending"}
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
