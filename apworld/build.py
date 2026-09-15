#!/usr/bin/env python3
"""
Package `bigwalk/` into `dist/bigwalk.apworld`.

An .apworld is a plain zip holding the world's package directory, so the
archive paths must all start with `bigwalk/`. Run it with any Python 3.10+;
it needs nothing installed.

    python build.py
"""

from __future__ import annotations

import argparse
import json
import zipfile
from pathlib import Path

HERE = Path(__file__).parent
PACKAGE = HERE / "bigwalk"
DEFAULT_OUTPUT = HERE / "dist" / "bigwalk.apworld"

EXCLUDED_DIRS = {"__pycache__", ".pytest_cache"}
EXCLUDED_SUFFIXES = {".pyc", ".pyo"}


def iter_files() -> list[Path]:
    files = []
    for path in sorted(PACKAGE.rglob("*")):
        if not path.is_file():
            continue
        if path.suffix in EXCLUDED_SUFFIXES:
            continue
        if EXCLUDED_DIRS & set(path.relative_to(PACKAGE).parts):
            continue
        files.append(path)
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-o", "--output", type=Path, default=DEFAULT_OUTPUT,
                        help=f"output path (default: {DEFAULT_OUTPUT.relative_to(HERE)})")
    args = parser.parse_args()

    manifest = json.loads((PACKAGE / "archipelago.json").read_text(encoding="utf-8"))
    files = iter_files()

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in files:
            archive.write(path, Path("bigwalk") / path.relative_to(PACKAGE))

    print(f"{args.output} - {manifest['game']} v{manifest['world_version']}, {len(files)} files")


if __name__ == "__main__":
    main()
