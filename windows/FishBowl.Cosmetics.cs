using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EmulatorHub {
    public class CosmeticSettings {
        public string LibrarySpacing {get;set;} public bool PlatformLabels {get;set;} public bool DetailPreview {get;set;}
        public bool CustomPalette {get;set;}
        public string TextColor {get;set;}
        public string MutedColor {get;set;}
        public string TopColor {get;set;}
        public string BottomColor {get;set;}
        public string SurfaceColor {get;set;}
        public string AccentColor {get;set;}
        public string SecondaryColor {get;set;}
        public int CornerRadius {get;set;}
        public string IconStyle {get;set;}
        public bool IconOnlyToolbars {get;set;}
        public string BackgroundStyle {get;set;}
        public string WallpaperPath {get;set;}
        public string WallpaperLayout {get;set;}
        public int WallpaperOpacity {get;set;}
        public string ArtworkFrame {get;set;}
        public bool ArtworkShadow {get;set;}
        public string SelectionColor {get;set;}
        public string FocusColor {get;set;}
        public int FocusWidth {get;set;}
        public string BadgeStyle {get;set;}
        public string ReadyColor {get;set;}
        public string RunningColor {get;set;}
        public string WarningColor {get;set;}
        public bool ShowFooter {get;set;}
        public CosmeticSettings() {
            TextColor="#F1F7FF"; MutedColor="#BED3F0"; TopColor="#051F4E"; BottomColor="#030D26"; SurfaceColor="#0C3B7A"; AccentColor="#59BEFF"; SecondaryColor="#43DCBB";
            LibrarySpacing="Comfortable"; CornerRadius=6; IconStyle="Outline"; BackgroundStyle="Plain"; WallpaperLayout="Fill"; WallpaperOpacity=15;
            ArtworkFrame="None"; FocusWidth=2; BadgeStyle="Text"; ShowFooter=true;
        }
    }
    public class CosmeticCardPanel : FlowLayoutPanel {
        public CosmeticCardPanel() {DoubleBuffered=true;}
        protected override void OnPaintBackground(PaintEventArgs e) {base.OnPaintBackground(e); CosmeticRuntime.DrawBackdrop(e.Graphics,ClientRectangle);}
    }
    public static class CosmeticRuntime {
        public static CosmeticSettings Current=new CosmeticSettings();
        private static Image wallpaper;
        private static string wallpaperKey;
        private static readonly ToolTip tips=new ToolTip();
        private class ButtonSize {public int Width; public bool AutoSize; public Size Minimum;}
        private static readonly ConditionalWeakTable<Button,ButtonSize> sizes=new ConditionalWeakTable<Button,ButtonSize>();
        public static Color Parse(string hex) {
            if(!Regex.IsMatch(hex??"","^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Use colors in #RRGGBB format, such as #59BEFF.");
            return ColorTranslator.FromHtml(hex);
        }
        public static void Validate(CosmeticSettings settings) {
            if(settings.CustomPalette) foreach(var value in new [] {settings.TextColor,settings.MutedColor,settings.TopColor,settings.BottomColor,settings.SurfaceColor,settings.AccentColor,settings.SecondaryColor}) Parse(value);
            foreach(var value in new [] {settings.SelectionColor,settings.FocusColor,settings.ReadyColor,settings.RunningColor,settings.WarningColor}) if(!string.IsNullOrWhiteSpace(value)) Parse(value);
            if(settings.BackgroundStyle=="Wallpaper" && !File.Exists(settings.WallpaperPath)) throw new IOException("Choose an existing wallpaper image before using the Wallpaper background.");
        }
        public static void Configure(CosmeticSettings settings) {
            Current=settings??new CosmeticSettings();
            string key=Current.WallpaperPath??"";
            if(key==wallpaperKey) return;
            Image replacement=null;
            try {if(File.Exists(key)) using(var original=Image.FromFile(key)) replacement=new Bitmap(original);} catch { }
            if(wallpaper!=null) wallpaper.Dispose(); wallpaper=replacement; wallpaperKey=key;
        }
        public static Color Optional(string value,Color fallback) {try {return string.IsNullOrWhiteSpace(value)?fallback:Parse(value);} catch {return fallback;}}
        public static Color Selection {get {return Optional(Current.SelectionColor,FishBowlPalette.MenuSelection);}}
        public static Color Focus(Color background) {return FishBowlPalette.EnsureReadable(Optional(Current.FocusColor,FishBowlPalette.IconAccent),background);}
        public static Color Status(string text,Color fallback) {
            string t=(text??"").ToLowerInvariant();
            if(t=="ready") return Optional(Current.ReadyColor,fallback);
            if(t=="running" || t=="playing") return Optional(Current.RunningColor,fallback);
            if(t.Contains("missing") || t.Contains("attention") || t.Contains("warning")) return Optional(Current.WarningColor,fallback);
            return fallback;
        }
        public static bool Badge(Graphics graphics,Rectangle bounds,string text,Font font) {
            string t=(text??"").ToLowerInvariant();
            if(Current.BadgeStyle=="Text" || !(t=="ready" || t=="running" || t=="playing" || t.Contains("missing") || t.Contains("attention"))) return false;
            Color color=Status(text,FishBowlPalette.IconAccent);
            int width=Math.Min(bounds.Width-6,TextRenderer.MeasureText(text,font).Width+16);
            if(width<8) return false;
            Rectangle rect=new Rectangle(bounds.X+3,bounds.Y+3,width,Math.Max(12,bounds.Height-6));
            using(var path=Shape(rect,Current.BadgeStyle=="Pill"?rect.Height/2f:0)) using(var fill=new SolidBrush(color)) graphics.FillPath(fill,path);
            FishBowlText.DrawText(graphics,text,font,rect,FishBowlPalette.EnsureReadable(FishBowlPalette.ThemeInk,color),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            return true;
        }
        private static GraphicsPath Shape(RectangleF rect,float radius) {
            var path=new GraphicsPath(); float d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
            if(d<=0) path.AddRectangle(rect); else {path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();} return path;
        }
        public static void DrawBackdrop(Graphics graphics,Rectangle bounds) {
            if(bounds.Width<1 || bounds.Height<1) return;
            if(Current.BackgroundStyle=="Gradient") using(var gradient=new LinearGradientBrush(bounds,Color.FromArgb(50,FishBowlPalette.IconAccent),Color.Transparent,LinearGradientMode.ForwardDiagonal)) graphics.FillRectangle(gradient,bounds);
            if(Current.BackgroundStyle=="Dots") using(var brush=new SolidBrush(Color.FromArgb(30,FishBowlPalette.ThemeInk))) for(int x=bounds.X+8;x<bounds.Right;x+=22) for(int y=bounds.Y+8;y<bounds.Bottom;y+=22) graphics.FillEllipse(brush,x,y,2,2);
            if(Current.BackgroundStyle=="Waves") using(var pen=new Pen(Color.FromArgb(25,FishBowlPalette.IconAccent),1)) for(int y=20;y<bounds.Height;y+=36) graphics.DrawBezier(pen,bounds.Left,y,bounds.Width/3,y-20,bounds.Width*2/3,y+20,bounds.Right,y);
            if(wallpaper==null || Current.BackgroundStyle!="Wallpaper") return;
            using(var attributes=new ImageAttributes()) {
                var matrix=new ColorMatrix();matrix.Matrix33=Math.Max(0,Math.Min(40,Current.WallpaperOpacity))/100f; attributes.SetColorMatrix(matrix);
                if(Current.WallpaperLayout=="Tile") {for(int x=0;x<bounds.Width;x+=wallpaper.Width) for(int y=0;y<bounds.Height;y+=wallpaper.Height) graphics.DrawImage(wallpaper,new Rectangle(x,y,wallpaper.Width,wallpaper.Height),0,0,wallpaper.Width,wallpaper.Height,GraphicsUnit.Pixel,attributes);}
                else {float scale=Current.WallpaperLayout=="Fit"?Math.Min((float)bounds.Width/wallpaper.Width,(float)bounds.Height/wallpaper.Height):Math.Max((float)bounds.Width/wallpaper.Width,(float)bounds.Height/wallpaper.Height); int w=(int)(wallpaper.Width*scale),h=(int)(wallpaper.Height*scale); graphics.DrawImage(wallpaper,new Rectangle(bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h),0,0,wallpaper.Width,wallpaper.Height,GraphicsUnit.Pixel,attributes);}
            }
        }
        public static void DecorateArtwork(Bitmap bitmap) {
            if(Current.ArtworkFrame=="None" && !Current.ArtworkShadow) return;
            using(var original=new Bitmap(bitmap)) using(var graphics=Graphics.FromImage(bitmap)) {
                graphics.Clear(FishBowlPalette.ThemeSurface); graphics.SmoothingMode=SmoothingMode.AntiAlias;
                int inset=Current.ArtworkShadow?5:3;
                Rectangle frame=new Rectangle(inset,inset,Math.Max(1,bitmap.Width-inset*2-2),Math.Max(1,bitmap.Height-inset*2-2));
                if(Current.ArtworkShadow) for(int spread=4;spread>=1;spread--) using(var shadow=new SolidBrush(Color.FromArgb(10,Color.Black))) using(var path=Shape(new Rectangle(frame.X+2-spread/2,frame.Y+3-spread/2,frame.Width+spread,frame.Height+spread),6)) graphics.FillPath(shadow,path);
                using(var path=Shape(frame,Current.ArtworkFrame=="Rounded"?8:0)) {var state=graphics.Save();graphics.SetClip(path);graphics.DrawImage(original,frame);graphics.Restore(state); if(Current.ArtworkFrame!="None") using(var pen=new Pen(FishBowlPalette.IconAccent,Current.ArtworkFrame=="Accent"?3:1)) graphics.DrawPath(pen,path);}
            }
        }
        public static void Apply(Control root) {
            foreach(var button in NextUi.Descendants(root).Concat(new [] {root}).OfType<FishBowlActionButton>()) {
                var bar=button.Parent as FlowLayoutPanel;
                bool toolbar=bar!=null && bar.Name=="FishBowlToolbar";
                button.RefreshCosmeticIcon();
                if(!toolbar) continue;
                ButtonSize original;
                if(!sizes.TryGetValue(button,out original)) {original=new ButtonSize {Width=button.Width,AutoSize=button.AutoSize,Minimum=button.MinimumSize}; sizes.Add(button,original);}
                button.IconOnly=Current.IconOnlyToolbars;
                if(button.IconOnly) {button.AutoSize=false;button.MinimumSize=new Size(40,original.Minimum.Height);button.Width=Math.Max(40,(int)(button.Font.Size*4.5));tips.SetToolTip(button,button.AccessibleName??button.Text);}
                else {button.MinimumSize=original.Minimum;button.Width=original.Width;button.AutoSize=original.AutoSize;tips.SetToolTip(button,"");}
                button.Invalidate();
            }
            root.Invalidate(true);
        }
    }
    public partial class MainForm {
        private void ApplyCosmeticPalette() {
            CosmeticRuntime.Configure(library.Cosmetics);
            var settings=CosmeticRuntime.Current;
            if(!settings.CustomPalette) return;
            try {ink=CosmeticRuntime.Parse(settings.TextColor);subtle=CosmeticRuntime.Parse(settings.MutedColor);top=CosmeticRuntime.Parse(settings.TopColor);bottom=CosmeticRuntime.Parse(settings.BottomColor);surface=CosmeticRuntime.Parse(settings.SurfaceColor);blue=CosmeticRuntime.Parse(settings.AccentColor);pink=CosmeticRuntime.Parse(settings.SecondaryColor);} catch { }
        }
        private void ShowCosmetics() {
            if(library.Cosmetics==null) library.Cosmetics=new CosmeticSettings();
            CosmeticSettings committed=NextData.Copy(library.Cosmetics);
            bool updating=false;
            using(var dialog=new NextDialog("Cosmetic styles",920,700)) {
                dialog.Actions.Tag="FishBowl overflow";
                dialog.Actions.WrapContents=true;
                var tabs=new FishBowlTabs {Dock=DockStyle.Fill,PreferredColumns=4,ItemSize=new Size(195,34)};
                dialog.Body.Controls.Add(tabs);
                var controls=new Dictionary<string,Control>();
                var fields=new Dictionary<string,TableLayoutPanel>();
                foreach(var name in new [] {"Palette","Shapes and icons","Background","Artwork and badges"}) {var page=new TabPage(name) {AutoScroll=true,UseVisualStyleBackColor=false,BackColor=FishBowlPalette.DeepSeaSurface,ForeColor=FishBowlPalette.ThemeInk};tabs.TabPages.Add(page);fields[name]=NextDialog.Fields(page);}
                Action<string,string,string> color=(tab,label,key)=> {var row=new TableLayoutPanel {ColumnCount=2,RowCount=1};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,63));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37)); var text=new TextBox {Dock=DockStyle.Fill,AccessibleName=label,Text=(string)typeof(CosmeticSettings).GetProperty(key).GetValue(committed,null)??""}; var pick=ExperienceUi.Button("Choose",()=> {using(var chooser=new ColorDialog {Color=CosmeticRuntime.Optional(text.Text,FishBowlPalette.IconAccent),FullOpen=true}) if(chooser.ShowDialog(dialog)==DialogResult.OK) text.Text="#"+chooser.Color.R.ToString("X2")+chooser.Color.G.ToString("X2")+chooser.Color.B.ToString("X2");}); pick.Dock=DockStyle.Fill;pick.MinimumSize=Size.Empty; pick.AccessibleName="Choose "+label; row.Controls.Add(text,0,0);row.Controls.Add(pick,1,0);controls[key]=text;NextDialog.Field(fields[tab],label,row,48);};
                Action<string,string,string,string[]> choice=(tab,label,key,values)=> {var box=NextDialog.Choice(values,(string)typeof(CosmeticSettings).GetProperty(key).GetValue(committed,null));controls[key]=box;NextDialog.Field(fields[tab],label,box);};
                Action<string,string,string> toggle=(tab,label,key)=> {var box=new CheckBox {Text=label,Checked=(bool)typeof(CosmeticSettings).GetProperty(key).GetValue(committed,null)};controls[key]=box;NextDialog.Field(fields[tab],"",box);};
                Action<string,string,string,int,int> number=(tab,label,key,min,max)=> {var box=NextDialog.Number((int)typeof(CosmeticSettings).GetProperty(key).GetValue(committed,null),min,max);controls[key]=box;NextDialog.Field(fields[tab],label,box);};
                toggle("Palette","Use custom palette","CustomPalette");
                color("Palette","Main text","TextColor");color("Palette","Secondary text","MutedColor");color("Palette","Header background","TopColor");color("Palette","Page background","BottomColor");color("Palette","Card background","SurfaceColor");color("Palette","Accent","AccentColor");color("Palette","Secondary accent","SecondaryColor");
                number("Shapes and icons","Corner radius (0 = square)","CornerRadius",0,20);
                choice("Shapes and icons","Icon style","IconStyle",new [] {"Outline","Filled","Monochrome"});
                toggle("Shapes and icons","Icon-only toolbars with tooltips","IconOnlyToolbars");
                choice("Shapes and icons","Library spacing","LibrarySpacing",new[]{"Compact","Comfortable"});toggle("Artwork and badges","Show platform labels","PlatformLabels");toggle("Artwork and badges","Cover and details pane","DetailPreview");
                color("Shapes and icons","Selection color (blank = theme)","SelectionColor");color("Shapes and icons","Focus outline (blank = theme)","FocusColor");number("Shapes and icons","Focus outline width","FocusWidth",1,5);
                choice("Background","Background style","BackgroundStyle",new [] {"Plain","Gradient","Dots","Waves","Wallpaper"});
                var wallpaper=new TextBox {Text=committed.WallpaperPath??""};controls["WallpaperPath"]=wallpaper;NextDialog.Field(fields["Background"],"Wallpaper image",wallpaper);
                var browse=ExperienceUi.Button("Choose wallpaper",()=> {using(var file=new OpenFileDialog {Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"}) if(file.ShowDialog(dialog)==DialogResult.OK) wallpaper.Text=file.FileName;});NextDialog.Field(fields["Background"],"",browse);
                choice("Background","Wallpaper layout","WallpaperLayout",new [] {"Fill","Fit","Tile"});number("Background","Wallpaper opacity (%)","WallpaperOpacity",0,40);toggle("Background","Show aquarium footer decoration","ShowFooter");
                choice("Artwork and badges","Artwork frame","ArtworkFrame",new [] {"None","Thin","Accent","Rounded"});toggle("Artwork and badges","Soft artwork shadows","ArtworkShadow");
                choice("Artwork and badges","Status badge style","BadgeStyle",new [] {"Text","Pill","Square"});
                color("Artwork and badges","Ready color (blank = theme)","ReadyColor");color("Artwork and badges","Running color (blank = theme)","RunningColor");color("Artwork and badges","Warning color (blank = theme)","WarningColor");
                foreach(var table in fields.Values) foreach(RowStyle row in table.RowStyles) row.Height=(float)Math.Ceiling(row.Height*Math.Max(1,NextUi.TextPercent/100.0));
                var message=ExperienceUi.Label("Preview changes on the app behind this window. Text contrast is adjusted automatically.",(int)Math.Ceiling(42*Math.Max(1,NextUi.TextPercent/100.0)));message.Dock=DockStyle.Bottom;dialog.Body.Controls.Add(message);
                Func<CosmeticSettings> read=()=> {var result=NextData.Copy(committed);foreach(var pair in controls) {object value=pair.Value is CheckBox ? (object)((CheckBox)pair.Value).Checked : pair.Value is NumericUpDown ? (object)(int)((NumericUpDown)pair.Value).Value : pair.Value.Text.Trim();typeof(CosmeticSettings).GetProperty(pair.Key).SetValue(result,value,null);} CosmeticRuntime.Validate(result);return result;};
                Action preview=()=> {if(updating) return;try {library.Cosmetics=read();ApplyAppearanceNow();message.Text="Preview active. Use Apply or Save and close to keep it.";} catch(Exception error) {message.Text=error.Message;}};
                Action<CosmeticSettings> set=settings=> {updating=true;foreach(var pair in controls) {object value=typeof(CosmeticSettings).GetProperty(pair.Key).GetValue(settings,null);if(pair.Value is CheckBox) ((CheckBox)pair.Value).Checked=(bool)value;else if(pair.Value is NumericUpDown) ((NumericUpDown)pair.Value).Value=(int)value;else if(pair.Value is ComboBox) ((ComboBox)pair.Value).SelectedItem=value;else pair.Value.Text=(string)value??"";}updating=false;preview();};
                foreach(var pair in controls) {var control=pair.Value;control.Name=pair.Key;if(control is CheckBox) ((CheckBox)control).CheckedChanged+=(s,e)=>preview();else if(control is NumericUpDown) ((NumericUpDown)control).ValueChanged+=(s,e)=>preview();else if(control is ComboBox) ((ComboBox)control).SelectedIndexChanged+=(s,e)=>preview();else control.TextChanged+=(s,e)=>preview();}
                Action save=()=> {library.Cosmetics=read();committed=NextData.Copy(library.Cosmetics);Store.Save(library);ApplyAppearanceNow();};
                dialog.Action("Apply",save);dialog.Action("Revert",()=>set(committed));dialog.Action("Restore defaults",()=>set(new CosmeticSettings()));dialog.Action("Save and close",()=>{save();dialog.Close();});dialog.Action("Cancel",dialog.Close);
                dialog.Shown+=(s,e)=> {foreach(var button in dialog.Actions.Controls.OfType<Button>()) {button.Width=Math.Max(116,TextRenderer.MeasureText(button.Text,button.Font).Width+58);button.Height=Math.Max(34,button.Font.Height+18);}};
                try {dialog.ShowDialog(this);} finally {library.Cosmetics=committed;ApplyAppearanceNow();}
            }
        }
    }
}
