using System;
using System.Collections.Generic;
using System.Globalization;
using BigWalkArchipelago.Core;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Trying the traps and bonuses without waiting for their items (Ctrl + Keypad 0), seeing what
    // they see (Ctrl + Keypad Enter), and marking the places Big Trip should know (Ctrl + Keypad .).
    internal static class DebugTraps
    {
        private const string Tag = "[" + nameof(DebugTraps) + "]";

        private static readonly string[] Order =
        {
            TrapEffects.Drop, TrapEffects.Throw, TrapEffects.Trip, TrapEffects.Meeting,
            TrapEffects.Night, TrapEffects.Sleep, TrapEffects.Load, TrapEffects.Mask, TrapEffects.Flare, TrapEffects.Day, TrapEffects.Speed, TrapEffects.Jump,
        };

        private static int _next;

        internal static void FireNext()
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"{Tag} Only the host fires a trap.");
                return;
            }

            var key = Order[_next % Order.Length];
            _next++;
            Plugin.Log.LogInfo($"{Tag} Firing {TrapEffects.DisplayName(key)} by hand.");
            Core.Net.ApNotices.Post($"Debug: {TrapEffects.DisplayName(key)}");
            Traps.Fire(key);
        }

        internal static void LogState()
        {
            Plugin.Log.LogInfo($"{Tag} This machine: {TrapEffects.Describe()}");
            Plugin.Log.LogInfo($"{Tag} {Traps.Describe()}");
            DumpTomato();
            DumpNearbyHomes();
        }

        // Every prop within 3 m of this player and where it is kept: for Big Load, to see where a
        // pack's contents live (2026-10-06).
        private static void DumpNearbyHomes()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
                return;

            var lines = new List<string> { $"{Tag} Props within 3 m and their homes:" };
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (prop == null || Vector3.Distance(prop.transform.position, player.transform.position) > 3f)
                    continue;

                var home = prop.currentHome;
                lines.Add($"    {prop.gameObject.name} parent='{(prop.transform.parent != null ? prop.transform.parent.name : "-")}' "
                          + $"home={(home != null ? Path(home.transform) + $" pinned={(home.pinnedProp != null ? home.pinnedProp.gameObject.name : "-")} inventory={home.isInventory}" : "-")}");
            }

            Plugin.Log.LogInfo(string.Join(Environment.NewLine, lines));
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent)
                parts.Insert(0, x.name);
            return string.Join("/", parts);
        }

        // How a timed tomato is wired, for the trap tomatoes: the first one in a dispenser, then
        // every trap clone still in the world. Every switch (what state it moves, how), every
        // timer (what it listens to, for how long, what it pecks at the end), every state.
        private static void DumpTomato()
        {
            var shown = 0;
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || !prop.gameObject.name.StartsWith("Pomodoro", StringComparison.Ordinal))
                    continue;

                var clone = prop.gameObject.name.Contains("(AP", StringComparison.Ordinal);
                if (!clone && shown > 0)
                    continue;
                shown++;

                var root = prop.transform;
                var lines = new List<string> { $"{Tag} Tomato '{prop.gameObject.name}' active={prop.gameObject.activeInHierarchy}:" };
                foreach (var state in root.GetComponentsInChildren<TrackedPeckState>(true))
                    lines.Add($"    state on '{Rel(root, state.transform)}' label='{state.label}' value={state.currentPeckContext.state} ticket={state.ticket} savable={state.savableSystem}");
                foreach (var sw in root.GetComponentsInChildren<PeckSwitch>(true))
                    lines.Add($"    switch on '{Rel(root, sw.transform)}' -> state '{(sw.trackedStateSystem != null ? Rel(root, sw.trackedStateSystem.transform) : "-")}' mode={sw.stateMode} specific={sw.specificState} ticket={sw.ticket} useTicket={sw.useTicket}");
                foreach (var timer in root.GetComponentsInChildren<PeckEffectTimer>(true))
                    lines.Add($"    timer on '{Rel(root, timer.transform)}' listens '{(timer.trackedStateSystem != null ? Rel(root, timer.trackedStateSystem.transform) : "-")}' filter={timer.stateFilter.filterType}"
                              + $"[{(timer.stateFilter.specificStates != null ? string.Join(",", timer.stateFilter.specificStates) : "")}] duration={timer.duration} remaining={timer.GetTimeRemaining()} onFinish='{(timer.onFinish != null ? Rel(root, timer.onFinish.transform) : "-")}'");
                // Every component, by object: where the tick could come from besides the effects.
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var types = new List<string>();
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        var type = component != null ? component.GetIl2CppType().Name : null;
                        if (type != null && type != "Transform")
                            types.Add(type);
                    }

                    if (types.Count > 0)
                        lines.Add($"    '{Rel(root, transform)}' active={transform.gameObject.activeSelf}: {string.Join(", ", types)}");
                }

                foreach (var animation in root.GetComponentsInChildren<TimerAnimation>(true))
                    lines.Add($"    TimerAnimation on '{Rel(root, animation.transform)}' timer='{(animation.timer != null ? Rel(root, animation.timer.transform) : "-")}' speed={animation.animationSpeed}");

                Plugin.Log.LogInfo(string.Join(Environment.NewLine, lines));
            }
        }

        private static string Rel(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent)
                parts.Insert(0, x.name);
            return parts.Count == 0 ? "." : string.Join("/", parts);
        }

        internal static void MarkTripSpot()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                Plugin.Log.LogInfo($"{Tag} No local player.");
                return;
            }

            var position = player.transform.position;
            var nearest = NearestPuzzle(position, out var distance);
            var line = string.Join("\t",
                position.x.ToString("F2", CultureInfo.InvariantCulture),
                position.y.ToString("F2", CultureInfo.InvariantCulture),
                position.z.ToString("F2", CultureInfo.InvariantCulture),
                player.transform.eulerAngles.y.ToString("F0", CultureInfo.InvariantCulture),
                nearest ?? "-",
                distance.ToString("F1", CultureInfo.InvariantCulture),
                Gauntlet.Contains(position) ? "gauntlet" : string.Empty);

            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "trip-spots.txt"), line + Environment.NewLine);
                Plugin.Log.LogInfo($"{Tag} Trip spot marked: {line}");
                Core.Net.ApNotices.Post($"Trip spot marked near {nearest ?? "nothing known"}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not write the spot: {ex.Message}");
            }
        }

        private static string NearestPuzzle(Vector3 position, out float nearestDistance)
        {
            string nearest = null;
            nearestDistance = float.MaxValue;
            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gourds == null)
                return null;

            foreach (var gourd in gourds)
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                    continue;

                var distance = Vector3.Distance(position, prop.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = prop.saveablePropName.ToString();
                }
            }

            return nearest;
        }
    }
}
