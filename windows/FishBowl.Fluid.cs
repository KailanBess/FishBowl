using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Linq;using System.Runtime.CompilerServices;using System.Windows.Forms;
namespace EmulatorHub {
 public static class FishBowlText {
  public static void DrawText(IDeviceContext dc,string text,Font font,Rectangle bounds,Color foreColor,TextFormatFlags flags){TextRenderer.DrawText(dc,text,font,bounds,foreColor,flags|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform);}
  public static void DrawText(IDeviceContext dc,string text,Font font,Point point,Color foreColor){TextRenderer.DrawText(dc,text,font,point,foreColor,TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform);}
 }
 public static class NativeSurfacePainting {
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern int GetUpdateRgn(IntPtr window,IntPtr region,bool erase);
  [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
  [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
  public static Region UpdateRegion(IntPtr window){IntPtr region=CreateRectRgn(0,0,0,0);if(region==IntPtr.Zero)return null;try{int kind=GetUpdateRgn(window,region,false);return kind>1?Region.FromHrgn(region):null;}finally{DeleteObject(region);}}
  public static void ExcludeChildren(Graphics graphics,Control parent){foreach(Control child in parent.Controls)if(child.Visible)graphics.ExcludeClip(child.Bounds);} }
 public class BufferedPromptTable : TableLayoutPanel {public BufferedPromptTable(){DoubleBuffered=true;}}
 public static class StartupPromptLayout {
  public static bool IsPrompt(Form f){return f is StartupAssistantDialog||f is GameStoragePromptDialog||f is RequirementsStoragePromptDialog||f is WhatsNewDialog;}
  public static void Apply(Form form){if(!IsPrompt(form))return;
   form.SuspendLayout();try{
    var area=Screen.FromControl(form).WorkingArea;int width=Math.Min(Math.Max(570,(int)(570*Math.Min(1.3,NextUi.TextPercent/100.0))),Math.Max(300,area.Width-48));
    var originals=form.Controls.Cast<Control>().ToArray();var labels=originals.OfType<Label>().OrderBy(c=>c.Top).ToArray();var title=labels.First();var logo=originals.OfType<PictureBox>().FirstOrDefault();var buttons=originals.OfType<Button>().OrderBy(c=>c.Left).ToArray();
    form.Controls.Clear();form.AutoScroll=false;form.AutoScrollMinSize=Size.Empty;
    var root=new BufferedPromptTable{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(20),BackColor=form.BackColor,Name="StartupPromptRoot"};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    var header=new BufferedPromptTable{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=logo==null?1:2,RowCount=1,Margin=new Padding(0,0,0,16),BackColor=form.BackColor};if(logo!=null){header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,60));logo.Dock=DockStyle.Fill;logo.MinimumSize=new Size(46,46);logo.Margin=new Padding(0,0,14,0);header.Controls.Add(logo,0,0);}header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));title.AutoSize=true;title.MaximumSize=new Size(width-40-(logo==null?0:60),0);title.Dock=DockStyle.Fill;title.Margin=Padding.Empty;title.BackColor=form.BackColor;header.Controls.Add(title,logo==null?0:1,0);root.Controls.Add(header,0,0);
    var body=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0,0,0,16),BackColor=form.BackColor,Name="StartupPromptBody"};int bodyHeight=0;
    foreach(var label in labels.Skip(1)){label.AutoSize=true;label.MaximumSize=new Size(width-40-SystemInformation.VerticalScrollBarWidth,0);label.Dock=DockStyle.Top;label.Margin=Padding.Empty;label.BackColor=form.BackColor;body.Controls.Add(label);bodyHeight+=label.GetPreferredSize(new Size(label.MaximumSize.Width,0)).Height+12;}
    foreach(var box in originals.OfType<TextBox>()){box.Dock=DockStyle.Fill;box.ScrollBars=ScrollBars.Vertical;box.WordWrap=true;box.TabStop=false;box.Select(0,0);box.BackColor=form.BackColor;body.Controls.Add(box);bodyHeight=Math.Max(bodyHeight,(int)(220*Math.Max(1,NextUi.TextPercent/100.0)));}root.Controls.Add(body,0,1);
    var footer=new BufferedPromptTable{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=2,Margin=Padding.Empty,BackColor=form.BackColor};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    foreach(var check in originals.OfType<CheckBox>()){check.AutoSize=true;check.MaximumSize=new Size(width-40,0);check.Dock=DockStyle.Fill;check.Margin=new Padding(0,0,0,16);check.BackColor=form.BackColor;footer.Controls.Add(check,0,0);}
    var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.RightToLeft,WrapContents=true,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=form.BackColor};foreach(var button in buttons.Reverse()){button.AutoSize=false;button.Size=new Size(Math.Max(110,TextRenderer.MeasureText(button.Text,button.Font).Width+32),Math.Max(38,button.Font.Height+16));button.Margin=new Padding(10,0,0,0);actions.Controls.Add(button);}footer.Controls.Add(actions,0,1);root.Controls.Add(footer,0,2);
    int headerHeight=Math.Max(46,title.GetPreferredSize(new Size(title.MaximumSize.Width,0)).Height)+16;int footerHeight=footer.GetPreferredSize(new Size(width-40,0)).Height;int height=40+headerHeight+Math.Max(120,bodyHeight)+16+footerHeight;form.ClientSize=new Size(width,Math.Min(height,Math.Max(240,area.Height-80)));form.Controls.Add(root);form.AcceptButton=buttons.FirstOrDefault();form.CancelButton=buttons.FirstOrDefault();
   }finally{form.ResumeLayout(true);}
  }
 }
 public static class ThemeCatalog { public static readonly string[] Names = new string[]{"FishBowl Water","Twilight","Lavender","Ember","Light","High Contrast","Midnight","Forest","Rosewood","Mist","Deep Ocean","Aurora","Slate","Plum","Sand","Paper","Nordic","Copper"}; }
 public static class FluidStyle {
  public static bool Motion=true,Transitions=true,Roomier=true,Bubbles=true;
  public static event Action Changed;
  public static void NotifyMotionChange(){var changed=Changed;if(changed!=null)changed();}
  public static bool Animate {get{return Motion&&Transitions&&!FishBowlHighlights.ReducedMotion&&!SystemInformation.HighContrast;}}
  public static void Configure(LibraryData d){var s=Immersion.Ensure(d);bool different=Motion!=d.Theme.EnableMotion||Transitions!=s.Transitions||Roomier!=s.Roomier||Bubbles!=s.Bubbles;Motion=d.Theme.EnableMotion;Transitions=s.Transitions;Roomier=s.Roomier;Bubbles=s.Bubbles;if(!different)return;var changed=Changed;if(changed!=null)changed();}
 }
 public class BubbleRail : Control {
  readonly double[] positions=new double[8];readonly float[] sizes=new float[8];readonly double[] speeds=new double[8],phases=new double[8];readonly Random random;readonly bool right;
  public BubbleRail(bool rightSide){right=rightSide;random=new Random(rightSide?9173:4289);Width=24;TabStop=false;AccessibleName="Decorative theme bubbles";SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);for(int i=0;i<8;i++){positions[i]=(i+random.NextDouble()*.85)/8;ResetBubble(i);}}
  void ResetBubble(int i){sizes[i]=6+(float)random.NextDouble()*9;speeds[i]=13+random.NextDouble()*22;phases[i]=random.NextDouble()*Math.PI*2;}
  public float[] Sizes{get{return (float[])sizes.Clone();}}public double[] Speeds{get{return (double[])speeds.Clone();}}
  public void Advance(double seconds){if(ClientSize.Height<1)return;for(int i=0;i<positions.Length;i++){positions[i]-=Math.Max(0,Math.Min(.1,seconds))*speeds[i]/Math.Max(1,Height);if(positions[i]<-.03){positions[i]=1.03+random.NextDouble()*.08;ResetBubble(i);}}Invalidate();}
  public double[] Positions {get{return (double[])positions.Clone();}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(FishBowlPalette.ThemeBottom);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color light,deep;FishBowlBranding.GetColors(out light,out deep);for(int i=0;i<positions.Length;i++){float size=Math.Min(sizes[i],Width-4);float x=(Width-size)/2+(float)Math.Sin(positions[i]*9+phases[i])*(right?-2:2);float y=(float)(positions[i]*Height);var bounds=new RectangleF(x,y,size,size);using(var fill=new LinearGradientBrush(bounds,Color.FromArgb(65,light),Color.FromArgb(32,deep),45f))using(var outline=new LinearGradientBrush(bounds,Color.FromArgb(210,light),Color.FromArgb(170,deep),45f))using(var ring=new Pen(outline,1.2f))using(var glint=new Pen(Color.FromArgb(175,Color.White),1f)){e.Graphics.FillEllipse(fill,bounds);e.Graphics.DrawEllipse(ring,bounds);e.Graphics.DrawArc(glint,x+size*.2f,y+size*.18f,size*.5f,size*.5f,200,65);}}}

 }
 public class AquariumFrame : Panel {
  public readonly BubbleRail LeftRail=new BubbleRail(false),RightRail=new BubbleRail(true);readonly Timer clock;readonly Stopwatch watch=new Stopwatch();Form owner;bool disposed;
  public AquariumFrame(Control content){Dock=DockStyle.Fill;DoubleBuffered=true;LeftRail.Dock=DockStyle.Left;RightRail.Dock=DockStyle.Right;content.Dock=DockStyle.Fill;Controls.Add(content);Controls.Add(LeftRail);Controls.Add(RightRail);clock=new Timer{Interval=40};clock.Tick+=(a,b)=>{double seconds=Math.Min(.1,watch.Elapsed.TotalSeconds);watch.Restart();if(!ShouldAnimate()){ApplyState();return;}LeftRail.Advance(seconds);RightRail.Advance(seconds);};VisibleChanged+=(a,b)=>ApplyState();ParentChanged+=(a,b)=>ConnectOwner();HandleCreated+=(a,b)=>ConnectOwner();FluidStyle.Changed+=ApplyState;ApplyState();}
  void ConnectOwner(){var form=FindForm();if(owner==form)return;if(owner!=null){owner.Activated-=OwnerState;owner.Deactivate-=OwnerState;owner.Resize-=OwnerState;}owner=form;if(owner!=null){owner.Activated+=OwnerState;owner.Deactivate+=OwnerState;owner.Resize+=OwnerState;}ApplyState();}
  void OwnerState(object sender,EventArgs e){ApplyState();}
  public bool ShouldAnimate(){return FluidStyle.Bubbles&&FluidStyle.Motion&&!FishBowlHighlights.ReducedMotion&&!SystemInformation.HighContrast&&Visible&&owner!=null&&owner.WindowState!=FormWindowState.Minimized&&Form.ActiveForm==owner;}
  public bool Running {get{return clock.Enabled;}}
  public void ApplyState(){if(disposed)return;if(LeftRail.Visible!=FluidStyle.Bubbles)LeftRail.Visible=FluidStyle.Bubbles;if(RightRail.Visible!=FluidStyle.Bubbles)RightRail.Visible=FluidStyle.Bubbles;LeftRail.Invalidate();RightRail.Invalidate();bool run=ShouldAnimate();if(clock.Enabled!=run){clock.Enabled=run;watch.Restart();}}
  protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;FluidStyle.Changed-=ApplyState;clock.Dispose();if(owner!=null){owner.Activated-=OwnerState;owner.Deactivate-=OwnerState;owner.Resize-=OwnerState;}}base.Dispose(disposing);}
 }
 // Animate only the custom tab header: never snapshot or cover native child windows.
 public sealed class SectionTransition : IDisposable {
  readonly TabControl tabs;readonly Timer clock;readonly Stopwatch watch=new Stopwatch();bool disposed;
  public float Progress=1;
  public SectionTransition(TabControl control){tabs=control;clock=new Timer{Interval=25};clock.Tick+=(a,b)=>{if(!FluidStyle.Animate){Cancel();return;}Progress=(float)Math.Min(1,watch.Elapsed.TotalMilliseconds/150);InvalidateHeader();if(Progress>=1)Cancel();};tabs.SelectedIndexChanged+=Selected;tabs.SizeChanged+=Resized;tabs.FontChanged+=Resized;tabs.HandleDestroyed+=Resized;tabs.Disposed+=Closed;FluidStyle.Changed+=Cancel;}
  public bool Running {get{return clock.Enabled;}}public bool HasImages {get{return false;}}
  void InvalidateHeader(){if(!tabs.IsDisposed&&tabs.SelectedIndex>=0)tabs.Invalidate(tabs.GetTabRect(tabs.SelectedIndex));}
  void Selected(object sender,EventArgs e){Cancel();}

  void Resized(object sender,EventArgs e){Cancel();}void Closed(object sender,EventArgs e){Dispose();}
  public void Cancel(){clock.Stop();Progress=1;InvalidateHeader();}
  public void Dispose(){if(disposed)return;Cancel();disposed=true;clock.Dispose();FluidStyle.Changed-=Cancel;tabs.SelectedIndexChanged-=Selected;tabs.SizeChanged-=Resized;tabs.FontChanged-=Resized;tabs.HandleDestroyed-=Resized;tabs.Disposed-=Closed;}
 }
 public static class SectionMotion {
  static readonly ConditionalWeakTable<TabControl,SectionTransition> transitions=new ConditionalWeakTable<TabControl,SectionTransition>();
  public static SectionTransition For(TabControl tabs){return transitions.GetValue(tabs,t=>new SectionTransition(t));}
  public static void Attach(Control root){foreach(var tab in NextUi.Descendants(root).OfType<TabControl>().ToArray())For(tab);}
  // Layered-window opacity changes can flicker native controls and startup dialogs.
  public static void Reveal(Form form){}

 }
 public partial class MainForm {
  void UpdateFluidHeader(){if(workspaceShell==null)return;var header=NextUi.Descendants(this).FirstOrDefault(c=>c.Name=="FishBowlHeader");if(header==null)return;var title=header.Controls.OfType<Label>().FirstOrDefault(c=>c.Text=="FishBowl");var subtitle=header.Controls.OfType<Label>().FirstOrDefault(c=>c!=title);var icon=header.Controls.OfType<PictureBox>().FirstOrDefault();if(title==null||subtitle==null)return;int left=icon==null?84:icon.Right+12;title.Location=new Point(left,8);subtitle.Location=new Point(left,Math.Max(43,title.Bottom+3));workspaceShell.RowStyles[1].Height=library.Theme.ShowBanner?Math.Max(Math.Max(76,subtitle.Bottom+10),icon==null?0:icon.Bottom+8):0;}
 }
 public partial class GameLibraryDialog {
  ulong? visitStamp;DateTime visitScan;public int VisitRefreshCount {get;private set;}
  static void Mix(ref ulong hash,string value){unchecked{hash=(hash^(uint)(value==null?0:value.GetHashCode()))*1099511628211UL;}}
  ulong VisitStamp(){ulong hash=1469598103934665603UL;foreach(var g in library.Games){foreach(var text in new[]{g.Id,g.Title,g.Path,g.EmulatorId,g.PreferredEmulatorId,g.ArtworkPath,g.LastLaunched,g.AddedAt,g.Genre,g.Developer,g.ReleaseYear,g.ConsoleLabel,g.PlayStatus,g.Notes,g.Description,g.ManualPath,g.Arguments,g.TitleId,g.EmulatorCore,g.CompatibilityNotes,g.LaunchProfileName,g.PreferredBuildId,g.ControllerProfileId,g.LastDiscPath,g.SaveCopyPreference,g.SessionTrackingNote})Mix(ref hash,text);unchecked{hash=(hash^(ulong)g.TotalPlaySeconds^(ulong)g.LaunchCount^(ulong)g.PersonalRating^(g.Pinned?71UL:0)^(g.Favorite?131UL:0))*1099511628211UL;}foreach(var text in g.Tags??new List<string>())Mix(ref hash,text);foreach(var text in g.Discs??new List<string>())Mix(ref hash,text);foreach(var save in g.Saves??new List<GameSaveEntry>()){Mix(ref hash,save.Path);Mix(ref hash,save.Kind);}if(g.Extras!=null){Mix(ref hash,g.Extras.Native.ToString());Mix(ref hash,g.Extras.WorkingDirectory);Mix(ref hash,g.Extras.TrailerUrl);Mix(ref hash,g.Extras.MetadataSource);if(g.Extras.Fields!=null)foreach(var pair in g.Extras.Fields){Mix(ref hash,pair.Key);Mix(ref hash,pair.Value);}}}Mix(ref hash,Json.Serialize(new object[]{library.Emulators,library.Collections,library.Experience,library.Cosmetics,UserTools.Ensure(library).ActiveId,library.Theme.Name,library.Theme.AccentColor,library.Enhancements.TextPercent}));return hash;}
  public void ReloadOnVisit(){ulong stamp=VisitStamp();if(visitStamp==stamp&&(DateTime.UtcNow-visitScan).TotalSeconds<10)return;ReloadLibrary();VisitRefreshCount++;visitStamp=VisitStamp();visitScan=DateTime.UtcNow;}

 }

}
