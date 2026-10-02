using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // One-shot "where am I" for labelling puzzles by what they actually
    // need (a sound cue, a plate, a timer, a worn item...) against the
    // island plot DebugPuzzleMapDump produces — player request, 2026-09-28:
    // standing at a puzzle and reading its own coordinates off a map by eye
    // is slower and less reliable than the game just saying which one this is.
    internal static class DebugPositionPing
    {
        internal static void Log()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPositionPing)}] No local player found.");
                return;
            }

            var position = player.transform.position;

            string nearestLabel = null;
            var nearestDistance = float.MaxValue;

            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gourds != null)
            {
                foreach (var gourd in gourds)
                {
                    var prop = gourd != null ? gourd.prop : null;
                    if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                        continue;

                    var distance = Vector3.Distance(position, prop.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestLabel = $"puzzle {prop.saveablePropName}";
                    }
                }
            }

            foreach (var propName in GourdRegistry.BigKeys)
            {
                var home = GourdRegistry.TryGetHomeFor(propName);
                if (home == null)
                    continue;

                var distance = Vector3.Distance(position, home.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestLabel = $"plinth {propName}";
                }
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugPositionPing)}] player x={position.x:F1} y={position.y:F1} z={position.z:F1} | "
                + (nearestLabel != null
                    ? $"nearest {nearestLabel} at {nearestDistance:F1}m"
                    : "nothing known nearby"));
        }
    }
}
