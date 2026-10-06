using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class UiPolishTools
    {
        public static string[] ResolveHomeCards(LibraryData data)
        {
            ExperienceData.Ensure(data);
            var available = (data.Experience.HomeCards ?? new List<string>()).Distinct().ToList();
            var order = data.Theme.HomeCardOrder ?? new List<string>();
            var hidden = data.Theme.HiddenHomeCards ?? new List<string>();
            return order.Where(available.Contains).Concat(available).Distinct().Where(x => !hidden.Contains(x)).ToArray();
        }

        public static Color RestrainAccent(Color accent, Color surface)
        {
            return Color.FromArgb((accent.R + surface.R * 2) / 3, (accent.G + surface.G * 2) / 3, (accent.B + surface.B * 2) / 3);
        }

        public static Size CoverSize(string aspect)
        {
            int side = ControlDensityTools.Choose(48, 56, 64);
            return aspect == "Portrait" ? new Size(side, side * 3 / 2) : aspect == "Landscape" ? new Size(side * 3 / 2, side) : new Size(side, side);
        }

        public static Bitmap Cover(string path, string title, Size size)
        {
            Bitmap result = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(FishBowlPalette.ThemeSurface);
                bool loaded = false;
                try
                {
                    if (File.Exists(path)) using (Image source = Image.FromFile(path))
                    {
                        double scale = Math.Min((double)size.Width / source.Width, (double)size.Height / source.Height);
                        int width = Math.Max(1, (int)(source.Width * scale)), height = Math.Max(1, (int)(source.Height * scale));
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle((size.Width - width) / 2, (size.Height - height) / 2, width, height));
                        loaded = true;
                    }
                }
                catch (ArgumentException) { }
                catch (IOException) { }
                catch (OutOfMemoryException) { }
                if (!loaded)
                {
                    using (Font font = new Font("Segoe UI", 18, FontStyle.Bold))
                        FishBowlText.DrawText(graphics, string.IsNullOrWhiteSpace(title) ? "?" : title.Trim().Substring(0, 1).ToUpperInvariant(), font, new Rectangle(Point.Empty, size), FishBowlPalette.ThemeInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                using (Pen border = new Pen(FishBowlPalette.ThemeSubtle)) graphics.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1);
            }
            return result;
        }

        public static void ApplyAccessibility(LibraryData data, string preset)
        {
            if (preset == "Current") return;
            if (data.Theme == null) data.Theme = new ThemeSettings();
            if (data.Enhancements == null) data.Enhancements = new NextSettings();
            if (preset == "Larger text") data.Enhancements.TextPercent = 150;
            if (preset == "High contrast") { data.Theme.Name = "High Contrast"; data.Theme.SelectionContrast = "Strong"; data.Theme.RestrainedAccents = false; }
            if (preset == "Reduced motion") { data.Enhancements.ReducedMotion = true; data.Theme.EnableMotion = false; }
            if (preset == "Controller") { UserTools.Ensure(data).Controller = true; Immersion.Ensure(data).Roomier = true; data.Theme.ControlDensity = "Roomy"; data.Enhancements.TextPercent = Math.Max(125, data.Enhancements.TextPercent); }
        }

        public static void Open(IWin32Window owner, LibraryData data, Action refresh)
        {
            ExperienceData.Ensure(data);
            using (NextDialog dialog = new NextDialog("Appearance and Home", 900, 720))
            {
                TableLayoutPanel fields = NextDialog.Fields(dialog.Body);
                ComboBox theme = NextDialog.Choice(ThemeCatalog.Names, data.Theme.Name);
                ComboBox accent = NextDialog.Choice(AccentCatalog.Names, data.Theme.AccentColor);
                ComboBox density = NextDialog.Choice(new[] { "Compact", "Standard", "Roomy" }, data.Theme.ControlDensity ?? "Compact");
                ComboBox harmony = NextDialog.Choice(new[] { "Harmonized", "Original" }, data.Theme.ColorHarmony ?? "Harmonized");
                ComboBox aspect = NextDialog.Choice(new[] { "Square", "Portrait", "Landscape" }, data.Theme.CoverAspect ?? "Square");
                ComboBox preset = NextDialog.Choice(new[] { "Current", "Larger text", "High contrast", "Reduced motion", "Controller" }, "Current");
                CheckBox restrained = new CheckBox { Text = "Use restrained accent colors", Checked = data.Theme.RestrainedAccents, AutoSize = true };
                ThemePreviewPanel preview = new ThemePreviewPanel { ThemeName = theme.Text, AccentName = accent.Text, Restrained = restrained.Checked, HarmonyMode = harmony.Text };
                NextDialog.Field(fields, "Theme", theme);
                NextDialog.Field(fields, "Accent", accent);
                NextDialog.Field(fields, "Control spacing", density);
                NextDialog.Field(fields, "Color blending", harmony);
                NextDialog.Field(fields, "Preview", preview, 135);
                NextDialog.Field(fields, "", restrained);
                NextDialog.Field(fields, "Home cover proportions", aspect);
                NextDialog.Field(fields, "Accessibility preset", preset);
                CheckedListBox cards = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, AccessibleName = "Home cards in display order" };
                var all = (data.Theme.HomeCardOrder ?? new List<string>()).Where(data.Experience.HomeCards.Contains).Concat(data.Experience.HomeCards).Distinct();
                foreach (string card in all) cards.Items.Add(card, !(data.Theme.HiddenHomeCards ?? new List<string>()).Contains(card));
                NextDialog.Field(fields, "Home cards", cards, 175);
                FlowLayoutPanel order = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
                order.Controls.Add(ExperienceUi.Button("Move up", delegate { Move(cards, -1); }));
                order.Controls.Add(ExperienceUi.Button("Move down", delegate { Move(cards, 1); }));
                NextDialog.Field(fields, "Order selected card", order, 64);
                theme.SelectedIndexChanged += delegate { preview.ThemeName = theme.Text; preview.Invalidate(); };
                accent.SelectedIndexChanged += delegate { preview.AccentName = accent.Text; preview.Invalidate(); };
                restrained.CheckedChanged += delegate { preview.Restrained = restrained.Checked; preview.Invalidate(); };
                harmony.SelectedIndexChanged += delegate { preview.HarmonyMode = harmony.Text; preview.Invalidate(); };
                dialog.Action("Theme samples", delegate { Samples(dialog, theme, accent, harmony.Text, restrained.Checked); });
                dialog.Action("Save", delegate
                {
                    data.Theme.Name = theme.Text; data.Theme.AccentColor = accent.Text;
                    data.Theme.ControlDensity = density.Text; data.Theme.ColorHarmony = harmony.Text;
                    data.Theme.CoverAspect = aspect.Text; data.Theme.RestrainedAccents = restrained.Checked;
                    data.Theme.HomeCardOrder = cards.Items.Cast<string>().ToList();
                    data.Theme.HiddenHomeCards = cards.Items.Cast<string>().Where((x, i) => !cards.GetItemChecked(i)).ToList();
                    ApplyAccessibility(data, preset.Text);
                    Store.Save(data);
                    if (refresh != null) refresh();
                    dialog.Close();
                });
                dialog.Action("Close", dialog.Close);
                dialog.ShowDialog(owner);
            }
        }

        public static void Move(CheckedListBox cards, int offset)
        {
            int index = cards.SelectedIndex, target = index + offset;
            if (index < 0 || target < 0 || target >= cards.Items.Count) return;
            object item = cards.Items[index]; bool check = cards.GetItemChecked(index);
            cards.Items.RemoveAt(index); cards.Items.Insert(target, item); cards.SetItemChecked(target, check); cards.SelectedIndex = target;
        }

        private static void Samples(IWin32Window owner, ComboBox theme, ComboBox accent, string harmony, bool restrained)
        {
            using (NextDialog dialog = new NextDialog("Theme and accent samples", 960, 700))
            {
                FlowLayoutPanel samples = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(8) };
                foreach (string name in ThemeCatalog.Names)
                {
                    string chosen = name;
                    ThemePreviewPanel sample = new ThemePreviewPanel { ThemeName = name, AccentName = accent.Text, HarmonyMode = harmony, Restrained = restrained, Size = new Size(210, 110), Margin = new Padding(8), TabStop = true, Cursor = Cursors.Hand, AccessibleName = "Select " + name + " theme" };
                    sample.Click += delegate { theme.SelectedItem = chosen; dialog.Close(); };
                    sample.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { theme.SelectedItem = chosen; dialog.Close(); } };
                    samples.Controls.Add(sample);
                }
                foreach (string name in AccentCatalog.Names)
                {
                    string chosen = name;
                    ThemePreviewPanel sample = new ThemePreviewPanel { ThemeName = theme.Text, AccentName = name, HarmonyMode = harmony, Restrained = restrained, Caption = name + " accent", Size = new Size(210, 110), Margin = new Padding(8), TabStop = true, Cursor = Cursors.Hand, AccessibleName = "Select " + name + " accent" };
                    sample.Click += delegate { accent.SelectedItem = chosen; dialog.Close(); };
                    sample.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { accent.SelectedItem = chosen; dialog.Close(); } };
                    samples.Controls.Add(sample);
                }
                dialog.Body.Controls.Add(samples);
                dialog.Action("Close", dialog.Close);
                dialog.ShowDialog(owner);
            }
        }
    }

    public static class ControlDensityTools
    {
        public static string Current = "Compact";
        public static bool Compact { get { return Current == "Compact"; } }
        public static int Choose(int compact, int standard, int roomy) { return Compact ? compact : Current == "Roomy" ? roomy : standard; }
        public static void Configure(LibraryData data) { Current = data.Theme == null || string.IsNullOrWhiteSpace(data.Theme.ControlDensity) ? "Compact" : data.Theme.ControlDensity; }
        sealed class Metrics
        {
            public Padding Padding, Margin;
            public Size Minimum;
            public int Height;
            public float[] Rows;
            public Metrics(Control control)
            {
                Padding = control.Padding; Margin = control.Margin; Minimum = control.MinimumSize; Height = control.Height;
                var table = control as TableLayoutPanel;
                if (table != null) Rows = table.RowStyles.Cast<RowStyle>().Select(r => r.Height).ToArray();
            }
        }
        static readonly ConditionalWeakTable<Control, Metrics> original = new ConditionalWeakTable<Control, Metrics>();
        static Padding Tight(Padding p, double scale) { return new Padding((int)Math.Round(p.Left * scale), (int)Math.Round(p.Top * scale), (int)Math.Round(p.Right * scale), (int)Math.Round(p.Bottom * scale)); }
        public static void Apply(Form form)
        {
            if (StartupPromptLayout.IsPrompt(form)) return;
            double scale = Compact ? .68 : Current == "Roomy" ? 1.12 : 1.0;
            form.SuspendLayout();
            try
            {
                foreach (Control control in NextUi.Descendants(form).Concat(new[] { (Control)form }))
                {
                    // Home computes its own responsive metrics; shrinking them twice would make labels overlap.
                    bool home = false;
                    for (Control ancestor = control; ancestor != null; ancestor = ancestor.Parent) if (ancestor is HomeSurface) { home = true; break; }
                    if (home) continue;
                    Metrics metric = original.GetValue(control, c => new Metrics(c));
                    control.Padding = Tight(metric.Padding, scale);
                    control.Margin = Tight(metric.Margin, scale);
                    Button button = control as Button;
                    if (button != null)
                    {
                        int height = Math.Max(button.Font.Height + Choose(10, 16, 20), Choose(28, 34, 42));
                        button.MinimumSize = new Size(Compact ? Math.Min(metric.Minimum.Width, 84) : metric.Minimum.Width, Math.Min(metric.Minimum.Height, height));
                        if (button.Dock == DockStyle.None && !button.AutoSize) button.Height = height;
                    }
                    TableLayoutPanel table = control as TableLayoutPanel;
                    if (table != null && metric.Rows != null && table.Name != "FishBowlWorkspaceShell" && !table.Controls.OfType<AquariumFooter>().Any())
                        for (int i = 0; i < Math.Min(metric.Rows.Length, table.RowStyles.Count); i++)
                            if (table.RowStyles[i].SizeType == SizeType.Absolute && metric.Rows[i] >= 32 && metric.Rows[i] <= 64)
                            {
                                int required = table.Controls.Cast<Control>().Where(c => table.GetPositionFromControl(c).Row == i).Select(c => c.Font.Height + Choose(10, 16, 20) + c.Margin.Vertical).DefaultIfEmpty(table.Font.Height + 12).Max();
                                table.RowStyles[i].Height = Math.Max(required, (float)Math.Round(metric.Rows[i] * scale));
                            }
                    TabControl tabs = control as TabControl;
                    if (tabs != null && tabs.SizeMode == TabSizeMode.Fixed) tabs.ItemSize = new Size(tabs.ItemSize.Width, Math.Max(tabs.Font.Height + 10, Choose(32, 44, 48)));
                }
            }
            finally { form.ResumeLayout(true); }
            if (form is SettingsDialog)
            {
                Label hint = form.Controls.OfType<Label>().FirstOrDefault(c => c.Text.StartsWith("Choose a OneDrive", StringComparison.Ordinal));
                Button browse = form.Controls.OfType<Button>().FirstOrDefault(c => c.Text == "Browse");
                if (hint != null && browse != null)
                {
                    int oldBottom = hint.Bottom;
                    int nextTop = Math.Max(hint.Top, browse.Bottom + 6);
                    int delta = nextTop - hint.Top;
                    if (delta > 0)
                        foreach (Control sibling in form.Controls)
                            if (sibling != hint && sibling.Top >= oldBottom) sibling.Top += delta;
                    hint.Top = nextTop;
                }
            }
            TextFit.FitLabels(form);
        }
    }

    public sealed class ThemePreviewPanel : Control
    {
        public string ThemeName = "FishBowl Water", AccentName = "Ocean";
        public string Caption;
        public bool Restrained;
        public string HarmonyMode = "Original";
        public ThemePreviewPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); AccessibleName = "Theme preview"; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            ThemeColors colors = ThemeCatalog.Get(ThemeName);
            HarmonyColors harmony = ColorHarmony.Preview(ThemeName, AccentName, Restrained, HarmonyMode);
            Color primary, secondary; AccentCatalog.TryGet(AccentName, out primary, out secondary);
            if (Restrained) { primary = UiPolishTools.RestrainAccent(primary, colors.Surface); secondary = UiPolishTools.RestrainAccent(secondary, colors.Surface); }
            using (LinearGradientBrush background = new LinearGradientBrush(ClientRectangle, colors.Top, colors.Bottom, 90f)) e.Graphics.FillRectangle(background, ClientRectangle);
            Rectangle card = new Rectangle(12, 32, Math.Max(1, Width - 24), Math.Max(1, Height - 44));
            using (SolidBrush surface = new SolidBrush(harmony.Card)) e.Graphics.FillRectangle(surface, card);
            FishBowlText.DrawText(e.Graphics, Caption ?? colors.Name, Font, new Rectangle(12, 8, Width - 24, Math.Max(22, Font.Height)), colors.Ink, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            FishBowlText.DrawText(e.Graphics, "Game Library", Font, new Rectangle(20, 40, Width - 40, Math.Max(22, Font.Height)), colors.Ink, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            using (SolidBrush input = new SolidBrush(harmony.Input)) e.Graphics.FillRectangle(input, 20, Math.Min(Height - 44, 40 + Font.Height + 4), Math.Max(1, Width - 40), 9);
            using (SolidBrush brush = new SolidBrush(primary)) e.Graphics.FillRectangle(brush, 20, Height - 28, Math.Max(1, (Width - 48) / 2), 12);
            using (SolidBrush brush = new SolidBrush(secondary)) e.Graphics.FillRectangle(brush, Width / 2, Height - 28, Math.Max(1, (Width - 48) / 2), 12);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), colors.Ink, colors.Bottom);
        }
    }
}
