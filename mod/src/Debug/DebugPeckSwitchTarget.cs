using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Pure diagnostic (no writes). Continuation of
    // DebugTrackedPeckStateLookup: the dump filtered on
    // savableSystem==SpawnHubGate found only one TrackedPeckState
    // ("GateMainSystem"), but its savableSystem never shows up in the save
    // file after a trigger confirmed in-game — so this is probably NOT the
    // TrackedPeckState that is actually modified. PeckDevHelper.Trigger
    // (decompiled via Ghidra, cf. big-walk-archipelago-notes.md) actually
    // calls PeckSwitch.Peck() on the PeckSwitch attached to the same
    // GameObject as the PeckDevHelper (e.g. "DevUnlock") — and it's the
    // PeckSwitch.trackedStateSystem field (a reference assigned in the Unity
    // editor, not necessarily the nearest TrackedPeckState) that receives
    // the real SetState(). This tool inspects that reference directly to
    // identify the right TrackedPeckState without guessing.
    internal static class DebugPeckSwitchTarget
    {
        internal static void DumpNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckSwitchTarget)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckSwitchTarget)}] No PeckSwitch found in the current area.");
                return;
            }

            foreach (var sw in all)
            {
                if (sw == null)
                    continue;

                var sqrDistance = (sw.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                var target = sw.trackedStateSystem;
                if (target == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckSwitchTarget)}]   {sw.gameObject.name} (PeckSwitch) — trackedStateSystem=<null> — specificState={sw.specificState}");
                    continue;
                }

                var saveIdentity = target.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<no SaveIdentity>";
                var savableSystem = target.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckSwitchTarget)}]   {sw.gameObject.name} (PeckSwitch, specificState={sw.specificState}) -> " +
                    $"trackedStateSystem={target.gameObject.name} — savableSystem={savableSystem} — saveGuid={saveGuid} — " +
                    $"actual key='{key}' — SaveManager[{key}]={currentValue}");
            }
        }
    }
}
