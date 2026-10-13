using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Steam integration for Linux: finding Steam and its accounts, and reading/writing non-Steam shortcuts
// (userdata/<account>/config/shortcuts.vdf) that start FishBowl with the same --launch-game / --launch-emulator
// arguments as the Windows Jump List. Linux only: compiled into FishBowl.Avalonia and the tests, never the csc.exe build.
//
// Sources: ValveKeyValue's KV1 binary reader (node types), steam-shortcut-editor and Steam ROM Manager
// (generate-app-id.ts: appid = crc32(Exe + AppName) | 0x80000000, grid art "<appid>_icon.png"),
// Lutris (Steam folders, loginusers.vdf MostRecent/Timestamp) and Heroic (Steam rewrites the file on exit).
namespace EmulatorHub
{
    // One binary KeyValues node. Keys and scalar values keep their raw bytes so unknown fields round-trip exactly.
    public sealed class VdfNode
    {
        public const byte Map = 0x00, Text = 0x01, Int32 = 0x02, Float32 = 0x03, Pointer = 0x04, Color = 0x06, UInt64 = 0x07, End = 0x08, Int64 = 0x0A;
        public byte Type;
        public byte[] KeyBytes = new byte[0];
        public byte[] Value = new byte[0];
        public List<VdfNode> Children = new List<VdfNode>();

        public string Key { get { return Encoding.UTF8.GetString(KeyBytes); } set { KeyBytes = Encoding.UTF8.GetBytes(value); } }
        public string StringValue { get { return Type == Text ? Encoding.UTF8.GetString(Value) : null; } }
        public int IntValue { get { return Type == Int32 && Value.Length == 4 ? BitConverter.ToInt32(Value, 0) : 0; } }

        public static VdfNode NewMap(string key) { return new VdfNode { Type = Map, Key = key }; }
        public static VdfNode NewString(string key, string value) { return new VdfNode { Type = Text, Key = key, Value = Encoding.UTF8.GetBytes(value ?? "") }; }
        public static VdfNode NewInt(string key, int value) { return new VdfNode { Type = Int32, Key = key, Value = BitConverter.GetBytes(value) }; }

        public VdfNode Child(string key) { return Children.FirstOrDefault(c => String.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)); }
        public string GetString(string key) { var c = Child(key); return c == null ? null : c.StringValue; }
        // Keeps an existing key's casing and position (Steam writes "AppName", some tools "appname").
        public void SetString(string key, string value)
        {
            var c = Child(key);
            if (c == null) Children.Add(NewString(key, value));
            else { c.Type = Text; c.Value = Encoding.UTF8.GetBytes(value ?? ""); c.Children.Clear(); }
        }
        public void SetInt(string key, int value)
        {
            var c = Child(key);
            if (c == null) Children.Add(NewInt(key, value));
            else { c.Type = Int32; c.Value = BitConverter.GetBytes(value); c.Children.Clear(); }
        }
    }

    // Steam's binary KeyValues: 0x00 map, 0x01 string, 0x02 int32, 0x08 end; float, pointer, color and 64-bit values are kept as-is.
    public static class BinaryVdf
    {
        public static VdfNode Read(byte[] data)
        {
            var root = VdfNode.NewMap("");
            if (data.Length == 0) return root;
            int position = 0;
            ReadChildren(data, ref position, root, 0);
            if (position != data.Length) throw new InvalidDataException("The Steam shortcut file has unexpected data after its end.");
            return root;
        }
        private static void ReadChildren(byte[] data, ref int position, VdfNode parent, int depth)
        {
            if (depth > 32) throw new InvalidDataException("The Steam shortcut file is nested too deeply.");
            while (true)
            {
                if (position >= data.Length) throw new InvalidDataException("The Steam shortcut file ends unexpectedly.");
                byte type = data[position++];
                if (type == VdfNode.End) return;
                var node = new VdfNode { Type = type, KeyBytes = ReadText(data, ref position) };
                int size;
                switch (type)
                {
                    case VdfNode.Map: ReadChildren(data, ref position, node, depth + 1); parent.Children.Add(node); continue;
                    case VdfNode.Text: node.Value = ReadText(data, ref position); parent.Children.Add(node); continue;
                    case VdfNode.Int32: case VdfNode.Float32: case VdfNode.Pointer: case VdfNode.Color: size = 4; break;
                    case VdfNode.UInt64: case VdfNode.Int64: size = 8; break;
                    default: throw new InvalidDataException("The Steam shortcut file contains an unsupported value type (" + type + "). FishBowl left it unchanged.");
                }
                if (position + size > data.Length) throw new InvalidDataException("The Steam shortcut file ends unexpectedly.");
                node.Value = new byte[size]; Buffer.BlockCopy(data, position, node.Value, 0, size); position += size;
                parent.Children.Add(node);
            }
        }
        private static byte[] ReadText(byte[] data, ref int position)
        {
            int end = Array.IndexOf(data, (byte)0, position);
            if (end < 0) throw new InvalidDataException("The Steam shortcut file ends unexpectedly.");
            var bytes = new byte[end - position]; Buffer.BlockCopy(data, position, bytes, 0, bytes.Length); position = end + 1;
            return bytes;
        }

        public static byte[] Write(VdfNode root)
        {
            using (var output = new MemoryStream()) { WriteChildren(output, root); return output.ToArray(); }
        }
        private static void WriteChildren(Stream output, VdfNode parent)
        {
            foreach (var node in parent.Children)
            {
                output.WriteByte(node.Type); output.Write(node.KeyBytes, 0, node.KeyBytes.Length); output.WriteByte(0);
                if (node.Type == VdfNode.Map) WriteChildren(output, node);
                else { output.Write(node.Value, 0, node.Value.Length); if (node.Type == VdfNode.Text) output.WriteByte(0); }
            }
            output.WriteByte(VdfNode.End);
        }
    }

    // Minimal text KeyValues reader for loginusers.vdf; keys are matched case-insensitively.
    public static class TextVdf
    {
        public sealed class Entry { public string Value; public Dictionary<string, Entry> Children; }
        public static Dictionary<string, Entry> Parse(string text)
        {
            var tokens = new List<string>(); var structural = new List<bool>(); int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (Char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') { while (i < text.Length && text[i] != '\n') i++; continue; }
                if (c == '{' || c == '}') { tokens.Add(c.ToString()); structural.Add(true); i++; continue; }
                var value = new StringBuilder();
                if (c == '"')
                {
                    i++;
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length) { i++; value.Append(text[i] == 'n' ? '\n' : text[i] == 't' ? '\t' : text[i]); }
                        else value.Append(text[i]);
                        i++;
                    }
                    i++;
                }
                else while (i < text.Length && !Char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"') value.Append(text[i++]);
                tokens.Add(value.ToString()); structural.Add(false);
            }
            int position = 0; return Block(tokens, structural, ref position);
        }
        private static Dictionary<string, Entry> Block(List<string> tokens, List<bool> structural, ref int position)
        {
            var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            while (position < tokens.Count)
            {
                if (structural[position] && tokens[position] == "}") { position++; break; }
                var key = tokens[position++]; if (position >= tokens.Count) break;
                if (structural[position] && tokens[position] == "{") { position++; result[key] = new Entry { Children = Block(tokens, structural, ref position) }; }
                else result[key] = new Entry { Value = tokens[position++] };
            }
            return result;
        }
    }

    public sealed class SteamInstall
    {
        public string Root, Kind; // Kind: "Steam", "Flatpak" or "Snap"
        public List<SteamAccount> Accounts = new List<SteamAccount>();
        public bool Sandboxed { get { return Kind != "Steam"; } }
    }

    public sealed class SteamAccount
    {
        public SteamInstall Install;
        public string AccountId, AccountName, PersonaName;
        public bool MostRecent; public long Timestamp;
        public string ConfigDirectory { get { return Path.Combine(Install.Root, "userdata", AccountId, "config"); } }
        public string ShortcutsFile { get { return Path.Combine(ConfigDirectory, "shortcuts.vdf"); } }
        public string GridDirectory { get { return Path.Combine(ConfigDirectory, "grid"); } }
        public string Label
        {
            get
            {
                var name = !String.IsNullOrWhiteSpace(PersonaName) ? PersonaName + (String.IsNullOrWhiteSpace(AccountName) ? "" : " (" + AccountName + ")") : "Steam account " + AccountId;
                return name + (Install.Kind == "Steam" ? "" : " — " + Install.Kind + " Steam");
            }
        }
        public override string ToString() { return Label; }
    }

    public static class SteamLocator
    {
        public const long SteamId64Base = 76561197960265728;

        // Native (~/.local/share/Steam, the ~/.steam links, Debian's ~/.steam/debian-installation), Flatpak and Snap.
        public static List<SteamInstall> Find(string home)
        {
            var candidates = new List<Tuple<string, string>>();
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!String.IsNullOrWhiteSpace(dataHome) && Path.IsPathRooted(dataHome)) candidates.Add(Tuple.Create(Path.Combine(dataHome, "Steam"), "Steam"));
            foreach (var relative in new[] { ".local/share/Steam", ".steam/steam", ".steam/root", ".steam/debian-installation" }) candidates.Add(Tuple.Create(Path.Combine(home, relative), "Steam"));
            foreach (var relative in new[] { ".var/app/com.valvesoftware.Steam/.local/share/Steam", ".var/app/com.valvesoftware.Steam/data/Steam" }) candidates.Add(Tuple.Create(Path.Combine(home, relative), "Flatpak"));
            candidates.Add(Tuple.Create(Path.Combine(home, "snap/steam/common/.local/share/Steam"), "Snap"));
            var installs = new List<SteamInstall>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (!Directory.Exists(Path.Combine(candidate.Item1, "userdata"))) continue;
                var real = FolderLinks.Canonical(candidate.Item1) ?? candidate.Item1;
                if (!seen.Add(real)) continue;
                var install = new SteamInstall { Root = real, Kind = candidate.Item2 };
                ReadAccounts(install); if (install.Accounts.Count > 0) installs.Add(install);
            }
            return installs.OrderByDescending(i => i.Accounts.Max(a => a.MostRecent ? Int64.MaxValue : a.Timestamp)).ToList();
        }

        public static void ReadAccounts(SteamInstall install)
        {
            var known = new Dictionary<string, TextVdf.Entry>();
            var file = Path.Combine(install.Root, "config", "loginusers.vdf");
            try
            {
                TextVdf.Entry users;
                if (File.Exists(file) && TextVdf.Parse(File.ReadAllText(file)).TryGetValue("users", out users) && users.Children != null)
                    foreach (var user in users.Children) { long id; if (Int64.TryParse(user.Key, out id) && id > SteamId64Base && user.Value.Children != null) known[(id - SteamId64Base).ToString()] = user.Value; }
            }
            catch (Exception error) { Store.Log("Steam loginusers.vdf unreadable: " + error.Message); }
            IEnumerable<string> folders;
            try { folders = Directory.EnumerateDirectories(Path.Combine(install.Root, "userdata")).Select(Path.GetFileName).Where(n => n.Length > 0 && n != "0" && n.All(Char.IsDigit)).ToList(); }
            catch (Exception) { return; }
            foreach (var id in folders)
            {
                var account = new SteamAccount { Install = install, AccountId = id };
                TextVdf.Entry user;
                if (known.TryGetValue(id, out user))
                {
                    account.AccountName = Value(user, "AccountName"); account.PersonaName = Value(user, "PersonaName");
                    account.MostRecent = Value(user, "MostRecent") == "1"; long stamp; Int64.TryParse(Value(user, "Timestamp"), out stamp); account.Timestamp = stamp;
                }
                install.Accounts.Add(account);
            }
            // Steam no longer always writes MostRecent; Timestamp (last login) decides then.
            install.Accounts = install.Accounts.OrderByDescending(a => a.MostRecent).ThenByDescending(a => a.Timestamp).ThenBy(a => a.AccountId).ToList();
        }
        private static string Value(TextVdf.Entry entry, string key) { TextVdf.Entry child; return entry.Children.TryGetValue(key, out child) ? child.Value : null; }

        // Steam rewrites shortcuts.vdf when it exits, so nothing is written while the client or its web helper runs.
        public static List<string> RunningProcesses(string procRoot, int uid)
        {
            var found = new List<string>();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateDirectories(procRoot).ToList(); } catch (Exception) { return found; }
            foreach (var directory in entries)
            {
                if (!Path.GetFileName(directory).All(Char.IsDigit)) continue;
                try
                {
                    var name = File.ReadAllText(Path.Combine(directory, "comm")).Trim();
                    if (name != "steam" && name != "steamwebhelper" && name != "steam.sh") continue;
                    if (uid >= 0 && ProcessUid(directory) != uid) continue;
                    found.Add(name);
                }
                catch (Exception) { }
            }
            return found.Distinct().ToList();
        }
        public static bool IsRunning() { return RunningProcesses("/proc", ProcessUid("/proc/self")).Count > 0; }
        private static int ProcessUid(string directory)
        {
            try
            {
                var line = File.ReadAllLines(Path.Combine(directory, "status")).FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal));
                int uid; return line != null && Int32.TryParse(line.Substring(4).Trim().Split('\t', ' ')[0], out uid) ? uid : -1;
            }
            catch (Exception) { return -1; }
        }
    }

    // What FishBowl wants in Steam: Key is "game:<id>", "emulator:<id>" or "living-room".
    // IconFile becomes grid/<appid>_icon.<ext>; GridFile (a portrait cover) becomes grid/<appid>p.<ext>.
    public sealed class SteamShortcutSpec
    {
        public string Key, Name, Exe, StartDir, LaunchOptions, IconFile, GridFile;
    }

    public sealed class SteamShortcutResult
    {
        public List<string> Added = new List<string>(), Updated = new List<string>(), Removed = new List<string>(), Artwork = new List<string>();
        public string File, Backup;
        public int Changes { get { return Added.Count + Updated.Count + Removed.Count; } }
    }

    public static class SteamShortcuts
    {
        public const string Tag = "FishBowl";

        // Steam ROM Manager's generate-app-id.ts: crc32 of the quoted Exe plus AppName, top bit set. The same number
        // is stored (as int32) in the "appid" field and names the grid artwork.
        public static uint AppId(string exe, string name) { return Crc32(Encoding.UTF8.GetBytes((exe ?? "") + (name ?? ""))) | 0x80000000; }
        public static ulong LongAppId(uint appId) { return ((ulong)appId << 32) | 0x02000000; }
        private static uint[] table;
        public static uint Crc32(byte[] data)
        {
            if (table == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[n] = c; }
                table = t;
            }
            uint crc = 0xFFFFFFFF;
            foreach (var b in data) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        public static string Quote(string value) { return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`") + "\""; }

        // Exe, StartDir and LaunchOptions for "FishBowl <arguments>". A sandboxed (Flatpak) Steam cannot see FishBowl or
        // the emulators, so its shortcuts go through flatpak-spawn --host, which needs Steam's org.freedesktop.Flatpak permission.
        public static void Command(SteamInstall install, string program, string leadingArgument, string arguments, out string exe, out string startDir, out string launchOptions)
        {
            var self = (String.IsNullOrEmpty(leadingArgument) ? "" : Quote(leadingArgument) + " ") + arguments;
            if (install != null && install.Kind == "Flatpak")
            {
                exe = "\"/usr/bin/flatpak-spawn\""; startDir = "\"/usr/bin/\"";
                launchOptions = "--host " + Quote(program) + " " + self; return;
            }
            exe = "\"" + program + "\""; startDir = "\"" + Path.GetDirectoryName(program).TrimEnd('/') + "/\""; launchOptions = self;
        }

        // The command-line arguments FishBowl understands (the Windows Jump List uses the same two launch options).
        public static string Arguments(string key)
        {
            if (key == "living-room") return "--living-room";
            if (key.StartsWith("game:", StringComparison.Ordinal)) return "--launch-game " + QuoteIfNeeded(key.Substring(5));
            if (key.StartsWith("emulator:", StringComparison.Ordinal)) return "--launch-emulator " + QuoteIfNeeded(key.Substring(9));
            throw new ArgumentException("Unknown Steam shortcut key: " + key);
        }
        static string QuoteIfNeeded(string id) { return id.All(c => Char.IsLetterOrDigit(c) || c == '-' || c == '_') ? id : Quote(id); }

        // FishBowl's entries carry the FishBowl tag and a --launch-game, --launch-emulator or --living-room option.
        public static string KeyOf(VdfNode shortcut)
        {
            var tags = shortcut.Child("tags");
            if (tags == null || !tags.Children.Any(t => String.Equals(t.StringValue, Tag, StringComparison.OrdinalIgnoreCase))) return null;
            var options = Platform.SplitArguments(shortcut.GetString("LaunchOptions") ?? "");
            int index = options.IndexOf("--launch-game");
            if (index >= 0 && index + 1 < options.Count) return "game:" + options[index + 1];
            index = options.IndexOf("--launch-emulator");
            if (index >= 0 && index + 1 < options.Count) return "emulator:" + options[index + 1];
            return options.Contains("--living-room") ? "living-room" : null;
        }

        // Artwork FishBowl writes for an app ID: the icon and the portrait grid image.
        public static bool IsOwnArtwork(string file, uint appId, string grid)
        {
            var name = Path.GetFileNameWithoutExtension(file ?? "");
            return HubPaths.Same(Path.GetDirectoryName(file), grid) && (name == appId + "_icon" || name == appId + "p");
        }

        public static VdfNode Load(string file)
        {
            var root = File.Exists(file) ? BinaryVdf.Read(File.ReadAllBytes(file)) : VdfNode.NewMap("");
            if (root.Child("shortcuts") == null)
            {
                if (root.Children.Count > 0) throw new InvalidDataException("This file is not a Steam shortcut list: " + file);
                root.Children.Add(VdfNode.NewMap("shortcuts"));
            }
            return root;
        }

        public static Dictionary<string, VdfNode> Existing(VdfNode root)
        {
            var result = new Dictionary<string, VdfNode>(StringComparer.Ordinal);
            foreach (var shortcut in root.Child("shortcuts").Children.Where(c => c.Type == VdfNode.Map)) { var key = KeyOf(shortcut); if (key != null && !result.ContainsKey(key)) result[key] = shortcut; }
            return result;
        }

        // Adds or updates the wanted shortcuts and removes the listed FishBowl ones, keeping every other entry and field.
        // With dryRun nothing is written; the result lists what would change.
        public static SteamShortcutResult Apply(SteamAccount account, IList<SteamShortcutSpec> wanted, IEnumerable<string> removeKeys, Func<bool> steamRunning, bool dryRun)
        {
            if (!dryRun && steamRunning()) throw new IOException("Steam is running. Exit Steam completely (Steam > Exit), then try again. Steam rewrites its shortcut list when it closes, so changes made now would be lost.");
            var result = new SteamShortcutResult { File = account.ShortcutsFile };
            var original = File.Exists(result.File) ? File.ReadAllBytes(result.File) : null;
            var root = Load(result.File); var list = root.Child("shortcuts");
            var seen = new HashSet<string>(StringComparer.Ordinal); var removals = new HashSet<string>(removeKeys ?? new string[0], StringComparer.Ordinal);
            var copies = new List<Tuple<string, string>>();
            foreach (var shortcut in list.Children.Where(c => c.Type == VdfNode.Map).ToList())
            {
                var key = KeyOf(shortcut); if (key == null) continue;
                bool duplicate = !seen.Add(key);
                if (duplicate || (removals.Contains(key) && !wanted.Any(w => w.Key == key)))
                {
                    list.Children.Remove(shortcut); result.Removed.Add(shortcut.GetString("AppName") ?? key);
                    var id = shortcut.Child("appid");
                    if (!duplicate && id != null && Directory.Exists(account.GridDirectory))
                    {
                        var app = unchecked((uint)id.IntValue);
                        foreach (var art in Directory.EnumerateFiles(account.GridDirectory, app + "*").Where(f => IsOwnArtwork(f, app, account.GridDirectory)))
                            copies.Add(Tuple.Create<string, string>(null, art));
                    }
                }
            }
            var existing = Existing(root);
            foreach (var spec in wanted)
            {
                VdfNode shortcut; uint appId;
                if (existing.TryGetValue(spec.Key, out shortcut))
                {
                    var appIdNode = shortcut.Child("appid");
                    appId = appIdNode != null && appIdNode.Type == VdfNode.Int32 ? unchecked((uint)appIdNode.IntValue) : AppId(spec.Exe, spec.Name);
                    bool changed = shortcut.GetString("AppName") != spec.Name || shortcut.GetString("Exe") != spec.Exe || shortcut.GetString("StartDir") != spec.StartDir || shortcut.GetString("LaunchOptions") != spec.LaunchOptions;
                    shortcut.SetInt("appid", unchecked((int)appId)); shortcut.SetString("AppName", spec.Name); shortcut.SetString("Exe", spec.Exe);
                    shortcut.SetString("StartDir", spec.StartDir); shortcut.SetString("LaunchOptions", spec.LaunchOptions);
                    if (changed) result.Updated.Add(spec.Name);
                }
                else
                {
                    appId = AppId(spec.Exe, spec.Name);
                    shortcut = VdfNode.NewMap(list.Children.Count.ToString());
                    shortcut.Children.Add(VdfNode.NewInt("appid", unchecked((int)appId)));
                    shortcut.Children.Add(VdfNode.NewString("AppName", spec.Name)); shortcut.Children.Add(VdfNode.NewString("Exe", spec.Exe));
                    shortcut.Children.Add(VdfNode.NewString("StartDir", spec.StartDir)); shortcut.Children.Add(VdfNode.NewString("icon", ""));
                    shortcut.Children.Add(VdfNode.NewString("ShortcutPath", "")); shortcut.Children.Add(VdfNode.NewString("LaunchOptions", spec.LaunchOptions));
                    foreach (var flag in new[] { "IsHidden|0", "AllowDesktopConfig|1", "AllowOverlay|1", "OpenVR|0", "Devkit|0" }) shortcut.Children.Add(VdfNode.NewInt(flag.Split('|')[0], Int32.Parse(flag.Split('|')[1])));
                    shortcut.Children.Add(VdfNode.NewString("DevkitGameID", "")); shortcut.Children.Add(VdfNode.NewInt("DevkitOverrideAppID", 0));
                    shortcut.Children.Add(VdfNode.NewInt("LastPlayTime", 0)); shortcut.Children.Add(VdfNode.NewString("FlatpakAppID", ""));
                    var tags = VdfNode.NewMap("tags"); tags.Children.Add(VdfNode.NewString("0", Tag)); shortcut.Children.Add(tags);
                    list.Children.Add(shortcut); existing[spec.Key] = shortcut; result.Added.Add(spec.Name);
                }
                if (!String.IsNullOrEmpty(spec.IconFile) && File.Exists(spec.IconFile))
                {
                    var target = Path.Combine(account.GridDirectory, appId + "_icon" + Path.GetExtension(spec.IconFile).ToLowerInvariant());
                    if (shortcut.GetString("icon") != target && !result.Added.Contains(spec.Name) && !result.Updated.Contains(spec.Name)) result.Updated.Add(spec.Name);
                    shortcut.SetString("icon", target); copies.Add(Tuple.Create(spec.IconFile, target)); result.Artwork.Add(target);
                }
                if (!String.IsNullOrEmpty(spec.GridFile) && File.Exists(spec.GridFile))
                {
                    var target = Path.Combine(account.GridDirectory, appId + "p" + Path.GetExtension(spec.GridFile).ToLowerInvariant());
                    copies.Add(Tuple.Create(spec.GridFile, target)); result.Artwork.Add(target);
                }
            }
            // Steam numbers its entries "0", "1", ...; keep that after removals.
            for (int i = 0; i < list.Children.Count; i++) if (list.Children[i].Type == VdfNode.Map && list.Children[i].Key != i.ToString()) list.Children[i].Key = i.ToString();
            if (dryRun) return result;

            Directory.CreateDirectory(account.ConfigDirectory);
            if (original != null)
            {
                result.Backup = result.File + ".fishbowl-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.WriteAllBytes(result.Backup, original);
            }
            foreach (var copy in copies)
            {
                if (copy.Item1 == null) { if (File.Exists(copy.Item2)) File.Delete(copy.Item2); continue; }
                Directory.CreateDirectory(account.GridDirectory);
                // One image per slot: drop an older copy with another extension (cover.jpg replaced by cover.png).
                foreach (var stale in Directory.EnumerateFiles(account.GridDirectory, Path.GetFileNameWithoutExtension(copy.Item2) + ".*").Where(f => f != copy.Item2)) File.Delete(stale);
                File.Copy(copy.Item1, copy.Item2, true);
            }
            var temporary = result.File + ".fishbowl-" + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, BinaryVdf.Write(root)); File.Move(temporary, result.File, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Store.Log("Steam shortcuts updated for account " + account.AccountId + ": " + result.Added.Count + " added, " + result.Updated.Count + " updated, " + result.Removed.Count + " removed" + (result.Backup == null ? "" : "; backup " + result.Backup));
            return result;
        }
    }

    // Resolves every symbolic link in a path, like realpath(3). Platform.RealPath only resolves the last component.
    public static class FolderLinks
    {
        public static string Canonical(string path) { return Canonical(path, 0); }
        private static string Canonical(string path, int depth)
        {
            if (String.IsNullOrWhiteSpace(path) || depth > 32) return null;
            string full; try { full = Path.GetFullPath(path); } catch (Exception) { return null; }
            var parts = full.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); var current = "/";
            for (int i = 0; i < parts.Length; i++)
            {
                var next = Path.Combine(current, parts[i]); string link = null;
                try { link = new FileInfo(next).LinkTarget; } catch (Exception) { }
                if (link != null)
                {
                    var target = Path.IsPathRooted(link) ? link : Path.Combine(current, link);
                    var rest = String.Join("/", parts.Skip(i + 1));
                    return Canonical(rest.Length == 0 ? target : Path.Combine(target, rest), depth + 1);
                }
                current = next;
            }
            return current;
        }
    }
}
