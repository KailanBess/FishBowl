using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal static class ModernUi
    {
        private static readonly ConditionalWeakTable<Form, object> rebuilt = new ConditionalWeakTable<Form, object>();
        private static readonly ConditionalWeakTable<TabControl, object> decks = new ConditionalWeakTable<TabControl, object>();
        private static readonly ConditionalWeakTable<TabPage, object> pages = new ConditionalWeakTable<TabPage, object>();
        private static readonly ConditionalWeakTable<TableLayoutPanel, ModernFieldLayout> fieldLayouts = new ConditionalWeakTable<TableLayoutPanel, ModernFieldLayout>();
        private static readonly ConditionalWeakTable<Control, object> watched = new ConditionalWeakTable<Control, object>();
        private sealed class PendingPresentation { internal bool Pending; }
        private static readonly ConditionalWeakTable<Form, PendingPresentation> pending = new ConditionalWeakTable<Form, PendingPresentation>();
        private static readonly ConditionalWeakTable<ListView, ModernCatalog> catalogs = new ConditionalWeakTable<ListView, ModernCatalog>();
        private sealed class OwnedFont { internal Font Value; }
        private static readonly ConditionalWeakTable<Control, OwnedFont> fonts = new ConditionalWeakTable<Control, OwnedFont>();
        private static readonly MethodInfo localVisibility = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool LocallyVisible(Control control)
        {
            return localVisibility == null ? control.Visible || control.Parent != null && !control.Parent.Visible : (bool)localVisibility.Invoke(control, new object[] { 2 });
        }

        internal static void SetFont(Control control, float size, FontStyle style)
        {
            if (Math.Abs(control.Font.Size - size) < .01f && control.Font.Style == style) return;
            var owned = fonts.GetValue(control, delegate(Control key)
            {
                var value = new OwnedFont();
                key.Disposed += delegate { if (value.Value != null) value.Value.Dispose(); };
                return value;
            });
            var previous = owned.Value;
            owned.Value = new Font(control.Font.FontFamily, size, style);
            control.Font = owned.Value;
            if (previous != null) previous.Dispose();
        }

        internal static bool PresentationVisible(ListView source)
        {
            ModernCatalog catalog;
            return catalogs.TryGetValue(source, out catalog) ? catalog.Visible : source.Visible;
        }

        internal static ListViewItem[] VisibleRows(ListView source)
        {
            ModernCatalog catalog;
            if (catalogs.TryGetValue(source, out catalog)) return catalog.VisibleRows();
            return source.Items.Cast<ListViewItem>().Where(item => item.Bounds.IntersectsWith(source.ClientRectangle)).ToArray();
        }

        internal static ModernCatalog Catalog(ListView source, string kind, bool grid, Action<ListViewItem> open)
        {
            ModernCatalog existing;
            if (catalogs.TryGetValue(source, out existing)) return existing;
            var catalog = new ModernCatalog(source, kind, grid, open);
            catalogs.Add(source, catalog);
            Replace(source, catalog);
            catalog.Attach();
            return catalog;
        }

        internal static void RebuildDialog(Form form)
        {
            object marker;
            if (rebuilt.TryGetValue(form, out marker)) return;
            rebuilt.Add(form, new object());
            if (!(form is NextDialog) && !(form is GameLibraryDialog))
                BuildDialogShell(form);
            foreach (var control in NextUi.Descendants(form))
            {
                control.MinimumSize = new Size(0, control.MinimumSize.Height);
                if (control is Label || control is TextBoxBase || control is Panel)
                    control.MaximumSize = new Size(0, control.MaximumSize.Height);
            }
            RefreshPresentation(form);
        }

        internal static void Watch(Control control)
        {
            object marker;
            if (watched.TryGetValue(control, out marker)) return;
            watched.Add(control, new object());
            control.ControlAdded += delegate(object sender, ControlEventArgs e)
            {
                if (!(e.Control is ListView || e.Control is TabControl || e.Control is TableLayoutPanel)) return;
                var form = control.FindForm();
                if (!(form is FishBowlDialog) || !form.IsHandleCreated || form.IsDisposed) return;
                var state = pending.GetValue(form, delegate { return new PendingPresentation(); });
                if (state.Pending) return;
                state.Pending = true;
                form.BeginInvoke((Action)delegate
                {
                    try { if (!form.IsDisposed && !form.Disposing) RefreshPresentation(form); }
                    finally { state.Pending = false; }
                });
            };
        }

        internal static void RefreshPresentation(Control root)
        {
            RebuildTabs(root);
            foreach (var container in NextUi.Descendants(root).Where(control =>
                control.GetType() == typeof(Panel) && control.Controls.OfType<Label>().Any() &&
                    control.Controls.Cast<Control>().Any(child => child is ComboBox || child is TextBoxBase) &&
                    control.Controls.Cast<Control>().All(child => child.Dock == DockStyle.None) ||
                control is TableLayoutPanel && ((TableLayoutPanel)control).ColumnCount == 1 &&
                    control.Controls.OfType<Label>().Any() && control.Controls.OfType<ModernTabDeck>().Any()).ToArray())
            {
                var children = container.Controls.Cast<Control>().ToArray();
                var body = new ModernDialogBody(children) { AutoScroll = true, Padding = new Padding(16) };
                Replace(container, body);
                foreach (var child in children) body.Controls.Add(child);
                container.Dispose();
            }
            foreach (var table in NextUi.Descendants(root).OfType<TableLayoutPanel>().ToArray())
            {
                if (table is ModernFieldsPanel || !table.Controls.OfType<Label>().Any() ||
                    !table.Controls.Cast<Control>().Any(control => control is TextBoxBase || control is ComboBox || control is NumericUpDown)) continue;
                ModernFieldLayout existing;
                if (fieldLayouts.TryGetValue(table, out existing)) continue;
                var layout = new ModernFieldLayout(table);
                fieldLayouts.Add(table, layout);
                table.AutoSize = false;
                table.Dock = DockStyle.Top;
                table.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
                table.Layout += delegate { layout.Arrange(); };
                layout.Arrange();
            }
            foreach (var list in NextUi.Descendants(root).OfType<ListView>().ToArray())
                Catalog(list, "items", false, null);
        }

        internal static void RebuildTabs(Control root)
        {
            foreach (var tabs in NextUi.Descendants(root).OfType<TabControl>().ToArray())
            {
                object marker;
                var workspace = tabs as FishBowlTabs;
                if (workspace != null && workspace.HideWorkspaceHeaders && !decks.TryGetValue(tabs, out marker)) continue;
                if (!decks.TryGetValue(tabs, out marker))
                {
                    decks.Add(tabs, new object());
                    var deck = new ModernTabDeck(tabs);
                    Replace(tabs, deck);
                    deck.Attach();
                }
                foreach (TabPage page in tabs.TabPages)
                {
                    object pageMarker;
                    if (pages.TryGetValue(page, out pageMarker)) continue;
                    pages.Add(page, new object());
                    var controls = page.Controls.Cast<Control>().ToArray();
                    var content = new ModernDialogBody(controls) { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20), Name = "Modern section content" };
                    foreach (var control in controls) { page.Controls.Remove(control); content.Controls.Add(control); }
                    page.Controls.Add(content);
                }
            }
        }

        internal static void Replace(Control original, Control replacement)
        {
            var parent = original.Parent;
            if (parent == null) return;
            int index = parent.Controls.GetChildIndex(original);
            var modernBody = parent as ModernDialogBody;
            if (modernBody != null) modernBody.ReplaceChild(original, replacement);
            var table = parent as TableLayoutPanel;
            int column = table == null ? 0 : table.GetColumn(original), row = table == null ? 0 : table.GetRow(original);
            int columns = table == null ? 1 : table.GetColumnSpan(original), rows = table == null ? 1 : table.GetRowSpan(original);
            replacement.Bounds = original.Bounds;
            replacement.Dock = original.Dock;
            replacement.Anchor = original.Anchor;
            replacement.Margin = original.Margin;
            parent.SuspendLayout();
            parent.Controls.Remove(original);
            if (table == null) parent.Controls.Add(replacement);
            else
            {
                table.Controls.Add(replacement, column, row);
                table.SetColumnSpan(replacement, columns);
                table.SetRowSpan(replacement, rows);
            }
            parent.Controls.SetChildIndex(replacement, index);
            parent.ResumeLayout(true);
        }

        internal static BufferedPromptTable DialogFrame(string name)
        {
            var frame = new BufferedPromptTable
            {
                Name = name, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(24), BackColor = FishBowlPalette.ThemeBottom
            };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            frame.Layout += delegate
            {
                foreach (var heading in frame.Controls.OfType<Label>().Where(label => frame.GetRow(label) == 0))
                {
                    int height = Math.Max(52, heading.Font.Height + 16);
                    heading.MaximumSize = new Size(Math.Max(1, frame.ClientSize.Width - frame.Padding.Horizontal), 0);
                    heading.MinimumSize = new Size(0, height);
                    if (heading.Height != height) heading.Height = height;
                    int rowHeight = height + heading.Margin.Vertical;
                    if (frame.RowStyles[0].SizeType != SizeType.Absolute || frame.RowStyles[0].Height != rowHeight)
                        frame.RowStyles[0] = new RowStyle(SizeType.Absolute, rowHeight);
                }
            };
            return frame;
        }

        internal static Label Heading(string title)
        {
            return new Label
            {
                Text = title, AccessibleName = title, Dock = DockStyle.Top, AutoSize = false, AutoEllipsis = true, Height = 52,
                Font = new Font("Segoe UI", 23f, FontStyle.Bold), BackColor = Color.Transparent,
                ForeColor = FishBowlPalette.ThemeInk, Margin = new Padding(0, 0, 0, 20)
            };
        }

        private static void BuildDialogShell(Form form)
        {
            var controls = form.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            int originalHeight = form.ClientSize.Height;
            Rectangle area = TextFit.WorkingAreaOverride ?? Screen.FromControl(form.Owner ?? form).WorkingArea;
            form.ClientSize = new Size(Math.Min(Math.Max(640, form.ClientSize.Width), Math.Max(300, area.Width - 48)),
                Math.Min(Math.Max(360, form.ClientSize.Height), Math.Max(240, area.Height - 80)));
            var root = DialogFrame(StartupPromptLayout.IsPrompt(form) ? "StartupPromptRoot" : "ModernDialogRoot");
            var existingHeading = controls.OfType<Label>().Where(label => label.Font.Bold && label.Font.Size >= 12 && label.Text.Length < 150).OrderBy(label => label.Top).FirstOrDefault();
            var heading = existingHeading ?? Heading(form.Text);
            SetFont(heading, 23f, FontStyle.Bold);
            heading.Dock = DockStyle.Top;
            heading.AutoSize = false;
            heading.AutoEllipsis = true;
            heading.Height = Math.Max(52, heading.Font.Height + 10);
            heading.Margin = new Padding(0, 0, 0, 20);
            heading.BackColor = Color.Transparent;
            if (existingHeading == null) form.TextChanged += delegate { heading.Text = form.Text; };
            var body = new ModernDialogBody(controls)
            {
                Name = StartupPromptLayout.IsPrompt(form) ? "StartupPromptBody" : "ModernDialogBody",
                Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, Padding = new Padding(20)
            };
            var actions = new ReferenceToolbar
            {
                Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Padding = new Padding(0, 16, 0, 0), Margin = Padding.Empty
            };
            var actionNames = new HashSet<string>(new[] { "OK", "Save", "Cancel", "Close", "Apply", "Revert", "Finish", "Continue", "Get started" }, StringComparer.OrdinalIgnoreCase);
            var footer = controls.OfType<Button>().Where(button => button.DialogResult != DialogResult.None ||
                actionNames.Contains(button.Text.Trim()) && button.Bottom >= originalHeight - 125 ||
                button.Bottom >= originalHeight - 80).OrderBy(button => button.Left).ToArray();
            form.SuspendLayout();
            foreach (var control in controls)
            {
                form.Controls.Remove(control);
                if (control == heading) continue;
                if (footer.Contains(control))
                {
                    control.Dock = DockStyle.None;
                    control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    control.AutoSize = true;
                    control.Margin = new Padding(0, 0, 10, 0);
                    actions.Controls.Add(control);
                }
                else body.Controls.Add(control);
            }
            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(actions, 0, 2);
            if (form.AcceptButton == null) form.AcceptButton = footer.FirstOrDefault();
            if (form.CancelButton == null) form.CancelButton = footer.FirstOrDefault();
            root.Layout += delegate { heading.MaximumSize = new Size(Math.Max(1, root.ClientSize.Width - root.Padding.Horizontal), 0); };
            form.AutoScroll = false;
            form.AutoScrollMinSize = Size.Empty;
            form.Controls.Add(root);
            form.ResumeLayout(true);
            form.Shown += delegate { body.PerformLayout(); root.PerformLayout(); };
        }
    }

    internal sealed class ModernWorkspaceHeader : Panel
    {
        private readonly Label heading;
        private readonly Control location;
        private readonly Button primary;
        private readonly ReferenceToolbar actions;
        private readonly Panel backend;
        private readonly ReferenceButton more;
        private readonly ContextMenuStrip menu;
        private readonly Button[] secondaryActions;
        private bool arranging;

        internal ModernWorkspaceHeader(string title, Control location, Button primary, IEnumerable<Button> visible, IEnumerable<Button> secondary)
        {
            Dock = DockStyle.Top;
            Padding = new Padding(20, 18, 20, 14);
            AccessibleName = title + " workspace header";
            var address = location as TextBox;
            if (address != null)
            {
                var surface = new ModernInputSurface { AccessibleName = address.AccessibleName };
                surface.Controls.Add(address);
                surface.Layout += delegate
                {
                    address.BorderStyle = BorderStyle.None;
                    address.SetBounds(16, Math.Max(0, (surface.Height - address.Height) / 2), Math.Max(1, surface.Width - 32), address.Height);
                };
                this.location = surface;
            }
            else this.location = location;
            this.primary = primary;
            secondaryActions = secondary.ToArray();
            heading = ModernUi.Heading(title);
            heading.Dock = DockStyle.None;
            actions = new ReferenceToolbar { Name = "FishBowlToolbar", Padding = Padding.Empty, AutoSize = false, WrapContents = true };
            backend = new Panel { Visible = false };
            menu = new ContextMenuStrip { Renderer = new FishBowlMenuRenderer() };
            NavigationController.Register(menu);
            more = new ReferenceButton("More") { Size = new Size(100, 38), AccessibleName = title + " actions" };
            more.ContextMenuStrip = menu;
            more.Click += delegate { menu.Show(more, new Point(0, more.Height)); };
            foreach (var button in visible)
            {
                button.Dock = DockStyle.None;
                button.AutoSize = true;
                button.Margin = new Padding(0, 0, 10, 0);
                actions.Controls.Add(button);
            }
            foreach (var button in secondaryActions)
            {
                var source = button;
                backend.Controls.Add(source);
                var action = new ToolStripMenuItem(source.Text);
                if (source.ContextMenuStrip != null) action.DropDown = source.ContextMenuStrip;
                else action.Click += delegate
                {
                    if (source.Enabled) typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { EventArgs.Empty });
                };
                menu.Opening += delegate { action.Text = source.ContextMenuStrip == null ? source.Text : "Session settings"; action.Enabled = source.Enabled; };
                menu.Items.Add(action);
                source.EnabledChanged += delegate { more.Enabled = secondaryActions.Any(item => item.Enabled); };
            }
            actions.Controls.Add(more);
            Controls.Add(heading);
            Controls.Add(this.location);
            Controls.Add(actions);
            Controls.Add(backend);
            if (primary != null) Controls.Add(primary);
            more.Enabled = secondaryActions.Any(item => item.Enabled);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (arranging || heading == null) return;
            arranging = true;
            try
            {
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
                int headingHeight = Math.Max(52, heading.Font.Height + 10);
                heading.SetBounds(Padding.Left, Padding.Top, width, headingHeight);
                int y = heading.Bottom + 12;
                int height = Math.Max(40, location.Font.Height + 16);
                int primaryWidth = primary == null ? 0 : Math.Max(90, TextRenderer.MeasureText(primary.Text, primary.Font).Width + 38);
                location.Dock = DockStyle.None;
                int locationHeight = location is ModernInputSurface ? height : location.Height;
                location.SetBounds(Padding.Left, y + (height - locationHeight) / 2, Math.Max(1, width - primaryWidth - (primary == null ? 0 : 12)), locationHeight);
                if (primary != null)
                {
                    primary.Dock = DockStyle.None;
                    primary.SetBounds(location.Right + 12, y, primaryWidth, height);
                }
                y += height + 12;
                actions.SetBounds(Padding.Left, y, width, Math.Max(42, actions.GetPreferredSize(new Size(width, 0)).Height));
                Height = actions.Bottom + Padding.Bottom;
            }
            finally { arranging = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) menu.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ModernInputSurface : Panel
    {
        internal ModernInputSurface() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? FishBowlPalette.ThemeBottom : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 2 || Height < 2) return;
            using (var outline = ReferenceButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (var fill = new SolidBrush(ColorHarmony.Input)) e.Graphics.FillPath(fill, outline);
        }
    }

    internal sealed class ModernCatalog : Panel
    {
        private const int PageSize = 48;
        private readonly ListView source;
        private readonly string kind;
        private readonly bool forceGrid;
        private readonly Action<ListViewItem> open;
        private readonly FlowLayoutPanel cards;
        private readonly Label summary;
        private readonly ReferenceButton previous, next;
        private readonly Timer refresh;
        private readonly List<ModernCatalogCard> visibleCards = new List<ModernCatalogCard>();
        private string signature;
        private int page, columns = 1, selectionAnchor;
        private bool attached, arranging;
        private bool synchronizingVisibility;
        private static readonly MethodInfo visibleState = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);

        internal ModernCatalog(ListView source, string kind, bool grid, Action<ListViewItem> open)
        {
            this.source = source;
            this.kind = kind;
            forceGrid = grid;
            this.open = open;
            AccessibleName = "Modern " + kind + " catalog";
            cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8), WrapContents = true };
            var footer = new ReferenceToolbar { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), WrapContents = true };
            summary = new Label { AutoSize = true, Margin = new Padding(0, 10, 20, 0), ForeColor = FishBowlPalette.ThemeSubtle };
            previous = new ReferenceButton("Previous") { Size = new Size(110, 36), AccessibleName = "Previous catalog page" };
            next = new ReferenceButton("Next") { Size = new Size(110, 36), AccessibleName = "Next catalog page" };
            previous.Click += delegate { page = Math.Max(0, page - 1); signature = null; RefreshCards(); };
            next.Click += delegate { page++; signature = null; RefreshCards(); };
            footer.Controls.Add(summary);
            footer.Controls.Add(previous);
            footer.Controls.Add(next);
            if (HasHandler(source, "ColumnClick"))
            {
                var sort = new ReferenceButton("Sort by") { Size = new Size(110, 36) };
                var choices = new ContextMenuStrip();
                choices.Opening += delegate
                {
                    choices.Items.Clear();
                    for (int i = 0; i < source.Columns.Count; i++)
                    {
                        int column = i;
                        choices.Items.Add(source.Columns[i].Text, null, delegate
                        {
                            typeof(ListView).GetMethod("OnColumnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { new ColumnClickEventArgs(column) });
                            signature = null; RefreshCards();
                        });
                    }
                };
                sort.Click += delegate { choices.Show(sort, new Point(0, sort.Height)); };
                sort.Disposed += delegate { choices.Dispose(); };
                sort.ContextMenuStrip = choices;
                footer.Controls.Add(sort);
            }
            Controls.Add(cards);
            Controls.Add(footer);
            refresh = new Timer { Interval = 250 };
            refresh.Tick += delegate { RefreshCards(); };
            VisibleChanged += delegate { if (Visible) { RefreshCards(); refresh.Start(); } else refresh.Stop(); };
            SizeChanged += delegate { ArrangeCards(); };
            cards.SizeChanged += delegate { ArrangeCards(); };
        }

        internal void Attach()
        {
            source.Dock = DockStyle.None;
            source.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            source.TabStop = false;
            Controls.Add(source);
            var handle = source.Handle;
            source.SetBounds(-30000, -30000, Math.Max(1, Width), Math.Max(1, Height));
            source.VisibleChanged += delegate
            {
                if (synchronizingVisibility) return;
                synchronizingVisibility = true;
                try
                {
                    bool locallyVisible = visibleState == null ? source.Visible || source.Parent != null && !source.Parent.Visible : (bool)visibleState.Invoke(source, new object[] { 2 });
                    Visible = locallyVisible;
                }
                finally { synchronizingVisibility = false; }
            };
            source.GotFocus += delegate
            {
                var selected = source.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
                if (selected != null) page = selected.Index / PageSize;
                RefreshCards();
                var card = visibleCards.FirstOrDefault(item => selected == null || item.Item == selected);
                if (card != null) card.Focus();
            };
            source.ItemSelectionChanged += delegate { UpdateSelection(); };
            AllowDrop = cards.AllowDrop = source.AllowDrop;
            DragEnter += ForwardDragEnter;
            DragDrop += ForwardDragDrop;
            cards.DragEnter += ForwardDragEnter;
            cards.DragDrop += ForwardDragDrop;
            source.BackgroundImageChanged += delegate
            {
                Image previous = cards.BackgroundImage;
                cards.BackgroundImage = source.BackgroundImage == null ? null : new Bitmap(source.BackgroundImage);
                cards.BackgroundImageLayout = ImageLayout.Stretch;
                if (previous != null) previous.Dispose();
            };
            attached = true;
            if (visibleState != null) Visible = (bool)visibleState.Invoke(source, new object[] { 2 });
            RefreshCards();
            if (Visible) refresh.Start();
        }

        internal void RefreshCards()
        {
            if (!attached || IsDisposed) return;
            page = Math.Min(page, Math.Max(0, (source.Items.Count - 1) / PageSize));
            var rows = source.Items.Cast<ListViewItem>().Skip(page * PageSize).Take(PageSize).ToArray();
            string state = source.Items.Count + "|" + source.View + "|" + source.Font.Name + "|" + source.Font.Size + "|" + page + "|" + FishBowlPalette.ThemeBottom.ToArgb() + "|" +
                FishBowlPalette.ThemeSurface.ToArgb() + "|" + string.Join(",", rows.Select(row => RuntimeHelpers.GetHashCode(row).ToString()));
            if (state == signature)
            {
                foreach (var card in visibleCards) { card.RefreshContent(source); card.RefreshArtwork(source); }
                UpdateSelection(); return;
            }
            signature = state;
            cards.SuspendLayout();
            foreach (var card in visibleCards) card.Dispose();
            visibleCards.Clear();
            foreach (var row in rows)
            {
                var item = row;
                var card = new ModernCatalogCard(item, source, delegate { Select(item, false); }, delegate { Activate(item); });
                card.KeyDown += delegate(object sender, KeyEventArgs e) { Navigate(item, e); };
                foreach (var target in NextUi.Descendants(card).Concat(new Control[] { card }))
                {
                    target.AllowDrop = source.AllowDrop;
                    target.DragEnter += ForwardDragEnter;
                    target.DragDrop += ForwardDragDrop;
                }
                cards.Controls.Add(card);
                visibleCards.Add(card);
            }
            BackColor = cards.BackColor = FishBowlPalette.ThemeBottom;
            summary.Text = source.Items.Count == 0 ? "No " + kind + " in this view" : source.Items.Count + " " + kind + (source.Items.Count > PageSize ? " · " + (page * PageSize + 1) + "–" + Math.Min((page + 1) * PageSize, source.Items.Count) : "");
            previous.Visible = next.Visible = source.Items.Count > PageSize;
            previous.Enabled = page > 0;
            next.Enabled = (page + 1) * PageSize < source.Items.Count;
            cards.ResumeLayout(true);
            ArrangeCards();
            UpdateSelection();
        }

        private void ArrangeCards()
        {
            if (arranging || !attached || ClientSize.Width < 1) return;
            arranging = true;
            try
            {
                int available = Math.Max(1, cards.ClientSize.Width - cards.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                bool grid = forceGrid || source.View != View.Details;
                int minimum = Math.Max(250, Font.Height * 17);
                columns = grid ? Math.Max(1, available / minimum) : 1;
                int width = Math.Max(1, available / columns - 16);
                foreach (var card in visibleCards)
                {
                    card.Margin = new Padding(8);
                    card.Size = new Size(width, grid ? Math.Max(220, card.Font.Height * 12) : Math.Max(190, card.Font.Height * 10));
                    card.Arrange(grid);
                }
                source.SetBounds(-30000, -30000, Math.Max(1, Width), Math.Max(1, Height));
            }
            finally { arranging = false; }
        }

        private void Select(ListViewItem item, bool focus)
        {
            if (!source.Enabled) return;
            bool toggle = source.MultiSelect && (ModifierKeys & Keys.Control) != 0;
            bool range = source.MultiSelect && (ModifierKeys & Keys.Shift) != 0;
            if (!toggle && !range)
                foreach (var selected in source.SelectedItems.Cast<ListViewItem>().ToArray()) selected.Selected = false;
            if (range)
                for (int i = Math.Min(selectionAnchor, item.Index); i <= Math.Max(selectionAnchor, item.Index); i++) source.Items[i].Selected = true;
            else { item.Selected = toggle ? !item.Selected : true; selectionAnchor = item.Index; }
            item.Focused = true;
            UpdateSelection();
            if (focus)
            {
                var card = visibleCards.FirstOrDefault(candidate => candidate.Item == item);
                if (card != null) { cards.ScrollControlIntoView(card); card.Focus(); }
            }
        }

        private void Activate(ListViewItem item)
        {
            if (!source.Enabled) return;
            foreach (var selected in source.SelectedItems.Cast<ListViewItem>().Where(row => row != item).ToArray()) selected.Selected = false;
            item.Selected = true;
            item.Focused = true;
            UpdateSelection();
            if (open != null) open(item);
            else if (HasHandler(source, "DoubleClick")) typeof(Control).GetMethod("OnDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { EventArgs.Empty });
            else FishBowlMessageBox.Show(FindForm(), string.Join("\n\n", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select((cell, index) =>
                (index < source.Columns.Count ? source.Columns[index].Text + "\n" : "") + cell.Text)), item.Text);
        }

        internal static bool HasHandler(Control control, string eventName)
        {
            var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control, null);
            for (Type type = control.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.Name.IndexOf(eventName, StringComparison.OrdinalIgnoreCase) >= 0 && field.GetValue(control) != null) return true;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (field.FieldType == typeof(object) && field.Name.IndexOf(eventName, StringComparison.OrdinalIgnoreCase) >= 0 && events[field.GetValue(null)] != null) return true;
            }
            return false;
        }

        private void Navigate(ListViewItem item, KeyEventArgs e)
        {
            int offset = e.KeyCode == Keys.Left ? -1 : e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Up ? -columns : e.KeyCode == Keys.Down ? columns :
                e.KeyCode == Keys.PageUp ? -PageSize : e.KeyCode == Keys.PageDown ? PageSize : e.KeyCode == Keys.Home ? -item.Index : e.KeyCode == Keys.End ? source.Items.Count - item.Index - 1 : 0;
            if (offset != 0)
            {
                int index = Math.Max(0, Math.Min(source.Items.Count - 1, item.Index + offset));
                page = index / PageSize;
                RefreshCards();
                Select(source.Items[index], true);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (e.KeyCode == Keys.Space && source.CheckBoxes) { item.Checked = !item.Checked; UpdateSelection(); }
                else Activate(item);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.A && source.MultiSelect)
            {
                foreach (ListViewItem row in source.Items) row.Selected = true;
                UpdateSelection();
                e.Handled = e.SuppressKeyPress = true;
            }
            else typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { e });
        }

        private void UpdateSelection()
        {
            foreach (var card in visibleCards) { card.Enabled = source.Enabled; card.Selected = card.Item.Selected; card.SynchronizeCheck(); card.Invalidate(); }
        }

        private void ForwardDragEnter(object sender, DragEventArgs e)
        {
            if (source.Enabled) typeof(Control).GetMethod("OnDragEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { e });
        }

        private void ForwardDragDrop(object sender, DragEventArgs e)
        {
            if (source.Enabled) typeof(Control).GetMethod("OnDragDrop", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { e });
        }

        internal ListViewItem[] VisibleRows()
        {
            return visibleCards.Where(card => card.Bounds.IntersectsWith(cards.ClientRectangle)).Select(card => card.Item).ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refresh.Stop(); refresh.Dispose();
                var backdrop = cards.BackgroundImage; cards.BackgroundImage = null;
                if (backdrop != null) backdrop.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class ModernEmptyPanel : Panel
    {
        private Rectangle card;
        private bool arranging;
        internal ModernEmptyPanel() { DoubleBuffered = true; }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (arranging) return;
            arranging = true;
            try
            {
                int width = Math.Max(1, Math.Min(760, ClientSize.Width - 48));
                int height = Math.Max(230, Math.Min(340, ClientSize.Height - 48));
                card = new Rectangle(Math.Max(0, (Width - width) / 2), Math.Max(0, (Height - height) / 2), width, height);
                var label = Controls.OfType<Label>().FirstOrDefault();
                var actions = Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
                if (label != null)
                {
                    label.Dock = DockStyle.None;
                    label.Padding = Padding.Empty;
                    label.BackColor = ColorHarmony.Card;
                    label.Font = new Font("Segoe UI", 11f);
                    label.SetBounds(card.Left + 28, card.Top + 88, Math.Max(1, card.Width - 56), 72);
                }
                if (actions != null)
                {
                    actions.Dock = DockStyle.None;
                    actions.AutoSize = false;
                    actions.BackColor = ColorHarmony.Card;
                    actions.Padding = Padding.Empty;
                    actions.SetBounds(card.Left + 24, card.Top + 168, Math.Max(1, card.Width - 48), Math.Max(48, card.Height - 180));
                }
            }
            finally { arranging = false; }
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(FishBowlPalette.ThemeBottom);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (card.Width < 1 || card.Height < 1) return;
            using (var outline = ReferenceButton.Round(card, 18))
            using (var fill = new SolidBrush(ColorHarmony.Card)) e.Graphics.FillPath(fill, outline);
            var label = Controls.OfType<Label>().FirstOrDefault();
            string title = label != null && label.Text.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0 ? "No matching games" : "Your library starts here";
            using (var font = new Font("Segoe UI", 23f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, title, font, new Rectangle(card.Left + 28, card.Top + 28, Math.Max(1, card.Width - 56), 52),
                    FishBowlPalette.ThemeInk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ModernHomeCard : ReferenceHomeCard
    {
        private Image icon;
        private string iconKey;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            string title = AccessibleName ?? "";
            string kind = title.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 ? "save" :
                title.IndexOf("pinned", StringComparison.OrdinalIgnoreCase) >= 0 ? "star" :
                title.IndexOf("emulator", StringComparison.OrdinalIgnoreCase) >= 0 ? "controller" :
                title.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0 ? "folder" : "info";
            string key = kind + "|" + FishBowlPalette.ThemeInk.ToArgb() + "|" + ReferencePalette.Violet.ToArgb() + "|" + CosmeticRuntime.Current.IconStyle;
            if (iconKey != key)
            {
                var previous = icon;
                icon = FishBowlVisuals.Icon(kind, 26, FishBowlPalette.ThemeInk, ReferencePalette.Violet);
                iconKey = key;
                if (previous != null) previous.Dispose();
            }
            var badge = new Rectangle(Padding.Left, Padding.Top, 46, 46);
            using (var outline = ReferenceButton.Round(badge, 12))
            using (var fill = new SolidBrush(ColorHarmony.Mix(ColorHarmony.Card, ReferencePalette.Violet, 18))) e.Graphics.FillPath(fill, outline);
            if (icon != null) e.Graphics.DrawImage(icon, badge.Left + 10, badge.Top + 10, 26, 26);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && icon != null) { icon.Dispose(); icon = null; }
            base.Dispose(disposing);
        }
    }

    internal sealed class ModernBlankState : Label
    {
        internal string Heading;
        internal ModernBlankState(string heading)
        {
            Heading = heading;
            AutoSize = false;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(FishBowlPalette.ThemeBottom);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int width = Math.Max(1, Math.Min(760, ClientSize.Width - 48));
            int height = Math.Max(180, Math.Min(300, ClientSize.Height - 48));
            var card = new Rectangle(Math.Max(0, (Width - width) / 2), Math.Max(0, (Height - height) / 2), width, height);
            using (var outline = ReferenceButton.Round(card, 18))
            using (var fill = new SolidBrush(ColorHarmony.Card)) e.Graphics.FillPath(fill, outline);
            using (var font = new Font("Segoe UI", 24f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Heading, font, new Rectangle(card.Left + 30, card.Top + 30, Math.Max(1, card.Width - 60), 60),
                    FishBowlPalette.ThemeInk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var font = new Font("Segoe UI", 11f))
                TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(card.Left + 30, card.Top + 104, Math.Max(1, card.Width - 60), Math.Max(1, card.Height - 124)),
                    FishBowlPalette.ThemeSubtle, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class ModernCatalogCard : ReferenceHomeCard
    {
        internal readonly ListViewItem Item;
        internal bool Selected;
        private readonly Label title, detail;
        private readonly PictureBox image;
        private readonly ReferenceButton open;
        private readonly CheckBox included;
        private int artworkIndex = -2, artworkCount = -1;
        private ImageList artworkSource;

        internal ModernCatalogCard(ListViewItem item, ListView source, Action select, Action activate)
        {
            Item = item;
            Font = source.Font;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleName = item.Text;
            BackColor = ColorHarmony.Card;
            float scale = Math.Max(.75f, NextUi.TextPercent / 100f);
            title = new Label { Text = item.Text, AutoEllipsis = true, Font = source.Font, ForeColor = FishBowlPalette.ThemeInk, BackColor = Color.Transparent };
            detail = new Label { Text = string.Join("\n", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Skip(1).Select((cell, index) =>
                (index + 1 < source.Columns.Count ? source.Columns[index + 1].Text + " · " : "") + cell.Text)),
                AutoEllipsis = true, Font = source.Font, ForeColor = FishBowlPalette.ThemeSubtle, BackColor = Color.Transparent };
            ModernUi.SetFont(title, 13f * scale, FontStyle.Bold);
            ModernUi.SetFont(detail, 10f * scale, FontStyle.Regular);
            image = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            RefreshArtwork(source);
            string action = item.Tag is GameEntry ? "Play" : source.Name == "ModernEmulatorSource" ? "Open" : ModernCatalog.HasHandler(source, "DoubleClick") ? "Open" : "Details";
            open = new ReferenceButton(action) { Primary = true, Font = source.Font, BackColor = ReferencePalette.Violet, ForeColor = Color.White, AccessibleName = action + " " + item.Text };
            ModernUi.SetFont(open, 10f * scale, FontStyle.Bold);
            open.Click += delegate { activate(); };
            ContextMenuStrip = source.ContextMenuStrip;
            foreach (var control in new Control[] { this, title, detail, image })
            {
                control.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button != MouseButtons.Right || !item.Selected) select(); Focus(); };
                control.DoubleClick += delegate { activate(); };
            }
            Controls.AddRange(new Control[] { image, title, detail, open });
            if (source.CheckBoxes)
            {
                included = new CheckBox { Checked = item.Checked, Text = "Include", AutoSize = true, FlatStyle = FlatStyle.Flat,
                    AccessibleName = "Include " + item.Text, ForeColor = FishBowlPalette.ThemeInk, BackColor = Color.Transparent };
                included.CheckedChanged += delegate { item.Checked = included.Checked; };
                Controls.Add(included);
            }
        }

        internal void Arrange(bool grid)
        {
            float scale = Math.Max(.75f, NextUi.TextPercent / 100f);
            int inset = Math.Min((int)(18 * scale), Math.Max(10, Width / 12)), artwork = (int)((grid ? 60 : 64) * scale);
            image.SetBounds(inset, inset, artwork, artwork);
            title.SetBounds(inset + artwork + 14, inset, Math.Max(1, Width - inset * 2 - artwork - 14), grid ? Math.Max(34, title.Font.Height * 2) : title.Font.Height + 8);
            int top = grid ? Math.Max(image.Bottom, title.Bottom) + 14 : title.Bottom + 10;
            int left = grid ? inset : inset + artwork + 14;
            int buttonHeight = Math.Max(34, open.Font.Height + 14), buttonWidth = Math.Min(Width - inset * 2, Math.Max(110, TextRenderer.MeasureText(open.Text, open.Font).Width + 36));
            open.SetBounds(inset, Height - buttonHeight - inset, buttonWidth, buttonHeight);
            if (!grid) open.Left = Math.Max(inset, Width - buttonWidth - inset);
            detail.SetBounds(left, top, Math.Max(1, Width - left - inset), Math.Max(1, open.Top - top - 12));
            if (included != null) included.Location = new Point(Math.Max(inset, Width - included.PreferredSize.Width - inset), inset);
        }

        internal void SynchronizeCheck()
        {
            if (included != null && included.Checked != Item.Checked) included.Checked = Item.Checked;
        }

        internal void RefreshContent(ListView source)
        {
            if (title.Text != Item.Text)
            {
                title.Text = Item.Text; AccessibleName = Item.Text;
                open.AccessibleName = open.Text + " " + Item.Text;
                artworkIndex = -2;
            }
            string text = string.Join("\n", Item.SubItems.Cast<ListViewItem.ListViewSubItem>().Skip(1).Select((cell, index) =>
                (index + 1 < source.Columns.Count ? source.Columns[index + 1].Text + " · " : "") + cell.Text));
            if (detail.Text != text) detail.Text = text;
        }

        internal void RefreshArtwork(ListView source)
        {
            var images = source.LargeImageList ?? source.SmallImageList;
            int count = images == null ? 0 : images.Images.Count;
            if (artworkSource == images && artworkIndex == Item.ImageIndex && artworkCount == count) return;
            artworkSource = images; artworkIndex = Item.ImageIndex; artworkCount = count;
            Image previous = image.Image;
            image.Image = null;
            if (images != null)
            {
                Image artwork = Item.ImageKey.Length > 0 && images.Images.ContainsKey(Item.ImageKey) ? images.Images[Item.ImageKey] :
                    Item.ImageIndex >= 0 && Item.ImageIndex < count ? images.Images[Item.ImageIndex] : null;
                if (artwork != null && (!(Item.Tag is GameEntry) || Item.ImageIndex > 0)) image.Image = new Bitmap(artwork);
            }
            if (image.Image == null) image.Image = GameLibraryDialog.MakeThumbnail(null, Item.Text.TrimStart('★', ' '), 96);
            if (previous != null) previous.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Selected && !Focused) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var outline = ReferenceButton.Round(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 12))
            using (var pen = new Pen(ReferencePalette.Violet, 2)) e.Graphics.DrawPath(pen, outline);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image.Image != null) { var artwork = image.Image; image.Image = null; artwork.Dispose(); }
            base.Dispose(disposing);
        }
    }

    public sealed class ModernFieldsPanel : TableLayoutPanel
    {
        private ModernFieldLayout layout;
        public ModernFieldsPanel() { DoubleBuffered = true; Padding = new Padding(4); layout = new ModernFieldLayout(this); }
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (layout == null || Controls.Count == 0) return base.GetPreferredSize(proposedSize);
            return new Size(proposedSize.Width > 0 ? proposedSize.Width : Width, Math.Max(1, layout.PreferredHeight));
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (layout != null) layout.Arrange();
        }
    }

    internal sealed class ModernFieldLayout
    {
        private readonly TableLayoutPanel table;
        private bool arranging;
        internal int PreferredHeight;
        internal ModernFieldLayout(TableLayoutPanel table) { this.table = table; }

        internal void Arrange()
        {
            if (arranging || table.Width < 2) return;
            arranging = true;
            try
            {
                int width = Math.Max(1, table.ClientSize.Width - table.Padding.Horizontal);
                int y = table.Padding.Top;
                foreach (var row in table.Controls.Cast<Control>().Where(ModernUi.LocallyVisible).GroupBy(table.GetRow).OrderBy(group => group.Key))
                {
                    var controls = row.OrderBy(table.GetColumn).ToArray();
                    var label = controls.FirstOrDefault() as Label;
                    if (label != null)
                    {
                        int labelHeight = string.IsNullOrWhiteSpace(label.Text) ? 0 : TextRenderer.MeasureText(label.Text, label.Font,
                            new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                        label.AutoSize = false;
                        label.MinimumSize = Size.Empty;
                        label.MaximumSize = new Size(width, 0);
                        label.Dock = DockStyle.None;
                        label.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                        label.SetBounds(table.Padding.Left, y, width, labelHeight);
                        y += labelHeight + (labelHeight > 0 ? 7 : 0);
                        controls = controls.Skip(1).ToArray();
                    }
                    if (controls.Length > 0)
                    {
                        int gap = 10;
                        var buttons = controls.OfType<Button>().ToArray();
                        bool splitActions = buttons.Length > 0 && controls.Length > buttons.Length;
                        var fields = splitActions ? controls.Except(buttons).ToArray() : controls;
                        int controlWidth = Math.Max(1, (width - gap * (fields.Length - 1)) / Math.Max(1, fields.Length));
                        int height = fields.Length == 0 ? 0 : fields.Max(control => ModernDialogBody.PreferredHeight(control, controlWidth));
                        for (int i = 0; i < fields.Length; i++) Place(fields[i], table.Padding.Left + i * (controlWidth + gap), y, controlWidth, height);
                        y += height;
                        if (splitActions)
                        {
                            y += 10;
                            int x = table.Padding.Left, rowHeight = 0;
                            foreach (var button in buttons)
                            {
                                int buttonWidth = Math.Min(width, Math.Max(100, TextRenderer.MeasureText(button.Text, button.Font).Width + 36));
                                int buttonHeight = Math.Max(38, button.Font.Height + 16);
                                if (x > table.Padding.Left && x + buttonWidth > table.Padding.Left + width) { x = table.Padding.Left; y += rowHeight + gap; rowHeight = 0; }
                                Place(button, x, y, buttonWidth, buttonHeight);
                                x += buttonWidth + gap; rowHeight = Math.Max(rowHeight, buttonHeight);
                            }
                            y += rowHeight;
                        }
                    }
                    y += 20;
                }
                PreferredHeight = y + table.Padding.Bottom;
                if (table.Height != PreferredHeight && table.Dock != DockStyle.Fill) table.Height = PreferredHeight;
            }
            finally { arranging = false; }
        }

        private static void Place(Control control, int x, int y, int width, int height)
        {
            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.SetBounds(x, y, width, height);
        }
    }
    internal sealed class ModernDialogBody : ReferenceHomeCard
    {
        private readonly Control[] order;
        private readonly Dictionary<Control, Rectangle> original = new Dictionary<Control, Rectangle>();
        private bool arranging;

        internal ModernDialogBody(IEnumerable<Control> controls)
        {
            order = controls.OrderBy(control => control.Top).ThenBy(control => control.Left).ToArray();
            foreach (var control in order) original[control] = control.Bounds;
        }

        internal void ReplaceChild(Control source, Control replacement)
        {
            int index = Array.IndexOf(order, source);
            if (index < 0) return;
            order[index] = replacement;
            original[replacement] = original[source];
        }

        internal static int PreferredHeight(Control control, int width)
        {
            var label = control as Label;
            if (label != null) return Math.Max(label.Font.Height + 6, TextRenderer.MeasureText(label.Text, label.Font,
                new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 8);
            var text = control as TextBoxBase;
            if (text != null && text.Multiline) return Math.Max(140, Math.Min(300, text.Height));
            if (control is ListView || control is ListBox || control is TreeView || control is TabControl || control is SplitContainer || control is ModernTabDeck || control is ModernCatalog) return Math.Max(220, control.Height);
            if (control is Panel || control is TableLayoutPanel) return Math.Max(80, control.GetPreferredSize(new Size(width, 0)).Height);
            return Math.Max(38, control.Font.Height + 16);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (arranging || ClientSize.Width < 2) return;
            arranging = true;
            try
            {
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                int y = Padding.Top + AutoScrollPosition.Y;
                var remaining = order.Where(control => control.Parent == this && ModernUi.LocallyVisible(control)).ToList();
                foreach (var control in remaining.ToArray())
                {
                    if (!remaining.Contains(control)) continue;
                    var label = control as Label;
                    Control field = null;
                    if (label != null)
                    {
                        var position = original[control];
                        field = remaining.FirstOrDefault(candidate => candidate != control && !(candidate is Label) &&
                            original[candidate].Left >= position.Right - 16 && Math.Abs(original[candidate].Top - position.Top) < 24);
                    }
                    control.Dock = DockStyle.None;
                    control.MinimumSize = new Size(0, control.MinimumSize.Height);
                    control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    int height = PreferredHeight(control, width);
                    control.SetBounds(Padding.Left, y, width, height);
                    y += height + (field == null ? 18 : 6);
                    remaining.Remove(control);
                    if (field != null)
                    {
                        field.Dock = DockStyle.None;
                        field.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                        height = PreferredHeight(field, width);
                        field.SetBounds(Padding.Left, y, width, height);
                        y += height + 20;
                        remaining.Remove(field);
                    }
                }
                AutoScrollMinSize = new Size(0, Math.Max(0, y - AutoScrollPosition.Y + Padding.Bottom));
            }
            finally { arranging = false; }
        }
    }

    internal sealed class ModernTabDeck : Panel
    {
        private readonly TabControl tabs;
        private readonly ReferenceToolbar rail;
        private readonly Panel viewport;
        private readonly List<ReferenceButton> buttons = new List<ReferenceButton>();

        internal ModernTabDeck(TabControl tabs)
        {
            this.tabs = tabs;
            Name = "Modern section navigation";
            rail = new ReferenceToolbar { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4), Margin = Padding.Empty };
            viewport = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            Controls.Add(viewport);
            Controls.Add(rail);
        }

        internal void Attach()
        {
            var custom = tabs as FishBowlTabs;
            if (custom != null) custom.HideWorkspaceHeaders = true;
            viewport.Controls.Add(tabs);
            tabs.Dock = DockStyle.Fill;
            tabs.SelectedIndexChanged += delegate { UpdateNavigation(); };
            tabs.ControlAdded += delegate { BuildNavigation(); };
            tabs.ControlRemoved += delegate { BuildNavigation(); };
            BuildNavigation();
        }

        private void BuildNavigation()
        {
            var previous = buttons.ToArray();
            buttons.Clear();
            foreach (var button in previous) button.Dispose();
            foreach (TabPage page in tabs.TabPages)
            {
                var destination = page;
                var button = new ReferenceButton(page.Text) { AutoSize = false, Height = 40, Margin = new Padding(0, 0, 8, 0), AccessibleName = page.Text };
                button.Width = Math.Max(90, TextRenderer.MeasureText(page.Text, button.Font).Width + 36);
                button.Click += delegate { tabs.SelectedTab = destination; };
                buttons.Add(button);
                rail.Controls.Add(button);
            }
            UpdateNavigation();
        }

        private void UpdateNavigation()
        {
            BackColor = viewport.BackColor = FishBowlPalette.ThemeBottom;
            rail.BackColor = ColorHarmony.Card;
            for (int i = 0; i < buttons.Count; i++)
            {
                bool selected = tabs.SelectedIndex == i;
                buttons[i].BackColor = selected ? ReferencePalette.Violet : ColorHarmony.Card;
                buttons[i].ForeColor = selected ? Color.White : FishBowlPalette.ThemeInk;
                buttons[i].Invalidate();
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (tabs == null) return;
            if (!(tabs is FishBowlTabs) && tabs.TabCount > 0)
            {
                int header = tabs.GetTabRect(0).Height + 4;
                tabs.Dock = DockStyle.None;
                tabs.SetBounds(0, -header, viewport.ClientSize.Width, viewport.ClientSize.Height + header);
            }
            UpdateNavigation();
        }
    }
}
