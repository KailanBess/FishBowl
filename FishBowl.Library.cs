using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace EmulatorHub
{
    public static class GameLibraryRemoval
    {
        public static bool SamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
            try { return string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'), Path.GetFullPath(second).TrimEnd('\\', '/'), Platform.PathComparison); }
            catch (Exception error) {
                if (!(error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException || error is PathTooLongException)) throw;
                return string.Equals(first, second, Platform.PathComparison);
            }
        }
        public static bool IsExcluded(LibraryData library, string path)
        {
            return !string.IsNullOrWhiteSpace(path) && (library.RemovedGamePaths ?? new List<string>()).Any(removed => SamePath(removed, path));
        }

        public static void AllowPath(LibraryData library, string path)
        {
            if (library.RemovedGamePaths != null) library.RemovedGamePaths.RemoveAll(removed => SamePath(removed, path));
        }

        public static int RemoveGames(LibraryData library, IEnumerable<string> gameIds)
        {
            var ids = new HashSet<string>(gameIds);
            var removed = library.Games.Where(game => ids.Contains(game.Id)).ToArray();
            if (removed.Length == 0) return 0;
            Remember(library, new LibraryRemoval {
                Games = removed.ToList(), Queue = (library.PlayQueue ?? new List<string>()).ToList(),
                Collections = (library.Collections ?? new List<GameCollection>()).Where(c => c.Id != null).ToDictionary(c => c.Id, c => (c.GameIds ?? new List<string>()).ToList())
            });
            if (library.RemovedGamePaths == null) library.RemovedGamePaths = new List<string>();
            foreach (var game in removed)
                foreach (string path in new[] { game.Path }.Concat(game.Discs ?? new List<string>()))
                    if (!string.IsNullOrWhiteSpace(path) && !IsExcluded(library, path)) library.RemovedGamePaths.Add(path);
            var removedIds = new HashSet<string>(removed.Select(game => game.Id));
            library.Games.RemoveAll(game => removedIds.Contains(game.Id));
            foreach (var collection in library.Collections ?? new List<GameCollection>())
                if (collection.GameIds != null) collection.GameIds.RemoveAll(id => removedIds.Contains(id));
            if (library.PlayQueue != null) library.PlayQueue.RemoveAll(id => removedIds.Contains(id));
            return removed.Length;
        }

        public static bool RemoveEmulator(LibraryData library, string id)
        {
            var emulator = library.Emulators.FirstOrDefault(e => e.Id == id);
            if (emulator == null) return false;
            Remember(library, new LibraryRemoval { Emulator = emulator, Assignments = library.Games.Where(g => g.EmulatorId == id || g.PreferredEmulatorId == id).Select(g => new RemovedAssignment { GameId = g.Id, EmulatorId = g.EmulatorId, PreferredEmulatorId = g.PreferredEmulatorId }).ToList() });
            if (library.Emulators.RemoveAll(entry => entry.Id == id) == 0) return false;
            foreach (var game in library.Games)
            {
                if (game.EmulatorId == id || game.PreferredEmulatorId == id) game.RequiresEmulatorAssignment = true;
                if (game.EmulatorId == id) game.EmulatorId = null;
                if (game.PreferredEmulatorId == id) game.PreferredEmulatorId = null;
            }
            return true;
        }

        static void Remember(LibraryData library, LibraryRemoval removal)
        {
            if (library.RemovalHistory == null) library.RemovalHistory = new List<LibraryRemoval>();
            library.RemovalHistory.Add(removal);
            if (library.RemovalHistory.Count > 20) library.RemovalHistory.RemoveAt(0);
        }
        public static int Undo(LibraryData library)
        {
            if (library.RemovalHistory == null || library.RemovalHistory.Count == 0) return 0;
            var removal = library.RemovalHistory.Last();
            int restored = 0;
            if (removal.Emulator != null && !library.Emulators.Any(e => e.Id == removal.Emulator.Id || SamePath(e.Executable, removal.Emulator.Executable))) {
                library.Emulators.Add(removal.Emulator); restored++;
                foreach (var assignment in removal.Assignments ?? new List<RemovedAssignment>()) {
                    var game = library.Games.FirstOrDefault(g => g.Id == assignment.GameId);
                    if (game == null || !game.RequiresEmulatorAssignment) continue;
                    game.EmulatorId = assignment.EmulatorId; game.PreferredEmulatorId = assignment.PreferredEmulatorId; game.RequiresEmulatorAssignment = false;
                }
                removal.Emulator = null;
            }
            var restoredIds = new HashSet<string>();
            foreach (var game in removal.Games ?? new List<GameEntry>()) {
                if (library.Games.Any(g => g.Id == game.Id || SamePath(g.Path, game.Path))) continue;
                library.Games.Add(game); restoredIds.Add(game.Id); restored++;
                foreach (var path in new[] { game.Path }.Concat(game.Discs ?? new List<string>())) AllowPath(library, path);
                if ((!string.IsNullOrWhiteSpace(game.EmulatorId) && !library.Emulators.Any(e => e.Id == game.EmulatorId)) || (!string.IsNullOrWhiteSpace(game.PreferredEmulatorId) && !library.Emulators.Any(e => e.Id == game.PreferredEmulatorId))) game.RequiresEmulatorAssignment = true;
            }
            foreach (var collection in library.Collections ?? new List<GameCollection>()) {
                List<string> old;
                if (removal.Collections == null || collection.Id == null || !removal.Collections.TryGetValue(collection.Id, out old)) continue;
                if (collection.GameIds == null) collection.GameIds = new List<string>();
                RestoreOrder(collection.GameIds, old, restoredIds);
            }
            if (library.PlayQueue == null) library.PlayQueue = new List<string>();
            RestoreOrder(library.PlayQueue, removal.Queue ?? new List<string>(), restoredIds);
            // Keep skipped entries available for recovery after a conflicting entry is removed.
            if (removal.Games != null) removal.Games.RemoveAll(g => restoredIds.Contains(g.Id));
            bool gamesDone = removal.Games == null || removal.Games.Count == 0;
            bool emulatorDone = removal.Emulator == null;
            if (gamesDone && emulatorDone) library.RemovalHistory.Remove(removal);
            return restored;
        }
        static void RestoreOrder(List<string> current, List<string> old, HashSet<string> restored)
        {
            for (int i = 0; i < old.Count; i++) if (restored.Contains(old[i]) && !current.Contains(old[i])) {
                int next = old.Skip(i + 1).Select(id => current.IndexOf(id)).FirstOrDefault(index => index >= 0);
                bool hasNext = old.Skip(i + 1).Any(current.Contains);
                current.Insert(hasNext ? next : current.Count, old[i]);
            }
        }
    }

    public static class LibraryPaths
    {
        static bool IsPath(string name) { return name.EndsWith("Path") || name.EndsWith("Paths") || name.EndsWith("Folder") || name.EndsWith("Root") || name == "Executable" || name == "WorkingDirectory" || name == "Target" || name == "Discs" || name == "GameFolders"; }
        // Walk the saved model only, including removed entries and inactive user profiles.
        static void Visit(object value, Func<string, string> map, HashSet<object> seen)
        {
            if (value == null || value is string || value.GetType().IsValueType || !seen.Add(value)) return;
            var sequence = value as System.Collections.IEnumerable;
            if (sequence != null) { foreach (var child in sequence) Visit(child, map, seen); return; }
            foreach (var property in value.GetType().GetProperties()) {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                object child = property.GetValue(value, null);
                if (IsPath(property.Name) && property.CanWrite && child is string) property.SetValue(value, map((string)child), null);
                else if (IsPath(property.Name) && child is List<string>) { var paths = (List<string>)child; for (int i = 0; i < paths.Count; i++) paths[i] = map(paths[i]); }
                else if (property.PropertyType.Namespace == typeof(LibraryData).Namespace || child is System.Collections.IEnumerable) Visit(child, map, seen);
            }
        }
        public static int Rebase(LibraryData library, string oldRoot, string newRoot, bool apply)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot) || GameLibraryRemoval.SamePath(oldRoot, newRoot)) return 0;
            oldRoot = Path.GetFullPath(oldRoot).TrimEnd('\\', '/'); newRoot = Path.GetFullPath(newRoot).TrimEnd('\\', '/');
            int count = 0;
            Visit(library, delegate(string path) {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return path;
                string full;
                try { full = Path.GetFullPath(path); }
                catch (ArgumentException) { return path; }
                catch (NotSupportedException) { return path; }
                catch (PathTooLongException) { return path; }
                if (!full.Equals(oldRoot, Platform.PathComparison) && !full.StartsWith(oldRoot + Path.DirectorySeparatorChar, Platform.PathComparison)) return path;
                if (File.Exists(path) || Directory.Exists(path)) return path;
                string candidate = newRoot + full.Substring(oldRoot.Length);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return path;
                count++; return apply ? candidate : path;
            }, new HashSet<object>());
            return count;
        }
        public static void Load(LibraryData library, string dataRoot, string portableRoot)
        {
            Rebase(library, library.SavedDataRoot, dataRoot, true);
            if (!string.IsNullOrWhiteSpace(portableRoot)) Rebase(library, library.SavedPortableRoot, portableRoot, true);
        }
        public static void Stamp(LibraryData library, string dataRoot, string portableRoot)
        {
            library.SavedDataRoot = Path.GetFullPath(dataRoot);
            library.SavedPortableRoot = string.IsNullOrWhiteSpace(portableRoot) ? null : Path.GetFullPath(portableRoot);
        }
        public static List<string> ArtworkCandidates(LibraryData library, string dataRoot, IEnumerable<LibraryData> backups)
        {
            string folder = Path.GetFullPath(Path.Combine(dataRoot, "Artwork"));
            var referenced = new HashSet<string>(Platform.PathComparer);
            Action<LibraryData> collect = delegate(LibraryData data) { Visit(data, delegate(string p) {
                if (!string.IsNullOrWhiteSpace(p) && Path.IsPathRooted(p)) {
                    try { referenced.Add(Path.GetFullPath(p)); }
                    catch (ArgumentException) { }
                    catch (NotSupportedException) { }
                    catch (PathTooLongException) { }
                }
                return p;
            }, new HashSet<object>()); };
            collect(library); foreach (var backup in backups) collect(backup);
            var files = new List<string>();
            if (!Directory.Exists(folder) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return files;
            foreach (var dir in new[] { folder, Path.Combine(folder, "GameCovers"), Path.Combine(folder, "GameIcons") }) {
                if (!Directory.Exists(dir) || (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var file in Directory.EnumerateFiles(dir)) {
                    Guid id; string name = Path.GetFileNameWithoutExtension(file);
                    bool generated = Guid.TryParseExact(name, "N", out id) || (name.Length == 53 && name[20] == '-' && name.Substring(0, 20).All(c => Uri.IsHexDigit(c)) && Guid.TryParseExact(name.Substring(21), "N", out id));
                    if (generated && !referenced.Contains(Path.GetFullPath(file)) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) files.Add(file);
                }
            }
            return files;
        }
    }

    public static class GameLibraryEditing
    {
        public static string RetainArtwork(string source, string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(source)) return "";
            source = Path.GetFullPath(source);
            string folder = Path.GetFullPath(Path.Combine(dataDirectory, "Artwork", "GameCovers"));
            if (source.StartsWith(folder + Path.DirectorySeparatorChar, Platform.PathComparison)) return source;
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Guid.NewGuid().ToString("N") + Path.GetExtension(source));
            File.Copy(source, target, false);
            return target;
        }
    }
}
