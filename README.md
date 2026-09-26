# Big Walk (Archipelago)

An [Archipelago](https://archipelago.gg) randomizer for *Big Walk* (House
House). Checks go out and items come in from inside the game: there is no
separate client to run while you play.

**Version 0.1.1 — alpha.** Every path has been exercised in game, on two
machines as well as one, but no one has yet played a seed from the first check
to the goal. Expect to find things, and please report them.

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
  stations, and depositing gourds into the monuments.
- **Items**: gourds, the big keys, the doors they used to open, each station's
  music, and the island's own gadgets.
- **Goal**: the Gauntlet's bell, the chapel bell, the second ending, or a
  number of deposits.

What each goal asks, what every item does and what the options change are on
the **[game page](apworld/bigwalk/docs/en_Big%20Walk.md)** — the same one the
Archipelago website shows.

## Reporting a problem

Open an [issue](https://github.com/Grizfreak/BigWalk-archipelago/issues) and
attach `BepInEx/LogOutput.log` from the **host's** machine, and from any other
player whose screen showed the problem: in co-op, what goes wrong on a machine
is usually only visible in that machine's own log.

## For developers

Building both halves, testing, and how they fit together:
[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License

[MIT](LICENSE). The mod zip also bundles BepInEx and its dependencies, each
under its own license, listed in `THIRD-PARTY-NOTICES.txt` inside the zip.

## AI disclosure

Some of the code, data and documentation here was produced with an LLM. It
was not vibe-coded: it was used to speed up the slow parts — reading the
decompiled game to find hooks and function implementations, and assisting
with the mod and client code — and to keep the documentation in step with
what the code does. Everything it produced was reviewed and tested in game.
