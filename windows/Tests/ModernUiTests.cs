using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class ModernUiTests
{
    static int checks;
    static readonly BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; Console.WriteLine(text); }
    static Control[] All(Control root) { return NextUi.Descendants(root).ToArray(); }
    static void Pump() { Application.DoEvents(); }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            using (var form = new Form { Size = new Size(1000, 700), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) })
            using (var source = new ListView { Dock = DockStyle.Fill, CheckBoxes = true, MultiSelect = true, AllowDrop = true })
            {
                source.Columns.Add("Title"); source.Columns.Add("State");
                for (int i = 0; i < 100; i++) source.Items.Add(new ListViewItem(new[] { "Fixture " + i, "Ready" }));
                form.Controls.Add(source);
                int opened = -1, checkedEvents = 0, dropped = 0, sorted = -1;
                source.ItemChecked += delegate { checkedEvents++; };
                source.DragDrop += delegate { dropped++; };
                source.ColumnClick += delegate(object sender, ColumnClickEventArgs e) { sorted = e.Column; };
                var ui = typeof(MainForm).Assembly.GetType("EmulatorHub.ModernUi");
                var catalog = (Control)ui.GetMethod("Catalog", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { source, "fixtures", true, new Action<ListViewItem>(item => opened = item.Index) });
                form.Show(); Pump();
                Check(source.Left < -10000 && catalog.Visible, "native rows are not painted in the new catalog");
                Check(All(catalog).Count(c => c.GetType().Name == "ModernCatalogCard") == 48, "large catalogs page their cards");
                var card = All(catalog).First(c => c.GetType().Name == "ModernCatalogCard");
                All(card).OfType<Button>().Single().PerformClick(); Pump();
                Check(opened == 0 && source.SelectedItems.Count == 1 && source.Items[0].Selected, "card action selects the real record before activating");
                var included = All(card).OfType<CheckBox>().Single(); included.Checked = true; Pump();
                catalog.GetType().GetMethod("RefreshCards", hidden).Invoke(catalog, null);
                Check(source.Items[0].Checked && checkedEvents > 0, "card checkbox retains existing checked-item handlers");
                Check(!card.IsDisposed, "checking a row preserves card and keyboard focus");
                source.Items[0].Checked = false;
                catalog.GetType().GetMethod("RefreshCards", hidden).Invoke(catalog, null);
                Check(!included.Checked, "programmatic checked changes return to the visible control");
                source.Items[0].Text = "Updated fixture"; source.Items[0].SubItems[1].Text = "Running";
                catalog.GetType().GetMethod("RefreshCards", hidden).Invoke(catalog, null);
                Check(!card.IsDisposed && All(card).OfType<Label>().Any(label => label.Text == "Updated fixture"), "status and title refresh preserve the card and focus");
                typeof(Control).GetMethod("OnKeyDown", hidden).Invoke(card, new object[] { new KeyEventArgs(Keys.Space) });
                Check(source.Items[0].Checked, "Space toggles the backing checked item");
                Check(All(card).OfType<PictureBox>().Single().Image != null, "missing artwork has a generated fallback");
                All(catalog).OfType<Button>().Single(b => b.Text == "Next").PerformClick(); Pump();
                card = All(catalog).First(c => c.GetType().Name == "ModernCatalogCard");
                All(card).OfType<Button>().Single().PerformClick();
                Check(opened == 48, "next page routes to its actual record");
                source.Enabled = false;
                catalog.GetType().GetMethod("RefreshCards", hidden).Invoke(catalog, null);
                Check(!card.Enabled, "busy source disables its card actions"); source.Enabled = true;
                var drag = new DragEventArgs(new DataObject(), 0, 0, 0, DragDropEffects.Copy, DragDropEffects.Copy);
                typeof(Control).GetMethod("OnDragDrop", hidden).Invoke(catalog, new object[] { drag });
                Check(dropped == 1, "catalog drag-drop reaches the existing handler");
                var sort = All(catalog).OfType<Button>().Single(b => b.Text == "Sort by");
                typeof(ContextMenuStrip).GetMethod("OnOpening", hidden).Invoke(sort.ContextMenuStrip, new object[] { new System.ComponentModel.CancelEventArgs() });
                ((ToolStripMenuItem)sort.ContextMenuStrip.Items[1]).PerformClick();
                Check(sorted == 1, "sort choice reaches the existing column handler");
            }
            using (var dialog = new NextDialog("Fixture preferences", 800, 600))
            {
                dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-30000, -30000);
                var tabs = new FishBowlTabs { Dock = DockStyle.Fill }; tabs.TabPages.Add("Library"); tabs.TabPages.Add("Appearance"); dialog.Body.Controls.Add(tabs);
                dialog.Show(); Pump();
                Check(All(dialog).Any(c => c.Name == "Modern section navigation"), "dialogs use the new section navigation");
                All(dialog).OfType<Button>().Single(b => b.Text == "Appearance").PerformClick();
                Check(tabs.SelectedIndex == 1, "new navigation selects the existing feature page");
            }
            using (var dialog = new NextDialog("Fixture details", 800, 600))
            {
                var text = new TextBox { Multiline = true, ReadOnly = true, Text = "First\nSecond", Dock = DockStyle.Fill };
                dialog.Body.Controls.Add(text); dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-30000, -30000); dialog.Show(); Pump();
                Check(text.Lines.Length == 2 && text.Text.Contains("\r\n"), "read-only details retain native multiline formatting");
                text.Text = "Updated\nDetails";
                Check(text.Lines.Length == 2 && text.Lines[1] == "Details", "live read-only details retain line breaks");
            }
            var saved = Store.Load();
            saved.Theme.WindowsInterfaceVersion = 0; saved.Theme.Name = "Ocean";
            UserTools.Ensure(saved).Navigation = new LibraryNavigation { Artwork = false }; Store.Save(saved);
            using (var main = new MainForm(true))
            {
                var data = (LibraryData)typeof(MainForm).GetField("library", hidden).GetValue(main);
                Check(data.Theme.WindowsInterfaceVersion == 1 && data.Theme.Name == "Gallery Night" && data.Experience.LibraryView == "Artwork", "first opening applies the new interface preset");
                using (var settings = new SettingsDialog(data.Theme, data.BackupFolder))
                {
                    typeof(SettingsDialog).GetMethod("SaveSettings", hidden).Invoke(settings, new object[] { null, EventArgs.Empty });
                    Check(settings.Theme.WindowsInterfaceVersion == 1, "saving preferences retains the migration marker");
                }
                data.Theme.Name = "Ocean"; Store.Save(data);
            }
            using (var main = new MainForm(true))
            {
                var data = (LibraryData)typeof(MainForm).GetField("library", hidden).GetValue(main);
                Check(data.Theme.Name == "Ocean", "later appearance choices survive reopening");
            }
            Console.WriteLine("PASS: " + checks + " modern catalog, paging, feature routing and dialog navigation checks."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
