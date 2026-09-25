"""The Big Walk Archipelago world."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World

from . import data, items, locations, regions, rules, web_world
from . import options as bigwalk_options

WORLD_VERSION = "0.1.0"
"""Kept in step with archipelago.json; sent in slot_data so the mod can check it."""

TRACKER_OPTIONS = (
    "goal",
    "deposit_goal_amount",
    "deposit_locations",
    "radio_station_checks",
    "radio_station_items",
)
"""
The slot_data fields that are options, and the whole of what a re-generation
needs to land on the same world.

Every other option decides what goes into the pool, not what the graph looks
like: `start_with_drawbridge_open` moves one item from the pool to the start
inventory — the server hands it over either way — and `trap_fill_percentage`
only ever picks between two kinds of filler. Neither can move a location.

Kept in step with `fill_slot_data` by a test rather than by care.
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

    origin_region_name = regions.OVERWORLD
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
    # the goalposts in silence: `deposit_locations: all` on its own invents
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
    """Gourds required to win when the goal is `deposits`."""

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

    def generate_early(self) -> None:
        self._take_options_from_tracker()

        # The Green Dome used to be optional (`green_dome_deposits`, removed
        # 2026-09-25 before the first release): its fifteen slots only ever
        # bought deposit checks, which deposit_locations already scales, and
        # leaving it out took the Hub Secret Door zone with it for nothing.
        self.towers = data.TOWERS
        self.gourd_count = data.MAX_MONUMENT_SLOTS

        self.deposit_amounts = self._pick_deposit_amounts(self.gourd_count)
        self.deposit_goal = self.options.deposit_goal_amount.value

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

        for name in TRACKER_OPTIONS:
            if name in slot_data:
                option = getattr(self.options, name)
                setattr(self.options, name, option.from_any(slot_data[name]))

    def _pick_deposit_amounts(self, total_slots: int) -> tuple[int, ...]:
        choice = self.options.deposit_locations
        if choice == bigwalk_options.DepositLocations.option_none:
            return ()
        if choice == bigwalk_options.DepositLocations.option_all:
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

    def fill_slot_data(self) -> Mapping[str, Any]:
        """
        Everything the mod needs to behave correctly without hardcoding this
        slot's settings. Documented field by field in ../protocol.md — treat
        that file and this method as one unit when changing either.
        """
        return {
            "world_version": WORLD_VERSION,

            # Choices travel as their YAML keys rather than their numeric
            # values: the mod compares strings, and a reordered enum here can
            # then never silently change what the mod does.
            "goal": self.options.goal.current_key,
            "deposit_goal_amount": self.deposit_goal,
            "deposit_locations": self.options.deposit_locations.current_key,
            "radio_station_checks": bool(self.options.radio_station_checks),

            # The mod suppresses the game's own radio unlock only while this
            # is true. An older mod that does not read the field keeps the
            # vanilla radio and simply gets seven items it ignores, which is
            # the harmless direction for the mismatch to fall.
            "radio_station_items": bool(self.options.radio_station_items),

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
        }
