"""
The world's logic, written into slot_data for the mod (R7, `tracker_board`, player 2026-10-13): a board in
the hub names the checks in logic, so the mod has to know the logic. Every rule of this world is built with the
rule builder, which is data, so it travels as data and the mod evaluates it again with the items it has received.

The format (`slot_data["logic"]`, documented in ../protocol.md):

    "origin":    the region the players start in
    "rules":     every distinct rule once, as nested lists:
                   ["true"] ["false"] ["and", [r, ...]] ["or", [r, ...]] ["has", item, count]
                   ["all", [items]] ["any", [items]] ["from", [items], count]
                   ["loc", location] ["reg", region]
    "exits":     [from region, to region, rule index] for every entrance
    "locations": [name, id (0 for an event), region, rule index, event item ("" for a real check)]

Items and locations go by name: the rules name them, and the mod reads names off the data package.
"""

from __future__ import annotations

import json
from typing import TYPE_CHECKING, Any

from rule_builder.rules import (
    And, CanReachLocation, CanReachRegion, False_, Has, HasAll, HasAny, HasFromList, Or, Rule, True_,
)

if TYPE_CHECKING:
    from .world import BigWalkWorld


def compact(rule: Rule) -> list[Any]:
    """One rule as the nested lists above. A rule of another kind is an error: the mod would not know it."""
    if isinstance(rule, True_):
        return ["true"]
    if isinstance(rule, False_):
        return ["false"]
    if isinstance(rule, And):
        return ["and", [compact(child) for child in rule.children]]
    if isinstance(rule, Or):
        return ["or", [compact(child) for child in rule.children]]
    if isinstance(rule, Has):
        return ["has", rule.item_name, rule.count]
    if isinstance(rule, HasAll):
        return ["all", list(rule.item_names)]
    if isinstance(rule, HasAny):
        return ["any", list(rule.item_names)]
    if isinstance(rule, HasFromList):
        return ["from", list(rule.item_names), rule.count]
    if isinstance(rule, CanReachLocation):
        return ["loc", rule.location_name]
    if isinstance(rule, CanReachRegion):
        return ["reg", rule.region_name]
    raise TypeError(f"Big Walk: the mod cannot evaluate a {type(rule).__name__} rule")


def export(world: BigWalkWorld) -> dict[str, Any]:
    rules: list[list[Any]] = [["true"]]
    index_of: dict[str, int] = {json.dumps(rules[0]): 0}

    def rule_index(access_rule: Any) -> int:
        built = world.exported_rule(access_rule)
        if built is None:
            return 0
        entry = compact(built)
        key = json.dumps(entry)
        if key not in index_of:
            index_of[key] = len(rules)
            rules.append(entry)
        return index_of[key]

    exits: list[list[Any]] = []
    located: list[list[Any]] = []
    for region in world.multiworld.get_regions(world.player):
        for entrance in region.exits:
            if entrance.connected_region is not None:
                exits.append([region.name, entrance.connected_region.name, rule_index(entrance.access_rule)])
        for location in region.locations:
            event_item = location.item.name if location.address is None and location.item is not None else ""
            located.append([location.name, location.address or 0, region.name, rule_index(location.access_rule),
                            event_item])

    return {"origin": world.origin_region_name, "rules": rules, "exits": exits, "locations": located}
