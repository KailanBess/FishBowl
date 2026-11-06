using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using EmulatorHub;

class ReferenceLayoutTests
{
    static Control[] All(Control root) { return root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c))).ToArray(); }
    [STAThread] static int Main()
    {
        try
        {
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
            Console.WriteLine("PASS: platform grouping, search, selection routing and honest empty-library state."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
