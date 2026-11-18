using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class WebRemovalTests
{
    static int checks;
    static readonly BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static IEnumerable<ToolStripItem> Items(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            yield return item;
            var menu = item as ToolStripMenuItem;
            if (menu != null) foreach (var child in Items(menu.DropDownItems)) yield return child;
        }
    }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            var saved = Store.Load();
            saved.Browser = new BrowserPreferences { Engine = "Firefox", Executable = "retired-browser.exe", HomePage = "https://example.com/legacy" };
            Store.Save(saved);
            string profiles = Path.Combine(Store.DataDirectory, "BrowserProfiles"); Directory.CreateDirectory(profiles);
            string marker = Path.Combine(profiles, "fixture.txt"); File.WriteAllText(marker, "retained");
            using (var main = new MainForm(true))
            {
                main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-30000, -30000);
                main.Show(); Application.DoEvents();
                var tabs = (TabControl)typeof(MainForm).GetField("workspaceNavigation", hidden).GetValue(main);
                Check(tabs.TabPages.Cast<TabPage>().Select(page => page.Text).SequenceEqual(new[] { "Home", "Emulators", "Library", "Play", "Settings" }), "Web is absent and Settings follows Play");
                var commands = (IEnumerable<ToolStrip>)typeof(MainForm).GetMethod("ApplicationMenus", hidden).Invoke(main, null);
                Check(!commands.SelectMany(menu => Items(menu.Items)).Any(item => item.Text == "Web browser..." || item.Text == "Browser settings..."), "browser commands are absent from application menus and search sources");
                tabs.SelectedIndex = 4; Application.DoEvents();
                Check(!NextUi.Descendants(tabs.SelectedTab).Any(control => control.Text == "Web browser" || control.Text == "Browser settings"), "Settings has no browser category");
                Check(typeof(MainForm).Assembly.GetType("EmulatorHub.BrowserSurface") == null && typeof(MainForm).Assembly.GetType("EmulatorHub.BrowserSettingsDialog") == null, "embedded browser and settings code are removed");
                var fullscreen = typeof(MainForm).GetMethod("ToggleFullScreen", hidden);
                fullscreen.Invoke(main, null); Application.DoEvents();
                var rail = (Control)typeof(MainForm).GetField("dashboardRail", hidden).GetValue(main);
                Check(rail.Controls.OfType<Button>().Single(button => button.AccessibleName == "Settings").Text.Contains("⚙"), "Settings retains its gear icon after removing Web");
                Check(rail.Visible, "fullscreen Settings retains navigation after the index shift");
                fullscreen.Invoke(main, null); main.Close();
            }
            foreach (string address in new[] { "https://example.com/help", "http://127.0.0.1:4567/client" })
            {
                var info = SystemWebLinks.StartInfo(address);
                Check(info.UseShellExecute && info.FileName == address && info.Arguments.Length == 0, "external links use the default browser without dedicated profiles");
            }
            foreach (string address in new[] { "javascript:alert(1)", "file:///C:/private", "cmd:calc", "https://user:secret@example.com/", "about:blank", "search words" })
            {
                bool rejected = false; try { SystemWebLinks.StartInfo(address); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "non-web or credential-bearing address is rejected");
            }
            Check(Store.Load().Browser.HomePage == saved.Browser.HomePage && File.ReadAllText(marker) == "retained", "legacy saved preferences and browser data are retained without being used");
            using (var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) })
            using (var timer = new Timer { Interval = 100 })
            {
                Exception failure = null; bool inspected = false;
                timer.Tick += delegate
                {
                    var dialog = Application.OpenForms.OfType<NextDialog>().FirstOrDefault(); if (dialog == null) return;
                    timer.Stop();
                    try { Check(!NextUi.Descendants(dialog).Any(c => c.Text == "Web browser" || c.Text == "Browser settings"), "General preferences has no browser controls"); inspected = true; }
                    catch (Exception error) { failure = error; }
                    finally { dialog.Close(); }
                };
                owner.Show(); timer.Start(); WindowsAppearance.General(owner, saved, delegate { });
                if (failure != null) throw failure; Check(inspected, "General preferences is inspected");
            }
            Console.WriteLine("PASS: " + checks + " Web removal, navigation, external-link safety and saved-data compatibility checks."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
