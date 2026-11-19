using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal sealed class PlaySessionFrame : Panel
    {
        sealed class MoveGrip : Control
        {
            readonly PlaySessionFrame frame;
            bool dragging, resizing;
            Point dragStart;
            Rectangle initial;
            internal MoveGrip(PlaySessionFrame owner, string title)
            {
                frame = owner; Text = title + "  ·  Drag to move";
                AccessibleName = "Move " + title + " within Play";
                AccessibleDescription = "Drag the title to move or its right handle to resize. Arrow keys move; Ctrl and an arrow resizes. Shift adjusts by one pixel. Press Enter to fit to Play.";
                AccessibleRole = AccessibleRole.Grip; Cursor = Cursors.SizeAll; TabStop = true;
                Dock = DockStyle.Top; DoubleBuffered = true;
                SetStyle(ControlStyles.Selectable, true);
                SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            }
            protected override bool IsInputKey(Keys key)
            {
                var code = key & Keys.KeyCode;
                return code == Keys.Left || code == Keys.Right || code == Keys.Up || code == Keys.Down || base.IsInputKey(key);
            }
            Rectangle ResizeHandle { get { return new Rectangle(Math.Max(0, Width - Height), 0, Height, Height); } }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left || !frame.Movable) return;
                Focus();
                resizing = ResizeHandle.Contains(e.Location);
                dragging = true; dragStart = PointToScreen(e.Location); initial = frame.Bounds; Capture = true;
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Cursor = (dragging ? resizing : ResizeHandle.Contains(e.Location)) ? Cursors.SizeNESW : Cursors.SizeAll;
                if (!dragging || !Capture) return;
                Point current = PointToScreen(e.Location);
                int dx = current.X - dragStart.X, dy = current.Y - dragStart.Y;
                if (resizing)
                    frame.ResizeFromTopRight(initial, dx, dy);
                else
                    frame.MoveTo(new Point(initial.Left + dx, initial.Top + dy));
            }
            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) EndDrag(); }
            protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
            protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); if (e.Button == MouseButtons.Left && !ResizeHandle.Contains(e.Location)) frame.Fit(); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                int step = e.Shift ? 1 : 10;
                int x = e.KeyCode == Keys.Left ? -step : e.KeyCode == Keys.Right ? step : 0;
                int y = e.KeyCode == Keys.Up ? -step : e.KeyCode == Keys.Down ? step : 0;
                if (x != 0 || y != 0)
                {
                    if (e.Control) frame.ResizeFromTopRight(frame.Bounds, x, y);
                    else frame.MoveTo(new Point(frame.Left + x, frame.Top + y));
                    e.Handled = e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape) { frame.Fit(); e.Handled = e.SuppressKeyPress = true; }
                base.OnKeyDown(e);
            }
            internal void EndDrag() { dragging = false; Capture = false; }
            protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(ColorHarmony.Card); }
            protected override void OnPaint(PaintEventArgs e)
            {
                FishBowlText.DrawText(e.Graphics, Text, Font, new Rectangle(12, 0, Math.Max(1, Width - Height - 16), Height),
                    FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, ColorHarmony.Card), TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                Rectangle handle = ResizeHandle;
                using (var pen = new Pen(FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, ColorHarmony.Card), 1.5f))
                {
                    int x = handle.Left + handle.Width / 2, y = Height / 2;
                    e.Graphics.DrawLine(pen, x - 5, y + 5, x + 5, y - 5);
                    e.Graphics.DrawLines(pen, new[] { new Point(x, y - 5), new Point(x + 5, y - 5), new Point(x + 5, y) });
                    e.Graphics.DrawLines(pen, new[] { new Point(x, y + 5), new Point(x - 5, y + 5), new Point(x - 5, y) });
                }
                if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), FishBowlPalette.ThemeInk, ColorHarmony.Card);
            }
        }

        readonly PlayWindowHost host;
        readonly MoveGrip grip;
        Control stage;
        bool constraining;
        Rectangle fullscreenRestore;
        internal bool Movable { get; private set; }
        internal bool FullscreenFit { get; private set; }
        internal event EventHandler ModeChanged;

        internal PlaySessionFrame(PlayWindowHost renderer, string title)
        {
            host = renderer; Dock = DockStyle.Fill; DoubleBuffered = true;
            AccessibleName = title + " Play window";
            grip = new MoveGrip(this, title) { Visible = false, Height = 36 };
            Controls.Add(host); Controls.Add(grip);
            LocationChanged += delegate { RefreshRenderer(); };
            SizeChanged += delegate { RefreshRenderer(); };
            VisibleChanged += delegate { if (!Visible) grip.EndDrag(); RefreshRenderer(); };
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (grip != null) grip.Height = Math.Max(36, Font.Height + 16); }
        protected override void OnParentChanged(EventArgs e)
        {
            if (stage != null) stage.SizeChanged -= StageResized;
            base.OnParentChanged(e); stage = Parent;
            if (stage != null) stage.SizeChanged += StageResized;
        }
        void StageResized(object sender, EventArgs e) { if (Movable && !FullscreenFit) MoveTo(Location); RefreshRenderer(); }
        internal void EnableMovement()
        {
            if (FullscreenFit) { SetFullscreen(false); return; }
            if (stage == null || stage.ClientSize.Width < 1 || stage.ClientSize.Height < 1 || Movable) return;
            Movable = true; Dock = DockStyle.None; Padding = new Padding(2); grip.Visible = true;
            int width = Math.Min(stage.ClientSize.Width, Math.Max(240, stage.ClientSize.Width * 4 / 5));
            int height = Math.Min(stage.ClientSize.Height, Math.Max(160, stage.ClientSize.Height * 4 / 5));
            Bounds = new Rectangle((stage.ClientSize.Width - width) / 2, (stage.ClientSize.Height - height) / 2, width, height);
            RefreshRenderer(); NotifyMode();
        }
        internal void MoveTo(Point location)
        {
            if (!Movable || FullscreenFit || stage == null || stage.ClientSize.Width < 1 || stage.ClientSize.Height < 1 || constraining) return;
            constraining = true;
            try
            {
                int width = Math.Max(1, Math.Min(Width, stage.ClientSize.Width)), height = Math.Max(1, Math.Min(Height, stage.ClientSize.Height));
                Bounds = new Rectangle(Math.Max(0, Math.Min(location.X, stage.ClientSize.Width - width)),
                    Math.Max(0, Math.Min(location.Y, stage.ClientSize.Height - height)), width, height);
            }
            finally { constraining = false; }
            RefreshRenderer();
        }
        internal void ResizeFromTopRight(Rectangle initial, int dx, int dy)
        {
            if (!Movable || FullscreenFit || stage == null) return;
            int maxWidth = stage.ClientSize.Width - initial.Left;
            int bottom = Math.Min(initial.Bottom, stage.ClientSize.Height);
            if (maxWidth < 1 || bottom < 1) return;
            int width = Math.Min(maxWidth, Math.Max(240, initial.Width + dx));
            int height = Math.Min(bottom, Math.Max(grip.Height + 80, initial.Height - dy));
            Bounds = new Rectangle(initial.Left, bottom - height, width, height);
        }
        internal void Fit()
        {
            if (!Movable) return;
            grip.EndDrag(); Movable = false; FullscreenFit = false; grip.Visible = false; Padding = Padding.Empty; Dock = DockStyle.Fill;
            if (Parent != null) Parent.PerformLayout(); RefreshRenderer(); NotifyMode();
        }
        internal void SetFullscreen(bool active)
        {
            if (active && Movable && !FullscreenFit)
            {
                grip.EndDrag(); fullscreenRestore = Bounds; FullscreenFit = true;
                grip.Visible = false; Padding = Padding.Empty; Dock = DockStyle.Fill;
            }
            else if (!active && FullscreenFit)
            {
                FullscreenFit = false; Dock = DockStyle.None; Padding = new Padding(2); grip.Visible = true;
                Bounds = fullscreenRestore; MoveTo(Location);
            }
            else return;
            if (Parent != null) Parent.PerformLayout(); RefreshRenderer(); NotifyMode();
        }
        void NotifyMode() { var handler = ModeChanged; if (handler != null) handler(this, EventArgs.Empty); }
        void RefreshRenderer() { if (host != null && !host.IsDisposed) host.RefreshPlacement(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Movable && !FullscreenFit) using (var border = new Pen(ColorHarmony.Border)) e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (stage != null) stage.SizeChanged -= StageResized; grip.EndDrag(); }
            base.Dispose(disposing);
        }
    }
}
