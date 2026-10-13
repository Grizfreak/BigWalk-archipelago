using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using TMPro;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // The 0.7 recon (player, 2026-10-13: recon before code). Ctrl + Shift + 8 writes
    // `BepInEx/recon-0.7-<n>.txt`, four parts, one per roadmap item:
    //   - R7 whiteboards: every text the scene holds (TMP_Text, the players' writable signs; a legacy TextMesh shows in the components) and every
    //     object whose name says board, sign or note, with its components, to tell a text from a picture;
    //   - R6b the Cabin Fever dial: the whole tree under each CabinFeverHouse, component by component, to find
    //     what turns with the TimerSystem;
    //   - C2: the colours each kind of gadget carries, and the firework launchers' trees;
    //   - U2 the map: every map flag, its state, and whether its puzzle's check was sent, to see what the map
    //     already shows on a seed.
    // Ctrl + Shift + 9 goes to the next recon spot (the two Cabin Fever rooms, the hub's notice board, the spare
    // sign) so the player can look at it. Pure diagnostic, no writes to the game.
    internal class DebugRecon07 : MonoBehaviour
    {
        private const string Tag = "[" + nameof(DebugRecon07) + "]";

        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut DumpKey =
            new(KeyCode.Alpha8, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut NextSpotKey =
            new(KeyCode.Alpha9, KeyCode.LeftControl, KeyCode.LeftShift);
        // R6b (Alt + 5: Windows keeps Ctrl + Shift + 0 for the keyboard layout): the Cabin Fever dial's animation shown or not, even while the wait
        // is not running.
        // U2: the map tracker shows every check (as `all`), or as the seed says.
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut MapAllKey =
            new(KeyCode.Alpha6, KeyCode.LeftAlt);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut DialModeKey =
            new(KeyCode.Alpha5, KeyCode.LeftAlt);

        // Paths (or path fragments) of the spots, found in the timer dump and the object census.
        private static readonly string[] SpotNames =
        {
            "CabinFever/Positioner/CabinFeverHouse",
            "CabinFeverLong/Positioner/CabinFeverHouse",
            "Hub/NoticeBoardWhiteboards",
            "CoordinatesSimPress/Positioner/SpareSignProp",
            MapSpot,
        };

        // U2: the spot of the first map (GourdMap) found, whatever its path.
        private const string MapSpot = "(the Map Room's map)";

        // C2: the gadgets that might take the seed's colours (S7's kinds less the lamps, already painted).
        private static readonly string[] GadgetKinds =
        {
            "WalkieTalkieProp", "MegaphoneProp", "FmRadioProp", "BinocularsProp", "LaserProp", "TorchProp",
            "XrayGogglesProp", "ClockProp", "CompassProp", "FoldingMapProp", "CowBellLargeProp", "CowBellMediumProp",
            "CowBellSmallProp", "SalonBrushProp", "KettleProp",
        };

        private static int _count;
        private int _spot = -1;

        public DebugRecon07(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            try
            {
                if (!WorldManager.isReadyForEffects)
                    return;
                if (DumpKey.IsDown())
                    Dump();
                if (NextSpotKey.IsDown())
                    NextSpot();
                if (MapAllKey.IsDown())
                {
                    MapTracker.DebugShowAll = !MapTracker.DebugShowAll;
                    ApNotices.Post($"Map tracker: {(MapTracker.DebugShowAll ? "every check" : "as the seed says")}");
                }
                if (DialModeKey.IsDown())
                {
                    CabinFeverDial.Preview = !CabinFeverDial.Preview;
                    ApNotices.Post($"Cabin Fever dial preview: {(CabinFeverDial.Preview ? "on" : "off")}");
                    Plugin.Log.LogInfo($"{Tag} dial preview {CabinFeverDial.Preview}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex}");
            }
        }

        private void NextSpot()
        {
            _spot = (_spot + 1) % SpotNames.Length;
            var target = SpotNames[_spot] == MapSpot ? FirstMap() : FindByPath(SpotNames[_spot]);
            if (target == null)
            {
                ApNotices.Post($"Recon: {SpotNames[_spot]} not found", warning: true);
                return;
            }

            var local = Mirror.NetworkClient.localPlayer;
            var player = local != null ? local.GetComponent<PlayerCharacter>() : null;
            var at = target.position;
            WorldButtons.TeleportTo(player, at + Vector3.back * 2.5f + Vector3.up * 1.5f, Quaternion.identity);
            var line = $"Recon {_spot + 1}/{SpotNames.Length}: {SpotNames[_spot]}";
            ApNotices.Post(line);
            Plugin.Log.LogInfo($"{Tag} {line} at {at}");
        }

        private static Transform FirstMap()
        {
            foreach (var map in UnityEngine.Object.FindObjectsByType<GourdMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (map != null && map.gameObject.activeInHierarchy)
                    return map.transform;
            }
            return null;
        }

        private static Transform FindByPath(string fragment)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && PathOf(t).EndsWith(fragment, StringComparison.Ordinal))
                    return t;
            }
            return null;
        }

        private static void Dump()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            var origin = player != null ? player.transform.position : Vector3.zero;
            var text = new StringBuilder();
            text.AppendLine($"# recon 0.7, map version {PlayerCountSwapper.playerCount} players, player at {origin}");

            text.AppendLine();
            text.AppendLine("## R7 texts (kind, active, distance, position, path, text)");
            foreach (var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null)
                    Row(text, "TMP_Text", t.transform, origin, t.text);
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<PeckEffectTextInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null)
                    Row(text, "PeckEffectTextInput", t.transform, origin, Components(t.gameObject));
            }

            text.AppendLine();
            text.AppendLine("## R7 boards, signs, notes by name (active, distance, position, path, components)");
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null)
                    continue;
                var name = t.name.ToLowerInvariant();
                if (name.Contains("board") || name.Contains("sign") || name.Contains("note") || name.Contains("paper")
                    || name.Contains("poster") || name.Contains("writing"))
                    Row(text, "named", t, origin, Components(t.gameObject));
            }

            text.AppendLine();
            text.AppendLine("## R6b Cabin Fever houses, every object and its components (depth, active, path, components)");
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == "CabinFeverHouse")
                    Tree(text, t, 0);
            }

            text.AppendLine();
            text.AppendLine("## U2 map flags (puzzle, flag state, active, check sent, location id)");
            foreach (var flag in UnityEngine.Object.FindObjectsByType<GourdFlag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (flag == null)
                    continue;
                var propName = flag.saveablePropName;
                var sent = GourdRegistry.TryGetLocationId(propName, out var locationId)
                    ? (CheckTracker.IsReported(locationId) ? "sent" : "not sent")
                    : "no location";
                text.AppendLine($"{propName}\t{flag.gourdState}\t{(flag.gameObject.activeInHierarchy ? "active" : "inactive")}\t{sent}\t{locationId}\t{PathOf(flag.transform)}");
            }

            text.AppendLine();
            text.AppendLine("## C2 gadget colours (kind, how many, distinct colour sets, first one's colours: PropertyBlockHelper path:property=#RGB, then renderer[on/off]:material)");
            var byKind = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    continue;
                var kind = Array.Find(GadgetKinds, k => prop.gameObject.name.StartsWith(k, StringComparison.Ordinal));
                if (kind == null)
                    continue;
                if (!byKind.TryGetValue(kind, out var list))
                    byKind[kind] = list = new List<string>();
                list.Add(DebugPickupCensus.ColoursOf(prop.gameObject));
            }
            foreach (var pair in byKind)
            {
                var distinct = new HashSet<string>(pair.Value);
                text.AppendLine($"{pair.Key}	{pair.Value.Count}	{distinct.Count}	{pair.Value[0]}");
                if (distinct.Count > 1)
                {
                    foreach (var other in distinct)
                    {
                        if (other != pair.Value[0])
                            text.AppendLine($"			also: {other}");
                    }
                }
            }

            text.AppendLine();
            text.AppendLine("## C2 firework launchers (path, colours, then the tree: depth, name, components, particle start colours)");
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith("FireworkLauncher", StringComparison.Ordinal))
                    continue;
                text.AppendLine($"{PathOf(t)}	{DebugPickupCensus.ColoursOf(t.gameObject)}");
                LauncherTree(text, t, 1);
            }

            text.AppendLine();
            text.AppendLine("## U2 maps (path, active, position, rotation, lossy scale, worldScaleOffset, scale, flag prefabs, flags)");
            foreach (var map in UnityEngine.Object.FindObjectsByType<GourdMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (map == null)
                    continue;
                var t = map.transform;
                text.AppendLine($"{PathOf(t)}\t{(map.gameObject.activeInHierarchy ? "active" : "inactive")}\t{t.position}\t{t.rotation.eulerAngles}\t{t.lossyScale}"
                                + $"\t{map.worldScaleOffset}\t{map.scale}\t{(map.flagPrefab != null ? map.flagPrefab.name : "-")}"
                                + $"\t{(map.flagPrefabVariantChallenge != null ? map.flagPrefabVariantChallenge.name : "-")}");
                if (map.flagPrefab != null)
                {
                    text.AppendLine("  flag prefab tree:");
                    Tree(text, map.flagPrefab.transform, 2);
                }

                // Each gourd's world position (the map's own data) beside its flag's place, to fit the conversion.
                var worldOf = new Dictionary<int, Vector3>();
                if (map.positionData != null && map.positionData.gourdListings != null)
                {
                    foreach (var listing in map.positionData.gourdListings)
                        worldOf[(int)listing.saveablePropName] = listing.position;
                }
                text.AppendLine("  flags: name, state, world (data), flag local position (map space), flag world position");
                foreach (var flag in map.GetComponentsInChildren<GourdFlag>(true))
                {
                    var world = worldOf.TryGetValue((int)flag.saveablePropName, out var w) ? w.ToString() : "?";
                    var local = t.InverseTransformPoint(flag.transform.position);
                    text.AppendLine($"  {flag.saveablePropName}\t{flag.gourdState}\t{world}\t({local.x:F4}, {local.y:F4}, {local.z:F4})\t{flag.transform.position}");
                }
            }

            _count++;
            var path = Path.Combine(Paths.BepInExRootPath, $"recon-0.7-{_count}.txt");
            File.WriteAllText(path, text.ToString());
            ApNotices.Post($"Recon dump {_count} written");
            Plugin.Log.LogInfo($"{Tag} wrote {path}");
        }

        private static void Row(StringBuilder text, string kind, Transform t, Vector3 origin, string detail)
        {
            var at = t.position;
            var flat = (detail ?? string.Empty).Replace('\n', '|').Replace('\r', ' ').Replace('\t', ' ');
            if (flat.Length > 160)
                flat = flat.Substring(0, 160) + "…";
            text.AppendLine($"{kind}\t{(t.gameObject.activeInHierarchy ? "active" : "inactive")}\t{Vector3.Distance(origin, at):F0}\t({at.x:F1}, {at.y:F1}, {at.z:F1})\t{PathOf(t)}\t{flat}");
        }

        private static void Tree(StringBuilder text, Transform t, int depth)
        {
            text.AppendLine($"{depth}\t{(t.gameObject.activeInHierarchy ? "active" : "inactive")}\t{new string(' ', depth * 2)}{t.name}\t{Components(t.gameObject)}");
            for (var i = 0; i < t.childCount; i++)
                Tree(text, t.GetChild(i), depth + 1);
        }

        private static void LauncherTree(StringBuilder text, Transform t, int depth)
        {
            var colour = string.Empty;
            var particles = t.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                // Some of the particle API is stripped from the game: a failure says so rather than ending the dump.
                try
                {
                    colour = $"	start #{ColorUtility.ToHtmlStringRGB(particles.main.startColor.color)}";
                }
                catch (Exception ex)
                {
                    colour = $"	start unreadable ({ex.GetType().Name})";
                }
            }
            text.AppendLine($"	{depth}	{new string(' ', depth * 2)}{t.name}	{Components(t.gameObject)}{colour}");
            for (var i = 0; i < t.childCount; i++)
                LauncherTree(text, t.GetChild(i), depth + 1);
        }

        private static string Components(GameObject go)
        {
            var names = new List<string>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component != null && !(component is Transform))
                    names.Add(component.GetIl2CppType().Name);
            }
            return string.Join(", ", names);
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }
    }
}
