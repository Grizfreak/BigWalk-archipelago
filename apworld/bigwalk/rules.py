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

from itertools import combinations

from rule_builder.rules import And, CanReachLocation, CanReachRegion, Has, HasAll, HasAny, Or, Rule

from . import data, items, locations, regions
from . import options as bigwalk_options
from .options import GauntletMode, GauntletStageItems, Goal

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
    set_tower_reached_rules(world)
    set_key_cut_rules(world)
    set_gourd_deposit_rules(world)
    set_puzzle_rules(world)
    set_gauntlet_rules(world)
    set_entrance_rules(world)
    set_completion_rule(world)


def past_far_door(world: BigWalkWorld, door: data.ArchDoor | None, rule):
    """
    `rule`, and the far door it lies past as well when `require_arch_doors` is on and
    the door starts closed. A door that starts open, or none, adds nothing.
    """
    if door is None or not world.options.require_arch_doors or door not in world.locked_arch_doors:
        return rule

    return rule & Has(door.item_name)


def set_key_deposit_rules(world: BigWalkWorld) -> None:
    """Placing a key in its receptacle needs that key, and the far door its tower lies past."""
    for tower in world.towers:
        world.set_rule(
            world.get_location(tower.location_name),
            past_far_door(world, data.TOWER_FAR_DOOR.get(tower.prop_name), Has(data.key_item_name(tower))),
        )


def set_tower_reached_rules(world: BigWalkWorld) -> None:
    """Reaching a tower asks for nothing but the far door it lies past, when that door is an item."""
    if not world.options.tower_sanity:
        return

    for tower in data.reached_towers():
        door = data.TOWER_FAR_DOOR.get(tower.prop_name)
        if door is not None and world.options.require_arch_doors and door in world.locked_arch_doors:
            world.set_rule(world.get_location(data.tower_reached_location_name(tower)), Has(door.item_name))


def set_key_cut_rules(world: BigWalkWorld) -> None:
    """
    Each cut segment costs what its tower's deposit costs.

    Cutting a key physically requires holding it, and a key is now an
    Archipelago item rather than something a full monument hands over. So
    all six of a tower's locations — its five cuts and its placement — rest
    on the same single requirement, and no gourd is involved anywhere.
    """
    for tower in world.towers:
        requirement = past_far_door(world, data.TOWER_FAR_DOOR.get(tower.prop_name), Has(data.key_item_name(tower)))
        for name in data.cut_locations(tower):
            world.set_rule(world.get_location(name), requirement)


def doors_up_to(world: BigWalkWorld, count: int) -> list[Rule]:
    """The far doors the first `count` towers of the seed's order lie past, each once, when they are items."""
    doors = []
    for prop in world.tower_order[:count]:
        door = data.TOWER_FAR_DOOR.get(prop)
        if (door is not None and world.options.require_arch_doors and door in world.locked_arch_doors
                and door not in doors):
            doors.append(door)
    return [Has(door.item_name) for door in doors]


def monuments_up_to(world: BigWalkWorld, count: int) -> list[Rule]:
    """What the first `count` monuments of the order cost: their far doors, and with by_item one item for each after the first."""
    rules = doors_up_to(world, count)
    if world.options.tower_mode == bigwalk_options.TowerMode.option_by_item:
        # The k-th monument needs k - 1 items when the first is open from the start, k when it is not.
        needed = count - (1 if world.options.first_tower_open else 0)
        if needed > 0:
            rules.append(Has(data.PROGRESSIVE_MONUMENT_ITEM_NAME, count=needed))
    return rules


def tower_count_needing(world: BigWalkWorld, amount: int) -> int:
    """How many towers of the order the `amount`-th gourd placed reaches into (1 for the first tower's slots)."""
    total = 0
    for index, prop in enumerate(world.tower_order, start=1):
        total += world.tower_slots(data.tower_by_prop(prop))
        if amount <= total:
            return index
    return len(world.tower_order)


def set_gourd_deposit_rules(world: BigWalkWorld) -> None:
    for amount in world.deposit_amounts:
        rule: Rule = Has(data.GOURD_ITEM_NAME, count=amount)
        # With the monuments opening in order, the amount-th gourd goes into a tower the first gourds
        # have filled the ones before: it costs those towers' doors, and with by_item their items.
        # Exact, since only one monument takes gourds at a time: nothing is spent in a way the
        # logic did not count.
        for more in monuments_up_to(world, tower_count_needing(world, amount)) if world.tower_order else []:
            rule = rule & more
        world.set_rule(world.get_location(data.deposit_location_name(amount)), rule)


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


def gauntlet_parts_are_items(world: BigWalkWorld) -> bool:
    """Whether the Gauntlet's stages ask for the parts their puzzles are built from."""
    return bool(world.options.lock_puzzle_needs and world.options.lock_gauntlet_needs)


def stairways_up_to(world: BigWalkWorld, count: int) -> list[Rule]:
    """The stairways of the first `count` stages: that many progressive doors, or those named."""
    if count <= 0:
        return []
    if world.options.gauntlet_stage_items == GauntletStageItems.option_progressive:
        return [Has(data.PROGRESSIVE_GAUNTLET_ITEM_NAME, count=count)]
    return [HasAll(*[stage.item_name for stage in data.GAUNTLET_STAGES[:count]])]


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
        rules = stairways_up_to(world, stage.index)
        if gauntlet_parts_are_items(world):
            needs = (data.gauntlet_needs_through(stage) if world.options.gauntlet_puzzles_required
                     else data.gauntlet_needs_of(stage))
            if needs:
                rules.append(HasAll(*[need.item_name for need in needs]))
        if rules:
            rule = rules[0]
            for more in rules[1:]:
                rule = rule & more
            world.set_rule(world.get_location(stage.location_name), rule)


def set_entrance_rules(world: BigWalkWorld) -> None:
    # Confirmed in-game: placing `bigKeyBoss` in `bigKeyPlinthEnding` is what
    # opens the way to the chapel, and everything past it — the field, the
    # Gauntlet and its final bell — is behind that one door.
    world.set_rule(
        world.get_entrance(regions.ENDING_ENTRANCE),
        past_far_door(world, data.ZONE_FAR_DOOR["ending"], Has(data.BLACK_MONOLITH.item_name)),
    )

    # Found in play on 2026-09-21, and the reason this world had a
    # seed-breaking bug: the chairlift and the tunnels are not scenery, they
    # are doors. Without these two rules nothing stopped generation putting
    # the Green Cup Key past the chairlift it opens.
    world.set_rule(
        world.get_entrance(regions.CHAIRLIFT_ENTRANCE),
        past_far_door(world, data.ZONE_FAR_DOOR["chairlift"], Has(data.GREEN_CUP.item_name)),
    )
    world.set_rule(
        world.get_entrance(regions.TUNNEL_ENTRANCE),
        past_far_door(world, data.ZONE_FAR_DOOR["tunnels"], Has(data.YELLOW_TWIST.item_name)),
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
        past_far_door(world, data.ZONE_FAR_DOOR["green_dome"], Has(data.GREEN_DOME.item_name)),
    )


def gauntlet_goal_rule(world: BigWalkWorld) -> Rule | None:
    """
    What leaving the Gauntlet asks for: every stairway when they are items, and every part its
    puzzles are built from when those are items. In `vanilla` the stages are solved in the game's
    own way, so all of them are; with locked stages only when the puzzles are required.
    """
    locked = world.options.gauntlet_mode == GauntletMode.option_locked_stages
    rules = stairways_up_to(world, len(data.GAUNTLET_STAGES)) if locked else []
    if gauntlet_parts_are_items(world) and (not locked or world.options.gauntlet_puzzles_required):
        needs = data.gauntlet_needs_through(data.GAUNTLET_STAGES[-1])
        if needs:
            rules.append(HasAll(*[need.item_name for need in needs]))
    if not rules:
        return None
    rule = rules[0]
    for more in rules[1:]:
        rule = rule & more
    return rule


def reach_at_least(names: list[str], count: int) -> Rule:
    """
    At least `count` of these locations reachable. Written as an Or over every choice of
    `count` of them (never more than 35 for the seven keys or stations), since the rule builder
    has no counting rule and a lambda would not survive its caching.
    """
    if count >= len(names):
        return And(*[CanReachLocation(name) for name in names])
    return Or(*[And(*[CanReachLocation(name) for name in choice]) for choice in combinations(names, count)])


def set_completion_rule(world: BigWalkWorld) -> None:
    # Objectives listed (ROADMAP G5): every one of them, in any order. Each asks for what its
    # single goal asked for; the places are regions, since the event does not sit in any of them.
    if world.objectives:
        rule: Rule | None = None
        for key in world.objective_keys():
            if key == "big_collection":
                this: Rule = Has(data.GOURD_ITEM_NAME, count=world.deposit_goal)
            elif key == "big_climb":
                # A place the host sees a player at; nothing but the island itself to reach.
                this = CanReachRegion(regions.OVERWORLD)
            elif key == "big_fiesta":
                # The party on the beach: a radio plugged into the big speakers, with a music playing (any
                # station). With `shuffle_radio_music` the stations are items: one of them is needed.
                this = CanReachRegion(regions.OVERWORLD) & Has(items.RADIO_ITEM_NAME)
                if world.options.shuffle_radio_music:
                    this = this & HasAny(*items.RADIO_ITEM_NAMES)
            elif key == "big_towers":
                count = world.options.towers_required.value
                capacity = sum(world.tower_slots(data.tower_by_prop(prop)) for prop in world.tower_order[:count])
                this = Has(data.GOURD_ITEM_NAME, count=capacity)
                for more in monuments_up_to(world, count):
                    this = this & more
            elif key == "big_keys":
                this = reach_at_least([tower.location_name for tower in data.TOWERS], world.options.keys_required.value)
            elif key == "big_radio":
                this = reach_at_least(
                    [station.location_name for station in data.RADIO_STATIONS], world.options.radio_stations_required.value)
            elif key == "big_game":
                this = CanReachRegion(regions.GREEN_DOME_ZONE)
            else:
                this = CanReachRegion(regions.ENDING_ZONE)
                if key == "big_goodbye":
                    gauntlet = gauntlet_goal_rule(world)
                    if gauntlet is not None:
                        this = this & gauntlet
            rule = this if rule is None else rule & this
        world.set_rule(world.get_location(locations.VICTORY_EVENT_NAME), rule)
        world.set_completion_rule(Has(locations.VICTORY_EVENT_NAME))
        return

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
    # Only the goal that is the Gauntlet asks for anything of it. Leaving it takes
    # every stairway when they are items, and every part its puzzles are built
    # from when those are items: in `vanilla` the stages are solved in the game's
    # own way, so all of them are, and with locked stages only when the puzzles
    # are required.
    if world.options.goal == Goal.option_big_goodbye:
        rule = gauntlet_goal_rule(world)
        if rule is not None:
            world.set_rule(world.get_location(locations.VICTORY_EVENT_NAME), rule)

    world.set_completion_rule(Has(locations.VICTORY_EVENT_NAME))
