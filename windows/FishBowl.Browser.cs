using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EmulatorHub {
 public static class FishBowlWeb {
  public static readonly string[] Engines={"Firefox","Microsoft Edge","Google Chrome"};
  public static BrowserPreferences Settings(LibraryData library) {
   if(library.Browser==null)library.Browser=new BrowserPreferences();
   if(!Engines.Contains(library.Browser.Engine))library.Browser.Engine="Firefox";
   if(string.IsNullOrWhiteSpace(library.Browser.HomePage))library.Browser.HomePage="about:blank";
   return library.Browser;
  }
  public static string Address(string input) {
   string text=(input??"").Trim();if(text=="about:blank")return text;
   Uri uri;
   if(Uri.TryCreate(text,UriKind.Absolute,out uri)) {
    if(uri.Scheme!=Uri.UriSchemeHttp&&uri.Scheme!=Uri.UriSchemeHttps)throw new InvalidDataException("Enter an http or https address, or search words.");
    if(string.IsNullOrEmpty(uri.Host)||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("Use an address without embedded sign-in details.");
    return uri.AbsoluteUri;
   }
   if(text.Length==0)return "about:blank";
   if(!text.Any(char.IsWhiteSpace)&&text.Contains(".")&&Uri.TryCreate("https://"+text,UriKind.Absolute,out uri)&&!string.IsNullOrEmpty(uri.Host))return uri.AbsoluteUri;
   return "https://duckduckgo.com/?q="+Uri.EscapeDataString(text);
  }
  public static string Resolve(BrowserPreferences settings) {
   if(!string.IsNullOrWhiteSpace(settings.Executable)) {
    string selected=Path.GetFullPath(settings.Executable);
    if(!File.Exists(selected)||!selected.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new FileNotFoundException("Choose the installed browser executable in Browser settings.");
    return selected;
   }
   string name=settings.Engine=="Firefox"?"firefox.exe":settings.Engine=="Microsoft Edge"?"msedge.exe":"chrome.exe";
   foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
    try { using(var root=RegistryKey.OpenBaseKey(hive,view))using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+name)) {
     string path=key==null?null:key.GetValue(null) as string;if(!string.IsNullOrWhiteSpace(path)&&File.Exists(path.Trim('"')))return Path.GetFullPath(path.Trim('"'));
    }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
   }
   string suffix=settings.Engine=="Firefox"?@"Mozilla Firefox\firefox.exe":settings.Engine=="Microsoft Edge"?@"Microsoft\Edge\Application\msedge.exe":@"Google\Chrome\Application\chrome.exe";
   foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}) {
    string path=Path.Combine(root,suffix);if(File.Exists(path))return path;
   }
   throw new FileNotFoundException(settings.Engine+" is not installed or could not be found. Open Browser settings to choose its program or change browser.");
  }
  static string Quote(string value) { return "\""+value.Replace("\"","\\\"")+"\""; }
  public static ProcessStartInfo StartInfo(BrowserPreferences settings,string profile,string url,bool existing) {
   string address=Address(url);string executable=Resolve(settings);
   string arguments=settings.Engine=="Firefox" ? (existing?"--profile "+Quote(profile)+" --new-tab "+Quote(address):"--new-instance --profile "+Quote(profile)+" --new-window "+Quote(address)) : "--user-data-dir="+Quote(profile)+" --no-first-run --no-default-browser-check --app="+Quote(address);
   return new ProcessStartInfo(executable,arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(executable)};
  }
  public static void OpenExternal(LibraryData library,string url) { var settings=Settings(library);Process.Start(new ProcessStartInfo(Resolve(settings),Quote(Address(url))){UseShellExecute=false}); }
 }

 public sealed class BrowserSettingsDialog : NextDialog {
  public BrowserSettingsDialog(LibraryData library) : base("Browser settings",780,620) {
   var settings=FishBowlWeb.Settings(library);var fields=NextDialog.Fields(Body);
   var engine=NextDialog.Choice(FishBowlWeb.Engines,settings.Engine);var executable=new TextBox{Text=settings.Executable??"",Dock=DockStyle.Fill};
   var homepage=new TextBox{Text=settings.HomePage,Dock=DockStyle.Fill};
   NextDialog.Field(fields,"Browser",engine);NextDialog.Field(fields,"Browser program (optional)",executable);
   NextDialog.Field(fields,"",ExperienceUi.Button("Browse",delegate{using(var picker=new OpenFileDialog{Title="Choose the browser program",Filter="Browser programs (*.exe)|*.exe"})if(picker.ShowDialog(this)==DialogResult.OK)executable.Text=picker.FileName;}));
   engine.SelectedIndexChanged+=delegate{executable.Text="";};
   NextDialog.Field(fields,"Home page",homepage);
   NextDialog.Field(fields,"",ExperienceUi.Label("Firefox is the default. FishBowl uses the installed browser in its Web workspace with a separate profile for cookies and logins. Close the current Web session before changing browser. Your personal browser windows are not attached or closed. A browser that cannot be hosted can open in its own window.",140),170);
   Action("Save",delegate {
    try { string page=FishBowlWeb.Address(homepage.Text);var value=new BrowserPreferences{Engine=engine.Text,Executable=executable.Text.Trim(),HomePage=page};
     if(value.Executable.Length>0)FishBowlWeb.Resolve(value);
     settings.Engine=value.Engine;settings.Executable=value.Executable;settings.HomePage=value.HomePage;Store.Save(library);DialogResult=DialogResult.OK;Close();
    }catch(Exception error){MessageBox.Show(this,error.Message,"Browser settings");}
   });Action("Cancel",Close);
  }
 }

 public sealed class BrowserSurface : UserControl {
  readonly LibraryData library;readonly PlayWindowHost host;readonly TextBox address;readonly Label status,empty;
  readonly Button back,forward,reload,stop,external,show,full;readonly Timer timer;readonly Action fullscreen;
  readonly List<SessionProcess> known=new List<SessionProcess>();readonly Dictionary<string,Process> handles=new Dictionary<string,Process>();
  HashSet<string> before=new HashSet<string>();Task<List<SessionProcess>> snapshot;
  BrowserPreferences active;string profile;DateTime started;bool inWindow,closing,disposing,fullScreen,leaveRunning;
  public bool HasRunningBrowser {get{return known.Any(PlayNative.IsAlive);}}
  public BrowserSurface(LibraryData data,Action toggleFullscreen) {
   library=data;fullscreen=toggleFullscreen;Dock=DockStyle.Fill;BackColor=FishBowlPalette.ThemeSurface;
   var bar=ExperienceUi.Bar();bar.Dock=DockStyle.Top;bar.Tag="FishBowl overflow";
   address=new TextBox{Width=330,Text=FishBowlWeb.Settings(library).HomePage,AccessibleName="Web address or search"};
   address.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Navigate(address.Text);}};
   bar.Controls.Add(address);bar.Controls.Add(ExperienceUi.Button("Go",delegate{Navigate(address.Text);}));
   back=ExperienceUi.Button("Back",delegate{Command(1);});forward=ExperienceUi.Button("Forward",delegate{Command(2);});reload=ExperienceUi.Button("Reload",delegate{Command(3);});
   stop=ExperienceUi.Button("Close browser",delegate{if(!RequestClose())status.Text="Finish the browser's save or exit prompt, then close it again.";});
   external=ExperienceUi.Button("Open in window",delegate{if(host.TryDetach()){inWindow=true;UpdateState();}});
   show=ExperienceUi.Button("Show in app",delegate{inWindow=false;timer.Start();UpdateState();});
   full=ExperienceUi.Button("Full screen",delegate{if(fullscreen!=null)fullscreen();});
   bar.Controls.AddRange(new Control[]{back,forward,reload,external,show,full,stop,ExperienceUi.Button("Browser settings",delegate{using(var dialog=new BrowserSettingsDialog(library))if(dialog.ShowDialog(FindForm())==DialogResult.OK){if(!HasRunningBrowser)address.Text=FishBowlWeb.Settings(library).HomePage;else status.Text="Browser settings saved. Close this Web session and press Go to use the new browser.";}})});
   host=new PlayWindowHost{PreserveRendererWindow=true};empty=ExperienceUi.Label("Browse inside FishBowl. Firefox is the default; Browser settings lets you choose another installed browser. Enter a web address or search, then press Go.",130);empty.Dock=DockStyle.Top;empty.Padding=new Padding(16);host.Controls.Add(empty);
   status=ExperienceUi.Label("Web is ready. No browser runs until you press Go.",80);status.Dock=DockStyle.Bottom;status.AutoSize=true;
   Controls.Add(host);Controls.Add(status);Controls.Add(bar);timer=new Timer{Interval=150};timer.Tick+=Tick;UpdateState();
  }
  protected override void OnLayout(LayoutEventArgs e){if(status!=null)status.MaximumSize=new Size(Math.Max(1,ClientSize.Width),0);if(empty!=null)empty.MaximumSize=new Size(Math.Max(1,ClientSize.Width),0);base.OnLayout(e);}
  public void Navigate(string input) {
   try {
    string url=FishBowlWeb.Address(input);bool running=HasRunningBrowser;
    if(running && active.Engine!="Firefox") {
     if(!RequestClose())throw new IOException("Finish the browser's save or exit prompt before opening another address.");
     running=false;
    }
    if(!running) {
     Release();var settings=FishBowlWeb.Settings(library);active=new BrowserPreferences{Engine=settings.Engine,Executable=settings.Executable,HomePage=settings.HomePage};
     profile=Path.Combine(Store.DataDirectory,"BrowserProfiles",active.Engine.Replace(" ",""));Directory.CreateDirectory(profile);
     before=new HashSet<string>(SessionLedger.ProcessSnapshot().Select(p=>p.Key));started=DateTime.UtcNow;inWindow=false;
    }
    var process=Process.Start(FishBowlWeb.StartInfo(active,profile,url,running));if(process==null)throw new IOException("The browser did not start.");
    try {
     var identity=new SessionProcess{Pid=process.Id,StartedUtcTicks=process.StartTime.ToUniversalTime().Ticks};
     if(!before.Contains(identity.Key)&&!known.Any(p=>p.Key==identity.Key)) { known.Add(identity);handles.Add(identity.Key,process);process=null; }
    }finally{if(process!=null)process.Dispose();}
    address.Text=url;timer.Start();timer.Interval=150;UpdateState();
   }catch(Exception error){status.Text=error.Message;Store.Log("Web: "+error.Message);}
  }
  void Tick(object sender,EventArgs e) {
   if(disposing||closing)return;if(host.IsAnchored)host.RefreshPlacement();
   if(snapshot==null){snapshot=Task.Factory.StartNew<List<SessionProcess>>(SessionLedger.ProcessSnapshot);return;}
   if(!snapshot.IsCompleted)return;if(snapshot.IsFaulted){snapshot=null;return;}
   var live=snapshot.Result;snapshot=null;var keys=new HashSet<string>(live.Select(p=>p.Key));
   foreach(var identity in known) { Process process;if(handles.TryGetValue(identity.Key,out process))try{if(process.HasExited)identity.EndedUtcTicks=process.ExitTime.ToUniversalTime().Ticks;}catch{} }
   bool added;
   do { added=false;foreach(var candidate in live) {
    if(known.Count>=128||before.Contains(candidate.Key)||known.Any(p=>p.Key==candidate.Key))continue;
    var parent=known.FirstOrDefault(p=>p.Pid==candidate.ParentPid&&candidate.StartedUtcTicks>=p.StartedUtcTicks&&(keys.Contains(p.Key)||p.EndedUtcTicks>0&&candidate.StartedUtcTicks<=p.EndedUtcTicks));if(parent==null)continue;
    try { var process=Process.GetProcessById(candidate.Pid);if(process.StartTime.ToUniversalTime().Ticks!=candidate.StartedUtcTicks){process.Dispose();continue;}known.Add(candidate);handles.Add(candidate.Key,process);added=true; }catch{}
   }}while(added);
   if(!HasRunningBrowser&&(DateTime.UtcNow-started).TotalSeconds>3){Release();timer.Stop();UpdateState();return;}
   if(!inWindow&&!host.IsAttached){IntPtr window=PlayNative.Find(known);uint pid;PlayNative.GetWindowThreadProcessId(window,out pid);var identity=known.FirstOrDefault(p=>p.Pid==pid&&keys.Contains(p.Key));if(identity!=null)host.AttachWindow(window,identity.Pid,identity.StartedUtcTicks);}
   timer.Interval=host.IsAttached||inWindow||DateTime.UtcNow-started>TimeSpan.FromSeconds(25)?(Visible?1000:2000):150;UpdateState();
  }
  void Command(int command) {
   IntPtr window=host.GameWindow;if(window==IntPtr.Zero)window=PlayNative.Find(known);
   if(known.Any(p=>PlayNative.Matches(window,p.Pid,p.StartedUtcTicks)))PlayNative.PostMessage(window,0x319,window,new IntPtr(command<<16));
  }
  void UpdateState() {
   bool running=HasRunningBrowser;back.Enabled=forward.Enabled=reload.Enabled=running;stop.Enabled=external.Enabled=running;show.Enabled=running&&(inWindow||!host.IsAttached);
   full.Text=fullScreen?"Exit full screen":"Full screen";empty.Visible=!running;
   if(!running)status.Text="Web is ready. No browser runs until you press Go.";
   else if(inWindow)status.Text=active.Engine+" is open in its own window. Show in app returns it to FishBowl.";
   else if(host.IsAttached)status.Text=active.Engine+" is running inside FishBowl. Switching sections keeps it running.";
   else status.Text=active.Engine+": "+(host.LastError??((DateTime.UtcNow-started).TotalSeconds>25?"No compatible browser window is available. Finish browser startup, then choose Show in app, or Open in window.":"Waiting for its browser window..."));
  }
  public void SetFullscreen(bool value){fullScreen=value;UpdateState();}
  public bool PrepareHandleChange(){return host.PrepareHandleChange();}
  public void ResumeHandleChange(){if(!inWindow)host.ResumeHandleChange();UpdateState();}
  public bool RequestClose() {
   if(!HasRunningBrowser)return true;if(!host.TryDetach())return false;inWindow=true;closing=true;
   try {
    PlayNative.EnumWindows(delegate(IntPtr window,IntPtr unused){if(PlayNative.GetWindow(window,4)==IntPtr.Zero&&PlayNative.IsWindowEnabled(window)&&known.Any(p=>PlayNative.Matches(window,p.Pid,p.StartedUtcTicks)))PlayNative.PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);
    var wait=Stopwatch.StartNew();while(HasRunningBrowser&&wait.ElapsedMilliseconds<1500){Application.DoEvents();System.Threading.Thread.Sleep(20);}
    if(HasRunningBrowser)return false;Release();timer.Stop();UpdateState();return true;
   }finally{closing=false;}
  }
  public bool ConfirmExit(IWin32Window owner) {
   if(RequestClose())return true;
   using(var dialog=new BrowserExitDialog()) {
    if(dialog.ShowDialog(owner)!=DialogResult.Yes)return false;
    if(!host.TryDetach())return false;leaveRunning=true;return true;
   }
  }
  void Release(){if(!host.TryDetach())throw new InvalidOperationException("The browser window could not be restored safely. Close it in the browser, then retry.");foreach(var process in handles.Values)process.Dispose();handles.Clear();known.Clear();snapshot=null;active=null;}
  protected override void Dispose(bool value){if(value&&!disposing){disposing=true;timer.Stop();if(!leaveRunning)RequestClose();timer.Dispose();Release();}base.Dispose(value);}
 }
 public sealed class BrowserExitDialog : NextDialog {
  public BrowserExitDialog() : base("Web browser is still running",720,460) {
   Body.Controls.Add(ExperienceUi.Label("The Web browser is still closing or waiting for a save or exit prompt. Finish that prompt and retry closing FishBowl. You can also leave this browser running in its own window and exit FishBowl.",170));
   Action("Leave browser running and exit",delegate{DialogResult=DialogResult.Yes;Close();});
   Action("Stay in FishBowl",delegate{DialogResult=DialogResult.Cancel;Close();});
   CancelButton=Actions.Controls.OfType<Button>().Last();AcceptButton=Actions.Controls.OfType<Button>().Last();
  }
 }
}
