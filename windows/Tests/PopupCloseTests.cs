using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EmulatorHub;
class PopupCloseTests
{
    static int checks;
    delegate bool EnumWindow(IntPtr window,IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,EnumWindow callback,IntPtr state);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static HashSet<IntPtr> VisibleWindows()
    {
        var result=new HashSet<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(),delegate(IntPtr h,IntPtr ignored) { if(IsWindowVisible(h)) result.Add(h); return true; },IntPtr.Zero);
        return result;
    }
    static void Check(bool ok,string name) { checks++; if(!ok) throw new Exception(name); }
    static Form Prompt(int index)
    {
        switch(index) { case 0:return new WhatsNewDialog("1.25.5");case 1:return new StartupAssistantDialog();case 2:return new GameStoragePromptDialog();case 3:return new RequirementsStoragePromptDialog();default:return new ResultsDialog("FishBowl Notifications",new[]{"Fixture notice"}); }
    }
    [STAThread] static int Main()
    {
        try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); Application.Idle+=delegate {FishBowlPalette.StyleOpenWindows();}; Run(); Console.WriteLine("PASS: "+checks+" modal popup ownership, button/Enter/Escape/X closure and native window cleanup checks."); return 0; }
        catch(Exception e) { Console.WriteLine("FAIL: "+e); return 1; }
    }
    static void Run()
    {
        using(var owner=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-5000,-5000)})
        {
            owner.Show(); Application.DoEvents();
            for(int round=0;round<3;round++) for(int mode=0;mode<4;mode++) for(int index=0;index<5;index++)
            {
                var baseline=VisibleWindows(); var f=Prompt(index); bool closing=false; int created=0,recreatedAfterClose=0;
                Check(!f.ShowInTaskbar,"Startup popup is not a standalone taskbar window");
                f.HandleCreated+=delegate {created++; if(closing) recreatedAfterClose++;};
                using(var timer=new Timer {Interval=30})
                {
                    timer.Tick+=delegate
                    {
                        timer.Stop(); closing=true;
                        if(mode==0) ((Button)f.AcceptButton).PerformClick();
                        else if(mode==1||mode==2)
                        {
                            bool handled=(bool)typeof(Form).GetMethod("ProcessDialogKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{mode==1?Keys.Enter:Keys.Escape});
                            Check(handled,"Enter/Escape uses dialog actions");
                        }
                        else f.Close();
                    };
                    f.Shown+=delegate { Check(!IsWindowEnabled(owner.Handle),"Modal popup disables its owner while open"); timer.Start(); };
                    f.ShowDialog(owner);
                }
                f.Dispose(); Application.DoEvents();
                Check(created==1&&recreatedAfterClose==0,"Popup native window is created once and never recreated during close");
                Check(f.IsDisposed&&!f.IsHandleCreated,"Disposed popup has no native window");
                Check(IsWindowEnabled(owner.Handle)&&owner.Visible,"Main owner stays visible and enabled after popup closure");
                Check(VisibleWindows().SetEquals(baseline),"Closing a startup popup leaves no additional visible top-level window");
            }
            // Cleanup of a dialog that was never displayed also must not create a window.
            using(var f=new StartupAssistantDialog())
            {
                int created=0; f.HandleCreated+=delegate {created++;}; f.Dispose();
                Check(created==0&&!f.IsHandleCreated,"Undisplayed prompt disposal does not create a native window");
            }
        }
        using(var main=new MainForm(true))
        {
            int created=0; bool closing=false; main.HandleCreated+=delegate {if(closing) created++;};
            main.ShowInTaskbar=false; main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-5000,-5000);main.Show();Application.DoEvents();
            closing=true;main.Close();main.Dispose();Application.DoEvents();
            Check(created==0&&!main.IsHandleCreated,"Main window cleanup never recreates its native window");
        }
    }
}
