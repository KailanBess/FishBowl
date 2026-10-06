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
        private async Task ImportSteamLibraryGame()
        {
            var folder = await Ui.PickFolder(this, "Steam library folder (containing steamapps)"); if (folder == null) return;
            var entries = await Task.Run(() => GameTools.SteamGames(folder));
            var dialog = new FishDialog("Import installed Steam game", 650); var body = new StackPanel { Spacing = 8 };
            var choice = new ComboBox { ItemsSource = entries, SelectedIndex = entries.Count == 0 ? -1 : 0 }; body.Children.Add(choice);
            body.Children.Add(Ui.Hint("Select an installed game, then choose its Linux program or launcher. FishBowl keeps the existing game files."));
            body.Children.Add(dialog.Footer("Import game", async () => { var entry = choice.SelectedItem as InstalledSteamGame; if (entry == null) return false; var program = await Ui.PickFile(dialog, "Game program or launcher", "Programs|*", entry.Folder); if (program == null) return false;
                if (!Platform.IsLaunchFile(program)) throw new IOException("Choose an executable game program or launcher.");
                if (library.Games.Any(g => GameLibraryRemoval.SamePath(g.Path, program))) throw new IOException("That game program is already in the library.");
                var game = GameTools.NativeEntry(program, entry.Title); GameLibraryRemoval.AllowPath(library, program); library.Games.Add(game);
                if (library.GameTools == null) library.GameTools = new GameToolsLibrarySettings(); if (library.GameTools.SteamLibraryFolders == null) library.GameTools.SteamLibraryFolders = new List<string>();
                if (!library.GameTools.SteamLibraryFolders.Contains(folder)) library.GameTools.SteamLibraryFolders.Add(folder);
                Store.Save(library); RefreshGameLibrary(); return true; }));
            dialog.Body = body; await dialog.Present(this);
        }
    }
}
