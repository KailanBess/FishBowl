using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmulatorHub
{
    // Linux additions to Emulator management: one-click Flatpak overrides in Setup checks, and Flatpak/AppImage updates.
    public partial class EmulatorManagerDialog
    {
        // ----- Setup checks: Flatpak sandbox ---------------------------------------------------------------------------

        private readonly WrapPanel flatpakBar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4), IsVisible = false };

        private Control FlatpakBar() { return flatpakBar; }

        private void ShowFlatpakFixes(EmulatorProfile shown, List<HealthCheck> checks)
        {
            flatpakBar.Children.Clear();
            flatpakBar.IsVisible = !Platform.IsWindows && shown != null && Platform.FlatpakId(shown.Executable) != null;
            if (!flatpakBar.IsVisible) return;
            var label = Ui.Text("Flatpak sandbox:", 12.5, true); label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new Thickness(0, 3, 10, 3);
            flatpakBar.Children.Add(label);
            foreach (var fix in (checks ?? new List<HealthCheck>()).OfType<FlatpakHealthCheck>().Where(c => c.Fix != null).Select(c => c.Fix).GroupBy(f => f.Option).Select(g => g.First()))
            {
                var chosen = fix; var button = Button(fix.Label, async () => { if (await FlatpakUi.ApplyFix(this, library, Selected(), chosen)) { progress.Text = "Permission added. Checking again…"; RefreshHealth(); } });
                ToolTip.SetTip(button, fix.Device ? "flatpak override --user --device=… " + fix.AppId : fix.Command);
                flatpakBar.Children.Add(button);
            }
            flatpakBar.Children.Add(Button("Flatpak permissions…", async () => { await new FlatpakPermissionsDialog(library, Selected()).Present(this); RefreshHealth(); }));
            foreach (var child in flatpakBar.Children.OfType<Button>()) child.Margin = new Thickness(0, 3, 8, 3);
        }

        // ----- Updates: how this emulator is installed --------------------------------------------------------------

        private readonly TextBlock installText = Ui.Hint("");
        private Button flatpakCheck, flatpakUpdate, appImageUpdate;
        private InstallInfo install;

        private Control InstallUpdateBar()
        {
            flatpakCheck = Button("Check Flatpak update", CheckFlatpakUpdate);
            flatpakUpdate = Button("Update Flatpak", UpdateFlatpak);
            appImageUpdate = Button("Update AppImage", UpdateAppImage);
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 4), IsVisible = !Platform.IsWindows };
            panel.Children.Add(installText); panel.Children.Add(Ui.Actions(flatpakCheck, flatpakUpdate, appImageUpdate));
            ShowInstallButtons();
            return panel;
        }
        private void ShowInstallButtons()
        {
            var kind = install == null ? InstallKind.Other : install.Kind;
            flatpakCheck.IsVisible = flatpakUpdate.IsVisible = kind == InstallKind.Flatpak;
            appImageUpdate.IsVisible = kind == InstallKind.AppImage;
        }
        private void RefreshInstallInfo(EmulatorProfile snapshot)
        {
            install = null; if (flatpakCheck != null) ShowInstallButtons();
            if (Platform.IsWindows) return;
            installText.Text = snapshot == null ? "" : "Installation: checking…";
            if (snapshot == null) return;
            Task.Run(() => InstallUpdates.Inspect(snapshot)).ContinueWith(t => Ui.Post(() =>
            {
                if (profile != snapshot || t.IsFaulted) { if (t.IsFaulted) installText.Text = "Installation: unknown."; return; }
                install = t.Result; ShowInstallButtons(); installText.Text = Describe(install);
                // An AppImage that names its GitHub releases fills an empty repository override.
                var repo = install.UpdateInfo == null ? null : install.UpdateInfo.Repository;
                if (repo != null && String.IsNullOrWhiteSpace(repository.Text) && EmulatorUpdates.Repository(snapshot).Length == 0) repository.Text = repo;
            }));
        }
        private static string Describe(InstallInfo info)
        {
            switch (info.Kind)
            {
                case InstallKind.Flatpak: return "Installation: Flatpak " + info.FlatpakId + " (" + info.Installation + "). FishBowl can check for and install its update with flatpak update.";
                case InstallKind.AppImage:
                    return "Installation: AppImage " + info.AppImage + "\n" + (info.UpdateInfo == null ? "This AppImage has no built-in update information; use Official downloads." : "Updates from " + info.UpdateInfo.Describe() + ".")
                        + (info.UpdateInfo != null && info.Tool == null ? " Install appimageupdatetool (or AppImageUpdate) to update it from FishBowl." : "");
                case InstallKind.Package: return "Installation: " + info.Package.Manager + " package " + info.Package.Name + " " + info.Package.Version + (info.Package.Foreign ? " (AUR or local package)" : "") + ". Update it with your system package manager" + (info.Package.Foreign ? " or AUR helper." : ".");
                default: return "Installation: program files. Replace them with a newer official build, then use Versions to switch.";
            }
        }

        private async Task CheckFlatpakUpdate()
        {
            var info = install; if (info == null || info.Kind != InstallKind.Flatpak) return;
            await RunAsync(async () =>
            {
                progress.Text = "Asking Flatpak for updates to " + info.FlatpakId + "…";
                string version = null, error = null;
                var available = await Task.Run(() => InstallUpdates.FlatpakUpdateAvailable(info.FlatpakId, info.Installation, out version, out error));
                var installed = await Task.Run(() => InstallUpdates.FlatpakVersion(info.FlatpakId, info.Installation));
                progress.Text = error != null ? "Flatpak could not check for updates: " + error
                    : available ? "An update is available for " + info.FlatpakId + (version == null ? "" : " (" + version + ")") + ". Use Update Flatpak."
                    : info.FlatpakId + " is up to date" + (installed == null ? "" : " (" + installed + ")") + ".";
            });
        }

        private async Task UpdateFlatpak()
        {
            var selected = Selected(); var info = install; if (info == null || info.Kind != InstallKind.Flatpak) return;
            var arguments = InstallUpdates.FlatpakUpdateArguments(info.FlatpakId, info.Installation);
            if (!await Ui.Confirm(this, "Update " + selected.Name + " with Flatpak?\n\nFishBowl will run:\n" + FlatpakTool.CommandLine("flatpak", arguments) + (info.Installation == "system" ? "\n\nSystem installations may ask for your password." : "") + "\n\nClose " + selected.Name + " first.", "Update Flatpak", "Update", "Cancel")) return;
            RequireStopped(selected);
            await RunProgram("flatpak", arguments, "Updating " + info.FlatpakId + "…");
            RefreshUpdates();
        }

        private async Task UpdateAppImage()
        {
            var selected = Selected(); var info = install; if (info == null || info.Kind != InstallKind.AppImage) return;
            if (info.UpdateInfo == null) { await Ui.Message(this, Path.GetFileName(info.AppImage) + " has no built-in update information, so it cannot update itself. Download the new AppImage from the official page, then use Versions to switch."); return; }
            if (info.Tool == null) { await Ui.Message(this, "Updating AppImages needs appimageupdatetool or AppImageUpdate from the AppImage project (github.com/AppImageCommunity/AppImageUpdate). Put it in ~/Applications or on your PATH, then try again.\n\nThis AppImage updates from " + info.UpdateInfo.Describe() + "."); return; }
            if (!await Ui.Confirm(this, "Update " + selected.Name + "?\n\nFishBowl will run:\n" + FlatpakTool.CommandLine(info.Tool, new[] { info.AppImage }) + "\n\nThe new AppImage is downloaded next to the current one, which is kept. Close " + selected.Name + " first.", "Update AppImage", "Update", "Cancel")) return;
            RequireStopped(selected);
            var folder = Path.GetDirectoryName(info.AppImage); var before = InstallUpdates.Snapshot(folder);
            var log = await RunProgram(info.Tool, new[] { info.AppImage }, "Updating " + Path.GetFileName(info.AppImage) + "…");
            var created = InstallUpdates.NewFileFromLog(log) ?? InstallUpdates.ChangedAppImage(before, folder);
            if (created == null || !File.Exists(created) || HubPaths.Same(created, info.AppImage)) { RefreshUpdates(); return; }
            Platform.MakeExecutable(created);
            if (await Ui.Confirm(this, "The updated AppImage is:\n" + created + "\n\nUse it for " + selected.Name + "? The previous AppImage stays registered under Versions.", "Update AppImage", "Use new AppImage", "Keep current"))
            {
                BuildRegistry.Remember(selected, created, "Updated AppImage");
                BuildRegistry.Activate(selected, selected.Builds.First(b => HubPaths.Same(b.Executable, created)));
                Store.Save(library); RefreshSelected(); progress.Text = selected.Name + " now uses " + Path.GetFileName(created) + ".";
            }
        }

        // Runs an updater and shows its output live in the Updates text; returns the complete output.
        private async Task<string> RunProgram(string program, string[] arguments, string status)
        {
            var log = new TerminalLog(); var all = new System.Text.StringBuilder(); int exit = -1;
            await RunAsync(async () =>
            {
                progress.Text = status; updateText.Text = "$ " + FlatpakTool.CommandLine(program, arguments) + "\n";
                var header = updateText.Text;
                exit = await Task.Run(() => ProcessStream.Run(program, arguments, chunk => { lock (all) all.Append(chunk); Ui.Post(() => { log.Append(chunk); updateText.Text = header + log; updateText.CaretIndex = updateText.Text.Length; }); }, cancellation.Token));
                progress.Text = exit == 0 ? "Finished." : "The updater stopped with exit code " + exit + ". Its output is shown above.";
                Store.Log("Updater " + program + " " + String.Join(" ", arguments) + " exited with " + exit);
            });
            lock (all) return exit == 0 ? all.ToString() : "";
        }
    }
}
