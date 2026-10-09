using System;
using System.Collections.Generic;
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
    //   vanilla  on the map as in the game, free, and no location.
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

        private static byte _mode;
        private static string[] _guids = Array.Empty<string>();
        private static bool _fireworkSanity;
        private static string[] _fireworkKeys = Array.Empty<string>();

        // The flare guns (`flare_gun_sanity`, player 2026-10-07): on the map whatever the option;
        // with it, the first pick-up of each is its location and the gun then disappears, as a
        // pack does, through the same Collected set and snapshot.
        private static bool _flareGunSanity;
        private static string[] _flareGunGuids = Array.Empty<string>();

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
                _ => ModeOff,
            };
            _guids = slot.PickupGuids ?? Array.Empty<string>();
            _fireworkSanity = slot.FireworkSanity;
            _fireworkKeys = slot.FireworkKeys ?? Array.Empty<string>();
            _flareGunSanity = slot.FlareGunSanity;
            _flareGunGuids = slot.FlareGunGuids ?? Array.Empty<string>();

            Collected.Clear();
            if (_mode == ModeOn)
            {
                foreach (var guid in _guids)
                {
                    if (CheckTracker.IsReported(ApLocationIds.PickupPrefix + guid))
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
                               + $"Fireworks: {(_fireworkSanity ? _fireworkKeys.Length.ToString() : "off")}.");
            ShowWhatStays();
        }

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        private static byte Mode => IsGuest ? (_mirrored ? _mirroredMode : ModeOff) : _mode;

        // Whether this machine leaves the packs on the map (on or vanilla).
        internal static bool KeepsPacksOnMap => Mode != ModeOff;

        internal static byte HostMode => _mode;

        internal static IEnumerable<string> HostCollected => Collected;

        internal static void ApplyFromHost(byte mode, List<string> collected)
        {
            _mirrored = true;
            _mirroredMode = mode;
            Collected.Clear();
            foreach (var guid in collected)
                Collected.Add(guid);
            ShowWhatStays();
        }

        internal static void ForgetMirror()
        {
            _mirrored = false;
            _mirroredMode = ModeOff;
            Collected.Clear();
        }

        // The packs hidden before this machine knew they stay, back on the map; never one collected.
        private static void ShowWhatStays()
        {
            if (KeepsPacksOnMap)
                GadgetItemSpawner.ShowPacks(guid => !Collected.Contains(guid));
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
                return;
            }

            // Only checks are ever collected (a pack with pack_sanity on, a flare gun with
            // flare_gun_sanity), so whatever is in the set goes, whichever its option.
            if (Collected.Count == 0)
                return;

            if (_packs == null)
                _packs = GadgetItemSpawner.VanillaPacksByGuid();

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

        // ------------------------------------------------------------------
        // Packs
        // ------------------------------------------------------------------

        internal static void OnPickedUp(Prop prop)
        {
            if (prop == null || !NetworkServer.active || _mode != ModeOn && !_flareGunSanity)
                return;

            // A pack the mod spawned (an item, a trap) is a copy of an island one and carries its
            // guid: only the island's own count (found in play, 2026-10-06: a received backpack
            // made the check of the one it was cloned from).
            if (ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                return;

            var guid = prop.savablePropGuid;
            if (string.IsNullOrEmpty(guid))
                return;

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

        // ------------------------------------------------------------------
        // Fireworks
        // ------------------------------------------------------------------

        // From Patches/WorldButtonPatch, for every state change: cheap until a launcher's.
        internal static void OnState(TrackedPeckState state, int value)
        {
            if (value == 0 || state == null || !NetworkServer.active)
                return;

            var key = LauncherKey(state.transform);
            if (key == null)
                return;

            if (ModConfig.ArchipelagoEnabled.Value && SaveManager.GetIntValue(FiredPrefix + key, 0, false) == 0)
                SaveManager.SetIntValue(FiredPrefix + key, 1);

            if (_fireworkSanity && Array.IndexOf(_fireworkKeys, key) >= 0)
                Report(ApLocationIds.FireworkPrefix + key, $"firework launcher in {key} fired");
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
