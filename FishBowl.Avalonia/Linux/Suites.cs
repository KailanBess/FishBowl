using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// EmuDeck and RetroDECK: finding their folders (ROMs, BIOS, saves), the emulators they installed, and the games in their
// per-system ROM folders, so FishBowl can register them without moving anything. Linux only. Only reads files.
namespace EmulatorHub
{
    // A ROM folder name shared by EmuDeck, RetroDECK and ES-DE, the console FishBowl shows, the game file types it
    // imports and the emulators (FishBowl presets, best first) that open them.
    public sealed class SuiteSystem
    {
        public string Folder, Console; public string[] Extensions, Presets;
        public SuiteSystem(string folder, string console, string extensions, string presets)
        { Folder = folder; Console = console; Extensions = extensions.Split(' '); Presets = presets.Length == 0 ? new string[0] : presets.Split(','); }

        public static readonly SuiteSystem[] All =
        {
            new SuiteSystem("nes", "Nintendo Entertainment System", ".nes .fds .unf .zip .7z", "Mesen,RetroArch"),
            new SuiteSystem("famicom", "Nintendo Entertainment System", ".nes .fds .zip .7z", "Mesen,RetroArch"),
            new SuiteSystem("snes", "Super Nintendo", ".sfc .smc .bs .zip .7z", "Snes9x,bsnes,Mesen,RetroArch"),
            new SuiteSystem("sfc", "Super Nintendo", ".sfc .smc .zip .7z", "Snes9x,bsnes,Mesen,RetroArch"),
            new SuiteSystem("n64", "Nintendo 64", ".z64 .n64 .v64 .zip .7z", "RMG,Gopher64,ares,RetroArch"),
            new SuiteSystem("gb", "Game Boy", ".gb .zip .7z", "SameBoy,mGBA,Mesen,RetroArch"),
            new SuiteSystem("gbc", "Game Boy Color", ".gbc .zip .7z", "SameBoy,mGBA,Mesen,RetroArch"),
            new SuiteSystem("gba", "Game Boy Advance", ".gba .zip .7z", "mGBA,RetroArch"),
            new SuiteSystem("nds", "Nintendo DS", ".nds .dsi .zip .7z", "melonDS,DeSmuME,RetroArch"),
            new SuiteSystem("n3ds", "Nintendo 3DS", ".3ds .cci .cia .cxi .3dsx .app", "Azahar Plus"),
            new SuiteSystem("3ds", "Nintendo 3DS", ".3ds .cci .cia .cxi .3dsx .app", "Azahar Plus"),
            new SuiteSystem("gc", "Nintendo GameCube", ".iso .gcm .gcz .rvz .ciso .nkit.iso .dol", "Dolphin"),
            new SuiteSystem("gamecube", "Nintendo GameCube", ".iso .gcm .gcz .rvz .ciso .dol", "Dolphin"),
            new SuiteSystem("wii", "Nintendo Wii", ".iso .wbfs .rvz .gcz .wad .ciso .wia", "Dolphin"),
            new SuiteSystem("wiiu", "Nintendo Wii U", ".wud .wux .wua .rpx", "Cemu"),
            new SuiteSystem("switch", "Nintendo Switch", ".nsp .xci .nca .nro", "Eden"),
            new SuiteSystem("psx", "PlayStation", ".m3u .cue .chd .pbp .ecm .mds .ccd .iso .img", "DuckStation,RetroArch"),
            new SuiteSystem("ps2", "PlayStation 2", ".iso .chd .cso .zso .gz .m3u .elf", "PCSX2"),
            new SuiteSystem("ps3", "PlayStation 3", ".iso .ps3 .psn", "RPCS3"),
            new SuiteSystem("ps4", "PlayStation 4", ".pkg", "shadPS4"),
            new SuiteSystem("psp", "PlayStation Portable", ".iso .cso .pbp .chd", "PPSSPP"),
            new SuiteSystem("psvita", "PlayStation Vita", ".vpk .psvita", "Vita3K"),
            new SuiteSystem("xbox", "Xbox", ".iso .xiso", "xemu"),
            new SuiteSystem("xbox360", "Xbox 360", ".iso .xex .zar", "Xenia Canary"),
            new SuiteSystem("genesis", "Sega Genesis", ".md .gen .smd .bin .zip .7z", "BlastEm,ares,RetroArch"),
            new SuiteSystem("megadrive", "Sega Genesis", ".md .gen .smd .bin .zip .7z", "BlastEm,ares,RetroArch"),
            new SuiteSystem("mastersystem", "Sega Master System", ".sms .zip .7z", "ares,RetroArch"),
            new SuiteSystem("gamegear", "Sega Game Gear", ".gg .zip .7z", "ares,RetroArch"),
            new SuiteSystem("segacd", "Sega CD", ".cue .chd .m3u .iso", "ares,RetroArch"),
            new SuiteSystem("saturn", "Sega Saturn", ".cue .chd .m3u .ccd .mds", "Ymir,RetroArch"),
            new SuiteSystem("dreamcast", "Dreamcast", ".cdi .gdi .chd .m3u .cue", "Flycast,RetroArch"),
            new SuiteSystem("atari2600", "Atari 2600", ".a26 .bin .zip .7z", "Stella,RetroArch"),
            new SuiteSystem("atarijaguar", "Atari Jaguar", ".j64 .jag .zip", "BigPEmu,RetroArch"),
            new SuiteSystem("atarist", "Atari ST", ".st .msa .stx .dim .ipf", "Hatari"),
            new SuiteSystem("pcengine", "PC Engine", ".pce .cue .chd .zip", "Mesen,ares,RetroArch"),
            new SuiteSystem("tg16", "PC Engine", ".pce .cue .chd .zip", "Mesen,ares,RetroArch"),
            new SuiteSystem("arcade", "Arcade", ".zip .7z", "MAME,FinalBurn Neo,RetroArch"),
            new SuiteSystem("mame", "Arcade", ".zip .7z .chd", "MAME,RetroArch"),
            new SuiteSystem("fbneo", "Arcade", ".zip .7z", "FinalBurn Neo,RetroArch"),
            new SuiteSystem("neogeo", "Neo Geo", ".zip .7z", "FinalBurn Neo,MAME,RetroArch"),
            new SuiteSystem("c64", "Commodore 64", ".d64 .t64 .prg .crt .tap .g64", "VICE,RetroArch"),
            new SuiteSystem("msx", "MSX", ".rom .mx1 .mx2 .dsk .cas", "openMSX,RetroArch"),
            new SuiteSystem("amstradcpc", "Amstrad CPC", ".dsk .cdt .sna", "Caprice32,RetroArch"),
            new SuiteSystem("zxspectrum", "ZX Spectrum", ".tzx .tap .z80 .sna .dsk", "ZEsarUX,RetroArch"),
            new SuiteSystem("scummvm", "ScummVM", ".scummvm", "ScummVM"),
        };
        public static SuiteSystem For(string folder) { return All.FirstOrDefault(s => String.Equals(s.Folder, folder, StringComparison.OrdinalIgnoreCase)); }
    }

    // One ROM folder found inside the suite's roms folder.
    public sealed class SuiteRomFolder
    {
        public string Path; public SuiteSystem System; public int Games;
        public string Name { get { return global::System.IO.Path.GetFileName(Path); } }
    }

    public sealed class SuiteInstall
    {
        public string Kind = "", SettingsFile, Home, RomsFolder, BiosFolder, SavesFolder, Launcher;
        public List<SuiteRomFolder> RomFolders = new List<SuiteRomFolder>();
        public List<DiscoveredEmulator> Emulators = new List<DiscoveredEmulator>();
        public override string ToString() { return Kind + (RomsFolder == null ? "" : " — " + RomsFolder); }
    }

    public static class Suites
    {
        public const string RetroDeckId = "net.retrodeck.retrodeck";
        static readonly string[] SkippedFolders = { "media", "images", "videos", "manuals", "downloaded_media", "screenshots", "titles", "snaps", "boxart", "marquees", "wheel", "covers", ".git" };

        // settings.sh lines: emulationPath=/home/deck/Emulation, export romsPath="/run/media/.../roms", with $HOME and ~ expanded.
        public static Dictionary<string, string> ReadShellSettings(string text, string home)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim(); if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).Trim();
                var m = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)=(.*)$"); if (!m.Success) continue;
                var value = m.Groups[2].Value.Trim();
                int close = value.Length > 0 && (value[0] == '"' || value[0] == '\'') ? value.IndexOf(value[0], 1) : -1;
                if (close > 0) value = value.Substring(1, close - 1);
                else { int comment = value.IndexOf(" #", StringComparison.Ordinal); if (comment >= 0) value = value.Substring(0, comment).Trim(); }
                values[m.Groups[1].Value] = Expand(value, home, values);
            }
            return values;
        }
        static string Expand(string value, string home, Dictionary<string, string> known)
        {
            if (value == "~" || value.StartsWith("~/", StringComparison.Ordinal)) value = home + value.Substring(1);
            return Regex.Replace(value, @"\$\{?([A-Za-z_][A-Za-z0-9_]*)\}?", m => { string v; return m.Groups[1].Value == "HOME" ? home : known.TryGetValue(m.Groups[1].Value, out v) ? v : m.Value; });
        }

        // RetroDECK keeps its paths in retrodeck.cfg ([paths] rdhome=, roms_folder=, bios_folder=, saves_folder=) or, from
        // 0.9, retrodeck.json ({"paths": {"rd_home_path": ..., "roms_path": ...}}). Keys are matched loosely for both.
        public static Dictionary<string, string> ReadRetroDeckPaths(string text, string home)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var raw = new List<KeyValuePair<string, string>>();
            var trimmed = (text ?? "").TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(trimmed))
                    {
                        System.Text.Json.JsonElement paths;
                        var scope = doc.RootElement.TryGetProperty("paths", out paths) && paths.ValueKind == System.Text.Json.JsonValueKind.Object ? paths : doc.RootElement;
                        foreach (var p in scope.EnumerateObject()) if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String) raw.Add(new KeyValuePair<string, string>(p.Name, p.Value.GetString()));
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            else
            {
                string section = "";
                foreach (var line in (text ?? "").Split('\n').Select(l => l.Trim()))
                {
                    if (line.StartsWith("[", StringComparison.Ordinal)) { section = line.Trim('[', ']').ToLowerInvariant(); continue; }
                    int eq = line.IndexOf('='); if (eq < 1 || line.StartsWith("#", StringComparison.Ordinal) || (section != "" && section != "paths")) continue;
                    raw.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim().Trim('"')));
                }
            }
            foreach (var pair in raw)
            {
                var key = Regex.Replace(pair.Key.ToLowerInvariant(), "(_|-|folder|path|dir)", "");
                var name = key == "rdhome" || key == "home" ? "home" : key == "roms" ? "roms" : key == "bios" ? "bios" : key == "saves" ? "saves" : null;
                if (name != null && !String.IsNullOrWhiteSpace(pair.Value)) result[name] = Expand(pair.Value.Trim(), home, new Dictionary<string, string>());
            }
            return result;
        }

        public static List<SuiteInstall> Find(string home, string dataHome)
        {
            var found = new List<SuiteInstall>();
            var emudeck = FindEmuDeck(home); if (emudeck != null) found.Add(emudeck);
            var retrodeck = FindRetroDeck(home, dataHome); if (retrodeck != null) found.Add(retrodeck);
            return found;
        }

        public static SuiteInstall FindEmuDeck(string home)
        {
            foreach (var file in new[] { Path.Combine(home, ".config", "EmuDeck", "settings.sh"), Path.Combine(home, "emudeck", "settings.sh") })
            {
                if (!File.Exists(file)) continue;
                var values = ReadShellSettings(SafeRead(file), home); string v;
                var emulation = values.TryGetValue("emulationPath", out v) && v.Length > 0 ? v : Path.Combine(home, "Emulation");
                var install = new SuiteInstall
                {
                    Kind = "EmuDeck", SettingsFile = file, Home = emulation,
                    RomsFolder = values.TryGetValue("romsPath", out v) && v.Length > 0 ? v : Path.Combine(emulation, "roms"),
                    BiosFolder = values.TryGetValue("biosPath", out v) && v.Length > 0 ? v : Path.Combine(emulation, "bios"),
                    SavesFolder = values.TryGetValue("savesPath", out v) && v.Length > 0 ? v : Path.Combine(emulation, "saves"),
                };
                var tools = values.TryGetValue("toolsPath", out v) && v.Length > 0 ? v : Path.Combine(emulation, "tools");
                install.RomFolders = RomFolders(install.RomsFolder);
                install.Emulators = EmuDeckLaunchers(Path.Combine(tools, "launchers"));
                return install;
            }
            return null;
        }

        // EmuDeck writes one launcher script per emulator (tools/launchers/dolphin-emu.sh, pcsx2-qt.sh, ...).
        public static List<DiscoveredEmulator> EmuDeckLaunchers(string folder)
        {
            var result = new List<DiscoveredEmulator>();
            if (!Directory.Exists(folder)) return result;
            foreach (var file in SafeFiles(folder).Where(f => f.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal))
            {
                var preset = EmulatorDiscovery.Recognize(file);
                if (preset.Name == "Custom") preset = EmulatorDiscovery.Recognize(Path.Combine(folder, Path.GetFileNameWithoutExtension(file)));
                if (preset.Name == "Custom" || result.Any(r => r.Name == preset.Name)) continue;
                result.Add(new DiscoveredEmulator { Name = preset.Name, Preset = preset.Label, Executable = file });
            }
            return result;
        }

        public static SuiteInstall FindRetroDeck(string home, string dataHome)
        {
            var config = Path.Combine(home, ".var", "app", RetroDeckId, "config", "retrodeck");
            string launcher = null;
            foreach (var root in new[] { Path.Combine(dataHome, "flatpak"), "/var/lib/flatpak" })
            {
                var candidate = Path.Combine(root, "exports", "bin", RetroDeckId); if (File.Exists(candidate)) { launcher = candidate; break; }
            }
            string file = new[] { "retrodeck.json", "retrodeck.cfg" }.Select(n => Path.Combine(config, n)).FirstOrDefault(File.Exists);
            if (file == null && launcher == null) return null;
            var paths = file == null ? new Dictionary<string, string>() : ReadRetroDeckPaths(SafeRead(file), home); string v;
            var rdhome = paths.TryGetValue("home", out v) ? v : Path.Combine(home, "retrodeck");
            var install = new SuiteInstall
            {
                Kind = "RetroDECK", SettingsFile = file, Home = rdhome, Launcher = launcher,
                RomsFolder = paths.TryGetValue("roms", out v) ? v : Path.Combine(rdhome, "roms"),
                BiosFolder = paths.TryGetValue("bios", out v) ? v : Path.Combine(rdhome, "bios"),
                SavesFolder = paths.TryGetValue("saves", out v) ? v : Path.Combine(rdhome, "saves"),
            };
            install.RomFolders = RomFolders(install.RomsFolder);
            // RetroDECK bundles its emulators in one Flatpak and starts them from its own frontend.
            if (launcher != null) install.Emulators.Add(new DiscoveredEmulator { Name = "RetroDECK", Preset = EmulatorCatalog.Find("Custom").Label, Executable = launcher });
            return install;
        }

        public static List<SuiteRomFolder> RomFolders(string roms)
        {
            var result = new List<SuiteRomFolder>();
            if (String.IsNullOrWhiteSpace(roms) || !Directory.Exists(roms)) return result;
            foreach (var folder in SafeDirectories(roms).OrderBy(d => d, StringComparer.Ordinal))
            {
                var system = SuiteSystem.For(Path.GetFileName(folder)); if (system == null) continue;
                var games = Games(folder, system, 5000).Count; if (games > 0) result.Add(new SuiteRomFolder { Path = folder, System = system, Games = games });
            }
            return result;
        }

        // Game files in a system folder (three levels deep, media folders skipped). Discs listed in an .m3u playlist are
        // left to the playlist, and a .bin that belongs to a .cue is left to the .cue.
        public static List<string> Games(string folder, SuiteSystem system, int limit)
        {
            var files = new List<string>(); var queue = new Queue<Tuple<string, int>>(); queue.Enqueue(Tuple.Create(folder, 0));
            while (queue.Count > 0 && files.Count < limit * 2)
            {
                var current = queue.Dequeue();
                foreach (var file in SafeFiles(current.Item1)) if (Matches(file, system)) files.Add(file);
                if (current.Item2 < 2) foreach (var dir in SafeDirectories(current.Item1)) if (!SkippedFolders.Contains(Path.GetFileName(dir).ToLowerInvariant()) && !IsLink(dir)) queue.Enqueue(Tuple.Create(dir, current.Item2 + 1));
            }
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var list in files.Where(f => f.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)))
            {
                var dir = Path.GetDirectoryName(list);
                foreach (var line in SafeRead(list).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")))
                {
                    var entry = line;
                    if (list.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)) { var m = Regex.Match(line, "^FILE\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase); if (!m.Success) continue; entry = m.Groups[1].Value; }
                    try { covered.Add(Path.GetFullPath(Path.Combine(dir, entry))); } catch (Exception) { }
                }
            }
            return files.Where(f => !covered.Contains(Path.GetFullPath(f))).OrderBy(f => f, StringComparer.Ordinal).Take(limit).ToList();
        }
        static bool Matches(string file, SuiteSystem system)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (name.StartsWith(".") || name == "systeminfo.txt" || name == "metadata.txt") return false;
            return system.Extensions.Any(e => name.EndsWith(e, StringComparison.Ordinal));
        }

        // The emulator FishBowl should use for a system: a registered profile of the first preset that has one.
        public static EmulatorProfile EmulatorFor(LibraryData library, SuiteSystem system)
        {
            foreach (var preset in system.Presets)
            {
                var match = (library.Emulators ?? new List<EmulatorProfile>()).FirstOrDefault(e => EmulatorCatalog.Find(e.Preset).Name == preset);
                if (match != null) return match;
            }
            return null;
        }

        // File types an emulator opens, collected from every system it is the first choice for (or listed for, when none).
        public static List<string> ExtensionsFor(string presetName)
        {
            return SuiteSystem.All.Where(s => s.Presets.Contains(presetName)).SelectMany(s => s.Extensions).Where(e => e != ".zip" && e != ".7z" || presetName == "MAME" || presetName == "FinalBurn Neo" || presetName == "RetroArch")
                .Select(e => e.Substring(e.LastIndexOf('.'))).Distinct().ToList();
        }

        // New library entries for a ROM folder, skipping files already in the library. The console comes from the folder.
        public static List<GameEntry> PrepareImport(LibraryData library, SuiteRomFolder folder, int limit)
        {
            var existing = new HashSet<string>((library.Games ?? new List<GameEntry>()).Where(g => g != null && g.Path != null).Select(g => g.Path), StringComparer.Ordinal);
            var emulator = EmulatorFor(library, folder.System);
            var result = new List<GameEntry>();
            foreach (var file in Games(folder.Path, folder.System, limit))
            {
                if (existing.Contains(Path.GetFullPath(file))) continue;
                var game = GameLibraryImport.NewEntry(library, file, false);
                game.ConsoleLabel = folder.System.Console;
                if (emulator != null) { game.EmulatorId = emulator.Id; game.RequiresEmulatorAssignment = false; }
                else
                {
                    // An extension match from another system (a GameCube .iso picked up by PCSX2) is not trusted.
                    var picked = (library.Emulators ?? new List<EmulatorProfile>()).FirstOrDefault(e => e.Id == game.EmulatorId);
                    if (picked == null || !folder.System.Presets.Contains(EmulatorCatalog.Find(picked.Preset).Name)) { game.EmulatorId = null; game.RequiresEmulatorAssignment = true; }
                }
                game.Extras.Native = false;
                result.Add(game);
            }
            return result;
        }

        static bool IsLink(string path) { try { return new DirectoryInfo(path).LinkTarget != null; } catch (Exception) { return true; } }
        static string SafeRead(string file) { try { return new FileInfo(file).Length > 1024 * 1024 ? "" : File.ReadAllText(file); } catch (Exception) { return ""; } }
        static IEnumerable<string> SafeFiles(string folder) { try { return Directory.EnumerateFiles(folder).Take(20000).ToList(); } catch (Exception) { return new string[0]; } }
        static IEnumerable<string> SafeDirectories(string folder) { try { return Directory.EnumerateDirectories(folder).Take(2000).ToList(); } catch (Exception) { return new string[0]; } }
    }
}
