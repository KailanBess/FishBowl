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

class PlayExitTests {
 static int checks;
 static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
 static void Pump(int ms) { var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);} }
 static bool Until(Func<bool> done) { var watch=Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<8000)Pump(25);return done(); }
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 delegate bool Callback(IntPtr window,IntPtr data);
 [DllImport("user32.dll")]static extern bool EnumWindows(Callback callback,IntPtr data);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr GetMenu(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int capacity);
 static string Caption(IntPtr window){var text=new System.Text.StringBuilder(256);GetWindowText(window,text,text.Capacity);return text.ToString();}
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 static IntPtr Find(int pid) { IntPtr result=IntPtr.Zero;EnumWindows(delegate(IntPtr window,IntPtr unused){uint actual;Rect rect;GetWindowThreadProcessId(window,out actual);if(actual==pid&&Caption(window)=="Game fixture"&&GetParent(window)==IntPtr.Zero&&IsWindowVisible(window)&&GetWindowRect(window,out rect)&&rect.Right-rect.Left>100&&rect.Bottom-rect.Top>100)result=window;return true;},IntPtr.Zero);return result; }
 static IntPtr Manager(int pid){IntPtr result=IntPtr.Zero;EnumWindows(delegate(IntPtr window,IntPtr unused){uint actual;GetWindowThreadProcessId(window,out actual);if(actual==pid&&Caption(window)=="PlayExitTests")result=window;return true;},IntPtr.Zero);return result;}
 static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
 static void Answer(MainForm main,string answer) {
  using(var timer=new System.Windows.Forms.Timer{Interval=40}) {
   bool shown=false;timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().OfType<PlayExitDialog>().FirstOrDefault();if(dialog==null)return;shown=true;timer.Stop();NextUi.Descendants(dialog).OfType<Button>().Single(b=>b.Text==answer).PerformClick();};
   timer.Start();main.Close();Check(shown,"actual MainForm closing asks before touching a running game (answer="+answer+", checks="+checks+", disposed="+main.IsDisposed+")");
  }
 }
 [STAThread]static int Main(string[] args) {
  if(args.Contains("--child")) { Application.EnableVisualStyles();using(var manager=new Form{Text="PlayExitTests",Bounds=new Rectangle(-3500,-3500,1100,800),StartPosition=FormStartPosition.Manual})using(var form=new Form{Text="Game fixture",Bounds=new Rectangle(-3500,-3500,640,480),StartPosition=FormStartPosition.Manual}) {
   manager.Show();
   form.Menu=new MainMenu(new[]{new MenuItem("Emulator",new[]{new MenuItem("Settings")})});
   form.Controls.Add(new Label{Text="Game surface",Dock=DockStyle.Fill});
   if(args.Contains("--refuse"))form.FormClosing+=delegate(object sender,FormClosingEventArgs e){e.Cancel=true;};
   Application.Run(form);
  }return 0; }
  try { Application.EnableVisualStyles();Run(false);Run(true);Console.WriteLine("PASS: "+checks+" real native exit confirmation, graceful shutdown and game-view checks.");return 0; }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 static void Run(bool refuses) {
  string executable=Assembly.GetExecutingAssembly().Location;
  using(var process=Process.Start(new ProcessStartInfo(executable,"--child"+(refuses?" --refuse":"")){UseShellExecute=false,CreateNoWindow=true})) {
   long started=process.StartTime.ToUniversalTime().Ticks;IntPtr window=IntPtr.Zero;
   try {
    Check(Until(delegate{window=Find(process.Id);return window!=IntPtr.Zero;}),"fixture creates its own window");IntPtr menu=GetMenu(window);Check(menu!=IntPtr.Zero,"fixture has a real native emulator menu");
    using(var main=new MainForm(true)) {
     main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Pump(100);
     var data=(LibraryData)Field(main,"library");data.Emulators=new List<EmulatorProfile>{new EmulatorProfile{Id="view",Name="Fixture",Executable=executable,PlayView=new PlayViewSettings{Top=30,Bottom=20}}};
     typeof(MainForm).GetMethod("ShowPlay",Private).Invoke(main,new object[]{process,"Game fixture",new List<SessionProcess>()});
     var play=(PlaySurface)Field(main,"playSurface");Check(Until(delegate{return NextUi.Descendants(play).OfType<PlayWindowHost>().Any(h=>h.IsAttached);}),"game view attaches verified native renderer");
     var host=NextUi.Descendants(play).OfType<PlayWindowHost>().Single(h=>h.IsAttached);
     IntPtr manager=Manager(process.Id);Check(manager!=IntPtr.Zero&&host.GameWindow==window,"game caption wins over the larger emulator frontend");
     Check(Until(delegate{return !IsWindowVisible(manager);}),"verified sibling emulator frontend is hidden while its game stays in Play");
     Check(GetMenu(window)==IntPtr.Zero,"game-only view hides standard native emulator menu");
     Rect game,viewport;GetWindowRect(window,out game);GetWindowRect(host.Handle,out viewport);
     Check(game.Top<viewport.Top&&game.Bottom>viewport.Bottom,"saved game view hides toolbar and status edges within viewport");
     host.ConfigureView(new PlayViewSettings{ShowControls=true});Pump(100);
     Check(host.IsAttached&&host.IsAnchored&&GetMenu(window)==menu,"showing emulator controls restores its native menu without losing the game");
     host.ConfigureView(new PlayViewSettings{Top=30,Bottom=20});Pump(100);
     Check(host.IsAttached&&GetMenu(window)==IntPtr.Zero,"returning to game-only view hides the original native menu safely");
     Check(play.OpenSelectedInWindow()&&Until(delegate{return IsWindowVisible(manager);}),"external mode restores the original sibling emulator frontend");
     NextUi.Descendants(play).OfType<Button>().Single(b=>b.Text=="Show in Play").PerformClick();
     Check(Until(delegate{return host.IsAttached&&!IsWindowVisible(manager);}),"returning to Play selects the game and hides its frontend again");
     Answer(main,"Cancel");Check(!main.IsDisposed&&!process.HasExited&&host.IsAttached,"Cancel retains FishBowl and the exact running renderer");
     if(refuses) {
      Answer(main,"Close games and exit");Check(!main.IsDisposed&&!process.HasExited,"emulator save/exit veto prevents FishBowl shutdown without force killing");
      Check(GetParent(window)==IntPtr.Zero&&GetMenu(window)==menu,"exit veto leaves restored native menus and window available");
      Answer(main,"Keep games running");Check(main.IsDisposed&&!process.HasExited,"explicit keep-running choice detaches and closes FishBowl");
     } else { Answer(main,"Close games and exit");Check(main.IsDisposed&&process.WaitForExit(2500),"confirmed close exits both FishBowl and its emulator normally: disposed="+main.IsDisposed+" exited="+process.HasExited+" enabled="+IsWindowEnabled(window)+" visible="+IsWindowVisible(window)+" owner="+GetWindow(window,4)+" parent="+GetParent(window)+" status="+string.Join(" | ",NextUi.Descendants(play).OfType<Label>().Select(l=>l.Text))); }
    }
    var copy=Json.Deserialize<EmulatorProfile>(Json.Serialize(new EmulatorProfile{PlayView=new PlayViewSettings{Top=30,Bottom=20,Left=5,Right=7}}));
    Check(copy.PlayView.Top==30&&copy.PlayView.Bottom==20&&copy.PlayView.Left==5&&copy.PlayView.Right==7,"view settings survive saved-data serialization");
   } finally {
    if(!process.HasExited&&process.StartTime.ToUniversalTime().Ticks==started) { uint pid;GetWindowThreadProcessId(window,out pid);if(pid==process.Id)PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);if(!process.WaitForExit(1000)){process.Kill();process.WaitForExit();} }
   }
  }
 }
}
