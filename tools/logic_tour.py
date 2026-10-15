#!/usr/bin/env python3
"""
The logic tour (player, 2026-10-14): every check worth a look, in the order a run would reach it, for the mod's
teleport tour (Debug/DebugLogicTour: Ctrl + Shift + 1 next, Ctrl + Shift + 2 back, Ctrl + Shift + 0 flags the stop).

Generates a solo seed from a YAML (in-process, from the Archipelago source checkout), then writes, sphere by sphere,
each check's place in the game (the key the mod finds it by), its region and what the logic asks for it: the rules of
the entrances that lead to its region, then its own. At each stop the player checks that nothing more is needed in the
game than what the line says.

    python tools/logic_tour.py [yaml] [--seed N] [--all] [--out PATH]

By default the checks the logic was least tried on: the gadgets, the lights, the packs, the flare guns, the fireworks,
the radio stations and the towers reached. `--all` adds the puzzles, the key cuts and deposits and the monuments.
Run it from anywhere; it needs the Archipelago checkout at AP_ROOT (default F:\\Archipelago\\dev\\Archipelago).
"""

from __future__ import annotations

import argparse
import logging
import os
import re
import sys
from collections import deque
from pathlib import Path

AP_ROOT = Path(os.environ.get("AP_ROOT", r"F:\Archipelago\dev\Archipelago"))
HERE = Path(__file__).parent
DEFAULT_YAML = HERE / "players" / "tour-0-7-1.yaml"
DEFAULT_OUT = Path(r"F:\SteamLibrary\steamapps\common\Big Walk\BepInEx\logic-tour.tsv")


def build(yaml_path: Path, seed: int):
    os.chdir(AP_ROOT)
    sys.path.insert(0, str(AP_ROOT))
    logging.disable(logging.WARNING)
    import yaml
    from argparse import Namespace
    from BaseClasses import CollectionState, MultiWorld
    from worlds.AutoWorld import call_all
    from worlds.bigwalk.world import BigWalkWorld
    from Fill import distribute_items_restrictive

    settings = yaml.safe_load(yaml_path.read_text(encoding="utf-8"))
    options = settings.get("Big Walk", {})
    multiworld = MultiWorld(1)
    multiworld.game[1] = BigWalkWorld.game
    multiworld.player_name = {1: settings.get("name", "Tour")}
    multiworld.set_seed(seed)
    args = Namespace()
    for name, option in BigWalkWorld.options_dataclass.type_hints.items():
        setattr(args, name, {1: option.from_any(options.get(name, option.default))})
    multiworld.set_options(args)
    multiworld.state = CollectionState(multiworld)
    for step in ("generate_early", "create_regions", "create_items", "set_rules", "connect_entrances",
                 "generate_basic", "pre_fill"):
        call_all(multiworld, step)
    distribute_items_restrictive(multiworld)
    return multiworld


def keys_by_location(full: bool, data=None) -> dict[str, tuple[str, str]]:
    """Location name -> (kind, the mod's key for its place)."""
    if data is None:
        from worlds.bigwalk import data

    keys: dict[str, tuple[str, str]] = {}
    for obj in data.ISLAND_OBJECTS:
        keys[obj.location_name] = ("gadget", "prop:" + obj.guid)
    for light in data.LIGHTS:
        keys[light.location_name] = ("light", ("switch:" if light.switch else "prop:") + light.key)
    for pickup in data.PICKUPS:
        keys[pickup.location_name] = ("pack", "prop:" + pickup.guid)
    for pickup in data.FLARE_GUN_PICKUPS:
        keys[pickup.location_name] = ("flare gun", "prop:" + pickup.guid)
    for firework in data.FIREWORKS:
        keys[firework.location_name] = ("firework", "firework:" + firework.key)
    for station in data.RADIO_STATIONS:
        keys[station.location_name] = ("radio", "radio:" + station.system_name)
    for tower in data.TOWERS:
        keys[data.tower_reached_location_name(tower)] = ("tower", "tower:" + tower.prop_name)
    if full:
        for puzzle in data.PUZZLES:
            keys[puzzle.location_name] = ("puzzle", "puzzle:" + puzzle.prop_name)
        for tower in data.TOWERS:
            keys[tower.location_name] = ("deposit", "deposit:" + tower.prop_name)
            keys[data.monument_full_location_name(tower)] = ("monument", "monument:" + tower.prop_name)
            keys[data.monument_key_location_name(tower)] = ("monument", "monumentkey:" + tower.prop_name)
            for index in range(tower.segments):
                keys[data.cut_location_name(tower, index)] = ("cut", f"cut:{tower.prop_name}:{index}")
        for stage in data.GAUNTLET_STAGES:
            keys[stage.location_name] = ("gauntlet", f"gauntlet:{stage.index}")
    return keys


def pretty(rule) -> str:
    text = str(rule)
    text = re.sub(r"Has\(([^(),]+), count=(\d+)\)", r"\2 \1", text)
    text = re.sub(r"Has\(([^(),]+)\)", r"\1", text)
    text = re.sub(r"CanReachRegion\(([^()]+)\)", r"reach \1", text)
    for _ in range(4):
        text = re.sub(r"And\(([^()]*)\)", lambda m: " + ".join(m.group(1).split(", ")), text)
        text = re.sub(r"Or\(([^()]*)\)", lambda m: "(" + " | ".join(m.group(1).split(", ")) + ")", text)
        text = re.sub(r"HasAll\(([^()]*)\)", lambda m: " + ".join(m.group(1).split(", ")), text)
        text = re.sub(r"HasAny\(([^()]*)\)", lambda m: "(" + " | ".join(m.group(1).split(", ")) + ")", text)
    return text


def region_paths(multiworld, world) -> dict[str, list]:
    """Each region's shortest way from the start, as its entrances in order."""
    start = multiworld.get_region(world.origin_region_name, 1)
    paths = {start.name: []}
    queue = deque([start])
    while queue:
        region = queue.popleft()
        for exit_ in region.exits:
            target = exit_.connected_region
            if target is not None and target.name not in paths:
                paths[target.name] = paths[region.name] + [exit_]
                queue.append(target)
    return paths


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("yaml", nargs="?", type=Path, default=DEFAULT_YAML)
    parser.add_argument("--seed", type=int, default=7101)
    parser.add_argument("--all", action="store_true", help="the puzzles, key cuts and deposits and monuments too")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    yaml_path = args.yaml.resolve()
    out = args.out.resolve()

    multiworld = build(yaml_path, args.seed)
    world = multiworld.worlds[1]
    keys = keys_by_location(args.all)
    paths = region_paths(multiworld, world)

    rows = []
    for sphere, locations in enumerate(multiworld.get_spheres()):
        for location in sorted(locations, key=lambda l: (l.parent_region.name, l.name)):
            if location.player != 1 or location.address is None or location.name not in keys:
                continue
            kind, key = keys[location.name]
            needs = [world.exported_rule(entrance.access_rule) for entrance in paths.get(location.parent_region.name, [])]
            needs.append(world.exported_rule(location.access_rule))
            needs_text = "Needs: " + (" + ".join(pretty(rule) for rule in needs if rule is not None) or "nothing")
            rows.append((sphere, kind, key, location.name, location.parent_region.name, needs_text))

    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(f"# logic tour, {yaml_path.name}, seed {args.seed}, {len(rows)} stops\n")
        handle.write("sphere\tkind\tkey\tlocation\tregion\tneeds\n")
        for row in rows:
            handle.write("\t".join(str(value) for value in row) + "\n")
    print(f"{len(rows)} stops in {max((r[0] for r in rows), default=0) + 1} spheres -> {out}")


if __name__ == "__main__":
    main()
