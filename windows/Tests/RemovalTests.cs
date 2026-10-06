using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using EmulatorHub;
class RemovalTests
{
    static int checks;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    static void Confirm(Action action, int response)
    {
        bool answered = false; int ticks = 0;
        using (var timer = new System.Windows.Forms.Timer { Interval = 40 })
        {
            timer.Tick += delegate {
                IntPtr dialog = FindWindow("#32770", "Remove games");
                if (dialog != IntPtr.Zero) { timer.Stop(); answered = true; SendMessage(dialog, 0x111, new IntPtr(response), IntPtr.Zero); }
                else if (++ticks > 100) { timer.Stop(); throw new Exception("Removal confirmation missing"); }
            };
            timer.Start(); action(); Check(answered, "Removal asks for confirmation");
        }
    }
    [STAThread] static int Main()
    {
        try { Run(); Console.WriteLine("PASS: " + checks + " removal, disk preservation, scans, persistence and UI checks."); return 0; }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
    static void Run()
    {
        Application.EnableVisualStyles();
        var data = Store.Load(); data.Games.Clear(); data.Emulators.Clear(); data.Collections.Clear();
        string folder = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "remove-fixture"); Directory.CreateDirectory(folder);
        string one = Path.Combine(folder, "One.rom"), two = Path.Combine(folder, "Two.rom"), saves = Path.Combine(folder, "One.sav"), executable = Path.Combine(folder, "emulator.exe");
        foreach (string file in new[] { one, two, saves, executable }) File.WriteAllText(file, "preserve");
        var emulator = new EmulatorProfile { Id="remove-emulator", Name="Remove fixture", Executable=executable, ScanFolder=folder, Extensions=new List<string>{"rom"}, Preset="Custom" };
        data.Emulators.Add(emulator);
        data.Games.Add(new GameEntry { Id="one", Title="One", Path=one, EmulatorId=emulator.Id, PreferredEmulatorId=emulator.Id, Tags=new List<string>() });
        data.Games.Add(new GameEntry { Id="two", Title="Two", Path=two, EmulatorId=emulator.Id, Tags=new List<string>() });
        data.Collections.Add(new GameCollection { Id="collection", Name="Collection", GameIds=new List<string>{"one","two"} });
        data.PlayQueue = new List<string>{"one","two"}; Store.Save(data);
        using (var form = new GameLibraryDialog(data))
        {
            form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-4000,-4000); form.Show(); Application.DoEvents();
            var list=(ListView)typeof(GameLibraryDialog).GetField("games",flags).GetValue(form);
            var remove=NextUi.Descendants(form).OfType<Button>().First(b=>b.Text=="Remove game");
            Check(remove.Visible && !remove.Enabled,"Visible removal action disabled without selection");
            list.Items.Cast<ListViewItem>().First(row=>((GameEntry)row.Tag).Id=="one").Selected=true; Application.DoEvents();
            Check(remove.Enabled,"Removal enabled with selection");
            Check(list.ContextMenuStrip.Items.Cast<ToolStripItem>().Any(item=>item.Text.Contains("Remove selected games")),"Right-click removal available");
            Confirm(remove.PerformClick,7); Check(data.Games.Count==2 && Store.Load().Games.Count==2,"Cancel leaves library unchanged");
            Confirm(remove.PerformClick,6); Check(data.Games.Count==1 && list.Items.Count==1,"Confirm removes selected game and refreshes rows");
            Check(Store.Load().Games.Count==1,"Game removal persists");
            Check(data.Collections[0].GameIds.SequenceEqual(new[]{"two"}) && data.PlayQueue.SequenceEqual(new[]{"two"}),"Removed references cleaned from collection and queue");
            Check(GameLibraryRemoval.IsExcluded(Store.Load(),one.ToUpperInvariant()),"Scan exclusion persists and ignores case");
            Check(GameLibraryCatalog.Sync(data)==0,"Legacy scan cannot re-add removed file");
            Check(LibraryJobs.Scan(data,CancellationToken.None,delegate{}).Count==0,"Background scan cannot re-add removed file");
            GameLibraryRemoval.AllowPath(data,one); Check(!GameLibraryRemoval.IsExcluded(data,one),"Explicit add clears exclusion");
            Check(GameLibraryCatalog.Sync(data)==1,"Explicitly restored path can be discovered again");
            form.Close();
        }
        Check(GameLibraryRemoval.RemoveGames(data,new[]{"one","two","missing"})==1,"Bulk removal counts actual records only");
        Check(GameLibraryRemoval.RemoveGames(data,data.Games.Select(g=>g.Id).ToArray())==1 && data.Games.Count==0,"Bulk removal handles remaining games");
        Check(data.RemovedGamePaths.Count==2,"Exclusions do not duplicate paths");
        data.Games.Add(new GameEntry{Id="keep",Title="Keep",Path=one,EmulatorId=emulator.Id,PreferredEmulatorId=emulator.Id,Tags=new List<string>()});
        data.Theme.ConfirmBeforeEmulatorRemoval=false; Store.Save(data);
        using(var main=new MainForm(true))
        {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Application.DoEvents();
            var remove=(Button)typeof(MainForm).GetField("removeButton",flags).GetValue(main);
            Check(remove.Parent!=null && remove.Text=="Remove emulator","Emulator toolbar includes explicit removal button");
            typeof(MainForm).GetField("selectedEmulatorId",flags).SetValue(main,emulator.Id);
            typeof(MainForm).GetMethod("RemoveEmulator",flags).Invoke(main,null);
            var saved=Store.Load();Check(saved.Emulators.Count==0 && saved.Games.Count==1,"Emulator removal persists and keeps game entries");
            Check(saved.Games[0].EmulatorId==null && saved.Games[0].PreferredEmulatorId==null,"Removed emulator assignments cleared");
            main.Close();
        }
        foreach(string file in new[]{one,two,saves,executable}) Check(File.ReadAllText(file)=="preserve","File remains intact: "+Path.GetFileName(file));
        Check(!GameLibraryRemoval.RemoveEmulator(data,"unknown"),"Unknown emulator is a no-op");
        data.Games[0].Discs=new List<string>{two};
        data.RemovedGamePaths.Clear();
        GameLibraryRemoval.RemoveGames(data,new[]{"keep"});
        Check(GameLibraryRemoval.IsExcluded(data,one) && GameLibraryRemoval.IsExcluded(data,two),"Multi-disc removal excludes every disc from scans");
        for(int percent=100;percent<=200;percent+=50)
        {
            NextUi.TextPercent=percent;
            using(var main=new MainForm(true))
            {
                main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Application.DoEvents();
                var navigation=(TabControl)typeof(MainForm).GetField("workspaceNavigation",flags).GetValue(main);
                navigation.SelectedIndex=1;Application.DoEvents();
                var remove=(Button)typeof(MainForm).GetField("removeButton",flags).GetValue(main);
                Check(remove.Visible && remove.Parent.ClientRectangle.Contains(remove.Bounds),"Emulator removal fits toolbar at "+percent+"% text");
                navigation.SelectedIndex=2;Application.DoEvents();
                var gameRemove=NextUi.Descendants(main).OfType<Button>().First(b=>b.Text=="Remove game");
                Check(gameRemove.Visible && gameRemove.Parent.ClientRectangle.Contains(gameRemove.Bounds),"Game removal fits toolbar at "+percent+"% text");
                using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("Removal-library-"+percent+".png");}
                navigation.SelectedIndex=1;Application.DoEvents();
                using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("Removal-emulators-"+percent+".png");}
                main.Close();
            }
        }
        NextUi.TextPercent=100;
    }
}
