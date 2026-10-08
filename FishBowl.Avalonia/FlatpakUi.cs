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
    // Runs the Flatpak permission changes FishBowl offers, always after showing the exact command or file edit.
    public static class FlatpakUi
    {
        // Returns true when an override was added.
        public static async Task<bool> ApplyFix(Window owner, LibraryData library, EmulatorProfile profile, FlatpakFix fix)
        {
            if (fix.Device)
            {
                var input = FlatpakFix.Controllers(fix.AppId, "input"); var all = FlatpakFix.Controllers(fix.AppId, "all");
                var version = FlatpakTool.InstalledVersion();
                if (version != null && version < FlatpakPermissions.InputDeviceVersion)
                {
                    if (!await Ui.Confirm(owner, "Let " + profile.Name + " use game controllers?\n\nFishBowl will run:\n" + all.Command + "\n\nThis gives the app access to all devices, which is what most emulator Flatpaks ask for. It applies to your user account the next time " + profile.Name + " starts. Flatpak " + version + " has no narrower controller permission.", "Allow controller access", "Allow all devices", "Cancel")) return false;
                    fix = all;
                }
                else
                {
                    var choice = await Ui.Ask(owner, "Let " + profile.Name + " use game controllers?\n\nInput devices only:\n" + input.Command + "\n\nAll devices:\n" + all.Command + "\n\nInput devices covers controllers in /dev/input. Some adapters, motion sensors and controllers read through raw HID need all devices, which is what most emulator Flatpaks ask for. Either applies to your user account the next time " + profile.Name + " starts.",
                        "Allow controller access", "Allow input devices", "Allow all devices", "Cancel");
                    if (choice == "Allow input devices") fix = input; else if (choice == "Allow all devices") fix = all; else return false;
                }
            }
            else if (!await Ui.Confirm(owner, "Let " + profile.Name + " use this folder?\n\n" + fix.Folder + "\n\nFishBowl will run:\n" + fix.Command + "\n\n" + (fix.Option.EndsWith(":ro") ? "Read-only is enough for firmware and games. " : fix.Option.EndsWith(":create") ? "Flatpak creates the folder when the emulator starts. " : "The emulator can read and write there. ") + "This applies to your user account the next time " + profile.Name + " starts. You can remove it again from Flatpak permissions.", "Allow folder access", "Allow access", "Cancel"))
                return false;
            var result = await Task.Run(() => FlatpakTool.Run("flatpak", fix.Arguments));
            if (!result.Ok) { await Ui.Message(owner, "Flatpak did not accept the change.\n\n" + fix.Command + "\n\n" + result.Message); return false; }
            var state = LinuxIntegrationState.Load(); state.AddGrant(fix.AppId, fix.Grant); state.Save();
            Store.Log("Flatpak override for " + profile.Name + ": " + fix.Command);
            return true;
        }

        // Removes one override FishBowl added by editing that line of the user override file; everything else stays.
        public static async Task<bool> RemoveGrant(Window owner, LibraryData library, EmulatorProfile profile, string grant)
        {
            var id = Platform.FlatpakId(profile.Executable); if (id == null) return false;
            int eq = grant.IndexOf('='); var key = grant.Substring(0, eq); var entry = grant.Substring(eq + 1);
            var file = FlatpakTool.OverrideFile(id);
            var before = File.Exists(file) ? File.ReadAllText(file) : "";
            var after = FlatpakTool.RemoveOverrideEntry(before, key, entry);
            var state = LinuxIntegrationState.Load();
            if (after == null)
            {
                state.RemoveGrant(id, grant); state.Save();
                await Ui.Message(owner, entry + " is no longer in your overrides for " + id + ", so there is nothing to remove. FishBowl has forgotten it.");
                return true;
            }
            Func<string, string> line = text => text.Split('\n').FirstOrDefault(l => l.StartsWith(key + "=")) ?? "(line removed)";
            if (!await Ui.Confirm(owner, "Remove the permission FishBowl added for " + profile.Name + "?\n\nFishBowl will edit " + file + ":\n\n" + line(before) + "\n→ " + line(after) + "\n\nYour other overrides stay as they are. The change applies the next time " + profile.Name + " starts.", "Remove Flatpak permission", "Remove", "Cancel")) return false;
            var temporary = file + ".fishbowl-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, after); File.Move(temporary, file, true);
            state.RemoveGrant(id, grant); state.Save(); Store.Log("Removed Flatpak override for " + profile.Name + ": " + grant + " from " + file);
            return true;
        }
    }

    // What a Flatpak emulator can use now, where each permission comes from, and removal of the ones FishBowl added.
    public class FlatpakPermissionsDialog : FishDialog
    {
        private readonly LibraryData library; private readonly EmulatorProfile profile; private readonly string id;
        private readonly Table rows = new Table("3*,3*,2*", false, "Permission", "Comes from", "Access");
        private readonly TextBlock summary = Ui.Hint("Reading permissions…");
        private readonly Button remove;
        public FlatpakPermissionsDialog(LibraryData data, EmulatorProfile selected) : base("Flatpak permissions — " + selected.Name, 780, 540)
        {
            library = data; profile = selected; id = Platform.FlatpakId(selected.Executable) ?? "";
            remove = Ui.Action("Remove FishBowl permission", async () => { var grant = rows.SelectedTag as string; if (grant != null && await FlatpakUi.RemoveGrant(this, library, profile, grant)) await Load(); });
            remove.IsEnabled = false; rows.List.SelectionChanged += delegate { remove.IsEnabled = rows.SelectedTag is string; };
            var close = Ui.Action("Close", () => Close()); close.IsCancel = true;
            var top = new StackPanel { Children = { summary, Ui.Hint("FishBowl only adds per-user overrides (flatpak override --user) and only removes the ones it added. Use Flatseal or flatpak override --user --reset " + id + " for anything else.") } };
            var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(close, Dock.Right); close.Margin = new Thickness(0);
            bottom.Children.Add(close); bottom.Children.Add(Ui.Actions(remove, Ui.Action("Refresh", Load)));
            var layout = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom);
            layout.Children.Add(top); layout.Children.Add(bottom); layout.Children.Add(rows);
            Body = layout; Opened += async delegate { await Load(); };
        }
        private async Task Load()
        {
            var installation = FlatpakTool.Installation(profile.Executable, id); string error = null; string overrides = null; Version version = null;
            var permissions = await Task.Run(() => { version = FlatpakTool.InstalledVersion(); overrides = FlatpakTool.UserOverrides(id); return FlatpakTool.Permissions(id, installation, out error); });
            rows.Clear();
            if (permissions == null) { summary.Text = "flatpak info could not read " + id + ": " + error; return; }
            var mine = FlatpakPermissions.Parse(overrides ?? ""); var grants = LinuxIntegrationState.Load().Grants(id);
            summary.Text = id + " · " + installation + " installation" + (version == null ? "" : " · Flatpak " + version) + "\nYour override file: " + FlatpakTool.OverrideFile(id);
            Func<string, string, string> source = (key, entry) =>
            {
                var name = key == "filesystems" ? FlatpakFilesystem.Parse(entry).Name : entry;
                var inOverride = mine.Values(key).Any(e => (key == "filesystems" ? FlatpakFilesystem.Parse(e).Name : e) == name);
                if (!inOverride) return "App or system";
                return grants.Any(g => g.StartsWith(key + "=") && (key == "filesystems" ? FlatpakFilesystem.Parse(g.Substring(key.Length + 1)).Name : g.Substring(key.Length + 1)) == name) ? "Added by FishBowl" : "Your override";
            };
            Func<string, string, string> grantFor = (key, entry) => grants.FirstOrDefault(g => g.StartsWith(key + "=") && (key == "filesystems" ? FlatpakFilesystem.Parse(g.Substring(key.Length + 1)).Name == FlatpakFilesystem.Parse(entry).Name : g.Substring(key.Length + 1) == entry));
            foreach (var fs in permissions.Values("filesystems").Concat(mine.Values("filesystems").Where(e => e.StartsWith("!"))))
            {
                var parsed = FlatpakFilesystem.Parse(fs); var from = source("filesystems", fs);
                rows.Add(from == "Added by FishBowl" ? grantFor("filesystems", fs) : null, false, "Folder: " + parsed.Name, from, parsed.Negated ? "Hidden" : parsed.Mode == SandboxAccess.ReadOnly ? "Read-only" : parsed.Create ? "Read-write (created)" : "Read-write");
            }
            foreach (var device in permissions.Devices.Where(d => !d.StartsWith("if:") && !d.StartsWith("!")).Distinct())
            {
                var from = source("devices", device);
                rows.Add(from == "Added by FishBowl" ? grantFor("devices", device) : null, false, "Devices: " + device, from, permissions.DeviceAllowed(device, version) ? "Allowed" : "Not used by this Flatpak version");
            }
            foreach (var grant in grants.Where(g => !rows.List.Items.OfType<ListBoxItem>().Any(i => (i.Tag as string) == g)))
                rows.Add(grant, false, grant.Replace("filesystems=", "Folder: ").Replace("devices=", "Devices: "), "Added by FishBowl", "Not in effect");
            remove.IsEnabled = false;
        }
    }
}
