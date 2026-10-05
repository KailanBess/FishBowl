using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class PopupAnimationTests
{
    static object Status(Form form){return typeof(FishBowlDialog).GetField("startupTransitionStatus",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    class RecreatedWelcome : StartupAssistantDialog {public void Recreate(){RecreateHandle();}}
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Disabled(Form form)
    {Check(Status(form) is int && (int)Status(form)==0,"Windows accepted the transition policy for startup popup: "+form.Text);}
    static void Enabled(Form form)
    {IntPtr handle=form.Handle;Check(!(form is FishBowlDialog)||Status(form)==null,"Other windows retain native transition defaults");}
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            // Welcome is the first native Form created in this fresh process.
            using(var first=new RecreatedWelcome())
            {
                int events=0;first.HandleCreated+=delegate {events++;Disabled(first);Check(!IsWindowVisible(first.Handle),"Transitions disabled before the window becomes visible");};
                IntPtr handle=first.Handle;first.Recreate();Check(events==2,"Recreated handles receive the same transition policy");
            }
            using(var owner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-5000,-5000)})
            {
                owner.Show();Enabled(owner);
                foreach(Form f in new Form[]{new StartupAssistantDialog(),new WhatsNewDialog("1.25.7"),new GameStoragePromptDialog(),new RequirementsStoragePromptDialog(),new ResultsDialog("FishBowl Notifications",new[]{"Fixture notice"})})
                using(f)using(var timer=new Timer{Interval=30})
                {
                    f.HandleCreated+=delegate{Disabled(f);Check(!IsWindowVisible(f.Handle),"Popup HWND is still hidden when animation policy is set");};
                    timer.Tick+=delegate{timer.Stop();Disabled(f);f.Close();};f.Shown+=delegate{Disabled(f);timer.Start();};f.ShowDialog(owner);
                    Check(!f.IsHandleCreated,"Closing the prompt destroys its HWND without animation helpers");
                }
                using(var settings=new TextPromptDialog("Fixture settings","Name"))Enabled(settings);
                using(var results=new ResultsDialog("Fixture results",new[]{"Example"}))Enabled(results);
                Enabled(owner);
            }
            Console.WriteLine("PASS: "+checks+" cold-start popup animation, handle recreation, closure and scope checks.");return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
    }
}
