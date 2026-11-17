using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal static class ReferenceWindowStyle
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal static void ApplyCaption(Form form)
        {
            if (!form.IsHandleCreated) return;
            try
            {
                bool contrast = SystemInformation.HighContrast;
                int dark = !contrast && FishBowlPalette.ThemeBottom.GetBrightness() < .38f ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                int corners = CosmeticRuntime.Current.CornerRadius == 0 ? 1 : 2;
                DwmSetWindowAttribute(form.Handle, 33, ref corners, sizeof(int));
                int background = contrast ? -1 : ColorTranslator.ToWin32(FishBowlPalette.ThemeTop);
                int foreground = contrast ? -1 : ColorTranslator.ToWin32(FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, FishBowlPalette.ThemeTop));
                DwmSetWindowAttribute(form.Handle, 35, ref background, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 36, ref foreground, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }

    internal sealed class ReferenceToolbar : FlowLayoutPanel
    {
        internal ReferenceToolbar() { DoubleBuffered = true; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(Parent == null ? FishBowlPalette.ThemeBottom : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = CosmeticRuntime.Current.CornerRadius == 6 ? 12 : CosmeticRuntime.Current.CornerRadius;
            using (var outline = ReferenceButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var fill = new SolidBrush(BackColor)) e.Graphics.FillPath(fill, outline);
        }
    }

    public sealed class ReferenceComboBox : ComboBox
    {
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (SystemInformation.HighContrast || !IsHandleCreated || IsDisposed) return;
            bool printing = message.Msg == 0x317 || message.Msg == 0x318;
            if (message.Msg != 0xF && message.Msg != 0x85 && !printing) return;
            if (printing && message.WParam == IntPtr.Zero) return;
            using (var graphics = printing ? Graphics.FromHdc(message.WParam) : Graphics.FromHwnd(Handle))
            {
                Color background = BackColor;
                Color ink = FishBowlPalette.EnsureReadable(Enabled ? ForeColor : FishBowlPalette.ThemeSubtle, background);
                int arrowWidth = Math.Min(Width, SystemInformation.VerticalScrollBarWidth + 3);
                int left = RightToLeft == RightToLeft.Yes ? 1 : Width - arrowWidth;
                var arrow = new Rectangle(left, 1, Math.Max(1, arrowWidth - 1), Math.Max(1, Height - 2));
                using (var fill = new SolidBrush(background)) graphics.FillRectangle(fill, arrow);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int centerX = arrow.Left + arrow.Width / 2, centerY = arrow.Top + arrow.Height / 2;
                using (var pen = new Pen(ink, 1.4f))
                    graphics.DrawLines(pen, new[] { new Point(centerX - 4, centerY - 2), new Point(centerX, centerY + 2), new Point(centerX + 4, centerY - 2) });
                using (var border = new Pen(Focused ? CosmeticRuntime.Focus(background) : ColorHarmony.Border))
                    graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            }
        }
    }

    internal sealed class ReferenceMessageText : TextBox
    {
        internal ReferenceMessageText()
        {
            ReadOnly = true;
            Multiline = true;
            BorderStyle = BorderStyle.None;
            Dock = DockStyle.Fill;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitScrollBar(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); FitScrollBar(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitScrollBar(); }

        private void FitScrollBar()
        {
            if (!Multiline || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            int width = Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            int height = TextRenderer.MeasureText(Text, Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
            ScrollBars desired = height > ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (ScrollBars != desired) ScrollBars = desired;
        }
    }

    public static class FishBowlMessageBox
    {
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            return Show(null, text, caption, buttons, icon, defaultButton);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            using (var dialog = new ReferenceMessageDialog(text, caption, buttons, icon, defaultButton))
                return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }
    }

    public sealed class ReferenceMessageDialog : NextDialog
    {
        public ReferenceMessageDialog(string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) : base(caption, 640, 380)
        {
            MinimumSize = new Size(420, 280);
            var message = new ReferenceMessageText
            {
                Text = text ?? "", WordWrap = true,
                AccessibleName = caption, BackColor = ColorHarmony.Card, ForeColor = FishBowlPalette.ThemeInk
            };
            Body.Controls.Add(message);
            if (icon != MessageBoxIcon.None)
            {
                Icon image = icon == MessageBoxIcon.Error ? SystemIcons.Error : icon == MessageBoxIcon.Warning ? SystemIcons.Warning :
                    icon == MessageBoxIcon.Question ? SystemIcons.Question : SystemIcons.Information;
                Body.Controls.Add(new PictureBox { Dock = DockStyle.Left, Width = 48, Padding = new Padding(4, 2, 4, 2), Image = image.ToBitmap(), SizeMode = PictureBoxSizeMode.Normal });
                var ownedImage = Body.Controls.OfType<PictureBox>().Single().Image;
                Disposed += delegate { ownedImage.Dispose(); };
            }
            DialogResult[] results;
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel: results = new[] { DialogResult.OK, DialogResult.Cancel }; break;
                case MessageBoxButtons.YesNo: results = new[] { DialogResult.Yes, DialogResult.No }; break;
                case MessageBoxButtons.YesNoCancel: results = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }; break;
                case MessageBoxButtons.RetryCancel: results = new[] { DialogResult.Retry, DialogResult.Cancel }; break;
                case MessageBoxButtons.AbortRetryIgnore: results = new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore }; break;
                default: results = new[] { DialogResult.OK }; break;
            }
            foreach (DialogResult result in results)
            {
                var action = new FishBowlActionButton { Text = result == DialogResult.OK ? "OK" : result.ToString(),
                    DialogResult = result, AutoSize = true, Margin = new Padding(5), AccessibleName = result.ToString() };
                Actions.Controls.Add(action);
            }
            var actions = Actions.Controls.OfType<Button>().ToArray();
            int selected = Math.Min(actions.Length - 1, Math.Max(0, (int)defaultButton / 256));
            AcceptButton = actions[selected];
            CancelButton = actions.Last();
            Shown += delegate { actions[selected].Focus(); };
            FormClosing += delegate { if (DialogResult == DialogResult.None) DialogResult = results.Last(); };
        }
    }
}
