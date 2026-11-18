using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EmulatorHub;

class ReliabilityTests
{
    static int checks;
    static void Check(bool valid, string label) { checks++; if (!valid) throw new Exception(label); }
    static string Health(string body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var worker = Task.Run(delegate
        {
            using (var client = listener.AcceptTcpClient()) using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000;
                var request = new byte[4096]; int count = stream.Read(request, 0, request.Length);
                Check(Encoding.ASCII.GetString(request, 0, count).StartsWith("GET /api/health "), "readiness requests only the public health endpoint");
                var bytes = Encoding.UTF8.GetBytes(body);
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length);
            }
        });
        try { return RemotePlayTools.CheckService("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port); }
        finally { listener.Stop(); if (!worker.Wait(6000)) throw new Exception("Health fixture did not complete."); }
    }
    [STAThread] static int Main()
    {
        try
        {
            var data = Store.Load();
            data.Games = new List<GameEntry> { new GameEntry { Id = "fixture", Title = "Private fixture title", Path = Path.GetFullPath("missing-fixture.rom") } };
            data.Emulators = new List<EmulatorProfile> { new EmulatorProfile { Name = "Fixture emulator", Executable = Path.GetFullPath("missing-emulator.exe") } };
            string report = ReliabilityReport.Build(data);
            Check(report.Contains("missing game files: 1") && report.Contains("missing programs: 1"), "report counts unavailable paths");
            Check(!report.Contains("Private fixture title") && !report.Contains(Path.GetFullPath("missing-fixture.rom")), "report excludes game names and paths");
            Check(report.Contains("live Play compatibility") || report.Contains("program missing"), "report does not certify emulation");
            Check(Health("{\"service\":\"FishBowl remote play\",\"protocol\":1}").Contains("does not verify LiveKit"), "health distinguishes reachability from streaming");
            bool refused = false; try { Health("{\"service\":\"unrelated\",\"protocol\":1}"); } catch (IOException) { refused = true; }
            Check(refused, "unrelated web services do not pass readiness");
            string policy = Path.GetFullPath("notice-hours.json");
            File.WriteAllText(policy, "{\"Enabled\":true,\"Start\":22,\"End\":8,\"Apps\":[\"FishBowl\"]}");
            Check(AquariumFamily.SuiteNoticePolicy.Read(policy, "FishBowl", 23) && !AquariumFamily.SuiteNoticePolicy.Read(policy, "FishBowl", 10), "C# 5 quiet hours honors overnight bounds");
            Console.WriteLine("PASS: " + checks + " compatibility privacy, bounded service health and shared quiet-hour checks."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
