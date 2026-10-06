using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using EmulatorHub;
class CosmeticTests {
    static int checks;
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string label) {if(!value) throw new Exception(label);checks++;}
    static string Hash(Image image) {using(var stream=new MemoryStream()) {image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToBase64String(SHA256.Create().ComputeHash(stream.ToArray()));}}
    static Bitmap Backdrop() {var image=new Bitmap(300,180);using(var graphics=Graphics.FromImage(image)) {graphics.Clear(FishBowlPalette.ThemeBottom);CosmeticRuntime.DrawBackdrop(graphics,new Rectangle(0,0,300,180));}return image;}
    static void Capture(Form form,string path) {Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path);}}
    static Control Field(Form form,string key) {return NextUi.Descendants(form).Single(c=>c.Name==key);}
    static void Click(Form form,string label) {NextUi.Descendants(form).OfType<Button>().Single(b=>b.Text==label).PerformClick();Application.DoEvents();}
    static void Dialog(MainForm main,Action<NextDialog> action) {
        using(var timer=new Timer {Interval=250}) {
            timer.Tick+=(s,e)=> {var dialog=Application.OpenForms.Cast<Form>().OfType<NextDialog>().SingleOrDefault(d=>d.Text=="Cosmetic styles");if(dialog==null) return;timer.Stop();action(dialog);};
            timer.Start();typeof(MainForm).GetMethod("ShowCosmetics",flags).Invoke(main,null);
        }
    }
    [STAThread] static int Main() {
        Application.EnableVisualStyles();Application.ThreadException+=(s,e)=> {Console.WriteLine(e.Exception);Environment.Exit(1);};
        Check(CosmeticRuntime.Parse("#Aa00ff").ToArgb()==Color.FromArgb(170,0,255).ToArgb(),"hex palette parses");
        bool rejected=false;try {CosmeticRuntime.Parse("#oops");} catch(ArgumentException){rejected=true;}Check(rejected,"invalid palette rejected");
        var settings=new CosmeticSettings();CosmeticRuntime.Configure(settings);
        string plain;using(var image=Backdrop()) plain=Hash(image);
        foreach(string style in new [] {"Dots","Waves","Gradient"}) {settings.BackgroundStyle=style;CosmeticRuntime.Configure(settings);using(var image=Backdrop()) Check(Hash(image)!=plain,style+" changes background");}
        using(var wallpaper=new Bitmap(80,60)) {using(var g=Graphics.FromImage(wallpaper)) {g.Clear(Color.OrangeRed);g.FillEllipse(Brushes.Blue,10,10,40,40);}wallpaper.Save("wallpaper.png");}
        settings.BackgroundStyle="Wallpaper";settings.WallpaperPath=Path.GetFullPath("wallpaper.png");
        foreach(string layout in new [] {"Fit","Fill","Tile"}) {settings.WallpaperLayout=layout;CosmeticRuntime.Configure(settings);using(var image=Backdrop()) Check(Hash(image)!=plain,"wallpaper "+layout);}
        settings.WallpaperOpacity=0;CosmeticRuntime.Configure(settings);using(var image=Backdrop()) Check(Hash(image)==plain,"zero opacity hides wallpaper");
        settings=new CosmeticSettings();CosmeticRuntime.Configure(settings);
        string outline;using(var image=FishBowlVisuals.Icon("folder",48,Color.White,Color.Cyan)) outline=Hash(image);
        foreach(string style in new [] {"Filled","Monochrome"}) {settings.IconStyle=style;CosmeticRuntime.Configure(settings);using(var image=FishBowlVisuals.Icon("folder",48,Color.White,Color.Cyan)) Check(Hash(image)!=outline,style+" icon rendering and cache");}
        settings.CornerRadius=0;CosmeticRuntime.Configure(settings);using(var path=FishBowlVisuals.Round(new RectangleF(0,0,100,40),6)) Check(path.PointCount==4,"square corners");
        settings.CornerRadius=15;using(var path=FishBowlVisuals.Round(new RectangleF(0,0,100,40),6)) Check(path.PointCount>4,"rounded corners");
        foreach(string frame in new [] {"Thin","Accent","Rounded"}) {settings.ArtworkFrame=frame;using(var art=new Bitmap(96,96)) {using(var g=Graphics.FromImage(art)) g.Clear(Color.Orange);string before=Hash(art);CosmeticRuntime.DecorateArtwork(art);Check(Hash(art)!=before,frame+" artwork frame");}}
        settings.ArtworkFrame="None";settings.ArtworkShadow=true;using(var art=new Bitmap(96,96)) {using(var g=Graphics.FromImage(art))g.Clear(Color.Orange);string before=Hash(art);CosmeticRuntime.DecorateArtwork(art);Check(Hash(art)!=before,"artwork shadow");}
        settings.BadgeStyle="Pill";settings.ReadyColor="#AF55CC";settings.RunningColor="#44AA66";settings.WarningColor="#DD9955";settings.SelectionColor="#214567";settings.FocusColor="#445599";
        Check(CosmeticRuntime.Status("Ready",Color.Red)==Color.FromArgb(175,85,204),"ready badge color");
        Check(CosmeticRuntime.Status("Running",Color.Red)==Color.FromArgb(68,170,102),"running badge color");
        Check(CosmeticRuntime.Status("Missing program",Color.Red)==Color.FromArgb(221,153,85),"warning badge color");
        Check(CosmeticRuntime.Selection==Color.FromArgb(33,69,103),"selection color");
        using(var image=new Bitmap(160,35)) using(var g=Graphics.FromImage(image)) Check(CosmeticRuntime.Badge(g,new Rectangle(0,0,160,35),"Ready",SystemFonts.DefaultFont),"badge paints");
        Check(FishBowlPalette.Contrast(CosmeticRuntime.Focus(Color.Black),Color.Black)>=4.5,"focus outline stays readable");
        CosmeticRuntime.Configure(new CosmeticSettings());
        using(var main=new MainForm(true)) {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.Show();Application.DoEvents();
            var data=(LibraryData)typeof(MainForm).GetField("library",flags).GetValue(main);
            data.Cosmetics=new CosmeticSettings();typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null);
            Dialog(main,dialog=> {
                var tabs=NextUi.Descendants(dialog).OfType<FishBowlTabs>().Single();Check(tabs.TabCount==4,"cosmetics organized in four tabs");
                ((CheckBox)Field(dialog,"CustomPalette")).Checked=true;
                ((TextBox)Field(dialog,"SurfaceColor")).Text="#203045";
                ((NumericUpDown)Field(dialog,"CornerRadius")).Value=12;
                ((ComboBox)Field(dialog,"IconStyle")).SelectedItem="Filled";
                ((ComboBox)Field(dialog,"BackgroundStyle")).SelectedItem="Dots";
                ((ComboBox)Field(dialog,"ArtworkFrame")).SelectedItem="Rounded";
                ((CheckBox)Field(dialog,"ArtworkShadow")).Checked=true;
                ((ComboBox)Field(dialog,"BadgeStyle")).SelectedItem="Pill";
                ((CheckBox)Field(dialog,"ShowFooter")).Checked=false;
                Check(FishBowlPalette.ThemeSurface==Color.FromArgb(32,48,69),"custom palette previews immediately");
                Check(!CosmeticRuntime.Current.ShowFooter && CosmeticRuntime.Current.CornerRadius==12,"footer and corners preview immediately");
                for(int tab=0;tab<tabs.TabCount;tab++) {tabs.SelectedIndex=tab;Capture(dialog,"cosmetics-tab-"+tab+".png");}
                Click(dialog,"Cancel");
            });
            Check(!data.Cosmetics.CustomPalette && data.Cosmetics.CornerRadius==6 && data.Cosmetics.ShowFooter,"Cancel restores uncommitted preferences");
            Dialog(main,dialog=> {
                ((NumericUpDown)Field(dialog,"CornerRadius")).Value=13;((CheckBox)Field(dialog,"IconOnlyToolbars")).Checked=true;
                Click(dialog,"Apply");
                ((NumericUpDown)Field(dialog,"CornerRadius")).Value=3;Click(dialog,"Revert");
                Check(data.Cosmetics.CornerRadius==13,"Revert uses last Apply");
                ((ComboBox)Field(dialog,"IconStyle")).SelectedItem="Monochrome";
                ((ComboBox)Field(dialog,"BackgroundStyle")).SelectedItem="Waves";
                ((CheckBox)Field(dialog,"ShowFooter")).Checked=false;
                Click(dialog,"Save and close");
            });
            var saved=Store.Load();Check(saved.Cosmetics!=null && saved.Cosmetics.CornerRadius==13 && saved.Cosmetics.IconStyle=="Monochrome" && !saved.Cosmetics.ShowFooter,"cosmetic choices persist");
            using(var library=new GameLibraryDialog(data)) {library.ShowInTaskbar=false;library.StartPosition=FormStartPosition.Manual;library.Location=new Point(-4000,-4000);library.Show();Application.DoEvents();
                var buttons=NextUi.Descendants(library).OfType<FishBowlActionButton>().Where(b=>b.Parent.Name=="FishBowlToolbar").ToList();
                Check(buttons.All(b=>b.IconOnly && !string.IsNullOrWhiteSpace(b.AccessibleName)),"icon-only toolbar preserves accessible labels");
                var tips=(ToolTip)typeof(CosmeticRuntime).GetField("tips",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);Check(buttons.All(b=>!string.IsNullOrWhiteSpace(tips.GetToolTip(b))),"icon-only buttons have tooltips");
                Capture(library,"cosmetic-library.png");
                data.Cosmetics.IconOnlyToolbars=false;CosmeticRuntime.Configure(data.Cosmetics);CosmeticRuntime.Apply(library);Check(buttons.All(b=>!b.IconOnly && b.Width>=100),"toolbar text mode restores widths");
            }
            data.Cosmetics=new CosmeticSettings {BackgroundStyle="Dots",ArtworkFrame="Rounded",ArtworkShadow=true,IconStyle="Filled",CornerRadius=12,BadgeStyle="Pill",ShowFooter=false};
            typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null);Capture(main,"cosmetic-home.png");
            data.Enhancements.TextPercent=200;typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null);
            Dialog(main,dialog=> {
                var text=Field(dialog,"TextColor");var row=(TableLayoutPanel)text.Parent;
                var picker=row.Controls.OfType<Button>().Single();
                Check(picker.Height>=50 && picker.Bottom<=row.ClientSize.Height,"large text keeps color chooser visible");
                Check(text.Right<=row.ClientSize.Width && picker.Right<=row.ClientSize.Width,"color inputs fit their row");
                Check(NextUi.Descendants(dialog).OfType<FishBowlTabs>().Single().ItemSize.Width>=190,"large text tabs retain useful widths");
                Capture(dialog,"cosmetics-large-text.png");Click(dialog,"Cancel");
            });
            data.Enhancements.TextPercent=100;typeof(MainForm).GetMethod("ApplyAppearanceNow",flags).Invoke(main,null);
            CosmeticRuntime.Validate(data.Cosmetics);
            Store.Save(data);
        }
        Console.WriteLine("PASS: "+checks+" cosmetic rendering, persistence, preview, Apply, Revert, Cancel and accessibility checks.");return 0;
    }
}
