"""L6 `linear_arch_doors`: the hub's arch doors as in the game, opened by their buttons; no door is an item."""

from BaseClasses import CollectionState

from .. import data, regions
from .bases import BigWalkTestBase


class TestLinearArchDoors(BigWalkTestBase):
    options = {"linear_arch_doors": True, "start_with_drawbridge_open": False}
    run_default_tests = False

    def test_no_door_items(self) -> None:
        names = {item.name for item in self.multiworld.itempool if item.player == self.player}
        for door in data.ARCH_DOORS:
            self.assertNotIn(door.item_name, names)
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["locked_arch_doors"], [])
        self.assertTrue(slot_data["linear_arch_doors"])

    def test_the_way_out_is_the_drawbridge(self) -> None:
        state = CollectionState(self.multiworld)
        self.assertFalse(state.can_reach_region(regions.OVERWORLD, self.player))
        state.collect(self.world.create_item(data.DRAWBRIDGE_ITEM_NAME))
        self.assertTrue(state.can_reach_region(regions.OVERWORLD, self.player))

    def test_the_drawbridge_comes_early(self) -> None:
        self.assertEqual(self.multiworld.local_early_items[self.player].get(data.DRAWBRIDGE_ITEM_NAME), 1)


class TestLinearArchDoorsDrawbridgeOpen(BigWalkTestBase):
    options = {"linear_arch_doors": True, "start_with_drawbridge_open": True}
    run_default_tests = False

    def test_free_way_out(self) -> None:
        state = CollectionState(self.multiworld)
        for item in self.multiworld.precollected_items[self.player]:
            state.collect(item)
        self.assertTrue(state.can_reach_region(regions.OVERWORLD, self.player))


class TestLinearArchDoorsGenerates(BigWalkTestBase):
    options = {"linear_arch_doors": True}
