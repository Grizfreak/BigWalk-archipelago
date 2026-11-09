using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BigWalkArchipelago.Core.Net;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // PegTileA<n>.wav replaces stem n-1 of the sound tiles' music; guests get the host's samples.
    internal static class TileSounds
    {
        private const string Tag = "[" + nameof(TileSounds) + "]";
        internal const int Rate = 16000;
        private const float DefaultSeconds = 27.83f;
        // A stem lasts 27.83 s: anything past 28 s is never played, so never kept nor accepted.
        internal const int MaxSamples = Rate * 28;
        private const long MaxFileBytes = 32L * 1024 * 1024;

        private static readonly BlobChannel Channel = new(0, Tag, 1024 * 1024, MaxSamples * 2);

        private sealed class Sound
        {
            internal short[] Pcm;
            internal Blob Blob;
            internal ulong Hash => Blob.Hash;
        }

        // The host's own files, by tile kind index (0 = A1).
        private static readonly Dictionary<int, Sound> Local = new();
        private static string _localSignature = string.Empty;
        private static bool _guestFilesLogged;

        // A guest: what the host says each kind plays (its hash), and the sounds received so far.
        private static Dictionary<int, ulong> _manifest;
        private static readonly Dictionary<int, Sound> Received = new();

        // The game's clips, by cue, to put back; and what each kind plays now.
        private static readonly Dictionary<IntPtr, AudioClip> Originals = new();
        private static readonly Dictionary<IntPtr, AudioClip> Ours = new();
        private static readonly Dictionary<int, ulong> Applied = new();

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        // What the host's snapshot carries: each replaced kind and its sound's hash.
        internal static Dictionary<int, ulong> HostManifest()
        {
            var manifest = new Dictionary<int, ulong>();
            if (!ApRuntime.ShufflesPegTiles)
                return manifest;
            foreach (var pair in Local)
                manifest[pair.Key] = pair.Value.Hash;
            return manifest;
        }

        internal static void ApplyManifestFromHost(Dictionary<int, ulong> manifest)
        {
            _manifest = manifest;
            foreach (var kind in new List<int>(Received.Keys))
            {
                if (!manifest.TryGetValue(kind, out var hash) || hash != Received[kind].Hash)
                    Received.Remove(kind);
            }
        }

        internal static void ForgetMirror()
        {
            _manifest = null;
            Received.Clear();
            Channel.Forget();
        }

        // Every couple of seconds while a world is ready (TeleportButtonRunner).
        internal static void Tick()
        {
            ScanLocal();
            var wanted = new Dictionary<int, Sound>();
            if (IsGuest)
            {
                if (_manifest != null)
                {
                    foreach (var pair in _manifest)
                    {
                        if (Received.TryGetValue(pair.Key, out var sound) && sound.Hash == pair.Value)
                            wanted[pair.Key] = sound;
                    }
                }
            }
            else if (ApRuntime.ShufflesPegTiles)
            {
                foreach (var pair in Local)
                    wanted[pair.Key] = pair.Value;
            }

            try
            {
                Apply(wanted);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not change the tile sounds, the game's stay: {ex.Message}");
            }
        }

        private static void Apply(Dictionary<int, Sound> wanted)
        {
            var same = wanted.Count == Applied.Count;
            foreach (var pair in wanted)
                same &= Applied.TryGetValue(pair.Key, out var hash) && hash == pair.Value.Hash;
            if (same)
                return;

            var changed = 0;
            var done = new HashSet<IntPtr>();
            var swaps = new Dictionary<IntPtr, AudioClip>();
            var dropped = new List<AudioClip>();
            foreach (var set in Resources.FindObjectsOfTypeAll<PegTileMusicSet>())
            {
                var group = set?.musicGroup;
                if (group?.SpeakerStems == null || set.mappings == null)
                    continue;
                foreach (var pair in set.mappings)
                {
                    var kind = KindIndex(pair.propGroup.ToString());
                    if (kind < 0 || pair.Index < 0 || pair.Index >= group.SpeakerStems.Length)
                        continue;
                    var cue = group.SpeakerStems[pair.Index];
                    if (cue == null || !done.Add(cue.Pointer))
                        continue;
                    if (!Originals.ContainsKey(cue.Pointer))
                        Originals[cue.Pointer] = cue._clip;

                    var original = Originals[cue.Pointer];
                    var target = wanted.TryGetValue(kind, out var sound) ? sound : null;
                    var current = Ours.TryGetValue(cue.Pointer, out var ours) ? ours : null;
                    if (target == null)
                    {
                        if (current == null)
                            continue;
                        cue._clip = original;
                        Ours.Remove(cue.Pointer);
                        swaps[current.Pointer] = original;
                        dropped.Add(current);
                    }
                    else
                    {
                        var seconds = original != null && original.frequency > 0 ? original.samples / (float)original.frequency : DefaultSeconds;
                        var clip = BuildClip($"PegTileA{kind + 1} (Archipelago)", target.Pcm, seconds);
                        cue._clip = clip;
                        Ours[cue.Pointer] = clip;
                        if (original != null)
                            swaps[original.Pointer] = clip;
                        if (current != null)
                        {
                            swaps[current.Pointer] = clip;
                            dropped.Add(current);
                        }
                    }
                    changed++;
                }
            }

            var players = Repoint(swaps);
            foreach (var clip in dropped)
                UnityEngine.Object.Destroy(clip);

            Applied.Clear();
            foreach (var pair in wanted)
                Applied[pair.Key] = pair.Value.Hash;
            Plugin.Log.LogInfo(wanted.Count == 0
                ? $"{Tag} The game's tile sounds back ({changed} stem(s), {players} speaker(s) switched)."
                : $"{Tag} Tile sounds replaced: {string.Join(", ", Names(wanted.Keys))} ({changed} stem(s), {players} speaker(s) switched).");
        }

        // Speakers loop their stem and are only muted: a live one gets the new clip at its play head.
        private static int Repoint(Dictionary<IntPtr, AudioClip> swaps)
        {
            if (swaps.Count == 0)
                return 0;
            var switched = 0;
            foreach (var player in UnityEngine.Object.FindObjectsByType<MusicPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    var controller = player?.ASC;
                    var held = controller?._clip;
                    if (held == null || !swaps.TryGetValue(held.Pointer, out var replacement) || replacement == null)
                        continue;
                    var source = controller._audioSource;
                    var playing = source != null && source.isPlaying;
                    var time = source != null ? source.time : 0f;
                    controller._clip = replacement;
                    if (source != null)
                    {
                        source.clip = replacement;
                        source.time = Mathf.Clamp(time, 0f, Math.Max(0f, replacement.length - 0.05f));
                        if (playing)
                            source.Play();
                    }
                    switched++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} A speaker kept its sound: {ex.Message}");
                }
            }
            return switched;
        }

        // The sound repeated to the stem's length, then silence: a stem loops on its length.
        private static AudioClip BuildClip(string name, short[] pcm, float seconds)
        {
            var length = Math.Max(1, (int)Math.Round(seconds * Rate));
            var samples = new float[length];
            var copies = pcm.Length == 0 ? 0 : Math.Max(1, length / pcm.Length);
            for (var copy = 0; copy < copies; copy++)
            {
                var start = copy * pcm.Length;
                for (var i = 0; i < pcm.Length && start + i < length; i++)
                    samples[start + i] = pcm[i] / 32768f;
            }

            var clip = AudioClip.Create(name, length, 1, Rate, false);
            clip.SetData((Il2CppStructArray<float>)samples, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        // ------------------------------------------------------------------
        // The tiles folder (the host's, or a player alone)
        // ------------------------------------------------------------------

        private static void ScanLocal()
        {
            FileInfo[] files;
            try
            {
                var folder = new DirectoryInfo(CustomTiles.LocalFolder);
                if (!folder.Exists)
                    return;
                files = folder.GetFiles("*.wav");
            }
            catch (Exception)
            {
                return;
            }

            Array.Sort(files, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            var signature = new StringBuilder();
            foreach (var file in files)
                signature.Append(file.Name).Append(':').Append(file.Length).Append(':').Append(file.LastWriteTimeUtc.Ticks).Append(';');
            if (signature.ToString() == _localSignature)
                return;
            _localSignature = signature.ToString();
            _guestFilesLogged = false;

            Local.Clear();
            foreach (var file in files)
            {
                var kind = KindIndex(Path.GetFileNameWithoutExtension(file.Name));
                if (kind < 0)
                {
                    Plugin.Log.LogWarning($"{Tag} '{file.Name}': only PegTileA1.wav to PegTileA9.wav have a sound; left out.");
                    continue;
                }
                try
                {
                    if (file.Length > MaxFileBytes)
                        throw new InvalidDataException($"more than {MaxFileBytes / (1024 * 1024)} MB");
                    using var stream = file.OpenRead();
                    var samples = WavReader.ReadMono(stream, out var frequency);
                    var pcm = Prepare(samples, frequency);
                    Local[kind] = new Sound { Pcm = pcm, Blob = Blob.Make(PcmBytes(pcm)) };
                    Plugin.Log.LogInfo($"{Tag} '{file.Name}' read: {samples.Length / (float)frequency:0.00} s at {frequency} Hz"
                                       + (samples.Length / (float)frequency > MaxSamples / (float)Rate ? ", cut at 28 s." : "."));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} '{file.Name}' could not be read ({ex.Message}); that tile keeps its sound.");
                }
            }

            if (IsGuest && Local.Count > 0 && !_guestFilesLogged)
            {
                _guestFilesLogged = true;
                Plugin.Log.LogInfo($"{Tag} Your own tile sounds are not used in another player's world: everyone hears the host's.");
            }
        }

        // To 16 kHz mono 16-bit, cut at 28 s, with 5 ms fades so the repeats do not click.
        private static short[] Prepare(float[] samples, int frequency)
        {
            var length = (int)Math.Min(MaxSamples, (long)samples.Length * Rate / frequency);
            var pcm = new short[length];
            var fade = Rate / 200;
            for (var i = 0; i < length; i++)
            {
                var at = (double)i * frequency / Rate;
                var index = (int)at;
                var next = Math.Min(index + 1, samples.Length - 1);
                var value = samples[index] + (samples[next] - samples[index]) * (float)(at - index);
                var gain = Math.Min(1f, Math.Min(i, length - 1 - i) / (float)fade);
                pcm[i] = (short)Math.Clamp(value * gain * 32767f, short.MinValue, short.MaxValue);
            }
            return pcm;
        }

        private static byte[] PcmBytes(short[] pcm)
        {
            var bytes = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        // ------------------------------------------------------------------
        // Host -> guests
        // ------------------------------------------------------------------

        // Every host frame (ModChannel): the host's sounds to its guests, a few chunks at a time.
        internal static void PumpHost(IEnumerable<NetworkConnection> guests)
        {
            var blobs = new Dictionary<string, Blob>();
            if (ApRuntime.ShufflesPegTiles)
            {
                foreach (var pair in Local)
                    blobs[$"PegTileA{pair.Key + 1}"] = pair.Value.Blob;
            }
            Channel.Pump(guests, blobs);
        }

        // Runs in ModChannel's handler: nothing may throw, so every check returns quietly.
        internal static void ReceiveChunk(string key, ulong hash, int total, int offset, byte[] data)
        {
            var kind = KindIndex(key);
            if (kind < 0)
                return;
            var raw = Channel.Receive(key, hash, total, offset, data);
            if (raw == null)
                return;
            if (raw.Length % 2 != 0)
            {
                Plugin.Log.LogWarning($"{Tag} The host's sound for {key} was refused (odd length).");
                return;
            }
            var pcm = new short[raw.Length / 2];
            Buffer.BlockCopy(raw, 0, pcm, 0, raw.Length);
            Received[kind] = new Sound { Pcm = pcm, Blob = new Blob { Hash = hash } };
            Plugin.Log.LogInfo($"{Tag} The host's sound for {key} received ({pcm.Length / (float)Rate:0.00} s, {total / 1024} KB).");
        }

        private static int KindIndex(string name)
        {
            if (name.Length == 9 && name.StartsWith("PegTileA", StringComparison.OrdinalIgnoreCase)
                && name[8] >= '1' && name[8] <= '9')
                return name[8] - '1';
            return -1;
        }

        private static IEnumerable<string> Names(IEnumerable<int> kinds)
        {
            foreach (var kind in kinds)
                yield return $"PegTileA{kind + 1}";
        }
    }
}
