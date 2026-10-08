using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        private readonly Dictionary<string, SessionTracker> sessionTrackers = new Dictionary<string, SessionTracker>();
        private readonly DispatcherTimer sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private long lastSessionSave;
        private string lastSessionError;

        private void ConfigureSessionTracking()
        {
            RecoverLibrarySessions();
            sessionTimer.Tick += delegate { PollLibrarySessions(false); }; sessionTimer.Start();
        }
        private void RecoverLibrarySessions()
        {
            try
            {
                UserTools.Ensure(library);
                var recovered = SessionLedger.Recover(Store.DataDirectory, library);
                if (recovered.Count > 0) { UserTools.SaveActive(library); Store.Save(library); }
                foreach (var tracker in recovered)
                {
                    if (tracker.Finished) { SessionLedger.Commit(tracker); tracker.Dispose(); }
                    else sessionTrackers[tracker.SessionId] = tracker;
                }
                UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches;
            }
            catch (Exception error) { Store.Log("Session recovery deferred: " + error.Message); pendingWarnings.Add("Play-session recovery could not finish. Its journal was retained.\n\n" + error.Message); }
        }
        private void StartLibrarySession(GameEntry game, Process process, List<SessionProcess> beforeLaunch, DateTime started, string program)
        {
            if (TrackGuestLaunch(game, process)) return;
            UserTools.Ensure(library);
            var record = GamePlay.BeginSession(library, game, program, true);
            record.StartedAt = started.ToString("o");
            try
            {
                var tracker = SessionLedger.Start(Store.DataDirectory, record, process, beforeLaunch, SessionLedger.ProfileKey(library));
                sessionTrackers[tracker.SessionId] = tracker; gameProcesses[game.Id] = process;
                UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches;
                game.SessionTrackingNote = "Tracks the verified launched process and descendants. Saved checkpoints exclude time FishBowl was closed.";
            }
            catch (Exception error)
            {
                // An instant Linux launcher may disappear before its exact /proc identity can be read.
                // A successful launch remains successful; never attach unrelated or pre-existing processes.
                record.EndedAt = DateTime.UtcNow.ToString("o"); record.Uncertain = true;
                record.Note = "The launch could not obtain a verified process identity; no playtime was inferred.";
                game.SessionTrackingNote = record.Note; Store.Log("Session tracking unavailable: " + error.Message); process.Dispose(); return;
            }
            // Event registration is an optimization. The timer still tracks the verified tree if it fails.
            try
            {
                process.Exited += delegate { Ui.Post(() => PollLibrarySessions(true)); }; process.EnableRaisingEvents = true;
                if (process.HasExited) Ui.Post(() => PollLibrarySessions(true));
            }
            catch (Exception error) { Store.Log("Session exit notification unavailable; polling remains active: " + error.Message); }
        }
        private void PollLibrarySessions(bool forceSave)
        {
            if (sessionTrackers.Count == 0) return;
            try
            {
                foreach (var tracker in sessionTrackers.Values) { tracker.Poll(); SessionLedger.Apply(library, tracker, tracker.Finished); }
                var completed = sessionTrackers.Values.Where(t => t.Finished).ToArray();
                if (forceSave || completed.Length > 0 || Environment.TickCount64 - lastSessionSave >= 5000)
                {
                    UserTools.SaveActive(library); Store.Save(library); lastSessionSave = Environment.TickCount64;
                    foreach (var tracker in completed)
                    {
                        SessionLedger.Commit(tracker); sessionTrackers.Remove(tracker.SessionId); gameProcesses.Remove(tracker.GameId); OfferSessionRecap(tracker); tracker.Dispose();
                    }
                    UserTools.ActiveLaunches = sessionTrackers.Count + guestLaunches;
                    if (completed.Length > 0) { RefreshGameLibrary(); if (livingRoom != null) livingRoom.RefreshGames(); }
                }
                lastSessionError = null;
            }
            catch (Exception error)
            {
                if (lastSessionError != error.Message) { Store.Log("Session checkpoint deferred: " + error.Message); lastSessionError = error.Message; SetStatus("Play-session journal retained; library checkpoint will retry."); }
            }
        }
        private void FinishLibraryGameSessionsAtClose()
        {
            sessionTimer.Stop();
            try
            {
                foreach (var tracker in sessionTrackers.Values) { tracker.Poll(); SessionLedger.Apply(library, tracker, tracker.Finished); }
                UserTools.SaveActive(library); Store.Save(library);
                foreach (var tracker in sessionTrackers.Values) { if (tracker.Finished) SessionLedger.Commit(tracker); tracker.Dispose(); }
                sessionTrackers.Clear(); gameProcesses.Clear(); UserTools.ActiveLaunches = guestLaunches;
            }
            catch { sessionTimer.Start(); throw; }
        }
    }
}
