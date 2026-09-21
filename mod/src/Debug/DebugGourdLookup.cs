using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Lookup shared between the debug tools (DebugGourdUnlocker, DebugItemSimulator).
    internal static class DebugGourdLookup
    {
        // Pure diagnostic (no writes). Scans Prop.allProps (not RewardGourd):
        // confirmed in a test session on 2026-09-07, big keys have NO
        // RewardGourd component at all (0 found via
        // FindObjectsByType<RewardGourd>, even including inactive ones, while
        // the player was standing in front of a key) — so contrary to the
        // notes' initial hypothesis ("same RewardGourd/Prop pipeline as
        // gourds"), a big key is a bare Prop (probably with a PeckSwitch
        // onUseAsKey / PropGroup.BigKey, cf. big-walk-archipelago-notes.md),
        // not a RewardGourd with a GourdState. Prop.allProps is the registry
        // that covers all of them, gourds and big keys alike.
        internal static void LogNearby(int count)
        {
            var all = Prop.allProps;
            if (all == null || all.Count == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] No Prop found in the current area.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            // Prop.allProps is an Il2CppSystem.Collections.Generic.List<Prop>
            // (not a System.Collections.Generic.List<T>): no IEnumerable<T>
            // on the interop side, so no LINQ directly on it — we materialize
            // it into a real .NET List<> before sorting/logging.
            var entries = new List<(Prop prop, RewardGourd gourdComponent, bool active, bool saved, float sqrDistance)>();
            foreach (var prop in all)
            {
                if (prop == null || prop.saveablePropName == SaveablePropName.notSavable)
                    continue;

                var sqrDistance = playerPosition.HasValue
                    ? (prop.transform.position - playerPosition.Value).sqrMagnitude
                    : 0f;
                entries.Add((
                    prop,
                    prop.GetComponent<RewardGourd>(),
                    prop.gameObject.activeInHierarchy,
                    SaveManager.GetIntValue(prop.saveablePropName.ToString(), 0, false) != 0,
                    sqrDistance));
            }

            entries.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            var playerLabel = playerPosition.HasValue ? playerPosition.Value.ToString() : "<local player not found>";
            Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] Player position: {playerLabel}");
            Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] {entries.Count} savable Prop in the area, the {Math.Min(count, entries.Count)} nearest:");
            for (var i = 0; i < entries.Count && i < count; i++)
            {
                var entry = entries[i];
                var distance = playerPosition.HasValue ? Math.Sqrt(entry.sqrDistance).ToString("F1") : "?";
                var gourdState = entry.gourdComponent != null ? entry.gourdComponent.gourdState.ToString() : "n/a (no RewardGourd)";
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}]   {entry.prop.saveablePropName} — gourdState={gourdState} — already saved={entry.saved} — active={entry.active} — distance={distance}m");
            }
        }

        // Pure diagnostic: lists the PropHome instances actually registered in
        // the scene (not the raw SaveableHomeName enum list, which can
        // contain reserved/unused values — cf. gourdTesting/bigKeyTesting)
        // whose name contains "monoument" [sic, matches the game's own typo].
        // Useful to verify in-game the real number of slots per monument (the
        // player recalls 4 for the tutorial and 5 for the towers, which does
        // not match the raw count found in the enum — to be confirmed against
        // the PropHome instances that are actually active).
        // Every RewardGourd currently loaded, with the one property the
        // Archipelago logic turns out to need: isVariantChallenge.
        //
        // Why it exists (2026-09-21): the player found that the purple gourds
        // and one radio station sit behind the chairlift, so they cannot be
        // reached before the Green Cup Key. The world's region graph assumes
        // the island is open apart from the ending, which makes that a real
        // seed-breaking bug rather than a rough edge. Fixing it needs the list
        // of which puzzles are gated, and a purple gourd is exactly a
        // RewardGourd with isVariantChallenge set — so this prints the roster
        // wherever it is pressed.
        //
        // Press it in the gated zone and again somewhere plainly open: the
        // difference between the two lists is the set that needs a region.
        internal static void LogAllLoaded()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            // Sorted by distance, and that is the whole correction of
            // 2026-09-21: the first version of this printed presence, and
            // presence turned out to be useless because the game loads every
            // gourd everywhere — two presses in two different zones returned
            // byte-identical lists. Only distance says where something is.
            var entries = new List<(string name, bool variant, bool known, string state, float distance)>();
            var variants = 0;

            foreach (var gourd in all)
            {
                if (gourd == null)
                    continue;

                var prop = gourd.prop;
                var propName = prop != null ? prop.saveablePropName.ToString() : "<no prop>";
                var known = prop != null && Core.GourdRegistry.TryGetLocationId(prop.saveablePropName, out _);

                if (gourd.isVariantChallenge)
                    variants++;

                var distance = playerPosition.HasValue && prop != null
                    ? Vector3.Distance(prop.transform.position, playerPosition.Value)
                    : -1f;

                entries.Add((propName, gourd.isVariantChallenge, known, gourd.gourdState.ToString(), distance));
            }

            entries.Sort((a, b) => a.distance.CompareTo(b.distance));

            foreach (var entry in entries)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGourdLookup)}]   {entry.distance.ToString("0.0").PadLeft(7)}m  {entry.name} "
                    + $"| variantChallenge={entry.variant} | isALocation={entry.known} | state={entry.state}");
            }

            // The counts matter more than the lines: "none here" and "the key
            // did not register" must never look alike.
            Plugin.Log.LogInfo(
                $"[{nameof(DebugGourdLookup)}] {all.Length} RewardGourd(s) in the world, {variants} of them purple"
                + " (variantChallenge), nearest first"
                + (playerPosition.HasValue ? "." : " — NO LOCAL PLAYER FOUND, so every distance is meaningless."));
        }

        internal static void LogMonumentHomes()
        {
            var all = PropHome.allPropHomes;
            if (all == null || all.Count == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] No PropHome found in the current area.");
                return;
            }

            var matches = new List<PropHome>();
            foreach (var home in all)
            {
                if (home != null && home.saveableHomeName.ToString().Contains("monoument", StringComparison.OrdinalIgnoreCase))
                    matches.Add(home);
            }

            Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] {all.Count} PropHome total in the area, {matches.Count} containing 'monoument':");
            foreach (var home in matches)
            {
                var pinnedLabel = home.pinnedProp != null ? home.pinnedProp.saveablePropName.ToString() : "<empty>";
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}]   {home.saveableHomeName} — pinnedProp={pinnedLabel}");
            }
        }

        // Generic lookup, successor to FindNearestLocked: covers both gourds
        // AND big keys based on the canonical signal "not yet written to
        // SaveManager" (the same one ItemApplier/CheckTracker already use)
        // rather than RewardGourd.gourdState, which does not exist for big
        // keys (cf. LogNearby above).
        internal static Prop FindNearestUncollectedProp()
        {
            var all = Prop.allProps;
            if (all == null || all.Count == 0)
                return null;

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            Prop closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (var prop in all)
            {
                if (prop == null || prop.saveablePropName == SaveablePropName.notSavable)
                    continue;

                if (SaveManager.GetIntValue(prop.saveablePropName.ToString(), 0, false) != 0)
                    continue;

                if (playerPosition == null)
                    return prop;

                float sqrDistance = (prop.transform.position - playerPosition.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = prop;
                }
            }

            return closest;
        }

        internal static RewardGourd FindNearestLocked()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            // Prefers the one closest to the local player (rather than an
            // arbitrary order) so the effect is immediately visible/testable.
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            RewardGourd closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (var gourd in all)
            {
                if (gourd == null || gourd.gourdState != GourdFlag.GourdState.Locked)
                    continue;

                if (playerPosition == null)
                    return gourd;

                float sqrDistance = (gourd.transform.position - playerPosition.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = gourd;
                }
            }

            return closest;
        }
    }
}
