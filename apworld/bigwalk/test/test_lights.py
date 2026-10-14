"""
`light_sanity` and the `big_lights` objective (player, 2026-10-13, from a player's idea): a check the first time each
light is switched on, and a share of them as an objective.
"""

from BaseClasses import LocationProgressType
from Options import OptionError

from .. import data, locations
from .bases import BigWalkTestBase


def light_locations(test: BigWalkTestBase) -> set[str]:
    names = locations.LOCATION_NAME_GROUPS["Lights"]
    return {loc.name for loc in test.multiworld.get_locations(test.player) if loc.name in names}


class TestLightsOffByDefault(BigWalkTestBase):
    def test_no_light(self) -> None:
        self.assertEqual(light_locations(self), set())


class TestLightsLamps(BigWalkTestBase):
    options = {"light_sanity": "lamps", "lock_puzzle_needs": False}

    def test_every_lamp_and_no_switch(self) -> None:
        lamps = {light.location_name for light in data.LIGHTS if not light.switch}
        self.assertEqual(light_locations(self), lamps)
        self.assertEqual(len(lamps), 83)


class TestLightsWithSwitches(BigWalkTestBase):
    options = {"light_sanity": "lamps_and_switches", "lock_puzzle_needs": False}

    def test_switches_too(self) -> None:
        self.assertEqual(len(light_locations(self)), 83 + len(data.LIGHT_SWITCHES))

    def test_train_headlights_need_the_train(self) -> None:
        self.collect_all_but(["Train"])
        self.assertFalse(self.can_reach_location("Light Switch (Train, Front Car) On"))
        self.assertTrue(self.can_reach_location("Light Switch (Upper Hub Train Stop) On"))


class TestBigLights(BigWalkTestBase):
    options = {"light_sanity": "lamps", "objectives": ["big_lights"], "lights_required": 25, "lock_puzzle_needs": False}

    def test_target_and_slot_data(self) -> None:
        self.assertEqual(self.world.lights_target(), 21)  # 25 % of 83, rounded up
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["lights_required_count"], 21)
        self.assertIn("big_lights", slot["objectives"])


class TestBigLightsWithoutLightSanity(BigWalkTestBase):
    """Without light_sanity, the lamps count for big_lights only: no check, the events alone (player, 2026-10-13)."""

    options = {"objectives": ["big_lights"], "lights_required": 50, "lock_puzzle_needs": False}

    def test_no_check_but_a_target(self) -> None:
        self.assertEqual(light_locations(self), set())
        self.assertEqual(self.world.lights_target(), 42)  # 50 % of 83, rounded up
        slot = self.world.fill_slot_data()
        self.assertEqual(len(slot["lights_counted"]), 83)
        self.assertEqual(slot["light_sanity"], "off")


class TestLightItemsJunkAndLocal(BigWalkTestBase):
    options = {"light_sanity": "lamps", "light_sanity_items": "junk", "object_sanity": "every",
               "object_sanity_items": "local"}


class TestJunkPercentage(BigWalkTestBase):
    """`*_sanity_items: any` keeps `*_junk_percentage` of the checks for junk (player, 2026-10-14)."""

    options = {"light_sanity": "lamps_and_switches", "light_sanity_junk_percentage": 75, "object_sanity": "every",
               "object_sanity_junk_percentage": 40}

    def test_share_excluded(self) -> None:
        for group, percentage in (("Lights", 75), ("Objects", 40)):
            spots = [loc for loc in self.multiworld.get_locations(self.player)
                     if loc.name in locations.LOCATION_NAME_GROUPS[group]]
            excluded = [loc for loc in spots if loc.progress_type == LocationProgressType.EXCLUDED]
            self.assertEqual(len(excluded), round(len(spots) * percentage / 100), group)


class TestNoJunkPercentage(BigWalkTestBase):
    options = {"light_sanity": "lamps", "light_sanity_junk_percentage": 0}
    run_default_tests = False

    def test_nothing_excluded(self) -> None:
        self.assertFalse(any(loc.progress_type == LocationProgressType.EXCLUDED
                             for loc in self.multiworld.get_locations(self.player)
                             if loc.name in locations.LOCATION_NAME_GROUPS["Lights"]))


class TestLightData(BigWalkTestBase):
    run_default_tests = False

    def test_ids_and_keys(self) -> None:
        self.assertEqual(len({light.key for light in data.LIGHTS}), len(data.LIGHTS))
        self.assertLess(data.LIGHT_ID_OFFSET + len(data.LIGHTS), 14_200)


class TestLampsAreLightsNotObjects(BigWalkTestBase):
    """With light_sanity, picking a lamp up is no check: switching it on is (player, 2026-10-13)."""

    options = {"light_sanity": "lamps", "object_sanity": "every", "lock_puzzle_needs": False}

    def test_no_lamp_object(self) -> None:
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        for obj in data.ISLAND_OBJECTS:
            if obj.kind in data.LAMP_KINDS:
                self.assertNotIn(obj.location_name, names)
        self.assertIn("Clock (Upper Hub Train Stop)", names)
        self.assertNotIn("BuoyLight", self.world.fill_slot_data()["object_sanity_kinds"])
