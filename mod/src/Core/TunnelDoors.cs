using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // tunnel_sanity: each door item opens its GateLogic states; the host keeps them open.
    internal static class TunnelDoors
    {
        private const string Tag = "[" + nameof(TunnelDoors) + "]";
        private const string KeyPrefix = "ap_tunnel_";

        private static readonly Dictionary<long, string[]> Gates = new();
        private static readonly Dictionary<string, TrackedPeckState> Found = new();

        internal static bool IsItem(long itemId) => Gates.ContainsKey(itemId);

        // Host: slot_data has arrived, after the ledger was cleared for a rebound save.
        internal static void Configure(Dictionary<long, string[]> gates)
        {
            Gates.Clear();
            Found.Clear();
            foreach (var pair in gates)
                Gates[pair.Key] = pair.Value;

            if (Gates.Count > 0)
                Plugin.Log.LogInfo($"{Tag} tunnel_sanity on: {Gates.Count} door item(s).");
        }

        internal static bool Grant(long itemId)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"{Tag} Ignored: tunnel door {itemId} received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + itemId, 1);
            Plugin.Log.LogInfo($"{Tag} Door {itemId} granted.");
            Enforce();
            return true;
        }

        internal static void ClearLedger()
        {
            foreach (var itemId in Gates.Keys)
            {
                if (IsGranted(itemId))
                    SaveManager.SetIntValue(KeyPrefix + itemId, 0);
            }
        }

        // Every couple of seconds (TeleportButtonRunner): opens the gates of every door granted.
        internal static void Enforce()
        {
            if (Gates.Count == 0 || !NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            foreach (var pair in Gates)
            {
                if (!IsGranted(pair.Key))
                    continue;

                foreach (var path in pair.Value)
                {
                    var gate = FindGate(path);
                    if (gate == null || gate.currentPeckContext.state >= 1)
                        continue;

                    gate.SetState(1);
                    Plugin.Log.LogInfo($"{Tag} {path} opened.");
                }
            }
        }

        private static bool IsGranted(long itemId) => SaveManager.GetIntValue(KeyPrefix + itemId, 0, false) != 0;

        private static TrackedPeckState FindGate(string path)
        {
            if (Found.TryGetValue(path, out var cached) && cached != null)
                return cached;

            var holder = GameObject.Find(path);
            var gate = holder != null ? holder.GetComponent<TrackedPeckState>() : null;
            if (gate != null)
                Found[path] = gate;
            return gate;
        }
    }
}
