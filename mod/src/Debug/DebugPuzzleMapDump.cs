using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Full world-space coordinates for every puzzle and every big-key
    // plinth, for building an accurate map — mapgenie.io's names are
    // suspected wrong (player, 2026-09-28) and this is the only source that
    // cannot be.
    //
    // Safe to run in one press wherever the player is standing: confirmed
    // by DebugGourdLookup.LogAllLoaded (2026-09-21) that every RewardGourd
    // is loaded everywhere, not streamed by zone — two dumps taken in two
    // different places came back byte-identical. Auto-run rather than a
    // hotkey for the same reason DebugWorldLayoutDump is: nothing here
    // depends on where the player walks.
    internal class DebugPuzzleMapDump : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public DebugPuzzleMapDump(IntPtr ptr) : base(ptr)
        {
        }

        private const float DelayAfterReadySeconds = 8f;

        private float _readySince = -1f;
        private bool _done;

        private void Update()
        {
            if (!WorldManager.isReadyForEffects)
            {
                _readySince = -1f;
                _done = false;
                return;
            }

            if (_done)
                return;

            if (_readySince < 0f)
                _readySince = Time.time;

            if (Time.time - _readySince < DelayAfterReadySeconds)
                return;

            _done = true;
            try
            {
                Dump();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPuzzleMapDump)}] Dump failed: {ex.Message}");
            }
        }

        private static void Dump()
        {
            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var rows = new List<(string propName, Vector3 position)>();
            if (gourds != null)
            {
                foreach (var gourd in gourds)
                {
                    var prop = gourd != null ? gourd.prop : null;
                    if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                        continue;

                    rows.Add((prop.saveablePropName.ToString(), prop.transform.position));
                }
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPuzzleMapDump)}] {rows.Count} puzzle(s):");
            foreach (var row in rows.OrderBy(r => r.propName, StringComparer.Ordinal))
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPuzzleMapDump)}]   puzzle {row.propName} "
                    + $"x={row.position.x:F1} y={row.position.y:F1} z={row.position.z:F1}");
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPuzzleMapDump)}] {GourdRegistry.BigKeys.Count()} plinth(s):");
            foreach (var propName in GourdRegistry.BigKeys.OrderBy(name => name.ToString(), StringComparer.Ordinal))
            {
                var home = GourdRegistry.TryGetHomeFor(propName);
                var position = home != null ? home.transform.position.ToString("F1") : "<not loaded>";
                Plugin.Log.LogInfo($"[{nameof(DebugPuzzleMapDump)}]   plinth {propName} {position}");
            }
        }
    }
}
