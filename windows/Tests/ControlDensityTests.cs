using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq; using System.Reflection; using System.Windows.Forms; using EmulatorHub;
class ControlDensityTests {
 static int checks;
 static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 [STAThread]static int Main(){try{Application.EnableVisualStyles();Run();Console.WriteLine("PASS: "+checks+" compact density, preserved font preferences, live apply, Home sizing and accessibility checks.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(){var data=Store.Load();NextData.Ensure(data);ExperienceData.Ensure(data);data.Theme.ControlDensity=null;ControlDensityTools.Configure(data);Check(ControlDensityTools.Compact,"older saved libraries default to Compact");data.Games.Clear();data.Emulators.Clear();for(int i=0;i<4;i++)data.Games.Add(new GameEntry{Id="dense"+i,Title="Game "+i,Pinned=true,Favorite=true,LastLaunched=DateTime.Now.ToString("o"),ArtworkPath="missing"});data.Experience.HomeTileCount=4;Directory.CreateDirectory("density-previews");
  int compactButton=0,compactCard=0,compactPad=0,compactCover=0;
  foreach(string mode in new[]{"Compact","Standard","Roomy"}){
   data.Theme.ControlDensity=mode;ControlDensityTools.Configure(data);NextUi.TextPercent=100;
   using(var form=new Form{ClientSize=new Size(1000,680),ShowInTaskbar=false})using(var home=new HomeSurface(data,delegate{})){
    form.Controls.Add(home);form.Show();Application.DoEvents();home.RefreshLayout();Application.DoEvents();var cards=(FlowLayoutPanel)typeof(HomeSurface).GetField("cards",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(home);var card=cards.Controls.Cast<Control>().Single(c=>c.AccessibleName=="Pinned games");var action=NextUi.Descendants(card).OfType<Button>().First();int cover=NextUi.Descendants(card).OfType<PictureBox>().First().Width;
    if(mode=="Compact"){compactButton=action.Height;compactCard=card.Height;compactPad=card.Padding.Top;compactCover=cover;}else{Check(action.Height>compactButton,"button density changes visible height "+mode);Check(card.Height>compactCard&&card.Padding.Top>compactPad,"card density changes visible height and padding "+mode);Check(cover>compactCover,"cover size follows density "+mode);}
    Capture(form,"home-"+mode);form.Close();
   }
  }
  data.Theme.ControlDensity="Compact";ControlDensityTools.Configure(data);
  foreach(int percent in new[]{100,150,200})foreach(bool small in new[]{false,true}){
   NextUi.TextPercent=percent;data.Enhancements.TextPercent=percent;data.Theme.FontFamily="Segoe UI";TextFit.WorkingAreaOverride=small?new Rectangle(0,0,1024,720):new Rectangle(0,0,1280,900);
   using(var form=new NextDialog("Density fixture",760,520)){
    var fields=NextDialog.Fields(form.Body);var input=new TextBox{Text="Editable input"};var button=ExperienceUi.Button("Review selected game",delegate{});NextDialog.Field(fields,"Game title",input);NextDialog.Field(fields,"Options",button);form.Action("Close",form.Close);form.Show();Application.DoEvents();float font=input.Font.Size;ControlDensityTools.Apply(form);Application.DoEvents();var bounds=NextUi.Descendants(form).Select(c=>c.Bounds).ToArray();ControlDensityTools.Apply(form);Application.DoEvents();Check(NextUi.Descendants(form).Select(c=>c.Bounds).SequenceEqual(bounds),"repeated density apply is stable at "+percent+" / "+small);Check(input.Font.Size==font&&data.Enhancements.TextPercent==percent&&data.Theme.FontFamily=="Segoe UI","density keeps exact text and font preference");Check(button.Height>=button.Font.Height+8,"compact button retains readable text height");Check(button.Bottom<=button.Parent.ClientSize.Height&&input.Bottom<=input.Parent.ClientSize.Height,"compact field rows retain all controls");Capture(form,"fields-"+percent+"-"+(small?"compact":"normal"));form.Close();
   }
  }
  NextUi.TextPercent=200;
  using(var settings=new SettingsDialog(data.Theme,data.BackupFolder)){
   settings.ShowInTaskbar=false;settings.Show();Application.DoEvents();
   var hint=NextUi.Descendants(settings).OfType<Label>().Single(c=>c.Text.StartsWith("Choose a OneDrive"));
   var startup=NextUi.Descendants(settings).OfType<CheckBox>().Single(c=>c.Text.StartsWith("Show the setup assistant"));
   var browse=NextUi.Descendants(settings).OfType<Button>().Single(c=>c.Text=="Browse");
   Check(hint.Parent==startup.Parent&&hint.Parent==browse.Parent&&!hint.Bounds.IntersectsWith(browse.Bounds)&&!hint.Bounds.IntersectsWith(startup.Bounds),"large-text backup hint fits between Browse and following preference");
   Rectangle beforeHint=hint.Bounds,beforeStartup=startup.Bounds;ControlDensityTools.Apply(settings);Application.DoEvents();
   Check(hint.Bounds==beforeHint&&startup.Bounds==beforeStartup,"backup hint spacing stable after repeated apply");Capture(settings,"settings-200");settings.Close();
  }
  foreach(int percent in new[]{100,150,200}){
   data.Enhancements.TextPercent=percent;data.Theme.ControlDensity="Compact";data.Experience.StartPage="Library";Store.Save(data);NextUi.TextPercent=percent;TextFit.WorkingAreaOverride=new Rectangle(0,0,1024,720);
   using(var main=new MainForm(true)){
    main.ShowInTaskbar=false;main.MinimumSize=new Size(800,600);main.Size=new Size(1024,720);main.Show();Application.DoEvents();
    ((TabControl)typeof(MainForm).GetField("workspaceNavigation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main)).SelectedIndex=2;Application.DoEvents();
    var embedded=NextUi.Descendants(main).OfType<GameLibraryDialog>().Single();
    var title=NextUi.Descendants(embedded).OfType<Label>().Single(c=>c.Text=="Game Library");
    Check(Math.Abs(title.Font.Size-16f*percent/100f)<0.1f,"embedded library text scales exactly once at "+percent+" actual="+title.Font.Size);
    var bar=NextUi.Descendants(embedded).OfType<FlowLayoutPanel>().Single(c=>c.Name=="FishBowlToolbar");
    var games=NextUi.Descendants(embedded).OfType<ListView>().Single(c=>c.AccessibleName=="FishBowl game list");
    var filters=NextUi.Descendants(embedded).OfType<ComboBox>().Where(c=>c.Parent is FlowLayoutPanel).ToArray();
    Check(filters.All(a=>filters.All(b=>a==b||!a.Bounds.IntersectsWith(b.Bounds))),"Library filters never overlap at "+percent+" "+string.Join(";",filters.Select(c=>c.Text+" "+c.Bounds)));
    Capture(main,"library-before-"+percent);Check(bar.Visible&&bar.Bottom<=bar.Parent.ClientSize.Height&&games.Height>=100,"library actions and list retain space at "+percent+" bar="+bar.Bounds+" visible="+bar.Visible+" list="+games.Bounds+" client="+embedded.ClientSize);
    for(int repeat=0;repeat<2;repeat++){typeof(MainForm).GetMethod("ApplyAppearanceNow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);Application.DoEvents();}
    Check(Math.Abs(title.Font.Size-16f*percent/100f)<0.1f,"live theme apply preserves library scale at "+percent);
    Check(bar.Parent.Width>=embedded.ClientSize.Width-24,"library scroll page spans workspace at "+percent+" page="+bar.Parent.Bounds+" dock="+bar.Parent.Dock+" scroll="+bar.Parent.Parent.Bounds+" sdock="+bar.Parent.Parent.Dock);Capture(main,"library-"+percent);
    var live=(LibraryData)typeof(MainForm).GetField("library",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
    live.Enhancements.TextPercent=100;typeof(MainForm).GetMethod("ApplyAppearanceNow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);Application.DoEvents();
    Check(Math.Abs(title.Font.Size-16f)<0.1f&&filters.All(c=>c.Height<=c.Font.Height+16),"returning to 100% restores Library text and input heights after "+percent);
    main.Close();
   }
  }
  data.Theme.ControlDensity="Roomy";FluidStyle.Configure(data);Check(FluidStyle.Roomier,"Roomy overrides older spacing preference");data.Theme.ControlDensity=null;FluidStyle.Configure(data);Check(!FluidStyle.Roomier,"Compact suppresses older Roomier default without modifying it");UiPolishTools.ApplyAccessibility(data,"Controller");Check(data.Theme.ControlDensity=="Roomy","controller preset selects roomy controls");NextUi.TextPercent=100;TextFit.WorkingAreaOverride=null;
 }
 static void Capture(Form form,string name){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine("density-previews",name+".png"));}}
}
