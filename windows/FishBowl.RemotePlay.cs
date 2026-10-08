using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class RemotePlayTools
    {
        public static void Open(Form owner, LibraryData library, EmulatorProfile emulator)
        {
            var dialog=new RemotePlayDialog(library,emulator);
            dialog.FormClosed+=delegate {dialog.Dispose();};dialog.Show(owner);
        }
        public static readonly Dictionary<string, ushort> Keys = new Dictionary<string, ushort> {
            { "ArrowUp", 0x26 }, { "ArrowDown", 0x28 }, { "ArrowLeft", 0x25 }, { "ArrowRight", 0x27 },
            { "KeyZ", 0x5A }, { "KeyX", 0x58 }, { "KeyA", 0x41 }, { "KeyS", 0x53 }, { "Enter", 0x0D }, { "ShiftLeft", 0xA0 }, { "Escape", 0x1B }, { "Tab", 0x09 }
        };
        public static bool ValidKeys(string[] keys) { return keys != null && keys.Length <= Keys.Count && keys.All(k => k != null && Keys.ContainsKey(k)); }
        public static string Service(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || !string.IsNullOrEmpty(uri.UserInfo) || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)) throw new IOException("Use an HTTPS token service, or localhost for development.");
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }
    public sealed class RemotePlayDialog : NextDialog
    {
        readonly ComboBox targets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly CheckBox allow = new CheckBox { Text = "Allow approved guest controls", AutoSize = true };
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(680, 0) };
        readonly TextBox service = new TextBox();
        RemotePlayBridge bridge;
        readonly RemoteWindow preferred;
        readonly LibraryData library;
        public RemotePlayDialog(LibraryData library, EmulatorProfile emulator) : this(library, emulator, null) { }
        public RemotePlayDialog(LibraryData library, EmulatorProfile emulator, RemoteWindow preferredWindow) : base("Remote couch play", 780, 520)
        {
            this.library = library; preferred = preferredWindow;
            var fields = Fields(Body);
            service.Text = library.Multiplayer == null ? "" : library.Multiplayer.RelayGatewayUrl;
            Field(fields, "Token service", service);
            Field(fields, "Game window", targets);
            Field(fields, "Shared input", allow);
            var note = new Label { AutoSize = true, MaximumSize = new Size(680, 0), Text = "Start the game first, choose its window, then open the browser client. The game stays in Play while this session is open. Choose the same game window in the sharing picker. Approve one guest in the browser and allow controls here. Only the focused game receives the listed keyboard controls. Configure those keys in the emulator." };
            Field(fields, "Session", note, 120);
            Field(fields, "Status", status, 70);
            Action("Refresh windows", RefreshTargets);
            Action("Open client", OpenClient);
            Action("Stop sharing", StopBridge);
            Action("Close", Close);
            allow.CheckedChanged += delegate { if (bridge != null) bridge.Allow(allow.Checked); };
            targets.SelectedIndexChanged += delegate { if (bridge != null) { StopBridge(); status.Text = "Target changed. Open a new client session."; } };
            FormClosed += delegate { StopBridge(); };
            RefreshTargets();
            if (emulator != null) for (int i = 0; i < targets.Items.Count; i++) { var target = (RemoteWindow)targets.Items[i]; if (string.Equals(target.Program, emulator.Executable, StringComparison.OrdinalIgnoreCase)) { targets.SelectedIndex = i; break; } }
        }
        void RefreshTargets()
        {
            StopBridge(); targets.Items.Clear();
            foreach (var process in Process.GetProcesses()) using (process)
            {
                try {
                    string program = PlayNative.Executable(process);
                    if (process.Id == Process.GetCurrentProcess().Id || process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                    if (new[] { "explorer", "chrome", "msedge", "firefox", "ApplicationFrameHost", "dwm", "Codex", "powershell", "pwsh", "cmd" }.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)) continue;
                    targets.Items.Add(new RemoteWindow { Pid = process.Id, Started = process.StartTime.ToUniversalTime().Ticks, Handle = process.MainWindowHandle, Title = process.MainWindowTitle, Program = program });
                } catch { }
            }
            // Embedded child windows are not Process.MainWindowHandle; retain the exact selected Play HWND.
            if(preferred!=null&&PlayNative.Matches(preferred.Handle,preferred.Pid,preferred.Started)&&!targets.Items.Cast<RemoteWindow>().Any(t=>t.Handle==preferred.Handle&&t.Pid==preferred.Pid&&t.Started==preferred.Started))targets.Items.Add(preferred);
            if (targets.Items.Count > 0) targets.SelectedIndex = 0;
            if (preferred != null) targets.SelectedIndex = -1;
            if (preferred != null) for (int i = 0; i < targets.Items.Count; i++) {
                var target = (RemoteWindow)targets.Items[i];
                if (target.Pid == preferred.Pid && target.Started == preferred.Started && target.Handle == preferred.Handle) { targets.SelectedIndex = i; break; }
            }
            status.Text = preferred != null && targets.SelectedIndex < 0 ? "The selected Play window is unavailable. Refresh windows or choose a game explicitly." : targets.Items.Count == 0 ? "No game window found. Start a game, then refresh." : "Choose the game window before opening the client.";
        }
        void OpenClient()
        {
            try {
                var target = targets.SelectedItem as RemoteWindow;
                if (target == null) throw new IOException("Choose a running game window.");
                string address = RemotePlayTools.Service(service.Text);
                if (!PlayNative.Matches(target.Handle, target.Pid, target.Started)) throw new IOException("This game window ended or changed. Refresh windows and choose the running session.");
                if (library.Multiplayer == null) library.Multiplayer = new MultiplayerSettings();
                library.Multiplayer.RelayGatewayUrl = address; Store.Save(library);
                StopBridge(); bridge = new RemotePlayBridge(target, address); bridge.Start(); bridge.Allow(allow.Checked);
                FishBowlWeb.OpenExternal(library,bridge.Address);
                status.Text = "Client opened. Keep this session open. Stop sharing releases all guest controls.";
            } catch (Exception ex) { StopBridge(); status.Text = ex.Message; }
        }
        void StopBridge() { if (bridge != null) { bridge.Dispose(); bridge = null; } status.Text = "Session stopped. Guest controls released."; }
    }
    public sealed class RemoteWindow
    {
        public int Pid; public long Started; public IntPtr Handle; public string Title, Program;
        public override string ToString() { return Title; }
    }
    public sealed class RemoteInputPacket { public string[] keys { get; set; } }
    public sealed class RemotePlayBridge : IDisposable
    {
        readonly RemoteWindow target; readonly string service; readonly object gate = new object();
        readonly HashSet<string> down = new HashSet<string>(); readonly Dictionary<string, byte[]> resources = new Dictionary<string, byte[]>();
        TcpListener listener; Thread worker; System.Threading.Timer lease; volatile bool running; bool allowed; string token, origin; DateTime lastInput; int clients;
        public string Address { get { return origin + "/#bridge=" + token; } }
        public RemotePlayBridge(RemoteWindow target, string service) { this.target = target; this.service = RemotePlayTools.Service(service); }
        public void Start()
        {
            if (running) throw new IOException("Session already running.");
            foreach (string name in new[] { "index.html", "client.js", "controls.js", "style.css", "livekit.js" }) {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RemotePlay." + name)) {
                    if (stream == null) throw new IOException("Remote-play client files are missing. Rebuild FishBowl.");
                    using (var buffer = new MemoryStream()) { stream.CopyTo(buffer); resources[name == "index.html" ? "/" : "/" + name] = buffer.ToArray(); }
                }
            }
            byte[] secret = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(secret); token = BitConverter.ToString(secret).Replace("-", "").ToLowerInvariant();
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); origin = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture); running = true;
            worker = new Thread(Listen) { IsBackground = true }; worker.Start();
            lease = new System.Threading.Timer(delegate { lock (gate) { if (!Active() || (DateTime.UtcNow - lastInput).TotalMilliseconds > 1500) Release(); } }, null, 200, 200);
        }
        public void Allow(bool value) { lock (gate) { allowed = value; if (!value) Release(); } }
        bool Active()
        {
            if (!running || !allowed || GetForegroundWindow() != target.Handle) return false;
            try { using (var process = Process.GetProcessById(target.Pid)) { uint pid; GetWindowThreadProcessId(target.Handle, out pid); return pid == target.Pid && !process.HasExited && process.StartTime.ToUniversalTime().Ticks == target.Started; } } catch { return false; }
        }
        void Input(string[] keys)
        {
            if (!RemotePlayTools.ValidKeys(keys)) throw new IOException("Unsupported controls.");
            lock (gate) {
                if (!Active()) { Release(); return; }
                lastInput = DateTime.UtcNow; var wanted = new HashSet<string>(keys);
                foreach (string key in down.Where(k => !wanted.Contains(k)).ToArray()) { Send(key, true); down.Remove(key); }
                foreach (string key in wanted.Where(k => !down.Contains(k))) { Send(key, false); down.Add(key); }
            }
        }
        void Release() { foreach (string key in down.ToArray()) Send(key, true); down.Clear(); }
        static void Send(string key, bool up)
        {
            var input = new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = RemotePlayTools.Keys[key], Flags = up ? 2u : 0u } } };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeInput)));
        }
        void Listen()
        {
            while (running) try {
                var client = listener.AcceptTcpClient();
                if (Interlocked.Increment(ref clients) > 6) { Interlocked.Decrement(ref clients); client.Close(); continue; }
                ThreadPool.QueueUserWorkItem(delegate { try { Serve(client); } catch { } finally { client.Close(); Interlocked.Decrement(ref clients); } });
            } catch (SocketException) { if (!running) return; } catch (ObjectDisposedException) { return; }
        }
        void Serve(TcpClient client)
        {
            client.ReceiveTimeout = 2000; client.SendTimeout = 2000;
            using (var stream = client.GetStream()) {
                var header = new StringBuilder(); int current;
                while (header.Length < 8192 && (current = stream.ReadByte()) >= 0) { header.Append((char)current); if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n") break; }
                var lines = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None); var request = lines[0].Split(' '); var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in lines.Skip(1)) { int colon = line.IndexOf(':'); if (colon > 0) { string name = line.Substring(0,colon); if (values.ContainsKey(name)) { Reply(stream,400,"Duplicate header."); return; } values[name] = line.Substring(colon+1).Trim(); } }
                string host, authorization, requestOrigin;
                if (!running || request.Length != 3 || !values.TryGetValue("Host", out host) || host != new Uri(origin).Authority || values.ContainsKey("Transfer-Encoding")) { Reply(stream,400,"Invalid request."); return; }
                if (values.TryGetValue("Origin", out requestOrigin) && requestOrigin != origin) { Reply(stream,403,"Origin denied."); return; }
                byte[] resource;
                if (request[0] == "GET" && resources.TryGetValue(request[1], out resource)) { Reply(stream,200,resource,request[1] == "/" ? "text/html; charset=utf-8" : request[1].EndsWith(".css") ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8"); return; }
                if (!values.TryGetValue("Authorization", out authorization) || !Same(authorization,"Bearer " + token)) { Reply(stream,401,"Session authorization required."); return; }
                if (request[0] == "GET" && request[1] == "/config") {
                    var colors = new { background = ColorTranslator.ToHtml(FishBowlPalette.DeepSeaSurface), surface = ColorTranslator.ToHtml(FishBowlPalette.ThemeSurface), ink = ColorTranslator.ToHtml(FishBowlPalette.ThemeInk), accent = ColorTranslator.ToHtml(FishBowlPalette.IconAccent), border = ColorTranslator.ToHtml(ColorHarmony.Border) };
                    Reply(stream,200,Json.Serialize(new { service = service, colors = colors }),"application/json"); return;
                }
                int length; string text;
                if (request[0] != "POST" || !values.TryGetValue("Content-Length",out text) || !int.TryParse(text,out length) || length < 0 || length > 2048) { Reply(stream,400,"Invalid body."); return; }
                byte[] bytes = new byte[length]; int offset = 0, count; while(offset < length && (count=stream.Read(bytes,offset,length-offset)) > 0) offset += count;
                if (offset != length) { Reply(stream,400,"Incomplete body."); return; }
                if (request[1] == "/state") { lock(gate) Reply(stream,200,Json.Serialize(new { active = Active() }),"application/json"); return; }
                if (request[1] == "/input") { try { var data = Json.Deserialize<RemoteInputPacket>(Encoding.UTF8.GetString(bytes)); if (data == null) throw new IOException(); Input(data.keys); Reply(stream,200,"{}","application/json"); } catch { Reply(stream,400,"Invalid controls."); } return; }
                Reply(stream,404,"Page not found.");
            }
        }
        static bool Same(string left, string right) { int value = left.Length ^ right.Length; for(int i=0;i<Math.Min(left.Length,right.Length);i++) value |= left[i] ^ right[i]; return value == 0; }
        static void Reply(Stream stream,int status,string text,string type="text/plain; charset=utf-8") { Reply(stream,status,Encoding.UTF8.GetBytes(text),type); }
        static void Reply(Stream stream,int status,byte[] bytes,string type) {
            byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Response\r\nContent-Type: " + type + "\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self' https: wss: http://127.0.0.1:* http://localhost:* ws://127.0.0.1:* ws://localhost:*; media-src blob:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'\r\n\r\n"); stream.Write(header,0,header.Length); stream.Write(bytes,0,bytes.Length);
        }
        public void Dispose() { running=false; if(listener!=null) listener.Stop(); if(lease!=null) lease.Dispose(); lock(gate) { allowed=false; Release(); token=""; } if(worker!=null && worker != Thread.CurrentThread) worker.Join(2500); }
        [StructLayout(LayoutKind.Sequential)] struct NativeInput { public uint Type; public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort VirtualKey, ScanCode; public uint Flags, Time; public IntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X,Y; public uint MouseData,Flags,Time; public IntPtr ExtraInfo; }
        [DllImport("user32.dll")] static extern uint SendInput(uint count, NativeInput[] inputs,int size);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    }
}
