using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorHub
{
    // A permission FishBowl can add with `flatpak override --user`, after the user confirms the exact command.
    public sealed class FlatpakFix
    {
        public string Label = "", AppId = "", Option = "", Grant = "", Folder = "";
        public bool Device;
        public string[] Arguments { get { return FlatpakTool.OverrideArguments(Option, AppId); } }
        public string Command { get { return FlatpakTool.CommandLine("flatpak", Arguments); } }
        public static FlatpakFix ForFolder(string appId, string path, SandboxAccess need, bool create, string home)
        {
            var entry = FlatpakTool.GrantEntry(path, need, create, home);
            return new FlatpakFix { Label = "Allow access to " + FlatpakSetup.Short(path, home), AppId = appId, Option = "--filesystem=" + entry, Grant = "filesystems=" + entry, Folder = path };
        }
        public static FlatpakFix Controllers(string appId, string device)
        { return new FlatpakFix { Label = "Allow controller access", AppId = appId, Option = "--device=" + device, Grant = "devices=" + device, Device = true }; }
    }

    // A Setup checks row with an optional one-click override.
    public sealed class FlatpakHealthCheck : HealthCheck
    {
        public FlatpakFix Fix;
    }

    // A folder the emulator needs to reach, and how much access it needs.
    public sealed class FlatpakWantedFolder
    {
        public string Path, Label; public SandboxAccess Need;
    }

    // Setup checks rows for Flatpak emulators: can the sandbox reach the folders FishBowl knows about, and the controllers?
    public static class FlatpakSetup
    {
        public static string Short(string path, string home)
        {
            var text = path.StartsWith(home.TrimEnd('/') + "/", StringComparison.Ordinal) ? "~" + path.Substring(home.TrimEnd('/').Length) : path;
            return text.Length > 44 ? "…" + text.Substring(text.Length - 42) : text;
        }

        // Game folders come from the library: the folders holding this emulator's games (outermost first, at most 12),
        // its scan folder and FishBowl's game storage folder. Firmware and games only need reading; storage folders read-write.
        public static List<FlatpakWantedFolder> WantedFolders(EmulatorProfile profile, LibraryData library, IEnumerable<EmulatorFolder> detected)
        {
            var wanted = new List<FlatpakWantedFolder>();
            Action<string, string, SandboxAccess> add = (path, label, need) => { if (!String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)) wanted.Add(new FlatpakWantedFolder { Path = FlatpakPaths.Normalize(path), Label = label, Need = need }); };
            add(profile.FirmwareFolder, "firmware folder", SandboxAccess.ReadOnly);
            add(profile.ScanFolder, "game folder", SandboxAccess.ReadOnly);
            var gameDirs = (library.Games ?? new List<GameEntry>()).Where(g => g != null && !String.IsNullOrWhiteSpace(g.Path) && Path.IsPathRooted(g.Path) && GameLibraryQuery.EmulatorFor(library, g) == profile)
                .Select(g => FlatpakPaths.Normalize(Path.GetDirectoryName(g.Path))).Distinct().OrderBy(d => d.Length).ToList();
            var outer = new List<string>();
            foreach (var dir in gameDirs) if (!outer.Any(o => dir == o || dir.StartsWith(o + "/", StringComparison.Ordinal))) outer.Add(dir);
            foreach (var dir in outer.Take(12)) add(dir, "game folder", SandboxAccess.ReadOnly);
            var games = GameStorage.Root(library); if (Directory.Exists(games)) add(games, "FishBowl games folder", SandboxAccess.ReadOnly);
            foreach (var folder in (detected ?? new List<EmulatorFolder>()).Where(f => f.Property != "Program" && !String.IsNullOrWhiteSpace(f.Path)))
                add(folder.Path, folder.Label.Substring(0, 1).ToLowerInvariant() + folder.Label.Substring(1), SandboxAccess.ReadWrite);
            return wanted.GroupBy(w => w.Path).Select(g => new FlatpakWantedFolder { Path = g.Key, Label = String.Join(", ", g.Select(x => x.Label).Distinct()), Need = g.Max(x => x.Need) }).ToList();
        }

        public static List<HealthCheck> Checks(EmulatorProfile profile, LibraryData library)
        {
            var checks = new List<HealthCheck>();
            var id = Platform.FlatpakId(profile.Executable);
            if (Platform.IsWindows || id == null) return checks;
            var installation = FlatpakTool.Installation(profile.Executable, id);
            var version = FlatpakTool.InstalledVersion(); string error;
            var permissions = FlatpakTool.Permissions(id, installation, out error);
            if (permissions == null)
            {
                checks.Add(new FlatpakHealthCheck { Name = "Flatpak sandbox", Status = "Check unavailable", Detail = "flatpak info could not read " + id + " (" + installation + " installation): " + error });
                return checks;
            }
            var paths = FlatpakPaths.Current(); var home = FlatpakPaths.Normalize(paths.Home);
            var own = Path.Combine(home, ".var", "app", id);
            List<EmulatorFolder> detected = null;
            try { detected = new EmulatorFolderDetector().Detect(profile); }
            catch (Exception ex) { Store.Log("Flatpak folder checks: " + ex.Message); }
            var folders = WantedFolders(profile, library, detected);
            var outside = folders.Where(f => !(f.Path == own || f.Path.StartsWith(own + "/", StringComparison.Ordinal))).ToList();
            checks.Add(new FlatpakHealthCheck { Name = "Flatpak sandbox", Status = "Information", Detail = id + " (" + installation + " installation" + (version == null ? "" : ", Flatpak " + version) + "). "
                + (folders.Count - outside.Count) + " folder(s) are inside its own ~/.var/app folder and always available; " + outside.Count + " outside it are checked below." });

            foreach (var folder in outside)
            {
                var name = "Flatpak access: " + folder.Label; var exists = Directory.Exists(folder.Path);
                var decision = permissions.Access(folder.Path, id, paths);
                if (decision.Reserved) { checks.Add(new FlatpakHealthCheck { Name = name, Status = "Not shareable", Detail = folder.Path + " — " + decision.Via + ". Move it elsewhere to use it from the Flatpak." }); continue; }
                if (!exists && folder.Need == SandboxAccess.ReadOnly) { checks.Add(new FlatpakHealthCheck { Name = name, Status = "Missing", Detail = folder.Path + " — the folder does not exist." }); continue; }
                var reported = exists ? FlatpakTool.FileAccess(id, installation, folder.Path) : null;
                checks.Add(FolderRow(name, folder, decision, reported, exists, id, home));
            }
            checks.Add(ControllerRow(id, permissions.ControllerAccess(version), version));
            return checks;
        }

        // One folder row: Flatpak's own verdict (when it answered) wins over FishBowl's reading of the permissions.
        public static FlatpakHealthCheck FolderRow(string name, FlatpakWantedFolder folder, FlatpakAccessDecision decision, SandboxAccess? reported, bool exists, string id, string home)
        {
            var mode = reported ?? decision.Mode; var via = decision.Via;
            if (reported != null && reported != decision.Mode) via = reported == SandboxAccess.None ? "Flatpak reports it hidden; an override may exclude it" : "Flatpak reports it shared";
            bool ok = folder.Need == SandboxAccess.ReadOnly ? mode != SandboxAccess.None : mode == SandboxAccess.ReadWrite;
            var check = new FlatpakHealthCheck { Name = name };
            if (ok) { check.Status = mode == SandboxAccess.ReadOnly ? "Allowed (read-only)" : "Allowed"; check.Detail = folder.Path + " — through " + via + "."; }
            else
            {
                check.Status = mode == SandboxAccess.ReadOnly ? "Read-only" : "Blocked";
                check.Detail = folder.Path + " — " + (mode == SandboxAccess.ReadOnly ? "only readable (" + via + "), but the emulator writes here." : via + ".") + (exists ? "" : " The folder does not exist yet.");
                check.Fix = FlatpakFix.ForFolder(id, folder.Path, folder.Need, !exists, home);
            }
            return check;
        }

        public static FlatpakHealthCheck ControllerRow(string id, string controllers, Version version)
        {
            var row = new FlatpakHealthCheck { Name = "Flatpak access: controllers" };
            if (controllers == "all") { row.Status = "Allowed"; row.Detail = "devices=all lets it open game controllers, including ones that use raw HID access."; }
            else if (controllers == "input") { row.Status = "Allowed"; row.Detail = "devices=input lets it open controllers in /dev/input. Some adapters and motion sensors need raw HID access, which only all devices gives."; }
            else
            {
                row.Status = "Blocked"; row.Detail = "No device permission: the emulator cannot see game controllers.";
                row.Fix = FlatpakFix.Controllers(id, version != null && version < FlatpakPermissions.InputDeviceVersion ? "all" : "input");
            }
            return row;
        }
    }
}
