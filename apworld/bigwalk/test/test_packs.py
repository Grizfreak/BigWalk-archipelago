"""The packs and the fireworks as checks: `pack_sanity`, `firework_sanity`."""

from .. import data, locations, regions
from .bases import BigWalkTestBase

PACKS = {pickup.location_name for pickup in data.PICKUPS}
FIREWORKS = {firework.location_name for firework in data.FIREWORKS}


def mine(test: BigWalkTestBase) -> set[str]:
    return {location.name for location in test.multiworld.get_locations(test.player)}


class TestOnByDefault(BigWalkTestBase):

    def test_twenty_more_checks(self) -> None:
        self.assertEqual(len(PACKS), 12)
        self.assertEqual(len(FIREWORKS), 8)
        self.assertTrue(PACKS <= mine(self))
        self.assertTrue(FIREWORKS <= mine(self))

    def test_the_far_east_is_past_the_chairlift(self) -> None:
        for name in ("Maroon Backpack", "Blue Belt"):
            self.assertEqual(self.multiworld.get_location(name, self.player).parent_region.name,
                             regions.CHAIRLIFT_ZONE, name)

    def test_the_tunnel_belt_is_past_the_tunnels(self) -> None:
        self.assertEqual(self.multiworld.get_location("Purple Belt", self.player).parent_region.name,
                         regions.TUNNEL_ZONE)

    def test_the_hub_fireworks_are_in_the_starting_zone(self) -> None:
        self.assertEqual(self.multiworld.get_location("Hub Fireworks", self.player).parent_region.name,
                         regions.STARTING_ZONE)

    def test_the_mod_is_told(self) -> None:
        slot = self.world.fill_slot_data()
        self.assertIs(slot["pack_sanity"], True)
        self.assertEqual(len(slot["pickup_guids"]), 12)
        for i, guid in enumerate(slot["pickup_guids"]):
            name = data.PICKUPS[i].location_name
            self.assertEqual(locations.LOCATION_NAME_TO_ID[name],
                             data.BASE_ID + slot["pickup_id_offset"] + i)
            self.assertEqual(guid, data.PICKUPS[i].guid)
        self.assertEqual(slot["firework_keys"][0], "Hub")


class TestOff(BigWalkTestBase):
    options = {"pack_sanity": False, "firework_sanity": False}

    def test_none_of_them(self) -> None:
        self.assertFalse((PACKS | FIREWORKS) & mine(self))


class TestPacksAsInTheGame(BigWalkTestBase):
    options = {"pack_sanity": False}

    def test_no_pack_location_and_the_mod_leaves_the_packs_on_the_map(self) -> None:
        self.assertFalse(PACKS & mine(self))
        self.assertIs(self.world.fill_slot_data()["pack_sanity"], False)
