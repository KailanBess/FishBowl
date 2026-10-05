using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
namespace EmulatorHub {
 public class PersonalGame {
  public string Id {get;set;} public bool Favorite {get;set;} public bool Pinned {get;set;}
  public string PlayStatus {get;set;} public int PersonalRating {get;set;} public long TotalPlaySeconds {get;set;}
  public int LaunchCount {get;set;} public string LastLaunched {get;set;}
 }
 public class BowlUser {
  public Dictionary<string,LibraryNavigation> Views {get;set;} public LibraryNavigation Navigation {get;set;} public List<SaveRouting> SaveRoutes {get;set;}
  public string Id {get;set;} public string Name {get;set;} public List<PersonalGame> Games {get;set;}
  public ThemeSettings Theme {get;set;} public CosmeticSettings Cosmetics {get;set;}
  public NextSettings Enhancements {get;set;} public ExperienceSettings Experience {get;set;} public List<PlaySession> Sessions {get;set;}
  public List<string> Queue {get;set;} public List<SmartLibraryList> Lists {get;set;}
  public int WeeklyMinutes {get;set;} public int BreakMinutes {get;set;} public bool Controller {get;set;}
 }
 public class UserToolSettings {
  public Dictionary<string,LibraryNavigation> Views {get;set;} public LibraryNavigation Navigation {get;set;} public List<SaveRouting> SaveRoutes {get;set;}
  public string ActiveId {get;set;} public List<BowlUser> Users {get;set;}
  public int WeeklyMinutes {get;set;} public int BreakMinutes {get;set;} public bool Controller {get;set;}
  public List<BulkUndoRecord> Undo {get;set;}
 }
 public class BulkUndoRecord {
  public string At {get;set;} public string UserId {get;set;}
  public List<GameEntry> Before {get;set;} public List<GameEntry> After {get;set;}
  public List<GameCollection> CollectionsBefore {get;set;} public List<GameCollection> CollectionsAfter {get;set;}
 }
 public static class UserTools {
  public static bool Guest {get;private set;} public static int ActiveLaunches;
  public static readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> ActiveSessions=new System.Collections.Concurrent.ConcurrentDictionary<string,byte>();
  static readonly string[] BulkFields={"PreferredEmulatorId","EmulatorId","PlayStatus","Favorite","Pinned","Tags","ArtworkPath"};
  public static T Copy<T>(T value){return Json.Deserialize<T>(Json.Serialize(value));}
  public static UserToolSettings Ensure(LibraryData d) {
   ExperienceData.Ensure(d);
   if(d.UserTools==null)d.UserTools=new UserToolSettings(); var s=d.UserTools;
   if(s.Users==null)s.Users=new List<BowlUser>(); if(s.Undo==null)s.Undo=new List<BulkUndoRecord>();
   if(s.Users.Count==0){var u=Capture(d,"Default");s.Users.Add(u);s.ActiveId=u.Id;}
   return s;
  }
  public static BowlUser Capture(LibraryData d,string name) {
   var s=d.UserTools??new UserToolSettings();
   return new BowlUser {Id=Guid.NewGuid().ToString("N"),Name=name,Games=d.Games.Select(g=>new PersonalGame{Id=g.Id,Favorite=g.Favorite,Pinned=g.Pinned,PlayStatus=g.PlayStatus,PersonalRating=g.PersonalRating,TotalPlaySeconds=g.TotalPlaySeconds,LaunchCount=g.LaunchCount,LastLaunched=g.LastLaunched}).ToList(),Theme=Copy(d.Theme),Cosmetics=Copy(d.Cosmetics),Enhancements=Copy(d.Enhancements),Experience=Copy(d.Experience),Sessions=Copy(d.PlaySessions),Queue=Copy(d.PlayQueue),Lists=Copy(d.SmartLists),WeeklyMinutes=s.WeeklyMinutes,BreakMinutes=s.BreakMinutes,Controller=s.Controller,Views=Copy(s.Views),Navigation=Copy(s.Navigation),SaveRoutes=Copy(s.SaveRoutes)};
  }
  public static void SaveActive(LibraryData d) {
   var s=Ensure(d);var u=s.Users.FirstOrDefault(x=>x.Id==s.ActiveId);if(u==null)return;
   var c=Capture(d,u.Name);c.Id=u.Id;s.Users[s.Users.IndexOf(u)]=c;
  }
  public static BowlUser Create(LibraryData d,string name) {
   var s=Ensure(d);name=(name??"").Trim();if(name.Length==0||name.Length>60||s.Users.Any(existing=>string.Equals(existing.Name,name,StringComparison.OrdinalIgnoreCase)))throw new IOException("Use a unique profile name of 1–60 characters.");
   var u=Capture(d,name);u.Games=new List<PersonalGame>();u.Sessions=new List<PlaySession>();u.Queue=new List<string>();u.Lists=new List<SmartLibraryList>();u.WeeklyMinutes=0;u.BreakMinutes=0;u.Navigation=null;u.Views=null;u.SaveRoutes=new List<SaveRouting>();s.Users.Add(u);return u;
  }
  public static void Switch(LibraryData d,string id) {
   if(Guest||ActiveLaunches>0||WorkGate.Busy>0)throw new IOException("Finish launched emulator sessions before changing profiles.");
   var s=Ensure(d);var u=s.Users.FirstOrDefault(x=>x.Id==id);if(u==null)throw new IOException("Profile unavailable.");if(id==s.ActiveId)return;
   SaveActive(d);u=s.Users.First(x=>x.Id==id);
   var personal=(u.Games??new List<PersonalGame>()).GroupBy(p=>p.Id).ToDictionary(group=>group.Key,group=>group.First());foreach(var g in d.Games){PersonalGame p;personal.TryGetValue(g.Id,out p);g.Favorite=p!=null&&p.Favorite;g.Pinned=p!=null&&p.Pinned;g.PlayStatus=p==null?"Not started":p.PlayStatus;g.PersonalRating=p==null?0:p.PersonalRating;g.TotalPlaySeconds=p==null?0:p.TotalPlaySeconds;g.LaunchCount=p==null?0:p.LaunchCount;g.LastLaunched=p==null?null:p.LastLaunched;}
   d.Theme=Copy(u.Theme);d.Cosmetics=Copy(u.Cosmetics);d.Enhancements=Copy(u.Enhancements);d.Experience=Copy(u.Experience);d.PlaySessions=Copy(u.Sessions)??new List<PlaySession>();d.PlayQueue=Copy(u.Queue)??new List<string>();d.SmartLists=Copy(u.Lists)??new List<SmartLibraryList>();s.WeeklyMinutes=u.WeeklyMinutes;s.BreakMinutes=u.BreakMinutes;s.Controller=u.Controller;s.Navigation=Copy(u.Navigation);s.Views=Copy(u.Views);s.SaveRoutes=Copy(u.SaveRoutes);s.ActiveId=id;ExperienceData.Ensure(d);NextData.Ensure(d);
  }
  static List<GameEntry> BulkSnapshot(List<GameEntry> games){return games.Select(g=>new GameEntry{Id=g.Id,PreferredEmulatorId=g.PreferredEmulatorId,EmulatorId=g.EmulatorId,PlayStatus=g.PlayStatus,Favorite=g.Favorite,Pinned=g.Pinned,Tags=g.Tags==null?null:new List<string>(g.Tags),ArtworkPath=g.ArtworkPath}).ToList();}
  public static BulkUndoRecord BeforeBulk(LibraryData d,List<GameEntry> games) {return new BulkUndoRecord{At=DateTime.Now.ToString("g"),UserId=Ensure(d).ActiveId,Before=BulkSnapshot(games),CollectionsBefore=Copy(d.Collections)};}
  public static void AfterBulk(LibraryData d,BulkUndoRecord r,List<GameEntry> games) {r.After=BulkSnapshot(games);r.CollectionsAfter=Copy(d.Collections);var s=Ensure(d);s.Undo.Add(r);while(s.Undo.Count>10)s.Undo.RemoveAt(0);}
  static bool Equal(object a,object b){return Json.Serialize(a)==Json.Serialize(b);}
  public static string UndoBulk(LibraryData d) {
   var s=Ensure(d);var r=s.Undo.LastOrDefault(x=>x.UserId==s.ActiveId);if(r==null)return "No bulk edits to undo for this profile.";
   var changes=new List<Action>();int conflicts=0;
   foreach(var before in r.Before){var after=r.After.FirstOrDefault(x=>x.Id==before.Id);var live=d.Games.FirstOrDefault(x=>x.Id==before.Id);if(live==null||after==null){conflicts++;continue;}foreach(var name in BulkFields){var p=typeof(GameEntry).GetProperty(name);var a=p.GetValue(before,null);var b=p.GetValue(after,null);if(Equal(a,b))continue;if(!Equal(p.GetValue(live,null),b)){conflicts++;continue;}var target=live;var prop=p;var value=a;changes.Add(()=>prop.SetValue(target,value,null));}}
   foreach(var a in r.CollectionsAfter??new List<GameCollection>()){var b=(r.CollectionsBefore??new List<GameCollection>()).FirstOrDefault(x=>x.Id==a.Id);var live=d.Collections.FirstOrDefault(x=>x.Id==a.Id);var added=(a.GameIds??new List<string>()).Except(b==null?new List<string>():b.GameIds??new List<string>()).ToList();if(added.Count>0&&(live==null||!Equal(live.GameIds,a.GameIds))){conflicts++;continue;}foreach(var id in added){if(live!=null&&live.GameIds!=null){var list=live.GameIds;var key=id;changes.Add(()=>list.Remove(key));}}}
   if(conflicts>0)return "Undo stopped: "+conflicts+" fields changed again or games were removed. No changes made.";
   foreach(var action in changes)action();s.Undo.Remove(r);Store.Save(d);return "Undid bulk edit from "+r.At+". Unrelated edits were preserved.";
  }
  public static string Diagnose(LibraryData d,GameEntry g) {
   if(g==null)return "Select a game first.";var b=new StringBuilder();var e=NextData.LaunchEmulator(d,g);string path=NextData.LaunchPath(g);
   b.AppendLine("Game: "+g.Title);b.AppendLine(File.Exists(path)?"✓ Game file exists.":"Game file is missing. Use Library tools → Repair game paths, or Edit game to choose its location.");
   if(e==null)b.AppendLine("Assign an emulator in Edit game.");else {b.AppendLine(File.Exists(e.Executable)?"✓ Emulator executable exists.":"Emulator executable is missing. Edit its profile and choose the installed executable.");b.AppendLine("Try opening the emulator directly and loading this game. If that fails too, check the emulator's supported formats, BIOS requirements and log.");}
   try{b.AppendLine("Launch validation: "+GameSessions.Validate(d,g));b.AppendLine("FishBowl's paths and arguments pass validation. If the emulator exits early, check its log and per-game settings.");}catch(Exception ex){b.AppendLine("Launch validation: "+ex.Message);b.AppendLine("Review emulator arguments, selected build, launch profile and working folder. Close any existing emulator instance, then retry.");}
   b.AppendLine("This check does not change or download files.");return b.ToString();
  }
  public static long WeekSeconds(LibraryData d,DateTime utc) {
   long total=0;foreach(var p in d.PlaySessions??new List<PlaySession>()){DateTime start;if(!DateTime.TryParse(p.StartedAt,null,DateTimeStyles.RoundtripKind,out start)||start.ToUniversalTime()<utc.AddDays(-7)||start.ToUniversalTime()>utc)continue;total+=Math.Max(0,p.Seconds);}return total;
  }
  public static string Html(IEnumerable<GameEntry> games,bool paths,bool history) {return Html(games,paths,history,false);}
  public static string Html(IEnumerable<GameEntry> games,bool paths,bool history,bool artwork) {return Html(games,paths,history,artwork,CancellationToken.None);}
  public static string Html(IEnumerable<GameEntry> games,bool paths,bool history,bool artwork,CancellationToken token) {
   var b=new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>FishBowl catalog</title><style>body{font:16px system-ui;background:#182332;color:#eef3fa;margin:24px}main{max-width:900px;margin:auto}article{background:#253348;border-radius:12px;padding:16px;margin:12px 0}h2{margin-top:0}p{overflow-wrap:anywhere}input{font:inherit;padding:12px;width:90%;background:#fff;color:#172334}small{color:#c8d6e6}</style><main><h1>Game catalog</h1><label>Filter games <input id='filter' type='search'></label>");
   foreach(var g in games.OrderBy(x=>x.Title)){token.ThrowIfCancellationRequested();if(b.Length>50*1024*1024)throw new IOException("Catalog exceeds 50 MB. Export fewer games or turn off artwork.");b.Append("<article>");if(artwork){string art=CatalogArt(g.ArtworkPath);if(art!=null)b.Append("<img alt='' width='128' height='128' style='object-fit:contain;float:right' src='data:image/png;base64,"+art+"'>");}b.Append("<h2>"+Escape(g.Title)+"</h2><p>"+Escape(g.Genre)+" · "+Escape(g.ConsoleLabel)+"</p><p>"+Escape(g.Description)+"</p><small>"+Escape(string.Join(", ",g.Tags??new List<string>()))+"</small>");if(g.Extras!=null&&Hub.Https(g.Extras.MetadataSource))b.Append("<p><a href=\""+Escape(g.Extras.MetadataSource)+"\">Metadata source and licensing</a></p>");if(paths)b.Append("<p>File: "+Escape(g.Path)+"</p>");if(history)b.Append("<p>"+Escape(g.PlayStatus)+" · "+g.LaunchCount+" launches · "+(g.TotalPlaySeconds/60)+" minutes · Last played: "+Escape(g.LastLaunched)+"</p>");b.Append("</article>");}
   return b.Append("</main><script>document.getElementById('filter').addEventListener('input',function(){var q=this.value.toLowerCase();document.querySelectorAll('article').forEach(function(a){a.hidden=a.textContent.toLowerCase().indexOf(q)<0;});});</script></html>").ToString();
  }
  static string CatalogArt(string path){try{if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||new FileInfo(path).Length>10*1024*1024)return null;using(var source=Image.FromFile(path)){if(source.Width>12000||source.Height>12000)return null;using(var thumb=new Bitmap(128,128))using(var gr=Graphics.FromImage(thumb))using(var stream=new MemoryStream()){gr.Clear(Color.Transparent);double scale=Math.Min(128.0/source.Width,128.0/source.Height);gr.DrawImage(source,(128-(int)(source.Width*scale))/2,(128-(int)(source.Height*scale))/2,(int)(source.Width*scale),(int)(source.Height*scale));thumb.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToBase64String(stream.ToArray());}}}catch{return null;}}
  public static string Escape(string s){return (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;").Replace("'","&#39;");}
  public static void Report(IWin32Window owner,string title,string text) {using(var f=new NextDialog(title)){f.Body.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Text=text,WordWrap=true});f.Action("Close",f.Close);f.ShowDialog(owner);}}
  public static void Troubleshoot(IWin32Window owner,LibraryData d,GameEntry game,string error){using(var f=new NextDialog("Launch troubleshooting")){GameEntry g=game;var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=(error??"")+"\r\n\r\n"+Diagnose(d,g)};f.Body.Controls.Add(text);if(!Guest){f.Action("Game path and assignment",()=>{using(var edit=new GameDialog(g,d.Emulators)){if(edit.ShowDialog(f)==DialogResult.OK){int at=d.Games.FindIndex(x=>x.Id==g.Id);if(at>=0)d.Games[at]=edit.Game;g=edit.Game;Store.Save(d);text.Text=Diagnose(d,g);}}});f.Action("Emulator settings",()=>{var e=ExperienceData.Emulator(d,g);if(e==null)throw new IOException("Assign an emulator in Game path and assignment first.");using(var edit=new EmulatorDialog(e)){if(edit.ShowDialog(f)==DialogResult.OK){edit.Profile.Id=e.Id;d.Emulators[d.Emulators.IndexOf(e)]=edit.Profile;Store.Save(d);text.Text=Diagnose(d,g);}}});}f.Action("Recheck",()=>text.Text=Diagnose(d,g));f.Action("Close",f.Close);f.ShowDialog(owner);}}
  static void Add(Control parent,string text,Action action){parent.Controls.Add(ExperienceUi.Button(text,action));}
  public static void Show(IWin32Window owner,LibraryData d,Action refresh) {
   NextData.Ensure(d);LibraryAdditions.Ensure(d);var s=Ensure(d);
   using(var f=new NextDialog("User tools",860,600)) {
    var tabs=new FishBowlTabs{Dock=DockStyle.Fill,PreferredColumns=3};f.Body.Controls.Add(tabs);
    var personal=new TabPage("Personal"){AutoScroll=true};var checks=new TabPage("Check and repair"){AutoScroll=true};var share=new TabPage("Share"){AutoScroll=true};tabs.TabPages.AddRange(new[]{personal,checks,share});
    var fields=NextDialog.Fields(personal);var users=NextDialog.Choice(s.Users.Select(x=>x.Name),s.Users.First(x=>x.Id==s.ActiveId).Name);NextDialog.Field(fields,"Active user profile",users);
    var profileActions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};NextDialog.Field(fields,"Profiles (shared games / saves)",profileActions,92);
    Add(profileActions,"Switch",()=>{Switch(d,s.Users.First(x=>x.Name==users.Text).Id);Store.Save(d);refresh();f.Close();});
    Add(profileActions,"New profile",()=>{using(var n=new NextDialog("New user profile",660,430)){var t=new TextBox();NextDialog.Field(NextDialog.Fields(n.Body),"Name",t);n.Action("Create",()=>{var u=Create(d,t.Text);Store.Save(d);users.Items.Add(u.Name);users.SelectedItem=u.Name;n.Close();});n.Action("Cancel",n.Close);n.ShowDialog(f);}});
    Add(profileActions,"Emulator saves",()=>UserSaveRoutes.Show(f,d));Add(profileActions,"Guest browser",()=>{GuestBrowser(f,d);f.Close();});
    var goal=NextDialog.Number(s.WeeklyMinutes,0,10080);var breaks=NextDialog.Number(s.BreakMinutes,0,240);var controller=new CheckBox{Text="Enable XInput navigation",Checked=s.Controller};
    NextDialog.Field(fields,"Rolling 7-day goal (minutes; 0 off)",goal);NextDialog.Field(fields,"Break reminder (minutes; 0 off)",breaks);NextDialog.Field(fields,"Controller navigation",controller);
    long played=WeekSeconds(d,DateTime.UtcNow)/60;
    NextDialog.Field(fields,"Recorded last 7 days",new Label{Text=played+" minutes"+(s.WeeklyMinutes>0?" / "+s.WeeklyMinutes+" goal ("+Math.Min(100,played*100/s.WeeklyMinutes)+"%)":" — goal off")+". Goals use recorded emulator time.",AutoSize=true},70);
    NextDialog.Field(fields,"Controller controls",new Label{Text="D-pad: move selection/focus · A: activate · B: close dialogs · shoulders: tabs. Only while FishBowl is active. XInput controllers only.",AutoSize=true},80);
    var save=ExperienceUi.Button("Save personal settings",()=>{s.WeeklyMinutes=(int)goal.Value;s.BreakMinutes=(int)breaks.Value;s.Controller=controller.Checked;Store.Save(d);refresh();});NextDialog.Field(fields,"",save);
    var cf=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};checks.Controls.Add(cf);
    cf.Controls.Add(ExperienceUi.Label("Choose a game for diagnostics. Integrity checks compare raw files with a local XML DAT; archives and disc sets are not unpacked.",80));
    var games=d.Games.OrderBy(g=>g.Title).ToList();var choice=new ComboBox{Width=600,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Title"};choice.Items.AddRange(games.Cast<object>().ToArray());if(choice.Items.Count>0)choice.SelectedIndex=0;cf.Controls.Add(choice);
    Add(cf,"Diagnose selected game",()=>{var g=choice.SelectedItem as GameEntry;if(g==null)throw new IOException("Choose a game.");Troubleshoot(f,d,g,null);});
    Add(cf,"Check selected file against DAT",()=>{var g=choice.SelectedItem as GameEntry;if(g==null)throw new IOException("Choose a game.");using(var open=new OpenFileDialog{Filter="XML DAT files|*.dat;*.xml",Title="Choose local reference DAT"}){if(open.ShowDialog(f)!=DialogResult.OK)return;string dat=open.FileName;string path=NextData.LaunchPath(g);var result=BackgroundWork<string>.Run(f,"ROM integrity check",(token,progress)=>RomIntegrity.Check(dat,path,token,progress));if(result!=null)Report(f,"Integrity result",result);}});
    Add(cf,"Library health",()=>Polish.HealthScreen(f,d));Add(cf,"Devices and emulator checks",()=>Report(f,"Device and emulator checks",Polish.DeviceReport(d)));
    Add(cf,"Undo last bulk edit",()=>Report(f,"Bulk undo",UndoBulk(d)));
    cf.Controls.Add(ExperienceUi.Label("Undo keeps the last 10 bulk operations. It restores changed fields only and stops if later changes conflict.",70));
    var sf=NextDialog.Fields(share);var scope=NextDialog.Choice(new[]{"All games","Favorites","Play queue","Choose games"},"All games");NextDialog.Field(sf,"Games to share",scope);var chosen=new CheckedListBox{CheckOnClick=true,DisplayMember="Title",Visible=false};chosen.Items.AddRange(games.Cast<object>().ToArray());NextDialog.Field(sf,"Select games",chosen,140);var chosenLabel=sf.GetControlFromPosition(0,sf.RowCount-1);int chosenRow=sf.RowCount-1;sf.RowStyles[chosenRow].Height=0;chosenLabel.Visible=false;scope.SelectedIndexChanged+=(a,b)=>{bool visible=scope.Text=="Choose games";chosen.Visible=visible;chosenLabel.Visible=visible;sf.RowStyles[chosenRow].Height=visible?140:0;};var paths=new CheckBox{Text="Include local file paths (private)",Checked=false};var history=new CheckBox{Text="Include progress and play history",Checked=false};var art=new CheckBox{Text="Include local artwork thumbnails",Checked=false};NextDialog.Field(sf,"Catalog privacy",paths);NextDialog.Field(sf,"",history);NextDialog.Field(sf,"Artwork",art);
    NextDialog.Field(sf,"Included by default",new Label{Text="Titles, platform, genre, descriptions and tags. Preview before sharing: those fields can also contain personal information.",AutoSize=true},90);
    NextDialog.Field(sf,"",ExperienceUi.Button("Export HTML catalog",()=>{IEnumerable<GameEntry> export=d.Games;if(scope.Text=="Favorites")export=d.Games.Where(g=>g.Favorite);else if(scope.Text=="Play queue")export=d.Games.Where(g=>d.PlayQueue.Contains(g.Id));else if(scope.Text=="Choose games")export=chosen.CheckedItems.Cast<GameEntry>();var selected=export.ToList();if(selected.Count==0)throw new IOException("Choose at least one game to export.");using(var saveFile=new SaveFileDialog{Filter="HTML catalog|*.html",FileName="FishBowl catalog.html"}){if(saveFile.ShowDialog(f)==DialogResult.OK){bool includePaths=paths.Checked,includeHistory=history.Checked,includeArt=art.Checked;var catalog=BackgroundWork<string>.Run(f,"Build catalog",(token,progress)=>{token.ThrowIfCancellationRequested();progress("Preparing "+selected.Count+" games…");return Html(selected,includePaths,includeHistory,includeArt,token);});if(catalog!=null){File.WriteAllText(saveFile.FileName,catalog,new UTF8Encoding(false));Report(f,"Catalog exported","Saved to "+saveFile.FileName+"\r\nOpen it in a browser to preview. It works offline and contains no remote resources.");}}}}));
    f.Shown+=(a,b)=>{foreach(var control in NextUi.Descendants(tabs)){if(control is TabPage||control is Panel||control is TableLayoutPanel||control is Label||control is CheckBox){control.BackColor=FishBowlPalette.ThemeSurface;control.ForeColor=FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk,control.BackColor);}}foreach(var table in NextUi.Descendants(f).OfType<TableLayoutPanel>())foreach(Control label in table.Controls){int row=table.GetRow(label);if(row<0||table.GetColumn(label)!=0||!label.Visible)continue;int height=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(60,label.Width),1000),TextFormatFlags.WordBreak).Height+18;table.RowStyles[row].Height=Math.Max(table.RowStyles[row].Height,Math.Max(height,label.Font.Height+28));}};
    f.Action("Close",f.Close);f.ShowDialog(owner);
   }
  }
  public static LibraryData BeginGuest(LibraryData d){if(Guest||ActiveLaunches>0||WorkGate.Busy>0)throw new IOException("Finish emulator sessions before entering guest mode.");var copy=Copy(d);Guest=true;return copy;}
  public static void EndGuest(LibraryData d,LibraryData snapshot){if(ActiveLaunches>0)throw new IOException("Close the guest's launched emulator before leaving guest mode.");foreach(var p in typeof(LibraryData).GetProperties())p.SetValue(d,p.GetValue(snapshot,null),null);Guest=false;}
  static void GuestBrowser(IWin32Window owner,LibraryData d) {
   d=Copy(d);
   var paused=new List<Action>();foreach(Form form in Application.OpenForms)foreach(var field in form.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic)){var timer=field.GetValue(form) as System.Windows.Forms.Timer;if(timer!=null&&timer.Enabled){timer.Stop();paused.Add(timer.Start);}var watcher=field.GetValue(form) as FileSystemWatcher;if(watcher!=null&&watcher.EnableRaisingEvents){watcher.EnableRaisingEvents=false;paused.Add(()=>watcher.EnableRaisingEvents=true);}var watchers=field.GetValue(form) as IEnumerable<FileSystemWatcher>;if(watchers!=null)foreach(var w in watchers)if(w.EnableRaisingEvents){var watched=w;watched.EnableRaisingEvents=false;paused.Add(()=>watched.EnableRaisingEvents=true);}}
   LibraryData snapshot=null;try {snapshot=BeginGuest(d);using(var f=new NextDialog("Guest browser — Library changes disabled")){
    var filter=new TextBox{Dock=DockStyle.Top,AccessibleName="Filter guest games"};var list=new ListBox{Dock=DockStyle.Fill,DisplayMember="Title"};f.Body.Controls.Add(list);f.Body.Controls.Add(filter);Action load=()=>{list.Items.Clear();list.Items.AddRange(d.Games.Where(g=>(g.Title??"").IndexOf(filter.Text,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(g=>g.Title).Cast<object>().ToArray());if(list.Items.Count>0)list.SelectedIndex=0;};filter.TextChanged+=(a,b)=>load();load();
    Action launch=()=>{var g=list.SelectedItem as GameEntry;if(g!=null)ExperienceTools.Launch(f,d,g);};list.DoubleClick+=(a,b)=>launch();f.Action("Launch",launch);f.Action("Details",()=>{var g=list.SelectedItem as GameEntry;if(g!=null)Report(f,"Game details",g.Title+"\r\n"+g.Genre+"\r\n"+g.Description);});f.Action("Leave guest mode",f.Close);
    f.FormClosing+=(a,b)=>{if(ActiveLaunches>0){b.Cancel=true;MessageBox.Show(f,"Close the launched emulator before leaving guest mode.","Guest browser");}};f.ShowDialog(owner);
   }}finally{if(snapshot!=null)EndGuest(d,snapshot);foreach(var resume in paused)resume();}
  }
 }
 public static class RomIntegrity {
  static readonly uint[] CrcTable=BuildCrcTable();
  static uint[] BuildCrcTable(){var table=new uint[256];for(uint i=0;i<256;i++){uint value=i;for(int k=0;k<8;k++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);table[i]=value;}return table;}
  public static string Check(string dat,string path,CancellationToken token,Action<string> progress) {
   if(!File.Exists(path))throw new IOException("The selected game file is missing.");if(new FileInfo(dat).Length>100*1024*1024)throw new IOException("DAT exceeds 100 MB.");
   var refs=new List<Dictionary<string,string>>();using(var reader=XmlReader.Create(dat,new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=100*1024*1024})){while(reader.Read()){token.ThrowIfCancellationRequested();if(reader.NodeType!=XmlNodeType.Element||reader.LocalName!="rom")continue;var r=new Dictionary<string,string>();foreach(var name in new[]{"name","size","crc","md5","sha1","status"})r[name]=reader.GetAttribute(name);refs.Add(r);}}
   long size=new FileInfo(path).Length;var candidates=refs.Where(r=>r["status"]!="nodump"&&(r["size"]==null||r["size"]==size.ToString(CultureInfo.InvariantCulture))).ToList();
   if(candidates.Count==0)return "No reference of this size. The DAT may describe a different format or an uncompressed ROM. No files changed.";
   uint crc=0xffffffff;long read=0;using(var md5=MD5.Create())using(var sha=SHA1.Create())using(var stream=File.OpenRead(path)){byte[] buffer=new byte[1024*1024];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();md5.TransformBlock(buffer,0,n,buffer,0);sha.TransformBlock(buffer,0,n,buffer,0);for(int i=0;i<n;i++)crc=(crc>>8)^CrcTable[(crc^buffer[i])&255];read+=n;progress("Checking "+Path.GetFileName(path)+" — "+(size==0?100:read*100/size)+"%");}md5.TransformFinalBlock(new byte[0],0,0);sha.TransformFinalBlock(new byte[0],0,0);string m=Hex(md5.Hash),s=Hex(sha.Hash),c=(~crc).ToString("x8");var match=candidates.Where(r=>Matches(r,"md5",m)&&Matches(r,"sha1",s)&&Matches(r,"crc",c)&&new[]{"crc","md5","sha1"}.Any(k=>!string.IsNullOrWhiteSpace(r[k]))).ToList();string hashes="\r\nCRC32: "+c+"\r\nMD5: "+m+"\r\nSHA1: "+s;
   if(match.Count>0)return "Verified against reference: "+string.Join(", ",match.Select(r=>r["name"]))+hashes+"\r\nAll supplied hashes matched. Reference quality depends on your DAT.";
   bool named=refs.Any(r=>string.Equals(r["name"],Path.GetFileName(path),StringComparison.OrdinalIgnoreCase));return (named?"Mismatch against named reference. The file may be modified, damaged, or a different revision.":"Unrecognized file. No hash match in this DAT; this alone does not prove corruption.")+hashes+"\r\nNo files changed.";}
  }
  static bool Matches(Dictionary<string,string> r,string key,string value){return string.IsNullOrWhiteSpace(r[key])||string.Equals(r[key],value,StringComparison.OrdinalIgnoreCase);}
  static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
 }
 public sealed class UserToolRuntime : IDisposable {
  readonly LibraryData data;readonly Form owner;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=33};readonly Dictionary<string,int> notices=new Dictionary<string,int>();readonly NotifyIcon notification=new NotifyIcon();ushort previous;DateTime hideNotice;DateTime nextReminder;
  public UserToolRuntime(Form owner,LibraryData data){this.owner=owner;this.data=data;timer.Tick+=(s,e)=>Tick();timer.Start();}
  void Tick(){if(notification.Visible&&DateTime.UtcNow>hideNotice)notification.Visible=false;var s=UserTools.Ensure(data);if(s.Controller)Navigate();if(UserTools.Guest||s.BreakMinutes<=0||DateTime.UtcNow<nextReminder)return;nextReminder=DateTime.UtcNow.AddSeconds(1);foreach(var p in data.PlaySessions??new List<PlaySession>()){DateTime start;if(!UserTools.ActiveSessions.ContainsKey(p.Id)||p.Uncertain||p.EndedAt!=null||!DateTime.TryParse(p.StartedAt,null,DateTimeStyles.RoundtripKind,out start))continue;int count=(int)((DateTime.UtcNow-start.ToUniversalTime()).TotalMinutes/s.BreakMinutes);int seen;if(count>0&&(!notices.TryGetValue(p.Id,out seen)||count>seen)){notices[p.Id]=count;notification.Icon=owner.Icon??SystemIcons.Information;notification.Visible=true;notification.BalloonTipTitle="FishBowl break reminder";notification.BalloonTipText="You've played for "+(count*s.BreakMinutes)+" minutes. Take a break when convenient.";notification.ShowBalloonTip(8000);hideNotice=DateTime.UtcNow.AddSeconds(12);}}}
  [StructLayout(LayoutKind.Sequential)]struct Pad{public ushort Buttons;public byte LeftTrigger,RightTrigger;public short LX,LY,RX,RY;}
  [StructLayout(LayoutKind.Sequential)]struct State{public uint Packet;public Pad Pad;}
  [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")]static extern uint Get(uint index,out State s);
  [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")]static extern uint GetOld(uint index,out State s);
  public static bool ControllerConnected(){State state;return Read(out state);}
  static bool Read(out State state){state=new State();try{for(uint i=0;i<4;i++)if(Get(i,out state)==0)return true;}catch(DllNotFoundException){try{for(uint i=0;i<4;i++)if(GetOld(i,out state)==0)return true;}catch(DllNotFoundException){}}return false;}
  void Navigate(){var f=Form.ActiveForm;State state;if(f==null||!Read(out state)){previous=0;return;}ushort pressed=(ushort)(state.Pad.Buttons&~previous);previous=state.Pad.Buttons;ApplyButtons(f,owner,pressed);}
  public static void ApplyButtons(Form f,Form owner,ushort pressed){var c=f.ActiveControl;while(c is ContainerControl&&((ContainerControl)c).ActiveControl!=null)c=((ContainerControl)c).ActiveControl;
   if((pressed&0x1000)!=0){var host=f as MainForm;if(host!=null&&host.ActivateControllerSelection(c))return;var button=c as Button;if(button!=null)button.PerformClick();else if(c is CheckBox)((CheckBox)c).Checked=!((CheckBox)c).Checked;else if(c is ComboBox)((ComboBox)c).DroppedDown=!((ComboBox)c).DroppedDown;else if(c is ListView||c is ListBox)typeof(Control).GetMethod("OnDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{EventArgs.Empty});}
   if((pressed&0x2000)!=0&&f!=owner)f.Close();
   int dir=(pressed&1)!=0?-1:(pressed&2)!=0?1:0;if(dir!=0){var list=c as ListBox;var view=c as ListView;var combo=c as ComboBox;if(list!=null&&list.Items.Count>0)list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+dir));else if(combo!=null&&combo.Items.Count>0)combo.SelectedIndex=Math.Max(0,Math.Min(combo.Items.Count-1,combo.SelectedIndex+dir));else if(view!=null&&view.Items.Count>0){int at=view.SelectedIndices.Count>0?view.SelectedIndices[0]:0;var item=view.Items[Math.Max(0,Math.Min(view.Items.Count-1,at+dir))];view.SelectedItems.Clear();item.Selected=true;item.Focused=true;item.EnsureVisible();}else f.SelectNextControl(c,dir>0,true,true,true);}
   if((pressed&12)!=0){var number=c as NumericUpDown;for(var parent=c==null?null:c.Parent;number==null&&parent!=null;parent=parent.Parent)number=parent as NumericUpDown;if(number!=null)number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,number.Value+((pressed&8)!=0?number.Increment:-number.Increment)));else f.SelectNextControl(c,(pressed&8)!=0,true,true,true);}
   if((pressed&0x300)!=0){TabControl tab=null;for(var parent=c==null?null:c.Parent;parent!=null;parent=parent.Parent)if(parent is TabControl){tab=(TabControl)parent;break;}if(tab==null)tab=Descendants(f).OfType<TabControl>().FirstOrDefault(t=>t.Visible);if(tab!=null&&tab.TabCount>0)tab.SelectedIndex=(tab.SelectedIndex+((pressed&0x200)!=0?1:tab.TabCount-1))%tab.TabCount;}
  }
  static IEnumerable<Control> Descendants(Control c){foreach(Control child in c.Controls){yield return child;foreach(var x in Descendants(child))yield return x;}}
  public void Dispose(){timer.Stop();timer.Dispose();notification.Dispose();}
 }
 public partial class MainForm {
  internal bool ActivateControllerSelection(Control control){if(control!=emulatorList)return false;RunUiAction(OpenSelectedEmulator);return true;}
  internal void RefreshUserToolsViews(LibraryData data){if(!object.ReferenceEquals(data,library))return;ApplyThemeColors();ApplyVisualScale();RefreshHub();if(embeddedLibrary!=null)embeddedLibrary.ReloadLibrary();ConfigureGameFolderWatchers();ConfigureScheduledLibraryScan();FishBowlPalette.StyleWindow(this);CosmeticRuntime.Apply(this);}
  void ShowUserTools(){UserTools.Show(this,library,()=>RefreshUserToolsViews(library));RefreshHub();if(embeddedLibrary!=null)embeddedLibrary.ReloadLibrary();}
 }
}
