using System;
using System.IO;
using EmulatorHub;

public static class SaveToolsTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        string folder = Path.Combine(root, "save-tools"); Directory.CreateDirectory(folder);
        string original = Path.Combine(folder, "game.sav"); File.WriteAllText(original, "original progress");
        var game = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "Fixture" };
        var snapshot = SaveTools.Capture(game, original, Path.Combine(folder, "data"));
        check("snapshot preserves original save", File.ReadAllText(original) == "original progress" && snapshot.Bytes == new FileInfo(original).Length);
        SaveTools.Verify(snapshot);
        string exported = Path.Combine(folder, "transferred.sav"); SaveTools.Restore(snapshot, exported, null);
        check("snapshot transfer verifies content", File.ReadAllText(exported) == "original progress");
        File.WriteAllText(original, "later progress"); string hash = SaveTools.CurrentHash(original);
        var backup = SaveTools.Capture(game, original, Path.Combine(folder, "data"));
        SaveTools.Restore(snapshot, original, hash);
        check("reviewed restore preserves recoverable backup", File.ReadAllText(original) == "original progress" && File.ReadAllText(backup.Path) == "later progress");
        File.WriteAllText(original, "changed after review"); bool stopped = false;
        try { SaveTools.Restore(snapshot, original, hash); } catch (IOException) { stopped = true; }
        check("changed reviewed destination is protected", stopped && File.ReadAllText(original) == "changed after review");
        File.AppendAllText(snapshot.Path, "tampered"); stopped = false;
        try { SaveTools.Restore(snapshot, exported, SaveTools.CurrentHash(exported)); } catch (IOException) { stopped = true; }
        check("tampered snapshot cannot overwrite destination", stopped && File.ReadAllText(exported) == "original progress");
        stopped = false;
        try { SaveTools.Capture(new GameEntry { Id = "../../outside" }, original, folder); } catch (IOException) { stopped = true; }
        check("invalid snapshot game id is rejected", stopped);
#if NETCOREAPP
        if (!Platform.IsWindows)
        {
            string link = Path.Combine(folder, "linked"); Directory.CreateSymbolicLink(link, folder); stopped = false;
            try { SaveTools.Capture(game, Path.Combine(link, "game.sav"), Path.Combine(folder, "data")); } catch (IOException) { stopped = true; }
            check("symlinked save parent is rejected", stopped);
        }
#endif
    }
}
