using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;

namespace EmulatorHub
{
    // The FishBowl hub: emulator list, information panel, menus and status bar (MainForm in the Windows build).
    public partial class MainWindow : Window
    {
        private static readonly List<string> pendingWarnings = new List<string>();
        private readonly LibraryData library;
        private readonly Palette p;
        private readonly ListBox emulatorList = new ListBox();
        private readonly TextBox filterBox = new TextBox();
        private readonly TextBlock status = new TextBlock();
        private readonly Button editButton = new Button(), removeButton = new Button(), managementButton = new Button(), favoriteButton = new Button(), informationButton = new Button();
        private readonly ComboBox platformFilter = new ComboBox();
        private readonly CheckBox favoritesOnly = new CheckBox();
        private readonly TextBlock infoTitle = new TextBlock(), infoVersion = new TextBlock(), infoPlatform = new TextBlock(), notesState = new TextBlock();
        private TextBox overviewText, setupText, controllersText, helpText, notesBox;
        private readonly TabControl infoTabs = new TabControl();
        private readonly StackPanel folderPanel = new StackPanel { Spacing = 2 };
        private readonly DispatcherTimer notesTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        private readonly DispatcherTimer gameFolderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        private readonly DispatcherTimer runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
        private readonly List<FileSystemWatcher> gameFolderWatchers = new List<FileSystemWatcher>();
        private readonly List<Tuple<Button, Func<EmulatorReference, string>>> referenceButtons = new List<Tuple<Button, Func<EmulatorReference, string>>>();
        private readonly Dictionary<string, EmulatorRow> rows = new Dictionary<string, EmulatorRow>();
        private readonly Dictionary<string, Bitmap> imageCache = new Dictionary<string, Bitmap>();
        private readonly Dictionary<string, Tuple<DateTime, Task<string>>> versionCache = new Dictionary<string, Tuple<DateTime, Task<string>>>();
        private readonly MenuItem linksMenu = new MenuItem { Header = "Links" }, convertersMenu = new MenuItem { Header = "Game converters" };
        private Grid split; private Control emulatorInformation;
        private string notesProfileId, selectedEmulatorId, folderRequestKey;
        private bool loadingInformation, notesDirty, loadingFilters, refreshing, reloadProgramMetadata, pollingRuntime;
        private int metadataGeneration, folderRequest;
        private WindowState savedWindowState;

        private FontFamily DisplayFont { get { return Ui.Font; } }

        public MainWindow()
        {
            Store.ShowWarning = message => { if (IsVisible) Ui.Post(async () => await Ui.Message(this, message)); else pendingWarnings.Add(message); };
            library = Store.Load();
            p = Palette.Apply(library.Theme);
            ((App)Application.Current).ApplyPalette(p);
            Ui.Font = Palette.Font(library.Theme);
            Title = "FishBowl 1.0"; Icon = Ui.AppIcon(); FontFamily = DisplayFont;
            MinWidth = 1100; MinHeight = 700; Width = 1340; Height = 820;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            RestoreWindowLayout();
            if (library.Theme.StartMaximized) WindowState = WindowState.Maximized;
            Background = p.BottomBrush; Foreground = p.InkBrush;
            BuildLayout();
            RefreshHub();
            ConfigureGameFolderWatchers();
            SetStatus("Double-click an emulator to open it. Manage games inside the emulator.");
            // Bubble, so open menus and drop-downs handle Escape/Enter before the hub shortcuts.
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
            runtimeTimer.Tick += delegate { RefreshRuntimeStatus(); }; runtimeTimer.Start();
            notesTimer.Tick += async delegate { await Ui.Run(this, () => { SaveProfileNotes(); return Task.CompletedTask; }); };
            gameFolderTimer.Tick += async delegate { gameFolderTimer.Stop(); await Ui.Run(this, () => { SyncWatchedGameFolders(); return Task.CompletedTask; }); };
            Opened += async delegate
            {
                KeepOnScreen();
                foreach (var warning in pendingWarnings.ToArray()) await Ui.Message(this, warning);
                pendingWarnings.Clear();
                if (library.Theme.ShowStartupAssistant) await ShowFirstRunGuide();
                if (library.Theme.ShowGameStorageAssistant) await ShowGameStoragePrompt();
            };
            Closing += (sender, e) =>
            {
                try { SaveProfileNotes(); SaveWindowLayout(); }
                catch (Exception error) { e.Cancel = true; Ui.Post(async () => await Ui.Message(this, "Your notes could not be saved.\n\n" + error.Message)); }
            };
            Closed += delegate { runtimeTimer.Stop(); foreach (var watcher in gameFolderWatchers) watcher.Dispose(); };
        }

        // ----- Layout ------------------------------------------------------------------------------------------------

        private void BuildLayout()
        {
            var shell = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), Background = p.BottomBrush };
            shell.Children.Add(BuildMenu());
            var banner = new Grid { Background = p.TopBrush, Height = 76, ColumnDefinitions = new ColumnDefinitions("Auto,*"), IsVisible = library.Theme.ShowBanner };
            banner.Children.Add(new Image { Source = Ui.Logo(), Width = 60, Height = 60, Margin = new Thickness(12, 8), Stretch = Stretch.Uniform });
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
            titles.Children.Add(Ui.Text("FishBowl", 24, true));
            titles.Children.Add(Ui.Text("Your emulators, together. Add and play games inside each emulator.", 12, false, p.SubtleBrush));
            Grid.SetColumn(titles, 1); banner.Children.Add(titles);
            Grid.SetRow(banner, 1); shell.Children.Add(banner);
            var actions = BuildPrimaryActions(); Grid.SetRow(actions, 2); shell.Children.Add(actions);

            split = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,8,2*") };
            split.ColumnDefinitions[0].MinWidth = 450; split.ColumnDefinitions[2].MinWidth = 370;
            split.Children.Add(BuildEmulatorList());
            var splitter = new GridSplitter { Background = p.TopBrush, ResizeDirection = GridResizeDirection.Columns }; Grid.SetColumn(splitter, 1); split.Children.Add(splitter);
            emulatorInformation = BuildInformationPanel(); Grid.SetColumn(emulatorInformation, 2); split.Children.Add(emulatorInformation);
            Grid.SetRow(split, 3); shell.Children.Add(split);

            var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,240,*"), Background = p.TopBrush, Height = 44, IsVisible = library.Theme.ShowStatusBar };
            footer.Children.Add(new TextBlock { Text = "Filter:", Foreground = p.SubtleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) });
            filterBox.Watermark = "Name, platform or notes"; filterBox.VerticalAlignment = VerticalAlignment.Center;
            filterBox.TextChanged += delegate { RefreshHub(); };
            Grid.SetColumn(filterBox, 1); footer.Children.Add(filterBox);
            status.Foreground = p.SubtleBrush; status.VerticalAlignment = VerticalAlignment.Center; status.Margin = new Thickness(14, 0, 12, 0); status.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(status, 2); footer.Children.Add(status);
            Grid.SetRow(footer, 4); shell.Children.Add(footer);

            int percent = Math.Max(75, Math.Min(140, library.Theme.UiScalePercent == 0 ? 100 : library.Theme.UiScalePercent));
            Content = percent == 100 ? (Control)shell : new LayoutTransformControl { LayoutTransform = new ScaleTransform(percent / 100.0, percent / 100.0), Child = shell };

            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, (sender, e) => { var files = DroppedPaths(e); e.DragEffects = files.Any(Platform.IsLaunchFile) ? DragDropEffects.Copy : DragDropEffects.None; });
            AddHandler(DragDrop.DropEvent, async (sender, e) => { var files = DroppedPaths(e); await Ui.Run(this, () => HandleDroppedPaths(files)); });
        }
        private static List<string> DroppedPaths(DragEventArgs e)
        {
            var items = e.Data.GetFiles();
            return items == null ? new List<string>() : items.Select(i => i.TryGetLocalPath()).Where(p => p != null).ToList();
        }

        private Control BuildEmulatorList()
        {
            var host = new DockPanel();
            var header = new Grid { ColumnDefinitions = EmulatorRow.Columns(), Background = p.SurfaceBrush, Height = 30 };
            var titles = new[] { "Emulator", "Status", "Platform / preset", "Program" };
            for (int i = 0; i < titles.Length; i++)
            { var t = new TextBlock { Text = titles[i], Foreground = p.SubtleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) }; Grid.SetColumn(t, i); header.Children.Add(t); }
            DockPanel.SetDock(header, Dock.Top); host.Children.Add(header);
            emulatorList.Background = p.BottomBrush; emulatorList.Foreground = p.InkBrush; emulatorList.SelectionMode = SelectionMode.Single; emulatorList.Padding = new Thickness(0);
            emulatorList.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel>(() => new StackPanel());
            emulatorList.SelectionChanged += delegate { if (!refreshing) ApplyEmulatorSelection(); };
            emulatorList.DoubleTapped += async (sender, e) =>
            {
                if (e.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>(true) == null) return;
                await Ui.Run(this, OpenSelectedEmulator);
            };
            emulatorList.AddHandler(PointerPressedEvent, (sender, e) =>
            {
                if (e.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>(true) == null) ClearEmulatorSelection();
            }, RoutingStrategies.Tunnel);
            emulatorList.ContextMenu = new ContextMenu
            {
                ItemsSource = new object[] {
                    MenuAction("Open emulator", "play", OpenSelectedEmulator), MenuAction("Edit emulator", "edit", EditEmulator),
                    MenuAction("Favorite / unfavorite", "star", ToggleEmulatorFavorite), MenuAction("Edit information and links", "info", EditEmulatorInformation),
                    MenuAction("Open program folder", "folder", OpenEmulatorFolder), new Separator(), MenuAction("Remove from FishBowl", "remove", RemoveEmulator) }
            };
            emulatorList.ContextRequested += (sender, e) => { if (CurrentEmulator() == null) e.Handled = true; };
            host.Children.Add(emulatorList);
            return host;
        }

        private Control BuildPrimaryActions()
        {
            // Wraps instead of clipping: tiling window managers can size the window below its minimum width.
            var bar = new WrapPanel { Orientation = Orientation.Horizontal, Background = p.TopBrush, Margin = new Thickness(0), MinHeight = 48 };
            bar.Children.Add(new Border { Width = 8, Height = 48 });
            bar.Children.Add(Ui.Action("Add emulator", AddEmulator, true));
            Ui.SetAction(editButton, "Edit emulator"); Wire(editButton, EditEmulator); bar.Children.Add(editButton);
            Ui.SetAction(removeButton, "Remove"); Wire(removeButton, RemoveEmulator); bar.Children.Add(removeButton);
            Ui.SetAction(managementButton, "Manage"); Wire(managementButton, () => ShowEmulatorManager("Setup checks")); bar.Children.Add(managementButton);
            var add = bar.Children.OfType<Button>().First(); add.Margin = new Thickness(0, 7, 8, 7);
            foreach (var b in new[] { editButton, removeButton, managementButton }) { b.Padding = new Thickness(12, 6); b.Margin = new Thickness(0, 7, 8, 7); b.MinHeight = 34; b.CornerRadius = new CornerRadius(6); b.VerticalAlignment = VerticalAlignment.Center; }
            favoritesOnly.Content = "Favorites only"; favoritesOnly.Foreground = p.InkBrush; favoritesOnly.Margin = new Thickness(16, 7, 12, 7); favoritesOnly.VerticalAlignment = VerticalAlignment.Center;
            favoritesOnly.IsCheckedChanged += delegate { RefreshHub(); };
            bar.Children.Add(favoritesOnly);
            platformFilter.Width = 240; platformFilter.VerticalAlignment = VerticalAlignment.Center; platformFilter.Margin = new Thickness(8, 7, 8, 7);
            platformFilter.SelectionChanged += delegate { if (!loadingFilters) RefreshHub(); };
            bar.Children.Add(platformFilter);
            return bar;
        }
        private void Wire(Button button, Func<Task> action) { button.Click += async delegate { await Ui.Run(this, action); }; }
        private void Wire(Button button, Action action) { Wire(button, () => { action(); return Task.CompletedTask; }); }

        private MenuItem MenuAction(string text, string icon, Func<Task> action)
        {
            var item = new MenuItem { Header = text, Icon = new FishIcon(icon, 18) };
            item.Click += async delegate { await Ui.Run(this, action); };
            return item;
        }
        private MenuItem MenuAction(string text, string icon, Action action) { return MenuAction(text, icon, () => { action(); return Task.CompletedTask; }); }
        private static MenuItem Disabled(string text) { return new MenuItem { Header = text, IsEnabled = false }; }

        private Control BuildMenu()
        {
            var menu = new Menu { Background = p.TopBrush, Foreground = p.InkBrush };
            var file = new MenuItem { Header = "_File", ItemsSource = new object[] {
                MenuAction("Add emulator...", "add", AddEmulator),
                MenuAction("Setup assistant / import emulator package...", "add", () => ShowEmulatorManager("Setup assistant")),
                MenuAction("Game storage organizer...", "folder", () => ShowGameStorageOrganizer(null)),
                MenuAction("Open selected emulator", "play", OpenSelectedEmulator), new Separator(),
                MenuAction("Exit", "power", Close) } };
            var emulators = new MenuItem { Header = "_Emulators", ItemsSource = new object[] {
                MenuAction("Setup assistant / emulator folder...", "folder", () => ShowEmulatorManager("Setup assistant")),
                MenuAction("Find installed emulators...", "add", () => ShowEmulatorManager("Find installed")),
                MenuAction("Check releases / updates...", "refresh", () => ShowEmulatorManager("Updates")),
                MenuAction("Manage installed builds...", "storage", () => ShowEmulatorManager("Versions")),
                MenuAction("Emulator backups / restore...", "export", () => ShowEmulatorManager("Backups")),
                MenuAction("Setup checks...", "info", () => ShowEmulatorManager("Setup checks")),
                MenuAction("Official setup guidance...", "info", OpenOfficialSetupGuidance),
                MenuAction("Repair selected location...", "folder", RepairSelectedLocation) } };
            var tools = new MenuItem { Header = "_Tools", ItemsSource = new object[] {
                MenuAction("Edit selected emulator...", "edit", EditEmulator),
                MenuAction("Edit information and links...", "info", EditEmulatorInformation),
                MenuAction("Favorite / unfavorite emulator", "star", ToggleEmulatorFavorite),
                MenuAction("Open emulator folder", "folder", OpenEmulatorFolder), new Separator(),
                MenuAction("Export FishBowl settings...", "export", ExportLibrary),
                MenuAction("Import FishBowl settings...", "import", ImportLibrary),
                MenuAction(Platform.ShortcutActionLabel, "desktop", CreateShortcut),
                MenuAction("FishBowl settings...", "settings", ShowSettings),
                MenuAction("Enable portable mode", "storage", EnablePortableMode),
                MenuAction("Open FishBowl activity log", "info", () => { if (!File.Exists(Store.LogFileName)) Store.Log("Activity log opened."); Platform.OpenTextFile(Store.LogFileName); }),
                MenuAction("Diagnostics report...", "info", ShowDiagnostics) } };
            var view = new MenuItem { Header = "_View", ItemsSource = new object[] {
                MenuAction("Refresh emulators", "refresh", () => { reloadProgramMetadata = true; RefreshHub(); }),
                MenuAction("Full screen (F11)", "desktop", ToggleFullScreen) } };
            convertersMenu.SubmenuOpened += delegate { RefreshConvertersMenu(); }; RefreshConvertersMenu();
            linksMenu.SubmenuOpened += delegate { RefreshWebsiteLinksMenu(); }; RefreshWebsiteLinksMenu();
            var help = new MenuItem { Header = "_Help", ItemsSource = new object[] {
                MenuAction("About FishBowl", "info", () => Ui.Message(this, "FishBowl opens your separately installed emulators.\n\nAdd games, manage libraries, and configure controls inside the dedicated emulator.")),
                MenuAction("First-run guide...", "info", ShowFirstRunGuide),
                MenuAction("Game storage guide...", "folder", ShowGameStoragePrompt) } };
            menu.ItemsSource = new[] { file, emulators, tools, view, convertersMenu, linksMenu, help };
            return menu;
        }

        // ----- Information panel -------------------------------------------------------------------------------------

        private Control BuildInformationPanel()
        {
            var panel = new DockPanel { Background = p.TopBrush };
            var head = new StackPanel { Margin = new Thickness(14, 12, 14, 4), Spacing = 2 };
            infoTitle.FontSize = 22; infoTitle.FontWeight = FontWeight.Bold; infoTitle.Foreground = p.InkBrush; infoTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            infoVersion.Foreground = infoPlatform.Foreground = p.SubtleBrush; infoVersion.TextTrimming = infoPlatform.TextTrimming = TextTrimming.CharacterEllipsis;
            head.Children.Add(infoTitle); head.Children.Add(infoVersion); head.Children.Add(infoPlatform);
            Ui.SetAction(favoriteButton, "Favorite"); Wire(favoriteButton, ToggleEmulatorFavorite);
            Ui.SetAction(informationButton, "Edit information"); Wire(informationButton, EditEmulatorInformation);
            foreach (var b in new[] { favoriteButton, informationButton }) { b.Padding = new Thickness(12, 6); b.CornerRadius = new CornerRadius(6); }
            head.Children.Add(Ui.Actions(favoriteButton, informationButton));
            DockPanel.SetDock(head, Dock.Top); panel.Children.Add(head);

            infoTabs.Margin = new Thickness(8, 0, 8, 8);
            overviewText = AddInfoTab("Overview", "info", new[] { Link("Project", r => r.Website), Link("Releases", r => r.Releases), Link("Changelog", r => r.Changelog) }, null);
            setupText = AddInfoTab("Setup", "settings", new[] { Link("Setup guide", r => r.Documentation), Link("Compatibility", r => r.Compatibility) }, null);
            Button controllerSettings = null;
            if (Platform.ControllerSettingsLabel != null) { controllerSettings = Ui.Action(Platform.ControllerSettingsLabel, Platform.OpenControllerSettings); }
            controllersText = AddInfoTab("Controls", "controller", new[] { Link("Controller guide", r => r.ControllerGuide) }, controllerSettings);
            infoTabs.Items.Add(Tab("Folders", "folder", new ScrollViewer { Content = folderPanel, Padding = new Thickness(10), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }));
            helpText = AddInfoTab("Help", "help", new[] { Link("Troubleshooting", r => r.Troubleshooting) }, null);
            notesBox = Ui.Paragraphs("", false); notesBox.Watermark = "Your notes about this emulator";
            notesBox.TextChanged += delegate { if (loadingInformation) return; notesDirty = true; notesState.Text = "Saving..."; notesTimer.Stop(); notesTimer.Start(); };
            notesState.Foreground = p.SubtleBrush; notesState.Margin = new Thickness(0, 8, 0, 0);
            var notes = new DockPanel { Margin = new Thickness(10) }; DockPanel.SetDock(notesState, Dock.Bottom); notes.Children.Add(notesState); notes.Children.Add(notesBox);
            infoTabs.Items.Add(Tab("Notes", "note", notes));
            infoTabs.SelectionChanged += (sender, e) => { if (e.Source == infoTabs) EnsureFolderRows(); };
            panel.Children.Add(infoTabs);
            return panel;
        }
        private static Tuple<string, Func<EmulatorReference, string>> Link(string label, Func<EmulatorReference, string> url) { return Tuple.Create(label, url); }
        private TabItem Tab(string title, string icon, Control content)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new FishIcon(icon, 16, p.Ink, p.Blue), new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center } } };
            return new TabItem { Header = header, Content = new Border { Background = p.BottomBrush, CornerRadius = new CornerRadius(6), Child = content }, Tag = title };
        }
        private TextBox AddInfoTab(string title, string icon, IEnumerable<Tuple<string, Func<EmulatorReference, string>>> links, Button extra)
        {
            var box = Ui.Paragraphs();
            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var link in links)
            {
                var getUrl = link.Item2;
                var button = Ui.Action(link.Item1, () => { var profile = CurrentEmulator(); if (profile != null) OpenInformationLink(getUrl(EmulatorReference.For(profile))); });
                button.Margin = new Thickness(0, 3, 6, 3); actions.Children.Add(button); referenceButtons.Add(Tuple.Create(button, getUrl));
            }
            if (extra != null) { extra.Margin = new Thickness(0, 3, 6, 3); actions.Children.Add(extra); }
            var layout = new DockPanel { Margin = new Thickness(10) }; DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions); layout.Children.Add(box);
            infoTabs.Items.Add(Tab(title, icon, layout));
            return box;
        }

        private void ShowInformation(bool show)
        {
            emulatorInformation.IsVisible = show;
            split.ColumnDefinitions[1].Width = show ? new GridLength(8) : new GridLength(0);
            split.ColumnDefinitions[2].MinWidth = show ? 370 : 0;
            split.ColumnDefinitions[2].Width = show ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        }

        private void UpdateInformationPanel()
        {
            var profile = CurrentEmulator();
            ShowInformation(profile != null && library.Theme.ShowInformationPanel);
            var nextId = profile == null ? null : profile.Id;
            if (notesProfileId != nextId)
            {
                folderRequest++; folderRequestKey = null;
                SaveProfileNotes(); loadingInformation = true;
                notesProfileId = nextId; notesBox.Text = profile == null ? "" : (profile.Notes ?? ""); notesDirty = false;
                notesState.Text = profile == null ? "Select an emulator to write notes." : "Notes save automatically."; loadingInformation = false;
            }
            favoriteButton.IsEnabled = informationButton.IsEnabled = notesBox.IsEnabled = profile != null;
            infoTitle.Text = profile == null ? "Emulator information" : profile.Name;
            var version = profile == null ? "" : String.IsNullOrWhiteSpace(profile.ManualVersion) ? "Checking version..." : profile.ManualVersion + " (entered manually)";
            infoVersion.Text = profile == null ? "" : "Installed version: " + version;
            infoPlatform.Text = profile == null ? "" : EmulatorReference.PlatformFor(profile);
            Ui.SetAction(favoriteButton, profile != null && profile.Favorite ? "Favorited" : "Favorite");
            if (profile == null)
            {
                overviewText.Text = "Select an emulator to see its information.\n\nAny " + Platform.OsName + " emulator can be added using Custom. Double-click a row to open it.";
                setupText.Text = controllersText.Text = helpText.Text = "Select an emulator first.";
                foreach (var link in referenceButtons) link.Item1.IsEnabled = false;
                folderRequest++; folderRequestKey = null; return;
            }
            var info = EmulatorReference.For(profile);
            overviewText.Text = info.Summary + "\n\nSTRENGTHS\n" + info.Strengths + "\n\nLIMITATIONS\n" + info.Limitations + "\n\nPROGRAM\n" + profile.Executable + "\n\nVERSION\n" + version;
            setupText.Text = Lf(info.Requirements) + "\n\nCOMPATIBILITY\n" + (String.IsNullOrWhiteSpace(info.Compatibility) ? "No separate compatibility page is linked for this profile. Check the project documentation, or add one in Edit information." : "Open Compatibility below to view the linked compatibility page in your browser. No games are imported into FishBowl.");
            controllersText.Text = Lf(info.Controllers) + (String.IsNullOrWhiteSpace(profile.ControllerProfileNotes) ? "" : "\n\nYOUR CONTROLLER NOTES\n" + Lf(profile.ControllerProfileNotes));
            helpText.Text = "PROGRAM WILL NOT OPEN\nCheck that the program exists and runs directly from " + Platform.OsName + "." + (Platform.IsWindows ? "" : " AppImages need execute permission (FishBowl sets it) and FUSE; Flatpaks must be installed for your user or system.") + " Re-select its path in Edit emulator after moving or updating it.\n\nFIRMWARE / BIOS ERRORS\nFollow the emulator's setup guide and check its configured firmware locations.\n\nGRAPHICS / PERFORMANCE\nReview the emulator's hardware requirements, update your graphics driver, and use a supported graphics backend. Change settings inside the emulator.\n\nCONTROLLER NOT DETECTED\nConnect it before opening the emulator" + (Platform.IsWindows ? ", check Windows controllers," : ", check that your system sees it (and, for Flatpaks, that the app has device access),") + " and review the emulator's input mapping and controller guide.\n\nLOGS\nUse the Folders tab to open your configured emulator log folder. FishBowl's own log is available from Tools.\n\nUse the linked troubleshooting guide for emulator-specific instructions.";
            foreach (var link in referenceButtons) link.Item1.IsEnabled = EmulatorReference.IsWebUrl(link.Item2(info));
            UpdateInstalledVersion(profile);
            EnsureFolderRows();
        }
        private static string Lf(string text) { return (text ?? "").Replace("\r\n", "\n"); }

        private void SaveProfileNotes()
        {
            notesTimer.Stop(); if (!notesDirty || notesProfileId == null) return;
            var profile = library.Emulators.FirstOrDefault(e => e.Id == notesProfileId);
            if (profile == null) { notesDirty = false; return; }
            profile.Notes = notesBox.Text; Store.Save(library); notesDirty = false; notesState.Text = "Saved.";
        }

        private async void UpdateInstalledVersion(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
            var executable = profile.Executable ?? ""; int generation = metadataGeneration;
            Tuple<DateTime, Task<string>> cached;
            if (!versionCache.TryGetValue(executable, out cached) || DateTime.UtcNow - cached.Item1 > TimeSpan.FromMinutes(1))
            {
                var snapshot = FolderSnapshot(profile);
                cached = Tuple.Create(DateTime.UtcNow, Task.Run(() => EmulatorReference.VersionFor(snapshot)));
                versionCache[executable] = cached;
            }
            var version = await cached.Item2;
            if (generation != metadataGeneration || CurrentEmulator() != profile || profile.Executable != executable || !String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
            infoVersion.Text = "Installed version: " + version;
            int marker = overviewText.Text.LastIndexOf("\n\nVERSION\n", StringComparison.Ordinal);
            if (marker >= 0) overviewText.Text = overviewText.Text.Substring(0, marker) + "\n\nVERSION\n" + version;
        }

        // ----- Folders tab -------------------------------------------------------------------------------------------

        private static EmulatorProfile FolderSnapshot(EmulatorProfile profile)
        {
            return new EmulatorProfile { Id = profile.Id, Name = profile.Name, Executable = profile.Executable, Preset = profile.Preset, ManualVersion = profile.ManualVersion,
                ConfigFolder = profile.ConfigFolder, InGameSaveFolder = profile.InGameSaveFolder, SaveStateFolder = profile.SaveStateFolder,
                ScreenshotFolder = profile.ScreenshotFolder, LogFolder = profile.LogFolder, SaveFolder = profile.SaveFolder };
        }
        private static string FolderKey(EmulatorProfile e)
        { return String.Join("\u0001", new[] { e.Id, e.Name, e.Executable, e.Preset, e.ConfigFolder, e.InGameSaveFolder, e.SaveStateFolder, e.ScreenshotFolder, e.LogFolder, e.SaveFolder }); }

        private bool FoldersTabSelected { get { var tab = infoTabs.SelectedItem as TabItem; return tab != null && (tab.Tag as string) == "Folders"; } }
        private void EnsureFolderRows()
        {
            var profile = CurrentEmulator();
            if (profile == null || !FoldersTabSelected) return;
            if (folderRequestKey != FolderKey(profile)) RefreshFolderRows(profile);
        }

        private async void RefreshFolderRows(EmulatorProfile profile)
        {
            int request = ++folderRequest; folderRequestKey = profile == null ? null : FolderKey(profile);
            if (profile == null) return;
            // Detect settings and path availability off the UI thread; never change emulator settings.
            var snapshot = FolderSnapshot(profile); string notice = "";
            folderPanel.Children.Clear(); AddFolderMessage("Checking emulator folders...");
            try
            {
                await Task.Delay(60);
                if (request != folderRequest) return;
                var detected = await Task.Run(() =>
                {
                    var detector = new EmulatorFolderDetector(); var locations = detector.Detect(snapshot); notice = detector.Notice;
                    var existing = new HashSet<string>(locations.Where(l => !String.IsNullOrWhiteSpace(l.Path) && Directory.Exists(l.Path)).Select(l => l.Path), Platform.PathComparer);
                    return Tuple.Create(locations, existing);
                });
                if (request != folderRequest || CurrentEmulator() == null || CurrentEmulator().Id != profile.Id || FolderKey(profile) != folderRequestKey) return;
                RenderFolderRows(profile, detected.Item1, notice, detected.Item2);
            }
            catch (Exception ex)
            {
                if (request != folderRequest) return;
                Store.Log("Folder shortcuts could not refresh: " + ex.Message);
                folderRequestKey = null; RenderFolderRows(profile, new List<EmulatorFolder>(), "Folder detection could not finish. Select another tab and return to retry.", new HashSet<string>());
            }
        }

        private void RenderFolderRows(EmulatorProfile profile, List<EmulatorFolder> locations, string notice, HashSet<string> existing)
        {
            folderPanel.Children.Clear();
            AddFolderMessage("In-game saves: progress saved by the game.\nSave states: snapshots created by the emulator.\nChoose sets a FishBowl shortcut. Auto restores detection.");
            var refresh = Ui.Action("Refresh detected folders", () => RefreshFolderRows(profile)); refresh.HorizontalAlignment = HorizontalAlignment.Left; refresh.Margin = new Thickness(0, 4, 0, 6);
            folderPanel.Children.Add(refresh);
            if (!String.IsNullOrWhiteSpace(notice)) AddFolderMessage(notice);
            foreach (var location in locations) AddFolderRow(location, profile, existing.Contains(location.Path));
            if (!String.IsNullOrWhiteSpace(profile.SaveFolder))
            {
                var classify = new WrapPanel();
                foreach (var kind in new[] { "InGameSaveFolder|Use as in-game saves", "SaveStateFolder|Use as save states" })
                {
                    var pieces = kind.Split('|'); var destination = pieces[0];
                    var button = Ui.Action(pieces[1], () => { profile.GetType().GetProperty(destination).SetValue(profile, profile.SaveFolder, null); profile.SaveFolder = ""; Store.Save(library); RefreshFolderRows(profile); });
                    button.Margin = new Thickness(0, 4, 8, 4); classify.Children.Add(button);
                }
                folderPanel.Children.Add(classify);
            }
        }
        private void AddFolderMessage(string text) { var label = Ui.Text(text, 12.5, false, p.SubtleBrush); label.Margin = new Thickness(0, 2, 0, 6); folderPanel.Children.Add(label); }
        private void AddFolderRow(EmulatorFolder location, EmulatorProfile profile, bool exists)
        {
            var path = location.Path; var property = location.Property == "Program" ? null : location.Property;
            var heading = Ui.Text(location.Label, 13, true); heading.Margin = new Thickness(0, 10, 0, 3);
            folderPanel.Children.Add(heading);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
            var value = Ui.Field(String.IsNullOrWhiteSpace(path) ? "No single folder detected" : path, true);
            row.Children.Add(value);
            var open = Ui.Action("Open", () => { if (Directory.Exists(path)) Platform.Open(path); }); open.IsEnabled = exists; open.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(open, 1); row.Children.Add(open);
            if (property != null)
            {
                var choose = Ui.Action("Choose", async () =>
                {
                    var folder = await Ui.PickFolder(this, "Choose the folder already used by " + profile.Name + " for " + location.Label.ToLowerInvariant(), Directory.Exists(path) ? path : null);
                    if (folder == null) return;
                    profile.GetType().GetProperty(property).SetValue(profile, folder, null); Store.Save(library); RefreshFolderRows(profile);
                });
                choose.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(choose, 2); row.Children.Add(choose);
                var auto = Ui.Action("Auto", () => { profile.GetType().GetProperty(property).SetValue(profile, "", null); Store.Save(library); RefreshFolderRows(profile); });
                auto.IsEnabled = !String.IsNullOrWhiteSpace(profile.GetType().GetProperty(property).GetValue(profile, null) as string);
                auto.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(auto, 3); row.Children.Add(auto);
            }
            folderPanel.Children.Add(row);
            var source = Ui.Text(location.Source + (!String.IsNullOrWhiteSpace(path) && !exists ? " • folder missing" : ""), 12, false, p.SubtleBrush); source.Margin = new Thickness(0, 3, 0, 0);
            folderPanel.Children.Add(source);
        }

        // ----- Hub list ----------------------------------------------------------------------------------------------

        private EmulatorProfile CurrentEmulator() { return library.Emulators.FirstOrDefault(e => e.Id == selectedEmulatorId); }
        private void SetStatus(string text) { status.Text = text; }

        private void RefreshPlatformOptions()
        {
            var items = new[] { "All platforms" }.Concat(library.Emulators.SelectMany(EmulatorReference.PlatformTags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s)).ToList();
            var current = platformFilter.ItemsSource as List<string>;
            if (current != null && current.SequenceEqual(items)) return;
            var selected = platformFilter.SelectedItem as string;
            loadingFilters = true; platformFilter.ItemsSource = items;
            platformFilter.SelectedItem = items.Contains(selected) ? selected : "All platforms"; loadingFilters = false;
        }

        private void RefreshHub()
        {
            SaveProfileNotes();
            RefreshPlatformOptions();
            refreshing = true;
            try
            {
                if (reloadProgramMetadata) { reloadProgramMetadata = false; metadataGeneration++; imageCache.Clear(); versionCache.Clear(); }
                var query = (filterBox.Text ?? "").Trim();
                var selectedPlatform = platformFilter.SelectedItem as string ?? "All platforms";
                var items = library.Emulators.Where(e => favoritesOnly.IsChecked != true || e.Favorite)
                    .Where(e => selectedPlatform == "All platforms" || EmulatorReference.PlatformTags(e).Contains(selectedPlatform, StringComparer.OrdinalIgnoreCase))
                    .Where(e => query.Length == 0 || (e.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || EmulatorReference.PlatformFor(e).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (e.Notes ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(e => e.Favorite).ThenBy(e => e.Name).ToList();
                var live = new HashSet<string>(library.Emulators.Select(ImageKey));
                foreach (var key in imageCache.Keys.Where(k => !live.Contains(k)).ToArray()) imageCache.Remove(key);
                emulatorList.Items.Clear(); rows.Clear();
                int index = 0; ListBoxItem selectedItem = null;
                foreach (var profile in items)
                {
                    var row = new EmulatorRow(library.Theme, p, profile, CachedImage(profile), index++, File.Exists(profile.Executable) ? "Ready" : "Missing program");
                    var item = new ListBoxItem { Content = row, Tag = profile.Id, Padding = new Thickness(0), MinHeight = 0 };
                    row.Attach(item);
                    rows[profile.Id] = row; emulatorList.Items.Add(item);
                    if (profile.Id == selectedEmulatorId) selectedItem = item;
                }
                if (selectedItem == null) selectedEmulatorId = null;
                emulatorList.SelectedItem = selectedItem;
                if (items.Count == 0) SetStatus(library.Emulators.Count == 0 ? "Click Add emulator to get started." : "No emulators match this filter.");
            }
            finally { refreshing = false; UpdateActions(); }
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
            RefreshRuntimeStatus();
        }
        private static string ImageKey(EmulatorProfile e) { return e.Id + "\u0001" + e.Executable + "\u0001" + e.IconPath + "\u0001" + e.Name; }
        private Bitmap CachedImage(EmulatorProfile profile)
        {
            Bitmap image; var key = ImageKey(profile);
            if (!imageCache.TryGetValue(key, out image)) { image = EmulatorImages.Load(profile); imageCache[key] = image; }
            return image;
        }

        private void ApplyEmulatorSelection()
        {
            var item = emulatorList.SelectedItem as ListBoxItem;
            var next = item == null ? null : item.Tag as string;
            if (next == selectedEmulatorId) return;
            SaveProfileNotes(); selectedEmulatorId = next; UpdateActions();
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
        }
        private void ClearEmulatorSelection()
        {
            SaveProfileNotes(); refreshing = true;
            try { emulatorList.SelectedItem = null; selectedEmulatorId = null; }
            finally { refreshing = false; UpdateActions(); }
            SetStatus("Select an emulator to see its information. Double-click to open.");
        }
        private void UpdateActions()
        {
            var selected = CurrentEmulator() != null;
            editButton.IsEnabled = removeButton.IsEnabled = managementButton.IsEnabled = selected;
            UpdateInformationPanel();
        }

        private async void RefreshRuntimeStatus()
        {
            if (pollingRuntime) return;
            pollingRuntime = true;
            try
            {
                var paths = library.Emulators.Select(e => e.Executable).Where(e => !String.IsNullOrWhiteSpace(e)).Distinct(Platform.PathComparer).ToArray();
                var states = await Task.Run(() => paths.ToDictionary(path => path, path =>
                {
                    var state = EmulatorRuntime.State(path);
                    return !File.Exists(path) ? "Missing program" : state == RuntimeState.Running ? "Running" : state == RuntimeState.Unknown ? "Status unknown" : "Ready";
                }, Platform.PathComparer));
                foreach (var profile in library.Emulators)
                {
                    EmulatorRow row; if (!rows.TryGetValue(profile.Id, out row)) continue;
                    string text; row.SetStatus(states.TryGetValue(profile.Executable ?? "", out text) ? text : "Missing program");
                }
            }
            catch (Exception ex) { Store.Log("Runtime status could not refresh: " + ex.Message); }
            finally { pollingRuntime = false; }
        }

        // ----- Keyboard ----------------------------------------------------------------------------------------------

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsActive) return;
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (ctrl && e.Key == Key.E) { e.Handled = true; await Ui.Run(this, AddEmulator); }
            else if (ctrl && e.Key == Key.F) { e.Handled = true; filterBox.Focus(); }
            else if (ctrl && e.Key == Key.G) { e.Handled = true; await Ui.Run(this, () => ShowGameStorageOrganizer(null)); }
            else if (e.Key == Key.F11) { e.Handled = true; ToggleFullScreen(); }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (WindowState == WindowState.FullScreen) ToggleFullScreen(); else if (!String.IsNullOrEmpty(filterBox.Text)) filterBox.Text = ""; else ClearEmulatorSelection();
            }
            else if (e.Key == Key.Enter && emulatorList.IsKeyboardFocusWithin) { e.Handled = true; await Ui.Run(this, OpenSelectedEmulator); }
        }

        private void ToggleFullScreen()
        {
            if (WindowState != WindowState.FullScreen) { savedWindowState = WindowState; WindowState = WindowState.FullScreen; SetStatus("Full-screen mode. Press F11 to return."); }
            else { WindowState = savedWindowState == WindowState.FullScreen ? WindowState.Normal : savedWindowState; SetStatus("Returned to the standard window."); }
        }

        private string WindowLayoutFile { get { return Path.Combine(Store.DataDirectory, "window-layout.json"); } }
        private void RestoreWindowLayout()
        {
            try
            {
                if (!File.Exists(WindowLayoutFile)) return;
                var values = Json.Deserialize<int[]>(File.ReadAllText(WindowLayoutFile));
                if (values == null || values.Length != 5) return;
                Width = Math.Max(1100, values[2]); Height = Math.Max(700, values[3]);
                if (values[0] != Int32.MinValue) { Position = new PixelPoint(values[0], values[1]); WindowStartupLocation = WindowStartupLocation.Manual; }
                WindowState = values[4] == 1 ? WindowState.Maximized : WindowState.Normal;
            }
            catch (Exception error) { Store.Log("Window layout could not be restored: " + error.Message); }
        }
        // A saved position can point at a monitor that is no longer connected; recentre on the primary screen.
        private void KeepOnScreen()
        {
            if (WindowState != WindowState.Normal || Screens.All.Count == 0) return;
            var bounds = new PixelRect(Position, PixelSize.FromSize(new Size(Width, Height), DesktopScaling));
            if (Screens.All.Any(s => s.WorkingArea.Intersects(bounds))) return;
            var area = (Screens.Primary ?? Screens.All[0]).WorkingArea;
            Position = new PixelPoint(area.X + Math.Max(0, (area.Width - bounds.Width) / 2), area.Y + Math.Max(0, (area.Height - bounds.Height) / 2));
        }
        private void SaveWindowLayout()
        {
            try
            {
                var state = WindowState == WindowState.FullScreen ? savedWindowState : WindowState;
                Directory.CreateDirectory(Store.DataDirectory);
                File.WriteAllText(WindowLayoutFile, Json.Serialize(new[] { Position.X, Position.Y, (int)Width, (int)Height, state == WindowState.Maximized ? 1 : 0 }));
            }
            catch (Exception error) { Store.Log("Window layout could not be saved: " + error.Message); }
        }
    }

    // One row of the hub list: icon tile and name, status dot, platform and program, with FishBowl's selection styling.
    public class EmulatorRow : Border
    {
        private readonly ThemeSettings theme; private readonly Palette p; private readonly int index;
        private readonly Border tile, marker, highlight; private readonly Ellipse dot; private readonly TextBlock statusText, name;
        private readonly List<TextBlock> texts = new List<TextBlock>();
        private string statusValue; private bool selected, hovered;
        public static ColumnDefinitions Columns() { return new ColumnDefinitions("36*,18*,29*,17*"); }

        public EmulatorRow(ThemeSettings theme, Palette palette, EmulatorProfile profile, Bitmap image, int index, string status)
        {
            this.theme = theme; p = palette; this.index = index;
            int size = theme.ListDensity == "Compact" ? 40 : theme.ListDensity == "Comfortable" ? 56 : 48;
            Height = size; BorderThickness = new Thickness(0, 1);
            var grid = new Grid { ColumnDefinitions = Columns() };
            marker = new Border { Width = 4, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Palette.Rgb(255, 164, 82)), IsVisible = false };
            var first = new DockPanel { Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
            if (theme.ShowEmulatorIcons)
            {
                var accent = p.Swatches[index % p.Swatches.Length];
                double radius = theme.IconTileShape == "Square" ? 2 : theme.IconTileShape == "Circular" ? 20 : 7;
                Control art = image != null ? (Control)new Image { Source = image, Width = 32, Height = 32, Stretch = Stretch.Uniform } : new Monogram(profile.Name, p.Swatches[Math.Abs(StableHash(profile.Id ?? profile.Name ?? "?")) % p.Swatches.Length]);
                highlight = new Border { CornerRadius = new CornerRadius(5), Margin = new Thickness(2), IsVisible = false };
                tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(radius), Background = new SolidColorBrush(Palette.Alpha(18, accent)), BorderBrush = new SolidColorBrush(Palette.Alpha(58, accent)), BorderThickness = new Thickness(1), Child = new Grid { Children = { highlight, art } } };
                art.HorizontalAlignment = HorizontalAlignment.Center; art.VerticalAlignment = VerticalAlignment.Center;
                tile.Margin = new Thickness(0, 0, 12, 0); DockPanel.SetDock(tile, Dock.Left); first.Children.Add(tile);
            }
            name = Cell((profile.Favorite ? "★ " : "") + (profile.Name ?? "Emulator")); name.Margin = new Thickness(0);
            first.Children.Add(name);
            grid.Children.Add(first);
            // A Grid (not a StackPanel) so the status text is width-limited and trims instead of overlapping the next column.
            var statusPanel = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(9, 0), VerticalAlignment = VerticalAlignment.Center };
            dot = new Ellipse { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            statusText = Cell(""); statusText.Margin = new Thickness(0); Grid.SetColumn(statusText, 1);
            statusPanel.Children.Add(dot); statusPanel.Children.Add(statusText);
            Grid.SetColumn(statusPanel, 1); grid.Children.Add(statusPanel);
            var platform = Cell(EmulatorReference.PlatformFor(profile)); Grid.SetColumn(platform, 2); grid.Children.Add(platform);
            var program = Cell(Path.GetFileName(profile.Executable ?? "")); Grid.SetColumn(program, 3); grid.Children.Add(program);
            ToolTip.SetTip(program, profile.Executable);
            Child = new Grid { Children = { grid, marker } };
            PointerEntered += delegate { hovered = true; Refresh(); };
            PointerExited += delegate { hovered = false; Refresh(); };
            SetStatus(status); Refresh();
        }
        private static int StableHash(string text) { unchecked { int h = 17; foreach (var c in text) h = h * 31 + c; return h == Int32.MinValue ? 0 : h; } }
        private TextBlock Cell(string text)
        {
            var block = new TextBlock { Text = text, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            texts.Add(block); return block;
        }
        public void Attach(ListBoxItem item)
        {
            item.PropertyChanged += (sender, e) => { if (e.Property == ListBoxItem.IsSelectedProperty) { selected = item.IsSelected; Refresh(); } };
        }
        public void SetStatus(string text)
        {
            if (statusValue == text) return;
            statusValue = text; statusText.Text = text; Refresh();
        }
        private Color DotColor()
        {
            bool available = statusValue == "Ready" || statusValue == "Running";
            return available ? (p.IsDark ? Palette.Rgb(118, 211, 161) : Palette.Rgb(35, 126, 82)) : statusValue == "Missing program" ? Palette.Rgb(246, 183, 105) : p.Subtle;
        }
        private void Refresh()
        {
            byte alpha = (byte)(theme.SelectionContrast == "Soft" ? 58 : theme.SelectionContrast == "Strong" ? 132 : 88);
            Background = new SolidColorBrush(selected ? Palette.Alpha(alpha, p.Blue) : theme.AlternateRowShading && index % 2 != 0 ? Palette.Alpha(20, p.Surface) : Colors.Transparent);
            BorderBrush = selected ? new SolidColorBrush(Palette.Rgb(207, 168, 255)) : Brushes.Transparent;
            marker.IsVisible = selected;
            var ink = selected ? Colors.White : p.Ink;
            foreach (var text in texts) text.Foreground = new SolidColorBrush(ink);
            dot.Fill = new SolidColorBrush(DotColor());
            statusText.Foreground = new SolidColorBrush(selected ? Colors.White : DotColor());
            if (highlight != null)
            {
                highlight.IsVisible = theme.EnableMotion && (selected || hovered);
                var light = p.IsDark ? Colors.White : Colors.Black;
                highlight.Background = new SolidColorBrush(Palette.Alpha((byte)(selected ? 32 : 24), light));
                highlight.BorderBrush = new SolidColorBrush(Palette.Alpha((byte)(selected ? 48 : 36), light)); highlight.BorderThickness = new Thickness(1);
            }
        }
    }

    // Initials on a tinted tile for emulators without artwork (MakeFallbackIcon in the Windows build).
    public class Monogram : Border
    {
        public Monogram(string name, Color color)
        {
            var words = (name ?? "?").Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var initials = String.Concat(words.Take(2).Select(w => w.Substring(0, 1).ToUpperInvariant()));
            Width = Height = 34; CornerRadius = new CornerRadius(7);
            Background = new SolidColorBrush(Palette.Alpha(42, color)); BorderBrush = new SolidColorBrush(Palette.Alpha(150, color)); BorderThickness = new Thickness(1.5);
            Child = new TextBlock { Text = initials.Length == 0 ? "?" : initials, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Palette.Rgb(242, 232, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
    }
}
