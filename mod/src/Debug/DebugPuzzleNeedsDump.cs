using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // What each puzzle is built from, as the game's own components say it —
    // the table `apworld/bigwalk/data.py` (PUZZLE_TAGS) needs to be switched
    // on and off in game. Written 2026-10-02 for `lock_puzzle_needs`.
    //
    // The puzzles are assembled from generic parts: a button is a PeckSwitch,
    // a synchronized button a SimPressSwitch, a panel a PegTileRenderer (what
    // it shows is its PropGroup and its PegTileSet), a timer a PeckEffectTimer.
    // Nothing names the puzzle a part belongs to, so each part is written with
    // its position and the nearest puzzle gourd, and the table is read off
    // the result.
    //
    // Everything is loaded everywhere (DebugGourdLookup.LogAllLoaded,
    // 2026-09-21), so one run on the title screen's far side is enough. The
    // output is a tab-separated file, not log lines: hundreds of PeckSwitch.
    internal class DebugPuzzleNeedsDump : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public DebugPuzzleNeedsDump(IntPtr ptr) : base(ptr)
        {
        }

        private const float DelayAfterReadySeconds = 12f;
        private const string FileName = "puzzle-needs-dump.tsv";

        private float _readySince = -1f;
        private bool _done;

        private void Update()
        {
            if (!WorldManager.isReadyForEffects)
            {
                _readySince = -1f;
                _done = false;
                return;
            }

            if (_done)
                return;

            if (_readySince < 0f)
                _readySince = Time.time;

            if (Time.time - _readySince < DelayAfterReadySeconds)
                return;

            _done = true;
            try
            {
                Dump();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPuzzleNeedsDump)}] Dump failed: {ex}");
            }
        }

        private static void Dump()
        {
            var puzzles = FindPuzzles();
            var rows = new List<string[]>();

            Add<PeckSwitch>(rows, puzzles, "PeckSwitch", s => s.transform, s =>
                $"state={Describe(() => s.trackedStateSystem != null ? s.trackedStateSystem.label : "<none>")}"
                + $" mode={s.stateMode} needsKey={s.needsKey} keyType={s.keyType}"
                + $" sim={(s.GetComponentInParent<SimPressSwitch>() != null)}");

            Add<SimPressSwitch>(rows, puzzles, "SimPressSwitch", s => s.transform, s => string.Empty);
            Add<SimPressController>(rows, puzzles, "SimPressController", s => s.transform,
                s => $"switches={Describe(() => s.switches.Length.ToString())}");

            Add<PegTileRenderer>(rows, puzzles, "PegTileRenderer", s => s.transform,
                s => $"propGroup={s.propGroup} hidden={s.hidden}");
            Add<PegTileSequenceGenerator>(rows, puzzles, "PegTileSequenceGenerator", s => s.transform,
                s => $"set={s.pegTileSet} length={s.sequenceLength}");
            Add<PegTileSequenceSetter>(rows, puzzles, "PegTileSequenceSetter", s => s.transform,
                s => $"renderers={Describe(() => s.pegTileRenderers.Length.ToString())}"
                    + $" flaps={Describe(() => s.splitFlapGlyphs.Length.ToString())}"
                    + $" music={Describe(() => s.musicConnectors.Length.ToString())}");
            Add<PegTileSetter>(rows, puzzles, "PegTileSetter", s => s.transform, s => $"set={s.tileSet}");
            Add<PegTileMusicConnector>(rows, puzzles, "PegTileMusicConnector", s => s.transform,
                s => $"propGroup={s.propGroup}");
            Add<PegTileValidator>(rows, puzzles, "PegTileValidator", s => s.transform, s => string.Empty);

            Add<PeckEffectTimer>(rows, puzzles, "PeckEffectTimer", s => s.transform, s => $"duration={s.duration.ToString("F1", CultureInfo.InvariantCulture)}");
            Add<PeckEffectTimerNetworked>(rows, puzzles, "PeckEffectTimerNetworked", s => s.transform,
                s => string.Empty);
            Add<PeckEffectMask>(rows, puzzles, "PeckEffectMask", s => s.transform, s => $"mask={s.maskType}");
            Add<PeckEffectTextInput>(rows, puzzles, "PeckEffectTextInput", s => s.transform, s => string.Empty);
            Add<PeckEffectBlockSwitch>(rows, puzzles, "PeckEffectBlockSwitch", s => s.transform, s => string.Empty);

            Add<HouseLight>(rows, puzzles, "HouseLight", s => s.transform, s => string.Empty);
            Add<ConductorLight>(rows, puzzles, "ConductorLight", s => s.transform, s => string.Empty);
            Add<ConductorPanel>(rows, puzzles, "ConductorPanel", s => s.transform, s => string.Empty);
            Add<CoordinatesHelper>(rows, puzzles, "CoordinatesHelper", s => s.transform, s => string.Empty);
            Add<CapsuleBomb>(rows, puzzles, "CapsuleBomb", s => s.transform, s => string.Empty);

            // Props that are carried: the group is what a pick-up refusal
            // (lock_pickups, R2) would key on.
            var wantedGroups = new HashSet<string> { "TimerBall", "CannonBall", "CannonFireable", "Blindfold" };
            Add<Prop>(rows, puzzles, "Prop", s => s.transform, s =>
                $"name={s.saveablePropName} groups={string.Join("+", Groups(s))}",
                s => Groups(s).Any(wantedGroups.Contains));

            var path = Path.Combine(Paths.BepInExRootPath, FileName);
            var text = new StringBuilder();
            text.AppendLine("kind\tpath\tx\ty\tz\tnearest_puzzle\tdistance\tdetails");
            foreach (var row in rows.OrderBy(r => r[5], StringComparer.Ordinal).ThenBy(r => r[0], StringComparer.Ordinal))
                text.AppendLine(string.Join("\t", row));
            File.WriteAllText(path, text.ToString());

            Plugin.Log.LogInfo($"[{nameof(DebugPuzzleNeedsDump)}] {rows.Count} part(s) written to {path}");

            DumpLightSwitches(puzzles);
        }

        // Which lamps does each light switch command? The switch writes a
        // TrackedPeckState, and the effects listening to that state are the
        // answer: a toggle's targets, a material property's block, a tween.
        // A state can also sit on a PeckBus shared with other blocks, which
        // is how one switch can light a whole building, so the bus is named.
        private static void DumpLightSwitches(List<(string name, Vector3 position)> puzzles)
        {
            var effects = new List<(TrackedPeckState state, string kind, string path, string detail)>();

            void Collect<T>(string kind, Func<T, TrackedPeckState> stateOf, Func<T, Transform> where, Func<T, string> detail)
                where T : UnityEngine.Object
            {
                var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found == null)
                    return;
                foreach (var effect in found)
                {
                    try
                    {
                        var state = stateOf(effect);
                        if (state != null)
                            effects.Add((state, kind, PathOf(where(effect)), detail(effect)));
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            Collect<PeckEffectToggle>("Toggle", e => e.peckSystemReference.peckSystem, e => e.transform,
                e => "targets=" + string.Join(",", TargetPaths(e)));
            Collect<PeckEffectMaterialProperty>("MaterialProperty", e => e.systemReference.peckSystem, e => e.transform,
                e => $"preset={e.presetName} global={e.isGlobal}");
            Collect<PeckEffectTween>("Tween", e => e.peckSystemReference.peckSystem, e => e.transform, e => string.Empty);
            Collect<PeckEffectAnimator>("Animator", e => e.systemReference.peckSystem, e => e.transform,
                e => $"param={e.parameterName}");

            var text = new StringBuilder();
            text.AppendLine("switch	switch_path	x	y	z	nearest_puzzle	state_path	bus	effect_kind	effect_path	effect_detail");

            var switches = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var count = 0;
            foreach (var peckSwitch in switches)
            {
                try
                {
                    if (peckSwitch == null || peckSwitch.trackedStateSystem == null)
                        continue;
                    var root = peckSwitch.transform;
                    for (var t = peckSwitch.transform; t != null; t = t.parent)
                        if (t.name.StartsWith("BasicLightSwitch", StringComparison.Ordinal))
                            root = t;
                    if (!root.name.StartsWith("BasicLightSwitch", StringComparison.Ordinal))
                        continue;

                    count++;
                    var state = peckSwitch.trackedStateSystem;
                    var position = root.position;
                    var (nearest, _) = Nearest(puzzles, position);
                    var bus = Describe(() =>
                    {
                        var connection = state.GetComponentInParent<PeckBusConnection>();
                        return connection != null && connection.peckBus != null
                            ? $"{connection.peckBus.name}({connection.peckBus.blocks.Count} blocks)"
                            : "<none>";
                    });

                    var listening = effects.Where(e => e.state == state).ToList();
                    var prefix = string.Join("	", new[]
                    {
                        root.name, PathOf(root),
                        position.x.ToString("F1", CultureInfo.InvariantCulture),
                        position.y.ToString("F1", CultureInfo.InvariantCulture),
                        position.z.ToString("F1", CultureInfo.InvariantCulture),
                        nearest, PathOf(state.transform), bus,
                    });
                    if (listening.Count == 0)
                        text.AppendLine(prefix + "	<none>		");
                    foreach (var effect in listening)
                        text.AppendLine($"{prefix}	{effect.kind}	{effect.path}	{effect.detail}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPuzzleNeedsDump)}] light switch skipped: {ex.Message}");
                }
            }

            var lightsPath = Path.Combine(Paths.BepInExRootPath, "light-switches.tsv");
            File.WriteAllText(lightsPath, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugPuzzleNeedsDump)}] {count} light switch(es) written to {lightsPath}");
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

        private static void Add<T>(
            List<string[]> rows,
            List<(string name, Vector3 position)> puzzles,
            string kind,
            Func<T, Transform> transformOf,
            Func<T, string> details,
            Func<T, bool> keep = null) where T : UnityEngine.Object
        {
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null)
                return;

            foreach (var item in found)
            {
                try
                {
                    if (item == null || (keep != null && !keep(item)))
                        continue;

                    var transform = transformOf(item);
                    var position = transform.position;
                    var (nearest, distance) = Nearest(puzzles, position);
                    rows.Add(new[]
                    {
                        kind,
                        PathOf(transform),
                        position.x.ToString("F1", CultureInfo.InvariantCulture),
                        position.y.ToString("F1", CultureInfo.InvariantCulture),
                        position.z.ToString("F1", CultureInfo.InvariantCulture),
                        nearest,
                        distance.ToString("F1", CultureInfo.InvariantCulture),
                        details(item),
                    });
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPuzzleNeedsDump)}] {kind} skipped: {ex.Message}");
                }
            }
        }

        private static List<(string name, Vector3 position)> FindPuzzles()
        {
            var result = new List<(string, Vector3)>();
            var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gourds == null)
                return result;

            foreach (var gourd in gourds)
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                    continue;

                result.Add((prop.saveablePropName.ToString(), prop.transform.position));
            }

            return result;
        }

        private static (string name, float distance) Nearest(List<(string name, Vector3 position)> puzzles, Vector3 at)
        {
            string name = "<none>";
            var best = float.MaxValue;
            foreach (var puzzle in puzzles)
            {
                var distance = Vector3.Distance(at, puzzle.position);
                if (distance < best)
                {
                    best = distance;
                    name = puzzle.name;
                }
            }

            return (name, best);
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static IEnumerable<string> Groups(Prop prop)
        {
            var list = prop.propGroups;
            if (list == null)
                yield break;
            for (var i = 0; i < list.Count; i++)
                yield return list[i].ToString();
        }

        private static string Describe(Func<string> read)
        {
            try
            {
                return read() ?? "<null>";
            }
            catch (Exception)
            {
                return "<error>";
            }
        }
    }
}
