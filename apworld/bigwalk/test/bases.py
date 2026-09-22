from argparse import Namespace
from typing import Any

from BaseClasses import CollectionState, MultiWorld
from test.bases import WorldTestBase
from worlds.AutoWorld import call_all

from .. import data
from ..world import BigWalkWorld

UT_GEN_STEPS = ("generate_early", "create_regions", "create_items", "set_rules")
"""
The steps Universal Tracker runs, in its order (`TrackerCore.TMain`). It stops
at `generate_basic`, and never fills: item placement comes from the server.
"""


def build_like_universal_tracker(
    options: dict[str, Any],
    slot_data: dict[str, Any] | None = None,
) -> MultiWorld:
    """
    Build a Big Walk world the way Universal Tracker builds one, so a test can
    compare what the tracker would show against what was really generated.

    `options` is the YAML the tracking player happens to have — UT writes an
    empty one (every option at its default) for a world that declares
    `ut_can_gen_without_yaml`. `slot_data` is what the real seed sent; passing
    it reproduces UT's second pass, and leaving it out reproduces the first.
    """
    multiworld = MultiWorld(1)
    multiworld.game[1] = BigWalkWorld.game
    multiworld.player_name = {1: "Tracker"}
    multiworld.set_seed(0)

    # Both flags are UT's, set before any world exists: the first tells a
    # world it is generating inside the tracker, the second carries the seed's
    # own slot_data back to it.
    multiworld.generation_is_fake = True
    if slot_data is not None:
        multiworld.re_gen_passthrough = {BigWalkWorld.game: slot_data}

    args = Namespace()
    for name, option in BigWalkWorld.options_dataclass.type_hints.items():
        setattr(args, name, {1: option.from_any(options.get(name, option.default))})
    multiworld.set_options(args)
    multiworld.state = CollectionState(multiworld)

    for step in UT_GEN_STEPS:
        call_all(multiworld, step)
    return multiworld


def world_shape(multiworld: MultiWorld) -> dict[str, Any]:
    """
    Everything about a generated slot that a tracker can get wrong: which
    locations exist, which region each one sits in, and the numbers the rules
    are written against. Two slots with the same shape track identically.
    """
    world = multiworld.worlds[1]
    return {
        "regions": {
            region.name: sorted(location.name for location in region.locations)
            for region in multiworld.get_regions(1)
        },
        "gourd_count": world.gourd_count,
        "deposit_amounts": world.deposit_amounts,
        "deposit_goal": world.deposit_goal,
        "towers": [tower.prop_name for tower in world.towers],
    }


class BigWalkTestBase(WorldTestBase):
    game = "Big Walk"
    world: BigWalkWorld

    _collected_gourds = 0

    def collect_gourds(self, count: int) -> None:
        """
        Collect `count` more Gourd items than are already collected.

        Not `collect_by_name`: every gourd shares one name, so that helper
        collects the whole stack at once and any threshold test written with it
        would pass no matter what the rule says. Successive calls hand over
        gourds that have not been collected yet, so a test can walk a threshold
        one gourd at a time.
        """
        gourds = self.get_items_by_name(data.GOURD_ITEM_NAME)
        end = self._collected_gourds + count
        assert end <= len(gourds), f"only {len(gourds)} gourds in this pool, asked for {end}"
        self.collect(gourds[self._collected_gourds:end])
        self._collected_gourds = end
