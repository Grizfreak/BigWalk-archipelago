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

    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    OUTPUT.mkdir(parents=True)

    staging = OUTPUT / "players"
    staging.mkdir()
    shutil.copy2(yaml_path, staging / yaml_path.name)

    command = generator_command(archipelago) + [
        "--player_files_path", str(staging),
        "--outputpath", str(OUTPUT),
    ]
    if seed is not None:
        command += ["--seed", str(seed)]

    run(command, cwd=archipelago)

    archives = sorted(OUTPUT.glob("*.zip"))
    if not archives:
        sys.exit("Generation reported success but produced no archive.")
    return archives[-1]


def generator_command(archipelago: Path) -> list[str]:
    if (archipelago / "Generate.py").is_file():
        return [sys.executable, str(archipelago / "Generate.py")]
    return [str(archipelago / "ArchipelagoGenerate.exe")]


def server_command(archipelago: Path) -> list[str]:
    if (archipelago / "MultiServer.py").is_file():
        return [sys.executable, str(archipelago / "MultiServer.py")]
    return [str(archipelago / "ArchipelagoServer.exe")]


def print_cheat_sheet(yaml_name: str, port: int) -> None:
    slot = "BigWalk"  # every file in tools/players uses this slot name
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
    print(f"    /send {slot} Red Funnel Key        should open the map room live")
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
    args = parser.parse_args()

    archipelago = find_archipelago(args.archipelago)
    print(f"Archipelago: {archipelago}")

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
