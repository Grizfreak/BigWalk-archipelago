using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // The logic tour (player, 2026-10-14: "a tool that teleports me to each important check in logic"). tools/logic_tour.py
    // writes BepInEx/logic-tour.tsv: a seed's checks sphere by sphere, each with the logic's needs. Ctrl + Shift + 1 goes
    // to the next stop, Ctrl + Shift + 2 back; the overlay says the check and what the logic asks for it, and the player
    // looks whether the game asks for more. Ctrl + Shift + 0 flags the stop (BepInEx/logic-tour-flags.tsv, with where the
    // player stands). Within a sphere the stops go nearest first. The file is read again when it changes.
    // tools/logic_path.py writes the same file from a spoiler's Playthrough (the shortest way to the goal): each stop then
    // says what its check gives; a stop with no place (the start, a check in another world) is only said.
    internal class DebugLogicTour : MonoBehaviour
    {
        private const string Tag = "[" + nameof(DebugLogicTour) + "]";

        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut NextKey =
            new(KeyCode.Alpha1, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut PreviousKey =
            new(KeyCode.Alpha2, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut FlagKey =
            new(KeyCode.Alpha0, KeyCode.LeftControl, KeyCode.LeftShift);

        private sealed class Stop
        {
            internal int Sphere;
            internal string Kind;
            internal string Key;
            internal string Location;
            internal string Region;
            internal string Needs;
            internal Vector3? Position;
        }

        private static string TourPath => Path.Combine(BepInEx.Paths.BepInExRootPath, "logic-tour.tsv");
        private static string FlagsPath => Path.Combine(BepInEx.Paths.BepInExRootPath, "logic-tour-flags.tsv");

        private List<Stop> _stops;
        private DateTime _loadedStamp;
        private int _index = -1;

        public DebugLogicTour(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            try
            {
                var step = NextKey.IsDown() ? 1 : PreviousKey.IsDown() ? -1 : 0;
                var flag = FlagKey.IsDown();
                if (step == 0 && !flag || !WorldManager.isReadyForEffects)
                    return;

                if (!Load())
                    return;

                if (flag)
                {
                    Flag();
                    return;
                }

                _index = ((_index + step) % _stops.Count + _stops.Count) % _stops.Count;
                Visit(_stops[_index]);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex.Message}");
            }
        }

        private static PlayerCharacter LocalPlayer()
        {
            var local = Mirror.NetworkClient.localPlayer;
            return local != null ? local.GetComponent<PlayerCharacter>() : null;
        }

        private void Visit(Stop stop)
        {
            var head = $"Tour {_index + 1}/{_stops.Count} - sphere {stop.Sphere} - {stop.Location}";
            var detail = stop.Needs + (string.IsNullOrEmpty(stop.Region) ? "" : $" ({stop.Region})");
            if (stop.Position == null && stop.Key == "none:")
            {
                ApNotices.Post(head, seconds: 12f);
                ApNotices.Post(detail, seconds: 12f);
                Plugin.Log.LogInfo($"{Tag} {head} | {detail}");
                return;
            }

            if (stop.Position == null)
            {
                ApNotices.Post($"{head}: no place found ({stop.Key})", warning: true);
                Plugin.Log.LogWarning($"{Tag} {head}: no place found for {stop.Key}");
                return;
            }

            WorldButtons.TeleportTo(LocalPlayer(), stop.Position.Value + Vector3.up * 1.5f + Vector3.back * 1.5f, Quaternion.identity);
            ApNotices.Post(head, seconds: 12f);
            ApNotices.Post(detail, seconds: 12f);
            Plugin.Log.LogInfo($"{Tag} {head} | {detail} | at {stop.Position.Value}");
        }

        private void Flag()
        {
            if (_index < 0)
            {
                ApNotices.Post("Tour: go to a stop first (Ctrl + Shift + 1)", warning: true);
                return;
            }

            var stop = _stops[_index];
            var player = LocalPlayer();
            var at = player != null ? player.transform.position : Vector3.zero;
            var fresh = !File.Exists(FlagsPath);
            using (var writer = new StreamWriter(FlagsPath, append: true))
            {
                if (fresh)
                    writer.WriteLine("time\tstop\tsphere\tlocation\tneeds\tregion\tplayer at");
                writer.WriteLine($"{DateTime.Now:HH:mm:ss}\t{_index + 1}\t{stop.Sphere}\t{stop.Location}\t{stop.Needs}\t{stop.Region}\t{at}");
            }

            ApNotices.Post($"Flagged: {stop.Location}", warning: true);
            Plugin.Log.LogInfo($"{Tag} Flagged stop {_index + 1}: {stop.Location} (player at {at}).");
        }

        // The tour file, read again when it changed; false (and said) when there is none.
        private bool Load()
        {
            if (!File.Exists(TourPath))
            {
                ApNotices.Post("Tour: no BepInEx/logic-tour.tsv (run tools/logic_tour.py)", warning: true);
                return false;
            }

            var stamp = File.GetLastWriteTimeUtc(TourPath);
            if (_stops != null && stamp == _loadedStamp)
                return _stops.Count > 0;

            var stops = new List<Stop>();
            foreach (var line in File.ReadAllLines(TourPath))
            {
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("sphere\t", StringComparison.Ordinal))
                    continue;
                var cells = line.Split('\t');
                if (cells.Length < 6 || !int.TryParse(cells[0], out var sphere))
                    continue;
                stops.Add(new Stop { Sphere = sphere, Kind = cells[1], Key = cells[2], Location = cells[3], Region = cells[4], Needs = cells[5] });
            }

            Resolve(stops);
            _stops = Order(stops);
            _loadedStamp = stamp;
            _index = -1;
            var missing = _stops.Count(s => s.Position == null && s.Key != "none:");
            Plugin.Log.LogInfo($"{Tag} {_stops.Count} stops read, {missing} without a place.");
            ApNotices.Post($"Tour: {_stops.Count} stops" + (missing > 0 ? $", {missing} without a place" : ""));
            return _stops.Count > 0;
        }

        // Sphere by sphere; within one, nearest first from the last stop of the sphere before (or the player).
        private static List<Stop> Order(List<Stop> stops)
        {
            var ordered = new List<Stop>();
            var player = LocalPlayer();
            var from = player != null ? player.transform.position : Vector3.zero;
            foreach (var sphere in stops.GroupBy(s => s.Sphere).OrderBy(g => g.Key))
            {
                var left = sphere.Where(s => s.Position != null).ToList();
                while (left.Count > 0)
                {
                    var here = from;
                    var next = left.OrderBy(s => (s.Position.Value - here).sqrMagnitude).First();
                    left.Remove(next);
                    ordered.Add(next);
                    from = next.Position.Value;
                }

                ordered.AddRange(sphere.Where(s => s.Position == null));
            }

            return ordered;
        }

        // Where each stop's check stands, by the same keys as Core/MapTracker.
        private static void Resolve(List<Stop> stops)
        {
            var props = new Dictionary<string, Prop>();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(prop.savablePropGuid))
                    props.TryAdd(prop.savablePropGuid, prop);
            }

            Dictionary<string, Transform> transformsByPath = null;
            var launchers = new Dictionary<string, Transform>();
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in transforms)
            {
                if (t == null || !t.name.StartsWith("FireworkLauncher", StringComparison.Ordinal))
                    continue;
                var landmark = PackChecks.LandmarkOf(t);
                if (!string.IsNullOrEmpty(landmark))
                    launchers.TryAdd(landmark, t);
            }

            var stations = new Dictionary<string, Transform>();
            foreach (var station in UnityEngine.Object.FindObjectsByType<BroadcastStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (station != null && station.radioPeckSystem != null)
                    stations.TryAdd(station.radioPeckSystem.savableSystem.ToString(), station.transform);
            }

            var keys = new Dictionary<string, Transform>();
            foreach (var blank in UnityEngine.Object.FindObjectsByType<KeyBlank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (blank != null && blank.prop != null && !ReceivedItemSpawner.IsCosmeticClone(blank.prop))
                    keys.TryAdd(blank.prop.saveablePropName.ToString(), blank.prop.transform);
            }

            var homes = new Dictionary<SaveableHomeName, Transform>();
            foreach (var home in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (home != null)
                    homes.TryAdd(home.saveableHomeName, home.transform);
            }

            var gourds = new Dictionary<string, Transform>();
            foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop != null)
                    gourds.TryAdd(prop.saveablePropName.ToString(), prop.transform);
            }

            foreach (var stop in stops)
            {
                var colon = stop.Key.IndexOf(':');
                if (colon < 0)
                    continue;
                var kind = stop.Key.Substring(0, colon);
                var rest = stop.Key.Substring(colon + 1);
                switch (kind)
                {
                    case "prop":
                        if (props.TryGetValue(rest, out var prop))
                            stop.Position = prop.transform.position;
                        break;
                    case "switch":
                        transformsByPath ??= ByPath(transforms);
                        if (transformsByPath.TryGetValue(rest, out var state))
                            stop.Position = (state.parent != null ? state.parent : state).position;
                        break;
                    case "firework":
                        if (launchers.TryGetValue(rest, out var launcher))
                            stop.Position = launcher.position;
                        break;
                    case "radio":
                        if (stations.TryGetValue(rest, out var station))
                            stop.Position = station.position;
                        break;
                    case "tower":
                    {
                        var tower = MonumentOrder.TowerIndexOf(rest);
                        var centre = tower >= 0 ? MonumentOrder.CentreOf(tower) : Vector3.zero;
                        if (centre != Vector3.zero)
                            stop.Position = centre;
                        break;
                    }
                    case "puzzle":
                        if (gourds.TryGetValue(rest, out var gourd))
                            stop.Position = gourd.position;
                        break;
                    case "monument":
                    case "monumentkey":
                    {
                        // The tower's centre: its monument stands inside.
                        var tower = MonumentOrder.TowerIndexOf(rest);
                        var centre = tower >= 0 ? MonumentOrder.CentreOf(tower) : Vector3.zero;
                        if (centre != Vector3.zero)
                            stop.Position = centre;
                        break;
                    }
                    case "deposit":
                        if (Enum.TryParse<SaveablePropName>(rest, out var deposited) && GourdRegistry.TryGetBigKeyHome(deposited, out var plinth)
                            && homes.TryGetValue(plinth, out var plinthPlace))
                            stop.Position = plinthPlace.position;
                        break;
                    case "cut":
                    {
                        // At its cutting station: the stations of the key's trail, the one whose end lies nearest the key.
                        var last = rest.LastIndexOf(':');
                        if (last < 0 || !keys.TryGetValue(rest.Substring(0, last), out var bigKey) || !int.TryParse(rest.Substring(last + 1), out var index))
                            break;
                        var trail = UnityEngine.Object.FindObjectsByType<UnlockTrail>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Where(t => t != null && t.finalTarget != null)
                            .OrderBy(t => (t.finalTarget.position - bigKey.position).sqrMagnitude).FirstOrDefault();
                        if (trail?.stations == null)
                            break;
                        foreach (var cutStation in trail.stations)
                        {
                            if (cutStation != null && cutStation.stationIndex == index)
                                stop.Position = cutStation.transform.position;
                        }
                        break;
                    }
                    case "gauntlet":
                    {
                        // The stage's Level<n> under the Silent Gauntlet.
                        var level = "Level" + rest;
                        foreach (var t in transforms)
                        {
                            if (t == null || t.name != level || !t.gameObject.activeInHierarchy)
                                continue;
                            var parent = t.parent;
                            while (parent != null && !parent.name.StartsWith("SilentGauntlet", StringComparison.Ordinal))
                                parent = parent.parent;
                            if (parent == null)
                                continue;
                            stop.Position = t.position;
                            break;
                        }
                        break;
                    }
                }
            }
        }

        private static Dictionary<string, Transform> ByPath(IEnumerable<Transform> transforms)
        {
            var byPath = new Dictionary<string, Transform>();
            foreach (var t in transforms)
            {
                if (t == null || t.name != "LightSwitchPeckLogic" && !t.name.EndsWith("PeckLogic", StringComparison.Ordinal))
                    continue;
                var path = t.name;
                for (var p = t.parent; p != null; p = p.parent)
                    path = p.name + "/" + path;
                byPath.TryAdd(path, t);
            }

            return byPath;
        }
    }
}
