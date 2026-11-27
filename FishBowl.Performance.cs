using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace EmulatorHub
{
    public sealed class SessionProcessMetric
    {
        public string Identity;
        public long ProcessorTicks, WorkingSetBytes;
    }
    public sealed class SessionPerformanceReading
    {
        public double? CpuPercent;
        public long WorkingSetBytes;
        public int ObservedProcesses, UnavailableProcesses;
    }
    public sealed class SessionPerformanceSampler
    {
        Dictionary<string, long> previous = new Dictionary<string, long>();
        long previousTime;
        public SessionPerformanceReading Sample(IEnumerable<SessionProcessMetric> metrics, int unavailable, long now, long frequency, int processors)
        {
            if (frequency <= 0 || processors <= 0 || unavailable < 0) throw new ArgumentOutOfRangeException();
            var values = metrics.ToArray();
            if (values.Any(v => v == null || String.IsNullOrEmpty(v.Identity) || v.ProcessorTicks < 0 || v.WorkingSetBytes < 0) ||
                values.Select(v => v.Identity).Distinct().Count() != values.Length) throw new ArgumentException("Invalid process samples.");
            var result = new SessionPerformanceReading { ObservedProcesses = values.Length, UnavailableProcesses = unavailable };
            long delta = 0; bool complete = previousTime > 0 && now > previousTime && unavailable == 0 && values.Length > 0;
            foreach (var value in values)
            {
                result.WorkingSetBytes = checked(result.WorkingSetBytes + value.WorkingSetBytes);
                long before;
                if (!previous.TryGetValue(value.Identity, out before) || value.ProcessorTicks < before) complete = false;
                else delta = checked(delta + value.ProcessorTicks - before);
            }
            if (complete)
            {
                double elapsedSeconds = (now - previousTime) / (double)frequency;
                double percent = delta / (double)TimeSpan.TicksPerSecond / elapsedSeconds / processors * 100;
                if (percent >= 0 && percent <= 100.5) result.CpuPercent = Math.Min(100, percent);
            }
            previous = values.ToDictionary(v => v.Identity, v => v.ProcessorTicks); previousTime = now;
            return result;
        }
    }
    public static class SessionPerformanceMetrics
    {
        public static SessionProcessMetric ReadProcess(SessionProcess identity)
        {
            using (var process = Process.GetProcessById(identity.Pid))
            {
                process.Refresh();
                if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != identity.StartedUtcTicks)
                    throw new InvalidOperationException("The tracked process has exited or its identity changed.");
                return new SessionProcessMetric { Identity = identity.Key, ProcessorTicks = process.TotalProcessorTime.Ticks, WorkingSetBytes = process.WorkingSet64 };
            }
        }
    }
}
