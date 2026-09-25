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

        // SINCE 2026-09-25 THE DOORS ARE ArchDoors' TO DECIDE. `lock_arch_doors`
        // can hold any of the three closed until its item arrives, so opening
        // all three on a save's first session — which is what this did, under
        // an `ap_archdoors_opened` flag — would give away what the slot is
        // meant to find. This now only says when: a world has become ready on
        // the host, and slot_data has said which doors are locked (or there
        // is no Archipelago, and none is). The how is ArchDoors.Enforce, run
        // on every world load since a door is idempotent to open.
        private bool _worldWasReady;
        private bool _enforced;

        private void Update()
        {
            var worldReady = NetworkServer.active && WorldManager.isReadyForEffects;
            if (!worldReady)
            {
                // A world going away takes its slot's list with it: the next
                // save may be another slot, or none. Only on the way out, not
                // while a world loads — slot_data usually arrives then.
                if (_worldWasReady)
                    ArchDoors.Forget();

                _worldWasReady = false;
                _enforced = false;
                return;
            }

            if (!_worldWasReady)
            {
                _worldWasReady = true;
                if (!ModConfig.ArchipelagoEnabled.Value)
                    ArchDoors.ConfigureWithoutArchipelago();
            }

            // Waits for slot_data when Archipelago is on; ArchDoors.Configure
            // enforces on its own if it arrives while a world is up.
            if (_enforced || !ArchDoors.Configured)
                return;

            _enforced = true;
            ArchDoors.Enforce();
        }
    }
}
