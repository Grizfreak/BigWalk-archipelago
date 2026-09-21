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

    def test_cut_ids_match_the_formula_the_mod_uses(self) -> None:
        """
        A cut segment has no identifier of the game's own — `cuts` is an
        anonymous Mirror SyncList of bools — so both sides build one from the
        key and the index. The C# is

            _base + _cutOffset + (long)propName * CutStride + index

        in `Core/Net/ApLocationIds.TryResolveCut`. It is spelled out again
        here rather than calling `data.cut_location_id`, because a test that
        calls the thing it is testing cannot catch the formula changing.
        """
        for tower in data.TOWERS:
            for index in range(tower.segments):
                self.assertEqual(
                    locations.LOCATION_NAME_TO_ID[data.cut_location_name(tower, index)],
                    data.BASE_ID + data.CUT_ID_OFFSET + tower.prop_value * data.CUT_STRIDE + index,
                )

    def test_cut_ids_cannot_reach_any_other_range(self) -> None:
        # The stride is what keeps one key's segments out of the next key's
        # block, and the offset is what keeps the whole lot away from the
        # puzzles, deposits and radio. Asserted against the real extremes
        # rather than against remembered numbers.
        cut_ids = [data.cut_location_id(tower, index)
                   for tower in data.TOWERS for index in range(tower.segments)]
        self.assertEqual(len(cut_ids), data.TOTAL_CUT_SEGMENTS)

        others = [location_id for name, location_id in locations.LOCATION_NAME_TO_ID.items()
                  if location_id not in set(cut_ids)]
        self.assertTrue(min(cut_ids) > max(others),
                        "the cut block overlaps something else in the id space")

        for tower in data.TOWERS:
            self.assertLessEqual(tower.segments, data.CUT_STRIDE,
                                 f"{tower.prop_name} has more segments than the stride allows")

    def test_item_ids_derive_from_game_enums(self) -> None:
        # Still keyed on the big key's enum value even though the item is now
        # the feature rather than the key: the mod resolves it with
        # `ApLocationIds.TryResolveBigKeyItem`, which is plain `id - base`.
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

    def test_cut_segment_counts_match_the_game(self) -> None:
        # Measured with the mod's Ctrl+K dump on 2026-09-21: nine KeyBlank
        # instances, five segments each on the drawbridge and the four
        # coloured towers, and none on the two keys that are born finished.
        self.assertEqual(data.TOTAL_CUT_SEGMENTS, 25)
        with_segments = [tower for tower in data.TOWERS if tower.segments]
        self.assertEqual(len(with_segments), 5)
        for tower in with_segments:
            self.assertEqual(tower.segments, 5, tower.prop_name)

    def test_monument_slot_counts(self) -> None:
        # Counted in-game with the mod's F6 tool and independently confirmed by
        # a third-party document; both agree on 4 + 5*4 + 6 + 15.
        self.assertEqual(data.BASE_MONUMENT_SLOTS, 30)
        self.assertEqual(data.MAX_MONUMENT_SLOTS, 45)

    def test_every_deposit_amount_has_a_location(self) -> None:
        for amount in range(1, data.MAX_MONUMENT_SLOTS + 1):
            self.assertIn(data.deposit_location_name(amount), locations.LOCATION_NAME_TO_ID)
