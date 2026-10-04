using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // What changes while the player presses things, for the buttons whose effect is saved nowhere
    // the mod already looks (the chapel's, the top of the Black Tower's: ROADMAP U10). Switched on
    // and off with one key; while on, it writes to the log
    //
    //  - every value the game saves, under any key, when it changes, and
    //  - every state of a networked peck state (what buttons and doors are made of) set within
    //    `Radius` metres of the player, with its place in the scene.
    //
    // Pure diagnostic, no writes to the game, and off until asked for: states change all the time.
    internal static class DebugStateLog
    {
        private const float Radius = 25f;
        private static readonly Dictionary<string, int> LastSaved = new();
        private static readonly Dictionary<int, int> LastState = new();

        internal static bool On { get; private set; }

        internal static void Toggle()
        {
            On = !On;
            LastSaved.Clear();
            LastState.Clear();
            Plugin.Log.LogInfo($"[{nameof(DebugStateLog)}] {(On ? "ON: logging every saved value and every nearby state change." : "OFF.")}");
        }

        internal static void ObserveWrite(string key, int value)
        {
            if (!On)
                return;

            if (LastSaved.TryGetValue(key, out var before) && before == value)
                return;

            LastSaved[key] = value;
            Plugin.Log.LogInfo($"[{nameof(DebugStateLog)}] saved {key} = {value} ({Where()})");
        }

        internal static void ObserveState(TrackedPeckState state, int value)
        {
            if (!On || state == null)
                return;

            try
            {
                var player = DebugPlayerLookup.FindLocalPlayer();
                if (player == null)
                    return;

                var position = state.transform.position;
                if ((position - player.transform.position).sqrMagnitude > Radius * Radius)
                    return;

                var id = state.GetInstanceID();
                if (LastState.TryGetValue(id, out var before) && before == value)
                    return;

                LastState[id] = value;
                var path = state.name;
                var parent = state.transform.parent;
                for (var depth = 0; depth < 4 && parent != null; depth++, parent = parent.parent)
                    path = parent.name + "/" + path;

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugStateLog)}] state {value} on {path} at {position.ToString("F1")} "
                    + $"({(position - player.transform.position).magnitude:F1} m away)");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugStateLog)}] {ex.Message}");
            }
        }

        private static string Where()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            return player != null ? "at " + player.transform.position.ToString("F1") : "no player";
        }
    }
}
