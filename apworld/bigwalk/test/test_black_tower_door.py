from .. import data
from .bases import BigWalkTestBase

REACHED = data.tower_reached_location_name(data.BLACK_MONOLITH)
FULL = data.monument_full_location_name(data.BLACK_MONOLITH)

# The drawbridge and the four coloured towers: what the game waits for before it opens the door.
FIVE_SLOTS = sum(tower.slots for tower in data.TOWERS if tower not in (data.BLACK_MONOLITH, data.GREEN_DOME))


class TestTheDoorWaitsForTheFiveMonuments(BigWalkTestBase):
    options = {"open_black_tower": False, "tower_sanity": True, "monument_sanity": True, "tower_mode": "all_open"}
    run_default_tests = False

    def test_the_tower_is_reached_once_the_five_are_full(self) -> None:
        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.collect_gourds(FIVE_SLOTS - 1)
        self.assertFalse(self.can_reach_location(REACHED))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location(REACHED))

    def test_its_monument_is_full_after_theirs(self) -> None:
        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.collect_gourds(FIVE_SLOTS + data.BLACK_MONOLITH.slots - 1)
        self.assertFalse(self.can_reach_location(FULL))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location(FULL))


class TestTheDoorOpensAtTheBlackTowersTurn(BigWalkTestBase):
    options = {"open_black_tower": False, "tower_sanity": True, "tower_mode": "by_item", "first_tower_open": False}
    run_default_tests = False

    def test_the_tower_waits_for_its_progressive_monument(self) -> None:
        position = self.world.tower_index(data.BLACK_MONOLITH.prop_name) + 1
        self.collect_all_but(data.PROGRESSIVE_MONUMENT_ITEM_NAME)
        monuments = self.get_items_by_name(data.PROGRESSIVE_MONUMENT_ITEM_NAME)
        self.collect(monuments[:position - 1])
        self.assertFalse(self.can_reach_location(REACHED))
        self.collect(monuments[position - 1:position])
        self.assertTrue(self.can_reach_location(REACHED))


class TestAnOpenDoorAsksNothing(BigWalkTestBase):
    options = {"open_black_tower": True, "tower_sanity": True, "tower_mode": "all_open"}
    run_default_tests = False

    def test_the_tower_needs_no_gourd(self) -> None:
        self.collect_all_but(data.GOURD_ITEM_NAME)
        self.assertTrue(self.can_reach_location(REACHED))
