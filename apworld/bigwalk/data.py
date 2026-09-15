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
# 9_000 and above is the item-only range for things the game has no enum for
# (filler, traps); see FILLER_ITEMS / TRAP_ITEMS at the bottom of this file.

GOURD_ITEM_NAME = "Gourd"

# --------------------------------------------------------------------------
# Puzzles (SaveablePropName.gourdXxx)
# --------------------------------------------------------------------------
# 58 puzzles. The enum skips 102 and 136; the game shipped with 45 of these
# and gained 13 more in a content update (see ../design-decisions.md).
#
# Deliberately excluded:
#   - gourdTesting00-39 / bigKeyTesting* (dev-only values, never triggered in
#     normal play; the mod already filters them out in GourdRegistry).
#   - gourdSecretZoneVice (290): the only gourd-prefixed value with no matching
#     valetXxx home, never observed in play, purpose unknown. Including it
#     would risk an unreachable location. The mod must not report it either.


class Puzzle(NamedTuple):
    prop_name: str
    """`SaveablePropName` enum name, exactly as the game writes it to the save."""
    prop_value: int
    """`SaveablePropName` enum value; the location id is BASE_ID + this."""
    location_name: str
    """Player-facing Archipelago location name."""


PUZZLES: tuple[Puzzle, ...] = (
    Puzzle("gourdCabinFever", 100, "Cabin Fever"),
    Puzzle("gourdHighButton", 101, "High Button"),
    Puzzle("gourdFielding", 103, "Fielding"),
    Puzzle("gourdCannonBall", 104, "Cannon Ball"),
    Puzzle("gourdInvisibleInk", 105, "Invisible Ink"),
    Puzzle("gourdTrapRoom", 106, "Trap Room"),
    Puzzle("gourdMediumSimPress", 107, "Medium Simultaneous Press"),
    Puzzle("gourdEasySimPress", 108, "Easy Simultaneous Press"),
    Puzzle("gourdRingRoom", 109, "Ring Room"),
    Puzzle("gourdBunker", 110, "Bunker"),
    Puzzle("gourdHighPegBoard", 111, "High Peg Board"),
    Puzzle("gourdFirstPegBoard", 112, "First Peg Board"),
    Puzzle("gourdObby", 113, "Obby"),
    Puzzle("gourdCarousel", 114, "Carousel"),
    Puzzle("gourdCoordinates", 115, "Coordinates"),
    Puzzle("gourdTelescopeToBox", 116, "Telescope to Box"),
    Puzzle("gourdObservationRoom", 117, "Observation Room"),
    Puzzle("gourdWindowLabyrinth", 118, "Window Labyrinth"),
    Puzzle("gourdMagiciansTrick", 119, "Magician's Trick"),
    Puzzle("gourdButtonBoothChallenge", 120, "Button Booth Challenge"),
    Puzzle("gourdBasketball", 121, "Basketball"),
    Puzzle("gourdConcert", 122, "Concert"),
    Puzzle("gourdIndoorSemaphore", 123, "Indoor Semaphore"),
    Puzzle("gourdOpticalTelegraph", 124, "Optical Telegraph"),
    # The game's own enum misspells "Priest"; the location name does not.
    Puzzle("gourdPoetAndPreist", 125, "Poet and Priest"),
    Puzzle("gourdTileSoup", 126, "Tile Soup"),
    Puzzle("gourdMemoryBombs", 127, "Memory Bombs"),
    Puzzle("gourdPanopticon", 128, "Panopticon"),
    Puzzle("gourdMaypole", 129, "Maypole"),
    Puzzle("gourdBlindfoldCircus", 130, "Blindfold Circus"),
    Puzzle("gourdMessengerRun", 131, "Messenger Run"),
    Puzzle("gourdHotPotato", 132, "Hot Potato"),
    Puzzle("gourdTileThief", 133, "Tile Thief"),
    Puzzle("gourdCharadesRooms", 134, "Charades Rooms"),
    Puzzle("gourdMicrophoneArray", 135, "Microphone Array"),
    Puzzle("gourdPointersParadise", 137, "Pointer's Paradise"),
    Puzzle("gourdCoordinatesHolding", 138, "Coordinates Holding"),
    Puzzle("gourdEggHunt", 139, "Egg Hunt"),
    Puzzle("gourdTellerWindow", 140, "Teller Window"),
    Puzzle("gourdSignalFlags", 141, "Signal Flags"),
    Puzzle("gourdCabinFeverLong", 142, "Cabin Fever Long"),
    Puzzle("gourdBreadcrumbLoop", 143, "Breadcrumb Loop"),
    Puzzle("gourdScoutBombs", 144, "Scout Bombs"),
    Puzzle("gourdScoutTiles", 145, "Scout Tiles"),
    Puzzle("gourdScoutCounting", 146, "Scout Counting"),
    # Another game-side typo ("Centuron"), kept only in prop_name.
    Puzzle("gourdCenturonSong", 147, "Centurion Song"),
    Puzzle("gourdMusicalHoliday", 148, "Musical Holiday"),
    Puzzle("gourdKickUpPits", 149, "Kick Up Pits"),
    Puzzle("gourdSingerAndSelecter", 150, "Singer and Selector"),
    Puzzle("gourdDancerAndSelecter", 151, "Dancer and Selector"),
    Puzzle("gourdSpeedObby", 152, "Speed Obby"),
    Puzzle("gourdBlindfoldCatwalk", 153, "Blindfold Catwalk"),
    Puzzle("gourdBlindfoldFishtrap", 154, "Blindfold Fishtrap"),
    Puzzle("gourdPerspectiveCounting", 155, "Perspective Counting"),
    Puzzle("gourdCenturionSeance", 156, "Centurion Seance"),
    Puzzle("gourdFlareRun", 157, "Flare Run"),
    Puzzle("gourdCannonballCommute", 158, "Cannonball Commute"),
    Puzzle("gourdPoetAndPontiff", 159, "Poet and Pontiff"),
)

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
    """Player-facing name of the big key item."""
    location_name: str
    """Player-facing name of the location "this key was placed in its plinth"."""
    plinth_name: str
    """`SaveableHomeName` enum name of the plinth the key is deposited into."""
    monument_prefix: str
    """`SaveableHomeName` prefix of the monument whose slots unlock this key."""
    slots: int
    """Number of monument slots for this tower (F6 count, matches the doc)."""


TUTORIAL_KEY_ITEM_NAME = "Tutorial Key"
GREEN_DOME_KEY_ITEM_NAME = "Green Dome Key"

TOWERS: tuple[Tower, ...] = (
    Tower("bigKeyIntro", 300, TUTORIAL_KEY_ITEM_NAME, "Drawbridge Key Deposit",
          "bigKeyPlinthIntro", "monoumentIntro", 4),
    Tower("bigKeyRedZone", 301, "Red Funnel Key", "Map Room Key Deposit",
          "bigKeyPlinthMapRoom", "monoument0", 5),
    Tower("bigKeyGreenZone", 302, "Green Cup Key", "Chairlift Key Deposit",
          "bigKeyPlinthSkiLift", "monoument1", 5),
    Tower("bigKeyBlueZone", 303, "Blue Castle Key", "Train Station Key Deposit",
          "bigKeyPlinthTrain", "monoument2", 5),
    Tower("bigKeyYellowZone", 304, "Yellow Twist Key", "Tunnel Key Deposit",
          "bigKeyPlinthTunnels", "monoument3", 5),
    Tower("bigKeyBoss", 305, "Black Monolith Key", "Dam Key Deposit",
          "bigKeyPlinthEnding", "monoumentFinal", 6),
    Tower("bigKeyOverflow", 306, GREEN_DOME_KEY_ITEM_NAME, "Goodbye Keyhole Key Deposit",
          "bigKeyPlinthGoodbye2", "monoumentOverflow", 15),
)

# monoument0-3 are known to be the four five-slot towers, but which of the four
# is Red/Green/Blue/Yellow was never established in-game. It does not matter
# here: they have identical slot counts, so no rule can tell them apart.

GREEN_DOME = TOWERS[-1]
"""The Green Dome tower (`bigKeyOverflow`), the postgame/true-ending key."""

BLACK_MONOLITH = TOWERS[5]
"""The Black Monolith tower (`bigKeyBoss`); its key opens the way to the ending."""

BASE_MONUMENT_SLOTS = sum(tower.slots for tower in TOWERS if tower is not GREEN_DOME)
"""Slots in every monument except the Green Dome's: 4 + 5 + 5 + 5 + 5 + 6 = 30."""

MAX_MONUMENT_SLOTS = BASE_MONUMENT_SLOTS + GREEN_DOME.slots
"""Largest possible total (45), used to size the static deposit location table."""

# --------------------------------------------------------------------------
# Radio stations (SavableSystem.FmStation*)
# --------------------------------------------------------------------------
# The enum declares ten, but only seven were ever observed being written at
# once by the game's own dev helper (`PeckDevHelper.Trigger(unlocks: true)`,
# see the "Arch doors" section of the mod notes) — and those seven are exactly
# the ones with real names. FmStation7/8/9 look like unused placeholders, so
# they are left out: a location the game can never check makes a seed
# unbeatable, which is a far worse failure than three missing checks.


class RadioStation(NamedTuple):
    system_name: str
    """`SavableSystem` enum name, the literal `SaveManager` key the game writes."""
    system_value: int
    """`SavableSystem` enum value; location id is BASE_ID + RADIO_ID_OFFSET + this."""
    location_name: str


RADIO_STATIONS: tuple[RadioStation, ...] = (
    RadioStation("FmStationSleuthFm", 30, "Radio Station: Sleuth FM"),
    RadioStation("FmStationKosmische", 31, "Radio Station: Kosmische"),
    RadioStation("FmStationDanceFm", 32, "Radio Station: Dance FM"),
    RadioStation("FmStationBreathwork", 33, "Radio Station: Breathwork"),
    RadioStation("FmStationJourneyBeat", 34, "Radio Station: Journey Beat"),
    RadioStation("FmStationAFJ", 35, "Radio Station: AFJ"),
    RadioStation("FmStationFourthSpace", 36, "Radio Station: Fourth Space"),
)

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
# The mod materializes exactly two things today: a generic cosmetic gourd and a
# big key. Everything below is inert by design — the client applies no effect
# for it — so these names exist purely to fill the pool with something readable
# for the other players in the multiworld.

FILLER_ITEMS: tuple[tuple[str, int], ...] = (
    ("Postcard", 9_001),
    ("Souvenir Pebble", 9_002),
    ("Novelty Keychain", 9_003),
)

TRAP_ITEMS: tuple[tuple[str, int], ...] = (
    ("Untied Shoelace", 9_101),
)


# --------------------------------------------------------------------------
# Id helpers
# --------------------------------------------------------------------------

def puzzle_location_id(puzzle: Puzzle) -> int:
    return BASE_ID + puzzle.prop_value


def key_deposit_location_id(tower: Tower) -> int:
    return BASE_ID + tower.prop_value


def key_item_id(tower: Tower) -> int:
    return BASE_ID + tower.prop_value


def radio_location_id(station: RadioStation) -> int:
    return BASE_ID + RADIO_ID_OFFSET + station.system_value


def deposit_location_name(amount: int) -> str:
    return f"Gourd Deposit {amount}"


def deposit_location_id(amount: int) -> int:
    return BASE_ID + DEPOSIT_ID_OFFSET + amount
