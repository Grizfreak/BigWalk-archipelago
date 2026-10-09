"""Item definitions and pool construction for the Big Walk world."""

from __future__ import annotations

from collections import Counter
from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification
from Options import OptionError

from . import data
from . import options as bigwalk_options

if TYPE_CHECKING:
    from .world import BigWalkWorld


# Item ids mirror the game's own enum values wherever one exists, so the mod
# can derive them arithmetically instead of duplicating a table. See data.py.
ITEM_NAME_TO_ID: dict[str, int] = {
    data.GOURD_ITEM_NAME: data.BASE_ID + 1,
    **{tower.item_name: data.feature_item_id(tower) for tower in data.TOWERS},
    **{data.key_item_name(tower): data.key_item_id(tower) for tower in data.TOWERS},
    **{station.item_name: data.radio_item_id(station) for station in data.RADIO_STATIONS},
    **{door.item_name: data.arch_door_item_id(door) for door in data.ARCH_DOORS},
    **{need.item_name: data.puzzle_need_item_id(need) for need in data.PUZZLE_NEEDS},
    **{stage.item_name: data.gauntlet_stage_id(stage) for stage in data.GAUNTLET_STAGES},
    data.PROGRESSIVE_GAUNTLET_ITEM_NAME: data.progressive_gauntlet_id(),
    **{name: data.BASE_ID + offset for name, offset in data.FILLER_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.TRAP_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.BONUS_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.JOKE_ITEMS},
    **{data.teleport_item_name(name): data.teleport_item_id(i) for i, (_, name) in enumerate(data.TELEPORT_DESTINATIONS)},
}

FILLER_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.FILLER_ITEMS)
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.TRAP_ITEMS)
BONUS_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.BONUS_ITEMS)
JOKE_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.JOKE_ITEMS)
TELEPORT_ITEM_NAMES: tuple[str, ...] = tuple(data.teleport_item_name(name) for _, name in data.TELEPORT_DESTINATIONS)

# The gear the pool always holds, and the option that sets how many of each.
GUARANTEED_GEAR: dict[str, str] = {
    "Backpack": "backpacks_in_pool",
    "Belt": "belts_in_pool",
    "Gourd Carton": "gourd_cartons_in_pool",
}

# Never drawn as filler (player, 2026-10-07): the four flare guns come in with `flare_gun_sanity`
# and the Rainbow Flare Gun always, once each, in create_all_items.
FLARE_GUN_NAMES: frozenset[str] = frozenset((*data.FLARE_GUN_ITEM_NAMES, data.RAINBOW_FLARE_GUN_ITEM_NAME))

RADIO_ITEM_NAMES: tuple[str, ...] = tuple(station.item_name for station in data.RADIO_STATIONS)

PUZZLE_NEED_ITEM_NAMES: tuple[str, ...] = tuple(need.item_name for need in data.PUZZLE_NEEDS)

GAUNTLET_ITEM_NAMES: tuple[str, ...] = tuple(stage.item_name for stage in data.GAUNTLET_STAGES)

# Two groups per tower, and they are genuinely different things. A Feature
# is the door — the chairlift, the train, the map room — and opens on
# receipt. A Big Key is the physical key: it arrives and spawns like a
# gourd, and what it is FOR is being cut five times and placed once, which
# is six checks. Receiving a key opens nothing by itself.
ITEM_NAME_GROUPS: dict[str, set[str]] = {
    "Features": {tower.item_name for tower in data.TOWERS},
    "Big Keys": {data.key_item_name(tower) for tower in data.TOWERS},
    "Radio Music": set(RADIO_ITEM_NAMES),
    "Arch Doors": {door.item_name for door in data.ARCH_DOORS},
    "Puzzle Needs": set(PUZZLE_NEED_ITEM_NAMES),
    "Gauntlet Doors": set(GAUNTLET_ITEM_NAMES) | {data.PROGRESSIVE_GAUNTLET_ITEM_NAME},
    "Filler": set(FILLER_ITEM_NAMES),
    "Joke Filler": set(JOKE_ITEM_NAMES),
    "Teleporters": set(TELEPORT_ITEM_NAMES),
    "Traps": set(TRAP_ITEM_NAMES),
    "Hard Traps": {trap.item_name for trap in data.TRAPS if trap.hard},
    "Bonuses": set(BONUS_ITEM_NAMES),
}


class BigWalkItem(Item):
    game = "Big Walk"

    def __repr__(self) -> str:
        # Spoiler log: show gourd_name, keep item.name for the rules.
        text = super().__repr__()
        region = self.location.parent_region if self.location else None
        if self.name != data.GOURD_ITEM_NAME or region is None or region.multiworld is None:
            return text
        world = region.multiworld.worlds.get(self.player)
        return getattr(world, "gourd_name", self.name) + text[len(self.name):]


def classification_for(name: str, require_arch_doors: bool = False) -> ItemClassification:
    if name == data.GOURD_ITEM_NAME:
        # Gourds are the only currency that fills monuments, and monuments are
        # what release the big keys, so every single one is progression.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Features"]:
        # Every one of them opens part of the island, and four of them —
        # the chairlift, the tunnels, the chapel door and the hub secret
        # door — gate regions outright.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Big Keys"]:
        # Progression because each one unlocks six locations of its own —
        # five cuts and a placement — and nothing else in the pool can.
        return ItemClassification.progression
    if name == data.FIRST_ARCH_DOOR.item_name:
        # One of the two ways out of the starting zone when it is locked.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Arch Doors"]:
        # Shortcuts: they shorten the walk, and gate what lies past them only when
        # `require_arch_doors` puts those places behind them in logic.
        return ItemClassification.progression if require_arch_doors else ItemClassification.useful
    if name in ITEM_NAME_GROUPS["Teleporters"]:
        # A comfort the logic does not count on: a teleport can only shorten a walk.
        return ItemClassification.useful
    if name in ITEM_NAME_GROUPS["Gauntlet Doors"]:
        # Only ever created with `gauntlet_mode: locked_stages`, where each one
        # is the way up out of its stage and the goal asks for all seven.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Puzzle Needs"]:
        # Only ever created when lock_puzzle_needs is on, where each one is
        # required by every puzzle built from it.
        return ItemClassification.progression
    if name in TRAP_ITEM_NAMES:
        return ItemClassification.trap
    if name in BONUS_ITEM_NAMES:
        # A boost for a while: nice to get, never needed.
        return ItemClassification.useful
    # A Radio Music item starts a piece of music playing and nothing else: no
    # rule in this world or any other can ever require one, so it is filler
    # that happens to do something, not a useful item.
    return ItemClassification.filler


def create_item(world: BigWalkWorld, name: str) -> BigWalkItem:
    return BigWalkItem(
        name, classification_for(name, bool(world.options.require_arch_doors)), ITEM_NAME_TO_ID[name], world.player)


def trap_weights(world: BigWalkWorld) -> dict[str, int]:
    """Each trap the seed may hold, by name, with its weight: `trap_weights`, less the hard ones
    when `hard_traps` is off, less every trap weighted 0."""
    weights = world.options.trap_weights.value
    hard = bool(world.options.hard_traps)
    return {
        trap.item_name: weights.get(trap.item_name, 0)
        for trap in data.TRAPS
        if (hard or not trap.hard) and weights.get(trap.item_name, 0) > 0
    }


def pick_trap(world: BigWalkWorld) -> str | None:
    weights = trap_weights(world)
    if not weights:
        return None
    return world.random.choices(list(weights), weights=list(weights.values()))[0]


def pick_bonus(world: BigWalkWorld) -> str | None:
    weights = {name: weight for name, weight in world.options.bonus_weights.value.items() if weight > 0}
    if not weights:
        return None
    return world.random.choices(list(weights), weights=list(weights.values()))[0]


def get_random_filler_item_name(world: BigWalkWorld) -> str:
    if world.random.randint(1, 100) <= world.options.trap_fill_percentage:
        trap = pick_trap(world)
        if trap is not None:
            return trap
    if world.random.randint(1, 100) <= world.options.bonus_fill_percentage:
        bonus = pick_bonus(world)
        if bonus is not None:
            return bonus
    if world.random.randint(1, 100) <= world.options.joke_filler_percentage:
        return world.random.choice(JOKE_ITEM_NAMES)
    return pick_island_object(world)


def pick_island_object(world: BigWalkWorld) -> str:
    """
    One of the island's objects, none past its `island_object_limits` count. Gear comes from
    the guaranteed block in create_all_items, so a top-up filler (create_filler is also called
    later by the generator) skips it. Once every object is at its limit, a bonus takes the place
    (by `bonus_weights`); with no bonus left either, the object the pool holds fewest of.
    """
    # Made on first use: an item link's group world never runs generate_early.
    counts = world.__dict__.setdefault("island_objects_made", Counter())
    limits = world.options.island_object_limits.value
    objects = [n for n in FILLER_ITEM_NAMES if n not in GUARANTEED_GEAR and n not in FLARE_GUN_NAMES]
    open_ = [n for n in objects if counts[n] < limits.get(n, 0)]
    if open_:
        name = world.random.choice(open_)
    else:
        bonus = pick_bonus(world)
        if bonus is not None:
            return bonus
        name = min(objects, key=lambda n: (counts[n], world.random.random()))
    counts[name] += 1
    return name


def create_all_items(world: BigWalkWorld) -> None:
    pool: list[Item] = [world.create_item(data.GOURD_ITEM_NAME) for _ in range(world.gourd_count)]

    for tower in world.towers:
        if tower.item_name == data.DRAWBRIDGE_ITEM_NAME and world.options.start_with_drawbridge_open:
            # Handed over up front rather than shuffled, so it never enters the
            # pool. Unlike before, this costs the player nothing and grants
            # nothing beyond the drawbridge: the key itself stays in its tower,
            # and its deposit location is still checked by walking the key over
            # there, at the same gourd price as every other tower (rules.py).
            world.push_precollected(world.create_item(tower.item_name))
            continue
        pool.append(world.create_item(tower.item_name))

    # The keys themselves, always shuffled and never precollected. Starting
    # with the drawbridge open is about being able to LEAVE the tutorial,
    # which is the feature; the tutorial key is just six checks like any
    # other key, and handing it over would be handing over checks.
    pool += [world.create_item(data.key_item_name(tower)) for tower in world.towers]

    # Seven Radio Music items displace seven filler rather than adding to the
    # pool: the count below is what balances it, so nothing special is needed.
    if world.options.shuffle_radio_music:
        pool += [world.create_item(station.item_name) for station in data.RADIO_STATIONS]

    # A locked arch door is an item, and like the radio music it displaces
    # filler rather than adding to the pool.
    pool += [world.create_item(door.item_name) for door in world.locked_arch_doors]

    # With `teleport_buttons: items`, each hub button is an item, displacing filler.
    if world.options.teleport_buttons == bigwalk_options.TeleportButtons.option_items:
        pool += [world.create_item(name) for name in TELEPORT_ITEM_NAMES]

    # With locked stages, the way up out of each one is an item. They displace
    # filler, like the arch doors.
    if world.options.gauntlet_mode == bigwalk_options.GauntletMode.option_locked_stages:
        if world.options.gauntlet_stage_items == bigwalk_options.GauntletStageItems.option_progressive:
            pool += [world.create_item(data.PROGRESSIVE_GAUNTLET_ITEM_NAME) for _ in data.GAUNTLET_STAGES]
        else:
            pool += [world.create_item(stage.item_name) for stage in data.GAUNTLET_STAGES]

    # The needs the player starts with are handed over up front, like the
    # drawbridge, and never enter the pool.
    if world.options.lock_puzzle_needs:
        for need in data.PUZZLE_NEEDS:
            if need.key in world.options.start_with_puzzle_needs.value:
                world.push_precollected(world.create_item(need.item_name))
            else:
                pool.append(world.create_item(need.item_name))

    unfilled = len(world.multiworld.get_unfilled_locations(world.player))
    filler_needed = unfilled - len(pool)
    if filler_needed < 0:
        raise OptionError(
            f"Big Walk: {world.player_name}'s options need {len(pool)} items for only {unfilled} "
            "locations. lock_puzzle_needs adds up to 17 items; raise gourd_sanity, turn radio_sanity "
            "on, list more start_with_puzzle_needs, or turn lock_puzzle_needs off to make room.")
    # Backpacks and belts are the only gear a player can wear, and a carton is
    # the one bulk container for gourds, so a seed that rolls none of them (one
    # name in seventeen each in the random draw) is a run without a bag. A few
    # are guaranteed instead (how many: the *_in_pool options), carved out of the filler
    # headroom and never past it — a tight pool loses them before it errors.
    for name, option in GUARANTEED_GEAR.items():
        take = min(getattr(world.options, option).value, filler_needed)
        pool += [world.create_item(name) for _ in range(take)]
        filler_needed -= take
    # The flare guns, from the same headroom, as many of each as `island_object_limits` says: the
    # island's four with their checks (`flare_gun_sanity`), and the Rainbow Flare Gun always.
    limits = world.options.island_object_limits.value
    flare_guns = [*(data.FLARE_GUN_ITEM_NAMES if world.options.flare_gun_sanity else ()), data.RAINBOW_FLARE_GUN_ITEM_NAME]
    for name in flare_guns:
        take = min(limits.get(name, 0), max(0, filler_needed))
        pool += [world.create_item(name) for _ in range(take)]
        filler_needed -= take
    pool += [world.create_filler() for _ in range(filler_needed)]

    world.multiworld.itempool += pool
