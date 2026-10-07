using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace EmulatorHub
{
    // The Library section: filters, list and cover views, game details, adding, editing, launching and removing games.
    // Filtering and sorting come from the shared GameLibraryQuery so both apps show the same games.
    public partial class MainWindow
    {
        private readonly ListBox gameList = new ListBox();
        private readonly TextBox gameSearch = Ui.Field();
        private readonly ComboBox gameScope = new ComboBox(), gameConsole = new ComboBox(), gameEmulator = new ComboBox(), gameSaves = new ComboBox(), gameSort = new ComboBox(), gameView = new ComboBox();
        private readonly TextBox gameTags = Ui.Field();
        private readonly TextBlock gameCount = new TextBlock();
        private readonly StackPanel gameDetails = new StackPanel { Spacing = 8 };
        private readonly StackPanel homeCards = new StackPanel { Spacing = 14 };
        private readonly Dictionary<string, Process> gameProcesses = new Dictionary<string, Process>();
        private bool rebuildingGames;
        private TabControl libraryPages;
        private const int HomePage = 0, LibraryPage = 1, EmulatorsPage = 2;

        private Control BuildLibraryPages(Control emulators)
        {
            libraryPages = new TabControl();
            if (library.Experience == null) library.Experience = new ExperienceSettings();
            var panel = new DockPanel { Margin = new Thickness(14, 8, 14, 10) };
            var more = Ui.Action("More", (Action)null);
            more.Flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft, ItemsSource = new object[] {
                MenuAction("Undo removal", "restore", UndoLibraryRemoval),
                MenuAction("Collections...", "library", ManageLibraryCollections),
                MenuAction("Smart lists...", "search", ShowSmartLists),
                MenuAction("Play queue...", "play", () => ShowPlayQueue(SelectedLibraryGame())),
                MenuAction("Surprise me...", "game", ShowSurpriseMe),
                MenuAction("Export library CSV...", "export", () => ExportLibraryCsv(VisibleLibraryGames())),
                new Separator(),
                MenuAction("Session journal and charts...", "note", () => ShowSessionJournal(null)),
                MenuAction("Game screenshot gallery...", "image", () => ShowScreenshotGallery(SelectedLibraryGame())),
                MenuAction("Game setup...", "settings", ShowLibraryGameTools),
                MenuAction("Save timeline...", "save", ShowLibrarySaveTimeline),
                MenuAction("Import Steam game...", "import", ImportSteamLibraryGame),
                MenuAction("Library tools...", "library", ShowLibraryIntegrations),
                new Separator(),
                MenuAction("Library maintenance...", "check", ShowLibraryMaintenance),
                MenuAction("Repair game paths...", "repair", RepairLibraryPaths),
                MenuAction("Artwork cleanup...", "image", CleanupLibraryArtwork),
                MenuAction("Profiles...", "settings", ManageLibraryProfiles),
                MenuAction("Remote couch play...", "controller", ShowRemoteCouchPlay) } };
            var toolbar = Ui.Actions(Ui.Action("Add games", AddLibraryGames, true), Ui.Action("Add folder", AddLibraryFolder), Ui.Action("Play", LaunchLibraryGame), Ui.Action("Edit game", EditLibraryGame), Ui.Action("Remove", RemoveLibraryGame), more);
            DockPanel.SetDock(toolbar, Dock.Top); panel.Children.Add(toolbar);

            gameSearch.Watermark = "Search title, platform, tags or notes";
            gameSearch.TextChanged += delegate { RefreshGameLibrary(); };
            gameTags.Watermark = "Tags"; gameTags.Text = library.Experience.LibraryFilter ?? ""; gameTags.Width = 130;
            gameTags.TextChanged += delegate { if (rebuildingGames) return; library.Experience.LibraryFilter = gameTags.Text; RefreshGameLibrary(); };
            SetChoices(gameSort, GameLibraryQuery.Sorts, library.Experience.LibrarySort ?? "Title");
            SetChoices(gameSaves, GameLibraryQuery.SaveFilters, null);
            SetChoices(gameView, new[] { "List", "Covers" }, library.Experience.LibraryView == "Artwork" ? "Covers" : "List");
            foreach (var box in new[] { gameScope, gameConsole, gameEmulator, gameSaves, gameSort, gameView })
            {
                box.MinWidth = 120; box.Margin = new Thickness(0, 0, 8, 6); box.VerticalAlignment = VerticalAlignment.Center;
                box.SelectionChanged += delegate { if (!rebuildingGames) RefreshGameLibrary(); };
            }
            ToolTip.SetTip(gameScope, "Show"); ToolTip.SetTip(gameConsole, "Filter by console"); ToolTip.SetTip(gameEmulator, "Filter by emulator");
            ToolTip.SetTip(gameSaves, "Filter by save availability"); ToolTip.SetTip(gameSort, "Sort games"); ToolTip.SetTip(gameView, "List or cover view"); ToolTip.SetTip(gameTags, "Filter by tags (comma separated)");
            gameTags.Margin = new Thickness(0, 0, 8, 6);
            var filters = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
            var choices = new WrapPanel(); foreach (Control c in new Control[] { gameScope, gameConsole, gameEmulator, gameSaves, gameSort, gameTags, gameView }) choices.Children.Add(c);
            gameSearch.Margin = new Thickness(0, 0, 0, 6);
            DockPanel.SetDock(gameSearch, Dock.Top); filters.Children.Add(gameSearch); filters.Children.Add(choices);
            DockPanel.SetDock(filters, Dock.Top); panel.Children.Add(filters);
            gameCount.Foreground = p.SubtleBrush; gameCount.Margin = new Thickness(2, 0, 0, 6); DockPanel.SetDock(gameCount, Dock.Top); panel.Children.Add(gameCount);

            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,8,2*") };
            body.ColumnDefinitions[0].MinWidth = 260; body.ColumnDefinitions[2].MinWidth = 260;
            gameList.Background = p.BottomBrush; gameList.Foreground = p.InkBrush; gameList.SelectionMode = SelectionMode.Multiple;
            gameList.SelectionChanged += delegate { if (!rebuildingGames) ShowLibraryGameDetails(); };
            gameList.DoubleTapped += async (sender, e) => { if (e.Source is Visual v && v.FindAncestorOfType<ListBoxItem>(true) != null) await Ui.Run(this, LaunchLibraryGame); };
            gameList.ContextMenu = new ContextMenu { ItemsSource = new object[] {
                MenuAction("Play", "play", LaunchLibraryGame), MenuAction("Edit game", "edit", EditLibraryGame),
                MenuAction("Favorite / unfavorite", "star", () => ToggleGameFlag(true)), MenuAction("Pin / unpin", "tag", () => ToggleGameFlag(false)),
                MenuAction("Add to play queue", "library", () => { foreach (var g in SelectedLibraryGames()) GameLists.Enqueue(library, g); Store.Save(library); SetStatus("Added to play queue."); ShowLibraryGameDetails(); RefreshHomeCards(); }),
                MenuAction("Add to collection...", "library", AddSelectedToCollection),
                MenuAction("Play sessions...", "note", () => ShowSessionJournal(SelectedLibraryGame())),
                MenuAction("Open game folder", "folder", OpenSelectedGameFolder), new Separator(),
                MenuAction("Remove selected games from FishBowl", "remove", RemoveLibraryGame) } };
            gameList.ContextRequested += (sender, e) => { if (SelectedLibraryGame() == null) e.Handled = true; };
            body.Children.Add(gameList);
            var splitter = new GridSplitter { Background = p.TopBrush, ResizeDirection = GridResizeDirection.Columns }; Grid.SetColumn(splitter, 1); body.Children.Add(splitter);
            var details = new ScrollViewer { Content = gameDetails, Padding = new Thickness(12, 0, 6, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(details, 2); body.Children.Add(details); panel.Children.Add(body);

            var home = new ScrollViewer { Content = homeCards, Padding = new Thickness(18), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var couch = new ScrollViewer { Content = LivingRoomPage() };
            libraryPages.ItemsSource = new[] { new TabItem { Header = "Home", Content = home }, new TabItem { Header = "Library", Content = panel }, new TabItem { Header = "Emulators", Content = emulators }, new TabItem { Header = "Living room", Content = couch } };
            libraryPages.SelectionChanged += (sender, e) => { if (e.Source != libraryPages) return; UpdateSectionChrome(); RefreshHomeCards(); };
            libraryPages.SelectedIndex = library.Experience.StartPage == "Library" ? LibraryPage : library.Experience.StartPage == "Emulators" ? EmulatorsPage : HomePage;
            RefreshGameLibrary();
            Ui.Post(UpdateSectionChrome);
            return libraryPages;
        }

        // The footer filter searches emulators, so it only shows in the Emulators section (as on Windows).
        private void UpdateSectionChrome()
        {
            bool emulators = libraryPages != null && libraryPages.SelectedIndex == EmulatorsPage;
            if (footerFilterLabel != null) footerFilterLabel.IsVisible = emulators;
            filterBox.IsVisible = emulators;
        }

        private static void SetChoices(ComboBox box, IEnumerable<string> items, string selected)
        {
            var list = items.ToList(); var current = selected ?? box.SelectedItem as string;
            box.ItemsSource = list; box.SelectedItem = list.Contains(current) ? current : list.FirstOrDefault();
        }

        private GameEntry SelectedLibraryGame() { return (gameList.SelectedItem as ListBoxItem)?.Tag as GameEntry; }
        private List<GameEntry> SelectedLibraryGames() { return (gameList.SelectedItems ?? new List<object>()).OfType<ListBoxItem>().Select(i => i.Tag as GameEntry).Where(g => g != null).ToList(); }
        private void SelectLibraryGame(GameEntry game)
        {
            var row = (gameList.ItemsSource as IEnumerable<ListBoxItem> ?? Enumerable.Empty<ListBoxItem>()).FirstOrDefault(r => ((GameEntry)r.Tag).Id == game.Id);
            if (row == null)
            {
                rebuildingGames = true; gameSearch.Text = ""; gameTags.Text = ""; library.Experience.LibraryFilter = "";
                foreach (var box in new[] { gameScope, gameConsole, gameEmulator, gameSaves }) box.SelectedIndex = 0;
                rebuildingGames = false; RefreshGameLibrary();
                row = (gameList.ItemsSource as IEnumerable<ListBoxItem>).FirstOrDefault(r => ((GameEntry)r.Tag).Id == game.Id);
            }
            if (row != null) { gameList.SelectedItems.Clear(); gameList.SelectedItem = row; gameList.ScrollIntoView(row); }
        }

        private List<GameEntry> VisibleLibraryGames()
        {
            IEnumerable<GameEntry> games = GameLibraryQuery.Scope(library, library.Games, gameScope.SelectedItem as string);
            games = games.Where(g => GameLibraryQuery.Matches(g, gameSearch.Text));
            games = GameLibraryQuery.Filter(library, games, gameConsole.SelectedItem as string, gameEmulator.SelectedItem as string, gameSaves.SelectedItem as string, gameTags.Text);
            return GameLibraryQuery.Sort(games, gameSort.SelectedItem as string).OrderByDescending(g => g.Pinned).ToList();
        }

        private void RefreshGameLibrary()
        {
            if (rebuildingGames || libraryPages == null) return;
            rebuildingGames = true;
            try
            {
                var selectedIds = new HashSet<string>(SelectedLibraryGames().Select(g => g.Id));
                SetChoices(gameScope, GameLibraryQuery.ScopeChoices(library), null);
                SetChoices(gameConsole, GameLibraryQuery.ConsoleChoices(library), null);
                SetChoices(gameEmulator, GameLibraryQuery.EmulatorChoices(library), null);
                library.Experience.LibrarySort = gameSort.SelectedItem as string;
                bool covers = gameView.SelectedItem as string == "Covers";
                library.Experience.LibraryView = covers ? "Artwork" : "List";
                var games = VisibleLibraryGames();
                gameList.ItemsPanel = covers ? new Avalonia.Controls.Templates.FuncTemplate<Panel>(() => new WrapPanel()) : new Avalonia.Controls.Templates.FuncTemplate<Panel>(() => new VirtualizingStackPanel());
                var rows = games.Select(g => new ListBoxItem { Tag = g, Content = covers ? GameCoverTile(g) : GameLibraryRow(g), Padding = covers ? new Thickness(6) : new Thickness(8, 6), Margin = covers ? new Thickness(4) : new Thickness(0) }).ToList();
                gameList.ItemsSource = rows;
                var keep = rows.Where(r => selectedIds.Contains(((GameEntry)r.Tag).Id)).ToList();
                if (keep.Count == 0 && rows.Count > 0) keep.Add(rows[0]);
                foreach (var row in keep) gameList.SelectedItems.Add(row);
                gameCount.Text = games.Count == library.Games.Count ? library.Games.Count + (library.Games.Count == 1 ? " game" : " games") : games.Count + " of " + library.Games.Count + " games";
            }
            finally { rebuildingGames = false; }
            RefreshHomeCards(); ShowLibraryGameDetails();
        }

        // ----- Covers and rows ----------------------------------------------------------------------------------------

        private Size CoverSize(double width)
        {
            var aspect = library.Theme.CoverAspect;
            return aspect == "Square" ? new Size(width, width) : aspect == "Landscape" ? new Size(width, width * 0.625) : new Size(width, width * 1.4);
        }

        // The game's artwork, or a lettered placeholder in the accent colour.
        private Control GameCover(GameEntry game, double width)
        {
            var size = CoverSize(width);
            var frame = new Border { Width = size.Width, Height = size.Height, Background = p.SurfaceBrush, CornerRadius = new CornerRadius(6), ClipToBounds = true };
            if (!String.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath))
            {
                try
                {
                    if (!imageCache.TryGetValue(game.ArtworkPath, out var bitmap)) { bitmap = new Bitmap(game.ArtworkPath); imageCache[game.ArtworkPath] = bitmap; }
                    frame.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                    return frame;
                }
                catch (Exception error) { Store.Log("Cover unavailable: " + error.Message); }
            }
            var words = (game.Title ?? "?").Split(new[] { ' ', '-', '_', ':' }, StringSplitOptions.RemoveEmptyEntries).Where(w => Char.IsLetterOrDigit(w[0])).Take(2).Select(w => Char.ToUpperInvariant(w[0]));
            frame.Background = new SolidColorBrush(Palette.Blend(p.Surface, 18));
            frame.Child = new TextBlock { Text = new string(words.ToArray()), FontSize = Math.Max(14, width / 3), FontWeight = FontWeight.Bold, Foreground = p.BlueBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            return frame;
        }

        private string GameSubtitle(GameEntry game)
        {
            if (GameLibraryQuery.Native(game)) return "Native game";
            var emulator = GameLibraryQuery.EmulatorFor(library, game);
            var console = GameLibraryQuery.ConsoleName(game);
            // File types such as .iso fit several consoles; the assigned emulator's platform is the better label.
            if ((String.IsNullOrWhiteSpace(console) || console.StartsWith("Needs review")) && emulator != null && !String.IsNullOrWhiteSpace(EmulatorReference.PlatformFor(emulator))) console = EmulatorReference.PlatformFor(emulator);
            return (String.IsNullOrWhiteSpace(console) ? "Unknown platform" : console) + " · " + (emulator == null ? "Needs an emulator" : emulator.Name);
        }

        private string GameProgressLine(GameEntry game)
        {
            var parts = new List<string>();
            if (!File.Exists(game.Path)) parts.Add("File missing");
            parts.Add(game.PlayStatus ?? "Not started");
            parts.Add(game.TotalPlaySeconds > 0 ? GamePlay.Duration(game.TotalPlaySeconds) : "Not played");
            if (sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished)) parts.Insert(0, "Playing now");
            return String.Join(" · ", parts);
        }

        private Control GameLibraryRow(GameEntry game, bool large = false)
        {
            var row = new DockPanel();
            var cover = GameCover(game, large ? 90 : 44); cover.Margin = new Thickness(0, 0, 12, 0); DockPanel.SetDock(cover, Dock.Left); row.Children.Add(cover);
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            var title = Ui.Text((game.Pinned ? "📌 " : "") + (game.Favorite ? "★ " : "") + game.Title, large ? 20 : 15, true); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = large ? TextWrapping.Wrap : TextWrapping.NoWrap;
            words.Children.Add(title);
            var subtitle = Ui.Text(GameSubtitle(game), 12, false, game.RequiresEmulatorAssignment ? p.PinkBrush : p.SubtleBrush); subtitle.TextTrimming = TextTrimming.CharacterEllipsis; words.Children.Add(subtitle);
            var progress = Ui.Text(GameProgressLine(game), 12, false, File.Exists(game.Path) ? p.SubtleBrush : p.PinkBrush); progress.TextTrimming = TextTrimming.CharacterEllipsis; words.Children.Add(progress);
            row.Children.Add(words); return row;
        }

        private Control GameCoverTile(GameEntry game)
        {
            var tile = new StackPanel { Width = 132, Spacing = 4 };
            tile.Children.Add(GameCover(game, 132));
            var title = Ui.Text((game.Favorite ? "★ " : "") + game.Title, 13, true); title.TextTrimming = TextTrimming.CharacterEllipsis; title.MaxLines = 2; title.TextWrapping = TextWrapping.Wrap; tile.Children.Add(title);
            var console = Ui.Text(GameLibraryQuery.Native(game) ? "Native game" : GameLibraryQuery.ConsoleName(game), 11, false, p.SubtleBrush); console.TextTrimming = TextTrimming.CharacterEllipsis; tile.Children.Add(console);
            ToolTip.SetTip(tile, game.Title + "\n" + GameSubtitle(game) + "\n" + GameProgressLine(game));
            return tile;
        }

        // ----- Details ------------------------------------------------------------------------------------------------

        private void ShowLibraryGameDetails()
        {
            gameDetails.Children.Clear(); var game = SelectedLibraryGame();
            if (game == null)
            {
                gameDetails.Children.Add(Ui.Text("No game selected", 18, true));
                var hint = Ui.Hint(library.Games.Count == 0 ? "Click Add games or Add folder to add games for your emulators. FishBowl keeps your files where they are." : "Select a game to see its details, or change the filters to show more games."); hint.TextWrapping = TextWrapping.Wrap;
                gameDetails.Children.Add(hint);
                return;
            }
            int selectedCount = SelectedLibraryGames().Count;
            var head = new DockPanel();
            var cover = GameCover(game, 120); cover.Margin = new Thickness(0, 0, 14, 0); DockPanel.SetDock(cover, Dock.Left); head.Children.Add(cover);
            var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var title = Ui.Text(game.Title, 22, true); title.TextWrapping = TextWrapping.Wrap; words.Children.Add(title);
            var subtitle = Ui.Text(GameSubtitle(game), 13, false, p.SubtleBrush); subtitle.TextWrapping = TextWrapping.Wrap; words.Children.Add(subtitle);
            words.Children.Add(Ui.Text(GameProgressLine(game), 13, false, p.SubtleBrush));
            if (selectedCount > 1) { var many = Ui.Hint(selectedCount + " games selected. Remove, favorite, pin and collection actions apply to all of them."); many.TextWrapping = TextWrapping.Wrap; words.Children.Add(many); }
            head.Children.Add(words); gameDetails.Children.Add(head);

            bool running = sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished);
            var play = Ui.Action(running ? "Playing" : "Play", LaunchLibraryGame, !running); play.IsEnabled = !running;
            gameDetails.Children.Add(Ui.Actions(play, Ui.Action("Edit game", EditLibraryGame),
                Ui.Action(game.Favorite ? "Unfavorite" : "Favorite", () => ToggleGameFlag(true)), Ui.Action(game.Pinned ? "Unpin" : "Pin", () => ToggleGameFlag(false)),
                Ui.Action((library.PlayQueue ?? new List<string>()).Contains(game.Id) ? "In play queue" : "Add to queue", () => { GameLists.Enqueue(library, game); Store.Save(library); ShowLibraryGameDetails(); RefreshHomeCards(); })));

            if (game.RequiresEmulatorAssignment) gameDetails.Children.Add(Warning("This game's emulator was removed. Choose another emulator before playing: click Play or Edit game."));
            else if (!File.Exists(game.Path)) gameDetails.Children.Add(Warning("The game file is missing. Edit its location, or use More > Repair game paths."));

            var facts = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 6, 0, 0) };
            var sessions = GamePlay.SessionsFor(library, game);
            AddFact(facts, "Play time", game.TotalPlaySeconds > 0 ? GamePlay.Duration(game.TotalPlaySeconds) : "Not played yet");
            AddFact(facts, "Last played", String.IsNullOrWhiteSpace(game.LastLaunched) ? "Never" : FriendlyDate(game.LastLaunched));
            AddFact(facts, "Launches", game.LaunchCount.ToString());
            AddFact(facts, "Sessions", sessions.Count.ToString());
            if (!String.IsNullOrWhiteSpace(game.Genre)) AddFact(facts, "Genre", game.Genre);
            if (!String.IsNullOrWhiteSpace(game.Developer)) AddFact(facts, "Developer", game.Developer);
            if (!String.IsNullOrWhiteSpace(game.ReleaseYear)) AddFact(facts, "Released", game.ReleaseYear);
            if ((game.Tags ?? new List<string>()).Count > 0) AddFact(facts, "Tags", String.Join(", ", game.Tags));
            var inCollections = (library.Collections ?? new List<GameCollection>()).Where(c => !c.IsSmart && (c.GameIds ?? new List<string>()).Contains(game.Id)).Select(c => c.Name).ToList();
            if (inCollections.Count > 0) AddFact(facts, "Collections", String.Join(", ", inCollections));
            if (!String.IsNullOrWhiteSpace(game.LaunchProfileName)) AddFact(facts, "Launch profile", game.LaunchProfileName);
            AddFact(facts, "Added", String.IsNullOrWhiteSpace(game.AddedAt) ? "Unknown" : FriendlyDate(game.AddedAt));
            gameDetails.Children.Add(facts);

            if (!String.IsNullOrWhiteSpace(game.Description)) { var d = Ui.Text(game.Description, 13); d.TextWrapping = TextWrapping.Wrap; gameDetails.Children.Add(d); }
            if (!String.IsNullOrWhiteSpace(game.Notes)) { gameDetails.Children.Add(Ui.Caption("Notes")); var n = Ui.Text(game.Notes, 13); n.TextWrapping = TextWrapping.Wrap; gameDetails.Children.Add(n); }

            gameDetails.Children.Add(Ui.Caption("Recent sessions"));
            foreach (var session in sessions.Take(4))
                gameDetails.Children.Add(Ui.Text(FriendlyDate(session.StartedAt) + " · " + SessionLength(session), 12, false, p.SubtleBrush));
            if (sessions.Count == 0) gameDetails.Children.Add(Ui.Hint("Play sessions started from FishBowl appear here."));
            if (!String.IsNullOrWhiteSpace(game.SessionTrackingNote)) { var note = Ui.Hint(game.SessionTrackingNote); note.TextWrapping = TextWrapping.Wrap; gameDetails.Children.Add(note); }

            var path = Ui.Hint(game.Path); path.TextWrapping = TextWrapping.Wrap; gameDetails.Children.Add(path);
            var links = Ui.Actions(Ui.Action("Play sessions", () => ShowSessionJournal(game)), Ui.Action("Screenshots", () => ShowScreenshotGallery(game)), Ui.Action("Open game folder", OpenSelectedGameFolder), Ui.Action("Game setup", ShowLibraryGameTools));
            if (File.Exists(game.ManualPath)) links.Children.Add(Ui.Action("Open manual", () => Platform.Open(game.ManualPath)));
            gameDetails.Children.Add(links);
        }

        private string SessionLength(PlaySession session)
        {
            bool active = sessionTrackers.Values.Any(t => t.SessionId == session.Id && !t.Finished);
            string length = active ? "In progress · " + GamePlay.Duration(session.Seconds) : session.Uncertain && session.Seconds == 0 ? "Duration unknown" : GamePlay.Duration(session.Seconds);
            return length + (session.Corrected ? " · corrected" : "");
        }

        private Control Warning(string text)
        {
            var t = Ui.Text(text, 13, false, p.InkBrush); t.TextWrapping = TextWrapping.Wrap;
            return new Border { Background = new SolidColorBrush(Palette.Alpha(60, p.Pink)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8), Child = t };
        }

        private void AddFact(Grid grid, string label, string value)
        {
            int row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var name = Ui.Text(label, 12, false, p.SubtleBrush); name.Margin = new Thickness(0, 2, 14, 2); Grid.SetRow(name, row); grid.Children.Add(name);
            var text = Ui.Text(value, 13); text.TextWrapping = TextWrapping.Wrap; text.Margin = new Thickness(0, 2); Grid.SetRow(text, row); Grid.SetColumn(text, 1); grid.Children.Add(text);
        }

        private static string FriendlyDate(string text)
        {
            var date = GameLibraryQuery.Date(text); if (date == DateTime.MinValue) return text ?? "";
            var today = DateTime.Now.Date;
            if (date.Date == today) return "Today " + date.ToString("t");
            if (date.Date == today.AddDays(-1)) return "Yesterday " + date.ToString("t");
            return date.Year == today.Year ? date.ToString("d MMM") + ", " + date.ToString("t") : date.ToString("d MMM yyyy");
        }

        private void ToggleGameFlag(bool favorite)
        {
            var games = SelectedLibraryGames(); if (games.Count == 0) return;
            bool value = favorite ? !games[0].Favorite : !games[0].Pinned;
            foreach (var game in games) { if (favorite) game.Favorite = value; else game.Pinned = value; }
            Store.Save(library); RefreshGameLibrary();
        }

        private void OpenSelectedGameFolder()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            var folder = Path.GetDirectoryName(game.Path ?? "");
            if (!Directory.Exists(folder)) throw new IOException("The game's folder is missing. Edit its location or use Repair game paths.");
            Platform.Open(folder);
        }

        // ----- Adding and editing -------------------------------------------------------------------------------------

        private int AddLibraryFiles(IEnumerable<string> files)
        {
            int added = 0; GameEntry last = null;
            foreach (var file in files)
            {
                if (library.Games.Any(g => GameLibraryRemoval.SamePath(g.Path, file))) continue;
                GameLibraryRemoval.AllowPath(library, file);
                var game = GameLibraryImport.NewEntry(library, file, Platform.IsLaunchFile(file));
                if (!game.Extras.Native) { try { GameRecognition.Apply(game, library.Emulators, true); } catch (Exception error) { Store.Log("Game recognition skipped for " + file + ": " + error.Message); } }
                library.Games.Add(game); added++; last = game;
            }
            Store.Save(library); libraryPages.SelectedIndex = LibraryPage; RefreshGameLibrary();
            if (last != null) SelectLibraryGame(last);
            return added;
        }

        private async Task AddLibraryGames()
        {
            var files = await Ui.PickFiles(this, "Add games", "Games and programs|*", library.GameLibraryRoot, true);
            if (files.Count == 0) return;
            int added = AddLibraryFiles(files);
            var unassigned = library.Games.Count(g => g.RequiresEmulatorAssignment && files.Any(f => GameLibraryRemoval.SamePath(f, g.Path)));
            SetStatus("Added " + added + (added == 1 ? " game." : " games.") + (unassigned > 0 ? " " + unassigned + " need an emulator; choose one when you play them." : ""));
        }

        private async Task AddLibraryFolder()
        {
            if (!library.Emulators.Any(e => (e.Extensions ?? new List<string>()).Count > 0)) { await Ui.Message(this, "Add an emulator with game file types first. FishBowl adds the files in a folder that your emulators can open."); return; }
            var folder = await Ui.PickFolder(this, "Add games from a folder", library.GameLibraryRoot);
            if (folder == null) return;
            var files = await Task.Run(() => GameLibraryImport.FolderCandidates(library, folder, 5000));
            if (files.Count == 0) { await Ui.Message(this, "No new games were found in this folder for the file types your emulators open."); return; }
            if (!await Ui.Confirm(this, "Add " + files.Count + (files.Count == 1 ? " game" : " games") + " from " + folder + "?\n\nFiles stay where they are.")) return;
            int added = AddLibraryFiles(files); SetStatus("Added " + added + " games from " + Path.GetFileName(folder) + ".");
        }

        private async Task EditLibraryGame()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            var dialog = new FishDialog("Edit game", 680, 760); var fields = new StackPanel { Spacing = 6 };
            var title = Ui.Field(game.Title); var path = Ui.Field(game.Path); var cover = Ui.Field(game.ArtworkPath);
            var notes = Ui.Paragraphs(game.Notes, false); notes.MinHeight = 90;
            var description = Ui.Paragraphs(game.Description, false); description.MinHeight = 70;
            var arguments = Ui.Field(game.Arguments); var statusChoice = Ui.Combo(GameLibraryQuery.Progress, game.PlayStatus ?? "Not started");
            var tags = Ui.Field(String.Join(", ", game.Tags ?? new List<string>())); var favorite = Ui.Check("Favorite", game.Favorite); var pinned = Ui.Check("Pinned", game.Pinned);
            var console = Ui.Field(game.ConsoleLabel); var genre = Ui.Field(game.Genre); var developer = Ui.Field(game.Developer); var year = Ui.Field(game.ReleaseYear);
            var emulatorEntries = new[] { "Native game / shortcut" }.Concat(library.Emulators.Select(e => e.Name + " · " + e.Id.Substring(0, Math.Min(6, e.Id.Length)))).ToArray();
            var current = GameLibraryQuery.EmulatorFor(library, game);
            var selected = current == null ? -1 : library.Emulators.IndexOf(current);
            var emulator = Ui.Combo(emulatorEntries, emulatorEntries[selected + 1]);
            if (game.RequiresEmulatorAssignment || (current == null && !GameLibraryQuery.Native(game))) emulator.SelectedIndex = -1;
            emulator.PlaceholderText = "Choose an emulator";
            fields.Children.Add(Ui.Caption("Title")); fields.Children.Add(title); fields.Children.Add(Ui.Hint("The library title does not rename the game file."));
            fields.Children.Add(Ui.Caption("Game file")); fields.Children.Add(Ui.WithButton(path, Ui.Action("Browse", async () => { var f = await Ui.PickFile(dialog, "Game file"); if (f != null) path.Text = f; })));
            fields.Children.Add(Ui.Caption("Cover image")); fields.Children.Add(Ui.WithButton(cover, Ui.Action("Browse", async () => { var f = await Ui.PickFile(dialog, "Game image", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp"); if (f != null) cover.Text = f; })));
            fields.Children.Add(Ui.Caption("Emulator")); fields.Children.Add(emulator); fields.Children.Add(Ui.Caption("Arguments ({game} inserts the game file)")); fields.Children.Add(arguments);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            var left = new StackPanel { Spacing = 4 }; var right = new StackPanel { Spacing = 4 }; Grid.SetColumn(right, 2); grid.Children.Add(left); grid.Children.Add(right);
            left.Children.Add(Ui.Caption("Platform")); left.Children.Add(console); left.Children.Add(Ui.Caption("Genre")); left.Children.Add(genre);
            right.Children.Add(Ui.Caption("Developer")); right.Children.Add(developer); right.Children.Add(Ui.Caption("Release year")); right.Children.Add(year);
            left.Children.Add(Ui.Caption("Progress")); left.Children.Add(statusChoice); right.Children.Add(Ui.Caption(" ")); right.Children.Add(Ui.Actions(favorite, pinned));
            fields.Children.Add(grid);
            fields.Children.Add(Ui.Caption("Tags, separated by commas")); fields.Children.Add(tags);
            fields.Children.Add(Ui.Caption("Description")); fields.Children.Add(description);
            fields.Children.Add(Ui.Caption("Notes")); fields.Children.Add(notes);
            fields.Children.Add(dialog.Footer("Save", async () =>
            {
                if (String.IsNullOrWhiteSpace(title.Text) || !File.Exists(path.Text)) { await Ui.Message(dialog, "Enter a title and choose an existing game file."); return false; }
                if (emulator.SelectedIndex < 0) { await Ui.Message(dialog, "Choose the emulator for this game, or Native game / shortcut for programs that run by themselves."); return false; }
                string artwork = cover.Text ?? "";
                if (!String.IsNullOrWhiteSpace(artwork) && !GameLibraryRemoval.SamePath(artwork, game.ArtworkPath))
                {
                    try { using (var check = new Bitmap(artwork)) { } } catch (Exception) { await Ui.Message(dialog, "The cover image cannot be read. Choose a PNG, JPEG, BMP or WebP image."); return false; }
                    artwork = GameLibraryEditing.RetainArtwork(artwork, Store.DataDirectory);
                }
                game.TitleIsCustom = game.TitleIsCustom || !String.Equals(game.Title, title.Text.Trim(), StringComparison.Ordinal); game.Title = title.Text.Trim(); game.Path = Path.GetFullPath(path.Text); game.ArtworkPath = String.IsNullOrWhiteSpace(artwork) ? null : artwork;
                var chosen = emulator.SelectedIndex > 0 ? library.Emulators[emulator.SelectedIndex - 1] : null;
                if (chosen?.Id != current?.Id) { game.PreferredBuildId = null; game.LaunchProfileName = null; }
                game.PreferredEmulatorId = chosen?.Id; game.EmulatorId = chosen?.Id; game.RequiresEmulatorAssignment = false;
                if (game.Extras == null) game.Extras = new GameExtras(); game.Extras.Native = emulator.SelectedIndex == 0;
                if (game.Extras.Native && String.IsNullOrWhiteSpace(game.Extras.WorkingDirectory)) game.Extras.WorkingDirectory = Path.GetDirectoryName(game.Path);
                game.Arguments = arguments.Text; game.Notes = notes.Text; game.Description = description.Text; game.PlayStatus = statusChoice.SelectedItem as string; game.Favorite = favorite.IsChecked == true; game.Pinned = pinned.IsChecked == true;
                game.ConsoleLabel = Blank(console.Text); game.Genre = Blank(genre.Text); game.Developer = Blank(developer.Text); game.ReleaseYear = Blank(year.Text);
                game.Tags = (tags.Text ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Store.Save(library); RefreshGameLibrary(); return true;
            }));
            dialog.Body = new ScrollViewer { Content = fields }; await dialog.Present(this);
        }
        private static string Blank(string text) { return String.IsNullOrWhiteSpace(text) ? null : text.Trim(); }

        // ----- Launching ----------------------------------------------------------------------------------------------

        private Task LaunchLibraryGame() { var game = SelectedLibraryGame(); return game == null ? Task.CompletedTask : LaunchGame(game); }

        // Starts a game with its emulator (or directly for native games) and records a tracked play session.
        // Other screens (Home, Living room, collections, play queue) launch through this method.
        private async Task LaunchGame(GameEntry game)
        {
            if (game == null) return;
            if (sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished)) { await Ui.Message(this, game.Title + " is already running."); return; }
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

        private async Task ReopenLastGame()
        {
            var game = GamePlay.LastGame(library);
            if (game == null) { await Ui.Message(this, "Games you launch from FishBowl can be reopened here."); return; }
            await LaunchGame(game);
        }

        // Like Windows: a game without a usable emulator asks for one before launching.
        private async Task<bool> ChooseGameEmulator(GameEntry game)
        {
            if (library.Emulators.Count == 0) { await Ui.Message(this, "Add an emulator before launching this game."); return false; }
            var labels = library.Emulators.Select(e => String.IsNullOrWhiteSpace(e.Name) ? Path.GetFileNameWithoutExtension(e.Executable) : e.Name).ToList();
            labels = labels.Select((name, i) => labels.Count(n => n == name) > 1 ? name + " (" + (i + 1) + ")" : name).ToList();
            var dialog = new FishDialog("Preferred emulator", 460); var body = new StackPanel { Spacing = 8 };
            var suggested = GameLibraryImport.EmulatorForFile(library, game.Path ?? "");
            var choice = Ui.Combo(labels, labels[Math.Max(0, suggested == null ? 0 : library.Emulators.IndexOf(suggested))]);
            var text = Ui.Text("Choose the emulator for " + game.Title + ".", 14); text.TextWrapping = TextWrapping.Wrap; body.Children.Add(text);
            body.Children.Add(Ui.Hint(GameLibraryQuery.ConsoleName(game)));
            body.Children.Add(choice); bool chosen = false;
            body.Children.Add(dialog.Footer("Use emulator", () => { chosen = true; return Task.FromResult(true); }));
            dialog.Body = body; await dialog.Present(this);
            if (!chosen || choice.SelectedIndex < 0) return false;
            var selected = library.Emulators[choice.SelectedIndex];
            game.EmulatorId = selected.Id; game.PreferredEmulatorId = selected.Id; game.PreferredBuildId = null; game.LaunchProfileName = null; game.RequiresEmulatorAssignment = false;
            if (game.Extras != null) game.Extras.Native = false;
            Store.Save(library); RefreshGameLibrary(); return true;
        }

        // ----- Removing and repair ------------------------------------------------------------------------------------

        private async Task RemoveLibraryGame()
        {
            var games = SelectedLibraryGames(); if (games.Count == 0) return;
            if (games.Any(game => sessionTrackers.Values.Any(t => t.GameId == game.Id && !t.Finished))) { await Ui.Message(this, "Close the running game before removing it from the library."); return; }
            var what = games.Count == 1 ? games[0].Title : games.Count + " games";
            if (!await Ui.Confirm(this, "Remove " + what + " from FishBowl? Game files and saves stay where they are. Use Undo removal to restore " + (games.Count == 1 ? "it." : "them."))) return;
            GameLibraryRemoval.RemoveGames(library, games.Select(g => g.Id)); Store.Save(library); RefreshGameLibrary();
            SetStatus("Removed " + what + ". Use More > Undo removal to restore.");
        }
        private void UndoLibraryRemoval() { int count = GameLibraryRemoval.Undo(library); Store.Save(library); RefreshHub(); RefreshGameLibrary(); ConfigureGameFolderWatchers(); SetStatus(count == 0 ? "Nothing to restore." : "Restored " + count + " entries."); }

        private async Task RepairLibraryPaths()
        {
            var dialog = new FishDialog("Repair game paths", 600); var body = new StackPanel { Spacing = 8 }; var old = Ui.Field(); var replacement = Ui.Field();
            var missing = library.Games.Where(g => !File.Exists(g.Path)).ToList();
            body.Children.Add(Ui.Hint("Find missing files under a moved folder. Existing files and filenames stay unchanged. Review the match count before applying."));
            body.Children.Add(Ui.Text(missing.Count + (missing.Count == 1 ? " game has" : " games have") + " a missing file.", 13));
            if (missing.Count > 0) old.Text = Path.GetDirectoryName(missing[0].Path);
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
