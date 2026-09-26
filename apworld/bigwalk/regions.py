"""Region graph for the Big Walk world.

Big Walk is *mostly* an open map. The towers' entrance doors are opened by a
button anyone can press, the mod opens the hub shortcuts itself on a save's
first session (`Core/ArchDoorUnlocker.cs`), and no puzzle is ever locked by the
mod — a received gourd is monument currency, it never validates a puzzle. So
most puzzles, radio stations and key deposits live in one region, and what
actually gates progress is the gourd count in rules.py.

Three structural gates are modelled, all of them big keys:

- **The ending.** The Black Monolith Key (`bigKeyBoss` -> `bigKeyPlinthEnding`)
  opens the Big Wall, the field behind it and the Gauntlet beyond that.
- **The chairlift.** The Green Cup Key opens it, and past it sit the purple
  "variant challenge" gourds and a radio station. Found in play on 2026-09-21:
  until then this file claimed the island was open apart from the ending, which
  let generation place the Green Cup Key behind the chairlift it opens and make
  the seed unbeatable.
- **The tunnels.** The Yellow Twist Key opens them, and a radio station sits
  past them.
- **The Spawn Secret Door.** Its own item opens it, and the game's second
  ending is behind it.
- **The way out of the starting zone** (2026-09-25). Players spawn at the hub,
  and the only zone open from there is the tutorial; the drawbridge and the
  first arch door lead on to the rest of the island. The mod opens that door
  on a save's first session unless `start_with_arch_doors_open` leaves it
  out, so
  the gate only has a rule in that case: the Drawbridge or the First Arch
  Door. The two far arch doors are shortcuts and gate nothing.

Still deliberately *not* modelled: which puzzle belongs to which tower. That
mapping was never established, and inventing one would produce logic that looks
precise while being wrong. The two gates above are different — they were
observed, not inferred.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Region

from . import data

if TYPE_CHECKING:
    from .world import BigWalkWorld


STARTING_ZONE = "Starting Zone"
OVERWORLD = "Big Walk"
ENDING_ZONE = "Ending Zone"
CHAIRLIFT_ZONE = "Past the Chairlift"
TUNNEL_ZONE = "Past the Tunnels"
GREEN_DOME_ZONE = "Past the Spawn Secret Door"

STARTING_EXIT = f"{STARTING_ZONE} to {OVERWORLD}"
ENDING_ENTRANCE = f"{OVERWORLD} to {ENDING_ZONE}"
CHAIRLIFT_ENTRANCE = f"{OVERWORLD} to {CHAIRLIFT_ZONE}"
TUNNEL_ENTRANCE = f"{OVERWORLD} to {TUNNEL_ZONE}"
GREEN_DOME_ENTRANCE = f"{OVERWORLD} to {GREEN_DOME_ZONE}"


def create_and_connect_regions(world: BigWalkWorld) -> None:
    starting_zone = Region(STARTING_ZONE, world.player, world.multiworld)
    overworld = Region(OVERWORLD, world.player, world.multiworld)
    ending_zone = Region(ENDING_ZONE, world.player, world.multiworld)
    chairlift_zone = Region(CHAIRLIFT_ZONE, world.player, world.multiworld)
    tunnel_zone = Region(TUNNEL_ZONE, world.player, world.multiworld)
    green_dome_zone = Region(GREEN_DOME_ZONE, world.player, world.multiworld)
    world.multiworld.regions += [starting_zone, overworld, ending_zone, chairlift_zone, tunnel_zone, green_dome_zone]

    starting_zone.connect(overworld, STARTING_EXIT)
    overworld.connect(ending_zone, ENDING_ENTRANCE)
    overworld.connect(chairlift_zone, CHAIRLIFT_ENTRANCE)
    overworld.connect(tunnel_zone, TUNNEL_ENTRANCE)
    overworld.connect(green_dome_zone, GREEN_DOME_ENTRANCE)
