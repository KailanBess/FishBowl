using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EmulatorHub
{
    // AppImage files read without running them: the type magic and the embedded update information.
    public static class AppImageFile
    {
        // 1 or 2 for AppImages ("AI" plus the type at offset 8 of an ELF file), 0 otherwise.
        public static int ImageType(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var head = new byte[11]; if (stream.Read(head, 0, 11) != 11) return 0;
                    if (head[0] != 0x7F || head[1] != 'E' || head[2] != 'L' || head[3] != 'F' || head[8] != 'A' || head[9] != 'I') return 0;
                    return head[10] == 1 || head[10] == 2 ? head[10] : 0;
                }
            }
            catch (Exception) { return 0; }
        }
        // Type 2 keeps it in the ELF section .upd_info; type 1 in the ISO 9660 volume descriptor at offset 33651 (512 bytes).
        public static string UpdateInformation(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var type = ImageType(path); byte[] data;
                    if (type == 2) data = ElfSection(stream, ".upd_info", 64 * 1024);
                    else if (type == 1 && stream.Length > 33651 + 512) { stream.Position = 33651; data = new byte[512]; ReadExactly(stream, data); }
                    else return null;
                    if (data == null) return null;
                    int end = Array.IndexOf(data, (byte)0); var text = Encoding.UTF8.GetString(data, 0, end < 0 ? data.Length : end).Trim();
                    return text.Length == 0 ? null : text;
                }
            }
            catch (Exception) { return null; }
        }

        // Minimal ELF section-header reader (ELF64 and ELF32, either byte order): returns the named section's bytes, or null.
        public static byte[] ElfSection(Stream stream, string name, int maxBytes)
        {
            var ident = new byte[16]; stream.Position = 0; if (stream.Read(ident, 0, 16) != 16 || ident[0] != 0x7F || ident[1] != 'E' || ident[2] != 'L' || ident[3] != 'F') return null;
            bool wide = ident[4] == 2, little = ident[5] != 2; if (ident[4] != 1 && ident[4] != 2) return null;
            Func<long, int, ulong> read = (offset, size) =>
            {
                var bytes = new byte[size]; stream.Position = offset; ReadExactly(stream, bytes);
                ulong value = 0; for (int i = 0; i < size; i++) value |= (ulong)bytes[little ? i : size - 1 - i] << (8 * i); return value;
            };
            long shoff = (long)(wide ? read(0x28, 8) : read(0x20, 4));
            int entsize = (int)read(wide ? 0x3A : 0x2E, 2), count = (int)read(wide ? 0x3C : 0x30, 2), strndx = (int)read(wide ? 0x3E : 0x32, 2);
            if (shoff <= 0 || shoff >= stream.Length || entsize < (wide ? 64 : 40)) return null;
            if (count == 0) count = (int)Math.Min(read(shoff + (wide ? 0x20 : 0x14), wide ? 8 : 4), 65536); // Extended numbering keeps the count in section 0.
            if (strndx == 0xFFFF) strndx = (int)read(shoff + (wide ? 0x28 : 0x18), 4);
            if (count > 65536 || strndx >= count || shoff + (long)count * entsize > stream.Length) return null;
            Func<int, Tuple<long, long, long>> header = i =>
            {
                long at = shoff + (long)i * entsize;
                return Tuple.Create((long)read(at, 4), (long)(wide ? read(at + 0x18, 8) : read(at + 0x10, 4)), (long)(wide ? read(at + 0x20, 8) : read(at + 0x14, 4)));
            };
            var strings = header(strndx); if (strings.Item2 < 0 || strings.Item3 > 1024 * 1024 || strings.Item2 + strings.Item3 > stream.Length) return null;
            var table = new byte[strings.Item3]; stream.Position = strings.Item2; ReadExactly(stream, table);
            var wanted = Encoding.ASCII.GetBytes(name);
            for (int i = 0; i < count; i++)
            {
                var section = header(i); int at = (int)section.Item1;
                if (at < 0 || at + wanted.Length >= table.Length || table[at + wanted.Length] != 0) continue;
                bool same = true; for (int j = 0; j < wanted.Length && same; j++) same = table[at + j] == wanted[j];
                if (!same) continue;
                if (section.Item3 > maxBytes || section.Item2 < 0 || section.Item2 + section.Item3 > stream.Length) return null;
                var data = new byte[section.Item3]; stream.Position = section.Item2; ReadExactly(stream, data); return data;
            }
            return null;
        }
        static void ReadExactly(Stream stream, byte[] buffer)
        {
            int done = 0; while (done < buffer.Length) { int n = stream.Read(buffer, done, buffer.Length - done); if (n <= 0) throw new EndOfStreamException(); done += n; }
        }
    }

    // AppImage update information, e.g. "gh-releases-zsync|stenzek|duckstation|latest|DuckStation-x64.AppImage.zsync".
    public sealed class AppImageUpdateInfo
    {
        public string Raw = "", Transport = "", Owner = "", Project = "", Tag = "", Pattern = "", Url = "";
        public static AppImageUpdateInfo Parse(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            var parts = text.Trim().Split('|'); var info = new AppImageUpdateInfo { Raw = text.Trim(), Transport = parts[0] };
            if (info.Transport == "gh-releases-zsync" && parts.Length >= 5) { info.Owner = parts[1]; info.Project = parts[2]; info.Tag = parts[3]; info.Pattern = parts[4]; }
            else if (info.Transport == "zsync" && parts.Length >= 2) info.Url = parts[1];
            else if (info.Transport == "pling-v1-zsync" && parts.Length >= 3) { info.Project = parts[1]; info.Pattern = parts[2]; }
            else if (parts.Length < 2) return null;
            return info;
        }
        // owner/repository on GitHub, also for zsync files published as GitHub release assets.
        public string Repository
        {
            get
            {
                if (Transport == "gh-releases-zsync") return EmulatorUpdates.ValidRepo(Owner + "/" + Project) ? Owner + "/" + Project : null;
                var m = Regex.Match(Url, @"^https://github\.com/([^/]+)/([^/]+)/releases/");
                return m.Success && EmulatorUpdates.ValidRepo(m.Groups[1].Value + "/" + m.Groups[2].Value) ? m.Groups[1].Value + "/" + m.Groups[2].Value : null;
            }
        }
        public string Describe()
        {
            if (Transport == "gh-releases-zsync") return "GitHub releases of " + Owner + "/" + Project + " (" + (Tag == "latest" ? "latest release" : Tag == "latest-pre" ? "latest preview" : Tag == "latest-all" ? "latest release or preview" : "tag " + Tag) + ")";
            if (Transport == "zsync") return "zsync file at " + Url;
            if (Transport == "pling-v1-zsync") return "Pling/AppImageHub product " + Project;
            return Raw;
        }
    }

    public sealed class SystemPackage { public string Manager = "", Name = "", Version = ""; public bool Foreign; }

    public enum InstallKind { Other, Flatpak, AppImage, Package }
    public sealed class InstallInfo
    {
        public InstallKind Kind; public string FlatpakId, Installation, AppImage, Tool; public bool ToolIsGui;
        public AppImageUpdateInfo UpdateInfo; public SystemPackage Package;
    }

    // How an emulator gets its updates. FishBowl never downloads builds itself; it can start Flatpak or AppImageUpdate on request.
    public static class InstallUpdates
    {
        static readonly Dictionary<string, Tuple<DateTime, long, AppImageUpdateInfo>> cache = new Dictionary<string, Tuple<DateTime, long, AppImageUpdateInfo>>();
        // The AppImage behind a launcher (directly, through a link, or a .desktop entry), or null.
        public static string AppImageFor(string executable)
        {
            if (Platform.IsWindows || String.IsNullOrWhiteSpace(executable) || !File.Exists(executable) || Platform.FlatpakId(executable) != null) return null;
            var target = executable.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase) ? Platform.DesktopProgram(executable) : executable;
            if (target == null) return null; target = Platform.RealPath(target);
            return AppImageFile.ImageType(target) > 0 ? target : null;
        }
        public static AppImageUpdateInfo UpdateInfoFor(string executable)
        {
            var file = AppImageFor(executable); if (file == null) return null;
            var stamp = File.GetLastWriteTimeUtc(file); var length = new FileInfo(file).Length;
            lock (cache)
            {
                Tuple<DateTime, long, AppImageUpdateInfo> hit;
                if (cache.TryGetValue(file, out hit) && hit.Item1 == stamp && hit.Item2 == length) return hit.Item3;
                var info = AppImageUpdateInfo.Parse(AppImageFile.UpdateInformation(file));
                cache[file] = Tuple.Create(stamp, length, info); return info;
            }
        }
        // Feeds EmulatorUpdates.Repository when no manual override is set.
        public static string RepositoryFor(string executable) { try { var info = UpdateInfoFor(executable); return info == null ? null : info.Repository; } catch (Exception) { return null; } }

        public static SystemPackage PackageFor(string executable)
        {
            if (Platform.IsWindows || Platform.FlatpakId(executable) != null) return null;
            var target = executable.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase) ? Platform.DesktopProgram(executable) : executable;
            if (target == null || AppImageFile.ImageType(target) > 0) return null;
            target = Platform.RealPath(target);
            var pacman = Platform.Run("pacman", "-Qo", target);
            var owned = Regex.Match(pacman ?? "", @" is owned by (\S+) (\S+)");
            if (owned.Success) return new SystemPackage { Manager = "pacman", Name = owned.Groups[1].Value, Version = owned.Groups[2].Value, Foreign = Platform.Run("pacman", "-Qmq", owned.Groups[1].Value) != null };
            var dpkg = Platform.Run("dpkg", "-S", target);
            if (dpkg != null && dpkg.Contains(":"))
            {
                var name = dpkg.Split(':')[0].Trim(); var version = Platform.Run("dpkg-query", "-W", "-f=${Version}", name);
                if (!String.IsNullOrWhiteSpace(version)) return new SystemPackage { Manager = "dpkg", Name = name, Version = version.Trim() };
            }
            var rpm = Platform.Run("rpm", "-qf", "--qf", "%{NAME} %{VERSION}-%{RELEASE}", target);
            if (!String.IsNullOrWhiteSpace(rpm) && rpm.Trim().Split(' ').Length == 2) return new SystemPackage { Manager = "rpm", Name = rpm.Trim().Split(' ')[0], Version = rpm.Trim().Split(' ')[1] };
            return null;
        }

        // appimageupdatetool on PATH or as an AppImage in the usual folders; AppImageUpdate (its window) as a fallback.
        public static string FindTool(out bool gui)
        {
            gui = false;
            foreach (var name in new[] { "appimageupdatetool", "appimageupdatetool.AppImage", "appimageupdatetool-x86_64.AppImage", "appimageupdatetool-aarch64.AppImage" })
            { var found = Platform.FindOnPath(name); if (found != null && Platform.IsExecutableFile(found)) return found; }
            var folders = new[] { "Applications", "AppImages", ".local/bin", "bin", "Downloads" }.Select(f => Path.Combine(Platform.Home, f)).Where(Directory.Exists).ToList();
            foreach (var prefix in new[] { "appimageupdatetool", "AppImageUpdate" })
                foreach (var folder in folders)
                {
                    var file = Directory.EnumerateFiles(folder, prefix + "*").FirstOrDefault(f => f.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal));
                    if (file != null) { gui = prefix == "AppImageUpdate"; return file; }
                }
            var window = Platform.FindOnPath("AppImageUpdate"); gui = window != null; return window;
        }

        public static InstallInfo Inspect(EmulatorProfile profile)
        {
            var info = new InstallInfo(); if (Platform.IsWindows || profile == null) return info;
            info.FlatpakId = Platform.FlatpakId(profile.Executable);
            if (info.FlatpakId != null) { info.Kind = InstallKind.Flatpak; info.Installation = FlatpakTool.Installation(profile.Executable, info.FlatpakId); return info; }
            info.AppImage = AppImageFor(profile.Executable);
            if (info.AppImage != null) { info.Kind = InstallKind.AppImage; info.UpdateInfo = UpdateInfoFor(profile.Executable); bool gui; info.Tool = FindTool(out gui); info.ToolIsGui = gui; return info; }
            info.Package = PackageFor(profile.Executable); if (info.Package != null) info.Kind = InstallKind.Package;
            return info;
        }

        // `flatpak remote-ls --updates` prints tab-separated rows without a header; the version column can be empty.
        public static bool ParseRemoteLs(string output, string id, out string version)
        {
            version = null;
            foreach (var line in (output ?? "").Split('\n'))
            {
                var cells = line.TrimEnd('\r').Split('\t'); if (cells[0].Trim() != id) continue;
                version = cells.Length > 1 && cells[1].Trim().Length > 0 ? cells[1].Trim() : null; return true;
            }
            return false;
        }
        public static string FlatpakVersion(string id, string installation)
        {
            var result = FlatpakTool.Run("flatpak", new[] { "info", "--" + installation, id }, 8000);
            var line = result.Output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("Version:", StringComparison.Ordinal));
            return line == null ? null : line.Substring(8).Trim();
        }
        public static string[] FlatpakUpdateArguments(string id, string installation) { return new[] { "update", "--" + installation, "-y", "--noninteractive", id }; }
        public static string[] FlatpakCheckArguments(string installation) { return new[] { "remote-ls", "--" + installation, "--updates", "--app", "--columns=application,version" }; }
        // True when Flatpak lists an update for the app; version is the remote version when the app publishes one.
        public static bool FlatpakUpdateAvailable(string id, string installation, out string version, out string error)
        {
            version = null; var result = FlatpakTool.Run("flatpak", FlatpakCheckArguments(installation), 60000);
            error = result.Ok ? null : result.Message;
            return result.Ok && ParseRemoteLs(result.Output, id, out version);
        }

        // appimageupdatetool's last line names the result: "Update successful. New file created: <path>".
        public static string NewFileFromLog(string log)
        {
            var m = Regex.Matches(log ?? "", @"(?:New file created|Updated existing file): (.+)").Cast<Match>().LastOrDefault();
            return m == null ? null : m.Groups[1].Value.Trim();
        }
        // For the AppImageUpdate window: the newest AppImage that appeared or changed in the folder.
        public static Dictionary<string, DateTime> Snapshot(string folder)
        {
            return Directory.EnumerateFiles(folder).ToDictionary(f => f, File.GetLastWriteTimeUtc);
        }
        public static string ChangedAppImage(Dictionary<string, DateTime> before, string folder)
        {
            return Directory.EnumerateFiles(folder).Where(f => f.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) && (!before.ContainsKey(f) || before[f] != File.GetLastWriteTimeUtc(f)))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
    }

    // Runs a program and hands its output to the caller as it arrives (stdout and stderr together).
    public static class ProcessStream
    {
        public static int Run(string program, IEnumerable<string> arguments, Action<string> output, System.Threading.CancellationToken token)
        {
            var info = new System.Diagnostics.ProcessStartInfo { FileName = program, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var a in arguments) info.ArgumentList.Add(a);
            info.Environment["LC_ALL"] = "C";
            using (var process = System.Diagnostics.Process.Start(info))
            {
                Action<System.IO.StreamReader> pump = reader =>
                {
                    var buffer = new char[1024]; int n;
                    while ((n = reader.Read(buffer, 0, buffer.Length)) > 0) output(new string(buffer, 0, n));
                };
                var a = System.Threading.Tasks.Task.Run(() => pump(process.StandardOutput)); var b = System.Threading.Tasks.Task.Run(() => pump(process.StandardError));
                using (token.Register(() => { try { process.Kill(true); } catch (Exception) { } }))
                {
                    process.WaitForExit(); System.Threading.Tasks.Task.WaitAll(a, b);
                }
                token.ThrowIfCancellationRequested();
                return process.ExitCode;
            }
        }
    }

    // Turns raw program output into readable lines: drops ANSI escapes and lets '\r' overwrite the current line, so
    // progress counters ("42% done") update in place instead of piling up.
    public sealed class TerminalLog
    {
        private readonly List<string> lines = new List<string> { "" }; private bool returned;
        public void Append(string chunk)
        {
            chunk = Regex.Replace(chunk ?? "", @"\x1B\[[0-9;?]*[A-Za-z]", "");
            foreach (var c in chunk)
            {
                if (c == '\n') { lines.Add(""); returned = false; }
                else if (c == '\r') returned = true;
                else { if (returned) { lines[lines.Count - 1] = ""; returned = false; } lines[lines.Count - 1] += c; }
            }
            if (lines.Count > 2000) lines.RemoveRange(0, lines.Count - 2000);
        }
        public override string ToString() { return String.Join("\n", lines).TrimEnd('\n'); }
    }
}
