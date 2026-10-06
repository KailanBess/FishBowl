using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using EmulatorHub;

// Text overflow audit: opens the main window (every section) and every dialog at 100%, 150% and 200% text size,
// measures each label, button, check box and group caption against the space it has, and writes
// overflow-report.md plus screenshots with clipped text outlined in red. Report only: it never fails the build.
class OverflowAudit {
 class Issue { public string Window, Path, Text, Problem; public int Percent; }
 static readonly List<Issue> issues = new List<Issue>();
 static readonly List<string> skipped = new List<string>();
 static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
 static LibraryData data;

 static void Pump(int ms) { var w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }

 [STAThread]
 static int Main(string[] args) {
  string output = Path.GetFullPath(args.Length > 0 ? args[0] : "overflow-audit"); Directory.CreateDirectory(output);
  Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
  Application.ThreadException += (s, e) => skipped.Add("UI exception: " + e.Exception.Message);
  data = Fixture();
  var dialogs = typeof(MainForm).Assembly.GetTypes().Where(t => typeof(Form).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(MainForm) && t.IsPublic).OrderBy(t => t.Name).ToList();
  foreach (int percent in new[] { 100, 150, 200 }) {
   NextUi.TextPercent = percent; data.Enhancements.TextPercent = percent; Store.Save(data);
   AuditMain(percent, output);
   foreach (var type in dialogs) AuditDialog(type, percent, output);
  }
  NextUi.TextPercent = 100;
  WriteReport(output, dialogs.Count);
  Console.WriteLine("Overflow audit: " + issues.Count + " clipped text findings; report in " + output);
  return 0;
 }

 static LibraryData Fixture() {
  var d = Store.Load();
  if (d.Emulators == null) d.Emulators = new List<EmulatorProfile>(); if (d.Games == null) d.Games = new List<GameEntry>();
  NextData.Ensure(d); LibraryAdditions.Ensure(d); UserTools.Ensure(d);
  File.WriteAllText("fixture.rom", "fixture"); File.WriteAllText("Dolphin.exe", "fixture");
  using (var art = new Bitmap(64, 64)) { using (var g = Graphics.FromImage(art)) g.Clear(Color.SteelBlue); art.Save("cover.png"); }
  string[] emulators = { "Dolphin", "PCSX2 Nightly (Vulkan, widescreen patches)", "RetroArch" };
  for (int i = 0; i < emulators.Length; i++) d.Emulators.Add(new EmulatorProfile { Id = "emulator" + i, Name = emulators[i], Executable = Path.GetFullPath("Dolphin.exe"), Preset = "Dolphin (GameCube / Wii)", Platform = "GameCube / Wii", Extensions = new List<string> { ".iso" }, LaunchProfiles = new List<LaunchProfile>(), Builds = new List<EmulatorBuild>() });
  string[] titles = { "Metroid Prime", "The Legend of Zelda: The Wind Waker HD Remaster Collector's Edition", "Super Mario Sunshine", "Xenoblade Chronicles: Definitive Edition" };
  for (int i = 0; i < titles.Length; i++) d.Games.Add(new GameEntry { Id = "game" + i, Title = titles[i], Path = Path.GetFullPath("fixture.rom"), ArtworkPath = Path.GetFullPath("cover.png"), EmulatorId = "emulator0", Tags = new List<string> { "Favorites" }, Genre = "Action adventure", Developer = "Nintendo EAD", ReleaseYear = "2003", Notes = "Fixture notes." });
  Store.Save(d); return Store.Load();
 }

 static void AuditMain(int percent, string output) {
  MainForm main = null;
  try {
   main = new MainForm(true); main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-4000, -4000); main.Size = new Size(1280, 800);
   main.Show(); Pump(600);
   var tabs = typeof(MainForm).GetField("workspaceNavigation", flags);
   var navigation = tabs == null ? null : tabs.GetValue(main) as TabControl;
   int sections = navigation == null ? 1 : navigation.TabCount;
   for (int i = 0; i < sections; i++) {
    if (navigation != null) { navigation.SelectedIndex = i; Pump(500); }
    string name = "MainForm-" + (navigation == null ? "main" : Safe(navigation.TabPages[i].Text));
    Audit(main, name, percent, output);
   }
  } catch (Exception error) { skipped.Add("MainForm at " + percent + "%: " + Inner(error).Message); }
  finally { if (main != null) { try { main.Close(); main.Dispose(); } catch { } } }
 }

 static void AuditDialog(Type type, int percent, string output) {
  Form form = null;
  try {
   form = Create(type);
   if (form == null) { if (percent == 100) skipped.Add(type.Name + ": no constructor the audit can satisfy"); return; }
   form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-4000, -4000);
   form.Show(); Pump(400);
   Audit(form, type.Name, percent, output);
  } catch (Exception error) { if (percent == 100) skipped.Add(type.Name + ": " + Inner(error).Message); }
  finally { if (form != null) { try { form.Close(); form.Dispose(); } catch { } } }
 }

 static Exception Inner(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return e; }

 // Builds a dialog with fixture data for each constructor parameter, preferring the constructor it can fill best.
 static Form Create(Type type) {
  foreach (var ctor in type.GetConstructors().OrderBy(c => c.GetParameters().Length)) {
   var parameters = ctor.GetParameters(); var args = new object[parameters.Length]; bool ok = true;
   for (int i = 0; i < parameters.Length && ok; i++) { object value; ok = Argument(parameters[i], out value); args[i] = value; }
   if (!ok) continue;
   try { return (Form)ctor.Invoke(args); } catch (Exception) { }
  }
  return null;
 }

 static bool Argument(ParameterInfo parameter, out object value) {
  var t = parameter.ParameterType; value = null;
  if (t == typeof(LibraryData)) value = data;
  else if (t == typeof(EmulatorProfile)) value = data.Emulators[0];
  else if (t == typeof(GameEntry)) value = data.Games[1];
  else if (t == typeof(ThemeSettings)) value = data.Theme;
  else if (t == typeof(string)) value = parameter.Name.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ? Path.GetFullPath("fixture.rom") : parameter.Name.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0 ? "1.24" : "Fixture text that is long enough to show how this dialog handles a realistic sentence";
  else if (t == typeof(int)) value = 0; else if (t == typeof(long)) value = 0L; else if (t == typeof(bool)) value = false; else if (t == typeof(double)) value = 0d;
  else if (t.IsEnum) value = Enum.GetValues(t).GetValue(0);
  else if (t == typeof(IEnumerable<string>) || t == typeof(List<string>) || t == typeof(string[])) value = t == typeof(string[]) ? (object)new[] { "First result line", "Second result line with more detail" } : new List<string> { "First result line", "Second result line with more detail" };
  else if (t.IsAssignableFrom(typeof(List<EmulatorProfile>))) value = data.Emulators;
  else if (t.IsAssignableFrom(typeof(List<GameEntry>))) value = data.Games;
  else if (parameter.IsOptional) value = parameter.DefaultValue == DBNull.Value ? null : parameter.DefaultValue;
  else if (!t.IsValueType) value = null; // Unknown reference types get null; constructors that need them are skipped.
  else return false;
  return true;
 }

 static readonly Type[] notText = { typeof(TextBoxBase), typeof(ListControl), typeof(ListView), typeof(TreeView), typeof(DataGridView), typeof(UpDownBase), typeof(WebBrowser), typeof(PictureBox), typeof(ScrollBar), typeof(ProgressBar), typeof(TabControl), typeof(TabPage), typeof(SplitContainer), typeof(ToolStrip) };

 static void Audit(Form form, string window, int percent, string output) {
  var found = new List<Control>();
  foreach (Control c in NextUi.Descendants(form)) {
   if (!c.Visible || c.Width <= 1 || c.Height <= 1 || notText.Any(t => t.IsInstanceOfType(c))) continue;
   string problem = Problem(c);
   if (problem == null) problem = Overlap(c);
   if (problem == null) continue;
   found.Add(c);
   issues.Add(new Issue { Window = window, Percent = percent, Path = PathOf(c), Text = Short(c.Text), Problem = problem });
  }
  try {
   using (var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height))) {
    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
    var client = form.PointToScreen(Point.Empty); var offset = new Point(client.X - form.Left, client.Y - form.Top);
    using (var g = Graphics.FromImage(bitmap)) using (var pen = new Pen(Color.Red, 3)) foreach (var c in found) {
     var p = form.PointToClient(c.PointToScreen(Point.Empty)); g.DrawRectangle(pen, p.X + offset.X, p.Y + offset.Y, c.Width - 1, c.Height - 1);
    }
    if (found.Count > 0 || percent == 100) bitmap.Save(Path.Combine(output, Safe(window) + "-" + percent + ".png"));
   }
  } catch (Exception error) { skipped.Add(window + " screenshot at " + percent + "%: " + error.Message); }
 }

 // Describes how a control's text does not fit, or returns null when it fits.
 static string Problem(Control c) {
  string text = c.Text; bool hasText = !String.IsNullOrWhiteSpace(text);
  if (hasText && c.Parent != null && !(c.Parent is Form)) {
   var parent = c.Parent as ScrollableControl; bool scrolls = parent != null && parent.AutoScroll;
   if (!scrolls && (c.Right > c.Parent.ClientSize.Width + 2 || c.Bottom > c.Parent.ClientSize.Height + 2) && c.Left < c.Parent.ClientSize.Width && c.Top < c.Parent.ClientSize.Height)
    return "cut off by its container (" + c.Right + "×" + c.Bottom + " in " + c.Parent.ClientSize.Width + "×" + c.Parent.ClientSize.Height + ")";
  }
  if (!hasText) return null;
  var font = c.Font; int lineHeight = TextRenderer.MeasureText("Ag", font).Height;
  var label = c as Label;
  if (label != null) {
   if (label.AutoEllipsis) return null;
   int width = label.ClientSize.Width - label.Padding.Horizontal, height = label.ClientSize.Height - label.Padding.Vertical;
   if (label.AutoSize) return null;
   var single = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
   if (single.Width <= width + 2 && single.Height <= height + 4) return null;
   // Compare whole lines: a label a few pixels short of its font height still shows its text.
   var wrapped = TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
   int linesNeeded = (int)Math.Round(wrapped.Height / (double)lineHeight), linesShown = Math.Max(1, (height + lineHeight / 3) / lineHeight);
   if (linesNeeded > linesShown) return "text needs " + linesNeeded + " lines, has room for " + linesShown;
   if (LongestWord(text, font) > width + 2) return "a word is wider (" + LongestWord(text, font) + "px) than the label (" + width + "px)";
   return null;
  }
  var group = c as GroupBox;
  if (group != null) { int need = TextRenderer.MeasureText(text, font).Width + 16; return need > group.Width + 2 ? "caption needs " + need + "px, box is " + group.Width + "px" : null; }
  var button = c as ButtonBase;
  if (button != null) {
   if (button.AutoEllipsis || button.AutoSize) return null; // Windows sizes these to their text.
   var own = button.GetType().GetMethod("TextWidthNeeded"); // FishBowlActionButton drops its icon when space is tight
   if (own != null) { int textNeed = (int)own.Invoke(button, null); return textNeed > button.Width + 2 ? "text needs " + textNeed + "px of width, has " + button.Width + "px" : (lineHeight > button.Height + 2 ? "text needs " + lineHeight + "px of height, has " + button.Height + "px" : null); }
   int need = TextRenderer.MeasureText(text.Replace("&&", "&"), font).Width + button.Padding.Horizontal + 8;
   if (button.Image != null && (button.TextImageRelation == TextImageRelation.ImageBeforeText || button.TextImageRelation == TextImageRelation.TextBeforeImage)) need += button.Image.Width + 4;
   var check = c as CheckBox; var radio = c as RadioButton;
   if ((check != null && check.Appearance == Appearance.Normal) || (radio != null && radio.Appearance == Appearance.Normal)) need += 18;
   if (need > button.Width + 2) return "text needs " + need + "px of width, has " + button.Width + "px";
   if (lineHeight > button.Height + 2) return "text needs " + lineHeight + "px of height, has " + button.Height + "px";
   return null;
  }
  return null;
 }

 // Text controls drawn on top of a sibling with text (e.g. an auto-sized check box running into the next label).
 static string Overlap(Control c) {
  if (c.Parent == null || String.IsNullOrWhiteSpace(c.Text) || !(c is Label || c is ButtonBase)) return null;
  foreach (Control other in c.Parent.Controls) {
   if (other == c || !other.Visible || String.IsNullOrWhiteSpace(other.Text) || !(other is Label || other is ButtonBase || other is ComboBox || other is TextBoxBase)) continue;
   if (c.Parent.Controls.GetChildIndex(other) > c.Parent.Controls.GetChildIndex(c) && (other is Label || other is ButtonBase)) continue; // report each pair once
   var shared = Rectangle.Intersect(c.Bounds, other.Bounds);
   if (shared.Width > 3 && shared.Height > 3) return "overlaps \"" + Short(other.Text) + "\"";
  }
  return null;
 }
 static int LongestWord(string text, Font font) { return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(w => TextRenderer.MeasureText(w, font).Width).DefaultIfEmpty(0).Max(); }
 static string PathOf(Control c) { var parts = new List<string>(); for (var x = c; x != null && !(x is Form); x = x.Parent) parts.Insert(0, String.IsNullOrEmpty(x.Name) ? x.GetType().Name : x.Name); return String.Join(" › ", parts.Skip(Math.Max(0, parts.Count - 4)).ToArray()); }
 static string Short(string text) { text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "/"); return text.Length > 60 ? text.Substring(0, 57) + "…" : text; }
 static string Safe(string text) { return new string((text ?? "").Where(ch => Char.IsLetterOrDigit(ch) || ch == '-').ToArray()); }

 static void WriteReport(string output, int dialogCount) {
  var report = new StringBuilder();
  report.AppendLine("# Text overflow audit").AppendLine();
  report.AppendLine("Windows checked at 100%, 150% and 200% text size: the main window (every section) and " + dialogCount + " dialog types. Red outlines in the PNGs mark the controls listed here.").AppendLine();
  report.AppendLine("**" + issues.Count + " findings** in " + issues.Select(i => i.Window).Distinct().Count() + " windows.").AppendLine();
  foreach (var window in issues.GroupBy(i => i.Window).OrderByDescending(g => g.Count())) {
   report.AppendLine("## " + window.Key + " (" + window.Count() + ")").AppendLine();
   report.AppendLine("| Text size | Control | Text | Problem |").AppendLine("|---|---|---|---|");
   foreach (var i in window.OrderBy(x => x.Percent).ThenBy(x => x.Path)) report.AppendLine("| " + i.Percent + "% | " + i.Path + " | " + i.Text + " | " + i.Problem + " |");
   report.AppendLine();
  }
  if (skipped.Count > 0) { report.AppendLine("## Not checked").AppendLine(); foreach (var s in skipped.Distinct()) report.AppendLine("- " + s); }
  File.WriteAllText(Path.Combine(output, "overflow-report.md"), report.ToString(), new UTF8Encoding(false));
 }
}
