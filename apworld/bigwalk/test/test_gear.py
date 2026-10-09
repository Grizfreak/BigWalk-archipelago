"""How many Backpacks, Belts and Gourd Cartons the pool holds: the *_in_pool options."""

from collections import Counter

from .. import items
from .bases import BigWalkTestBase


def gear(test: BigWalkTestBase) -> Counter[str]:
    names = [item.name for item in test.multiworld.itempool if item.player == test.player]
    return Counter(n for n in names if n in ("Backpack", "Belt", "Gourd Carton"))


class TestByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_two_backpacks_two_belts_one_carton(self) -> None:
        counts = gear(self)
        self.assertGreaterEqual(counts["Backpack"], 2)
        self.assertGreaterEqual(counts["Belt"], 2)
        self.assertGreaterEqual(counts["Gourd Carton"], 1)


class TestMore(BigWalkTestBase):
    options = {"backpacks_in_pool": 8, "belts_in_pool": 8, "gourd_cartons_in_pool": 4}

    def test_the_pool_holds_at_least_that_many(self) -> None:
        counts = gear(self)
        self.assertGreaterEqual(counts["Backpack"], 8)
        self.assertGreaterEqual(counts["Belt"], 8)
        self.assertGreaterEqual(counts["Gourd Carton"], 4)


class TestNone(BigWalkTestBase):
    options = {"backpacks_in_pool": 0, "belts_in_pool": 0, "gourd_cartons_in_pool": 0}

    def test_the_pool_still_fills(self) -> None:
        locations = self.multiworld.get_unfilled_locations(self.player)
        mine = [item for item in self.multiworld.itempool if item.player == self.player]
        self.assertEqual(len(mine), len(locations))


class TestIslandObjectLimits(BigWalkTestBase):
    options = {"gourd_sanity": "every_gourd", "radio_sanity": True, "lock_puzzle_needs": False,
               "bonus_fill_percentage": 0}

    def test_no_object_past_its_limit(self) -> None:
        names = Counter(item.name for item in self.multiworld.itempool if item.player == self.player)
        limits = self.world.options.island_object_limits.value
        for name, limit in limits.items():
            self.assertLessEqual(names[name], limit, name)
        self.assertEqual(names["Flare Gun"] + names["Blue Flare Gun"]
                         + names["Green Flare Gun"] + names["Yellow Flare Gun"] <= 4, True)


class TestAnObjectLeftOut(BigWalkTestBase):
    options = {"island_object_limits": {"Walkie-Talkie": 50}, "bonus_fill_percentage": 0}
    run_default_tests = False

    def test_only_that_one(self) -> None:
        names = {item.name for item in self.multiworld.itempool if item.player == self.player}
        self.assertNotIn("Lamp", names)
        self.assertIn("Walkie-Talkie", names)


class TestPastEveryLimitBonuses(BigWalkTestBase):
    options = {"island_object_limits": {}, "bonus_fill_percentage": 0}

    def test_the_rest_is_bonuses(self) -> None:
        names = Counter(item.name for item in self.multiworld.itempool if item.player == self.player)
        objects = [n for n in items.FILLER_ITEM_NAMES if n not in items.GUARANTEED_GEAR and n not in items.FLARE_GUN_NAMES]
        self.assertFalse(any(names[n] for n in objects))
        self.assertTrue(any(names[n] for n in items.BONUS_ITEM_NAMES))
