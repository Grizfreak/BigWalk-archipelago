using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // `tower_mode` (ROADMAP G4 and H1, 0.5, player 2026-10-11): the monuments take gourds in an order the
    // seed draws, one at a time. `in_order`: the next opens once the one before it is full. `by_item`:
    // it also waits for a Progressive Monument, the first monument being open from the start and the
    // k-th item opening the monument at position k + 1. Closed, a monument refuses a gourd
    // (PropHome.blockPlacing and IsSafeToPlace, Patches/MonumentHoldPatch) and the button that opens
    // its tower stays still (the same patch).
    //
    // Only one monument takes gourds at a time, which is what makes the apworld's logic exact: a gourd
    // placed costs the towers before it and nothing is spent anywhere the logic did not count.
    //
    // The host decides (its slot data and its save); a guest is told which towers are open (ModChannel)
    // and sets the same on its own machine, since a gourd is placed where the player stands.
    internal static class MonumentOrder
    {
        private const string Tag = "[" + nameof(MonumentOrder) + "]";
        private const string GrantedKey = "ap_monuments_granted";
        private const string MapPrefix = "ap_monument_tower_";

        // The apworld's data.TOWERS order, which the guests' mask follows too.
        private static readonly string[] Props =
        {
            "bigKeyIntro", "bigKeyRedZone", "bigKeyGreenZone", "bigKeyBlueZone", "bigKeyYellowZone", "bigKeyBoss", "bigKeyOverflow",
        };

        private static readonly string[] Names =
        {
            "the Drawbridge", "the Red Funnel Tower", "the Green Cup Tower", "the Blue Castle Tower", "the Yellow Twist Tower",
            "the Black Tower", "the Green Dome",
        };

        private static readonly int[] Slots = { 4, 5, 5, 5, 5, 6, 15 };

        // Which `monoument0..3` is which tower was never established in the game: the guess is the
        // table order (the apworld's), and each monument that loads corrects it by where it stands.
        private static readonly string[] GuessedPrefixes =
        {
            "monoumentIntro", "monoument0", "monoument1", "monoument2", "monoument3", "monoumentFinal", "monoumentOverflow",
        };

        // Inside each of the four towers (Core/TeleportButtons' way back), for telling the four apart.
        private static readonly Vector3[] Centres =
        {
            Vector3.zero,
            new Vector3(-88.75f, 87.11f, -608.67f),
            new Vector3(454.01f, 102.51f, -447.57f),
            new Vector3(139.55f, 102.38f, -201.39f),
            new Vector3(-229.89f, 121.50f, -239.87f),
            Vector3.zero,
            Vector3.zero,
        };

        // SavableSystem of each tower's opening button, by the towers above (-1: it has none).
        private static readonly string[] ButtonSystems =
        {
            null, "LookoutLightRed", "LookoutLightGreen", "LookoutLightBlue", "LookoutLightYellow", "BlackTowerInteriorDoor", null,
        };

        private static readonly SavableSystem[] ButtonEnums = ParseButtons();

        private static SavableSystem[] ParseButtons()
        {
            var result = new SavableSystem[ButtonSystems.Length];
            for (var i = 0; i < result.Length; i++)
                result[i] = ButtonSystems[i] != null && Enum.TryParse<SavableSystem>(ButtonSystems[i], out var parsed)
                    ? parsed
                    : SavableSystem.NotSavable;
            return result;
        }

        // On a guest: the towers open, one bit each (the order above), as the host last said.
        private static int _mirroredOpen = -1;

        private static readonly Dictionary<string, int> Learned = new();
        private static readonly HashSet<int> Blocked = new();
        private static int _lastActive = -2;

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        // The debug key's (DebugApFeed): a mode and an order of its own, to see the monuments hold without a server.
        internal static string DebugMode;
        // Drawn again at each start of the debug key's cycle (DebugApFeed): a seed draws its own.
        internal static void ShuffleDebugOrder()
        {
            var random = new System.Random();
            for (var i = DebugOrder.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (DebugOrder[i], DebugOrder[j]) = (DebugOrder[j], DebugOrder[i]);
            }
        }

        internal static readonly string[] DebugOrder =
        {
            "bigKeyRedZone", "bigKeyIntro", "bigKeyGreenZone", "bigKeyBlueZone", "bigKeyYellowZone", "bigKeyBoss", "bigKeyOverflow",
        };

        private static string Mode => DebugMode ?? (ApRuntime.SlotData != null ? ApRuntime.SlotData.TowerMode : "all_open");
        // `first_tower_open` off (the player, 2026-10-12): with by_item, the first monument waits for an item as well.
        internal static bool DebugFirstClosed;
        private static bool FirstTowerOpen => DebugMode != null ? !DebugFirstClosed : ApRuntime.SlotData == null || ApRuntime.SlotData.FirstTowerOpen;

        private static string[] Order => DebugMode != null ? DebugOrder : ApRuntime.SlotData != null ? ApRuntime.SlotData.TowerOrder : Array.Empty<string>();

        // Whether the monuments are held to an order on this machine at all.
        internal static bool Active => IsGuest ? _mirroredOpen >= 0 : Mode != "all_open" && Order.Length == Props.Length;

        internal static bool IsItem(long itemId) =>
            ApRuntime.SlotData != null && ApRuntime.SlotData.MonumentItemId != 0 && itemId == ApRuntime.SlotData.MonumentItemId;

        // The host's: one more monument opened by an item.
        internal static bool Grant()
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"{Tag} Ignored: a Progressive Monument received while not host.");
                return false;
            }

            var granted = SaveManager.GetIntValue(GrantedKey, 0, false) + 1;
            SaveManager.SetIntValue(GrantedKey, granted);
            Plugin.Log.LogInfo($"{Tag} Progressive Monument {granted} received.");
            ApNotices.Post("Progressive Monument: another monument can open");
            return true;
        }

        internal static void ClearLedger() => SaveManager.SetIntValue(GrantedKey, 0);

        private static int Granted => SaveManager.GetIntValue(GrantedKey, 0, false);

        // ----- the towers and their monuments

        private static int IndexOfProp(string prop) => Array.IndexOf(Props, prop);

        // What the loaded monuments (or an earlier session, in the save) have taught of `monoument0..3`.
        private static bool TryKnownTower(string prefix, out int tower)
        {
            if (Learned.TryGetValue(prefix, out tower))
                return true;

            var saved = SaveManager.GetIntValue(MapPrefix + prefix, 0, false);
            if (saved > 0)
            {
                tower = saved - 1;
                Learned[prefix] = tower;
                return true;
            }

            tower = -1;
            return false;
        }

        // A tower's slots as the seed counts them: the Green Dome gives up what the gourds cannot fill (the player, 2026-10-12).
        internal static int SlotsOf(int tower) =>
            tower == 6 && ApRuntime.SlotData != null ? Math.Min(Slots[6], ApRuntime.SlotData.GreenDomeSlots) : Slots[tower];

        internal static string PrefixOf(int tower)
        {
            if (tower == 0 || tower >= 5)
                return GuessedPrefixes[tower];

            for (var candidate = 1; candidate <= 4; candidate++)
            {
                if (TryKnownTower(GuessedPrefixes[candidate], out var known) && known == tower)
                    return GuessedPrefixes[candidate];
            }

            return GuessedPrefixes[tower];
        }

        private static int TowerOfPrefix(string prefix)
        {
            var guessed = Array.IndexOf(GuessedPrefixes, prefix);
            if (guessed == 0 || guessed >= 5)
                return guessed;

            return TryKnownTower(prefix, out var known) ? known : guessed;
        }

        internal static string PrefixOfHome(PropHome home)
        {
            var name = home.saveableHomeName.ToString();
            var slot = name.LastIndexOf("Slot", StringComparison.Ordinal);
            return slot > 0 ? name.Substring(0, slot) : name;
        }

        // Called for each monument slot as it loads (CosmeticMonumentFillTracker.RegisterHome): the four
        // `monoument0..3` are told apart by the tower they stand nearest, on the ground plan.
        internal static void Learn(PropHome home)
        {
            if (home == null || !ReceivedItemSpawner.IsMonumentHome(home))
                return;

            var prefix = PrefixOfHome(home);
            var guessed = Array.IndexOf(GuessedPrefixes, prefix);
            if (guessed < 1 || guessed > 4)
                return;

            var best = -1;
            var bestDistance = float.MaxValue;
            for (var tower = 1; tower <= 4; tower++)
            {
                var offset = home.transform.position - Centres[tower];
                offset.y = 0f;
                if (offset.sqrMagnitude < bestDistance)
                {
                    bestDistance = offset.sqrMagnitude;
                    best = tower;
                }
            }

            if (Learned.TryGetValue(prefix, out var known) && known == best)
                return;

            Learned[prefix] = best;
            SaveManager.SetIntValue(MapPrefix + prefix, best + 1);
            Plugin.Log.LogInfo(
                $"{Tag} {prefix} stands at {home.transform.position} nearest {Names[best]}"
                + (guessed == best ? " (as guessed)." : $" (not {Names[guessed]}, as guessed): corrected."));
        }

        // A tower's position in the apworld's TOWERS (the order above), or -1.
        internal static int TowerIndexOf(string prop) => IndexOfProp(prop);

        // L1 (0.6): once a second on the host, each monument whose fill is a check (`monument_full_keys`) and is now
        // full sends it ("monument:<prop>"); remembered like every check, so each reports once. A tower without slots
        // in this seed has no check (the apworld leaves it out).
        private static float _nextFullCheck;

        internal static void TickFullChecks()
        {
            if (!NetworkServer.active || Time.unscaledTime < _nextFullCheck || !WorldManager.isReadyForEffects)
                return;
            _nextFullCheck = Time.unscaledTime + 1f;

            var keys = ApRuntime.SlotData?.MonumentFullKeys;
            if (keys == null || keys.Length == 0)
                return;

            foreach (var prop in keys)
            {
                var tower = IndexOfProp(prop);
                if (tower < 0 || SlotsOf(tower) <= 0 || !IsFull(tower))
                    continue;
                var location = ApLocationIds.MonumentPrefix + prop;
                if (!CheckTracker.TryMarkReported(location))
                    continue;
                Plugin.Log.LogInfo($"{Tag} {Names[tower]}'s monument is full: {location}.");
                Plugin.Reporter.ReportCheck(location);
            }
        }

        internal static bool IsFull(int tower) =>
            CosmeticMonumentFillTracker.FilledSlots(PrefixOf(tower)) >= SlotsOf(tower);

        // ----- who is open

        // The host's own reckoning; a guest's is the mask it was told.
        internal static bool IsOpen(int tower)
        {
            if (!Active)
                return true;

            if (IsGuest)
                return (_mirroredOpen & (1 << tower)) != 0;

            var position = Array.IndexOf(Order, Props[tower]);
            if (position < 0)
                return true;

            for (var before = 0; before < position; before++)
            {
                var other = IndexOfProp(Order[before]);
                if (other >= 0 && !IsFull(other))
                    return false;
            }

            return Mode != "by_item" || Granted >= position + (FirstTowerOpen ? 0 : 1);
        }

        // A monument slot that must refuse a gourd on this machine: its tower is not open, and holds none
        // already (what an earlier game left in the wrong place is left alone).
        internal static bool IsClosed(PropHome home)
        {
            if (!Active || home == null || !ReceivedItemSpawner.IsMonumentHome(home))
                return false;

            // Asked while a player holds a gourd near the monument, every frame: remembered for a moment.
            var prefix = PrefixOfHome(home);
            var now = Time.unscaledTime;
            if (ClosedAt.TryGetValue(prefix, out var seen) && now - seen.at < 0.5f)
                return seen.closed;

            var tower = TowerOfPrefix(prefix);
            var closed = tower >= 0 && !IsOpen(tower) && CosmeticMonumentFillTracker.FilledSlots(prefix) == 0;
            ClosedAt[prefix] = (now, closed);
            return closed;
        }

        private static readonly Dictionary<string, (float at, bool closed)> ClosedAt = new();

        // The towers' opening buttons: a state raise on a closed tower's button is refused.
        internal static bool HoldsButton(TrackedPeckState state)
        {
            if (state == null || !Active)
                return false;

            var system = state.savableSystem;
            for (var tower = 1; tower <= 5; tower++)
            {
                if (ButtonEnums[tower] == system)
                    return !IsOpen(tower);
            }

            return false;
        }

        internal static byte HostMask()
        {
            if (!Active)
                return 0;

            var mask = 0x80;
            for (var tower = 0; tower < Props.Length; tower++)
            {
                if (IsOpen(tower))
                    mask |= 1 << tower;
            }

            return (byte)mask;
        }

        internal static void ApplyFromHost(byte mask) => _mirroredOpen = (mask & 0x80) != 0 ? mask & 0x7F : -1;

        internal static void ForgetMirror() => _mirroredOpen = -1;

        // The monuments in the order the seed opens them, for what restores deposits in a new save
        // (CosmeticMonumentFillTracker.PutDepositsBack); null when they are all open.
        internal static (string Prefix, int Slots)[] OrderedMonuments()
        {
            if (IsGuest || !Active)
                return null;

            var list = new List<(string, int)>();
            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower >= 0)
                    list.Add((PrefixOf(tower), SlotsOf(tower)));
            }

            return list.ToArray();
        }

        // The first n towers of the order are full (the objective `big_towers`).
        internal static bool TowersFull(int count)
        {
            if (!Active || IsGuest)
                return false;

            for (var position = 0; position < count && position < Order.Length; position++)
            {
                var tower = IndexOfProp(Order[position]);
                if (tower < 0 || !IsFull(tower))
                    return false;
            }

            return count > 0;
        }

        internal static int FullCount()
        {
            var count = 0;
            if (!Active || IsGuest)
                return 0;

            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower < 0 || !IsFull(tower))
                    break;
                count++;
            }

            return count;
        }

        // One line for the overlay: the tower that takes the gourds now and how many it still wants, or that no tower is open
        // (by_item: a Progressive Monument is missing). Null when there is no order or every tower is full. Read each frame,
        // and the fill walks the save: remembered for a second.
        private static string _hudLine;
        private static float _hudAt = -10f;

        internal static string HudLine()
        {
            if (!Active || IsGuest)
                return null;

            var now = Time.unscaledTime;
            if (now - _hudAt < 1f)
                return _hudLine;

            _hudAt = now;
            _hudLine = null;
            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower < 0 || IsFull(tower))
                    continue;

                if (!IsOpen(tower))
                {
                    _hudLine = Mode == "by_item"
                        ? "Towers: none open, a Progressive Monument is missing"
                        : "Towers: none open";
                    break;
                }

                _hudLine = $"Tower open: {Names[tower].Replace("the ", string.Empty)} ({CosmeticMonumentFillTracker.FilledSlots(PrefixOf(tower))}/{SlotsOf(tower)})";
                break;
            }

            return _hudLine;
        }

        // The board's lines: the order, each tower full, open or waiting.
        internal static string BoardText()
        {
            if (!Active || IsGuest)
                return string.Empty;

            // The order is a surprise (the player, 2026-10-12): the full towers and the open one are named, the rest is
            // counted, unless ShowTowerOrder asks for the whole list.
            var text = "<b>MONUMENTS</b>";
            var hidden = 0;
            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower < 0)
                    continue;

                var full = IsFull(tower);
                var open = IsOpen(tower);
                if (!full && !open && !ModConfig.ShowTowerOrder.Value)
                {
                    hidden++;
                    continue;
                }

                var state = full ? "[x]" : open ? " >" : "[  ]";
                text += $"\n{state} {Names[tower].Replace("the ", string.Empty)} ({CosmeticMonumentFillTracker.FilledSlots(PrefixOf(tower))}/{SlotsOf(tower)})";
            }

            if (hidden > 0)
                text += $"\n[  ] ??? ({hidden} more)";

            return text;
        }

        // Debug key (Alt + 3): the first monument of the order that is not full is filled, in the save: the deposits come
        // back as monuments do on a new save (CosmeticMonumentFillTracker.PutDepositsBack), so "this one is full, the next
        // opens" can be seen without placing five gourds.
        internal static void DebugFillNext()
        {
            if (!Active || IsGuest)
            {
                ApNotices.Post("Debug: no monument order to fill (Ctrl + 9)");
                return;
            }

            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower < 0 || IsFull(tower))
                    continue;

                var missing = SlotsOf(tower) - CosmeticMonumentFillTracker.FilledSlots(PrefixOf(tower));
                var marked = CosmeticMonumentFillTracker.PutDepositsBack(missing);
                Plugin.Log.LogInfo($"{Tag} Debug: {marked} slot(s) of {Names[tower]} marked filled.");
                ApNotices.Post($"Debug: {Names[tower]} filled ({marked} slots)");
                return;
            }

            ApNotices.Post("Debug: every monument is full");
        }

        // ----- the towers' buttons, hidden while their tower is closed (the player, 2026-10-12: a button that does nothing is
        // a lie). Measured with F9 at the Yellow tower: the button is `Lookouts/Lookout_<Colour>.../lighting and door/
        // BasicPokeButton`, its drawing and collider under `BasicPokeButtonModel`; its state (the network part) is left
        // alone, so opening the tower later needs nothing put back but the drawing.

        // The Black Tower's opening buttons (measured with F9 there, 2026-10-12): `BlackTower <n>Player/Positioner/NHoldFullSet/
        // NHoldSet<k>/Pivot/ButtonNHold<k>/Button-nHold-Four`, one model each, which carries a LODGroup too.

        private static readonly string[] ButtonColours = { null, "Red", "Green", "Blue", "Yellow", null, null };
        private static readonly List<Transform>[] ButtonModels = { new(), new(), new(), new(), new(), new(), new() };
        private static readonly Dictionary<Transform, Vector3>[] ButtonHidden =
            { new(), new(), new(), new(), new(), new(), new() };
        private static float _nextButtonSearch;

        private static void TickButtons(bool active)
        {
            for (var tower = 1; tower <= 5; tower++)
            {
                var closed = active && !IsOpen(tower);
                var hidden = ButtonHidden[tower];
                if (!closed && hidden.Count == 0)
                    continue;

                if (!closed)
                {
                    foreach (var pair in hidden)
                    {
                        if (pair.Key != null)
                            SetButton(pair.Key, pair.Value, false);
                    }

                    hidden.Clear();
                    Plugin.Log.LogInfo($"{Tag} The buttons of {Names[tower]} are back.");
                    continue;
                }

                // A tower that unloads and loads again brings new buttons (the Yellow one, 2026-10-12): the list is searched
                // again when one of its models is gone, or when none was found.
                var models = ButtonModels[tower];
                if ((models.Count == 0 || models.Exists(model => model == null)) && Time.unscaledTime >= _nextButtonSearch)
                    FindButtons();

                var count = 0;
                foreach (var model in models)
                {
                    if (model == null || hidden.ContainsKey(model))
                        continue;

                    hidden[model] = model.localScale;
                    SetButton(model, Vector3.zero, true);
                    count++;
                }

                if (count > 0)
                    Plugin.Log.LogInfo($"{Tag} {count} button(s) of {Names[tower]} hidden.");
            }
        }

        // The scale first: the model carries a LODGroup, which turns its renderers back on every frame (2026-10-12, measured:
        // `enabled = false` alone left the Green tower's button showing and pressable). A model at scale zero is neither seen
        // nor hit. The renderers and colliders are switched too, for what scale cannot reach.
        private static void SetButton(Transform model, Vector3 scale, bool hide)
        {
            model.localScale = scale;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = !hide;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = !hide;
        }

        private static void FindButtons()
        {
            _nextButtonSearch = Time.unscaledTime + 10f;
            foreach (var list in ButtonModels)
                list.RemoveAll(model => model == null);

            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null)
                    continue;

                var isPoke = transform.name == "BasicPokeButton";
                var isHold = transform.name == "Button-nHold-Four";
                if (!isPoke && !isHold)
                    continue;

                var path = string.Empty;
                for (var up = transform; up != null; up = up.parent)
                    path = up.name + "/" + path;

                if (isHold)
                {
                    if (path.Contains("/BlackTower ", StringComparison.Ordinal) && path.Contains("/NHoldFullSet/", StringComparison.Ordinal)
                        && !ButtonModels[5].Contains(transform))
                        ButtonModels[5].Add(transform);
                    continue;
                }

                if (!path.Contains("/Lookouts/Lookout_", StringComparison.Ordinal))
                    continue;

                for (var tower = 1; tower <= 4; tower++)
                {
                    if (!path.Contains("Lookout_" + ButtonColours[tower], StringComparison.OrdinalIgnoreCase))
                        continue;

                    var model = transform.Find("BasicPokeButtonModel");
                    model = model != null ? model : transform;
                    if (!ButtonModels[tower].Contains(model))
                        ButtonModels[tower].Add(model);
                }
            }
        }

        // ----- applying it, every couple of seconds (TeleportButtons.Sync), on every machine

        internal static void Tick()
        {
            if (!WorldManager.isReadyForEffects)
                return;

            // The scene's own homes, not `PropHome.allPropHomes`: no monument was ever found in that list (2026-10-12: the log
            // never said "blockPlacing on").
            var homes = UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsSortMode.None);
            if (homes == null)
                return;

            var active = Active;
            TickButtons(active);
            foreach (var home in homes)
            {
                if (home == null || !ReceivedItemSpawner.IsMonumentHome(home))
                    continue;

                var id = home.GetInstanceID();
                var closed = active && IsClosed(home);
                if (closed && !home.blockPlacing)
                {
                    home.blockPlacing = true;
                    Blocked.Add(id);
                    Plugin.Log.LogInfo($"{Tag} {home.saveableHomeName} is closed: blockPlacing on.");
                }
                else if (!closed && Blocked.Remove(id))
                {
                    home.blockPlacing = false;
                }
            }

            if (!active || IsGuest || !NetworkServer.active)
                return;

            // Said when the monument that takes gourds changes.
            var now = -1;
            foreach (var prop in Order)
            {
                var tower = IndexOfProp(prop);
                if (tower >= 0 && IsOpen(tower) && !IsFull(tower))
                {
                    now = tower;
                    break;
                }
            }

            if (now != _lastActive)
            {
                _lastActive = now;
                if (now >= 0)
                    ApNotices.Post($"The monument of {Names[now]} takes gourds now");
            }
        }
    }
}
