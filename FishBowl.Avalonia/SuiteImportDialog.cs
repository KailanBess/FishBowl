using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmulatorHub
{
    // Tools > EmuDeck / RetroDECK: registers the emulators a suite installed and imports its per-system ROM folders.
    // Nothing is moved or changed inside the suite; games stay where they are.
    public class SuiteImportDialog : FishDialog
    {
        private readonly LibraryData library; private readonly Action changed;
        private readonly List<SuiteInstall> installs;
        private readonly ComboBox suiteBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32 };
        private readonly TextBlock summary = Ui.Hint(""), status = Ui.Hint("");
        private readonly Table emulators = new Table("2*,2*,5*", true, "Emulator", "Status", "Program");
        private readonly Table folders = new Table("2*,3*,*,2*", true, "ROM folder", "Console", "Games", "Opens with");
        private readonly CheckBox useBios = Ui.Check("Use the suite's BIOS folder as the firmware folder of emulators added here", true);

        public SuiteImportDialog(LibraryData data, Action onChanged) : base("FishBowl — EmuDeck and RetroDECK", 900, 760)
        {
            library = data; changed = onChanged; MinWidth = 720; MinHeight = 600;
            installs = Suites.Find(Platform.Home, Platform.DataHome);
            var top = new StackPanel();
            top.Children.Add(Ui.Hint("FishBowl reads where EmuDeck or RetroDECK keeps ROMs and BIOS files, registers the emulators they installed, and adds the games in their ROM folders to your library. Files stay where they are and the suite keeps working as before."));
            if (installs.Count == 0)
                top.Children.Add(Ui.Text("Neither EmuDeck (~/.config/EmuDeck/settings.sh) nor RetroDECK (the net.retrodeck.retrodeck Flatpak) was found for this account.", 13));
            else { top.Children.Add(Ui.Caption("Suite")); suiteBox.ItemsSource = installs; suiteBox.SelectionChanged += delegate { Load(); }; top.Children.Add(suiteBox); }
            top.Children.Add(summary);
            var emulatorPage = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var emulatorTop = new StackPanel { Children = { Ui.Caption("Emulators"), useBios } }; DockPanel.SetDock(emulatorTop, Dock.Top);
            var addEmulators = Ui.Actions(Ui.Action("Add checked emulators", AddEmulators, true)); DockPanel.SetDock(addEmulators, Dock.Bottom);
            emulatorPage.Children.Add(emulatorTop); emulatorPage.Children.Add(addEmulators); emulatorPage.Children.Add(emulators);
            var folderPage = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var folderTop = Ui.Hint("Checked folders are added to the Library. Add the emulators first so each game opens with its system's emulator; games without one ask for an emulator when you play them."); DockPanel.SetDock(folderTop, Dock.Top);
            var addFolders = Ui.Actions(Ui.Action("Import checked folders", ImportFolders, true)); DockPanel.SetDock(addFolders, Dock.Bottom);
            folderPage.Children.Add(folderTop); folderPage.Children.Add(addFolders); folderPage.Children.Add(folders);
            var tabs = new TabControl { ItemsSource = new[] { new TabItem { Header = "Emulators", Content = emulatorPage }, new TabItem { Header = "ROM folders", Content = folderPage } } };
            var close = Ui.Action("Close", () => Close()); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 10, 0, 0);
            var bottom = new StackPanel { Children = { status, close } };
            var layout = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom);
            layout.Children.Add(top); layout.Children.Add(bottom); layout.Children.Add(tabs);
            Body = layout;
            if (installs.Count > 0) suiteBox.SelectedIndex = 0;
        }

        private SuiteInstall Suite { get { return suiteBox.SelectedItem as SuiteInstall; } }

        private async void Load()
        {
            emulators.Clear(); folders.Clear(); var suite = Suite; if (suite == null) return;
            summary.Text = suite.Kind + (suite.SettingsFile == null ? "" : " settings: " + suite.SettingsFile) + "\nROMs: " + suite.RomsFolder + (Directory.Exists(suite.RomsFolder) ? "" : " (not found)") + "\nBIOS: " + suite.BiosFolder + (Directory.Exists(suite.BiosFolder) ? "" : " (not found)");
            status.Text = "Looking for installed emulators…";
            var candidates = await Task.Run(() => Candidates(suite));
            if (Suite != suite) return;
            foreach (var candidate in candidates)
            {
                bool registered = Registered(candidate) != null;
                var preset = EmulatorCatalog.Find(candidate.Preset).Name;
                var same = preset == "Custom" ? null : library.Emulators.FirstOrDefault(e => EmulatorCatalog.Find(e.Preset).Name == preset);
                emulators.Add(candidate, !registered && same == null, candidate.Name, registered ? "Already in FishBowl" : same != null ? "FishBowl already has " + same.Name : "Not added", candidate.Executable);
            }
            ShowFolders();
            status.Text = candidates.Count + " emulators and " + suite.RomFolders.Count + " ROM folders with games found.";
        }
        private void ShowFolders()
        {
            folders.Clear(); var suite = Suite; if (suite == null) return;
            foreach (var folder in suite.RomFolders)
            {
                var emulator = Suites.EmulatorFor(library, folder.System);
                folders.Add(folder, true, folder.Name, folder.System.Console, folder.Games.ToString(), emulator == null ? "Choose when played" : emulator.Name);
            }
        }

        // EmuDeck installs Flatpaks and AppImages in ~/Applications, which the regular scan finds; its launcher scripts
        // cover the rest. Distribution packages on PATH are not EmuDeck's and stay in Find installed.
        private static List<DiscoveredEmulator> Candidates(SuiteInstall suite)
        {
            if (suite.Kind != "EmuDeck") return suite.Emulators.ToList();
            var applications = Path.Combine(Platform.Home, "Applications") + "/";
            var result = EmulatorDiscovery.ScanSystem(CancellationToken.None).Items.Where(e => Platform.FlatpakId(e.Executable) != null || e.Executable.StartsWith(applications, StringComparison.Ordinal)).ToList();
            result.AddRange(suite.Emulators.Where(e => !result.Any(r => r.Name == e.Name)));
            return result;
        }
        private EmulatorProfile Registered(DiscoveredEmulator candidate)
        {
            var id = Platform.FlatpakId(candidate.Executable);
            return library.Emulators.FirstOrDefault(e => HubPaths.Same(e.Executable, candidate.Executable) || (id != null && Platform.FlatpakId(e.Executable) == id));
        }

        private async Task AddEmulators()
        {
            var suite = Suite; if (suite == null) return; int added = 0;
            foreach (var item in emulators.CheckedItems)
            {
                var candidate = item.Tag as DiscoveredEmulator; if (candidate == null || Registered(candidate) != null) continue;
                var preset = EmulatorCatalog.Find(candidate.Preset);
                var profile = new EmulatorProfile { Id = Guid.NewGuid().ToString("N"), Name = candidate.Name, Preset = candidate.Preset, Executable = Path.GetFullPath(candidate.Executable), Extensions = Suites.ExtensionsFor(preset.Name), LaunchProfiles = new List<LaunchProfile>(), Builds = new List<EmulatorBuild>() };
                if (useBios.IsChecked == true && Directory.Exists(suite.BiosFolder)) profile.FirmwareFolder = suite.BiosFolder;
                BuildRegistry.Remember(profile, profile.Executable, "Installed by " + suite.Kind);
                library.Emulators.Add(profile); added++;
                Table.Uncheck(item);
            }
            if (added == 0) { await Ui.Message(this, "Check the emulators that are not in FishBowl yet."); return; }
            Store.Save(library); Store.Log("Added " + added + " emulators from " + suite.Kind + ".");
            changed(); Load(); status.Text = "Added " + added + " emulators from " + suite.Kind + ".";
        }

        private async Task ImportFolders()
        {
            var suite = Suite; if (suite == null) return;
            var chosen = folders.CheckedItems.Select(i => i.Tag as SuiteRomFolder).Where(f => f != null).ToList();
            if (chosen.Count == 0) { await Ui.Message(this, "Check the ROM folders to import."); return; }
            status.Text = "Reading ROM folders…";
            var games = await Task.Run(() => chosen.SelectMany(f => Suites.PrepareImport(library, f, 5000)).GroupBy(g => g.Path).Select(g => g.First()).ToList());
            if (games.Count == 0) { status.Text = ""; await Ui.Message(this, "Every game in these folders is already in the library."); return; }
            int unassigned = games.Count(g => g.RequiresEmulatorAssignment);
            if (!await Ui.Confirm(this, "Add " + games.Count + (games.Count == 1 ? " game" : " games") + " from " + chosen.Count + " ROM folder(s) in " + suite.RomsFolder + "?\n\nFiles stay where they are." + (unassigned > 0 ? "\n\n" + unassigned + " of them have no emulator in FishBowl yet; FishBowl asks for one when you play them." : ""), "Import ROM folders", "Add games", "Cancel")) { status.Text = ""; return; }
            foreach (var game in games) { GameLibraryRemoval.AllowPath(library, game.Path); library.Games.Add(game); }
            Store.Save(library); Store.Log("Imported " + games.Count + " games from " + suite.Kind + " ROM folders.");
            changed(); status.Text = "Added " + games.Count + " games to the Library.";
            foreach (var item in folders.CheckedItems.ToList()) Table.Uncheck(item);
        }
    }
}
