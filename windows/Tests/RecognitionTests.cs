using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using EmulatorHub;
class RecognitionTests
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static void Bytes(byte[] a, int p, byte[] b) { Array.Copy(b, 0, a, p, b.Length); }
    static void Txt(byte[] a, int p, string s) { Bytes(a, p, Encoding.ASCII.GetBytes(s)); }
    static void Uni(byte[] a, int p, string s) { Bytes(a, p, Encoding.Unicode.GetBytes(s)); }
    static byte[] Smdh()
    {
        byte[] a = new byte[0x36c0]; Txt(a, 0, "SMDH"); Uni(a, 0x208, "Example Adventure"); Uni(a, 0x288, "Example Adventure Full Title"); Uni(a, 0x388, "Example Studio");
        for(int p=0x24c0; p<a.Length; p+=2) Bytes(a,p,BitConverter.GetBytes((ushort)0xf800));
        return a;
    }
    static byte[] Ncch(ulong id)
    {
        byte[] a = new byte[0x400+0x200+0x36c0]; Txt(a,0x100,"NCCH"); Bytes(a,0x118,BitConverter.GetBytes(id)); Bytes(a,0x1a0,BitConverter.GetBytes((uint)2));
        a[0x18d]=2; Bytes(a,0x180,BitConverter.GetBytes((uint)0x400));
        Txt(a,0x400,"icon"); Bytes(a,0x40c,BitConverter.GetBytes((uint)0x36c0)); Bytes(a,0x600,Smdh()); return a;
    }
    static byte[] Sfo(string title,string id)
    {
        byte[] a = new byte[256]; Txt(a,1,"PSF"); Bytes(a,8,BitConverter.GetBytes((uint)52)); Bytes(a,12,BitConverter.GetBytes((uint)96)); Bytes(a,16,BitConverter.GetBytes((uint)2));
        Bytes(a,24,BitConverter.GetBytes((uint)(Encoding.UTF8.GetByteCount(title)+1))); Bytes(a,36,BitConverter.GetBytes((ushort)6)); Bytes(a,40,BitConverter.GetBytes((uint)10)); Bytes(a,48,BitConverter.GetBytes((uint)64));
        Txt(a,52,"TITLE\0TITLE_ID\0"); Bytes(a,96,Encoding.UTF8.GetBytes(title)); Txt(a,160,id); return a;
    }
    [STAThread] static int Main()
    {
        try { Run(); Console.WriteLine("PASS: "+checks+" recognition, icon persistence, installed launch and settings UI checks."); return 0; }
        catch(Exception e) { Console.WriteLine("FAIL: "+e); return 1; }
    }
    static void Run()
    {
        Application.EnableVisualStyles();
        string root=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"recognition-fixture"); Directory.CreateDirectory(root);
        ulong id=0x0004000000033500; string idText=id.ToString("X16");
        string app=Path.Combine(root,"fixture.app"); File.WriteAllBytes(app,Ncch(id));
        using(var r=GameRecognition.Inspect(app)) { Check(r.Title=="Example Adventure Full Title","NCCH title"); Check(r.TitleId==idText,"NCCH title ID"); Check(r.Developer=="Example Studio","SMDH publisher"); Check(r.Icon.Width==48 && r.Icon.GetPixel(9,11).R==255,"Embedded tiled RGB565 icon"); }
        string cia=Path.Combine(root,"Example.cia"); byte[] c=new byte[0x3c0]; Bytes(c,0,BitConverter.GetBytes((uint)0x20)); Bytes(c,16,BitConverter.GetBytes((uint)0x200)); c[0x40+1]=1;c[0x40+3]=4;
        for(int i=0;i<8;i++) c[0x40+0x18c+i]=(byte)(id>>(56-8*i)); File.WriteAllBytes(cia,c);
        using(var r=GameRecognition.Inspect(cia)) Check(r.TitleId==idText,"CIA signed TMD title ID");
        var e=new EmulatorProfile{Id="3ds",Name="Azahar",Preset="Azahar Plus",Executable=Path.Combine(root,"azahar.exe"),Extensions=new List<string>{"cia","app"}}; File.WriteAllText(e.Executable,"fixture");
        string sd=Path.Combine(root,"user","sdmc","Nintendo 3DS",new string('0',32),new string('0',32),"title","00040000","00033500","content"); Directory.CreateDirectory(sd); string installed=Path.Combine(sd,"00000000.app"); File.WriteAllBytes(installed,Ncch(id));
        var g=new GameEntry{Id="example",Title="New game",Path=cia,EmulatorId=e.Id}; GameRecognition.Apply(g,new[]{e},true);
        Check(g.Title=="Example Adventure Full Title","Installed metadata identifies CIA name"); Check(g.TitleId==idText,"Recognized ID stored"); Check(File.Exists(g.ArtworkPath),"Icon retained in app storage");
        var json=new JavaScriptSerializer(); var reloaded=json.Deserialize<GameEntry>(json.Serialize(g)); Check(File.Exists(reloaded.ArtworkPath),"Icon survives record reload");
        string originalIcon=g.ArtworkPath; g.Title="My custom name"; GameRecognition.Apply(g,new[]{e},false); Check(g.Title=="My custom name" && g.ArtworkPath==originalIcon,"Custom title and artwork preserved");
        string args=InstalledGames.ResolveArguments(e,g,"--fullscreen {game}",cia); Check(args.Contains(installed)&&!args.Contains(cia),"CIA routes to installed app");
        for(int i=0;i<3;i++) Check(InstalledGames.ResolveArguments(e,g,"",cia).Contains(installed),"Repeated launch uses installed content");
        File.Delete(installed); bool blocked=false;try{InstalledGames.ResolveArguments(e,g,"",cia);}catch(IOException){blocked=true;}Check(blocked,"Missing install stops package relaunch"); File.WriteAllBytes(installed,Ncch(id));
        File.WriteAllBytes(installed,Ncch(id+1)); blocked=false;try{InstalledGames.ResolveArguments(e,g,"",cia);}catch(IOException){blocked=true;}Check(blocked,"Mismatched installed title never launches");File.WriteAllBytes(installed,Ncch(id));
        var library=new LibraryData{Games=new List<GameEntry>{g},Emulators=new List<EmulatorProfile>{e},Theme=new ThemeSettings()}; NextData.Ensure(library); string validated=GameSessions.Validate(library,g); Check(validated.Contains(installed)&&!validated.Contains(cia),"Launch validation uses installed path");
        File.Delete(cia);Check(GameSessions.Validate(library,g).Contains(installed),"Installed title launches after original package is removed");File.WriteAllBytes(cia,c);
        Check(InstalledGames.ResolveArguments(e,g,"",app)==null,"Ordinary ROM launch unchanged");
        string pkg=Path.Combine(root,"Secret of Mana.pkg"); byte[] p=new byte[96];p[0]=0x7f;Txt(p,1,"PKG");Txt(p,0x30,"HP0001-PCSH10067_00-EXAMPLE");File.WriteAllBytes(pkg,p);
        using(var r=GameRecognition.Inspect(pkg))Check(r.TitleId=="PCSH10067" && r.Platform=="PlayStation Vita","PKG content identity");
        string vita=Path.Combine(root,"vita");Directory.CreateDirectory(vita);string storage=Path.Combine(root,"vita-storage"); File.WriteAllText(Path.Combine(vita,"config.yml"),"pref-path: "+storage+"\n");
        var ve=new EmulatorProfile{Id="vita",Name="Vita3K",Executable=Path.Combine(vita,"Vita3K.exe")}; File.WriteAllText(ve.Executable,"fixture");
        Check(InstalledGames.Roots(ve).Count()==1,"Configured Vita storage does not fall back to another installation");
        string installedVita=Path.Combine(storage,"ux0","app","PCSH10067");Directory.CreateDirectory(Path.Combine(installedVita,"sce_sys"));File.WriteAllText(Path.Combine(installedVita,"eboot.bin"),"fixture");File.WriteAllBytes(Path.Combine(installedVita,"sce_sys","param.sfo"),Sfo("Secret of Mana","PCSH10067"));
        using(var icon=new Bitmap(64,64)){using(var graphics=Graphics.FromImage(icon))graphics.Clear(Color.Lime);icon.Save(Path.Combine(installedVita,"sce_sys","icon0.png"));}
        var vg=new GameEntry{Id="vita-game",Path=pkg,Title="New game",EmulatorId=ve.Id};GameRecognition.Apply(vg,new[]{ve},true);
        Check(vg.Title=="Secret of Mana" && File.Exists(vg.ArtworkPath),"Installed Vita title and icon");
        args=InstalledGames.ResolveArguments(ve,vg,"-F",pkg);Check(args.Trim()=="-F --run PCSH10067"&&!args.Contains(pkg),"Vita uses --run instead of PKG install");
        blocked=false;try{InstalledGames.ResolveArguments(ve,vg,"--pkg {game}",pkg);}catch(IOException){blocked=true;}Check(blocked,"Vita install flags require correction");
        string vpk=Path.Combine(root,"fixture.vpk"); if(File.Exists(vpk))File.Delete(vpk);
        using(var archive=System.IO.Compression.ZipFile.Open(vpk,System.IO.Compression.ZipArchiveMode.Create))
        { using(var output=archive.CreateEntry("sce_sys/param.sfo").Open()){byte[] sfo=Sfo("Vita VPK Title","PCSH10067");output.Write(sfo,0,sfo.Length);} }
        using(var r=GameRecognition.Inspect(Path.Combine(root,"fixture.vpk"))) Check(r.Title=="Vita VPK Title"&&r.TitleId=="PCSH10067","VPK metadata read without extraction");
        string nds=Path.Combine(root,"Example.nds"); byte[] n=new byte[0x940];Txt(n,12,"ABCE");Bytes(n,0x68,BitConverter.GetBytes((uint)0x100));Uni(n,0x440,"DS Adventure");n[0x120]=0x11;Bytes(n,0x322,BitConverter.GetBytes((ushort)31));File.WriteAllBytes(nds,n);
        using(var r=GameRecognition.Inspect(nds)) Check(r.Title=="DS Adventure"&&r.Icon.Width==32&&r.Icon.GetPixel(0,0).R==255,"DS banner title and palette icon");
        File.WriteAllBytes(Path.Combine(root,"broken.cia"),new byte[2]);using(var r=GameRecognition.Inspect(Path.Combine(root,"broken.cia"))) Check(r.Title=="broken"&&r.Icon==null,"Malformed metadata falls back safely");
        Check(GameRecognition.CleanTitle("0004000000033500 The Legend of Zelda (CTR-P-AQEE) (v0.1.0) (U).legit.cia")=="The Legend of Zelda","Clean title removes dump labels");
        using(var r=GameRecognition.Inspect(Path.Combine(root,"Celeste [01002B30028F6000][v0].nsp")))Check(r.Title=="Celeste"&&r.TitleId=="01002B30028F6000"&&r.Platform=="Nintendo Switch","Switch filename title, identity and platform");
        string gba=Path.Combine(root,"cart.gba");byte[] gb=new byte[0xc0];Txt(gb,0xa0,"GBA QUEST");Txt(gb,0xac,"ABCE");gb[0xb2]=0x96;File.WriteAllBytes(gba,gb);using(var r=GameRecognition.Inspect(gba))Check(r.Title=="GBA QUEST"&&r.TitleId=="ABCE","GBA header identity");
        string n64=Path.Combine(root,"cart.z64");byte[] nh=new byte[0x40];nh[0]=0x80;nh[1]=0x37;nh[2]=0x12;nh[3]=0x40;Txt(nh,0x20,"N64 ADVENTURE");File.WriteAllBytes(n64,nh);using(var r=GameRecognition.Inspect(n64))Check(r.Title=="N64 ADVENTURE","N64 header title");
        for(int i=0;i<nh.Length;i+=2){byte b=nh[i];nh[i]=nh[i+1];nh[i+1]=b;}File.WriteAllBytes(Path.Combine(root,"cart.v64"),nh);using(var r=GameRecognition.Inspect(Path.Combine(root,"cart.v64")))Check(r.Title=="N64 ADVENTURE","N64 swapped byte order");
        string disc=Path.Combine(root,"disc.iso");byte[] dh=new byte[0x400];Txt(dh,0,"ABCE01");dh[0x18]=0x5d;dh[0x19]=0x1c;dh[0x1a]=0x9e;dh[0x1b]=0xa3;Txt(dh,0x20,"Wii Adventure");File.WriteAllBytes(disc,dh);using(var r=GameRecognition.Inspect(disc))Check(r.Title=="Wii Adventure"&&r.TitleId=="ABCE01"&&r.Platform=="Nintendo Wii","Wii disc identity");
        using(var form=new GameDialog(new GameEntry{Title="New game"},new[]{e}))
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;((TextBox)typeof(GameDialog).GetField("path",flags).GetValue(form)).Text=cia;typeof(GameDialog).GetMethod("SuggestGame",flags).Invoke(form,null);
            Check(((TextBox)typeof(GameDialog).GetField("title",flags).GetValue(form)).Text=="Example Adventure Full Title","Add dialog identifies picked game");
            form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.Show();Application.DoEvents();
            foreach(var box in NextUi.Descendants(form).OfType<TextBoxBase>()) Check(box.BorderStyle==BorderStyle.FixedSingle&&box.BackColor==FishBowlPalette.DeepSeaSurface&&box.Region==null,"Every game settings input follows shared theme");
            var nested=new Panel();form.Controls.Add(nested);var dynamicBox=new TextBox();nested.Controls.Add(dynamicBox);Check(dynamicBox.BorderStyle==BorderStyle.FixedSingle&&dynamicBox.BackColor==FishBowlPalette.DeepSeaSurface,"Dynamically added settings input themed");
            var tabs=new FishBowlTabs{Dock=DockStyle.Fill};tabs.TabPages.Add("General");tabs.TabPages.Add("Paths");nested.Controls.Add(tabs);Check(tabs.LegacyHeaders&&tabs.SurfaceColor==FishBowlPalette.ThemeTop,"Settings tabs share main header painter");
            dynamicBox.Text="Stable selection";dynamicBox.Select(2,4);var handle=dynamicBox.Handle;for(int i=0;i<5;i++)FishBowlPalette.StyleWindow(form);Check(dynamicBox.Text=="Stable selection"&&dynamicBox.SelectionStart==2&&dynamicBox.SelectionLength==4&&dynamicBox.Handle==handle,"Repeated styling preserves editing state");
            Check(((TextBox)typeof(GameDialog).GetField("notes",flags).GetValue(form)).Height>=100,"Multiline notes have usable height");
            nested.Dispose();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"game-settings.png"));}form.Close();
        }
        using(var f=new FishBowlDialog{ClientSize=new Size(700,450),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000)})
        {
            var tabs=new FishBowlTabs{Dock=DockStyle.Fill};var page=new TabPage("Appearance");tabs.TabPages.Add(page);tabs.TabPages.Add("Folders");page.Controls.Add(new TextBox{Text="Example setting",Location=new Point(24,36),Width=400});f.Controls.Add(tabs);f.Show();Application.DoEvents();Check(page.BackColor==FishBowlPalette.DeepSeaSurface&&!page.UseVisualStyleBackColor,"Settings pages have themed opaque backgrounds");using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"settings-tabs.png"));}f.Close();
        }
    }
}
