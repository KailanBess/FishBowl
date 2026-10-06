using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EmulatorHub;

class ExpansionVisualTests
{
    static int checks, screenshots;
    static string output;
    static readonly List<string> problems = new List<string>();
    static void Check(bool value, string name) { checks++; if (!value) problems.Add(name); }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            output = Path.GetFullPath("expansion-visuals"); Directory.CreateDirectory(output);
            LibraryData data = Store.Load(); NextData.Ensure(data); ExperienceData.Ensure(data); UserTools.Ensure(data); IntegrationData.Ensure(data);
            data.Games.Clear(); data.Emulators.Clear();
            GameEntry game = new GameEntry { Id = "visual-game", Title = "The Legend of Zelda: The Wind Waker Collector's Edition", Path = "visual.rom", CompatibilityNotes = "Fixture setup notes", Tools = new GameToolsSettings() };
            data.Games.Add(game);
            data.Emulators.Add(new EmulatorProfile { Id = "visual-emulator", Name = "PCSX2 Nightly (Vulkan, widescreen patches)", Builds = new List<EmulatorBuild>(), LaunchProfiles = new List<LaunchProfile>() });
            foreach (bool compact in new[] { false, true }) foreach (int percent in new[] { 100, 150, 200 })
            {
                TextFit.WorkingAreaOverride = compact ? new Rectangle(0, 0, 1024, 720) : new Rectangle(0, 0, 1280, 900);
                NextUi.TextPercent = percent; data.Enhancements.TextPercent = percent;
                string suffix = (compact ? "compact" : "normal") + "-" + percent;
                foreach (string command in new[] { "Game setup", "Mod profiles", "Media library", "Save timeline and transfer", "Play history" })
                    Drive(delegate { WindowsGameTools.Open(null, data, game, delegate { }); }, command, suffix);
                Drive(delegate { WindowsGameTools.OpenLibrary(null, data, delegate { }); }, "Play history", suffix);
                foreach (string command in new[] { "RetroAchievements", "Metadata providers", "Extensions", "Companion" })
                    Drive(delegate { IntegrationTools.Open(null, data, delegate { }); }, command, suffix);
                Drive(delegate { UiPolishTools.Open(null, data, delegate { }); }, "Theme samples", suffix);
            }
            NextUi.TextPercent = 100; TextFit.WorkingAreaOverride = null;
            File.WriteAllLines(Path.Combine(output, "findings.txt"), problems);
            if (problems.Count > 0) throw new Exception(string.Join("\r\n", problems));
            Console.WriteLine("PASS: " + checks + " expansion dialog layout checks and " + screenshots + " screenshots at 100/150/200% on normal and compact desktops.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static void Drive(Action open, string command, string suffix)
    {
        int phase = 0; Exception error = null; Form parent = null; Stopwatch elapsed = Stopwatch.StartNew();
        using (var timer = new Timer { Interval = 40 })
        {
            timer.Tick += delegate
            {
                try
                {
                    if (elapsed.ElapsedMilliseconds > 15000) throw new Exception("Timed out opening " + command);
                    if (phase == 0)
                    {
                        parent = Application.OpenForms.OfType<NextDialog>().LastOrDefault(); if (parent == null) return;
                        Audit(parent, suffix);
                        Button action = NextUi.Descendants(parent).OfType<Button>().FirstOrDefault(b => b.Text == command);
                        if (action == null) throw new Exception("Missing " + command + " action in " + parent.Text);
                        phase = 1; action.PerformClick(); parent.Close();
                    }
                    else
                    {
                        Form child = Application.OpenForms.OfType<NextDialog>().LastOrDefault(f => f != parent); if (child == null) return;
                        Audit(child, suffix); timer.Stop(); child.Close();
                    }
                }
                catch (Exception ex)
                {
                    error = ex; timer.Stop(); foreach (Form form in Application.OpenForms.OfType<NextDialog>().ToArray()) form.Close();
                }
            };
            timer.Start(); open();
        }
        if (error != null) throw error;
        Check(phase == 1, "Modal navigation completed: " + command + " / " + suffix);
    }

    static void Audit(Form form, string suffix)
    {
        string name = form.Text + " / " + suffix;
        Check(form.Width <= TextFit.WorkingAreaOverride.Value.Width + 2 && form.Height <= TextFit.WorkingAreaOverride.Value.Height + 2, "Dialog fits working area: " + name + " bounds=" + form.Bounds);
        foreach (Control control in NextUi.Descendants(form).Where(c => c.Visible && c.Width > 1 && c.Height > 1))
        {
            if (control is Button && control.Parent is FlowLayoutPanel && control.Parent.Dock == DockStyle.Bottom)
                Check(form.RectangleToScreen(form.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle)), "Action remains visible: " + name + " / " + control.Text);
            var scroll = control.Parent as ScrollableControl;
            if (control.Parent != null && !(control.Parent is Form) && (scroll == null || !scroll.AutoScroll))
                Check(control.Right <= control.Parent.ClientSize.Width + 3 && control.Bottom <= control.Parent.ClientSize.Height + 3, "Control fits container: " + name + " / " + control.GetType().Name + " " + control.Text + " " + control.Bounds + " parent=" + control.Parent.ClientSize);
            Label label = control as Label;
            if (label != null && !label.AutoSize && !label.AutoEllipsis && !string.IsNullOrWhiteSpace(label.Text))
            {
                int width = Math.Max(1, label.ClientSize.Width - label.Padding.Horizontal);
                int height = label.ClientSize.Height - label.Padding.Vertical;
                int lineHeight = TextRenderer.MeasureText("Ag", label.Font).Height;
                Size needed = TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
                int linesNeeded = (int)Math.Round(needed.Height / (double)lineHeight), linesShown = Math.Max(1, (height + lineHeight / 3) / lineHeight);
                Check(linesNeeded <= linesShown, "Label fits text: " + name + " / " + label.Text + " needs=" + linesNeeded + " shows=" + linesShown);
            }
        }
        string file = new string(form.Text.Where(char.IsLetterOrDigit).Take(100).ToArray()) + "-" + suffix + ".png";
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, file)); }
        screenshots++;
    }
}
