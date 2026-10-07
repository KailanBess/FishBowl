using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmulatorHub
{
    // "Session journal and charts": every play session with this week's chart, monthly totals, corrections,
    // manual sessions and CSV export. Corrections use the shared GamePlay rules, so totals match Windows.
    public partial class MainWindow
    {
        private async Task ShowSessionJournal(GameEntry only)
        {
            if (library.PlaySessions == null) library.PlaySessions = new List<PlaySession>();
            var dialog = new FishDialog(only == null ? "Session journal and charts" : "Play sessions - " + only.Title, 900, 680);
            var root = new DockPanel();
            var gameNames = new[] { "All games" }.Concat(library.Games.Where(g => library.PlaySessions.Any(s => s.GameId == g.Id) || g == only).Select(g => g.Title).Distinct().OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)).ToList();
            var filter = Ui.Combo(gameNames, only?.Title ?? "All games"); filter.HorizontalAlignment = HorizontalAlignment.Left; filter.MinWidth = 260;
            var summary = Ui.Text("", 13, false, p.SubtleBrush);
            var chartHost = new ContentControl();
            var monthly = new TextBlock { Foreground = p.SubtleBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            var list = new ListBox { Background = p.BottomBrush, Foreground = p.InkBrush };
            Func<GameEntry> filtered = () => filter.SelectedIndex <= 0 ? null : library.Games.FirstOrDefault(g => g.Title == filter.SelectedItem as string);
            Action reload = () =>
            {
                var game = filtered();
                var sessions = GamePlay.SessionsFor(library, game);
                var scope = new LibraryData { PlaySessions = sessions, Games = library.Games };
                var week = GamePlay.WeekTotals(scope, DateTime.Now);
                chartHost.Content = WeekChart(week, 70);
                summary.Text = sessions.Count + (sessions.Count == 1 ? " session · " : " sessions · ") + GamePlay.Duration(sessions.Sum(s => s.Seconds)) + " in total";
                monthly.Text = String.Join("   ", GameTools.Monthly(sessions).Take(6).Select(m => m.Month + ": " + GamePlay.Duration(m.Seconds)));
                var rows = sessions.Select(s =>
                {
                    var title = library.Games.FirstOrDefault(g => g.Id == s.GameId)?.Title ?? "Removed game";
                    string kind = s.Corrected ? "Manually corrected" : s.Uncertain ? "Uncertain" : s.EndedAt == null ? (sessionTrackers.Values.Any(t => t.SessionId == s.Id && !t.Finished) ? "In progress" : "Interrupted") : "Emulator process";
                    var line = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,1.4*,1*,1.4*") };
                    Action<string, int, IBrush> cell = (text, column, brush) => { var t = Ui.Text(text, 13, false, brush); t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new Thickness(0, 0, 10, 0); Grid.SetColumn(t, column); line.Children.Add(t); };
                    cell(title, 0, p.InkBrush); cell(FriendlyDate(s.StartedAt), 1, p.SubtleBrush); cell(s.Uncertain && s.Seconds == 0 ? "Unknown" : GamePlay.Duration(s.Seconds), 2, p.InkBrush); cell(kind, 3, p.SubtleBrush);
                    var item = new StackPanel { Children = { line } };
                    if (!String.IsNullOrWhiteSpace(s.Note)) { var note = Ui.Text(s.Note, 11, false, p.SubtleBrush); note.TextTrimming = TextTrimming.CharacterEllipsis; item.Children.Add(note); }
                    return new ListBoxItem { Tag = s, Content = item, Padding = new Thickness(8, 4) };
                }).ToList();
                list.ItemsSource = rows;
                if (rows.Count == 0) list.ItemsSource = new[] { new ListBoxItem { Content = Ui.Hint("No play sessions yet. Games launched from FishBowl record their play time here."), IsEnabled = false } };
            };
            filter.SelectionChanged += delegate { reload(); };
            Func<PlaySession> picked = () =>
            {
                var session = (list.SelectedItem as ListBoxItem)?.Tag as PlaySession;
                if (session == null) throw new InvalidOperationException("Select a session.");
                return session;
            };
            var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 10) };
            var head = new DockPanel(); DockPanel.SetDock(chartHost, Dock.Right); head.Children.Add(chartHost);
            var words = new StackPanel { Spacing = 6 }; words.Children.Add(filter); words.Children.Add(summary); words.Children.Add(monthly); head.Children.Add(words);
            top.Children.Add(head);
            var actions = Ui.Actions(
                Ui.Action("Edit time and note", async () => { var s = picked(); if (sessionTrackers.Values.Any(t => t.SessionId == s.Id && !t.Finished)) { await Ui.Message(dialog, "This session is still running. Correct it after the game closes."); return; } if (await CorrectSessionDialog(dialog, s)) reload(); }),
                Ui.Action("Add session", async () => { var game = filtered() ?? SelectedLibraryGame(); if (game == null) { await Ui.Message(dialog, "Choose a game in the list above, or select one in Library first."); return; } if (await AddSessionDialog(dialog, game)) reload(); }),
                Ui.Action("Export CSV", async () =>
                {
                    var file = await Ui.SaveFile(dialog, "Export sessions", "CSV|*.csv", "FishBowl sessions.csv"); if (file == null) return;
                    var lines = new List<string> { "Game,Started,Ended,Seconds,Uncertain,Corrected,Note" };
                    foreach (var s in GamePlay.SessionsFor(library, filtered()))
                        lines.Add(String.Join(",", new[] { library.Games.FirstOrDefault(g => g.Id == s.GameId)?.Title ?? "Removed game", s.StartedAt, s.EndedAt, s.Seconds.ToString(), s.Uncertain.ToString(), s.Corrected.ToString(), s.Note }.Select(GameLists.CsvCell)));
                    File.WriteAllLines(file, lines); SetStatus("Exported " + (lines.Count - 1) + " sessions.");
                }),
                Ui.Action("Close", dialog.Close));
            DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(top); root.Children.Add(actions); root.Children.Add(list);
            reload(); dialog.Body = root; await dialog.Present(this);
            RefreshGameLibrary();
        }

        private async Task<bool> CorrectSessionDialog(Window owner, PlaySession session)
        {
            var dialog = new FishDialog("Correct session", 520); var body = new StackPanel { Spacing = 6 };
            var minutes = new NumericUpDown { Minimum = 0, Maximum = 525600, Increment = 1, FormatString = "0.0", Value = (decimal)Math.Round(session.Seconds / 60.0, 1), Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
            var note = Ui.Paragraphs(session.Note, false); note.MinHeight = 90;
            body.Children.Add(Wrapped((library.Games.FirstOrDefault(g => g.Id == session.GameId)?.Title ?? "Removed game") + " · " + FriendlyDate(session.StartedAt), p.SubtleBrush));
            body.Children.Add(Ui.Caption("Minutes")); body.Children.Add(minutes); body.Children.Add(Ui.Caption("Personal note / correction reason")); body.Children.Add(note);
            body.Children.Add(dialog.Footer("Save correction", () =>
            {
                GamePlay.CorrectSession(library, session, (long)((minutes.Value ?? 0) * 60m), note.Text);
                Store.Save(library); return Task.FromResult(true);
            }));
            dialog.Body = body; await dialog.Present(owner);
            return dialog.Confirmed;
        }

        // For launchers FishBowl could not track, or play outside FishBowl.
        private async Task<bool> AddSessionDialog(Window owner, GameEntry game)
        {
            var dialog = new FishDialog("Add play session", 520); var body = new StackPanel { Spacing = 6 };
            var date = new DatePicker { SelectedDate = DateTimeOffset.Now, HorizontalAlignment = HorizontalAlignment.Left };
            var time = new TimePicker { SelectedTime = DateTime.Now.TimeOfDay - TimeSpan.FromHours(1), HorizontalAlignment = HorizontalAlignment.Left, ClockIdentifier = "24HourClock" };
            var minutes = new NumericUpDown { Minimum = 1, Maximum = 1440, Increment = 5, FormatString = "0", Value = 60, Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
            var note = Ui.Field("Entered manually.");
            body.Children.Add(Wrapped("Add time you played " + game.Title + " outside FishBowl or with a launcher FishBowl could not track.", p.SubtleBrush));
            body.Children.Add(Ui.Caption("Started")); body.Children.Add(Ui.Actions(date, time));
            body.Children.Add(Ui.Caption("Minutes played")); body.Children.Add(minutes); body.Children.Add(Ui.Caption("Note")); body.Children.Add(note);
            body.Children.Add(dialog.Footer("Add session", () =>
            {
                var day = (date.SelectedDate ?? DateTimeOffset.Now).Date; var started = day + (time.SelectedTime ?? TimeSpan.Zero);
                GamePlay.AddManualSession(library, game, DateTime.SpecifyKind(started, DateTimeKind.Local), (long)((minutes.Value ?? 0) * 60m), note.Text);
                Store.Save(library); return Task.FromResult(true);
            }));
            dialog.Body = body; await dialog.Present(owner);
            return dialog.Confirmed;
        }
    }
}
