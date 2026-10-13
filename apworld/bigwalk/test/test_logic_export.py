"""
The logic as it travels to the mod (R7, `tracker_board`): evaluated again here the way the mod evaluates it
(Core/ApLogic.cs, the same sweep), it must agree with Archipelago's own logic on what can be reached.
"""

import json
from collections import Counter
from typing import Any

from BaseClasses import CollectionState, ItemClassification

from .bases import BigWalkTestBase


def reachable(logic: dict[str, Any], received: Counter) -> set[str]:
    """The mod's sweep: regions and locations reachable with `received`, events collected as they are reached."""
    rules = logic["rules"]
    have = Counter(received)
    regions: set[str] = {logic["origin"]}
    reached: set[str] = set()
    location_region = {entry[0]: entry[2] for entry in logic["locations"]}
    location_rule = {entry[0]: entry[3] for entry in logic["locations"]}

    def holds(rule: list[Any]) -> bool:
        kind = rule[0]
        if kind == "true":
            return True
        if kind == "false":
            return False
        if kind == "and":
            return all(holds(child) for child in rule[1])
        if kind == "or":
            return any(holds(child) for child in rule[1])
        if kind == "has":
            return have[rule[1]] >= rule[2]
        if kind == "all":
            return all(have[name] >= 1 for name in rule[1])
        if kind == "any":
            return any(have[name] >= 1 for name in rule[1])
        if kind == "from":
            return sum(have[name] for name in rule[1]) >= rule[2]
        if kind == "loc":
            return location_region[rule[1]] in regions and holds(rules[location_rule[rule[1]]])
        if kind == "reg":
            return rule[1] in regions
        raise AssertionError(kind)

    changed = True
    while changed:
        changed = False
        for source, target, rule in logic["exits"]:
            if source in regions and target not in regions and holds(rules[rule]):
                regions.add(target)
                changed = True
        for name, _, region, rule, event_item in logic["locations"]:
            if name in reached or region not in regions or not holds(rules[rule]):
                continue
            reached.add(name)
            changed = True
            if event_item:
                have[event_item] += 1
    return reached


class LogicExportChecks:
    """Mixed into a test case with `tracker_board` on (not a test case itself: it has no options)."""

    run_default_tests = False

    def compare(self, received_items: list) -> None:
        logic = json.loads(json.dumps(self.world.fill_slot_data()["logic"]))
        state = CollectionState(self.multiworld)
        for item in received_items:
            state.collect(item, prevent_sweep=True)
        events = [location for location in self.multiworld.get_locations(self.player) if location.address is None]
        state.sweep_for_advancements(events)

        theirs = {location.name for location in self.multiworld.get_locations(self.player)
                  if location.address is not None and location.can_reach(state)}
        ours = {name for name in reachable(logic, Counter(item.name for item in received_items))
                if self.multiworld.get_location(name, self.player).address is not None}
        self.assertEqual(theirs, ours)

    def test_agrees_with_archipelago(self) -> None:
        progression = [item for item in self.multiworld.itempool
                       if item.player == self.player and item.classification & ItemClassification.progression]
        self.compare([])
        self.compare(progression)
        for _ in range(12):
            self.compare(self.multiworld.random.sample(progression, self.multiworld.random.randint(0, len(progression))))


class TestOffByDefault(BigWalkTestBase):
    run_default_tests = False

    def test_no_logic_without_the_board(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertFalse(slot_data["tracker_board"])
        self.assertNotIn("logic", slot_data)


class TestDefaults(LogicExportChecks, BigWalkTestBase):
    options = {"tracker_board": True}


class TestEverythingLocked(LogicExportChecks, BigWalkTestBase):
    options = {
        "tracker_board": True,
        "lock_abilities": "handed",
        "lock_running": True,
        "object_sanity": "every",
        "light_sanity": "lamps_and_switches",
        "objectives": ["big_objects", "big_lights", "big_body", "big_towers"],
        "tower_mode": "by_item",
        "gauntlet_mode": "locked_stages",
        "start_with_arch_doors_open": [],
    }


class TestHintItemNames(BigWalkTestBase):
    options = {"start_with_drawbridge_open": True}
    run_default_tests = False

    def test_placed_items_only(self) -> None:
        counts = self.world.fill_slot_data()["hint_items"]
        self.assertNotIn("Drawbridge", counts)
        self.assertEqual(counts["Map Room"], 1)
        self.assertEqual(counts["Gourd"], len([item for item in self.multiworld.itempool if item.name == "Gourd"]))
