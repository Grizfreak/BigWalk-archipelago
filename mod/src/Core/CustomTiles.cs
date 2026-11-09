using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
    // The host's own PNGs (LocalFolder) override a drawing or a tile kind, and go to its guests.
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
        private static PegTileRenderer _probe;
        private static bool _newWorld;
        private static readonly Dictionary<string, byte[]> Drawings = new(StringComparer.OrdinalIgnoreCase);

        // The player's folder: drawings by name, and "@<tile kind>" for a kind's own file.
        private const string KindPrefix = "@";
        private const long MaxFileBytes = 16 * 1024 * 1024;
        private static readonly Dictionary<string, string> LocalFiles = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LocalKinds = new();
        private static string _localSignature = string.Empty;
        private static bool _folderFailed;

        // Only the icons and the scribbles: the poses and the points are what their puzzles read.
        private static readonly Regex ReplaceableKind = new("^PegTile(A[1-9]|C[1-9]|Priest[A-D][1-9])$");

        // The host's drawings for guests: 512 x 512 ink, by "@<kind>" or drawing name.
        private static readonly BlobChannel Channel = new(1, Tag, 1024 * 1024, Cell * Cell);
        private static Dictionary<string, Blob> _hostBlobs = new();
        private static string _hostBlobsSignature;
        private static Dictionary<string, ulong> _hostDrawings;
        private static readonly Dictionary<string, (ulong Hash, byte[] Ink)> Received = new(StringComparer.OrdinalIgnoreCase);
        private static bool _guestFilesLogged;

        internal static string LocalFolder => Path.Combine(BepInEx.Paths.ConfigPath, "BigWalkArchipelago", "tiles");

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

        internal static void ForgetMirror()
        {
            _mirrored = null;
            _hostDrawings = null;
            Received.Clear();
            Drawings.Clear();
            Channel.Forget();
        }

        // What the host's snapshot carries: each of its own drawings and the hash of its ink.
        internal static Dictionary<string, ulong> HostManifest()
        {
            var manifest = new Dictionary<string, ulong>();
            foreach (var pair in HostBlobs())
                manifest[pair.Key] = pair.Value.Hash;
            return manifest;
        }

        internal static void PumpHost(IEnumerable<NetworkConnection> guests) => Channel.Pump(guests, HostBlobs());

        private static Dictionary<string, Blob> HostBlobs()
        {
            if (IsGuest || !ApRuntime.ShufflesPegTiles)
                return new Dictionary<string, Blob>();
            if (_hostBlobsSignature == _localSignature)
                return _hostBlobs;

            _hostBlobsSignature = _localSignature;
            _hostBlobs = new Dictionary<string, Blob>();
            foreach (var name in LocalFiles.Keys)
            {
                var pixels = Drawing(name);
                if (pixels == null)
                    continue;
                var ink = new byte[Cell * Cell];
                for (var i = 0; i < ink.Length; i++)
                    ink[i] = pixels[i * 4 + 3];
                _hostBlobs[name] = Blob.Make(ink);
            }
            return _hostBlobs;
        }

        internal static void ApplyDrawingsFromHost(Dictionary<string, ulong> drawings)
        {
            _hostDrawings = drawings;
            foreach (var name in new List<string>(Received.Keys))
            {
                if (!drawings.TryGetValue(name, out var hash) || hash != Received[name].Hash)
                {
                    Received.Remove(name);
                    Drawings.Remove(name);
                }
            }
        }

        // Runs in ModChannel's handler: nothing may throw, so every check returns quietly.
        internal static void ReceiveChunk(string name, ulong hash, int total, int offset, byte[] data)
        {
            var ink = Channel.Receive(name, hash, total, offset, data);
            if (ink == null)
                return;
            if (ink.Length != Cell * Cell)
            {
                Plugin.Log.LogWarning($"{Tag} The host's drawing '{name}' was refused (not {Cell} x {Cell}).");
                return;
            }
            Received[name] = (hash, ink);
            Drawings.Remove(name);
            Plugin.Log.LogInfo($"{Tag} The host's drawing '{name}' received ({total / 1024} KB).");
        }

        // Every couple of seconds while a world is ready (TeleportButtonRunner): the cells follow what this machine
        // should show, and are left alone once they do.
        internal static void Tick()
        {
            // Its tile gone, this is another world (a guest rejoining): its set may be a fresh one.
            if (!ReferenceEquals(_probe, null) && _probe == null)
                ForgetSet();

            ScanLocal();
            var guest = IsGuest;
            var inWorld = guest ? _mirrored != null : Net.ApRuntime.ShufflesPegTiles;
            var seed = guest ? _mirrored ?? Array.Empty<string>() : HostTiles;
            // A guest shows the host's own kinds, never its own folder's.
            var overridden = guest ? HostKinds() : LocalKinds;
            if (guest && LocalFiles.Count > 0 && !_guestFilesLogged)
            {
                _guestFilesLogged = true;
                Plugin.Log.LogInfo($"{Tag} Your own drawings are not used in another player's world: everyone sees the host's.");
            }
            var wanted = new List<string>();
            foreach (var entry in seed)
            {
                var equals = entry.IndexOf('=');
                if (!inWorld || equals <= 0 || !overridden.Contains(entry.Substring(0, equals)))
                    wanted.Add(entry);
            }
            if (inWorld)
            {
                foreach (var kind in overridden)
                    wanted.Add($"{kind}={KindPrefix}{kind}");
            }
            wanted.Sort(StringComparer.Ordinal);
            var key = string.Join(",", wanted) + "|" + (guest ? ReceivedSignature() : _localSignature);
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

        private static void ForgetSet()
        {
            _probe = null;
            _applied = null;
            _newWorld = true;
            var sets = Resources.FindObjectsOfTypeAll<PegTileDataSet>();
            if (_set != null && sets.Length > 0 && sets[0].Pointer == _set.Pointer)
                return;

            Plugin.Log.LogInfo($"{Tag} A new tile data set came with this world; the drawings go on it again.");
            _set = null;
            _original = null;
            _textureIndex = -1;
            if (_material != null)
                UnityEngine.Object.Destroy(_material);
            _material = null;
        }

        private static List<string> HostKinds()
        {
            var kinds = new List<string>();
            if (_hostDrawings == null)
                return kinds;
            foreach (var name in _hostDrawings.Keys)
            {
                if (name.StartsWith(KindPrefix, StringComparison.Ordinal) && ReplaceableKind.IsMatch(name.Substring(1)))
                    kinds.Add(name.Substring(1));
            }
            return kinds;
        }

        private static string ReceivedSignature()
        {
            var parts = new List<string>();
            foreach (var pair in Received)
                parts.Add($"{pair.Key}:{pair.Value.Hash}");
            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
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

            var renderers = new List<PegTileRenderer>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer != null && renderer.pegTileDataSet != null && renderer.pegTileDataSet.Pointer == _set.Pointer)
                    renderers.Add(renderer);
            }

            var datas = _set.indexDatas;

            // Tiles born on our cells keep their mesh's material: the game's cells make them swap.
            if (_newWorld)
            {
                _newWorld = false;
                for (var i = 0; i < datas.Length && i < _original.Length; i++)
                    datas[i] = _original[i];
                foreach (var renderer in renderers)
                    renderer.Refresh();
            }

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

            foreach (var renderer in renderers)
                renderer.Refresh();
            var refreshed = renderers.Count;
            if (refreshed > 0)
                _probe = renderers[0];

            Plugin.Log.LogInfo(kinds.Count > 0
                ? $"{Tag} {kinds.Count} tile kind(s) shown as {shown.Count} drawing(s) ({string.Join(", ", shown)}); {refreshed} tile(s) refreshed."
                : $"{Tag} The game's tiles back; {refreshed} tile(s) refreshed.");
            return true;
        }

        // White ink on 512 x 512, from the tiles folder or the DLL; null if unreadable.
        private static byte[] Drawing(string name)
        {
            if (Drawings.TryGetValue(name, out var known))
                return known;

            if (name.StartsWith(MirrorPrefix, StringComparison.Ordinal))
                return Drawings[name] = Mirrored(name.Substring(MirrorPrefix.Length));

            byte[] pixels = null;
            try
            {
                if (IsGuest)
                {
                    // The host's once here; until then the DLL's, or the game's tile for a kind.
                    if (Received.TryGetValue(name, out var received))
                        pixels = InkToPixels(received.Ink);
                    else if (!name.StartsWith(KindPrefix, StringComparison.Ordinal))
                        pixels = FromDll(name);
                }
                else if (LocalFiles.TryGetValue(name, out var path))
                {
                    if (new FileInfo(path).Length > MaxFileBytes)
                        throw new InvalidDataException($"more than {MaxFileBytes / (1024 * 1024)} MB");
                    using var file = File.OpenRead(path);
                    var rgba = PngReader.ReadRgba(file, out var width, out var height);
                    pixels = ToDrawing(rgba, width, height, Path.GetFileName(path));
                    Plugin.Log.LogInfo($"{Tag} '{Path.GetFileName(path)}' read from the tiles folder.");
                }
                else if (!name.StartsWith(KindPrefix, StringComparison.Ordinal))
                    pixels = FromDll(name);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} The drawing '{name}' could not be read ({ex.Message}); that tile stays as it is.");
                pixels = null;
            }

            Drawings[name] = pixels;
            return pixels;
        }

        private static byte[] FromDll(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tiles." + name + ".png");
            if (stream == null)
            {
                Plugin.Log.LogWarning($"{Tag} No drawing '{name}' in this build nor in the tiles folder; that tile stays as it is.");
                return null;
            }
            var rgba = PngReader.ReadRgba(stream, out var width, out var height);
            return ToDrawing(rgba, width, height, name);
        }

        private static byte[] InkToPixels(byte[] ink)
        {
            var pixels = new byte[Cell * Cell * 4];
            for (var i = 0; i < ink.Length; i++)
            {
                pixels[i * 4] = 255;
                pixels[i * 4 + 1] = 255;
                pixels[i * 4 + 2] = 255;
                pixels[i * 4 + 3] = ink[i];
            }
            return pixels;
        }

        // Listed every tick: a file added, removed or saved again makes every drawing read anew.
        private static void ScanLocal()
        {
            if (_folderFailed)
                return;

            FileInfo[] files;
            try
            {
                var folder = new DirectoryInfo(LocalFolder);
                if (!folder.Exists)
                {
                    folder.Create();
                    Plugin.Log.LogInfo($"{Tag} Your own tiles go in {folder.FullName}.");
                }
                files = folder.GetFiles();
            }
            catch (Exception ex)
            {
                _folderFailed = true;
                Plugin.Log.LogWarning($"{Tag} The tiles folder cannot be read, only the mod's drawings are shown: {ex.Message}");
                return;
            }

            Array.Sort(files, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            var pool = PoolFiles();
            var draw = PoolDrawKey();
            var signature = new StringBuilder();
            foreach (var file in files)
                signature.Append(file.Name).Append(':').Append(file.Length).Append(':').Append(file.LastWriteTimeUtc.Ticks).Append(';');
            signature.Append("|pool:");
            foreach (var file in pool)
                signature.Append(file.Name).Append(':').Append(file.Length).Append(':').Append(file.LastWriteTimeUtc.Ticks).Append(';');
            signature.Append("|draw:").Append(draw).Append("|mode:").Append(ModConfig.TilePool.Value);
            if (signature.ToString() == _localSignature)
                return;

            _localSignature = signature.ToString();
            _guestFilesLogged = false;
            LocalFiles.Clear();
            LocalKinds.Clear();
            Drawings.Clear();
            var names = new List<string>();
            foreach (var file in files)
            {
                // The sounds are TileSounds' to read.
                if (string.Equals(file.Extension, ".wav", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(file.Extension, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.LogWarning($"{Tag} '{file.Name}' in the tiles folder is not a .png; left out.");
                    continue;
                }

                var stem = Path.GetFileNameWithoutExtension(file.Name);
                if (stem.StartsWith("PegTile", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<PropGroup>(stem, true, out var kind))
                {
                    var kindName = kind.ToString();
                    if (!ReplaceableKind.IsMatch(kindName))
                    {
                        Plugin.Log.LogWarning($"{Tag} '{file.Name}': {kindName} is a pose or point tile, which its puzzle reads; left out.");
                        continue;
                    }
                    LocalFiles[KindPrefix + kindName] = file.FullName;
                    if (!LocalKinds.Contains(kindName))
                        LocalKinds.Add(kindName);
                }
                else
                    LocalFiles[stem] = file.FullName;
                names.Add(file.Name);
            }

            Plugin.Log.LogInfo(names.Count == 0
                ? $"{Tag} No drawing of your own in {LocalFolder}."
                : $"{Tag} Your own drawings in {LocalFolder}: {string.Join(", ", names)}.");
            DrawPool(pool, draw);
        }

        // tiles/pool: any number of PNGs, drawn for the icons no file of their own names.
        private const int MaxPoolFiles = 1000;
        private const int PoolOnGameTiles = 1;
        private const int PoolOff = 2;

        private static FileInfo[] PoolFiles()
        {
            try
            {
                var folder = new DirectoryInfo(Path.Combine(LocalFolder, "pool"));
                if (!folder.Exists)
                {
                    folder.Create();
                    return Array.Empty<FileInfo>();
                }
                var files = new List<FileInfo>();
                foreach (var file in folder.GetFiles("*.png"))
                {
                    if (files.Count == MaxPoolFiles)
                        break;
                    files.Add(file);
                }
                files.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                return files.ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<FileInfo>();
            }
        }

        // Keyed on seed and slot: the same draw on every reconnection, another next seed.
        private static string PoolDrawKey()
        {
            if (IsGuest || !ApRuntime.ShufflesPegTiles || ApRuntime.Current == null)
                return string.Empty;
            return $"{ApRuntime.Current.SeedName}/{ApRuntime.Current.SlotName}";
        }

        private static void DrawPool(FileInfo[] pool, string draw)
        {
            var mode = ModConfig.TilePool.Value;
            if (pool.Length == 0 || draw.Length == 0 || mode == PoolOff)
                return;

            // Mode 1 leaves every icon the seed drew (custom_tiles) to the seed.
            var seeded = new HashSet<string>();
            if (mode == PoolOnGameTiles)
            {
                foreach (var entry in _seed)
                {
                    var equals = entry.IndexOf('=');
                    if (equals > 0)
                        seeded.Add(entry.Substring(0, equals));
                }
            }

            var free = new List<string>();
            for (var i = 1; i <= Vanilla.Length; i++)
            {
                var kind = $"PegTileA{i}";
                if (!LocalKinds.Contains(kind) && !seeded.Contains(kind))
                    free.Add(kind);
            }
            // A hash of the key, not System.Random: the same order on every machine.
            free.Sort((a, b) => Rank(draw, a).CompareTo(Rank(draw, b)));
            var picks = new List<FileInfo>(pool);
            picks.Sort((a, b) => Rank(draw, a.Name.ToLowerInvariant()).CompareTo(Rank(draw, b.Name.ToLowerInvariant())));

            var drawn = new List<string>();
            for (var i = 0; i < free.Count && i < picks.Count; i++)
            {
                LocalFiles[KindPrefix + free[i]] = picks[i].FullName;
                LocalKinds.Add(free[i]);
                drawn.Add($"{free[i]}={picks[i].Name}");
            }
            drawn.Sort(StringComparer.Ordinal);
            Plugin.Log.LogInfo($"{Tag} {pool.Length} drawing(s) in the pool, {drawn.Count} drawn for this seed"
                               + (mode == PoolOnGameTiles ? " on the game's own icons" : string.Empty) + $": {string.Join(", ", drawn)}."); 
        }

        private static ulong Rank(string draw, string name) => Blob.Fnv(Encoding.UTF8.GetBytes(draw + "|" + name));

        // Ink is the alpha; with no transparency, the distance from the border's brightness.
        private static byte[] ToDrawing(byte[] rgba, int width, int height, string name)
        {
            var count = width * height;
            var ink = new byte[count];
            var transparent = false;
            for (var i = 0; i < count && !transparent; i++)
                transparent = rgba[i * 4 + 3] < 255;

            if (transparent)
            {
                for (var i = 0; i < count; i++)
                    ink[i] = rgba[i * 4 + 3];
            }
            else
            {
                long sum = 0;
                var border = 0;
                for (var x = 0; x < width; x++)
                {
                    sum += Brightness(rgba, x) + Brightness(rgba, (height - 1) * width + x);
                    border += 2;
                }
                for (var y = 0; y < height; y++)
                {
                    sum += Brightness(rgba, y * width) + Brightness(rgba, y * width + width - 1);
                    border += 2;
                }
                var background = (int)(sum / border);
                var range = Math.Max(1, Math.Max(background, 255 - background));
                for (var i = 0; i < count; i++)
                    ink[i] = (byte)Math.Min(255, Math.Abs(Brightness(rgba, i) - background) * 255 / range);
                Plugin.Log.LogInfo($"{Tag} '{name}' has no transparency: its ink is what stands out from its "
                                   + (background < 128 ? "dark" : "light") + " background.");
            }

            var pixels = new byte[Cell * Cell * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
            }

            if (width == Cell && height == Cell)
            {
                for (var i = 0; i < count; i++)
                    pixels[i * 4 + 3] = ink[i];
                return pixels;
            }

            // Box average; a picture smaller than a tile is scaled up blocky.
            var side = Math.Max(width, height);
            var fitWidth = Math.Max(1, (int)((long)width * Cell / side));
            var fitHeight = Math.Max(1, (int)((long)height * Cell / side));
            var left = (Cell - fitWidth) / 2;
            var top = (Cell - fitHeight) / 2;
            for (var ty = 0; ty < fitHeight; ty++)
            {
                var y0 = (int)((long)ty * height / fitHeight);
                var y1 = Math.Max(y0 + 1, (int)((long)(ty + 1) * height / fitHeight));
                for (var tx = 0; tx < fitWidth; tx++)
                {
                    var x0 = (int)((long)tx * width / fitWidth);
                    var x1 = Math.Max(x0 + 1, (int)((long)(tx + 1) * width / fitWidth));
                    long total = 0;
                    for (var y = y0; y < y1; y++)
                        for (var x = x0; x < x1; x++)
                            total += ink[y * width + x];
                    pixels[((top + ty) * Cell + left + tx) * 4 + 3] = (byte)(total / ((y1 - y0) * (x1 - x0)));
                }
            }

            Plugin.Log.LogInfo($"{Tag} '{name}' is {width}x{height}: fitted into {Cell}x{Cell}"
                               + (width != height ? ", centred." : "."));
            return pixels;
        }

        private static int Brightness(byte[] rgba, int pixel)
        {
            var at = pixel * 4;
            return (rgba[at] * 299 + rgba[at + 1] * 587 + rgba[at + 2] * 114) / 1000;
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
            // Past 16 drawings, 256 cells: a 4096 atlas with its mips costs hundreds of MB.
            var cell = grid > 4 ? Cell / 2 : Cell;
            var size = cell * grid;
            var top = new byte[size * size * 4];
            for (var index = 0; index < shown.Count; index++)
            {
                var pixels = Drawing(shown[index]);
                if (cell != Cell)
                    pixels = HalfSize(pixels, Cell);
                var cellX = index % grid;
                var cellY = index / grid;
                for (var row = 0; row < cell; row++)
                {
                    var textureRow = size - 1 - (cellY * cell + row);
                    Buffer.BlockCopy(pixels, row * cell * 4, top, (textureRow * size + cellX * cell) * 4, cell * 4);
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

            // One texture for good: tiles copy the material, a new texture left them on a dead one.
            var atlas = _atlas;
            if (atlas == null)
            {
                atlas = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "pegtileatlas_archipelago" };
                var game = _set.materialPerTextureIndex[0]?.mainTexture;
                if (game != null)
                {
                    atlas.wrapMode = game.wrapMode;
                    atlas.filterMode = game.filterMode;
                    atlas.anisoLevel = game.anisoLevel;
                }
                UnityEngine.Object.DontDestroyOnLoad(atlas);
            }
            else if (atlas.width != size)
                atlas.Reinitialize(size, size, TextureFormat.RGBA32, true);
            atlas.LoadRawTextureData((Il2CppStructArray<byte>)raw);
            // Kept readable: it is filled again in place at the next change.
            atlas.Apply(false, false);

            // Unity's null as well: a material destroyed is made again, in the same slot.
            if (_material == null)
            {
                _material = new Material(_set.materialPerTextureIndex[0]) { name = "PegTile Archipelago" };
                UnityEngine.Object.DontDestroyOnLoad(_material);
            }

            if (_textureIndex >= 0)
            {
                _set.materialPerTextureIndex[_textureIndex] = _material;
            }
            else
            {
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
            _atlas = atlas;
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
