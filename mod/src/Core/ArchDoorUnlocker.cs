using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Automatically opens the "Arch doors"/HubGate (hub shortcuts, normally
    // unlocked one at a time via a one-way button behind each door) from
    // the very first session on a given save. User decision (cf.
    // big-walk-archipelago-notes.md, session on 2026-09-09): these
    // shortcuts have no reason to stay closed in an Archipelago world,
    // unlike gourds/big keys which are the actual randomized content.
    //
    // Timing fix: simple polling in Update() on
    // WorldManager.isReadyForEffects (a static game property designed to
    // signal "safe to trigger effects now") — a one-shot event
    // (OnWorldManagerStart, onLocalPlayerCharcterStart) fired too early,
    // before the game's "peck manager" existed (confirmed in-game by a
    // burst of "no peck manager instance. this is maybe too early").
    // Component added unconditionally (not only when Debug.Enabled, cf.
    // Plugin.Load()): this isn't a debug tool, it's a real mod feature.
    //
    // Precise targeting (2026-09-09): PeckDevHelper.Trigger(UnlockRules{
    // unlocks=true}) — the dev cheat already built into the game, found by
    // scanning the components near a HubGate — did open the Arch doors,
    // but broadcast to the ENTIRE "unlocks" category: direct inspection of
    // several save files confirmed it also persists EndingGate=1 and the 7
    // FmStation*/4 LookoutLight* values to 1, in addition to the 3 hub keys
    // actually wanted. Ghidra decompilation of TrackedPeckState.SetState
    // confirmed the real SaveManager key written is Enum.ToString(
    // savableSystem) — hence writing directly here, bypassing Trigger,
    // which avoids any side effect on EndingGate/FmStations/LookoutLights.
    // The 3 keys below are the only ones in the "hub shortcut" category
    // confirmed to be written together by Trigger(unlocks) across several
    // test saves (SpawnHubGate + HubTunnel + HubShortcutToSportsCreek) —
    // the same philosophy as the Arch doors (hub navigation shortcuts), so
    // they're kept together here.
    //
    // Live effect (2026-09-09): a raw SaveManager write alone causes no
    // visual effect in the current session (confirmed in-game: doors still
    // closed after a direct write, without a reload) — unlike Trigger()
    // which went through the real Peck mechanic. Fix: call
    // TrackedPeckState.SetState(1) directly on the instances found in the
    // scene (savableSystem == the targeted key) rather than
    // SaveManager.SetIntValue — SetState performs exactly the same write
    // internally (confirmed via Ghidra) while also triggering the
    // visual/animation callbacks, without Trigger()'s broad broadcast.
    // Falls back to the raw write if the object isn't loaded in the
    // current zone (same philosophy as ItemApplier.TryApplyLiveEffect for
    // gourds/big keys): the load-time restoration will take over on the
    // next reload.
    internal class ArchDoorUnlocker : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ArchDoorUnlocker(IntPtr ptr) : base(ptr)
        {
        }

        private const string OpenedFlagKey = "ap_archdoors_opened";

        private static readonly SavableSystem[] HubShortcutKeys =
        {
            SavableSystem.SpawnHubGate,
            SavableSystem.HubTunnel,
            SavableSystem.HubShortcutToSportsCreek,
        };

        private bool _done;

        private void Update()
        {
            if (_done)
                return;

            // Host authority model, like the rest of the mod (cf. ItemApplier).
            if (!NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            // Latched on the very first frame both conditions are met,
            // whether or not the SaveManager flag is already set — avoids
            // re-checking every frame for the rest of the session.
            _done = true;

            if (SaveManager.GetIntValue(OpenedFlagKey, 0, false) != 0)
                return;

            SaveManager.SetIntValue(OpenedFlagKey, 1);

            Plugin.Log.LogInfo(
                $"[{nameof(ArchDoorUnlocker)}] First session on this save: automatically opening the hub shortcuts.");

            var loaded = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            foreach (var key in HubShortcutKeys)
            {
                TrackedPeckState target = null;
                if (loaded != null)
                {
                    foreach (var state in loaded)
                    {
                        if (state != null && state.savableSystem == key)
                        {
                            target = state;
                            break;
                        }
                    }
                }

                if (target != null)
                {
                    target.SetState(1);
                    Plugin.Log.LogInfo($"[{nameof(ArchDoorUnlocker)}]   {key}: immediate effect applied (object found in scene).");
                }
                else
                {
                    SaveManager.SetIntValue(key.ToString(), 1);
                    Plugin.Log.LogInfo($"[{nameof(ArchDoorUnlocker)}]   {key}: object not loaded, SaveManager write only (effect on next load).");
                }
            }
        }
    }
}
