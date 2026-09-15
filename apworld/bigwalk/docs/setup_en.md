# Big Walk Setup Guide

## Required software

- Big Walk on Steam.
- [BepInEx 6 (IL2CPP, x64)](https://builds.bepinex.dev/projects/bepinex_be) —
  the Unity IL2CPP build, not the Mono one.
- The Big Walk Archipelago mod.
- Archipelago 0.6.7 or newer.

## Status

The mod connects to Archipelago on its own — there is no separate client to
run while you play. Connecting from inside the game works; a full run has
not been played end to end yet, so treat this as an alpha.

## Installing the mod

1. Install BepInEx into Big Walk's folder and launch the game once through
   Steam so BepInEx generates its interop assemblies. Launching the executable
   directly does not always load BepInEx.
2. Copy `BigWalkArchipelago.dll`, `Archipelago.MultiClient.Net.dll` and
   `Newtonsoft.Json.dll` into
   `<game folder>/BepInEx/plugins/BigWalkArchipelago/`. All three are
   required; leaving one out stops the mod loading at all.
3. Launch the game through Steam again.

## Joining a multiworld

Only the host of the co-op session connects to Archipelago. Big Walk's save
data belongs to the host, so every check and every received item goes through
them; the other players just play.

1. From the main menu, start hosting a game.
2. On the hosting screen, the mod adds and relabels three fields:
   - **Slot name** — your player name in the multiworld.
   - **Archipelago password** — the room's password, or leave it empty.
   - **host:port** — the Archipelago server, for example
     `archipelago.gg:38281`. It is prefilled with `archipelago.gg:` the first
     time and remembers what you typed afterwards.
3. Continue. Your co-op partners join your session the usual way.

## Playing solo

You cannot. Depositing a gourd into a monument and breaking either bell both
require two players acting at once, and every goal depends on one or the
other.

## If you get stuck somewhere unreachable

Start a new save and reconnect to the same slot. The server, not the save file,
owns the list of items you have received, so reconnecting replays all of them.
The one thing that does not come back automatically is gourds you had already
deposited: those reappear at the hub and you have to walk them back to a
monument. Since deposits are counted globally, it does not matter which one.
