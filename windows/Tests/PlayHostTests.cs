using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using EmulatorHub;

class PlayHostTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Pump(int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
    }
    static bool Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < 7000) Pump(25);
        return condition();
    }
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
    delegate bool WindowCallback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] static extern int GetRgnBox(IntPtr region, out Rect bounds);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    static Rect RegionBounds(IntPtr window)
    {
        IntPtr region = CreateRectRgn(0,0,0,0);
        try { Rect bounds; Check(GetWindowRgn(window,region)>0 && GetRgnBox(region,out bounds)>0,"native region exists"); GetRgnBox(region,out bounds); return bounds; }
        finally { DeleteObject(region); }
    }
    class RecreatedHost : PlayWindowHost { public void RecreateNativeHandle() { RecreateHandle(); } }
    static IntPtr Find(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr data)
        {
            uint actual; GetWindowThreadProcessId(window, out actual);
            if (actual == pid && GetWindow(window,4)==IntPtr.Zero && IsWindowVisible(window)) found = window;
            return true;
        }, IntPtr.Zero);
        return found;
    }
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
    static Process Child() { return Child(false); }
    static Process Child(bool aware) { return Child(aware ? " --dpi-aware" : ""); }
    static Process Child(string extra)
    {
        var child = Process.Start(new ProcessStartInfo { FileName = Assembly.GetExecutingAssembly().Location, Arguments = "--child"+extra, UseShellExecute = false, CreateNoWindow = true });
        Check(Until(delegate { return !child.HasExited && Find(child.Id) != IntPtr.Zero; }), "real foreign process creates a native window");
        return child;
    }
    static void Stop(Process process, IntPtr window)
    {
        // Only the fixture process returned by Child is closed, after restoring its original parent.
        if (window != IntPtr.Zero && IsWindow(window)) { uint pid; GetWindowThreadProcessId(window, out pid); if (pid == process.Id) PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero); }
        if (!process.WaitForExit(3000) && process.StartTime.ToUniversalTime().Ticks > 0) { process.Kill(); process.WaitForExit(); }
        process.Dispose();
    }
    [STAThread] static int Main(string[] args)
    {
        if (args.Contains("--child"))
        {
            if(args.Contains("--per-monitor")) SetProcessDpiAwarenessContext(new IntPtr(-4));
            else if(args.Contains("--dpi-aware")) SetProcessDpiAwarenessContext(new IntPtr(-2));
            Application.EnableVisualStyles();
            using (var form = new Form { Text = "Native Play Fixture", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(-3500, -3500, 640, 480), ShowInTaskbar=true, FormBorderStyle=FormBorderStyle.SizableToolWindow })
            {
                if(args.Contains("--region")) form.Region = new Region(new Rectangle(0,0,500,350));
                if(args.Contains("--minsize")) form.MinimumSize = new Size(1000,750);
                form.Controls.Add(new Label { Text = "Native cross-process game window", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                Application.Run(form);
            }
            return 0;
        }
        try { Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Run(); Console.WriteLine("PASS: " + checks + " real cross-process Play window ownership, lifetime, navigation and full-screen checks."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void CloseKeepingGames(MainForm main)
    {
        using(var responder=new System.Windows.Forms.Timer { Interval=40 }) {
            responder.Tick+=delegate {
                var prompt=Application.OpenForms.Cast<Form>().OfType<PlayExitDialog>().FirstOrDefault();
                if(prompt==null) return;
                responder.Stop(); NextUi.Descendants(prompt).OfType<Button>().Single(b=>b.Text=="Keep games running").PerformClick();
            };
            responder.Start();main.Close();
        }
    }
    static void Run()
    {
        var process = Child(); IntPtr window = Find(process.Id); long start = process.StartTime.ToUniversalTime().Ticks;
        IntPtr originalParent=GetParent(window);
        int style = GetWindowLong(window, -16), extended = GetWindowLong(window, -20);
        Rect original; GetWindowRect(window, out original);
        try
        {
            using (var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000,-4000), ClientSize = new Size(800,600) })
            using (var host = new RecreatedHost())
            {
                form.Controls.Add(host); form.Show(); Pump(80);
                Check(!host.AttachWindow(window, process.Id, start + 1), "wrong process generation cannot attach native window");
                Check(GetParent(window) == originalParent, "rejected identity preserves external window");
                Check(host.AttachWindow(window, process.Id, start), "verified foreign HWND embeds");
                Check(host.IsAttached && GetParent(window) == host.Handle, "native parent becomes live Play viewport");
                Check((GetWindowLong(window,-16) & 0x40000000) != 0, "native hosted window has child style");
                form.ClientSize = new Size(900,650); Pump(80);
                Rect client; GetClientRect(window, out client);
                Check(client.Right-client.Left == host.ClientSize.Width && client.Bottom-client.Top == host.ClientSize.Height, "foreign client resizes to viewport");
                host.RecreateNativeHandle(); Pump(100);
                Check(host.IsAttached && GetParent(window) == host.Handle && !process.HasExited, "viewport handle recreation preserves native window and process");
                host.Visible = false; Pump(80); Check(!process.HasExited, "hidden Play viewport leaves game process running");
                host.Visible = true; Pump(80); Check(host.IsAttached, "returning to Play retains attachment");
                host.Detach(); Pump(80);
                Check(GetParent(window) == originalParent && !host.IsAttached, "Open in window restores top-level parent");
                Check(GetWindowLong(window,-16) == style && GetWindowLong(window,-20) == extended, "detach restores exact native styles");
                Rect restored; GetWindowRect(window,out restored);
                Check(restored.Left == original.Left && restored.Top == original.Top && restored.Right == original.Right && restored.Bottom == original.Bottom, "detach restores original native placement");
                Check(host.AttachWindow(window, process.Id, start), "native window can return to Play");
                host.Dispose(); Pump(80);
                Check(IsWindow(window) && GetParent(window) == originalParent && !process.HasExited, "host disposal detaches before child HWND destruction");
            }
            using (var main = new MainForm(true))
            {
                main.ShowInTaskbar=false; main.StartPosition=FormStartPosition.Manual; main.Location=new Point(-4000,-4000); main.Show(); Pump(150);
                var tabs=(TabControl)Field(main,"workspaceNavigation");
                Check(tabs.TabCount==5 && tabs.TabPages[3].Text=="Play", "main workspace includes persistent Play section");
                var emptyPlay=(PlaySurface)Field(main,"playSurface");
                Check(NextUi.Descendants(emptyPlay).OfType<Button>().All(b=>!b.Enabled),"empty Play disables actions that require a session: "+string.Join(", ",NextUi.Descendants(emptyPlay).OfType<Button>().Select(b=>b.Text+"="+b.Enabled))+" sessions="+emptyPlay.HasSessions);
                typeof(MainForm).GetMethod("ShowPlay",Private).Invoke(main,new object[]{process,"Native fixture",new List<SessionProcess>()});
                var play=(PlaySurface)Field(main,"playSurface");
                Check(Until(delegate { return NextUi.Descendants(play).OfType<PlayWindowHost>().Any(h=>h.IsAttached); }), "main ShowPlay discovers real launched native window");
                var host=NextUi.Descendants(play).OfType<PlayWindowHost>().Single(h=>h.IsAttached);
                Check(!NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="Show in Play").Enabled,"attached session disables unnecessary retry");
                var footer=(Control)Field(main,"workspaceFooter");
                foreach(int index in new[]{0,1,2,3}) {
                    tabs.SelectedIndex=index; Pump(40);
                    Check(footer.Controls.OfType<Label>().Single(l=>l.Text=="Filter:").Visible==(index==1),"filter caption belongs only to Emulators section "+index);
                    if(index==1) {
                        var toolbar=(Control)Field(main,"primaryToolbar");
                        int content=toolbar.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(0).Max()+toolbar.Padding.Bottom+toolbar.Margin.Vertical;
                        Check(toolbar.Height<=content+4,"emulator toolbar avoids retained blank height after navigation");
                    }
                }
                typeof(PlayWindowHost).GetField("transitioning",Private).SetValue(host,true);
                try {
                    Check(!play.OpenSelectedInWindow() && host.IsAttached,"failed native detach retains actual attachment and external state");
                    Check(NextUi.Descendants(play).OfType<Label>().Any(l=>l.Text.Contains("It remains in Play")),"failed detach gives actionable feedback");
                } finally { typeof(PlayWindowHost).GetField("transitioning",Private).SetValue(host,false); }
                Check(play.OpenSelectedInWindow(),"retry succeeds after native transition completes");
                var returnToPlay=NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="Return to Play");
                Check(returnToPlay.Enabled,"external window button offers an enabled return action");returnToPlay.PerformClick();
                Check(Until(delegate{return host.IsAttached;}),"retry returns exact native session after detach failure");
                using(var closeSharing=new System.Windows.Forms.Timer{Interval=50}) {
                    bool selectedExact=false,keptAttached=false;
                    closeSharing.Tick+=delegate {
                        var dialog=Application.OpenForms.Cast<Form>().OfType<RemotePlayDialog>().FirstOrDefault();
                        if(dialog==null)return;
                        var target=NextUi.Descendants(dialog).OfType<ComboBox>().Single().SelectedItem as RemoteWindow;
                        selectedExact=target!=null&&target.Pid==process.Id&&target.Started==start&&target.Handle==window;
                        keptAttached=host.IsAttached&&host.GameWindow==window&&host.IsAnchored&&main.Enabled;
                        closeSharing.Stop(); dialog.Close();
                    };
                    closeSharing.Start(); NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="Share session").PerformClick();
                    Check(Until(delegate{return selectedExact;}),"guided sharing chooses the exact embedded session generation and HWND");
                    Check(keptAttached,"sharing keeps the same renderer in Play and leaves the app enabled");
                }
                Check(Until(delegate{return host.IsAttached;}),"closing sharing restores selected game into Play");
                foreach(int percent in new[]{150,200}) {
                    int oldText=NextUi.TextPercent; Exception detailError=null;
                    NextUi.TextPercent=percent;
                    using(var closeDetails=new System.Windows.Forms.Timer{Interval=80}) {
                        closeDetails.Tick+=delegate {
                            var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Play session");
                            if(dialog==null)return;closeDetails.Stop();
                            try {
                                foreach(var label in NextUi.Descendants(dialog).OfType<Label>().Where(l=>l.Visible&&!string.IsNullOrEmpty(l.Text))) {
                                    int needed=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(1,label.ClientSize.Width-label.Padding.Horizontal),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
                                    Check(label.ClientSize.Height-label.Padding.Vertical>=needed-3,"session details retain readable labels at "+percent+"%: "+label.Text);
                                }
                            } catch(Exception error) { detailError=error; } finally { dialog.Close(); }
                        };
                        closeDetails.Start();NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="More").ContextMenuStrip.Items[0].PerformClick();
                    }
                    NextUi.TextPercent=oldText;if(detailError!=null)throw detailError;
                }

                foreach(int index in new[]{3,0,1,2,3})
                {
                    tabs.SelectedIndex=index; Pump(80);
                    Check(!process.HasExited && host.IsAttached && host.GameWindow==window && (host.IsAnchored?GetWindow(window,4)==main.Handle:GetParent(window)==host.Handle), "native process and attachment survive main tab "+index);
                }
                var oldOverride=TextFit.WorkingAreaOverride;
                TextFit.WorkingAreaOverride=new Rectangle(-4000,-4000,1024,768);
                try
                {
                    var full=typeof(MainForm).GetMethod("ToggleFullScreen",Private);
                    full.Invoke(main,null); Pump(120); Check(!process.HasExited && host.IsAttached, "main full-screen entry preserves native attachment");
                    Check(NextUi.Descendants(play).OfType<Button>().Any(b=>b.Text=="Exit full screen"&&b.Visible), "native full screen retains visible exit action");
                    full.Invoke(main,null); Pump(120); Check(!process.HasExited && host.IsAttached, "main full-screen exit preserves native attachment");
                }
                finally { TextFit.WorkingAreaOverride=oldOverride; }
                CloseKeepingGames(main); main.Dispose(); Pump(80);
                Check(IsWindow(window)&&GetParent(window)==originalParent&&!process.HasExited, "closing FishBowl restores game window without stopping process");
                Check(GetWindowLong(window,-16)==style && GetWindowLong(window,-20)==extended, "FishBowl shutdown restores original native window styles");
            }
        }
        finally { Stop(process,window); }
        var awareProcess=Child(true); var awareWindow=Find(awareProcess.Id);
        var context=GetWindowDpiAwarenessContext(awareWindow);
        try
        {
            using(var main=new MainForm(true))
            {
                main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Pump(100);
                typeof(MainForm).GetMethod("ShowPlay",Private).Invoke(main,new object[]{awareProcess,"DPI-aware fixture",new List<SessionProcess>()});
                var play=(PlaySurface)Field(main,"playSurface");
                Check(Until(delegate {return NextUi.Descendants(play).OfType<PlayWindowHost>().Any(h=>h.IsAttached);}),"mixed-DPI stage embeds aware renderer without resetting process");
                Check(AreDpiAwarenessContextsEqual(context,GetWindowDpiAwarenessContext(awareWindow)),"embedding preserves foreign system-aware DPI context");
                var another=Child();var anotherWindow=Find(another.Id);
                try
                {
                    typeof(MainForm).GetMethod("ShowPlay",Private).Invoke(main,new object[]{another,"Second fixture",new List<SessionProcess>()});
                    Check(Until(delegate{return NextUi.Descendants(play).OfType<PlayWindowHost>().Count(h=>h.IsAttached)==2;}),"multiple native sessions remain independently hosted");
                    using(var selectTimer=new System.Windows.Forms.Timer{Interval=50})
                    {
                        selectTimer.Tick+=delegate
                        {
                            var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Show running window in Play");
                            if(dialog==null)return;
                            var select=NextUi.Descendants(dialog).OfType<ComboBox>().Single();
                            select.SelectedIndex=0;Check(select.Items.Count==2,"same executable sessions require explicit selection");
                            selectTimer.Stop();NextUi.Descendants(dialog).OfType<Button>().Single(b=>b.Text=="Show in Play").PerformClick();
                        };
                        selectTimer.Start();play.Adopt(awareProcess.MainModule.FileName,"Aware fixture");
                    }
                    Pump(80);
                    var choices=NextUi.Descendants(play).OfType<ComboBox>().Single();
                    Check(choices.SelectedItem.ToString()=="DPI-aware fixture","adopting hosted executable selects existing exact session");
                    var open=NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="Open in window");open.PerformClick();Pump(100);
                    Check(NextUi.Descendants(play).OfType<PlayWindowHost>().Count(h=>h.IsAttached)==1,"Open in window detaches selected session only");
                    play.Launch(awareProcess,"DPI-aware fixture");
                    Check(Until(delegate{return NextUi.Descendants(play).OfType<PlayWindowHost>().Count(h=>h.IsAttached)==2;}),"duplicate exact launch restores existing external session into Play");
                    Check(play.TryDetachAll(),"verified multiple sessions detach successfully before shutdown");
                }
                finally { play.DetachAll();Stop(another,anotherWindow); }
                CloseKeepingGames(main);main.Dispose();Pump(80);
                Check(AreDpiAwarenessContextsEqual(context,GetWindowDpiAwarenessContext(awareWindow))&&!awareProcess.HasExited,"shutdown preserves aware renderer and DPI identity");
            }
        }
        finally { Stop(awareProcess,awareWindow); }
        var perMonitor=Child(" --per-monitor --region --minsize");var monitorWindow=Find(perMonitor.Id);
        var monitorContext=GetWindowDpiAwarenessContext(monitorWindow);var monitorParent=GetParent(monitorWindow);
        var originalRegion=RegionBounds(monitorWindow);
        try
        {
            using(var form=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000)})
            using(var host=new PlayWindowHost())
            {
                form.Controls.Add(host);form.Show();Pump(80);
                Check(host.AttachWindow(monitorWindow,perMonitor.Id,perMonitor.StartTime.ToUniversalTime().Ticks)&&host.IsAnchored,"per-monitor renderer uses owned top-level anchor without DPI reset");
                Check(IsWindow(monitorWindow)&&GetWindow(monitorWindow,4)==form.Handle&&!perMonitor.HasExited,"anchored renderer has only FishBowl owner and remains running");
                Check((GetWindowLong(monitorWindow,-16)&0x40000000)==0,"per-monitor anchored renderer remains a top-level window");
                Check(AreDpiAwarenessContextsEqual(monitorContext,GetWindowDpiAwarenessContext(monitorWindow)),"anchored renderer retains exact original per-monitor awareness");
                Pump(100); var clip=RegionBounds(monitorWindow); Rect viewport;GetWindowRect(host.Handle,out viewport);
                int placements=host.PlacementUpdates;
                var refresh=typeof(PlayWindowHost).GetMethod("RefreshPlacement",Private);
                for(int i=0;i<10;i++) refresh.Invoke(host,null);
                Check(host.PlacementUpdates==placements,"idle renderer does not repeat placement and clipping mutations");

                Check(clip.Right-clip.Left<=viewport.Right-viewport.Left && clip.Bottom-clip.Top<=viewport.Bottom-viewport.Top,"minimum-size foreign renderer is clipped inside physical viewport");
                host.Visible=false;Pump(100);Check(!IsWindowVisible(monitorWindow)&&!perMonitor.HasExited,"anchored renderer hides on non-Play section without exiting");
                host.Visible=true;Pump(100);Check(IsWindowVisible(monitorWindow)&&host.IsAttached,"anchored renderer returns when Play viewport is shown");
                form.Enabled=false;Pump(100);Check(!IsWindowVisible(monitorWindow),"anchored renderer hides behind modal owner");
                form.Enabled=true;Pump(100);Check(IsWindowVisible(monitorWindow),"anchored renderer resumes after modal owner");
                Check(host.TryDetach(),"anchored renderer detaches safely");Pump(100);
                Check(GetParent(monitorWindow)==monitorParent&&!perMonitor.HasExited,"anchored renderer restores exact original owner without process exit");
                var restoredRegion=RegionBounds(monitorWindow);
                Check(restoredRegion.Left==originalRegion.Left && restoredRegion.Top==originalRegion.Top && restoredRegion.Right==originalRegion.Right && restoredRegion.Bottom==originalRegion.Bottom,"detach restores original custom native region");
            }
            using(var main=new MainForm(true))
            {
                main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Pump(100);
                typeof(MainForm).GetMethod("ShowPlay",Private).Invoke(main,new object[]{perMonitor,"Per-monitor fixture",new List<SessionProcess>()});
                var play=(PlaySurface)Field(main,"playSurface");
                Check(Until(delegate{return NextUi.Descendants(play).OfType<PlayWindowHost>().Any(h=>h.IsAnchored);}),"actual Main Play route anchors per-monitor renderer");
                var oldOverride=TextFit.WorkingAreaOverride;TextFit.WorkingAreaOverride=new Rectangle(-4000,-4000,1024,768);
                try
                {
                    var full=typeof(MainForm).GetMethod("ToggleFullScreen",Private);
                    for(int i=0;i<2;i++)
                    {
                        full.Invoke(main,null);Pump(150);
                        Check(!perMonitor.HasExited && NextUi.Descendants(play).OfType<PlayWindowHost>().Any(h=>h.IsAnchored) && AreDpiAwarenessContextsEqual(monitorContext,GetWindowDpiAwarenessContext(monitorWindow)),"actual full-screen transition preserves anchored process, owner and DPI");
                    }
                }
                finally{TextFit.WorkingAreaOverride=oldOverride;}
                CloseKeepingGames(main);main.Dispose();Pump(80);
                Check(GetParent(monitorWindow)==monitorParent&&!perMonitor.HasExited,"actual Main close restores anchored renderer owner without exiting");
            }
        }
        finally{Stop(perMonitor,monitorWindow);}
    }
}
