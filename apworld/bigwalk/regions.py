"""Region graph for the Big Walk world.

Big Walk is *mostly* an open map. The towers' entrance doors are opened by a
button anyone can press, the mod opens the hub shortcuts itself on a save's
first session (`Core/ArchDoorUnlocker.cs`), and no puzzle is ever locked by the
mod — a received gourd is monument currency, it never validates a puzzle. So
most puzzles, radio stations and key deposits live in one region, and what
actually gates progress is the gourd count in rules.py.

Three structural gates are modelled, all of them big keys:

- **The ending.** The Black Monolith Key (`bigKeyBoss` -> `bigKeyPlinthEnding`)
  opens the chapel, the field behind it and the Gauntlet beyond that.
- **The chairlift.** The Green Cup Key opens it, and past it sit the purple
  "variant challenge" gourds and a radio station. Found in play on 2026-09-21:
  until then this file claimed the island was open apart from the ending, which
  let generation place the Green Cup Key behind the chairlift it opens and make
  the seed unbeatable.
- **The tunnels.** The Yellow Twist Key opens them, and a radio station sits
  past them.

Still deliberately *not* modelled: which puzzle belongs to which tower. That
mapping was never established, and inventing one would produce logic that looks
precise while being wrong. The two gates above are different — they were
observed, not inferred.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Region

if TYPE_CHECKING:
    from .world import BigWalkWorld


OVERWORLD = "Big Walk"
ENDING_ZONE = "Ending Zone"
CHAIRLIFT_ZONE = "Past the Chairlift"
TUNNEL_ZONE = "Past the Tunnels"

ENDING_ENTRANCE = f"{OVERWORLD} to {ENDING_ZONE}"
CHAIRLIFT_ENTRANCE = f"{OVERWORLD} to {CHAIRLIFT_ZONE}"
TUNNEL_ENTRANCE = f"{OVERWORLD} to {TUNNEL_ZONE}"


def create_and_connect_regions(world: BigWalkWorld) -> None:
    overworld = Region(OVERWORLD, world.player, world.multiworld)
    ending_zone = Region(ENDING_ZONE, world.player, world.multiworld)
    chairlift_zone = Region(CHAIRLIFT_ZONE, world.player, world.multiworld)
    tunnel_zone = Region(TUNNEL_ZONE, world.player, world.multiworld)
    world.multiworld.regions += [overworld, ending_zone, chairlift_zone, tunnel_zone]

    overworld.connect(ending_zone, ENDING_ENTRANCE)
    overworld.connect(chairlift_zone, CHAIRLIFT_ENTRANCE)
    overworld.connect(tunnel_zone, TUNNEL_ENTRANCE)
