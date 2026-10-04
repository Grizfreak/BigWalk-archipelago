"""Player-facing YAML options for the Big Walk world."""

from __future__ import annotations

from dataclasses import dataclass

from Options import (
    Choice, DefaultOnToggle, OptionGroup, OptionSet, PerGameCommonOptions, Range, StartInventoryPool, Toggle, Visibility,
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


class GourdSanity(Choice):
    """
    Checks for placing gourds in the towers' slots, counted across all towers together.

    - off: none.
    - every_5: a check every 5 gourds.
    - every_gourd: a check for every gourd.
    """

    display_name = "Gourd Sanity"
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


GOURD_SANITY_ON_THE_WIRE = {
    "off": "none",
    "every_5": "milestones",
    "every_gourd": "all",
}
"""The pre-rename strings slot_data keeps sending; see GOAL_ON_THE_WIRE."""


class RadioSanity(DefaultOnToggle):
    """
    Switching on each of the seven radio stations is a check.
    """

    display_name = "Radio Sanity"


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

    - The hub's arch doors follow Start With Arch Doors Open, not this.
    - The Drawbridge Key Deposit stays a check either way.
    """

    display_name = "Start With Drawbridge Open"


# From the proposed options of the first alpha's players (2026-09-25), less
# its mention of `lock_map_room`, which this world does not have. The first
# door is the starting zone's other way out besides the drawbridge; the two
# far ones are shortcuts, so locking them changes the walk and not the logic.
# A set rather than a Choice since 2026-09-26: a player asked to lock the first
# door alone, and naming every combination took eight values.
class StartWithArchDoorsOpen(OptionSet):
    """
    The hub's arch doors open from the start; the others are items to find.

    - First Arch Door: closed, it makes a real early game.
    - Left and Right Arch Doors: shortcuts; the island stays reachable without them.
    """

    display_name = "Start With Arch Doors Open"
    valid_keys = frozenset(door.item_name for door in data.ARCH_DOORS)
    default = frozenset({data.FIRST_ARCH_DOOR.item_name})


class RequireArchDoors(DefaultOnToggle):
    """
    What lies past a closed Left or Right Arch Door needs that door in logic, so you are never
    sent the shortcut after the walk.

    - Left: the Yellow, Blue and Black towers, the chapel, the Green Dome.
    - Right: the Green Tower, past the chairlift, the purple gourds.
    """

    display_name = "Require Arch Doors"


# The Choice that `StartWithArchDoorsOpen` replaced, as released in 0.1.1.
# Hidden and kept only so a YAML that sets it, or a 0.1.1 seed's slot_data in
# Universal Tracker, still gets the doors it asked for: an unknown option is
# only a warning to Archipelago, and would silently fall back to the default.
class LockArchDoors(Choice):
    """Replaced by Start With Arch Doors Open."""

    display_name = "Lock Arch Doors"
    option_all = 0
    option_far = 1
    option_far_left = 2
    option_far_right = 3
    option_disabled = 4
    option_unset = 5
    default = option_unset
    visibility = Visibility.none


LOCKED_ARCH_DOORS = {
    "all": data.ARCH_DOORS,
    "far": (data.LEFT_ARCH_DOOR, data.RIGHT_ARCH_DOOR),
    "far_left": (data.LEFT_ARCH_DOOR,),
    "far_right": (data.RIGHT_ARCH_DOOR,),
    "disabled": (),
}
"""The doors each value of the old `lock_arch_doors` held closed until their item arrived."""


class LockPuzzleNeeds(DefaultOnToggle):
    """
    What puzzles are built from (buttons, panels, speakers, timers...) are items to find first.
    The parts missing are hidden in the world; Start With Puzzle Needs lists those you already have.
    """

    display_name = "Lock Puzzle Needs"


class StartWithPuzzleNeeds(OptionSet):
    """
    Which puzzle needs you already have when Lock Puzzle Needs is on. Every
    need not listed is an item to find.

    Needs: buttons, sync_buttons, icon_panels, drawing_panels, pose_panels,
    sound_panels, point_panels, speakers, lights, teapots, timed_tomato,
    golf_ball, big_head, ink_viewer, counter, coordinates_computer, eggs.
    """

    display_name = "Start With Puzzle Needs"
    valid_keys = data.PUZZLE_NEED_KEYS
    default = frozenset()


class StartWithRandomPuzzleNeed(DefaultOnToggle):
    """
    With Lock Puzzle Needs on, start with one random need, so that at least
    one of the tutorial's puzzles is doable from the first minute. Nothing is
    added if Start With Puzzle Needs already allows one.

    The tutorial's puzzles need buttons or synchronized buttons, so it is one
    of those two. Turning this off is only possible when Start With Puzzle
    Needs lists one of them: a seed with nothing to do at the start cannot be
    generated.
    """

    display_name = "Start With Random Puzzle Need"


class HintKeys(Choice):
    """
    Whether the server tells you, from the first minute, where the seven big
    keys are in the multiworld.

    - off: nothing is hinted.
    - from_start: the seven Big Key items are hinted at the start, as if
      they were listed in Start Hints. Anything you list there stays.
    """

    display_name = "Hint Keys"
    option_off = 0
    option_from_start = 1
    default = option_off


class GauntletMode(Choice):
    """
    What the Silent Gauntlet is, behind the Big Wall.

    - vanilla: nothing changes, and it has no checks. Holding the stairway
      buttons together opens the way up, as in the game.
    - locked_stages: the stairway buttons are gone and each stage's way up is
      an item. Solving a stage's puzzle is a check and still opens the wall
      inside the stage, but you only leave it with that stage's item. 7 checks,
      7 items.

    Whatever the goal. With Big Goodbye the seven items are needed to win. With
    another goal the Gauntlet is extra: its checks sit behind the chapel, so with
    the usual accessibility (full) the Black Monolith Key and the seven items
    are always findable, but the goal itself asks for none of them.
    """

    display_name = "Gauntlet Mode"
    option_vanilla = 0
    option_locked_stages = 1
    default = option_vanilla


class GauntletPuzzlesRequired(DefaultOnToggle):
    """
    With Gauntlet Mode locked_stages: whether a stage's puzzle has to be solved
    to leave the stage.

    - On: solving the puzzle opens the wall inside the stage and is a check;
      the stage's item opens the stairway. You leave a stage by doing both.
    - Off: the stage's item opens the wall and the stairway at once. You may
      do the puzzles in any order, or none: with all seven items you can walk
      straight to the top. The puzzles are still checks.
    """

    display_name = "Gauntlet Puzzles Required"


class LockGauntletNeeds(DefaultOnToggle):
    """
    With Lock Puzzle Needs: whether the parts of the seven stages' puzzles in the
    Silent Gauntlet are items too, in either Gauntlet Mode.

    - On: the buttons, panels and so on inside the Gauntlet's stages are not on
      the map until their item arrives, and the Big Goodbye goal, which means
      finishing the Gauntlet, asks for them. With Gauntlet Mode locked_stages the
      stages' checks ask for them too. The finale and the entrance are left alone.
    - Off: the Gauntlet's parts stay on the map and nothing is asked for them.
    """

    display_name = "Lock Gauntlet Needs"


class GauntletStagesLocal(DefaultOnToggle):
    """
    With Gauntlet Mode locked_stages: keep the seven stage items in your own
    world, so that a stage is never held up by another game's item.
    """

    display_name = "Gauntlet Stages Local"


class TeleportBackToHub(Toggle):
    """
    With Teleport Buttons, a button back to the hub in each destination.
    """

    display_name = "Teleport Back to Hub"


class TowerResyncStations(Toggle):
    """
    A resync station in each of the five towers: a button that brings your gourds, keys and
    gadgets lying around back in front of it. The hub's station is always there.
    """

    display_name = "Tower Resync Stations"


class GuestsCanResync(Toggle):
    """
    The guests may use the resync stations too. Off, only the host can.
    """

    display_name = "Guests Can Resync"


class OpenBlackTower(DefaultOnToggle):
    """
    The Black Tower's door is open from the start, instead of waiting for the five monuments.
    """

    display_name = "Open Black Tower"


class TeleportButtons(Choice):
    """
    Buttons in the hub that teleport you to the Red, Green, Blue, Yellow and Black towers and the
    Silent Gauntlet.

    - off: none.
    - free: all there from the start.
    - with_towers: once the place has been opened on foot (the tower's button, the chapel's).
    - items: the same, and each also needs its Teleporter item (6 items).
    """

    display_name = "Teleport Buttons"
    option_off = 0
    option_free = 1
    option_with_towers = 2
    option_items = 3
    default = option_off


class CabinFeverTime(Choice):
    """
    The wait of the Cabin Fever puzzle (5 minutes in the game). Mod only; it changes no logic.

    - vanilla: the game's own wait.
    - reduced: down to `cabin_fever_seconds_min`, never longer than the game's.
    - random_between: a time between `cabin_fever_seconds_min` and `cabin_fever_seconds_max`, drawn once
      when the world is made, so it is the same for every player.
    - fixed: exactly `cabin_fever_seconds` seconds, even longer than the game's.
    """

    display_name = "Cabin Fever Wait"
    option_vanilla = 0
    option_reduced = 1
    option_random_between = 2
    option_fixed = 3
    default = option_vanilla


class CabinFeverSecondsMin(Range):
    """The shortest wait of the Cabin Fever puzzle, in seconds: what `reduced` brings it down to, and the low end of `random_between`."""

    display_name = "Cabin Fever Shortest Wait"
    range_start = 0
    range_end = 300
    default = 60


class CabinFeverSecondsMax(Range):
    """The longest wait of the Cabin Fever puzzle, in seconds: the high end of `random_between`."""

    display_name = "Cabin Fever Longest Wait"
    range_start = 0
    range_end = 3600
    default = 300


class CabinFeverSeconds(Range):
    """The wait of the Cabin Fever puzzle, in seconds, when it is `fixed`."""

    display_name = "Cabin Fever Fixed Wait"
    range_start = 0
    range_end = 3600
    default = 120


class CabinFeverHelp(Toggle):
    """
    A hidden button in the Cabin Fever room that takes ten seconds off the wait each time it is
    pressed. Mod only.
    """

    display_name = "Cabin Fever Help Button"


class CabinFeverLongTime(Choice):
    """
    The wait of the Cabin Fever Long puzzle (30 minutes in the game). Mod only; it changes no logic.

    - vanilla: the game's own wait.
    - reduced: down to `cabin_fever_long_seconds_min`, never longer than the game's.
    - random_between: a time between `cabin_fever_long_seconds_min` and `cabin_fever_long_seconds_max`, drawn once
      when the world is made, so it is the same for every player.
    - fixed: exactly `cabin_fever_long_seconds` seconds, even longer than the game's.
    """

    display_name = "Cabin Fever Long Wait"
    option_vanilla = 0
    option_reduced = 1
    option_random_between = 2
    option_fixed = 3
    default = option_vanilla


class CabinFeverLongSecondsMin(Range):
    """The shortest wait of the Cabin Fever Long puzzle, in seconds: what `reduced` brings it down to, and the low end of `random_between`."""

    display_name = "Cabin Fever Long Shortest Wait"
    range_start = 0
    range_end = 1800
    default = 300


class CabinFeverLongSecondsMax(Range):
    """The longest wait of the Cabin Fever Long puzzle, in seconds: the high end of `random_between`."""

    display_name = "Cabin Fever Long Longest Wait"
    range_start = 0
    range_end = 3600
    default = 1800


class CabinFeverLongSeconds(Range):
    """The wait of the Cabin Fever Long puzzle, in seconds, when it is `fixed`."""

    display_name = "Cabin Fever Long Fixed Wait"
    range_start = 0
    range_end = 3600
    default = 600


class CabinFeverLongHelp(Toggle):
    """
    A hidden button in the Cabin Fever Long room that takes ten seconds off the wait each time it is
    pressed. Mod only.
    """

    display_name = "Cabin Fever Long Help Button"


class TileThief(Choice):
    """
    A button beside the Tile Thief puzzle that makes peg tiles in front of it.

    - vanilla: no button.
    - easy: the tiles the puzzle expects.
    - chaos: one tile of every kind the puzzle draws from; the answer is still yours to find.

    With Lock Puzzle Needs, it waits for the panels Tile Thief is built from.
    """

    display_name = "Tile Thief"
    option_vanilla = 0
    option_easy = 1
    option_chaos = 2
    default = option_vanilla


class ShufflePegTiles(Toggle):
    """
    The island's peg tiles swap places on their stands, within each place, differently for each
    seed. A tile never leaves its place, so nothing becomes harder to reach.
    """

    display_name = "Shuffle Peg Tiles"


class JokeFillerPercentage(Range):
    """
    Share of the filler items that are jokes: the community's thirteen wrong names for a
    Gourd (Baby, Boid, Bouba, Plumbus, Thing...). They do nothing beyond a line in the
    item feed.

    - 0: none.
    - 100: every filler item that is not a prop of the island is one.
    """

    display_name = "Joke Filler Percentage"
    range_start = 0
    range_end = 100
    default = 0
    # Back with the traps and the bonuses (0.1.4): hidden and off until then (player, 2026-10-05).
    visibility = Visibility.none


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
    gourd_sanity: GourdSanity
    radio_sanity: RadioSanity
    shuffle_radio_music: ShuffleRadioMusic
    start_with_drawbridge_open: StartWithDrawbridgeOpen
    start_with_arch_doors_open: StartWithArchDoorsOpen
    require_arch_doors: RequireArchDoors
    lock_arch_doors: LockArchDoors
    lock_puzzle_needs: LockPuzzleNeeds
    start_with_puzzle_needs: StartWithPuzzleNeeds
    start_with_random_puzzle_need: StartWithRandomPuzzleNeed
    hint_keys: HintKeys
    joke_filler_percentage: JokeFillerPercentage
    open_black_tower: OpenBlackTower
    tower_resync_stations: TowerResyncStations
    guests_can_resync: GuestsCanResync
    teleport_buttons: TeleportButtons
    teleport_back_to_hub: TeleportBackToHub
    cabin_fever_time: CabinFeverTime
    cabin_fever_seconds_min: CabinFeverSecondsMin
    cabin_fever_seconds_max: CabinFeverSecondsMax
    cabin_fever_seconds: CabinFeverSeconds
    cabin_fever_help: CabinFeverHelp
    cabin_fever_long_time: CabinFeverLongTime
    cabin_fever_long_seconds_min: CabinFeverLongSecondsMin
    cabin_fever_long_seconds_max: CabinFeverLongSecondsMax
    cabin_fever_long_seconds: CabinFeverLongSeconds
    cabin_fever_long_help: CabinFeverLongHelp
    tile_thief: TileThief
    shuffle_peg_tiles: ShufflePegTiles
    gauntlet_mode: GauntletMode
    gauntlet_puzzles_required: GauntletPuzzlesRequired
    lock_gauntlet_needs: LockGauntletNeeds
    gauntlet_stages_local: GauntletStagesLocal
    trap_fill_percentage: TrapFillPercentage
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, GourdsRequired]),
    OptionGroup("Sanity", [GourdSanity, RadioSanity, ShuffleRadioMusic]),
    OptionGroup("Logic", [
        StartWithDrawbridgeOpen, OpenBlackTower, StartWithArchDoorsOpen, RequireArchDoors,
        GauntletMode, GauntletPuzzlesRequired, LockGauntletNeeds, GauntletStagesLocal,
        LockPuzzleNeeds, StartWithPuzzleNeeds, StartWithRandomPuzzleNeed,
    ]),
    OptionGroup("QoL", [HintKeys, TeleportButtons, TeleportBackToHub, TowerResyncStations, GuestsCanResync]),
    OptionGroup("Puzzle QoL", [
        CabinFeverTime, CabinFeverSecondsMin, CabinFeverSecondsMax, CabinFeverSeconds, CabinFeverHelp,
        CabinFeverLongTime, CabinFeverLongSecondsMin, CabinFeverLongSecondsMax, CabinFeverLongSeconds,
        CabinFeverLongHelp, TileThief, ShufflePegTiles,
    ]),
]

option_presets = {
    # Everything on: the full alpha experience.
    "Full Run": {
        "goal": Goal.option_big_goodbye,
        "gourd_sanity": GourdSanity.option_every_gourd,
        "radio_sanity": True,
        "shuffle_radio_music": True,
    },
    # Trimmed down: the bell inside the Big Wall, and a check every five gourds placed.
    "Short": {
        "goal": Goal.option_big_wall,
        "gourd_sanity": GourdSanity.option_every_5,
        "radio_sanity": True,
        "shuffle_radio_music": True,
    },
}
