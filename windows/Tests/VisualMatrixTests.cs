using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;

class VisualMatrixTests
{
    static LibraryData data;static GameEntry game;static EmulatorProfile emulator;static int rendered;static readonly List<string> failures=new List<string>();
    static object Value(ParameterInfo parameter)
    {
        Type type=parameter.ParameterType;string name=parameter.Name;
        if(type==typeof(LibraryData))return data;if(type==typeof(GameEntry))return game;if(type==typeof(EmulatorProfile))return emulator;
        if(type==typeof(PlaySession))return new PlaySession{Id="preview",GameId=game.Id,Seconds=1800};if(type==typeof(ThemeSettings))return data.Theme;if(type==typeof(MultiplayerSettings))return data.Multiplayer??new MultiplayerSettings();
        if(type==typeof(WorkspaceItem))return new WorkspaceItem{Title="A long workspace item name for layout coverage",Kind="Folder",Target=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)};
        if(type==typeof(string)){if(name=="folder")return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);if(name=="source")return game.Path;if(name=="page")return "Setup checks";if(name=="version")return "1.3";if(name=="prefillExecutable")return emulator.Executable;if(name=="currentBackupFolder")return data.BackupFolder??"";return "A long example label to exercise clipping and layout";}
        if(type==typeof(bool))return false;if(type==typeof(int))return name=="width"?840:580;
        if(type==typeof(Action<GameEntry>))return new Action<GameEntry>(delegate{});
        if(type.IsGenericType){Type element=type.GetGenericArguments()[0];var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element));if(element==typeof(GameEntry))list.Add(game);if(element==typeof(EmulatorProfile))list.Add(emulator);if(element==typeof(string))list.Add(name=="paths"?game.Path:name=="extensions"?".png":"Long example row / text for layout review");return list;}
        if(parameter.IsOptional)return parameter.DefaultValue;throw new Exception("No fixture for "+type.Name);
    }
    static void Capture(Form form,string filename,float scale)
    {
        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-6000,-6000);
        if(form is ControllerLauncher){var timer=(System.Windows.Forms.Timer)typeof(ControllerLauncher).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);form.Shown+=delegate{timer.Stop();};}
        form.Show();Application.DoEvents();if(scale!=1f){form.Scale(new SizeF(scale,scale));Application.DoEvents();}
        using(var image=new Bitmap(Math.Max(1,form.Width),Math.Max(1,form.Height))){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(filename);}
        rendered++;
    }
    [STAThread]static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();data=Store.Load();ExperienceData.Ensure(data);NextData.Ensure(data);game=data.Games.First();emulator=data.Emulators.First();data.Theme.LastSeenBuild="1.3";data.Enhancements.TextPercent=100;game.Title="A very long example game title to check truncation and wrapping across views";if(args.Contains("--large")){for(int i=0;i<1000;i++){var extra=NextData.Copy(game);extra.Id=Guid.NewGuid().ToString("N");extra.Title="Large library example "+i.ToString("D4")+" with a long title for wrapping";data.Games.Add(extra);}}if(args.Contains("--empty")){data.Games.Clear();data.Emulators.Clear();}
        string root=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"visual-matrix");Directory.CreateDirectory(root);
        var types=typeof(MainForm).Assembly.GetTypes().Where(t=>!t.ContainsGenericParameters&&!t.IsAbstract&&typeof(FishBowlDialog).IsAssignableFrom(t)&&t!=typeof(FishBowlDialog)&&t.GetConstructors().Length>0).OrderBy(t=>t.Name).ToArray();
        string[] themes=args.Contains("--full")?new[]{"FishBowl Water","Light","High Contrast"}:new[]{"FishBowl Water"};float[] scales=args.Contains("--full")?new[]{1f,1.25f,1.5f,2f}:new[]{1f};
        foreach(string theme in themes)foreach(float scale in scales)
        {
            data.Theme.Name=theme;Store.Save(data);using(var main=new MainForm(true)){Capture(main,Path.Combine(root,theme+"-"+scale+"-Main.png"),scale);}
            foreach(var type in types)
            {
                try{var constructor=type.GetConstructors().OrderBy(c=>c.GetParameters().Length).First();using(var form=(Form)constructor.Invoke(constructor.GetParameters().Select(Value).ToArray()))Capture(form,Path.Combine(root,theme+"-"+scale+"-"+type.Name+".png"),scale);}
                catch(Exception error){Console.WriteLine(type.Name+": "+error.GetBaseException());failures.Add(theme+" "+scale+" "+type.Name+": "+error.GetBaseException().Message);}
            }
        }
        File.WriteAllLines(Path.Combine(root,"failures.txt"),failures);Console.WriteLine("Rendered "+rendered+" application/dialog previews; "+failures.Count+" failures. Output: "+root);foreach(string error in failures)Console.WriteLine(error);return failures.Count==0?0:1;
    }
}
