using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal static class PlayNative
    {
        internal const int Style = -16, ExStyle = -20;
        internal const long Child = 0x40000000L, Popup = 0x80000000L, Caption = 0x00c00000L, ThickFrame = 0x00040000L;
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Placement
        {
            internal int Length, Flags, Show;
            internal Point Minimum, Maximum;
            internal Rect Normal;
        }
        internal delegate bool EnumCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumCallback callback, IntPtr data);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] internal static extern int GetWindowRgn(IntPtr window, IntPtr region);
        [DllImport("user32.dll")] internal static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] internal static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll")] internal static extern bool IsHungAppWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr window, IntPtr parent);
        [DllImport("kernel32.dll")] internal static extern void SetLastError(uint error);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] static extern IntPtr GetLong64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] static extern int GetLong32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern IntPtr SetLong64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] static extern int SetLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect bounds);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window,ref Point point);
        [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
        [DllImport("user32.dll")] internal static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool SetProp(IntPtr window, string name, IntPtr value);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr RemoveProp(IntPtr window, string name);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
        [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern int SetThreadDpiHostingBehavior(int behavior);
        internal static long GetLong(IntPtr window, int index) { return IntPtr.Size == 8 ? GetLong64(window, index).ToInt64() : (long)(uint)GetLong32(window, index); }
        internal static bool PutLong(IntPtr window, int index, long value)
        {
            SetLastError(0);
            long previous=IntPtr.Size==8 ? SetLong64(window,index,new IntPtr(value)).ToInt64() : SetLong32(window,index,unchecked((int)value));
            return previous!=0 || Marshal.GetLastWin32Error()==0;
        }
        internal static bool Matches(IntPtr window, int pid, long started)
        {
            if (!IsWindow(window)) return false;
            uint actual; GetWindowThreadProcessId(window, out actual);
            if (actual != pid || pid == Process.GetCurrentProcess().Id) return false;
            try { using (var process = Process.GetProcessById(pid)) return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == started; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        internal static IntPtr Find(IEnumerable<SessionProcess> identities)
        {
            var known = identities.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.Last().StartedUtcTicks);
            IntPtr best = IntPtr.Zero; long area = 0;
            EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                uint pid; GetWindowThreadProcessId(window, out pid);
                long started; Rect bounds;
                if (known.TryGetValue((int)pid, out started) && IsWindowVisible(window) && GetWindow(window,4)==IntPtr.Zero &&
                    (GetLong(window, Style) & Child) == 0 && !IsHungAppWindow(window) &&
                    GetClientRect(window, out bounds) && Matches(window, (int)pid, started))
                {
                    long size = (long)(bounds.Right - bounds.Left) * (bounds.Bottom - bounds.Top);
                    if (size > area && size >= 4096) { area = size; best = window; }
                }
                return true;
            }, IntPtr.Zero);
            return best;
        }
    }

    public class PlayWindowHost : Panel
    {
        IntPtr window, originalParent, originalRegion;
        int pid;
        long started, originalStyle, originalExStyle;
        PlayNative.Placement originalPlacement;
        PlayNative.Rect originalBounds;
        bool attached, disposing, transitioning, embedding, anchored, altered;
        Form anchorOwner;
        readonly string ownership = "FishBowl.Play." + Guid.NewGuid().ToString("N");
        readonly IntPtr ownershipValue = new IntPtr(1);
        IntPtr desiredDpi;
        bool OwnsWindow { get { return PlayNative.Matches(window,pid,started) && PlayNative.GetProp(window,ownership)==ownershipValue; } }
        public bool IsAttached { get { return attached && IsHandleCreated && OwnsWindow && (anchored ? anchorOwner!=null && anchorOwner.IsHandleCreated && PlayNative.GetWindow(window,4)==anchorOwner.Handle : PlayNative.GetParent(window)==Handle); } }
        public bool IsAnchored { get { return IsAttached && anchored; } }
        public IntPtr GameWindow { get { return window; } }
        public PlayWindowHost() { Dock = DockStyle.Fill; BackColor = FishBowlPalette.ThemeBottom; TabStop = true; }
        public bool AttachWindow(IntPtr candidate, int processId, long processStarted)
        {
            if (disposing || !PlayNative.Matches(candidate, processId, processStarted) || PlayNative.IsHungAppWindow(candidate)) return false;
            if (window == candidate && IsAttached) return true;
            IntPtr owner=PlayNative.GetWindow(candidate,4);
            if ((PlayNative.GetLong(candidate,PlayNative.Style)&PlayNative.Child)!=0 || owner!=IntPtr.Zero) return false;
            if (!TryDetach()) return false;
            window = candidate; pid = processId; started = processStarted;
            originalParent = PlayNative.GetParent(window);
            originalStyle = PlayNative.GetLong(window, PlayNative.Style);
            originalExStyle = PlayNative.GetLong(window, PlayNative.ExStyle);
            PlayNative.GetWindowRect(window,out originalBounds);
            originalPlacement = new PlayNative.Placement { Length = Marshal.SizeOf(typeof(PlayNative.Placement)) };
            if (!PlayNative.GetWindowPlacement(window, ref originalPlacement)) { window = IntPtr.Zero; return false; }
            if (!PlayNative.SetProp(window, ownership, ownershipValue)) { window=IntPtr.Zero; return false; }
            originalRegion = PlayNative.CreateRectRgn(0, 0, 0, 0);
            if (originalRegion != IntPtr.Zero && PlayNative.GetWindowRgn(window, originalRegion) == 0)
            { PlayNative.DeleteObject(originalRegion); originalRegion = IntPtr.Zero; }
            try
            {
                desiredDpi=PlayNative.GetWindowDpiAwarenessContext(window);
                if (IsHandleCreated && !PlayNative.AreDpiAwarenessContextsEqual(PlayNative.GetWindowDpiAwarenessContext(Handle),desiredDpi)) RecreateHandle();
            }
            catch (EntryPointNotFoundException) { Restore(true); return false; }
            return Embed();
        }
        protected override void CreateHandle()
        {
            IntPtr old=IntPtr.Zero; int hosting=-1;
            try
            {
                if(desiredDpi!=IntPtr.Zero)
                {
                    hosting=PlayNative.SetThreadDpiHostingBehavior(1);
                    old=PlayNative.SetThreadDpiAwarenessContext(desiredDpi);
                }
                base.CreateHandle();
            }
            catch(EntryPointNotFoundException) { base.CreateHandle(); }
            finally
            {
                if(old!=IntPtr.Zero) PlayNative.SetThreadDpiAwarenessContext(old);
                if(hosting>=0) PlayNative.SetThreadDpiHostingBehavior(hosting);
            }
        }
        bool Embed()
        {
            if (window == IntPtr.Zero || disposing || embedding || !OwnsWindow) return false;
            embedding = true;
            try
            {
                IntPtr target = Handle;
                if(desiredDpi==IntPtr.Zero) throw new InvalidOperationException();
                if(!PlayNative.AreDpiAwarenessContextsEqual(PlayNative.GetWindowDpiAwarenessContext(target),desiredDpi)) return EmbedAnchored();
                anchored=false;
                altered=true;
                long childStyle = (originalStyle & ~(PlayNative.Popup | PlayNative.Caption | PlayNative.ThickFrame)) | PlayNative.Child;
                if (!PlayNative.PutLong(window, PlayNative.Style, childStyle) || !PlayNative.PutLong(window, PlayNative.ExStyle, originalExStyle & ~0x00040008L)) throw new InvalidOperationException();
                PlayNative.SetLastError(0); PlayNative.SetParent(window, target);
                if (Marshal.GetLastWin32Error() != 0 || PlayNative.GetParent(window) != target) throw new InvalidOperationException();
                attached = true;
                ResizeGame();
                return true;
            }
            catch (Exception) { Restore(true); return false; }
            finally { embedding = false; }
        }
        bool EmbedAnchored()
        {
            Form owner=FindForm();
            if(owner==null || !owner.IsHandleCreated) return false;
            if(anchorOwner!=owner)
            {
                UnbindOwner(); anchorOwner=owner;
                owner.LocationChanged+=OwnerChanged;owner.SizeChanged+=OwnerChanged;
                owner.VisibleChanged+=OwnerChanged;owner.EnabledChanged+=OwnerChanged;
            }
            anchored=true;altered=true;
            long style=(originalStyle & ~(PlayNative.Child|PlayNative.Caption|PlayNative.ThickFrame))|PlayNative.Popup;
            if(!PlayNative.PutLong(window,PlayNative.Style,style)||!PlayNative.PutLong(window,PlayNative.ExStyle,originalExStyle&~0x00040008L)||!PlayNative.PutLong(window,-8,owner.Handle.ToInt64())) throw new InvalidOperationException();
            if(PlayNative.GetWindow(window,4)!=owner.Handle || !PlayNative.AreDpiAwarenessContextsEqual(PlayNative.GetWindowDpiAwarenessContext(window),desiredDpi)) throw new InvalidOperationException();
            anchored=true;attached=true;ResizeGame();return true;
        }
        void OwnerChanged(object sender,EventArgs e) { ResizeGame(); }
        void UnbindOwner()
        {
            if(anchorOwner!=null)
            {
                anchorOwner.LocationChanged-=OwnerChanged;anchorOwner.SizeChanged-=OwnerChanged;
                anchorOwner.VisibleChanged-=OwnerChanged;anchorOwner.EnabledChanged-=OwnerChanged;
                anchorOwner=null;
            }
        }
        public bool PrepareHandleChange() { return Restore(false); }
        public void ResumeHandleChange() { if(window!=IntPtr.Zero && !disposing) Embed(); }
        public void Detach() { TryDetach(); }
        public bool TryDetach() { return Restore(true); }
        bool Restore(bool forget)
        {
            if (transitioning) return false;
            transitioning = true;
            try
            {
                if (window != IntPtr.Zero && OwnsWindow && altered)
                {
                    if(!forget)PlayNative.ShowWindowAsync(window,0);
                    IntPtr parent = PlayNative.IsWindow(originalParent) ? originalParent : IntPtr.Zero;
                    if(anchored)
                    {
                        PlayNative.PutLong(window,-8,parent.ToInt64());
                        if(PlayNative.GetWindow(window,4)!=parent) return false;
                    }
                    else
                    {
                        PlayNative.SetParent(window,parent);
                        IntPtr actualParent=PlayNative.GetParent(window);
                        if(actualParent!=parent && !(parent==IntPtr.Zero && actualParent==PlayNative.GetDesktopWindow())) return false;
                    }
                    PlayNative.PutLong(window, PlayNative.Style, originalStyle);
                    PlayNative.PutLong(window, PlayNative.ExStyle, originalExStyle);
                    if(PlayNative.GetParent(window)!=parent) return false;
                    if (!RestoreRegion()) return false;
                    var placement=originalPlacement; placement.Flags|=4;
                    if(!forget) placement.Show=0;
                    PlayNative.SetWindowPlacement(window, ref placement);
                    if(originalPlacement.Show==1 || originalPlacement.Show==9)
                        PlayNative.SetWindowPos(window,IntPtr.Zero,originalBounds.Left,originalBounds.Top,originalBounds.Right-originalBounds.Left,originalBounds.Bottom-originalBounds.Top,0x4034);
                    else PlayNative.SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x4037);
                }
                if(forget && window!=IntPtr.Zero && OwnsWindow) PlayNative.RemoveProp(window,ownership);
                attached = false;anchored=false;altered=false;UnbindOwner();
                if (forget)
                {
                    window = IntPtr.Zero;
                    if (originalRegion != IntPtr.Zero) PlayNative.DeleteObject(originalRegion);
                    originalRegion = IntPtr.Zero;
                }
                return true;
            }
            finally { transitioning = false; }
        }
        bool RestoreRegion()
        {
            if (originalRegion == IntPtr.Zero) return PlayNative.SetWindowRgn(window, IntPtr.Zero, false) != 0;
            IntPtr copy = PlayNative.CreateRectRgn(0, 0, 0, 0);
            if (copy == IntPtr.Zero) return false;
            if (PlayNative.CombineRgn(copy, originalRegion, IntPtr.Zero, 5) == 0 || PlayNative.SetWindowRgn(window, copy, false) == 0)
            { PlayNative.DeleteObject(copy); return false; }
            // A successful SetWindowRgn transfers the copy to Windows; the saved region remains ours.
            return true;
        }
        internal void RefreshPlacement() { ResizeGame(); }
        void ResizeGame()
        {
            if (IsAttached)
            {
                PlayNative.Rect client;
                if(anchored)
                {
                    if(!Visible || anchorOwner==null || !anchorOwner.Visible || !anchorOwner.Enabled || !PlayNative.IsWindowEnabled(anchorOwner.Handle) || anchorOwner.WindowState==FormWindowState.Minimized)
                    { PlayNative.ShowWindowAsync(window,0); return; }
                    IntPtr previous=PlayNative.SetThreadDpiAwarenessContext(desiredDpi);
                    try
                    {
                        if(PlayNative.GetWindowRect(Handle,out client))
                        {
                            int width = Math.Max(1, client.Right-client.Left), height = Math.Max(1, client.Bottom-client.Top);
                            IntPtr clip = PlayNative.CreateRectRgn(0, 0, width, height);
                            if (clip == IntPtr.Zero) { PlayNative.ShowWindowAsync(window, 0); return; }
                            if (PlayNative.SetWindowRgn(window, clip, false) == 0)
                            { PlayNative.DeleteObject(clip); PlayNative.ShowWindowAsync(window, 0); return; }
                            PlayNative.SetWindowPos(window,IntPtr.Zero,client.Left,client.Top,width,height,0x4074);
                        }
                    }
                    finally{if(previous!=IntPtr.Zero)PlayNative.SetThreadDpiAwarenessContext(previous);}
                }
                else if(PlayNative.GetClientRect(Handle,out client)) PlayNative.SetWindowPos(window, IntPtr.Zero, 0, 0, Math.Max(1,client.Right-client.Left),Math.Max(1,client.Bottom-client.Top),0x4074);
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ResizeGame(); }
        protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e);ResizeGame(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e);ResizeGame(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e);ResizeGame(); }
        protected override void OnEnter(EventArgs e) { base.OnEnter(e); if (IsAttached && !PlayNative.IsHungAppWindow(window)) PlayNative.SetFocus(window); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (IsAttached && !PlayNative.IsHungAppWindow(window)) PlayNative.SetFocus(window); }
        protected override void DestroyHandle() { if(!Restore(false)) throw new InvalidOperationException("The game window could not be detached safely."); base.DestroyHandle(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (!transitioning && window != IntPtr.Zero && !disposing) Embed(); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 2) Restore(false);
            base.WndProc(ref message);
        }
        protected override void Dispose(bool disposingValue)
        {
            if (disposingValue) { disposing = true; Detach(); }
            base.Dispose(disposingValue);
        }
    }

    public sealed class PlaySurface : UserControl
    {
        sealed class MixedStage : Panel
        {
            protected override void CreateHandle()
            {
                int previous=-1;
                try { previous=PlayNative.SetThreadDpiHostingBehavior(1); base.CreateHandle(); }
                catch(EntryPointNotFoundException) { base.CreateHandle(); }
                finally { if(previous>=0) PlayNative.SetThreadDpiHostingBehavior(previous); }
            }
        }
        sealed class Session : IDisposable
        {
            internal string Title;
            internal readonly List<SessionProcess> Known = new List<SessionProcess>();
            internal readonly Dictionary<string, Process> Handles = new Dictionary<string, Process>();
            internal HashSet<string> Before;
            internal PlayWindowHost Host;
            internal bool External;
            internal DateTime Added = DateTime.UtcNow;
            public override string ToString() { return Title; }
            public void Dispose() { Host.Dispose(); foreach (Process process in Handles.Values) process.Dispose(); }
        }
        readonly LibraryData library;
        readonly Action fullscreen;
        readonly ComboBox choice;
        readonly Label status;
        readonly Panel stage;
        readonly Timer timer;
        readonly List<Session> sessions = new List<Session>();
        Task<List<SessionProcess>> snapshot;
        bool disposing;
        public bool HasSessions { get { return sessions.Count > 0; } }
        public PlaySurface(LibraryData data, Action fullScreen)
        {
            library = data; fullscreen = fullScreen;
            Dock = DockStyle.Fill; BackColor = FishBowlPalette.ThemeSurface;
            var toolbar = ExperienceUi.Bar(); toolbar.Dock = DockStyle.Top;
            choice = NextDialog.Choice(new string[0], null); choice.Width = 220; choice.AccessibleName = "Running games";
            toolbar.Controls.Add(choice);
            toolbar.Controls.Add(ExperienceUi.Button("Full screen", delegate { if (fullscreen != null) fullscreen(); }));
            toolbar.Controls.Add(ExperienceUi.Button("Open in window", delegate { var selected = Selected(); if (selected != null) { selected.External = true; selected.Host.Detach(); RefreshStatus(); } }));
            toolbar.Controls.Add(ExperienceUi.Button("Show in Play", delegate { var selected = Selected(); if (selected != null) { selected.External = false; selected.Added = DateTime.UtcNow; RefreshStatus(); } }));
            stage = new MixedStage { Dock = DockStyle.Fill, BackColor = FishBowlPalette.ThemeBottom };
            status = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), ForeColor = FishBowlPalette.ThemeSubtle, BackColor = FishBowlPalette.ThemeSurface, Text = "Launch a game or emulator to show it here." };
            Controls.Add(stage); Controls.Add(status); Controls.Add(toolbar);
            choice.SelectedIndexChanged += delegate { ShowSelected(); };
            timer = new Timer { Interval = 400 }; timer.Tick += Tick; timer.Start();
        }
        Session Selected() { return choice.SelectedItem as Session; }
        public void Launch(Process process, string title) { Launch(process, title, new List<SessionProcess>()); }
        public void Launch(Process process, string title, IList<SessionProcess> before)
        {
            if (process == null || disposing) return;
            try
            {
                var identity = new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks };
                var existing=sessions.FirstOrDefault(s => s.Known.Any(p=>p.Key==identity.Key));
                if(existing!=null) { existing.External=false; existing.Added=DateTime.UtcNow; choice.SelectedItem=existing; ShowSelected(); return; }
                if ((before ?? new List<SessionProcess>()).Any(p => p.Key == identity.Key)) { status.Text = "This launcher is already running. Choose its window with Show in Play."; return; }
                var session = new Session { Title = string.IsNullOrWhiteSpace(title) ? "Running game" : title, Before = new HashSet<string>((before ?? new List<SessionProcess>()).Select(p => p.Key)), Host = new PlayWindowHost() };
                session.Known.Add(identity);
                try
                {
                    var owned = Process.GetProcessById(identity.Pid);
                    if (owned.StartTime.ToUniversalTime().Ticks != identity.StartedUtcTicks) { owned.Dispose(); return; }
                    session.Handles.Add(identity.Key, owned);
                }
                catch (ArgumentException) { if (process.HasExited) identity.EndedUtcTicks = process.ExitTime.ToUniversalTime().Ticks; }
                stage.Controls.Add(session.Host); session.Host.Visible = false;
                sessions.Add(session); choice.Items.Add(session); choice.SelectedItem = session;
                ShowSelected();
                IntPtr initial=PlayNative.Find(session.Known);
                if(initial!=IntPtr.Zero) session.Host.AttachWindow(initial,identity.Pid,identity.StartedUtcTicks);
                timer.Interval=150;
            }
            catch (Exception) { status.Text = "The launched window could not be verified. It remains open outside FishBowl."; }
        }
        public void Adopt(string executable, string title)
        {
            string path;
            try { path = Path.GetFullPath(executable); } catch { status.Text = "Choose an emulator executable first."; return; }
            var matches = new List<Session>();
            foreach(var session in sessions)
                foreach(var identity in session.Known)
                    try
                    {
                        using(var running=Process.GetProcessById(identity.Pid))
                            if(!running.HasExited && running.StartTime.ToUniversalTime().Ticks==identity.StartedUtcTicks && string.Equals(Path.GetFullPath(running.MainModule.FileName),path,StringComparison.OrdinalIgnoreCase))
                            { if (!matches.Contains(session)) matches.Add(session); }
                    }
                    catch { }
            if (matches.Count == 1)
            {
                var matched = matches[0]; matched.External=false; matched.Added=DateTime.UtcNow;
                choice.SelectedItem=matched; ShowSelected(); return;
            }
            if (matches.Count > 1)
            {
                using (var dialog = new NextDialog("Show running window in Play", 620, 300))
                {
                    var names = matches.Select(s => s.Title + " (" + s.Known[0].Pid + ")").ToList();
                    var select = NextDialog.Choice(names, names[0]); select.Dock=DockStyle.Top; dialog.Body.Controls.Add(select);
                    dialog.Action("Show in Play", delegate
                    {
                        var matched=matches[names.IndexOf(select.Text)];matched.External=false;matched.Added=DateTime.UtcNow;
                        choice.SelectedItem=matched;ShowSelected();dialog.Close();
                    });
                    dialog.Action("Cancel", dialog.Close);dialog.ShowDialog(FindForm());
                }
                return;
            }
            var candidates = new List<Process>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (!process.HasExited && string.Equals(Path.GetFullPath(process.MainModule.FileName), path, StringComparison.OrdinalIgnoreCase) &&
                        PlayNative.Find(new[] { new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks } }) != IntPtr.Zero) candidates.Add(process);
                    else process.Dispose();
                }
                catch { process.Dispose(); }
            }
            try
            {
                if (candidates.Count == 0) { status.Text = "No visible window was found for this emulator."; return; }
                using (var dialog = new NextDialog("Show running window in Play", 620, 300))
                {
                    var names = candidates.Select(p => (string.IsNullOrWhiteSpace(p.MainWindowTitle) ? title : p.MainWindowTitle) + " (" + p.Id + ")").ToList();
                    var select = NextDialog.Choice(names, names[0]); select.Dock = DockStyle.Top; dialog.Body.Controls.Add(select);
                    dialog.Action("Show in Play", delegate { Launch(candidates[names.IndexOf(select.Text)], title); dialog.Close(); });
                    dialog.Action("Cancel", dialog.Close); dialog.ShowDialog(FindForm());
                }
            }
            finally { foreach (Process process in candidates) process.Dispose(); }
        }
        void ShowSelected()
        {
            var selected = Selected(); foreach (var session in sessions) session.Host.Visible = session == selected;
            if (selected != null) selected.Host.BringToFront(); RefreshStatus();
        }
        void RefreshStatus()
        {
            var selected = Selected();
            if (selected == null) status.Text = "Launch a game or emulator to show it here.";
            else if (selected.External) status.Text = selected.Title + " is open in its own window. The game keeps running.";
            else if (selected.Host.IsAttached) status.Text = selected.Title + " is running in Play. Switching menus keeps it running.";
            else if ((DateTime.UtcNow - selected.Added).TotalSeconds > 20) status.Text = "This window cannot be shown in Play yet. Use Open in window, or Show in Play to retry.";
            else status.Text = "Waiting for " + selected.Title + " to open its game window...";
        }
        void Tick(object sender, EventArgs e)
        {
            if (disposing || sessions.Count == 0) return;
            foreach (var session in sessions) if (session.Host.IsAnchored) session.Host.RefreshPlacement();
            if (snapshot == null) { snapshot = Task.Factory.StartNew<List<SessionProcess>>(delegate { return SessionLedger.ProcessSnapshot(); }); return; }
            if (!snapshot.IsCompleted) return;
            if (snapshot.IsFaulted) { snapshot = null; RefreshStatus(); return; }
            var live = snapshot.Result; snapshot = null;
            timer.Interval=sessions.Any(s=>!s.External&&!s.Host.IsAttached&&(DateTime.UtcNow-s.Added).TotalSeconds<=20) ? 150 : 400;
            var keys = new HashSet<string>(live.Select(p => p.Key));
            foreach (var session in sessions.ToArray())
            {
                foreach (var known in session.Known)
                {
                    Process handle;
                    if (session.Handles.TryGetValue(known.Key, out handle))
                        try { if (handle.HasExited) known.EndedUtcTicks = handle.ExitTime.ToUniversalTime().Ticks; } catch { }
                }
                bool added;
                do
                {
                    added = false;
                    foreach (var candidate in live)
                    {
                        if (session.Known.Count >= 128 || session.Before.Contains(candidate.Key) || session.Known.Any(p => p.Key == candidate.Key)) continue;
                        var parent = session.Known.FirstOrDefault(p => p.Pid == candidate.ParentPid && candidate.StartedUtcTicks >= p.StartedUtcTicks &&
                            (keys.Contains(p.Key) || p.EndedUtcTicks > 0 && candidate.StartedUtcTicks <= p.EndedUtcTicks));
                        if (parent == null) continue;
                        session.Known.Add(candidate); added = true;
                        try
                        {
                            var owned = Process.GetProcessById(candidate.Pid);
                            if (owned.StartTime.ToUniversalTime().Ticks == candidate.StartedUtcTicks) session.Handles.Add(candidate.Key, owned); else owned.Dispose();
                        }
                        catch { }
                    }
                } while (added);
                bool running = session.Known.Any(p => keys.Contains(p.Key));
                if (!running && (DateTime.UtcNow - session.Added).TotalSeconds > 3)
                {
                    sessions.Remove(session); choice.Items.Remove(session); session.Dispose(); continue;
                }
                if (!session.External && !session.Host.IsAttached && (DateTime.UtcNow - session.Added).TotalSeconds <= 20)
                {
                    IntPtr candidate = PlayNative.Find(session.Known.Where(p => keys.Contains(p.Key)));
                    if (candidate != IntPtr.Zero)
                    {
                        uint pid; PlayNative.GetWindowThreadProcessId(candidate, out pid);
                        var identity = session.Known.First(p => p.Pid == pid && keys.Contains(p.Key));
                        session.Host.AttachWindow(candidate, identity.Pid, identity.StartedUtcTicks);
                    }
                }
            }
            if (choice.SelectedIndex < 0 && choice.Items.Count > 0) choice.SelectedIndex = 0;
            ShowSelected();
        }
        public bool PrepareHandleChange()
        {
            bool success=true;
            foreach(var session in sessions) if(!session.Host.PrepareHandleChange()) success=false;
            return success;
        }
        public void ResumeHandleChange()
        {
            foreach(var session in sessions) if(!session.External) session.Host.ResumeHandleChange();
            RefreshStatus();
        }
        public void DetachAll() { TryDetachAll(); }
        public bool TryDetachAll()
        {
            bool success=true;
            foreach(var session in sessions) if(session.Host.TryDetach()) session.External=true; else success=false;
            RefreshStatus(); return success;
        }
        protected override void Dispose(bool disposingValue)
        {
            if (disposingValue && !disposing)
            {
                disposing = true; timer.Stop(); timer.Dispose(); DetachAll();
                foreach (var session in sessions) session.Dispose(); sessions.Clear();
            }
            base.Dispose(disposingValue);
        }
    }
}
