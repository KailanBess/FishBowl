using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EmulatorHub
{
    public sealed class SessionProcess
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public long StartedUtcTicks { get; set; }
        public long EndedUtcTicks { get; set; }
        public string Key { get { return Pid.ToString(CultureInfo.InvariantCulture) + ":" + StartedUtcTicks.ToString(CultureInfo.InvariantCulture); } }
    }

    public sealed class SessionCheckpoint
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
        public string SessionId { get; set; }
        public string OwnerKey { get; set; }
        public string GameId { get; set; }
        public double ElapsedSeconds { get; set; }
        public string CheckedUtc { get; set; }
        public bool Finished { get; set; }
        public List<SessionProcess> Processes { get; set; }
    }

    public sealed class SessionTracker : IDisposable
    {
        internal readonly string FileName;
        internal readonly SessionCheckpoint State;
        readonly object gate = new object();
        bool disposed;
        readonly Process launched;
        readonly HashSet<string> beforeLaunch;
        DateTime lastPoll;
        DateTime? emptySince;
        bool previousActive;
        public string SessionId { get { return State.SessionId; } }
        public string GameId { get { return State.GameId; } }
        public long Seconds { get { return (long)Math.Floor(State.ElapsedSeconds); } }
        public bool Finished { get { return State.Finished; } }

        internal SessionTracker(string file, SessionCheckpoint checkpoint, Process process, IEnumerable<SessionProcess> before, DateTime now)
        {
            FileName = file; State = checkpoint; launched = process;
            beforeLaunch = new HashSet<string>((before ?? new SessionProcess[0]).Select(p => p.Key), StringComparer.Ordinal);
            lastPoll = now; previousActive = true;
        }

        public void Poll()
        {
            lock (gate)
            {
                if (disposed) return;
                PollNative();
            }
        }

        void PollNative()
        {
            if (launched != null)
            {
                try
                {
                    if (launched.HasExited)
                    {
                        SessionProcess root = State.Processes[0];
                        root.EndedUtcTicks = launched.ExitTime.ToUniversalTime().Ticks;
                    }
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            Poll(SessionLedger.ProcessSnapshot(), DateTime.UtcNow);
        }

        // An injected snapshot also makes PID reuse, launcher exits and recovery testable without starting games.
        public void Poll(IEnumerable<SessionProcess> processes, DateTime now)
        {
            lock (gate)
            {
                if (disposed) return;
                PollObserved(processes, now);
            }
        }

        void PollObserved(IEnumerable<SessionProcess> processes, DateTime now)
        {
            if (Finished) return;
            List<SessionProcess> snapshot = processes.ToList();
            var liveKeys = new HashSet<string>(snapshot.Select(p => p.Key), StringComparer.Ordinal);
            bool found;
            do
            {
                found = false;
                foreach (SessionProcess candidate in snapshot)
                {
                    if (beforeLaunch.Contains(candidate.Key) || State.Processes.Any(p => p.Key == candidate.Key)) continue;
                    // Parent identity is verified while alive, or by its retained process handle's exact exit time.
                    SessionProcess parent = State.Processes.FirstOrDefault(p => p.Pid == candidate.ParentPid &&
                        candidate.StartedUtcTicks >= p.StartedUtcTicks &&
                        (liveKeys.Contains(p.Key) || p.EndedUtcTicks > 0 && candidate.StartedUtcTicks <= p.EndedUtcTicks));
                    if (parent == null) continue;
                    if (State.Processes.Count >= 4096) continue;
                    State.Processes.Add(candidate); found = true;
                }
            } while (found);
            bool active = State.Processes.Any(p => liveKeys.Contains(p.Key));
            double elapsed = Math.Max(0, Math.Min(30, (now - lastPoll).TotalSeconds));
            if (previousActive)
            {
                // Root-only launches can finish between polls; never include the post-exit grace interval.
                if (!active && State.Processes.Count == 1 && State.Processes[0].EndedUtcTicks > 0)
                    elapsed = Math.Min(elapsed, Math.Max(0, (new DateTime(State.Processes[0].EndedUtcTicks, DateTimeKind.Utc) - lastPoll).TotalSeconds));
                State.ElapsedSeconds += elapsed;
            }
            lastPoll = now; previousActive = active;
            if (active) emptySince = null;
            else if (!emptySince.HasValue) emptySince = now;
            else if ((now - emptySince.Value).TotalSeconds >= 2) State.Finished = true;
            State.CheckedUtc = now.ToString("o", CultureInfo.InvariantCulture);
            SessionLedger.WriteCheckpoint(FileName, State);
        }

        public void Dispose()
        {
            // Preserve the journal on app shutdown; a surviving exact process may be resumed next time.
            lock (gate)
            {
                disposed = true;
                if (launched != null) launched.Dispose();
            }
        }
    }

    public static class SessionLedger
    {
        public static SessionTracker Start(string directory, PlaySession session, Process process, IEnumerable<SessionProcess> beforeLaunch)
        {
            return Start(directory, session, process, beforeLaunch, null);
        }

        public static SessionTracker Start(string directory, PlaySession session, Process process, IEnumerable<SessionProcess> beforeLaunch, string ownerKey)
        {
            if (process == null) throw new ArgumentNullException("process");
            var identity = new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks };
            if (beforeLaunch != null && beforeLaunch.Any(p => p.Key == identity.Key))
                throw new InvalidOperationException("The launcher returned an already-running process; its activity cannot be attributed to this launch.");
            var tracker = StartObserved(directory, session, identity, beforeLaunch, DateTime.UtcNow, process, ownerKey);
            tracker.Poll();
            return tracker;
        }

        public static SessionTracker StartObserved(string directory, PlaySession session, SessionProcess root, IEnumerable<SessionProcess> beforeLaunch, DateTime now)
        {
            return StartObserved(directory, session, root, beforeLaunch, now, null, null);
        }

        public static SessionTracker StartObserved(string directory, PlaySession session, SessionProcess root, IEnumerable<SessionProcess> beforeLaunch, DateTime now, string ownerKey)
        {
            return StartObserved(directory, session, root, beforeLaunch, now, null, ownerKey);
        }

        public static string ProfileKey(LibraryData library)
        {
            return library.UserTools == null ? "" : library.UserTools.ActiveId ?? "";
        }

        static SessionTracker StartObserved(string directory, PlaySession session, SessionProcess root, IEnumerable<SessionProcess> beforeLaunch, DateTime now, Process process, string ownerKey)
        {
            if (session == null || root == null || root.Pid <= 0 || root.StartedUtcTicks <= 0) throw new ArgumentException("A session and verified process identity are required.");
            Guid id;
            if (!Guid.TryParseExact(session.Id, "N", out id)) throw new ArgumentException("Invalid session identity.");
            string folder = Path.Combine(Path.GetFullPath(directory), "active-sessions");
            CheckDirectory(directory);
            Directory.CreateDirectory(folder);
            CheckDirectory(folder);
            string file = Path.Combine(folder, session.Id + ".json");
            if (File.Exists(file)) throw new IOException("This session already has a tracking journal.");
            var state = new SessionCheckpoint { OwnerKey = ownerKey ?? "", SessionId = session.Id, GameId = session.GameId, CheckedUtc = now.ToString("o", CultureInfo.InvariantCulture), Processes = new List<SessionProcess> { root } };
            WriteCheckpoint(file, state);
            return new SessionTracker(file, state, process, beforeLaunch, now);
        }

        public static bool Apply(LibraryData library, SessionTracker tracker, bool complete)
        {
            PlaySession session = (library.PlaySessions ?? new List<PlaySession>()).FirstOrDefault(s => s.Id == tracker.SessionId && s.GameId == tracker.GameId);
            GameEntry game = (library.Games ?? new List<GameEntry>()).FirstOrDefault(g => g.Id == tracker.GameId);
            if (session == null || game == null || !string.Equals(tracker.State.OwnerKey ?? "", ProfileKey(library), StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(session.EndedAt)) return false;
            long next = Math.Max(session.Seconds, tracker.Seconds);
            game.TotalPlaySeconds += next - session.Seconds;
            session.Seconds = next;
            session.Uncertain = false;
            session.Note = "Verified launched process and descendants; app downtime is excluded.";
            if (complete) session.EndedAt = tracker.State.CheckedUtc;
            return true;
        }

        public static List<SessionTracker> Recover(string directory, LibraryData library)
        {
            return Recover(directory, library, ProcessSnapshot(), DateTime.UtcNow);
        }

        public static List<SessionTracker> Recover(string directory, LibraryData library, IEnumerable<SessionProcess> snapshot, DateTime now)
        {
            var resumed = new List<SessionTracker>();
            string folder = Path.Combine(Path.GetFullPath(directory), "active-sessions");
            if (!Directory.Exists(folder)) return resumed;
            CheckDirectory(folder);
            var live = new HashSet<string>(snapshot.Select(p => p.Key), StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file).Length > 1024 * 1024) continue;
                    SessionCheckpoint state = Json.Deserialize<SessionCheckpoint>(File.ReadAllText(file));
                    Guid parsed;
                    if (state == null || !Guid.TryParseExact(state.SessionId, "N", out parsed) || Path.GetFileNameWithoutExtension(file) != state.SessionId ||
                        state.Processes == null || state.Processes.Count == 0 || state.Processes.Count > 4096 ||
                        double.IsNaN(state.ElapsedSeconds) || double.IsInfinity(state.ElapsedSeconds) || state.ElapsedSeconds < 0 || state.ElapsedSeconds > 315360000 ||
                        state.Processes.Any(p => p == null || p.Pid <= 0 || p.StartedUtcTicks <= 0 || p.StartedUtcTicks > DateTime.MaxValue.Ticks || p.EndedUtcTicks < 0 || p.EndedUtcTicks > DateTime.MaxValue.Ticks)) continue;
                    // Profiles share storage: never consume another profile's journal or attach it by game ID alone.
                    PlaySession session = (library.PlaySessions ?? new List<PlaySession>()).FirstOrDefault(s => s.Id == state.SessionId && s.GameId == state.GameId);
                    if (session == null || !string.Equals(state.OwnerKey ?? "", ProfileKey(library), StringComparison.Ordinal)) continue;
                    bool active = string.IsNullOrEmpty(session.EndedAt) && !state.Finished && state.Processes.Any(p => live.Contains(p.Key));
                    if (!active) state.Finished = true;
                    var tracker = new SessionTracker(file, state, null, null, now);
                    Apply(library, tracker, !active);
                    resumed.Add(tracker);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (InvalidDataException) { }
            }
            return resumed;
        }

        public static void Commit(SessionTracker tracker)
        {
            if (tracker.Finished && File.Exists(tracker.FileName))
            {
                CheckDirectory(Path.GetDirectoryName(tracker.FileName));
                File.Delete(tracker.FileName);
            }
        }

        internal static void WriteCheckpoint(string file, SessionCheckpoint checkpoint)
        {
            CheckDirectory(Path.GetDirectoryName(file));
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("A session journal cannot be a link.");
            string temporary = file + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] content = System.Text.Encoding.UTF8.GetBytes(Json.Serialize(checkpoint));
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(content, 0, content.Length); output.Flush(true);
                }
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static List<SessionProcess> ProcessSnapshot()
        {
            var result = new List<SessionProcess>();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
                if (snapshot == new IntPtr(-1)) return result;
                try
                {
                    PROCESSENTRY32 entry = new PROCESSENTRY32(); entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                    if (Process32First(snapshot, ref entry)) do { AddIdentity(result, (int)entry.th32ProcessID, (int)entry.th32ParentProcessID); } while (Process32Next(snapshot, ref entry));
                }
                finally { CloseHandle(snapshot); }
            }
            else if (Directory.Exists("/proc"))
            {
                foreach (string folder in Directory.GetDirectories("/proc"))
                {
                    int pid;
                    if (!int.TryParse(Path.GetFileName(folder), out pid)) continue;
                    try
                    {
                        string stat = File.ReadAllText(Path.Combine(folder, "stat"));
                        string[] fields = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ');
                        int parent;
                        if (fields.Length > 1 && int.TryParse(fields[1], out parent)) AddIdentity(result, pid, parent);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            return result;
        }

        static void AddIdentity(List<SessionProcess> result, int pid, int parent)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    string firstStat = null;
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                        firstStat = File.ReadAllText("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat");
                    long started = process.StartTime.ToUniversalTime().Ticks;
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        PROCESS_BASIC_INFORMATION basic;
                        int returned;
                        if (NtQueryInformationProcess(process.Handle, 0, out basic, Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out returned) != 0) return;
                        parent = basic.InheritedFromUniqueProcessId.ToInt32();
                    }
                    else
                    {
                        string path = "/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat";
                        string[] fields = firstStat.Substring(firstStat.LastIndexOf(')') + 2).Split(' ');
                        string second = File.ReadAllText(path);
                        string[] again = second.Substring(second.LastIndexOf(')') + 2).Split(' ');
                        if (fields.Length <= 19 || again.Length <= 19 || fields[19] != again[19] || fields[1] != again[1]) return;
                        parent = int.Parse(fields[1], CultureInfo.InvariantCulture);
                    }
                    result.Add(new SessionProcess { Pid = pid, ParentPid = parent, StartedUtcTicks = started });
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            catch (NotSupportedException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (FormatException) { }
            catch (OverflowException) { }
        }

        static void CheckDirectory(string directory)
        {
            for (string current = Path.GetFullPath(directory); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Session journals require ordinary local directories.");
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, InheritedFromUniqueProcessId;
        }
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int informationClass, out PROCESS_BASIC_INFORMATION information, int length, out int returned);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PROCESSENTRY32
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    }
}
