using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmulatorHub
{
    // Launch profiles for one emulator (the Windows LaunchProfilesDialog), plus the Linux launch options each profile
    // or the emulator itself can use: Proton or Wine for Windows programs, GameMode, MangoHud, Gamescope, environment.
    public class LaunchProfilesDialog : FishDialog
    {
        private sealed class Row
        {
            public LaunchProfile Original; public string Name, Arguments; public LinuxLaunchOptions Options; public bool OwnOptions;
            public override string ToString() { return Original == null && Name == null ? "Emulator default" : Name; }
        }

        private readonly LibraryData library;
        private readonly EmulatorProfile emulator;
        private readonly List<Row> rows = new List<Row>();
        private readonly ListBox list = new ListBox();
        private readonly TextBox name = Ui.Field(), arguments = Ui.Field();
        private readonly CheckBox own = Ui.Check("Use its own Linux launch options (otherwise the emulator default)", false);
        private readonly LaunchOptionsEditor editor;
        private readonly TextBox checks = Ui.Paragraphs("");
        private readonly StackPanel profileFields = new StackPanel { Spacing = 4 };
        private bool loading;
        public LaunchProfile OpenSelectedProfile { get; private set; }
        public bool OpenDefault { get; private set; }

        public LaunchProfilesDialog(LibraryData library, EmulatorProfile emulator) : base("Launch profiles — " + emulator.Name, 980, 700)
        {
            this.library = library; this.emulator = emulator;
            if (emulator.LaunchProfiles == null) emulator.LaunchProfiles = new List<LaunchProfile>();
            rows.Add(new Row { Options = LinuxLaunch.For(emulator).Copy(), OwnOptions = true });
            foreach (var profile in emulator.LaunchProfiles)
            {
                var options = LinuxLaunch.For(profile);
                rows.Add(new Row { Original = profile, Name = profile.Name, Arguments = profile.Arguments, Options = (options ?? LinuxLaunch.For(emulator)).Copy(), OwnOptions = options != null });
            }
            editor = new LaunchOptionsEditor(emulator, () => Preview());
            list.Background = Ui.P.SurfaceBrush; list.Foreground = Ui.P.InkBrush; list.Width = 230;
            list.SelectionChanged += delegate { LoadSelected(); };
            name.TextChanged += delegate { StoreSelected(); list.InvalidateMeasure(); };
            arguments.TextChanged += delegate { StoreSelected(); Preview(); };
            own.IsCheckedChanged += delegate { StoreSelected(); editor.IsEnabled = own.IsChecked == true; Preview(); };

            var left = new DockPanel { Margin = new Thickness(0, 0, 14, 0) };
            var leftButtons = Ui.Actions(Ui.Action("New", () => Add()), Ui.Action("Remove", () => Remove()));
            DockPanel.SetDock(leftButtons, Dock.Bottom); left.Children.Add(leftButtons); left.Children.Add(list);

            var right = new StackPanel { Spacing = 4 };
            profileFields.Children.Add(Ui.Caption("Profile name")); profileFields.Children.Add(name);
            profileFields.Children.Add(Ui.Caption("Command-line arguments")); profileFields.Children.Add(arguments);
            profileFields.Children.Add(Ui.Hint("A launch profile adds only the arguments you save here when a game uses it or you choose Open profile. It does not rewrite the emulator's settings or its usual launch behavior."));
            profileFields.Children.Add(own);
            right.Children.Add(profileFields);
            right.Children.Add(Ui.Text("Linux launch options", 15, true));
            right.Children.Add(editor);
            right.Children.Add(Ui.Caption("Checks and command"));
            checks.MinHeight = 120; right.Children.Add(checks);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Spacing = 8 };
            footer.Children.Add(Ui.Action("Open profile", () => { if (!Save()) return; var row = list.SelectedItem as Row; OpenDefault = row == null || row.Original == null && row.Name == null; OpenSelectedProfile = OpenDefault ? null : emulator.LaunchProfiles.FirstOrDefault(p => p.Name == row.Name); Confirmed = true; Close(); }));
            var save = Ui.Action("Save", () => { if (Save()) { Confirmed = true; Close(); } }, true); save.IsDefault = true; footer.Children.Add(save);
            var cancel = Ui.Action("Close", () => Close()); cancel.IsCancel = true; footer.Children.Add(cancel);

            var columns = new DockPanel(); DockPanel.SetDock(left, Dock.Left); columns.Children.Add(left); columns.Children.Add(new ScrollViewer { Content = right });
            var layout = new DockPanel(); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer); layout.Children.Add(columns);
            Body = layout;
            Refresh(0);
        }

        private void Refresh(int index)
        {
            loading = true; list.ItemsSource = null; list.ItemsSource = rows.ToList(); loading = false;
            list.SelectedIndex = Math.Max(0, Math.Min(index, rows.Count - 1));
        }

        private void LoadSelected()
        {
            if (loading) return;
            var row = list.SelectedItem as Row; if (row == null) return;
            loading = true;
            bool isDefault = row.Original == null && row.Name == null;
            profileFields.IsVisible = !isDefault;
            name.Text = row.Name ?? ""; arguments.Text = row.Arguments ?? "";
            own.IsChecked = row.OwnOptions; editor.IsEnabled = row.OwnOptions;
            editor.Load(row.OwnOptions ? row.Options : rows[0].Options);
            loading = false;
            Preview();
        }

        private void StoreSelected()
        {
            if (loading) return;
            var row = list.SelectedItem as Row; if (row == null) return;
            if (!(row.Original == null && row.Name == null)) { row.Name = name.Text; row.Arguments = arguments.Text; row.OwnOptions = own.IsChecked == true; }
            if (row.OwnOptions) row.Options = editor.Read();
        }

        private void Preview()
        {
            if (loading) return;
            StoreSelected();
            var row = list.SelectedItem as Row; if (row == null) return;
            var options = row.OwnOptions ? row.Options : rows[0].Options;
            var probe = Json.Deserialize<EmulatorProfile>(Json.Serialize(emulator));
            var lines = LinuxLaunch.Checks(probe, options, LaunchHost.Current());
            if (!String.IsNullOrWhiteSpace(row.Arguments)) lines.Add("Profile arguments: " + row.Arguments.Trim());
            checks.Text = String.Join("\n", lines);
        }

        private void Add()
        {
            StoreSelected();
            var names = rows.Where(r => r.Name != null).Select(r => r.Name).ToList(); string label = "New profile";
            for (int i = 2; names.Any(n => String.Equals(n, label, StringComparison.OrdinalIgnoreCase)); i++) label = "New profile " + i;
            rows.Add(new Row { Name = label, Arguments = "", Options = rows[0].Options.Copy(), OwnOptions = false });
            Refresh(rows.Count - 1);
        }

        private void Remove()
        {
            var row = list.SelectedItem as Row; if (row == null || row.Original == null && row.Name == null) return;
            int index = rows.IndexOf(row); rows.Remove(row); Refresh(index - 1);
        }

        // Applies the edited rows to the emulator; games follow renamed profiles and fall back to Default for removed ones.
        private bool Save()
        {
            StoreSelected();
            var names = rows.Skip(1).Select(r => String.IsNullOrWhiteSpace(r.Name) ? "Launch profile" : r.Name.Trim()).ToList();
            var duplicate = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) { Ui.Post(async () => await Ui.Message(this, "Two launch profiles are called " + duplicate.Key + ". Use a different name for each.")); return false; }
            var kept = rows.Skip(1).Where(r => r.Original != null).Select(r => r.Original).ToList();
            foreach (var removed in emulator.LaunchProfiles.Where(p => !kept.Contains(p)).ToList()) ProfileTools.RemoveLaunchProfile(library, emulator, removed);
            // Rename in two steps so swapped names cannot collide.
            foreach (var row in rows.Skip(1).Where(r => r.Original != null)) ProfileTools.UpdateLaunchProfile(library, emulator, row.Original, row.Original.Name + "\u0001rename", row.Arguments);
            var ordered = new List<LaunchProfile>();
            foreach (var row in rows.Skip(1))
            {
                var profile = row.Original ?? new LaunchProfile();
                if (row.Original == null) { emulator.LaunchProfiles.Add(profile); profile.Name = "\u0001new"; }
                ProfileTools.UpdateLaunchProfile(library, emulator, profile, row.Name, row.Arguments);
                LinuxLaunch.Save(profile, row.OwnOptions ? row.Options : null);
                ordered.Add(profile);
            }
            emulator.LaunchProfiles = ordered;
            LinuxLaunch.Save(emulator, rows[0].Options);
            return true;
        }
    }

    // The Linux launch option fields, shared by the launch profiles dialog.
    public class LaunchOptionsEditor : StackPanel
    {
        private readonly ComboBox runner = Ui.Combo(new[] { "Automatic", LinuxLaunch.ProtonRunner, LinuxLaunch.WineRunner }, "Automatic");
        private readonly ComboBox proton;
        private readonly TextBox prefix = Ui.Field(), wine = Ui.Field(), gameId = Ui.Field(), store = Ui.Field(), width = Ui.Field(), height = Ui.Field(), fps = Ui.Field(), scopeArgs = Ui.Field();
        private readonly CheckBox gameMode = Ui.Check("GameMode (gamemoderun): asks the system for performance settings while playing", false);
        private readonly CheckBox mangoHud = Ui.Check("MangoHud: frame rate and performance overlay", false);
        private readonly CheckBox gamescope = Ui.Check("Gamescope: run inside its own compositor window", false);
        private readonly CheckBox fullscreen = Ui.Check("Gamescope full screen", false);
        private readonly TextBox environment = Ui.Paragraphs("", false);
        private readonly StackPanel windowsPanel = new StackPanel { Spacing = 4 }, scopePanel = new StackPanel { Spacing = 4 };
        private readonly Action changed;
        private bool loading;

        public LaunchOptionsEditor(EmulatorProfile emulator, Action changed)
        {
            this.changed = changed; Spacing = 4;
            var builds = ProtonBuilds.Find(Platform.Home, Platform.DataHome);
            proton = new ComboBox { ItemsSource = new[] { "UMU-Proton (default)", LinuxLaunch.GeProton }.Concat(builds.Select(b => b.Path)).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            bool exe = LinuxLaunch.IsWindowsProgram(emulator.Executable);
            windowsPanel.Children.Add(Ui.Hint(exe ? emulator.Name + " is a Windows program, so it runs through Proton (umu-launcher) or Wine." : "These apply only when the emulator is a Windows program (.exe)."));
            windowsPanel.Children.Add(Ui.Caption("Runner")); windowsPanel.Children.Add(runner);
            windowsPanel.Children.Add(Ui.Caption("Proton build (umu-launcher)")); windowsPanel.Children.Add(proton);
            windowsPanel.Children.Add(Ui.Caption("Wine prefix (blank: FishBowl's own per emulator)")); windowsPanel.Children.Add(Ui.WithButton(prefix, Ui.Action("Browse", async () => { var owner = TopLevel.GetTopLevel(this) as Window; var folder = await Ui.PickFolder(owner, "Wine prefix"); if (folder != null) prefix.Text = folder; })));
            windowsPanel.Children.Add(Ui.Caption("Wine program (blank: wine from PATH)")); windowsPanel.Children.Add(wine);
            windowsPanel.Children.Add(Ui.Caption("umu GAMEID and STORE (blank: umu-default)"));
            var umuRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") }; Grid.SetColumn(store, 2); umuRow.Children.Add(gameId); umuRow.Children.Add(store); windowsPanel.Children.Add(umuRow);
            gameId.Watermark = "GAMEID, e.g. umu-default"; store.Watermark = "STORE, e.g. gog";
            windowsPanel.IsVisible = exe;
            Children.Add(windowsPanel);
            Children.Add(gameMode); Children.Add(mangoHud); Children.Add(gamescope);
            width.Watermark = "Width"; height.Watermark = "Height"; fps.Watermark = "Frame limit"; scopeArgs.Watermark = "Other gamescope options";
            var size = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,*") }; Grid.SetColumn(height, 2); Grid.SetColumn(fps, 4); size.Children.Add(width); size.Children.Add(height); size.Children.Add(fps);
            scopePanel.Children.Add(size); scopePanel.Children.Add(fullscreen); scopePanel.Children.Add(scopeArgs); scopePanel.Margin = new Thickness(24, 0, 0, 0);
            Children.Add(scopePanel);
            Children.Add(Ui.Caption("Environment variables (KEY=VALUE, one per line)"));
            environment.MinHeight = 70; environment.Background = Ui.P.SurfaceBrush; Children.Add(environment);
            foreach (var box in new[] { prefix, wine, gameId, store, width, height, fps, scopeArgs, environment }) box.TextChanged += delegate { Changed(); };
            foreach (var box in new[] { gameMode, mangoHud, gamescope, fullscreen }) box.IsCheckedChanged += delegate { Changed(); };
            runner.SelectionChanged += delegate { Changed(); }; proton.SelectionChanged += delegate { Changed(); };
        }

        private void Changed() { scopePanel.IsVisible = gamescope.IsChecked == true; if (!loading) changed(); }

        public void Load(LinuxLaunchOptions o)
        {
            loading = true;
            runner.SelectedItem = o.Runner == LinuxLaunch.ProtonRunner || o.Runner == LinuxLaunch.WineRunner ? o.Runner : "Automatic";
            var items = proton.ItemsSource.Cast<string>().ToList();
            if (!String.IsNullOrWhiteSpace(o.Proton) && !items.Contains(o.Proton)) { items.Add(o.Proton); proton.ItemsSource = items; }
            proton.SelectedItem = String.IsNullOrWhiteSpace(o.Proton) ? items[0] : o.Proton;
            prefix.Text = o.WinePrefix ?? ""; wine.Text = o.WineProgram ?? ""; gameId.Text = o.GameId ?? ""; store.Text = o.UmuStore ?? "";
            gameMode.IsChecked = o.GameMode; mangoHud.IsChecked = o.MangoHud; gamescope.IsChecked = o.Gamescope; fullscreen.IsChecked = o.GamescopeFullscreen;
            width.Text = o.GamescopeWidth > 0 ? o.GamescopeWidth.ToString() : ""; height.Text = o.GamescopeHeight > 0 ? o.GamescopeHeight.ToString() : ""; fps.Text = o.GamescopeFrameLimit > 0 ? o.GamescopeFrameLimit.ToString() : "";
            scopeArgs.Text = o.GamescopeArguments ?? ""; environment.Text = o.EnvironmentVariables ?? "";
            scopePanel.IsVisible = o.Gamescope;
            loading = false;
        }

        public LinuxLaunchOptions Read()
        {
            Func<TextBox, string> text = box => String.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();
            Func<TextBox, int> number = box => { int value; return Int32.TryParse((box.Text ?? "").Trim(), out value) && value > 0 && value <= 100000 ? value : 0; };
            var chosenRunner = runner.SelectedItem as string;
            return new LinuxLaunchOptions
            {
                Runner = chosenRunner == "Automatic" ? null : chosenRunner,
                Proton = proton.SelectedIndex <= 0 ? null : proton.SelectedItem as string,
                WinePrefix = text(prefix), WineProgram = text(wine), GameId = text(gameId), UmuStore = text(store),
                GameMode = gameMode.IsChecked == true, MangoHud = mangoHud.IsChecked == true, Gamescope = gamescope.IsChecked == true,
                GamescopeWidth = number(width), GamescopeHeight = number(height), GamescopeFrameLimit = number(fps), GamescopeFullscreen = fullscreen.IsChecked == true,
                GamescopeArguments = text(scopeArgs), EnvironmentVariables = String.IsNullOrWhiteSpace(environment.Text) ? null : environment.Text
            };
        }
    }

    public partial class MainWindow
    {
        private Task ShowLaunchProfiles() { var emulator = CurrentEmulator(); return emulator == null ? Ui.Message(this, "Select an emulator first.") : ShowLaunchProfiles(this, emulator); }

        private async Task ShowLaunchProfiles(Window owner, EmulatorProfile emulator)
        {
            var dialog = new LaunchProfilesDialog(library, emulator);
            await dialog.Present(owner);
            if (!dialog.Confirmed) return;
            Store.Save(library); RefreshGameLibrary();
            SetStatus(emulator.Name + " launch profiles saved.");
            if (dialog.OpenSelectedProfile != null || dialog.OpenDefault) await OpenEmulatorWith(emulator, dialog.OpenSelectedProfile);
        }

        // Opens an emulator by itself through its launch options (and a launch profile's arguments when given).
        private async Task OpenEmulatorWith(EmulatorProfile emulator, LaunchProfile profile)
        {
            var plan = LinuxLaunch.ForEmulator(emulator, profile, LaunchHost.Current());
            using (Process.Start(LinuxLaunch.StartInfo(plan))) { }
            Store.Log("Opened " + emulator.Name + (profile == null ? "" : " - " + profile.Name) + ": " + LinuxLaunch.CommandLine(plan));
            Platform.ProcessesChanged();
            await Task.Delay(400); RefreshRuntimeStatus();
            SetStatus("Opened " + emulator.Name + (profile == null ? "" : " - " + profile.Name) + "." + (plan.Notes.Count == 0 ? "" : " " + String.Join(" ", plan.Notes)));
        }
    }
}
