using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EmulatorHub
{
    // Operating-system specific behaviour shared by the Windows (WinForms) and cross-platform (Avalonia) front ends.
    // Keep this file C# 5 compatible: the Windows build compiles it with the .NET Framework csc.exe.
    // Linux-only code sits behind NETCOREAPP, which csc.exe never defines.
    public static class Platform
    {
        public static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }
        public static bool IsMac { get { return !IsWindows && Directory.Exists("/System/Library/CoreServices"); } }
        public static string OsName { get { return IsWindows ? "Windows" : IsMac ? "macOS" : "Linux"; } }
        public static StringComparison PathComparison { get { return IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; } }
        public static StringComparer PathComparer { get { return IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal; } }

        public static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }
        public static string ConfigHome { get { return Xdg("XDG_CONFIG_HOME", ".config"); } }
        public static string DataHome { get { return Xdg("XDG_DATA_HOME", Path.Combine(".local", "share")); } }
        public static string CacheHome { get { return Xdg("XDG_CACHE_HOME", ".cache"); } }
        private static string Xdg(string variable, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            return !String.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) ? value : Path.Combine(Home, fallback);
        }
        public static string Documents
        {
            get
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (IsWindows || (!String.IsNullOrWhiteSpace(documents) && !String.Equals(documents.TrimEnd('/'), Home.TrimEnd('/'), StringComparison.Ordinal))) return documents;
                return Path.Combine(Home, "Documents");
            }
        }

        // ----- Launch files -------------------------------------------------------------------------------------------

        private static readonly string[] WindowsLaunchExtensions = { ".exe", ".bat", ".cmd", ".lnk" };
        private static readonly string[] UnixLaunchExtensions = { ".appimage", ".sh", ".desktop" };
        private static readonly string[] UnixNonPrograms = { ".so", ".a", ".o", ".dll", ".exe", ".txt", ".md", ".json", ".ini", ".cfg", ".png", ".jpg", ".svg", ".py", ".pyc", ".zip", ".7z" };

        public static bool IsLaunchFile(string path)
        {
            var extension = Path.GetExtension(path ?? "");
            if (IsWindows) return WindowsLaunchExtensions.Any(e => String.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
            if (UnixLaunchExtensions.Any(e => String.Equals(e, extension, StringComparison.OrdinalIgnoreCase))) return true;
            if (UnixNonPrograms.Any(e => String.Equals(e, extension, StringComparison.OrdinalIgnoreCase)) || (path ?? "").Contains(".so.")) return false;
            return IsExecutableFile(path);
        }
        public static string LaunchFileDescription
        {
            get { return IsWindows ? ".exe, .bat, .cmd and .lnk launchers are supported." : "Programs, AppImages, Flatpak launchers, .sh scripts and .desktop entries are supported."; }
        }
        public static string LaunchFileKinds { get { return IsWindows ? ".exe, .bat, .cmd, or .lnk" : "program, AppImage, Flatpak launcher, .sh script, or .desktop entry"; } }
        // File dialog filter as "Label|*.a;*.b". Linux programs rarely have an extension, so "All files" leads there.
        public static string ProgramFilter
        {
            get { return IsWindows ? "Windows launchers|*.exe;*.bat;*.cmd;*.lnk" : "All files|*|AppImages and launchers|*.AppImage;*.appimage;*.sh;*.desktop"; }
        }
        public static string ScriptFilter
        {
            get { return IsWindows ? "Programs and scripts|*.exe;*.bat;*.cmd" : "All files|*|Scripts|*.sh"; }
        }
        // True when storage can be inferred from the program itself (not a script or shortcut that may redirect it).
        public static bool IsDirectProgram(string path)
        {
            var extension = Path.GetExtension(path ?? "");
            if (IsWindows) return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
            if (extension.Equals(".sh", StringComparison.OrdinalIgnoreCase)) return false;
            if (extension.Equals(".desktop", StringComparison.OrdinalIgnoreCase)) return FlatpakId(path) != null || DesktopProgram(path) != null;
            return true;
        }

        public static bool IsExecutableFile(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            if (IsWindows) return true;
#if NETCOREAPP
            try { return (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0; }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        public static void MakeExecutable(string path)
        {
#if NETCOREAPP
            if (IsWindows || IsExecutableFile(path)) return;
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | ((mode & UnixFileMode.GroupRead) != 0 ? UnixFileMode.GroupExecute : 0) | ((mode & UnixFileMode.OtherRead) != 0 ? UnixFileMode.OtherExecute : 0));
            Store.Log("Marked as executable: " + path);
#endif
        }

        // Applies Unix permission bits recorded in a ZIP entry (setuid/setgid/sticky are never applied).
        public static void ApplyArchiveMode(string path, int externalAttributes)
        {
#if NETCOREAPP
            if (IsWindows) return;
            int mode = (externalAttributes >> 16) & 0x1FF;
            if (mode == 0) return;
            File.SetUnixFileMode(path, (UnixFileMode)(mode | 0x180));
#endif
        }
        public static bool ArchiveEntryIsExecutable(string name, int externalAttributes)
        {
            if (IsWindows) return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            if (name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)) return true;
            return ((externalAttributes >> 16) & 0xF000) == 0x8000 && ((externalAttributes >> 16) & 0x49) != 0;
        }

        public static ProcessStartInfo StartInfo(string program, string arguments)
        {
            var directory = Path.GetDirectoryName(program);
            if (IsWindows) return new ProcessStartInfo { FileName = program, Arguments = arguments ?? "", WorkingDirectory = directory, UseShellExecute = true };
            var extension = Path.GetExtension(program);
            if (extension.Equals(".desktop", StringComparison.OrdinalIgnoreCase))
            {
                var entry = DesktopEntry.Read(program);
                if (entry == null || entry.Command.Count == 0) throw new IOException("This .desktop entry has no command to run.");
                var info = new ProcessStartInfo { FileName = entry.Command[0], UseShellExecute = false, WorkingDirectory = Directory.Exists(entry.WorkingDirectory) ? entry.WorkingDirectory : Home };
                AddArguments(info, entry.Command.Skip(1)); AddArguments(info, SplitArguments(arguments)); return info;
            }
            var start = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = directory };
            if (extension.Equals(".sh", StringComparison.OrdinalIgnoreCase) && !IsExecutableFile(program)) { start.FileName = "/bin/sh"; AddArguments(start, new[] { program }); }
            else { MakeExecutable(program); start.FileName = program; }
            AddArguments(start, SplitArguments(arguments)); return start;
        }
        private static void AddArguments(ProcessStartInfo info, IEnumerable<string> values)
        {
#if NETCOREAPP
            foreach (var value in values) info.ArgumentList.Add(value);
#else
            info.Arguments = String.Join(" ", new[] { info.Arguments }.Concat(values.Select(v => "\"" + v.Replace("\"", "\\\"") + "\"")).Where(v => !String.IsNullOrEmpty(v)).ToArray());
#endif
        }
        // Splits a user-entered argument string with shell-like quoting ("..." and '...').
        public static List<string> SplitArguments(string text)
        {
            var result = new List<string>(); if (String.IsNullOrWhiteSpace(text)) return result;
            var current = new System.Text.StringBuilder(); bool any = false; char quote = '\0';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; else if (c == '\\' && quote == '"' && i + 1 < text.Length && (text[i + 1] == '"' || text[i + 1] == '\\')) current.Append(text[++i]); else current.Append(c); continue; }
                if (c == '"' || c == '\'') { quote = c; any = true; continue; }
                if (Char.IsWhiteSpace(c)) { if (any || current.Length > 0) { result.Add(current.ToString()); current.Clear(); any = false; } continue; }
                if (c == '\\' && i + 1 < text.Length) { current.Append(text[++i]); continue; }
                current.Append(c);
            }
            if (any || current.Length > 0) result.Add(current.ToString());
            return result;
        }

        public static void Launch(string program, string arguments)
        {
            using (Process.Start(StartInfo(program, arguments))) { }
        }

        // Opens a folder, document or web address with the desktop's default application.
        public static void Open(string target)
        {
            if (IsWindows) { using (Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })) { } return; }
            var opener = IsMac ? "open" : "xdg-open";
            var info = new ProcessStartInfo { FileName = opener, UseShellExecute = false };
            AddArguments(info, new[] { target });
            try { using (Process.Start(info)) { } }
            catch (System.ComponentModel.Win32Exception) { throw new IOException("No desktop opener (" + opener + ") is available to open " + target); }
        }
        public static void OpenTextFile(string path)
        {
            if (IsWindows) { using (Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true })) { } return; }
            Open(path);
        }

        public static string ControllerSettingsLabel { get { return IsWindows ? "Windows controllers" : null; } }
        public static void OpenControllerSettings()
        {
            if (IsWindows) using (Process.Start(new ProcessStartInfo("control.exe", "joy.cpl") { UseShellExecute = true })) { }
        }

        // ----- Flatpak and .desktop helpers --------------------------------------------------------------------------

        public static IEnumerable<string> FlatpakInstallations
        {
            get { return IsWindows ? new string[0] : new[] { Path.Combine(DataHome, "flatpak"), "/var/lib/flatpak" }; }
        }
        // The Flatpak application ID behind a launcher: an exports/bin wrapper named after the ID, or a .desktop entry running "flatpak run".
        public static string FlatpakId(string path)
        {
            if (IsWindows || String.IsNullOrWhiteSpace(path)) return null;
            var name = Path.GetFileName(path);
            var directory = (Path.GetDirectoryName(path) ?? "").TrimEnd('/');
            if (directory.EndsWith("/exports/bin", StringComparison.Ordinal) && name.Count(c => c == '.') >= 2) return name;
            if (!name.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)) return null;
            var entry = DesktopEntry.Read(path);
            if (entry == null) return null;
            if (entry.Command.Count > 1 && Path.GetFileName(entry.Command[0]) == "flatpak" && entry.Command[1] == "run")
                return entry.Command.Skip(2).FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
            if (entry.Command.Count > 0 && (Path.GetDirectoryName(entry.Command[0]) ?? "").EndsWith("/exports/bin", StringComparison.Ordinal)) return Path.GetFileName(entry.Command[0]);
            return null;
        }
        // The program a (non-Flatpak) .desktop entry runs, resolved through PATH.
        public static string DesktopProgram(string desktopFile)
        {
            var entry = DesktopEntry.Read(desktopFile);
            if (entry == null || entry.Command.Count == 0) return null;
            return FindOnPath(entry.Command[0]);
        }
        public static string FindOnPath(string command)
        {
            if (String.IsNullOrWhiteSpace(command)) return null;
            if (Path.IsPathRooted(command)) return File.Exists(command) ? command : null;
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (directory.Length == 0) continue;
                var candidate = Path.Combine(directory, command);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
        public static string FlatpakFilesFolder(string id)
        {
            foreach (var root in FlatpakInstallations)
            {
                var files = Path.Combine(root, "app", id, "current", "active", "files");
                if (Directory.Exists(files)) return files;
            }
            return null;
        }

        // ----- Installed version -------------------------------------------------------------------------------------

        // Returns null when the platform cannot report a version for this program.
        public static string ProgramVersion(string executable)
        {
            if (IsWindows)
            {
                var info = FileVersionInfo.GetVersionInfo(executable);
                return String.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
            }
            var flatpak = FlatpakId(executable);
            if (flatpak != null)
            {
                var output = Run("flatpak", "info", flatpak);
                var line = (output ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("Version:", StringComparison.Ordinal));
                return line == null ? null : line.Substring(8).Trim() + " (Flatpak)";
            }
            var target = Path.GetExtension(executable).Equals(".desktop", StringComparison.OrdinalIgnoreCase) ? DesktopProgram(executable) : executable;
            if (target == null) return null;
            if (Path.GetExtension(target).Equals(".AppImage", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(target), @"(?<![A-Za-z0-9])v?(\d+(?:\.\d+)+(?:-(?:alpha|beta|rc|dev)[0-9.]*)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value + " (from AppImage name)" : null;
            }
            target = RealPath(target);
            var pacman = Run("pacman", "-Qo", target);
            if (pacman != null)
            {
                var owned = System.Text.RegularExpressions.Regex.Match(pacman, @" is owned by (\S+) (\S+)");
                if (owned.Success) return owned.Groups[2].Value + " (" + owned.Groups[1].Value + " package)";
            }
            var dpkg = Run("dpkg", "-S", target);
            if (dpkg != null && dpkg.Contains(":"))
            {
                var package = dpkg.Split(':')[0].Trim();
                var version = Run("dpkg-query", "-W", "-f=${Version}", package);
                if (!String.IsNullOrWhiteSpace(version)) return version.Trim() + " (" + package + " package)";
            }
            var rpm = Run("rpm", "-qf", "--qf", "%{VERSION}-%{RELEASE}", target);
            if (!String.IsNullOrWhiteSpace(rpm) && !rpm.Contains(" ")) return rpm.Trim();
            return null;
        }

        private static readonly HashSet<int> helpers = new HashSet<int>();
        internal static bool IsHelper(int pid) { lock (helpers) return helpers.Contains(pid); }

        // Runs a helper program and returns its standard output, or null if it is unavailable or fails.
        public static string Run(string program, params string[] arguments)
        {
            try
            {
                var info = new ProcessStartInfo { FileName = program, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                AddArguments(info, arguments);
                info.EnvironmentVariables["LC_ALL"] = "C";
                using (var process = Process.Start(info))
                {
                    lock (helpers) helpers.Add(process.Id);
                    try
                    {
                        var output = process.StandardOutput.ReadToEndAsync();
                        process.StandardError.ReadToEndAsync();
                        if (!process.WaitForExit(4000)) { try { process.Kill(); } catch (Exception) { } return null; }
                        return process.ExitCode == 0 ? output.Result : null;
                    }
                    finally { lock (helpers) helpers.Remove(process.Id); }
                }
            }
            catch (Exception) { return null; }
        }

        public static string RealPath(string path)
        {
#if NETCOREAPP
            try
            {
                var target = File.ResolveLinkTarget(path, true);
                return target == null ? Path.GetFullPath(path) : target.FullName;
            }
            catch (Exception) { return path; }
#else
            return path;
#endif
        }

        // ----- Running state ------------------------------------------------------------------------------------------

        public static RuntimeState State(string executable)
        {
            if (!File.Exists(executable)) return RuntimeState.Stopped;
            if (IsWindows)
            {
                if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return RuntimeState.Unknown;
                bool unknown = false;
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                {
                    using (process)
                    {
                        try { if (!process.HasExited && HubPaths.Same(process.MainModule.FileName, executable)) return RuntimeState.Running; }
                        catch (Exception) { unknown = true; }
                    }
                }
                return unknown ? RuntimeState.Unknown : RuntimeState.Stopped;
            }
#if NETCOREAPP
            bool certain;
            var matches = UnixProcesses.Matching(executable, out certain);
            return matches.Count > 0 ? RuntimeState.Running : certain ? RuntimeState.Stopped : RuntimeState.Unknown;
#else
            return RuntimeState.Unknown;
#endif
        }

        // Call after starting a program so the next State() check rescans running processes.
        public static void ProcessesChanged()
        {
#if NETCOREAPP
            UnixProcesses.Invalidate();
#endif
        }

        public static bool BringForward(string executable)
        {
            if (IsWindows)
            {
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                    using (process)
                        try
                        {
                            if (HubPaths.Same(process.MainModule.FileName, executable) && process.MainWindowHandle != IntPtr.Zero)
                            { ShowWindow(process.MainWindowHandle, 9); SetForegroundWindow(process.MainWindowHandle); return true; }
                        }
                        catch (Exception) { }
                return false;
            }
#if NETCOREAPP
            bool certain;
            foreach (var pid in UnixProcesses.Matching(executable, out certain))
            {
                var text = pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
                { var result = Run("hyprctl", "dispatch", "focuswindow", "pid:" + text); if (result != null && result.Trim() == "ok") return true; }
                else if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SWAYSOCK")))
                { if (Run("swaymsg", "[pid=" + text + "]", "focus") != null) return true; }
                else if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                { if (Run("xdotool", "search", "--pid", text, "windowactivate") != null) return true; }
            }
#endif
            return false;
        }
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

        // ----- Shortcuts ---------------------------------------------------------------------------------------------

        // Windows: a desktop .lnk. Linux: an application-menu entry (desktop icons are not universal there).
        public static string ShortcutActionLabel { get { return IsWindows ? "Create desktop shortcut" : "Add FishBowl to the application menu"; } }
        public static string CreateAppShortcut(string appPath, string iconPath)
        {
            if (IsWindows)
            {
                string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FishBowl.lnk");
                if (!File.Exists(iconPath)) iconPath = appPath;
                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                object shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { appPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(appPath) });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "FishBowl Emulator Hub" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return "A FishBowl shortcut was added to your desktop.";
            }
            var applications = Path.Combine(DataHome, "applications"); Directory.CreateDirectory(applications);
            var file = Path.Combine(applications, "fishbowl.desktop");
            var quoted = "\"" + appPath.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`") + "\"";
            File.WriteAllText(file, "[Desktop Entry]\nType=Application\nName=FishBowl\nGenericName=Emulator Hub\nComment=Your emulators, together\nExec=" + quoted + "\nPath=" + Path.GetDirectoryName(appPath) + "\nIcon=" + (File.Exists(iconPath) ? iconPath : "applications-games") + "\nTerminal=false\nCategories=Game;Emulator;\nStartupWMClass=FishBowl\n");
            Run("update-desktop-database", applications);
            return "FishBowl was added to your application menu.\n\n" + file;
        }
    }

    // Minimal reader for freedesktop.org .desktop launchers: the [Desktop Entry] group's Name, Exec, Path and Icon.
    public class DesktopEntry
    {
        public string Name, Icon, TryExec, WorkingDirectory;
        public List<string> Command = new List<string>();
        public static DesktopEntry Read(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 256 * 1024) return null;
                var entry = new DesktopEntry(); bool inGroup = false;
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal)) { inGroup = line == "[Desktop Entry]"; continue; }
                    if (!inGroup || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int equals = line.IndexOf('='); if (equals < 1) continue;
                    var key = line.Substring(0, equals).Trim(); var value = line.Substring(equals + 1).Trim();
                    if (key == "Name") entry.Name = value;
                    else if (key == "Icon") entry.Icon = value;
                    else if (key == "TryExec") entry.TryExec = value;
                    else if (key == "Path") entry.WorkingDirectory = value;
                    else if (key == "Exec") entry.Command = ParseExec(value);
                }
                return entry;
            }
            catch (Exception) { return null; }
        }
        // Exec quoting rules from the Desktop Entry specification; field codes such as %f and %U are dropped.
        public static List<string> ParseExec(string value)
        {
            var result = new List<string>(); var current = new System.Text.StringBuilder(); bool quoted = false, any = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (quoted)
                {
                    if (c == '"') { quoted = false; continue; }
                    if (c == '\\' && i + 1 < value.Length && "\"`$\\".IndexOf(value[i + 1]) >= 0) { current.Append(value[++i]); continue; }
                    current.Append(c); continue;
                }
                if (c == '"') { quoted = true; any = true; continue; }
                if (c == ' ' || c == '\t') { if (any || current.Length > 0) { result.Add(current.ToString()); current.Clear(); any = false; } continue; }
                current.Append(c);
            }
            if (any || current.Length > 0) result.Add(current.ToString());
            var cleaned = new List<string>();
            foreach (var argument in result)
            {
                if (argument.Length == 2 && argument[0] == '%' && argument[1] != '%') continue;
                cleaned.Add(argument.Replace("%%", "%"));
            }
            return cleaned;
        }
    }

#if NETCOREAPP
    // Reads /proc to find processes started from an emulator program, AppImage, script or Flatpak.
    internal static class UnixProcesses
    {
        private class Info { public int Pid; public string Exe; public string[] Arguments; public string Cgroup; public string AppImage; }
        private static List<Info> cache; private static DateTime cachedAt;
        private static readonly object gate = new object();

        public static void Invalidate() { lock (gate) cache = null; }
        private static List<Info> Snapshot()
        {
            lock (gate)
            {
                if (cache != null && DateTime.UtcNow - cachedAt < TimeSpan.FromSeconds(1.5)) return cache;
                var list = new List<Info>(); int self = Environment.ProcessId;
                foreach (var directory in Directory.EnumerateDirectories("/proc"))
                {
                    int pid; if (!Int32.TryParse(Path.GetFileName(directory), out pid) || pid == self) continue;
                    var info = new Info { Pid = pid };
                    // Skip FishBowl's own helpers (pacman -Qo <program>, flatpak info, ...), which name the program in their arguments.
                    if (Platform.IsHelper(pid)) continue;
                    try { var link = new FileInfo(Path.Combine(directory, "exe")).LinkTarget; info.Exe = link == null ? null : link.Replace(" (deleted)", ""); }
                    catch (Exception) { continue; } // Another user's process: never ours to report.
                    try { info.Arguments = File.ReadAllText(Path.Combine(directory, "cmdline")).Split('\0').Take(4).ToArray(); } catch (Exception) { info.Arguments = new string[0]; }
                    try { info.Cgroup = File.ReadAllText(Path.Combine(directory, "cgroup")); } catch (Exception) { info.Cgroup = ""; }
                    if (info.Exe != null && info.Exe.StartsWith("/tmp/.mount_", StringComparison.Ordinal))
                    {
                        try { info.AppImage = File.ReadAllText(Path.Combine(directory, "environ")).Split('\0').Where(v => v.StartsWith("APPIMAGE=", StringComparison.Ordinal)).Select(v => v.Substring(9)).FirstOrDefault(); }
                        catch (Exception) { }
                    }
                    list.Add(info);
                }
                cache = list; cachedAt = DateTime.UtcNow; return list;
            }
        }

        // certain is false when a launcher (script, unresolvable .desktop) may start processes this cannot attribute.
        public static List<int> Matching(string executable, out bool certain)
        {
            certain = true; var full = Path.GetFullPath(executable);
            var flatpak = Platform.FlatpakId(full);
            var processes = Snapshot();
            if (flatpak != null)
            {
                var scope = "app-flatpak-" + flatpak + "-";
                return processes.Where(p => p.Cgroup.Contains(scope)).Select(p => p.Pid).ToList();
            }
            var extension = Path.GetExtension(full);
            if (extension.Equals(".AppImage", StringComparison.OrdinalIgnoreCase))
                return processes.Where(p => p.AppImage == full || p.Exe == full).Select(p => p.Pid).ToList();
            var target = full;
            if (extension.Equals(".desktop", StringComparison.OrdinalIgnoreCase))
            {
                target = Platform.DesktopProgram(full);
                if (target == null) { certain = false; return new List<int>(); }
            }
            var real = Platform.RealPath(target);
            var found = processes.Where(p => p.Exe == real || p.Exe == target || p.Arguments.Skip(1).Take(2).Contains(target) || p.AppImage == target).Select(p => p.Pid).ToList();
            if (found.Count == 0 && (extension.Equals(".sh", StringComparison.OrdinalIgnoreCase) || !IsElf(real))) certain = false;
            return found;
        }
        private static bool IsElf(string path)
        {
            try { using (var stream = File.OpenRead(path)) { var magic = new byte[4]; return stream.Read(magic, 0, 4) == 4 && magic[0] == 0x7F && magic[1] == (byte)'E' && magic[2] == (byte)'L' && magic[3] == (byte)'F'; } }
            catch (Exception) { return false; }
        }
    }
#endif

    // JSON used for library.json, backups and GitHub feeds. The Windows build keeps JavaScriptSerializer;
    // .NET (Linux) builds use System.Text.Json with the same property names, so settings files stay interchangeable.
    public static class Json
    {
#if NETCOREAPP
        private static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true };
        public static T Deserialize<T>(string text, int maxLength)
        {
            if (text != null && text.Length > maxLength) throw new InvalidDataException("The JSON document is too large.");
            try { return System.Text.Json.JsonSerializer.Deserialize<T>(text, Options); }
            catch (System.Text.Json.JsonException error) { throw new InvalidDataException(error.Message, error); }
        }
        public static string Serialize(object value, int maxLength)
        {
            var text = System.Text.Json.JsonSerializer.Serialize(value, value == null ? typeof(object) : value.GetType(), Options);
            if (text.Length > maxLength) throw new InvalidDataException("The JSON document is too large.");
            return text;
        }
#else
        public static T Deserialize<T>(string text, int maxLength)
        { return new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = maxLength }.Deserialize<T>(text); }
        public static string Serialize(object value, int maxLength)
        { return new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = maxLength }.Serialize(value); }
#endif
        public static T Deserialize<T>(string text) { return Deserialize<T>(text, 16 * 1024 * 1024); }
        public static string Serialize(object value) { return Serialize(value, 16 * 1024 * 1024); }
        public static bool TryParseString(string json, out string value)
        {
            value = null;
            try { value = Deserialize<string>(json, 1024 * 1024); return value != null; }
            catch (Exception) { return false; }
        }
    }
}
