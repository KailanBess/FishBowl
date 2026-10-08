using System; using System.IO; using System.Linq; using System.Collections.Generic; using EmulatorHub;
// Fixture-based checks: every test works in a temporary folder and never touches real emulator data.
static class T {
  static int fails;
  static void Check(string what, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
  static int Main() {
    var root = Path.Combine(Path.GetTempPath(), "fbtest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    // Preset recognition
    Func<string,string> rec = f => EmulatorDiscovery.Recognize(f).Name;
    Check("dolphin-emu", rec("/usr/bin/dolphin-emu") == "Dolphin");
    Check("flatpak wrapper", rec("/var/lib/flatpak/exports/bin/net.pcsx2.PCSX2") == "PCSX2");
    Check("AppImage pcsx2", rec("/x/pcsx2-v2.2.0-linux-appimage-x64-Qt.AppImage") == "PCSX2");
    Check("AppImage dolphin", rec("/x/Dolphin_Emulator-2412-x86_64.AppImage") == "Dolphin");
    Check("AppImage dosbox-x", rec("/x/dosbox-x-0.84.AppImage") == "DOSBox-X");
    Check("AppImage duckstation", rec("/x/DuckStation-x64.AppImage") == "DuckStation");
    Check("azahar stays custom", rec("/x/azahar.AppImage") == "Custom");
    Check("windows exe still works", rec("C:/x/dolphin.exe") == "Dolphin");
    var desk = Path.Combine(root, "d.desktop"); File.WriteAllText(desk, "[Desktop Entry]\nName=Dolphin\nExec=/usr/bin/flatpak run --branch=stable --arch=x86_64 --command=dolphin-emu-wrapper --file-forwarding org.DolphinEmu.dolphin-emu @@ %f @@\nIcon=org.DolphinEmu.dolphin-emu\n");
    Check("desktop flatpak id", Platform.FlatpakId(desk) == "org.DolphinEmu.dolphin-emu");
    Check("desktop recognized", rec(desk) == "Dolphin");
    Check("exec parse", string.Join("|", DesktopEntry.ParseExec("\"/opt/my app/run\" --x %U")) == "/opt/my app/run|--x");
    Check("split args", string.Join("|", Platform.SplitArguments("-a \"b c\" 'd e' f\\ g")) == "-a|b c|d e|f g");
    // Detection with fixture homes
    var home = Path.Combine(root, "home"); var cfg = Path.Combine(home, ".config"); var dat = Path.Combine(home, ".local/share"); var cache = Path.Combine(home, ".cache");
    Directory.CreateDirectory(Path.Combine(cfg, "dolphin-emu")); File.WriteAllText(Path.Combine(cfg, "dolphin-emu", "Dolphin.ini"), "[General]\nNANDRootPath = /mnt/nand\n");
    var det = new EmulatorFolderDetector("", "", Path.Combine(home, "Documents"), false, home, cfg, dat, cache);
    var bin = Path.Combine(root, "bin"); Directory.CreateDirectory(bin); var dolphin = Path.Combine(bin, "dolphin-emu"); File.WriteAllText(dolphin, "#!/bin/sh\n");
    var f = det.Detect(new EmulatorProfile { Executable = dolphin, Preset = "Custom" });
    Func<string,string,string> path = (prop, label) => f.Where(x => x.Property == prop && (label == null || x.Label.Contains(label))).Select(x => x.Path).FirstOrDefault();
    Check("dolphin config", path("ConfigFolder", null) == Path.Combine(cfg, "dolphin-emu"));
    Check("dolphin GC", path("InGameSaveFolder", "GameCube") == Path.Combine(dat, "dolphin-emu", "GC"));
    Check("dolphin NAND from ini", path("InGameSaveFolder", "Wii") == "/mnt/nand/title");
    Check("dolphin states", path("SaveStateFolder", null) == Path.Combine(dat, "dolphin-emu", "StateSaves"));
    // Flatpak wrapper -> sandbox dirs
    var exp = Path.Combine(root, "exports", "bin"); Directory.CreateDirectory(exp); var pcsx = Path.Combine(exp, "net.pcsx2.PCSX2"); File.WriteAllText(pcsx, "");
    f = det.Detect(new EmulatorProfile { Executable = pcsx, Preset = "PCSX2 (PlayStation 2)" });
    Check("pcsx2 flatpak memcards", path("InGameSaveFolder", null) == Path.Combine(home, ".var/app/net.pcsx2.PCSX2/config/PCSX2/memcards"));
    // RetroArch with ~ paths
    Directory.CreateDirectory(Path.Combine(cfg, "retroarch")); File.WriteAllText(Path.Combine(cfg, "retroarch", "retroarch.cfg"), "savefile_directory = \"~/rasaves\"\nsavestate_directory = \"default\"\n");
    var ra = Path.Combine(bin, "retroarch"); File.WriteAllText(ra, "");
    f = det.Detect(new EmulatorProfile { Executable = ra, Preset = "Custom" });
    Check("retroarch ~ saves", path("InGameSaveFolder", null) == Path.Combine(Platform.Home, "rasaves"));
    // Manual override + script notice
    var sh = Path.Combine(bin, "run.sh"); File.WriteAllText(sh, "");
    f = det.Detect(new EmulatorProfile { Executable = sh, Preset = "Custom", ConfigFolder = "~/cfg" });
    Check("script notice", det.Notice.Contains("Shortcuts and scripts"));
    Check("override ~", path("ConfigFolder", null) == Path.Combine(Platform.Home, "cfg"));
    // JSON round trip
    var lib = new LibraryData { Version = 1, Emulators = new List<EmulatorProfile> { new EmulatorProfile { Id = "a", Name = "Dolphin ★", Favorite = true, Builds = new List<EmulatorBuild>() } }, Theme = new ThemeSettings { Name = "Forest", UiScalePercent = 110 } };
    var json = Json.Serialize(lib); var back = Json.Deserialize<LibraryData>(json);
    Check("json roundtrip", back.Emulators[0].Name == "Dolphin ★" && back.Theme.UiScalePercent == 110 && json.Contains("\"Favorite\":true"));
    Check("json case-insensitive", Json.Deserialize<LibraryData>("{\"emulators\":[{\"name\":\"x\"}]}").Emulators[0].Name == "x");
    string s; Check("yaml string", Json.TryParseString("\"a\\\\b\"", out s) && s == "a\\b");
    // SafeChild
    Check("safechild backslash", HubPaths.SafeChild(root, "a\\b.txt") == Path.Combine(root, "a", "b.txt"));
    try { HubPaths.SafeChild(root, "/etc/passwd"); Check("safechild rooted rejected", false); } catch (InvalidDataException) { Check("safechild rooted rejected", true); }
    try { HubPaths.SafeChild(root, "../x"); Check("safechild dotdot rejected", false); } catch (InvalidDataException) { Check("safechild dotdot rejected", true); }
    Check("Same case-sensitive", !HubPaths.Same("/tmp/A", "/tmp/a"));
    // Backup create + restore round trip using a manual override folder
    var saves = Path.Combine(root, "saves"); Directory.CreateDirectory(Path.Combine(saves, "sub")); File.WriteAllText(Path.Combine(saves, "sub", "game.sav"), "hello");
    var prof = new EmulatorProfile { Id = "p1", Name = "Test", Executable = ra, Preset = "Custom", InGameSaveFolder = saves };
    var tok = System.Threading.CancellationToken.None; var backups = Path.Combine(root, "backups");
    var plan = EmulatorBackups.Preview(prof, new[] { "InGameSaveFolder" }, tok);
    var zip = EmulatorBackups.Create(prof, plan, backups, tok);
    File.WriteAllText(Path.Combine(saves, "sub", "game.sav"), "changed");
    var before = EmulatorBackups.Restore(prof, zip, backups, tok);
    Check("backup restore", File.ReadAllText(Path.Combine(saves, "sub", "game.sav")) == "hello" && File.Exists(before));
    // Zip install with exec bit
    var pkg = Path.Combine(root, "pkg.zip");
    using (var z = System.IO.Compression.ZipFile.Open(pkg, System.IO.Compression.ZipArchiveMode.Create)) { var e = z.CreateEntry("emu/dolphin-emu"); e.ExternalAttributes = (0x81ED << 16); using (var w = new StreamWriter(e.Open())) w.Write("#!/bin/sh\necho hi\n"); }
    var installed = EmulatorInstaller.InstallZip(pkg, Path.Combine(root, "emus"), "Dolphin", tok);
    var exe = Path.Combine(installed, "emu", "dolphin-emu");
    Check("zip exec bit", Platform.IsExecutableFile(exe));
    Check("discovery scan", EmulatorDiscovery.Scan(installed, false, tok).Items.Any(i => i.Name == "Dolphin"));
    // Runtime state
    Check("script state unknown", Platform.State(exe) == RuntimeState.Unknown);
    var p = System.Diagnostics.Process.Start("/usr/bin/sleep", "5"); System.Threading.Thread.Sleep(300); Platform.ProcessesChanged();
    Check("running state (sleep)", Platform.State("/usr/bin/sleep") == RuntimeState.Running); p.Kill();
    if (Platform.FindOnPath("pacman") != null)
      Check("version from package manager", (Platform.ProgramVersion("/usr/bin/sleep") ?? "").Contains("coreutils"));
    Check("system scan runs", EmulatorDiscovery.ScanSystem(tok) != null);
    SaveHistoryTests.Run(root, Check);
    SaveMonitorTests.Run(root, Check);
    SessionTests.Run(root, Check);
    GamesTests.Run(root, Check);
    LinuxIntegrationTests.Run(root, Check);
    SingleInstanceTests.Run(root, Check);
    Directory.Delete(root, true);
    RecognitionTests.Run(Check);
    ControllerInputTests.Run(Check);
    LivingRoomTests.Run(Check);
    Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    return fails == 0 ? 0 : 1;
  }
}
