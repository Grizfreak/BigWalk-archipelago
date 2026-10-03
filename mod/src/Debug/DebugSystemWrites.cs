using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Says which of the game's saved switches (`SavableSystem`) changes, when
    // and where the player stood: the way to find out what a flag the mod does
    // not know yet is for (the lookout lights, the Black Tower's inner door, the
    // Poet and Priest / Pontiff doors, the skip aids). Play normally, then read
    // the `[SystemWrites]` lines of the log.
    //
    // Pure diagnostic, called from SaveValuePatch only while Debug.Enabled is
    // on. A system is logged when its value CHANGES, which is why the last value
    // seen is kept: the game writes the same value again at every load.
    internal static class DebugSystemWrites
    {
        private static readonly Dictionary<SavableSystem, int> Last = new();

        internal static void Observe(SavableSystem system, int value)
        {
            try
            {
                if (system == SavableSystem.NotSavable)
                    return;

                if (Last.TryGetValue(system, out var before) && before == value)
                    return;

                Last[system] = value;

                var player = DebugPlayerLookup.FindLocalPlayer();
                var where = player != null ? player.transform.position.ToString("F1") : "<no player>";
                Plugin.Log.LogInfo(
                    $"[SystemWrites] {Time.realtimeSinceStartup:F0}s {system} ({(int)system}) = {value} at {where}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[SystemWrites] {system}: {ex.Message}");
            }
        }
    }
}
