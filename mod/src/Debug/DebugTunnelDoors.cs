using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Tunnel sanity recon: lists the states under TunnelSystem and opens one at a time.
    internal static class DebugTunnelDoors
    {
        private static float _nextRequestCheck;

        // BepInEx/tunnel-dump.request dumps; tunnel-open.request holds a path suffix to open.
        internal static void TickRequests()
        {
            if (Time.unscaledTime < _nextRequestCheck)
                return;
            _nextRequestCheck = Time.unscaledTime + 2f;
            if (!WorldManager.isReadyForEffects)
                return;

            var dump = System.IO.Path.Combine(Paths.BepInExRootPath, "tunnel-dump.request");
            if (File.Exists(dump))
            {
                File.Delete(dump);
                Dump();
            }

            var open = System.IO.Path.Combine(Paths.BepInExRootPath, "tunnel-open.request");
            if (File.Exists(open))
            {
                var suffix = File.ReadAllText(open).Trim();
                File.Delete(open);
                Open(suffix);
            }
        }

        private static void Dump()
        {
            var text = new StringBuilder();
            text.AppendLine("== TrackedPeckState ==");
            foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var path = PathOf(state.transform);
                if (!InTunnels(path))
                    continue;
                var guid = state.saveIdentity != null ? state.saveIdentity.saveGuid : "-";
                var saved = state.saveIdentity != null ? SaveManager.GetIntValue(guid, -1, false).ToString() : "-";
                var refs = state.systemRefences != null ? state.systemRefences.Count : -1;
                text.AppendLine($"{path}\tstate={state.currentPeckContext.state}\tlabel='{state.label}'\t"
                                + $"system={state.savableSystem}\tguid={guid}\tsaved={saved}\trefs={refs}\t"
                                + $"{(state.gameObject.activeInHierarchy ? "on" : "off")}");
            }

            text.AppendLine("== PeckRelay ==");
            foreach (var relay in UnityEngine.Object.FindObjectsByType<PeckRelay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var path = PathOf(relay.transform);
                var input = relay.inputStateSystem != null ? PathOf(relay.inputStateSystem.transform) : "-";
                var output = relay.outputStateSystem != null ? PathOf(relay.outputStateSystem.transform) : "-";
                if (!InTunnels(path) && !InTunnels(input) && !InTunnels(output))
                    continue;
                text.AppendLine($"{path}\tin={input}\tout={output}\tsync={relay.keepInSync}\tinitial={relay.onlyOnInitial}");
            }

            text.AppendLine("== Components ==");
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var name = transform.name;
                if (name != "OutletRelays" && name != "SouthPeakTunnelLogic" && name != "GateLogic"
                    && name != "BasicDoorPeckLogic" && name != "SouthPeakTunnel" && !name.StartsWith("TunnelOutlet"))
                    continue;
                var path = PathOf(transform);
                if (!InTunnels(path))
                    continue;
                var types = new List<string>();
                foreach (var component in transform.GetComponents<Component>())
                    types.Add(component == null ? "null" : component.GetIl2CppType().Name);
                text.AppendLine($"{path}\t[{string.Join(", ", types)}]");
            }

            var home = Core.GourdRegistry.TryGetHome(SaveableHomeName.bigKeyPlinthTunnels);
            var door = home != null ? home.pinDirectControlSystem : null;
            text.AppendLine($"== Yellow plinth door: {(door != null ? PathOf(door.transform) : "not loaded")}");

            var file = System.IO.Path.Combine(Paths.BepInExRootPath, "tunnel-dump.txt");
            File.WriteAllText(file, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugTunnelDoors)}] Written to {file}.");
        }

        private static void Open(string suffix)
        {
            var opened = 0;
            foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var path = PathOf(state.transform);
                if (!path.EndsWith(suffix))
                    continue;
                state.SetState(1);
                opened++;
                Plugin.Log.LogInfo($"[{nameof(DebugTunnelDoors)}] SetState(1) on {path}.");
            }

            if (opened == 0)
                Plugin.Log.LogInfo($"[{nameof(DebugTunnelDoors)}] No TrackedPeckState ends with '{suffix}'.");
        }

        private static bool InTunnels(string path)
        {
            return path.Contains("TunnelSystem") || path.Contains("Tunnel");
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var at = transform; at != null; at = at.parent)
                parts.Add(at.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
