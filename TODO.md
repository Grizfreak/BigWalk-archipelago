# TODO

From the 0.4 playtest of 2026-10-09 (2 players, 3 Autopelago, host log checked).

## Bugs

- [ ] **Gauntlet bounds cover the whole island.** `Gauntlet.Find` (`mod/src/Core/Traps.cs`) logged
  `from (-814, -54, -817) to (772, 128, 760)`: some huge renderer under `SilentGauntlet 2Player`.
  With `traps_spare_the_gauntlet`, every Big Trip spot is dropped ("no place to send anyone yet",
  12 times) and every player is spared (Big Meeting "0 free to move", 7 times). Big Trip only worked
  once, to `fireworks PeakMarker` (above y = 128), both players on the same spot.
  - Skip oversized renderers (> 200 m) and log their names.
  - If the box is still too big, drop it with a warning instead of sparing everyone.
  - Check in game with `DebugTraps` (`in gauntlet=`), inside and outside the Gauntlet.
- [ ] **Lamp colours.** Players saw an orange lamp giving green light, a blue one giving white light.
  Body, halo and light share one colour (`ColourPainter.PaintIfBuoy`). Check a carried / received
  lamp, and the body tint against the light. Ask for a screenshot.

## Feedback on screen

- [ ] DeathLink lines on the overlay: sent ("DeathLink sent: took a big fall"), received
  ("DeathLink from X: Big Drop"), forgiven ("forgiven 1/3"), skipped by the 10 s cooldown.
  DeathLink worked all along (44 sent, 3 received) but players could not tell.
- [ ] A line when Big Trip or Big Meeting does nothing ("Big Trip: nowhere to go yet").
- [ ] At connection, a line when Settings override the YAML (DeathLink off, amnesty).
- [ ] A line when a big key is placed before its feature item ("Drawbridge: opens when its item
  arrives").

## gourd_name tests (0.4.0)

- [ ] Mod builds.
- [ ] Text Client shows the new name; `!hint <name>` and `!hint Gourd` work.
- [ ] Overlay shows the seed's name (feed and goal line); a name typed in Settings wins; guests see the host's.
- [ ] Universal Tracker: logic and name.
- [ ] Another game of the multiworld sees the new name.
- [ ] Two Big Walk slots with different names: both keep "Gourd", warning in the generation log.
- [ ] Refused name (`Backpack`): "Gourd", and "Gourds called: Gourd" in the spoiler.
- [ ] Upload to archipelago.gg accepted; web tracker shows the name.
- [ ] Seed without the option, or from 0.3: nothing changes.
