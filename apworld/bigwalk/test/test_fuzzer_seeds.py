"""
Seeds the Archipelago fuzzer failed on with 0.6.0 (2026-10-13), each cut down to the options that broke it. Every
class here also fills its seed (WorldTestBase), which is half of what it guards.
"""

from .. import data, items, regions
from .bases import BigWalkTestBase, build_like_universal_tracker, world_shape
from .test_vanilla_options import pool, precollected


class TestThePartyHasARadioInATightPool(BigWalkTestBase):
    """
    `big_fiesta` needs a radio, which used to come from the filler headroom after the gear: with the puzzles holding
    their gourds and the keys their monuments, the gear took it all and the seed could not be won (62 seeds).
    """

    options = {
        "goal": "big_goodbye", "objectives": ["big_body", "big_collection", "big_fiesta"], "random_objectives": 0,
        "gourds_required": 40, "body_required": 64, "gourd_sanity": "every_5", "radio_sanity": False,
        "shuffle_radio_music": True, "pack_sanity": True, "firework_sanity": False, "flare_gun_sanity": True,
        "tower_sanity": False, "monument_sanity": False, "cut_key_sanity": "off", "object_sanity": "off",
        "light_sanity": "off", "start_with_map_room_open": True, "start_with_arch_doors_open": ["First Arch Door"],
        "require_arch_doors": True, "include_big_game_puzzles": "disabled", "gauntlet_mode": "locked_stages",
        "linear_towers": True, "linear_puzzles": True, "lock_abilities": "ambidextrous",
        "start_with_abilities": ["carrying", "legs", "arms", "seat", "throwing"],
        "backpacks_in_pool": 4, "belts_in_pool": 5, "gourd_cartons_in_pool": 1,
    }

    def test_the_radio_is_in_the_pool(self) -> None:
        self.assertIn(items.RADIO_ITEM_NAME, pool(self))


class TestTheTrackerRebuildsAVanillaSeed(BigWalkTestBase):
    """The vanilla options put items in locations: Universal Tracker failed to rebuild such a seed without them."""

    options = {"linear_keys": True, "linear_puzzles": True, "linear_arch_doors": True, "linear_towers": True,
               "start_with_map_room_open": True}

    def test_it_lands_on_this_exact_world(self) -> None:
        tracked = build_like_universal_tracker({}, self.world.fill_slot_data())
        self.assertEqual(world_shape(tracked), world_shape(self.multiworld))


class TestNoRoomForTheWayOut(BigWalkTestBase):
    """
    The tutorial's puzzles hold their gourds, its lamps this world's own filler, and its deposit the Drawbridge: the
    First Arch Door, asked for early as the way out, had nowhere to go (23 seeds).
    """

    options = {
        "start_with_drawbridge_open": False, "start_with_arch_doors_open": ["Right Arch Door"],
        "linear_keys": True, "linear_puzzles": True, "linear_towers": True, "monument_sanity": False,
        "lock_abilities": "disabled", "object_sanity_items": "local", "light_sanity": "lamps_and_switches",
        "light_sanity_items": "local", "gourd_sanity": "off", "cut_key_sanity": "junk",
        "include_big_game_puzzles": "disabled", "firework_sanity": False, "tower_sanity": False,
    }

    def test_the_player_starts_with_it(self) -> None:
        self.assertIn(data.FIRST_ARCH_DOOR.item_name, precollected(self))
        self.assertNotIn(data.FIRST_ARCH_DOOR.item_name, pool(self))


class TestTheGamesOwnWayOut(BigWalkTestBase):
    """
    Every door as in the game, and the drawbridge closed: the tutorial's four gourds fill its monument, which holds
    the key the drawbridge waits for. That monument full used to sit in the overworld, past the drawbridge.
    """

    options = {"start_with_drawbridge_open": False, "linear_arch_doors": True, "linear_keys": True,
               "linear_towers": True, "linear_puzzles": True, "tower_mode": "all_open"}

    def test_the_tutorials_monument_full_is_in_the_tutorial(self) -> None:
        location = self.multiworld.get_location(data.monument_full_location_name(data.TOWERS[0]), self.player)
        self.assertEqual(location.parent_region.name, regions.STARTING_ZONE)
        self.assertEqual(location.item.name, data.key_item_name(data.TOWERS[0]))


class TestNoRoomForTheFirstMonument(BigWalkTestBase):
    """
    With `by_item` and the first monument closed, every puzzle holding its gourd and every door on its key's deposit,
    nothing was left that did not wait for the first Progressive Monument, which was then placed last, nowhere.
    """

    options = {
        "goal": "big_collection", "start_with_drawbridge_open": True,
        "start_with_arch_doors_open": ["Right Arch Door", "First Arch Door"],
        "linear_keys": True, "linear_puzzles": True, "monument_sanity": True, "lock_puzzle_needs": True,
        "start_with_puzzle_needs": ["eggs", "buttons", "drawing_panels", "lights", "ink_viewer", "coordinates_computer",
                                    "icon_panels", "pose_panels", "point_panels", "sync_buttons", "counter", "teapots"],
        "lock_abilities": "ambidextrous", "start_with_abilities": ["legs", "throwing", "carrying", "seat"],
        "tower_mode": "by_item", "first_tower_open": False, "object_sanity": "first_pickup",
        "object_sanity_items": "local", "light_sanity": "off", "gourd_sanity": "every_gourd", "cut_key_sanity": "off", "pack_sanity": False, "radio_sanity": False,
        "firework_sanity": False, "flare_gun_sanity": False, "tower_sanity": False, "gauntlet_mode": "locked_stages",
        "lock_gauntlet_needs": False, "include_big_game_puzzles": "disabled",
    }

    def test_the_player_starts_with_it(self) -> None:
        self.assertIn(data.PROGRESSIVE_MONUMENT_ITEM_NAME, precollected(self))


class TestJunkLeavesRoomForProgression(BigWalkTestBase):
    """
    1.0's fuzz: the packs on their own checks, the objects' and lights' checks as junk and the vanilla behaviours left
    the progression too few locations (10 short), mostly in the first spheres. The junk options now stop excluding
    while half as many again as the progression items are left (rules.room_for_progression).
    """

    options = {
        "goal": "big_game", "gourd_sanity": "off", "radio_sanity": False, "pack_sanity": "vanilla",
        "firework_sanity": False, "flare_gun_sanity": "junk", "monument_sanity": False, "cut_key_sanity": "off",
        "object_sanity": "every", "object_sanity_items": "junk", "light_sanity": "lamps_and_switches",
        "light_sanity_items": "junk", "light_sanity_junk_percentage": 75, "tower_mode": "by_item",
        "first_tower_open": False, "gauntlet_mode": "locked_stages", "linear_keys": True,
        "linear_towers": "all_deposit_boxes", "linear_puzzles": True, "lock_abilities": "ambidextrous",
        "lock_puzzle_needs": False, "trap_fill_percentage": 95, "bonus_fill_percentage": 22,
        "objectives": ["big_objects"], "random_objectives": 0, "objects_required": 7, "object_sanity_kinds": [],
        "object_sanity_junk_percentage": 58, "tower_sanity": True, "include_big_game_puzzles": "when_unlocked",
        "open_black_tower": False, "start_with_arch_doors_open": ["First Arch Door", "Left Arch Door", "Right Arch Door"],
        "start_with_abilities": ["arms", "carrying", "throwing"], "backpacks_in_pool": 6, "belts_in_pool": 0,
        "gourd_cartons_in_pool": 3,
    }

    def test_room_left(self) -> None:
        from BaseClasses import LocationProgressType
        # The progression the fill placed (not the items locked on their location: a puzzle's gourd, a deposit's door).
        progression = sum(1 for location in self.multiworld.get_locations()
                          if location.item is not None and location.item.player == self.player
                          and location.item.advancement and location.address is not None and not location.locked)
        free = sum(1 for location in self.multiworld.get_locations(self.player)
                   if location.address is not None and not location.locked
                   and location.progress_type != LocationProgressType.EXCLUDED)
        self.assertGreaterEqual(free, progression + max(5, progression // 2))
