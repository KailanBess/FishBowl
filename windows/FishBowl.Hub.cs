using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System;
namespace EmulatorHub
{
	public class EmulatorAdapter
	{
		public string Name { get; set; }

		public string Platform { get; set; }

		public string Arguments { get; set; }

		public List<string> Extensions { get; set; }

		public string Website { get; set; }

		public string Repository { get; set; }
	}
}

namespace EmulatorHub
{
	public class GameExtras
	{
		public bool Native { get; set; }

		public string WorkingDirectory { get; set; }

		public string TrailerUrl { get; set; }

		public string MetadataSource { get; set; }

		public Dictionary<string, string> Fields { get; set; }

		public GameExtras()
		{
			Fields = new Dictionary<string, string>();
		}
	}
}

namespace EmulatorHub
{
	public static class Hub
	{
		public static HubSettings Ensure(LibraryData d)
		{
			if (d.Hub == null)
			{
				d.Hub = new HubSettings();
			}
			HubSettings hub = d.Hub;
			if (hub.Adapters == null)
			{
				hub.Adapters = new List<EmulatorAdapter>();
			}
			if (hub.Paths == null)
			{
				hub.Paths = new List<PathBinding>();
			}
			if (hub.Activity == null)
			{
				hub.Activity = new List<HubActivity>();
			}
			if (hub.SyncHashes == null)
			{
				hub.SyncHashes = new Dictionary<string, string>();
			}
			return hub;
		}

		public static GameExtras Extra(GameEntry g)
		{
			if (g.Extras == null)
			{
				g.Extras = new GameExtras();
			}
			if (g.Extras.Fields == null)
			{
				g.Extras.Fields = new Dictionary<string, string>();
			}
			return g.Extras;
		}

		public static bool Native(GameEntry g)
		{
			return g != null && g.Extras != null && g.Extras.Native;
		}

		public static EmulatorProfile NativeProfile(GameEntry g)
		{
			EmulatorProfile emulatorProfile = new EmulatorProfile();
			emulatorProfile.Id = "native-" + g.Id;
			emulatorProfile.Name = "PC / shortcut";
			emulatorProfile.Executable = g.Path;
			emulatorProfile.Arguments = g.Arguments;
			emulatorProfile.Extensions = new List<string>();
			emulatorProfile.LaunchProfiles = new List<LaunchProfile>();
			return emulatorProfile;
		}

		public static string NativeArguments(GameEntry g)
		{
			if (!File.Exists(g.Path))
			{
				throw new IOException("The PC game or shortcut is missing. Repair its path.");
			}
			string text = Path.GetExtension(g.Path).ToLowerInvariant();
			if (text != ".exe" && text != ".lnk")
			{
				throw new IOException("PC entries support .exe or .lnk files.");
			}
			if ((g.Arguments ?? "").Contains("{") || (g.Arguments ?? "").Contains("}"))
			{
				throw new IOException("PC arguments must be literal text; templates are not supported.");
			}
			if (g.Extras != null && !string.IsNullOrWhiteSpace(g.Extras.WorkingDirectory) && !Directory.Exists(g.Extras.WorkingDirectory))
			{
				throw new IOException("The PC game's working folder is missing.");
			}
			if(Platform.IsDirectProgram(g.Path)&&EmulatorRuntime.State(g.Path)==RuntimeState.Running)throw new IOException("The PC game is already running. Close it before starting another tracked session.");
			return g.Arguments ?? "";
		}

		public static string SearchText(GameEntry g)
		{
			return string.Join(" ", g.Title, g.Path, g.Genre, g.Developer, g.ReleaseYear, g.Description, g.Notes, g.ConsoleLabel, g.PlayStatus, string.Join(" ", g.Tags ?? new List<string>()), (g.Extras == null || g.Extras.Fields == null) ? "" : string.Join(" ", g.Extras.Fields.Select((KeyValuePair<string, string> p) => p.Key + " " + p.Value)));
		}

		public static void Record(LibraryData d, string text)
		{
			HubSettings hubSettings = Ensure(d);
			hubSettings.Activity.Add(new HubActivity
			{
				At = DateTime.UtcNow.ToString("o"),
				UserId = UserTools.Ensure(d).ActiveId,
				Text = text
			});
			if (hubSettings.Activity.Count > 500)
			{
				hubSettings.Activity.RemoveRange(0, hubSettings.Activity.Count - 500);
			}
		}

		public static void Idle(LibraryData d)
		{
			if (UserTools.Guest || UserTools.ActiveLaunches > 0 || WorkGate.Busy > 0 || Application.OpenForms.OfType<MainForm>().Any(main=>main.HubBusy))
			{
				throw new IOException("Finish active sessions and background work before changing Library paths or profiles.");
			}
		}

		public static IEnumerable<GameEntry> NestedEntries(LibraryData d, GameCollection parent, IEnumerable<GameEntry> source)
		{
			HashSet<string> ids = new HashSet<string>();
			Queue<string> queue = new Queue<string>();
			queue.Enqueue(parent.Id);
			while (queue.Count > 0)
			{
				string id = queue.Dequeue();
				if (!ids.Add(id))
				{
					continue;
				}
				foreach (GameCollection item in d.Collections.Where((GameCollection c) => c.ParentId == id))
				{
					queue.Enqueue(item.Id);
				}
			}
			List<GameEntry> games = source.ToList();
			HashSet<string> wanted = new HashSet<string>(from g in d.Collections.Where((GameCollection c) => ids.Contains(c.Id)).SelectMany((GameCollection c) => GameCollections.Entries(c, games))
				select g.Id);
			return games.Where((GameEntry g) => wanted.Contains(g.Id));
		}

		public static void Parent(LibraryData d, GameCollection child, string parent)
		{
			if (parent == child.Id)
			{
				throw new IOException("A collection cannot contain itself.");
			}
			HashSet<string> hashSet = new HashSet<string>();
			hashSet.Add(child.Id);
			HashSet<string> hashSet2 = hashSet;
			string next = parent;
			while (!string.IsNullOrWhiteSpace(next))
			{
				if (!hashSet2.Add(next))
				{
					throw new IOException("That parent would create a collection cycle.");
				}
				GameCollection gameCollection = d.Collections.FirstOrDefault((GameCollection x) => x.Id == next);
				if (gameCollection == null)
				{
					throw new IOException("Parent collection is unavailable.");
				}
				next = gameCollection.ParentId;
			}
			child.ParentId = parent;
		}

		public static string Readiness(LibraryData d, GameEntry g)
		{
			StringBuilder stringBuilder = new StringBuilder(g.Title + "\r\n");
			try
			{
				string text = GameSessions.Validate(d, g);
				EmulatorProfile emulatorProfile = NextData.LaunchEmulator(d, g);
				stringBuilder.AppendLine("Ready to start: configured launch files and arguments pass validation.");
				stringBuilder.AppendLine("Program: " + emulatorProfile.Executable);
				stringBuilder.AppendLine("Arguments: " + text);
				if (!Native(g))
				{
					SetupChecklist.Ensure(emulatorProfile);
					stringBuilder.AppendLine("Emulator setup checklist: " + SetupChecklist.CompleteCount(emulatorProfile) + " / " + emulatorProfile.SetupChecklist.Count + ". Review firmware, graphics and controller requirements inside the emulator.");
				}
			}
			catch (Exception ex)
			{
				stringBuilder.AppendLine("Needs attention: " + ex.Message);
			}
			stringBuilder.AppendLine("A successful path check does not establish compatibility inside the game or emulator.");
			return stringBuilder.ToString();
		}

		public static bool PreviewImport(IWin32Window owner, LibraryData d, List<GameEntry> found)
		{
			if (found == null)
			{
				return false;
			}
			HashSet<string> hashSet = new HashSet<string>(d.Games.Select((GameEntry g) => g.Path ?? ""), StringComparer.OrdinalIgnoreCase);
			List<GameEntry> list2 = new List<GameEntry>();
			int num = 0;
			foreach (GameEntry item in found)
			{
				if (!hashSet.Add(item.Path ?? ""))
				{
					num++;
				}
				else
				{
					list2.Add(item);
				}
			}
			if (list2.Count == 0)
			{
				UserTools.Report(owner, "Folder import", "No new matching files. Skipped existing or duplicate paths: " + num);
				return false;
			}
			NextDialog f = new NextDialog("Review folder import", 900, 630);
			try
			{
				ListView list = new ListView
				{
					Dock = DockStyle.Fill,
					View = View.Details,
					CheckBoxes = true,
					FullRowSelect = true,
					AccessibleName = "Games to import"
				};
				list.Columns.Add("Title", 220);
				list.Columns.Add("Path", 480);
				list.Columns.Add("Emulator", 150);
				foreach (GameEntry g2 in list2)
				{
					List<EmulatorProfile> emulators = d.Emulators;
					Func<EmulatorProfile, bool> predicate = (EmulatorProfile x) => x.Id == g2.EmulatorId;
					EmulatorProfile emulatorProfile = emulators.FirstOrDefault(predicate);
					list.Items.Add(new ListViewItem(new string[3]
					{
						g2.Title,
						g2.Path,
						(emulatorProfile == null) ? "Unassigned" : emulatorProfile.Name
					})
					{
						Checked = true,
						Tag = g2
					});
				}
				f.Body.Controls.Add(list);
				f.Body.Controls.Add(new Label
				{
					Dock = DockStyle.Top,
					Height = 55,
					Text = "Uncheck files you do not want. Existing/duplicate paths skipped: " + num + ". No files will be moved. Disc grouping remains available in Game actions."
				});
				f.Action("Import checked", delegate
				{
					Idle(d);
					foreach (ListViewItem checkedItem in list.CheckedItems)
					{
						d.Games.Add((GameEntry)checkedItem.Tag);
					}
					Record(d, "Imported " + list.CheckedItems.Count + " games");
					Store.Save(d);
					f.DialogResult = DialogResult.OK;
				});
				f.Action("Cancel", f.Close);
				return f.ShowDialog(owner) == DialogResult.OK;
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static void ImportFolder(IWin32Window owner, LibraryData d)
		{
			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
			folderBrowserDialog.Description = "Choose a game folder to preview";
			using (FolderBrowserDialog folderBrowserDialog2 = folderBrowserDialog)
			{
				if (folderBrowserDialog2.ShowDialog(owner) != DialogResult.OK)
				{
					return;
				}
				LibraryData snapshot = UserTools.Copy(d);
				foreach (EmulatorProfile emulator in snapshot.Emulators)
				{
					emulator.ScanFolder = folderBrowserDialog2.SelectedPath;
				}
				List<GameEntry> found = BackgroundWork<List<GameEntry>>.Run(owner, "Preview game folder", (CancellationToken token, Action<string> progress) => LibraryJobs.Scan(snapshot, token, progress));
				PreviewImport(owner, d, found);
			}
		}

		public static void AddPc(IWin32Window owner, LibraryData d)
		{
			OpenFileDialog file = new OpenFileDialog
			{
				Filter = "PC game or shortcut|*.exe;*.lnk"
			};
			try
			{
				if (file.ShowDialog(owner) != DialogResult.OK)
				{
					return;
				}
				TextPromptDialog textPromptDialog = new TextPromptDialog("PC game", "Display name");
				textPromptDialog.Value = Path.GetFileNameWithoutExtension(file.FileName);
				using (TextPromptDialog textPromptDialog2 = textPromptDialog)
				{
					if (textPromptDialog2.ShowDialog(owner) == DialogResult.OK)
					{
						if (string.IsNullOrWhiteSpace(textPromptDialog2.Value))
						{
							throw new IOException("Enter a game name.");
						}
						if (d.Games.Any((GameEntry g) => string.Equals(g.Path, file.FileName, StringComparison.OrdinalIgnoreCase)))
						{
							throw new IOException("This path is already in the Library.");
						}
						d.Games.Add(new GameEntry
						{
							Id = Guid.NewGuid().ToString("N"),
							Title = textPromptDialog2.Value.Trim(),
							Path = file.FileName,
							AddedAt = DateTime.UtcNow.ToString("o"),
							Tags = new List<string>(),
							ConsoleLabel = "PC",
							Extras = new GameExtras
							{
								Native = true
							}
						});
						Record(d, "Added PC game: " + textPromptDialog2.Value);
						Store.Save(d);
					}
				}
			}
			finally
			{
				if (file != null)
				{
					((IDisposable)file).Dispose();
				}
			}
		}

		public static void Collections(IWin32Window owner, LibraryData d)
		{
			NextDialog f = new NextDialog("Nested collections", 800, 560);
			try
			{
				TableLayoutPanel table = NextDialog.Fields(f.Body);
				List<GameCollection> collections = d.Collections.OrderBy((GameCollection c) => c.Name).ToList();
				ComboBox child = NextDialog.Choice(collections.Select((GameCollection c) => c.Name), null);
				ComboBox parents = NextDialog.Choice(new string[1] { "No parent" }.Concat(collections.Select((GameCollection c) => c.Name)), "No parent");
				NextDialog.Field(table, "Collection", child);
				NextDialog.Field(table, "Parent", parents);
				NextDialog.Field(table, "", new Label
				{
					Text = "A parent includes games from all its descendants. Existing collection membership remains editable through Collections.",
					AutoSize = true
				}, 95);
				child.SelectedIndexChanged += delegate
				{
					if (child.SelectedIndex >= 0)
					{
						GameCollection c2 = collections[child.SelectedIndex];
						GameCollection gameCollection = collections.FirstOrDefault((GameCollection x) => x.Id == c2.ParentId);
						parents.SelectedItem = ((gameCollection == null) ? "No parent" : gameCollection.Name);
					}
				};
				f.Action("Save parent", delegate
				{
					if (child.SelectedIndex < 0)
					{
						throw new IOException("Create a collection first.");
					}
					GameCollection child2 = collections[child.SelectedIndex];
					Parent(d, child2, (parents.SelectedIndex <= 0) ? null : collections[parents.SelectedIndex - 1].Id);
					Store.Save(d);
					f.Close();
				});
				f.Action("Close", f.Close);
				if (child.Items.Count > 0)
				{
					child.SelectedIndex = -1;
					child.SelectedIndex = 0;
				}
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static void Activity(IWin32Window owner, LibraryData d)
		{
			string user = UserTools.Ensure(d).ActiveId;
			List<string> list = new List<string>();
			foreach (HubActivity item in Ensure(d).Activity.Where((HubActivity a) => a.UserId == user))
			{
				list.Add(item.At + " | " + item.Text);
			}
			foreach (GameEntry item2 in d.Games.Where((GameEntry g) => !string.IsNullOrWhiteSpace(g.LastLaunched)))
			{
				list.Add(item2.LastLaunched + " | Played " + item2.Title);
			}
			foreach (SaveSnapshot s in d.SaveSnapshots)
			{
				list.Add(s.CreatedAt + " | Save snapshot: " + (d.Games.FirstOrDefault((GameEntry g) => g.Id == s.GameId) ?? new GameEntry
				{
					Title = "Unknown game"
				}).Title);
			}
			using (ResultsDialog resultsDialog = new ResultsDialog("Recent activity", list.OrderByDescending(ParseActivityDate).Take(250)))
			{
				resultsDialog.ShowDialog(owner);
			}
		}

		private static DateTime ParseActivityDate(string value)
		{
			DateTime result;
			return DateTime.TryParse(value.Split('|')[0].Trim(), out result) ? result.ToUniversalTime() : DateTime.MinValue;
		}

		public static string Relative(string root, string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return null;
			}
			root = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
			path = Path.GetFullPath(path);
            if(path.TrimEnd('\\','/').Equals(root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))return ".";
			return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : null;
		}

		public static string Resolve(string root, string relative)
		{
			if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
			{
				throw new IOException("Expected a relative path.");
			}
			if(relative==".")return Path.GetFullPath(root);
            string text = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
			string fullPath = Path.GetFullPath(Path.Combine(text, relative));
			if (!fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("Path escapes the chosen collection folder.");
			}
			return fullPath;
		}

		public static List<PathBinding> Bind(LibraryData d, string root)
		{
			List<PathBinding> list = new List<PathBinding>();
			foreach (KeyValuePair<string, string> item in PathFields(d))
			{
				string text = Relative(root, item.Value);
				if (text != null)
				{
					list.Add(new PathBinding
					{
						Key = item.Key,
						Relative = text, Original=item.Value
					});
				}
			}
			return list;
		}

        static List<Tuple<string,string,Action<string>>> ExtraPaths(LibraryData d){
            var refs=new List<Tuple<string,string,Action<string>>>();
            Action<string,string,Action<string>> add=(key,value,set)=>refs.Add(Tuple.Create(key,value,set));
            add("root:games",d.GameLibraryRoot,value=>d.GameLibraryRoot=value);add("root:emulators",d.EmulatorRootDirectory,value=>d.EmulatorRootDirectory=value);add("root:requirements",d.RequirementsLibraryRoot,value=>d.RequirementsLibraryRoot=value);add("root:backups",d.BackupFolder,value=>d.BackupFolder=value);
            if(d.Cosmetics!=null)add("appearance:wallpaper",d.Cosmetics.WallpaperPath,value=>d.Cosmetics.WallpaperPath=value);
            foreach(var g in d.Games)for(int i=0;i<(g.Saves??new List<GameSaveEntry>()).Count;i++){var save=g.Saves[i];add("linked:"+g.Id+":"+i,save.Path,value=>save.Path=value);}
            foreach(var snap in d.SaveSnapshots??new List<SaveSnapshot>()){add("snapshot:"+snap.Id,snap.Path,value=>snap.Path=value);add("snapshot-source:"+snap.Id,snap.Source,value=>snap.Source=value);}
            foreach(var shot in d.GameScreenshots??new List<GameScreenshot>())add("screenshot:"+shot.Id,shot.Path,value=>shot.Path=value);
            foreach(var emulator in d.Emulators){add("firmware:"+emulator.Id,emulator.FirmwareFolder,value=>emulator.FirmwareFolder=value);add("screenshots:"+emulator.Id,emulator.ScreenshotFolder,value=>emulator.ScreenshotFolder=value);add("logs:"+emulator.Id,emulator.LogFolder,value=>emulator.LogFolder=value);add("icon:"+emulator.Id,emulator.IconPath,value=>emulator.IconPath=value);add("banner:"+emulator.Id,emulator.BannerPath,value=>emulator.BannerPath=value);}
            foreach(var converter in d.Converters??new List<GameConverter>())add("converter:"+converter.Id,converter.Program,value=>converter.Program=value);
            return refs;
        }

		private static Dictionary<string, string> PathFields(LibraryData d)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
            foreach(var reference in ExtraPaths(d))dictionary[reference.Item1]=reference.Item2;
			foreach (GameEntry game in d.Games)
			{
				dictionary["game:" + game.Id] = game.Path;
				dictionary["art:" + game.Id] = game.ArtworkPath;
				dictionary["manual:" + game.Id] = game.ManualPath;
				if (game.Extras != null)
				{
					dictionary["work:" + game.Id] = game.Extras.WorkingDirectory;
				}
				for (int i = 0; i < (game.Discs ?? new List<string>()).Count; i++)
				{
					dictionary["disc:" + game.Id + ":" + i] = game.Discs[i];
				}
			}
			foreach (EmulatorProfile emulator in d.Emulators)
			{
				dictionary["exe:" + emulator.Id] = emulator.Executable;
				dictionary["scan:" + emulator.Id] = emulator.ScanFolder;
				dictionary["save:" + emulator.Id] = emulator.SaveFolder;
				dictionary["ingame:" + emulator.Id] = emulator.InGameSaveFolder;
				dictionary["state:" + emulator.Id] = emulator.SaveStateFolder;
				dictionary["config:" + emulator.Id] = emulator.ConfigFolder;
				foreach (EmulatorBuild item in emulator.Builds ?? new List<EmulatorBuild>())
				{
					dictionary["build:" + emulator.Id + ":" + item.Id] = item.Executable;
				}
			}
			return dictionary;
		}

		public static List<string> Rebase(LibraryData d, string root, bool apply)
		{
			Dictionary<string, string> dictionary = PathFields(d);
			List<string> list = new List<string>();
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			foreach (PathBinding path in Ensure(d).Paths)
			{
				if (path != null && !string.IsNullOrEmpty(path.Key) && dictionary.ContainsKey(path.Key))
				{
					if(path.Original!=null&&!string.Equals(path.Original,dictionary[path.Key],StringComparison.OrdinalIgnoreCase))throw new IOException("A recorded path has changed since binding: "+path.Key+". Record the current root again before relocation.");
                    string text = Resolve(root, path.Relative);
					dictionary2[path.Key] = text;
					list.Add(path.Key + "\r\n  " + dictionary[path.Key] + "\r\n  → " + text + ((!File.Exists(text) && !Directory.Exists(text)) ? " (unavailable)" : ""));
				}
			}
			if (!apply)
			{
				return list;
			}
			foreach (GameEntry game in d.Games)
			{
				if (dictionary2.ContainsKey("game:" + game.Id))
				{
					game.Path = dictionary2["game:" + game.Id];
				}
				if (dictionary2.ContainsKey("art:" + game.Id))
				{
					game.ArtworkPath = dictionary2["art:" + game.Id];
				}
				if (dictionary2.ContainsKey("manual:" + game.Id))
				{
					game.ManualPath = dictionary2["manual:" + game.Id];
				}
				if (dictionary2.ContainsKey("work:" + game.Id))
				{
					Extra(game).WorkingDirectory = dictionary2["work:" + game.Id];
				}
				for (int i = 0; i < (game.Discs ?? new List<string>()).Count; i++)
				{
					if (dictionary2.ContainsKey("disc:" + game.Id + ":" + i))
					{
						game.Discs[i] = dictionary2["disc:" + game.Id + ":" + i];
					}
				}
				game.LastDiscPath = null;
			}
			foreach (EmulatorProfile emulator in d.Emulators)
			{
				if (dictionary2.ContainsKey("exe:" + emulator.Id))
				{
					emulator.Executable = dictionary2["exe:" + emulator.Id];
				}
				if (dictionary2.ContainsKey("scan:" + emulator.Id))
				{
					emulator.ScanFolder = dictionary2["scan:" + emulator.Id];
				}
				if (dictionary2.ContainsKey("save:" + emulator.Id))
				{
					emulator.SaveFolder = dictionary2["save:" + emulator.Id];
				}
				if (dictionary2.ContainsKey("ingame:" + emulator.Id))
				{
					emulator.InGameSaveFolder = dictionary2["ingame:" + emulator.Id];
				}
				if (dictionary2.ContainsKey("state:" + emulator.Id))
				{
					emulator.SaveStateFolder = dictionary2["state:" + emulator.Id];
				}
				if (dictionary2.ContainsKey("config:" + emulator.Id))
				{
					emulator.ConfigFolder = dictionary2["config:" + emulator.Id];
				}
				foreach (EmulatorBuild item in emulator.Builds ?? new List<EmulatorBuild>())
				{
					if (dictionary2.ContainsKey("build:" + emulator.Id + ":" + item.Id))
					{
						item.Executable = dictionary2["build:" + emulator.Id + ":" + item.Id];
					}
				}
			}
            foreach(var reference in ExtraPaths(d)){string value;if(dictionary2.TryGetValue(reference.Item1,out value))reference.Item3(value);}
            var current=PathFields(d);foreach(var binding in Ensure(d).Paths)if(current.ContainsKey(binding.Key))binding.Original=current[binding.Key];
			return list;
		}

		public static void Portable(IWin32Window owner, LibraryData d)
		{
			NextDialog f = new NextDialog("Portable collection paths", 900, 600);
			try
			{
				f.Body.Controls.Add(new TextBox
				{
					Dock = DockStyle.Fill,
					ReadOnly = true,
					Multiline = true,
					Text = "First record relative paths under the current collection folder. After moving that folder yourself, choose its new location and review path updates.\r\n\r\nPaths outside the recorded folder remain unchanged. Games, original saves and backup catalogs are never moved. Linked saves and snapshots under the recorded root are included; outside-root paths stay unchanged. Emulator-internal paths in configuration files and literal arguments require separate review. Recorded bindings: " + Ensure(d).Paths.Count
				});
				f.Action("Record current root", delegate
				{
					using (FolderBrowserDialog folderBrowserDialog2 = new FolderBrowserDialog
					{
						Description = "Choose the common parent of your games and emulator folders"
					})
					{
						if (folderBrowserDialog2.ShowDialog(f) == DialogResult.OK)
						{
							Idle(d);
							List<PathBinding> list2 = Bind(d, folderBrowserDialog2.SelectedPath);
							if (Polish.Review(f, "Record relative paths", string.Join("\r\n", list2.Select((PathBinding p) => p.Key + " → " + p.Relative))))
							{
								Ensure(d).Paths = list2;
								Store.Save(d);
								f.Close();
							}
						}
					}
				});
				f.Action("Rebase to new root", delegate
				{
					using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
					{
						Description = "Choose the collection's new location"
					})
					{
						if (folderBrowserDialog.ShowDialog(f) == DialogResult.OK)
						{
							Idle(d);
							List<string> list = Rebase(d, folderBrowserDialog.SelectedPath, false);
							if (list.Count == 0)
							{
								throw new IOException("Record current root before moving your collection.");
							}
							if (Polish.Review(f, "Review relocated paths", string.Join("\r\n\r\n", list)))
							{
								Idle(d);
								Store.CreateRestorePoint(d, "Before path rebase");
								Rebase(d, folderBrowserDialog.SelectedPath, true);
								Record(d, "Rebased collection paths");
								Store.Save(d);
								f.Close();
							}
						}
					}
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static Dictionary<string, long> Storage(LibraryData d, CancellationToken token)
		{
			Dictionary<string, IEnumerable<string>> dictionary = new Dictionary<string, IEnumerable<string>>();
			dictionary.Add("Games", d.Games.Select((GameEntry g) => g.Path).Concat(d.Games.SelectMany((GameEntry g) => g.Discs ?? new List<string>())));
			dictionary.Add("Artwork", d.Games.Select((GameEntry g) => g.ArtworkPath));
			dictionary.Add("Linked saves", d.Games.SelectMany((GameEntry g) => (g.Saves ?? new List<GameSaveEntry>()).Select((GameSaveEntry s) => s.Path)));
			dictionary.Add("Save snapshots", d.SaveSnapshots.Select((SaveSnapshot s) => s.Path));
			Dictionary<string, IEnumerable<string>> dictionary2 = dictionary;
			Dictionary<string, long> dictionary3 = new Dictionary<string, long>();
			foreach (KeyValuePair<string, IEnumerable<string>> item in dictionary2)
			{
				long num = 0L;
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string item2 in item.Value.Where((string p) => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
				{
					token.ThrowIfCancellationRequested();
					try
					{
						List<string> list2;
						if (!Directory.Exists(item2))
						{
							List<string> list = new List<string>();
							list.Add(item2);
							list2 = list;
						}
						else
						{
							list2 = SafeFiles.Tree(item2, token);
						}
						List<string> list3 = list2;
						foreach (string item3 in list3)
						{
							token.ThrowIfCancellationRequested();
							if (hashSet.Add(Path.GetFullPath(item3)) && File.Exists(item3))
							{
								num += new FileInfo(item3).Length;
							}
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}
				dictionary3[item.Key] = num;
			}
			return dictionary3;
		}

		public static void StorageScreen(IWin32Window owner, LibraryData d)
		{
			Dictionary<string, long> dictionary = BackgroundWork<Dictionary<string, long>>.Run(owner, "Measure Library storage", (CancellationToken token, Action<string> progress) => Storage(d, token));
			if (dictionary == null)
			{
				return;
			}
			NextDialog f = new NextDialog("Storage overview");
			try
			{
				f.Body.Controls.Add(new TextBox
				{
					Dock = DockStyle.Fill,
					ReadOnly = true,
					Multiline = true,
					Text = string.Join("\r\n", dictionary.Select((KeyValuePair<string, long> p) => p.Key + ": " + ExperienceUi.Bytes(p.Value))) + "\r\n\r\nCounts configured local files, not entire emulator installations. Categories may overlap. Unreadable files are excluded. Cleanup uses the existing reviewed backup retention controls; original games and saves are never automatically deleted."
				});
				f.Action("Backup verification and retention", delegate
				{
					ExperienceTools.Backups(f, d);
				});
				f.Action("Backup schedule", delegate
				{
					NextTools.BackupPlanner(f, d);
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static string RestorePreview(SaveSnapshot s, CancellationToken token)
		{
			SaveHistory.Verify(s, token);
			Dictionary<string, string> incoming = NextData.SnapshotFiles(s, token);
			SaveSnapshot saveSnapshot = new SaveSnapshot();
			saveSnapshot.Path = s.Source;
			saveSnapshot.Source = s.Source;
			saveSnapshot.IsFolder = s.IsFolder;
			SaveSnapshot snapshot = saveSnapshot;
			Dictionary<string, string> dictionary = NextData.SnapshotFiles(snapshot, token);
			StringBuilder stringBuilder = new StringBuilder("Restore destination: " + s.Source + "\r\nExisting contents will be backed up. Emulator must be closed.\r\n\r\n");
			foreach (KeyValuePair<string, string> item in incoming)
			{
				string value;
				string text = ((!dictionary.TryGetValue(item.Key, out value)) ? "ADD" : ((value == item.Value) ? "UNCHANGED" : "REPLACE"));
				stringBuilder.AppendLine(text + " " + item.Key);
			}
			foreach (string item2 in dictionary.Keys.Where((string key) => !incoming.ContainsKey(key)))
			{
				stringBuilder.AppendLine("REMOVE FROM LIVE (retained in rollback): " + item2);
			}
			return stringBuilder.ToString();
		}

		public static List<string> CloudConflicts(string root, CancellationToken token)
		{
			return SafeFiles.Tree(root, token).Where(delegate(string p)
			{
				string text = Path.GetFileName(p).ToLowerInvariant();
				return text.Contains("conflicted copy") || text.Contains("conflict") || text.Contains("sync-conflict");
			}).Take(500)
				.ToList();
		}

		public static void Conflicts(IWin32Window owner)
		{
			FolderBrowserDialog folder = new FolderBrowserDialog
			{
				Description = "Choose your cloud client's local save/backup folder"
			};
			try
			{
				if (folder.ShowDialog(owner) != DialogResult.OK)
				{
					return;
				}
				List<string> list = BackgroundWork<List<string>>.Run(owner, "Find cloud conflict copies", (CancellationToken token, Action<string> progress) => CloudConflicts(folder.SelectedPath, token));
				if (list != null)
				{
					using (ResultsDialog resultsDialog = new ResultsDialog("Cloud conflict review", (list.Count == 0) ? new string[1] { "No filenames indicating conflict copies were found. This does not verify cloud upload completion or detect every conflict." } : list.ToArray()))
					{
						resultsDialog.ShowDialog(owner);
						return;
					}
				}
			}
			finally
			{
				if (folder != null)
				{
					((IDisposable)folder).Dispose();
				}
			}
		}

		public static void ValidateAdapter(EmulatorAdapter a)
		{
			if (a == null || string.IsNullOrWhiteSpace(a.Name) || a.Name.Length > 120)
			{
				throw new IOException("Adapter needs a name of up to 120 characters.");
			}
			if (a.Extensions == null || a.Extensions.Count == 0 || a.Extensions.Count > 100 || a.Extensions.Any((string e) => string.IsNullOrWhiteSpace(e) || e.Length > 20 || e.Any((char c) => !char.IsLetterOrDigit(c) && c != '.')))
			{
				throw new IOException("Adapter extensions must be simple file extensions.");
			}
			string text = (a.Arguments ?? "").Replace("{game}", "");
			if (text.Contains("{") || text.Contains("}"))
			{
				throw new IOException("Adapters support only {game} in launch arguments.");
			}
			if (!string.IsNullOrWhiteSpace(a.Website) && !Https(a.Website))
			{
				throw new IOException("Adapter website must use HTTPS.");
			}
			if (!string.IsNullOrWhiteSpace(a.Repository) && !Regex.IsMatch(a.Repository, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
			{
				throw new IOException("Repository must be owner/repository.");
			}
		}

		public static EmulatorProfile FromAdapter(EmulatorAdapter a, string executable)
		{
			ValidateAdapter(a);
			if (!File.Exists(executable) || Path.GetExtension(executable).ToLowerInvariant() != ".exe")
			{
				throw new IOException("Choose an installed emulator executable.");
			}
			EmulatorProfile emulatorProfile = new EmulatorProfile();
			emulatorProfile.Id = Guid.NewGuid().ToString("N");
			emulatorProfile.Name = a.Name;
			emulatorProfile.Platform = a.Platform;
			emulatorProfile.Preset = "Custom";
			emulatorProfile.Executable = executable;
			emulatorProfile.Arguments = a.Arguments;
			emulatorProfile.Extensions = a.Extensions.Select((string e) => e.TrimStart('.').ToLowerInvariant()).ToList();
			emulatorProfile.WebsiteUrl = a.Website;
			emulatorProfile.GitHubRepository = a.Repository;
			emulatorProfile.AddedAt = DateTime.UtcNow.ToString("o");
			emulatorProfile.Builds = new List<EmulatorBuild>();
			emulatorProfile.LaunchProfiles = new List<LaunchProfile>();
			return emulatorProfile;
		}

		public static void Adapters(IWin32Window owner, LibraryData d)
		{
			NextDialog f = new NextDialog("Emulator adapters", 860, 620);
			try
			{
				ListBox list = new ListBox
				{
					Dock = DockStyle.Fill,
					DisplayMember = "Name",
					AccessibleName = "Installed adapter definitions"
				};
				Action refresh = delegate
				{
					list.DataSource = null;
					list.DataSource = Ensure(d).Adapters.ToList();
				};
				f.Body.Controls.Add(list);
				f.Body.Controls.Add(new Label
				{
					Dock = DockStyle.Top,
					Height = 70,
					Text = "Optional JSON adapters define launch formats and setup links. They do not load executable plugins or download emulator programs. Use an adapter with an emulator you have installed."
				});
				f.Action("Import JSON adapter", delegate
				{
					using (OpenFileDialog openFileDialog2 = new OpenFileDialog
					{
						Filter = "Adapter JSON|*.json"
					})
					{
						if (openFileDialog2.ShowDialog(f) == DialogResult.OK)
						{
							if (new FileInfo(openFileDialog2.FileName).Length > 1048576)
							{
								throw new IOException("Adapter exceeds 1 MB.");
							}
							EmulatorAdapter a = Json.Deserialize<EmulatorAdapter>(File.ReadAllText(openFileDialog2.FileName));
							ValidateAdapter(a);
							if (Polish.Review(f, "Review adapter", Json.Serialize(a)))
							{
								if (Ensure(d).Adapters.Any((EmulatorAdapter x) => x.Name == a.Name))
								{
									throw new IOException("An adapter with this name already exists.");
								}
								Ensure(d).Adapters.Add(a);
								Store.Save(d);
								refresh();
							}
						}
					}
				});
				f.Action("Export template", delegate
				{
					using (SaveFileDialog saveFileDialog = new SaveFileDialog
					{
						Filter = "Adapter JSON|*.json",
						FileName = "emulator-adapter.json"
					})
					{
						if (saveFileDialog.ShowDialog(f) == DialogResult.OK)
						{
							File.WriteAllText(saveFileDialog.FileName, Json.Serialize((list.SelectedItem as EmulatorAdapter) ?? new EmulatorAdapter
							{
								Name = "My emulator",
								Platform = "My platform",
								Extensions = new List<string> { "rom" },
								Arguments = "{game}",
								Website = "https://example.com"
							}));
						}
					}
				});
				f.Action("Add emulator from adapter", delegate
				{
					EmulatorAdapter emulatorAdapter2 = list.SelectedItem as EmulatorAdapter;
					if (emulatorAdapter2 == null)
					{
						throw new IOException("Select an adapter first.");
					}
					using (OpenFileDialog openFileDialog = new OpenFileDialog
					{
						Filter = "Installed emulator|*.exe"
					})
					{
						if (openFileDialog.ShowDialog(f) == DialogResult.OK)
						{
							EmulatorProfile emulatorProfile = FromAdapter(emulatorAdapter2, openFileDialog.FileName);
							if (Polish.Review(f, "Add emulator", emulatorProfile.Name + "\r\n" + emulatorProfile.Executable + "\r\nArguments: " + emulatorProfile.Arguments))
							{
								d.Emulators.Add(emulatorProfile);
								Store.Save(d);
							}
						}
					}
				});
				f.Action("Remove definition", delegate
				{
					EmulatorAdapter emulatorAdapter = list.SelectedItem as EmulatorAdapter;
					if (emulatorAdapter != null && Polish.Review(f, "Remove adapter", emulatorAdapter.Name + "\r\nExisting emulator profiles are retained."))
					{
						Ensure(d).Adapters.Remove(emulatorAdapter);
						Store.Save(d);
						refresh();
					}
				});
				f.Action("Close", f.Close);
				refresh();
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static void Inventory(IWin32Window owner, LibraryData d)
		{
			NextDialog f = new NextDialog("Emulator versions and updates", 940, 650);
			try
			{
				ListView list = new ListView
				{
					Dock = DockStyle.Fill,
					View = View.Details,
					FullRowSelect = true,
					HideSelection = false
				};
				list.Columns.Add("Emulator / build", 240);
				list.Columns.Add("Version", 150);
				list.Columns.Add("Executable", 530);
				foreach (EmulatorProfile emulator in d.Emulators)
				{
					AddInventoryRow(list, emulator, null);
					foreach (EmulatorBuild item in emulator.Builds ?? new List<EmulatorBuild>())
					{
						AddInventoryRow(list, emulator, item);
					}
				}
				f.Body.Controls.Add(list);var watch=new CheckBox{Dock=DockStyle.Top,Height=48,Text="Check published emulator releases daily while FishBowl is open (optional)",Checked=Ensure(d).WatchReleases};f.Body.Controls.Add(watch);watch.CheckedChanged+=(a,b)=>{Ensure(d).WatchReleases=watch.Checked;Store.Save(d);};
				f.Action("Check release", delegate
				{
					EmulatorProfile e = SelectedEmulator(list);
					UpdateResult updateResult = BackgroundWork<UpdateResult>.Run(f, "Check emulator release", (CancellationToken token, Action<string> progress) => EmulatorUpdates.Check(e, (string url) => Encoding.UTF8.GetString(Fetch(url, 2097152L, token))));
					if (updateResult != null)
					{
						UserTools.Report(f, "Emulator release", updateResult.Status + "\r\nInstalled: " + updateResult.Installed + "\r\nLatest: " + updateResult.Latest + "\r\n" + updateResult.Url + "\r\n\r\n" + updateResult.Notes);
					}
				});
				f.Action("Official releases", delegate
				{
					EmulatorProfile emulatorProfile = SelectedEmulator(list);
					EmulatorReference emulatorReference = EmulatorReference.For(emulatorProfile);
					OpenLink(string.IsNullOrWhiteSpace(emulatorProfile.ReleasesUrl) ? emulatorReference.Releases : emulatorProfile.ReleasesUrl);
				});
				f.Action("Use selected build", delegate
				{
					if (list.SelectedItems.Count == 0)
					{
						throw new IOException("Select a build first.");
					}
					Tuple<EmulatorProfile, EmulatorBuild> tuple = (Tuple<EmulatorProfile, EmulatorBuild>)list.SelectedItems[0].Tag;
					if (tuple.Item2 == null)
					{
						throw new IOException("Choose a registered build. Add alternate versions in the emulator editor.");
					}
					Idle(d);
					if (Polish.Review(f, "Activate emulator build", tuple.Item1.Executable + "\r\n→ " + tuple.Item2.Executable + "\r\nThe current executable is retained in the registered build list for rollback."))
					{
						BuildRegistry.Activate(tuple.Item1, tuple.Item2);
						Record(d, "Changed emulator build: " + tuple.Item1.Name);
						Store.Save(d);
						f.Close();
					}
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		private static void AddInventoryRow(ListView list, EmulatorProfile e, EmulatorBuild b)
		{
			string text = ((b == null) ? e.Executable : b.Executable);
			string text2 = ((b == null) ? e.ManualVersion : b.ManualVersion);
			try
			{
				if (File.Exists(text))
				{
					string fileVersion = FileVersionInfo.GetVersionInfo(text).FileVersion;
					if (!string.IsNullOrWhiteSpace(fileVersion))
					{
						text2 = fileVersion;
					}
				}
				else
				{
					text2 = "Missing";
				}
			}
			catch (Exception ex)
			{
				text2 = ex.Message;
			}
			list.Items.Add(new ListViewItem(new string[3]
			{
				e.Name + ((b == null) ? " (active)" : (" / " + b.Label)),
				text2 ?? "Unknown",
				text
			})
			{
				Tag = Tuple.Create(e, b)
			});
		}

		private static EmulatorProfile SelectedEmulator(ListView list)
		{
			if (list.SelectedItems.Count == 0)
			{
				throw new IOException("Select an emulator or build.");
			}
			return ((Tuple<EmulatorProfile, EmulatorBuild>)list.SelectedItems[0].Tag).Item1;
		}

		public static bool Https(string value)
		{
			Uri result;
			return Uri.TryCreate(value, UriKind.Absolute, out result) && result.Scheme == "https" && string.IsNullOrEmpty(result.UserInfo);
		}

		public static void OpenLink(string value)
		{
			if (!Https(value))
			{
				throw new IOException("Enter an HTTPS web address.");
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(value);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}

		public static byte[] Fetch(string url, long limit, CancellationToken token)
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			if(limit<=0)throw new ArgumentOutOfRangeException("limit");
			token.ThrowIfCancellationRequested();
			if (!Https(url))
			{
				throw new IOException("Network requests require HTTPS.");
			}
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
			httpWebRequest.UserAgent = "FishBowl/1.8 (desktop library; user-requested lookup)";
			httpWebRequest.Timeout = 20000;
			httpWebRequest.ReadWriteTimeout = 20000;
			httpWebRequest.AllowAutoRedirect = false;
			try { using (token.Register(httpWebRequest.Abort))
			{
				using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
				{
					if (httpWebResponse.ContentLength > limit)
					{
						throw new IOException("Download exceeds size limit.");
					}
					using (Stream stream = httpWebResponse.GetResponseStream())
					{
						using (MemoryStream memoryStream = new MemoryStream())
						{
							byte[] array = new byte[16384];
							int num;
							while ((num = stream.Read(array, 0, array.Length)) > 0)
							{
								token.ThrowIfCancellationRequested();
								if (memoryStream.Length + num > limit)
								{
									throw new IOException("Download exceeds size limit.");
								}
								memoryStream.Write(array, 0, num);
							}
							return memoryStream.ToArray();
						}
					}
				}
			} } catch(WebException error) { if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw new IOException("The online service could not be reached. Try Open search website, use a local metadata catalog, or check your network connection. " + error.Message,error); }
		}

		public static string MetadataQuery(string title)
		{
			return "https://en.wikipedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=0&gsrlimit=8&gsrsearch=" + Uri.EscapeDataString(title + " video game") + "&prop=extracts%7Cinfo%7Cpageimages&inprop=url&exintro=1&explaintext=1&exsentences=4&piprop=thumbnail&pithumbsize=400";
		}

		public static List<MetadataMatch> ParseMetadata(string json)
		{
			Dictionary<string, object> dictionary = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
			List<MetadataMatch> list = new List<MetadataMatch>();
			if (dictionary == null || !dictionary.ContainsKey("query"))
			{
				return list;
			}
			Dictionary<string, object> dictionary2 = dictionary["query"] as Dictionary<string, object>;
			if (dictionary2 == null || !dictionary2.ContainsKey("pages"))
			{
				return list;
			}
			Dictionary<string, object> dictionary3 = dictionary2["pages"] as Dictionary<string, object>;
			if (dictionary3 == null)
			{
				return list;
			}
			foreach (object value in dictionary3.Values)
			{
				Dictionary<string, object> page = value as Dictionary<string, object>;
				if (page == null)
				{
					continue;
				}
				Func<string, string> func = (string key) => page.ContainsKey(key) ? Convert.ToString(page[key]) : "";
				string image = "";
				if (page.ContainsKey("thumbnail"))
				{
					Dictionary<string, object> dictionary4 = page["thumbnail"] as Dictionary<string, object>;
					if (dictionary4 != null && dictionary4.ContainsKey("source"))
					{
						image = Convert.ToString(dictionary4["source"]);
					}
				}
				list.Add(new MetadataMatch
				{
					Title = func("title"),
					Description = func("extract"),
					Url = func("fullurl"),
					Image = image
				});
			}
			return list.OrderBy((MetadataMatch m) => m.Title).ToList();
		}

		public static void Metadata(IWin32Window owner, LibraryData d, GameEntry g)
		{
			if (g == null)
			{
				throw new IOException("Select a game first.");
			}
			NextDialog f = new NextDialog("Online metadata and cover selection", 950, 700);
			try
			{
				TextBox search = new TextBox
				{
					Dock = DockStyle.Top,
					Text = g.Title,
					AccessibleName = "Wikipedia game lookup"
				};
				ListBox list = new ListBox
				{
					Dock = DockStyle.Left,
					Width = 275,
					AccessibleName = "Metadata matches"
				};
				TextBox details = new TextBox
				{
					Dock = DockStyle.Fill,
					ReadOnly = true,
					Multiline = true,
					ScrollBars = ScrollBars.Vertical
				};
				f.Body.Controls.Add(details);
				f.Body.Controls.Add(list);
				f.Body.Controls.Add(search);
				list.SelectedIndexChanged += delegate
				{
					MetadataMatch metadataMatch3 = list.SelectedItem as MetadataMatch;
					details.Text = ((metadataMatch3 == null) ? "" : (metadataMatch3.Title + "\r\n\r\n" + metadataMatch3.Description + "\r\n\r\nSource: " + metadataMatch3.Url + "\r\nThumbnail: " + (string.IsNullOrWhiteSpace(metadataMatch3.Image) ? "unavailable" : metadataMatch3.Image) + "\r\n\r\nWikipedia text has attribution/share-alike requirements when redistributed. Images have their own rights; review the source before sharing."));
				};
				details.Text = "Lookup runs only when you choose Search online. It sends the search text to English Wikipedia. Choose a match, review its source and apply only what you want. Existing developer, genre and year fields are retained.";
				f.Action("Search online", delegate
				{
					List<MetadataMatch> list2 = BackgroundWork<List<MetadataMatch>>.Run(f, "Find game metadata", (CancellationToken token, Action<string> progress) => ParseMetadata(Encoding.UTF8.GetString(Fetch(MetadataQuery(search.Text), 2097152L, token))));
					if (list2 != null)
					{
						list.DataSource = list2;
						if (list2.Count == 0)
						{
							details.Text = "No matches. Try another title or import a local metadata catalog.";
						}
					}
				});
				f.Action("Apply description", delegate
				{
					MetadataMatch metadataMatch2 = list.SelectedItem as MetadataMatch;
					if (metadataMatch2 == null)
					{
						throw new IOException("Choose a metadata match.");
					}
					if (Polish.Review(f, "Review description", g.Description + "\r\n→\r\n" + metadataMatch2.Description + "\r\nSource: " + metadataMatch2.Url))
					{
						g.Description = metadataMatch2.Description;
						Extra(g).MetadataSource = metadataMatch2.Url;
						Record(d, "Updated description: " + g.Title);
						Store.Save(d);
					}
				});
				f.Action("View source / image rights", delegate
				{
					MetadataMatch metadataMatch = list.SelectedItem as MetadataMatch;
					if (metadataMatch == null)
					{
						throw new IOException("Choose a match.");
					}
					OpenLink(metadataMatch.Url);
				});
				f.Action("Choose online cover", delegate
				{
					MetadataMatch i = list.SelectedItem as MetadataMatch;
					if (i == null || string.IsNullOrWhiteSpace(i.Image))
					{
						throw new IOException("This match has no image; choose a local cover instead.");
					}
					Uri uri = new Uri(i.Image);
					if (uri.Host != "upload.wikimedia.org")
					{
						throw new IOException("Only Wikimedia thumbnails are supported.");
					}
					byte[] array = BackgroundWork<byte[]>.Run(f, "Load cover preview", (CancellationToken token, Action<string> progress) => Fetch(i.Image, 5242880L, token));
					if (array == null)
					{
						return;
					}
					using (MemoryStream stream = new MemoryStream(array))
					{
						Image source = Image.FromStream(stream);
						try
						{
							if (source.Width > 4096 || source.Height > 4096)
							{
								throw new IOException("Image dimensions exceed the limit.");
							}
							NextDialog preview = new NextDialog("Review online cover", 600, 570);
							try
							{
								preview.Body.Controls.Add(new PictureBox
								{
									Dock = DockStyle.Fill,
									Image = source,
									SizeMode = PictureBoxSizeMode.Zoom
								});
								preview.Action("Use this cover", delegate
								{
									Directory.CreateDirectory(Path.Combine(Store.DataDirectory, "Artwork"));
									string text = Path.Combine(Store.DataDirectory, "Artwork", Guid.NewGuid().ToString("N") + ".png");
									using (Bitmap bitmap = new Bitmap(source))
									{
										bitmap.Save(text, ImageFormat.Png);
									}
									g.ArtworkPath = text;
									Extra(g).MetadataSource = i.Url;
									Store.Save(d);
									preview.Close();
								});
								preview.Action("Cancel", preview.Close);
								preview.ShowDialog(f);
							}
							finally
							{
								if (preview != null)
								{
									((IDisposable)preview).Dispose();
								}
							}
						}
						finally
						{
							if (source != null)
							{
								((IDisposable)source).Dispose();
							}
						}
					}
				});
				f.Action("Choose local cover", delegate
				{
					using (OpenFileDialog openFileDialog = new OpenFileDialog
					{
						Filter = "Cover image|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
					})
					{
						if (openFileDialog.ShowDialog(f) == DialogResult.OK)
						{
							using (Image image = Image.FromFile(openFileDialog.FileName))
							{
								if (image.Width > 16000 || image.Height > 16000)
								{
									throw new IOException("Image dimensions are too large.");
								}
							}
							g.ArtworkPath = openFileDialog.FileName;
							Store.Save(d);
						}
					}
				});
				f.Action("Local metadata catalog", delegate
				{
					ExperienceTools.Metadata(f, d, g);
				});
				f.Action("Open search website", delegate {OpenLink("https://en.wikipedia.org/w/index.php?search="+Uri.EscapeDataString(search.Text+" video game"));});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static Dictionary<string, string> ParseFields(string text)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text2 in array)
			{
				int num = text2.IndexOf('=');
				if (num <= 0)
				{
					throw new IOException("Use one Key = Value per line.");
				}
				string text3 = text2.Substring(0, num).Trim();
				string text4 = text2.Substring(num + 1).Trim();
				if (text3.Length == 0 || text3.Length > 80 || text4.Length > 2000 || dictionary.Count >= 100 || dictionary.ContainsKey(text3))
				{
					throw new IOException("Use unique field names (up to 80 characters) and values up to 2000 characters, at most 100 fields.");
				}
				dictionary.Add(text3, text4);
			}
			return dictionary;
		}

		public static void GameOptions(IWin32Window owner, LibraryData d, GameEntry g)
		{
			if (g == null)
			{
				throw new IOException("Select a game first.");
			}
			NextDialog f = new NextDialog("Game extensions — " + g.Title, 900, 690);
			try
			{
				FishBowlTabs fishBowlTabs = new FishBowlTabs();
				fishBowlTabs.Dock = DockStyle.Fill;
				fishBowlTabs.PreferredColumns = 2;
				FishBowlTabs fishBowlTabs2 = fishBowlTabs;
				TabPage tabPage = new TabPage("Launch and media");
				tabPage.AutoScroll = true;
				TabPage tabPage2 = tabPage;
				TabPage tabPage3 = new TabPage("Custom fields");
				tabPage3.AutoScroll = true;
				TabPage tabPage4 = tabPage3;
				fishBowlTabs2.TabPages.Add(tabPage2);
				fishBowlTabs2.TabPages.Add(tabPage4);
				f.Body.Controls.Add(fishBowlTabs2);
				TableLayoutPanel table = NextDialog.Fields(tabPage2);
				GameExtras extra = Extra(g);
				CheckBox native = new CheckBox
				{
					Text = "Launch this entry as a PC game / shortcut",
					Checked = extra.Native
				};
				TextBox work = new TextBox
				{
					Text = (extra.WorkingDirectory ?? "")
				};
				TextBox args = new TextBox
				{
					Text = (g.Arguments ?? "")
				};
				TextBox trailer = new TextBox
				{
					Text = (extra.TrailerUrl ?? "")
				};
				TextBox manual = new TextBox
				{
					Text = (g.ManualPath ?? "")
				};
				NextDialog.Field(table, "Launch type", native);
				NextDialog.Field(table, "Working folder (PC only)", work);
				NextDialog.Field(table, "Extra launch arguments", args);
				NextDialog.Field(table, "Trailer HTTPS address", trailer);
				NextDialog.Field(table, "Local manual path", manual);
				NextDialog.Field(table, "", new Label
				{
					AutoSize = true,
					Text = "Graphics presets use launch profiles with flags supported by your emulator. FishBowl does not guess emulator-specific settings."
				}, 70);
				TextBox fields = new TextBox
				{
					Dock = DockStyle.Fill,
					Multiline = true,
					ScrollBars = ScrollBars.Vertical,
					Text = string.Join("\r\n", extra.Fields.Select((KeyValuePair<string, string> p) => p.Key + " = " + p.Value)),
					AccessibleName = "Custom metadata fields, one Key = Value per line"
				};
				tabPage4.Controls.Add(fields);
				f.Action("Save", delegate
				{
					if (!string.IsNullOrWhiteSpace(trailer.Text) && !Https(trailer.Text))
					{
						throw new IOException("Trailer must use HTTPS.");
					}
					Dictionary<string, string> fields2 = ParseFields(fields.Text);
					if (native.Checked)
					{
						GameEntry gameEntry = UserTools.Copy(g);
						gameEntry.Arguments = args.Text;
						Extra(gameEntry).Native = true;
						gameEntry.Extras.WorkingDirectory = work.Text.Trim();
						NativeArguments(gameEntry);
					}
					g.Arguments = args.Text;
					extra.Native = native.Checked;
					extra.WorkingDirectory = work.Text.Trim();
					extra.TrailerUrl = trailer.Text.Trim();
					extra.Fields = fields2;
					g.ManualPath = manual.Text.Trim();
					Record(d, "Changed game options: " + g.Title);
					Store.Save(d);
					f.Close();
				});
				f.Action("Focus view", ()=>Immersion.Show(f,d,g));
                f.Action("Build / launch preset", delegate
				{
					NextTools.PreferredBuild(f, d, g);
				});
				f.Action("Launch readiness", delegate
				{
					UserTools.Troubleshoot(f, d, g, Readiness(d, g));
				});
				f.Action("Open trailer", delegate
				{
					OpenLink(trailer.Text.Trim());
				});
				f.Action("Open manual", delegate
				{
					if (!File.Exists(manual.Text))
					{
						throw new IOException("Choose an existing local manual.");
					}
					string value = Path.GetExtension(manual.Text).ToLowerInvariant();
					if (!new string[5] { ".pdf", ".txt", ".md", ".html", ".htm" }.Contains(value))
					{
						throw new IOException("Manuals support PDF, text or HTML documents.");
					}
					Process.Start(new ProcessStartInfo(manual.Text)
					{
						UseShellExecute = true
					});
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static string Fingerprint(GameEntry g)
		{
			using (SHA256 sHA = SHA256.Create())
			{
				return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(Json.Serialize(g)))).Replace("-", "");
			}
		}

		public static TransferPackage ExportPackage(LibraryData d)
		{
			UserTools.SaveActive(d);
			TransferPackage transferPackage = new TransferPackage();
			transferPackage.Schema = 1;
			transferPackage.At = DateTime.UtcNow.ToString("o");
			transferPackage.Profile = UserTools.Copy(UserTools.Ensure(d).Users.First((BowlUser u) => u.Id == d.UserTools.ActiveId));
			transferPackage.Games = UserTools.Copy(d.Games);
			return transferPackage;
		}

		public static List<TransferChange> TransferPlan(LibraryData d, TransferPackage p)
		{
			if (p == null || p.Schema != 1 || p.Games == null)
			{
				throw new IOException("Unsupported transfer package.");
			}
			if (p.Games.Count > 100000 || p.Games.Any((GameEntry g) => g == null || string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Title)) || (from g in p.Games
				group g by g.Id).Any((IGrouping<string, GameEntry> g) => g.Count() > 1))
			{
				throw new IOException("Transfer contains invalid or duplicate game records.");
			}
			List<TransferChange> list = new List<TransferChange>();
			Dictionary<string, GameEntry> dictionary = d.Games.ToDictionary((GameEntry g) => g.Id);
			var knownPaths=new HashSet<string>(d.Games.Where(g=>!string.IsNullOrWhiteSpace(g.Path)).Select(g=>g.Path),StringComparer.OrdinalIgnoreCase);
			foreach (GameEntry incoming in p.Games)
			{
				GameEntry value;
				dictionary.TryGetValue(incoming.Id, out value);
				if (value == null)
				{
					if (!string.IsNullOrWhiteSpace(incoming.Path) && !knownPaths.Add(incoming.Path))
					{
						list.Add(new TransferChange
						{
							Incoming = incoming,
							Status = "Duplicate path — keep local"
						});
					}
					else
					{
						list.Add(new TransferChange
						{
							Incoming = incoming,
							Status = "Add"
						});
					}
					continue;
				}
				string text = Fingerprint(incoming);
				string text2 = Fingerprint(value);
				string value2;
				Ensure(d).SyncHashes.TryGetValue(incoming.Id, out value2);
				if (!(text == text2))
				{
					list.Add(new TransferChange
					{
						Incoming = incoming,
						Existing = value,
						ReviewedHash = text2,
						Status = ((value2 != null && text2 == value2) ? "Update" : "Conflict — choose explicitly")
					});
				}
			}
			return list;
		}

		public static void ApplyTransfer(LibraryData d, IEnumerable<TransferChange> chosen)
		{
			Idle(d);
			List<TransferChange> list = chosen.ToList();
			var newPaths=new HashSet<string>(d.Games.Where(g=>!string.IsNullOrWhiteSpace(g.Path)).Select(g=>g.Path),StringComparer.OrdinalIgnoreCase);
			foreach(var change in list.Where(c=>c.Existing==null))if(!string.IsNullOrWhiteSpace(change.Incoming.Path)&&!newPaths.Add(change.Incoming.Path))throw new IOException("A new entry's path is already present. Start a fresh synchronization preview.");
			if (list.Any((TransferChange c) => c.Status.StartsWith("Duplicate")))
			{
				throw new IOException("Duplicate paths cannot be imported as new entries.");
			}
			foreach (TransferChange c2 in list)
			{
				List<GameEntry> games = d.Games;
				Func<GameEntry, bool> predicate = (GameEntry g) => g.Id == c2.Incoming.Id;
				GameEntry gameEntry = games.FirstOrDefault(predicate);
				if ((c2.Existing == null && gameEntry != null) || (c2.Existing != null && (gameEntry == null || Fingerprint(gameEntry) != c2.ReviewedHash)))
				{
					throw new IOException("The Library changed during review; start another preview.");
				}
			}
			foreach (TransferChange item in list)
			{
				GameEntry gameEntry2 = UserTools.Copy(item.Incoming);
				if (item.Existing == null)
				{
					d.Games.Add(gameEntry2);
				}
				else
				{
					gameEntry2.Arguments = item.Existing.Arguments;
					gameEntry2.LaunchProfileName = item.Existing.LaunchProfileName;
					if(gameEntry2.Extras==null)gameEntry2.Extras=UserTools.Copy(item.Existing.Extras);
					else {gameEntry2.Extras.Native=Native(item.Existing);gameEntry2.Extras.WorkingDirectory=item.Existing.Extras==null?null:item.Existing.Extras.WorkingDirectory;}
					gameEntry2.ArtworkPath = (File.Exists(gameEntry2.ArtworkPath) ? gameEntry2.ArtworkPath : item.Existing.ArtworkPath);
					gameEntry2.ManualPath = item.Existing.ManualPath;
					gameEntry2.Path = item.Existing.Path;
					gameEntry2.EmulatorId = item.Existing.EmulatorId;
					gameEntry2.PreferredEmulatorId = item.Existing.PreferredEmulatorId;
					gameEntry2.PreferredBuildId = item.Existing.PreferredBuildId;
					gameEntry2.Saves = item.Existing.Saves;
					gameEntry2.Discs = item.Existing.Discs;
					gameEntry2.LastDiscPath = item.Existing.LastDiscPath;
					gameEntry2.TotalPlaySeconds = item.Existing.TotalPlaySeconds;
					gameEntry2.LaunchCount = item.Existing.LaunchCount;
					gameEntry2.LastLaunched = item.Existing.LastLaunched;
					d.Games[d.Games.IndexOf(item.Existing)] = gameEntry2;
				}
				Ensure(d).SyncHashes[gameEntry2.Id] = Fingerprint(gameEntry2);
			}
		}

		public static void Transfer(IWin32Window owner, LibraryData d)
		{
			NextDialog f = new NextDialog("Profile transfer and reviewed sync", 900, 600);
			try
			{
				f.Body.Controls.Add(new TextBox
				{
					ReadOnly = true,
					Multiline = true,
					Dock = DockStyle.Fill,
					Text = "Use a transfer JSON file on another computer or in your cloud client's local folder. Files include game metadata, paths and the active personal profile, but no game bytes, emulator executables or save contents.\r\n\r\nImport previews every change. Conflicts default to keeping local data. Existing game paths, emulator assignments, linked saves and recorded time remain local. New entries may need path repair and emulator assignment. Profiles can be imported separately. Cloud upload/download completion is managed by your cloud client; FishBowl does not silently merge files."
				});
				f.Action("Export profile and Library", delegate
				{
					using (SaveFileDialog saveFileDialog = new SaveFileDialog
					{
						Filter = "FishBowl transfer JSON|*.fishbowl-transfer.json",
						FileName = "FishBowl-transfer.fishbowl-transfer.json"
					})
					{
						if (saveFileDialog.ShowDialog(f) == DialogResult.OK)
						{
							TransferPackage value = ExportPackage(d);
							File.WriteAllText(saveFileDialog.FileName, Json.Serialize(value));
							foreach (GameEntry game in d.Games)
							{
								Ensure(d).SyncHashes[game.Id] = Fingerprint(game);
							}
							Store.Save(d);
						}
					}
				});
				f.Action("Preview Library sync", delegate
				{
					ImportTransfer(f, d, false);
				});
				f.Action("Import personal profile", delegate
				{
					ImportTransfer(f, d, true);
				});
				f.Action("Check cloud conflicts", delegate
				{
					Conflicts(f);
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		private static void ImportTransfer(IWin32Window owner, LibraryData d, bool profileOnly)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "FishBowl transfer JSON|*.fishbowl-transfer.json;*.json";
			using (OpenFileDialog openFileDialog2 = openFileDialog)
			{
				if (openFileDialog2.ShowDialog(owner) != DialogResult.OK)
				{
					return;
				}
				if (new FileInfo(openFileDialog2.FileName).Length > 16777216)
				{
					throw new IOException("Transfer exceeds 16 MB.");
				}
				TransferPackage transferPackage = Json.Deserialize<TransferPackage>(File.ReadAllText(openFileDialog2.FileName));
				List<TransferChange> list2 = TransferPlan(d, transferPackage);
				if (profileOnly)
				{
					if (transferPackage.Profile == null)
					{
						throw new IOException("Package has no personal profile.");
					}
					if (Polish.Review(owner, "Import personal profile", transferPackage.Profile.Name + "\r\nPersonal progress for matching game IDs will be imported as a new profile. Emulator save routes and queues remain subject to local availability."))
					{
						Idle(d);
						BowlUser bowlUser = UserTools.Copy(transferPackage.Profile);
						bowlUser.Id = Guid.NewGuid().ToString("N");
						bowlUser.Name = (bowlUser.Name ?? "Imported") + " (imported)";
						UserTools.Ensure(d).Users.Add(bowlUser);
						Store.Save(d);
					}
					return;
				}
				NextDialog f = new NextDialog("Review Library synchronization", 950, 680);
				try
				{
					ListView list = new ListView
					{
						Dock = DockStyle.Fill,
						View = View.Details,
						CheckBoxes = true,
						FullRowSelect = true,
						HideSelection = false
					};
					list.Columns.Add("Change", 225);
					list.Columns.Add("Game", 230);
					list.Columns.Add("Incoming path", 420);
					foreach (TransferChange item in list2)
					{
						list.Items.Add(new ListViewItem(new string[3]
						{
							item.Status,
							item.Incoming.Title,
							item.Incoming.Path
						})
						{
							Tag = item,
							Checked = (item.Status == "Add" || item.Status == "Update")
						});
					}
					f.Body.Controls.Add(list);
					f.Action("Inspect selected", delegate
					{
						if (list.SelectedItems.Count != 0)
						{
							TransferChange transferChange = (TransferChange)list.SelectedItems[0].Tag;
							UserTools.Report(f, "Incoming metadata review", "LOCAL:\r\n" + Json.Serialize(transferChange.Existing) + "\r\n\r\nINCOMING:\r\n" + Json.Serialize(transferChange.Incoming));
						}
					});
					f.Action("Apply checked changes", delegate
					{
						Idle(d);
						Store.CreateRestorePoint(d, "Before reviewed sync");
						ApplyTransfer(d, from ListViewItem i in list.CheckedItems
							select (TransferChange)i.Tag);
						Record(d, "Applied reviewed Library sync");
						Store.Save(d);
						f.Close();
					});
					f.Action("Cancel", f.Close);
					f.ShowDialog(owner);
				}
				finally
				{
					if (f != null)
					{
						((IDisposable)f).Dispose();
					}
				}
			}
		}

		public static void Show(IWin32Window owner, LibraryData d)
		{
			Ensure(d);
			NextDialog f = new NextDialog("Library extensions", 940, 710);
			try
			{
				FishBowlTabs tabs = new FishBowlTabs
				{
					Dock = DockStyle.Fill,
					PreferredColumns = 4
				};
				f.Body.Controls.Add(tabs);
				Dictionary<string, FlowLayoutPanel> pages = new Dictionary<string, FlowLayoutPanel>();
				string[] array = new string[4] { "Library", "Emulators", "Protect and move", "Presentation" };
				foreach (string text in array)
				{
					TabPage tabPage = new TabPage(text);
					tabPage.AutoScroll = true;
					tabPage.UseVisualStyleBackColor = false;
					tabPage.BackColor = FishBowlPalette.ThemeSurface;
					tabPage.ForeColor = FishBowlPalette.ThemeInk;
					TabPage tabPage2 = tabPage;
					tabs.TabPages.Add(tabPage2);
					FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
					flowLayoutPanel.Dock = DockStyle.Top;
					flowLayoutPanel.AutoSize = true;
					flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
					flowLayoutPanel.WrapContents = false;
					flowLayoutPanel.Padding = new Padding(14);
					FlowLayoutPanel value = flowLayoutPanel;
					tabPage2.Controls.Add(value);
					pages[text] = value;
				}
				Action<string, string, string, Action> action2 = delegate(string tab, string title, string description, Action action)
				{
					TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
					{
						Width = 790,
						Height = 68,
						ColumnCount = 2,
						RowCount = 1,
						Margin = new Padding(4, 8, 4, 8)
					};
					tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
					tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
					FishBowlActionButton fishBowlActionButton = ExperienceUi.Button(title, action);
					fishBowlActionButton.Dock = DockStyle.Fill;
					fishBowlActionButton.AutoSize = true;
					fishBowlActionButton.MinimumSize = new Size(0, 48);
					tableLayoutPanel.Controls.Add(fishBowlActionButton, 0, 0);
					tableLayoutPanel.Controls.Add(new Label
					{
						Dock = DockStyle.Fill,
						Text = description,
						ForeColor = FishBowlPalette.ThemeInk,
						TextAlign = ContentAlignment.MiddleLeft,
						Margin = new Padding(12, 4, 4, 4)
					}, 1, 0);
					pages[tab].Controls.Add(tableLayoutPanel);
				};
				action2("Library", "Launch readiness", "Check a game and open its guided repair options.", delegate
				{
					PickGame(f, d, delegate(GameEntry g)
					{
						UserTools.Troubleshoot(f, d, g, Readiness(d, g));
					});
				});
				action2("Library", "Recent activity", "Recent play, Library edits and save snapshots.", delegate
				{
					Activity(f, d);
				});
				action2("Library", "Preview folder import", "Review and select discovered games before adding them.", delegate
				{
					ImportFolder(f, d);
				});
				action2("Library", "Add PC game / shortcut", "Launch installed Windows games alongside emulated games.", delegate
				{
					AddPc(f, d);
				});
				action2("Library", "Nested collections", "Include child collections in their parent's Library view.", delegate
				{
					Collections(f, d);
				});
				action2("Library", "Game media and custom fields", "Launch overrides, trailers, manuals and searchable custom metadata.", delegate
				{
					PickGame(f, d, delegate(GameEntry g)
					{
						GameOptions(f, d, g);
					});
				});
				action2("Library", "Online metadata and covers", "Optional Wikipedia lookup and reviewed local/online cover selection.", delegate
				{
					PickGame(f, d, delegate(GameEntry g)
					{
						Metadata(f, d, g);
					});
				});
				action2("Emulators", "Versions, updates and rollback", "Inventory installed builds, check releases, activate registered versions.", delegate
				{
					Inventory(f, d);
				});
				action2("Emulators", "Adapters and setup templates", "Import/export optional JSON definitions for additional emulators.", delegate
				{
					Adapters(f, d);
				});
				action2("Emulators", "Per-game builds and presets", "Reuse registered emulator builds and launch profiles.", delegate
				{
					NextTools.PreferredBuild(f, d, null);
				});
				action2("Protect and move", "Storage overview", "Measure configured games, artwork, saves and snapshots; review retention.", delegate
				{
					StorageScreen(f, d);
				});
				action2("Protect and move", "Backup schedule", "Configure existing snapshot export scheduling and storage quota.", delegate
				{
					HubSaveSchedule.Settings(f, d);
				});
				action2("Protect and move", "Cloud conflict review", "Find filenames indicating conflicts without deleting copies.", delegate
				{
					Conflicts(f);
				});
				action2("Protect and move", "Portable collection paths", "Record relative paths and review a relocation to another drive.", delegate
				{
					Portable(f, d);
				});
				action2("Protect and move", "Profile transfer and sync", "Offline transfer packages and explicit conflict resolution.", delegate
				{
					Transfer(f, d);
				});
				action2("Presentation", "Immersion", "Focus view, personal shelves, galleries, atmosphere and one settings page.", ()=>Immersion.Show(f,d,null));
                action2("Presentation", "Living-room Library", "Full-screen, large cards with mouse/keyboard navigation.", delegate
				{
					using (LivingRoomLibrary livingRoomLibrary = new LivingRoomLibrary(d))
					{
						livingRoomLibrary.ShowDialog(f);
					}
				});
				action2("Presentation", "Library view styles", "Card sizes, spacing, platform labels and a cover-detail pane.", delegate
				{
					ViewStyles(f, d);
				});
				action2("Presentation", "Crash recovery", "Offer the previous Library view after an interrupted application run.", delegate
				{
					NextDialog prompt = new NextDialog("Crash recovery");
					try
					{
						CheckBox check = new CheckBox
						{
							Dock = DockStyle.Top,
							Checked = Ensure(d).ResumeAfterCrash,
							Text = "Offer to reopen the previous Library view after an unexpected exit"
						};
						prompt.Body.Controls.Add(check);
						prompt.Action("Save", delegate
						{
							Ensure(d).ResumeAfterCrash = check.Checked;
							Store.Save(d);
							prompt.Close();
						});
						prompt.Action("Close", prompt.Close);
						prompt.ShowDialog(f);
					}
					finally
					{
						if (prompt != null)
						{
							((IDisposable)prompt).Dispose();
						}
					}
				});
				f.Shown += delegate
				{
					foreach (FlowLayoutPanel value2 in pages.Values)
					{
						value2.Width = Math.Max(420, tabs.ClientSize.Width - 40);
						foreach (TableLayoutPanel item in value2.Controls.OfType<TableLayoutPanel>())
						{
							item.Width = value2.Width - 40;
							item.Height = Math.Max(68, f.Font.Height * 4);
						}
					}
				};
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}

		public static void PickGame(IWin32Window owner, LibraryData d, Action<GameEntry> action)
		{
			using (NextDialog nextDialog = new NextDialog("Choose a Library game", 750, 530))
			{
				ListBox list = new ListBox
				{
					Dock = DockStyle.Fill,
					DisplayMember = "Title",
					DataSource = d.Games.OrderBy((GameEntry g) => g.Title).ToList()
				};
				nextDialog.Body.Controls.Add(list);
				nextDialog.Action("Open selected", delegate
				{
					GameEntry gameEntry = list.SelectedItem as GameEntry;
					if (gameEntry == null)
					{
						throw new IOException("Add a game first.");
					}
					action(gameEntry);
				});
				nextDialog.Action("Close", nextDialog.Close);
				nextDialog.ShowDialog(owner);
			}
		}

	  public static Bitmap PlatformThumbnail(string path,string title,string platform,int size,bool badge){var bitmap=GameLibraryDialog.MakeThumbnail(path,title,size);if(badge&&!string.IsNullOrWhiteSpace(platform)){string label=platform.Length>5?platform.Substring(0,5):platform;using(var graphics=Graphics.FromImage(bitmap))using(var font=new Font("Segoe UI",Math.Max(7,size/12),FontStyle.Bold))using(var fill=new SolidBrush(FishBowlPalette.ThemeSurface)){int width=Math.Min(size-4,TextRenderer.MeasureText(label,font).Width+6);var bounds=new Rectangle(size-width-2,size-font.Height-8,width,font.Height+6);graphics.FillRectangle(fill,bounds);FishBowlText.DrawText(graphics,label,font,bounds,FishBowlPalette.ThemeInk,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}return bitmap;}

	public static void ViewStyles(IWin32Window owner, LibraryData d)
		{
			if (d.Cosmetics == null)
			{
				d.Cosmetics = new CosmeticSettings();
			}
			NextDialog f = new NextDialog("Library view styles", 820, 620);
			try
			{
				TableLayoutPanel table = NextDialog.Fields(f.Body);
				NumericUpDown size = NextDialog.Number(d.Experience.ArtworkSize, 64m, 192m);
				ComboBox density = NextDialog.Choice(new string[2] { "Compact", "Comfortable" }, d.Cosmetics.LibrarySpacing ?? "Comfortable");
				CheckBox platform = new CheckBox
				{
					Text = "Show platform labels on Library rows/cards",
					Checked = d.Cosmetics.PlatformLabels
				};
				CheckBox preview = new CheckBox
				{
					Text = "Show a cover and details pane",
					Checked = d.Cosmetics.DetailPreview
				};
				CheckBox reduced = new CheckBox
				{
					Text = "Reduce motion",
					Checked = d.Enhancements.ReducedMotion
				};
				NextDialog.Field(table, "Artwork card size", size);
				NextDialog.Field(table, "Spacing", density);
				NextDialog.Field(table, "", platform);
				NextDialog.Field(table, "", preview);
				NextDialog.Field(table, "", reduced);
				NextDialog.Field(table, "", new Label
				{
					Text = "Details and artwork views retain their own sorting/filter history. Changes apply to open Library views after closing this screen.",
					AutoSize = true
				}, 85);
				f.Action("Save", delegate
				{
					d.Experience.ArtworkSize = (int)size.Value;
					d.Cosmetics.LibrarySpacing = density.Text;
					d.Cosmetics.PlatformLabels = platform.Checked;
					d.Cosmetics.DetailPreview = preview.Checked;
					d.Enhancements.ReducedMotion = reduced.Checked;
					d.Theme.EnableMotion = !reduced.Checked;
					FishBowlHighlights.ReducedMotion = reduced.Checked;
					Store.Save(d);
					foreach (GameLibraryDialog item in Application.OpenForms.OfType<GameLibraryDialog>())
					{
						item.ReloadLibrary();
					}
					f.Close();
				});
				f.Action("Close", f.Close);
				f.ShowDialog(owner);
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
		}
	}
}

namespace EmulatorHub
{
	public class HubActivity
	{
		public string At { get; set; }

		public string UserId { get; set; }

		public string Text { get; set; }
	}
}

namespace EmulatorHub
{
	public class HubSettings
	{
        public ImmersionSettings Immersion {get;set;}
		public List<EmulatorAdapter> Adapters { get; set; }

		public List<PathBinding> Paths { get; set; }

		public List<HubActivity> Activity { get; set; }

		public Dictionary<string, string> SyncHashes { get; set; }

		public bool ResumeAfterCrash { get; set; } public bool WatchReleases {get;set;} public string LastReleaseWatchAt {get;set;} public Dictionary<string,string> NotifiedReleases {get;set;}

		public int CaptureMinutes { get; set; }

		public string NextCaptureAt { get; set; }

		public string LastCaptureReport { get; set; }

		public HubSettings()
		{
			Adapters = new List<EmulatorAdapter>();
			Paths = new List<PathBinding>();
			Activity = new List<HubActivity>();
			SyncHashes = new Dictionary<string, string>();
			ResumeAfterCrash = true;
		}
	}
}

namespace EmulatorHub
{
	public class MetadataMatch
	{
		public string Title { get; set; }

		public string Description { get; set; }

		public string Url { get; set; }

		public string Image { get; set; }

		public override string ToString()
		{
			return Title;
		}
	}
}

namespace EmulatorHub
{
	public class PathBinding
	{
        public string Original {get;set;}
		public string Key { get; set; }

		public string Relative { get; set; }
	}
}

namespace EmulatorHub
{
	public class TransferChange
	{
		public GameEntry Incoming;

		public GameEntry Existing;

		public string Status;

		public string ReviewedHash;

		public override string ToString()
		{
			return Status + ": " + Incoming.Title;
		}
	}
}

namespace EmulatorHub
{
	public class TransferPackage
	{
		public int Schema { get; set; }

		public string At { get; set; }

		public BowlUser Profile { get; set; }

		public List<GameEntry> Games { get; set; }
	}
}

namespace EmulatorHub {
 public class LivingRoomLibrary : NextDialog {
  readonly LibraryData data;
  readonly ListBox games=new ListBox{Dock=DockStyle.Fill,DisplayMember="Title",ItemHeight=120,DrawMode=DrawMode.OwnerDrawFixed,AccessibleName="Living-room game cards"};
  readonly TextBox search=new TextBox{Dock=DockStyle.Top,AccessibleName="Find a game"};Font roomFont,searchFont;
  readonly Dictionary<string,Bitmap> covers=new Dictionary<string,Bitmap>();bool onlyFavorites;
  public LivingRoomLibrary(LibraryData d):base("Living-room Library",1100,750){
   data=d;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;KeyPreview=true;Body.Controls.Add(games);Body.Controls.Add(search);
   games.DrawItem+=(a,b)=>{if(b.Index<0)return;b.DrawBackground();var g=(GameEntry)games.Items[b.Index];Bitmap cover;
    if(!covers.TryGetValue(g.Id,out cover)){if(covers.Count>=48){foreach(var old in covers.Values)old.Dispose();covers.Clear();}cover=Hub.PlatformThumbnail(g.ArtworkPath,g.Title,g.ConsoleLabel,96,true);covers[g.Id]=cover;}
    int imageSize=Math.Min(160,b.Bounds.Height-20);b.Graphics.DrawImage(cover,new Rectangle(b.Bounds.X+10,b.Bounds.Y+10,imageSize,imageSize));
    var ink=(b.State&DrawItemState.Selected)!=0?SystemColors.HighlightText:FishBowlPalette.ThemeInk;
    int titleHeight=games.Font.Height+18;FishBowlText.DrawText(b.Graphics,(g.Favorite?"★ ":"")+g.Title,games.Font,new Rectangle(b.Bounds.X+imageSize+30,b.Bounds.Y+12,Math.Max(10,b.Bounds.Width-imageSize-50),titleHeight),ink,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
    using(var smaller=new Font(games.Font.FontFamily,Math.Max(12,games.Font.Size/2)))FishBowlText.DrawText(b.Graphics,g.ConsoleLabel??"",smaller,new Rectangle(b.Bounds.X+imageSize+32,b.Bounds.Y+titleHeight+20,Math.Max(10,b.Bounds.Width-imageSize-50),smaller.Height+8),ink,TextFormatFlags.EndEllipsis);b.DrawFocusRectangle();};
   search.TextChanged+=(a,b)=>RefreshGames();games.DoubleClick+=(a,b)=>Launch();KeyDown+=(a,b)=>{if(b.KeyCode==Keys.Escape){Close();b.SuppressKeyPress=true;}else if(b.KeyCode==Keys.Enter&&games.ContainsFocus){Launch();b.SuppressKeyPress=true;}};
   Action("Launch",Launch);Action("Details",()=>{var g=games.SelectedItem as GameEntry;if(g!=null)using(var f=new GameExperienceDialog(data,g))f.ShowDialog(this);RefreshAfterDetails();});Action("Favorites / all",()=>{onlyFavorites=!onlyFavorites;RefreshGames();});Action("Exit full screen",Close);
   Shown+=(a,b)=>{float scale=Math.Max(1,NextUi.TextPercent/100f);roomFont=new Font(string.IsNullOrWhiteSpace(NextUi.FontFamily)?"Segoe UI":NextUi.FontFamily,24*scale);searchFont=new Font(roomFont.FontFamily,20*scale);games.Font=roomFont;games.ItemHeight=(int)(120*scale);search.Font=searchFont;};Disposed+=(a,b)=>{foreach(var cover in covers.Values)cover.Dispose();if(roomFont!=null)roomFont.Dispose();if(searchFont!=null)searchFont.Dispose();};RefreshGames();
  }
  void RefreshAfterDetails(){foreach(var cover in covers.Values)cover.Dispose();covers.Clear();RefreshGames();}
  void RefreshGames(){var selected=games.SelectedItem as GameEntry;string id=selected==null?null:selected.Id;games.DataSource=null;var items=data.Games.Where(g=>(!onlyFavorites||g.Favorite)&&Hub.SearchText(g).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(g=>g.Title).ToList();games.DataSource=items;var remembered=items.FirstOrDefault(g=>g.Id==id);if(remembered!=null)games.SelectedItem=remembered;}
  void Launch(){var g=games.SelectedItem as GameEntry;if(g!=null)ExperienceTools.Launch(this,data,g);}
 }
}

namespace EmulatorHub {
 public partial class MainForm {
  void InitializeHub(bool preview){Hub.Ensure(library);FluidStyle.Configure(library);SectionMotion.Attach(this);if(!preview)StartHubRuntime();}
  public bool HubBusy {get{return backgroundLibraryScanRunning||automaticCopyRunning||captureRunning||releaseWatchRunning;}}
  public void OpenCommandSearch(){ShowGlobalSearch();}
  void ShowHubExtensions(){Hub.Show(this,library);RefreshHub();embeddedLibrary.ReloadLibrary();}
 }
 public partial class GameLibraryDialog {
  Panel detailPane;PictureBox detailCover;TextBox detailText;string detailStamp;ImageList densityImages;int densityHeight;
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam);
  void ApplyHubSpacing(){if(games.Columns.Count>=5&&games.ClientSize.Width>0){int beforeLast=games.Columns.Cast<ColumnHeader>().Take(games.Columns.Count-1).Sum(c=>c.Width);games.Columns[games.Columns.Count-1].Width=Math.Max(130,games.ClientSize.Width-beforeLast-SystemInformation.VerticalScrollBarWidth);}int height=library.Cosmetics!=null&&library.Cosmetics.LibrarySpacing=="Compact"?Math.Max(FluidStyle.Roomier?24:18,games.Font.Height+(FluidStyle.Roomier?8:4)):Math.Max(FluidStyle.Roomier?34:28,games.Font.Height+(FluidStyle.Roomier?18:12));if(densityImages==null){densityImages=new ImageList{ColorDepth=ColorDepth.Depth32Bit};games.SmallImageList=densityImages;}if(height!=densityHeight){densityImages.Images.Clear();densityImages.ImageSize=new Size(1,height);var handle=densityImages.Handle;using(var blank=new Bitmap(1,height))densityImages.Images.Add(blank);densityHeight=height;}if(artworkView&&games.IsHandleCreated){int width=Math.Max(110,library.Experience.ArtworkSize+32);int vertical=library.Experience.ArtworkSize+(library.Cosmetics!=null&&library.Cosmetics.LibrarySpacing=="Compact"?38:65);SendMessage(games.Handle,0x1035,IntPtr.Zero,new IntPtr((vertical<<16)|width));}}
  void InitializeHubLibrary(){StartImmersionLibrary();KeyPreview=true;KeyDown+=(a,b)=>{if(TopLevel&&b.Control&&b.KeyCode==Keys.K){b.SuppressKeyPress=true;var main=Application.OpenForms.OfType<MainForm>().FirstOrDefault();if(main!=null)main.OpenCommandSearch();else using(var searchDialog=new GlobalSearchDialog(library.Games.Select(g=>new SearchResult{Caption="Game: "+g.Title,Open=()=>Hub.GameOptions(this,library,g)}).Concat(new[]{new SearchResult{Caption="Library extensions",Open=()=>Hub.Show(this,library)}}))){searchDialog.ShowDialog(this);var selected=searchDialog.SelectedAction;if(selected!=null)BeginInvoke(selected);}ReloadOnVisit();b.SuppressKeyPress=true;}};detailPane=new Panel{Dock=DockStyle.Right,Width=265,Visible=false,Padding=new Padding(12),BackColor=FishBowlPalette.ThemeSurface};detailCover=new PictureBox{Dock=DockStyle.Top,Height=220,SizeMode=PictureBoxSizeMode.Zoom};detailText=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,ForeColor=FishBowlPalette.ThemeInk,BackColor=FishBowlPalette.ThemeSurface,AccessibleName="Selected game details"};detailPane.Controls.Add(detailText);detailPane.Controls.Add(detailCover);var area=new Panel{Dock=DockStyle.Fill};Controls.Remove(games);Controls.Remove(emptyState);area.Controls.Add(games);area.Controls.Add(emptyState);area.Controls.Add(detailPane);Controls.Add(area);area.BringToFront();SizeChanged+=(a,b)=>{UpdateDetailPane();ApplyHubSpacing();};games.SelectedIndexChanged+=(a,b)=>UpdateDetailPane();Disposed+=(a,b)=>{if(detailCover.Image!=null)detailCover.Image.Dispose();games.SmallImageList=null;if(densityImages!=null)densityImages.Dispose();};}
  void UpdateDetailPane(){RefreshImmersionBackdrop();if(detailPane==null)return;detailPane.Visible=library.Cosmetics!=null&&library.Cosmetics.DetailPreview&&ClientSize.Width>=900;var g=SelectedGame();string stamp=g==null?"":g.Id+"|"+g.ArtworkPath+"|"+g.Description;if(stamp==detailStamp)return;detailStamp=stamp;var old=detailCover.Image;detailCover.Image=null;if(old!=null)old.Dispose();detailText.Text=g==null?"Select a game to preview its cover and details.":g.Title+"\r\n"+g.ConsoleLabel+"\r\n"+g.Genre+"\r\n"+g.Description;if(g!=null&&File.Exists(g.ArtworkPath))try{using(var source=Image.FromFile(g.ArtworkPath))if(source.Width<12000&&source.Height<12000)detailCover.Image=new Bitmap(source,new Size(220,200));}catch{}}
 }
}
