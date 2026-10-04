"""The waits of the two Cabin Fever puzzles: four modes, resolved once, and a help button."""

from .bases import BigWalkTestBase


def wait(slot_data: dict, puzzle: str) -> dict:
    return {field: slot_data[f"{puzzle}_{field}"] for field in ("mode", "seconds", "help")}


class TestVanilla(BigWalkTestBase):
    run_default_tests = False

    def test_nothing_changes_by_default(self) -> None:
        slot_data = self.world.fill_slot_data()
        for key in ("cabin_fever", "cabin_fever_long"):
            self.assertEqual(wait(slot_data, key), {"mode": "vanilla", "seconds": 0, "help": False})


class TestReduced(BigWalkTestBase):
    options = {"cabin_fever_time": "reduced", "cabin_fever_seconds_min": 45, "cabin_fever_long_time": "reduced"}
    run_default_tests = False

    def test_reduced_goes_down_to_the_shortest_wait(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["cabin_fever_seconds"], 45)
        self.assertEqual(slot_data["cabin_fever_mode"], "reduced")
        self.assertEqual(slot_data["cabin_fever_long_seconds"], 300)


class TestRandom(BigWalkTestBase):
    options = {
        "cabin_fever_time": "random_between",
        "cabin_fever_seconds_min": 100,
        "cabin_fever_seconds_max": 200,
        "cabin_fever_long_time": "random_between",
        "cabin_fever_long_seconds_min": 900,
        "cabin_fever_long_seconds_max": 400,
    }
    run_default_tests = False

    def test_random_stays_between_the_bounds(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertTrue(100 <= slot_data["cabin_fever_seconds"] <= 200)
        # The bounds given the wrong way round still make a range.
        self.assertTrue(400 <= slot_data["cabin_fever_long_seconds"] <= 900)

    def test_asking_twice_gives_the_same_answer(self) -> None:
        first = self.world.fill_slot_data()["cabin_fever_seconds"]
        self.assertEqual(self.world.fill_slot_data()["cabin_fever_seconds"], first)


class TestFixedAndHelp(BigWalkTestBase):
    options = {
        "cabin_fever_time": "fixed",
        "cabin_fever_seconds": 3000,
        "cabin_fever_help": True,
        "cabin_fever_long_help": True,
    }
    run_default_tests = False

    def test_fixed_may_be_longer_than_the_game_and_help_is_per_puzzle(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(wait(slot_data, "cabin_fever"), {"mode": "fixed", "seconds": 3000, "help": True})
        self.assertEqual(wait(slot_data, "cabin_fever_long"), {"mode": "vanilla", "seconds": 0, "help": True})


class TestOpenBlackTower(BigWalkTestBase):
    run_default_tests = False

    def test_open_by_default(self) -> None:
        self.assertTrue(self.world.fill_slot_data()["open_black_tower"])


class TestBlackTowerAsInTheGame(BigWalkTestBase):
    options = {"open_black_tower": False}
    run_default_tests = False

    def test_off_travels(self) -> None:
        self.assertFalse(self.world.fill_slot_data()["open_black_tower"])


class TestResyncStationsByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_towers_off_guests_off(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertFalse(slot_data["tower_resync_stations"])
        self.assertFalse(slot_data["guests_can_resync"])


class TestResyncStationsChanged(BigWalkTestBase):
    options = {"tower_resync_stations": True, "guests_can_resync": True}
    run_default_tests = False

    def test_both_travel(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertTrue(slot_data["tower_resync_stations"])
        self.assertTrue(slot_data["guests_can_resync"])


class TestTileThief(BigWalkTestBase):
    options = {"tile_thief": "chaos"}
    run_default_tests = False

    def test_the_mode_travels(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["tile_thief"], "chaos")


class TestTileThiefByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_vanilla(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["tile_thief"], "vanilla")


class TestShufflePegTiles(BigWalkTestBase):
    options = {"shuffle_peg_tiles": True}
    run_default_tests = False

    def test_it_travels(self) -> None:
        self.assertTrue(self.world.fill_slot_data()["shuffle_peg_tiles"])
