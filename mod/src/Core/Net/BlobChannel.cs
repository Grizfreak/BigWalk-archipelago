using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Mirror;

namespace BigWalkArchipelago.Core.Net
{
    // A named blob the host shares with its guests (a tile sound, a drawing), compressed and hashed once.
    internal sealed class Blob
    {
        internal ulong Hash;
        internal byte[] Compressed;

        internal static Blob Make(byte[] raw)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                deflate.Write(raw, 0, raw.Length);
            return new Blob { Hash = Fnv(raw), Compressed = output.ToArray() };
        }

        internal static ulong Fnv(byte[] bytes)
        {
            var hash = 14695981039346656037UL;
            foreach (var b in bytes)
                hash = (hash ^ b) * 1099511628211UL;
            return hash;
        }
    }

    // Host to guests only, in chunks over ModChannel. A guest gets data, never a file: bounded, inflated with a
    // cap, hash-checked, and anything wrong is dropped quietly (it runs inside Mirror's handler).
    internal sealed class BlobChannel
    {
        internal const int ChunkBytes = 16 * 1024;
        internal const int MaxKeyLength = 64;
        private const int ChunksPerFrame = 4;

        internal readonly byte Id;
        private readonly string _tag;
        private readonly int _maxCompressed;
        private readonly int _maxRaw;

        // Host: per guest, the hash of each key it was sent, and the chunks still to send.
        private readonly Dictionary<int, Dictionary<string, ulong>> _sent = new();
        private readonly Dictionary<int, Queue<(string Key, ulong Hash, int Offset)>> _outbox = new();

        // Guest: the transfers under way.
        private readonly Dictionary<string, Transfer> _transfers = new();

        private sealed class Transfer
        {
            internal ulong Hash;
            internal byte[] Buffer;
            internal int Filled;
        }

        internal BlobChannel(byte id, string tag, int maxCompressed, int maxRaw)
        {
            Id = id;
            _tag = tag;
            _maxCompressed = maxCompressed;
            _maxRaw = maxRaw;
        }

        // Every host frame: what changed is queued, then a few chunks per guest go out.
        internal void Pump(IEnumerable<NetworkConnection> guests, IReadOnlyDictionary<string, Blob> blobs)
        {
            var live = new HashSet<int>();
            foreach (var connection in guests)
            {
                live.Add(connection.connectionId);
                if (!_sent.TryGetValue(connection.connectionId, out var sent))
                    _sent[connection.connectionId] = sent = new Dictionary<string, ulong>();
                if (!_outbox.TryGetValue(connection.connectionId, out var outbox))
                    _outbox[connection.connectionId] = outbox = new Queue<(string, ulong, int)>();

                foreach (var pair in blobs)
                {
                    if (sent.TryGetValue(pair.Key, out var hash) && hash == pair.Value.Hash)
                        continue;
                    sent[pair.Key] = pair.Value.Hash;
                    for (var offset = 0; offset < pair.Value.Compressed.Length; offset += ChunkBytes)
                        outbox.Enqueue((pair.Key, pair.Value.Hash, offset));
                }
                foreach (var key in new List<string>(sent.Keys))
                {
                    if (!blobs.ContainsKey(key))
                        sent.Remove(key);
                }

                for (var n = 0; n < ChunksPerFrame && outbox.Count > 0; n++)
                {
                    var (key, hash, offset) = outbox.Dequeue();
                    // Changed since it was queued: its new chunks are already behind it.
                    if (!blobs.TryGetValue(key, out var blob) || blob.Hash != hash)
                        continue;
                    var count = Math.Min(ChunkBytes, blob.Compressed.Length - offset);
                    ModChannel.SendBlobChunk(connection, Id, key, hash, blob.Compressed, offset, count);
                }
            }

            foreach (var id in new List<int>(_sent.Keys))
            {
                if (!live.Contains(id))
                {
                    _sent.Remove(id);
                    _outbox.Remove(id);
                }
            }
        }

        // Guest: the whole blob once its last chunk is in and checked, else null.
        internal byte[] Receive(string key, ulong hash, int total, int offset, byte[] data)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength || total <= 0 || total > _maxCompressed
                || offset < 0 || data.Length == 0 || offset + data.Length > total)
            {
                Plugin.Log.LogWarning($"{_tag} A chunk from the host out of bounds; dropped.");
                if (!string.IsNullOrEmpty(key))
                    _transfers.Remove(key);
                return null;
            }

            if (!_transfers.TryGetValue(key, out var transfer) || transfer.Hash != hash || transfer.Buffer.Length != total)
            {
                if (offset != 0)
                    return null;
                _transfers[key] = transfer = new Transfer { Hash = hash, Buffer = new byte[total] };
            }
            if (offset != transfer.Filled)
            {
                Plugin.Log.LogWarning($"{_tag} Chunks for '{key}' out of order; dropped until the host changes it or you rejoin.");
                _transfers.Remove(key);
                return null;
            }

            Buffer.BlockCopy(data, 0, transfer.Buffer, offset, data.Length);
            transfer.Filled += data.Length;
            if (transfer.Filled < total)
                return null;

            _transfers.Remove(key);
            try
            {
                var raw = Inflate(transfer.Buffer, _maxRaw);
                if (Blob.Fnv(raw) != hash)
                    throw new InvalidDataException("its hash does not match");
                return raw;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{_tag} The host's '{key}' was refused ({ex.Message}).");
                return null;
            }
        }

        internal void Forget() => _transfers.Clear();

        // Stops at `limit` bytes: a small message cannot unpack into a huge one.
        private static byte[] Inflate(byte[] compressed, int limit)
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > limit)
                    throw new InvalidDataException("larger than allowed");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
    }
}
