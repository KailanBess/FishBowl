using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EmulatorHub;

// Second-launch hand-over (FishBowl.Avalonia/SingleInstance.cs): uses sockets in the test's temporary folder only.
public static class SingleInstanceTests
{
    public static void Run(string root, Action<string, bool> check)
    {
        var a = SingleInstance.SocketPath(Path.Combine(root, "data-a")); var b = SingleInstance.SocketPath(Path.Combine(root, "data-b"));
        check("single instance: socket differs per data folder", a != b && a.EndsWith(".sock"));
        var socket = Path.Combine(root, "fb-test.sock");
        check("single instance: no running copy, nothing forwarded", !SingleInstance.TryForward(socket, new[] { "--living-room" }));
        File.WriteAllText(socket, "stale");
        check("single instance: stale socket file is not a running copy", !SingleInstance.TryForward(socket, new string[0]));
        var received = new List<string[]>(); var signal = new AutoResetEvent(false);
        using (var first = SingleInstance.Listen(socket, args => { lock (received) received.Add(args); signal.Set(); }))
        {
            check("single instance: listens after replacing a stale socket", first != null);
            check("single instance: second copy hands over", SingleInstance.TryForward(socket, new[] { "--launch-game", "abc 123" }));
            check("single instance: running copy gets the arguments", signal.WaitOne(3000) && received.Count == 1 && received[0].Length == 2 && received[0][1] == "abc 123");
            check("single instance: a third copy cannot listen too", SingleInstance.Listen(socket, delegate { }) == null);
            Thread.Sleep(100);
            check("single instance: the liveness probe does not count as a launch", received.Count == 1);
        }
        check("single instance: socket removed on exit", !File.Exists(socket));
    }
}
