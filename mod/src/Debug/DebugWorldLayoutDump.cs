using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Where things are, for the logic of `lock_arch_doors` (2026-09-25).
    //
    // Two questions only the running game can answer: which four puzzles sit
    // in the starting zone (the player remembers them by what they do, not by
    // name), and which of the three arch doors is the left one, towards the
    // Green Dome, and which the right one, towards the Red Tower. Every
    // puzzle gourd is listed by its distance to the tutorial's monument, and
    // each door by its distance to the tutorial monument and both towers'
    // plinths. Once per loaded world, when Debug is enabled.
    internal class DebugWorldLayoutDump : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public DebugWorldLayoutDump(IntPtr ptr) : base(ptr)
        {
        }

        private const float DelayAfterReadySeconds = 8f;

        private static readonly SavableSystem[] ArchDoors =
        {
            SavableSystem.SpawnHubGate,
            SavableSystem.HubTunnel,
            SavableSystem.HubShortcutToSportsCreek,
        };

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
                Plugin.Log.LogWarning($"[{nameof(DebugWorldLayoutDump)}] Dump failed: {ex.Message}");
            }
        }

        private static void Dump()
        {
            var tutorial = GourdRegistry.TryGetHome(SaveableHomeName.monoumentIntroSlot0);
            var red = GourdRegistry.TryGetHome(SaveableHomeName.bigKeyPlinthMapRoom);
            var dome = GourdRegistry.TryGetHome(SaveableHomeName.bigKeyPlinthGoodbye2);
            Plugin.Log.LogInfo(
                $"[{nameof(DebugWorldLayoutDump)}] tutorial monument {Where(tutorial)}, red plinth {Where(red)}, "
                + $"green dome plinth {Where(dome)}");

            if (tutorial == null)
                return;

            var origin = tutorial.transform.position;
            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rows = new List<(float distance, string line)>();
            if (gourds != null)
            {
                foreach (var gourd in gourds)
                {
                    var prop = gourd != null ? gourd.prop : null;
                    if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop)
                        || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                        continue;

                    var distance = Vector3.Distance(origin, gourd.transform.position);
                    rows.Add((distance, $"{prop.saveablePropName} {distance:F0}m (state {gourd.gourdState})"));
                }
            }

            foreach (var row in rows.OrderBy(r => r.distance))
                Plugin.Log.LogInfo($"[{nameof(DebugWorldLayoutDump)}]   puzzle {row.line}");

            var states = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (states == null)
                return;

            foreach (var state in states)
            {
                if (state == null || Array.IndexOf(ArchDoors, state.savableSystem) < 0)
                    continue;

                var p = state.transform.position;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugWorldLayoutDump)}]   door {state.savableSystem} at {p}"
                    + $" | tutorial {Vector3.Distance(p, origin):F0}m"
                    + (red != null ? $" | red plinth {Vector3.Distance(p, red.transform.position):F0}m" : "")
                    + (dome != null ? $" | green dome plinth {Vector3.Distance(p, dome.transform.position):F0}m" : "")
                    + $" | state {state.currentPeckContext.state}");
            }
        }

        private static string Where(PropHome home)
        {
            return home != null ? home.transform.position.ToString() : "<not loaded>";
        }
    }
}
