using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private async Task OpenSelectedEmulator()
        {
            var profile = CurrentEmulator();
            if (profile == null) { SetStatus("Select an emulator first."); return; }
            if (!File.Exists(profile.Executable)) { await RepairSelectedLocation(); if (!File.Exists(profile.Executable)) return; }
            var state = await Task.Run(() => EmulatorRuntime.State(profile.Executable));
            if (state == RuntimeState.Running)
            {
                var focused = await Task.Run(() => EmulatorRuntime.BringForward(profile.Executable));
                SetStatus(profile.Name + " is already running." + (focused ? "" : " Switch to its window to continue."));
                return;
            }
            Platform.Launch(profile.Executable, "");
            Store.Log("Opened " + profile.Name + ": " + profile.Executable);
            Platform.ProcessesChanged();
            await Task.Delay(400); RefreshRuntimeStatus();
            SetStatus("Opened " + profile.Name + ". Add and launch games inside the emulator.");
        }

        private Task AddEmulator() { return AddEmulatorWithPath(null); }
        private async Task AddEmulatorWithPath(string path)
        {
            var dialog = new EmulatorDialog(null, path);
            await dialog.Present(this);
            if (!dialog.Confirmed) return;
            var added = dialog.Profile; added.Id = Guid.NewGuid().ToString("N");
            BuildRegistry.Remember(added, added.Executable, "Initial installation");
            library.Emulators.Add(added); selectedEmulatorId = added.Id;
            Store.Save(library); filterBox.Text = ""; RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Added " + added.Name + ".");
            await ShowFirmwareSetupNotice(added);
        }

        private async Task ShowFirmwareSetupNotice(EmulatorProfile profile)
        {
            var reference = EmulatorReference.For(profile);
            var requirements = Lf(reference.Requirements);
            bool requiresSetup = new[] { "BIOS", "firmware", "key", "system software" }.Any(word => requirements.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!requiresSetup) return;
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string message = profile.Name + " requires additional setup before it can run some or all games.\n\nWHAT THIS EMULATOR REQUIRES\n" + requirements + "\n\nWHERE TO CONFIGURE IT\nIn FishBowl, select " + profile.Name + " and open Emulators > Setup checks. Choose the firmware folder you already use, then follow the emulator's own setup screen.\n\nFishBowl does not include, download, or bypass BIOS, firmware, system software, or keys. Use files you are allowed to use and the emulator's official instructions.\n\nOpen the official setup guide now?";
            if (!await Ui.Confirm(this, message, "Setup required")) return;
            if (EmulatorReference.IsWebUrl(guide)) Platform.Open(guide);
            else await Ui.Message(this, "This emulator does not have a setup link saved yet. Open Edit information and links to add its official guide.");
        }

        private async Task EditEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            var previousExecutable = current.Executable; var previousVersion = current.ManualVersion;
            var dialog = new EmulatorDialog(current);
            await dialog.Present(this);
            if (!dialog.Confirmed) return;
            dialog.Profile.Id = current.Id;
            if (!HubPaths.Same(previousExecutable, dialog.Profile.Executable))
            {
                var previous = new EmulatorProfile { Executable = previousExecutable, ManualVersion = previousVersion, Builds = dialog.Profile.Builds ?? new List<EmulatorBuild>() };
                BuildRegistry.Remember(previous, previousExecutable, "Previous installation"); dialog.Profile.Builds = previous.Builds;
                dialog.Profile.ManualVersion = ""; BuildRegistry.Remember(dialog.Profile, dialog.Profile.Executable, "Edited location");
            }
            library.Emulators[library.Emulators.IndexOf(current)] = dialog.Profile;
            reloadProgramMetadata = true; Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Updated " + dialog.Profile.Name + ".");
        }

        private async Task RemoveEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            if (!await Ui.Confirm(this, "Remove " + current.Name + " from FishBowl?\n\nIts emulator program, games, and saves will stay where they are.")) return;
            GameLibraryRemoval.RemoveEmulator(library, current.Id); selectedEmulatorId = null;
            Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); RefreshGameLibrary(); SetStatus("Emulator removed from FishBowl. Use Undo removal in Library to restore it.");
        }

        private async Task HandleDroppedPaths(List<string> paths)
        {
            foreach (var path in paths)
                if (File.Exists(path) && EmulatorReference.IsLaunchFile(path))
                {
                    if (library.Emulators.Any(e => HubPaths.Same(e.Executable, path))) { SetStatus("That emulator is already in FishBowl."); continue; }
                    await AddEmulatorWithPath(path);
                }
                else SetStatus("Add games inside the emulator. Drop an emulator program or launcher here to add it to FishBowl.");
        }

        private void OpenEmulatorFolder()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            var flatpak = Platform.FlatpakId(profile.Executable);
            var folder = flatpak != null ? Path.Combine(Platform.Home, ".var", "app", flatpak) : Path.GetDirectoryName(profile.Executable);
            if (!Directory.Exists(folder)) folder = Path.GetDirectoryName(profile.Executable);
            if (Directory.Exists(folder)) Platform.Open(folder);
        }

        private async Task ToggleEmulatorFavorite()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes(); profile.Favorite = !profile.Favorite; Store.Save(library); RefreshHub();
            await Task.CompletedTask;
        }
        private async Task EditEmulatorInformation()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes();
            var dialog = new EmulatorInformationDialog(profile);
            await dialog.Present(this);
            if (!dialog.Confirmed) return;
            Store.Save(library); folderRequestKey = null; RefreshHub(); SetStatus("Saved information for " + profile.Name + ".");
        }
        private void OpenInformationLink(string url)
        {
            if (!EmulatorReference.IsWebUrl(url)) throw new InvalidDataException("Enter an http or https web address in Edit information.");
            Platform.Open(url);
        }

        private async Task OpenOfficialSetupGuidance()
        {
            var profile = CurrentEmulator();
            if (profile == null) { await Ui.Message(this, "Select an emulator first."); return; }
            var reference = EmulatorReference.For(profile);
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string requirements = Lf(reference.Requirements ?? "Review the emulator's official setup instructions for any required system files, graphics setup, and controller configuration.");
            string message = profile.Name + " setup guidance\n\n" + requirements + "\n\nFishBowl can open the emulator project's official guide. For firmware, BIOS, keys, or system software, use only files you are allowed to use and official instructions. FishBowl does not link to third-party downloads.\n\nOpen the official guide now?";
            if (!await Ui.Confirm(this, message, "Official setup guidance")) return;
            if (!EmulatorReference.IsWebUrl(guide)) { await Ui.Message(this, "No official setup guide is saved for this emulator. Use Edit information and links to add one."); return; }
            Platform.Open(guide); Store.Log("Opened official setup guidance for " + profile.Name);
        }

        private async Task RepairSelectedLocation()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            var file = await Ui.PickFile(this, "Locate " + profile.Name + "'s emulator program", Platform.ProgramFilter, HubPaths.EmulatorRoot(library));
            if (file == null) return;
            BuildRegistry.Repair(profile, file); Store.Save(library); reloadProgramMetadata = true; RefreshHub(); SetStatus("Program location repaired for " + profile.Name + ".");
        }

        private async Task ShowEmulatorManager(string page)
        {
            SaveProfileNotes();
            var dialog = new EmulatorManagerDialog(library, CurrentEmulator(), page);
            await dialog.Present(this);
            if (dialog.SelectedProfile != null) selectedEmulatorId = dialog.SelectedProfile.Id;
            reloadProgramMetadata = true; folderRequestKey = null; RefreshHub();
        }

        // ----- Game converters ---------------------------------------------------------------------------------------

        private void RefreshConvertersMenu()
        {
            var items = new List<object> {
                MenuAction("Prepare game for selected emulator...", "play", PrepareGameForSelectedEmulator),
                MenuAction("View emulator format presets", "info", ShowFormatPresets), new Separator(),
                MenuAction("Add converter for selected emulator...", "add", AddGameConverter) };
            if (!library.Converters.Any()) items.Add(Disabled("No game converters added"));
            else
            {
                items.Add(new Separator());
                foreach (var tool in library.Converters.OrderBy(item => item.Name))
                {
                    var saved = tool; var emulator = library.Emulators.FirstOrDefault(item => item.Id == saved.EmulatorId);
                    items.Add(MenuAction((emulator == null ? "Unassigned" : emulator.Name) + " - " + saved.Name, "play", () => RunGameConverter(saved)));
                }
            }
            convertersMenu.ItemsSource = items;
        }
        private Task ShowFormatPresets()
        {
            var rows = library.Emulators.OrderBy(item => item.Name).Select(item => item.Name + " | " + EmulatorReference.PlatformFor(item) + " | " + String.Join(", ", item.Extensions ?? new List<string>())).ToList();
            return new ResultsDialog("Emulator Format Presets", rows.Any() ? (IEnumerable<string>)rows : new[] { "Add an emulator to see its supported game formats." }).Present(this);
        }
        private async Task PrepareGameForSelectedEmulator()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { await Ui.Message(this, "Select an emulator first."); return; }
            var file = await Ui.PickFile(this, "Choose a game archive or file for " + emulator.Name, "All files|*");
            if (file == null) return;
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension == ".zip") { await ExtractZipForEmulator(emulator, file); return; }
            var converter = library.Converters.FirstOrDefault(item => item.EmulatorId == emulator.Id && (item.InputExtensions ?? "").Split(',').Any(value => String.Equals(value.Trim().TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
            if (converter == null) { await Ui.Message(this, "FishBowl has no registered extractor or converter for " + extension + " with " + emulator.Name + ".\n\nAdd one from Game converters, then try again."); return; }
            await RunGameConverter(converter, file);
        }
        private async Task ExtractZipForEmulator(EmulatorProfile emulator, string archivePath)
        {
            try
            {
                string root = !String.IsNullOrWhiteSpace(emulator.ScanFolder) && Directory.Exists(emulator.ScanFolder) ? emulator.ScanFolder : Path.GetDirectoryName(archivePath);
                string destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath));
                int suffix = 2; while (Directory.Exists(destination)) destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath) + " (" + suffix++ + ")");
                await Task.Run(() =>
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(archivePath)) EmulatorInstaller.ValidateArchive(archive, destination, false);
                    System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, destination);
                });
                var supported = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => (emulator.Extensions ?? new List<string>()).Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).ToList();
                Store.Log("Extracted ZIP for " + emulator.Name + ": " + archivePath);
                await new ResultsDialog("Extraction Complete", supported.Any() ? supported.Select(path => "Compatible game: " + path) : new[] { "Extracted to: " + destination, "No files matching " + emulator.Name + "'s configured formats were found." }).Present(this);
            }
            catch (Exception error) { await Ui.Message(this, "FishBowl could not extract that ZIP archive.\n\n" + error.Message); }
        }
        private async Task AddGameConverter()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { await Ui.Message(this, "Select an emulator first, then add its converter."); return; }
            var name = await TextPromptDialog.Ask(this, "Game Converter", "Converter name for " + emulator.Name, "");
            if (String.IsNullOrWhiteSpace(name)) return;
            var program = await Ui.PickFile(this, "Choose converter program or script", Platform.ScriptFilter);
            if (program == null) return;
            var input = await TextPromptDialog.Ask(this, "Game Converter", "Input file extensions, for example .zip, .rar, .bin", "");
            if (input == null) return;
            var output = await TextPromptDialog.Ask(this, "Game Converter", "Output extension, for example .3ds (leave blank for extract-only tools)", "");
            if (output == null) return;
            var arguments = await TextPromptDialog.Ask(this, "Game Converter", "Arguments - use {input} and {output}", "{input} {output}");
            if (arguments == null) return;
            library.Converters.Add(new GameConverter { Id = Guid.NewGuid().ToString("N"), EmulatorId = emulator.Id, Name = name.Trim(), Program = program, InputExtensions = input.Trim(), OutputExtension = output.Trim(), Arguments = arguments.Trim() });
            Store.Save(library); SetStatus("Added game converter for " + emulator.Name + ".");
        }
        private async Task RunGameConverter(GameConverter tool)
        {
            if (!File.Exists(tool.Program)) { await Ui.Message(this, "The converter program cannot be found. Add it again from Game converters."); return; }
            var patterns = (tool.InputExtensions ?? "").Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).Select(item => "*" + (item.StartsWith(".") ? item : "." + item)).ToArray();
            string filter = patterns.Length == 0 ? "All files|*" : "Supported input|" + String.Join(";", patterns) + "|All files|*";
            var file = await Ui.PickFile(this, "Choose a game file for " + tool.Name, filter);
            if (file != null) await RunGameConverter(tool, file);
        }
        private async Task RunGameConverter(GameConverter tool, string inputPath)
        {
            if (!File.Exists(tool.Program)) { await Ui.Message(this, "The converter program cannot be found. Add it again from Game converters."); return; }
            string extension = tool.OutputExtension ?? ""; if (extension.Length > 0 && !extension.StartsWith(".")) extension = "." + extension;
            string output = extension.Length == 0 ? Path.GetDirectoryName(inputPath) : Path.ChangeExtension(inputPath, extension);
            string arguments = (tool.Arguments ?? "{input}").Replace("{input}", "\"" + inputPath.Replace("\"", "\\\"") + "\"").Replace("{output}", "\"" + output.Replace("\"", "\\\"") + "\"");
            try { Platform.Launch(tool.Program, arguments); Store.Log("Ran game converter: " + tool.Name); SetStatus("Started " + tool.Name + "."); }
            catch (Exception error) { await Ui.Message(this, "The converter could not be started.\n\n" + error.Message); }
        }

        // ----- Website links -----------------------------------------------------------------------------------------

        private void RefreshWebsiteLinksMenu()
        {
            var items = new List<object> { MenuAction("Add website link...", "add", AddWebsiteLink) };
            if (!library.Links.Any()) items.Add(Disabled("No saved links"));
            else
            {
                items.Add(MenuAction("Remove website link...", "remove", RemoveWebsiteLink));
                items.Add(new Separator());
                foreach (var link in library.Links.OrderBy(item => item.Name)) { var saved = link; items.Add(MenuAction(saved.Name, "globe", () => OpenWebsiteLink(saved))); }
            }
            linksMenu.ItemsSource = items;
        }
        private async Task AddWebsiteLink()
        {
            var dialog = new WebsiteLinkDialog(); await dialog.Present(this);
            if (!dialog.Confirmed) return;
            library.Links.Add(dialog.Link); Store.Save(library); SetStatus("Saved website link: " + dialog.Link.Name + ".");
        }
        private async Task RemoveWebsiteLink()
        {
            if (!library.Links.Any()) return;
            var dialog = new WebsiteLinkPicker(library.Links); await dialog.Present(this);
            if (!dialog.Confirmed || dialog.SelectedLink == null) return;
            library.Links.RemoveAll(link => link.Id == dialog.SelectedLink.Id); Store.Save(library);
            SetStatus("Removed website link: " + dialog.SelectedLink.Name + ".");
        }
        private async Task OpenWebsiteLink(WebsiteLink link)
        {
            if (!EmulatorReference.IsWebUrl(link.Url)) { await Ui.Message(this, "This saved link is not a valid http or https address."); return; }
            Platform.Open(new Uri(link.Url).AbsoluteUri); SetStatus("Opening " + link.Name + " in your browser.");
        }

        // ----- Settings, import/export, diagnostics ------------------------------------------------------------------

        private async Task ExportLibrary()
        {
            var file = await Ui.SaveFile(this, "Export FishBowl settings", "JSON files|*.json", "FishBowl-settings.json");
            if (file == null) return;
            Store.Save(library); File.Copy(Store.FileName, file, true); SetStatus("Exported FishBowl settings.");
        }
        private async Task ImportLibrary()
        {
            var file = await Ui.PickFile(this, "Import FishBowl settings", "JSON files|*.json");
            if (file == null) return;
            if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before importing settings.");
            var imported = Json.Deserialize<LibraryData>(File.ReadAllText(file));
            if (imported == null || imported.Emulators == null) throw new InvalidDataException("That file does not contain FishBowl emulator settings.");
            if (!await Ui.Confirm(this, "Replace your FishBowl settings with this backup?")) return;
            Store.Save(library); File.Copy(Store.FileName, Store.FileName + ".before-import.json", true);
            foreach (var property in typeof(LibraryData).GetProperties().Where(p => p.CanRead && p.CanWrite)) property.SetValue(library, property.GetValue(imported));
            library.Games = library.Games ?? new List<GameEntry>(); library.Collections = library.Collections ?? new List<GameCollection>(); library.Links = library.Links ?? new List<WebsiteLink>(); library.Converters = library.Converters ?? new List<GameConverter>();
            library.Theme = library.Theme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7 };
            foreach (var game in library.Games) if (game.Tags == null) game.Tags = new List<string>();
            foreach (var collection in library.Collections) if (collection.GameIds == null) collection.GameIds = new List<string>();
            LibraryPaths.Load(library, Store.DataDirectory, Store.PortableMode ? AppDomain.CurrentDomain.BaseDirectory : null);
            foreach (var emulator in library.Emulators) { if (emulator.LaunchProfiles == null) emulator.LaunchProfiles = new List<LaunchProfile>(); if (emulator.Builds == null) emulator.Builds = new List<EmulatorBuild>(); }
            selectedEmulatorId = null;
            Store.Save(library); filterBox.Text = ""; RefreshHub(); RefreshGameLibrary(); ConfigureGameFolderWatchers(); SetStatus("Imported FishBowl settings. Reopen FishBowl to apply the theme.");
        }

        // How to start this copy of FishBowl again: the published executable, or the dotnet host plus FishBowl.dll.
        private static ProcessStartInfo SelfStart(out string argument)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false };
            argument = null;
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") { argument = typeof(MainWindow).Assembly.Location; info.ArgumentList.Add(argument); }
            return info;
        }

        private async Task CreateShortcut()
        {
            string argument; var app = SelfStart(out argument).FileName;
            var icon = Path.Combine(AppContext.BaseDirectory, "FishBowl.png");
            if (!File.Exists(icon))
            {
                icon = Path.Combine(Store.DataDirectory, "FishBowl.png"); Directory.CreateDirectory(Store.DataDirectory);
                using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("FishBowl.png")) using (var output = File.Create(icon)) stream.CopyTo(output);
            }
            try { await Ui.Message(this, Platform.CreateAppShortcut(app, icon, argument)); }
            catch (Exception error) { await Ui.Message(this, "FishBowl could not create the shortcut.\n\n" + error.Message); }
        }

        private Task ShowSettings() { return ShowSettings(this); }
        private async Task ShowSettings(Window owner)
        {
            var dialog = new SettingsDialog(library.Theme, library.BackupFolder);
            await dialog.Present(owner);
            if (!dialog.Confirmed) return;
            var t = library.Theme; var n = dialog.ChosenTheme;
            bool restartRequired = !String.Equals(t.Name, n.Name, StringComparison.OrdinalIgnoreCase) || t.StartMaximized != n.StartMaximized || !String.Equals(t.AccentColor, n.AccentColor, StringComparison.OrdinalIgnoreCase) || !String.Equals(t.FontFamily, n.FontFamily, StringComparison.OrdinalIgnoreCase) || t.UiScalePercent != n.UiScalePercent || !String.Equals(t.ListDensity, n.ListDensity, StringComparison.OrdinalIgnoreCase) || t.ShowBanner != n.ShowBanner || t.ShowStatusBar != n.ShowStatusBar || t.ShowInformationPanel != n.ShowInformationPanel || t.ShowEmulatorIcons != n.ShowEmulatorIcons || t.EnableMotion != n.EnableMotion || t.AlternateRowShading != n.AlternateRowShading || t.SelectionContrast != n.SelectionContrast || t.IconTileShape != n.IconTileShape;
            library.Theme = n; library.BackupFolder = dialog.BackupFolder;
            Store.Save(library); ConfigureGameFolderWatchers();
            if (restartRequired && await Ui.Confirm(owner, "These settings need FishBowl to restart before they can take effect.\n\nRestart FishBowl now?", "Restart FishBowl"))
            {
                Store.Log("Restarting to apply settings.");
                SaveProfileNotes(); SaveWindowLayout();
                string argument; using (Process.Start(SelfStart(out argument))) { }
                Close();
            }
            else if (restartRequired) await Ui.Message(owner, "Settings saved. Restart FishBowl whenever you are ready to apply them.");
            else SetStatus("Settings saved and applied.");
        }

        private async Task EnablePortableMode()
        {
            if (Store.PortableMode) { await Ui.Message(this, "FishBowl is already using portable storage beside the app."); return; }
            Store.EnablePortableMode(library);
            await Ui.Message(this, "Portable mode is ready. Your library is now stored beside FishBowl in the FishBowlData folder.");
        }

        private Task ShowDiagnostics()
        {
            var rows = new List<string>();
            rows.Add("FishBowl data: " + Store.DataDirectory);
            rows.Add("Settings file: " + (File.Exists(Store.FileName) ? "Available" : "Not yet created"));
            rows.Add("Platform: " + Platform.OsName + " / .NET " + Environment.Version + (Platform.IsWindows ? "" : " / session " + (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown")));
            rows.Add("Emulators: " + library.Emulators.Count + "    Games: " + library.Games.Count + "    Collections: " + library.Collections.Count);
            foreach (var emulator in library.Emulators) rows.Add((File.Exists(emulator.Executable) ? "OK  " : "MISSING  ") + emulator.Name + " — " + emulator.Executable);
            foreach (var game in library.Games.Where(item => !File.Exists(item.Path)).Take(20)) rows.Add("MISSING GAME  " + game.Title + " — " + game.Path);
            try
            {
                var backups = HubPaths.BackupRoot(library); var probe = backups; while (!Directory.Exists(probe) && Path.GetDirectoryName(probe) != null) probe = Path.GetDirectoryName(probe);
                var drive = new DriveInfo(probe); rows.Add("Backup drive free space: " + (drive.AvailableFreeSpace / 1024 / 1024 / 1024) + " GB");
            }
            catch { rows.Add("Backup drive free space: unavailable"); }
            rows.Add("Activity and crash report: " + Store.LogFileName);
            return new ResultsDialog("FishBowl Diagnostics", rows).Present(this);
        }

        // ----- Assistants --------------------------------------------------------------------------------------------

        private async Task ShowFirstRunGuide()
        {
            var dialog = new StartupAssistantDialog(); await dialog.Present(this);
            if (!dialog.Confirmed) return;
            library.Theme.ShowStartupAssistant = !dialog.DontShowAgain; library.Theme.StartupAssistantPreferenceSet = true; Store.Save(library);
            if (dialog.OpenSetupAssistant) await ShowEmulatorManager("Setup assistant");
        }
        private async Task ShowGameStoragePrompt()
        {
            var dialog = new GameStoragePromptDialog(); await dialog.Present(this);
            if (!dialog.Confirmed) return;
            library.Theme.ShowGameStorageAssistant = !dialog.DontShowAgain; library.Theme.GameStorageAssistantPreferenceSet = true; Store.Save(library);
            if (dialog.OpenOrganizer) await ShowGameStorageOrganizer(null);
        }
        private async Task ShowGameStorageOrganizer(IEnumerable<string> droppedPaths)
        {
            await new GameStorageOrganizerDialog(library, droppedPaths).Present(this);
            Store.Save(library);
        }

        // ----- Game folder sync (kept for parity with the Windows build) ----------------------------------------------

        private void ConfigureGameFolderWatchers()
        {
            foreach (var watcher in gameFolderWatchers) watcher.Dispose();
            gameFolderWatchers.Clear();
            if (!library.Theme.AutoSyncGameFolders) return;
            foreach (var emulator in library.Emulators.Where(item => Directory.Exists(item.ScanFolder)))
            {
                try
                {
                    var watcher = new FileSystemWatcher(emulator.ScanFolder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
                    FileSystemEventHandler changed = delegate { Ui.Post(() => { gameFolderTimer.Stop(); gameFolderTimer.Start(); }); };
                    watcher.Created += changed; watcher.Deleted += changed; watcher.Renamed += (s, e) => changed(s, e);
                    gameFolderWatchers.Add(watcher);
                }
                catch (Exception error) { Store.Log("Game folder watch unavailable: " + error.Message); }
            }
        }
        private void SyncWatchedGameFolders()
        {
            int added = GameLibraryCatalog.Sync(library);
            if (added > 0) { Store.Save(library); RefreshGameLibrary(); SetStatus("Added " + added + " newly detected game" + (added == 1 ? "." : "s.")); }
        }
    }
}
