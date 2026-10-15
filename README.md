# Big Walk (Archipelago)

An [Archipelago](https://archipelago.gg) randomizer for *Big Walk* (House
House). Checks go out and items come in from inside the game: there is no
separate client to run while you play.

**Version 1.0.** Every path has been exercised in game (1.0's on one machine, with a second instance as the guest).
If you find something, please report it.

## Download

From the [releases](https://github.com/Grizfreak/BigWalk-archipelago/releases):

| File | Who needs it |
| --- | --- |
| `BigWalkArchipelago-<version>.zip` | every player in the session, host and guests alike |
| `bigwalk.apworld` | whoever generates the seed (Archipelago 0.6.7 or newer) |

## Getting started

1. Every player extracts the zip into the Big Walk folder and launches the
   game **through Steam**.
2. Whoever generates adds `bigwalk.apworld` to Archipelago, writes **one YAML
   per co-op group**, and hosts the room.
3. The host types the Archipelago address, the slot name and the password on
   the hosting screen, and presses Continue.

→ **[The full setup guide](SETUP.md)**

## Big Walk is co-op, and so is this

Nothing here can be played solo: both bells and every monument deposit need
two players. A whole co-op group shares one Archipelago slot, and only the
host types the Archipelago details — but every player installs the mod
([why](SETUP.md#why-the-others-still-need-the-mod-installed)).

## What gets randomized

- **Checks**: the puzzle gourds, cutting and placing the big keys, the radio
  stations, and depositing gourds into the monuments; if you like, picking up
  the island's objects, switching on its lights, and filling each monument.
- **Items**: gourds, the big keys, the doors they used to open, each station's
  music, the island's own gadgets and, if you turn it on, the parts the
  puzzles are built from (`lock_puzzle_needs`: their buttons, panels,
  speakers... are not on the map until you are sent them) and the players'
  own bodies (`lock_abilities`: jumping, sitting, the arms, throwing,
  carrying another player).
- **Goal**: the Gauntlet's bell, the chapel bell, the second ending, a
  number of deposits; and, if you like, **objectives** to do on the way, in
  any order (gourds, big keys, radio stations, towers filled, the island's
  highest point, a party on the beach, a share of the lights, objects or
  abilities), listed or drawn at random, shown on a board in the hub. The towers can open one after the other, in an order the
  seed draws, or each one on a Progressive Monument.
- **Looks**: the island's buoys and flare guns in the seed's colors, and the puzzles' nine basic tiles shown as
  drawings made by the community, those of the games in your multiworld first (Celeste's strawberry, Zelda's
  Triforce, Minecraft's creeper...).

What each goal asks, what every item does and what the options change are on
the **[game page](apworld/bigwalk/docs/en_Big%20Walk.md)** — the same one the
Archipelago website shows.

## Reporting a problem

Open an [issue](https://github.com/Grizfreak/BigWalk-archipelago/issues) and
attach `BepInEx/LogOutput.log` from the **host's** machine, and from any other
player whose screen showed the problem: in co-op, what goes wrong on a machine
is usually only visible in that machine's own log.

## For developers

The world is built with `apworld/build.py` and the mod with `tools/package-mod.ps1`
(both documented in their own folder: [`apworld/README.md`](apworld/README.md),
[`mod/README.md`](mod/README.md)).

To propose a tile of your own (an icon for your game, a scribble for the drawing panels):
[`mod/assets/tiles/README.md`](mod/assets/tiles/README.md).

## License

[MIT](LICENSE). The mod zip also bundles BepInEx and its dependencies, each
under its own license, listed in `THIRD-PARTY-NOTICES.txt` inside the zip.

The custom tiles in [`mod/assets/tiles/`](mod/assets/tiles/) are not under MIT: they are drawn by Jack5, Don
Chalant, GameFraek and Kensufox, and licensed under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Who drew which tile:
[`mod/assets/tiles/LICENSE.md`](mod/assets/tiles/LICENSE.md). Thanks to them.

## AI disclosure

Some of the code, data and documentation here was produced with an LLM. It
was not vibe-coded: it was used to speed up the slow parts — reading the
decompiled game to find hooks and function implementations, and assisting
with the mod and client code — and to keep the documentation in step with
what the code does. Everything it produced was reviewed and tested in game.
