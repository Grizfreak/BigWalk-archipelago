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


def gourd_requirements(world: BigWalkWorld) -> dict[str, int]:
    """
    How many `Gourd` items each tower's key deposit costs, keyed by the big
    key's `SaveablePropName`.

    A tower's key only appears once its monument is full, and a gourd deposited
    into a monument **cannot be taken back out** — confirmed by the player, it
    is simply not possible in the base game. So gourds are spent, not lent: a
    monument's slots are paid for on top of every monument already filled, and
    the last key costs every gourd in the pool.

    The towers are therefore sorted by slot count and charged the running
    total. That sort is a logic device, not a claim about play order: it just
    says a player who has filled k monuments has paid at least for the k
    cheapest ones.
    """
    requirements: dict[str, int] = {}
    running_total = 0
    for tower in sorted(world.towers, key=world.slots_for):
        running_total += world.slots_for(tower)
        requirements[tower.prop_name] = running_total
    return requirements


def set_all_rules(world: BigWalkWorld) -> None:
    set_key_deposit_rules(world)
    set_gourd_deposit_rules(world)
    set_entrance_rules(world)
    set_completion_rule(world)


def set_key_deposit_rules(world: BigWalkWorld) -> None:
    for tower in world.towers:
        if tower.item_name == data.TUTORIAL_KEY_ITEM_NAME and world.options.start_with_tutorial_key:
            # The mod pins a received big key straight into its plinth and
            # reports that location itself, because doing so consumes the
            # plinth and the players could never place the key by hand
            # afterwards. A precollected Tutorial Key therefore checks its own
            # deposit the moment the client connects, before a single gourd
            # exists — so requiring gourds for it would be a lie.
            continue

        world.set_rule(
            world.get_location(tower.location_name),
            Has(data.GOURD_ITEM_NAME, count=world.key_requirements[tower.prop_name]),
        )


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


def set_completion_rule(world: BigWalkWorld) -> None:
    if world.options.goal == Goal.option_deposits:
        world.set_rule(
            world.get_location(locations.VICTORY_EVENT_NAME),
            Has(data.GOURD_ITEM_NAME, count=world.deposit_goal),
        )

    # For both bell goals the requirement is simply standing in the Ending Zone,
    # which the entrance rule above already covers. The Gauntlet's seven
    # chambers have no items or checks of their own: nothing in the game
    # persists them individually (`GauntletChamber0..6` were never found
    # written anywhere), so the logic cannot and does not model them.
    world.set_completion_rule(Has(locations.VICTORY_EVENT_NAME))
