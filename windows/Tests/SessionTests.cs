using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EmulatorHub;

class SessionTests
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static SessionProcess Identity(int pid, int parent, DateTime start) { return new SessionProcess { Pid = pid, ParentPid = parent, StartedUtcTicks = start.Ticks }; }
    static LibraryData Library(PlaySession session)
    {
        return new LibraryData { Games = new List<GameEntry> { new GameEntry { Id = session.GameId, TotalPlaySeconds = 40 } }, PlaySessions = new List<PlaySession> { session } };
    }
    static PlaySession Session() { return new PlaySession { Id = Guid.NewGuid().ToString("N"), GameId = "fixture", StartedAt = DateTime.UtcNow.ToString("o") }; }
    static int Main()
    {
        try { Run(); Console.WriteLine("PASS: " + checks + " crash-safe session identity, descendants, checkpoint and recovery checks."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FishBowl-session-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        DateTime now = DateTime.UtcNow;
        var root = Identity(101, 9, now);
        var session = Session(); var library = Library(session);
        using (var tracker = SessionLedger.StartObserved(directory, session, root, null, now))
        {
            Check(File.Exists(Path.Combine(directory, "active-sessions", session.Id + ".json")), "initial journal exists before timing begins");
            tracker.Poll(new[] { root }, now.AddSeconds(5));
            Check(tracker.Seconds == 5, "verified process accrues active elapsed seconds");
            Check(SessionLedger.Apply(library, tracker, false), "checkpoint applies to exact session");
            Check(library.Games[0].TotalPlaySeconds == 45 && session.Seconds == 5, "checkpoint adds delta to game total");
            SessionLedger.Apply(library, tracker, false);
            Check(library.Games[0].TotalPlaySeconds == 45, "repeated checkpoint does not double count");
            var child = Identity(102, 101, now.AddSeconds(1));
            tracker.Poll(new[] { root, child }, now.AddSeconds(6));
            tracker.Poll(new[] { child }, now.AddSeconds(10));
            Check(tracker.Seconds == 10 && !tracker.Finished, "child survives launcher exit");
            var recovered = SessionLedger.Recover(directory, library, new[] { child }, now.AddHours(1));
            Check(recovered.Count == 1 && !recovered[0].Finished, "recovery resumes exact surviving descendant");
            Check(session.Seconds == 10 && library.Games[0].TotalPlaySeconds == 50, "recovery replays only durable delta");
            using (var resumed = recovered[0])
            {
                resumed.Poll(new[] { child }, now.AddHours(1).AddSeconds(2));
                Check(resumed.Seconds == 12, "app downtime is excluded from resumed clock");
                resumed.Poll(new SessionProcess[0], now.AddHours(1).AddSeconds(3));
                resumed.Poll(new SessionProcess[0], now.AddHours(1).AddSeconds(6));
                Check(resumed.Finished && resumed.Seconds == 13, "empty grace is not counted as playing time");
                SessionLedger.Apply(library, resumed, true);
                Check(session.EndedAt != null && library.Games[0].TotalPlaySeconds == 53, "finalization records exact delta once");
                SessionLedger.Apply(library, resumed, true);
                Check(library.Games[0].TotalPlaySeconds == 53, "duplicate completion is harmless");
                SessionLedger.Commit(resumed);
                Check(!File.Exists(Path.Combine(directory, "active-sessions", session.Id + ".json")), "completed journal removed only after explicit commit");
            }
        }
        session = Session(); library = Library(session);
        using (var tracker = SessionLedger.StartObserved(directory, session, root, null, now))
        {
            tracker.Poll(new[] { root }, now.AddSeconds(4));
            var reused = Identity(101, 9, now.AddSeconds(5));
            var stranger = Identity(103, 101, now.AddSeconds(6));
            tracker.Poll(new[] { reused, stranger }, now.AddSeconds(7));
            tracker.Poll(new[] { reused, stranger }, now.AddSeconds(10));
            Check(tracker.Finished, "PID reuse does not resume root or adopt its unrelated child");
            var other = Library(Session());
            Check(SessionLedger.Recover(directory, other, new SessionProcess[0], now.AddHours(1)).Count == 0, "different profile does not consume journal");
            Check(File.Exists(Path.Combine(directory, "active-sessions", session.Id + ".json")), "unknown profile journal remains available");
            var restored = SessionLedger.Recover(directory, library, new SessionProcess[0], now.AddHours(1));
            Check(restored.Count == 1 && restored[0].Finished, "dead process journal conservatively completes");
            Check(session.Seconds == 7 && library.Games[0].TotalPlaySeconds == 47, "dead recovery retains checkpoint but excludes restart downtime");
            restored[0].Dispose();
        }
        session = Session(); root = Identity(201, 9, now); root.EndedUtcTicks = now.AddSeconds(1).Ticks;
        var spawned = Identity(202, 201, now.AddMilliseconds(500));
        var unrelated = Identity(203, 201, now.AddSeconds(2));
        using (var tracker = SessionLedger.StartObserved(directory, session, root, null, now))
        {
            tracker.Poll(new[] { spawned, unrelated }, now.AddSeconds(3));
            tracker.Poll(new[] { unrelated }, now.AddSeconds(4));
            tracker.Poll(new[] { unrelated }, now.AddSeconds(7));
            Check(tracker.Finished, "fast-launch child verified within retained root lifetime; later parent-PID reuse rejected");
        }
        session = Session(); root = Identity(301, 9, now);
        var oldChild = Identity(302, 301, now.AddSeconds(1));
        using (var tracker = SessionLedger.StartObserved(directory, session, root, new[] { oldChild }, now))
        {
            tracker.Poll(new[] { root, oldChild }, now.AddSeconds(1));
            tracker.Poll(new[] { oldChild }, now.AddSeconds(2));
            tracker.Poll(new[] { oldChild }, now.AddSeconds(5));
            Check(tracker.Finished, "pre-launch process identities are never adopted");
        }
        session = Session(); library = Library(session);
        library.UserTools = new UserToolSettings { ActiveId = "owner-one" };
        using (var owned = SessionLedger.StartObserved(directory, session, root, null, now, SessionLedger.ProfileKey(library)))
        {
            owned.Poll(new[] { root }, now.AddSeconds(3));
            var cloned = Library(new PlaySession { Id = session.Id, GameId = session.GameId });
            cloned.UserTools = new UserToolSettings { ActiveId = "owner-two" };
            Check(!SessionLedger.Apply(cloned, owned, false), "copied session UUID cannot apply to another owner");
            Check(SessionLedger.Recover(directory, cloned, new[] { root }, now.AddHours(1)).Count == 0, "copied profile history cannot claim owner journal");
            Check(cloned.Games[0].TotalPlaySeconds == 40 && File.Exists(Path.Combine(directory, "active-sessions", session.Id + ".json")), "foreign journal remains unchanged for its owner");
            var recovered = SessionLedger.Recover(directory, library, new[] { root }, now.AddHours(1));
            Check(recovered.Count == 1 && session.Seconds == 3, "owning profile can recover its journal");
            recovered[0].Dispose();
        }
        bool rejected = false;
        try { SessionLedger.StartObserved(directory, new PlaySession { Id = "../escape", GameId = "fixture" }, root, null, now); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "session identity cannot escape journal directory");
        File.WriteAllText(Path.Combine(directory, "active-sessions", "invalid.json"), "not json");
        Check(SessionLedger.Recover(directory, Library(Session()), new SessionProcess[0], now).Count == 0, "malformed journal safely ignored without consuming other profiles");
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            string child = Path.Combine(directory, "Child.exe");
            string childSource = Path.Combine(directory, "Child.cs");
            string launcher = Path.Combine(directory, "Launcher.exe");
            string launcherSource = Path.Combine(directory, "Launcher.cs");
            File.WriteAllText(childSource, "class Child { static void Main(){System.Threading.Thread.Sleep(1800);} }");
            File.WriteAllText(launcherSource, "using System.Diagnostics;class Launcher {static void Main(string[] args){Process.Start(new ProcessStartInfo{FileName=args[0],UseShellExecute=false,CreateNoWindow=true});System.Threading.Thread.Sleep(100);}}");
            string compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            foreach (string source in new[] { childSource, launcherSource })
            {
                string output = Path.ChangeExtension(source, ".exe");
                using (var compile = Process.Start(new ProcessStartInfo { FileName = compiler, Arguments = "/nologo /target:winexe /out:\"" + output + "\" \"" + source + "\"", UseShellExecute = false, CreateNoWindow = true }))
                {
                    compile.WaitForExit(); Check(compile.ExitCode == 0, "real handoff process fixture compiles");
                }
            }
            var before = SessionLedger.ProcessSnapshot();
            session = Session(); library = Library(session);
            var process = Process.Start(new ProcessStartInfo { FileName = launcher, Arguments = "\"" + child + "\"", UseShellExecute = false, CreateNoWindow = true });
            using (var tracker = SessionLedger.Start(directory, session, process, before))
            {
                bool followedChild = false;
                var clock = Stopwatch.StartNew();
                while (!tracker.Finished && clock.Elapsed.TotalSeconds < 8)
                {
                    tracker.Poll();
                    if (process.HasExited && !tracker.Finished && clock.Elapsed.TotalMilliseconds < 1400) followedChild = true;
                    System.Threading.Thread.Sleep(100);
                }
                Check(followedChild, "actual launcher exits while verified child remains tracked");
                Check(tracker.Finished && tracker.Seconds >= 1, "actual child handoff finishes after child lifetime");
                SessionLedger.Apply(library, tracker, true); SessionLedger.Commit(tracker);
                Check(library.Games[0].TotalPlaySeconds == 40 + session.Seconds, "actual process completion adds one total");
            }
        }
        using (Process own = Process.GetCurrentProcess())
        {
            var snapshot = SessionLedger.ProcessSnapshot();
            Check(snapshot.Any(p => p.Pid == own.Id && p.StartedUtcTicks == own.StartTime.ToUniversalTime().Ticks), "native snapshot verifies actual current process identity");
            Check(snapshot.All(p => p.Pid > 0 && p.StartedUtcTicks > 0), "native snapshots only expose verified identities");
            bool existingRejected = false;
            try { SessionLedger.Start(directory, Session(), own, snapshot); }
            catch (InvalidOperationException) { existingRejected = true; }
            Check(existingRejected, "already-running shell process returned by launcher is not attributed to game");
        }
    }
}
