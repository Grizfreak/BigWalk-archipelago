"""
Universal Tracker support.

UT re-runs this world's generation inside the tracker and compares the result
against the server. What these tests guard is that it re-runs it with the
SEED's options and not with whatever YAML the tracking player happens to have
— the whole of that mechanism is `interpret_slot_data` plus the passthrough
block in `generate_early`, and both are silent when they break.
"""

from __future__ import annotations

import inspect

from .. import world as bigwalk_world
from ..world import BigWalkWorld
from .bases import BigWalkTestBase, build_like_universal_tracker, world_shape

# A seed that differs from the defaults in every way that can move a location,
# a region or a number a rule is written against.
UNLIKE_THE_DEFAULTS = {
    "goal": "second_ending",
    "green_dome_deposits": "key_only",
    "deposit_locations": "all",
    "deposit_goal_amount": 25,
    "radio_station_checks": False,
    "radio_station_items": False,
}


class TestTheTrackerRebuildsTheSeed(BigWalkTestBase):
    options = UNLIKE_THE_DEFAULTS

    def test_it_lands_on_this_exact_world(self) -> None:
        """
        The tracker generates with an empty YAML — every option at its default
        — and then again with the seed's slot_data. The second pass must land
        on the world that was really generated.
        """
        tracked = build_like_universal_tracker({}, self.world.fill_slot_data())
        self.assertEqual(world_shape(tracked), world_shape(self.multiworld))

    def test_and_without_the_passthrough_it_would_not(self) -> None:
        """
        The control. Without slot_data the tracker keeps the default YAML and
        shows a different world, which is what the test above is worth.
        """
        untracked = build_like_universal_tracker({})
        self.assertNotEqual(world_shape(untracked), world_shape(self.multiworld))


class TestARepairedOptionSurvives(BigWalkTestBase):
    """
    `second_ending` + `excluded` is generated as `key_only` (world.py). What
    travels in slot_data is the repair, not the request, so the tracker must
    rebuild the world that exists — Hub Secret Door and all.
    """

    options = {"goal": "second_ending", "green_dome_deposits": "excluded"}

    def test_the_tracker_sees_the_repaired_world(self) -> None:
        tracked = build_like_universal_tracker({}, self.world.fill_slot_data())
        self.assertEqual(world_shape(tracked), world_shape(self.multiworld))
        self.assertIn("Past the Hub Secret Door", world_shape(tracked)["regions"])


class TestSlotDataCarriesEveryTrackedOption(BigWalkTestBase):
    options = UNLIKE_THE_DEFAULTS

    def test_each_one_is_an_option_and_travels(self) -> None:
        slot_data = self.world.fill_slot_data()
        for name in bigwalk_world.TRACKER_OPTIONS:
            with self.subTest(option=name):
                self.assertIn(name, BigWalkWorld.options_dataclass.type_hints)
                self.assertIn(name, slot_data)

    def test_each_one_reads_back_into_the_value_it_was_generated_with(self) -> None:
        slot_data = self.world.fill_slot_data()
        for name in bigwalk_world.TRACKER_OPTIONS:
            with self.subTest(option=name):
                option = getattr(self.world.options, name)
                self.assertEqual(option.from_any(slot_data[name]).value, option.value)


class TestTheHooksAreShapedTheWayUTChecksThem(BigWalkTestBase):
    def test_interpret_slot_data_is_static(self) -> None:
        """
        UT reads it with `inspect.getattr_static` and only skips its first
        generation when it finds a staticmethod or a classmethod there
        (`TrackerCore.initalize_tracker_core`). A plain method would quietly
        cost the yaml-less path.
        """
        hook = inspect.getattr_static(BigWalkWorld, "interpret_slot_data", None)
        self.assertIsInstance(hook, staticmethod)

    def test_it_returns_something_so_that_ut_regenerates(self) -> None:
        """Returning None would leave the tracker on its first, YAML-shaped pass."""
        slot_data = self.world.fill_slot_data()
        self.assertIs(BigWalkWorld.interpret_slot_data(slot_data), slot_data)

    def test_ut_is_not_disabled_and_needs_no_yaml(self) -> None:
        self.assertTrue(BigWalkWorld.ut_can_gen_without_yaml)
        self.assertFalse(getattr(BigWalkWorld, "disable_ut", False))
