using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class SmoothUiTests
{
    static int checks;
    static void Check(bool ok,string name) { checks++; if(!ok) throw new Exception(name); }
    static void Show(Form f) { f.ShowInTaskbar=false; f.StartPosition=FormStartPosition.Manual; f.Location=new Point(-5000,-5000); f.Show(); Application.DoEvents(); }
    static void Capture(Form f,string path) { using(var b=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size)); b.Save(path); } }
    static bool Buffered(Control c) { return (bool)typeof(Control).GetProperty("DoubleBuffered",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c,null); }
    [STAThread] static int Main()
    {
        try { Application.EnableVisualStyles(); Run(); Console.WriteLine("PASS: "+checks+" idle painting, theme refresh, rapid navigation and startup popup checks."); return 0; }
        catch(Exception e) { Console.WriteLine("FAIL: "+e); return 1; }
    }
    static void Run()
    {
        string dir=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"smooth-ui-previews"); Directory.CreateDirectory(dir);
        var data=Store.Load(); NextData.Ensure(data);
        using(var main=new MainForm(true))
        {
            Show(main); FishBowlPalette.StyleOpenWindows(); Application.DoEvents();
            int invalidations=0; main.Invalidated+=delegate { invalidations++; };
            for(int i=0;i<100;i++) FishBowlPalette.StyleOpenWindows();
            Check(invalidations==0,"Unchanged idle theme pass never invalidates the main window");
            Check(Buffered(main),"Main window painting is buffered");
            var tabs=(FishBowlTabs)typeof(MainForm).GetField("workspaceNavigation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
            int tabInvalidations=0; tabs.Invalidated+=delegate { tabInvalidations++; };
            for(int i=0;i<100;i++) tabs.LegacyHeaders=true;
            Check(tabInvalidations==0,"Repeated header styling does not repaint tabs");
            var panel=new Panel(); main.Controls.Add(panel); var box=new TextBox { Text="Stable editing state" }; panel.Controls.Add(box);
            box.Select(2,5); IntPtr handle=box.Handle;
            Check(Buffered(panel),"Newly added layout surfaces are buffered");
            for(int i=0;i<50;i++) FishBowlPalette.StyleOpenWindows();
            Check(box.Handle==handle&&box.SelectionStart==2&&box.SelectionLength==5,"Idle checks preserve native text editing state");
            Color oldAccent=FishBowlPalette.IconAccent;
            FishBowlPalette.Configure(FishBowlPalette.ThemeInk,FishBowlPalette.ThemeTop,FishBowlPalette.ThemeBottom,FishBowlPalette.ThemeSurface,FishBowlPalette.ThemeSubtle,Color.Magenta,Color.Cyan);
            FishBowlPalette.StyleOpenWindows();
            Check(invalidations>0&&tabs.AccentColor==FishBowlPalette.IconAccent,"Actual theme change refreshes open windows");
            invalidations=0; for(int i=0;i<50;i++) FishBowlPalette.StyleOpenWindows();
            Check(invalidations==0,"Refreshed theme is not repainted on later idle passes");
            panel.Dispose();
            var home=(HomeSurface)typeof(MainForm).GetField("homeSurface",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
            int revision=home.ContentRevision;
            for(int i=0;i<90;i++)
            {
                tabs.SelectedIndex=i%3; Application.DoEvents();
                Check(tabs.TabPages.Cast<TabPage>().Count(p=>p.Visible)==1,"Rapid switching keeps exactly one workspace visible");
                Check(!(bool)typeof(MainForm).GetField("workspaceTransition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main),"Every rapid navigation restores layout");
            }
            Check(home.ContentRevision==revision,"Unchanged Home is retained while switching menus");
            Capture(main,Path.Combine(dir,"workspace.png"));
            main.Close();
        }
        foreach(string theme in new[]{"FishBowl Water","Light","High Contrast"}) foreach(int percent in new[]{100,150,200})
        {
            data.Theme.Name=theme; data.Enhancements.TextPercent=percent; Store.Save(data);
            using(var main=new MainForm(true)) { Show(main); main.Close(); }
            Form[] prompts={new StartupAssistantDialog(),new GameStoragePromptDialog(),new RequirementsStoragePromptDialog(),new WhatsNewDialog("1.25.4"),new ResultsDialog("FishBowl Notifications",new[]{"READY  Pokemon X","Installed title launches successfully.","A longer notification row for wrapping and scroll layout."})};
            foreach(Form f in prompts) using(f)
            {
                Show(f);
                Check(Buffered(f),"Startup dialog painting is buffered");
                var root=f.Controls.Find("StartupPromptRoot",true).Single(); var body=f.Controls.Find("StartupPromptBody",true).Single();
                Check(root.Dock==DockStyle.Fill,"Startup popup uses the common shell");
                Check(NextUi.Descendants(f).OfType<PictureBox>().Any(),"Every startup popup includes the common logo");
                var buttons=NextUi.Descendants(f).OfType<Button>().ToArray();
                Check(buttons.Length>0&&f.AcceptButton!=null&&f.CancelButton!=null,"Startup actions support Enter and Escape");
                foreach(var button in buttons)
                {
                    Check(button.Width>=TextRenderer.MeasureText(button.Text,button.Font).Width+58,"Startup captions have room for text and action icons");
                    var rect=f.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                    Check(f.ClientRectangle.Contains(rect),"Startup actions remain inside the popup at every text size");
                    var bodyRect=f.RectangleToClient(body.RectangleToScreen(body.ClientRectangle));
                    Check(!rect.IntersectsWith(bodyRect),"Startup body never covers action footer");
                }
                int count=root.Controls.Count; StartupPromptLayout.Apply(f);
                Check(f.Controls.Count==1&&root.Controls.Count==count,"Startup shell application is idempotent");
                foreach(var text in NextUi.Descendants(f).OfType<TextBox>()) Check(text.ReadOnly&&text.ScrollBars==ScrollBars.Vertical&&text.TabStop,"Startup text can scroll and receive keyboard focus");
                Capture(f,Path.Combine(dir,theme+"-"+percent+"-"+f.GetType().Name+".png"));
                if(f is ResultsDialog) { ((Button)f.AcceptButton).PerformClick(); Application.DoEvents(); Check(f.DialogResult==DialogResult.OK,"Startup notification Close action works"); }
                f.Close();
            }
        }
    }
}
