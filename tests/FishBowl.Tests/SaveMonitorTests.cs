using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EmulatorHub;

// Shared changed-save review rules (SaveMonitor, SaveChangeTracker).
public static class SaveMonitorTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        string folder = Path.Combine(root, "save-monitor"); Directory.CreateDirectory(folder);
        string saves = Path.Combine(folder, "emulator-saves"); Directory.CreateDirectory(saves);
        var quest = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "Test Quest", EmulatorId = "emu", Saves = new List<GameSaveEntry>() };
        var racer = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "Sample Racer", EmulatorId = "emu", Saves = new List<GameSaveEntry>() };
        var library = new LibraryData { Emulators = new List<EmulatorProfile> { new EmulatorProfile { Id = "emu", Name = "Fixture" } }, Games = new List<GameEntry> { quest, racer }, GameLibraryRoot = Path.Combine(folder, "library"), Theme = new ThemeSettings() };
        SaveHistory.EnsureData(library);

        var roots = SaveMonitor.WatchRoots(library, new Dictionary<string, List<string>> { { "emu", new List<string> { saves } } });
        check("detected emulator save folder is watched for its games", roots.Count == 1 && roots.Values.First().SetEquals(new[] { quest.Id, racer.Id }));
        check("temporary and staging files are ignored", !SaveMonitor.ShouldQueue(library, Path.Combine(saves, "a.tmp")) && !SaveMonitor.ShouldQueue(library, Path.Combine(saves, ".fishbowl-restore-1")) && !SaveMonitor.ShouldQueue(library, Path.Combine(saves, "x.state1")));
        check("managed snapshots are ignored", !SaveMonitor.ShouldQueue(library, Path.Combine(SaveMonitor.ManagedRoot(library), "x.sav")));
        check("ordinary save files are queued", SaveMonitor.ShouldQueue(library, Path.Combine(saves, "Test Quest.sav")));

        string questSave = Path.Combine(saves, "Test Quest.sav"); File.WriteAllText(questSave, "one");
        check("unlinked change is recorded with a suggested game", SaveMonitor.Record(library, questSave) && library.SaveReviews.Single().Path == "suggested:" + quest.Id && library.SaveReviews[0].GameId == null);
        SaveMonitor.Record(library, questSave);
        check("repeated change joins the same review", library.SaveReviews.Count == 1 && library.SaveReviews[0].Files.Count == 1);

        library.SaveReviews.Clear();
        string folderSave = Path.Combine(saves, "racer"); Directory.CreateDirectory(folderSave); File.WriteAllText(Path.Combine(folderSave, "slot.dat"), "a");
        GameSaves.Link(racer, folderSave, "In-game saves");
        SaveMonitor.Record(library, Path.Combine(folderSave, "slot.dat"));
        check("change inside a linked folder is grouped under that folder and game", library.SaveReviews.Count == 1 && library.SaveReviews[0].GameId == racer.Id && library.SaveReviews[0].Files.Single() == folderSave);
        check("no automatic copy without the preference", SaveMonitor.AutomaticCandidate(library) == null);
        racer.SaveCopyPreference = "Automatic copies";
        var auto = SaveMonitor.AutomaticCandidate(library);
        check("automatic copies apply to linked originals", auto != null && auto.GameId == racer.Id);
        var copies = SaveMonitor.CopyGroup(library, racer, auto.Files, CancellationToken.None);
        check("copying a group snapshots each source", copies.Count == 1 && Directory.Exists(copies[0].Path));
        racer.SaveCopyPreference = "Never ask"; library.SaveReviews.Clear();
        check("never ask skips the game's changes", !SaveMonitor.Record(library, Path.Combine(folderSave, "slot.dat")) && library.SaveReviews.Count == 0);

        library.SaveReviews.Clear(); quest.SaveCopyPreference = null;
        GameSaves.Link(quest, questSave, "In-game saves");
        SaveHistory.Capture(library, quest, questSave, "In-game saves", false, CancellationToken.None);
        check("a save equal to its newest snapshot is not queued again", !SaveMonitor.Record(library, questSave) && library.SaveReviews.Count == 0);
        File.WriteAllText(questSave, "two");
        check("a save that differs from its newest snapshot is queued", SaveMonitor.Record(library, questSave) && library.SaveReviews.Count == 1);

        // Backup reminders: played after the last save backup, and that backup older than the interval.
        library.Theme.AutoBackupDays = 7; var now = DateTime.UtcNow;
        library.PlaySessions = new List<PlaySession> { new PlaySession { Id = "p", GameId = quest.Id, StartedAt = now.AddDays(-1).ToString("o"), EndedAt = now.AddDays(-1).ToString("o") } };
        var played = BackupReminders.LastPlayed(library);
        check("play sessions mark the game's emulator as played", played.ContainsKey("emu"));
        check("played but never backed up is due", BackupReminders.Due(library, new Dictionary<string, DateTime>(), played, now).Count == 1);
        check("recent backup is not due", BackupReminders.Due(library, new Dictionary<string, DateTime> { { "emu", now.AddDays(-2) } }, played, now).Count == 0);
        var stale = new Dictionary<string, DateTime> { { "emu", now.AddDays(-10) } };
        check("old backup with newer play is due", BackupReminders.Due(library, stale, played, now).Count == 1 && BackupReminders.Notice(BackupReminders.Due(library, stale, played, now), stale).StartsWith("Backup reminder: Fixture has been played", StringComparison.Ordinal));
        check("never played is never due", BackupReminders.Due(library, stale, new Dictionary<string, DateTime>(), now).Count == 0);

        var tracker = new SaveChangeTracker(); var start = DateTime.UtcNow;
        tracker.Queue(questSave, start);
        check("tracker waits for writes to settle", tracker.Ready(start).Count == 0 && tracker.Ready(start.AddSeconds(1)).Count == 0 && tracker.Ready(start.AddSeconds(5)).Count == 1);
        library.Experience.QuietStartHour = 22; library.Experience.QuietEndHour = 6;
        check("quiet hours wrap past midnight", SaveMonitor.Quiet(library.Experience, new DateTime(2026, 1, 1, 23, 0, 0)) && !SaveMonitor.Quiet(library.Experience, new DateTime(2026, 1, 1, 12, 0, 0)));
    }
}
