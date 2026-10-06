using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

// Shared FishBowl data model and emulator management logic, used by both the Windows (FishBowl.cs) and
// cross-platform (FishBowl.Avalonia) front ends. Keep this file C# 5 compatible for the Windows csc.exe build;
// OS-specific behaviour belongs in FishBowl.Platform.cs.
namespace EmulatorHub
{








    public static class Store
    {
        private static readonly string PortableMarker = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.flag");
        public static bool PortableMode { get { return File.Exists(PortableMarker); } }
        public static string DataDirectory { get { return PortableMode ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FishBowlData") : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FishBowl"); } }
        public static string FileName { get { return System.IO.Path.Combine(DataDirectory, "library.json"); } }
        private static readonly string LegacyFileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmulatorHub", "library.json");
        // Shows a warning to the user; each front end supplies its own message box.
        public static Action<string> ShowWarning = delegate { };

        public static LibraryData Load()
        {
            try
            {
                var source = File.Exists(FileName) ? FileName : LegacyFileName;
                var data = Json.Deserialize<LibraryData>(File.ReadAllText(source));
                if (data == null) throw new InvalidDataException("The FishBowl settings file is empty or invalid.");
                if (data.Emulators == null) data.Emulators = new List<EmulatorProfile>();
                if (data.Games == null) data.Games = new List<GameEntry>();
                if (data.Links == null) data.Links = new List<WebsiteLink>();
                if (data.Converters == null) data.Converters = new List<GameConverter>();
                if (data.Collections == null) data.Collections = new List<GameCollection>();
                if (data.Theme == null) data.Theme = new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true, StartupAssistantPreferenceSet = true };
                if (data.Theme.AutoBackupDays <= 0) data.Theme.AutoBackupDays = 7;
                if (!data.Theme.StartupAssistantPreferenceSet) data.Theme.ShowStartupAssistant = true;
                if (!data.Theme.GameStorageAssistantPreferenceSet) data.Theme.ShowGameStorageAssistant = true;
                if (data.Theme.CustomizationVersion < 1) { data.Theme.AutoSyncGameFolders = true; data.Theme.CustomizationVersion = 1; }
                if (data.Theme.CustomizationVersion < 2) { data.Theme.AccentColor = "Sunset"; data.Theme.FontFamily = "Bahnschrift"; data.Theme.UiScalePercent = 100; data.Theme.ListDensity = "Standard"; data.Theme.ShowBanner = true; data.Theme.ShowStatusBar = true; data.Theme.ShowInformationPanel = true; data.Theme.ShowEmulatorIcons = true; data.Theme.EnableMotion = true; data.Theme.CustomizationVersion = 2; }
                if (data.Theme.CustomizationVersion < 3) { data.Theme.AlternateRowShading = true; data.Theme.SelectionContrast = "Standard"; data.Theme.IconTileShape = "Rounded"; data.Theme.CustomizationVersion = 3; }
                foreach (var emulator in data.Emulators) { if (emulator.LaunchProfiles == null) emulator.LaunchProfiles = new List<LaunchProfile>(); if (emulator.Builds == null) emulator.Builds = new List<EmulatorBuild>(); }
                foreach (var game in data.Games) if (game.Tags == null) game.Tags = new List<string>();
                foreach (var collection in data.Collections) if (collection.GameIds == null) collection.GameIds = new List<string>();
                LibraryPaths.Load(data, DataDirectory, PortableMode ? AppDomain.CurrentDomain.BaseDirectory : null);
                return data;
            }
            catch (Exception error)
            {
                var source = File.Exists(FileName) ? FileName : (File.Exists(LegacyFileName) ? LegacyFileName : null);
                if (source != null)
                {
                    var recovery = source + ".recovery-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".json";
                    try { File.Copy(source, recovery, false); }
                    catch (Exception copyError) { throw new IOException("FishBowl could not read its settings or create a recovery copy. Your existing settings are left in place.", copyError); }
                    Log("Settings load failed: " + error.Message + "; recovery: " + recovery);
                    ShowWarning("FishBowl could not read its settings. A recovery copy was saved before opening an empty hub.\n\n" + recovery);
                }
                return new LibraryData { Version = 1, Emulators = new List<EmulatorProfile>(), Games = new List<GameEntry>(), Links = new List<WebsiteLink>(), Converters = new List<GameConverter>(), Collections = new List<GameCollection>(), Theme = new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true, StartupAssistantPreferenceSet = true, ShowGameStorageAssistant = true, GameStorageAssistantPreferenceSet = true, AutoSyncGameFolders = true, AccentColor = "Sunset", FontFamily = "Bahnschrift", UiScalePercent = 100, ListDensity = "Standard", ShowBanner = true, ShowStatusBar = true, ShowInformationPanel = true, ShowEmulatorIcons = true, EnableMotion = true, AlternateRowShading = true, SelectionContrast = "Standard", IconTileShape = "Rounded", CustomizationVersion = 3 } };
            }
        }

        public static void Save(LibraryData data)
        {
            LibraryPaths.Stamp(data, DataDirectory, PortableMode ? AppDomain.CurrentDomain.BaseDirectory : null);
            Directory.CreateDirectory(DataDirectory);
            var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Json.Serialize(data));
                if (File.Exists(FileName)) File.Replace(temporary, FileName, FileName + ".bak", true);
                else File.Move(temporary, FileName);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string LogFileName { get { return System.IO.Path.Combine(DataDirectory, "activity.log"); } }
        public static void Log(string message)
        {
            try { Directory.CreateDirectory(DataDirectory); File.AppendAllText(LogFileName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine); }
            catch { }
        }

        public static void EnablePortableMode(LibraryData data)
        {
            File.WriteAllText(PortableMarker, "FishBowl portable mode");
            Save(data);
        }

    }

    public class EmulatorPreset
    {
        public string Name; public string Platform; public string Website; public string[] Executables;
        // Linux: program names on PATH or inside release archives, and the Flathub application ID.
        public string[] UnixExecutables = new string[0]; public string FlatpakId;
        public EmulatorPreset(string name, string platform, string website, params string[] executables) { Name = name; Platform = platform; Website = website; Executables = executables; }
        public EmulatorPreset Linux(string flatpakId, params string[] executables) { FlatpakId = flatpakId; UnixExecutables = executables; return this; }
        // AppImage file names start with the project name, e.g. "Dolphin_Emulator-2412-x86_64.AppImage".
        public IEnumerable<string> AppImagePrefixes { get { return new[] { Name }.Concat(UnixExecutables).Select(EmulatorCatalog.Compact).Where(p => p.Length >= 3).Distinct(); } }
        public string Label { get { return Name + " (" + Platform + ")"; } }
        public override string ToString() { return Label; }
    }

    public static class EmulatorCatalog
    {
        public static readonly EmulatorPreset[] Presets = new[]
        {
            new EmulatorPreset("Custom", "Other emulator", ""),
            new EmulatorPreset("Mesen", "NES / SNES / Game Boy / PC Engine", "https://www.mesen.ca/", "mesen.exe").Linux(null, "Mesen", "mesen"),
            new EmulatorPreset("Snes9x", "SNES", "https://www.snes9x.com/", "snes9x.exe", "snes9x-x64.exe").Linux("com.snes9x.Snes9x", "snes9x-gtk", "snes9x-qt", "snes9x"),
            new EmulatorPreset("bsnes", "SNES", "https://github.com/bsnes-emu/bsnes", "bsnes.exe").Linux("dev.bsnes.bsnes", "bsnes"),
            new EmulatorPreset("SameBoy", "Game Boy / Game Boy Color", "https://sameboy.github.io/", "sameboy.exe").Linux("io.github.sameboy.SameBoy", "sameboy"),
            new EmulatorPreset("mGBA", "Game Boy Advance", "https://mgba.io/", "mgba.exe").Linux("io.mgba.mGBA", "mgba-qt", "mgba"),
            new EmulatorPreset("melonDS", "Nintendo DS / DSi", "https://github.com/melonDS-emu/melonDS", "melonds.exe").Linux("net.kuribo64.melonDS", "melonDS"),
            new EmulatorPreset("DeSmuME", "Nintendo DS", "https://desmume.org/", "desmume.exe", "desmume_0.9.13_x64.exe").Linux("org.desmume.DeSmuME", "desmume", "desmume-cli"),
            new EmulatorPreset("Azahar Plus", "Nintendo 3DS", "https://github.com/AzaharPlus/AzaharPlus", "azaharplus.exe", "azahar-plus.exe").Linux(null, "azahar-plus", "azaharplus"),
            new EmulatorPreset("Gopher64", "Nintendo 64", "https://github.com/gopher64/gopher64", "gopher64.exe", "gopher64-windows-x86_64.exe").Linux("io.github.gopher64.gopher64", "gopher64"),
            new EmulatorPreset("RMG", "Nintendo 64", "https://github.com/Rosalie241/RMG", "rmg.exe").Linux("com.github.Rosalie241.RMG", "RMG"),
            new EmulatorPreset("Dolphin", "GameCube / Wii", "https://dolphin-emu.org/", "dolphin.exe").Linux("org.DolphinEmu.dolphin-emu", "dolphin-emu", "dolphin-emu-nogui"),
            new EmulatorPreset("Cemu", "Wii U", "https://cemu.info/", "cemu.exe").Linux("info.cemu.Cemu", "Cemu", "cemu"),
            new EmulatorPreset("Eden", "Nintendo Switch", "https://eden-emu.dev/", "eden.exe").Linux("dev.eden_emu.eden", "eden"),
            new EmulatorPreset("DuckStation", "PlayStation", "https://github.com/stenzek/duckstation", "duckstation.exe", "duckstation-qt-x64-release.exe").Linux(null, "duckstation-qt", "duckstation"),
            new EmulatorPreset("PCSX2", "PlayStation 2", "https://pcsx2.net/", "pcsx2.exe", "pcsx2-qt.exe").Linux("net.pcsx2.PCSX2", "pcsx2-qt", "pcsx2"),
            new EmulatorPreset("RPCS3", "PlayStation 3", "https://github.com/RPCS3/rpcs3", "rpcs3.exe").Linux("net.rpcs3.RPCS3", "rpcs3"),
            new EmulatorPreset("shadPS4", "PlayStation 4 - experimental", "https://github.com/shadps4-emu/shadPS4", "shadps4.exe").Linux("net.shadps4.shadPS4", "shadps4", "shadPS4"),
            new EmulatorPreset("PPSSPP", "PSP", "https://www.ppsspp.org/", "ppssppwindows64.exe", "ppssppwindows.exe").Linux("org.ppsspp.PPSSPP", "PPSSPPSDL", "PPSSPPQt", "ppsspp", "ppsspp-sdl", "ppsspp-qt"),
            new EmulatorPreset("Vita3K", "PS Vita - experimental", "https://vita3k.org/", "vita3k.exe").Linux(null, "Vita3K"),
            new EmulatorPreset("xemu", "Xbox", "https://xemu.app/", "xemu.exe").Linux("app.xemu.xemu", "xemu"),
            new EmulatorPreset("Xenia Canary", "Xbox 360 - experimental", "https://github.com/xenia-canary/xenia-canary", "xenia_canary.exe").Linux(null, "xenia_canary", "xenia-canary"),
            new EmulatorPreset("BlastEm", "Mega Drive / Genesis", "https://www.retrodev.com/blastem/", "blastem.exe").Linux("com.retrodev.blastem", "blastem"),
            new EmulatorPreset("Ymir", "Sega Saturn", "https://github.com/ymir-emu/Ymir", "ymir-sdl3.exe", "ymir.exe").Linux("io.github.strikerx3.ymir", "ymir-sdl3", "ymir"),
            new EmulatorPreset("Flycast", "Dreamcast / NAOMI / Atomiswave", "https://github.com/flyinghead/flycast", "flycast.exe").Linux("org.flycast.Flycast", "flycast"),
            new EmulatorPreset("Stella", "Atari 2600", "https://stella-emu.github.io/", "stella.exe").Linux("io.github.stella_emu.Stella", "stella"),
            new EmulatorPreset("Altirra", "Atari 8-bit / 5200", "https://www.virtualdub.org/altirra.html", "altirra64.exe", "altirra.exe"),
            new EmulatorPreset("BigPEmu", "Atari Jaguar / Jaguar CD", "https://www.richwhitehouse.com/jaguar/", "bigpemu.exe").Linux("com.richwhitehouse.BigPEmu", "bigpemu"),
            new EmulatorPreset("Hatari", "Atari ST / STE / TT / Falcon", "https://hatari.tuxfamily.org/", "hatari.exe").Linux("org.tuxfamily.hatari", "hatari"),
            new EmulatorPreset("MAME", "Arcade / multiple systems", "https://www.mamedev.org/", "mame.exe", "mame64.exe").Linux("org.mamedev.MAME", "mame"),
            new EmulatorPreset("FinalBurn Neo", "Arcade / Neo Geo", "https://github.com/finalburnneo/FBNeo", "fbneo.exe", "fbneo64.exe", "fbn.exe", "fbn64.exe").Linux(null, "fbneo"),
            new EmulatorPreset("ares", "Multiple systems", "https://ares-emu.net/", "ares.exe").Linux("dev.ares.ares", "ares"),
            new EmulatorPreset("RetroArch", "Multiple systems / cores", "https://www.retroarch.com/", "retroarch.exe").Linux("org.libretro.RetroArch", "retroarch"),
            new EmulatorPreset("BizHawk", "Multiple systems / TAS", "https://github.com/TASEmulators/BizHawk", "emuhawk.exe").Linux(null, "EmuHawkMono.sh", "emuhawk"),
            new EmulatorPreset("DOSBox Staging", "DOS games", "https://www.dosbox-staging.org/", "dosbox.exe").Linux("io.github.dosbox-staging", "dosbox-staging", "dosbox"),
            new EmulatorPreset("DOSBox-X", "DOS / Windows 3.x / 9x", "https://dosbox-x.com/", "dosbox-x.exe").Linux("com.dosbox_x.DOSBox-X", "dosbox-x"),
            new EmulatorPreset("86Box", "Classic IBM PCs", "https://86box.net/", "86box.exe").Linux("net._86box._86Box", "86Box"),
            new EmulatorPreset("ScummVM", "Classic adventure games", "https://www.scummvm.org/", "scummvm.exe").Linux("org.scummvm.ScummVM", "scummvm"),
            new EmulatorPreset("WinUAE", "Amiga / CD32 / CDTV", "https://github.com/tonioni/WinUAE", "winuae.exe", "winuae64.exe"),
            new EmulatorPreset("VICE", "Commodore 64 / 128 / VIC-20", "https://vice-emu.sourceforge.io/", "x64sc.exe", "x128.exe", "xvic.exe").Linux("net.sf.VICE", "x64sc", "x128", "xvic"),
            new EmulatorPreset("openMSX", "MSX / MSX2", "https://openmsx.org/", "openmsx.exe").Linux("org.openmsx.openMSX", "openmsx"),
            new EmulatorPreset("Caprice32", "Amstrad CPC", "https://github.com/ColinPitrat/caprice32", "cap32.exe", "caprice32.exe").Linux(null, "cap32"),
            new EmulatorPreset("ZEsarUX", "ZX Spectrum / ZX81 / ZX Next", "https://github.com/chernandezba/zesarux", "zesarux.exe").Linux(null, "zesarux"),
        };
        public static EmulatorPreset Find(string label)
        {
            return Presets.FirstOrDefault(p => String.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase) || String.Equals(p.Name, label, StringComparison.OrdinalIgnoreCase)) ?? Presets.FirstOrDefault(p => !String.IsNullOrWhiteSpace(label) && label.StartsWith(p.Name + " (", StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
        }
        public static EmulatorPreset ForExecutable(string executable)
        {
            var name = Path.GetFileName(executable);
            var known = Presets.FirstOrDefault(p => p.Executables.Any(n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase)));
            if (known != null || Platform.IsWindows) return known ?? Presets[0];
            return ForUnixProgram(executable) ?? Presets[0];
        }
        // Linux launchers: Flatpak IDs, program names (also behind .desktop entries), and AppImage names.
        private static EmulatorPreset ForUnixProgram(string executable)
        {
            var flatpak = Platform.FlatpakId(executable);
            if (flatpak != null) return Presets.FirstOrDefault(p => String.Equals(p.FlatpakId, flatpak, StringComparison.OrdinalIgnoreCase));
            var name = Path.GetFileName(executable);
            if (name.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase))
            {
                var entry = DesktopEntry.Read(executable);
                if (entry == null || entry.Command.Count == 0) return null;
                name = Path.GetFileName(entry.Command[0]);
            }
            var byName = Presets.FirstOrDefault(p => p.UnixExecutables.Any(n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase)));
            if (byName != null || !name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)) return byName;
            var compact = Compact(Path.GetFileNameWithoutExtension(name));
            return Presets.Skip(1).SelectMany(p => p.AppImagePrefixes.Select(prefix => Tuple.Create(p, prefix)))
                .Where(t => compact.StartsWith(t.Item2, StringComparison.Ordinal)).OrderByDescending(t => t.Item2.Length).Select(t => t.Item1).FirstOrDefault();
        }
        public static string Compact(string text) { return new string((text ?? "").ToLowerInvariant().Where(Char.IsLetterOrDigit).ToArray()); }
    }

    public class EmulatorReference
    {
        public string Summary, Strengths, Limitations, Requirements, Controllers;
        public string Website, Documentation, Compatibility, Releases, Changelog, ControllerGuide, Troubleshooting;
        public static string PlatformFor(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.Platform)) return profile.Platform;
            var preset = EmulatorCatalog.Find(profile.Preset);
            return preset.Name == "Custom" ? "Other emulator" : preset.Platform.Replace(" - experimental", "");
        }
        public static string[] PlatformTags(EmulatorProfile profile) { return PlatformFor(profile).Split(new[] { '/', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
        public static string VersionFor(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.ManualVersion)) return profile.ManualVersion + " (entered manually)";
            try
            {
                var version = Platform.ProgramVersion(profile.Executable);
                return String.IsNullOrWhiteSpace(version) || version == "0.0.0.0" ? "Not reported by this program" : version;
            }
            catch { return "Version unavailable"; }
        }
        public static bool IsLaunchFile(string path) { return Platform.IsLaunchFile(path); }
        public static bool IsWebUrl(string value) { Uri uri; return Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps); }
        private static string Pick(string saved, string fallback) { return String.IsNullOrWhiteSpace(saved) ? fallback : saved; }
        public static EmulatorReference For(EmulatorProfile profile)
        {
            var p = EmulatorCatalog.Find(profile.Preset);
            var site = p.Website ?? "";
            var github = site.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase);
            var result = new EmulatorReference {
                Summary = p.Name == "Custom" ? "Your own emulator or emulator frontend. FishBowl can launch any installed " + Platform.OsName + " emulator, independently of the preset list." : p.Name + " is an emulator or frontend for " + p.Platform + ".",
                Strengths = "Supported systems: " + PlatformFor(profile) + ". See the project documentation for the available features and input options.",
                Limitations = p.Platform.IndexOf("experimental", StringComparison.OrdinalIgnoreCase) >= 0 ? "Experimental emulator: features and compatibility are incomplete and depend on the build and title." : "Compatibility, performance, and available features depend on the emulator build, the emulated system, and your PC. Check the project's current guidance.",
                Requirements = "BIOS / firmware: follow the emulator's official setup guide for the required files and setup steps.\r\n\r\nHardware / graphics: check the project's current CPU, GPU, graphics API, and driver requirements. Configure graphics inside the emulator.\r\n\r\nFishBowl only opens the emulator. All game setup stays inside it.",
                Controllers = "Set up controllers in the emulator's Input or Controller settings. Choose the input backend offered by that emulator, map the buttons, and save the configuration there.\r\n\r\nSupport for gamepads, keyboards, adapters, motion controls, and multiple players varies by emulator. Use its controller documentation for device-specific guidance.",
                Website = site, Documentation = github ? site + "#readme" : site, Releases = github ? site + "/releases" : site,
                Changelog = github ? site + "/releases" : site, Compatibility = "", ControllerGuide = github ? site + "#readme" : site, Troubleshooting = github ? site + "/issues" : site
            };
            switch (p.Name)
            {
                case "Azahar Plus":
                    result.Summary = "A Nintendo 3DS emulator based on Azahar, with additional compatibility and features.";
                    result.Strengths = "Support for additional Citra-compatible game formats, older CPUs, ZipPass, built-in cheats, and added multiplayer features.";
                    result.Requirements = "Set up the system files and configuration inside Azahar Plus.\r\n\r\nThe project lists Windows 10 64-bit, OpenGL 4.3 or Vulkan 1.1, and 2 GB RAM as desktop minimums. Check its current requirements before choosing settings.";
                    result.Documentation = "https://github.com/AzaharPlus/AzaharPlus/wiki";
                    break;
                case "Dolphin":
                    result.Summary = "Emulates Nintendo GameCube and Wii on PC.";
                    result.Strengths = "GameCube and Wii emulation, controller configuration, graphics improvements, and netplay options.";
                    result.Documentation = "https://dolphin-emu.org/docs/guides/";
                    result.ControllerGuide = "https://dolphin-emu.org/docs/guides/";
                    result.Compatibility = "https://dolphin-emu.org/compat/";
                    result.Releases = result.Changelog = "https://dolphin-emu.org/download/";
                    result.Troubleshooting = "https://dolphin-emu.org/docs/guides/";
                    result.Controllers = "Supports emulated GameCube pads and Wii remotes with configurable keyboard / controller mappings. Official GameCube adapters and Wii-specific controls have their own setup guides. Open Controller guide for the project's instructions.";
                    break;
                case "PCSX2":
                    result.Documentation = "https://pcsx2.net/docs/"; result.Compatibility = "https://pcsx2.net/compat/";
                    result.Releases = "https://pcsx2.net/downloads/"; result.Changelog = "https://github.com/PCSX2/pcsx2/releases";
                    result.ControllerGuide = "https://pcsx2.net/docs/"; result.Troubleshooting = "https://pcsx2.net/docs/category/troubleshooting/";
                    result.Requirements = "A PlayStation 2 BIOS is required. Follow PCSX2's official setup guide for BIOS setup and current CPU, GPU, graphics API, and runtime requirements.";
                    break;
                case "RPCS3":
                    result.Documentation = "https://rpcs3.net/quickstart"; result.Compatibility = "https://rpcs3.net/compatibility";
                    result.Releases = result.Changelog = "https://rpcs3.net/download"; result.ControllerGuide = "https://rpcs3.net/quickstart"; result.Troubleshooting = "https://wiki.rpcs3.net/";
                    result.Requirements = "Install the PlayStation 3 system software inside RPCS3 following the official quickstart guide. Review its current CPU, GPU, graphics driver, and platform requirements.";
                    break;
                case "DuckStation":
                    result.Requirements = "A PlayStation BIOS image is required. Follow the project's README for BIOS setup, supported graphics backends, and system requirements.";
                    break;
                case "PPSSPP":
                    result.Documentation = "https://www.ppsspp.org/docs/"; result.Releases = "https://www.ppsspp.org/download/"; result.Changelog = "https://www.ppsspp.org/news/";
                    result.ControllerGuide = "https://www.ppsspp.org/docs/settings/"; result.Troubleshooting = "https://www.ppsspp.org/docs/troubleshooting/";
                    break;
                case "Vita3K": result.Compatibility = "https://vita3k.org/compatibility.html"; result.Documentation = "https://vita3k.org/quickstart.html"; break;
                case "Cemu": result.Compatibility = "https://compat.cemu.info/"; result.Documentation = "https://cemu.info/"; break;
                case "xemu": result.Documentation = "https://xemu.app/docs/"; result.Compatibility = "https://xemu.app/#compatibility"; result.Controllers = "xemu uses SDL for input and supports up to four controllers. Configure input inside xemu and consult its controller documentation for mapping and supported devices."; break;
                case "RetroArch":
                    result.Summary = "A frontend that runs many emulator cores through a shared interface.";
                    result.Strengths = "One interface and a shared controller setup for many systems and cores.";
                    result.Documentation = "https://docs.libretro.com/"; result.ControllerGuide = "https://docs.libretro.com/guides/input-and-controls/";
                    result.Releases = "https://www.retroarch.com/"; result.Changelog = "https://github.com/libretro/RetroArch/releases"; result.Troubleshooting = "https://docs.libretro.com/";
                    result.Requirements = "Install the appropriate core inside RetroArch. BIOS / firmware requirements depend on the selected core and system; consult its core documentation. Choose graphics and input drivers inside RetroArch.";
                    break;
                case "ares": result.Summary = "A multi-system emulator with a focus on accuracy and preservation."; break;
                case "BigPEmu": result.Strengths = "Atari Jaguar and Jaguar CD support. The project reports compatibility with the complete Jaguar retail cartridge library."; break;
            }
            result.Summary = Pick(profile.Description, result.Summary); result.Strengths = Pick(profile.Strengths, result.Strengths); result.Limitations = Pick(profile.Limitations, result.Limitations);
            result.Requirements = Pick(profile.Requirements, result.Requirements); result.Controllers = Pick(profile.ControllerInfo, result.Controllers);
            result.Website = Pick(profile.WebsiteUrl, result.Website); result.Documentation = Pick(profile.DocumentationUrl, result.Documentation); result.Compatibility = Pick(profile.CompatibilityUrl, result.Compatibility);
            result.Releases = Pick(profile.ReleasesUrl, result.Releases); result.Changelog = Pick(profile.ChangelogUrl, result.Changelog); result.ControllerGuide = Pick(profile.ControllerGuideUrl, result.ControllerGuide); result.Troubleshooting = Pick(profile.TroubleshootingUrl, result.Troubleshooting);
            return result;
        }
    }

    public class EmulatorFolder
    {
        public string Property, Label, Path, Source;
        public EmulatorFolder(string property, string label, string path, string source)
        { Property = property; Label = label; Path = path ?? ""; Source = source; }
    }

    // Read-only detection. Blank profile paths use detection; nonblank paths are manual overrides.
    public class EmulatorFolderDetector
    {
        private readonly string roaming, local, documents;
        private readonly bool readRegistry;
        // Linux: user home, XDG configuration/data/cache homes, and the launcher's Flatpak ID (null for native programs).
        private readonly string home, configHome, dataHome, cacheHome;
        private string flatpak;
        private readonly List<EmulatorFolder> folders = new List<EmulatorFolder>();
        private string program;
        public string Notice { get; private set; }
        public EmulatorFolderDetector() : this(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Platform.Documents, Platform.IsWindows) { }
        public EmulatorFolderDetector(string roamingPath, string localPath, string documentsPath, bool registry)
            : this(roamingPath, localPath, documentsPath, registry, Platform.Home, Platform.ConfigHome, Platform.DataHome, Platform.CacheHome) { }
        public EmulatorFolderDetector(string roamingPath, string localPath, string documentsPath, bool registry, string homePath, string configPath, string dataPath, string cachePath)
        {
            roaming = roamingPath; local = localPath; documents = documentsPath; readRegistry = registry;
            home = homePath; configHome = configPath; dataHome = dataPath; cacheHome = cachePath;
        }
        public List<EmulatorFolder> Detect(EmulatorProfile profile)
        {
            folders.Clear(); Notice = ""; program = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                program = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(profile.Executable));
                flatpak = Platform.FlatpakId(profile.Executable);
                if (flatpak != null) Add("Program", "Program folder", Platform.FlatpakFilesFolder(flatpak) ?? program, "Flatpak " + flatpak);
                else Add("Program", "Program folder", program, "Emulator location");
                if (!Platform.IsDirectProgram(profile.Executable))
                    Notice = "Shortcuts and scripts can redirect storage. Choose their emulator's folders manually.";
                else DetectKnown(profile);
            }
            catch (Exception ex) { Notice = "Folder detection could not finish: " + ex.Message; }
            foreach (var entry in new[] { "ConfigFolder|Configuration", "InGameSaveFolder|In-game saves", "SaveStateFolder|Save states", "ScreenshotFolder|Screenshots", "LogFolder|Logs" })
            {
                var parts = entry.Split('|'); var custom = profile.GetType().GetProperty(parts[0]).GetValue(profile, null) as string;
                if (!String.IsNullOrWhiteSpace(custom))
                {
                    folders.RemoveAll(f => f.Property == parts[0]);
                    try { Add(parts[0], parts[1], Resolve(custom, program), "Manual override"); }
                    catch (Exception) { Add(parts[0], parts[1], custom, "Manual path is invalid or unavailable"); }
                }
                else if (!folders.Any(f => f.Property == parts[0]))
                    Add(parts[0], parts[1], "", "Not detected - choose manually");
            }
            if (!String.IsNullOrWhiteSpace(profile.SaveFolder))
                Add("SaveFolder", "Previous save shortcut (unclassified)", profile.SaveFolder, "Preserved - select its save type below");
            return folders.OrderBy(f => Array.IndexOf(new[] { "InGameSaveFolder", "SaveStateFolder", "ConfigFolder", "ScreenshotFolder", "LogFolder", "Program", "SaveFolder" }, f.Property)).ToList();
        }
        private void DetectKnown(EmulatorProfile profile)
        {
            var name = EmulatorCatalog.Find(profile.Preset).Name;
            if (name == "Custom") name = EmulatorCatalog.ForExecutable(profile.Executable).Name;
            var exe = System.IO.Path.GetFileNameWithoutExtension(profile.Executable).ToLowerInvariant();
            if (exe.StartsWith("desmume")) name = "DeSmuME";
            if (exe.StartsWith("pcsx2")) name = "PCSX2";
            if (exe.StartsWith("duckstation")) name = "DuckStation";
            if (exe.StartsWith("ppsspp")) name = "PPSSPP";
            if ((exe == "azahar" || EmulatorCatalog.Compact(exe).StartsWith("azahar") || flatpak == "org.azahar_emu.Azahar") && name == "Custom")
                Notice = "Select the Azahar Plus preset to detect this installation's 3DS storage.";
            string data, config, configDir;
            Dictionary<string, string> settings;
            switch (name)
            {
                case "DeSmuME":
                    if (!Platform.IsWindows)
                    {
                        Add("ConfigFolder", "Configuration", ConfigRoot("desmume"), "DeSmuME configuration");
                        Notice = "DeSmuME for Linux keeps its save paths in its own settings. Choose in-game save and save-state folders manually.";
                        break;
                    }
                    config = P(program, "desmume.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", program, "desmume.ini");
                    Configured(settings, "PathSettings/Battery", "InGameSaveFolder", "In-game saves (Battery)", program, "Battery", config);
                    Configured(settings, "PathSettings/StateSlots", "SaveStateFolder", "Save states (slots)", program, "StateSlots", config);
                    Configured(settings, "PathSettings/States", "SaveStateFolder", "Save states (manual files)", program, "States", config);
                    Configured(settings, "PathSettings/Screenshots", "ScreenshotFolder", "Screenshots", program, "Screenshots", config);
                    break;
                case "Dolphin":
                    data = DolphinRoot(out configDir); config = P(configDir, "Dolphin.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", configDir, "Dolphin user directory");
                    Add("InGameSaveFolder", "In-game saves (GameCube)", P(data, "GC"), "Dolphin memory-card storage");
                    foreach (var key in new[] { "Core/MemcardAPath", "Core/MemcardBPath", "Core/GCIFolderAPath", "Core/GCIFolderBPath" })
                    {
                        var value = Get(settings, key);
                        if (!String.IsNullOrWhiteSpace(value))
                        {
                            var card = Resolve(value, program);
                            if (key.IndexOf("card", StringComparison.OrdinalIgnoreCase) >= 0) card = System.IO.Path.GetDirectoryName(card);
                            Add("InGameSaveFolder", "In-game saves (custom GameCube)", card, "Dolphin.ini");
                        }
                    }
                    var nand = SettingPath(settings, "General/NANDRootPath", program, P(data, "Wii"));
                    Add("InGameSaveFolder", "In-game saves (Wii titles)", P(nand, "title"), String.IsNullOrWhiteSpace(Get(settings, "General/NANDRootPath")) ? "Dolphin Wii storage" : "Dolphin.ini");
                    Add("SaveStateFolder", "Save states", P(data, "StateSaves"), "Dolphin user directory");
                    Add("ScreenshotFolder", "Screenshots", P(data, "ScreenShots"), "Dolphin user directory");
                    Add("LogFolder", "Logs", P(data, "Logs"), "Dolphin user directory");
                    Notice = "GameCube and Wii use separate storage. Title/SD/NAND folders may also contain installed content.";
                    break;
                case "Azahar Plus":
                    data = Directory.Exists(P(program, "user")) ? P(program, "user") : Platform.IsWindows ? P(roaming, "Azahar") : DataRoot("azahar-emu");
                    configDir = Directory.Exists(P(program, "user")) || Platform.IsWindows ? P(data, "config") : ConfigRoot("azahar-emu");
                    config = P(configDir, "qt-config.ini"); settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", configDir, "Azahar Plus user directory");
                    var customStorage = IsTrue(Get(settings, "Data Storage/use_custom_storage")) && !IsTrue(Get(settings, "Data Storage/use_custom_storage/default"));
                    var sd = customStorage ? SettingPath(settings, "Data Storage/sdmc_directory", program, P(data, "sdmc")) : P(data, "sdmc");
                    nand = customStorage ? SettingPath(settings, "Data Storage/nand_directory", program, P(data, "nand")) : P(data, "nand");
                    Add("InGameSaveFolder", "In-game saves (3DS SD storage)", P(sd, "Nintendo 3DS"), customStorage ? "qt-config.ini" : "Azahar Plus user directory");
                    Add("InGameSaveFolder", "In-game saves (3DS NAND data)", P(nand, "data"), customStorage ? "qt-config.ini" : "Azahar Plus user directory");
                    Add("SaveStateFolder", "Save states", P(data, "states"), "Azahar Plus user directory");
                    Configured(settings, "Paths/screenshotPath", "ScreenshotFolder", "Screenshots", program, "", config);
                    Add("LogFolder", "Logs", P(data, "log"), "Azahar Plus user directory");
                    Notice = "3DS saves live inside SD/NAND storage, which also contains installed content.";
                    break;
                case "PCSX2":
                    data = File.Exists(P(program, "portable.txt")) || File.Exists(P(program, "portable.ini")) ? program : Platform.IsWindows ? P(documents, "PCSX2") : ConfigRoot("PCSX2");
                    config = P(data, "inis", "PCSX2.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", P(data, "inis"), "PCSX2 data directory");
                    Configured(settings, "Folders/MemoryCards", "InGameSaveFolder", "In-game saves (memory cards)", data, "memcards", config);
                    Configured(settings, "Folders/Savestates", "SaveStateFolder", "Save states", data, "sstates", config);
                    Configured(settings, "Folders/Snapshots", "ScreenshotFolder", "Screenshots", data, "snaps", config);
                    Configured(settings, "Folders/Logs", "LogFolder", "Logs", data, "logs", config);
                    break;
                case "DuckStation":
                    data = File.Exists(P(program, "portable.txt")) ? program : !Platform.IsWindows ? DataRoot("duckstation") :
                        Directory.Exists(P(local, "DuckStation")) ? P(local, "DuckStation") :
                        Directory.Exists(P(documents, "DuckStation")) ? P(documents, "DuckStation") : P(local, "DuckStation");
                    config = P(data, "settings.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", data, "DuckStation data directory");
                    Configured(settings, "MemoryCards/Directory", "InGameSaveFolder", "In-game saves (memory cards)", data, "memcards", config);
                    Configured(settings, "Folders/SaveStates", "SaveStateFolder", "Save states", data, "savestates", config);
                    Configured(settings, "Folders/Screenshots", "ScreenshotFolder", "Screenshots", data, "screenshots", config);
                    if (File.Exists(P(data, "duckstation.log"))) Add("LogFolder", "Logs", data, "duckstation.log");
                    break;
                case "melonDS":
                    data = FindConfigRoot(new[] { program, Platform.IsWindows ? P(roaming, "melonDS") : ConfigRoot("melonDS") }, new[] { "melonDS.toml", "melonDS.ini" });
                    if (data == null) { Notice = "Open melonDS once to create its configuration, then refresh folders."; break; }
                    config = File.Exists(P(data, "melonDS.toml")) ? P(data, "melonDS.toml") : P(data, "melonDS.ini");
                    settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", data, System.IO.Path.GetFileName(config));
                    Configured(settings, FirstKey(settings, "Instance0/SaveFilePath", "SaveFilePath"), "InGameSaveFolder", "In-game saves", program, "", config);
                    Configured(settings, FirstKey(settings, "Instance0/SavestatePath", "SavestatePath"), "SaveStateFolder", "Save states", program, "", config);
                    Notice = "melonDS instance 0 paths are shown. Empty save paths store saves beside each ROM; no single hub folder applies.";
                    break;
                case "Cemu":
                    data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : File.Exists(P(program, "settings.xml")) ? program : Platform.IsWindows ? P(roaming, "Cemu") : DataRoot("Cemu");
                    configDir = Platform.IsWindows || data != DataRoot("Cemu") ? data : ConfigRoot("Cemu");
                    config = P(configDir, "settings.xml"); Add("ConfigFolder", "Configuration", configDir, "Cemu user directory");
                    var xml = new System.Xml.XmlDocument { XmlResolver = null }; string mlc = "";
                    if (File.Exists(config))
                    {
                        using (var reader = System.Xml.XmlReader.Create(new StringReader(ReadText(config)), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null })) xml.Load(reader);
                        var node = xml.SelectSingleNode("//mlc_path"); if (node != null) mlc = node.InnerText;
                    }
                    var mlcRoot = String.IsNullOrWhiteSpace(mlc) ? P(data, "mlc01") : Resolve(mlc, program);
                    Add("InGameSaveFolder", "In-game saves (Wii U)", P(mlcRoot, "usr", "save"), String.IsNullOrWhiteSpace(mlc) ? "Cemu default MLC" : "settings.xml - mlc_path");
                    Add("SaveStateFolder", "Save states", "", "No save-state folder detected for Cemu");
                    if (Directory.Exists(P(data, "screenshots"))) Add("ScreenshotFolder", "Screenshots", P(data, "screenshots"), "Existing Cemu folder");
                    Add("LogFolder", "Logs", data, "Cemu log.txt location");
                    break;
                case "Vita3K":
                    configDir = Platform.IsWindows ? program : FindConfigRoot(new[] { program, ConfigRoot("Vita3K"), P(DataRoot("Vita3K"), "Vita3K") }, new[] { "config.yml" }) ?? ConfigRoot("Vita3K");
                    config = P(configDir, "config.yml"); settings = ReadYaml(config);
                    Add("ConfigFolder", "Configuration", configDir, "config.yml");
                    var pref = Get(settings, "pref-path");
                    data = String.IsNullOrWhiteSpace(pref) ? (Platform.IsWindows ? P(roaming, "Vita3K", "Vita3K") : P(DataRoot("Vita3K"), "Vita3K")) : Resolve(pref, program);
                    var user = Get(settings, "user-id"); if (String.IsNullOrWhiteSpace(user)) user = "00";
                    var users = P(data, "ux0", "user");
                    var userDirs = Directory.Exists(users) ? Directory.GetDirectories(users).Take(64).ToArray() : new[] { P(users, user) };
                    foreach (var u in userDirs) Add("InGameSaveFolder", "In-game saves (Vita user " + System.IO.Path.GetFileName(u) + ")", P(u, "savedata"), "config.yml - pref-path");
                    if (Directory.Exists(P(data, "screenshots"))) Add("ScreenshotFolder", "Screenshots", P(data, "screenshots"), "Vita3K storage");
                    if (File.Exists(P(configDir, "vita3k.log"))) Add("LogFolder", "Logs", configDir, "vita3k.log");
                    Notice = "Vita3K's pref-path determines save storage. No save-state directory is assumed.";
                    break;
                case "PPSSPP":
                    var installed = P(program, "installed.txt");
                    var installedPath = File.Exists(installed) ? ReadText(installed).Trim().Trim('\uFEFF') : "";
                    data = File.Exists(installed) ? (String.IsNullOrWhiteSpace(installedPath) ? P(documents, "PPSSPP") : Resolve(installedPath, program)) : P(program, "memstick");
                    if (!Directory.Exists(data) && Directory.Exists(P(documents, "PPSSPP"))) data = P(documents, "PPSSPP");
                    if (!Platform.IsWindows && !File.Exists(installed) && !Directory.Exists(P(program, "memstick"))) data = ConfigRoot("ppsspp");
                    Add("ConfigFolder", "Configuration", P(data, "PSP", "SYSTEM"), "PPSSPP memory-stick storage");
                    Add("InGameSaveFolder", "In-game saves (PSP)", P(data, "PSP", "SAVEDATA"), "PPSSPP memory-stick storage");
                    Add("SaveStateFolder", "Save states", P(data, "PSP", "PPSSPP_STATE"), "PPSSPP memory-stick storage");
                    Add("ScreenshotFolder", "Screenshots", P(data, "PSP", "SCREENSHOT"), "PPSSPP memory-stick storage");
                    break;
                case "RetroArch":
                    data = FindConfigRoot(new[] { program, Platform.IsWindows ? P(roaming, "RetroArch") : ConfigRoot("retroarch") }, new[] { "retroarch.cfg" });
                    if (data == null) { Notice = "Open RetroArch once to create retroarch.cfg, then refresh folders."; break; }
                    config = P(data, "retroarch.cfg"); settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", data, "retroarch.cfg");
                    Configured(settings, "savefile_directory", "InGameSaveFolder", "In-game saves (base directory)", program, "", config);
                    Configured(settings, "savestate_directory", "SaveStateFolder", "Save states (base directory)", program, "", config);
                    Configured(settings, "screenshot_directory", "ScreenshotFolder", "Screenshots", program, "", config);
                    Configured(settings, "log_dir", "LogFolder", "Logs", program, "", config);
                    Notice = "RetroArch core/content overrides may use other folders. These are global paths; blank/default can depend on loaded content.";
                    break;
                case "RPCS3":
                    data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : Platform.IsWindows ? program : ConfigRoot("rpcs3");
                    var alternate = Environment.GetEnvironmentVariable("RPCS3_CONFIG_DIR");
                    if (!Directory.Exists(P(program, "portable")) && !String.IsNullOrWhiteSpace(alternate)) data = Resolve(alternate, program);
                    config = File.Exists(P(data, "config", "vfs.yml")) ? P(data, "config", "vfs.yml") : P(data, "vfs.yml");
                    settings = ReadYaml(config);
                    Add("ConfigFolder", "Configuration", Directory.Exists(P(data, "config")) ? P(data, "config") : data, "RPCS3 configuration");
                    var emuRoot = Get(settings, "$(EmulatorDir)");
                    emuRoot = String.IsNullOrWhiteSpace(emuRoot) ? data : Resolve(emuRoot, program);
                    var hdd = Get(settings, "/dev_hdd0/");
                    hdd = String.IsNullOrWhiteSpace(hdd) ? P(emuRoot, "dev_hdd0") : Resolve(hdd.Replace("$(EmulatorDir)", emuRoot.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar), program);
                    var homes = P(hdd, "home");
                    var psUsers = Directory.Exists(homes) ? Directory.GetDirectories(homes).Take(64).ToArray() : new[] { P(homes, "00000001") };
                    foreach (var u in psUsers) Add("InGameSaveFolder", "In-game saves (PS3 user " + System.IO.Path.GetFileName(u) + ")", P(u, "savedata"), "RPCS3 virtual HDD / vfs.yml");
                    Add("SaveStateFolder", "Save states", P(data, "savestates"), "RPCS3 configuration root");
                    Add("ScreenshotFolder", "Screenshots", P(data, "screenshots"), "RPCS3 configuration root");
                    Add("LogFolder", "Logs", Platform.IsWindows ? P(data, "log") : CacheRoot("rpcs3"), "RPCS3 log directory");
                    Notice = "RPCS3 users have separate in-game saves. Save-state support depends on the installed build and game.";
                    break;
                case "mGBA":
                    data = File.Exists(P(program, "portable.ini")) ? program : Platform.IsWindows ? P(roaming, "mGBA") : ConfigRoot("mgba");
                    if (File.Exists(P(program, "portable.ini")))
                    {
                        var portable = ReadSettings(P(program, "portable.ini"), false);
                        data = SettingPath(portable, "portable/path", program, program);
                    }
                    config = P(data, "config.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", data, "mGBA config.ini / qt.ini");
                    Configured(settings, FirstKey(settings, "ports/qt/savegamePath", "savegamePath"), "InGameSaveFolder", "In-game saves", data, "", config);
                    Configured(settings, FirstKey(settings, "ports/qt/savestatePath", "savestatePath"), "SaveStateFolder", "Save states", data, "", config);
                    Configured(settings, FirstKey(settings, "ports/qt/screenshotPath", "screenshotPath"), "ScreenshotFolder", "Screenshots", data, "", config);
                    Notice = "Empty mGBA save paths store files beside each ROM. Explicit paths are resolved relative to mGBA's configuration directory.";
                    break;
                case "MAME":
                    // Linux builds read $HOME/.mame (kept inside the sandbox for the Flatpak) before the program folder.
                    var mameHome = Platform.IsWindows || File.Exists(P(program, "mame.ini")) ? program : flatpak != null ? P(home, ".var", "app", flatpak, ".mame") : P(home, ".mame");
                    config = P(mameHome, "mame.ini"); settings = ReadMame(config);
                    var iniDirs = Get(settings, "inipath");
                    if (String.IsNullOrWhiteSpace(iniDirs)) iniDirs = ".;ini";
                    foreach (var iniDir in iniDirs.Split(';').Take(8))
                    {
                        var iniFile = P(Resolve(iniDir, mameHome), "mame.ini");
                        if (String.Equals(iniFile, config, StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var item in ReadMame(iniFile)) settings[item.Key] = item.Value;
                    }
                    Configured(settings, "cfg_directory", "ConfigFolder", "Configuration (system settings)", mameHome, "cfg", config);
                    Configured(settings, "nvram_directory", "InGameSaveFolder", "In-game saves (NVRAM)", mameHome, "nvram", config);
                    Configured(settings, "state_directory", "SaveStateFolder", "Save states", mameHome, "sta", config);
                    Configured(settings, "snapshot_directory", "ScreenshotFolder", "Screenshots", mameHome, "snap", config);
                    if (File.Exists(P(mameHome, "error.log"))) Add("LogFolder", "Logs", mameHome, "MAME error.log");
                    Notice = "MAME global directory settings are shown. System-specific INI overrides may change locations.";
                    break;
                case "Eden":
                    data = Directory.Exists(P(program, "user")) ? P(program, "user") : Platform.IsWindows ? P(roaming, "eden") : DataRoot("eden");
                    configDir = Directory.Exists(P(program, "user")) || Platform.IsWindows ? P(data, "config") : ConfigRoot("eden");
                    config = P(configDir, "qt-config.ini"); settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", configDir, "Eden user directory");
                    nand = SettingPath(settings, "Data Storage/nand_directory", program, P(data, "nand"));
                    var saveRoot = SettingPath(settings, "Data Storage/save_directory", program, nand);
                    Add("InGameSaveFolder", "In-game saves (Switch users)", P(saveRoot, "user", "save"), "Eden storage / qt-config.ini");
                    Add("InGameSaveFolder", "In-game saves (Switch system)", P(saveRoot, "system", "save"), "Eden storage / qt-config.ini");
                    Add("LogFolder", "Logs", P(data, "log"), "Eden user directory");
                    Notice = "Switch saves are separate from installed content. No save-state directory is assumed.";
                    break;
                default:
                    if (exe == "ryujinx" || flatpak == "io.github.ryubing.Ryujinx" || (!Platform.IsWindows && EmulatorCatalog.Compact(exe).StartsWith("ryujinx")))
                    {
                        data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : Platform.IsWindows ? P(roaming, "Ryujinx") : ConfigRoot("Ryujinx");
                        Add("ConfigFolder", "Configuration", data, "Ryujinx user directory");
                        Add("InGameSaveFolder", "In-game saves (Switch)", P(data, "bis", "user", "save"), "Ryujinx storage");
                        Add("LogFolder", "Logs", P(data, "Logs"), "Ryujinx user directory");
                    }
                    else if (String.IsNullOrWhiteSpace(Notice)) Notice = "Automatic detection is not available for this emulator yet. Choose its folders once; all emulators remain supported as launchers.";
                    break;
            }
        }
        private string DolphinRoot(out string configDir)
        {
            var root = DolphinUserRoot(out configDir);
            if (configDir == null) configDir = P(root, "Config");
            return root;
        }
        private string DolphinUserRoot(out string configDir)
        {
            configDir = null;
            if (File.Exists(P(program, "portable.txt"))) return P(program, "User");
            if (!Platform.IsWindows)
            {
                // Dolphin on Linux: $DOLPHIN_EMU_USERPATH, legacy ~/.dolphin-emu, else split XDG config/data folders.
                var userPath = Environment.GetEnvironmentVariable("DOLPHIN_EMU_USERPATH");
                if (flatpak == null && !String.IsNullOrWhiteSpace(userPath)) return Resolve(userPath, home);
                if (flatpak == null && Directory.Exists(P(home, ".dolphin-emu"))) return P(home, ".dolphin-emu");
                configDir = ConfigRoot("dolphin-emu"); return DataRoot("dolphin-emu");
            }
            if (readRegistry)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Dolphin Emulator"))
                        if (key != null)
                        {
                            if (Convert.ToString(key.GetValue("LocalUserConfig", "0")) != "0") return P(program, "User");
                            var custom = key.GetValue("UserConfigPath") as string;
                            if (!String.IsNullOrWhiteSpace(custom)) return Resolve(custom, program);
                        }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            return Directory.Exists(P(documents, "Dolphin Emulator")) ? P(documents, "Dolphin Emulator") : P(roaming, "Dolphin Emulator");
        }
        // Linux storage roots: a Flatpak keeps its XDG folders under ~/.var/app/<id>; native programs use the XDG homes.
        private string ConfigRoot(string name) { return flatpak == null ? P(configHome, name) : P(home, ".var", "app", flatpak, "config", name); }
        private string DataRoot(string name) { return flatpak == null ? P(dataHome, name) : P(home, ".var", "app", flatpak, "data", name); }
        private string CacheRoot(string name) { return flatpak == null ? P(cacheHome, name) : P(home, ".var", "app", flatpak, "cache", name); }
        private void Add(string property, string label, string value, string source)
        {
            if (folders.Any(f => f.Property == property && String.Equals(f.Path, value, Platform.PathComparison))) return;
            folders.Add(new EmulatorFolder(property, label, value, source));
        }
        private void Configured(Dictionary<string, string> settings, string key, string property, string label, string basis, string fallback, string file)
        {
            var value = Get(settings, key);
            if (String.IsNullOrWhiteSpace(value) || value.Equals("default", StringComparison.OrdinalIgnoreCase))
                Add(property, label, String.IsNullOrWhiteSpace(fallback) ? "" : P(basis, fallback), String.IsNullOrWhiteSpace(fallback) ? "Content dependent / not configured" : "Emulator default");
            else Add(property, label, Resolve(value, basis), System.IO.Path.GetFileName(file));
        }
        private static string SettingPath(Dictionary<string, string> settings, string key, string basis, string fallback)
        { var value = Get(settings, key); return String.IsNullOrWhiteSpace(value) || IsTrue(Get(settings, key + "/default")) ? fallback : Resolve(value, basis); }
        private static bool IsTrue(string value) { return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); }
        private static string Get(Dictionary<string, string> settings, string key) { string value; return settings.TryGetValue(key, out value) ? value : ""; }
        private static string FirstKey(Dictionary<string, string> settings, params string[] keys) { return keys.FirstOrDefault(k => settings.ContainsKey(k)) ?? keys[0]; }
        private static string P(params string[] parts) { return System.IO.Path.Combine(parts); }
        private static string Resolve(string value, string basis)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            if (!Platform.IsWindows) return ResolveUnix(value, basis);
            value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')).Replace('/', '\\');
            if (value.StartsWith(@":\")) value = value.Substring(2);
            if (value.Contains("%") || value.StartsWith("@")) return "";
            return System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(value) ? value : P(basis, value));
        }
        // Expands ~, $VAR and ${VAR}; ":/" (RetroArch) is relative to the program folder.
        private static string ResolveUnix(string value, string basis)
        {
            value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            value = System.Text.RegularExpressions.Regex.Replace(value, @"\$(\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|(?<name>[A-Za-z_][A-Za-z0-9_]*))", m =>
            {
                var name = m.Groups["name"].Value; var variable = name == "HOME" ? Platform.Home : Environment.GetEnvironmentVariable(name);
                return String.IsNullOrEmpty(variable) ? m.Value : variable;
            });
            if (value == "~") value = Platform.Home;
            else if (value.StartsWith("~/", StringComparison.Ordinal)) value = P(Platform.Home, value.Substring(2));
            else if (value.StartsWith(":/", StringComparison.Ordinal)) value = value.Substring(2);
            if (value.Contains("%") || value.Contains("$") || value.StartsWith("@", StringComparison.Ordinal)) return "";
            return System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(value) ? value : P(basis, value));
        }
        private static string FindConfigRoot(IEnumerable<string> roots, string[] names)
        { return roots.FirstOrDefault(r => names.Any(n => File.Exists(P(r, n)))); }
        private static string ReadText(string file)
        {
            if (!File.Exists(file)) return "";
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length > 4 * 1024 * 1024) throw new IOException("Configuration is too large to read safely.");
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }
        private static Dictionary<string, string> ReadSettings(string file, bool escapes)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); string section = "";
            foreach (var raw in ReadText(file).Split('\n'))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Trim('[', ']', '"', '\'').Replace('.', '/'); continue; }
                int equals = line.IndexOf('='); if (equals < 1) continue;
                var key = line.Substring(0, equals).Trim().Trim('"').Replace('\\', '/');
                var value = line.Substring(equals + 1).Trim(); bool literal = value.StartsWith("'");
                if (value.StartsWith("\"") || literal)
                {
                    char quote = value[0]; int end = value.LastIndexOf(quote);
                    if (end > 0) value = value.Substring(1, end - 1);
                }
                if (escapes && !literal) value = value.Replace(@"\\", @"\").Replace("\\\"", "\"");
                result[(section.Length == 0 ? "" : section + "/") + key] = value;
            }
            return result;
        }
        private static Dictionary<string, string> ReadMame(string file)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in ReadText(file).Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                int space = line.IndexOfAny(new[] { ' ', '\t' }); if (space < 1) continue;
                var value = line.Substring(space).Trim();
                if (value.StartsWith("\"")) { int end = value.LastIndexOf('"'); if (end > 0) value = value.Substring(1, end - 1); }
                else { int comment = value.IndexOf('#'); if (comment >= 0) value = value.Substring(0, comment).TrimEnd(); }
                result[line.Substring(0, space)] = value;
            }
            return result;
        }
        private static Dictionary<string, string> ReadYaml(string file)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in ReadText(file).Split('\n'))
            {
                if (raw.StartsWith(" ") || raw.StartsWith("\t")) continue;
                var line = raw.Trim(); int colon = line.IndexOf(':'); if (colon < 1 || line.StartsWith("#")) continue;
                var value = line.Substring(colon + 1).Trim();
                if (value.StartsWith("\""))
                { string parsed; if (!Json.TryParseString(value, out parsed)) continue; value = parsed; }
                else if (value.StartsWith("'") && value.EndsWith("'")) value = value.Substring(1, value.Length - 2).Replace("''", "'");
                else { int comment = value.IndexOf(" #"); if (comment >= 0) value = value.Substring(0, comment).TrimEnd(); }
                result[line.Substring(0, colon).Trim()] = value;
            }
            return result;
        }
    }


    public static class HubPaths
    {
        public static string EmulatorRoot(LibraryData library)
        { return String.IsNullOrWhiteSpace(library.EmulatorRootDirectory) ? Path.Combine(Platform.Documents, "FishBowl", "Emulators") : Path.GetFullPath(library.EmulatorRootDirectory); }
        public static string BackupRoot(LibraryData library)
        { return String.IsNullOrWhiteSpace(library.BackupFolder) ? Path.Combine(Platform.Documents, "FishBowl", "Backups") : Path.GetFullPath(library.BackupFolder); }
        public static bool Same(string a, string b)
        { try { return String.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), Platform.PathComparison); } catch { return false; } }
        public static bool Inside(string root, string child)
        { return Path.GetFullPath(child).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, Platform.PathComparison); }
        public static string SafeName(string text)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((text ?? "").Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.', ' ');
            if (String.IsNullOrEmpty(name)) name = "Emulator";
            if (name.Length > 80) name = name.Substring(0, 80);
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) name = "_" + name;
            return name;
        }
        public static string SafeChild(string root, string relative)
        {
            relative = (relative ?? "").Replace('/', '\\');
            if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.StartsWith("\\") || relative.Contains(':')) throw new InvalidDataException("An archive contains an invalid path.");
            foreach (var part in relative.Split('\\'))
            {
                if (part == "" || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ") ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new InvalidDataException("An archive contains an unsafe filename.");
            }
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar)));
            if (!Inside(root, full)) throw new InvalidDataException("An archive entry escapes its destination.");
            return full;
        }
        public static bool IsLink(string path)
        { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        public static void CheckParents(string root, string target)
        {
            if (!Same(root, target) && !Inside(root, target)) throw new IOException("Destination is outside the selected folder.");
            for (var dir = Directory.Exists(target) ? target : Path.GetDirectoryName(target); !String.IsNullOrWhiteSpace(dir); dir = Path.GetDirectoryName(dir))
            {
                if (Directory.Exists(dir) && IsLink(dir)) throw new IOException("Folder links are not supported for this operation.");
                if (Same(dir, root)) break;
            }
        }
        public static void DeleteStage(string parent, string stage)
        {
            if (!Inside(parent, stage) || !Path.GetFileName(stage).StartsWith(".FishBowl-", StringComparison.Ordinal)) throw new IOException("Invalid temporary directory.");
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }
    public static class BuildRegistry
    {
        public static void Remember(EmulatorProfile profile, string executable, string label)
        {
            if (profile.Builds == null) profile.Builds = new List<EmulatorBuild>();
            var existing = profile.Builds.FirstOrDefault(b => HubPaths.Same(b.Executable, executable));
            if (existing != null) { if (HubPaths.Same(profile.Executable, executable)) existing.ManualVersion = profile.ManualVersion; return; }
            profile.Builds.Add(new EmulatorBuild { Id = Guid.NewGuid().ToString("N"), Label = String.IsNullOrWhiteSpace(label) ? Path.GetFileName(Path.GetDirectoryName(executable)) : label,
                Executable = Path.GetFullPath(executable), ManualVersion = HubPaths.Same(executable, profile.Executable) ? profile.ManualVersion : "", AddedAt = DateTime.UtcNow.ToString("o") });
        }
        public static void Activate(EmulatorProfile profile, EmulatorBuild build)
        {
            if (!File.Exists(build.Executable) || !EmulatorReference.IsLaunchFile(build.Executable)) throw new IOException("This build's executable is unavailable.");
            Remember(profile, profile.Executable, "Previous installation");
            profile.Executable = Path.GetFullPath(build.Executable); profile.ManualVersion = build.ManualVersion;
        }
        public static void Repair(EmulatorProfile profile, string executable)
        {
            if (!File.Exists(executable) || !EmulatorReference.IsLaunchFile(executable)) throw new IOException("Choose an existing " + Platform.OsName + " emulator program or launcher.");
            Remember(profile, profile.Executable, "Previous location");
            profile.Executable = Path.GetFullPath(executable); profile.ManualVersion = "";
            Remember(profile, profile.Executable, "Repaired location");
        }
    }
    public class DiscoveredEmulator
    {
        public string Executable, Preset, Name;
        public override string ToString() { return Name + " — " + Executable; }
    }
    public class DiscoveryResult
    {
        public List<DiscoveredEmulator> Items = new List<DiscoveredEmulator>();
        public List<string> Warnings = new List<string>();
    }
    public static class EmulatorDiscovery
    {
        public static readonly string[] SkippedDirectories = { "games", "roms", "rom", "nand", "sdmc", "mlc01", "dev_hdd0", "dev_flash", "ux0", "cache", "saves", "savestates", "states", "backups", "fishbowldata", ".git" };
        public static EmulatorPreset Recognize(string file)
        {
            var preset = EmulatorCatalog.ForExecutable(file); var name = Path.GetFileName(file).ToLowerInvariant();
            if (name.StartsWith("desmume") && name.EndsWith(".exe")) return EmulatorCatalog.Find("DeSmuME");
            if (name.StartsWith("pcsx2") && name.EndsWith(".exe")) return EmulatorCatalog.Find("PCSX2");
            // azahar.exe is shared with regular Azahar; the user chooses Plus explicitly.
            return preset;
        }
        public static DiscoveryResult Scan(string root, bool includeUnknown, System.Threading.CancellationToken token)
        {
            var result = new DiscoveryResult(); root = Path.GetFullPath(root);
            if (!Directory.Exists(root)) { result.Warnings.Add("The emulator folder does not exist yet."); return result; }
            if (HubPaths.Same(root, Path.GetPathRoot(root))) throw new IOException("Choose an emulator folder rather than a whole drive.");
            var queue = new Queue<Tuple<string, int>>(); queue.Enqueue(Tuple.Create(root, 0)); int visited = 0;
            while (queue.Count > 0)
            {
                token.ThrowIfCancellationRequested(); var item = queue.Dequeue();
                if (++visited > 1500) { result.Warnings.Add("Folder scan stopped at its directory limit. Choose a smaller folder to continue."); break; }
                try
                {
                    if (HubPaths.IsLink(item.Item1)) { result.Warnings.Add("Skipped folder link: " + item.Item1); continue; }
                    foreach (var file in (Platform.IsWindows ? Directory.EnumerateFiles(item.Item1, "*.exe") : Directory.EnumerateFiles(item.Item1).Where(Platform.IsLaunchFile)).Take(300))
                    {
                        if (HubPaths.IsLink(file)) continue;
                        var preset = Recognize(file); if (preset.Name == "Custom" && !includeUnknown) continue;
                        result.Items.Add(new DiscoveredEmulator { Name = preset.Name == "Custom" ? Path.GetFileNameWithoutExtension(file) : preset.Name, Preset = preset.Label, Executable = Path.GetFullPath(file) });
                    }
                    if (item.Item2 < 4)
                        foreach (var dir in Directory.EnumerateDirectories(item.Item1).Take(300))
                            if (!SkippedDirectories.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase) && !Path.GetFileName(dir).StartsWith(".FishBowl-", StringComparison.OrdinalIgnoreCase))
                                queue.Enqueue(Tuple.Create(dir, item.Item2 + 1));
                }
                catch (IOException ex) { result.Warnings.Add(ex.Message); }
                catch (UnauthorizedAccessException ex) { result.Warnings.Add(ex.Message); }
            }
            result.Items = result.Items.OrderBy(p => p.Name).ThenBy(p => p.Executable).ToList(); return result;
        }
        // Linux: recognized emulators installed as Flatpaks, on PATH, or as AppImages in the usual download folders.
        public static DiscoveryResult ScanSystem(System.Threading.CancellationToken token)
        {
            var result = new DiscoveryResult(); if (Platform.IsWindows) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Action<string> consider = delegate(string file)
            {
                token.ThrowIfCancellationRequested();
                var preset = Recognize(file); if (preset.Name == "Custom") return;
                if (!seen.Add(Platform.FlatpakId(file) ?? Platform.RealPath(file))) return;
                result.Items.Add(new DiscoveredEmulator { Name = preset.Name, Preset = preset.Label, Executable = file });
            };
            foreach (var installation in Platform.FlatpakInstallations)
            {
                var exports = Path.Combine(installation, "exports", "bin");
                try { if (Directory.Exists(exports)) foreach (var file in Directory.EnumerateFiles(exports)) consider(file); }
                catch (IOException ex) { result.Warnings.Add(ex.Message); } catch (UnauthorizedAccessException ex) { result.Warnings.Add(ex.Message); }
            }
            var names = EmulatorCatalog.Presets.SelectMany(p => p.UnixExecutables).Distinct().ToArray();
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(d => d.Length > 0).Distinct())
                foreach (var name in names)
                {
                    var file = Path.Combine(directory, name);
                    if (Platform.IsExecutableFile(file)) consider(file);
                }
            foreach (var folder in new[] { "Applications", "AppImages", "Downloads", Path.Combine(".local", "bin") })
            {
                var directory = Path.Combine(Platform.Home, folder);
                try { if (Directory.Exists(directory)) foreach (var file in Directory.EnumerateFiles(directory).Where(f => f.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) || Platform.IsExecutableFile(f)).Take(500)) consider(file); }
                catch (IOException ex) { result.Warnings.Add(ex.Message); } catch (UnauthorizedAccessException ex) { result.Warnings.Add(ex.Message); }
            }
            result.Items = result.Items.OrderBy(p => p.Name).ThenBy(p => p.Executable).ToList(); return result;
        }
    }
    public static class EmulatorInstaller
    {
        public static void ValidateArchive(System.IO.Compression.ZipArchive archive, string destination, bool requireExecutable = true)
        {
            if (archive.Entries.Count > 50000) throw new InvalidDataException("This archive contains too many entries.");
            long size = 0; var names = new HashSet<string>(Platform.PathComparer);
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName.TrimEnd('/', '\\'); if (relative.Length == 0) continue;
                var full = HubPaths.SafeChild(destination, relative);
                if (!names.Add(full)) throw new InvalidDataException("This archive contains duplicate paths.");
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Archive links are not supported.");
                if (entry.Length > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("An archive entry is too large.");
                checked { size += entry.Length; }
                if (size > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("The extracted archive exceeds 4 GB.");
            }
            if (requireExecutable && !archive.Entries.Any(e => Platform.ArchiveEntryIsExecutable(e.Name, e.ExternalAttributes))) throw new InvalidDataException("This ZIP contains no " + Platform.OsName + " emulator executable.");
        }
        public static string InstallZip(string zipFile, string root, string label, System.Threading.CancellationToken token)
        {
            root = Path.GetFullPath(root); Directory.CreateDirectory(root);
            if (HubPaths.IsLink(root)) throw new IOException("Choose a regular directory for emulator installation.");
            var stage = Path.Combine(root, ".FishBowl-install-" + Guid.NewGuid().ToString("N"));
            string target = Path.Combine(root, HubPaths.SafeName(label)); int suffix = 2;
            while (Directory.Exists(target) || File.Exists(target)) target = Path.Combine(root, HubPaths.SafeName(label) + " (" + suffix++ + ")");
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFile))
                {
                    ValidateArchive(archive, stage); token.ThrowIfCancellationRequested(); Directory.CreateDirectory(stage);
                    foreach (var entry in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested(); var relative = entry.FullName.TrimEnd('/', '\\'); if (relative.Length == 0) continue;
                        var destination = HubPaths.SafeChild(stage, relative);
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (var from = entry.Open()) using (var to = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                            Copy(from, to, entry.Length, token);
                        Platform.ApplyArchiveMode(destination, entry.ExternalAttributes);
                    }
                }
                token.ThrowIfCancellationRequested(); Directory.Move(stage, target); return target;
            }
            finally { HubPaths.DeleteStage(root, stage); }
        }
        // Linux: copies an AppImage into its own folder under the emulator root and marks it executable.
        public static string InstallAppImage(string appImage, string root, string label, System.Threading.CancellationToken token)
        {
            root = Path.GetFullPath(root); Directory.CreateDirectory(root);
            if (HubPaths.IsLink(root)) throw new IOException("Choose a regular directory for emulator installation.");
            string target = Path.Combine(root, HubPaths.SafeName(label)); int suffix = 2;
            while (Directory.Exists(target) || File.Exists(target)) target = Path.Combine(root, HubPaths.SafeName(label) + " (" + suffix++ + ")");
            var stage = Path.Combine(root, ".FishBowl-install-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(stage);
                var length = new FileInfo(appImage).Length;
                using (var from = new FileStream(appImage, FileMode.Open, FileAccess.Read, FileShare.Read)) using (var to = new FileStream(Path.Combine(stage, Path.GetFileName(appImage)), FileMode.CreateNew, FileAccess.Write))
                    Copy(from, to, length, token);
                Platform.MakeExecutable(Path.Combine(stage, Path.GetFileName(appImage)));
                token.ThrowIfCancellationRequested(); Directory.Move(stage, target); return Path.Combine(target, Path.GetFileName(appImage));
            }
            finally { HubPaths.DeleteStage(root, stage); }
        }
        public static void Copy(Stream source, Stream destination, long limit, System.Threading.CancellationToken token)
        {
            var buffer = new byte[81920]; long copied = 0; int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
            { token.ThrowIfCancellationRequested(); copied += count; if (copied > limit) throw new InvalidDataException("Archive data exceeds its declared size."); destination.Write(buffer, 0, count); }
            if (copied != limit) throw new InvalidDataException("Archive data has an unexpected size.");
        }
    }
    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string html_url { get; set; }
        public string body { get; set; }
        public string published_at { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
    }
    public class UpdateResult
    {
        public string Status, Installed, Latest, Url, Notes, CheckedAt;
        public bool HasFeed;
    }
    public static class EmulatorUpdates
    {
        public static string Repository(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.GitHubRepository)) return ValidRepo(profile.GitHubRepository.Trim()) ? profile.GitHubRepository.Trim() : "";
            foreach (var url in new[] { EmulatorReference.For(profile).Releases, EmulatorCatalog.Find(profile.Preset).Website })
            {
                Uri uri; if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = uri.AbsolutePath.Trim('/').Split('/'); if (parts.Length >= 2 && ValidRepo(parts[0] + "/" + parts[1])) return parts[0] + "/" + parts[1];
                }
            }
            var name = EmulatorCatalog.Find(profile.Preset).Name;
            var extras = new Dictionary<string, string> { { "PCSX2", "PCSX2/pcsx2" }, { "Cemu", "cemu-project/Cemu" }, { "Dolphin", "dolphin-emu/dolphin" }, { "mGBA", "mgba-emu/mgba" } };
            return extras.ContainsKey(name) ? extras[name] : "";
        }
        public static bool ValidRepo(string value)
        { return System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") && !value.Contains(".."); }
        public static string ReadOfficialFeed(string url)
        {
            var uri = new Uri(url); if (uri.Scheme != "https" || uri.Host != "api.github.com") throw new IOException("Use an official GitHub release feed.");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(uri); request.UserAgent = "FishBowl/1.0"; request.Accept = "application/vnd.github+json"; request.Timeout = 12000; request.ReadWriteTimeout = 12000;
            using (var response = (HttpWebResponse)request.GetResponse()) using (var stream = response.GetResponseStream()) using (var output = new MemoryStream())
            {
                var buffer = new byte[8192]; int count; while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) { if (output.Length + count > 2 * 1024 * 1024) throw new IOException("The release feed is too large."); output.Write(buffer, 0, count); }
                return System.Text.Encoding.UTF8.GetString(output.ToArray());
            }
        }
        public static UpdateResult Check(EmulatorProfile profile, Func<string, string> fetch = null)
        {
            var result = new UpdateResult { Installed = EmulatorReference.VersionFor(profile), CheckedAt = DateTime.UtcNow.ToString("o") };
            var repo = Repository(profile);
            if (String.IsNullOrWhiteSpace(repo)) { result.Status = "Automatic release comparison is unavailable. Use the official download page, or configure its GitHub repository."; result.Url = EmulatorReference.For(profile).Releases; return result; }
            fetch = fetch ?? ReadOfficialFeed;
            GitHubRelease release;
            if (profile.IncludePreviewReleases)
                release = Json.Deserialize<List<GitHubRelease>>(fetch("https://api.github.com/repos/" + repo + "/releases?per_page=10"), 2 * 1024 * 1024).Where(r => !r.draft).OrderByDescending(r => r.published_at).FirstOrDefault();
            else release = Json.Deserialize<GitHubRelease>(fetch("https://api.github.com/repos/" + repo + "/releases/latest"), 2 * 1024 * 1024);
            if (release == null || release.draft || (!profile.IncludePreviewReleases && release.prerelease) || String.IsNullOrWhiteSpace(release.tag_name)) throw new IOException("The project has no matching published release.");
            Uri uri; if (!Uri.TryCreate(release.html_url, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "github.com") throw new InvalidDataException("The release link is invalid.");
            result.HasFeed = true; result.Latest = release.tag_name; result.Url = release.html_url; result.Notes = release.body ?? ""; result.Status = Compare(result.Installed, String.IsNullOrWhiteSpace(release.name) ? release.tag_name : release.name);
            return result;
        }
        public static string Compare(string installed, string latest)
        {
            var a = VersionParts(installed); var b = VersionParts(latest);
            if (a == null || b == null) return "Latest release found; compare versions manually.";
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int left = i < a.Length ? a[i] : 0, right = i < b.Length ? b[i] : 0;
                if (left < right) return "Newer release available.";
                if (left > right) return "Installed version is newer than this release.";
            }
            // An exact numeric match does not establish equality of preview/build suffixes.
            if (!String.Equals(NormalizeVersion(installed), NormalizeVersion(latest), StringComparison.OrdinalIgnoreCase)) return "Version numbers match; compare build labels manually.";
            return "Installed version matches the latest release.";
        }
        private static string NormalizeVersion(string text)
        {
            text = (text ?? "").Replace("(entered manually)", "").Trim();
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+(?:[._]\d+)*(?:[-+._][A-Za-z][A-Za-z0-9._-]*)?");
            if (!match.Success) return "";
            var normalized = match.Value.Replace('_', '.');
            return System.Text.RegularExpressions.Regex.Replace(normalized, @"(?<=\d)(\.0)+$", "");
        }
        private static int[] VersionParts(string text)
        {
            var normalized = NormalizeVersion(text);
            var m = System.Text.RegularExpressions.Regex.Match(normalized, @"^\d+(?:\.\d+)*");
            if (!m.Success) return null;
            var values = new List<int>(); foreach (var part in m.Value.Split('.')) { int value; if (!Int32.TryParse(part, out value)) return null; values.Add(value); }
            return values.All(v => v == 0) ? null : values.ToArray();
        }
    }
    public enum RuntimeState { Stopped, Running, Unknown }
    public static class EmulatorRuntime
    {
        public static RuntimeState State(string executable) { return Platform.State(executable); }
        public static bool BringForward(string executable) { return Platform.BringForward(executable); }
    }


    public class BackupRoot
    {
        public string Key { get; set; }
        public string Category { get; set; }
        public string Label { get; set; }
        public string Source { get; set; }
    }
    public class BackupFile
    {
        public string RootKey { get; set; }
        public string Relative { get; set; }
        public string ArchivePath { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; }
    }
    public class BackupManifest
    {
        public int Format { get; set; }
        public string EmulatorId { get; set; }
        public string EmulatorName { get; set; }
        public string CreatedAt { get; set; }
        public List<BackupRoot> Roots { get; set; }
        public List<BackupFile> Files { get; set; }
    }
    public class BackupPlan
    {
        public List<BackupRoot> Roots = new List<BackupRoot>();
        public List<BackupFile> Files = new List<BackupFile>();
        public List<string> Warnings = new List<string>();
        public long Bytes { get { return Files.Sum(f => f.Size); } }
        public string Summary()
        {
            var text = new System.Text.StringBuilder();
            foreach (var root in Roots)
            {
                var files = Files.Where(f => f.RootKey == root.Key).ToList();
                text.AppendLine(root.Label + " — " + files.Count + " files").AppendLine(root.Source).AppendLine();
            }
            text.AppendLine("Total: " + Files.Count + " files / " + (Bytes / 1024.0 / 1024.0).ToString("0.0") + " MB");
            foreach (var warning in Warnings) text.AppendLine(warning);
            text.AppendLine().AppendLine("Included files:");
            foreach (var file in Files.Take(500)) text.AppendLine(file.ArchivePath);
            if (Files.Count > 500) text.AppendLine("Preview shows the first 500 files. The archive manifest lists every included file.");
            return text.ToString();
        }
    }
    public static class EmulatorBackups
    {
        public static readonly string[] Categories = { "ConfigFolder", "InGameSaveFolder", "SaveStateFolder" };
        private static readonly string[] ConfigExtensions = { ".ini", ".cfg", ".conf", ".json", ".xml", ".toml", ".yaml", ".yml", ".bml", ".opt", ".rmp" };
        private static readonly string[] ExcludedExtensions = { ".exe", ".dll", ".zip", ".7z", ".rar", ".iso", ".cso", ".chd", ".gcm", ".wbfs", ".wud", ".wux", ".nsp", ".xci", ".nds", ".3ds", ".cia", ".cci", ".app", ".ncch", ".gba", ".gb", ".gbc", ".nes", ".sfc", ".smc", ".z64", ".v64", ".n64", ".md", ".gen", ".cue", ".img", ".pbp", ".pkg", ".vpk", ".elf", ".dol", ".wad", ".rom" };
        private static readonly string[] Skipped = EmulatorDiscovery.SkippedDirectories.Concat(new[] { "content", "contents", "registered", "bios", "firmware", "screenshots", "logs", "log", "stateslots", "battery", "memcards" }).ToArray();
        public static bool IsContainer(EmulatorFolder folder)
        { return folder.Label.Contains("3DS SD storage") || folder.Label.Contains("3DS NAND data") || folder.Label.Contains("Wii titles"); }
        public static List<BackupRoot> Roots(EmulatorProfile profile, IEnumerable<string> categories, System.Threading.CancellationToken token)
        {
            var roots = new List<BackupRoot>();
            foreach (var folder in new EmulatorFolderDetector().Detect(profile).Where(f => categories.Contains(f.Property) && !String.IsNullOrWhiteSpace(f.Path)))
            {
                if (IsContainer(folder))
                {
                    foreach (var leaf in SaveLeaves(folder.Path, token))
                        roots.Add(new BackupRoot { Category = folder.Property, Label = folder.Label + " / " + leaf.Substring(folder.Path.TrimEnd('\\', '/').Length).TrimStart('\\', '/'), Source = leaf });
                }
                else roots.Add(new BackupRoot { Category = folder.Property, Label = folder.Label, Source = Path.GetFullPath(folder.Path) });
            }
            roots = roots.GroupBy(r => r.Category + "|" + r.Source, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            for (int i = 0; i < roots.Count; i++) roots[i].Key = "root-" + i;
            return roots;
        }
        private static IEnumerable<string> SaveLeaves(string root, System.Threading.CancellationToken token)
        {
            var result = new List<string>(); if (!Directory.Exists(root)) return result;
            var queue = new Queue<Tuple<string, int>>(); queue.Enqueue(Tuple.Create(root, 0)); int visited = 0;
            while (queue.Count > 0)
            {
                token.ThrowIfCancellationRequested(); if (++visited > 20000) throw new IOException("Save container is too large to inspect.");
                var current = queue.Dequeue(); if (HubPaths.IsLink(current.Item1)) continue;
                if (current.Item2 > 0 && new[] { "data", "extdata" }.Contains(Path.GetFileName(current.Item1), StringComparer.OrdinalIgnoreCase))
                { result.Add(current.Item1); continue; }
                if (current.Item2 < 8)
                    foreach (var dir in Directory.GetDirectories(current.Item1))
                        if (!new[] { "content", "contents", "registered" }.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase)) queue.Enqueue(Tuple.Create(dir, current.Item2 + 1));
            }
            return result;
        }
        public static bool AllowedFile(BackupRoot root, string relative)
        {
            var extension = Path.GetExtension(relative).ToLowerInvariant(); var name = Path.GetFileName(relative);
            var segments = relative.Replace('/', '\\').Split('\\');
            if (segments.Take(Math.Max(0, segments.Length - 1)).Any(s => new[] { "games", "roms", "rom", "content", "contents", "registered", "bios", "firmware", "cache", "backups", ".git" }.Contains(s, StringComparer.OrdinalIgnoreCase) || s.StartsWith(".FishBowl-", StringComparison.OrdinalIgnoreCase))) return false;
            if (ExcludedExtensions.Contains(extension) || name.Equals("IPL.bin", StringComparison.OrdinalIgnoreCase) || name.StartsWith("scph", StringComparison.OrdinalIgnoreCase) && extension == ".bin") return false;
            if (root.Category == "ConfigFolder") return ConfigExtensions.Contains(extension);
            if (root.Category != "InGameSaveFolder" && root.Category != "SaveStateFolder") return false;
            // A generic user-selected folder must not silently back up ambiguous ROM .bin files.
            if (extension == ".bin")
            {
                var parts = (root.Source + "\\" + relative).Split('\\', '/');
                if (!parts.Any(p => new[] { "save", "saves", "savedata", "data", "extdata", "battery", "memcards", "states", "sstates", "savestates", "ppsspp_state", "sta" }.Contains(p, StringComparer.OrdinalIgnoreCase))) return false;
            }
            return true;
        }
        public static BackupPlan Preview(EmulatorProfile profile, IEnumerable<string> categories, System.Threading.CancellationToken token)
        {
            var plan = new BackupPlan(); plan.Roots = Roots(profile, categories, token);
            int visited = 0; long total = 0;
            foreach (var root in plan.Roots)
            {
                if (!Directory.Exists(root.Source)) { plan.Warnings.Add("Missing folder: " + root.Source); continue; }
                HubPaths.CheckParents(root.Source, root.Source);
                var stack = new Stack<string>(); stack.Push(root.Source);
                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested(); if (++visited > 25000) throw new IOException("Backup scan is too large. Choose narrower folder overrides.");
                    var dir = stack.Pop(); if (HubPaths.IsLink(dir)) { plan.Warnings.Add("Skipped folder link: " + dir); continue; }
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        if (HubPaths.IsLink(file)) continue;
                        var relative = file.Substring(root.Source.TrimEnd('\\', '/').Length).TrimStart('\\', '/');
                        if (!AllowedFile(root, relative)) continue;
                        long size = new FileInfo(file).Length; checked { total += size; }
                        if (size > 2L * 1024 * 1024 * 1024 || total > 4L * 1024 * 1024 * 1024 || plan.Files.Count >= 50000) throw new IOException("Backup exceeds the 4 GB / 50,000 file limit.");
                        plan.Files.Add(new BackupFile { RootKey = root.Key, Relative = relative, ArchivePath = "data/" + root.Key + "/" + relative.Replace('\\', '/'), Size = size });
                    }
                    foreach (var child in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(child);
                        bool skip = EmulatorDiscovery.SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase) || name.Equals("content", StringComparison.OrdinalIgnoreCase) || name.Equals("contents", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".FishBowl-", StringComparison.OrdinalIgnoreCase);
                        if (root.Category == "ConfigFolder") skip |= Skipped.Contains(name, StringComparer.OrdinalIgnoreCase);
                        if (!skip) stack.Push(child);
                    }
                }
            }
            if (new EmulatorFolderDetector().Detect(profile).Any(f => IsContainer(f) && categories.Contains(f.Property)))
                plan.Warnings.Add("SD/NAND/title storage is narrowed to data/extdata save folders. Installed content is excluded.");
            plan.Warnings.Add("ROM/executable/archive formats and folder links are excluded. Review manual save folders; ambiguous .bin files outside recognized save directories are excluded.");
            return plan;
        }
        private static string CopyHash(Stream source, Stream target, long limit, System.Threading.CancellationToken token)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] buffer = new byte[81920]; int count; long copied = 0;
                while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                { token.ThrowIfCancellationRequested(); copied += count; if (copied > limit) throw new IOException("A source file changed during backup."); hash.TransformBlock(buffer, 0, count, buffer, 0); target.Write(buffer, 0, count); }
                if (copied != limit) throw new IOException("A source file changed during backup.");
                hash.TransformFinalBlock(new byte[0], 0, 0); return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        public static string Create(EmulatorProfile profile, BackupPlan plan, string backupDirectory, System.Threading.CancellationToken token, string prefix = "Backup")
        {
            if (plan.Files.Count == 0) throw new IOException("No eligible files were found for the selected backup categories.");
            backupDirectory = Path.GetFullPath(backupDirectory);
            foreach (var root in plan.Roots) if (HubPaths.Same(root.Source, backupDirectory) || HubPaths.Inside(root.Source, backupDirectory)) throw new IOException("Store backups outside the folders being backed up.");
            Directory.CreateDirectory(backupDirectory); HubPaths.CheckParents(backupDirectory, backupDirectory);
            string target = Path.Combine(backupDirectory, HubPaths.SafeName(profile.Name) + " - " + prefix + " - " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".zip");
            string temp = target + ".partial";
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew)) using (var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var file in plan.Files)
                    {
                        token.ThrowIfCancellationRequested(); var root = plan.Roots.Single(r => r.Key == file.RootKey);
                        var source = HubPaths.SafeChild(root.Source, file.Relative); HubPaths.CheckParents(root.Source, source);
                        if (!AllowedFile(root, file.Relative) || HubPaths.IsLink(source)) throw new IOException("A backup source no longer matches its preview.");
                        using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var to = archive.CreateEntry(file.ArchivePath, System.IO.Compression.CompressionLevel.Optimal).Open()) file.Sha256 = CopyHash(from, to, file.Size, token);
                    }
                    var manifest = new BackupManifest { Format = 1, EmulatorId = profile.Id, EmulatorName = profile.Name, CreatedAt = DateTime.UtcNow.ToString("o"), Roots = plan.Roots, Files = plan.Files };
                    using (var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open(), new System.Text.UTF8Encoding(false))) writer.Write(Json.Serialize(manifest, 8 * 1024 * 1024));
                }
                token.ThrowIfCancellationRequested(); File.Move(temp, target); return target;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static BackupManifest ReadManifest(System.IO.Compression.ZipArchive archive)
        {
            var entry = archive.GetEntry("manifest.json");
            if (entry == null || entry.Length > 8 * 1024 * 1024) throw new InvalidDataException("This is not a supported FishBowl backup.");
            BackupManifest manifest; using (var reader = new StreamReader(entry.Open())) manifest = Json.Deserialize<BackupManifest>(reader.ReadToEnd(), 8 * 1024 * 1024);
            if (manifest == null || manifest.Format != 1 || manifest.Roots == null || manifest.Files == null || manifest.Files.Count > 50000 || manifest.Roots.Count > 25000) throw new InvalidDataException("Backup manifest is invalid.");
            return manifest;
        }
        public static void ValidateRestore(EmulatorProfile profile, BackupManifest manifest)
        {
            if (manifest.EmulatorId != profile.Id) throw new InvalidDataException("This backup belongs to another FishBowl emulator profile.");
            if (manifest.Roots.Select(r => r.Key).Distinct().Count() != manifest.Roots.Count) throw new InvalidDataException("Backup roots are duplicated.");
            var routes = new EmulatorFolderDetector().Detect(profile);
            foreach (var root in manifest.Roots)
            {
                if (!Categories.Contains(root.Category) || String.IsNullOrWhiteSpace(root.Source)) throw new InvalidDataException("Invalid backup destination.");
                var source = Path.GetFullPath(root.Source);
                bool matched = routes.Any(f => f.Property == root.Category && !String.IsNullOrWhiteSpace(f.Path) &&
                    (HubPaths.Same(f.Path, source) && !IsContainer(f) ||
                    IsContainer(f) && HubPaths.Inside(f.Path, source) && new[] { "data", "extdata" }.Contains(Path.GetFileName(source.TrimEnd('\\', '/')), StringComparer.OrdinalIgnoreCase)));
                if (!matched) throw new InvalidDataException("A backup folder no longer matches this emulator's detected/manual paths. Select the original build or folder before restoring:\n" + source);
                HubPaths.CheckParents(source, source);
            }
            var targets = new HashSet<string>(Platform.PathComparer);
            foreach (var file in manifest.Files)
            {
                var root = manifest.Roots.SingleOrDefault(r => r.Key == file.RootKey); if (root == null) throw new InvalidDataException("Unknown backup root.");
                var destination = HubPaths.SafeChild(root.Source, file.Relative);
                if (!targets.Add(destination) || !AllowedFile(root, file.Relative) || file.ArchivePath != "data/" + root.Key + "/" + file.Relative.Replace('\\', '/') || file.Size < 0) throw new InvalidDataException("Backup contains an invalid file.");
                if (String.IsNullOrWhiteSpace(file.Sha256) || !System.Text.RegularExpressions.Regex.IsMatch(file.Sha256, "^[0-9a-f]{64}$")) throw new InvalidDataException("Backup checksum is invalid.");
                HubPaths.CheckParents(root.Source, destination);
                if (File.Exists(destination) && HubPaths.IsLink(destination)) throw new IOException("Restore destination is a file link.");
            }
        }
        public static string Restore(EmulatorProfile profile, string backupFile, string backupDirectory, System.Threading.CancellationToken token)
        {
            backupDirectory = Path.GetFullPath(backupDirectory); Directory.CreateDirectory(backupDirectory);
            var stage = Path.Combine(backupDirectory, ".FishBowl-restore-" + Guid.NewGuid().ToString("N")); string before = "";
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(backupFile))
                {
                    EmulatorInstaller.ValidateArchive(archive, stage, false); var manifest = ReadManifest(archive); ValidateRestore(profile, manifest); Directory.CreateDirectory(stage);
                    foreach (var file in manifest.Files)
                    {
                        token.ThrowIfCancellationRequested(); var entry = archive.GetEntry(file.ArchivePath);
                        if (entry == null || entry.Length != file.Size) throw new InvalidDataException("Backup file is missing or has a different size.");
                        var staged = HubPaths.SafeChild(stage, file.ArchivePath); Directory.CreateDirectory(Path.GetDirectoryName(staged));
                        using (var from = entry.Open()) using (var to = new FileStream(staged, FileMode.CreateNew))
                            if (CopyHash(from, to, file.Size, token) != file.Sha256) throw new InvalidDataException("Backup checksum failed. No files have been restored.");
                    }
                    var preimage = Preview(profile, manifest.Roots.Select(r => r.Category).Distinct().ToArray(), token);
                    if (preimage.Files.Count > 0) before = Create(profile, preimage, backupDirectory, token, "Before restore");
                    foreach (var file in manifest.Files)
                    {
                        token.ThrowIfCancellationRequested(); var root = manifest.Roots.Single(r => r.Key == file.RootKey); var destination = HubPaths.SafeChild(root.Source, file.Relative);
                        HubPaths.CheckParents(root.Source, destination); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        var temporary = destination + ".FishBowl-" + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            File.Copy(HubPaths.SafeChild(stage, file.ArchivePath), temporary, false);
                            if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                }
                return before;
            }
            catch (Exception ex) { throw new IOException("Restore did not finish. " + (before.Length > 0 ? "Previous files are preserved in: " + before + "\n" : "") + ex.Message, ex); }
            finally { HubPaths.DeleteStage(backupDirectory, stage); }
        }
    }


    public class HealthCheck
    {
        public string Name, Status, Detail;
    }
    public static class EmulatorHealth
    {
        public static List<HealthCheck> Check(EmulatorProfile profile, LibraryData library)
        {
            var checks = new List<HealthCheck>();
            checks.Add(new HealthCheck { Name = "Emulator program", Status = File.Exists(profile.Executable) ? "Found" : "Missing", Detail = profile.Executable });
            checks.Add(new HealthCheck { Name = "Launch format", Status = EmulatorReference.IsLaunchFile(profile.Executable ?? "") ? "Supported" : "Check path", Detail = Platform.LaunchFileDescription });
            var state = EmulatorRuntime.State(profile.Executable);
            checks.Add(new HealthCheck { Name = "Running status", Status = state == RuntimeState.Running ? "Running" : state == RuntimeState.Unknown ? "Unknown" : "Stopped", Detail = state == RuntimeState.Unknown ? "Wrappers and inaccessible processes cannot always be tracked." : "Matched using the executable's full path." });
            checks.Add(new HealthCheck { Name = "Installed version", Status = "Information", Detail = EmulatorReference.VersionFor(profile) });
            checks.Add(new HealthCheck { Name = "Dedicated emulator folder", Status = Directory.Exists(HubPaths.EmulatorRoot(library)) ? "Found" : "Not created", Detail = HubPaths.EmulatorRoot(library) + " — existing installations can remain elsewhere." });
            foreach (var folder in new EmulatorFolderDetector().Detect(profile).Where(f => f.Property != "Program" && f.Property != "SaveFolder"))
                checks.Add(new HealthCheck { Name = folder.Label, Status = String.IsNullOrWhiteSpace(folder.Path) ? "Manual check" : Directory.Exists(folder.Path) ? "Found" : "Not created / unavailable", Detail = (folder.Path ?? "") + " — " + folder.Source });
            if (!String.IsNullOrWhiteSpace(profile.FirmwareFolder))
                checks.Add(new HealthCheck { Name = "Chosen firmware folder", Status = Directory.Exists(profile.FirmwareFolder) ? "Found" : "Missing", Detail = profile.FirmwareFolder + " — the emulator validates firmware contents." });
            else checks.Add(new HealthCheck { Name = "Firmware / BIOS", Status = "Follow setup guide", Detail = "Requirements vary by emulator. Choose a firmware folder to check its availability; FishBowl does not download or validate firmware." });
            return checks;
        }
    }

    public static class GameStorage
    {
        public static string Root(LibraryData library)
        {
            return String.IsNullOrWhiteSpace(library.GameLibraryRoot) ? Path.Combine(Platform.Documents, "FishBowl", "Games") : Path.GetFullPath(library.GameLibraryRoot);
        }
        public static string ConsoleFor(string file)
        {
            switch ((Path.GetExtension(file) ?? "").ToLowerInvariant())
            {
                case ".nes": return "Nintendo Entertainment System";
                case ".sfc": case ".smc": return "Super Nintendo";
                case ".gb": return "Game Boy";
                case ".gbc": return "Game Boy Color";
                case ".gba": return "Game Boy Advance";
                case ".nds": return "Nintendo DS";
                case ".3ds": case ".cia": return "Nintendo 3DS";
                case ".n64": case ".z64": case ".v64": return "Nintendo 64";
                case ".gcm": case ".gcz": case ".rvz": return "Nintendo GameCube";
                case ".wbfs": case ".wad": return "Nintendo Wii";
                case ".wud": case ".wux": return "Nintendo Wii U";
                case ".psx": case ".pbp": case ".chd": return "PlayStation";
                case ".cso": return "PlayStation Portable";
                case ".vpk": return "PlayStation Vita";
                case ".xiso": case ".xbe": return "Xbox";
                case ".xex": return "Xbox 360";
                case ".md": case ".gen": case ".bin": return "Sega Genesis";
                case ".cue": return "Disc Images";
                case ".arc": case ".zip": return "Arcade and Archives";
                default: return "Needs review - " + ((Path.GetExtension(file) ?? ".file").TrimStart('.').ToUpperInvariant());
            }
        }
        public static IEnumerable<string> Expand(IEnumerable<string> paths)
        {
            foreach (var path in paths ?? Enumerable.Empty<string>())
            {
                if (File.Exists(path)) yield return path;
                else if (Directory.Exists(path))
                {
                    IEnumerable<string> files; try { files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Take(5000).ToArray(); } catch { continue; }
                    foreach (var file in files) yield return file;
                }
            }
        }
        public static string MoveOrCopy(string source, string root, string console, bool copy)
        {
            string safeConsole = String.Join("", console.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            string destinationFolder = Path.Combine(root, safeConsole); Directory.CreateDirectory(destinationFolder);
            string destination = Path.Combine(destinationFolder, Path.GetFileName(source));
            int number = 2;
            while (File.Exists(destination)) { destination = Path.Combine(destinationFolder, Path.GetFileNameWithoutExtension(source) + " (" + number++ + ")" + Path.GetExtension(source)); }
            if (copy) File.Copy(source, destination); else { try { File.Move(source, destination); } catch (IOException) { File.Copy(source, destination); File.Delete(source); } }
            return destination;
        }
    }


    public static class GameLibraryCatalog
    {
        public static int Sync(LibraryData library)
        {
            int added = 0;
            foreach (var emulator in library.Emulators.Where(item => Directory.Exists(item.ScanFolder) && item.Extensions != null && item.Extensions.Count > 0))
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(emulator.ScanFolder, "*.*", SearchOption.AllDirectories).Take(5000).ToArray(); }
                catch { continue; }
                foreach (var file in files)
                {
                    if (!emulator.Extensions.Any(ext => String.Equals(ext.Trim().TrimStart('.'), Path.GetExtension(file).TrimStart('.'), StringComparison.OrdinalIgnoreCase))) continue;
                    if (library.Games.Any(game => String.Equals(game.Path, file, StringComparison.OrdinalIgnoreCase))) continue;
                    library.Games.Add(new GameEntry { Id = Guid.NewGuid().ToString("N"), EmulatorId = emulator.Id, Title = Path.GetFileNameWithoutExtension(file), Path = file, AddedAt = DateTime.UtcNow.ToString("o"), Tags = new List<string>() });
                    added++;
                }
            }
            return added;
        }
    }


}
