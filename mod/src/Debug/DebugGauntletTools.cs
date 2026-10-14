using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // The Silent Gauntlet without solving it (player, 2026-10-14: testing the goal alone, the top bell would not break
    // with the combinator force while only the first stage was solved). Host only.
    //
    //   Alt + 7   the stage the player stands in (Level0..6, the nearest by height) solved and its stairway open;
    //   Alt + 8   all seven stages solved and every stairway open;
    //   Alt + 9   the top bell (Level7): its `NHoldSucess` switch pecked, as the two held buttons do; with nothing
    //             written after a moment, `GauntletEndingLogicGroup/DevTrigger` (the game's own developer trigger).
    //
    // A stage solved is its `GauntletChamber<N>` system set to 1, which the game writes itself when the puzzle is done
    // (the stage's check, and the wall inside the stage); its stairway is the `GourdTowerGateNetworking` state set to 1,
    // or its item granted when `gauntlet_mode: locked_stages` holds it (Core/GauntletStairways). Every step is logged
    // with the states before and after.
    internal class DebugGauntletTools : MonoBehaviour
    {
        private const string Tag = "[" + nameof(DebugGauntletTools) + "]";
        private const string GauntletPath = "SilentGauntlet";
        private const string GateName = "GourdTowerGateNetworking";

        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut StageKey = new(KeyCode.Alpha7, KeyCode.LeftAlt);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut AllStagesKey = new(KeyCode.Alpha8, KeyCode.LeftAlt);
        private static readonly BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut BellKey = new(KeyCode.Alpha9, KeyCode.LeftAlt);

        private float _devTriggerAt = -1f;

        public DebugGauntletTools(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            try
            {
                if (_devTriggerAt > 0f && Time.unscaledTime >= _devTriggerAt)
                {
                    _devTriggerAt = -1f;
                    DevTriggerIfStillNotDone();
                }

                var stage = StageKey.IsDown();
                var all = AllStagesKey.IsDown();
                var bell = BellKey.IsDown();
                if (!stage && !all && !bell || !WorldManager.isReadyForEffects)
                    return;

                if (!NetworkServer.active)
                {
                    ApNotices.Post("Gauntlet tools: host only", warning: true);
                    return;
                }

                if (bell)
                {
                    RingTheBell();
                    return;
                }

                var parts = Parts();
                if (all)
                {
                    for (var level = 0; level < GauntletStairways.Chambers.Length; level++)
                        Solve(level, parts);
                    ApNotices.Post("Gauntlet: all seven stages solved, stairways open");
                    return;
                }

                var here = NearestLevel(parts);
                if (here < 0)
                {
                    ApNotices.Post("Gauntlet: not in a stage (Level0..6 not found)", warning: true);
                    return;
                }

                Solve(here, parts);
                ApNotices.Post($"Gauntlet: stage {here + 1} solved, stairway open");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex}");
            }
        }

        private sealed class StageParts
        {
            internal TrackedPeckState Chamber;
            internal TrackedPeckState Gate;
            internal Transform Level;
        }

        // Each stage's puzzle system, stairway state and Level transform, from the active map version's Gauntlet.
        private static Dictionary<int, StageParts> Parts()
        {
            var parts = new Dictionary<int, StageParts>();
            StageParts Of(int level) => parts.TryGetValue(level, out var p) ? p : parts[level] = new StageParts();

            foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (state == null || !state.gameObject.activeInHierarchy)
                    continue;
                var chamber = Array.IndexOf(GauntletStairways.Chambers, state.savableSystem);
                if (chamber >= 0)
                {
                    Of(chamber).Chamber ??= state;
                    continue;
                }

                if (state.gameObject.name != GateName)
                    continue;
                var level = LevelOf(state.transform, out _);
                if (level >= 0 && level < GauntletStairways.Chambers.Length)
                    Of(level).Gate ??= state;
            }

            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith("Level", StringComparison.Ordinal) || !int.TryParse(t.name.Substring(5), out var level)
                    || level >= GauntletStairways.Chambers.Length || !UnderGauntlet(t))
                    continue;
                Of(level).Level ??= t;
            }

            return parts;
        }

        private static bool UnderGauntlet(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name.StartsWith(GauntletPath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // The Level<n> a transform sits under, inside the Gauntlet; -1 outside.
        private static int LevelOf(Transform t, out Transform levelTransform)
        {
            levelTransform = null;
            var level = -1;
            for (var p = t; p != null; p = p.parent)
            {
                if (level < 0 && p.name.StartsWith("Level", StringComparison.Ordinal) && int.TryParse(p.name.Substring(5), out var n))
                {
                    level = n;
                    levelTransform = p;
                }

                if (p.name.StartsWith(GauntletPath, StringComparison.Ordinal))
                    return level;
            }

            return -1;
        }

        // The stage the player stands in: the Level whose floor is the highest one under the player's feet.
        private static int NearestLevel(Dictionary<int, StageParts> parts)
        {
            var local = NetworkClient.localPlayer;
            if (local == null)
                return -1;
            var y = local.transform.position.y;
            var best = -1;
            var bestY = float.MinValue;
            foreach (var pair in parts)
            {
                var level = pair.Value.Level;
                if (level == null)
                    continue;
                var floor = level.position.y;
                if (floor <= y + 1f && floor > bestY)
                {
                    best = pair.Key;
                    bestY = floor;
                }
            }

            return best;
        }

        private static void Solve(int level, Dictionary<int, StageParts> parts)
        {
            if (!parts.TryGetValue(level, out var stage))
            {
                Plugin.Log.LogWarning($"{Tag} Stage {level + 1}: nothing of it found.");
                return;
            }

            if (stage.Chamber != null)
            {
                var before = stage.Chamber.currentPeckContext.compressedState;
                if (before < 1)
                    stage.Chamber.SetState(1);
                Plugin.Log.LogInfo($"{Tag} Stage {level + 1}: {GauntletStairways.Chambers[level]} {before} -> "
                                   + $"{stage.Chamber.currentPeckContext.compressedState} ({stage.Chamber.gameObject.name}).");
            }
            else
                Plugin.Log.LogWarning($"{Tag} Stage {level + 1}: no {GauntletStairways.Chambers[level]} system found.");

            if (GauntletStairways.Enabled && !GauntletStairways.IsGranted(level))
            {
                GauntletStairways.Grant(level);
                Plugin.Log.LogInfo($"{Tag} Stage {level + 1}: stairway item granted (locked_stages).");
            }
            else if (stage.Gate != null)
            {
                var before = stage.Gate.currentPeckContext.compressedState;
                if (before < 1)
                    stage.Gate.SetState(1);
                Plugin.Log.LogInfo($"{Tag} Stage {level + 1}: stairway {before} -> {stage.Gate.currentPeckContext.compressedState}.");
            }
            else
                Plugin.Log.LogWarning($"{Tag} Stage {level + 1}: no {GateName} found.");
        }

        private static bool BellDone() => SaveManager.GetIntValue(SavableSystem.GauntletComplete.ToString(), 0, false) != 0;

        // The top bell: what the two held buttons trigger (the combinator's onConditionMet, `NHoldSucess`), pecked.
        private void RingTheBell()
        {
            if (BellDone())
            {
                ApNotices.Post("Gauntlet: GauntletComplete is already 1");
                Plugin.Log.LogInfo($"{Tag} GauntletComplete already 1.");
                return;
            }

            var success = TopSwitch("NHoldSucess", inNHoldAnchor: true);
            if (success == null)
            {
                Plugin.Log.LogWarning($"{Tag} No Level7/NHoldAnchor/NHoldSucess switch found; trying the DevTrigger now.");
                DevTriggerIfStillNotDone();
                return;
            }

            var tracked = success.trackedStateSystem;
            Plugin.Log.LogInfo($"{Tag} Pecking {Path(success.transform)} (its state "
                               + $"{(tracked != null ? tracked.currentPeckContext.compressedState.ToString() : "none")}).");
            success.Peck();
            Plugin.Log.LogInfo($"{Tag} Pecked; its state now {(tracked != null ? tracked.currentPeckContext.compressedState.ToString() : "none")}.");
            ApNotices.Post("Gauntlet: the bell's switch pecked; the DevTrigger follows in 5 s if nothing is written");
            _devTriggerAt = Time.unscaledTime + 5f;
        }

        private static void DevTriggerIfStillNotDone()
        {
            if (BellDone())
            {
                Plugin.Log.LogInfo($"{Tag} GauntletComplete written: the bell is done.");
                ApNotices.Post("Gauntlet: GauntletComplete written");
                return;
            }

            var dev = TopSwitch("DevTrigger", inNHoldAnchor: false);
            if (dev == null)
            {
                Plugin.Log.LogWarning($"{Tag} No Level7/GauntletEndingLogicGroup/DevTrigger found either.");
                ApNotices.Post("Gauntlet: the bell did not ring, and no DevTrigger found", warning: true);
                return;
            }

            Plugin.Log.LogInfo($"{Tag} GauntletComplete still 0: pecking {Path(dev.transform)}.");
            dev.Peck();
            ApNotices.Post("Gauntlet: the DevTrigger pecked");
        }

        // A switch of this name in the active Gauntlet's Level7 (the top), under NHoldAnchor or GauntletEndingLogicGroup.
        private static PeckSwitch TopSwitch(string name, bool inNHoldAnchor)
        {
            var group = inNHoldAnchor ? "NHoldAnchor" : "GauntletEndingLogicGroup";
            return UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(sw => sw != null && sw.gameObject.name == name && sw.gameObject.activeInHierarchy)
                .FirstOrDefault(sw =>
                {
                    var path = Path(sw.transform);
                    return path.Contains(GauntletPath, StringComparison.Ordinal) && path.Contains("/Level7/", StringComparison.Ordinal)
                           && path.Contains(group, StringComparison.Ordinal);
                });
        }

        private static string Path(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }
    }
}
