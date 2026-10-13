using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EmulatorHub
{
    // User tools (profiles, guest mode, goals and breaks, diagnostics, catalog sharing) and profile transfer, matching
    // the Windows "User tools" and "Profile transfer and reviewed sync" dialogs. Logic lives in FishBowl.Profiles.cs.
    public partial class MainWindow
    {
        private int guestLaunches;
        private readonly Dictionary<string, int> breakNotices = new Dictionary<string, int>();
        private readonly DispatcherTimer breakTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };

        private Task ManageLibraryProfiles() { return ShowUserTools(); }

        private async Task ShowUserTools()
        {
            UserTools.Ensure(library);
            var s = library.UserTools;
            var dialog = new FishDialog("User tools", 860, 640);
            var tabs = new TabControl();

            // Personal
            var personal = new StackPanel { Spacing = 6 };
            Func<string> activeName = () => s.Users.First(x => x.Id == s.ActiveId).Name;
            var users = Ui.Combo(s.Users.Select(x => x.Name), activeName());
            personal.Children.Add(Ui.Caption("Active user profile")); personal.Children.Add(users);
            personal.Children.Add(Ui.Hint("Profiles keep favorites, play status and history, the play queue, smart lists, goals and appearance separate. Games, emulators and saves are shared."));
            personal.Children.Add(Ui.Actions(
                Ui.Action("Switch", async () =>
                {
                    var chosen = s.Users.FirstOrDefault(x => x.Name == users.SelectedItem as string); if (chosen == null) return;
                    if (chosen.Id == s.ActiveId) { dialog.Close(); return; }
                    UserTools.Switch(library, chosen.Id);
                    RecoverLibrarySessions(); Store.Save(library); RefreshGameLibrary(); dialog.Close();
                    SetStatus("Switched to " + chosen.Name + ".");
                    await Ui.Message(this, "Switched to " + chosen.Name + ". Restart FishBowl to apply this profile's theme and text settings.");
                }, true),
                Ui.Action("New profile", async () =>
                {
                    var name = await PromptText(dialog, "New user profile", "Name", ""); if (name == null) return;
                    var created = UserTools.Create(library, name); Store.Save(library);
                    users.ItemsSource = s.Users.Select(x => x.Name).ToList(); users.SelectedItem = created.Name;
                }),
                Ui.Action("Guest browser", async () => { dialog.Close(); await ShowGuestBrowser(); }),
                Ui.Action("Profile transfer and sync...", () => ShowProfileTransfer(dialog))));
            var goal = new NumericUpDown { Minimum = 0, Maximum = 10080, Value = s.WeeklyMinutes, FormatString = "0", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            var breaks = new NumericUpDown { Minimum = 0, Maximum = 240, Value = s.BreakMinutes, FormatString = "0", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            var controller = Ui.Check("Enable controller navigation", s.Controller);
            personal.Children.Add(Ui.Caption("Rolling 7-day goal (minutes; 0 off)")); personal.Children.Add(goal);
            personal.Children.Add(Ui.Caption("Break reminder (minutes; 0 off)")); personal.Children.Add(breaks);
            personal.Children.Add(Ui.Caption("Controller navigation")); personal.Children.Add(controller);
            personal.Children.Add(Ui.Caption("Recorded last 7 days")); personal.Children.Add(Ui.Hint(ProfileTools.WeeklyGoal(library, DateTime.UtcNow)));
            personal.Children.Add(Ui.Caption("Controller controls"));
            personal.Children.Add(Ui.Hint("Left stick or D-pad: move selection/focus · A: activate · B: close dialogs · shoulders: tabs · Start: menu. Only while FishBowl is active. Linux reads controllers through the kernel's input devices."));
            personal.Children.Add(Ui.Action("Save personal settings", () =>
            {
                s.WeeklyMinutes = (int)(goal.Value ?? 0); s.BreakMinutes = (int)(breaks.Value ?? 0); s.Controller = controller.IsChecked == true;
                Store.Save(library); SetStatus("Personal settings saved.");
            }, true));
            tabs.Items.Add(new TabItem { Header = "Personal", Content = new ScrollViewer { Content = personal, Padding = new Thickness(4, 8) } });

            // Check and repair
            var repair = new StackPanel { Spacing = 6 };
            var games = library.Games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            var choice = new ComboBox { ItemsSource = games.Select(g => g.Title).ToList(), SelectedIndex = games.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch };
            var selected = SelectedLibraryGame(); if (selected != null && games.Contains(selected)) choice.SelectedIndex = games.IndexOf(selected);
            Func<GameEntry> chosenGame = () => { if (choice.SelectedIndex < 0) throw new IOException("Choose a game."); return games[choice.SelectedIndex]; };
            repair.Children.Add(Ui.Hint("Choose a game for diagnostics. Integrity checks compare raw files with a local XML DAT; archives and disc sets are not unpacked."));
            repair.Children.Add(choice);
            repair.Children.Add(Ui.Actions(
                Ui.Action("Diagnose selected game", () => ShowLaunchTroubleshooting(dialog, chosenGame(), null)),
                Ui.Action("Check selected file against DAT", async () =>
                {
                    var game = chosenGame();
                    var dat = await Ui.PickFile(dialog, "Choose local reference DAT", "XML DAT files|*.dat;*.xml"); if (dat == null) return;
                    var path = GamePlay.LaunchPath(game);
                    SetStatus("Checking " + Path.GetFileName(path) + "...");
                    var result = await Task.Run(() => RomIntegrity.Check(dat, path, CancellationToken.None, delegate { }));
                    SetStatus("Integrity check finished.");
                    await ShowReport(dialog, "Integrity result", result);
                }),
                Ui.Action("Library health", async () => { dialog.Close(); await ShowLibraryMaintenance(); }),
                Ui.Action("Devices and emulator checks", ShowDiagnostics),
                Ui.Action("Undo last bulk edit", async () => { var text = UserTools.UndoBulk(library); RefreshGameLibrary(); await ShowReport(dialog, "Bulk undo", text); })));
            repair.Children.Add(Ui.Hint("Undo keeps the last 10 bulk operations. It restores changed fields only and stops if later changes conflict."));
            tabs.Items.Add(new TabItem { Header = "Check and repair", Content = new ScrollViewer { Content = repair, Padding = new Thickness(4, 8) } });

            // Share
            var share = new StackPanel { Spacing = 6 };
            var scope = Ui.Combo(new[] { "All games", "Favorites", "Play queue", "Choose games" }, "All games");
            var pick = new ListBox { ItemsSource = games.Select(g => new CheckBox { Content = g.Title, Tag = g, Foreground = Ui.P.InkBrush }).ToList(), MaxHeight = 180, IsVisible = false, Background = Ui.P.SurfaceBrush };
            scope.SelectionChanged += delegate { pick.IsVisible = scope.SelectedItem as string == "Choose games"; };
            var paths = Ui.Check("Include local file paths (private)", false);
            var history = Ui.Check("Include progress and play history", false);
            var art = Ui.Check("Include local artwork thumbnails", false);
            share.Children.Add(Ui.Caption("Games to share")); share.Children.Add(scope); share.Children.Add(pick);
            share.Children.Add(Ui.Caption("Catalog privacy")); share.Children.Add(paths); share.Children.Add(history);
            share.Children.Add(Ui.Caption("Artwork")); share.Children.Add(art);
            share.Children.Add(Ui.Caption("Included by default"));
            share.Children.Add(Ui.Hint("Titles, platform, genre, descriptions and tags. Preview before sharing: those fields can also contain personal information."));
            share.Children.Add(Ui.Action("Export HTML catalog", async () =>
            {
                IEnumerable<GameEntry> source = library.Games;
                var mode = scope.SelectedItem as string;
                if (mode == "Favorites") source = library.Games.Where(g => g.Favorite);
                else if (mode == "Play queue") source = library.Games.Where(g => (library.PlayQueue ?? new List<string>()).Contains(g.Id));
                else if (mode == "Choose games") source = pick.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (GameEntry)c.Tag);
                var chosen = source.ToList();
                if (chosen.Count == 0) throw new IOException("Choose at least one game to export.");
                var target = await Ui.SaveFile(dialog, "Export HTML catalog", "HTML catalog|*.html", "FishBowl catalog.html"); if (target == null) return;
                bool includePaths = paths.IsChecked == true, includeHistory = history.IsChecked == true, includeArt = art.IsChecked == true;
                SetStatus("Preparing " + chosen.Count + " games...");
                var html = await Task.Run(() => UserTools.Html(chosen, includePaths, includeHistory, includeArt ? new Func<string, string>(CatalogArt) : null, CancellationToken.None));
                File.WriteAllText(target, html, new UTF8Encoding(false)); SetStatus("Catalog exported.");
                await ShowReport(dialog, "Catalog exported", "Saved to " + target + "\nOpen it in a browser to preview. It works offline and contains no remote resources.");
            }, true));
            tabs.Items.Add(new TabItem { Header = "Share", Content = new ScrollViewer { Content = share, Padding = new Thickness(4, 8) } });

            var close = Ui.Action("Close", () => dialog.Close()); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 10, 0, 0);
            var layout = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); layout.Children.Add(close); layout.Children.Add(tabs);
            dialog.Body = layout; await dialog.Present(this);
        }

        // A 128 px PNG thumbnail of a cover as base64 for the HTML catalog (CatalogArt in the Windows build).
        private static string CatalogArt(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path) || new FileInfo(path).Length > 10485760) return null;
                using (var source = new Bitmap(path))
                {
                    if (source.PixelSize.Width > 12000 || source.PixelSize.Height > 12000) return null;
                    double scale = Math.Min(128.0 / source.PixelSize.Width, 128.0 / source.PixelSize.Height);
                    using (var scaled = source.CreateScaledBitmap(new PixelSize(Math.Max(1, (int)(source.PixelSize.Width * scale)), Math.Max(1, (int)(source.PixelSize.Height * scale)))))
                    using (var stream = new MemoryStream()) { scaled.Save(stream); return Convert.ToBase64String(stream.ToArray()); }
                }
            }
            catch (Exception) { return null; }
        }

        private async Task<string> PromptText(Window owner, string title, string label, string value)
        {
            var dialog = new FishDialog(title, 520); var body = new StackPanel { Spacing = 6 };
            var field = Ui.Field(value); body.Children.Add(Ui.Caption(label)); body.Children.Add(field);
            body.Children.Add(dialog.Footer("Create", () => Task.FromResult(!String.IsNullOrWhiteSpace(field.Text))));
            dialog.Body = body; dialog.Opened += delegate { field.Focus(); };
            await dialog.Present(owner);
            return dialog.Confirmed ? field.Text : null;
        }

        private static Task ShowReport(Window owner, string title, string text)
        {
            var dialog = new FishDialog(title, 720, 460);
            var box = Ui.Paragraphs(text); var close = Ui.Action("Close", () => dialog.Close()); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 10, 0, 0);
            var layout = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); layout.Children.Add(close); layout.Children.Add(new ScrollViewer { Content = box });
            dialog.Body = layout; return dialog.Present(owner);
        }

        // Launch troubleshooting (UserTools.Troubleshoot): checks without changing or downloading files.
        private string DiagnoseGame(GameEntry g)
        {
            if (g == null) return "Select a game first.";
            var text = new StringBuilder();
            var emulator = GameLibraryQuery.Native(g) ? null : GamePlay.LaunchEmulator(library, g);
            text.AppendLine("Game: " + g.Title);
            text.AppendLine(File.Exists(GamePlay.LaunchPath(g)) ? "✓ Game file exists." : "Game file is missing. Use Library → Repair game paths, or Edit game to choose its location.");
            if (GameLibraryQuery.Native(g)) text.AppendLine("This game starts directly, without an emulator.");
            else if (emulator == null) text.AppendLine("Assign an emulator in Edit game.");
            else
            {
                text.AppendLine(File.Exists(emulator.Executable) ? "✓ Emulator executable exists." : "Emulator executable is missing. Edit its profile and choose the installed executable.");
                text.AppendLine("Try opening the emulator directly and loading this game. If that fails too, check the emulator's supported formats, BIOS requirements and log.");
            }
            try
            {
                var plan = GamePlay.Prepare(library, g);
                text.AppendLine("Launch validation: " + LinuxLaunch.CommandLine(plan));
                if (!String.IsNullOrEmpty(plan.Summary)) text.AppendLine("Runs with: " + plan.Summary);
                foreach (var note in plan.Notes ?? new List<string>()) text.AppendLine(note);
                text.AppendLine("FishBowl's paths and arguments pass validation. If the emulator exits early, check its log and per-game settings.");
            }
            catch (Exception error)
            {
                text.AppendLine("Launch validation: " + error.Message);
                text.AppendLine("Review emulator arguments, selected build, launch profile and working folder. Close any existing emulator instance, then retry.");
            }
            text.AppendLine("This check does not change or download files.");
            return text.ToString();
        }

        private async Task ShowLaunchTroubleshooting(Window owner, GameEntry game, string error)
        {
            var dialog = new FishDialog("Launch troubleshooting", 760, 520);
            var report = Ui.Paragraphs(((error ?? "") + "\n\n" + DiagnoseGame(game)).Trim());
            var actions = new List<Control>();
            if (!UserTools.Guest)
            {
                actions.Add(Ui.Action("Launch profiles", async () => { var emulator = GamePlay.LaunchEmulator(library, game); if (emulator == null) throw new IOException("Assign an emulator first."); await ShowLaunchProfiles(dialog, emulator); report.Text = DiagnoseGame(game); }));
            }
            actions.Add(Ui.Action("Recheck", () => { report.Text = DiagnoseGame(game); }));
            actions.Add(Ui.Action("Close", () => dialog.Close()));
            var row = Ui.Actions(actions.ToArray()); var layout = new DockPanel(); DockPanel.SetDock(row, Dock.Bottom); layout.Children.Add(row); layout.Children.Add(new ScrollViewer { Content = report });
            dialog.Body = layout; await dialog.Present(owner);
        }

        // ----- Guest mode --------------------------------------------------------------------------------------------

        private async Task ShowGuestBrowser()
        {
            if (sessionTrackers.Count > 0) throw new IOException("Finish emulator sessions before entering guest mode.");
            var snapshot = UserTools.BeginGuest(library);
            try
            {
                var dialog = new FishDialog("Guest browser — Library changes disabled", 640, 560);
                var filter = Ui.Field(); filter.Watermark = "Filter games";
                var list = new ListBox { Background = Ui.P.SurfaceBrush, Foreground = Ui.P.InkBrush };
                List<GameEntry> shown = new List<GameEntry>();
                Action load = () =>
                {
                    shown = library.Games.Where(g => (g.Title ?? "").IndexOf(filter.Text ?? "", StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
                    list.ItemsSource = shown.Select(g => g.Title).ToList(); if (shown.Count > 0) list.SelectedIndex = 0;
                };
                filter.TextChanged += delegate { load(); }; load();
                Func<Task> launch = async () => { if (list.SelectedIndex >= 0 && list.SelectedIndex < shown.Count) await LaunchGame(shown[list.SelectedIndex]); };
                list.DoubleTapped += async delegate { await Ui.Run(dialog, launch); };
                var row = Ui.Actions(Ui.Action("Launch", launch, true),
                    Ui.Action("Details", async () => { if (list.SelectedIndex < 0) return; var g = shown[list.SelectedIndex]; await ShowReport(dialog, "Game details", g.Title + "\n" + g.Genre + "\n" + g.Description); }),
                    Ui.Action("Leave guest mode", () => dialog.Close()));
                dialog.Closing += async (sender, e) =>
                {
                    if (UserTools.ActiveLaunches > 0) { e.Cancel = true; await Ui.Message(dialog, "Close the launched emulator before leaving guest mode.", "Guest browser"); }
                };
                var layout = new DockPanel(); DockPanel.SetDock(filter, Dock.Top); DockPanel.SetDock(row, Dock.Bottom);
                filter.Margin = new Thickness(0, 0, 0, 8); layout.Children.Add(filter); layout.Children.Add(row); layout.Children.Add(list);
                dialog.Body = layout; await dialog.Present(this);
            }
            finally
            {
                UserTools.EndGuest(library, snapshot);
                RefreshGameLibrary(); SetStatus("Left guest mode. Guest changes were discarded.");
            }
        }

        // Guest launches keep no journal; they only block profile changes until the program exits (as on Windows).
        private bool TrackGuestLaunch(GameEntry game, Process process)
        {
            if (!UserTools.Guest) return false;
            guestLaunches++; UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches;
            try
            {
                process.EnableRaisingEvents = true;
                process.Exited += delegate { Ui.Post(() => { guestLaunches = Math.Max(0, guestLaunches - 1); UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches; process.Dispose(); }); };
                if (process.HasExited) { guestLaunches = Math.Max(0, guestLaunches - 1); UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches; }
            }
            catch (Exception error) { Store.Log("Guest process tracking: " + error.Message); guestLaunches = Math.Max(0, guestLaunches - 1); UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches; }
            return true;
        }

        // ----- Before a launch: controller reminder and confirmation ------------------------------------------------

        private async Task<bool> ConfirmGameLaunch(GameEntry game, GamePlay.LaunchPlan plan)
        {
            var reminder = ProfileTools.ControllerReminder(library, game);
            if (reminder != null) await Ui.Message(this, reminder.Name + "\n\n" + reminder.Notes, "Controller reminder");
            if (library.Theme != null && library.Theme.ConfirmBeforeGameLaunch)
                return await Ui.Confirm(this, "Launch " + game.Title + "?\n\n" + LinuxLaunch.CommandLine(plan), "Launch game");
            return true;
        }

        // ----- Break reminders ----------------------------------------------------------------------------------------

        private void ConfigureBreakReminders()
        {
            breakTimer.Tick += delegate { CheckBreakReminders(); };
            breakTimer.Start();
        }

        private void CheckBreakReminders()
        {
            var settings = library.UserTools;
            if (UserTools.Guest || settings == null || settings.BreakMinutes <= 0) return;
            foreach (var session in (library.PlaySessions ?? new List<PlaySession>()).Where(x => x.EndedAt == null && sessionTrackers.ContainsKey(x.Id)))
            {
                int periods = ProfileTools.BreakPeriods(settings, session, DateTime.UtcNow);
                int shown; if (periods <= 0 || (breakNotices.TryGetValue(session.Id, out shown) && periods <= shown)) continue;
                breakNotices[session.Id] = periods;
                var text = "You've played for " + periods * settings.BreakMinutes + " minutes. Take a break when convenient.";
                SetStatus(text); Notify("FishBowl break reminder", text); // notify-send, as for save alerts
            }
        }

        // ----- Profile transfer -----------------------------------------------------------------------------------------

        private async Task ShowProfileTransfer(Window owner)
        {
            var dialog = new FishDialog("Profile transfer and reviewed sync", 900, 520);
            var text = Ui.Paragraphs(ProfileTransfer.Explanation.Replace("\r\n", "\n"));
            var row = Ui.Actions(
                Ui.Action("Export profile and Library", async () =>
                {
                    var target = await Ui.SaveFile(dialog, "Export profile and Library", "FishBowl transfer JSON|*.fishbowl-transfer.json", "FishBowl-transfer.fishbowl-transfer.json"); if (target == null) return;
                    ProfileTransfer.Export(library, target); Store.Save(library); SetStatus("Exported profile and Library to " + target + ".");
                }, true),
                Ui.Action("Preview Library sync", () => ImportTransfer(dialog, false)),
                Ui.Action("Import personal profile", () => ImportTransfer(dialog, true)),
                Ui.Action("Check cloud conflicts", async () =>
                {
                    var folder = await Ui.PickFolder(dialog, "Choose your cloud client's local save/backup folder"); if (folder == null) return;
                    var found = await Task.Run(() => ProfileTransfer.CloudConflicts(folder, CancellationToken.None));
                    await new ResultsDialog("Cloud conflict review", found.Count == 0 ? new[] { "No filenames indicating conflict copies were found. This does not verify cloud upload completion or detect every conflict." } : found.ToArray()).Present(dialog);
                }),
                Ui.Action("Close", () => dialog.Close()));
            var layout = new DockPanel(); DockPanel.SetDock(row, Dock.Bottom); layout.Children.Add(row); layout.Children.Add(new ScrollViewer { Content = text });
            dialog.Body = layout; await dialog.Present(owner);
        }

        private async Task ImportTransfer(Window owner, bool profileOnly)
        {
            var file = await Ui.PickFile(owner, profileOnly ? "Import personal profile" : "Preview Library sync", "FishBowl transfer JSON|*.fishbowl-transfer.json;*.json"); if (file == null) return;
            if (sessionTrackers.Count > 0 || UserTools.Guest) throw new IOException("Finish active sessions and background work before changing Library paths or profiles.");
            var package = ProfileTransfer.Read(file);
            var plan = ProfileTransfer.Plan(library, package);
            if (profileOnly)
            {
                if (package.Profile == null) throw new IOException("Package has no personal profile.");
                if (!await Ui.Confirm(owner, package.Profile.Name + "\nPersonal progress for matching game IDs will be imported as a new profile. Emulator save routes and queues remain subject to local availability.", "Import personal profile", "Import", "Cancel")) return;
                var user = ProfileTransfer.ImportProfile(library, package); Store.Save(library);
                SetStatus("Imported profile " + user.Name + ". Switch to it in User tools."); return;
            }
            var dialog = new FishDialog("Review Library synchronization", 950, 640);
            var rows = plan.Select(c => new CheckBox { Content = c.Status + " · " + c.Incoming.Title + " · " + (c.Incoming.Path ?? ""), Tag = c, IsChecked = c.Status == "Add" || c.Status == "Update", Foreground = Ui.P.InkBrush }).ToList();
            var list = new ListBox { ItemsSource = rows, Background = Ui.P.SurfaceBrush };
            var row = Ui.Actions(
                Ui.Action("Inspect selected", async () =>
                {
                    var box = list.SelectedItem as CheckBox; if (box == null) return; var change = (TransferChange)box.Tag;
                    await ShowReport(dialog, "Incoming metadata review", "LOCAL:\n" + Json.Serialize(change.Existing) + "\n\nINCOMING:\n" + Json.Serialize(change.Incoming));
                }),
                Ui.Action("Apply checked changes", () =>
                {
                    ProfileTransfer.Apply(library, rows.Where(r => r.IsChecked == true).Select(r => (TransferChange)r.Tag));
                    Store.Save(library); RefreshGameLibrary(); dialog.Close(); SetStatus("Applied reviewed Library sync.");
                }, true),
                Ui.Action("Cancel", () => dialog.Close()));
            var hint = Ui.Hint(plan.Count == 0 ? "Nothing differs from this Library." : "Additions and reviewed updates are checked. Conflicts keep local data unless you check them.");
            var layout = new DockPanel(); DockPanel.SetDock(hint, Dock.Top); DockPanel.SetDock(row, Dock.Bottom); layout.Children.Add(hint); layout.Children.Add(row); layout.Children.Add(list);
            dialog.Body = layout; await dialog.Present(owner);
        }
    }
}
