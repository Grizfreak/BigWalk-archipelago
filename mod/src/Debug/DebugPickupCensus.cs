using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // The census for the 0.4 locations (ROADMAP S1, S10): every backpack, belt (HolsterProp) and
    // gourd carton of the island, and everything that could be a firework launcher, written to
    // BepInEx/pickup-census.tsv in one press (Ctrl + Keypad 1), from anywhere: the game builds
    // every prop everywhere (2026-09-21), hidden ones included.
    //
    // For each one: its guid (the game's own identity for a prop, which a location can be keyed
    // on), where it is, the nearest puzzle (to give it a region), and which versions of the map
    // it belongs to. The map has a version per player count (PlayerCountSwapper: target2, target3,
    // target4, one switched on at load) and more players get fewer tools, so a location must only
    // count on an object that every version has: "2,3,4" in the `versions` column.
    internal static class DebugPickupCensus
    {
        private const string Tag = "[" + nameof(DebugPickupCensus) + "]";

        // The flare guns too (flare_gun_sanity, player 2026-10-07): FlareGunProp, ...Blue, ...Green, ...Yellow.
        private static readonly string[] PackPrefabs = { "BackpackProp", "HolsterProp", "GourdCartonProp", "FlareGunProp" };

        // Names a firework launcher might carry: the game's class list names none, so the scene's
        // object names are all there is (ROADMAP S10).
        private static readonly string[] FireworkHints = { "firework", "rocket", "launcher", "pyro", "flare station", "flarestation" };

        internal static void Dump()
        {
            try
            {
                var versions = VersionsByObject();
                var text = new StringBuilder();
                text.AppendLine($"# pickup census, map version now: {PlayerCountSwapper.playerCount}");
                text.AppendLine("kind\tname\tguid\tx\ty\tz\tactive\tversions\tnearest puzzle\tdistance\tpath");

                var packs = 0;
                var colours = new StringBuilder();
                colours.AppendLine("# the colours each pack carries: every PropertyBlockHelper colour (path:property=#RRGGBB), then each renderer's material");
                foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop))
                        continue;

                    var name = prop.gameObject.name;
                    var prefab = Array.Find(PackPrefabs, p => name.StartsWith(p, StringComparison.Ordinal));
                    if (prefab == null || name.Contains("(AP", StringComparison.Ordinal))
                        continue;

                    packs++;
                    text.AppendLine(Row(prefab, prop.transform, prop.savablePropGuid, versions));
                    colours.AppendLine($"{prefab}	{prop.savablePropGuid}	{ColoursOf(prop.gameObject)}");
                }

                var fireworks = 0;
                var seen = new HashSet<int>();
                foreach (var launch in UnityEngine.Object.FindObjectsByType<PeckEffectLaunchProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (launch == null || !seen.Add(launch.gameObject.GetInstanceID()))
                        continue;
                    fireworks++;
                    var state = launch.trackedStateSystem;
                    text.AppendLine(Row("LaunchProp", launch.transform,
                        state != null ? $"state {state.gameObject.name} ({state.savableSystem})={state.currentPeckContext.state} saveIdentity={(state.saveIdentity != null)}" : "no state", versions));
                }

                foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (transform == null || !seen.Add(transform.gameObject.GetInstanceID()))
                        continue;

                    var lower = transform.name.ToLowerInvariant();
                    if (Array.Exists(FireworkHints, hint => lower.Contains(hint)))
                    {
                        fireworks++;
                        text.AppendLine(Row("NameHint", transform, Components(transform.gameObject), versions));
                    }
                }

                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "pickup-colours.tsv"), colours.ToString());

                var path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "pickup-census.tsv");
                System.IO.File.WriteAllText(path, text.ToString());
                Plugin.Log.LogInfo($"{Tag} {packs} pack(s) and {fireworks} firework candidate(s) written to {path}.");
                Core.Net.ApNotices.Post($"Census: {packs} packs, {fireworks} firework candidates");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Census failed: {ex}");
            }
        }

        private static int _nextSpot;

        // Ctrl + F3: the local player to the next pack or firework launcher, in turn, beside it.
        // For walking every one of the 20 checks without crossing the island on foot.
        internal static void TeleportNext()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
                return;

            var spots = new List<KeyValuePair<string, Vector3>>();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop))
                    continue;
                var name = prop.gameObject.name;
                if (name.Contains("(AP", StringComparison.Ordinal)
                    || !Array.Exists(PackPrefabs, p => name.StartsWith(p, StringComparison.Ordinal)))
                    continue;
                spots.Add(new KeyValuePair<string, Vector3>($"{name} {prop.savablePropGuid}", prop.transform.position));
            }

            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.name.StartsWith("FireworkLauncher", StringComparison.Ordinal))
                    spots.Add(new KeyValuePair<string, Vector3>(
                        $"FireworkLauncher in {transform.parent?.name}/{transform.parent?.parent?.name}", transform.position));
            }

            if (spots.Count == 0)
                return;

            spots.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var spot = spots[_nextSpot % spots.Count];
            _nextSpot++;
            var landing = Traps.Ground(spot.Value + Vector3.back * 2f);
            BigWalkArchipelago.Core.WorldButtons.TeleportTo(player, landing, Quaternion.identity);
            Plugin.Log.LogInfo($"{Tag} Teleported to {spot.Key} ({_nextSpot}/{spots.Count}) at {spot.Value.ToString("F1")}.");
            Core.Net.ApNotices.Post($"Teleport {_nextSpot}/{spots.Count}: {spot.Key.Split(' ')[0]}");
        }

        // For every object under a PlayerCountSwapper's targets, the versions it belongs to. An
        // object under no swapper is in every version.
        private static Dictionary<int, string> VersionsByObject()
        {
            var result = new Dictionary<int, string>();
            foreach (var swapper in UnityEngine.Object.FindObjectsByType<PlayerCountSwapper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (swapper == null)
                    continue;
                Mark(result, swapper.target2, "2");
                Mark(result, swapper.target3, "3");
                Mark(result, swapper.target4, "4");
            }

            return result;
        }

        private static void Mark(Dictionary<int, string> result, GameObject target, string version)
        {
            if (target == null)
                return;

            foreach (var transform in target.GetComponentsInChildren<Transform>(true))
            {
                var id = transform.gameObject.GetInstanceID();
                result[id] = result.TryGetValue(id, out var before) && !before.Contains(version) ? before + "," + version : version;
            }
        }

        private static string VersionsOf(Transform transform, Dictionary<int, string> versions)
        {
            return versions.TryGetValue(transform.gameObject.GetInstanceID(), out var found) ? found : "2,3,4";
        }

        private static string Row(string kind, Transform transform, string detail, Dictionary<int, string> versions)
        {
            var p = transform.position;
            var nearest = NearestPuzzle(p, out var distance);
            return string.Join("\t",
                kind,
                transform.name,
                detail ?? string.Empty,
                p.x.ToString("F1", CultureInfo.InvariantCulture),
                p.y.ToString("F1", CultureInfo.InvariantCulture),
                p.z.ToString("F1", CultureInfo.InvariantCulture),
                transform.gameObject.activeInHierarchy ? "active" : "inactive",
                VersionsOf(transform, versions),
                nearest ?? "-",
                distance.ToString("F0", CultureInfo.InvariantCulture),
                PathOf(transform));
        }

        private static string ColoursOf(GameObject root)
        {
            var parts = new List<string>();
            foreach (var helper in root.GetComponentsInChildren<PropertyBlockHelper>(true))
            {
                if (helper == null || helper.colorSettings == null)
                    continue;

                foreach (var setting in helper.colorSettings)
                    parts.Add($"{helper.gameObject.name}:{setting.propertyName}=#{ColorUtility.ToHtmlStringRGB(setting.color)}");
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var material = renderer.sharedMaterial;
                parts.Add($"{renderer.gameObject.name}[{(renderer.gameObject.activeSelf ? "on" : "off")}]:{(material != null ? material.name : "-")}");
            }

            return string.Join("  ", parts);
        }

        private static string Components(GameObject gameObject)
        {
            var names = new List<string>();
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component != null)
                    names.Add(component.GetIl2CppType().Name);
            }

            return string.Join(",", names);
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string NearestPuzzle(Vector3 position, out float nearestDistance)
        {
            string nearest = null;
            nearestDistance = float.MaxValue;
            foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                    continue;

                var distance = Vector3.Distance(position, prop.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = prop.saveablePropName.ToString();
                }
            }

            return nearest;
        }
    }
}
