"""Traps, bonuses and DeathLink: what goes into the pool, and what the mod is told."""

from .. import data, items
from .bases import BigWalkTestBase

TRAPS = set(items.TRAP_ITEM_NAMES)
HARD = {trap.item_name for trap in data.TRAPS if trap.hard}
BONUSES = set(items.BONUS_ITEM_NAMES)


def pool(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestTheNames(BigWalkTestBase):
    run_default_tests = False

    def test_every_one_is_big_and_a_word(self) -> None:
        for name in TRAPS | BONUSES:
            first, *rest = name.split(" ")
            self.assertEqual(first, "Big", name)
            self.assertEqual(len(rest), 1, name)

    def test_ids_are_their_own(self) -> None:
        ids = [items.ITEM_NAME_TO_ID[name] for name in TRAPS | BONUSES]
        self.assertEqual(len(set(ids)), len(ids))
        self.assertEqual(len(set(items.ITEM_NAME_TO_ID.values())), len(items.ITEM_NAME_TO_ID))

    def test_the_retired_trap_id_is_not_reused(self) -> None:
        self.assertNotIn(data.BASE_ID + 9_101, items.ITEM_NAME_TO_ID.values())

    def test_classifications(self) -> None:
        for name in TRAPS:
            self.assertTrue(self.world.create_item(name).trap, name)
        for name in BONUSES:
            self.assertTrue(self.world.create_item(name).useful, name)


class TestByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_no_trap_but_bonuses(self) -> None:
        self.assertFalse(set(pool(self)) & TRAPS)
        self.assertEqual(self.world.options.bonus_fill_percentage.value, 25)

    def test_death_link_is_off(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["death_link"], "off")


class TestEveryFillerATrap(BigWalkTestBase):
    options = {"trap_fill_percentage": 100, "bonus_fill_percentage": 0}
    run_default_tests = False

    def test_every_trap_can_come(self) -> None:
        self.assertTrue(set(pool(self)) & TRAPS)
        self.assertFalse(set(pool(self)) & BONUSES)


class TestNoHardTraps(BigWalkTestBase):
    options = {"trap_fill_percentage": 100, "hard_traps": False}
    run_default_tests = False

    def test_only_standard_traps(self) -> None:
        names = set(pool(self))
        self.assertTrue(names & TRAPS)
        self.assertFalse(names & HARD)


class TestWeights(BigWalkTestBase):
    options = {"trap_fill_percentage": 100, "trap_weights": {"Big Night": 1}}
    run_default_tests = False

    def test_a_trap_left_out_never_comes(self) -> None:
        self.assertEqual(set(pool(self)) & TRAPS, {"Big Night"})


class TestEveryWeightZero(BigWalkTestBase):
    options = {"trap_fill_percentage": 100, "trap_weights": {}}

    def test_no_trap_and_the_pool_still_fills(self) -> None:
        self.assertFalse(set(pool(self)) & TRAPS)


class TestBonuses(BigWalkTestBase):
    options = {"bonus_fill_percentage": 100}
    run_default_tests = False

    def test_bonuses_in_the_pool(self) -> None:
        self.assertTrue(set(pool(self)) & BONUSES)


class TestBonusWeights(BigWalkTestBase):
    options = {"bonus_fill_percentage": 100, "bonus_weights": {"Big Jump": 5, "Big Speed": 0}}
    run_default_tests = False

    def test_only_the_bonus_left(self) -> None:
        self.assertEqual(set(pool(self)) & BONUSES, {"Big Jump"})


class TestEveryBonusWeightZero(BigWalkTestBase):
    options = {"bonus_fill_percentage": 100, "bonus_weights": {}}

    def test_no_bonus_and_the_pool_still_fills(self) -> None:
        self.assertFalse(set(pool(self)) & BONUSES)


class TestSlotData(BigWalkTestBase):
    options = {
        "death_link": "both",
        "death_link_effect": "roulette",
        "death_link_roulette_hard_traps": False,
        "trap_duration": 45,
    }
    run_default_tests = False

    def test_the_mod_is_told(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["death_link"], "both")
        self.assertEqual(slot["death_link_triggers"], ["puzzle_failed"])
        self.assertEqual(slot["death_link_effect"], "roulette")
        self.assertEqual(slot["trap_duration"], 45)
        self.assertTrue(slot["traps_spare_the_gauntlet"])

    def test_the_roulette_leaves_the_hard_traps_out(self) -> None:
        roulette = self.world.fill_slot_data()["death_link_roulette"]
        hard_keys = {trap.key for trap in data.TRAPS if trap.hard}
        self.assertTrue(roulette)
        self.assertFalse(set(roulette) & hard_keys)

    def test_its_own_weights(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["death_link_amnesty"], 0)
        self.assertFalse(slot["death_link_trap_on_send"])

    def test_every_effect_has_its_id(self) -> None:
        effects = self.world.fill_slot_data()["effect_items"]
        for effect in (*data.TRAPS, *data.BONUSES):
            self.assertEqual(effects[str(effect.offset)], effect.key)


class TestDeathLinkTrapWeights(BigWalkTestBase):
    options = {"death_link": "receive", "death_link_trap_weights": {"Big Night": 3, "Big Trip": 5},
               "death_link_trap_on_send": True}
    run_default_tests = False

    def test_only_the_listed_traps(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["death_link_roulette"], {"night": 3, "trip": 5})
        self.assertTrue(slot["death_link_trap_on_send"])


class TestTwentySecondsByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_the_lasting_traps_last_twenty_seconds(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["trap_duration"], 20)

    def test_nothing_is_linked_and_a_death_has_no_echo_by_default(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertFalse(slot["trap_link"])
        self.assertFalse(slot["death_link_trap_on_send"])
        self.assertEqual(slot["death_link_target"], "everyone")
        self.assertEqual(slot["death_link_effect"], "knockout")


class TestLinkedAndSingle(BigWalkTestBase):
    options = {
        "death_link": "both",
        "death_link_target": "one_player",
        "death_link_trap_on_send": True,
        "trap_link": True,
    }
    run_default_tests = False

    def test_the_mod_is_told(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertTrue(slot["trap_link"])
        self.assertTrue(slot["death_link_trap_on_send"])
        self.assertEqual(slot["death_link_target"], "one_player")
