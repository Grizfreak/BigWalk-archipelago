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

    - big_wall: break the bell inside the Big Wall. Needs the Big Wall
      Door.
    - big_goodbye: complete the Silent Gauntlet, behind the Big Wall. Needs
      the Big Wall Door. Saying farewell afterwards is up to you.
    - big_game: reach the secret ending behind the Spawn Secret Door. Needs
      that item, and nothing else: the sphere that normally seals it until
      you have finished the game is gone from the start.
    - big_collection: place a number of gourds in the towers' slots (see
      Gourds Required).
    """

    display_name = "Goal"
    # Named after the game's achievements, in order of base-game effort;
    # big_collection has no achievement and comes last as the one goal only
    # Archipelago offers.
    option_big_wall = 0
    option_big_goodbye = 1
    option_big_game = 2
    option_big_collection = 3
    # Every name this option has had before, so an older YAML still reads:
    # the first alpha's (which are also what the mod receives, see
    # GOAL_ON_THE_WIRE) and the short-lived ones of 2026-09-25.
    alias_ending = option_big_wall
    alias_gauntlet = option_big_goodbye
    alias_second_ending = option_big_game
    alias_secret_ending = option_big_game
    alias_deposits = option_big_collection
    alias_gourds = option_big_collection
    default = option_big_goodbye


GOAL_ON_THE_WIRE = {
    "big_wall": "ending",
    "big_goodbye": "gauntlet",
    "big_game": "second_ending",
    "big_collection": "deposits",
}
"""
The string slot_data carries for each goal, which is what the mod compares.

Frozen at the names the mod was built against: the YAML is free to follow
players' words, but a released mod must keep recognising its goal. The old
names are also aliases of the option, so the tracker reads them back.
"""


class GourdsRequired(Range):
    """
    Only relevant if the Goal is "big_collection".

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
# internal names, and numbered by their light on the dial, which carries no
# labels at all (see data.RADIO_STATIONS).
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

    - The hub's arch doors follow Lock Arch Doors, not this.
    - The Drawbridge Key Deposit stays a check either way.
    """

    display_name = "Start With Drawbridge Open"


# From the proposed options of the first alpha's players (2026-09-25), less
# its mention of `lock_map_room`, which this world does not have. The first
# door is the starting zone's other way out besides the drawbridge; the two
# far ones are shortcuts, so locking them changes the walk and not the logic.
class LockArchDoors(Choice):
    """
    Which of the hub's three arch doors start closed. Each closed door opens
    when its own item is found.

    - all: all three. You leave the starting area through the Drawbridge or
      the First Arch Door, and one of the two is always found early.
    - far: the Left and Right Arch Doors; the first one opens as usual.
    - far_left: only the Left Arch Door, towards Sports Creek.
    - far_right: only the Right Arch Door.
    - disabled: all three are open from the start.

    The Left and Right doors are shortcuts: without them the whole island is
    still reachable, the long way round.
    """

    display_name = "Lock Arch Doors"
    option_all = 0
    option_far = 1
    option_far_left = 2
    option_far_right = 3
    option_disabled = 4
    default = 1


LOCKED_ARCH_DOORS = {
    "all": data.ARCH_DOORS,
    "far": (data.LEFT_ARCH_DOOR, data.RIGHT_ARCH_DOOR),
    "far_left": (data.LEFT_ARCH_DOOR,),
    "far_right": (data.RIGHT_ARCH_DOOR,),
    "disabled": (),
}
"""The doors each value holds closed until their item arrives."""


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
    lock_arch_doors: LockArchDoors
    trap_fill_percentage: TrapFillPercentage
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, GourdsRequired]),
    OptionGroup("Gourds", [GourdSlotChecks]),
    OptionGroup("Radio", [RadioChecks, ShuffleRadioMusic]),
    OptionGroup("Keys", [StartWithDrawbridgeOpen]),
    OptionGroup("Doors", [LockArchDoors]),
]

option_presets = {
    # Everything on: the full alpha experience.
    "Full Run": {
        "goal": Goal.option_big_goodbye,
        "gourd_slot_checks": GourdSlotChecks.option_every_gourd,
        "radio_checks": True,
        "shuffle_radio_music": True,
    },
    # Trimmed down: the bell inside the Big Wall, and a check every five gourds placed.
    "Short": {
        "goal": Goal.option_big_wall,
        "gourd_slot_checks": GourdSlotChecks.option_every_5,
        "radio_checks": True,
        "shuffle_radio_music": True,
    },
}
