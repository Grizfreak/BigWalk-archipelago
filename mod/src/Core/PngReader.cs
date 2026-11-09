using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BigWalkArchipelago.Core
{
    // Every PNG colour type and depth, not interlaced, for the custom tiles (CustomTiles).
    // Not ImageConversion: its EncodeToPNG crashed the game via Il2CppInterop (2026-10-15).
    internal static class PngReader
    {
        // Checked before anything is inflated: a small file can claim a huge picture.
        internal const int MaxSide = 4096;

        // The pixels as 8-bit RGBA, top row first (as in the file).
        internal static byte[] ReadRgba(Stream stream, out int width, out int height)
        {
            using var reader = new BinaryReader(stream);
            var signature = reader.ReadBytes(8);
            if (signature.Length != 8 || signature[0] != 137 || signature[1] != 80 || signature[2] != 78 || signature[3] != 71)
                throw new InvalidDataException("not a PNG");

            width = height = 0;
            int depth = 0, colour = -1;
            byte[] palette = null, transparency = null;
            using var data = new MemoryStream();
            while (true)
            {
                var length = ReadBigEndian(reader);
                var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (length < 0 || length > stream.Length)
                    throw new InvalidDataException($"a broken {type} chunk");
                var body = reader.ReadBytes(length);
                if (body.Length != length)
                    throw new EndOfStreamException();
                reader.ReadBytes(4); // CRC
                if (type == "IHDR")
                {
                    width = (body[0] << 24) | (body[1] << 16) | (body[2] << 8) | body[3];
                    height = (body[4] << 24) | (body[5] << 16) | (body[6] << 8) | body[7];
                    depth = body[8];
                    colour = body[9];
                    if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
                        throw new InvalidDataException($"{width}x{height}, more than {MaxSide} pixels a side");
                    if (body[12] != 0)
                        throw new InvalidDataException("interlaced: save it again with interlacing off");
                    if (!Supported(colour, depth))
                        throw new InvalidDataException($"an unknown format (depth {depth}, colour type {colour})");
                }
                else if (type == "PLTE")
                    palette = body;
                else if (type == "tRNS")
                    transparency = body;
                else if (type == "IDAT")
                    data.Write(body, 0, body.Length);
                else if (type == "IEND")
                    break;
            }
            if (colour < 0)
                throw new InvalidDataException("no header");
            if (colour == 3 && palette == null)
                throw new InvalidDataException("a palette image without its palette");

            var channels = colour switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
            var bitsPerPixel = channels * depth;
            var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
            var stride = (width * bitsPerPixel + 7) / 8;

            data.Position = 0;
            using var zlib = new ZLibStream(data, CompressionMode.Decompress);
            var pixels = new byte[width * height * 4];
            var line = new byte[stride + 1];
            var previous = new byte[stride];
            var current = new byte[stride];
            for (var y = 0; y < height; y++)
            {
                ReadExactly(zlib, line);
                var filter = line[0];
                for (var x = 0; x < stride; x++)
                {
                    int left = x >= bytesPerPixel ? current[x - bytesPerPixel] : 0;
                    int up = previous[x];
                    int upLeft = x >= bytesPerPixel ? previous[x - bytesPerPixel] : 0;
                    int raw = line[x + 1];
                    current[x] = (byte)(filter switch
                    {
                        0 => raw,
                        1 => raw + left,
                        2 => raw + up,
                        3 => raw + ((left + up) >> 1),
                        4 => raw + Paeth(left, up, upLeft),
                        _ => throw new InvalidDataException($"filter {filter}"),
                    });
                }

                ToRgba(current, pixels, y * width * 4, width, colour, depth, palette, transparency);
                (previous, current) = (current, previous);
            }

            return pixels;
        }

        private static bool Supported(int colour, int depth) => colour switch
        {
            0 => depth is 1 or 2 or 4 or 8 or 16,
            3 => depth is 1 or 2 or 4 or 8,
            2 or 4 or 6 => depth is 8 or 16,
            _ => false,
        };

        private static void ToRgba(byte[] row, byte[] pixels, int at, int width, int colour, int depth,
                                   byte[] palette, byte[] transparency)
        {
            var wide = depth == 16;
            for (var x = 0; x < width; x++, at += 4)
            {
                int r, g, b, a = 255;
                switch (colour)
                {
                    case 0:
                    {
                        var raw = Sample(row, x, depth);
                        r = g = b = Scale(raw, depth);
                        if (transparency is { Length: >= 2 } && raw == ((transparency[0] << 8) | transparency[1]))
                            a = 0;
                        break;
                    }
                    case 3:
                    {
                        var index = Sample(row, x, depth);
                        if (index * 3 + 2 >= palette.Length)
                            throw new InvalidDataException($"palette index {index} out of the palette");
                        r = palette[index * 3];
                        g = palette[index * 3 + 1];
                        b = palette[index * 3 + 2];
                        if (transparency != null && index < transparency.Length)
                            a = transparency[index];
                        break;
                    }
                    case 2:
                    {
                        var step = wide ? 6 : 3;
                        int rr = Channel(row, x * step, wide), gg = Channel(row, x * step + (wide ? 2 : 1), wide),
                            bb = Channel(row, x * step + (wide ? 4 : 2), wide);
                        if (transparency is { Length: >= 6 } && rr == ((transparency[0] << 8) | transparency[1])
                            && gg == ((transparency[2] << 8) | transparency[3]) && bb == ((transparency[4] << 8) | transparency[5]))
                            a = 0;
                        r = wide ? rr >> 8 : rr;
                        g = wide ? gg >> 8 : gg;
                        b = wide ? bb >> 8 : bb;
                        break;
                    }
                    case 4:
                    {
                        var step = wide ? 4 : 2;
                        r = g = b = Channel(row, x * step, wide) >> (wide ? 8 : 0);
                        a = Channel(row, x * step + step / 2, wide) >> (wide ? 8 : 0);
                        break;
                    }
                    default:
                    {
                        var step = wide ? 8 : 4;
                        var half = step / 4;
                        r = Channel(row, x * step, wide) >> (wide ? 8 : 0);
                        g = Channel(row, x * step + half, wide) >> (wide ? 8 : 0);
                        b = Channel(row, x * step + 2 * half, wide) >> (wide ? 8 : 0);
                        a = Channel(row, x * step + 3 * half, wide) >> (wide ? 8 : 0);
                        break;
                    }
                }

                pixels[at] = (byte)r;
                pixels[at + 1] = (byte)g;
                pixels[at + 2] = (byte)b;
                pixels[at + 3] = (byte)a;
            }
        }

        // A grey level or palette index of 1 to 16 bits, packed from the high bits of each byte.
        private static int Sample(byte[] row, int x, int depth)
        {
            if (depth == 16)
                return (row[x * 2] << 8) | row[x * 2 + 1];
            if (depth == 8)
                return row[x];
            var bit = x * depth;
            var shift = 8 - depth - bit % 8;
            return (row[bit / 8] >> shift) & ((1 << depth) - 1);
        }

        private static int Scale(int raw, int depth) => depth == 16 ? raw >> 8 : raw * 255 / ((1 << depth) - 1);

        private static int Channel(byte[] row, int at, bool wide) => wide ? (row[at] << 8) | row[at + 1] : row[at];

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int ReadBigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
                throw new EndOfStreamException();
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var got = stream.Read(buffer, read, buffer.Length - read);
                if (got <= 0)
                    throw new EndOfStreamException();
                read += got;
            }
        }
    }
}
