using System;
using System.IO;
using System.Net;
using Avalonia.Media.Imaging;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private static string DownloadLibraryCover(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !String.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Choose an HTTPS cover address.");
            var request = (HttpWebRequest)WebRequest.Create(uri); request.AllowAutoRedirect = false; request.Timeout = 20000; request.ReadWriteTimeout = 20000;
            using (var response = request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[8192]; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { if (memory.Length + count > 4 * 1024 * 1024) throw new IOException("Cover exceeds 4 MB."); memory.Write(buffer, 0, count); }
                ValidateCoverDimensions(memory.ToArray()); memory.Position = 0;
                using (var image = new Bitmap(memory))
                {
                    if (image.PixelSize.Width > 4096 || image.PixelSize.Height > 4096) throw new IOException("Cover dimensions exceed 4096 pixels.");
                    string folder = Path.Combine(Store.DataDirectory, "Artwork", "GameCovers"); Directory.CreateDirectory(folder);
                    string target = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".png"); image.Save(target); return target;
                }
            }
        }

        private static void ValidateCoverDimensions(byte[] bytes)
        {
            int width = 0, height = 0;
            if (bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71)
            { width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]; height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]; }
            else if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216)
            {
                int offset = 2;
                while (offset + 4 < bytes.Length)
                {
                    if (bytes[offset++] != 255) throw new IOException("Cover JPEG header is invalid.");
                    while (offset < bytes.Length && bytes[offset] == 255) offset++;
                    if (offset >= bytes.Length) break; int marker = bytes[offset++];
                    if (marker == 217 || marker == 218) break;
                    if (marker == 1 || marker >= 208 && marker <= 215) continue;
                    if (offset + 2 > bytes.Length) break; int length = (bytes[offset] << 8) | bytes[offset + 1];
                    if (length < 2 || offset + length > bytes.Length) break;
                    if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                    { if (length < 8) break; height = (bytes[offset + 3] << 8) | bytes[offset + 4]; width = (bytes[offset + 5] << 8) | bytes[offset + 6]; break; }
                    offset += length;
                }
            }
            if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new IOException("Choose a PNG or JPEG cover up to 4096 pixels per side.");
        }
    }
}
