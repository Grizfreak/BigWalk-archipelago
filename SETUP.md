# Big Walk Archipelago — Setup Guide

## What you need

- **Big Walk** on Steam (Windows). Every player needs their own copy: this is
  a co-op game and the randomizer does not change that.
- **The mod**, `BigWalkArchipelago-<version>.zip`, from this repository's
  [releases](https://github.com/Grizfreak/BigWalk-archipelago/releases). It is
  an all-in-one: BepInEx is inside it (see below), so there is nothing to
  install separately.
- **Archipelago 0.6.7 or newer**, from
  [its releases](https://github.com/ArchipelagoMW/Archipelago/releases) —
  only for whoever generates the seed.
- **The apworld**, `bigwalk.apworld`, from the same release as the mod —
  again, only for whoever generates.

There is no separate client to run while you play. The mod talks to
Archipelago from inside the game.

### Who installs what

| | The mod zip | Archipelago + `bigwalk.apworld` |
| --- | --- | --- |
| Whoever hosts the Big Walk session | yes | — |
| Everyone else in the session | yes | — |
| Whoever generates the seed | — | yes |

Those are two different jobs and they need not be the same person. What does
matter is that **every player installs the mod**, not just the host — see
[Playing together](#playing-together).

### About BepInEx

The mod is a BepInEx plugin, so the game needs BepInEx to load it — and not
just any BepInEx. It needs **BepInEx 6, the Unity IL2CPP x64 build**, and
specifically **`6.0.0-be.781`**, the build the mod is compiled and tested
against (.NET 6.0.7 runtime).

You do not have to go and get it: the release zip contains that exact build,
which is the whole reason the zip is packaged the way it is. Installing a
different one yourself is the most common way to end up with a mod that
loads nothing. BepInEx 6 is a bleeding-edge project with no stable release,
its builds are numbered rather than versioned, and they are not
interchangeable — the Mono build in particular will not work at all, Big
Walk being an IL2CPP game.

If the game folder already has a BepInEx in it from something else, replace
it with the one from the zip. The build is published at
[builds.bepinex.dev](https://builds.bepinex.dev/projects/bepinex_be) as
`BepInEx-Unity.IL2CPP-win-x64` under build 781, if you ever need it on its
own.

## Installing the mod

1. Close the game.
2. Open the game's install folder. On Steam: right-click **Big Walk** →
   **Manage** → **Browse local files**. It is the folder that contains
   `Big Walk.exe`.
3. Extract the **whole** zip in there, merging folders. Next to
   `Big Walk.exe` you should end up with `winhttp.dll`,
   `doorstop_config.ini`, `dotnet/` and `BepInEx/`.
4. Launch the game **through Steam**. Double-clicking the executable does not
   always load BepInEx.
5. The first launch is slow — a minute or two of black screen while BepInEx
   downloads the Unity libraries it needs and builds its cache, so be online
   for it. The ones after it are normal.

To check it worked, open `BepInEx/LogOutput.log` and look for a line reading
`Big Walk Archipelago v0.1.0 loaded.`

**Everyone must run the same mod version and the same game version.** A
mismatch is not always loud: an older mod ignores parts of the seed it does
not know about, which can make a game unwinnable rather than crash.

To uninstall, delete `winhttp.dll`, `doorstop_config.ini`, `dotnet/` and
`BepInEx/`. Nothing in the game's own files is ever modified.

## Installing the apworld

Drop `bigwalk.apworld` into the `custom_worlds/` folder of your Archipelago
installation. One copy, in `custom_worlds/`, and nowhere else.

## Configuring your YAML file

### What is a YAML file and why do I need one?

Your YAML holds the options the generator uses to build your game. Each
player of a multiworld provides one, so everyone can play the game they want
inside the same seed.

### Where do I get one?

Big Walk is not on the Archipelago website, so there is no hosted options
page for it. Once the apworld is installed, press **Generate Template
Options** in the Archipelago Launcher: it writes
`Players/Templates/Big Walk.yaml`, with every option and its documentation in
the comments. Copy that file into `Players/` — not `Templates/` — and edit it
there.

If `Big Walk.yaml` is missing from `Players/Templates/`, the apworld is not
where Archipelago is looking. Check `custom_worlds/` again.

The [options table in the README](README.md#options) is the short version of
what is in it.

### One YAML per co-op group

A co-op group shares one Archipelago slot, so it fills in **one** YAML
between them, with one slot name. Two people in one Big Walk session cannot
take two slots — see below.

## Generating and hosting

Put every player's `.yaml` into `Players/` and run `ArchipelagoGenerate.exe`,
or press **Generate** in the Launcher. The result lands in `output/` as a
`.zip`.

To get a room from it, either:

- run `ArchipelagoServer.exe` on that zip and host it yourself, or
- upload it to [archipelago.gg/uploads](https://archipelago.gg/uploads),
  which gives you a room and a port. The website does not need the apworld
  installed to host a game that is already generated.

Either way you end up with an address, a port and one slot name per player.

## Connecting from the game

Only the host of the Big Walk session does this.

1. From the main menu, start hosting a game.
2. The mod adds one field to the hosting screen and relabels two:
   - **SLOT NAME** — your slot name in the multiworld, exactly as it is in
     the YAML. It is also the save's own name, so it can only be typed when
     the save is created: once it exists, the field is locked, because
     renaming it would point the save at a different Archipelago slot. Switch
     the toggle off if you really do want to rename a save.
   - **ARCHIPELAGO PASSWORD** — the room's password. Leave it empty if there
     is none: the mod makes the field optional, which it is not in the
     vanilla game. On a build where it stays mandatory anyway, type anything
     — a server with no password ignores what you send.
   - **ARCHIPELAGO HOST** — the server, as `host:port`, for example
     `archipelago.gg:38281`. It is prefilled with `archipelago.gg:` the first
     time and remembers what you typed afterwards.
3. Press **TEST CONNECTION**. It logs in once, throws the session away and
   says what happened: `Connected as 'yourslot'`, or why not — an unknown
   slot name, a wrong password and a server that never answered are told
   apart rather than all reading "it failed". It never stops you continuing;
   it only tells you what you are about to find out the slow way.
4. Continue. Your partners join your session the usual way — they have
   nothing to fill in.

**AP : ON / AP : OFF**, the small button beside Continue, decides whether
this session talks to Archipelago at all. Switched off, the host field greys
out, the test button goes inert, and the two fields above go back to being
the game's own save name and session password — which is what they are when
nothing is connecting. It is the same switch as `Enabled` in the mod's config
file, put somewhere you can reach it without closing the game.

A line in the corner of the screen shows the connection for the whole
session, and says `Archipelago: off` when the switch is off — so a session
started disconnected by mistake says so instead of looking normal. If you
would rather not see it, turn off `ShowConnectionStatus` in the mod's config.

## Playing together

Big Walk is co-op, and **a whole co-op group shares one Archipelago slot**.
That is not a compromise. You share one world and one save, so you share one
set of checks — and the game agrees, since depositing a gourd in a monument
and breaking either bell both need two players acting at once.

**You cannot play solo.** Every goal depends on one of those two-player
actions.

**Two people cannot take two separate slots in the same game either.** They
would need two separate sessions, and then neither could deposit or ring
anything.

### Who does what

- **The host** fills in the three fields on the hosting screen and is the
  only one who talks to Archipelago. Their save owns everything: the checks
  sent, the items received, the filled monuments.
- **Everyone else** joins the session the normal way. No address, no slot
  name, no password — the mod stays dormant for them on purpose.

### Why the others still need the mod installed

Some of what the mod does reaches everyone by itself: doors and shortcuts
replicate like any other game state, a check is reported by the host whoever
made it, and the vanilla props removed for the filler items are removed for
everybody.

Most of what a player *sees*, though, their own copy of the mod has to build.
A player without it:

- sees none of the gourds or gadgets the multiworld sends — they exist for
  everyone else and are simply not there on that player's screen — and sees
  the big keys all alike, untinted;
- gets no Archipelago overlay and hears no radio music, even once it has been
  granted;
- does not see the purple postgame gourds on the map, and walks into the
  sphere on the way to the true ending that the host walks straight through.

A friend without the mod can still join and play — that was tried — but only
a vanilla-looking world. So everyone installs it.

The big keys have been through a two-machine session too: a guest can carry,
cut and place a key, and every one of those checks reaches the host. If
something still looks wrong on the joining player's side, that is worth
reporting rather than working around.

### Always keep the same host

The save lives on the host's machine, and with it the checks, the items
received and the monuments you have filled. If someone else hosts later, that
save has none of it: the mod reconnects to the same slot with a blank save,
replays the whole item history, and the deposits you had already made are
gone. Pick a host at the start and stay with them.

## While you are playing

The host's screen carries a line in the corner: the Archipelago connection,
for the whole session, with the last few checks sent and items received
underneath it and the resync shortcut below that. It is off for everyone
else, who never connects. `ShowConnectionStatus` turns the whole thing off,
`StatusFontSize` resizes it, and `ShowItemFeed` keeps the status line while
dropping the feed.

What it cannot show you is the rest of the multiworld — who received what you
sent, or what anyone else is doing. Keep Archipelago's **Text Client** open
beside the game for that; everything the mod does is also in
`BepInEx/LogOutput.log`.

**Ctrl+R** (host only) sweeps up everything Archipelago has given you that is
loose — gourds, big keys, filler props, including whatever is in someone's
hands — and puts back exactly what the server says you have received. It is
for when one has ended up somewhere you cannot reach it: stranded in a sealed
puzzle room, say. Deposited gourds and placed keys are never touched, so
nothing Archipelago counts can be lost by pressing it.

If the Archipelago server dies, the Big Walk session does not. The host gets
a notice on screen, and the mod reconnects.

## If you get stuck, or lose a save

Start a new save and reconnect to the same slot. The server, not the save
file, owns the list of items you have received, so reconnecting replays all
of them. The one thing that does not come back by itself is the gourds you
had already deposited: they reappear at the hub and have to be walked back to
a monument. Since deposits are counted globally, it does not matter which one.

## Mod settings

The mod writes `BepInEx/config/com.grizfreak.bigwalk.archipelago.cfg` on its
first launch. Everything in it has a comment; the ones worth knowing are in
the `[Archipelago]` section:

- `Enabled` — turn the connection off entirely. The mod's other features stay
  on and checks are only written to the log. Useful for playing vanilla-ish
  with the shortcuts open.
- `HostPort` — the address remembered from the hosting screen.
- `ShowConnectionStatus` — the whole corner display; `StatusFontSize`,
  `ShowItemFeed`, `NoticeSeconds` and `ShowResyncHint` tune what it contains.
- `SpawnGourdAtPlayer`, `PutGourdInHands`, `HandoverRadius` — where a gourd
  received mid-game ends up. The batch rebuilt at the start of a session
  always goes to the hub.
- `GourdColor`, `ColorBigKeys` — how received gourds and keys are tinted so
  they stand out from the ones sitting in puzzles.
- `ResyncGourdsKey` — the Ctrl+R shortcut above.

The `[Debug]` section is off by default and is developer tooling, not
gameplay.
