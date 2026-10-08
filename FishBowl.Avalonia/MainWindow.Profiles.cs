using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private async Task ManageLibraryProfiles()
        {
            var settings = UserTools.Ensure(library);
            var active = settings.Users.FirstOrDefault(u => u.Id == settings.ActiveId);
            var dialog = new FishDialog("Profiles", 560);
            var body = new StackPanel { Spacing = 8 };
            var profiles = Ui.Combo(settings.Users.Select(u => u.Name), active?.Name);
            var name = Ui.Field();
            body.Children.Add(Ui.Hint("Profiles keep favorites, play history, queue, and preferences separate. Games and emulator files are shared."));
            body.Children.Add(Ui.Caption("Profile")); body.Children.Add(profiles);
            body.Children.Add(Ui.Caption("New profile name")); body.Children.Add(name);
            body.Children.Add(Ui.Actions(Ui.Action("Create", () => { var user = UserTools.Create(library, name.Text); Store.Save(library); profiles.ItemsSource = settings.Users.Select(u => u.Name).ToList(); profiles.SelectedItem = user.Name; }),
                Ui.Action("Switch profile", async () => { if (UserTools.ActiveLaunches > 0) { await Ui.Message(dialog, "Close launched games before switching profiles."); return; } var chosen = settings.Users.FirstOrDefault(u => u.Name == profiles.SelectedItem as string); if (chosen == null) return; UserTools.Switch(library, chosen.Id); Store.Save(library); RecoverLibrarySessions(); RefreshGameLibrary(); dialog.Close(); await Ui.Message(this, "Profile switched. Restart FishBowl to apply its appearance preferences."); }),
                Ui.Action("Export profile", async () => { UserTools.SaveActive(library); var user = settings.Users.FirstOrDefault(u => u.Name == profiles.SelectedItem as string); if (user == null) return; var target = await Ui.SaveFile(dialog, "Export profile", "FishBowl profile|*.json", "FishBowl-profile.json"); if (target != null) File.WriteAllText(target, Json.Serialize(user)); }),
                Ui.Action("Import profile", async () => { var file = await Ui.PickFile(dialog, "Import profile", "FishBowl profile|*.json"); if (file == null) return; var user = Json.Deserialize<BowlUser>(File.ReadAllText(file)); if (user == null || String.IsNullOrWhiteSpace(user.Name) || user.Name.Length > 60 || (user.Games ?? new System.Collections.Generic.List<PersonalGame>()).Any(g => g == null || String.IsNullOrWhiteSpace(g.Id))) throw new IOException("The profile is invalid."); if (settings.Users.Any(u => String.Equals(u.Name, user.Name, StringComparison.OrdinalIgnoreCase))) throw new IOException("A profile with that name already exists."); user.Id = Guid.NewGuid().ToString("N"); settings.Users.Add(user); Store.Save(library); profiles.ItemsSource = settings.Users.Select(u => u.Name).ToList(); profiles.SelectedItem = user.Name; })));
            body.Children.Add(Ui.Action("Close", dialog.Close)); dialog.Body = body; await dialog.Present(this);
        }
    }
}
