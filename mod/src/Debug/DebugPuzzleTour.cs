using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // A tour of the puzzles by teleport (the player, 2026-10-12), to say puzzle by puzzle what each one
    // needs of the player's body (R3 `lock_abilities`) and of the map, per player count. Ctrl + Shift + 3
    // goes to the next puzzle, Ctrl + Shift + 4 back to the previous one, in the apworld's order (the
    // gourds' enum values). Lands two metres above the puzzle's gourd, which sits in or beside its
    // puzzle; the overlay and the log say which puzzle it is and which map version is loaded.
    internal class DebugPuzzleTour : MonoBehaviour
    {
        private const string Tag = "[" + nameof(DebugPuzzleTour) + "]";

        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut NextKey =
            new(KeyCode.Alpha3, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut PreviousKey =
            new(KeyCode.Alpha4, KeyCode.LeftControl, KeyCode.LeftShift);

        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut ObjectCensusKey =
            new(KeyCode.Alpha5, KeyCode.LeftControl, KeyCode.LeftShift);

        // S9 recon (player, 2026-10-13): every lamp pedestal of the island, in turn.
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut NextPedestalKey =
            new(KeyCode.Alpha6, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut PreviousPedestalKey =
            new(KeyCode.Alpha7, KeyCode.LeftControl, KeyCode.LeftShift);

        private int _pedestal = -1;


        private int _index = -1;

        public DebugPuzzleTour(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            try
            {
                if (ObjectCensusKey.IsDown() && WorldManager.isReadyForEffects)
                    DebugPickupCensus.DumpObjects();

                var pedestalStep = NextPedestalKey.IsDown() ? 1 : PreviousPedestalKey.IsDown() ? -1 : 0;
                if (pedestalStep != 0 && WorldManager.isReadyForEffects)
                    VisitPedestal(pedestalStep);

                var step = NextKey.IsDown() ? 1 : PreviousKey.IsDown() ? -1 : 0;
                if (step == 0 || !WorldManager.isReadyForEffects)
                    return;

                var stops = Stops();
                if (stops.Count == 0)
                {
                    ApNotices.Post("Tour: no puzzle found", warning: true);
                    return;
                }

                _index = ((_index + step) % stops.Count + stops.Count) % stops.Count;
                var (name, position) = stops[_index];
                var local = Mirror.NetworkClient.localPlayer;
                var player = local != null ? local.GetComponent<PlayerCharacter>() : null;
                WorldButtons.TeleportTo(player, position + Vector3.up * 2f, Quaternion.identity);

                var line = $"Tour {_index + 1}/{stops.Count}: {name} ({PlayerCountSwapper.playerCount} players map)";
                ApNotices.Post(line);
                Plugin.Log.LogInfo($"{Tag} {line} at {position}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex.Message}");
            }
        }

        private void VisitPedestal(int step)
        {
            var pedestals = new List<PropHome>();
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null)
                    continue;
                var path = PathOf(candidate.transform);
                if (path.IndexOf("pedestal", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("pedistal", StringComparison.OrdinalIgnoreCase) >= 0)
                    pedestals.Add(candidate);
            }

            if (pedestals.Count == 0)
            {
                ApNotices.Post("Pedestals: none found", warning: true);
                return;
            }

            pedestals.Sort((a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            _pedestal = ((_pedestal + step) % pedestals.Count + pedestals.Count) % pedestals.Count;
            var home = pedestals[_pedestal];
            var local = Mirror.NetworkClient.localPlayer;
            var player = local != null ? local.GetComponent<PlayerCharacter>() : null;
            var at = home.transform.position;
            WorldButtons.TeleportTo(player, at + Vector3.back * 1.5f + Vector3.up * 1f, Quaternion.identity);

            var holds = home.pinnedProp != null ? home.pinnedProp.gameObject.name : "EMPTY";
            var where = PathOf(home.transform);
            var line = $"Pedestal {_pedestal + 1}/{pedestals.Count}: {holds} ({home.gameObject.activeInHierarchy switch { true => "active", false => "inactive" }})";
            ApNotices.Post(line);
            Plugin.Log.LogInfo($"{Tag} {line} at {at} path={where}");
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        // Every puzzle's gourd that is a location, in enum order.
        private static List<(string Name, Vector3 Position)> Stops()
        {
            var stops = new List<(int Value, string Name, Vector3 Position)>();
            foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                    continue;

                stops.Add(((int)prop.saveablePropName, prop.saveablePropName.ToString(), prop.transform.position));
            }

            return stops.OrderBy(s => s.Value).Select(s => (s.Name, s.Position)).ToList();
        }
    }
}
