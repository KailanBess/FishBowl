using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class CompactTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static void Capture(Form form,string path) {Application.DoEvents(); using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path);}}
    static void Leaves(ToolStripItemCollection items,System.Collections.Generic.List<string> values) {foreach(var item in items.OfType<ToolStripMenuItem>()) {if(item.DropDownItems.Count==0) values.Add(item.Text); else Leaves(item.DropDownItems,values);}}
    [STAThread] static int Main(string[] args) {
        Application.EnableVisualStyles(); Application.ThreadException+=(s,e)=>{Console.WriteLine(e.Exception);Environment.Exit(1);};
        using(var main=new MainForm(true)) {
            main.ShowInTaskbar=false; main.StartPosition=FormStartPosition.Manual; main.Location=new Point(-4000,-4000); main.Show(); Application.DoEvents();
            var strip=NextUi.Descendants(main).OfType<MenuStrip>().First(); var leaves=new System.Collections.Generic.List<string>(); Leaves(strip.Items,leaves); File.WriteAllLines(args[0],leaves.OrderBy(s=>s));
            if(args.Length>1 && args[1]=="baseline") return 0;
            Check(strip.Items.Count==6,"six top-level menus");
            var library=strip.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="Library");
            Check(library.DropDownItems.Count<=6,"Library menu reduced to primary action and five groups");
            Check(leaves.Contains("Smart lists...") && leaves.Contains("Create local support bundle..."),"new and advanced tools retained");
            Capture(main,"compact-main.png");
            var field=typeof(MainForm).GetField("library",BindingFlags.Instance|BindingFlags.NonPublic);
            var data=(LibraryData)field.GetValue(main);
            foreach(float scale in new [] {1f,1.5f,2f}) using(var ui=new GameLibraryDialog(data)) {
                ui.ShowInTaskbar=false; ui.StartPosition=FormStartPosition.Manual; ui.Location=new Point(-4000,-4000); ui.Show();
                ui.Scale(new SizeF(scale,scale)); Application.DoEvents();
                var bar=NextUi.Descendants(ui).OfType<FlowLayoutPanel>().Single(p=>p.Controls.OfType<Button>().Any(b=>b.Text=="Game actions ▾"));
                Check(bar.Controls.OfType<Button>().Where(b=>b.Visible).Count()<=6,"compact toolbar at "+scale);
                Check(bar.Height<=68*scale,"single roomier toolbar row at "+scale);
                foreach(string title in new [] {"Game actions ▾","Library tools ▾"}) {
                    var button=bar.Controls.OfType<Button>().Single(b=>b.Text==title);
                    typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty}); Application.DoEvents();
                    var menu=(ContextMenuStrip)button.Tag;
                    Check(menu.Items.Count>=8,title+" retains grouped actions at "+scale);
                    if(title=="Game actions ▾") Check(!menu.Items.OfType<ToolStripMenuItem>().Any(i=>i.Enabled),"game actions require selection at "+scale);
                    menu.Close();
                }
                Check(!NextUi.Descendants(ui).OfType<Button>().Single(b=>b.Text=="Launch").Enabled,"launch requires selection at "+scale);
                Capture(ui,"compact-library-"+scale+".png");
            }
        }
        var root=new ToolStripMenuItem("Library"); var item=new ToolStripMenuItem("Save monitoring") {Checked=true,ShortcutKeys=Keys.Control|Keys.S}; int invoked=0; item.Click+=(s,e)=>invoked++;
        root.DropDownItems.Add(item); CompactMenus.Group(root,CompactMenus.LibraryCategory,"Saves");
        Check(root.DropDownItems.OfType<ToolStripMenuItem>().Single().DropDownItems[0]==item && item.Checked && item.ShortcutKeys==(Keys.Control|Keys.S),"grouping retains command instance, state and shortcut");
        item.PerformClick(); Check(invoked==1,"grouped command still invokes action"); root.Dispose();
        Console.WriteLine("PASS: "+checks+" compact-menu and toolbar checks."); return 0;
    }
}
