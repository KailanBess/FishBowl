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
    // File snapshots use the same SaveSnapshot records as the existing Windows save history.
    // Folder archives are handled by EmulatorBackups, including its manifest and size validation.
    public static class SaveTools
    {
        public const long MaximumSaveBytes = 512L * 1024 * 1024;
        public static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new IOException("Choose a save file.");
            string full = Path.GetFullPath(path);
            for (string current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic links are not supported for save snapshots.");
                if (Path.GetPathRoot(current) == current) break;
            }
            if (File.Exists(full) && new FileInfo(full).Length > MaximumSaveBytes) throw new IOException("The save file exceeds the 512 MB snapshot limit.");
        }
        public static string CurrentHash(string path)
        {
            ValidatePath(path);
            return File.Exists(path) ? GameTools.Hash(path) : null;
        }
        public static SaveSnapshot Capture(GameEntry game, string source, string dataRoot)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.Id)) throw new IOException("Choose a game.");
            // The identifier never becomes a filesystem path supplied by a library file.
            Guid gameId; if (!Guid.TryParse(game.Id, out gameId)) throw new IOException("The game identifier is invalid.");
            source = Path.GetFullPath(source); ValidatePath(source); ValidatePath(dataRoot);
            if (!File.Exists(source)) throw new IOException("Choose an existing save file.");
            string hash = GameTools.Hash(source), id = Guid.NewGuid().ToString("N");
            string folder = Path.Combine(dataRoot, "Saves", gameId.ToString("N"), "History", id); ValidatePath(folder); Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Path.GetFileName(source));
            try
            {
                File.Copy(source, target, false);
                if (GameTools.Hash(source) != hash || GameTools.Hash(target) != hash) throw new IOException("The save changed while copying. Close the game and retry.");
                var snapshot = new SaveSnapshot { Id = id, GameId = game.Id, Source = source, Path = target, Hash = hash, Bytes = new FileInfo(target).Length, Kind = "In-game save", CreatedAt = DateTime.UtcNow.ToString("o"), IsFolder = false, EmulatorId = game.PreferredEmulatorId ?? game.EmulatorId, Note = "Verified file snapshot" };
                File.WriteAllText(Path.Combine(folder, "snapshot.json"), Json.Serialize(snapshot));
                return snapshot;
            }
            catch { if (File.Exists(target)) File.Delete(target); throw; }
        }
        public static void Verify(SaveSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsFolder || !File.Exists(snapshot.Path)) throw new IOException("Choose an available file snapshot. Use emulator backups for folders.");
            ValidatePath(snapshot.Path);
            if (new FileInfo(snapshot.Path).Length != snapshot.Bytes || GameTools.Hash(snapshot.Path) != snapshot.Hash) throw new IOException("Snapshot verification failed; no save was changed.");
        }
        // Call Capture for an existing destination and persist that backup before Restore.
        // expectedHash is the value displayed during review; null means a new destination.
        public static void Restore(SaveSnapshot snapshot, string destination, string expectedHash)
        {
            Verify(snapshot); destination = Path.GetFullPath(destination); ValidatePath(destination);
            if (GameLibraryRemoval.SamePath(destination, snapshot.Path)) throw new IOException("Choose a destination outside this snapshot.");
            if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException("Choose an existing destination folder.");
            if (CurrentHash(destination) != expectedHash) throw new IOException("The destination changed after review. Review it again.");
            string temporary = destination + ".fishbowl-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(snapshot.Path, temporary, false);
                if (GameTools.Hash(temporary) != snapshot.Hash) throw new IOException("Copied save failed verification.");
                if (CurrentHash(destination) != expectedHash) throw new IOException("The destination changed while copying.");
                ValidatePath(destination);
                if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    // Save history shared by both builds (moved from windows/FishBowl.cs). Snapshots live in
    // <game library>/Game Saves/<game id>/<kind>/History/<snapshot id>, the same layout on Windows and Linux.
    public static class SafeFiles
    {
        public static string Under(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":"))
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
}
