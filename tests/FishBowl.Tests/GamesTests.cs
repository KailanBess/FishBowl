using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmulatorHub;

// Shared game library logic (FishBowl.Games.cs): filters, collections, lists, launch plans and sessions.
public static class GamesTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        var dir = Path.Combine(root, "games-tests"); Directory.CreateDirectory(dir);
        string rom = Path.Combine(dir, "Test Quest.gba"), iso = Path.Combine(dir, "Sample Racer.iso"), script = Path.Combine(dir, "Native Demo.sh");
        File.WriteAllText(rom, "x"); File.WriteAllText(iso, "x"); File.WriteAllText(script, "#!/bin/sh\n");
        var emu = Path.Combine(dir, "emu.sh"); File.WriteAllText(emu, "#!/bin/sh\n");
        var lib = new LibraryData
        {
            Emulators = new List<EmulatorProfile> {
                new EmulatorProfile { Id = "e1", Name = "GBA emu", Executable = emu, Arguments = "-f {game}", Extensions = new List<string> { ".gba" },
                    LaunchProfiles = new List<LaunchProfile> { new LaunchProfile { Name = "Fast", Arguments = "--fast" } } },
                new EmulatorProfile { Id = "e2", Name = "Other", Executable = emu } },
            Games = new List<GameEntry> {
                new GameEntry { Id = "a", Title = "Test Quest", Path = rom, EmulatorId = "e1", Favorite = true, Tags = new List<string> { "rpg" }, LastLaunched = "2026-10-01T10:00:00.0000000Z", TotalPlaySeconds = 50 },
                new GameEntry { Id = "b", Title = "Sample Racer", Path = iso, EmulatorId = "e2", PlayStatus = "Playing", LastLaunched = "2026-10-03T10:00:00.0000000Z", TotalPlaySeconds = 500 },
                new GameEntry { Id = "c", Title = "Missing Thing", Path = Path.Combine(dir, "gone.gba"), RequiresEmulatorAssignment = true },
                new GameEntry { Id = "d", Title = "Native Demo", Path = script, Arguments = "--windowed", Extras = new GameExtras { Native = true } } },
            Collections = new List<GameCollection> {
                new GameCollection { Id = "p", Name = "Parent", GameIds = new List<string> { "a" } },
                new GameCollection { Id = "k", Name = "Kid", ParentId = "p", GameIds = new List<string> { "b" } },
                new GameCollection { Id = "s", Name = "Smart favorites", IsSmart = true, SmartRule = "Favorites" } }
        };
        Func<IEnumerable<GameEntry>, string> ids = g => String.Join("", g.Select(x => x.Id));
        check("games: scope favorites", ids(GameLibraryQuery.Scope(lib, lib.Games, "Favorites")) == "a");
        check("games: scope missing", ids(GameLibraryQuery.Scope(lib, lib.Games, "Missing")) == "c");
        check("games: scope recent newest first", ids(GameLibraryQuery.Scope(lib, lib.Games, "Recent")) == "ba");
        check("games: nested collection includes child", ids(GameLibraryQuery.Scope(lib, lib.Games, "Collection: Parent")) == "ab");
        check("games: smart collection", ids(GameLibraryQuery.Scope(lib, lib.Games, "Collection: Smart favorites")) == "a");
        check("games: unassigned filter", ids(GameLibraryQuery.Filter(lib, lib.Games, null, "Unassigned", null, null)) == "cd");
        check("games: emulator filter", ids(GameLibraryQuery.Filter(lib, lib.Games, null, "GBA emu", null, null)) == "a");
        check("games: tag filter", ids(GameLibraryQuery.Filter(lib, lib.Games, null, null, null, "rp")) == "a");
        check("games: console filter", ids(GameLibraryQuery.Filter(lib, lib.Games, "Game Boy Advance", null, null, null)) == "ac");
        check("games: sort by play time", ids(GameLibraryQuery.Sort(lib.Games, "Play time")).StartsWith("ba"));
        check("games: sort by title", ids(GameLibraryQuery.Sort(lib.Games, "Title")) == "cdba");
        check("games: search", GameLibraryQuery.Matches(lib.Games[0], "rpg") && !GameLibraryQuery.Matches(lib.Games[1], "rpg"));
        check("games: scope choices list collections", GameLibraryQuery.ScopeChoices(lib).Contains("Collection: Kid"));
        var tree = GameCollections.Tree(lib);
        check("games: collection tree nests child", tree.Count == 3 && tree[0].Key.Id == "p" && tree[1].Key.Id == "k" && tree[1].Value == 1);
        bool cycle = false; try { GameCollections.SetParent(lib, lib.Collections[0], "k"); } catch (IOException) { cycle = true; }
        check("games: collection cycle refused", cycle && lib.Collections[0].ParentId == null);
        GameCollections.Delete(lib, lib.Collections[0]);
        check("games: deleting a parent lifts children", lib.Collections.First(c => c.Id == "k").ParentId == null);

        GameLists.Enqueue(lib, lib.Games[1]); GameLists.Enqueue(lib, lib.Games[0]); GameLists.Enqueue(lib, lib.Games[0]);
        check("games: queue keeps unique order", ids(GameLists.QueueGames(lib)) == "ba");
        check("games: move queue", GameLists.MoveQueue(lib, "a", -1) && ids(GameLists.QueueGames(lib)) == "ab" && !GameLists.MoveQueue(lib, "a", -1));
        check("games: smart list match", ids(GameLists.Match(lib, new SmartLibraryList { Status = "Playing" })) == "b");
        check("games: surprise skips missing files", !GameLists.SurpriseCandidates(lib, false, null).Any(g => g.Id == "c"));
        var csv = GameLists.Csv(lib.Games.Take(1));
        check("games: csv header and row", csv.StartsWith("Title,Platform") && csv.Contains("\"Test Quest\""));
        check("games: csv neutralises formulas", GameLists.CsvCell("=SUM(A1)") == "\"'=SUM(A1)\"");

        var plan = GamePlay.Prepare(lib, lib.Games[0]);
        check("games: launch plan uses emulator and {game}", plan.Program == emu && plan.Arguments.SequenceEqual(new[] { "-f", rom }));
        lib.Games[0].LaunchProfileName = "Fast";
        check("games: launch profile arguments", GamePlay.Prepare(lib, lib.Games[0]).Arguments.SequenceEqual(new[] { "-f", rom, "--fast" }));
        lib.Games[0].LaunchProfileName = "Gone"; bool refused = false; try { GamePlay.Prepare(lib, lib.Games[0]); } catch (IOException) { refused = true; }
        check("games: missing launch profile refused", refused); lib.Games[0].LaunchProfileName = null;
        check("games: emulator without {game} appends path", GamePlay.Prepare(lib, lib.Games[1]).Arguments.Last() == iso);
        var native = GamePlay.Prepare(lib, lib.Games[3]);
        check("games: native game runs directly", native.Program == script && native.Arguments.SequenceEqual(new[] { "--windowed" }));
        refused = false; try { GamePlay.Prepare(lib, lib.Games[2]); } catch (IOException) { refused = true; }
        check("games: game awaiting reassignment is refused", refused);
        lib.Games[0].Discs = new List<string> { rom, iso }; lib.Games[0].LastDiscPath = iso;
        check("games: launch path follows last disc", GamePlay.LaunchPath(lib.Games[0]) == iso); lib.Games[0].LastDiscPath = "elsewhere";
        check("games: unknown last disc falls back", GamePlay.LaunchPath(lib.Games[0]) == rom);

        var session = GamePlay.BeginSession(lib, lib.Games[1], emu, true);
        check("games: begin session records game and last game", session.GameId == "b" && lib.Enhancements.LastGameId == "b" && lib.PlaySessions.Count == 1);
        GamePlay.CompleteSession(lib, lib.Games[1], session, 120);
        check("games: complete session adds playtime", lib.Games[1].TotalPlaySeconds == 620 && session.EndedAt != null);
        GamePlay.CorrectSession(lib, session, 60, "fixed");
        check("games: correct session adjusts total", lib.Games[1].TotalPlaySeconds == 560 && session.Corrected);
        GamePlay.AddManualSession(lib, lib.Games[1], DateTime.Now.AddHours(-1), 600, null);
        var week = GamePlay.WeekTotals(lib, DateTime.Now);
        check("games: week totals cover 7 days and today", week.Count == 7 && week[DateTime.Now.ToString("yyyy-MM-dd")] >= 660);
        check("games: Windows local dates parse", GameLibraryQuery.Date(new DateTime(2026, 1, 2, 3, 4, 0).ToString("g")).Year == 2026);
        GamePlay.RecordLaunch(lib, lib.Games[0], DateTime.UtcNow);
        check("games: record launch", lib.Games[0].LaunchCount == 1 && GamePlay.LastGame(lib).Id == "a" && GamePlay.Recent(lib, 1)[0].Id == "a");
        check("games: duration text", GamePlay.Duration(3725) == "1 h 02 min" && GamePlay.Duration(125) == "2 min");
    }
}
