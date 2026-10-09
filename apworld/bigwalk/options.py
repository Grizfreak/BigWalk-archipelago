"""Player-facing YAML options for the Big Walk world."""

from __future__ import annotations

from dataclasses import dataclass

from Options import (
    Choice, DefaultOnToggle, FreeText, OptionCounter, OptionGroup, OptionSet, PerGameCommonOptions, Range, StartInventoryPool, Toggle, Visibility,
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
class PackSanity(DefaultOnToggle):
    """
    The island's backpacks, belts and gourd carton (12 in all) as checks.

    - on: picking each one up for the first time is a check (12 locations); the pack then
      disappears from the map, so the packs a player can wear are still the items of the pool.
    - off: as in the game, free to take on the map, and no location.
    """

    display_name = "Pack Sanity"


class FireworkSanity(DefaultOnToggle):
    """
    Firing each of the eight firework launchers (the button at its foot) for the first time is a
    check: 8 locations.
    """

    display_name = "Firework Sanity"


class FlareGunSanity(DefaultOnToggle):
    """
    Picking up each of the island's four flare guns for the first time is a check: 4 locations. The
    four flare guns are then items in the pool (how many of each: `island_object_limits`; with
    `random_colors`, painted from the seed's colors). Off: the flare guns stay on the island and
    none is an item. The Rainbow Flare Gun is an item either way (`island_object_limits`).
    """

    display_name = "Flare Gun Sanity"


class GourdName(FreeText):
    """
    Renames the gourds for the run (24 characters max), everywhere: clients, hints, spoiler, overlay.
    All Big Walk slots must use the same name, and it can't be another item's or group's name;
    otherwise it stays "Gourd".
    """

    display_name = "Gourd Name"
    default = "Gourd"


class RandomColors(DefaultOnToggle):
    """
    The island's buoys (their body, halo and light) and flare guns (and their shots) take colors
    drawn for the seed, between five and ten of them, the same on every player's screen. It paints
    objects of the map only: never the players, the gourds or the keys. Off: the game's own colors.
    The Rainbow Flare Gun, whose shots run through every color, is not affected by it.
    """

    display_name = "Random Colors"


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

    - Left: the Yellow, Blue and Black towers, the Green Dome. Never the chapel: the end of the
      game needs only the Big Wall Door.
    - Right: the Green Tower, past the chairlift, the purple gourds.

    The doors are shortcuts, not walls: the island can be walked round them. But until a door
    arrives, what lies past it is out of logic: its checks are never expected of you, and a
    tracker shows them, and the goal, as out of reach even once you have walked there.
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


class GauntletStageItems(Choice):
    """
    With Gauntlet Mode locked_stages, what the seven stage items are.

    - progressive: seven Progressive Gauntlet Doors; each one opens the next stage up.
    - individual: one item per stage, Gauntlet Stage 1 Door to Stage 7 Door, found in any order.
    """

    display_name = "Gauntlet Stage Items"
    option_progressive = 0
    option_individual = 1
    default = option_progressive


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


class CabinFeverSeconds(Range):
    """
    The wait of the Cabin Fever puzzle, in seconds; 300 (5 minutes) is the game's own. Mod only; it
    changes no logic. For a wait drawn once per seed, Archipelago's own `random-range-60-300`.
    """

    display_name = "Cabin Fever Wait"
    range_start = 0
    range_end = 3600
    default = 300


class CabinFeverHelp(Toggle):
    """
    A hidden button in the Cabin Fever room that takes ten seconds off the wait each time it is
    pressed. Mod only.
    """

    display_name = "Cabin Fever Help Button"


class CabinFeverLongSeconds(Range):
    """
    The wait of the Cabin Fever Long puzzle, in seconds; 1800 (30 minutes) is the game's own. Mod
    only; it changes no logic. For a wait drawn once per seed, `random-range-300-1800`.
    """

    display_name = "Cabin Fever Long Wait"
    range_start = 0
    range_end = 3600
    default = 1800


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


class BackpacksInPool(Range):
    """
    How many Backpacks the item pool holds. Each one takes the place of a filler item; a pool
    without that much room holds fewer.
    """

    display_name = "Backpacks in Pool"
    range_start = 0
    range_end = 8
    default = 2


class BeltsInPool(Range):
    """
    How many Belts the item pool holds. Each one takes the place of a filler item; a pool
    without that much room holds fewer.
    """

    display_name = "Belts in Pool"
    range_start = 0
    range_end = 8
    default = 2


class GourdCartonsInPool(Range):
    """
    How many Gourd Cartons the item pool holds. Each one takes the place of a filler item; a
    pool without that much room holds fewer.
    """

    display_name = "Gourd Cartons in Pool"
    range_start = 0
    range_end = 4
    default = 1


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


class TrapFillPercentage(Range):
    """
    Percentage of filler items replaced by traps. A trap hits every player of the session, for
    `trap_duration` seconds when it lasts. Which traps, and how often each: `trap_weights`.
    """

    display_name = "Trap Fill Percentage"
    range_start = 0
    range_end = 100
    default = 0


class TrapWeights(OptionCounter):
    """
    How often each trap comes, against the others. 0 takes a trap out of the pool.

    - Big Drop: everyone drops what they hold.
    - Big Throw (hard): everyone throws what they hold, after a five-second wind-up.
    - Big Trip (hard): everyone is sent somewhere else on the map, each to a different place.
    - Big Meeting (hard): everyone is sent next to one player drawn at random.
    - Big Night: the clock fast-forwards to midnight, and runs on from there.
    - Big Sleep (hard): for `trap_duration` seconds, everyone falls asleep where they stand for two
      seconds, then wakes for five, again and again.
    - Big Load (hard): every pack worn comes off, and what it holds falls out.
    - Big Mask (hard): for `trap_duration` seconds, everyone wears the blindfold helmet of the
      blindfold puzzles, and nobody can take it off; a player already wearing a mask is spared.
    - Big Flare: a flare gun shot lands on every player, in the Archipelago colors.
      Each player can hide it on their own screen (Settings > Archipelago > Gentle effects).
    """

    display_name = "Trap Weights"
    valid_keys = frozenset(trap.item_name for trap in data.TRAPS)
    min = 0
    max = 100
    default = {trap.item_name: 10 for trap in data.TRAPS}


class TrapLink(Toggle):
    """
    TrapLink: a trap received in one game plays in every game that has it on (the "TrapLink" tag).

    With it on, every trap of this world's item pool that reaches you is also sent to the other
    games, and a trap sent by another game plays here: as the Big Walk trap of the same name when
    it is one, otherwise as the nearest one by its name (a freeze is Big Sleep, a bomb is Big Flare,
    ...), and as one drawn from the DeathLink roulette (`death_link_trap_weights`) when none comes
    near. Bonuses are never sent. A trap that comes from a DeathLink (`death_link_effect:
    roulette`) or from another game's TrapLink is not sent on, so none loops.
    """

    display_name = "TrapLink"


class HardTraps(DefaultOnToggle):
    """
    Lets the hard traps into the pool: those that cost time or progress (Big Throw, Big Trip,
    Big Meeting, Big Sleep, Big Load, Big Mask). Off keeps only the standard ones.
    """

    display_name = "Hard Traps"


class BonusFillPercentage(Range):
    """
    Percentage of filler items replaced by bonuses, a boost for every player. Which bonuses, and how
    often each: `bonus_weights`.
    """

    display_name = "Bonus Fill Percentage"
    range_start = 0
    range_end = 100
    default = 25


class BonusWeights(OptionCounter):
    """
    How often each bonus comes, against the others. 0 takes a bonus out of the pool.

    - Big Speed: everyone walks and runs faster for `trap_duration` seconds.
    - Big Jump: everyone jumps higher for `trap_duration` seconds.
    - Big Day: the clock fast-forwards to noon, and runs on from there.
    """

    display_name = "Bonus Weights"
    valid_keys = frozenset(bonus.item_name for bonus in data.BONUSES)
    min = 0
    max = 100
    default = {bonus.item_name: 10 for bonus in data.BONUSES}


class IslandObjectLimits(OptionCounter):
    """
    The most of each of the island's objects the pool holds; 0 leaves one out. Once every object
    is at its limit, the rest of the filler is bonuses (`bonus_weights`).

    - Most objects are random filler, drawn up to their limit. They are props the item feed names and
      that land beside you, with no effect and nothing in logic: none is ever needed.
    - The Walkie-Talkie is one of them, and 8 is not a lot to ask of the pool: 8 is how many the
      island itself has, so it is the most the filler can ever hold of them, and a seed that does
      not draw them all just makes more bonuses (traps and bonuses replace part of the filler as
      well). Lower it, or take it to 0, for a pool with fewer of them.
    - The four flare guns (`Flare Gun`, `Blue Flare Gun`, `Green Flare Gun`, `Yellow Flare Gun`) are
      not random filler: with `flare_gun_sanity` the pool holds exactly this many of each, and
      without it none. The `Rainbow Flare Gun` is always in the pool, this many times (0 leaves
      it out).
    - Backpacks, belts and gourd cartons are set apart: `backpacks_in_pool` and the like.
    """

    display_name = "Island Object Limits"
    valid_keys = frozenset(name for name, _ in data.FILLER_ITEMS if name not in ("Backpack", "Belt", "Gourd Carton"))
    min = 0
    max = 50
    default = {
        "Lamp": 2,
        "Flare Gun": 1,
        "Blue Flare Gun": 1,
        "Green Flare Gun": 1,
        "Yellow Flare Gun": 1,
        "Rainbow Flare Gun": 1,
        "Folding Map": 2,
        "Walkie-Talkie": 8,
        "Laser": 3,
        "Torch": 3,
        "Binoculars": 3,
        "Megaphone": 3,
        "Compass": 3,
        "Radio": 3,
        "X-Ray Goggles": 3,
    }


class TrapDuration(Range):
    """How long Big Speed, Big Jump, Big Sleep and Big Mask last, in seconds."""

    display_name = "Trap Duration"
    range_start = 10
    range_end = 120
    default = 20


class TrapsSpareTheGauntlet(DefaultOnToggle):
    """
    Big Trip and Big Meeting leave alone the players inside the Silent Gauntlet, and Big Meeting
    never sends anyone into it.
    """

    display_name = "Traps Spare the Gauntlet"


class BigWalkDeathLink(Choice):
    """
    DeathLink: a death in one game is a death in every game that has it on.

    - off: nothing sent, nothing received. The host can also switch DeathLink off for a session, and
      replace the amnesty, in the game's Settings > Archipelago, whatever the YAML says.
    - send: Big Walk sends one when something in `death_link_triggers` happens, and ignores those it
      receives.
    - receive: Big Walk plays `death_link_effect` when one arrives, and sends none.
    - both: both.
    """

    display_name = "DeathLink"
    option_off = 0
    option_send = 1
    option_receive = 2
    option_both = 3
    default = option_off


class DeathLinkTriggers(OptionSet):
    """
    What sends a DeathLink, with `death_link` on send or both.

    - puzzle_failed: a puzzle failed, as the game itself tells it: a wrong tile validated, buttons
      pressed out of order, a count gone wrong, a timer run out.
    - big_fall: a player falls far enough to land dazed. Not by default. A daze a received
      DeathLink causes (`death_link_effect: knockout`) sends nothing back.
    """

    display_name = "DeathLink Triggers"
    valid_keys = frozenset(data.DEATH_LINK_TRIGGERS)
    default = frozenset({"puzzle_failed"})


class DeathLinkAmnesty(Range):
    """How many triggers (failed puzzles, big falls) are forgiven before one sends a DeathLink: 0 sends on the first."""

    display_name = "DeathLink Amnesty"
    range_start = 0
    range_end = 10
    default = 0


class DeathLinkEffect(Choice):
    """
    What a DeathLink received does, with `death_link` on receive or both.

    - knockout: everyone drops what they hold, takes off their backpacks and belts (their contents
      fall out), and is dazed as after a big fall.
    - drop: everyone drops what they hold.
    - roulette: a trap drawn at random, by `death_link_trap_weights`.
    """

    display_name = "DeathLink Effect"
    option_drop = 0
    option_roulette = 1
    option_knockout = 2
    default = option_knockout


class DeathLinkTrapWeights(OptionCounter):
    """
    The traps the DeathLink roulette draws from, and how often each, against the others; 0 leaves
    one out. Apart from `trap_weights`, which is the item pool's.
    """

    display_name = "DeathLink Trap Weights"
    valid_keys = frozenset(trap.item_name for trap in data.TRAPS)
    min = 0
    max = 100
    default = {trap.item_name: 10 for trap in data.TRAPS}


class DeathLinkTarget(Choice):
    """
    Who a DeathLink received hits, with `death_link` on receive or both.

    - everyone: every player of the session.
    - one_player: one player only, the one hit the fewest times so far this session (ties at
      random), as received items go to the player given the fewest. With `death_link_effect:
      roulette`, the traps that need several players or hit the whole session are left out of the
      draw, whatever `death_link_trap_weights` says: Big Meeting (it gathers everyone around one
      player) and Big Night (the clock is the same for everyone). The other traps play on the
      one player: Big Drop, Big Throw, Big Trip, Big Sleep, Big Load, Big Mask and Big Flare.
    """

    display_name = "DeathLink Target"
    option_everyone = 0
    option_one_player = 1
    default = option_everyone


class DeathLinkTrapOnSend(Toggle):
    """
    With `death_link` on send or both: when Big Walk sends a DeathLink, its own players take the
    effect of a death too (`death_link_effect`, on `death_link_target`). Normally a game only
    receives the link of the others' deaths; this makes the one who dies pay for it as well.
    Nothing happens with `death_link` on receive or off: nothing is sent.
    """

    display_name = "DeathLink Trap on Send"


class DeathLinkRouletteHardTraps(DefaultOnToggle):
    """Lets the roulette draw the hard traps (Big Throw, Big Trip, Big Meeting, Big Sleep, Big Load, Big Mask). Off keeps them out of it."""

    display_name = "DeathLink Roulette Hard Traps"


@dataclass
class BigWalkOptions(PerGameCommonOptions):
    goal: Goal
    gourds_required: GourdsRequired
    gourd_sanity: GourdSanity
    radio_sanity: RadioSanity
    shuffle_radio_music: ShuffleRadioMusic
    pack_sanity: PackSanity
    firework_sanity: FireworkSanity
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
    cabin_fever_seconds: CabinFeverSeconds
    cabin_fever_help: CabinFeverHelp
    cabin_fever_long_seconds: CabinFeverLongSeconds
    cabin_fever_long_help: CabinFeverLongHelp
    tile_thief: TileThief
    gauntlet_mode: GauntletMode
    gauntlet_puzzles_required: GauntletPuzzlesRequired
    lock_gauntlet_needs: LockGauntletNeeds
    gauntlet_stages_local: GauntletStagesLocal
    gauntlet_stage_items: GauntletStageItems
    trap_fill_percentage: TrapFillPercentage
    trap_weights: TrapWeights
    hard_traps: HardTraps
    bonus_fill_percentage: BonusFillPercentage
    bonus_weights: BonusWeights
    island_object_limits: IslandObjectLimits
    trap_duration: TrapDuration
    traps_spare_the_gauntlet: TrapsSpareTheGauntlet
    death_link: BigWalkDeathLink
    death_link_triggers: DeathLinkTriggers
    death_link_amnesty: DeathLinkAmnesty
    death_link_effect: DeathLinkEffect
    death_link_roulette_hard_traps: DeathLinkRouletteHardTraps
    death_link_trap_weights: DeathLinkTrapWeights
    death_link_trap_on_send: DeathLinkTrapOnSend
    death_link_target: DeathLinkTarget
    trap_link: TrapLink
    backpacks_in_pool: BackpacksInPool
    belts_in_pool: BeltsInPool
    gourd_cartons_in_pool: GourdCartonsInPool
    flare_gun_sanity: FlareGunSanity
    random_colors: RandomColors
    gourd_name: GourdName
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [Goal, GourdsRequired]),
    OptionGroup("Sanity", [GourdSanity, RadioSanity, ShuffleRadioMusic, PackSanity, FireworkSanity, FlareGunSanity]),
    OptionGroup("Logic", [
        StartWithDrawbridgeOpen, OpenBlackTower, StartWithArchDoorsOpen, RequireArchDoors,
        GauntletMode, GauntletStageItems, GauntletPuzzlesRequired, LockGauntletNeeds, GauntletStagesLocal,
        LockPuzzleNeeds, StartWithPuzzleNeeds, StartWithRandomPuzzleNeed,
    ]),
    OptionGroup("Item Pool", [BackpacksInPool, BeltsInPool, GourdCartonsInPool, IslandObjectLimits]),
    OptionGroup("Colors and Names", [RandomColors, GourdName]),
    OptionGroup("Traps and Bonuses", [
        TrapFillPercentage, TrapWeights, HardTraps, BonusFillPercentage, BonusWeights, TrapDuration, TrapsSpareTheGauntlet,
        TrapLink,
    ]),
    OptionGroup("DeathLink", [
        BigWalkDeathLink, DeathLinkTriggers, DeathLinkAmnesty, DeathLinkEffect, DeathLinkTrapWeights,
        DeathLinkRouletteHardTraps, DeathLinkTrapOnSend, DeathLinkTarget,
    ]),
    OptionGroup("QoL", [HintKeys, TeleportButtons, TeleportBackToHub, TowerResyncStations, GuestsCanResync]),
    OptionGroup("Puzzle QoL", [
        CabinFeverSeconds, CabinFeverHelp, CabinFeverLongSeconds, CabinFeverLongHelp, TileThief,
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
