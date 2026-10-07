using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmulatorHub
{
    // "Game screenshot gallery": screenshots linked to a game (files stay where they are), as on Windows.
    public partial class MainWindow
    {
        private async Task ShowScreenshotGallery(GameEntry start)
        {
            if (library.GameScreenshots == null) library.GameScreenshots = new List<GameScreenshot>();
            if (library.Games.Count == 0) { await Ui.Message(this, "Add games to the Library first."); return; }
            var dialog = new FishDialog("Game screenshot gallery", 960, 680);
            var titles = library.Games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            var game = Ui.Combo(titles.Select(g => g.Title), (start ?? titles[0]).Title); game.HorizontalAlignment = HorizontalAlignment.Left; game.MinWidth = 300;
            var favorites = Ui.Check("Favorites only", false);
            var list = new ListBox { Background = p.BottomBrush, Foreground = p.InkBrush, ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel>(() => new WrapPanel()) };
            var count = Ui.Text("", 12, false, p.SubtleBrush);
            var thumbnails = new List<Bitmap>();
            Func<GameEntry> current = () => titles.ElementAtOrDefault(game.SelectedIndex);
            Func<GameScreenshot> picked = () => { var s = (list.SelectedItem as ListBoxItem)?.Tag as GameScreenshot; if (s == null) throw new InvalidOperationException("Select a screenshot."); return s; };
            Action reload = () =>
            {
                foreach (var b in thumbnails) b.Dispose(); thumbnails.Clear();
                var g = current(); if (g == null) return;
                var shots = library.GameScreenshots.Where(s => s.GameId == g.Id && (favorites.IsChecked != true || s.Favorite)).OrderByDescending(s => s.Favorite).ThenByDescending(s => GameLibraryQuery.Date(s.AddedAt)).ToList();
                list.ItemsSource = shots.Select(s =>
                {
                    Control image = new Border { Width = 200, Height = 120, Background = p.SurfaceBrush, Child = Ui.Text("Missing", 12, false, p.PinkBrush) };
                    if (File.Exists(s.Path)) { try { using (var stream = File.OpenRead(s.Path)) { var bitmap = Bitmap.DecodeToWidth(stream, 200); thumbnails.Add(bitmap); image = new Image { Source = bitmap, Width = 200, Height = 120, Stretch = Stretch.Uniform }; } } catch (Exception error) { Store.Log("Screenshot preview unavailable: " + error.Message); } }
                    var name = Ui.Text((s.Favorite ? "★ " : "") + Path.GetFileName(s.Path), 12); name.TextTrimming = TextTrimming.CharacterEllipsis; name.Width = 200;
                    return new ListBoxItem { Tag = s, Padding = new Thickness(6), Content = new StackPanel { Spacing = 4, Children = { image, name, Ui.Text(FriendlyDate(s.AddedAt), 11, false, p.SubtleBrush) } } };
                }).ToList();
                count.Text = shots.Count == 0 ? "No screenshots linked to this game yet. Use Associate screenshots to link image files; they stay where they are." : shots.Count + (shots.Count == 1 ? " screenshot" : " screenshots");
            };
            game.SelectionChanged += delegate { reload(); }; favorites.IsCheckedChanged += delegate { reload(); };
            var head = Ui.Actions(game, favorites);
            var actions = Ui.Actions(
                Ui.Action("Associate screenshots", async () =>
                {
                    var g = current(); var emulator = GameLibraryQuery.EmulatorFor(library, g);
                    var files = await Ui.PickFiles(dialog, "Associate screenshots", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp", null, true);
                    foreach (var path in files)
                        if (!library.GameScreenshots.Any(s => s.GameId == g.Id && GameLibraryRemoval.SamePath(s.Path, path)))
                            library.GameScreenshots.Add(new GameScreenshot { Id = Guid.NewGuid().ToString("N"), GameId = g.Id, Path = path, AddedAt = File.GetLastWriteTimeUtc(path).ToString("o") });
                    Store.Save(library); reload();
                }, true),
                Ui.Action("Favorite / unfavorite", () => { var s = picked(); s.Favorite = !s.Favorite; Store.Save(library); reload(); }),
                Ui.Action("Open image", () => Platform.Open(picked().Path)),
                Ui.Action("Use as cover", () => { var s = picked(); var g = current(); g.ArtworkPath = GameLibraryEditing.RetainArtwork(s.Path, Store.DataDirectory); Store.Save(library); RefreshGameLibrary(); SetStatus("Cover updated for " + g.Title + "."); }),
                Ui.Action("Export selected", async () => { var s = picked(); var target = await Ui.SaveFile(dialog, "Export screenshot", "Images|*" + Path.GetExtension(s.Path), Path.GetFileName(s.Path)); if (target != null) File.Copy(s.Path, target, true); }),
                Ui.Action("Remove from gallery", () => { library.GameScreenshots.Remove(picked()); Store.Save(library); reload(); }),
                Ui.Action("Close", dialog.Close));
            var root = new DockPanel(); var top = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8), Children = { head, count } };
            DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(top); root.Children.Add(actions); root.Children.Add(list);
            dialog.Closed += delegate { foreach (var b in thumbnails) b.Dispose(); };
            reload(); dialog.Body = root; await dialog.Present(this);
        }
    }
}
