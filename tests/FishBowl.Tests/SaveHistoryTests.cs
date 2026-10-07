using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using EmulatorHub;

// Shared SaveHistory/SafeFiles/GameSaves (moved from windows/FishBowl.cs).
public static class SaveHistoryTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        string folder = Path.Combine(root, "save-history"); Directory.CreateDirectory(folder);
        var game = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "History Fixture", Saves = new List<GameSaveEntry>() };
        var library = new LibraryData { Emulators = new List<EmulatorProfile>(), Games = new List<GameEntry> { game }, GameLibraryRoot = Path.Combine(folder, "library") };
        string live = Path.Combine(folder, "live"); Directory.CreateDirectory(Path.Combine(live, "slot1"));
        File.WriteAllText(Path.Combine(live, "slot1", "save.dat"), "first"); File.WriteAllText(Path.Combine(live, "system.dat"), "sys");
        GameSaves.Link(game, live, "In-game saves");
        check("linking a save folder records it once", game.Saves.Count == 1 && game.Saves[0].IsFolder);
        GameSaves.Link(game, live, "In-game saves");
        check("linking the same folder twice is ignored", game.Saves.Count == 1);

        var first = SaveHistory.Capture(library, game, live, "In-game saves", false, CancellationToken.None);
        string expectedRoot = Path.Combine(library.GameLibraryRoot, "Game Saves", game.Id, "In-game saves", "History");
        check("folder snapshot uses the Windows library layout", SafeFiles.Within(first.Path, expectedRoot) && Directory.Exists(first.Path) && first.IsFolder);
        check("folder snapshot records hash and size", first.Hash == SafeFiles.Hash(live, CancellationToken.None) && first.Bytes == 8);
        check("unassigned game snapshot names no emulator", first.EmulatorName == "Unassigned");
        var again = SaveHistory.Capture(library, game, live, "In-game saves", true, CancellationToken.None);
        check("deduplicated capture reuses identical snapshot", again.Id == first.Id && library.SaveSnapshots.Count == 1);
        bool refused = false;
        try { SaveHistory.Capture(library, game, first.Path, "In-game saves", false, CancellationToken.None); } catch (IOException) { refused = true; }
        check("snapshot of a snapshot is refused", refused);

        // Restore needs a closed, assigned emulator.
        refused = false;
        try { SaveHistory.Restore(library, first, CancellationToken.None); } catch (IOException) { refused = true; }
        check("restore without an assigned emulator is refused", refused);
        string fake = Path.Combine(folder, "fake-emulator"); File.WriteAllBytes(fake, new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0 });
        var emulator = new EmulatorProfile { Id = "emu", Name = "Fixture Emulator", Executable = fake };
        library.Emulators.Add(emulator); game.EmulatorId = "emu";
        File.WriteAllText(Path.Combine(live, "slot1", "save.dat"), "second progress");
        SaveHistory.Restore(library, first, CancellationToken.None);
        check("folder restore brings back snapshot content", File.ReadAllText(Path.Combine(live, "slot1", "save.dat")) == "first");
        var before = library.SaveSnapshots.FirstOrDefault(s => s.Pinned);
        check("restore keeps the replaced save as a pinned snapshot", before != null && File.ReadAllText(Path.Combine(before.Path, "slot1", "save.dat")) == "second progress");
        check("restore keeps an adjacent rollback copy", Directory.GetDirectories(folder, ".fishbowl-before-*").Length == 1);

        File.AppendAllText(Path.Combine(first.Path, "system.dat"), "tampered"); refused = false;
        try { SaveHistory.Verify(first, CancellationToken.None); } catch (InvalidDataException) { refused = true; }
        check("tampered snapshot fails verification", refused);
        File.WriteAllText(Path.Combine(first.Path, "system.dat"), "sys");

        string bundle = Path.Combine(folder, "bundle.zip");
        SaveHistory.Export(library, first, bundle, CancellationToken.None);
        var imported = SaveHistory.Import(library, game, bundle, CancellationToken.None);
        check("exported bundle imports with the same hash", imported.Hash == first.Hash && imported.Source == null && Directory.Exists(imported.Path));

        string cloud = Path.Combine(folder, "cloud"); Directory.CreateDirectory(cloud);
        int exported = SaveHistory.ExportAll(library, cloud, CancellationToken.None, null);
        check("cloud export writes one bundle per snapshot", exported == library.SaveSnapshots.Count && Directory.GetFiles(cloud, "*.fishbowl-save.zip", SearchOption.AllDirectories).Length == exported);
        check("cloud export skips bundles already exported", SaveHistory.ExportAll(library, cloud, CancellationToken.None, null) == 0 && library.Experience.LastCloudBackup != null);

        string evil = Path.Combine(folder, "evil.zip");
        using (var archive = ZipFile.Open(evil, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(archive.CreateEntry("snapshot.json").Open())) w.Write(Json.Serialize(new SaveSnapshot { Kind = "In-game saves", Hash = "x", IsFolder = true }));
            using (var w = new StreamWriter(archive.CreateEntry("data/../../escape.txt").Open())) w.Write("x");
        }
        refused = false; int count = library.SaveSnapshots.Count;
        try { SaveHistory.Import(library, game, evil, CancellationToken.None); } catch (InvalidDataException) { refused = true; }
        check("bundle entries cannot escape the import folder", refused && library.SaveSnapshots.Count == count && !File.Exists(Path.Combine(folder, "library", "Game Saves", game.Id, "escape.txt")));

        library.Experience.SnapshotCount = 1; library.Experience.SnapshotDays = 0; library.Experience.SnapshotMegabytes = 0;
        var candidates = SaveHistory.CleanupCandidates(library, game);
        check("cleanup keeps pinned and newest snapshots", candidates.Count == library.SaveSnapshots.Count - 2 && candidates.All(s => !s.Pinned));
        foreach (var s in candidates) SaveHistory.Remove(library, s);
        check("cleanup removes managed snapshot folders", candidates.All(s => !Directory.Exists(s.Path)) && library.SaveSnapshots.Count == 2);

        // Single-file saves (memory cards, battery saves).
        string card = Path.Combine(folder, "card.mcd"); File.WriteAllText(card, "card one");
        var states = SaveHistory.Capture(library, game, card, "Save states", false, CancellationToken.None);
        check("file snapshot stores a verified copy", !states.IsFolder && File.ReadAllText(states.Path) == "card one");
        check("save-state compatibility reports recorded emulator", SaveHistory.Compatibility(library, states).StartsWith("Recorded emulator version and core match", StringComparison.Ordinal));
        emulator.ManualVersion = "9.9";
        check("save-state compatibility warns about version changes", SaveHistory.Compatibility(library, states).Contains("Version differs"));
        refused = false;
        try { GameSaves.Root(library, new GameEntry { Id = "../x" }, "In-game saves"); } catch (ArgumentException) { refused = true; }
        check("game save root rejects unsafe game ids", refused);
        refused = false;
        try { SafeFiles.Under(folder, "../outside"); } catch (InvalidDataException) { refused = true; }
        check("SafeFiles.Under rejects escaping paths", refused);
    }
}
