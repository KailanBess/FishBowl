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
        [DllImport("user32.dll")] internal static extern IntPtr GetMenu(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetMenu(IntPtr window, IntPtr menu);
        [DllImport("user32.dll")] internal static extern bool DrawMenuBar(IntPtr window);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int capacity);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int capacity);
        internal static string WindowCaption(IntPtr window) { var value=new System.Text.StringBuilder(2048);GetWindowText(window,value,value.Capacity);return value.ToString(); }
        internal static string NormalizeCaption(string text) { return new string((text??"").Normalize(System.Text.NormalizationForm.FormD).Where(c=>char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray()); }
        internal static int Rank(IntPtr window,string title) {
            var name=new System.Text.StringBuilder(256);GetClassName(window,name,name.Capacity);string kind=name.ToString();
            if(kind.IndexOf("RenderWindow",StringComparison.OrdinalIgnoreCase)>=0 || kind.IndexOf("DolphinRender",StringComparison.OrdinalIgnoreCase)>=0 || kind.IndexOf("QWindowOwnDC",StringComparison.OrdinalIgnoreCase)>=0 || kind=="SDL_app" || kind=="GLFW30") return 2;
            string preferred=NormalizeCaption(title);return preferred.Length>2&&NormalizeCaption(WindowCaption(window)).Contains(preferred)?1:0;
        }
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
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, System.Text.StringBuilder path, ref uint length);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetLongPathName(string path,System.Text.StringBuilder result,uint capacity);
        internal static bool SameExecutable(string first,string second) {
            if(string.IsNullOrWhiteSpace(first)||string.IsNullOrWhiteSpace(second))return false;
            try { return CanonicalPath(first).Equals(CanonicalPath(second),StringComparison.OrdinalIgnoreCase); }
            catch(ArgumentException){return false;}catch(NotSupportedException){return false;}catch(PathTooLongException){return false;}
        }
        static string CanonicalPath(string path) {
            string full=Path.GetFullPath(path);var result=new System.Text.StringBuilder(32768);uint length=GetLongPathName(full,result,(uint)result.Capacity);
            return length>0&&length<result.Capacity?Path.GetFullPath(result.ToString()):full;
        }
        internal static string Executable(Process process) {
            try { var module=process.MainModule; if(module!=null && !string.IsNullOrWhiteSpace(module.FileName)) return Path.GetFullPath(module.FileName); } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
            IntPtr handle=OpenProcess(0x1000, false, process.Id);
            if(handle==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {
                var path=new System.Text.StringBuilder(32768); uint length=(uint)path.Capacity;
                if(!QueryFullProcessImageName(handle,0,path,ref length) || length==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return Path.GetFullPath(path.ToString());
            } finally { CloseHandle(handle); }
        }
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
        internal static bool IsAlive(SessionProcess identity)
        {
            if(identity == null || identity.Pid == Process.GetCurrentProcess().Id) return false;
            try { using(var process=Process.GetProcessById(identity.Pid)) return !process.HasExited && process.StartTime.ToUniversalTime().Ticks==identity.StartedUtcTicks; }
            catch(ArgumentException) { return false; }
            catch(InvalidOperationException) { return false; }
            catch(System.ComponentModel.Win32Exception) { return true; } // Access denial must not silently permit exit.
        }
        internal static IntPtr Find(IEnumerable<SessionProcess> identities)
        { return Find(identities,null); }
        internal static IntPtr Find(IEnumerable<SessionProcess> identities,string title)
        {
            var known = identities.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.Last().StartedUtcTicks);
            IntPtr best = IntPtr.Zero; long area = 0;int rank=-1;
            EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                uint pid; GetWindowThreadProcessId(window, out pid);
                long started; Rect bounds;
                if (known.TryGetValue((int)pid, out started) && IsWindowVisible(window) && GetWindow(window,4)==IntPtr.Zero &&
                    (GetLong(window, Style) & Child) == 0 && !IsHungAppWindow(window) &&
                    GetClientRect(window, out bounds) && Matches(window, (int)pid, started))
                {
                    long size = (long)(bounds.Right - bounds.Left) * (bounds.Bottom - bounds.Top);
                    int priority=Rank(window,title);
                    if (size >= 4096 && (priority>rank || priority==rank && size>area)) { area = size; best = window;rank=priority; }
                }
                return true;
            }, IntPtr.Zero);
            return best;
        }
    }

    public class PlayWindowHost : Panel
    {
        IntPtr window, originalParent, originalRegion;
        IntPtr originalMenu;
        bool menuAltered;
        PlayViewSettings view = new PlayViewSettings();
        int pid;
        long started, originalStyle, originalExStyle;
        PlayNative.Placement originalPlacement;
        PlayNative.Rect originalBounds;
        bool attached, disposing, transitioning, embedding, anchored, altered;
        bool placementValid, windowHidden;
        Rectangle lastViewport;
        public int PlacementUpdates { get; private set; }
        public string LastError { get; private set; }
        Form anchorOwner;
        readonly string ownership = "FishBowl.Play." + Guid.NewGuid().ToString("N");
        readonly IntPtr ownershipValue = new IntPtr(1);
        IntPtr desiredDpi;
        bool OwnsWindow { get { return PlayNative.Matches(window,pid,started) && PlayNative.GetProp(window,ownership)==ownershipValue; } }
        public bool IsAttached { get { return attached && IsHandleCreated && OwnsWindow && (anchored ? anchorOwner!=null && anchorOwner.IsHandleCreated && PlayNative.GetWindow(window,4)==anchorOwner.Handle : PlayNative.GetParent(window)==Handle); } }
        public bool IsAnchored { get { return IsAttached && anchored; } }
        public bool PreserveRendererWindow { get; set; }
        public IntPtr GameWindow { get { return window; } }
        public PlayWindowHost() { Dock = DockStyle.Fill; BackColor = FishBowlPalette.ThemeBottom; TabStop = true; }
        public void ConfigureView(PlayViewSettings settings)
        {
            view=settings ?? new PlayViewSettings(); placementValid=false;
            if(IsAttached) {
                if(originalMenu!=IntPtr.Zero) { if(PrepareHandleChange())Embed(); }
                else ResizeGame();
            }
        }
        void ApplyMenu()
        {
            if(!OwnsWindow || originalMenu==IntPtr.Zero) return;
            if(PlayNative.SetMenu(window,view.ShowControls ? originalMenu : IntPtr.Zero)) {
                menuAltered=!view.ShowControls; PlayNative.DrawMenuBar(window);
            }
        }
        Padding Insets()
        {
            if(view.ShowControls) return Padding.Empty;
            uint dpi=96; try { uint current=PlayNative.GetDpiForWindow(window); if(current>0) dpi=current; } catch(EntryPointNotFoundException) { }
            return new Padding((int)(Math.Max(0,Math.Min(400,view.Left))*dpi/96), (int)(Math.Max(0,Math.Min(400,view.Top))*dpi/96),
                (int)(Math.Max(0,Math.Min(400,view.Right))*dpi/96), (int)(Math.Max(0,Math.Min(400,view.Bottom))*dpi/96));
        }
        public bool AttachWindow(IntPtr candidate, int processId, long processStarted)
        {
            LastError = null;
            if (disposing || !PlayNative.Matches(candidate, processId, processStarted)) { LastError = "The game window changed or the process ended. Choose Show in Play to retry."; return false; }
            if (PlayNative.IsHungAppWindow(candidate)) { LastError = "The emulator is busy. Wait for it to respond, then choose Show in Play."; return false; }
            if (window == candidate && IsAttached) return true;
            IntPtr owner=PlayNative.GetWindow(candidate,4);
            if ((PlayNative.GetLong(candidate,PlayNative.Style)&PlayNative.Child)!=0 || owner!=IntPtr.Zero) return false;
            if (!TryDetach()) return false;
            window = candidate; pid = processId; started = processStarted;
            originalParent = PlayNative.GetParent(window);
            originalStyle = PlayNative.GetLong(window, PlayNative.Style);
            originalExStyle = PlayNative.GetLong(window, PlayNative.ExStyle);
            originalMenu = PlayNative.GetMenu(window); menuAltered=false;
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
            catch (EntryPointNotFoundException) { LastError = "This Windows version cannot host this renderer. Use Open in window."; Restore(true); return false; }
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
                if(PreserveRendererWindow || view.ShowControls && originalMenu!=IntPtr.Zero || !PlayNative.AreDpiAwarenessContextsEqual(PlayNative.GetWindowDpiAwarenessContext(target),desiredDpi)) return EmbedAnchored();
                anchored=false;
                altered=true;
                ApplyMenu();
                long childStyle = (originalStyle & ~(PlayNative.Popup | PlayNative.Caption | PlayNative.ThickFrame)) | PlayNative.Child;
                if (!PlayNative.PutLong(window, PlayNative.Style, childStyle) || !PlayNative.PutLong(window, PlayNative.ExStyle, originalExStyle & ~0x00040008L)) throw new InvalidOperationException();
                PlayNative.SetLastError(0); PlayNative.SetParent(window, target);
                if (Marshal.GetLastWin32Error() != 0 || PlayNative.GetParent(window) != target) throw new InvalidOperationException();
                attached = true; placementValid = false; windowHidden = false;
                ResizeGame();
                return true;
            }
            catch (Exception error) {
                LastError = "Windows could not host this game window. Use Open in window, or retry when the emulator responds.";
                Store.Log("Play attachment failed (PID " + pid + "): " + error.Message + "; native error " + Marshal.GetLastWin32Error());
                Restore(true); return false;
            }
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
            ApplyMenu();
            long style=(originalStyle & ~(PlayNative.Child|PlayNative.Caption|PlayNative.ThickFrame))|PlayNative.Popup;
            if(!PlayNative.PutLong(window,PlayNative.Style,style)||!PlayNative.PutLong(window,PlayNative.ExStyle,originalExStyle&~0x00040008L)||!PlayNative.PutLong(window,-8,owner.Handle.ToInt64())) throw new InvalidOperationException();
            if(PlayNative.GetWindow(window,4)!=owner.Handle || !PlayNative.AreDpiAwarenessContextsEqual(PlayNative.GetWindowDpiAwarenessContext(window),desiredDpi)) throw new InvalidOperationException();
            anchored=true;attached=true;placementValid=false;windowHidden=false;ResizeGame();return true;
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
                    if(menuAltered && !PlayNative.SetMenu(window,originalMenu)) return false;
                    if(menuAltered) PlayNative.DrawMenuBar(window);
                    menuAltered=false;
                    if(PlayNative.GetParent(window)!=parent) return false;
                    if (!RestoreRegion()) return false;
                    var placement=originalPlacement; placement.Flags|=4;
                    if(!forget) placement.Show=0;
                    PlayNative.SetWindowPlacement(window, ref placement);
                    if(originalPlacement.Show==1 || originalPlacement.Show==9)
                        PlayNative.SetWindowPos(window,IntPtr.Zero,originalBounds.Left,originalBounds.Top,originalBounds.Right-originalBounds.Left,originalBounds.Bottom-originalBounds.Top,0x4034);
                    else PlayNative.SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x4037);
                    if(forget && (originalStyle & 0x10000000L)!=0) PlayNative.ShowWindowAsync(window,originalPlacement.Show==0 ? 1 : originalPlacement.Show);
                }
                if(forget && window!=IntPtr.Zero && OwnsWindow) PlayNative.RemoveProp(window,ownership);
                attached = false;anchored=false;altered=false;placementValid=false;windowHidden=false;UnbindOwner();
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
                Padding inset=Insets();
                PlayNative.Rect client;
                if(anchored)
                {
                    if(!Visible || anchorOwner==null || !anchorOwner.Visible || !anchorOwner.Enabled || !PlayNative.IsWindowEnabled(anchorOwner.Handle) || anchorOwner.WindowState==FormWindowState.Minimized)
                    { if (!windowHidden) PlayNative.ShowWindowAsync(window,0); windowHidden = true; placementValid = false; return; }
                    IntPtr previous=PlayNative.SetThreadDpiAwarenessContext(desiredDpi);
                    try
                    {
                        if(PlayNative.GetWindowRect(Handle,out client))
                        {
                            int width = Math.Max(1, client.Right-client.Left), height = Math.Max(1, client.Bottom-client.Top);
                            var viewport = new Rectangle(client.Left, client.Top, width, height);
                            if (placementValid && !windowHidden && viewport == lastViewport && PlayNative.IsWindowVisible(window)) return;
                            IntPtr clip = PlayNative.CreateRectRgn(inset.Left, inset.Top, inset.Left+width, inset.Top+height);
                            if (clip == IntPtr.Zero) { PlayNative.ShowWindowAsync(window, 0); return; }
                            if (PlayNative.SetWindowRgn(window, clip, false) == 0)
                            { PlayNative.DeleteObject(clip); PlayNative.ShowWindowAsync(window, 0); return; }
                            if (PlayNative.SetWindowPos(window,IntPtr.Zero,client.Left-inset.Left,client.Top-inset.Top,width+inset.Horizontal,height+inset.Vertical,0x4074)) {
                                lastViewport = viewport; placementValid = true; windowHidden = false; PlacementUpdates++;
                            }
                        }
                    }
                    finally{if(previous!=IntPtr.Zero)PlayNative.SetThreadDpiAwarenessContext(previous);}
                }
                else if(PlayNative.GetClientRect(Handle,out client)) {
                    var viewport = new Rectangle(0, 0, Math.Max(1,client.Right-client.Left), Math.Max(1,client.Bottom-client.Top));
                    if (placementValid && viewport == lastViewport) return;
                    if (PlayNative.SetWindowPos(window, IntPtr.Zero, -inset.Left, -inset.Top, viewport.Width+inset.Horizontal, viewport.Height+inset.Vertical,0x4074)) {
                        lastViewport = viewport; placementValid = true; PlacementUpdates++;
                    }
                }
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

    public sealed class PlayExitDialog : NextDialog
    {
        public PlayExitDialog(IEnumerable<string> games) : base("Close FishBowl?",720,460)
        {
            var fields=NextDialog.Fields(Body);
            NextDialog.Field(fields,"Running games",new TextBox { Text=string.Join(Environment.NewLine,games ?? new[]{"Running game"}),ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill },140);
            NextDialog.Field(fields,"",ExperienceUi.Label("Are you sure you want to close FishBowl and its running games? Save your progress first. Emulator save or exit prompts will be shown before FishBowl closes.",120),140);
            Action("Close games and exit",delegate { DialogResult=DialogResult.OK; Close(); });
            Action("Keep games running",delegate { DialogResult=DialogResult.Yes; Close(); });
            Action("Cancel",delegate { DialogResult=DialogResult.Cancel; Close(); });
            CancelButton=Actions.Controls.OfType<Button>().Last(); AcceptButton=Actions.Controls.OfType<Button>().Last();
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
            internal sealed class Frontend { internal IntPtr Window;internal int Pid;internal long Started;internal PlayNative.Placement Placement; }
            internal readonly List<Frontend> Frontends=new List<Frontend>();
            internal readonly string FrontendMarker="FishBowl.Frontend."+Guid.NewGuid().ToString("N");
            internal string Title, Notice;
            internal EmulatorProfile ViewProfile;
            internal PlayViewSettings View = new PlayViewSettings();
            internal readonly List<SessionProcess> Known = new List<SessionProcess>();
            internal readonly Dictionary<string, Process> Handles = new Dictionary<string, Process>();
            internal HashSet<string> Before;
            internal PlayWindowHost Host;
            internal bool External;
            internal DateTime Added = DateTime.UtcNow;
            public override string ToString() { return Title; }
            internal bool RestoreFrontends() {
                foreach(var item in Frontends.ToArray()) {
                    if(PlayNative.Matches(item.Window,item.Pid,item.Started)&&PlayNative.GetProp(item.Window,FrontendMarker)==new IntPtr(1)) {
                        var placement=item.Placement;if(!PlayNative.SetWindowPlacement(item.Window,ref placement))return false;
                        PlayNative.ShowWindowAsync(item.Window,placement.Show==0?1:placement.Show);PlayNative.RemoveProp(item.Window,FrontendMarker);
                    }
                    Frontends.Remove(item);
                }return true;
            }
            public void Dispose() { RestoreFrontends();Host.Dispose(); foreach (Process process in Handles.Values) process.Dispose(); }
        }
        readonly LibraryData library;
        readonly Action fullscreen;
        readonly ComboBox choice;
        readonly Label status, empty;
        readonly Button fullscreenButton, externalButton, retryButton, shareButton, moreButton;
        bool fullScreenActive;
        readonly Panel stage;
        readonly Timer timer;
        readonly List<Session> sessions = new List<Session>();
        Task<List<SessionProcess>> snapshot;
        bool disposing;
        bool reportedSessions;
        public event EventHandler SessionStateChanged;
        public bool HasSessions { get { return sessions.Count > 0; } }
        public PlaySurface(LibraryData data, Action fullScreen)
        {
            library = data; fullscreen = fullScreen;
            Dock = DockStyle.Fill; BackColor = FishBowlPalette.ThemeSurface;
            var toolbar = ExperienceUi.Bar(); toolbar.Dock = DockStyle.Top;
            // This toolbar already owns a More menu and wraps; avoid adding a second overflow menu.
            toolbar.Tag = "FishBowl overflow";
            choice = NextDialog.Choice(new string[0], null); choice.Width = 220; choice.AccessibleName = "Running games";
            toolbar.Controls.Add(choice);
            fullscreenButton = ExperienceUi.Button("Full screen", delegate { if (fullscreen != null) fullscreen(); });
            externalButton = ExperienceUi.Button("Open in window", delegate { OpenSelectedInWindow(); });
            retryButton = ExperienceUi.Button("Show in Play", RetrySelected);
            shareButton = ExperienceUi.Button("Share session", ShareSelected);
            moreButton = ExperienceUi.Button("More", delegate { });
            var menu = new ContextMenuStrip(); NavigationController.Register(menu);
            menu.Items.Add("Session details", null, delegate { SessionDetails(); });
            menu.Items.Add("Game view settings", null, delegate { GameViewSettings(); });
            moreButton.ContextMenuStrip = menu;
            moreButton.Click += delegate { menu.Show(moreButton, new Point(0, moreButton.Height)); };
            moreButton.Disposed += delegate { menu.Dispose(); };
            toolbar.Controls.AddRange(new Control[] { fullscreenButton, externalButton, retryButton, shareButton, moreButton });
            stage = new MixedStage { Dock = DockStyle.Fill, BackColor = FishBowlPalette.ThemeBottom };
            empty = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16), Text = "Your game will appear here. Launch a game from Library or an emulator from Emulators.", ForeColor = FishBowlPalette.ThemeSubtle, BackColor = FishBowlPalette.ThemeBottom };
            stage.Controls.Add(empty);
            status = new Label { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), ForeColor = FishBowlPalette.ThemeSubtle, BackColor = FishBowlPalette.ThemeSurface, Text = "Launch a game or emulator to show it here." };
            Controls.Add(stage); Controls.Add(status); Controls.Add(toolbar);
            choice.SelectedIndexChanged += delegate { ShowSelected(); };
            timer = new Timer { Interval = 1000 }; timer.Tick += Tick;
            RefreshStatus();
        }
        Session Selected() { return choice.SelectedItem as Session; }
        public void SetFullscreen(bool active) { fullScreenActive = active; RefreshStatus(); }
        protected override void OnLayout(LayoutEventArgs e) {
            if (status != null) status.MaximumSize = new Size(Math.Max(1, ClientSize.Width), 0);
            if (empty != null) empty.MaximumSize = new Size(Math.Max(1, ClientSize.Width), 0);
            base.OnLayout(e);
        }
        public bool OpenSelectedInWindow() {
            var selected = Selected(); if (selected == null) return false;
            if (!selected.RestoreFrontends() || !selected.Host.TryDetach()) {
                selected.Notice = "The emulator window is busy. It remains in Play. Wait for it to respond, then retry.";
                Store.Log("Play detach failed for " + selected.Title); RefreshStatus(); return false;
            }
            selected.External = true; selected.Notice = null; RefreshStatus(); return true;
        }
        void RetrySelected() {
            var selected = Selected(); if (selected == null) return;
            selected.External = false; selected.Notice = null; selected.Added = DateTime.UtcNow; timer.Start(); RefreshStatus();
        }
        void ShareSelected() {
            var selected = Selected(); if (selected == null) return;
            IntPtr window = selected.Host.GameWindow;
            if (window == IntPtr.Zero) window = PlayNative.Find(selected.Known,selected.Title);
            uint pid; PlayNative.GetWindowThreadProcessId(window, out pid);
            var identity = selected.Known.FirstOrDefault(p => p.Pid == pid && PlayNative.Matches(window, p.Pid, p.StartedUtcTicks));
            if (identity == null) { selected.Notice = "No responding game window is available. Wait for the emulator, then retry."; RefreshStatus(); return; }
            bool wasExternal = selected.External;
            if (!OpenSelectedInWindow()) return;
            try {
                using (var dialog = new RemotePlayDialog(library, null, new RemoteWindow { Pid = identity.Pid, Started = identity.StartedUtcTicks, Handle = window, Title = selected.Title }))
                    dialog.ShowDialog(FindForm());
            } finally {
                if (!wasExternal && sessions.Contains(selected)) { choice.SelectedItem = selected; RetrySelected(); }
            }
        }
        void SessionDetails() {
            var selected = Selected(); if (selected == null) return;
            using (var dialog = new NextDialog("Play session", 760, 520)) {
                var fields = NextDialog.Fields(dialog.Body);
                NextDialog.Field(fields, "Game", new Label { Text = selected.Title, AutoSize = true }, 60);
                NextDialog.Field(fields, "Status", new Label { Text = status.Text, AutoSize = true }, 100);
                NextDialog.Field(fields, "Controls", new Label { Text = "Switching menus keeps the game running. Use the fullscreen button to return from fullscreen while game controls have focus. Closing FishBowl asks before closing running games. Emulator save prompts remain available.", AutoSize = true }, 110);
                NextDialog.Field(fields, "Recovery", new Label { Text = "If the renderer is busy or unsupported, open it in its own window. Show in Play retries the same verified session. Details are recorded in the activity log.", AutoSize = true }, 100);
                dialog.Action("Close", dialog.Close); dialog.ShowDialog(FindForm());
            }
        }
        void GameViewSettings()
        {
            var selected=Selected(); if(selected==null) return;
            using(var dialog=new NextDialog("Game view settings",760,620)) {
                var fields=NextDialog.Fields(dialog.Body);
                var controls=new CheckBox { Text="Show emulator controls in Play", Checked=selected.View.ShowControls, AutoSize=true };
                NextDialog.Field(fields,"View",controls);
                var top=NextDialog.Number(Math.Max(0,Math.Min(400,selected.View.Top)),0,400);
                var bottom=NextDialog.Number(Math.Max(0,Math.Min(400,selected.View.Bottom)),0,400);
                var left=NextDialog.Number(Math.Max(0,Math.Min(400,selected.View.Left)),0,400);
                var right=NextDialog.Number(Math.Max(0,Math.Min(400,selected.View.Right)),0,400);
                NextDialog.Field(fields,"Hide top toolbar (pixels)",top); NextDialog.Field(fields,"Hide bottom status bar (pixels)",bottom);
                NextDialog.Field(fields,"Hide left edge (pixels)",left); NextDialog.Field(fields,"Hide right edge (pixels)",right);
                NextDialog.Field(fields,"",ExperienceUi.Label("Standard window borders and menus are hidden in Play. Set edge sizes for controls drawn by the emulator. Start at zero, then adjust until only the game is visible. Open in window restores the original emulator view. " + (selected.ViewProfile==null ? "These adjustments apply to this session." : "These adjustments are saved for this emulator."),130),150);
                dialog.Action("Apply",delegate {
                    var value=new PlayViewSettings { ShowControls=controls.Checked, Top=(int)top.Value, Bottom=(int)bottom.Value, Left=(int)left.Value, Right=(int)right.Value };
                    selected.View=value;
                    if(selected.ViewProfile!=null) { selected.ViewProfile.PlayView=value; Store.Save(library); }
                    foreach(var session in sessions.Where(s=>s==selected || selected.ViewProfile!=null && s.ViewProfile==selected.ViewProfile)) { session.View=value;session.Host.ConfigureView(value);if(value.ShowControls)session.RestoreFrontends(); }
                    dialog.Close();
                });
                dialog.Action("Reset",delegate { controls.Checked=false;top.Value=bottom.Value=left.Value=right.Value=0; });
                dialog.Action("Cancel",dialog.Close);dialog.ShowDialog(FindForm());
            }
        }
        public bool HasRunningSessions { get { return sessions.Any(s=>s.Known.Any(PlayNative.IsAlive)); } }
        public bool ConfirmExit(IWin32Window owner)
        {
            if(!HasRunningSessions) return true;
            using(var dialog=new PlayExitDialog(sessions.Where(s=>s.Known.Any(PlayNative.IsAlive)).Select(s=>s.Title))) {
                DialogResult answer=dialog.ShowDialog(owner);
                if(answer==DialogResult.Yes) return TryDetachAll();
                if(answer!=DialogResult.OK) return false;
            }
            return RequestCloseAll();
        }
        public bool RequestCloseAll()
        {
            // Restore before WM_CLOSE so emulator confirmation/save dialogs can be used.
            if(!TryDetachAll()) return false;
            var identities=sessions.SelectMany(s=>s.Known).GroupBy(p=>p.Key).Select(g=>g.First()).ToList();
            PlayNative.EnumWindows(delegate(IntPtr window,IntPtr unused) {
                uint pid;PlayNative.GetWindowThreadProcessId(window,out pid);
                PlayNative.Rect client;
                if(PlayNative.GetWindow(window,4)==IntPtr.Zero && PlayNative.IsWindowEnabled(window) && PlayNative.GetClientRect(window,out client) && client.Right-client.Left>64 && client.Bottom-client.Top>64 && identities.Any(p=>p.Pid==pid && PlayNative.Matches(window,p.Pid,p.StartedUtcTicks)))
                    PlayNative.PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);
                return true;
            },IntPtr.Zero);
            foreach(var session in sessions) session.Notice="Closing the game. If the emulator asks to save or confirm, finish that prompt and close FishBowl again.";
            RefreshStatus();
            var wait=Stopwatch.StartNew();
            while(HasRunningSessions && wait.ElapsedMilliseconds<1500) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            return !HasRunningSessions;
        }
        public void Launch(Process process, string title) { Launch(process, title, new List<SessionProcess>()); }
        public void Launch(Process process, string title, IList<SessionProcess> before)
        {
            if (process == null || disposing) return;
            try
            {
                var identity = new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks };
                var existing=sessions.FirstOrDefault(s => s.Known.Any(p=>p.Key==identity.Key));
                if(existing!=null) { existing.External=false; existing.Notice=null; existing.Added=DateTime.UtcNow; timer.Start(); choice.SelectedItem=existing; ShowSelected(); return; }
                if ((before ?? new List<SessionProcess>()).Any(p => p.Key == identity.Key)) { status.Text = "This launcher is already running. Choose its window with Show in Play."; return; }
                var session = new Session { Title = string.IsNullOrWhiteSpace(title) ? "Running game" : title, Before = new HashSet<string>((before ?? new List<SessionProcess>()).Select(p => p.Key)), Host = new PlayWindowHost() };
                try {
                    string executable=PlayNative.Executable(process);
                    session.ViewProfile=library.Emulators.FirstOrDefault(e=>PlayNative.SameExecutable(e.Executable,executable) || (e.Builds??new List<EmulatorBuild>()).Any(b=>PlayNative.SameExecutable(b.Executable,executable)));
                    if(session.ViewProfile!=null && session.ViewProfile.PlayView!=null) session.View=session.ViewProfile.PlayView;
                } catch(Exception error) { Store.Log("Play view profile: " + error.Message); }
                session.Host.ConfigureView(session.View);
                session.Known.Add(identity); timer.Start();
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
                IntPtr initial=PlayNative.Find(session.Known,session.Title);
                if(initial!=IntPtr.Zero) session.Host.AttachWindow(initial,identity.Pid,identity.StartedUtcTicks);
                RefreshStatus();
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
                            if(!running.HasExited && running.StartTime.ToUniversalTime().Ticks==identity.StartedUtcTicks && string.Equals(PlayNative.Executable(running),path,StringComparison.OrdinalIgnoreCase))
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
            var candidates = new List<Process>(); bool unverified = false;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (!process.HasExited && string.Equals(PlayNative.Executable(process), path, StringComparison.OrdinalIgnoreCase) &&
                        PlayNative.Find(new[] { new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks } }) != IntPtr.Zero) candidates.Add(process);
                    else process.Dispose();
                }
                catch {
                    try { if (string.Equals(process.ProcessName, Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)) unverified = true; } catch { }
                    process.Dispose();
                }
            }
            try
            {
                if (candidates.Count == 0) { status.Text = unverified ? "Windows could not verify the running emulator. Check its permissions and use its own window, then retry." : "No responding game window is available. Finish any emulator startup dialog, then retry opening it in Play."; return; }
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
            if (reportedSessions != HasSessions) {
                reportedSessions = HasSessions;
                var changed = SessionStateChanged; if (changed != null) changed(this, EventArgs.Empty);
            }
            var selected = Selected();
            choice.Enabled = selected != null;
            fullscreenButton.Text = fullScreenActive ? "Exit full screen" : "Full screen";
            fullscreenButton.Enabled = selected != null || fullScreenActive;
            externalButton.Enabled = selected != null && !selected.External;
            retryButton.Enabled = selected != null && (selected.External || !selected.Host.IsAttached);
            shareButton.Enabled = selected != null;
            moreButton.Enabled = selected != null;
            empty.Visible = selected == null;
            if (selected == null) status.Text = "Launch a game or emulator to show it here.";
            else if (!string.IsNullOrEmpty(selected.Notice)) status.Text = selected.Title + ": " + selected.Notice;
            else if (selected.External) status.Text = selected.Title + " is open in its own window. The game keeps running.";
            else if (selected.Host.IsAttached) status.Text = selected.Title + " is running in Play. Switching menus keeps it running.";
            else if ((DateTime.UtcNow - selected.Added).TotalSeconds > 20) status.Text = selected.Title + ": " + (selected.Host.LastError ?? "No compatible game window is available yet. Finish any emulator startup dialog, then choose Show in Play; Open in window keeps the session available.");
            else status.Text = "Waiting for " + selected.Title + " to open its game window...";
        }
        void Tick(object sender, EventArgs e)
        {
            if (disposing || sessions.Count == 0) { timer.Stop(); return; }
            foreach (var session in sessions) if (session.Host.IsAnchored) session.Host.RefreshPlacement();
            if (snapshot == null) { snapshot = Task.Factory.StartNew<List<SessionProcess>>(delegate { return SessionLedger.ProcessSnapshot(); }); return; }
            if (!snapshot.IsCompleted) return;
            if (snapshot.IsFaulted) { snapshot = null; RefreshStatus(); return; }
            var live = snapshot.Result; snapshot = null;
            timer.Interval=sessions.Any(s=>!s.External&&!s.Host.IsAttached&&(DateTime.UtcNow-s.Added).TotalSeconds<=20) ? 150 : (Visible ? 1000 : 2000);
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
                    sessions.Remove(session); choice.Items.Remove(session); session.Dispose();
                    Store.Log("Play session ended: " + session.Title); continue;
                }
                if (!session.External && ((!session.Host.IsAttached && (DateTime.UtcNow - session.Added).TotalSeconds <= 20) || session.Host.IsAttached && PlayNative.Rank(session.Host.GameWindow,session.Title)<2))
                {
                    IntPtr candidate = PlayNative.Find(session.Known.Where(p => keys.Contains(p.Key)),session.Title);
                    if (candidate != IntPtr.Zero && (!session.Host.IsAttached || PlayNative.Rank(candidate,session.Title)>PlayNative.Rank(session.Host.GameWindow,session.Title)))
                    {
                        uint pid; PlayNative.GetWindowThreadProcessId(candidate, out pid);
                        var identity = session.Known.First(p => p.Pid == pid && keys.Contains(p.Key));
                        session.Host.AttachWindow(candidate, identity.Pid, identity.StartedUtcTicks);
                    }
                }
                HideFrontend(session);
            }
            if (choice.SelectedIndex < 0 && choice.Items.Count > 0) choice.SelectedIndex = 0;
            ShowSelected();
            if (sessions.Count == 0) timer.Stop();
        }
        public bool PrepareHandleChange()
        {
            bool success=true;
            foreach(var session in sessions) if(!session.Host.PrepareHandleChange()) success=false;
            return success;
        }
        void HideFrontend(Session session)
        {
            if(session.External || !session.Host.IsAttached || session.View.ShowControls || session.ViewProfile==null) return;
            string vendor=PlayNative.NormalizeCaption(Path.GetFileNameWithoutExtension(session.ViewProfile.Executable??"").Split('-','_').FirstOrDefault());
            if(vendor.Length<3)return;
            PlayNative.EnumWindows(delegate(IntPtr window,IntPtr unused) {
                if(window==session.Host.GameWindow || session.Frontends.Any(f=>f.Window==window) || !PlayNative.IsWindowVisible(window) || !PlayNative.IsWindowEnabled(window) || PlayNative.IsHungAppWindow(window) || PlayNative.GetWindow(window,4)!=IntPtr.Zero || (PlayNative.GetLong(window,PlayNative.ExStyle)&1)!=0)return true;
                uint pid;PlayNative.GetWindowThreadProcessId(window,out pid);
                var identity=session.Known.FirstOrDefault(p=>p.Pid==pid&&PlayNative.Matches(window,p.Pid,p.StartedUtcTicks));if(identity==null)return true;
                string caption=PlayNative.WindowCaption(window);string normalized=PlayNative.NormalizeCaption(caption);
                if(!normalized.StartsWith(vendor,StringComparison.Ordinal) || caption.IndexOfAny(new[]{'|',':'})>=0 || new[]{"settings","configuration","error","warning","save","open","load"}.Any(normalized.Contains) || PlayNative.Rank(window,session.Title)>=PlayNative.Rank(session.Host.GameWindow,session.Title))return true;
                var placement=new PlayNative.Placement{Length=Marshal.SizeOf(typeof(PlayNative.Placement))};
                if(PlayNative.GetWindowPlacement(window,ref placement)&&PlayNative.SetProp(window,session.FrontendMarker,new IntPtr(1))) {
                    session.Frontends.Add(new Session.Frontend{Window=window,Pid=identity.Pid,Started=identity.StartedUtcTicks,Placement=placement});PlayNative.ShowWindowAsync(window,0);
                }return true;
            },IntPtr.Zero);
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
            foreach(var session in sessions) if(session.RestoreFrontends() && session.Host.TryDetach()) session.External=true; else success=false;
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
