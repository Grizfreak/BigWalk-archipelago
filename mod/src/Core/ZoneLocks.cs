using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Zone locks (ROADMAP R12, 0.7.1): the island's gadgets by the zone of the nearest coloured tower, each zone's an
    // item (apworld `zone_locks`). Until a zone's item arrives, its gadgets are off every machine's map; the lamps and
    // light switches are only out of logic, so nothing here touches them.
    //
    // The same shape as PuzzleNeeds: only the host has slot_data and the items, keeps what arrived in the save
    // (`ap_zone_<item id>`, items already applied are not replayed) and tells the guests the gadgets still locked in
    // its snapshot (ModChannel); every machine hides its own copies, checked again every second since Mirror switches a
    // scene object back on when it spawns it.
    internal static class ZoneLocks
    {
        private const string Tag = "[" + nameof(ZoneLocks) + "]";
        private const string KeyPrefix = "ap_zone_";

        private static bool _enabled;
        private static readonly Dictionary<long, string[]> Gadgets = new();

        // The guids locked right now: the host's from the ledger, a guest's as the host says.
        private static readonly HashSet<string> Locked = new();
        private static bool _mirrored;

        // What this machine switched off, so only those are switched back on.
        private static readonly Dictionary<string, GameObject> Hidden = new();
        private static Dictionary<string, Prop> _props;
        private static bool _worldWasReady;
        private static float _nextTick;
        private static float _nextRefresh;

        internal static IEnumerable<string> HostLocked => _enabled ? Locked : Enumerable.Empty<string>();

        internal static bool IsItem(long itemId) => _enabled && Gadgets.ContainsKey(itemId);

        // Host: slot_data has arrived, after the ledger was cleared for a rebound save.
        internal static void Configure(bool enabled, Dictionary<long, string[]> gadgets)
        {
            _enabled = enabled && gadgets.Count > 0;
            Gadgets.Clear();
            if (_enabled)
            {
                foreach (var pair in gadgets)
                    Gadgets[pair.Key] = pair.Value;
            }

            Rearm();
            if (_enabled)
                Plugin.Log.LogInfo($"{Tag} zone_locks on: {Locked.Count} gadget(s) off the map until their zone arrives "
                                   + $"({Gadgets.Keys.Count(id => !IsGranted(id))} of {Gadgets.Count} zones locked).");
        }

        // What is locked is what the ledger says.
        private static void Rearm()
        {
            if (_mirrored)
                return;

            Locked.Clear();
            foreach (var pair in Gadgets)
            {
                if (IsGranted(pair.Key))
                    continue;
                foreach (var guid in pair.Value)
                    Locked.Add(guid);
            }
        }

        internal static bool Grant(long itemId)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"{Tag} Ignored: zone item {itemId} received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + itemId, 1);
            Rearm();
            Plugin.Log.LogInfo($"{Tag} zone item {itemId} granted: {Gadgets[itemId].Length} gadget(s) back on the map.");
            SessionJournal.Write("zone", $"{itemId} granted");
            ModChannelNotify();
            return true;
        }

        private static void ModChannelNotify() => Net.ModChannel.SendSnapshotNow();

        private static bool IsGranted(long itemId) => SaveManager.GetIntValue(KeyPrefix + itemId, 0, false) != 0;

        // A save rebound to another seed replays every item from scratch.
        internal static void ClearLedger()
        {
            foreach (var itemId in Gadgets.Keys)
            {
                if (IsGranted(itemId))
                    SaveManager.SetIntValue(KeyPrefix + itemId, 0);
            }
        }

        // Guest: the host's snapshot says which gadgets are locked right now.
        internal static void ApplyFromHost(List<string> locked)
        {
            if (!_mirrored && locked.Count == 0)
                return;

            _mirrored = true;
            if (Locked.SetEquals(locked))
                return;

            Locked.Clear();
            foreach (var guid in locked)
                Locked.Add(guid);
            Plugin.Log.LogInfo($"{Tag} The host says {Locked.Count} gadget(s) locked by zone.");
        }

        internal static void ForgetMirror()
        {
            if (!_mirrored)
                return;

            _mirrored = false;
            Locked.Clear();
        }

        // Every frame (Core/Traps), every machine; the work once a second.
        internal static void Tick()
        {
            if (Time.unscaledTime < _nextTick)
                return;
            _nextTick = Time.unscaledTime + 1f;

            var ready = WorldManager.isReadyForEffects;
            if (!ready)
            {
                if (_worldWasReady)
                {
                    Hidden.Clear();
                    _props = null;
                }

                _worldWasReady = false;
                return;
            }

            if (!_worldWasReady)
            {
                _worldWasReady = true;
                Rearm();
            }

            if (Locked.Count == 0 && Hidden.Count == 0)
                return;

            // Read again, every five seconds at most, while a locked gadget is missing (a guest's arrive late).
            if (_props == null || Time.unscaledTime >= _nextRefresh && Locked.Any(guid => !_props.ContainsKey(guid)))
            {
                _props = IslandGadgets();
                _nextRefresh = Time.unscaledTime + 5f;
            }

            foreach (var guid in Locked)
            {
                if (!_props.TryGetValue(guid, out var prop) || prop == null || prop.gameObject == null)
                    continue;
                if (!prop.gameObject.activeSelf)
                {
                    Hidden.TryAdd(guid, prop.gameObject);
                    continue;
                }

                try
                {
                    prop.gameObject.SetActive(false);
                    Hidden[guid] = prop.gameObject;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not hide a gadget: {ex.Message}");
                }
            }

            if (Hidden.Count == 0)
                return;

            var shown = 0;
            foreach (var guid in Hidden.Keys.Where(guid => !Locked.Contains(guid)).ToList())
            {
                var gadget = Hidden[guid];
                Hidden.Remove(guid);
                // Not one picked up for its check, nor a kind object_sanity takes off the island.
                if (gadget == null || PackChecks.IsCollected(guid) || !PackChecks.KeepsObjectOnMap(gadget.name))
                    continue;

                try
                {
                    gadget.SetActive(true);
                    shown++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not show a gadget: {ex.Message}");
                }
            }

            if (shown > 0)
                Plugin.Log.LogInfo($"{Tag} {shown} gadget(s) back on the map: their zone arrived.");
        }

        // The island's own gadgets by guid (not the mod's clones, which copy the guid).
        private static Dictionary<string, Prop> IslandGadgets()
        {
            var props = new Dictionary<string, Prop>();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || prop.gameObject == null || ReceivedItemSpawner.IsCosmeticClone(prop)
                    || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    continue;
                var guid = prop.savablePropGuid;
                if (!string.IsNullOrEmpty(guid))
                    props.TryAdd(guid, prop);
            }

            return props;
        }
    }
}
