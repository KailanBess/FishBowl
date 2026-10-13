using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EmulatorHub
{
    // Keeps one FishBowl per library: a second launch (a desktop or Steam shortcut with --launch-game, --launch-emulator
    // or --living-room, or simply starting FishBowl again) hands its arguments to the running copy over a Unix domain
    // socket and exits, so two copies never save library.json over each other. The socket name includes a hash of the
    // data folder, so test or portable copies with a different library run side by side.
    public sealed class SingleInstance : IDisposable
    {
        private readonly Socket listener;
        private readonly string path;
        private readonly Action<string[]> received;
        private volatile bool disposed;

        private SingleInstance(Socket listener, string path, Action<string[]> received)
        {
            this.listener = listener; this.path = path; this.received = received;
            new Thread(Accept) { IsBackground = true, Name = "FishBowl single instance" }.Start();
        }

        // $XDG_RUNTIME_DIR/fishbowl-<user>-<data folder hash>.sock (falls back to the temp folder).
        public static string SocketPath(string dataDirectory)
        {
            var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (String.IsNullOrWhiteSpace(runtime) || !Directory.Exists(runtime)) runtime = Path.GetTempPath();
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory)))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            var user = new string((Environment.UserName ?? "user").Where(Char.IsLetterOrDigit).Take(24).ToArray());
            return Path.Combine(runtime, "fishbowl-" + user + "-" + hash + ".sock");
        }

        // Sends the arguments to a running copy. True when it accepted them (this copy should then exit).
        public static bool TryForward(string socketPath, string[] args, int timeoutMs = 3000)
        {
            if (!File.Exists(socketPath)) return false;
            try
            {
                using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                {
                    socket.SendTimeout = socket.ReceiveTimeout = timeoutMs;
                    socket.Connect(new UnixDomainSocketEndPoint(socketPath));
                    var payload = Encoding.UTF8.GetBytes((args == null ? "null" : Json.Serialize(args)) + "\n");
                    socket.Send(payload);
                    var reply = new byte[16]; int count = socket.Receive(reply);
                    return Encoding.ASCII.GetString(reply, 0, count).StartsWith("ok", StringComparison.Ordinal);
                }
            }
            catch (SocketException) { return false; }
            catch (IOException) { return false; }
        }

        // Starts listening, or returns null when another copy owns the socket (or sockets are unavailable).
        // A socket file left by a crashed copy is replaced.
        public static SingleInstance Listen(string socketPath, Action<string[]> received)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    socket.Bind(new UnixDomainSocketEndPoint(socketPath));
                    socket.Listen(8);
                    return new SingleInstance(socket, socketPath, received);
                }
                catch (SocketException error)
                {
                    socket.Dispose();
                    if (error.SocketErrorCode != SocketError.AddressAlreadyInUse || attempt > 0) return null;
                    if (TryForward(socketPath, null, 1000)) return null;
                    try { File.Delete(socketPath); } catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
                }
            }
            return null;
        }

        private void Accept()
        {
            while (!disposed)
            {
                Socket client;
                try { client = listener.Accept(); }
                catch (Exception) { if (disposed) return; Thread.Sleep(200); continue; }
                using (client)
                {
                    try
                    {
                        client.ReceiveTimeout = client.SendTimeout = 3000;
                        var buffer = new MemoryStream(); var chunk = new byte[4096]; int count;
                        while (buffer.Length < 65536 && (count = client.Receive(chunk)) > 0)
                        { buffer.Write(chunk, 0, count); if (Array.IndexOf(chunk, (byte)'\n', 0, count) >= 0) break; }
                        var text = Encoding.UTF8.GetString(buffer.ToArray()).Trim();
                        client.Send(Encoding.ASCII.GetBytes("ok\n"));
                        // "null" is Listen's liveness probe from a starting copy; it does not raise the window.
                        if (text.Length > 0 && text != "null") received(Json.Deserialize<string[]>(text) ?? new string[0]);
                    }
                    catch (Exception error) { Store.Log("Second FishBowl launch could not be handed over: " + error.Message); }
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { listener.Dispose(); } catch (Exception) { }
            try { File.Delete(path); } catch (Exception) { }
        }
    }
}
