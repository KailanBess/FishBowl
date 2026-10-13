using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Path = System.IO.Path;

namespace EmulatorHub
{
    // Tools > Add to Steam: non-Steam shortcuts that start FishBowl --launch-game <id>, --launch-emulator <id> or
    // --living-room, so games appear in Big Picture and gaming mode with Steam Input and their covers.
    // Checked rows are kept in Steam; unchecking a FishBowl shortcut removes it. Other shortcuts are never changed.
    public class SteamShortcutsDialog : FishDialog
    {
        private readonly LibraryData library;
        private readonly string program, leadingArgument;
        private readonly List<SteamAccount> accounts;
        private readonly ComboBox accountBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32 };
        private readonly TextBlock steamState = Ui.Text("", 12.5), commandText = Ui.Hint("");
        private readonly Ellipse dot = new Ellipse();
        private readonly Table rows = new Table("4*,2*,2*", true, "Shortcut", "Kind", "In Steam");
        private readonly HashSet<string> preselect = new HashSet<string>();
        private Dictionary<string, VdfNode> existing = new Dictionary<string, VdfNode>();
        private Button save;

        public SteamShortcutsDialog(LibraryData data, string program, string leadingArgument, IEnumerable<string> preselectKeys) : base("FishBowl — Add to Steam", 820, 680)
        {
            library = data; this.program = program; this.leadingArgument = leadingArgument; MinWidth = 680; MinHeight = 540;
            foreach (var key in preselectKeys ?? new string[0]) preselect.Add(key);
            accounts = SteamLocator.Find(Platform.Home).SelectMany(i => i.Accounts).ToList();
            var top = new StackPanel();
            top.Children.Add(Ui.Hint("Each shortcut starts FishBowl, which opens the game (or emulator) with its FishBowl launch settings. In Big Picture and Steam Deck gaming mode, Steam Input applies. Game covers become the Steam library artwork. Checked rows are kept in Steam; uncheck a FishBowl shortcut to remove it."));
            top.Children.Add(Ui.Caption("Steam account"));
            accountBox.ItemsSource = accounts; accountBox.SelectionChanged += delegate { LoadAccount(); };
            top.Children.Add(accounts.Count > 0 ? (Control)accountBox : Ui.Text("Steam was not found. FishBowl looks in ~/.local/share/Steam, ~/.steam, Flatpak Steam (~/.var/app/com.valvesoftware.Steam) and Snap Steam (~/snap/steam).", 13));
            dot.Width = dot.Height = 8; dot.VerticalAlignment = VerticalAlignment.Center; dot.Margin = new Thickness(0, 0, 8, 0);
            var check = Ui.Action("Check again", () => RefreshSteamState()); check.Margin = new Thickness(12, 0, 0, 0);
            var state = new DockPanel { Margin = new Thickness(0, 10, 0, 4) };
            DockPanel.SetDock(dot, Dock.Left); DockPanel.SetDock(check, Dock.Right); steamState.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(dot); state.Children.Add(check); state.Children.Add(steamState);
            top.Children.Add(state); top.Children.Add(commandText);
            var footer = Footer("Save to Steam", SaveToSteam);
            save = footer is StackPanel panel ? panel.Children.OfType<Button>().FirstOrDefault(b => Ui.ActionText(b) == "Save to Steam") : null;
            var layout = new DockPanel(); DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(top); layout.Children.Add(footer); layout.Children.Add(rows);
            Body = layout;
            if (accounts.Count > 0) accountBox.SelectedIndex = 0; else LoadAccount();
            RefreshSteamState();
        }

        private SteamAccount Account { get { return accountBox.SelectedItem as SteamAccount; } }

        private void RefreshSteamState()
        {
            bool running = SteamLocator.IsRunning();
            dot.Fill = new SolidColorBrush(running ? Palette.Rgb(246, 183, 105) : Ui.P.IsDark ? Palette.Rgb(118, 211, 161) : Palette.Rgb(35, 126, 82));
            steamState.Text = running ? "Steam is running. Exit Steam (Steam > Exit) before saving: it rewrites its shortcut list when it closes." : "Steam is not running. Changes can be saved.";
        }

        private void LoadAccount()
        {
            rows.Clear(); existing.Clear(); var account = Account;
            if (account != null)
            {
                try { existing = SteamShortcuts.Existing(SteamShortcuts.Load(account.ShortcutsFile)); }
                catch (Exception error) { commandText.Text = "FishBowl cannot read this account's shortcut list, so it will not change it.\n" + error.Message; if (save != null) save.IsEnabled = false; return; }
                string exe, dir, options; SteamShortcuts.Command(account.Install, program, leadingArgument, "--launch-game <game>", out exe, out dir, out options);
                commandText.Text = "Steam will run: " + exe.Trim('"') + " " + options + "\nShortcut list: " + account.ShortcutsFile;
                if (account.Install.Kind == "Flatpak")
                    commandText.Text += "\nFlatpak Steam runs shortcuts in its sandbox, so they start FishBowl through flatpak-spawn. Allow that once with:\nflatpak override --user --talk-name=org.freedesktop.Flatpak com.valvesoftware.Steam";
                else if (account.Install.Kind == "Snap") commandText.Text += "\nSnap Steam is confined and may not be able to start programs outside the snap.";
                if (AppContext.BaseDirectory.Contains("/bin/Debug/") || AppContext.BaseDirectory.Contains("/bin/Release/")) commandText.Text += "\nFishBowl is running from a build folder. Install it first so these shortcuts keep working.";
            }
            if (save != null) save.IsEnabled = account != null;
            Add("living-room", "FishBowl living room", "FishBowl");
            foreach (var profile in library.Emulators.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)) Add("emulator:" + profile.Id, profile.Name, "Emulator");
            foreach (var game in library.Games.Where(g => g != null && !String.IsNullOrWhiteSpace(g.Id)).OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase))
                Add("game:" + game.Id, game.Title, String.IsNullOrWhiteSpace(game.ConsoleLabel) ? "Game" : game.ConsoleLabel);
            // Shortcuts whose game or emulator left the library stay listed (checked) until the user unchecks them.
            foreach (var key in existing.Keys.Where(k => !rows.List.Items.OfType<ListBoxItem>().Any(i => (i.Tag as string) == k)).ToList())
                rows.Add(key, true, existing[key].GetString("AppName") ?? key, "Not in library", "In Steam");
        }
        private void Add(string key, string name, string kind)
        {
            bool inSteam = existing.ContainsKey(key);
            rows.Add(key, inSteam || preselect.Contains(key), name, kind, inSteam ? "In Steam" : "Not added");
        }

        private SteamShortcutSpec Spec(SteamAccount account, string key)
        {
            string name;
            if (key == "living-room") name = "FishBowl living room";
            else if (key.StartsWith("game:", StringComparison.Ordinal)) { var game = library.Games.FirstOrDefault(g => g != null && "game:" + g.Id == key); if (game == null) return null; name = game.Title; }
            else { var profile = library.Emulators.FirstOrDefault(e => "emulator:" + e.Id == key); if (profile == null) return null; name = profile.Name; }
            string exe, dir, options; SteamShortcuts.Command(account.Install, program, leadingArgument, SteamShortcuts.Arguments(key), out exe, out dir, out options);
            return new SteamShortcutSpec { Key = key, Name = name, Exe = exe, StartDir = dir, LaunchOptions = options };
        }

        private async Task<bool> SaveToSteam()
        {
            var account = Account; if (account == null) return false;
            RefreshSteamState();
            if (SteamLocator.IsRunning()) { await Ui.Message(this, "Steam is running.\n\nExit Steam completely (Steam > Exit, or close it from the system tray), then click Save to Steam again. Steam rewrites its shortcut list when it closes, so changes saved now would be lost.", "Close Steam first"); return false; }
            var checkedKeys = new HashSet<string>(rows.CheckedItems.Select(i => i.Tag as string));
            var wanted = checkedKeys.Select(k => Spec(account, k)).Where(s => s != null).ToList();
            // Unchecked shortcuts are removed; a checked one whose game left the library is kept untouched (no spec, not removed).
            var removals = existing.Keys.Where(k => !checkedKeys.Contains(k)).ToList();
            var preview = SteamShortcuts.Apply(account, wanted, removals, () => false, true);
            if (preview.Changes == 0) { await Ui.Message(this, wanted.Count == 0 ? "Check the games, emulators or living room you want in Steam." : "Steam already has these shortcuts. There is nothing to change."); return false; }
            var detail = "Steam account: " + account.Label + "\nShortcut list: " + account.ShortcutsFile + "\n" + (File.Exists(account.ShortcutsFile) ? "A copy is saved first as shortcuts.vdf.fishbowl-<date>.\n" : "") +
                "\nOther shortcuts and Steam's own settings are kept.\n" +
                (preview.Added.Count > 0 ? "\nADD\n" + String.Join("\n", preview.Added) + "\n" : "") + (preview.Updated.Count > 0 ? "\nUPDATE\n" + String.Join("\n", preview.Updated) + "\n" : "") +
                (preview.Removed.Count > 0 ? "\nREMOVE\n" + String.Join("\n", preview.Removed) + "\n" : "") +
                "\nEACH SHORTCUT RUNS\n" + String.Join("\n", wanted.Take(40).Select(w => w.Name + ": " + w.Exe + " " + w.LaunchOptions)) + (wanted.Count > 40 ? "\n… and " + (wanted.Count - 40) + " more" : "") +
                "\n\nIcons and covers are copied to " + account.GridDirectory + ".";
            var review = new ReviewDialog("Review Steam shortcuts", detail, "Save to Steam"); await review.Present(this);
            if (!review.Confirmed) return false;
            await Task.Run(() => { foreach (var spec in wanted) Artwork(spec); });
            SteamShortcutResult result;
            try { result = await Task.Run(() => SteamShortcuts.Apply(account, wanted, removals, SteamLocator.IsRunning, false)); }
            catch (Exception error) { Store.Log("Add to Steam failed: " + error); await Ui.Message(this, "FishBowl did not change Steam's shortcuts.\n\n" + error.Message); return false; }
            await Ui.Message(this, "Steam shortcuts saved for " + account.Label + ".\n\n" + result.Added.Count + " added, " + result.Updated.Count + " updated, " + result.Removed.Count + " removed." +
                (result.Backup == null ? "" : "\nPrevious list saved as " + Path.GetFileName(result.Backup) + ".") + "\n\nStart Steam again to see the changes.", "Add to Steam");
            return true;
        }

        // Icon: the emulator's image (the game's emulator for games), or the FishBowl logo. Grid: the game's cover.
        private void Artwork(SteamShortcutSpec spec)
        {
            try
            {
                var folder = Path.Combine(Platform.CacheHome, "FishBowl", "steam-art"); Directory.CreateDirectory(folder);
                var name = HubPaths.SafeName(spec.Key.Replace(':', '-'));
                if (spec.Key == "living-room")
                {
                    var target = Path.Combine(folder, name + "-icon.png");
                    using (var stream = typeof(Ui).Assembly.GetManifestResourceStream("FishBowl.png")) using (var logo = Bitmap.DecodeToWidth(stream, 256)) logo.Save(target);
                    spec.IconFile = target; return;
                }
                EmulatorProfile profile;
                if (spec.Key.StartsWith("game:", StringComparison.Ordinal))
                {
                    var game = library.Games.First(g => g != null && "game:" + g.Id == spec.Key);
                    if (!String.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath)) spec.GridFile = Steamable(game.ArtworkPath, Path.Combine(folder, name + "-grid.png"), 600);
                    profile = GameLibraryQuery.EmulatorFor(library, game);
                }
                else profile = library.Emulators.First(e => "emulator:" + e.Id == spec.Key);
                if (profile == null) return;
                var source = !String.IsNullOrWhiteSpace(profile.IconPath) && File.Exists(profile.IconPath) ? profile.IconPath : File.Exists(profile.Executable) ? EmulatorImages.IconFile(profile.Executable) : null;
                if (source != null) spec.IconFile = Steamable(source, Path.Combine(folder, name + "-icon.png"), 256);
            }
            catch (Exception error) { Store.Log("Steam artwork unavailable for " + spec.Key + ": " + error.Message); }
        }
        // Steam shows PNG and JPEG; anything else (SVG, WebP, BMP) is converted to PNG, large images scaled down.
        private static string Steamable(string source, string target, int width)
        {
            var extension = Path.GetExtension(source).ToLowerInvariant();
            if (extension == ".svg" || extension == ".svgz")
                return Platform.Run("rsvg-convert", "-w", width.ToString(), "-o", target, source) != null && File.Exists(target) ? target : null;
            if ((extension == ".png" || extension == ".jpg" || extension == ".jpeg") && new FileInfo(source).Length < 4 * 1024 * 1024) return source;
            using (var stream = File.OpenRead(source)) using (var image = new Bitmap(stream))
            using (var scaled = image.PixelSize.Width > width ? image.CreateScaledBitmap(new PixelSize(width, Math.Max(1, image.PixelSize.Height * width / image.PixelSize.Width))) : null)
                (scaled ?? image).Save(target);
            return target;
        }
    }
}
