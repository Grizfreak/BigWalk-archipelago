using System;
using System.IO;
using System.Text;

namespace BigWalkArchipelago.Core
{
    // Tile sounds (TileSounds): RIFF WAVE, PCM 8 to 32 bits or 32-bit float, any channel count.
    internal static class WavReader
    {
        internal const long MaxDataBytes = 64L * 1024 * 1024;

        // The sound as mono samples in [-1, 1], the channels averaged.
        internal static float[] ReadMono(Stream stream, out int frequency)
        {
            using var reader = new BinaryReader(stream);
            if (Tag(reader) != "RIFF")
                throw new InvalidDataException("not a WAV (no RIFF header)");
            reader.ReadUInt32();
            if (Tag(reader) != "WAVE")
                throw new InvalidDataException("not a WAV (no WAVE tag)");

            int format = 0, channels = 0, bits = 0;
            frequency = 0;
            while (stream.Position + 8 <= stream.Length)
            {
                var id = Tag(reader);
                var size = reader.ReadUInt32();
                if (size > stream.Length - stream.Position)
                    throw new InvalidDataException($"a broken '{id}' chunk");

                if (id == "fmt ")
                {
                    var body = reader.ReadBytes((int)size);
                    if (body.Length < 16)
                        throw new InvalidDataException("a header too short");
                    format = BitConverter.ToUInt16(body, 0);
                    channels = BitConverter.ToUInt16(body, 2);
                    frequency = BitConverter.ToInt32(body, 4);
                    bits = BitConverter.ToUInt16(body, 14);
                    // WAVE_FORMAT_EXTENSIBLE: the real format opens its sub-format.
                    if (format == 0xFFFE && body.Length >= 26)
                        format = BitConverter.ToUInt16(body, 24);
                }
                else if (id == "data")
                {
                    if (format == 0)
                        throw new InvalidDataException("the sound comes before its header");
                    if (size > MaxDataBytes)
                        throw new InvalidDataException($"more than {MaxDataBytes / (1024 * 1024)} MB of sound");
                    return Decode(reader.ReadBytes((int)size), format, channels, bits, frequency);
                }
                else
                    stream.Seek(size, SeekOrigin.Current);

                // Chunks are padded to an even size.
                if ((size & 1) == 1 && stream.Position < stream.Length)
                    stream.Seek(1, SeekOrigin.Current);
            }

            throw new InvalidDataException("no sound in the file");
        }

        private static float[] Decode(byte[] data, int format, int channels, int bits, int frequency)
        {
            if (channels < 1 || channels > 8)
                throw new InvalidDataException($"{channels} channels");
            if (frequency < 4000 || frequency > 192000)
                throw new InvalidDataException($"a rate of {frequency} Hz");
            var pcm = format == 1 && bits is 8 or 16 or 24 or 32;
            var floating = format == 3 && bits == 32;
            if (!pcm && !floating)
                throw new InvalidDataException($"an unknown format ({format}, {bits} bits): save it as PCM");

            var width = bits / 8;
            var frames = data.Length / (width * channels);
            var samples = new float[frames];
            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    var at = (frame * channels + channel) * width;
                    sum += floating ? BitConverter.ToSingle(data, at) : bits switch
                    {
                        8 => (data[at] - 128) / 128f,
                        16 => BitConverter.ToInt16(data, at) / 32768f,
                        24 => ((data[at] << 8 | data[at + 1] << 16 | data[at + 2] << 24) >> 8) / 8388608f,
                        _ => BitConverter.ToInt32(data, at) / 2147483648f,
                    };
                }
                samples[frame] = Math.Clamp(sum / channels, -1f, 1f);
            }

            return samples;
        }

        private static string Tag(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
                throw new EndOfStreamException();
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
