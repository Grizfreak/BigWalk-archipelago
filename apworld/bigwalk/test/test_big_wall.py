"""The two lamps behind the Big Wall (Morgan, 2026-11-08): out of logic until the Big Wall Door."""

from BaseClasses import CollectionState

from .. import data, locations, regions
from .bases import BigWalkTestBase

LAMPS = ("Lamp (Big Wall)", "Pedestal Lamp (Big Wall)")
LIGHTS = tuple(f"{name} Lit" for name in LAMPS)


class TestBigWallIds(BigWalkTestBase):
    run_default_tests = False

    def test_renaming_kept_the_ids(self) -> None:
        # Named "near Centurion Song" until 1.0.8; the mod reports them by these ids.
        ids = locations.LOCATION_NAME_TO_ID
        self.assertEqual([ids[name] for name in LAMPS], [8_613_577, 8_613_616])
        self.assertEqual([ids[name] for name in LIGHTS], [8_614_014, 8_614_053])

    def test_no_zone_colour(self) -> None:
        for obj in data.ISLAND_OBJECTS:
            if obj.zone == "big_wall":
                self.assertNotIn(obj.guid, data.OVERWORLD_ZONES)
                self.assertIsNone(data.zone_lock_for(obj.guid, obj.zone, True))


OPTIONS = {"light_sanity": "lamps", "zone_locks": True, "lock_puzzle_needs": False}


class BigWallLampsMixin:
    names = LIGHTS

    def test_behind_the_big_wall_door(self: BigWalkTestBase) -> None:
        for name in self.names:
            location = self.multiworld.get_location(name, self.player)
            self.assertEqual(location.parent_region.name, regions.ENDING_ZONE, name)
        self.collect_all_but(data.BLACK_MONOLITH.item_name)
        for name in self.names:
            self.assertFalse(self.can_reach_location(name), name)
        self.collect_by_name(data.BLACK_MONOLITH.item_name)
        for name in self.names:
            self.assertTrue(self.can_reach_location(name), name)


class TestBigWallLampsBigGoodbye(BigWallLampsMixin, BigWalkTestBase):
    options = {**OPTIONS, "goal": "big_goodbye"}


class TestBigWallLampsBigWall(BigWallLampsMixin, BigWalkTestBase):
    options = {**OPTIONS, "goal": "big_wall"}


class TestBigWallLampsBigGame(BigWallLampsMixin, BigWalkTestBase):
    options = {**OPTIONS, "goal": "big_game"}


class TestBigWallLampsAsObjects(BigWallLampsMixin, BigWalkTestBase):
    # Without light_sanity the lamps are objects, and the zone locks lock the lamps by their pick-up.
    names = LAMPS
    options = {"object_sanity": "every", "zone_locks": True, "lock_puzzle_needs": False}


class TestBigWallLampsBigLights(BigWalkTestBase):
    # A goal that does not open the door itself: all the lamps then ask for it.
    options = {"goal": "big_collection", "gourds_required": 20, "objectives": ["big_lights"],
               "lights_required": 100, "lock_puzzle_needs": False}

    def test_every_lamp_needs_the_door(self) -> None:
        # collect_all_but also takes the placed events: sweep a fresh state instead.
        state = CollectionState(self.multiworld)
        for item in self.multiworld.itempool:
            if item.player == self.player and item.name != data.BLACK_MONOLITH.item_name:
                state.collect(item, True)
        state.sweep_for_advancements()
        self.assertFalse(self.multiworld.has_beaten_game(state, self.player))
        state.collect(self.world.create_item(data.BLACK_MONOLITH.item_name), True)
        state.sweep_for_advancements()
        self.assertTrue(self.multiworld.has_beaten_game(state, self.player))
