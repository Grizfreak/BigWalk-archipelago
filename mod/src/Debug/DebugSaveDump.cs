using System;

namespace BigWalkArchipelago.Debug
{
    // Pure diagnostic (no writes). For mechanisms whose Peck chain leads to
    // no usable TrackedPeckState/savableSystem (e.g. the Gauntlet, cf.
    // big-walk-archipelago-notes.md — NHoldSucess is NotSavable and nothing
    // downstream is visible from a PeckCombinator/PeckSwitch), the only
    // reliable way to find the real key is to compare a full SaveManager
    // dump before/after the in-game action (e.g. triggering the bell) rather
    // than guessing via the object graph.
    internal static class DebugSaveDump
    {
        internal static void DumpAll()
        {
            var data = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (data == null || data.entries == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] No SaveData loaded.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}]   skipAidsActive={data.skipAidsActive}");

            var entries = data.entries;
            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] --- full dump ({entries.Count} int entries) ---");

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}]   {entry.key}={entry.value}");
            }

            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] --- end of dump ---");
        }
    }
}
