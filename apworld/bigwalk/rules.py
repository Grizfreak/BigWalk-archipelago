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

from rule_builder.rules import Has, HasAll

from . import data, locations, regions
from .options import GauntletMode, Goal

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
    set_puzzle_rules(world)
    set_gauntlet_rules(world)
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


def set_puzzle_rules(world: BigWalkWorld) -> None:
    """
    With `lock_puzzle_needs`, a puzzle is in logic once every item it is built
    from has been received (`data.PUZZLE_TAGS`). It is a rule the mod never
    enforces on the puzzle itself: the mod only hides the parts until their
    item arrives (mod/src/Core/PuzzleNeedHider.cs). Solving one early sends its
    check early; it only stops generation relying on it. No puzzle sits behind
    an item of its own.

    The tutorial's four (`data.START_ZONE_PUZZLES`) are gated like the others:
    they are built from things too. They sit in the one region nothing else in
    this world locks (arch doors and the drawbridge only gate the way OUT of
    it), so a seed that left one of them with nothing to do would have no
    location reachable at empty state: `start_with_random_puzzle_need` is what
    keeps one of them doable.
    """
    if not world.options.lock_puzzle_needs:
        return

    for puzzle in data.PUZZLES:
        required = [need.item_name for need in data.puzzle_needs(puzzle)]
        if required:
            world.set_rule(world.get_location(puzzle.location_name), HasAll(*required))


def set_gauntlet_rules(world: BigWalkWorld) -> None:
    """
    With `locked_stages`, stage k's puzzle is only reachable through the
    stairways of the k-1 stages before it, each an item. With
    `gauntlet_puzzles_required` it is also reachable only through the puzzles
    of those stages, which `lock_puzzle_needs` makes items too
    (`data.gauntlet_needs_through`); without it each item opens its stage whole,
    and a puzzle needs only what it is built from (`data.gauntlet_needs_of`).
    """
    if world.options.gauntlet_mode != GauntletMode.option_locked_stages:
        return

    for stage in data.GAUNTLET_STAGES:
        required = [before.item_name for before in data.GAUNTLET_STAGES[:stage.index]]
        if world.options.lock_puzzle_needs:
            needs = (data.gauntlet_needs_through(stage) if world.options.gauntlet_puzzles_required
                     else data.gauntlet_needs_of(stage))
            required += [need.item_name for need in needs]
        if required:
            world.set_rule(world.get_location(stage.location_name), HasAll(*required))


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

    # The way out of the starting zone, gated only while the first arch door
    # is held closed: then it is the drawbridge or that door. Otherwise the
    # mod opens the door on the first session and the exit is free.
    if data.FIRST_ARCH_DOOR in world.locked_arch_doors:
        world.set_rule(
            world.get_entrance(regions.STARTING_EXIT),
            Has(data.DRAWBRIDGE_ITEM_NAME) | Has(data.FIRST_ARCH_DOOR.item_name),
        )

    # The Spawn Secret Door, and the second ending behind it.
    world.set_rule(
        world.get_entrance(regions.GREEN_DOME_ENTRANCE),
        Has(data.GREEN_DOME.item_name),
    )


def set_completion_rule(world: BigWalkWorld) -> None:
    if world.options.goal == Goal.option_big_collection:
        world.set_rule(
            world.get_location(locations.VICTORY_EVENT_NAME),
            Has(data.GOURD_ITEM_NAME, count=world.deposit_goal),
        )

    # Every other goal is a place, not a count, and the region the Victory
    # event was put in already carries the requirement: the two bell goals
    # sit past the Black Monolith Key, and `big_game` sits past the
    # Spawn Secret Door.
    #
    # The Gauntlet's seven chambers have no items or checks of their own:
    # nothing in the game persists them individually (`GauntletChamber0..6`
    # were never found written anywhere), so the logic cannot and does not
    # model them. `big_game` asks for nothing beyond the door for the
    # same kind of reason — see locations.create_victory_event.
    # Only the goal that is the Gauntlet asks for its stages, and only when
    # they are items: leaving it needs every stairway.
    if world.options.goal == Goal.option_big_goodbye and world.options.gauntlet_mode == GauntletMode.option_locked_stages:
        required = [stage.item_name for stage in data.GAUNTLET_STAGES]
        if world.options.lock_puzzle_needs and world.options.gauntlet_puzzles_required:
            required += [need.item_name for need in data.gauntlet_needs_through(data.GAUNTLET_STAGES[-1])]
        world.set_rule(world.get_location(locations.VICTORY_EVENT_NAME), HasAll(*required))

    world.set_completion_rule(Has(locations.VICTORY_EVENT_NAME))
