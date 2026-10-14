#!/usr/bin/env python3
"""
The shortest way through a run (player, 2026-10-14): the checks a seed's goal needs, sphere by sphere, for the mod's
teleport tour (Debug/DebugLogicTour: Ctrl + Shift + 1 next, Ctrl + Shift + 2 back, Ctrl + Shift + 0 flags the stop).

Reads the Playthrough of the seed's spoiler (Archipelago's own minimal path: the progression the goal cannot do
without), in the AP_*.zip the generator wrote or the _Spoiler.txt itself. At each stop the overlay says what the check
gives. A check in another player's world is a stop with no place, to wait for.

    python tools/logic_path.py <AP_*.zip | *_Spoiler.txt> [--player NAME] [--out PATH]

`--player`: the Big Walk slot to follow when the seed has several (the first one otherwise). Needs nothing installed:
it reads the world's data.py straight from apworld/bigwalk.
"""

from __future__ import annotations

import argparse
import importlib.util
import re
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).parent
DATA = HERE.parent / "apworld" / "bigwalk" / "data.py"
DEFAULT_OUT = Path(r"F:\SteamLibrary\steamapps\common\Big Walk\BepInEx\logic-tour.tsv")


def load_data():
    spec = importlib.util.spec_from_file_location("bigwalk_data", DATA)
    module = importlib.util.module_from_spec(spec)
    sys.modules["bigwalk_data"] = module
    spec.loader.exec_module(module)
    return module


def spoiler_text(path: Path) -> str:
    if path.suffix == ".zip":
        with zipfile.ZipFile(path) as archive:
            name = next(n for n in archive.namelist() if n.endswith("_Spoiler.txt"))
            return archive.read(name).decode("utf-8-sig")
    return path.read_text(encoding="utf-8-sig")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("seed", type=Path)
    parser.add_argument("--player")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()

    sys.path.insert(0, str(HERE))
    from logic_tour import keys_by_location

    text = spoiler_text(args.seed)
    # Several players: each one's name and game, and " (name)" after every location and item of the Playthrough.
    names = [name.strip() for name in re.findall(r"^Player \d+: (.+)$", text, re.M)]
    games = [game.strip() for game in re.findall(r"^Game:\s+(.+)$", text, re.M)]
    multi = len(names) > 1
    me = args.player or next((n for n, g in zip(names, games) if g == "Big Walk"), names[0] if names else None)

    def split(part: str) -> tuple[str, str | None]:
        if multi:
            for name in names:
                if part.endswith(f" ({name})"):
                    return part[: -len(name) - 3], name
        return part, me

    data = load_data()
    keys = keys_by_location(True, data)
    section = text.split("\nPlaythrough:", 1)[1].split("\nPaths:", 1)[0]
    rows = []
    sphere = 0
    for line in section.splitlines():
        start = re.match(r"^(\d+): \{", line)
        if start:
            sphere = int(start.group(1))
            continue
        line = line.strip()
        if not line or line == "}":
            continue
        if ": " not in line:
            # Sphere 0: what the player starts with.
            if sphere == 0:
                rows.append((0, "start", "none:", "Start", "", f"Start with: {split(line)[0]}"))
            continue
        # A location's name may hold ": " (the Gauntlet's stages): the item is after the last one.
        where, what = line.rsplit(": ", 1)
        location, owner = split(where)
        item, receiver = split(what)
        if location == "Victory":
            # The Gauntlet's bell is at its top, the finale past the seventh stage (Level7); the other goals have no place the mod
            # knows.
            goal = re.search(r"^Goal:\s+(.+)$", text, re.M)
            goal = goal.group(1).strip() if goal else "the goal"
            key = "gauntlet:7" if goal.lower().replace("_", " ") in ("big goodbye", "gauntlet") else "none:"
            if key != "none:":
                # The bell waits for all seven stages solved (player, 2026-10-14), which the Playthrough does not list
                # (the stages are checks the goal needs in the game, not progression).
                for stage in data.GAUNTLET_STAGES:
                    rows.append((sphere, "gauntlet", f"gauntlet:{stage.index}", stage.location_name, "",
                                 "Solve it (Alt+7 solves the stage you stand in): the bell waits for all seven"))
            # A sphere of its own, so the tour (nearest first within a sphere) keeps it after the stages.
            rows.append((sphere + 1 if key != "none:" else sphere, "goal", key, "The goal", "", f"Done: {goal}"))
            continue
        gives = f"Gives: {item}" + (f" (to {receiver})" if multi and receiver != me else "")
        if owner != me:
            rows.append((sphere, "elsewhere", "none:", f"{location}", f"in {owner}'s world", gives))
            continue
        kind, key = keys.get(location, ("other", "none:"))
        rows.append((sphere, kind, key, location, "", gives))

    out = args.out.resolve()
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(f"# logic path, {args.seed.name}, player {me or 'solo'}, {len(rows)} stops\n")
        handle.write("sphere\tkind\tkey\tlocation\tregion\tneeds\n")
        for row in rows:
            handle.write("\t".join(str(value) for value in row) + "\n")
    unplaced = sum(1 for row in rows if row[2] == "none:" and row[1] not in ("goal", "elsewhere", "start"))
    print(f"{len(rows)} stops in {sphere + 1} spheres for {me or 'the one player'} -> {out}"
          + (f" ({unplaced} the mod cannot place)" if unplaced else ""))


if __name__ == "__main__":
    main()
