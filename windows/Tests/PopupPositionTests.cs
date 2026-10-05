using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EmulatorHub;
class PopupPositionTests
{
    delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct Call {public IntPtr Param,Value;public uint Message;public IntPtr Window;}
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;public Rectangle Bounds {get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}}}
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static Form current;static HookProc callback;static List<Rectangle> visible=new List<Rectangle>();static int checks;
    static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    static Form Prompt(int i) {switch(i){case 0:return new WhatsNewDialog("1.25.6");case 1:return new StartupAssistantDialog();case 2:return new GameStoragePromptDialog();case 3:return new RequirementsStoragePromptDialog();default:return new ResultsDialog("FishBowl Notifications",new[]{"Fixture notice"});}}
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.Idle+=delegate {FishBowlPalette.StyleOpenWindows();};
        callback=delegate(int code,IntPtr w,IntPtr l)
        {
            if(code>=0&&current!=null&&current.IsHandleCreated)
            {
                Call call=(Call)Marshal.PtrToStructure(l,typeof(Call));
                if(call.Window==current.Handle&&(call.Message==0x0047||call.Message==0x0018)&&IsWindowVisible(call.Window))
                {Rect rect;if(GetWindowRect(call.Window,out rect))visible.Add(rect.Bounds);}
            }
            return CallNextHookEx(IntPtr.Zero,code,w,l);
        };
        IntPtr hook=SetWindowsHookEx(4,callback,IntPtr.Zero,GetCurrentThreadId());
        try
        {
            Check(hook!=IntPtr.Zero,"Native window message hook installed");
            using(var owner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(-5000,-5000,900,650)})
            {
                owner.Show();Application.DoEvents();
                foreach(int text in new[]{100,150,200})
                {
                    NextUi.TextPercent=text;
                    for(int i=0;i<5;i++)using(var f=Prompt(i))using(var timer=new Timer{Interval=30})
                    {
                        current=f;visible.Clear();Rectangle final=Rectangle.Empty;
                        timer.Tick+=delegate{timer.Stop();((Button)f.AcceptButton).PerformClick();};
                        f.Shown+=delegate{final=f.Bounds;timer.Start();};
                        f.ShowDialog(owner);current=null;
                        Check(visible.Count>0,"Native visible bounds recorded: "+f.Text);
                        foreach(var bounds in visible)Check(bounds==final,"Popup became visible before final position/size: "+f.Text+" "+bounds+" expected "+final);
                        Check(Screen.FromRectangle(final).WorkingArea.Contains(final),"Popup stays inside the working area");
                    }
                }
                NextUi.TextPercent=100;
                Rectangle workArea=Screen.PrimaryScreen.WorkingArea;
                owner.Bounds=new Rectangle(workArea.Left+100,workArea.Top+100,900,650);
                foreach(var state in new[]{FormWindowState.Normal,FormWindowState.Maximized})
                {
                    owner.WindowState=state;Application.DoEvents();
                    for(int i=0;i<5;i++)using(var f=Prompt(i))using(var timer=new Timer{Interval=30})
                    {
                        current=f;visible.Clear();Rectangle final=Rectangle.Empty;
                        timer.Tick+=delegate{timer.Stop();f.Close();};f.Shown+=delegate{final=f.Bounds;timer.Start();};
                        f.ShowDialog(owner);current=null;
                        Check(visible.Count>0,"Normal/maximized owner visible bounds captured");
                        foreach(var bounds in visible)Check(bounds==final,"Normal/maximized owner popup appears at final bounds");
                        Check(Math.Abs(final.Left+final.Width/2-(owner.Left+owner.Width/2))<=1,"Popup centered over owner horizontally");
                        Check(Math.Abs(final.Top+final.Height/2-(owner.Top+owner.Height/2))<=1,"Popup centered over owner vertically");
                    }
                }
            }
            NextUi.TextPercent=100;
            // Ownerless prompts center on their screen instead of a default cascade position.
            using(var f=Prompt(1))using(var timer=new Timer{Interval=30})
            {
                current=f;visible.Clear();Rectangle final=Rectangle.Empty;
                timer.Tick+=delegate{timer.Stop();f.Close();};f.Shown+=delegate{final=f.Bounds;timer.Start();};
                f.ShowDialog();current=null;Check(visible.Count>0,"Ownerless visible bounds captured");
                foreach(var bounds in visible)Check(bounds==final,"Ownerless popup appears at final bounds");
                Rectangle area=Screen.FromRectangle(final).WorkingArea;
                Check(Math.Abs(final.Left+(final.Width/2)-(area.Left+area.Width/2))<=1,"Ownerless popup centered horizontally");
                Check(Math.Abs(final.Top+(final.Height/2)-(area.Top+area.Height/2))<=1,"Ownerless popup centered vertically");
            }
            Console.WriteLine("PASS: "+checks+" native popup first-visible position/size checks.");return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
        finally{UnhookWindowsHookEx(hook);}
    }
}
