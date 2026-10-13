using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Runtime.InteropServices;using System.Threading;using System.Windows.Forms;using EmulatorHub;
class BrowserTests {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
 static bool Until(Func<bool> done){var watch=Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<10000)Pump(25);return done();}
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Private).GetValue(value);}
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect bounds);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
 [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
 static bool Fits(IntPtr window,PlayWindowHost host){Rect actual,expected;return GetWindowRect(window,out actual)&&GetWindowRect(host.Handle,out expected)&&actual.Left==expected.Left&&actual.Top==expected.Top&&actual.Right==expected.Right&&actual.Bottom==expected.Bottom;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,ref CopyData data);
 [StructLayout(LayoutKind.Sequential)]struct CopyData {public IntPtr Tag;public int Length;public IntPtr Data;}
 class BrowserFixture:Form {
  readonly string profile;
  readonly TextBox location=new TextBox{Dock=DockStyle.Top};
  public BrowserFixture(string folder){profile=folder;Text="Owned browser fixture";Bounds=new Rectangle(-3500,-3500,700,500);StartPosition=FormStartPosition.Manual;Controls.Add(location);location.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){File.WriteAllText(Path.Combine(profile,"navigation.txt"),location.Text);e.SuppressKeyPress=true;}};}
  protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==(Keys.Control|Keys.L)){location.Focus();location.SelectAll();return true;}return base.ProcessCmdKey(ref message,key);}
  protected override void WndProc(ref Message message){if(message.Msg==0x4a){var data=(CopyData)Marshal.PtrToStructure(message.LParam,typeof(CopyData));File.WriteAllText(Path.Combine(profile,"navigation.txt"),Marshal.PtrToStringUni(data.Data));message.Result=new IntPtr(1);return;}if(message.Msg==0x319)File.AppendAllText(Path.Combine(profile,"commands.txt"),((message.LParam.ToInt64()>>16)&0x7fff)+Environment.NewLine);base.WndProc(ref message);}
 }
 [STAThread]static int Main(string[] args){
  int profileIndex=Array.IndexOf(args,"--profile");
  if(profileIndex>=0){string profile=args[profileIndex+1];Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"arguments.txt"),string.Join(Environment.NewLine,args));if(args.Contains("--new-tab")){File.WriteAllText(Path.Combine(profile,"navigation.txt"),args.Last());return 0;}File.WriteAllText(Path.Combine(profile,"commands.txt"),"");Application.EnableVisualStyles();using(var form=new BrowserFixture(profile))Application.Run(form);return 0;}
  try{Application.EnableVisualStyles();Run(args.Contains("--firefox-smoke"));Console.WriteLine("PASS: "+checks+" browser preferences, URI routing, dedicated process ownership and native lifecycle checks.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 static void Run(bool realFirefox){
  var data=Store.Load();NextData.Ensure(data);data.Browser=null;var settings=FishBowlWeb.Settings(data);Check(settings.Engine=="Firefox"&&settings.HomePage=="about:blank","new libraries default to Firefox without loading a website");
  Check(FishBowlWeb.Address("example.com/path")=="https://example.com/path","bare domain gets https");Check(FishBowlWeb.Address("pokemon x").Contains("q=pokemon%20x"),"search text is encoded");
  foreach(string invalid in new[]{"javascript:alert(1)","file:///C:/private","https://user:secret@example.com/","cmd:calc"}){bool rejected=false;try{FishBowlWeb.Address(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"unsafe address rejected: "+invalid);}
  if(!realFirefox)settings.Executable=Assembly.GetExecutingAssembly().Location;
  string profile=Path.Combine(Store.DataDirectory,"BrowserProfiles","Firefox");var info=FishBowlWeb.StartInfo(settings,profile,"https://example.com",false);
  Check(!info.UseShellExecute&&info.Arguments.Contains("--new-instance")&&info.Arguments.Contains("--profile")&&info.Arguments.Contains("--new-window")&&!info.Arguments.Contains("--kiosk"),"Firefox owns a distinct profile and native instance");
  if(!realFirefox){foreach(string engine in new[]{"Microsoft Edge","Google Chrome"}){var value=new BrowserPreferences{Engine=engine,Executable=settings.Executable};var start=FishBowlWeb.StartInfo(value,profile,"https://example.com",false);Check(start.Arguments.Contains("--user-data-dir=")&&start.Arguments.Contains("--new-window")&&!start.Arguments.Contains("--app="),engine+" uses a dedicated navigable browser profile");}}
  var copy=Json.Deserialize<LibraryData>(Json.Serialize(data));Check(copy.Browser.Engine=="Firefox"&&copy.Browser.HomePage=="about:blank","browser preference survives serialization");
  using(var main=new MainForm(true)){
   main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=realFirefox?new Point(40,40):new Point(-4000,-4000);main.Show();Pump(100);
   var library=Field<LibraryData>(main,"library");library.Browser=settings;
   var tabs=Field<TabControl>(main,"workspaceNavigation");Check(tabs.TabCount==5&&tabs.TabPages[4].Text=="Web","Web joins current workspace navigation");
   var browser=Field<BrowserSurface>(main,"browserSurface");Check(!browser.HasRunningBrowser,"browser does not start on app startup");
   // The CI desktop cannot grant keyboard focus. Exercise surface routing over a fixture-only transport.
   // --firefox-smoke retains the production foreground/keyboard transport for interactive verification.
   if(!realFirefox)typeof(BrowserSurface).GetField("navigateWindow",Private).SetValue(browser,new Func<IntPtr,SessionProcess,string,bool>(delegate(IntPtr target,SessionProcess identity,string url){
    using(var process=Process.GetProcessById(identity.Pid))if(process.StartTime.ToUniversalTime().Ticks!=identity.StartedUtcTicks)return false;
    IntPtr text=Marshal.StringToHGlobalUni(url);try{var packet=new CopyData{Length=(url.Length+1)*2,Data=text};return SendMessage(target,0x4a,IntPtr.Zero,ref packet)==new IntPtr(1);}finally{Marshal.FreeHGlobal(text);}
   }));
   tabs.SelectedIndex=4;browser.Navigate("about:blank");
   var host=NextUi.Descendants(browser).OfType<PlayWindowHost>().Single();Check(Until(delegate{return host.IsAttached;}),"owned browser renderer attaches inside Web: "+string.Join(" | ",NextUi.Descendants(browser).OfType<Label>().Select(l=>l.Text)));
   Check(browser.HasRunningBrowser,"browser process is verified and tracked");Check(host.IsAnchored,"browser keeps its original renderer context in a borderless owned viewport");
   Check(host.MaintainViewportBounds&&Until(delegate{return Fits(host.GameWindow,host);}),"browser defaults to the exact Web viewport without manual adjustment");
   if(!realFirefox) {
    IntPtr fitted=host.GameWindow;
    SetWindowPos(fitted,IntPtr.Zero,-3000,-2900,700,500,0x4014);
    Check(Until(delegate{return Fits(fitted,host);}),"browser-native moves and resizes are corrected by the regular placement check");
    ShowWindowAsync(fitted,3);Pump(100);
    Check(Until(delegate{return !IsZoomed(fitted)&&Fits(fitted,host);}),"browser maximization is restored to the Web viewport automatically");
    main.Location=new Point(-3900,-3900);main.Size=new Size(main.Width+90,main.Height+30);
    Check(Until(delegate{return Fits(fitted,host);}),"browser follows FishBowl movement and resizing");
    int placements=host.PlacementUpdates;Pump(2300);
    Check(host.PlacementUpdates==placements,"unchanged browser bounds do not cause repeated native placement");
   }
   foreach(int page in new[]{0,1,2,3,4}){tabs.SelectedIndex=page;Pump(80);Check(browser.HasRunningBrowser&&host.IsAttached,"browser remains running across workspace "+page);}
   if(!realFirefox){foreach(string command in new[]{"Back","Forward","Reload"}){NextUi.Descendants(browser).OfType<Button>().Single(b=>b.Text==command).PerformClick();Pump(60);}Check(File.ReadAllLines(Path.Combine(profile,"commands.txt")).SequenceEqual(new[]{"1","2","3"}),"native navigation messages reach only the hosted browser window");}
   IntPtr navigationWindow=host.GameWindow;
   browser.Navigate("https://example.com/next?q=a%2Bb");Pump(400);Check(browser.HasRunningBrowser&&host.IsAttached&&host.GameWindow==navigationWindow,"new address keeps the same browser HWND");
   if(!realFirefox)Check(Until(delegate{return File.Exists(Path.Combine(profile,"navigation.txt"))&&File.ReadAllText(Path.Combine(profile,"navigation.txt"))=="https://example.com/next?q=a%2Bb";}),"address is typed into the owned window without a second browser process");
   browser.Navigate("pokemon x");Pump(400);
   if(!realFirefox)Check(File.ReadAllText(Path.Combine(profile,"navigation.txt"))==FishBowlWeb.Address("pokemon x")&&host.GameWindow==navigationWindow,"search updates the same embedded browser");
   if(!realFirefox){var known=Field<List<SessionProcess>>(browser,"known");Check(known.Count(p=>p.EndedUtcTicks==0)==1,"repeated Go creates no second launch process");}
   NextUi.Descendants(browser).OfType<Button>().Single(b=>b.Text=="Open in window").PerformClick();Pump(100);Check(!host.IsAttached&&browser.HasRunningBrowser,"external mode retains browser process");
   Check(Until(delegate{return Fits(navigationWindow,host);}),"external browser starts sized and positioned over the Web viewport");
   if(!realFirefox){SetWindowPos(navigationWindow,IntPtr.Zero,-3100,-3000,730,510,0x4014);Pump(2200);Rect moved;GetWindowRect(navigationWindow,out moved);Check(moved.Left==-3100&&moved.Top==-3000&&moved.Right-moved.Left==730,"external positioning stays under user control after the default fit");}
   var returnButton=NextUi.Descendants(browser).OfType<Button>().Single(b=>b.Text=="Return to Web");Check(returnButton.Enabled,"external window button offers an enabled return action");returnButton.PerformClick();Check(Until(delegate{return host.IsAttached&&host.GameWindow==navigationWindow;}),"same verified browser returns inside FishBowl");
   IntPtr originalWindow=host.GameWindow;NextUi.Descendants(browser).OfType<Button>().Single(b=>b.Text=="Full screen").PerformClick();Pump(150);Check(host.IsAnchored&&host.GameWindow==originalWindow,"fullscreen keeps the same browser renderer in the owned viewport");NextUi.Descendants(browser).OfType<Button>().Single(b=>b.Text=="Exit full screen").PerformClick();Pump(150);Check(host.IsAnchored&&host.GameWindow==originalWindow,"fullscreen exit restores the same hosted browser");
   Check(browser.RequestClose(),"dedicated browser closes normally");Check(!browser.HasRunningBrowser,"closed browser is released without affecting personal browser instances");main.Close();
  }
 }
}
