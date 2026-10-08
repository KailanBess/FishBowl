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
using Avalonia.Styling;

namespace EmulatorHub
{
    // Settings parity with the Windows build: More preferences, Live appearance and accessibility, Keyboard shortcuts,
    // multiplayer (session, readiness, privacy), controller profiles and center, and emulator profile files.
    public partial class MainWindow
    {
        // Called once from the constructor.
        private void ConfigureProfiles()
        {
            ConfigureBreakReminders();
            if (library.Enhancements != null && library.Enhancements.StrongFocus)
            {
                // Stronger keyboard focus outlines (NextSettings.StrongFocus).
                var style = new Style(x => x.Is<Control>().Class(":focus-visible"));
                style.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(3)));
                style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(p.Pink)));
                Application.Current.Styles.Add(style);
            }
        }

        // Menu entries for this area, added to the Tools, Emulators and View menus and the Multiplayer menu.
        private object[] ProfileToolsMenuItems()
        {
            return new object[] {
                new Separator(),
                MenuAction("User tools...", "settings", ShowUserTools),
                new MenuItem { Header = "Profile transfer", Icon = new FishIcon("export", 18), ItemsSource = new object[] {
                    MenuAction("Profile transfer and sync...", "export", () => ShowProfileTransfer(this)),
                    MenuAction("Export selected emulator profile...", "export", ExportSelectedEmulatorProfile),
                    MenuAction("Import emulator profile...", "import", ImportEmulatorProfile) } },
                new MenuItem { Header = "Controllers", Icon = new FishIcon("controller", 18), ItemsSource = new object[] {
                    MenuAction("Controller center...", "controller", ShowControllerCenter),
                    MenuAction("Controller profiles...", "controller", () => ShowControllerProfiles(this, CurrentEmulator())) } },
                MenuAction("More preferences...", "settings", ShowMorePreferences),
                MenuAction("Keyboard shortcuts...", "settings", ShowKeyboardShortcuts) };
        }

        private MenuItem MultiplayerMenu()
        {
            return new MenuItem { Header = "_Multiplayer", ItemsSource = new object[] {
                MenuAction("Start a session...", "controller", ShowMultiplayerHub),
                MenuAction("Multiplayer readiness check", "check", () => Ui.Message(this, MultiplayerSupport.Report(CurrentEmulator(), library.Multiplayer), "Multiplayer readiness")),
                MenuAction("Privacy and connection settings...", "settings", ShowMultiplayerSettings),
                MenuAction("Remote couch play...", "controller", ShowRemoteCouchPlay) } };
        }

        // ----- More preferences ---------------------------------------------------------------------------------------

        private async Task ShowMorePreferences()
        {
            var t = library.Theme;
            var dialog = new FishDialog("FishBowl More Preferences", 560); var body = new StackPanel { Spacing = 8 };
            body.Children.Add(Ui.Text("Optional behavior", 18, true));
            var openEmulator = Ui.Check("Confirm before opening an emulator", t.ConfirmBeforeEmulatorLaunch);
            var launchGame = Ui.Check("Confirm before launching a game", t.ConfirmBeforeGameLaunch);
            var hints = Ui.Check("Show command and keyboard hints", t.ShowCommandHints);
            body.Children.Add(openEmulator); body.Children.Add(launchGame); body.Children.Add(hints);
            body.Children.Add(Ui.Hint("These choices only change FishBowl. They never alter emulator settings, games, saves, or controller mappings."));
            body.Children.Add(dialog.Footer("Save", () =>
            {
                t.ConfirmBeforeEmulatorLaunch = openEmulator.IsChecked == true; t.ConfirmBeforeGameLaunch = launchGame.IsChecked == true; t.ShowCommandHints = hints.IsChecked == true;
                Store.Save(library); SetStatus("Preferences saved."); return Task.FromResult(true);
            }));
            dialog.Body = body; await dialog.Present(this);
        }

        // ----- Live appearance and accessibility ------------------------------------------------------------------------

        private async Task ShowLiveAppearance()
        {
            if (library.Enhancements == null) library.Enhancements = new NextSettings();
            var t = library.Theme; var n = library.Enhancements;
            var dialog = new FishDialog("Live appearance and accessibility", 640); var body = new StackPanel { Spacing = 6 };
            var theme = Ui.Combo(Palette.Themes, t.Name ?? "FishBowl Water");
            var accent = Ui.Combo(Palette.Accents, t.AccentColor ?? "Ocean");
            var font = Ui.Combo(Palette.FontChoices, String.IsNullOrWhiteSpace(t.FontFamily) ? "Bahnschrift" : t.FontFamily);
            var text = new NumericUpDown { Minimum = 75, Maximum = 200, Increment = 5, Value = n.TextPercent < 75 || n.TextPercent > 200 ? 100 : n.TextPercent, FormatString = "0", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            var density = Ui.Combo(new[] { "Compact", "Standard", "Roomy" }, t.ControlDensity ?? "Compact");
            var motion = Ui.Check("Reduce decorative motion", n.ReducedMotion || !t.EnableMotion);
            var focus = Ui.Check("Stronger keyboard focus outlines", n.StrongFocus);
            var preset = Ui.Combo(ProfileTools.AccessibilityPresets, "Current");
            var preview = new Border { Height = 70, CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = new TextBlock { Text = "FishBowl preview", FontSize = 18, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center } };
            Action update = () =>
            {
                var sample = Palette.From(new ThemeSettings { Name = theme.SelectedItem as string, AccentColor = accent.SelectedItem as string, RestrainedAccents = t.RestrainedAccents });
                preview.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(sample.Top, 0), new GradientStop(sample.Bottom, 1) } };
                preview.BorderBrush = new SolidColorBrush(sample.Blue); preview.BorderThickness = new Thickness(3);
                ((TextBlock)preview.Child).Foreground = new SolidColorBrush(sample.Ink);
                ((TextBlock)preview.Child).FontSize = 18 * (double)(text.Value ?? 100) / 100.0;
            };
            theme.SelectionChanged += delegate { update(); }; accent.SelectionChanged += delegate { update(); }; text.ValueChanged += delegate { update(); }; update();
            body.Children.Add(Ui.Caption("Theme")); body.Children.Add(theme);
            body.Children.Add(Ui.Caption("Accent")); body.Children.Add(accent);
            body.Children.Add(Ui.Caption("Font")); body.Children.Add(font);
            body.Children.Add(Ui.Caption("Text size (%) — independent of spacing")); body.Children.Add(text);
            body.Children.Add(Ui.Caption("Control spacing")); body.Children.Add(density);
            body.Children.Add(Ui.Caption("Reduced motion")); body.Children.Add(motion);
            body.Children.Add(Ui.Caption("Keyboard focus")); body.Children.Add(focus);
            body.Children.Add(Ui.Caption("Accessibility preset")); body.Children.Add(preset);
            body.Children.Add(Ui.Caption("Preview")); body.Children.Add(preview);
            body.Children.Add(Ui.Hint("Linux applies these when FishBowl restarts. Text size multiplies the interface scale chosen in FishBowl settings."));
            body.Children.Add(dialog.Footer("Save", () =>
            {
                t.Name = theme.SelectedItem as string; t.AccentColor = accent.SelectedItem as string; t.FontFamily = font.SelectedItem as string;
                n.TextPercent = (int)(text.Value ?? 100); t.ControlDensity = density.SelectedItem as string;
                n.ReducedMotion = motion.IsChecked == true; t.EnableMotion = !n.ReducedMotion; n.StrongFocus = focus.IsChecked == true;
                ProfileTools.ApplyAccessibility(library, preset.SelectedItem as string);
                Store.Save(library); return Task.FromResult(true);
            }));
            dialog.Body = new ScrollViewer { Content = body, MaxHeight = 760 }; await dialog.Present(this);
            if (dialog.Confirmed) await OfferRestart("Appearance saved.");
        }

        private async Task OfferRestart(string saved)
        {
            if (!await Ui.Confirm(this, saved + " FishBowl needs to restart before it can apply them.\n\nRestart FishBowl now?", "Restart FishBowl")) { SetStatus(saved + " Restart FishBowl to apply them."); return; }
            Store.Log("Restarting to apply appearance.");
            SaveProfileNotes(); SaveWindowLayout();
            string argument; using (System.Diagnostics.Process.Start(SelfStart(out argument))) { }
            Close();
        }

        // ----- Keyboard shortcuts ---------------------------------------------------------------------------------------

        private async Task ShowKeyboardShortcuts()
        {
            var dialog = new FishDialog("Keyboard shortcuts", 620); var body = new StackPanel { Spacing = 4 };
            var current = ShortcutText.Current(library); var fields = new Dictionary<string, TextBox>();
            foreach (var item in ModelDefaults.Shortcuts())
            {
                var field = Ui.Field(current[item.Key]); fields[item.Key] = field;
                body.Children.Add(Ui.Caption(item.Key)); body.Children.Add(field);
            }
            body.Children.Add(Ui.Hint("Use key names such as Control, P or F11. Escape, Enter and Control, F are reserved for navigation."));
            var restore = Ui.Action("Restore defaults", () => { foreach (var item in ModelDefaults.Shortcuts()) fields[item.Key].Text = item.Value; });
            body.Children.Add(dialog.Footer("Save", async () =>
            {
                try
                {
                    var saved = ShortcutText.Validate(fields.ToDictionary(f => f.Key, f => f.Value.Text));
                    if (library.Enhancements == null) library.Enhancements = new NextSettings();
                    library.Enhancements.Shortcuts = saved; Store.Save(library); SetStatus("Keyboard shortcuts saved."); return true;
                }
                catch (IOException error) { await Ui.Message(dialog, error.Message, "Keyboard shortcuts"); return false; }
            }, restore));
            dialog.Body = new ScrollViewer { Content = body, MaxHeight = 720 }; await dialog.Present(this);
        }

        // Runs the action bound to a key press in Keyboard shortcuts. Returns true when one ran.
        private async Task<bool> RunConfiguredShortcut(KeyEventArgs e)
        {
            var key = e.Key == Key.Return ? "Enter" : e.Key.ToString();
            var action = ShortcutText.Action(library, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt), key);
            if (action == null) return false;
            e.Handled = true;
            switch (action)
            {
                case "Search everything": libraryPages.SelectedIndex = LibraryPage; gameSearch.Focus(); gameSearch.SelectAll(); break;
                case "Home": libraryPages.SelectedIndex = HomePage; break;
                case "Library": libraryPages.SelectedIndex = LibraryPage; break;
                case "Emulators": libraryPages.SelectedIndex = EmulatorsPage; break;
                case "Add emulator": await Ui.Run(this, AddEmulator); break;
                case "Organize games": await Ui.Run(this, () => ShowGameStorageOrganizer(null)); break;
                case "Full screen": ToggleFullScreen(); break;
                case "Controller launcher": OpenLivingRoom(); break;
                case "Last game": await Ui.Run(this, ReopenLastGame); break;
                default: e.Handled = false; return false;
            }
            return true;
        }

        // ----- Multiplayer ------------------------------------------------------------------------------------------------

        private async Task ShowMultiplayerHub()
        {
            var settings = library.Multiplayer ?? MultiplayerSupport.Defaults();
            var emulators = library.Emulators.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (emulators.Count == 0) { await Ui.Message(this, "Add an emulator before starting a session."); return; }
            var dialog = new FishDialog("FishBowl Multiplayer", 560); var body = new StackPanel { Spacing = 6 };
            body.Children.Add(Ui.Text("Multiplayer session", 18, true));
            body.Children.Add(Ui.Hint("Choose an emulator and session type. FishBowl opens emulators unchanged."));
            var current = CurrentEmulator();
            var emulator = new ComboBox { ItemsSource = emulators.Select(x => x.Name).ToList(), SelectedIndex = Math.Max(0, current == null ? 0 : emulators.IndexOf(current)), HorizontalAlignment = HorizontalAlignment.Stretch };
            var mode = Ui.Combo(new[] { "Local play", "Native online", "Remote couch play" }, "Local play");
            var invite = Ui.Check("Keep remote sessions invite-only", settings.InviteOnly);
            body.Children.Add(Ui.Caption("Emulator")); body.Children.Add(emulator);
            body.Children.Add(Ui.Caption("Session type")); body.Children.Add(mode); body.Children.Add(invite);
            body.Children.Add(Ui.Hint("Native online uses the selected emulator's own feature. Remote couch play is prepared for LiveKit Cloud; FishBowl never shares an emulator's files or changes its settings."));
            body.Children.Add(dialog.Footer("Continue", () => Task.FromResult(emulator.SelectedIndex >= 0)));
            dialog.Body = body; await dialog.Present(this);
            if (!dialog.Confirmed) return;
            if (library.Multiplayer == null) library.Multiplayer = settings;
            library.Multiplayer.InviteOnly = invite.IsChecked == true; library.Multiplayer.RelayProvider = "LiveKit Cloud"; Store.Save(library);
            var chosen = emulators[emulator.SelectedIndex]; var kind = mode.SelectedItem as string;
            if (kind == "Remote couch play") { await ShowRemoteCouchPlay(); return; }
            var capability = MultiplayerSupport.For(chosen);
            await Ui.Message(this, kind == "Native online" ? chosen.Name + " will open normally. Start its own netplay or multiplayer feature inside the emulator.\n\n" + capability.NativeOnline : chosen.Name + " will open normally. Configure all local controllers inside the emulator.\n\n" + capability.LocalPlay, kind == "Native online" ? "Native online session" : "Local session");
            selectedEmulatorId = chosen.Id; RefreshHub();
            await OpenSelectedEmulator();
        }

        private async Task ShowMultiplayerSettings()
        {
            var settings = library.Multiplayer ?? MultiplayerSupport.Defaults();
            var dialog = new FishDialog("Multiplayer Privacy and Connection", 600); var body = new StackPanel { Spacing = 6 };
            body.Children.Add(Ui.Text("Privacy and connection", 18, true));
            body.Children.Add(Ui.Hint("Remote couch play uses LiveKit Cloud. Enter only your token endpoint. Keep the LiveKit API secret on that server, where it can issue short-lived session credentials."));
            var relay = Ui.Field(settings.RelayGatewayUrl ?? "");
            var invite = Ui.Check("Keep remote sessions invite-only", settings.InviteOnly);
            var remember = Ui.Check("Remember recent session names on this device", settings.RememberRecentSessions);
            var diagnostics = Ui.Check("Share basic diagnostics with the session host", settings.ShareDiagnosticsWithHost);
            var updates = Ui.Check("Check FishBowl's public release page for updates", settings.CheckForHubUpdates);
            var channel = Ui.Combo(new[] { "Stable", "Beta" }, String.Equals(settings.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable");
            body.Children.Add(Ui.Caption("LiveKit token endpoint (optional)")); body.Children.Add(relay);
            body.Children.Add(invite); body.Children.Add(remember); body.Children.Add(diagnostics); body.Children.Add(updates);
            body.Children.Add(Ui.Caption("Release channel")); body.Children.Add(channel);
            body.Children.Add(Ui.Hint("Checking only opens release information. FishBowl will never replace its own files without a separately reviewed installer or package update."));
            body.Children.Add(dialog.Footer("Save", async () =>
            {
                string address;
                try { address = MultiplayerSupport.CheckRelay(relay.Text); }
                catch (IOException error) { await Ui.Message(dialog, error.Message); return false; }
                if (library.Multiplayer == null) library.Multiplayer = settings;
                var m = library.Multiplayer;
                m.RelayGatewayUrl = address; m.InviteOnly = invite.IsChecked == true; m.RememberRecentSessions = remember.IsChecked == true; m.ShareDiagnosticsWithHost = diagnostics.IsChecked == true;
                m.CheckForHubUpdates = updates.IsChecked == true; m.UpdateChannel = channel.SelectedItem as string; m.RelayProvider = "LiveKit Cloud";
                Store.Save(library); SetStatus("Multiplayer privacy and update preferences saved."); return true;
            }));
            dialog.Body = body; await dialog.Present(this);
        }

        // ----- Controllers ------------------------------------------------------------------------------------------------

        private async Task ShowControllerProfiles(Window owner, EmulatorProfile emulator)
        {
            var dialog = new FishDialog("FishBowl Controller Profiles", 760, 520);
            var list = new ListBox { Width = 230, Background = Ui.P.SurfaceBrush, Foreground = Ui.P.InkBrush };
            var name = Ui.Field(); var notes = Ui.Paragraphs("", false); notes.MinHeight = 220; notes.Background = Ui.P.SurfaceBrush;
            List<ControllerProfile> items = null; bool loading = false;
            Action save = () => { var profile = list.SelectedIndex >= 0 && list.SelectedIndex < items.Count ? items[list.SelectedIndex] : null; if (profile != null && !loading) ProfileTools.UpdateControllerProfile(profile, name.Text, notes.Text); };
            Action<ControllerProfile> refresh = select =>
            {
                loading = true; items = ProfileTools.ControllerProfiles(library, emulator); list.ItemsSource = items.Select(x => x.Name).ToList(); loading = false;
                list.SelectedIndex = items.Count == 0 ? -1 : Math.Max(0, select == null ? 0 : items.IndexOf(select));
            };
            list.SelectionChanged += delegate { if (loading) return; var profile = list.SelectedIndex >= 0 ? items[list.SelectedIndex] : null; loading = true; name.Text = profile == null ? "" : profile.Name; notes.Text = profile == null ? "" : profile.Notes; loading = false; };
            var right = new StackPanel { Spacing = 4, Margin = new Thickness(14, 0, 0, 0) };
            right.Children.Add(Ui.Caption("Profile name")); right.Children.Add(name);
            right.Children.Add(Ui.Caption("Setup notes")); right.Children.Add(notes);
            right.Children.Add(Ui.Hint(emulator == null ? "Select an emulator to add a profile." : "Profiles are notes only; FishBowl does not overwrite " + emulator.Name + " mappings. Assign one to a game in Game setup to see it as a reminder before the game starts."));
            var row = Ui.Actions(
                Ui.Action("New", () => { save(); refresh(ProfileTools.AddControllerProfile(library, emulator)); }),
                Ui.Action("Remove", () => { if (list.SelectedIndex < 0) return; ProfileTools.RemoveControllerProfile(library, items[list.SelectedIndex]); refresh(null); }),
                Ui.Action("Save", () => { save(); Store.Save(library); dialog.Close(); SetStatus("Controller profiles saved. They do not modify emulator mappings."); }, true),
                Ui.Action("Close", () => dialog.Close()));
            var columns = new DockPanel(); DockPanel.SetDock(list, Dock.Left); columns.Children.Add(list); columns.Children.Add(right);
            var layout = new DockPanel(); DockPanel.SetDock(row, Dock.Bottom); layout.Children.Add(row); layout.Children.Add(columns);
            refresh(null); dialog.Body = layout; await dialog.Present(owner);
        }

        private async Task ShowControllerCenter()
        {
            var emulator = CurrentEmulator();
            var dialog = new FishDialog("FishBowl Controller Center", 600); var body = new StackPanel { Spacing = 8 };
            body.Children.Add(Ui.Text("Controller center", 18, true));
            body.Children.Add(Ui.Hint((emulator == null ? "No emulator is selected." : "Selected emulator: " + emulator.Name + ".") + "\n\nFishBowl stores controller profiles as setup notes, so it never overwrites an emulator's controller mappings. Open the emulator itself to map buttons, adjust dead zones, or choose an input device."));
            body.Children.Add(Ui.Hint("Detected for navigation: " + controllerDevice.Status));
            body.Children.Add(Ui.Actions(Ui.Action("Controller profiles", () => ShowControllerProfiles(dialog, emulator), true), Ui.Action("Controller navigation...", ShowAppearanceHub)));
            body.Children.Add(Ui.Hint("Linux reads controllers through the kernel's input devices (/dev/input). If a controller is missing, check that it is connected and that your account can read its event device."));
            var done = Ui.Action("Done", () => dialog.Close()); done.HorizontalAlignment = HorizontalAlignment.Right; body.Children.Add(done);
            dialog.Body = body; await dialog.Present(this);
        }

        // ----- Emulator profile files -----------------------------------------------------------------------------------

        private async Task ExportSelectedEmulatorProfile()
        {
            var emulator = CurrentEmulator(); if (emulator == null) { await Ui.Message(this, "Select an emulator first."); return; }
            var target = await Ui.SaveFile(this, "Export emulator profile", "FishBowl emulator profile|*.fishbowl-emulator.json", HubPaths.SafeName(emulator.Name) + ".fishbowl-emulator.json"); if (target == null) return;
            File.WriteAllText(target, Json.Serialize(emulator)); SetStatus("Exported " + emulator.Name + " profile.");
        }

        private async Task ImportEmulatorProfile()
        {
            var file = await Ui.PickFile(this, "Import emulator profile", "FishBowl emulator profile|*.fishbowl-emulator.json;*.json"); if (file == null) return;
            EmulatorProfile emulator;
            try { emulator = Json.Deserialize<EmulatorProfile>(File.ReadAllText(file)); }
            catch (Exception error) { await Ui.Message(this, "FishBowl could not read that emulator profile.\n\n" + error.Message); return; }
            if (emulator == null || String.IsNullOrWhiteSpace(emulator.Name)) { await Ui.Message(this, "That file does not contain a valid FishBowl emulator profile."); return; }
            emulator.Id = Guid.NewGuid().ToString("N");
            if (emulator.LaunchProfiles == null) emulator.LaunchProfiles = new List<LaunchProfile>();
            if (emulator.Builds == null) emulator.Builds = new List<EmulatorBuild>();
            if (emulator.Extensions == null) emulator.Extensions = new List<string>();
            library.Emulators.Add(emulator); selectedEmulatorId = emulator.Id;
            Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers();
            SetStatus("Imported " + emulator.Name + ". Review its program location before opening it.");
        }
    }
}
