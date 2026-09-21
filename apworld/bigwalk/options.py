"""Player-facing YAML options for the Big Walk world."""

from __future__ import annotations

from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, StartInventoryPool

from . import data


class Goal(Choice):
    """
    The victory condition for this slot.

    - gauntlet: break the bell at the top of the Gauntlet, the game's true
      ending. Requires the Black Monolith Key, which opens the way to the
      chapel, the field behind it and the Gauntlet.
    - ending: break the chapel bell. Also requires the Black Monolith Key, but
      stops well short of the Gauntlet's seven puzzle chambers.
    - deposits: deposit a number of gourds into the towers' monuments (see
      Deposit Goal Amount). Does not require reaching the ending at all.

    Both bells need two players pressing two buttons at once. Big Walk's
    monument deposits are a two-player action as well, so no goal here is
    solo-friendly.
    """

    display_name = "Goal"
    option_gauntlet = 0
    option_ending = 1
    option_deposits = 2
    default = 0


class DepositGoalAmount(Range):
    """
    How many gourds must be deposited into monuments to win, when Goal is set
    to "deposits". Ignored for every other goal.

    Clamped down to the number of monument slots actually in play if you ask
    for more than exist (see Green Dome Deposits).
    """

    display_name = "Deposit Goal Amount"
    range_start = 5
    range_end = data.MAX_MONUMENT_SLOTS
    default = 30


class GreenDomeDeposits(Choice):
    """
    Whether the Green Dome tower takes part. It is the postgame tower, and by
    far the biggest: 15 deposit slots against 4-6 everywhere else.

    - full: it takes part. Its key is in the pool, its deposit is a location,
      and the pool holds 45 gourds.
    - excluded: its key and its deposit location leave the world entirely,
      for 30 gourds. The way to cut the grind.
    """

    display_name = "Green Dome Deposits"
    option_full = 0
    option_excluded = 2
    default = 0


class DepositLocations(Choice):
    """
    Whether depositing gourds into monuments awards checks of its own, and how
    often. Deposits are counted globally across every monument, so it never
    matters which tower a gourd goes into.

    - none: no deposit checks at all.
    - milestones: a check every 5 gourds deposited, plus one for the last one.
    - all: a check for every single gourd deposited.
    """

    display_name = "Deposit Locations"
    option_none = 0
    option_milestones = 1
    option_all = 2
    default = 1


class RadioStationChecks(DefaultOnToggle):
    """Turning on each of the seven radio stations awards a check."""

    display_name = "Radio Station Checks"


class RadioStationItems(DefaultOnToggle):
    """
    Shuffle the radio music itself into the item pool.

    Switching a station on in the game still awards its check, but the music
    only starts playing once the matching Radio Music item arrives — so the
    radio behaves like every other check in this world instead of rewarding
    itself. Seven Radio Music items enter the pool, replacing seven filler.

    Stations are named after the music they actually play, which is not what
    the game's internal names suggest. The radio dial shows numbers and no
    names at all, so there is nothing in the world to contradict.

    Turn this off for the vanilla radio, where switching a station on unlocks
    its music on the spot.

    Only the Archipelago host is affected. Nobody else in the session runs the
    Archipelago client, so a guest hears a station the moment it is switched
    on, whether or not the host has received it.
    """

    display_name = "Radio Station Items"


class StartWithTutorialKey(DefaultOnToggle):
    """
    Start with the Tutorial Key instead of shuffling it into the pool.

    The drawbridge this key opens is believed to be what lets you leave the
    tutorial area. If that is true and the key is shuffled, a seed can lock you
    in the tutorial with nothing to do, so the safe default is to hand it over
    up front. Turn this off only once you have confirmed you can walk out of
    the tutorial without it.
    """

    display_name = "Start With Tutorial Key"


class TrapFillPercentage(Range):
    """
    Percentage of the filler items in this slot's pool replaced by traps.

    Defaults to 0 because the mod does not implement any trap effect yet: a
    trap sent today arrives and does nothing at all.
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
    start_with_tutorial_key: StartWithTutorialKey
    trap_fill_percentage: TrapFillPercentage
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, DepositGoalAmount]),
    OptionGroup("Locations", [DepositLocations, RadioStationChecks]),
    OptionGroup("Radio", [RadioStationItems]),
    OptionGroup("Gourds and Keys", [GreenDomeDeposits, StartWithTutorialKey]),
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
