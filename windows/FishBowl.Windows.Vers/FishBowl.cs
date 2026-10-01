using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EmulatorHub
{

    public partial class MainForm : Form
    {
        private string DisplayFont { get { return String.IsNullOrWhiteSpace(library.Theme.FontFamily) ? "Bahnschrift" : library.Theme.FontFamily; } }
        private readonly Icon ownedAppIcon;
        private bool visualResourcesDisposed;
        private readonly LibraryData library = Store.Load();
        private readonly ListView emulatorList = new SmoothListView();
        private readonly ImageList emulatorImages = new ImageList();
        private readonly List<Image> emulatorImageSources = new List<Image>();
        private readonly TextBox filterBox = new TextBox();
        private readonly Label status = new Label();
        private readonly Button editButton = new FishBowlActionButton();
        private readonly Button removeButton = new FishBowlActionButton();
        private readonly ComboBox platformFilter = new ComboBox();
        private readonly CheckBox favoritesOnly = new CheckBox();
        private readonly Button favoriteButton = new FishBowlActionButton();
        private readonly Button informationButton = new FishBowlActionButton();
        private readonly Label infoTitle = new Label();
        private readonly Label infoVersion = new Label();
        private readonly Label infoPlatform = new Label();
        private readonly TextBox overviewText = new TextBox();
        private readonly TextBox setupText = new TextBox();
        private readonly TextBox controllersText = new TextBox();
        private readonly TextBox helpText = new TextBox();
        private readonly TextBox notesBox = new TextBox();
        private readonly Label notesState = new Label();
        private readonly TabControl infoTabs = new FishBowlTabs();
        private readonly TableLayoutPanel folderTable = new TableLayoutPanel();
        private readonly Timer notesTimer = new Timer { Interval = 600 };
        private readonly Timer gameFolderTimer = new Timer { Interval = 1100 };
        private readonly List<FileSystemWatcher> gameFolderWatchers = new List<FileSystemWatcher>();
        private readonly List<Tuple<Button, Func<EmulatorReference, string>>> referenceButtons = new List<Tuple<Button, Func<EmulatorReference, string>>>();
        private string notesProfileId;
        private bool loadingInformation, notesDirty, loadingFilters;
        private readonly ToolStripMenuItem linksMenu = new ToolStripMenuItem("Links");
        private readonly ToolStripMenuItem convertersMenu = new ToolStripMenuItem("Game converters");
        private string selectedEmulatorId;
        private string quickFilter = "All";
        private string hoveredEmulatorId;
        private SplitContainer emulatorSplit;
        private Control emulatorInformation;
        private bool refreshing;
        private bool selectionPending;
        private bool reloadProgramMetadata;
        private int metadataGeneration;
        private int folderRequest;
        private string folderRequestKey;
        private readonly Dictionary<string, Image> emulatorIconCache = new Dictionary<string, Image>();
        private readonly Dictionary<string, Tuple<DateTime, System.Threading.Tasks.Task<string>>> versionCache = new Dictionary<string, Tuple<DateTime, System.Threading.Tasks.Task<string>>>();
        private bool fullScreen;
        private FormBorderStyle savedBorderStyle;
        private FormWindowState savedWindowState;
        private Rectangle savedBounds;
        private Rectangle savedNormalBounds;
        private Color ink, top, bottom, surface, subtle, blue, pink;
        private Color[] swatches = { Color.FromArgb(162, 82, 241), Color.FromArgb(255, 137, 48), Color.FromArgb(207, 94, 238) };

        public MainForm()
        {
            Text = "FishBowl 1.0";
            ownedAppIcon = LoadAppIcon(); Icon = ownedAppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            Size = new Size(1340, 820);
            RestoreWindowLayout();
            if (library.Theme.StartMaximized) WindowState = FormWindowState.Maximized;
            ApplyThemeColors();
            BackColor = bottom;
            KeyPreview = true;
            selectedEmulatorId = null;
            BuildLayout();
            if (library.Theme.EnableMotion) FishBowlHighlights.Attach(this);
            RefreshHub();
            ApplyVisualScale();
            ConfigureGameFolderWatchers();
            SetStatus("Double-click an emulator to open it. Manage games inside the emulator.");
            KeyDown += OnKeyDown;
            runtimeTimer.Tick += delegate { RefreshRuntimeStatus(); }; if (!library.Theme.PauseBackgroundRefresh) runtimeTimer.Start();
            notesTimer.Tick += delegate { RunUiAction(SaveProfileNotes); };
            gameFolderTimer.Tick += delegate { gameFolderTimer.Stop(); RunUiAction(SyncWatchedGameFolders); };
            Shown += delegate { if (library.Theme.ShowStartupAssistant) ShowFirstRunGuide(); if (library.Theme.ShowGameStorageAssistant) ShowGameStoragePrompt(); if (library.Theme.ShowRequirementsStorageAssistant) ShowRequirementsStoragePrompt(); if (library.Theme.CheckHealthOnStartup) ShowNotifications(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!e.Cancel) { try { SaveProfileNotes(); SaveWindowLayout(); } catch (Exception error) { e.Cancel = true; MessageBox.Show(this, "Your notes could not be saved.\n\n" + error.Message, "FishBowl"); } } };
        }

        private void BuildLayout()
        {
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = bottom };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, library.Theme.ShowBanner ? 76 : 0));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, library.Theme.ShowStatusBar ? 38 : 0));
            Controls.Add(shell);
            shell.Controls.Add(BuildMenu(), 0, 0);
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = top, Padding = new Padding(12) };
            var logo = LoadLogo();
            var picture = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(60, 60), Location = new Point(12, 8) };
            picture.Disposed += delegate { if (logo != null) logo.Dispose(); };
            banner.Controls.Add(picture);
            banner.Controls.Add(new Label { Text = "FishBowl", ForeColor = ink, Font = new Font(DisplayFont, 18, FontStyle.Bold), AutoSize = true, Location = new Point(84, 8) });
            banner.Controls.Add(new Label { Text = "Your emulators, together. Add and play games inside each emulator.", ForeColor = subtle, Font = new Font(DisplayFont, 9), AutoSize = true, Location = new Point(86, 43) });
            shell.Controls.Add(banner, 0, 1);
            shell.Controls.Add(BuildPrimaryActions(), 0, 2);
            int iconSize = library.Theme.ListDensity == "Compact" ? 40 : library.Theme.ListDensity == "Comfortable" ? 56 : 48;
            emulatorImages.ImageSize = new Size(iconSize, iconSize);
            emulatorImages.ColorDepth = ColorDepth.Depth32Bit;
            emulatorList.Dock = DockStyle.Fill;
            emulatorList.BackColor = bottom;
            emulatorList.ForeColor = ink;
            emulatorList.Font = new Font(DisplayFont, 10);
            emulatorList.View = View.Details;
            emulatorList.FullRowSelect = true;
            emulatorList.BorderStyle = BorderStyle.None;
            emulatorList.OwnerDraw = true;
            emulatorList.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (var brush = new SolidBrush(surface)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, emulatorList.Font, Rectangle.Inflate(e.Bounds, -8, 0), subtle, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            emulatorList.DrawItem += delegate { };
            emulatorList.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var selected = e.Item.Selected;
                var selectionAlpha = library.Theme.SelectionContrast == "Soft" ? 58 : library.Theme.SelectionContrast == "Strong" ? 132 : 88;
                var background = selected ? Color.FromArgb(selectionAlpha, blue.R, blue.G, blue.B) : (library.Theme.AlternateRowShading && e.Item.Index % 2 != 0 ? Color.FromArgb(20, surface) : bottom);
                using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (selected)
                {
                    using (var border = new Pen(Color.FromArgb(207, 168, 255)))
                    {
                        e.Graphics.DrawLine(border, e.Bounds.Left, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Top);
                        e.Graphics.DrawLine(border, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                        if (e.ColumnIndex == emulatorList.Columns.Count - 1) e.Graphics.DrawLine(border, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                    }
                    if (e.ColumnIndex == 0) using (var accent = new SolidBrush(Color.FromArgb(255, 164, 82))) e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height);
                }
                var bounds = Rectangle.Inflate(e.Bounds, -8, 0);
                if (library.Theme.ShowEmulatorIcons && e.ColumnIndex == 0 && e.Item.ImageIndex >= 0)
                {
                    var iconBounds = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + (e.Bounds.Height - 32) / 2, 32, 32);
                    var tile = new RectangleF(iconBounds.Left - 4, iconBounds.Top - 4, 40, 40); var accentColor = swatches[e.Item.Index % swatches.Length];
                    int radius = library.Theme.IconTileShape == "Square" ? 2 : library.Theme.IconTileShape == "Circular" ? 20 : 7;
                    using (var tilePath = FishBowlVisuals.Round(tile, radius)) using (var tileFill = new SolidBrush(Color.FromArgb(18, accentColor))) using (var tilePen = new Pen(Color.FromArgb(58, accentColor))) { e.Graphics.FillPath(tileFill, tilePath); e.Graphics.DrawPath(tilePen, tilePath); }
                    if (library.Theme.EnableMotion && (selected || (e.Item.Tag as string) == hoveredEmulatorId)) FishBowlHighlights.Draw(e.Graphics, Rectangle.Inflate(iconBounds, 2, 2), background, selected);
                    e.Graphics.DrawImage(emulatorImageSources[e.Item.ImageIndex], iconBounds);
                    bounds = new Rectangle(e.Bounds.Left + 58, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 66), e.Bounds.Height);
                }
                var color = selected ? Color.White : ink;
                if (e.ColumnIndex == 1)
                {
                    bool available = e.SubItem.Text == "Ready" || e.SubItem.Text == "Running";
                    var dot = available ? (bottom.GetBrightness() < 0.65f ? Color.FromArgb(118,211,161) : Color.FromArgb(35,126,82)) : e.SubItem.Text == "Missing program" ? Color.FromArgb(246,183,105) : subtle;
                    using (var brush = new SolidBrush(dot)) e.Graphics.FillEllipse(brush, bounds.Left + 1, e.Bounds.Top + (e.Bounds.Height - 6) / 2, 6, 6);
                    bounds = new Rectangle(bounds.Left + 14, bounds.Top, Math.Max(0,bounds.Width - 14), bounds.Height);
                    color = selected ? Color.White : dot;
                }
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, emulatorList.Font, bounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            emulatorList.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                var hovered = emulatorList.GetItemAt(e.X, e.Y); var next = hovered == null ? null : hovered.Tag as string;
                if (hoveredEmulatorId == next) return; var previous = hoveredEmulatorId; hoveredEmulatorId = next; InvalidateEmulatorRow(previous); InvalidateEmulatorRow(next);
            };
            emulatorList.MouseLeave += delegate { var previous = hoveredEmulatorId; hoveredEmulatorId = null; InvalidateEmulatorRow(previous); };
            emulatorList.HideSelection = false;
            emulatorList.MultiSelect = false;
            emulatorList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            emulatorList.SmallImageList = emulatorImages;
            emulatorList.Columns.Add("Emulator", 270);
            emulatorList.Columns.Add("Status", 140);
            emulatorList.Columns.Add("Platform / preset", 320);
            emulatorList.Columns.Add("Program", 240);
            emulatorList.Resize += delegate { ResizeEmulatorColumns(); };
            emulatorList.SelectedIndexChanged += delegate
            {
                if (refreshing) return;
                // Native single-select lists notify deselection before selecting the next row.
                // Wait for the next message only when empty, keeping the details pane stable.
                if (emulatorList.SelectedItems.Count != 0) { ApplyEmulatorSelection(); return; }
                if (selectionPending || !IsHandleCreated) return;
                selectionPending = true;
                BeginInvoke(new MethodInvoker(delegate { selectionPending = false; if (!IsDisposed && !refreshing) ApplyEmulatorSelection(); }));
            };
            emulatorList.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                var item = emulatorList.GetItemAt(e.X, e.Y);
                if (item == null) return;
                item.Selected = true;
                selectedEmulatorId = item.Tag as string;
                RunUiAction(OpenSelectedEmulator);
            };
            var context = new ContextMenuStrip { Renderer = new FishBowlMenuRenderer(), BackColor = surface, ForeColor = ink, ImageScalingSize = new Size(20,20) };
            context.Items.Add(MenuAction("Open emulator", "play", OpenSelectedEmulator));
            context.Items.Add(MenuAction("Edit emulator", "edit", EditEmulator));
            context.Items.Add(MenuAction("Favorite / unfavorite", "star", ToggleEmulatorFavorite));
            context.Items.Add(MenuAction("Edit information and links", "info", EditEmulatorInformation));
            context.Items.Add(MenuAction("Open program folder", "folder", OpenEmulatorFolder));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(MenuAction("Remove from FishBowl", "remove", RemoveEmulator));
            emulatorList.ContextMenuStrip = context;
            emulatorList.Disposed += delegate { context.Dispose(); };
            emulatorList.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
                var item = emulatorList.GetItemAt(e.X, e.Y);
                if (item == null) ClearEmulatorSelection(); else if (e.Button == MouseButtons.Right) item.Selected = true;
            };
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1300, 550), SplitterWidth = 8, BackColor = top, Panel1MinSize = 450, Panel2MinSize = 370, SplitterDistance = 770 };
            emulatorSplit = split; emulatorInformation = BuildInformationPanel();
            split.Panel1.Controls.Add(emulatorList);
            split.Panel2.Controls.Add(emulatorInformation);
            split.Panel2Collapsed = !library.Theme.ShowInformationPanel;
            shell.Controls.Add(split, 0, 3);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = top, Padding = new Padding(8, 5, 8, 5) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.Controls.Add(new Label { Text = "Filter:", ForeColor = subtle, Font = new Font(DisplayFont, 9), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            filterBox.Dock = DockStyle.Fill;
            filterBox.BackColor = surface; filterBox.ForeColor = ink; filterBox.BorderStyle = BorderStyle.FixedSingle;
            filterBox.TextChanged += delegate { RefreshHub(); };
            footer.Controls.Add(filterBox, 1, 0);
            status.Dock = DockStyle.Fill; status.ForeColor = subtle; status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft; status.Margin = new Padding(14, 0, 0, 0);
            footer.Controls.Add(status, 2, 0);
            shell.Controls.Add(footer, 0, 4);
            foreach (var control in new Control[] { this, emulatorList, banner })
            {
                control.AllowDrop = true;
                control.DragEnter += delegate(object sender, DragEventArgs e) { var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effect = paths != null && paths.Any(p => EmulatorReference.IsLaunchFile(p)) ? DragDropEffects.Copy : DragDropEffects.None; };
                control.DragDrop += delegate(object sender, DragEventArgs e) { RunUiAction(delegate { HandleDroppedPaths(e.Data.GetData(DataFormats.FileDrop) as string[]); }); };
            }
        }

        private Control BuildInformationPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = top };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            infoTitle.Font = new Font(DisplayFont, 17, FontStyle.Bold); infoTitle.ForeColor = ink; infoTitle.Dock = DockStyle.Fill; infoTitle.AutoEllipsis = true;
            infoVersion.ForeColor = subtle; infoVersion.Dock = DockStyle.Fill; infoVersion.AutoEllipsis = true;
            infoPlatform.ForeColor = subtle; infoPlatform.Dock = DockStyle.Fill; infoPlatform.AutoEllipsis = true;
            panel.Controls.Add(infoTitle, 0, 0); panel.Controls.Add(infoVersion, 0, 1); panel.Controls.Add(infoPlatform, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ConfigureButton(favoriteButton, "Favorite", ToggleEmulatorFavorite, surface);
            ConfigureButton(informationButton, "Edit information", EditEmulatorInformation, surface);
            actions.Controls.Add(favoriteButton); actions.Controls.Add(informationButton); panel.Controls.Add(actions, 0, 3);
            infoTabs.Dock = DockStyle.Fill; infoTabs.Font = new Font(DisplayFont, 9);
            BuildInfoTab("Overview", overviewText, new[] { Tuple.Create("Project", new Func<EmulatorReference, string>(r => r.Website)), Tuple.Create("Releases", new Func<EmulatorReference, string>(r => r.Releases)), Tuple.Create("Changelog", new Func<EmulatorReference, string>(r => r.Changelog)) });
            BuildInfoTab("Setup", setupText, new[] { Tuple.Create("Setup guide", new Func<EmulatorReference, string>(r => r.Documentation)), Tuple.Create("Compatibility", new Func<EmulatorReference, string>(r => r.Compatibility)) });
            var controllers = BuildInfoTab("Controls", controllersText, new[] { Tuple.Create("Controller guide", new Func<EmulatorReference, string>(r => r.ControllerGuide)) });
            var windowsControllers = new FishBowlActionButton(); ConfigureButton(windowsControllers, "Windows controllers", delegate { Process.Start(new ProcessStartInfo("control.exe", "joy.cpl") { UseShellExecute = true }); }, surface);
            controllers.Controls.Add(windowsControllers);
            var folders = new TabPage("Folders") { BackColor = bottom, Padding = new Padding(10), AutoScroll = true };
            folderTable.Dock = DockStyle.Top; folderTable.AutoSize = true; folderTable.ColumnCount = 4;
            folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            folders.Controls.Add(folderTable); infoTabs.TabPages.Add(folders);
            BuildInfoTab("Help", helpText, new[] { Tuple.Create("Troubleshooting", new Func<EmulatorReference, string>(r => r.Troubleshooting)) });
            var notes = new TabPage("Notes") { BackColor = bottom, Padding = new Padding(10) };
            var noteLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            noteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); noteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            StyleInfoText(notesBox, false); notesBox.TextChanged += delegate { if (loadingInformation) return; notesDirty = true; notesState.Text = "Saving..."; notesTimer.Stop(); notesTimer.Start(); };
            notesState.ForeColor = subtle; notesState.Dock = DockStyle.Fill; notesState.TextAlign = ContentAlignment.MiddleLeft;
            noteLayout.Controls.Add(notesBox, 0, 0); noteLayout.Controls.Add(notesState, 0, 1); notes.Controls.Add(noteLayout); infoTabs.TabPages.Add(notes);
            FishBowlVisuals.Tabs(infoTabs, top, ink, blue, "info", "settings", "controller", "folder", "help", "note");
            infoTabs.SelectedIndexChanged += delegate { EnsureFolderRows(); };
            panel.Controls.Add(infoTabs, 0, 4);
            return panel;
        }
        private void StyleInfoText(TextBox box, bool readOnly)
        {
            box.Multiline = true; box.ReadOnly = readOnly; box.ScrollBars = ScrollBars.Vertical; box.Dock = DockStyle.Fill;
            box.BackColor = bottom; box.ForeColor = ink; box.BorderStyle = BorderStyle.None; box.Font = new Font(DisplayFont, 10); box.WordWrap = true;
        }
        private FlowLayoutPanel BuildInfoTab(string title, TextBox box, IEnumerable<Tuple<string, Func<EmulatorReference, string>>> links)
        {
            var tab = new TabPage(title) { BackColor = bottom, Padding = new Padding(10) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            StyleInfoText(box, true); layout.Controls.Add(box, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
            foreach (var link in links)
            {
                var button = new FishBowlActionButton(); var getUrl = link.Item2;
                ConfigureButton(button, link.Item1, delegate { var profile = CurrentEmulator(); if (profile != null) OpenInformationLink(getUrl(EmulatorReference.For(profile))); }, surface);
                button.Margin = new Padding(0, 3, 6, 3); actions.Controls.Add(button); referenceButtons.Add(Tuple.Create((Button)button, getUrl));
            }
            layout.Controls.Add(actions, 0, 1); tab.Controls.Add(layout); infoTabs.TabPages.Add(tab); return actions;
        }
        private void UpdateInformationPanel()
        {
            var profile = CurrentEmulator();
            if (emulatorInformation != null) emulatorInformation.Visible = profile != null;
            bool hideInformation = profile == null || !library.Theme.ShowInformationPanel;
            if (emulatorSplit != null && emulatorSplit.Panel2Collapsed != hideInformation) { emulatorSplit.Panel2Collapsed = hideInformation; ResizeEmulatorColumns(); }
            var nextId = profile == null ? null : profile.Id;
            if (notesProfileId != nextId)
            {
                folderRequest++; folderRequestKey = null;
                SaveProfileNotes(); loadingInformation = true;
                notesProfileId = nextId; notesBox.Text = profile == null ? "" : (profile.Notes ?? ""); notesDirty = false;
                notesState.Text = profile == null ? "Select an emulator to write notes." : "Notes save automatically."; loadingInformation = false;
            }
            favoriteButton.Enabled = informationButton.Enabled = notesBox.Enabled = profile != null;
            infoTitle.Text = profile == null ? "Emulator information" : profile.Name;
            var version = profile == null ? "" : String.IsNullOrWhiteSpace(profile.ManualVersion) ? "Checking version..." : profile.ManualVersion + " (entered manually)";
            infoVersion.Text = profile == null ? "" : "Installed version: " + version;
            infoPlatform.Text = profile == null ? "" : EmulatorReference.PlatformFor(profile);
            favoriteButton.Text = profile != null && profile.Favorite ? "Favorited" : "Favorite";
            if (profile == null)
            {
                overviewText.Text = "Select an emulator to see its information.\r\n\r\nAny Windows emulator can be added using Custom. Double-click a row to open it.";
                setupText.Text = controllersText.Text = helpText.Text = "Select an emulator first.";
                foreach (var link in referenceButtons) link.Item1.Enabled = false;
                folderRequest++; folderRequestKey = null; return;
            }
            var info = EmulatorReference.For(profile);
            overviewText.Text = info.Summary + "\r\n\r\nSTRENGTHS\r\n" + info.Strengths + "\r\n\r\nLIMITATIONS\r\n" + info.Limitations + "\r\n\r\nPROGRAM\r\n" + profile.Executable + "\r\n\r\nVERSION\r\n" + version;
            setupText.Text = info.Requirements + "\r\n\r\nCOMPATIBILITY\r\n" + (String.IsNullOrWhiteSpace(info.Compatibility) ? "No separate compatibility page is linked for this profile. Check the project documentation, or add one in Edit information." : "Open Compatibility below to view the linked compatibility page in your browser. No games are imported into FishBowl.");
            controllersText.Text = info.Controllers + (String.IsNullOrWhiteSpace(profile.ControllerProfileNotes) ? "" : "\r\n\r\nYOUR CONTROLLER NOTES\r\n" + profile.ControllerProfileNotes);
            helpText.Text = "PROGRAM WILL NOT OPEN\r\nCheck that the executable exists and works directly from Windows. Re-select its path in Edit emulator after moving or updating it.\r\n\r\nFIRMWARE / BIOS ERRORS\r\nFollow the emulator's setup guide and check its configured firmware locations.\r\n\r\nGRAPHICS / PERFORMANCE\r\nReview the emulator's hardware requirements, update your graphics driver, and use a supported graphics backend. Change settings inside the emulator.\r\n\r\nCONTROLLER NOT DETECTED\r\nConnect it before opening the emulator, check Windows controllers, and review the emulator's input mapping and controller guide.\r\n\r\nLOGS\r\nUse the Folders tab to open your configured emulator log folder. FishBowl's own log is available from Tools.\r\n\r\nUse the linked troubleshooting guide for emulator-specific instructions.";
            foreach (var link in referenceButtons) link.Item1.Enabled = EmulatorReference.IsWebUrl(link.Item2(info));
            UpdateInstalledVersion(profile);
            EnsureFolderRows();
        }
        private void SaveProfileNotes()
        {
            notesTimer.Stop(); if (!notesDirty || notesProfileId == null) return;
            var profile = library.Emulators.FirstOrDefault(p => p.Id == notesProfileId);
            if (profile == null) { notesDirty = false; return; }
            profile.Notes = notesBox.Text; Store.Save(library); notesDirty = false; notesState.Text = "Saved.";
        }
        private void RefreshPlatformOptions()
        {
            var items = new[] { "All platforms" }.Concat(library.Emulators.SelectMany(EmulatorReference.PlatformTags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s)).ToArray();
            if (platformFilter.Items.Cast<string>().SequenceEqual(items)) return;
            var selected = platformFilter.SelectedItem as string;
            loadingFilters = true; platformFilter.Items.Clear(); platformFilter.Items.AddRange(items);
            platformFilter.SelectedItem = items.Contains(selected) ? selected : "All platforms"; loadingFilters = false;
        }
        private void ToggleEmulatorFavorite()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes(); profile.Favorite = !profile.Favorite; Store.Save(library); RefreshHub();
        }
        private void EditEmulatorInformation()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes(); using (var dialog = new EmulatorInformationDialog(profile))
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; Store.Save(library); RefreshHub(); SetStatus("Saved information for " + profile.Name + "."); }
        }
        private void OpenInformationLink(string url)
        {
            if (!EmulatorReference.IsWebUrl(url)) throw new InvalidDataException("Enter an http or https web address in Edit information.");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        private void PostUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new MethodInvoker(delegate { if (!IsDisposed && !Disposing) action(); })); }
                catch (InvalidOperationException) { /* Window closed while work completed. */ }
            }
            else action();
        }

        private static EmulatorProfile FolderSnapshot(EmulatorProfile profile)
        {
            return new EmulatorProfile { Id = profile.Id, Name = profile.Name, Executable = profile.Executable, Preset = profile.Preset, ManualVersion = profile.ManualVersion,
                ConfigFolder = profile.ConfigFolder, InGameSaveFolder = profile.InGameSaveFolder, SaveStateFolder = profile.SaveStateFolder,
                ScreenshotFolder = profile.ScreenshotFolder, LogFolder = profile.LogFolder, SaveFolder = profile.SaveFolder };
        }

        private static string FolderKey(EmulatorProfile p)
        {
            return new JavaScriptSerializer().Serialize(new[] { p.Id, p.Name, p.Executable, p.Preset, p.ConfigFolder, p.InGameSaveFolder, p.SaveStateFolder, p.ScreenshotFolder, p.LogFolder, p.SaveFolder });
        }

        private void EnsureFolderRows()
        {
            var profile = CurrentEmulator();
            if (profile == null || infoTabs.SelectedIndex != 3) return;
            if (folderRequestKey != FolderKey(profile)) RefreshFolderRows(profile);
        }

        private async void RefreshFolderRows(EmulatorProfile profile)
        {
            int request = ++folderRequest; folderRequestKey = profile == null ? null : FolderKey(profile);
            if (profile == null || IsDisposed) return;
            // Detect settings and path availability off the UI thread; never change emulator settings.
            var snapshot = FolderSnapshot(profile); string notice = "";
            folderTable.SuspendLayout();
            try { while (folderTable.Controls.Count > 0) folderTable.Controls[0].Dispose(); folderTable.RowStyles.Clear(); folderTable.RowCount = 0; AddFolderMessage("Checking emulator folders...", 38); }
            finally { folderTable.ResumeLayout(true); }
            try
            {
                await System.Threading.Tasks.Task.Delay(60).ConfigureAwait(false);
                if (IsDisposed || request != folderRequest) return;
                var detected = await System.Threading.Tasks.Task.Run(delegate {
                    var detector = new EmulatorFolderDetector(); var locations = detector.Detect(snapshot); notice = detector.Notice;
                    var existing = new HashSet<string>(locations.Where(l => !String.IsNullOrWhiteSpace(l.Path) && Directory.Exists(l.Path)).Select(l => l.Path), StringComparer.OrdinalIgnoreCase);
                    return Tuple.Create(locations, existing);
                }).ConfigureAwait(false);
                PostUi(delegate {
                    if (request != folderRequest || CurrentEmulator() == null || CurrentEmulator().Id != profile.Id || FolderKey(profile) != folderRequestKey) return;
                    RenderFolderRows(profile, detected.Item1, notice, detected.Item2);
                });
            }
            catch (Exception ex)
            {
                if (IsDisposed || request != folderRequest) return;
                Store.Log("Folder shortcuts could not refresh: " + ex.Message);
                PostUi(delegate { if (request != folderRequest) return; folderRequestKey = null; RenderFolderRows(profile, new List<EmulatorFolder>(), "Folder detection could not finish. Select another tab and return to retry.", new HashSet<string>()); });
            }
        }

        private async void UpdateInstalledVersion(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
            var executable = profile.Executable ?? ""; int generation = metadataGeneration;
            Tuple<DateTime, System.Threading.Tasks.Task<string>> cached;
            if (!versionCache.TryGetValue(executable, out cached) || DateTime.UtcNow - cached.Item1 > TimeSpan.FromMinutes(1))
            {
                var snapshot = FolderSnapshot(profile);
                cached = Tuple.Create(DateTime.UtcNow, System.Threading.Tasks.Task.Run(() => EmulatorReference.VersionFor(snapshot)));
                versionCache[executable] = cached;
            }
            var version = await cached.Item2.ConfigureAwait(false);
            PostUi(delegate {
                if (generation != metadataGeneration || CurrentEmulator() != profile || profile.Executable != executable || !String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
                infoVersion.Text = "Installed version: " + version;
                int marker = overviewText.Text.LastIndexOf("\r\n\r\nVERSION\r\n", StringComparison.Ordinal);
                if (marker >= 0) overviewText.Text = overviewText.Text.Substring(0, marker) + "\r\n\r\nVERSION\r\n" + version;
            });
        }

        private void RenderFolderRows(EmulatorProfile profile, List<EmulatorFolder> locations, string notice, HashSet<string> existing)
        {
            var host = folderTable.Parent; host.SuspendLayout(); emulatorInformation.SuspendLayout();
            folderTable.SuspendLayout(); folderTable.AutoSize = false;
            try
            {
                while (folderTable.Controls.Count > 0) folderTable.Controls[0].Dispose();
                folderTable.RowStyles.Clear(); folderTable.RowCount = 0;
                if (profile == null) return;
                AddFolderMessage("In-game saves: progress saved by the game.\nSave states: snapshots created by the emulator.\nChoose sets a FishBowl shortcut. Auto restores detection.", 60);
                var refresh = new FishBowlActionButton { Text = "Refresh detected folders", Height = 30, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                refresh.Click += delegate { RunUiAction(delegate { RefreshFolderRows(profile); }); };
                int row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                folderTable.Controls.Add(refresh, 0, row); folderTable.SetColumnSpan(refresh, 4);
                if (!String.IsNullOrWhiteSpace(notice)) AddFolderMessage(notice, 54);
                foreach (var location in locations) AddFolderRow(location, profile, existing.Contains(location.Path));
                if (!String.IsNullOrWhiteSpace(profile.SaveFolder))
                {
                    var classify = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
                    foreach (var kind in new[] { "InGameSaveFolder|Use as in-game saves", "SaveStateFolder|Use as save states" })
                    {
                        var pieces = kind.Split('|'); var destination = pieces[0];
                        var button = new FishBowlActionButton { Text = pieces[1], AutoSize = true, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                        button.Click += delegate { RunUiAction(delegate { profile.GetType().GetProperty(destination).SetValue(profile, profile.SaveFolder, null); profile.SaveFolder = ""; Store.Save(library); RefreshFolderRows(profile); }); };
                        classify.Controls.Add(button);
                    }
                    row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                    folderTable.Controls.Add(classify, 0, row); folderTable.SetColumnSpan(classify, 4);
                }
            }
            finally { folderTable.AutoSize = true; folderTable.ResumeLayout(true); emulatorInformation.ResumeLayout(true); host.ResumeLayout(true); }
        }
        private void AddFolderMessage(string text, int height)
        {
            int row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var label = new Label { Text = text, Dock = DockStyle.Fill, ForeColor = subtle, Padding = new Padding(0, 2, 0, 2) };
            folderTable.Controls.Add(label, 0, row); folderTable.SetColumnSpan(label, 4);
        }
        private void AddFolderRow(EmulatorFolder location, EmulatorProfile profile, bool exists)
        {
            var path = location.Path; var property = location.Property == "Program" ? null : location.Property;
            int row = folderTable.RowCount; folderTable.RowCount += 3;
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            var heading = new Label { Text = location.Label, Dock = DockStyle.Fill, ForeColor = ink, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            folderTable.Controls.Add(heading, 0, row); folderTable.SetColumnSpan(heading, 4);
            var value = new TextBox { Text = String.IsNullOrWhiteSpace(path) ? "No single folder detected" : path, ReadOnly = true, Dock = DockStyle.Fill, BackColor = surface, ForeColor = ink, BorderStyle = BorderStyle.FixedSingle };
            folderTable.Controls.Add(value, 0, row + 1);
            var open = new FishBowlActionButton { Text = "Open", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink, Enabled = exists };
            open.Click += delegate { RunUiAction(delegate { if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }); };
            folderTable.Controls.Add(open, 1, row + 1);
            var source = new Label { Text = location.Source + (!String.IsNullOrWhiteSpace(path) && !exists ? " • folder missing" : ""), Dock = DockStyle.Fill, ForeColor = subtle, AutoEllipsis = true };
            folderTable.Controls.Add(source, 0, row + 2); folderTable.SetColumnSpan(source, 4);
            if (property != null)
            {
                var choose = new FishBowlActionButton { Text = "Choose", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                choose.Click += delegate { RunUiAction(delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the folder already used by " + profile.Name + " for " + location.Label.ToLowerInvariant(), SelectedPath = Directory.Exists(path) ? path : "", ShowNewFolderButton = false }) { if (dialog.ShowDialog(this) != DialogResult.OK) return; profile.GetType().GetProperty(property).SetValue(profile, dialog.SelectedPath, null); Store.Save(library); RefreshFolderRows(profile); } }); };
                folderTable.Controls.Add(choose, 2, row + 1);
                var auto = new FishBowlActionButton { Text = "Auto", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink, Enabled = !String.IsNullOrWhiteSpace(profile.GetType().GetProperty(property).GetValue(profile, null) as string) };
                auto.Click += delegate { RunUiAction(delegate { profile.GetType().GetProperty(property).SetValue(profile, "", null); Store.Save(library); RefreshFolderRows(profile); }); };
                folderTable.Controls.Add(auto, 3, row + 1);
            }
        }

        private Control BuildPrimaryActions()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = top, Padding = new Padding(8, 5, 8, 5), WrapContents = false };
            var add = new FishBowlActionButton();
            ConfigureButton(add, "Add emulator", AddEmulator, surface);
            ConfigureButton(editButton, "Edit emulator", EditEmulator, surface);
            ConfigureButton(removeButton, "Remove", RemoveEmulator, surface);
            ConfigureButton(managementButton, "Manage", delegate { ShowEmulatorManager("Setup checks"); }, surface);
            bar.Controls.Add(add); bar.Controls.Add(editButton); bar.Controls.Add(removeButton); bar.Controls.Add(managementButton);
            favoritesOnly.Text = "Favorites only"; favoritesOnly.ForeColor = ink; favoritesOnly.AutoSize = true; favoritesOnly.Margin = new Padding(16, 6, 12, 0);
            favoritesOnly.CheckedChanged += delegate { RunUiAction(RefreshHub); };
            bar.Controls.Add(favoritesOnly);
            platformFilter.DropDownStyle = ComboBoxStyle.DropDownList; platformFilter.Width = 240; platformFilter.Margin = new Padding(0, 3, 0, 0);
            platformFilter.BackColor = surface; platformFilter.ForeColor = ink;
            platformFilter.SelectedIndexChanged += delegate { if (!loadingFilters) RunUiAction(RefreshHub); };
            bar.Controls.Add(platformFilter);
            return bar;
        }

        private void ConfigureButton(Button button, string text, Action action, Color background)
        {
            button.Text = text; button.AutoSize = true; button.Height = 32;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            bool primary = text == "Add emulator"; button.BackColor = primary ? blue : background; button.ForeColor = primary || background == blue ? top : ink;
            button.Font = new Font(DisplayFont, 9, primary || background == blue ? FontStyle.Bold : FontStyle.Regular);
            button.Padding = new Padding(12, 0, 12, 0); button.Margin = new Padding(0, 0, 8, 0);
            button.Click += delegate { RunUiAction(action); };
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { Dock = DockStyle.Fill, BackColor = top, ForeColor = ink, Renderer = new FishBowlMenuRenderer(), ImageScalingSize = new Size(20,20) };
            var file = new ToolStripMenuItem("File");
            file.DropDownItems.Add(MenuAction("Add emulator...", "add", AddEmulator));
            file.DropDownItems.Add(MenuAction("Setup assistant / import emulator ZIP...", "add", delegate { ShowEmulatorManager("Setup assistant"); }));
            file.DropDownItems.Add(MenuAction("Game storage organizer...", "folder", delegate { ShowGameStorageOrganizer(null); }));
            file.DropDownItems.Add(MenuAction("Requirements storage organizer...", "storage", delegate { ShowRequirementsStorageOrganizer(null); }));
            file.DropDownItems.Add(MenuAction("Open selected emulator", "play", OpenSelectedEmulator));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(MenuAction("Exit", "power", Close));
            var tools = new ToolStripMenuItem("Tools");
            tools.DropDownItems.Add(MenuAction("Edit selected emulator...", "edit", EditEmulator));
            tools.DropDownItems.Add(MenuAction("Edit information and links...", "info", EditEmulatorInformation));
            tools.DropDownItems.Add(MenuAction("Favorite / unfavorite emulator", "star", ToggleEmulatorFavorite));
            tools.DropDownItems.Add(MenuAction("Open emulator folder", "folder", OpenEmulatorFolder));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MenuAction("Command palette...", "search", ShowCommandPalette));
            tools.DropDownItems.Add(MenuAction("Controller profiles...", "controller", ShowControllerProfiles));
            tools.DropDownItems.Add(MenuAction("Notifications / health...", "info", ShowNotifications));
            tools.DropDownItems.Add(MenuAction("Setup workspace...", "check", ShowSetupWorkspace));
            tools.DropDownItems.Add(MenuAction("More preferences...", "settings", ShowMorePreferences));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MenuAction("Export FishBowl settings...", "export", ExportLibrary));
            tools.DropDownItems.Add(MenuAction("Import FishBowl settings...", "import", ImportLibrary));
            tools.DropDownItems.Add(MenuAction("Create desktop shortcut", "desktop", CreateDesktopShortcut));
            tools.DropDownItems.Add(MenuAction("FishBowl settings...", "settings", ShowSettings));
            tools.DropDownItems.Add(MenuAction("Enable portable mode", "storage", EnablePortableMode));
            tools.DropDownItems.Add(MenuAction("Open FishBowl activity log", "info", delegate { if (!File.Exists(Store.LogFileName)) Store.Log("Activity log opened."); Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Store.LogFileName + "\"") { UseShellExecute = true }); }));
            tools.DropDownItems.Add(MenuAction("Diagnostics report...", "info", ShowDiagnostics));
            convertersMenu.DropDownOpening += delegate { RefreshConvertersMenu(); };
            var emulators = new ToolStripMenuItem("Emulators");
            emulators.DropDownItems.Add(MenuAction("Setup assistant / emulator folder...", "folder", delegate { ShowEmulatorManager("Setup assistant"); }));
            emulators.DropDownItems.Add(MenuAction("Find installed emulators...", "add", delegate { ShowEmulatorManager("Find installed"); }));
            emulators.DropDownItems.Add(MenuAction("Check releases / updates...", "refresh", delegate { ShowEmulatorManager("Updates"); }));
            emulators.DropDownItems.Add(MenuAction("Manage installed builds...", "storage", delegate { ShowEmulatorManager("Versions"); }));
            emulators.DropDownItems.Add(MenuAction("Emulator backups / restore...", "export", delegate { ShowEmulatorManager("Backups"); }));
            emulators.DropDownItems.Add(MenuAction("Setup checks...", "info", delegate { ShowEmulatorManager("Setup checks"); }));
            emulators.DropDownItems.Add(MenuAction("Official setup guidance...", "info", OpenOfficialSetupGuidance));
            emulators.DropDownItems.Add(MenuAction("Repair selected location...", "folder", RepairSelectedLocation));
            var view = new ToolStripMenuItem("View");
            view.DropDownItems.Add(MenuAction("Refresh emulators", "refresh", RefreshHub));
            var quick = new ToolStripMenuItem("Quick filter");
            foreach (var name in new[] { "All", "Running", "Needs attention", "Recently added" }) { var saved = name; quick.DropDownItems.Add(MenuAction(saved, "search", delegate { quickFilter = saved; RefreshHub(); SetStatus("Quick filter: " + saved + "."); })); }
            view.DropDownItems.Add(quick);
            view.DropDownItems.Add(MenuAction("Hub performance...", "settings", ShowPerformanceSettings));
            view.DropDownItems.Add(MenuAction("Full screen (F11)", "desktop", ToggleFullScreen));
            var multiplayer = new ToolStripMenuItem("Multiplayer");
            multiplayer.DropDownItems.Add(MenuAction("Start a session...", "controller", ShowMultiplayerHub));
            multiplayer.DropDownItems.Add(MenuAction("Multiplayer readiness check", "check", ShowMultiplayerReadiness));
            multiplayer.DropDownItems.Add(MenuAction("Privacy and connection settings...", "settings", ShowMultiplayerSettings));
            multiplayer.DropDownItems.Add(MenuAction("Check FishBowl updates...", "refresh", ShowFishBowlUpdates));
            linksMenu.DropDownOpening += delegate { RefreshWebsiteLinksMenu(); };
            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add(MenuAction("About FishBowl", "info", delegate { MessageBox.Show(this, "FishBowl opens your separately installed emulators.\n\nAdd games, manage libraries, and configure controls inside the dedicated emulator.", "FishBowl"); }));
            help.DropDownItems.Add(MenuAction("First-run guide...", "info", ShowFirstRunGuide));
            help.DropDownItems.Add(MenuAction("Game storage guide...", "folder", ShowGameStoragePrompt));
            help.DropDownItems.Add(MenuAction("Requirements storage guide...", "storage", ShowRequirementsStoragePrompt));
            foreach (var item in new[] { file, emulators, tools, view, multiplayer, convertersMenu, linksMenu, help }) { item.ForeColor = ink; item.DropDown.BackColor = bottom; item.DropDown.ForeColor = ink; menu.Items.Add(item); }
            return menu;
        }

        private void ShowMultiplayerHub()
        {
            using (var dialog = new MultiplayerDialog(library.Emulators, CurrentEmulator(), library.Multiplayer))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedProfile == null) return;
                library.Multiplayer = dialog.Settings; Store.Save(library);
                var profile = dialog.SelectedProfile;
                if (dialog.Mode == "Remote couch play")
                {
                    if (String.IsNullOrWhiteSpace(library.Multiplayer.RelayGatewayUrl))
                    {
                        MessageBox.Show(this, "Remote couch play needs a LiveKit Cloud token endpoint before it can start. FishBowl keeps this unconfigured by default so no LiveKit API secret or private session information is stored in the app. Use Privacy and connection settings to enter your endpoint after its server is set up.", "Remote couch play");
                        return;
                    }
                    MessageBox.Show(this, "FishBowl saved the private session preference and confirmed a LiveKit Cloud token endpoint is configured. The LiveKit media client is not included in this desktop build yet, so FishBowl will not expose a game window or controller data until that service is connected.", "Remote couch play");
                    return;
                }
                if (dialog.Mode == "Native online")
                    MessageBox.Show(this, profile.Name + " will open normally. Start its own netplay or multiplayer feature inside the emulator.\n\n" + MultiplayerSupport.For(profile).NativeOnline, "Native online session");
                else
                    MessageBox.Show(this, profile.Name + " will open normally. Configure all local controllers inside the emulator.\n\n" + MultiplayerSupport.For(profile).LocalPlay, "Local session");
                selectedEmulatorId = profile.Id; RefreshHub(); OpenSelectedEmulator();
            }
        }

        private void ShowMultiplayerReadiness()
        {
            var profile = CurrentEmulator();
            var rows = MultiplayerSupport.Readiness(profile, library.Multiplayer);
            if (profile != null)
            {
                var capability = MultiplayerSupport.For(profile);
                rows.Add(""); rows.Add("NATIVE ONLINE\n" + capability.NativeOnline); rows.Add("\nLOCAL PLAY\n" + capability.LocalPlay); rows.Add("\nREMOTE COUCH PLAY\n" + capability.RemoteCouchPlay);
            }
            MessageBox.Show(this, String.Join("\n\n", rows), "Multiplayer readiness", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowMultiplayerSettings()
        {
            using (var dialog = new MultiplayerSettingsDialog(library.Multiplayer))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Multiplayer = dialog.Settings; Store.Save(library); SetStatus("Multiplayer privacy and update preferences saved.");
            }
        }

        private void ShowPerformanceSettings()
        {
            using (var dialog = new PerformanceDialog(library.Theme))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.HubPerformanceMode = dialog.Mode; library.Theme.PauseBackgroundRefresh = dialog.PauseBackgroundRefresh; Store.Save(library);
                MessageBox.Show(this, "Hub performance preferences saved. Restart FishBowl to apply background refresh changes.", "FishBowl performance");
            }
        }

        private void ShowFishBowlUpdates()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var result = FishBowlUpdates.Check(library.Multiplayer);
                MessageBox.Show(this, result.Status + "\n\nInstalled: " + result.Installed + "\nLatest: " + (result.Latest ?? "No published release") + "\n\nFishBowl only checks the public release page. It does not download or replace the app automatically.\n\n" + (result.Notes ?? ""), "FishBowl updates");
            }
            catch (Exception error) { MessageBox.Show(this, "FishBowl could not check its release feed.\n\n" + error.Message, "FishBowl updates"); }
            finally { Cursor = Cursors.Default; }
        }

        private void ShowNotifications()
        {
            var rows = HubHealth.Notices(library).Select(n => n.Level.ToUpperInvariant() + "  " + n.Title + "\n" + n.Detail).ToList();
            using (var dialog = new ResultsDialog("FishBowl Notifications", rows)) dialog.ShowDialog(this);
        }

        private void ShowControllerProfiles()
        {
            using (var dialog = new ControllerProfilesDialog(library, CurrentEmulator()))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Store.Save(library); UpdateInformationPanel(); SetStatus("Controller profiles saved. They do not modify emulator mappings.");
            }
        }

        private void ShowMorePreferences()
        {
            using (var dialog = new MorePreferencesDialog(library.Theme))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                dialog.ApplyTo(library.Theme); Store.Save(library); SetStatus("Optional preferences saved.");
            }
        }

        private void ShowCommandPalette()
        {
            using (var dialog = new CommandPaletteDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                switch (dialog.Command)
                {
                    case "Add emulator": AddEmulator(); break;
                    case "Open selected emulator": OpenSelectedEmulator(); break;
                    case "Game storage organizer": ShowGameStorageOrganizer(null); break;
                    case "Requirements storage organizer": ShowRequirementsStorageOrganizer(null); break;
                    case "Setup workspace": ShowSetupWorkspace(); break;
                    case "Controller profiles": ShowControllerProfiles(); break;
                    case "Notifications / health": ShowNotifications(); break;
                    case "FishBowl settings": ShowSettings(); break;
                    case "Full screen": ToggleFullScreen(); break;
                }
            }
        }

        private EmulatorProfile CurrentEmulator() { return library.Emulators.FirstOrDefault(p => p.Id == selectedEmulatorId); }

        private void OpenOfficialSetupGuidance()
        {
            var profile = CurrentEmulator();
            if (profile == null) { MessageBox.Show(this, "Select an emulator first.", "FishBowl"); return; }
            var reference = EmulatorReference.For(profile);
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string requirements = reference.Requirements ?? "Review the emulator's official setup instructions for any required system files, graphics setup, and controller configuration.";
            string message = profile.Name + " setup guidance\n\n" + requirements + "\n\nFishBowl can open the emulator project's official guide. For firmware, BIOS, keys, or system software, use only files you are allowed to use and official instructions. FishBowl does not link to third-party downloads.\n\nOpen the official guide now?";
            if (MessageBox.Show(this, message, "Official setup guidance", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            if (!EmulatorReference.IsWebUrl(guide)) { MessageBox.Show(this, "No official setup guide is saved for this emulator. Use Edit information and links to add one.", "FishBowl"); return; }
            try { Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true }); Store.Log("Opened official setup guidance for " + profile.Name); }
            catch (Exception error) { MessageBox.Show(this, "The official setup guide could not be opened.\n\n" + error.Message, "FishBowl"); }
        }

        private void RefreshConvertersMenu()
        {
            convertersMenu.DropDownItems.Clear();
            convertersMenu.DropDownItems.Add(MenuAction("Prepare game for selected emulator...", "play", PrepareGameForSelectedEmulator));
            convertersMenu.DropDownItems.Add(MenuAction("View emulator format presets", "info", ShowFormatPresets));
            convertersMenu.DropDownItems.Add(new ToolStripSeparator());
            convertersMenu.DropDownItems.Add(MenuAction("Add converter for selected emulator...", "add", AddGameConverter));
            if (!library.Converters.Any()) { convertersMenu.DropDownItems.Add(new ToolStripMenuItem("No game converters added") { Enabled = false }); return; }
            convertersMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var tool in library.Converters.OrderBy(item => item.Name))
            {
                var saved = tool;
                var emulator = library.Emulators.FirstOrDefault(item => item.Id == saved.EmulatorId);
                convertersMenu.DropDownItems.Add(MenuAction((emulator == null ? "Unassigned" : emulator.Name) + " - " + saved.Name, "play", delegate { RunGameConverter(saved); }));
            }
        }

        private void ShowFormatPresets()
        {
            var rows = library.Emulators.OrderBy(item => item.Name).Select(item => item.Name + " | " + EmulatorReference.PlatformFor(item) + " | " + String.Join(", ", item.Extensions ?? new List<string>())).ToList();
            using (var dialog = new ResultsDialog("Emulator Format Presets", rows.Any() ? (IEnumerable<string>)rows : new[] { "Add an emulator to see its supported game formats." })) dialog.ShowDialog(this);
        }

        private void PrepareGameForSelectedEmulator()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { MessageBox.Show(this, "Select an emulator first.", "FishBowl"); return; }
            using (var choose = new OpenFileDialog { Title = "Choose a game archive or file for " + emulator.Name, Filter = "All files (*.*)|*.*" })
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                string extension = Path.GetExtension(choose.FileName).ToLowerInvariant();
                if (extension == ".zip") { ExtractZipForEmulator(emulator, choose.FileName); return; }
                var converter = library.Converters.FirstOrDefault(item => item.EmulatorId == emulator.Id && (item.InputExtensions ?? "").Split(',').Any(value => String.Equals(value.Trim().TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
                if (converter == null)
                {
                    MessageBox.Show(this, "FishBowl has no registered extractor or converter for " + extension + " with " + emulator.Name + ".\n\nAdd one from Game converters, then try again.", "FishBowl");
                    return;
                }
                RunGameConverter(converter, choose.FileName);
            }
        }

        private void ExtractZipForEmulator(EmulatorProfile emulator, string archivePath)
        {
            try
            {
                string root = !String.IsNullOrWhiteSpace(emulator.ScanFolder) && Directory.Exists(emulator.ScanFolder) ? emulator.ScanFolder : Path.GetDirectoryName(archivePath);
                string destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath));
                int suffix = 2; while (Directory.Exists(destination)) destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath) + " (" + suffix++ + ")");
                using (var archive = System.IO.Compression.ZipFile.OpenRead(archivePath)) EmulatorInstaller.ValidateArchive(archive, destination, false);
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, destination);
                var supported = Directory.GetFiles(destination, "*.*", SearchOption.AllDirectories).Where(path => (emulator.Extensions ?? new List<string>()).Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).ToList();
                Store.Log("Extracted ZIP for " + emulator.Name + ": " + archivePath);
                using (var dialog = new ResultsDialog("Extraction Complete", supported.Any() ? supported.Select(path => "Compatible game: " + path) : new[] { "Extracted to: " + destination, "No files matching " + emulator.Name + "'s configured formats were found." })) dialog.ShowDialog(this);
            }
            catch (Exception error) { MessageBox.Show(this, "FishBowl could not extract that ZIP archive.\n\n" + error.Message, "FishBowl"); }
        }

        private void AddGameConverter()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { MessageBox.Show(this, "Select an emulator first, then add its converter.", "FishBowl"); return; }
            using (var name = new TextPromptDialog("Game Converter", "Converter name for " + emulator.Name))
            {
                if (name.ShowDialog(this) != DialogResult.OK || String.IsNullOrWhiteSpace(name.Value)) return;
                using (var choose = new OpenFileDialog { Title = "Choose converter program or script", Filter = "Programs and scripts (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*" })
                {
                    if (choose.ShowDialog(this) != DialogResult.OK) return;
                    using (var input = new TextPromptDialog("Game Converter", "Input file extensions, for example .zip, .rar, .bin"))
                    {
                        if (input.ShowDialog(this) != DialogResult.OK) return;
                        using (var output = new TextPromptDialog("Game Converter", "Output extension, for example .3ds (leave blank for extract-only tools)"))
                        {
                            if (output.ShowDialog(this) != DialogResult.OK) return;
                            using (var arguments = new TextPromptDialog("Game Converter", "Arguments - use {input} and {output}"))
                            {
                                arguments.Value = "{input} {output}";
                                if (arguments.ShowDialog(this) != DialogResult.OK) return;
                                library.Converters.Add(new GameConverter { Id = Guid.NewGuid().ToString("N"), EmulatorId = emulator.Id, Name = name.Value.Trim(), Program = choose.FileName, InputExtensions = input.Value.Trim(), OutputExtension = output.Value.Trim(), Arguments = arguments.Value.Trim() });
                                Store.Save(library); SetStatus("Added game converter for " + emulator.Name + ".");
                            }
                        }
                    }
                }
            }
        }

        private void RunGameConverter(GameConverter tool)
        {
            if (!File.Exists(tool.Program)) { MessageBox.Show(this, "The converter program cannot be found. Add it again from Game converters.", "FishBowl"); return; }
            var patterns = (tool.InputExtensions ?? "").Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).Select(item => "*" + (item.StartsWith(".") ? item : "." + item)).ToArray();
            string filter = patterns.Length == 0 ? "All files (*.*)|*.*" : "Supported input (" + String.Join(";", patterns) + ")|" + String.Join(";", patterns) + "|All files (*.*)|*.*";
            using (var choose = new OpenFileDialog { Title = "Choose a game file for " + tool.Name, Filter = filter })
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                RunGameConverter(tool, choose.FileName);
            }
        }

        private void RunGameConverter(GameConverter tool, string inputPath)
        {
            if (!File.Exists(tool.Program)) { MessageBox.Show(this, "The converter program cannot be found. Add it again from Game converters.", "FishBowl"); return; }
            string extension = tool.OutputExtension ?? ""; if (extension.Length > 0 && !extension.StartsWith(".")) extension = "." + extension;
            string output = extension.Length == 0 ? Path.GetDirectoryName(inputPath) : Path.ChangeExtension(inputPath, extension);
            string arguments = (tool.Arguments ?? "{input}").Replace("{input}", "\"" + inputPath.Replace("\"", "\\\"") + "\"").Replace("{output}", "\"" + output.Replace("\"", "\\\"") + "\"");
            try { Process.Start(new ProcessStartInfo { FileName = tool.Program, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(tool.Program), UseShellExecute = true }); Store.Log("Ran game converter: " + tool.Name); SetStatus("Started " + tool.Name + "."); }
            catch (Exception error) { MessageBox.Show(this, "The converter could not be started.\n\n" + error.Message, "FishBowl"); }
        }
        private void SetStatus(string text) { status.Text = text; }
        private void InvalidateEmulatorRow(string id)
        {
            if (id == null) return;
            foreach (ListViewItem row in emulatorList.Items) if (row.Tag as string == id) { emulatorList.Invalidate(row.Bounds); return; }
        }

        private void ApplyEmulatorSelection()
        {
            var next = emulatorList.SelectedItems.Count == 0 ? null : emulatorList.SelectedItems[0].Tag as string;
            if (next == selectedEmulatorId) return;
            SaveProfileNotes(); selectedEmulatorId = next; UpdateActions();
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
        }

        private void ClearEmulatorSelection()
        {
            SaveProfileNotes(); refreshing = true;
            try { foreach (ListViewItem row in emulatorList.Items) row.Selected = false; selectedEmulatorId = null; }
            finally { refreshing = false; UpdateActions(); emulatorList.Invalidate(); }
            SetStatus("Select an emulator to see its information. Double-click to open.");
        }
        private void UpdateActions() { var selected = CurrentEmulator() != null; editButton.Enabled = selected; removeButton.Enabled = selected; managementButton.Enabled = selected; UpdateInformationPanel(); }
        private void RefreshHub()
        {
            SaveProfileNotes();
            RefreshPlatformOptions();
            refreshing = true;
            emulatorList.BeginUpdate();
            try
            {
                emulatorList.Items.Clear(); emulatorImages.Images.Clear();
                emulatorImageSources.Clear();
                if (reloadProgramMetadata) { reloadProgramMetadata = false; metadataGeneration++; foreach (var image in emulatorIconCache.Values) image.Dispose(); emulatorIconCache.Clear(); versionCache.Clear(); }
                PruneVisualCaches();
                var query = filterBox.Text.Trim();
                var selectedPlatform = platformFilter.SelectedItem as string ?? "All platforms";
                var items = library.Emulators.Where(p => !favoritesOnly.Checked || p.Favorite)
                    .Where(p => selectedPlatform == "All platforms" || EmulatorReference.PlatformTags(p).Contains(selectedPlatform, StringComparer.OrdinalIgnoreCase))
                    .Where(p => query.Length == 0 || (p.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || EmulatorReference.PlatformFor(p).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (p.Notes ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Where(p => quickFilter == "All" || quickFilter == "Needs attention" && !File.Exists(p.Executable) || quickFilter == "Running" && EmulatorRuntime.State(p.Executable) == RuntimeState.Running || quickFilter == "Recently added" && !String.IsNullOrWhiteSpace(p.AddedAt))
                    .OrderByDescending(p => p.Favorite).ThenBy(p => p.Name).ToList();
                foreach (var p in items)
                {
                    var image = CachedEmulatorImage(p);
                    emulatorImageSources.Add(image); emulatorImages.Images.Add(image);
                    var item = new ListViewItem((p.Favorite ? "★ " : "") + (p.Name ?? "Emulator"), emulatorImages.Images.Count - 1) { Tag = p.Id };
                    item.SubItems.Add(File.Exists(p.Executable) ? "Ready" : "Missing program");
                    item.SubItems.Add(EmulatorReference.PlatformFor(p));
                    item.SubItems.Add(Path.GetFileName(p.Executable ?? ""));
                    item.Selected = p.Id == selectedEmulatorId;
                    emulatorList.Items.Add(item);
                }
                if (!items.Any(p => p.Id == selectedEmulatorId)) selectedEmulatorId = null;
                if (!items.Any(p => p.Id == hoveredEmulatorId)) hoveredEmulatorId = null;
                foreach (ListViewItem item in emulatorList.Items) item.Selected = (item.Tag as string) == selectedEmulatorId;
                if (items.Count == 0) SetStatus(library.Emulators.Count == 0 ? "Click Add emulator to get started." : "No emulators match this filter.");
            }
            finally { emulatorList.EndUpdate(); refreshing = false; UpdateActions(); }
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
        }
        private void ResizeEmulatorColumns()
        {
            if (emulatorList.Columns.Count != 4) return;
            var width = Math.Max(1, emulatorList.ClientSize.Width - 4);
            emulatorList.Columns[0].Width = (int)(width * 0.36);
            emulatorList.Columns[1].Width = (int)(width * 0.18);
            emulatorList.Columns[2].Width = (int)(width * 0.29);
            emulatorList.Columns[3].Width = width - emulatorList.Columns[0].Width - emulatorList.Columns[1].Width - emulatorList.Columns[2].Width;
            if (emulatorList.IsHandleCreated) emulatorList.AutoResizeColumn(3, ColumnHeaderAutoResizeStyle.HeaderSize);
        }
        private ProcessStartInfo EmulatorStartInfo(EmulatorProfile profile)
        {
            return new ProcessStartInfo { FileName = profile.Executable, Arguments = "", WorkingDirectory = Path.GetDirectoryName(profile.Executable), UseShellExecute = true };
        }
        private void OpenSelectedEmulator()
        {
            var profile = CurrentEmulator();
            if (profile == null) { SetStatus("Select an emulator first."); return; }
            if (!File.Exists(profile.Executable)) { RepairSelectedLocation(); if (!File.Exists(profile.Executable)) return; }
            if (EmulatorRuntime.State(profile.Executable) == RuntimeState.Running) { EmulatorRuntime.BringForward(profile.Executable); SetStatus(profile.Name + " is already running."); return; }
            if (library.Theme.ConfirmBeforeEmulatorLaunch && MessageBox.Show(this, "Open " + profile.Name + "? FishBowl will start the emulator normally without changing its settings.", "Open emulator", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using (var process = Process.Start(EmulatorStartInfo(profile))) { }
            RefreshRuntimeStatus();
            SetStatus("Opened " + profile.Name + ". Add and launch games inside the emulator.");
        }
        private void AddEmulator() { AddEmulatorWithPath(null); }
        private void AddEmulatorWithPath(string path)
        {
            using (var dialog = new EmulatorDialog(null, path))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var p = dialog.Profile; p.Id = Guid.NewGuid().ToString("N"); p.AddedAt = DateTime.UtcNow.ToString("o");
                BuildRegistry.Remember(p, p.Executable, "Initial installation");
                library.Emulators.Add(p); selectedEmulatorId = p.Id;
                Store.Save(library); filterBox.Clear(); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Added " + p.Name + ".");
                ShowFirmwareSetupNotice(p);
            }
        }

        private void ShowFirmwareSetupNotice(EmulatorProfile profile)
        {
            var reference = EmulatorReference.For(profile);
            var requirements = reference.Requirements ?? "";
            bool requiresSetup = requirements.IndexOf("BIOS", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("firmware", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("system software", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!requiresSetup) return;
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string message = profile.Name + " requires additional setup before it can run some or all games.\n\nWHAT THIS EMULATOR REQUIRES\n" + requirements + "\n\nWHERE TO CONFIGURE IT\nIn FishBowl, select " + profile.Name + " and open Emulators > Setup checks. Choose the firmware folder you already use, then follow the emulator's own setup screen.\n\nFishBowl does not include, download, or bypass BIOS, firmware, system software, or keys. Use files you are allowed to use and the emulator's official instructions.\n\nOpen the official setup guide now?";
            if (MessageBox.Show(this, message, "Setup required", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                if (EmulatorReference.IsWebUrl(guide))
                {
                    try { Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true }); }
                    catch (Exception error) { MessageBox.Show(this, "The official setup guide could not be opened.\n\n" + error.Message, "FishBowl"); }
                }
                else MessageBox.Show(this, "This emulator does not have a setup link saved yet. Open Edit information and links to add its official guide.", "FishBowl");
            }
        }
        private void EditEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            var previousExecutable = current.Executable; var previousVersion = current.ManualVersion;
            using (var dialog = new EmulatorDialog(current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                dialog.Profile.Id = current.Id;
                if (!HubPaths.Same(previousExecutable, dialog.Profile.Executable))
                {
                    var previous = new EmulatorProfile { Executable = previousExecutable, ManualVersion = previousVersion, Builds = dialog.Profile.Builds ?? new List<EmulatorBuild>() };
                    BuildRegistry.Remember(previous, previousExecutable, "Previous installation"); dialog.Profile.Builds = previous.Builds;
                    dialog.Profile.ManualVersion = ""; BuildRegistry.Remember(dialog.Profile, dialog.Profile.Executable, "Edited location");
                }
                library.Emulators[library.Emulators.IndexOf(current)] = dialog.Profile;
                reloadProgramMetadata = true; Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Updated " + dialog.Profile.Name + ".");
            }
        }
        private void RemoveEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            if (library.Theme.ConfirmBeforeEmulatorRemoval && MessageBox.Show(this, "Remove " + current.Name + " from FishBowl?\n\nIts emulator program, games, and saves will stay where they are.", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            library.Emulators.Remove(current);
            selectedEmulatorId = null;
            Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Emulator removed from FishBowl.");
        }
        private void HandleDroppedPaths(string[] paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
                if (File.Exists(path) && EmulatorReference.IsLaunchFile(path))
                {
                    if (library.Emulators.Any(p => String.Equals(p.Executable, path, StringComparison.OrdinalIgnoreCase))) { SetStatus("That emulator is already in FishBowl."); continue; }
                    AddEmulatorWithPath(path);
                }
                else SetStatus("Add games inside the emulator. Drop an emulator program or shortcut here to add it to FishBowl.");
        }
        private void OpenEmulatorFolder() { var p = CurrentEmulator(); if (p != null && Directory.Exists(Path.GetDirectoryName(p.Executable))) Process.Start(new ProcessStartInfo(Path.GetDirectoryName(p.Executable)) { UseShellExecute = true }); }
        private void ExportLibrary()
        {
            using (var dialog = new SaveFileDialog { Title = "Export FishBowl settings", Filter = "JSON files (*.json)|*.json", FileName = "FishBowl-settings.json" })
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; Store.Save(library); File.Copy(Store.FileName, dialog.FileName, true); SetStatus("Exported FishBowl settings."); }
        }
        private void ImportLibrary()
        {
            using (var dialog = new OpenFileDialog { Title = "Import FishBowl settings", Filter = "JSON files (*.json)|*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var imported = new JavaScriptSerializer().Deserialize<LibraryData>(File.ReadAllText(dialog.FileName));
                if (imported == null || imported.Emulators == null) throw new InvalidDataException("That file does not contain FishBowl emulator settings.");
                if (MessageBox.Show(this, "Replace your FishBowl settings with this backup?", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Store.Save(library); File.Copy(Store.FileName, Store.FileName + ".before-import.json", true);
                library.Emulators = imported.Emulators; library.Games = imported.Games ?? new List<GameEntry>();
                library.Collections = imported.Collections ?? new List<GameCollection>(); library.Links = imported.Links ?? new List<WebsiteLink>(); library.ControllerProfiles = imported.ControllerProfiles ?? new List<ControllerProfile>();
                library.Theme = imported.Theme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7 }; library.Multiplayer = imported.Multiplayer ?? new MultiplayerSettings { InviteOnly = true, RelayProvider = "LiveKit Cloud", UpdateChannel = "Stable", CheckForHubUpdates = true }; library.BackupFolder = imported.BackupFolder; library.EmulatorRootDirectory = imported.EmulatorRootDirectory; library.GameLibraryRoot = imported.GameLibraryRoot; library.RequirementsLibraryRoot = imported.RequirementsLibraryRoot;
                selectedEmulatorId = null;
                Store.Save(library); filterBox.Clear(); RefreshHub(); SetStatus("Imported FishBowl settings. Reopen FishBowl to apply the theme.");
            }
        }
        private void ShowGameLibrary()
        {
            using (var dialog = new GameLibraryDialog(library))
            {
                dialog.ShowDialog(this);
                Store.Save(library);
                SetStatus("Game library updated. " + library.Games.Count + " game" + (library.Games.Count == 1 ? "" : "s") + " saved.");
            }
        }
        private void ShowFirstRunGuide()
        {
            using (var dialog = new StartupAssistantDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.ShowStartupAssistant = !dialog.DontShowAgain;
                library.Theme.StartupAssistantPreferenceSet = true;
                Store.Save(library);
                if (dialog.OpenSetupAssistant) ShowEmulatorManager("Setup assistant");
            }
        }
        private void ShowGameStoragePrompt()
        {
            using (var dialog = new GameStoragePromptDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.ShowGameStorageAssistant = !dialog.DontShowAgain;
                library.Theme.GameStorageAssistantPreferenceSet = true;
                Store.Save(library);
                if (dialog.OpenOrganizer) ShowGameStorageOrganizer(null);
            }
        }
        private void ShowGameStorageOrganizer(IEnumerable<string> droppedPaths)
        {
            using (var dialog = new GameStorageOrganizerDialog(library, droppedPaths))
            {
                dialog.ShowDialog(this);
                Store.Save(library);
            }
        }
        private void ShowRequirementsStoragePrompt()
        {
            using (var dialog = new RequirementsStoragePromptDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.ShowRequirementsStorageAssistant = !dialog.DontShowAgain;
                library.Theme.RequirementsStorageAssistantPreferenceSet = true;
                Store.Save(library);
                if (dialog.OpenOrganizer) ShowRequirementsStorageOrganizer(null);
            }
        }
        private void ShowRequirementsStorageOrganizer(IEnumerable<string> droppedPaths)
        {
            using (var dialog = new RequirementsStorageOrganizerDialog(library, droppedPaths))
            {
                dialog.ShowDialog(this);
                Store.Save(library);
                SetStatus("Requirements library contains " + RequirementsStorage.FileCount(library) + " file" + (RequirementsStorage.FileCount(library) == 1 ? "." : "s."));
            }
        }
        private void ShowSetupWorkspace()
        {
            var rows = new List<string>();
            rows.Add("REQUIREMENTS LIBRARY\n" + RequirementsStorage.FileCount(library) + " stored file" + (RequirementsStorage.FileCount(library) == 1 ? "." : "s.") + " This is storage only; emulator setup stays inside each emulator.");
            foreach (var emulator in library.Emulators.OrderBy(item => item.Name))
            {
                var reference = EmulatorReference.For(emulator);
                bool needsSystemFiles = (reference.Requirements ?? "").IndexOf("firmware", StringComparison.OrdinalIgnoreCase) >= 0 || (reference.Requirements ?? "").IndexOf("bios", StringComparison.OrdinalIgnoreCase) >= 0 || (reference.Requirements ?? "").IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0;
                rows.Add(emulator.Name.ToUpperInvariant() + "\n" + (needsSystemFiles ? "Review emulator setup: " : "No system-file reminder found: ") + (String.IsNullOrWhiteSpace(emulator.FirmwareFolder) ? "No emulator folder recorded in FishBowl." : "Emulator folder recorded.") + "\n" + (reference.Requirements ?? "See the emulator's official guide."));
            }
            using (var dialog = new ResultsDialog("FishBowl Setup Workspace", rows.Any() ? (IEnumerable<string>)rows : new[] { "Add an emulator to see its setup checklist." })) dialog.ShowDialog(this);
        }
        private void ShowDiagnostics()
        {
            var rows = new List<string>();
            rows.Add("FishBowl data: " + Store.DataDirectory);
            rows.Add("Settings file: " + (File.Exists(Store.FileName) ? "Available" : "Not yet created"));
            rows.Add("Emulators: " + library.Emulators.Count + "    Games: " + library.Games.Count + "    Collections: " + library.Collections.Count);
            foreach (var emulator in library.Emulators) rows.Add((File.Exists(emulator.Executable) ? "OK  " : "MISSING  ") + emulator.Name + " — " + emulator.Executable);
            foreach (var game in library.Games.Where(item => !File.Exists(item.Path)).Take(20)) rows.Add("MISSING GAME  " + game.Title + " — " + game.Path);
            try { var root = Path.GetPathRoot(HubPaths.BackupRoot(library)); var drive = new DriveInfo(root); rows.Add("Backup drive free space: " + (drive.AvailableFreeSpace / 1024 / 1024 / 1024) + " GB"); } catch { rows.Add("Backup drive free space: unavailable"); }
            rows.Add("Activity and crash report: " + Store.LogFileName);
            using (var dialog = new ResultsDialog("FishBowl Diagnostics", rows)) dialog.ShowDialog(this);
        }
        private void ConfigureGameFolderWatchers()
        {
            foreach (var watcher in gameFolderWatchers) watcher.Dispose();
            gameFolderWatchers.Clear();
            if (!library.Theme.AutoSyncGameFolders) return;
            foreach (var emulator in library.Emulators.Where(item => Directory.Exists(item.ScanFolder)))
            {
                try
                {
                    var watcher = new FileSystemWatcher(emulator.ScanFolder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
                    FileSystemEventHandler changed = delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { gameFolderTimer.Stop(); gameFolderTimer.Start(); }); };
                    watcher.Created += changed; watcher.Deleted += changed; watcher.Renamed += delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { gameFolderTimer.Stop(); gameFolderTimer.Start(); }); };
                    gameFolderWatchers.Add(watcher);
                }
                catch (Exception error) { Store.Log("Game folder watch unavailable: " + error.Message); }
            }
        }
        private void SyncWatchedGameFolders()
        {
            int added = GameLibraryCatalog.Sync(library);
            if (added > 0) { Store.Save(library); SetStatus("Added " + added + " newly detected game" + (added == 1 ? "." : "s.") + " Open Game library to view them."); }
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.E) { RunUiAction(AddEmulator); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.P) { ShowCommandPalette(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { filterBox.Focus(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.G) { ShowGameStorageOrganizer(null); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F11) { ToggleFullScreen(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { if (fullScreen) ToggleFullScreen(); else if (filterBox.TextLength > 0) filterBox.Clear(); else ClearEmulatorSelection(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter && emulatorList.ContainsFocus) { RunUiAction(OpenSelectedEmulator); e.SuppressKeyPress = true; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !visualResourcesDisposed) { visualResourcesDisposed = true; runtimeTimer.Dispose(); notesTimer.Dispose(); gameFolderTimer.Dispose(); foreach (var watcher in gameFolderWatchers) watcher.Dispose(); emulatorImages.Dispose(); foreach (var image in emulatorIconCache.Values) image.Dispose(); emulatorIconCache.Clear(); emulatorImageSources.Clear(); Icon = null; if (ownedAppIcon != null) ownedAppIcon.Dispose(); }
            base.Dispose(disposing);
        }
        private void RunUiAction(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                Store.Log("Action failed: " + error);
                MessageBox.Show(this, "FishBowl could not complete this action.\n\n" + error.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private ToolStripMenuItem MenuAction(string text, string icon, Action action)
        {
            var item = new ToolStripMenuItem(text) { Image = MakeUiIcon(icon), ImageScaling = ToolStripItemImageScaling.SizeToFit };
            item.Click += delegate { RunUiAction(action); };
            var ownedMenuIcon = item.Image; item.Disposed += delegate { item.Image = null; if (ownedMenuIcon != null) ownedMenuIcon.Dispose(); };
            return item;
        }

        private static Image MakeUiIcon(string kind) { return FishBowlVisuals.Icon(kind,32,Color.FromArgb(224,208,255),Color.FromArgb(255,180,105)); }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ApplyThemeColors()
        {
            var theme = library.Theme == null ? "Twilight" : library.Theme.Name;
            if (theme == "Lavender")
            {
                ink = Color.FromArgb(44, 37, 54); top = Color.FromArgb(242, 237, 248); bottom = Color.FromArgb(231, 222, 240); surface = Color.FromArgb(255, 250, 255); subtle = Color.FromArgb(105, 91, 119); blue = Color.FromArgb(137, 80, 215); pink = Color.FromArgb(236, 128, 65);
            }
            else if (theme == "Light")
            {
                ink = Color.FromArgb(42, 43, 50); top = Color.FromArgb(246, 246, 249); bottom = Color.FromArgb(235, 236, 241); surface = Color.FromArgb(255, 255, 255); subtle = Color.FromArgb(101, 103, 115); blue = Color.FromArgb(103, 82, 171); pink = Color.FromArgb(203, 113, 61);
            }
            else if (theme == "High Contrast")
            {
                ink = Color.White; top = Color.Black; bottom = Color.FromArgb(17, 17, 17); surface = Color.FromArgb(30, 30, 30); subtle = Color.FromArgb(224, 224, 224); blue = Color.FromArgb(112, 191, 255); pink = Color.FromArgb(255, 220, 77);
            }
            else if (theme == "Ember")
            {
                ink = Color.FromArgb(255, 248, 240); top = Color.FromArgb(47, 28, 25); bottom = Color.FromArgb(64, 38, 32); surface = Color.FromArgb(84, 50, 40); subtle = Color.FromArgb(229, 191, 174); blue = Color.FromArgb(218, 107, 174); pink = Color.FromArgb(255, 176, 72);
            }
            else if (theme == "Midnight")
            {
                ink = Color.FromArgb(232, 237, 250); top = Color.FromArgb(16, 20, 33); bottom = Color.FromArgb(21, 27, 43); surface = Color.FromArgb(35, 43, 63); subtle = Color.FromArgb(159, 173, 201); blue = Color.FromArgb(111, 142, 255); pink = Color.FromArgb(121, 211, 255);
            }
            else if (theme == "Forest")
            {
                ink = Color.FromArgb(235, 246, 238); top = Color.FromArgb(27, 46, 38); bottom = Color.FromArgb(34, 58, 47); surface = Color.FromArgb(48, 76, 62); subtle = Color.FromArgb(177, 206, 187); blue = Color.FromArgb(111, 202, 150); pink = Color.FromArgb(206, 211, 109);
            }
            else if (theme == "Rosewood")
            {
                ink = Color.FromArgb(250, 239, 243); top = Color.FromArgb(52, 30, 40); bottom = Color.FromArgb(67, 38, 52); surface = Color.FromArgb(87, 51, 67); subtle = Color.FromArgb(225, 184, 199); blue = Color.FromArgb(226, 110, 166); pink = Color.FromArgb(255, 169, 116);
            }
            else if (theme == "Mist")
            {
                ink = Color.FromArgb(38, 46, 56); top = Color.FromArgb(237, 243, 248); bottom = Color.FromArgb(222, 231, 239); surface = Color.FromArgb(251, 253, 255); subtle = Color.FromArgb(92, 109, 125); blue = Color.FromArgb(69, 135, 191); pink = Color.FromArgb(200, 113, 116);
            }
            else
            {
                ink = Color.FromArgb(239, 239, 243); top = Color.FromArgb(31, 32, 37); bottom = Color.FromArgb(35, 36, 42); surface = Color.FromArgb(47, 48, 56); subtle = Color.FromArgb(174, 176, 186); blue = Color.FromArgb(183, 150, 245); pink = Color.FromArgb(235, 158, 94);
            }
            var accent = library.Theme.AccentColor ?? "Sunset";
            if (accent == "Ocean") { blue = Color.FromArgb(89, 190, 255); pink = Color.FromArgb(67, 220, 187); }
            else if (accent == "Rose") { blue = Color.FromArgb(228, 100, 181); pink = Color.FromArgb(255, 157, 101); }
            else if (accent == "Lime") { blue = Color.FromArgb(155, 213, 98); pink = Color.FromArgb(246, 210, 92); }
            else if (accent == "Amethyst") { blue = Color.FromArgb(177, 122, 255); pink = Color.FromArgb(222, 109, 244); }
            else if (accent == "Gold") { blue = Color.FromArgb(236, 178, 62); pink = Color.FromArgb(255, 219, 122); }
            else if (accent == "Ice") { blue = Color.FromArgb(103, 198, 232); pink = Color.FromArgb(173, 235, 255); }
            swatches = new[] { blue, pink, Color.FromArgb((blue.R + pink.R) / 2, (blue.G + pink.G) / 2, (blue.B + pink.B) / 2) };
        }

        private void ApplyVisualScale()
        {
            int percent = Math.Max(85, Math.Min(125, library.Theme.UiScalePercent == 0 ? 100 : library.Theme.UiScalePercent));
            if (percent != 100) Scale(new SizeF(percent / 100f, percent / 100f));
        }

        private void ToggleFullScreen()
        {
            if (!fullScreen)
            {
                savedBorderStyle = FormBorderStyle;
                savedWindowState = WindowState;
                savedBounds = Bounds;
                savedNormalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;
                fullScreen = true;
                SetStatus("Full-screen mode. Press F11 to return.");
            }
            else
            {
                FormBorderStyle = savedBorderStyle;
                WindowState = FormWindowState.Normal;
                Bounds = savedBounds;
                WindowState = savedWindowState;
                fullScreen = false;
                SetStatus("Returned to the standard window.");
            }
        }

        private void RestoreWindowLayout()
        {
            try
            {
                if (!File.Exists(WindowLayoutFile)) return;
                var values = new JavaScriptSerializer().Deserialize<int[]>(File.ReadAllText(WindowLayoutFile));
                if (values == null || values.Length != 5) return;
                var bounds = new Rectangle(values[0], values[1], Math.Max(1100, values[2]), Math.Max(700, values[3]));
                var screen = Screen.AllScreens.FirstOrDefault(item => item.WorkingArea.IntersectsWith(bounds));
                if (screen == null) screen = Screen.PrimaryScreen;
                var area = screen.WorkingArea;
                bounds.Width = Math.Min(bounds.Width, area.Width);
                bounds.Height = Math.Min(bounds.Height, area.Height);
                bounds.X = Math.Max(area.Left, Math.Min(bounds.X, area.Right - bounds.Width));
                bounds.Y = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - bounds.Height));
                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
                WindowState = values[4] == 1 ? FormWindowState.Maximized : FormWindowState.Normal;
            }
            catch (Exception error) { Store.Log("Window layout could not be restored: " + error.Message); }
        }

        private void SaveWindowLayout()
        {
            try
            {
                var state = fullScreen ? savedWindowState : WindowState;
                var bounds = fullScreen ? savedNormalBounds : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
                Directory.CreateDirectory(Store.DataDirectory);
                File.WriteAllText(WindowLayoutFile, new JavaScriptSerializer().Serialize(new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height, state == FormWindowState.Maximized ? 1 : 0 }));
            }
            catch (Exception error) { Store.Log("Window layout could not be saved: " + error.Message); }
        }

        private static Image LoadLogo()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.png"))
            using (var source = stream == null ? null : Image.FromStream(stream))
                return source == null ? null : new Bitmap(source);
        }

        private static Icon LoadAppIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.ico"))
                return stream == null ? null : new Icon(stream);
        }

        private string EmulatorIconKey(EmulatorProfile profile)
        {
            return new JavaScriptSerializer().Serialize(new[] { profile.Id, profile.Executable, profile.IconPath, profile.Name });
        }

        private Image CachedEmulatorImage(EmulatorProfile profile)
        {
            var key = EmulatorIconKey(profile); Image image;
            if (!emulatorIconCache.TryGetValue(key, out image))
            {
                var color = swatches[Math.Abs((profile.Id ?? profile.Name ?? "?").GetHashCode() % swatches.Length)];
                image = GetEmulatorImage(profile) ?? MakeFallbackIcon(profile.Name ?? "?", color); emulatorIconCache[key] = image;
            }
            return image;
        }

        private void PruneVisualCaches()
        {
            var liveKeys = new HashSet<string>(library.Emulators.Select(EmulatorIconKey));
            foreach (var key in emulatorIconCache.Keys.Where(k => !liveKeys.Contains(k)).ToArray()) { emulatorIconCache[key].Dispose(); emulatorIconCache.Remove(key); }
            var livePrograms = new HashSet<string>(library.Emulators.Select(p => p.Executable ?? ""));
            foreach (var key in versionCache.Keys.Where(k => !livePrograms.Contains(k)).ToArray()) versionCache.Remove(key);
        }

        private static Image GetEmulatorImage(EmulatorProfile emulator)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(emulator.IconPath) && File.Exists(emulator.IconPath))
                {
                    using (var source = Image.FromFile(emulator.IconPath)) return new Bitmap(source);
                }
                if (File.Exists(emulator.Executable))
                {
                    using (var icon = Icon.ExtractAssociatedIcon(emulator.Executable))
                        return icon == null ? null : icon.ToBitmap();
                }
            }
            catch { }
            return null;
        }

        private static Image MakeFallbackIcon(string name, Color color)
        {
            var image=new Bitmap(48,48);using(var g=Graphics.FromImage(image)){g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var p=FishBowlVisuals.Round(new RectangleF(4,4,40,40),8))using(var b=new SolidBrush(Color.FromArgb(42,color)))using(var pen=new Pen(Color.FromArgb(150,color),1.5f)){g.FillPath(b,p);g.DrawPath(pen,p);}
                var words=(name??"?").Split(new[]{' ','-','_'},StringSplitOptions.RemoveEmptyEntries);var initials=String.Concat(words.Take(2).Select(w=>w.Substring(0,1).ToUpperInvariant()));
                using(var font=new Font("Bahnschrift",16,FontStyle.Bold))TextRenderer.DrawText(g,initials.Length==0?"?":initials,font,new Rectangle(3,3,42,42),Color.FromArgb(242,232,255),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }return image;
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                string appPath = Application.ExecutablePath;
                MessageBox.Show(Platform.CreateAppShortcut(appPath, Path.Combine(Path.GetDirectoryName(appPath), "FishBowl.ico")), "FishBowl");
            }
            catch (Exception exception)
            {
                MessageBox.Show("FishBowl could not create the desktop shortcut.\n\n" + exception.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshWebsiteLinksMenu()
        {
            linksMenu.DropDownItems.Clear();
            linksMenu.DropDownItems.Add("Add website link...", null, delegate { AddWebsiteLink(); });
            if (!library.Links.Any())
            {
                linksMenu.DropDownItems.Add(new ToolStripMenuItem("No saved links") { Enabled = false });
                return;
            }
            linksMenu.DropDownItems.Add("Remove website link...", null, delegate { RemoveWebsiteLink(); });
            linksMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var link in library.Links.OrderBy(item => item.Name))
            {
                var savedLink = link;
                linksMenu.DropDownItems.Add(savedLink.Name, null, delegate { OpenWebsiteLink(savedLink); });
            }
        }

        private void AddWebsiteLink()
        {
            using (var dialog = new WebsiteLinkDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Links.Add(dialog.Link);
                Store.Save(library);
                SetStatus("Saved website link: " + dialog.Link.Name + ".");
            }
        }

        private void RemoveWebsiteLink()
        {
            if (!library.Links.Any()) return;
            using (var dialog = new WebsiteLinkPicker(library.Links))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedLink == null) return;
                library.Links.RemoveAll(link => link.Id == dialog.SelectedLink.Id);
                Store.Save(library);
                SetStatus("Removed website link: " + dialog.SelectedLink.Name + ".");
            }
        }

        private void OpenWebsiteLink(WebsiteLink link)
        {
            Uri address;
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("This saved link is not a valid http or https address.", "FishBowl");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = address.AbsoluteUri, UseShellExecute = true });
                SetStatus("Opening " + link.Name + " in your browser.");
            }
            catch (Exception error) { MessageBox.Show("The website could not be opened:\n" + error.Message, "FishBowl"); }
        }

        private void ShowSettings()
        {
            using (var dialog = new SettingsDialog(library.Theme, library.BackupFolder))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                bool restartRequired = !String.Equals(library.Theme.Name, dialog.Theme.Name, StringComparison.OrdinalIgnoreCase) || library.Theme.StartMaximized != dialog.Theme.StartMaximized || !String.Equals(library.Theme.AccentColor, dialog.Theme.AccentColor, StringComparison.OrdinalIgnoreCase) || !String.Equals(library.Theme.FontFamily, dialog.Theme.FontFamily, StringComparison.OrdinalIgnoreCase) || library.Theme.UiScalePercent != dialog.Theme.UiScalePercent || !String.Equals(library.Theme.ListDensity, dialog.Theme.ListDensity, StringComparison.OrdinalIgnoreCase) || library.Theme.ShowBanner != dialog.Theme.ShowBanner || library.Theme.ShowStatusBar != dialog.Theme.ShowStatusBar || library.Theme.ShowInformationPanel != dialog.Theme.ShowInformationPanel || library.Theme.ShowEmulatorIcons != dialog.Theme.ShowEmulatorIcons || library.Theme.EnableMotion != dialog.Theme.EnableMotion;
                library.Theme = dialog.Theme;
                library.BackupFolder = dialog.BackupFolder;
                Store.Save(library);
                ConfigureGameFolderWatchers();
                if (restartRequired && MessageBox.Show(this, "These settings need FishBowl to restart before they can take effect.\n\nRestart FishBowl now?", "Restart FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Store.Log("Restarting to apply settings.");
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath) { WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = true });
                    Close();
                }
                else if (restartRequired) MessageBox.Show("Settings saved. Restart FishBowl whenever you are ready to apply them.", "FishBowl");
                else SetStatus("Settings saved and applied.");
            }
        }

        private void EnablePortableMode()
        {
            if (Store.PortableMode) { MessageBox.Show("FishBowl is already using portable storage beside the app.", "FishBowl"); return; }
            Store.EnablePortableMode(library);
            MessageBox.Show("Portable mode is ready. Your library is now stored beside FishBowl in the FishBowlData folder.", "FishBowl");
        }


        private string WindowLayoutFile { get { return Path.Combine(Store.DataDirectory, "window-layout.json"); } }
    }



    public class BuildLabelDialog : Form
    {
        private readonly TextBox value = new TextBox();
        public string Value { get { return value.Text.Trim(); } }
        public BuildLabelDialog(string title, string label, string initial)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 160); MinimizeBox = MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedDialog;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3, ColumnCount = 1 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill }, 0, 0); value.Text = initial; value.Dock = DockStyle.Fill; panel.Controls.Add(value, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK }; var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel }; actions.Controls.Add(cancel); actions.Controls.Add(ok);
            panel.Controls.Add(actions, 0, 2); Controls.Add(panel); AcceptButton = ok; CancelButton = cancel;
        }
    }
    public class ReviewDialog : Form
    {
        public ReviewDialog(string title, string detail, string action)
        {
            Text = title; ClientSize = new Size(780, 580); MinimumSize = new Size(640, 460); StartPosition = FormStartPosition.CenterParent;
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new TextBox { Text = detail, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill }, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var confirm = new FishBowlActionButton { Text = action, DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel); buttons.Controls.Add(confirm); shell.Controls.Add(buttons, 0, 1); Controls.Add(shell); CancelButton = cancel;
        }
    }
    public class CandidatePickerDialog : Form
    {
        private readonly ListBox choices = new ListBox();
        public DiscoveredEmulator Selected { get { return choices.SelectedItem as DiscoveredEmulator; } }
        public CandidatePickerDialog(IEnumerable<DiscoveredEmulator> items)
        {
            Text = "Choose the emulator program"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 420);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new Label { Text = "Select the main emulator executable. Helper tools and installers can also appear in the package.", Dock = DockStyle.Fill }, 0, 0);
            choices.Dock = DockStyle.Fill; choices.HorizontalScrollbar = true; choices.Items.AddRange(items.Cast<object>().ToArray()); if (choices.Items.Count > 0) choices.SelectedIndex = 0; shell.Controls.Add(choices, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new FishBowlActionButton { Text = "Choose", DialogResult = DialogResult.OK }; var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok); shell.Controls.Add(buttons, 0, 2); Controls.Add(shell); AcceptButton = ok; CancelButton = cancel;
        }
    }
    public class EmulatorManagerDialog : Form
    {
        private readonly LibraryData library;
        private EmulatorProfile profile;
        private readonly ComboBox profiles = new ComboBox();
        private readonly TabControl tabs = new FishBowlTabs();
        private readonly Label progress = new Label();
        private readonly TextBox rootBox = new TextBox(), backupRootBox = new TextBox(), setupName = new TextBox(), setupProgram = new TextBox(), packageName = new TextBox(), downloadUrl = new TextBox(), repository = new TextBox();
        private readonly ComboBox setupPreset = new ComboBox();
        private readonly ListView discovered = new SmoothListView(), builds = new SmoothListView(), health = new SmoothListView();
        private readonly CheckBox unknownPrograms = new CheckBox(), previews = new CheckBox();
        private readonly TextBox updateText = new TextBox(), backupText = new TextBox(), requirementText = new TextBox();
        private readonly Dictionary<string, CheckBox> categories = new Dictionary<string, CheckBox>();
        private readonly System.Threading.CancellationTokenSource cancellation = new System.Threading.CancellationTokenSource();
        private bool busy, loading;
        private BackupPlan backupPlan;
        private string planProfileId, releaseUrl;
        private readonly Color background = Color.FromArgb(31, 32, 37), surface = Color.FromArgb(47, 48, 56), textColor = Color.FromArgb(235, 235, 241), muted = Color.FromArgb(174, 176, 186);
        public EmulatorManagerDialog(LibraryData data, EmulatorProfile selected, string page)
        {
            library = data; profile = selected; Text = "FishBowl — Emulator management"; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            Icon managementIcon = null; using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("FishBowl.ico")) if (stream != null) { managementIcon = new Icon(stream); Icon = managementIcon; }
            Disposed += delegate { if (managementIcon != null) managementIcon.Dispose(); };
            ClientSize = new Size(960, 730); MinimumSize = new Size(860, 650); BackColor = background; ForeColor = textColor; Font = new Font("Bahnschrift", 9);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3 };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            top.Controls.Add(new Label { Text = "Selected emulator", AutoSize = true, Margin = new Padding(0, 8, 12, 0) }); profiles.Width = 440; profiles.DropDownStyle = ComboBoxStyle.DropDownList; top.Controls.Add(profiles);
            top.Controls.Add(Button("Repair location", RepairLocation)); shell.Controls.Add(top, 0, 0);
            tabs.Dock = DockStyle.Fill; BuildSetup(); BuildDiscovery(); BuildUpdates(); BuildVersions(); BuildBackups(); BuildHealth();
            FishBowlVisuals.Tabs(tabs, background, textColor, Color.FromArgb(183,150,245), "controller", "search", "download", "layers", "backup", "check"); shell.Controls.Add(tabs, 0, 1);
            progress.Dock = DockStyle.Fill; progress.ForeColor = muted; progress.Text = "Emulator tools only. Games stay inside the dedicated emulator."; shell.Controls.Add(progress, 0, 2); Controls.Add(shell);
            profiles.SelectedIndexChanged += delegate { if (loading) return; int i = profiles.SelectedIndex - 1; profile = i >= 0 && i < library.Emulators.Count ? library.Emulators[i] : null; RefreshSelected(); };
            FishBowlHighlights.Attach(this);
            RefreshProfiles(selected); var selectedPage = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text == page); if (selectedPage != null) tabs.SelectedTab = selectedPage;
            FormClosing += delegate { cancellation.Cancel(); };
        }
        private Button Button(string label, Action action)
        {
            var button = new FishBowlActionButton { Text = label, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = textColor, Margin = new Padding(0, 4, 8, 4), Padding = new Padding(8, 2, 8, 2) };
            if (label == "Add to FishBowl" || label == "Create backup" || label == "Use selected build") { button.BackColor = Color.FromArgb(183,150,245); button.ForeColor = background; button.Font = new Font(Font, FontStyle.Bold); }
            button.Click += delegate { try { action(); } catch (Exception ex) { Fail(ex); } }; return button;
        }
        private void Fail(Exception ex)
        {
            Store.Log("Emulator management: " + ex); if (IsDisposed) return;
            progress.Text = "Action could not finish.";
            MessageBox.Show(this, ex.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private async void RunAsync(Func<System.Threading.Tasks.Task> action)
        {
            if (busy) return; busy = true; tabs.Enabled = false; profiles.Enabled = false;
            try { await action(); }
            catch (OperationCanceledException) { if (!IsDisposed) progress.Text = "Cancelled."; }
            catch (Exception ex) { Fail(ex); }
            finally { busy = false; if (!IsDisposed) { tabs.Enabled = true; profiles.Enabled = true; } }
        }
        private TableLayoutPanel Page(string name)
        {
            var tab = new TabPage(name) { BackColor = background, ForeColor = textColor, Padding = new Padding(12), AutoScroll = true };
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = background };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tab.Controls.Add(panel); tabs.TabPages.Add(tab); return panel;
        }
        private void Row(TableLayoutPanel panel, Control control, int height)
        {
            int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
            control.Dock = DockStyle.Fill; panel.Controls.Add(control, 0, row);
        }
        private Label Hint(string text)
        { return new Label { Text = text, ForeColor = muted, Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 2) }; }
        private FlowLayoutPanel Actions(params Control[] items)
        { var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true }; panel.Controls.AddRange(items); return panel; }
        private Control Input(string label, TextBox field, Button button = null)
        {
            var panel = new TableLayoutPanel { ColumnCount = button == null ? 1 : 2, RowCount = 2, Dock = DockStyle.Fill };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); if (button != null) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, ForeColor = textColor, Dock = DockStyle.Fill }, 0, 0);
            field.Dock = DockStyle.Fill; field.BackColor = surface; field.ForeColor = textColor; panel.Controls.Add(field, 0, 1);
            if (button != null) { button.Dock = DockStyle.Fill; panel.Controls.Add(button, 1, 1); } return panel;
        }
        private void StyleText(TextBox box)
        { box.ReadOnly = true; box.Multiline = true; box.ScrollBars = ScrollBars.Vertical; box.BackColor = surface; box.ForeColor = textColor; box.BorderStyle = BorderStyle.FixedSingle; box.Dock = DockStyle.Fill; }
        private void StyleList(ListView list, params string[] columns)
        {
            list.View = View.Details; list.FullRowSelect = true; list.HideSelection = false; list.MultiSelect = false; list.BackColor = surface; list.ForeColor = textColor; list.Dock = DockStyle.Fill;
            foreach (var column in columns) list.Columns.Add(column, column == "Program" || column == "Details" ? 400 : 160);
        }
        private EmulatorProfile Selected()
        { if (profile == null) throw new InvalidOperationException("Choose an emulator at the top first."); return profile; }
        private void RequireStopped(EmulatorProfile p)
        { if (EmulatorRuntime.State(p.Executable) == RuntimeState.Running) throw new IOException("Close " + p.Name + " before changing versions or backing up/restoring its files."); }
        private void RefreshProfiles(EmulatorProfile selected)
        {
            loading = true; profiles.Items.Clear(); profiles.Items.Add("Choose an emulator…");
            foreach (var p in library.Emulators) profiles.Items.Add(p.Name);
            profiles.SelectedIndex = selected != null && library.Emulators.Contains(selected) ? library.Emulators.IndexOf(selected) + 1 : 0; loading = false; profile = selected; RefreshSelected();
        }
        private void RefreshSelected()
        {
            backupPlan = null; backupText.Text = "Choose backup categories and preview the included folders/files.";
            loading = true; repository.Text = profile == null ? "" : (profile.GitHubRepository ?? ""); previews.Checked = profile != null && profile.IncludePreviewReleases; loading = false;
            RefreshUpdates(); RefreshBuilds(); RefreshHealth();
        }
        private void ChooseRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose your dedicated emulator folder. Existing installations stay where they are.", SelectedPath = Directory.Exists(HubPaths.EmulatorRoot(library)) ? HubPaths.EmulatorRoot(library) : "", ShowNewFolderButton = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) { library.EmulatorRootDirectory = Path.GetFullPath(dialog.SelectedPath); Store.Save(library); rootBox.Text = HubPaths.EmulatorRoot(library); }
        }
        private void OpenRoot()
        { var root = HubPaths.EmulatorRoot(library); Directory.CreateDirectory(root); Process.Start(new ProcessStartInfo(root) { UseShellExecute = true }); }
        private void OpenUrl(string url)
        { if (!EmulatorReference.IsWebUrl(url)) throw new IOException("Supply a full official http/https project address."); Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        public EmulatorProfile SelectedProfile { get { return profile; } }
        private void BuildSetup()
        {
            var page = Page("Setup assistant"); page.AutoScroll = true; page.AutoScrollMinSize = new Size(0, 620);
            Row(page, Hint("Choose where emulator packages live. Download from the project's official page, import a ZIP or browse an existing installation, then add its main program. Installer-based emulators can stay in their normal location."), 52);
            rootBox.ReadOnly = true; rootBox.Text = HubPaths.EmulatorRoot(library);
            Row(page, Input("Dedicated emulator folder", rootBox, Button("Choose…", ChooseRoot)), 62);
            setupPreset.DropDownStyle = ComboBoxStyle.DropDownList; setupPreset.Items.AddRange(EmulatorCatalog.Presets.OrderBy(p => p.Name == "Custom" ? "" : p.Name).Select(p => (object)p.Label).ToArray());
            setupPreset.SelectedIndexChanged += delegate { var preset = EmulatorCatalog.Find(setupPreset.Text); downloadUrl.Text = preset.Website; if (preset.Name != "Custom") { setupName.Text = preset.Name; packageName.Text = preset.Name; } };
            Row(page, setupPreset, 36); setupPreset.SelectedIndex = 0;
            Row(page, Input("Official download page (custom emulators can supply their own)", downloadUrl, Button("Open page", delegate { OpenUrl(downloadUrl.Text.Trim()); })), 62);
            Row(page, Input("Folder / build name for a ZIP import", packageName), 62);
            Row(page, Actions(Button("Create / open emulator folder", OpenRoot), Button("Import emulator ZIP…", ImportZip)), 46);
            Row(page, Hint("ZIP imports use a new folder and keep package files together. For 7z/RAR archives or installers, use your normal extraction/installation tool, then browse below. Portable mode remains an emulator-specific choice."), 48);
            Row(page, Input("Main executable or shortcut", setupProgram, Button("Browse…", BrowseProgram)), 62);
            Row(page, Input("Display name", setupName), 62);
            Row(page, Actions(Button("Add to FishBowl", AddProgram), Button("Register as another build", RegisterSetupBuild)), 46);
        }
        private string PickProgram(string initial)
        {
            using (var dialog = new OpenFileDialog { Title = "Choose the emulator's main program", Filter = "Windows launchers|*.exe;*.bat;*.cmd;*.lnk|All files|*.*", InitialDirectory = Directory.Exists(initial) ? initial : "" })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        private void BrowseProgram()
        { var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file != null) FillProgram(file); }
        private void FillProgram(string executable)
        {
            setupProgram.Text = executable; var known = EmulatorDiscovery.Recognize(executable);
            if (EmulatorCatalog.Find(setupPreset.Text).Name == "Custom" && known.Name != "Custom") setupPreset.SelectedItem = known.Label;
            if (String.IsNullOrWhiteSpace(setupName.Text)) setupName.Text = known.Name == "Custom" ? Path.GetFileNameWithoutExtension(executable) : known.Name;
        }
        private void ImportZip()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose an emulator ZIP downloaded from its official project", Filter = "ZIP packages|*.zip" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var zip = dialog.FileName; var root = HubPaths.EmulatorRoot(library); var label = String.IsNullOrWhiteSpace(packageName.Text) ? Path.GetFileNameWithoutExtension(zip) : packageName.Text;
                RunAsync(async delegate
                {
                    progress.Text = "Extracting emulator package…";
                    var folder = await System.Threading.Tasks.Task.Run(() => EmulatorInstaller.InstallZip(zip, root, label, cancellation.Token));
                    var found = await System.Threading.Tasks.Task.Run(() => EmulatorDiscovery.Scan(folder, true, cancellation.Token));
                    if (IsDisposed) return;
                    progress.Text = "Package extracted to " + folder;
                    using (var picker = new CandidatePickerDialog(found.Items.OrderBy(p => EmulatorCatalog.Find(p.Preset).Name == "Custom").ThenBy(p => p.Name)))
                        if (picker.ShowDialog(this) == DialogResult.OK && picker.Selected != null) FillProgram(picker.Selected.Executable);
                });
            }
        }
        public EmulatorProfile RegisterProgram(string executable, string displayName, string preset)
        {
            if (!File.Exists(executable) || !EmulatorReference.IsLaunchFile(executable)) throw new IOException("Choose an existing emulator program or shortcut.");
            if (String.IsNullOrWhiteSpace(displayName)) throw new IOException("Enter an emulator display name.");
            if (library.Emulators.Any(p => HubPaths.Same(p.Executable, executable))) throw new IOException("This emulator program is already registered.");
            var added = new EmulatorProfile { Id = Guid.NewGuid().ToString("N"), Name = displayName.Trim(), Preset = preset, Executable = Path.GetFullPath(executable), Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>(), Builds = new List<EmulatorBuild>() };
            BuildRegistry.Remember(added, added.Executable, "Initial installation"); library.Emulators.Add(added); Store.Save(library); RefreshProfiles(added); return added;
        }
        private void AddProgram()
        {
            var p = RegisterProgram(setupProgram.Text.Trim().Trim('"'), setupName.Text, setupPreset.Text);
            if (EmulatorCatalog.Find(p.Preset).Name == "Custom" && EmulatorReference.IsWebUrl(downloadUrl.Text.Trim())) { p.WebsiteUrl = downloadUrl.Text.Trim(); p.ReleasesUrl = p.WebsiteUrl; Store.Save(library); }
            progress.Text = "Added " + p.Name + ". Storage folders are detected automatically where supported.";
        }
        private void RegisterSetupBuild()
        {
            var p = Selected(); var file = setupProgram.Text.Trim().Trim('"');
            if (!File.Exists(file) || !EmulatorReference.IsLaunchFile(file)) throw new IOException("Choose the extracted emulator program first.");
            BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, packageName.Text); Store.Save(library); RefreshBuilds(); tabs.SelectedIndex = 3; progress.Text = "Build registered. Select it here to make it active.";
        }
        private void BuildDiscovery()
        {
            var page = Page("Find installed");
            Row(page, Hint("Search the dedicated emulator folder, up to four subfolder levels. Game/storage folders and directory links are skipped. Check the programs you want to register. Azahar's shared filename requires choosing the Plus preset in the setup assistant."), 56);
            unknownPrograms.Text = "Also show unrecognized .exe programs"; unknownPrograms.AutoSize = true;
            Row(page, Actions(Button("Find installed emulators", ScanEmulators), unknownPrograms, Button("Add checked programs", AddDiscovered)), 46);
            StyleList(discovered, "Emulator", "Preset", "Program"); discovered.CheckBoxes = true; Row(page, discovered, 0);
        }
        private void ScanEmulators()
        {
            var root = HubPaths.EmulatorRoot(library); bool include = unknownPrograms.Checked;
            RunAsync(async delegate
            {
                progress.Text = "Finding emulator programs…";
                var result = await System.Threading.Tasks.Task.Run(() => EmulatorDiscovery.Scan(root, include, cancellation.Token));
                if (IsDisposed) return; discovered.Items.Clear();
                foreach (var candidate in result.Items)
                {
                    var item = new ListViewItem(candidate.Name) { Tag = candidate }; item.SubItems.Add(candidate.Preset); item.SubItems.Add(candidate.Executable);
                    bool registered = library.Emulators.Any(p => HubPaths.Same(p.Executable, candidate.Executable));
                    item.Checked = !registered && EmulatorCatalog.Find(candidate.Preset).Name != "Custom"; if (registered) item.Text += " (already added)"; discovered.Items.Add(item);
                }
                progress.Text = result.Items.Count + " programs found." + (result.Warnings.Count > 0 ? " " + result.Warnings.First() : "");
            });
        }
        private void AddDiscovered()
        {
            int added = 0;
            foreach (ListViewItem item in discovered.CheckedItems.Cast<ListViewItem>().ToArray())
            {
                var candidate = item.Tag as DiscoveredEmulator;
                if (candidate == null || library.Emulators.Any(p => HubPaths.Same(p.Executable, candidate.Executable))) continue;
                RegisterProgram(candidate.Executable, candidate.Name, candidate.Preset); added++; item.Checked = false;
            }
            progress.Text = "Added " + added + " emulator programs.";
        }
        private void BuildUpdates()
        {
            var page = Page("Updates");
            Row(page, Hint("Checks published releases from the project's GitHub feed when available. No emulator files are replaced automatically. Projects without a feed use their official download page."), 46);
            Row(page, Input("Optional GitHub repository override: owner/repository (blank = preset source)", repository), 62);
            previews.Text = "Include preview/nightly published releases"; previews.AutoSize = true;
            Row(page, Actions(Button("Check for updates", CheckUpdates), Button("Official downloads", delegate { OpenUrl(EmulatorReference.For(Selected()).Releases); }), Button("Open found release", delegate { OpenUrl(releaseUrl); }), previews), 78);
            StyleText(updateText); Row(page, updateText, 0);
        }
        private void RefreshUpdates()
        {
            releaseUrl = profile == null ? "" : profile.LatestReleaseUrl;
            if (profile == null) { updateText.Text = "Select an emulator to check its releases."; return; }
            updateText.Text = "Installed: " + EmulatorReference.VersionFor(profile) + "\r\nRelease source: " + (EmulatorUpdates.Repository(profile).Length > 0 ? EmulatorUpdates.Repository(profile) : "Official download page; no automatic feed") + "\r\n";
            if (!String.IsNullOrWhiteSpace(profile.LastUpdateCheck))
                updateText.AppendText("\r\nLast successful check (UTC): " + profile.LastUpdateCheck + "\r\nLatest published release: " + profile.LatestReleaseTag + "\r\n\r\n" + profile.LatestReleaseNotes);
            else updateText.AppendText("\r\nNo release check has been completed for this profile.");
        }
        private void CheckUpdates()
        {
            var selected = Selected(); var repo = repository.Text.Trim();
            if (repo.Length > 0 && !EmulatorUpdates.ValidRepo(repo)) throw new IOException("Use owner/repository for the GitHub release source.");
            selected.GitHubRepository = repo; selected.IncludePreviewReleases = previews.Checked; Store.Save(library);
            RunAsync(async delegate
            {
                progress.Text = "Checking official published releases…";
                UpdateResult result;
                try { result = await System.Threading.Tasks.Task.Run(() => EmulatorUpdates.Check(selected)); }
                catch (WebException ex)
                {
                    if (IsDisposed) return;
                    RefreshUpdates(); updateText.AppendText("\r\n\r\nThis check failed: " + ex.Message + "\r\nUse Official downloads. Cached results are not a current update check."); progress.Text = "Release feed unavailable. No up-to-date status was inferred."; return;
                }
                if (IsDisposed) return; releaseUrl = result.Url;
                if (result.HasFeed) { selected.LatestReleaseTag = result.Latest; selected.LatestReleaseUrl = result.Url; selected.LatestReleaseNotes = result.Notes.Length > 60000 ? result.Notes.Substring(0, 60000) + "\r\n[Cached notes truncated. Open the release for the full changelog.]" : result.Notes; selected.LastUpdateCheck = result.CheckedAt; Store.Save(library); }
                updateText.Text = result.Status + "\r\n\r\nInstalled: " + result.Installed + "\r\nLatest: " + (result.Latest ?? "See official downloads") + "\r\nChecked (UTC): " + result.CheckedAt + "\r\n\r\n" + result.Notes;
                progress.Text = result.Status;
            });
        }
        private void BuildVersions()
        {
            var page = Page("Versions");
            Row(page, Hint("Register separate installed builds and choose which executable FishBowl opens. Previous builds stay on disk. Switching builds does not move settings or saves; use each emulator's storage rules. Save-state compatibility can vary between builds."), 56);
            Row(page, Actions(Button("Register installed build…", AddBuild), Button("Use selected build", UseBuild), Button("Forget selected entry", ForgetBuild)), 46);
            StyleList(builds, "Build", "Status", "Program"); Row(page, builds, 0);
            Row(page, Actions(Button("Import another build ZIP…", delegate { tabs.SelectedIndex = 0; packageName.Focus(); progress.Text = "Import a ZIP into its own folder, then register the program here as another build."; })), 46);
        }
        private void RefreshBuilds()
        {
            builds.Items.Clear(); if (profile == null) return;
            var entries = new List<EmulatorBuild>(profile.Builds ?? new List<EmulatorBuild>());
            if (!entries.Any(b => HubPaths.Same(b.Executable, profile.Executable))) entries.Insert(0, new EmulatorBuild { Id = "", Label = "Current installation", Executable = profile.Executable, ManualVersion = profile.ManualVersion });
            foreach (var build in entries)
            {
                var item = new ListViewItem(build.Label ?? "Emulator build") { Tag = build }; item.SubItems.Add(HubPaths.Same(build.Executable, profile.Executable) ? "Active" : File.Exists(build.Executable) ? "Available" : "Missing"); item.SubItems.Add(build.Executable); builds.Items.Add(item);
            }
        }
        private void AddBuild()
        {
            var p = Selected(); var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            using (var prompt = new BuildLabelDialog("Emulator build", "Name this installed build", Path.GetFileName(Path.GetDirectoryName(file))))
            { if (prompt.ShowDialog(this) != DialogResult.OK) return; BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, prompt.Value); Store.Save(library); RefreshBuilds(); }
        }
        private void UseBuild()
        {
            var p = Selected(); if (builds.SelectedItems.Count == 0) throw new IOException("Select a build first."); RequireStopped(p);
            var build = builds.SelectedItems[0].Tag as EmulatorBuild; BuildRegistry.Activate(p, build); Store.Save(library); RefreshSelected(); progress.Text = "Active build changed. Review detected/manual storage paths before launching.";
        }
        private void ForgetBuild()
        {
            var p = Selected(); if (builds.SelectedItems.Count == 0) return; var build = builds.SelectedItems[0].Tag as EmulatorBuild;
            if (HubPaths.Same(build.Executable, p.Executable)) throw new IOException("The active build remains registered.");
            if (p.Builds != null) p.Builds.Remove(build); Store.Save(library); RefreshBuilds(); progress.Text = "Build entry forgotten. Its files remain on disk.";
        }
        private void BuildBackups()
        {
            var page = Page("Backups");
            Row(page, Hint("Back up configuration, in-game saves and save states separately. Review the preview before creating an archive. Emulator/ROM files and installed content are excluded. Close the emulator first. Restore preserves a backup of the current files before overwriting."), 58);
            backupRootBox.ReadOnly = true; backupRootBox.Text = HubPaths.BackupRoot(library);
            Row(page, Input("Backup destination", backupRootBox, Button("Choose…", ChooseBackupRoot)), 62);
            var choices = new FlowLayoutPanel { Dock = DockStyle.Fill };
            foreach (var entry in new[] { "ConfigFolder|Configuration", "InGameSaveFolder|In-game saves", "SaveStateFolder|Save states" })
            {
                var pieces = entry.Split('|'); var box = new CheckBox { Text = pieces[1], Checked = true, AutoSize = true, Margin = new Padding(0, 6, 18, 0) };
                box.CheckedChanged += delegate { backupPlan = null; backupText.Text = "Categories changed. Preview the backup again."; }; categories.Add(pieces[0], box); choices.Controls.Add(box);
            }
            Row(page, choices, 34);
            Row(page, Actions(Button("Preview backup", PreviewBackup), Button("Create backup", CreateBackup), Button("Restore ZIP…", RestoreBackup), Button("Open backup folder", delegate { var folder = HubPaths.BackupRoot(library); Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); })), 76);
            StyleText(backupText); Row(page, backupText, 0);
        }
        private void ChooseBackupRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Store emulator backups outside the emulator's source/save folders.", SelectedPath = Directory.Exists(HubPaths.BackupRoot(library)) ? HubPaths.BackupRoot(library) : "" })
                if (dialog.ShowDialog(this) == DialogResult.OK) { library.BackupFolder = dialog.SelectedPath; Store.Save(library); backupRootBox.Text = HubPaths.BackupRoot(library); }
        }
        private void PreviewBackup()
        {
            var selected = Selected(); RequireStopped(selected); var choices = categories.Where(c => c.Value.Checked).Select(c => c.Key).ToArray();
            if (choices.Length == 0) throw new IOException("Choose at least one backup category.");
            RunAsync(async delegate
            {
                progress.Text = "Inspecting configuration and save folders…";
                var plan = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Preview(selected, choices, cancellation.Token));
                if (IsDisposed) return; backupPlan = plan; planProfileId = selected.Id; backupText.Text = plan.Summary(); progress.Text = "Preview ready. Review the source folders and included files.";
            });
        }
        private void CreateBackup()
        {
            var selected = Selected(); RequireStopped(selected); if (backupPlan == null || planProfileId != selected.Id) throw new IOException("Preview the selected backup categories first.");
            var plan = backupPlan; var destination = HubPaths.BackupRoot(library);
            RunAsync(async delegate
            {
                progress.Text = "Creating backup archive…"; var file = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Create(selected, plan, destination, cancellation.Token));
                if (IsDisposed) return; backupText.AppendText("\r\n\r\nCreated:\r\n" + file); progress.Text = "Backup completed.";
            });
        }
        private void RestoreBackup()
        {
            var selected = Selected(); RequireStopped(selected); string file;
            using (var dialog = new OpenFileDialog { Title = "Choose a FishBowl emulator backup", Filter = "FishBowl backups|*.zip", InitialDirectory = Directory.Exists(HubPaths.BackupRoot(library)) ? HubPaths.BackupRoot(library) : "" })
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; file = dialog.FileName; }
            BackupManifest manifest;
            using (var archive = System.IO.Compression.ZipFile.OpenRead(file)) { manifest = EmulatorBackups.ReadManifest(archive); EmulatorBackups.ValidateRestore(selected, manifest); }
            var detail = "Restore backup for " + selected.Name + "?\r\n\r\nMatching files will be overwritten. Other files are retained. Current eligible files are backed up before restoration.\r\n\r\nArchive: " + file + "\r\nCreated (UTC): " + manifest.CreatedAt + "\r\n\r\nDestinations:\r\n" + String.Join("\r\n\r\n", manifest.Roots.Select(r => r.Label + "\r\n" + r.Source)) + "\r\n\r\nIncluded files: " + manifest.Files.Count + "\r\n" + String.Join("\r\n", manifest.Files.Take(300).Select(f => f.ArchivePath)) + (manifest.Files.Count > 300 ? "\r\nFirst 300 shown; the archive manifest lists every file." : "");
            using (var review = new ReviewDialog("Review emulator restore", detail, "Restore these files")) if (review.ShowDialog(this) != DialogResult.OK) return;
            var destination = HubPaths.BackupRoot(library);
            RunAsync(async delegate
            {
                progress.Text = "Validating archive checksums and restoring…"; var before = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Restore(selected, file, destination, cancellation.Token));
                if (IsDisposed) return; backupPlan = null; backupText.Text = "Restore completed.\r\n\r\n" + (before.Length > 0 ? "Previous files preserved in:\r\n" + before : "No existing eligible files needed a backup."); progress.Text = "Restore completed."; RefreshHealth();
            });
        }
        private void BuildHealth()
        {
            var page = Page("Setup checks");
            Row(page, Hint("Check the program path, running status and storage folder availability. An uncreated save folder is informational. BIOS/firmware validity and graphics/controller configuration are checked inside the emulator."), 48);
            Row(page, Actions(Button("Refresh checks", RefreshHealth), Button("Choose firmware folder…", ChooseFirmware), Button("Setup guide", delegate { OpenUrl(EmulatorReference.For(Selected()).Documentation); }), Button("Repair location", RepairLocation)), 76);
            StyleList(health, "Check", "Status", "Details"); Row(page, health, 0); StyleText(requirementText); Row(page, requirementText, 130);
        }
        private void RefreshHealth()
        {
            health.Items.Clear(); if (profile == null) { requirementText.Text = "Select an emulator to review its setup requirements."; return; }
            foreach (var check in EmulatorHealth.Check(profile, library)) { var item = new ListViewItem(check.Name); item.SubItems.Add(check.Status); item.SubItems.Add(check.Detail); item.ToolTipText = check.Detail; health.Items.Add(item); }
            health.ShowItemToolTips = true; requirementText.Text = EmulatorReference.For(profile).Requirements;
        }
        private void ChooseFirmware()
        {
            var selected = Selected(); using (var dialog = new FolderBrowserDialog { Description = "Choose the BIOS/firmware folder already configured inside " + selected.Name, ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) { selected.FirmwareFolder = dialog.SelectedPath; Store.Save(library); RefreshHealth(); }
        }
        private void RepairLocation()
        {
            if (busy) return;
            var selected = Selected(); var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            BuildRegistry.Repair(selected, file); Store.Save(library); RefreshSelected(); progress.Text = "Program location repaired. Your emulator information and folder overrides were retained.";
        }
    }


    public partial class MainForm
    {
        private readonly Timer runtimeTimer = new Timer { Interval = 2500 };
        private readonly Button managementButton = new FishBowlActionButton();
        private bool pollingRuntime;
        private readonly Dictionary<string, RuntimeState> runtimeStates = new Dictionary<string, RuntimeState>(StringComparer.OrdinalIgnoreCase);
        private void ShowEmulatorManager(string page)
        {
            SaveProfileNotes();
            using (var dialog = new EmulatorManagerDialog(library, CurrentEmulator(), page))
            {
                dialog.ShowDialog(this);
                if (dialog.SelectedProfile != null) selectedEmulatorId = dialog.SelectedProfile.Id;
            }
            reloadProgramMetadata = true; RefreshHub();
        }
        private void RepairSelectedLocation()
        {
            var p = CurrentEmulator(); if (p == null) return;
            using (var dialog = new OpenFileDialog { Title = "Locate " + p.Name + "'s emulator program", Filter = "Windows launchers|*.exe;*.bat;*.cmd;*.lnk|All files|*.*", InitialDirectory = Directory.Exists(HubPaths.EmulatorRoot(library)) ? HubPaths.EmulatorRoot(library) : "" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                BuildRegistry.Repair(p, dialog.FileName); Store.Save(library); RefreshHub(); SetStatus("Program location repaired for " + p.Name + ".");
            }
        }
        private async void RefreshRuntimeStatus()
        {
            if (pollingRuntime || IsDisposed) return;
            pollingRuntime = true;
            try
            {
                var paths = library.Emulators.Select(p => p.Executable).Where(p => !String.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var states = await System.Threading.Tasks.Task.Run(() => paths.ToDictionary(p => p, p => {
                    var state = EmulatorRuntime.State(p);
                    var text = !File.Exists(p) ? "Missing program" : state == RuntimeState.Running ? "Running" : state == RuntimeState.Unknown ? "Status unknown" : "Ready";
                    return Tuple.Create(state, text);
                }, StringComparer.OrdinalIgnoreCase)).ConfigureAwait(false);
                PostUi(delegate {
                runtimeStates.Clear(); foreach (var item in states) runtimeStates[item.Key] = item.Value.Item1;
                var profiles = library.Emulators.ToDictionary(p => p.Id);
                foreach (ListViewItem row in emulatorList.Items)
                {
                    EmulatorProfile p; if (!profiles.TryGetValue(row.Tag as string, out p)) continue;
                    Tuple<RuntimeState, string> state; var text = states.TryGetValue(p.Executable ?? "", out state) ? state.Item2 : "Missing program";
                    if (row.SubItems[1].Text != text) { row.SubItems[1].Text = text; emulatorList.Invalidate(row.Bounds); }
                }
                });
            }
            catch (Exception ex) { Store.Log("Runtime status could not refresh: " + ex.Message); }
            finally { pollingRuntime = false; }
        }
    }

    public class EmulatorInformationDialog : Form
    {
        private readonly EmulatorProfile profile;
        private readonly Dictionary<string, TextBox> fields = new Dictionary<string, TextBox>();
        public EmulatorInformationDialog(EmulatorProfile selected)
        {
            profile = selected; Text = "Information - " + selected.Name; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 620); MinimumSize = new Size(620, 520); BackColor = Color.FromArgb(31, 32, 37);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new Label { Text = "These details work with every emulator. Leave a field blank to use its preset information, when available.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(174, 176, 186) }, 0, 0);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            AddTab(tabs, "Overview", new[] { "Platform|Platforms (separate multiple systems with /)", "ManualVersion|Installed version (optional manual override)", "Description|Overview", "Strengths|Strengths / useful features", "Limitations|Known limitations" });
            AddTab(tabs, "Setup & controls", new[] { "Requirements|BIOS / firmware and hardware requirements", "ControllerInfo|Supported controllers / input setup", "ControllerProfileNotes|Your controller notes" });
            AddTab(tabs, "Links", new[] { "WebsiteUrl|Project website", "DocumentationUrl|Setup / documentation", "CompatibilityUrl|Compatibility list", "ReleasesUrl|Releases / downloads", "ChangelogUrl|Changelog", "ControllerGuideUrl|Controller setup guide", "TroubleshootingUrl|Troubleshooting / support" });
            AddTab(tabs, "Folders", new[] { "ConfigFolder|Configuration override (blank = automatic)", "InGameSaveFolder|In-game saves override (blank = automatic)", "SaveStateFolder|Save states override (blank = automatic)", "ScreenshotFolder|Screenshots override (blank = automatic)", "LogFolder|Logs override (blank = automatic)", "SaveFolder|Previous unclassified save shortcut (optional)" });
            shell.Controls.Add(tabs, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var save = new FishBowlActionButton { Text = "Save", Width = 90, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(154, 128, 211), ForeColor = Color.FromArgb(31, 32, 37) };
            save.Click += Save; var cancel = new FishBowlActionButton { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.White };
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); shell.Controls.Add(buttons, 0, 2); Controls.Add(shell); AcceptButton = save; CancelButton = cancel;
        }
        private void AddTab(TabControl tabs, string title, IEnumerable<string> definitions)
        {
            var page = new TabPage(title) { BackColor = Color.FromArgb(35, 36, 42), AutoScroll = true, Padding = new Padding(8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            foreach (var definition in definitions)
            {
                var parts = definition.Split('|'); var property = parts[0];
                bool multiline = new[] { "Description", "Strengths", "Limitations", "Requirements", "ControllerInfo", "ControllerProfileNotes" }.Contains(property);
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, multiline ? 100 : 35));
                table.Controls.Add(new Label { Text = parts[1], Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.BottomLeft }, 0, row++);
                var box = new TextBox { Text = profile.GetType().GetProperty(property).GetValue(profile, null) as string ?? "", Dock = DockStyle.Fill, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                fields.Add(property, box); table.Controls.Add(box, 0, row++);
            }
            table.RowCount = row; page.Controls.Add(table); tabs.TabPages.Add(page);
        }
        private void Save(object sender, EventArgs e)
        {
            foreach (var entry in fields.Where(f => f.Key.EndsWith("Url")))
                if (!String.IsNullOrWhiteSpace(entry.Value.Text) && !EmulatorReference.IsWebUrl(entry.Value.Text.Trim()))
                { MessageBox.Show(this, "Use a full http or https web address for each link, or leave it blank.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            foreach (var entry in fields) profile.GetType().GetProperty(entry.Key).SetValue(profile, entry.Value.Text.Trim(), null);
            DialogResult = DialogResult.OK;
        }
    }

    public class EmulatorDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly TextBox executable = new TextBox();
        private readonly TextBox iconPath = new TextBox();
        private readonly ComboBox preset = new ComboBox();
        private readonly Label hint = new Label();
        private readonly EmulatorProfile existingProfile;
        public EmulatorProfile Profile { get; private set; }

        public EmulatorDialog(EmulatorProfile existing = null, string prefillExecutable = null)
        {
            existingProfile = existing;
            Text = existing == null ? "Add Emulator" : "Edit Emulator";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 386);
            BackColor = Color.FromArgb(31, 32, 37);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 10 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            Controls.Add(form);
            preset.DropDownStyle = ComboBoxStyle.DropDownList;
            preset.DropDownWidth = 660; preset.MaxDropDownItems = 18;
            preset.Items.Add(EmulatorCatalog.Presets[0].Label);
            preset.Items.AddRange(EmulatorCatalog.Presets.Skip(1).OrderBy(p => p.Name).Select(p => (object)p.Label).ToArray());
            preset.SelectedIndexChanged += delegate
            {
                var selected = EmulatorCatalog.Find(preset.Text);
                if (existingProfile == null && selected.Name != "Custom") name.Text = selected.Name;
                hint.Text = selected.Name == "Azahar Plus" ? "For Nintendo 3DS, select the Azahar Plus executable. Games are managed inside Azahar Plus." : "Select the installed emulator's executable. Games and settings stay inside that emulator.";
                if (selected.Platform.IndexOf("experimental", StringComparison.OrdinalIgnoreCase) >= 0) hint.Text = "This emulator is experimental; compatibility varies. Manage games inside the emulator.";
            };
            AddRow(form, "Preset", preset, null, 0);
            AddRow(form, "Display name", name, null, 2);
            AddRow(form, "Emulator program or shortcut", executable, PickExecutable, 4);
            AddRow(form, "Custom emulator image (optional)", iconPath, PickIcon, 6);
            hint.Dock = DockStyle.Fill; hint.ForeColor = Color.FromArgb(174, 176, 186); hint.Font = new Font("Bahnschrift", 9);
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.Controls.Add(hint, 0, 8); form.SetColumnSpan(hint, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var save = new FishBowlActionButton { Text = "Save", Width = 88, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(154, 128, 211), ForeColor = Color.FromArgb(31, 32, 37) };
            save.Click += Save;
            var cancel = new FishBowlActionButton { Text = "Cancel", Width = 88, Height = 30, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) };
            var website = new FishBowlActionButton { Text = "Project website", Width = 126, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) };
            website.Click += delegate
            {
                var selected = EmulatorCatalog.Find(preset.Text);
                if (selected.Website.Length == 0) return;
                try { Process.Start(new ProcessStartInfo(selected.Website) { UseShellExecute = true }); }
                catch (Exception error) { MessageBox.Show(this, "The website could not be opened.\n\n" + error.Message, "FishBowl"); }
            };
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); buttons.Controls.Add(website);
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            form.Controls.Add(buttons, 0, 9); form.SetColumnSpan(buttons, 2);
            AcceptButton = save; CancelButton = cancel;
            if (existing != null)
            {
                var selected = EmulatorCatalog.Find(existing.Preset);
                var label = selected.Name == "Custom" && !String.IsNullOrWhiteSpace(existing.Preset) ? existing.Preset : selected.Label;
                if (!preset.Items.Contains(label)) preset.Items.Add(label);
                preset.SelectedItem = label; name.Text = existing.Name; executable.Text = existing.Executable; iconPath.Text = existing.IconPath;
            }
            else
            {
                preset.SelectedItem = String.IsNullOrWhiteSpace(prefillExecutable) ? EmulatorCatalog.Presets[0].Label : EmulatorCatalog.ForExecutable(prefillExecutable).Label;
                if (!String.IsNullOrWhiteSpace(prefillExecutable)) { executable.Text = prefillExecutable; if (name.Text.Length == 0) name.Text = Path.GetFileNameWithoutExtension(prefillExecutable); }
            }
        }
        private void AddRow(TableLayoutPanel form, string label, Control input, EventHandler browse, int row)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); form.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            form.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(239, 239, 243), Font = new Font("Bahnschrift", 9), TextAlign = ContentAlignment.BottomLeft }, 0, row);
            form.SetColumnSpan(form.GetControlFromPosition(0, row), 2);
            input.Dock = DockStyle.Fill; input.BackColor = Color.FromArgb(47, 48, 56); input.ForeColor = Color.FromArgb(239, 239, 243);
            form.Controls.Add(input, 0, row + 1);
            if (browse == null) form.SetColumnSpan(input, 2);
            else { var button = new FishBowlActionButton { Text = "Browse", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) }; button.Click += browse; form.Controls.Add(button, 1, row + 1); }
        }
        private void PickExecutable(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Programs and shortcuts (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                executable.Text = dialog.FileName;
                var detected = EmulatorCatalog.ForExecutable(dialog.FileName);
                if (preset.SelectedIndex == 0 && detected.Name != "Custom") preset.SelectedItem = detected.Label;
                if (name.Text.Trim().Length == 0) name.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
        private void PickIcon(object sender, EventArgs e) { using (var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico" }) if (dialog.ShowDialog(this) == DialogResult.OK) iconPath.Text = dialog.FileName; }
        private void Save(object sender, EventArgs e)
        {
            var path = executable.Text.Trim().Trim('"');
            if (name.Text.Trim().Length == 0 || !File.Exists(path) || !EmulatorReference.IsLaunchFile(path)) { MessageBox.Show(this, "Enter a display name and choose an existing Windows emulator program or shortcut (.exe, .bat, .cmd, or .lnk).", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Profile = existingProfile ?? new EmulatorProfile { Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>() };
            Profile.Name = name.Text.Trim(); Profile.Preset = preset.Text; Profile.Executable = Path.GetFullPath(path); Profile.IconPath = iconPath.Text.Trim();
            DialogResult = DialogResult.OK;
        }
    }

    public static class FishBowlPromptBranding
    {
        public static void AddLogo(Form form)
        {
            Image logo = null;
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.png"))
                using (var source = stream == null ? null : Image.FromStream(stream))
                    if (source != null) logo = new Bitmap(source);
            }
            catch { }
            if (logo == null) return;
            var picture = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(22, 16), Size = new Size(46, 46), BackColor = Color.Transparent };
            picture.Disposed += delegate { logo.Dispose(); };
            form.Controls.Add(picture);
        }
    }

    public class GameStoragePromptDialog : Form
    {
        private readonly CheckBox dontShowAgain = new CheckBox();
        public bool DontShowAgain { get { return dontShowAgain.Checked; } }
        public bool OpenOrganizer { get; private set; }
        public GameStoragePromptDialog()
        {
            Text = "FishBowl Game Storage"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(570, 294); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            FishBowlPromptBranding.AddLogo(this);
            Controls.Add(new Label { Text = "Keep game files organized", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(80, 22) });
            Controls.Add(new Label { Text = "FishBowl can create a separate Games folder with a different folder for each detected console. Drop game files into FishBowl or open the organizer whenever you want to sort files.\r\n\r\nThis only organizes files on disk. Games are still opened and managed inside their own emulator.", ForeColor = Color.White, Font = new Font("Bahnschrift", 9), Location = new Point(24, 76), Size = new Size(514, 124) });
            dontShowAgain.Text = "Don't show this when FishBowl opens"; dontShowAgain.ForeColor = Color.FromArgb(220, 214, 229); dontShowAgain.BackColor = BackColor; dontShowAgain.Font = new Font("Bahnschrift", 8); dontShowAgain.AutoSize = true; dontShowAgain.Location = new Point(24, 210); Controls.Add(dontShowAgain);
            var continueButton = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(360, 250), Size = new Size(86, 28) }; continueButton.FlatAppearance.BorderSize = 0; Controls.Add(continueButton);
            var openButton = new FishBowlActionButton { Text = "Open organizer", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(454, 250), Size = new Size(94, 28) }; openButton.FlatAppearance.BorderSize = 0; openButton.Click += delegate { OpenOrganizer = true; }; Controls.Add(openButton);
        }
    }

    public class GameStorageOrganizerDialog : Form
    {
        private readonly LibraryData library;
        private readonly ListBox queue = new ListBox();
        private readonly TextBox root = new TextBox();
        private readonly CheckBox copyFiles = new CheckBox();
        private readonly ComboBox forcedConsole = new ComboBox();
        private readonly List<string> queued = new List<string>();
        public GameStorageOrganizerDialog(LibraryData library, IEnumerable<string> droppedPaths)
        {
            this.library = library;
            Text = "FishBowl Game Storage Organizer"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 516); MinimumSize = new Size(680, 450); BackColor = Color.FromArgb(35, 36, 42); FormBorderStyle = FormBorderStyle.Sizable;
            Controls.Add(new Label { Text = "Game Storage Organizer", ForeColor = Color.White, Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(18, 16) });
            Controls.Add(new Label { Text = "Files are sorted into separate console folders. FishBowl does not add them to, or launch them from, the app.", ForeColor = Color.FromArgb(174, 176, 186), Font = new Font("Bahnschrift", 8), AutoSize = true, Location = new Point(20, 45) });
            Controls.Add(new Label { Text = "Games root", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 80), Size = new Size(120, 16) });
            root.ReadOnly = true; root.Text = GameStorage.Root(library); root.BackColor = Color.FromArgb(62, 56, 69); root.ForeColor = Color.White; root.BorderStyle = BorderStyle.FixedSingle; root.Location = new Point(18, 100); root.Size = new Size(552, 24); root.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(root);
            var chooseRoot = Button("Choose root", 580, 98, ChooseRoot); chooseRoot.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(chooseRoot);
            Controls.Add(new Label { Text = "Destination", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 138), Size = new Size(120, 16) });
            forcedConsole.DropDownStyle = ComboBoxStyle.DropDownList; forcedConsole.BackColor = Color.FromArgb(62, 56, 69); forcedConsole.ForeColor = Color.White; forcedConsole.Location = new Point(18, 158); forcedConsole.Size = new Size(300, 24); forcedConsole.Items.Add("Auto detect by file type"); forcedConsole.Items.AddRange(new object[] { "Nintendo Entertainment System", "Super Nintendo", "Game Boy", "Game Boy Color", "Game Boy Advance", "Nintendo DS", "Nintendo 3DS", "Nintendo 64", "Nintendo GameCube", "Nintendo Wii", "Nintendo Wii U", "PlayStation", "PlayStation Portable", "PlayStation Vita", "Xbox", "Xbox 360", "Sega Genesis", "Disc Images", "Arcade and Archives" }); forcedConsole.SelectedIndex = 0; Controls.Add(forcedConsole);
            copyFiles.Text = "Copy files instead of moving them"; copyFiles.ForeColor = Color.White; copyFiles.BackColor = BackColor; copyFiles.Font = new Font("Bahnschrift", 8); copyFiles.AutoSize = true; copyFiles.Location = new Point(340, 161); Controls.Add(copyFiles);
            queue.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; queue.BackColor = Color.FromArgb(47, 48, 56); queue.ForeColor = Color.White; queue.BorderStyle = BorderStyle.None; queue.HorizontalScrollbar = true; queue.Location = new Point(18, 202); queue.Size = new Size(704, 228); queue.AllowDrop = true; queue.DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }; queue.DragDrop += delegate(object sender, DragEventArgs e) { AddPaths(e.Data.GetData(DataFormats.FileDrop) as string[]); }; Controls.Add(queue);
            Controls.Add(Button("Add files", 18, 448, AddFiles)); Controls.Add(Button("Add folder", 108, 448, AddFolder)); Controls.Add(Button("Clear", 208, 448, delegate { queued.Clear(); RefreshQueue(); })); Controls.Add(Button("Open root", 278, 448, OpenRoot));
            var sort = Button("Sort queued files", 588, 448, SortFiles); sort.BackColor = Color.FromArgb(255, 164, 82); sort.ForeColor = Color.FromArgb(40, 25, 14); sort.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; Controls.Add(sort);
            AddPaths(droppedPaths);
        }
        private FishBowlActionButton Button(string text, int x, int y, Action action) { var button = new FishBowlActionButton { Text = text, Location = new Point(x, y), Size = new Size(text == "Sort queued files" ? 134 : 82, 28), BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; return button; }
        private void AddFiles() { using (var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*", Multiselect = true }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames); }
        private void AddFolder() { using (var dialog = new FolderBrowserDialog { Description = "Choose a folder of game files to organize" }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dialog.SelectedPath }); }
        private void ChooseRoot() { using (var dialog = new FolderBrowserDialog { Description = "Choose FishBowl's dedicated games folder", SelectedPath = Directory.Exists(root.Text) ? root.Text : "" }) if (dialog.ShowDialog(this) == DialogResult.OK) { library.GameLibraryRoot = dialog.SelectedPath; root.Text = GameStorage.Root(library); } }
        private void AddPaths(IEnumerable<string> paths) { foreach (var file in GameStorage.Expand(paths).Where(file => !EmulatorReference.IsLaunchFile(file) && !queued.Contains(file, StringComparer.OrdinalIgnoreCase))) queued.Add(file); RefreshQueue(); }
        private void RefreshQueue() { queue.BeginUpdate(); queue.Items.Clear(); foreach (var file in queued) queue.Items.Add(Path.GetFileName(file) + "  —  " + GameStorage.ConsoleFor(file)); queue.EndUpdate(); }
        private void OpenRoot() { Directory.CreateDirectory(GameStorage.Root(library)); Process.Start(new ProcessStartInfo(GameStorage.Root(library)) { UseShellExecute = true }); }
        private void SortFiles()
        {
            if (queued.Count == 0) return; int complete = 0; var errors = new List<string>(); string destinationRoot = GameStorage.Root(library);
            foreach (var file in queued.ToArray())
            {
                try { string console = forcedConsole.SelectedIndex == 0 ? GameStorage.ConsoleFor(file) : forcedConsole.Text; GameStorage.MoveOrCopy(file, destinationRoot, console, copyFiles.Checked); complete++; }
                catch (Exception error) { errors.Add(Path.GetFileName(file) + ": " + error.Message); }
            }
            queued.Clear(); RefreshQueue(); string message = "Organized " + complete + " file" + (complete == 1 ? "." : "s."); if (errors.Count > 0) message += "\n\nCould not organize:\n" + String.Join("\n", errors.Take(5)); MessageBox.Show(this, message, "FishBowl Game Storage");
        }
    }

    public class RequirementsStoragePromptDialog : Form
    {
        private readonly CheckBox dontShowAgain = new CheckBox();
        public bool DontShowAgain { get { return dontShowAgain.Checked; } }
        public bool OpenOrganizer { get; private set; }
        public RequirementsStoragePromptDialog()
        {
            Text = "FishBowl Requirements Storage"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(570, 310); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            FishBowlPromptBranding.AddLogo(this);
            Controls.Add(new Label { Text = "Keep setup files separate", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(80, 22) });
            Controls.Add(new Label { Text = "FishBowl can create a separate Requirements folder with platform and file-type folders for BIOS, firmware, keys, and system archives. It is only a filing system for files you already have.\r\n\r\nFishBowl never downloads, opens, validates, extracts, or configures these files in an emulator. Use your emulator's official setup screens for that.", ForeColor = Color.White, Font = new Font("Bahnschrift", 9), Location = new Point(24, 76), Size = new Size(514, 138) });
            dontShowAgain.Text = "Don't show this when FishBowl opens"; dontShowAgain.ForeColor = Color.FromArgb(220, 214, 229); dontShowAgain.BackColor = BackColor; dontShowAgain.Font = new Font("Bahnschrift", 8); dontShowAgain.AutoSize = true; dontShowAgain.Location = new Point(24, 225); Controls.Add(dontShowAgain);
            var continueButton = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(360, 266), Size = new Size(86, 28) }; continueButton.FlatAppearance.BorderSize = 0; Controls.Add(continueButton);
            var openButton = new FishBowlActionButton { Text = "Open organizer", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(454, 266), Size = new Size(94, 28) }; openButton.FlatAppearance.BorderSize = 0; openButton.Click += delegate { OpenOrganizer = true; }; Controls.Add(openButton);
        }
    }

    public class RequirementsStorageOrganizerDialog : Form
    {
        private readonly LibraryData library;
        private readonly ListBox queue = new ListBox();
        private readonly TextBox root = new TextBox();
        private readonly CheckBox copyFiles = new CheckBox();
        private readonly ComboBox platform = new ComboBox(), kind = new ComboBox();
        private readonly List<string> queued = new List<string>();
        public RequirementsStorageOrganizerDialog(LibraryData library, IEnumerable<string> droppedPaths)
        {
            this.library = library;
            Text = "FishBowl Requirements Storage"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 546); MinimumSize = new Size(680, 470); BackColor = Color.FromArgb(35, 36, 42); FormBorderStyle = FormBorderStyle.Sizable;
            Controls.Add(new Label { Text = "Requirements Storage", ForeColor = Color.White, Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(18, 16) });
            Controls.Add(new Label { Text = "Store user-provided setup files by platform. This never changes emulator folders or configuration.", ForeColor = Color.FromArgb(174, 176, 186), Font = new Font("Bahnschrift", 8), AutoSize = true, Location = new Point(20, 45) });
            Controls.Add(new Label { Text = "Requirements root", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 80), Size = new Size(130, 16) });
            root.ReadOnly = true; root.Text = RequirementsStorage.Root(library); root.BackColor = Color.FromArgb(62, 56, 69); root.ForeColor = Color.White; root.BorderStyle = BorderStyle.FixedSingle; root.Location = new Point(18, 100); root.Size = new Size(552, 24); root.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(root);
            var chooseRoot = Button("Choose root", 580, 98, ChooseRoot); chooseRoot.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(chooseRoot);
            Controls.Add(new Label { Text = "Platform", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 138), Size = new Size(120, 16) });
            platform.DropDownStyle = ComboBoxStyle.DropDown; platform.BackColor = Color.FromArgb(62, 56, 69); platform.ForeColor = Color.White; platform.Location = new Point(18, 158); platform.Size = new Size(280, 24); platform.Items.AddRange(new object[] { "Nintendo Entertainment System", "Super Nintendo", "Nintendo DS", "Nintendo 3DS", "Nintendo 64", "Nintendo GameCube", "Nintendo Wii", "Nintendo Wii U", "PlayStation", "PlayStation 2", "PlayStation Portable", "PlayStation Vita", "Sega Genesis", "Xbox", "Xbox 360", "Arcade", "Other" }); platform.Text = "Other"; Controls.Add(platform);
            Controls.Add(new Label { Text = "File type", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(318, 138), Size = new Size(120, 16) });
            kind.DropDownStyle = ComboBoxStyle.DropDownList; kind.BackColor = Color.FromArgb(62, 56, 69); kind.ForeColor = Color.White; kind.Location = new Point(318, 158); kind.Size = new Size(170, 24); kind.Items.AddRange(new object[] { "Auto detect", "BIOS", "Firmware", "Keys", "System archives", "Needs review" }); kind.SelectedIndex = 0; Controls.Add(kind);
            copyFiles.Text = "Copy files instead of moving them"; copyFiles.ForeColor = Color.White; copyFiles.BackColor = BackColor; copyFiles.Font = new Font("Bahnschrift", 8); copyFiles.AutoSize = true; copyFiles.Location = new Point(505, 161); Controls.Add(copyFiles);
            queue.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; queue.BackColor = Color.FromArgb(47, 48, 56); queue.ForeColor = Color.White; queue.BorderStyle = BorderStyle.None; queue.HorizontalScrollbar = true; queue.Location = new Point(18, 202); queue.Size = new Size(704, 258); queue.AllowDrop = true; queue.DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }; queue.DragDrop += delegate(object sender, DragEventArgs e) { AddPaths(e.Data.GetData(DataFormats.FileDrop) as string[]); }; Controls.Add(queue);
            Controls.Add(Button("Add files", 18, 478, AddFiles)); Controls.Add(Button("Add folder", 108, 478, AddFolder)); Controls.Add(Button("Clear", 208, 478, delegate { queued.Clear(); RefreshQueue(); })); Controls.Add(Button("Open root", 278, 478, OpenRoot));
            var sort = Button("Store queued files", 588, 478, StoreFiles); sort.BackColor = Color.FromArgb(255, 164, 82); sort.ForeColor = Color.FromArgb(40, 25, 14); sort.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; Controls.Add(sort);
            AddPaths(droppedPaths);
        }
        private FishBowlActionButton Button(string text, int x, int y, Action action) { var button = new FishBowlActionButton { Text = text, Location = new Point(x, y), Size = new Size(text == "Store queued files" ? 134 : 82, 28), BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; return button; }
        private void AddFiles() { using (var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*", Multiselect = true }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames); }
        private void AddFolder() { using (var dialog = new FolderBrowserDialog { Description = "Choose a folder of setup files to organize" }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dialog.SelectedPath }); }
        private void ChooseRoot() { using (var dialog = new FolderBrowserDialog { Description = "Choose FishBowl's dedicated requirements folder", SelectedPath = Directory.Exists(root.Text) ? root.Text : "" }) if (dialog.ShowDialog(this) == DialogResult.OK) { library.RequirementsLibraryRoot = dialog.SelectedPath; root.Text = RequirementsStorage.Root(library); } }
        private void AddPaths(IEnumerable<string> paths) { foreach (var file in GameStorage.Expand(paths).Where(file => !EmulatorReference.IsLaunchFile(file) && !queued.Contains(file, StringComparer.OrdinalIgnoreCase))) queued.Add(file); RefreshQueue(); }
        private void RefreshQueue() { queue.BeginUpdate(); queue.Items.Clear(); foreach (var file in queued) queue.Items.Add(Path.GetFileName(file) + "  —  " + RequirementsStorage.KindFor(file)); queue.EndUpdate(); }
        private void OpenRoot() { Directory.CreateDirectory(RequirementsStorage.Root(library)); Process.Start(new ProcessStartInfo(RequirementsStorage.Root(library)) { UseShellExecute = true }); }
        private void StoreFiles()
        {
            if (queued.Count == 0) return; int complete = 0; var errors = new List<string>(); string destinationRoot = RequirementsStorage.Root(library); string selectedPlatform = String.IsNullOrWhiteSpace(platform.Text) ? "Other" : platform.Text.Trim();
            foreach (var file in queued.ToArray())
            {
                try { string selectedKind = kind.SelectedIndex == 0 ? RequirementsStorage.KindFor(file) : kind.Text; RequirementsStorage.MoveOrCopy(file, destinationRoot, selectedPlatform, selectedKind, copyFiles.Checked); complete++; }
                catch (Exception error) { errors.Add(Path.GetFileName(file) + ": " + error.Message); }
            }
            queued.Clear(); RefreshQueue(); string message = "Stored " + complete + " file" + (complete == 1 ? "." : "s."); if (errors.Count > 0) message += "\n\nCould not store:\n" + String.Join("\n", errors.Take(5)); MessageBox.Show(this, message, "FishBowl Requirements Storage");
        }
    }

    public class GameLibraryDialog : Form
    {
        private readonly LibraryData library;
        private readonly ListView games = new ListView();
        private readonly TextBox search = new TextBox();
        private readonly ComboBox scope = new ComboBox();

        public GameLibraryDialog(LibraryData library)
        {
            this.library = library;
            Text = "FishBowl Game Library"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(940, 580); MinimumSize = new Size(760, 480); BackColor = Color.FromArgb(35, 36, 42); FormBorderStyle = FormBorderStyle.Sizable;
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(31, 32, 37), Padding = new Padding(18, 14, 18, 12) };
            header.Controls.Add(new Label { Text = "Game Library", ForeColor = Color.White, Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(18, 12) });
            header.Controls.Add(new Label { Text = "Manage games without changing your emulators.", ForeColor = Color.FromArgb(174, 176, 186), Font = new Font("Bahnschrift", 8), AutoSize = true, Location = new Point(20, 41) });
            search.Anchor = AnchorStyles.Top | AnchorStyles.Right; search.Location = new Point(592, 22); search.Size = new Size(210, 24); search.BackColor = Color.FromArgb(54, 55, 64); search.ForeColor = Color.White; search.BorderStyle = BorderStyle.FixedSingle; search.AccessibleName = "Search games"; search.TextChanged += delegate { RefreshGames(); };
            scope.Anchor = AnchorStyles.Top | AnchorStyles.Right; scope.Location = new Point(812, 22); scope.Size = new Size(110, 24); scope.DropDownStyle = ComboBoxStyle.DropDownList; scope.BackColor = Color.FromArgb(54, 55, 64); scope.ForeColor = Color.White; scope.Items.AddRange(new object[] { "All games", "Favorites", "Recent", "Missing" }); scope.SelectedIndex = 0; scope.SelectedIndexChanged += delegate { RefreshGames(); };
            header.Controls.Add(search); header.Controls.Add(scope); Controls.Add(header);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(47, 48, 56), Padding = new Padding(12, 7, 12, 5), WrapContents = false };
            AddButton(actions, "Add game", AddGame); AddButton(actions, "Edit", EditGame); AddButton(actions, "Launch", LaunchGame); AddButton(actions, "Open folder", OpenGameFolder); AddButton(actions, "Details", ShowDetails); AddButton(actions, "Favorite", ToggleFavorite); AddButton(actions, "Sync folders", SyncFolders); AddButton(actions, "Duplicates", ShowDuplicates); AddButton(actions, "Collection", CreateCollection);
            Controls.Add(actions);
            games.Dock = DockStyle.Fill; games.View = View.Details; games.FullRowSelect = true; games.MultiSelect = true; games.HideSelection = false; games.BorderStyle = BorderStyle.None; games.BackColor = Color.FromArgb(35, 36, 42); games.ForeColor = Color.White; games.Font = new Font("Bahnschrift", 9); games.Columns.Add("Title", 245); games.Columns.Add("Emulator", 150); games.Columns.Add("File", 280); games.Columns.Add("Size", 85); games.Columns.Add("Last played", 130); games.DoubleClick += delegate { LaunchGame(); }; games.AccessibleName = "FishBowl game list";
            Controls.Add(games);
            RefreshGames();
        }
        private void AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new FishBowlActionButton { Text = text, Width = text == "Open folder" ? 92 : 78, Height = 28, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AccessibleName = text };
            button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; panel.Controls.Add(button);
        }
        private IEnumerable<GameEntry> VisibleGames()
        {
            IEnumerable<GameEntry> items = library.Games;
            if (scope.Text == "Favorites") items = items.Where(item => item.Favorite);
            else if (scope.Text == "Recent") items = items.OrderByDescending(item => item.LastLaunched).Take(30);
            else if (scope.Text == "Missing") items = items.Where(item => !File.Exists(item.Path));
            string text = search.Text.Trim(); if (text.Length > 0) items = items.Where(item => (item.Title ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || (item.Path ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            return items.OrderBy(item => item.Title).ToArray();
        }
        private void RefreshGames()
        {
            string selected = SelectedGame() == null ? null : SelectedGame().Id;
            games.BeginUpdate(); games.Items.Clear();
            foreach (var game in VisibleGames())
            {
                var emulator = EmulatorFor(game); var item = new ListViewItem(game.Favorite ? "★ " + game.Title : game.Title) { Tag = game };
                item.SubItems.Add(emulator == null ? "Not assigned" : emulator.Name); item.SubItems.Add(game.Path ?? ""); item.SubItems.Add(File.Exists(game.Path) ? FormatBytes(new FileInfo(game.Path).Length) : "Missing"); item.SubItems.Add(String.IsNullOrWhiteSpace(game.LastLaunched) ? "Never" : game.LastLaunched); games.Items.Add(item);
                if (game.Id == selected) item.Selected = true;
            }
            games.EndUpdate();
        }
        private GameEntry SelectedGame() { return games.SelectedItems.Count == 0 ? null : games.SelectedItems[0].Tag as GameEntry; }
        private EmulatorProfile EmulatorFor(GameEntry game) { return library.Emulators.FirstOrDefault(item => item.Id == (String.IsNullOrWhiteSpace(game.PreferredEmulatorId) ? game.EmulatorId : game.PreferredEmulatorId)) ?? (library.Emulators.Count == 1 ? library.Emulators[0] : null); }
        private void AddGame()
        {
            var game = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "New game", AddedAt = DateTime.UtcNow.ToString("o"), Tags = new List<string>(), EmulatorId = library.Emulators.Count == 1 ? library.Emulators[0].Id : null };
            using (var dialog = new GameDialog(game, library.Emulators)) if (dialog.ShowDialog(this) == DialogResult.OK) { library.Games.Add(dialog.Game); Store.Save(library); RefreshGames(); }
        }
        private void EditGame()
        {
            var game = SelectedGame(); if (game == null) return;
            using (var dialog = new GameDialog(game, library.Emulators)) if (dialog.ShowDialog(this) == DialogResult.OK) { library.Games[library.Games.IndexOf(game)] = dialog.Game; Store.Save(library); RefreshGames(); }
        }
        private void LaunchGame()
        {
            var game = SelectedGame(); if (game == null) return; var emulator = EmulatorFor(game);
            if (emulator == null || !File.Exists(emulator.Executable)) { MessageBox.Show(this, "Assign an available emulator to this game first.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!File.Exists(game.Path)) { MessageBox.Show(this, "This game file is unavailable. Use Edit to repair its path.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (library.Theme.ConfirmBeforeGameLaunch && MessageBox.Show(this, "Launch " + game.Title + " with " + emulator.Name + "?", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string arguments = String.Join(" ", new[] { emulator.Arguments, game.Arguments, "\"" + game.Path + "\"" }.Where(value => !String.IsNullOrWhiteSpace(value)).ToArray());
            Process.Start(new ProcessStartInfo { FileName = emulator.Executable, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(emulator.Executable), UseShellExecute = true });
            game.LastLaunched = DateTime.Now.ToString("g"); game.LaunchCount++; Store.Save(library); RefreshGames();
        }
        private void OpenGameFolder() { var game = SelectedGame(); if (game != null && File.Exists(game.Path)) Process.Start(new ProcessStartInfo(Path.GetDirectoryName(game.Path)) { UseShellExecute = true }); }
        private void ShowDetails() { var game = SelectedGame(); if (game == null) return; using (var dialog = new GameDetailsDialog(game, EmulatorFor(game))) dialog.ShowDialog(this); }
        private void ToggleFavorite() { var game = SelectedGame(); if (game == null) return; game.Favorite = !game.Favorite; Store.Save(library); RefreshGames(); }
        private void SyncFolders() { int added = GameLibraryCatalog.Sync(library); Store.Save(library); RefreshGames(); MessageBox.Show(this, added == 0 ? "No new matching game files were found." : "Added " + added + " game" + (added == 1 ? "." : "s."), "FishBowl"); }
        private void ShowDuplicates()
        {
            var duplicates = library.Games.GroupBy(game => (game.Path ?? "").ToLowerInvariant()).Where(group => group.Key.Length > 0 && group.Count() > 1).SelectMany(group => group.Select(game => game.Title + " — " + game.Path)).ToArray();
            using (var dialog = new ResultsDialog("Duplicate Games", duplicates.Length == 0 ? new[] { "No duplicate game paths were found." } : duplicates)) dialog.ShowDialog(this);
        }
        private void CreateCollection()
        {
            var selected = games.SelectedItems.Cast<ListViewItem>().Select(item => item.Tag as GameEntry).Where(item => item != null).ToList(); if (selected.Count == 0) return;
            using (var prompt = new TextPromptDialog("New Collection", "Collection name")) if (prompt.ShowDialog(this) == DialogResult.OK && prompt.Value.Trim().Length > 0) { library.Collections.Add(new GameCollection { Id = Guid.NewGuid().ToString("N"), Name = prompt.Value.Trim(), GameIds = selected.Select(item => item.Id).ToList() }); Store.Save(library); }
        }
        private static string FormatBytes(long bytes) { return bytes < 1024 * 1024 ? Math.Max(1, bytes / 1024) + " KB" : (bytes / 1024d / 1024d).ToString("0.0") + " MB"; }
    }

    public class GameDialog : Form
    {
        private readonly TextBox title = new TextBox();
        private readonly TextBox path = new TextBox();
        private readonly TextBox artwork = new TextBox();
        private readonly ComboBox preferredEmulator = new ComboBox();
        private readonly TextBox arguments = new TextBox();
        private readonly TextBox notes = new TextBox();
        private readonly CheckBox favorite = new CheckBox();
        private readonly List<EmulatorProfile> emulators;
        public GameEntry Game { get; private set; }

        public GameDialog(GameEntry existing, IEnumerable<EmulatorProfile> profiles)
        {
            emulators = profiles.ToList();
            Text = "Edit Game";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 512);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 13, BackColor = BackColor };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            Controls.Add(form);
            AddRow(form, "Title", title, null, 0);
            AddRow(form, "Game file", path, PickGame, 2);
            AddRow(form, "Cover artwork (optional)", artwork, PickArtwork, 4);
            preferredEmulator.DropDownStyle = ComboBoxStyle.DropDownList;
            preferredEmulator.Items.Add(new EmulatorChoice(null, "Use this library's emulator"));
            foreach (var emulator in emulators) preferredEmulator.Items.Add(new EmulatorChoice(emulator.Id, emulator.Name));
            AddRow(form, "Preferred emulator (optional)", preferredEmulator, null, 6);
            AddRow(form, "Custom launch arguments (optional)", arguments, null, 8);
            var noteLabel = new Label { Text = "Notes", ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            notes.BackColor = Color.FromArgb(62, 56, 69); notes.ForeColor = Color.White; notes.BorderStyle = BorderStyle.FixedSingle; notes.Multiline = true; notes.ScrollBars = ScrollBars.Vertical; notes.Dock = DockStyle.Fill;
            form.Controls.Add(noteLabel, 0, 10); form.SetColumnSpan(noteLabel, 2); form.Controls.Add(notes, 0, 11); form.SetColumnSpan(notes, 2);
            favorite.Text = "Favorite"; favorite.ForeColor = Color.White; favorite.BackColor = BackColor; favorite.Font = new Font("Bahnschrift", 8, FontStyle.Bold); favorite.Location = new Point(18, 438); favorite.AutoSize = true;
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(350, 454), Size = new Size(82, 28) };
            save.FlatAppearance.BorderSize = 0; save.Click += delegate { Save(existing); };
            var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(440, 454), Size = new Size(82, 28) };
            Controls.Add(favorite); Controls.Add(save); Controls.Add(cancel);
            title.Text = existing.Title; path.Text = existing.Path; artwork.Text = existing.ArtworkPath; arguments.Text = existing.Arguments; notes.Text = existing.Notes; favorite.Checked = existing.Favorite;
            preferredEmulator.SelectedIndex = Math.Max(0, preferredEmulator.Items.Cast<EmulatorChoice>().ToList().FindIndex(item => item.Id == existing.PreferredEmulatorId));
        }

        private void AddRow(TableLayoutPanel form, string label, Control input, EventHandler browse, int row)
        {
            var caption = new Label { Text = label, ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.Dock = DockStyle.Fill;
            form.Controls.Add(caption, 0, row); form.SetColumnSpan(caption, 2); form.Controls.Add(input, 0, row + 1);
            if (browse != null) { var button = new FishBowlActionButton { Text = "Browse", Dock = DockStyle.Fill, BackColor = Color.FromArgb(87, 52, 117), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.FlatAppearance.BorderSize = 0; button.Click += browse; form.Controls.Add(button, 1, row + 1); }
            else form.SetColumnSpan(input, 2);
        }

        private void PickGame(object sender, EventArgs args) { using (var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" }) if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName; }
        private void PickArtwork(object sender, EventArgs args) { using (var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*" }) if (dialog.ShowDialog(this) == DialogResult.OK) artwork.Text = dialog.FileName; }
        private void Save(GameEntry existing)
        {
            if (String.IsNullOrWhiteSpace(title.Text) || String.IsNullOrWhiteSpace(path.Text)) { MessageBox.Show("Title and game file are required.", "FishBowl"); DialogResult = DialogResult.None; return; }
            var choice = preferredEmulator.SelectedItem as EmulatorChoice;
            Game = new GameEntry { Id = existing.Id, EmulatorId = existing.EmulatorId, Title = title.Text.Trim(), Path = path.Text.Trim(), ArtworkPath = artwork.Text.Trim(), Arguments = arguments.Text.Trim(), Notes = notes.Text.Trim(), PreferredEmulatorId = choice == null ? null : choice.Id, Favorite = favorite.Checked, AddedAt = existing.AddedAt, LastLaunched = existing.LastLaunched, LaunchCount = existing.LaunchCount, Tags = existing.Tags, Genre = existing.Genre, Developer = existing.Developer, Description = existing.Description, ReleaseYear = existing.ReleaseYear, TotalPlaySeconds = existing.TotalPlaySeconds };
        }

        private class EmulatorChoice
        {
            public string Id { get; private set; }
            private readonly string name;
            public EmulatorChoice(string id, string name) { Id = id; this.name = name; }
            public override string ToString() { return name; }
        }
    }

    public class StartupAssistantDialog : Form
    {
        private readonly CheckBox dontShowAgain = new CheckBox();
        public bool DontShowAgain { get { return dontShowAgain.Checked; } }
        public bool OpenSetupAssistant { get; private set; }

        public StartupAssistantDialog()
        {
            Text = "Welcome to FishBowl"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 308); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            FishBowlPromptBranding.AddLogo(this);
            Controls.Add(new Label { Text = "FishBowl Setup Assistant", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(80, 22) });
            Controls.Add(new Label { Text = "Get your library ready in a few steps:\r\n\r\n• Add an emulator program or shortcut.\r\n• Review its setup checks and official requirements.\r\n• Add or sync games in Game Library.\r\n• Configure controllers and graphics inside each emulator.", ForeColor = Color.White, Font = new Font("Bahnschrift", 9), Location = new Point(24, 76), Size = new Size(500, 142) });
            dontShowAgain.Text = "Don't show this when FishBowl opens"; dontShowAgain.ForeColor = Color.FromArgb(220, 214, 229); dontShowAgain.BackColor = BackColor; dontShowAgain.Font = new Font("Bahnschrift", 8); dontShowAgain.AutoSize = true; dontShowAgain.Location = new Point(24, 221); dontShowAgain.AccessibleName = "Do not show setup assistant at startup"; Controls.Add(dontShowAgain);
            var continueButton = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(350, 258), Size = new Size(86, 28) }; continueButton.FlatAppearance.BorderSize = 0; Controls.Add(continueButton);
            var setupButton = new FishBowlActionButton { Text = "Open setup", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(444, 258), Size = new Size(92, 28) }; setupButton.FlatAppearance.BorderSize = 0; setupButton.Click += delegate { OpenSetupAssistant = true; }; Controls.Add(setupButton);
        }
    }

    public class TextPromptDialog : Form
    {
        private readonly TextBox input = new TextBox();
        public string Value { get { return input.Text; } set { input.Text = value ?? ""; } }

        public TextPromptDialog(string title, string label)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(390, 130); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = label, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 15), Size = new Size(250, 16) });
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.BorderStyle = BorderStyle.FixedSingle; input.Location = new Point(18, 36); input.Size = new Size(354, 24); Controls.Add(input);
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(198, 82), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0;
            Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(290, 82), Size = new Size(82, 28) });
        }
    }

    public class GameMetadataDialog : Form
    {
        private readonly TextBox genre = new TextBox();
        private readonly TextBox developer = new TextBox();
        private readonly TextBox year = new TextBox();
        private readonly TextBox description = new TextBox();

        public GameMetadataDialog(GameEntry game)
        {
            Text = "Game Metadata"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(500, 360); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            AddField("Genre", genre, 18); AddField("Developer", developer, 72); AddField("Release year", year, 126);
            Controls.Add(new Label { Text = "Description", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 180), Size = new Size(180, 16) });
            description.Location = new Point(18, 201); description.Size = new Size(464, 92); description.Multiline = true; description.ScrollBars = ScrollBars.Vertical; description.BackColor = Color.FromArgb(62, 56, 69); description.ForeColor = Color.White; Controls.Add(description);
            Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(400, 314), Size = new Size(82, 28) });
            Controls.Add(new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(235, 158, 94), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(310, 314), Size = new Size(82, 28) });
            genre.Text = game.Genre ?? ""; developer.Text = game.Developer ?? ""; year.Text = game.ReleaseYear ?? ""; description.Text = game.Description ?? "";
        }

        private void AddField(string label, TextBox field, int y)
        {
            Controls.Add(new Label { Text = label, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, y), Size = new Size(180, 16) });
            field.Location = new Point(18, y + 20); field.Size = new Size(464, 24); field.BackColor = Color.FromArgb(62, 56, 69); field.ForeColor = Color.White; Controls.Add(field);
        }

        public void ApplyTo(GameEntry game) { game.Genre = genre.Text.Trim(); game.Developer = developer.Text.Trim(); game.ReleaseYear = year.Text.Trim(); game.Description = description.Text.Trim(); }
    }

    public class ArtworkUrlDialog : Form
    {
        private readonly TextBox url = new TextBox();
        public string Url { get; private set; }

        public ArtworkUrlDialog()
        {
            Text = "Download Cover Artwork"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(460, 150); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Direct image URL", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 15), Size = new Size(190, 16) });
            url.BackColor = Color.FromArgb(62, 56, 69); url.ForeColor = Color.White; url.BorderStyle = BorderStyle.FixedSingle; url.Location = new Point(18, 37); url.Size = new Size(424, 24); Controls.Add(url);
            Controls.Add(new Label { Text = "Paste an http or https link to a PNG, JPG, or other image.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 67), Size = new Size(420, 18) });
            var download = new FishBowlActionButton { Text = "Download", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(266, 104), Size = new Size(84, 28) }; download.FlatAppearance.BorderSize = 0; download.Click += Save;
            Controls.Add(download); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(358, 104), Size = new Size(84, 28) });
        }

        private void Save(object sender, EventArgs args)
        {
            Uri address;
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)) { MessageBox.Show("Enter a complete http or https image address.", "FishBowl"); DialogResult = DialogResult.None; return; }
            Url = address.AbsoluteUri;
        }
    }

    public class ResultsDialog : Form
    {
        public ResultsDialog(string title, IEnumerable<string> rows)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 400); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.Sizable;
            var list = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(62, 56, 69), ForeColor = Color.White, Font = new Font("Bahnschrift", 9), BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
            foreach (var row in rows) list.Items.Add(row);
            Controls.Add(list);
        }
    }

    public class GameDetailsDialog : Form
    {
        public GameDetailsDialog(GameEntry game, EmulatorProfile emulator)
        {
            Text = game.Title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 386); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            var artwork = new PictureBox { BackColor = Color.FromArgb(62, 56, 69), Location = new Point(18, 18), Size = new Size(180, 138), SizeMode = PictureBoxSizeMode.Zoom };
            try { if (!String.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath)) artwork.Image = Image.FromFile(game.ArtworkPath); } catch { }
            var size = File.Exists(game.Path) ? new FileInfo(game.Path).Length : 0;
            var details = "Title: " + game.Title + "\n\nEmulator: " + (emulator == null ? "None" : emulator.Name) + "\n\nFile: " + game.Path + "\n\nSize: " + (File.Exists(game.Path) ? FormatBytes(size) : "Missing file") + "\n\nAdded: " + (game.AddedAt ?? "Unknown") + "\nLast played: " + (game.LastLaunched ?? "Never") + "\nLaunches: " + game.LaunchCount + "\n\nNotes:\n" + (String.IsNullOrWhiteSpace(game.Notes) ? "None" : game.Notes);
            Controls.Add(artwork); Controls.Add(new TextBox { Text = details, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(62, 56, 69), ForeColor = Color.White, BorderStyle = BorderStyle.None, Location = new Point(216, 18), Size = new Size(326, 320), Font = new Font("Bahnschrift", 8) }); Controls.Add(new FishBowlActionButton { Text = "Close", DialogResult = DialogResult.OK, Location = new Point(460, 344), Size = new Size(82, 28) });
        }

        private static string FormatBytes(long bytes) { string[] units = { "B", "KB", "MB", "GB", "TB" }; double value = bytes; int unit = 0; while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; } return value.ToString(unit == 0 ? "0" : "0.0") + " " + units[unit]; }
    }

    public class SettingsDialog : Form
    {
        private readonly ComboBox theme = new ComboBox();
        private readonly TextBox backupFolder = new TextBox();
        private readonly CheckBox startupAssistant = new CheckBox();
        private readonly CheckBox gameStorageAssistant = new CheckBox();
        private readonly CheckBox startMaximized = new CheckBox();
        private readonly CheckBox autoSync = new CheckBox();
        private readonly CheckBox confirmLaunch = new CheckBox();
        private readonly NumericUpDown backupDays = new NumericUpDown();
        private readonly ComboBox accent = new ComboBox();
        private readonly ComboBox fontFamily = new ComboBox();
        private readonly ComboBox uiScale = new ComboBox();
        private readonly ComboBox density = new ComboBox();
        private readonly CheckBox showBanner = new CheckBox();
        private readonly CheckBox showStatusBar = new CheckBox();
        private readonly CheckBox showInformation = new CheckBox();
        private readonly CheckBox showIcons = new CheckBox();
        private readonly CheckBox enableMotion = new CheckBox();
        private readonly CheckBox alternateRows = new CheckBox();
        private readonly ComboBox selectionContrast = new ComboBox();
        private readonly ComboBox iconTileShape = new ComboBox();
        private readonly ThemeSettings originalTheme;
        public ThemeSettings Theme { get; private set; }
        public string BackupFolder { get; private set; }

        public SettingsDialog(ThemeSettings currentTheme, string currentBackupFolder)
        {
            originalTheme = currentTheme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true };
            Text = "FishBowl Settings"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(640, 752); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Theme", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 16), Size = new Size(180, 16) });
            theme.DropDownStyle = ComboBoxStyle.DropDownList; theme.BackColor = Color.FromArgb(62, 56, 69); theme.ForeColor = Color.White; theme.Items.AddRange(new object[] { "Twilight", "Lavender", "Ember", "Light", "High Contrast", "Midnight", "Forest", "Rosewood", "Mist" }); theme.SelectedItem = currentTheme == null ? "Twilight" : currentTheme.Name; theme.Location = new Point(18, 37); theme.Size = new Size(504, 24); Controls.Add(theme);
            Controls.Add(new Label { Text = "Cloud-synced backup folder (optional)", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 76), Size = new Size(280, 16) });
            backupFolder.BackColor = Color.FromArgb(62, 56, 69); backupFolder.ForeColor = Color.White; backupFolder.BorderStyle = BorderStyle.FixedSingle; backupFolder.Location = new Point(18, 97); backupFolder.Size = new Size(416, 24); backupFolder.Text = currentBackupFolder; Controls.Add(backupFolder);
            var browse = new FishBowlActionButton { Text = "Browse", Location = new Point(442, 96), Size = new Size(80, 26), BackColor = Color.FromArgb(87, 52, 117), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; browse.FlatAppearance.BorderSize = 0; browse.Click += Browse; Controls.Add(browse);
            Controls.Add(new Label { Text = "Choose a OneDrive, Dropbox, or other synced folder to receive a copy whenever you export a backup.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 130), Size = new Size(500, 34) });
            startupAssistant.Text = "Show the setup assistant when FishBowl opens"; startupAssistant.ForeColor = Color.White; startupAssistant.BackColor = BackColor; startupAssistant.Font = new Font("Bahnschrift", 8); startupAssistant.AutoSize = true; startupAssistant.Location = new Point(18, 171); startupAssistant.Checked = !originalTheme.StartupAssistantPreferenceSet || originalTheme.ShowStartupAssistant; startupAssistant.AccessibleName = "Show startup setup assistant"; Controls.Add(startupAssistant);
            startMaximized.Text = "Start FishBowl maximized"; startMaximized.ForeColor = Color.White; startMaximized.BackColor = BackColor; startMaximized.Font = new Font("Bahnschrift", 8); startMaximized.AutoSize = true; startMaximized.Location = new Point(18, 201); startMaximized.Checked = originalTheme.StartMaximized; Controls.Add(startMaximized);
            autoSync.Text = "Automatically sync configured game folders"; autoSync.ForeColor = Color.White; autoSync.BackColor = BackColor; autoSync.Font = new Font("Bahnschrift", 8); autoSync.AutoSize = true; autoSync.Location = new Point(18, 231); autoSync.Checked = originalTheme.AutoSyncGameFolders; Controls.Add(autoSync);
            confirmLaunch.Text = "Ask before launching a game from Game Library"; confirmLaunch.ForeColor = Color.White; confirmLaunch.BackColor = BackColor; confirmLaunch.Font = new Font("Bahnschrift", 8); confirmLaunch.AutoSize = true; confirmLaunch.Location = new Point(18, 261); confirmLaunch.Checked = originalTheme.ConfirmBeforeGameLaunch; Controls.Add(confirmLaunch);
            Controls.Add(new Label { Text = "Backup reminder interval", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 301), Size = new Size(180, 16) });
            backupDays.Minimum = 1; backupDays.Maximum = 90; backupDays.Value = Math.Max(1, Math.Min(90, originalTheme.AutoBackupDays)); backupDays.BackColor = Color.FromArgb(62, 56, 69); backupDays.ForeColor = Color.White; backupDays.Location = new Point(18, 321); backupDays.Size = new Size(86, 24); Controls.Add(backupDays);
            Controls.Add(new Label { Text = "days", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(112, 325), Size = new Size(50, 16) });
            gameStorageAssistant.Text = "Show the game storage assistant when FishBowl opens"; gameStorageAssistant.ForeColor = Color.White; gameStorageAssistant.BackColor = BackColor; gameStorageAssistant.Font = new Font("Bahnschrift", 8); gameStorageAssistant.AutoSize = true; gameStorageAssistant.Location = new Point(180, 322); gameStorageAssistant.Checked = !originalTheme.GameStorageAssistantPreferenceSet || originalTheme.ShowGameStorageAssistant; Controls.Add(gameStorageAssistant);
            Controls.Add(new Label { Text = "Appearance", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 11, FontStyle.Bold), Location = new Point(18, 365), Size = new Size(190, 22) });
            AddCaption("Accent color", 18, 396); accent.DropDownStyle = ComboBoxStyle.DropDownList; accent.Items.AddRange(new object[] { "Sunset", "Amethyst", "Ocean", "Rose", "Lime", "Gold", "Ice" }); accent.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.AccentColor) ? "Sunset" : originalTheme.AccentColor; StyleCombo(accent, 18, 416, 184); Controls.Add(accent);
            AddCaption("Interface font", 218, 396); fontFamily.DropDownStyle = ComboBoxStyle.DropDownList; fontFamily.Items.AddRange(new object[] { "Bahnschrift", "Segoe UI", "Calibri", "Arial", "Tahoma", "Verdana", "Trebuchet MS", "Consolas", "Georgia", "Palatino Linotype", "Courier New" }); fontFamily.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.FontFamily) ? "Bahnschrift" : originalTheme.FontFamily; if (fontFamily.SelectedIndex < 0) fontFamily.SelectedIndex = 0; StyleCombo(fontFamily, 218, 416, 184); Controls.Add(fontFamily);
            AddCaption("Interface scale", 418, 396); uiScale.DropDownStyle = ComboBoxStyle.DropDownList; uiScale.Items.AddRange(new object[] { "75%", "80%", "85%", "90%", "100%", "110%", "120%", "125%", "130%", "140%" }); uiScale.SelectedItem = (originalTheme.UiScalePercent <= 0 ? 100 : originalTheme.UiScalePercent).ToString() + "%"; StyleCombo(uiScale, 418, 416, 104); Controls.Add(uiScale);
            AddCaption("List density", 18, 454); density.DropDownStyle = ComboBoxStyle.DropDownList; density.Items.AddRange(new object[] { "Compact", "Standard", "Comfortable" }); density.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.ListDensity) ? "Standard" : originalTheme.ListDensity; StyleCombo(density, 18, 474, 184); Controls.Add(density);
            AddVisualCheck(showBanner, "Show FishBowl banner", 218, 456, originalTheme.ShowBanner);
            AddVisualCheck(showStatusBar, "Show filter and status bar", 218, 482, originalTheme.ShowStatusBar);
            AddVisualCheck(showInformation, "Show emulator information pane", 218, 508, originalTheme.ShowInformationPanel);
            AddVisualCheck(showIcons, "Show emulator icons", 418, 456, originalTheme.ShowEmulatorIcons);
            AddVisualCheck(enableMotion, "Enable hover and selection motion", 418, 482, originalTheme.EnableMotion);
            Controls.Add(new Label { Text = "Fine tuning", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 11, FontStyle.Bold), Location = new Point(18, 554), Size = new Size(190, 22) });
            AddVisualCheck(alternateRows, "Use subtle alternating rows", 18, 586, originalTheme.AlternateRowShading);
            AddCaption("Selection contrast", 218, 582); selectionContrast.DropDownStyle = ComboBoxStyle.DropDownList; selectionContrast.Items.AddRange(new object[] { "Soft", "Standard", "Strong" }); selectionContrast.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.SelectionContrast) ? "Standard" : originalTheme.SelectionContrast; StyleCombo(selectionContrast, 218, 602, 184); Controls.Add(selectionContrast);
            AddCaption("Icon tile shape", 418, 582); iconTileShape.DropDownStyle = ComboBoxStyle.DropDownList; iconTileShape.Items.AddRange(new object[] { "Square", "Rounded", "Circular" }); iconTileShape.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.IconTileShape) ? "Rounded" : originalTheme.IconTileShape; StyleCombo(iconTileShape, 418, 602, 104); Controls.Add(iconTileShape);
            Controls.Add(new Label { Text = "Appearance changes apply after restart.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 648), Size = new Size(360, 18) });
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(450, 708), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0; save.Click += SaveSettings; Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(542, 708), Size = new Size(82, 28) });
        }

        private void AddCaption(string text, int x, int y) { Controls.Add(new Label { Text = text, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(x, y), Size = new Size(180, 16) }); }
        private void StyleCombo(ComboBox box, int x, int y, int width) { box.BackColor = Color.FromArgb(62, 56, 69); box.ForeColor = Color.White; box.Location = new Point(x, y); box.Size = new Size(width, 24); }
        private void AddVisualCheck(CheckBox box, string text, int x, int y, bool value) { box.Text = text; box.ForeColor = Color.White; box.BackColor = BackColor; box.Font = new Font("Bahnschrift", 8); box.AutoSize = true; box.Location = new Point(x, y); box.Checked = value; Controls.Add(box); }
        private void SaveSettings(object sender, EventArgs args)
        {
            int scale; if (!Int32.TryParse((uiScale.Text ?? "100").TrimEnd('%'), out scale)) scale = 100;
            Theme = new ThemeSettings { Name = theme.Text, AutoBackupDays = (int)backupDays.Value, LastBackupAt = originalTheme.LastBackupAt, DiscordRichPresenceEnabled = originalTheme.DiscordRichPresenceEnabled, ShowStartupAssistant = startupAssistant.Checked, StartupAssistantPreferenceSet = true, ShowGameStorageAssistant = gameStorageAssistant.Checked, GameStorageAssistantPreferenceSet = true, ShowRequirementsStorageAssistant = originalTheme.ShowRequirementsStorageAssistant, RequirementsStorageAssistantPreferenceSet = originalTheme.RequirementsStorageAssistantPreferenceSet, StartMaximized = startMaximized.Checked, AutoSyncGameFolders = autoSync.Checked, ConfirmBeforeGameLaunch = confirmLaunch.Checked, AccentColor = accent.Text, FontFamily = fontFamily.Text, UiScalePercent = scale, ListDensity = density.Text, ShowBanner = showBanner.Checked, ShowStatusBar = showStatusBar.Checked, ShowInformationPanel = showInformation.Checked, ShowEmulatorIcons = showIcons.Checked, EnableMotion = enableMotion.Checked, AlternateRowShading = alternateRows.Checked, SelectionContrast = selectionContrast.Text, IconTileShape = iconTileShape.Text, HubPerformanceMode = String.IsNullOrWhiteSpace(originalTheme.HubPerformanceMode) ? "Balanced" : originalTheme.HubPerformanceMode, PauseBackgroundRefresh = originalTheme.PauseBackgroundRefresh, ConfirmBeforeEmulatorLaunch = originalTheme.ConfirmBeforeEmulatorLaunch, ConfirmBeforeEmulatorRemoval = originalTheme.ConfirmBeforeEmulatorRemoval, ShowCommandHints = originalTheme.ShowCommandHints, CheckHealthOnStartup = originalTheme.CheckHealthOnStartup, CustomizationVersion = 6 };
            BackupFolder = backupFolder.Text.Trim();
        }

        private void Browse(object sender, EventArgs args) { using (var dialog = new FolderBrowserDialog()) if (dialog.ShowDialog(this) == DialogResult.OK) backupFolder.Text = dialog.SelectedPath; }
    }

    public class CommandPaletteDialog : Form
    {
        private readonly TextBox filter = new TextBox(); private readonly ListBox list = new ListBox();
        private readonly string[] commands = { "Add emulator", "Open selected emulator", "Game storage organizer", "Requirements storage organizer", "Setup workspace", "Controller profiles", "Notifications / health", "FishBowl settings", "Full screen" };
        public string Command { get { return list.SelectedItem as string; } }
        public CommandPaletteDialog()
        {
            Text = "FishBowl Command Palette"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 390); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            filter.BackColor = Color.FromArgb(62, 56, 69); filter.ForeColor = Color.White; filter.BorderStyle = BorderStyle.FixedSingle; filter.Location = new Point(18, 18); filter.Size = new Size(524, 25); filter.TextChanged += delegate { RefreshCommands(); }; Controls.Add(filter);
            list.BackColor = Color.FromArgb(62, 56, 69); list.ForeColor = Color.White; list.BorderStyle = BorderStyle.None; list.Location = new Point(18, 58); list.Size = new Size(524, 274); list.DoubleClick += delegate { if (Command != null) DialogResult = DialogResult.OK; }; Controls.Add(list);
            Controls.Add(new Label { Text = "Search FishBowl actions. Ctrl+P opens this palette.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 342), Size = new Size(350, 18) });
            Controls.Add(new FishBowlActionButton { Text = "Run", DialogResult = DialogResult.OK, Location = new Point(366, 348), Size = new Size(82, 28) }); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(458, 348), Size = new Size(82, 28) });
            RefreshCommands(); Shown += delegate { filter.Focus(); };
        }
        private void RefreshCommands() { var query = (filter.Text ?? "").Trim(); list.Items.Clear(); foreach (var command in commands.Where(c => query.Length == 0 || c.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)) list.Items.Add(command); if (list.Items.Count > 0) list.SelectedIndex = 0; }
    }

    public class ControllerProfilesDialog : Form
    {
        private readonly LibraryData library; private readonly EmulatorProfile emulator; private readonly ListBox profiles = new ListBox(); private readonly TextBox name = new TextBox(), notes = new TextBox();
        public ControllerProfilesDialog(LibraryData library, EmulatorProfile emulator)
        {
            this.library = library; this.emulator = emulator; Text = "FishBowl Controller Profiles"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(680, 470); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            profiles.BackColor = Color.FromArgb(62, 56, 69); profiles.ForeColor = Color.White; profiles.BorderStyle = BorderStyle.None; profiles.Location = new Point(18, 18); profiles.Size = new Size(218, 382); profiles.SelectedIndexChanged += delegate { LoadSelected(); }; Controls.Add(profiles);
            AddLabel("Profile name", 256, 18); Style(name, 256, 38, 404, 24); Controls.Add(name); AddLabel("Setup notes", 256, 78); Style(notes, 256, 98, 404, 230); notes.Multiline = true; notes.ScrollBars = ScrollBars.Vertical; Controls.Add(notes);
            Controls.Add(new Label { Text = emulator == null ? "Select an emulator to add a profile." : "Profiles are notes only; FishBowl does not overwrite " + emulator.Name + " mappings.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(256, 338), Size = new Size(404, 34) });
            var add = new FishBowlActionButton { Text = "New", Location = new Point(18, 414), Size = new Size(68, 28) }; add.Click += delegate { Add(); }; Controls.Add(add); var remove = new FishBowlActionButton { Text = "Remove", Location = new Point(94, 414), Size = new Size(76, 28) }; remove.Click += delegate { Remove(); }; Controls.Add(remove);
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(484, 414), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0; save.Click += delegate { SaveSelected(); }; Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(576, 414), Size = new Size(82, 28) }); RefreshProfiles();
        }
        private void AddLabel(string text, int x, int y) { Controls.Add(new Label { Text = text, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(x, y), Size = new Size(180, 16) }); }
        private static void Style(TextBox box, int x, int y, int width, int height) { box.BackColor = Color.FromArgb(62, 56, 69); box.ForeColor = Color.White; box.BorderStyle = BorderStyle.FixedSingle; box.Location = new Point(x, y); box.Size = new Size(width, height); }
        private List<ControllerProfile> Items { get { return library.ControllerProfiles.Where(p => emulator == null || p.EmulatorId == emulator.Id).OrderBy(p => p.Name).ToList(); } }
        private void RefreshProfiles() { profiles.Items.Clear(); foreach (var item in Items) profiles.Items.Add(item); profiles.DisplayMember = "Name"; if (profiles.Items.Count > 0) profiles.SelectedIndex = 0; }
        private void LoadSelected() { var item = profiles.SelectedItem as ControllerProfile; name.Text = item == null ? "" : item.Name; notes.Text = item == null ? "" : item.Notes; }
        private void Add() { if (emulator == null) return; var item = new ControllerProfile { Id = Guid.NewGuid().ToString("N"), EmulatorId = emulator.Id, Name = "New profile", Notes = "", UpdatedAt = DateTime.UtcNow.ToString("o") }; library.ControllerProfiles.Add(item); RefreshProfiles(); profiles.SelectedItem = item; }
        private void Remove() { var item = profiles.SelectedItem as ControllerProfile; if (item == null) return; library.ControllerProfiles.Remove(item); RefreshProfiles(); }
        private void SaveSelected() { var item = profiles.SelectedItem as ControllerProfile; if (item == null) return; item.Name = String.IsNullOrWhiteSpace(name.Text) ? "Controller profile" : name.Text.Trim(); item.Notes = (notes.Text ?? "").Trim(); item.UpdatedAt = DateTime.UtcNow.ToString("o"); }
    }

    public class MorePreferencesDialog : Form
    {
        private readonly CheckBox launch = new CheckBox(), remove = new CheckBox(), hints = new CheckBox(), health = new CheckBox(), requirementsAssistant = new CheckBox();
        public MorePreferencesDialog(ThemeSettings theme)
        {
            Text = "FishBowl More Preferences"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 350); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Optional behavior", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 15, FontStyle.Bold), Location = new Point(20, 18), Size = new Size(350, 28) });
            Add(launch, "Confirm before opening an emulator", 20, 78, theme.ConfirmBeforeEmulatorLaunch); Add(remove, "Confirm before removing an emulator from FishBowl", 20, 110, theme.ConfirmBeforeEmulatorRemoval); Add(hints, "Show command and keyboard hints", 20, 142, theme.ShowCommandHints); Add(health, "Check hub health when FishBowl opens", 20, 174, theme.CheckHealthOnStartup);
            Add(requirementsAssistant, "Show requirements storage assistant when FishBowl opens", 20, 206, !theme.RequirementsStorageAssistantPreferenceSet || theme.ShowRequirementsStorageAssistant);
            Controls.Add(new Label { Text = "These choices only change FishBowl. They never alter emulator settings, games, saves, or controller mappings.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(20, 245), Size = new Size(500, 34) });
            Controls.Add(new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(372, 306), Size = new Size(82, 28) }); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(464, 306), Size = new Size(82, 28) });
        }
        private void Add(CheckBox box, string text, int x, int y, bool value) { box.Text = text; box.Checked = value; box.ForeColor = Color.White; box.BackColor = BackColor; box.Font = new Font("Bahnschrift", 9); box.AutoSize = true; box.Location = new Point(x, y); Controls.Add(box); }
        public void ApplyTo(ThemeSettings theme) { theme.ConfirmBeforeEmulatorLaunch = launch.Checked; theme.ConfirmBeforeEmulatorRemoval = remove.Checked; theme.ShowCommandHints = hints.Checked; theme.CheckHealthOnStartup = health.Checked; theme.ShowRequirementsStorageAssistant = requirementsAssistant.Checked; theme.RequirementsStorageAssistantPreferenceSet = true; theme.CustomizationVersion = 6; }
    }

    public class PerformanceDialog : Form
    {
        private readonly ComboBox mode = new ComboBox();
        private readonly CheckBox pause = new CheckBox();
        public string Mode { get { return mode.Text; } }
        public bool PauseBackgroundRefresh { get { return pause.Checked; } }
        public PerformanceDialog(ThemeSettings theme)
        {
            Text = "FishBowl Performance"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 270); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Hub performance", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 15, FontStyle.Bold), Location = new Point(22, 18), Size = new Size(360, 30) });
            Controls.Add(new Label { Text = "These choices only affect FishBowl. Emulator graphics and performance settings remain inside each emulator.", ForeColor = Color.FromArgb(218, 210, 226), Font = new Font("Bahnschrift", 9), Location = new Point(22, 54), Size = new Size(470, 40) });
            Controls.Add(new Label { Text = "Hub mode", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(22, 110), Size = new Size(160, 16) });
            mode.DropDownStyle = ComboBoxStyle.DropDownList; mode.Items.AddRange(new object[] { "Balanced", "Power saver", "Responsive" }); mode.SelectedItem = String.IsNullOrWhiteSpace(theme.HubPerformanceMode) ? "Balanced" : theme.HubPerformanceMode; if (mode.SelectedIndex < 0) mode.SelectedIndex = 0; mode.BackColor = Color.FromArgb(62, 56, 69); mode.ForeColor = Color.White; mode.Location = new Point(22, 131); mode.Size = new Size(220, 24); Controls.Add(mode);
            pause.Text = "Pause automatic running-status checks"; pause.Checked = theme.PauseBackgroundRefresh; pause.ForeColor = Color.White; pause.BackColor = BackColor; pause.Font = new Font("Bahnschrift", 9); pause.AutoSize = true; pause.Location = new Point(22, 180); Controls.Add(pause);
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(336, 226), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0; Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(428, 226), Size = new Size(82, 28) });
        }
    }

    public class MultiplayerDialog : Form
    {
        private readonly ComboBox emulator = new ComboBox();
        private readonly ComboBox mode = new ComboBox();
        private readonly CheckBox inviteOnly = new CheckBox();
        private readonly List<EmulatorProfile> profiles;
        private readonly MultiplayerSettings original;
        public EmulatorProfile SelectedProfile { get { return emulator.SelectedItem as EmulatorProfile; } }
        public string Mode { get { return mode.SelectedItem as string ?? "Local play"; } }
        public MultiplayerSettings Settings { get; private set; }

        public MultiplayerDialog(IEnumerable<EmulatorProfile> emulators, EmulatorProfile selected, MultiplayerSettings current)
        {
            original = current ?? new MultiplayerSettings { InviteOnly = true, RelayProvider = "LiveKit Cloud", UpdateChannel = "Stable", CheckForHubUpdates = true };
            profiles = emulators.OrderBy(p => p.Name).ToList();
            Text = "FishBowl Multiplayer"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(580, 372); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            var title = new Label { Text = "Multiplayer session", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 15, FontStyle.Bold), Location = new Point(22, 18), Size = new Size(400, 30) }; Controls.Add(title);
            Controls.Add(new Label { Text = "Choose an emulator and session type. FishBowl opens emulators unchanged.", ForeColor = Color.FromArgb(218, 210, 226), Font = new Font("Bahnschrift", 9), Location = new Point(23, 53), Size = new Size(520, 36) });
            AddLabel("Emulator", 22, 105); emulator.DropDownStyle = ComboBoxStyle.DropDownList; emulator.DataSource = profiles; emulator.DisplayMember = "Name"; emulator.Location = new Point(22, 126); emulator.Size = new Size(536, 26); Style(emulator); if (selected != null) emulator.SelectedItem = profiles.FirstOrDefault(p => p.Id == selected.Id); if (emulator.SelectedIndex < 0 && profiles.Any()) emulator.SelectedIndex = 0; Controls.Add(emulator);
            AddLabel("Session type", 22, 168); mode.DropDownStyle = ComboBoxStyle.DropDownList; mode.Items.AddRange(new object[] { "Local play", "Native online", "Remote couch play" }); mode.SelectedIndex = 0; mode.Location = new Point(22, 189); mode.Size = new Size(536, 26); Style(mode); Controls.Add(mode);
            inviteOnly.Text = "Keep remote sessions invite-only"; inviteOnly.Checked = original.InviteOnly; inviteOnly.ForeColor = Color.White; inviteOnly.BackColor = BackColor; inviteOnly.Font = new Font("Bahnschrift", 9); inviteOnly.AutoSize = true; inviteOnly.Location = new Point(22, 236); Controls.Add(inviteOnly);
            Controls.Add(new Label { Text = "Native online uses the selected emulator's own feature. Remote couch play is prepared for LiveKit Cloud; FishBowl never shares an emulator's files or changes its settings.", ForeColor = Color.FromArgb(190, 181, 201), Font = new Font("Bahnschrift", 8), Location = new Point(22, 264), Size = new Size(520, 46) });
            var start = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(380, 330), Size = new Size(84, 28) }; start.FlatAppearance.BorderSize = 0; start.Click += Save; Controls.Add(start);
            Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(474, 330), Size = new Size(84, 28) });
        }
        private void AddLabel(string text, int x, int y) { Controls.Add(new Label { Text = text, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(x, y), Size = new Size(180, 16) }); }
        private static void Style(ComboBox box) { box.BackColor = Color.FromArgb(62, 56, 69); box.ForeColor = Color.White; }
        private void Save(object sender, EventArgs args)
        {
            if (SelectedProfile == null) { MessageBox.Show(this, "Add an emulator before starting a session.", "FishBowl"); DialogResult = DialogResult.None; return; }
            Settings = new MultiplayerSettings { InviteOnly = inviteOnly.Checked, RememberRecentSessions = original.RememberRecentSessions, ShareDiagnosticsWithHost = original.ShareDiagnosticsWithHost, RelayProvider = "LiveKit Cloud", RelayGatewayUrl = original.RelayGatewayUrl, UpdateChannel = original.UpdateChannel, CheckForHubUpdates = original.CheckForHubUpdates };
        }
    }

    public class MultiplayerSettingsDialog : Form
    {
        private readonly TextBox relay = new TextBox();
        private readonly ComboBox channel = new ComboBox();
        private readonly CheckBox inviteOnly = new CheckBox(), remember = new CheckBox(), diagnostics = new CheckBox(), checkUpdates = new CheckBox();
        private readonly MultiplayerSettings original;
        public MultiplayerSettings Settings { get; private set; }
        public MultiplayerSettingsDialog(MultiplayerSettings current)
        {
            original = current ?? new MultiplayerSettings { InviteOnly = true, RelayProvider = "LiveKit Cloud", UpdateChannel = "Stable", CheckForHubUpdates = true };
            Text = "Multiplayer Privacy and Connection"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(620, 454); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Privacy and connection", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 15, FontStyle.Bold), Location = new Point(22, 18), Size = new Size(400, 30) });
            Controls.Add(new Label { Text = "Remote couch play uses LiveKit Cloud. Enter only your token endpoint. Keep the LiveKit API secret on that server, where it can issue short-lived session credentials.", ForeColor = Color.FromArgb(218, 210, 226), Font = new Font("Bahnschrift", 9), Location = new Point(23, 54), Size = new Size(560, 46) });
            Label caption = new Label { Text = "LiveKit token endpoint (optional)", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(22, 116), Size = new Size(250, 16) }; Controls.Add(caption);
            relay.Text = original.RelayGatewayUrl ?? ""; relay.BackColor = Color.FromArgb(62, 56, 69); relay.ForeColor = Color.White; relay.BorderStyle = BorderStyle.FixedSingle; relay.Location = new Point(22, 137); relay.Size = new Size(574, 24); Controls.Add(relay);
            AddCheck(inviteOnly, "Keep remote sessions invite-only", 22, 186, original.InviteOnly);
            AddCheck(remember, "Remember recent session names on this device", 22, 216, original.RememberRecentSessions);
            AddCheck(diagnostics, "Share basic diagnostics with the session host", 22, 246, original.ShareDiagnosticsWithHost);
            AddCheck(checkUpdates, "Check FishBowl's public release page for updates", 22, 276, original.CheckForHubUpdates);
            Controls.Add(new Label { Text = "Release channel", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(22, 316), Size = new Size(160, 16) });
            channel.DropDownStyle = ComboBoxStyle.DropDownList; channel.Items.AddRange(new object[] { "Stable", "Beta" }); channel.SelectedItem = String.Equals(original.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable"; channel.BackColor = Color.FromArgb(62, 56, 69); channel.ForeColor = Color.White; channel.Location = new Point(22, 337); channel.Size = new Size(160, 24); Controls.Add(channel);
            Controls.Add(new Label { Text = "Checking only opens release information. FishBowl will never replace its own files without a separately reviewed installer or package update.", ForeColor = Color.FromArgb(190, 181, 201), Font = new Font("Bahnschrift", 8), Location = new Point(202, 321), Size = new Size(390, 42) });
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(420, 412), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0; save.Click += Save; Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(512, 412), Size = new Size(84, 28) });
        }
        private void AddCheck(CheckBox box, string text, int x, int y, bool value) { box.Text = text; box.Checked = value; box.ForeColor = Color.White; box.BackColor = BackColor; box.Font = new Font("Bahnschrift", 9); box.AutoSize = true; box.Location = new Point(x, y); Controls.Add(box); }
        private void Save(object sender, EventArgs args)
        {
            var value = (relay.Text ?? "").Trim(); Uri uri;
            if (value.Length > 0 && (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https")) { MessageBox.Show(this, "Use a full https LiveKit token endpoint, or leave it blank.", "FishBowl"); DialogResult = DialogResult.None; return; }
            Settings = new MultiplayerSettings { InviteOnly = inviteOnly.Checked, RememberRecentSessions = remember.Checked, ShareDiagnosticsWithHost = diagnostics.Checked, RelayProvider = "LiveKit Cloud", RelayGatewayUrl = value, UpdateChannel = channel.Text, CheckForHubUpdates = checkUpdates.Checked };
        }
    }

    public class WebsiteLinkDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly TextBox url = new TextBox();
        public WebsiteLink Link { get; private set; }

        public WebsiteLinkDialog()
        {
            Text = "Add Website Link";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 202);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 5, BackColor = BackColor };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            Controls.Add(form);
            AddField(form, "Link name", name, 0);
            AddField(form, "Website address", url, 2);
            var note = new Label { Text = "Use a full link, for example https://example.com", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            form.Controls.Add(note, 0, 4); form.SetColumnSpan(note, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, BackColor = BackColor, Padding = new Padding(18, 0, 18, 8) };
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, Width = 82, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14) };
            save.FlatAppearance.BorderSize = 0; save.Click += Save;
            footer.Controls.Add(save); footer.Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 82 });
            Controls.Add(footer);
        }

        private void AddField(TableLayoutPanel form, string label, TextBox input, int row)
        {
            var caption = new Label { Text = label, ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.BorderStyle = BorderStyle.FixedSingle; input.Dock = DockStyle.Fill;
            form.Controls.Add(caption, 0, row); form.SetColumnSpan(caption, 2); form.Controls.Add(input, 0, row + 1); form.SetColumnSpan(input, 2);
        }

        private void Save(object sender, EventArgs args)
        {
            Uri address;
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("Enter a complete http or https website address.", "FishBowl");
                DialogResult = DialogResult.None;
                return;
            }
            Link = new WebsiteLink { Id = Guid.NewGuid().ToString("N"), Name = String.IsNullOrWhiteSpace(name.Text) ? address.Host : name.Text.Trim(), Url = address.AbsoluteUri };
        }
    }

    public class WebsiteLinkPicker : Form
    {
        private readonly ComboBox links = new ComboBox();
        public WebsiteLink SelectedLink { get; private set; }

        public WebsiteLinkPicker(IEnumerable<WebsiteLink> savedLinks)
        {
            Text = "Remove Website Link";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 128);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var label = new Label { Text = "Saved link", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 16), Size = new Size(170, 16) };
            links.DropDownStyle = ComboBoxStyle.DropDownList; links.BackColor = Color.FromArgb(62, 56, 69); links.ForeColor = Color.White; links.Location = new Point(18, 36); links.Size = new Size(364, 24);
            foreach (var link in savedLinks.OrderBy(item => item.Name)) links.Items.Add(new WebsiteLinkChoice(link));
            if (links.Items.Count > 0) links.SelectedIndex = 0;
            var remove = new FishBowlActionButton { Text = "Remove", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(204, 79), Size = new Size(84, 28) };
            remove.FlatAppearance.BorderSize = 0; remove.Click += delegate { SelectedLink = ((WebsiteLinkChoice)links.SelectedItem).Link; };
            Controls.Add(label); Controls.Add(links); Controls.Add(remove); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(298, 79), Size = new Size(84, 28) });
        }

        private class WebsiteLinkChoice
        {
            public WebsiteLink Link { get; private set; }
            public WebsiteLinkChoice(WebsiteLink link) { Link = link; }
            public override string ToString() { return Link.Name + "  -  " + Link.Url; }
        }
    }



    public static class FishBowlVisuals
    {
        public static GraphicsPath Round(RectangleF box, float radius)
        {
            var p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(box.Width, box.Height));
            if (d <= 0) { p.AddRectangle(box); return p; }
            p.AddArc(box.X, box.Y, d, d, 180, 90); p.AddArc(box.Right - d, box.Y, d, d, 270, 90);
            p.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90); p.AddArc(box.X, box.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static string IconForText(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Contains("favorite") || t.Contains("favour")) return t.Contains("★") || t.Contains("favorited") ? "star-filled" : "star";
            if (t.StartsWith("cancel") || t.StartsWith("close")) return "close";
            if (t.StartsWith("remove") || t.StartsWith("forget")) return "remove";
            if (t.Contains("firmware") || t.Contains("bios")) return "chip";
            if (t.Contains("restore")) return "restore";
            if (t.Contains("preview") || t.StartsWith("find") || t.Contains("search")) return "search";
            if (t.StartsWith("create backup")) return "backup";
            if (t.Contains("backup folder")) return "folder";
            if (t.Contains("backup")) return "backup";
            if (t.Contains("repair")) return "repair";
            if (t.Contains("import") || t.Contains("zip")) return "import";
            if (t.StartsWith("register")) return "layers-add";
            if (t.Contains("use selected") || t.Contains("compatibility") || t.Contains("checks")) return "check";
            if (t.StartsWith("add")) return "add";
            if (t.Contains("controller")) return "controller";
            if (t.StartsWith("edit") && !t.Contains("information")) return "edit";
            if (t.Contains("information")) return "info";
            if (t.StartsWith("manage") || t.Contains("settings")) return "settings";
            if (t.StartsWith("refresh") || t == "auto" || t.Contains("check for updates")) return "refresh";
            if (t.Contains("changelog") || t.Contains("notes")) return "note";
            if (t.Contains("guide") || t.Contains("documentation")) return "book";
            if (t.Contains("troubleshoot") || t.Contains("help")) return "help";
            if (t.Contains("download") || t.Contains("releases") || t.Contains("found release")) return "download";
            if (t.Contains("project") || t.Contains("open page")) return "globe";
            if (t.StartsWith("save") || t == "ok" || t == "choose") return "check";
            if (t.Contains("browse") || t.Contains("choose") || t.Contains("folder") || t == "open") return "folder";
            if (t.Contains("open emulator")) return "play";
            if (t.Contains("in-game saves")) return "save";
            if (t.Contains("save states")) return "state";
            return "arrow";
        }
        private static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();
        private static readonly object iconCacheLock = new object();
        public static Image Icon(string kind, int size, Color ink, Color accent)
        {
            string key = kind + "|" + size + "|" + ink.ToArgb() + "|" + accent.ToArgb();
            lock (iconCacheLock)
            {
                Image image;
                if (!iconCache.TryGetValue(key, out image))
                {
                    // Each caller owns its clone. Keep the shared artwork cache bounded.
                    if (iconCache.Count >= 96) { foreach (var old in iconCache.Values) old.Dispose(); iconCache.Clear(); }
                    image = DrawIcon(kind, size, ink, accent); iconCache[key] = image;
                }
                return new Bitmap(image);
            }
        }

        private static Image DrawIcon(string kind, int size, Color ink, Color accent)
        {
            using (var canvas = new Bitmap(size * 4, size * 4))
            {
                using (var g = Graphics.FromImage(canvas))
                using (var pen = new Pen(ink, 2.3f)) using (var mark = new Pen(accent, 2.3f))
                using (var wash = new SolidBrush(Color.FromArgb(24, ink)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size / 8f, size / 8f);
                    pen.StartCap = pen.EndCap = mark.StartCap = mark.EndCap = LineCap.Round; pen.LineJoin = mark.LineJoin = LineJoin.Round;
                    Action<float,float,float,float,float> box = (x,y,w,h,r) => { using(var p=Round(new RectangleF(x,y,w,h),r)) { g.FillPath(wash,p); g.DrawPath(pen,p); } };
                    switch(kind)
                    {
                        case "add": box(5,5,22,22,6); g.DrawLine(mark,16,10,16,22); g.DrawLine(mark,10,16,22,16); break;
                        case "remove": case "close": g.DrawLine(pen,9,9,23,23); g.DrawLine(mark,23,9,9,23); break;
                        case "edit": g.DrawLines(pen,new[]{new PointF(7,21),new PointF(7,26),new PointF(12,25),new PointF(25,12),new PointF(20,7),new PointF(7,21)}); g.DrawLine(mark,18,10,23,15); break;
                        case "settings": g.DrawLine(pen,5,8,27,8); g.DrawLine(pen,5,16,27,16); g.DrawLine(pen,5,24,27,24); using(var b=new SolidBrush(ink)){g.FillEllipse(b,9,5,6,6);g.FillEllipse(b,19,13,6,6);}g.FillEllipse(mark.Brush,11,21,6,6); break;
                        case "folder": using(var p=new GraphicsPath()){p.AddLines(new[]{new PointF(5,10),new PointF(5,7),new PointF(12,7),new PointF(16,11),new PointF(27,11),new PointF(27,25),new PointF(5,25)});p.CloseFigure();g.FillPath(wash,p);g.DrawPath(pen,p);}g.DrawLine(mark,9,17,23,17);break;
                        case "info": g.DrawEllipse(pen,5,5,22,22);g.FillEllipse(mark.Brush,14.5f,8,3,3);g.DrawLine(mark,16,14,16,23);break;
                        case "help": g.DrawEllipse(pen,5,5,22,22);g.DrawArc(mark,11,9,10,8,185,270);g.DrawLine(mark,16,17,16,19);g.FillEllipse(mark.Brush,14.5f,22,3,3);break;
                        case "search": g.DrawEllipse(pen,5,5,17,17);g.DrawLine(mark,21,21,27,27);break;
                        case "refresh": g.DrawArc(pen,6,6,20,20,40,280);g.DrawLines(mark,new[]{new PointF(26,6),new PointF(26,12),new PointF(20,12)});break;
                        case "restore": g.DrawArc(pen,6,6,20,20,210,285);g.DrawLines(mark,new[]{new PointF(5,6),new PointF(5,12),new PointF(11,12)});g.DrawLine(pen,16,11,16,17);g.DrawLine(mark,16,17,21,20);break;
                        case "backup": g.DrawLines(pen,new[]{new PointF(6,9),new PointF(16,5),new PointF(26,9),new PointF(26,23),new PointF(16,27),new PointF(6,23),new PointF(6,9)});g.DrawLines(pen,new[]{new PointF(6,9),new PointF(16,14),new PointF(26,9)});g.DrawLine(mark,16,14,16,27);g.DrawLine(mark,12,8,21,12);break;
                        case "import": case "download": box(6,21,20,6,2);g.DrawLine(mark,16,5,16,18);g.DrawLines(mark,new[]{new PointF(10,13),new PointF(16,19),new PointF(22,13)});break;
                        case "export": box(6,21,20,6,2);g.DrawLine(mark,16,6,16,19);g.DrawLines(mark,new[]{new PointF(10,11),new PointF(16,5),new PointF(22,11)});break;
                        case "check": g.DrawEllipse(pen,5,5,22,22);g.DrawLines(mark,new[]{new PointF(10,16),new PointF(14,20),new PointF(22,12)});break;
                        case "star": case "star-filled": var pts=new PointF[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5;float r=i%2==0?11.5f:5.5f;pts[i]=new PointF(16+(float)Math.Cos(a)*r,16+(float)Math.Sin(a)*r);}if(kind=="star-filled")using(var starFill=new SolidBrush(Color.FromArgb(55,accent)))g.FillPolygon(starFill,pts);g.DrawPolygon(mark,pts);break;
                        case "controller": case "game": using(var p=Round(new RectangleF(4,9,24,15),6)){g.FillPath(wash,p);g.DrawPath(pen,p);}g.DrawLine(pen,9,16.5f,15,16.5f);g.DrawLine(pen,12,13.5f,12,19.5f);g.FillEllipse(mark.Brush,20,13,3,3);g.FillEllipse(mark.Brush,23,17,3,3);break;
                        case "globe": g.DrawEllipse(pen,5,5,22,22);g.DrawEllipse(pen,11,5,10,22);g.DrawLine(mark,6,16,26,16);break;
                        case "book": g.DrawLines(pen,new[]{new PointF(16,8),new PointF(11,6),new PointF(5,6),new PointF(5,25),new PointF(11,25),new PointF(16,27),new PointF(21,25),new PointF(27,25),new PointF(27,6),new PointF(21,6),new PointF(16,8),new PointF(16,27)});g.DrawLine(mark,9,12,12,12);g.DrawLine(mark,20,12,23,12);break;
                        case "note": box(7,4,18,24,3);g.DrawLine(mark,12,10,20,10);g.DrawLine(pen,12,16,20,16);g.DrawLine(pen,12,22,17,22);break;
                        case "layers": case "layers-add": g.DrawPolygon(pen,new[]{new PointF(16,4),new PointF(28,10),new PointF(16,16),new PointF(4,10)});g.DrawLines(pen,new[]{new PointF(5,16),new PointF(16,22),new PointF(27,16)});if(kind=="layers"){g.DrawLines(mark,new[]{new PointF(5,22),new PointF(16,28),new PointF(27,22)});}else{g.DrawLine(mark,23,22,23,29);g.DrawLine(mark,19.5f,25.5f,26.5f,25.5f);}break;
                        case "chip": box(9,9,14,14,3);box(13,13,6,6,1);for(int i=0;i<3;i++){float t=11+i*5;g.DrawLine(mark,t,5,t,9);g.DrawLine(mark,t,23,t,27);g.DrawLine(pen,5,t,9,t);g.DrawLine(pen,23,t,27,t);}break;
                        case "save": box(6,5,20,22,3);box(10,5,12,8,1);g.DrawLine(mark,11,20,21,20);break;
                        case "state": box(5,6,22,20,4);g.DrawArc(mark,11,11,10,10,210,290);g.DrawLine(mark,16,13,16,16);g.DrawLine(mark,16,16,20,18);break;
                        case "desktop": box(4,5,24,17,3);g.DrawLine(pen,16,22,16,27);g.DrawLine(mark,10,27,22,27);break;
                        case "storage": box(5,8,22,17,3);g.DrawLine(pen,5,14,27,14);g.FillEllipse(mark.Brush,21,18,3,3);break;
                        case "repair": g.DrawLine(mark,8,25,20,13);g.DrawEllipse(pen,5,22,5,5);g.DrawArc(pen,17,5,10,10,30,300);break;
                        case "image": box(5,5,22,22,3);g.FillEllipse(mark.Brush,19,9,4,4);g.DrawLines(pen,new[]{new PointF(8,23),new PointF(13,16),new PointF(18,21),new PointF(22,17),new PointF(26,23)});break;
                        case "power":g.DrawArc(pen,6,6,20,20,40,280);g.DrawLine(mark,16,4,16,16);break;
                        case "play":g.DrawPolygon(mark,new[]{new PointF(11,6),new PointF(26,16),new PointF(11,26)});break;
                        case "copy":box(5,5,16,18,3);box(11,10,16,18,3);break;
                        case "library":box(5,6,22,21,3);g.DrawLine(pen,11,6,11,27);g.DrawLine(mark,16,12,22,12);g.DrawLine(mark,16,18,22,18);break;
                        case "tag":g.DrawPolygon(pen,new[]{new PointF(5,6),new PointF(16,6),new PointF(27,17),new PointF(17,27),new PointF(5,15)});g.FillEllipse(mark.Brush,9,10,3,3);break;
                        default:g.DrawLine(pen,6,16,25,16);g.DrawLines(mark,new[]{new PointF(19,10),new PointF(25,16),new PointF(19,22)});break;
                    }
                }
                var output = new Bitmap(size,size); using(var g=Graphics.FromImage(output)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(canvas,new Rectangle(0,0,size,size));}return output;
            }
        }
        public static void Tabs(TabControl tabs, Color background, Color foreground, Color accent, params string[] kinds)
        {
            var images=new ImageList { ColorDepth=ColorDepth.Depth32Bit, ImageSize=new Size(18,18) };var sources=new List<Image>();
            foreach(var kind in kinds) {var icon=Icon(kind,18,foreground,accent);sources.Add(icon);images.Images.Add(icon);}
            tabs.ImageList=images;for(int i=0;i<tabs.TabPages.Count;i++)tabs.TabPages[i].ImageIndex=i<images.Images.Count?i:-1;
            tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.Appearance=TabAppearance.FlatButtons;tabs.Multiline=true;tabs.SizeMode=TabSizeMode.Fixed;tabs.Padding=new Point(8,4);
            tabs.ItemSize=new Size(tabs.TabPages.Cast<TabPage>().Any(p=>p.Text=="Setup assistant")?148:110,32);
            var styled=tabs as FishBowlTabs;if(styled!=null){styled.SurfaceColor=background;styled.HeaderTextColor=foreground;styled.AccentColor=accent;styled.ManagementPages=tabs.TabPages.Cast<TabPage>().Any(p=>p.Text=="Setup assistant");styled.FitTabs();}
            tabs.Disposed+=delegate{tabs.ImageList=null;images.Dispose();foreach(var icon in sources)icon.Dispose();};
        }
    }

    public class FishBowlTabs : TabControl
    {
        public Color SurfaceColor = Color.FromArgb(31,32,37), HeaderTextColor = Color.FromArgb(235,235,241), AccentColor = Color.FromArgb(183,150,245);
        public bool ManagementPages; private bool fittingTabs;
        public FishBowlTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        public void FitTabs(){if(fittingTabs||ClientSize.Width<=0)return;fittingTabs=true;try{int columns=ManagementPages&&ClientSize.Width>=900?6:3;int width=Math.Max(100,(ClientSize.Width-(columns-1)*10-8)/columns);if(ItemSize.Width!=width||ItemSize.Height!=32)ItemSize=new Size(width,32);}finally{fittingTabs=false;}}
        protected override void OnResize(EventArgs e){base.OnResize(e);FitTabs();Invalidate();}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int next=SelectedIndex;if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Down)next=Math.Min(TabPages.Count-1,next+1);else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Up)next=Math.Max(0,next-1);else if(e.KeyCode==Keys.Home)next=0;else if(e.KeyCode==Keys.End)next=TabPages.Count-1;else {base.OnKeyDown(e);return;}
            if(next>=0)SelectedIndex=next;e.Handled=true;e.SuppressKeyPress=true;Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SurfaceColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            for(int i=0;i<TabPages.Count;i++)
            {
                var page=TabPages[i];var bounds=GetTabRect(i);var r=Rectangle.Inflate(bounds,-2,-2);bool selected=SelectedIndex==i;
                using(var p=FishBowlVisuals.Round(r,5))using(var b=new SolidBrush(selected?FishBowlHighlights.Blend(SurfaceColor,24):SurfaceColor))e.Graphics.FillPath(b,p);
                if(selected)using(var b=new SolidBrush(AccentColor))e.Graphics.FillRectangle(b,r.Left+7,r.Bottom-3,Math.Max(1,r.Width-14),2);
                int left=r.Left+8;if(ImageList!=null&&page.ImageIndex>=0&&page.ImageIndex<ImageList.Images.Count){e.Graphics.DrawImage(ImageList.Images[page.ImageIndex],new Rectangle(left,r.Top+(r.Height-18)/2,18,18));left+=24;}
                var fg=selected?HeaderTextColor:FishBowlHighlights.Blend(SurfaceColor,150);
                TextRenderer.DrawText(e.Graphics,page.Text,Font,new Rectangle(left,r.Top,Math.Max(1,r.Right-left-5),r.Height),fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            }
            var frame=Rectangle.Inflate(DisplayRectangle,1,1);using(var pen=new Pen(FishBowlHighlights.Blend(SurfaceColor,22)))e.Graphics.DrawRectangle(pen,frame);
        }
    }

    public class SmoothListView : ListView
    {
        public SmoothListView() { DoubleBuffered = true; }
    }

    public class FishBowlActionButton : Button
    {
        private bool hovering, pressed; private Image ownedIcon; private string iconKind; private int iconColor;
        public FishBowlActionButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AutoSizeMode=AutoSizeMode.GrowAndShrink;TextImageRelation=TextImageRelation.ImageBeforeText;ImageAlign=ContentAlignment.MiddleLeft;
            TextChanged+=delegate{RefreshIcon();};ForeColorChanged+=delegate{RefreshIcon();};
        }
        private void RefreshIcon()
        {
            string kind=FishBowlVisuals.IconForText(Text);int color=ForeColor.ToArgb();if(kind==iconKind&&color==iconColor)return;
            iconKind=kind;iconColor=color;var old=ownedIcon;
            bool darkInk=ForeColor.GetBrightness()<0.55f;ownedIcon=FishBowlVisuals.Icon(kind,20,darkInk?ForeColor:Color.FromArgb(224,208,255),darkInk?ForeColor:Color.FromArgb(255,180,105));Image=ownedIcon;if(old!=null)old.Dispose();
        }
        public override Size GetPreferredSize(Size proposedSize){var size=base.GetPreferredSize(proposedSize);return new Size(size.Width+4,Math.Max(34,size.Height));}
        protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovering=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnEnabledChanged(EventArgs e){if(!Enabled){hovering=pressed=false;}Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(0.5f,0.5f,Math.Max(1,Width-1),Math.Max(1,Height-1));
            Color bg=Enabled&&(hovering||pressed)?FishBowlHighlights.Blend(BackColor,pressed?36:24):BackColor;
            bool dark=BackColor.GetBrightness()<0.65f;Color border=Enabled&&Focused?(dark?Color.FromArgb(206,176,250):ForeColor):FishBowlHighlights.Blend(BackColor,Enabled?22:12);
            using(var p=FishBowlVisuals.Round(r,6))using(var b=new SolidBrush(bg))using(var pen=new Pen(border,Focused&&Enabled?1.5f:1f)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}
            Color fg=Enabled?ForeColor:dark?Color.FromArgb(127,129,144):Color.FromArgb(130,132,146);
            var textSize=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);bool showIcon=Image!=null&&Width>=textSize.Width+36;
            int iconWidth=showIcon?20:0;int gap=showIcon?7:0;int total=iconWidth+gap+textSize.Width;int left=Math.Max(7,(Width-total)/2);int offset=pressed&&Enabled?1:0;
            if(showIcon){var box=new Rectangle(left+offset,(Height-20)/2+offset,20,20);if(Enabled)e.Graphics.DrawImage(Image,box);else using(var attributes=new System.Drawing.Imaging.ImageAttributes()){var m=new System.Drawing.Imaging.ColorMatrix { Matrix33=0.38f };attributes.SetColorMatrix(m);e.Graphics.DrawImage(Image,box,0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}left+=iconWidth+gap;}
            var textBox=new Rectangle(left+offset,offset,Math.Max(1,Width-left-7),Height-2*offset);TextRenderer.DrawText(e.Graphics,Text,Font,textBox,fg,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        protected override void Dispose(bool disposing){if(disposing&&ownedIcon!=null){var image=ownedIcon;ownedIcon=null;Image=null;image.Dispose();}base.Dispose(disposing);}
    }

    public static class FishBowlHighlights
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Attached = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        public static Color Blend(Color background, int opacity = 24)
        {
            var light = background.GetBrightness() < 0.65f ? Color.White : Color.Black;
            return Color.FromArgb((background.R * (255 - opacity) + light.R * opacity) / 255,
                (background.G * (255 - opacity) + light.G * opacity) / 255,
                (background.B * (255 - opacity) + light.B * opacity) / 255);
        }
        public static void Draw(Graphics graphics, Rectangle bounds, Color background, bool selected = false)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1) return;
            var light = background.GetBrightness() < 0.65f ? Color.White : Color.Black;
            using (var p = FishBowlVisuals.Round(new RectangleF(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), 5))
            using (var brush = new SolidBrush(Color.FromArgb(selected ? 32 : 24, light))) using (var pen = new Pen(Color.FromArgb(selected ? 48 : 36, light))) { graphics.FillPath(brush, p); graphics.DrawPath(pen, p); }
        }
        public static void Attach(Control control)
        {
            object marker; if (Attached.TryGetValue(control, out marker)) return; Attached.Add(control, new object());
            var button = control as Button;
            if (button != null)
            {
                Action update = delegate { button.FlatAppearance.MouseOverBackColor = Blend(button.BackColor); button.FlatAppearance.MouseDownBackColor = Blend(button.BackColor, 36); };
                update(); button.BackColorChanged += delegate { update(); };
            }
            var picture = control as PictureBox;
            if (picture != null)
            {
                bool hovering = false;
                picture.MouseEnter += delegate { hovering = true; picture.Invalidate(); };
                picture.MouseLeave += delegate { hovering = false; picture.Invalidate(); };
                picture.Paint += delegate(object sender, PaintEventArgs e) { if (hovering && picture.Image != null) Draw(e.Graphics, Rectangle.Inflate(picture.ClientRectangle, -1, -1), picture.Parent == null ? picture.BackColor : picture.Parent.BackColor); };
            }
            control.ControlAdded += delegate(object sender, ControlEventArgs e) { Attach(e.Control); };
            foreach (Control child in control.Controls) Attach(child);
        }
    }
    public class FishBowlMenuRenderer : ToolStripProfessionalRenderer
    {
        public FishBowlMenuRenderer() : base(new FishBowlMenuColors()) { RoundedEdges = false; }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed))
                FishBowlHighlights.Draw(e.Graphics, new Rectangle(1, 1, Math.Max(1, e.Item.Width - 2), Math.Max(1, e.Item.Height - 2)), e.ToolStrip.BackColor, e.Item.Pressed);
            else base.OnRenderMenuItemBackground(e);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed)) FishBowlHighlights.Draw(e.Graphics, Rectangle.Inflate(e.ImageRectangle, 2, 2), e.ToolStrip.BackColor, e.Item.Pressed);
            base.OnRenderItemImage(e);
        }
    }

    public class FishBowlMenuColors : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Color.FromArgb(31, 32, 37); } }
        public override Color MenuStripGradientEnd { get { return Color.FromArgb(31, 32, 37); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(47, 48, 56); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(69, 63, 84); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(69, 63, 84); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(69, 63, 84); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(47, 48, 56); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(47, 48, 56); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(47, 48, 56); } }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Store.ShowWarning = delegate(string message) { MessageBox.Show(message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            { Store.Log("Interface action failed: " + e.Exception); MessageBox.Show("FishBowl could not complete this action.\n\n" + e.Exception.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); };
            try { Application.Run(new MainForm()); }
            catch (Exception error) { Store.Log("Startup failed: " + error); MessageBox.Show("FishBowl could not start.\n\n" + error.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
