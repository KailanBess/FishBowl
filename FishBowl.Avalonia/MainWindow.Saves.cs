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
        private async Task ShowLibrarySaveTimeline()
        {
            var game = SelectedLibraryGame(); if (game == null) return;
            if (library.SaveSnapshots == null) library.SaveSnapshots = new List<SaveSnapshot>();
            var dialog = new FishDialog("Save timeline", 740, 560); var body = new StackPanel { Spacing = 8 };
            var snapshots = new ComboBox { ItemsSource = library.SaveSnapshots.Where(s => s.GameId == game.Id).OrderByDescending(s => s.CreatedAt).Select(s => s.CreatedAt + " · " + Path.GetFileName(s.Source) + " · " + s.Id).ToList() };
            Func<SaveSnapshot> selected = () => library.SaveSnapshots.FirstOrDefault(s => (snapshots.SelectedItem as string ?? "").EndsWith(" · " + s.Id, StringComparison.Ordinal));
            var details = Ui.Paragraphs("Select a snapshot to compare it with the current save."); details.MinHeight = 180;
            body.Children.Add(Ui.Hint("Close games before copying or restoring saves. File snapshots include SHA-256 verification. Folder snapshots remain available through emulator backups."));
            body.Children.Add(snapshots); body.Children.Add(details);
            body.Children.Add(Ui.Actions(Ui.Action("Capture save file", async () => { if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before capturing a save."); var file = await Ui.PickFile(dialog, "Save file"); if (file == null) return; var record = await Task.Run(() => CaptureLibrarySaveFile(game, file)); library.SaveSnapshots.Add(record); Store.Save(library); snapshots.ItemsSource = library.SaveSnapshots.Where(s => s.GameId == game.Id).OrderByDescending(s => s.CreatedAt).Select(s => s.CreatedAt + " · " + Path.GetFileName(s.Source) + " · " + s.Id).ToList(); }),
                Ui.Action("Compare", async () => { var record = selected(); if (record == null) return; if (record.IsFolder) { await Ui.Message(dialog, "Use the emulator backup manager for folder snapshots."); return; } var current = File.Exists(record.Source) ? await Task.Run(() => GameTools.Hash(record.Source)) : null; details.Text = record.CreatedAt + "\n" + record.Source + "\nSnapshot bytes: " + record.Bytes + "\n" + (current == null ? "Current save is missing." : current == record.Hash ? "Current save matches this snapshot." : "Current save differs from this snapshot."); }),
                Ui.Action("Restore", async () => { var record = selected(); if (record == null) return; await RestoreLibrarySaveFile(dialog, game, record, record.Source); }),
                Ui.Action("Transfer copy", async () => { var record = selected(); if (record == null) return; var target = await Ui.SaveFile(dialog, "Transfer save copy", "Save files|*", Path.GetFileName(record.Source)); if (target == null) return; await RestoreLibrarySaveFile(dialog, game, record, target); })));
            body.Children.Add(Ui.Action("Close", dialog.Close)); dialog.Body = new ScrollViewer { Content = body }; await dialog.Present(this);
        }

        private SaveSnapshot CaptureLibrarySaveFile(GameEntry game, string source) { return SaveTools.Capture(game, source, Store.DataDirectory); }

        private async Task RestoreLibrarySaveFile(Window owner, GameEntry game, SaveSnapshot record, string target)
        {
            if (LibraryProfiles.ActiveLaunches > 0) throw new IOException("Close launched games before restoring or transferring saves.");
            await Task.Run(() => SaveTools.Verify(record));
            target = Path.GetFullPath(target); if (GameLibraryRemoval.SamePath(target, record.Path)) throw new IOException("Choose a destination outside this snapshot.");
            if (!Directory.Exists(Path.GetDirectoryName(target)) || (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)) throw new IOException("Choose an existing destination folder without symbolic links.");
            string oldHash = await Task.Run(() => SaveTools.CurrentHash(target));
            if (!await Ui.Confirm(owner, "Copy this verified snapshot to:\n" + target + "\n\n" + (oldHash == null ? "The destination does not yet exist." : "The current destination will be backed up before replacement."))) return;
            if ((File.Exists(target) ? GameTools.Hash(target) : null) != oldHash) throw new IOException("The destination changed after review. Review it again.");
            if (oldHash != null) { library.SaveSnapshots.Add(await Task.Run(() => CaptureLibrarySaveFile(game, target))); Store.Save(library); }
            await Task.Run(() => SaveTools.Restore(record, target, oldHash));
            await Ui.Message(owner, "Verified save copy completed.");
        }
    }
}
