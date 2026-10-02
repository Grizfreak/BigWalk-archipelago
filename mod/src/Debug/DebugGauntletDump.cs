using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // What the Silent Gauntlet is made of, for `gauntlet_mode` (0.1.3).
    //
    // The seven chambers are saved nowhere (`GauntletChamber0..6` never found
    // written), so each stage's puzzle, its inner door and its collective door
    // have to be told apart by their wiring: which TrackedPeckState a door
    // listens to, which switch or combinator writes it. One press writes every
    // piece of that wiring within a radius of the player to a tab-separated
    // file, `BepInEx/gauntlet-dump-<n>.tsv`, and one log line says where.
    //
    // Pure diagnostic, no writes to the game. Meant to be pressed twice per
    // stage — before and after the puzzle, then before and after the collective
    // button — so two files can be compared on their `state` column: what
    // changed is what that step drives.
    //
    // Every object is read inside its own try/catch and the file is written in
    // one go at the end; F7 once froze the game near this area, so nothing
    // here walks anything it does not have to.
    internal static class DebugGauntletDump
    {
        private static int _count;

        internal static void Dump(float radius, string label)
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugGauntletDump)}] Local player not found.");
                return;
            }

            _count++;
            var origin = player.transform.position;
            var text = new StringBuilder();
            text.AppendLine($"# dump {_count} label='{label}' radius={radius} player={Format(origin)}");
            text.AppendLine("kind\tname\tpath\tx\ty\tz\tdistance\tstate\tlink\tdetail");

            var listeners = new Dictionary<TrackedPeckState, List<string>>();
            var rows = new List<string>();

            Collect<PeckEffectToggle>(origin, radius, rows, listeners, "Toggle",
                e => e.peckSystemReference.peckSystem,
                e => "targets=" + string.Join(",", TargetPaths(e)));
            Collect<PeckEffectAnimancer>(origin, radius, rows, listeners, "Animancer",
                e => e.peckSystemReference.peckSystem, e => string.Empty);
            Collect<PeckEffectAnimator>(origin, radius, rows, listeners, "Animator",
                e => e.systemReference.peckSystem, e => $"param={e.parameterName}");
            Collect<PeckEffectTween>(origin, radius, rows, listeners, "Tween",
                e => e.peckSystemReference.peckSystem, e => string.Empty);

            var states = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (states != null)
            {
                foreach (var state in states)
                {
                    try
                    {
                        if (state == null || !Within(state.transform, origin, radius))
                            continue;
                        var heard = listeners.TryGetValue(state, out var list) ? string.Join(" | ", list) : "<none>";
                        rows.Add(Row("State", state.transform, origin,
                            state.currentPeckContext.state.ToString(CultureInfo.InvariantCulture),
                            $"savable={state.savableSystem}",
                            "listeners=" + heard));
                    }
                    catch (Exception ex)
                    {
                        Skipped("TrackedPeckState", ex);
                    }
                }
            }

            var switches = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (switches != null)
            {
                foreach (var peckSwitch in switches)
                {
                    try
                    {
                        if (peckSwitch == null || !Within(peckSwitch.transform, origin, radius))
                            continue;
                        var target = peckSwitch.trackedStateSystem;
                        rows.Add(Row("Switch", peckSwitch.transform, origin, string.Empty,
                            target != null ? PathOf(target.transform) : "<null>",
                            $"specificState={peckSwitch.specificState} mode={peckSwitch.stateMode} needsKey={peckSwitch.needsKey}"));
                    }
                    catch (Exception ex)
                    {
                        Skipped("PeckSwitch", ex);
                    }
                }
            }

            var presses = UnityEngine.Object.FindObjectsByType<SimPressSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (presses != null)
            {
                foreach (var press in presses)
                {
                    try
                    {
                        if (press == null || !Within(press.transform, origin, radius))
                            continue;
                        rows.Add(Row("SimPress", press.transform, origin, string.Empty,
                            "press=" + (press.pressSystem != null ? PathOf(press.pressSystem.transform) : "<null>"),
                            "state=" + (press.stateSystem != null ? PathOf(press.stateSystem.transform) : "<null>")));
                    }
                    catch (Exception ex)
                    {
                        Skipped("SimPressSwitch", ex);
                    }
                }
            }

            var combinators = UnityEngine.Object.FindObjectsByType<PeckCombinator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (combinators != null)
            {
                foreach (var combinator in combinators)
                {
                    try
                    {
                        if (combinator == null || !Within(combinator.transform, origin, radius))
                            continue;
                        rows.Add(Row("Combinator", combinator.transform, origin, string.Empty,
                            "out=" + Outputs(combinator),
                            "rules=" + Rules(combinator)));
                    }
                    catch (Exception ex)
                    {
                        Skipped("PeckCombinator", ex);
                    }
                }
            }

            rows.Sort(StringComparer.Ordinal);
            foreach (var row in rows)
                text.AppendLine(row);

            var path = Path.Combine(Paths.BepInExRootPath, $"gauntlet-dump-{_count}.tsv");
            File.WriteAllText(path, text.ToString());
            Plugin.Log.LogInfo(
                $"[{nameof(DebugGauntletDump)}] #{_count} '{label}' at {Format(origin)}: {rows.Count} row(s) within {radius:0}m written to {path}");
        }

        private static void Collect<T>(
            Vector3 origin, float radius, List<string> rows, Dictionary<TrackedPeckState, List<string>> listeners,
            string kind, Func<T, TrackedPeckState> stateOf, Func<T, string> detailOf) where T : Component
        {
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null)
                return;

            foreach (var effect in found)
            {
                try
                {
                    if (effect == null || !Within(effect.transform, origin, radius))
                        continue;

                    var state = stateOf(effect);
                    rows.Add(Row(kind, effect.transform, origin, string.Empty,
                        state != null ? PathOf(state.transform) : "<null>", detailOf(effect)));

                    if (state == null)
                        continue;
                    if (!listeners.TryGetValue(state, out var list))
                        listeners[state] = list = new List<string>();
                    list.Add($"{kind}:{PathOf(effect.transform)}");
                }
                catch (Exception ex)
                {
                    Skipped(kind, ex);
                }
            }
        }

        private static string Outputs(PeckCombinator combinator)
        {
            var parts = new List<string>();
            if (combinator.directControlSystem != null)
                parts.Add("direct=" + PathOf(combinator.directControlSystem.transform));
            if (combinator.onConditionMet != null)
                parts.Add("met=" + PathOf(combinator.onConditionMet.transform));
            if (combinator.onConditionStop != null)
                parts.Add("stop=" + PathOf(combinator.onConditionStop.transform));
            return parts.Count == 0 ? "<none>" : string.Join(",", parts);
        }

        private static string Rules(PeckCombinator combinator)
        {
            var rules = combinator.rules;
            if (rules == null || rules.Length == 0)
                return "<none>";

            var parts = new List<string>();
            foreach (var rule in rules)
            {
                var names = new List<string>();
                if (rule.systems != null)
                    foreach (var system in rule.systems)
                        if (system != null)
                            names.Add(system.gameObject.name);
                if (rule.block != null && rule.block.systems != null)
                    foreach (var system in rule.block.systems)
                        if (system != null)
                            names.Add(system.gameObject.name);
                parts.Add($"min{rule.minimumMatches}@{rule.desiredState}[{string.Join(",", names)}]");
            }

            return string.Join(";", parts);
        }

        private static IEnumerable<string> TargetPaths(PeckEffectToggle toggle)
        {
            if (toggle.target != null)
                yield return PathOf(toggle.target);
            if (toggle.targets != null)
                foreach (var target in toggle.targets)
                    if (target != null)
                        yield return PathOf(target);
        }

        private static bool Within(Transform transform, Vector3 origin, float radius)
        {
            return (transform.position - origin).sqrMagnitude <= radius * radius;
        }

        private static string Row(string kind, Transform transform, Vector3 origin, string state, string link, string detail)
        {
            var p = transform.position;
            return string.Join("\t", new[]
            {
                kind, transform.name, PathOf(transform),
                p.x.ToString("F1", CultureInfo.InvariantCulture),
                p.y.ToString("F1", CultureInfo.InvariantCulture),
                p.z.ToString("F1", CultureInfo.InvariantCulture),
                Vector3.Distance(p, origin).ToString("F1", CultureInfo.InvariantCulture),
                state, link, detail,
            });
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Format(Vector3 v)
        {
            return $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
        }

        private static void Skipped(string what, Exception ex)
        {
            Plugin.Log.LogWarning($"[{nameof(DebugGauntletDump)}] {what} skipped: {ex.Message}");
        }
    }
}
