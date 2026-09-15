using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Pure diagnostic (no writes). Continuation of the investigation into
    // the "bells" in the ending area (cf. big-walk-archipelago-notes.md,
    // "bells" section): PeckCombinator is the generic system that combines
    // several TrackedPeckState instances (e.g. two buttons pressed
    // simultaneously by 2 players) via PeckRule entries (systems[] +
    // minimumMatches), and triggers as output either a direct
    // TrackedPeckState (directControlSystem) or two PeckSwitch
    // (onConditionMet/onConditionStop). Spotted in-game (F9) near
    // GoodbyeChapel: a GameObject named "NHoldLogic" carries exactly a
    // PeckSystemBlock + PeckCombinator, a natural candidate for the bells'
    // "two simultaneous buttons" mechanism. This tool inspects these
    // references directly to find the real SaveManager key without guessing.
    //
    // Hardened on 2026-09-10 after a freeze (Windows "Application Hang", no
    // crash/exception logged) triggered by an F7 near VictoryLogic, in an
    // area (Gauntlet) with denser Peck wiring than the bell rooms already
    // tested successfully. Each combinator/reference is now logged and
    // wrapped individually (try/catch) so a problematic reference only
    // interrupts its own block rather than the whole dump — a safety net for
    // a genuine .NET exception (e.g. accessing a partially destroyed Unity
    // object); it does not help against a hard native crash, but it reduces
    // the risk by logging each step before touching it.
    internal static class DebugPeckCombinatorLookup
    {
        internal static void DumpNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckCombinator>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] No PeckCombinator found in the current area.");
                return;
            }

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
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] Unreadable PeckCombinator (position): {ex.Message}");
                    continue;
                }

                if (sqrDistance > sqrRadius)
                    continue;

                // Logged before reading any fields: if the rest crashes, we
                // at least know which GameObject was being inspected.
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {combinator.gameObject.name} (PeckCombinator) — starting inspection.");

                try
                {
                    DescribeCombinator(combinator);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(DebugPeckCombinatorLookup)}] {combinator.gameObject.name} — exception during inspection, block skipped: {ex}");
                }
            }
        }

        private static void DescribeCombinator(PeckCombinator combinator)
        {
            DescribeTrackedPeckState("  directControlSystem", combinator.directControlSystem);
            DescribeSwitch("  onConditionMet", combinator.onConditionMet);
            DescribeSwitch("  onConditionStop", combinator.onConditionStop);

            var rules = combinator.rules;
            if (rules == null || rules.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}]   (no rule)");
                return;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                try
                {
                    var rule = rules[i];
                    var maxDesc = rule.hasMaximum ? rule.maximumMatches.ToString() : "unlimited";
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckCombinatorLookup)}]   rule[{i}] — minimumMatches={rule.minimumMatches}, maximumMatches={maxDesc}, desiredState={rule.desiredState}");

                    var systems = rule.systems;
                    if (systems != null)
                    {
                        for (var s = 0; s < systems.Length; s++)
                            DescribeTrackedPeckState($"    systems[{s}]", systems[s]);
                    }

                    // rule.block groups TrackedPeckState instances by
                    // PeckSystemBlock rather than directly in systems[] —
                    // this is the case for "N-hold" rules (e.g. NHoldLogic
                    // 2/EndingGate) whose systems[] is empty but which still
                    // reference a group of buttons (e.g. "ButtonNHoldTwo")
                    // via this field.
                    DescribeBlock("    rule.block", rule.block);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}]   rule[{i}] — exception, ignored: {ex.Message}");
                }
            }
        }

        private static void DescribeTrackedPeckState(string label, TrackedPeckState state)
        {
            try
            {
                if (state == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<no SaveIdentity>";
                var savableSystem = state.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckCombinatorLookup)}] {label}={state.gameObject.name} — savableSystem={savableSystem} — " +
                    $"saveGuid={saveGuid} — actual key='{key}' — SaveManager[{key}]={currentValue}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception while reading, ignored: {ex.Message}");
            }
        }

        private static void DescribeBlock(string label, PeckSystemBlock block)
        {
            try
            {
                if (block == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}={block.gameObject.name} (PeckSystemBlock)");

                var systems = block.systems;
                if (systems == null || systems.Length == 0)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label} — (no system in the block)");
                    return;
                }

                for (var s = 0; s < systems.Length; s++)
                    DescribeTrackedPeckState($"{label}.systems[{s}]", systems[s]);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception while reading, ignored: {ex.Message}");
            }
        }

        private static void DescribeSwitch(string label, PeckSwitch sw)
        {
            try
            {
                if (sw == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckCombinatorLookup)}] {label}={sw.gameObject.name} (PeckSwitch, specificState={sw.specificState})");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception while reading, ignored: {ex.Message}");
                return;
            }

            DescribeTrackedPeckState($"{label}.trackedStateSystem", sw.trackedStateSystem);
        }
    }
}
