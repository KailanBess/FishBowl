using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EmulatorHub
{
    public class EmulatorProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Preset { get; set; }
        public string Executable { get; set; }
        public string IconPath { get; set; }
        public List<string> Extensions { get; set; }
        public string Arguments { get; set; }
        public string ScanFolder { get; set; }
        public string SaveFolder { get; set; } // Preserved legacy shortcut.
        public string InGameSaveFolder { get; set; }
        public string SaveStateFolder { get; set; }
        public string BannerPath { get; set; }
        public string AccentColor { get; set; }
        public string LastGameFolder { get; set; }
        public List<LaunchProfile> LaunchProfiles { get; set; }
        public string ControllerProfileNotes { get; set; }
        public bool Favorite { get; set; }
        public string Platform { get; set; }
        public string Notes { get; set; }
        public string ManualVersion { get; set; }
        public string Description { get; set; }
        public string Strengths { get; set; }
        public string Limitations { get; set; }
        public string Requirements { get; set; }
        public string ControllerInfo { get; set; }
        public string WebsiteUrl { get; set; }
        public string DocumentationUrl { get; set; }
        public string CompatibilityUrl { get; set; }
        public string ReleasesUrl { get; set; }
        public string ChangelogUrl { get; set; }
        public string ControllerGuideUrl { get; set; }
        public string TroubleshootingUrl { get; set; }
        public string ConfigFolder { get; set; }
        public string ScreenshotFolder { get; set; }
        public string LogFolder { get; set; }
        public List<EmulatorBuild> Builds { get; set; }
        public string GitHubRepository { get; set; }
        public bool IncludePreviewReleases { get; set; }
        public string LatestReleaseTag { get; set; }
        public string LatestReleaseUrl { get; set; }
        public string LatestReleaseNotes { get; set; }
        public string LastUpdateCheck { get; set; }
        public string FirmwareFolder { get; set; }

    }

    public class LaunchProfile
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
    }

    public class GameEntry
    {
        public string Id { get; set; }
        public string EmulatorId { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string ArtworkPath { get; set; }
        public string Arguments { get; set; }
        public string Notes { get; set; }
        public string PreferredEmulatorId { get; set; }
        public bool Favorite { get; set; }
        public string AddedAt { get; set; }
        public string LastLaunched { get; set; }
        public int LaunchCount { get; set; }
        public List<string> Tags { get; set; }
        public string Genre { get; set; }
        public string Developer { get; set; }
        public string Description { get; set; }
        public string ReleaseYear { get; set; }
        public long TotalPlaySeconds { get; set; }
    }

    public class WebsiteLink
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class GameConverter
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string EmulatorId { get; set; }
        public string InputExtensions { get; set; }
        public string OutputExtension { get; set; }
        public string Mode { get; set; }
        public string Program { get; set; }
        public string Arguments { get; set; }
    }

    public class GameCollection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> GameIds { get; set; }
        public bool IsSmart { get; set; }
        public string SmartRule { get; set; }
        public int SortOrder { get; set; }
    }

    public class ThemeSettings
    {
        public string Name { get; set; }
        public int AutoBackupDays { get; set; }
        public string LastBackupAt { get; set; }
        public bool DiscordRichPresenceEnabled { get; set; }
        public bool WelcomeComplete { get; set; }
        public bool ShowStartupAssistant { get; set; }
        public bool StartupAssistantPreferenceSet { get; set; }
        public bool StartMaximized { get; set; }
        public bool AutoSyncGameFolders { get; set; }
        public bool ConfirmBeforeGameLaunch { get; set; }
        public int CustomizationVersion { get; set; }
        public string AccentColor { get; set; }
        public string FontFamily { get; set; }
        public int UiScalePercent { get; set; }
        public string ListDensity { get; set; }
        public bool ShowBanner { get; set; }
        public bool ShowStatusBar { get; set; }
        public bool ShowInformationPanel { get; set; }
        public bool ShowEmulatorIcons { get; set; }
        public bool EnableMotion { get; set; }
        public bool AlternateRowShading { get; set; }
        public string SelectionContrast { get; set; }
        public string IconTileShape { get; set; }
        public bool ShowGameStorageAssistant { get; set; }
        public bool GameStorageAssistantPreferenceSet { get; set; }
    }

    public class LibraryData
    {
        public int Version { get; set; }
        public List<EmulatorProfile> Emulators { get; set; }
        public List<GameEntry> Games { get; set; }
        public List<WebsiteLink> Links { get; set; }
        public List<GameConverter> Converters { get; set; }
        public List<GameCollection> Collections { get; set; }
        public ThemeSettings Theme { get; set; }
        public string BackupFolder { get; set; }
        public string EmulatorRootDirectory { get; set; }
        public string GameLibraryRoot { get; set; }
    }

    public static class Store
    {
        private static readonly string PortableMarker = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.flag");
        public static bool PortableMode { get { return File.Exists(PortableMarker); } }
        public static string DataDirectory { get { return PortableMode ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FishBowlData") : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FishBowl"); } }
        public static string FileName { get { return System.IO.Path.Combine(DataDirectory, "library.json"); } }
        private static readonly string LegacyFileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmulatorHub", "library.json");
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };

        public static LibraryData Load()
        {
            try
            {
                var source = File.Exists(FileName) ? FileName : LegacyFileName;
                var data = Serializer.Deserialize<LibraryData>(File.ReadAllText(source));
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
                    MessageBox.Show("FishBowl could not read its settings. A recovery copy was saved before opening an empty hub.\n\n" + recovery, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return new LibraryData { Version = 1, Emulators = new List<EmulatorProfile>(), Games = new List<GameEntry>(), Links = new List<WebsiteLink>(), Converters = new List<GameConverter>(), Collections = new List<GameCollection>(), Theme = new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true, StartupAssistantPreferenceSet = true, ShowGameStorageAssistant = true, GameStorageAssistantPreferenceSet = true, AutoSyncGameFolders = true, AccentColor = "Sunset", FontFamily = "Bahnschrift", UiScalePercent = 100, ListDensity = "Standard", ShowBanner = true, ShowStatusBar = true, ShowInformationPanel = true, ShowEmulatorIcons = true, EnableMotion = true, AlternateRowShading = true, SelectionContrast = "Standard", IconTileShape = "Rounded", CustomizationVersion = 3 } };
            }
        }

        public static void Save(LibraryData data)
        {
            Directory.CreateDirectory(DataDirectory);
            var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Serializer.Serialize(data));
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
        public EmulatorPreset(string name, string platform, string website, params string[] executables) { Name = name; Platform = platform; Website = website; Executables = executables; }
        public string Label { get { return Name + " (" + Platform + ")"; } }
        public override string ToString() { return Label; }
    }

    public static class EmulatorCatalog
    {
        public static readonly EmulatorPreset[] Presets = new[]
        {
            new EmulatorPreset("Custom", "Other emulator", ""),
            new EmulatorPreset("Mesen", "NES / SNES / Game Boy / PC Engine", "https://www.mesen.ca/", "mesen.exe"),
            new EmulatorPreset("Snes9x", "SNES", "https://www.snes9x.com/", "snes9x.exe", "snes9x-x64.exe"),
            new EmulatorPreset("bsnes", "SNES", "https://github.com/bsnes-emu/bsnes", "bsnes.exe"),
            new EmulatorPreset("SameBoy", "Game Boy / Game Boy Color", "https://sameboy.github.io/", "sameboy.exe"),
            new EmulatorPreset("mGBA", "Game Boy Advance", "https://mgba.io/", "mgba.exe"),
            new EmulatorPreset("melonDS", "Nintendo DS / DSi", "https://github.com/melonDS-emu/melonDS", "melonds.exe"),
            new EmulatorPreset("DeSmuME", "Nintendo DS", "https://desmume.org/", "desmume.exe", "desmume_0.9.13_x64.exe"),
            new EmulatorPreset("Azahar Plus", "Nintendo 3DS", "https://github.com/AzaharPlus/AzaharPlus", "azaharplus.exe", "azahar-plus.exe"),
            new EmulatorPreset("Gopher64", "Nintendo 64", "https://github.com/gopher64/gopher64", "gopher64.exe", "gopher64-windows-x86_64.exe"),
            new EmulatorPreset("RMG", "Nintendo 64", "https://github.com/Rosalie241/RMG", "rmg.exe"),
            new EmulatorPreset("Dolphin", "GameCube / Wii", "https://dolphin-emu.org/", "dolphin.exe"),
            new EmulatorPreset("Cemu", "Wii U", "https://cemu.info/", "cemu.exe"),
            new EmulatorPreset("Eden", "Nintendo Switch", "https://eden-emu.dev/", "eden.exe"),
            new EmulatorPreset("DuckStation", "PlayStation", "https://github.com/stenzek/duckstation", "duckstation.exe", "duckstation-qt-x64-release.exe"),
            new EmulatorPreset("PCSX2", "PlayStation 2", "https://pcsx2.net/", "pcsx2.exe", "pcsx2-qt.exe"),
            new EmulatorPreset("RPCS3", "PlayStation 3", "https://github.com/RPCS3/rpcs3", "rpcs3.exe"),
            new EmulatorPreset("shadPS4", "PlayStation 4 - experimental", "https://github.com/shadps4-emu/shadPS4", "shadps4.exe"),
            new EmulatorPreset("PPSSPP", "PSP", "https://www.ppsspp.org/", "ppssppwindows64.exe", "ppssppwindows.exe"),
            new EmulatorPreset("Vita3K", "PS Vita - experimental", "https://vita3k.org/", "vita3k.exe"),
            new EmulatorPreset("xemu", "Xbox", "https://xemu.app/", "xemu.exe"),
            new EmulatorPreset("Xenia Canary", "Xbox 360 - experimental", "https://github.com/xenia-canary/xenia-canary", "xenia_canary.exe"),
            new EmulatorPreset("BlastEm", "Mega Drive / Genesis", "https://www.retrodev.com/blastem/", "blastem.exe"),
            new EmulatorPreset("Ymir", "Sega Saturn", "https://github.com/ymir-emu/Ymir", "ymir-sdl3.exe", "ymir.exe"),
            new EmulatorPreset("Flycast", "Dreamcast / NAOMI / Atomiswave", "https://github.com/flyinghead/flycast", "flycast.exe"),
            new EmulatorPreset("Stella", "Atari 2600", "https://stella-emu.github.io/", "stella.exe"),
            new EmulatorPreset("Altirra", "Atari 8-bit / 5200", "https://www.virtualdub.org/altirra.html", "altirra64.exe", "altirra.exe"),
            new EmulatorPreset("BigPEmu", "Atari Jaguar / Jaguar CD", "https://www.richwhitehouse.com/jaguar/", "bigpemu.exe"),
            new EmulatorPreset("Hatari", "Atari ST / STE / TT / Falcon", "https://hatari.tuxfamily.org/", "hatari.exe"),
            new EmulatorPreset("MAME", "Arcade / multiple systems", "https://www.mamedev.org/", "mame.exe", "mame64.exe"),
            new EmulatorPreset("FinalBurn Neo", "Arcade / Neo Geo", "https://github.com/finalburnneo/FBNeo", "fbneo.exe", "fbneo64.exe", "fbn.exe", "fbn64.exe"),
            new EmulatorPreset("ares", "Multiple systems", "https://ares-emu.net/", "ares.exe"),
            new EmulatorPreset("RetroArch", "Multiple systems / cores", "https://www.retroarch.com/", "retroarch.exe"),
            new EmulatorPreset("BizHawk", "Multiple systems / TAS", "https://github.com/TASEmulators/BizHawk", "emuhawk.exe"),
            new EmulatorPreset("DOSBox Staging", "DOS games", "https://www.dosbox-staging.org/", "dosbox.exe"),
            new EmulatorPreset("DOSBox-X", "DOS / Windows 3.x / 9x", "https://dosbox-x.com/", "dosbox-x.exe"),
            new EmulatorPreset("86Box", "Classic IBM PCs", "https://86box.net/", "86box.exe"),
            new EmulatorPreset("ScummVM", "Classic adventure games", "https://www.scummvm.org/", "scummvm.exe"),
            new EmulatorPreset("WinUAE", "Amiga / CD32 / CDTV", "https://github.com/tonioni/WinUAE", "winuae.exe", "winuae64.exe"),
            new EmulatorPreset("VICE", "Commodore 64 / 128 / VIC-20", "https://vice-emu.sourceforge.io/", "x64sc.exe", "x128.exe", "xvic.exe"),
            new EmulatorPreset("openMSX", "MSX / MSX2", "https://openmsx.org/", "openmsx.exe"),
            new EmulatorPreset("Caprice32", "Amstrad CPC", "https://github.com/ColinPitrat/caprice32", "cap32.exe", "caprice32.exe"),
            new EmulatorPreset("ZEsarUX", "ZX Spectrum / ZX81 / ZX Next", "https://github.com/chernandezba/zesarux", "zesarux.exe"),
        };
        public static EmulatorPreset Find(string label)
        {
            return Presets.FirstOrDefault(p => String.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase) || String.Equals(p.Name, label, StringComparison.OrdinalIgnoreCase)) ?? Presets.FirstOrDefault(p => !String.IsNullOrWhiteSpace(label) && label.StartsWith(p.Name + " (", StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
        }
        public static EmulatorPreset ForExecutable(string executable)
        {
            var name = Path.GetFileName(executable);
            return Presets.FirstOrDefault(p => p.Executables.Any(n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase))) ?? Presets[0];
        }
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
                var info = FileVersionInfo.GetVersionInfo(profile.Executable);
                var version = String.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
                return String.IsNullOrWhiteSpace(version) || version == "0.0.0.0" ? "Not reported by this program" : version;
            }
            catch { return "Version unavailable"; }
        }
        public static bool IsLaunchFile(string path) { var ext = Path.GetExtension(path); return new[] { ".exe", ".bat", ".cmd", ".lnk" }.Any(e => String.Equals(e, ext, StringComparison.OrdinalIgnoreCase)); }
        public static bool IsWebUrl(string value) { Uri uri; return Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps); }
        private static string Pick(string saved, string fallback) { return String.IsNullOrWhiteSpace(saved) ? fallback : saved; }
        public static EmulatorReference For(EmulatorProfile profile)
        {
            var p = EmulatorCatalog.Find(profile.Preset);
            var site = p.Website ?? "";
            var github = site.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase);
            var result = new EmulatorReference {
                Summary = p.Name == "Custom" ? "Your own emulator or emulator frontend. FishBowl can launch any installed Windows emulator, independently of the preset list." : p.Name + " is an emulator or frontend for " + p.Platform + ".",
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

    public partial class MainForm : Form
    {
        private string DisplayFont { get { return String.IsNullOrWhiteSpace(library.Theme.FontFamily) ? "Bahnschrift" : library.Theme.FontFamily; } }
        private readonly Icon ownedAppIcon;
        private bool visualResourcesDisposed;
        private readonly LibraryData library = Store.Load();
        private readonly ListView emulatorList = new SmoothListView();
        private readonly ImageList emulatorImages = new ImageList();
        private readonly List<Image> emulatorImageSources = new List<Image>();
        private readonly TextBox filterBox = new TextBox();
        private readonly Label status = new Label();
        private readonly Button editButton = new FishBowlActionButton();
        private readonly Button removeButton = new FishBowlActionButton();
        private readonly ComboBox platformFilter = new ComboBox();
        private readonly CheckBox favoritesOnly = new CheckBox();
        private readonly Button favoriteButton = new FishBowlActionButton();
        private readonly Button informationButton = new FishBowlActionButton();
        private readonly Label infoTitle = new Label();
        private readonly Label infoVersion = new Label();
        private readonly Label infoPlatform = new Label();
        private readonly TextBox overviewText = new TextBox();
        private readonly TextBox setupText = new TextBox();
        private readonly TextBox controllersText = new TextBox();
        private readonly TextBox helpText = new TextBox();
        private readonly TextBox notesBox = new TextBox();
        private readonly Label notesState = new Label();
        private readonly TabControl infoTabs = new FishBowlTabs();
        private readonly TableLayoutPanel folderTable = new TableLayoutPanel();
        private readonly Timer notesTimer = new Timer { Interval = 600 };
        private readonly Timer gameFolderTimer = new Timer { Interval = 1100 };
        private readonly List<FileSystemWatcher> gameFolderWatchers = new List<FileSystemWatcher>();
        private readonly List<Tuple<Button, Func<EmulatorReference, string>>> referenceButtons = new List<Tuple<Button, Func<EmulatorReference, string>>>();
        private string notesProfileId;
        private bool loadingInformation, notesDirty, loadingFilters;
        private readonly ToolStripMenuItem linksMenu = new ToolStripMenuItem("Links");
        private readonly ToolStripMenuItem convertersMenu = new ToolStripMenuItem("Game converters");
        private string selectedEmulatorId;
        private string hoveredEmulatorId;
        private SplitContainer emulatorSplit;
        private Control emulatorInformation;
        private bool refreshing;
        private bool selectionPending;
        private bool reloadProgramMetadata;
        private int metadataGeneration;
        private int folderRequest;
        private string folderRequestKey;
        private readonly Dictionary<string, Image> emulatorIconCache = new Dictionary<string, Image>();
        private readonly Dictionary<string, Tuple<DateTime, System.Threading.Tasks.Task<string>>> versionCache = new Dictionary<string, Tuple<DateTime, System.Threading.Tasks.Task<string>>>();
        private bool fullScreen;
        private FormBorderStyle savedBorderStyle;
        private FormWindowState savedWindowState;
        private Rectangle savedBounds;
        private Rectangle savedNormalBounds;
        private Color ink, top, bottom, surface, subtle, blue, pink;
        private Color[] swatches = { Color.FromArgb(162, 82, 241), Color.FromArgb(255, 137, 48), Color.FromArgb(207, 94, 238) };

        public MainForm()
        {
            Text = "FishBowl 1.0";
            ownedAppIcon = LoadAppIcon(); Icon = ownedAppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            Size = new Size(1340, 820);
            RestoreWindowLayout();
            if (library.Theme.StartMaximized) WindowState = FormWindowState.Maximized;
            ApplyThemeColors();
            BackColor = bottom;
            KeyPreview = true;
            selectedEmulatorId = null;
            BuildLayout();
            if (library.Theme.EnableMotion) FishBowlHighlights.Attach(this);
            RefreshHub();
            ApplyVisualScale();
            ConfigureGameFolderWatchers();
            SetStatus("Double-click an emulator to open it. Manage games inside the emulator.");
            KeyDown += OnKeyDown;
            runtimeTimer.Tick += delegate { RefreshRuntimeStatus(); }; runtimeTimer.Start();
            notesTimer.Tick += delegate { RunUiAction(SaveProfileNotes); };
            gameFolderTimer.Tick += delegate { gameFolderTimer.Stop(); RunUiAction(SyncWatchedGameFolders); };
            Shown += delegate { if (library.Theme.ShowStartupAssistant) ShowFirstRunGuide(); if (library.Theme.ShowGameStorageAssistant) ShowGameStoragePrompt(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!e.Cancel) { try { SaveProfileNotes(); SaveWindowLayout(); } catch (Exception error) { e.Cancel = true; MessageBox.Show(this, "Your notes could not be saved.\n\n" + error.Message, "FishBowl"); } } };
        }

        private void BuildLayout()
        {
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = bottom };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, library.Theme.ShowBanner ? 76 : 0));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, library.Theme.ShowStatusBar ? 38 : 0));
            Controls.Add(shell);
            shell.Controls.Add(BuildMenu(), 0, 0);
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = top, Padding = new Padding(12) };
            var logo = LoadLogo();
            var picture = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(60, 60), Location = new Point(12, 8) };
            picture.Disposed += delegate { if (logo != null) logo.Dispose(); };
            banner.Controls.Add(picture);
            banner.Controls.Add(new Label { Text = "FishBowl", ForeColor = ink, Font = new Font(DisplayFont, 18, FontStyle.Bold), AutoSize = true, Location = new Point(84, 8) });
            banner.Controls.Add(new Label { Text = "Your emulators, together. Add and play games inside each emulator.", ForeColor = subtle, Font = new Font(DisplayFont, 9), AutoSize = true, Location = new Point(86, 43) });
            shell.Controls.Add(banner, 0, 1);
            shell.Controls.Add(BuildPrimaryActions(), 0, 2);
            int iconSize = library.Theme.ListDensity == "Compact" ? 40 : library.Theme.ListDensity == "Comfortable" ? 56 : 48;
            emulatorImages.ImageSize = new Size(iconSize, iconSize);
            emulatorImages.ColorDepth = ColorDepth.Depth32Bit;
            emulatorList.Dock = DockStyle.Fill;
            emulatorList.BackColor = bottom;
            emulatorList.ForeColor = ink;
            emulatorList.Font = new Font(DisplayFont, 10);
            emulatorList.View = View.Details;
            emulatorList.FullRowSelect = true;
            emulatorList.BorderStyle = BorderStyle.None;
            emulatorList.OwnerDraw = true;
            emulatorList.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (var brush = new SolidBrush(surface)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, emulatorList.Font, Rectangle.Inflate(e.Bounds, -8, 0), subtle, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            emulatorList.DrawItem += delegate { };
            emulatorList.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var selected = e.Item.Selected;
                var selectionAlpha = library.Theme.SelectionContrast == "Soft" ? 58 : library.Theme.SelectionContrast == "Strong" ? 132 : 88;
                var background = selected ? Color.FromArgb(selectionAlpha, blue.R, blue.G, blue.B) : (library.Theme.AlternateRowShading && e.Item.Index % 2 != 0 ? Color.FromArgb(20, surface) : bottom);
                using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (selected)
                {
                    using (var border = new Pen(Color.FromArgb(207, 168, 255)))
                    {
                        e.Graphics.DrawLine(border, e.Bounds.Left, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Top);
                        e.Graphics.DrawLine(border, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                        if (e.ColumnIndex == emulatorList.Columns.Count - 1) e.Graphics.DrawLine(border, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                    }
                    if (e.ColumnIndex == 0) using (var accent = new SolidBrush(Color.FromArgb(255, 164, 82))) e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height);
                }
                var bounds = Rectangle.Inflate(e.Bounds, -8, 0);
                if (library.Theme.ShowEmulatorIcons && e.ColumnIndex == 0 && e.Item.ImageIndex >= 0)
                {
                    var iconBounds = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + (e.Bounds.Height - 32) / 2, 32, 32);
                    var tile = new RectangleF(iconBounds.Left - 4, iconBounds.Top - 4, 40, 40); var accentColor = swatches[e.Item.Index % swatches.Length];
                    int radius = library.Theme.IconTileShape == "Square" ? 2 : library.Theme.IconTileShape == "Circular" ? 20 : 7;
                    using (var tilePath = FishBowlVisuals.Round(tile, radius)) using (var tileFill = new SolidBrush(Color.FromArgb(18, accentColor))) using (var tilePen = new Pen(Color.FromArgb(58, accentColor))) { e.Graphics.FillPath(tileFill, tilePath); e.Graphics.DrawPath(tilePen, tilePath); }
                    if (library.Theme.EnableMotion && (selected || (e.Item.Tag as string) == hoveredEmulatorId)) FishBowlHighlights.Draw(e.Graphics, Rectangle.Inflate(iconBounds, 2, 2), background, selected);
                    e.Graphics.DrawImage(emulatorImageSources[e.Item.ImageIndex], iconBounds);
                    bounds = new Rectangle(e.Bounds.Left + 58, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 66), e.Bounds.Height);
                }
                var color = selected ? Color.White : ink;
                if (e.ColumnIndex == 1)
                {
                    bool available = e.SubItem.Text == "Ready" || e.SubItem.Text == "Running";
                    var dot = available ? (bottom.GetBrightness() < 0.65f ? Color.FromArgb(118,211,161) : Color.FromArgb(35,126,82)) : e.SubItem.Text == "Missing program" ? Color.FromArgb(246,183,105) : subtle;
                    using (var brush = new SolidBrush(dot)) e.Graphics.FillEllipse(brush, bounds.Left + 1, e.Bounds.Top + (e.Bounds.Height - 6) / 2, 6, 6);
                    bounds = new Rectangle(bounds.Left + 14, bounds.Top, Math.Max(0,bounds.Width - 14), bounds.Height);
                    color = selected ? Color.White : dot;
                }
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, emulatorList.Font, bounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            emulatorList.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                var hovered = emulatorList.GetItemAt(e.X, e.Y); var next = hovered == null ? null : hovered.Tag as string;
                if (hoveredEmulatorId == next) return; var previous = hoveredEmulatorId; hoveredEmulatorId = next; InvalidateEmulatorRow(previous); InvalidateEmulatorRow(next);
            };
            emulatorList.MouseLeave += delegate { var previous = hoveredEmulatorId; hoveredEmulatorId = null; InvalidateEmulatorRow(previous); };
            emulatorList.HideSelection = false;
            emulatorList.MultiSelect = false;
            emulatorList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            emulatorList.SmallImageList = emulatorImages;
            emulatorList.Columns.Add("Emulator", 270);
            emulatorList.Columns.Add("Status", 140);
            emulatorList.Columns.Add("Platform / preset", 320);
            emulatorList.Columns.Add("Program", 240);
            emulatorList.Resize += delegate { ResizeEmulatorColumns(); };
            emulatorList.SelectedIndexChanged += delegate
            {
                if (refreshing) return;
                // Native single-select lists notify deselection before selecting the next row.
                // Wait for the next message only when empty, keeping the details pane stable.
                if (emulatorList.SelectedItems.Count != 0) { ApplyEmulatorSelection(); return; }
                if (selectionPending || !IsHandleCreated) return;
                selectionPending = true;
                BeginInvoke(new MethodInvoker(delegate { selectionPending = false; if (!IsDisposed && !refreshing) ApplyEmulatorSelection(); }));
            };
            emulatorList.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                var item = emulatorList.GetItemAt(e.X, e.Y);
                if (item == null) return;
                item.Selected = true;
                selectedEmulatorId = item.Tag as string;
                RunUiAction(OpenSelectedEmulator);
            };
            var context = new ContextMenuStrip { Renderer = new FishBowlMenuRenderer(), BackColor = surface, ForeColor = ink, ImageScalingSize = new Size(20,20) };
            context.Items.Add(MenuAction("Open emulator", "play", OpenSelectedEmulator));
            context.Items.Add(MenuAction("Edit emulator", "edit", EditEmulator));
            context.Items.Add(MenuAction("Favorite / unfavorite", "star", ToggleEmulatorFavorite));
            context.Items.Add(MenuAction("Edit information and links", "info", EditEmulatorInformation));
            context.Items.Add(MenuAction("Open program folder", "folder", OpenEmulatorFolder));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(MenuAction("Remove from FishBowl", "remove", RemoveEmulator));
            emulatorList.ContextMenuStrip = context;
            emulatorList.Disposed += delegate { context.Dispose(); };
            emulatorList.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
                var item = emulatorList.GetItemAt(e.X, e.Y);
                if (item == null) ClearEmulatorSelection(); else if (e.Button == MouseButtons.Right) item.Selected = true;
            };
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1300, 550), SplitterWidth = 8, BackColor = top, Panel1MinSize = 450, Panel2MinSize = 370, SplitterDistance = 770 };
            emulatorSplit = split; emulatorInformation = BuildInformationPanel();
            split.Panel1.Controls.Add(emulatorList);
            split.Panel2.Controls.Add(emulatorInformation);
            split.Panel2Collapsed = !library.Theme.ShowInformationPanel;
            shell.Controls.Add(split, 0, 3);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = top, Padding = new Padding(8, 5, 8, 5) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.Controls.Add(new Label { Text = "Filter:", ForeColor = subtle, Font = new Font(DisplayFont, 9), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            filterBox.Dock = DockStyle.Fill;
            filterBox.BackColor = surface; filterBox.ForeColor = ink; filterBox.BorderStyle = BorderStyle.FixedSingle;
            filterBox.TextChanged += delegate { RefreshHub(); };
            footer.Controls.Add(filterBox, 1, 0);
            status.Dock = DockStyle.Fill; status.ForeColor = subtle; status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft; status.Margin = new Padding(14, 0, 0, 0);
            footer.Controls.Add(status, 2, 0);
            shell.Controls.Add(footer, 0, 4);
            foreach (var control in new Control[] { this, emulatorList, banner })
            {
                control.AllowDrop = true;
                control.DragEnter += delegate(object sender, DragEventArgs e) { var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effect = paths != null && paths.Any(p => EmulatorReference.IsLaunchFile(p)) ? DragDropEffects.Copy : DragDropEffects.None; };
                control.DragDrop += delegate(object sender, DragEventArgs e) { RunUiAction(delegate { HandleDroppedPaths(e.Data.GetData(DataFormats.FileDrop) as string[]); }); };
            }
        }

        private Control BuildInformationPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = top };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            infoTitle.Font = new Font(DisplayFont, 17, FontStyle.Bold); infoTitle.ForeColor = ink; infoTitle.Dock = DockStyle.Fill; infoTitle.AutoEllipsis = true;
            infoVersion.ForeColor = subtle; infoVersion.Dock = DockStyle.Fill; infoVersion.AutoEllipsis = true;
            infoPlatform.ForeColor = subtle; infoPlatform.Dock = DockStyle.Fill; infoPlatform.AutoEllipsis = true;
            panel.Controls.Add(infoTitle, 0, 0); panel.Controls.Add(infoVersion, 0, 1); panel.Controls.Add(infoPlatform, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ConfigureButton(favoriteButton, "Favorite", ToggleEmulatorFavorite, surface);
            ConfigureButton(informationButton, "Edit information", EditEmulatorInformation, surface);
            actions.Controls.Add(favoriteButton); actions.Controls.Add(informationButton); panel.Controls.Add(actions, 0, 3);
            infoTabs.Dock = DockStyle.Fill; infoTabs.Font = new Font(DisplayFont, 9);
            BuildInfoTab("Overview", overviewText, new[] { Tuple.Create("Project", new Func<EmulatorReference, string>(r => r.Website)), Tuple.Create("Releases", new Func<EmulatorReference, string>(r => r.Releases)), Tuple.Create("Changelog", new Func<EmulatorReference, string>(r => r.Changelog)) });
            BuildInfoTab("Setup", setupText, new[] { Tuple.Create("Setup guide", new Func<EmulatorReference, string>(r => r.Documentation)), Tuple.Create("Compatibility", new Func<EmulatorReference, string>(r => r.Compatibility)) });
            var controllers = BuildInfoTab("Controls", controllersText, new[] { Tuple.Create("Controller guide", new Func<EmulatorReference, string>(r => r.ControllerGuide)) });
            var windowsControllers = new FishBowlActionButton(); ConfigureButton(windowsControllers, "Windows controllers", delegate { Process.Start(new ProcessStartInfo("control.exe", "joy.cpl") { UseShellExecute = true }); }, surface);
            controllers.Controls.Add(windowsControllers);
            var folders = new TabPage("Folders") { BackColor = bottom, Padding = new Padding(10), AutoScroll = true };
            folderTable.Dock = DockStyle.Top; folderTable.AutoSize = true; folderTable.ColumnCount = 4;
            folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); folderTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            folders.Controls.Add(folderTable); infoTabs.TabPages.Add(folders);
            BuildInfoTab("Help", helpText, new[] { Tuple.Create("Troubleshooting", new Func<EmulatorReference, string>(r => r.Troubleshooting)) });
            var notes = new TabPage("Notes") { BackColor = bottom, Padding = new Padding(10) };
            var noteLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            noteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); noteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            StyleInfoText(notesBox, false); notesBox.TextChanged += delegate { if (loadingInformation) return; notesDirty = true; notesState.Text = "Saving..."; notesTimer.Stop(); notesTimer.Start(); };
            notesState.ForeColor = subtle; notesState.Dock = DockStyle.Fill; notesState.TextAlign = ContentAlignment.MiddleLeft;
            noteLayout.Controls.Add(notesBox, 0, 0); noteLayout.Controls.Add(notesState, 0, 1); notes.Controls.Add(noteLayout); infoTabs.TabPages.Add(notes);
            FishBowlVisuals.Tabs(infoTabs, top, ink, blue, "info", "settings", "controller", "folder", "help", "note");
            infoTabs.SelectedIndexChanged += delegate { EnsureFolderRows(); };
            panel.Controls.Add(infoTabs, 0, 4);
            return panel;
        }
        private void StyleInfoText(TextBox box, bool readOnly)
        {
            box.Multiline = true; box.ReadOnly = readOnly; box.ScrollBars = ScrollBars.Vertical; box.Dock = DockStyle.Fill;
            box.BackColor = bottom; box.ForeColor = ink; box.BorderStyle = BorderStyle.None; box.Font = new Font(DisplayFont, 10); box.WordWrap = true;
        }
        private FlowLayoutPanel BuildInfoTab(string title, TextBox box, IEnumerable<Tuple<string, Func<EmulatorReference, string>>> links)
        {
            var tab = new TabPage(title) { BackColor = bottom, Padding = new Padding(10) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            StyleInfoText(box, true); layout.Controls.Add(box, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
            foreach (var link in links)
            {
                var button = new FishBowlActionButton(); var getUrl = link.Item2;
                ConfigureButton(button, link.Item1, delegate { var profile = CurrentEmulator(); if (profile != null) OpenInformationLink(getUrl(EmulatorReference.For(profile))); }, surface);
                button.Margin = new Padding(0, 3, 6, 3); actions.Controls.Add(button); referenceButtons.Add(Tuple.Create((Button)button, getUrl));
            }
            layout.Controls.Add(actions, 0, 1); tab.Controls.Add(layout); infoTabs.TabPages.Add(tab); return actions;
        }
        private void UpdateInformationPanel()
        {
            var profile = CurrentEmulator();
            if (emulatorInformation != null) emulatorInformation.Visible = profile != null;
            bool hideInformation = profile == null || !library.Theme.ShowInformationPanel;
            if (emulatorSplit != null && emulatorSplit.Panel2Collapsed != hideInformation) { emulatorSplit.Panel2Collapsed = hideInformation; ResizeEmulatorColumns(); }
            var nextId = profile == null ? null : profile.Id;
            if (notesProfileId != nextId)
            {
                folderRequest++; folderRequestKey = null;
                SaveProfileNotes(); loadingInformation = true;
                notesProfileId = nextId; notesBox.Text = profile == null ? "" : (profile.Notes ?? ""); notesDirty = false;
                notesState.Text = profile == null ? "Select an emulator to write notes." : "Notes save automatically."; loadingInformation = false;
            }
            favoriteButton.Enabled = informationButton.Enabled = notesBox.Enabled = profile != null;
            infoTitle.Text = profile == null ? "Emulator information" : profile.Name;
            var version = profile == null ? "" : String.IsNullOrWhiteSpace(profile.ManualVersion) ? "Checking version..." : profile.ManualVersion + " (entered manually)";
            infoVersion.Text = profile == null ? "" : "Installed version: " + version;
            infoPlatform.Text = profile == null ? "" : EmulatorReference.PlatformFor(profile);
            favoriteButton.Text = profile != null && profile.Favorite ? "Favorited" : "Favorite";
            if (profile == null)
            {
                overviewText.Text = "Select an emulator to see its information.\r\n\r\nAny Windows emulator can be added using Custom. Double-click a row to open it.";
                setupText.Text = controllersText.Text = helpText.Text = "Select an emulator first.";
                foreach (var link in referenceButtons) link.Item1.Enabled = false;
                folderRequest++; folderRequestKey = null; return;
            }
            var info = EmulatorReference.For(profile);
            overviewText.Text = info.Summary + "\r\n\r\nSTRENGTHS\r\n" + info.Strengths + "\r\n\r\nLIMITATIONS\r\n" + info.Limitations + "\r\n\r\nPROGRAM\r\n" + profile.Executable + "\r\n\r\nVERSION\r\n" + version;
            setupText.Text = info.Requirements + "\r\n\r\nCOMPATIBILITY\r\n" + (String.IsNullOrWhiteSpace(info.Compatibility) ? "No separate compatibility page is linked for this profile. Check the project documentation, or add one in Edit information." : "Open Compatibility below to view the linked compatibility page in your browser. No games are imported into FishBowl.");
            controllersText.Text = info.Controllers + (String.IsNullOrWhiteSpace(profile.ControllerProfileNotes) ? "" : "\r\n\r\nYOUR CONTROLLER NOTES\r\n" + profile.ControllerProfileNotes);
            helpText.Text = "PROGRAM WILL NOT OPEN\r\nCheck that the executable exists and works directly from Windows. Re-select its path in Edit emulator after moving or updating it.\r\n\r\nFIRMWARE / BIOS ERRORS\r\nFollow the emulator's setup guide and check its configured firmware locations.\r\n\r\nGRAPHICS / PERFORMANCE\r\nReview the emulator's hardware requirements, update your graphics driver, and use a supported graphics backend. Change settings inside the emulator.\r\n\r\nCONTROLLER NOT DETECTED\r\nConnect it before opening the emulator, check Windows controllers, and review the emulator's input mapping and controller guide.\r\n\r\nLOGS\r\nUse the Folders tab to open your configured emulator log folder. FishBowl's own log is available from Tools.\r\n\r\nUse the linked troubleshooting guide for emulator-specific instructions.";
            foreach (var link in referenceButtons) link.Item1.Enabled = EmulatorReference.IsWebUrl(link.Item2(info));
            UpdateInstalledVersion(profile);
            EnsureFolderRows();
        }
        private void SaveProfileNotes()
        {
            notesTimer.Stop(); if (!notesDirty || notesProfileId == null) return;
            var profile = library.Emulators.FirstOrDefault(p => p.Id == notesProfileId);
            if (profile == null) { notesDirty = false; return; }
            profile.Notes = notesBox.Text; Store.Save(library); notesDirty = false; notesState.Text = "Saved.";
        }
        private void RefreshPlatformOptions()
        {
            var items = new[] { "All platforms" }.Concat(library.Emulators.SelectMany(EmulatorReference.PlatformTags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s)).ToArray();
            if (platformFilter.Items.Cast<string>().SequenceEqual(items)) return;
            var selected = platformFilter.SelectedItem as string;
            loadingFilters = true; platformFilter.Items.Clear(); platformFilter.Items.AddRange(items);
            platformFilter.SelectedItem = items.Contains(selected) ? selected : "All platforms"; loadingFilters = false;
        }
        private void ToggleEmulatorFavorite()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes(); profile.Favorite = !profile.Favorite; Store.Save(library); RefreshHub();
        }
        private void EditEmulatorInformation()
        {
            var profile = CurrentEmulator(); if (profile == null) return;
            SaveProfileNotes(); using (var dialog = new EmulatorInformationDialog(profile))
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; Store.Save(library); RefreshHub(); SetStatus("Saved information for " + profile.Name + "."); }
        }
        private void OpenInformationLink(string url)
        {
            if (!EmulatorReference.IsWebUrl(url)) throw new InvalidDataException("Enter an http or https web address in Edit information.");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        private void PostUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new MethodInvoker(delegate { if (!IsDisposed && !Disposing) action(); })); }
                catch (InvalidOperationException) { /* Window closed while work completed. */ }
            }
            else action();
        }

        private static EmulatorProfile FolderSnapshot(EmulatorProfile profile)
        {
            return new EmulatorProfile { Id = profile.Id, Name = profile.Name, Executable = profile.Executable, Preset = profile.Preset, ManualVersion = profile.ManualVersion,
                ConfigFolder = profile.ConfigFolder, InGameSaveFolder = profile.InGameSaveFolder, SaveStateFolder = profile.SaveStateFolder,
                ScreenshotFolder = profile.ScreenshotFolder, LogFolder = profile.LogFolder, SaveFolder = profile.SaveFolder };
        }

        private static string FolderKey(EmulatorProfile p)
        {
            return new JavaScriptSerializer().Serialize(new[] { p.Id, p.Name, p.Executable, p.Preset, p.ConfigFolder, p.InGameSaveFolder, p.SaveStateFolder, p.ScreenshotFolder, p.LogFolder, p.SaveFolder });
        }

        private void EnsureFolderRows()
        {
            var profile = CurrentEmulator();
            if (profile == null || infoTabs.SelectedIndex != 3) return;
            if (folderRequestKey != FolderKey(profile)) RefreshFolderRows(profile);
        }

        private async void RefreshFolderRows(EmulatorProfile profile)
        {
            int request = ++folderRequest; folderRequestKey = profile == null ? null : FolderKey(profile);
            if (profile == null || IsDisposed) return;
            // Detect settings and path availability off the UI thread; never change emulator settings.
            var snapshot = FolderSnapshot(profile); string notice = "";
            folderTable.SuspendLayout();
            try { while (folderTable.Controls.Count > 0) folderTable.Controls[0].Dispose(); folderTable.RowStyles.Clear(); folderTable.RowCount = 0; AddFolderMessage("Checking emulator folders...", 38); }
            finally { folderTable.ResumeLayout(true); }
            try
            {
                await System.Threading.Tasks.Task.Delay(60).ConfigureAwait(false);
                if (IsDisposed || request != folderRequest) return;
                var detected = await System.Threading.Tasks.Task.Run(delegate {
                    var detector = new EmulatorFolderDetector(); var locations = detector.Detect(snapshot); notice = detector.Notice;
                    var existing = new HashSet<string>(locations.Where(l => !String.IsNullOrWhiteSpace(l.Path) && Directory.Exists(l.Path)).Select(l => l.Path), StringComparer.OrdinalIgnoreCase);
                    return Tuple.Create(locations, existing);
                }).ConfigureAwait(false);
                PostUi(delegate {
                    if (request != folderRequest || CurrentEmulator() == null || CurrentEmulator().Id != profile.Id || FolderKey(profile) != folderRequestKey) return;
                    RenderFolderRows(profile, detected.Item1, notice, detected.Item2);
                });
            }
            catch (Exception ex)
            {
                if (IsDisposed || request != folderRequest) return;
                Store.Log("Folder shortcuts could not refresh: " + ex.Message);
                PostUi(delegate { if (request != folderRequest) return; folderRequestKey = null; RenderFolderRows(profile, new List<EmulatorFolder>(), "Folder detection could not finish. Select another tab and return to retry.", new HashSet<string>()); });
            }
        }

        private async void UpdateInstalledVersion(EmulatorProfile profile)
        {
            if (!String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
            var executable = profile.Executable ?? ""; int generation = metadataGeneration;
            Tuple<DateTime, System.Threading.Tasks.Task<string>> cached;
            if (!versionCache.TryGetValue(executable, out cached) || DateTime.UtcNow - cached.Item1 > TimeSpan.FromMinutes(1))
            {
                var snapshot = FolderSnapshot(profile);
                cached = Tuple.Create(DateTime.UtcNow, System.Threading.Tasks.Task.Run(() => EmulatorReference.VersionFor(snapshot)));
                versionCache[executable] = cached;
            }
            var version = await cached.Item2.ConfigureAwait(false);
            PostUi(delegate {
                if (generation != metadataGeneration || CurrentEmulator() != profile || profile.Executable != executable || !String.IsNullOrWhiteSpace(profile.ManualVersion)) return;
                infoVersion.Text = "Installed version: " + version;
                int marker = overviewText.Text.LastIndexOf("\r\n\r\nVERSION\r\n", StringComparison.Ordinal);
                if (marker >= 0) overviewText.Text = overviewText.Text.Substring(0, marker) + "\r\n\r\nVERSION\r\n" + version;
            });
        }

        private void RenderFolderRows(EmulatorProfile profile, List<EmulatorFolder> locations, string notice, HashSet<string> existing)
        {
            var host = folderTable.Parent; host.SuspendLayout(); emulatorInformation.SuspendLayout();
            folderTable.SuspendLayout(); folderTable.AutoSize = false;
            try
            {
                while (folderTable.Controls.Count > 0) folderTable.Controls[0].Dispose();
                folderTable.RowStyles.Clear(); folderTable.RowCount = 0;
                if (profile == null) return;
                AddFolderMessage("In-game saves: progress saved by the game.\nSave states: snapshots created by the emulator.\nChoose sets a FishBowl shortcut. Auto restores detection.", 60);
                var refresh = new FishBowlActionButton { Text = "Refresh detected folders", Height = 30, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                refresh.Click += delegate { RunUiAction(delegate { RefreshFolderRows(profile); }); };
                int row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                folderTable.Controls.Add(refresh, 0, row); folderTable.SetColumnSpan(refresh, 4);
                if (!String.IsNullOrWhiteSpace(notice)) AddFolderMessage(notice, 54);
                foreach (var location in locations) AddFolderRow(location, profile, existing.Contains(location.Path));
                if (!String.IsNullOrWhiteSpace(profile.SaveFolder))
                {
                    var classify = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
                    foreach (var kind in new[] { "InGameSaveFolder|Use as in-game saves", "SaveStateFolder|Use as save states" })
                    {
                        var pieces = kind.Split('|'); var destination = pieces[0];
                        var button = new FishBowlActionButton { Text = pieces[1], AutoSize = true, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                        button.Click += delegate { RunUiAction(delegate { profile.GetType().GetProperty(destination).SetValue(profile, profile.SaveFolder, null); profile.SaveFolder = ""; Store.Save(library); RefreshFolderRows(profile); }); };
                        classify.Controls.Add(button);
                    }
                    row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                    folderTable.Controls.Add(classify, 0, row); folderTable.SetColumnSpan(classify, 4);
                }
            }
            finally { folderTable.AutoSize = true; folderTable.ResumeLayout(true); emulatorInformation.ResumeLayout(true); host.ResumeLayout(true); }
        }
        private void AddFolderMessage(string text, int height)
        {
            int row = folderTable.RowCount++; folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var label = new Label { Text = text, Dock = DockStyle.Fill, ForeColor = subtle, Padding = new Padding(0, 2, 0, 2) };
            folderTable.Controls.Add(label, 0, row); folderTable.SetColumnSpan(label, 4);
        }
        private void AddFolderRow(EmulatorFolder location, EmulatorProfile profile, bool exists)
        {
            var path = location.Path; var property = location.Property == "Program" ? null : location.Property;
            int row = folderTable.RowCount; folderTable.RowCount += 3;
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            folderTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            var heading = new Label { Text = location.Label, Dock = DockStyle.Fill, ForeColor = ink, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            folderTable.Controls.Add(heading, 0, row); folderTable.SetColumnSpan(heading, 4);
            var value = new TextBox { Text = String.IsNullOrWhiteSpace(path) ? "No single folder detected" : path, ReadOnly = true, Dock = DockStyle.Fill, BackColor = surface, ForeColor = ink, BorderStyle = BorderStyle.FixedSingle };
            folderTable.Controls.Add(value, 0, row + 1);
            var open = new FishBowlActionButton { Text = "Open", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink, Enabled = exists };
            open.Click += delegate { RunUiAction(delegate { if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }); };
            folderTable.Controls.Add(open, 1, row + 1);
            var source = new Label { Text = location.Source + (!String.IsNullOrWhiteSpace(path) && !exists ? " • folder missing" : ""), Dock = DockStyle.Fill, ForeColor = subtle, AutoEllipsis = true };
            folderTable.Controls.Add(source, 0, row + 2); folderTable.SetColumnSpan(source, 4);
            if (property != null)
            {
                var choose = new FishBowlActionButton { Text = "Choose", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink };
                choose.Click += delegate { RunUiAction(delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the folder already used by " + profile.Name + " for " + location.Label.ToLowerInvariant(), SelectedPath = Directory.Exists(path) ? path : "", ShowNewFolderButton = false }) { if (dialog.ShowDialog(this) != DialogResult.OK) return; profile.GetType().GetProperty(property).SetValue(profile, dialog.SelectedPath, null); Store.Save(library); RefreshFolderRows(profile); } }); };
                folderTable.Controls.Add(choose, 2, row + 1);
                var auto = new FishBowlActionButton { Text = "Auto", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = ink, Enabled = !String.IsNullOrWhiteSpace(profile.GetType().GetProperty(property).GetValue(profile, null) as string) };
                auto.Click += delegate { RunUiAction(delegate { profile.GetType().GetProperty(property).SetValue(profile, "", null); Store.Save(library); RefreshFolderRows(profile); }); };
                folderTable.Controls.Add(auto, 3, row + 1);
            }
        }

        private Control BuildPrimaryActions()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = top, Padding = new Padding(8, 5, 8, 5), WrapContents = false };
            var add = new FishBowlActionButton();
            ConfigureButton(add, "Add emulator", AddEmulator, surface);
            ConfigureButton(editButton, "Edit emulator", EditEmulator, surface);
            ConfigureButton(removeButton, "Remove", RemoveEmulator, surface);
            ConfigureButton(managementButton, "Manage", delegate { ShowEmulatorManager("Setup checks"); }, surface);
            bar.Controls.Add(add); bar.Controls.Add(editButton); bar.Controls.Add(removeButton); bar.Controls.Add(managementButton);
            favoritesOnly.Text = "Favorites only"; favoritesOnly.ForeColor = ink; favoritesOnly.AutoSize = true; favoritesOnly.Margin = new Padding(16, 6, 12, 0);
            favoritesOnly.CheckedChanged += delegate { RunUiAction(RefreshHub); };
            bar.Controls.Add(favoritesOnly);
            platformFilter.DropDownStyle = ComboBoxStyle.DropDownList; platformFilter.Width = 240; platformFilter.Margin = new Padding(0, 3, 0, 0);
            platformFilter.BackColor = surface; platformFilter.ForeColor = ink;
            platformFilter.SelectedIndexChanged += delegate { if (!loadingFilters) RunUiAction(RefreshHub); };
            bar.Controls.Add(platformFilter);
            return bar;
        }

        private void ConfigureButton(Button button, string text, Action action, Color background)
        {
            button.Text = text; button.AutoSize = true; button.Height = 32;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            bool primary = text == "Add emulator"; button.BackColor = primary ? blue : background; button.ForeColor = primary || background == blue ? top : ink;
            button.Font = new Font(DisplayFont, 9, primary || background == blue ? FontStyle.Bold : FontStyle.Regular);
            button.Padding = new Padding(12, 0, 12, 0); button.Margin = new Padding(0, 0, 8, 0);
            button.Click += delegate { RunUiAction(action); };
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { Dock = DockStyle.Fill, BackColor = top, ForeColor = ink, Renderer = new FishBowlMenuRenderer(), ImageScalingSize = new Size(20,20) };
            var file = new ToolStripMenuItem("File");
            file.DropDownItems.Add(MenuAction("Add emulator...", "add", AddEmulator));
            file.DropDownItems.Add(MenuAction("Setup assistant / import emulator ZIP...", "add", delegate { ShowEmulatorManager("Setup assistant"); }));
            file.DropDownItems.Add(MenuAction("Game storage organizer...", "folder", delegate { ShowGameStorageOrganizer(null); }));
            file.DropDownItems.Add(MenuAction("Open selected emulator", "play", OpenSelectedEmulator));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(MenuAction("Exit", "power", Close));
            var tools = new ToolStripMenuItem("Tools");
            tools.DropDownItems.Add(MenuAction("Edit selected emulator...", "edit", EditEmulator));
            tools.DropDownItems.Add(MenuAction("Edit information and links...", "info", EditEmulatorInformation));
            tools.DropDownItems.Add(MenuAction("Favorite / unfavorite emulator", "star", ToggleEmulatorFavorite));
            tools.DropDownItems.Add(MenuAction("Open emulator folder", "folder", OpenEmulatorFolder));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(MenuAction("Export FishBowl settings...", "export", ExportLibrary));
            tools.DropDownItems.Add(MenuAction("Import FishBowl settings...", "import", ImportLibrary));
            tools.DropDownItems.Add(MenuAction("Create desktop shortcut", "desktop", CreateDesktopShortcut));
            tools.DropDownItems.Add(MenuAction("FishBowl settings...", "settings", ShowSettings));
            tools.DropDownItems.Add(MenuAction("Enable portable mode", "storage", EnablePortableMode));
            tools.DropDownItems.Add(MenuAction("Open FishBowl activity log", "info", delegate { if (!File.Exists(Store.LogFileName)) Store.Log("Activity log opened."); Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Store.LogFileName + "\"") { UseShellExecute = true }); }));
            tools.DropDownItems.Add(MenuAction("Diagnostics report...", "info", ShowDiagnostics));
            convertersMenu.DropDownOpening += delegate { RefreshConvertersMenu(); };
            var emulators = new ToolStripMenuItem("Emulators");
            emulators.DropDownItems.Add(MenuAction("Setup assistant / emulator folder...", "folder", delegate { ShowEmulatorManager("Setup assistant"); }));
            emulators.DropDownItems.Add(MenuAction("Find installed emulators...", "add", delegate { ShowEmulatorManager("Find installed"); }));
            emulators.DropDownItems.Add(MenuAction("Check releases / updates...", "refresh", delegate { ShowEmulatorManager("Updates"); }));
            emulators.DropDownItems.Add(MenuAction("Manage installed builds...", "storage", delegate { ShowEmulatorManager("Versions"); }));
            emulators.DropDownItems.Add(MenuAction("Emulator backups / restore...", "export", delegate { ShowEmulatorManager("Backups"); }));
            emulators.DropDownItems.Add(MenuAction("Setup checks...", "info", delegate { ShowEmulatorManager("Setup checks"); }));
            emulators.DropDownItems.Add(MenuAction("Official setup guidance...", "info", OpenOfficialSetupGuidance));
            emulators.DropDownItems.Add(MenuAction("Repair selected location...", "folder", RepairSelectedLocation));
            var view = new ToolStripMenuItem("View");
            view.DropDownItems.Add(MenuAction("Refresh emulators", "refresh", RefreshHub));
            view.DropDownItems.Add(MenuAction("Full screen (F11)", "desktop", ToggleFullScreen));
            linksMenu.DropDownOpening += delegate { RefreshWebsiteLinksMenu(); };
            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add(MenuAction("About FishBowl", "info", delegate { MessageBox.Show(this, "FishBowl opens your separately installed emulators.\n\nAdd games, manage libraries, and configure controls inside the dedicated emulator.", "FishBowl"); }));
            help.DropDownItems.Add(MenuAction("First-run guide...", "info", ShowFirstRunGuide));
            help.DropDownItems.Add(MenuAction("Game storage guide...", "folder", ShowGameStoragePrompt));
            foreach (var item in new[] { file, emulators, tools, view, convertersMenu, linksMenu, help }) { item.ForeColor = ink; item.DropDown.BackColor = bottom; item.DropDown.ForeColor = ink; menu.Items.Add(item); }
            return menu;
        }

        private EmulatorProfile CurrentEmulator() { return library.Emulators.FirstOrDefault(p => p.Id == selectedEmulatorId); }

        private void OpenOfficialSetupGuidance()
        {
            var profile = CurrentEmulator();
            if (profile == null) { MessageBox.Show(this, "Select an emulator first.", "FishBowl"); return; }
            var reference = EmulatorReference.For(profile);
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string requirements = reference.Requirements ?? "Review the emulator's official setup instructions for any required system files, graphics setup, and controller configuration.";
            string message = profile.Name + " setup guidance\n\n" + requirements + "\n\nFishBowl can open the emulator project's official guide. For firmware, BIOS, keys, or system software, use only files you are allowed to use and official instructions. FishBowl does not link to third-party downloads.\n\nOpen the official guide now?";
            if (MessageBox.Show(this, message, "Official setup guidance", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            if (!EmulatorReference.IsWebUrl(guide)) { MessageBox.Show(this, "No official setup guide is saved for this emulator. Use Edit information and links to add one.", "FishBowl"); return; }
            try { Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true }); Store.Log("Opened official setup guidance for " + profile.Name); }
            catch (Exception error) { MessageBox.Show(this, "The official setup guide could not be opened.\n\n" + error.Message, "FishBowl"); }
        }

        private void RefreshConvertersMenu()
        {
            convertersMenu.DropDownItems.Clear();
            convertersMenu.DropDownItems.Add(MenuAction("Prepare game for selected emulator...", "play", PrepareGameForSelectedEmulator));
            convertersMenu.DropDownItems.Add(MenuAction("View emulator format presets", "info", ShowFormatPresets));
            convertersMenu.DropDownItems.Add(new ToolStripSeparator());
            convertersMenu.DropDownItems.Add(MenuAction("Add converter for selected emulator...", "add", AddGameConverter));
            if (!library.Converters.Any()) { convertersMenu.DropDownItems.Add(new ToolStripMenuItem("No game converters added") { Enabled = false }); return; }
            convertersMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var tool in library.Converters.OrderBy(item => item.Name))
            {
                var saved = tool;
                var emulator = library.Emulators.FirstOrDefault(item => item.Id == saved.EmulatorId);
                convertersMenu.DropDownItems.Add(MenuAction((emulator == null ? "Unassigned" : emulator.Name) + " - " + saved.Name, "play", delegate { RunGameConverter(saved); }));
            }
        }

        private void ShowFormatPresets()
        {
            var rows = library.Emulators.OrderBy(item => item.Name).Select(item => item.Name + " | " + EmulatorReference.PlatformFor(item) + " | " + String.Join(", ", item.Extensions ?? new List<string>())).ToList();
            using (var dialog = new ResultsDialog("Emulator Format Presets", rows.Any() ? (IEnumerable<string>)rows : new[] { "Add an emulator to see its supported game formats." })) dialog.ShowDialog(this);
        }

        private void PrepareGameForSelectedEmulator()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { MessageBox.Show(this, "Select an emulator first.", "FishBowl"); return; }
            using (var choose = new OpenFileDialog { Title = "Choose a game archive or file for " + emulator.Name, Filter = "All files (*.*)|*.*" })
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                string extension = Path.GetExtension(choose.FileName).ToLowerInvariant();
                if (extension == ".zip") { ExtractZipForEmulator(emulator, choose.FileName); return; }
                var converter = library.Converters.FirstOrDefault(item => item.EmulatorId == emulator.Id && (item.InputExtensions ?? "").Split(',').Any(value => String.Equals(value.Trim().TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));
                if (converter == null)
                {
                    MessageBox.Show(this, "FishBowl has no registered extractor or converter for " + extension + " with " + emulator.Name + ".\n\nAdd one from Game converters, then try again.", "FishBowl");
                    return;
                }
                RunGameConverter(converter, choose.FileName);
            }
        }

        private void ExtractZipForEmulator(EmulatorProfile emulator, string archivePath)
        {
            try
            {
                string root = !String.IsNullOrWhiteSpace(emulator.ScanFolder) && Directory.Exists(emulator.ScanFolder) ? emulator.ScanFolder : Path.GetDirectoryName(archivePath);
                string destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath));
                int suffix = 2; while (Directory.Exists(destination)) destination = Path.Combine(root, Path.GetFileNameWithoutExtension(archivePath) + " (" + suffix++ + ")");
                using (var archive = System.IO.Compression.ZipFile.OpenRead(archivePath)) EmulatorInstaller.ValidateArchive(archive, destination, false);
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, destination);
                var supported = Directory.GetFiles(destination, "*.*", SearchOption.AllDirectories).Where(path => (emulator.Extensions ?? new List<string>()).Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).ToList();
                Store.Log("Extracted ZIP for " + emulator.Name + ": " + archivePath);
                using (var dialog = new ResultsDialog("Extraction Complete", supported.Any() ? supported.Select(path => "Compatible game: " + path) : new[] { "Extracted to: " + destination, "No files matching " + emulator.Name + "'s configured formats were found." })) dialog.ShowDialog(this);
            }
            catch (Exception error) { MessageBox.Show(this, "FishBowl could not extract that ZIP archive.\n\n" + error.Message, "FishBowl"); }
        }

        private void AddGameConverter()
        {
            var emulator = CurrentEmulator();
            if (emulator == null) { MessageBox.Show(this, "Select an emulator first, then add its converter.", "FishBowl"); return; }
            using (var name = new TextPromptDialog("Game Converter", "Converter name for " + emulator.Name))
            {
                if (name.ShowDialog(this) != DialogResult.OK || String.IsNullOrWhiteSpace(name.Value)) return;
                using (var choose = new OpenFileDialog { Title = "Choose converter program or script", Filter = "Programs and scripts (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*" })
                {
                    if (choose.ShowDialog(this) != DialogResult.OK) return;
                    using (var input = new TextPromptDialog("Game Converter", "Input file extensions, for example .zip, .rar, .bin"))
                    {
                        if (input.ShowDialog(this) != DialogResult.OK) return;
                        using (var output = new TextPromptDialog("Game Converter", "Output extension, for example .3ds (leave blank for extract-only tools)"))
                        {
                            if (output.ShowDialog(this) != DialogResult.OK) return;
                            using (var arguments = new TextPromptDialog("Game Converter", "Arguments - use {input} and {output}"))
                            {
                                arguments.Value = "{input} {output}";
                                if (arguments.ShowDialog(this) != DialogResult.OK) return;
                                library.Converters.Add(new GameConverter { Id = Guid.NewGuid().ToString("N"), EmulatorId = emulator.Id, Name = name.Value.Trim(), Program = choose.FileName, InputExtensions = input.Value.Trim(), OutputExtension = output.Value.Trim(), Arguments = arguments.Value.Trim() });
                                Store.Save(library); SetStatus("Added game converter for " + emulator.Name + ".");
                            }
                        }
                    }
                }
            }
        }

        private void RunGameConverter(GameConverter tool)
        {
            if (!File.Exists(tool.Program)) { MessageBox.Show(this, "The converter program cannot be found. Add it again from Game converters.", "FishBowl"); return; }
            var patterns = (tool.InputExtensions ?? "").Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).Select(item => "*" + (item.StartsWith(".") ? item : "." + item)).ToArray();
            string filter = patterns.Length == 0 ? "All files (*.*)|*.*" : "Supported input (" + String.Join(";", patterns) + ")|" + String.Join(";", patterns) + "|All files (*.*)|*.*";
            using (var choose = new OpenFileDialog { Title = "Choose a game file for " + tool.Name, Filter = filter })
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                RunGameConverter(tool, choose.FileName);
            }
        }

        private void RunGameConverter(GameConverter tool, string inputPath)
        {
            if (!File.Exists(tool.Program)) { MessageBox.Show(this, "The converter program cannot be found. Add it again from Game converters.", "FishBowl"); return; }
            string extension = tool.OutputExtension ?? ""; if (extension.Length > 0 && !extension.StartsWith(".")) extension = "." + extension;
            string output = extension.Length == 0 ? Path.GetDirectoryName(inputPath) : Path.ChangeExtension(inputPath, extension);
            string arguments = (tool.Arguments ?? "{input}").Replace("{input}", "\"" + inputPath.Replace("\"", "\\\"") + "\"").Replace("{output}", "\"" + output.Replace("\"", "\\\"") + "\"");
            try { Process.Start(new ProcessStartInfo { FileName = tool.Program, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(tool.Program), UseShellExecute = true }); Store.Log("Ran game converter: " + tool.Name); SetStatus("Started " + tool.Name + "."); }
            catch (Exception error) { MessageBox.Show(this, "The converter could not be started.\n\n" + error.Message, "FishBowl"); }
        }
        private void SetStatus(string text) { status.Text = text; }
        private void InvalidateEmulatorRow(string id)
        {
            if (id == null) return;
            foreach (ListViewItem row in emulatorList.Items) if (row.Tag as string == id) { emulatorList.Invalidate(row.Bounds); return; }
        }

        private void ApplyEmulatorSelection()
        {
            var next = emulatorList.SelectedItems.Count == 0 ? null : emulatorList.SelectedItems[0].Tag as string;
            if (next == selectedEmulatorId) return;
            SaveProfileNotes(); selectedEmulatorId = next; UpdateActions();
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
        }

        private void ClearEmulatorSelection()
        {
            SaveProfileNotes(); refreshing = true;
            try { foreach (ListViewItem row in emulatorList.Items) row.Selected = false; selectedEmulatorId = null; }
            finally { refreshing = false; UpdateActions(); emulatorList.Invalidate(); }
            SetStatus("Select an emulator to see its information. Double-click to open.");
        }
        private void UpdateActions() { var selected = CurrentEmulator() != null; editButton.Enabled = selected; removeButton.Enabled = selected; managementButton.Enabled = selected; UpdateInformationPanel(); }
        private void RefreshHub()
        {
            SaveProfileNotes();
            RefreshPlatformOptions();
            refreshing = true;
            emulatorList.BeginUpdate();
            try
            {
                emulatorList.Items.Clear(); emulatorImages.Images.Clear();
                emulatorImageSources.Clear();
                if (reloadProgramMetadata) { reloadProgramMetadata = false; metadataGeneration++; foreach (var image in emulatorIconCache.Values) image.Dispose(); emulatorIconCache.Clear(); versionCache.Clear(); }
                PruneVisualCaches();
                var query = filterBox.Text.Trim();
                var selectedPlatform = platformFilter.SelectedItem as string ?? "All platforms";
                var items = library.Emulators.Where(p => !favoritesOnly.Checked || p.Favorite)
                    .Where(p => selectedPlatform == "All platforms" || EmulatorReference.PlatformTags(p).Contains(selectedPlatform, StringComparer.OrdinalIgnoreCase))
                    .Where(p => query.Length == 0 || (p.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || EmulatorReference.PlatformFor(p).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (p.Notes ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(p => p.Favorite).ThenBy(p => p.Name).ToList();
                foreach (var p in items)
                {
                    var image = CachedEmulatorImage(p);
                    emulatorImageSources.Add(image); emulatorImages.Images.Add(image);
                    var item = new ListViewItem((p.Favorite ? "★ " : "") + (p.Name ?? "Emulator"), emulatorImages.Images.Count - 1) { Tag = p.Id };
                    item.SubItems.Add(File.Exists(p.Executable) ? "Ready" : "Missing program");
                    item.SubItems.Add(EmulatorReference.PlatformFor(p));
                    item.SubItems.Add(Path.GetFileName(p.Executable ?? ""));
                    item.Selected = p.Id == selectedEmulatorId;
                    emulatorList.Items.Add(item);
                }
                if (!items.Any(p => p.Id == selectedEmulatorId)) selectedEmulatorId = null;
                if (!items.Any(p => p.Id == hoveredEmulatorId)) hoveredEmulatorId = null;
                foreach (ListViewItem item in emulatorList.Items) item.Selected = (item.Tag as string) == selectedEmulatorId;
                if (items.Count == 0) SetStatus(library.Emulators.Count == 0 ? "Click Add emulator to get started." : "No emulators match this filter.");
            }
            finally { emulatorList.EndUpdate(); refreshing = false; UpdateActions(); }
            var current = CurrentEmulator();
            if (current != null) SetStatus("Selected " + current.Name + ". Double-click to open.");
        }
        private void ResizeEmulatorColumns()
        {
            if (emulatorList.Columns.Count != 4) return;
            var width = Math.Max(1, emulatorList.ClientSize.Width - 4);
            emulatorList.Columns[0].Width = (int)(width * 0.36);
            emulatorList.Columns[1].Width = (int)(width * 0.18);
            emulatorList.Columns[2].Width = (int)(width * 0.29);
            emulatorList.Columns[3].Width = width - emulatorList.Columns[0].Width - emulatorList.Columns[1].Width - emulatorList.Columns[2].Width;
            if (emulatorList.IsHandleCreated) emulatorList.AutoResizeColumn(3, ColumnHeaderAutoResizeStyle.HeaderSize);
        }
        private ProcessStartInfo EmulatorStartInfo(EmulatorProfile profile)
        {
            return new ProcessStartInfo { FileName = profile.Executable, Arguments = "", WorkingDirectory = Path.GetDirectoryName(profile.Executable), UseShellExecute = true };
        }
        private void OpenSelectedEmulator()
        {
            var profile = CurrentEmulator();
            if (profile == null) { SetStatus("Select an emulator first."); return; }
            if (!File.Exists(profile.Executable)) { RepairSelectedLocation(); if (!File.Exists(profile.Executable)) return; }
            if (EmulatorRuntime.State(profile.Executable) == RuntimeState.Running) { EmulatorRuntime.BringForward(profile.Executable); SetStatus(profile.Name + " is already running."); return; }
            using (var process = Process.Start(EmulatorStartInfo(profile))) { }
            RefreshRuntimeStatus();
            SetStatus("Opened " + profile.Name + ". Add and launch games inside the emulator.");
        }
        private void AddEmulator() { AddEmulatorWithPath(null); }
        private void AddEmulatorWithPath(string path)
        {
            using (var dialog = new EmulatorDialog(null, path))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var p = dialog.Profile; p.Id = Guid.NewGuid().ToString("N");
                BuildRegistry.Remember(p, p.Executable, "Initial installation");
                library.Emulators.Add(p); selectedEmulatorId = p.Id;
                Store.Save(library); filterBox.Clear(); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Added " + p.Name + ".");
                ShowFirmwareSetupNotice(p);
            }
        }

        private void ShowFirmwareSetupNotice(EmulatorProfile profile)
        {
            var reference = EmulatorReference.For(profile);
            var requirements = reference.Requirements ?? "";
            bool requiresSetup = requirements.IndexOf("BIOS", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("firmware", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 || requirements.IndexOf("system software", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!requiresSetup) return;
            string guide = EmulatorReference.IsWebUrl(reference.Documentation) ? reference.Documentation : reference.Website;
            string message = profile.Name + " requires additional setup before it can run some or all games.\n\nWHAT THIS EMULATOR REQUIRES\n" + requirements + "\n\nWHERE TO CONFIGURE IT\nIn FishBowl, select " + profile.Name + " and open Emulators > Setup checks. Choose the firmware folder you already use, then follow the emulator's own setup screen.\n\nFishBowl does not include, download, or bypass BIOS, firmware, system software, or keys. Use files you are allowed to use and the emulator's official instructions.\n\nOpen the official setup guide now?";
            if (MessageBox.Show(this, message, "Setup required", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                if (EmulatorReference.IsWebUrl(guide))
                {
                    try { Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true }); }
                    catch (Exception error) { MessageBox.Show(this, "The official setup guide could not be opened.\n\n" + error.Message, "FishBowl"); }
                }
                else MessageBox.Show(this, "This emulator does not have a setup link saved yet. Open Edit information and links to add its official guide.", "FishBowl");
            }
        }
        private void EditEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            var previousExecutable = current.Executable; var previousVersion = current.ManualVersion;
            using (var dialog = new EmulatorDialog(current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                dialog.Profile.Id = current.Id;
                if (!HubPaths.Same(previousExecutable, dialog.Profile.Executable))
                {
                    var previous = new EmulatorProfile { Executable = previousExecutable, ManualVersion = previousVersion, Builds = dialog.Profile.Builds ?? new List<EmulatorBuild>() };
                    BuildRegistry.Remember(previous, previousExecutable, "Previous installation"); dialog.Profile.Builds = previous.Builds;
                    dialog.Profile.ManualVersion = ""; BuildRegistry.Remember(dialog.Profile, dialog.Profile.Executable, "Edited location");
                }
                library.Emulators[library.Emulators.IndexOf(current)] = dialog.Profile;
                reloadProgramMetadata = true; Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Updated " + dialog.Profile.Name + ".");
            }
        }
        private void RemoveEmulator()
        {
            var current = CurrentEmulator(); if (current == null) return;
            if (MessageBox.Show(this, "Remove " + current.Name + " from FishBowl?\n\nIts emulator program, games, and saves will stay where they are.", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            library.Emulators.Remove(current);
            selectedEmulatorId = null;
            Store.Save(library); RefreshHub(); ConfigureGameFolderWatchers(); SetStatus("Emulator removed from FishBowl.");
        }
        private void HandleDroppedPaths(string[] paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
                if (File.Exists(path) && EmulatorReference.IsLaunchFile(path))
                {
                    if (library.Emulators.Any(p => String.Equals(p.Executable, path, StringComparison.OrdinalIgnoreCase))) { SetStatus("That emulator is already in FishBowl."); continue; }
                    AddEmulatorWithPath(path);
                }
                else SetStatus("Add games inside the emulator. Drop an emulator program or shortcut here to add it to FishBowl.");
        }
        private void OpenEmulatorFolder() { var p = CurrentEmulator(); if (p != null && Directory.Exists(Path.GetDirectoryName(p.Executable))) Process.Start(new ProcessStartInfo(Path.GetDirectoryName(p.Executable)) { UseShellExecute = true }); }
        private void ExportLibrary()
        {
            using (var dialog = new SaveFileDialog { Title = "Export FishBowl settings", Filter = "JSON files (*.json)|*.json", FileName = "FishBowl-settings.json" })
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; Store.Save(library); File.Copy(Store.FileName, dialog.FileName, true); SetStatus("Exported FishBowl settings."); }
        }
        private void ImportLibrary()
        {
            using (var dialog = new OpenFileDialog { Title = "Import FishBowl settings", Filter = "JSON files (*.json)|*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var imported = new JavaScriptSerializer().Deserialize<LibraryData>(File.ReadAllText(dialog.FileName));
                if (imported == null || imported.Emulators == null) throw new InvalidDataException("That file does not contain FishBowl emulator settings.");
                if (MessageBox.Show(this, "Replace your FishBowl settings with this backup?", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Store.Save(library); File.Copy(Store.FileName, Store.FileName + ".before-import.json", true);
                library.Emulators = imported.Emulators; library.Games = imported.Games ?? new List<GameEntry>();
                library.Collections = imported.Collections ?? new List<GameCollection>(); library.Links = imported.Links ?? new List<WebsiteLink>();
                library.Theme = imported.Theme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7 }; library.BackupFolder = imported.BackupFolder; library.EmulatorRootDirectory = imported.EmulatorRootDirectory;
                selectedEmulatorId = null;
                Store.Save(library); filterBox.Clear(); RefreshHub(); SetStatus("Imported FishBowl settings. Reopen FishBowl to apply the theme.");
            }
        }
        private void ShowGameLibrary()
        {
            using (var dialog = new GameLibraryDialog(library))
            {
                dialog.ShowDialog(this);
                Store.Save(library);
                SetStatus("Game library updated. " + library.Games.Count + " game" + (library.Games.Count == 1 ? "" : "s") + " saved.");
            }
        }
        private void ShowFirstRunGuide()
        {
            using (var dialog = new StartupAssistantDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.ShowStartupAssistant = !dialog.DontShowAgain;
                library.Theme.StartupAssistantPreferenceSet = true;
                Store.Save(library);
                if (dialog.OpenSetupAssistant) ShowEmulatorManager("Setup assistant");
            }
        }
        private void ShowGameStoragePrompt()
        {
            using (var dialog = new GameStoragePromptDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Theme.ShowGameStorageAssistant = !dialog.DontShowAgain;
                library.Theme.GameStorageAssistantPreferenceSet = true;
                Store.Save(library);
                if (dialog.OpenOrganizer) ShowGameStorageOrganizer(null);
            }
        }
        private void ShowGameStorageOrganizer(IEnumerable<string> droppedPaths)
        {
            using (var dialog = new GameStorageOrganizerDialog(library, droppedPaths))
            {
                dialog.ShowDialog(this);
                Store.Save(library);
            }
        }
        private void ShowDiagnostics()
        {
            var rows = new List<string>();
            rows.Add("FishBowl data: " + Store.DataDirectory);
            rows.Add("Settings file: " + (File.Exists(Store.FileName) ? "Available" : "Not yet created"));
            rows.Add("Emulators: " + library.Emulators.Count + "    Games: " + library.Games.Count + "    Collections: " + library.Collections.Count);
            foreach (var emulator in library.Emulators) rows.Add((File.Exists(emulator.Executable) ? "OK  " : "MISSING  ") + emulator.Name + " — " + emulator.Executable);
            foreach (var game in library.Games.Where(item => !File.Exists(item.Path)).Take(20)) rows.Add("MISSING GAME  " + game.Title + " — " + game.Path);
            try { var root = Path.GetPathRoot(HubPaths.BackupRoot(library)); var drive = new DriveInfo(root); rows.Add("Backup drive free space: " + (drive.AvailableFreeSpace / 1024 / 1024 / 1024) + " GB"); } catch { rows.Add("Backup drive free space: unavailable"); }
            rows.Add("Activity and crash report: " + Store.LogFileName);
            using (var dialog = new ResultsDialog("FishBowl Diagnostics", rows)) dialog.ShowDialog(this);
        }
        private void ConfigureGameFolderWatchers()
        {
            foreach (var watcher in gameFolderWatchers) watcher.Dispose();
            gameFolderWatchers.Clear();
            if (!library.Theme.AutoSyncGameFolders) return;
            foreach (var emulator in library.Emulators.Where(item => Directory.Exists(item.ScanFolder)))
            {
                try
                {
                    var watcher = new FileSystemWatcher(emulator.ScanFolder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
                    FileSystemEventHandler changed = delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { gameFolderTimer.Stop(); gameFolderTimer.Start(); }); };
                    watcher.Created += changed; watcher.Deleted += changed; watcher.Renamed += delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { gameFolderTimer.Stop(); gameFolderTimer.Start(); }); };
                    gameFolderWatchers.Add(watcher);
                }
                catch (Exception error) { Store.Log("Game folder watch unavailable: " + error.Message); }
            }
        }
        private void SyncWatchedGameFolders()
        {
            int added = GameLibraryCatalog.Sync(library);
            if (added > 0) { Store.Save(library); SetStatus("Added " + added + " newly detected game" + (added == 1 ? "." : "s.") + " Open Game library to view them."); }
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.E) { RunUiAction(AddEmulator); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { filterBox.Focus(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.G) { ShowGameStorageOrganizer(null); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F11) { ToggleFullScreen(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { if (fullScreen) ToggleFullScreen(); else if (filterBox.TextLength > 0) filterBox.Clear(); else ClearEmulatorSelection(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter && emulatorList.ContainsFocus) { RunUiAction(OpenSelectedEmulator); e.SuppressKeyPress = true; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !visualResourcesDisposed) { visualResourcesDisposed = true; runtimeTimer.Dispose(); notesTimer.Dispose(); gameFolderTimer.Dispose(); foreach (var watcher in gameFolderWatchers) watcher.Dispose(); emulatorImages.Dispose(); foreach (var image in emulatorIconCache.Values) image.Dispose(); emulatorIconCache.Clear(); emulatorImageSources.Clear(); Icon = null; if (ownedAppIcon != null) ownedAppIcon.Dispose(); }
            base.Dispose(disposing);
        }
        private void RunUiAction(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                Store.Log("Action failed: " + error);
                MessageBox.Show(this, "FishBowl could not complete this action.\n\n" + error.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private ToolStripMenuItem MenuAction(string text, string icon, Action action)
        {
            var item = new ToolStripMenuItem(text) { Image = MakeUiIcon(icon), ImageScaling = ToolStripItemImageScaling.SizeToFit };
            item.Click += delegate { RunUiAction(action); };
            var ownedMenuIcon = item.Image; item.Disposed += delegate { item.Image = null; if (ownedMenuIcon != null) ownedMenuIcon.Dispose(); };
            return item;
        }

        private static Image MakeUiIcon(string kind) { return FishBowlVisuals.Icon(kind,32,Color.FromArgb(224,208,255),Color.FromArgb(255,180,105)); }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ApplyThemeColors()
        {
            var theme = library.Theme == null ? "Twilight" : library.Theme.Name;
            if (theme == "Lavender")
            {
                ink = Color.FromArgb(44, 37, 54); top = Color.FromArgb(242, 237, 248); bottom = Color.FromArgb(231, 222, 240); surface = Color.FromArgb(255, 250, 255); subtle = Color.FromArgb(105, 91, 119); blue = Color.FromArgb(137, 80, 215); pink = Color.FromArgb(236, 128, 65);
            }
            else if (theme == "Light")
            {
                ink = Color.FromArgb(42, 43, 50); top = Color.FromArgb(246, 246, 249); bottom = Color.FromArgb(235, 236, 241); surface = Color.FromArgb(255, 255, 255); subtle = Color.FromArgb(101, 103, 115); blue = Color.FromArgb(103, 82, 171); pink = Color.FromArgb(203, 113, 61);
            }
            else if (theme == "High Contrast")
            {
                ink = Color.White; top = Color.Black; bottom = Color.FromArgb(17, 17, 17); surface = Color.FromArgb(30, 30, 30); subtle = Color.FromArgb(224, 224, 224); blue = Color.FromArgb(112, 191, 255); pink = Color.FromArgb(255, 220, 77);
            }
            else if (theme == "Ember")
            {
                ink = Color.FromArgb(255, 248, 240); top = Color.FromArgb(47, 28, 25); bottom = Color.FromArgb(64, 38, 32); surface = Color.FromArgb(84, 50, 40); subtle = Color.FromArgb(229, 191, 174); blue = Color.FromArgb(218, 107, 174); pink = Color.FromArgb(255, 176, 72);
            }
            else if (theme == "Midnight")
            {
                ink = Color.FromArgb(232, 237, 250); top = Color.FromArgb(16, 20, 33); bottom = Color.FromArgb(21, 27, 43); surface = Color.FromArgb(35, 43, 63); subtle = Color.FromArgb(159, 173, 201); blue = Color.FromArgb(111, 142, 255); pink = Color.FromArgb(121, 211, 255);
            }
            else if (theme == "Forest")
            {
                ink = Color.FromArgb(235, 246, 238); top = Color.FromArgb(27, 46, 38); bottom = Color.FromArgb(34, 58, 47); surface = Color.FromArgb(48, 76, 62); subtle = Color.FromArgb(177, 206, 187); blue = Color.FromArgb(111, 202, 150); pink = Color.FromArgb(206, 211, 109);
            }
            else if (theme == "Rosewood")
            {
                ink = Color.FromArgb(250, 239, 243); top = Color.FromArgb(52, 30, 40); bottom = Color.FromArgb(67, 38, 52); surface = Color.FromArgb(87, 51, 67); subtle = Color.FromArgb(225, 184, 199); blue = Color.FromArgb(226, 110, 166); pink = Color.FromArgb(255, 169, 116);
            }
            else if (theme == "Mist")
            {
                ink = Color.FromArgb(38, 46, 56); top = Color.FromArgb(237, 243, 248); bottom = Color.FromArgb(222, 231, 239); surface = Color.FromArgb(251, 253, 255); subtle = Color.FromArgb(92, 109, 125); blue = Color.FromArgb(69, 135, 191); pink = Color.FromArgb(200, 113, 116);
            }
            else
            {
                ink = Color.FromArgb(239, 239, 243); top = Color.FromArgb(31, 32, 37); bottom = Color.FromArgb(35, 36, 42); surface = Color.FromArgb(47, 48, 56); subtle = Color.FromArgb(174, 176, 186); blue = Color.FromArgb(183, 150, 245); pink = Color.FromArgb(235, 158, 94);
            }
            var accent = library.Theme.AccentColor ?? "Sunset";
            if (accent == "Ocean") { blue = Color.FromArgb(89, 190, 255); pink = Color.FromArgb(67, 220, 187); }
            else if (accent == "Rose") { blue = Color.FromArgb(228, 100, 181); pink = Color.FromArgb(255, 157, 101); }
            else if (accent == "Lime") { blue = Color.FromArgb(155, 213, 98); pink = Color.FromArgb(246, 210, 92); }
            else if (accent == "Amethyst") { blue = Color.FromArgb(177, 122, 255); pink = Color.FromArgb(222, 109, 244); }
            else if (accent == "Gold") { blue = Color.FromArgb(236, 178, 62); pink = Color.FromArgb(255, 219, 122); }
            else if (accent == "Ice") { blue = Color.FromArgb(103, 198, 232); pink = Color.FromArgb(173, 235, 255); }
            swatches = new[] { blue, pink, Color.FromArgb((blue.R + pink.R) / 2, (blue.G + pink.G) / 2, (blue.B + pink.B) / 2) };
        }

        private void ApplyVisualScale()
        {
            int percent = Math.Max(85, Math.Min(125, library.Theme.UiScalePercent == 0 ? 100 : library.Theme.UiScalePercent));
            if (percent != 100) Scale(new SizeF(percent / 100f, percent / 100f));
        }

        private void ToggleFullScreen()
        {
            if (!fullScreen)
            {
                savedBorderStyle = FormBorderStyle;
                savedWindowState = WindowState;
                savedBounds = Bounds;
                savedNormalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;
                fullScreen = true;
                SetStatus("Full-screen mode. Press F11 to return.");
            }
            else
            {
                FormBorderStyle = savedBorderStyle;
                WindowState = FormWindowState.Normal;
                Bounds = savedBounds;
                WindowState = savedWindowState;
                fullScreen = false;
                SetStatus("Returned to the standard window.");
            }
        }

        private void RestoreWindowLayout()
        {
            try
            {
                if (!File.Exists(WindowLayoutFile)) return;
                var values = new JavaScriptSerializer().Deserialize<int[]>(File.ReadAllText(WindowLayoutFile));
                if (values == null || values.Length != 5) return;
                var bounds = new Rectangle(values[0], values[1], Math.Max(1100, values[2]), Math.Max(700, values[3]));
                var screen = Screen.AllScreens.FirstOrDefault(item => item.WorkingArea.IntersectsWith(bounds));
                if (screen == null) screen = Screen.PrimaryScreen;
                var area = screen.WorkingArea;
                bounds.Width = Math.Min(bounds.Width, area.Width);
                bounds.Height = Math.Min(bounds.Height, area.Height);
                bounds.X = Math.Max(area.Left, Math.Min(bounds.X, area.Right - bounds.Width));
                bounds.Y = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - bounds.Height));
                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
                WindowState = values[4] == 1 ? FormWindowState.Maximized : FormWindowState.Normal;
            }
            catch (Exception error) { Store.Log("Window layout could not be restored: " + error.Message); }
        }

        private void SaveWindowLayout()
        {
            try
            {
                var state = fullScreen ? savedWindowState : WindowState;
                var bounds = fullScreen ? savedNormalBounds : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
                Directory.CreateDirectory(Store.DataDirectory);
                File.WriteAllText(WindowLayoutFile, new JavaScriptSerializer().Serialize(new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height, state == FormWindowState.Maximized ? 1 : 0 }));
            }
            catch (Exception error) { Store.Log("Window layout could not be saved: " + error.Message); }
        }

        private static Image LoadLogo()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.png"))
            using (var source = stream == null ? null : Image.FromStream(stream))
                return source == null ? null : new Bitmap(source);
        }

        private static Icon LoadAppIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.ico"))
                return stream == null ? null : new Icon(stream);
        }

        private string EmulatorIconKey(EmulatorProfile profile)
        {
            return new JavaScriptSerializer().Serialize(new[] { profile.Id, profile.Executable, profile.IconPath, profile.Name });
        }

        private Image CachedEmulatorImage(EmulatorProfile profile)
        {
            var key = EmulatorIconKey(profile); Image image;
            if (!emulatorIconCache.TryGetValue(key, out image))
            {
                var color = swatches[Math.Abs((profile.Id ?? profile.Name ?? "?").GetHashCode() % swatches.Length)];
                image = GetEmulatorImage(profile) ?? MakeFallbackIcon(profile.Name ?? "?", color); emulatorIconCache[key] = image;
            }
            return image;
        }

        private void PruneVisualCaches()
        {
            var liveKeys = new HashSet<string>(library.Emulators.Select(EmulatorIconKey));
            foreach (var key in emulatorIconCache.Keys.Where(k => !liveKeys.Contains(k)).ToArray()) { emulatorIconCache[key].Dispose(); emulatorIconCache.Remove(key); }
            var livePrograms = new HashSet<string>(library.Emulators.Select(p => p.Executable ?? ""));
            foreach (var key in versionCache.Keys.Where(k => !livePrograms.Contains(k)).ToArray()) versionCache.Remove(key);
        }

        private static Image GetEmulatorImage(EmulatorProfile emulator)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(emulator.IconPath) && File.Exists(emulator.IconPath))
                {
                    using (var source = Image.FromFile(emulator.IconPath)) return new Bitmap(source);
                }
                if (File.Exists(emulator.Executable))
                {
                    using (var icon = Icon.ExtractAssociatedIcon(emulator.Executable))
                        return icon == null ? null : icon.ToBitmap();
                }
            }
            catch { }
            return null;
        }

        private static Image MakeFallbackIcon(string name, Color color)
        {
            var image=new Bitmap(48,48);using(var g=Graphics.FromImage(image)){g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var p=FishBowlVisuals.Round(new RectangleF(4,4,40,40),8))using(var b=new SolidBrush(Color.FromArgb(42,color)))using(var pen=new Pen(Color.FromArgb(150,color),1.5f)){g.FillPath(b,p);g.DrawPath(pen,p);}
                var words=(name??"?").Split(new[]{' ','-','_'},StringSplitOptions.RemoveEmptyEntries);var initials=String.Concat(words.Take(2).Select(w=>w.Substring(0,1).ToUpperInvariant()));
                using(var font=new Font("Bahnschrift",16,FontStyle.Bold))TextRenderer.DrawText(g,initials.Length==0?"?":initials,font,new Rectangle(3,3,42,42),Color.FromArgb(242,232,255),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }return image;
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                string appPath = Application.ExecutablePath;
                string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FishBowl.lnk");
                string iconPath = Path.Combine(Path.GetDirectoryName(appPath), "FishBowl.ico");
                if (!File.Exists(iconPath)) iconPath = appPath;
                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                object shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { appPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(appPath) });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "FishBowl Emulator Hub" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                MessageBox.Show("A FishBowl shortcut was added to your desktop.", "FishBowl");
            }
            catch (Exception exception)
            {
                MessageBox.Show("FishBowl could not create the desktop shortcut.\n\n" + exception.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshWebsiteLinksMenu()
        {
            linksMenu.DropDownItems.Clear();
            linksMenu.DropDownItems.Add("Add website link...", null, delegate { AddWebsiteLink(); });
            if (!library.Links.Any())
            {
                linksMenu.DropDownItems.Add(new ToolStripMenuItem("No saved links") { Enabled = false });
                return;
            }
            linksMenu.DropDownItems.Add("Remove website link...", null, delegate { RemoveWebsiteLink(); });
            linksMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var link in library.Links.OrderBy(item => item.Name))
            {
                var savedLink = link;
                linksMenu.DropDownItems.Add(savedLink.Name, null, delegate { OpenWebsiteLink(savedLink); });
            }
        }

        private void AddWebsiteLink()
        {
            using (var dialog = new WebsiteLinkDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                library.Links.Add(dialog.Link);
                Store.Save(library);
                SetStatus("Saved website link: " + dialog.Link.Name + ".");
            }
        }

        private void RemoveWebsiteLink()
        {
            if (!library.Links.Any()) return;
            using (var dialog = new WebsiteLinkPicker(library.Links))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedLink == null) return;
                library.Links.RemoveAll(link => link.Id == dialog.SelectedLink.Id);
                Store.Save(library);
                SetStatus("Removed website link: " + dialog.SelectedLink.Name + ".");
            }
        }

        private void OpenWebsiteLink(WebsiteLink link)
        {
            Uri address;
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("This saved link is not a valid http or https address.", "FishBowl");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = address.AbsoluteUri, UseShellExecute = true });
                SetStatus("Opening " + link.Name + " in your browser.");
            }
            catch (Exception error) { MessageBox.Show("The website could not be opened:\n" + error.Message, "FishBowl"); }
        }

        private void ShowSettings()
        {
            using (var dialog = new SettingsDialog(library.Theme, library.BackupFolder))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                bool restartRequired = !String.Equals(library.Theme.Name, dialog.Theme.Name, StringComparison.OrdinalIgnoreCase) || library.Theme.StartMaximized != dialog.Theme.StartMaximized || !String.Equals(library.Theme.AccentColor, dialog.Theme.AccentColor, StringComparison.OrdinalIgnoreCase) || !String.Equals(library.Theme.FontFamily, dialog.Theme.FontFamily, StringComparison.OrdinalIgnoreCase) || library.Theme.UiScalePercent != dialog.Theme.UiScalePercent || !String.Equals(library.Theme.ListDensity, dialog.Theme.ListDensity, StringComparison.OrdinalIgnoreCase) || library.Theme.ShowBanner != dialog.Theme.ShowBanner || library.Theme.ShowStatusBar != dialog.Theme.ShowStatusBar || library.Theme.ShowInformationPanel != dialog.Theme.ShowInformationPanel || library.Theme.ShowEmulatorIcons != dialog.Theme.ShowEmulatorIcons || library.Theme.EnableMotion != dialog.Theme.EnableMotion;
                library.Theme = dialog.Theme;
                library.BackupFolder = dialog.BackupFolder;
                Store.Save(library);
                ConfigureGameFolderWatchers();
                if (restartRequired && MessageBox.Show(this, "These settings need FishBowl to restart before they can take effect.\n\nRestart FishBowl now?", "Restart FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Store.Log("Restarting to apply settings.");
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath) { WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = true });
                    Close();
                }
                else if (restartRequired) MessageBox.Show("Settings saved. Restart FishBowl whenever you are ready to apply them.", "FishBowl");
                else SetStatus("Settings saved and applied.");
            }
        }

        private void EnablePortableMode()
        {
            if (Store.PortableMode) { MessageBox.Show("FishBowl is already using portable storage beside the app.", "FishBowl"); return; }
            Store.EnablePortableMode(library);
            MessageBox.Show("Portable mode is ready. Your library is now stored beside FishBowl in the FishBowlData folder.", "FishBowl");
        }


        private string WindowLayoutFile { get { return Path.Combine(Store.DataDirectory, "window-layout.json"); } }
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
        private readonly List<EmulatorFolder> folders = new List<EmulatorFolder>();
        private string program;
        public string Notice { get; private set; }
        public EmulatorFolderDetector() : this(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), true) { }
        public EmulatorFolderDetector(string roamingPath, string localPath, string documentsPath, bool registry)
        { roaming = roamingPath; local = localPath; documents = documentsPath; readRegistry = registry; }
        public List<EmulatorFolder> Detect(EmulatorProfile profile)
        {
            folders.Clear(); Notice = ""; program = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                program = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(profile.Executable));
                Add("Program", "Program folder", program, "Emulator location");
                if (!System.IO.Path.GetExtension(profile.Executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
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
            if (exe == "azahar" && name == "Custom")
                Notice = "Select the Azahar Plus preset to detect this installation's 3DS storage.";
            string data, config;
            Dictionary<string, string> settings;
            switch (name)
            {
                case "DeSmuME":
                    config = P(program, "desmume.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", program, "desmume.ini");
                    Configured(settings, "PathSettings/Battery", "InGameSaveFolder", "In-game saves (Battery)", program, "Battery", config);
                    Configured(settings, "PathSettings/StateSlots", "SaveStateFolder", "Save states (slots)", program, "StateSlots", config);
                    Configured(settings, "PathSettings/States", "SaveStateFolder", "Save states (manual files)", program, "States", config);
                    Configured(settings, "PathSettings/Screenshots", "ScreenshotFolder", "Screenshots", program, "Screenshots", config);
                    break;
                case "Dolphin":
                    data = DolphinRoot(); config = P(data, "Config", "Dolphin.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", P(data, "Config"), "Dolphin user directory");
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
                    data = Directory.Exists(P(program, "user")) ? P(program, "user") : P(roaming, "Azahar");
                    config = P(data, "config", "qt-config.ini"); settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", P(data, "config"), "Azahar Plus user directory");
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
                    data = File.Exists(P(program, "portable.txt")) || File.Exists(P(program, "portable.ini")) ? program : P(documents, "PCSX2");
                    config = P(data, "inis", "PCSX2.ini"); settings = ReadSettings(config, false);
                    Add("ConfigFolder", "Configuration", P(data, "inis"), "PCSX2 data directory");
                    Configured(settings, "Folders/MemoryCards", "InGameSaveFolder", "In-game saves (memory cards)", data, "memcards", config);
                    Configured(settings, "Folders/Savestates", "SaveStateFolder", "Save states", data, "sstates", config);
                    Configured(settings, "Folders/Snapshots", "ScreenshotFolder", "Screenshots", data, "snaps", config);
                    Configured(settings, "Folders/Logs", "LogFolder", "Logs", data, "logs", config);
                    break;
                case "DuckStation":
                    data = File.Exists(P(program, "portable.txt")) ? program :
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
                    data = FindConfigRoot(new[] { program, P(roaming, "melonDS") }, new[] { "melonDS.toml", "melonDS.ini" });
                    if (data == null) { Notice = "Open melonDS once to create its configuration, then refresh folders."; break; }
                    config = File.Exists(P(data, "melonDS.toml")) ? P(data, "melonDS.toml") : P(data, "melonDS.ini");
                    settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", data, System.IO.Path.GetFileName(config));
                    Configured(settings, FirstKey(settings, "Instance0/SaveFilePath", "SaveFilePath"), "InGameSaveFolder", "In-game saves", program, "", config);
                    Configured(settings, FirstKey(settings, "Instance0/SavestatePath", "SavestatePath"), "SaveStateFolder", "Save states", program, "", config);
                    Notice = "melonDS instance 0 paths are shown. Empty save paths store saves beside each ROM; no single hub folder applies.";
                    break;
                case "Cemu":
                    data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : File.Exists(P(program, "settings.xml")) ? program : P(roaming, "Cemu");
                    config = P(data, "settings.xml"); Add("ConfigFolder", "Configuration", data, "Cemu user directory");
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
                    config = P(program, "config.yml"); settings = ReadYaml(config);
                    Add("ConfigFolder", "Configuration", program, "config.yml");
                    var pref = Get(settings, "pref-path");
                    data = String.IsNullOrWhiteSpace(pref) ? P(roaming, "Vita3K", "Vita3K") : Resolve(pref, program);
                    var user = Get(settings, "user-id"); if (String.IsNullOrWhiteSpace(user)) user = "00";
                    var users = P(data, "ux0", "user");
                    var userDirs = Directory.Exists(users) ? Directory.GetDirectories(users).Take(64).ToArray() : new[] { P(users, user) };
                    foreach (var u in userDirs) Add("InGameSaveFolder", "In-game saves (Vita user " + System.IO.Path.GetFileName(u) + ")", P(u, "savedata"), "config.yml - pref-path");
                    if (Directory.Exists(P(data, "screenshots"))) Add("ScreenshotFolder", "Screenshots", P(data, "screenshots"), "Vita3K storage");
                    if (File.Exists(P(program, "vita3k.log"))) Add("LogFolder", "Logs", program, "vita3k.log");
                    Notice = "Vita3K's pref-path determines save storage. No save-state directory is assumed.";
                    break;
                case "PPSSPP":
                    var installed = P(program, "installed.txt");
                    var installedPath = File.Exists(installed) ? ReadText(installed).Trim().Trim('\uFEFF') : "";
                    data = File.Exists(installed) ? (String.IsNullOrWhiteSpace(installedPath) ? P(documents, "PPSSPP") : Resolve(installedPath, program)) : P(program, "memstick");
                    if (!Directory.Exists(data) && Directory.Exists(P(documents, "PPSSPP"))) data = P(documents, "PPSSPP");
                    Add("ConfigFolder", "Configuration", P(data, "PSP", "SYSTEM"), "PPSSPP memory-stick storage");
                    Add("InGameSaveFolder", "In-game saves (PSP)", P(data, "PSP", "SAVEDATA"), "PPSSPP memory-stick storage");
                    Add("SaveStateFolder", "Save states", P(data, "PSP", "PPSSPP_STATE"), "PPSSPP memory-stick storage");
                    Add("ScreenshotFolder", "Screenshots", P(data, "PSP", "SCREENSHOT"), "PPSSPP memory-stick storage");
                    break;
                case "RetroArch":
                    data = FindConfigRoot(new[] { program, P(roaming, "RetroArch") }, new[] { "retroarch.cfg" });
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
                    data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : program;
                    var alternate = Environment.GetEnvironmentVariable("RPCS3_CONFIG_DIR");
                    if (!Directory.Exists(P(program, "portable")) && !String.IsNullOrWhiteSpace(alternate)) data = Resolve(alternate, program);
                    config = File.Exists(P(data, "config", "vfs.yml")) ? P(data, "config", "vfs.yml") : P(data, "vfs.yml");
                    settings = ReadYaml(config);
                    Add("ConfigFolder", "Configuration", Directory.Exists(P(data, "config")) ? P(data, "config") : data, "RPCS3 configuration");
                    var emuRoot = Get(settings, "$(EmulatorDir)");
                    emuRoot = String.IsNullOrWhiteSpace(emuRoot) ? data : Resolve(emuRoot, program);
                    var hdd = Get(settings, "/dev_hdd0/");
                    hdd = String.IsNullOrWhiteSpace(hdd) ? P(emuRoot, "dev_hdd0") : Resolve(hdd.Replace("$(EmulatorDir)", emuRoot.TrimEnd('\\', '/') + @"\"), program);
                    var homes = P(hdd, "home");
                    var psUsers = Directory.Exists(homes) ? Directory.GetDirectories(homes).Take(64).ToArray() : new[] { P(homes, "00000001") };
                    foreach (var u in psUsers) Add("InGameSaveFolder", "In-game saves (PS3 user " + System.IO.Path.GetFileName(u) + ")", P(u, "savedata"), "RPCS3 virtual HDD / vfs.yml");
                    Add("SaveStateFolder", "Save states", P(data, "savestates"), "RPCS3 configuration root");
                    Add("ScreenshotFolder", "Screenshots", P(data, "screenshots"), "RPCS3 configuration root");
                    Add("LogFolder", "Logs", P(data, "log"), "RPCS3 log directory");
                    Notice = "RPCS3 users have separate in-game saves. Save-state support depends on the installed build and game.";
                    break;
                case "mGBA":
                    data = File.Exists(P(program, "portable.ini")) ? program : P(roaming, "mGBA");
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
                    config = P(program, "mame.ini"); settings = ReadMame(config);
                    var iniDirs = Get(settings, "inipath");
                    if (String.IsNullOrWhiteSpace(iniDirs)) iniDirs = ".;ini";
                    foreach (var iniDir in iniDirs.Split(';').Take(8))
                    {
                        var iniFile = P(Resolve(iniDir, program), "mame.ini");
                        if (String.Equals(iniFile, config, StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var item in ReadMame(iniFile)) settings[item.Key] = item.Value;
                    }
                    Configured(settings, "cfg_directory", "ConfigFolder", "Configuration (system settings)", program, "cfg", config);
                    Configured(settings, "nvram_directory", "InGameSaveFolder", "In-game saves (NVRAM)", program, "nvram", config);
                    Configured(settings, "state_directory", "SaveStateFolder", "Save states", program, "sta", config);
                    Configured(settings, "snapshot_directory", "ScreenshotFolder", "Screenshots", program, "snap", config);
                    if (File.Exists(P(program, "error.log"))) Add("LogFolder", "Logs", program, "MAME error.log");
                    Notice = "MAME global directory settings are shown. System-specific INI overrides may change locations.";
                    break;
                case "Eden":
                    data = Directory.Exists(P(program, "user")) ? P(program, "user") : P(roaming, "eden");
                    config = P(data, "config", "qt-config.ini"); settings = ReadSettings(config, true);
                    Add("ConfigFolder", "Configuration", P(data, "config"), "Eden user directory");
                    nand = SettingPath(settings, "Data Storage/nand_directory", program, P(data, "nand"));
                    var saveRoot = SettingPath(settings, "Data Storage/save_directory", program, nand);
                    Add("InGameSaveFolder", "In-game saves (Switch users)", P(saveRoot, "user", "save"), "Eden storage / qt-config.ini");
                    Add("InGameSaveFolder", "In-game saves (Switch system)", P(saveRoot, "system", "save"), "Eden storage / qt-config.ini");
                    Add("LogFolder", "Logs", P(data, "log"), "Eden user directory");
                    Notice = "Switch saves are separate from installed content. No save-state directory is assumed.";
                    break;
                default:
                    if (exe == "ryujinx")
                    {
                        data = Directory.Exists(P(program, "portable")) ? P(program, "portable") : P(roaming, "Ryujinx");
                        Add("ConfigFolder", "Configuration", data, "Ryujinx user directory");
                        Add("InGameSaveFolder", "In-game saves (Switch)", P(data, "bis", "user", "save"), "Ryujinx storage");
                        Add("LogFolder", "Logs", P(data, "Logs"), "Ryujinx user directory");
                    }
                    else if (String.IsNullOrWhiteSpace(Notice)) Notice = "Automatic detection is not available for this emulator yet. Choose its folders once; all emulators remain supported as launchers.";
                    break;
            }
        }
        private string DolphinRoot()
        {
            if (File.Exists(P(program, "portable.txt"))) return P(program, "User");
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
        private void Add(string property, string label, string value, string source)
        {
            if (folders.Any(f => f.Property == property && String.Equals(f.Path, value, StringComparison.OrdinalIgnoreCase))) return;
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
            value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')).Replace('/', '\\');
            if (value.StartsWith(@":\")) value = value.Substring(2);
            if (value.Contains("%") || value.StartsWith("@")) return "";
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
                { try { value = new JavaScriptSerializer().Deserialize<string>(value); } catch (ArgumentException) { continue; } }
                else if (value.StartsWith("'") && value.EndsWith("'")) value = value.Substring(1, value.Length - 2).Replace("''", "'");
                else { int comment = value.IndexOf(" #"); if (comment >= 0) value = value.Substring(0, comment).TrimEnd(); }
                result[line.Substring(0, colon).Trim()] = value;
            }
            return result;
        }
    }


    public class EmulatorBuild
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Executable { get; set; }
        public string ManualVersion { get; set; }
        public string AddedAt { get; set; }
    }
    public static class HubPaths
    {
        public static string EmulatorRoot(LibraryData library)
        { return String.IsNullOrWhiteSpace(library.EmulatorRootDirectory) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FishBowl", "Emulators") : Path.GetFullPath(library.EmulatorRootDirectory); }
        public static string BackupRoot(LibraryData library)
        { return String.IsNullOrWhiteSpace(library.BackupFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FishBowl", "Backups") : Path.GetFullPath(library.BackupFolder); }
        public static bool Same(string a, string b)
        { try { return String.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        public static bool Inside(string root, string child)
        { return Path.GetFullPath(child).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
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
            if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("An archive contains an invalid path.");
            foreach (var part in relative.Split('\\'))
            {
                if (part == "" || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ") ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new InvalidDataException("An archive contains an unsafe filename.");
            }
            var full = Path.GetFullPath(Path.Combine(root, relative));
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
            if (!File.Exists(executable) || !EmulatorReference.IsLaunchFile(executable)) throw new IOException("Choose an existing Windows emulator executable or shortcut.");
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
                    foreach (var file in Directory.EnumerateFiles(item.Item1, "*.exe").Take(300))
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
    }
    public static class EmulatorInstaller
    {
        public static void ValidateArchive(System.IO.Compression.ZipArchive archive, string destination, bool requireExecutable = true)
        {
            if (archive.Entries.Count > 50000) throw new InvalidDataException("This archive contains too many entries.");
            long size = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            if (requireExecutable && !archive.Entries.Any(e => e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("This ZIP contains no Windows emulator executable.");
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
                    }
                }
                token.ThrowIfCancellationRequested(); Directory.Move(stage, target); return target;
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
            fetch = fetch ?? ReadOfficialFeed; var serializer = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
            GitHubRelease release;
            if (profile.IncludePreviewReleases)
                release = serializer.Deserialize<List<GitHubRelease>>(fetch("https://api.github.com/repos/" + repo + "/releases?per_page=10")).Where(r => !r.draft).OrderByDescending(r => r.published_at).FirstOrDefault();
            else release = serializer.Deserialize<GitHubRelease>(fetch("https://api.github.com/repos/" + repo + "/releases/latest"));
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
        public static RuntimeState State(string executable)
        {
            if (!File.Exists(executable)) return RuntimeState.Stopped;
            if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return RuntimeState.Unknown;
            bool unknown = false;
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
            {
                using (process)
                {
                    try { if (!process.HasExited && HubPaths.Same(process.MainModule.FileName, executable)) return RuntimeState.Running; }
                    catch (Exception) { unknown = true; }
                }
            }
            return unknown ? RuntimeState.Unknown : RuntimeState.Stopped;
        }
        public static bool BringForward(string executable)
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                using (process)
                    try
                    {
                        if (HubPaths.Same(process.MainModule.FileName, executable) && process.MainWindowHandle != IntPtr.Zero)
                        { ShowWindow(process.MainWindowHandle, 9); SetForegroundWindow(process.MainWindowHandle); return true; }
                    } catch (Exception) { }
            return false;
        }
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
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
                    using (var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open(), new System.Text.UTF8Encoding(false))) writer.Write(new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(manifest));
                }
                token.ThrowIfCancellationRequested(); File.Move(temp, target); return target;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static BackupManifest ReadManifest(System.IO.Compression.ZipArchive archive)
        {
            var entry = archive.GetEntry("manifest.json");
            if (entry == null || entry.Length > 8 * 1024 * 1024) throw new InvalidDataException("This is not a supported FishBowl backup.");
            BackupManifest manifest; using (var reader = new StreamReader(entry.Open())) manifest = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Deserialize<BackupManifest>(reader.ReadToEnd());
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
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            checks.Add(new HealthCheck { Name = "Launch format", Status = EmulatorReference.IsLaunchFile(profile.Executable ?? "") ? "Supported" : "Check path", Detail = ".exe, .bat, .cmd and .lnk launchers are supported." });
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
    public class BuildLabelDialog : Form
    {
        private readonly TextBox value = new TextBox();
        public string Value { get { return value.Text.Trim(); } }
        public BuildLabelDialog(string title, string label, string initial)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 160); MinimizeBox = MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedDialog;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3, ColumnCount = 1 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill }, 0, 0); value.Text = initial; value.Dock = DockStyle.Fill; panel.Controls.Add(value, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK }; var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel }; actions.Controls.Add(cancel); actions.Controls.Add(ok);
            panel.Controls.Add(actions, 0, 2); Controls.Add(panel); AcceptButton = ok; CancelButton = cancel;
        }
    }
    public class ReviewDialog : Form
    {
        public ReviewDialog(string title, string detail, string action)
        {
            Text = title; ClientSize = new Size(780, 580); MinimumSize = new Size(640, 460); StartPosition = FormStartPosition.CenterParent;
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new TextBox { Text = detail, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill }, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var confirm = new FishBowlActionButton { Text = action, DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel); buttons.Controls.Add(confirm); shell.Controls.Add(buttons, 0, 1); Controls.Add(shell); CancelButton = cancel;
        }
    }
    public class CandidatePickerDialog : Form
    {
        private readonly ListBox choices = new ListBox();
        public DiscoveredEmulator Selected { get { return choices.SelectedItem as DiscoveredEmulator; } }
        public CandidatePickerDialog(IEnumerable<DiscoveredEmulator> items)
        {
            Text = "Choose the emulator program"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 420);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new Label { Text = "Select the main emulator executable. Helper tools and installers can also appear in the package.", Dock = DockStyle.Fill }, 0, 0);
            choices.Dock = DockStyle.Fill; choices.HorizontalScrollbar = true; choices.Items.AddRange(items.Cast<object>().ToArray()); if (choices.Items.Count > 0) choices.SelectedIndex = 0; shell.Controls.Add(choices, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new FishBowlActionButton { Text = "Choose", DialogResult = DialogResult.OK }; var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok); shell.Controls.Add(buttons, 0, 2); Controls.Add(shell); AcceptButton = ok; CancelButton = cancel;
        }
    }
    public class EmulatorManagerDialog : Form
    {
        private readonly LibraryData library;
        private EmulatorProfile profile;
        private readonly ComboBox profiles = new ComboBox();
        private readonly TabControl tabs = new FishBowlTabs();
        private readonly Label progress = new Label();
        private readonly TextBox rootBox = new TextBox(), backupRootBox = new TextBox(), setupName = new TextBox(), setupProgram = new TextBox(), packageName = new TextBox(), downloadUrl = new TextBox(), repository = new TextBox();
        private readonly ComboBox setupPreset = new ComboBox();
        private readonly ListView discovered = new SmoothListView(), builds = new SmoothListView(), health = new SmoothListView();
        private readonly CheckBox unknownPrograms = new CheckBox(), previews = new CheckBox();
        private readonly TextBox updateText = new TextBox(), backupText = new TextBox(), requirementText = new TextBox();
        private readonly Dictionary<string, CheckBox> categories = new Dictionary<string, CheckBox>();
        private readonly System.Threading.CancellationTokenSource cancellation = new System.Threading.CancellationTokenSource();
        private bool busy, loading;
        private BackupPlan backupPlan;
        private string planProfileId, releaseUrl;
        private readonly Color background = Color.FromArgb(31, 32, 37), surface = Color.FromArgb(47, 48, 56), textColor = Color.FromArgb(235, 235, 241), muted = Color.FromArgb(174, 176, 186);
        public EmulatorManagerDialog(LibraryData data, EmulatorProfile selected, string page)
        {
            library = data; profile = selected; Text = "FishBowl — Emulator management"; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            Icon managementIcon = null; using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("FishBowl.ico")) if (stream != null) { managementIcon = new Icon(stream); Icon = managementIcon; }
            Disposed += delegate { if (managementIcon != null) managementIcon.Dispose(); };
            ClientSize = new Size(960, 730); MinimumSize = new Size(860, 650); BackColor = background; ForeColor = textColor; Font = new Font("Bahnschrift", 9);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3 };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            top.Controls.Add(new Label { Text = "Selected emulator", AutoSize = true, Margin = new Padding(0, 8, 12, 0) }); profiles.Width = 440; profiles.DropDownStyle = ComboBoxStyle.DropDownList; top.Controls.Add(profiles);
            top.Controls.Add(Button("Repair location", RepairLocation)); shell.Controls.Add(top, 0, 0);
            tabs.Dock = DockStyle.Fill; BuildSetup(); BuildDiscovery(); BuildUpdates(); BuildVersions(); BuildBackups(); BuildHealth();
            FishBowlVisuals.Tabs(tabs, background, textColor, Color.FromArgb(183,150,245), "controller", "search", "download", "layers", "backup", "check"); shell.Controls.Add(tabs, 0, 1);
            progress.Dock = DockStyle.Fill; progress.ForeColor = muted; progress.Text = "Emulator tools only. Games stay inside the dedicated emulator."; shell.Controls.Add(progress, 0, 2); Controls.Add(shell);
            profiles.SelectedIndexChanged += delegate { if (loading) return; int i = profiles.SelectedIndex - 1; profile = i >= 0 && i < library.Emulators.Count ? library.Emulators[i] : null; RefreshSelected(); };
            FishBowlHighlights.Attach(this);
            RefreshProfiles(selected); var selectedPage = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text == page); if (selectedPage != null) tabs.SelectedTab = selectedPage;
            FormClosing += delegate { cancellation.Cancel(); };
        }
        private Button Button(string label, Action action)
        {
            var button = new FishBowlActionButton { Text = label, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = textColor, Margin = new Padding(0, 4, 8, 4), Padding = new Padding(8, 2, 8, 2) };
            if (label == "Add to FishBowl" || label == "Create backup" || label == "Use selected build") { button.BackColor = Color.FromArgb(183,150,245); button.ForeColor = background; button.Font = new Font(Font, FontStyle.Bold); }
            button.Click += delegate { try { action(); } catch (Exception ex) { Fail(ex); } }; return button;
        }
        private void Fail(Exception ex)
        {
            Store.Log("Emulator management: " + ex); if (IsDisposed) return;
            progress.Text = "Action could not finish.";
            MessageBox.Show(this, ex.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private async void RunAsync(Func<System.Threading.Tasks.Task> action)
        {
            if (busy) return; busy = true; tabs.Enabled = false; profiles.Enabled = false;
            try { await action(); }
            catch (OperationCanceledException) { if (!IsDisposed) progress.Text = "Cancelled."; }
            catch (Exception ex) { Fail(ex); }
            finally { busy = false; if (!IsDisposed) { tabs.Enabled = true; profiles.Enabled = true; } }
        }
        private TableLayoutPanel Page(string name)
        {
            var tab = new TabPage(name) { BackColor = background, ForeColor = textColor, Padding = new Padding(12), AutoScroll = true };
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = background };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tab.Controls.Add(panel); tabs.TabPages.Add(tab); return panel;
        }
        private void Row(TableLayoutPanel panel, Control control, int height)
        {
            int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
            control.Dock = DockStyle.Fill; panel.Controls.Add(control, 0, row);
        }
        private Label Hint(string text)
        { return new Label { Text = text, ForeColor = muted, Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 2) }; }
        private FlowLayoutPanel Actions(params Control[] items)
        { var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true }; panel.Controls.AddRange(items); return panel; }
        private Control Input(string label, TextBox field, Button button = null)
        {
            var panel = new TableLayoutPanel { ColumnCount = button == null ? 1 : 2, RowCount = 2, Dock = DockStyle.Fill };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); if (button != null) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, ForeColor = textColor, Dock = DockStyle.Fill }, 0, 0);
            field.Dock = DockStyle.Fill; field.BackColor = surface; field.ForeColor = textColor; panel.Controls.Add(field, 0, 1);
            if (button != null) { button.Dock = DockStyle.Fill; panel.Controls.Add(button, 1, 1); } return panel;
        }
        private void StyleText(TextBox box)
        { box.ReadOnly = true; box.Multiline = true; box.ScrollBars = ScrollBars.Vertical; box.BackColor = surface; box.ForeColor = textColor; box.BorderStyle = BorderStyle.FixedSingle; box.Dock = DockStyle.Fill; }
        private void StyleList(ListView list, params string[] columns)
        {
            list.View = View.Details; list.FullRowSelect = true; list.HideSelection = false; list.MultiSelect = false; list.BackColor = surface; list.ForeColor = textColor; list.Dock = DockStyle.Fill;
            foreach (var column in columns) list.Columns.Add(column, column == "Program" || column == "Details" ? 400 : 160);
        }
        private EmulatorProfile Selected()
        { if (profile == null) throw new InvalidOperationException("Choose an emulator at the top first."); return profile; }
        private void RequireStopped(EmulatorProfile p)
        { if (EmulatorRuntime.State(p.Executable) == RuntimeState.Running) throw new IOException("Close " + p.Name + " before changing versions or backing up/restoring its files."); }
        private void RefreshProfiles(EmulatorProfile selected)
        {
            loading = true; profiles.Items.Clear(); profiles.Items.Add("Choose an emulator…");
            foreach (var p in library.Emulators) profiles.Items.Add(p.Name);
            profiles.SelectedIndex = selected != null && library.Emulators.Contains(selected) ? library.Emulators.IndexOf(selected) + 1 : 0; loading = false; profile = selected; RefreshSelected();
        }
        private void RefreshSelected()
        {
            backupPlan = null; backupText.Text = "Choose backup categories and preview the included folders/files.";
            loading = true; repository.Text = profile == null ? "" : (profile.GitHubRepository ?? ""); previews.Checked = profile != null && profile.IncludePreviewReleases; loading = false;
            RefreshUpdates(); RefreshBuilds(); RefreshHealth();
        }
        private void ChooseRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose your dedicated emulator folder. Existing installations stay where they are.", SelectedPath = Directory.Exists(HubPaths.EmulatorRoot(library)) ? HubPaths.EmulatorRoot(library) : "", ShowNewFolderButton = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) { library.EmulatorRootDirectory = Path.GetFullPath(dialog.SelectedPath); Store.Save(library); rootBox.Text = HubPaths.EmulatorRoot(library); }
        }
        private void OpenRoot()
        { var root = HubPaths.EmulatorRoot(library); Directory.CreateDirectory(root); Process.Start(new ProcessStartInfo(root) { UseShellExecute = true }); }
        private void OpenUrl(string url)
        { if (!EmulatorReference.IsWebUrl(url)) throw new IOException("Supply a full official http/https project address."); Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        public EmulatorProfile SelectedProfile { get { return profile; } }
        private void BuildSetup()
        {
            var page = Page("Setup assistant"); page.AutoScroll = true; page.AutoScrollMinSize = new Size(0, 620);
            Row(page, Hint("Choose where emulator packages live. Download from the project's official page, import a ZIP or browse an existing installation, then add its main program. Installer-based emulators can stay in their normal location."), 52);
            rootBox.ReadOnly = true; rootBox.Text = HubPaths.EmulatorRoot(library);
            Row(page, Input("Dedicated emulator folder", rootBox, Button("Choose…", ChooseRoot)), 62);
            setupPreset.DropDownStyle = ComboBoxStyle.DropDownList; setupPreset.Items.AddRange(EmulatorCatalog.Presets.OrderBy(p => p.Name == "Custom" ? "" : p.Name).Select(p => (object)p.Label).ToArray());
            setupPreset.SelectedIndexChanged += delegate { var preset = EmulatorCatalog.Find(setupPreset.Text); downloadUrl.Text = preset.Website; if (preset.Name != "Custom") { setupName.Text = preset.Name; packageName.Text = preset.Name; } };
            Row(page, setupPreset, 36); setupPreset.SelectedIndex = 0;
            Row(page, Input("Official download page (custom emulators can supply their own)", downloadUrl, Button("Open page", delegate { OpenUrl(downloadUrl.Text.Trim()); })), 62);
            Row(page, Input("Folder / build name for a ZIP import", packageName), 62);
            Row(page, Actions(Button("Create / open emulator folder", OpenRoot), Button("Import emulator ZIP…", ImportZip)), 46);
            Row(page, Hint("ZIP imports use a new folder and keep package files together. For 7z/RAR archives or installers, use your normal extraction/installation tool, then browse below. Portable mode remains an emulator-specific choice."), 48);
            Row(page, Input("Main executable or shortcut", setupProgram, Button("Browse…", BrowseProgram)), 62);
            Row(page, Input("Display name", setupName), 62);
            Row(page, Actions(Button("Add to FishBowl", AddProgram), Button("Register as another build", RegisterSetupBuild)), 46);
        }
        private string PickProgram(string initial)
        {
            using (var dialog = new OpenFileDialog { Title = "Choose the emulator's main program", Filter = "Windows launchers|*.exe;*.bat;*.cmd;*.lnk|All files|*.*", InitialDirectory = Directory.Exists(initial) ? initial : "" })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        private void BrowseProgram()
        { var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file != null) FillProgram(file); }
        private void FillProgram(string executable)
        {
            setupProgram.Text = executable; var known = EmulatorDiscovery.Recognize(executable);
            if (EmulatorCatalog.Find(setupPreset.Text).Name == "Custom" && known.Name != "Custom") setupPreset.SelectedItem = known.Label;
            if (String.IsNullOrWhiteSpace(setupName.Text)) setupName.Text = known.Name == "Custom" ? Path.GetFileNameWithoutExtension(executable) : known.Name;
        }
        private void ImportZip()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose an emulator ZIP downloaded from its official project", Filter = "ZIP packages|*.zip" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var zip = dialog.FileName; var root = HubPaths.EmulatorRoot(library); var label = String.IsNullOrWhiteSpace(packageName.Text) ? Path.GetFileNameWithoutExtension(zip) : packageName.Text;
                RunAsync(async delegate
                {
                    progress.Text = "Extracting emulator package…";
                    var folder = await System.Threading.Tasks.Task.Run(() => EmulatorInstaller.InstallZip(zip, root, label, cancellation.Token));
                    var found = await System.Threading.Tasks.Task.Run(() => EmulatorDiscovery.Scan(folder, true, cancellation.Token));
                    if (IsDisposed) return;
                    progress.Text = "Package extracted to " + folder;
                    using (var picker = new CandidatePickerDialog(found.Items.OrderBy(p => EmulatorCatalog.Find(p.Preset).Name == "Custom").ThenBy(p => p.Name)))
                        if (picker.ShowDialog(this) == DialogResult.OK && picker.Selected != null) FillProgram(picker.Selected.Executable);
                });
            }
        }
        public EmulatorProfile RegisterProgram(string executable, string displayName, string preset)
        {
            if (!File.Exists(executable) || !EmulatorReference.IsLaunchFile(executable)) throw new IOException("Choose an existing emulator program or shortcut.");
            if (String.IsNullOrWhiteSpace(displayName)) throw new IOException("Enter an emulator display name.");
            if (library.Emulators.Any(p => HubPaths.Same(p.Executable, executable))) throw new IOException("This emulator program is already registered.");
            var added = new EmulatorProfile { Id = Guid.NewGuid().ToString("N"), Name = displayName.Trim(), Preset = preset, Executable = Path.GetFullPath(executable), Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>(), Builds = new List<EmulatorBuild>() };
            BuildRegistry.Remember(added, added.Executable, "Initial installation"); library.Emulators.Add(added); Store.Save(library); RefreshProfiles(added); return added;
        }
        private void AddProgram()
        {
            var p = RegisterProgram(setupProgram.Text.Trim().Trim('"'), setupName.Text, setupPreset.Text);
            if (EmulatorCatalog.Find(p.Preset).Name == "Custom" && EmulatorReference.IsWebUrl(downloadUrl.Text.Trim())) { p.WebsiteUrl = downloadUrl.Text.Trim(); p.ReleasesUrl = p.WebsiteUrl; Store.Save(library); }
            progress.Text = "Added " + p.Name + ". Storage folders are detected automatically where supported.";
        }
        private void RegisterSetupBuild()
        {
            var p = Selected(); var file = setupProgram.Text.Trim().Trim('"');
            if (!File.Exists(file) || !EmulatorReference.IsLaunchFile(file)) throw new IOException("Choose the extracted emulator program first.");
            BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, packageName.Text); Store.Save(library); RefreshBuilds(); tabs.SelectedIndex = 3; progress.Text = "Build registered. Select it here to make it active.";
        }
        private void BuildDiscovery()
        {
            var page = Page("Find installed");
            Row(page, Hint("Search the dedicated emulator folder, up to four subfolder levels. Game/storage folders and directory links are skipped. Check the programs you want to register. Azahar's shared filename requires choosing the Plus preset in the setup assistant."), 56);
            unknownPrograms.Text = "Also show unrecognized .exe programs"; unknownPrograms.AutoSize = true;
            Row(page, Actions(Button("Find installed emulators", ScanEmulators), unknownPrograms, Button("Add checked programs", AddDiscovered)), 46);
            StyleList(discovered, "Emulator", "Preset", "Program"); discovered.CheckBoxes = true; Row(page, discovered, 0);
        }
        private void ScanEmulators()
        {
            var root = HubPaths.EmulatorRoot(library); bool include = unknownPrograms.Checked;
            RunAsync(async delegate
            {
                progress.Text = "Finding emulator programs…";
                var result = await System.Threading.Tasks.Task.Run(() => EmulatorDiscovery.Scan(root, include, cancellation.Token));
                if (IsDisposed) return; discovered.Items.Clear();
                foreach (var candidate in result.Items)
                {
                    var item = new ListViewItem(candidate.Name) { Tag = candidate }; item.SubItems.Add(candidate.Preset); item.SubItems.Add(candidate.Executable);
                    bool registered = library.Emulators.Any(p => HubPaths.Same(p.Executable, candidate.Executable));
                    item.Checked = !registered && EmulatorCatalog.Find(candidate.Preset).Name != "Custom"; if (registered) item.Text += " (already added)"; discovered.Items.Add(item);
                }
                progress.Text = result.Items.Count + " programs found." + (result.Warnings.Count > 0 ? " " + result.Warnings.First() : "");
            });
        }
        private void AddDiscovered()
        {
            int added = 0;
            foreach (ListViewItem item in discovered.CheckedItems.Cast<ListViewItem>().ToArray())
            {
                var candidate = item.Tag as DiscoveredEmulator;
                if (candidate == null || library.Emulators.Any(p => HubPaths.Same(p.Executable, candidate.Executable))) continue;
                RegisterProgram(candidate.Executable, candidate.Name, candidate.Preset); added++; item.Checked = false;
            }
            progress.Text = "Added " + added + " emulator programs.";
        }
        private void BuildUpdates()
        {
            var page = Page("Updates");
            Row(page, Hint("Checks published releases from the project's GitHub feed when available. No emulator files are replaced automatically. Projects without a feed use their official download page."), 46);
            Row(page, Input("Optional GitHub repository override: owner/repository (blank = preset source)", repository), 62);
            previews.Text = "Include preview/nightly published releases"; previews.AutoSize = true;
            Row(page, Actions(Button("Check for updates", CheckUpdates), Button("Official downloads", delegate { OpenUrl(EmulatorReference.For(Selected()).Releases); }), Button("Open found release", delegate { OpenUrl(releaseUrl); }), previews), 78);
            StyleText(updateText); Row(page, updateText, 0);
        }
        private void RefreshUpdates()
        {
            releaseUrl = profile == null ? "" : profile.LatestReleaseUrl;
            if (profile == null) { updateText.Text = "Select an emulator to check its releases."; return; }
            updateText.Text = "Installed: " + EmulatorReference.VersionFor(profile) + "\r\nRelease source: " + (EmulatorUpdates.Repository(profile).Length > 0 ? EmulatorUpdates.Repository(profile) : "Official download page; no automatic feed") + "\r\n";
            if (!String.IsNullOrWhiteSpace(profile.LastUpdateCheck))
                updateText.AppendText("\r\nLast successful check (UTC): " + profile.LastUpdateCheck + "\r\nLatest published release: " + profile.LatestReleaseTag + "\r\n\r\n" + profile.LatestReleaseNotes);
            else updateText.AppendText("\r\nNo release check has been completed for this profile.");
        }
        private void CheckUpdates()
        {
            var selected = Selected(); var repo = repository.Text.Trim();
            if (repo.Length > 0 && !EmulatorUpdates.ValidRepo(repo)) throw new IOException("Use owner/repository for the GitHub release source.");
            selected.GitHubRepository = repo; selected.IncludePreviewReleases = previews.Checked; Store.Save(library);
            RunAsync(async delegate
            {
                progress.Text = "Checking official published releases…";
                UpdateResult result;
                try { result = await System.Threading.Tasks.Task.Run(() => EmulatorUpdates.Check(selected)); }
                catch (WebException ex)
                {
                    if (IsDisposed) return;
                    RefreshUpdates(); updateText.AppendText("\r\n\r\nThis check failed: " + ex.Message + "\r\nUse Official downloads. Cached results are not a current update check."); progress.Text = "Release feed unavailable. No up-to-date status was inferred."; return;
                }
                if (IsDisposed) return; releaseUrl = result.Url;
                if (result.HasFeed) { selected.LatestReleaseTag = result.Latest; selected.LatestReleaseUrl = result.Url; selected.LatestReleaseNotes = result.Notes.Length > 60000 ? result.Notes.Substring(0, 60000) + "\r\n[Cached notes truncated. Open the release for the full changelog.]" : result.Notes; selected.LastUpdateCheck = result.CheckedAt; Store.Save(library); }
                updateText.Text = result.Status + "\r\n\r\nInstalled: " + result.Installed + "\r\nLatest: " + (result.Latest ?? "See official downloads") + "\r\nChecked (UTC): " + result.CheckedAt + "\r\n\r\n" + result.Notes;
                progress.Text = result.Status;
            });
        }
        private void BuildVersions()
        {
            var page = Page("Versions");
            Row(page, Hint("Register separate installed builds and choose which executable FishBowl opens. Previous builds stay on disk. Switching builds does not move settings or saves; use each emulator's storage rules. Save-state compatibility can vary between builds."), 56);
            Row(page, Actions(Button("Register installed build…", AddBuild), Button("Use selected build", UseBuild), Button("Forget selected entry", ForgetBuild)), 46);
            StyleList(builds, "Build", "Status", "Program"); Row(page, builds, 0);
            Row(page, Actions(Button("Import another build ZIP…", delegate { tabs.SelectedIndex = 0; packageName.Focus(); progress.Text = "Import a ZIP into its own folder, then register the program here as another build."; })), 46);
        }
        private void RefreshBuilds()
        {
            builds.Items.Clear(); if (profile == null) return;
            var entries = new List<EmulatorBuild>(profile.Builds ?? new List<EmulatorBuild>());
            if (!entries.Any(b => HubPaths.Same(b.Executable, profile.Executable))) entries.Insert(0, new EmulatorBuild { Id = "", Label = "Current installation", Executable = profile.Executable, ManualVersion = profile.ManualVersion });
            foreach (var build in entries)
            {
                var item = new ListViewItem(build.Label ?? "Emulator build") { Tag = build }; item.SubItems.Add(HubPaths.Same(build.Executable, profile.Executable) ? "Active" : File.Exists(build.Executable) ? "Available" : "Missing"); item.SubItems.Add(build.Executable); builds.Items.Add(item);
            }
        }
        private void AddBuild()
        {
            var p = Selected(); var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            using (var prompt = new BuildLabelDialog("Emulator build", "Name this installed build", Path.GetFileName(Path.GetDirectoryName(file))))
            { if (prompt.ShowDialog(this) != DialogResult.OK) return; BuildRegistry.Remember(p, p.Executable, "Current installation"); BuildRegistry.Remember(p, file, prompt.Value); Store.Save(library); RefreshBuilds(); }
        }
        private void UseBuild()
        {
            var p = Selected(); if (builds.SelectedItems.Count == 0) throw new IOException("Select a build first."); RequireStopped(p);
            var build = builds.SelectedItems[0].Tag as EmulatorBuild; BuildRegistry.Activate(p, build); Store.Save(library); RefreshSelected(); progress.Text = "Active build changed. Review detected/manual storage paths before launching.";
        }
        private void ForgetBuild()
        {
            var p = Selected(); if (builds.SelectedItems.Count == 0) return; var build = builds.SelectedItems[0].Tag as EmulatorBuild;
            if (HubPaths.Same(build.Executable, p.Executable)) throw new IOException("The active build remains registered.");
            if (p.Builds != null) p.Builds.Remove(build); Store.Save(library); RefreshBuilds(); progress.Text = "Build entry forgotten. Its files remain on disk.";
        }
        private void BuildBackups()
        {
            var page = Page("Backups");
            Row(page, Hint("Back up configuration, in-game saves and save states separately. Review the preview before creating an archive. Emulator/ROM files and installed content are excluded. Close the emulator first. Restore preserves a backup of the current files before overwriting."), 58);
            backupRootBox.ReadOnly = true; backupRootBox.Text = HubPaths.BackupRoot(library);
            Row(page, Input("Backup destination", backupRootBox, Button("Choose…", ChooseBackupRoot)), 62);
            var choices = new FlowLayoutPanel { Dock = DockStyle.Fill };
            foreach (var entry in new[] { "ConfigFolder|Configuration", "InGameSaveFolder|In-game saves", "SaveStateFolder|Save states" })
            {
                var pieces = entry.Split('|'); var box = new CheckBox { Text = pieces[1], Checked = true, AutoSize = true, Margin = new Padding(0, 6, 18, 0) };
                box.CheckedChanged += delegate { backupPlan = null; backupText.Text = "Categories changed. Preview the backup again."; }; categories.Add(pieces[0], box); choices.Controls.Add(box);
            }
            Row(page, choices, 34);
            Row(page, Actions(Button("Preview backup", PreviewBackup), Button("Create backup", CreateBackup), Button("Restore ZIP…", RestoreBackup), Button("Open backup folder", delegate { var folder = HubPaths.BackupRoot(library); Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); })), 76);
            StyleText(backupText); Row(page, backupText, 0);
        }
        private void ChooseBackupRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Store emulator backups outside the emulator's source/save folders.", SelectedPath = Directory.Exists(HubPaths.BackupRoot(library)) ? HubPaths.BackupRoot(library) : "" })
                if (dialog.ShowDialog(this) == DialogResult.OK) { library.BackupFolder = dialog.SelectedPath; Store.Save(library); backupRootBox.Text = HubPaths.BackupRoot(library); }
        }
        private void PreviewBackup()
        {
            var selected = Selected(); RequireStopped(selected); var choices = categories.Where(c => c.Value.Checked).Select(c => c.Key).ToArray();
            if (choices.Length == 0) throw new IOException("Choose at least one backup category.");
            RunAsync(async delegate
            {
                progress.Text = "Inspecting configuration and save folders…";
                var plan = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Preview(selected, choices, cancellation.Token));
                if (IsDisposed) return; backupPlan = plan; planProfileId = selected.Id; backupText.Text = plan.Summary(); progress.Text = "Preview ready. Review the source folders and included files.";
            });
        }
        private void CreateBackup()
        {
            var selected = Selected(); RequireStopped(selected); if (backupPlan == null || planProfileId != selected.Id) throw new IOException("Preview the selected backup categories first.");
            var plan = backupPlan; var destination = HubPaths.BackupRoot(library);
            RunAsync(async delegate
            {
                progress.Text = "Creating backup archive…"; var file = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Create(selected, plan, destination, cancellation.Token));
                if (IsDisposed) return; backupText.AppendText("\r\n\r\nCreated:\r\n" + file); progress.Text = "Backup completed.";
            });
        }
        private void RestoreBackup()
        {
            var selected = Selected(); RequireStopped(selected); string file;
            using (var dialog = new OpenFileDialog { Title = "Choose a FishBowl emulator backup", Filter = "FishBowl backups|*.zip", InitialDirectory = Directory.Exists(HubPaths.BackupRoot(library)) ? HubPaths.BackupRoot(library) : "" })
            { if (dialog.ShowDialog(this) != DialogResult.OK) return; file = dialog.FileName; }
            BackupManifest manifest;
            using (var archive = System.IO.Compression.ZipFile.OpenRead(file)) { manifest = EmulatorBackups.ReadManifest(archive); EmulatorBackups.ValidateRestore(selected, manifest); }
            var detail = "Restore backup for " + selected.Name + "?\r\n\r\nMatching files will be overwritten. Other files are retained. Current eligible files are backed up before restoration.\r\n\r\nArchive: " + file + "\r\nCreated (UTC): " + manifest.CreatedAt + "\r\n\r\nDestinations:\r\n" + String.Join("\r\n\r\n", manifest.Roots.Select(r => r.Label + "\r\n" + r.Source)) + "\r\n\r\nIncluded files: " + manifest.Files.Count + "\r\n" + String.Join("\r\n", manifest.Files.Take(300).Select(f => f.ArchivePath)) + (manifest.Files.Count > 300 ? "\r\nFirst 300 shown; the archive manifest lists every file." : "");
            using (var review = new ReviewDialog("Review emulator restore", detail, "Restore these files")) if (review.ShowDialog(this) != DialogResult.OK) return;
            var destination = HubPaths.BackupRoot(library);
            RunAsync(async delegate
            {
                progress.Text = "Validating archive checksums and restoring…"; var before = await System.Threading.Tasks.Task.Run(() => EmulatorBackups.Restore(selected, file, destination, cancellation.Token));
                if (IsDisposed) return; backupPlan = null; backupText.Text = "Restore completed.\r\n\r\n" + (before.Length > 0 ? "Previous files preserved in:\r\n" + before : "No existing eligible files needed a backup."); progress.Text = "Restore completed."; RefreshHealth();
            });
        }
        private void BuildHealth()
        {
            var page = Page("Setup checks");
            Row(page, Hint("Check the program path, running status and storage folder availability. An uncreated save folder is informational. BIOS/firmware validity and graphics/controller configuration are checked inside the emulator."), 48);
            Row(page, Actions(Button("Refresh checks", RefreshHealth), Button("Choose firmware folder…", ChooseFirmware), Button("Setup guide", delegate { OpenUrl(EmulatorReference.For(Selected()).Documentation); }), Button("Repair location", RepairLocation)), 76);
            StyleList(health, "Check", "Status", "Details"); Row(page, health, 0); StyleText(requirementText); Row(page, requirementText, 130);
        }
        private void RefreshHealth()
        {
            health.Items.Clear(); if (profile == null) { requirementText.Text = "Select an emulator to review its setup requirements."; return; }
            foreach (var check in EmulatorHealth.Check(profile, library)) { var item = new ListViewItem(check.Name); item.SubItems.Add(check.Status); item.SubItems.Add(check.Detail); item.ToolTipText = check.Detail; health.Items.Add(item); }
            health.ShowItemToolTips = true; requirementText.Text = EmulatorReference.For(profile).Requirements;
        }
        private void ChooseFirmware()
        {
            var selected = Selected(); using (var dialog = new FolderBrowserDialog { Description = "Choose the BIOS/firmware folder already configured inside " + selected.Name, ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) { selected.FirmwareFolder = dialog.SelectedPath; Store.Save(library); RefreshHealth(); }
        }
        private void RepairLocation()
        {
            if (busy) return;
            var selected = Selected(); var file = PickProgram(HubPaths.EmulatorRoot(library)); if (file == null) return;
            BuildRegistry.Repair(selected, file); Store.Save(library); RefreshSelected(); progress.Text = "Program location repaired. Your emulator information and folder overrides were retained.";
        }
    }


    public partial class MainForm
    {
        private readonly Timer runtimeTimer = new Timer { Interval = 2500 };
        private readonly Button managementButton = new FishBowlActionButton();
        private bool pollingRuntime;
        private readonly Dictionary<string, RuntimeState> runtimeStates = new Dictionary<string, RuntimeState>(StringComparer.OrdinalIgnoreCase);
        private void ShowEmulatorManager(string page)
        {
            SaveProfileNotes();
            using (var dialog = new EmulatorManagerDialog(library, CurrentEmulator(), page))
            {
                dialog.ShowDialog(this);
                if (dialog.SelectedProfile != null) selectedEmulatorId = dialog.SelectedProfile.Id;
            }
            reloadProgramMetadata = true; RefreshHub();
        }
        private void RepairSelectedLocation()
        {
            var p = CurrentEmulator(); if (p == null) return;
            using (var dialog = new OpenFileDialog { Title = "Locate " + p.Name + "'s emulator program", Filter = "Windows launchers|*.exe;*.bat;*.cmd;*.lnk|All files|*.*", InitialDirectory = Directory.Exists(HubPaths.EmulatorRoot(library)) ? HubPaths.EmulatorRoot(library) : "" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                BuildRegistry.Repair(p, dialog.FileName); Store.Save(library); RefreshHub(); SetStatus("Program location repaired for " + p.Name + ".");
            }
        }
        private async void RefreshRuntimeStatus()
        {
            if (pollingRuntime || IsDisposed) return;
            pollingRuntime = true;
            try
            {
                var paths = library.Emulators.Select(p => p.Executable).Where(p => !String.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var states = await System.Threading.Tasks.Task.Run(() => paths.ToDictionary(p => p, p => {
                    var state = EmulatorRuntime.State(p);
                    var text = !File.Exists(p) ? "Missing program" : state == RuntimeState.Running ? "Running" : state == RuntimeState.Unknown ? "Status unknown" : "Ready";
                    return Tuple.Create(state, text);
                }, StringComparer.OrdinalIgnoreCase)).ConfigureAwait(false);
                PostUi(delegate {
                runtimeStates.Clear(); foreach (var item in states) runtimeStates[item.Key] = item.Value.Item1;
                var profiles = library.Emulators.ToDictionary(p => p.Id);
                foreach (ListViewItem row in emulatorList.Items)
                {
                    EmulatorProfile p; if (!profiles.TryGetValue(row.Tag as string, out p)) continue;
                    Tuple<RuntimeState, string> state; var text = states.TryGetValue(p.Executable ?? "", out state) ? state.Item2 : "Missing program";
                    if (row.SubItems[1].Text != text) { row.SubItems[1].Text = text; emulatorList.Invalidate(row.Bounds); }
                }
                });
            }
            catch (Exception ex) { Store.Log("Runtime status could not refresh: " + ex.Message); }
            finally { pollingRuntime = false; }
        }
    }

    public class EmulatorInformationDialog : Form
    {
        private readonly EmulatorProfile profile;
        private readonly Dictionary<string, TextBox> fields = new Dictionary<string, TextBox>();
        public EmulatorInformationDialog(EmulatorProfile selected)
        {
            profile = selected; Text = "Information - " + selected.Name; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 620); MinimumSize = new Size(620, 520); BackColor = Color.FromArgb(31, 32, 37);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.Controls.Add(new Label { Text = "These details work with every emulator. Leave a field blank to use its preset information, when available.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(174, 176, 186) }, 0, 0);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            AddTab(tabs, "Overview", new[] { "Platform|Platforms (separate multiple systems with /)", "ManualVersion|Installed version (optional manual override)", "Description|Overview", "Strengths|Strengths / useful features", "Limitations|Known limitations" });
            AddTab(tabs, "Setup & controls", new[] { "Requirements|BIOS / firmware and hardware requirements", "ControllerInfo|Supported controllers / input setup", "ControllerProfileNotes|Your controller notes" });
            AddTab(tabs, "Links", new[] { "WebsiteUrl|Project website", "DocumentationUrl|Setup / documentation", "CompatibilityUrl|Compatibility list", "ReleasesUrl|Releases / downloads", "ChangelogUrl|Changelog", "ControllerGuideUrl|Controller setup guide", "TroubleshootingUrl|Troubleshooting / support" });
            AddTab(tabs, "Folders", new[] { "ConfigFolder|Configuration override (blank = automatic)", "InGameSaveFolder|In-game saves override (blank = automatic)", "SaveStateFolder|Save states override (blank = automatic)", "ScreenshotFolder|Screenshots override (blank = automatic)", "LogFolder|Logs override (blank = automatic)", "SaveFolder|Previous unclassified save shortcut (optional)" });
            shell.Controls.Add(tabs, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var save = new FishBowlActionButton { Text = "Save", Width = 90, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(154, 128, 211), ForeColor = Color.FromArgb(31, 32, 37) };
            save.Click += Save; var cancel = new FishBowlActionButton { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.White };
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); shell.Controls.Add(buttons, 0, 2); Controls.Add(shell); AcceptButton = save; CancelButton = cancel;
        }
        private void AddTab(TabControl tabs, string title, IEnumerable<string> definitions)
        {
            var page = new TabPage(title) { BackColor = Color.FromArgb(35, 36, 42), AutoScroll = true, Padding = new Padding(8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            foreach (var definition in definitions)
            {
                var parts = definition.Split('|'); var property = parts[0];
                bool multiline = new[] { "Description", "Strengths", "Limitations", "Requirements", "ControllerInfo", "ControllerProfileNotes" }.Contains(property);
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, multiline ? 100 : 35));
                table.Controls.Add(new Label { Text = parts[1], Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.BottomLeft }, 0, row++);
                var box = new TextBox { Text = profile.GetType().GetProperty(property).GetValue(profile, null) as string ?? "", Dock = DockStyle.Fill, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                fields.Add(property, box); table.Controls.Add(box, 0, row++);
            }
            table.RowCount = row; page.Controls.Add(table); tabs.TabPages.Add(page);
        }
        private void Save(object sender, EventArgs e)
        {
            foreach (var entry in fields.Where(f => f.Key.EndsWith("Url")))
                if (!String.IsNullOrWhiteSpace(entry.Value.Text) && !EmulatorReference.IsWebUrl(entry.Value.Text.Trim()))
                { MessageBox.Show(this, "Use a full http or https web address for each link, or leave it blank.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            foreach (var entry in fields) profile.GetType().GetProperty(entry.Key).SetValue(profile, entry.Value.Text.Trim(), null);
            DialogResult = DialogResult.OK;
        }
    }

    public class EmulatorDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly TextBox executable = new TextBox();
        private readonly TextBox iconPath = new TextBox();
        private readonly ComboBox preset = new ComboBox();
        private readonly Label hint = new Label();
        private readonly EmulatorProfile existingProfile;
        public EmulatorProfile Profile { get; private set; }

        public EmulatorDialog(EmulatorProfile existing = null, string prefillExecutable = null)
        {
            existingProfile = existing;
            Text = existing == null ? "Add Emulator" : "Edit Emulator";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 386);
            BackColor = Color.FromArgb(31, 32, 37);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 10 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            Controls.Add(form);
            preset.DropDownStyle = ComboBoxStyle.DropDownList;
            preset.DropDownWidth = 660; preset.MaxDropDownItems = 18;
            preset.Items.Add(EmulatorCatalog.Presets[0].Label);
            preset.Items.AddRange(EmulatorCatalog.Presets.Skip(1).OrderBy(p => p.Name).Select(p => (object)p.Label).ToArray());
            preset.SelectedIndexChanged += delegate
            {
                var selected = EmulatorCatalog.Find(preset.Text);
                if (existingProfile == null && selected.Name != "Custom") name.Text = selected.Name;
                hint.Text = selected.Name == "Azahar Plus" ? "For Nintendo 3DS, select the Azahar Plus executable. Games are managed inside Azahar Plus." : "Select the installed emulator's executable. Games and settings stay inside that emulator.";
                if (selected.Platform.IndexOf("experimental", StringComparison.OrdinalIgnoreCase) >= 0) hint.Text = "This emulator is experimental; compatibility varies. Manage games inside the emulator.";
            };
            AddRow(form, "Preset", preset, null, 0);
            AddRow(form, "Display name", name, null, 2);
            AddRow(form, "Emulator program or shortcut", executable, PickExecutable, 4);
            AddRow(form, "Custom emulator image (optional)", iconPath, PickIcon, 6);
            hint.Dock = DockStyle.Fill; hint.ForeColor = Color.FromArgb(174, 176, 186); hint.Font = new Font("Bahnschrift", 9);
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.Controls.Add(hint, 0, 8); form.SetColumnSpan(hint, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var save = new FishBowlActionButton { Text = "Save", Width = 88, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(154, 128, 211), ForeColor = Color.FromArgb(31, 32, 37) };
            save.Click += Save;
            var cancel = new FishBowlActionButton { Text = "Cancel", Width = 88, Height = 30, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) };
            var website = new FishBowlActionButton { Text = "Project website", Width = 126, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) };
            website.Click += delegate
            {
                var selected = EmulatorCatalog.Find(preset.Text);
                if (selected.Website.Length == 0) return;
                try { Process.Start(new ProcessStartInfo(selected.Website) { UseShellExecute = true }); }
                catch (Exception error) { MessageBox.Show(this, "The website could not be opened.\n\n" + error.Message, "FishBowl"); }
            };
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); buttons.Controls.Add(website);
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            form.Controls.Add(buttons, 0, 9); form.SetColumnSpan(buttons, 2);
            AcceptButton = save; CancelButton = cancel;
            if (existing != null)
            {
                var selected = EmulatorCatalog.Find(existing.Preset);
                var label = selected.Name == "Custom" && !String.IsNullOrWhiteSpace(existing.Preset) ? existing.Preset : selected.Label;
                if (!preset.Items.Contains(label)) preset.Items.Add(label);
                preset.SelectedItem = label; name.Text = existing.Name; executable.Text = existing.Executable; iconPath.Text = existing.IconPath;
            }
            else
            {
                preset.SelectedItem = String.IsNullOrWhiteSpace(prefillExecutable) ? EmulatorCatalog.Presets[0].Label : EmulatorCatalog.ForExecutable(prefillExecutable).Label;
                if (!String.IsNullOrWhiteSpace(prefillExecutable)) { executable.Text = prefillExecutable; if (name.Text.Length == 0) name.Text = Path.GetFileNameWithoutExtension(prefillExecutable); }
            }
        }
        private void AddRow(TableLayoutPanel form, string label, Control input, EventHandler browse, int row)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); form.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            form.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(239, 239, 243), Font = new Font("Bahnschrift", 9), TextAlign = ContentAlignment.BottomLeft }, 0, row);
            form.SetColumnSpan(form.GetControlFromPosition(0, row), 2);
            input.Dock = DockStyle.Fill; input.BackColor = Color.FromArgb(47, 48, 56); input.ForeColor = Color.FromArgb(239, 239, 243);
            form.Controls.Add(input, 0, row + 1);
            if (browse == null) form.SetColumnSpan(input, 2);
            else { var button = new FishBowlActionButton { Text = "Browse", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 48, 56), ForeColor = Color.FromArgb(239, 239, 243) }; button.Click += browse; form.Controls.Add(button, 1, row + 1); }
        }
        private void PickExecutable(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Programs and shortcuts (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                executable.Text = dialog.FileName;
                var detected = EmulatorCatalog.ForExecutable(dialog.FileName);
                if (preset.SelectedIndex == 0 && detected.Name != "Custom") preset.SelectedItem = detected.Label;
                if (name.Text.Trim().Length == 0) name.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
        private void PickIcon(object sender, EventArgs e) { using (var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico" }) if (dialog.ShowDialog(this) == DialogResult.OK) iconPath.Text = dialog.FileName; }
        private void Save(object sender, EventArgs e)
        {
            var path = executable.Text.Trim().Trim('"');
            if (name.Text.Trim().Length == 0 || !File.Exists(path) || !EmulatorReference.IsLaunchFile(path)) { MessageBox.Show(this, "Enter a display name and choose an existing Windows emulator program or shortcut (.exe, .bat, .cmd, or .lnk).", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Profile = existingProfile ?? new EmulatorProfile { Extensions = new List<string>(), LaunchProfiles = new List<LaunchProfile>() };
            Profile.Name = name.Text.Trim(); Profile.Preset = preset.Text; Profile.Executable = Path.GetFullPath(path); Profile.IconPath = iconPath.Text.Trim();
            DialogResult = DialogResult.OK;
        }
    }

    public static class GameStorage
    {
        public static string Root(LibraryData library)
        {
            return String.IsNullOrWhiteSpace(library.GameLibraryRoot) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FishBowl", "Games") : Path.GetFullPath(library.GameLibraryRoot);
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

    public class GameStoragePromptDialog : Form
    {
        private readonly CheckBox dontShowAgain = new CheckBox();
        public bool DontShowAgain { get { return dontShowAgain.Checked; } }
        public bool OpenOrganizer { get; private set; }
        public GameStoragePromptDialog()
        {
            Text = "FishBowl Game Storage"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(570, 294); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Keep game files organized", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(22, 22) });
            Controls.Add(new Label { Text = "FishBowl can create a separate Games folder with a different folder for each detected console. Drop game files into FishBowl or open the organizer whenever you want to sort files.\r\n\r\nThis only organizes files on disk. Games are still opened and managed inside their own emulator.", ForeColor = Color.White, Font = new Font("Bahnschrift", 9), Location = new Point(24, 66), Size = new Size(514, 124) });
            dontShowAgain.Text = "Don't show this when FishBowl opens"; dontShowAgain.ForeColor = Color.FromArgb(220, 214, 229); dontShowAgain.BackColor = BackColor; dontShowAgain.Font = new Font("Bahnschrift", 8); dontShowAgain.AutoSize = true; dontShowAgain.Location = new Point(24, 210); Controls.Add(dontShowAgain);
            var continueButton = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(360, 250), Size = new Size(86, 28) }; continueButton.FlatAppearance.BorderSize = 0; Controls.Add(continueButton);
            var openButton = new FishBowlActionButton { Text = "Open organizer", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(454, 250), Size = new Size(94, 28) }; openButton.FlatAppearance.BorderSize = 0; openButton.Click += delegate { OpenOrganizer = true; }; Controls.Add(openButton);
        }
    }

    public class GameStorageOrganizerDialog : Form
    {
        private readonly LibraryData library;
        private readonly ListBox queue = new ListBox();
        private readonly TextBox root = new TextBox();
        private readonly CheckBox copyFiles = new CheckBox();
        private readonly ComboBox forcedConsole = new ComboBox();
        private readonly List<string> queued = new List<string>();
        public GameStorageOrganizerDialog(LibraryData library, IEnumerable<string> droppedPaths)
        {
            this.library = library;
            Text = "FishBowl Game Storage Organizer"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 516); MinimumSize = new Size(680, 450); BackColor = Color.FromArgb(35, 36, 42); FormBorderStyle = FormBorderStyle.Sizable;
            Controls.Add(new Label { Text = "Game Storage Organizer", ForeColor = Color.White, Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(18, 16) });
            Controls.Add(new Label { Text = "Files are sorted into separate console folders. FishBowl does not add them to, or launch them from, the app.", ForeColor = Color.FromArgb(174, 176, 186), Font = new Font("Bahnschrift", 8), AutoSize = true, Location = new Point(20, 45) });
            Controls.Add(new Label { Text = "Games root", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 80), Size = new Size(120, 16) });
            root.ReadOnly = true; root.Text = GameStorage.Root(library); root.BackColor = Color.FromArgb(62, 56, 69); root.ForeColor = Color.White; root.BorderStyle = BorderStyle.FixedSingle; root.Location = new Point(18, 100); root.Size = new Size(552, 24); root.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(root);
            var chooseRoot = Button("Choose root", 580, 98, ChooseRoot); chooseRoot.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(chooseRoot);
            Controls.Add(new Label { Text = "Destination", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 138), Size = new Size(120, 16) });
            forcedConsole.DropDownStyle = ComboBoxStyle.DropDownList; forcedConsole.BackColor = Color.FromArgb(62, 56, 69); forcedConsole.ForeColor = Color.White; forcedConsole.Location = new Point(18, 158); forcedConsole.Size = new Size(300, 24); forcedConsole.Items.Add("Auto detect by file type"); forcedConsole.Items.AddRange(new object[] { "Nintendo Entertainment System", "Super Nintendo", "Game Boy", "Game Boy Color", "Game Boy Advance", "Nintendo DS", "Nintendo 3DS", "Nintendo 64", "Nintendo GameCube", "Nintendo Wii", "Nintendo Wii U", "PlayStation", "PlayStation Portable", "PlayStation Vita", "Xbox", "Xbox 360", "Sega Genesis", "Disc Images", "Arcade and Archives" }); forcedConsole.SelectedIndex = 0; Controls.Add(forcedConsole);
            copyFiles.Text = "Copy files instead of moving them"; copyFiles.ForeColor = Color.White; copyFiles.BackColor = BackColor; copyFiles.Font = new Font("Bahnschrift", 8); copyFiles.AutoSize = true; copyFiles.Location = new Point(340, 161); Controls.Add(copyFiles);
            queue.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; queue.BackColor = Color.FromArgb(47, 48, 56); queue.ForeColor = Color.White; queue.BorderStyle = BorderStyle.None; queue.HorizontalScrollbar = true; queue.Location = new Point(18, 202); queue.Size = new Size(704, 228); queue.AllowDrop = true; queue.DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }; queue.DragDrop += delegate(object sender, DragEventArgs e) { AddPaths(e.Data.GetData(DataFormats.FileDrop) as string[]); }; Controls.Add(queue);
            Controls.Add(Button("Add files", 18, 448, AddFiles)); Controls.Add(Button("Add folder", 108, 448, AddFolder)); Controls.Add(Button("Clear", 208, 448, delegate { queued.Clear(); RefreshQueue(); })); Controls.Add(Button("Open root", 278, 448, OpenRoot));
            var sort = Button("Sort queued files", 588, 448, SortFiles); sort.BackColor = Color.FromArgb(255, 164, 82); sort.ForeColor = Color.FromArgb(40, 25, 14); sort.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; Controls.Add(sort);
            AddPaths(droppedPaths);
        }
        private FishBowlActionButton Button(string text, int x, int y, Action action) { var button = new FishBowlActionButton { Text = text, Location = new Point(x, y), Size = new Size(text == "Sort queued files" ? 134 : 82, 28), BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; return button; }
        private void AddFiles() { using (var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*", Multiselect = true }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames); }
        private void AddFolder() { using (var dialog = new FolderBrowserDialog { Description = "Choose a folder of game files to organize" }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dialog.SelectedPath }); }
        private void ChooseRoot() { using (var dialog = new FolderBrowserDialog { Description = "Choose FishBowl's dedicated games folder", SelectedPath = Directory.Exists(root.Text) ? root.Text : "" }) if (dialog.ShowDialog(this) == DialogResult.OK) { library.GameLibraryRoot = dialog.SelectedPath; root.Text = GameStorage.Root(library); } }
        private void AddPaths(IEnumerable<string> paths) { foreach (var file in GameStorage.Expand(paths).Where(file => !EmulatorReference.IsLaunchFile(file) && !queued.Contains(file, StringComparer.OrdinalIgnoreCase))) queued.Add(file); RefreshQueue(); }
        private void RefreshQueue() { queue.BeginUpdate(); queue.Items.Clear(); foreach (var file in queued) queue.Items.Add(Path.GetFileName(file) + "  —  " + GameStorage.ConsoleFor(file)); queue.EndUpdate(); }
        private void OpenRoot() { Directory.CreateDirectory(GameStorage.Root(library)); Process.Start(new ProcessStartInfo(GameStorage.Root(library)) { UseShellExecute = true }); }
        private void SortFiles()
        {
            if (queued.Count == 0) return; int complete = 0; var errors = new List<string>(); string destinationRoot = GameStorage.Root(library);
            foreach (var file in queued.ToArray())
            {
                try { string console = forcedConsole.SelectedIndex == 0 ? GameStorage.ConsoleFor(file) : forcedConsole.Text; GameStorage.MoveOrCopy(file, destinationRoot, console, copyFiles.Checked); complete++; }
                catch (Exception error) { errors.Add(Path.GetFileName(file) + ": " + error.Message); }
            }
            queued.Clear(); RefreshQueue(); string message = "Organized " + complete + " file" + (complete == 1 ? "." : "s."); if (errors.Count > 0) message += "\n\nCould not organize:\n" + String.Join("\n", errors.Take(5)); MessageBox.Show(this, message, "FishBowl Game Storage");
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

    public class GameLibraryDialog : Form
    {
        private readonly LibraryData library;
        private readonly ListView games = new ListView();
        private readonly TextBox search = new TextBox();
        private readonly ComboBox scope = new ComboBox();

        public GameLibraryDialog(LibraryData library)
        {
            this.library = library;
            Text = "FishBowl Game Library"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(940, 580); MinimumSize = new Size(760, 480); BackColor = Color.FromArgb(35, 36, 42); FormBorderStyle = FormBorderStyle.Sizable;
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(31, 32, 37), Padding = new Padding(18, 14, 18, 12) };
            header.Controls.Add(new Label { Text = "Game Library", ForeColor = Color.White, Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(18, 12) });
            header.Controls.Add(new Label { Text = "Manage games without changing your emulators.", ForeColor = Color.FromArgb(174, 176, 186), Font = new Font("Bahnschrift", 8), AutoSize = true, Location = new Point(20, 41) });
            search.Anchor = AnchorStyles.Top | AnchorStyles.Right; search.Location = new Point(592, 22); search.Size = new Size(210, 24); search.BackColor = Color.FromArgb(54, 55, 64); search.ForeColor = Color.White; search.BorderStyle = BorderStyle.FixedSingle; search.AccessibleName = "Search games"; search.TextChanged += delegate { RefreshGames(); };
            scope.Anchor = AnchorStyles.Top | AnchorStyles.Right; scope.Location = new Point(812, 22); scope.Size = new Size(110, 24); scope.DropDownStyle = ComboBoxStyle.DropDownList; scope.BackColor = Color.FromArgb(54, 55, 64); scope.ForeColor = Color.White; scope.Items.AddRange(new object[] { "All games", "Favorites", "Recent", "Missing" }); scope.SelectedIndex = 0; scope.SelectedIndexChanged += delegate { RefreshGames(); };
            header.Controls.Add(search); header.Controls.Add(scope); Controls.Add(header);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(47, 48, 56), Padding = new Padding(12, 7, 12, 5), WrapContents = false };
            AddButton(actions, "Add game", AddGame); AddButton(actions, "Edit", EditGame); AddButton(actions, "Launch", LaunchGame); AddButton(actions, "Open folder", OpenGameFolder); AddButton(actions, "Details", ShowDetails); AddButton(actions, "Favorite", ToggleFavorite); AddButton(actions, "Sync folders", SyncFolders); AddButton(actions, "Duplicates", ShowDuplicates); AddButton(actions, "Collection", CreateCollection);
            Controls.Add(actions);
            games.Dock = DockStyle.Fill; games.View = View.Details; games.FullRowSelect = true; games.MultiSelect = true; games.HideSelection = false; games.BorderStyle = BorderStyle.None; games.BackColor = Color.FromArgb(35, 36, 42); games.ForeColor = Color.White; games.Font = new Font("Bahnschrift", 9); games.Columns.Add("Title", 245); games.Columns.Add("Emulator", 150); games.Columns.Add("File", 280); games.Columns.Add("Size", 85); games.Columns.Add("Last played", 130); games.DoubleClick += delegate { LaunchGame(); }; games.AccessibleName = "FishBowl game list";
            Controls.Add(games);
            RefreshGames();
        }
        private void AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new FishBowlActionButton { Text = text, Width = text == "Open folder" ? 92 : 78, Height = 28, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AccessibleName = text };
            button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; panel.Controls.Add(button);
        }
        private IEnumerable<GameEntry> VisibleGames()
        {
            IEnumerable<GameEntry> items = library.Games;
            if (scope.Text == "Favorites") items = items.Where(item => item.Favorite);
            else if (scope.Text == "Recent") items = items.OrderByDescending(item => item.LastLaunched).Take(30);
            else if (scope.Text == "Missing") items = items.Where(item => !File.Exists(item.Path));
            string text = search.Text.Trim(); if (text.Length > 0) items = items.Where(item => (item.Title ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || (item.Path ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            return items.OrderBy(item => item.Title).ToArray();
        }
        private void RefreshGames()
        {
            string selected = SelectedGame() == null ? null : SelectedGame().Id;
            games.BeginUpdate(); games.Items.Clear();
            foreach (var game in VisibleGames())
            {
                var emulator = EmulatorFor(game); var item = new ListViewItem(game.Favorite ? "★ " + game.Title : game.Title) { Tag = game };
                item.SubItems.Add(emulator == null ? "Not assigned" : emulator.Name); item.SubItems.Add(game.Path ?? ""); item.SubItems.Add(File.Exists(game.Path) ? FormatBytes(new FileInfo(game.Path).Length) : "Missing"); item.SubItems.Add(String.IsNullOrWhiteSpace(game.LastLaunched) ? "Never" : game.LastLaunched); games.Items.Add(item);
                if (game.Id == selected) item.Selected = true;
            }
            games.EndUpdate();
        }
        private GameEntry SelectedGame() { return games.SelectedItems.Count == 0 ? null : games.SelectedItems[0].Tag as GameEntry; }
        private EmulatorProfile EmulatorFor(GameEntry game) { return library.Emulators.FirstOrDefault(item => item.Id == (String.IsNullOrWhiteSpace(game.PreferredEmulatorId) ? game.EmulatorId : game.PreferredEmulatorId)) ?? (library.Emulators.Count == 1 ? library.Emulators[0] : null); }
        private void AddGame()
        {
            var game = new GameEntry { Id = Guid.NewGuid().ToString("N"), Title = "New game", AddedAt = DateTime.UtcNow.ToString("o"), Tags = new List<string>(), EmulatorId = library.Emulators.Count == 1 ? library.Emulators[0].Id : null };
            using (var dialog = new GameDialog(game, library.Emulators)) if (dialog.ShowDialog(this) == DialogResult.OK) { library.Games.Add(dialog.Game); Store.Save(library); RefreshGames(); }
        }
        private void EditGame()
        {
            var game = SelectedGame(); if (game == null) return;
            using (var dialog = new GameDialog(game, library.Emulators)) if (dialog.ShowDialog(this) == DialogResult.OK) { library.Games[library.Games.IndexOf(game)] = dialog.Game; Store.Save(library); RefreshGames(); }
        }
        private void LaunchGame()
        {
            var game = SelectedGame(); if (game == null) return; var emulator = EmulatorFor(game);
            if (emulator == null || !File.Exists(emulator.Executable)) { MessageBox.Show(this, "Assign an available emulator to this game first.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!File.Exists(game.Path)) { MessageBox.Show(this, "This game file is unavailable. Use Edit to repair its path.", "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (library.Theme.ConfirmBeforeGameLaunch && MessageBox.Show(this, "Launch " + game.Title + " with " + emulator.Name + "?", "FishBowl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string arguments = String.Join(" ", new[] { emulator.Arguments, game.Arguments, "\"" + game.Path + "\"" }.Where(value => !String.IsNullOrWhiteSpace(value)).ToArray());
            Process.Start(new ProcessStartInfo { FileName = emulator.Executable, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(emulator.Executable), UseShellExecute = true });
            game.LastLaunched = DateTime.Now.ToString("g"); game.LaunchCount++; Store.Save(library); RefreshGames();
        }
        private void OpenGameFolder() { var game = SelectedGame(); if (game != null && File.Exists(game.Path)) Process.Start(new ProcessStartInfo(Path.GetDirectoryName(game.Path)) { UseShellExecute = true }); }
        private void ShowDetails() { var game = SelectedGame(); if (game == null) return; using (var dialog = new GameDetailsDialog(game, EmulatorFor(game))) dialog.ShowDialog(this); }
        private void ToggleFavorite() { var game = SelectedGame(); if (game == null) return; game.Favorite = !game.Favorite; Store.Save(library); RefreshGames(); }
        private void SyncFolders() { int added = GameLibraryCatalog.Sync(library); Store.Save(library); RefreshGames(); MessageBox.Show(this, added == 0 ? "No new matching game files were found." : "Added " + added + " game" + (added == 1 ? "." : "s."), "FishBowl"); }
        private void ShowDuplicates()
        {
            var duplicates = library.Games.GroupBy(game => (game.Path ?? "").ToLowerInvariant()).Where(group => group.Key.Length > 0 && group.Count() > 1).SelectMany(group => group.Select(game => game.Title + " — " + game.Path)).ToArray();
            using (var dialog = new ResultsDialog("Duplicate Games", duplicates.Length == 0 ? new[] { "No duplicate game paths were found." } : duplicates)) dialog.ShowDialog(this);
        }
        private void CreateCollection()
        {
            var selected = games.SelectedItems.Cast<ListViewItem>().Select(item => item.Tag as GameEntry).Where(item => item != null).ToList(); if (selected.Count == 0) return;
            using (var prompt = new TextPromptDialog("New Collection", "Collection name")) if (prompt.ShowDialog(this) == DialogResult.OK && prompt.Value.Trim().Length > 0) { library.Collections.Add(new GameCollection { Id = Guid.NewGuid().ToString("N"), Name = prompt.Value.Trim(), GameIds = selected.Select(item => item.Id).ToList() }); Store.Save(library); }
        }
        private static string FormatBytes(long bytes) { return bytes < 1024 * 1024 ? Math.Max(1, bytes / 1024) + " KB" : (bytes / 1024d / 1024d).ToString("0.0") + " MB"; }
    }

    public class GameDialog : Form
    {
        private readonly TextBox title = new TextBox();
        private readonly TextBox path = new TextBox();
        private readonly TextBox artwork = new TextBox();
        private readonly ComboBox preferredEmulator = new ComboBox();
        private readonly TextBox arguments = new TextBox();
        private readonly TextBox notes = new TextBox();
        private readonly CheckBox favorite = new CheckBox();
        private readonly List<EmulatorProfile> emulators;
        public GameEntry Game { get; private set; }

        public GameDialog(GameEntry existing, IEnumerable<EmulatorProfile> profiles)
        {
            emulators = profiles.ToList();
            Text = "Edit Game";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 512);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 13, BackColor = BackColor };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            Controls.Add(form);
            AddRow(form, "Title", title, null, 0);
            AddRow(form, "Game file", path, PickGame, 2);
            AddRow(form, "Cover artwork (optional)", artwork, PickArtwork, 4);
            preferredEmulator.DropDownStyle = ComboBoxStyle.DropDownList;
            preferredEmulator.Items.Add(new EmulatorChoice(null, "Use this library's emulator"));
            foreach (var emulator in emulators) preferredEmulator.Items.Add(new EmulatorChoice(emulator.Id, emulator.Name));
            AddRow(form, "Preferred emulator (optional)", preferredEmulator, null, 6);
            AddRow(form, "Custom launch arguments (optional)", arguments, null, 8);
            var noteLabel = new Label { Text = "Notes", ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            notes.BackColor = Color.FromArgb(62, 56, 69); notes.ForeColor = Color.White; notes.BorderStyle = BorderStyle.FixedSingle; notes.Multiline = true; notes.ScrollBars = ScrollBars.Vertical; notes.Dock = DockStyle.Fill;
            form.Controls.Add(noteLabel, 0, 10); form.SetColumnSpan(noteLabel, 2); form.Controls.Add(notes, 0, 11); form.SetColumnSpan(notes, 2);
            favorite.Text = "Favorite"; favorite.ForeColor = Color.White; favorite.BackColor = BackColor; favorite.Font = new Font("Bahnschrift", 8, FontStyle.Bold); favorite.Location = new Point(18, 438); favorite.AutoSize = true;
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(350, 454), Size = new Size(82, 28) };
            save.FlatAppearance.BorderSize = 0; save.Click += delegate { Save(existing); };
            var cancel = new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(440, 454), Size = new Size(82, 28) };
            Controls.Add(favorite); Controls.Add(save); Controls.Add(cancel);
            title.Text = existing.Title; path.Text = existing.Path; artwork.Text = existing.ArtworkPath; arguments.Text = existing.Arguments; notes.Text = existing.Notes; favorite.Checked = existing.Favorite;
            preferredEmulator.SelectedIndex = Math.Max(0, preferredEmulator.Items.Cast<EmulatorChoice>().ToList().FindIndex(item => item.Id == existing.PreferredEmulatorId));
        }

        private void AddRow(TableLayoutPanel form, string label, Control input, EventHandler browse, int row)
        {
            var caption = new Label { Text = label, ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.Dock = DockStyle.Fill;
            form.Controls.Add(caption, 0, row); form.SetColumnSpan(caption, 2); form.Controls.Add(input, 0, row + 1);
            if (browse != null) { var button = new FishBowlActionButton { Text = "Browse", Dock = DockStyle.Fill, BackColor = Color.FromArgb(87, 52, 117), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.FlatAppearance.BorderSize = 0; button.Click += browse; form.Controls.Add(button, 1, row + 1); }
            else form.SetColumnSpan(input, 2);
        }

        private void PickGame(object sender, EventArgs args) { using (var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" }) if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName; }
        private void PickArtwork(object sender, EventArgs args) { using (var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*" }) if (dialog.ShowDialog(this) == DialogResult.OK) artwork.Text = dialog.FileName; }
        private void Save(GameEntry existing)
        {
            if (String.IsNullOrWhiteSpace(title.Text) || String.IsNullOrWhiteSpace(path.Text)) { MessageBox.Show("Title and game file are required.", "FishBowl"); DialogResult = DialogResult.None; return; }
            var choice = preferredEmulator.SelectedItem as EmulatorChoice;
            Game = new GameEntry { Id = existing.Id, EmulatorId = existing.EmulatorId, Title = title.Text.Trim(), Path = path.Text.Trim(), ArtworkPath = artwork.Text.Trim(), Arguments = arguments.Text.Trim(), Notes = notes.Text.Trim(), PreferredEmulatorId = choice == null ? null : choice.Id, Favorite = favorite.Checked, AddedAt = existing.AddedAt, LastLaunched = existing.LastLaunched, LaunchCount = existing.LaunchCount, Tags = existing.Tags, Genre = existing.Genre, Developer = existing.Developer, Description = existing.Description, ReleaseYear = existing.ReleaseYear, TotalPlaySeconds = existing.TotalPlaySeconds };
        }

        private class EmulatorChoice
        {
            public string Id { get; private set; }
            private readonly string name;
            public EmulatorChoice(string id, string name) { Id = id; this.name = name; }
            public override string ToString() { return name; }
        }
    }

    public class StartupAssistantDialog : Form
    {
        private readonly CheckBox dontShowAgain = new CheckBox();
        public bool DontShowAgain { get { return dontShowAgain.Checked; } }
        public bool OpenSetupAssistant { get; private set; }

        public StartupAssistantDialog()
        {
            Text = "Welcome to FishBowl"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 308); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "FishBowl Setup Assistant", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 16, FontStyle.Bold), AutoSize = true, Location = new Point(22, 22) });
            Controls.Add(new Label { Text = "Get your library ready in a few steps:\r\n\r\n• Add an emulator program or shortcut.\r\n• Review its setup checks and official requirements.\r\n• Add or sync games in Game Library.\r\n• Configure controllers and graphics inside each emulator.", ForeColor = Color.White, Font = new Font("Bahnschrift", 9), Location = new Point(24, 65), Size = new Size(500, 142) });
            dontShowAgain.Text = "Don't show this when FishBowl opens"; dontShowAgain.ForeColor = Color.FromArgb(220, 214, 229); dontShowAgain.BackColor = BackColor; dontShowAgain.Font = new Font("Bahnschrift", 8); dontShowAgain.AutoSize = true; dontShowAgain.Location = new Point(24, 221); dontShowAgain.AccessibleName = "Do not show setup assistant at startup"; Controls.Add(dontShowAgain);
            var continueButton = new FishBowlActionButton { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(82, 58, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(350, 258), Size = new Size(86, 28) }; continueButton.FlatAppearance.BorderSize = 0; Controls.Add(continueButton);
            var setupButton = new FishBowlActionButton { Text = "Open setup", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(444, 258), Size = new Size(92, 28) }; setupButton.FlatAppearance.BorderSize = 0; setupButton.Click += delegate { OpenSetupAssistant = true; }; Controls.Add(setupButton);
        }
    }

    public class TextPromptDialog : Form
    {
        private readonly TextBox input = new TextBox();
        public string Value { get { return input.Text; } set { input.Text = value ?? ""; } }

        public TextPromptDialog(string title, string label)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(390, 130); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = label, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 15), Size = new Size(250, 16) });
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.BorderStyle = BorderStyle.FixedSingle; input.Location = new Point(18, 36); input.Size = new Size(354, 24); Controls.Add(input);
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(198, 82), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0;
            Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(290, 82), Size = new Size(82, 28) });
        }
    }

    public class GameMetadataDialog : Form
    {
        private readonly TextBox genre = new TextBox();
        private readonly TextBox developer = new TextBox();
        private readonly TextBox year = new TextBox();
        private readonly TextBox description = new TextBox();

        public GameMetadataDialog(GameEntry game)
        {
            Text = "Game Metadata"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(500, 360); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            AddField("Genre", genre, 18); AddField("Developer", developer, 72); AddField("Release year", year, 126);
            Controls.Add(new Label { Text = "Description", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 180), Size = new Size(180, 16) });
            description.Location = new Point(18, 201); description.Size = new Size(464, 92); description.Multiline = true; description.ScrollBars = ScrollBars.Vertical; description.BackColor = Color.FromArgb(62, 56, 69); description.ForeColor = Color.White; Controls.Add(description);
            Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(400, 314), Size = new Size(82, 28) });
            Controls.Add(new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(235, 158, 94), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(310, 314), Size = new Size(82, 28) });
            genre.Text = game.Genre ?? ""; developer.Text = game.Developer ?? ""; year.Text = game.ReleaseYear ?? ""; description.Text = game.Description ?? "";
        }

        private void AddField(string label, TextBox field, int y)
        {
            Controls.Add(new Label { Text = label, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, y), Size = new Size(180, 16) });
            field.Location = new Point(18, y + 20); field.Size = new Size(464, 24); field.BackColor = Color.FromArgb(62, 56, 69); field.ForeColor = Color.White; Controls.Add(field);
        }

        public void ApplyTo(GameEntry game) { game.Genre = genre.Text.Trim(); game.Developer = developer.Text.Trim(); game.ReleaseYear = year.Text.Trim(); game.Description = description.Text.Trim(); }
    }

    public class ArtworkUrlDialog : Form
    {
        private readonly TextBox url = new TextBox();
        public string Url { get; private set; }

        public ArtworkUrlDialog()
        {
            Text = "Download Cover Artwork"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(460, 150); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Direct image URL", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 15), Size = new Size(190, 16) });
            url.BackColor = Color.FromArgb(62, 56, 69); url.ForeColor = Color.White; url.BorderStyle = BorderStyle.FixedSingle; url.Location = new Point(18, 37); url.Size = new Size(424, 24); Controls.Add(url);
            Controls.Add(new Label { Text = "Paste an http or https link to a PNG, JPG, or other image.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 67), Size = new Size(420, 18) });
            var download = new FishBowlActionButton { Text = "Download", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(266, 104), Size = new Size(84, 28) }; download.FlatAppearance.BorderSize = 0; download.Click += Save;
            Controls.Add(download); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(358, 104), Size = new Size(84, 28) });
        }

        private void Save(object sender, EventArgs args)
        {
            Uri address;
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)) { MessageBox.Show("Enter a complete http or https image address.", "FishBowl"); DialogResult = DialogResult.None; return; }
            Url = address.AbsoluteUri;
        }
    }

    public class ResultsDialog : Form
    {
        public ResultsDialog(string title, IEnumerable<string> rows)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 400); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.Sizable;
            var list = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(62, 56, 69), ForeColor = Color.White, Font = new Font("Bahnschrift", 9), BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
            foreach (var row in rows) list.Items.Add(row);
            Controls.Add(list);
        }
    }

    public class GameDetailsDialog : Form
    {
        public GameDetailsDialog(GameEntry game, EmulatorProfile emulator)
        {
            Text = game.Title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 386); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            var artwork = new PictureBox { BackColor = Color.FromArgb(62, 56, 69), Location = new Point(18, 18), Size = new Size(180, 138), SizeMode = PictureBoxSizeMode.Zoom };
            try { if (!String.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath)) artwork.Image = Image.FromFile(game.ArtworkPath); } catch { }
            var size = File.Exists(game.Path) ? new FileInfo(game.Path).Length : 0;
            var details = "Title: " + game.Title + "\n\nEmulator: " + (emulator == null ? "None" : emulator.Name) + "\n\nFile: " + game.Path + "\n\nSize: " + (File.Exists(game.Path) ? FormatBytes(size) : "Missing file") + "\n\nAdded: " + (game.AddedAt ?? "Unknown") + "\nLast played: " + (game.LastLaunched ?? "Never") + "\nLaunches: " + game.LaunchCount + "\n\nNotes:\n" + (String.IsNullOrWhiteSpace(game.Notes) ? "None" : game.Notes);
            Controls.Add(artwork); Controls.Add(new TextBox { Text = details, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(62, 56, 69), ForeColor = Color.White, BorderStyle = BorderStyle.None, Location = new Point(216, 18), Size = new Size(326, 320), Font = new Font("Bahnschrift", 8) }); Controls.Add(new FishBowlActionButton { Text = "Close", DialogResult = DialogResult.OK, Location = new Point(460, 344), Size = new Size(82, 28) });
        }

        private static string FormatBytes(long bytes) { string[] units = { "B", "KB", "MB", "GB", "TB" }; double value = bytes; int unit = 0; while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; } return value.ToString(unit == 0 ? "0" : "0.0") + " " + units[unit]; }
    }

    public class SettingsDialog : Form
    {
        private readonly ComboBox theme = new ComboBox();
        private readonly TextBox backupFolder = new TextBox();
        private readonly CheckBox startupAssistant = new CheckBox();
        private readonly CheckBox gameStorageAssistant = new CheckBox();
        private readonly CheckBox startMaximized = new CheckBox();
        private readonly CheckBox autoSync = new CheckBox();
        private readonly CheckBox confirmLaunch = new CheckBox();
        private readonly NumericUpDown backupDays = new NumericUpDown();
        private readonly ComboBox accent = new ComboBox();
        private readonly ComboBox fontFamily = new ComboBox();
        private readonly ComboBox uiScale = new ComboBox();
        private readonly ComboBox density = new ComboBox();
        private readonly CheckBox showBanner = new CheckBox();
        private readonly CheckBox showStatusBar = new CheckBox();
        private readonly CheckBox showInformation = new CheckBox();
        private readonly CheckBox showIcons = new CheckBox();
        private readonly CheckBox enableMotion = new CheckBox();
        private readonly CheckBox alternateRows = new CheckBox();
        private readonly ComboBox selectionContrast = new ComboBox();
        private readonly ComboBox iconTileShape = new ComboBox();
        private readonly ThemeSettings originalTheme;
        public ThemeSettings Theme { get; private set; }
        public string BackupFolder { get; private set; }

        public SettingsDialog(ThemeSettings currentTheme, string currentBackupFolder)
        {
            originalTheme = currentTheme ?? new ThemeSettings { Name = "Twilight", AutoBackupDays = 7, ShowStartupAssistant = true };
            Text = "FishBowl Settings"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(640, 752); BackColor = Color.FromArgb(48, 43, 54); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Theme", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 16), Size = new Size(180, 16) });
            theme.DropDownStyle = ComboBoxStyle.DropDownList; theme.BackColor = Color.FromArgb(62, 56, 69); theme.ForeColor = Color.White; theme.Items.AddRange(new object[] { "Twilight", "Lavender", "Ember", "Light", "High Contrast", "Midnight", "Forest", "Rosewood", "Mist" }); theme.SelectedItem = currentTheme == null ? "Twilight" : currentTheme.Name; theme.Location = new Point(18, 37); theme.Size = new Size(504, 24); Controls.Add(theme);
            Controls.Add(new Label { Text = "Cloud-synced backup folder (optional)", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 76), Size = new Size(280, 16) });
            backupFolder.BackColor = Color.FromArgb(62, 56, 69); backupFolder.ForeColor = Color.White; backupFolder.BorderStyle = BorderStyle.FixedSingle; backupFolder.Location = new Point(18, 97); backupFolder.Size = new Size(416, 24); backupFolder.Text = currentBackupFolder; Controls.Add(backupFolder);
            var browse = new FishBowlActionButton { Text = "Browse", Location = new Point(442, 96), Size = new Size(80, 26), BackColor = Color.FromArgb(87, 52, 117), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; browse.FlatAppearance.BorderSize = 0; browse.Click += Browse; Controls.Add(browse);
            Controls.Add(new Label { Text = "Choose a OneDrive, Dropbox, or other synced folder to receive a copy whenever you export a backup.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 130), Size = new Size(500, 34) });
            startupAssistant.Text = "Show the setup assistant when FishBowl opens"; startupAssistant.ForeColor = Color.White; startupAssistant.BackColor = BackColor; startupAssistant.Font = new Font("Bahnschrift", 8); startupAssistant.AutoSize = true; startupAssistant.Location = new Point(18, 171); startupAssistant.Checked = !originalTheme.StartupAssistantPreferenceSet || originalTheme.ShowStartupAssistant; startupAssistant.AccessibleName = "Show startup setup assistant"; Controls.Add(startupAssistant);
            startMaximized.Text = "Start FishBowl maximized"; startMaximized.ForeColor = Color.White; startMaximized.BackColor = BackColor; startMaximized.Font = new Font("Bahnschrift", 8); startMaximized.AutoSize = true; startMaximized.Location = new Point(18, 201); startMaximized.Checked = originalTheme.StartMaximized; Controls.Add(startMaximized);
            autoSync.Text = "Automatically sync configured game folders"; autoSync.ForeColor = Color.White; autoSync.BackColor = BackColor; autoSync.Font = new Font("Bahnschrift", 8); autoSync.AutoSize = true; autoSync.Location = new Point(18, 231); autoSync.Checked = originalTheme.AutoSyncGameFolders; Controls.Add(autoSync);
            confirmLaunch.Text = "Ask before launching a game from Game Library"; confirmLaunch.ForeColor = Color.White; confirmLaunch.BackColor = BackColor; confirmLaunch.Font = new Font("Bahnschrift", 8); confirmLaunch.AutoSize = true; confirmLaunch.Location = new Point(18, 261); confirmLaunch.Checked = originalTheme.ConfirmBeforeGameLaunch; Controls.Add(confirmLaunch);
            Controls.Add(new Label { Text = "Backup reminder interval", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 301), Size = new Size(180, 16) });
            backupDays.Minimum = 1; backupDays.Maximum = 90; backupDays.Value = Math.Max(1, Math.Min(90, originalTheme.AutoBackupDays)); backupDays.BackColor = Color.FromArgb(62, 56, 69); backupDays.ForeColor = Color.White; backupDays.Location = new Point(18, 321); backupDays.Size = new Size(86, 24); Controls.Add(backupDays);
            Controls.Add(new Label { Text = "days", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(112, 325), Size = new Size(50, 16) });
            gameStorageAssistant.Text = "Show the game storage assistant when FishBowl opens"; gameStorageAssistant.ForeColor = Color.White; gameStorageAssistant.BackColor = BackColor; gameStorageAssistant.Font = new Font("Bahnschrift", 8); gameStorageAssistant.AutoSize = true; gameStorageAssistant.Location = new Point(180, 322); gameStorageAssistant.Checked = !originalTheme.GameStorageAssistantPreferenceSet || originalTheme.ShowGameStorageAssistant; Controls.Add(gameStorageAssistant);
            Controls.Add(new Label { Text = "Appearance", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 11, FontStyle.Bold), Location = new Point(18, 365), Size = new Size(190, 22) });
            AddCaption("Accent color", 18, 396); accent.DropDownStyle = ComboBoxStyle.DropDownList; accent.Items.AddRange(new object[] { "Sunset", "Amethyst", "Ocean", "Rose", "Lime", "Gold", "Ice" }); accent.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.AccentColor) ? "Sunset" : originalTheme.AccentColor; StyleCombo(accent, 18, 416, 184); Controls.Add(accent);
            AddCaption("Interface font", 218, 396); fontFamily.DropDownStyle = ComboBoxStyle.DropDownList; fontFamily.Items.AddRange(new object[] { "Bahnschrift", "Segoe UI", "Calibri", "Arial", "Tahoma", "Verdana", "Trebuchet MS", "Consolas", "Georgia", "Palatino Linotype", "Courier New" }); fontFamily.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.FontFamily) ? "Bahnschrift" : originalTheme.FontFamily; if (fontFamily.SelectedIndex < 0) fontFamily.SelectedIndex = 0; StyleCombo(fontFamily, 218, 416, 184); Controls.Add(fontFamily);
            AddCaption("Interface scale", 418, 396); uiScale.DropDownStyle = ComboBoxStyle.DropDownList; uiScale.Items.AddRange(new object[] { "75%", "80%", "85%", "90%", "100%", "110%", "120%", "125%", "130%", "140%" }); uiScale.SelectedItem = (originalTheme.UiScalePercent <= 0 ? 100 : originalTheme.UiScalePercent).ToString() + "%"; StyleCombo(uiScale, 418, 416, 104); Controls.Add(uiScale);
            AddCaption("List density", 18, 454); density.DropDownStyle = ComboBoxStyle.DropDownList; density.Items.AddRange(new object[] { "Compact", "Standard", "Comfortable" }); density.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.ListDensity) ? "Standard" : originalTheme.ListDensity; StyleCombo(density, 18, 474, 184); Controls.Add(density);
            AddVisualCheck(showBanner, "Show FishBowl banner", 218, 456, originalTheme.ShowBanner);
            AddVisualCheck(showStatusBar, "Show filter and status bar", 218, 482, originalTheme.ShowStatusBar);
            AddVisualCheck(showInformation, "Show emulator information pane", 218, 508, originalTheme.ShowInformationPanel);
            AddVisualCheck(showIcons, "Show emulator icons", 418, 456, originalTheme.ShowEmulatorIcons);
            AddVisualCheck(enableMotion, "Enable hover and selection motion", 418, 482, originalTheme.EnableMotion);
            Controls.Add(new Label { Text = "Fine tuning", ForeColor = Color.FromArgb(255, 181, 106), Font = new Font("Bahnschrift", 11, FontStyle.Bold), Location = new Point(18, 554), Size = new Size(190, 22) });
            AddVisualCheck(alternateRows, "Use subtle alternating rows", 18, 586, originalTheme.AlternateRowShading);
            AddCaption("Selection contrast", 218, 582); selectionContrast.DropDownStyle = ComboBoxStyle.DropDownList; selectionContrast.Items.AddRange(new object[] { "Soft", "Standard", "Strong" }); selectionContrast.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.SelectionContrast) ? "Standard" : originalTheme.SelectionContrast; StyleCombo(selectionContrast, 218, 602, 184); Controls.Add(selectionContrast);
            AddCaption("Icon tile shape", 418, 582); iconTileShape.DropDownStyle = ComboBoxStyle.DropDownList; iconTileShape.Items.AddRange(new object[] { "Square", "Rounded", "Circular" }); iconTileShape.SelectedItem = String.IsNullOrWhiteSpace(originalTheme.IconTileShape) ? "Rounded" : originalTheme.IconTileShape; StyleCombo(iconTileShape, 418, 602, 104); Controls.Add(iconTileShape);
            Controls.Add(new Label { Text = "Appearance changes apply after restart.", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Location = new Point(18, 648), Size = new Size(360, 18) });
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(450, 708), Size = new Size(82, 28) }; save.FlatAppearance.BorderSize = 0; save.Click += SaveSettings; Controls.Add(save); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(542, 708), Size = new Size(82, 28) });
        }

        private void AddCaption(string text, int x, int y) { Controls.Add(new Label { Text = text, ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(x, y), Size = new Size(180, 16) }); }
        private void StyleCombo(ComboBox box, int x, int y, int width) { box.BackColor = Color.FromArgb(62, 56, 69); box.ForeColor = Color.White; box.Location = new Point(x, y); box.Size = new Size(width, 24); }
        private void AddVisualCheck(CheckBox box, string text, int x, int y, bool value) { box.Text = text; box.ForeColor = Color.White; box.BackColor = BackColor; box.Font = new Font("Bahnschrift", 8); box.AutoSize = true; box.Location = new Point(x, y); box.Checked = value; Controls.Add(box); }
        private void SaveSettings(object sender, EventArgs args)
        {
            int scale; if (!Int32.TryParse((uiScale.Text ?? "100").TrimEnd('%'), out scale)) scale = 100;
            Theme = new ThemeSettings { Name = theme.Text, AutoBackupDays = (int)backupDays.Value, LastBackupAt = originalTheme.LastBackupAt, DiscordRichPresenceEnabled = originalTheme.DiscordRichPresenceEnabled, ShowStartupAssistant = startupAssistant.Checked, StartupAssistantPreferenceSet = true, ShowGameStorageAssistant = gameStorageAssistant.Checked, GameStorageAssistantPreferenceSet = true, StartMaximized = startMaximized.Checked, AutoSyncGameFolders = autoSync.Checked, ConfirmBeforeGameLaunch = confirmLaunch.Checked, AccentColor = accent.Text, FontFamily = fontFamily.Text, UiScalePercent = scale, ListDensity = density.Text, ShowBanner = showBanner.Checked, ShowStatusBar = showStatusBar.Checked, ShowInformationPanel = showInformation.Checked, ShowEmulatorIcons = showIcons.Checked, EnableMotion = enableMotion.Checked, AlternateRowShading = alternateRows.Checked, SelectionContrast = selectionContrast.Text, IconTileShape = iconTileShape.Text, CustomizationVersion = 3 };
            BackupFolder = backupFolder.Text.Trim();
        }

        private void Browse(object sender, EventArgs args) { using (var dialog = new FolderBrowserDialog()) if (dialog.ShowDialog(this) == DialogResult.OK) backupFolder.Text = dialog.SelectedPath; }
    }

    public class WebsiteLinkDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly TextBox url = new TextBox();
        public WebsiteLink Link { get; private set; }

        public WebsiteLinkDialog()
        {
            Text = "Add Website Link";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 202);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 5, BackColor = BackColor };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            Controls.Add(form);
            AddField(form, "Link name", name, 0);
            AddField(form, "Website address", url, 2);
            var note = new Label { Text = "Use a full link, for example https://example.com", ForeColor = Color.FromArgb(213, 205, 219), Font = new Font("Bahnschrift", 8), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            form.Controls.Add(note, 0, 4); form.SetColumnSpan(note, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, BackColor = BackColor, Padding = new Padding(18, 0, 18, 8) };
            var save = new FishBowlActionButton { Text = "Save", DialogResult = DialogResult.OK, Width = 82, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14) };
            save.FlatAppearance.BorderSize = 0; save.Click += Save;
            footer.Controls.Add(save); footer.Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 82 });
            Controls.Add(footer);
        }

        private void AddField(TableLayoutPanel form, string label, TextBox input, int row)
        {
            var caption = new Label { Text = label, ForeColor = Color.FromArgb(250, 247, 252), Font = new Font("Bahnschrift", 8, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            input.BackColor = Color.FromArgb(62, 56, 69); input.ForeColor = Color.White; input.BorderStyle = BorderStyle.FixedSingle; input.Dock = DockStyle.Fill;
            form.Controls.Add(caption, 0, row); form.SetColumnSpan(caption, 2); form.Controls.Add(input, 0, row + 1); form.SetColumnSpan(input, 2);
        }

        private void Save(object sender, EventArgs args)
        {
            Uri address;
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("Enter a complete http or https website address.", "FishBowl");
                DialogResult = DialogResult.None;
                return;
            }
            Link = new WebsiteLink { Id = Guid.NewGuid().ToString("N"), Name = String.IsNullOrWhiteSpace(name.Text) ? address.Host : name.Text.Trim(), Url = address.AbsoluteUri };
        }
    }

    public class WebsiteLinkPicker : Form
    {
        private readonly ComboBox links = new ComboBox();
        public WebsiteLink SelectedLink { get; private set; }

        public WebsiteLinkPicker(IEnumerable<WebsiteLink> savedLinks)
        {
            Text = "Remove Website Link";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 128);
            BackColor = Color.FromArgb(48, 43, 54);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var label = new Label { Text = "Saved link", ForeColor = Color.White, Font = new Font("Bahnschrift", 8, FontStyle.Bold), Location = new Point(18, 16), Size = new Size(170, 16) };
            links.DropDownStyle = ComboBoxStyle.DropDownList; links.BackColor = Color.FromArgb(62, 56, 69); links.ForeColor = Color.White; links.Location = new Point(18, 36); links.Size = new Size(364, 24);
            foreach (var link in savedLinks.OrderBy(item => item.Name)) links.Items.Add(new WebsiteLinkChoice(link));
            if (links.Items.Count > 0) links.SelectedIndex = 0;
            var remove = new FishBowlActionButton { Text = "Remove", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(255, 164, 82), ForeColor = Color.FromArgb(40, 25, 14), FlatStyle = FlatStyle.Flat, Location = new Point(204, 79), Size = new Size(84, 28) };
            remove.FlatAppearance.BorderSize = 0; remove.Click += delegate { SelectedLink = ((WebsiteLinkChoice)links.SelectedItem).Link; };
            Controls.Add(label); Controls.Add(links); Controls.Add(remove); Controls.Add(new FishBowlActionButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(298, 79), Size = new Size(84, 28) });
        }

        private class WebsiteLinkChoice
        {
            public WebsiteLink Link { get; private set; }
            public WebsiteLinkChoice(WebsiteLink link) { Link = link; }
            public override string ToString() { return Link.Name + "  -  " + Link.Url; }
        }
    }



    public static class FishBowlVisuals
    {
        public static GraphicsPath Round(RectangleF box, float radius)
        {
            var p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(box.Width, box.Height));
            if (d <= 0) { p.AddRectangle(box); return p; }
            p.AddArc(box.X, box.Y, d, d, 180, 90); p.AddArc(box.Right - d, box.Y, d, d, 270, 90);
            p.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90); p.AddArc(box.X, box.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static string IconForText(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Contains("favorite") || t.Contains("favour")) return t.Contains("★") || t.Contains("favorited") ? "star-filled" : "star";
            if (t.StartsWith("cancel") || t.StartsWith("close")) return "close";
            if (t.StartsWith("remove") || t.StartsWith("forget")) return "remove";
            if (t.Contains("firmware") || t.Contains("bios")) return "chip";
            if (t.Contains("restore")) return "restore";
            if (t.Contains("preview") || t.StartsWith("find") || t.Contains("search")) return "search";
            if (t.StartsWith("create backup")) return "backup";
            if (t.Contains("backup folder")) return "folder";
            if (t.Contains("backup")) return "backup";
            if (t.Contains("repair")) return "repair";
            if (t.Contains("import") || t.Contains("zip")) return "import";
            if (t.StartsWith("register")) return "layers-add";
            if (t.Contains("use selected") || t.Contains("compatibility") || t.Contains("checks")) return "check";
            if (t.StartsWith("add")) return "add";
            if (t.Contains("controller")) return "controller";
            if (t.StartsWith("edit") && !t.Contains("information")) return "edit";
            if (t.Contains("information")) return "info";
            if (t.StartsWith("manage") || t.Contains("settings")) return "settings";
            if (t.StartsWith("refresh") || t == "auto" || t.Contains("check for updates")) return "refresh";
            if (t.Contains("changelog") || t.Contains("notes")) return "note";
            if (t.Contains("guide") || t.Contains("documentation")) return "book";
            if (t.Contains("troubleshoot") || t.Contains("help")) return "help";
            if (t.Contains("download") || t.Contains("releases") || t.Contains("found release")) return "download";
            if (t.Contains("project") || t.Contains("open page")) return "globe";
            if (t.StartsWith("save") || t == "ok" || t == "choose") return "check";
            if (t.Contains("browse") || t.Contains("choose") || t.Contains("folder") || t == "open") return "folder";
            if (t.Contains("open emulator")) return "play";
            if (t.Contains("in-game saves")) return "save";
            if (t.Contains("save states")) return "state";
            return "arrow";
        }
        private static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();
        private static readonly object iconCacheLock = new object();
        public static Image Icon(string kind, int size, Color ink, Color accent)
        {
            string key = kind + "|" + size + "|" + ink.ToArgb() + "|" + accent.ToArgb();
            lock (iconCacheLock)
            {
                Image image;
                if (!iconCache.TryGetValue(key, out image))
                {
                    // Each caller owns its clone. Keep the shared artwork cache bounded.
                    if (iconCache.Count >= 96) { foreach (var old in iconCache.Values) old.Dispose(); iconCache.Clear(); }
                    image = DrawIcon(kind, size, ink, accent); iconCache[key] = image;
                }
                return new Bitmap(image);
            }
        }

        private static Image DrawIcon(string kind, int size, Color ink, Color accent)
        {
            using (var canvas = new Bitmap(size * 4, size * 4))
            {
                using (var g = Graphics.FromImage(canvas))
                using (var pen = new Pen(ink, 2.3f)) using (var mark = new Pen(accent, 2.3f))
                using (var wash = new SolidBrush(Color.FromArgb(24, ink)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size / 8f, size / 8f);
                    pen.StartCap = pen.EndCap = mark.StartCap = mark.EndCap = LineCap.Round; pen.LineJoin = mark.LineJoin = LineJoin.Round;
                    Action<float,float,float,float,float> box = (x,y,w,h,r) => { using(var p=Round(new RectangleF(x,y,w,h),r)) { g.FillPath(wash,p); g.DrawPath(pen,p); } };
                    switch(kind)
                    {
                        case "add": box(5,5,22,22,6); g.DrawLine(mark,16,10,16,22); g.DrawLine(mark,10,16,22,16); break;
                        case "remove": case "close": g.DrawLine(pen,9,9,23,23); g.DrawLine(mark,23,9,9,23); break;
                        case "edit": g.DrawLines(pen,new[]{new PointF(7,21),new PointF(7,26),new PointF(12,25),new PointF(25,12),new PointF(20,7),new PointF(7,21)}); g.DrawLine(mark,18,10,23,15); break;
                        case "settings": g.DrawLine(pen,5,8,27,8); g.DrawLine(pen,5,16,27,16); g.DrawLine(pen,5,24,27,24); using(var b=new SolidBrush(ink)){g.FillEllipse(b,9,5,6,6);g.FillEllipse(b,19,13,6,6);}g.FillEllipse(mark.Brush,11,21,6,6); break;
                        case "folder": using(var p=new GraphicsPath()){p.AddLines(new[]{new PointF(5,10),new PointF(5,7),new PointF(12,7),new PointF(16,11),new PointF(27,11),new PointF(27,25),new PointF(5,25)});p.CloseFigure();g.FillPath(wash,p);g.DrawPath(pen,p);}g.DrawLine(mark,9,17,23,17);break;
                        case "info": g.DrawEllipse(pen,5,5,22,22);g.FillEllipse(mark.Brush,14.5f,8,3,3);g.DrawLine(mark,16,14,16,23);break;
                        case "help": g.DrawEllipse(pen,5,5,22,22);g.DrawArc(mark,11,9,10,8,185,270);g.DrawLine(mark,16,17,16,19);g.FillEllipse(mark.Brush,14.5f,22,3,3);break;
                        case "search": g.DrawEllipse(pen,5,5,17,17);g.DrawLine(mark,21,21,27,27);break;
                        case "refresh": g.DrawArc(pen,6,6,20,20,40,280);g.DrawLines(mark,new[]{new PointF(26,6),new PointF(26,12),new PointF(20,12)});break;
                        case "restore": g.DrawArc(pen,6,6,20,20,210,285);g.DrawLines(mark,new[]{new PointF(5,6),new PointF(5,12),new PointF(11,12)});g.DrawLine(pen,16,11,16,17);g.DrawLine(mark,16,17,21,20);break;
                        case "backup": g.DrawLines(pen,new[]{new PointF(6,9),new PointF(16,5),new PointF(26,9),new PointF(26,23),new PointF(16,27),new PointF(6,23),new PointF(6,9)});g.DrawLines(pen,new[]{new PointF(6,9),new PointF(16,14),new PointF(26,9)});g.DrawLine(mark,16,14,16,27);g.DrawLine(mark,12,8,21,12);break;
                        case "import": case "download": box(6,21,20,6,2);g.DrawLine(mark,16,5,16,18);g.DrawLines(mark,new[]{new PointF(10,13),new PointF(16,19),new PointF(22,13)});break;
                        case "export": box(6,21,20,6,2);g.DrawLine(mark,16,6,16,19);g.DrawLines(mark,new[]{new PointF(10,11),new PointF(16,5),new PointF(22,11)});break;
                        case "check": g.DrawEllipse(pen,5,5,22,22);g.DrawLines(mark,new[]{new PointF(10,16),new PointF(14,20),new PointF(22,12)});break;
                        case "star": case "star-filled": var pts=new PointF[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5;float r=i%2==0?11.5f:5.5f;pts[i]=new PointF(16+(float)Math.Cos(a)*r,16+(float)Math.Sin(a)*r);}if(kind=="star-filled")using(var starFill=new SolidBrush(Color.FromArgb(55,accent)))g.FillPolygon(starFill,pts);g.DrawPolygon(mark,pts);break;
                        case "controller": case "game": using(var p=Round(new RectangleF(4,9,24,15),6)){g.FillPath(wash,p);g.DrawPath(pen,p);}g.DrawLine(pen,9,16.5f,15,16.5f);g.DrawLine(pen,12,13.5f,12,19.5f);g.FillEllipse(mark.Brush,20,13,3,3);g.FillEllipse(mark.Brush,23,17,3,3);break;
                        case "globe": g.DrawEllipse(pen,5,5,22,22);g.DrawEllipse(pen,11,5,10,22);g.DrawLine(mark,6,16,26,16);break;
                        case "book": g.DrawLines(pen,new[]{new PointF(16,8),new PointF(11,6),new PointF(5,6),new PointF(5,25),new PointF(11,25),new PointF(16,27),new PointF(21,25),new PointF(27,25),new PointF(27,6),new PointF(21,6),new PointF(16,8),new PointF(16,27)});g.DrawLine(mark,9,12,12,12);g.DrawLine(mark,20,12,23,12);break;
                        case "note": box(7,4,18,24,3);g.DrawLine(mark,12,10,20,10);g.DrawLine(pen,12,16,20,16);g.DrawLine(pen,12,22,17,22);break;
                        case "layers": case "layers-add": g.DrawPolygon(pen,new[]{new PointF(16,4),new PointF(28,10),new PointF(16,16),new PointF(4,10)});g.DrawLines(pen,new[]{new PointF(5,16),new PointF(16,22),new PointF(27,16)});if(kind=="layers"){g.DrawLines(mark,new[]{new PointF(5,22),new PointF(16,28),new PointF(27,22)});}else{g.DrawLine(mark,23,22,23,29);g.DrawLine(mark,19.5f,25.5f,26.5f,25.5f);}break;
                        case "chip": box(9,9,14,14,3);box(13,13,6,6,1);for(int i=0;i<3;i++){float t=11+i*5;g.DrawLine(mark,t,5,t,9);g.DrawLine(mark,t,23,t,27);g.DrawLine(pen,5,t,9,t);g.DrawLine(pen,23,t,27,t);}break;
                        case "save": box(6,5,20,22,3);box(10,5,12,8,1);g.DrawLine(mark,11,20,21,20);break;
                        case "state": box(5,6,22,20,4);g.DrawArc(mark,11,11,10,10,210,290);g.DrawLine(mark,16,13,16,16);g.DrawLine(mark,16,16,20,18);break;
                        case "desktop": box(4,5,24,17,3);g.DrawLine(pen,16,22,16,27);g.DrawLine(mark,10,27,22,27);break;
                        case "storage": box(5,8,22,17,3);g.DrawLine(pen,5,14,27,14);g.FillEllipse(mark.Brush,21,18,3,3);break;
                        case "repair": g.DrawLine(mark,8,25,20,13);g.DrawEllipse(pen,5,22,5,5);g.DrawArc(pen,17,5,10,10,30,300);break;
                        case "image": box(5,5,22,22,3);g.FillEllipse(mark.Brush,19,9,4,4);g.DrawLines(pen,new[]{new PointF(8,23),new PointF(13,16),new PointF(18,21),new PointF(22,17),new PointF(26,23)});break;
                        case "power":g.DrawArc(pen,6,6,20,20,40,280);g.DrawLine(mark,16,4,16,16);break;
                        case "play":g.DrawPolygon(mark,new[]{new PointF(11,6),new PointF(26,16),new PointF(11,26)});break;
                        case "copy":box(5,5,16,18,3);box(11,10,16,18,3);break;
                        case "library":box(5,6,22,21,3);g.DrawLine(pen,11,6,11,27);g.DrawLine(mark,16,12,22,12);g.DrawLine(mark,16,18,22,18);break;
                        case "tag":g.DrawPolygon(pen,new[]{new PointF(5,6),new PointF(16,6),new PointF(27,17),new PointF(17,27),new PointF(5,15)});g.FillEllipse(mark.Brush,9,10,3,3);break;
                        default:g.DrawLine(pen,6,16,25,16);g.DrawLines(mark,new[]{new PointF(19,10),new PointF(25,16),new PointF(19,22)});break;
                    }
                }
                var output = new Bitmap(size,size); using(var g=Graphics.FromImage(output)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(canvas,new Rectangle(0,0,size,size));}return output;
            }
        }
        public static void Tabs(TabControl tabs, Color background, Color foreground, Color accent, params string[] kinds)
        {
            var images=new ImageList { ColorDepth=ColorDepth.Depth32Bit, ImageSize=new Size(18,18) };var sources=new List<Image>();
            foreach(var kind in kinds) {var icon=Icon(kind,18,foreground,accent);sources.Add(icon);images.Images.Add(icon);}
            tabs.ImageList=images;for(int i=0;i<tabs.TabPages.Count;i++)tabs.TabPages[i].ImageIndex=i<images.Images.Count?i:-1;
            tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.Appearance=TabAppearance.FlatButtons;tabs.Multiline=true;tabs.SizeMode=TabSizeMode.Fixed;tabs.Padding=new Point(8,4);
            tabs.ItemSize=new Size(tabs.TabPages.Cast<TabPage>().Any(p=>p.Text=="Setup assistant")?148:110,32);
            var styled=tabs as FishBowlTabs;if(styled!=null){styled.SurfaceColor=background;styled.HeaderTextColor=foreground;styled.AccentColor=accent;styled.ManagementPages=tabs.TabPages.Cast<TabPage>().Any(p=>p.Text=="Setup assistant");styled.FitTabs();}
            tabs.Disposed+=delegate{tabs.ImageList=null;images.Dispose();foreach(var icon in sources)icon.Dispose();};
        }
    }

    public class FishBowlTabs : TabControl
    {
        public Color SurfaceColor = Color.FromArgb(31,32,37), HeaderTextColor = Color.FromArgb(235,235,241), AccentColor = Color.FromArgb(183,150,245);
        public bool ManagementPages; private bool fittingTabs;
        public FishBowlTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        public void FitTabs(){if(fittingTabs||ClientSize.Width<=0)return;fittingTabs=true;try{int columns=ManagementPages&&ClientSize.Width>=900?6:3;int width=Math.Max(100,(ClientSize.Width-(columns-1)*10-8)/columns);if(ItemSize.Width!=width||ItemSize.Height!=32)ItemSize=new Size(width,32);}finally{fittingTabs=false;}}
        protected override void OnResize(EventArgs e){base.OnResize(e);FitTabs();Invalidate();}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int next=SelectedIndex;if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Down)next=Math.Min(TabPages.Count-1,next+1);else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Up)next=Math.Max(0,next-1);else if(e.KeyCode==Keys.Home)next=0;else if(e.KeyCode==Keys.End)next=TabPages.Count-1;else {base.OnKeyDown(e);return;}
            if(next>=0)SelectedIndex=next;e.Handled=true;e.SuppressKeyPress=true;Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SurfaceColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            for(int i=0;i<TabPages.Count;i++)
            {
                var page=TabPages[i];var bounds=GetTabRect(i);var r=Rectangle.Inflate(bounds,-2,-2);bool selected=SelectedIndex==i;
                using(var p=FishBowlVisuals.Round(r,5))using(var b=new SolidBrush(selected?FishBowlHighlights.Blend(SurfaceColor,24):SurfaceColor))e.Graphics.FillPath(b,p);
                if(selected)using(var b=new SolidBrush(AccentColor))e.Graphics.FillRectangle(b,r.Left+7,r.Bottom-3,Math.Max(1,r.Width-14),2);
                int left=r.Left+8;if(ImageList!=null&&page.ImageIndex>=0&&page.ImageIndex<ImageList.Images.Count){e.Graphics.DrawImage(ImageList.Images[page.ImageIndex],new Rectangle(left,r.Top+(r.Height-18)/2,18,18));left+=24;}
                var fg=selected?HeaderTextColor:FishBowlHighlights.Blend(SurfaceColor,150);
                TextRenderer.DrawText(e.Graphics,page.Text,Font,new Rectangle(left,r.Top,Math.Max(1,r.Right-left-5),r.Height),fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            }
            var frame=Rectangle.Inflate(DisplayRectangle,1,1);using(var pen=new Pen(FishBowlHighlights.Blend(SurfaceColor,22)))e.Graphics.DrawRectangle(pen,frame);
        }
    }

    public class SmoothListView : ListView
    {
        public SmoothListView() { DoubleBuffered = true; }
    }

    public class FishBowlActionButton : Button
    {
        private bool hovering, pressed; private Image ownedIcon; private string iconKind; private int iconColor;
        public FishBowlActionButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AutoSizeMode=AutoSizeMode.GrowAndShrink;TextImageRelation=TextImageRelation.ImageBeforeText;ImageAlign=ContentAlignment.MiddleLeft;
            TextChanged+=delegate{RefreshIcon();};ForeColorChanged+=delegate{RefreshIcon();};
        }
        private void RefreshIcon()
        {
            string kind=FishBowlVisuals.IconForText(Text);int color=ForeColor.ToArgb();if(kind==iconKind&&color==iconColor)return;
            iconKind=kind;iconColor=color;var old=ownedIcon;
            bool darkInk=ForeColor.GetBrightness()<0.55f;ownedIcon=FishBowlVisuals.Icon(kind,20,darkInk?ForeColor:Color.FromArgb(224,208,255),darkInk?ForeColor:Color.FromArgb(255,180,105));Image=ownedIcon;if(old!=null)old.Dispose();
        }
        public override Size GetPreferredSize(Size proposedSize){var size=base.GetPreferredSize(proposedSize);return new Size(size.Width+4,Math.Max(34,size.Height));}
        protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovering=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnEnabledChanged(EventArgs e){if(!Enabled){hovering=pressed=false;}Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(0.5f,0.5f,Math.Max(1,Width-1),Math.Max(1,Height-1));
            Color bg=Enabled&&(hovering||pressed)?FishBowlHighlights.Blend(BackColor,pressed?36:24):BackColor;
            bool dark=BackColor.GetBrightness()<0.65f;Color border=Enabled&&Focused?(dark?Color.FromArgb(206,176,250):ForeColor):FishBowlHighlights.Blend(BackColor,Enabled?22:12);
            using(var p=FishBowlVisuals.Round(r,6))using(var b=new SolidBrush(bg))using(var pen=new Pen(border,Focused&&Enabled?1.5f:1f)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}
            Color fg=Enabled?ForeColor:dark?Color.FromArgb(127,129,144):Color.FromArgb(130,132,146);
            var textSize=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);bool showIcon=Image!=null&&Width>=textSize.Width+36;
            int iconWidth=showIcon?20:0;int gap=showIcon?7:0;int total=iconWidth+gap+textSize.Width;int left=Math.Max(7,(Width-total)/2);int offset=pressed&&Enabled?1:0;
            if(showIcon){var box=new Rectangle(left+offset,(Height-20)/2+offset,20,20);if(Enabled)e.Graphics.DrawImage(Image,box);else using(var attributes=new System.Drawing.Imaging.ImageAttributes()){var m=new System.Drawing.Imaging.ColorMatrix { Matrix33=0.38f };attributes.SetColorMatrix(m);e.Graphics.DrawImage(Image,box,0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}left+=iconWidth+gap;}
            var textBox=new Rectangle(left+offset,offset,Math.Max(1,Width-left-7),Height-2*offset);TextRenderer.DrawText(e.Graphics,Text,Font,textBox,fg,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        protected override void Dispose(bool disposing){if(disposing&&ownedIcon!=null){var image=ownedIcon;ownedIcon=null;Image=null;image.Dispose();}base.Dispose(disposing);}
    }

    public static class FishBowlHighlights
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Attached = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        public static Color Blend(Color background, int opacity = 24)
        {
            var light = background.GetBrightness() < 0.65f ? Color.White : Color.Black;
            return Color.FromArgb((background.R * (255 - opacity) + light.R * opacity) / 255,
                (background.G * (255 - opacity) + light.G * opacity) / 255,
                (background.B * (255 - opacity) + light.B * opacity) / 255);
        }
        public static void Draw(Graphics graphics, Rectangle bounds, Color background, bool selected = false)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1) return;
            var light = background.GetBrightness() < 0.65f ? Color.White : Color.Black;
            using (var p = FishBowlVisuals.Round(new RectangleF(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), 5))
            using (var brush = new SolidBrush(Color.FromArgb(selected ? 32 : 24, light))) using (var pen = new Pen(Color.FromArgb(selected ? 48 : 36, light))) { graphics.FillPath(brush, p); graphics.DrawPath(pen, p); }
        }
        public static void Attach(Control control)
        {
            object marker; if (Attached.TryGetValue(control, out marker)) return; Attached.Add(control, new object());
            var button = control as Button;
            if (button != null)
            {
                Action update = delegate { button.FlatAppearance.MouseOverBackColor = Blend(button.BackColor); button.FlatAppearance.MouseDownBackColor = Blend(button.BackColor, 36); };
                update(); button.BackColorChanged += delegate { update(); };
            }
            var picture = control as PictureBox;
            if (picture != null)
            {
                bool hovering = false;
                picture.MouseEnter += delegate { hovering = true; picture.Invalidate(); };
                picture.MouseLeave += delegate { hovering = false; picture.Invalidate(); };
                picture.Paint += delegate(object sender, PaintEventArgs e) { if (hovering && picture.Image != null) Draw(e.Graphics, Rectangle.Inflate(picture.ClientRectangle, -1, -1), picture.Parent == null ? picture.BackColor : picture.Parent.BackColor); };
            }
            control.ControlAdded += delegate(object sender, ControlEventArgs e) { Attach(e.Control); };
            foreach (Control child in control.Controls) Attach(child);
        }
    }
    public class FishBowlMenuRenderer : ToolStripProfessionalRenderer
    {
        public FishBowlMenuRenderer() : base(new FishBowlMenuColors()) { RoundedEdges = false; }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed))
                FishBowlHighlights.Draw(e.Graphics, new Rectangle(1, 1, Math.Max(1, e.Item.Width - 2), Math.Max(1, e.Item.Height - 2)), e.ToolStrip.BackColor, e.Item.Pressed);
            else base.OnRenderMenuItemBackground(e);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed)) FishBowlHighlights.Draw(e.Graphics, Rectangle.Inflate(e.ImageRectangle, 2, 2), e.ToolStrip.BackColor, e.Item.Pressed);
            base.OnRenderItemImage(e);
        }
    }

    public class FishBowlMenuColors : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Color.FromArgb(31, 32, 37); } }
        public override Color MenuStripGradientEnd { get { return Color.FromArgb(31, 32, 37); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(47, 48, 56); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(69, 63, 84); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(69, 63, 84); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(69, 63, 84); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(47, 48, 56); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(47, 48, 56); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(47, 48, 56); } }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            { Store.Log("Interface action failed: " + e.Exception); MessageBox.Show("FishBowl could not complete this action.\n\n" + e.Exception.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Warning); };
            try { Application.Run(new MainForm()); }
            catch (Exception error) { Store.Log("Startup failed: " + error); MessageBox.Show("FishBowl could not start.\n\n" + error.Message, "FishBowl", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
