"""The Big Walk Archipelago world."""

from __future__ import annotations

import logging
from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World

from . import data, items, locations, regions, rules, web_world
from . import options as bigwalk_options

logger = logging.getLogger("Big Walk")

WORLD_VERSION = "0.1.0"
"""Kept in step with archipelago.json; sent in slot_data so the mod can check it."""


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

    # --- Derived from the options, computed once in generate_early ---

    green_dome_slots: int
    """Monument slots the Green Dome tower is considered to have (0 if excluded)."""

    towers: tuple[data.Tower, ...]
    """Towers in play this slot: all seven, or six with the Green Dome excluded."""

    gourd_count: int
    """`Gourd` items in the pool; exactly enough to fill every monument in play."""

    deposit_amounts: tuple[int, ...]
    """Deposit counts that are locations, e.g. (5, 10, ..., 45)."""

    key_requirements: dict[str, int]
    """Gourds each tower's key deposit costs, keyed by big key `SaveablePropName`."""

    deposit_goal: int
    """Gourds required to win when the goal is `deposits` (clamped to what exists)."""

    def generate_early(self) -> None:
        green_dome = self.options.green_dome_deposits
        if green_dome == bigwalk_options.GreenDomeDeposits.option_excluded:
            self.green_dome_slots = 0
            self.towers = tuple(tower for tower in data.TOWERS if tower is not data.GREEN_DOME)
        else:
            self.green_dome_slots = (
                6 if green_dome == bigwalk_options.GreenDomeDeposits.option_limited
                else data.GREEN_DOME.slots
            )
            self.towers = data.TOWERS

        total_slots = sum(self.slots_for(tower) for tower in self.towers)
        self.gourd_count = total_slots

        self.deposit_amounts = self._pick_deposit_amounts(total_slots)
        self.key_requirements = rules.gourd_requirements(self)

        self.deposit_goal = min(self.options.deposit_goal_amount.value, total_slots)
        if (self.options.goal == bigwalk_options.Goal.option_deposits
                and self.deposit_goal != self.options.deposit_goal_amount.value):
            logger.warning(
                "Big Walk (%s): deposit goal lowered from %d to %d, which is every monument slot "
                "in play with the current Green Dome Deposits setting.",
                self.player_name, self.options.deposit_goal_amount.value, self.deposit_goal,
            )

    def slots_for(self, tower: data.Tower) -> int:
        """A tower's monument slot count, honouring the Green Dome option."""
        return self.green_dome_slots if tower is data.GREEN_DOME else tower.slots

    def _pick_deposit_amounts(self, total_slots: int) -> tuple[int, ...]:
        choice = self.options.deposit_locations
        if choice == bigwalk_options.DepositLocations.option_none:
            return ()
        if choice == bigwalk_options.DepositLocations.option_all:
            return tuple(range(1, total_slots + 1))

        # Milestones: every fifth deposit, plus the very last one so that
        # filling every monument always lands on a check.
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
            "green_dome_deposits": self.options.green_dome_deposits.current_key,
            "radio_station_checks": bool(self.options.radio_station_checks),

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
            "gourd_item_id": items.ITEM_NAME_TO_ID[data.GOURD_ITEM_NAME],
        }
