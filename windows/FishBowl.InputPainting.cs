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
        public static void Style(Control c)
        {
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
