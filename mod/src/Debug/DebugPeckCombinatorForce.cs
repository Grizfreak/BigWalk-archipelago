using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Action tool (not a diagnostic): directly forces the state of the
    // TrackedPeckState instances a nearby PeckCombinator watches
    // (rule.systems[] AND rule.block.systems[], cf.
    // DebugPeckCombinatorLookup/F7) to their desiredState, instead of
    // simulating button presses.
    //
    // Rationale (bell at the top of the Gauntlet, cf. big-walk-archipelago-
    // notes.md): the "N-hold" buttons seem to require a continuous hold by 2
    // players, not a simple tap — DebugPeckFire.Peck() (a discrete event) is
    // not enough and, worse, re-pecking the ONLY button found in range (your
    // own) toggles it instead of simulating the second player.
    // TrackedPeckState.SetState(int) writes the state directly and
    // short-circuits the entire press/hold mechanism.
    internal static class DebugPeckCombinatorForce
    {
        internal static void ForceNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckCombinator>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] No PeckCombinator found in the current area.");
                return;
            }

            // Says what is actually around before filtering. Without this
            // the method had a completely silent exit — every combinator out
            // of range simply `continue`d and it returned having logged
            // nothing at all, which is indistinguishable from "the key did
            // nothing". That cost an hour on 2026-09-18, chasing a keyboard
            // problem that did not exist: the tool worked, the puzzle's
            // combinator was just further than the radius.
            var nearestName = string.Empty;
            var nearestDistance = float.MaxValue;
            var inRange = 0;

            foreach (var combinator in all)
            {
                if (combinator == null)
                    continue;

                float sqrDistance;
                try
                {
                    sqrDistance = (combinator.transform.position - origin).sqrMagnitude;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}] Unreadable PeckCombinator (position): {ex.Message}");
                    continue;
                }

                var distance = Mathf.Sqrt(sqrDistance);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestName = combinator.gameObject.name;
                }

                if (sqrDistance > sqrRadius)
                    continue;

                inRange++;
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] {combinator.gameObject.name} (PeckCombinator) — starting force.");

                try
                {
                    ForceCombinator(combinator);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(DebugPeckCombinatorForce)}] {combinator.gameObject.name} — exception while forcing, block skipped: {ex}");
                }
            }

            if (inRange == 0)
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckCombinatorForce)}] {all.Length} PeckCombinator(s) in the scene, none within {radius:0}m."
                    + (nearestName.Length > 0 ? $" Nearest: '{nearestName}' at {nearestDistance:0.0}m." : string.Empty));
        }

        private static void ForceCombinator(PeckCombinator combinator)
        {
            var rules = combinator.rules;
            if (rules == null || rules.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]   (no rule)");
                return;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                try
                {
                    var rule = rules[i];
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]   rule[{i}] — target desiredState={rule.desiredState}");

                    ForceState(rule.systems, rule.desiredState);

                    var block = rule.block;
                    if (block != null)
                        ForceState(block.systems, rule.desiredState);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}]   rule[{i}] — exception, ignored: {ex.Message}");
                }
            }
        }

        private static void ForceState(TrackedPeckState[] systems, int desiredState)
        {
            if (systems == null)
                return;

            for (var s = 0; s < systems.Length; s++)
            {
                var state = systems[s];
                if (state == null)
                    continue;

                try
                {
                    state.SetState(desiredState);
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]     {state.gameObject.name}.SetState({desiredState}) called.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}]     SetState exception, ignored: {ex.Message}");
                }
            }
        }
    }
}
