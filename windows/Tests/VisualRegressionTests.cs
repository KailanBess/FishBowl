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
        using (var main = new MainForm(true))
        {
            Show(main);
            var tabs = (TabControl)typeof(MainForm).GetField("workspaceNavigation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
            Check(tabs.SizeMode == TabSizeMode.Fixed && tabs.GetTabRect(2).Width >= TextRenderer.MeasureText("Library", tabs.Font).Width + 20, "Workspace tab labels have enough width");
            var home = (HomeSurface)typeof(MainForm).GetField("homeSurface", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
            for (int i = 0; i < 60; i++)
            {
                tabs.SelectedIndex = i % 3; Application.DoEvents();
                Check(tabs.TabPages.Cast<TabPage>().Count(page => page.Visible) == 1, "Only the active workspace is visible");
                Check(!(bool)typeof(MainForm).GetField("workspaceTransition", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main), "Redraw resumes after every transition");
            }
            tabs.SelectedIndex = 0;
            foreach (var button in Descendants(home).OfType<Button>()) Check(button.Height >= 28, "Home action retains its full height");
        }
        using (var organizer = new GameStorageOrganizerDialog(library, null))
        {
            Show(organizer); var buttons = organizer.Controls.OfType<Button>().ToArray();
            for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++) Check(!buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), "Organizer actions do not overlap");
        }
        using (var settings = new SettingsDialog(library.Theme, library.BackupFolder))
        {
            Show(settings); var browse = settings.Controls.OfType<Button>().Single(b => b.Text == "Browse");
            Check(browse.Width >= TextRenderer.MeasureText(browse.Text, browse.Font).Width + 36, "Browse label has room for its icon");
            var info = settings.Controls.OfType<CheckBox>().Single(b => b.Text == "Show emulator information pane");
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
