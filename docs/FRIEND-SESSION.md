# A multiworld session: puzzle needs, 2026-10-02

One player (Big Walk, with the loopback guest on Ctrl+L as the second body)
and a friend playing another game in the same multiworld. `lock_puzzle_needs`
is on (`tools/players/friends-test.yaml`): a puzzle is in logic once everything
it is built from has been received, and nothing else. No puzzle sits behind an
item of its own. Items for Big Walk now come from the
friend's game too, and Big Walk's checks send items to theirs.

What this session is for is **data**: in what order things happened, what
each screen showed, and where the logic and the game disagree.

## Setup

- Whoever generates puts `tools/players/friends-test.yaml` next to the friend's
  YAML, and `apworld/dist/bigwalk.apworld` in `custom_worlds/`.
- The friend needs nothing from this repo: they play their own game.
- The save is named `BigWalk`: the hosting screen's first field is also the
  slot name. Join the room with the address of the real server.
- `dist/BigWalkArchipelago-0.1.2.0-20261002.zip` is only for a Big Walk player
  who does not have the dev install.

## What gets recorded without doing anything

- `BepInEx/session-journal.tsv`, **one line per event with the time and where
  you stood**: the connection, every item received (and who sent it, from
  which location), every check reported, every need opened, the world loading
  and going, and a status line a minute. It is appended and flushed on every
  line, and the loopback guest writes into it too (the `role` column tells
  them apart). Send it back whole.
  **It survives restarts**: `LogOutput.log` is overwritten by BepInEx at every
  launch, the journal is only ever added to. Each line starts with the id of
  the launch that wrote it, and a `launch` line opens every run, so several
  sessions are told apart in one file. Past 4 MB it is moved to
  `session-journal.old.tsv` (replacing the previous one), so it never holds
  more than two generations — about 6 KB per hour of play.
- `BepInEx/LogOutput.log` (and `LogOutput.1.log` for the guest) — copy them out
  at the end of a session, the next launch replaces them.
- The top-left overlay now says which puzzle parts are still missing
  (`Puzzle parts missing (12): buttons, icon panels, ...`).

## Keys

| Key | What |
|---|---|
| Ctrl+Z | A marker in the journal and the log with the state of every need: press it **the moment something looks wrong**. |
| Ctrl+E | Names what is under the crosshair and everything within 4 m. |
| Ctrl+X | Position and nearest puzzle. |
| Ctrl+Q | Unlock every need by hand (the way out of a dead end, local only). |
| Ctrl+W | Lock/unlock the next need, one per press. |
| Ctrl+D | Noon, clock stopped. |
| Ctrl+P | Sends a DeathLink to the multiworld (host only, 3 s between two). The friend's game must have DeathLink on to react; Big Walk itself has no death to receive. |

## What to try

1. **Spawn to first check.** With the one random start need, can a tutorial
   puzzle be solved at once? Can you leave the tutorial?
2. **Each item received.** Watch what appears when a Big Walk item comes in
   from the friend. Anything that should have appeared and did not, or the
   other way round: Ctrl+Z.
3. **A puzzle with everything it is built from.** Can it be completed? If not, which?
4. **A puzzle completed with something still missing.** That is the logic being
   cautious, not a bug, but note it (Ctrl+Z) so we know which needs are not
   really needed.
5. **Co-op events with the loopback guest**, once each: the guest joins after
   items were received; it quits and comes back. (The host cannot reload its
   world with a guest connected: the guest is dropped. A need arriving while
   you stand in a puzzle that uses it is just item reception, test 2.)
6. **Dead ends.** Anywhere you cannot go on: Ctrl+Z, then Ctrl+Q to get out.
7. **Left alone on purpose**, and should NOT disappear: door handles, the
   train's controls, the hub gates, the lookouts, the Gauntlet, the chapel bell
   buttons, the radios at the peaks.

## What to send back

`BepInEx/session-journal.tsv`, both logs, and a line on what looked wrong with
roughly the time. If the friend's game has its own spoiler log, that too: it
shows where the Big Walk items were placed.
