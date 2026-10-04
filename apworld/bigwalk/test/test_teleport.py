"""The in-world teleport buttons: an option, and with `items` six Teleporter items."""

from .. import data, items
from .bases import BigWalkTestBase

TELEPORTERS = set(items.TELEPORT_ITEM_NAMES)


def pool(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestOffByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_no_teleporter_in_the_pool(self) -> None:
        self.assertFalse(set(pool(self)) & TELEPORTERS)
        self.assertEqual(self.world.fill_slot_data()["teleport_buttons"], "off")

    def test_five_destinations_and_no_green_dome(self) -> None:
        keys = [key for key, _ in data.TELEPORT_DESTINATIONS]
        self.assertEqual(keys, ["red", "green", "blue", "yellow", "black", "gauntlet"])
        self.assertEqual(len(TELEPORTERS), 6)

    def test_ids_are_clear_of_everything_else(self) -> None:
        ids = [items.ITEM_NAME_TO_ID[name] for name in items.TELEPORT_ITEM_NAMES]
        self.assertEqual(len(set(ids)), 6)
        others = set(items.ITEM_NAME_TO_ID.values()) - set(ids)
        self.assertFalse(set(ids) & others)
        self.assertEqual(ids, [data.BASE_ID + data.TELEPORT_ID_OFFSET + i for i in range(6)])


class TestFree(BigWalkTestBase):
    options = {"teleport_buttons": "free"}
    run_default_tests = False

    def test_the_mode_travels_and_no_item_is_added(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["teleport_buttons"], "free")
        self.assertFalse(set(pool(self)) & TELEPORTERS)


class TestWithTowers(BigWalkTestBase):
    options = {"teleport_buttons": "with_towers"}
    run_default_tests = False

    def test_the_mode_travels_and_no_item_is_added(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["teleport_buttons"], "with_towers")
        self.assertFalse(set(pool(self)) & TELEPORTERS)


class TestItems(BigWalkTestBase):
    options = {"teleport_buttons": "items"}
    run_default_tests = False

    def test_one_teleporter_each_in_the_pool(self) -> None:
        names = pool(self)
        for name in TELEPORTERS:
            self.assertEqual(names.count(name), 1, name)

    def test_they_are_useful_not_progression(self) -> None:
        for name in TELEPORTERS:
            self.assertFalse(self.world.create_item(name).advancement, name)

    def test_the_pool_still_fits_the_locations(self) -> None:
        real = [location for location in self.multiworld.get_locations(self.player) if location.address is not None]
        self.assertEqual(len(pool(self)), len(real))

    def test_slot_data_lists_the_destinations_in_item_order(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["teleport_destinations"], ["red", "green", "blue", "yellow", "black", "gauntlet"])
        self.assertEqual(slot_data["teleport_id_offset"], data.TELEPORT_ID_OFFSET)
