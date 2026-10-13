"""
`object_sanity` (ROADMAP S7, 0.6): the island's gadgets stay on the map and their pick-ups are checks,
every one of them or the first of each kind.
"""

from BaseClasses import LocationProgressType

from .. import data, locations
from .bases import BigWalkTestBase


def object_locations(test: BigWalkTestBase) -> set[str]:
    names = locations.LOCATION_NAME_GROUPS["Objects"]
    return {loc.name for loc in test.multiworld.get_locations(test.player) if loc.name in names}


class TestObjectsOffByDefault(BigWalkTestBase):
    def test_no_object_location(self) -> None:
        self.assertEqual(object_locations(self), set())
        self.assertEqual(self.world.fill_slot_data()["object_sanity"], "off")


class TestObjectsEvery(BigWalkTestBase):
    options = {"object_sanity": "every", "lock_puzzle_needs": False}

    def test_every_object_but_the_paintbrushes_is_a_location(self) -> None:
        expected = {obj.location_name for obj in data.ISLAND_OBJECTS if obj.kind != "SalonBrushProp"}
        self.assertEqual(object_locations(self), expected)
        self.assertEqual(len(expected), 61 + 36 + 47)

    def test_map_room_needs_its_item(self) -> None:
        self.collect_all_but(["Map Room"])
        self.assertFalse(self.can_reach_location("Compass (Map Room)"))
        self.assertTrue(self.can_reach_location("Clock (Upper Hub Train Stop)"))

    def test_regions(self) -> None:
        self.assertEqual(self.multiworld.get_location("Large Cowbell (Tutorial)", self.player).parent_region.name,
                         "Starting Zone")
        self.collect_all_but(["Chairlift", "Tunnels"])
        self.assertFalse(self.can_reach_location("Binoculars (South Peak)"))
        self.assertFalse(self.can_reach_location("Megaphone (Spiral Holder)"))
        self.assertFalse(self.can_reach_location("Torch (Red Tunnel Outlet)"))
        self.assertTrue(self.can_reach_location("Walkie-Talkie (Ocean Treasure, Blue)"))


class TestObjectsFirstPickup(BigWalkTestBase):
    options = {"object_sanity": "first_pickup", "lock_puzzle_needs": False}

    def test_one_location_per_kind(self) -> None:
        self.assertEqual(object_locations(self), {data.object_kind_location_name(label) for kind, label in data.OBJECT_KINDS
                                                  if kind != "SalonBrushProp"})

    def test_compass_needs_the_map_room(self) -> None:
        self.collect_all_but(["Map Room"])
        self.assertFalse(self.can_reach_location("First Compass Pickup"))
        self.assertTrue(self.can_reach_location("First Laser Pickup"))


class TestObjectKindsLeftOut(BigWalkTestBase):
    options = {"object_sanity": "every", "object_sanity_kinds": ["radio", "cowbell"], "lock_puzzle_needs": False}

    def test_only_the_kinds_kept(self) -> None:
        kept = {obj.location_name for obj in data.ISLAND_OBJECTS if obj.kind in ("FmRadioProp", "CowBellLargeProp",
                                                                                "CowBellMediumProp", "CowBellSmallProp")}
        self.assertEqual(object_locations(self), kept)
        self.assertEqual(self.world.fill_slot_data()["object_sanity_kinds"],
                         ["FmRadioProp", "CowBellLargeProp", "CowBellMediumProp", "CowBellSmallProp"])


class TestObjectItemsJunk(BigWalkTestBase):
    options = {"object_sanity": "every", "object_sanity_items": "junk"}

    def test_objects_hold_no_progression(self) -> None:
        names = locations.LOCATION_NAME_GROUPS["Objects"]
        excluded = [location for location in self.multiworld.get_locations(self.player)
                    if location.name in names and location.progress_type == LocationProgressType.EXCLUDED]
        self.assertTrue(excluded)
        for location in excluded:
            if location.item is not None:
                self.assertFalse(location.item.advancement, location.name)


class TestObjectItemsLocal(BigWalkTestBase):
    options = {"object_sanity": "first_pickup", "object_sanity_items": "local"}


class TestObjectData(BigWalkTestBase):
    run_default_tests = False

    def test_unique_guids_and_known_kinds(self) -> None:
        self.assertLess(data.OBJECT_ID_OFFSET + len(data.ISLAND_OBJECTS), data.OBJECT_KIND_ID_OFFSET)
        guids = [obj.guid for obj in data.ISLAND_OBJECTS]
        self.assertEqual(len(set(guids)), len(guids))
        kinds = {kind for kind, _ in data.OBJECT_KINDS}
        self.assertEqual(set(data.OBJECT_KIND_KEYS), kinds)
        self.assertFalse({obj.kind for obj in data.ISLAND_OBJECTS} - kinds)
        zones = {"start", "overworld", "chairlift", "tunnels", "map_room"}
        for obj in data.ISLAND_OBJECTS:
            self.assertTrue(obj.zone in zones or obj.zone.startswith("tower:"), obj.location_name)
