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


OBJECTIVE_ENDINGS = ("big_goodbye", "big_game", "big_wall")
"""What can end a list: the Gauntlet's bell, the secret ending, and the chapel's bell for a short seed without the
Gauntlet (the player, 2026-10-12)."""

EXCLUSIVE_ENDINGS = ("big_goodbye", "big_game")
"""The endings a save plays only one of: a list holds at most one of these (the chapel's bell can go beside either)."""

OBJECTIVE_KEYS = (
    "big_wall", "big_goodbye", "big_game", "big_collection", "big_keys", "big_radio", "big_towers",
    "big_climb", "big_fiesta", "big_lights", "big_objects", "big_body",
)
"""Every objective, in the order the slot data lists them. Frozen: the mod reads these strings."""


class Objectives(OptionSet):
    """
    What a seed asks for besides its Goal, in any order: the Goal (the ending) is then won once every objective listed
    is done too. Empty (the default): the Goal alone, as before. With Random Objectives, a few of these are drawn instead.

    - big_collection: place a number of gourds in the towers' slots (see Gourds Required).
    - big_keys: place a number of big keys in their plinths, the drawbridge's included (see Keys Required).
    - big_radio: switch on a number of radio stations (see Radio Stations Required; needs Radio Sanity).
    - big_towers: complete a number of towers (see Towers Required; needs Tower Mode in_order or by_item).
    - big_lights: switch on a share of the island's lights, each at least once (see Lights Required). Without Light
      Sanity the lamps count, with no check of their own.
    - big_objects: pick up a share of the island's objects (see Objects Required; needs Object Sanity).
    - big_body: get back a share of the body's abilities (see Body Required; needs Lock Abilities).
    - big_climb: stand on the highest point of the island.
    - big_fiesta (also read as big_club): the party on the beach, a radio plugged into the big speakers with a music
      playing. Needs a Radio, and with Shuffle Radio Music one of the stations, which become progression.

    The endings (big_wall, big_goodbye, big_game) are not listed here: they are the Goal.
    """

    display_name = "Objectives"
    # The endings are accepted only to be refused with a clear word (world._check_the_objectives); big_club is
    # another name of big_fiesta.
    valid_keys = frozenset(OBJECTIVE_KEYS) | {"big_club"}
    default = frozenset()


class RandomObjectives(Range):
    """
    Draws this many objectives at random, from the Objectives listed or, if none are, from all of those the other options
    allow. 0 (the default): the Objectives listed are all asked for, none drawn.
    """

    display_name = "Random Objectives"
    range_start = 0
    range_end = len(OBJECTIVE_KEYS) - len(OBJECTIVE_ENDINGS)
    default = 0


class LockEndingsUntilObjectives(DefaultOnToggle):
    """
    With Objectives: the ending's entry point (the chapel's bell, the Gauntlet's bell door, the secret room's
    printer) stays shut on every screen until the other objectives are done, so an ending,
    which plays once per save, is never played too early. On by default (0.6). Off: nothing is shut, and the ending
    can be played in any order.
    """

    display_name = "Lock Endings Until Objectives"


class KeysRequired(Range):
    """
    Only relevant if the Objectives hold "big_keys".

    How many big keys must be placed in their plinths, the drawbridge's and the six towers'.
    """

    display_name = "Keys Required"
    range_start = 1
    range_end = len(data.TOWERS)
    default = len(data.TOWERS)


class LightsRequired(Range):
    """
    Only relevant if the Objectives hold "big_lights".

    The share of the island's lights (those Light Sanity counts, or the lamps without it) that must have been
    switched on at least once, in percent.
    """

    display_name = "Lights Required"
    range_start = 1
    range_end = 100
    default = 50


class ObjectsRequired(Range):
    """
    Only relevant if the Objectives hold "big_objects".

    The share of Object Sanity's checks (each object with every, each kind with first_pickup) that must be sent, in
    percent.
    """

    display_name = "Objects Required"
    range_start = 1
    range_end = 100
    default = 50


class BodyRequired(Range):
    """
    Only relevant if the Objectives hold "big_body".

    The share of the ability items (Legs, Seat, the arms, Throwing, Carrying; not Running) that must have been
    received, in percent.
    """

    display_name = "Body Required"
    range_start = 1
    range_end = 100
    default = 100


class RadioStationsRequired(Range):
    """
    Only relevant if the Objectives hold "big_radio".

    How many radio stations must be switched on. Needs Radio Sanity, whose checks are the stations.
    """

    display_name = "Radio Stations Required"
    range_start = 1
    range_end = len(data.RADIO_STATIONS)
    default = len(data.RADIO_STATIONS)


class GourdsRequired(Range):
    """
    Only relevant if the Goal, or the Objectives, hold "big_collection".

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


class ObjectSanity(Choice):
    """
    The island's gadgets (walkie-talkies, radios, megaphones, binoculars, lasers, torches, scanners, clocks, the
    compass, the map, the cowbells) stay on the map, and picking them up sends checks.

    - off: no check, and the gadgets the pool holds are taken off the map as before.
    - first_pickup: the first one of each kind picked up (16 checks by default); the objects stay.
    - every: each one, the first time it is picked up (144 checks by default); it then leaves the map, like a pack.
    """

    display_name = "Object Sanity"
    option_off = 0
    option_first_pickup = 1
    option_every = 2
    default = option_off


class ObjectSanityKinds(OptionSet):
    """
    Which kinds of gadget Object Sanity counts. A kind left out is no check, and it leaves the island while
    Object Sanity is on.

    Kinds: walkie_talkie, radio, megaphone, binoculars, laser, torch, scanner, clock, compass, map, cowbell,
    lamp, paintbrush. The paintbrushes are left out by default, and they never leave the island (the Salon needs
    them). A lamp left out stays where it is (puzzles use them). With Light Sanity on, the lamps are never counted
    here: switching them on is their check.
    """

    display_name = "Object Sanity Kinds"
    valid_keys = frozenset(data.OBJECT_KIND_KEYS.values())
    default = frozenset(data.OBJECT_KIND_KEYS.values()) - data.OBJECT_KINDS_LEFT_OUT_BY_DEFAULT


class ObjectSanityItems(Choice):
    """
    What Object Sanity's checks may hold.

    - any: anything, from any world.
    - local: only this world's own items, of those nothing needs (filler, traps, useful ones).
    - junk: only filler, never anything a world needs.
    """

    display_name = "Object Sanity Items"
    option_any = 0
    option_local = 1
    option_junk = 2
    default = option_any


class LightSanity(Choice):
    """
    A check the first time each light of the island is switched on. The puzzles'
    own lights are not counted.

    - off: no check.
    - lamps: the lamps (the ones Object Sanity knows, on their pedestals, in the towers, in the tutorial).
    - lamps_and_switches: the lamps, and the wall switches of the Doof Chamber, the salons, the tutorial, the
      train and its stops.
    """

    display_name = "Light Sanity"
    option_off = 0
    option_lamps = 1
    option_lamps_and_switches = 2
    default = option_off


class LightSanityItems(Choice):
    """
    What Light Sanity's checks may hold.

    - any: anything, from any world.
    - local: only this world's own items, of those nothing needs (filler, traps, useful ones).
    - junk: only filler, never anything a world needs.
    """

    display_name = "Light Sanity Items"
    option_any = 0
    option_local = 1
    option_junk = 2
    default = option_any


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
    The Gourd Carton follows: "<name> Carton".
    Letters, digits and spaces, with ' - _ . , ! ? & + : ; ( ) @; anything else is dropped.
    All Big Walk slots must use the same name, and it can't be another item's or group's name;
    otherwise it stays "Gourd".
    """

    display_name = "Gourd Name"
    default = "Gourd"


class TowerMode(Choice):
    """
    How the towers' monuments open for the gourds.

    - all_open: every monument takes gourds from the start, in any order. The way it has always been.
    - in_order: one monument at a time, in an order the seed draws: the next opens once the one before is
      full, and a monument that is not the next refuses gourds, and its tower's button stays still.
    - by_item: the same order, and the next monument also waits for a Progressive Monument item, the first
      monument being open from the start.
    """

    display_name = "Tower Mode"
    option_all_open = 0
    option_in_order = 1
    option_by_item = 2
    default = option_all_open


class FirstTowerOpen(DefaultOnToggle):
    """
    With Tower Mode by_item: the first monument takes gourds from the start (the default). Off: it waits for a
    Progressive Monument like the others, and the seed starts with no monument open. Needs by_item: nothing could
    open the first monument in in_order.
    """

    display_name = "First Tower Open"


class EveryoneGathers(DefaultOnToggle):
    """
    Big Climb and Big Fiesta count their ten seconds only while every player is there: at the island's highest point for
    the climb, near the big speakers on the beach for the party (the default). Off: one player is enough.
    """

    display_name = "Everyone Gathers"


class TowersRequired(Range):
    """
    Only relevant if the Objectives hold "big_towers" (which needs Tower Mode in_order or by_item).

    How many towers must be completed, a tower being complete when its monument is full, in the seed's order.
    """

    display_name = "Towers Required"
    range_start = 1
    range_end = len(data.TOWERS)
    default = len(data.TOWERS)


class TowerSanity(DefaultOnToggle):
    """
    A check for reaching each of the seven towers: the Red, Green, Blue, Yellow and Black ones when the tower's
    opening button is pressed (the two buttons at the top of the Black Tower held together), the drawbridge and
    the Green Dome when a player gets there.
    """

    display_name = "Tower Sanity"


class HintDiscoveredKeyLocations(Toggle):
    """
    When a tower is reached (its opening button pressed), hint what sits at its key cuts and its key deposit, so a player who
    walks up to a tower learns what is waiting there. Free, like the hints at the start.
    """

    display_name = "Hint Discovered Key Locations"


class HintDiscoveredDepositBoxes(Toggle):
    """
    With Tower Mode in_order or by_item: when a tower is reached, hint the gourd deposit checks that sit in its
    monument (the gourds placed there, counted from the towers before it). Free, like the hints at the start.
    Without a tower order the deposits belong to no tower, and nothing is hinted.
    """

    display_name = "Hint Discovered Deposit Boxes"


class RandomColors(DefaultOnToggle):
    """
    The island's buoys (their body, halo and light), flare guns (and their shots), gadgets (one color
    per kind: binoculars, clocks, compasses, cowbells, radios, lasers, megaphones, torches, scanners,
    walkie-talkies) and the firework launchers away from the towers (and their bursts) take colors
    drawn for the seed, between five and ten of them, the same on every player's screen. It paints objects of the map only:
    never the players, the gourds, the keys, or what a puzzle needs. Off: the game's own colors.
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


class StartWithMapRoomOpen(Toggle):
    """
    If on, you start with the Map Room open instead of finding it in the multiworld. The Red Funnel
    Tower Key Deposit stays a check either way.
    """

    display_name = "Start With Map Room Open"


class LinearKeys(Toggle):
    """
    If on, a big key placed in its tower opens what it opens in the game (the drawbridge, the Map Room, the
    chairlift, the train, the tunnels, the Big Wall Door, the Spawn Secret Door) instead of that being an item to
    find. The key deposits stay checks; each one holds its own door.
    """

    display_name = "Linear Keys"


class LinearArchDoors(Toggle):
    """
    If on, the hub's three arch doors are as in the game: no items, each opened by its push button on its far side
    (the First Arch Door's outside the hub; the Left one coming from the train or the Green Dome, the Right one from
    the tunnels). The way out of the starting zone is then the Drawbridge. Start With Arch Doors Open and Require
    Arch Doors have nothing to do.
    """

    display_name = "Linear Arch Doors"


class MonumentSanity(DefaultOnToggle):
    """
    A check for each monument filled to its last slot: the Drawbridge's, the five towers' and the Green Dome's.
    Linear Towers puts each tower's key there. On by default.
    """

    display_name = "Monument Sanity"


class LinearTowers(Toggle):
    """
    If on, filling a tower's monument gives its key, as in the game, instead of the key being an item to find.
    The key still arrives as an item, beside the player, the moment the monument is full. The
    "Monument Full" checks exist with it, Monument Sanity or not.
    """

    display_name = "Linear Towers"


class LinearPuzzles(Toggle):
    """
    If on, every puzzle holds a gourd, as in the game, instead of anything from the multiworld (not the seven purple
    ones past the chairlift, which stay checks like any other). The
    gourd still arrives as an item, beside the player.
    """

    display_name = "Linear Puzzles"


class IncludeBigGamePuzzles(Choice):
    """
    The seven purple gourds' puzzles, past the chairlift.

    - from_start: checks, and shown on the map from the start.
    - when_unlocked: checks, but as in the game: not on the map, and in logic only once the ending is reached.
    - disabled: not checks at all.
    """

    display_name = "Include Big Game Puzzles"
    option_from_start = 0
    option_when_unlocked = 1
    option_disabled = 2
    default = option_from_start


class CutKeySanity(Choice):
    """
    The 25 cuts of the big keys.

    - on: each cut is a check.
    - junk: each cut is a check that holds only filler.
    - off: no check for the cuts.
    """

    display_name = "Cut Key Sanity"
    option_on = 0
    option_junk = 1
    option_off = 2
    default = option_on


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


class LockAbilities(Choice):
    """
    The players' bodies behind items, received by every player at once. Legs: jumping and crouching,
    needed to leave the starting zone. Seat: sitting on a chair or on the floor. A Progressive Arm:
    raising it, then pointing, then extending it sideways. The gesture puzzles ask for them.

    Throwing and Carrying (another player) are items too, and Running with Lock Running.

    - disabled: every ability from the start.
    - handed: three Progressive Left Arm and three Progressive Right Arm.
    - ambidextrous: three Progressive Arms, both arms at once.
    """

    display_name = "Lock Abilities"
    option_disabled = 0
    option_handed = 1
    option_ambidextrous = 2
    default = option_disabled


class FastGolf(Toggle):
    """
    The golf ball (the cannonball of the golf puzzles) is no longer too heavy, and is carried like any object.
    """

    display_name = "Fast Golf"


class StartWithAbilities(OptionSet):
    """
    Which abilities you already have when Lock Abilities is on: their items are handed over at once, and leave the
    pool. Every ability not listed is an item to find.

    Abilities: legs, seat, arms, throwing, carrying. Running has its own option, Lock Running.
    """

    display_name = "Start With Abilities"
    valid_keys = frozenset({"legs", "seat", "arms", "throwing", "carrying"})
    default = frozenset()


class LockRunning(Toggle):
    """
    With Lock Abilities on, running is an item too (Running). No place asks for it: the island is only slower
    to cross until it arrives.
    """

    display_name = "Lock Running"


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

    Not with the goal big_wall, which ends the run before the Gauntlet. With Big
    Goodbye the seven items are needed to win. With big_game the Gauntlet is extra: its checks sit behind the chapel, so with
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


class TrackerBoard(Toggle):
    """
    The right sign of the hub's notice board counts the checks you can do now, in logic, against those not done
    yet, and names a few of them: those a hint points to first, then those that hold a progression item. Mod only.
    """

    display_name = "Tracker Board"


class MapTracker(Choice):
    """
    The big map of the Map Room tracks the seed: every check stands on it (puzzles, objects, lamps, switches,
    fireworks, radio stations, towers...), a small copy of its object on a pin, and leaves it once done. Mod only.

    - off: the game's map.
    - in_logic: only the checks you can do now.
    - all: every check not done, those out of logic greyed.
    """

    display_name = "Map Tracker"
    option_off = 0
    option_in_logic = 1
    option_all = 2
    default = 0


class HintBoard(DefaultOnToggle):
    """
    The centre sign of the hub's notice board shows your hint points and the cost of a hint, and lists the hints you
    asked for that are not found yet. Write an item's name on it to ask for a hint (it costs points, as `!hint`).
    Mod only.
    """

    display_name = "Hint Board"


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


class CabinFeverHideTimer(Toggle):
    """
    The Cabin Fever room's dial hides the time left: "??:??" while the wait runs, with now and then a digit
    flashing. The game's sound still plays over the last ten seconds. Mod only.
    """

    display_name = "Cabin Fever Hide Timer"


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


class CabinFeverLongHideTimer(Toggle):
    """
    The Cabin Fever Long room's dial hides the time left: "??:??" while the wait runs, with now and then a
    digit flashing. The game's sound still plays over the last ten seconds. Mod only.
    """

    display_name = "Cabin Fever Long Hide Timer"


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
    Big Meeting, Big Sleep, Big Load, Big Mask, Big Air, Big Reset). Off keeps only the standard ones.
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
    # "X-Ray Goggles" is the Scanner's name before 0.5.0, still read (items.island_object_limits).
    valid_keys = frozenset(name for name, _ in data.FILLER_ITEMS if name not in ("Backpack", "Belt", "Gourd Carton")) | {
        "X-Ray Goggles"}
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
        "Scanner": 3,
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
    """
    Lets the roulette draw the hard traps (Big Throw, Big Trip, Big Meeting, Big Sleep, Big Load, Big Mask, Big Air,
    Big Reset). Off keeps them out of it.
    """

    display_name = "DeathLink Roulette Hard Traps"


@dataclass
class BigWalkOptions(PerGameCommonOptions):
    goal: Goal
    objectives: Objectives
    random_objectives: RandomObjectives
    lock_endings_until_objectives: LockEndingsUntilObjectives
    keys_required: KeysRequired
    radio_stations_required: RadioStationsRequired
    gourds_required: GourdsRequired
    gourd_sanity: GourdSanity
    radio_sanity: RadioSanity
    shuffle_radio_music: ShuffleRadioMusic
    pack_sanity: PackSanity
    firework_sanity: FireworkSanity
    start_with_drawbridge_open: StartWithDrawbridgeOpen
    start_with_arch_doors_open: StartWithArchDoorsOpen
    require_arch_doors: RequireArchDoors
    lock_puzzle_needs: LockPuzzleNeeds
    start_with_puzzle_needs: StartWithPuzzleNeeds
    start_with_random_puzzle_need: StartWithRandomPuzzleNeed
    lock_abilities: LockAbilities
    start_with_map_room_open: StartWithMapRoomOpen
    linear_keys: LinearKeys
    linear_arch_doors: LinearArchDoors
    tower_sanity: TowerSanity
    monument_sanity: MonumentSanity
    linear_towers: LinearTowers
    linear_puzzles: LinearPuzzles
    include_big_game_puzzles: IncludeBigGamePuzzles
    cut_key_sanity: CutKeySanity
    start_with_abilities: StartWithAbilities
    lock_running: LockRunning
    fast_golf: FastGolf
    hint_keys: HintKeys
    open_black_tower: OpenBlackTower
    tower_resync_stations: TowerResyncStations
    tracker_board: TrackerBoard
    hint_board: HintBoard
    map_tracker: MapTracker
    guests_can_resync: GuestsCanResync
    teleport_buttons: TeleportButtons
    teleport_back_to_hub: TeleportBackToHub
    cabin_fever_seconds: CabinFeverSeconds
    cabin_fever_help: CabinFeverHelp
    cabin_fever_long_seconds: CabinFeverLongSeconds
    cabin_fever_long_help: CabinFeverLongHelp
    cabin_fever_hide_timer: CabinFeverHideTimer
    cabin_fever_long_hide_timer: CabinFeverLongHideTimer
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
    object_sanity: ObjectSanity
    object_sanity_kinds: ObjectSanityKinds
    object_sanity_items: ObjectSanityItems
    light_sanity: LightSanity
    light_sanity_items: LightSanityItems
    lights_required: LightsRequired
    objects_required: ObjectsRequired
    body_required: BodyRequired
    random_colors: RandomColors
    tower_mode: TowerMode
    first_tower_open: FirstTowerOpen
    everyone_gathers: EveryoneGathers
    towers_required: TowersRequired
    hint_discovered_key_locations: HintDiscoveredKeyLocations
    hint_discovered_deposit_boxes: HintDiscoveredDepositBoxes
    gourd_name: GourdName
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [
        Goal, Objectives, RandomObjectives, EveryoneGathers, LockEndingsUntilObjectives,
        GourdsRequired, KeysRequired, RadioStationsRequired, TowersRequired, LightsRequired, ObjectsRequired, BodyRequired,
    ]),
    OptionGroup("Towers", [TowerMode, FirstTowerOpen]),
    OptionGroup("Sanity", [
        GourdSanity, RadioSanity, ShuffleRadioMusic, PackSanity, FireworkSanity, FlareGunSanity, TowerSanity,
        MonumentSanity, CutKeySanity,
        ObjectSanity, ObjectSanityKinds, ObjectSanityItems, LightSanity, LightSanityItems,
    ]),
    OptionGroup("Logic", [
        StartWithDrawbridgeOpen, StartWithMapRoomOpen, StartWithArchDoorsOpen, RequireArchDoors, OpenBlackTower,
        LockPuzzleNeeds, StartWithPuzzleNeeds, StartWithRandomPuzzleNeed, IncludeBigGamePuzzles,
        GauntletMode, GauntletStageItems, GauntletPuzzlesRequired, LockGauntletNeeds, GauntletStagesLocal,
    ]),
    OptionGroup("Vanilla Behaviours", [LinearKeys, LinearArchDoors, LinearTowers, LinearPuzzles]),
    OptionGroup("Movement Locking", [LockAbilities, StartWithAbilities, LockRunning]),
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
    OptionGroup("QoL", [
        HintKeys, HintDiscoveredKeyLocations, HintDiscoveredDepositBoxes, HintBoard, TrackerBoard, MapTracker,
        TeleportButtons, TeleportBackToHub, TowerResyncStations, GuestsCanResync,
    ]),
    OptionGroup("Puzzle QoL", [
        FastGolf, CabinFeverSeconds, CabinFeverHelp, CabinFeverHideTimer, CabinFeverLongSeconds, CabinFeverLongHelp,
        CabinFeverLongHideTimer, TileThief,
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
