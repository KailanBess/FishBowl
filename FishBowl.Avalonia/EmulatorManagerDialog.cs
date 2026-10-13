using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmulatorHub
{
    // A simple multi-column list: header row plus selectable rows, optionally with check boxes.
    public class Table : DockPanel
    {
        private readonly string columns; private readonly bool checks;
        public readonly ListBox List = new ListBox();
        public Table(string columnDefinitions, bool checkBoxes, params string[] headers)
        {
            columns = columnDefinitions; checks = checkBoxes;
            var header = Row(headers.Select(h => (Control)new TextBlock { Text = h, Foreground = Ui.P.SubtleBrush }).ToArray());
            header.Margin = new Thickness(checks ? 36 : 12, 6, 12, 6);
            DockPanel.SetDock(header, Dock.Top); Children.Add(header);
            List.Background = Ui.P.SurfaceBrush; List.MinHeight = 120; Children.Add(List);
        }
        private Grid Row(Control[] cells)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
            for (int i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); cells[i].Margin = new Thickness(0, 0, 10, 0); grid.Children.Add(cells[i]); }
            return grid;
        }
        public void Clear() { List.Items.Clear(); }
        public ListBoxItem Add(object tag, bool isChecked, params string[] values)
        {
            var cells = values.Select(v => (Control)new TextBlock { Text = v, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Ui.P.InkBrush }).ToArray();
            for (int i = 0; i < values.Length; i++) ToolTip.SetTip(cells[i], values[i]);
            Control content = Row(cells);
            if (checks) { var box = new CheckBox { IsChecked = isChecked, Margin = new Thickness(0, 0, 8, 0) }; content = new DockPanel { Children = { box, content } }; DockPanel.SetDock(box, Dock.Left); }
            var item = new ListBoxItem { Content = content, Tag = tag };
            List.Items.Add(item); return item;
        }
        public object SelectedTag { get { var item = List.SelectedItem as ListBoxItem; return item == null ? null : item.Tag; } }
        public IEnumerable<ListBoxItem> CheckedItems
        {
            get { return List.Items.OfType<ListBoxItem>().Where(i => { var panel = i.Content as DockPanel; var box = panel == null ? null : panel.Children.OfType<CheckBox>().FirstOrDefault(); return box != null && box.IsChecked == true; }).ToList(); }
        }
        public static void Uncheck(ListBoxItem item) { var panel = item.Content as DockPanel; if (panel != null) foreach (var box in panel.Children.OfType<CheckBox>()) box.IsChecked = false; }
    }

    public partial class EmulatorManagerDialog : FishDialog
    {
        private readonly LibraryData library;
        private EmulatorProfile profile;
        private readonly ComboBox profiles = new ComboBox { Width = 440 };
        private readonly TabControl tabs = new TabControl();
        private readonly TextBlock progress;
        private readonly TextBox rootBox = Ui.Field("", true), backupRootBox = Ui.Field("", true), setupName = Ui.Field(), setupProgram = Ui.Field(), packageName = Ui.Field(), downloadUrl = Ui.Field(), repository = Ui.Field();
        private ComboBox setupPreset;
        private readonly Table discovered = new Table("2*,2*,5*", true, "Emulator", "Preset", "Program"), builds = new Table("2*,*,5*", false, "Build", "Status", "Program"), health = new Table("2*,*,5*", false, "Check", "Status", "Details");
        private readonly CheckBox unknownPrograms = Ui.Check(Platform.IsWindows ? "Also show unrecognized .exe programs" : "Also show unrecognized programs in the emulator folder", false), previews = Ui.Check("Include preview/nightly published releases", false);
        private readonly TextBox updateText = Ui.Paragraphs(), backupText = Ui.Paragraphs(), requirementText = Ui.Paragraphs();
        private readonly Dictionary<string, CheckBox> categories = new Dictionary<string, CheckBox>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool busy, loading;
        private BackupPlan backupPlan;
        private string planProfileId, releaseUrl;
        public EmulatorProfile SelectedProfile { get { return profile; } }

        public EmulatorManagerDialog(LibraryData data, EmulatorProfile selected, string page) : base("FishBowl — Emulator management", 980, 740)
        {
            library = data; profile = selected; MinWidth = 860; MinHeight = 650;
            foreach (var box in new[] { updateText, backupText, requirementText }) box.Background = Ui.P.SurfaceBrush;
            progress = Ui.Text("Emulator tools only. Games stay inside the dedicated emulator.", 12.5, false, Ui.P.SubtleBrush); progress.Margin = new Thickness(0, 10, 0, 0);
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 10) };
            top.Children.Add(new TextBlock { Text = "Selected emulator", VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(profiles); top.Children.Add(Button("Repair location", RepairLocation));
            BuildSetup(); BuildDiscovery(); BuildUpdates(); BuildVersions(); BuildBackups(); BuildHealth();
            profiles.SelectionChanged += delegate { if (loading) return; int i = profiles.SelectedIndex - 1; profile = i >= 0 && i < library.Emulators.Count ? library.Emulators[i] : null; RefreshSelected(); };
            var layout = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(progress, Dock.Bottom);
            layout.Children.Add(top); layout.Children.Add(progress); layout.Children.Add(tabs);
            Body = layout;
            RefreshProfiles(selected);
            var selectedPage = tabs.Items.OfType<TabItem>().FirstOrDefault(t => (t.Tag as string) == page); if (selectedPage != null) tabs.SelectedItem = selectedPage;
            Closing += delegate { cancellation.Cancel(); };
        }

        private Button Button(string label, Func<Task> action)
        {
            bool primary = label == "Add to FishBowl" || label == "Create backup" || label == "Use selected build";
            var button = Ui.Action(label, (Func<Task>)null, primary);
            button.Click += async delegate { try { await action(); } catch (Exception ex) { await Fail(ex); } };
            return button;
        }
        private Button Button(string label, Action action) { return Button(label, () => { action(); return Task.CompletedTask; }); }
        private async Task Fail(Exception ex)
        {
            Store.Log("Emulator management: " + ex); progress.Text = "Action could not finish.";
            await Ui.Message(this, ex.Message);
        }
        private async Task RunAsync(Func<Task> action)
        {
            if (busy) return; busy = true; tabs.IsEnabled = false; profiles.IsEnabled = false;
            try { await action(); }
            catch (OperationCanceledException) { progress.Text = "Cancelled."; }
            catch (Exception ex) { await Fail(ex); }
            finally { busy = false; tabs.IsEnabled = true; profiles.IsEnabled = true; }
        }
        private StackPanel Page(string name, string icon, bool fillLast = false)
        {
            var panel = new StackPanel { Margin = new Thickness(4, 8, 14, 8) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FishIcon(icon, 18, Ui.P.Ink, Ui.P.Blue), new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } };
            tabs.Items.Add(new TabItem { Header = header, Tag = name, Content = new ScrollViewer { Content = panel } });
            return panel;
        }
        // Pages whose last element is a list/text area that should fill the remaining height.
        private DockPanel FillPage(string name, string icon, Control fill, params Control[] above)
        {
            var dock = new DockPanel { Margin = new Thickness(4, 8, 4, 8) };
            var stack = new StackPanel(); foreach (var item in above) stack.Children.Add(item);
            DockPanel.SetDock(stack, Dock.Top); dock.Children.Add(stack); dock.Children.Add(fill);
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FishIcon(icon, 18, Ui.P.Ink, Ui.P.Blue), new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } };
            tabs.Items.Add(new TabItem { Header = header, Tag = name, Content = dock });
            return dock;
        }
        private static Control Input(string label, TextBox field, Button button = null)
        { return new StackPanel { Children = { Ui.Caption(label), button == null ? (Control)field : Ui.WithButton(field, button) } }; }
        private EmulatorProfile Selected() { if (profile == null) throw new InvalidOperationException("Choose an emulator at the top first."); return profile; }
        private static void RequireStopped(EmulatorProfile p)
        { if (EmulatorRuntime.State(p.Executable) == RuntimeState.Running) throw new IOException("Close " + p.Name + " before changing versions or backing up/restoring its files."); }

        private void RefreshProfiles(EmulatorProfile selected)
        {
            loading = true;
            profiles.ItemsSource = new[] { "Choose an emulator…" }.Concat(library.Emulators.Select(e => e.Name)).ToList();
            profiles.SelectedIndex = selected != null && library.Emulators.Contains(selected) ? library.Emulators.IndexOf(selected) + 1 : 0;
            loading = false; profile = selected; RefreshSelected();
        }
        private void RefreshSelected()
        {
            backupPlan = null; backupText.Text = "Choose backup categories and preview the included folders/files.";
            loading = true; repository.Text = profile == null ? "" : (profile.GitHubRepository ?? ""); previews.IsChecked = profile != null && profile.IncludePreviewReleases; loading = false;
            RefreshUpdates(); RefreshBuilds(); RefreshHealth();
        }

        // ----- Setup assistant ---------------------------------------------------------------------------------------

        private void BuildSetup()
        {
            var page = Page("Setup assistant", "controller");
            page.Children.Add(Ui.Hint("Choose where emulator packages live. Download from the project's official page, import a " + (Platform.IsWindows ? "ZIP" : "ZIP or AppImage") + " or browse an existing installation, then add its main program." + (Platform.IsWindows ? " Installer-based emulators can stay in their normal location." : " Flatpak and distribution packages stay where they are; Find installed detects them.")));
            rootBox.Text = HubPaths.EmulatorRoot(library);
            page.Children.Add(Input("Dedicated emulator folder", rootBox, Button("Choose…", ChooseRoot)));
            var labels = EmulatorCatalog.Presets.OrderBy(x => x.Name == "Custom" ? "" : x.Name).Select(x => x.Label).ToList();
            setupPreset = Ui.Combo(labels, labels[0]); setupPreset.Margin = new Thickness(0, 10, 0, 0); setupPreset.MaxDropDownHeight = 460;
            setupPreset.SelectionChanged += delegate { var preset = EmulatorCatalog.Find(setupPreset.SelectedItem as string); downloadUrl.Text = preset.Website; if (preset.Name != "Custom") { setupName.Text = preset.Name; packageName.Text = preset.Name; } };
            page.Children.Add(setupPreset);
            page.Children.Add(Input("Official download page (custom emulators can supply their own)", downloadUrl, Button("Open page", () => OpenUrl((downloadUrl.Text ?? "").Trim()))));
            page.Children.Add(Input("Folder / build name for a package import", packageName));
            var imports = new List<Control> { Button("Create / open emulator folder", OpenRoot), Button("Import emulator ZIP…", ImportZip) };
            if (!Platform.IsWindows) imports.Add(Button("Import AppImage…", ImportAppImage));
            page.Children.Add(Ui.Actions(imports.ToArray()));
            page.Children.Add(Ui.Hint(Platform.IsWindows ? "ZIP imports use a new folder and keep package files together. For 7z/RAR archives or installers, use your normal extraction/installation tool, then browse below. Portable mode remains an emulator-specific choice."
                : "Imports use a new folder and keep package files together; AppImages are marked executable. For tar.gz/tar.xz/7z archives, extract them with your usual tool, then browse below. Portable mode remains an emulator-specific choice."));
            page.Children.Add(Input("Main program or launcher", setupProgram, Button("Browse…", BrowseProgram)));
            page.Children.Add(Input("Display name", setupName));
            page.Children.Add(Ui.Actions(Button("Add to FishBowl", AddProgram), Button("Register as another build", RegisterSetupBuild)));
        }
        private async Task ChooseRoot()
        {
            var folder = await Ui.PickFolder(this, "Choose your dedicated emulator folder. Existing installations stay where they are.", HubPaths.EmulatorRoot(library));
            if (folder != null) { library.EmulatorRootDirectory = Path.GetFullPath(folder); Store.Save(library); rootBox.Text = HubPaths.EmulatorRoot(library); }
        }
        private void OpenRoot() { var root = HubPaths.EmulatorRoot(library); Directory.CreateDirectory(root); Platform.Open(root); }
        private static void OpenUrl(string url) { if (!EmulatorReference.IsWebUrl(url)) throw new IOException("Supply a full official http/https project address."); Platform.Open(url); }
        private Task<string> PickProgram(string initial) { return Ui.PickFile(this, "Choose the emulator's main program", Platform.ProgramFilter, initial); }
        private async Task BrowseProgram() { var file = await PickProgram(HubPaths.EmulatorRoot(library)); if (file != null) FillProgram(file); }
        private void FillProgram(string executable)
        {
            setupProgram.Text = executable; var known = EmulatorDiscovery.Recognize(executable);
            if (EmulatorCatalog.Find(setupPreset.SelectedItem as string).Name == "Custom" && known.Name != "Custom") setupPreset.SelectedItem = known.Label;
            if (String.IsNullOrWhiteSpace(setupName.Text)) setupName.Text = known.Name == "Custom" ? Path.GetFileNameWithoutExtension(executable) : known.Name;
        }
        private async Task ImportZip()
        {
            var zip = await Ui.PickFile(this, "Choose an emulator ZIP downloaded from its official project", "ZIP packages|*.zip");
            if (zip == null) return;
            var root = HubPaths.EmulatorRoot(library); var label = String.IsNullOrWhiteSpace(packageName.Text) ? Path.GetFileNameWithoutExtension(zip) : packageName.Text;
            await RunAsync(async () =>
            {
                progress.Text = "Extracting emulator package…";
                var folder = await Task.Run(() => EmulatorInstaller.InstallZip(zip, root, label, cancellation.Token));
                var found = await Task.Run(() => EmulatorDiscovery.Scan(folder, true, cancellation.Token));
                progress.Text = "Package extracted to " + folder;
                var picker = new CandidatePickerDialog(found.Items.OrderBy(x => EmulatorCatalog.Find(x.Preset).Name == "Custom").ThenBy(x => x.Name));
                await picker.Present(this);
                if (picker.Confirmed && picker.Selected != null) FillProgram(picker.Selected.Executable);
            });
        }
        private async Task ImportAppImage()
        {
            var file = await Ui.PickFile(this, "Choose an emulator AppImage downloaded from its official project", "AppImages|*.AppImage;*.appimage");
            if (file == null) return;
            var root = HubPaths.EmulatorRoot(library); var known = EmulatorDiscovery.Recognize(file);
            var label = !String.IsNullOrWhiteSpace(packageName.Text) ? packageName.Text : known.Name != "Custom" ? known.Name : Path.GetFileNameWithoutExtension(file);
            await RunAsync(async () =>
            {
                progress.Text = "Copying AppImage…";
                var installed = await Task.Run(() => EmulatorInstaller.InstallAppImage(file, root, label, cancellation.Token));
                FillProgram(installed); progress.Text = "AppImage copied to " + Path.GetDirectoryName(installed) + ". Add it to FishBowl or register it as another build.";
            });
        }
        private EmulatorProfile RegisterProgram(string executable, string displayName, string preset)
        {
            if (!File.Exists(executable) || !EmulatorReference.IsLaunchFile(executable)) throw new IOException("Choose an existing emulator program or launcher.");
            if (String.IsNullOrWhiteSpace(displayName)) throw new IOException("Enter an emulator display name.");
            if (library.Emulators.Any(e => HubPaths.Same(e.Executable, executable))) throw new IOException("This emulator program is already registered.");
            var added = new EmulatorProfile { Id = Guid.NewGuid().ToString("N"), Name = displayName.Trim(), Preset = preset, Executable = Path.GetFullPath(executable), Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>(), Builds = new List<EmulatorBuild>() };
            BuildRegistry.Remember(added, added.Executable, "Initial installation"); library.Emulators.Add(added); Store.Save(library); RefreshProfiles(added); return added;
        }
        private void AddProgram()
        {
            var added = RegisterProgram((setupProgram.Text ?? "").Trim().Trim('"'), setupName.Text, setupPreset.SelectedItem as string);
            if (EmulatorCatalog.Find(added.Preset).Name == "Custom" && EmulatorReference.IsWebUrl((downloadUrl.Text ?? "").Trim())) { added.WebsiteUrl = downloadUrl.Text.Trim(); added.ReleasesUrl = added.WebsiteUrl; Store.Save(library); }
            progress.Text = "Added " + added.Name + ". Storage folders are detected automatically where supported.";
        }
        private void RegisterSetupBuild()
        {
            var p = Selected(); var file = (setupProgram.Text ?? "").Trim().Trim('"');
            if (!File.Exists(file) || !EmulatorReference.IsLaunchFile(file)) throw new IOException("Choose the extracted emulator program first.");
            BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, packageName.Text); Store.Save(library); RefreshBuilds();
            tabs.SelectedIndex = 3; progress.Text = "Build registered. Select it here to make it active.";
        }

        // ----- Find installed ----------------------------------------------------------------------------------------

        private void BuildDiscovery()
        {
            var hint = Ui.Hint(Platform.IsWindows
                ? "Search the dedicated emulator folder, up to four subfolder levels. Game/storage folders and directory links are skipped. Check the programs you want to register. Azahar's shared filename requires choosing the Plus preset in the setup assistant."
                : "Finds emulators installed as Flatpaks, distribution packages on your PATH, and AppImages in the emulator folder, ~/Applications, ~/AppImages and ~/Downloads. The emulator folder is searched up to four levels deep; game/storage folders and links are skipped. Check the programs you want to register.");
            FillPage("Find installed", "search", discovered, hint, Ui.Actions(Button("Find installed emulators", ScanEmulators), unknownPrograms, Button("Add checked programs", AddDiscovered)));
        }
        private async Task ScanEmulators()
        {
            var root = HubPaths.EmulatorRoot(library); bool include = unknownPrograms.IsChecked == true;
            await RunAsync(async () =>
            {
                progress.Text = "Finding emulator programs…";
                var result = await Task.Run(() =>
                {
                    var found = EmulatorDiscovery.Scan(root, include, cancellation.Token);
                    if (!Platform.IsWindows)
                    {
                        var system = EmulatorDiscovery.ScanSystem(cancellation.Token);
                        if (found.Warnings.Count > 0 && found.Warnings[0].StartsWith("The emulator folder does not exist")) found.Warnings.Clear();
                        found.Items = found.Items.Concat(system.Items.Where(s => !found.Items.Any(f => HubPaths.Same(f.Executable, s.Executable)))).OrderBy(x => x.Name).ThenBy(x => x.Executable).ToList();
                        found.Warnings.AddRange(system.Warnings);
                    }
                    return found;
                });
                discovered.Clear();
                foreach (var candidate in result.Items)
                {
                    bool registered = library.Emulators.Any(e => HubPaths.Same(e.Executable, candidate.Executable) || (Platform.FlatpakId(e.Executable) != null && Platform.FlatpakId(e.Executable) == Platform.FlatpakId(candidate.Executable)));
                    discovered.Add(candidate, !registered && EmulatorCatalog.Find(candidate.Preset).Name != "Custom", candidate.Name + (registered ? " (already added)" : ""), candidate.Preset, candidate.Executable);
                }
                progress.Text = result.Items.Count + " programs found." + (result.Warnings.Count > 0 ? " " + result.Warnings.First() : "");
            });
        }
        private void AddDiscovered()
        {
            int added = 0;
            foreach (var item in discovered.CheckedItems)
            {
                var candidate = item.Tag as DiscoveredEmulator;
                if (candidate == null || library.Emulators.Any(e => HubPaths.Same(e.Executable, candidate.Executable))) continue;
                RegisterProgram(candidate.Executable, candidate.Name, candidate.Preset); added++; Table.Uncheck(item);
            }
            progress.Text = "Added " + added + " emulator programs.";
        }

        // ----- Updates -----------------------------------------------------------------------------------------------

        private void BuildUpdates()
        {
            var hint = Ui.Hint("Checks published releases from the project's GitHub feed when available. No emulator files are replaced automatically. Projects without a feed use their official download page." + (Platform.IsWindows ? "" : " Flatpak and distribution packages update through your package manager."));
            FillPage("Updates", "download", updateText, hint, Input("Optional GitHub repository override: owner/repository (blank = preset source)", repository),
                Ui.Actions(Button("Check for updates", CheckUpdates), Button("Official downloads", () => OpenUrl(EmulatorReference.For(Selected()).Releases)), Button("Open found release", () => OpenUrl(releaseUrl)), previews), InstallUpdateBar());
        }
        private void RefreshUpdates()
        {
            releaseUrl = profile == null ? "" : profile.LatestReleaseUrl; RefreshInstallInfo(profile);
            if (profile == null) { updateText.Text = "Select an emulator to check its releases."; return; }
            var snapshot = profile;
            updateText.Text = "Installed: checking…\nRelease source: " + (EmulatorUpdates.Repository(profile).Length > 0 ? EmulatorUpdates.Repository(profile) : "Official download page; no automatic feed") + "\n";
            if (!String.IsNullOrWhiteSpace(profile.LastUpdateCheck))
                updateText.Text += "\nLast successful check (UTC): " + profile.LastUpdateCheck + "\nLatest published release: " + profile.LatestReleaseTag + "\n\n" + profile.LatestReleaseNotes;
            else updateText.Text += "\nNo release check has been completed for this profile.";
            Task.Run(() => EmulatorReference.VersionFor(snapshot)).ContinueWith(t => Ui.Post(() => { if (profile == snapshot && updateText.Text.StartsWith("Installed: checking…")) updateText.Text = "Installed: " + t.Result + updateText.Text.Substring("Installed: checking…".Length); }));
        }
        private async Task CheckUpdates()
        {
            var selected = Selected(); var repo = (repository.Text ?? "").Trim();
            if (repo.Length > 0 && !EmulatorUpdates.ValidRepo(repo)) throw new IOException("Use owner/repository for the GitHub release source.");
            selected.GitHubRepository = repo; selected.IncludePreviewReleases = previews.IsChecked == true; Store.Save(library);
            await RunAsync(async () =>
            {
                progress.Text = "Checking official published releases…";
                UpdateResult result;
                try { result = await Task.Run(() => EmulatorUpdates.Check(selected)); }
                catch (WebException ex)
                {
                    RefreshUpdates(); updateText.Text += "\n\nThis check failed: " + ex.Message + "\nUse Official downloads. Cached results are not a current update check."; progress.Text = "Release feed unavailable. No up-to-date status was inferred."; return;
                }
                releaseUrl = result.Url;
                if (result.HasFeed) { selected.LatestReleaseTag = result.Latest; selected.LatestReleaseUrl = result.Url; selected.LatestReleaseNotes = result.Notes.Length > 60000 ? result.Notes.Substring(0, 60000) + "\n[Cached notes truncated. Open the release for the full changelog.]" : result.Notes; selected.LastUpdateCheck = result.CheckedAt; Store.Save(library); }
                updateText.Text = result.Status + "\n\nInstalled: " + result.Installed + "\nLatest: " + (result.Latest ?? "See official downloads") + "\nChecked (UTC): " + result.CheckedAt + "\n\n" + result.Notes;
                progress.Text = result.Status;
            });
        }

        // ----- Versions ----------------------------------------------------------------------------------------------

        private void BuildVersions()
        {
            var hint = Ui.Hint("Register separate installed builds and choose which program FishBowl opens. Previous builds stay on disk. Switching builds does not move settings or saves; use each emulator's storage rules. Save-state compatibility can vary between builds.");
            var bottom = Ui.Actions(Button("Import another build package…", () => { tabs.SelectedIndex = 0; packageName.Focus(); progress.Text = "Import a package into its own folder, then register the program here as another build."; }));
            DockPanel.SetDock(bottom, Dock.Bottom);
            var dock = FillPage("Versions", "layers", builds, hint, Ui.Actions(Button("Register installed build…", AddBuild), Button("Use selected build", UseBuild), Button("Forget selected entry", ForgetBuild)));
            dock.Children.Insert(1, bottom);
        }
        private void RefreshBuilds()
        {
            builds.Clear(); if (profile == null) return;
            var entries = new List<EmulatorBuild>(profile.Builds ?? new List<EmulatorBuild>());
            if (!entries.Any(b => HubPaths.Same(b.Executable, profile.Executable))) entries.Insert(0, new EmulatorBuild { Id = "", Label = "Current installation", Executable = profile.Executable, ManualVersion = profile.ManualVersion });
            foreach (var build in entries)
                builds.Add(build, false, build.Label ?? "Emulator build", HubPaths.Same(build.Executable, profile.Executable) ? "Active" : File.Exists(build.Executable) ? "Available" : "Missing", build.Executable);
        }
        private async Task AddBuild()
        {
            var p = Selected(); var file = await PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            var label = await TextPromptDialog.Ask(this, "Emulator build", "Name this installed build", Path.GetFileName(Path.GetDirectoryName(file)));
            if (label == null) return;
            BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, label.Trim()); Store.Save(library); RefreshBuilds();
        }
        private void UseBuild()
        {
            var p = Selected(); var build = builds.SelectedTag as EmulatorBuild; if (build == null) throw new IOException("Select a build first."); RequireStopped(p);
            BuildRegistry.Activate(p, build); Store.Save(library); RefreshSelected(); progress.Text = "Active build changed. Review detected/manual storage paths before launching.";
        }
        private void ForgetBuild()
        {
            var p = Selected(); var build = builds.SelectedTag as EmulatorBuild; if (build == null) return;
            if (HubPaths.Same(build.Executable, p.Executable)) throw new IOException("The active build remains registered.");
            if (p.Builds != null) p.Builds.Remove(build); Store.Save(library); RefreshBuilds(); progress.Text = "Build entry forgotten. Its files remain on disk.";
        }

        // ----- Backups -----------------------------------------------------------------------------------------------

        private void BuildBackups()
        {
            var hint = Ui.Hint("Back up configuration, in-game saves and save states separately. Review the preview before creating an archive. Emulator/ROM files and installed content are excluded. Close the emulator first. Restore preserves a backup of the current files before overwriting.");
            backupRootBox.Text = HubPaths.BackupRoot(library);
            var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var entry in new[] { "ConfigFolder|Configuration", "InGameSaveFolder|In-game saves", "SaveStateFolder|Save states" })
            {
                var pieces = entry.Split('|'); var box = Ui.Check(pieces[1], true);
                box.IsCheckedChanged += delegate { backupPlan = null; backupText.Text = "Categories changed. Preview the backup again."; };
                categories.Add(pieces[0], box); choices.Children.Add(box);
            }
            FillPage("Backups", "backup", backupText, hint, Input("Backup destination", backupRootBox, Button("Choose…", ChooseBackupRoot)), choices,
                Ui.Actions(Button("Preview backup", PreviewBackup), Button("Create backup", CreateBackup), Button("Restore ZIP…", RestoreBackup), Button("Open backup folder", () => { var folder = HubPaths.BackupRoot(library); Directory.CreateDirectory(folder); Platform.Open(folder); })));
        }
        private async Task ChooseBackupRoot()
        {
            var folder = await Ui.PickFolder(this, "Store emulator backups outside the emulator's source/save folders.", HubPaths.BackupRoot(library));
            if (folder != null) { library.BackupFolder = folder; Store.Save(library); backupRootBox.Text = HubPaths.BackupRoot(library); }
        }
        private async Task PreviewBackup()
        {
            var selected = Selected(); RequireStopped(selected); var choices = categories.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToArray();
            if (choices.Length == 0) throw new IOException("Choose at least one backup category.");
            await RunAsync(async () =>
            {
                progress.Text = "Inspecting configuration and save folders…";
                var plan = await Task.Run(() => EmulatorBackups.Preview(selected, choices, cancellation.Token));
                backupPlan = plan; planProfileId = selected.Id; backupText.Text = plan.Summary(); progress.Text = "Preview ready. Review the source folders and included files.";
            });
        }
        private async Task CreateBackup()
        {
            var selected = Selected(); RequireStopped(selected); if (backupPlan == null || planProfileId != selected.Id) throw new IOException("Preview the selected backup categories first.");
            var plan = backupPlan; var destination = HubPaths.BackupRoot(library);
            await RunAsync(async () =>
            {
                progress.Text = "Creating and verifying backup archive…";
                var file = await Task.Run(() => { var created = EmulatorBackups.Create(selected, plan, destination, cancellation.Token); BackupIntegrity.Verify(created, cancellation.Token); return created; });
                SaveHistory.EnsureData(library); library.Theme.LastBackupAt = DateTime.UtcNow.ToString("o"); library.Experience.LastSuccessfulBackup = library.Theme.LastBackupAt; Store.Save(library);
                backupText.Text += "\n\nCreated:\n" + file; progress.Text = "Backup completed.";
            });
        }
        private async Task RestoreBackup()
        {
            var selected = Selected(); RequireStopped(selected);
            var file = await Ui.PickFile(this, "Choose a FishBowl emulator backup", "FishBowl backups|*.zip", HubPaths.BackupRoot(library));
            if (file == null) return;
            BackupManifest manifest;
            using (var archive = System.IO.Compression.ZipFile.OpenRead(file)) { manifest = EmulatorBackups.ReadManifest(archive); EmulatorBackups.ValidateRestore(selected, manifest); }
            var detail = "Restore backup for " + selected.Name + "?\n\nMatching files will be overwritten. Other files are retained. Current eligible files are backed up before restoration.\n\nArchive: " + file + "\nCreated (UTC): " + manifest.CreatedAt + "\n\nDestinations:\n" + String.Join("\n\n", manifest.Roots.Select(r => r.Label + "\n" + r.Source)) + "\n\nIncluded files: " + manifest.Files.Count + "\n" + String.Join("\n", manifest.Files.Take(300).Select(f => f.ArchivePath)) + (manifest.Files.Count > 300 ? "\nFirst 300 shown; the archive manifest lists every file." : "");
            var review = new ReviewDialog("Review emulator restore", detail, "Restore these files"); await review.Present(this);
            if (!review.Confirmed) return;
            var destination = HubPaths.BackupRoot(library);
            await RunAsync(async () =>
            {
                progress.Text = "Validating archive checksums and restoring…"; var before = await Task.Run(() => EmulatorBackups.Restore(selected, file, destination, cancellation.Token));
                backupPlan = null; backupText.Text = "Restore completed.\n\n" + (before.Length > 0 ? "Previous files preserved in:\n" + before : "No existing eligible files needed a backup."); progress.Text = "Restore completed."; RefreshHealth();
            });
        }

        // ----- Setup checks ------------------------------------------------------------------------------------------

        private void BuildHealth()
        {
            var hint = Ui.Hint("Check the program path, running status and storage folder availability. An uncreated save folder is informational. BIOS/firmware validity and graphics/controller configuration are checked inside the emulator.");
            requirementText.Height = 130; DockPanel.SetDock(requirementText, Dock.Bottom); requirementText.Margin = new Thickness(0, 10, 0, 0);
            var dock = FillPage("Setup checks", "check", health, hint, Ui.Actions(Button("Refresh checks", RefreshHealth), Button("Choose firmware folder…", ChooseFirmware), Button("Setup guide", () => OpenUrl(EmulatorReference.For(Selected()).Documentation)), Button("Repair location", RepairLocation)), FlatpakBar());
            dock.Children.Insert(1, requirementText);
        }
        private async void RefreshHealth()
        {
            health.Clear(); ShowFlatpakFixes(null, null); if (profile == null) { requirementText.Text = "Select an emulator to review its setup requirements."; return; }
            var snapshot = profile;
            requirementText.Text = (EmulatorReference.For(profile).Requirements ?? "").Replace("\r\n", "\n");
            try
            {
                var checks = await Task.Run(() => { var list = EmulatorHealth.Check(snapshot, library); if (!Platform.IsWindows) list.AddRange(FlatpakSetup.Checks(snapshot, library)); return list; });
                if (profile != snapshot) return;
                health.Clear(); foreach (var check in checks) health.Add(check, false, check.Name, check.Status, check.Detail);
                ShowFlatpakFixes(snapshot, checks);
            }
            catch (Exception ex) { Store.Log("Setup checks failed: " + ex.Message); }
        }
        private async Task ChooseFirmware()
        {
            var selected = Selected(); var folder = await Ui.PickFolder(this, "Choose the BIOS/firmware folder already configured inside " + selected.Name, selected.FirmwareFolder);
            if (folder != null) { selected.FirmwareFolder = folder; Store.Save(library); RefreshHealth(); }
        }
        private async Task RepairLocation()
        {
            if (busy) return;
            var selected = Selected(); var file = await PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            BuildRegistry.Repair(selected, file); Store.Save(library); RefreshSelected(); progress.Text = "Program location repaired. Your emulator information and folder overrides were retained.";
        }
    }
}
