using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmulatorHub
{
    // Home: the same cards as Windows (Continue playing, Pinned games, Favorite emulators, Save review, Attention,
    // Storage and backups, Recent activity, Quick actions), in the order and visibility the player chose.
    public partial class MainWindow
    {
        public static readonly string[] HomeCardNames = { "Continue playing", "Pinned games", "Favorite emulators", "Save review", "Attention", "Storage and backups", "Recent activity", "Quick actions" };

        // Same rule as Windows UiPolishTools.ResolveHomeCards: saved order first, then the remaining cards, minus hidden ones.
        private List<string> ResolveHomeCards()
        {
            if (library.Experience == null) library.Experience = new ExperienceSettings();
            var available = (library.Experience.HomeCards ?? HomeCardNames.ToList()).Distinct().ToList();
            var order = library.Theme.HomeCardOrder ?? new List<string>();
            var hidden = library.Theme.HiddenHomeCards ?? new List<string>();
            return order.Where(available.Contains).Concat(available).Distinct().Where(x => !hidden.Contains(x)).ToList();
        }

        private void RefreshHomeCards()
        {
            if (libraryPages == null) return;
            companionServer?.Update(IntegrationData.CompanionSnapshot(library));
            homeCards.Children.Clear();
            if (libraryPages.SelectedIndex == HomePage) BuildHome();
        }

        private void BuildHome()
        {
            homeCards.Children.Add(Ui.Text("Home", 24, true));
            int ready = library.Emulators.Count(e => File.Exists(e.Executable) || Platform.FlatpakId(e.Executable) != null);
            int missingGames = library.Games.Count(g => !File.Exists(g.Path));
            int reviews = (library.SaveReviews ?? new List<SaveReviewItem>()).Count;
            homeCards.Children.Add(Ui.Actions(
                Chip(library.Games.Count + (library.Games.Count == 1 ? " game" : " games"), () => OpenLibraryScope(null)),
                Chip(ready + " ready emulator" + (ready == 1 ? "" : "s"), () => libraryPages.SelectedIndex = EmulatorsPage),
                Chip(reviews + " save review" + (reviews == 1 ? "" : "s"), () => OpenLibraryScope(null)),
                Chip(missingGames + " missing game paths", () => OpenLibraryScope("Missing"))));

            var last = GamePlay.LastGame(library);
            if (last != null) homeCards.Children.Add(ContinueHero(last));
            else homeCards.Children.Add(Card("Welcome", new Control[] { Wrapped(library.Games.Count == 0 ? "Add your games, and FishBowl will launch them with the right emulator and track your play time." : "Pick a game in Library to start playing."), Ui.Actions(Ui.Action("Add games", AddLibraryGames, true), Ui.Action("Add folder", AddLibraryFolder), Ui.Action("Open library", () => OpenLibraryScope(null))) }));

            var grid = new Grid(); bool twoColumns = (Bounds.Width > 0 ? Bounds.Width : Width) >= 1000;
            grid.ColumnDefinitions = new ColumnDefinitions(twoColumns ? "*,14,*" : "*");
            int index = 0;
            foreach (var name in ResolveHomeCards())
            {
                var card = HomeCard(name); if (card == null) continue;
                int column = twoColumns ? (index % 2) * 2 : 0, row = twoColumns ? index / 2 : index;
                while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                card.Margin = new Thickness(0, 0, 0, 14); Grid.SetColumn(card, column); Grid.SetRow(card, row); grid.Children.Add(card); index++;
            }
            homeCards.Children.Add(grid);
            if (index == 0) homeCards.Children.Add(Ui.Hint("All Home cards are hidden. Use Customize Home to show them."));
        }

        private Button Chip(string text, Action click)
        {
            var b = Ui.Action(text, click); b.MinHeight = 30; b.Padding = new Thickness(10, 4); return b;
        }

        private TextBlock Wrapped(string text, IBrush brush = null) { var t = Ui.Text(text, 13, false, brush); t.TextWrapping = TextWrapping.Wrap; return t; }

        private Border Card(string title, IEnumerable<Control> children)
        {
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(Ui.Text(title, 17, true));
            foreach (var child in children) stack.Children.Add(child);
            return new Border { Background = p.TopBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12), Child = stack };
        }

        private void OpenLibraryScope(string scope)
        {
            libraryPages.SelectedIndex = LibraryPage;
            if (scope != null) gameScope.SelectedItem = scope;
        }

        private Control ContinueHero(GameEntry game)
        {
            var row = new DockPanel();
            var cover = GameCover(game, 110); cover.Margin = new Thickness(0, 0, 16, 0); DockPanel.SetDock(cover, Dock.Left); row.Children.Add(cover);
            var week = GamePlay.WeekTotals(library, DateTime.Now);
            var chart = WeekChart(week, 64); chart.Margin = new Thickness(16, 0, 0, 0); DockPanel.SetDock(chart, Dock.Right); row.Children.Add(chart);
            var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(Ui.Text("Continue playing", 12, true, p.SubtleBrush));
            var title = Ui.Text(game.Title, 22, true); title.TextWrapping = TextWrapping.Wrap; words.Children.Add(title);
            words.Children.Add(Ui.Text(GameSubtitle(game), 13, false, p.SubtleBrush));
            words.Children.Add(Ui.Text((String.IsNullOrWhiteSpace(game.LastLaunched) ? "" : "Last played " + FriendlyDate(game.LastLaunched) + " · ") + (game.TotalPlaySeconds > 0 ? GamePlay.Duration(game.TotalPlaySeconds) + " played" : "Not played yet"), 13, false, p.SubtleBrush));
            bool running = sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished);
            var play = Ui.Action(running ? "Playing" : "Play", () => LaunchGame(game), !running); play.IsEnabled = !running;
            words.Children.Add(Ui.Actions(play, Ui.Action("Show in Library", () => { libraryPages.SelectedIndex = LibraryPage; SelectLibraryGame(game); }), Ui.Action("Session journal", () => ShowSessionJournal(null))));
            row.Children.Add(words);
            return new Border { Background = p.TopBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Child = row };
        }

        // Seven bars for the last seven days, like the Windows session chart.
        private Control WeekChart(Dictionary<string, long> week, double height)
        {
            long max = Math.Max(60, week.Values.DefaultIfEmpty(0).Max());
            var panel = new StackPanel { Spacing = 4 };
            var bars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Height = height };
            var labels = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var day in week)
            {
                var bar = new Border { Width = 18, Height = Math.Max(2, height * day.Value / max), Background = day.Value > 0 ? p.BlueBrush : new SolidColorBrush(Palette.Blend(p.Surface, 20)), CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Bottom };
                ToolTip.SetTip(bar, DateTime.Parse(day.Key).ToString("dddd") + ": " + GamePlay.Duration(day.Value));
                bars.Children.Add(bar);
                labels.Children.Add(new TextBlock { Text = DateTime.Parse(day.Key).ToString("ddd").Substring(0, 1), Width = 18, TextAlignment = TextAlignment.Center, FontSize = 11, Foreground = p.SubtleBrush });
            }
            panel.Children.Add(Ui.Text("This week · " + GamePlay.Duration(week.Values.Sum()), 12, false, p.SubtleBrush));
            panel.Children.Add(bars); panel.Children.Add(labels);
            return panel;
        }

        private Button GameTile(GameEntry game)
        {
            var button = new Button { Content = GameLibraryRow(game), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 6) };
            ToolTip.SetTip(button, "Play " + game.Title);
            button.Click += async delegate { await Ui.Run(this, () => LaunchGame(game)); };
            button.ContextMenu = new ContextMenu { ItemsSource = new object[] { MenuAction("Play", "play", () => LaunchGame(game)), MenuAction("Show in Library", "library", () => { libraryPages.SelectedIndex = LibraryPage; SelectLibraryGame(game); }) } };
            return button;
        }

        private Border HomeCard(string name)
        {
            int count = Math.Max(1, Math.Min(12, library.Experience.HomeTileCount <= 0 ? 6 : library.Experience.HomeTileCount));
            var items = new List<Control>();
            switch (name)
            {
                case "Continue playing":
                    {
                        var hero = GamePlay.LastGame(library);
                        var games = GamePlay.Recent(library, count + 1).Where(g => hero == null || g.Id != hero.Id).Take(count).ToList();
                        items.AddRange(games.Select(g => (Control)GameTile(g)));
                        if (games.Count == 0) { items.Add(Wrapped(hero == null ? "Games you launch from FishBowl will appear here." : "Other games you play from FishBowl will appear here.", p.SubtleBrush)); items.Add(Ui.Action("Open Game Library", () => OpenLibraryScope(null))); }
                        break;
                    }
                case "Pinned games":
                    {
                        var games = library.Games.Where(g => g.Pinned || g.Favorite).OrderByDescending(g => g.Pinned).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).Take(count).ToList();
                        items.AddRange(games.Select(g => (Control)GameTile(g)));
                        if (games.Count == 0) { items.Add(Wrapped("Pin a game or mark it Favorite in Library.", p.SubtleBrush)); items.Add(Ui.Action("Choose games", () => OpenLibraryScope(null))); }
                        break;
                    }
                case "Favorite emulators":
                    {
                        var emulators = library.Emulators.Where(e => e.Favorite).Take(count).ToList();
                        foreach (var emulator in emulators)
                        {
                            var e = emulator; bool ok = File.Exists(e.Executable);
                            items.Add(Ui.Action(e.Name + " • " + (ok ? "Ready" : "Missing"), () => ShowEmulatorInHub(e)));
                        }
                        if (emulators.Count == 0) { items.Add(Wrapped("Favorite an emulator to pin it here.", p.SubtleBrush)); items.Add(Ui.Action("Browse emulators", () => libraryPages.SelectedIndex = EmulatorsPage)); }
                        break;
                    }
                case "Save review":
                    {
                        items.Add(Wrapped((library.SaveReviews ?? new List<SaveReviewItem>()).Count + " pending changed saves"));
                        var snapshots = (library.SaveSnapshots ?? new List<SaveSnapshot>()).OrderByDescending(s => GameLibraryQuery.Date(s.CreatedAt)).Take(3).ToList();
                        foreach (var snapshot in snapshots)
                        {
                            var game = library.Games.FirstOrDefault(g => g.Id == snapshot.GameId);
                            items.Add(Wrapped((game == null ? "Unknown game" : game.Title) + " • " + snapshot.Kind + " • " + FriendlyDate(snapshot.CreatedAt), p.SubtleBrush));
                        }
                        if (snapshots.Count == 0) items.Add(Wrapped("Link an original save, then create your first snapshot.", p.SubtleBrush));
                        items.Add(Ui.Action("Save timeline", ShowLibrarySaveTimeline));
                        break;
                    }
                case "Attention":
                    {
                        int missingGames = library.Games.Count(g => !File.Exists(g.Path));
                        int missingEmulators = library.Emulators.Count(e => !File.Exists(e.Executable));
                        int unassigned = library.Games.Count(g => g.RequiresEmulatorAssignment);
                        int missingSaves = library.Games.SelectMany(g => g.Saves ?? new List<GameSaveEntry>()).Count(s => !File.Exists(s.Path) && !Directory.Exists(s.Path));
                        items.Add(Wrapped(missingGames + " missing game paths; " + missingEmulators + " missing emulator paths"));
                        if (unassigned > 0) items.Add(Wrapped(unassigned + " games need an emulator"));
                        items.Add(Wrapped(missingSaves + " unavailable linked saves", p.SubtleBrush));
                        items.Add(Ui.Actions(Ui.Action("Repair game paths", RepairLibraryPaths), Ui.Action("Setup readiness", () => ShowEmulatorManager("Setup checks"))));
                        break;
                    }
                case "Storage and backups":
                    {
                        items.Add(Wrapped("Snapshot storage: " + Bytes((library.SaveSnapshots ?? new List<SaveSnapshot>()).Sum(s => s.Bytes))));
                        items.Add(Wrapped("Last emulator backup: " + (library.Theme.LastBackupAt ?? "Not recorded"), p.SubtleBrush));
                        try { items.Add(Wrapped("Free drive space: " + Bytes(new DriveInfo(Path.GetPathRoot(Path.GetFullPath(GameStorage.Root(library)))).AvailableFreeSpace), p.SubtleBrush)); } catch { }
                        items.Add(Ui.Action("Emulator backups", () => ShowEmulatorManager("Backups")));
                        break;
                    }
                case "Recent activity":
                    {
                        var lines = ActivityTail(5);
                        foreach (var line in lines) items.Add(Wrapped(line, p.SubtleBrush));
                        if (lines.Count == 0) items.Add(Wrapped("Your launches, imports and backups will appear here.", p.SubtleBrush));
                        items.Add(Ui.Action("Open activity log", () => { if (!File.Exists(Store.LogFileName)) Store.Log("Activity log opened."); Platform.OpenTextFile(Store.LogFileName); }));
                        break;
                    }
                case "Quick actions":
                    {
                        var panel = new WrapPanel();
                        foreach (var action in new[] { Ui.Action("Add emulator", AddEmulator), Ui.Action("Organize games", () => ShowGameStorageOrganizer(null)), Ui.Action("Library", () => OpenLibraryScope(null)), Ui.Action("Session journal", () => ShowSessionJournal(null)), Ui.Action("Surprise me", ShowSurpriseMe), Ui.Action("Customize Home", CustomizeLibraryHome) })
                        { action.Margin = new Thickness(0, 3, 8, 3); panel.Children.Add(action); }
                        items.Add(panel);
                        break;
                    }
                default: return null;
            }
            return Card(name, items);
        }

        private void ShowEmulatorInHub(EmulatorProfile emulator)
        {
            libraryPages.SelectedIndex = EmulatorsPage; filterBox.Text = ""; favoritesOnly.IsChecked = false;
            var item = (emulatorList.ItemsSource as IEnumerable<object> ?? Enumerable.Empty<object>()).OfType<ListBoxItem>().FirstOrDefault(i => i.Tag as string == emulator.Id);
            if (item != null) { emulatorList.SelectedItem = item; emulatorList.ScrollIntoView(item); }
        }

        private static string Bytes(long bytes)
        {
            if (bytes < 1024 * 1024) return Math.Max(0, bytes / 1024) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB";
        }

        private static List<string> ActivityTail(int count)
        {
            try
            {
                if (!File.Exists(Store.LogFileName)) return new List<string>();
                using (var stream = new FileStream(Store.LogFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(Math.Max(0, stream.Length - 65536), SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 20 && !l.Contains("Exception")).Reverse().Take(count).Select(l => l.Length > 160 ? l.Substring(0, 160) + "…" : l).ToList();
                }
            }
            catch (IOException) { return new List<string>(); }
        }

        private Task CustomizeLibraryHome() { return CustomizeLibraryHome(this); }
        // "Customize Home, Library and saves" on Windows: card order and visibility, tile count, start page and cover shape.
        private async Task CustomizeLibraryHome(Window owner)
        {
            var dialog = new FishDialog("Customize Home", 560, 720); var body = new StackPanel { Spacing = 8 };
            var hidden = new HashSet<string>(library.Theme.HiddenHomeCards ?? new List<string>());
            var order = (library.Theme.HomeCardOrder ?? new List<string>()).Where(HomeCardNames.Contains).Concat(library.Experience.HomeCards ?? new List<string>()).Concat(HomeCardNames).Distinct().ToList();
            var list = new ListBox { Height = 280 };
            Action fill = null; fill = () =>
            {
                var selected = (list.SelectedItem as ListBoxItem)?.Tag as string;
                var rows = order.Select(name => { var check = Ui.Check(name, !hidden.Contains(name)); var captured = name; check.IsCheckedChanged += delegate { if (check.IsChecked == true) hidden.Remove(captured); else hidden.Add(captured); }; return new ListBoxItem { Content = check, Tag = name, Padding = new Thickness(6, 2) }; }).ToList();
                list.ItemsSource = rows; list.SelectedItem = rows.FirstOrDefault(r => (string)r.Tag == selected);
            };
            Action<int> move = delta => { var name = (list.SelectedItem as ListBoxItem)?.Tag as string; if (name == null) return; int i = order.IndexOf(name), j = i + delta; if (j < 0 || j >= order.Count) return; order.RemoveAt(i); order.Insert(j, name); fill(); };
            fill();
            body.Children.Add(Ui.Hint("Choose which cards Home shows and their order. Windows FishBowl uses the same settings."));
            body.Children.Add(list); body.Children.Add(Ui.Actions(Ui.Action("Move up", () => move(-1)), Ui.Action("Move down", () => move(1))));
            var tiles = new NumericUpDown { Minimum = 1, Maximum = 12, Value = library.Experience.HomeTileCount <= 0 ? 6 : library.Experience.HomeTileCount, Increment = 1, FormatString = "0", Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            var start = Ui.Combo(new[] { "Home", "Library", "Emulators" }, library.Experience.StartPage ?? "Home");
            var aspect = Ui.Combo(new[] { "Portrait", "Square", "Landscape" }, String.IsNullOrWhiteSpace(library.Theme.CoverAspect) ? "Portrait" : library.Theme.CoverAspect);
            body.Children.Add(Ui.Caption("Games per card")); body.Children.Add(tiles);
            body.Children.Add(Ui.Caption("Start page")); body.Children.Add(start);
            body.Children.Add(Ui.Caption("Cover shape")); body.Children.Add(aspect);
            body.Children.Add(dialog.Footer("Save", () =>
            {
                library.Theme.HomeCardOrder = order.ToList(); library.Theme.HiddenHomeCards = order.Where(hidden.Contains).ToList();
                library.Experience.HomeTileCount = (int)(tiles.Value ?? 6); library.Experience.StartPage = start.SelectedItem as string ?? "Home";
                library.Theme.CoverAspect = aspect.SelectedItem as string;
                Store.Save(library); RefreshGameLibrary(); return Task.FromResult(true);
            }));
            dialog.Body = new ScrollViewer { Content = body }; await dialog.Present(owner);
        }
    }
}
