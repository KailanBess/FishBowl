using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace EmulatorHub {
 public class GameToolsSettings {
#if NETCOREAPP
  [System.Text.Json.Serialization.JsonExtensionData]
  public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

  public List<ModProfile> ModProfiles { get; set; }
  public List<GameMediaLink> MediaLinks { get; set; }
  public GameToolsSettings() { ModProfiles = new List<ModProfile>(); MediaLinks = new List<GameMediaLink>(); }
 }
 public class GameToolsLibrarySettings {
#if NETCOREAPP
  [System.Text.Json.Serialization.JsonExtensionData]
  public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

  public List<string> SteamLibraryFolders { get; set; }
  public GameToolsLibrarySettings() { SteamLibraryFolders = new List<string>(); }
 }
 public class GameMediaLink {
#if NETCOREAPP
  [System.Text.Json.Serialization.JsonExtensionData]
  public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
 public string Label { get; set; } public string Path { get; set; } public string Kind { get; set; } }
 public class ModFileRecord {
#if NETCOREAPP
  [System.Text.Json.Serialization.JsonExtensionData]
  public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
 public string RelativePath { get; set; } public string PreviousHash { get; set; } public string AppliedHash { get; set; } public bool Existed { get; set; } }
 public class ModProfile {
#if NETCOREAPP
  [System.Text.Json.Serialization.JsonExtensionData]
  public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif

  public string Id { get; set; } public string Name { get; set; } public string SourceFolder { get; set; } public string DestinationFolder { get; set; }
  public string BackupFolder { get; set; } public bool Applied { get; set; } public List<ModFileRecord> Files { get; set; }
  public ModProfile() { Files = new List<ModFileRecord>(); }
  public override string ToString() { return Name + (Applied ? " (applied)" : ""); }
 }
 public class InstalledSteamGame { public string AppId { get; set; } public string Title { get; set; } public string Folder { get; set; } public override string ToString() { return Title + " (" + AppId + ")"; } }
 public class MonthlyPlayTotal { public string Month { get; set; } public long Seconds { get; set; } public int Sessions { get; set; } public int Games { get; set; } }
 public static class GameTools {
  public static string Hash(string path) { using (SHA256 h = SHA256.Create()) using (FileStream s = File.OpenRead(path)) { return BitConverter.ToString(h.ComputeHash(s)).Replace("-", ""); } }
  static string Root(string path) { if (string.IsNullOrWhiteSpace(path)) throw new IOException("Select a folder."); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; }
  static void NoLinks(string path) {
   string full = Path.GetFullPath(path);
   for (string p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p)) {
    if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked folders and files are not supported: " + p);
    if (Path.GetPathRoot(p) == p) break;
   }
  }
  static string Inside(string root, string relative) {
   if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(p => string.IsNullOrEmpty(p) || p == ".." || p == "." || p.EndsWith(".") || p.EndsWith(" ") || p.IndexOf(':') >= 0 || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || (Platform.IsWindows && Regex.IsMatch(p, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|$)", RegexOptions.IgnoreCase)))) throw new IOException("Invalid relative file path.");
   string path = Path.GetFullPath(Path.Combine(root, relative));
   if (!path.StartsWith(Root(root), StringComparison.OrdinalIgnoreCase)) throw new IOException("File leaves the selected folder.");
   NoLinks(path); return path;
  }
  static IEnumerable<string> Files(string root) {
   Stack<string> pending = new Stack<string>(); pending.Push(root);
   while (pending.Count > 0) { string folder = pending.Pop(); NoLinks(folder); foreach (string f in Directory.GetFiles(folder)) { NoLinks(f); yield return f; } foreach (string child in Directory.GetDirectories(folder)) { NoLinks(child); pending.Push(child); } }
  }
  static void CopyAtomic(string source, string target, string expectedHash) {
   string temporary = target + ".fishbowl-" + Guid.NewGuid().ToString("N");
   try { File.Copy(source, temporary, false); if (Hash(temporary) != expectedHash) throw new IOException("The source file changed while copying. Review it again."); if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target); } finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }
  public static List<ModFileRecord> PreviewMod(string source, string destination) {
   string src = Root(source), dst = Root(destination); NoLinks(src); NoLinks(dst);
   if (!Directory.Exists(src) || !Directory.Exists(dst)) throw new IOException("Both selected folders must exist.");
   if (src.StartsWith(dst, StringComparison.OrdinalIgnoreCase) || dst.StartsWith(src, StringComparison.OrdinalIgnoreCase)) throw new IOException("Mod and destination folders must be separate.");
   List<ModFileRecord> result = new List<ModFileRecord>();
   foreach (string file in Files(src)) {
    if (result.Count >= 10000) throw new IOException("A profile supports at most 10,000 files.");
    NoLinks(file); string relative = file.Substring(src.Length); string target = Inside(dst, relative);
    if (Directory.Exists(target)) throw new IOException("A folder conflicts with a mod file: " + relative);
    result.Add(new ModFileRecord { RelativePath = relative, Existed = File.Exists(target), PreviousHash = File.Exists(target) ? Hash(target) : null, AppliedHash = Hash(file) });
   }
   if (result.Count == 0) throw new IOException("The mod folder has no files."); return result;
  }
  public static void ApplyMod(ModProfile profile, List<ModFileRecord> reviewed, string backupRoot) { ApplyMod(profile, reviewed, backupRoot, null); }
  public static void ApplyMod(ModProfile profile, List<ModFileRecord> reviewed, string backupRoot, Action checkpoint) {
   if (profile.Applied) throw new IOException("Roll back this profile before applying it again.");
   List<ModFileRecord> current = PreviewMod(profile.SourceFolder, profile.DestinationFolder);
   if (reviewed == null || current.Count != reviewed.Count || current.Any(x => !reviewed.Any(y => y.RelativePath == x.RelativePath && y.AppliedHash == x.AppliedHash && y.PreviousHash == x.PreviousHash && y.Existed == x.Existed))) throw new IOException("Files changed since the preview. Review them again.");
   string backup = Root(Path.Combine(backupRoot, Guid.NewGuid().ToString("N"))); NoLinks(backup);
   string src = Root(profile.SourceFolder), dst = Root(profile.DestinationFolder);
   if (backup.StartsWith(src, StringComparison.OrdinalIgnoreCase) || backup.StartsWith(dst, StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup storage must be outside the mod and destination folders.");
   Directory.CreateDirectory(backup);
   foreach (ModFileRecord f in current.Where(x => x.Existed)) { string b = Inside(backup, f.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(b)); File.Copy(Inside(dst, f.RelativePath), b, false); if (Hash(b) != f.PreviousHash) throw new IOException("A destination file changed while backing up. Review it again."); }
   profile.BackupFolder = backup; profile.Files = new List<ModFileRecord>(); profile.Applied = true; if (checkpoint != null) checkpoint();
   try { foreach (ModFileRecord f in current) { string target = Inside(dst, f.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target) != f.Existed || (f.Existed && Hash(target) != f.PreviousHash)) throw new IOException("The destination changed after review: " + f.RelativePath); profile.Files.Add(f); if (checkpoint != null) checkpoint(); CopyAtomic(Inside(src, f.RelativePath), target, f.AppliedHash); } }
   catch { RollbackMod(profile, checkpoint); throw; }
  }
  public static void ValidateRollback(ModProfile profile) {
   if (!profile.Applied || profile.Files == null) throw new IOException("This profile is not applied.");
   HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach (ModFileRecord f in profile.Files) {
    if (f == null || !visited.Add(f.RelativePath)) throw new IOException("Invalid duplicate rollback record.");
    string target = Inside(profile.DestinationFolder, f.RelativePath);
    if (!IsAppliedOrRestored(target, f)) throw new IOException("A mod file changed after application. Preserve it before rollback: " + f.RelativePath);
    if (f.Existed) { string backup = Inside(profile.BackupFolder, f.RelativePath); if (!File.Exists(backup) || Hash(backup) != f.PreviousHash) throw new IOException("A rollback backup is missing or changed: " + f.RelativePath); }
   }
  }
  static bool IsAppliedOrRestored(string target, ModFileRecord file) { if (!File.Exists(target)) return !file.Existed; string hash = Hash(target); return hash == file.AppliedHash || file.Existed && hash == file.PreviousHash; }
  public static void RollbackMod(ModProfile profile) { RollbackMod(profile, null); }
  public static void RollbackMod(ModProfile profile, Action checkpoint) {
   ValidateRollback(profile);
   foreach (ModFileRecord f in profile.Files.ToArray()) { string target = Inside(profile.DestinationFolder, f.RelativePath); if (!IsAppliedOrRestored(target, f)) throw new IOException("A destination file changed during rollback: " + f.RelativePath); if (f.Existed && Hash(target) != f.PreviousHash) CopyAtomic(Inside(profile.BackupFolder, f.RelativePath), target, f.PreviousHash); else if (!f.Existed && File.Exists(target)) File.Delete(target); profile.Files.Remove(f); if (checkpoint != null) checkpoint(); }
   profile.Applied = false; if (checkpoint != null) checkpoint();
  }
  public static void CreateIpsPatchedCopy(string original, string patch, string output) {
   original = Path.GetFullPath(original); patch = Path.GetFullPath(patch); output = Path.GetFullPath(output);
   NoLinks(original); NoLinks(patch); NoLinks(output);
   if (File.Exists(output) || Directory.Exists(output) || string.Equals(output, original, StringComparison.OrdinalIgnoreCase) || string.Equals(output, patch, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a new output file. Original files are preserved.");
   const int limit = 64 * 1024 * 1024;
   if (new FileInfo(original).Length > limit || new FileInfo(patch).Length > limit) throw new IOException("IPS files and originals must be at most 64 MB.");
   byte[] input = File.ReadAllBytes(original), data = File.ReadAllBytes(patch);
   if (data.Length < 8 || System.Text.Encoding.ASCII.GetString(data, 0, 5) != "PATCH") throw new IOException("Invalid IPS patch header.");
   using (MemoryStream result = new MemoryStream()) {
    result.Write(input, 0, input.Length); int at = 5; bool ended = false;
    while (at + 3 <= data.Length) {
     if (data[at] == 69 && data[at+1] == 79 && data[at+2] == 70) { at += 3; ended = true; break; }
     int offset = (data[at] << 16) | (data[at+1] << 8) | data[at+2]; at += 3;
     if (at + 2 > data.Length) throw new IOException("Truncated IPS record.");
     int count = (data[at] << 8) | data[at+1]; at += 2; result.Position = offset;
     if (count == 0) {
      if (at + 3 > data.Length) throw new IOException("Truncated IPS repeat record.");
      count = (data[at] << 8) | data[at+1]; byte value = data[at+2]; at += 3;
      if (count == 0 || offset + count > limit) throw new IOException("Invalid IPS repeat length.");
      for (int i = 0; i < count; i++) result.WriteByte(value);
     } else { if (at + count > data.Length || offset + count > limit) throw new IOException("Truncated or oversized IPS record."); result.Write(data, at, count); at += count; }
    }
    if (!ended || (data.Length != at && data.Length != at + 3)) throw new IOException("Invalid IPS end marker.");
    if (data.Length == at + 3) result.SetLength((data[at] << 16) | (data[at+1] << 8) | data[at+2]);
    using (FileStream destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { result.Position = 0; result.CopyTo(destination); }
   }
  }
  public static List<InstalledSteamGame> SteamGames(string steamLibrary) {
   string root = Root(steamLibrary); NoLinks(root); string apps = Path.Combine(root, "steamapps");
   if (!Directory.Exists(apps)) throw new IOException("Select a Steam library folder containing steamapps.");
   List<InstalledSteamGame> result = new List<InstalledSteamGame>();
   foreach (string path in Directory.GetFiles(apps, "appmanifest_*.acf")) {
    NoLinks(path); if (new FileInfo(path).Length > 1024 * 1024) continue;
    string text = File.ReadAllText(path); string appid = Acf(text, "appid"), name = Acf(text, "name"), folder = Acf(text, "installdir");
    if (!Regex.IsMatch(appid, "^[0-9]+$") || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder)) continue;
    try { string installed = Inside(Path.Combine(apps, "common"), folder); if (Directory.Exists(installed)) result.Add(new InstalledSteamGame { AppId = appid, Title = name, Folder = installed }); } catch (IOException) { } catch (ArgumentException) { }
   }
   return result.OrderBy(x => x.Title).ToList();
  }
  static string Acf(string text, string field) { Match m = Regex.Match(text, "\\\"" + field + "\\\"\\s*\\\"([^\\\"]*)\\\"", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value : ""; }
  public static GameEntry NativeEntry(string path, string title) {
   path = Path.GetFullPath(path); bool supported = Platform.IsWindows ? string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase) : Platform.IsLaunchFile(path); if (!File.Exists(path) || !supported) throw new IOException("Select an existing native executable or launcher.");
   return new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(path) : title, TitleIsCustom = !string.IsNullOrWhiteSpace(title), Path = path, AddedAt = DateTime.UtcNow.ToString("o"), Tags = new List<string>(), Extras = new GameExtras { Native = true, WorkingDirectory = Path.GetDirectoryName(path) } };
  }
  public static List<MonthlyPlayTotal> Monthly(IEnumerable<PlaySession> sessions) {
   Dictionary<string, MonthlyPlayTotal> totals = new Dictionary<string, MonthlyPlayTotal>(); Dictionary<string, HashSet<string>> games = new Dictionary<string, HashSet<string>>();
   foreach (PlaySession s in sessions ?? new List<PlaySession>()) { DateTimeOffset date; if (s == null || !DateTimeOffset.TryParse(s.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)) continue; string month = date.ToString("yyyy-MM", CultureInfo.InvariantCulture); if (!totals.ContainsKey(month)) { totals[month] = new MonthlyPlayTotal { Month = month }; games[month] = new HashSet<string>(); } long seconds = Math.Max(0, s.Seconds); totals[month].Seconds = seconds > long.MaxValue - totals[month].Seconds ? long.MaxValue : totals[month].Seconds + seconds; totals[month].Sessions++; if (!string.IsNullOrEmpty(s.GameId)) games[month].Add(s.GameId); }
   foreach (string month in totals.Keys) totals[month].Games = games[month].Count; return totals.Values.OrderByDescending(x => x.Month).ToList();
  }
 }
}
