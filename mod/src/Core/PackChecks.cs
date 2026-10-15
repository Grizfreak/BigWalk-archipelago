using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The packs and the fireworks as checks (ROADMAP S1, S10; slot options `pack_sanity`,
    // `firework_sanity`).
    //
    // Packs, the island's 6 backpacks, 5 belts and the gourd carton, by `pack_sanity`:
    //   off      hidden, like the island's other objects (GadgetItemSpawner): items only.
    //   on       on the map; the first pick-up of each is its location, told apart by its
    //            `savablePropGuid`, and the pack then disappears (player, 2026-10-05): the packs a
    //            player can wear stay the pool's items.
    //   vanilla  on the map as in the game, free, and no location (the apworld's `off`).
    //   self     on the map; the first pick-up of each is its location, whose item is the pack itself (the
    //            apworld's `vanilla`, ROADMAP S1): the pack stays in the player's hands, and the item, when it
    //            comes back from the server, spawns nothing (IsPackItself).
    // The pick-up is caught on the host, in the server half of the game's pick-up Command
    // (Patches/PackCheckPatches), so a guest's counts too. "Disappears" is each machine hiding its
    // own copy, like the island's hidden objects, never a network destroy (which broke the guests'
    // templates in September): the host sends the guests the packs collected, in the snapshot, and
    // every machine keeps them hidden, world reload or not.
    //
    // Fireworks: the eight launchers save nothing when fired, but each has a button at its foot
    // whose state goes to 1 (`.../FireworkLauncher/BasicPokeButton/PokeButtonPeckLogic`, measured
    // at the hub's on 2026-10-05). The launcher is named by the landmark it stands in, found among
    // its parents (`Hub`, `Lookout_RedCone`, ...), as the apworld lists them.
    //
    // A check is remembered in the save like every other (CheckTracker), so each reports once.
    internal static class PackChecks
    {
        private const string Tag = "[" + nameof(PackChecks) + "]";
        private const string LauncherName = "FireworkLauncher";

        internal const byte ModeOff = 0;
        internal const byte ModeOn = 1;
        internal const byte ModeVanilla = 2;
        internal const byte ModeSelf = 3;

        private static byte _mode;
        private static string[] _guids = Array.Empty<string>();
        private static bool _fireworkSanity;
        private static string[] _fireworkKeys = Array.Empty<string>();

        // The flare guns (`flare_gun_sanity`, player 2026-10-07): on the map whatever the option;
        // with it, the first pick-up of each is its location and the gun then disappears, as a
        // pack does, through the same Collected set and snapshot.
        private static bool _flareGunSanity;
        private static string[] _flareGunGuids = Array.Empty<string>();

        // The island's gadgets (`object_sanity`, ROADMAP S7, 0.6): with it on they stay on the map (the player,
        // 2026-10-12) and are never taken off it once picked up: `every` makes each one's first pick-up its
        // location (by guid), `first_pickup` the first of each kind (by the game's name for it).
        internal const byte ObjectsOff = 0;
        internal const byte ObjectsFirstPickup = 1;
        internal const byte ObjectsEvery = 2;
        private static byte _objectMode;
        private static string[] _objectGuids = Array.Empty<string>();
        private static string[] _objectKinds = Array.Empty<string>();
        private static byte _mirroredObjectMode;

        // The kinds counted (`object_sanity_kinds`), by the game's name: the others leave the island while
        // object_sanity is on (player, 2026-10-13). A guest is told them in the snapshot.
        private static readonly HashSet<string> CountedKinds = new();
        private static readonly HashSet<string> MirroredKinds = new();

        // Every kind object_sanity knows, the apworld's OBJECT_KINDS.
        internal static readonly string[] AllObjectKinds =
        {
            "WalkieTalkieProp", "MegaphoneProp", "FmRadioProp", "BinocularsProp", "LaserProp", "TorchProp",
            "XrayGogglesProp", "ClockProp", "CompassProp", "FoldingMapProp",
            "CowBellLargeProp", "CowBellMediumProp", "CowBellSmallProp", "SalonBrushProp", "BuoyProp", "BuoyRedProp", "BuoyLight",
        };

        // Left on the island when their kind is left out (they are the game's, and puzzles use them): the lamps.
        private static readonly string[] StayWhenLeftOut = { "BuoyProp", "BuoyRedProp", "BuoyLight" };

        // Never taken off the island, picked up or left out: the Salon's paintbrushes (player, 2026-10-13).
        // ... and the lamps, which light sanity switches on (player, 2026-10-13).
        private static readonly string[] NeverTaken = { "SalonBrushProp", "BuoyProp", "BuoyRedProp", "BuoyLight" };

        private static HashSet<string> Counted => IsGuest ? MirroredKinds : CountedKinds;

        internal static IEnumerable<string> HostCountedKinds => CountedKinds;

        // The game's name of an object of one of these kinds, or null.
        internal static string ObjectKindOf(string name)
        {
            foreach (var kind in AllObjectKinds)
            {
                if (name == kind || name.StartsWith(kind + " (", StringComparison.Ordinal))
                    return kind;
            }

            return null;
        }

        // Whether this machine leaves an object of this name on the island: object_sanity on and its kind counted.
        internal static bool KeepsObjectOnMap(string name)
        {
            var kind = ObjectKindOf(name);
            return KeepsObjectsOnMap && kind != null && Counted.Contains(kind);
        }

        // Whether this machine takes an object of this name off the island: object_sanity on, its kind left out.
        internal static bool HidesObject(string name)
        {
            var kind = ObjectKindOf(name);
            return KeepsObjectsOnMap && kind != null && !Counted.Contains(kind) && Array.IndexOf(NeverTaken, kind) < 0
                   && Array.IndexOf(StayWhenLeftOut, kind) < 0;
        }

        private static byte ObjectMode => IsGuest ? (_mirrored ? _mirroredObjectMode : ObjectsOff) : _objectMode;

        // Whether this machine leaves the island's gadgets on the map.
        internal static bool KeepsObjectsOnMap => ObjectMode != ObjectsOff;

        internal static byte HostObjectMode => _objectMode;

        // The packs picked up for their check: hidden on every machine.
        private static readonly HashSet<string> Collected = new();

        // On a guest: what the host last said.
        private static bool _mirrored;
        private static byte _mirroredMode;

        internal static void Configure(ApSlotData slot)
        {
            _mode = slot.PackSanity switch
            {
                // The slot says on or off since the toggle (0.4.0): off is the packs as in the game.
                // Without the field (a 0.3 seed) they stay hidden, as that build had it.
                "on" or "True" => ModeOn,
                "vanilla" or "False" => ModeVanilla,
                "self" => ModeSelf,
                _ => ModeOff,
            };
            _guids = slot.PickupGuids ?? Array.Empty<string>();
            _fireworkSanity = slot.FireworkSanity;
            _fireworkKeys = slot.FireworkKeys ?? Array.Empty<string>();
            _flareGunSanity = slot.FlareGunSanity;
            _flareGunGuids = slot.FlareGunGuids ?? Array.Empty<string>();
            _objectMode = slot.ObjectSanity switch
            {
                "every" => ObjectsEvery,
                "first_pickup" => ObjectsFirstPickup,
                _ => ObjectsOff,
            };
            _objectGuids = slot.ObjectGuids ?? Array.Empty<string>();
            _objectKinds = slot.ObjectKinds ?? Array.Empty<string>();
            CountedKinds.Clear();
            foreach (var kind in slot.ObjectSanityKinds ?? AllObjectKinds)
                CountedKinds.Add(kind);
            _leftOutHidden = false;

            Collected.Clear();
            if (_mode == ModeOn)
            {
                foreach (var guid in _guids)
                {
                    if (CheckTracker.IsReported(ApLocationIds.PickupPrefix + guid))
                        Collected.Add(guid);
                }
            }
            if (_objectMode == ObjectsEvery)
            {
                foreach (var guid in _objectGuids)
                {
                    if (CheckTracker.IsReported(ApLocationIds.ObjectPrefix + guid))
                        Collected.Add(guid);
                }
            }
            if (_flareGunSanity)
            {
                foreach (var guid in _flareGunGuids)
                {
                    if (CheckTracker.IsReported(ApLocationIds.FlareGunPrefix + guid))
                        Collected.Add(guid);
                }
            }

            Plugin.Log.LogInfo($"{Tag} Packs: mode {_mode}, {Collected.Count} of {_guids.Length} already collected. "
                               + $"Fireworks: {(_fireworkSanity ? _fireworkKeys.Length.ToString() : "off")}. "
                               + $"Objects: mode {_objectMode}.");
            ShowWhatStays();
        }

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        private static byte Mode => IsGuest ? (_mirrored ? _mirroredMode : ModeOff) : _mode;

        // Whether this machine leaves the packs on the map (on or vanilla).
        internal static bool KeepsPacksOnMap => Mode != ModeOff;

        internal static byte HostMode => _mode;

        internal static IEnumerable<string> HostCollected => Collected;

        // `self`: an item that is a pack of this slot's own pick-up locations, already in the world (the host's).
        internal static bool IsPackItself(Archipelago.MultiClient.Net.Models.ItemInfo item) =>
            _mode == ModeSelf && item?.Player != null && ApRuntime.Current != null
            && item.Player.Slot == ApRuntime.Current.SlotNumber && ApLocationIds.IsPickupLocation(item.LocationId);

        // Whether this pack or gadget was picked up for its check (on a guest, as the host says).
        internal static bool IsCollected(string guid) => Collected.Contains(guid);

        internal static void ApplyFromHost(byte mode, byte objectMode, List<string> kinds, List<string> collected)
        {
            _mirrored = true;
            _mirroredMode = mode;
            _mirroredObjectMode = objectMode;
            if (!MirroredKinds.SetEquals(kinds))
            {
                MirroredKinds.Clear();
                foreach (var kind in kinds)
                    MirroredKinds.Add(kind);
                _leftOutHidden = false;
            }
            Collected.Clear();
            foreach (var guid in collected)
                Collected.Add(guid);
            ShowWhatStays();
        }

        internal static void ForgetMirror()
        {
            _mirrored = false;
            _mirroredMode = ModeOff;
            _mirroredObjectMode = ObjectsOff;
            MirroredKinds.Clear();
            Collected.Clear();
        }

        // The packs hidden before this machine knew they stay, back on the map; never one collected.
        private static void ShowWhatStays()
        {
            if (KeepsPacksOnMap)
                GadgetItemSpawner.ShowPacks(guid => !Collected.Contains(guid));
            if (KeepsObjectsOnMap)
                GadgetItemSpawner.ShowObjects(prop => !Collected.Contains(prop.savablePropGuid ?? string.Empty)
                                                      && KeepsObjectOnMap(prop.gameObject.name));
        }

        // Once a second, every machine: a collected pack still showing (a world reloaded, the
        // snapshot that named it) is let go of and hidden.
        private static float _nextTick;
        private static Dictionary<string, Prop> _packs;

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextTick)
                return;
            _nextTick = Time.unscaledTime + 1f;

            if (!WorldManager.isReadyForEffects)
            {
                _packs = null;
                _leftOutHidden = false;
                _worldReadySince = -1f;
                return;
            }

            if (_worldReadySince < 0f)
                _worldReadySince = Time.unscaledTime;

            HideLeftOutKinds();

            // Only checks are ever collected (a pack with pack_sanity on, a flare gun with
            // flare_gun_sanity, a gadget with object_sanity: every), so whatever is in the set goes.
            if (Collected.Count == 0)
                return;

            // A guid collected since the lookup was built is not in it yet: build it again.
            if (_packs != null)
            {
                foreach (var guid in Collected)
                {
                    if (!_packs.ContainsKey(guid))
                    {
                        _packs = null;
                        break;
                    }
                }
            }

            if (_packs == null)
            {
                // The packs and flare guns, and every other prop of the island a check took (the gadgets of
                // `object_sanity: every`, the clocks and cowbells among them, by guid).
                _packs = GadgetItemSpawner.VanillaPacksByGuid();
                foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var id = prop != null ? prop.savablePropGuid : null;
                    if (!string.IsNullOrEmpty(id) && Collected.Contains(id) && !_packs.ContainsKey(id)
                        && !ReceivedItemSpawner.IsCosmeticClone(prop) && !prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                        _packs[id] = prop;
                }
            }

            foreach (var guid in Collected)
            {
                if (!_packs.TryGetValue(guid, out var prop) || prop == null || !prop.gameObject.activeSelf)
                    continue;

                try
                {
                    ReceivedItemSpawner.ReleaseFromHands(prop);
                    GadgetItemSpawner.HidePack(prop.gameObject);
                    Plugin.Log.LogInfo($"{Tag} {prop.gameObject.name} {guid} collected: taken off the map.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not take {guid} off the map: {ex.Message}");
                }
            }
        }

        // The kinds object_sanity_kinds leaves out, off this machine's map, once per world (and again when the
        // kinds change). The pool's gadgets are already kept off by the hiding pass; this is for the clocks and
        // cowbells, which it never hid, and for a gadget a guest showed before it knew.
        private static bool _leftOutHidden;

        private static void HideLeftOutKinds()
        {
            if (_leftOutHidden || !KeepsObjectsOnMap)
                return;
            _leftOutHidden = true;

            var hidden = 0;
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None))
            {
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal)
                    || !HidesObject(prop.gameObject.name))
                    continue;
                GadgetItemSpawner.HidePack(prop.gameObject);
                hidden++;
            }

            if (hidden > 0)
                Plugin.Log.LogInfo($"{Tag} {hidden} object(s) of kinds object_sanity_kinds leaves out taken off the map.");
        }

        // ------------------------------------------------------------------
        // Packs
        // ------------------------------------------------------------------

        internal static void OnPickedUp(Prop prop)
        {
            if (prop == null || !NetworkServer.active || _mode != ModeOn && _mode != ModeSelf && !_flareGunSanity
                && _objectMode == ObjectsOff)
                return;

            // A pack the mod spawned (an item, a trap) is a copy of an island one and carries its
            // guid: only the island's own count (found in play, 2026-10-06: a received backpack
            // made the check of the one it was cloned from).
            if (ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                return;

            var guid = prop.savablePropGuid;
            if (string.IsNullOrEmpty(guid))
                return;

            if (_objectMode != ObjectsOff && OnObjectPickedUp(prop, guid))
                return;

            if (_mode == ModeSelf && Array.IndexOf(_guids, guid) >= 0)
            {
                // Its own item: it stays where it is, in the player's hands.
                Report(ApLocationIds.PickupPrefix + guid, $"{prop.gameObject.name} picked up, its own item");
                return;
            }

            if (_mode == ModeOn && Array.IndexOf(_guids, guid) >= 0)
                Report(ApLocationIds.PickupPrefix + guid, $"{prop.gameObject.name} picked up");
            else if (_flareGunSanity && Array.IndexOf(_flareGunGuids, guid) >= 0)
                Report(ApLocationIds.FlareGunPrefix + guid, $"{prop.gameObject.name} picked up");
            else
                return;

            // Taken off the map on the next tick, out of the Command that is still handing it over.
            if (Collected.Add(guid))
            {
                _nextTick = 0f;
                ModChannel.SendSnapshotNow();
            }
        }

        // An island gadget's check, when it is one; it stays where the player put it down.
        private static bool OnObjectPickedUp(Prop prop, string guid)
        {
            if (_objectMode == ObjectsEvery)
            {
                var kind = ObjectKindOf(prop.gameObject.name);
                if (Array.IndexOf(_objectGuids, guid) < 0 || kind == null || !CountedKinds.Contains(kind))
                    return false;
                Report(ApLocationIds.ObjectPrefix + guid, $"{prop.gameObject.name} picked up");
                // Off the map on the next tick, as a pack (player, 2026-10-13); never a paintbrush.
                if (Array.IndexOf(NeverTaken, kind) < 0 && Collected.Add(guid))
                {
                    _nextTick = 0f;
                    ModChannel.SendSnapshotNow();
                }
                return true;
            }

            var found = ObjectKindOf(prop.gameObject.name);
            if (found == null || !CountedKinds.Contains(found) || Array.IndexOf(_objectKinds, found) < 0)
                return false;
            Report(ApLocationIds.ObjectKindPrefix + found, $"first {found} picked up");
            return true;
        }

        // ------------------------------------------------------------------
        // Fireworks
        // ------------------------------------------------------------------

        // From Patches/WorldButtonPatch, for every state change: cheap until a launcher's.
        internal static void OnState(TrackedPeckState state, int value)
        {
            if (value == 0 || state == null || !NetworkServer.active)
                return;

            OnLight(state);

            var key = LauncherKey(state.transform);
            if (key == null)
                return;

            if (ModConfig.ArchipelagoEnabled.Value && SaveManager.GetIntValue(FiredPrefix + key, 0, false) == 0)
                SaveManager.SetIntValue(FiredPrefix + key, 1);

            if (_fireworkSanity && Array.IndexOf(_fireworkKeys, key) >= 0)
                Report(ApLocationIds.FireworkPrefix + key, $"firework launcher in {key} fired");
        }

        // Light sanity (player, 2026-10-13): a lamp (its own state, on the prop) or a wall switch (its state's path)
        // switched on for the first time, among the slot's lights. Every player's, since the host sees every state.
        private static void OnLight(TrackedPeckState state)
        {
            var slot = ApRuntime.SlotData;
            if (slot == null || slot.LightSanity == "off" && slot.LightsCounted.Length == 0)
                return;

            // The world being built writes every light back to how it starts (the two salons' main lights are on):
            // that is no player switching anything on (seen 2026-10-13, two checks sent at load).
            if (_worldReadySince < 0f || Time.unscaledTime - _worldReadySince < LightSettleSeconds)
                return;

            var prop = state.GetComponent<Prop>();
            string key = null;
            if (prop != null)
            {
                if (ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    return;
                key = prop.savablePropGuid;
            }
            else
            {
                key = state.transform.name;
                for (var t = state.transform.parent; t != null; t = t.parent)
                    key = t.name + "/" + key;
            }

            if (string.IsNullOrEmpty(key))
                return;

            // Counted for big_lights (with or without a check), remembered in the save.
            if (Array.IndexOf(slot.LightsCounted, key) >= 0 && SaveManager.GetIntValue(LitPrefix + key, 0, false) == 0)
            {
                SaveManager.SetIntValue(LitPrefix + key, 1);
                Plugin.Log.LogInfo($"{Tag} light counted: {key}");
            }

            if (slot.LightSanity == "off" || Array.IndexOf(slot.LightKeys, key) < 0)
                return;
            if (slot.LightSanity == "lamps" && prop == null)
                return;

            Report(ApLocationIds.LightPrefix + key, $"{(prop != null ? prop.gameObject.name : "light switch")} switched on");
        }

        private const float LightSettleSeconds = 10f;
        private static float _worldReadySince = -1f;

        // The lights switched on at least once that big_lights counts (light_sanity's checks, or the lamps alone).
        private const string LitPrefix = "ap_lit_";

        internal static int LightsOn()
        {
            var counted = ApRuntime.SlotData?.LightsCounted;
            if (counted == null || counted.Length == 0)
                return 0;
            var on = 0;
            foreach (var key in counted)
            {
                if (SaveManager.GetIntValue(LitPrefix + key, 0, false) != 0
                    || CheckTracker.IsReported(ApLocationIds.LightPrefix + key))
                    on++;
            }
            return on;
        }

        // A save rebound to another seed counts its lights again.
        internal static void ClearLightLedger()
        {
            var data = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (data?.entries == null)
                return;
            var lit = new List<string>();
            foreach (var entry in data.entries)
            {
                if (entry.value != 0 && entry.key != null && entry.key.StartsWith(LitPrefix, StringComparison.Ordinal))
                    lit.Add(entry.key);
            }
            foreach (var key in lit)
                SaveManager.SetIntValue(key, 0);
        }

        // Every launcher fired is remembered, check or no check, for Big Trip (Core/Traps).
        private const string FiredPrefix = "ap_firework_fired_";

        // The landmarks the launchers stand in, the apworld's own list; known to the mod too, so
        // that a launcher is recognised before (or without) a connection.
        private static readonly string[] Landmarks =
        {
            "Hub", "Lookout_RedCone", "Lookout_YellowZigzag", "Lookout_BlueTube", "Lookout_GreenHourglass",
            "PeakMarker", "SouthPeak", "FireworksPlatform_Intro",
        };

        // The landmark a launcher stands in, from the launcher's own transform.
        internal static string LandmarkOf(Transform launcher)
        {
            for (var t = launcher != null ? launcher.parent : null; t != null; t = t.parent)
            {
                if (Array.IndexOf(Landmarks, t.name) >= 0)
                    return t.name;
            }

            return null;
        }

        internal static bool LauncherFired(string key)
        {
            return SaveManager.GetIntValue(FiredPrefix + key, 0, false) != 0
                   || CheckTracker.IsReported(ApLocationIds.FireworkPrefix + key);
        }

        // The landmark of the launcher this transform is part of, or null.
        internal static string LauncherKey(Transform transform)
        {
            var inLauncher = false;
            for (var t = transform; t != null; t = t.parent)
            {
                if (!inLauncher)
                {
                    inLauncher = t.name.StartsWith(LauncherName, StringComparison.Ordinal);
                    continue;
                }

                if (Array.IndexOf(Landmarks, t.name) >= 0)
                    return t.name;
            }

            return null;
        }

        private static void Report(string location, string what)
        {
            if (!CheckTracker.TryMarkReported(location))
                return;

            Plugin.Log.LogInfo($"{Tag} {what}: {location}.");
            Plugin.Reporter.ReportCheck(location);
        }
    }
}
