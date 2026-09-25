"""Player-facing YAML options for the Big Walk world."""

from __future__ import annotations

from dataclasses import dataclass

from Options import (
    Choice, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, StartInventoryPool, Toggle, Visibility,
)

from . import data


class Goal(Choice):
    """
    What you need to do to win. No goal can be done solo.

    - gauntlet: pass behind the big green wall and finish the game. Needs
      the Chapel Door.
    - ending: get in the big green wall. Needs the Chapel Door.
    - secret_ending: reach the secret ending behind the Hub Secret Door.
      Needs that item, and nothing else: the sphere that normally seals it
      until you have finished the game is gone from the start.
    - gourds: place a number of gourds in the towers' slots (see Gourds
      Required).
    """

    display_name = "Goal"
    option_gauntlet = 0
    option_ending = 1
    option_gourds = 2
    option_secret_ending = 3
    # The names this option had before the 2026-09-25 rename, so a YAML
    # written for the first alpha still reads. They are also what the mod
    # receives: see GOAL_ON_THE_WIRE.
    alias_deposits = option_gourds
    alias_second_ending = option_secret_ending
    default = 0


GOAL_ON_THE_WIRE = {
    "gauntlet": "gauntlet",
    "ending": "ending",
    "gourds": "deposits",
    "secret_ending": "second_ending",
}
"""
The string slot_data carries for each goal, which is what the mod compares.

Frozen at the names the mod was built against: the YAML is free to follow
players' words, but a released mod must keep recognising its goal. The old
names are also aliases of the option, so the tracker reads them back.
"""


class GourdsRequired(Range):
    """
    Only relevant if the Goal is "gourds".

    How many gourds must be placed in the towers' slots to win.
    """

    display_name = "Gourds Required"
    range_start = 5
    range_end = data.MAX_MONUMENT_SLOTS
    default = 30


class GourdSlotChecks(Choice):
    """
    Checks for placing gourds in the towers' slots, counted across all
    towers together.

    - off: no checks for placing gourds.
    - every_5: a check every 5 gourds.
    - every_gourd: a check for every gourd.
    """

    display_name = "Gourd Slot Checks"
    option_off = 0
    option_every_5 = 1
    option_every_gourd = 2
    # Pre-rename names, kept readable and still sent to the mod.
    alias_none = option_off
    alias_milestones = option_every_5
    alias_all = option_every_gourd
    # A bare `on` / `true` in a YAML reads as a boolean; take it to mean the
    # default rather than refuse it. (`off` / `false` already land on 0.)
    alias_on = option_every_5
    alias_true = option_every_5
    default = 1


GOURD_SLOT_CHECKS_ON_THE_WIRE = {
    "off": "none",
    "every_5": "milestones",
    "every_gourd": "all",
}
"""The pre-rename strings slot_data keeps sending; see GOAL_ON_THE_WIRE."""


class RadioChecks(DefaultOnToggle):
    """If on, switching on each of the seven radio stations is a check."""

    display_name = "Radio Checks"


# Stations are named after the music they actually play, not the game's
# internal names; the dial shows numbers only, so nothing contradicts that.
class ShuffleRadioMusic(DefaultOnToggle):
    """
    If on, each station's music is an item, and a station stays silent until
    its music is found.

    - On: switching a station on is still a check, but its music only plays
      once its Radio Music item arrives. 7 items, replacing filler.
    - Off: vanilla, a station plays as soon as it is switched on.
    """

    display_name = "Shuffle Radio Music"


# Off by default since 2026-09-22: the drawbridge turned out not to be the only
# way out of the tutorial (the mod opens the hub's arch doors on a save's first
# session), so shuffling it can no longer lock anyone in.
class StartWithDrawbridgeOpen(Toggle):
    """
    If on, you start with the Drawbridge open instead of finding it in the
    multiworld.

    - Doors at the hub will be opened on start no matter what.
    - The Drawbridge Key Deposit stays a check either way.
    """

    display_name = "Start With Drawbridge Open"


# Hidden until the mod gives a trap an effect: today a trap is a filler item
# with a different name, and offering the option would promise otherwise.
class TrapFillPercentage(Range):
    """
    Percentage of filler items replaced by traps.

    - Traps do nothing yet: keep it at 0.
    """

    display_name = "Trap Fill Percentage"
    range_start = 0
    range_end = 100
    default = 0
    visibility = Visibility.none


@dataclass
class BigWalkOptions(PerGameCommonOptions):
    goal: Goal
    gourds_required: GourdsRequired
    gourd_slot_checks: GourdSlotChecks
    radio_checks: RadioChecks
    shuffle_radio_music: ShuffleRadioMusic
    start_with_drawbridge_open: StartWithDrawbridgeOpen
    trap_fill_percentage: TrapFillPercentage
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, GourdsRequired]),
    OptionGroup("Gourds", [GourdSlotChecks]),
    OptionGroup("Radio", [RadioChecks, ShuffleRadioMusic]),
    OptionGroup("Keys", [StartWithDrawbridgeOpen]),
]

option_presets = {
    # Everything on: the full alpha experience.
    "Full Run": {
        "goal": Goal.option_gauntlet,
        "gourd_slot_checks": GourdSlotChecks.option_every_gourd,
        "radio_checks": True,
        "shuffle_radio_music": True,
    },
    # Trimmed down: the chapel bell, and a check every five gourds placed.
    "Short": {
        "goal": Goal.option_ending,
        "gourd_slot_checks": GourdSlotChecks.option_every_5,
        "radio_checks": True,
        "shuffle_radio_music": True,
    },
}
