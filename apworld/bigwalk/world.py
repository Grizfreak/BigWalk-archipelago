"""The Big Walk Archipelago world."""

from __future__ import annotations

import logging
from collections.abc import Mapping
from typing import TYPE_CHECKING, Any, TextIO

from BaseClasses import MultiWorld
from Options import OptionError
from worlds.AutoWorld import World

from . import data, gourd_name, items, locations, regions, rules, web_world
from . import options as bigwalk_options

if TYPE_CHECKING:
    from NetUtils import MultiData

WORLD_VERSION = "0.4.0"
"""Kept in step with archipelago.json; sent in slot_data so the mod can check it."""

TRACKER_OPTIONS = {
    "goal": "goal",
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
    "pack_sanity": "pack_sanity",
    "firework_sanity": "firework_sanity",
    "flare_gun_sanity": "flare_gun_sanity",
    "random_colors": "random_colors",
    "trap_link": "trap_link",
}
"""
The slot_data fields that are options, mapped to the option each one comes
from, and the whole of what a re-generation needs to land on the same world.

The two sides have different names since the options were renamed after the
words players use (2026-09-25): slot_data kept the names the mod was built
against. Its Choice values are the pre-rename ones too, which the options
still accept as aliases, so `from_any` reads them back unchanged.

Every other option decides what goes into the pool, not what the graph looks
like: `start_with_drawbridge_open` moves one item from the pool to the start
inventory — the server hands it over either way — and `trap_fill_percentage`
only ever picks between two kinds of filler. Neither can move a location.

Kept in step with `fill_slot_data` by a test rather than by care.
"""

LEGACY_TRACKER_OPTIONS = {
    "lock_arch_doors": "lock_arch_doors",
}
"""
Fields only an older seed's slot_data carries: `lock_arch_doors` until
0.1.1, before `start_with_arch_doors_open` replaced it. Read like the ones
above so the tracker still rebuilds those seeds.
"""


class BigWalkWorld(World):
    """
    Big Walk is a co-op walking game: two or more players wander a large
    open island, solve puzzles together for gourds, carry those gourds to the
    towers' monuments, and unlock the big keys that open up the map.
    """

    game = "Big Walk"

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
        seconds, and whether the hidden help button is there. One number each since 0.4 (players,
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

        self.deposit_amounts = self._pick_deposit_amounts(self.gourd_count)
        self.cabin_fever = self._cabin_fever_waits()
        self.deposit_goal = self.options.gourds_required.value

        self._pick_a_random_puzzle_need()
        self._ask_for_buttons_early()
        self._read_the_old_arch_door_option()
        opened = self.options.start_with_arch_doors_open.value
        self.locked_arch_doors = tuple(door for door in data.ARCH_DOORS if door.item_name not in opened)
        self._ask_for_an_early_way_out()
        self._ask_for_the_far_doors_early()
        self._hint_the_keys()
        self._keep_the_gauntlet_stages_local()

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

    def _read_the_old_arch_door_option(self) -> None:
        """
        Turn a `lock_arch_doors` from a 0.1.1 YAML or seed into the
        `start_with_arch_doors_open` that replaced it, which then decides
        alone — slot_data included, so the tracker sees the new option.
        """
        old = self.options.lock_arch_doors
        if old == bigwalk_options.LockArchDoors.option_unset:
            return

        logging.warning(
            f"Big Walk: {self.player_name}'s lock_arch_doors is replaced by start_with_arch_doors_open; "
            f"reading {old.current_key} as the doors it left open.")
        locked = bigwalk_options.LOCKED_ARCH_DOORS[old.current_key]
        self.options.start_with_arch_doors_open = bigwalk_options.StartWithArchDoorsOpen(
            [door.item_name for door in data.ARCH_DOORS if door not in locked])

    def _ask_for_an_early_way_out(self) -> None:
        """
        With the first arch door locked, the starting zone holds only a
        handful of checks, and the way out — the Drawbridge or the First
        Arch Door — must be among the first things this player finds. One of
        the two, picked at random, is asked for as an early local item; if
        the Drawbridge is handed over at the start there is nothing to ask.
        """
        if data.FIRST_ARCH_DOOR not in self.locked_arch_doors or self.options.start_with_drawbridge_open:
            return

        way_out = self.random.choice((data.DRAWBRIDGE_ITEM_NAME, data.FIRST_ARCH_DOOR.item_name))
        self.multiworld.local_early_items[self.player][way_out] = 1

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

        for field, name in {**TRACKER_OPTIONS, **LEGACY_TRACKER_OPTIONS}.items():
            if field in slot_data:
                option = getattr(self.options, name)
                setattr(self.options, name, option.from_any(slot_data[field]))

        # A newer seed has no `lock_arch_doors`: one left in the tracking
        # player's YAML must not override the seed's doors.
        if "lock_arch_doors" not in slot_data:
            self.options.lock_arch_doors = bigwalk_options.LockArchDoors(bigwalk_options.LockArchDoors.option_unset)

        # A seed from before `gauntlet_stage_items` (0.3.0) has one item per stage: the
        # progressive default would ask the tracker for doors that seed never had, and the
        # Gauntlet's end would never be in logic (E2, 2026-10-05).
        if "gauntlet_stage_items" not in slot_data:
            self.options.gauntlet_stage_items = bigwalk_options.GauntletStageItems(
                bigwalk_options.GauntletStageItems.option_individual)

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
            "goal": bigwalk_options.GOAL_ON_THE_WIRE[self.options.goal.current_key],
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
            "guests_can_resync": bool(self.options.guests_can_resync),

            # The waits of the two Cabin Fever puzzles (mod only), flat: mode, seconds, help.
            **{
                f"{puzzle}_{field}": value
                for puzzle, wait in self.cabin_fever.items()
                for field, value in wait.items()
            },

            # The packs and fireworks as checks: the mod keeps the packs on the map, and turns a
            # pick-up (by guid) or a launcher's button (by landmark) into the location at
            # `<offset> + position` in these lists.
            "pack_sanity": bool(self.options.pack_sanity),
            "pickup_guids": [pickup.guid for pickup in data.PICKUPS],
            "pickup_id_offset": data.PICKUP_ID_OFFSET,
            "firework_sanity": bool(self.options.firework_sanity),
            "firework_keys": [firework.key for firework in data.FIREWORKS],
            "firework_id_offset": data.FIREWORK_ID_OFFSET,
            "flare_gun_sanity": bool(self.options.flare_gun_sanity),
            "flare_gun_guids": [pickup.guid for pickup in data.FLARE_GUN_PICKUPS],
            "flare_gun_id_offset": data.FLARE_GUN_ID_OFFSET,

            # The seed's colors (mod only): indices into the mod's palette, buoys and flare
            # guns painted from them; empty keeps the game's own.
            "random_colors": bool(self.options.random_colors),
            "color_palette": self.color_palette,
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
