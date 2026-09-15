"""How the Big Walk world presents itself on the Archipelago website."""

from __future__ import annotations

from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld

from .options import option_groups, option_presets


class BigWalkWebWorld(WebWorld):
    game = "Big Walk"
    theme = "grass"

    setup_en = Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up Big Walk for Archipelago multiworld.",
        "English",
        "setup_en.md",
        "setup/en",
        ["Grizfreak"],
    )

    tutorials = [setup_en]

    option_groups = option_groups
    options_presets = option_presets
