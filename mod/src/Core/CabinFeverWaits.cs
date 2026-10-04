using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The waits of the two Cabin Fever puzzles (ROADMAP R6), set by the slot (apworld options
    // `cabin_fever_*` and `cabin_fever_long_*`), and the hidden help button of each room.
    //
    // Each wait is a `PeckEffectTimerNetworked` named TimerSystem under the puzzle's house (the
    // house of the number of players chosen: `CabinFever 2Player`, `3Player`, `4Player`)
    // (timer dump of 2026-10-05: 300 s and 1800 s). The server starts it at `now + duration`
    // and the clients draw their dial from `duration`, so the duration is set on every machine:
    // the host from its slot, a guest from the host's snapshot. Setting it before the wait starts
    // is enough; a wait already running keeps its end.
    //
    // The help button takes ten seconds off a running wait each time it is pressed, by moving
    // the timer's end, a SyncVar only the host may write: a guest's press runs on the host.
    internal static class CabinFeverWaits
    {
        private sealed class Puzzle
        {
            internal string Key;
            internal string HousePath;
            internal float Vanilla;
            internal Vector3 HelpPoint;
            internal Vector3 HelpNormal;
            internal int HelpSlot;

            // From the slot (host) or the snapshot (guest): the seconds to set, 0 for the game's.
            internal int Seconds;
            internal bool Help;

            internal PeckEffectTimerNetworked Timer;
            internal float AppliedDuration = -1f;
        }

        // The marks of 2026-10-05 for the help buttons (Keypad 8, one inside each room).
        private static readonly Puzzle[] Puzzles =
        {
            new Puzzle
            {
                Key = "cabin_fever", HousePath = "CabinFever", Vanilla = 300f, HelpSlot = 18,
                HelpPoint = new Vector3(300.71f, 24.06f, -110.36f), HelpNormal = new Vector3(-0.73f, 0f, -0.68f).normalized,
            },
            new Puzzle
            {
                Key = "cabin_fever_long", HousePath = "CabinFeverLong", Vanilla = 1800f, HelpSlot = 19,
                HelpPoint = new Vector3(525.14f, 77.96f, -864.88f), HelpNormal = new Vector3(-0.07f, 0f, -1.00f).normalized,
            },
        };

        private const float HelpSeconds = 10f;
        private const float HelpSink = 0.25f;

        // Never quite zero: a wait of nothing would end the moment it starts, which is what the
        // player asked for, but a timer of zero length may not be one the game expects.
        private const float ShortestWait = 1f;

        internal static void Configure(string mode, int seconds, bool help, string longMode, int longSeconds, bool longHelp)
        {
            Set(Puzzles[0], Resolve(mode, seconds, Puzzles[0].Vanilla), help);
            Set(Puzzles[1], Resolve(longMode, longSeconds, Puzzles[1].Vanilla), longHelp);
        }

        // On a guest: what the host's slot says, from the snapshot.
        internal static void ApplyFromHost(int seconds, int longSeconds, bool help, bool longHelp)
        {
            Set(Puzzles[0], seconds, help);
            Set(Puzzles[1], longSeconds, longHelp);
        }

        internal static void ForgetMirror()
        {
            Set(Puzzles[0], 0, false);
            Set(Puzzles[1], 0, false);
        }

        // For the snapshot.
        internal static int Seconds(int puzzle) => Puzzles[puzzle].Seconds;
        internal static bool Help(int puzzle) => Puzzles[puzzle].Help;

        private static void Set(Puzzle puzzle, int seconds, bool help)
        {
            if (puzzle.Seconds != seconds || puzzle.Help != help)
                Plugin.Log.LogInfo(
                    $"[{nameof(CabinFeverWaits)}] {puzzle.Key}: wait {(seconds > 0 ? seconds + " s" : "as in the game")}, help button {(help ? "on" : "off")}.");

            puzzle.Seconds = seconds;
            puzzle.Help = help;
        }

        // `reduced` never makes the wait longer than the game's; `random_between` has been drawn by
        // the apworld already, so it is a number like `fixed`.
        private static int Resolve(string mode, int seconds, float vanilla)
        {
            switch (mode)
            {
                case "reduced":
                    return (int)Math.Min(seconds, vanilla);
                case "random_between":
                case "fixed":
                    return seconds;
                default:
                    return 0;
            }
        }

        // Every couple of seconds (TeleportButtonRunner): finds each timer once its world is
        // loaded, gives it its duration, and puts the help buttons in or out of the world.
        internal static void Tick()
        {
            foreach (var puzzle in Puzzles)
            {
                ApplyDuration(puzzle);
                SyncHelpButton(puzzle);
            }
        }

        private static void ApplyDuration(Puzzle puzzle)
        {
            var timer = FindTimer(puzzle);
            if (timer == null)
                return;

            var wanted = puzzle.Seconds > 0 ? Math.Max(puzzle.Seconds, ShortestWait) : puzzle.Vanilla;
            if (Mathf.Approximately(timer.duration, wanted) && Mathf.Approximately(puzzle.AppliedDuration, wanted))
                return;

            timer.duration = wanted;
            puzzle.AppliedDuration = wanted;
            Plugin.Log.LogInfo($"[{nameof(CabinFeverWaits)}] {puzzle.Key}: timer set to {wanted:F0} s.");
        }

        private static PeckEffectTimerNetworked FindTimer(Puzzle puzzle)
        {
            if (puzzle.Timer != null)
                return puzzle.Timer;

            foreach (var timer in UnityEngine.Object.FindObjectsByType<PeckEffectTimerNetworked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (timer == null || timer.name != "TimerSystem")
                    continue;

                var parent = timer.transform.parent;
                var path = string.Empty;
                for (var depth = 0; depth < 4 && parent != null; depth++, parent = parent.parent)
                    path = parent.name + "/" + path;

                // "CabinFever 2Player/", "CabinFever 3Player/"...: the game loads the house of the
                // number of players chosen when hosting (PlayerCountSwapper), so any of them.
                if (!IsHouse(path, puzzle.HousePath))
                    continue;

                puzzle.Timer = timer;
                puzzle.AppliedDuration = -1f;
                Plugin.Log.LogInfo($"[{nameof(CabinFeverWaits)}] {puzzle.Key}: timer found ({timer.duration:F0} s in the game).");
                return timer;
            }

            return null;
        }

        private static bool IsHouse(string path, string house)
        {
            var at = path.IndexOf("/" + house, StringComparison.Ordinal);
            if (at < 0)
                return false;

            // "CabinFever 2Player/" for two and three players, "CabinFever/" for four (timer dumps of
            // 2026-10-05): the house's name, then either its count or nothing.
            var rest = path.Substring(at + 1 + house.Length);
            if (rest.StartsWith("/", StringComparison.Ordinal))
                return true;

            return rest.Length > 2 && rest[0] == ' ' && char.IsDigit(rest[1])
                   && rest.Substring(2).StartsWith("Player/", StringComparison.Ordinal);
        }

        private static void SyncHelpButton(Puzzle puzzle)
        {
            // No wait worth helping with: the button would do nothing.
            var wanted = puzzle.Help && (puzzle.Seconds == 0 || puzzle.Seconds > HelpSeconds);
            if (!wanted)
            {
                WorldButtons.Remove(puzzle.HelpSlot);
                return;
            }

            if (WorldButtons.Has(puzzle.HelpSlot))
                return;

            WorldButtons.Add(puzzle.HelpSlot, "Cabin Fever help", puzzle.HelpPoint - puzzle.HelpNormal * HelpSink,
                Quaternion.LookRotation(puzzle.HelpNormal, Vector3.up), presser => TakeTimeOff(puzzle),
                icon: null, tint: "hub", hostSide: true);
        }

        // Runs on the host. Ten seconds off a running wait, never past its end.
        private static void TakeTimeOff(Puzzle puzzle)
        {
            if (!NetworkServer.active)
                return;

            var timer = FindTimer(puzzle);
            if (timer == null || !timer.timerActive)
            {
                Plugin.Log.LogInfo($"[{nameof(CabinFeverWaits)}] {puzzle.Key}: help pressed, but no wait is running.");
                return;
            }

            var remaining = timer.GetTimeRemaining();
            var taken = Math.Min(HelpSeconds, Math.Max(0f, remaining - 0.5f));
            timer.NetworkendTime = timer.EndTime - taken;
            Plugin.Log.LogInfo(
                $"[{nameof(CabinFeverWaits)}] {puzzle.Key}: help pressed, {taken:F0} s taken off, {remaining - taken:F0} s left.");
        }
    }
}
