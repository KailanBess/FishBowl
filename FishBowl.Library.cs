using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace EmulatorHub
{
    public static class GameLibraryRemoval
    {
        static bool SamePath(string first, string second)
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
            if (library.Emulators.RemoveAll(emulator => emulator.Id == id) == 0) return false;
            foreach (var game in library.Games)
            {
                if (game.EmulatorId == id) game.EmulatorId = null;
                if (game.PreferredEmulatorId == id) game.PreferredEmulatorId = null;
            }
            return true;
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
