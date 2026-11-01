"""
`tunnel_sanity` opens the tunnels door by door: four outlets, any of which lets you in, and the purple hole on
its own, the way to the seven purple puzzles past the chairlift.
"""

from .. import data, regions
from .bases import BigWalkTestBase


def item_names(test: BigWalkTestBase) -> list[str]:
    return [item.name for item in test.multiworld.itempool if item.player == test.player]


def purple_puzzles() -> list[str]:
    return [p.location_name for p in data.PUZZLES if p.prop_name in data.CHAIRLIFT_PUZZLES]


class TestTunnelSanityOff(BigWalkTestBase):
    options: dict = {}
    run_default_tests = False

    def test_the_tunnels_item_and_no_door(self) -> None:
        names = item_names(self)
        self.assertIn(data.YELLOW_TWIST.item_name, names)
        self.assertFalse(set(names) & {door.item_name for door in data.TUNNEL_DOORS})
        self.assertEqual(self.world.fill_slot_data()["tunnel_doors"], {})


class TestTunnelSanity(BigWalkTestBase):
    options = {"tunnel_sanity": True}

    def test_the_doors_replace_the_tunnels(self) -> None:
        names = item_names(self)
        self.assertNotIn(data.YELLOW_TWIST.item_name, names)
        for door in data.TUNNEL_DOORS:
            self.assertEqual(names.count(door.item_name), 1, door.item_name)

    def test_any_outlet_lets_you_in(self) -> None:
        entrance = self.multiworld.get_entrance(regions.TUNNEL_ENTRANCE, self.player)
        for door in data.TUNNEL_DOORS:
            if door.tower is None:
                continue
            self.remove_by_name(
                [d.item_name for d in data.TUNNEL_DOORS if self.multiworld.state.has(d.item_name, self.player)])
            self.assertFalse(entrance.can_reach(self.multiworld.state), door.item_name)
            self.collect_by_name(door.item_name)
            self.assertTrue(entrance.can_reach(self.multiworld.state), door.item_name)

    def test_the_purple_tunnel_is_not_a_way_in(self) -> None:
        self.collect_by_name(data.PURPLE_TUNNEL_ITEM_NAME)
        entrance = self.multiworld.get_entrance(regions.TUNNEL_ENTRANCE, self.player)
        self.assertFalse(entrance.can_reach(self.multiworld.state))

    def test_the_purple_puzzles_need_the_purple_tunnel(self) -> None:
        self.collect_by_name(data.GREEN_CUP.item_name)
        for name in purple_puzzles():
            self.assertFalse(self.can_reach_location(name), name)
        self.collect_by_name(data.PURPLE_TUNNEL_ITEM_NAME)
        for name in purple_puzzles():
            self.assertTrue(self.can_reach_location(name), name)

    def test_slot_data_names_the_gates(self) -> None:
        doors = self.world.fill_slot_data()["tunnel_doors"]
        self.assertEqual(len(doors), len(data.TUNNEL_DOORS))
        purple = doors[str(data.tunnel_door_item_id(data.TUNNEL_DOORS[-1]))]
        self.assertEqual(len(purple), 2)


class TestTunnelSanityFarDoors(BigWalkTestBase):
    options = {"tunnel_sanity": True, "require_arch_doors": True,
               "start_with_arch_doors_open": [data.FIRST_ARCH_DOOR.item_name]}
    run_default_tests = False

    def test_an_outlet_past_a_far_door_needs_it(self) -> None:
        entrance = self.multiworld.get_entrance(regions.TUNNEL_ENTRANCE, self.player)
        self.collect_by_name("Yellow Tunnel Door")
        self.assertFalse(entrance.can_reach(self.multiworld.state))
        self.collect_by_name(data.LEFT_ARCH_DOOR.item_name)
        self.assertTrue(entrance.can_reach(self.multiworld.state))


class TestTunnelSanityWithLinearKeys(BigWalkTestBase):
    options = {"tunnel_sanity": True, "linear_keys": True}
    run_default_tests = False

    def test_linear_keys_wins(self) -> None:
        self.assertFalse(self.world.options.tunnel_sanity)
        self.assertNotIn("Purple Tunnel", item_names(self))
        tunnels = self.multiworld.get_location(data.YELLOW_TWIST.location_name, self.player)
        self.assertEqual(tunnels.item.name, data.YELLOW_TWIST.item_name)
