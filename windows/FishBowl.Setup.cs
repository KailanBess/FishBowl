using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyDescription("Emulators, games and saves, organized together")]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyFileVersion("1.29.0.0")]
[assembly: CompilationRelaxations(8)]
[assembly: AssemblyTitle("FishBowl")]
[assembly: AssemblyVersion("1.29.0.0")]
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
		private sealed class OceanPanel : Panel
		{
			protected override void OnPaintBackground(PaintEventArgs e)
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(base.ClientRectangle, Color.FromArgb(3, 13, 38), Color.FromArgb(10, 52, 112), LinearGradientMode.Horizontal))
				{
					e.Graphics.FillRectangle(brush, base.ClientRectangle);
				}
			}
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
				IFileDialog fileDialog = (IFileDialog)new FileOpenDialog();
				FileDialogOptions options;
				fileDialog.GetOptions(out options);
				fileDialog.SetOptions(options | FileDialogOptions.PickFolders | FileDialogOptions.ForceFileSystem | FileDialogOptions.PathMustExist);
				fileDialog.SetTitle("Choose where FishBowl should be installed");
				fileDialog.SetOkButtonLabel("Choose folder");
				if (Directory.Exists(initialFolder))
				{
					Guid riid = typeof(IShellItem).GUID;
					IShellItem item;
					SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref riid, out item);
					fileDialog.SetFolder(item);
				}
				if (fileDialog.Show(owner) != 0)
				{
					return null;
				}
				IShellItem item2;
				fileDialog.GetResult(out item2);
				IntPtr name;
				item2.GetDisplayName(ShellItemDisplayName.FileSystemPath, out name);
				try
				{
					return Marshal.PtrToStringUni(name);
				}
				finally
				{
					Marshal.FreeCoTaskMem(name);
				}
			}

			[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
			private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem item);
		}

		[ComImport]
		[Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
		private class FileOpenDialog
		{
		}

		[ComImport]
		[Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IFileDialog
		{
			[PreserveSig]
			int Show(IntPtr parent);

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

		[ComImport]
		[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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
			PickFolders = 0x20u,
			ForceFileSystem = 0x40u,
			PathMustExist = 0x800u
		}

		private enum ShellItemDisplayName
		{
			FileSystemPath = -2147123200
		}

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
			base.ClientSize = new Size(780, 520);
			MinimumSize = new Size(700, 500);
			base.StartPosition = FormStartPosition.CenterScreen;
			BackColor = Color.FromArgb(3, 13, 38);
			ForeColor = Color.White;
			Font = new Font("Segoe UI", 10f);
			base.Icon = LoadIcon();
			Color color = Color.FromArgb(83, 170, 245);
			Color borderColor = Color.FromArgb(26, 105, 196);
			OceanPanel oceanPanel = new OceanPanel
			{
				Dock = DockStyle.Top,
				Height = 108,
				BackColor = Color.FromArgb(5, 31, 78)
			};
			logo.Image = LoadLogo();
			logo.SizeMode = PictureBoxSizeMode.Zoom;
			logo.Location = new Point(28, 18);
			logo.Size = new Size(72, 72);
			oceanPanel.Controls.Add(logo);
			Label value = new Label
			{
				Text = "FishBowl",
				Font = new Font("Segoe UI Semibold", 21f),
				ForeColor = Color.White,
				AutoSize = true,
				Location = new Point(118, 24)
			};
			Label value2 = new Label
			{
				Text = "FishBowl Setup 1.29.0",
				Font = new Font("Segoe UI", 10f),
				ForeColor = Color.FromArgb(157, 184, 225),
				AutoSize = true,
				Location = new Point(120, 61)
			};
			oceanPanel.Controls.Add(value);
			oceanPanel.Controls.Add(value2);
			base.Controls.Add(oceanPanel);
			title.Font = new Font("Segoe UI Semibold", 16f);
			title.ForeColor = color;
			title.AutoSize = true;
			title.Location = new Point(34, 142);
			base.Controls.Add(title);
			detail.ForeColor = Color.FromArgb(208, 224, 247);
			detail.Location = new Point(36, 182);
			detail.Size = new Size(620, 94);
			base.Controls.Add(detail);
			installPath.Location = new Point(36, 258);
			installPath.Size = new Size(505, 29);
			installPath.ReadOnly = true;
			installPath.BackColor = Color.FromArgb(10, 52, 112);
			installPath.ForeColor = Color.White;
			installPath.BorderStyle = BorderStyle.FixedSingle;
			installPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "FishBowl");
			base.Controls.Add(installPath);
			browse.Text = "Browse";
			browse.Location = new Point(553, 256);
			browse.Size = new Size(103, 32);
			browse.FlatStyle = FlatStyle.Flat;
			browse.FlatAppearance.BorderColor = borderColor;
			browse.ForeColor = Color.White;
			browse.BackColor = Color.FromArgb(10, 52, 112);
			browse.Click += Browse;
			base.Controls.Add(browse);
			desktopShortcut.Text = "Create a desktop shortcut";
			desktopShortcut.AutoSize = true;
			desktopShortcut.Checked = true;
			desktopShortcut.ForeColor = Color.FromArgb(208, 224, 247);
			desktopShortcut.Location = new Point(36, 304);
			base.Controls.Add(desktopShortcut);
			progress.Location = new Point(36, 305);
			progress.Size = new Size(620, 18);
			progress.Style = ProgressBarStyle.Continuous;
			progress.Visible = false;
			base.Controls.Add(progress);
			footerRule.BackColor = Color.FromArgb(83, 170, 245);
			base.Controls.Add(footerRule);
			back.Text = "Back";
			back.Location = new Point(382, 375);
			back.Size = new Size(84, 32);
			back.FlatStyle = FlatStyle.Flat;
			back.FlatAppearance.BorderColor = borderColor;
			back.ForeColor = Color.White;
			back.BackColor = Color.FromArgb(10, 52, 112);
			Button button = back;
			EventHandler value3 = delegate
			{
				page = Math.Max(0, page - 1);
				ShowPage();
			};
			button.Click += value3;
			base.Controls.Add(back);
			cancel.Text = "Cancel";
			cancel.Location = new Point(476, 375);
			cancel.Size = new Size(84, 32);
			cancel.FlatStyle = FlatStyle.Flat;
			cancel.FlatAppearance.BorderColor = borderColor;
			cancel.ForeColor = Color.White;
			cancel.BackColor = Color.FromArgb(10, 52, 112);
			cancel.Click += delegate
			{
				Close();
			};
			base.Controls.Add(cancel);
			primary.Text = "Next";
			primary.Location = new Point(570, 375);
			primary.Size = new Size(86, 32);
			primary.FlatStyle = FlatStyle.Flat;
			primary.FlatAppearance.BorderColor = color;
			primary.ForeColor = Color.FromArgb(3, 13, 38);
			primary.BackColor = color;
			primary.Font = new Font("Segoe UI Semibold", 9f);
			primary.Click += PrimaryClick;
			base.Controls.Add(primary);
			installer.WorkerReportsProgress = true;
			installer.DoWork += Install;
			installer.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
			{
				progress.Value = Math.Min(100, Math.Max(0, e.ProgressPercentage));
			};
			installer.RunWorkerCompleted += InstallFinished;
			base.Resize += delegate
			{
				LayoutWizard();
			};
			ShowPage();
			LayoutWizard();
		}

		private void ShowPage()
		{
			bool flag = page == 1;
			bool flag2 = page == 2;
			back.Enabled = page == 1 && !installer.IsBusy;
			Button button = browse;
			TextBox textBox = installPath;
			bool flag4 = (desktopShortcut.Visible = flag);
			bool flag5 = flag4;
			flag4 = (textBox.Visible = flag5);
			flag5 = (button.Visible = flag4);
			progress.Visible = installer.IsBusy;
			cancel.Visible = !installed;
			if (page == 0)
			{
				title.Text = "Welcome to FishBowl";
				detail.Text = "This setup wizard installs FishBowl as a Windows app. You will be able to open it from Start and search for it like other installed applications.";
				primary.Text = "Next";
			}
			else if (flag)
			{
				title.Text = "Choose an install location";
				detail.Text = "FishBowl will keep its app files here. The app's appearance and emulator behavior will not change.";
				primary.Text = "Install";
			}
			else if (flag2)
			{
				title.Text = (installed ? "FishBowl is installed" : "Installing FishBowl");
				detail.Text = (installed ? "FishBowl is ready in the Start menu. You can launch it now or close this setup wizard." : "Copying FishBowl and creating its Windows shortcuts...");
				primary.Text = (installed ? "Launch" : "Installing");
				primary.Enabled = installed;
			}
		}

		private void LayoutWizard()
		{
			int num = Math.Min(760, Math.Max(560, base.ClientSize.Width - 68));
			int num2 = Math.Max(34, (base.ClientSize.Width - num) / 2);
			int num3 = 108 + Math.Max(34, (base.ClientSize.Height - 108 - 265) / 3);
			int num4 = base.ClientSize.Height - 77;
			title.Location = new Point(num2, num3);
			detail.Location = new Point(num2 + 2, num3 + 40);
			detail.Size = new Size(num - 4, 94);
			installPath.Location = new Point(num2, num3 + 142);
			installPath.Size = new Size(num - 115, 29);
			browse.Location = new Point(num2 + num - 103, num3 + 140);
			desktopShortcut.Location = new Point(num2, num3 + 188);
			progress.Location = new Point(num2, num3 + 189);
			progress.Size = new Size(num, 18);
			footerRule.Location = new Point(0, num4);
			footerRule.Size = new Size(base.ClientSize.Width, 1);
			primary.Location = new Point(base.ClientSize.Width - 44 - primary.Width, base.ClientSize.Height - 54);
			cancel.Location = new Point(primary.Left - 10 - cancel.Width, base.ClientSize.Height - 54);
			back.Location = new Point(cancel.Left - 10 - back.Width, base.ClientSize.Height - 54);
		}

		private void Browse(object sender, EventArgs e)
		{
			string text = ModernFolderPicker.Select(base.Handle, installPath.Text);
			if (!string.IsNullOrEmpty(text))
			{
				installPath.Text = Path.Combine(text, "FishBowl");
			}
		}

		private void PrimaryClick(object sender, EventArgs e)
		{
			if (installed)
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(Path.Combine(installPath.Text, "FishBowl.exe"));
				processStartInfo.UseShellExecute = true;
				Process.Start(processStartInfo);
				Close();
			}
			else if (page == 0)
			{
				page = 1;
				ShowPage();
			}
			else if (page == 1 && (!Directory.Exists(installPath.Text) || Directory.GetFileSystemEntries(installPath.Text).Length <= 0 || MessageBox.Show(this, "FishBowl is already installed here. Replace the existing files?", "FishBowl Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes))
			{
				page = 2;
				primary.Enabled = false;
				back.Enabled = false;
				Button button = browse;
				TextBox textBox = installPath;
				bool flag2 = (desktopShortcut.Visible = false);
				bool flag3 = flag2;
				flag2 = (textBox.Visible = flag3);
				flag3 = (button.Visible = flag2);
				progress.Visible = true;
				ShowPage();
				installer.RunWorkerAsync(new InstallOptions
				{
					Destination = installPath.Text,
					DesktopShortcut = desktopShortcut.Checked
				});
			}
		}

		private void Install(object sender, DoWorkEventArgs e)
		{
			InstallOptions installOptions = (InstallOptions)e.Argument;
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string[] files = Directory.GetFiles(baseDirectory, "*", SearchOption.AllDirectories);
			int num = Math.Max(1, files.Length);
			int num2 = 0;
			string[] array = files;
			string[] array2 = array;
			foreach (string text in array2)
			{
				string text2 = text.Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				if (!ShouldSkip(text2))
				{
					string text3 = Path.Combine(installOptions.Destination, text2);
					Directory.CreateDirectory(Path.GetDirectoryName(text3));
					File.Copy(text, text3, true);
					num2++;
					installer.ReportProgress((int)((long)num2 * 85L / num));
				}
			}
			string target = Path.Combine(installOptions.Destination, "FishBowl.exe");
			string value = Path.Combine(installOptions.Destination, "FishBowl.ico");
			string text4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FishBowl");
			Directory.CreateDirectory(text4);
			CreateShortcut(Path.Combine(text4, "FishBowl.lnk"), target, installOptions.Destination, value);
			installer.ReportProgress(92);
			if (installOptions.DesktopShortcut)
			{
				CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FishBowl.lnk"), target, installOptions.Destination, value);
			}
			RefreshShellIcons();
			using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\FishBowl"))
			{
				registryKey.SetValue("DisplayName", "FishBowl");
				registryKey.SetValue("DisplayVersion", "1.29.0");
				registryKey.SetValue("Publisher", "FishBowl");
				registryKey.SetValue("DisplayIcon", value);
				registryKey.SetValue("UninstallString", "\"" + Path.Combine(installOptions.Destination, "Uninstall FishBowl.bat") + "\"");
			}
			installer.ReportProgress(100);
		}

		private void InstallFinished(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				MessageBox.Show(this, "FishBowl could not be installed.\n\n" + e.Error.Message, "FishBowl Setup", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
			if (!string.Equals(relative, Path.GetFileName(relative), StringComparison.Ordinal))
			{
				return true;
			}
			string[] array = new string[5] { "FishBowl.exe", "FishBowl.ico", "Uninstall FishBowl.bat", "Uninstall FishBowl.ps1", "README.md" };
			string[] array2 = array;
			string[] array3 = array2;
			foreach (string value in array3)
			{
				if (relative.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
			}
			return true;
		}

		private static void CreateShortcut(string path, string target, string workingDirectory, string icon)
		{
			dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
			dynamic val2 = val.CreateShortcut(path);
			val2.TargetPath = target;
			val2.WorkingDirectory = workingDirectory;
			val2.IconLocation = icon + ",0";
			val2.Description = "FishBowl Emulator Hub";
			val2.Save();
		}

		[DllImport("shell32.dll")]
		private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

		private static void RefreshShellIcons()
		{
			try
			{
				SHChangeNotify(134217728u, 0u, IntPtr.Zero, IntPtr.Zero);
			}
			catch
			{
			}
		}

		private static Icon LoadIcon()
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FishBowl.ico");
			return File.Exists(text) ? new Icon(text) : SystemIcons.Application;
		}

		private static Image LoadLogo()
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FishBowl.png"))
			{
				using (Image original = Image.FromStream(stream))
				{
					return new Bitmap(original);
				}
			}
		}
	}
}
