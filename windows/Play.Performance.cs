using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EmulatorHub
{
    public static class SessionPerformance
    {
        public static SessionProcessMetric ReadProcess(SessionProcess identity) { return SessionPerformanceMetrics.ReadProcess(identity); }
        public static string BatteryText(PowerStatus power)
        {
            string charge = power.BatteryChargeStatus == BatteryChargeStatus.Unknown || power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) ||
                float.IsNaN(power.BatteryLifePercent) || float.IsInfinity(power.BatteryLifePercent) || power.BatteryLifePercent < 0 || power.BatteryLifePercent > 1
                ? "Battery charge: unavailable" : "Battery charge: " + (power.BatteryLifePercent * 100).ToString("F0") + "%";
            string remaining = power.BatteryLifeRemaining < 0 ? "remaining time not reported" : TimeSpan.FromSeconds(power.BatteryLifeRemaining).ToString();
            return charge + "; power: " + power.PowerLineStatus + "; " + remaining + ". Windows estimates are not a measured discharge rate.";
        }
        public static void Show(IWin32Window owner, Func<SessionProcess[]> identities)
        {
            var dialog = new NextDialog("Session performance", 800, 560);
            var fields = NextDialog.Fields(dialog.Body);
            var report = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, AccessibleName = "Measured session resources" };
            NextDialog.Field(fields, "Measurements", report, 250);
            NextDialog.Field(fields, "Coverage", new Label { AutoSize = true, Text = "CPU is normalized to the computer's logical processors. Memory is summed process working sets, not unique physical RAM. Missing/new processes produce an unknown CPU rate. FPS, frame times, GPU use, temperatures and power consumption are not measured by this view." }, 140);
            dialog.Action("Close", dialog.Close);
            var sampler = new SessionPerformanceSampler(); bool busy = false;
            var timer = new Timer { Interval = 1000 };
            timer.Tick += async delegate
            {
                if (busy || dialog.IsDisposed) return; busy = true;
                try
                {
                    var known = identities().GroupBy(i => i.Key).Select(g => g.First()).Take(129).ToArray();
                    if (known.Length > 128) { report.Text = "Session process limit exceeded; measurements unavailable."; return; }
                    var observation = await System.Threading.Tasks.Task.Run(delegate
                    {
                        var values = new List<SessionProcessMetric>(); int unavailable = 0;
                        foreach (var identity in known) try { values.Add(ReadProcess(identity)); } catch { unavailable++; }
                        return sampler.Sample(values, unavailable, Stopwatch.GetTimestamp(), Stopwatch.Frequency, Environment.ProcessorCount);
                    });
                    if (dialog.IsDisposed) return;
                    var text = new StringBuilder();
                    text.AppendLine("CPU: " + (observation.CpuPercent.HasValue ? observation.CpuPercent.Value.ToString("F1") + "%" : "Unknown; waiting for complete consecutive samples"));
                    text.AppendLine("Process working sets: " + (observation.ObservedProcesses == 0 ? "Unknown" : (observation.WorkingSetBytes / 1048576.0).ToString("F1") + " MiB" + (observation.UnavailableProcesses > 0 ? " (partial)" : "")));
                    text.AppendLine("Observed processes: " + observation.ObservedProcesses + "; unavailable: " + observation.UnavailableProcesses);
                    text.AppendLine(); text.AppendLine(BatteryText(SystemInformation.PowerStatus));
                    text.AppendLine(); text.AppendLine("Read-only sampling while this window is open. No game settings, process priorities or power plans are changed.");
                    report.Text = text.ToString();
                }
                catch (Exception) { if (!dialog.IsDisposed) report.Text = "Session measurements are unavailable. No healthy or zero-load result is inferred."; }
                finally { busy = false; }
            };
            dialog.FormClosed += delegate { timer.Stop(); timer.Dispose(); };
            dialog.Shown += delegate { timer.Start(); };
            dialog.Show(owner);
        }
    }
}
