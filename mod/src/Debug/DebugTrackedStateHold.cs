using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Holds a push button down, for as long as the key is held.
    //
    // Why this exists, and why the two tools next to it could not do it.
    // The sealed-box puzzles are opened by a button somewhere else in the
    // room, and their mechanism turned out to be neither of the ones already
    // covered (established in-game on 2026-09-18):
    //
    //   PeckSwitchTrigger (PeckSwitch, specificState=1) -> BasicPushButton
    //   UpSwitch          (PeckSwitch, specificState=0) -> BasicPushButton
    //
    // A momentary push button: one switch drives its TrackedPeckState to 1
    // on press, another back to 0 on release. DebugPeckFire sends a single
    // discrete peck, which the release immediately undoes;
    // DebugPeckCombinatorForce only knows about PeckCombinator, and the
    // nearest one to these puzzles sits 178m away — they are N-hold
    // hardware, a different thing entirely.
    //
    // So this writes the state directly, every frame, while the key is down:
    // TrackedPeckState.SetState(int) short-circuits the whole press/hold
    // mechanism, and re-applying it each frame is what a sustained hold
    // actually is. Releasing the key stops writing and lets the game put the
    // button back up on its own, exactly as if a second player had let go.
    //
    // NEAREST match only, deliberately. "BasicPushButton" is a generic name
    // shared by buttons all over the world; forcing every match in the zone
    // would press buttons in puzzles nobody asked about.
    internal static class DebugTrackedStateHold
    {
        private static TrackedPeckState _held;
        private static bool _wasHolding;

        internal static void Update(bool keyIsDown, string nameFragment, int value, float radius)
        {
            if (!keyIsDown)
            {
                if (_wasHolding)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugTrackedStateHold)}] Released '{(_held != null ? _held.gameObject.name : "?")}'.");
                    _wasHolding = false;
                    _held = null;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(nameFragment))
            {
                if (!_wasHolding)
                {
                    _wasHolding = true;
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugTrackedStateHold)}] No name configured (Debug/HoldTrackedStateName), nothing to hold.");
                }

                return;
            }

            // Resolved once per hold, not per frame: FindObjectsByType is not
            // free, and re-picking the nearest every frame would hand the
            // hold to a different button as the player drifts.
            if (!_wasHolding)
            {
                _wasHolding = true;
                _held = FindNearest(nameFragment, radius);

                if (_held == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugTrackedStateHold)}] No TrackedPeckState matching '{nameFragment}' within {radius:0}m.");
                    return;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugTrackedStateHold)}] Holding '{_held.gameObject.name}' at state {value} — let go of the key to release.");
            }

            if (_held == null)
                return;

            try
            {
                _held.SetState(value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugTrackedStateHold)}] SetState failed, hold abandoned: {ex.Message}");
                _held = null;
            }
        }

        private static TrackedPeckState FindNearest(string nameFragment, float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugTrackedStateHold)}] Local player not found.");
                return null;
            }

            var origin = localPlayer.transform.position;
            var all = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugTrackedStateHold)}] No TrackedPeckState in the current area.");
                return null;
            }

            TrackedPeckState best = null;
            var bestDistance = radius;

            foreach (var state in all)
            {
                if (state == null)
                    continue;

                if (state.gameObject.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                float distance;
                try
                {
                    distance = Vector3.Distance(state.transform.position, origin);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugTrackedStateHold)}] Unreadable TrackedPeckState (position): {ex.Message}");
                    continue;
                }

                if (distance >= bestDistance)
                    continue;

                best = state;
                bestDistance = distance;
            }

            if (best != null)
                Plugin.Log.LogInfo($"[{nameof(DebugTrackedStateHold)}] Nearest match: '{best.gameObject.name}' at {bestDistance:0.0}m.");

            return best;
        }
    }
}
