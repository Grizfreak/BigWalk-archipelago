"""Region graph for the Big Walk world.

Big Walk is an open map and this world keeps it that way. The towers' entrance
doors are opened by a button anyone can press, the mod opens the hub shortcuts
itself on a save's first session (`Core/ArchDoorUnlocker.cs`), and no puzzle is
ever locked by the mod — a received gourd is monument currency, it never
validates a puzzle. So every puzzle, radio station and key deposit lives in one
region, and what actually gates progress is the gourd count in rules.py.

The one structural gate we are confident about is the ending: the Black
Monolith Key (`bigKeyBoss` -> `bigKeyPlinthEnding`) opens the chapel, the field
behind it and the Gauntlet beyond that. That is the second region.

Deliberately *not* modelled: which puzzle belongs to which tower. That mapping
was never established, and inventing one would produce logic that looks
precise while being wrong.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Region

if TYPE_CHECKING:
    from .world import BigWalkWorld


OVERWORLD = "Big Walk"
ENDING_ZONE = "Ending Zone"

ENDING_ENTRANCE = f"{OVERWORLD} to {ENDING_ZONE}"


def create_and_connect_regions(world: BigWalkWorld) -> None:
    overworld = Region(OVERWORLD, world.player, world.multiworld)
    ending_zone = Region(ENDING_ZONE, world.player, world.multiworld)
    world.multiworld.regions += [overworld, ending_zone]

    overworld.connect(ending_zone, ENDING_ENTRANCE)
