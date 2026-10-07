using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EmulatorHub;

class NextRegressionTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static void Reject(Action run,string name){bool rejected=false;try{run();}catch{rejected=true;}Check(rejected,name);}
    static void Render(Action action,string title,string file)
    {
        using(var timer=new System.Windows.Forms.Timer{Interval=350}){int ticks=0;timer.Tick+=delegate{var form=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text==title);if(form==null){if(++ticks>20)throw new Exception("Dialog missing: "+title);return;}form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(file);}Check(form.Icon!=null,"Branded dialog: "+title);timer.Stop();form.Close();};timer.Start();action();}
    }
    [STAThread]static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
    static void Run()
    {
        Application.EnableVisualStyles();var data=Store.Load();ExperienceData.Ensure(data);NextData.Ensure(data);var game=data.Games[0];var emulator=ExperienceData.Emulator(data,game);
        Check(data.Enhancements.TextPercent==100,"Older libraries get text-size defaults");
        var session=NextData.BeginSession(data,game,emulator.Executable,true);long total=game.TotalPlaySeconds;NextData.CompleteSession(data,game,session,120);Check(game.TotalPlaySeconds==total+120,"Completed session updates game total");NextData.CorrectSession(data,session,180,"Correction");Check(game.TotalPlaySeconds==total+180&&session.Corrected,"Correction replaces the old duration");Check(NextData.WeekTotals(data,DateTime.Now).Values.Sum()>=180,"Chart includes corrected sessions");Reject(()=>NextData.CorrectSession(data,session,-1,"Invalid"),"Negative session time rejected");
        var cloned=NextData.Copy(data);Check(cloned.PlaySessions.Any(p=>p.Id==session.Id)&&cloned.Enhancements.LastGameId==game.Id,"New data survives JSON roundtrip");
        emulator.Builds=new List<EmulatorBuild>{new EmulatorBuild{Id="missing",Label="Missing",Executable="Z:\\missing.exe"},new EmulatorBuild{Id="present",Label="Present",Executable=emulator.Executable,ManualVersion="test"}};game.PreferredBuildId="missing";Check(NextData.LaunchEmulator(data,game).Executable==emulator.Executable,"Missing preferred build falls back");game.PreferredBuildId="present";Check(NextData.LaunchEmulator(data,game).ManualVersion=="test","Present preferred build overrides launch metadata");game.PreferredBuildId=null;
        string disc=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"disc-test.iso");File.WriteAllText(disc,"disc");game.Discs=new List<string>{disc};game.LastDiscPath=disc;Check(GameSessions.Validate(data,game).Contains(disc),"Selected disc is used in launch arguments");game.LastDiscPath="Z:\\unregistered.iso";Check(NextData.LaunchPath(game)==game.Path,"Unregistered last disc is ignored");game.LastDiscPath=null;
        using(var source=new Bitmap(40,20)){using(var g=Graphics.FromImage(source))g.Clear(Color.Red);using(var fit=NextMedia.RenderArtwork(source,new Rectangle(0,0,40,20),100,100,true,Color.Blue)){Check(fit.GetPixel(50,1).B>200&&fit.GetPixel(50,50).R>200,"Artwork fit preserves background and image");}Reject(()=>NextMedia.RenderArtwork(source,new Rectangle(100,100,5,5),50,50,false,Color.Black),"Out-of-image crop rejected");}
        string root=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"feature-fixtures");Directory.CreateDirectory(root);string a=Path.Combine(root,"before"),b=Path.Combine(root,"after");Directory.CreateDirectory(a);Directory.CreateDirectory(b);File.WriteAllText(Path.Combine(a,"changed.sav"),"before");File.WriteAllText(Path.Combine(b,"changed.sav"),"after");File.WriteAllText(Path.Combine(a,"removed.sav"),"gone");File.WriteAllText(Path.Combine(b,"added.sav"),"new");var changes=NextData.SnapshotChanges(new SaveSnapshot{Path=a,IsFolder=true},new SaveSnapshot{Path=b,IsFolder=true},CancellationToken.None);Check(changes.Contains("Changed: changed.sav")&&changes.Contains("Added: added.sav")&&changes.Contains("Removed: removed.sav"),"Timeline identifies byte changes, additions and removals");
        Reject(()=>FishBowlUpdater.Parse("{\"Version\":\"1.4\",\"PackageUrl\":\"http://example.com/a.zip\",\"Sha256\":\""+new string('a',64)+"\"}"),"Insecure update URL rejected");Reject(()=>FishBowlUpdater.Parse("{}"),"Incomplete release manifest rejected");
        string zip=Path.Combine(root,"runtime.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){foreach(string name in FishBowlUpdater.RuntimeFiles)using(var writer=new StreamWriter(archive.CreateEntry("FishBowl-1.4/"+name).Open()))writer.Write("fixture "+name);}
        string hash=SafeFiles.HashFile(zip,CancellationToken.None);FishBowlUpdater.ExtractVerified(zip,hash,Path.Combine(root,"runtime"),CancellationToken.None);Check(Directory.GetFiles(Path.Combine(root,"runtime")).Length==6,"Updater extracts only six runtime files");Reject(()=>FishBowlUpdater.ExtractVerified(zip,new string('0',64),Path.Combine(root,"bad-hash"),CancellationToken.None),"Mismatched package hash rejected");
        string escape=Path.Combine(root,"escape.zip");using(var archive=ZipFile.Open(escape,ZipArchiveMode.Create))using(var writer=new StreamWriter(archive.CreateEntry("../FishBowl.exe").Open()))writer.Write("bad");Reject(()=>FishBowlUpdater.ExtractVerified(escape,SafeFiles.HashFile(escape,CancellationToken.None),Path.Combine(root,"escape"),CancellationToken.None),"Updater traversal rejected");
        using(var owner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000)})
        {
            owner.Show();Render(()=>NextTools.Sessions(owner,data),"Session journal and weekly chart","next-sessions.png");Render(()=>NextTools.Timeline(owner,data),"Save timeline and changed files","next-timeline.png");Render(()=>NextTools.BackupPlanner(owner,data),"Backup planner and quota","next-backup.png");Render(()=>NextTools.PreferredBuild(owner,data,game),"Preferred game build and disc","next-build.png");Render(()=>NextMedia.Artwork(owner,data,game),"Artwork crop and tile preview","next-artwork.png");Render(()=>NextMedia.Screenshots(owner,data,game),"Game screenshot gallery","next-screenshots.png");Render(()=>FishBowlUpdater.Show(owner,data),"Reviewed FishBowl updater","next-updater.png");Render(()=>NextUi.Shortcuts(owner,data,delegate{}),"Keyboard shortcuts","next-shortcuts.png");
        }
        using(var main=new MainForm(true))
        {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();var field=typeof(MainForm).GetField("library",BindingFlags.NonPublic|BindingFlags.Instance);var live=(LibraryData)field.GetValue(main);var options=live.Enhancements;options.TextPercent=150;var apply=typeof(MainForm).GetMethod("ApplyAppearanceNow",BindingFlags.NonPublic|BindingFlags.Instance);apply.Invoke(main,null);var first=NextUi.Descendants(main).OfType<Button>().First().Font.Size;apply.Invoke(main,null);Check(Math.Abs(NextUi.Descendants(main).OfType<Button>().First().Font.Size-first)<0.1,"Repeated appearance apply does not compound text size");Render(()=>typeof(MainForm).GetMethod("ShowLiveAppearanceCore",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,null),"Live appearance and accessibility","next-appearance.png");
            Render(()=>typeof(MainForm).GetMethod("ShowGlobalSearch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,null),"Search everything","next-search.png");
        }
        Console.WriteLine("PASS: "+checks+" feature, persistence, launch, artwork, timeline, updater and dialog checks.");
    }
}
