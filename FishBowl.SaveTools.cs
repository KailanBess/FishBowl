using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EmulatorHub
{
    // Save history shared by both builds (moved from windows/FishBowl.cs). Snapshots live in
    // <game library>/Game Saves/<game id>/<kind>/History/<snapshot id>, the same layout on Windows and Linux.
    public static class SafeFiles
    {
        public static string Under(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || (Platform.IsWindows && relative.Contains(":")))
            {
                throw new InvalidDataException("Invalid relative path.");
            }
            string fullPath = Path.GetFullPath(Path.Combine(root, relative));
            string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(value, Platform.PathComparison))
            {
                throw new InvalidDataException("Path escapes its destination.");
            }
            return fullPath;
        }

        public static bool Within(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(value, Platform.PathComparison);
        }

        public static void CheckLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked files/folders must be copied manually: " + path);
            }
        }

        public static List<string> Tree(string root, CancellationToken token)
        {
            CheckLink(root);
            List<string> list = new List<string>();
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string path = queue.Dequeue();
                foreach (string item in Directory.EnumerateDirectories(path))
                {
                    CheckLink(item);
                    queue.Enqueue(item);
                }
                foreach (string item2 in Directory.EnumerateFiles(path))
                {
                    CheckLink(item2);
                    list.Add(item2);
                    if (list.Count > 100000)
                    {
                        throw new IOException("This operation is limited to 100,000 files.");
                    }
                }
            }
            return list;
        }

        public static string HashFile(string path, CancellationToken token)
        {
            using (SHA256 sHA = SHA256.Create())
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] array = new byte[65536];
                    int inputCount;
                    while ((inputCount = fileStream.Read(array, 0, array.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        sHA.TransformBlock(array, 0, inputCount, array, 0);
                    }
                    sHA.TransformFinalBlock(new byte[0], 0, 0);
                    return BitConverter.ToString(sHA.Hash).Replace("-", "");
                }
            }
        }

        public static List<string> Folders(string root, CancellationToken token)
        {
            Tree(root, token);
            List<string> list = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).ToList();
            foreach (string item in list)
            {
                token.ThrowIfCancellationRequested();
                CheckLink(item);
            }
            return list;
        }

        public static string Hash(string path, CancellationToken token)
        {
            if (File.Exists(path))
            {
                CheckLink(path);
                return HashFile(path, token);
            }
            if (!Directory.Exists(path))
            {
                throw new FileNotFoundException("Source is missing.", path);
            }
            IEnumerable<string> second = from f in Tree(path, token).OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase)
                select f.Substring(path.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/') + "|" + new FileInfo(f).Length + "|" + HashFile(f, token);
            using (SHA256 sHA = SHA256.Create())
            {
                return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", (from f in Folders(path, token).OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase)
                    select "DIR|" + f.Substring(path.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/')).Concat(second))))).Replace("-", "");
            }
        }

        public static void CopyFile(string source, string destination, CancellationToken token)
        {
            CheckLink(source);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (FileStream fileStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fileStream2 = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] array = new byte[65536];
                    int count;
                    while ((count = fileStream.Read(array, 0, array.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        fileStream2.Write(array, 0, count);
                    }
                    fileStream2.Flush(true);
                }
            }
        }

        public static void CopyTree(string source, string destination, CancellationToken token)
        {
            if (File.Exists(source))
            {
                CopyFile(source, destination, token);
                return;
            }
            Directory.CreateDirectory(destination);
            foreach (string item in Folders(source, token))
            {
                Directory.CreateDirectory(Under(destination, item.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1)));
            }
            foreach (string item2 in Tree(source, token))
            {
                CopyFile(item2, Under(destination, item2.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1)), token);
            }
        }

        public static long Size(string path)
        {
            return File.Exists(path) ? new FileInfo(path).Length : (Directory.Exists(path) ? Tree(path, CancellationToken.None).Sum((string f) => new FileInfo(f).Length) : 0);
        }

        public static string Unique(string folder, string name)
        {
            string text = Path.Combine(folder, name);
            int num = 2;
            while (File.Exists(text) || Directory.Exists(text))
            {
                text = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + " (" + num++ + ")" + Path.GetExtension(name));
            }
            return text;
        }
    }
    public static partial class GameSaves
    {
        public static string Root(LibraryData library, GameEntry game, string kind)
        {
            if (kind != "In-game saves" && kind != "Save states")
            {
                throw new ArgumentException("Choose a save category.");
            }
            if (string.IsNullOrWhiteSpace(game.Id) || game.Id.Any((char ch) => !char.IsLetterOrDigit(ch)))
            {
                throw new ArgumentException("Invalid game identity.");
            }
            return Path.Combine(GameStorage.Root(library), "Game Saves", game.Id, kind);
        }

        public static void Link(GameEntry game, string path, string kind)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException("Save file or folder is unavailable.", path);
            }
            if (kind != "In-game saves" && kind != "Save states")
            {
                throw new ArgumentException("Choose a save category.");
            }
            if (game.Saves == null)
            {
                game.Saves = new List<GameSaveEntry>();
            }
            path = Path.GetFullPath(path);
            if (!game.Saves.Any((GameSaveEntry item) => string.Equals(item.Path, path, Platform.PathComparison)))
            {
                game.Saves.Add(new GameSaveEntry
                {
                    Path = path,
                    Kind = kind,
                    AddedAt = DateTime.UtcNow.ToString("o"),
                    IsFolder = Directory.Exists(path)
                });
            }
        }
    }
    public static partial class SaveHistory
    {
        // Platform hooks. The Windows build implements them in windows/FishBowl.cs (NextData/ExperienceData);
        // other builds use the portable defaults below.
        static partial void EnsureDataOverride(LibraryData library, ref bool handled);
        static partial void LaunchEmulatorOverride(LibraryData library, GameEntry game, ref EmulatorProfile result, ref bool handled);
        static partial void AssignedEmulatorOverride(LibraryData library, GameEntry game, ref EmulatorProfile result, ref bool handled);
        static partial void RequireClosedOverride(LibraryData library, SaveSnapshot snapshot, ref bool handled);

        public static void EnsureData(LibraryData library)
        {
            bool handled = false;
            EnsureDataOverride(library, ref handled);
            if (handled) return;
            if (library.Experience == null) library.Experience = new ExperienceSettings();
            if (library.Experience.PinnedBackupPaths == null) library.Experience.PinnedBackupPaths = new List<string>();
            if (library.SaveSnapshots == null) library.SaveSnapshots = new List<SaveSnapshot>();
            if (library.SaveReviews == null) library.SaveReviews = new List<SaveReviewItem>();
        }

        // The emulator assigned to a game (no per-game build override).
        public static EmulatorProfile AssignedEmulator(LibraryData library, GameEntry game)
        {
            EmulatorProfile result = null; bool handled = false;
            AssignedEmulatorOverride(library, game, ref result, ref handled);
            if (handled) return result;
            if (game == null || game.RequiresEmulatorAssignment || (game.Extras != null && game.Extras.Native)) return null;
            string id = string.IsNullOrWhiteSpace(game.PreferredEmulatorId) ? game.EmulatorId : game.PreferredEmulatorId;
            result = library.Emulators.FirstOrDefault(p => p.Id == id);
            return result ?? (library.Emulators.Count == 1 ? library.Emulators[0] : null);
        }

        // The emulator a launch would use, including the game's preferred installed build.
        public static EmulatorProfile LaunchEmulator(LibraryData library, GameEntry game)
        {
            EmulatorProfile result = null; bool handled = false;
            LaunchEmulatorOverride(library, game, ref result, ref handled);
            if (handled) return result;
            EmulatorProfile assigned = AssignedEmulator(library, game);
            if (assigned == null) return null;
            EmulatorBuild build = (assigned.Builds ?? new List<EmulatorBuild>()).FirstOrDefault(b => b.Id == game.PreferredBuildId);
            if (build == null || !File.Exists(build.Executable)) return assigned;
            EmulatorProfile copy = Json.Deserialize<EmulatorProfile>(Json.Serialize(assigned));
            copy.Executable = build.Executable;
            copy.ManualVersion = build.ManualVersion;
            return copy;
        }

        // Restoring over a save the emulator still has open would be lost or corrupted when it next writes.
        public static void RequireClosed(LibraryData library, SaveSnapshot snapshot)
        {
            bool handled = false;
            RequireClosedOverride(library, snapshot, ref handled);
            if (handled) return;
            GameEntry game = library.Games.FirstOrDefault(g => g.Id == snapshot.GameId);
            if (game != null && game.Extras != null && game.Extras.Native)
            {
                if (EmulatorRuntime.State(game.Path) != RuntimeState.Stopped) throw new IOException("Close the PC game before restoring. FishBowl cannot confirm it has exited.");
                return;
            }
            EmulatorProfile assigned = game == null ? null : LaunchEmulator(library, game);
            if (assigned == null) throw new IOException("Assign the game to a directly registered emulator before restoring.");
            var executables = library.Emulators.Where(p => p.Id == assigned.Id).SelectMany(p => new[] { p.Executable }.Concat((p.Builds ?? new List<EmulatorBuild>()).Where(b => File.Exists(b.Executable)).Select(b => b.Executable))).Concat(new[] { assigned.Executable }).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.Ordinal);
            foreach (string executable in executables)
            {
                RuntimeState state = EmulatorRuntime.State(executable);
                if (state == RuntimeState.Running) throw new IOException("Close " + assigned.Name + " and its registered builds before restoring a save.");
                if (state == RuntimeState.Unknown) throw new IOException("FishBowl cannot verify whether " + assigned.Name + " is closed. Close it and try again, or register its program directly.");
            }
        }

        public static SaveSnapshot Capture(LibraryData library, GameEntry game, string source, string kind, bool deduplicate, CancellationToken token)
        {
            EnsureData(library);
            source = Path.GetFullPath(source);
            string text = GameSaves.Root(library, game, kind);
            if (SafeFiles.Within(source, Path.Combine(GameStorage.Root(library), "Game Saves")))
            {
                throw new IOException("Choose an emulator's original save, not an existing library snapshot.");
            }
            if (Directory.Exists(source) && SafeFiles.Within(text, source))
            {
                throw new IOException("The selected save folder contains the FishBowl library. Choose only the game save folder.");
            }
            string hash = SafeFiles.Hash(source, token);
            if (deduplicate)
            {
                SaveSnapshot saveSnapshot = library.SaveSnapshots.Where((SaveSnapshot s) => s.GameId == game.Id && s.Kind == kind && string.Equals(s.Source, source, Platform.PathComparison) && s.Hash == hash).LastOrDefault((SaveSnapshot s) => File.Exists(s.Path) || Directory.Exists(s.Path));
                if (saveSnapshot != null)
                {
                    return saveSnapshot;
                }
            }
            string text2 = Guid.NewGuid().ToString("N");
            string text3 = Path.Combine(text, "History");
            Directory.CreateDirectory(text3);
            string text4 = Path.Combine(text3, ".pending-" + text2);
            string text5 = Path.Combine(text3, text2);
            Directory.CreateDirectory(text4);
            string fileName = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
            string text6 = SafeFiles.Under(text4, fileName);
            try
            {
                SafeFiles.CopyTree(source, text6, token);
                string text7 = SafeFiles.Hash(source, token);
                if (hash != text7 || SafeFiles.Hash(text6, token) != hash)
                {
                    throw new IOException("The save changed while copying. Retry after saving finishes.");
                }
                token.ThrowIfCancellationRequested();
                Directory.Move(text4, text5);
            }
            catch
            {
                if (Directory.Exists(text4) && SafeFiles.Within(text4, text3))
                {
                    Directory.Delete(text4, true);
                }
                throw;
            }
            EmulatorProfile emulatorProfile = LaunchEmulator(library, game);
            SaveSnapshot saveSnapshot2 = new SaveSnapshot();
            saveSnapshot2.Id = text2;
            saveSnapshot2.GameId = game.Id;
            saveSnapshot2.Source = source;
            saveSnapshot2.Path = Path.Combine(text5, fileName);
            saveSnapshot2.Kind = kind;
            saveSnapshot2.CreatedAt = DateTime.UtcNow.ToString("o");
            saveSnapshot2.Hash = hash;
            saveSnapshot2.Bytes = SafeFiles.Size(Path.Combine(text5, fileName));
            saveSnapshot2.IsFolder = Directory.Exists(source);
            saveSnapshot2.EmulatorId = ((emulatorProfile == null) ? null : emulatorProfile.Id);
            saveSnapshot2.EmulatorName = ((emulatorProfile == null) ? "Unassigned" : emulatorProfile.Name);
            saveSnapshot2.EmulatorVersion = ((emulatorProfile == null) ? "Unknown" : EmulatorReference.VersionFor(emulatorProfile));
            saveSnapshot2.Core = game.EmulatorCore;
            SaveSnapshot saveSnapshot3 = saveSnapshot2;
            File.WriteAllText(Path.Combine(text5, "snapshot.json"), Json.Serialize(saveSnapshot3));
            library.SaveSnapshots.Add(saveSnapshot3);
            GameSaves.Link(game, saveSnapshot3.Path, kind);
            library.Experience.LastSuccessfulBackup = saveSnapshot3.CreatedAt;
            Store.Log("Save snapshot created: " + game.Title + " / " + kind);
            return saveSnapshot3;
        }

        public static void Verify(SaveSnapshot snapshot, CancellationToken token)
        {
            if (SafeFiles.Hash(snapshot.Path, token) != snapshot.Hash)
            {
                throw new InvalidDataException("The snapshot's content no longer matches its recorded hash.");
            }
        }

        public static string Compatibility(LibraryData library, SaveSnapshot snapshot)
        {
            if (snapshot.Kind != "Save states")
            {
                return "In-game save";
            }
            GameEntry gameEntry = library.Games.FirstOrDefault((GameEntry g) => g.Id == snapshot.GameId);
            EmulatorProfile emulatorProfile = ((gameEntry == null) ? null : AssignedEmulator(library, gameEntry));
            if (emulatorProfile == null)
            {
                return "Assign an emulator before restoring this state.";
            }
            string text = EmulatorReference.VersionFor(emulatorProfile);
            List<string> list = new List<string>();
            if (emulatorProfile.Id != snapshot.EmulatorId)
            {
                list.Add("Assigned emulator differs from the recorded emulator.");
            }
            if (!string.Equals(text, snapshot.EmulatorVersion, StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Version differs: snapshot " + snapshot.EmulatorVersion + "; installed " + text);
            }
            if (!string.Equals(gameEntry.EmulatorCore ?? "", snapshot.Core ?? "", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Core differs: snapshot " + snapshot.Core + "; game " + gameEntry.EmulatorCore);
            }
            return (list.Count == 0) ? "Recorded emulator version and core match; compatibility still depends on the emulator." : string.Join(" ", list);
        }

        public static void Restore(LibraryData library, SaveSnapshot snapshot, CancellationToken token)
        {
            RequireClosed(library, snapshot);
            Verify(snapshot, token);
            string fullPath = Path.GetFullPath(snapshot.Source);
            if (SafeFiles.Within(fullPath, Path.Combine(GameStorage.Root(library), "Game Saves")))
            {
                throw new IOException("Restore target cannot be inside snapshot storage.");
            }
            GameEntry gameEntry = library.Games.Single((GameEntry g) => g.Id == snapshot.GameId);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                SaveSnapshot saveSnapshot = Capture(library, gameEntry, fullPath, snapshot.Kind, false, token);
                saveSnapshot.Note = "Before restoring " + snapshot.CreatedAt;
                saveSnapshot.Pinned = true;
            }
            string directoryName = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directoryName);
            SafeFiles.CheckLink(directoryName);
            string text = Path.Combine(directoryName, ".fishbowl-restore-" + Guid.NewGuid().ToString("N"));
            string text2 = Path.Combine(directoryName, ".fishbowl-before-" + Guid.NewGuid().ToString("N"));
            SafeFiles.CopyTree(snapshot.Path, text, token);
            if (SafeFiles.Hash(text, token) != snapshot.Hash)
            {
                throw new InvalidDataException("Restore staging verification failed.");
            }
            token.ThrowIfCancellationRequested();
            bool flag = File.Exists(fullPath) || Directory.Exists(fullPath);
            RequireClosed(library, snapshot);
            try
            {
                if (flag)
                {
                    if (Directory.Exists(fullPath))
                    {
                        Directory.Move(fullPath, text2);
                    }
                    else
                    {
                        File.Move(fullPath, text2);
                    }
                }
                if (snapshot.IsFolder)
                {
                    Directory.Move(text, fullPath);
                }
                else
                {
                    File.Move(text, fullPath);
                }
            }
            catch
            {
                if (flag && (File.Exists(text2) || Directory.Exists(text2)) && !File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    if (Directory.Exists(text2))
                    {
                        Directory.Move(text2, fullPath);
                    }
                    else
                    {
                        File.Move(text2, fullPath);
                    }
                }
                throw;
            }
            Store.Log("Save restored: " + gameEntry.Title + "; previous live save retained at " + (flag ? text2 : "no prior save"));
        }

        // File name (relative path for folders) to SHA-256 for a snapshot, or for a live save when Path = Source.
        public static Dictionary<string, string> SnapshotFiles(SaveSnapshot snapshot, CancellationToken token)
        {
            var files = new Dictionary<string, string>(SaveMonitor.PathComparer);
            if (snapshot.IsFolder)
            {
                if (Directory.Exists(snapshot.Path))
                    foreach (string file in SafeFiles.Tree(snapshot.Path, token))
                        files[file.Substring(snapshot.Path.TrimEnd('\\', '/').Length + 1)] = SafeFiles.HashFile(file, token);
            }
            else if (File.Exists(snapshot.Path))
                files[Path.GetFileName(snapshot.Source ?? snapshot.Path)] = SafeFiles.HashFile(snapshot.Path, token);
            return files;
        }

        // Added/Removed/Changed lines between two snapshots (before may be null).
        public static string[] SnapshotChanges(SaveSnapshot before, SaveSnapshot after, CancellationToken token)
        {
            Dictionary<string, string> old = before == null ? new Dictionary<string, string>(SaveMonitor.PathComparer) : SnapshotFiles(before, token);
            Dictionary<string, string> next = SnapshotFiles(after, token);
            return old.Keys.Union(next.Keys, SaveMonitor.PathComparer).OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => !old.ContainsKey(k) ? "Added: " + k : !next.ContainsKey(k) ? "Removed: " + k : old[k] != next[k] ? "Changed: " + k : null)
                .Where(k => k != null).ToArray();
        }

        public static long SnapshotStorage(LibraryData library)
        {
            return library.SaveSnapshots == null ? 0L : library.SaveSnapshots.Sum(s => s.Bytes);
        }

        // The previous snapshot of the same save, for "Compare previous".
        public static SaveSnapshot Previous(LibraryData library, SaveSnapshot after)
        {
            return library.SaveSnapshots.Where(s => s.GameId == after.GameId && s.Kind == after.Kind && s.Source == after.Source && string.CompareOrdinal(s.CreatedAt, after.CreatedAt) < 0).OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        }

        // What a restore would add, replace or remove in the live save (shown before restoring).
        public static string RestorePreview(SaveSnapshot snapshot, CancellationToken token)
        {
            Verify(snapshot, token);
            Dictionary<string, string> incoming = SnapshotFiles(snapshot, token);
            Dictionary<string, string> live = SnapshotFiles(new SaveSnapshot { Path = snapshot.Source, Source = snapshot.Source, IsFolder = snapshot.IsFolder }, token);
            var text = new StringBuilder("Restore destination: " + snapshot.Source + Environment.NewLine + "Existing contents will be backed up. Emulator must be closed." + Environment.NewLine + Environment.NewLine);
            foreach (KeyValuePair<string, string> item in incoming)
            {
                string current;
                text.AppendLine((!live.TryGetValue(item.Key, out current) ? "ADD" : current == item.Value ? "UNCHANGED" : "REPLACE") + " " + item.Key);
            }
            foreach (string key in live.Keys.Where(k => !incoming.ContainsKey(k)))
                text.AppendLine("REMOVE FROM LIVE (retained in rollback): " + key);
            return text.ToString();
        }

        // Exports every snapshot as a bundle into a folder kept by a sync client (the optional cloud backup).
        public static int ExportAll(LibraryData library, string folder, CancellationToken token, Action<string> progress)
        {
            EnsureData(library);
            if (!Directory.Exists(folder)) throw new IOException("Choose an existing sync folder.");
            int count = 0;
            foreach (SaveSnapshot snapshot in library.SaveSnapshots.ToList())
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(snapshot.GameId) || snapshot.GameId.Any(ch => !char.IsLetterOrDigit(ch))) continue;
                string target = Path.Combine(folder, "FishBowl Saves", snapshot.GameId);
                Directory.CreateDirectory(target);
                // Snapshot ids never change content, so a bundle already exported for this id is skipped.
                if (File.Exists(Path.Combine(target, snapshot.Id + ".fishbowl-save.zip"))) continue;
                string file = Path.Combine(target, snapshot.Id + ".fishbowl-save.zip");
                if (progress != null) progress(file);
                Export(library, snapshot, file, token);
                count++;
            }
            library.Experience.CloudFolder = folder;
            library.Experience.LastCloudBackup = DateTime.UtcNow.ToString("o");
            return count;
        }

        // Folder summary for the storage dashboard (LibraryJobs.Storage in the Windows build).
        public static string StorageSummary(string label, string folder, CancellationToken token)
        {
            if (!Directory.Exists(folder)) return label + "\nNot created yet\n" + folder;
            List<string> files = SafeFiles.Tree(folder, token);
            long bytes = 0;
            foreach (string file in files) { token.ThrowIfCancellationRequested(); bytes += new FileInfo(file).Length; }
            string free = "Unavailable";
            try { free = Bytes(new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace); } catch (Exception) { }
            return label + "\n" + folder + "\n" + files.Count + " files / " + Bytes(bytes) + " / free " + free;
        }

        public static string Bytes(long bytes)
        {
            return bytes < 1048576 ? Math.Max(1L, bytes / 1024) + " KB" : bytes < 1073741824 ? (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB" : (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + " GB";
        }

        // ----- Scheduled snapshot export (NextTools.BackupPlanner / RunScheduledBackup) -----

        public static string PlannedFolder(LibraryData library) { return Path.Combine(HubPaths.BackupRoot(library), "Scheduled snapshots"); }

        // Average snapshot growth over the last 30 days, projected over the given number of days.
        public static long EstimatedGrowth(LibraryData library, int days)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-30.0);
            long recent = library.SaveSnapshots.Where(s => { DateTime created; return DateTime.TryParse(s.CreatedAt, out created) && created.ToUniversalTime() >= cutoff; }).Sum(s => s.Bytes);
            return (long)(recent / 30.0 * Math.Max(0, days));
        }

        public static bool PlannedExportDue(LibraryData library, DateTime utcNow)
        {
            DateTime next;
            return library.Enhancements != null && library.Enhancements.BackupIntervalDays > 0 && DateTime.TryParse(library.Enhancements.NextBackupAt, out next) && next.ToUniversalTime() <= utcNow;
        }

        // Exports snapshots not yet in the scheduled-export folder, within the quota (in MB). Returns the number exported.
        public static int ExportPlanned(LibraryData library, long quotaMegabytes, CancellationToken token, Action<string> progress)
        {
            string folder = PlannedFolder(library);
            Directory.CreateDirectory(folder);
            long limit = Math.Max(1L, quotaMegabytes) * 1024 * 1024;
            long used = Directory.GetFiles(folder, "*.zip").Sum(f => new FileInfo(f).Length);
            int count = 0;
            foreach (SaveSnapshot snapshot in library.SaveSnapshots.ToArray())
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(snapshot.Id) || snapshot.Id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) throw new IOException("Snapshot ID is invalid.");
                string target = Path.Combine(folder, snapshot.Id + ".zip");
                if (File.Exists(target)) continue;
                if (used + snapshot.Bytes > limit) throw new IOException("The scheduled export quota would be exceeded. Increase the quota or review old exports.");
                if (progress != null) progress("Export " + snapshot.CreatedAt);
                Export(library, snapshot, target, token);
                long length = new FileInfo(target).Length;
                if (used + length > limit) { File.Delete(target); throw new IOException("This new archive exceeds the scheduled export quota."); }
                used += length; count++;
            }
            return count;
        }

        // Records a finished scheduled export and the next due time.
        public static void PlannedExportDone(LibraryData library)
        {
            NextSettings settings = library.Enhancements;
            settings.LastPlannedBackup = DateTime.Now.ToString("g");
            settings.NextBackupAt = settings.BackupIntervalDays <= 0 ? null : DateTime.UtcNow.AddDays(settings.BackupIntervalDays).ToString("o");
        }

        public static List<SaveSnapshot> CleanupCandidates(LibraryData library, GameEntry game)
        {
            EnsureData(library);
            ExperienceSettings experience = library.Experience;
            List<SaveSnapshot> list = (from s in library.SaveSnapshots
                where s.GameId == game.Id
                orderby s.CreatedAt descending
                select s).ToList();
            List<SaveSnapshot> list2 = new List<SaveSnapshot>();
            long num = 0L;
            int num2 = 0;
            foreach (SaveSnapshot item in list)
            {
                num += item.Bytes;
                num2++;
                DateTime result;
                bool flag = DateTime.TryParse(item.CreatedAt, out result) && experience.SnapshotDays > 0 && result.ToUniversalTime() < DateTime.UtcNow.AddDays(-experience.SnapshotDays);
                if (!item.Pinned && (flag || (experience.SnapshotCount > 0 && num2 > experience.SnapshotCount) || (experience.SnapshotMegabytes > 0 && num > experience.SnapshotMegabytes * 1024 * 1024)))
                {
                    list2.Add(item);
                }
            }
            return list2;
        }

        public static void Remove(LibraryData library, SaveSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Id) || snapshot.Id.Any((char ch) => !char.IsLetterOrDigit(ch)))
            {
                throw new IOException("Invalid snapshot identity.");
            }
            string text = Path.Combine(GameSaves.Root(library, library.Games.Single((GameEntry g) => g.Id == snapshot.GameId), snapshot.Kind), "History", snapshot.Id);
            if (!SafeFiles.Within(snapshot.Path, text) || !Directory.Exists(text))
            {
                throw new IOException("Only this game's managed snapshots can be removed.");
            }
            SafeFiles.Tree(text, CancellationToken.None);
            Directory.Delete(text, true);
            library.SaveSnapshots.Remove(snapshot);
            foreach (GameEntry game in library.Games)
            {
                if (game.Saves != null)
                {
                    game.Saves.RemoveAll((GameSaveEntry s) => s.Path == snapshot.Path);
                }
            }
        }

        public static void Export(LibraryData library, SaveSnapshot snapshot, string output, CancellationToken token)
        {
            Verify(snapshot, token);
            if (snapshot.IsFolder && SafeFiles.Within(output, snapshot.Path))
            {
                throw new IOException("Export bundles outside the snapshot folder.");
            }
            if (File.Exists(output))
            {
                throw new IOException("Choose a new archive filename.");
            }
            string text = output + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                using (ZipArchive zipArchive = ZipFile.Open(text, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry("snapshot.json");
                    using (StreamWriter streamWriter = new StreamWriter(zipArchiveEntry.Open()))
                    {
                        streamWriter.Write(Json.Serialize(snapshot));
                    }
                    ZipArchiveEntry zipArchiveEntry2 = zipArchive.CreateEntry("game.json");
                    using (StreamWriter streamWriter = new StreamWriter(zipArchiveEntry2.Open()))
                    {
                        streamWriter.Write(Json.Serialize(library.Games.Single((GameEntry g) => g.Id == snapshot.GameId)));
                    }
                    if (snapshot.IsFolder)
                    {
                        foreach (string item in SafeFiles.Folders(snapshot.Path, token))
                        {
                            zipArchive.CreateEntry("data/" + item.Substring(snapshot.Path.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/') + "/");
                        }
                        foreach (string item2 in SafeFiles.Tree(snapshot.Path, token))
                        {
                            token.ThrowIfCancellationRequested();
                            zipArchive.CreateEntryFromFile(item2, "data/" + item2.Substring(snapshot.Path.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/'));
                        }
                    }
                    else
                    {
                        zipArchive.CreateEntryFromFile(snapshot.Path, "data/" + Path.GetFileName(snapshot.Path));
                    }
                }
                File.Move(text, output);
            }
            catch
            {
                if (File.Exists(text))
                {
                    File.Delete(text);
                }
                throw;
            }
        }

        public static SaveSnapshot Import(LibraryData library, GameEntry game, string archivePath, CancellationToken token)
        {
            string text = Path.Combine(GameSaves.Root(library, game, "In-game saves"), ".import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(text);
            try
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(archivePath))
                {
                    ZipArchiveEntry entry = zipArchive.GetEntry("snapshot.json");
                    if (entry == null || entry.Length > 1048576)
                    {
                        throw new InvalidDataException("Missing or oversized snapshot manifest.");
                    }
                    SaveSnapshot saveSnapshot;
                    using (StreamReader streamReader = new StreamReader(entry.Open()))
                    {
                        saveSnapshot = Json.Deserialize<SaveSnapshot>(streamReader.ReadToEnd());
                    }
                    if (saveSnapshot == null || (saveSnapshot.Kind != "In-game saves" && saveSnapshot.Kind != "Save states"))
                    {
                        throw new InvalidDataException("Invalid snapshot type.");
                    }
                    List<ZipArchiveEntry> list = zipArchive.Entries.Where((ZipArchiveEntry e) => e.FullName.StartsWith("data/", StringComparison.Ordinal)).ToList();
                    if (list.Count > 100000 || list.Sum((ZipArchiveEntry e) => e.Length) > 10737418240L)
                    {
                        throw new InvalidDataException("Archive exceeds import limits.");
                    }
                    HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (ZipArchiveEntry item in list)
                    {
                        token.ThrowIfCancellationRequested();
                        string text2 = item.FullName.Substring(5);
                        if (string.IsNullOrEmpty(text2))
                        {
                            continue;
                        }
                        if (text2.EndsWith("/"))
                        {
                            Directory.CreateDirectory(SafeFiles.Under(text, text2.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar)));
                            continue;
                        }
                        string text3 = SafeFiles.Under(text, text2.Replace('/', Path.DirectorySeparatorChar));
                        if (!hashSet.Add(text3))
                        {
                            throw new InvalidDataException("Duplicate archive path.");
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(text3));
                        using (Stream stream = item.Open())
                        {
                            using (FileStream fileStream = new FileStream(text3, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            {
                                byte[] array = new byte[65536];
                                long num = 0L;
                                int num2;
                                while ((num2 = stream.Read(array, 0, array.Length)) > 0)
                                {
                                    token.ThrowIfCancellationRequested();
                                    num += num2;
                                    if (num > item.Length)
                                    {
                                        throw new InvalidDataException("Archive entry exceeds its declared size.");
                                    }
                                    fileStream.Write(array, 0, num2);
                                }
                                if (num != item.Length)
                                {
                                    throw new InvalidDataException("Archive entry size is inconsistent.");
                                }
                            }
                        }
                    }
                    string text4 = (saveSnapshot.IsFolder ? text : SafeFiles.Tree(text, token).Single());
                    if (SafeFiles.Hash(text4, token) != saveSnapshot.Hash)
                    {
                        throw new InvalidDataException("Archive content hash does not match.");
                    }
                    string source = text4;
                    return CaptureImported(library, game, source, saveSnapshot, token);
                }
            }
            finally
            {
                if (Directory.Exists(text) && SafeFiles.Within(text, GameSaves.Root(library, game, "In-game saves")))
                {
                    Directory.Delete(text, true);
                }
            }
        }

        private static SaveSnapshot CaptureImported(LibraryData library, GameEntry game, string source, SaveSnapshot original, CancellationToken token)
        {
            EnsureData(library);
            string text = Guid.NewGuid().ToString("N");
            string path = Path.Combine(GameSaves.Root(library, game, original.Kind), "History", text);
            string text2 = Path.Combine(path, original.IsFolder ? "Imported save" : Path.GetFileName(source));
            SafeFiles.CopyTree(source, text2, token);
            SaveSnapshot saveSnapshot = new SaveSnapshot();
            saveSnapshot.Id = text;
            saveSnapshot.GameId = game.Id;
            saveSnapshot.Source = null;
            saveSnapshot.Path = text2;
            saveSnapshot.Kind = original.Kind;
            saveSnapshot.CreatedAt = DateTime.UtcNow.ToString("o");
            saveSnapshot.Hash = original.Hash;
            saveSnapshot.Bytes = SafeFiles.Size(text2);
            saveSnapshot.IsFolder = original.IsFolder;
            saveSnapshot.EmulatorId = original.EmulatorId;
            saveSnapshot.EmulatorName = original.EmulatorName;
            saveSnapshot.EmulatorVersion = original.EmulatorVersion;
            saveSnapshot.Core = original.Core;
            saveSnapshot.Note = "Imported; choose a local restore destination before restoring.";
            SaveSnapshot saveSnapshot2 = saveSnapshot;
            library.SaveSnapshots.Add(saveSnapshot2);
            GameSaves.Link(game, text2, saveSnapshot2.Kind);
            return saveSnapshot2;
        }
    }
    public class SaveChangeTracker
    {
        private class Sample
        {
            public string Stamp;

            public DateTime Since;
        }

        private readonly Dictionary<string, Sample> waiting = new Dictionary<string, Sample>(SaveMonitor.PathComparer);

        private readonly Dictionary<string, string> notified = new Dictionary<string, string>(SaveMonitor.PathComparer);

        public void Queue(string path, DateTime now)
        {
            if (waiting.Count < 500 || waiting.ContainsKey(path))
            {
                waiting[path] = new Sample
                {
                    Since = now
                };
            }
        }

        public List<string> Ready(DateTime now)
        {
            List<string> list = new List<string>();
            string[] array = waiting.Keys.ToArray();
            string[] array2 = array;
            foreach (string text in array2)
            {
                Sample sample = waiting[text];
                if (now - sample.Since > TimeSpan.FromMinutes(2.0))
                {
                    waiting.Remove(text);
                    continue;
                }
                try
                {
                    FileInfo fileInfo = new FileInfo(text);
                    if (!fileInfo.Exists)
                    {
                        waiting.Remove(text);
                        continue;
                    }
                    string text2 = fileInfo.Length + ":" + fileInfo.LastWriteTimeUtc.Ticks;
                    if (sample.Stamp != text2)
                    {
                        sample.Stamp = text2;
                        sample.Since = now;
                    }
                    else if (!(now - sample.Since < TimeSpan.FromSeconds(3.0)))
                    {
                        using (new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                        }
                        string value;
                        if (!notified.TryGetValue(text, out value) || value != text2)
                        {
                            list.Add(text);
                            notified[text] = text2;
                        }
                        waiting.Remove(text);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                    waiting.Remove(text);
                }
            }
            if (notified.Count > 5000)
            {
                notified.Clear();
            }
            return list;
        }

        public void Clear()
        {
            waiting.Clear();
            notified.Clear();
        }
    }

    // Decides which changed files become "changed in-game save" reviews (the Windows MainForm rules, shared so
    // Linux behaves the same). The UI owns the file watchers and the review dialogs.
    public static class SaveMonitor
    {
        public static StringComparer PathComparer { get { return Platform.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal; } }
        private static readonly string[] IgnoredExtensions = { ".tmp", ".temp", ".bak", ".log", ".lock", ".p2s", ".ppst" };

        public static string ManagedRoot(LibraryData library) { return Path.Combine(GameStorage.Root(library), "Game Saves"); }

        public static bool IsManaged(LibraryData library, string path)
        {
            string root = Path.GetFullPath(ManagedRoot(library)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, Platform.PathComparison);
        }

        // Folders to watch, each with the games whose saves may live there. detected maps emulator id to its
        // detected in-game save folders (EmulatorFolderDetector, run off the UI thread by the caller).
        public static Dictionary<string, HashSet<string>> WatchRoots(LibraryData library, Dictionary<string, List<string>> detected)
        {
            var roots = new Dictionary<string, HashSet<string>>(PathComparer);
            Action<string, string> add = delegate(string folder, string gameId)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder) || IsManaged(library, folder)) return;
                folder = Path.GetFullPath(folder);
                HashSet<string> games;
                if (!roots.TryGetValue(folder, out games)) { games = new HashSet<string>(); roots.Add(folder, games); }
                if (!string.IsNullOrWhiteSpace(gameId)) games.Add(gameId);
            };
            foreach (GameEntry game in library.Games)
                foreach (GameSaveEntry save in (game.Saves ?? new List<GameSaveEntry>()).Where(s => s.Kind == "In-game saves" && !string.IsNullOrWhiteSpace(s.Path)))
                {
                    try { if (!IsManaged(library, save.Path)) add(Directory.Exists(save.Path) ? save.Path : Path.GetDirectoryName(save.Path), game.Id); }
                    catch (Exception error) { Store.Log("Save watch path skipped: " + error.Message); }
                }
            if (detected == null) return roots;
            if (library.Games.Count > 0)
                foreach (List<string> folders in detected.Values)
                    foreach (string folder in folders) add(folder, null);
            foreach (GameEntry game in library.Games)
            {
                string id = string.IsNullOrWhiteSpace(game.PreferredEmulatorId) ? game.EmulatorId : game.PreferredEmulatorId;
                if (string.IsNullOrWhiteSpace(id) && library.Emulators.Count == 1) id = library.Emulators[0].Id;
                List<string> folders;
                if (id != null && detected.TryGetValue(id, out folders))
                    foreach (string folder in folders) add(folder, game.Id);
            }
            return roots;
        }

        // Filters watcher events: temporary files, save states and FishBowl's own restore staging are ignored.
        public static bool ShouldQueue(LibraryData library, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || IsManaged(library, path)) return false;
            if (Path.GetFileName(path).StartsWith(".fishbowl-", StringComparison.Ordinal) || path.Contains(Path.DirectorySeparatorChar + ".fishbowl-")) return false;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (IgnoredExtensions.Contains(extension) || extension.StartsWith(".state", StringComparison.OrdinalIgnoreCase)) return false;
            return !library.Games.Any(g => (g.Saves ?? new List<GameSaveEntry>()).Any(s => s.Kind == "Save states" && string.Equals(s.Path, path, Platform.PathComparison)));
        }

        // A change inside a linked save folder belongs to that folder (the longest linked folder wins).
        public static string OriginalGroup(LibraryData library, string path)
        {
            GameSaveEntry folder = library.Games.SelectMany(g => g.Saves ?? new List<GameSaveEntry>())
                .Where(s => s.Kind == "In-game saves" && Directory.Exists(s.Path) && SafeFiles.Within(path, s.Path) && !IsManaged(library, s.Path))
                .OrderByDescending(s => s.Path.Length).FirstOrDefault();
            return folder == null ? path : folder.Path;
        }

        public static List<GameEntry> Suggestions(LibraryData library, IEnumerable<string> paths)
        {
            return library.Games.Where(g => paths.Any(p => (!string.IsNullOrWhiteSpace(g.Title) && Path.GetFileNameWithoutExtension(p).IndexOf(g.Title, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(g.TitleId) && p.IndexOf(g.TitleId, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
        }

        // Adds a settled change to the review queue. Returns false when it was not queued (the game's preference
        // is "Never ask", or 100 groups already wait).
        public static bool Record(LibraryData library, string changed)
        {
            SaveHistory.EnsureData(library);
            string source = OriginalGroup(library, changed);
            if (MatchesLatestSnapshot(library, source)) return false;
            List<GameEntry> owners = library.Games.Where(g => (g.Saves ?? new List<GameSaveEntry>()).Any(s => s.Kind == "In-game saves" && string.Equals(s.Path, source, Platform.PathComparison))).ToList();
            if (owners.Count == 1 && owners[0].SaveCopyPreference == "Never ask") return false;
            string gameId = owners.Count == 1 ? owners[0].Id : null;
            List<GameEntry> suggested = Suggestions(library, new[] { source });
            string key = gameId ?? (suggested.Count == 1 ? "suggested:" + suggested[0].Id : source);
            SaveReviewItem review = library.SaveReviews.FirstOrDefault(r => r.GameId == gameId && r.Path == key);
            if (review == null)
            {
                if (library.SaveReviews.Count >= 100) return false;
                review = new SaveReviewItem { Path = key, GameId = gameId, ChangedAt = DateTime.UtcNow.ToString("o"), Files = new List<string>() };
                library.SaveReviews.Add(review);
            }
            if (review.Files == null) review.Files = new List<string>();
            if (!review.Files.Contains(source, PathComparer)) review.Files.Add(source);
            review.ChangedAt = DateTime.UtcNow.ToString("o");
            return true;
        }

        // True when a small save file already equals its newest snapshot, as right after a restore or a copy,
        // so FishBowl does not ask to copy what it just wrote.
        public static bool MatchesLatestSnapshot(LibraryData library, string source)
        {
            try
            {
                if (!File.Exists(source) || new FileInfo(source).Length > 64L * 1024 * 1024) return false;
                SaveSnapshot latest = library.SaveSnapshots.Where(s => !s.IsFolder && string.Equals(s.Source, source, Platform.PathComparison)).OrderByDescending(s => s.CreatedAt, StringComparer.Ordinal).FirstOrDefault();
                return latest != null && latest.Hash == SafeFiles.Hash(source, CancellationToken.None);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        // A review whose game asked for "Automatic copies" and whose files are all that game's linked originals.
        public static SaveReviewItem AutomaticCandidate(LibraryData library)
        {
            SaveHistory.EnsureData(library);
            return library.SaveReviews.FirstOrDefault(r =>
            {
                if (r.GameId == null || r.Files == null) return false;
                GameEntry game = library.Games.FirstOrDefault(g => g.Id == r.GameId);
                return game != null && game.SaveCopyPreference == "Automatic copies" && r.Files.All(f => (game.Saves ?? new List<GameSaveEntry>()).Any(s => s.Kind == "In-game saves" && string.Equals(s.Path, f, Platform.PathComparison)));
            });
        }

        // Copies a reviewed group into the game's history and links each source (the "Copy snapshots" action).
        public static List<SaveSnapshot> CopyGroup(LibraryData library, GameEntry game, IEnumerable<string> sources, CancellationToken token)
        {
            var copies = new List<SaveSnapshot>();
            foreach (string path in sources)
            {
                token.ThrowIfCancellationRequested();
                copies.Add(SaveHistory.Capture(library, game, path, "In-game saves", true, token));
                GameSaves.Link(game, path, "In-game saves");
            }
            return copies;
        }

        // Quiet hours and snooze (ExperienceData.Quiet in the Windows build).
        public static bool Quiet(ExperienceSettings settings, DateTime now)
        {
            if (settings == null) return false;
            DateTime until;
            if (DateTime.TryParse(settings.SnoozeUntil, out until) && now.ToUniversalTime() < until.ToUniversalTime()) return true;
            int start = settings.QuietStartHour, end = settings.QuietEndHour;
            if (start < 0 || end < 0 || start == end) return false;
            return start < end ? (now.Hour >= start && now.Hour < end) : (now.Hour >= start || now.Hour < end);
        }
    }

    // Scheduled capture of changed linked saves (moved from windows/FishBowl.cs; the schedule dialog stays per platform).
    public class ScheduledSaveResult
    {
        public List<SaveSnapshot> Snapshots = new List<SaveSnapshot>();

        public List<string> Messages = new List<string>();
    }
    public static partial class HubSaveSchedule
    {
        public static ScheduledSaveResult Capture(LibraryData copy, CancellationToken token)
        {
            return Capture(copy, token, null);
        }

        public static ScheduledSaveResult Capture(LibraryData copy, CancellationToken token, Action<string> progress)
        {
            ScheduledSaveResult scheduledSaveResult = new ScheduledSaveResult();
            string managed = Path.Combine(GameStorage.Root(copy), "Game Saves");
            try
            {
                string hash;
                foreach (GameEntry g in copy.Games)
                {
                    token.ThrowIfCancellationRequested();
                    if (!(g.Saves ?? new List<GameSaveEntry>()).Any((GameSaveEntry link) => !string.IsNullOrWhiteSpace(link.Path) && !SafeFiles.Within(link.Path, managed)))
                    {
                        continue;
                    }
                    EmulatorProfile emulatorProfile = SaveHistory.LaunchEmulator(copy, g);
                    if (emulatorProfile == null || !Platform.IsDirectProgram(emulatorProfile.Executable) || EmulatorRuntime.State(emulatorProfile.Executable) != RuntimeState.Stopped)
                    {
                        scheduledSaveResult.Messages.Add(g.Title + ": skipped; a closed executable could not be verified.");
                        continue;
                    }
                    try
                    {
                        SaveHistory.RequireClosed(copy, new SaveSnapshot
                        {
                            GameId = g.Id
                        });
                    }
                    catch (Exception ex)
                    {
                        scheduledSaveResult.Messages.Add(g.Title + ": " + ex.Message);
                        continue;
                    }
                    GameSaveEntry[] array = (g.Saves ?? new List<GameSaveEntry>()).ToArray();
                    foreach (GameSaveEntry link2 in array)
                    {
                        token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(link2.Path) || SafeFiles.Within(link2.Path, managed))
                        {
                            continue;
                        }
                        if (!File.Exists(link2.Path) && !Directory.Exists(link2.Path))
                        {
                            scheduledSaveResult.Messages.Add(g.Title + ": linked save missing.");
                            continue;
                        }
                        try
                        {
                            hash = SafeFiles.Hash(link2.Path, token);
                            if (copy.SaveSnapshots.Any((SaveSnapshot s) => s.GameId == g.Id && s.Source == link2.Path && s.Kind == link2.Kind && s.Hash == hash))
                            {
                                scheduledSaveResult.Messages.Add(g.Title + ": unchanged save skipped.");
                                continue;
                            }
                            SaveSnapshot saveSnapshot = SaveHistory.Capture(copy, g, link2.Path, link2.Kind, false, token);
                            saveSnapshot.Note = "Scheduled linked-save capture";
                            scheduledSaveResult.Snapshots.Add(saveSnapshot);
                            scheduledSaveResult.Messages.Add(g.Title + ": verified save captured.");
                            if (progress != null)
                            {
                                progress(g.Title + ": verified save captured.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            scheduledSaveResult.Messages.Add(g.Title + ": " + ex.Message);
                        }
                    }
                }
                token.ThrowIfCancellationRequested();
                return scheduledSaveResult;
            }
            catch (OperationCanceledException)
            {
                foreach (SaveSnapshot snapshot in scheduledSaveResult.Snapshots)
                {
                    SaveHistory.Remove(copy, snapshot);
                }
                throw;
            }
        }

        public static void Apply(LibraryData d, ScheduledSaveResult result)
        {
            foreach (SaveSnapshot s in result.Snapshots)
            {
                List<SaveSnapshot> saveSnapshots = d.SaveSnapshots;
                Func<SaveSnapshot, bool> predicate = (SaveSnapshot x) => x.Id == s.Id;
                if (!saveSnapshots.Any(predicate))
                {
                    d.SaveSnapshots.Add(s);
                    GameEntry gameEntry = d.Games.FirstOrDefault((GameEntry x) => x.Id == s.GameId);
                    if (gameEntry != null)
                    {
                        GameSaves.Link(gameEntry, s.Path, s.Kind);
                    }
                }
            }
            if (result.Snapshots.Count > 0)
            {
                d.Experience.LastSuccessfulBackup = DateTime.UtcNow.ToString("o");
            }
            if (d.Hub == null) d.Hub = new HubSettings();
            d.Hub.LastCaptureReport = string.Join(Environment.NewLine, result.Messages);
        }
    }

    // Re-reads an emulator backup archive and checks every file against its manifest hash (moved from windows/FishBowl.cs).
    public static class BackupIntegrity
    {
        public static BackupManifest Verify(string path, CancellationToken token)
        {
            using (ZipArchive zipArchive = ZipFile.OpenRead(path))
            {
                BackupManifest backupManifest = EmulatorBackups.ReadManifest(zipArchive);
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (BackupFile file in backupManifest.Files)
                {
                    token.ThrowIfCancellationRequested();
                    if (!hashSet.Add(file.ArchivePath))
                    {
                        throw new InvalidDataException("Duplicate backup path.");
                    }
                    ZipArchiveEntry entry = zipArchive.GetEntry(file.ArchivePath);
                    if (entry == null || entry.Length != file.Size)
                    {
                        throw new InvalidDataException("Backup entry is missing or has the wrong size.");
                    }
                    using (Stream stream = entry.Open())
                    {
                        using (SHA256 sHA = SHA256.Create())
                        {
                            byte[] array = new byte[65536];
                            int inputCount;
                            while ((inputCount = stream.Read(array, 0, array.Length)) > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                sHA.TransformBlock(array, 0, inputCount, array, 0);
                            }
                            sHA.TransformFinalBlock(new byte[0], 0, 0);
                            if (!BitConverter.ToString(sHA.Hash).Replace("-", "").Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidDataException("Backup content hash mismatch.");
                            }
                        }
                    }
                }
                return backupManifest;
            }
        }
    }

    // Emulator backup archives and their retention (ExperienceTools.Backups in the Windows build).
    public static class BackupRetention
    {
        // Every .zip under the backup folder, newest first.
        public static List<string> Archives(LibraryData library, CancellationToken token)
        {
            string root = HubPaths.BackupRoot(library);
            if (!Directory.Exists(root)) return new List<string>();
            return SafeFiles.Tree(root, token).Where(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        }

        // Unpinned archives beyond the configured count (Experience.BackupArchiveCount; 0 keeps everything).
        public static List<string> CleanupCandidates(LibraryData library, IEnumerable<string> archivesNewestFirst)
        {
            SaveHistory.EnsureData(library);
            int keep = library.Experience.BackupArchiveCount;
            if (keep <= 0) return new List<string>();
            return archivesNewestFirst.Skip(keep).Where(p => !library.Experience.PinnedBackupPaths.Contains(p)).ToList();
        }

        public static void Delete(LibraryData library, IEnumerable<string> files)
        {
            string root = HubPaths.BackupRoot(library);
            foreach (string file in files)
            {
                if (!SafeFiles.Within(file, root)) throw new IOException("Cleanup is limited to configured backup folders.");
                SafeFiles.CheckLink(file);
                File.Delete(file);
            }
        }
    }

    // Backup reminders from existing data only: an emulator is due when one of its games was played (PlaySessions)
    // after its newest save backup and that backup is older than Theme.AutoBackupDays ("Backup reminder interval").
    // An emulator played but never backed up is due as well; one never played through the library is not.
    public static class BackupReminders
    {
        public static readonly string[] SaveCategories = { "InGameSaveFolder", "SaveStateFolder" };

        public static int IntervalDays(LibraryData library) { return library.Theme == null || library.Theme.AutoBackupDays <= 0 ? 7 : library.Theme.AutoBackupDays; }

        private static bool Moment(string text, out DateTime utc)
        {
            return DateTime.TryParse(text ?? "", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out utc);
        }

        // Emulator id to the newest backup archive (by manifest) that contains in-game saves or save states.
        public static Dictionary<string, DateTime> LastBackups(LibraryData library, CancellationToken token)
        {
            var newest = new Dictionary<string, DateTime>();
            foreach (string archive in BackupRetention.Archives(library, token))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    BackupManifest manifest;
                    using (ZipArchive zip = ZipFile.OpenRead(archive)) manifest = EmulatorBackups.ReadManifest(zip);
                    var saveRoots = new HashSet<string>(manifest.Roots.Where(r => SaveCategories.Contains(r.Category)).Select(r => r.Key));
                    DateTime created;
                    if (string.IsNullOrWhiteSpace(manifest.EmulatorId) || !manifest.Files.Any(f => saveRoots.Contains(f.RootKey)) || !Moment(manifest.CreatedAt, out created)) continue;
                    DateTime known;
                    if (!newest.TryGetValue(manifest.EmulatorId, out known) || created > known) newest[manifest.EmulatorId] = created;
                }
                catch (Exception error)
                {
                    if (error is OperationCanceledException) throw;
                    // Not a FishBowl emulator backup (scheduled snapshot exports live here too).
                }
            }
            return newest;
        }

        // Emulator id to the end of the latest play session of one of its games.
        public static Dictionary<string, DateTime> LastPlayed(LibraryData library)
        {
            var played = new Dictionary<string, DateTime>();
            foreach (PlaySession session in library.PlaySessions ?? new List<PlaySession>())
            {
                GameEntry game = library.Games.FirstOrDefault(g => g.Id == session.GameId);
                EmulatorProfile emulator = game == null ? null : SaveHistory.AssignedEmulator(library, game);
                DateTime when;
                if (emulator == null || !(Moment(session.EndedAt, out when) || Moment(session.StartedAt, out when))) continue;
                DateTime known;
                if (!played.TryGetValue(emulator.Id, out known) || when > known) played[emulator.Id] = when;
            }
            return played;
        }

        public static List<EmulatorProfile> Due(LibraryData library, Dictionary<string, DateTime> lastBackups, Dictionary<string, DateTime> lastPlayed, DateTime utcNow)
        {
            var due = new List<EmulatorProfile>();
            foreach (EmulatorProfile emulator in library.Emulators)
            {
                DateTime played, backedUp;
                if (emulator.Id == null || !lastPlayed.TryGetValue(emulator.Id, out played)) continue;
                if (!lastBackups.TryGetValue(emulator.Id, out backedUp) || (played > backedUp && utcNow - backedUp >= TimeSpan.FromDays(IntervalDays(library)))) due.Add(emulator);
            }
            return due.OrderBy(e => e.Name ?? "", StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // The status-bar notice, or null when nothing is due.
        public static string Notice(List<EmulatorProfile> due, Dictionary<string, DateTime> lastBackups)
        {
            if (due.Count == 0) return null;
            DateTime backedUp;
            string text = due.Count > 1 ? Names(due) + " have been played since their last save backups"
                : lastBackups.TryGetValue(due[0].Id, out backedUp) ? due[0].Name + " has been played since its last save backup on " + backedUp.ToLocalTime().ToString("d")
                : due[0].Name + " has been played but its saves are not backed up yet";
            return "Backup reminder: " + text + ". Use File > Game saves > Back up due emulators now.";
        }

        private static string Names(List<EmulatorProfile> profiles)
        {
            var names = profiles.Select(p => p.Name ?? "Emulator").ToList();
            if (names.Count > 3) return names[0] + ", " + names[1] + " and " + (names.Count - 2) + " more";
            return string.Join(", ", names.Take(names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
        }

        // Backs up one emulator's in-game saves and save states, verifies the archive and returns a one-line result.
        // Running emulators are skipped; errors are returned, not thrown, so a batch continues.
        public static string BackUp(LibraryData library, EmulatorProfile profile, CancellationToken token)
        {
            try
            {
                if (EmulatorRuntime.State(profile.Executable) == RuntimeState.Running) return profile.Name + ": skipped while it is running. Close it, then try again.";
                BackupPlan plan = EmulatorBackups.Preview(profile, SaveCategories, token);
                if (plan.Files.Count == 0) return profile.Name + ": no in-game saves or save states were found. Check its save folders.";
                string archive = EmulatorBackups.Create(profile, plan, HubPaths.BackupRoot(library), token);
                BackupIntegrity.Verify(archive, token);
                Store.Log("Backup of " + profile.Name + ": " + archive);
                return profile.Name + ": backed up " + plan.Files.Count + " file(s) to " + archive;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { Store.Log("Backup of " + profile.Name + " failed: " + error.Message); return profile.Name + ": could not finish. " + error.Message; }
        }
    }
}
