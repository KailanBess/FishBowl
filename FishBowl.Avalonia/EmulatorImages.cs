using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace EmulatorHub
{
    // Finds an emulator's artwork: a custom image, else (Linux) the icon named by its .desktop entry or Flatpak ID,
    // looked up in the hicolor icon theme. SVG icons are rasterised once with rsvg-convert and cached.
    public static class EmulatorImages
    {
        private static Dictionary<string, DesktopEntry> desktopIndex;
        private static readonly object gate = new object();

        public static Bitmap Load(EmulatorProfile profile)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(profile.IconPath) && File.Exists(profile.IconPath)) return Decode(profile.IconPath);
                if (Platform.IsWindows || !File.Exists(profile.Executable)) return null;
                var file = IconFile(profile.Executable);
                return file == null ? null : Decode(file);
            }
            catch (Exception error) { Store.Log("Emulator image unavailable for " + profile.Name + ": " + error.Message); return null; }
        }

        private static Bitmap Decode(string file)
        {
            if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase)) file = Rasterise(file);
            if (file == null) return null;
            using (var stream = File.OpenRead(file)) return Bitmap.DecodeToWidth(stream, 96);
        }

        private static string Rasterise(string svg)
        {
            var cache = Path.Combine(Platform.CacheHome, "FishBowl", "icons"); Directory.CreateDirectory(cache);
            var info = new FileInfo(svg);
            var target = Path.Combine(cache, Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(svg))).Substring(0, 16) + "-" + info.Length + "-" + info.LastWriteTimeUtc.Ticks + ".png");
            if (File.Exists(target)) return target;
            return Platform.Run("rsvg-convert", "-w", "96", "-h", "96", "-o", target, svg) != null && File.Exists(target) ? target : null;
        }

        public static string IconFile(string executable)
        {
            string name = null;
            if (executable.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)) { var entry = DesktopEntry.Read(executable); if (entry != null) name = entry.Icon; }
            if (name == null) name = Platform.FlatpakId(executable);
            if (name == null) { var entry = EntryFor(executable); if (entry != null) name = entry.Icon; }
            if (String.IsNullOrWhiteSpace(name)) return null;
            if (Path.IsPathRooted(name)) return File.Exists(name) ? name : null;
            return ThemeIcon(name);
        }

        private static IEnumerable<string> DataDirectories()
        {
            yield return Platform.DataHome;
            foreach (var installation in Platform.FlatpakInstallations) yield return Path.Combine(installation, "exports", "share");
            var dirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            foreach (var dir in (String.IsNullOrWhiteSpace(dirs) ? "/usr/local/share:/usr/share" : dirs).Split(':')) if (dir.Length > 0) yield return dir;
        }

        // The application-menu entry that launches this program (native install, AppImage integration, etc.).
        private static DesktopEntry EntryFor(string executable)
        {
            lock (gate)
            {
                if (desktopIndex == null)
                {
                    desktopIndex = new Dictionary<string, DesktopEntry>(StringComparer.Ordinal);
                    foreach (var directory in DataDirectories().Select(d => Path.Combine(d, "applications")).Where(Directory.Exists).Distinct())
                    {
                        IEnumerable<string> files; try { files = Directory.EnumerateFiles(directory, "*.desktop").Take(2000).ToList(); } catch (Exception) { continue; }
                        foreach (var file in files)
                        {
                            var entry = DesktopEntry.Read(file); if (entry == null || String.IsNullOrWhiteSpace(entry.Icon) || entry.Command.Count == 0) continue;
                            foreach (var command in new[] { entry.Command[0], entry.TryExec }.Where(c => !String.IsNullOrWhiteSpace(c)))
                            {
                                var resolved = Platform.FindOnPath(command) ?? command;
                                if (!desktopIndex.ContainsKey(resolved)) desktopIndex[resolved] = entry;
                                var real = Platform.RealPath(resolved); if (!desktopIndex.ContainsKey(real)) desktopIndex[real] = entry;
                            }
                        }
                    }
                }
                DesktopEntry found;
                if (desktopIndex.TryGetValue(executable, out found) || desktopIndex.TryGetValue(Platform.RealPath(executable), out found)) return found;
                return null;
            }
        }

        private static readonly string[] Sizes = { "256x256", "512x512", "128x128", "96x96", "64x64", "48x48", "scalable" };
        private static string ThemeIcon(string name)
        {
            var roots = new[] { Path.Combine(Platform.Home, ".icons") }.Concat(DataDirectories().Select(d => Path.Combine(d, "icons"))).Where(Directory.Exists).Distinct().ToList();
            foreach (var size in Sizes)
                foreach (var root in roots)
                    foreach (var extension in new[] { ".png", ".svg" })
                    {
                        var file = Path.Combine(root, "hicolor", size, "apps", name + extension);
                        if (File.Exists(file)) return file;
                    }
            foreach (var directory in DataDirectories().Select(d => Path.Combine(d, "pixmaps")).Where(Directory.Exists))
                foreach (var extension in new[] { ".png", ".svg" })
                    if (File.Exists(Path.Combine(directory, name + extension))) return Path.Combine(directory, name + extension);
            return null;
        }
    }
}
