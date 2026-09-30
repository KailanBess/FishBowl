using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FishBowlSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox installPath = new TextBox();
        private readonly Button primary = new Button();
        private readonly Button back = new Button();
        private readonly Button cancel = new Button();
        private readonly Button browse = new Button();
        private readonly CheckBox desktopShortcut = new CheckBox();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Label title = new Label();
        private readonly Label detail = new Label();
        private readonly PictureBox logo = new PictureBox();
        private readonly Panel footerRule = new Panel();
        private readonly BackgroundWorker installer = new BackgroundWorker();
        private int page;
        private bool installed;

        public SetupForm()
        {
            Text = "FishBowl Setup";
            ClientSize = new Size(780, 520);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 20, 36);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            Icon = LoadIcon();

            var accent = Color.FromArgb(255, 127, 55);
            var purple = Color.FromArgb(147, 82, 255);

            var header = new Panel { Dock = DockStyle.Top, Height = 108, BackColor = Color.FromArgb(36, 29, 54) };
            logo.Image = LoadLogo();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Location = new Point(28, 18);
            logo.Size = new Size(72, 72);
            header.Controls.Add(logo);

            var brand = new Label { Text = "FishBowl", Font = new Font("Segoe UI Semibold", 21f), ForeColor = Color.White, AutoSize = true, Location = new Point(118, 24) };
            var subtitle = new Label { Text = "Emulator Hub Setup", Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(222, 211, 241), AutoSize = true, Location = new Point(120, 61) };
            header.Controls.Add(brand);
            header.Controls.Add(subtitle);
            Controls.Add(header);

            title.Font = new Font("Segoe UI Semibold", 16f);
            title.ForeColor = accent;
            title.AutoSize = true;
            title.Location = new Point(34, 142);
            Controls.Add(title);

            detail.ForeColor = Color.FromArgb(225, 220, 236);
            detail.Location = new Point(36, 182);
            detail.Size = new Size(620, 94);
            Controls.Add(detail);

            installPath.Location = new Point(36, 258);
            installPath.Size = new Size(505, 29);
            installPath.ReadOnly = true;
            installPath.BackColor = Color.FromArgb(49, 42, 66);
            installPath.ForeColor = Color.White;
            installPath.BorderStyle = BorderStyle.FixedSingle;
            installPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "FishBowl");
            Controls.Add(installPath);

            browse.Text = "Browse";
            browse.Location = new Point(553, 256);
            browse.Size = new Size(103, 32);
            browse.FlatStyle = FlatStyle.Flat;
            browse.FlatAppearance.BorderColor = purple;
            browse.ForeColor = Color.White;
            browse.BackColor = Color.FromArgb(57, 46, 79);
            browse.Click += Browse;
            Controls.Add(browse);

            desktopShortcut.Text = "Create a desktop shortcut";
            desktopShortcut.AutoSize = true;
            desktopShortcut.Checked = true;
            desktopShortcut.ForeColor = Color.FromArgb(225, 220, 236);
            desktopShortcut.Location = new Point(36, 304);
            Controls.Add(desktopShortcut);

            progress.Location = new Point(36, 305);
            progress.Size = new Size(620, 18);
            progress.Style = ProgressBarStyle.Continuous;
            progress.Visible = false;
            Controls.Add(progress);

            footerRule.BackColor = Color.FromArgb(72, 61, 93);
            Controls.Add(footerRule);

            back.Text = "Back";
            back.Location = new Point(382, 375);
            back.Size = new Size(84, 32);
            back.FlatStyle = FlatStyle.Flat;
            back.FlatAppearance.BorderColor = Color.FromArgb(102, 90, 127);
            back.ForeColor = Color.White;
            back.BackColor = Color.FromArgb(49, 42, 66);
            back.Click += delegate { page = Math.Max(0, page - 1); ShowPage(); };
            Controls.Add(back);

            cancel.Text = "Cancel";
            cancel.Location = new Point(476, 375);
            cancel.Size = new Size(84, 32);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(102, 90, 127);
            cancel.ForeColor = Color.White;
            cancel.BackColor = Color.FromArgb(49, 42, 66);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            primary.Text = "Next";
            primary.Location = new Point(570, 375);
            primary.Size = new Size(86, 32);
            primary.FlatStyle = FlatStyle.Flat;
            primary.FlatAppearance.BorderColor = accent;
            primary.ForeColor = Color.FromArgb(25, 19, 31);
            primary.BackColor = accent;
            primary.Font = new Font("Segoe UI Semibold", 9f);
            primary.Click += PrimaryClick;
            Controls.Add(primary);

            installer.WorkerReportsProgress = true;
            installer.DoWork += Install;
            installer.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { progress.Value = Math.Min(100, Math.Max(0, e.ProgressPercentage)); };
            installer.RunWorkerCompleted += InstallFinished;

            Resize += delegate { LayoutWizard(); };

            ShowPage();
            LayoutWizard();
        }

        private void ShowPage()
        {
            bool location = page == 1;
            bool complete = page == 2;
            back.Enabled = page == 1 && !installer.IsBusy;
            browse.Visible = installPath.Visible = desktopShortcut.Visible = location;
            progress.Visible = installer.IsBusy;
            cancel.Visible = !installed;

            if (page == 0)
            {
                title.Text = "Welcome to FishBowl";
                detail.Text = "This setup wizard installs FishBowl as a Windows app. You will be able to open it from Start and search for it like other installed applications.";
                primary.Text = "Next";
            }
            else if (location)
            {
                title.Text = "Choose an install location";
                detail.Text = "FishBowl will keep its app files here. The app's appearance and emulator behavior will not change.";
                primary.Text = "Install";
            }
            else if (complete)
            {
                title.Text = installed ? "FishBowl is installed" : "Installing FishBowl";
                detail.Text = installed ? "FishBowl is ready in the Start menu. You can launch it now or close this setup wizard." : "Copying FishBowl and creating its Windows shortcuts...";
                primary.Text = installed ? "Launch" : "Installing";
                primary.Enabled = installed;
            }
        }

        private void LayoutWizard()
        {
            int contentWidth = Math.Min(760, Math.Max(560, ClientSize.Width - 68));
            int contentLeft = Math.Max(34, (ClientSize.Width - contentWidth) / 2);
            int pageTop = 108 + Math.Max(34, (ClientSize.Height - 108 - 265) / 3);
            int footerTop = ClientSize.Height - 77;

            title.Location = new Point(contentLeft, pageTop);
            detail.Location = new Point(contentLeft + 2, pageTop + 40);
            detail.Size = new Size(contentWidth - 4, 94);

            installPath.Location = new Point(contentLeft, pageTop + 142);
            installPath.Size = new Size(contentWidth - 115, 29);
            browse.Location = new Point(contentLeft + contentWidth - 103, pageTop + 140);
            desktopShortcut.Location = new Point(contentLeft, pageTop + 188);
            progress.Location = new Point(contentLeft, pageTop + 189);
            progress.Size = new Size(contentWidth, 18);

            footerRule.Location = new Point(0, footerTop);
            footerRule.Size = new Size(ClientSize.Width, 1);
            primary.Location = new Point(ClientSize.Width - 44 - primary.Width, ClientSize.Height - 54);
            cancel.Location = new Point(primary.Left - 10 - cancel.Width, ClientSize.Height - 54);
            back.Location = new Point(cancel.Left - 10 - back.Width, ClientSize.Height - 54);
        }

        private void Browse(object sender, EventArgs e)
        {
            string folder = ModernFolderPicker.Select(Handle, installPath.Text);
            if (!String.IsNullOrEmpty(folder)) installPath.Text = Path.Combine(folder, "FishBowl");
        }

        private void PrimaryClick(object sender, EventArgs e)
        {
            if (installed)
            {
                Process.Start(new ProcessStartInfo(Path.Combine(installPath.Text, "FishBowl.exe")) { UseShellExecute = true });
                Close();
                return;
            }
            if (page == 0)
            {
                page = 1;
                ShowPage();
                return;
            }
            if (page == 1)
            {
                if (Directory.Exists(installPath.Text) && Directory.GetFileSystemEntries(installPath.Text).Length > 0 && MessageBox.Show(this, "FishBowl is already installed here. Replace the existing files?", "FishBowl Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                page = 2;
                primary.Enabled = false;
                back.Enabled = false;
                browse.Visible = installPath.Visible = desktopShortcut.Visible = false;
                progress.Visible = true;
                ShowPage();
                installer.RunWorkerAsync(new InstallOptions { Destination = installPath.Text, DesktopShortcut = desktopShortcut.Checked });
            }
        }

        private void Install(object sender, DoWorkEventArgs e)
        {
            var options = (InstallOptions)e.Argument;
            string source = AppDomain.CurrentDomain.BaseDirectory;
            var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            int total = Math.Max(1, files.Length);
            int copied = 0;
            foreach (string file in files)
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (ShouldSkip(relative)) continue;
                string target = Path.Combine(options.Destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                copied++;
                installer.ReportProgress((int)(copied * 85L / total));
            }

            string app = Path.Combine(options.Destination, "FishBowl.exe");
            string icon = Path.Combine(options.Destination, "FishBowl.ico");
            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FishBowl");
            Directory.CreateDirectory(startMenu);
            CreateShortcut(Path.Combine(startMenu, "FishBowl.lnk"), app, options.Destination, icon);
            installer.ReportProgress(92);

            if (options.DesktopShortcut)
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FishBowl.lnk"), app, options.Destination, icon);
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FishBowl"))
            {
                key.SetValue("DisplayName", "FishBowl");
                key.SetValue("DisplayVersion", "1.0");
                key.SetValue("Publisher", "FishBowl");
                key.SetValue("DisplayIcon", icon);
                key.SetValue("UninstallString", "\"" + Path.Combine(options.Destination, "Uninstall FishBowl.bat") + "\"");
            }
            installer.ReportProgress(100);
        }

        private void InstallFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show(this, "FishBowl could not be installed.\n\n" + e.Error.Message, "FishBowl Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                page = 1;
                primary.Enabled = true;
            }
            else
            {
                installed = true;
                progress.Value = 100;
            }
            ShowPage();
        }

        private static bool ShouldSkip(string relative)
        {
            string name = Path.GetFileName(relative);
            return name.Equals("FishBowl Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FishBowl Setup.cs", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("portable.flag", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Install FishBowl.bat", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Install FishBowl.ps1", StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateShortcut(string path, string target, string workingDirectory, string icon)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = icon + ",0";
            shortcut.Description = "FishBowl Emulator Hub";
            shortcut.Save();
        }

        private static Icon LoadIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FishBowl.ico");
            return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
        }

        private static Image LoadLogo()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.png"))
            using (Image image = Image.FromStream(stream)) return new Bitmap(image);
        }

        private sealed class InstallOptions
        {
            public string Destination;
            public bool DesktopShortcut;
        }

        private static class ModernFolderPicker
        {
            public static string Select(IntPtr owner, string initialFolder)
            {
                IFileDialog dialog = (IFileDialog)new FileOpenDialog();
                FileDialogOptions options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FileDialogOptions.PickFolders | FileDialogOptions.ForceFileSystem | FileDialogOptions.PathMustExist);
                dialog.SetTitle("Choose where FishBowl should be installed");
                dialog.SetOkButtonLabel("Choose folder");

                if (Directory.Exists(initialFolder))
                {
                    IShellItem initial;
                    Guid shellItem = typeof(IShellItem).GUID;
                    SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref shellItem, out initial);
                    dialog.SetFolder(initial);
                }

                if (dialog.Show(owner) != 0) return null;
                IShellItem selected;
                dialog.GetResult(out selected);
                IntPtr pathPointer;
                selected.GetDisplayName(ShellItemDisplayName.FileSystemPath, out pathPointer);
                try { return Marshal.PtrToStringUni(pathPointer); }
                finally { Marshal.FreeCoTaskMem(pathPointer); }
            }

            [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
            private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem item);
        }

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(FileDialogOptions options);
            void GetOptions(out FileDialogOptions options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, int placement);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid bhid, ref Guid riid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(ShellItemDisplayName displayName, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [Flags]
        private enum FileDialogOptions : uint
        {
            PickFolders = 0x00000020,
            ForceFileSystem = 0x00000040,
            PathMustExist = 0x00000800
        }

        private enum ShellItemDisplayName
        {
            FileSystemPath = unchecked((int)0x80058000)
        }
    }
}
