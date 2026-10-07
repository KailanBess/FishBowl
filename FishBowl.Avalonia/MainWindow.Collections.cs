using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmulatorHub
{
    // Collections, smart lists, the play queue, Surprise me, CSV export and Library maintenance.
    // The rules live in the shared FishBowl.Games.cs (GameCollections, GameLists) so Windows and Linux agree.
    public partial class MainWindow
    {
        private static ListBox GamePickList(double height)
        {
            return new ListBox { Height = height, Background = Ui.P.BottomBrush, Foreground = Ui.P.InkBrush };
        }
        private static void FillGamePickList(ListBox list, IEnumerable<GameEntry> games, Func<GameEntry, string> detail)
        {
            var selected = (list.SelectedItem as ListBoxItem)?.Tag as GameEntry;
            var rows = games.Select(g => new ListBoxItem { Tag = g, Padding = new Thickness(8, 4), Content = new StackPanel { Children = { Ui.Text(g.Title ?? "Untitled", 14, true), Ui.Text(detail(g), 12, false, Ui.P.SubtleBrush) } } }).ToList();
            list.ItemsSource = rows; list.SelectedItem = rows.FirstOrDefault(r => selected != null && ((GameEntry)r.Tag).Id == selected.Id) ?? rows.FirstOrDefault();
        }
        private static GameEntry PickedGame(ListBox list)
        {
            var game = (list.SelectedItem as ListBoxItem)?.Tag as GameEntry;
            if (game == null) throw new InvalidOperationException("Select a game first.");
            return game;
        }
        private static string GameListDetail(GameEntry g)
        {
            return String.Join(" · ", new[] { GameLibraryQuery.ConsoleName(g), g.PlayStatus ?? "Not started", g.LaunchCount + (g.LaunchCount == 1 ? " launch" : " launches") }.Where(s => !String.IsNullOrWhiteSpace(s)));
        }

        // ----- Collections ----------------------------------------------------------------------------------------

        private async Task ManageLibraryCollections()
        {
            if (library.Collections == null) library.Collections = new List<GameCollection>();
            var selectedGames = SelectedLibraryGames();
            var dialog = new FishDialog("Collections", 860, 640);
            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("260,16,*") };
            var collections = new ListBox { Background = p.BottomBrush, Foreground = p.InkBrush };
            var left = new DockPanel();
            var leftActions = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(leftActions, Dock.Bottom); left.Children.Add(leftActions); left.Children.Add(collections); layout.Children.Add(left);
            var right = new StackPanel { Spacing = 6 }; Grid.SetColumn(right, 2); layout.Children.Add(new ScrollViewer { Content = right, [Grid.ColumnProperty] = 2 });
            var members = GamePickList(250);
            Func<GameCollection> current = () => (collections.SelectedItem as ListBoxItem)?.Tag as GameCollection;
            Action refreshList = null, showCollection = null;
            refreshList = () =>
            {
                var keep = current()?.Id;
                var rows = GameCollections.Tree(library).Select(pair => new ListBoxItem { Tag = pair.Key, Padding = new Thickness(8 + pair.Value * 16, 4, 8, 4), Content = Ui.Text((pair.Key.IsSmart ? "◆ " : "") + pair.Key.Name + "  (" + GameLibraryQuery.NestedEntries(library, pair.Key, library.Games).Count() + ")", 14) }).ToList();
                collections.ItemsSource = rows; collections.SelectedItem = rows.FirstOrDefault(r => ((GameCollection)r.Tag).Id == keep) ?? rows.FirstOrDefault();
                showCollection();
            };
            showCollection = () =>
            {
                right.Children.Clear(); var c = current();
                if (c == null) { right.Children.Add(Ui.Text("No collections yet", 18, true)); right.Children.Add(Wrapped("Create a static collection to choose games by hand, or a smart collection that updates itself (favorites, pinned, playing, completed, recent or missing games).", p.SubtleBrush)); return; }
                right.Children.Add(Ui.Text(c.Name, 20, true));
                right.Children.Add(Ui.Text(c.IsSmart ? "Smart collection · " + c.SmartRule : "Static collection", 13, false, p.SubtleBrush));
                var parentNames = new[] { "(None)" }.Concat(library.Collections.Where(x => x.Id != c.Id).Select(x => x.Name)).ToList();
                var parent = Ui.Combo(parentNames, library.Collections.FirstOrDefault(x => x.Id == c.ParentId)?.Name ?? "(None)");
                parent.SelectionChanged += async delegate
                {
                    var target = library.Collections.FirstOrDefault(x => x.Id != c.Id && x.Name == parent.SelectedItem as string);
                    try { GameCollections.SetParent(library, c, target?.Id); Store.Save(library); refreshList(); }
                    catch (IOException error) { await Ui.Message(dialog, error.Message); }
                };
                right.Children.Add(Ui.Caption("Inside collection")); right.Children.Add(parent);
                if (c.IsSmart)
                {
                    var rule = Ui.Combo(GameCollections.SmartRules, c.SmartRule);
                    rule.SelectionChanged += delegate { c.SmartRule = rule.SelectedItem as string; Store.Save(library); refreshList(); };
                    right.Children.Add(Ui.Caption("Rule")); right.Children.Add(rule);
                }
                var games = GameLibraryQuery.NestedEntries(library, c, library.Games).OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
                right.Children.Add(Ui.Caption(games.Count + (games.Count == 1 ? " game" : " games") + (library.Collections.Any(x => x.ParentId == c.Id) ? ", including child collections" : "")));
                FillGamePickList(members, games, GameListDetail); right.Children.Add(members);
                var actions = new List<Control> { Ui.Action("Show in Library", () => { dialog.Close(); OpenLibraryScope(GameLibraryQuery.CollectionPrefix + c.Name); }), Ui.Action("Play", async () => { var g = PickedGame(members); dialog.Close(); await LaunchGame(g); }) };
                if (!c.IsSmart)
                {
                    if (selectedGames.Count > 0) actions.Insert(0, Ui.Action("Add " + (selectedGames.Count == 1 ? selectedGames[0].Title : selectedGames.Count + " selected games"), () => { AddGamesToCollection(c, selectedGames); refreshList(); }, true));
                    actions.Add(Ui.Action("Remove from collection", () => { var g = PickedGame(members); (c.GameIds ?? new List<string>()).Remove(g.Id); Store.Save(library); refreshList(); RefreshGameLibrary(); }));
                }
                right.Children.Add(Ui.Actions(actions.ToArray()));
                if (!c.IsSmart) right.Children.Add(Ui.Hint("Add games by selecting them in Library, then right-click > Add to collection, or open Collections with them selected."));
            };
            collections.SelectionChanged += delegate { showCollection(); };
            Func<bool, Task> create = async smart =>
            {
                var name = (await TextPromptDialog.Ask(dialog, smart ? "New smart collection" : "New static collection", "Collection name", "") ?? "").Trim();
                if (name.Length == 0) return;
                if (library.Collections.Any(c => String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) { await Ui.Message(dialog, "A collection with this name already exists."); return; }
                var collection = new GameCollection { Id = Guid.NewGuid().ToString("N"), Name = name, GameIds = smart ? new List<string>() : selectedGames.Select(g => g.Id).ToList(), IsSmart = smart, SmartRule = smart ? "Favorites" : null, SortOrder = library.Collections.Count };
                library.Collections.Add(collection); Store.Save(library); refreshList();
                collections.SelectedItem = (collections.ItemsSource as IEnumerable<ListBoxItem>).FirstOrDefault(r => r.Tag == collection);
                RefreshGameLibrary();
            };
            leftActions.Children.Add(Ui.Action("New static", () => create(false), true));
            leftActions.Children.Add(Ui.Action("New smart", () => create(true)));
            leftActions.Children.Add(Ui.Action("Rename", async () => { var c = current(); if (c == null) return; var name = (await TextPromptDialog.Ask(dialog, "Rename collection", "Collection name", c.Name) ?? "").Trim(); if (name.Length == 0 || name == c.Name) return; if (library.Collections.Any(x => x != c && String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) { await Ui.Message(dialog, "A collection with this name already exists."); return; } c.Name = name; Store.Save(library); refreshList(); RefreshGameLibrary(); }));
            leftActions.Children.Add(Ui.Action("Remove", async () => { var c = current(); if (c == null) return; if (!await Ui.Confirm(dialog, "Remove the collection " + c.Name + "? Its games stay in your library.")) return; GameCollections.Delete(library, c); Store.Save(library); refreshList(); RefreshGameLibrary(); }));
            foreach (Control b in leftActions.Children) b.HorizontalAlignment = HorizontalAlignment.Stretch;
            refreshList();
            var root = new DockPanel(); var close = Ui.Action("Close", dialog.Close); close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 12, 0, 0);
            var intro = Wrapped(selectedGames.Count == 0 ? "Create and review static or smart collections." : selectedGames.Count + " selected game" + (selectedGames.Count == 1 ? " is" : "s are") + " ready for a static collection.", p.SubtleBrush); intro.Margin = new Thickness(0, 0, 0, 10);
            DockPanel.SetDock(intro, Dock.Top); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(intro); root.Children.Add(close); root.Children.Add(layout);
            dialog.Body = root; await dialog.Present(this);
        }

        private void AddGamesToCollection(GameCollection collection, IEnumerable<GameEntry> games)
        {
            if (collection.GameIds == null) collection.GameIds = new List<string>();
            foreach (var game in games) if (!collection.GameIds.Contains(game.Id)) collection.GameIds.Add(game.Id);
            Store.Save(library); RefreshGameLibrary();
        }

        private async Task AddSelectedToCollection()
        {
            var games = SelectedLibraryGames(); if (games.Count == 0) return;
            var statics = (library.Collections ?? new List<GameCollection>()).Where(c => !c.IsSmart).OrderBy(c => c.Name).ToList();
            if (statics.Count == 0) { await ManageLibraryCollections(); return; }
            var names = statics.Select(c => c.Name).Concat(new[] { "New collection..." }).ToList();
            var dialog = new FishDialog("Add to collection", 440); var body = new StackPanel { Spacing = 8 }; var choice = Ui.Combo(names, names[0]);
            body.Children.Add(Wrapped("Add " + (games.Count == 1 ? games[0].Title : games.Count + " games") + " to:")); body.Children.Add(choice);
            bool ok = false; body.Children.Add(dialog.Footer("Add", () => { ok = true; return Task.FromResult(true); })); dialog.Body = body; await dialog.Present(this);
            if (!ok) return;
            if (choice.SelectedIndex == names.Count - 1) { await ManageLibraryCollections(); return; }
            AddGamesToCollection(statics[choice.SelectedIndex], games); SetStatus("Added to " + statics[choice.SelectedIndex].Name + ".");
        }

        // ----- Smart lists ----------------------------------------------------------------------------------------

        private async Task ShowSmartLists()
        {
            GameLists.Ensure(library);
            var dialog = new FishDialog("Smart Library lists", 900, 620);
            var lists = Ui.Combo(library.SmartLists.Select(l => l.Name), library.SmartLists.FirstOrDefault()?.Name); lists.PlaceholderText = "No smart lists yet";
            var count = Ui.Text("", 13, false, p.SubtleBrush); var games = GamePickList(330);
            Func<SmartLibraryList> current = () => library.SmartLists.FirstOrDefault(l => l.Name == lists.SelectedItem as string);
            Action refresh = () =>
            {
                var list = current(); var matches = list == null ? new List<GameEntry>() : GameLists.Match(library, list);
                count.Text = list == null ? "Create a smart list to save a search that updates as your Library changes." : matches.Count + " matching games — updates as your Library changes";
                FillGamePickList(games, matches, GameListDetail);
            };
            Action reloadNames = () => { var keep = lists.SelectedItem as string; lists.ItemsSource = library.SmartLists.Select(l => l.Name).ToList(); lists.SelectedItem = library.SmartLists.Any(l => l.Name == keep) ? keep : library.SmartLists.FirstOrDefault()?.Name; refresh(); };
            lists.SelectionChanged += delegate { refresh(); };
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(Wrapped("Save dynamic smart lists by search, platform, progress, favorites or unplayed games.", p.SubtleBrush));
            body.Children.Add(lists); body.Children.Add(count); body.Children.Add(games);
            body.Children.Add(Ui.Actions(
                Ui.Action("New list", async () => { var made = await EditSmartList(dialog, null); if (made != null) { reloadNames(); lists.SelectedItem = made.Name; } }, true),
                Ui.Action("Edit list", async () => { var list = current(); if (list == null) return; var edited = await EditSmartList(dialog, list); if (edited != null) { reloadNames(); lists.SelectedItem = edited.Name; } }),
                Ui.Action("Delete list", async () => { var list = current(); if (list == null) return; if (!await Ui.Confirm(dialog, "Delete the smart list " + list.Name + "? Games are not changed.")) return; library.SmartLists.Remove(list); Store.Save(library); reloadNames(); }),
                Ui.Action("Add to queue", () => { GameLists.Enqueue(library, PickedGame(games)); Store.Save(library); count.Text = "Added to play queue."; }),
                Ui.Action("Launch", async () => { var g = PickedGame(games); dialog.Close(); await LaunchGame(g); }),
                Ui.Action("Export list", async () => { var list = current(); if (list != null) await ExportLibraryCsv(GameLists.Match(library, list), dialog); }),
                Ui.Action("Close", dialog.Close)));
            refresh(); dialog.Body = body; await dialog.Present(this);
        }

        private async Task<SmartLibraryList> EditSmartList(Window owner, SmartLibraryList existing)
        {
            var source = existing ?? new SmartLibraryList();
            var dialog = new FishDialog(existing == null ? "New smart list" : "Edit smart list", 560); var body = new StackPanel { Spacing = 6 };
            var name = Ui.Field(source.Name); var search = Ui.Field(source.Search);
            var platforms = new[] { "Any" }.Concat(library.Games.Select(g => g.ConsoleLabel).Where(s => !String.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s)).ToList();
            var platform = Ui.Combo(platforms, source.Platform ?? "Any");
            var statuses = new[] { "Any" }.Concat(GameLibraryQuery.Progress).Concat(library.Games.Select(g => g.PlayStatus).Where(s => !String.IsNullOrWhiteSpace(s))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var status = Ui.Combo(statuses, source.Status ?? "Any");
            var favorites = Ui.Check("Favorites only", source.FavoritesOnly); var unplayed = Ui.Check("Never launched through FishBowl", source.UnplayedOnly);
            body.Children.Add(Ui.Caption("List name")); body.Children.Add(name);
            body.Children.Add(Ui.Caption("Search title, genre, developer or tags")); body.Children.Add(search);
            body.Children.Add(Ui.Caption("Platform")); body.Children.Add(platform); body.Children.Add(Ui.Caption("Progress")); body.Children.Add(status);
            body.Children.Add(favorites); body.Children.Add(unplayed);
            SmartLibraryList result = null;
            body.Children.Add(dialog.Footer("Save", async () =>
            {
                var text = (name.Text ?? "").Trim();
                if (text.Length == 0) { await Ui.Message(dialog, "Enter a list name."); return false; }
                if (library.SmartLists.Any(x => x != existing && String.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase))) { await Ui.Message(dialog, "A smart list with this name already exists."); return false; }
                result = existing ?? new SmartLibraryList();
                result.Name = text; result.Search = (search.Text ?? "").Trim(); result.Platform = platform.SelectedItem as string; result.Status = status.SelectedItem as string; result.FavoritesOnly = favorites.IsChecked == true; result.UnplayedOnly = unplayed.IsChecked == true;
                if (existing == null) library.SmartLists.Add(result);
                Store.Save(library); return true;
            }));
            dialog.Body = body; await dialog.Present(owner);
            return result;
        }

        // ----- Play queue -----------------------------------------------------------------------------------------

        private async Task ShowPlayQueue(GameEntry selected)
        {
            GameLists.Ensure(library);
            var dialog = new FishDialog("Play queue", 820, 600); var body = new StackPanel { Spacing = 8 };
            var list = GamePickList(380); var note = Wrapped("Choose games for later. Launching keeps them queued until you remove them.", p.SubtleBrush);
            Action refresh = () => { FillGamePickList(list, GameLists.QueueGames(library), GameListDetail); if (GameLists.QueueGames(library).Count == 0) note.Text = "The queue is empty. Add games from Library (right-click > Add to play queue), smart lists or Surprise me."; };
            Action<int> move = delta => { var g = PickedGame(list); if (GameLists.MoveQueue(library, g.Id, delta)) { Store.Save(library); refresh(); } };
            body.Children.Add(note); body.Children.Add(list);
            body.Children.Add(Ui.Actions(
                Ui.Action(selected == null ? "Add selected game" : "Add " + selected.Title, () => { if (selected == null) throw new InvalidOperationException("Select a game in Library first."); GameLists.Enqueue(library, selected); Store.Save(library); refresh(); }, true),
                Ui.Action("Move up", () => move(-1)), Ui.Action("Move down", () => move(1)),
                Ui.Action("Remove", () => { library.PlayQueue.Remove(PickedGame(list).Id); Store.Save(library); refresh(); }),
                Ui.Action("Launch", async () => { var g = PickedGame(list); dialog.Close(); await LaunchGame(g); }),
                Ui.Action("Details", () => { var g = PickedGame(list); dialog.Close(); libraryPages.SelectedIndex = LibraryPage; SelectLibraryGame(g); }),
                Ui.Action("Export queue", () => ExportLibraryCsv(GameLists.QueueGames(library), dialog)),
                Ui.Action("Close", dialog.Close)));
            refresh(); dialog.Body = body; await dialog.Present(this);
            RefreshGameLibrary();
        }

        // ----- Surprise me ----------------------------------------------------------------------------------------

        private static readonly Random surpriseRandom = new Random();
        private async Task ShowSurpriseMe()
        {
            var dialog = new FishDialog("Surprise me", 620); var body = new StackPanel { Spacing = 10 };
            var unplayed = Ui.Check("Only games never launched through FishBowl", true);
            var title = Ui.Text("", 22, true); title.TextWrapping = TextWrapping.Wrap; var detail = Wrapped("", p.SubtleBrush);
            var coverHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left };
            GameEntry picked = null;
            Action roll = () =>
            {
                var candidates = GameLists.SurpriseCandidates(library, unplayed.IsChecked == true, g => { try { GamePlay.Prepare(library, g); return true; } catch (IOException) { return false; } });
                var others = candidates.Where(g => picked == null || g.Id != picked.Id).ToList();
                picked = others.Count > 0 ? others[surpriseRandom.Next(others.Count)] : candidates.FirstOrDefault();
                title.Text = picked == null ? "No launch-ready games match." : picked.Title;
                detail.Text = picked == null ? "Add games or turn off the unplayed filter. Missing game files and invalid launch settings are excluded." : (picked.ConsoleLabel ?? "Platform not set") + "\n" + (picked.Genre ?? "Genre not set") + "\n" + candidates.Count + " eligible games";
                coverHost.Content = picked == null ? null : GameCover(picked, 100);
            };
            unplayed.IsCheckedChanged += delegate { roll(); };
            var row = new DockPanel(); DockPanel.SetDock(coverHost, Dock.Left); coverHost.Margin = new Thickness(0, 0, 14, 0); row.Children.Add(coverHost);
            var words = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(title); words.Children.Add(detail); row.Children.Add(words);
            body.Children.Add(unplayed); body.Children.Add(row);
            body.Children.Add(Ui.Actions(
                Ui.Action("Launch", async () => { if (picked == null) throw new InvalidOperationException("No game selected."); var g = picked; dialog.Close(); await LaunchGame(g); }, true),
                Ui.Action("Pick another", roll),
                Ui.Action("Add to queue", () => { if (picked == null) throw new InvalidOperationException("No game selected."); GameLists.Enqueue(library, picked); Store.Save(library); detail.Text = "Added to play queue."; }),
                Ui.Action("Close", dialog.Close)));
            roll(); dialog.Body = body; await dialog.Present(this);
        }

        // ----- CSV export -----------------------------------------------------------------------------------------

        private Task ExportLibraryCsv(IEnumerable<GameEntry> games) { return ExportLibraryCsv(games, this); }
        private async Task ExportLibraryCsv(IEnumerable<GameEntry> games, Window owner)
        {
            var list = games.ToList();
            var file = await Ui.SaveFile(owner, "Export library CSV", "CSV spreadsheet|*.csv", "FishBowl Library.csv");
            if (file == null) return;
            File.WriteAllText(file, GameLists.Csv(list), new UTF8Encoding(true));
            await Ui.Message(owner, "Exported " + list.Count + " games.");
        }

        // ----- Library maintenance --------------------------------------------------------------------------------

        // Windows "Library maintenance": read-only reviews plus a folder sync; scans never change emulator settings or game files.
        private async Task ShowLibraryMaintenance()
        {
            var dialog = new FishDialog("Library maintenance", 820, 600); var body = new DockPanel();
            var results = new ListBox { Background = p.BottomBrush, Foreground = p.InkBrush };
            Action<IEnumerable<string>> show = rows => { var items = rows.ToList(); if (items.Count == 0) items.Add("Nothing needs attention."); results.ItemsSource = items.Select(r => new ListBoxItem { Content = Wrapped(r), Padding = new Thickness(8, 4) }).ToList(); };
            Action summary = () => show(new[] {
                "Games: " + library.Games.Count, "Collections: " + (library.Collections ?? new List<GameCollection>()).Count,
                "Game storage: " + GameStorage.Root(library),
                "Automatic folder watching: " + (library.Theme.AutoSyncGameFolders ? "on" : "off"),
                "Last library scan: " + (library.Theme.LastLibraryScanAt ?? "Not yet run") });
            var intro = Wrapped("Review FishBowl's saved game entries and folders. Scans never alter emulator settings or game files.", p.SubtleBrush); intro.Margin = new Thickness(0, 0, 0, 8);
            var actions = Ui.Actions(
                Ui.Action("Sync folders", () => { int added = GameLibraryCatalog.Sync(library); library.Theme.LastLibraryScanAt = DateTime.Now.ToString("g"); Store.Save(library); RefreshGameLibrary(); show(new[] { added == 0 ? "No new matching game files were found in emulator game folders." : "Added " + added + " new game entries." }); }),
                Ui.Action("Missing files", () => show(library.Games.Where(g => String.IsNullOrWhiteSpace(g.Path) || !File.Exists(g.Path)).Select(g => "MISSING\n" + g.Title + " - " + (g.Path ?? "No path")))),
                Ui.Action("Needs an emulator", () => show(library.Games.Where(g => g.RequiresEmulatorAssignment || (!GameLibraryQuery.Native(g) && GameLibraryQuery.EmulatorFor(library, g) == null)).Select(g => g.Title + " - " + GameLibraryQuery.ConsoleName(g)))),
                Ui.Action("Content duplicates", async () => { show(new[] { "Comparing files…" }); show(await Task.Run(() => DuplicateGames())); }),
                Ui.Action("Cleanup ideas", () => show(new[] {
                    "Missing game entries: " + library.Games.Count(g => !File.Exists(g.Path)) + " - review them before removing any records.",
                    "Missing artwork links: " + library.Games.Count(g => !String.IsNullOrWhiteSpace(g.ArtworkPath) && !File.Exists(g.ArtworkPath)) + " - use Edit game to repair or clear them.",
                    "Content duplicates: use Content duplicates for a read-only SHA-256 review." })),
                Ui.Action("Open games folder", () => { Directory.CreateDirectory(GameStorage.Root(library)); Platform.Open(GameStorage.Root(library)); }));
            var close = Ui.Action("Close", dialog.Close); close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 10, 0, 0);
            DockPanel.SetDock(intro, Dock.Top); DockPanel.SetDock(actions, Dock.Top); DockPanel.SetDock(close, Dock.Bottom);
            body.Children.Add(intro); body.Children.Add(actions); body.Children.Add(close); body.Children.Add(results);
            summary(); dialog.Body = body; await dialog.Present(this);
        }

        private List<string> DuplicateGames()
        {
            var rows = library.Games.Where(g => !String.IsNullOrWhiteSpace(g.Path)).GroupBy(g => g.Path, Platform.PathComparer).Where(g => g.Count() > 1).Select(g => "SAME PATH\n" + String.Join("\n", g.Select(x => x.Title + " - " + x.Path))).ToList();
            foreach (var size in library.Games.Where(g => File.Exists(g.Path)).GroupBy(g => new FileInfo(g.Path).Length).Where(g => g.Count() > 1))
                foreach (var same in size.GroupBy(g => GameTools.Hash(g.Path)).Where(g => g.Select(x => x.Path).Distinct(Platform.PathComparer).Count() > 1))
                    rows.Add("SAME FILE CONTENT\n" + String.Join("\n", same.Select(g => g.Title + " / " + g.Path)));
            return rows;
        }
    }
}
