using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;

namespace EmulatorHub
{
    public sealed class PlayClosingDialog : NextDialog
    {
        internal readonly Label Status = new Label { Text="Waiting for the emulator to close. Save prompts remain available here.",AutoSize=true };
        internal readonly Panel PromptView = new Panel { Dock=DockStyle.Fill,MinimumSize=new Size(400,260) };
        public PlayClosingDialog() : base("Closing games",760,600)
        {
            var fields=NextDialog.Fields(Body);
            NextDialog.Field(fields,"Status",Status,90);
            NextDialog.Field(fields,"Emulator prompt",PromptView,300);
            Action("Cancel",delegate { DialogResult=DialogResult.Cancel;Close(); });
        }
    }
    // Only explicitly tracked process generations and dialogs owned by their closing roots.
    internal static class PlayShutdown
    {
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr window,PlayNative.EnumCallback callback,IntPtr data);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder value,int length);
        [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,StringBuilder text,uint flags,uint timeout,out IntPtr result);
        static string ClassName(IntPtr window) { var value=new StringBuilder(256);GetClassName(window,value,value.Capacity);return value.ToString(); }
        static string Text(IntPtr window) { var value=new StringBuilder(4096);IntPtr result;return SendMessageTimeout(window,0x0d,new IntPtr(value.Capacity),value,2,100,out result)==IntPtr.Zero ? null : value.ToString(); }
        static bool NativeConfirmation(IntPtr window,SessionProcess identity,IntPtr root,CancellationToken token)
        {
            if(ClassName(window)!="#32770") return false;
            var text=new List<string>(); var labels=new List<string>(); IntPtr yes=IntPtr.Zero;int affirmative=0;bool readable=true;
            EnumChildWindows(window,delegate(IntPtr child,IntPtr unused) {
                if(PlayNative.GetParent(child)!=window)return true;
                string kind=ClassName(child);
                if(kind!="Static" && kind!="Button") { readable=false;return true; }
                // Icons have no text. Read all other controls with a bounded cross-process call.
                if(kind=="Static" && (PlayNative.GetLong(child,PlayNative.Style)&31)==3)return true;
                string value=Text(child);if(value==null){readable=false;return true;}
                if(kind=="Static") { if(!string.IsNullOrWhiteSpace(value))text.Add(value); }
                else {
                    labels.Add(value);int id=GetDlgCtrlID(child);
                    string label=value.Replace("&","").Trim().ToLowerInvariant();
                    if(id==6 && label=="yes" || id==1 && label=="ok"){yes=child;affirmative=id;}
                    else if(!(id==7 && label=="no" || id==2 && label=="cancel"))readable=false;
                }
                return true;
            },IntPtr.Zero);
            if(!readable || yes==IntPtr.Zero || !PlainExit(PlayNative.WindowCaption(window),text.ToArray(),labels.ToArray()))return false;
            if(token.IsCancellationRequested || !OwnedPrompt(window,identity.Pid,identity.StartedUtcTicks,root) || !PlayNative.IsWindowEnabled(yes))return false;
            return PlayNative.PostMessage(window,0x111,new IntPtr(affirmative),yes);
        }
        internal static bool OwnedPrompt(IntPtr window, int pid, long started, IntPtr root)
        {
            if(window==root || !PlayNative.Matches(window,pid,started) || !PlayNative.Matches(root,pid,started)) return false;
            IntPtr owner=PlayNative.GetWindow(window,4);
            for(int depth=0;owner!=IntPtr.Zero && depth<16;depth++) {
                if(owner==root) return true;
                if(!PlayNative.Matches(owner,pid,started)) return false;
                owner=PlayNative.GetWindow(owner,4);
            }
            return false;
        }
        internal static bool PlainExit(string title, string[] text, string[] buttons)
        {
            Func<string,string> clean=value=>(value??"").Replace("&","").Trim().ToLowerInvariant();
            string caption=clean(title), message=string.Join(" ",text.Select(clean)).Trim();
            bool azahar=new[]{"azahar","azahar plus","citra","lime3ds"}.Contains(caption) && message=="would you like to exit now?";
            if(!azahar && !new[]{"confirm exit","confirm quit","exit","quit","confirmation","close emulator"}.Contains(caption)) return false;
            var names=new[]{"", " azahar", " azahar plus", " citra", " lime3ds", " dolphin", " ryujinx", " yuzu", " sudachi", " the emulator", " game fixture"};
            bool plain=(from verb in new[]{"exit","quit","close"} from name in names
                select "are you sure you want to "+verb+name+"?").Contains(message);
            var labels=buttons.Select(clean).OrderBy(s=>s).ToArray();
            return (plain || azahar) && (labels.SequenceEqual(new[]{"no","yes"}) || labels.SequenceEqual(new[]{"cancel","ok"}));
        }
        internal static bool Acknowledge(IntPtr window, SessionProcess identity, IntPtr root, CancellationToken token)
        {
            try {
                if(token.IsCancellationRequested || !OwnedPrompt(window,identity.Pid,identity.StartedUtcTicks,root) || PlayNative.IsHungAppWindow(window)) return false;
                if(NativeConfirmation(window,identity,root,token))return true;
                if(ClassName(window)=="#32770")return false;
                var element=AutomationElement.FromHandle(window);
                if(element==null || element.Current.ProcessId!=identity.Pid) return false;
                var controls=element.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().ToArray();
                if(controls.Length>128 || controls.Any(e=>e.Current.ProcessId!=identity.Pid ||
                    new[]{ControlType.CheckBox,ControlType.RadioButton,ControlType.Edit,ControlType.Document,ControlType.Hyperlink,ControlType.Custom}.Contains(e.Current.ControlType))) return false;
                var text=controls.Where(e=>e.Current.ControlType==ControlType.Text).Select(e=>e.Current.Name).Where(s=>!string.IsNullOrWhiteSpace(s)).ToArray();
                var buttons=controls.Where(e=>e.Current.ControlType==ControlType.Button).ToArray();
                if(!PlainExit(element.Current.Name,text,buttons.Select(b=>b.Current.Name).ToArray())) return false;
                var affirmative=buttons.Single(b=>new[]{"yes","ok"}.Contains(b.Current.Name.Replace("&","").Trim().ToLowerInvariant()));
                object pattern;
                if(!affirmative.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)) return false;
                // Recheck ownership and cancellation after potentially slow accessibility calls.
                if(token.IsCancellationRequested || !OwnedPrompt(window,identity.Pid,identity.StartedUtcTicks,root) || !PlayNative.IsWindowEnabled(window) || !affirmative.Current.IsEnabled) return false;
                ((InvokePattern)pattern).Invoke();
                return true;
            } catch(Exception error) { Store.Log("Emulator exit prompt remains available: "+error.Message); return false; }
        }
        sealed class Prompt
        {
            internal IntPtr Window, Root;
            internal SessionProcess Identity;
        }
        internal static bool Close(Form owner, IList<SessionProcess> identities, IList<IntPtr> gameRoots)
        {
            var roots=new List<IntPtr>(gameRoots);
            // Include verified manager windows, including hidden frontends, once only.
            PlayNative.EnumWindows(delegate(IntPtr window,IntPtr unused) {
                uint pid; PlayNative.GetWindowThreadProcessId(window,out pid);
                if(PlayNative.GetWindow(window,4)==IntPtr.Zero && (PlayNative.GetLong(window,PlayNative.Style)&PlayNative.Child)==0 &&
                    identities.Any(p=>p.Pid==pid && PlayNative.Matches(window,p.Pid,p.StartedUtcTicks))) roots.Add(window);
                return true;
            },IntPtr.Zero);
            var targets=roots.Distinct().Select(w=>new Prompt { Window=w, Identity=identities.FirstOrDefault(p=>PlayNative.Matches(w,p.Pid,p.StartedUtcTicks)) }).Where(p=>p.Identity!=null).ToArray();
            using(var dialog=new PlayClosingDialog())
            using(var cancel=new CancellationTokenSource())
            using(var timer=new System.Windows.Forms.Timer { Interval=100 }) {
                var status=dialog.Status;
                var host=new PlayWindowHost { PreserveRendererWindow=true };
                host.ConfigureView(new PlayViewSettings { ShowControls=true });dialog.PromptView.Controls.Add(host);
                var watch=new Stopwatch(); Task probe=null; var token=cancel.Token;
                var attempted=new HashSet<IntPtr>(); Prompt attached=null;
                dialog.Shown+=delegate {
                    watch.Start();
                    foreach(var target in targets) if(PlayNative.Matches(target.Window,target.Identity.Pid,target.Identity.StartedUtcTicks)) PlayNative.PostMessage(target.Window,0x10,IntPtr.Zero,IntPtr.Zero);
                    timer.Start();
                };
                timer.Tick+=delegate {
                    if(!identities.Any(PlayNative.IsAlive)) { dialog.DialogResult=DialogResult.OK;dialog.Close();return; }
                    if(attached!=null && !PlayNative.Matches(attached.Window,attached.Identity.Pid,attached.Identity.StartedUtcTicks)) { host.TryDetach();attached=null; }
                    var prompts=new List<Prompt>();
                    PlayNative.EnumWindows(delegate(IntPtr window,IntPtr unused) {
                        if(!PlayNative.IsWindowVisible(window) || !PlayNative.IsWindowEnabled(window)) return true;
                        foreach(var target in targets) if(OwnedPrompt(window,target.Identity.Pid,target.Identity.StartedUtcTicks,target.Window)) {
                            prompts.Add(new Prompt { Window=window,Root=target.Window,Identity=target.Identity });break;
                        }
                        return true;
                    },IntPtr.Zero);
                    // Hosting changes the owner, so retain the verified prompt we already own.
                    if(attached!=null && host.IsAttached) prompts.Insert(0,attached);
                    var next=prompts.FirstOrDefault();
                    if(next!=null && attached==null) {
                        status.Text="Review the emulator prompt below. Save or cancel choices are yours.";
                        // Probe before hosting: the provider runs on a worker, never the UI thread.
                        if((probe==null || probe.IsCompleted) && attempted.Add(next.Window)) {
                            var captured=next; probe=Task.Factory.StartNew(delegate { return Acknowledge(captured.Window,captured.Identity,captured.Root,token); });
                        }
                        // Wait briefly for a plain exit confirmation; unknown prompts are embedded.
                        if(probe==null || probe.IsCompleted || watch.ElapsedMilliseconds>2000) {
                            if(host.AttachExitPrompt(next.Window,next.Identity.Pid,next.Identity.StartedUtcTicks,next.Root)) attached=next;
                        }
                    }
                    host.RefreshPlacement();
                    if(next==null && attached==null && watch.ElapsedMilliseconds>6000) {
                        // A silent veto or unsupported renderer must not trap FishBowl in shutdown.
                        dialog.DialogResult=DialogResult.Cancel;dialog.Close();
                    }
                };
                dialog.FormClosing+=delegate { timer.Stop();cancel.Cancel();host.TryDetach(); };
                var result=dialog.ShowDialog(owner);
                return result==DialogResult.OK && !identities.Any(PlayNative.IsAlive);
            }
        }
    }
}
