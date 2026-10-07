using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private async Task ShowLibraryGameTools()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            if (game.Tools == null) game.Tools = new GameToolsSettings();
            var tools = game.Tools; var dialog = new FishDialog("Game setup", 720, 720); var body = new StackPanel { Spacing = 8 };
            var manual = Ui.Field(game.ManualPath); var notes = Ui.Paragraphs(game.CompatibilityNotes, false); notes.MinHeight = 80;
            var emulator = library.Emulators.FirstOrDefault(e => e.Id == (game.PreferredEmulatorId ?? game.EmulatorId));
            var launch = Ui.Combo(new[] { "Default" }.Concat((emulator?.LaunchProfiles ?? new List<LaunchProfile>()).Select(p => p.Name)), game.LaunchProfileName ?? "Default");
            var builds = emulator?.Builds ?? new List<EmulatorBuild>();
            var buildNames = new[] { "Active build" }.Concat(builds.Select(b => b.Label + " · " + b.Id.Substring(0, Math.Min(6, b.Id.Length)))).ToArray();
            var build = Ui.Combo(buildNames, buildNames[builds.FindIndex(b => b.Id == game.PreferredBuildId) + 1]);
            var controller = Ui.Combo(new[] { "None" }.Concat((library.ControllerProfiles ?? new List<ControllerProfile>()).Select(p => p.Name)), (library.ControllerProfiles ?? new List<ControllerProfile>()).FirstOrDefault(p => p.Id == game.ControllerProfileId)?.Name ?? "None");
            body.Children.Add(Ui.Caption("Emulator build")); body.Children.Add(build); body.Children.Add(Ui.Caption("Launch profile")); body.Children.Add(launch); body.Children.Add(Ui.Caption("Controller profile")); body.Children.Add(controller);
            body.Children.Add(Ui.Caption("Manual")); body.Children.Add(Ui.WithButton(manual, Ui.Action("Browse", async () => { var file = await Ui.PickFile(dialog, "Game manual"); if (file != null) manual.Text = file; })));
            body.Children.Add(Ui.Caption("Setup and compatibility notes")); body.Children.Add(notes);
            body.Children.Add(Ui.Caption("Media gallery"));
            foreach (var media in tools.MediaLinks ?? new List<GameMediaLink>()) { var item = media; body.Children.Add(Ui.Action(item.Label, () => Platform.Open(item.Path))); }
            body.Children.Add(Ui.Action("Add media", async () => { var file = await Ui.PickFile(dialog, "Screenshot, video, or manual"); if (file == null) return; if (tools.MediaLinks == null) tools.MediaLinks = new List<GameMediaLink>(); tools.MediaLinks.Add(new GameMediaLink { Label = Path.GetFileName(file), Path = file, Kind = Path.GetExtension(file) }); Store.Save(library); await Ui.Message(dialog, "Media added. Reopen Game setup to view it."); }));
            body.Children.Add(Ui.Caption("Mod profiles")); var modChoice = new ComboBox { ItemsSource = tools.ModProfiles, SelectedIndex = tools.ModProfiles.Count > 0 ? 0 : -1 };
            body.Children.Add(modChoice);
            body.Children.Add(Ui.Actions(Ui.Action("Add mod profile", async () => { var source = await Ui.PickFolder(dialog, "Mod files folder"); if (source == null) return; var destination = await Ui.PickFolder(dialog, "Game destination folder"); if (destination == null) return; var profile = new ModProfile { Id = Guid.NewGuid().ToString("N"), Name = Path.GetFileName(source), SourceFolder = source, DestinationFolder = destination }; tools.ModProfiles.Add(profile); Store.Save(library); modChoice.ItemsSource = tools.ModProfiles.ToList(); modChoice.SelectedItem = profile; }),
                Ui.Action("Review and apply", async () => { var profile = modChoice.SelectedItem as ModProfile; if (profile == null) return; var preview = await Task.Run(() => GameTools.PreviewMod(profile.SourceFolder, profile.DestinationFolder)); await new ResultsDialog("Mod preview", preview.Select(f => f.RelativePath + (f.Existed ? " · replace with backup" : " · add"))).Present(dialog); if (!await Ui.Confirm(dialog, "Apply these " + preview.Count + " reviewed mod files? Existing files receive rollback backups.")) return; try { await Task.Run(() => GameTools.ApplyMod(profile, preview, Path.Combine(Store.DataDirectory, "ModBackups"), () => Store.Save(library))); } finally { Store.Save(library); } }),
                Ui.Action("Roll back", async () => { var profile = modChoice.SelectedItem as ModProfile; if (profile == null) return; GameTools.ValidateRollback(profile); if (!await Ui.Confirm(dialog, "Roll back " + profile.Name + " using its validated backups?")) return; await Task.Run(() => GameTools.RollbackMod(profile, () => Store.Save(library))); Store.Save(library); })));
            body.Children.Add(Ui.Action("Play history", () => new ResultsDialog("Play history", GameTools.Monthly(library.PlaySessions).Select(m => m.Month + " · " + TimeSpan.FromSeconds(m.Seconds).TotalHours.ToString("0.0") + " hours · " + m.Sessions + " sessions · " + m.Games + " games")).Present(dialog)));
            body.Children.Add(dialog.Footer("Save", () => { game.ManualPath = manual.Text; game.CompatibilityNotes = notes.Text; game.PreferredBuildId = build.SelectedIndex > 0 ? builds[build.SelectedIndex - 1].Id : null; game.LaunchProfileName = launch.SelectedIndex > 0 ? launch.SelectedItem as string : null; game.ControllerProfileId = (library.ControllerProfiles ?? new List<ControllerProfile>()).FirstOrDefault(p => p.Name == controller.SelectedItem as string)?.Id; Store.Save(library); RefreshGameLibrary(); return Task.FromResult(true); }));
            dialog.Body = new ScrollViewer { Content = body }; await dialog.Present(this);
        }
    }
}
