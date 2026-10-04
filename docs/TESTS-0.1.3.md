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

**B3. The Black Tower.** Fill the five monuments (hub, red, green, blue, yellow: 24 gourds,
ask for them).
- The **black** button appears once the last slot is filled.
- (Second way in, no need to test separately: holding the two buttons at the top of the Black
  Tower writes `BlackTowerInteriorDoor = 1`, which also opens it.)

**B4. The Silent Gauntlet.** Hold the chapel's two buttons together (End on one, press the other).
- Log: `EndingGate = 2`. The **Gauntlet** button appears.

**B5. Guest.** Start the guest after B2.
- The guest sees the same hub buttons as the host, and its own presses teleport it.

## C. Teleports, `items` (`BWItems`)

**C1. Nothing at first.** No hub button on a fresh save.

**C2. One item, one button.** Ask Claude for `Red Funnel Tower Teleporter`.
- Feed: `Received: Red Funnel Tower Teleporter`; log: `[TeleportButtons] Teleporter to Red Funnel
  Tower granted.` The red button appears; the other five do not.

**C3. Kept.** Quit to the menu and load the save again.
- The red button is still there (the save keeps the ledger `ap_tp_red`).

## D. Resync and gather, with the guest (`BWCabin` or any slot)

**D1. Host, Resync.** Drop a few gourds far away, press the Resync button at the hub.
- Feed: `Resync: N gourd(s) cleared, restocking the hub`; the gourds come back at the hub.

**D2. Guest, Resync.** The guest presses the Resync button.
- Same result as D1: the host's log has the resync lines (it runs on the host now, no more
  "Only the host can resync").

**D3. Gather in a tower.** Carry gourds out of a tower, then press that tower's gather button
(coloured plate, no icon, next to the way back), once as host and once as guest.
- The gourds, keys and gadgets land in a heap in front of the button.

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

**F2. Cabin Fever** (`BWCabin`): A1 and A2 again. The timer must be found (`timer found`) and the
help button must stand on a wall, inside the house.

**F3. The Gauntlet** (`gauntlet-test.yaml`): walk the stages; walls, stairways and hidden parts
behave as with two players.

**F4. Everything else at a glance**: teleport buttons, gather buttons and the Resync button stand
where they did (not inside new walls), skip aids and hidden puzzle parts look right.

## Results

| Test | Result | Notes |
|---|---|---|
| A1 | | |
| A2 | | |
| A3 | | |
| A4 | | |
| A5 | | |
| B1 | | |
| B2 | | |
| B3 | | |
| B4 | | |
| B5 | | |
| C1 | | |
| C2 | | |
| C3 | | |
| D1 | | |
| D2 | | |
| D3 | | |
| E1 | | |
| E2 | | |
| F0 | | |
| F1 | | |
| F2 | | |
| F3 | | |
| F4 | | |
