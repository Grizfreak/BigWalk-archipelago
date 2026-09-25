"""
The radio is the one thing in this world that used to reward itself: switching
a station on both reported the check and started the music. With
`shuffle_radio_music` the music becomes an item like everything else, which is
a contract with the mod as much as a pool change — see ../../protocol.md.
"""

from .. import data, items
from .bases import BigWalkTestBase


def item_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestRadioItemsOn(BigWalkTestBase):
    """The default."""

    options: dict = {}

    def test_one_music_item_per_station(self) -> None:
        pool = item_names(self)
        for station in data.RADIO_STATIONS:
            self.assertEqual(pool.count(station.item_name), 1, station.item_name)

    def test_a_music_item_gates_nothing(self) -> None:
        # Nothing in this world or any other may ever require one: the music
        # is the whole effect. Classified filler for exactly that reason.
        for station in data.RADIO_STATIONS:
            self.assertTrue(self.world.create_item(station.item_name).filler)

    def test_ids_are_the_station_ids(self) -> None:
        # Item and location ids are separate namespaces, so a station's two
        # ids are deliberately the same number — the mod derives both from the
        # same SavableSystem value with one rule.
        for station in data.RADIO_STATIONS:
            self.assertEqual(items.ITEM_NAME_TO_ID[station.item_name],
                             data.BASE_ID + data.RADIO_ID_OFFSET + station.system_value)

    def test_slot_data_tells_the_mod_to_suppress(self) -> None:
        self.assertIs(self.world.fill_slot_data()["radio_station_items"], True)


class TestRadioItemsOff(BigWalkTestBase):
    """Vanilla radio: switching a station on still unlocks its own music."""

    options = {"shuffle_radio_music": False}
    run_default_tests = False

    def test_no_music_item_in_the_pool(self) -> None:
        pool = set(item_names(self))
        self.assertFalse(pool & self.world.item_name_groups["Radio Music"])

    def test_slot_data_leaves_the_radio_alone(self) -> None:
        self.assertIs(self.world.fill_slot_data()["radio_station_items"], False)


class TestRadioItemsWithoutChecks(BigWalkTestBase):
    """
    The two options are independent on purpose: a player may want the music
    shuffled without seven extra checks, or the checks without losing their
    radio. Seven fewer locations and seven more items still has to balance.
    """

    options = {"radio_checks": False, "shuffle_radio_music": True}
    run_default_tests = False

    def test_pool_still_fits_the_locations(self) -> None:
        pool = item_names(self)
        unfilled = [location for location in self.multiworld.get_locations(self.player)
                    if location.address is not None]
        self.assertEqual(len(pool), len(unfilled))
        self.assertTrue(set(pool) & self.world.item_name_groups["Radio Music"])
