using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace EmulatorHub
{
    // Save menus, changed-save monitoring and the storage/backup screens (MainForm's save features in the Windows build).
    public partial class MainWindow
    {
        private readonly List<FileSystemWatcher> saveWatchers = new List<FileSystemWatcher>();
        private readonly Dictionary<string, HashSet<string>> saveWatchRoots = new Dictionary<string, HashSet<string>>();
        private readonly List<string> saveMonitorErrors = new List<string>();
        private readonly SaveChangeTracker saveChangeTracker = new SaveChangeTracker();
        private readonly DispatcherTimer saveMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private int saveWatchGeneration; private string lastSaveObserved; private bool reviewingSaves, automaticCopyRunning, pendingSaveNotice;

        // Library > Game saves submenu (the Windows build lists these in its Library menu).
        private MenuItem BuildSavesMenu()
        {
            var prompt = new MenuItem { Header = "Prompt to copy changed in-game saves", ToggleType = MenuItemToggleType.CheckBox, IsChecked = !library.Theme.DisableInGameSaveNotifications };
            prompt.Click += delegate { library.Theme.DisableInGameSaveNotifications = !library.Theme.DisableInGameSaveNotifications; prompt.IsChecked = !library.Theme.DisableInGameSaveNotifications; Store.Save(library); ConfigureSaveMonitoring(); };
            var menu = new MenuItem { Header = "Game saves", Icon = new FishIcon("storage", 18), ItemsSource = new object[] {
                MenuAction("Game saves and save states...", "storage", () => ShowSaveHistory(SelectedLibraryGame())),
                MenuAction("Save history and retention...", "storage", () => ShowSaveHistory(null)),
                MenuAction("Review changed in-game saves...", "storage", ReviewChangedSaves),
                prompt,
                MenuAction("Save monitoring status...", "info", ShowSaveMonitoringStatus), new Separator(),
                MenuAction("Storage and backup dashboard...", "storage", ShowStorageDashboard),
                MenuAction("Optional cloud backup folder...", "export", ShowCloudBackupFolder),
                MenuAction("Emulator backups / restore...", "export", () => ShowEmulatorManager("Backups")) } };
            menu.SubmenuOpened += delegate { prompt.IsChecked = !library.Theme.DisableInGameSaveNotifications; };
            return menu;
        }

        // Emulators menu entries for the selected emulator's save folders.
        private object[] SelectedEmulatorSaveFolderItems()
        {
            return new object[] {
                MenuAction("Open selected save folder", "folder", () => OpenSelectedSaveFolder("InGameSaveFolder", "save folder")),
                MenuAction("Open selected save states folder", "folder", () => OpenSelectedSaveFolder("SaveStateFolder", "save states folder")) };
        }

        private async Task OpenSelectedSaveFolder(string property, string label)
        {
            var profile = CurrentEmulator(); if (profile == null) { await Ui.Message(this, "Select an emulator first."); return; }
            var folder = await Task.Run(() => new EmulatorFolderDetector().Detect(profile).FirstOrDefault(f => f.Property == property && Directory.Exists(f.Path)));
            if (folder == null) { await Ui.Message(this, "No " + label + " was found for " + profile.Name + ". Choose it in the emulator's folder list, or start the emulator once so it creates its folders."); return; }
            Platform.Open(folder.Path);
        }

        private async Task ShowSaveHistory(GameEntry game)
        {
            await new SaveHistoryDialog(library, game).Present(this);
            Store.Save(library); ConfigureSaveMonitoring(); RefreshGameLibrary();
        }

        // ----- Monitoring -------------------------------------------------------------------------------------------

        private void StartSaveMonitoring()
        {
            saveMonitorTimer.Tick += async delegate { await Ui.Run(this, () => { ProcessSaveChanges(); return Task.CompletedTask; }); };
            saveMonitorTimer.Start();
            Closed += delegate { saveMonitorTimer.Stop(); StopSaveWatchers(); };
            ConfigureSaveMonitoring();
        }

        private void StopSaveWatchers()
        {
            foreach (var watcher in saveWatchers) watcher.Dispose();
            saveWatchers.Clear(); saveWatchRoots.Clear(); saveChangeTracker.Clear();
        }

        private void ConfigureSaveMonitoring()
        {
            int generation = ++saveWatchGeneration; StopSaveWatchers();
            if (library.Theme.DisableInGameSaveNotifications) return;
            SaveHistory.EnsureData(library);
            // Detection reads emulator config files; it runs on copies so the library is never touched off the UI thread.
            var profiles = library.Emulators.Select(e => Json.Deserialize<EmulatorProfile>(Json.Serialize(e))).ToList();
            Task.Run(() =>
            {
                var detected = new Dictionary<string, List<string>>();
                foreach (var profile in profiles)
                {
                    try { detected[profile.Id] = new EmulatorFolderDetector().Detect(profile).Where(f => f.Property == "InGameSaveFolder" && Directory.Exists(f.Path)).Select(f => f.Path).ToList(); }
                    catch (Exception error) { Store.Log("Save folder detection unavailable: " + error.Message); }
                }
                return detected;
            }).ContinueWith(task => Ui.Post(() =>
            {
                if (generation != saveWatchGeneration || library.Theme.DisableInGameSaveNotifications || task.IsFaulted) return;
                foreach (var root in SaveMonitor.WatchRoots(library, task.Result))
                {
                    try
                    {
                        var watcher = new FileSystemWatcher(root.Key) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite };
                        FileSystemEventHandler changed = (sender, e) => QueueSaveChange(e.FullPath, generation);
                        watcher.Created += changed; watcher.Changed += changed; watcher.Renamed += (sender, e) => QueueSaveChange(e.FullPath, generation);
                        watcher.Error += delegate { Ui.Post(() => { if (generation == saveWatchGeneration) SetStatus("Save monitoring missed file events. Open File > Game saves to review files manually."); }); };
                        watcher.EnableRaisingEvents = true;
                        saveWatchers.Add(watcher); saveWatchRoots[root.Key] = root.Value;
                    }
                    catch (Exception error)
                    {
                        // inotify watch limits are the usual cause on Linux (fs.inotify.max_user_watches).
                        if (saveMonitorErrors.Count < 50) saveMonitorErrors.Add(root.Key + ": " + error.Message);
                        Store.Log("In-game save monitoring unavailable: " + error.Message);
                    }
                }
            }));
        }

        private void QueueSaveChange(string path, int generation)
        {
            Ui.Post(() =>
            {
                if (generation == saveWatchGeneration && !library.Theme.DisableInGameSaveNotifications && !Directory.Exists(path) && SaveMonitor.ShouldQueue(library, path))
                    saveChangeTracker.Queue(path, DateTime.UtcNow);
            });
        }

        private void ProcessSaveChanges()
        {
            if (library.Theme.DisableInGameSaveNotifications || reviewingSaves || automaticCopyRunning) return;
            SaveHistory.EnsureData(library);
            bool changed = false;
            foreach (var path in saveChangeTracker.Ready(DateTime.UtcNow))
            {
                lastSaveObserved = DateTime.Now.ToString("g");
                if (SaveMonitor.Record(library, path)) { changed = true; pendingSaveNotice = true; }
                else if (library.SaveReviews.Count >= 100) { SetStatus("100 save groups await review. Open File > Game saves to review them."); break; }
            }
            if (changed) Store.Save(library);
            var automatic = SaveMonitor.AutomaticCandidate(library);
            if (automatic != null) { StartAutomaticCopies(automatic); return; }
            if (library.SaveReviews.Count == 0) { pendingSaveNotice = false; return; }
            if (pendingSaveNotice && !SaveMonitor.Quiet(library.Experience, DateTime.Now))
            {
                pendingSaveNotice = false;
                SetStatus(library.SaveReviews.Count + " save groups await review: File > Game saves > Review changed in-game saves.");
                Notify("Copy your in-game saves to FishBowl?", library.SaveReviews.Count + " changed save group(s). Open FishBowl to copy, skip, or review later.");
            }
        }

        // Copies run on a copy of the library; the results are merged back on the UI thread.
        private void StartAutomaticCopies(SaveReviewItem review)
        {
            automaticCopyRunning = true;
            var clone = Json.Deserialize<LibraryData>(Json.Serialize(library));
            var game = clone.Games.First(g => g.Id == review.GameId); var sources = review.Files.ToArray();
            Task.Run(() => SaveMonitor.CopyGroup(clone, game, sources, CancellationToken.None)).ContinueWith(task => Ui.Post(() =>
            {
                automaticCopyRunning = false;
                if (task.IsFaulted)
                {
                    string error = task.Exception.GetBaseException().Message;
                    review.GameId = null; if (saveMonitorErrors.Count < 50) saveMonitorErrors.Add(error); pendingSaveNotice = true;
                    Store.Log("Automatic save copy failed; queued for review: " + error); Store.Save(library); return;
                }
                var original = library.Games.FirstOrDefault(g => g.Id == review.GameId); if (original == null) return;
                foreach (var snapshot in task.Result)
                {
                    if (!library.SaveSnapshots.Any(s => s.Id == snapshot.Id)) library.SaveSnapshots.Add(snapshot);
                    GameSaves.Link(original, snapshot.Path, snapshot.Kind);
                }
                library.SaveReviews.Remove(review);
                library.Experience.LastSuccessfulBackup = DateTime.UtcNow.ToString("o");
                Store.Save(library); SetStatus("Automatic save snapshot created for " + original.Title + ".");
            }));
        }

        private static void Notify(string title, string text)
        {
            try
            {
                var start = new ProcessStartInfo("notify-send") { UseShellExecute = false };
                start.ArgumentList.Add("--app-name=FishBowl"); start.ArgumentList.Add(title); start.ArgumentList.Add(text);
                using (Process.Start(start)) { }
            }
            catch (Exception) { } // No notification daemon or notify-send: the status bar message remains.
        }

        private async Task ReviewChangedSaves()
        {
            SaveHistory.EnsureData(library);
            if (reviewingSaves) return;
            if (library.SaveReviews.Count == 0) { await Ui.Message(this, "No changed saves await review. Link original saves or choose emulator save folders to begin monitoring."); return; }
            reviewingSaves = true;
            try
            {
                foreach (var review in library.SaveReviews.ToArray())
                {
                    var paths = (review.Files ?? new List<string>()).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                    if (paths.Count == 0) { library.SaveReviews.Remove(review); continue; }
                    var candidates = review.GameId == null ? library.Games.ToList() : library.Games.Where(g => g.Id == review.GameId).ToList();
                    if (candidates.Count == 0) candidates = library.Games.ToList();
                    var dialog = new SaveGroupPromptDialog(library, paths, candidates); await dialog.Present(this);
                    if (dialog.Result == null) break;
                    library.SaveReviews.Remove(review); Store.Save(library);
                }
            }
            finally { reviewingSaves = false; Store.Save(library); ConfigureSaveMonitoring(); }
        }

        private async Task ShowSaveMonitoringStatus()
        {
            SaveHistory.EnsureData(library);
            var rows = new List<string> {
                "Monitoring enabled: " + !library.Theme.DisableInGameSaveNotifications,
                "Pending review groups: " + library.SaveReviews.Count,
                "Quiet / snoozed now: " + SaveMonitor.Quiet(library.Experience, DateTime.Now),
                "Last observed change: " + (lastSaveObserved ?? "Not yet observed"),
                "Desktop notifications use notify-send when available; pending reviews stay in File > Game saves." };
            rows.AddRange(saveWatchRoots.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => "Watched folder: " + k));
            rows.AddRange(saveMonitorErrors.Select(e => "Error: " + e));
            await new ResultsDialog("Save monitoring status", rows).Present(this);
        }

        // ----- Storage and cloud ------------------------------------------------------------------------------------

        private async Task ShowStorageDashboard()
        {
            SaveHistory.EnsureData(library);
            SetStatus("Reading storage and backup inventory...");
            var rows = await Task.Run(() => new List<string> {
                SaveHistory.StorageSummary("Games", GameStorage.Root(library), CancellationToken.None),
                SaveHistory.StorageSummary("Save snapshots", SaveMonitor.ManagedRoot(library), CancellationToken.None),
                SaveHistory.StorageSummary("Backups", HubPaths.BackupRoot(library), CancellationToken.None) });
            rows.Add("Snapshot history: " + library.SaveSnapshots.Count + " snapshots / " + SaveHistory.Bytes(SaveHistory.SnapshotStorage(library)));
            rows.Add("Last successful save backup: " + (library.Experience.LastSuccessfulBackup ?? "Not recorded"));
            rows.Add("Last emulator backup: " + (library.Theme.LastBackupAt ?? "Not recorded"));
            rows.Add("Last cloud folder export: " + (library.Experience.LastCloudBackup ?? "Not yet exported"));
            SetStatus("Storage inventory ready.");
            await new ResultsDialog("FishBowl Storage and Backup Dashboard", rows.SelectMany(r => r.Split('\n').Select((line, i) => i == 0 ? line : "    " + line))).Present(this);
        }

        private async Task ShowCloudBackupFolder()
        {
            SaveHistory.EnsureData(library); var settings = library.Experience;
            var dialog = new FishDialog("Optional Cloud Backup Folder", 620);
            var provider = Ui.Combo(new[] { "Nextcloud", "Syncthing", "Dropbox", "Google Drive", "OneDrive", "Custom synced folder" }, settings.CloudProvider ?? "Custom synced folder");
            var folder = Ui.Field(settings.CloudFolder);
            var last = Ui.Hint("Last successful export: " + (settings.LastCloudBackup ?? "Not yet exported"));
            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(Ui.Caption("Installed sync provider")); body.Children.Add(provider);
            body.Children.Add(Ui.Caption("Your provider's local synced folder"));
            body.Children.Add(Ui.WithButton(folder, Ui.Action("Choose folder", async () => { var chosen = await Ui.PickFolder(dialog, "Choose a local folder managed by your sync client", folder.Text); if (chosen != null) folder.Text = chosen; })));
            body.Children.Add(Ui.Hint("FishBowl exports versioned snapshot bundles only. Your sync client handles upload. Live emulator files are not synchronized by FishBowl."));
            body.Children.Add(last);
            body.Children.Add(Ui.Actions(
                Ui.Action("Save setup", () => { if (!Directory.Exists(folder.Text)) throw new IOException("Choose an existing sync folder."); settings.CloudFolder = folder.Text; settings.CloudProvider = provider.SelectedItem as string; Store.Save(library); }),
                Ui.Action("Preview / export", async () =>
                {
                    if (!Directory.Exists(folder.Text)) throw new IOException("Choose an existing sync folder.");
                    await new ResultsDialog("Cloud export preview", library.SaveSnapshots.Count == 0 ? new[] { "No snapshots yet. Create snapshots in File > Game saves first." } : library.SaveSnapshots.Select(s => { var g = library.Games.FirstOrDefault(x => x.Id == s.GameId); return (g == null ? s.GameId : g.Title) + " / " + s.CreatedAt + " / " + SaveHistory.Bytes(s.Bytes); })).Present(dialog);
                    if (library.SaveSnapshots.Count == 0 || !await Ui.Confirm(dialog, "Export verified snapshot bundles into " + folder.Text + "? Snapshots exported before are skipped. Your sync client controls upload.", "Review cloud export")) return;
                    var clone = Json.Deserialize<LibraryData>(Json.Serialize(library)); string target = folder.Text;
                    int count = await Task.Run(() => SaveHistory.ExportAll(clone, target, CancellationToken.None, null));
                    settings.CloudFolder = target; settings.CloudProvider = provider.SelectedItem as string; settings.LastCloudBackup = clone.Experience.LastCloudBackup; Store.Save(library);
                    last.Text = "Last successful export: " + settings.LastCloudBackup;
                    await Ui.Message(dialog, count + " new snapshot bundle(s) exported.");
                }),
                Ui.Action("Close", () => dialog.Close())));
            dialog.Body = body; await dialog.Present(this);
        }
    }
}
