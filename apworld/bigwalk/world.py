"""The Big Walk Archipelago world."""

from __future__ import annotations

import logging
import random
from collections import Counter
from collections.abc import Mapping
from typing import TYPE_CHECKING, Any, TextIO

from BaseClasses import CollectionState, Entrance, Item, Location, LocationProgressType, MultiWorld
from Options import OptionError
from rule_builder.rules import Rule
from worlds.AutoWorld import World

from . import data, gourd_name, items, locations, logic_export, regions, rules, tiles, web_world
from . import options as bigwalk_options

if TYPE_CHECKING:
    from NetUtils import MultiData

WORLD_VERSION = "1.0.6"
"""Kept in step with archipelago.json; sent in slot_data so the mod can check it."""

TRACKER_OPTIONS = {
    "goal": "goal",
    "objectives": "objectives",
    "lock_endings_until_objectives": "lock_endings_until_objectives",
    "keys_required": "keys_required",
    "tower_sanity": "tower_sanity",
    "tower_mode": "tower_mode",
    "first_tower_open": "first_tower_open",
    "everyone_gathers": "everyone_gathers",
    "towers_required": "towers_required",
    "radio_stations_required": "radio_stations_required",
    "deposit_goal_amount": "gourds_required",
    "deposit_locations": "gourd_sanity",
    "radio_station_checks": "radio_sanity",
    "radio_station_items": "shuffle_radio_music",
    "start_with_arch_doors_open": "start_with_arch_doors_open",
    "require_arch_doors": "require_arch_doors",
    "gauntlet_mode": "gauntlet_mode",
    "gauntlet_puzzles_required": "gauntlet_puzzles_required",
    "lock_gauntlet_needs": "lock_gauntlet_needs",
    "gauntlet_stage_items": "gauntlet_stage_items",
    "teleport_buttons": "teleport_buttons",
    "lock_puzzle_needs": "lock_puzzle_needs",
    "start_with_puzzle_needs": "start_with_puzzle_needs",
    "start_with_random_puzzle_need": "start_with_random_puzzle_need",
    "lock_abilities": "lock_abilities",
    "start_with_abilities": "start_with_abilities",
    "include_big_game_puzzles": "include_big_game_puzzles",
    "monument_sanity": "monument_sanity",
    "linear_towers": "linear_towers",
    "cut_key_sanity": "cut_key_sanity",
    "lock_running": "lock_running",
    "rides_require_seat": "rides_require_seat",
    "pack_sanity": "pack_sanity",
    "firework_sanity": "firework_sanity",
    "flare_gun_sanity": "flare_gun_sanity",
    "object_sanity": "object_sanity",
    "object_sanity_kind_keys": "object_sanity_kinds",
    "light_sanity": "light_sanity",
    "zone_locks": "zone_locks",
    "fast_golf": "fast_golf",
    "lights_required": "lights_required",
    "objects_required": "objects_required",
    "body_required": "body_required",
    "random_colors": "random_colors",
    "trap_link": "trap_link",
    # The vanilla options place items in locations (L1 to L6), which moves what the pool must hold and, for the arch
    # doors, the rules: a tracker without them failed to rebuild the seed (the fuzzer's UT check, 2026-10-13).
    "linear_keys": "linear_keys",
    "linear_puzzles": "linear_puzzles",
    "linear_arch_doors": "linear_arch_doors",
    "start_with_drawbridge_open": "start_with_drawbridge_open",
    "start_with_map_room_open": "start_with_map_room_open",
}
"""
The slot_data fields that are options, mapped to the option each one comes
from, and the whole of what a re-generation needs to land on the same world.

The two sides have different names since the options were renamed after the
words players use (2026-09-25): slot_data kept the names the mod was built
against. Its Choice values are the pre-rename ones too, which the options
still accept as aliases, so `from_any` reads them back unchanged.

Every other option decides what goes into the pool, not what the graph looks
like: `trap_fill_percentage` only ever picks between two kinds of filler, and
cannot move a location. `start_with_drawbridge_open` used to be one of them, and
travels since `linear_keys` (L2): with it, the Drawbridge is either handed over
or locked on the key's deposit.

Kept in step with `fill_slot_data` by a test rather than by care.
"""

class BigWalkWorld(World):
    """
    Big Walk is a co-op walking game: two or more players wander a large
    open island, solve puzzles together for gourds, carry those gourds to the
    towers' monuments, and unlock the big keys that open up the map.
    """

    game = "Big Walk"
    # What the seed asks for besides its Goal (_check_the_objectives); empty for a world that never generates early.
    objectives: frozenset[str] = frozenset()

    web = web_world.BigWalkWebWorld()

    options_dataclass = bigwalk_options.BigWalkOptions
    options: bigwalk_options.BigWalkOptions

    item_name_to_id = items.ITEM_NAME_TO_ID
    location_name_to_id = locations.LOCATION_NAME_TO_ID

    item_name_groups = items.ITEM_NAME_GROUPS
    location_name_groups = locations.LOCATION_NAME_GROUPS

    origin_region_name = regions.STARTING_ZONE
    topology_present = True

    # The Big Walk mod is not an Archipelago text client: it needs slot_data
    # and the modern data package, so there is no point pretending it works
    # with anything older.
    required_client_version = (0, 6, 0)

    # --- Universal Tracker ---
    #
    # UT re-runs this world's generation inside the tracker and compares the
    # result against the server. Nothing here is random beyond which filler
    # is picked, so the graph it builds is the seed's graph exactly — as long
    # as it generates with the seed's OPTIONS, which is what these two hooks
    # are for. Left to the tracking player's own YAML, one wrong option moves
    # the goalposts in silence: `gourd_sanity: every_gourd` on its own invents
    # thirty-six locations.

    ut_can_gen_without_yaml = True
    """
    No YAML is needed in the tracker: everything that shapes this world
    travels in slot_data (TRACKER_OPTIONS), so UT skips its first generation
    and builds straight from the seed.
    """

    # --- Derived from the options, computed once in generate_early ---

    towers: tuple[data.Tower, ...]
    """Towers in play this slot: always all seven."""

    gourd_count: int
    """`Gourd` items in the pool; exactly enough to fill every monument."""

    deposit_amounts: tuple[int, ...]
    """Deposit counts that are locations, e.g. (5, 10, ..., 45)."""

    deposit_goal: int
    """Gourds required to win when the goal is `big_collection`."""

    locked_arch_doors: tuple[data.ArchDoor, ...]
    """Arch doors held closed until their item arrives."""

    gourd_name: str = data.GOURD_ITEM_NAME
    """Display name of the gourds, shared by every Big Walk slot."""

    way_out: tuple[str, ...] = ()
    """The items asked for early as the way out of the starting zone; empty when nothing needs asking."""

    @staticmethod
    def interpret_slot_data(slot_data: Mapping[str, Any]) -> Mapping[str, Any]:
        """
        Hand the seed's slot_data back to Universal Tracker, which re-runs
        generation with it in `multiworld.re_gen_passthrough` — picked up by
        `_take_options_from_tracker` below.

        Returning something non-None is what asks for that second pass;
        returning None would leave UT tracking the YAML it started from.
        """
        return slot_data

    def _cabin_fever_waits(self) -> dict[str, dict[str, object]]:
        """
        What the mod does to the waits of the two Cabin Fever puzzles, by puzzle: the mode, the
        seconds, whether the hidden help button is there, and whether the dial hides the time (R6b). One number each since 0.4 (players,
        2026-10-06): the game's own wait is `vanilla` (seconds 0), any other `fixed`; a random wait
        is Archipelago's `random-range`, drawn once when the options are read.
        """
        waits: dict[str, dict[str, object]] = {}
        for key, game_wait in (("cabin_fever", 300), ("cabin_fever_long", 1800)):
            seconds = getattr(self.options, f"{key}_seconds").value
            mode = "vanilla" if seconds == game_wait else "fixed"
            waits[key] = {
                "mode": mode,
                "seconds": 0 if mode == "vanilla" else seconds,
                "help": bool(getattr(self.options, f"{key}_help")),
                "hide_timer": bool(getattr(self.options, f"{key}_hide_timer")),
            }
        return waits

    def generate_early(self) -> None:
        self._take_options_from_tracker()

        # The seed's colors (C1, player 2026-10-07): between five and all ten of the palette,
        # in a drawn order; the first four paint the four flare guns.
        self.color_palette: list[int] = []
        if self.options.random_colors:
            count = self.random.randint(5, data.PALETTE_SIZE)
            self.color_palette = self.random.sample(range(data.PALETTE_SIZE), count)

        # The Green Dome used to be optional (`green_dome_deposits`, removed
        # 2026-09-25 before the first release): its fifteen slots only ever
        # bought deposit checks, which gourd_sanity already scales, and
        # leaving it out took the Spawn Secret Door zone with it for nothing.
        self.towers = data.TOWERS
        self.gourd_count = data.MAX_MONUMENT_SLOTS

        self.peg_tiles = self._draw_the_tiles()
        self.peg_drawings: dict[str, str] = {}
        if self.options.custom_drawings:
            own = random.Random(f"{self.multiworld.seed}-{self.player}-drawings")
            self.peg_drawings = tiles.draw_drawings(own, [drawing.key for drawing in tiles.CUSTOM_DRAWINGS])
        if self.options.mirrored_poses.value:
            poses = random.Random(f"{self.multiworld.seed}-{self.player}-poses")
            self.peg_drawings.update(tiles.draw_poses(poses, self.options.mirrored_poses.value))
        self.deposit_amounts = self._pick_deposit_amounts(self.gourd_count)
        self.cabin_fever = self._cabin_fever_waits()
        self.deposit_goal = self.options.gourds_required.value
        self._check_the_objectives()
        self._draw_the_tower_order()
        self._draw_the_monument_keys()

        self._pick_a_random_puzzle_need()
        self._ask_for_buttons_early()
        opened = self.options.start_with_arch_doors_open.value
        # L6 `linear_arch_doors`: no door is an item; the game's buttons open them.
        self.locked_arch_doors = () if self.options.linear_arch_doors else tuple(
            door for door in data.ARCH_DOORS if door.item_name not in opened)
        self._ask_for_an_early_way_out()
        self._ask_for_the_first_monument_early()
        self._ask_for_the_far_doors_early()
        self._hint_the_keys()
        self._keep_the_gauntlet_stages_local()
        if self.options.zone_locks_local:
            self.options.local_items.value |= {lock.item_name for lock in self.zone_locks()}

    @classmethod
    def stage_generate_early(cls, multiworld: MultiWorld) -> None:
        # The data package is per game, so one name for all Big Walk slots.
        worlds = [multiworld.worlds[player] for player in multiworld.get_game_players(cls.game)]
        taken = (*cls.item_name_to_id, *cls.item_name_groups)
        name = gourd_name.shared({
            world.player_name: gourd_name.clean(world.options.gourd_name.value, taken, world.player_name)
            for world in worlds
        })
        for world in worlds:
            world.gourd_name = name

    def modify_multidata(self, multidata: MultiData) -> None:
        package = multidata["datapackage"].get(self.game)
        if self.gourd_name == data.GOURD_ITEM_NAME or package is None:
            return
        if data.GOURD_ITEM_NAME in package["item_name_to_id"]:
            multidata["datapackage"][self.game] = gourd_name.renamed_package(package, self.gourd_name)

    def write_spoiler_header(self, spoiler_handle: TextIO) -> None:
        if self.gourd_name != self.options.gourd_name.value:
            spoiler_handle.write(f"{'Gourds called:':33}{self.gourd_name}\n")
        if self.options.random_objectives.value:
            spoiler_handle.write(f"{'Objectives drawn:':33}{', '.join(self.objective_keys())}\n")
        if self.peg_tiles != tiles.VANILLA_TILES:
            spoiler_handle.write(f"{'Basic tiles shown as:':33}{', '.join(self.peg_tiles)}\n")
        mirrors = {place: name for place, name in self.peg_drawings.items() if place in tiles.POSE_PLACES}
        if mirrors:
            spoiler_handle.write(f"{'Mirrored poses:':33}"
                                 f"{', '.join(f'{place}={name}' for place, name in sorted(mirrors.items()))}\n")

    def _draw_the_tiles(self) -> tuple[str, ...]:
        """
        R8 `custom_tiles`: the nine basic tiles' drawings (tiles.draw). From a generator of their own, drawn from the
        seed, so that the rest of the seed draws as it did before the tiles.
        """
        mode = self.options.custom_tiles
        if mode == bigwalk_options.CustomTiles.option_off:
            return tiles.VANILLA_TILES
        drawings = [tile.key for tile in tiles.CUSTOM_TILES]
        preferred: list[str] = []
        if mode == bigwalk_options.CustomTiles.option_custom_only:
            candidates = drawings
        elif mode == bigwalk_options.CustomTiles.option_all:
            candidates = [*tiles.VANILLA_TILES, *drawings]
        else:
            matching = tiles.matching(self.multiworld.game.values())
            candidates = [*tiles.VANILLA_TILES, *matching]
            if mode == bigwalk_options.CustomTiles.option_matching_first:
                preferred = matching
        own = random.Random(f"{self.multiworld.seed}-{self.player}-tiles")
        return tiles.draw(own, candidates, self.options.custom_tile_weights.value,
                          sorted(self.options.custom_tile_choices.value), preferred)

    def _keep_the_gauntlet_stages_local(self) -> None:
        """
        Asks the generator to keep the seven stage items in this player's own
        world when `gauntlet_stages_local` is on. Where they go changes no
        location, so it stays out of slot_data and the tracker.
        """
        locked = self.options.gauntlet_mode == bigwalk_options.GauntletMode.option_locked_stages
        if locked and self.options.gauntlet_stages_local:
            self.options.local_items.value |= set(items.GAUNTLET_ITEM_NAMES) | {data.PROGRESSIVE_GAUNTLET_ITEM_NAME}

    def _hint_the_keys(self) -> None:
        """
        `hint_keys: from_start` adds the seven big keys to the player's start
        hints, which the server turns into hints when the room opens. Nothing
        here moves an item or a location, so it never reaches slot_data.
        """
        if self.options.hint_keys != bigwalk_options.HintKeys.option_from_start:
            return
        self.options.start_hints.value |= {data.key_item_name(tower) for tower in self.towers}

    def _pick_a_random_puzzle_need(self) -> None:
        """
        Write one random need into `start_with_puzzle_needs` when none of
        the player's already opens a tutorial puzzle. The pick lands in the
        option itself, so slot_data carries the result and Universal Tracker,
        which finds the foothold already there, draws nothing.
        """
        if not self.options.lock_puzzle_needs:
            return

        candidates = data.tutorial_footholds(self.options.start_with_puzzle_needs.value)
        if candidates and not self.options.start_with_random_puzzle_need:
            # Measured: generation does not fail here, it never ends, so
            # say so before it gets that far.
            raise OptionError(
                f"Big Walk: {self.player_name} starts with nothing to do. With lock_puzzle_needs on, list "
                "buttons or sync_buttons in start_with_puzzle_needs, or turn start_with_random_puzzle_need "
                "on, so that one of the tutorial's puzzles can be solved from the start.")
        if candidates:
            picked = self.random.choice(candidates)
            self.options.start_with_puzzle_needs.value = set(self.options.start_with_puzzle_needs.value) | {picked.key}

    def _ask_for_buttons_early(self) -> None:
        """
        With the puzzles' parts as items, ask for the three most puzzles share (Buttons,
        Synchronized Buttons, Icon Panels) early, in any world (in a solo seed the player's own),
        those the player does not start with. The default start opens a dozen locations and each
        part one to six more: 1 seed in 23 failed to generate (2026-10-05).
        """
        if not self.options.lock_puzzle_needs:
            return
        for need in data.EARLY_NEEDS:
            if need.key not in self.options.start_with_puzzle_needs.value:
                self.multiworld.early_items[self.player][need.item_name] = 1

    def _ask_for_an_early_way_out(self) -> None:
        """
        With the first arch door locked, the starting zone holds only a
        handful of checks, and the way out — the Drawbridge or the First
        Arch Door — must be among the first things this player finds. One of
        the two, picked at random, is asked for as an early local item; if
        the Drawbridge is handed over at the start there is nothing to ask.
        """
        self.way_out = ()
        if self.options.start_with_drawbridge_open:
            return
        # With the game's own doors, the First Arch Door's button is outside: the Drawbridge is the way out.
        if self.options.linear_arch_doors:
            if not self.options.linear_keys:
                self.way_out = (data.DRAWBRIDGE_ITEM_NAME,)
            elif not self.key_in_its_monument(data.TOWERS[0]):
                # L2: the Drawbridge sits on its key's deposit, in the tutorial; the key must be found there.
                self.way_out = (data.key_item_name(data.TOWERS[0]),)
            else:
                # With linear_towers too, the game's own way: the tutorial's monument holds the key, and its gourds
                # must be found in the tutorial, unless its puzzles hold them (`linear_puzzles`); with `by_item` and
                # the first monument closed, its item as well.
                gourds = 0 if self.options.linear_puzzles else self.key_gourds(data.TOWERS[0])
                self.way_out = (data.GOURD_ITEM_NAME,) * gourds
                if self.options.tower_mode == bigwalk_options.TowerMode.option_by_item and not self.options.first_tower_open:
                    self.way_out += (data.PROGRESSIVE_MONUMENT_ITEM_NAME,)
        elif data.FIRST_ARCH_DOOR in self.locked_arch_doors:
            self.way_out = (data.FIRST_ARCH_DOOR.item_name if self.options.linear_keys
                            else self.random.choice((data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name)),)
        for name in self.way_out:
            self.multiworld.local_early_items[self.player][name] = self.way_out.count(name)

    def _ask_for_the_first_monument_early(self) -> None:
        """
        With `by_item` and the first monument closed, every deposit, every monument full and with the vanilla options
        every key and door wait for the first Progressive Monument: asked for early, in this world, where
        _make_room_at_the_start can tell whether there is room for it. Left to the fill, it was the last item
        placed, with nowhere left (the fuzzer, 4 seeds in 4000, 2026-10-13).
        """
        if (self.options.tower_mode != bigwalk_options.TowerMode.option_by_item or self.options.first_tower_open
                or data.PROGRESSIVE_MONUMENT_ITEM_NAME in self.way_out):
            return
        self.multiworld.local_early_items[self.player][data.PROGRESSIVE_MONUMENT_ITEM_NAME] = 1

    def _ask_for_the_far_doors_early(self) -> None:
        """
        With `require_arch_doors`, a far door that starts closed is asked for as an
        early item in this player's own world. The doors only matter if they come
        first: one that turned up late would hold a whole region in logic for a
        shortcut nobody could use yet, which is the walk the option is meant to spare.

        Only while the First Arch Door is open and the puzzles' parts are not items.
        With the door closed the starting zone holds a handful of locations that can
        be reached with nothing, and the way out is already asked for there; with the
        parts locked most puzzles cannot be reached with nothing either. In both cases
        two more early items do not fit and generation fails, found by generating the
        same seeds with and without the request: 8 of 8 without it, 6 of 8 with it.
        The doors are in logic all the same, so they still come before what lies past
        them; they are just not forced to the very start.
        """
        if (not self.options.require_arch_doors or data.FIRST_ARCH_DOOR in self.locked_arch_doors
                or self.options.lock_puzzle_needs):
            return

        for door in (data.LEFT_ARCH_DOOR, data.RIGHT_ARCH_DOOR):
            if door in self.locked_arch_doors:
                self.multiworld.local_early_items[self.player][door.item_name] = 1

    def _take_options_from_tracker(self) -> None:
        """
        Swap this slot's options for the ones the seed was really generated
        from, while Universal Tracker is re-generating. A no-op everywhere
        else, since nothing sets `re_gen_passthrough` during a real fill.

        The values arrive as `current_key` strings and one int, which
        `from_any` reads back into the option types.
        """
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        slot_data = passthrough.get(self.game) if passthrough else None
        if not slot_data:
            return

        for field, name in TRACKER_OPTIONS.items():
            if field in slot_data:
                option = getattr(self.options, name)
                setattr(self.options, name, option.from_any(slot_data[field]))

        # A seed from before `gauntlet_stage_items` (0.3.0) has one item per stage: the
        # progressive default would ask the tracker for doors that seed never had, and the
        # Gauntlet's end would never be in logic (E2, 2026-10-05).
        if "gauntlet_stage_items" not in slot_data:
            self.options.gauntlet_stage_items = bigwalk_options.GauntletStageItems(
                bigwalk_options.GauntletStageItems.option_individual)

    def _check_the_objectives(self) -> None:
        """
        The objectives asked for besides the Goal (the ending, chosen apart, the player 2026-10-12): those listed, or a
        few drawn among them (`random_objectives`). Universal Tracker reads back what the seed drew.
        """
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        slot_data = passthrough.get(self.game) if passthrough else None
        if slot_data and "objectives" in slot_data:
            self.objectives = frozenset(slot_data["objectives"]) - {self.options.goal.current_key}
            return

        if (self.options.goal == bigwalk_options.Goal.option_big_wall
                and self.options.gauntlet_mode == bigwalk_options.GauntletMode.option_locked_stages):
            raise OptionError(
                f"Big Walk ({self.player_name}): the goal big_wall ends the run at the chapel, before the Silent "
                f"Gauntlet, whose locked stages would then hold checks for nothing. Set "
                f"gauntlet_mode to vanilla, or the goal to big_goodbye or big_game.")

        listed = {bigwalk_options.OBJECTIVE_ALIASES.get(key, key) for key in self.options.objectives.value}
        endings = sorted(listed & set(bigwalk_options.OBJECTIVE_ENDINGS))
        if endings:
            raise OptionError(
                f"Big Walk ({self.player_name}): {', '.join(endings)} is an ending: it is the goal, not an objective. "
                f"Set `goal` to the ending, and list the other things to do in `objectives`.")

        count = self.options.random_objectives.value
        if count:
            pool = listed or {key for key in bigwalk_options.OBJECTIVE_KEYS
                              if key not in bigwalk_options.OBJECTIVE_ENDINGS and self._objective_allowed(key)}
            if count > len(pool):
                logging.warning(f"Big Walk ({self.player_name}): random_objectives asks for {count}, "
                                f"only {len(pool)} can be drawn.")
                count = len(pool)
            listed = set(self.random.sample(sorted(pool), count))

        if "big_objects" in listed and self.options.object_sanity == bigwalk_options.ObjectSanity.option_off:
            raise OptionError(
                f"Big Walk ({self.player_name}): the Objectives hold big_objects, whose objects are the Object Sanity "
                f"checks. Set Object Sanity to first_pickup or every.")
        if "big_body" in listed and self.options.lock_abilities == bigwalk_options.LockAbilities.option_disabled:
            raise OptionError(
                f"Big Walk ({self.player_name}): the Objectives hold big_body, whose abilities are the Lock Abilities "
                f"items. Set Lock Abilities to handed or ambidextrous.")
        if "big_radio" in listed and not self.options.radio_sanity:
            raise OptionError(
                f"Big Walk ({self.player_name}): the Objectives hold big_radio, whose stations are the "
                f"Radio Sanity checks. Turn Radio Sanity on.")
        self.objectives = frozenset(listed)

    def _objective_allowed(self, key: str) -> bool:
        """Whether the other options let this objective be drawn."""
        if key == "big_radio":
            return bool(self.options.radio_sanity)
        if key == "big_objects":
            return self.options.object_sanity != bigwalk_options.ObjectSanity.option_off
        if key == "big_body":
            return self.options.lock_abilities != bigwalk_options.LockAbilities.option_disabled
        if key == "big_towers":
            return self.options.tower_mode != bigwalk_options.TowerMode.option_all_open
        return True

    def _tower_reach_hints(self) -> dict[str, list[int]]:
        """
        By tower: the key cuts and the key deposit, hinted when it is reached (`hint_discovered_key_locations`), and its
        monument's deposits. All seven: the drawbridge and the Green Dome are reached by position (no check of their own).
        """
        hints: dict[str, list[int]] = {}
        for tower in data.TOWERS:
            ids: list[int] = []
            if self.options.hint_discovered_key_locations:
                if self.options.cut_key_sanity != bigwalk_options.CutKeySanity.option_off:
                    ids += [data.cut_location_id(tower, i) for i in range(tower.segments)]
                ids.append(data.key_deposit_location_id(tower))
            if self.options.hint_discovered_deposit_boxes and self.tower_order:
                ids += self._deposit_ids_of(tower)
            if ids:
                hints[tower.prop_name] = ids
        return hints

    def _deposit_ids_of(self, tower: data.Tower) -> list[int]:
        """The gourd deposit checks that sit in this tower's monument, in the seed's order of towers."""
        start = 0
        for prop in self.tower_order:
            if prop == tower.prop_name:
                break
            start += self.tower_slots(data.tower_by_prop(prop))
        return [data.deposit_location_id(amount) for amount in self.deposit_amounts
                if start < amount <= start + self.tower_slots(tower)]

    def tower_slots(self, tower: data.Tower) -> int:
        """
        How many slots of this tower the seed's gourds can fill. The monuments hold 45 slots and the pool 45 gourds, so
        today it is the tower's own number; if the pool holds fewer, the Green Dome, the last tower, gives up what is
        missing (the player, 2026-10-12), so that every other tower can still be filled and the dome is the one left short.
        """
        missing = max(0, data.MAX_MONUMENT_SLOTS - self.gourd_count)
        if tower.prop_name == "bigKeyOverflow":
            return max(0, tower.slots - missing)
        return tower.slots

    def _draw_the_tower_order(self) -> None:
        """
        With `tower_mode` in_order or by_item, the order the monuments open in: drawn once per seed from all
        seven towers, or read back from the seed's slot data while Universal Tracker re-generates it.
        """
        self.tower_order: tuple[str, ...] = ()
        if self.options.tower_mode == bigwalk_options.TowerMode.option_all_open:
            if "big_towers" in self.objectives:
                raise OptionError(
                    f"Big Walk ({self.player_name}): the Objectives hold big_towers, which needs Tower Mode "
                    f"in_order or by_item.")
            return

        if not self.options.first_tower_open and self.options.tower_mode != bigwalk_options.TowerMode.option_by_item:
            raise OptionError(
                f"Big Walk ({self.player_name}): first_tower_open is off, which needs Tower Mode by_item: in_order has "
                f"nothing to open the first monument.")

        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        slot_data = passthrough.get(self.game) if passthrough else None
        known = tuple(slot_data.get("tower_order", ())) if slot_data else ()
        props = [tower.prop_name for tower in self.towers]
        if sorted(known) == sorted(props):
            self.tower_order = known
            return

        order = list(props)
        self.random.shuffle(order)
        self.tower_order = tuple(order)

    def tower_index(self, prop_name: str) -> int:
        return self.tower_order.index(prop_name)

    def _goal_for_older_mods(self) -> str:
        """The single goal a mod from before the objectives reads: the Goal itself, which is the ending."""
        return self.options.goal.current_key

    def objective_keys(self) -> tuple[str, ...]:
        """What this seed asks for, in the frozen order: the objectives and the Goal; the Goal alone when none are."""
        listed = self.objectives | {self.options.goal.current_key}
        return tuple(key for key in bigwalk_options.OBJECTIVE_KEYS if key in listed)

    def _pick_deposit_amounts(self, total_slots: int) -> tuple[int, ...]:
        choice = self.options.gourd_sanity
        if choice == bigwalk_options.GourdSanity.option_off:
            return ()
        if choice == bigwalk_options.GourdSanity.option_every_gourd:
            return tuple(range(1, total_slots + 1))

        # Milestones: every fifth deposit, plus the very last one so that
        # filling every monument always lands on a check. That second part
        # is defensive today — the total (45) is already a multiple of
        # five — and exists so a future option that
        # changes the totals cannot silently drop the final milestone.
        milestones = set(range(5, total_slots + 1, 5))
        milestones.add(total_slots)
        return tuple(sorted(milestones))

    # --- Generation steps ---

    def create_regions(self) -> None:
        regions.create_and_connect_regions(self)
        locations.create_all_locations(self)

    def create_items(self) -> None:
        items.create_all_items(self)

    def set_rules(self) -> None:
        rules.set_all_rules(self)

    def set_rule(self, spot: Location | Entrance, rule: Any) -> None:
        """
        As Archipelago's, and remembers the rule as it was built, by the resolved rule it becomes: the logic travels
        to the mod as data (logic_export, R7), and a rule copied from one location to another (big_objects' events)
        is found by its resolved self.
        """
        super().set_rule(spot, rule)
        if isinstance(rule, Rule):
            if not hasattr(self, "_built_rules"):
                self._built_rules: dict[int, tuple[Any, Rule]] = {}
            self._built_rules[id(spot.access_rule)] = (spot.access_rule, rule)

    def _hint_items(self) -> dict[str, int]:
        """
        This slot's items by name and count, in the pool or already placed: what `!hint` can find. Not the items given
        at the start (the server finds nothing for them, seen 2026-10-13 with the Drawbridge open from the start).
        """
        placed = [location.item for location in self.multiworld.get_locations() if location.item is not None]
        counts = Counter(item.name for item in {id(item): item for item in [*self.multiworld.itempool, *placed]}.values()
                         if item.player == self.player and item.code is not None)
        return dict(sorted(counts.items()))

    def exported_rule(self, access_rule: Any) -> Rule | None:
        """The built rule behind a resolved one; None for a location or entrance that has none (always open)."""
        found = getattr(self, "_built_rules", {}).get(id(access_rule))
        if found is not None and found[0] is access_rule:
            return found[1]
        if access_rule is Location.access_rule or access_rule is Entrance.access_rule:
            return None
        raise TypeError("Big Walk: a rule set without the rule builder cannot travel to the mod")

    def pre_fill(self) -> None:
        # The local items first: they may take the starting zone's places, and Legs must then see what is left.
        self._place_local_items("Objects", self.options.object_sanity_items.value, "object_sanity_items")
        self._place_local_items("Lights", self.options.light_sanity_items.value, "light_sanity_items")
        self._make_room_at_the_start()
        self._open_the_way_one_step_at_a_time()

    def _open_the_way_one_step_at_a_time(self) -> None:
        """
        The fill places one item at a time, each where the items not yet placed can reach: a step that needs two
        items at once, with a single free location before it, cannot be filled. With `linear_keys` and `linear_towers`
        the starting zone can come to that: one free location before Legs and Carrying together (the
        fuzzer, 2026-10-14: 2 option sets of 7 failed every seed). So the way is opened here as the fill would, with
        this world's progression: one item per free location; where no single item opens anything but two together
        do, and only one location is free, the player starts with one of the two (the generation log says which).
        Steps that only count gourds are left to the fill. Only this world's part of the state is swept, tried and
        put back: its rules ask for its own items, and a sweep or a copy of the whole state costs every other world's
        logic at each trial (10-20 minutes for two walks among ~300 slots, 2026-10-16).
        """
        mine = [item for item in self.multiworld.itempool if item.player == self.player and item.advancement]
        if not mine:
            return
        own = self.multiworld.get_locations(self.player)
        state = CollectionState(self.multiworld)
        state.sweep_for_advancements(own)
        used = 0

        def reach(with_state: CollectionState) -> set[str]:
            return {loc.name for loc in self.multiworld.get_unfilled_locations(self.player)
                    if loc.progress_type != LocationProgressType.EXCLUDED and loc.can_reach(with_state)}

        def opens(names: tuple[str, ...], now: set[str]) -> int:
            player = self.player
            saved = (state.prog_items[player].copy(), state.reachable_regions[player].copy(),
                     state.blocked_connections[player].copy(), state.stale[player],
                     state.advancements.copy(), state.locations_checked.copy())
            try:
                for name in names:
                    state.collect(self.create_item(name), True)
                state.sweep_for_advancements(own)
                return len(reach(state) - now)
            finally:
                (state.prog_items[player], state.reachable_regions[player], state.blocked_connections[player],
                 state.stale[player], state.advancements, state.locations_checked) = saved

        for _ in range(len(mine)):
            now = reach(state)
            free = len(now) - used
            names = sorted({item.name for item in mine})
            if not names:
                return
            singles = [(opens((name,), now), name) for name in names]
            best = max(singles)
            if best[0] > 0:
                chosen: tuple[str, ...] = (best[1],)
            else:
                pairs = [(opens((a, b), now), (a, b)) for i, a in enumerate(names) for b in names[i:]
                         if a != b or sum(1 for item in mine if item.name == a) > 1]
                pairs = [pair for pair in pairs if pair[0] > 0]
                if not pairs:
                    return
                chosen = max(pairs)[1]
            if free < len(chosen):
                item = next(item for item in mine if item.name == chosen[0])
                logging.warning(f"Big Walk ({self.player_name}): {' and '.join(chosen)} needed with room for "
                                f"{max(free, 0)}; starting with {item.name}.")
                mine.remove(item)
                self.multiworld.itempool[:] = [other for other in self.multiworld.itempool if other is not item]
                local_early = self.multiworld.local_early_items[self.player]
                if local_early.get(item.name):
                    local_early[item.name] -= 1
                    if not local_early[item.name]:
                        del local_early[item.name]
                self.push_precollected(item)
                self.multiworld.itempool.append(self.create_filler())
                state.collect(item, True)
                state.sweep_for_advancements(own)
                continue
            for name in chosen:
                item = next(item for item in mine if item.name == name)
                mine.remove(item)
                state.collect(item, True)
                used += 1
            state.sweep_for_advancements(own)

    def _make_room_at_the_start(self) -> None:
        """
        What the exit of the starting zone asks for must lie in it: the way out (_ask_for_an_early_way_out) and, with
        `lock_abilities`, Legs; and with `by_item` the first monument's item, which nearly everything waits for. All
        are asked for as early local items, which the early fill places before anything else, in the locations it
        reaches with nothing. The tutorial's own gourds (`linear_puzzles`), its doors
        (`linear_keys`) and the local checks (pre_fill) may leave too few of them: the fuzzer failed 1 seed in 200
        that way, 4 in 4 with `linear_puzzles` and `by_item` (2026-10-13). Then the far doors stop being asked for
        early, then the player starts with Legs, then with the first monument's item, then with the way out, and the
        generation log says so.
        """
        local_early = self.multiworld.local_early_items[self.player]
        mine = [item for item in self.multiworld.itempool if item.player == self.player]
        if any(item.name == data.LEGS_ITEM_NAME for item in mine):
            local_early[data.LEGS_ITEM_NAME] = 1
        # One pool item for each name asked for, given up from the end: Legs first. By identity, since items compare
        # equal by name and player.
        needed: list[Item] = []
        monument = () if data.PROGRESSIVE_MONUMENT_ITEM_NAME in self.way_out else (data.PROGRESSIVE_MONUMENT_ITEM_NAME,)
        for name in (*self.way_out, *monument, data.LEGS_ITEM_NAME):
            item = next((item for item in mine if item.name == name and all(item is not n for n in needed)), None)
            if item is not None and name in local_early:
                needed.append(item)
        if not needed:
            return

        # The early fill's own reach: the start items, and the events those open (Fill.distribute_early_items).
        state = CollectionState(self.multiworld)
        state.sweep_for_advancements(locations=[loc for loc in self.multiworld.get_filled_locations(self.player)
                                                if loc.address is None])
        room = sum(1 for loc in self.multiworld.get_unfilled_locations(self.player)
                   if loc.progress_type != LocationProgressType.EXCLUDED and loc.can_reach(state))
        if room < sum(local_early.values()):
            for name in [name for name in local_early if all(item.name != name for item in needed)]:
                del local_early[name]
        while len(needed) > room:
            item = needed.pop()
            logging.warning(f"Big Walk ({self.player_name}): no room in the starting zone for {item.name}; "
                            f"starting with it.")
            local_early[item.name] -= 1
            if not local_early[item.name]:
                del local_early[item.name]
            self.multiworld.itempool[:] = [other for other in self.multiworld.itempool if other is not item]
            self.push_precollected(item)
            self.multiworld.itempool.append(self.create_filler())  # the location it leaves

    def _place_local_items(self, group: str, contents: int, option: str) -> None:
        """
        `object_sanity_items` / `light_sanity_items` `local`: those checks take this world's own items that nothing
        needs (filler, traps, useful), placed before the fill. An item rule asking for "this world's items" on dozens of
        locations failed 1 seed in 4 at the end of a two-slot fill (2026-10-13); placing them first cannot. A world
        short of such items leaves the rest to the fill, with a warning.
        """
        if contents != bigwalk_options.ObjectSanityItems.option_local:
            return
        names = locations.LOCATION_NAME_GROUPS[group]
        spots = [loc for loc in self.multiworld.get_unfilled_locations(self.player) if loc.name in names]
        own = [item for item in self.multiworld.itempool if item.player == self.player and not item.advancement]
        self.random.shuffle(own)
        if len(own) < len(spots):
            logging.warning(f"Big Walk ({self.player_name}): {option} local: only {len(own)} items of this "
                            f"world for {len(spots)} checks; the others may hold anything.")
        placed = set()
        for location, item in zip(spots, own):
            location.place_locked_item(item)
            placed.add(id(item))
        # By identity: items compare equal by name and player, so list.remove would take another copy.
        self.multiworld.itempool[:] = [item for item in self.multiworld.itempool if id(item) not in placed]

    def create_item(self, name: str) -> items.BigWalkItem:
        return items.create_item(self, name)

    def get_filler_item_name(self) -> str:
        return items.get_random_filler_item_name(self)

    def _death_link_roulette(self) -> dict[str, int]:
        weights = self.options.death_link_trap_weights.value
        allow_hard = bool(self.options.death_link_roulette_hard_traps)
        return {
            trap.key: weights[trap.item_name]
            for trap in data.TRAPS
            if weights.get(trap.item_name, 0) > 0 and (allow_hard or not trap.hard)
        }

    def tutorial_monument_first(self) -> bool:
        """Whether the tutorial's monument takes the first gourds: every monument open, or it first in the order."""
        return not self.tower_order or self.tower_order[0] == data.TOWERS[0].prop_name

    def key_in_its_monument(self, tower: data.Tower) -> bool:
        """
        L1 `linear_towers`: whether this tower's key is in its monument (full, or the gourd drawn for it). Not the
        Drawbridge Key when the drawbridge is the only way out of the tutorial (`linear_arch_doors` and `linear_keys`)
        and the tutorial's monument does not come first: its monument would then lie past the drawbridge the key
        opens. That key stays in the pool.
        """
        if not self.options.linear_towers or self.tower_slots(tower) <= 0:
            return False
        return not (tower is data.TOWERS[0] and self.options.linear_arch_doors and self.options.linear_keys
                    and not self.options.start_with_drawbridge_open and not self.tutorial_monument_first())

    def key_location_name(self, tower: data.Tower) -> str:
        """Where `linear_towers` puts this tower's key: its monument full, or its monument key."""
        if self.options.linear_towers == bigwalk_options.LinearTowers.option_one_deposit_box:
            return data.monument_key_location_name(tower)
        return data.monument_full_location_name(tower)

    def key_gourds(self, tower: data.Tower) -> int:
        """How many gourds this tower's monument holds when its key comes (`linear_towers`)."""
        return self.monument_key_gourds.get(tower.prop_name, self.tower_slots(tower))

    def monuments_full(self) -> list[data.Tower]:
        """
        The towers whose monument full is a check: with monument_sanity or linear_towers all_deposit_boxes, those
        that have slots.
        """
        if not (self.options.monument_sanity
                or self.options.linear_towers == bigwalk_options.LinearTowers.option_all_deposit_boxes):
            return []
        return [tower for tower in data.TOWERS if self.tower_slots(tower) > 0]

    def monument_keys(self) -> list[data.Tower]:
        """The towers whose monument key is a location (`linear_towers: one_deposit_box`): those that hold their key."""
        if self.options.linear_towers != bigwalk_options.LinearTowers.option_one_deposit_box:
            return []
        return [tower for tower in data.TOWERS if self.key_in_its_monument(tower)]

    def _draw_the_monument_keys(self) -> None:
        """
        `linear_towers: one_deposit_box`: for each tower, the gourd of its monument that frees its key, between the
        first and the last slot; read back from the slot data while Universal Tracker re-generates the seed.
        """
        self.monument_key_gourds: dict[str, int] = {}
        if self.options.linear_towers != bigwalk_options.LinearTowers.option_one_deposit_box:
            return
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        slot_data = passthrough.get(self.game) if passthrough else None
        if slot_data and "monument_key_gourds" in slot_data:
            self.monument_key_gourds = dict(zip(slot_data["monument_key_keys"], slot_data["monument_key_gourds"]))
            return
        for tower in data.TOWERS:
            if self.tower_slots(tower) > 0:
                self.monument_key_gourds[tower.prop_name] = self.random.randint(1, self.tower_slots(tower))

    def puzzles(self) -> list[data.Puzzle]:
        """The puzzles that are checks: all of them, less the purple ones with `include_big_game_puzzles: disabled`."""
        puzzles = list(data.PUZZLES)
        if self.options.include_big_game_puzzles == bigwalk_options.IncludeBigGamePuzzles.option_disabled:
            puzzles = [p for p in puzzles if p.prop_name not in data.CHAIRLIFT_PUZZLES]
        return puzzles

    def lights(self) -> list[data.Light]:
        """The lights that are checks (`light_sanity`)."""
        if self.options.light_sanity == bigwalk_options.LightSanity.option_off:
            return []
        return self.lights_counted()

    def lights_counted(self) -> list[data.Light]:
        """
        The lights `big_lights` counts: those of `light_sanity`, or, with it off, the lamps, which then count for the
        objective only, with no check (player, 2026-10-13). Empty when neither asks.
        """
        mode = self.options.light_sanity
        if mode == bigwalk_options.LightSanity.option_off and "big_lights" not in self.objectives:
            return []
        return [light for light in data.LIGHTS
                if not light.switch or mode == bigwalk_options.LightSanity.option_lamps_and_switches]

    def object_location_names(self) -> list[str]:
        """Object Sanity's checks: every object counted, or every kind counted."""
        if self.options.object_sanity == bigwalk_options.ObjectSanity.option_every:
            return [obj.location_name for obj in data.ISLAND_OBJECTS if self.object_kind_counted(obj.kind)]
        if self.options.object_sanity == bigwalk_options.ObjectSanity.option_first_pickup:
            return [data.object_kind_location_name(label) for kind, label in data.OBJECT_KINDS if self.object_kind_counted(kind)]
        return []

    def objects_target(self) -> int:
        """How many objects `big_objects` asks for: `objects_required` percent, rounded up, at least one."""
        count = len(self.object_location_names())
        return max(1, -(-count * self.options.objects_required.value // 100)) if count else 0

    def ability_item_names(self) -> list[str]:
        """
        Every ability item of the pool that the logic counts, a progressive arm once per copy. Not Running: it stays
        useful (player, 2026-10-13), so the logic never holds it, and the mod does not count it either.
        """
        return [ability.item_name for ability in items.abilities_in_pool(self) for _ in range(ability.copies)
                if ability.item_name != data.RUNNING_ITEM_NAME]

    def body_target(self) -> int:
        """How many ability items `big_body` asks for: `body_required` percent, rounded up, at least one."""
        count = len(self.ability_item_names())
        return max(1, -(-count * self.options.body_required.value // 100)) if count else 0

    def lights_target(self) -> int:
        """How many lights `big_lights` asks for: `lights_required` percent of them, rounded up, at least one."""
        lights = len(self.lights_counted())
        return max(1, -(-lights * self.options.lights_required.value // 100)) if lights else 0

    def zone_locks(self) -> list[data.ZoneLock]:
        """
        `zone_locks` (R12): the zone items of this seed. The lamps' with Light Sanity, or with Object Sanity counting
        the lamps; the gadgets' with Object Sanity counting another kind.
        """
        if not self.options.zone_locks:
            return []
        objects = self.options.object_sanity != bigwalk_options.ObjectSanity.option_off
        lamps = self.options.light_sanity != bigwalk_options.LightSanity.option_off or objects and any(
            self.object_kind_counted(kind) for kind in data.LAMP_KINDS)
        gadgets = objects and any(self.object_kind_counted(kind) for kind, _ in data.OBJECT_KINDS
                                  if kind not in data.LAMP_KINDS)
        return [lock for lock in data.ZONE_LOCKS if (lamps if lock.lamps else gadgets)]

    def object_kind_counted(self, kind: str) -> bool:
        """
        Whether `object_sanity_kinds` keeps this kind (the game's name for it). With `light_sanity` the lamps are
        counted by their light, not by being picked up (player, 2026-10-13): no lamp is an object then.
        """
        if kind in data.LAMP_KINDS and self.options.light_sanity != bigwalk_options.LightSanity.option_off:
            return False
        return data.OBJECT_KIND_KEYS[kind] in self.options.object_sanity_kinds.value

    def fill_slot_data(self) -> Mapping[str, Any]:
        """
        Everything the mod needs to behave correctly without hardcoding this
        slot's settings. Documented field by field in ../protocol.md — treat
        that file and this method as one unit when changing either.
        """
        return {
            "world_version": WORLD_VERSION,

            # Choices travel as strings rather than their numeric values: the
            # mod compares strings, and a reordered enum here can then never
            # silently change what the mod does. They are the pre-rename
            # names, frozen in the *_ON_THE_WIRE tables, so renaming a YAML
            # value never needs a matching mod release.
            "goal": bigwalk_options.GOAL_ON_THE_WIRE[self._goal_for_older_mods()],
            # The objectives (ROADMAP G5, 0.5): empty for a seed that keeps the older `goal`, which
            # the mod then plays as before. `lock_endings` only means something with a list.
            "objectives": list(self.objective_keys()) if self.objectives else [],
            "lock_endings": bool(self.objectives and self.options.lock_endings_until_objectives),
            # The option itself, for Universal Tracker (lock_endings above is false without objectives).
            "lock_endings_until_objectives": bool(self.options.lock_endings_until_objectives),
            "keys_required": self.options.keys_required.value,
            # Reaching a tower (ROADMAP S8, H3, 0.5): the check, and what to hint when a tower is reached
            # (the ids of this slot's own locations, which a client can scout as hints).
            "tower_mode": self.options.tower_mode.current_key,
            "tower_order": list(self.tower_order),
            "green_dome_slots": self.tower_slots(data.tower_by_prop("bigKeyOverflow")),
            "first_tower_open": bool(self.options.first_tower_open),
            "everyone_gathers": bool(self.options.everyone_gathers),
            "monument_item_id": data.progressive_monument_id(),
            "towers_required": self.options.towers_required.value,
            "tower_sanity": bool(self.options.tower_sanity),
            "tower_reached_keys": [tower.prop_name for tower in data.reached_towers()] if self.options.tower_sanity else [],
            "tower_reached_id_offset": data.TOWER_REACHED_ID_OFFSET,
            "tower_reach_hints": self._tower_reach_hints(),
            "radio_stations_required": self.options.radio_stations_required.value,
            "deposit_goal_amount": self.deposit_goal,
            "deposit_locations": bigwalk_options.GOURD_SANITY_ON_THE_WIRE[
                self.options.gourd_sanity.current_key],
            "radio_station_checks": bool(self.options.radio_sanity),

            # The mod suppresses the game's own radio unlock only while this
            # is true. An older mod that does not read the field keeps the
            # vanilla radio and simply gets seven items it ignores, which is
            # the harmless direction for the mismatch to fall.
            "radio_station_items": bool(self.options.shuffle_radio_music),

            # The option for the tracker, and what it means for the mod: the
            # `SavableSystem` names of the doors to hold closed until their
            # item arrives. A mod too old to read it opens all three, the
            # harmless direction — the logic never needs a door shut.
            "start_with_arch_doors_open": sorted(self.options.start_with_arch_doors_open.value),
            "locked_arch_doors": [door.system_name for door in self.locked_arch_doors],
            "require_arch_doors": bool(self.options.require_arch_doors),
            "arch_door_id_offset": data.ARCH_DOOR_ID_OFFSET,

            # The Silent Gauntlet. `locked_stages` is the only value the mod acts
            # on: it removes the stairway buttons and opens a stage's collective
            # door when its item arrives, and the stage's puzzle (the
            # `GauntletChamberN` systems) is a check by plain id arithmetic.
            # `gauntlet_stage_systems` is in the order of the stage items.
            "gauntlet_mode": self.options.gauntlet_mode.current_key,
            "gauntlet_stage_items": self.options.gauntlet_stage_items.current_key,
            "gauntlet_puzzles_required": bool(self.options.gauntlet_puzzles_required),
            "lock_gauntlet_needs": bool(self.options.lock_gauntlet_needs),
            "gauntlet_stage_systems": [stage.system_name for stage in data.GAUNTLET_STAGES],
            "gauntlet_id_offset": data.GAUNTLET_ID_OFFSET,

            # The in-world teleport buttons (mod only): the mode, and the items of `items` mode,
            # by the keys the mod knows the destinations by, in the order of their item ids.
            "teleport_buttons": self.options.teleport_buttons.current_key,
            "teleport_destinations": [key for key, _ in data.TELEPORT_DESTINATIONS],
            "teleport_id_offset": data.TELEPORT_ID_OFFSET,
            "teleport_back_to_hub": bool(self.options.teleport_back_to_hub),

            # The Black Tower's door at its foot, open from the start (mod only).
            "open_black_tower": bool(self.options.open_black_tower),
            "tile_thief": self.options.tile_thief.current_key,

            # The resync stations (mod only): those of the towers, and whether guests may use them.
            "tower_resync_stations": bool(self.options.tower_resync_stations),

            # R7: the hub's board of the checks in logic, and the logic it needs (logic_export).
            "tracker_board": bool(self.options.tracker_board),
            "hint_board": bool(self.options.hint_board),
            # This slot's items and how many of each, for the hint sign's suggestions while a player writes on it
            # (those not all received yet).
            "hint_items": self._hint_items(),
            # U2: the Map Room's map as a tracker; it needs the logic too.
            "map_tracker": self.options.map_tracker.current_key,
            **({"logic": logic_export.export(self)} if self.options.tracker_board or self.options.map_tracker else {}),
            "guests_can_resync": bool(self.options.guests_can_resync),

            # The waits of the two Cabin Fever puzzles (mod only), flat: mode, seconds, help, hide_timer.
            **{
                f"{puzzle}_{field}": value
                for puzzle, wait in self.cabin_fever.items()
                for field, value in wait.items()
            },

            # The packs and fireworks as checks: the mod keeps the packs on the map, and turns a
            # pick-up (by guid) or a launcher's button (by landmark) into the location at
            # `<offset> + position` in these lists.
            # "self" with `vanilla` (the mod's own "vanilla" is the packs free on the map, which the apworld calls off).
            "pack_sanity": "self" if self.options.pack_sanity == bigwalk_options.PackSanity.option_vanilla
            else bool(self.options.pack_sanity),
            "pickup_guids": [pickup.guid for pickup in data.PICKUPS],
            "pickup_id_offset": data.PICKUP_ID_OFFSET,
            "firework_sanity": bool(self.options.firework_sanity),
            "firework_keys": [firework.key for firework in data.FIREWORKS],
            "firework_id_offset": data.FIREWORK_ID_OFFSET,
            "flare_gun_sanity": bool(self.options.flare_gun_sanity),
            "flare_gun_guids": [pickup.guid for pickup in data.FLARE_GUN_PICKUPS],
            "flare_gun_id_offset": data.FLARE_GUN_ID_OFFSET,
            # The island's gadgets as checks (ROADMAP S7): `off`, `first_pickup` (the kinds, by the game's
            # name) or `every` (by guid); with either, the mod leaves them on the map.
            "object_sanity": self.options.object_sanity.current_key,
            "object_guids": [obj.guid for obj in data.ISLAND_OBJECTS],
            "object_id_offset": data.OBJECT_ID_OFFSET,
            "object_kinds": [kind for kind, _ in data.OBJECT_KINDS],
            "object_kind_id_offset": data.OBJECT_KIND_ID_OFFSET,
            # The kinds counted, by the game's name; the mod hides the others while object_sanity is on.
            "object_sanity_kinds": [kind for kind, _ in data.OBJECT_KINDS if self.object_kind_counted(kind)],
            "object_sanity_kind_keys": sorted(self.options.object_sanity_kinds.value),
            # The lights (player, 2026-10-13): every light's key in id order (`base + light_id_offset + position`; a
            # lamp by guid, a switch by its state's path), the mode, and how many `big_lights` asks for.
            "light_sanity": self.options.light_sanity.current_key,
            "light_keys": [light.key for light in data.LIGHTS],
            "light_id_offset": data.LIGHT_ID_OFFSET,
            "lights_required": self.options.lights_required.value,
            "lights_required_count": self.lights_target(),
            "lights_counted": [light.key for light in self.lights_counted()],
            # Zone locks (R12): the gadgets each zone item keeps off the map until it arrives, by item id; the lamps
            # and switches are only out of logic, so the mod is told nothing of them.
            "zone_locks": bool(self.options.zone_locks),
            "zone_locked_gadgets": {
                str(data.zone_lock_item_id(lock)): [
                    obj.guid for obj in data.ISLAND_OBJECTS
                    if obj.kind not in data.LAMP_KINDS and self.object_kind_counted(obj.kind)
                    and data.zone_lock_for(obj.guid, obj.zone, False) == lock]
                for lock in self.zone_locks() if not lock.lamps},
            # fast_golf (R6): the mod takes the golf ball's (the cannonball's) "too heavy" off.
            "fast_golf": bool(self.options.fast_golf),
            # big_objects and big_body: how many object checks, and how many ability items, they ask for.
            "objects_required": self.options.objects_required.value,
            "objects_required_count": self.objects_target(),
            "body_required": self.options.body_required.value,
            "body_required_count": self.body_target(),

            # The seed's colors (mod only): indices into the mod's palette, buoys and flare
            # guns painted from them; empty keeps the game's own.
            "random_colors": bool(self.options.random_colors),
            "color_palette": self.color_palette,
            # The custom tiles (R8, mod only): the drawing each basic tile shows, PegTileA1 to PegTileA9; the game's own
            # names (tiles.VANILLA_TILES) for those left as they are.
            "peg_tiles": list(self.peg_tiles),
            # The drawing panels' scribbles replaced (`custom_drawings`): "<the tile kind>=<the drawing>".
            "peg_drawings": [f"{place}={name}" for place, name in sorted(self.peg_drawings.items())],
            "trap_link": bool(self.options.trap_link),

            # Traps and bonuses (mod only): each effect's key by its item id offset, how long
            # one lasts, and whether Big Trip and Big Meeting leave the Gauntlet alone.
            "effect_items": {
                str(effect.offset): effect.key for effect in (*data.TRAPS, *data.BONUSES)},
            "trap_duration": self.options.trap_duration.value,
            "traps_spare_the_gauntlet": bool(self.options.traps_spare_the_gauntlet),

            # DeathLink (mod only): the mode, what sends one and after how many, and what one
            # received does; `death_link_roulette` is the traps it may draw, by key, with weights.
            "death_link": self.options.death_link.current_key,
            "death_link_triggers": sorted(self.options.death_link_triggers.value),
            "death_link_amnesty": self.options.death_link_amnesty.value,
            "death_link_trap_on_send": bool(self.options.death_link_trap_on_send),
            "death_link_target": self.options.death_link_target.current_key,
            "death_link_effect": self.options.death_link_effect.current_key,
            "death_link_roulette": self._death_link_roulette(),

            "lock_puzzle_needs": bool(self.options.lock_puzzle_needs),
            "start_with_puzzle_needs": sorted(self.options.start_with_puzzle_needs.value),
            "start_with_random_puzzle_need": bool(self.options.start_with_random_puzzle_need),

            # Read by the mod: with `lock_puzzle_needs` on, it hides the
            # objects of every need whose item it has not received. The
            # items the player starts with arrive in the server's list like
            # any other, so `start_with_puzzle_needs` is only for the tracker.
            # The keys are in the order of their item ids (base + offset +
            # position), which is how the mod turns an id into a need.
            "puzzle_need_keys": [need.key for need in data.PUZZLE_NEEDS],
            "puzzle_need_id_offset": data.PUZZLE_NEED_ID_OFFSET,

            # `lock_abilities` (ROADMAP R3): the mode, and every ability item's key in id order. The mod
            # blocks each ability on every player's machine until its item arrives.
            "lock_abilities": self.options.lock_abilities.current_key,
            "ability_keys": [ability.key for ability in data.ABILITIES],
            "ability_id_offset": data.ABILITY_ID_OFFSET,
            "lock_running": bool(self.options.lock_running),
            "rides_require_seat": bool(self.options.rides_require_seat),
            "start_with_abilities": sorted(self.options.start_with_abilities.value),

            # The vanilla behaviours back (ROADMAP L2 to L8, 0.6). Only the purple puzzles change what the mod does:
            # it shows them on the map with `from_start` alone.
            "include_big_game_puzzles": self.options.include_big_game_puzzles.current_key,
            "cut_key_sanity": self.options.cut_key_sanity.current_key,
            "linear_keys": bool(self.options.linear_keys),
            "linear_arch_doors": bool(self.options.linear_arch_doors),
            "linear_puzzles": bool(self.options.linear_puzzles),
            # For the tracker only: the server hands these doors over as start items either way.
            "start_with_drawbridge_open": bool(self.options.start_with_drawbridge_open),
            "start_with_map_room_open": bool(self.options.start_with_map_room_open),
            # A check for each monument full (L1): the towers by their big key's prop, in id order
            # (`base + monument_full_id_offset + position in data.TOWERS`); empty when there are none.
            "monument_sanity": bool(self.options.monument_sanity),
            "linear_towers": self.options.linear_towers.current_key,
            "monument_full_keys": [tower.prop_name for tower in self.monuments_full()],
            "monument_full_id_offset": data.MONUMENT_FULL_ID_OFFSET,
            # `linear_towers: one_deposit_box`: each monument key (`base + monument_key_id_offset + position in
            # data.TOWERS`), sent once its monument holds the gourds of the same position here.
            "monument_key_keys": [tower.prop_name for tower in self.monument_keys()],
            "monument_key_gourds": [self.key_gourds(tower) for tower in self.monument_keys()],
            "monument_key_id_offset": data.MONUMENT_KEY_ID_OFFSET,

            # Not an option: it is the model this world is built on. The mod
            # stops a placed key from opening its door only while this is
            # true, and grants the feature when its item arrives instead. It
            # travels as a field rather than being assumed so that a mod too
            # old to read it keeps the vanilla doors — the harmless direction,
            # since the alternative is seven doors that can never open.
            "big_key_features": True,

            # Likewise not an option. While this is true the mod holds every
            # big key locked in its stone until the matching item arrives, so
            # filling a monument no longer releases one. A mod too old to read
            # it keeps the vanilla release and simply gets seven items it
            # ignores — the harmless direction, since the alternative is seven
            # keys that can never be obtained.
            "big_key_items": True,

            # Everything the mod needs to know which checks exist and how many
            # gourds are in circulation, without re-deriving it from options.
            "total_monument_slots": self.gourd_count,
            "deposit_location_amounts": list(self.deposit_amounts),
            "big_keys_in_play": [tower.prop_name for tower in self.towers],

            # Id arithmetic, so the mod computes ids instead of shipping a
            # copy of data.py that would drift. See ../protocol.md.
            "location_id_base": data.BASE_ID,
            "radio_id_offset": data.RADIO_ID_OFFSET,
            "deposit_id_offset": data.DEPOSIT_ID_OFFSET,
            "cut_id_offset": data.CUT_ID_OFFSET,
            "key_item_id_offset": data.KEY_ITEM_ID_OFFSET,
            "gourd_item_id": items.ITEM_NAME_TO_ID[data.GOURD_ITEM_NAME],
            "gourd_name": self.gourd_name,
        }
