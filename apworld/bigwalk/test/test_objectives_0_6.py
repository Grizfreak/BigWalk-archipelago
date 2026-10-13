"""
The 0.6 objectives (player, 2026-10-13): big_objects and big_body, each at a share the player chooses.
"""

from Options import OptionError

from .. import data, locations
from .bases import BigWalkTestBase


class TestBigObjects(BigWalkTestBase):
    options = {"object_sanity": "first_pickup", "objectives": ["big_objects"], "objects_required": 50,
               "lock_puzzle_needs": False}

    def test_target(self) -> None:
        kinds = len(self.world.object_location_names())
        self.assertEqual(self.world.objects_target(), -(-kinds // 2))
        self.assertEqual(self.world.fill_slot_data()["objects_required_count"], self.world.objects_target())


class TestBigBody(BigWalkTestBase):
    options = {"lock_abilities": "handed", "objectives": ["big_body"], "body_required": 50}

    def test_target_and_rule(self) -> None:
        self.assertEqual(len(self.world.ability_item_names()), 10)
        self.assertEqual(self.world.body_target(), 5)
        victory = locations.VICTORY_EVENT_NAME
        self.collect_all_but(["Legs", "Seat", "Progressive Left Arm", "Progressive Right Arm", "Throwing", "Carrying"])
        self.assertFalse(self.can_reach_location(victory))


class TestBigBodyFull(BigWalkTestBase):
    options = {"lock_abilities": "ambidextrous", "lock_running": True, "objectives": ["big_body", "big_objects"],
               "object_sanity": "every", "objects_required": 100}


class TestNewObjectivesNeedTheirOption(BigWalkTestBase):
    run_default_tests = False

    def test_refused(self) -> None:
        for objective in ("big_objects", "big_body"):  # big_lights works without light_sanity
            self.options = {"objectives": [objective]}
            with self.assertRaises(OptionError, msg=objective):
                self.world_setup()
