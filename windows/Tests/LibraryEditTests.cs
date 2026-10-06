using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EmulatorHub;
class LibraryEditTests
{
    static int checks;
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,flags).GetValue(target);}
    static void Show(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.Show();Application.DoEvents();}
    static void Wait(Func<bool> ready,string message){var end=DateTime.UtcNow.AddSeconds(8);while(!ready()&&DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(15);}Check(ready(),message);}
    static GameEntry Save(GameEntry entry,IEnumerable<EmulatorProfile> profiles,string title,string art)
    {
        using(var dialog=new GameDialog(entry,profiles))
        {
            Show(dialog);Field<TextBox>(dialog,"title").Text=title;Field<TextBox>(dialog,"artwork").Text=art;
            typeof(GameDialog).GetMethod("SuggestGame",flags).Invoke(dialog,null);
            Check(Field<TextBox>(dialog,"title").Text==title,"Path recognition preserves an explicitly entered title");
            NextUi.Descendants(dialog).OfType<Button>().Single(button=>button.Text=="Save").PerformClick();
            Check(dialog.DialogResult==DialogResult.OK && dialog.Game!=null,"Save accepts the edited game");
            return dialog.Game;
        }
    }
    [STAThread]static int Main()
    {
        try{Run();Console.WriteLine("PASS: "+checks+" title edits, cover retention, list/artwork previews, cancellation, persistence and file preservation checks.");return 0;}
        catch(Exception error){Console.WriteLine(error);return 1;}
    }
    static void Run()
    {
        Application.EnableVisualStyles();
        var data=Store.Load();ExperienceData.Ensure(data);NextData.Ensure(data);LibraryAdditions.Ensure(data);UserTools.Ensure(data);if(data.Cosmetics==null)data.Cosmetics=new CosmeticSettings();data.Games.Clear();data.Emulators.Clear();data.Collections.Clear();
        string folder=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"library-edit-fixture");Directory.CreateDirectory(folder);
        string gamePath=Path.Combine(folder,"Example.nds"),green=Path.Combine(folder,"green.png"),blue=Path.Combine(folder,"blue.png");
        byte[] bytes=new byte[0x940];Encoding.ASCII.GetBytes("ABCE").CopyTo(bytes,12);BitConverter.GetBytes((uint)0x100).CopyTo(bytes,0x68);Encoding.Unicode.GetBytes("Embedded game title").CopyTo(bytes,0x440);File.WriteAllBytes(gamePath,bytes);
        using(var image=new Bitmap(80,80)){using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Lime);image.Save(green);using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Blue);image.Save(blue);}
        var emulator=new EmulatorProfile{Id="edit-emulator",Name="Edit fixture",Executable=gamePath,Extensions=new List<string>{"nds"},Preset="Custom"};data.Emulators.Add(emulator);
        var entry=new GameEntry{Id="edit-game",Title="Original",Path=gamePath,EmulatorId=emulator.Id,Tags=new List<string>()};
        foreach(string title in new[]{"Custom library title","Example","New game"})
        {
            entry=Save(entry,data.Emulators,title,green);Check(entry.Title==title && entry.TitleIsCustom,"Saved display title remains exact: "+title);
            GameRecognition.Apply(entry,data.Emulators,false);Check(entry.Title==title,"Later recognition preserves the saved display title");
            Check(entry.Path==gamePath && File.ReadAllBytes(gamePath).SequenceEqual(bytes),"Editing never renames or changes the game file");
        }
        Check(entry.ArtworkPath!=green && File.Exists(entry.ArtworkPath),"Cover retained in FishBowl artwork storage");
        Check(File.Exists(green),"Original selected image remains on disk");
        File.Delete(green);Check(File.Exists(entry.ArtworkPath),"Retained cover survives removal of original image");
        entry.Title="Saved title";data.Games.Add(entry);data.Cosmetics.DetailPreview=true;data.Experience.LibraryView="Details";Store.Save(data);
        Check(Store.Load().Games.Single().Title=="Saved title" && Store.Load().Games.Single().TitleIsCustom,"Title preference survives library reload");
        using(var library=new GameLibraryDialog(data))
        {
            Show(library);var list=Field<ListView>(library,"games");list.Items[0].Selected=true;Application.DoEvents();
            Wait(()=>list.Items[0].ImageIndex>0,"Default list view loads assigned game image");
            using(var icon=new Bitmap(list.SmallImageList.Images[list.Items[0].ImageIndex]))Check(icon.GetPixel(icon.Width/2,icon.Height/2).G>180,"List row shows selected green cover");
            Check(Field<TextBox>(library,"detailText").Text.StartsWith("Saved title"),"Selected-game preview shows saved display title");
            var old=data.Games[0];
            using(var timer=new System.Windows.Forms.Timer{Interval=40})
            {
                timer.Tick+=delegate{
                    var dialog=Application.OpenForms.OfType<GameDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();
                    Field<TextBox>(dialog,"title").Text="Updated display title";Field<TextBox>(dialog,"artwork").Text=blue;
                    NextUi.Descendants(dialog).OfType<Button>().Single(button=>button.Text=="Save").PerformClick();
                };
                timer.Start();typeof(GameLibraryDialog).GetMethod("EditGame",flags).Invoke(library,null);
            }
            Check(list.Items[0].Text.StartsWith("Updated display title"),"Edit game updates library row immediately");
            Check(Field<TextBox>(library,"detailText").Text.StartsWith("Updated display title"),"Edit game refreshes selected-game preview immediately");
            Wait(()=>{
                if(list.Items[0].ImageIndex<=0)return false;
                using(var image=new Bitmap(list.SmallImageList.Images[list.Items[0].ImageIndex]))return image.GetPixel(image.Width/2,image.Height/2).B>180;
            },"Edited cover replaces cached list image");
            Check(Store.Load().Games[0].Title=="Updated display title" && Store.Load().Games[0].ArtworkPath==data.Games[0].ArtworkPath,"Title and cover persist from library Edit game action");
            File.Delete(blue);
            typeof(GameLibraryDialog).GetMethod("ToggleGameView",flags).Invoke(library,null);
            Wait(()=>list.Items[0].ImageIndex>0,"Artwork view loads retained custom cover");
            using(var image=new Bitmap(list.LargeImageList.Images[list.Items[0].ImageIndex]))Check(image.GetPixel(image.Width/2,image.Height/2).B>180,"Artwork view displays the blue cover after original is removed");
            using(var image=new Bitmap(library.Width,library.Height)){library.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("Library-edit-artwork.png");}
            typeof(GameLibraryDialog).GetMethod("ToggleGameView",flags).Invoke(library,null);Wait(()=>list.Items[0].ImageIndex>0,"Switching back to list reloads cover");
            using(var image=new Bitmap(library.Width,library.Height)){library.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("Library-edit-list.png");}
            library.Close();
        }
        using(var dialog=new GameDialog(data.Games[0],data.Emulators))
        {
            Show(dialog);Field<TextBox>(dialog,"title").Text="Cancelled title";NextUi.Descendants(dialog).OfType<Button>().Single(button=>button.Text=="Cancel").PerformClick();
            Check(data.Games[0].Title=="Updated display title" && Store.Load().Games[0].Title=="Updated display title","Cancel preserves saved title");
        }
        var roundtrip=Json.Deserialize<LibraryData>(Json.Serialize(data));Check(roundtrip.Games[0].TitleIsCustom && roundtrip.Games[0].ArtworkPath==data.Games[0].ArtworkPath,"Shared data model preserves title and retained image fields");
        Check(Directory.GetFiles(folder,"*.nds").SequenceEqual(new[]{gamePath}) && File.ReadAllBytes(gamePath).SequenceEqual(bytes),"Library editing leaves original game filename and content intact");
    }
}
