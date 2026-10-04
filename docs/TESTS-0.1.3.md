# 0.1.3 test session

What has to pass before 0.1.3 ships. Each test says what to set up, what to do, and what to
see on screen and in `BepInEx/LogOutput.log` (the host's; the loopback guest writes
`LogOutput.1.log`). Mark each one **PASS**, **FAIL** (with what was seen) or **SKIP**.

## Setup

- Room: `python tools/testroom.py --yaml qol-0-1-3.yaml`, port 38281. One seed, three slots:

  | Slot | What it tests | Options |
  |---|---|---|
  | `BWCabin` | Cabin Fever waits and help buttons | `cabin_fever_time: reduced` (30 s), `cabin_fever_help: true`, `cabin_fever_long_time: fixed` (90 s), `cabin_fever_long_help: true`, `teleport_buttons: free` |
  | `BWTowers` | `teleport_buttons: with_towers` | defaults otherwise |
  | `BWItems` | `teleport_buttons: items` | defaults otherwise |

- For each slot, host a **new** save named exactly like the slot. Never press F11 (the game's
  unlock cheat writes every lookout light at once and spoils `with_towers`).
- Items on demand: ask Claude ("give BWItems the Red Funnel Tower Teleporter", "30 gourds to
  BWTowers"); it sends them through the room.
- Loopback guest: Ctrl+L on the host starts it and joins, Ctrl+T switches window, Ctrl+C brings
  the guest to the host. Hold a button for the other player with End (it holds the nearest one).
- Debug keys that help: Keypad 2 logs every saved value and nearby state change (on/off),
  Keypad 3 dumps every timer.

## A. Cabin Fever (`BWCabin`, with the loopback guest: both puzzles are for two players)

**A1. The shorter wait.**
Go into the Cabin Fever house with the guest and start the wait as the puzzle asks.
- Log at load or on arrival: `[CabinFeverWaits] cabin_fever: timer found (300 s in the game).`
  then `cabin_fever: timer set to 30 s.`
- The dial runs out in about **30 s** (not 5 min), and the puzzle completes: its check is sent
  (`[Check] gourdCabinFever`).
- Guest: its dial shows the same time (its log: `cabin_fever: timer set to 30 s.`).

**A2. The help button.**
Before or during the wait, find the hidden button on the wall you marked (beige plate, sunk into
the wall, no icon). Start the wait, press the button once.
- Log: `cabin_fever: help pressed, 10 s taken off, N s left.`
- The dial jumps forward by 10 s, on both screens.
- Pressed with no wait running: `help pressed, but no wait is running.`, nothing else happens.

**A3. The guest presses help.**
Same as A2, the guest presses.
- The **host's** log has the `help pressed` line (the press runs on the host). The dial jumps on
  both screens.

**A4. The long one, fixed.**
Cabin Fever Long house, both players, start the wait.
- Log: `cabin_fever_long: timer set to 90 s.` The wait lasts about **90 s** (not 30 min); the check
  `gourdCabinFeverLong` is sent.
- Its help button is there and works as in A2.

**A5. Vanilla stays vanilla.** (any other slot, e.g. `BWTowers`)
- No `timer set` line other than to the game's own 300 s / 1800 s; no help button in either house.

## B. Teleports, `with_towers` (`BWTowers`)

**B1. A fresh save.** At the hub wall of teleport buttons:
- **No** hub button. Each destination's way back to the hub is there (they always are once the
  option is not `off`), and the Resync button.

**B2. A coloured tower.** Open the Red tower's door with the button at its foot (the lookout).
- Within about 2 s the **red** button appears at the hub. Repeat for green, blue, yellow.
- Pressing it sends you in front of the tower's way back; that one sends you back to the hub.

**B3. The Black Tower.** Climb it and hold the two buttons at the top together (End on one, press
the other).
- Log: `BlackTowerInteriorDoor = 1`. The **black** button appears at the hub, and not before.
- Its door at the foot is open from the start (B6), so no monument needs filling.

**B6. The Black Tower's door, open from the start** (any slot, `open_black_tower` is on by default).
On a fresh save, go to the Black Tower.
- Log: `[BlackTowerDoor] Door found under 'BlackTower PlayerCount2'.` then `door opened.`
- The door at its foot is open; it stays open after a monument slot is filled or emptied.

**B4. The Silent Gauntlet.** Hold the chapel's two buttons together (End on one, press the other).
- Log: `EndingGate = 2`. The **Gauntlet** button appears.

**B5. Guest.** Start the guest after B2.
- The guest sees the same hub buttons as the host, and its own presses teleport it.

**B7. `teleport_back_to_hub: false`** (a slot with it off): the hub's buttons are there, no
destination has a button back.

## C. Teleports, `items` (`BWItems`)

**C1. Nothing at first.** No hub button on a fresh save.

**C2. The item and the tower, both.** Ask Claude for `Red Funnel Tower Teleporter`.
- Feed: `Received: Red Funnel Tower Teleporter`; log: `[TeleportButtons] Teleporter to Red Funnel
  Tower granted.` **No** button yet: the tower has not been reached.
- Open the Red tower's door with its lookout button: now the red button appears, and only it.
- (The other way round, a tower opened without its item, shows no button either.)

**C3. Kept.** Quit to the menu and load the save again.
- The red button is still there (the save keeps the ledger `ap_tp_red`).

## D. Resync stations, with the guest (any slot)

A station is a button, a toggle on its right and a sign above (in the hub, the sign covers the
board of the inventory zone). There is one in the hub and one in each of the five towers.

**D1. The sign.** It reads `Press this button to recover your gourds, gadgets and keys in here.`
and, smaller, `The button beside it: gadgets in.` Readable, the right way round, not inside the wall.

**D2. Host, resync.** Drop a few gourds and a gadget far away, press the hub's button.
- Feed: `Resync: N gourd(s) cleared, restocking`; log: `[ResyncStations] Resync in the hub, gadgets in.`
- Everything lands at the hub's inventory spawn (where the game puts a new player's items). The sign says `A resync is running.` meanwhile.

**D3. The toggle.** Press the toggle: its plate turns dark, the sign says `gourds and keys`. Resync:
the gadgets stay where they are. Press it again: green, gadgets in. It is kept after reloading.

**D4. One at a time.** Press a station's button, then at once another's (or the same one again).
- Feed: `A resync is already running`; nothing happens until the first one is finished.

**D5. A tower's station.** In a tower, D2 again: the items land in front of that tower's button.

**D6. Guest.** The guest presses a button and a toggle.
- Its feed: `Only the host can resync` / `Only the host can change this`; nothing happens.
- Its signs follow the host's toggles and the running state.

**D7. No Ctrl+R.** Ctrl+R does nothing.

**D8. `guests_can_resync`** (`BWItems` has it on): the guest's presses on a button and a switch work
as the host's do.

**D9. `tower_resync_stations: false`** (`BWItems` has it off): only the hub's station is there; the
guest does not see the towers' either.

## E. Things from before

**E1. The Silent Gauntlet's stage parts** (`gauntlet-test.yaml`): walk the seven stages and check
that every part of each stage's puzzle is hidden until its item arrives (the ink viewer rule was
fixed but not seen in play).

**E2. A long duo run** with a real generation (`solo-full.yaml` style, two players): play an
hour, note anything odd. This is the last gate before shipping.

## F. Three and four players

The game loads a different version of many landmarks for the number of players chosen when
hosting (`PlayerCountSwapper`: `target2`, `target3`, `target4`); everything above was made on the
two-player one. With the loopback guest, host a save choosing **3**, then **4** players.

**F0.** Does the game start a 3- or 4-player session with two players connected? If not, F needs
real players.

**F1. What is loaded.** Keypad 3 anywhere: the timer dump should show `LandmarksPlayerCount3` (or
4) paths. Note the Cabin Fever houses' names.

**F2. Cabin Fever** (`BWCabin`): A1 and A2 again. The timer must be found (`timer found`). Only the
layout of the buttons changes with the player count (player), so the help button stays put.

**F3. The Gauntlet** (`gauntlet-test.yaml`): walk the stages. More players add walls (player): check
that the extra walls do not block a stage the mod holds or opens, and that stairways behave.

**F4. Everything else at a glance** (the player expects the two-player version to be the most
generous with tools, so hiding should only ever be stricter with more players): teleport buttons, gather buttons and the Resync button stand
where they did (not inside new walls), skip aids and hidden puzzle parts look right.

## Results

| Test | Result | Notes |
|---|---|---|
| A1 | PASS | 30 s wait, 2026-10-05 |
| A2 | PASS | host: -10 s on a running wait, nothing without one |
| A3 | PASS | guest presses run on the host: 10 s off each |
| A4 | PASS | 90 s wait, help button -10 s |
| A5 | PASS | BWTowers: 300 s and 1800 s, no help button |
| B1 | PASS (log) | no hub button placed on a fresh save; resync, gather and ways back there |
| B2 | PASS | red: lookout light opens the hub button; there and back |
| B3 | PASS | top buttons write BlackTowerInteriorDoor, hub button appears |
| B4 | PASS | chapel buttons: EndingGate = 2, hub button appears |
| B5 | PASS | guest sees the same hub buttons and teleports |
| B6 | PASS | door open on a fresh save |
| B7 | PASS | hub buttons, no way back |
| C1 | PASS | nothing on a fresh save |
| C2 | PASS | item alone shows nothing; with the red lookout the red button appears |
| C3 | PASS | red button still there after reloading |
| D1 | PASS | readable, after fitting each tower's wall |
| D2 | PASS | heap in front of the button; the hub's now goes to its inventory spawn |
| D3 | PASS | switch leaves the gadgets out, kept |
| D4 | PASS | second press refused while the first runs |
| D5 | PASS | tower station: heap in front of its button |
| D6 | PASS | guest refused on the host, told, switch put back |
| D7 | PASS | Ctrl+R does nothing |
| D8 | PASS | guest presses work; its sign lagged, fixed (snapshot sent at once) |
| D9 | PASS | only the hub's station |
| E1 | | |
| E2 | | |
| F0 | PASS | the game starts 3 and 4 player sessions with fewer players connected (player); only layouts change |
| F1 | | |
| F2 | | |
| F3 | | |
| F4 | | |
