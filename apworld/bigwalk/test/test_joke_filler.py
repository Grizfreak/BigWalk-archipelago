"""The community's thirteen wrong names for a Gourd, as filler that does nothing."""

from .. import data, items
from .bases import BigWalkTestBase

JOKES = set(items.JOKE_ITEM_NAMES)


def pool(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestTheNames(BigWalkTestBase):
    run_default_tests = False

    def test_there_are_thirteen_and_none_clash(self) -> None:
        self.assertEqual(len(JOKES), 13)
        ids = [items.ITEM_NAME_TO_ID[name] for name in JOKES]
        self.assertEqual(len(set(ids)), 13)
        used = set(items.ITEM_NAME_TO_ID.values()) - set(ids)
        self.assertFalse(set(ids) & used)

    def test_they_sit_clear_of_the_gadgets_the_mod_orders(self) -> None:
        gadgets = [data.BASE_ID + offset for _, offset in data.FILLER_ITEMS]
        for name in JOKES:
            self.assertGreater(items.ITEM_NAME_TO_ID[name], max(gadgets), name)

    def test_they_are_filler(self) -> None:
        for name in JOKES:
            self.assertTrue(self.world.create_item(name).filler, name)


class TestShareOfFiller(BigWalkTestBase):
    options = {"joke_filler_percentage": 100}
    run_default_tests = False

    def test_every_random_filler_is_a_joke_but_the_gear(self) -> None:
        names = pool(self)
        filler = [n for n in names if n in items.FILLER_ITEM_NAMES or n in JOKES]
        gear = set(items.GUARANTEED_GEAR)
        extra = [n for n in filler if n not in gear]
        self.assertTrue(extra)
        self.assertTrue(all(n in JOKES for n in extra))


class TestNone(BigWalkTestBase):
    options = {"joke_filler_percentage": 0}
    run_default_tests = False

    def test_no_joke_in_the_pool(self) -> None:
        self.assertFalse(set(pool(self)) & JOKES)
