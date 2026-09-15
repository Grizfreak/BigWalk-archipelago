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

**Everyone playing installs it, not just the host.** See "Playing together"
below for why.

1. Install BepInEx into Big Walk's folder and launch the game once through
   Steam so BepInEx generates its interop assemblies. Launching the executable
   directly does not always load BepInEx.
2. Copy `BigWalkArchipelago.dll`, `Archipelago.MultiClient.Net.dll` and
   `Newtonsoft.Json.dll` into
   `<game folder>/BepInEx/plugins/BigWalkArchipelago/`. All three are
   required; leaving one out stops the mod loading at all.
3. Launch the game through Steam again.

## Joining a multiworld

Only the host of the co-op session connects to Archipelago — see "Playing
together" below for how a group shares one slot.

1. From the main menu, start hosting a game.
2. On the hosting screen, the mod adds and relabels three fields:
   - **Slot name** — your player name in the multiworld.
   - **Archipelago password** — the room's password, or leave it empty.
   - **host:port** — the Archipelago server, for example
     `archipelago.gg:38281`. It is prefilled with `archipelago.gg:` the first
     time and remembers what you typed afterwards.
3. Continue. Your co-op partners join your session the usual way.

## Playing together

Big Walk is co-op, and **a whole co-op group shares one Archipelago slot**.
That is not a compromise, it is the only shape that makes sense: you share
one world and one save, so you share one set of checks. It also suits the
game, since depositing a gourd in a monument and breaking either bell both
need two players acting at once — a Big Walk slot is a two-person slot by
nature.

**You cannot play solo.** Every goal depends on one of those two-player
actions.

**Two people cannot take two separate slots in the same game either.** They
would need two separate sessions, and then neither could deposit or ring
anything.

### Who does what

- **The host** fills in the three fields on the hosting screen and is the
  only one who talks to Archipelago. Their save owns everything: the checks,
  the items received, the filled monuments.
- **Everyone else** just joins the session the normal way. No address, no
  slot name, no password — the mod stays dormant for them on purpose.

### Why the others still need the mod installed

Most of what the mod does is host-authoritative and reaches everyone by
itself: received gourds appear as real networked objects, and keys, doors
and unlocked shortcuts are replicated like any other game state.

Two things are not. They are local display decisions that Big Walk never
sends over the network, so a player without the mod simply does not get
them:

- the postgame "purple" gourds revealed on the map — they stay invisible on
  their map;
- the sphere blocking the way to the true ending — it stays solid **for
  them**, so they walk into it while the host walks through.

None of this has been tried with a real second player yet. The host-only
model is enforced everywhere in the mod, but a genuine co-op session has not
been tested — if something looks wrong on the joining player's side, that is
worth reporting rather than working around.

### Always keep the same host

The save lives on the host's machine, and with it the checks, the received
items and the monuments you have filled. If someone else hosts later, that
save has none of it: the mod would reconnect to the same slot with a blank
save, replay the whole item history, and the deposits you had already made
would be gone. Pick a host at the start and stay with them.

## If you get stuck somewhere unreachable

Start a new save and reconnect to the same slot. The server, not the save file,
owns the list of items you have received, so reconnecting replays all of them.
The one thing that does not come back automatically is gourds you had already
deposited: those reappear at the hub and you have to walk them back to a
monument. Since deposits are counted globally, it does not matter which one.
