using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Pure diagnostic (no writes), for scene mechanisms with no dedicated C#
    // class findable by name in il2cpp.cs — e.g. the "Arch doors"/shortcut
    // buttons from the third-party document (cf.
    // big-walk-archipelago-notes.md), unfindable statically (no "Arch"/
    // "Shortcut" class in the dump, and `strings` on GameAssembly.dll doesn't
    // surface any "Arch" identifier on the game side either). Probably, like
    // the big-key plinths, generic scene wiring (Peck system) rather than a
    // dedicated class — this tool lists, in-game, nearby GameObjects whose
    // name or a component matches a keyword, along with their component
    // list, in order to identify the real mechanism without blindly guessing
    // via Ghidra.
    //
    // Extended on 2026-09-11 (hierarchy path + savableSystem) for the hub's
    // black sphere: the F9 dump from 2026-09-10 revealed
    // "Spawn_SecondEnding_Sphere_Whole" (mesh+collider, 3.5m from the player
    // at the hub) and, further away (~21m), a cluster of "ModelOrb Colliders"/
    // _Wall*/"SecondEndingDoorLogic" (PeckEffectAnimancer) — so NOT a
    // mechanism entirely without a Peck as assumed on 2026-09-10 (cf.
    // big-walk-archipelago-notes.md), just not carried by the Sphere/_Wall
    // objects themselves. Key object still to confirm: "OpenSystem"
    // (TrackedPeckState+PeckSystemBlock+PeckBusConnection+2xPeckEffectToggle
    // +PeckEffectAudio, 4.8m away, right next to the sphere). Without the
    // hierarchy path we can't tell whether these objects belong to the same
    // scene assembly — hence the addition below.
    internal static class DebugComponentLookup
    {
        private static readonly string[] DefaultKeywords = { "arch", "door", "switch", "button", "gate", "peck", "shortcut", "bell", "chime", "gong", "cowbell", "sphere", "orb", "void", "block", "barrier", "wall", "unlock", "skip", "proven", "reward", "postgame", "complet", "second", "ending", "cosmetic" };

        internal static void DumpNearbyByKeyword(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var entries = new List<(GameObject go, float sqrDistance, string components)>();

            foreach (var t in allTransforms)
            {
                if (t == null)
                    continue;

                var sqrDistance = (t.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                var go = t.gameObject;
                var components = DescribeComponents(go);
                if (!Matches(go.name, components))
                    continue;

                entries.Add((go, sqrDistance, components));
            }

            entries.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}] {entries.Count} matching object(s) within a radius of {radius}m:");
            foreach (var entry in entries)
            {
                var distance = Math.Sqrt(entry.sqrDistance).ToString("F1");
                var path = DescribePath(entry.go.transform);
                Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}]   {entry.go.name} — distance={distance}m — path={path} — components: {entry.components}");

                var trackedState = entry.go.GetComponent<TrackedPeckState>();
                if (trackedState != null)
                    DescribeTrackedPeckState(trackedState);
            }
        }

        private static string DescribePath(Transform t)
        {
            var names = new List<string>();
            var current = t.parent;
            var guard = 0;
            while (current != null && guard++ < 20)
            {
                names.Insert(0, current.name);
                current = current.parent;
            }

            return names.Count == 0 ? "<root>" : string.Join("/", names);
        }

        private static void DescribeTrackedPeckState(TrackedPeckState state)
        {
            try
            {
                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<no SaveIdentity>";
                var savableSystem = state.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugComponentLookup)}]     TrackedPeckState — label='{state.label}' — savableSystem={savableSystem} — " +
                    $"saveGuid={saveGuid} — actual key='{key}' — SaveManager[{key}]={currentValue}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugComponentLookup)}]     TrackedPeckState — exception while reading, ignored: {ex.Message}");
            }
        }

        private static string DescribeComponents(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var names = new StringBuilder();
            foreach (var c in components)
            {
                if (c == null)
                    continue;
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(c.GetIl2CppType().Name);
            }

            return names.ToString();
        }

        private static bool Matches(string goName, string components)
        {
            foreach (var kw in DefaultKeywords)
            {
                if (goName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (components.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
