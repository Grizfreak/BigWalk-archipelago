using System;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Tile Thief and its tiles, for `tile_thief` (0.3.0): every peg tile validator (where it is,
    // the sequence of tile kinds it expects, the sets its generator draws from, its slots) and
    // every prop that draws a peg tile (its kinds, place, whether it is switched on), with the
    // player's distance. Written to BepInEx/peg-tile-dump-<n>.txt. Pure diagnostic.
    internal static class DebugPegTileDump
    {
        private static int _count;

        internal static void Dump()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            var origin = player != null ? player.transform.position : Vector3.zero;
            var text = new StringBuilder();

            foreach (var validator in UnityEngine.Object.FindObjectsByType<PegTileValidator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    text.AppendLine($"validator {Path(validator.transform)} at {validator.transform.position:F1} ({(validator.transform.position - origin).magnitude:F1} m)");
                    var generator = validator.sequenceGenerator;
                    if (generator != null)
                    {
                        text.AppendLine($"  generator {Path(generator.transform)} seed {generator.seed} length {generator.sequenceLength} set {generator.pegTileSet}");
                        if (generator.setsPerSlot != null)
                            foreach (var set in generator.setsPerSlot)
                                text.AppendLine($"    slot set {set}");
                        if (generator.sequence != null)
                            foreach (var kind in generator.sequence)
                                text.AppendLine($"    expects {kind} ({(int)kind})");
                        if (generator.pegTileDataSet != null && generator.pegTileDataSet.collections != null)
                            foreach (var collection in generator.pegTileDataSet.collections)
                            {
                                var kinds = new StringBuilder();
                                if (collection.propGroups != null)
                                    foreach (var kind in collection.propGroups)
                                        kinds.Append(kind).Append(' ');
                                text.AppendLine($"    collection {collection.pegTileSet} repeats={collection.allowRepeats}: {kinds}");
                            }
                    }

                    if (validator.propHomeBlocks != null)
                        foreach (var block in validator.propHomeBlocks)
                            if (block != null && block.homes != null)
                                foreach (var home in block.homes)
                                    if (home != null)
                                        text.AppendLine($"  slot {Path(home.transform)} at {home.transform.position:F1}");
                }
                catch (Exception ex)
                {
                    text.AppendLine("  # unreadable validator: " + ex.Message);
                }
            }

            var tiles = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    var prop = renderer.GetComponentInParent<Prop>(true);
                    var kinds = new StringBuilder();
                    if (prop != null && prop.propGroups != null)
                        foreach (var kind in prop.propGroups)
                            kinds.Append(kind).Append(' ');
                    var at = renderer.transform.position;
                    text.AppendLine(
                        $"tile {renderer.propGroup} drawn by {Path(renderer.transform)} prop '{(prop != null ? prop.name : "-")}' groups [{kinds}] "
                        + $"at {at:F1} ({(at - origin).magnitude:F0} m) active={renderer.gameObject.activeInHierarchy} hidden={renderer.hidden}");
                    tiles++;
                }
                catch (Exception ex)
                {
                    text.AppendLine("# unreadable tile: " + ex.Message);
                }
            }

            // The prefabs Mirror can make by itself, by asset id: a peg tile among them could be made
            // new from its own prefab instead of copied from one of the island's.
            try
            {
                var prefabs = Mirror.NetworkClient.prefabs;
                text.AppendLine($"mirror prefabs: {(prefabs != null ? prefabs.Count : 0)}");
                if (prefabs != null)
                    foreach (var pair in prefabs)
                        text.AppendLine($"  prefab {pair.Key:X} {(pair.Value != null ? pair.Value.name : "-")}");
            }
            catch (Exception ex)
            {
                text.AppendLine("# mirror prefabs unreadable: " + ex.Message);
            }

            var path = System.IO.Path.Combine(Paths.BepInExRootPath, $"peg-tile-dump-{++_count}.txt");
            File.WriteAllText(path, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugPegTileDump)}] {tiles} tile(s) written to {path}.");
        }

        private static string Path(Transform t)
        {
            var path = new StringBuilder(t.name);
            for (var parent = t.parent; parent != null; parent = parent.parent)
                path.Insert(0, parent.name + "/");
            return path.ToString();
        }
    }
}
