"""
`zone_locks` (ROADMAP R12, player 2026-10-14): the island's lamps and gadgets by the zone of the nearest coloured
tower, each zone's lamps and gadgets an item.
"""

from .. import data
from .bases import BigWalkTestBase

ZONE_ITEMS = {lock.item_name for lock in data.ZONE_LOCKS}


def pool_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


class TestZoneLocksOffByDefault(BigWalkTestBase):
    options = {"object_sanity": "every", "light_sanity": "lamps_and_switches"}

    def test_no_zone_item(self) -> None:
        self.assertFalse(ZONE_ITEMS & set(pool_names(self)))
        self.assertEqual(self.world.fill_slot_data()["zone_locked_gadgets"], {})


class TestZoneLocksBoth(BigWalkTestBase):
    options = {"object_sanity": "every", "light_sanity": "lamps_and_switches", "zone_locks": True}

    def test_eight_items(self) -> None:
        self.assertEqual(sorted(n for n in pool_names(self) if n in ZONE_ITEMS), sorted(ZONE_ITEMS))

    def test_a_gadget_needs_its_zone(self) -> None:
        name = "Radio (Peak Marker)"
        obj = next(o for o in data.ISLAND_OBJECTS if o.location_name == name)
        lock = data.zone_lock_for(obj.guid, obj.zone, False)
        self.assertIsNotNone(lock)
        self.assertFalse(self.can_reach_location(name))
        self.collect_by_name(lock.item_name)
        self.assertTrue(self.can_reach_location(name))

    def test_a_switch_needs_its_zone(self) -> None:
        name = "Light Switch (Ring Room Train Stop) On"
        light = next(light for light in data.LIGHTS if light.location_name == name)
        lock = data.zone_lock_for(light.key, light.zone, True)
        self.assertIsNotNone(lock)
        self.assertFalse(self.can_reach_location(name))

    def test_the_tutorial_stays_open(self) -> None:
        self.assertTrue(self.can_reach_location("Lamp (Tutorial 1) Lit"))

    def test_slot_data_gadgets(self) -> None:
        locked = self.world.fill_slot_data()["zone_locked_gadgets"]
        self.assertEqual(len(locked), 4)
        guids = [guid for guids in locked.values() for guid in guids]
        self.assertEqual(len(guids), len(set(guids)))
        self.assertTrue(set(guids) <= {obj.guid for obj in data.ISLAND_OBJECTS})


class TestZoneLocksLightsOnly(BigWalkTestBase):
    options = {"light_sanity": "lamps", "zone_locks": True, "start_with_zone_locks": ["red_lamps"]}

    def test_lamps_only_red_given(self) -> None:
        names = set(pool_names(self))
        self.assertEqual({n for n in names if n in ZONE_ITEMS}, {"Green Zone Lamps", "Blue Zone Lamps",
                                                                 "Yellow Zone Lamps"})
        self.assertIn("Red Zone Lamps", {item.name for item in self.multiworld.precollected_items[self.player]})


class TestZoneLocksLocal(BigWalkTestBase):
    options = {"object_sanity": "every", "zone_locks": True, "zone_locks_local": True}

    def test_local(self) -> None:
        self.assertTrue({"Red Zone Gadgets", "Yellow Zone Gadgets"} <= self.world.options.local_items.value)


class TestZoneLocksNoSanity(BigWalkTestBase):
    options = {"zone_locks": True}
    run_default_tests = False

    def test_nothing(self) -> None:
        self.assertFalse(ZONE_ITEMS & set(pool_names(self)))
