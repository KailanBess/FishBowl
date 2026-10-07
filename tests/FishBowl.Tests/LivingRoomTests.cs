using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorHub;

// Living-room Library and Immersion logic shared with Windows (FishBowl.LivingRoom.cs), plus Linux evdev input.
public static class LivingRoomTests
{
    public static void Run(Action<string, bool> check)
    {
        var d = new LibraryData { Games = new List<GameEntry>() };
        Func<string, GameEntry> add = title => { var g = new GameEntry { Id = title, Title = title, Path = "/games/" + title, Tags = new List<string>() }; d.Games.Add(g); return g; };
        var alpha = add("Alpha Test"); var beta = add("beta sample"); var gamma = add("Gamma Mock"); var delta = add("Delta Stub");
        alpha.Favorite = true; gamma.Favorite = true;
        beta.LaunchCount = 2; beta.LastLaunched = "2026-10-01T10:00:00Z"; delta.LaunchCount = 1; delta.LastLaunched = "2026-10-03T10:00:00Z";
        gamma.LaunchCount = 5; gamma.LastLaunched = "2026-10-04T10:00:00Z"; gamma.PlayStatus = "Completed";
        alpha.AddedAt = "2026-09-01T00:00:00Z"; beta.AddedAt = "2026-09-20T00:00:00Z"; gamma.AddedAt = "2026-09-10T00:00:00Z";
        d.Hub = null;
        var settings = Immersion.Ensure(d);
        check("immersion settings are created with Windows defaults", settings != null && d.Hub.Immersion == settings && settings.Transitions && !settings.Sounds && !settings.Ambient && settings.IdleMinutes == 3 && d.Hub.Activity != null);
        check("living room All games sorts by title ignoring case", LivingRoom.Games(d, "All games", "").Select(g => g.Title).SequenceEqual(new[] { "Alpha Test", "beta sample", "Delta Stub", "Gamma Mock" }));
        check("Favorites shelf lists only favorites", LivingRoom.Games(d, "Favorites", null).Select(g => g.Id).SequenceEqual(new[] { "Alpha Test", "Gamma Mock" }));
        check("Continue playing skips completed and unplayed games, newest first", LivingRoom.Games(d, "Continue playing", "").Select(g => g.Id).SequenceEqual(new[] { "Delta Stub", "beta sample" }));
        check("Recently added is newest first", LivingRoom.Games(d, "Recently added", "").First().Id == "beta sample");
        check("living room search matches across fields", LivingRoom.Games(d, "All games", "  STUB ").Single().Id == "Delta Stub");
        check("shelf cycling wraps both ways", LivingRoom.NextShelf("Favorites", true) == "All games" && LivingRoom.NextShelf("All games", false) == "Favorites" && LivingRoom.NextShelf("unknown", true) == "Continue playing");
        check("grid fits columns to width", LivingRoom.Columns(1000, 200, 20) == 5 && LivingRoom.Columns(150, 200, 20) == 1 && LivingRoom.Columns(double.NaN, 200, 20) == 1);
        check("grid left/right wrap in reading order", LivingRoom.Move(0, 7, 3, CouchAction.Left) == 6 && LivingRoom.Move(6, 7, 3, CouchAction.Right) == 0);
        check("grid up/down keep the column and stop at edges", LivingRoom.Move(1, 7, 3, CouchAction.Down) == 4 && LivingRoom.Move(1, 7, 3, CouchAction.Up) == 1 && LivingRoom.Move(6, 7, 3, CouchAction.Down) == 6);
        check("grid down into a shorter last row lands on its last tile", LivingRoom.Move(5, 7, 3, CouchAction.Down) == 6);
        check("grid handles empty and out-of-range selections", LivingRoom.Move(0, 0, 3, CouchAction.Down) == -1 && LivingRoom.Move(9, 4, 3, CouchAction.Up) == 0);
        check("play time wording", LivingRoom.PlayTime(0) == "Not played yet" && LivingRoom.PlayTime(600) == "10 min played" && LivingRoom.PlayTime(5400) == "1.5 hours played");
        var wave = Immersion.Wave(50, true);
        check("interface sound is a valid PCM wave", wave.Length == 44 + 22050 * 180 / 1000 * 2 && System.Text.Encoding.ASCII.GetString(wave, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(wave, 8, 4) == "WAVE");
        check("muted interface sound is silent", Immersion.Wave(0, false).Skip(44).All(b => b == 0));
        Immersion.ApplyPreset(d, "Retro");
        check("Retro preset sets the coordinated palette", d.Cosmetics.CustomPalette && d.Cosmetics.AccentColor == "#E7C67F" && d.Cosmetics.BackgroundStyle == "Dots");
        bool rejected = false; try { Immersion.ApplyPreset(d, "Neon"); } catch (ArgumentException) { rejected = true; }
        check("unknown preset is rejected", rejected);
        var session = new PlaySession { Id = "s", GameId = "Alpha Test", Seconds = 90 };
        check("recap is off by default", !Immersion.OfferRecap(d, session));
        settings.Recap = true;
        check("recap offered for a verified session", Immersion.OfferRecap(d, session) && !Immersion.OfferRecap(d, new PlaySession { Uncertain = true }));
        Immersion.SaveRecap(alpha, session, "  Beat the first area. ", 9);
        check("recap stores rating clamped and the note on session and game", alpha.PersonalRating == 5 && session.Note == "Beat the first area." && alpha.Notes.EndsWith("] Beat the first area."));
        bool tooLong = false; try { Immersion.SaveRecap(alpha, session, new string('x', 2001), 1); } catch (ArgumentException) { tooLong = true; }
        check("recap notes are limited to 2000 characters", tooLong && alpha.PersonalRating == 5);
        var now = DateTime.UtcNow;
        check("ambient waits for opt-in and idle time", !Immersion.AmbientDue(d, now.AddMinutes(-10), now));
        settings.Ambient = true;
        check("ambient starts after idle minutes", Immersion.AmbientDue(d, now.AddMinutes(-3), now) && !Immersion.AmbientDue(d, now.AddMinutes(-2), now));

        var pad = new EvdevInput();
        check("evdev A press activates, release does not", pad.Read(1, EvdevInput.South, 1, 0).SequenceEqual(new[] { ControllerAction.Activate }) && pad.Read(1, EvdevInput.South, 0, 5).Count == 0);
        check("evdev face buttons map to back, details and favorite", pad.Read(1, EvdevInput.East, 1, 10)[0] == ControllerAction.Back && pad.Read(1, EvdevInput.North, 1, 11)[0] == ControllerAction.Details && pad.Read(1, EvdevInput.West, 1, 12)[0] == ControllerAction.Favorite);
        check("evdev shoulders and start", pad.Read(1, EvdevInput.LeftShoulder, 1, 13)[0] == ControllerAction.PreviousTab && pad.Read(1, EvdevInput.RightShoulder, 1, 14)[0] == ControllerAction.NextTab && pad.Read(1, EvdevInput.Start, 1, 15)[0] == ControllerAction.Menu);
        check("evdev hat directional pad moves and repeats", pad.Read(3, EvdevInput.HatY, 1, 100).SequenceEqual(new[] { ControllerAction.Down }) && pad.Repeat(449).Count == 0 && pad.Repeat(450).SequenceEqual(new[] { ControllerAction.Down }));
        check("evdev hat release stops", pad.Read(3, EvdevInput.HatY, 0, 500).Count == 0 && pad.Repeat(5000).Count == 0);
        pad.Range(EvdevInput.StickX, 0, 255);
        check("evdev stick uses the device range and deadzone", pad.Read(3, EvdevInput.StickX, 140, 600).Count == 0 && pad.Read(3, EvdevInput.StickX, 10, 610).SequenceEqual(new[] { ControllerAction.Left }));
        pad.Read(3, EvdevInput.StickX, 128, 620);
        check("evdev button directional pad", pad.Read(1, EvdevInput.DpadRight, 1, 700).SequenceEqual(new[] { ControllerAction.Right }) && pad.Read(1, EvdevInput.DpadRight, 0, 710).Count == 0);
        check("evdev gamepad capability detection", EvdevInput.IsGamepad("7fdb000000000000 0 0 0 0") && !EvdevInput.IsGamepad("0 0 0 0 0") && !EvdevInput.IsGamepad("1f") && !EvdevInput.IsGamepad(null));
    }
}
