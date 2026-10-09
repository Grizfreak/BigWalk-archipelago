"""The waits of the two Cabin Fever puzzles: one number each, the game's own by default, and a help button."""

from .bases import BigWalkTestBase


def wait(slot_data: dict, puzzle: str) -> dict:
    return {field: slot_data[f"{puzzle}_{field}"] for field in ("mode", "seconds", "help")}


class TestVanilla(BigWalkTestBase):
    run_default_tests = False

    def test_nothing_changes_by_default(self) -> None:
        slot_data = self.world.fill_slot_data()
        for key in ("cabin_fever", "cabin_fever_long"):
            self.assertEqual(wait(slot_data, key), {"mode": "vanilla", "seconds": 0, "help": False})


class TestShorter(BigWalkTestBase):
    options = {"cabin_fever_seconds": 45, "cabin_fever_long_seconds": 300}
    run_default_tests = False

    def test_another_number_is_fixed(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(wait(slot_data, "cabin_fever"), {"mode": "fixed", "seconds": 45, "help": False})
        self.assertEqual(wait(slot_data, "cabin_fever_long"), {"mode": "fixed", "seconds": 300, "help": False})


class TestLongerAndHelp(BigWalkTestBase):
    options = {
        "cabin_fever_seconds": 3000,
        "cabin_fever_help": True,
        "cabin_fever_long_help": True,
    }
    run_default_tests = False

    def test_longer_than_the_game_and_help_is_per_puzzle(self) -> None:
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

