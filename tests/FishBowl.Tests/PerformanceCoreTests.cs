using System;
using System.Diagnostics;
using EmulatorHub;

static class PerformanceCoreTests
{
    public static void Run()
    {
        var sampler = new SessionPerformanceSampler();
        var sample = new SessionProcessMetric { Identity = "fixture", ProcessorTicks = 0, WorkingSetBytes = 128 };
        if (sampler.Sample(new[] { sample }, 0, 1000, 1000, 2).CpuPercent.HasValue) throw new Exception("First CPU observation was not unknown");
        sample.ProcessorTicks = TimeSpan.TicksPerSecond;
        var next = sampler.Sample(new[] { sample }, 0, 2000, 1000, 2);
        if (!next.CpuPercent.HasValue || Math.Abs(next.CpuPercent.Value - 50) > .001) throw new Exception("Portable CPU calculation failed");
        using (var process = Process.GetCurrentProcess())
        {
            var metric = SessionPerformanceMetrics.ReadProcess(new SessionProcess { Pid = process.Id, StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks });
            if (metric.WorkingSetBytes <= 0 || metric.ProcessorTicks < 0) throw new Exception("Portable fixture process metrics unavailable");
        }
        Console.WriteLine("PASS portable session performance samples and process identity");
    }
}
