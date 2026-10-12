using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Measuring pass for U12 (ROADMAP, player 2026-10-09): the clips of the three sounds the mod
    // wants to play (a gourd dropped into a monument, a puzzle's end, a click). The dump has no clip
    // names, so this lists every PeckEffectAudio of the scene, active or not, with the state
    // system it listens to and, state by state, the asset it plays, to BepInEx/audio-probe.tsv
    // (one line per asset). Ctrl + Backslash, from anywhere: grep it for the monuments and the puzzles.
    internal static class DebugAudioProbe
    {
        private const string Tag = "[" + nameof(DebugAudioProbe) + "]";

        // Every asset seen, by name: the probe's dump and the played-sound log both feed it, and the
        // cycling keys (Ctrl + [ and Ctrl + ]) play from it, to hear a candidate and read its name.
        internal static readonly SortedDictionary<string, AudioAsset> Assets = new(StringComparer.OrdinalIgnoreCase);
        private static int _cycle = -1;

        internal static void Remember(AudioAsset asset)
        {
            if (asset != null && !string.IsNullOrEmpty(asset.name))
                Assets[asset.name] = asset;
        }

        // Plays the next (or previous) remembered asset whose name holds the filter, where the
        // local player stands, and logs which: the ear picks, the log names it.
        internal static void Cycle(int step)
        {
            var filter = ModConfig.AudioFilter.Value ?? "";
            var names = Assets.Keys.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (names.Count == 0)
            {
                Plugin.Log.LogInfo($"{Tag} No remembered asset matches \"{filter}\" ({Assets.Count} known): press the probe key (Ctrl + Backslash) first.");
                return;
            }

            _cycle = ((_cycle + step) % names.Count + names.Count) % names.Count;
            var name = names[_cycle];
            var player = DebugPlayerLookup.FindLocalPlayer();
            var at = player != null ? player.transform.position : Vector3.zero;
            DebugAudioLog.Mute = true;
            try
            {
                AudioPlayHelper.Play(Assets[name], at);
                Plugin.Log.LogInfo($"{Tag} {_cycle + 1}/{names.Count} playing {name}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {name} did not play: {ex.Message}");
            }
            finally
            {
                DebugAudioLog.Mute = false;
            }
        }

        internal static void Dump()
        {
            var text = new StringBuilder("object\tpath\tstate_system\tstate\taction\tasset\tposition\n");
            var lines = 0;
            var effects = 0;

            foreach (var audio in UnityEngine.Object.FindObjectsByType<PeckEffectAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (audio == null)
                    continue;
                effects++;
                try
                {
                    var system = audio.systemReference != null && audio.systemReference.peckSystem != null
                        ? audio.systemReference.peckSystem.gameObject.name : "<none>";
                    var path = Path(audio.transform);
                    var position = audio.transform.position;
                    var outcomes = audio.outcomesPerState;
                    for (var state = 0; outcomes != null && state < outcomes.Length; state++)
                    {
                        var action = outcomes[state].audioAction;
                        var items = action != null ? action.Actions : null;
                        if (items == null)
                            continue;
                        foreach (var item in items)
                        {
                            DebugAudioProbe.Remember(item.Asset);
                            text.AppendLine($"{audio.gameObject.name}\t{path}\t{system}\t{state}\t{item.ActionType}\t"
                                + $"{(item.Asset != null ? item.Asset.name : "<none>")}\t{position.x:0.#},{position.y:0.#},{position.z:0.#}");
                            lines++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} {audio.gameObject.name}: {ex.Message}");
                }
            }

            // Every AudioAsset the game has in memory, played or not, one name per line: the catalogue
            // to look a candidate up in (a monument's own sound, a gourd's "place").
            try
            {
                var all = Resources.FindObjectsOfTypeAll<AudioAsset>();
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var asset in all)
                {
                    if (asset == null)
                        continue;
                    names.Add($"{asset.GetIl2CppType().Name}\t{asset.name}");
                    Remember(asset);
                }
                System.IO.File.WriteAllLines(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "audio-assets.tsv"), names);
                Plugin.Log.LogInfo($"{Tag} {names.Count} audio asset(s) in memory written to audio-assets.tsv.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} The asset catalogue failed: {ex.Message}");
            }

            var file = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "audio-probe.tsv");
            System.IO.File.WriteAllText(file, text.ToString());
            Plugin.Log.LogInfo($"{Tag} {effects} audio effect(s), {lines} asset line(s) written to {file}.");
        }

        private static string Path(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
