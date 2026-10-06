using System;
using System.IO;
using System.Linq;
using System.Text;
using EmulatorHub;
class AzaharStorageTests
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static int Main()
    {
        try { Run(); Console.WriteLine("PASS: " + checks + " Azahar Plus installed storage checks."); return 0; }
        catch(Exception e) { Console.WriteLine("FAIL: " + e); return 1; }
    }
    static void Run()
    {
        string root=Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),"azahar-storage-"+Guid.NewGuid().ToString("N").Substring(0,8));
        string roaming=Path.Combine(root,"roaming"), plus=Path.Combine(roaming,"AzaharPlus"), program=Path.Combine(root,"azaharplus-build");
        Directory.CreateDirectory(program); Directory.CreateDirectory(Path.Combine(plus,"config"));
        var e=new EmulatorProfile { Name="Azahar Plus", Preset="Azahar Plus", Executable=Path.Combine(program,"azahar.exe") };
        File.WriteAllText(e.Executable,"fixture");
        Check(ThreeDsStorage.UserRoot(e,roaming)==plus,"Plus build uses AzaharPlus rather than Azahar");
        var detector=new EmulatorFolderDetector(roaming,root,root,false);
        Check(InstalledGames.Roots(e,detector).Contains(Path.Combine(plus,"sdmc","Nintendo 3DS")),"Default Plus SD storage discovered");
        e.InGameSaveFolder=Path.Combine(roaming,"Azahar","old-backup");
        Check(InstalledGames.Roots(e,detector).Contains(Path.Combine(plus,"sdmc","Nintendo 3DS")),"Stale backup override cannot hide active installed titles");
        e.ConfigFolder=Path.Combine(roaming,"Azahar","config");
        Check(ThreeDsStorage.UserRoot(e,roaming)==plus,"Old autodetected Azahar config corrected when Plus storage exists");
        Check(InstalledGames.Roots(e,detector).Contains(Path.Combine(plus,"sdmc","Nintendo 3DS")),"Old config override cannot send Plus launch back to Azahar");
        string sd=Path.Combine(root,"custom-sd");
        File.WriteAllText(Path.Combine(plus,"config","qt-config.ini"),"[Data Storage]\nuse_custom_storage=true\nuse_custom_storage\\default=false\nsdmc_directory="+sd.Replace("\\","/")+"\nsdmc_directory\\default=false\n");
        e.ConfigFolder=Path.Combine(plus,"config");
        Check(InstalledGames.Roots(e,detector).Contains(Path.Combine(sd,"Nintendo 3DS")),"Explicit config and custom SD storage honored");
        string content=Path.Combine(sd,"Nintendo 3DS",new string('0',32),new string('1',32),"title","00040000","00055d00","content"); Directory.CreateDirectory(content);
        byte[] app=new byte[512]; Array.Copy(Encoding.ASCII.GetBytes("NCCH"),0,app,0x100,4); Array.Copy(BitConverter.GetBytes(0x0004000000055D00UL),0,app,0x118,8);
        app[0x18d]=2; Array.Copy(BitConverter.GetBytes(0x400U),0,app,0x180,4); Array.Copy(BitConverter.GetBytes(2U),0,app,0x1a0,4);
        string installed=Path.Combine(content,"00000000.app"); File.WriteAllBytes(installed,app);
        var game=new GameEntry {Title="Pokemon X",TitleId="0004000000055D00",Path=Path.Combine(root,"Pokemon X.cia")};
        for(int i=0;i<3;i++) Check(InstalledGames.ResolveArguments(e,game,"--fullscreen {game}",game.Path)=="--fullscreen \""+installed+"\"","Pokemon X repeated launch uses installed content");
        Check(InstalledGames.ResolveArguments(e,game,"\"{game}\"",game.Path)=="\""+installed+"\"","Quoted template does not double quote installed path");
        string manual=Path.Combine(content,"00000001.app"); byte[] data=(byte[])app.Clone(); data[0x18d]=1; File.WriteAllBytes(manual,data);
        File.Delete(installed);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==null,"Manual with matching program ID must never launch");
        File.WriteAllBytes(installed,app);
        byte[] tmd=new byte[0x140+0x9c4+0x60]; tmd[1]=1; tmd[3]=4;
        byte[] tid=BitConverter.GetBytes(0x0004000000055D00UL); Array.Reverse(tid); Array.Copy(tid,0,tmd,0x140+0x4c,8);
        tmd[0x140+0x9f]=2; tmd[0x140+0x9c4+3]=0x20;
        string tmdPath=Path.Combine(content,"00000000.tmd"); File.WriteAllBytes(tmdPath,tmd);
        string main=Path.Combine(content,"00000020.app"); File.WriteAllBytes(main,app);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==main,"TMD main selected ahead of other matching executable app");
        string newer=Path.Combine(content,"00000001.tmd"); byte[] newerTmd=(byte[])tmd.Clone(); newerTmd[0x140+0x9c4+3]=0; File.WriteAllBytes(newer,newerTmd);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==main,"Oldest installed TMD chosen as Azahar does");
        File.Delete(main);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==null,"Missing metadata selected content must not launch different executable");
        File.WriteAllBytes(main,data);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==null,"Metadata selected manual rejected");
        File.WriteAllBytes(main,new byte[512]);
        Check(InstalledGames.Find(e,game.TitleId,game.Path)==main,"Wrapped metadata selected installed app delegated to emulator");
        File.Delete(tmdPath); File.Delete(newer); File.Delete(main);
        File.Delete(installed);
        try { InstalledGames.ResolveArguments(e,game,"",game.Path); throw new Exception("Missing install should stop"); }
        catch(IOException error) { Check(error.Message.Contains(game.TitleId)&&error.Message.Contains(sd)&&!error.Message.Contains("Install it once"),"Failure reports title ID and actual searched storage"); }
        e.ConfigFolder=""; e.Preset="Custom";
        Check(InstalledGames.Roots(e,detector).Contains(Path.Combine(sd,"Nintendo 3DS")),"Plus directory recognized despite custom preset and azahar.exe basename");
        string portable=Path.Combine(program,"user"); Directory.CreateDirectory(portable);
        Check(ThreeDsStorage.UserRoot(e,roaming)==portable,"Portable user directory takes precedence");
        e.Executable=Path.Combine(root,"azahar.exe");
        Check(ThreeDsStorage.UserRoot(e,roaming)==Path.Combine(roaming,"Azahar"),"Official Azahar remains separate");
    }
}
