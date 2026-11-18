using System;
using System.IO;
using System.Linq;

namespace AquariumFamily
{
    public sealed class NoticeHours
    {
        public NoticeHours() { Apps = new string[0]; }
        public bool Enabled { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public string[] Apps { get; set; }
    }

    public static class SuiteNoticePolicy
    {
        public static bool IsQuiet(string app, DateTime local)
        {
            return Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aquarium", "notification-policy.json"), app, local.Hour);
        }

        public static bool Read(string path, string app, int hour)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) return false;
                string json = File.ReadAllText(path);
#if NETCOREAPP
                NoticeHours policy = System.Text.Json.JsonSerializer.Deserialize<NoticeHours>(json) ?? new NoticeHours();
#else
                NoticeHours policy = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 4096 }.Deserialize<NoticeHours>(json);
#endif
                if (policy == null || !policy.Enabled || policy.Apps == null || policy.Apps.Length > 2 || policy.Apps.Any(a => a != "FishBowl" && a != "FishTank") ||
                    policy.Start < 0 || policy.Start > 23 || policy.End < 0 || policy.End > 23 || hour < 0 || hour > 23 || !policy.Apps.Contains(app) || policy.Start == policy.End) return false;
                return policy.Start < policy.End ? hour >= policy.Start && hour < policy.End : hour >= policy.Start || hour < policy.End;
            }
            catch { return false; } // An unavailable shared preference must not suppress alerts.
        }
    }
}
