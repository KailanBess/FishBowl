using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class SmoothPainting
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> enabled = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        static readonly System.Reflection.PropertyInfo buffered = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        public static void Enable(Control control)
        {
            if (!(control is Panel || control is UserControl || control is ListView)) return;
            object marker;
            if (enabled.TryGetValue(control, out marker)) return;
            buffered.SetValue(control, true, null);
            enabled.Add(control, new object());
        }
    }

    public static class ConsistentInputs
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> watched = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        public static void Watch(Control c, Action<Control> style)
        {
            object marker;
            if (watched.TryGetValue(c, out marker)) return;
            watched.Add(c, new object());
            c.ControlAdded += delegate(object sender, ControlEventArgs e) { style(e.Control); };
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, object> choices = new System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, object>();
        public static void Style(Control c)
        {
            var choice = c as ComboBox;
            if (choice != null && choice.DropDownStyle == ComboBoxStyle.DropDownList) {
                choice.DrawMode = DrawMode.OwnerDrawFixed;
                choice.ItemHeight = Math.Max(18, choice.Font.Height + 6);
                choice.BackColor = FishBowlPalette.DeepSeaSurface;
                choice.ForeColor = FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, choice.BackColor);
                object marker;
                if (!choices.TryGetValue(choice, out marker)) {
                    choices.Add(choice, new object());
                    choice.FontChanged += delegate { choice.ItemHeight = Math.Max(18, choice.Font.Height + 6); };
                    choice.EnabledChanged += delegate { choice.Invalidate(); };
                    choice.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                        bool selected = (e.State & DrawItemState.Selected) != 0;
                        Color background = selected ? CosmeticRuntime.Selection : choice.BackColor;
                        Color ink = FishBowlPalette.EnsureReadable(choice.Enabled ? choice.ForeColor : FishBowlPalette.ThemeSubtle, background);
                        if (SystemInformation.HighContrast) { background = selected ? SystemColors.Highlight : SystemColors.Window; ink = selected ? SystemColors.HighlightText : SystemColors.WindowText; }
                        using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                        string caption = e.Index >= 0 && e.Index < choice.Items.Count ? choice.GetItemText(choice.Items[e.Index]) : choice.Text;
                        FishBowlText.DrawText(e.Graphics, caption, choice.Font, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 10), e.Bounds.Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
                    };
                }
            }
            var text = c as TextBoxBase;
            if (text != null)
            {
                text.BorderStyle = BorderStyle.FixedSingle;
                text.BackColor = FishBowlPalette.DeepSeaSurface;
                text.ForeColor = FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk, text.BackColor);
                // Preserve native caret, selection, scrollbars and DPI sizing; no clipping regions.
                if (text.Region != null) { var old = text.Region; text.Region = null; old.Dispose(); }
            }
            var tabs = c as FishBowlTabs;
            if (tabs != null)
            {
                tabs.LegacyHeaders = true;
                tabs.SurfaceColor = FishBowlPalette.ThemeTop;
                tabs.HeaderTextColor = FishBowlPalette.ThemeInk;
                tabs.AccentColor = FishBowlPalette.IconAccent;
            }
            if (c is TabPage)
            {
                c.BackColor = FishBowlPalette.DeepSeaSurface;
                c.ForeColor = FishBowlPalette.ThemeInk;
                ((TabPage)c).UseVisualStyleBackColor = false;
            }
        }
    }
}
