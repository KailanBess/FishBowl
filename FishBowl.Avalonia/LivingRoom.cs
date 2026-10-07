using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EmulatorHub
{
    // The Living-room Library: a full-screen, controller-, keyboard- and mouse-navigable view of the library with
    // personal shelves, a focus panel, artwork backdrops, interface sounds, a launch presentation and ambient artwork.
    // Windows: LivingRoomLibrary + ImmersionWindow. Logic shared with Windows lives in FishBowl.LivingRoom.cs.
    public sealed class LivingRoomWindow : Window
    {
        private const double TileWidth = 196, TileSpacing = 18;
        private readonly LibraryData data;
        private readonly Func<GameEntry, Task> launch;
        private readonly Func<GameEntry, bool> running;
        private readonly Action changed;
        private readonly Palette p = Ui.P;
        private readonly Dictionary<string, Bitmap> covers = new Dictionary<string, Bitmap>();
        private readonly WrapPanel tiles = new WrapPanel { Orientation = Orientation.Horizontal };
        private readonly ScrollViewer tileScroll;
        private readonly StackPanel shelfBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBox search = new TextBox { Watermark = "Find a game", Width = 320, FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
        private readonly Image backdrop = new Image { Stretch = Stretch.UniformToFill, Opacity = 0.16, IsHitTestVisible = false };
        private readonly Image focusCover = new Image { Stretch = Stretch.Uniform, Height = 360, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBlock focusMonogram = Ui.Text("", 96, true);
        private readonly TextBlock focusTitle = new TextBlock { FontSize = 30, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock focusMeta = new TextBlock { FontSize = 16, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock focusStats = new TextBlock { FontSize = 15, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock focusText = new TextBlock { FontSize = 16, TextWrapping = TextWrapping.Wrap };
        private readonly Border focusPanel;
        private readonly Grid body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
        private readonly Grid overlay = new Grid { IsVisible = false };
        private readonly TextBlock status = new TextBlock { FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        private readonly DispatcherTimer idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        private List<GameEntry> games = new List<GameEntry>();
        private List<Border> tileViews = new List<Border>();
        private string shelf = "All games";
        private int selected;
        private int columns = 1;
        private bool focusMode, launching, ambient;
        private Action overlayCancel;
        private DateTime lastInput = DateTime.UtcNow;
        private DispatcherTimer ambientTimer;
        private int ambientIndex;

        public static LivingRoomWindow Current { get; private set; }

        public LivingRoomWindow(LibraryData library, Func<GameEntry, Task> launchGame, Func<GameEntry, bool> isRunning, Action libraryChanged)
        {
            data = library; launch = launchGame; running = isRunning; changed = libraryChanged;
            Immersion.Ensure(data);
            Title = "Living-room Library"; Icon = Ui.AppIcon(); FontFamily = Ui.Font; Background = p.BottomBrush; Foreground = p.InkBrush;
            Width = 1280; Height = 800; WindowStartupLocation = WindowStartupLocation.CenterScreen; Focusable = true;

            var header = new DockPanel { LastChildFill = false };
            var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new Image { Source = Ui.Logo(), Width = 44, Height = 44 });
            title.Children.Add(Ui.Text("Living-room Library", 28, true));
            DockPanel.SetDock(title, Dock.Left); header.Children.Add(title);
            DockPanel.SetDock(search, Dock.Right); header.Children.Add(search);
            var top = new StackPanel { Margin = new Thickness(36, 22, 36, 6), Spacing = 14, Children = { header, new ScrollViewer { Content = shelfBar, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } } };

            tileScroll = new ScrollViewer { Content = tiles, Margin = new Thickness(36, 8, 18, 8), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            body.Children.Add(tileScroll);
            var focus = new StackPanel { Spacing = 12 };
            focus.Children.Add(new Panel { Children = { focusCover, focusMonogram } });
            focusMonogram.HorizontalAlignment = HorizontalAlignment.Center; focusMonogram.VerticalAlignment = VerticalAlignment.Center; focusMonogram.Foreground = p.BlueBrush;
            focusMeta.Foreground = p.SubtleBrush; focusStats.Foreground = p.SubtleBrush;
            focus.Children.Add(focusTitle); focus.Children.Add(focusMeta); focus.Children.Add(focusStats); focus.Children.Add(focusText);
            focusPanel = new Border { Background = new SolidColorBrush(Palette.Alpha(215, p.Surface)), CornerRadius = new CornerRadius(14), Padding = new Thickness(26), Margin = new Thickness(0, 8, 36, 8), Child = new ScrollViewer { Content = focus } };
            Grid.SetColumn(focusPanel, 1); body.Children.Add(focusPanel);

            var hints = new StackPanel { Margin = new Thickness(36, 6, 36, 18), Spacing = 6 };
            var keys = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var hint in new[] { "A|Enter|Play", "X|F|Favorite", "Y|I|Details", "LB RB|Tab|Shelf", "Start|M|Menu", "B|Esc|Exit" })
            {
                var parts = hint.Split('|'); var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 22, 2) };
                item.Children.Add(new Border { Background = p.BlueBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 2), Child = Ui.Text(parts[0], 14, true, new SolidColorBrush(Palette.ReadableInk(p.Blue))) });
                item.Children.Add(Ui.Text(parts[2] + "  (" + parts[1] + ")", 15));
                keys.Children.Add(item);
            }
            hints.Children.Add(keys); hints.Children.Add(status);

            var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            layout.Children.Add(top); Grid.SetRow(body, 1); layout.Children.Add(body); Grid.SetRow(hints, 2); layout.Children.Add(hints);
            Content = new Panel { Children = { backdrop, layout, overlay } };

            search.TextChanged += delegate { Reload(null); };
            search.KeyDown += (sender, e) => { if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Down) { e.Handled = true; Focus(); } };
            tileScroll.SizeChanged += delegate { int fitted = LivingRoom.Columns(tileScroll.Bounds.Width - 4, TileWidth, TileSpacing); if (fitted != columns) { columns = fitted; } };
            AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(PointerPressedEvent, (sender, e) => { if (Wake()) e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, (sender, e) => { lastInput = DateTime.UtcNow; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            idleTimer.Tick += delegate { if (IsActive && !ambient && overlayCancel == null && Immersion.AmbientDue(data, lastInput, DateTime.UtcNow)) ShowAmbient(); };
            Opened += delegate { Current = this; EnterFullScreen(0); Focus(); idleTimer.Start(); };
            Closed += delegate { if (Current == this) Current = null; idleTimer.Stop(); if (ambientTimer != null) ambientTimer.Stop(); foreach (var bitmap in covers.Values) bitmap.Dispose(); covers.Clear(); };
            Reload(null);
        }

        // Some window managers ignore a full-screen request made while the window is still being mapped; ask again.
        private void EnterFullScreen(int attempt)
        {
            if (attempt > 0 && WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;
            WindowState = WindowState.FullScreen;
            if (attempt < 4) DispatcherTimer.RunOnce(() => { if (IsVisible && !CoversScreen()) EnterFullScreen(attempt + 1); }, TimeSpan.FromMilliseconds(300));
        }
        private bool CoversScreen()
        {
            var screen = Screens.ScreenFromWindow(this);
            return screen == null || WindowState != WindowState.FullScreen ? WindowState == WindowState.FullScreen && screen == null : Bounds.Width * screen.Scaling >= screen.Bounds.Width - 2 && Bounds.Height * screen.Scaling >= screen.Bounds.Height - 2;
        }

        public GameEntry SelectedGame { get { return selected >= 0 && selected < games.Count ? games[selected] : null; } }

        // Reloads after the library changed elsewhere (a finished session, an edit), keeping the selection.
        public void RefreshGames() { var game = SelectedGame; Reload(game == null ? null : game.Id); }

        private void Reload(string keepId)
        {
            if (keepId == null && SelectedGame != null) keepId = SelectedGame.Id;
            shelfBar.Children.Clear();
            foreach (var name in Immersion.Shelves)
            {
                bool on = name == shelf; var captured = name;
                var chip = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 6), Background = on ? p.BlueBrush : new SolidColorBrush(Palette.Alpha(160, p.Surface)), Cursor = new Cursor(StandardCursorType.Hand),
                    Child = Ui.Text(name, 16, on, on ? new SolidColorBrush(Palette.ReadableInk(p.Blue)) : p.InkBrush) };
                chip.PointerPressed += delegate { SetShelf(captured); };
                shelfBar.Children.Add(chip);
            }
            games = LivingRoom.Games(data, shelf, search.Text);
            tiles.Children.Clear(); tileViews = new List<Border>();
            for (int i = 0; i < games.Count; i++) { var tile = Tile(games[i], i); tileViews.Add(tile); tiles.Children.Add(tile); }
            if (games.Count == 0) tiles.Children.Add(Ui.Text(data.Games.Count == 0 ? "Add games in Library to use the Living-room Library." : shelf == "Favorites" ? "Mark games as favorites to fill this shelf." : shelf == "Continue playing" ? "Games you are playing appear here." : "No games match.", 20, false, p.SubtleBrush));
            int index = keepId == null ? -1 : games.FindIndex(g => g.Id == keepId);
            Select(index >= 0 ? index : (games.Count > 0 ? Math.Min(Math.Max(0, selected), games.Count - 1) : -1), false);
        }

        private Border Tile(GameEntry game, int index)
        {
            var art = new Panel { Width = TileWidth, Height = TileWidth * 4 / 3, Background = new SolidColorBrush(Palette.Blend(p.Surface, 18)) };
            var bitmap = Cover(game.ArtworkPath, 400);
            if (bitmap != null) art.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill });
            else art.Children.Add(new TextBlock { Text = Monogram(game.Title), FontSize = 56, FontWeight = FontWeight.Bold, Foreground = p.BlueBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            if (game.Favorite) art.Children.Add(new Border { Background = p.PinkBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(7, 1), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Text("★", 15, true, new SolidColorBrush(Palette.ReadableInk(p.Pink))) });
            if (running(game)) art.Children.Add(new Border { Background = p.BlueBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 1), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Text("Running", 13, true, new SolidColorBrush(Palette.ReadableInk(p.Blue))) });
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = art });
            stack.Children.Add(new TextBlock { Text = game.Title, FontSize = 16, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = TileWidth });
            stack.Children.Add(new TextBlock { Text = game.ConsoleLabel ?? "", FontSize = 13, Foreground = p.SubtleBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = TileWidth });
            var tile = new Border { Child = stack, Padding = new Thickness(6), Margin = new Thickness(0, 0, TileSpacing, TileSpacing), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(4), BorderBrush = Brushes.Transparent, RenderTransformOrigin = RelativePoint.Center, RenderTransform = new ScaleTransform(1, 1) };
            if (Immersion.Animate(data)) tile.Transitions = new Transitions { new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140) } };
            tile.PointerPressed += (sender, e) => { if (e.ClickCount >= 2) Perform(CouchAction.Confirm); else Select(index, true); };
            return tile;
        }

        private static string Monogram(string title)
        {
            var words = (title ?? "?").Split(new[] { ' ', '-', '_', ':' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 0 ? "?" : String.Concat(words.Take(2).Select(w => Char.ToUpperInvariant(w[0])));
        }

        private Bitmap Cover(string path, int width)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            string key = width + "|" + Immersion.ArtworkStamp(path);
            Bitmap bitmap;
            if (covers.TryGetValue(key, out bitmap)) return bitmap;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 33554432) return null;
                using (var stream = File.OpenRead(path)) bitmap = Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
                covers[key] = bitmap; return bitmap;
            }
            catch (Exception error) { Store.Log("Living-room artwork unavailable: " + error.Message); return null; }
        }

        private void Select(int index, bool sound)
        {
            if (selected >= 0 && selected < tileViews.Count) { tileViews[selected].BorderBrush = Brushes.Transparent; tileViews[selected].RenderTransform = new ScaleTransform(1, 1); }
            selected = index;
            var game = SelectedGame;
            if (game != null && selected < tileViews.Count)
            {
                var tile = tileViews[selected]; tile.BorderBrush = p.BlueBrush; tile.RenderTransform = new ScaleTransform(1.04, 1.04);
                tile.BringIntoView();
                if (sound) InterfaceSound.Play(data, false);
            }
            ShowFocus(game);
        }

        private void ShowFocus(GameEntry game)
        {
            focusTitle.Text = game == null ? "Choose a game" : game.Title;
            if (game == null)
            {
                focusMeta.Text = ""; focusStats.Text = ""; focusText.Text = data.Games.Count == 0 ? "Add games to your Library to start." : "";
                focusCover.Source = null; focusMonogram.Text = ""; backdrop.Source = null; return;
            }
            var emulator = data.Emulators.FirstOrDefault(e => e.Id == (game.PreferredEmulatorId ?? game.EmulatorId));
            focusMeta.Text = String.Join("  •  ", new[] { game.ConsoleLabel ?? (emulator == null ? null : emulator.Platform), game.Genre, game.ReleaseYear, emulator == null ? null : emulator.Name }.Where(s => !String.IsNullOrWhiteSpace(s)));
            var last = Immersion.Date(game.LastLaunched);
            focusStats.Text = LivingRoom.PlayTime(game.TotalPlaySeconds) + (last == DateTime.MinValue ? "" : "  •  Last played " + last.ToLocalTime().ToString("d")) + (String.IsNullOrWhiteSpace(game.PlayStatus) ? "" : "  •  " + game.PlayStatus) + (game.PersonalRating > 0 ? "  •  " + new string('★', game.PersonalRating) : "") + (File.Exists(game.Path) ? "" : "\nThe game file is missing.");
            focusText.Text = ((game.Description ?? "") + (String.IsNullOrWhiteSpace(game.Notes) ? "" : "\n\n" + game.Notes)).Trim();
            var cover = Cover(game.ArtworkPath, 720);
            focusCover.Source = cover; focusMonogram.Text = cover == null ? Monogram(game.Title) : "";
            backdrop.Source = Immersion.Ensure(data).Backdrops ? cover : null;
            if (Immersion.Animate(data))
            {
                focusCover.Opacity = 0; focusCover.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
                Dispatcher.UIThread.Post(() => focusCover.Opacity = 1, DispatcherPriority.Background);
            }
            else focusCover.Opacity = 1;
        }

        private void SetShelf(string name)
        {
            if (name == shelf) return;
            shelf = name; selected = 0; Reload(null); if (games.Count > 0) Select(0, true);
        }

        private void SetFocusMode(bool on)
        {
            focusMode = on;
            tileScroll.IsVisible = !on;
            body.ColumnDefinitions = on ? new ColumnDefinitions("0,*") : new ColumnDefinitions("3*,2*");
            focusPanel.Margin = on ? new Thickness(120, 8, 120, 8) : new Thickness(0, 8, 36, 8);
            focusCover.Height = on ? 460 : 360;
        }

        // Returns true when the input only woke the window from ambient artwork.
        private bool Wake()
        {
            lastInput = DateTime.UtcNow;
            if (!ambient) return false;
            HideOverlay(); return true;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (Wake()) { e.Handled = true; return; }
            if (overlayCancel != null) { if (e.Key == Key.Escape || e.Key == Key.Back) { e.Handled = true; overlayCancel(); } return; }
            if (search.IsKeyboardFocusWithin) return;
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control), shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            CouchAction action;
            switch (e.Key)
            {
                case Key.Up: action = CouchAction.Up; break;
                case Key.Down: action = CouchAction.Down; break;
                case Key.Left: action = CouchAction.Left; break;
                case Key.Right: action = CouchAction.Right; break;
                case Key.Enter: case Key.Space: action = CouchAction.Confirm; break;
                case Key.Escape: case Key.Back: action = CouchAction.Back; break;
                case Key.F: action = ctrl ? CouchAction.Search : CouchAction.Favorite; break;
                case Key.I: case Key.D: action = CouchAction.Details; break;
                case Key.Tab: action = shift ? CouchAction.PreviousShelf : CouchAction.NextShelf; break;
                case Key.PageUp: action = CouchAction.PreviousShelf; break;
                case Key.PageDown: action = CouchAction.NextShelf; break;
                case Key.Home: action = CouchAction.First; break;
                case Key.End: action = CouchAction.Last; break;
                case Key.M: case Key.F10: action = CouchAction.Menu; break;
                case Key.Oem2: action = CouchAction.Search; break;
                case Key.F11: action = CouchAction.None; WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen; break;
                default: action = CouchAction.None; break;
            }
            if (action == CouchAction.None) return;
            e.Handled = true; Perform(action);
        }

        // Every input (keyboard, mouse, controller) ends up here.
        public async void Perform(CouchAction action)
        {
            if (Wake() || action == CouchAction.None) return;
            if (overlayCancel != null) { if (action == CouchAction.Back) overlayCancel(); return; }
            switch (action)
            {
                case CouchAction.Back:
                    if (focusMode) SetFocusMode(false); else if (!String.IsNullOrEmpty(search.Text)) search.Text = ""; else Close();
                    break;
                case CouchAction.Confirm: await Play(); break;
                case CouchAction.Favorite:
                    var game = SelectedGame; if (game == null) break;
                    game.Favorite = !game.Favorite; Store.Save(data); changed(); Reload(game.Id); SetStatus(game.Favorite ? "Added " + game.Title + " to Favorites." : "Removed " + game.Title + " from Favorites.");
                    break;
                case CouchAction.Details: SetFocusMode(!focusMode); break;
                case CouchAction.PreviousShelf: case CouchAction.NextShelf: SetShelf(LivingRoom.NextShelf(shelf, action == CouchAction.NextShelf)); break;
                case CouchAction.Search: search.Focus(); break;
                case CouchAction.Menu: await ShowMenu(); break;
                default:
                    if (focusMode && (action == CouchAction.Up || action == CouchAction.Down)) break;
                    int next = LivingRoom.Move(selected, games.Count, focusMode ? 1 : columns, action);
                    if (next != selected) Select(next, true);
                    break;
            }
        }

        public void Perform(ControllerAction action)
        {
            switch (action)
            {
                case ControllerAction.Up: Perform(CouchAction.Up); break;
                case ControllerAction.Down: Perform(CouchAction.Down); break;
                case ControllerAction.Left: Perform(CouchAction.Left); break;
                case ControllerAction.Right: Perform(CouchAction.Right); break;
                case ControllerAction.Activate: Perform(CouchAction.Confirm); break;
                case ControllerAction.Back: Perform(CouchAction.Back); break;
                case ControllerAction.PreviousTab: Perform(CouchAction.PreviousShelf); break;
                case ControllerAction.NextTab: Perform(CouchAction.NextShelf); break;
                case ControllerAction.Details: Perform(CouchAction.Details); break;
                case ControllerAction.Favorite: Perform(CouchAction.Favorite); break;
                case ControllerAction.Menu: Perform(CouchAction.Menu); break;
            }
        }

        private void SetStatus(string text) { status.Text = text; }

        private async Task Play()
        {
            var game = SelectedGame; if (game == null || launching) return;
            launching = true;
            try
            {
                if (Immersion.Ensure(data).LaunchPresentation && !await Presentation(game)) { SetStatus("Launch cancelled."); return; }
                InterfaceSound.Play(data, true);
                await Ui.Run(this, () => launch(game));
                Reload(game.Id);
                if (running(game)) SetStatus("Started " + game.Title + ". Press B or Esc here to leave the Living-room Library.");
            }
            finally { launching = false; }
        }

        // Windows LaunchPresentation: brief launch artwork with a chance to cancel before the program starts.
        private async Task<bool> Presentation(GameEntry game)
        {
            var done = new TaskCompletionSource<bool>();
            var card = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 720 };
            var cover = Cover(game.ArtworkPath, 720);
            if (cover != null) card.Children.Add(new Image { Source = cover, Height = 420, Stretch = Stretch.Uniform });
            card.Children.Add(new TextBlock { Text = "Preparing " + game.Title, FontSize = 30, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(new TextBlock { Text = "Configuration validated. Preparing to start the selected program.", FontSize = 17, TextAlignment = TextAlignment.Center, Foreground = p.SubtleBrush, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(new TextBlock { Text = "Cancel launch: B or Esc", FontSize = 15, TextAlignment = TextAlignment.Center, Foreground = p.SubtleBrush });
            ShowOverlay(card, 235, () => done.TrySetResult(false));
            int step = Immersion.Animate(data) ? 250 : 100;
            var delay = Task.Delay(step * 3);
            var finished = await Task.WhenAny(delay, done.Task);
            HideOverlay();
            return finished == delay;
        }

        private void ShowOverlay(Control content, byte opacity, Action cancel)
        {
            overlay.Children.Clear();
            overlay.Children.Add(new Border { Background = new SolidColorBrush(Palette.Alpha(opacity, p.Bottom)) });
            overlay.Children.Add(content);
            overlay.IsVisible = true; overlayCancel = cancel;
        }

        private void HideOverlay()
        {
            overlay.IsVisible = false; overlay.Children.Clear(); overlayCancel = null; ambient = false;
            if (ambientTimer != null) { ambientTimer.Stop(); ambientTimer = null; }
            lastInput = DateTime.UtcNow; Focus();
        }

        // Windows AmbientWindow: an artwork slideshow after idle time; any input returns to the library.
        public void ShowAmbient()
        {
            var list = Immersion.AmbientGames(data);
            var image = new Image { Stretch = Stretch.Uniform, Opacity = 0.85 };
            var caption = new TextBlock { FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 40), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            Action advance = () =>
            {
                if (list.Count == 0) { caption.Text = "No game artwork linked. Move, click or press a key to exit."; return; }
                var game = list[ambientIndex++ % list.Count];
                image.Source = Cover(game.ArtworkPath, 1600); caption.Text = game.Title + "  •  " + game.ConsoleLabel;
            };
            ShowOverlay(new Panel { Children = { image, caption } }, 255, HideOverlay);
            ambient = true; advance();
            ambientTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            ambientTimer.Tick += delegate { advance(); }; ambientTimer.Start();
        }

        private async Task ShowMenu()
        {
            var dialog = new FishDialog("Living-room menu", 460); var stack = new StackPanel { Spacing = 10 };
            Func<string, Func<Task>, Button> item = (text, act) => { var b = Ui.Action(text, async () => { dialog.Close(); await act(); }); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.FontSize = 18; return b; };
            stack.Children.Add(Ui.Text("Living-room Library", 22, true));
            var resume = item("Resume", () => Task.CompletedTask); resume.IsDefault = true; stack.Children.Add(resume);
            stack.Children.Add(item("Find a game", () => { search.Focus(); return Task.CompletedTask; }));
            stack.Children.Add(item(focusMode ? "Back to shelves" : "Focus view", () => { SetFocusMode(!focusMode); return Task.CompletedTask; }));
            stack.Children.Add(item("Ambient preview", () => { ShowAmbient(); return Task.CompletedTask; }));
            stack.Children.Add(item("Immersion settings", async () => { await ImmersionSettingsDialog.Show(this, data, changed); Reload(null); }));
            stack.Children.Add(item("Exit full screen", () => { Close(); return Task.CompletedTask; }));
            dialog.Body = stack; dialog.Opened += delegate { resume.Focus(); };
            await dialog.Present(this);
            Focus();
        }
    }

    // Windows ImmersionSettingsDialog: one settings page for the immersive features.
    public static class ImmersionSettingsDialog
    {
        public static readonly string[][] Options =
        {
            new[] { "Bubbles", "Theme bubbles along the workspace sides" },
            new[] { "Roomier", "Roomier Home cards and Library rows" },
            new[] { "Backdrops", "Dim selected-game backgrounds" },
            new[] { "Transitions", "Gentle tab highlights and artwork fades (honors reduced motion)" },
            new[] { "Sounds", "Interface selection and launch sounds" },
            new[] { "LaunchPresentation", "Brief launch artwork with cancel before starting" },
            new[] { "Recap", "Offer session recap after tracked process closes" },
            new[] { "Ambient", "Artwork slideshow after idle time" },
        };

        public static async Task Show(Window owner, LibraryData data, Action changed)
        {
            var settings = Immersion.Ensure(data);
            var dialog = new FishDialog("Immersion settings", 620); var stack = new StackPanel { Spacing = 6 };
            var checks = new Dictionary<string, CheckBox>();
            foreach (var option in Options)
            {
                var check = Ui.Check(option[1], (bool)typeof(ImmersionSettings).GetProperty(option[0]).GetValue(settings, null));
                checks[option[0]] = check; stack.Children.Add(check);
            }
            var volume = new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, Value = settings.Volume, FormatString = "0" };
            var idle = new NumericUpDown { Minimum = 1, Maximum = 60, Increment = 1, Value = Math.Max(1, settings.IdleMinutes), FormatString = "0" };
            var preset = Ui.Combo(Immersion.Presets, "Current");
            stack.Children.Add(Ui.Caption("Interface volume (%)")); stack.Children.Add(volume);
            stack.Children.Add(Ui.Caption("Idle minutes")); stack.Children.Add(idle);
            stack.Children.Add(Ui.Caption("Apply a coordinated cosmetic preset")); stack.Children.Add(preset);
            stack.Children.Add(Ui.Hint("Backgrounds are local artwork. Sounds and ambient mode start off. Preset replaces palette, spacing and background style when applied."));
            var test = Ui.Action("Test sound", () => { var probe = new LibraryData(); Immersion.Ensure(probe).Sounds = true; probe.Hub.Immersion.Volume = (int)(volume.Value ?? 0); InterfaceSound.Play(probe, false, true); });
            stack.Children.Add(dialog.Footer("Apply", () =>
            {
                var updated = new ImmersionSettings { Volume = (int)(volume.Value ?? 0), IdleMinutes = (int)(idle.Value ?? 3), Preset = preset.SelectedItem as string ?? "Current" };
                foreach (var pair in checks) typeof(ImmersionSettings).GetProperty(pair.Key).SetValue(updated, pair.Value.IsChecked == true, null);
                updated.AdditionalFields = settings.AdditionalFields;
                Immersion.ApplyPreset(data, updated.Preset);
                data.Hub.Immersion = updated; Store.Save(data); if (changed != null) changed();
                return Task.FromResult(true);
            }, test));
            dialog.Body = new ScrollViewer { Content = stack };
            await dialog.Present(owner);
        }
    }

    // Windows Immersion.Sound: short interface tones, played through the desktop's sound server when one is installed.
    public static class InterfaceSound
    {
        private static readonly object gate = new object();
        private static DateTime last;
        private static bool busy;
        private static string player;
        private static bool searched;

        public static void Play(LibraryData data, bool launch, bool force = false)
        {
            var settings = Immersion.Ensure(data);
            if ((!settings.Sounds && !force) || settings.Volume <= 0) return;
            lock (gate)
            {
                if (busy || (!launch && (DateTime.UtcNow - last).TotalMilliseconds < 150)) return;
                busy = true; last = DateTime.UtcNow;
            }
            int volume = settings.Volume;
            Task.Run(() =>
            {
                string file = null;
                try
                {
                    if (!searched) { player = new[] { "pw-play", "paplay", "aplay" }.Select(Platform.FindOnPath).FirstOrDefault(p => p != null); searched = true; }
                    if (player == null) return;
                    file = Path.Combine(Path.GetTempPath(), "fishbowl-sound-" + Guid.NewGuid().ToString("N") + ".wav");
                    File.WriteAllBytes(file, Immersion.Wave(volume, launch));
                    var info = new ProcessStartInfo(player) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                    if (Path.GetFileName(player) == "aplay") info.ArgumentList.Add("-q");
                    info.ArgumentList.Add(file);
                    using (var process = Process.Start(info)) { if (process != null && !process.WaitForExit(3000)) process.Kill(); }
                }
                catch (Exception error) { Store.Log("Interface sound unavailable: " + error.Message); }
                finally
                {
                    try { if (file != null) File.Delete(file); } catch (IOException) { }
                    lock (gate) busy = false;
                }
            });
        }
    }

    // Windows SessionRecap: an optional note and personal rating after a verified play session.
    public static class SessionRecapDialog
    {
        public static async Task Show(Window owner, LibraryData data, GameEntry game, PlaySession session)
        {
            var dialog = new FishDialog("Session recap — " + game.Title, 560); var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(Ui.Caption("Recorded session"));
            stack.Children.Add(Ui.Text(TimeSpan.FromSeconds(Math.Max(0, session.Seconds)).ToString() + " (launched process)", 15));
            var note = Ui.Paragraphs("", false); note.MinHeight = 100; note.MaxLength = 2000;
            stack.Children.Add(Ui.Caption("Session note (optional)")); stack.Children.Add(note);
            var rating = Ui.Combo(new[] { "0", "1", "2", "3", "4", "5" }, Math.Max(0, Math.Min(5, game.PersonalRating)).ToString());
            stack.Children.Add(Ui.Caption("Personal rating (0 = unset)")); stack.Children.Add(rating);
            var footer = dialog.Footer("Save note / rating", () =>
            {
                Immersion.SaveRecap(game, session, note.Text, Int32.Parse(rating.SelectedItem as string ?? "0"));
                Store.Save(data); return Task.FromResult(true);
            });
            stack.Children.Add(footer);
            dialog.Body = stack;
            await dialog.Present(owner);
        }
    }
}
