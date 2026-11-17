using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal static class ReferencePalette
    {
        internal static bool Dark(string theme)
        {
            return !string.Equals(theme, "High Contrast", StringComparison.OrdinalIgnoreCase) &&
                FishBowlPalette.ThemeBottom.GetBrightness() < .38f;
        }
        internal static Color Canvas { get { return FishBowlPalette.ThemeBottom; } }
        internal static Color Rail { get { return FishBowlPalette.ThemeTop; } }
        internal static Color Search { get { return ColorHarmony.Input; } }
        private static Color previousAccent, accessibleAccent;
        internal static Color Violet
        {
            get
            {
                if (SystemInformation.HighContrast) return SystemColors.Highlight;
                var accent = FishBowlPalette.IconAccent;
                if (accent != previousAccent || accessibleAccent.IsEmpty)
                {
                    previousAccent = accent;
                    accessibleAccent = ColorHarmony.EnsureContrast(accent, Color.White, 4.5);
                }
                return accessibleAccent;
            }
        }
    }

    internal class ReferenceHomeCard : Panel
    {
        internal ReferenceHomeCard() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = CosmeticRuntime.Current.CornerRadius == 6 ? 12 : CosmeticRuntime.Current.CornerRadius;
            using (var outline = ReferenceButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var fill = new SolidBrush(ColorHarmony.Card))
                e.Graphics.FillPath(fill, outline);
        }
    }

    internal sealed class ReferenceSettingsCard : TableLayoutPanel
    {
        internal ReferenceSettingsCard() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = CosmeticRuntime.Current.CornerRadius == 6 ? 12 : CosmeticRuntime.Current.CornerRadius;
            using (var outline = ReferenceButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var fill = new SolidBrush(ColorHarmony.Card))
                e.Graphics.FillPath(fill, outline);
        }
    }

    internal sealed class ReferenceButton : Button
    {
        internal int Radius = 18;
        internal bool LeftAligned;
        internal bool Primary;
        private bool hovered;

        internal ReferenceButton(string title)
        {
            Text = title;
            Font = new Font("Segoe UI", 11f);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitToolbarCaption(); }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); FitToolbarCaption(); }

        private void FitToolbarCaption()
        {
            if (!(Parent is TableLayoutPanel) || LeftAligned) return;
            int width = Math.Max(84, TextRenderer.MeasureText(Text, Font).Width + 42);
            MinimumSize = new Size(width, 0);
            Width = width;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = Parent == null ? BackColor : Parent.BackColor;
            Color face = BackColor;
            Color ink = ForeColor;
            var destination = Tag as TabPage;
            var tabs = destination == null ? null : destination.Parent as TabControl;
            if (tabs != null && background == ReferencePalette.Rail)
            {
                bool selected = tabs.SelectedTab == destination;
                face = selected ? ReferencePalette.Violet : background;
                ink = selected ? Color.White : Color.FromArgb(222, 214, 218);
            }
            else if (Parent is TableLayoutPanel && background == ReferencePalette.Canvas)
            {
                face = LeftAligned ? ReferencePalette.Search : background;
                ink = Color.FromArgb(228, 221, 236);
            }
            if (Primary) { face = SystemInformation.HighContrast ? SystemColors.Highlight : ReferencePalette.Violet; ink = FishBowlPalette.EnsureReadable(Color.White, face); }
            e.Graphics.Clear(background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width < 2 || rect.Height < 2) return;
            using (var outline = Round(rect, Radius))
            using (var fill = new SolidBrush(hovered ? ControlPaint.Light(face, .10f) : face))
            {
                e.Graphics.FillPath(fill, outline);
                if (Focused && ShowFocusCues)
                    using (var stroke = new Pen(ink)) e.Graphics.DrawPath(stroke, outline);
            }
            int inset = Math.Min(15, Width / 5);
            rect.Inflate(-inset, 0);
            var align = LeftAligned ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            string caption = Text;
            if (destination != null && TextRenderer.MeasureText(caption, Font, Size.Empty, flags).Width > rect.Width)
                caption = destination.Text;
            TextRenderer.DrawText(e.Graphics, caption, Font, rect, ink,
                align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | flags);
        }

        internal static GraphicsPath Round(Rectangle rect, int radius)
        {
            int arc = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, arc, arc, 180, 90);
            path.AddArc(rect.Right - arc, rect.Top, arc, arc, 270, 90);
            path.AddArc(rect.Right - arc, rect.Bottom - arc, arc, arc, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - arc, arc, arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ReferenceGallery : Panel
    {
        private const int Gap = 16;
        private readonly Label featured = Heading("Featured");
        private readonly Label play = Heading("Continue playing");
        private readonly Label tools = Heading("Library tools");
        private readonly GalleryCard[] heroes;
        private readonly GalleryCard[] games;
        private readonly GalleryCard[] actions;
        private readonly ReferenceButton seeAll = new ReferenceButton("See all");
        private readonly ReferenceButton customize = new ReferenceButton("Customize");
        private readonly string theme;

        internal ReferenceGallery(LibraryData library, Action<string, GameEntry, EmulatorProfile> open)
        {
            theme = library.Theme.Name;
            AccessibleName = "FishBowl featured games and emulators";
            Margin = new Padding(0, 0, 0, 14);
            DoubleBuffered = true;
            heroes = new[]
            {
                new GalleryCard("Game Library", "Browse your games, collections, artwork and play history.", "Open library", "▦", 0, true, () => open("Library", null, null)),
                new GalleryCard("Emulators", "Set up your systems and play with the emulators you already use.", "Browse emulators", "◇", 1, true, () => open("Emulators", null, null))
            };
            var recent = library.Games.Where(game => !string.IsNullOrWhiteSpace(game.LastLaunched))
                .OrderByDescending(game => Played(game.LastLaunched));
            var entries = recent.Concat(library.Games.Where(game => game.Pinned || game.Favorite))
                .Concat(library.Games).Distinct().Take(4).ToArray();
            if (entries.Length == 0 || entries.Any(game => string.IsNullOrWhiteSpace(game.LastLaunched)))
                play.Text = "From your library";
            games = entries.Select((item, i) =>
            {
                var emulator = library.Emulators.FirstOrDefault(candidate => candidate.Id == item.EmulatorId);
                bool available = File.Exists(item.Path);
                var description = available ? (emulator == null ? "Game library" : emulator.Name) : "Game file missing";
                var card = new GalleryCard(item.Title, description, available ? "Play" : "Find file", available ? "▷" : "⌕",
                    i, false, () => open(available ? "Game" : "Missing games", item, null));
                card.SetArtwork(UiPolishTools.Cover(item.ArtworkPath, item.Title, new Size(72, 72)));
                var menu = new ContextMenuStrip();
                NavigationController.Register(menu);
                menu.Items.Add("Game details", null, delegate { open("Game details", item, null); });
                card.ContextMenuStrip = menu;
                card.Disposed += delegate { menu.Dispose(); };
                return card;
            }).ToArray();
            if (games.Length == 0)
                games = new[] { new GalleryCard("Your games", "Add a game to start playing.", "Open library", "▷", 0, false, () => open("Library", null, null)) };
            actions = new[]
            {
                new GalleryCard("Add emulator", "Connect an installed emulator.", "Add", "＋", 1, false, () => open("Add emulator", null, null)),
                new GalleryCard("Save history", "Review and restore save snapshots.", "Open", "↶", 2, false, () => open("Save history", null, null)),
                new GalleryCard("Setup readiness", "Check game and emulator paths.", "Review", "✓", 3, false, () => open("Readiness", null, null)),
                new GalleryCard("Organize games", "Manage your library folders.", "Open", "▤", 0, false, () => open("Organize games", null, null))
            };
            seeAll.AccessibleName = "See all games in Library";
            seeAll.Click += delegate { open("Library", null, null); };
            customize.AccessibleName = "Customize Home";
            customize.Click += delegate { open("Customize Home", null, null); };
            Controls.AddRange(new Control[] { featured, play, tools, seeAll, customize });
            Controls.AddRange(heroes);
            Controls.AddRange(games);
            Controls.AddRange(actions);
        }

        private static DateTime Played(string raw)
        {
            DateTime value;
            return DateTime.TryParse(raw, out value) ? value : DateTime.MinValue;
        }

        private static Label Heading(string text)
        {
            return new Label { Text = text, AutoEllipsis = true, Font = new Font("Segoe UI", 17f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        }

        internal void Fit(int width)
        {
            Width = Math.Max(1, width);
            bool dark = ReferencePalette.Dark(theme);
            BackColor = dark ? ReferencePalette.Canvas : FishBowlPalette.ThemeBottom;
            Color ink = dark ? Color.WhiteSmoke : FishBowlPalette.ThemeInk;
            seeAll.BackColor = BackColor;
            seeAll.ForeColor = dark ? Color.FromArgb(197, 139, 255) : ink;
            customize.BackColor = seeAll.BackColor;
            customize.ForeColor = seeAll.ForeColor;
            foreach (Label heading in new[] { featured, play, tools }) { heading.BackColor = BackColor; heading.ForeColor = ink; }
            int y = PlaceHeading(featured, 0, 0);
            int heroColumns = Width >= Math.Max(650, featured.Font.Height * 23) ? 2 : 1;
            int heroWidth = (Width - Gap * (heroColumns - 1)) / heroColumns;
            y = PlaceCards(heroes, y, heroColumns, Math.Max(245, Math.Min(420, (int)(heroWidth * .55f))), dark);
            int seeWidth = Math.Min(110, Width / 3);
            int shelfTop = y + 9;
            y = PlaceHeading(play, shelfTop, seeWidth);
            seeAll.SetBounds(Width - seeWidth, shelfTop, seeWidth, y - shelfTop - 5);
            int columns = Width >= Math.Max(930, featured.Font.Height * 32) ? 4 :
                Width >= Math.Max(510, featured.Font.Height * 15) ? 2 : 1;
            int cardHeight = Math.Max(112, Math.Min(150, Width / 10));
            y = PlaceCards(games, y, Math.Min(columns, games.Length), cardHeight, dark);
            int toolHeadingTop = y + 9;
            int customizeWidth = Math.Min(150, Width / 3);
            y = PlaceHeading(tools, toolHeadingTop, customizeWidth);
            customize.SetBounds(Width - customizeWidth, toolHeadingTop, customizeWidth, y - toolHeadingTop - 5);
            Height = PlaceCards(actions, y, columns, cardHeight, dark);
        }

        private int PlaceHeading(Label label, int y, int reserve)
        {
            int height = Math.Max(42, label.Font.Height + 12);
            label.SetBounds(0, y, Math.Max(1, Width - reserve), height);
            return y + height + 5;
        }

        private int PlaceCards(IList<GalleryCard> list, int y, int columns, int minHeight, bool dark)
        {
            int width = Math.Max(1, (Width - Gap * (columns - 1)) / columns);
            for (int start = 0; start < list.Count; start += columns)
            {
                int end = Math.Min(start + columns, list.Count);
                int height = minHeight;
                for (int i = start; i < end; i++) height = Math.Max(height, list[i].PreferredHeight(width, minHeight));
                for (int i = start; i < end; i++) list[i].Arrange((i - start) * (width + Gap), y, width, height, dark);
                y += height + Gap;
            }
            return y;
        }
    }

    internal sealed class GalleryCard : Panel
    {
        private readonly Label title;
        private readonly Label description;
        private readonly ReferenceButton run;
        private readonly PictureBox badge;
        private readonly bool hero;
        private readonly int variant;
        private readonly string glyph;
        private Image artwork;
        private bool dark;

        internal GalleryCard(string name, string detail, string action, string glyph, int variant, bool hero, Action open)
        {
            AccessibleName = name;
            this.hero = hero;
            this.variant = variant;
            this.glyph = glyph;
            DoubleBuffered = true;
            title = new Label { Text = name, AutoEllipsis = !hero, BackColor = Color.Transparent,
                Font = new Font(hero ? "Bahnschrift" : "Segoe UI", hero ? 27f : 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            description = new Label { Text = detail, AutoEllipsis = true, BackColor = Color.Transparent, Font = new Font("Segoe UI", hero ? 12f : 9f) };
            badge = new PictureBox { BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.Zoom };
            run = new ReferenceButton(action) { AccessibleName = action + " " + name, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            run.Click += delegate { open(); };
            Controls.AddRange(new Control[] { badge, title, description, run });
        }

        internal void SetArtwork(Image image)
        {
            if (artwork != null) artwork.Dispose();
            artwork = image;
            badge.Image = image;
        }

        internal int PreferredHeight(int width, int minHeight)
        {
            int contentWidth = Math.Max(1, width - (hero ? 48 : 98));
            int detailHeight = TextRenderer.MeasureText(description.Text, description.Font, new Size(contentWidth, 1000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            return Math.Max(minHeight, TitleHeight(width) + detailHeight + run.Font.Height + (hero ? 86 : 43));
        }

        private int TitleHeight(int width)
        {
            if (!hero) return Math.Max(24, title.Font.Height + 4);
            int titleWidth = Math.Max(1, width - (width >= 260 ? 122 : 48));
            return Math.Max(62, TextRenderer.MeasureText(title.Text, title.Font, new Size(titleWidth, 1000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 5);
        }

        internal void Arrange(int x, int y, int width, int height, bool dark)
        {
            this.dark = dark;
            SetBounds(x, y, width, height);
            Color face = ColorHarmony.Card;
            BackColor = face;
            title.ForeColor = dark ? Color.White : FishBowlPalette.ThemeInk;
            description.ForeColor = dark ? Color.FromArgb(225, 221, 232) : FishBowlPalette.ThemeInk;
            run.BackColor = hero && dark ? Color.White : dark ? ReferencePalette.Violet : FishBowlPalette.ThemeTop;
            run.ForeColor = hero && dark ? Color.FromArgb(22, 18, 28) : dark ? Color.White : FishBowlPalette.ThemeInk;
            int inset = hero ? 24 : 12;
            int icon = Math.Min(hero ? 62 : 54, Math.Max(0, Width / 4));
            int titleX = inset + icon + 12;
            bool useBadge = width >= (hero ? 260 : 165);
            badge.Visible = useBadge;
            if (!useBadge) titleX = inset;
            int titleHeight = TitleHeight(width);
            if (useBadge) badge.SetBounds(inset, inset, icon, icon);
            title.SetBounds(titleX, inset, Math.Max(1, width - titleX - inset), titleHeight);
            int bodyX = hero ? inset : titleX;
            int bodyY = inset + titleHeight + (hero ? 12 : 1);
            int runHeight = Math.Max(hero ? 42 : 30, run.Font.Height + 10);
            int runY = height - inset - runHeight;
            description.SetBounds(bodyX, bodyY, Math.Max(1, width - bodyX - inset), Math.Max(1, runY - bodyY - 4));
            int runWidth = TextRenderer.MeasureText(run.Text, run.Font).Width + (hero ? 52 : 36);
            run.SetBounds(bodyX, runY, Math.Max(1, Math.Min(runWidth, width - bodyX - inset)), runHeight);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var bounds = new Rectangle(0, 0, Width, Height);
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var outline = ReferenceButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            {
                GraphicsState state = e.Graphics.Save();
                e.Graphics.SetClip(outline);
                using (var fill = new LinearGradientBrush(bounds, BackColor,
                    dark ? Color.FromArgb(variant % 2 == 0 ? 54 : 27, 46, 62) : BackColor, 0f))
                    e.Graphics.FillRectangle(fill, bounds);
                if (hero && dark) PaintBanner(e.Graphics);
                e.Graphics.Restore(state);
            }
            if (badge.Visible && artwork == null)
            {
                using (var shape = ReferenceButton.Round(badge.Bounds, 11))
                using (var brush = new SolidBrush(dark ? Color.FromArgb(variant % 2 == 0 ? 66 : 19, 51, 179) : FishBowlPalette.ThemeTop))
                    e.Graphics.FillPath(brush, shape);
                using (var font = new Font("Segoe UI Symbol", hero ? 23f : 19f))
                    TextRenderer.DrawText(e.Graphics, glyph, font, badge.Bounds, dark ? Color.White : FishBowlPalette.ThemeInk,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void PaintBanner(Graphics graphics)
        {
            for (int line = 0; line < 24; line++)
            {
                float t = line / 23f;
                float x = Width * (.44f + t * .30f);
                float y = Height * (.14f + t * .76f);
                Color ribbon = variant == 0 ? Color.FromArgb(80, 115 + line * 5, 37, 233)
                    : Color.FromArgb(72, 14 + line * 8, 176 - line * 4, 217 - line * 5);
                using (var pen = new Pen(ribbon, Math.Max(2f, Height / 35f)))
                    graphics.DrawBezier(pen, x, y, Width * .71f, y - Height * .43f,
                        Width * .80f, y + Height * .30f, Width * 1.08f, y - Height * .06f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && artwork != null) { badge.Image = null; artwork.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
