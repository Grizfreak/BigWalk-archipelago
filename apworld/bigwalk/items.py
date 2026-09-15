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
    **{tower.item_name: data.key_item_id(tower) for tower in data.TOWERS},
    **{name: data.BASE_ID + offset for name, offset in data.FILLER_ITEMS},
    **{name: data.BASE_ID + offset for name, offset in data.TRAP_ITEMS},
}

FILLER_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.FILLER_ITEMS)
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(name for name, _ in data.TRAP_ITEMS)

ITEM_NAME_GROUPS: dict[str, set[str]] = {
    "Big Keys": {tower.item_name for tower in data.TOWERS},
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
    if name in ITEM_NAME_GROUPS["Big Keys"]:
        return ItemClassification.progression
    if name in TRAP_ITEM_NAMES:
        return ItemClassification.trap
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
        if tower.item_name == data.TUTORIAL_KEY_ITEM_NAME and world.options.start_with_tutorial_key:
            # Handed over up front rather than shuffled, so it never enters the
            # pool. Its deposit location is checked the moment the mod applies
            # it (see rules.py), which is why this does not cost a check.
            world.push_precollected(world.create_item(tower.item_name))
            continue
        pool.append(world.create_item(tower.item_name))

    unfilled = len(world.multiworld.get_unfilled_locations(world.player))
    pool += [world.create_filler() for _ in range(unfilled - len(pool))]

    world.multiworld.itempool += pool
