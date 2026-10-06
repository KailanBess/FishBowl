#if NETCOREAPP
using System;
using System.IO;
using System.IO.Compression;

// Linux stand-ins for Windows-only types that shared files (FishBowl.GameRecognition.cs) use. System.Drawing is
// Windows-only on .NET, so these provide just what game identification needs: pixel icons saved as PNG, and
// existing PNG/ICO/JPEG artwork kept as-is (the Linux interface decodes those itself). Not compiled on Windows.
namespace EmulatorHub.Imaging
{
    public struct Color
    {
        public byte A, R, G, B;
        public static Color FromArgb(int red, int green, int blue) { return new Color { A = 255, R = (byte)red, G = (byte)green, B = (byte)blue }; }
        public static Color FromArgb(int alpha, int red, int green, int blue) { return new Color { A = (byte)alpha, R = (byte)red, G = (byte)green, B = (byte)blue }; }
        public static readonly Color Transparent = new Color();
    }

    public struct Size
    {
        public int Width, Height;
        public Size(int width, int height) { Width = width; Height = height; }
    }

    public sealed class ImageFormat
    {
        public static readonly ImageFormat Png = new ImageFormat();
    }

    // An image read from a file or stream: its encoded bytes and, where the header says, its size.
    public class Image : IDisposable
    {
        public int Width, Height;
        internal byte[] Encoded;

        public static Image FromFile(string path) { return FromBytes(File.ReadAllBytes(path)); }
        public static Image FromStream(Stream stream) { using (var memory = new MemoryStream()) { stream.CopyTo(memory); return FromBytes(memory.ToArray()); } }

        private static Image FromBytes(byte[] data)
        {
            var image = new Image { Encoded = data };
            if (data.Length > 24 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
            { image.Width = BigEndian(data, 16); image.Height = BigEndian(data, 20); }
            else if (data.Length > 8 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0)
            { image.Width = data[6] == 0 ? 256 : data[6]; image.Height = data[7] == 0 ? 256 : data[7]; }
            else if (data.Length > 4 && data[0] == 0xFF && data[1] == 0xD8)
            {
                for (int i = 2; i + 9 < data.Length; )
                {
                    if (data[i] != 0xFF) break;
                    int marker = data[i + 1], length = (data[i + 2] << 8) | data[i + 3];
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    { image.Height = (data[i + 5] << 8) | data[i + 6]; image.Width = (data[i + 7] << 8) | data[i + 8]; break; }
                    i += 2 + length;
                }
            }
            else throw new ArgumentException("Unsupported image format.");
            return image;
        }
        private static int BigEndian(byte[] b, int i) { return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]; }

        public void Dispose() { }
    }

    // A pixel image (decoded console icons) or a kept copy of an encoded image.
    public sealed class Bitmap : Image
    {
        private readonly Color[] pixels;

        public Bitmap(int width, int height) { Width = width; Height = height; pixels = new Color[width * height]; }
        // Linux keeps the original artwork at full quality instead of resizing; the interface scales it.
        public Bitmap(Image original, Size size)
        {
            Width = original.Width; Height = original.Height; Encoded = original.Encoded;
            var bitmap = original as Bitmap;
            if (bitmap != null && bitmap.pixels != null) pixels = (Color[])bitmap.pixels.Clone();
        }
        public Bitmap(Bitmap other) : this(other, new Size(other.Width, other.Height)) { }

        public void SetPixel(int x, int y, Color color) { pixels[y * Width + x] = color; }
        public Color GetPixel(int x, int y) { return pixels[y * Width + x]; }

        public void Save(string path, ImageFormat format)
        {
            if (pixels == null) { File.WriteAllBytes(path, Encoded); return; }
            File.WriteAllBytes(path, Png.Encode(Width, Height, pixels));
        }
    }

    // Minimal PNG encoder: 8-bit RGBA, no filtering, zlib-compressed.
    internal static class Png
    {
        private static uint[] crcTable;

        public static byte[] Encode(int width, int height, Color[] pixels)
        {
            var raw = new byte[height * (width * 4 + 1)];
            for (int y = 0, o = 0; y < height; y++)
            {
                raw[o++] = 0;
                for (int x = 0; x < width; x++) { var c = pixels[y * width + x]; raw[o++] = c.R; raw[o++] = c.G; raw[o++] = c.B; raw[o++] = c.A; }
            }
            using (var output = new MemoryStream())
            {
                output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
                var header = new byte[13];
                Put(header, 0, (uint)width); Put(header, 4, (uint)height); header[8] = 8; header[9] = 6;
                Chunk(output, "IHDR", header);
                Chunk(output, "IDAT", Zlib(raw));
                Chunk(output, "IEND", new byte[0]);
                return output.ToArray();
            }
        }

        private static byte[] Zlib(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78); output.WriteByte(0x9C);
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true)) deflate.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                foreach (byte value in data) { a = (a + value) % 65521; b = (b + a) % 65521; }
                var adler = new byte[4]; Put(adler, 0, (b << 16) | a); output.Write(adler, 0, 4);
                return output.ToArray();
            }
        }

        private static void Chunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4]; Put(length, 0, (uint)data.Length); output.Write(length, 0, 4);
            var body = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, body, 4, data.Length);
            output.Write(body, 0, body.Length);
            var crc = new byte[4]; Put(crc, 0, Crc(body)); output.Write(crc, 0, 4);
        }

        private static uint Crc(byte[] data)
        {
            if (crcTable == null)
            {
                var table = new uint[256];
                for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; table[n] = c; }
                crcTable = table;
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte value in data) crc = crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        private static void Put(byte[] b, int i, uint value) { b[i] = (byte)(value >> 24); b[i + 1] = (byte)(value >> 16); b[i + 2] = (byte)(value >> 8); b[i + 3] = (byte)value; }
    }
}

namespace EmulatorHub
{
    // The Windows app's profile tools are not ported yet; Linux has no guest mode, so recognition always applies.
    // Remove this when UserTools becomes shared.
    public static class UserTools
    {
        public static bool Guest { get { return false; } }
    }
}
#endif
