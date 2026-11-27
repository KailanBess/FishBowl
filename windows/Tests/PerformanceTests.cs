using System;
using System.Diagnostics;
using EmulatorHub;

class PerformanceTests
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static SessionProcessMetric Metric(string identity, double seconds, long bytes) { return new SessionProcessMetric { Identity = identity, ProcessorTicks = (long)(seconds * TimeSpan.TicksPerSecond), WorkingSetBytes = bytes }; }
    static void Main()
    {
        var sampler = new SessionPerformanceSampler();
        var first = sampler.Sample(new[] { Metric("one", 1, 100) }, 0, 1000, 1000, 4);
        Check(!first.CpuPercent.HasValue && first.WorkingSetBytes == 100, "First sample invented a CPU rate");
        var measured = sampler.Sample(new[] { Metric("one", 2, 120) }, 0, 2000, 1000, 4);
        Check(measured.CpuPercent.HasValue && Math.Abs(measured.CpuPercent.Value - 25) < .001, "CPU normalization incorrect");
        var changed = sampler.Sample(new[] { Metric("one", 3, 100), Metric("new", .5, 200) }, 0, 3000, 1000, 4);
        Check(!changed.CpuPercent.HasValue && changed.WorkingSetBytes == 300, "New process produced a complete rate");
        var missing = sampler.Sample(new[] { Metric("one", 4, 100) }, 1, 4000, 1000, 4);
        Check(!missing.CpuPercent.HasValue && missing.UnavailableProcesses == 1, "Missing process hidden");
        var reset = sampler.Sample(new[] { Metric("one", 1, 100) }, 0, 5000, 1000, 4);
        Check(!reset.CpuPercent.HasValue, "Reset CPU counter produced negative usage");
        var empty = sampler.Sample(new SessionProcessMetric[0], 0, 6000, 1000, 4);
        Check(!empty.CpuPercent.HasValue, "Empty session reported zero CPU");
        sampler.Sample(new[] { Metric("one", 1, 100), Metric("two", 1, 100) }, 0, 7000, 1000, 4);
        Check(!sampler.Sample(new[] { Metric("one", 2, 100) }, 0, 8000, 1000, 4).CpuPercent.HasValue, "Disappeared process produced a complete rate");
        sampler.Sample(new[] { Metric("one", 3, 100) }, 1, 9000, 1000, 4);
        Check(!sampler.Sample(new[] { Metric("one", 4, 100) }, 0, 10000, 1000, 4).CpuPercent.HasValue, "Previous incomplete sample produced a complete rate");
        using (var process = Process.GetCurrentProcess())
        {
            var identity = new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks };
            var read = SessionPerformance.ReadProcess(identity);
            Check(read.Identity == identity.Key && read.WorkingSetBytes > 0 && read.ProcessorTicks >= 0, "Real fixture process was not measured");
            identity.StartedUtcTicks++;
            try { SessionPerformance.ReadProcess(identity); throw new Exception("Reused PID accepted"); } catch (InvalidOperationException) { checks++; }
        }
        Console.WriteLine("PASS: " + checks + " session performance checks");
    }
}
