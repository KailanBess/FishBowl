using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EmulatorHub
{
    internal static class WindowsProfileLease
    {
        internal static FileStream Open(string directory)
        {
            string path = Path.Combine(directory, "profile.lock");
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked profile paths are not supported for concurrent-edit protection.");
            Directory.CreateDirectory(directory);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
    }
    public partial class MainForm
    {
        private void ShowReliabilityCenter()
        {
            using (var dialog = new NextDialog("Compatibility and recovery", 820, 620))
            {
                var fields = NextDialog.Fields(dialog.Body);
                var report = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
                Action refresh = delegate { report.Text = ReliabilityReport.Build(library); };
                NextDialog.Field(fields, "Readiness", report, 340);
                NextDialog.Field(fields, "Validation", new Label { AutoSize = true, Text = "Available programs and file paths do not prove renderer, audio or controller compatibility. In Play, use More → Input and session details to inspect the active renderer. Test audio, input, fullscreen, Move window, Open in window and Return to Play for each emulator build." }, 130);
                dialog.Action("Repair game paths", delegate { ExperienceTools.Relink(dialog, library); embeddedLibrary.ReloadLibrary(); homeSurface.Reload(); refresh(); });
                dialog.Action("Review emulators", delegate { dialog.Close(); workspaceNavigation.SelectedIndex = 1; });
                dialog.Action("Save history", delegate { using (var saves = new SaveHistoryDialog(library, null)) saves.ShowDialog(dialog); refresh(); });
                dialog.Action("Save report", delegate
                {
                    using (var picker = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "FishBowl-readiness.txt" })
                        if (picker.ShowDialog(dialog) == DialogResult.OK) File.WriteAllText(picker.FileName, report.Text, Encoding.UTF8);
                });
                dialog.Action("Close", dialog.Close);
                refresh(); dialog.ShowDialog(this);
            }
        }
    }

    public static class ReliabilityReport
    {
        public static string Build(LibraryData library)
        {
            var report = new StringBuilder();
            report.AppendLine("FishBowl compatibility and recovery review");
            report.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("u"));
            report.AppendLine("This report includes emulator names and counts, but omits paths, game titles, account details and credentials.");
            var games = library.Games ?? new List<GameEntry>();
            var emulators = library.Emulators ?? new List<EmulatorProfile>();
            report.AppendLine();
            report.AppendLine("Games: " + games.Count + "; missing game files: " + games.Count(g => !File.Exists(g.Path)));
            report.AppendLine("Emulators: " + emulators.Count + "; missing programs: " + emulators.Count(e => !File.Exists(e.Executable)));
            report.AppendLine("XInput controller: " + (NavigationController.HasConnectedController() ? "detected; emulator mapping still needs testing" : "not detected; other controller APIs may still work"));
            report.AppendLine();
            foreach (var emulator in emulators)
            {
                report.AppendLine(emulator.Name + ": " + (File.Exists(emulator.Executable) ? "program exists; live Play compatibility unverified by this check" : "program missing; repair its executable path in Emulators → Edit"));
            }
            report.AppendLine();
            report.AppendLine("Moved drive: Library → Repair game paths → Review moved. Choose the replacement folder and review the matching path count. Game identities and save associations stay together; no game files move.");
            report.AppendLine("Recovery: use Save history to verify and restore linked saves. A library-record backup does not include the referenced game, save or artwork files.");
            report.AppendLine("Remote play: configure a private token service and LiveKit deployment. Test the service, then verify video/audio and approve/revoke controls from a second device. A reachable service is not a successful stream.");
            report.AppendLine("Fallback: Open in window restores the emulator's own window when its renderer cannot work inside Play.");
            return report.ToString();
        }
    }
}
