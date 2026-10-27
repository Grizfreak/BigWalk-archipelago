using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Q6, the whole mod's cost (player, 2026-10-13: the map tracker once made the game stutter). With Debug.PerfProbe
    // on, every Update, LateUpdate, FixedUpdate and OnGUI of the mod's own components is timed, and every
    // PerfSeconds the slowest are logged: the time per frame on average and the worst single call.
    internal static class PerfProbe
    {
        private const string Tag = "[" + nameof(PerfProbe) + "]";
        private const float PerfSeconds = 30f;
        private static readonly string[] Names = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };
        private static readonly Dictionary<MethodBase, (long total, long worst, int calls)> Times = new();
        private static readonly HashSet<MethodBase> Nested = new();
        private static readonly string[] StaticNames = { "Tick", "Sync", "Enforce", "RevealAll" };
        // A frame longer than this is logged with what the mod spent in it.
        private const float SlowFrameMs = 25f;
        private static readonly Dictionary<MethodBase, long> ThisFrame = new();
        private static float _since = -1f;
        private static int _frames;
        private static int _lastFrame = -1;

        internal static void TryApply(Harmony harmony)
        {
            if (!ModConfig.PerfProbe.Value)
                return;
            var prefix = new HarmonyMethod(typeof(PerfProbe), nameof(Prefix));
            var postfix = new HarmonyMethod(typeof(PerfProbe), nameof(Postfix));
            var count = 0;
            foreach (var type in typeof(PerfProbe).Assembly.GetTypes())
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                    continue;
                foreach (var name in Names)
                {
                    var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (method == null)
                        continue;
                    try
                    {
                        harmony.Patch(method, prefix, postfix);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"{Tag} {type.Name}.{name} not timed: {ex.Message}");
                    }
                }
            }
            // Static Ticks run inside the runners' Updates: timed alone, left out of the total.
            foreach (var type in typeof(PerfProbe).Assembly.GetTypes())
            foreach (var name in StaticNames)
            {
                var tick = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (tick == null || tick.ContainsGenericParameters)
                    continue;
                try
                {
                    harmony.Patch(tick, prefix, postfix);
                    Nested.Add(tick);
                    count++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} {type.Name}.{name} not timed: {ex.Message}");
                }
            }
            Plugin.Log.LogInfo($"{Tag} Timing {count} method(s).");
        }

        private static void Prefix(out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        private static void Postfix(MethodBase __originalMethod, long __state)
        {
            var spent = Stopwatch.GetTimestamp() - __state;
            Times.TryGetValue(__originalMethod, out var time);
            Times[__originalMethod] = (time.total + spent, Math.Max(time.worst, spent), time.calls + 1);

            if (Time.frameCount != _lastFrame)
            {
                LogIfSlow();
                _lastFrame = Time.frameCount;
                _frames++;
            }
            ThisFrame.TryGetValue(__originalMethod, out var inFrame);
            ThisFrame[__originalMethod] = inFrame + spent;
            if (_since < 0f)
                _since = Time.unscaledTime;
            if (Time.unscaledTime - _since < PerfSeconds)
                return;

            var ms = 1000.0 / Stopwatch.Frequency;
            var all = Times.Where(p => !Nested.Contains(p.Key)).Sum(p => p.Value.total) * ms / Math.Max(1, _frames);
            var lines = Times.OrderByDescending(p => p.Value.worst).Take(16)
                .Select(p => $"  {p.Key.DeclaringType?.Name}.{p.Key.Name}: {p.Value.total * ms / Math.Max(1, _frames):F3} ms/frame, worst {p.Value.worst * ms:F2} ms");
            Plugin.Log.LogInfo($"{Tag} {_frames} frames, the mod {all:F3} ms/frame in all:\n{string.Join("\n", lines)}");
            Times.Clear();
            _frames = 0;
            _since = Time.unscaledTime;
        }

        // On a frame's first timed call: the delta is the frame ThisFrame holds.
        private static void LogIfSlow()
        {
            var frameMs = Time.unscaledDeltaTime * 1000f;
            if (frameMs >= SlowFrameMs && ThisFrame.Count > 0)
            {
                var ms = 1000.0 / Stopwatch.Frequency;
                var mod = ThisFrame.Where(p => !Nested.Contains(p.Key)).Sum(p => p.Value) * ms;
                var top = ThisFrame.OrderByDescending(p => p.Value).Take(5)
                    .Select(p => $"{p.Key.DeclaringType?.Name}.{p.Key.Name} {p.Value * ms:F1}");
                Plugin.Log.LogInfo($"{Tag} slow frame {frameMs:F0} ms, the mod {mod:F1} ms: {string.Join(", ", top)}");
            }
            ThisFrame.Clear();
        }
    }
}
