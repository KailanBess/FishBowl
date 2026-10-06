using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class AdditionTests {
    static int checks;
    static void Check(bool condition,string label) {if(!condition) throw new Exception(label); checks++;}
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Modal(Action open,string file) {
        using(var timer=new Timer {Interval=250}) {
            timer.Tick+=(s,e)=> {var dialog=Application.OpenForms.Cast<Form>().OfType<NextDialog>().LastOrDefault(); if(dialog==null) return;
                timer.Stop(); Application.DoEvents();
                var bars=NextUi.Descendants(dialog).OfType<FlowLayoutPanel>().ToList();
                Check(bars.All(b=>b.Controls.Cast<Control>().Where(c=>c.Visible).All(c=>c.Bottom<=b.ClientSize.Height)),file+" action buttons fit");
                using(var bmp=new Bitmap(dialog.Width,dialog.Height)) {dialog.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(file+".png");}
                dialog.Close();};
            timer.Start(); open();
        }
    }
    [STAThread] static int Main() {
        try { return Run(); }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static int Run() {
        Application.EnableVisualStyles();
        Application.ThreadException+=(sender,eventArgs)=> {Console.WriteLine(eventArgs.Exception.ToString()); Environment.Exit(1);};
        var data=new LibraryData {Games=new List<GameEntry>(),Emulators=new List<EmulatorProfile>(),Collections=new List<GameCollection>(),Theme=new ThemeSettings()}; ExperienceData.Ensure(data); NextData.Ensure(data); LibraryAdditions.Ensure(data);
        var a=new GameEntry {Id="a",Title="Mario, \"World\"",ConsoleLabel="SNES",Genre="Platformer",Favorite=true,LaunchCount=0,Tags=new List<string>{"Co-op"},PlayStatus="Backlog"};
        var b=new GameEntry {Id="b",Title="=SUM(1,2)",ConsoleLabel="SNES",Genre="RPG",LaunchCount=2,PlayStatus="Completed"};
        data.Games.AddRange(new [] {a,b});
        var rule=new SmartLibraryList {Name="Unplayed favorites",Search="co-op",Platform="SNES",FavoritesOnly=true,UnplayedOnly=true};
        data.SmartLists.Add(rule);
        Check(LibraryAdditions.Match(data,rule).SequenceEqual(new [] {a}),"combined rule matches metadata and filters");
        a.LaunchCount=1; Check(LibraryAdditions.Match(data,rule).Count==0,"smart list changes after launch"); a.LaunchCount=0;
        LibraryAdditions.Enqueue(data,a); LibraryAdditions.Enqueue(data,a); LibraryAdditions.Enqueue(data,b);
        Check(data.PlayQueue.Count==2,"queue prevents duplicate entries");
        Check(LibraryAdditions.MoveQueue(data,"b",-1) && data.PlayQueue[0]=="b","queue reorders");
        Check(!LibraryAdditions.MoveQueue(data,"b",-1),"queue boundary stays intact");
        data.PlayQueue.Add("deleted"); Check(LibraryAdditions.QueueGames(data).Count==2,"queue tolerates removed games");
        var csv=LibraryAdditions.Csv(data.Games); File.WriteAllText("export.csv",csv);
        Check(csv.Contains("\"Mario, \"\"World\"\"\""),"CSV quotes commas and quotes");
        Check(csv.Contains("\"'=SUM(1,2)\""),"CSV keeps formulas literal");
        Check(LibraryAdditions.SurpriseCandidates(data,true).Count==0,"picker excludes missing files");
        File.WriteAllText("picker.gba","test"); a.Path=Path.GetFullPath("picker.gba"); a.EmulatorId="testemu";
        data.Emulators.Add(new EmulatorProfile {Id="testemu",Name="Fixture",Preset="Custom",Executable=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"FishBowl.exe"),Extensions=new List<string>{"gba"}});
        Check(LibraryAdditions.SurpriseCandidates(data,true).Single()==a,"picker accepts a valid unplayed game");
        a.LaunchCount=1; Check(LibraryAdditions.SurpriseCandidates(data,true).Count==0 && LibraryAdditions.SurpriseCandidates(data,false).Count==1,"picker respects unplayed preference"); a.LaunchCount=0;
        data.Emulators[0].Arguments="{unsupported}"; Check(LibraryAdditions.SurpriseCandidates(data,false).Count==0,"picker rejects invalid launch template"); data.Emulators[0].Arguments=null;
        Store.Save(data); var loaded=Store.Load(); LibraryAdditions.Ensure(loaded);
        Check(loaded.SmartLists.Count==1 && loaded.PlayQueue.SequenceEqual(data.PlayQueue),"new settings persist");
        using(var main=new MainForm(true)) {
            main.ShowInTaskbar=false; main.StartPosition=FormStartPosition.Manual; main.Location=new Point(-4000,-4000); main.Show(); Application.DoEvents();
            var library=(LibraryData)typeof(MainForm).GetField("library",flags).GetValue(main);
            string[] themes={"FishBowl Water","Twilight","Lavender","Ember","Light","High Contrast","Midnight","Forest","Rosewood","Mist"};
            string[] accents={"Ocean","Sunset","Amethyst","Rose","Lime","Gold","Ice"};
            foreach(var theme in themes) foreach(var accent in accents) {
                library.Theme.Name=theme; library.Theme.AccentColor=accent;
                typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null); Application.DoEvents();
                using(var ui=new GameLibraryDialog(library)) {
                    FishBowlPalette.StyleWindow(ui);
                    var buttons=NextUi.Descendants(ui).OfType<Button>().ToList();
                    Check(buttons.Count>0 && buttons.All(button=>button.BackColor==ColorHarmony.Button),theme+"/"+accent+" Library actions use the current button surface role");
                    Check(buttons.All(button=>FishBowlPalette.Contrast(button.ForeColor,button.BackColor)>=4.5),theme+"/"+accent+" button text contrast");
                    Check(FishBowlPalette.Contrast(FishBowlPalette.DisabledText,FishBowlPalette.DisabledSurface)>=4.5,theme+"/"+accent+" disabled contrast");
                    Check(buttons.All(button=>FishBowlPalette.Contrast(FishBowlPalette.EnsureReadable(button.ForeColor,FishBowlHighlights.Blend(button.BackColor,36)),FishBowlHighlights.Blend(button.BackColor,36))>=4.5),theme+"/"+accent+" pressed contrast");
                    if(accent=="Ocean" && (theme=="FishBowl Water" || theme=="Light" || theme=="Forest" || theme=="High Contrast")) {
                        ui.ShowInTaskbar=false; ui.StartPosition=FormStartPosition.Manual; ui.Location=new Point(-4000,-4000); ui.Show(); Application.DoEvents();
                        using(var bmp=new Bitmap(ui.Width,ui.Height)){ui.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save("library-"+theme.Replace(" ","-")+".png");}
                    }
                }
            }
            library.Theme.Name="FishBowl Water"; library.Theme.AccentColor="Ocean";
            typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null);
            Modal(()=>LibraryAdditions.SmartLists(main,loaded),"smart-lists");
            Modal(()=>LibraryAdditions.Queue(main,loaded,a),"queue");
            Modal(()=>LibraryAdditions.Surprise(main,loaded),"surprise");
            Modal(()=>LibraryAdditions.Show(main,loaded,a,loaded.Games),"more-tools");
        }
        Console.WriteLine("PASS: "+checks+" addition and appearance checks across 70 theme/accent combinations."); return 0;
    }
}
