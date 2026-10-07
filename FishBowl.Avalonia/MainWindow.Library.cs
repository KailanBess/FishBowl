using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private readonly ListBox gameList = new ListBox();
        private readonly TextBox gameSearch = Ui.Field();
        private readonly ComboBox gameCollection = new ComboBox();
        private readonly CheckBox gameFavorites = Ui.Check("Favorites only", false);
        private readonly StackPanel gameDetails = new StackPanel { Spacing = 8 };
        private readonly StackPanel homeCards = new StackPanel { Spacing = 14 };
        private readonly WrapPanel couchCards = new WrapPanel();
        private readonly Dictionary<string, Process> gameProcesses = new Dictionary<string, Process>();
        private bool rebuildingGames;
        private TabControl libraryPages;

        private Control BuildLibraryPages(Control emulators)
        {
            libraryPages = new TabControl();
            var panel = new DockPanel { Margin = new Thickness(14) };
            var toolbar = Ui.Actions(Ui.Action("Add games", AddLibraryGames, true), Ui.Action("Import Steam game", ImportSteamLibraryGame), Ui.Action("Edit game", EditLibraryGame),
                Ui.Action("Play", LaunchLibraryGame), Ui.Action("Remove", RemoveLibraryGame), Ui.Action("Undo removal", UndoLibraryRemoval),
                Ui.Action("Collections", ManageLibraryCollections), Ui.Action("Profiles", ManageLibraryProfiles), Ui.Action("Remote couch play", ShowRemoteCouchPlay),
                Ui.Action("Library repair", RepairLibraryPaths), Ui.Action("Artwork cleanup", CleanupLibraryArtwork), Ui.Action("Library tools", ShowLibraryIntegrations), Ui.Action("Game setup", ShowLibraryGameTools), Ui.Action("Save timeline", ShowLibrarySaveTimeline));
            DockPanel.SetDock(toolbar, Dock.Top); panel.Children.Add(toolbar);
            gameSearch.Watermark = "Search title, platform, tags or notes";
            gameSearch.TextChanged += delegate { RefreshGameLibrary(); };
            gameFavorites.IsCheckedChanged += delegate { RefreshGameLibrary(); };
            gameCollection.MinWidth = 180;
            gameCollection.SelectionChanged += delegate { if (!rebuildingGames) RefreshGameLibrary(); };
            var filters = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 4, 0, 12) };
            filters.Children.Add(gameSearch); Grid.SetColumn(gameFavorites, 1); gameFavorites.Margin = new Thickness(12, 0); filters.Children.Add(gameFavorites);
            Grid.SetColumn(gameCollection, 2); filters.Children.Add(gameCollection); DockPanel.SetDock(filters, Dock.Top); panel.Children.Add(filters);
            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,12,2*") };
            gameList.Background = p.BottomBrush; gameList.Foreground = p.InkBrush;
            gameList.SelectionChanged += delegate { ShowLibraryGameDetails(); };
            gameList.DoubleTapped += async delegate { await Ui.Run(this, LaunchLibraryGame); };
            body.Children.Add(gameList);
            var details = new ScrollViewer { Content = gameDetails }; Grid.SetColumn(details, 2); body.Children.Add(details); panel.Children.Add(body);
            var home = new ScrollViewer { Content = homeCards, Margin = new Thickness(18) };
            var couch = new ScrollViewer { Content = couchCards, Margin = new Thickness(18) };
            libraryPages.ItemsSource = new[] { new TabItem { Header = "Home", Content = home }, new TabItem { Header = "Library", Content = panel }, new TabItem { Header = "Emulators", Content = emulators }, new TabItem { Header = "Living room", Content = couch } };
            libraryPages.SelectionChanged += delegate { RefreshHomeCards(); };
            RefreshGameLibrary();
            return libraryPages;
        }

        private GameEntry SelectedLibraryGame() { return (gameList.SelectedItem as ListBoxItem)?.Tag as GameEntry; }
        private void RefreshGameLibrary()
        {
            if (rebuildingGames) return;
            rebuildingGames = true;
            try
            {
                var selectedId = SelectedLibraryGame()?.Id;
                var collectionName = gameCollection.SelectedItem as string ?? "All games";
                gameCollection.ItemsSource = new[] { "All games" }.Concat(library.Collections.Select(c => c.Name)).ToList();
                gameCollection.SelectedItem = collectionName;
                if (gameCollection.SelectedIndex < 0) gameCollection.SelectedIndex = 0;
                var collection = library.Collections.FirstOrDefault(c => c.Name == collectionName);
                var text = gameSearch.Text ?? "";
                var games = library.Games.Where(g => (!gameFavorites.IsChecked.GetValueOrDefault() || g.Favorite)
                    && (collection == null || (collection.GameIds ?? new List<string>()).Contains(g.Id))
                    && (g.Title + " " + g.ConsoleLabel + " " + g.Notes + " " + String.Join(" ", g.Tags ?? new List<string>())).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(g => g.Pinned).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToList();
                var rows = games.Select(g => new ListBoxItem { Tag = g, Content = GameLibraryRow(g), Padding = new Thickness(8) }).ToList();
                gameList.ItemsSource = rows;
                gameList.SelectedItem = rows.FirstOrDefault(r => ((GameEntry)r.Tag).Id == selectedId) ?? rows.FirstOrDefault();
                RefreshHomeCards(); ShowLibraryGameDetails();
            }
            finally { rebuildingGames = false; }
        }

        private Control GameLibraryRow(GameEntry game, bool large = false)
        {
            double coverWidth = large ? 90 : 48, coverHeight = large ? 120 : 64;
            if (library.Theme.CoverAspect == "Square") coverHeight = coverWidth; else if (library.Theme.CoverAspect == "Landscape") coverHeight = coverWidth * 0.625;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            Control artwork = Ui.Text("◈", large ? 40 : 26, true, p.BlueBrush);
            if (File.Exists(game.ArtworkPath))
            {
                try
                {
                    if (!imageCache.TryGetValue(game.ArtworkPath, out var bitmap)) { bitmap = new Bitmap(game.ArtworkPath); imageCache[game.ArtworkPath] = bitmap; }
                    artwork = new Image { Source = bitmap, Width = coverWidth, Height = coverHeight, Stretch = Stretch.Uniform };
                }
                catch (Exception error) { Store.Log("Cover unavailable: " + error.Message); }
            }
            row.Children.Add(new Border { Width = coverWidth + 4, Height = coverHeight + 4, Background = p.SurfaceBrush, CornerRadius = new CornerRadius(6), Child = artwork });
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3, MaxWidth = large ? 240 : 360 };
            words.Children.Add(Ui.Text((game.Favorite ? "★ " : "") + game.Title, large ? 20 : 15, true));
            var emulator = library.Emulators.FirstOrDefault(e => e.Id == (game.PreferredEmulatorId ?? game.EmulatorId));
            words.Children.Add(Ui.Text(game.ConsoleLabel ?? emulator?.Platform ?? "Native game", 12, false, p.SubtleBrush));
            words.Children.Add(Ui.Text((File.Exists(game.Path) ? "" : "File missing · ") + (game.PlayStatus ?? "Not started") + " · " + TimeSpan.FromSeconds(game.TotalPlaySeconds).TotalHours.ToString("0.0") + " hours", 12, false, p.SubtleBrush));
            row.Children.Add(words); return row;
        }

        private void RefreshHomeCards()
        {
            if (libraryPages == null) return;
            companionServer?.Update(IntegrationData.CompanionSnapshot(library));
            homeCards.Children.Clear(); couchCards.Children.Clear();
            homeCards.Children.Add(Ui.Text("Home", 24, true));
            homeCards.Children.Add(Ui.Text(library.Games.Count + " games · " + library.Emulators.Count + " emulators · " + TimeSpan.FromSeconds(library.Games.Sum(g => g.TotalPlaySeconds)).TotalHours.ToString("0.0") + " hours played", 14, false, p.SubtleBrush));
            homeCards.Children.Add(Ui.Actions(Ui.Action("Add games", AddLibraryGames, true), Ui.Action("Open library", () => libraryPages.SelectedIndex = 1), Ui.Action("Profiles", ManageLibraryProfiles), Ui.Action("Appearance", ShowAppearanceHub), Ui.Action("Customize home", CustomizeLibraryHome)));
            foreach (var section in (library.Theme.HomeCardOrder ?? new List<string> { "Recently played", "Favorites" }).Concat(new[] { "Recently played", "Favorites" }).Distinct().Where(s => (s == "Recently played" || s == "Favorites") && !(library.Theme.HiddenHomeCards ?? new List<string>()).Contains(s)))
            {
                homeCards.Children.Add(Ui.Text(section, 18, true));
                var entries = section == "Favorites" ? library.Games.Where(g => g.Favorite).Take(8) : library.Games.Where(g => !String.IsNullOrWhiteSpace(g.LastLaunched)).OrderByDescending(g => g.LastLaunched).Take(8);
                foreach (var game in entries) { var captured = game; var button = new Button { Content = GameLibraryRow(game), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10) }; button.Click += async delegate { await Ui.Run(this, () => LaunchGame(captured)); }; homeCards.Children.Add(button); }
                if (!entries.Any()) homeCards.Children.Add(Ui.Hint(section == "Favorites" ? "Mark games as favorites in Library to show them here." : "Your played games will appear here."));
            }
            foreach (var game in library.Games.OrderByDescending(g => g.Favorite).ThenBy(g => g.Title))
            {
                var captured = game;
                var card = new Button { Content = GameLibraryRow(game, true), Width = 380, Margin = new Thickness(6), Padding = new Thickness(14), MinHeight = 158 };
                card.Click += async delegate { await Ui.Run(this, () => LaunchGame(captured)); }; couchCards.Children.Add(card);
            }
            if (library.Games.Count == 0) couchCards.Children.Add(Ui.Hint("Add games in Library to use Living room. Use Tab and Enter to select and launch a game."));
        }

        private void ShowLibraryGameDetails()
        {
            gameDetails.Children.Clear(); var game = SelectedLibraryGame();
            if (game == null) { gameDetails.Children.Add(Ui.Hint("Add a game or select a game to view its details.")); return; }
            gameDetails.Children.Add(GameLibraryRow(game, true));
            gameDetails.Children.Add(Ui.Actions(Ui.Action("Play", LaunchLibraryGame, true), Ui.Action("Edit game", EditLibraryGame), Ui.Action(game.Favorite ? "Unfavorite" : "Favorite", () => { game.Favorite = !game.Favorite; Store.Save(library); RefreshGameLibrary(); })));
            gameDetails.Children.Add(Ui.Text(game.Description ?? "", 13)); gameDetails.Children.Add(Ui.Hint(game.Path));
            gameDetails.Children.Add(Ui.Text(game.Notes ?? "", 13));
            gameDetails.Children.Add(Ui.Actions(Ui.Action("Open game folder", () => Platform.Open(Path.GetDirectoryName(game.Path))), Ui.Action("Collections", ManageLibraryCollections)));
            if (File.Exists(game.ManualPath)) gameDetails.Children.Add(Ui.Action("Open manual", () => Platform.Open(game.ManualPath)));
        }

        private async Task AddLibraryGames()
        {
            var files = await Ui.PickFiles(this, "Add games", "Games and programs|*", library.GameLibraryRoot, true);
            foreach (var file in files)
            {
                if (library.Games.Any(g => GameLibraryRemoval.SamePath(g.Path, file))) continue;
                var emulator = library.Emulators.FirstOrDefault(e => (e.Extensions ?? new List<string>()).Any(ext => String.Equals(ext.TrimStart('.'), Path.GetExtension(file).TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
                GameLibraryRemoval.AllowPath(library, file);
                var newGame = new GameEntry { Id = Guid.NewGuid().ToString("N"), Path = file, Title = Path.GetFileNameWithoutExtension(file), EmulatorId = emulator?.Id, AddedAt = DateTime.UtcNow.ToString("o"), Tags = new List<string>(), RequiresEmulatorAssignment = emulator == null && !Platform.IsLaunchFile(file), Extras = new GameExtras { Native = emulator == null && Platform.IsLaunchFile(file), WorkingDirectory = Path.GetDirectoryName(file) } };
                if (!newGame.Extras.Native) GameRecognition.Apply(newGame, library.Emulators, true);
                library.Games.Add(newGame);
            }
            Store.Save(library); RefreshGameLibrary(); libraryPages.SelectedIndex = 1;
        }

        private async Task EditLibraryGame()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            var dialog = new FishDialog("Edit game", 680, 720); var fields = new StackPanel { Spacing = 6 };
            var title = Ui.Field(game.Title); var path = Ui.Field(game.Path); var cover = Ui.Field(game.ArtworkPath);
            var notes = Ui.Paragraphs(game.Notes, false); notes.MinHeight = 90;
            var arguments = Ui.Field(game.Arguments); var statusChoice = Ui.Combo(new[] { "Not started", "Playing", "Completed", "On hold", "Dropped" }, game.PlayStatus);
            var tags = Ui.Field(String.Join(", ", game.Tags ?? new List<string>())); var favorite = Ui.Check("Favorite", game.Favorite);
            var emulatorEntries = new[] { "Native game / shortcut" }.Concat(library.Emulators.Select(e => e.Name + " · " + e.Id.Substring(0, Math.Min(6, e.Id.Length)))).ToArray();
            var selected = library.Emulators.FindIndex(e => e.Id == (game.PreferredEmulatorId ?? game.EmulatorId)); var emulator = Ui.Combo(emulatorEntries, emulatorEntries[selected + 1]);
            fields.Children.Add(Ui.Caption("Title")); fields.Children.Add(title); fields.Children.Add(Ui.Hint("The library title does not rename the game file."));
            fields.Children.Add(Ui.Caption("Game file")); fields.Children.Add(Ui.WithButton(path, Ui.Action("Browse", async () => { var f = await Ui.PickFile(dialog, "Game file"); if (f != null) path.Text = f; })));
            fields.Children.Add(Ui.Caption("Image")); fields.Children.Add(Ui.WithButton(cover, Ui.Action("Browse", async () => { var f = await Ui.PickFile(dialog, "Game image", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp"); if (f != null) cover.Text = f; })));
            fields.Children.Add(Ui.Caption("Emulator")); fields.Children.Add(emulator); fields.Children.Add(Ui.Caption("Arguments ({game} inserts the game file)")); fields.Children.Add(arguments);
            fields.Children.Add(Ui.Caption("Status")); fields.Children.Add(statusChoice); fields.Children.Add(favorite); fields.Children.Add(Ui.Caption("Tags, separated by commas")); fields.Children.Add(tags);
            fields.Children.Add(Ui.Caption("Notes")); fields.Children.Add(notes);
            fields.Children.Add(dialog.Footer("Save", async () =>
            {
                if (String.IsNullOrWhiteSpace(title.Text) || !File.Exists(path.Text)) { await Ui.Message(dialog, "Enter a title and choose an existing game file."); return false; }
                string artwork = cover.Text ?? "";
                if (!String.IsNullOrWhiteSpace(artwork)) { using (var check = new Bitmap(artwork)) { } artwork = GameLibraryEditing.RetainArtwork(artwork, Store.DataDirectory); }
                game.TitleIsCustom = game.TitleIsCustom || !String.Equals(game.Title, title.Text.Trim(), StringComparison.Ordinal); game.Title = title.Text.Trim(); game.Path = Path.GetFullPath(path.Text); game.ArtworkPath = artwork;
                game.PreferredEmulatorId = emulator.SelectedIndex > 0 ? library.Emulators[emulator.SelectedIndex - 1].Id : null; game.EmulatorId = game.PreferredEmulatorId; game.RequiresEmulatorAssignment = false; if (game.Extras == null) game.Extras = new GameExtras(); game.Extras.Native = emulator.SelectedIndex == 0;
                game.Arguments = arguments.Text; game.Notes = notes.Text; game.PlayStatus = statusChoice.SelectedItem as string; game.Favorite = favorite.IsChecked == true;
                game.Tags = (tags.Text ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Store.Save(library); RefreshGameLibrary(); return true;
            }));
            dialog.Body = new ScrollViewer { Content = fields }; await dialog.Present(this);
        }

        private Task LaunchLibraryGame() { var game = SelectedLibraryGame(); return game == null ? Task.CompletedTask : LaunchGame(game); }
        // Starts a game with its emulator (or directly for native games) and records a tracked play session.
        // Other screens (Home, Living room, collections, play queue) launch through this method.
        private async Task LaunchGame(GameEntry game)
        {
            if (game == null) return;
            if (sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished)) { await Ui.Message(this, "This game is already running."); return; }
            if (!GameLibraryQuery.Native(game) && (game.RequiresEmulatorAssignment || GameLibraryQuery.EmulatorFor(library, game) == null) && !(Platform.IsLaunchFile(game.Path) && !game.RequiresEmulatorAssignment))
            {
                if (!await ChooseGameEmulator(game)) return;
            }
            GamePlay.LaunchPlan plan;
            try { plan = GamePlay.Prepare(library, game); }
            catch (IOException error) { await Ui.Message(this, game.Title + " could not start.\n\n" + error.Message); return; }
            var info = Platform.StartInfo(plan.Program, "");
            if (Directory.Exists(plan.WorkingDirectory)) info.WorkingDirectory = plan.WorkingDirectory;
            foreach (var argument in plan.Arguments) info.ArgumentList.Add(argument);
            var beforeLaunch = SessionLedger.ProcessSnapshot();
            var started = DateTime.UtcNow; Process process;
            try { process = Process.Start(info); }
            catch (Exception error) { await Ui.Message(this, game.Title + " could not start.\n\n" + error.Message); return; }
            if (process == null) throw new IOException("The game could not be started.");
            GamePlay.RecordLaunch(library, game, started);
            StartLibrarySession(game, process, beforeLaunch, started, plan.Program);
            Store.Log("Game launched: " + game.Title + " | Program: " + plan.Program + " | Arguments: " + String.Join(" ", plan.Arguments));
            Store.Save(library); RefreshGameLibrary();
            SetStatus("Started " + game.Title + ".");
        }

        // Like Windows: a game without a usable emulator asks for one before launching.
        private async Task<bool> ChooseGameEmulator(GameEntry game)
        {
            if (library.Emulators.Count == 0) { await Ui.Message(this, "Add an emulator before launching this game."); return false; }
            var labels = library.Emulators.Select((e, i) => (String.IsNullOrWhiteSpace(e.Name) ? Path.GetFileNameWithoutExtension(e.Executable) : e.Name)).ToList();
            labels = labels.Select((name, i) => labels.Count(n => n == name) > 1 ? name + " (" + (i + 1) + ")" : name).ToList();
            var dialog = new FishDialog("Preferred emulator", 460); var body = new StackPanel { Spacing = 8 };
            var console = GameLibraryQuery.ConsoleName(game);
            var suggested = library.Emulators.FindIndex(e => (e.Extensions ?? new List<string>()).Any(x => String.Equals(x.TrimStart('.'), Path.GetExtension(game.Path ?? "").TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
            var choice = Ui.Combo(labels, labels[Math.Max(0, suggested)]);
            body.Children.Add(Ui.Text("Choose the emulator for " + game.Title + ".", 14)); if (!String.IsNullOrWhiteSpace(console)) body.Children.Add(Ui.Hint(console));
            body.Children.Add(choice); bool chosen = false;
            body.Children.Add(dialog.Footer("Use emulator", () => { chosen = true; return Task.FromResult(true); }));
            dialog.Body = body; await dialog.Present(this);
            if (!chosen || choice.SelectedIndex < 0) return false;
            var selected = library.Emulators[choice.SelectedIndex];
            game.EmulatorId = selected.Id; game.PreferredEmulatorId = selected.Id; game.PreferredBuildId = null; game.LaunchProfileName = null; game.RequiresEmulatorAssignment = false;
            if (game.Extras != null) game.Extras.Native = false;
            Store.Save(library); RefreshGameLibrary(); return true;
        }

        private async Task RemoveLibraryGame()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            if (sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished)) { await Ui.Message(this, "Close this game before removing it from the library."); return; }
            if (!await Ui.Confirm(this, "Remove " + game.Title + " from FishBowl? Its file and saves stay where they are. Use Undo removal to restore it.")) return;
            GameLibraryRemoval.RemoveGames(library, new[] { game.Id }); Store.Save(library); RefreshGameLibrary();
        }
        private void UndoLibraryRemoval() { int count = GameLibraryRemoval.Undo(library); Store.Save(library); RefreshHub(); RefreshGameLibrary(); ConfigureGameFolderWatchers(); SetStatus("Restored " + count + " entries."); }

        private async Task ManageLibraryCollections()
        {
            var dialog = new FishDialog("Collections", 540); var body = new StackPanel { Spacing = 8 }; var name = Ui.Field(); var selected = SelectedLibraryGame();
            var list = Ui.Combo(library.Collections.Select(c => c.Name), library.Collections.FirstOrDefault()?.Name);
            body.Children.Add(Ui.Caption("Collection")); body.Children.Add(list); body.Children.Add(Ui.Caption("New collection name")); body.Children.Add(name);
            body.Children.Add(Ui.Actions(Ui.Action("Create", () => { var text = (name.Text ?? "").Trim(); if (text.Length == 0 || library.Collections.Any(c => String.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase))) throw new IOException("Enter a unique collection name."); library.Collections.Add(new GameCollection { Id = Guid.NewGuid().ToString("N"), Name = text, GameIds = new List<string>() }); Store.Save(library); list.ItemsSource = library.Collections.Select(c => c.Name).ToList(); list.SelectedItem = text; RefreshGameLibrary(); }),
                Ui.Action("Add selected game", () => { var collection = library.Collections.FirstOrDefault(c => c.Name == list.SelectedItem as string); if (collection == null || selected == null) return; if (collection.GameIds == null) collection.GameIds = new List<string>(); if (!collection.GameIds.Contains(selected.Id)) collection.GameIds.Add(selected.Id); Store.Save(library); RefreshGameLibrary(); }),
                Ui.Action("Remove selected game", () => { library.Collections.FirstOrDefault(c => c.Name == list.SelectedItem as string)?.GameIds?.Remove(selected?.Id); Store.Save(library); RefreshGameLibrary(); })));
            body.Children.Add(Ui.Action("Close", dialog.Close)); dialog.Body = body; await dialog.Present(this);
        }

        private async Task RepairLibraryPaths()
        {
            var dialog = new FishDialog("Library repair", 600); var body = new StackPanel { Spacing = 8 }; var old = Ui.Field(); var replacement = Ui.Field();
            body.Children.Add(Ui.Hint("Find missing files under a moved folder. Existing files and filenames stay unchanged. Review the match count before applying."));
            body.Children.Add(Ui.Caption("Previous folder")); body.Children.Add(old); body.Children.Add(Ui.Caption("Current folder")); body.Children.Add(Ui.WithButton(replacement, Ui.Action("Browse", async () => { var folder = await Ui.PickFolder(dialog, "Current folder"); if (folder != null) replacement.Text = folder; })));
            body.Children.Add(dialog.Footer("Review and repair", async () => { int count = LibraryPaths.Rebase(library, old.Text, replacement.Text, false); if (count == 0) { await Ui.Message(dialog, "No matching missing paths were found."); return false; } if (!await Ui.Confirm(dialog, "Repair " + count + " missing paths using the matching files in this folder?")) return false; LibraryPaths.Rebase(library, old.Text, replacement.Text, true); Store.Save(library); RefreshHub(); RefreshGameLibrary(); return true; }));
            dialog.Body = body; await dialog.Present(this);
        }

        private async Task CleanupLibraryArtwork()
        {
            var backups = new List<LibraryData>();
            foreach (var file in Directory.Exists(Store.DataDirectory) ? Directory.EnumerateFiles(Store.DataDirectory).Where(f => (f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !GameLibraryRemoval.SamePath(f, Store.FileName) && !f.EndsWith("window-layout.json", StringComparison.OrdinalIgnoreCase)) || f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)).ToArray() : new string[0]) backups.Add(Json.Deserialize<LibraryData>(File.ReadAllText(file)) ?? throw new IOException("A backup cannot be read; artwork cleanup was stopped."));
            var candidates = LibraryPaths.ArtworkCandidates(library, Store.DataDirectory, backups);
            if (candidates.Count == 0) { await Ui.Message(this, "No unused generated artwork was found."); return; }
            await new ResultsDialog("Unused artwork", candidates).Present(this);
            if (!await Ui.Confirm(this, "Delete the " + candidates.Count + " reviewed unused generated artwork files?")) return;
            var current = new HashSet<string>(LibraryPaths.ArtworkCandidates(library, Store.DataDirectory, backups), Platform.PathComparer);
            foreach (var file in candidates.Where(current.Contains)) File.Delete(file);
            SetStatus("Unused artwork removed.");
        }
    }
}
