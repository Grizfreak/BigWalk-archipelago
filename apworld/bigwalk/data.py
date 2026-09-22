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
CUT_ID_OFFSET = 3_000
KEY_ITEM_ID_OFFSET = 4_000
# 9_000 and above is the item-only range for things the game has no enum for
# (filler, traps); see FILLER_ITEMS / TRAP_ITEMS at the bottom of this file.

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
    Puzzle("gourdCabinFever", 100, "Cabin Fever"),
    Puzzle("gourdHighButton", 101, "High Button"),
    Puzzle("gourdFielding", 103, "Fielding"),
    Puzzle("gourdCannonBall", 104, "Cannon Ball"),
    Puzzle("gourdInvisibleInk", 105, "Invisible Ink"),
    Puzzle("gourdTrapRoom", 106, "Trap Room"),
    Puzzle("gourdMediumSimPress", 107, "Medium Simultaneous Press"),
    Puzzle("gourdEasySimPress", 108, "Easy Simultaneous Press"),
    Puzzle("gourdRingRoom", 109, "Ring Room"),
    Puzzle("gourdObby", 113, "Obby"),
    Puzzle("gourdCarousel", 114, "Carousel"),
    Puzzle("gourdCoordinates", 115, "Coordinates"),
    Puzzle("gourdTelescopeToBox", 116, "Telescope to Box"),
    Puzzle("gourdObservationRoom", 117, "Observation Room"),
    Puzzle("gourdWindowLabyrinth", 118, "Window Labyrinth"),
    Puzzle("gourdBasketball", 121, "Basketball"),
    Puzzle("gourdConcert", 122, "Concert"),
    Puzzle("gourdIndoorSemaphore", 123, "Indoor Semaphore"),
    Puzzle("gourdOpticalTelegraph", 124, "Optical Telegraph"),
    # The game's own enum misspells "Priest"; the location name does not.
    Puzzle("gourdPoetAndPreist", 125, "Poet and Priest"),
    Puzzle("gourdMemoryBombs", 127, "Memory Bombs"),
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
    `key_item_name()`. The two are different things and always have been:
    a key is a check carrier the players cut and place, and a feature is
    what actually opens. Keeping the key's name derived from the feature's
    is what stops a player having to learn that the Green Cup tower is the
    chairlift one.
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
DRAWBRIDGE_ITEM_NAME = "Drawbridge"
GREEN_DOME_ITEM_NAME = "Green Dome"

TOWERS: tuple[Tower, ...] = (
    Tower("bigKeyIntro", 300, DRAWBRIDGE_ITEM_NAME, "Drawbridge Key Deposit",
          "bigKeyPlinthIntro", "monoumentIntro", 4, 5),
    Tower("bigKeyRedZone", 301, "Map Room", "Map Room Key Deposit",
          "bigKeyPlinthMapRoom", "monoument0", 5, 5),
    Tower("bigKeyGreenZone", 302, "Chairlift", "Chairlift Key Deposit",
          "bigKeyPlinthSkiLift", "monoument1", 5, 5),
    Tower("bigKeyBlueZone", 303, "Train", "Train Key Deposit",
          "bigKeyPlinthTrain", "monoument2", 5, 5),
    Tower("bigKeyYellowZone", 304, "Tunnels", "Tunnels Key Deposit",
          "bigKeyPlinthTunnels", "monoument3", 5, 5),
    Tower("bigKeyBoss", 305, "Dam", "Dam Key Deposit",
          "bigKeyPlinthEnding", "monoumentFinal", 6, 0),
    Tower("bigKeyOverflow", 306, GREEN_DOME_ITEM_NAME, "Green Dome Key Deposit",
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
"""The Green Cup tower (`bigKeyGreenZone`); its key opens the chairlift."""

YELLOW_TWIST = TOWERS[4]
"""The Yellow Twist tower (`bigKeyYellowZone`); its key opens the tunnels."""

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
# station names at all (the radio dial displays numbers), so nothing in the
# world contradicts this choice — it is an Archipelago convention, and the
# honest one.
#
# `system_name` stays the enum value: it is the SaveManager key and the id
# arithmetic, and it must never be "corrected" to match the display name.

RADIO_STATIONS: tuple[RadioStation, ...] = (
    RadioStation("FmStationSleuthFm", 30, "Radio Station: Fourth Space", "Radio Music: Fourth Space"),
    RadioStation("FmStationKosmische", 31, "Radio Station: Bristol", "Radio Music: Bristol"),
    RadioStation("FmStationDanceFm", 32, "Radio Station: Dance FM", "Radio Music: Dance FM"),
    RadioStation("FmStationBreathwork", 33, "Radio Station: Bobby", "Radio Music: Bobby"),
    RadioStation("FmStationJourneyBeat", 34, "Radio Station: Journey Beat", "Radio Music: Journey Beat"),
    RadioStation("FmStationAFJ", 35, "Radio Station: Mallets", "Radio Music: Mallets"),
    RadioStation("FmStationFourthSpace", 36, "Radio Station: Breathwork", "Radio Music: Breathwork"),
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
new one, so it pins the mapping with no room for doubt.
"""


def chairlift_locations() -> tuple[str, ...]:
    """Player-facing names of every location behind the Green Cup Key."""
    return _locations_for(CHAIRLIFT_PUZZLES, CHAIRLIFT_RADIO)


def tunnel_locations() -> tuple[str, ...]:
    """Player-facing names of every location behind the Yellow Twist Key."""
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


def feature_item_id(tower: Tower) -> int:
    """The door: map room, chairlift, train, tunnels, drawbridge, dam, dome."""
    return BASE_ID + tower.prop_value


def key_item_name(tower: Tower) -> str:
    """
    The physical key a player carries, cuts and places.

    Named after the feature rather than after the tower ("Chairlift Key",
    not "Green Cup Key") so that it matches its own locations — `Chairlift
    Key Cut 1` and `Chairlift Key Deposit` — and so that nothing has to be
    memorised to know where a key belongs. That match holds for all seven
    towers since the deposit renames of 2026-09-21; it used to hold for four.
    """
    return f"{tower.item_name} Key"


def key_item_id(tower: Tower) -> int:
    return BASE_ID + KEY_ITEM_ID_OFFSET + tower.prop_value


def cut_location_name(tower: Tower, index: int) -> str:
    """Player-facing name of "the Nth segment of this key was cut".

    Numbered from 1 for the player; the mod counts the SyncList from 0, which
    is what `cut_location_id` undoes.
    """
    return f"{tower.item_name} Key Cut {index + 1}"


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


def deposit_location_name(amount: int) -> str:
    return f"Gourd Deposit {amount}"


def deposit_location_id(amount: int) -> int:
    return BASE_ID + DEPOSIT_ID_OFFSET + amount
