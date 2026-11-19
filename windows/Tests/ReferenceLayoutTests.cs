using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class ReferenceLayoutTests
{
    static Control[] All(Control root) { return root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c))).ToArray(); }
    [STAThread] static int Main()
    {
        try
        {
            var rows = LibraryJobs.SummarizeActivity(new[] { "  at System.Windows.Forms.NativeWindow.Callback(IntPtr hwnd)", "System.IO.IOException: fixture", "Fixture import completed." }, 5);
            if (rows.Any(row => row.Contains("NativeWindow") || row.Contains("IOException")) || !rows.Contains("Fixture import completed."))
                throw new Exception("Home activity exposed a raw stack trace or lost an activity summary");
            Application.EnableVisualStyles(); string selected = null;
            using (var gallery = new ReferencePlatformGallery(new Dictionary<string, int> { { "Fixture Console A", 2 }, { "Fixture Console B", 1 } }, delegate(string name) { selected = name; }))
            {
                gallery.ShowInTaskbar = false; gallery.Location = new System.Drawing.Point(-30000, -30000); gallery.Show(); Application.DoEvents();
                if (All(gallery).OfType<ReferencePlatformTile>().Count() != 2) throw new Exception("Platform collections missing");
                var search = All(gallery).OfType<TextBox>().Single(); search.Text = "Console B"; Application.DoEvents();
                var tile = All(gallery).OfType<ReferencePlatformTile>().Single();
                if (!tile.Text.Contains("1 game")) throw new Exception("Filtered collection count incorrect");
                tile.PerformClick(); if (selected != "Fixture Console B") throw new Exception("Platform selection was not routed");
            }
            using (var empty = new ReferencePlatformGallery(new Dictionary<string, int>(), delegate { }))
                if (All(empty).OfType<ReferencePlatformTile>().Any()) throw new Exception("Empty library fabricated platforms");
            using (var main = new MainForm(true))
            {
                main.ShowInTaskbar = false;
                main.StartPosition = FormStartPosition.Manual;
                main.Location = new Point(-3000, -3000);
                main.Show();
                var navigation = (TabControl)typeof(MainForm).GetField("workspaceNavigation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main);
                navigation.SelectedIndex = 1;
                main.WindowState = FormWindowState.Normal;
                main.ClientSize = new Size(920, 680);
                Application.DoEvents();
                var toolbar = (FlowLayoutPanel)typeof(MainForm).GetField("primaryToolbar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main);
                var search = toolbar.Controls.OfType<TextBox>().Single();
                if (!search.Visible || !toolbar.Controls.Cast<Control>().Where(c => c.Visible).All(c => toolbar.ClientRectangle.Contains(c.Bounds)))
                    throw new Exception("Emulator toolbar clipped an action or filter at a narrow window width");
            }
            using (var stage = new Panel { Size = new Size(800, 600) })
            {
                var frameType = typeof(PlayWindowHost).Assembly.GetType("EmulatorHub.PlaySessionFrame", true);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var host = new PlayWindowHost();
                var frame = (Control)Activator.CreateInstance(frameType, flags, null, new object[] { host, "Fixture" }, null);
                stage.Controls.Add(frame);
                frameType.GetMethod("EnableMovement", flags).Invoke(frame, null);
                var start = frame.Bounds;
                var client = host.ClientSize;
                frameType.GetMethod("ResizeFromTopRight", flags).Invoke(frame, new object[] { start, 100, -100 });
                stage.PerformLayout();
                if (host.Width <= client.Width || host.Height <= client.Height || !stage.ClientRectangle.Contains(frame.Bounds))
                    throw new Exception("Resizing the frame failed to enlarge the renderer viewport within Play");
                frameType.GetMethod("Fit", flags).Invoke(frame, null);
                if (frame.Bounds != stage.ClientRectangle)
                    throw new Exception("Fit to Play failed to restore the full viewport after resizing");
            }
            Console.WriteLine("PASS: platform layout, narrow emulator toolbar, and movable Play viewport sizing."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
