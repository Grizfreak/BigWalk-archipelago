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
ARCH_DOOR_ID_OFFSET = 5_000
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
    What players call the tower this key belongs to — "Red Tower", "Green
    Dome" — and so the prefix of the key item and of its checks: "Red Tower
    Key", "Red Tower Key Cut 1", "Red Tower Key Deposit". The drawbridge has
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
    Tower("bigKeyRedZone", 301, "Map Room", "Red Tower", "Red Tower Key Deposit",
          "bigKeyPlinthMapRoom", "monoument0", 5, 5),
    Tower("bigKeyGreenZone", 302, "Chairlift", "Green Tower", "Green Tower Key Deposit",
          "bigKeyPlinthSkiLift", "monoument1", 5, 5),
    Tower("bigKeyBlueZone", 303, "Train", "Blue Tower", "Blue Tower Key Deposit",
          "bigKeyPlinthTrain", "monoument2", 5, 5),
    Tower("bigKeyYellowZone", 304, "Tunnels", "Yellow Tower", "Yellow Tower Key Deposit",
          "bigKeyPlinthTunnels", "monoument3", 5, 5),
    Tower("bigKeyBoss", 305, "Big Wall Door", "Black Tower", "Black Tower Key Deposit",
          "bigKeyPlinthEnding", "monoumentFinal", 6, 0),
    Tower("bigKeyOverflow", 306, SPAWN_SECRET_DOOR_ITEM_NAME, "Green Dome", "Green Dome Key Deposit",
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
    ("X-Ray Goggles", 9_014),
    ("Blue Flare Gun", 9_015),
    ("Green Flare Gun", 9_016),
    ("Yellow Flare Gun", 9_017),
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
"""

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

    Named after its tower ("Red Tower Key"), like its own locations — `Red
    Tower Key Cut 1` and `Red Tower Key Deposit` — so that nothing has to be
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


def arch_door_item_id(door: ArchDoor) -> int:
    return BASE_ID + ARCH_DOOR_ID_OFFSET + door.system_value


def deposit_location_name(amount: int) -> str:
    return f"Gourd Deposit {amount}"


def deposit_location_id(amount: int) -> int:
    return BASE_ID + DEPOSIT_ID_OFFSET + amount
