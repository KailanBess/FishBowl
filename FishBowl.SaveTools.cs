using System;
using System.IO;

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
}
