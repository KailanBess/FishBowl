using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using EmulatorHub;
class EmulatorEditorTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Field(EmulatorDialog d,string name,string value){((TextBox)typeof(EmulatorDialog).GetField(name,flags).GetValue(d)).Text=value;}
    static void VisibleAction(Form form,Button button)
    {
        var screen=button.RectangleToScreen(button.ClientRectangle);
        var client=form.RectangleToScreen(form.ClientRectangle);
        var parent=button.Parent.RectangleToScreen(button.Parent.ClientRectangle);
        Check(button.Visible&&button.Width>50&&button.Height>25&&client.Contains(screen)&&parent.Contains(screen),"Action is fully visible: "+button.Text);
    }
    [STAThread]static int Main()
    {
        try{Run();Console.WriteLine("PASS: "+checks+" Add/Edit Emulator footer, scrolling, text size, save and library persistence checks.");return 0;}
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
    static void Run()
    {
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-emulator.exe");File.WriteAllText(executable,"test fixture; never executed");
        foreach(int percent in new[]{100,150,200})foreach(bool edit in new[]{false,true})
        {
            NextUi.TextPercent=percent;
            var existing=edit?new EmulatorProfile{Id="existing",Name="Existing emulator",Executable=executable,Preset="Custom"}:null;
            using(var d=new EmulatorDialog(existing,executable))
            {
                d.ShowInTaskbar=false;d.StartPosition=FormStartPosition.Manual;d.Location=new Point(-4000,-4000);d.Show();Application.DoEvents();
                var accept=d.AcceptButton as Button;Check(accept!=null&&accept.Text==(edit?"Save":"Add emulator"),"Correct explicit add/save action");
                var footer=NextUi.Descendants(d).First(c=>c.Name=="EmulatorEditorActions");Check(footer.Parent is TableLayoutPanel && ((TableLayoutPanel)footer.Parent).GetRow(footer)==1,"Actions outside scrolling body");
                foreach(var button in footer.Controls.OfType<Button>())VisibleAction(d,button);
                var body=(Panel)NextUi.Descendants(d).First(c=>c.Name=="EmulatorEditorBody");Check(body.AutoScroll,"Fields can scroll at larger text sizes");
                Check(!body.Bounds.IntersectsWith(footer.Bounds),"Body cannot cover footer");
                Point before=accept.PointToScreen(Point.Empty);body.AutoScrollPosition=new Point(0,10000);Application.DoEvents();Check(before==accept.PointToScreen(Point.Empty),"Scrolling fields cannot hide action button");
                using(var image=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,(edit?"Edit":"Add")+"-Emulator-"+percent+".png"));}
                Field(d,"name","Added through visible button");Field(d,"executable",executable);accept.PerformClick();
                Check(d.DialogResult==DialogResult.OK&&d.Profile!=null&&d.Profile.Name=="Added through visible button"&&d.Profile.Executable==executable,"Visible action creates/updates emulator profile");
            }
        }
        NextUi.TextPercent=100;
        var data=Store.Load();data.Emulators.Clear();Store.Save(data);
        using(var main=new MainForm(true))using(var timer=new System.Windows.Forms.Timer{Interval=50})
        {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();
            int ticks=0;
            timer.Tick+=delegate{
                var dialog=Application.OpenForms.OfType<EmulatorDialog>().FirstOrDefault(f=>f.Text=="Add Emulator");
                if(dialog==null){if(++ticks>100)throw new Exception("Add Emulator dialog missing");return;}
                timer.Stop();((ComboBox)typeof(EmulatorDialog).GetField("preset",flags).GetValue(dialog)).SelectedItem=EmulatorCatalog.Find("Dolphin").Label;Field(dialog,"name","Workflow test emulator");Field(dialog,"executable",executable);
                var add=(Button)dialog.AcceptButton;VisibleAction(dialog,add);add.PerformClick();if(dialog.Profile!=null)dialog.Profile.Requirements="Configured test fixture";
            };
            timer.Start();typeof(MainForm).GetMethod("AddEmulatorWithPath",flags).Invoke(main,new object[]{executable});
            var live=(LibraryData)typeof(MainForm).GetField("library",flags).GetValue(main);
            Check(live.Emulators.Any(e=>e.Name=="Workflow test emulator"&&e.Executable==executable),"Main Add Emulator workflow adds record");
            Check(Store.Load().Emulators.Any(e=>e.Name=="Workflow test emulator"&&e.Executable==executable),"Added emulator persists on library reload");
            main.Close();
        }
    }
}
