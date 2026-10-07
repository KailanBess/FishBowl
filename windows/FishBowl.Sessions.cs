using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class WindowsSessions
    {
        static readonly Dictionary<string, Timer> watchers = new Dictionary<string, Timer>();
        public static void Recover(IWin32Window owner, LibraryData library)
        {
            if (UserTools.Guest) return;
            try
            {
                UserTools.Ensure(library);
                var recovered = SessionLedger.Recover(Store.DataDirectory, library);
                if (recovered.Count > 0) Store.Save(library);
                foreach (var tracker in recovered)
                {
                    if (tracker.Finished) { SessionLedger.Commit(tracker); tracker.Dispose(); }
                    else Watch(owner, library, tracker);
                }
            }
            catch (Exception error) { Store.Log("Session recovery: " + error.Message); }
        }

        public static void Track(IWin32Window owner, LibraryData library, GameEntry game, PlaySession session, Process process, IEnumerable<SessionProcess> beforeLaunch)
        {
            if (UserTools.Guest)
            {
                session.Uncertain = true;
                session.Note = "Guest launch: no durable session journal is stored.";
                if (process != null)
                {
                    UserTools.ActiveLaunches++;
                    Task.Factory.StartNew(delegate
                    {
                        try { process.WaitForExit(); }
                        catch (Exception error) { Store.Log("Guest process tracking: " + error.Message); }
                        finally { System.Threading.Interlocked.Decrement(ref UserTools.ActiveLaunches); process.Dispose(); }
                    });
                }
                return;
            }
            try
            {
                UserTools.Ensure(library);
                var tracker = SessionLedger.Start(Store.DataDirectory, session, process, beforeLaunch, SessionLedger.ProfileKey(library));
                game.SessionTrackingNote = "Tracking the launched process and verified descendants; active time is checkpointed.";
                Watch(owner, library, tracker);
            }
            catch (Exception error)
            {
                session.Uncertain = true;
                session.Note = "Session tracking could not verify the launched process: " + error.Message;
                Store.Log(session.Note); Store.Save(library);
                if (process != null) process.Dispose();
            }
        }

        static void Watch(IWin32Window owner, LibraryData library, SessionTracker tracker)
        {
            if (watchers.ContainsKey(tracker.SessionId)) { tracker.Dispose(); return; }
            Timer timer = new Timer { Interval = 500 };
            watchers.Add(tracker.SessionId, timer);
            UserTools.ActiveLaunches++;
            UserTools.ActiveSessions.TryAdd(tracker.SessionId, 0);
            DateTime saved = DateTime.UtcNow;
            bool released = false;
            Task polling = null;
            Action release = delegate
            {
                if (released) return;
                released = true; timer.Stop(); timer.Dispose(); watchers.Remove(tracker.SessionId);
                UserTools.ActiveLaunches = Math.Max(0, UserTools.ActiveLaunches - 1);
                byte ignored; UserTools.ActiveSessions.TryRemove(tracker.SessionId, out ignored);
                tracker.Dispose();
            };
            EventHandler shutdown = null;
            shutdown = delegate
            {
                try
                {
                    if (polling != null) polling.Wait(1000);
                    tracker.Poll();
                    SessionLedger.Apply(library, tracker, tracker.Finished);
                    Store.Save(library); SessionLedger.Commit(tracker);
                }
                catch (Exception error) { Store.Log("Session shutdown checkpoint: " + error.Message); }
                finally { Application.ApplicationExit -= shutdown; release(); }
            };
            Application.ApplicationExit += shutdown;
            timer.Tick += delegate
            {
                try
                {
                    if (polling == null)
                    {
                        polling = Task.Factory.StartNew(delegate { tracker.Poll(); });
                        return;
                    }
                    if (!polling.IsCompleted) return;
                    if (polling.IsFaulted) throw polling.Exception;
                    polling = null;
                    if (!tracker.Finished && (DateTime.UtcNow - saved).TotalSeconds < 10) return;
                    SessionLedger.Apply(library, tracker, tracker.Finished);
                    Store.Save(library); saved = DateTime.UtcNow;
                    if (!tracker.Finished) return;
                    SessionLedger.Commit(tracker);
                    Application.ApplicationExit -= shutdown; release();
                    GameEntry game = library.Games.FirstOrDefault(g => g.Id == tracker.GameId);
                    PlaySession session = library.PlaySessions.FirstOrDefault(s => s.Id == tracker.SessionId);
                    if (game != null && session != null) Immersion.AfterSession(owner, library, game, session);
                }
                catch (Exception error)
                {
                    Store.Log("Session checkpoint: " + error.Message);
                    Application.ApplicationExit -= shutdown; release();
                }
            };
            timer.Start();
        }
    }
}
