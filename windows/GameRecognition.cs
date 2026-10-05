using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EmulatorHub
{
    public sealed class RecognizedGame : IDisposable
    {
        public string Title;
        public string TitleId;
        public string Platform;
        public string Developer;
        public string Source;
        public Bitmap Icon;
        public void Dispose() { if (Icon != null) Icon.Dispose(); }
    }

    // Local, bounded reads only. Encrypted content is never decrypted or modified.
    public static class GameRecognition
    {
        static byte[] Read(Stream s, long offset, int count)
        {
            if (offset < 0 || count < 0 || count > 1024 * 1024 || offset > s.Length - count)
                throw new InvalidDataException("Game metadata is outside the file.");
            s.Position = offset;
            byte[] bytes = new byte[count];
            int done = 0, n;
            while (done < count && (n = s.Read(bytes, done, count - done)) > 0) done += n;
            if (done != count) throw new EndOfStreamException();
            return bytes;
        }
        static uint U32(byte[] b, int p) { return BitConverter.ToUInt32(b, p); }
        static ulong U64(byte[] b, int p) { return BitConverter.ToUInt64(b, p); }
        static long Align(long n) { return checked((n + 63) & ~63L); }
        static string Text(byte[] b, int p, int n, Encoding encoding)
        {
            return encoding.GetString(b, p, n).Split('\0')[0].Trim();
        }
        static bool Magic(byte[] b, int p, string text)
        {
            return p >= 0 && p + text.Length <= b.Length && Encoding.ASCII.GetString(b, p, text.Length) == text;
        }
        public static string CleanTitle(string path)
        {
            string title = Path.GetFileNameWithoutExtension(path) ?? "";
            title = Regex.Replace(title, @"(?i)\.(?:piratelegit|legit)$", "");
            title = Regex.Replace(title, @"(?i)\b[0-9a-f]{16}\b", "");
            title = Regex.Replace(title, @"(?i)\[(?:[0-9a-f]{16}|v\d+|PC[A-Z]{2}\d{5})\]", "");
            title = title.Replace("[]", "");
            title = Regex.Replace(title, @"(?i)\((?:[^()]*\.com|CTR-P-[A-Z0-9]+|v\d[^()]*|U|E|J|W|USA|Europe|Japan)\)", "");
            return Regex.Replace(title, @"\s+", " ").Trim(' ', '-', '_');
        }
        public static RecognizedGame Inspect(string path)
        {
            var r = new RecognizedGame { Title = CleanTitle(path), Platform = GameStorage.ConsoleFor(path), Source = "File name" };
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".nsp" || extension == ".xci" || extension == ".nca") r.Platform = "Nintendo Switch";
            if (extension == ".cxi" || extension == ".cci" || extension == ".ncch" || extension == ".smdh") r.Platform = "Nintendo 3DS";
            Match id = Regex.Match(Path.GetFileName(path) ?? "", @"(?i)(?<![0-9a-f])[0-9a-f]{16}(?![0-9a-f])|PC[A-Z]{2}\d{5}");
            if (id.Success) r.TitleId = id.Value.ToUpperInvariant();
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".vpk") ReadVpk(path, r);
                else if (ext == ".exe" || ext == ".lnk")
                {
                    r.Platform = "PC";
                    if (ext == ".exe")
                    {
                        var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                        if (!string.IsNullOrWhiteSpace(v.ProductName)) { r.Title = v.ProductName; r.Source = "Program metadata"; }
                        r.Developer = v.CompanyName;
                    }
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path)) if (icon != null) r.Icon = icon.ToBitmap();
                }
                else if (ext == ".nds" || ext == ".smdh" || ext == ".cia" || ext == ".3ds" || ext == ".cci" || ext == ".cxi" || ext == ".app" || ext == ".ncch" || ext == ".pkg" || ext == ".gba" || ext == ".gb" || ext == ".gbc" || ext == ".z64" || ext == ".n64" || ext == ".v64" || ext == ".iso" || ext == ".gcm")
                {
                    using (var s = File.OpenRead(path))
                    {
                        if (ext == ".nds") ReadNds(s, r);
                        else if (ext == ".smdh") ReadSmdh(Read(s, 0, 0x36c0), r);
                        else if (ext == ".cia") ReadCia(s, r);
                        else if (ext == ".pkg")
                        {
                            byte[] h = Read(s, 0, 0x60);
                            if (h[0] == 0x7f && Magic(h, 1, "PKG"))
                            {
                                var match = Regex.Match(Text(h, 0x30, 0x30, Encoding.ASCII), @"PC[A-Z]{2}\d{5}");
                                if (match.Success) { r.TitleId = match.Value; r.Platform = "PlayStation Vita"; r.Source = "Package content ID"; }
                            }
                        }
                        else if (ext == ".gba" || ext == ".gb" || ext == ".gbc" || ext == ".z64" || ext == ".n64" || ext == ".v64" || ext == ".iso" || ext == ".gcm") ReadHeader(s, ext, r);
                        else ReadNcch(s, 0, r);
                    }
                }
                if (r.Icon == null)
                {
                    string stem = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
                    foreach (string image in new[] { stem + ".png", stem + ".ico", Path.Combine(Path.GetDirectoryName(path), "icon0.png") })
                    {
                        if (File.Exists(image) && new FileInfo(image).Length <= 4 * 1024 * 1024)
                        {
                            using (Image original = Image.FromFile(image))
                                if (original.Width <= 4096 && original.Height <= 4096) r.Icon = new Bitmap(original, new Size(128, 128));
                            if (r.Icon != null) break;
                        }
                    }
                }
            }
            catch (Exception e) { if (!(e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is OverflowException || e is System.Runtime.InteropServices.ExternalException)) { r.Dispose(); throw; } }
            if (string.IsNullOrWhiteSpace(r.Title)) r.Title = Path.GetFileNameWithoutExtension(path);
            return r;
        }
        static void ReadHeader(Stream s, string ext, RecognizedGame r)
        {
            byte[] h;
            string name = null, id = null;
            if (ext == ".gba")
            {
                h = Read(s, 0, 0xc0);
                if (h[0xb2] != 0x96) return;
                name = Text(h, 0xa0, 12, Encoding.ASCII); id = Text(h, 0xac, 4, Encoding.ASCII);
            }
            else if (ext == ".gb" || ext == ".gbc")
            {
                h = Read(s, 0, 0x150);
                if (h[0x104] != 0xce || h[0x105] != 0xed || h[0x106] != 0x66) return;
                name = Text(h, 0x134, (h[0x143] & 0x80) != 0 ? 11 : 16, Encoding.ASCII);
            }
            else if (ext == ".n64" || ext == ".z64" || ext == ".v64")
            {
                h = Read(s, 0, 0x40);
                if (h[0] == 0x37 && h[1] == 0x80) for (int i = 0; i < h.Length; i += 2) { byte b = h[i]; h[i] = h[i + 1]; h[i + 1] = b; }
                else if (h[0] == 0x40 && h[3] == 0x80) for (int i = 0; i < h.Length; i += 4) Array.Reverse(h, i, 4);
                if (h[0] != 0x80 || h[1] != 0x37 || h[2] != 0x12 || h[3] != 0x40) return;
                name = Text(h, 0x20, 20, Encoding.ASCII); id = Text(h, 0x3b, 4, Encoding.ASCII);
            }
            else
            {
                h = Read(s, 0, 0x400);
                if (h[0x18] == 0x5d && h[0x19] == 0x1c && h[0x1a] == 0x9e && h[0x1b] == 0xa3) r.Platform = "Nintendo Wii";
                else if (h[0x1c] == 0xc2 && h[0x1d] == 0x33 && h[0x1e] == 0x9f && h[0x1f] == 0x3d) r.Platform = "Nintendo GameCube";
                else return;
                name = Text(h, 0x20, 0x3e0, Encoding.ASCII); id = Text(h, 0, 6, Encoding.ASCII);
            }
            if (!string.IsNullOrWhiteSpace(name) && !name.Any(c => char.IsControl(c))) { r.Title = name; r.Source = "Game header"; }
            if (!string.IsNullOrWhiteSpace(id) && Regex.IsMatch(id, "^[A-Z0-9]{4,6}$")) r.TitleId = id;
        }
        static void ReadCia(Stream s, RecognizedGame r)
        {
            byte[] h = Read(s, 0, 32);
            uint header = U32(h, 0), cert = U32(h, 8), ticket = U32(h, 12), tmd = U32(h, 16), meta = U32(h, 20);
            if (header < 32 || header > 0x10000 || cert > 1024 * 1024 || ticket > 1024 * 1024 || tmd > 1024 * 1024) return;
            long tmdOffset = Align(Align(Align(header) + cert) + ticket);
            byte[] signature = Read(s, tmdOffset, 4);
            uint signatureType = (uint)(signature[0] << 24 | signature[1] << 16 | signature[2] << 8 | signature[3]);
            int body = signatureType == 0x10003 || signatureType == 0x10000 ? 0x240 : signatureType == 0x10004 || signatureType == 0x10001 ? 0x140 : signatureType == 0x10005 || signatureType == 0x10002 ? 0x80 : -1;
            if (body < 0 || tmd < body + 0x54) return;
            byte[] title = Read(s, tmdOffset + body + 0x4c, 8);
            r.TitleId = BitConverter.ToString(title).Replace("-", "");
            r.Platform = "Nintendo 3DS"; r.Source = "CIA title metadata";
            long content = Align(tmdOffset + tmd);
            long metaOffset = Align(checked(content + (long)U64(h, 24)));
            if (meta >= 0x400 + 0x36c0 && metaOffset <= s.Length - meta)
                ReadSmdh(Read(s, metaOffset + 0x400, 0x36c0), r);
            if (r.Icon == null) ReadNcch(s, content, r);
        }
        static void ReadNcch(Stream s, long start, RecognizedGame r)
        {
            byte[] h = Read(s, start, 0x200);
            if (Magic(h, 0x100, "NCSD"))
            {
                long partition = U32(h, 0x120) * 512L;
                if (partition < 0x200) return;
                start += partition; h = Read(s, start, 0x200);
            }
            if (!Magic(h, 0x100, "NCCH")) return;
            if (string.IsNullOrWhiteSpace(r.TitleId)) r.TitleId = U64(h, 0x118).ToString("X16");
            r.Platform = "Nintendo 3DS"; r.Source = "NCCH title metadata";
            long exefs = start + U32(h, 0x1a0) * 512L;
            if (exefs == start) return;
            byte[] entries = Read(s, exefs, 0x200);
            for (int i = 0; i < 10; i++)
            {
                int p = i * 16;
                if (Text(entries, p, 8, Encoding.ASCII) == "icon" && U32(entries, p + 12) >= 0x36c0)
                { ReadSmdh(Read(s, exefs + 0x200 + U32(entries, p + 8), 0x36c0), r); return; }
            }
        }
        public static void ReadSmdh(byte[] b, RecognizedGame r)
        {
            if (b.Length < 0x36c0 || !Magic(b, 0, "SMDH")) return;
            int entry = 8 + 0x200; // English, with Japanese fallback.
            if (string.IsNullOrWhiteSpace(Text(b, entry, 0x80, Encoding.Unicode))) entry = 8;
            string title = Text(b, entry + 0x80, 0x100, Encoding.Unicode);
            if (string.IsNullOrWhiteSpace(title)) title = Text(b, entry, 0x80, Encoding.Unicode);
            if (!string.IsNullOrWhiteSpace(title)) r.Title = Regex.Replace(title, @"\s+", " ");
            r.Developer = Text(b, entry + 0x180, 0x80, Encoding.Unicode);
            if (r.Icon != null) r.Icon.Dispose();
            r.Icon = new Bitmap(48, 48);
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
            {
                int a = x & 7, c = y & 7;
                int morton = (a & 1) | ((c & 1) << 1) | ((a & 2) << 1) | ((c & 2) << 2) | ((a & 4) << 2) | ((c & 4) << 3);
                int p = 0x24c0 + ((y / 8 * 6 + x / 8) * 64 + morton) * 2;
                int rgb = BitConverter.ToUInt16(b, p);
                r.Icon.SetPixel(x, y, Color.FromArgb((rgb >> 11) * 255 / 31, ((rgb >> 5) & 63) * 255 / 63, (rgb & 31) * 255 / 31));
            }
            r.Platform = "Nintendo 3DS"; r.Source = "Embedded SMDH";
        }
        static void ReadNds(Stream s, RecognizedGame r)
        {
            byte[] h = Read(s, 0, 0x70);
            string code = Text(h, 12, 4, Encoding.ASCII);
            if (Regex.IsMatch(code, "^[A-Z0-9]{4}$")) r.TitleId = code;
            r.Platform = "Nintendo DS";
            long banner = U32(h, 0x68);
            if (banner < 0x70) return;
            byte[] b = Read(s, banner, 0x840);
            string title = Text(b, 0x340, 0x100, Encoding.Unicode);
            if (string.IsNullOrWhiteSpace(title)) title = Text(b, 0x240, 0x100, Encoding.Unicode);
            if (!string.IsNullOrWhiteSpace(title)) r.Title = Regex.Replace(title, @"\s+", " ");
            r.Icon = new Bitmap(32, 32);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                int pixel = ((y / 8 * 4 + x / 8) * 64) + (y % 8 * 8) + x % 8;
                int index = (b[0x20 + pixel / 2] >> ((pixel & 1) * 4)) & 15;
                int rgb = BitConverter.ToUInt16(b, 0x220 + index * 2);
                r.Icon.SetPixel(x, y, index == 0 ? Color.Transparent : Color.FromArgb((rgb & 31) * 255 / 31, ((rgb >> 5) & 31) * 255 / 31, ((rgb >> 10) & 31) * 255 / 31));
            }
            r.Source = "Embedded DS banner";
        }
        public static void ReadSfo(byte[] b, RecognizedGame r)
        {
            if (b.Length < 20 || b[0] != 0 || !Magic(b, 1, "PSF")) return;
            uint keys = U32(b, 8), values = U32(b, 12), count = U32(b, 16);
            if (count > 256 || 20L + count * 16 > b.Length) return;
            for (int i = 0; i < count; i++)
            {
                int p = 20 + i * 16;
                long key = keys + (long)BitConverter.ToUInt16(b, p), value = values + (long)U32(b, p + 12);
                uint length = U32(b, p + 4);
                if (key < 0 || key >= b.Length || value < 0 || length > 8192 || value + length > b.Length) continue;
                string name = Text(b, (int)key, Math.Min(64, b.Length - (int)key), Encoding.UTF8);
                string text = Text(b, (int)value, (int)length, Encoding.UTF8);
                if (name == "TITLE" && !string.IsNullOrWhiteSpace(text)) r.Title = text;
                if (name == "TITLE_ID" && Regex.IsMatch(text, @"^PC[A-Z]{2}\d{5}$")) r.TitleId = text;
            }
            r.Platform = "PlayStation Vita"; r.Source = "Vita param.sfo";
        }
        static void ReadVpk(string path, RecognizedGame r)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                if (zip.Entries.Count > 100000) return;
                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    bool sfo = name.Equals("sce_sys/param.sfo", StringComparison.OrdinalIgnoreCase);
                    bool icon = name.Equals("sce_sys/icon0.png", StringComparison.OrdinalIgnoreCase);
                    if ((!sfo && !icon) || entry.Length > 4 * 1024 * 1024) continue;
                    using (var input = entry.Open()) using (var memory = new MemoryStream())
                    {
                        input.CopyTo(memory);
                        if (sfo) ReadSfo(memory.ToArray(), r);
                        else { memory.Position = 0; using (var original = Image.FromStream(memory)) if (original.Width <= 4096 && original.Height <= 4096) r.Icon = new Bitmap(original, new Size(128, 128)); }
                    }
                }
            }
        }
        public static string CacheIcon(Bitmap image, string path)
        {
            if (image == null) return null;
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
            string folder = Path.Combine(Store.DataDirectory, "Artwork", "GameIcons");
            Directory.CreateDirectory(folder);
            // Unique files avoid stale thumbnail caches and never overwrite custom artwork.
            string dest = Path.Combine(folder, hash.Substring(0, 20) + "-" + Guid.NewGuid().ToString("N") + ".png");
            image.Save(dest, ImageFormat.Png);
            return dest;
        }
        public static void Apply(GameEntry g, IEnumerable<EmulatorProfile> profiles, bool replaceTitle)
        {
            if (g == null || !File.Exists(g.Path) || UserTools.Guest) return;
            using (var r = Inspect(g.Path))
            {
                if (replaceTitle || string.IsNullOrWhiteSpace(g.Title) || g.Title == "New game") g.Title = r.Title;
                if (string.IsNullOrWhiteSpace(g.TitleId)) g.TitleId = r.TitleId;
                if (string.IsNullOrWhiteSpace(g.ConsoleLabel) || g.ConsoleLabel.StartsWith("Needs review")) g.ConsoleLabel = r.Platform;
                if (string.IsNullOrWhiteSpace(g.Developer)) g.Developer = r.Developer;
                var all = (profiles ?? Enumerable.Empty<EmulatorProfile>()).ToList();
                if (string.IsNullOrWhiteSpace(g.EmulatorId))
                {
                    var matches = all.Where(e => (e.Extensions ?? new List<string>()).Any(ext => ext.TrimStart('.').Equals(Path.GetExtension(g.Path).TrimStart('.'), StringComparison.OrdinalIgnoreCase))).ToList();
                    if (matches.Count == 1) g.EmulatorId = matches[0].Id;
                }
                var emulator = all.FirstOrDefault(e => e.Id == (g.PreferredEmulatorId ?? g.EmulatorId));
                string installed = InstalledGames.Find(emulator, g.TitleId, g.Path);
                if (installed != null && (r.Icon == null || r.Source == "File name" || r.Source == "Package content ID" || r.Source == "CIA title metadata")) InstalledGames.Metadata(installed, r);
                if ((replaceTitle || g.Title == "New game" || g.Title == CleanTitle(g.Path)) && r.Source != "File name") g.Title = r.Title;
                if (string.IsNullOrWhiteSpace(g.Developer)) g.Developer = r.Developer;
                if (string.IsNullOrWhiteSpace(g.ArtworkPath) || !File.Exists(g.ArtworkPath))
                    if (r.Icon != null) g.ArtworkPath = CacheIcon(r.Icon, g.Path);
            }
        }
    }

    public static class ThreeDsStorage
    {
        public static string UserRoot(EmulatorProfile e, string roaming)
        {
            string program = Path.GetDirectoryName(e.Executable);
            string portable = Path.Combine(program, "user");
            if (Directory.Exists(portable)) return portable;
            string plus = Path.Combine(roaming, "AzaharPlus");
            bool isPlus = Regex.IsMatch((e.Preset ?? "") + " " + e.Executable, @"(?i)azahar[\s_-]*plus");
            if (isPlus && Directory.Exists(plus) && IsLegacyConfig(e.ConfigFolder, roaming)) return plus;
            if (!string.IsNullOrWhiteSpace(e.ConfigFolder))
            {
                string config = e.ConfigFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(Path.GetFileName(config), "config", StringComparison.OrdinalIgnoreCase)) return Path.GetDirectoryName(config);
            }
            string identity = (e.Preset ?? "") + " " + e.Executable;
            string name = Regex.IsMatch(identity, @"(?i)azahar[\s_-]*plus") ? "AzaharPlus" :
                Regex.IsMatch(identity, @"(?i)citra") ? "Citra" : Regex.IsMatch(identity, @"(?i)lime3ds") ? "Lime3DS" : "Azahar";
            return Path.Combine(roaming, name);
        }
        static bool IsLegacyConfig(string config, string roaming)
        {
            return !string.IsNullOrWhiteSpace(config) && string.Equals(config.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.Combine(roaming, "Azahar", "config"), StringComparison.OrdinalIgnoreCase);
        }
        public static string ConfigDirectory(EmulatorProfile e, string roaming, string userRoot)
        {
            if (string.IsNullOrWhiteSpace(e.ConfigFolder) || (string.Equals(userRoot, Path.Combine(roaming, "AzaharPlus"), StringComparison.OrdinalIgnoreCase) && IsLegacyConfig(e.ConfigFolder, roaming))) return Path.Combine(userRoot, "config");
            return e.ConfigFolder;
        }
    }

    public static class InstalledGames
    {
        static bool Is3ds(EmulatorProfile e) { return e != null && Regex.IsMatch((e.Preset ?? "") + " " + Path.GetFileName(e.Executable), @"(?i)azahar|citra|lime3ds"); }
        static bool IsVita(EmulatorProfile e) { return e != null && Regex.IsMatch(Path.GetFileName(e.Executable) ?? "", @"(?i)^vita3k"); }
        public static bool IsPackage(EmulatorProfile e, string source)
        {
            string ext = Path.GetExtension(source).ToLowerInvariant();
            return (Is3ds(e) && ext == ".cia") || (IsVita(e) && (ext == ".pkg" || ext == ".vpk"));
        }
        static IEnumerable<string> ChildDirs(string root)
        {
            try { return Directory.Exists(root) ? Directory.GetDirectories(root).Take(128).Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0).ToArray() : new string[0]; }
            catch (IOException) { return new string[0]; } catch (UnauthorizedAccessException) { return new string[0]; }
        }
        public static IEnumerable<string> Roots(EmulatorProfile e) { return Roots(e, new EmulatorFolderDetector()); }
        public static IEnumerable<string> Roots(EmulatorProfile e, EmulatorFolderDetector detector)
        {
            var roots = new List<string>();
            if (e == null || string.IsNullOrWhiteSpace(e.Executable)) return roots;
            string program = Path.GetDirectoryName(e.Executable);
            if (Is3ds(e))
            {
                // Save backup overrides must not replace the emulator's active SD storage.
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var active = serializer.Deserialize<EmulatorProfile>(serializer.Serialize(e));
                active.InGameSaveFolder = "";
                if (Regex.IsMatch(e.Executable ?? "", @"(?i)azahar[\s_-]*plus")) active.Preset = "Azahar Plus";
                try { roots.AddRange(detector.Detect(active).Where(f => f.Property == "InGameSaveFolder").Select(f => f.Path)); } catch (IOException) { }
                if (roots.Count == 0)
                    roots.Add(Path.Combine(ThreeDsStorage.UserRoot(e, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), "sdmc", "Nintendo 3DS"));
                if (!string.IsNullOrWhiteSpace(e.InGameSaveFolder))
                {
                    string manual = e.InGameSaveFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    roots.Add(manual);
                    if (string.Equals(Path.GetFileName(manual), "sdmc", StringComparison.OrdinalIgnoreCase)) roots.Add(Path.Combine(manual, "Nintendo 3DS"));
                }
            }
            if (IsVita(e))
            {
                string config = Path.Combine(program, "config.yml");
                if (File.Exists(config) && new FileInfo(config).Length < 1024 * 1024)
                {
                    var match = Regex.Match(File.ReadAllText(config), @"(?m)^pref-path:\s*(.+?)\s*$");
                    if (match.Success)
                    {
                        string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                        if (path.StartsWith("C:\\\\") || path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
                        roots.Add(Path.IsPathRooted(path) ? path : Path.Combine(program, path));
                    }
                }
                if (roots.Count == 0) roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vita3K", "Vita3K"));
            }
            return roots.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        public static string Find(EmulatorProfile e, string id, string source)
        {
            if (e == null || string.IsNullOrWhiteSpace(id)) return null;
            if (IsVita(e) && Regex.IsMatch(id, @"^PC[A-Z]{2}\d{5}$"))
            {
                foreach (string root in Roots(e))
                {
                    string app = Path.Combine(root, "ux0", "app", id);
                    if (File.Exists(Path.Combine(app, "eboot.bin")) && File.Exists(Path.Combine(app, "sce_sys", "param.sfo"))) return app;
                }
            }
            if (Is3ds(e) && Regex.IsMatch(id, @"(?i)^00040000[0-9a-f]{8}$"))
            {
                foreach (string root in Roots(e)) foreach (string first in ChildDirs(root)) foreach (string second in ChildDirs(first))
                {
                    string content = Path.Combine(second, "title", id.Substring(0, 8).ToLowerInvariant(), id.Substring(8).ToLowerInvariant(), "content");
                    if (!Directory.Exists(content)) continue;
                    bool hasMetadata;
                    string main = MainContent(content, id, out hasMetadata);
                    if (main != null) return main;
                    if (hasMetadata) continue;
                    foreach (string app in Directory.GetFiles(content, "*.app").Take(64))
                    {
                        if (ExecutableContent(app, id)) return app;
                    }
                }
            }
            return null;
        }
        static uint Big32(byte[] b, int offset) { return ((uint)b[offset] << 24) | ((uint)b[offset+1] << 16) | ((uint)b[offset+2] << 8) | b[offset+3]; }
        static int Big16(byte[] b, int offset) { return (b[offset] << 8) | b[offset+1]; }
        static bool ExecutableContent(string path, string id)
        {
            try
            {
                using(var file = File.OpenRead(path))
                {
                    byte[] h = new byte[0x200];
                    if (file.Read(h,0,h.Length) != h.Length || Encoding.ASCII.GetString(h,0x100,4) != "NCCH") return false;
                    return BitConverter.ToUInt64(h,0x118).ToString("X16").Equals(id,StringComparison.OrdinalIgnoreCase)
                        && (h[0x18d] & 2) != 0 && BitConverter.ToUInt32(h,0x180) != 0 && BitConverter.ToUInt32(h,0x1a0) != 0;
                }
            }
            catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
        }
        static string MainContent(string content, string id, out bool hasMetadata)
        {
            hasMetadata = false;
            try
            {
                // Azahar uses the smallest numbered installed TMD and its first content record.
                string tmd = Directory.GetFiles(content,"*.tmd").Where(p => Regex.IsMatch(Path.GetFileNameWithoutExtension(p),"^[0-9a-fA-F]{8}$"))
                    .OrderBy(p => Convert.ToUInt32(Path.GetFileNameWithoutExtension(p),16)).FirstOrDefault();
                if(tmd == null || new FileInfo(tmd).Length > 1024*1024) return null;
                byte[] b = File.ReadAllBytes(tmd);
                if(b.Length < 4) return null;
                uint signature = Big32(b,0);
                int start = signature == 0x10003 || signature == 0x10000 ? 0x240 : signature == 0x10004 || signature == 0x10001 ? 0x140 : signature == 0x10005 || signature == 0x10002 ? 0x80 : -1;
                if(start < 0 || b.Length < start+0x9c4+0x30) return null;
                int count = Big16(b,start+0x9e);
                if(count == 0 || b.Length < start+0x9c4+count*0x30 || !BitConverter.ToString(b,start+0x4c,8).Replace("-","").Equals(id,StringComparison.OrdinalIgnoreCase)) return null;
                hasMetadata = true;
                string main = Path.Combine(content,Big32(b,start+0x9c4).ToString("x8")+".app");
                if(!File.Exists(main)) return null;
                // Wrapped/compressed installed content is decoded by Azahar, not FishBowl.
                using(var file = File.OpenRead(main))
                {
                    byte[] h = new byte[0x104];
                    if(file.Read(h,0,h.Length) != h.Length) return null;
                    if(Encoding.ASCII.GetString(h,0x100,4) == "NCCH" && !ExecutableContent(main,id)) return null;
                }
                return main;
            }
            catch(IOException) { return null; } catch(UnauthorizedAccessException) { return null; }
        }
        public static void Metadata(string installed, RecognizedGame r)
        {
            try
            {
                if (Directory.Exists(installed))
                {
                    string sfo = Path.Combine(installed, "sce_sys", "param.sfo");
                    if (new FileInfo(sfo).Length <= 1024 * 1024) GameRecognition.ReadSfo(File.ReadAllBytes(sfo), r);
                    string icon = Path.Combine(installed, "sce_sys", "icon0.png");
                    if (File.Exists(icon) && new FileInfo(icon).Length < 4 * 1024 * 1024)
                        using (var original = Image.FromFile(icon)) if (original.Width <= 4096 && original.Height <= 4096) r.Icon = new Bitmap(original, new Size(128, 128));
                }
                else using (var other = GameRecognition.Inspect(installed))
                {
                    if (other.Icon != null) r.Icon = new Bitmap(other.Icon);
                    if (other.Source != "File name") { r.Title = other.Title; r.Developer = other.Developer; r.Source = other.Source; }
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
        }
        public static string ResolveArguments(EmulatorProfile e, GameEntry g, string arguments, string source)
        {
            if (!IsPackage(e, source)) return null;
            string id = g.TitleId;
            if (string.IsNullOrWhiteSpace(id) && File.Exists(source)) using (var r = GameRecognition.Inspect(source)) id = r.TitleId;
            string installed = Find(e, id, source);
            if (installed == null)
                throw new IOException("FishBowl could not locate readable installed content for title " + (id ?? "(unknown)") + ". If the game is already installed, check this emulator\'s Configuration folder and SD storage setting in FishBowl. Searched: " + string.Join("; ", Roots(e).ToArray()));
            if (IsVita(e))
            {
                if (Regex.IsMatch(arguments ?? "", @"(?:^|\s)(?:--pkg|--install|-i|--run|-r)(?:\s|=)"))
                    throw new IOException("Remove install/run flags from this game's custom arguments. FishBowl launches the installed Vita title using --run automatically.");
                return (arguments ?? "").Replace("{game}", "--run " + id) + ((arguments ?? "").Contains("{game}") ? "" : " --run " + id);
            }
            if (Regex.IsMatch(arguments ?? "", @"(?:^|\s)(?:--install|-i)(?:\s|=)"))
                throw new IOException("Remove install flags from the launch arguments. FishBowl launches the installed 3DS content directly.");
            string template = (arguments ?? "").Replace("\"{game}\"", "{game}");
            return template.Contains("{game}") ? template.Replace("{game}", "\"" + installed + "\"") : template + " \"" + installed + "\"";
        }
    }

    public static class SmoothPainting
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> enabled = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        static readonly System.Reflection.PropertyInfo buffered = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        public static void Enable(Control control)
        {
            if (!(control is Panel || control is UserControl || control is ListView)) return;
            object marker;
            if (enabled.TryGetValue(control, out marker)) return;
            buffered.SetValue(control, true, null);
            enabled.Add(control, new object());
        }
    }

    public static class ConsistentInputs
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> watched = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        public static void Watch(Control c, Action<Control> style)
        {
            object marker;
            if (watched.TryGetValue(c, out marker)) return;
            watched.Add(c, new object());
            c.ControlAdded += delegate(object sender, ControlEventArgs e) { style(e.Control); };
        }
        public static void Style(Control c)
        {
            var text = c as TextBoxBase;
            if (text != null)
            {
                text.BorderStyle = BorderStyle.FixedSingle;
                text.BackColor = FishBowlPalette.DeepSeaSurface;
                text.ForeColor = FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, text.BackColor);
                // Preserve native caret, selection, scrollbars and DPI sizing; no clipping regions.
                if (text.Region != null) { var old = text.Region; text.Region = null; old.Dispose(); }
            }
            var tabs = c as FishBowlTabs;
            if (tabs != null)
            {
                tabs.LegacyHeaders = true;
                tabs.SurfaceColor = FishBowlPalette.ThemeTop;
                tabs.HeaderTextColor = FishBowlPalette.ThemeInk;
                tabs.AccentColor = FishBowlPalette.IconAccent;
            }
            if (c is TabPage)
            {
                c.BackColor = FishBowlPalette.DeepSeaSurface;
                c.ForeColor = FishBowlPalette.ThemeInk;
                ((TabPage)c).UseVisualStyleBackColor = false;
            }
        }
    }
}
