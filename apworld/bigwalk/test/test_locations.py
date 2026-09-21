from .. import data
from .bases import BigWalkTestBase


def location_names(test: BigWalkTestBase) -> set[str]:
    return {location.name for location in test.multiworld.get_locations(test.player)}


class TestDepositMilestones(BigWalkTestBase):
    """The default: a check every fifth deposit, plus the last one."""

    options = {"deposit_locations": "milestones"}
    run_default_tests = False

    def test_milestones_only(self) -> None:
        self.assertEqual(self.world.deposit_amounts, (5, 10, 15, 20, 25, 30, 35, 40, 45))

    def test_a_milestone_needs_exactly_that_many_gourds(self) -> None:
        self.collect_gourds(9)
        self.assertFalse(self.can_reach_location("Gourd Deposit 10"))
        self.collect_gourds(1)
        self.assertTrue(self.can_reach_location("Gourd Deposit 10"))


class TestAllDeposits(BigWalkTestBase):
    options = {"deposit_locations": "all"}
    run_default_tests = False

    def test_one_check_per_deposit(self) -> None:
        self.assertEqual(self.world.deposit_amounts, tuple(range(1, 46)))


class TestNoDeposits(BigWalkTestBase):
    options = {"deposit_locations": "none"}
    run_default_tests = False

    def test_no_deposit_locations_exist(self) -> None:
        self.assertEqual(self.world.deposit_amounts, ())
        self.assertFalse(location_names(self) & self.world.location_name_groups["Gourd Deposits"])


class TestRadioStationsOff(BigWalkTestBase):
    options = {"radio_station_checks": False}
    run_default_tests = False

    def test_no_radio_locations_exist(self) -> None:
        self.assertFalse(location_names(self) & self.world.location_name_groups["Radio Stations"])


class TestRadioStationsOn(BigWalkTestBase):
    options = {"radio_station_checks": True}
    run_default_tests = False

    def test_all_seven_exist_and_need_nothing(self) -> None:
        present = location_names(self) & self.world.location_name_groups["Radio Stations"]
        self.assertEqual(len(present), len(data.RADIO_STATIONS))

        # "Need nothing" means no item switches a station on — but two of them
        # sit past a big key, which is geography rather than logic. Those are
        # covered by test_regions.py.
        gated = set(data.chairlift_locations()) | set(data.tunnel_locations())
        for station in data.RADIO_STATIONS:
            if station.location_name in gated:
                continue
            self.assertTrue(self.can_reach_location(station.location_name), station.location_name)
