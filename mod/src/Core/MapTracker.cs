using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // U2 (`map_tracker`, player 2026-10-13): the Map Room's big map as a tracker of the seed. A check done leaves the map
    // (however it was sent), one out of logic is greyed, never hidden. The puzzles on the game's own flags; the island's
    // objects, lamps, packs and flare guns on markers of the mod's own: a stick and, on it, a small copy of the
    // object's own model (player, 2026-10-13: the kind told by the object on the pin, not by a colour).
    //
    // The map (recon 2026-10-13): one `GourdMap` (MapRoom/Positioner/Map-Relief/GourdMap) builds a flag per gourd from
    // `GourdMapFlag`, placed by the map's `worldScaleOffset` (0, 50, 0) and `scale` (about 1/70); a flag holds the
    // gourd in three states (gourd_locked, gourd_loose, gourd_stashed, each coloured through a PropertyBlockHelper) and
    // the towers' two marks. The game sets a flag's state again as gourds move, so this is applied every second, and a
    // flag left alone goes back as the game made it.
    //
    // Every check is known by a key any machine can find on the island ("prop:<guid>", "cut:<key>:<n>", ...): the host,
    // which has the slot, works out each key's state (shown, greyed, hinted, in the tunnels, an ending open) and sends
    // the list to the guests (ModChannel, kind 8), who build the same markers from the keys and show the same states.
    internal static class MapTracker
    {
        private const string Tag = "[" + nameof(MapTracker) + "]";
        private const float TickSeconds = 1f;

        // A flag out of logic: its colours drawn towards this grey.
        private static readonly Color Grey = new(0.55f, 0.55f, 0.55f, 1f);
        private const float GreyAmount = 0.8f;

        private static GourdMap _map;
        private static float _tickAt;

        // The colours a flag's helpers had before the mod greyed them, by helper, to give them back.
        private static readonly Dictionary<int, Color[]> Original = new();
        private static readonly Dictionary<int, PropertyBlockHelper> Greyed = new();
        private static readonly Dictionary<int, GourdFlag> HiddenFlags = new();
        private static int _loggedBuilt;

        // The mod's markers, by location id.
        private sealed class Marker
        {
            internal GameObject Root;
            internal readonly List<(Renderer renderer, MaterialPropertyBlock own, MaterialPropertyBlock grey)> Parts = new();
            internal bool Greyed;
            internal Renderer Stick;
            internal Color StickColour;
            internal bool Hinted;
        }

        // A hinted check (player, 2026-10-13): its stick gold, glowing (its colour runs from gold to white and back),
        // shown even out of logic. A puzzle's flag gets such a stick of its own, under it.
        private static readonly Color Gold = new(1.6f, 1.2f, 0.25f, 1f);
        private static readonly Color Glow = new(2.2f, 2.2f, 2f, 1f);
        private static readonly HashSet<Renderer> Glowing = new();
        private static readonly Dictionary<int, GameObject> FlagSticks = new();

        // By key; built once for every key known, in the keys' order, so a spot's spread is the same on every machine.
        private static readonly Dictionary<string, Marker> Markers = new();
        private static readonly HashSet<string> Built = new();

        // A key's state: bits.
        private const byte Visible = 1, GreyBit = 2, HintBit = 4, TunnelBit = 8, OpenBit = 16;

        // The host's keys (with their location ids) and the states last worked out; a guest's, as the host sent them.
        private static readonly List<(string key, long id)> HostKeys = new();
        private static bool _hostKeysBuilt;
        private static Dictionary<string, byte> _states = new();
        private static Dictionary<string, byte> _mirrored;
        private static string _sent;
        private static float _sentAt;
        private const float ResendSeconds = 10f;

        private static bool IsGuest => Mirror.NetworkClient.active && !Mirror.NetworkServer.active;

        // A marker's size on the map: the copy fits in this, on a stick this tall (map units are metres; a gourd's flag
        // stands about 0.2 above the relief).
        private const float ModelSize = 0.13f;
        private const float StickHeight = 0.2f;
        private const float Neighbour = 0.22f;
        private const float StackStep = 0.03f;
        private const float SpreadStep = 0.09f;
        private static readonly Color StickColour = new(0.25f, 0.35f, 0.75f);
        private static readonly Color BuriedStickColour = new(0.95f, 0.55f, 0.1f);
        private static readonly Color GreyTint = new(0.35f, 0.35f, 0.35f, 1f);

        private static bool OnlyInLogic =>
            !DebugShowAll && ApRuntime.SlotData != null && ApRuntime.SlotData.MapTrackerMode == "in_logic";

        // Debug (Alt + 6, Debug/DebugRecon07): every check on the map, as `all`, on this machine.
        internal static bool DebugShowAll;

        private static bool On => IsGuest
            ? _mirrored != null
            : ApRuntime.SlotData != null && ApRuntime.SlotData.MapTracker && TrackerState.Ready;

        internal static void ApplyFromHost(string text)
        {
            var states = new Dictionary<string, byte>();
            foreach (var line in (text ?? string.Empty).Split('\n'))
            {
                var tab = line.LastIndexOf('\t');
                if (tab > 0 && byte.TryParse(line.Substring(tab + 1), out var state))
                    states[line.Substring(0, tab)] = state;
            }
            _mirrored = states.Count > 0 ? states : null;
        }

        internal static void ForgetMirror() => _mirrored = null;

        // Every frame (TrapRunner).
        internal static void Tick()
        {
            PulseHints();
            PulseRainbow();
            if (Time.unscaledTime < _tickAt)
                return;
            _tickAt = Time.unscaledTime + TickSeconds;

            try
            {
                if (!WorldManager.isReadyForEffects)
                {
                    _map = null;
                    Original.Clear();
                    Greyed.Clear();
                    HiddenFlags.Clear();
                    Markers.Clear();
                    Built.Clear();
                    Glowing.Clear();
                    Rainbow.Clear();
                    FlagSticks.Clear();
                    _placed.Clear();
                    _scene = null;
                    return;
                }

                if (!IsGuest)
                    TrackerState.Tick();
                if (!On)
                {
                    RestoreAll();
                    return;
                }

                if (_map == null)
                    _map = FindMap();
                if (_map == null)
                    return;

                if (!IsGuest)
                {
                    _states = HostStates();
                    SendToGuests();
                }
                var states = IsGuest ? _mirrored : _states;

                // The markers, built once for every key, in order.
                var keys = new List<string>(states.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var key in keys)
                {
                    if (!key.StartsWith("puzzle:", StringComparison.Ordinal) && Built.Add(key))
                        Build(key);
                }
                if (Built.Count != _loggedBuilt)
                {
                    _loggedBuilt = Built.Count;
                    Plugin.Log.LogInfo($"{Tag} {Markers.Count} marker(s) on the map for {Built.Count} key(s){(IsGuest ? " (from the host)" : string.Empty)}.");
                }
                foreach (var pair in Markers)
                    ApplyMarker(pair.Key, pair.Value, states.TryGetValue(pair.Key, out var state) ? state : (byte)0);
                ApplyFlags(states);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex.Message}");
            }
        }

        private static GourdMap FindMap()
        {
            foreach (var map in UnityEngine.Object.FindObjectsByType<GourdMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (map != null)
                    return map;
            }
            return null;
        }

        // A done check's flag: its renderers off (the game keeps the flag and may set its state again).
        private static void Hide(GourdFlag flag)
        {
            HiddenFlags[flag.GetInstanceID()] = flag;
            foreach (var renderer in flag.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private static void Show(GourdFlag flag)
        {
            if (!HiddenFlags.Remove(flag.GetInstanceID()))
                return;
            foreach (var renderer in flag.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }

        private static void SetGrey(PropertyBlockHelper helper, bool grey)
        {
            var settings = helper != null ? helper.colorSettings : null;
            if (settings == null || settings.Length == 0)
                return;
            var id = helper.GetInstanceID();
            if (grey == Greyed.ContainsKey(id))
                return;

            if (grey)
            {
                var original = new Color[settings.Length];
                for (var i = 0; i < settings.Length; i++)
                {
                    // An element of an IL2CPP struct array comes back as a copy (ReceivedItemSpawner).
                    var setting = settings[i];
                    original[i] = setting.color;
                    setting.color = Color.Lerp(setting.color, Grey, GreyAmount);
                    settings[i] = setting;
                }
                Original[id] = original;
                Greyed[id] = helper;
            }
            else
            {
                if (Original.TryGetValue(id, out var original))
                {
                    for (var i = 0; i < settings.Length && i < original.Length; i++)
                    {
                        var setting = settings[i];
                        setting.color = original[i];
                        settings[i] = setting;
                    }
                }
                Original.Remove(id);
                Greyed.Remove(id);
            }
            helper.Refresh();
        }

        // ------------------------------------------------------------------
        // The host: the keys and their states
        // ------------------------------------------------------------------

        private static void BuildHostKeys()
        {
            _hostKeysBuilt = true;
            HostKeys.Clear();
            var slot = ApRuntime.SlotData;
            var connection = ApRuntime.Current;

            void Add(string key, string location)
            {
                if (ApLocationIds.TryResolveLocation(location, out var id) && connection.BelongsToSlot(id))
                    HostKeys.Add((key, id));
            }

            foreach (var guid in slot.ObjectGuids)
                Add("prop:" + guid, ApLocationIds.ObjectPrefix + guid);
            foreach (var key in slot.LightKeys)
                Add((key.Contains("/") ? "switch:" : "prop:") + key, ApLocationIds.LightPrefix + key);
            foreach (var guid in slot.PickupGuids)
                Add("prop:" + guid, ApLocationIds.PickupPrefix + guid);
            foreach (var guid in slot.FlareGunGuids)
                Add("prop:" + guid, ApLocationIds.FlareGunPrefix + guid);
            foreach (var landmark in slot.FireworkKeys)
                Add("firework:" + landmark, ApLocationIds.FireworkPrefix + landmark);
            foreach (var system in RadioStations.All)
                Add("radio:" + system, system.ToString());
            for (var tower = 0; tower < MonumentOrder.TowerCount; tower++)
            {
                var prop = MonumentOrder.PropOfTower(tower);
                Add("tower:" + prop, ApLocationIds.TowerPrefix + prop);
                Add("monument:" + prop, ApLocationIds.MonumentPrefix + prop);
                Add("monumentkey:" + prop, ApLocationIds.MonumentKeyPrefix + prop);
            }
            for (var chamber = 0; chamber < 7; chamber++)
                Add("gauntlet:" + chamber, "GauntletChamber" + chamber);
            foreach (var key in GourdRegistry.BigKeyProps)
            {
                for (var index = 0; index < 5; index++)
                    Add("cut:" + key + ":" + index, ApLocationIds.CutLocationName(key, index));
                if (GourdRegistry.TryGetLocationId(key, out var deposit))
                    Add("deposit:" + key, deposit);
            }
            foreach (var flag in _map.GetComponentsInChildren<GourdFlag>(true))
            {
                if (flag != null && GourdRegistry.TryGetLocationId(flag.saveablePropName, out var puzzle))
                    Add("puzzle:" + flag.saveablePropName, puzzle);
            }
        }

        private static Dictionary<string, byte> HostStates()
        {
            if (!_hostKeysBuilt)
                BuildHostKeys();
            var logic = ApRuntime.SlotData?.Logic;
            var states = new Dictionary<string, byte>();
            foreach (var (key, id) in HostKeys)
            {
                var done = TrackerState.Checked.Contains(id);
                var inLogic = TrackerState.InLogic.Contains(id);
                var hinted = TrackerState.Hinted.Contains(id);
                var shown = !done && (inLogic || hinted || !OnlyInLogic);
                var region = logic?.RegionOf(id);
                var tunnel = region != null && region.IndexOf("Tunnel", StringComparison.OrdinalIgnoreCase) >= 0;
                states[key] = (byte)((shown ? Visible : 0) | (inLogic ? 0 : GreyBit) | (hinted ? HintBit : 0) | (tunnel ? TunnelBit : 0));
            }

            // The run's objectives with a place, and the seed's ending even when it is the goal alone.
            var objectives = new List<string>(ApObjectives.Keys);
            var goal = ApRuntime.SlotData?.Goal switch
            {
                "gauntlet" => "big_goodbye",
                "ending" => "big_wall",
                "second_ending" => "big_game",
                _ => null,
            };
            if (goal != null && !objectives.Contains(goal))
                objectives.Add(goal);
            foreach (var key in objectives)
            {
                if (!HasPlace(key))
                    continue;
                var done = ApObjectives.IsDone(key);
                var ending = ApObjectives.IsEnding(key);
                var open = ending && ApObjectives.OthersDone(key);
                var locked = ending && ObjectiveBoard.Locked(key);
                states["objective:" + key] = (byte)((done ? 0 : Visible) | (locked ? GreyBit : 0) | (open ? OpenBit : 0));
            }
            return states;
        }

        private static bool HasPlace(string objective) =>
            objective is "big_climb" or "big_fiesta" or "big_wall" or "big_game" or "big_goodbye";

        // To the guests: the list when it changes, and every few seconds for one who joined since.
        private static void SendToGuests()
        {
            var lines = new List<string>();
            foreach (var pair in _states)
                lines.Add(pair.Key + "\t" + pair.Value);
            lines.Sort(StringComparer.Ordinal);
            var text = string.Join("\n", lines);
            if (text == _sent && Time.unscaledTime < _sentAt + ResendSeconds)
                return;
            _sent = text;
            _sentAt = Time.unscaledTime;
            ModChannel.SendMap(text);
        }

        // ------------------------------------------------------------------
        // Every machine: the markers and the puzzles' flags
        // ------------------------------------------------------------------

        private static void ApplyFlags(Dictionary<string, byte> states)
        {
            foreach (var flag in _map.GetComponentsInChildren<GourdFlag>(true))
            {
                if (flag == null || !states.TryGetValue("puzzle:" + flag.saveablePropName, out var state))
                    continue;
                var hinted = (state & HintBit) != 0;
                if ((state & Visible) == 0)
                {
                    Hide(flag);
                    FlagStick(flag, false);
                    continue;
                }
                FlagStick(flag, hinted);
                Show(flag);
                foreach (var helper in flag.GetComponentsInChildren<PropertyBlockHelper>(true))
                    SetGrey(helper, (state & GreyBit) != 0);
            }
        }

        private static void ApplyMarker(string key, Marker marker, byte state)
        {
            if (marker.Root == null)
                return;
            var shown = (state & Visible) != 0;
            if (marker.Root.activeSelf != shown)
                marker.Root.SetActive(shown);
            if (!shown)
                return;

            var grey = (state & GreyBit) != 0;
            if (grey != marker.Greyed)
            {
                marker.Greyed = grey;
                foreach (var (renderer, own, greyBlock) in marker.Parts)
                {
                    if (renderer != null)
                        renderer.SetPropertyBlock(grey ? greyBlock : own);
                }
            }

            if (key.StartsWith("objective:", StringComparison.Ordinal))
            {
                SetOpen(marker, (state & OpenBit) != 0);
                return;
            }

            var hinted = (state & HintBit) != 0;
            var stickColour = (state & TunnelBit) != 0 ? BuriedStickColour : StickColour;
            if (marker.Stick != null && (hinted != marker.Hinted || stickColour != marker.StickColour))
            {
                marker.Hinted = hinted;
                marker.StickColour = stickColour;
                if (hinted)
                    Glowing.Add(marker.Stick);
                else
                {
                    Glowing.Remove(marker.Stick);
                    Tint(marker.Stick, stickColour);
                }
            }
        }

        // ------------------------------------------------------------------
        // Where each key stands on the island, and its model
        // ------------------------------------------------------------------

        private sealed class Scene
        {
            internal readonly Dictionary<string, Prop> Props = new();
            internal readonly Dictionary<string, Transform> Launchers = new();
            internal readonly Dictionary<string, Transform> Stations = new();
            internal readonly Dictionary<SaveablePropName, Transform> Keys = new();
            internal readonly Dictionary<SaveableHomeName, Transform> Homes = new();
            internal Dictionary<int, Vector3> Monuments;
            internal Transform TowerMark;
            internal Transform GourdMark;
            internal Transform Gauntlet;
        }

        private static Scene _scene;

        private static Scene Island()
        {
            if (_scene != null)
                return _scene;
            var scene = new Scene();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(prop.savablePropGuid))
                    scene.Props[prop.savablePropGuid] = prop;
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith("FireworkLauncher", StringComparison.Ordinal))
                    continue;
                var landmark = PackChecks.LandmarkOf(t);
                if (!string.IsNullOrEmpty(landmark))
                    scene.Launchers[landmark] = t;
            }
            foreach (var station in UnityEngine.Object.FindObjectsByType<BroadcastStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (station != null && station.radioPeckSystem != null)
                    scene.Stations[station.radioPeckSystem.savableSystem.ToString()] = station.transform;
            }
            foreach (var blank in UnityEngine.Object.FindObjectsByType<KeyBlank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (blank != null && blank.prop != null && !ReceivedItemSpawner.IsCosmeticClone(blank.prop))
                    scene.Keys[blank.prop.saveablePropName] = blank.prop.transform;
            }
            foreach (var home in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (home != null)
                    scene.Homes[home.saveableHomeName] = home.transform;
            }
            scene.Monuments = MonumentPlaces();
            var flag = _map.flagPrefab != null ? _map.flagPrefab.transform : null;
            scene.TowerMark = flag != null ? FindChild(flag, "tower_flag") : null;
            scene.GourdMark = flag != null ? FindChild(flag, "gourd_stashed") : null;
            scene.Gauntlet = ActiveNamed("SilentGauntlet");
            return _scene = scene;
        }

        // The marker of a key: where it stands and what it shows.
        private static void Build(string key)
        {
            var scene = Island();
            var colon = key.IndexOf(':');
            var kind = key.Substring(0, colon);
            var rest = key.Substring(colon + 1);
            switch (kind)
            {
                case "prop":
                    if (scene.Props.TryGetValue(rest, out var prop))
                        Place(key, prop.transform.position, prop.transform, false);
                    break;
                case "switch":
                {
                    var state = FindByPath(rest);
                    if (state != null && state.parent != null)
                        Place(key, state.parent.position, state.parent, false);
                    break;
                }
                case "firework":
                    if (scene.Launchers.TryGetValue(rest, out var launcher))
                    {
                        var model = launcher.Find("fireworkLauncherModel");
                        Place(key, launcher.position, model != null ? model : launcher, false);
                    }
                    break;
                case "radio":
                    // The station itself, not the radio that tunes it (player, 2026-10-13).
                    if (scene.Stations.TryGetValue(rest, out var station))
                        Place(key, station.position, station, false);
                    break;
                case "tower":
                case "monument":
                {
                    var tower = MonumentOrder.TowerIndexOf(rest);
                    if (tower < 0)
                        break;
                    var hasMonument = scene.Monuments.TryGetValue(tower, out var monument);
                    if (kind == "tower" && scene.TowerMark != null)
                    {
                        var centre = MonumentOrder.CentreOf(tower);
                        if (centre != Vector3.zero)
                            Place(key, centre, scene.TowerMark, true);
                        else if (hasMonument)
                            Place(key, monument, scene.TowerMark, true);
                    }
                    else if (kind == "monument" && hasMonument && scene.GourdMark != null)
                        Place(key, monument, scene.GourdMark, true);
                    break;
                }
                case "monumentkey":
                {
                    // At the monument, the tower's own key on the pin.
                    var tower = MonumentOrder.TowerIndexOf(rest);
                    if (tower >= 0 && scene.Monuments.TryGetValue(tower, out var monument)
                        && Enum.TryParse<SaveablePropName>(rest, out var keyName) && scene.Keys.TryGetValue(keyName, out var bigKey))
                        Place(key, monument, bigKey, false);
                    break;
                }
                case "gauntlet":
                    // In a bunch at the Gauntlet (player, 2026-10-13).
                    if (scene.Gauntlet != null && scene.GourdMark != null)
                        Place(key, scene.Gauntlet.position, scene.GourdMark, true);
                    break;
                case "cut":
                {
                    // Each at its own cutting station, five per key (player, 2026-10-13); a key's trail of stations is
                    // the one whose end lies nearest the key.
                    var last = rest.LastIndexOf(':');
                    if (last < 0 || !Enum.TryParse<SaveablePropName>(rest.Substring(0, last), out var name)
                        || !int.TryParse(rest.Substring(last + 1), out var index) || !scene.Keys.TryGetValue(name, out var bigKey))
                        break;
                    var trail = TrailOf(bigKey);
                    if (trail?.stations == null)
                        break;
                    foreach (var cutStation in trail.stations)
                    {
                        if (cutStation != null && cutStation.stationIndex == index)
                            Place(key, cutStation.transform.position, bigKey, false);
                    }
                    break;
                }
                case "deposit":
                    if (Enum.TryParse<SaveablePropName>(rest, out var deposited) && scene.Keys.TryGetValue(deposited, out var depositKey)
                        && GourdRegistry.TryGetBigKeyHome(deposited, out var plinth) && scene.Homes.TryGetValue(plinth, out var plinthPlace))
                        Place(key, plinthPlace.position, depositKey, false);
                    break;
                case "objective":
                {
                    var place = ObjectivePlace(rest);
                    if (place == null || scene.TowerMark == null)
                        break;
                    var at = MapPoint(place.Value);
                    Surface(ref at);
                    var marker = Make(at, scene.TowerMark, true, ObjectiveStick, ObjectiveColour);
                    if (marker != null)
                        Markers[key] = marker;
                    break;
                }
            }
        }

        private static UnlockTrail TrailOf(Transform key)
        {
            UnlockTrail best = null;
            var bestDistance = float.MaxValue;
            foreach (var trail in UnityEngine.Object.FindObjectsByType<UnlockTrail>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (trail == null || trail.finalTarget == null)
                    continue;
                var distance = (trail.finalTarget.position - key.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = trail;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // The run's objectives that have a place (player, 2026-10-13)
        // ------------------------------------------------------------------

        // A tall white stick with the map's tower flag; an ending still locked greyed; an ending open (every other
        // objective done) on a tall stick running through the colours; done, gone.
        private static readonly Dictionary<string, Marker> Objectives = new();
        private const float ObjectiveStick = 0.6f;
        private const float OpenStick = 1.6f;
        private static readonly Color ObjectiveColour = new(1.2f, 1.2f, 1.2f, 1f);

        private static Vector3? ObjectivePlace(string key)
        {
            switch (key)
            {
                case "big_climb":
                    return ObjectiveSpots.ClimbCentre();
                case "big_fiesta":
                    foreach (var home in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (home != null && home.name == "FmRadioDockPropHome")
                            return home.transform.position;
                    }
                    return null;
                case "big_wall":
                    return ActiveNamed("GoodbyeChapel")?.position;
                case "big_game":
                    return ActiveNamed("GoodbyePrinter")?.position;
                case "big_goodbye":
                {
                    var gauntlet = ActiveNamed("SilentGauntlet");
                    var last = gauntlet != null ? FindChild(gauntlet, "Level6") : null;
                    return (last ?? gauntlet)?.position;
                }
                default:
                    return null;
            }
        }

        private static readonly HashSet<Renderer> Rainbow = new();

        private static void PulseRainbow()
        {
            if (Rainbow.Count == 0)
                return;
            var colour = Color.HSVToRGB(Time.unscaledTime * 0.25f % 1f, 0.9f, 1f) * 1.4f;
            colour.a = 1f;
            foreach (var renderer in new List<Renderer>(Rainbow))
            {
                if (renderer == null)
                    Rainbow.Remove(renderer);
                else
                    Tint(renderer, colour);
            }
        }

        // The active object of this name (the one of the map version loaded), or any.
        private static Transform ActiveNamed(string prefix)
        {
            Transform any = null;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (t.gameObject.activeInHierarchy)
                    return t;
                any ??= t;
            }
            return any;
        }

        // Each tower's monument, where its slots stand (their middle), by the tower's place in MonumentOrder.
        private static Dictionary<int, Vector3> MonumentPlaces()
        {
            var sums = new Dictionary<int, (Vector3 sum, int count)>();
            foreach (var home in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (home == null || !ReceivedItemSpawner.IsMonumentHome(home))
                    continue;
                var prefix = MonumentOrder.PrefixOfHome(home);
                for (var tower = 0; tower < MonumentOrder.TowerCount; tower++)
                {
                    if (MonumentOrder.PrefixOf(tower) != prefix)
                        continue;
                    var entry = sums.TryGetValue(tower, out var known) ? known : (Vector3.zero, 0);
                    sums[tower] = (entry.Item1 + home.transform.position, entry.Item2 + 1);
                    break;
                }
            }
            var places = new Dictionary<int, Vector3>();
            foreach (var pair in sums)
                places[pair.Key] = pair.Value.sum / pair.Value.count;
            return places;
        }

        // The open ending: its stick tall and through the colours.
        private static void SetOpen(Marker marker, bool open)
        {
            if (marker.Stick == null || open == marker.Hinted)
                return;
            marker.Hinted = open;
            var height = open ? OpenStick : ObjectiveStick;
            var foot = marker.Root.transform.position;
            marker.Stick.transform.localScale = new Vector3(open ? 0.025f : 0.012f, height, open ? 0.025f : 0.012f);
            marker.Stick.transform.position = foot + Vector3.up * (height / 2f);
            var model = marker.Root.transform.Find("model");
            if (model != null)
                model.position = foot + Vector3.up * (height + ModelSize / 2f);
            if (open)
                Rainbow.Add(marker.Stick);
            else
            {
                Rainbow.Remove(marker.Stick);
                Tint(marker.Stick, ObjectiveColour);
            }
        }

        private static readonly List<Vector3> _placed = new();

        // A marker for this key at this point of the island; markers on one spot spread on a spiral around it, a little
        // higher each (player, 2026-10-13). Every marker stands on the relief's surface above its point.
        private static void Place(string key, Vector3 world, Transform modelSource, bool fromPrefab)
        {
            var at = MapPoint(world);
            Surface(ref at);
            var neighbours = 0;
            foreach (var other in _placed)
            {
                if ((other - at).sqrMagnitude < Neighbour * Neighbour)
                    neighbours++;
            }
            var spot = at;
            if (neighbours > 0)
            {
                var angle = neighbours * 137.5f * Mathf.Deg2Rad;
                var radius = SpreadStep * Mathf.Sqrt(neighbours);
                at += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Surface(ref at);
            }
            var marker = Make(at, modelSource, fromPrefab, StickHeight + neighbours * StackStep, StickColour);
            if (marker == null)
                return;
            _placed.Add(spot);
            Markers[key] = marker;
        }

        private static bool _noSurfaceLogged;

        // Lifts a map point to the relief's surface right above it, when it is under: whether it was.
        private static bool Surface(ref Vector3 at)
        {
            var relief = _map.transform.parent;
            var terrain = Terrain(relief);
            var best = float.MinValue;
            var onTerrain = false;
            foreach (var hit in Physics.RaycastAll(new Ray(at + Vector3.up * 5f, Vector3.down), 10f))
            {
                if (hit.collider == null || relief == null || !hit.collider.transform.IsChildOf(relief) || hit.point.y <= best)
                    continue;
                best = hit.point.y;
                onTerrain = hit.collider == terrain;
            }
            if (best == float.MinValue)
            {
                if (!_noSurfaceLogged)
                {
                    _noSurfaceLogged = true;
                    Plugin.Log.LogInfo($"{Tag} No collider of the relief under a marker: markers stay at their own height.");
                }
                return false;
            }
            if (best <= at.y + 0.005f)
                return false;
            at.y = best;
            // Orange only when it was under the ground itself; inside a tower's or a building's model it stands on
            // the model, as anything there (player, 2026-10-13: the towers' sticks were orange).
            return onTerrain;
        }

        // The relief's ground: its largest collider.
        private static Collider _terrain;

        private static Collider Terrain(Transform relief)
        {
            if (_terrain != null || relief == null)
                return _terrain;
            var largest = 0f;
            foreach (var collider in relief.GetComponentsInChildren<Collider>(true))
            {
                var size = collider.bounds.size.x * collider.bounds.size.z;
                if (size > largest)
                {
                    largest = size;
                    _terrain = collider;
                }
            }
            if (_terrain != null)
                Plugin.Log.LogInfo($"{Tag} The relief's ground: {_terrain.name}.");
            return _terrain;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == name)
                    return child;
            }
            return null;
        }

        private static Transform FindByPath(string path)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == path.Substring(path.LastIndexOf('/') + 1) && PathOf(t) == path)
                    return t;
            }
            return null;
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        // Where a point of the island stands on the map: the map's own conversion (GourdMap.worldScaleOffset, scale).
        private static Vector3 MapPoint(Vector3 world) =>
            _map.transform.TransformPoint(Vector3.Scale(world + _map.worldScaleOffset, _map.scale));

        // A stick at this point of the map and, on it, a small copy of the model under `source`: each mesh with its
        // place, materials and colours. `fromPrefab`: the source is the map's own flag (inactive), taken whole.
        private static Marker Make(Vector3 at, Transform source, bool fromPrefab, float stickHeight, Color stickColour)
        {
            var marker = new Marker { Root = new GameObject("AP map marker " + source.name) };
            marker.Root.transform.SetParent(_map.transform, true);
            marker.Root.transform.position = at;

            var stick = WorldButtons.MakeSlab("stick", at + Vector3.up * (stickHeight / 2f), Quaternion.identity,
                new Vector3(0.012f, stickHeight, 0.012f), stickColour);
            stick.transform.SetParent(marker.Root.transform, true);
            marker.Stick = stick.GetComponent<Renderer>();
            marker.StickColour = stickColour;

            var model = new GameObject("model");
            model.transform.SetParent(marker.Root.transform, false);
            var parts = new List<GameObject>();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(fromPrefab))
            {
                if (renderer == null || !fromPrefab && !renderer.enabled || IsFarLod(renderer.name))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var part = new GameObject(renderer.name);
                part.transform.SetParent(model.transform, false);
                var localPosition = source.InverseTransformPoint(renderer.transform.position);
                var localRotation = Quaternion.Inverse(source.rotation) * renderer.transform.rotation;
                var scale = renderer.transform.lossyScale;
                var root = source.lossyScale;
                var localScale = new Vector3(scale.x / root.x, scale.y / root.y, scale.z / root.z);
                part.transform.localPosition = localPosition;
                part.transform.localRotation = localRotation;
                part.transform.localScale = localScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;

                var own = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(own);
                copy.SetPropertyBlock(own);
                var grey = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(grey);
                grey.SetColor("_TintColor", GreyTint);
                marker.Parts.Add((copy, own, grey));

                // The part's box in the source's space, from its mesh (a prefab's renderer has no bounds).
                var mesh = filter.sharedMesh.bounds;
                foreach (var corner in Corners(mesh))
                {
                    var p = localPosition + localRotation * Vector3.Scale(corner, localScale);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                parts.Add(part);
            }

            if (parts.Count == 0)
            {
                UnityEngine.Object.Destroy(marker.Root);
                return null;
            }

            // Centred on the stick's top, its largest side ModelSize, turned as the source.
            var centre = (min + max) / 2f;
            foreach (var part in parts)
                part.transform.localPosition -= centre;
            var extent = max - min;
            var size = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            var factor = size > 0.0001f ? ModelSize / size : 1f;
            model.transform.localScale = Vector3.one * factor;
            // A part of the map's flag keeps the turn it has in the flag (the tower's mark stood askew without it).
            model.transform.rotation = fromPrefab && _map.flagPrefab != null
                ? Quaternion.Inverse(_map.flagPrefab.transform.rotation) * source.rotation
                : source.rotation;
            model.transform.position = at + Vector3.up * (stickHeight + ModelSize / 2f);
            return marker;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }

        // A level of detail past the first (`..._LOD1`, `LOD2`): the first one is enough on a model this small.
        private static bool IsFarLod(string name) =>
            name.IndexOf("LOD", StringComparison.OrdinalIgnoreCase) >= 0 && !name.EndsWith("LOD0", StringComparison.OrdinalIgnoreCase);

        private static void PulseHints()
        {
            if (Glowing.Count == 0)
                return;
            var colour = Color.Lerp(Gold, Glow, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) / 2f);
            foreach (var renderer in new List<Renderer>(Glowing))
            {
                if (renderer == null)
                    Glowing.Remove(renderer);
                else
                    Tint(renderer, colour);
            }
        }

        private static void Tint(Renderer renderer, Color colour)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_TintColor", colour);
            renderer.SetPropertyBlock(block);
        }

        // A hinted puzzle's gold stick, under its flag.
        private static void FlagStick(GourdFlag flag, bool hinted)
        {
            var key = flag.GetInstanceID();
            FlagSticks.TryGetValue(key, out var stick);
            if (hinted && stick == null)
            {
                var foot = flag.transform.position;
                stick = WorldButtons.MakeSlab("AP hint stick", foot + Vector3.down * 0.05f, Quaternion.identity,
                    new Vector3(0.02f, 0.3f, 0.02f), Gold);
                stick.transform.SetParent(flag.transform, true);
                FlagSticks[key] = stick;
                Glowing.Add(stick.GetComponent<Renderer>());
            }
            else if (!hinted && stick != null)
            {
                Glowing.Remove(stick.GetComponent<Renderer>());
                UnityEngine.Object.Destroy(stick);
                FlagSticks.Remove(key);
            }
        }

        private static void DestroyMarkers()
        {
            foreach (var stick in FlagSticks.Values)
            {
                if (stick != null)
                    UnityEngine.Object.Destroy(stick);
            }
            FlagSticks.Clear();
            Glowing.Clear();
            Rainbow.Clear();
            foreach (var marker in Markers.Values)
            {
                if (marker.Root != null)
                    UnityEngine.Object.Destroy(marker.Root);
            }
            Markers.Clear();
            Built.Clear();
            _placed.Clear();
            _scene = null;
            _hostKeysBuilt = false;
        }

        private static void RestoreAll()
        {
            DestroyMarkers();
            foreach (var helper in new List<PropertyBlockHelper>(Greyed.Values))
            {
                if (helper != null)
                    SetGrey(helper, false);
            }
            Greyed.Clear();
            Original.Clear();
            foreach (var flag in new List<GourdFlag>(HiddenFlags.Values))
            {
                if (flag != null)
                    Show(flag);
            }
            HiddenFlags.Clear();
        }
    }
}
