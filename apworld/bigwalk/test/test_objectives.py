import unittest

from Options import OptionError

from .. import data, items
from .bases import BigWalkTestBase, build_like_universal_tracker


class TestTheGauntletAndTheGourds(BigWalkTestBase):
    """Two objectives at once: the Gauntlet's end AND the gourds, in any order."""

    options = {"goal": "big_goodbye", "objectives": ["big_collection"], "gourds_required": 20}
    run_default_tests = False

    def test_victory_needs_the_gauntlet_and_the_gourds(self) -> None:
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_gourds(20)
        self.assertTrue(self.can_reach_location("Victory"))

    def test_the_gourds_alone_are_not_enough(self) -> None:
        self.collect_gourds(20)
        self.assertFalse(self.can_reach_location("Victory"))


class TestTheWalkIsGone(unittest.TestCase):
    """big_walk was dropped from the objectives (the player, 2026-10-12): a YAML that still lists it is told so."""

    def test_big_walk_is_an_error(self) -> None:
        with self.assertRaises(OptionError):
            build_like_universal_tracker({"goal": "big_game", "objectives": ["big_walk"]})


class TestAnEndingIsNotAnObjective(unittest.TestCase):
    """The ending is the goal (the player, 2026-10-12): listed among the objectives, it is refused with a clear word."""

    def test_an_ending_in_the_list_is_an_error(self) -> None:
        for ending in ("big_wall", "big_goodbye", "big_game", "big_bell"):
            with self.assertRaises(OptionError):
                build_like_universal_tracker({"objectives": [ending, "big_collection"]})


class TestTheBellIsAnEnding(unittest.TestCase):
    """big_wall ends a short seed (the player, 2026-10-12)."""

    def test_the_bell_as_the_goal_generates(self) -> None:
        build_like_universal_tracker({"goal": "big_wall", "objectives": ["big_collection"]})


class TestTheBellEndsBeforeTheGauntlet(unittest.TestCase):
    def test_the_bell_with_locked_stages_is_refused(self) -> None:
        with self.assertRaises(OptionError):
            build_like_universal_tracker({"goal": "big_wall", "gauntlet_mode": "locked_stages"})


class TestTheGourdsAsTheGoalWithObjectives(BigWalkTestBase):
    """goal big_collection with objectives: won once the gourds AND the rest are done, in any order."""

    options = {"goal": "big_collection", "objectives": ["big_climb", "big_collection"], "gourds_required": 20}
    run_default_tests = False

    def test_the_list_holds_the_gourds_once(self) -> None:
        self.assertEqual(["big_collection", "big_climb"], self.world.fill_slot_data()["objectives"])

    def test_the_gourds_are_needed(self) -> None:
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_gourds(20)
        self.assertTrue(self.can_reach_location("Victory"))


class TestRandomObjectives(BigWalkTestBase):
    """random_objectives draws among those listed."""

    options = {"goal": "big_game", "objectives": ["big_collection", "big_keys", "big_climb", "big_fiesta"],
               "random_objectives": 2}
    run_default_tests = False

    def test_two_are_drawn_among_the_listed(self) -> None:
        self.assertEqual(2, len(self.world.objectives))
        self.assertTrue(self.world.objectives <= {"big_collection", "big_keys", "big_climb", "big_fiesta"})
        self.assertIn("big_game", self.world.fill_slot_data()["objectives"])


class TestRandomObjectivesFromAll(BigWalkTestBase):
    """With none listed, they are drawn from all those the other options allow."""

    options = {"goal": "big_wall", "random_objectives": 6, "radio_sanity": False, "tower_mode": "all_open"}
    run_default_tests = False

    def test_what_cannot_be_asked_is_not_drawn(self) -> None:
        self.assertEqual({"big_collection", "big_keys", "big_climb", "big_fiesta"}, set(self.world.objectives))


class TestTheSlotDataOfAList(BigWalkTestBase):
    options = {"goal": "big_goodbye", "objectives": ["big_collection"], "lock_endings_until_objectives": True}
    run_default_tests = False

    def test_the_list_is_in_the_frozen_order(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(["big_goodbye", "big_collection"], slot_data["objectives"])
        self.assertTrue(slot_data["lock_endings"])

    def test_an_older_mod_reads_the_first_ending(self) -> None:
        self.assertEqual("gauntlet", self.world.fill_slot_data()["goal"])


class TestAnOlderGoalKeepsItsWay(BigWalkTestBase):
    """No objectives listed: the seed is what it was, the older `goal` and nothing more."""

    options = {"goal": "big_wall", "lock_endings_until_objectives": True}
    run_default_tests = False

    def test_no_list_and_the_goal_as_before(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual([], slot_data["objectives"])
        self.assertEqual("ending", slot_data["goal"])

    def test_the_locking_means_nothing_without_a_list(self) -> None:
        self.assertFalse(self.world.fill_slot_data()["lock_endings"])


class TestTheWalkAndTheKeys(BigWalkTestBase):
    """big_keys (a number of keys placed, the drawbridge's among them), beside an ending."""

    options = {"goal": "big_game", "objectives": ["big_keys"], "keys_required": 2}
    run_default_tests = False

    def test_victory_needs_the_ending_among_the_rest(self) -> None:
        self.collect_all_but(data.GREEN_DOME.item_name)
        self.assertFalse(self.can_reach_location("Victory"))

        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertTrue(self.can_reach_location("Victory"))

    def test_the_deposits_are_needed_too(self) -> None:
        # The ending's door alone is not enough: the keys are places to reach as well.
        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertFalse(self.can_reach_location("Victory"))


class TestTheRadioStations(BigWalkTestBase):
    options = {"goal": "big_game", "objectives": ["big_radio"], "radio_stations_required": 3, "radio_sanity": True}
    run_default_tests = False

    def test_the_slot_data_carries_the_count(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(3, slot_data["radio_stations_required"])
        self.assertEqual(["big_game", "big_radio"], slot_data["objectives"])


class TestTheClimb(BigWalkTestBase):
    options = {"goal": "big_game", "objectives": ["big_climb"]}
    run_default_tests = False

    def test_the_climb_asks_for_nothing_but_the_island(self) -> None:
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertTrue(self.can_reach_location("Victory"))


class TestTheOldNameOfTheFiesta(BigWalkTestBase):
    """big_club was the first name of big_fiesta: a YAML that still says so is read as the new one."""

    options = {"goal": "big_game", "objectives": ["big_club"], "shuffle_radio_music": True, "everyone_gathers": True}
    run_default_tests = False

    def test_it_is_the_fiesta(self) -> None:
        self.assertEqual(["big_game", "big_fiesta"], self.world.fill_slot_data()["objectives"])

    def test_the_slot_data_carries_everyone_gathers(self) -> None:
        self.assertTrue(self.world.fill_slot_data()["everyone_gathers"])


class TestTheClub(BigWalkTestBase):
    """The party: a radio, and with the stations as items, any one of them."""

    options = {"goal": "big_game", "objectives": ["big_fiesta"], "shuffle_radio_music": True}
    run_default_tests = False

    def test_they_are_listed_in_the_frozen_order(self) -> None:
        self.assertEqual(["big_game", "big_fiesta"], self.world.fill_slot_data()["objectives"])

    def test_a_radio_is_certain_in_the_pool(self) -> None:
        self.assertGreaterEqual(len(self.get_items_by_name("Radio")), 1)

    def test_the_radio_and_the_stations_are_progression(self) -> None:
        for item in self.get_items_by_name("Radio"):
            self.assertTrue(item.advancement)
        for item in self.multiworld.itempool:
            if item.player == self.player and item.name in items.RADIO_ITEM_NAMES:
                self.assertTrue(item.advancement)

    def test_victory_needs_the_ending_a_radio_and_a_station(self) -> None:
        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Radio")
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect(self.get_items_by_name(items.RADIO_ITEM_NAMES[0])[:1])
        self.assertTrue(self.can_reach_location("Victory"))


class TestTheClubWithoutShuffledMusic(BigWalkTestBase):
    options = {"goal": "big_game", "objectives": ["big_fiesta"], "shuffle_radio_music": False}
    run_default_tests = False

    def test_only_the_radio_is_needed(self) -> None:
        self.collect_by_name(data.GREEN_DOME.item_name)
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Radio")
        self.assertTrue(self.can_reach_location("Victory"))


class TestObjectivesTakeTheGoalAsTheirEnding(unittest.TestCase):
    def test_a_list_without_a_goal_takes_the_default_ending(self) -> None:
        build_like_universal_tracker({"objectives": ["big_collection", "big_keys"]})

    def test_the_radio_objective_needs_radio_sanity(self) -> None:
        with self.assertRaises(OptionError):
            build_like_universal_tracker({"goal": "big_game", "objectives": ["big_radio"], "radio_sanity": False})
