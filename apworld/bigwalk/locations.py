"""Location definitions for the Big Walk world."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Location

from . import data, regions
from .items import BigWalkItem
from .options import Goal

if TYPE_CHECKING:
    from .world import BigWalkWorld


# This table must list every location the world could ever create, whatever the
# options: Archipelago reads it as a static class attribute, long before any
# option is known. Which of these are actually created is decided in
# create_all_locations below.
LOCATION_NAME_TO_ID: dict[str, int] = {
    **{puzzle.location_name: data.puzzle_location_id(puzzle) for puzzle in data.PUZZLES},
    **{tower.location_name: data.key_deposit_location_id(tower) for tower in data.TOWERS},
    **{station.location_name: data.radio_location_id(station) for station in data.RADIO_STATIONS},
    **{data.cut_location_name(tower, index): data.cut_location_id(tower, index)
       for tower in data.TOWERS for index in range(tower.segments)},
    **{data.deposit_location_name(amount): data.deposit_location_id(amount)
       for amount in range(1, data.MAX_MONUMENT_SLOTS + 1)},
}

LOCATION_NAME_GROUPS: dict[str, set[str]] = {
    "Puzzles": {puzzle.location_name for puzzle in data.PUZZLES},
    "Key Deposits": {tower.location_name for tower in data.TOWERS},
    "Key Cuts": {data.cut_location_name(tower, index)
                 for tower in data.TOWERS for index in range(tower.segments)},
    "Radio Stations": {station.location_name for station in data.RADIO_STATIONS},
    "Gourd Deposits": {data.deposit_location_name(amount)
                       for amount in range(1, data.MAX_MONUMENT_SLOTS + 1)},
}

VICTORY_EVENT_NAME = "Victory"


class BigWalkLocation(Location):
    game = "Big Walk"


def gated_regions() -> dict[str, str]:
    """
    Which region each location behind a big key belongs in, keyed by location
    name. Everything not in here lives in the overworld.

    Built from data.py rather than listed here, because the membership is
    measured in game (see the "What sits behind a big key" section there) and
    two places holding the same list is how they drift apart.
    """
    mapping = {name: regions.CHAIRLIFT_ZONE for name in data.chairlift_locations()}
    mapping.update({name: regions.TUNNEL_ZONE for name in data.tunnel_locations()})
    return mapping


def create_all_locations(world: BigWalkWorld) -> None:
    overworld = world.get_region(world.origin_region_name)
    gated = gated_regions()

    def place(location_names: list[str]) -> None:
        """Puts each location in its own region, which is the overworld unless
        a big key stands between the players and it."""
        for name in location_names:
            region = world.get_region(gated.get(name, world.origin_region_name))
            region.add_locations({name: LOCATION_NAME_TO_ID[name]}, BigWalkLocation)

    place([puzzle.location_name for puzzle in data.PUZZLES])

    # A key deposit sits at the foot of its own tower, all of which are open
    # from the start: nothing in the game locks a tower's entrance, and the mod
    # opens the hub shortcuts on the first session of a save anyway. What gates
    # these locations is the gourd count in rules.py, not where they are.
    overworld.add_locations(
        {tower.location_name: LOCATION_NAME_TO_ID[tower.location_name] for tower in world.towers},
        BigWalkLocation,
    )

    # The 25 cut segments, in the overworld beside the deposit they lead to.
    #
    # VERIFIED IN PLAY (2026-09-21): every one of the five cutting trails is
    # reachable without any big key. This was the last thing in this world
    # that could still have made a seed unbeatable, and it deserved checking
    # rather than assuming — the identical claim about the puzzles turned
    # out to be false earlier the same day, and cost a seed-breaking bug.
    #
    # If a future update moves a trail, the mod's Ctrl+K dump lists every
    # `UnlockTrailStation` sorted by distance to the player, which is how
    # both this question and the chairlift one were settled: stand in the
    # gated zone, read the distances, then stand somewhere plainly open and
    # read them again. This is the line that would change.
    overworld.add_locations(
        {name: LOCATION_NAME_TO_ID[name]
         for tower in world.towers for name in data.cut_locations(tower)},
        BigWalkLocation,
    )

    if world.options.radio_station_checks:
        place([station.location_name for station in data.RADIO_STATIONS])

    overworld.add_locations(
        {data.deposit_location_name(amount): LOCATION_NAME_TO_ID[data.deposit_location_name(amount)]
         for amount in world.deposit_amounts},
        BigWalkLocation,
    )

    create_victory_event(world)


def create_victory_event(world: BigWalkWorld) -> None:
    """
    The goal is an event, not a real check: the mod reports it by sending a
    ClientStatus of GOAL rather than a location id (see ../protocol.md).

    Which region it lives in is the whole point — both bell goals sit past the
    Black Monolith Key, the second ending sits past the Hub Secret Door, and a
    deposit goal is reachable without ever going near any of them.

    `second_ending` requires the Hub Secret Door and nothing else, and that is
    now MEASURED (2026-09-22). The ending was reached and reported on a save
    whose log read `EndingGate latched: False, GauntletComplete latched:
    False` — neither bell had been rung. In vanilla the path is sealed by a
    sphere that only breaks once the game has been finished, and the mod
    disables that sphere from a save's first session
    (`SecondEndingSphereUnlocker.cs`), which is what makes the door the only
    requirement left. Walked on foot from the door and finished that way too
    (player, 2026-09-22), so nothing here rests on the debug flight that the
    first run used.
    """
    goal = world.options.goal
    if goal == Goal.option_deposits:
        region_name = regions.OVERWORLD
    elif goal == Goal.option_second_ending:
        region_name = regions.GREEN_DOME_ZONE
    else:
        region_name = regions.ENDING_ZONE

    world.get_region(region_name).add_event(
        VICTORY_EVENT_NAME,
        VICTORY_EVENT_NAME,
        location_type=BigWalkLocation,
        item_type=BigWalkItem,
    )
