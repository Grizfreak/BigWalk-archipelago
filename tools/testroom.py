#!/usr/bin/env python3
"""
One command to get a Big Walk room running for an in-game test.

Rebuilds the apworld, installs it into an Archipelago setup, generates a
seed from tools/players/, and hosts it — so iterating on the mod does not
mean redoing the Generate/Server dance by hand every time.

    python tools/testroom.py                        # solo-smoke.yaml, port 38281
    python tools/testroom.py --yaml solo-full.yaml
    python tools/testroom.py --no-host              # generate only

Stdlib only, and it finds Archipelago by itself (see find_archipelago).
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
PLAYERS = REPO / "tools" / "players"
OUTPUT = REPO / "tools" / "out"
APWORLD = REPO / "apworld" / "dist" / "bigwalk.apworld"

DEFAULT_PORT = 38281


def find_archipelago(explicit: str | None) -> Path:
    """
    Locate an Archipelago setup: either a source checkout (Generate.py) or an
    installed build (ArchipelagoGenerate.exe). Both work.

    Looked for, in order: --archipelago, then a folder named Archipelago
    beside this repo, then one beside its parent — which covers the usual
    layout of a dev checkout living inside an Archipelago install.
    """
    candidates = []
    if explicit:
        candidates.append(Path(explicit))
    candidates += [REPO.parent / "Archipelago", REPO.parent.parent / "Archipelago"]

    for candidate in candidates:
        if (candidate / "Generate.py").is_file() or (candidate / "ArchipelagoGenerate.exe").is_file():
            return candidate

    sys.exit(
        "Could not find Archipelago. Pass --archipelago <path> to a source checkout "
        "or an installed Archipelago folder."
    )


def run(command: list[str], cwd: Path) -> None:
    print(f"$ {' '.join(str(part) for part in command)}")
    result = subprocess.run(command, cwd=cwd)
    if result.returncode != 0:
        sys.exit(f"Command failed with exit code {result.returncode}.")


def build_and_install_apworld(archipelago: Path) -> None:
    run([sys.executable, str(REPO / "apworld" / "build.py")], cwd=REPO)

    # A dev checkout may already expose the world as a live folder under
    # worlds/ (a junction or symlink back to apworld/bigwalk). Installing the
    # packaged copy on top of that registers the same game twice and
    # Archipelago silently picks one — which is how you end up testing the
    # previous build without noticing. The live folder wins, since it is the
    # one that reflects what is on disk right now.
    linked = archipelago / "worlds" / "bigwalk"
    if linked.exists():
        print(f"using the linked world at {linked} (skipping the packaged copy)")
        return

    custom_worlds = archipelago / "custom_worlds"
    custom_worlds.mkdir(exist_ok=True)
    shutil.copy2(APWORLD, custom_worlds / APWORLD.name)
    print(f"installed {APWORLD.name} -> {custom_worlds}")


def generate(archipelago: Path, yaml_name: str, seed: int | None) -> Path:
    # One YAML per run rather than the whole folder: these files are
    # alternative setups for the same single slot, not a multiworld.
    yaml_path = PLAYERS / yaml_name
    if not yaml_path.is_file():
        sys.exit(f"No such player file: {yaml_path}")

    # Deliberately does NOT wipe tools/out. Next to each seed lives its room
    # state (AP_<seed>.apsave: what the server has already sent and
    # received), and deleting that silently destroys the continuity of an
    # ongoing test — which is exactly what you need to reconnect a save and
    # watch the replay behave. Only the staging folder is rebuilt.
    OUTPUT.mkdir(parents=True, exist_ok=True)

    staging = OUTPUT / "players"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir()
    shutil.copy2(yaml_path, staging / yaml_path.name)

    command = generator_command(archipelago) + [
        "--player_files_path", str(staging),
        "--outputpath", str(OUTPUT),
    ]
    if seed is not None:
        command += ["--seed", str(seed)]

    # Which zip is the one just produced: the one that was not there
    # before. Sorting the folder and taking the last entry looked
    # equivalent and is not — the names carry a random seed, so their
    # alphabetical order says nothing about age. On 2026-09-15 that quietly
    # hosted a three-hour-old seed, complete with its .apsave and the items
    # already sent in it, while announcing the new one on screen.
    before = {path.name for path in OUTPUT.glob("AP_*.zip")}
    run(command, cwd=archipelago)

    produced = [path for path in OUTPUT.glob("AP_*.zip") if path.name not in before]
    if not produced:
        sys.exit("Generation reported success but produced no new archive.")
    if len(produced) > 1:
        # Never expected with one YAML, but picking blind here would bring
        # back exactly the bug above.
        sys.exit("Generation produced several archives: " + ", ".join(p.name for p in produced))
    return produced[0]


def generator_command(archipelago: Path) -> list[str]:
    if (archipelago / "Generate.py").is_file():
        return [sys.executable, str(archipelago / "Generate.py")]
    return [str(archipelago / "ArchipelagoGenerate.exe")]


def server_command(archipelago: Path) -> list[str]:
    if (archipelago / "MultiServer.py").is_file():
        return [sys.executable, str(archipelago / "MultiServer.py")]
    return [str(archipelago / "ArchipelagoServer.exe")]


def read_slot_name(yaml_name: str) -> str:
    # The slot name is the YAML's `name:`, and the in-game save must match it
    # exactly. Read rather than assumed: the files in tools/players no longer
    # all share one slot name, and printing the wrong one sends you to type a
    # save name the server will reject.
    for line in (PLAYERS / yaml_name).read_text(encoding="utf-8").splitlines():
        if line.startswith("name:"):
            return line.split(":", 1)[1].strip()
    sys.exit(f"No `name:` found in {yaml_name}; cannot tell what to call the save.")


def print_cheat_sheet(yaml_name: str, port: int) -> None:
    slot = read_slot_name(yaml_name)
    print()
    print("=" * 70)
    print("In the game's hosting screen:")
    print(f"    SLOT NAME              {slot}   <- the save must have this exact name")
    print("    ARCHIPELAGO PASSWORD   (leave empty)")
    print(f"    HOST ARCHIPELAGO       127.0.0.1:{port}")
    print()
    print("Expected in BepInEx/LogOutput.log:")
    print(f"    [ApRuntime] Connecting to 127.0.0.1:{port} as '{slot}'...")
    print("    [ApRuntime] Connected. apworld v0.1.0, goal=...")
    print()
    print("Useful commands in this server console:")
    print(f"    /send {slot} Gourd                 spawn a gourd at the hub")
    # The door and the key are two items since 2026-09-21, and the console is
    # where the difference is easiest to see: one opens something, the other
    # is six checks you carry.
    print(f"    /send {slot} Map Room              the door: opens live")
    print(f"    /send {slot} Map Room Key          the key: opens nothing, carries checks")
    print(f"    /send_location {slot} Cabin Fever  mark a check server-side")
    print("    /players                           who is connected")
    print()
    print("The test that matters most: quit the game, relaunch, reconnect —")
    print("no gourd may be spawned twice. That is the item cursor working.")
    print(f"({yaml_name}; Ctrl+C here stops the room.)")
    print("=" * 70)
    print()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--yaml", default="solo-smoke.yaml",
                        help="player file in tools/players (default: solo-smoke.yaml)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--archipelago", default=None,
                        help="path to an Archipelago checkout or install")
    parser.add_argument("--no-host", action="store_true", help="generate but do not host")
    parser.add_argument("--resume", action="store_true",
                        help="re-host the most recent seed instead of generating a new one, "
                             "keeping the room's history so a save can reconnect to it")
    args = parser.parse_args()

    archipelago = find_archipelago(args.archipelago)
    print(f"Archipelago: {archipelago}")

    if args.resume:
        # Reconnecting a save only works against the room it was bound to:
        # the cursor is keyed on (seed, slot), and the item history lives in
        # that room's .apsave, not in the seed. A new seed is a new world.
        existing = sorted(OUTPUT.glob("AP_*.zip"), key=lambda p: p.stat().st_mtime)
        if not existing:
            sys.exit(f"Nothing to resume: no seed in {OUTPUT}.")
        archive = existing[-1]
        print(f"resuming: {archive}")
    else:
        build_and_install_apworld(archipelago)
        archive = generate(archipelago, args.yaml, args.seed)
        print(f"seed: {archive}")

    if args.no_host:
        return

    print_cheat_sheet(args.yaml, args.port)

    try:
        subprocess.run(
            server_command(archipelago) + [str(archive), "--port", str(args.port)],
            cwd=archipelago,
        )
    except KeyboardInterrupt:
        print("\nRoom stopped.")


if __name__ == "__main__":
    main()
