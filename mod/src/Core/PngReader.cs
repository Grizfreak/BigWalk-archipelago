using System;
using System.IO;
using System.IO.Compression;

namespace BigWalkArchipelago.Core
{
    // Reads the PNGs the mod carries (the custom tiles, mod/assets/tiles): 8-bit RGBA, not interlaced, which is what
    // they are saved as. Not Unity's ImageConversion: through Il2CppInterop its EncodeToPNG crashed the game
    // (2026-10-15), and LoadImage goes through the same module.
    internal static class PngReader
    {
        // The pixels as RGBA bytes, top row first (as in the file).
        internal static byte[] ReadRgba(Stream stream, out int width, out int height)
        {
            using var reader = new BinaryReader(stream);
            var signature = reader.ReadBytes(8);
            if (signature.Length != 8 || signature[0] != 137 || signature[1] != 80 || signature[2] != 78 || signature[3] != 71)
                throw new InvalidDataException("not a PNG");

            width = height = 0;
            using var data = new MemoryStream();
            while (true)
            {
                var length = ReadBigEndian(reader);
                var type = new string(reader.ReadChars(4));
                var body = reader.ReadBytes(length);
                reader.ReadBytes(4); // CRC
                if (type == "IHDR")
                {
                    width = (body[0] << 24) | (body[1] << 16) | (body[2] << 8) | body[3];
                    height = (body[4] << 24) | (body[5] << 16) | (body[6] << 8) | body[7];
                    if (body[8] != 8 || body[9] != 6 || body[12] != 0)
                        throw new InvalidDataException($"only 8-bit RGBA, not interlaced (depth {body[8]}, colour {body[9]})");
                }
                else if (type == "IDAT")
                    data.Write(body, 0, body.Length);
                else if (type == "IEND")
                    break;
            }

            data.Position = 0;
            using var zlib = new ZLibStream(data, CompressionMode.Decompress);
            const int bytesPerPixel = 4;
            var stride = width * bytesPerPixel;
            var pixels = new byte[stride * height];
            var line = new byte[stride + 1];
            for (var y = 0; y < height; y++)
            {
                ReadExactly(zlib, line);
                var filter = line[0];
                var row = y * stride;
                for (var x = 0; x < stride; x++)
                {
                    int left = x >= bytesPerPixel ? pixels[row + x - bytesPerPixel] : 0;
                    int up = y > 0 ? pixels[row - stride + x] : 0;
                    int upLeft = x >= bytesPerPixel && y > 0 ? pixels[row - stride + x - bytesPerPixel] : 0;
                    int raw = line[x + 1];
                    pixels[row + x] = (byte)(filter switch
                    {
                        0 => raw,
                        1 => raw + left,
                        2 => raw + up,
                        3 => raw + ((left + up) >> 1),
                        4 => raw + Paeth(left, up, upLeft),
                        _ => throw new InvalidDataException($"filter {filter}"),
                    });
                }
            }

            return pixels;
        }

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
