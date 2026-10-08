using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorHub
{
    // Linux-only bookkeeping kept beside library.json (linux-integrations.json) so the shared library format stays
    // unchanged: the Flatpak overrides FishBowl added, keyed by Flatpak application ID ("filesystems=<entry>", "devices=<name>").
    public sealed class LinuxIntegrationState
    {
        public Dictionary<string, List<string>> FlatpakGrants { get; set; }

        public static string FileName { get { return Path.Combine(Store.DataDirectory, "linux-integrations.json"); } }

        public static LinuxIntegrationState Load() { return Load(FileName); }
        public static LinuxIntegrationState Load(string file)
        {
            LinuxIntegrationState state = null;
            try { if (File.Exists(file)) state = Json.Deserialize<LinuxIntegrationState>(File.ReadAllText(file)); }
            catch (Exception error) { Store.Log("Linux integration state unreadable, starting fresh: " + error.Message); }
            state = state ?? new LinuxIntegrationState();
            if (state.FlatpakGrants == null) state.FlatpakGrants = new Dictionary<string, List<string>>();
            return state;
        }
        public void Save() { Save(FileName); }
        public void Save(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var temporary = file + ".tmp";
            File.WriteAllText(temporary, Json.Serialize(this)); File.Move(temporary, file, true);
        }

        public List<string> Grants(string appId)
        {
            List<string> list; return appId != null && FlatpakGrants.TryGetValue(appId, out list) ? list : new List<string>();
        }
        public void AddGrant(string appId, string grant)
        {
            List<string> list; if (!FlatpakGrants.TryGetValue(appId, out list)) FlatpakGrants[appId] = list = new List<string>();
            if (!list.Contains(grant)) list.Add(grant);
        }
        public void RemoveGrant(string appId, string grant)
        {
            List<string> list; if (!FlatpakGrants.TryGetValue(appId, out list)) return;
            list.Remove(grant); if (list.Count == 0) FlatpakGrants.Remove(appId);
        }
    }
}
