using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmulatorHub;

// Linux launch options (FishBowl.Profiles.cs, NETCOREAPP section): Proton/Wine, GameMode, MangoHud, Gamescope,
// environment variables, their storage in extra fields, GamePlay.Prepare and the command line's matching.
public static class LaunchOptionsTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        var dir = Path.Combine(root, "launch-options"); Directory.CreateDirectory(dir);
        var tools = new Dictionary<string, string> { { "umu-run", "/opt/t/umu-run" }, { "wine", "/opt/t/wine" }, { "gamemoderun", "/opt/t/gamemoderun" }, { "mangohud", "/opt/t/mangohud" }, { "gamescope", "/opt/t/gamescope" }, { "flatpak", "/usr/bin/flatpak" } };
        Func<string[], LaunchHost> host = present => new LaunchHost { DataDirectory = Path.Combine(dir, "data"), Home = "/home/tester", Find = name => present.Contains(name) && tools.ContainsKey(name) ? tools[name] : null };
        var everything = host(tools.Keys.ToArray());
        var exe = Path.Combine(dir, "Handheld Emu.exe"); File.WriteAllText(exe, "MZ");
        var script = Path.Combine(dir, "emu.sh"); File.WriteAllText(script, "#!/bin/sh\n"); File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Func<string, GamePlay.LaunchPlan> plan = program => new GamePlay.LaunchPlan { Program = program, Arguments = new List<string> { "/games/Coral Kart.gba" }, WorkingDirectory = dir };

        // Windows programs
        var p = plan(exe);
        var summary = LinuxLaunch.Apply(p, new LinuxLaunchOptions(), everything, "Handheld Emu");
        check("launch: exe runs through umu-run by default", p.Program == "/opt/t/umu-run" && p.Arguments[0] == exe && p.Arguments[1] == "/games/Coral Kart.gba" && summary.StartsWith("Proton (UMU-Proton)"));
        check("launch: proton prefix and game id", p.Environment["WINEPREFIX"] == Path.Combine(dir, "data", "prefixes", "Handheld Emu") && p.Environment["GAMEID"] == "umu-default" && Directory.Exists(Path.Combine(dir, "data", "prefixes")));
        p = plan(exe); LinuxLaunch.Apply(p, new LinuxLaunchOptions { Runner = "Wine", WinePrefix = "~/wine/emu", EnvironmentVariables = "export DXVK_HUD=1\n# comment\nbad line" }, everything, "Handheld Emu");
        check("launch: wine with prefix and variables", p.Program == "/opt/t/wine" && p.Environment["WINEPREFIX"] == "/home/tester/wine/emu" && p.Environment["DXVK_HUD"] == "1" && p.Notes.Any(n => n.Contains("Line 3")));
        p = plan(exe); LinuxLaunch.Apply(p, new LinuxLaunchOptions { Proton = "GE-Proton", GameId = "umu-1234", UmuStore = "gog" }, everything, "Handheld Emu");
        check("launch: proton version and store", p.Environment["PROTONPATH"] == "GE-Proton" && p.Environment["GAMEID"] == "umu-1234" && p.Environment["STORE"] == "gog");
        try { LinuxLaunch.Apply(plan(exe), new LinuxLaunchOptions(), host(new string[0]), "Handheld Emu"); check("launch: exe without runner fails", false); }
        catch (IOException error) { check("launch: exe without runner fails", error.Message.Contains("needs Proton or Wine")); }
        try { LinuxLaunch.Apply(plan(exe), new LinuxLaunchOptions { Runner = "Proton" }, host(new[] { "wine" }), "Handheld Emu"); check("launch: chosen proton without umu fails", false); }
        catch (IOException error) { check("launch: chosen proton without umu fails", error.Message.Contains("umu-launcher is not installed")); }

        // Wrappers around a native program
        p = plan(script);
        LinuxLaunch.Apply(p, new LinuxLaunchOptions { GameMode = true, MangoHud = true, Gamescope = true, GamescopeWidth = 1280, GamescopeHeight = 720, GamescopeFrameLimit = 60, GamescopeFullscreen = true }, everything, "emu");
        check("launch: gamescope -- gamemoderun mangohud program", p.Program == "/opt/t/gamescope" && String.Join(" ", p.Arguments) == "-W 1280 -H 720 -r 60 -f -- /opt/t/gamemoderun " + script + " /games/Coral Kart.gba");
        check("launch: mangohud without mangoapp skipped inside gamescope", p.Notes.Any(n => n.Contains("mangoapp")));
        p = plan(script);
        LinuxLaunch.Apply(p, new LinuxLaunchOptions { GameMode = true, MangoHud = true }, everything, "emu");
        check("launch: gamemoderun mangohud order", p.Program == "/opt/t/gamemoderun" && p.Arguments[0] == "/opt/t/mangohud" && p.Arguments[1] == script);
        p = plan(script);
        LinuxLaunch.Apply(p, new LinuxLaunchOptions { GameMode = true, Gamescope = true }, host(new string[0]), "emu");
        check("launch: missing tools start without them", p.Program == script && p.Notes.Count == 2);
        p = plan(script);
        var plain = Path.Combine(dir, "plain.sh"); File.WriteAllText(plain, "echo\n");
        p = plan(plain); LinuxLaunch.Apply(p, new LinuxLaunchOptions { EnvironmentVariables = "A=1" }, everything, "emu");
        check("launch: scripts that are not executable run through sh", p.Program == "/bin/sh" && p.Arguments[0] == plain);
        p = plan(script);
        check("launch: default options leave the plan alone", LinuxLaunch.Apply(p, new LinuxLaunchOptions(), everything, "emu") == "Native" && p.Program == script && p.Arguments.Count == 1);

        // Flatpak: GameMode and MangoHud go inside the sandbox through --env
        var exports = Path.Combine(dir, "exports", "bin"); Directory.CreateDirectory(exports);
        var wrapper = Path.Combine(exports, "org.example.HandheldEmu"); File.WriteAllText(wrapper, "#!/bin/sh\nexec /usr/bin/flatpak run --branch=stable --arch=x86_64 org.example.HandheldEmu \"$@\"\n");
        p = plan(wrapper);
        LinuxLaunch.Apply(p, new LinuxLaunchOptions { GameMode = true, MangoHud = true, EnvironmentVariables = "A=1" }, everything, "emu");
        check("launch: flatpak env inside the sandbox", p.Program == "/usr/bin/flatpak" && String.Join(" ", p.Arguments) == "run --env=A=1 --env=LD_PRELOAD=libgamemodeauto.so.0 --env=MANGOHUD=1 --branch=stable --arch=x86_64 org.example.HandheldEmu /games/Coral Kart.gba" && p.Environment.Count == 0);

        // Storage: extra fields survive a library save and load; profiles can override the emulator
        var emulator = new EmulatorProfile { Id = "e", Name = "Emu", Executable = script, LaunchProfiles = new List<LaunchProfile> { new LaunchProfile { Name = "Fast", Arguments = "--fast" }, new LaunchProfile { Name = "Plain" } } };
        LinuxLaunch.Save(emulator, new LinuxLaunchOptions { GameMode = true });
        LinuxLaunch.Save(emulator.LaunchProfiles[0], new LinuxLaunchOptions { MangoHud = true });
        var lib = Json.Deserialize<LibraryData>(Json.Serialize(new LibraryData { Emulators = new List<EmulatorProfile> { emulator }, Games = new List<GameEntry>() }));
        var back = lib.Emulators[0];
        check("launch: options survive save and load", LinuxLaunch.For(back).GameMode && LinuxLaunch.For(back.LaunchProfiles[0]).MangoHud && LinuxLaunch.For(back.LaunchProfiles[1]) == null);
        check("launch: profile overrides, others follow the emulator", LinuxLaunch.Effective(back, "Fast").MangoHud && !LinuxLaunch.Effective(back, "Fast").GameMode && LinuxLaunch.Effective(back, "Plain").GameMode && LinuxLaunch.Effective(back, null).GameMode);
        LinuxLaunch.Save(back, new LinuxLaunchOptions());
        check("launch: default options are not stored", back.AdditionalFields == null || !back.AdditionalFields.ContainsKey(LinuxLaunch.Field));
        check("launch: parse variables", LinuxLaunch.ParseVariables("X='a b'\nexport Y=\"2\"\n\n", null).Count == 2);

        // GamePlay.Prepare applies the game's launch profile options
        var rom = Path.Combine(dir, "Coral Kart.gba"); File.WriteAllText(rom, "x");
        var prepared = new LibraryData { Emulators = new List<EmulatorProfile> { new EmulatorProfile { Id = "e", Name = "Emu", Executable = script, Arguments = "{game}", LaunchProfiles = new List<LaunchProfile> { new LaunchProfile { Name = "Debug", Arguments = "--debug" } } } },
            Games = new List<GameEntry> { new GameEntry { Id = "g", Title = "Coral Kart", Path = rom, EmulatorId = "e", LaunchProfileName = "Debug" } } };
        LinuxLaunch.Save(prepared.Emulators[0].LaunchProfiles[0], new LinuxLaunchOptions { EnvironmentVariables = "EMU_LOG=verbose" });
        var game = GamePlay.Prepare(prepared, prepared.Games[0]);
        check("launch: prepare applies profile options", game.Program == script && game.Environment["EMU_LOG"] == "verbose" && String.Join(" ", game.Arguments) == rom + " --debug");
        var start = LinuxLaunch.StartInfo(game);
        check("launch: start info carries the environment", start.Environment["EMU_LOG"] == "verbose" && start.ArgumentList.Count == 2);
        check("launch: command line quoting", LinuxLaunch.Quote("a b'c") == "'a b'\\''c'" && LinuxLaunch.Quote("/x/y") == "/x/y");

        // Command line
        string problem;
        prepared.Emulators.Add(new EmulatorProfile { Id = "e2", Name = "Coral Kart" });
        check("cli: game id first", CommandLine.Find(prepared, "g", out problem) == prepared.Games[0]);
        check("cli: game title before emulator name", CommandLine.Find(prepared, "coral kart", out problem) == prepared.Games[0]);
        check("cli: emulator by name", CommandLine.Find(prepared, "EMU", out problem) == prepared.Emulators[0]);
        check("cli: unknown explains --list", CommandLine.Find(prepared, "nothing", out problem) == null && problem.Contains("fishbowl --list"));
        check("cli: requested options", CommandLine.Requested(new[] { "--launch-last" }) && CommandLine.Requested(new[] { "--launch=x" }) && !CommandLine.Requested(new[] { "--verbose" }));
        var output = new StringWriter();
        check("cli: help", CommandLine.Run(new[] { "--help" }, output, new StringWriter()) == 0 && output.ToString().Contains("--launch-last"));
    }
}
