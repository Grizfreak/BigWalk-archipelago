using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Every timer of the game, for the Cabin Fever puzzles (ROADMAP R6): they ask the player to
    // wait in a room for some minutes, and the wait is very likely a PeckEffectTimer (or its
    // networked sibling), whose `duration` is a plain field. One press lists each of them with
    // its place in the scene, its duration, what starts it and how far it is from the player,
    // so the two Cabin Fever ones can be told from the rest by standing near them.
    //
    // Pure diagnostic, no writes to the game.
    internal static class DebugTimerDump
    {
        private static int _count;

        internal static void Dump()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            var origin = player != null ? player.transform.position : Vector3.zero;
            var text = new StringBuilder();
            text.AppendLine("kind\tdistance\tduration\tactive\tposition\tstate\tpath");

            var rows = 0;
            foreach (var timer in UnityEngine.Object.FindObjectsByType<PeckEffectTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    Row(text, "timer", timer.transform, timer.duration, timer.timerActive, timer.trackedStateSystem, origin);
                    rows++;
                }
                catch (Exception ex)
                {
                    text.AppendLine("# unreadable timer: " + ex.Message);
                }
            }

            foreach (var timer in UnityEngine.Object.FindObjectsByType<PeckEffectTimerNetworked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    Row(text, "networked", timer.transform, timer.duration, timer.timerActive, timer.trackedStateSystem, origin);
                    rows++;
                }
                catch (Exception ex)
                {
                    text.AppendLine("# unreadable timer: " + ex.Message);
                }
            }

            var path = Path.Combine(Paths.BepInExRootPath, $"timer-dump-{++_count}.txt");
            File.WriteAllText(path, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugTimerDump)}] {rows} timer(s) written to {path}.");
        }

        private static void Row(StringBuilder text, string kind, Transform transform, float duration, bool active,
            TrackedPeckState state, Vector3 origin)
        {
            var position = transform.position;
            var path = new StringBuilder(transform.name);
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path.Insert(0, parent.name + "/");

            text.Append(kind).Append('\t')
                .Append((position - origin).magnitude.ToString("F1", CultureInfo.InvariantCulture)).Append('\t')
                .Append(duration.ToString("F1", CultureInfo.InvariantCulture)).Append('\t')
                .Append(active ? "on" : "off").Append('\t')
                .Append(position.ToString("F1")).Append('\t')
                .Append(state != null ? state.name : "-").Append('\t')
                .Append(path).AppendLine();
        }
    }
}
