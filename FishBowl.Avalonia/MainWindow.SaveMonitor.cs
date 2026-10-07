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
        private readonly DispatcherTimer backupScheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        private CancellationTokenSource captureCancellation; private bool plannedExportRunning;
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
                MenuAction("Backup schedule...", "refresh", ShowLinkedSaveSchedule),
                MenuAction("Backup planner...", "storage", ShowBackupPlanner),
                MenuAction("Back up due emulators now...", "export", BackUpDueEmulators),
                MenuAction("Emulator backups / restore...", "export", () => ShowEmulatorManager("Backups")),
                MenuAction("Backup verification and retention...", "check", ShowBackupVerification) } };
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
            backupScheduleTimer.Tick += delegate { RunScheduledSaveWork(); }; backupScheduleTimer.Start();
            Closed += delegate { backupScheduleTimer.Stop(); if (captureCancellation != null) captureCancellation.Cancel(); };
            Closed += delegate { saveMonitorTimer.Stop(); StopSaveWatchers(); };
            ConfigureSaveMonitoring();
            Opened += delegate { CheckBackupReminders(); };
        }

        // Shows a calm status-bar reminder when played emulators have no recent save backup.
        private void CheckBackupReminders()
        {
            var copy = Json.Deserialize<LibraryData>(Json.Serialize(library));
            Task.Run(() => { var backups = BackupReminders.LastBackups(copy, CancellationToken.None); return BackupReminders.Notice(BackupReminders.Due(copy, backups, BackupReminders.LastPlayed(copy), DateTime.UtcNow), backups); })
                .ContinueWith(task => Ui.Post(() => { if (!task.IsFaulted && task.Result != null) SetStatus(task.Result); }));
        }

        private async Task BackUpDueEmulators()
        {
            if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before backing up their saves.");
            SetStatus("Checking which emulators are due for a backup...");
            var due = await Task.Run(() => { var backups = BackupReminders.LastBackups(library, CancellationToken.None); return BackupReminders.Due(library, backups, BackupReminders.LastPlayed(library), DateTime.UtcNow); });
            if (due.Count == 0) { SetStatus("No emulators are due for a backup."); await Ui.Message(this, "No emulators are due for a backup.\n\nAn emulator becomes due when one of its games was played since its last save backup and that backup is more than " + BackupReminders.IntervalDays(library) + " day(s) old (FishBowl settings, backup reminder interval). One played but never backed up is due straight away."); return; }
            if (!await Ui.Confirm(this, "Back up the in-game saves and save states of " + String.Join(", ", due.Select(e => e.Name)) + " into " + HubPaths.BackupRoot(library) + "?", "Back up due emulators")) return;
            var results = new List<string>();
            for (int i = 0; i < due.Count; i++)
            {
                var profile = due[i]; SetStatus("Backing up due emulators (" + (i + 1) + " of " + due.Count + "): " + profile.Name + "...");
                results.Add(await Task.Run(() => BackupReminders.BackUp(library, profile, CancellationToken.None)));
            }
            if (results.Any(r => r.Contains(": backed up "))) { SaveHistory.EnsureData(library); library.Theme.LastBackupAt = DateTime.UtcNow.ToString("o"); library.Experience.LastSuccessfulBackup = library.Theme.LastBackupAt; Store.Save(library); }
            SetStatus("Backup of due emulators finished.");
            await new ResultsDialog("Back up due emulators", results).Present(this);
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

        // ----- Schedules ---------------------------------------------------------------------------------------------

        private static string NextExportText(NextSettings settings) { DateTime next; return DateTime.TryParse(settings.NextBackupAt, out next) ? next.ToLocalTime().ToString("g") : "After saving the schedule"; }
        private NextSettings EnsureEnhancements() { if (library.Enhancements == null) library.Enhancements = new NextSettings(); return library.Enhancements; }
        private HubSettings EnsureHub() { if (library.Hub == null) library.Hub = new HubSettings(); return library.Hub; }

        // Runs once a minute: the linked-save capture schedule and the scheduled snapshot export, never during play.
        private void RunScheduledSaveWork()
        {
            if (LibraryProfiles.ActiveLaunches > 0 || automaticCopyRunning || reviewingSaves) return;
            var hub = EnsureHub();
            if (hub.CaptureMinutes > 0 && captureCancellation == null)
            {
                DateTime next;
                if (!DateTime.TryParse(hub.NextCaptureAt, out next)) { hub.NextCaptureAt = DateTime.UtcNow.AddMinutes(hub.CaptureMinutes).ToString("o"); Store.Save(library); }
                else if (DateTime.UtcNow >= next.ToUniversalTime()) StartLinkedSaveCapture(null);
            }
            if (!plannedExportRunning && SaveHistory.PlannedExportDue(library, DateTime.UtcNow)) StartPlannedExport(null);
        }

        // Captures changed linked saves on a copy of the library and merges the snapshots back (HubSaveSchedule).
        private Task StartLinkedSaveCapture(Window report)
        {
            var hub = EnsureHub(); var copy = Json.Deserialize<LibraryData>(Json.Serialize(library));
            var cancellation = captureCancellation = new CancellationTokenSource();
            var done = new TaskCompletionSource<bool>();
            Task.Run(() => HubSaveSchedule.Capture(copy, cancellation.Token)).ContinueWith(task => Ui.Post(async () =>
            {
                try
                {
                    hub.LastCaptureReport = task.IsCanceled ? "Capture cancelled; new snapshots from the batch were discarded." : task.IsFaulted ? task.Exception.GetBaseException().Message : String.Join(Environment.NewLine, task.Result.Messages);
                    if (!task.IsFaulted && !task.IsCanceled) HubSaveSchedule.Apply(library, task.Result);
                    hub.NextCaptureAt = DateTime.UtcNow.AddMinutes(Math.Max(1, hub.CaptureMinutes)).ToString("o");
                    Store.Save(library);
                    if (report != null) await new ResultsDialog("Linked-save capture", (hub.LastCaptureReport ?? "").Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).DefaultIfEmpty("No games have linked original saves yet.")).Present(report);
                }
                catch (Exception error) { Store.Log("Scheduled linked saves: " + error.Message); }
                finally { captureCancellation = null; cancellation.Dispose(); done.TrySetResult(true); }
            }));
            return done.Task;
        }

        private async Task StartPlannedExport(Window owner)
        {
            var settings = EnsureEnhancements(); plannedExportRunning = true;
            try
            {
                var copy = Json.Deserialize<LibraryData>(Json.Serialize(library)); long quota = settings.BackupQuotaMegabytes;
                int count = await Task.Run(() => SaveHistory.ExportPlanned(copy, quota, CancellationToken.None, null));
                SaveHistory.PlannedExportDone(library); Store.Save(library);
                if (owner != null) await Ui.Message(owner, count + " snapshot(s) exported to " + SaveHistory.PlannedFolder(library) + ".");
            }
            catch (Exception error)
            {
                Store.Log("Scheduled backup failed: " + error.Message);
                if (owner != null) throw;
                settings.NextBackupAt = DateTime.UtcNow.AddHours(1.0).ToString("o"); Store.Save(library);
            }
            finally { plannedExportRunning = false; }
        }

        private async Task ShowLinkedSaveSchedule()
        {
            var hub = EnsureHub();
            var dialog = new FishDialog("Linked-save backup schedule", 720);
            var interval = new NumericUpDown { Minimum = 0, Maximum = 10080, Value = hub.CaptureMinutes, FormatString = "0" };
            var report = Ui.Paragraphs(hub.LastCaptureReport ?? "No scheduled captures yet."); report.Height = 160; report.Background = Ui.P.SurfaceBrush;
            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(Ui.Caption("Capture every N minutes (0 = off)")); body.Children.Add(interval);
            body.Children.Add(Ui.Hint("While FishBowl is open, capture changed original saves linked to games whose emulator is verifiably closed. Unchanged contents and managed snapshots are skipped. Keep sufficient backup space; use reviewed retention to remove old snapshots."));
            body.Children.Add(Ui.Caption("Last capture")); body.Children.Add(report);
            body.Children.Add(Ui.Actions(
                Ui.Action("Save schedule", () => { hub.CaptureMinutes = (int)(interval.Value ?? 0); hub.NextCaptureAt = DateTime.UtcNow.AddMinutes(Math.Max(1, hub.CaptureMinutes)).ToString("o"); Store.Save(library); dialog.Close(); }, true),
                Ui.Action("Capture linked saves now", async () => { if (captureCancellation != null) throw new IOException("A capture is already running."); if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before capturing saves."); await StartLinkedSaveCapture(dialog); report.Text = hub.LastCaptureReport ?? ""; }),
                Ui.Action("Stop scheduled capture", () => { if (captureCancellation != null) captureCancellation.Cancel(); }),
                Ui.Action("Snapshot export schedule", ShowBackupPlanner),
                Ui.Action("Close", () => dialog.Close())));
            dialog.Body = body; await dialog.Present(this);
        }

        private async Task ShowBackupPlanner()
        {
            var settings = EnsureEnhancements(); SaveHistory.EnsureData(library);
            var dialog = new FishDialog("Backup planner and quota", 640);
            var interval = new NumericUpDown { Minimum = 0, Maximum = 365, Value = settings.BackupIntervalDays, FormatString = "0" };
            var quota = new NumericUpDown { Minimum = 1, Maximum = 1048576, Value = Math.Max(1, settings.BackupQuotaMegabytes), FormatString = "0" };
            var status = Ui.Text("", 13);
            Action preview = () =>
            {
                long used = SaveHistory.SnapshotStorage(library), limit = (long)(quota.Value ?? 1) * 1024 * 1024;
                status.Text = "Managed snapshot storage: " + SaveHistory.Bytes(used) + "\nEstimated next 30 days: " + SaveHistory.Bytes(SaveHistory.EstimatedGrowth(library, 30)) + " (recent 30-day average)\nQuota use for this snapshot set: " + Math.Round(100.0 * used / Math.Max(1L, limit), 1) + "%\nNext export: " + ((interval.Value ?? 0) == 0 ? "Disabled" : NextExportText(settings)) + "\nLast export: " + (settings.LastPlannedBackup ?? "Not yet") + "\nExport folder: " + SaveHistory.PlannedFolder(library);
            };
            interval.ValueChanged += delegate { preview(); }; quota.ValueChanged += delegate { preview(); }; preview();
            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(Ui.Caption("Export every N days (0 disables)")); body.Children.Add(interval);
            body.Children.Add(Ui.Caption("Scheduled export quota (MB)")); body.Children.Add(quota);
            body.Children.Add(Ui.Caption("Plan and storage")); body.Children.Add(status);
            body.Children.Add(Ui.Actions(
                Ui.Action("Save schedule", () => { settings.BackupIntervalDays = (int)(interval.Value ?? 0); settings.BackupQuotaMegabytes = (long)(quota.Value ?? 1); settings.NextBackupAt = settings.BackupIntervalDays == 0 ? null : DateTime.UtcNow.AddDays(settings.BackupIntervalDays).ToString("o"); Store.Save(library); preview(); }, true),
                Ui.Action("Export snapshot set now", async () => { if (plannedExportRunning) return; settings.BackupQuotaMegabytes = (long)(quota.Value ?? 1); await StartPlannedExport(dialog); preview(); }),
                Ui.Action("Open export folder", () => { var folder = SaveHistory.PlannedFolder(library); Directory.CreateDirectory(folder); Platform.Open(folder); }),
                Ui.Action("Close", () => dialog.Close())));
            dialog.Body = body; await dialog.Present(this);
        }

        // "Backup verification and retention": verify, restore, pin and clean up emulator backup archives.
        private async Task ShowBackupVerification()
        {
            SaveHistory.EnsureData(library);
            var dialog = new FishDialog("Backup Verification and Retention", 940, 600);
            var list = new ListBox { Background = Ui.P.SurfaceBrush, Foreground = Ui.P.InkBrush };
            string root = HubPaths.BackupRoot(library);
            Func<Task> reload = async () =>
            {
                var files = await Task.Run(() => BackupRetention.Archives(library, CancellationToken.None));
                list.ItemsSource = files.Select(f => new ListBoxItem { Content = (library.Experience.PinnedBackupPaths.Contains(f) ? "★ " : "") + f, Tag = f }).ToList();
            };
            Func<string> selected = () => { var item = list.SelectedItem as ListBoxItem; if (item == null) throw new IOException("Select a backup archive."); return (string)item.Tag; };
            var actions = Ui.Actions(
                Ui.Action("Verify selected", async () => { var path = selected(); var manifest = await Task.Run(() => BackupIntegrity.Verify(path, CancellationToken.None)); await Ui.Message(dialog, manifest.EmulatorName + "\n" + manifest.Files.Count + " verified files\nCreated: " + manifest.CreatedAt, "Backup verified"); }),
                Ui.Action("Preview / restore", async () =>
                {
                    var path = selected();
                    var manifest = await Task.Run(() => BackupIntegrity.Verify(path, CancellationToken.None));
                    var emulator = library.Emulators.FirstOrDefault(e => e.Id == manifest.EmulatorId);
                    if (emulator == null) throw new IOException("This emulator profile is not registered.");
                    EmulatorBackups.ValidateRestore(emulator, manifest);
                    if (EmulatorRuntime.State(emulator.Executable) != RuntimeState.Stopped) throw new IOException("Close " + emulator.Name + " before restoring. FishBowl must be able to confirm it has exited.");
                    await new ResultsDialog("Backup restore destinations", manifest.Roots.Select(r => r.Label + " -> " + r.Source).Concat(new[] { manifest.Files.Count + " files; current eligible files receive a backup first." })).Present(dialog);
                    if (!await Ui.Confirm(dialog, "Restore this reviewed emulator backup?", "Confirm backup restore")) return;
                    var result = await Task.Run(() => EmulatorBackups.Restore(emulator, path, root, CancellationToken.None));
                    await Ui.Message(dialog, "Backup restored." + (String.IsNullOrWhiteSpace(result) ? "" : "\n\nThe replaced files were backed up first:\n" + result)); await reload();
                }),
                Ui.Action("Pin / unpin", async () => { var path = selected(); if (!library.Experience.PinnedBackupPaths.Remove(path)) library.Experience.PinnedBackupPaths.Add(path); Store.Save(library); await reload(); }),
                Ui.Action("Retention settings", async () =>
                {
                    var count = await TextPromptDialog.Ask(dialog, "Backup retention", "Emulator backup archives to keep (0 keeps all)", library.Experience.BackupArchiveCount.ToString());
                    int value; if (count == null) return; if (!int.TryParse(count, out value) || value < 0 || value > 10000) throw new IOException("Enter a number from 0 to 10000.");
                    library.Experience.BackupArchiveCount = value; Store.Save(library);
                }),
                Ui.Action("Preview cleanup", async () =>
                {
                    var files = await Task.Run(() => BackupRetention.Archives(library, CancellationToken.None)); var candidates = BackupRetention.CleanupCandidates(library, files);
                    await new ResultsDialog("Backup cleanup preview", candidates.Count == 0 ? new[] { "No unpinned backups exceed the configured limits." } : candidates).Present(dialog);
                    if (candidates.Count == 0 || !await Ui.Confirm(dialog, "Delete the " + candidates.Count + " reviewed, unpinned backup files?", "Confirm cleanup")) return;
                    await Task.Run(() => BackupRetention.Delete(library, candidates)); await reload();
                }),
                Ui.Action("Open backup folder", () => { Directory.CreateDirectory(root); Platform.Open(root); }),
                Ui.Action("Refresh", reload),
                Ui.Action("Close", () => dialog.Close()));
            var layout = new DockPanel();
            var hint = Ui.Hint("Archives in " + root + ". Verification re-reads every file against its recorded SHA-256. Pinned archives are kept by cleanup."); DockPanel.SetDock(hint, Dock.Top); layout.Children.Add(hint);
            DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions); layout.Children.Add(list);
            dialog.Body = layout; await reload(); await dialog.Present(this);
        }
    }
}
