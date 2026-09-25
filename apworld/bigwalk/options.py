"""Player-facing YAML options for the Big Walk world."""

from __future__ import annotations

from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, StartInventoryPool, Toggle

from . import data


class Goal(Choice):
    """
    What you need to do to win. No goal can be done solo.

    - gauntlet: break the bell at the top of the Gauntlet. Needs the Black Monolith Key.
    - ending: break the chapel bell. Needs the Black Monolith Key.
    - second_ending: reach the ending behind the Hub Secret Door. Needs that item;
      the sphere is gone from the start, so this is not postgame.
    - deposits: deposit a number of gourds into monuments (see Deposit Goal Amount).

    second_ending does not work with green_dome_deposits: excluded, which becomes key_only.
    """

    display_name = "Goal"
    option_gauntlet = 0
    option_ending = 1
    option_deposits = 2
    option_second_ending = 3
    default = 0


class DepositGoalAmount(Range):
    """
    Gourds to deposit to win, with goal: deposits.

    - Ignored by every other goal.
    - Lowered to the number of monument slots in play if set higher.
    """

    display_name = "Deposit Goal Amount"
    range_start = 5
    range_end = data.MAX_MONUMENT_SLOTS
    default = 30


# key_only exists because a filled monument no longer releases anything: the
# only reason to deposit fifteen more gourds is the deposit checks themselves.
# It is also the way to play goal: second_ending without that grind.
class GreenDomeDeposits(Choice):
    """
    Whether the Green Dome, the 15-slot postgame tower, takes part.

    - full: everything stays. 45 gourds in the pool.
    - key_only: its 15 slots leave; the Hub Secret Door, its key and key deposit stay. 30 gourds.
    - excluded: the Hub Secret Door, its key and key deposit leave too. 30 gourds.
    """

    display_name = "Green Dome Deposits"
    option_full = 0
    option_excluded = 2
    # 1 was `limited`, removed on 2026-09-15 because it claimed the Green
    # Dome monument could be completed with six gourds and nothing made that
    # true (see ../design-decisions.md). Not reused: a YAML written with the
    # numeric value would silently mean something else.
    option_key_only = 3
    default = 0


class DepositLocations(Choice):
    """
    Checks for depositing gourds, counted across all monuments together.

    - none: no deposit checks.
    - milestones: a check every 5 gourds, plus the last one.
    - all: a check for every gourd.
    """

    display_name = "Deposit Locations"
    option_none = 0
    option_milestones = 1
    option_all = 2
    default = 1


class RadioStationChecks(DefaultOnToggle):
    """Turning on each of the seven radio stations awards a check."""

    display_name = "Radio Station Checks"


# Stations are named after the music they actually play, not the game's
# internal names; the dial shows numbers only, so nothing contradicts that.
class RadioStationItems(DefaultOnToggle):
    """
    Shuffle each station's music into the item pool.

    - On: switching a station on is still a check, but its music only plays
      once its Radio Music item arrives. 7 items, replacing filler.
    - Off: vanilla, a station plays as soon as it is switched on.
    """

    display_name = "Radio Station Items"


# Off by default since 2026-09-22: the drawbridge turned out not to be the only
# way out of the tutorial (the mod opens the hub's arch doors on a save's first
# session), so shuffling it can no longer lock anyone in.
class StartWithDrawbridgeOpen(Toggle):
    """
    Start with the Drawbridge open instead of finding it in the multiworld.

    - The tutorial key deposit stays a check either way.
    """

    display_name = "Start With Drawbridge Open"


class TrapFillPercentage(Range):
    """
    Percentage of filler items replaced by traps.

    - Traps do nothing yet: keep it at 0.
    """

    display_name = "Trap Fill Percentage"
    range_start = 0
    range_end = 100
    default = 0


@dataclass
class BigWalkOptions(PerGameCommonOptions):
    goal: Goal
    deposit_goal_amount: DepositGoalAmount
    green_dome_deposits: GreenDomeDeposits
    deposit_locations: DepositLocations
    radio_station_checks: RadioStationChecks
    radio_station_items: RadioStationItems
    start_with_drawbridge_open: StartWithDrawbridgeOpen
    trap_fill_percentage: TrapFillPercentage
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, DepositGoalAmount]),
    OptionGroup("Locations", [DepositLocations, RadioStationChecks]),
    OptionGroup("Radio", [RadioStationItems]),
    OptionGroup("Gourds and Keys", [GreenDomeDeposits, StartWithDrawbridgeOpen]),
]

option_presets = {
    # Everything on: the full alpha experience.
    "Full Run": {
        "goal": Goal.option_gauntlet,
        "green_dome_deposits": GreenDomeDeposits.option_full,
        "deposit_locations": DepositLocations.option_all,
        "radio_station_checks": True,
        "radio_station_items": True,
    },
    # Trimmed down: no postgame tower, fewer deposits to grind out.
    "Short": {
        "goal": Goal.option_ending,
        "green_dome_deposits": GreenDomeDeposits.option_excluded,
        "deposit_locations": DepositLocations.option_milestones,
        "radio_station_checks": True,
        "radio_station_items": True,
    },
}
