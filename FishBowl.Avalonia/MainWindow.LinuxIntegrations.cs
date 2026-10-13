using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace EmulatorHub
{
    // Linux desktop integrations: Add to Steam, EmuDeck/RetroDECK import, the controllers panel, and the
    // --launch-game / --launch-emulator arguments those shortcuts use (the same ones as the Windows Jump List).
    public partial class MainWindow
    {
        private IEnumerable<object> LinuxToolItems()
        {
            if (Platform.IsWindows) return new object[0];
            return new object[] {
                new Separator(),
                MenuAction("Add to Steam...", "controller", () => ShowSteamShortcuts(null)),
                MenuAction("EmuDeck / RetroDECK import...", "import", ShowSuiteImport),
                MenuAction("Controllers...", "controller", ShowControllers) };
        }
        private IEnumerable<object> LinuxLibraryItems()
        {
            if (Platform.IsWindows) return new object[0];
            return new object[] { MenuAction("Add selected game to Steam...", "controller", () => { var game = SelectedLibraryGame(); return ShowSteamShortcuts(game == null ? null : "game:" + game.Id); }) };
        }

        private async Task ShowSteamShortcuts(string preselect)
        {
            SaveProfileNotes();
            string argument; var program = SelfStart(out argument).FileName;
            var dialog = new SteamShortcutsDialog(library, program, argument, preselect == null ? null : new[] { preselect }); await dialog.Present(this);
            if (dialog.Confirmed) SetStatus("Steam shortcuts saved. Start Steam again to see them.");
        }
        private async Task ShowSuiteImport()
        {
            SaveProfileNotes();
            await new SuiteImportDialog(library, () => { RefreshHub(); RefreshGameLibrary(); }).Present(this);
        }
        private async Task ShowControllers() { await new ControllersDialog(library).Present(this); }

        // Steam (and desktop) shortcuts start FishBowl with --launch-game <id> or --launch-emulator <id>.
        private async Task HandleExternalLaunch(string[] args)
        {
            int index = Array.IndexOf(args, "--launch-game");
            if (index >= 0 && index + 1 < args.Length)
            {
                var game = library.Games.FirstOrDefault(g => g != null && g.Id == args[index + 1]);
                if (game == null) await Ui.Message(this, "This shortcut's game is no longer in your library.");
                else await Ui.Run(this, () => LaunchGame(game));
            }
            index = Array.IndexOf(args, "--launch-emulator");
            if (index >= 0 && index + 1 < args.Length)
            {
                var emulator = library.Emulators.FirstOrDefault(e => e.Id == args[index + 1]);
                if (emulator == null) await Ui.Message(this, "This shortcut's emulator is no longer in FishBowl.");
                else { ShowEmulatorInHub(emulator); selectedEmulatorId = emulator.Id; await Ui.Run(this, OpenSelectedEmulator); }
            }
        }
    }
}
