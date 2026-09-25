"""Access rules for the Big Walk world.

Only one resource gates anything here: the number of `Gourd` items received.
That is a deliberate consequence of how the mod works — a received gourd is a
cosmetic prop the players carry to a monument, entirely decoupled from puzzle
solving, and the Archipelago server is the only thing that decides how many of
them exist. Nothing in the logic depends on *where* a gourd ends up, which is
what makes the whole model softlock-proof (Option A in ../design-decisions.md).
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from rule_builder.rules import Has

from . import data, locations, regions
from .options import Goal

if TYPE_CHECKING:
    from .world import BigWalkWorld


# WHAT USED TO BE HERE, and why it is gone (2026-09-21).
#
# `gourd_requirements` charged each tower's key deposit a running total of
# gourds, because a tower's key only appeared once its monument was full and
# a deposited gourd cannot be taken back out. That was a faithful model of
# the vanilla game, and this world no longer plays it: the keys are
# Archipelago items that spawn like gourds, and a full monument buys nothing
# but its own deposit check. Cutting and placing a key needs the key, and
# the key alone.
#
# Nothing else in the world used the cumulative count, so it is removed
# rather than left computed and unread.


def set_all_rules(world: BigWalkWorld) -> None:
    set_key_deposit_rules(world)
    set_key_cut_rules(world)
    set_gourd_deposit_rules(world)
    set_entrance_rules(world)
    set_completion_rule(world)


def set_key_deposit_rules(world: BigWalkWorld) -> None:
    """Placing a key in its receptacle needs that key, and nothing else."""
    for tower in world.towers:
        world.set_rule(
            world.get_location(tower.location_name),
            Has(data.key_item_name(tower)),
        )


def set_key_cut_rules(world: BigWalkWorld) -> None:
    """
    Each cut segment costs what its tower's deposit costs.

    Cutting a key physically requires holding it, and a key is now an
    Archipelago item rather than something a full monument hands over. So
    all six of a tower's locations — its five cuts and its placement — rest
    on the same single requirement, and no gourd is involved anywhere.
    """
    for tower in world.towers:
        requirement = Has(data.key_item_name(tower))
        for name in data.cut_locations(tower):
            world.set_rule(world.get_location(name), requirement)


def set_gourd_deposit_rules(world: BigWalkWorld) -> None:
    for amount in world.deposit_amounts:
        world.set_rule(
            world.get_location(data.deposit_location_name(amount)),
            Has(data.GOURD_ITEM_NAME, count=amount),
        )


def set_entrance_rules(world: BigWalkWorld) -> None:
    # Confirmed in-game: placing `bigKeyBoss` in `bigKeyPlinthEnding` is what
    # opens the way to the chapel, and everything past it — the field, the
    # Gauntlet and its final bell — is behind that one door.
    world.set_rule(
        world.get_entrance(regions.ENDING_ENTRANCE),
        Has(data.BLACK_MONOLITH.item_name),
    )

    # Found in play on 2026-09-21, and the reason this world had a
    # seed-breaking bug: the chairlift and the tunnels are not scenery, they
    # are doors. Without these two rules nothing stopped generation putting
    # the Green Cup Key past the chairlift it opens.
    world.set_rule(
        world.get_entrance(regions.CHAIRLIFT_ENTRANCE),
        Has(data.GREEN_CUP.item_name),
    )
    world.set_rule(
        world.get_entrance(regions.TUNNEL_ENTRANCE),
        Has(data.YELLOW_TWIST.item_name),
    )

    # The Hub Secret Door, and the second ending behind it.
    world.set_rule(
        world.get_entrance(regions.GREEN_DOME_ENTRANCE),
        Has(data.GREEN_DOME.item_name),
    )


def set_completion_rule(world: BigWalkWorld) -> None:
    if world.options.goal == Goal.option_deposits:
        world.set_rule(
            world.get_location(locations.VICTORY_EVENT_NAME),
            Has(data.GOURD_ITEM_NAME, count=world.deposit_goal),
        )

    # Every other goal is a place, not a count, and the region the Victory
    # event was put in already carries the requirement: the two bell goals
    # sit past the Black Monolith Key, and `second_ending` sits past the
    # Hub Secret Door.
    #
    # The Gauntlet's seven chambers have no items or checks of their own:
    # nothing in the game persists them individually (`GauntletChamber0..6`
    # were never found written anywhere), so the logic cannot and does not
    # model them. `second_ending` asks for nothing beyond the door for the
    # same kind of reason — see locations.create_victory_event.
    world.set_completion_rule(Has(locations.VICTORY_EVENT_NAME))
