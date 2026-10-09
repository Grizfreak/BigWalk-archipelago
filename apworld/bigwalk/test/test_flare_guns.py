"""The flare guns as checks and items (`flare_gun_sanity`) and the seed's colors (`random_colors`)."""

from collections import Counter

from .. import data, items
from .bases import BigWalkTestBase

FLARE_GUN_LOCATIONS = {pickup.location_name for pickup in data.FLARE_GUN_PICKUPS}


def mine(test: BigWalkTestBase) -> set[str]:
    return {location.name for location in test.multiworld.get_locations(test.player)}


def pool(test: BigWalkTestBase) -> Counter:
    return Counter(item.name for item in test.multiworld.itempool if item.player == test.player)


class TestOnByDefault(BigWalkTestBase):

    def test_four_checks(self) -> None:
        self.assertEqual(len(FLARE_GUN_LOCATIONS), 4)
        self.assertTrue(FLARE_GUN_LOCATIONS <= mine(self))

    def test_four_guns_and_the_rainbow_in_the_pool(self) -> None:
        names = pool(self)
        for name in data.FLARE_GUN_ITEM_NAMES:
            self.assertEqual(names[name], 1, name)
        self.assertEqual(names[data.RAINBOW_FLARE_GUN_ITEM_NAME], 1)

    def test_a_palette_of_five_to_ten(self) -> None:
        palette = self.world.fill_slot_data()["color_palette"]
        self.assertTrue(5 <= len(palette) <= data.PALETTE_SIZE)
        self.assertEqual(len(set(palette)), len(palette))
        self.assertTrue(all(0 <= index < data.PALETTE_SIZE for index in palette))


class TestSanityOff(BigWalkTestBase):
    options = {"flare_gun_sanity": False}

    def test_no_checks(self) -> None:
        self.assertFalse(FLARE_GUN_LOCATIONS & mine(self))

    def test_only_the_rainbow(self) -> None:
        names = pool(self)
        for name in data.FLARE_GUN_ITEM_NAMES:
            self.assertEqual(names[name], 0, name)
        self.assertEqual(names[data.RAINBOW_FLARE_GUN_ITEM_NAME], 1)


class TestColorsOff(BigWalkTestBase):
    options = {"random_colors": False}

    def test_the_games_own(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertFalse(slot["random_colors"])
        self.assertEqual(slot["color_palette"], [])


class TestLimits(BigWalkTestBase):
    options = {"island_object_limits": {"Flare Gun": 2, "Blue Flare Gun": 0, "Rainbow Flare Gun": 0}}

    def test_the_limit_is_how_many_of_each_the_pool_holds(self) -> None:
        names = pool(self)
        self.assertEqual(names["Flare Gun"], 2)
        self.assertEqual(names["Blue Flare Gun"], 0)
        self.assertEqual(names[data.RAINBOW_FLARE_GUN_ITEM_NAME], 0)


class TestNeverFiller(BigWalkTestBase):
    options = {"flare_gun_sanity": False, "bonus_fill_percentage": 0}

    def test_no_flare_gun_drawn_as_filler(self) -> None:
        # The limits list them (1 each by default), and without the sanity none is in the pool.
        limits = self.multiworld.worlds[self.player].options.island_object_limits.value
        self.assertTrue(set(data.FLARE_GUN_ITEM_NAMES) <= set(limits))
        names = pool(self)
        self.assertEqual(sum(names[n] for n in data.FLARE_GUN_ITEM_NAMES), 0)
