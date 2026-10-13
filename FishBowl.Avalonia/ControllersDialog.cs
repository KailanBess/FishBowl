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
    // Tools > Controllers: the game controllers Linux sees, whether this account can read them, and whether each
    // Flatpak emulator may open them, with the one-click override where it cannot.
    public class ControllersDialog : FishDialog
    {
        private readonly LibraryData library;
        private readonly Table devices = new Table("4*,*,2*,2*", false, "Controller", "Connection", "Device", "This account");
        private readonly Table apps = new Table("3*,2*,4*", false, "Flatpak emulator", "Controllers", "Permission");
        private readonly TextBlock deviceHint = Ui.Hint(""), appHint = Ui.Hint("");
        private readonly Button allow;

        public ControllersDialog(LibraryData data) : base("FishBowl — Controllers", 860, 680)
        {
            library = data; MinWidth = 700; MinHeight = 520;
            allow = Ui.Action("Allow controller access…", AllowSelected); allow.IsEnabled = false;
            apps.List.SelectionChanged += delegate { allow.IsEnabled = apps.SelectedTag is FlatpakFix; };
            var top = new StackPanel { Children = { Ui.Hint("Connect controllers before opening an emulator. Button mapping is configured inside each emulator; FishBowl only checks that Linux and the emulator can see them."), Ui.Caption("Connected controllers") } };
            var middle = new StackPanel { Margin = new Thickness(0, 6, 0, 0), Children = { deviceHint, Ui.Caption("Flatpak emulators") } };
            var bottom = new StackPanel { Children = { appHint } };
            var close = Ui.Action("Close", () => Close()); close.IsCancel = true;
            var actions = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(close, Dock.Right); close.Margin = new Thickness(0);
            actions.Children.Add(close); actions.Children.Add(Ui.Actions(Ui.Action("Refresh", Load), allow));
            bottom.Children.Add(actions);
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,*,Auto") };
            Grid.SetRow(top, 0); Grid.SetRow(devices, 1); Grid.SetRow(middle, 2); Grid.SetRow(apps, 3); Grid.SetRow(bottom, 4);
            grid.Children.Add(top); grid.Children.Add(devices); grid.Children.Add(middle); grid.Children.Add(apps); grid.Children.Add(bottom);
            Body = grid; Opened += async delegate { await Load(); };
        }

        private async Task Load()
        {
            devices.Clear(); apps.Clear(); allow.IsEnabled = false; appHint.Text = "Reading Flatpak permissions…";
            var found = await Task.Run(() => ControllerInventory.Scan());
            foreach (var device in found)
                devices.Add(device, false, device.Name, device.BusName, "/dev/input/" + device.Event + (device.Joystick == null ? "" : " · " + device.Joystick), device.Readable ? "Readable" : "No access");
            deviceHint.Text = found.Count == 0 ? "No game controller is connected (or Linux does not report it as a gamepad). Connect it by USB or pair it over Bluetooth, then Refresh."
                : found.All(d => d.Readable) ? found.Count + " controller(s) connected. Native emulators can open them."
                : "Some controllers are not readable by this account. Desktops normally grant the signed-in user access to game controllers through systemd-logind; Steam's udev rules (the steam-devices or game-devices-udev package) cover many controllers that are missing.";
            var flatpaks = library.Emulators.Where(e => Platform.FlatpakId(e.Executable) != null).ToList();
            var rows = await Task.Run(() => flatpaks.Select(e =>
            {
                var id = Platform.FlatpakId(e.Executable); string error;
                var permissions = FlatpakTool.Permissions(id, FlatpakTool.Installation(e.Executable, id), out error);
                var version = FlatpakTool.InstalledVersion();
                return Tuple.Create(e, id, permissions == null ? null : FlatpakSetup.ControllerRow(id, permissions.ControllerAccess(version), version), error);
            }).ToList());
            foreach (var row in rows)
            {
                if (row.Item3 == null) { apps.Add(null, false, row.Item1.Name, "Unknown", "flatpak info could not read " + row.Item2 + ": " + row.Item4); continue; }
                apps.Add(row.Item3.Fix, false, row.Item1.Name, row.Item3.Status, row.Item3.Detail);
            }
            appHint.Text = flatpaks.Count == 0 ? "No emulator in FishBowl is a Flatpak. Native programs and AppImages use your account's access."
                : rows.Any(r => r.Item3 != null && r.Item3.Fix != null) ? "Select a blocked emulator and choose Allow controller access. FishBowl shows the exact flatpak override command first." : "Every Flatpak emulator may open controllers.";
        }

        private async Task AllowSelected()
        {
            var fix = apps.SelectedTag as FlatpakFix; if (fix == null) return;
            var profile = library.Emulators.FirstOrDefault(e => Platform.FlatpakId(e.Executable) == fix.AppId); if (profile == null) return;
            if (await FlatpakUi.ApplyFix(this, library, profile, fix)) await Load();
        }
    }
}
