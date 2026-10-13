using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EmulatorHub;

// Shared profiles (FishBowl.Profiles.cs): user profiles and guest mode, transfer and reviewed sync, launch/controller
// profiles, multiplayer readiness and shortcut text. Linux launch options are covered in LaunchOptionsTests.
public static class ProfilesTests
{
    static void Throws(Action<string, bool> check, string what, Action action)
    {
        try { action(); check(what, false); } catch (IOException) { check(what, true); }
    }

    public static void Run(string root, Action<string, bool> check)
    {
        var dir = Path.Combine(root, "profiles-tests"); Directory.CreateDirectory(dir);
        var rom = Path.Combine(dir, "Puzzle Lagoon.gba"); File.WriteAllText(rom, "x");
        var emu = Path.Combine(dir, "emu.sh"); File.WriteAllText(emu, "#!/bin/sh\n");
        Func<LibraryData> make = () => new LibraryData
        {
            Emulators = new List<EmulatorProfile> { new EmulatorProfile { Id = "e1", Name = "Handheld emu", Executable = emu, Preset = "Custom", LaunchProfiles = new List<LaunchProfile>() } },
            Games = new List<GameEntry> {
                new GameEntry { Id = "g1", Title = "Puzzle Lagoon", Path = rom, EmulatorId = "e1", Favorite = true, PlayStatus = "Completed", TotalPlaySeconds = 300, LaunchCount = 3 },
                new GameEntry { Id = "g2", Title = "Coral Kart", Path = Path.Combine(dir, "Coral Kart.gba"), EmulatorId = "e1" } },
            Collections = new List<GameCollection>(),
            Theme = new ThemeSettings { Name = "Forest", AccentColor = "Ocean" },
            PlayQueue = new List<string> { "g1" },
            PlaySessions = new List<PlaySession> { new PlaySession { Id = "s1", GameId = "g1", StartedAt = DateTime.UtcNow.AddDays(-1).ToString("o"), Seconds = 600 } }
        };

        // User profiles
        var d = make();
        var settings = UserTools.Ensure(d); settings.WeeklyMinutes = 100;
        string original = settings.ActiveId;
        check("profiles: default profile created", settings.Users.Count == 1 && settings.Users[0].Name == "Default");
        var second = UserTools.Create(d, "Second");
        Throws(check, "profiles: duplicate names rejected", () => UserTools.Create(d, "second"));
        Throws(check, "profiles: empty names rejected", () => UserTools.Create(d, " "));
        UserTools.Switch(d, second.Id);
        var g = d.Games[0];
        check("profiles: new profile resets personal progress", !g.Favorite && g.PlayStatus == "Not started" && g.TotalPlaySeconds == 0 && g.LaunchCount == 0);
        check("profiles: journal, queue and goal isolated", d.PlaySessions.Count == 0 && d.PlayQueue.Count == 0 && d.UserTools.WeeklyMinutes == 0);
        g.PlayStatus = "Playing"; g.TotalPlaySeconds = 120; d.UserTools.WeeklyMinutes = 25;
        UserTools.Switch(d, original);
        check("profiles: original progress restored", g.Favorite && g.PlayStatus == "Completed" && g.TotalPlaySeconds == 300 && g.LaunchCount == 3);
        check("profiles: original preferences restored", d.UserTools.WeeklyMinutes == 100 && d.PlayQueue.Count == 1 && d.PlaySessions.Count == 1);
        check("profiles: theme kept per profile", d.Theme != null && d.Theme.Name == "Forest");
        UserTools.Switch(d, second.Id);
        check("profiles: second profile progress kept", d.Games[0].PlayStatus == "Playing" && d.Games[0].TotalPlaySeconds == 120 && d.UserTools.WeeklyMinutes == 25);
        UserTools.Switch(d, original);
        UserTools.ActiveLaunches = 1;
        Throws(check, "profiles: switch blocked during a session", () => UserTools.Switch(d, second.Id));
        Throws(check, "profiles: guest blocked during a session", () => UserTools.BeginGuest(d));
        UserTools.ActiveLaunches = 0;
        check("profiles: weekly seconds count the last 7 days", UserTools.WeekSeconds(d, DateTime.UtcNow) == 600);
        check("profiles: weekly goal text", ProfileTools.WeeklyGoal(d, DateTime.UtcNow).StartsWith("10 minutes / 100 goal (10%)"));

        // Guest mode: changes are discarded and nothing is saved
        var snapshot = UserTools.BeginGuest(d);
        check("profiles: guest mode on and saving paused", UserTools.Guest && Store.ReadOnly);
        d.Games[0].Title = "Guest edit"; d.Games[0].LaunchCount++;
        UserTools.EndGuest(d, snapshot);
        check("profiles: guest exit restores library", d.Games[0].Title == "Puzzle Lagoon" && d.Games[0].LaunchCount == 3 && !UserTools.Guest && !Store.ReadOnly);

        // Bulk undo
        var before = UserTools.BeforeBulk(d, d.Games);
        d.Games[1].Favorite = true; d.Games[1].Tags = new List<string> { "kart" };
        UserTools.AfterBulk(d, before, d.Games);
        d.Games[0].Notes = "unrelated";
        string undo = UserTools.UndoBulk(d);
        check("profiles: bulk undo restores fields only", undo.StartsWith("Undid bulk edit") && !d.Games[1].Favorite && d.Games[0].Notes == "unrelated");

        // HTML catalog
        string html = UserTools.Html(d.Games, false, true, null, CancellationToken.None);
        check("profiles: catalog escapes and omits paths", html.Contains("Puzzle Lagoon") && !html.Contains(rom) && html.Contains("3 launches"));
        check("profiles: catalog artwork callback", UserTools.Html(d.Games.Take(1), false, false, p => "QUJD", CancellationToken.None).Contains("base64,QUJD"));
        check("profiles: escape", UserTools.Escape("<a&'\">") == "&lt;a&amp;&#39;&quot;&gt;");

        // Profile transfer and reviewed sync
        var source = make();
        var file = Path.Combine(dir, "transfer.fishbowl-transfer.json");
        ProfileTransfer.Export(source, file);
        var target = make(); target.Games.RemoveAt(1); target.Games[0].Notes = "local note";
        var package = ProfileTransfer.Read(file);
        var plan = ProfileTransfer.Plan(target, package);
        check("profiles: transfer plans add and conflict", plan.Count == 2 && plan.Any(c => c.Status == "Add" && c.Incoming.Id == "g2") && plan.Any(c => c.Status.StartsWith("Conflict") && c.Incoming.Id == "g1"));
        ProfileTransfer.Apply(target, plan.Where(c => c.Status == "Add"));
        check("profiles: transfer adds new games", target.Games.Count == 2 && target.Games[0].Notes == "local note");
        target.Games[0].Path = "/elsewhere/Puzzle Lagoon.gba";
        var conflict = ProfileTransfer.Plan(target, package).Single(c => c.Incoming.Id == "g1");
        ProfileTransfer.Apply(target, new[] { conflict });
        check("profiles: chosen conflict keeps the local path", target.Games[0].Path == "/elsewhere/Puzzle Lagoon.gba" && target.Games[0].Notes == null);
        target.Games[0].Genre = "changed";
        Throws(check, "profiles: stale review rejected", () => ProfileTransfer.Apply(target, new[] { conflict }));
        var dup = make(); dup.Games[1].Id = "other";
        Throws(check, "profiles: duplicate path rejected", () => ProfileTransfer.Apply(dup, ProfileTransfer.Plan(dup, package).Where(c => c.Status.StartsWith("Duplicate"))));
        var imported = ProfileTransfer.ImportProfile(target, package);
        check("profiles: personal profile imported separately", imported.Name == "Default (imported)" && target.UserTools.Users.Count == 2);
        Throws(check, "profiles: invalid package rejected", () => ProfileTransfer.Plan(target, new TransferPackage { Schema = 2, Games = new List<GameEntry>() }));
        File.WriteAllText(Path.Combine(dir, "save (conflicted copy).sav"), "x");
        check("profiles: cloud conflict copies found", ProfileTransfer.CloudConflicts(dir, CancellationToken.None).Count == 1);

        // Launch and controller profiles
        var lib = make(); var e = lib.Emulators[0];
        var lp = ProfileTools.AddLaunchProfile(e); var lp2 = ProfileTools.AddLaunchProfile(e);
        check("profiles: launch profile names unique", lp.Name == "New profile" && lp2.Name == "New profile 2");
        lib.Games[0].LaunchProfileName = lp.Name;
        int moved = ProfileTools.UpdateLaunchProfile(lib, e, lp, " Fast ", " --fast ");
        check("profiles: rename keeps games assigned", moved == 1 && lib.Games[0].LaunchProfileName == "Fast" && lp.Arguments == "--fast");
        Throws(check, "profiles: duplicate launch profile name rejected", () => ProfileTools.UpdateLaunchProfile(lib, e, lp2, "fast", ""));
        ProfileTools.RemoveLaunchProfile(lib, e, lp);
        check("profiles: removed launch profile clears games", lib.Games[0].LaunchProfileName == null && e.LaunchProfiles.Count == 1);
        var cp = ProfileTools.AddControllerProfile(lib, e);
        ProfileTools.UpdateControllerProfile(cp, "Two pads", "Player 2 uses the left pad.");
        lib.Games[0].ControllerProfileId = cp.Id;
        check("profiles: controller reminder", ProfileTools.ControllerReminder(lib, lib.Games[0]) == cp && ProfileTools.ControllerProfiles(lib, e).Count == 1);
        ProfileTools.RemoveControllerProfile(lib, cp);
        check("profiles: removed controller profile clears reminders", lib.Games[0].ControllerProfileId == null);
        Throws(check, "profiles: controller profile needs an emulator", () => ProfileTools.AddControllerProfile(lib, null));
        var running = new PlaySession { StartedAt = DateTime.UtcNow.AddMinutes(-65).ToString("o") };
        check("profiles: break periods", ProfileTools.BreakPeriods(new UserToolSettings { BreakMinutes = 30 }, running, DateTime.UtcNow) == 2 && ProfileTools.BreakPeriods(new UserToolSettings(), running, DateTime.UtcNow) == 0);

        // Multiplayer
        var report = MultiplayerSupport.Report(e, null);
        check("profiles: multiplayer readiness", report.Contains("Ready: emulator program found.") && report.Contains("not connected to a LiveKit") && report.Contains("LOCAL PLAY"));
        check("profiles: multiplayer needs an emulator", MultiplayerSupport.Report(null, null) == "Choose an emulator before starting a session.");
        Throws(check, "profiles: relay must be https", () => MultiplayerSupport.CheckRelay("http://example.com/token"));
        check("profiles: relay blank allowed", MultiplayerSupport.CheckRelay("  ") == "");

        // Shortcut text
        check("profiles: shortcut normalize", ShortcutText.Normalize("p, control") == "Control, P" && ShortcutText.Normalize("ctrl+shift+1") == "Control, Shift, D1" && ShortcutText.Normalize("F11") == "F11");
        check("profiles: shortcut invalid", ShortcutText.Normalize("Control") == null && ShortcutText.Normalize("A, B") == null);
        Throws(check, "profiles: shortcut conflict", () => ShortcutText.Validate(new Dictionary<string, string> { { "Home", "Control, H" }, { "Library", "H, Control" } }));
        Throws(check, "profiles: reserved shortcut", () => ShortcutText.Validate(new Dictionary<string, string> { { "Home", "Escape" } }));
        var keyed = make(); keyed.Enhancements = new NextSettings { Shortcuts = new Dictionary<string, string> { { "Home", "Alt, H" } } };
        check("profiles: shortcut action lookup", ShortcutText.Action(keyed, false, false, true, "H") == "Home" && ShortcutText.Action(keyed, true, false, false, "L") == "Library" && ShortcutText.Action(keyed, true, false, false, "H") == null);
    }
}
