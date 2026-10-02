using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // "What is that thing?" — for the parts DebugPuzzleNeedsDump could not
    // name (speakers, teapots, lights, a panel nobody can place), added
    // 2026-10-02 for lock_puzzle_needs. Two answers in one press:
    //
    //  - the object under the crosshair: its hierarchy path, the components
    //    on it and on each parent up to the puzzle's root, the nearest
    //    puzzle, and the state of any PeckSwitch / TrackedPeckState on it;
    //  - everything with a collider within a few metres, one line each, so
    //    a thing behind the crosshair is not missed.
    //
    // Read-only. The output goes to the BepInEx log.
    internal static class DebugLookAt
    {
        private const float RayDistance = 25f;
        private const float NearbyRadius = 4f;
        private const int MaxParents = 6;
        private const int MaxNearby = 60;

        internal static void Log()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}] No main camera.");
                return;
            }

            var origin = camera.transform.position;
            var (nearest, nearestDistance) = NearestPuzzle(origin);
            Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}] ---- from {Fmt(origin)}, nearest puzzle {nearest} at {nearestDistance.ToString("F1", CultureInfo.InvariantCulture)}m");

            // The ray starts inside the player's own head: skip every hit on a
            // PlayerCharacter and take the nearest of the rest.
            var hits = Physics.RaycastAll(origin, camera.transform.forward, RayDistance)
                .Where(h => !PathOf(h.collider.transform).StartsWith("PlayerCharacter", StringComparison.Ordinal))
                .OrderBy(h => h.distance)
                .ToList();
            if (hits.Count > 0)
            {
                var hit = hits[0];
                Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}] looking at {PathOf(hit.collider.transform)} "
                    + $"({hit.distance.ToString("F1", CultureInfo.InvariantCulture)}m)");
                var t = hit.collider.transform;
                for (var i = 0; t != null && i <= MaxParents; i++, t = t.parent)
                    Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}]   {(i == 0 ? "on" : "parent " + i)} {t.name}: {Components(t.gameObject)}");
            }
            else
            {
                Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}] nothing under the crosshair within {RayDistance}m.");
            }

            var colliders = Physics.OverlapSphere(origin, NearbyRadius);
            var seen = new HashSet<int>();
            var lines = new List<string>();
            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var go = collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
                if (!seen.Add(go.GetInstanceID()))
                    continue;
                lines.Add($"{Vector3.Distance(origin, go.transform.position).ToString("F1", CultureInfo.InvariantCulture)}m {PathOf(go.transform)}: {Components(go)}");
            }

            Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}] {lines.Count} thing(s) within {NearbyRadius}m (showing {Math.Min(lines.Count, MaxNearby)}):");
            foreach (var line in lines.OrderBy(l => float.Parse(l.Substring(0, l.IndexOf('m')), CultureInfo.InvariantCulture)).Take(MaxNearby))
                Plugin.Log.LogInfo($"[{nameof(DebugLookAt)}]   {line}");
        }

        private static (string name, float distance) NearestPuzzle(Vector3 at)
        {
            var name = "<none>";
            var best = float.MaxValue;
            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gourds == null)
                return (name, best);

            foreach (var gourd in gourds)
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                    continue;
                var distance = Vector3.Distance(at, prop.transform.position);
                if (distance < best)
                {
                    best = distance;
                    name = prop.saveablePropName.ToString();
                }
            }

            return (name, best);
        }

        private static string Components(GameObject go)
        {
            var names = new StringBuilder();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null)
                    continue;
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(c.GetIl2CppType().Name);
                try
                {
                    var sw = c.TryCast<PeckSwitch>();
                    if (sw != null && sw.trackedStateSystem != null)
                        names.Append($"[state={sw.trackedStateSystem.currentPeckContext.compressedState} label={sw.trackedStateSystem.label}]");
                    var re = c.TryCast<PegTileRenderer>();
                    if (re != null)
                        names.Append($"[{re.propGroup}]");
                }
                catch (Exception)
                {
                }
            }

            return names.ToString();
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Fmt(Vector3 v) =>
            $"x={v.x.ToString("F1", CultureInfo.InvariantCulture)} y={v.y.ToString("F1", CultureInfo.InvariantCulture)} z={v.z.ToString("F1", CultureInfo.InvariantCulture)}";
    }
}
