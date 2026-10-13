"""gourd_name: renames the gourds outside the world (data package, spoiler, slot_data)."""

from __future__ import annotations

import logging
import unicodedata
from collections.abc import Iterable, Mapping
from typing import Any

from worlds.AutoWorld import data_package_checksum

from . import data

MAX_LENGTH = 24  # Same as GourdNames.MaxLength in the mod.


# What a name may hold: letters, marks and digits of any script, spaces, and these. Everything else is dropped:
# control and invisible characters, emoji, and the characters that mean something to a text engine (the
# board in the hub is TextMeshPro, where `<b>` is a tag) or to a key, a path or a format string.
ALLOWED_PUNCTUATION = frozenset("'-_.,!?&+:;()@")


def carton_name(name: str) -> str:
    """The carton follows the gourds (a player, 2026-10-14): "Plumbus" gives "Plumbus Carton"."""
    return f"{name} Carton"


def carrier_group(name: str) -> str:
    """An alias group for the carton, the word players use for it: "Plumbus Carrier"."""
    return f"{name} Carrier"


def keep(character: str) -> bool:
    return (character == " " or character in ALLOWED_PUNCTUATION
            or unicodedata.category(character)[0] in ("L", "M", "N"))


def filtered(wanted: str) -> str:
    """
    The name without what it may not hold; every kind of white space is one space first. Decorated letters (𝕱𝖗𝖆𝖐𝖙𝖚𝖗,
    Ⓐⓑⓒ, full-width) become plain ones first (NFKC), which the game's fonts can draw.
    """
    wanted = unicodedata.normalize("NFKC", wanted)
    return "".join(character for character in " ".join(wanted.split()) if keep(character))


def clean(wanted: str, taken: Iterable[str], player_name: str) -> str:
    """Trims and cuts the name; falls back to "Gourd" if empty or already used by an item or group."""
    name = filtered(wanted)
    if name != " ".join(wanted.split()):
        logging.warning(f"Big Walk ({player_name}): gourd_name lost the characters it may not hold.")
    name = " ".join(name.split())
    if len(name) > MAX_LENGTH:
        logging.warning(f"Big Walk ({player_name}): gourd_name cut to {MAX_LENGTH} characters.")
        name = name[:MAX_LENGTH].rstrip()
    if not any(unicodedata.category(character)[0] in ("L", "M") for character in name):
        # No letter (empty, or only digits and signs, like `{0}` once the braces are gone): the plain word.
        if name:
            logging.warning(f"Big Walk ({player_name}): gourd_name has no letter, keeping Gourd.")
        return data.GOURD_ITEM_NAME
    if name.casefold() == data.GOURD_ITEM_NAME.casefold():
        return data.GOURD_ITEM_NAME
    # "Victory" is the goal's event: an item of that name would read as the goal in a spoiler or a client.
    # The carton's name and its alias group come with it, and must be free as well.
    wanted_names = {name.casefold(), carton_name(name).casefold(), carrier_group(name).casefold()}
    if wanted_names & ({other.casefold() for other in taken if other != data.GOURD_ITEM_NAME} | {"victory"}):
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
    """
    Copy of the data package with the gourd and the carton renamed, and alias groups: "Gourd", "Gourd Carton" and
    "<name> Carrier" (the static "Gourd Carrier" group follows the carton by itself).
    """
    renames = {data.GOURD_ITEM_NAME: name, data.GOURD_CARTON_ITEM_NAME: carton_name(name)}

    def rename(item: str) -> str:
        return renames.get(item, item)

    groups = {group: sorted(rename(item) for item in items) for group, items in package["item_name_groups"].items()}
    groups[data.GOURD_ITEM_NAME] = [name]
    groups[data.GOURD_CARTON_ITEM_NAME] = [carton_name(name)]
    groups[carrier_group(name)] = [carton_name(name)]
    # Same key order as get_data_package_data, or the checksum differs.
    renamed: dict[str, Any] = {
        "item_name_groups": {group: groups[group] for group in sorted(groups)},
        "item_name_to_id": {rename(item): item_id for item, item_id in package["item_name_to_id"].items()},
        "location_name_groups": package["location_name_groups"],
        "location_name_to_id": package["location_name_to_id"],
    }
    renamed["checksum"] = data_package_checksum(renamed)
    return renamed
