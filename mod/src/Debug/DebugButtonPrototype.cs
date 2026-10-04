using System;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // A throwaway experiment for ROADMAP U4/U10, now on the mod's own buttons
    // (Core/WorldButtons): one press of the key puts a button two metres in front of the
    // player, and clicking it teleports the player to the hub's spawn point.
    //
    // The first version copied one of the game's buttons and was dropped: the copy kept the
    // original's network reference and every press went to the original.
    internal static class DebugButtonPrototype
    {
        // Where a player stands when a world has just loaded at the hub, measured on
        // 2026-10-03 (SystemWrites logged it at every start).
        private static readonly Vector3 HubSpawn = new Vector3(-242.3f, 37.0f, -484.7f);

        // Each press of the key tries the next icon, so one session shows them all.
        private static int _iconTest;
        private static readonly string[] Icons = { "red", "green", "blue", "yellow", "black", "hub", "gauntlet" };

        private static string IconForNextTest() => Icons[_iconTest++ % Icons.Length];

        // Puts the button on the wall the player is looking at, facing out of it, as a mounted one
        // is. With no wall in reach it hangs at hand height in front of the player.
        internal static void SpawnInFrontOfPlayer()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugButtonPrototype)}] Local player not found.");
                return;
            }

            if (TryAimedSurface(player, out var point, out var normal))
            {
                WorldButtons.Add(DebugSlot, "Back to the hub", point + normal * 0.02f, Quaternion.LookRotation(normal, Vector3.up),
                    presser => WorldButtons.TeleportTo(presser, HubSpawn, Quaternion.identity), IconForNextTest());
                return;
            }

            var forward = player.transform.forward;
            forward.y = 0f;
            var position = player.transform.position + forward.normalized * 2.5f + Vector3.up * 1.2f;
            WorldButtons.Add(DebugSlot, "Back to the hub", position, Quaternion.LookRotation(-forward.normalized, Vector3.up),
                presser => WorldButtons.TeleportTo(presser, HubSpawn, Quaternion.identity));
        }

        // Writes the spot the player is looking at, a point on a surface and the way the surface
        // faces, to a file: how the real buttons get their places, without guessing coordinates.
        private const int DebugSlot = 31;

        internal static void MarkAimedSpot()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null || !TryAimedSurface(player, out var point, out var normal))
            {
                Plugin.Log.LogInfo($"[{nameof(DebugButtonPrototype)}] Nothing to mark: no surface within reach of the crosshair.");
                return;
            }

            var line = $"{DateTime.Now:HH:mm:ss} point {point.x:F2} {point.y:F2} {point.z:F2} normal {normal.x:F2} {normal.y:F2} {normal.z:F2}";
            Plugin.Log.LogInfo($"[{nameof(DebugButtonPrototype)}] Marked: {line}");
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "world-button-spots.txt"), line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugButtonPrototype)}] Could not write the spot: {ex.Message}");
            }
        }

        // What the Black Tower's panel of tower icons is made of, to copy them as decoration
        // for the teleport buttons: every renderer under an `EndingGateIndicator`, with its mesh,
        // material, size and place, so the icon of each tower can be told from the others. Pure
        // diagnostic, written to the log and to BepInEx/indicator-dump.txt.
        internal static void DumpIndicators()
        {
            var text = new System.Text.StringBuilder();
            var count = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;

                var path = "";
                var inIndicator = false;
                for (var t = renderer.transform; t != null; t = t.parent)
                {
                    path = t.name + "/" + path;
                    if (t.name.StartsWith("EndingGateIndicator", StringComparison.Ordinal))
                        inIndicator = true;
                }

                if (!inIndicator)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "<no mesh>";
                var materials = new System.Collections.Generic.List<string>();
                foreach (var material in renderer.sharedMaterials)
                    materials.Add(material != null ? material.name : "<null>");

                var p = renderer.transform.position;
                var scale = renderer.transform.lossyScale;
                text.AppendLine(
                    $"{path.TrimEnd('/')} | mesh={mesh} | materials={string.Join(",", materials)} "
                    + $"| pos=({p.x:F2},{p.y:F2},{p.z:F2}) | scale=({scale.x:F2},{scale.y:F2},{scale.z:F2}) "
                    + $"| active={renderer.gameObject.activeInHierarchy}");
                count++;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugButtonPrototype)}] {count} indicator renderer(s) written to indicator-dump.txt.");
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "indicator-dump.txt"), text.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugButtonPrototype)}] Could not write the dump: {ex.Message}");
            }
        }

        // The first surface along the view that is not the player's own body.
        private static bool TryAimedSurface(PlayerCharacter player, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            var camera = Camera.main;
            if (camera == null)
                return false;

            var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 8f);
            var bestDistance = float.MaxValue;
            var found = false;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.GetComponentInParent<PlayerCharacter>() != null)
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                point = hit.point;
                normal = hit.normal;
                found = true;
            }

            return found;
        }
    }
}
