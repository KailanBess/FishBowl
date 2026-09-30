using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace EmulatorHub
{
    public class ResultsDialog : FishDialog
    {
        public ResultsDialog(string title, IEnumerable<string> rows) : base(title, 760, 440)
        {
            var list = new ListBox { ItemsSource = rows.ToList(), Background = Ui.P.SurfaceBrush, Foreground = Ui.P.InkBrush };
            var close = Ui.Action("Close", () => Close()); close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 12, 0, 0); close.IsCancel = true;
            var layout = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); layout.Children.Add(close); layout.Children.Add(list);
            Body = layout;
        }
    }

    public class TextPromptDialog : FishDialog
    {
        private readonly TextBox input;
        public TextPromptDialog(string title, string label, string value) : base(title, 460)
        {
            input = Ui.Field(value);
            Body = new StackPanel { Children = { Ui.Caption(label), input, Footer("Save", () => Task.FromResult(true)) } };
            Opened += delegate { input.Focus(); input.SelectAll(); };
        }
        public string Value { get { return input.Text ?? ""; } }
        // Returns the entered text, or null when cancelled.
        public static async Task<string> Ask(Window owner, string title, string label, string value)
        {
            var dialog = new TextPromptDialog(title, label, value); await dialog.Present(owner);
            return dialog.Confirmed ? dialog.Value : null;
        }
    }

    public class ReviewDialog : FishDialog
    {
        public ReviewDialog(string title, string detail, string action) : base(title, 780, 580)
        {
            var text = Ui.Paragraphs(detail); text.Background = Ui.P.SurfaceBrush;
            var footer = Footer(action, () => Task.FromResult(true));
            var layout = new DockPanel(); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer); layout.Children.Add(text);
            Body = layout;
        }
    }

    public class CandidatePickerDialog : FishDialog
    {
        private readonly ListBox choices;
        public DiscoveredEmulator Selected { get { return choices.SelectedItem as DiscoveredEmulator; } }
        public CandidatePickerDialog(IEnumerable<DiscoveredEmulator> items) : base("Choose the emulator program", 820, 440)
        {
            choices = new ListBox { ItemsSource = items.ToList(), Background = Ui.P.SurfaceBrush };
            if (choices.ItemCount > 0) choices.SelectedIndex = 0;
            choices.DoubleTapped += delegate { if (Selected != null) { Confirmed = true; Close(); } };
            var footer = Footer("Choose", () => Task.FromResult(Selected != null));
            var hint = Ui.Hint("Select the main emulator program. Helper tools and installers can also appear in the package.");
            var layout = new DockPanel(); DockPanel.SetDock(hint, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(hint); layout.Children.Add(footer); layout.Children.Add(choices);
            Body = layout;
        }
    }

    public class WebsiteLinkDialog : FishDialog
    {
        public WebsiteLink Link { get; private set; }
        public WebsiteLinkDialog() : base("Add Website Link", 460)
        {
            var name = Ui.Field(); var url = Ui.Field();
            Body = new StackPanel { Children = { Ui.Caption("Link name"), name, Ui.Caption("Website address"), url, Ui.Hint("Use a full link, for example https://example.com"),
                Footer("Save", async () =>
                {
                    Uri address;
                    if (!Uri.TryCreate((url.Text ?? "").Trim(), UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
                    { await Ui.Message(this, "Enter a complete http or https website address."); return false; }
                    Link = new WebsiteLink { Id = Guid.NewGuid().ToString("N"), Name = String.IsNullOrWhiteSpace(name.Text) ? address.Host : name.Text.Trim(), Url = address.AbsoluteUri };
                    return true;
                }) } };
        }
    }

    public class WebsiteLinkPicker : FishDialog
    {
        public WebsiteLink SelectedLink { get; private set; }
        public WebsiteLinkPicker(IEnumerable<WebsiteLink> savedLinks) : base("Remove Website Link", 460)
        {
            var links = savedLinks.OrderBy(item => item.Name).ToList();
            var combo = Ui.Combo(links.Select(l => l.Name + "  -  " + l.Url), null);
            Body = new StackPanel { Children = { Ui.Caption("Saved link"), combo, Footer("Remove", () => { SelectedLink = combo.SelectedIndex >= 0 ? links[combo.SelectedIndex] : null; return Task.FromResult(SelectedLink != null); }) } };
        }
    }

    // First-run and game-storage prompts share this layout: heading, text, "don't show again", two actions.
    public class AssistantPrompt : FishDialog
    {
        private readonly CheckBox dontShowAgain = Ui.Check("Don't show this when FishBowl opens", false);
        public bool DontShowAgain { get { return dontShowAgain.IsChecked == true; } }
        protected bool SecondaryChosen;
        protected AssistantPrompt(string title, string heading, string text, string secondary) : base(title, 580)
        {
            var header = Ui.Text(heading, 21, true, new SolidColorBrush(Palette.Rgb(255, 181, 106)));
            var body = Ui.Text(text, 13.5); body.Margin = new Thickness(0, 12, 0, 12);
            var open = Ui.Action(secondary, () => { SecondaryChosen = true; Confirmed = true; Close(); }, true);
            var cont = Ui.Action("Continue", () => { Confirmed = true; Close(); }); cont.IsDefault = true;
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0), Children = { cont, open } };
            Body = new StackPanel { Children = { header, body, dontShowAgain, row } };
        }
    }
    public class StartupAssistantDialog : AssistantPrompt
    {
        public bool OpenSetupAssistant { get { return SecondaryChosen; } }
        public StartupAssistantDialog() : base("Welcome to FishBowl", "FishBowl Setup Assistant",
            "Get your library ready in a few steps:\n\n• Add an emulator program" + (Platform.IsWindows ? " or shortcut." : ", AppImage or Flatpak — Find installed detects them for you.") + "\n• Review its setup checks and official requirements.\n• Configure controllers and graphics inside each emulator.\n• Add and play games inside the emulator.", "Open setup") { }
    }
    public class GameStoragePromptDialog : AssistantPrompt
    {
        public bool OpenOrganizer { get { return SecondaryChosen; } }
        public GameStoragePromptDialog() : base("FishBowl Game Storage", "Keep game files organized",
            "FishBowl can create a separate Games folder with a different folder for each detected console. Drop game files into the organizer or open it whenever you want to sort files.\n\nThis only organizes files on disk. Games are still opened and managed inside their own emulator.", "Open organizer") { }
    }

    public class EmulatorDialog : FishDialog
    {
        private readonly TextBox name = Ui.Field(), executable = Ui.Field(), iconPath = Ui.Field();
        private readonly ComboBox preset;
        private readonly TextBlock hint = Ui.Hint("");
        private readonly EmulatorProfile existingProfile;
        public EmulatorProfile Profile { get; private set; }

        public EmulatorDialog(EmulatorProfile existing = null, string prefillExecutable = null) : base(existing == null ? "Add Emulator" : "Edit Emulator", 640)
        {
            existingProfile = existing;
            var labels = new List<string> { EmulatorCatalog.Presets[0].Label }; labels.AddRange(EmulatorCatalog.Presets.Skip(1).OrderBy(x => x.Name).Select(x => x.Label));
            string initial;
            if (existing != null)
            {
                var selected = EmulatorCatalog.Find(existing.Preset);
                initial = selected.Name == "Custom" && !String.IsNullOrWhiteSpace(existing.Preset) ? existing.Preset : selected.Label;
                if (!labels.Contains(initial)) labels.Add(initial);
            }
            else initial = String.IsNullOrWhiteSpace(prefillExecutable) ? EmulatorCatalog.Presets[0].Label : EmulatorCatalog.ForExecutable(prefillExecutable).Label;
            preset = Ui.Combo(labels, initial); preset.MaxDropDownHeight = 460;
            preset.SelectionChanged += delegate { PresetChanged(); };
            var website = Ui.Action("Project website", () => { var selected = EmulatorCatalog.Find(PresetText); if (selected.Website.Length > 0) Platform.Open(selected.Website); });
            Body = new StackPanel { Children = {
                Ui.Caption("Preset"), preset,
                Ui.Caption("Display name"), name,
                Ui.Caption("Emulator program or launcher"), Ui.WithButton(executable, Ui.Action("Browse", PickExecutable)),
                Ui.Caption("Custom emulator image (optional)"), Ui.WithButton(iconPath, Ui.Action("Browse", PickIcon)),
                hint, Footer("Save", Save, website) } };
            if (existing != null) { name.Text = existing.Name; executable.Text = existing.Executable; iconPath.Text = existing.IconPath; }
            else if (!String.IsNullOrWhiteSpace(prefillExecutable)) { executable.Text = prefillExecutable; }
            PresetChanged();
            if (existing == null && !String.IsNullOrWhiteSpace(prefillExecutable) && String.IsNullOrWhiteSpace(name.Text)) name.Text = Path.GetFileNameWithoutExtension(prefillExecutable);
        }
        private string PresetText { get { return preset.SelectedItem as string ?? ""; } }
        private void PresetChanged()
        {
            var selected = EmulatorCatalog.Find(PresetText);
            if (existingProfile == null && selected.Name != "Custom") name.Text = selected.Name;
            hint.Text = selected.Name == "Azahar Plus" ? "For Nintendo 3DS, select the Azahar Plus program. Games are managed inside Azahar Plus." : "Select the installed emulator's " + (Platform.IsWindows ? "executable" : "program, AppImage, Flatpak launcher or .desktop entry") + ". Games and settings stay inside that emulator.";
            if (selected.Platform.IndexOf("experimental", StringComparison.OrdinalIgnoreCase) >= 0) hint.Text = "This emulator is experimental; compatibility varies. Manage games inside the emulator.";
            if (!Platform.IsWindows && selected.Name != "Custom" && selected.FlatpakId == null && selected.UnixExecutables.Length == 0) hint.Text = selected.Name + " has no Linux build. Add it with Custom through a Wine launcher script if you use one.";
        }
        private async Task PickExecutable()
        {
            var file = await Ui.PickFile(this, "Choose the emulator program", Platform.ProgramFilter, Directory.Exists(Path.GetDirectoryName(executable.Text ?? "") ?? "") ? Path.GetDirectoryName(executable.Text) : null);
            if (file == null) return;
            executable.Text = file;
            var detected = EmulatorCatalog.ForExecutable(file);
            if (preset.SelectedIndex == 0 && detected.Name != "Custom") preset.SelectedItem = detected.Label;
            if ((name.Text ?? "").Trim().Length == 0) name.Text = Path.GetFileNameWithoutExtension(file);
        }
        private async Task PickIcon()
        {
            var file = await Ui.PickFile(this, "Choose an emulator image", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.webp");
            if (file != null) iconPath.Text = file;
        }
        private async Task<bool> Save()
        {
            var path = (executable.Text ?? "").Trim().Trim('"');
            if ((name.Text ?? "").Trim().Length == 0 || !File.Exists(path) || !EmulatorReference.IsLaunchFile(path))
            {
                await Ui.Message(this, "Enter a display name and choose an existing " + Platform.OsName + " emulator " + Platform.LaunchFileKinds + "." + (!Platform.IsWindows && File.Exists(path) ? "\n\nThis file is not marked as a program. Use its AppImage, launcher script, or make it executable (chmod +x)." : ""));
                return false;
            }
            Profile = existingProfile ?? new EmulatorProfile { Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>() };
            Profile.Name = name.Text.Trim(); Profile.Preset = PresetText; Profile.Executable = Path.GetFullPath(path); Profile.IconPath = (iconPath.Text ?? "").Trim();
            return true;
        }
    }

    public class EmulatorInformationDialog : FishDialog
    {
        private readonly EmulatorProfile profile;
        private readonly Dictionary<string, TextBox> fields = new Dictionary<string, TextBox>();
        public EmulatorInformationDialog(EmulatorProfile selected) : base("Information - " + selected.Name, 720, 640)
        {
            profile = selected; MinWidth = 620; MinHeight = 520;
            var tabs = new TabControl();
            AddTab(tabs, "Overview", new[] { "Platform|Platforms (separate multiple systems with /)", "ManualVersion|Installed version (optional manual override)", "Description|Overview", "Strengths|Strengths / useful features", "Limitations|Known limitations" });
            AddTab(tabs, "Setup & controls", new[] { "Requirements|BIOS / firmware and hardware requirements", "ControllerInfo|Supported controllers / input setup", "ControllerProfileNotes|Your controller notes" });
            AddTab(tabs, "Links", new[] { "WebsiteUrl|Project website", "DocumentationUrl|Setup / documentation", "CompatibilityUrl|Compatibility list", "ReleasesUrl|Releases / downloads", "ChangelogUrl|Changelog", "ControllerGuideUrl|Controller setup guide", "TroubleshootingUrl|Troubleshooting / support" });
            AddTab(tabs, "Folders", new[] { "ConfigFolder|Configuration override (blank = automatic)", "InGameSaveFolder|In-game saves override (blank = automatic)", "SaveStateFolder|Save states override (blank = automatic)", "ScreenshotFolder|Screenshots override (blank = automatic)", "LogFolder|Logs override (blank = automatic)", "SaveFolder|Previous unclassified save shortcut (optional)" });
            var hint = Ui.Hint("These details work with every emulator. Leave a field blank to use its preset information, when available.");
            var footer = Footer("Save", Save);
            var layout = new DockPanel(); DockPanel.SetDock(hint, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(hint); layout.Children.Add(footer); layout.Children.Add(tabs);
            Body = layout;
        }
        private void AddTab(TabControl tabs, string title, IEnumerable<string> definitions)
        {
            var panel = new StackPanel { Margin = new Thickness(4, 4, 14, 4) };
            foreach (var definition in definitions)
            {
                var parts = definition.Split('|'); var property = parts[0];
                bool multiline = new[] { "Description", "Strengths", "Limitations", "Requirements", "ControllerInfo", "ControllerProfileNotes" }.Contains(property);
                var value = ((profile.GetType().GetProperty(property).GetValue(profile, null) as string) ?? "").Replace("\r\n", "\n");
                var box = multiline ? Ui.Paragraphs(value, false) : Ui.Field(value);
                if (multiline) { box.Height = 100; box.Background = Ui.P.SurfaceBrush; }
                fields.Add(property, box); panel.Children.Add(Ui.Caption(parts[1])); panel.Children.Add(box);
            }
            tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = panel } });
        }
        private async Task<bool> Save()
        {
            foreach (var entry in fields.Where(f => f.Key.EndsWith("Url")))
                if (!String.IsNullOrWhiteSpace(entry.Value.Text) && !EmulatorReference.IsWebUrl(entry.Value.Text.Trim()))
                { await Ui.Message(this, "Use a full http or https web address for each link, or leave it blank."); return false; }
            foreach (var entry in fields) profile.GetType().GetProperty(entry.Key).SetValue(profile, (entry.Value.Text ?? "").Trim(), null);
            return true;
        }
    }

    public class SettingsDialog : FishDialog
    {
        public ThemeSettings ChosenTheme { get; private set; }
        public string BackupFolder { get; private set; }
        public SettingsDialog(ThemeSettings currentTheme, string currentBackupFolder) : base("FishBowl Settings", 680, 780)
        {
            var original = currentTheme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true };
            var theme = Ui.Combo(Palette.Themes, original.Name ?? "Twilight");
            var backup = Ui.Field(currentBackupFolder);
            var startup = Ui.Check("Show the setup assistant when FishBowl opens", !original.StartupAssistantPreferenceSet || original.ShowStartupAssistant);
            var storage = Ui.Check("Show the game storage assistant when FishBowl opens", !original.GameStorageAssistantPreferenceSet || original.ShowGameStorageAssistant);
            var maximized = Ui.Check("Start FishBowl maximized", original.StartMaximized);
            var autoSync = Ui.Check("Automatically sync configured game folders", original.AutoSyncGameFolders);
            var days = new NumericUpDown { Minimum = 1, Maximum = 90, Value = Math.Max(1, Math.Min(90, original.AutoBackupDays)), Width = 130, HorizontalAlignment = HorizontalAlignment.Left, FormatString = "0" };
            var accent = Ui.Combo(Palette.Accents, String.IsNullOrWhiteSpace(original.AccentColor) ? "Sunset" : original.AccentColor);
            var fonts = Palette.FontChoices.ToList(); if (!String.IsNullOrWhiteSpace(original.FontFamily) && !fonts.Contains(original.FontFamily)) fonts.Add(original.FontFamily);
            var font = Ui.Combo(fonts, String.IsNullOrWhiteSpace(original.FontFamily) ? "Bahnschrift" : original.FontFamily);
            var scale = Ui.Combo(new[] { "75%", "80%", "85%", "90%", "100%", "110%", "120%", "125%", "130%", "140%" }, (original.UiScalePercent <= 0 ? 100 : original.UiScalePercent) + "%");
            var density = Ui.Combo(new[] { "Compact", "Standard", "Comfortable" }, String.IsNullOrWhiteSpace(original.ListDensity) ? "Standard" : original.ListDensity);
            var banner = Ui.Check("Show FishBowl banner", original.ShowBanner);
            var statusBar = Ui.Check("Show filter and status bar", original.ShowStatusBar);
            var information = Ui.Check("Show emulator information pane", original.ShowInformationPanel);
            var icons = Ui.Check("Show emulator icons", original.ShowEmulatorIcons);
            var motion = Ui.Check("Enable hover and selection motion", original.EnableMotion);
            var alternate = Ui.Check("Use subtle alternating rows", original.AlternateRowShading);
            var contrast = Ui.Combo(new[] { "Soft", "Standard", "Strong" }, String.IsNullOrWhiteSpace(original.SelectionContrast) ? "Standard" : original.SelectionContrast);
            var shape = Ui.Combo(new[] { "Square", "Rounded", "Circular" }, String.IsNullOrWhiteSpace(original.IconTileShape) ? "Rounded" : original.IconTileShape);
            Func<string, IBrush, TextBlock> section = (text, brush) => { var t = Ui.Text(text, 15, true, brush); t.Margin = new Thickness(0, 18, 0, 4); return t; };
            var orange = new SolidColorBrush(Palette.Rgb(255, 181, 106));
            Func<Control[], Grid> columns = items =>
            {
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(String.Join(",", items.Select(i => "*"))) };
                for (int i = 0; i < items.Length; i++) { items[i].Margin = new Thickness(0, 0, i == items.Length - 1 ? 0 : 12, 0); Grid.SetColumn(items[i], i); grid.Children.Add(items[i]); }
                return grid;
            };
            Func<string, Control, StackPanel> labelled = (text, control) => new StackPanel { Children = { Ui.Caption(text), control } };
            var content = new StackPanel { Margin = new Thickness(0, 0, 14, 0), Spacing = 2, Children = {
                Ui.Caption("Theme"), theme,
                Ui.Caption("Cloud-synced backup folder (optional)"), Ui.WithButton(backup, Ui.Action("Browse", async () => { var folder = await Ui.PickFolder(this, "Choose a backup folder", backup.Text); if (folder != null) backup.Text = folder; })),
                Ui.Hint("Choose a Nextcloud, Syncthing, Dropbox, or other synced folder to store emulator backups."),
                startup, storage, maximized, autoSync,
                Ui.Caption("Backup reminder interval (days)"), days,
                section("Appearance", orange),
                columns(new Control[] { labelled("Accent color", accent), labelled("Interface font", font), labelled("Interface scale", scale) }),
                labelled("List density", density),
                columns(new Control[] { new StackPanel { Children = { banner, statusBar, information } }, new StackPanel { Children = { icons, motion } } }),
                section("Fine tuning", orange),
                alternate,
                columns(new Control[] { labelled("Selection contrast", contrast), labelled("Icon tile shape", shape) }),
                Ui.Hint("Appearance changes apply after restart.") } };
            var footer = Footer("Save", () =>
            {
                int percent; if (!Int32.TryParse(((scale.SelectedItem as string) ?? "100").TrimEnd('%'), out percent)) percent = 100;
                ChosenTheme = new ThemeSettings { Name = theme.SelectedItem as string, AutoBackupDays = (int)(days.Value ?? 7), LastBackupAt = original.LastBackupAt, DiscordRichPresenceEnabled = original.DiscordRichPresenceEnabled,
                    ShowStartupAssistant = startup.IsChecked == true, StartupAssistantPreferenceSet = true, ShowGameStorageAssistant = storage.IsChecked == true, GameStorageAssistantPreferenceSet = true,
                    StartMaximized = maximized.IsChecked == true, AutoSyncGameFolders = autoSync.IsChecked == true, ConfirmBeforeGameLaunch = original.ConfirmBeforeGameLaunch,
                    AccentColor = accent.SelectedItem as string, FontFamily = font.SelectedItem as string, UiScalePercent = percent, ListDensity = density.SelectedItem as string,
                    ShowBanner = banner.IsChecked == true, ShowStatusBar = statusBar.IsChecked == true, ShowInformationPanel = information.IsChecked == true, ShowEmulatorIcons = icons.IsChecked == true,
                    EnableMotion = motion.IsChecked == true, AlternateRowShading = alternate.IsChecked == true, SelectionContrast = contrast.SelectedItem as string, IconTileShape = shape.SelectedItem as string, CustomizationVersion = 3 };
                BackupFolder = (backup.Text ?? "").Trim();
                return Task.FromResult(true);
            });
            var layout = new DockPanel(); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer); layout.Children.Add(new ScrollViewer { Content = content });
            Body = layout;
        }
    }

    public class GameStorageOrganizerDialog : FishDialog
    {
        private readonly LibraryData library;
        private readonly ListBox queue = new ListBox();
        private readonly TextBox root;
        private readonly CheckBox copyFiles = Ui.Check("Copy files instead of moving them", false);
        private readonly ComboBox forcedConsole;
        private readonly List<string> queued = new List<string>();
        public GameStorageOrganizerDialog(LibraryData library, IEnumerable<string> droppedPaths) : base("FishBowl Game Storage Organizer", 780, 560)
        {
            this.library = library; MinWidth = 680; MinHeight = 450;
            root = Ui.Field(GameStorage.Root(library), true);
            forcedConsole = Ui.Combo(new[] { "Auto detect by file type", "Nintendo Entertainment System", "Super Nintendo", "Game Boy", "Game Boy Color", "Game Boy Advance", "Nintendo DS", "Nintendo 3DS", "Nintendo 64", "Nintendo GameCube", "Nintendo Wii", "Nintendo Wii U", "PlayStation", "PlayStation Portable", "PlayStation Vita", "Xbox", "Xbox 360", "Sega Genesis", "Disc Images", "Arcade and Archives" }, "Auto detect by file type");
            forcedConsole.Width = 300; forcedConsole.HorizontalAlignment = HorizontalAlignment.Left;
            queue.Background = Ui.P.SurfaceBrush;
            DragDrop.SetAllowDrop(queue, true);
            queue.AddHandler(DragDrop.DropEvent, (s, e) => { var files = e.Data.GetFiles(); if (files != null) AddPaths(files.Select(f => f.TryGetLocalPath()).Where(f => f != null)); });
            var top = new StackPanel { Children = {
                Ui.Text("Game Storage Organizer", 21, true),
                Ui.Hint("Files are sorted into separate console folders. FishBowl does not add them to, or launch them from, the app."),
                Ui.Caption("Games root"), Ui.WithButton(root, Ui.Action("Choose root", ChooseRoot)),
                Ui.Caption("Destination"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { forcedConsole, copyFiles } },
                Ui.Caption("Queued files (drop files or folders here)") } };
            var sort = Ui.Action("Sort queued files", SortFiles, true);
            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(sort, Dock.Right); bottom.Children.Add(sort);
            bottom.Children.Add(Ui.Actions(Ui.Action("Add files", AddFiles), Ui.Action("Add folder", AddFolder), Ui.Action("Clear", () => { queued.Clear(); RefreshQueue(); }), Ui.Action("Open root", OpenRoot)));
            var layout = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom);
            layout.Children.Add(top); layout.Children.Add(bottom); layout.Children.Add(queue);
            Body = layout;
            AddPaths(droppedPaths);
        }
        private async Task AddFiles() { AddPaths(await Ui.PickFiles(this, "Choose game files", "All files|*", null, true)); }
        private async Task AddFolder() { var folder = await Ui.PickFolder(this, "Choose a folder of game files to organize"); if (folder != null) AddPaths(new[] { folder }); }
        private async Task ChooseRoot()
        {
            var folder = await Ui.PickFolder(this, "Choose FishBowl's dedicated games folder", root.Text);
            if (folder != null) { library.GameLibraryRoot = folder; root.Text = GameStorage.Root(library); }
        }
        private void AddPaths(IEnumerable<string> paths)
        {
            foreach (var file in GameStorage.Expand(paths).Where(file => !IsLauncher(file)))
                if (!queued.Contains(file, Platform.PathComparer)) queued.Add(file);
            RefreshQueue();
        }
        // Game files on shared drives often carry execute bits on Linux, so only true launcher types are skipped.
        private static bool IsLauncher(string file)
        {
            if (Platform.IsWindows || !EmulatorReference.IsLaunchFile(file)) return EmulatorReference.IsLaunchFile(file);
            var extension = Path.GetExtension(file).ToLowerInvariant();
            return extension.Length == 0 || extension == ".appimage" || extension == ".sh" || extension == ".desktop";
        }
        private void RefreshQueue() { queue.ItemsSource = queued.Select(file => Path.GetFileName(file) + "  —  " + GameStorage.ConsoleFor(file)).ToList(); }
        private void OpenRoot() { Directory.CreateDirectory(GameStorage.Root(library)); Platform.Open(GameStorage.Root(library)); }
        private async Task SortFiles()
        {
            if (queued.Count == 0) return; int complete = 0; var errors = new List<string>(); string destinationRoot = GameStorage.Root(library);
            string forced = forcedConsole.SelectedIndex <= 0 ? null : forcedConsole.SelectedItem as string; bool copy = copyFiles.IsChecked == true;
            var files = queued.ToArray();
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try { GameStorage.MoveOrCopy(file, destinationRoot, forced ?? GameStorage.ConsoleFor(file), copy); complete++; }
                    catch (Exception error) { errors.Add(Path.GetFileName(file) + ": " + error.Message); }
                }
            });
            queued.Clear(); RefreshQueue();
            string message = "Organized " + complete + " file" + (complete == 1 ? "." : "s."); if (errors.Count > 0) message += "\n\nCould not organize:\n" + String.Join("\n", errors.Take(5));
            await Ui.Message(this, message, "FishBowl Game Storage");
        }
    }
}
