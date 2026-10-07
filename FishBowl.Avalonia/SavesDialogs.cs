using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmulatorHub
{
    // "Game saves and snapshot history" (SaveHistoryDialog + GameSavesDialog + the save timeline in the Windows build).
    // Linked live saves and verified snapshots for one game; the logic is the shared SaveHistory/GameSaves.
    public class SaveHistoryDialog : FishDialog
    {
        private const string InGame = "In-game saves", States = "Save states";
        private readonly LibraryData library;
        private readonly List<GameEntry> games;
        private readonly ComboBox gameChoice, kind;
        private readonly ListBox list = new ListBox();
        private readonly TextBlock summary = Ui.Hint("");

        private GameEntry Game { get { return gameChoice.SelectedIndex < 0 ? null : games[gameChoice.SelectedIndex]; } }
        private object Selected { get { var item = list.SelectedItem as ListBoxItem; return item == null ? null : item.Tag; } }

        public SaveHistoryDialog(LibraryData library, GameEntry selected) : base("Game saves and snapshot history", 980, 640)
        {
            this.library = library; SaveHistory.EnsureData(library);
            games = library.Games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            gameChoice = Ui.Combo(games.Select(g => g.Title ?? "(untitled)"), null); gameChoice.MinWidth = 360;
            if (selected != null && games.Contains(selected)) gameChoice.SelectedIndex = games.IndexOf(selected);
            kind = Ui.Combo(new[] { InGame, States }, InGame); kind.MinWidth = 170; kind.HorizontalAlignment = HorizontalAlignment.Left;
            gameChoice.SelectionChanged += delegate { Reload(); }; kind.SelectionChanged += delegate { Reload(); };
            list.Background = Ui.P.SurfaceBrush; list.Foreground = Ui.P.InkBrush; list.MinHeight = 240;
            list.DoubleTapped += async delegate { await Ui.Run(this, () => { OpenLocation(); return Task.CompletedTask; }); };

            var top = new StackPanel { Spacing = 4 };
            top.Children.Add(Ui.Hint("Link the game's original save file or folder, then create verified snapshots. Shared memory cards may contain several games. Save states depend on the emulator and build; FishBowl does not load them automatically."));
            var pickers = new WrapPanel(); gameChoice.Margin = new Thickness(0, 0, 8, 0); pickers.Children.Add(gameChoice); pickers.Children.Add(kind); top.Children.Add(pickers);
            top.Children.Add(summary);

            var actions = new StackPanel { Spacing = 0 };
            actions.Children.Add(Ui.Actions(Ui.Action("Link file", LinkFiles), Ui.Action("Link folder", LinkFolder), Ui.Action("Unlink", Unlink),
                Ui.Action("Create snapshot", CreateSnapshot, true), Ui.Action("Restore", Restore), Ui.Action("Compare previous", ComparePrevious)));
            actions.Children.Add(Ui.Actions(Ui.Action("Export", Export), Ui.Action("Import", Import), Ui.Action("Pin", Pin), Ui.Action("Note / core", Note),
                Ui.Action("Copy preference", CopyPreference), Ui.Action("Retention", Retention), Ui.Action("Cleanup", Cleanup)));
            actions.Children.Add(Ui.Actions(Ui.Action("Open location", OpenLocation), Ui.Action("Emulator folder", OpenEmulatorFolder), Ui.Action("Library folder", OpenLibraryFolder), Ui.Action("Close", () => Close())));

            var layout = new DockPanel();
            DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top);
            DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
            layout.Children.Add(list);
            Body = layout;
            Reload();
        }

        private GameEntry RequireGame() { if (Game == null) throw new IOException("Add or select a game in Library first."); return Game; }
        private SaveSnapshot RequireSnapshot() { var snapshot = Selected as SaveSnapshot; if (snapshot == null) throw new IOException("Choose a snapshot."); return snapshot; }
        private string Kind { get { return kind.SelectedItem as string ?? InGame; } }
        private void Save() { Store.Save(library); Reload(); }

        private static string LocalDate(string value) { DateTime date; return DateTime.TryParse(value, out date) ? date.ToLocalTime().ToString("g") : value; }

        private void Reload()
        {
            var selected = Selected; var items = new List<ListBoxItem>(); var game = Game;
            if (game != null)
            {
                foreach (var link in (game.Saves ?? new List<GameSaveEntry>()).Where(s => s.Kind == Kind && !library.SaveSnapshots.Any(p => p.Path == s.Path)))
                {
                    string state = !File.Exists(link.Path) && !Directory.Exists(link.Path) ? "Missing" : link.IsFolder ? "Folder" : "File";
                    items.Add(Row("Live link  ·  " + state + "  ·  Original", link.Path, link));
                }
                foreach (var snapshot in library.SaveSnapshots.Where(s => s.GameId == game.Id && s.Kind == Kind).OrderByDescending(s => s.CreatedAt))
                    items.Add(Row((snapshot.Pinned ? "★ " : "") + "Snapshot  ·  " + LocalDate(snapshot.CreatedAt) + "  ·  " + SaveHistory.Bytes(snapshot.Bytes) + "  ·  " + (snapshot.EmulatorName + " " + snapshot.EmulatorVersion).Trim(),
                        (snapshot.Note ?? snapshot.Core ?? "") + (String.IsNullOrWhiteSpace(snapshot.Source) ? "" : "  ·  from " + snapshot.Source), snapshot));
            }
            list.ItemsSource = items;
            var again = items.FirstOrDefault(i => i.Tag == selected); if (again != null) list.SelectedItem = again;
            int count = game == null ? 0 : library.SaveSnapshots.Count(s => s.GameId == game.Id);
            summary.Text = game == null ? "Add games in Library to manage their saves." : count + " snapshot(s) for this game · " + SaveHistory.Bytes(library.SaveSnapshots.Where(s => s.GameId == game.Id).Sum(s => s.Bytes)) + " · copy preference: " + (game.SaveCopyPreference ?? "Ask") + " · Close the emulator before restoring.";
        }

        private static ListBoxItem Row(string title, string detail, object tag)
        {
            var panel = new StackPanel { Spacing = 1 };
            panel.Children.Add(Ui.Text(title, 13, true)); if (!String.IsNullOrWhiteSpace(detail)) panel.Children.Add(Ui.Text(detail, 12, false, Ui.P.SubtleBrush));
            return new ListBoxItem { Content = panel, Tag = tag };
        }

        // The detected emulator folder for this category, if the game's emulator has one.
        private string EmulatorFolder()
        {
            var emulator = SaveHistory.AssignedEmulator(library, RequireGame()); if (emulator == null) return null;
            string property = Kind == InGame ? "InGameSaveFolder" : "SaveStateFolder";
            var folder = new EmulatorFolderDetector().Detect(emulator).FirstOrDefault(f => f.Property == property && Directory.Exists(f.Path));
            return folder == null ? null : folder.Path;
        }

        private async Task LinkFiles()
        {
            var game = RequireGame();
            var files = await Ui.PickFiles(this, "Choose " + Kind.ToLowerInvariant() + " for " + game.Title, null, await Task.Run(() => EmulatorFolder()), true);
            foreach (var file in files) GameSaves.Link(game, file, Kind);
            if (files.Count > 0) Save();
        }

        private async Task LinkFolder()
        {
            var game = RequireGame();
            var folder = await Ui.PickFolder(this, "Choose this game's complete original save folder", await Task.Run(() => EmulatorFolder()));
            if (folder == null) return;
            if (SaveMonitor.IsManaged(library, folder)) throw new IOException("Choose an emulator's original save, not an existing library snapshot.");
            GameSaves.Link(game, folder, Kind); Save();
        }

        private void Unlink()
        {
            var link = Selected as GameSaveEntry; if (link == null) throw new IOException("Select a live save link.");
            RequireGame().Saves.Remove(link); Save();
        }

        private async Task CreateSnapshot()
        {
            var game = RequireGame(); var link = Selected as GameSaveEntry;
            if (link == null) throw new IOException("Select an original live save link.");
            SetBusy(true);
            try
            {
                var snapshot = await Task.Run(() => SaveHistory.Capture(library, game, link.Path, link.Kind, false, CancellationToken.None));
                snapshot.Note = "Manual snapshot";
            }
            finally { SetBusy(false); }
            Save();
        }

        private async Task Restore()
        {
            var game = RequireGame(); var snapshot = RequireSnapshot();
            if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before restoring a save.");
            if (String.IsNullOrWhiteSpace(snapshot.Source))
            {
                string target = snapshot.IsFolder ? await Ui.PickFolder(this, "Choose the local emulator save folder to restore into", await Task.Run(() => EmulatorFolder()))
                    : await Ui.SaveFile(this, "Choose the emulator's local save destination", "Save files|*", Path.GetFileName(snapshot.Path));
                if (target == null) return;
                snapshot.Source = target;
            }
            string preview = await Task.Run(() => SaveHistory.RestorePreview(snapshot, CancellationToken.None));
            var review = new ReviewDialog("Review save file changes", preview, "Continue"); await review.Present(this);
            if (!review.Confirmed) return;
            if (!await Ui.Confirm(this, "Restore " + game.Title + "?\n\nDestination: " + snapshot.Source + "\n\n" + SaveHistory.Compatibility(library, snapshot) + "\n\nClose the emulator first. Current contents will be backed up and retained.", "Review save restore")) return;
            SetBusy(true);
            try { await Task.Run(() => SaveHistory.Restore(library, snapshot, CancellationToken.None)); }
            finally { SetBusy(false); Save(); }
            await Ui.Message(this, "Save restored. The previous live save is retained as a pinned snapshot and an adjacent rollback copy.");
        }

        private async Task ComparePrevious()
        {
            var after = RequireSnapshot(); var before = SaveHistory.Previous(library, after);
            var changes = await Task.Run(() => SaveHistory.SnapshotChanges(before, after, CancellationToken.None));
            await new ResultsDialog("Changed snapshot files", changes.Length == 0 ? new[] { "No file-content changes found." } : new[] { "Changes describe files and bytes, not in-game progress." }.Concat(changes)).Present(this);
        }

        private async Task Export()
        {
            var snapshot = RequireSnapshot();
            var target = await Ui.SaveFile(this, "Export save bundle", "FishBowl save bundle|*.zip", (RequireGame().Title ?? "save") + " " + DateTime.Now.ToString("yyyy-MM-dd") + ".fishbowl-save.zip");
            if (target == null) return;
            if (File.Exists(target)) File.Delete(target); // The picker already asked before replacing it.
            await Task.Run(() => SaveHistory.Export(library, snapshot, target, CancellationToken.None));
            await Ui.Message(this, "Verified save bundle exported:\n" + target);
        }

        private async Task Import()
        {
            var game = RequireGame();
            var file = await Ui.PickFile(this, "Import save bundle", "FishBowl save bundle|*.zip"); if (file == null) return;
            if (!await Ui.Confirm(this, "Import this bundle into " + game.Title + "? Import creates history only; it does not restore an emulator save.", "Review save import")) return;
            await Task.Run(() => SaveHistory.Import(library, game, file, CancellationToken.None)); Save();
        }

        private void Pin() { var snapshot = RequireSnapshot(); snapshot.Pinned = !snapshot.Pinned; Save(); }

        private async Task Note()
        {
            var snapshot = RequireSnapshot();
            var note = await TextPromptDialog.Ask(this, "Snapshot note", "Note", snapshot.Note); if (note == null) return;
            var core = await TextPromptDialog.Ask(this, "Emulator core", "Optional actual core name (not automatically detected)", snapshot.Core);
            snapshot.Note = note; if (core != null) snapshot.Core = core; Save();
        }

        private async Task CopyPreference()
        {
            var game = RequireGame();
            var dialog = new FishDialog("Save copy preference", 520);
            var choice = Ui.Combo(new[] { "Ask", "Automatic copies", "Never ask" }, game.SaveCopyPreference ?? "Ask");
            var body = new StackPanel { Spacing = 6 };
            body.Children.Add(Ui.Hint("When a monitored in-game save changes, FishBowl can ask to copy it, copy it automatically into this game's history, or never ask."));
            body.Children.Add(choice);
            body.Children.Add(dialog.Footer("Save preference", () =>
            {
                string value = choice.SelectedItem as string;
                if (value == "Automatic copies" && !(game.Saves ?? new List<GameSaveEntry>()).Any(s => s.Kind == InGame && !SaveMonitor.IsManaged(library, s.Path)))
                    throw new IOException("Link this game's original save first to enable automatic copies.");
                game.SaveCopyPreference = value; return Task.FromResult(true);
            }));
            dialog.Body = body; await dialog.Present(this);
            if (dialog.Confirmed) Save();
        }

        private async Task Retention()
        {
            var settings = library.Experience;
            var dialog = new FishDialog("Save history retention", 520);
            var count = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = settings.SnapshotCount, FormatString = "0" };
            var days = new NumericUpDown { Minimum = 0, Maximum = 3650, Value = settings.SnapshotDays, FormatString = "0" };
            var megabytes = new NumericUpDown { Minimum = 0, Maximum = 1048576, Value = settings.SnapshotMegabytes, FormatString = "0" };
            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(Ui.Hint("Cleanup offers to delete unpinned snapshots beyond these limits, per game. 0 turns a limit off. Pinned snapshots are always kept."));
            body.Children.Add(Ui.Caption("Snapshots to keep per game")); body.Children.Add(count);
            body.Children.Add(Ui.Caption("Keep snapshots newer than (days)")); body.Children.Add(days);
            body.Children.Add(Ui.Caption("Snapshot storage per game (MB)")); body.Children.Add(megabytes);
            body.Children.Add(dialog.Footer("Save", () =>
            {
                settings.SnapshotCount = (int)(count.Value ?? 0); settings.SnapshotDays = (int)(days.Value ?? 0); settings.SnapshotMegabytes = (long)(megabytes.Value ?? 0);
                return Task.FromResult(true);
            }));
            dialog.Body = body; await dialog.Present(this);
            if (dialog.Confirmed) Save();
        }

        private async Task Cleanup()
        {
            var game = RequireGame(); var candidates = SaveHistory.CleanupCandidates(library, game);
            await new ResultsDialog("Snapshot cleanup preview", candidates.Count == 0 ? new[] { "Nothing exceeds the configured limits. Pinned snapshots are preserved." } : candidates.Select(s => LocalDate(s.CreatedAt) + " / " + SaveHistory.Bytes(s.Bytes) + " / " + s.Path)).Present(this);
            if (candidates.Count == 0 || !await Ui.Confirm(this, "Delete these " + candidates.Count + " unpinned managed snapshots?", "Confirm cleanup")) return;
            try { await Task.Run(() => { foreach (var snapshot in candidates) SaveHistory.Remove(library, snapshot); }); }
            finally { Save(); }
        }

        private void OpenLocation()
        {
            var snapshot = Selected as SaveSnapshot; var link = Selected as GameSaveEntry;
            string path = snapshot != null ? snapshot.Path : link == null ? null : link.Path;
            if (path == null) throw new IOException("Select a save.");
            string folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!Directory.Exists(folder)) throw new IOException("Save folder is unavailable.");
            Platform.Open(folder);
        }

        private async Task OpenEmulatorFolder()
        {
            var folder = await Task.Run(() => EmulatorFolder());
            if (folder == null) throw new IOException("Assign an emulator to this game and set its save folder in emulator information.");
            Platform.Open(folder);
        }

        private void OpenLibraryFolder() { string folder = GameSaves.Root(library, RequireGame(), Kind); Directory.CreateDirectory(folder); Platform.Open(folder); }

        private void SetBusy(bool busy) { IsEnabled = !busy; Cursor = busy ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Wait) : null; }
    }

    // "Review changed save group" (SaveGroupPromptDialog): copy, skip, or review later.
    public class SaveGroupPromptDialog : FishDialog
    {
        public string Result { get; private set; }

        public SaveGroupPromptDialog(LibraryData library, List<string> paths, List<GameEntry> candidates) : base("Review changed save group", 680)
        {
            var ordered = candidates.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            var choice = Ui.Combo(ordered.Select(g => g.Title ?? "(untitled)"), null); choice.SelectedIndex = -1;
            var suggested = SaveMonitor.Suggestions(library, paths).Where(ordered.Contains).ToList();
            if (ordered.Count == 1) choice.SelectedIndex = 0; else if (suggested.Count == 1) choice.SelectedIndex = ordered.IndexOf(suggested[0]);
            var body = new StackPanel { Spacing = 6 };
            body.Children.Add(Ui.Text("Copy " + paths.Count + " changed save source(s) into FishBowl?", 15, true));
            body.Children.Add(Ui.Hint("Confirm the game; suggested matches are not verified. Shared save folders and memory cards may contain several games."));
            var files = Ui.Paragraphs(String.Join("\n", paths)); files.MaxHeight = 160; body.Children.Add(files);
            body.Children.Add(choice);
            var copy = Ui.Action("Copy snapshots", async () =>
            {
                if (choice.SelectedIndex < 0) throw new IOException("Confirm the game first.");
                var game = ordered[choice.SelectedIndex]; IsEnabled = false;
                try { await Task.Run(() => SaveMonitor.CopyGroup(library, game, paths, CancellationToken.None)); }
                finally { IsEnabled = true; }
                Store.Save(library); Result = "Copied"; Close();
            }, true);
            copy.IsDefault = true;
            var skip = Ui.Action("Skip group", () => { Result = "Skipped"; Close(); });
            var later = Ui.Action("Review later", () => Close()); later.IsCancel = true;
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            row.Children.Add(copy); row.Children.Add(skip); row.Children.Add(later); body.Children.Add(row);
            Body = body;
        }
    }
}
