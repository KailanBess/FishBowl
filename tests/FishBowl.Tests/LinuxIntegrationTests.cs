using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EmulatorHub;

// Linux integrations (FishBowl.Avalonia/Linux): Flatpak permissions and overrides, AppImage/Flatpak updates, Steam
// shortcuts, EmuDeck/RetroDECK and the controller inventory. Every check uses fixture text and folders; no real Flatpak,
// Steam or suite installation is read or changed. Game titles are made up.
public static class LinuxIntegrationTests
{
    static Action<string, bool> Check;

    // flatpak info --show-permissions output captured from flatpak 1.18.2 (an app with devices=all and Downloads access).
    const string AppPermissions = "[Context]\nshared=ipc;network;\nsockets=pcsc;pulseaudio;x11;\ndevices=all;\nfilesystems=xdg-download;\n\n[Session Bus Policy]\norg.gnome.SessionManager=talk\norg.freedesktop.Notifications=talk\n\n[Environment]\nTZ=\n";
    // flatpak override --user --show after --filesystem='~/Games:ro' --filesystem=/mnt/test:create --filesystem=xdg-documents/bios
    //   --nofilesystem=xdg-download/sub --filesystem='/opt/my' --device=input --device-if=all:!has-input-device --nodevice=kvm
    const string UserOverride = "[Context]\ndevices=all;if:all:!has-input-device;input;!kvm;\nfilesystems=/opt/my;/mnt/test:create;xdg-documents/bios;~/Games:ro;!xdg-download/sub;\n";
    // flatpak info --show-permissions with that override: negations are dropped from this view.
    const string Effective = "[Context]\nshared=ipc;network;\nsockets=pcsc;pulseaudio;x11;\ndevices=all;if:all:!has-input-device;input;\nfilesystems=xdg-download;/opt/my;/mnt/test:create;xdg-documents/bios;~/Games:ro;\n\n[Session Bus Policy]\norg.gnome.SessionManager=talk\n\n[Environment]\nTZ=\n";

    public static void Run(string root, Action<string, bool> check)
    {
        Check = check; var dir = Path.Combine(root, "linux-integrations"); Directory.CreateDirectory(dir);
        Flatpak(); FlatpakFolders(dir); State(dir); Updates(dir); Steam(dir); Suites(dir); Controllers(dir);
    }

    static void Flatpak()
    {
        var v118 = new Version(1, 18, 2); var v116 = new Version(1, 16, 0); var v114 = new Version(1, 14, 4);
        var app = FlatpakPermissions.Parse(AppPermissions);
        Check("flatpak: parse filesystems", app.Filesystems.Single().Name == "xdg-download" && app.Filesystems[0].Mode == SandboxAccess.ReadWrite);
        Check("flatpak: other groups ignored", !app.Context.ContainsKey("org.gnome.SessionManager") && app.Values("sockets").Count == 3);
        Check("flatpak: devices=all is controller access", app.ControllerAccess(v118) == "all");
        var eff = FlatpakPermissions.Parse(Effective);
        Check("flatpak: conditional all dropped on 1.18, input used", eff.ControllerAccess(v118) == "input" && !eff.DeviceAllowed("all", v118));
        Check("flatpak: pre-conditional flatpak keeps the plain all", eff.DeviceAllowed("all", v116));
        var input = FlatpakPermissions.Parse("[Context]\ndevices=input;\n");
        Check("flatpak: input unknown before 1.15.6", !input.DeviceAllowed("input", v114) && input.ControllerAccess(v116) == "input");
        Check("flatpak: no devices, no controllers", FlatpakPermissions.Parse("[Context]\ndevices=dri;\n").ControllerAccess(v118) == null);
        Check("flatpak: negated device", !FlatpakPermissions.Parse("[Context]\ndevices=!all;\n").DeviceAllowed("all", v118));
        var ovr = FlatpakPermissions.Parse(UserOverride);
        Check("flatpak: entries round-trip", String.Join(";", ovr.Filesystems.Select(f => f.Entry)) == "/opt/my;/mnt/test:create;xdg-documents/bios;~/Games:ro;!xdg-download/sub");
        Check("flatpak: suffixes", ovr.Filesystems[1].Create && ovr.Filesystems[3].Mode == SandboxAccess.ReadOnly && ovr.Filesystems[4].Negated && ovr.Filesystems[4].Mode == SandboxAccess.None);
        var colon = FlatpakFilesystem.Parse("/mnt/a\\:b:ro");
        Check("flatpak: escaped colon", colon.Name == "/mnt/a:b" && colon.Mode == SandboxAccess.ReadOnly && colon.Entry == "/mnt/a\\:b:ro");
        Check("flatpak: ~ is home", FlatpakFilesystem.Parse("~").Name == "home");
        Check("flatpak: keyfile list escapes", String.Join("|", FlatpakPermissions.SplitList("a\\;b;\\sc;d\\\\e;")) == "a;b| c|d\\e" && FlatpakPermissions.JoinList(new[] { "a;b", " c", "d\\e" }) == "a\\;b;\\sc;d\\\\e;");

        var home = "/fx/home"; var existing = new HashSet<string> { "/fx/home", "/fx/home/Games", "/fx/home/Games/roms", "/fx/home/Downloads", "/fx/home/Downloads/sub", "/fx/home/Docs", "/fx/home/Docs/bios", "/opt/my", "/media/disk" };
        var paths = new FlatpakPaths { Home = home, ConfigHome = home + "/.config", DataHome = home + "/.local/share", CacheHome = home + "/.cache", RuntimeDir = "/run/user/1000", Exists = existing.Contains,
            UserDirs = FlatpakPaths.ReadUserDirs("XDG_DESKTOP_DIR=\"$HOME/Desktop\"\nXDG_DOWNLOAD_DIR=\"$HOME/Downloads\"\nXDG_DOCUMENTS_DIR=\"$HOME/Docs\"\nXDG_MUSIC_DIR=\"$HOME\"\n", home) };
        const string id = "org.example.Emu";
        Func<FlatpakPermissions, string, SandboxAccess> access = (p, path) => p.Access(path, id, paths).Mode;
        Check("access: ~/Games:ro covers subfolder read-only", access(ovr, home + "/Games/roms") == SandboxAccess.ReadOnly);
        Check("access: xdg-documents/bios", access(eff, home + "/Docs/bios") == SandboxAccess.ReadWrite);
        Check("access: Docs itself hidden", access(eff, home + "/Docs") == SandboxAccess.None);
        Check("access: xdg-download read-write", access(eff, home + "/Downloads") == SandboxAccess.ReadWrite);
        var hidden = FlatpakPermissions.Parse("[Context]\nfilesystems=xdg-download;!xdg-download/sub;\n").Access(home + "/Downloads/sub", id, paths);
        Check("access: negation hides a subfolder", hidden.Mode == SandboxAccess.None && hidden.Via.Contains("!xdg-download/sub"));
        Check("access: own sandbox always", eff.Access(home + "/.var/app/" + id + "/data", id, paths).Sandbox);
        var withHome = FlatpakPermissions.Parse("[Context]\nfilesystems=home;\n");
        Check("access: home covers home folders", access(withHome, home + "/Games/roms") == SandboxAccess.ReadWrite);
        Check("access: home does not reveal other apps", access(withHome, home + "/.var/app/org.other.App/data") == SandboxAccess.None);
        Check("access: home leaves /media out", access(withHome, "/media/disk") == SandboxAccess.None);
        var withHost = FlatpakPermissions.Parse("[Context]\nfilesystems=host:ro;~/Games;\n");
        Check("access: host shares /media", access(withHost, "/media/disk") == SandboxAccess.ReadOnly);
        Check("access: host read-only, deeper rw wins", access(withHost, home + "/Docs") == SandboxAccess.ReadOnly && access(withHost, home + "/Games/roms") == SandboxAccess.ReadWrite);
        Check("access: host skips /tmp and /var", access(withHost, "/tmp/x") == SandboxAccess.None && access(withHost, "/var/games") == SandboxAccess.None);
        Check("access: /usr reserved", withHost.Access("/usr/share/bios", id, paths).Reserved);
        Check("access: XDG folder equal to $HOME is ignored", access(FlatpakPermissions.Parse("[Context]\nfilesystems=xdg-music;\n"), home + "/Games") == SandboxAccess.None);
        Check("access: missing explicit folder is not shared", access(FlatpakPermissions.Parse("[Context]\nfilesystems=/mnt/gone;\n"), "/mnt/gone") == SandboxAccess.None);
        Check("access: :create shares a missing folder", access(ovr, "/mnt/test/saves") == SandboxAccess.ReadWrite);

        Check("grant: home path uses ~/", FlatpakTool.GrantEntry(home + "/Games/roms", SandboxAccess.ReadOnly, false, home) == "~/Games/roms:ro");
        Check("grant: rw and create", FlatpakTool.GrantEntry("/mnt/saves/", SandboxAccess.ReadWrite, false, home) == "/mnt/saves" && FlatpakTool.GrantEntry("/mnt/new", SandboxAccess.ReadWrite, true, home) == "/mnt/new:create");
        Check("grant: colon escaped", FlatpakTool.GrantEntry("/mnt/a:b", SandboxAccess.ReadOnly, false, home) == "/mnt/a\\:b:ro");
        var args = FlatpakTool.OverrideArguments("--filesystem=/mnt/My Games:ro", "org.DolphinEmu.dolphin-emu");
        Check("command: arguments are --user only", String.Join("|", args) == "override|--user|--filesystem=/mnt/My Games:ro|org.DolphinEmu.dolphin-emu");
        Check("command: display quoting", FlatpakTool.CommandLine("flatpak", args) == "flatpak override --user '--filesystem=/mnt/My Games:ro' org.DolphinEmu.dolphin-emu");
        Check("remove: entry gone, rest kept", FlatpakTool.RemoveOverrideEntry(UserOverride, "filesystems", "~/Games:ro") == "[Context]\ndevices=all;if:all:!has-input-device;input;!kvm;\nfilesystems=/opt/my;/mnt/test:create;xdg-documents/bios;!xdg-download/sub;\n");
        Check("remove: matches any mode", FlatpakTool.RemoveOverrideEntry("[Context]\nfilesystems=~/Games;\n", "filesystems", "~/Games:ro") == "[Context]\n");
        Check("remove: missing entry", FlatpakTool.RemoveOverrideEntry(UserOverride, "filesystems", "/nope") == null);
        Check("remove: device keeps conditional entries", FlatpakTool.RemoveOverrideEntry("[Context]\ndevices=dri;input;if:input:true;\n", "devices", "input") == "[Context]\ndevices=dri;if:input:true;\n");
        Check("flatpak version parse", FlatpakTool.ParseVersion("Flatpak 1.18.2\n") == v118);
        var row = FlatpakSetup.ControllerRow(id, null, v118);
        Check("controller row offers input devices", row.Status == "Blocked" && row.Fix != null && row.Fix.Command == "flatpak override --user --device=input " + id);
        Check("controller row offers all on old flatpak", FlatpakSetup.ControllerRow(id, null, v114).Fix.Option == "--device=all");
        var folder = new FlatpakWantedFolder { Path = home + "/Saves", Label = "save folder", Need = SandboxAccess.ReadWrite };
        var readOnly = FlatpakSetup.FolderRow("x", folder, new FlatpakAccessDecision { Mode = SandboxAccess.ReadOnly, Via = "home:ro" }, null, true, id, home);
        Check("folder row: read-only is not enough for saves", readOnly.Status == "Read-only" && readOnly.Fix.Option == "--filesystem=~/Saves");
        var reported = FlatpakSetup.FolderRow("x", folder, new FlatpakAccessDecision { Mode = SandboxAccess.ReadWrite, Via = "home" }, SandboxAccess.None, true, id, home);
        Check("folder row: flatpak's own verdict wins", reported.Status == "Blocked" && reported.Detail.Contains("reports it hidden"));
        var missing = FlatpakSetup.FolderRow("x", folder, new FlatpakAccessDecision(), null, false, id, home);
        Check("folder row: missing folder gets :create", missing.Fix.Option == "--filesystem=~/Saves:create");
    }

    static void FlatpakFolders(string dir)
    {
        var a = Path.Combine(dir, "roms", "gba"); var b = Path.Combine(a, "hacks"); var c = Path.Combine(dir, "other"); foreach (var d in new[] { a, b, c }) Directory.CreateDirectory(d);
        var emu = new EmulatorProfile { Id = "m", Name = "mGBA", Preset = "mGBA", Executable = "/fx/exports/bin/io.mgba.mGBA", FirmwareFolder = Path.Combine(dir, "bios") };
        var other = new EmulatorProfile { Id = "o", Name = "Other", Executable = "/fx/other" };
        var library = new LibraryData { Emulators = new List<EmulatorProfile> { emu, other }, Games = new List<GameEntry> {
            new GameEntry { Id = "1", Title = "Pixel Quest", Path = Path.Combine(a, "Pixel Quest.gba"), EmulatorId = "m" },
            new GameEntry { Id = "2", Title = "Pixel Quest Plus", Path = Path.Combine(b, "Pixel Quest Plus.gba"), EmulatorId = "m" },
            new GameEntry { Id = "3", Title = "Elsewhere", Path = Path.Combine(c, "Elsewhere.iso"), EmulatorId = "o" } }, GameLibraryRoot = Path.Combine(dir, "nope") };
        var detected = new List<EmulatorFolder> { new EmulatorFolder("InGameSaveFolder", "Save folder", Path.Combine(dir, "saves"), "test"), new EmulatorFolder("Program", "Program", "/fx", "test") };
        var wanted = FlatpakSetup.WantedFolders(emu, library, detected);
        Check("wanted folders: outermost game folder once", wanted.Count(w => w.Label == "game folder") == 1 && wanted.Any(w => w.Path == a));
        Check("wanted folders: other emulators' games skipped", !wanted.Any(w => w.Path == c));
        Check("wanted folders: firmware read-only, saves read-write, program skipped", wanted.Single(w => w.Label == "firmware folder").Need == SandboxAccess.ReadOnly
            && wanted.Single(w => w.Label == "save folder").Need == SandboxAccess.ReadWrite && !wanted.Any(w => w.Path == "/fx"));
    }

    static void State(string dir)
    {
        var file = Path.Combine(dir, "state", "linux-integrations.json");
        var state = LinuxIntegrationState.Load(file);
        state.AddGrant("org.example.Emu", "devices=input"); state.AddGrant("org.example.Emu", "devices=input"); state.AddGrant("org.example.Emu", "filesystems=~/Games:ro"); state.Save(file);
        var back = LinuxIntegrationState.Load(file);
        Check("state: grants round-trip without duplicates", back.Grants("org.example.Emu").SequenceEqual(new[] { "devices=input", "filesystems=~/Games:ro" }));
        back.RemoveGrant("org.example.Emu", "devices=input"); back.RemoveGrant("org.example.Emu", "filesystems=~/Games:ro");
        Check("state: empty app removed", back.FlatpakGrants.Count == 0 && back.Grants("missing").Count == 0);
        File.WriteAllText(file, "{not json");
        Check("state: unreadable file starts fresh", LinuxIntegrationState.Load(file).FlatpakGrants.Count == 0);
    }

    // A minimal ELF64 AppImage (type 2) with a .upd_info section.
    static byte[] AppImage(string update)
    {
        var strtab = Encoding.ASCII.GetBytes("\0.shstrtab\0.upd_info\0"); var data = new byte[1024]; var text = Encoding.UTF8.GetBytes(update); Buffer.BlockCopy(text, 0, data, 0, text.Length);
        long strAt = 64, dataAt = strAt + strtab.Length, shAt = dataAt + data.Length;
        var o = new MemoryStream(); var w = new BinaryWriter(o);
        w.Write(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0, (byte)'A', (byte)'I', 2, 0, 0, 0, 0, 0 });
        w.Write((ushort)2); w.Write((ushort)62); w.Write(1); w.Write(0L); w.Write(0L); w.Write(shAt); w.Write(0); w.Write((ushort)64); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)64); w.Write((ushort)3); w.Write((ushort)1);
        w.Write(strtab); w.Write(data);
        Action<int, int, long, long> section = (name, type, offset, size) => { w.Write(name); w.Write(type); w.Write(0L); w.Write(0L); w.Write(offset); w.Write(size); w.Write(0); w.Write(0); w.Write(1L); w.Write(0L); };
        section(0, 0, 0, 0); section(1, 3, strAt, strtab.Length); section(11, 7, dataAt, data.Length);
        return o.ToArray();
    }

    static void Updates(string dir)
    {
        var file = Path.Combine(dir, "Emu-x86_64.AppImage"); File.WriteAllBytes(file, AppImage("gh-releases-zsync|example-org|example-emu|latest|Emu-*x86_64.AppImage.zsync"));
        Check("appimage: type 2 recognized", AppImageFile.ImageType(file) == 2);
        var info = AppImageUpdateInfo.Parse(AppImageFile.UpdateInformation(file));
        Check("appimage: update information from .upd_info", info != null && info.Transport == "gh-releases-zsync" && info.Repository == "example-org/example-emu" && info.Describe().Contains("latest release"));
        Check("appimage: zsync on GitHub gives the repository", AppImageUpdateInfo.Parse("zsync|https://github.com/a-b/c.d/releases/download/v1/x.zsync").Repository == "a-b/c.d");
        Check("appimage: unknown transport kept raw, empty ignored", AppImageUpdateInfo.Parse("bintray-zsync|x|y|z|w").Repository == null && AppImageUpdateInfo.Parse("  ") == null);
        var plain = Path.Combine(dir, "plain.AppImage"); File.WriteAllText(plain, "#!/bin/sh\n");
        Check("appimage: script is not an AppImage", AppImageFile.ImageType(plain) == 0 && AppImageFile.UpdateInformation(plain) == null);
        string version;
        Check("flatpak remote-ls: update listed", InstallUpdates.ParseRemoteLs("org.other.App\t2.0\nio.mgba.mGBA\t0.10.5\n", "io.mgba.mGBA", out version) && version == "0.10.5");
        Check("flatpak remote-ls: empty version, not listed", InstallUpdates.ParseRemoteLs("io.mgba.mGBA\t\n", "io.mgba.mGBA", out version) && version == null && !InstallUpdates.ParseRemoteLs("", "io.mgba.mGBA", out version));
        Check("flatpak update arguments", String.Join(" ", InstallUpdates.FlatpakUpdateArguments("io.mgba.mGBA", "user")) == "update --user -y --noninteractive io.mgba.mGBA"
            && String.Join(" ", InstallUpdates.FlatpakCheckArguments("system")) == "remote-ls --system --updates --app --columns=application,version");
        Check("appimageupdatetool result line", InstallUpdates.NewFileFromLog("Fetching...\nUpdate successful. New file created: /home/x/Emu-2.AppImage\n") == "/home/x/Emu-2.AppImage");
        var log = new TerminalLog(); log.Append("\x1B[1mStarting\x1B[0m\n10%\r55%\r100%\nDone");
        Check("terminal log: escapes dropped, carriage return overwrites", log.ToString() == "Starting\n100%\nDone");
        var output = new StringBuilder(); var exit = ProcessStream.Run("/bin/sh", new[] { "-c", "echo out; echo err >&2; exit 3" }, s => { lock (output) output.Append(s); }, System.Threading.CancellationToken.None);
        Check("process stream: output and exit code", exit == 3 && output.ToString().Contains("out") && output.ToString().Contains("err"));
    }

    // A shortcuts.vdf as Steam writes it, plus fields FishBowl does not know.
    static byte[] SteamFile()
    {
        var o = new MemoryStream();
        Action<byte, string> head = (t, k) => { o.WriteByte(t); var b = Encoding.UTF8.GetBytes(k); o.Write(b, 0, b.Length); o.WriteByte(0); };
        Action<string, string> str = (k, v) => { head(1, k); var b = Encoding.UTF8.GetBytes(v); o.Write(b, 0, b.Length); o.WriteByte(0); };
        Action<string, int> i32 = (k, v) => { head(2, k); o.Write(BitConverter.GetBytes(v), 0, 4); };
        head(0, "shortcuts"); head(0, "0");
        i32("appid", unchecked((int)0xCBB3990A)); str("AppName", "Sample Tool"); str("Exe", "\"/usr/bin/flatpak\""); str("StartDir", "\"/usr/bin/\"");
        str("icon", ""); str("ShortcutPath", "/var/lib/flatpak/exports/share/applications/org.example.Tool.desktop");
        str("LaunchOptions", "run org.example.Tool --launch-game abc"); i32("IsHidden", 0); i32("AllowDesktopConfig", 1); i32("AllowOverlay", 1); i32("OpenVR", 0); i32("Devkit", 0);
        str("DevkitGameID", ""); i32("DevkitOverrideAppID", 0); i32("LastPlayTime", 1700000000); str("FlatpakAppID", "");
        head(7, "SomeFutureUInt64"); o.Write(BitConverter.GetBytes(123456789012345UL), 0, 8);
        head(3, "SomeFloat"); o.Write(BitConverter.GetBytes(1.5f), 0, 4);
        str("sortas", "Sample Tool é");
        head(0, "tags"); str("0", "Tools"); o.WriteByte(8);
        o.WriteByte(8); o.WriteByte(8); o.WriteByte(8);
        return o.ToArray();
    }

    static void Steam(string dir)
    {
        Check("crc32 documented check value", SteamShortcuts.Crc32(Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926);
        Check("steam app id = crc | 0x80000000", SteamShortcuts.AppId("1234", "56789") == 3421780262u && SteamShortcuts.LongAppId(3421780262u) == 14696434319421865984UL);
        Check("steam app id example", SteamShortcuts.AppId("\"/usr/lib/fishbowl/FishBowl\"", "Dolphin") == 3848284903u && unchecked((int)SteamShortcuts.AppId("\"/usr/lib/fishbowl/FishBowl\"", "Dolphin")) == -446682393);

        var bytes = SteamFile(); var tree = BinaryVdf.Read(bytes);
        Check("vdf round trip is byte-identical", BinaryVdf.Write(tree).SequenceEqual(bytes));
        var first = tree.Child("shortcuts").Children[0];
        Check("vdf reads fields", first.GetString("appname") == "Sample Tool" && first.Child("LastPlayTime").IntValue == 1700000000 && first.GetString("sortas") == "Sample Tool é");
        Check("vdf empty file", BinaryVdf.Read(new byte[0]).Children.Count == 0);
        try { BinaryVdf.Read(bytes.Take(bytes.Length - 2).ToArray()); Check("vdf truncated rejected", false); } catch (InvalidDataException) { Check("vdf truncated rejected", true); }
        var bad = (byte[])bytes.Clone(); bad[Array.IndexOf(bad, (byte)7)] = 9;
        try { BinaryVdf.Read(bad); Check("vdf unknown type rejected", false); } catch (InvalidDataException) { Check("vdf unknown type rejected", true); }

        var home = Path.Combine(dir, "steam-home"); var steam = Path.Combine(home, ".local", "share", "Steam");
        foreach (var id in new[] { "22202", "12345", "0" }) Directory.CreateDirectory(Path.Combine(steam, "userdata", id, "config"));
        Directory.CreateDirectory(Path.Combine(steam, "config"));
        File.WriteAllText(Path.Combine(steam, "config", "loginusers.vdf"), "\"users\"\n{\n\t\"76561197960287930\"\n\t{\n\t\t\"AccountName\"\t\t\"player1\"\n\t\t\"PersonaName\"\t\t\"Player One\"\n\t\t\"MostRecent\"\t\t\"0\"\n\t\t\"Timestamp\"\t\t\"1700000000\"\n\t}\n\t\"76561197960278073\"\n\t{\n\t\t\"AccountName\"\t\t\"deck\"\n\t\t\"PersonaName\"\t\t\"Deck \\\"user\\\"\"\n\t\t\"mostrecent\"\t\t\"1\"\n\t\t\"Timestamp\"\t\t\"1600000000\"\n\t}\n}\n");
        Directory.CreateDirectory(Path.Combine(home, ".steam")); File.CreateSymbolicLink(Path.Combine(home, ".steam", "steam"), steam);
        Directory.CreateDirectory(Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/userdata/999/config"));
        var previous = Environment.GetEnvironmentVariable("XDG_DATA_HOME"); Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);
        List<SteamInstall> installs; try { installs = SteamLocator.Find(home); } finally { Environment.SetEnvironmentVariable("XDG_DATA_HOME", previous); }
        Check("steam roots found once (link deduplicated)", installs.Count == 2 && installs.Count(i => i.Kind == "Steam") == 1 && installs.Any(i => i.Kind == "Flatpak"));
        var native = installs.First(i => i.Kind == "Steam");
        Check("steam account id from SteamID64", native.Accounts.Any(a => a.AccountId == "22202" && a.AccountName == "player1"));
        Check("steam MostRecent account first; account 0 skipped", native.Accounts[0].AccountId == "12345" && native.Accounts[0].PersonaName == "Deck \"user\"" && native.Accounts.All(a => a.AccountId != "0"));
        var account = native.Accounts[0];

        var proc = Path.Combine(dir, "proc"); Directory.CreateDirectory(Path.Combine(proc, "4242")); Directory.CreateDirectory(Path.Combine(proc, "4343"));
        File.WriteAllText(Path.Combine(proc, "4242", "comm"), "bash\n"); File.WriteAllText(Path.Combine(proc, "4242", "status"), "Name:\tbash\nUid:\t1000\t1000\t1000\t1000\n");
        File.WriteAllText(Path.Combine(proc, "4343", "comm"), "steamwebhelper\n"); File.WriteAllText(Path.Combine(proc, "4343", "status"), "Name:\tsteamwebhelper\nUid:\t1001\t1001\t1001\t1001\n");
        Check("steam of another user ignored", SteamLocator.RunningProcesses(proc, 1000).Count == 0);
        Check("steamwebhelper detected", SteamLocator.RunningProcesses(proc, 1001).SequenceEqual(new[] { "steamwebhelper" }));

        Check("shortcut arguments match the Windows Jump List", SteamShortcuts.Arguments("game:ab12") == "--launch-game ab12" && SteamShortcuts.Arguments("emulator:e1") == "--launch-emulator e1" && SteamShortcuts.Arguments("living-room") == "--living-room");
        File.WriteAllBytes(account.ShortcutsFile, bytes);
        var icon = Path.Combine(dir, "emu.png"); File.WriteAllBytes(icon, new byte[] { 137, 80, 78, 71 });
        var cover = Path.Combine(dir, "cover.jpg"); File.WriteAllBytes(cover, new byte[] { 0xFF, 0xD8, 0xFF });
        string exe, start, options; SteamShortcuts.Command(native, "/usr/lib/fishbowl/FishBowl", null, SteamShortcuts.Arguments("game:g1"), out exe, out start, out options);
        Check("command for native steam", exe == "\"/usr/lib/fishbowl/FishBowl\"" && start == "\"/usr/lib/fishbowl/\"" && options == "--launch-game g1");
        var wanted = new List<SteamShortcutSpec> { new SteamShortcutSpec { Key = "game:g1", Name = "Pixel Quest", Exe = exe, StartDir = start, LaunchOptions = options, IconFile = icon, GridFile = cover } };
        SteamShortcuts.Command(native, "/usr/share/dotnet/dotnet", "/opt/fish bowl/FishBowl.dll", "--living-room", out exe, out start, out options);
        Check("command through the dotnet host", options == "\"/opt/fish bowl/FishBowl.dll\" --living-room");
        wanted.Add(new SteamShortcutSpec { Key = "living-room", Name = "FishBowl living room", Exe = exe, StartDir = start, LaunchOptions = options });
        try { SteamShortcuts.Apply(account, wanted, null, () => true, false); Check("refuses to write while Steam runs", false); }
        catch (IOException) { Check("refuses to write while Steam runs", File.ReadAllBytes(account.ShortcutsFile).SequenceEqual(bytes)); }
        var preview = SteamShortcuts.Apply(account, wanted, null, () => true, true);
        Check("dry run lists changes, writes nothing", preview.Added.Count == 2 && File.ReadAllBytes(account.ShortcutsFile).SequenceEqual(bytes));
        var result = SteamShortcuts.Apply(account, wanted, null, () => false, false);
        var written = SteamShortcuts.Load(account.ShortcutsFile).Child("shortcuts");
        Check("added two shortcuts", result.Added.Count == 2 && written.Children.Count == 3 && written.Children.Select(c => c.Key).SequenceEqual(new[] { "0", "1", "2" }));
        Check("backup written", result.Backup != null && File.ReadAllBytes(result.Backup).SequenceEqual(bytes));
        Check("existing entry preserved byte for byte", BinaryVdf.Write(new VdfNode { Children = { written.Children[0] } }).SequenceEqual(BinaryVdf.Write(new VdfNode { Children = { first } })));
        Check("untagged entries are not FishBowl's even with --launch-game", SteamShortcuts.KeyOf(written.Children[0]) == null);
        var game = written.Children[1]; var appId = SteamShortcuts.AppId("\"/usr/lib/fishbowl/FishBowl\"", "Pixel Quest");
        Check("new entry fields", game.GetString("LaunchOptions") == "--launch-game g1" && unchecked((uint)game.Child("appid").IntValue) == appId && game.Child("tags").Children[0].StringValue == "FishBowl" && SteamShortcuts.KeyOf(game) == "game:g1");
        var iconArt = Path.Combine(account.GridDirectory, appId + "_icon.png"); var gridArt = Path.Combine(account.GridDirectory, appId + "p.jpg");
        Check("icon and portrait cover copied to grid", File.Exists(iconArt) && File.Exists(gridArt) && game.GetString("icon") == iconArt);

        // Updating keeps the app ID and Steam's own fields; a cover in another format replaces the old one.
        var reloaded = SteamShortcuts.Load(account.ShortcutsFile); reloaded.Child("shortcuts").Children[1].SetInt("LastPlayTime", 1750000000); File.WriteAllBytes(account.ShortcutsFile, BinaryVdf.Write(reloaded));
        var png = Path.Combine(dir, "cover.png"); File.WriteAllBytes(png, new byte[] { 137, 80, 78, 71 });
        wanted[0].Name = "Pixel Quest (Deluxe)"; wanted[0].GridFile = png;
        result = SteamShortcuts.Apply(account, wanted, null, () => false, false);
        written = SteamShortcuts.Load(account.ShortcutsFile).Child("shortcuts"); game = written.Children[1];
        Check("updated, not duplicated", result.Updated.Contains("Pixel Quest (Deluxe)") && result.Added.Count == 0 && written.Children.Count == 3);
        Check("update keeps appid and play time", unchecked((uint)game.Child("appid").IntValue) == appId && game.Child("LastPlayTime").IntValue == 1750000000);
        Check("cover replaced, not duplicated", File.Exists(Path.Combine(account.GridDirectory, appId + "p.png")) && !File.Exists(gridArt));
        Check("no changes the second time", SteamShortcuts.Apply(account, wanted, null, () => false, true).Changes == 0);

        var unrelated = Path.Combine(account.GridDirectory, appId + "_hero.png"); File.WriteAllBytes(unrelated, new byte[] { 1 });
        result = SteamShortcuts.Apply(account, wanted.Skip(1).ToList(), new[] { "game:g1" }, () => false, false);
        written = SteamShortcuts.Load(account.ShortcutsFile).Child("shortcuts");
        Check("removed FishBowl entry only, renumbered", result.Removed.Count == 1 && written.Children.Count == 2 && written.Children[0].GetString("AppName") == "Sample Tool" && written.Children[1].Key == "1");
        Check("removal deletes FishBowl's artwork only", !File.Exists(iconArt) && !File.Exists(Path.Combine(account.GridDirectory, appId + "p.png")) && File.Exists(unrelated));
        var fresh = native.Accounts.First(a => a.AccountId == "22202");
        SteamShortcuts.Apply(fresh, wanted, null, () => false, false);
        Check("creates shortcuts.vdf when missing", SteamShortcuts.Existing(SteamShortcuts.Load(fresh.ShortcutsFile)).ContainsKey("living-room"));
        SteamShortcuts.Command(installs.First(i => i.Kind == "Flatpak"), "/home/me/FishBowl", null, "--launch-emulator x", out exe, out start, out options);
        Check("flatpak steam uses flatpak-spawn --host", exe == "\"/usr/bin/flatpak-spawn\"" && options == "--host \"/home/me/FishBowl\" --launch-emulator x");
        File.WriteAllBytes(Path.Combine(dir, "bad.vdf"), new byte[] { 0, (byte)'x', 0, 8, 8 });
        try { SteamShortcuts.Load(Path.Combine(dir, "bad.vdf")); Check("foreign vdf refused", false); } catch (InvalidDataException) { Check("foreign vdf refused", true); }
    }

    static void Suites(string dir)
    {
        var home = Path.Combine(dir, "suite-home"); var emulation = Path.Combine(dir, "card", "Emulation");
        Directory.CreateDirectory(Path.Combine(home, ".config", "EmuDeck"));
        File.WriteAllText(Path.Combine(home, ".config", "EmuDeck", "settings.sh"), "#!/bin/bash\nexport emulationPath=\"" + emulation + "\"\nromsPath=\"$emulationPath/roms\" # comment\nbiosPath='" + emulation + "/bios'\ntoolsPath=$emulationPath/tools\nexpert=true\n");
        var values = global::EmulatorHub.Suites.ReadShellSettings(File.ReadAllText(Path.Combine(home, ".config", "EmuDeck", "settings.sh")), home);
        Check("emudeck: settings expand earlier variables", values["romsPath"] == emulation + "/roms" && values["biosPath"] == emulation + "/bios" && values["toolsPath"] == emulation + "/tools");
        Check("emudeck: ~ and $HOME expand", global::EmulatorHub.Suites.ReadShellSettings("a=~/x\nb=${HOME}/y", "/h")["a"] == "/h/x" && global::EmulatorHub.Suites.ReadShellSettings("b=${HOME}/y", "/h")["b"] == "/h/y");
        var gba = Path.Combine(emulation, "roms", "gba"); var psx = Path.Combine(emulation, "roms", "psx"); var gc = Path.Combine(emulation, "roms", "gc");
        foreach (var d in new[] { gba, Path.Combine(gba, "media"), Path.Combine(psx, "Disc Game"), gc, Path.Combine(emulation, "roms", "unknownsys"), Path.Combine(emulation, "tools", "launchers") }) Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(gba, "Pixel Quest.gba"), "x"); File.WriteAllText(Path.Combine(gba, "systeminfo.txt"), "x"); File.WriteAllText(Path.Combine(gba, "media", "Pixel Quest.gba"), "x");
        File.WriteAllText(Path.Combine(psx, "Disc Game", "Disc Game (Disc 1).cue"), "FILE \"Disc Game (Disc 1).bin\" BINARY\n"); File.WriteAllText(Path.Combine(psx, "Disc Game", "Disc Game (Disc 1).bin"), "x");
        File.WriteAllText(Path.Combine(psx, "Disc Game", "Disc Game (Disc 2).cue"), "FILE \"Disc Game (Disc 2).bin\" BINARY\n"); File.WriteAllText(Path.Combine(psx, "Disc Game", "Disc Game (Disc 2).bin"), "x");
        File.WriteAllText(Path.Combine(psx, "Disc Game.m3u"), "Disc Game/Disc Game (Disc 1).cue\nDisc Game/Disc Game (Disc 2).cue\n");
        File.WriteAllText(Path.Combine(gc, "Sample Racer.rvz"), "x"); File.WriteAllText(Path.Combine(gc, "Sample Racer Demo.iso"), "x");
        File.WriteAllText(Path.Combine(emulation, "roms", "unknownsys", "a.bin"), "x");
        File.WriteAllText(Path.Combine(emulation, "tools", "launchers", "dolphin-emu.sh"), "#!/bin/sh\n"); File.WriteAllText(Path.Combine(emulation, "tools", "launchers", "pcsx2-qt.sh"), "#!/bin/sh\n"); File.WriteAllText(Path.Combine(emulation, "tools", "launchers", "notes.sh"), "#!/bin/sh\n");
        var emudeck = global::EmulatorHub.Suites.FindEmuDeck(home);
        Check("emudeck: folders from settings.sh", emudeck != null && emudeck.RomsFolder == emulation + "/roms" && emudeck.BiosFolder == emulation + "/bios");
        Check("emudeck: known systems with games only", emudeck.RomFolders.Select(f => f.Name).SequenceEqual(new[] { "gba", "gc", "psx" }));
        Check("emudeck: media and info files skipped", emudeck.RomFolders.First(f => f.Name == "gba").Games == 1);
        Check("emudeck: m3u covers its discs, cue covers its bin", global::EmulatorHub.Suites.Games(psx, SuiteSystem.For("psx"), 100).Select(Path.GetFileName).SequenceEqual(new[] { "Disc Game.m3u" }));
        Check("emudeck: launcher scripts recognized", emudeck.Emulators.Select(e => e.Name).OrderBy(n => n).SequenceEqual(new[] { "Dolphin", "PCSX2" }));
        Check("emudeck: missing settings means not installed", global::EmulatorHub.Suites.FindEmuDeck(Path.Combine(dir, "nobody")) == null);

        var dolphin = new EmulatorProfile { Id = "d", Name = "Dolphin", Preset = "Dolphin (GameCube / Wii)", Executable = "/fx/dolphin-emu", Extensions = global::EmulatorHub.Suites.ExtensionsFor("Dolphin") };
        var pcsx2 = new EmulatorProfile { Id = "p", Name = "PCSX2", Preset = "PCSX2 (PlayStation 2)", Executable = "/fx/pcsx2", Extensions = new List<string> { ".iso" } };
        Check("suite extensions for a preset", dolphin.Extensions.Contains(".rvz") && dolphin.Extensions.Contains(".wbfs") && !dolphin.Extensions.Contains(".zip"));
        var library = new LibraryData { Emulators = new List<EmulatorProfile> { pcsx2, dolphin }, Games = new List<GameEntry> { new GameEntry { Id = "x", Title = "Sample Racer", Path = Path.Combine(gc, "Sample Racer.rvz") } } };
        var games = global::EmulatorHub.Suites.PrepareImport(library, emudeck.RomFolders.First(f => f.Name == "gc"), 100);
        Check("import: existing game skipped, system emulator chosen over extension match", games.Count == 1 && games[0].Path.EndsWith("Sample Racer Demo.iso") && games[0].EmulatorId == "d" && games[0].ConsoleLabel == "Nintendo GameCube" && !games[0].RequiresEmulatorAssignment);
        library.Emulators.Remove(dolphin);
        games = global::EmulatorHub.Suites.PrepareImport(library, emudeck.RomFolders.First(f => f.Name == "gc"), 100);
        Check("import: another system's emulator is not trusted", games.Count == 1 && games[0].EmulatorId == null && games[0].RequiresEmulatorAssignment);

        // RetroDECK: both config formats, and the Flatpak launcher as its emulator.
        var cfg = global::EmulatorHub.Suites.ReadRetroDeckPaths("[version]\nversion=0.8.1b\n[paths]\nrdhome=/mnt/sd/retrodeck\nroms_folder=/mnt/sd/retrodeck/roms\nbios_folder=/mnt/sd/retrodeck/bios\n[options]\nroms_folder=/wrong\n", "/h");
        Check("retrodeck: cfg [paths]", cfg["home"] == "/mnt/sd/retrodeck" && cfg["roms"] == "/mnt/sd/retrodeck/roms" && cfg["bios"] == "/mnt/sd/retrodeck/bios");
        var json = global::EmulatorHub.Suites.ReadRetroDeckPaths("{\"version\":\"0.9.0b\",\"paths\":{\"rd_home_path\":\"~/retrodeck\",\"roms_path\":\"~/retrodeck/roms\",\"saves_path\":\"~/retrodeck/saves\"}}", "/h");
        Check("retrodeck: json paths", json["home"] == "/h/retrodeck" && json["roms"] == "/h/retrodeck/roms" && json["saves"] == "/h/retrodeck/saves" && !json.ContainsKey("bios"));
        var rdHome = Path.Combine(dir, "rd-home"); var data = Path.Combine(rdHome, ".local", "share");
        var exports = Path.Combine(data, "flatpak", "exports", "bin"); Directory.CreateDirectory(exports); File.WriteAllText(Path.Combine(exports, global::EmulatorHub.Suites.RetroDeckId), "#!/bin/sh\n");
        Directory.CreateDirectory(Path.Combine(rdHome, "retrodeck", "roms", "snes")); File.WriteAllText(Path.Combine(rdHome, "retrodeck", "roms", "snes", "Block Puzzle.sfc"), "x");
        var rd = global::EmulatorHub.Suites.FindRetroDeck(rdHome, data);
        Check("retrodeck: defaults to ~/retrodeck without a config", rd != null && rd.RomsFolder == Path.Combine(rdHome, "retrodeck", "roms") && rd.RomFolders.Single().System.Console == "Super Nintendo");
        Check("retrodeck: flatpak launcher offered", rd.Emulators.Single().Name == "RetroDECK" && rd.Emulators[0].Executable.EndsWith(global::EmulatorHub.Suites.RetroDeckId));
    }

    static void Controllers(string dir)
    {
        var sys = Path.Combine(dir, "sys-input"); var pad = Path.Combine(sys, "event5", "device"); var keyboard = Path.Combine(sys, "event2", "device");
        Directory.CreateDirectory(Path.Combine(pad, "capabilities")); Directory.CreateDirectory(Path.Combine(pad, "id")); Directory.CreateDirectory(Path.Combine(pad, "js0"));
        Directory.CreateDirectory(Path.Combine(keyboard, "capabilities"));
        // BTN_SOUTH is 0x130: bit 48 of the fifth 64-bit word from the right.
        File.WriteAllText(Path.Combine(pad, "capabilities", "key"), "7fdb000000000000 0 0 0 0\n"); File.WriteAllText(Path.Combine(pad, "name"), "Example Gamepad\n");
        File.WriteAllText(Path.Combine(pad, "id", "bustype"), "0005\n"); File.WriteAllText(Path.Combine(pad, "id", "vendor"), "045e\n"); File.WriteAllText(Path.Combine(pad, "id", "product"), "0b13\n");
        File.WriteAllText(Path.Combine(keyboard, "capabilities", "key"), "fffffffffffe\n"); File.WriteAllText(Path.Combine(keyboard, "name"), "Keyboard\n");
        var found = ControllerInventory.Scan(sys, "/dev/input", p => p.EndsWith("event5"));
        Check("controllers: gamepad listed, keyboard skipped", found.Count == 1 && found[0].Name == "Example Gamepad" && found[0].Event == "event5");
        Check("controllers: bus, ids, joystick node, access", found[0].BusName == "Bluetooth" && found[0].VendorProduct == "045e:0b13" && found[0].Joystick == "js0" && found[0].Readable && found[0].Device == "/dev/input/event5");
        Check("controllers: missing sysfs is empty", ControllerInventory.Scan(Path.Combine(dir, "none"), "/dev/input", p => true).Count == 0);
    }
}
