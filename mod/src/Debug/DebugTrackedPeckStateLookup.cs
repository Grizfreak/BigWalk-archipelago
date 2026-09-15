using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Pure diagnostic (no writes): lists every loaded TrackedPeckState whose
    // savableSystem == SavableSystem.SpawnHubGate (the 3 "Arch doors"/
    // HubGate — Tutorial/Left/Right, cf. big-walk-archipelago-notes.md,
    // session from 2026-09-09) with their SaveIdentity.saveGuid and the
    // currently associated SaveManager value. Goal: empirically confirm that
    // saveGuid is the real persistence key before using it to write directly
    // (like ItemApplier does for gourds/big keys), rather than continuing to
    // go through PeckDevHelper.Trigger, which broadcasts to the whole
    // "unlocks" category and visually opens out-of-scope doors (confirmed to
    // have no permanent effect, but visually distracting).
    internal static class DebugTrackedPeckStateLookup
    {
        internal static void DumpSpawnHubGate()
        {
            var all = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugTrackedPeckStateLookup)}] No TrackedPeckState found in the current area.");
                return;
            }

            var matches = new List<TrackedPeckState> ();
            foreach (var state in all)
            {
                if (state != null && state.savableSystem == SavableSystem.SpawnHubGate)
                    matches.Add(state);
            }

            Plugin.Log.LogInfo($"[{nameof(DebugTrackedPeckStateLookup)}] {all.Length} TrackedPeckState total, {matches.Count} with savableSystem=SpawnHubGate:");
            foreach (var state in matches)
            {
                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<no SaveIdentity>";
                var currentValue = saveIdentity != null
                    ? SaveManager.GetIntValue(saveGuid, -12345, false).ToString()
                    : "n/a";
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugTrackedPeckStateLookup)}]   {state.gameObject.name} — label='{state.label}' — " +
                    $"hasInitialState={state.hasInitialState} — initialState={state.initialState} — " +
                    $"saveGuid={saveGuid} — SaveManager[{saveGuid}]={currentValue}");
            }
        }
    }
}
