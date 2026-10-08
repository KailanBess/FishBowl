using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace EmulatorHub
{
    // Flatpak sandbox permissions as FishBowl reads them: the [Context] keys printed by `flatpak info --show-permissions`
    // and stored in override files. Linux only; nothing here changes a permission by itself.
    public enum SandboxAccess { None, ReadOnly, ReadWrite }

    // One filesystems= entry, e.g. "~/Games:ro", "!xdg-download/sub", "/mnt/a\:b:create".
    public sealed class FlatpakFilesystem
    {
        public string Name = ""; public SandboxAccess Mode = SandboxAccess.ReadWrite; public bool Create, Negated;
        public static FlatpakFilesystem Parse(string entry)
        {
            var fs = new FlatpakFilesystem(); var text = entry ?? "";
            if (text.StartsWith("!", StringComparison.Ordinal)) { fs.Negated = true; fs.Mode = SandboxAccess.None; text = text.Substring(1); }
            var name = new StringBuilder(); int i = 0;
            for (; i < text.Length && text[i] != ':'; i++) { if (text[i] == '\\' && i + 1 < text.Length) i++; name.Append(text[i]); }
            fs.Name = name.ToString();
            if (i < text.Length && !fs.Negated)
                switch (text.Substring(i + 1)) { case "ro": fs.Mode = SandboxAccess.ReadOnly; break; case "create": fs.Create = true; break; }
            if (fs.Name == "~") fs.Name = "home";
            return fs;
        }
        // Flatpak's own escaping for a location: a literal ':' or '\' would otherwise read as a suffix.
        public static string Escape(string name) { return (name ?? "").Replace("\\", "\\\\").Replace(":", "\\:"); }
        public string Entry { get { return (Negated ? "!" : "") + Escape(Name) + (Negated ? "" : Mode == SandboxAccess.ReadOnly ? ":ro" : Create ? ":create" : ""); } }
        public override string ToString() { return Entry; }
    }

    // Host folders a Flatpak resolves its locations against; Exists is replaceable so decisions can be tested with fixtures.
    public sealed class FlatpakPaths
    {
        public string Home = "", ConfigHome = "", DataHome = "", CacheHome = "", RuntimeDir = "";
        public Dictionary<string, string> UserDirs = new Dictionary<string, string>();
        public Func<string, bool> Exists = p => Directory.Exists(p) || File.Exists(p);
        public static FlatpakPaths Current()
        {
            var paths = new FlatpakPaths { Home = Platform.Home, ConfigHome = Platform.ConfigHome, DataHome = Platform.DataHome, CacheHome = Platform.CacheHome, RuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "" };
            try { var file = Path.Combine(paths.ConfigHome, "user-dirs.dirs"); if (File.Exists(file)) paths.UserDirs = ReadUserDirs(File.ReadAllText(file), paths.Home); } catch (Exception) { }
            return paths;
        }
        // ~/.config/user-dirs.dirs: XDG_DOWNLOAD_DIR="$HOME/Downloads". Desktop defaults to ~/Desktop like GLib; others stay unset.
        public static Dictionary<string, string> ReadUserDirs(string text, string home)
        {
            var dirs = new Dictionary<string, string> { { "XDG_DESKTOP_DIR", Path.Combine(home, "Desktop") } };
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim(); int eq = line.IndexOf('='); if (line.StartsWith("#") || eq < 1) continue;
                var value = line.Substring(eq + 1).Trim().Trim('"');
                if (value.StartsWith("$HOME", StringComparison.Ordinal)) value = home + value.Substring(5); else if (!value.StartsWith("/")) continue;
                dirs[line.Substring(0, eq).Trim()] = value.TrimEnd('/');
            }
            return dirs;
        }
        static readonly Dictionary<string, string> userDirKeys = new Dictionary<string, string> {
            { "xdg-desktop", "XDG_DESKTOP_DIR" }, { "xdg-documents", "XDG_DOCUMENTS_DIR" }, { "xdg-download", "XDG_DOWNLOAD_DIR" }, { "xdg-music", "XDG_MUSIC_DIR" },
            { "xdg-pictures", "XDG_PICTURES_DIR" }, { "xdg-public-share", "XDG_PUBLICSHARE_DIR" }, { "xdg-templates", "XDG_TEMPLATES_DIR" }, { "xdg-videos", "XDG_VIDEOS_DIR" } };
        // The host folder behind a location name, or null for special names and unconfigured or disabled XDG folders.
        public string Resolve(string name)
        {
            if (name.StartsWith("~/", StringComparison.Ordinal)) return Join(Home, name.Substring(2));
            if (name.StartsWith("/", StringComparison.Ordinal)) return Normalize(name);
            if (!name.StartsWith("xdg-", StringComparison.Ordinal)) return null;
            int slash = name.IndexOf('/'); var prefix = slash < 0 ? name : name.Substring(0, slash); var rest = slash < 0 ? "" : name.Substring(slash + 1).Trim('/');
            string dir, key;
            if (prefix == "xdg-config") dir = ConfigHome; else if (prefix == "xdg-data") dir = DataHome; else if (prefix == "xdg-cache") dir = CacheHome;
            else if (prefix == "xdg-run") dir = rest.Length == 0 ? null : RuntimeDir;
            else if (userDirKeys.TryGetValue(prefix, out key)) { UserDirs.TryGetValue(key, out dir); if (dir != null && Normalize(dir) == Normalize(Home)) dir = null; }
            else dir = null;
            return String.IsNullOrEmpty(dir) ? null : Join(dir, rest);
        }
        public static string Normalize(string path) { var full = Path.GetFullPath(path); return full.Length > 1 ? full.TrimEnd('/') : full; }
        static string Join(string a, string b) { return Normalize(b.Length == 0 ? a : Path.Combine(a, b)); }
    }

    public sealed class FlatpakAccessDecision
    {
        public SandboxAccess Mode; public string Via = ""; public bool Sandbox, Reserved;
    }

    public sealed class FlatpakPermissions
    {
        public readonly Dictionary<string, List<string>> Context = new Dictionary<string, List<string>>();
        public List<string> Values(string key) { List<string> list; return Context.TryGetValue(key, out list) ? list : new List<string>(); }
        public List<FlatpakFilesystem> Filesystems { get { return Values("filesystems").Select(FlatpakFilesystem.Parse).ToList(); } }
        public List<string> Devices { get { return Values("devices"); } }

        // Parses a GLib key file and keeps the [Context] lists.
        public static FlatpakPermissions Parse(string keyFile)
        {
            var result = new FlatpakPermissions(); bool context = false;
            foreach (var raw in (keyFile ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r'); if (line.TrimStart().StartsWith("#")) continue;
                if (line.StartsWith("[")) { context = line.Trim() == "[Context]"; continue; }
                int eq = line.IndexOf('='); if (!context || eq < 1) continue;
                result.Context[line.Substring(0, eq).Trim()] = SplitList(line.Substring(eq + 1));
            }
            return result;
        }
        // GKeyFile string lists: ';' separated, with \; \\ \s \n \t \r escapes.
        public static List<string> SplitList(string value)
        {
            var items = new List<string>(); var current = new StringBuilder(); value = (value ?? "").TrimStart(' ');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char n = value[++i];
                    current.Append(n == 's' ? ' ' : n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                }
                else if (c == ';') { items.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            if (current.Length > 0) items.Add(current.ToString());
            return items;
        }
        public static string JoinList(IEnumerable<string> items)
        {
            var text = new StringBuilder();
            foreach (var item in items)
            {
                for (int i = 0; i < item.Length; i++)
                {
                    char c = item[i];
                    text.Append(c == '\\' ? "\\\\" : c == ';' ? "\\;" : c == '\n' ? "\\n" : c == '\t' ? "\\t" : c == '\r' ? "\\r" : c == ' ' && i == 0 ? "\\s" : c.ToString());
                }
                text.Append(';');
            }
            return text.ToString();
        }

        public static readonly Version InputDeviceVersion = new Version(1, 15, 6), ConditionalVersion = new Version(1, 17, 0);
        // Device permissions, including the conditional "if:name:condition" form Flatpak 1.17+ writes next to a plain fallback entry.
        public bool DeviceAllowed(string name, Version flatpak)
        {
            if (name == "input" && flatpak != null && flatpak < InputDeviceVersion) return false;
            bool plain = false; var conditions = new List<string>();
            foreach (var entry in Devices)
            {
                if (entry.StartsWith("if:", StringComparison.Ordinal)) { var parts = entry.Split(new[] { ':' }, 3); if (parts.Length == 3 && parts[1] == name) conditions.Add(parts[2]); }
                else if (entry == name) plain = true; else if (entry == "!" + name) plain = false;
            }
            bool conditional = flatpak == null || flatpak >= ConditionalVersion;
            if (!conditional || conditions.Count == 0) return plain;
            foreach (var condition in conditions)
            {
                bool negated = condition.StartsWith("!"); var test = negated ? condition.Substring(1) : condition; bool value;
                if (test == "true" || test == "has-usb-device") value = true;
                else if (test == "false") value = false;
                else if (test == "has-input-device") value = flatpak == null || flatpak >= InputDeviceVersion;
                else continue; // Unknown or session-dependent conditions cannot be confirmed here.
                if (value != negated) return true;
            }
            return false;
        }
        // "all", "input", or null when the app cannot open controllers.
        public string ControllerAccess(Version flatpak) { return DeviceAllowed("all", flatpak) ? "all" : DeviceAllowed("input", flatpak) ? "input" : null; }

        static readonly string[] reserved = { "/.flatpak-info", "/app", "/dev", "/etc", "/proc", "/run/flatpak", "/run/host", "/usr", "/bin", "/lib", "/lib32", "/lib64", "/sbin" };
        static readonly string[] notInHost = { "app", "bin", "boot", "dev", "efi", "etc", "lib", "lib32", "lib64", "proc", "root", "run", "sbin", "sys", "tmp", "usr", "var" };
        static bool Under(string path, string root) { return path == root || (root == "/" ? path.StartsWith("/") : path.StartsWith(root + "/", StringComparison.Ordinal)); }

        // Mirrors Flatpak's exports: host shares most top-level folders, home shares $HOME, each location shares (or hides, when
        // negated) its own folder, ~/.var/app is hidden except the app's own folder, and the most specific location wins.
        // `flatpak info --file-access` stays the final word because show-permissions drops negations; this explains the result.
        public FlatpakAccessDecision Access(string path, string appId, FlatpakPaths paths)
        {
            path = FlatpakPaths.Normalize(path); var home = FlatpakPaths.Normalize(paths.Home);
            var own = Path.Combine(home, ".var", "app", appId);
            if (Under(path, own)) return new FlatpakAccessDecision { Mode = SandboxAccess.ReadWrite, Sandbox = true, Via = "the app's own folder" };
            var blocked = reserved.FirstOrDefault(r => Under(path, r));
            if (blocked != null)
                return new FlatpakAccessDecision { Reserved = true, Via = blocked + " is reserved by Flatpak" + (Filesystems.Any(f => !f.Negated && (f.Name == "host-os" || f.Name == "host-etc" || f.Name == "host")) ? "; host-os and host-etc show it under /run/host instead" : "") };
            var rules = new List<Tuple<string, SandboxAccess, string>>();
            var all = Filesystems; var host = all.FirstOrDefault(f => f.Name == "host" && !f.Negated);
            var hostMode = host == null ? SandboxAccess.None : host.Mode;
            if (host != null)
            {
                var first = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (first != null && !notInHost.Contains(first)) rules.Add(Tuple.Create("/" + first, hostMode, "host"));
                rules.Add(Tuple.Create("/run/media", hostMode, "host"));
            }
            var homeEntry = all.FirstOrDefault(f => f.Name == "home" && !f.Negated);
            if (homeEntry != null) rules.Add(Tuple.Create(home, (SandboxAccess)Math.Max((int)homeEntry.Mode, (int)hostMode), "home"));
            foreach (var fs in all)
            {
                var target = paths.Resolve(fs.Name);
                if (target == null || (!fs.Create && !paths.Exists(target))) continue;
                rules.Add(Tuple.Create(target, fs.Mode, (fs.Negated ? "!" : "") + fs.Name + (fs.Mode == SandboxAccess.ReadOnly ? ":ro" : fs.Create ? ":create" : "")));
            }
            rules.Add(Tuple.Create(Path.Combine(home, ".var", "app"), SandboxAccess.None, "~/.var/app (other apps' folders)"));
            var match = rules.Where(r => Under(path, r.Item1)).OrderByDescending(r => r.Item1.Length).ThenByDescending(r => r.Item2).FirstOrDefault();
            if (match == null) return new FlatpakAccessDecision { Via = "no permission covers it" };
            if (match.Item2 == SandboxAccess.None) return new FlatpakAccessDecision { Via = "hidden by " + match.Item3 };
            return new FlatpakAccessDecision { Mode = match.Item2, Via = match.Item3 };
        }
    }

    public sealed class ToolResult
    {
        public int ExitCode = -1; public string Output = "", Error = ""; public bool Missing, TimedOut;
        public bool Ok { get { return ExitCode == 0 && !Missing && !TimedOut; } }
        public string Message { get { return Missing ? "The program is not installed." : TimedOut ? "It did not answer in time." : (Error.Trim().Length > 0 ? Error.Trim() : Output.Trim()); } }
    }

    // Runs the flatpak command line. Read-only queries plus the `flatpak override --user` commands the user confirms.
    public static class FlatpakTool
    {
        public static ToolResult Run(string program, IEnumerable<string> arguments, int timeoutMs = 15000)
        {
            var result = new ToolResult();
            try
            {
                var info = new ProcessStartInfo { FileName = program, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                foreach (var a in arguments) info.ArgumentList.Add(a);
                info.Environment["LC_ALL"] = "C";
                using (var process = Process.Start(info))
                {
                    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs)) { try { process.Kill(true); } catch (Exception) { } result.TimedOut = true; return result; }
                    process.WaitForExit(); result.ExitCode = process.ExitCode; result.Output = output.Result; result.Error = error.Result;
                }
            }
            catch (System.ComponentModel.Win32Exception) { result.Missing = true; }
            return result;
        }

        static Version version; static DateTime versionAt;
        public static Version InstalledVersion()
        {
            if (version != null && DateTime.UtcNow - versionAt < TimeSpan.FromMinutes(5)) return version;
            var result = Run("flatpak", new[] { "--version" }, 5000);
            version = result.Ok ? ParseVersion(result.Output) : null; versionAt = DateTime.UtcNow; return version;
        }
        public static Version ParseVersion(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d+)\.(\d+)(?:\.(\d+))?");
            return m.Success ? new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0) : null;
        }

        public static string UserDirectory
        {
            get { var dir = Environment.GetEnvironmentVariable("FLATPAK_USER_DIR"); return !String.IsNullOrWhiteSpace(dir) ? dir : Path.Combine(Platform.DataHome, "flatpak"); }
        }
        public static string SystemDirectory
        {
            get { var dir = Environment.GetEnvironmentVariable("FLATPAK_SYSTEM_DIR"); return !String.IsNullOrWhiteSpace(dir) ? dir : "/var/lib/flatpak"; }
        }
        // "user" or "system": the exports folder a launcher sits in, else where the app is deployed (flatpak run prefers the user one).
        public static string Installation(string executable, string id)
        {
            var full = Path.GetFullPath(executable ?? "");
            if (full.StartsWith(UserDirectory.TrimEnd('/') + "/exports/", StringComparison.Ordinal)) return "user";
            if (full.StartsWith(SystemDirectory.TrimEnd('/') + "/exports/", StringComparison.Ordinal)) return "system";
            if (Directory.Exists(Path.Combine(UserDirectory, "app", id))) return "user";
            return "system";
        }
        static string[] With(string installation, params string[] args)
        {
            var list = new List<string> { args[0] }; if (installation != null) list.Add("--" + installation);
            list.AddRange(args.Skip(1)); return list.ToArray();
        }

        public static FlatpakPermissions Permissions(string id, string installation, out string error)
        {
            var result = Run("flatpak", With(installation, "info", "--show-permissions", id));
            error = result.Ok ? null : result.Message;
            return result.Ok ? FlatpakPermissions.Parse(result.Output) : null;
        }
        // Flatpak's own verdict for a path: read-write, read-only or hidden. Null when it cannot be asked.
        public static SandboxAccess? FileAccess(string id, string installation, string path)
        {
            var result = Run("flatpak", With(installation, "info", "--file-access=" + path, id));
            if (!result.Ok) return null;
            var answer = result.Output.Trim();
            return answer == "read-write" ? SandboxAccess.ReadWrite : answer == "read-only" ? SandboxAccess.ReadOnly : answer == "hidden" ? SandboxAccess.None : (SandboxAccess?)null;
        }
        public static string UserOverrides(string id)
        {
            var result = Run("flatpak", new[] { "override", "--user", "--show", id });
            return result.Ok ? result.Output : null;
        }
        public static string OverrideFile(string id) { return Path.Combine(UserDirectory, "overrides", id); }

        // The filesystem entry FishBowl grants for a folder: ~/relative inside home, the escaped path otherwise.
        public static string GrantEntry(string folder, SandboxAccess need, bool create, string home)
        {
            var path = FlatpakPaths.Normalize(folder); home = FlatpakPaths.Normalize(home);
            var name = path.StartsWith(home + "/", StringComparison.Ordinal) ? "~/" + path.Substring(home.Length + 1) : path;
            return FlatpakFilesystem.Escape(name) + (need == SandboxAccess.ReadOnly ? ":ro" : create ? ":create" : "");
        }
        public static string[] OverrideArguments(string option, string id) { return new[] { "override", "--user", option, id }; }
        public static string CommandLine(string program, IEnumerable<string> arguments) { return String.Join(" ", new[] { program }.Concat(arguments).Select(Quote)); }
        public static string Quote(string argument)
        {
            if (argument.Length > 0 && argument.All(c => Char.IsLetterOrDigit(c) || "-_./=:~+,@%".IndexOf(c) >= 0)) return argument;
            return "'" + argument.Replace("'", "'\\''") + "'";
        }

        // Removes one entry (matched by location or device name, any mode) from a list key in an override file's [Context]
        // group and returns the new text; other lines stay byte-for-byte. Null when the entry is not there.
        public static string RemoveOverrideEntry(string text, string key, string entry)
        {
            var lines = (text ?? "").Split('\n').ToList(); bool context = false;
            Func<string, string> identity = e => key == "filesystems" ? FlatpakFilesystem.Parse(e).Name : e.TrimStart('!');
            var target = identity(entry);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.StartsWith("[")) { context = line.Trim() == "[Context]"; continue; }
                int eq = line.IndexOf('='); if (!context || eq < 1 || line.Substring(0, eq).Trim() != key) continue;
                var items = FlatpakPermissions.SplitList(line.Substring(eq + 1));
                var kept = items.Where(e => identity(e) != target || e.StartsWith("if:")).ToList();
                if (kept.Count == items.Count) return null;
                if (kept.Count == 0) lines.RemoveAt(i); else lines[i] = key + "=" + FlatpakPermissions.JoinList(kept);
                return String.Join("\n", lines);
            }
            return null;
        }
    }
}
