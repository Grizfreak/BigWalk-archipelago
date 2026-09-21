"""Item definitions and pool construction for the Big Walk world."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification

from . import data

if TYPE_CHECKING:
    from .world import BigWalkWorld


# Item ids mirror the game's own enum values wherever one exists, so the mod
# can derive them arithmetically instead of duplicating a table. See data.py.
ITEM_NAME_TO_ID: dict[str, int] = {
    data.GOURD_ITEM_NAME: data.BASE_ID + 1,
    **{tower.item_name: data.feature_item_id(tower) for tower in data.TOWERS},
    **{data.key_item_name(tower): data.key_item_id(tower) for tower in data.TOWERS},
    **{station.item_name: data.radio_item_id(station) for station in data.RADIO_STATIONS},
    **{name: data.BASE_ID + offset for name, offset in data.FILLER_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.TRAP_ITEMS},
}

FILLER_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.FILLER_ITEMS)
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.TRAP_ITEMS)

RADIO_ITEM_NAMES: tuple[str, ...] = tuple(station.item_name for station in data.RADIO_STATIONS)

# Two groups per tower, and they are genuinely different things. A Feature
# is the door — the chairlift, the train, the map room — and opens on
# receipt. A Big Key is the physical key: it arrives and spawns like a
# gourd, and what it is FOR is being cut five times and placed once, which
# is six checks. Receiving a key opens nothing by itself.
ITEM_NAME_GROUPS: dict[str, set[str]] = {
    "Features": {tower.item_name for tower in data.TOWERS},
    "Big Keys": {data.key_item_name(tower) for tower in data.TOWERS},
    "Radio Music": set(RADIO_ITEM_NAMES),
    "Filler": set(FILLER_ITEM_NAMES),
    "Traps": set(TRAP_ITEM_NAMES),
}


class BigWalkItem(Item):
    game = "Big Walk"


def classification_for(name: str) -> ItemClassification:
    if name == data.GOURD_ITEM_NAME:
        # Gourds are the only currency that fills monuments, and monuments are
        # what release the big keys, so every single one is progression.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Features"]:
        # Every one of them opens part of the island, and three of them —
        # the chairlift, the tunnels and the dam — gate regions outright.
        return ItemClassification.progression
    if name in ITEM_NAME_GROUPS["Big Keys"]:
        # Progression because each one unlocks six locations of its own —
        # five cuts and a placement — and nothing else in the pool can.
        return ItemClassification.progression
    if name in TRAP_ITEM_NAMES:
        return ItemClassification.trap
    # A Radio Music item starts a piece of music playing and nothing else: no
    # rule in this world or any other can ever require one, so it is filler
    # that happens to do something, not a useful item.
    return ItemClassification.filler


def create_item(world: BigWalkWorld, name: str) -> BigWalkItem:
    return BigWalkItem(name, classification_for(name), ITEM_NAME_TO_ID[name], world.player)


def get_random_filler_item_name(world: BigWalkWorld) -> str:
    if world.random.randint(1, 100) <= world.options.trap_fill_percentage:
        return world.random.choice(TRAP_ITEM_NAMES)
    return world.random.choice(FILLER_ITEM_NAMES)


def create_all_items(world: BigWalkWorld) -> None:
    pool: list[Item] = [world.create_item(data.GOURD_ITEM_NAME) for _ in range(world.gourd_count)]

    for tower in world.towers:
        if tower.item_name == data.DRAWBRIDGE_ITEM_NAME and world.options.start_with_tutorial_key:
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
    if world.options.radio_station_items:
        pool += [world.create_item(station.item_name) for station in data.RADIO_STATIONS]

    unfilled = len(world.multiworld.get_unfilled_locations(world.player))
    pool += [world.create_filler() for _ in range(unfilled - len(pool))]

    world.multiworld.itempool += pool
