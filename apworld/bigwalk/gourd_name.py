"""gourd_name: renames the gourds outside the world (data package, spoiler, slot_data)."""

from __future__ import annotations

import logging
from collections.abc import Iterable, Mapping
from typing import Any

from worlds.AutoWorld import data_package_checksum

from . import data

MAX_LENGTH = 24  # Same as GourdNames.MaxLength in the mod.


def clean(wanted: str, taken: Iterable[str], player_name: str) -> str:
    """Trims and cuts the name; falls back to "Gourd" if empty or already used by an item or group."""
    name = " ".join(wanted.split())
    if len(name) > MAX_LENGTH:
        logging.warning(f"Big Walk ({player_name}): gourd_name cut to {MAX_LENGTH} characters.")
        name = name[:MAX_LENGTH].rstrip()
    if not name or name.casefold() == data.GOURD_ITEM_NAME.casefold():
        return data.GOURD_ITEM_NAME
    if name.casefold() in {other.casefold() for other in taken if other != data.GOURD_ITEM_NAME}:
        logging.warning(f"Big Walk ({player_name}): gourd_name '{name}' is already taken, keeping Gourd.")
        return data.GOURD_ITEM_NAME
    return name


def shared(wanted: Mapping[str, str]) -> str:
    """The name every slot agrees on, else "Gourd"."""
    names = set(wanted.values())
    if len(names) == 1:
        return names.pop()
    if names:
        asked = ", ".join(f"{player}: '{name}'" for player, name in sorted(wanted.items()))
        logging.warning(f"Big Walk: slots disagree on gourd_name ({asked}), keeping Gourd.")
    return data.GOURD_ITEM_NAME


def renamed_package(package: Mapping[str, Any], name: str) -> dict[str, Any]:
    """Copy of the data package with the gourd renamed and a "Gourd" alias group."""
    def rename(item: str) -> str:
        return name if item == data.GOURD_ITEM_NAME else item

    groups = {group: sorted(rename(item) for item in items) for group, items in package["item_name_groups"].items()}
    groups[data.GOURD_ITEM_NAME] = [name]
    # Same key order as get_data_package_data, or the checksum differs.
    renamed: dict[str, Any] = {
        "item_name_groups": {group: groups[group] for group in sorted(groups)},
        "item_name_to_id": {rename(item): item_id for item, item_id in package["item_name_to_id"].items()},
        "location_name_groups": package["location_name_groups"],
        "location_name_to_id": package["location_name_to_id"],
    }
    renamed["checksum"] = data_package_checksum(renamed)
    return renamed
