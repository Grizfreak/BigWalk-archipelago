"""Item definitions and pool construction for the Big Walk world."""

from __future__ import annotations

from collections import Counter
from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification
from Options import OptionError

from . import data, gourd_name
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
    data.PROGRESSIVE_MONUMENT_ITEM_NAME: data.progressive_monument_id(),
    **{ability.item_name: data.ability_item_id(ability) for ability in data.ABILITIES},
    **{name: data.BASE_ID + offset for name, offset in data.FILLER_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.TRAP_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.BONUS_ITEMS},
    **{data.teleport_item_name(name): data.teleport_item_id(i) for i, (_, name) in enumerate(data.TELEPORT_DESTINATIONS)},
    **{lock.item_name: data.zone_lock_item_id(lock) for lock in data.ZONE_LOCKS},
}

FILLER_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.FILLER_ITEMS)
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.TRAP_ITEMS)
BONUS_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.BONUS_ITEMS)
TELEPORT_ITEM_NAMES: tuple[str, ...] = tuple(data.teleport_item_name(name) for _, name in data.TELEPORT_DESTINATIONS)

# The gear the pool always holds, and the option that sets how many of each.
GUARANTEED_GEAR: dict[str, str] = {
    "Backpack": "backpacks_in_pool",
    "Belt": "belts_in_pool",
    data.GOURD_CARTON_ITEM_NAME: "gourd_cartons_in_pool",
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
    "Monuments": {data.PROGRESSIVE_MONUMENT_ITEM_NAME},
    "Abilities": {ability.item_name for ability in data.ABILITIES},
    "Zone Locks": {lock.item_name for lock in data.ZONE_LOCKS},
    "Filler": set(FILLER_ITEM_NAMES),
    "Teleporters": set(TELEPORT_ITEM_NAMES),
    "Traps": set(TRAP_ITEM_NAMES),
    "Hard Traps": {trap.item_name for trap in data.TRAPS if trap.hard},
    "Bonuses": set(BONUS_ITEM_NAMES),
    # What players call it (2026-10-14); with gourd_name, "<name> Carrier" as well (gourd_name.py).
    "Gourd Carrier": {data.GOURD_CARTON_ITEM_NAME},
}


def island_object_limits(world: BigWalkWorld) -> dict[str, int]:
    """`island_object_limits`, with the Scanner's older name ("X-Ray Goggles") read as the Scanner."""
    limits = dict(world.options.island_object_limits.value)
    if "X-Ray Goggles" in limits:
        limits.setdefault("Scanner", limits.pop("X-Ray Goggles"))
    return limits


class BigWalkItem(Item):
    game = "Big Walk"

    def __repr__(self) -> str:
        # Spoiler log: show gourd_name (and the carton after it), keep item.name for the rules.
        text = super().__repr__()
        region = self.location.parent_region if self.location else None
        if (self.name not in (data.GOURD_ITEM_NAME, data.GOURD_CARTON_ITEM_NAME)
                or region is None or region.multiworld is None):
            return text
        world = region.multiworld.worlds.get(self.player)
        name = getattr(world, "gourd_name", data.GOURD_ITEM_NAME)
        shown = name if self.name == data.GOURD_ITEM_NAME else gourd_name.carton_name(name)
        return shown + text[len(self.name):]


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
    if name in ITEM_NAME_GROUPS["Monuments"]:
        # Only ever created with `tower_mode: by_item`, where the k-th one opens the k-th tower after the first.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Zone Locks"]:
        # Only ever created with `zone_locks`, for the checks it locks.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Puzzle Needs"]:
        # Only ever created when lock_puzzle_needs is on, where each one is
        # required by every puzzle built from it.
        return ItemClassification.progression
    if name == data.RUNNING_ITEM_NAME:
        # Nothing asks for it: the island is only slower to cross without it.
        return ItemClassification.useful
    if name in ITEM_NAME_GROUPS["Abilities"]:
        # Only ever created with `lock_abilities`: Legs is the way out of the starting zone, the rest is asked
        # for by the puzzles that need it (data.ABILITY_PUZZLES).
        return ItemClassification.progression
    if name in TRAP_ITEM_NAMES:
        return ItemClassification.trap
    if name == data.CONFETTI_ITEM_NAME:
        # Drawn with the bonuses, but only a show: filler, so a junk-only check can hold it.
        return ItemClassification.filler
    if name in BONUS_ITEM_NAMES:
        # A boost for a while: nice to get, never needed.
        return ItemClassification.useful
    # A Radio Music item starts a piece of music playing and nothing else: no
    # rule in this world or any other can ever require one, so it is filler
    # that happens to do something, not a useful item.
    return ItemClassification.filler


RADIO_ITEM_NAME = "Radio"
"""The portable radio, a gadget item (the world's own radios are taken out of the island)."""


def party_asked_for(world: BigWalkWorld) -> bool:
    return "big_party" in world.objectives


def create_item(world: BigWalkWorld, name: str) -> BigWalkItem:
    classification = classification_for(name, bool(world.options.require_arch_doors))
    # The party (`big_party`) needs a radio and a music: from filler, they become what the goal waits for.
    if party_asked_for(world) and (name == RADIO_ITEM_NAME or name in RADIO_ITEM_NAMES):
        classification = ItemClassification.progression
    return BigWalkItem(name, classification, ITEM_NAME_TO_ID[name], world.player)


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
    return pick_island_object(world)


def pick_island_object(world: BigWalkWorld) -> str:
    """
    One of the island's objects, drawn among those under their `island_object_limits` count. Once
    every object is at its limit, the objects not left out (limit above 0) go on past it, the one the
    pool holds fewest of each time (player, 2026-10-14: junk for the big sanities, not a pool of
    bonuses); with every object left out, a bonus (by `bonus_weights`), else the fewest of all. Gear
    comes from the guaranteed block in create_all_items, so a top-up filler (create_filler is also
    called later by the generator) skips it.
    """
    # Made on first use: an item link's group world never runs generate_early.
    counts = world.__dict__.setdefault("island_objects_made", Counter())
    limits = island_object_limits(world)
    objects = [n for n in FILLER_ITEM_NAMES if n not in GUARANTEED_GEAR and n not in FLARE_GUN_NAMES]
    open_ = [n for n in objects if counts[n] < limits.get(n, 0)]
    kept = [n for n in objects if limits.get(n, 0) > 0]
    if open_:
        name = world.random.choice(open_)
    elif kept:
        name = min(kept, key=lambda n: (counts[n], world.random.random()))
    else:
        bonus = pick_bonus(world)
        if bonus is not None:
            return bonus
        name = min(objects, key=lambda n: (counts[n], world.random.random()))
    counts[name] += 1
    return name


def abilities_in_pool(world: BigWalkWorld) -> tuple[data.Ability, ...]:
    """The ability items `lock_abilities` puts in the pool (each `copies` times): the arms one by one, or both together."""
    mode = world.options.lock_abilities
    if mode == bigwalk_options.LockAbilities.option_handed:
        chosen = tuple(a for a in data.ABILITIES if a.handed)
    elif mode == bigwalk_options.LockAbilities.option_ambidextrous:
        chosen = tuple(a for a in data.ABILITIES if a.ambidextrous)
    else:
        return ()
    if not world.options.lock_running:
        chosen = tuple(a for a in chosen if a.item_name != data.RUNNING_ITEM_NAME)
    return chosen


def packs_give_themselves(world: BigWalkWorld) -> bool:
    return world.options.pack_sanity == bigwalk_options.PackSanity.option_vanilla


def pack_item_name(pickup: data.Pickup) -> str:
    """The item a pack of the island is: by the end of its location's name (Sky Blue Backpack, Gourd Carton Pickup)."""
    if pickup.location_name.endswith("Backpack"):
        return "Backpack"
    if pickup.location_name.endswith("Belt"):
        return "Belt"
    return data.GOURD_CARTON_ITEM_NAME


def create_all_items(world: BigWalkWorld) -> None:
    gourds = world.gourd_count
    # `linear_puzzles`: each puzzle holds a gourd, as in the game; those gourds leave the pool. Not the seven purple
    # ones past the chairlift (post-game in the game): with the keys and the towers linear too, a gourd behind the
    # chairlift could be needed to open the chairlift, and no seed could be made (random campaign, 2026-10-13).
    if world.options.linear_puzzles:
        for puzzle in [p for p in world.puzzles() if p.prop_name not in data.CHAIRLIFT_PUZZLES][:gourds]:
            world.get_location(puzzle.location_name).place_locked_item(world.create_item(data.GOURD_ITEM_NAME))
            gourds -= 1
    pool: list[Item] = [world.create_item(data.GOURD_ITEM_NAME) for _ in range(gourds)]

    # S1 `pack_sanity: vanilla`: each pack's check holds the pack itself (the mod leaves it in the player's hands), and
    # the pool holds no other.
    if packs_give_themselves(world):
        for pickup in data.PICKUPS:
            world.get_location(pickup.location_name).place_locked_item(world.create_item(pack_item_name(pickup)))

    for tower in world.towers:
        if tower.item_name == data.MAP_ROOM_ITEM_NAME and world.options.start_with_map_room_open:
            # L3: handed over at the start, like the drawbridge.
            world.push_precollected(world.create_item(tower.item_name))
            continue
        if world.options.linear_keys and not (
                tower.item_name == data.DRAWBRIDGE_ITEM_NAME and world.options.start_with_drawbridge_open):
            # L2: the key placed opens its door, as in the game: the door sits on the key's deposit.
            world.get_location(tower.location_name).place_locked_item(world.create_item(tower.item_name))
            continue
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
    # L1 `linear_towers`: a tower's key is its monument full, as in the game, or with one_deposit_box the gourd drawn
    # for it (a tower without slots keeps it in the pool).
    for tower in world.towers:
        if world.key_in_its_monument(tower):
            world.get_location(world.key_location_name(tower)).place_locked_item(
                world.create_item(data.key_item_name(tower)))
        else:
            pool.append(world.create_item(data.key_item_name(tower)))

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

    # With `tower_mode: by_item`, one copy opens each monument after the first, displacing filler.
    if world.options.tower_mode == bigwalk_options.TowerMode.option_by_item:
        # One for each monument after the first, and for the first as well when it does not start open.
        count = len(world.towers) - (1 if world.options.first_tower_open else 0)
        pool += [world.create_item(data.PROGRESSIVE_MONUMENT_ITEM_NAME) for _ in range(count)]

    # The needs the player starts with are handed over up front, like the
    # drawbridge, and never enter the pool.
    if world.options.lock_puzzle_needs:
        for need in data.PUZZLE_NEEDS:
            if need.key in world.options.start_with_puzzle_needs.value:
                world.push_precollected(world.create_item(need.item_name))
            else:
                pool.append(world.create_item(need.item_name))

    # The zones' lamps and gadgets (`zone_locks`, R12), displacing filler; those the player starts with handed over.
    for lock in world.zone_locks():
        if lock.key in world.options.start_with_zone_locks.value:
            world.push_precollected(world.create_item(lock.item_name))
        else:
            pool.append(world.create_item(lock.item_name))

    # The body's abilities, displacing filler like the rest.
    # An ability listed in `start_with_abilities` is the player's from the start: its items are handed over at once.
    for ability in abilities_in_pool(world):
        locked = data.ABILITY_LOCK_KEYS.get(ability.key) not in world.options.start_with_abilities.value
        for _ in range(ability.copies):
            if locked:
                pool.append(world.create_item(ability.item_name))
            else:
                world.push_precollected(world.create_item(ability.item_name))

    # The party (`big_party`) needs a radio: one is certain, counted with what the seed needs rather than taken
    # from the filler headroom, which the gear may have spent (the fuzzer, 62 seeds without it, 2026-10-13).
    # The filler gives more up to `island_object_limits`.
    if party_asked_for(world):
        pool.append(world.create_item(RADIO_ITEM_NAME))

    unfilled = len(world.multiworld.get_unfilled_locations(world.player))
    filler_needed = unfilled - len(pool)
    # Universal Tracker rebuilds a seed that was generated: it only needs the locations and the rules, and an error
    # here would leave the player with no tracker at all.
    if filler_needed < 0 and getattr(world.multiworld, "re_gen_passthrough", None):
        filler_needed = 0
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
        take = 0 if packs_give_themselves(world) else min(getattr(world.options, option).value, filler_needed)
        pool += [world.create_item(name) for _ in range(take)]
        filler_needed -= take
    # The flare guns, from the same headroom, as many of each as `island_object_limits` says: the
    # island's four with their checks (`flare_gun_sanity`), and the Rainbow Flare Gun always.
    limits = island_object_limits(world)
    flare_guns = [*(data.FLARE_GUN_ITEM_NAMES if world.options.flare_gun_sanity else ()), data.RAINBOW_FLARE_GUN_ITEM_NAME]
    for name in flare_guns:
        take = min(limits.get(name, 0), max(0, filler_needed))
        pool += [world.create_item(name) for _ in range(take)]
        filler_needed -= take
    pool += [world.create_filler() for _ in range(filler_needed)]

    world.multiworld.itempool += pool
