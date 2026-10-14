"""
Static mirror of the Big Walk save data the mod reads and writes.

Everything in this file comes from the game's own enums (`SaveablePropName`,
`SaveableHomeName`, `SavableSystem`, extracted with Il2CppInspectorRedux) and
from the reverse-engineering notes in `../../mod/reverse-engineering-notes.md`.
It is the single source of truth shared by items.py, locations.py and rules.py.

Archipelago ids are derived from the game's own enum values rather than being
allocated sequentially. That is deliberate: the mod can then compute an id with
plain arithmetic (`BASE_ID + (int)saveablePropName`) instead of shipping a
duplicated table that would silently drift from this file. See `../protocol.md`.
"""

from __future__ import annotations

from typing import NamedTuple

# All Big Walk ids live in a single contiguous block. Sub-ranges are offset so
# that two categories can never collide even though they reuse the game's own
# (per-enum) numbering.
BASE_ID = 8_600_000

RADIO_ID_OFFSET = 1_000
DEPOSIT_ID_OFFSET = 2_000
GAUNTLET_ID_OFFSET = 2_500
"""
The Silent Gauntlet's stages, location and item alike (separate namespaces, as
for the radio): `BASE_ID + GAUNTLET_ID_OFFSET + SavableSystem.GauntletChamberN`,
that is 2550..2556. Below the cuts' 6000, which the id tests keep as the
highest location block, and clear of the deposits' 2001..2045.
"""
CUT_ID_OFFSET = 3_000
KEY_ITEM_ID_OFFSET = 4_000
ARCH_DOOR_ID_OFFSET = 5_000
# 9_000 and above is the item-only range for things the game has no enum for
# (filler, traps); see FILLER_ITEMS / TRAP_ITEMS at the bottom of this file.
# 10_000..10_159 held the per-puzzle Unlock items of `lock_puzzles`, removed
# before it was ever released (2026-10-02: no puzzle sits behind an item of its
# own). The range stays empty rather than being reused, and PUZZLE_NEED_ID_OFFSET
# below keeps its number, so no id a seed may carry ever changes meaning.
PUZZLE_NEED_ID_OFFSET = 11_000
"""
Item-only: `lock_puzzle_needs`' items, numbered by their position in
PUZZLE_NEEDS. Clear of the 9_000s' filler and traps.
"""

CUT_STRIDE = 10
"""
Ids reserved per big key for its cut segments: `BASE_ID + CUT_ID_OFFSET +
prop_value * CUT_STRIDE + index`.

Ten where the widest key has five, so the block can absorb a longer key
without every id below it moving. With `prop_value` in 300..306 the range
works out to 6000..6064 above BASE_ID, which sits clear of the puzzles
(100..159), the keys (300..306), the deposits (2001..2045), the radio
(1030..1036) and the item-only 9000s.
"""

GOURD_ITEM_NAME = "Gourd"
GOURD_CARTON_ITEM_NAME = "Gourd Carton"
"""Renamed after the gourds by `gourd_name`: "<name> Carton"."""

# --------------------------------------------------------------------------
# Puzzles (SaveablePropName.gourdXxx)
# --------------------------------------------------------------------------
# 45 puzzles — every one that exists in the build people are playing. The enum
# holds 58 gourd values (100-159, skipping 102 and 136), and thirteen of them
# produce nothing: see ABSENT_FROM_THE_BUILD below for how that was settled,
# and note that ../design-decisions.md read the same gap backwards for two
# weeks, as a content update that had ADDED thirteen puzzles.
#
# Deliberately excluded:
#   - gourdTesting00-39 / bigKeyTesting* (dev-only values, never triggered in
#     normal play; the mod already filters them out in GourdRegistry).
#   - gourdSecretZoneVice (290): the only gourd-prefixed value with no matching
#     valetXxx home, never observed in play, purpose unknown. Including it
#     would risk an unreachable location. The mod must not report it either.
#   - THE 46TH GOURD, measured 2026-09-22 and not in this enum at all. A
#     Ctrl+V roster taken beside it reads "46 RewardGourd(s) in the world"
#     against the 45 below, and the extra one is 4.3m away while the next is
#     115m — so it really is there, which the roster's distance sort is the
#     only way to know (the game instantiates every gourd everywhere). It is
#     a shiny white gourd in the zone behind the Spawn Secret Door, released by
#     two buttons held at once (`NHoldLogic 2`, minimumMatches=2 — the bells'
#     mechanism). It carries `saveablePropName = notSavable`.
#     - It is NOT gourdSecretZoneVice: that value would have printed its own
#       name. The exclusion above stands, for a still-unknown reason.
#     - It is not a mod artefact either: that session had "0 received, 0
#       deposited, 0 already spawned", so no cosmetic gourd existed.
#     - **It is scenery, settled in game on 2026-09-22.** It sat next to the
#       two buttons that end that zone, so it looked like the zone's
#       conclusion and the goal was built on it for an afternoon. It is not:
#       the buttons unlock its vise and the gourd STAYS IN PLACE.
#       `RewardGourd.ServerSetGourdState` is never called for it, and a
#       roster taken afterwards still reads `state=Locked`. That is also why
#       it has no save identity and no enum value — nothing about it is meant
#       to be recorded. `goal: second_ending` fires on the ending transition
#       the buttons start instead (../protocol.md §6).
#     - Worth keeping even so, because the obvious implementation is a trap:
#       the mod's own cosmetic gourds are `notSavable` too, so "the gourd
#       that is notSavable" would have goaled a seed the first time a player
#       was sent one.
#   - the thirteen in ABSENT_FROM_THE_BUILD below, cut on 2026-09-21: they are
#     full enum entries with their own valet, but nothing in the shipped game
#     produces them.


class Puzzle(NamedTuple):
    prop_name: str
    """`SaveablePropName` enum name, exactly as the game writes it to the save."""
    prop_value: int
    """`SaveablePropName` enum value; the location id is BASE_ID + this."""
    location_name: str
    """Player-facing Archipelago location name."""


PUZZLES: tuple[Puzzle, ...] = (
    Puzzle("gourdCabinFever", 100, "Cabin Fever Puzzle"),
    Puzzle("gourdHighButton", 101, "High Button Puzzle"),
    Puzzle("gourdFielding", 103, "Fielding Puzzle"),
    Puzzle("gourdCannonBall", 104, "Cannonball Puzzle"),
    Puzzle("gourdInvisibleInk", 105, "Invisible Ink Puzzle"),
    Puzzle("gourdTrapRoom", 106, "Trap Room Puzzle"),
    Puzzle("gourdMediumSimPress", 107, "Medium Simultaneous Press Puzzle"),
    Puzzle("gourdEasySimPress", 108, "Easy Simultaneous Press Puzzle"),
    Puzzle("gourdRingRoom", 109, "Ring Room Puzzle"),
    Puzzle("gourdObby", 113, "Obby Puzzle"),
    Puzzle("gourdCarousel", 114, "Carousel Puzzle"),
    Puzzle("gourdCoordinates", 115, "Coordinates Puzzle"),
    Puzzle("gourdTelescopeToBox", 116, "Telescope to Box Puzzle"),
    Puzzle("gourdObservationRoom", 117, "Observation Room Puzzle"),
    Puzzle("gourdWindowLabyrinth", 118, "Window Labyrinth Puzzle"),
    Puzzle("gourdBasketball", 121, "Basketball Puzzle"),
    Puzzle("gourdConcert", 122, "Concert Puzzle"),
    Puzzle("gourdIndoorSemaphore", 123, "Indoor Semaphore Puzzle"),
    Puzzle("gourdOpticalTelegraph", 124, "Optical Telegraph Puzzle"),
    # The game's own enum misspells "Priest"; the location name does not.
    Puzzle("gourdPoetAndPreist", 125, "Poet and Priest Puzzle"),
    Puzzle("gourdMemoryBombs", 127, "Memory Bombs Puzzle"),
    Puzzle("gourdTileThief", 133, "Tile Thief Puzzle"),
    Puzzle("gourdCharadesRooms", 134, "Charades Rooms Puzzle"),
    Puzzle("gourdMicrophoneArray", 135, "Microphone Array Puzzle"),
    Puzzle("gourdPointersParadise", 137, "Pointer's Paradise Puzzle"),
    Puzzle("gourdCoordinatesHolding", 138, "Coordinates Holding Puzzle"),
    Puzzle("gourdEggHunt", 139, "Egg Hunt Puzzle"),
    Puzzle("gourdTellerWindow", 140, "Teller Window Puzzle"),
    Puzzle("gourdSignalFlags", 141, "Signal Flags Puzzle"),
    Puzzle("gourdCabinFeverLong", 142, "Cabin Fever Long Puzzle"),
    Puzzle("gourdBreadcrumbLoop", 143, "Breadcrumb Loop Puzzle"),
    Puzzle("gourdScoutBombs", 144, "Scout Bombs Puzzle"),
    # Another game-side typo ("Centuron"), kept only in prop_name.
    Puzzle("gourdCenturonSong", 147, "Centurion Song Puzzle"),
    Puzzle("gourdMusicalHoliday", 148, "Musical Holiday Puzzle"),
    Puzzle("gourdKickUpPits", 149, "Kick Up Pits Puzzle"),
    Puzzle("gourdSingerAndSelecter", 150, "Singer and Selector Puzzle"),
    Puzzle("gourdDancerAndSelecter", 151, "Dancer and Selector Puzzle"),
    Puzzle("gourdSpeedObby", 152, "Speed Obby Puzzle"),
    Puzzle("gourdBlindfoldCatwalk", 153, "Blindfold Catwalk Puzzle"),
    Puzzle("gourdBlindfoldFishtrap", 154, "Blindfold Fishtrap Puzzle"),
    Puzzle("gourdPerspectiveCounting", 155, "Perspective Counting Puzzle"),
    Puzzle("gourdCenturionSeance", 156, "Centurion Seance Puzzle"),
    Puzzle("gourdFlareRun", 157, "Flare Run Puzzle"),
    Puzzle("gourdCannonballCommute", 158, "Cannonball Commute Puzzle"),
    Puzzle("gourdPoetAndPontiff", 159, "Poet and Pontiff Puzzle"),
)

class PuzzleNeed(NamedTuple):
    key: str
    """Tag used in PUZZLE_TAGS and as the player-facing option value."""
    item_name: str


PUZZLE_NEEDS: tuple[PuzzleNeed, ...] = (
    PuzzleNeed("buttons", "Buttons Unlock"),
    PuzzleNeed("sync_buttons", "Synchronized Buttons Unlock"),
    PuzzleNeed("icon_panels", "Icon Panels Unlock"),
    PuzzleNeed("drawing_panels", "Drawing Panels Unlock"),
    PuzzleNeed("pose_panels", "Pose Panels Unlock"),
    PuzzleNeed("sound_panels", "Sound Panels Unlock"),
    PuzzleNeed("point_panels", "Point Panels Unlock"),
    PuzzleNeed("speakers", "Speakers Unlock"),
    PuzzleNeed("lights", "Lights Unlock"),
    PuzzleNeed("teapots", "Teapots Unlock"),
    PuzzleNeed("timed_tomato", "Timed Tomato Unlock"),
    PuzzleNeed("golf_ball", "Golf Ball Unlock"),
    PuzzleNeed("big_head", "Big Head Mask Unlock"),
    PuzzleNeed("ink_viewer", "Ink Viewer Unlock"),
    PuzzleNeed("counter", "Counter Unlock"),
    PuzzleNeed("coordinates_computer", "Coordinates Computer Unlock"),
    PuzzleNeed("eggs", "Terrain Pegs Unlock"),
)

EARLY_NEEDS: tuple[PuzzleNeed, ...] = PUZZLE_NEEDS[0:3]
"""Buttons, Synchronized Buttons, Icon Panels: the needs most puzzles share, asked for early."""
"""
What a puzzle can need that this world turns into an item (`lock_puzzle_needs`).
The order is load-bearing: an item's id is its position here.

The terrain pegs of Egg Hunt (the YAML key stays `eggs`) are a need of their own, like the tomato, the golf ball
and the mask: objects belonging to one puzzle, hidden until the item arrives.

Not in this list on purpose: `coop`, `throwable` and `item_pickup`. They are
tags in PUZZLE_TAGS and nothing more. A second player is a head count, not
something found, and throwing and picking up are abilities of the player,
which belong to the `lock_*` options still on the roadmap (R2, R3).
"""

PUZZLE_NEED_KEYS: frozenset[str] = frozenset(need.key for need in PUZZLE_NEEDS)
PUZZLE_NEEDS_BY_KEY: dict[str, PuzzleNeed] = {need.key: need for need in PUZZLE_NEEDS}

NON_ITEM_TAGS: frozenset[str] = frozenset({"coop", "throwable", "item_pickup"})

PUZZLE_TAGS: dict[str, frozenset[str]] = {
    # Categorised in game by the player (2026-10-02), puzzle by puzzle.
    # Wording kept from that pass where it was ambiguous:
    #   - Carousel: "panel buttons, buttons" turned out, on site
    #     (2026-10-02), to be buttons and icon panels.
    #   - Tile Thief: "speaker panels" read as sound panels (answers), not
    #     speakers (the tool), which it does not list separately.
    #   - Coordinates and Coordinates Holding: "in logic with coordinates
    #     computer" read as needing the computer itself.
    #   - Cabin Fever (both), High Button, Perspective Counting: the
    #     player noted they could be cheated or are not truly synced; the
    #     need is kept as listed.
    "gourdCabinFever": frozenset({"coop", "buttons"}),
    "gourdHighButton": frozenset({"coop", "buttons"}),
    "gourdFielding": frozenset({"coop", "timed_tomato", "buttons"}),
    "gourdCannonBall": frozenset({"golf_ball"}),
    "gourdInvisibleInk": frozenset({"coop", "buttons", "icon_panels", "ink_viewer"}),
    "gourdTrapRoom": frozenset({"coop", "buttons", "icon_panels"}),
    "gourdMediumSimPress": frozenset({"coop", "sync_buttons"}),
    "gourdEasySimPress": frozenset({"coop", "sync_buttons"}),
    "gourdRingRoom": frozenset({"coop", "speakers", "icon_panels", "lights", "buttons"}),
    # Audited on 2026-10-05 against what the mod really hides in each puzzle's place (its own
    # rules applied to a dump of the island, after E2 found the Obby and Fielding in logic too
    # early): the validators' buttons were missing from Fielding, both Obbies, Blindfold Catwalk,
    # Optical Telegraph and Indoor Semaphore, the Obby's tomato dispenser (fifth floor), Indoor
    # Semaphore's icon panels and the Charades Rooms' pose panels.
    "gourdObby": frozenset({"coop", "sync_buttons", "timed_tomato", "buttons"}),
    "gourdCarousel": frozenset({"buttons", "icon_panels"}),
    "gourdCoordinates": frozenset({"coop", "sync_buttons", "coordinates_computer"}),
    "gourdTelescopeToBox": frozenset({"coop", "buttons"}),
    "gourdObservationRoom": frozenset({"coop", "lights", "icon_panels", "buttons"}),
    "gourdWindowLabyrinth": frozenset({"coop", "timed_tomato"}),
    "gourdBasketball": frozenset({"throwable"}),
    "gourdConcert": frozenset({"coop", "buttons"}),
    "gourdIndoorSemaphore": frozenset({"coop", "pose_panels", "buttons", "icon_panels"}),
    "gourdOpticalTelegraph": frozenset({"coop", "pose_panels", "buttons"}),
    "gourdPoetAndPreist": frozenset({"coop", "drawing_panels", "buttons"}),
    "gourdMemoryBombs": frozenset({"buttons"}),
    "gourdTileThief": frozenset({"icon_panels", "drawing_panels", "pose_panels", "sound_panels", "buttons"}),
    "gourdCharadesRooms": frozenset({"coop", "icon_panels", "buttons", "pose_panels"}),
    "gourdMicrophoneArray": frozenset({"coop", "buttons"}),
    "gourdPointersParadise": frozenset({"coop", "buttons"}),
    "gourdCoordinatesHolding": frozenset({"coop", "buttons", "coordinates_computer"}),
    "gourdEggHunt": frozenset({"eggs", "buttons"}),
    "gourdTellerWindow": frozenset({"coop", "icon_panels", "buttons"}),
    "gourdSignalFlags": frozenset({"coop", "icon_panels", "counter", "buttons"}),
    "gourdCabinFeverLong": frozenset({"coop", "buttons"}),
    "gourdBreadcrumbLoop": frozenset({"coop", "sync_buttons"}),
    "gourdScoutBombs": frozenset({"coop", "buttons", "teapots", "point_panels"}),
    "gourdCenturonSong": frozenset({"coop", "buttons"}),
    "gourdMusicalHoliday": frozenset({"coop", "buttons"}),
    "gourdKickUpPits": frozenset(),
    "gourdSingerAndSelecter": frozenset({"coop", "buttons", "sound_panels", "speakers"}),
    "gourdDancerAndSelecter": frozenset({"coop", "buttons", "sound_panels"}),
    "gourdSpeedObby": frozenset({"coop", "timed_tomato", "sync_buttons", "buttons"}),
    "gourdBlindfoldCatwalk": frozenset({"coop", "big_head", "buttons"}),
    "gourdBlindfoldFishtrap": frozenset({"coop", "big_head", "icon_panels", "buttons"}),
    "gourdPerspectiveCounting": frozenset({"coop", "counter", "buttons"}),
    "gourdCenturionSeance": frozenset({"coop", "buttons", "speakers", "lights"}),
    "gourdFlareRun": frozenset({"coop", "buttons"}),
    "gourdCannonballCommute": frozenset({"golf_ball"}),
    "gourdPoetAndPontiff": frozenset({"coop", "buttons", "drawing_panels"}),
}
"""
Every puzzle's requirements, by `prop_name`. A tag is either a PUZZLE_NEEDS key
(which `lock_puzzle_needs` turns into an item) or one of NON_ITEM_TAGS.
"""

GAUNTLET_STAGE_TAGS: tuple[frozenset[str], ...] = (
    frozenset({"coop", "icon_panels", "buttons"}),
    frozenset({"coop", "buttons"}),
    frozenset({"coop", "timed_tomato"}),
    frozenset({"coop", "sync_buttons"}),
    frozenset({"coop", "teapots", "buttons"}),
    frozenset({"coop", "icon_panels", "buttons", "ink_viewer"}),
    frozenset({"coop", "buttons"}),
)
"""
The Silent Gauntlet's seven stages, in order: what each stage's puzzle is built
from. With `gauntlet_mode: locked_stages` a stage's check needs these (rules.py,
set_gauntlet_rules), and so does every stage after it, which can only be
reached by solving it.
"""


class GauntletStage(NamedTuple):
    index: int
    """0..6: the game's `Level<index>` under `SilentGauntlet 2Player/Positioner`."""
    system_name: str
    """`SavableSystem` enum name, written 0 -> 1 when the stage's puzzle is solved."""
    system_value: int
    """`SavableSystem` enum value; both ids are BASE_ID + GAUNTLET_ID_OFFSET + this."""
    chamber: str
    """The stage's puzzle, in the words of the game's chamber names."""

    @property
    def number(self) -> int:
        return self.index + 1

    @property
    def location_name(self) -> str:
        return f"Gauntlet Stage {self.number}: {self.chamber}"

    @property
    def item_name(self) -> str:
        """Opens the stairway out of this stage, the collective door `GourdTower_Gate All Hold`."""
        return f"Gauntlet Stage {self.number} Door"


# Measured in game on 2026-10-03 (the mod's Keypad 5 dump): solving a stage's
# puzzle writes `ChallengeCompletedSystem`, whose savable system is
# GauntletChamber<N> for `Level<N>`. The chamber names are the game's own.
GAUNTLET_STAGES: tuple[GauntletStage, ...] = (
    GauntletStage(0, "GauntletChamber0", 50, "Peg Board"),
    GauntletStage(1, "GauntletChamber1", 51, "Pointers"),
    GauntletStage(2, "GauntletChamber2", 52, "Kick To Kick"),
    GauntletStage(3, "GauntletChamber3", 53, "Sim Press"),
    GauntletStage(4, "GauntletChamber4", 54, "Volley Ball"),
    GauntletStage(5, "GauntletChamber5", 55, "Invisible"),
    GauntletStage(6, "GauntletChamber6", 56, "Sculptures"),
)


PROGRESSIVE_GAUNTLET_ITEM_NAME = "Progressive Gauntlet Door"
PROGRESSIVE_GAUNTLET_VALUE = 60
"""Past the seven `GauntletChamberN` values (50..56): `BASE_ID + GAUNTLET_ID_OFFSET + 60`. Each copy
received opens the lowest stage still shut (`gauntlet_stage_items: progressive`)."""


def progressive_gauntlet_id() -> int:
    return BASE_ID + GAUNTLET_ID_OFFSET + PROGRESSIVE_GAUNTLET_VALUE


def gauntlet_stage_id(stage: GauntletStage) -> int:
    return BASE_ID + GAUNTLET_ID_OFFSET + stage.system_value


def gauntlet_needs_through(stage: GauntletStage) -> tuple[PuzzleNeed, ...]:
    """
    The items `lock_puzzle_needs` asks for before this stage's puzzle can be
    solved when every puzzle below it must be solved on the way up: its own,
    and those of every stage before it.
    """
    tags = frozenset().union(*GAUNTLET_STAGE_TAGS[:stage.index + 1])
    return tuple(need for need in PUZZLE_NEEDS if need.key in tags)


def gauntlet_needs_of(stage: GauntletStage) -> tuple[PuzzleNeed, ...]:
    """The same for this stage's puzzle alone, when the stages below it can be skipped."""
    tags = GAUNTLET_STAGE_TAGS[stage.index]
    return tuple(need for need in PUZZLE_NEEDS if need.key in tags)


def puzzle_needs(puzzle: Puzzle) -> tuple[PuzzleNeed, ...]:
    """The items `lock_puzzle_needs` asks for before this puzzle is in logic, in PUZZLE_NEEDS order."""
    tags = PUZZLE_TAGS[puzzle.prop_name]
    return tuple(need for need in PUZZLE_NEEDS if need.key in tags)


def tutorial_footholds(started: frozenset[str] | set[str]) -> tuple[PuzzleNeed, ...]:
    """
    The needs that, added to `started`, make one of the tutorial's puzzles
    doable on their own: the candidates for `start_with_random_puzzle_need`.
    Empty when `started` already opens one.
    """
    def opens(have: set[str]) -> bool:
        return any(all(need.key in have for need in puzzle_needs(puzzle))
                   for puzzle in PUZZLES if puzzle.prop_name in START_ZONE_PUZZLES)

    if opens(set(started)):
        return ()
    return tuple(need for need in PUZZLE_NEEDS if opens(set(started) | {need.key}))


ABSENT_FROM_THE_BUILD: tuple[str, ...] = (
    "gourdBunker",
    "gourdHighPegBoard",
    "gourdFirstPegBoard",
    "gourdMagiciansTrick",
    "gourdButtonBoothChallenge",
    "gourdTileSoup",
    "gourdPanopticon",
    "gourdMaypole",
    "gourdBlindfoldCircus",
    "gourdMessengerRun",
    "gourdHotPotato",
    "gourdScoutTiles",
    "gourdScoutCounting",
)
"""
Thirteen `SaveablePropName` values that exist in the game's metadata and
produce nothing in the build people are playing. They were locations here
until 2026-09-21, which meant a seed could put progression on a check that
can never be sent — the same class of bug as the chairlift, and beyond the
reach of any region to fix.

They are not the dev-only values: `gourdTesting00-39` and `gourdSecretZoneVice`
have no `valetXxx` home, and every one of these thirteen has one. They look
like cut or unreleased content, not scaffolding.

Four independent readings agree, and no measurement contradicts them:

- **A finished save.** A vanilla playthrough of 2026-08-23 holds exactly 45
  `gourd*` entries and not one of these. A gourd entry's value is the
  `SaveableHomeName` it sits in — `gourdHighButton = 1012` is `valetHighButton`,
  a gourd still at home — and all 45 of that save's values are monument slots,
  spread 4/5/5/5/5/6/15. That is every slot in the game, the Green Dome's
  fifteen included: the run went to the end and filled the island with 45
  gourds.
- **The economy.** 45 is also the total number of monument slots that exist.
  A 58-puzzle game would have thirteen gourds with nowhere to go.
- **A third party's inventory.** The document of 2026-08 listed 45 puzzles,
  and its 45 are exactly these 45 (see ../design-decisions.md, which read the
  difference as a content update adding thirteen — the opposite of what the
  save shows).
- **Every dump.** Ctrl+V instantiated 45 wherever it was pressed and never
  these, in zones that included the postgame.

The player, who has played the game, recognises none of the thirteen names.

**Checked against both builds on this machine (2026-09-22)**, because the
save above was played on one and the dumps were taken on the other: the game
of 2026-08-10 (the version the co-op sessions run on) and the game of
2026-09-07 carry an identical gourd inventory — 64 `gourd*` names in
`global-metadata.dat`, not one of them present in only one build. So the
thirteen are not content the September update added and the August save
simply predates. They produce nothing in either.

Kept here rather than deleted: if a future patch ships them, this is the list
to paste back into PUZZLES, ids and all.
"""


# --------------------------------------------------------------------------
# Towers, big keys and monuments
# --------------------------------------------------------------------------
# Cross-validated three ways (see ../../mod/reverse-engineering-notes.md, the
# "reference mapping table" section): the plinth mapping was confirmed in-game
# one key at a time, the tower names and deposit-box counts come from a
# third-party document, and the slot counts were counted in-game with the F6
# debug tool. All three agree.
#
# `slots` is the number of gourds that must be deposited in that tower's
# monument for its big key to become available in the vanilla game — which, in
# this world, is also how many `Gourd` items the logic requires.


class Tower(NamedTuple):
    prop_name: str
    """`SaveablePropName` enum name of the big key itself."""
    prop_value: int
    """`SaveablePropName` enum value; item id and location id are BASE_ID + this."""
    item_name: str
    """
    Player-facing name of the item that is the FEATURE this tower's key
    opens — the chairlift, the train, the map room.

    The physical key is a SECOND item, named from this one by
    `key_item_name()` from `key_name`. The two are different things and
    always have been: a key is a check carrier the players cut and place,
    and a feature is what actually opens.
    """
    key_name: str
    """
    What players call the tower this key belongs to — "Red Funnel Tower", "Green
    Dome Tower" — and so the prefix of the key item and of its checks: "Red Funnel
    Tower Key", "Red Funnel Tower Key Cut 1", "Red Funnel Tower Key Deposit". These
    are the community document's names (renamed 2026-10-03, ROADMAP U9); ids did not move. The drawbridge has
    no tower and no colour, so its key keeps the drawbridge's name.
    """
    location_name: str
    """Player-facing name of the location "this key was placed in its plinth"."""
    plinth_name: str
    """`SaveableHomeName` enum name of the plinth the key is deposited into."""
    monument_prefix: str
    """`SaveableHomeName` prefix of the monument whose slots unlock this key."""
    slots: int
    """Number of monument slots for this tower (F6 count, matches the doc)."""
    segments: int
    """
    Segments that must be cut out of this tower's key blank, each one a check.

    Measured in game on 2026-09-21 with the mod's Ctrl+K dump, not taken from
    the third-party document: five each on the drawbridge and the four
    coloured towers, and **none** on the Black Monolith and Green Dome keys,
    which are born finished with `covers = 0` and their Complete PropGroup
    already in `propGroups`. 25 in all.
    """


# THE ITEM IS THE FEATURE, NOT THE KEY (decided 2026-09-21, see
# ../design-decisions.md). One physical act used to carry three roles at once:
# pinning a key in its plinth was the location, the effect of the item, and
# what opened the door. The three are now split — the 25 cut segments and the
# 7 placements are locations, the key is an inert check carrier the player
# fores and places freely, and what arrives from Archipelago is the map room,
# the chairlift, the train, the tunnels, the drawbridge, the dam or the Green
# Dome itself.
#
# So these names are things, not keys. "Chairlift" is what the player receives
# and "Chairlift Key Deposit" is where they put the key that no longer opens
# it — the pairing is deliberate, and the old "Green Cup Key" style of name
# would now be a lie about what the item does.
#
# One feature name per tower, and everything else is derived from it: the key
# item is "<feature> Key", the five cuts are "<feature> Key Cut 1..5" and the
# placement is "<feature> Key Deposit". Three of the deposits used to be named
# after their PLINTH instead — "Train Station", "Tunnel", "Goodbye Keyhole" —
# inherited from the third-party document that first listed them (see the
# cross-validation table in ../../mod/reverse-engineering-notes.md, which
# quotes that document and is left alone). Renamed on 2026-09-21, before the
# datapackage was published and the names became awkward to change: a player
# holding a "Tunnels Key" should not have to learn that it belongs in the
# "Tunnel Key Deposit".
#
# A fourth was renamed the same way on 2026-09-22, and it is worth saying why
# it was missed the first time. The Green Dome tower's feature was called
# "Green Dome" — the only one of the seven named after its TOWER instead of
# what it opens, which is the Hub Secret Door. It read as correct because the
# tower really is called the Green Dome; it was caught when `goal:
# second_ending` was added and the name propagated into a description of the
# door that was simply wrong. The tower keeps its name everywhere it means
# the tower (`GREEN_DOME`, its monument); only the
# feature and the names derived from it moved.
#
# The fifth and last, on 2026-09-22: "Dam" became "Chapel Door". It was the
# only feature name never derived from what its key opens — it came from the
# third-party document's "Dam Key Deposit", which names where the key is PUT,
# and "dam" appears nowhere in the game's own metadata (no `Dam*` identifier
# exists in the il2cpp dump). What placing `bigKeyBoss` opens is the chapel
# door, and everything past the bell inside it: the field, the Gauntlet and
# the summit bell. Caught by the player, who read "Dam" in an item list and
# could not tell what it named.
#
# Both doors renamed again after the first alpha (2026-09-25), from players'
# feedback: "Chapel Door" became "Big Wall Door", since nobody called that
# building a chapel and the game's own achievement calls it the Big Wall; and
# "Hub Secret Door" became "Spawn Secret Door", since "hub" read as the Green
# Dome Tower while the door is the one players spawn beside.
#
# KEYS ARE NAMED AFTER THEIR TOWER'S COLOUR (2026-09-25), reversing the
# "<feature> Key" naming above for the keys and their checks only. The first
# alpha's players found the item list did not read like the game: a key is
# found at, cut along and placed at its TOWER, and the towers are what
# players name by colour. The features keep their names — they are what
# opens. The drawbridge has no tower, so its key stays the Drawbridge Key.
# "Green Cup", "Yellow Twist" and "Black Monolith" were never players'
# names; they survive only in the constants below.
DRAWBRIDGE_ITEM_NAME = "Drawbridge"
SPAWN_SECRET_DOOR_ITEM_NAME = "Spawn Secret Door"

TOWERS: tuple[Tower, ...] = (
    Tower("bigKeyIntro", 300, DRAWBRIDGE_ITEM_NAME, "Drawbridge", "Drawbridge Key Deposit",
          "bigKeyPlinthIntro", "monoumentIntro", 4, 5),
    Tower("bigKeyRedZone", 301, "Map Room", "Red Funnel Tower", "Red Funnel Tower Key Deposit",
          "bigKeyPlinthMapRoom", "monoument0", 5, 5),
    Tower("bigKeyGreenZone", 302, "Chairlift", "Green Cup Tower", "Green Cup Tower Key Deposit",
          "bigKeyPlinthSkiLift", "monoument1", 5, 5),
    Tower("bigKeyBlueZone", 303, "Train", "Blue Castle Tower", "Blue Castle Tower Key Deposit",
          "bigKeyPlinthTrain", "monoument2", 5, 5),
    Tower("bigKeyYellowZone", 304, "Tunnels", "Yellow Twist Tower", "Yellow Twist Tower Key Deposit",
          "bigKeyPlinthTunnels", "monoument3", 5, 5),
    Tower("bigKeyBoss", 305, "Big Wall Door", "Black Monolith Tower", "Black Monolith Tower Key Deposit",
          "bigKeyPlinthEnding", "monoumentFinal", 6, 0),
    Tower("bigKeyOverflow", 306, SPAWN_SECRET_DOOR_ITEM_NAME, "Green Dome Tower", "Green Dome Tower Key Deposit",
          "bigKeyPlinthGoodbye2", "monoumentOverflow", 15, 0),
)

TOTAL_CUT_SEGMENTS = sum(tower.segments for tower in TOWERS)
"""25 — the drawbridge and the four coloured towers, five each."""

# monoument0-3 are known to be the four five-slot towers, but which of the four
# is Red/Green/Blue/Yellow was never established in-game. It does not matter
# here: they have identical slot counts, so no rule can tell them apart.

GREEN_DOME = TOWERS[-1]
"""The Green Dome tower (`bigKeyOverflow`), the postgame/true-ending key."""

GREEN_CUP = TOWERS[2]
"""The Green Tower (`bigKeyGreenZone`); its feature is the chairlift."""

YELLOW_TWIST = TOWERS[4]
"""The Yellow Tower (`bigKeyYellowZone`); its feature is the tunnels."""

BLACK_MONOLITH = TOWERS[5]
"""The Black Tower (`bigKeyBoss`); its feature, the Big Wall Door, is the way to the ending."""

MAX_MONUMENT_SLOTS = sum(tower.slots for tower in TOWERS)
"""Every monument slot on the island: 4 + 5 + 5 + 5 + 5 + 6 + 15 = 45."""

# --------------------------------------------------------------------------
# Radio stations (SavableSystem.FmStation*)
# --------------------------------------------------------------------------
# The enum declares ten, but only seven were ever observed being written at
# once by the game's own dev helper (`PeckDevHelper.Trigger(unlocks: true)`,
# see the "Arch doors" section of the mod notes) — and those seven are exactly
# the ones with real names. FmStation7/8/9 look like unused placeholders, so
# they are left out: a location the game can never check makes a seed
# unbeatable, which is a far worse failure than three missing checks.
#
# A station is both a location and an item. Switching one on in the game is
# the check; the music itself only starts once the matching item arrives, so
# the radio stops being the one check in this world that rewards itself. The
# mod suppresses the local unlock for exactly as long as the item is missing
# (see ../protocol.md and the BroadcastStation notes in the mod).


class RadioStation(NamedTuple):
    system_name: str
    """`SavableSystem` enum name, the literal `SaveManager` key the game writes."""
    system_value: int
    """`SavableSystem` enum value; both ids are BASE_ID + RADIO_ID_OFFSET + this."""
    location_name: str
    """Player-facing name of the location "this station was switched on"."""
    item_name: str
    """Player-facing name of the item that actually starts the music playing."""


# NAMES DO NOT FOLLOW THE ENUM. Measured in game on 2026-09-21 (the mod's
# Ctrl+B dump): the `FmStation*` names are internal labels that mostly do not
# describe the music the station plays. `FmStationBreathwork` broadcasts
# `musicGroup_bobby`, `FmStationFourthSpace` broadcasts `musicGroup_breathwork`,
# `FmStationSleuthFm` broadcasts `musicGroup_FourthSpace`, and so on — only
# DanceFm and JourneyBeat line up with their own name.
#
# The names below are therefore taken from the MusicGroup each station really
# plays, because that is what the player receives. The game itself shows no
# station names at all, so nothing in the world contradicts this choice — it
# is an Archipelago convention, and the honest one.
#
# THE NUMBER IS WHAT A PLAYER CAN SEE (first alpha's feedback, 2026-09-25:
# "there is no way of telling what the station name is in-game"). The radio's
# dial is a row of lights with no labels (player, 2026-09-26), so a station
# is told apart by its place on it, counted from 1: `Radio Station 01: Bobby`
# is the first light. Places measured by the mod, which learns them from the
# world and keeps them in the save as position + 1: two of our saves
# (BeltWalk, CoopWalk3) and the first alpha's guest log agree on all seven.
#
# `system_name` stays the enum value: it is the SaveManager key and the id
# arithmetic, and it must never be "corrected" to match the display name. The
# ids do not move with the renaming either, so a seed generated before it
# still reports the same checks.

RADIO_STATIONS: tuple[RadioStation, ...] = (
    RadioStation("FmStationSleuthFm", 30, "Radio Station 02: Fourth Space", "Radio Music 02: Fourth Space"),
    RadioStation("FmStationKosmische", 31, "Radio Station 07: Bristol", "Radio Music 07: Bristol"),
    RadioStation("FmStationDanceFm", 32, "Radio Station 05: Dance FM", "Radio Music 05: Dance FM"),
    RadioStation("FmStationBreathwork", 33, "Radio Station 01: Bobby", "Radio Music 01: Bobby"),
    RadioStation("FmStationJourneyBeat", 34, "Radio Station 04: Journey Beat", "Radio Music 04: Journey Beat"),
    RadioStation("FmStationAFJ", 35, "Radio Station 06: Mallets", "Radio Music 06: Mallets"),
    RadioStation("FmStationFourthSpace", 36, "Radio Station 03: Breathwork", "Radio Music 03: Breathwork"),
)

# --------------------------------------------------------------------------
# What sits behind a big key
# --------------------------------------------------------------------------
# Found in play on 2026-09-21, and it is why regions.py gained two regions.
# Until then the world claimed the island was open apart from the ending,
# which let generation place the Green Cup Key behind the chairlift that key
# opens — an unbeatable seed, not a rough edge.
#
# These lists hold the game's own identifiers (`prop_name` for a puzzle,
# `system_name` for a station), not the player-facing names, so a later
# rename cannot silently empty them.
#
# MEASURED IN GAME on 2026-09-21, standing in each zone, with the mod's Ctrl+V
# and Ctrl+B dumps sorted by distance to the player. Presence was useless —
# the game instantiates every gourd and every station everywhere, so two dumps
# taken in two different zones came back byte-identical. Only distance answers
# "what is in here".

START_ZONE_PUZZLES: tuple[str, ...] = (
    "gourdHighButton",
    "gourdEasySimPress",
    "gourdTellerWindow",
    "gourdTelescopeToBox",
)
"""
`SaveablePropName` of each puzzle in the starting zone: the hub players spawn
at and the tutorial beside it, everything short of the drawbridge and the
first arch door.

Measured 2026-09-25 by distance to the tutorial's monument (143m to 215m),
and named by the player: the one you climb on each other for, the one of
buttons pressed together, the telescope and the teller window. Microphone
Array sits in the same band, at 173m, and is NOT in the zone — the player
settled it. As with the chairlift, distance narrows the question and the
player answers it.
"""

CHAIRLIFT_PUZZLES: tuple[str, ...] = (
    "gourdCannonballCommute",
    "gourdCharadesRooms",
    "gourdDancerAndSelecter",
    "gourdPoetAndPontiff",
    "gourdCenturionSeance",
    "gourdCabinFeverLong",
    "gourdSpeedObby",
)
"""
`SaveablePropName` of each puzzle past the chairlift: the seven purple
"variant challenge" gourds, and exactly those.

They were 57m to 293m from the player standing in the zone, and they are the
only seven `isVariantChallenge` gourds in the game. Four ordinary gourds sit
in the same band of distance — gourdTrapRoom, gourdPerspectiveCounting,
gourdMemoryBombs and gourdSignalFlags, 225m to 320m — and the player confirmed
all four are reachable without the chairlift, so they stay in the overworld.
Distance alone does not draw this boundary; it only narrows the question.
"""

CHAIRLIFT_RADIO: str | None = "FmStationSleuthFm"
"""
`SavableSystem` of the station past the chairlift.

129m away with the next one at 548m. Note that it is dial position 2, not 7 —
a first recollection of "position 7" would have gated FmStationKosmische, which
the same dump put 1189m away, the furthest of the seven.
"""

TUNNEL_RADIO: str | None = "FmStationDanceFm"
"""
`SavableSystem` of the station past the tunnels.

7.6m away with the next at 252m, and independently confirmed from the server
side: claiming it printed `RadioWalk sent Novelty Keychain to RadioWalk (Radio
Station: Dance FM)`. That name means the same station in the old naming and the
new one, so it pins the mapping with no room for doubt. (It has read `Radio
Station 05: Dance FM` since the dial numbers were added.)
"""


def chairlift_locations() -> tuple[str, ...]:
    """Player-facing names of every location behind the Chairlift."""
    return _locations_for(CHAIRLIFT_PUZZLES, CHAIRLIFT_RADIO)


def tunnel_locations() -> tuple[str, ...]:
    """Player-facing names of every location behind the Tunnels."""
    return _locations_for((), TUNNEL_RADIO)


def _locations_for(puzzle_prop_names: tuple[str, ...], radio_system: str | None) -> tuple[str, ...]:
    names = [puzzle.location_name for puzzle in PUZZLES if puzzle.prop_name in puzzle_prop_names]
    names += [station.location_name for station in RADIO_STATIONS if station.system_name == radio_system]
    return tuple(names)


# --------------------------------------------------------------------------
# Goal flags (SavableSystem)
# --------------------------------------------------------------------------
# Both were confirmed in-game (mod notes, session of 2026-09-10):
#   - EndingGate: written when the chapel bell is broken by two players.
#     Careful, it is the door's live open/closed state, not a latching flag —
#     it can go back to 0. The mod already treats the first non-zero write as
#     the trigger and never re-reads it.
#   - GauntletComplete: written when the second bell, at the top of the
#     Gauntlet, is broken. This is the real end-of-game check.

ENDING_GATE_SYSTEM = "EndingGate"
GAUNTLET_COMPLETE_SYSTEM = "GauntletComplete"

# --------------------------------------------------------------------------
# Filler and traps
# --------------------------------------------------------------------------
# The mod materializes three things today: a generic cosmetic gourd, a big key
# and a radio station. Everything below is inert by design — the client applies
# no effect for it — so these names exist purely to fill the pool with something
# readable for the other players in the multiworld.

FILLER_ITEMS: tuple[tuple[str, int], ...] = (
    ("Megaphone", 9_001),
    ("Walkie-Talkie", 9_002),
    ("Backpack", 9_003),
    ("Belt", 9_004),
    ("Flare Gun", 9_005),
    ("Laser", 9_006),
    ("Binoculars", 9_007),
    ("Compass", 9_008),
    ("Folding Map", 9_009),
    ("Radio", 9_010),
    ("Gourd Carton", 9_011),
    ("Torch", 9_012),
    ("Lamp", 9_013),
    ("Scanner", 9_014),  # "X-Ray Goggles" until 0.5.0 (the player, 2026-10-12)
    ("Blue Flare Gun", 9_015),
    ("Green Flare Gun", 9_016),
    ("Yellow Flare Gun", 9_017),
    ("Rainbow Flare Gun", 9_018),
)
"""
The island's own hand props, cloned in and spawned near the player on
receipt (mod/src/Core/GadgetItemSpawner.cs) rather than an invented object —
player decision, 2026-09-22. The order here is load-bearing: the mod derives
which gadget an id names from its position in this same list
(ApLocationIds.GadgetItemOrder), not from the name, so re-ordering this tuple
without a matching mod change silently swaps what two ids grant. Their
vanilla instances are removed from the map on the mod side for the same
reason a big key's plinth is emptied on pickup: a filler item must not also
be findable for free outside the multiworld.

The Rainbow Flare Gun (player, 2026-10-07) is the one object the island has none of: a white
flare gun whose shots run through every colour. It is always in the pool, once, and never drawn
as filler (FLARE_GUN_NAMES, items.py).
"""

FLARE_GUN_ITEM_NAMES: tuple[str, ...] = ("Flare Gun", "Blue Flare Gun", "Green Flare Gun", "Yellow Flare Gun")
"""The island's four flare guns as items: in the pool with `flare_gun_sanity`, never as filler."""

RAINBOW_FLARE_GUN_ITEM_NAME = "Rainbow Flare Gun"

PALETTE_SIZE = 10
"""
How many colours the mod's palette holds (mod/src/Core/Palette.cs): red, orange, yellow, lime,
green, cyan, blue, purple, pink, white. With `random_colors`, a seed draws between 5 and all of
them, by index, and the buoys and flare guns are painted from that draw.
"""

TELEPORT_ID_OFFSET = 2_700
"""Item-only, like the filler: the teleporters of `teleport_buttons: items`, `BASE_ID + 2_700 + position`."""

TELEPORT_DESTINATIONS: tuple[tuple[str, str], ...] = (
    ("red", "Red Funnel Tower"),
    ("green", "Green Cup Tower"),
    ("blue", "Blue Castle Tower"),
    ("yellow", "Yellow Twist Tower"),
    ("black", "Black Tower"),
    ("gauntlet", "Silent Gauntlet"),
)
"""
Where the in-world teleport buttons of the hub lead, by the key the mod knows each by and the
player-facing name. The Green Dome is not here: it is next to the spawn (player, 2026-10-03).
Every destination has a button back to the hub, always there.
"""


def teleport_item_name(destination: str) -> str:
    return f"{destination} Teleporter"


def teleport_item_id(position: int) -> int:
    return BASE_ID + TELEPORT_ID_OFFSET + position


# 9_201..9_213: the joke filler items (Baby, Boid, Plumbus...), retired so gourd_name can take those names. Not reused.


class Pickup(NamedTuple):
    """A backpack, belt or gourd carton of the island, whose first pick-up is a check (`pack_sanity`)."""

    guid: str
    """The prop's `savablePropGuid`: the game's own identity for it, the same on every machine."""
    location_name: str
    past_chairlift: bool = False
    past_tunnels: bool = False


PICKUP_ID_OFFSET = 13_000
"""A pick-up's location id: `BASE_ID + 13_000 + position` in PICKUPS."""

PICKUPS: tuple[Pickup, ...] = (
    Pickup("2d8101c4-223f-4939-8df6-809562469516", "Sky Blue Backpack"),
    Pickup("76601ed6-da1c-4326-9c8d-2c6168d31f36", "Yellow Backpack"),
    Pickup("730affd5-3139-473c-aaf2-4ecd5142c82f", "Teal Backpack"),
    Pickup("22009189-d33d-4e65-8b3c-c8257188743d", "Orange Backpack"),
    Pickup("f2dea8ab-08ea-434d-b98b-7e66a499392e", "Pink Backpack"),
    Pickup("02e75b25-ffa8-49e9-9fa3-7c453206aad9", "Maroon Backpack", past_chairlift=True),
    Pickup("9057af3c-12c5-48d8-8251-07233a203861", "Green Belt"),
    Pickup("d73ec09b-8e2b-432e-8e62-b5ff71db2d23", "Purple Belt", past_tunnels=True),
    Pickup("3bfe0dec-42de-4c31-87ab-a72f0b94722a", "Yellow Belt"),
    Pickup("ef09f445-cd71-4414-938a-7a7122dcd121", "Orange Belt"),
    Pickup("3e944e74-2d2e-40e2-b1ba-a1afa8c937ee", "Blue Belt", past_chairlift=True),
    Pickup("5fa8468a-dfaa-48fe-8057-46d47b9e56a5", "Gourd Carton Pickup"),
)
"""
Every backpack (6), belt (5, `HolsterProp`) and gourd carton (1) of the island, from the
census of 2026-10-05 (Ctrl+F1): all twelve are in every map version (2, 3 and 4 players).
Named by colour (player, 2026-10-05), the `_TintColor` each carries (census, pickup-colours.tsv):
no two of a kind share one; the carton has none, and its location says Pickup so as not to read like the item. Every one walked to by the player on 2026-10-05: the far-east
pair is past the chairlift (like the purple gourds), the belt at (63, 59, -390) in the
tunnels, and nothing else has a way in other than the open island.
"""


FLARE_GUN_ID_OFFSET = 13_050
"""A flare gun pick-up's location id: `BASE_ID + 13_050 + position` in FLARE_GUN_PICKUPS."""

FLARE_GUN_PICKUPS: tuple[Pickup, ...] = (
    Pickup("80381b52-ede7-494f-b800-0c77a71f57e0", "Red Flare Gun Pickup"),
    Pickup("72bc4c22-4418-4b7c-9c1d-e8cfa5040102", "Green Flare Gun Pickup"),
    Pickup("66de92ed-177d-49d5-aa97-e86d98083ac0", "Yellow Flare Gun Pickup"),
    Pickup("8b9fc973-4200-4934-8169-dde2de5b1d10", "Blue Flare Gun Pickup"),
)
"""
The island's four flare guns (census of 2026-10-07, Ctrl+F1), whose first pick-up is a check with
`flare_gun_sanity`: in every map version, all four on the open island (walked to by the player,
2026-10-07). Named Pickup, like the carton, so as not to read like the items.
"""


class Firework(NamedTuple):
    """A firework launcher, whose first firing (the button at its foot) is a check (`firework_sanity`)."""

    key: str
    """The landmark the launcher stands in, as the mod finds it among the launcher's parents."""
    location_name: str
    past_chairlift: bool = False
    in_starting_zone: bool = False


FIREWORK_ID_OFFSET = 13_100
"""A firework's location id: `BASE_ID + 13_100 + position` in FIREWORKS."""

FIREWORKS: tuple[Firework, ...] = (
    Firework("Hub", "Hub Fireworks", in_starting_zone=True),
    Firework("Lookout_RedCone", "Red Funnel Tower Fireworks"),
    Firework("Lookout_YellowZigzag", "Yellow Twist Tower Fireworks"),
    Firework("Lookout_BlueTube", "Blue Castle Tower Fireworks"),
    Firework("Lookout_GreenHourglass", "Green Cup Tower Fireworks"),
    Firework("PeakMarker", "Peak Fireworks"),
    Firework("SouthPeak", "South Peak Fireworks"),
    Firework("FireworksPlatform_Intro", "Intro Platform Fireworks"),
)
"""
The eight firework launchers (census of 2026-10-05), in every map version. The game saves
nothing when one is fired: the mod sees its button go to state 1 (`PokeButtonPeckLogic`
under `FireworkLauncher`), measured at the hub's. All on the open island but the hub's, in
the starting zone (walked to by the player, 2026-10-05).
"""


TOWER_REACHED_ID_OFFSET = 13_200
"""`BASE_ID + TOWER_REACHED_ID_OFFSET + the tower's position in reached_towers()`: a check for reaching it."""


def reachable_towers() -> tuple[Tower, ...]:
    """
    The towers a player can walk up to and be seen at: the four coloured ones and the Black Tower. Not the
    drawbridge, which has no tower, nor the Green Dome, whose place in the world was never measured.
    """
    return tuple(tower for tower in TOWERS if tower.prop_name not in ("bigKeyIntro", "bigKeyOverflow"))


def reached_towers() -> tuple[Tower, ...]:
    """
    Every tower with a "reached" check, in the order of their ids: the five with a button, then the drawbridge and the
    Green Dome, reached by position (measured in game, 2026-10-12). Appended, so the first five keep their ids.
    """
    return (*reachable_towers(), tower_by_prop("bigKeyIntro"), tower_by_prop("bigKeyOverflow"))


def tower_reached_location_name(tower: Tower) -> str:
    return f"Reached the {tower.key_name}"


MONUMENT_FULL_ID_OFFSET = 13_800
"""`BASE_ID + 13_800 + the tower's position in TOWERS`: its monument full (ROADMAP L1, 0.6)."""


def monument_full_location_name(tower: Tower) -> str:
    return f"{tower.key_name} Monument Full"


def monument_full_location_id(tower: Tower) -> int:
    return BASE_ID + MONUMENT_FULL_ID_OFFSET + TOWERS.index(tower)


def tower_reached_location_id(tower: Tower) -> int:
    return BASE_ID + TOWER_REACHED_ID_OFFSET + reached_towers().index(tower)


PROGRESSIVE_MONUMENT_ITEM_NAME = "Progressive Monument"
PROGRESSIVE_MONUMENT_ID_OFFSET = 13_300


def progressive_monument_id() -> int:
    """`BASE_ID + 13_300`: one id, for every copy (`tower_mode: by_item` holds one fewer than there are towers)."""
    return BASE_ID + PROGRESSIVE_MONUMENT_ID_OFFSET


class Ability(NamedTuple):
    """The player's body as an item, with `lock_abilities` (ROADMAP R3, 0.6)."""

    key: str
    """What slot_data calls it; the mod's AbilityLocks grants by it."""
    item_name: str
    copies: int
    """How many the pool holds: a progressive arm has three, raising, then pointing, then extending."""
    handed: bool
    """With `lock_abilities: handed` (one arm per item)."""
    ambidextrous: bool
    """With `lock_abilities: ambidextrous` (both arms per item)."""


ABILITY_ID_OFFSET = 13_400
"""Item-only: `BASE_ID + 13_400 + position` in ABILITIES. The order is load-bearing."""

ABILITIES: tuple[Ability, ...] = (
    # Decided by the player, 2026-10-12, over the community document's ten: fewer items, each one felt.
    # Legs: jumping and crouching. Seat: a chair and the floor. An arm: raising (Q / E), then pointing
    # (a click held, hand empty), then extending (raised and pointing), as measured in game.
    Ability("legs", "Legs", 1, True, True),
    Ability("seat", "Seat", 1, True, True),
    Ability("left_arm", "Progressive Left Arm", 3, True, False),
    Ability("right_arm", "Progressive Right Arm", 3, True, False),
    Ability("arms", "Progressive Arms", 3, False, True),
    # Added the same day (player): throwing (a wound-up drop), carrying another player, and running, the last
    # only with `lock_running`.
    Ability("throwing", "Throwing", 1, True, True),
    Ability("carrying", "Carrying", 1, True, True),
    Ability("running", "Running", 1, True, True),
)

ABILITY_LOCK_KEYS: dict[str, str] = {
    "legs": "legs", "seat": "seat", "left_arm": "arms", "right_arm": "arms", "arms": "arms",
    "throwing": "throwing", "carrying": "carrying",
}
"""The `start_with_abilities` key each ability item answers to (Running has its own option, `lock_running`)."""

LEGS_ITEM_NAME = "Legs"
"""
What leaving the starting zone asks for with `lock_abilities` (the player, 2026-10-12: jumping and crouching are
needed nearly everywhere on the island). Asked for at the way out, it comes early.
"""
SEAT_ITEM_NAME = "Seat"
LEFT_ARM_ITEM_NAME = "Progressive Left Arm"
RIGHT_ARM_ITEM_NAME = "Progressive Right Arm"
ARMS_ITEM_NAME = "Progressive Arms"
THROWING_ITEM_NAME = "Throwing"
CARRYING_ITEM_NAME = "Carrying"
RUNNING_ITEM_NAME = "Running"


class Body(NamedTuple):
    """What a place asks of the players' bodies with `lock_abilities`."""

    seat: bool = False
    arms: int = 0
    """Copies of each arm (both arms): ARM_RAISING, ARM_POINTING or ARM_EXTENDING; 0 for none."""
    throwing: bool = False
    carrying: bool = False

ARM_RAISING, ARM_POINTING, ARM_EXTENDING = 1, 2, 3
"""How many copies of a progressive arm each gesture needs."""

POSES = Body(seat=True, arms=ARM_EXTENDING)
"""
"Poses" (the player): sitting and the arm poses, raised and extended, both arms, by both players at once.
Abilities go to every player, so one item is enough for both.
"""

ABILITY_PUZZLES: dict[str, Body] = {
    # The puzzle tour of 2026-10-12 (2 players, docs/PUZZLE-TOUR.md). Jumping and crouching (High Button,
    # Obby, Speed Obby, Blindfold Catwalk) are covered by Legs at the way out. Basketball: carrying helps,
    # it is not needed.
    "gourdHighButton": Body(carrying=True),
    "gourdFielding": Body(throwing=True),
    "gourdInvisibleInk": Body(arms=ARM_RAISING),
    "gourdObby": Body(carrying=True),
    "gourdCarousel": Body(seat=True),
    "gourdWindowLabyrinth": Body(throwing=True),
    "gourdBasketball": Body(throwing=True),
    "gourdConcert": POSES,
    "gourdIndoorSemaphore": POSES,
    "gourdOpticalTelegraph": POSES,
    # Checked in 3 and 4 players the same day: Charades and Pointer's Paradise ask for both arms pointing and the
    # poses (in 2 players too), and Pointer's Paradise for carrying a player as well (3 and 4 players; the logic
    # cannot tell the player count, so it asks for it always). The Gauntlet is the same in every version.
    "gourdCharadesRooms": POSES,
    "gourdPointersParadise": Body(seat=True, arms=ARM_EXTENDING, carrying=True),
    "gourdKickUpPits": Body(throwing=True),
    "gourdDancerAndSelecter": POSES,
    "gourdSpeedObby": Body(carrying=True),
    "gourdCannonballCommute": Body(seat=True),
}

GAUNTLET_STAGE_ABILITIES: tuple[Body, ...] = (
    Body(arms=ARM_EXTENDING), Body(), Body(throwing=True), Body(), Body(throwing=True), Body(arms=ARM_EXTENDING), POSES,
)
"""The same, for the Silent Gauntlet's seven stages, in order (tour of 2026-10-12)."""


def ability_item_id(ability: Ability) -> int:
    return BASE_ID + ABILITY_ID_OFFSET + ABILITIES.index(ability)


class IslandObject(NamedTuple):
    """A gadget of the island whose pick-up is a check with `object_sanity` (ROADMAP S7, 0.6)."""

    guid: str
    """The prop's `savablePropGuid`, the same on every machine."""
    kind: str
    """The prop's name in the game, which the mod matches a pick-up by (`first_pickup`)."""
    location_name: str
    zone: str
    """
    Where it lies: start (the tutorial), overworld, chairlift, tunnels, map_room (behind the Map Room's
    door, its item), or tower:<big key prop> (a tower's roof and stairs: the far door of that tower).
    """


OBJECT_ID_OFFSET = 13_500
"""`object_sanity: every`: `BASE_ID + 13_500 + position` in ISLAND_OBJECTS."""
OBJECT_KIND_ID_OFFSET = 13_700
"""`object_sanity: first_pickup`: `BASE_ID + 13_700 + position` in OBJECT_KINDS (13_500..13_699 left to the objects)."""

ISLAND_OBJECTS: tuple[IslandObject, ...] = (
    # Every gadget of the island in all three versions of the map (object census, 2026-10-12, Ctrl+Shift+5),
    # placed by the player: the Map Room's bench is inside its door, the Secret Salon and the tunnel outlets'
    # torches are in the tunnels, the Spiral megaphone is past the chairlift, Ocean Treasure is not.
    # Not here: the packs and flare guns (`pack_sanity`, `flare_gun_sanity`), the lamps, the puzzles' tools.
    IslandObject("6364d493-5c48-4480-a02d-ae086ef06825", "BinocularsProp", "Binoculars (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("88942e8e-6fae-4d71-93f0-8500741a82d1", "BinocularsProp", "Binoculars (Carousel)", "overworld"),
    IslandObject("16e1e4a2-aadf-43e1-a67b-f2513dd63303", "BinocularsProp", "Binoculars (Green Cup Tower)", "tower:bigKeyGreenZone"),
    IslandObject("ed2f8967-b4c1-43d9-91b5-11679be10bc6", "BinocularsProp", "Binoculars (Peak Marker)", "overworld"),
    IslandObject("f40709b7-b15b-4131-ab88-ae08571224ac", "BinocularsProp", "Binoculars (Red Funnel Tower)", "tower:bigKeyRedZone"),
    IslandObject("eed83af3-1958-4718-a634-5280bdc6b3cb", "BinocularsProp", "Binoculars (South Peak)", "chairlift"),
    IslandObject("6debc10d-8e02-40d5-a5be-bfc992b89a9a", "BinocularsProp", "Binoculars (Yellow Twist Tower)", "tower:bigKeyYellowZone"),
    IslandObject("1c9f8bc9-5850-4085-80f6-05c2821b505d", "ClockProp", "Clock (Green Diffuser Train Stop)", "overworld"),
    IslandObject("44015a0e-20ca-441a-b6e9-bbd28657b5f3", "ClockProp", "Clock (Ring Room Train Stop)", "overworld"),
    IslandObject("e56da5eb-bac5-4c5e-b489-a8de3d204e7c", "ClockProp", "Clock (South Valley Beach Train Stop)", "overworld"),
    IslandObject("209a7dc3-9dcb-425e-86c5-3a84aa9bd0ec", "ClockProp", "Clock (Sports Valley Train Stop)", "overworld"),
    IslandObject("f468d4dc-594e-4c62-80d4-3a85d9e25b3e", "ClockProp", "Clock (Trap Room Train Stop)", "overworld"),
    IslandObject("a08daa18-2fef-4a49-a062-2baafdf48686", "ClockProp", "Clock (Upper Hub Train Stop)", "overworld"),
    IslandObject("d793e0fe-a145-496f-a348-2d8a65add9e8", "CompassProp", "Compass (Map Room)", "map_room"),
    IslandObject("672cd949-1e5b-465a-a1cb-3860594f9e9f", "FoldingMapProp", "Folding Map (Map Room)", "map_room"),
    IslandObject("cf0fd97f-0f39-4b0c-9474-54c665aeb2e9", "CowBellLargeProp", "Large Cowbell (Tutorial)", "start"),
    IslandObject("f68a6dbc-0b73-47b6-b9e6-fd28e016606e", "LaserProp", "Laser (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("b2913ee0-2c3f-4582-a2f5-6c9f33b40b3b", "LaserProp", "Laser (Green Cup Tower)", "tower:bigKeyGreenZone"),
    IslandObject("c7ba38c6-f0fe-45fd-ad4a-6d1907b408ae", "LaserProp", "Laser (Map Room 1)", "map_room"),
    IslandObject("cb8326bc-26c2-4214-8dc4-3ebafe0b5603", "LaserProp", "Laser (Map Room 2)", "map_room"),
    IslandObject("7c788690-d373-4b31-9be9-31bbb3ec1f88", "LaserProp", "Laser (Map Room 3)", "map_room"),
    IslandObject("8fbe1de2-1ea0-4da4-8ef5-282f5d19b492", "LaserProp", "Laser (Red Funnel Tower)", "tower:bigKeyRedZone"),
    IslandObject("e69463b0-e27c-4247-93e4-50df64cd12ad", "LaserProp", "Laser (Yellow Twist Tower)", "tower:bigKeyYellowZone"),
    IslandObject("a418a7cd-0945-44c1-9f7b-14eac4584cf1", "CowBellMediumProp", "Medium Cowbell (Tutorial)", "start"),
    IslandObject("7e2f3ba8-2124-4fe9-8707-1621be5a0926", "MegaphoneProp", "Megaphone (Pyramid Holder)", "overworld"),
    IslandObject("c6259d07-2499-4063-b033-b72cddd951c2", "MegaphoneProp", "Megaphone (Ramp Holder)", "overworld"),
    IslandObject("48da7ac6-988f-4f4b-a376-3686fe550f69", "MegaphoneProp", "Megaphone (Spiral Holder)", "chairlift"),
    IslandObject("2835fac9-81b1-4312-a576-cc977e6e8c85", "FmRadioProp", "Radio (Abbey Music Garden)", "chairlift"),
    IslandObject("21ab205e-db90-452b-b620-8f64ebcabd63", "FmRadioProp", "Radio (Doof Chamber)", "overworld"),
    IslandObject("ec17f547-5b1f-4fcf-828e-f2932dc2f620", "FmRadioProp", "Radio (Factory Music Garden)", "overworld"),
    IslandObject("d8e70c55-9800-498e-af4e-7054093950e4", "FmRadioProp", "Radio (Floorplan Music Garden)", "overworld"),
    IslandObject("87dbbb84-5c58-43a4-96ba-e43c1c7308b9", "FmRadioProp", "Radio (Greensteps Music Garden)", "overworld"),
    IslandObject("41e7b481-2e10-4d62-9136-5e6e0ba9fa7d", "FmRadioProp", "Radio (Lighthouse Music Garden)", "overworld"),
    IslandObject("070cf8f2-6824-413e-9c2a-43a1027c24c5", "FmRadioProp", "Radio (Musical Holiday)", "overworld"),
    IslandObject("ea8c71ac-810f-4426-84a1-17e0e3b0b81d", "FmRadioProp", "Radio (Peak Marker)", "overworld"),
    IslandObject("ebdec567-58c0-48c7-a62a-bbd843bef36f", "FmRadioProp", "Radio (Upper Hub)", "overworld"),
    IslandObject("b4dbfae3-89e9-45c0-9dd0-3fd815cd3904", "XrayGogglesProp", "Scanner (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("044daaf7-fb90-4f73-a9bb-00b8a3df9c60", "XrayGogglesProp", "Scanner (Fireworks Platform)", "overworld"),
    IslandObject("ed7b657e-5949-4a1e-8b82-e50ad2a8d785", "XrayGogglesProp", "Scanner (Green Cup Tower)", "tower:bigKeyGreenZone"),
    IslandObject("0132c70b-9a0c-4b5a-be06-b9bb077a6e5a", "XrayGogglesProp", "Scanner (Red Funnel Tower)", "tower:bigKeyRedZone"),
    IslandObject("c420401c-ba23-4e0c-aeac-2ea3ebcafa26", "XrayGogglesProp", "Scanner (South Peak)", "chairlift"),
    IslandObject("28cf2386-5e54-4b7e-99ba-283298aa3816", "XrayGogglesProp", "Scanner (Upper Hub)", "overworld"),
    IslandObject("9fadd6c5-af79-437f-80dd-ebb74d8526d4", "XrayGogglesProp", "Scanner (Yellow Twist Tower)", "tower:bigKeyYellowZone"),
    IslandObject("307d526d-780f-4fe2-b3fd-95f74b86319c", "CowBellSmallProp", "Small Cowbell (Tutorial)", "start"),
    IslandObject("381abbdb-da55-4243-a5df-692acd3fb2c0", "TorchProp", "Torch (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("cf899912-898f-4486-86ae-91ed78025a9d", "TorchProp", "Torch (Blue Tunnel Outlet)", "tunnels"),
    IslandObject("b25443e0-ec2d-4b55-b8e9-01ac48604ba5", "TorchProp", "Torch (Green Cup Tower)", "tower:bigKeyGreenZone"),
    IslandObject("57632bfc-688b-40e4-aabd-d020f489b291", "TorchProp", "Torch (Green Tunnel Outlet)", "tunnels"),
    IslandObject("d6270f5f-1ab7-4526-9e64-cccebde9389d", "TorchProp", "Torch (Red Funnel Tower)", "tower:bigKeyRedZone"),
    IslandObject("cbbf5ba7-0026-4737-9547-2e69187a2845", "TorchProp", "Torch (Red Tunnel Outlet)", "tunnels"),
    IslandObject("ac52bd4b-990f-4908-999d-5badeb8b6eda", "TorchProp", "Torch (Tutorial Exit)", "overworld"),
    IslandObject("56a5d535-d1a6-4ada-a1b3-cf6951bb2e66", "TorchProp", "Torch (Yellow Tunnel Outlet)", "tunnels"),
    IslandObject("b459af97-5a07-4024-8950-02f752c5292b", "TorchProp", "Torch (Yellow Twist Tower)", "tower:bigKeyYellowZone"),
    IslandObject("cee6114c-2dc0-4d02-a393-5686a2f2e9b8", "WalkieTalkieProp", "Walkie-Talkie (Back Beach Hill, Yellow)", "overworld"),
    IslandObject("56a7313e-9e55-485b-ac52-fcc6f8030133", "WalkieTalkieProp", "Walkie-Talkie (Beach, Blue)", "overworld"),
    IslandObject("cd159c6e-88aa-4158-a11f-2790cf581052", "WalkieTalkieProp", "Walkie-Talkie (Ocean Treasure, Yellow)", "overworld"),
    IslandObject("00d7ed74-a4d9-4969-b8e2-bf8d4975ac8a", "WalkieTalkieProp", "Walkie-Talkie (Ocean Treasure, Blue)", "overworld"),
    IslandObject("6efb6db4-76c1-4ffa-b249-8badc6d4bf82", "WalkieTalkieProp", "Walkie-Talkie (Peak, Blue)", "overworld"),
    IslandObject("a5992995-191a-45b7-aa23-e9a64a4fede7", "WalkieTalkieProp", "Walkie-Talkie (Peak, Yellow)", "overworld"),
    IslandObject("185e3864-0dcb-498b-baee-6da1a4b21a8e", "WalkieTalkieProp", "Walkie-Talkie (Red Cone Hill, Yellow)", "overworld"),
    IslandObject("f29614f5-0c70-4ed8-b152-9e57b371f20d", "WalkieTalkieProp", "Walkie-Talkie (Red Cone Valley, Blue)", "overworld"),
    # The paintbrushes, back as a kind of their own (player, 2026-10-13): left out by default, and never taken
    # off the island, picked up or left out (the Salon needs them).
    IslandObject("52fedcd9-caa6-4ec6-a285-f73e6ff40f50", "SalonBrushProp", "Paintbrush (Salon)", "overworld"),
    IslandObject("64d92f9a-e44c-4218-ab98-4ea48d0c9b33", "SalonBrushProp", "Paintbrush (Secret Salon)", "tunnels"),
    # The lamps (ROADMAP S9, player 2026-10-13): the island's own, on their pedestals, the towers' and the tutorial's;
    # never a puzzle's (a lamp under a puzzle's landmark is one of its tools). A pedestal is placed by the puzzle it
    # stands nearest to; one nearest a puzzle past the chairlift counts as past it (the safe side).
    IslandObject("1cdcd9a1-0343-4234-b1d9-7be646dec4ae", "BuoyProp", "Lamp (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("24d2f9a5-5376-4a7c-b08c-17e9c060855b", "BuoyProp", "Lamp (Diffuser Peak)", "overworld"),
    IslandObject("fcda7d44-81af-4526-be44-f16a90e5b0ec", "BuoyProp", "Lamp (Green Cup Tower)", "tower:bigKeyGreenZone"),
    IslandObject("98c2cd14-bbcf-4066-b58c-d7d23d2757f1", "BuoyProp", "Lamp (South Peak 1)", "chairlift"),
    IslandObject("d252953b-ebd1-484a-a555-953bccb904c0", "BuoyProp", "Lamp (South Peak 2)", "chairlift"),
    IslandObject("2141d27e-647c-401f-9a0c-a3c1aa029434", "BuoyProp", "Lamp (Tutorial 1)", "start"),
    IslandObject("0cb88fcc-6d33-4b08-9e5e-124a34f7f15b", "BuoyProp", "Lamp (Tutorial 2)", "start"),
    IslandObject("0460483f-1636-4ee6-8635-2168a9acaf34", "BuoyProp", "Lamp (Tutorial 3)", "start"),
    IslandObject("4df70c57-8d1b-447e-bad5-5aa66c1169f4", "BuoyProp", "Lamp (Tutorial 4)", "start"),
    IslandObject("1ff0231c-4933-4bec-b878-4e6f1fe88856", "BuoyProp", "Lamp (Yellow Twist Tower)", "tower:bigKeyYellowZone"),
    IslandObject("c5e792c4-080b-4377-8d68-f09c00debc91", "BuoyProp", "Lamp (near Cabin Fever Long)", "chairlift"),
    IslandObject("b5461e87-1a09-4f2c-b9b5-158f195adbf3", "BuoyProp", "Lamp (near Cannonball Commute)", "chairlift"),
    IslandObject("5d10fec3-7a2d-4bfd-be0e-6196152d88f5", "BuoyProp", "Lamp (near Cannonball 1)", "overworld"),
    IslandObject("12a7f64b-8175-4857-a371-77dc2989cd68", "BuoyProp", "Lamp (near Cannonball 2)", "overworld"),
    IslandObject("b95078fd-d8f2-4fd9-b2aa-ecf8b84fdad7", "BuoyProp", "Lamp (near Centurion Song)", "overworld"),
    IslandObject("59a73457-6368-4639-8ec7-335902797343", "BuoyProp", "Lamp (near Coordinates)", "overworld"),
    IslandObject("f7f2e9ed-14fd-4a54-a918-fc06b444a39e", "BuoyProp", "Lamp (near Easy Simultaneous Press)", "overworld"),
    IslandObject("80e38f3e-d054-4187-9675-66d6e70c8356", "BuoyProp", "Lamp (near Egg Hunt)", "overworld"),
    IslandObject("cec1412b-4727-48c4-b6a1-5a5e7989af18", "BuoyProp", "Lamp (near High Button 1)", "overworld"),
    IslandObject("94876c24-50ba-41cb-8fdd-9bd026f3be32", "BuoyProp", "Lamp (near High Button 2)", "overworld"),
    IslandObject("087f1e73-309b-4db2-8e39-dee6c926ad59", "BuoyProp", "Lamp (near Invisible Ink)", "overworld"),
    IslandObject("df814b39-e550-4e17-ad44-e32028ab3c47", "BuoyProp", "Lamp (near Kick Up Pits)", "overworld"),
    IslandObject("f4819494-6b1d-48ef-823b-4b26d2ceda53", "BuoyProp", "Lamp (near Memory Bombs)", "overworld"),
    IslandObject("13956d25-97a1-4b5c-9992-00c187092476", "BuoyProp", "Lamp (near Musical Holiday)", "overworld"),
    IslandObject("982f4dbf-22bb-4553-aa9c-dd6588c19fda", "BuoyProp", "Lamp (near Optical Telegraph)", "overworld"),
    IslandObject("3bfaaa40-7101-4f52-ad72-a283a8e39cac", "BuoyProp", "Lamp (near Poet and Pontiff)", "chairlift"),
    IslandObject("17e22f02-4f01-4aa4-83fb-d2871acd97cd", "BuoyProp", "Lamp (near Pointer's Paradise)", "overworld"),
    IslandObject("6a673617-992d-49ca-87f9-223aef4c9fe4", "BuoyProp", "Lamp (near Scout Bombs)", "overworld"),
    IslandObject("6beb48a3-60de-41e8-a9f6-d39772bcdfa4", "BuoyProp", "Lamp (near Singer and Selector)", "overworld"),
    IslandObject("d3e57e13-a91d-492c-bd46-2c59da8bef9e", "BuoyProp", "Lamp (near Telescope to Box)", "overworld"),
    IslandObject("5ee8fa69-8937-4841-b9a7-ee8a10c3a933", "BuoyProp", "Lamp (near Teller Window 1)", "overworld"),
    IslandObject("4b9cad35-ab44-4dfe-9e4b-390cb165aca5", "BuoyProp", "Lamp (near Teller Window 2)", "overworld"),
    IslandObject("632fc258-f424-4b88-930a-a62c45044965", "BuoyProp", "Lamp (near Trap Room)", "overworld"),
    IslandObject("d9f93a0a-19e3-4702-b5be-7b79e39d32c8", "BuoyRedProp", "Red Lamp (Tutorial 1)", "start"),
    IslandObject("1a014620-29dd-4517-8990-3854045129bd", "BuoyRedProp", "Red Lamp (Tutorial 2)", "start"),
    IslandObject("abc5a86c-4618-4bf6-bdba-e97e488ef81e", "BuoyRedProp", "Red Lamp (Tutorial 3)", "start"),
    # The pedestal lamps (BuoyLight, on the red pedestals), found picked up in play on 2026-10-13: a second
    # lamp model, the same rules as the lamps above.
    IslandObject("0537b69a-6e49-406a-b93c-d6bc7f6cc4b3", "BuoyLight", "Pedestal Lamp (Blue Castle Tower)", "tower:bigKeyBlueZone"),
    IslandObject("dab5cfc8-f4d9-44f2-abb0-90bc42b3cade", "BuoyLight", "Pedestal Lamp (Diffuser Peak)", "overworld"),
    IslandObject("eaa97883-12ad-4e8e-9f6d-cdf7f1a3a061", "BuoyLight", "Pedestal Lamp (Green Cup Tower 1)", "tower:bigKeyGreenZone"),
    IslandObject("68287599-1f13-42f0-9424-cabd187e8a55", "BuoyLight", "Pedestal Lamp (Green Cup Tower 2)", "tower:bigKeyGreenZone"),
    IslandObject("52b0dfcb-4588-4ce7-a78c-bdb9cb800fe6", "BuoyLight", "Pedestal Lamp (Peak Marker 1)", "overworld"),
    IslandObject("f84cb1c7-1702-4fa1-9ddb-f4c334041237", "BuoyLight", "Pedestal Lamp (Peak Marker 2)", "overworld"),
    IslandObject("cd88c773-6375-42c7-9475-76b288bd1c91", "BuoyLight", "Pedestal Lamp (Peak Marker 3)", "overworld"),
    IslandObject("1b047167-6e68-49c3-abf2-e15fbbeca0a5", "BuoyLight", "Pedestal Lamp (Red Funnel Tower 1)", "tower:bigKeyRedZone"),
    IslandObject("d27fe6ce-2b92-42bd-8d60-8885e368877c", "BuoyLight", "Pedestal Lamp (Red Funnel Tower 2)", "tower:bigKeyRedZone"),
    IslandObject("b28ac861-6c5d-4702-8b11-ef38df1eac22", "BuoyLight", "Pedestal Lamp (South Peak 1)", "chairlift"),
    IslandObject("4cd9c5fc-7414-4bfc-b4a2-6fb4fa0312c5", "BuoyLight", "Pedestal Lamp (South Peak 2)", "chairlift"),
    IslandObject("3d09137d-fbbd-4cb8-a17a-05de7676f61c", "BuoyLight", "Pedestal Lamp (Yellow Twist Tower 1)", "tower:bigKeyYellowZone"),
    IslandObject("d0f4305f-b577-41f7-a879-18be53553509", "BuoyLight", "Pedestal Lamp (Yellow Twist Tower 2)", "tower:bigKeyYellowZone"),
    IslandObject("60412245-4b6c-47eb-a4e7-67eec146bf20", "BuoyLight", "Pedestal Lamp (near Basketball)", "overworld"),
    IslandObject("b2f9b7af-39f5-431c-9908-c2ebf6d6453b", "BuoyLight", "Pedestal Lamp (near Breadcrumb Loop 1)", "overworld"),
    IslandObject("16990dcc-ebb8-453e-88fc-6478e3980728", "BuoyLight", "Pedestal Lamp (near Breadcrumb Loop 2)", "overworld"),
    IslandObject("db6fc3f6-366c-4cf9-81f1-9c45cbdceef4", "BuoyLight", "Pedestal Lamp (near Cabin Fever Long)", "chairlift"),
    IslandObject("cf2e3191-4c77-4ce9-8c56-f43a73d6e531", "BuoyLight", "Pedestal Lamp (near Centurion Song)", "overworld"),
    IslandObject("080554bb-bc16-41cd-bc96-4765cec96beb", "BuoyLight", "Pedestal Lamp (near Egg Hunt)", "overworld"),
    IslandObject("d0a1c556-d62f-42df-a36e-019636da7679", "BuoyLight", "Pedestal Lamp (near Fielding)", "overworld"),
    IslandObject("5efbf72b-9dff-4251-a536-f625ba8ff315", "BuoyLight", "Pedestal Lamp (near High Button 1)", "overworld"),
    IslandObject("fee806b8-8617-49f8-99af-bdea0c148ed0", "BuoyLight", "Pedestal Lamp (near High Button 2)", "overworld"),
    IslandObject("4ec6aca0-9961-4550-9c26-7cacb9917566", "BuoyLight", "Pedestal Lamp (near High Button 3)", "overworld"),
    IslandObject("b46c590f-7038-4033-b84d-e3fb664e095c", "BuoyLight", "Pedestal Lamp (near Musical Holiday 1)", "overworld"),
    IslandObject("d069a848-37f5-40e0-ac18-64516951f481", "BuoyLight", "Pedestal Lamp (near Musical Holiday 2)", "overworld"),
    IslandObject("c7141d1b-96bf-4c2c-8c05-3d2067e64a93", "BuoyLight", "Pedestal Lamp (near Musical Holiday 3)", "overworld"),
    IslandObject("11c1d018-3dbf-46d4-b1de-f9aba4d29fc2", "BuoyLight", "Pedestal Lamp (near Observation Room 1)", "overworld"),
    IslandObject("bef6d32e-57a9-4ba1-b984-05fdec08800c", "BuoyLight", "Pedestal Lamp (near Observation Room 2)", "overworld"),
    IslandObject("accb5df0-1452-4b84-8607-9f01bd517103", "BuoyLight", "Pedestal Lamp (near Observation Room 3)", "overworld"),
    IslandObject("0e8faaf2-b5fd-4906-aaab-788f8ccbf603", "BuoyLight", "Pedestal Lamp (near Perspective Counting)", "overworld"),
    IslandObject("a552b903-066f-45a8-8550-4cbf5652de74", "BuoyLight", "Pedestal Lamp (near Poet and Priest)", "overworld"),
    IslandObject("38722a78-da82-4d6e-80c4-efd70e1be90f", "BuoyLight", "Pedestal Lamp (near Pointer's Paradise 1)", "overworld"),
    IslandObject("d368b4c7-8389-4f63-a20f-ba88ef17f0a5", "BuoyLight", "Pedestal Lamp (near Pointer's Paradise 2)", "overworld"),
    IslandObject("7017169d-96f6-4578-8c1f-eb65f3549e32", "BuoyLight", "Pedestal Lamp (near Ring Room)", "overworld"),
    IslandObject("4ee18fc7-2a43-44e0-9a2b-4508e9d2705d", "BuoyLight", "Pedestal Lamp (near Singer and Selector 1)", "overworld"),
    IslandObject("c716c86d-81a7-40cc-873f-0405821dbd72", "BuoyLight", "Pedestal Lamp (near Singer and Selector 2)", "overworld"),
    IslandObject("cef7ae31-a105-491d-8d0d-daab0253909c", "BuoyLight", "Pedestal Lamp (near Singer and Selector 3)", "overworld"),
    IslandObject("007a1599-17ad-426f-a68b-37718423e9f7", "BuoyLight", "Pedestal Lamp (near Telescope to Box 1)", "overworld"),
    IslandObject("99d8a99a-89eb-4ad0-8736-104aafe1fdd7", "BuoyLight", "Pedestal Lamp (near Telescope to Box 2)", "overworld"),
    IslandObject("fa653354-bdfa-431d-aa19-7453e5730e80", "BuoyLight", "Pedestal Lamp (near Telescope to Box 3)", "overworld"),
    IslandObject("942347f0-cd23-445f-8019-d296e5c92ead", "BuoyLight", "Pedestal Lamp (near Telescope to Box 4)", "overworld"),
    IslandObject("0abc8402-398d-4f0d-bf5e-fb3051a16e21", "BuoyLight", "Pedestal Lamp (near Telescope to Box 5)", "overworld"),
    IslandObject("c2756ad3-4f4a-45e5-93a3-4c10e892ebdc", "BuoyLight", "Pedestal Lamp (near Teller Window 1)", "overworld"),
    IslandObject("226bdfbc-c2ad-4bda-b9ec-2d72c09f1502", "BuoyLight", "Pedestal Lamp (near Teller Window 2)", "overworld"),
    IslandObject("d467b790-8968-46b1-9be9-e36eac9ebe52", "BuoyLight", "Pedestal Lamp (near Teller Window 3)", "overworld"),
    IslandObject("2cb8963f-ac2f-4f2a-b720-11a701bc4b08", "BuoyLight", "Pedestal Lamp (near Teller Window 4)", "overworld"),
    IslandObject("d2b218b4-279c-4e3e-9751-53a8cbfade87", "BuoyLight", "Pedestal Lamp (near Trap Room)", "overworld"),
)

OBJECT_KINDS: tuple[tuple[str, str], ...] = (
    ("WalkieTalkieProp", "Walkie-Talkie"), ("MegaphoneProp", "Megaphone"), ("FmRadioProp", "Radio"),
    ("BinocularsProp", "Binoculars"), ("LaserProp", "Laser"), ("TorchProp", "Torch"), ("XrayGogglesProp", "Scanner"),
    ("ClockProp", "Clock"), ("CompassProp", "Compass"), ("FoldingMapProp", "Folding Map"),
    ("CowBellLargeProp", "Large Cowbell"), ("CowBellMediumProp", "Medium Cowbell"),
    ("CowBellSmallProp", "Small Cowbell"), ("SalonBrushProp", "Paintbrush"),
    ("BuoyProp", "Lamp"), ("BuoyRedProp", "Red Lamp"), ("BuoyLight", "Pedestal Lamp"),
)
"""The kinds of ISLAND_OBJECTS, in id order: (the game's name, the location's)."""

OBJECT_KIND_KEYS: dict[str, str] = {
    "WalkieTalkieProp": "walkie_talkie", "MegaphoneProp": "megaphone", "FmRadioProp": "radio",
    "BinocularsProp": "binoculars", "LaserProp": "laser", "TorchProp": "torch", "XrayGogglesProp": "scanner",
    "ClockProp": "clock", "CompassProp": "compass", "FoldingMapProp": "map",
    "CowBellLargeProp": "cowbell", "CowBellMediumProp": "cowbell", "CowBellSmallProp": "cowbell",
    "SalonBrushProp": "paintbrush",
    "BuoyProp": "lamp", "BuoyRedProp": "lamp", "BuoyLight": "lamp",
}

OBJECT_KINDS_LEFT_OUT_BY_DEFAULT: frozenset[str] = frozenset({"paintbrush"})
OBJECT_KINDS_NEVER_TAKEN: frozenset[str] = frozenset({"SalonBrushProp"})
"""Kinds the mod never takes off the island, picked up (`every`) or left out (the Salon's paintbrushes)."""
"""The `object_sanity_kinds` key of each kind (the three cowbells share one)."""


class Light(NamedTuple):
    """A light whose first switching on is a check with `light_sanity` (player, 2026-10-13, from a player's idea)."""

    key: str
    """What the mod reports: a lamp's `savablePropGuid`, or a wall switch's TrackedPeckState path."""
    location_name: str
    zone: str
    """As IslandObject.zone, plus "train" (the train's headlights: the Train item)."""
    switch: bool
    """A wall switch (`lamps_and_switches` only), not a lamp."""


LIGHT_ID_OFFSET = 14_000
"""`light_sanity`: `BASE_ID + 14_000 + position` in LIGHTS (lamps, then switches; 14_000..14_199)."""

LIGHT_EVENT_ITEM_NAME = "Light"

OBJECT_EVENT_ITEM_NAME = "Object"
"""The event each object check holds beside it, so the logic can count them for `big_objects`."""


def object_event_name(location_name: str) -> str:
    return f"{location_name} (event)"
"""The event every light holds beside its check, so the logic can count them for `big_lights`."""

LIGHT_SWITCHES: tuple[tuple[str, str, str], ...] = (
    # The wall switches outside the puzzles (light-switches.tsv, 2026-10-04): the puzzles' own lights are part of
    # them, and the `lights` need hides them. (TrackedPeckState path, name, zone.)
    ("LandmarksNonChallenge/DoofChamber/Positioner/FXControls/BasicLightSwitchHouse/LightSwitchPeckLogic",
     "Light Switch (Doof Chamber, House)", "overworld"),
    ("LandmarksNonChallenge/DoofChamber/Positioner/FXControls/BasicLightSwitchRing/LightSwitchPeckLogic",
     "Light Switch (Doof Chamber, Ring)", "overworld"),
    ("LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchPurple/LightSwitchPeckLogic",
     "Light Switch (Doof Chamber, Purple)", "overworld"),
    ("LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchReds/LightSwitchPeckLogic",
     "Light Switch (Doof Chamber, Reds)", "overworld"),
    ("LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchWhiteSpots/LightSwitchPeckLogic",
     "Light Switch (Doof Chamber, White Spots)", "overworld"),
    ("LandmarksNonChallenge/Salon/Positioner/SalonBuilding/BasicLightSwitch - Salon Main/LightSwitchPeckLogic",
     "Light Switch (Salon, Main)", "overworld"),
    ("LandmarksNonChallenge/Salon/Positioner/SalonBuilding/BasicLightSwitch - Mirror/LightSwitchPeckLogic",
     "Light Switch (Salon, Mirror)", "overworld"),
    ("LandmarksNonChallenge/Salon_Secret/Positioner/SalonBuilding/BasicLightSwitch - Salon Main/LightSwitchPeckLogic",
     "Light Switch (Secret Salon, Main)", "tunnels"),
    ("LandmarksNonChallenge/Salon_Secret/Positioner/SalonBuilding/BasicLightSwitch - Mirror/LightSwitchPeckLogic",
     "Light Switch (Secret Salon, Mirror)", "tunnels"),
    ("LandmarksNonChallenge/SpawnCourtyardandHubPlatform/Positioner/TeachingArea/Positioner/ObjectsTeaching/Switch/LightSwitch/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Tutorial)", "start"),
    ("TrainSystem/TrainNew/TrainCarNew/CarriageBody/CarriageContentsFront/BasicLightSwitch - headlight/LightSwitchPeckLogic",
     "Light Switch (Train, Front Car)", "train"),
    ("TrainSystem/TrainNew/TrainCarNew B(5)/CarriageBody/CarriageContentsFront/BasicLightSwitch - headlight/LightSwitchPeckLogic",
     "Light Switch (Train, Middle Car)", "train"),
    ("TrainSystem/TrainNew/TrainCarNew C(10)/CarriageBody/CarriageContentsFront/BasicLightSwitch - headlight/LightSwitchPeckLogic",
     "Light Switch (Train, Back Car)", "train"),
    ("TrainSystem/TrainStop - Green Diffuser/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Green Diffuser Train Stop)", "overworld"),
    ("TrainSystem/TrainStop - Ring Room/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Ring Room Train Stop)", "overworld"),
    ("TrainSystem/TrainStop - South Valley Beach/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (South Valley Beach Train Stop)", "overworld"),
    ("TrainSystem/TrainStop - Sports Valley/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Sports Valley Train Stop)", "overworld"),
    ("TrainSystem/TrainStop - Trap Room/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Trap Room Train Stop)", "overworld"),
    ("TrainSystem/TrainStop - Upper Hub/Lighting/BasicLightSwitch/LightSwitchPeckLogic",
     "Light Switch (Upper Hub Train Stop)", "overworld"),
)

LAMP_KINDS: frozenset[str] = frozenset({"BuoyProp", "BuoyRedProp", "BuoyLight"})


def object_kind_location_name(label: str) -> str:
    return f"First {label} Pickup"


LIGHTS: tuple[Light, ...] = (
    *(Light(obj.guid, f"{obj.location_name} Lit", obj.zone, False) for obj in ISLAND_OBJECTS if obj.kind in LAMP_KINDS),
    *(Light(key, f"{name} On", zone, True) for key, name, zone in LIGHT_SWITCHES),
)
"""Every light of `light_sanity`, in id order: the island's lamps (those of object_sanity), then the wall switches."""


# --------------------------------------------------------------------------
# Zone locks (ROADMAP R12, player 2026-10-14): the island's lamps and gadgets, by the zone of the nearest coloured
# tower, each zone's lamps and gadgets an item. The Map Room's, the chairlift's, the tunnels', the tutorial's and the
# train's keep the lock they have.

ZONE_LOCK_ID_OFFSET = 14_300
"""`BASE_ID + 14_300 + position` in ZONE_LOCKS (the lights end below 14_200)."""


class ZoneLock(NamedTuple):
    key: str
    """The option value (`start_with_zone_locks`): `red_lamps`, `red_gadgets`..."""
    item_name: str
    zone: str
    """The tower's colour, as in OVERWORLD_ZONES."""
    lamps: bool
    """The lamps and the light switches; False: the other gadgets."""


ZONE_COLOURS: tuple[tuple[str, str], ...] = (
    ("red", "bigKeyRedZone"), ("green", "bigKeyGreenZone"), ("blue", "bigKeyBlueZone"), ("yellow", "bigKeyYellowZone"),
)
"""The four coloured towers, by colour and big key prop."""

ZONE_LOCKS: tuple[ZoneLock, ...] = tuple(
    ZoneLock(f"{colour}_{kind}", f"{colour.title()} Zone {kind.title()}", colour, kind == "lamps")
    for colour, _ in ZONE_COLOURS for kind in ("lamps", "gadgets"))

ZONE_LOCK_KEYS: frozenset[str] = frozenset(lock.key for lock in ZONE_LOCKS)

# Each overworld lamp, gadget and light switch to the nearest of the four towers' centres (x/z), from BepInEx's
# object-census.tsv (2026-10-13) and light-switches.tsv, the centres being the mod's MonumentOrder.Centres
# (2026-10-14). Keyed as IslandObject.guid / Light.key. Close calls (under 40 m between the two nearest): the Green
# Diffuser and Upper Hub train stops, Radio (Greensteps), Binoculars (Carousel), the lamps near Invisible Ink (4 m),
# Optical Telegraph, Observation Room 2 and Breadcrumb Loop 2; set one by hand here if it sits on the wrong side.
OVERWORLD_ZONES: dict[str, str] = {
    "ed2f8967-b4c1-43d9-91b5-11679be10bc6": "blue",  # Binoculars (Peak Marker)
    "24d2f9a5-5376-4a7c-b08c-17e9c060855b": "blue",  # Lamp (Diffuser Peak)
    "12a7f64b-8175-4857-a371-77dc2989cd68": "blue",  # Lamp (near Cannonball 2)
    "59a73457-6368-4639-8ec7-335902797343": "blue",  # Lamp (near Coordinates)
    "LandmarksNonChallenge/DoofChamber/Positioner/FXControls/BasicLightSwitchHouse/LightSwitchPeckLogic": "blue",  # Light Switch (Doof Chamber, House) On
    "LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchPurple/LightSwitchPeckLogic": "blue",  # Light Switch (Doof Chamber, Purple) On
    "LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchReds/LightSwitchPeckLogic": "blue",  # Light Switch (Doof Chamber, Reds) On
    "LandmarksNonChallenge/DoofChamber/Positioner/FXControls/BasicLightSwitchRing/LightSwitchPeckLogic": "blue",  # Light Switch (Doof Chamber, Ring) On
    "LandmarksNonChallenge/DoofChamber/Positioner/FXControls/ColouredSpotSwitches/BasicLightSwitchWhiteSpots/LightSwitchPeckLogic": "blue",  # Light Switch (Doof Chamber, White Spots) On
    "dab5cfc8-f4d9-44f2-abb0-90bc42b3cade": "blue",  # Pedestal Lamp (Diffuser Peak)
    "52b0dfcb-4588-4ce7-a78c-bdb9cb800fe6": "blue",  # Pedestal Lamp (Peak Marker 1)
    "f84cb1c7-1702-4fa1-9ddb-f4c334041237": "blue",  # Pedestal Lamp (Peak Marker 2)
    "cd88c773-6375-42c7-9475-76b288bd1c91": "blue",  # Pedestal Lamp (Peak Marker 3)
    "b2f9b7af-39f5-431c-9908-c2ebf6d6453b": "blue",  # Pedestal Lamp (near Breadcrumb Loop 1)
    "accb5df0-1452-4b84-8607-9f01bd517103": "blue",  # Pedestal Lamp (near Observation Room 3)
    "a552b903-066f-45a8-8550-4cbf5652de74": "blue",  # Pedestal Lamp (near Poet and Priest)
    "21ab205e-db90-452b-b620-8f64ebcabd63": "blue",  # Radio (Doof Chamber)
    "ec17f547-5b1f-4fcf-828e-f2932dc2f620": "blue",  # Radio (Factory Music Garden)
    "ea8c71ac-810f-4426-84a1-17e0e3b0b81d": "blue",  # Radio (Peak Marker)
    "cee6114c-2dc0-4d02-a393-5686a2f2e9b8": "blue",  # Walkie-Talkie (Back Beach Hill, Yellow)
    "6efb6db4-76c1-4ffa-b249-8badc6d4bf82": "blue",  # Walkie-Talkie (Peak, Blue)
    "a5992995-191a-45b7-aa23-e9a64a4fede7": "blue",  # Walkie-Talkie (Peak, Yellow)
    "44015a0e-20ca-441a-b6e9-bbd28657b5f3": "green",  # Clock (Ring Room Train Stop)
    "e56da5eb-bac5-4c5e-b489-a8de3d204e7c": "green",  # Clock (South Valley Beach Train Stop)
    "f468d4dc-594e-4c62-80d4-3a85d9e25b3e": "green",  # Clock (Trap Room Train Stop)
    "087f1e73-309b-4db2-8e39-dee6c926ad59": "green",  # Lamp (near Invisible Ink)
    "f4819494-6b1d-48ef-823b-4b26d2ceda53": "green",  # Lamp (near Memory Bombs)
    "13956d25-97a1-4b5c-9992-00c187092476": "green",  # Lamp (near Musical Holiday)
    "982f4dbf-22bb-4553-aa9c-dd6588c19fda": "green",  # Lamp (near Optical Telegraph)
    "632fc258-f424-4b88-930a-a62c45044965": "green",  # Lamp (near Trap Room)
    "TrainSystem/TrainStop - Ring Room/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "green",  # Light Switch (Ring Room Train Stop) On
    "TrainSystem/TrainStop - South Valley Beach/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "green",  # Light Switch (South Valley Beach Train Stop) On
    "TrainSystem/TrainStop - Trap Room/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "green",  # Light Switch (Trap Room Train Stop) On
    "7e2f3ba8-2124-4fe9-8707-1621be5a0926": "green",  # Megaphone (Pyramid Holder)
    "16990dcc-ebb8-453e-88fc-6478e3980728": "green",  # Pedestal Lamp (near Breadcrumb Loop 2)
    "b46c590f-7038-4033-b84d-e3fb664e095c": "green",  # Pedestal Lamp (near Musical Holiday 1)
    "d069a848-37f5-40e0-ac18-64516951f481": "green",  # Pedestal Lamp (near Musical Holiday 2)
    "c7141d1b-96bf-4c2c-8c05-3d2067e64a93": "green",  # Pedestal Lamp (near Musical Holiday 3)
    "0e8faaf2-b5fd-4906-aaab-788f8ccbf603": "green",  # Pedestal Lamp (near Perspective Counting)
    "7017169d-96f6-4578-8c1f-eb65f3549e32": "green",  # Pedestal Lamp (near Ring Room)
    "d2b218b4-279c-4e3e-9751-53a8cbfade87": "green",  # Pedestal Lamp (near Trap Room)
    "87dbbb84-5c58-43a4-96ba-e43c1c7308b9": "green",  # Radio (Greensteps Music Garden)
    "070cf8f2-6824-413e-9c2a-43a1027c24c5": "green",  # Radio (Musical Holiday)
    "00d7ed74-a4d9-4969-b8e2-bf8d4975ac8a": "green",  # Walkie-Talkie (Ocean Treasure, Blue)
    "cd159c6e-88aa-4158-a11f-2790cf581052": "green",  # Walkie-Talkie (Ocean Treasure, Yellow)
    "88942e8e-6fae-4d71-93f0-8500741a82d1": "red",  # Binoculars (Carousel)
    "209a7dc3-9dcb-425e-86c5-3a84aa9bd0ec": "red",  # Clock (Sports Valley Train Stop)
    "a08daa18-2fef-4a49-a062-2baafdf48686": "red",  # Clock (Upper Hub Train Stop)
    "5d10fec3-7a2d-4bfd-be0e-6196152d88f5": "red",  # Lamp (near Cannonball 1)
    "f7f2e9ed-14fd-4a54-a918-fc06b444a39e": "red",  # Lamp (near Easy Simultaneous Press)
    "cec1412b-4727-48c4-b6a1-5a5e7989af18": "red",  # Lamp (near High Button 1)
    "94876c24-50ba-41cb-8fdd-9bd026f3be32": "red",  # Lamp (near High Button 2)
    "df814b39-e550-4e17-ad44-e32028ab3c47": "red",  # Lamp (near Kick Up Pits)
    "17e22f02-4f01-4aa4-83fb-d2871acd97cd": "red",  # Lamp (near Pointer's Paradise)
    "d3e57e13-a91d-492c-bd46-2c59da8bef9e": "red",  # Lamp (near Telescope to Box)
    "5ee8fa69-8937-4841-b9a7-ee8a10c3a933": "red",  # Lamp (near Teller Window 1)
    "4b9cad35-ab44-4dfe-9e4b-390cb165aca5": "red",  # Lamp (near Teller Window 2)
    "LandmarksNonChallenge/Salon/Positioner/SalonBuilding/BasicLightSwitch - Salon Main/LightSwitchPeckLogic": "red",  # Light Switch (Salon, Main) On
    "LandmarksNonChallenge/Salon/Positioner/SalonBuilding/BasicLightSwitch - Mirror/LightSwitchPeckLogic": "red",  # Light Switch (Salon, Mirror) On
    "TrainSystem/TrainStop - Sports Valley/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "red",  # Light Switch (Sports Valley Train Stop) On
    "TrainSystem/TrainStop - Upper Hub/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "red",  # Light Switch (Upper Hub Train Stop) On
    "52fedcd9-caa6-4ec6-a285-f73e6ff40f50": "red",  # Paintbrush (Salon)
    "60412245-4b6c-47eb-a4e7-67eec146bf20": "red",  # Pedestal Lamp (near Basketball)
    "d0a1c556-d62f-42df-a36e-019636da7679": "red",  # Pedestal Lamp (near Fielding)
    "5efbf72b-9dff-4251-a536-f625ba8ff315": "red",  # Pedestal Lamp (near High Button 1)
    "fee806b8-8617-49f8-99af-bdea0c148ed0": "red",  # Pedestal Lamp (near High Button 2)
    "4ec6aca0-9961-4550-9c26-7cacb9917566": "red",  # Pedestal Lamp (near High Button 3)
    "38722a78-da82-4d6e-80c4-efd70e1be90f": "red",  # Pedestal Lamp (near Pointer's Paradise 1)
    "d368b4c7-8389-4f63-a20f-ba88ef17f0a5": "red",  # Pedestal Lamp (near Pointer's Paradise 2)
    "007a1599-17ad-426f-a68b-37718423e9f7": "red",  # Pedestal Lamp (near Telescope to Box 1)
    "99d8a99a-89eb-4ad0-8736-104aafe1fdd7": "red",  # Pedestal Lamp (near Telescope to Box 2)
    "fa653354-bdfa-431d-aa19-7453e5730e80": "red",  # Pedestal Lamp (near Telescope to Box 3)
    "942347f0-cd23-445f-8019-d296e5c92ead": "red",  # Pedestal Lamp (near Telescope to Box 4)
    "0abc8402-398d-4f0d-bf5e-fb3051a16e21": "red",  # Pedestal Lamp (near Telescope to Box 5)
    "c2756ad3-4f4a-45e5-93a3-4c10e892ebdc": "red",  # Pedestal Lamp (near Teller Window 1)
    "226bdfbc-c2ad-4bda-b9ec-2d72c09f1502": "red",  # Pedestal Lamp (near Teller Window 2)
    "d467b790-8968-46b1-9be9-e36eac9ebe52": "red",  # Pedestal Lamp (near Teller Window 3)
    "2cb8963f-ac2f-4f2a-b720-11a701bc4b08": "red",  # Pedestal Lamp (near Teller Window 4)
    "41e7b481-2e10-4d62-9136-5e6e0ba9fa7d": "red",  # Radio (Lighthouse Music Garden)
    "ebdec567-58c0-48c7-a62a-bbd843bef36f": "red",  # Radio (Upper Hub)
    "044daaf7-fb90-4f73-a9bb-00b8a3df9c60": "red",  # Scanner (Fireworks Platform)
    "28cf2386-5e54-4b7e-99ba-283298aa3816": "red",  # Scanner (Upper Hub)
    "ac52bd4b-990f-4908-999d-5badeb8b6eda": "red",  # Torch (Tutorial Exit)
    "185e3864-0dcb-498b-baee-6da1a4b21a8e": "red",  # Walkie-Talkie (Red Cone Hill, Yellow)
    "f29614f5-0c70-4ed8-b152-9e57b371f20d": "red",  # Walkie-Talkie (Red Cone Valley, Blue)
    "1c9f8bc9-5850-4085-80f6-05c2821b505d": "yellow",  # Clock (Green Diffuser Train Stop)
    "b95078fd-d8f2-4fd9-b2aa-ecf8b84fdad7": "yellow",  # Lamp (near Centurion Song)
    "80e38f3e-d054-4187-9675-66d6e70c8356": "yellow",  # Lamp (near Egg Hunt)
    "6a673617-992d-49ca-87f9-223aef4c9fe4": "yellow",  # Lamp (near Scout Bombs)
    "6beb48a3-60de-41e8-a9f6-d39772bcdfa4": "yellow",  # Lamp (near Singer and Selector)
    "TrainSystem/TrainStop - Green Diffuser/Lighting/BasicLightSwitch/LightSwitchPeckLogic": "yellow",  # Light Switch (Green Diffuser Train Stop) On
    "c6259d07-2499-4063-b033-b72cddd951c2": "yellow",  # Megaphone (Ramp Holder)
    "cf2e3191-4c77-4ce9-8c56-f43a73d6e531": "yellow",  # Pedestal Lamp (near Centurion Song)
    "080554bb-bc16-41cd-bc96-4765cec96beb": "yellow",  # Pedestal Lamp (near Egg Hunt)
    "11c1d018-3dbf-46d4-b1de-f9aba4d29fc2": "yellow",  # Pedestal Lamp (near Observation Room 1)
    "bef6d32e-57a9-4ba1-b984-05fdec08800c": "yellow",  # Pedestal Lamp (near Observation Room 2)
    "4ee18fc7-2a43-44e0-9a2b-4508e9d2705d": "yellow",  # Pedestal Lamp (near Singer and Selector 1)
    "c716c86d-81a7-40cc-873f-0405821dbd72": "yellow",  # Pedestal Lamp (near Singer and Selector 2)
    "cef7ae31-a105-491d-8d0d-daab0253909c": "yellow",  # Pedestal Lamp (near Singer and Selector 3)
    "d8e70c55-9800-498e-af4e-7054093950e4": "yellow",  # Radio (Floorplan Music Garden)
    "56a7313e-9e55-485b-ac52-fcc6f8030133": "yellow",  # Walkie-Talkie (Beach, Blue)
}


def zone_of(key: str, zone: str) -> str | None:
    """The zone colour of a lamp, gadget or switch (its key, its IslandObject/Light zone); None: no zone lock."""
    if zone.startswith("tower:"):
        return next((colour for colour, prop in ZONE_COLOURS if zone == f"tower:{prop}"), None)
    if zone == "overworld":
        return OVERWORLD_ZONES.get(key)
    return None


def zone_lock_for(key: str, zone: str, lamps: bool) -> ZoneLock | None:
    colour = zone_of(key, zone)
    if colour is None:
        return None
    return next(lock for lock in ZONE_LOCKS if lock.zone == colour and lock.lamps == lamps)


def zone_lock_item_id(lock: ZoneLock) -> int:
    return BASE_ID + ZONE_LOCK_ID_OFFSET + ZONE_LOCKS.index(lock)


def light_event_name(light: Light) -> str:
    return f"{light.location_name} (event)"


def light_location_id(light: Light) -> int:
    return BASE_ID + LIGHT_ID_OFFSET + LIGHTS.index(light)


def object_location_id(obj: IslandObject) -> int:
    return BASE_ID + OBJECT_ID_OFFSET + ISLAND_OBJECTS.index(obj)


def object_kind_location_id(position: int) -> int:
    return BASE_ID + OBJECT_KIND_ID_OFFSET + position


MAP_ROOM_ITEM_NAME = "Map Room"


def tower_by_prop(prop_name: str) -> Tower:
    return next(tower for tower in TOWERS if tower.prop_name == prop_name)


def pickup_location_id(position: int) -> int:
    return BASE_ID + PICKUP_ID_OFFSET + position


def firework_location_id(position: int) -> int:
    return BASE_ID + FIREWORK_ID_OFFSET + position


def flare_gun_location_id(position: int) -> int:
    return BASE_ID + FLARE_GUN_ID_OFFSET + position


class Effect(NamedTuple):
    """A trap or a bonus: an item whose effect the mod plays on every player of the session."""

    item_name: str
    offset: int
    # What the mod knows the effect by (slot data, the ModChannel).
    key: str
    # A trap that costs time or progress (player, 2026-10-05): `hard_traps` keeps them out.
    hard: bool = False


TRAPS: tuple[Effect, ...] = (
    Effect("Big Drop", 9_102, "drop"),
    Effect("Big Throw", 9_103, "throw", hard=True),
    Effect("Big Trip", 9_104, "trip", hard=True),
    Effect("Big Meeting", 9_105, "meeting", hard=True),
    Effect("Big Night", 9_106, "night"),
    Effect("Big Sleep", 9_107, "sleep", hard=True),
    Effect("Big Load", 9_108, "load", hard=True),
    Effect("Big Mask", 9_109, "mask", hard=True),
    Effect("Big Flare", 9_110, "flare"),
    # A clock in every player's hands, theirs to keep, telling the real time of their own computer (player,
    # 2026-10-13).
    Effect("Big Time", 9_111, "time"),
    # Every player muted for the trap's duration, as with the game's own mute (R9 "No Comms", player 2026-10-13).
    Effect("Big Silence", 9_112, "silence"),
    # The ground turns slippery for the trap's duration: starting and stopping take a long while (0.6).
    Effect("Big Slide", 9_113, "slide"),
    # Every player white for the trap's duration, as in the Silent Gauntlet: the colour, not the silence (R9, 0.7).
    Effect("Big Ghost", 9_114, "ghost"),
    # The players swap colours for the trap's duration (two swap, more go round; T5's colour chaos, 0.7).
    Effect("Big Paint", 9_115, "paint"),
    # A timed tomato, then up in the air (a player's idea, 0.7).
    Effect("Big Air", 9_116, "air", hard=True),
    # The field of view narrowed for the trap's duration (0.7).
    Effect("Big Goggles", 9_117, "goggles"),
    # The island in big square pixels for the trap's duration (0.7).
    Effect("Big Pixels", 9_118, "pixels"),
    # The players' heads grow for the trap's duration (0.7).
    Effect("Big Head", 9_119, "head"),
    # The players' heads become a glowing lamp for the trap's duration (0.7).
    Effect("Big Brain", 9_120, "brain"),
    # The players' hands grow for the trap's duration (0.7).
    Effect("Big Arms", 9_121, "arms"),
    # Every player gives off a green gas and the players are pushed apart (0.7).
    Effect("Big Smell", 9_122, "smell"),
    # Every player becomes small for the trap's duration (0.7).
    Effect("Big Brother", 9_123, "brother"),
    # The nearest puzzle still holding its gourd starts again (0.7).
    Effect("Big Reset", 9_124, "reset", hard=True),
    # A folding chair for every player, who is sat on it (0.7).
    Effect("Big Break", 9_125, "break"),
)
"""
Every trap is "Big" + one word (player, 2026-10-05) and hits every player of the session. Only
the traps the mod plays are here; the others wait in docs/ROADMAP.md. 9_101 was "Untied
Shoelace", a trap that did nothing and was never in a pool: retired, not reused.
"""

BONUSES: tuple[Effect, ...] = (
    Effect("Big Speed", 9_301, "speed"),
    Effect("Big Jump", 9_302, "jump"),
    Effect("Big Day", 9_303, "day"),
)
"""The traps' good twins: a boost for every player, for `trap_duration` seconds."""

TRAP_ITEMS: tuple[tuple[str, int], ...] = tuple((trap.item_name, trap.offset) for trap in TRAPS)
BONUS_ITEMS: tuple[tuple[str, int], ...] = tuple((bonus.item_name, bonus.offset) for bonus in BONUSES)

DEATH_LINK_TRIGGERS: tuple[str, ...] = ("puzzle_failed", "big_fall")
"""
What sends a DeathLink: a puzzle failed, as the game itself signals it (tile validators, buttons in
order, counting, timers), and a fall that knocks a player down. The fall was dropped on 2026-10-05
and is back as a choice, off by default (players, 2026-10-07). Falling asleep stays dropped.
"""


# --------------------------------------------------------------------------
# Id helpers
# --------------------------------------------------------------------------

def puzzle_location_id(puzzle: Puzzle) -> int:
    return BASE_ID + puzzle.prop_value


def puzzle_need_item_id(need: PuzzleNeed) -> int:
    return BASE_ID + PUZZLE_NEED_ID_OFFSET + PUZZLE_NEEDS.index(need)


def key_deposit_location_id(tower: Tower) -> int:
    return BASE_ID + tower.prop_value


def feature_item_id(tower: Tower) -> int:
    """The door: map room, chairlift, train, tunnels, drawbridge, dam, dome."""
    return BASE_ID + tower.prop_value


def key_item_name(tower: Tower) -> str:
    """
    The physical key a player carries, cuts and places.

    Named after its tower ("Red Funnel Tower Key"), like its own locations — `Red
    Funnel Tower Key Cut 1` and `Red Funnel Tower Key Deposit` — so that nothing has to be
    memorised to know where a key belongs.
    """
    return f"{tower.key_name} Key"


def key_item_id(tower: Tower) -> int:
    return BASE_ID + KEY_ITEM_ID_OFFSET + tower.prop_value


def cut_location_name(tower: Tower, index: int) -> str:
    """Player-facing name of "the Nth segment of this key was cut".

    Numbered from 1 for the player; the mod counts the SyncList from 0, which
    is what `cut_location_id` undoes.
    """
    return f"{tower.key_name} Key Cut {index + 1}"


def cut_location_id(tower: Tower, index: int) -> int:
    return BASE_ID + CUT_ID_OFFSET + tower.prop_value * CUT_STRIDE + index


def cut_locations(tower: Tower) -> tuple[str, ...]:
    return tuple(cut_location_name(tower, index) for index in range(tower.segments))


def radio_location_id(station: RadioStation) -> int:
    return BASE_ID + RADIO_ID_OFFSET + station.system_value


def radio_item_id(station: RadioStation) -> int:
    # Same number as the location, as for the big keys above: item ids and
    # location ids are separate namespaces in Archipelago, and reusing the
    # game's own enum value in both keeps the mod's arithmetic to one rule
    # per category instead of two.
    return BASE_ID + RADIO_ID_OFFSET + station.system_value


# --------------------------------------------------------------------------
# Arch doors (SavableSystem.SpawnHubGate / HubShortcutToSportsCreek / HubTunnel)
# --------------------------------------------------------------------------
# The hub's three arch doors, which the mod opens on a save's first session
# unless `start_with_arch_doors_open` leaves some of them out, behind an item each.
#
# The first is the tutorial's way back to the hub: in vanilla it is opened
# from the far side once the drawbridge is down, so it and the Drawbridge are
# the two ways out of the starting zone. The left one (towards Sports Creek)
# and the right one are shortcuts, which the player confirmed change no
# reachability, only the length of the walk. Which system is which was
# measured on 2026-09-25: SpawnHubGate is 11m from the hub's keyhole, the
# Sports Creek shortcut furthest east, the tunnel between them.


class ArchDoor(NamedTuple):
    system_name: str
    """`SavableSystem` enum name, the SaveManager key the door's state lives under."""
    system_value: int
    """`SavableSystem` enum value; the item id is BASE_ID + ARCH_DOOR_ID_OFFSET + this."""
    item_name: str


FIRST_ARCH_DOOR = ArchDoor("SpawnHubGate", 11, "First Arch Door")
LEFT_ARCH_DOOR = ArchDoor("HubShortcutToSportsCreek", 13, "Left Arch Door")
RIGHT_ARCH_DOOR = ArchDoor("HubTunnel", 12, "Right Arch Door")
ARCH_DOORS: tuple[ArchDoor, ...] = (FIRST_ARCH_DOOR, LEFT_ARCH_DOOR, RIGHT_ARCH_DOOR)


# What lies past the two far doors, told by the player on 2026-10-03 (the map is
# theirs, not measured). The Left door (towards Sports Creek) leads to the Yellow
# and Blue towers and to the Black Tower and the chapel behind it, and so to the
# Green Dome, which is reached from the Black Tower; the Right door (the tunnel) to
# the Green Tower and to what is past the chairlift, the purple gourds. Not the Red
# Tower: it can be reached very early without the door, only the way through the
# tunnel is hard (player, 2026-10-03).
# Either door can be walked round, which is the whole point of putting them in
# logic (`require_arch_doors`): so that the generator does not ask for a place on
# foot and then send the shortcut afterwards. A list that is too short only asks for
# fewer doors than it could, never more than a seed has.
TOWER_FAR_DOOR: dict[str, ArchDoor] = {
    "bigKeyGreenZone": RIGHT_ARCH_DOOR,
    "bigKeyBlueZone": LEFT_ARCH_DOOR,
    "bigKeyYellowZone": LEFT_ARCH_DOOR,
    "bigKeyBoss": LEFT_ARCH_DOOR,
    "bigKeyOverflow": LEFT_ARCH_DOOR,
}
"""The far door each tower lies past, by the tower's `prop_name`. The drawbridge has none."""

ZONE_FAR_DOOR: dict[str, ArchDoor | None] = {
    "chairlift": RIGHT_ARCH_DOOR,
    "tunnels": LEFT_ARCH_DOOR,
    # Not the chapel: the end of the game is never held behind the Left door (player,
    # 2026-10-05, E2 walked round it to the chapel and the tracker never saw go mode).
    "ending": None,
    "green_dome": LEFT_ARCH_DOOR,
}
"""The far door each zone behind a big key lies past (`regions.py` names the entrances)."""


def arch_door_item_id(door: ArchDoor) -> int:
    return BASE_ID + ARCH_DOOR_ID_OFFSET + door.system_value


def deposit_location_name(amount: int) -> str:
    return f"Gourd Deposit {amount}"


def deposit_location_id(amount: int) -> int:
    return BASE_ID + DEPOSIT_ID_OFFSET + amount
