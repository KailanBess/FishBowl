using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class VisualRegressionTests
{
    static int checks;
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    static void Show(Form form) { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-4000, -4000); form.Show(); Application.DoEvents(); }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Descendants(child)) yield return nested; }
    }
    [STAThread] static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
    static void Run()
    {
        Application.EnableVisualStyles();
        var library = Store.Load(); ExperienceData.Ensure(library);
        using (var painted = new Form { ClientSize = new Size(240, 100), BackColor = Color.FromArgb(25, 40, 60) })
        using (var button = new FishBowlActionButton { Text = "Open", Bounds = new Rectangle(25, 20, 150, 42), BackColor = Color.FromArgb(120, 70, 150) })
        {
            painted.Controls.Add(button); Show(painted);
            using (var bitmap = new Bitmap(button.Width, button.Height))
            {
                button.DrawToBitmap(bitmap, button.ClientRectangle);
                Check(bitmap.GetPixel(0, 0).ToArgb() == painted.BackColor.ToArgb(), "Rounded action corners show the parent surface without rectangular patches: " + bitmap.GetPixel(0, 0) + " vs " + painted.BackColor + " radius " + CosmeticRuntime.Current.CornerRadius);
                Check(bitmap.GetPixel(button.Width / 2, button.Height / 2).ToArgb() != painted.BackColor.ToArgb(), "Rounded actions still paint their face");
            }
        }
        using (var message = new ReferenceMessageDialog("Review this change.", "Confirm action", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2))
        {
            Show(message);
            Check(((Button)message.AcceptButton).DialogResult == DialogResult.No, "A confirmation retains its safe default action");
            Check(((Button)message.CancelButton).DialogResult == DialogResult.No, "Escape declines a Yes/No confirmation");
            Check(message.Actions.Controls.OfType<Button>().Select(button => button.DialogResult).SequenceEqual(new[] { DialogResult.Yes, DialogResult.No }), "Styled confirmations preserve their requested results");
            Check(message.BackColor == FishBowlPalette.ThemeBottom, "Styled messages inherit the live shell palette");
            message.Close();
            Check(message.DialogResult == DialogResult.No, "Closing a styled Yes/No confirmation declines its action");
        }
        using (var main = new MainForm(true))
        {
            Show(main);
            var tabs = (TabControl)typeof(MainForm).GetField("workspaceNavigation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
            Check(!Descendants(main).OfType<Label>().Any(label => label.Visible && label.Text == "FishBowl"), "The shell has no repeated FishBowl labels");
            Check(main.Text == tabs.SelectedTab.Text, "The native caption names the active workspace");
            Check(tabs.TabPages.Cast<TabPage>().Select(page=>page.Text).SequenceEqual(new[]{"Home","Emulators","Library","Play","Settings"}),"Five workspaces preserve page order and append Settings");
            var rail = (FlowLayoutPanel)typeof(MainForm).GetField("dashboardRail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
            Check(((FishBowlTabs)tabs).HideWorkspaceHeaders && tabs.TabPages.Cast<TabPage>().All(page => rail.Controls.OfType<Button>().Any(button => button.AccessibleName == page.Text && button.Width >= TextRenderer.MeasureText(page.Text, button.Font).Width)), "The dashboard rail exposes readable labels for all workspaces");
            var home = (HomeSurface)typeof(MainForm).GetField("homeSurface", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
            for (int i = 0; i < 60; i++)
            {
                tabs.SelectedIndex = i % tabs.TabCount; Application.DoEvents();
                Check(tabs.TabPages.Cast<TabPage>().Count(page => page.Visible) == 1, "Only the active workspace is visible");
                Check(!(bool)typeof(MainForm).GetField("workspaceTransition", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main), "Redraw resumes after every transition");
            }
            tabs.SelectedIndex = 0;
            Application.DoEvents();
            var feature = Descendants(home).Single(control => control.AccessibleName == "FishBowl featured games and emulators");
            var libraryBanner = Descendants(feature).OfType<Button>().Single(button => button.AccessibleName == "Open library Game Library");
            var emulatorBanner = Descendants(feature).OfType<Button>().Single(button => button.AccessibleName == "Browse emulators Emulators");
            Check(libraryBanner.Visible && emulatorBanner.Visible && !libraryBanner.Parent.Bounds.IntersectsWith(emulatorBanner.Parent.Bounds), "Both featured cards are visible and separated");
            libraryBanner.PerformClick();
            Check(tabs.SelectedIndex == 2, "Featured library action opens the existing Library");
            Check(main.Text == "Library", "The native caption follows navigation");
            tabs.SelectedIndex = 0;
            emulatorBanner.PerformClick();
            Check(tabs.SelectedIndex == 1, "Featured emulator action opens the existing Emulators workspace");
            tabs.SelectedIndex = 0;
            foreach (var button in Descendants(home).OfType<Button>()) Check(button.Height >= 28, "Home action retains its full height");
        }
        using (var organizer = new GameStorageOrganizerDialog(library, null))
        {
            Show(organizer); var buttons = Descendants(organizer).OfType<Button>().Where(button => button.Visible).ToArray();
            for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++) Check(!buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), "Organizer actions do not overlap");
        }
        using (var settings = new SettingsDialog(library.Theme, library.BackupFolder))
        {
            Show(settings); var browse = Descendants(settings).OfType<Button>().Single(b => b.Text == "Browse");
            Check(browse.Width >= TextRenderer.MeasureText(browse.Text, browse.Font).Width + 36, "Browse label has room for its icon");
            var info = Descendants(settings).OfType<CheckBox>().Single(b => b.Text == "Show emulator information pane");
            Check(info.Right <= settings.ClientSize.Width, "Information-panel checkbox fits");
        }
        using (var history = new SaveHistoryDialog(library, library.Games.FirstOrDefault()))
        {
            Show(history); Check(history.Icon != null, "Save history is branded");
            Check(history.BackColor == FishBowlPalette.ThemeBottom, "New dialogs use the current window background role");
            Check(FishBowlPalette.Contrast(history.ForeColor, history.BackColor) >= 4.5, "Window text remains readable against the dialog background");
        }
        using (var form = new Form { ClientSize = new Size(1000, 640) })
        {
            var home = new HomeSurface(library, (a,g,e) => {}); form.Controls.Add(home); Show(form);
            var first = Descendants(home).OfType<Button>().First(); float font = first.Font.Size;
            form.Scale(new SizeF(2,2)); Application.DoEvents();
            Check(first.Font.Size >= font * 1.9f, "Button text scales with controls");
            home.Reload(); Application.DoEvents();
            Check(Descendants(home).OfType<Button>().First().Font.Size >= font * 1.9f, "Reload retains the selected scale");
        }
        Console.WriteLine("PASS: " + checks + " visual layout and workspace-transition checks.");
    }
}
