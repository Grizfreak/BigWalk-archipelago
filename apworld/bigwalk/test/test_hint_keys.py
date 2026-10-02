"""`hint_keys: from_start` puts the seven big keys in the player's start hints."""

from .. import data
from .bases import BigWalkTestBase

KEY_NAMES = {data.key_item_name(tower) for tower in data.TOWERS}


class TestHintKeysOff(BigWalkTestBase):
    """The default: nothing is hinted."""

    options: dict = {}

    def test_no_start_hint(self) -> None:
        self.assertFalse(self.world.options.start_hints.value & KEY_NAMES)


class TestHintKeysFromStart(BigWalkTestBase):
    options = {"hint_keys": "from_start", "start_hints": ["Gourd"]}

    def test_the_seven_keys_are_hinted(self) -> None:
        self.assertEqual(len(KEY_NAMES), 7)
        self.assertTrue(KEY_NAMES <= set(self.world.options.start_hints.value))

    def test_the_players_own_hints_stay(self) -> None:
        self.assertIn("Gourd", self.world.options.start_hints.value)

    def test_nothing_reaches_slot_data(self) -> None:
        self.assertNotIn("hint_keys", self.world.fill_slot_data())
