using System;
using System.Collections.Generic;
using System.Reflection;
using BigWalkArchipelago.Core.Net;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // R8, the custom tiles (1.0): tiles shown as the drawings the seed drew, from the community's drawings carried in the
    // DLL (mod/assets/tiles/<category>, CC BY 4.0): the nine basic tiles (PegTileA1..A9, `custom_tiles`, slot data
    // `peg_tiles`) and a few of the drawing panels' scribbles (C, Priest; `custom_drawings`, slot data `peg_drawings`).
    //
    // Every peg tile, split-flap and display draws its symbol through the one PegTileDataSet: a tile kind is a cell
    // of an atlas (texture index, x, y). The drawings go into an atlas of the mod's own, a fourth material of the data
    // set, and their kinds are pointed at its cells; then every renderer refreshes. What a puzzle asks for (the
    // PropGroup) never changes, only what the tile shows. Dumped 2026-10-15 (Debug/DebugPegTileDump): one data set,
    // three 6x6 atlases, no renderer drawing an atlas outside it.
    //
    // The host has the drawings from the slot; a guest from the host, in the snapshot (ModChannel), as
    // "<tile kind>=<drawing>". Outside an Archipelago world the cells are the game's again.
    internal static class CustomTiles
    {
        private const string Tag = "[" + nameof(CustomTiles) + "]";

        // The game's basic tiles, PegTileA1 to PegTileA9, by the apworld's names (tiles.VANILLA_TILES).
        private static readonly string[] Vanilla =
            { "triangle", "asterisk", "diagonal_line", "G", "dot_and_line", "vertical_ellipsis", "S", "spiral", "e" };

        // The mod's atlas: cells of 512 (the drawings are 512 x 512), as many as the drawings shown, on a square
        // power-of-two grid (1, 2, 4 or 8 a side), filled from the top left as the game's atlases are.
        private const int Cell = 512;

        private static string[] _seed = Array.Empty<string>();
        private static string[] _mirrored;
        private static string _applied = string.Empty;

        private static PegTileDataSet _set;
        private static PegTileDataSet.TileIndexData[] _original;
        private static int _textureIndex = -1;
        private static Material _material;
        private static Texture2D _atlas;
        private static readonly Dictionary<string, byte[]> Drawings = new();

        // The mod's atlas, for the tile dump (Debug/DebugPegTileDump).
        internal static Texture2D Atlas => _atlas;

        internal static void Configure(ApSlotData slot)
        {
            var wanted = new List<string>();
            if (slot.PegTiles.Length == Vanilla.Length)
            {
                for (var place = 0; place < Vanilla.Length; place++)
                {
                    if (slot.PegTiles[place] != Vanilla[place])
                        wanted.Add($"PegTileA{place + 1}={slot.PegTiles[place]}");
                }
            }
            wanted.AddRange(slot.PegDrawings);
            _seed = wanted.ToArray();
            Plugin.Log.LogInfo(_seed.Length == 0
                ? $"{Tag} The game's tiles."
                : $"{Tag} Tiles shown as: {string.Join(", ", _seed)}.");
        }

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        internal static string[] HostTiles => ApRuntime.ShufflesPegTiles ? _seed : Array.Empty<string>();

        internal static void ApplyFromHost(List<string> tiles) => _mirrored = tiles.ToArray();

        internal static void ForgetMirror() => _mirrored = null;

        // Every couple of seconds while a world is ready (TeleportButtonRunner): the cells follow what this machine
        // should show, and are left alone once they do.
        internal static void Tick()
        {
            var wanted = new List<string>(IsGuest ? _mirrored ?? Array.Empty<string>() : HostTiles);
            wanted.Sort(StringComparer.Ordinal);
            var key = string.Join(",", wanted);
            if (key == _applied)
                return;

            try
            {
                if (Apply(wanted))
                    _applied = key;
            }
            catch (Exception ex)
            {
                // Once: the game's tiles stay, and the same draw is not tried again.
                _applied = key;
                Plugin.Log.LogWarning($"{Tag} Could not show the custom tiles, the game's stay: {ex}");
            }
        }

        private static bool Apply(List<string> wanted)
        {
            if (_set == null)
            {
                var sets = Resources.FindObjectsOfTypeAll<PegTileDataSet>();
                if (sets.Length == 0)
                    return false;
                _set = sets[0];
                if (sets.Length > 1)
                    Plugin.Log.LogWarning($"{Tag} {sets.Length} tile data sets; only '{_set.name}' is changed.");
                var cells = _set.indexDatas;
                _original = new PegTileDataSet.TileIndexData[cells.Length];
                for (var i = 0; i < cells.Length; i++)
                    _original[i] = cells[i];
            }

            // Each tile kind wanted, to its drawing's cell in the mod's atlas; a kind or a drawing unknown is left out.
            var kinds = new Dictionary<PropGroup, int>();
            var shown = new List<string>();
            foreach (var entry in wanted)
            {
                var equals = entry.IndexOf('=');
                if (equals <= 0 || !Enum.TryParse<PropGroup>(entry.Substring(0, equals), out var kind)
                    || kinds.ContainsKey(kind))
                    continue;
                var name = entry.Substring(equals + 1);
                if (Drawing(name) == null)
                    continue;
                var cell = shown.IndexOf(name);
                if (cell < 0)
                {
                    cell = shown.Count;
                    shown.Add(name);
                }
                kinds[kind] = cell;
            }

            var grid = 1;
            while (grid * grid < shown.Count)
                grid *= 2;
            if (shown.Count > 0)
                BuildAtlas(shown, grid);

            var datas = _set.indexDatas;
            for (var i = 0; i < datas.Length && i < _original.Length; i++)
            {
                var cell = _original[i];
                if (kinds.TryGetValue(cell.propGroup, out var at))
                {
                    cell.textureIndex = _textureIndex;
                    cell.xIndex = at % grid;
                    cell.yIndex = at / grid;
                }
                datas[i] = cell;
            }

            var refreshed = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.pegTileDataSet == null || renderer.pegTileDataSet.Pointer != _set.Pointer)
                    continue;
                renderer.Refresh();
                refreshed++;
            }

            Plugin.Log.LogInfo(kinds.Count > 0
                ? $"{Tag} {kinds.Count} tile kind(s) shown as {shown.Count} drawing(s) ({string.Join(", ", shown)}); {refreshed} tile(s) refreshed."
                : $"{Tag} The game's tiles back; {refreshed} tile(s) refreshed.");
            return true;
        }

        // The drawing's pixels (RGBA, top row first), read once from the DLL; null if the mod does not carry it.
        private static byte[] Drawing(string name)
        {
            if (Drawings.TryGetValue(name, out var known))
                return known;

            if (name.StartsWith(MirrorPrefix, StringComparison.Ordinal))
                return Drawings[name] = Mirrored(name.Substring(MirrorPrefix.Length));

            byte[] pixels = null;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tiles." + name + ".png"))
            {
                if (stream == null)
                    Plugin.Log.LogWarning($"{Tag} No drawing '{name}' in this build; that tile stays as it is.");
                else
                {
                    pixels = PngReader.ReadRgba(stream, out var width, out var height);
                    if (width != Cell || height != Cell)
                    {
                        Plugin.Log.LogWarning($"{Tag} The drawing '{name}' is {width}x{height}, not {Cell}x{Cell}; left out.");
                        pixels = null;
                    }
                }
            }

            Drawings[name] = pixels;
            return pixels;
        }

        // `mirrored_poses` (the player, 2026-10-20): "mirror_PegTileD4" is pose D4 the other way round. No picture of the
        // game travels in the DLL: the pose's own cell is read from the game's atlas (Blit and ReadPixels, as the tile
        // dump does; not EncodeToPNG, which crashed), flipped left to right and brought to the drawings' 512 x 512. The
        // ink is the pixel's alpha times its brightness, whichever of the two the atlas carries it in.
        private const string MirrorPrefix = "mirror_";

        private static byte[] Mirrored(string kindName)
        {
            if (_set == null || _original == null || !Enum.TryParse<PropGroup>(kindName, out var kind))
                return null;

            var found = false;
            var cell = default(PegTileDataSet.TileIndexData);
            foreach (var data in _original)
            {
                if (data.propGroup == kind)
                {
                    cell = data;
                    found = true;
                    break;
                }
            }

            var materials = _set.materialPerTextureIndex;
            var texture = found && cell.textureIndex >= 0 && cell.textureIndex < materials.Length && materials[cell.textureIndex] != null
                ? materials[cell.textureIndex].mainTexture
                : null;
            if (texture == null)
            {
                Plugin.Log.LogWarning($"{Tag} No cell of {kindName} to mirror; that tile stays as it is.");
                return null;
            }

            var grid = _set.sizePerTextureIndex[cell.textureIndex];
            var cellWidth = texture.width / (float)Math.Max(1, grid.x);
            var cellHeight = texture.height / (float)Math.Max(1, grid.y);
            var left = Mathf.FloorToInt(cell.xIndex * cellWidth);
            var width = Mathf.FloorToInt((cell.xIndex + 1) * cellWidth) - left;
            // Row 0 of the grid is the texture's top; ReadPixels counts from the bottom.
            var top = Mathf.FloorToInt(cell.yIndex * cellHeight);
            var height = Mathf.FloorToInt((cell.yIndex + 1) * cellHeight) - top;
            var bottom = texture.height - top - height;

            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(left, bottom, width, height), 0, 0);
                copy.Apply();
                var source = copy.GetPixels32();

                var pixels = new byte[Cell * Cell * 4];
                for (var row = 0; row < Cell; row++)
                {
                    // Top row first here; the copy's first row is its bottom one.
                    var y = height - 1 - Math.Min(height - 1, (int)((row + 0.5f) * height / Cell));
                    for (var column = 0; column < Cell; column++)
                    {
                        // Left to right flipped: the mirror.
                        var x = width - 1 - Math.Min(width - 1, (int)((column + 0.5f) * width / Cell));
                        var pixel = source[y * width + x];
                        var bright = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                        var at = (row * Cell + column) * 4;
                        pixels[at] = 255;
                        pixels[at + 1] = 255;
                        pixels[at + 2] = 255;
                        pixels[at + 3] = (byte)(pixel.a * bright / 255);
                    }
                }

                Plugin.Log.LogInfo($"{Tag} {kindName} mirrored from its cell ({left}, {top}, {width} x {height}) of '{texture.name}'.");
                return pixels;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not mirror {kindName}, it stays as it is: {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null)
                    UnityEngine.Object.Destroy(copy);
            }
        }

        private static void BuildAtlas(List<string> shown, int grid)
        {
            // Unity's rows run bottom to top: the cell of row 0 is the texture's top.
            var size = Cell * grid;
            var top = new byte[size * size * 4];
            for (var index = 0; index < shown.Count; index++)
            {
                var pixels = Drawing(shown[index]);
                var cellX = index % grid;
                var cellY = index / grid;
                for (var row = 0; row < Cell; row++)
                {
                    var textureRow = size - 1 - (cellY * Cell + row);
                    Buffer.BlockCopy(pixels, row * Cell * 4, top, (textureRow * size + cellX * Cell) * 4, Cell * 4);
                }
            }

            // As the game's atlas has it: white ink, black where there is none (its shader reads the colour, not only
            // the alpha: an atlas white everywhere drew every cell as all ink, 2026-10-15). The drawings are white
            // ink with an alpha, so the colour is the alpha; averaged that way, the mips keep no rim either.
            for (var i = 0; i < top.Length; i += 4)
            {
                var ink = top[i + 3];
                top[i] = ink;
                top[i + 1] = ink;
                top[i + 2] = ink;
            }

            var chain = new List<byte[]> { top };
            var side = size;
            while (side > 1)
            {
                chain.Add(HalfSize(chain[chain.Count - 1], side));
                side /= 2;
            }
            var total = 0;
            foreach (var level in chain)
                total += level.Length;
            var raw = new byte[total];
            var at = 0;
            foreach (var level in chain)
            {
                Buffer.BlockCopy(level, 0, raw, at, level.Length);
                at += level.Length;
            }

            var atlas = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "pegtileatlas_archipelago" };
            var game = _set.materialPerTextureIndex[0]?.mainTexture;
            if (game != null)
            {
                atlas.wrapMode = game.wrapMode;
                atlas.filterMode = game.filterMode;
                atlas.anisoLevel = game.anisoLevel;
            }
            atlas.LoadRawTextureData((Il2CppStructArray<byte>)raw);
            atlas.Apply(false, true);

            if (_material == null)
            {
                _material = new Material(_set.materialPerTextureIndex[0]) { name = "PegTile Archipelago" };
                UnityEngine.Object.DontDestroyOnLoad(_material);

                var materials = _set.materialPerTextureIndex;
                var moreMaterials = new Il2CppReferenceArray<Material>(materials.Length + 1);
                for (var i = 0; i < materials.Length; i++)
                    moreMaterials[i] = materials[i];
                moreMaterials[materials.Length] = _material;
                _set.materialPerTextureIndex = moreMaterials;

                var sizes = _set.sizePerTextureIndex;
                var moreSizes = new Il2CppStructArray<PegTileDataSet.Int2>(sizes.Length + 1);
                for (var i = 0; i < sizes.Length; i++)
                    moreSizes[i] = sizes[i];
                _set.sizePerTextureIndex = moreSizes;

                _textureIndex = materials.Length;
            }

            // The grid of this atlas: it follows the number of drawings shown.
            _set.sizePerTextureIndex[_textureIndex] = new PegTileDataSet.Int2 { x = grid, y = grid };

            _material.mainTexture = atlas;
            if (_atlas != null)
                UnityEngine.Object.Destroy(_atlas);
            _atlas = atlas;
            UnityEngine.Object.DontDestroyOnLoad(_atlas);
        }

        // A mip level: each pixel the mean of the four under it.
        private static byte[] HalfSize(byte[] pixels, int side)
        {
            var half = side / 2;
            var result = new byte[half * half * 4];
            for (var y = 0; y < half; y++)
            {
                for (var x = 0; x < half; x++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        var sum = pixels[((2 * y) * side + 2 * x) * 4 + c] + pixels[((2 * y) * side + 2 * x + 1) * 4 + c]
                                  + pixels[((2 * y + 1) * side + 2 * x) * 4 + c] + pixels[((2 * y + 1) * side + 2 * x + 1) * 4 + c];
                        result[(y * half + x) * 4 + c] = (byte)((sum + 2) / 4);
                    }
                }
            }
            return result;
        }
    }
}
