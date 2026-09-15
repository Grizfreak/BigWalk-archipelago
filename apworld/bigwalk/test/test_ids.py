"""
Id tables are a contract with the mod, not an implementation detail: the C#
client computes ids arithmetically from the game's own enum values (see
../../protocol.md). These tests are what stops that contract drifting.
"""

import unittest

from .. import data, items, locations


class TestIdTables(unittest.TestCase):
    def test_no_duplicate_location_ids(self) -> None:
        ids = list(locations.LOCATION_NAME_TO_ID.values())
        self.assertEqual(len(ids), len(set(ids)))

    def test_no_duplicate_item_ids(self) -> None:
        ids = list(items.ITEM_NAME_TO_ID.values())
        self.assertEqual(len(ids), len(set(ids)))

    def test_location_ids_derive_from_game_enums(self) -> None:
        for puzzle in data.PUZZLES:
            self.assertEqual(
                locations.LOCATION_NAME_TO_ID[puzzle.location_name],
                data.BASE_ID + puzzle.prop_value,
            )
        for tower in data.TOWERS:
            self.assertEqual(
                locations.LOCATION_NAME_TO_ID[tower.location_name],
                data.BASE_ID + tower.prop_value,
            )
        for station in data.RADIO_STATIONS:
            self.assertEqual(
                locations.LOCATION_NAME_TO_ID[station.location_name],
                data.BASE_ID + data.RADIO_ID_OFFSET + station.system_value,
            )

    def test_item_ids_derive_from_game_enums(self) -> None:
        for tower in data.TOWERS:
            self.assertEqual(items.ITEM_NAME_TO_ID[tower.item_name],
                             data.BASE_ID + tower.prop_value)

    def test_puzzle_count_matches_the_game(self) -> None:
        # 58 puzzles: SaveablePropName 100-159, minus the two values the enum
        # skips (102 and 136). If this ever fails, the game shipped a content
        # update and the table above needs a fresh dump.
        self.assertEqual(len(data.PUZZLES), 58)
        self.assertEqual(len({puzzle.prop_name for puzzle in data.PUZZLES}), 58)
        self.assertEqual(len({puzzle.location_name for puzzle in data.PUZZLES}), 58)

    def test_monument_slot_counts(self) -> None:
        # Counted in-game with the mod's F6 tool and independently confirmed by
        # a third-party document; both agree on 4 + 5*4 + 6 + 15.
        self.assertEqual(data.BASE_MONUMENT_SLOTS, 30)
        self.assertEqual(data.MAX_MONUMENT_SLOTS, 45)

    def test_every_deposit_amount_has_a_location(self) -> None:
        for amount in range(1, data.MAX_MONUMENT_SLOTS + 1):
            self.assertIn(data.deposit_location_name(amount), locations.LOCATION_NAME_TO_ID)
