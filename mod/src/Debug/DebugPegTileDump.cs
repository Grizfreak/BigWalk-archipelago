using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static float _nextRequestCheck;

        // A dump without a key, for a launch driven from outside the game: a file
        // BepInEx/peg-tile-dump.request, taken once the world is ready, and deleted.
        internal static void DumpOnRequest()
        {
            if (Time.unscaledTime < _nextRequestCheck)
                return;
            _nextRequestCheck = Time.unscaledTime + 2f;
            var request = System.IO.Path.Combine(Paths.BepInExRootPath, "peg-tile-dump.request");
            if (!WorldManager.isReadyForEffects || !File.Exists(request))
                return;
            File.Delete(request);
            Dump();
        }

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

            DumpDataSets(text);

            var path = System.IO.Path.Combine(Paths.BepInExRootPath, $"peg-tile-dump-{++_count}.txt");
            File.WriteAllText(path, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugPegTileDump)}] {tiles} tile(s) written to {path}.");
        }

        // R8 (custom tiles): every PegTileDataSet in use (its atlas cells per tile kind, its materials), its
        // textures written out as PNG in BepInEx/tile-atlas/, the split-flap glyphs, and every renderer that draws
        // one of those atlases without a PegTileRenderer (a symbol the data set does not drive).
        private static void DumpDataSets(StringBuilder text)
        {
            var sets = new List<PegTileDataSet>();
            void Keep(PegTileDataSet set)
            {
                if (set != null && !sets.Exists(known => known.Pointer == set.Pointer))
                    sets.Add(set);
            }
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Keep(renderer.pegTileDataSet);
            foreach (var generator in UnityEngine.Object.FindObjectsByType<PegTileSequenceGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Keep(generator.pegTileDataSet);
            var flaps = UnityEngine.Object.FindObjectsByType<SplitFlapGlyph>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var flap in flaps)
                Keep(flap.pegTileDataSet);
            foreach (var asset in Resources.FindObjectsOfTypeAll<PegTileDataSet>())
                Keep(asset);

            var folder = System.IO.Path.Combine(Paths.BepInExRootPath, "tile-atlas");
            Directory.CreateDirectory(folder);
            var atlases = new Dictionary<IntPtr, string>();
            for (var s = 0; s < sets.Count; s++)
            {
                var set = sets[s];
                try
                {
                    text.AppendLine($"dataset {s} '{set.name}'");
                    if (set.sizePerTextureIndex != null)
                        for (var i = 0; i < set.sizePerTextureIndex.Length; i++)
                            text.AppendLine($"  texture {i} grid {set.sizePerTextureIndex[i].x}x{set.sizePerTextureIndex[i].y}");
                    if (set.materialPerTextureIndex != null)
                        for (var i = 0; i < set.materialPerTextureIndex.Length; i++)
                        {
                            var material = set.materialPerTextureIndex[i];
                            if (material == null)
                            {
                                text.AppendLine($"  material {i} -");
                                continue;
                            }
                            text.AppendLine($"  material {i} '{material.name}' shader '{(material.shader != null ? material.shader.name : "-")}'");
                            foreach (var property in material.GetTexturePropertyNames())
                            {
                                var texture = material.GetTexture(property);
                                if (texture == null)
                                    continue;
                                var file = $"set{s}-tex{i}-{property.TrimStart('_')}.png";
                                text.AppendLine($"    {property} '{texture.name}' {texture.width}x{texture.height} scale {material.GetTextureScale(property)} offset {material.GetTextureOffset(property)} -> {file}");
                                if (!atlases.ContainsKey(texture.Pointer))
                                {
                                    atlases[texture.Pointer] = file;
                                    Export(texture, System.IO.Path.Combine(folder, file), text);
                                }
                            }
                        }
                    if (set.indexDatas != null)
                        foreach (var cell in set.indexDatas)
                            text.AppendLine($"  cell {cell.propGroup} ({(int)cell.propGroup}) texture {cell.textureIndex} x {cell.xIndex} y {cell.yIndex}");
                }
                catch (Exception ex)
                {
                    text.AppendLine("  # unreadable dataset: " + ex.Message);
                }
            }

            text.AppendLine($"split-flap glyphs: {flaps.Length}");
            foreach (var flap in flaps)
            {
                if (flap != null)
                    text.AppendLine($"  flap {Path(flap.transform)} type {flap.splitFlapType} set {flap.tileSet} at {flap.transform.position:F1}");
            }

            // The atlases drawn by renderers no PegTileRenderer drives: symbols the data set would not change.
            var driven = new HashSet<IntPtr>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer.targetRenderer != null)
                    driven.Add(renderer.targetRenderer.Pointer);
            }
            var undriven = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null || driven.Contains(renderer.Pointer))
                    continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        if (texture == null || !atlases.TryGetValue(texture.Pointer, out var file))
                            continue;
                        text.AppendLine($"undriven {Path(renderer.transform)} material '{material.name}' {property} {file} scale {material.GetTextureScale(property)} offset {material.GetTextureOffset(property)} at {renderer.transform.position:F1}");
                        undriven++;
                    }
                }
            }
            text.AppendLine($"undriven renderers: {undriven}");

            // R8: what a few basic tiles really draw with (the mod's atlas showed blank on 2026-10-15), and the mod's
            // atlas read back from the GPU.
            var seen = new Dictionary<PropGroup, int>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.targetRenderer == null || !renderer.propGroup.ToString().StartsWith("PegTileA"))
                    continue;
                seen.TryGetValue(renderer.propGroup, out var count);
                if (count >= 2)
                    continue;
                seen[renderer.propGroup] = count + 1;
                try
                {
                    var materials = renderer.targetRenderer.sharedMaterials;
                    var material = renderer.targetSubmesh < materials.Length ? materials[renderer.targetSubmesh] : null;
                    text.AppendLine($"sample {renderer.propGroup} {Path(renderer.transform)} submesh {renderer.targetSubmesh}/{materials.Length} "
                                    + $"material '{(material != null ? material.name : "-")}' instance '{(renderer._materialInstance != null ? renderer._materialInstance.name : "-")}' index {renderer._materialIndex}");
                    if (material == null)
                        continue;
                    var block = new MaterialPropertyBlock();
                    renderer.targetRenderer.GetPropertyBlock(block, renderer.targetSubmesh);
                    var shader = material.shader;
                    for (var i = 0; i < shader.GetPropertyCount(); i++)
                    {
                        var name = shader.GetPropertyName(i);
                        var type = shader.GetPropertyType(i);
                        var value = type switch
                        {
                            UnityEngine.Rendering.ShaderPropertyType.Texture =>
                                $"{(material.GetTexture(name) != null ? material.GetTexture(name).name : "-")} st {material.GetTextureScale(name)} {material.GetTextureOffset(name)}"
                                + $" block {(block.GetTexture(name) != null ? block.GetTexture(name).name : "-")}",
                            UnityEngine.Rendering.ShaderPropertyType.Vector => $"{material.GetVector(name)} block {block.GetVector(name)}",
                            UnityEngine.Rendering.ShaderPropertyType.Color => $"{material.GetColor(name)} block {block.GetColor(name)}",
                            _ => $"{material.GetFloat(name)} block {block.GetFloat(name)}",
                        };
                        text.AppendLine($"    {name} ({type}) {value}");
                    }
                    if (block.HasVector("_MainTex_ST"))
                        text.AppendLine($"    block _MainTex_ST {block.GetVector("_MainTex_ST")}");
                }
                catch (Exception ex)
                {
                    text.AppendLine("  # unreadable sample: " + ex.Message);
                }
            }

            var atlas = Core.CustomTiles.Atlas;
            if (atlas != null)
            {
                text.AppendLine($"mod atlas '{atlas.name}' {atlas.width}x{atlas.height} mips {atlas.mipmapCount} format {atlas.format} -> mod-atlas.png");
                Export(atlas, System.IO.Path.Combine(folder, "mod-atlas.png"), text);
            }
        }

        private static void Export(Texture texture, string file, StringBuilder text)
        {
            try
            {
                var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(texture, target);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                // Not ImageConversion.EncodeToPNG: through Il2CppInterop it crashed the game (access violation,
                // 2026-10-15). The pixels are read back and written as a PNG here.
                File.WriteAllBytes(file, Png(copy.width, copy.height, copy.GetPixels32()));
                UnityEngine.Object.Destroy(copy);
            }
            catch (Exception ex)
            {
                text.AppendLine($"    # not written: {ex.Message}");
            }
        }

        // A plain RGBA PNG; Unity's first row is the bottom one, a PNG's the top one.
        private static byte[] Png(int width, int height, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32> pixels)
        {
            var raw = new byte[height * (width * 4 + 1)];
            var at = 0;
            for (var y = height - 1; y >= 0; y--)
            {
                raw[at++] = 0;
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    raw[at++] = pixel.r;
                    raw[at++] = pixel.g;
                    raw[at++] = pixel.b;
                    raw[at++] = pixel.a;
                }
            }
            using var compressed = new MemoryStream();
            using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
                zlib.Write(raw, 0, raw.Length);

            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            WriteBigEndian(header, 0, width);
            WriteBigEndian(header, 4, height);
            header[8] = 8;
            header[9] = 6;
            Chunk(png, "IHDR", header);
            Chunk(png, "IDAT", compressed.ToArray());
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static void Chunk(Stream png, string type, byte[] body)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, body.Length);
            png.Write(length);
            var typed = new byte[4 + body.Length];
            Encoding.ASCII.GetBytes(type, 0, 4, typed, 0);
            Buffer.BlockCopy(body, 0, typed, 4, body.Length);
            png.Write(typed);
            var crc = new byte[4];
            WriteBigEndian(crc, 0, (int)Crc32(typed));
            png.Write(crc);
        }

        private static void WriteBigEndian(byte[] into, int at, int value)
        {
            into[at] = (byte)(value >> 24);
            into[at + 1] = (byte)(value >> 16);
            into[at + 2] = (byte)(value >> 8);
            into[at + 3] = (byte)value;
        }

        private static uint Crc32(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
            return ~crc;
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
