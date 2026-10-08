using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// Profiles shared by both apps: user profiles and guest mode, profile transfer and reviewed sync, launch and controller
// profiles, multiplayer readiness and keyboard shortcut text. Keep this file C# 5 compatible for the Windows csc.exe build.
// Linux-only launch options (Proton or Wine, GameMode, MangoHud, Gamescope, environment) and FishBowl's command line
// live in the NETCOREAPP section at the end.
namespace EmulatorHub
{
	// User profiles: personal progress and preferences per person; games, emulators and saves are shared.
	public static partial class UserTools
	{
		public static int ActiveLaunches;

		public static readonly ConcurrentDictionary<string, byte> ActiveSessions = new ConcurrentDictionary<string, byte>();

		private static readonly string[] BulkFields = new string[7] { "PreferredEmulatorId", "EmulatorId", "PlayStatus", "Favorite", "Pinned", "Tags", "ArtworkPath" };

		public static bool Guest { get; private set; }

		// Each app supplies these: Windows prepares its experience data and reports background work.
		static partial void PrepareLibrary(LibraryData d);

		static partial void AfterSwitch(LibraryData d);

		static partial void BackgroundBusy(ref bool busy);

		private static bool Busy()
		{
			bool busy = false;
			BackgroundBusy(ref busy);
			return busy;
		}

		public static T Copy<T>(T value)
		{
			return Json.Deserialize<T>(Json.Serialize(value));
		}

		public static UserToolSettings Ensure(LibraryData d)
		{
			PrepareLibrary(d);
			if (d.UserTools == null)
			{
				d.UserTools = new UserToolSettings();
			}
			UserToolSettings userTools = d.UserTools;
			if (userTools.Users == null)
			{
				userTools.Users = new List<BowlUser>();
			}
			if (userTools.Undo == null)
			{
				userTools.Undo = new List<BulkUndoRecord>();
			}
			if (userTools.Users.Count == 0)
			{
				BowlUser bowlUser = Capture(d, "Default");
				userTools.Users.Add(bowlUser);
				userTools.ActiveId = bowlUser.Id;
			}
			return userTools;
		}

		public static BowlUser Capture(LibraryData d, string name)
		{
			UserToolSettings userToolSettings = d.UserTools ?? new UserToolSettings();
			BowlUser bowlUser = new BowlUser();
			bowlUser.Id = Guid.NewGuid().ToString("N");
			bowlUser.Name = name;
			bowlUser.Games = d.Games.Select((GameEntry g) => new PersonalGame
			{
				Id = g.Id,
				Favorite = g.Favorite,
				Pinned = g.Pinned,
				PlayStatus = g.PlayStatus,
				PersonalRating = g.PersonalRating,
				TotalPlaySeconds = g.TotalPlaySeconds,
				LaunchCount = g.LaunchCount,
				LastLaunched = g.LastLaunched
			}).ToList();
			bowlUser.Theme = Copy(d.Theme);
			bowlUser.Cosmetics = Copy(d.Cosmetics);
			bowlUser.Enhancements = Copy(d.Enhancements);
			bowlUser.Experience = Copy(d.Experience);
			bowlUser.Sessions = Copy(d.PlaySessions);
			bowlUser.Queue = Copy(d.PlayQueue);
			bowlUser.Lists = Copy(d.SmartLists);
			bowlUser.WeeklyMinutes = userToolSettings.WeeklyMinutes;
			bowlUser.BreakMinutes = userToolSettings.BreakMinutes;
			bowlUser.Controller = userToolSettings.Controller;
			bowlUser.Views = Copy(userToolSettings.Views);
			bowlUser.Navigation = Copy(userToolSettings.Navigation);
			bowlUser.SaveRoutes = Copy(userToolSettings.SaveRoutes);
			return bowlUser;
		}

		public static void SaveActive(LibraryData d)
		{
			UserToolSettings s = Ensure(d);
			BowlUser bowlUser = s.Users.FirstOrDefault((BowlUser x) => x.Id == s.ActiveId);
			if (bowlUser != null)
			{
				BowlUser bowlUser2 = Capture(d, bowlUser.Name);
				bowlUser2.Id = bowlUser.Id;
				s.Users[s.Users.IndexOf(bowlUser)] = bowlUser2;
			}
		}

		public static BowlUser Create(LibraryData d, string name)
		{
			UserToolSettings userToolSettings = Ensure(d);
			name = (name ?? "").Trim();
			if (name.Length == 0 || name.Length > 60 || userToolSettings.Users.Any((BowlUser existing) => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				throw new IOException("Use a unique profile name of 1–60 characters.");
			}
			BowlUser bowlUser = Capture(d, name);
			bowlUser.Games = new List<PersonalGame>();
			bowlUser.Sessions = new List<PlaySession>();
			bowlUser.Queue = new List<string>();
			bowlUser.Lists = new List<SmartLibraryList>();
			bowlUser.WeeklyMinutes = 0;
			bowlUser.BreakMinutes = 0;
			bowlUser.Navigation = null;
			bowlUser.Views = null;
			bowlUser.SaveRoutes = new List<SaveRouting>();
			userToolSettings.Users.Add(bowlUser);
			return bowlUser;
		}

		public static void Switch(LibraryData d, string id)
		{
			if (Guest || ActiveLaunches > 0 || Busy())
			{
				throw new IOException("Finish launched emulator sessions before changing profiles.");
			}
			UserToolSettings userToolSettings = Ensure(d);
			BowlUser bowlUser = userToolSettings.Users.FirstOrDefault((BowlUser x) => x.Id == id);
			if (bowlUser == null)
			{
				throw new IOException("Profile unavailable.");
			}
			if (id == userToolSettings.ActiveId)
			{
				return;
			}
			SaveActive(d);
			bowlUser = userToolSettings.Users.First((BowlUser x) => x.Id == id);
			Dictionary<string, PersonalGame> dictionary = (from p in bowlUser.Games ?? new List<PersonalGame>()
				group p by p.Id).ToDictionary((IGrouping<string, PersonalGame> group) => group.Key, (IGrouping<string, PersonalGame> group) => group.First());
			foreach (GameEntry game in d.Games)
			{
				PersonalGame value;
				dictionary.TryGetValue(game.Id, out value);
				game.Favorite = value != null && value.Favorite;
				game.Pinned = value != null && value.Pinned;
				game.PlayStatus = ((value == null) ? "Not started" : value.PlayStatus);
				game.PersonalRating = ((value != null) ? value.PersonalRating : 0);
				game.TotalPlaySeconds = ((value == null) ? 0 : value.TotalPlaySeconds);
				game.LaunchCount = ((value != null) ? value.LaunchCount : 0);
				game.LastLaunched = ((value == null) ? null : value.LastLaunched);
			}
			d.Theme = Copy(bowlUser.Theme);
			d.Cosmetics = Copy(bowlUser.Cosmetics);
			d.Enhancements = Copy(bowlUser.Enhancements);
			d.Experience = Copy(bowlUser.Experience);
			d.PlaySessions = Copy(bowlUser.Sessions) ?? new List<PlaySession>();
			d.PlayQueue = Copy(bowlUser.Queue) ?? new List<string>();
			d.SmartLists = Copy(bowlUser.Lists) ?? new List<SmartLibraryList>();
			userToolSettings.WeeklyMinutes = bowlUser.WeeklyMinutes;
			userToolSettings.BreakMinutes = bowlUser.BreakMinutes;
			userToolSettings.Controller = bowlUser.Controller;
			userToolSettings.Navigation = Copy(bowlUser.Navigation);
			userToolSettings.Views = Copy(bowlUser.Views);
			userToolSettings.SaveRoutes = Copy(bowlUser.SaveRoutes);
			userToolSettings.ActiveId = id;
			AfterSwitch(d);
		}

		private static List<GameEntry> BulkSnapshot(List<GameEntry> games)
		{
			return games.Select((GameEntry g) => new GameEntry
			{
				Id = g.Id,
				PreferredEmulatorId = g.PreferredEmulatorId,
				EmulatorId = g.EmulatorId,
				PlayStatus = g.PlayStatus,
				Favorite = g.Favorite,
				Pinned = g.Pinned,
				Tags = ((g.Tags == null) ? null : new List<string>(g.Tags)),
				ArtworkPath = g.ArtworkPath
			}).ToList();
		}

		public static BulkUndoRecord BeforeBulk(LibraryData d, List<GameEntry> games)
		{
			BulkUndoRecord bulkUndoRecord = new BulkUndoRecord();
			bulkUndoRecord.At = DateTime.Now.ToString("g");
			bulkUndoRecord.UserId = Ensure(d).ActiveId;
			bulkUndoRecord.Before = BulkSnapshot(games);
			bulkUndoRecord.CollectionsBefore = Copy(d.Collections);
			return bulkUndoRecord;
		}

		public static void AfterBulk(LibraryData d, BulkUndoRecord r, List<GameEntry> games)
		{
			r.After = BulkSnapshot(games);
			r.CollectionsAfter = Copy(d.Collections);
			UserToolSettings userToolSettings = Ensure(d);
			userToolSettings.Undo.Add(r);
			while (userToolSettings.Undo.Count > 10)
			{
				userToolSettings.Undo.RemoveAt(0);
			}
		}

		private static bool Equal(object a, object b)
		{
			return Json.Serialize(a) == Json.Serialize(b);
		}

		public static string UndoBulk(LibraryData d)
		{
			UserToolSettings s = Ensure(d);
			BulkUndoRecord bulkUndoRecord = s.Undo.LastOrDefault((BulkUndoRecord x) => x.UserId == s.ActiveId);
			if (bulkUndoRecord == null)
			{
				return "No bulk edits to undo for this profile.";
			}
			List<Action> list2 = new List<Action>();
			int num = 0;
			foreach (GameEntry before in bulkUndoRecord.Before)
			{
				List<GameEntry> after = bulkUndoRecord.After;
				Func<GameEntry, bool> predicate = (GameEntry x) => x.Id == before.Id;
				GameEntry gameEntry = after.FirstOrDefault(predicate);
				GameEntry gameEntry2 = d.Games.FirstOrDefault((GameEntry x) => x.Id == before.Id);
				if (gameEntry2 == null || gameEntry == null)
				{
					num++;
					continue;
				}
				string[] bulkFields = BulkFields;
				foreach (string name in bulkFields)
				{
					PropertyInfo property = typeof(GameEntry).GetProperty(name);
					object value2 = property.GetValue(before, null);
					object value3 = property.GetValue(gameEntry, null);
					if (Equal(value2, value3))
					{
						continue;
					}
					if (!Equal(property.GetValue(gameEntry2, null), value3))
					{
						num++;
						continue;
					}
					GameEntry target = gameEntry2;
					PropertyInfo prop = property;
					object value = value2;
					list2.Add(delegate
					{
						prop.SetValue(target, value, null);
					});
				}
			}
			foreach (GameCollection a in bulkUndoRecord.CollectionsAfter ?? new List<GameCollection>())
			{
				GameCollection gameCollection = (bulkUndoRecord.CollectionsBefore ?? new List<GameCollection>()).FirstOrDefault((GameCollection x) => x.Id == a.Id);
				GameCollection gameCollection2 = d.Collections.FirstOrDefault((GameCollection x) => x.Id == a.Id);
				List<string> list3 = (a.GameIds ?? new List<string>()).Except((gameCollection == null) ? new List<string>() : (gameCollection.GameIds ?? new List<string>())).ToList();
				if (list3.Count > 0 && (gameCollection2 == null || !Equal(gameCollection2.GameIds, a.GameIds)))
				{
					num++;
					continue;
				}
				foreach (string item in list3)
				{
					if (gameCollection2 != null && gameCollection2.GameIds != null)
					{
						List<string> list = gameCollection2.GameIds;
						string key = item;
						list2.Add(delegate
						{
							list.Remove(key);
						});
					}
				}
			}
			if (num > 0)
			{
				return "Undo stopped: " + num + " fields changed again or games were removed. No changes made.";
			}
			foreach (Action item2 in list2)
			{
				item2();
			}
			s.Undo.Remove(bulkUndoRecord);
			Store.Save(d);
			return "Undid bulk edit from " + bulkUndoRecord.At + ". Unrelated edits were preserved.";
		}

		public static long WeekSeconds(LibraryData d, DateTime utc)
		{
			long num = 0L;
			foreach (PlaySession item in d.PlaySessions ?? new List<PlaySession>())
			{
				DateTime result;
				if (DateTime.TryParse(item.StartedAt, null, DateTimeStyles.RoundtripKind, out result) && !(result.ToUniversalTime() < utc.AddDays(-7.0)) && !(result.ToUniversalTime() > utc))
				{
					num += Math.Max(0L, item.Seconds);
				}
			}
			return num;
		}

		// artwork turns a cover path into base64 PNG data (or null); pass null to leave covers out.
		public static string Html(IEnumerable<GameEntry> games, bool paths, bool history, Func<string, string> artwork, CancellationToken token)
		{
			StringBuilder stringBuilder = new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>FishBowl catalog</title><style>body{font:16px system-ui;background:#182332;color:#eef3fa;margin:24px}main{max-width:900px;margin:auto}article{background:#253348;border-radius:12px;padding:16px;margin:12px 0}h2{margin-top:0}p{overflow-wrap:anywhere}input{font:inherit;padding:12px;width:90%;background:#fff;color:#172334}small{color:#c8d6e6}</style><main><h1>Game catalog</h1><label>Filter games <input id='filter' type='search'></label>");
			foreach (GameEntry item in games.OrderBy((GameEntry x) => x.Title))
			{
				token.ThrowIfCancellationRequested();
				if (stringBuilder.Length > 52428800)
				{
					throw new IOException("Catalog exceeds 50 MB. Export fewer games or turn off artwork.");
				}
				stringBuilder.Append("<article>");
				if (artwork != null)
				{
					string text = artwork(item.ArtworkPath);
					if (text != null)
					{
						stringBuilder.Append("<img alt='' width='128' height='128' style='object-fit:contain;float:right' src='data:image/png;base64," + text + "'>");
					}
				}
				stringBuilder.Append("<h2>" + Escape(item.Title) + "</h2><p>" + Escape(item.Genre) + " · " + Escape(item.ConsoleLabel) + "</p><p>" + Escape(item.Description) + "</p><small>" + Escape(string.Join(", ", item.Tags ?? new List<string>())) + "</small>");
				if (item.Extras != null && HttpsLink(item.Extras.MetadataSource))
				{
					stringBuilder.Append("<p><a href=\"" + Escape(item.Extras.MetadataSource) + "\">Metadata source and licensing</a></p>");
				}
				if (paths)
				{
					stringBuilder.Append("<p>File: " + Escape(item.Path) + "</p>");
				}
				if (history)
				{
					stringBuilder.Append("<p>" + Escape(item.PlayStatus) + " · " + item.LaunchCount + " launches · " + item.TotalPlaySeconds / 60 + " minutes · Last played: " + Escape(item.LastLaunched) + "</p>");
				}
				stringBuilder.Append("</article>");
			}
			return stringBuilder.Append("</main><script>document.getElementById('filter').addEventListener('input',function(){var q=this.value.toLowerCase();document.querySelectorAll('article').forEach(function(a){a.hidden=a.textContent.toLowerCase().indexOf(q)<0;});});</script></html>").ToString();
		}

		public static string Escape(string s)
		{
			return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
				.Replace("\"", "&quot;")
				.Replace("'", "&#39;");
		}

		public static LibraryData BeginGuest(LibraryData d)
		{
			if (Guest || ActiveLaunches > 0 || Busy())
			{
				throw new IOException("Finish emulator sessions before entering guest mode.");
			}
			LibraryData result = Copy(d);
			Guest = true;
#if NETCOREAPP
			Store.ReadOnly = true;
#endif
			return result;
		}

		public static void EndGuest(LibraryData d, LibraryData snapshot)
		{
			if (ActiveLaunches > 0)
			{
				throw new IOException("Close the guest's launched emulator before leaving guest mode.");
			}
			PropertyInfo[] properties = typeof(LibraryData).GetProperties();
			foreach (PropertyInfo propertyInfo in properties)
			{
				propertyInfo.SetValue(d, propertyInfo.GetValue(snapshot, null), null);
			}
			Guest = false;
#if NETCOREAPP
			Store.ReadOnly = false;
#endif
		}

		private static bool HttpsLink(string value)
		{
			Uri result;
			return Uri.TryCreate(value, UriKind.Absolute, out result) && result.Scheme == "https" && string.IsNullOrEmpty(result.UserInfo);
		}
	}

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

	public class TransferPackage
	{
		public int Schema { get; set; }

		public string At { get; set; }

		public BowlUser Profile { get; set; }

		public List<GameEntry> Games { get; set; }
	}

	// Profile transfer and reviewed Library sync between computers (a JSON file; no game bytes, programs or saves).
	public static class ProfileTransfer
	{
		public const long MaximumBytes = 16777216L;

		public const string Explanation = "Use a transfer JSON file on another computer or in your cloud client's local folder. Files include game metadata, paths and the active personal profile, but no game bytes, emulator executables or save contents.\r\n\r\nImport previews every change. Conflicts default to keeping local data. Existing game paths, emulator assignments, linked saves and recorded time remain local. New entries may need path repair and emulator assignment. Profiles can be imported separately. Cloud upload/download completion is managed by your cloud client; FishBowl does not silently merge files.";

		public static Dictionary<string, string> SyncHashes(LibraryData d)
		{
			if (d.Hub == null)
			{
				d.Hub = new HubSettings();
			}
			if (d.Hub.SyncHashes == null)
			{
				d.Hub.SyncHashes = new Dictionary<string, string>();
			}
			return d.Hub.SyncHashes;
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

		// Writes a transfer file and remembers what was exported, so later imports can tell updates from conflicts.
		public static void Export(LibraryData d, string path)
		{
			TransferPackage value = ExportPackage(d);
			File.WriteAllText(path, Json.Serialize(value));
			foreach (GameEntry game in d.Games)
			{
				SyncHashes(d)[game.Id] = Fingerprint(game);
			}
		}

		public static TransferPackage Read(string path)
		{
			if (new FileInfo(path).Length > MaximumBytes)
			{
				throw new IOException("Transfer exceeds 16 MB.");
			}
			TransferPackage package = Json.Deserialize<TransferPackage>(File.ReadAllText(path));
			if (package == null)
			{
				throw new IOException("Unsupported transfer package.");
			}
			return package;
		}

		public static List<TransferChange> Plan(LibraryData d, TransferPackage p)
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
			HashSet<string> hashSet = new HashSet<string>(from g in d.Games
				where !string.IsNullOrWhiteSpace(g.Path)
				select g.Path, StringComparer.OrdinalIgnoreCase);
			foreach (GameEntry game in p.Games)
			{
				GameEntry value;
				dictionary.TryGetValue(game.Id, out value);
				if (value == null)
				{
					if (!string.IsNullOrWhiteSpace(game.Path) && !hashSet.Add(game.Path))
					{
						list.Add(new TransferChange
						{
							Incoming = game,
							Status = "Duplicate path — keep local"
						});
					}
					else
					{
						list.Add(new TransferChange
						{
							Incoming = game,
							Status = "Add"
						});
					}
					continue;
				}
				string text = Fingerprint(game);
				string text2 = Fingerprint(value);
				string value2;
				SyncHashes(d).TryGetValue(game.Id, out value2);
				if (!(text == text2))
				{
					list.Add(new TransferChange
					{
						Incoming = game,
						Existing = value,
						ReviewedHash = text2,
						Status = ((value2 != null && text2 == value2) ? "Update" : "Conflict — choose explicitly")
					});
				}
			}
			return list;
		}

		// Applies reviewed changes; local paths, assignments, saves and recorded time stay as they are.
		public static void Apply(LibraryData d, IEnumerable<TransferChange> chosen)
		{
			if (UserTools.Guest || UserTools.ActiveLaunches > 0)
			{
				throw new IOException("Finish active sessions and background work before changing Library paths or profiles.");
			}
			List<TransferChange> list = chosen.ToList();
			HashSet<string> hashSet = new HashSet<string>(from g in d.Games
				where !string.IsNullOrWhiteSpace(g.Path)
				select g.Path, StringComparer.OrdinalIgnoreCase);
			foreach (TransferChange item in list.Where((TransferChange c) => c.Existing == null))
			{
				if (!string.IsNullOrWhiteSpace(item.Incoming.Path) && !hashSet.Add(item.Incoming.Path))
				{
					throw new IOException("A new entry's path is already present. Start a fresh synchronization preview.");
				}
			}
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
			foreach (TransferChange item2 in list)
			{
				GameEntry gameEntry2 = UserTools.Copy(item2.Incoming);
				if (item2.Existing == null)
				{
					d.Games.Add(gameEntry2);
				}
				else
				{
					gameEntry2.Arguments = item2.Existing.Arguments;
					gameEntry2.LaunchProfileName = item2.Existing.LaunchProfileName;
					if (gameEntry2.Extras == null)
					{
						gameEntry2.Extras = UserTools.Copy(item2.Existing.Extras);
					}
					else
					{
						gameEntry2.Extras.Native = GameLibraryQuery.Native(item2.Existing);
						gameEntry2.Extras.WorkingDirectory = ((item2.Existing.Extras == null) ? null : item2.Existing.Extras.WorkingDirectory);
					}
					gameEntry2.ArtworkPath = (File.Exists(gameEntry2.ArtworkPath) ? gameEntry2.ArtworkPath : item2.Existing.ArtworkPath);
					gameEntry2.ManualPath = item2.Existing.ManualPath;
					gameEntry2.Path = item2.Existing.Path;
					gameEntry2.EmulatorId = item2.Existing.EmulatorId;
					gameEntry2.PreferredEmulatorId = item2.Existing.PreferredEmulatorId;
					gameEntry2.PreferredBuildId = item2.Existing.PreferredBuildId;
					gameEntry2.Saves = item2.Existing.Saves;
					gameEntry2.Discs = item2.Existing.Discs;
					gameEntry2.LastDiscPath = item2.Existing.LastDiscPath;
					gameEntry2.TotalPlaySeconds = item2.Existing.TotalPlaySeconds;
					gameEntry2.LaunchCount = item2.Existing.LaunchCount;
					gameEntry2.LastLaunched = item2.Existing.LastLaunched;
					d.Games[d.Games.IndexOf(item2.Existing)] = gameEntry2;
				}
				SyncHashes(d)[gameEntry2.Id] = Fingerprint(gameEntry2);
			}
		}

		// Adds the package's personal profile as a new, separately named profile.
		public static BowlUser ImportProfile(LibraryData d, TransferPackage p)
		{
			if (p == null || p.Profile == null)
			{
				throw new IOException("Package has no personal profile.");
			}
			if (UserTools.Guest || UserTools.ActiveLaunches > 0)
			{
				throw new IOException("Finish active sessions and background work before changing Library paths or profiles.");
			}
			BowlUser bowlUser = UserTools.Copy(p.Profile);
			bowlUser.Id = Guid.NewGuid().ToString("N");
			bowlUser.Name = (bowlUser.Name ?? "Imported") + " (imported)";
			UserTools.Ensure(d).Users.Add(bowlUser);
			return bowlUser;
		}

		// File names cloud clients give conflict copies (Dropbox, OneDrive, Syncthing and similar), up to 500.
		public static List<string> CloudConflicts(string root, CancellationToken token)
		{
			List<string> result = new List<string>();
			Stack<string> pending = new Stack<string>();
			pending.Push(root);
			while (pending.Count > 0 && result.Count < 500)
			{
				token.ThrowIfCancellationRequested();
				string folder = pending.Pop();
				try
				{
					foreach (string child in Directory.GetDirectories(folder))
					{
						if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
					}
					foreach (string file in Directory.GetFiles(folder))
					{
						string name = Path.GetFileName(file).ToLowerInvariant();
						if (name.Contains("conflicted copy") || name.Contains("conflict") || name.Contains("sync-conflict"))
						{
							result.Add(file);
							if (result.Count >= 500) break;
						}
					}
				}
				catch (UnauthorizedAccessException) { }
				catch (IOException) { }
			}
			return result;
		}
	}

	public class MultiplayerCapability
	{
		public string NativeOnline { get; set; }

		public string LocalPlay { get; set; }

		public string RemoteCouchPlay { get; set; }

		public string Notes { get; set; }
	}

	public static class MultiplayerSupport
	{
		public static MultiplayerCapability For(EmulatorProfile profile)
		{
			string name = EmulatorCatalog.Find((profile == null) ? "Custom" : profile.Preset).Name;
			MultiplayerCapability multiplayerCapability = new MultiplayerCapability();
			multiplayerCapability.NativeOnline = "Check the emulator's own multiplayer or netplay settings.";
			multiplayerCapability.LocalPlay = "Configure additional controllers inside the emulator.";
			multiplayerCapability.RemoteCouchPlay = "Browser window streaming and approved keyboard/gamepad-to-keyboard controls use the configured LiveKit token service. Start the game first; configure matching keys in the emulator.";
			multiplayerCapability.Notes = "FishBowl never changes the emulator's multiplayer settings.";
			MultiplayerCapability multiplayerCapability2 = multiplayerCapability;
			switch (name)
			{
			case "Dolphin":
				multiplayerCapability2.NativeOnline = "Dolphin includes NetPlay for supported GameCube and Wii games.";
				multiplayerCapability2.LocalPlay = "Dolphin supports multiple GameCube controllers and Wii Remotes.";
				break;
			case "RetroArch":
				multiplayerCapability2.NativeOnline = "RetroArch supports netplay for compatible cores and matching content.";
				multiplayerCapability2.LocalPlay = "RetroArch supports multiple local players when the loaded core does.";
				break;
			case "PPSSPP":
				multiplayerCapability2.NativeOnline = "PPSSPP supports its own multiplayer modes for compatible games.";
				multiplayerCapability2.LocalPlay = "Configure local controller mappings in PPSSPP.";
				break;
			case "Azahar Plus":
				multiplayerCapability2.NativeOnline = "Azahar Plus may offer multiplayer features depending on its build and the game.";
				multiplayerCapability2.LocalPlay = "Configure additional controllers inside Azahar Plus.";
				break;
			}
			return multiplayerCapability2;
		}

		public static List<string> Readiness(EmulatorProfile profile, MultiplayerSettings settings)
		{
			List<string> list = new List<string>();
			if (profile == null)
			{
				list.Add("Choose an emulator before starting a session.");
				return list;
			}
			list.Add(File.Exists(profile.Executable) ? "Ready: emulator program found." : "Needs attention: emulator program is missing.");
			list.Add((EmulatorRuntime.State(profile.Executable) == RuntimeState.Running) ? "Ready: emulator is running." : "Info: emulator is not running yet; FishBowl can open it for a session.");
			list.Add("Check controller mappings and game compatibility inside " + profile.Name + ".");
			list.Add(string.IsNullOrWhiteSpace((settings == null) ? null : settings.RelayGatewayUrl) ? "Remote couch play is not connected to a LiveKit token service." : "A LiveKit Cloud token service is configured.");
			return list;
		}

		// The full readiness report shown by "Multiplayer readiness check".
		public static string Report(EmulatorProfile profile, MultiplayerSettings settings)
		{
			List<string> list = Readiness(profile, settings);
			if (profile != null)
			{
				MultiplayerCapability capability = For(profile);
				list.Add("");
				list.Add("NATIVE ONLINE\n" + capability.NativeOnline);
				list.Add("\nLOCAL PLAY\n" + capability.LocalPlay);
				list.Add("\nREMOTE COUCH PLAY\n" + capability.RemoteCouchPlay);
			}
			return string.Join("\n\n", list.ToArray());
		}

		public static MultiplayerSettings Defaults()
		{
			MultiplayerSettings settings = new MultiplayerSettings();
			settings.InviteOnly = true;
			settings.RelayProvider = "LiveKit Cloud";
			settings.UpdateChannel = "Stable";
			settings.CheckForHubUpdates = true;
			return settings;
		}

		// A LiveKit token endpoint must be a full https address, or blank. Returns the trimmed value.
		public static string CheckRelay(string text)
		{
			text = (text ?? "").Trim();
			Uri result;
			if (text.Length > 0 && (!Uri.TryCreate(text, UriKind.Absolute, out result) || result.Scheme != "https"))
			{
				throw new IOException("Use a full https LiveKit token endpoint, or leave it blank.");
			}
			return text;
		}
	}

	// Launch and controller profiles: small rules both apps' dialogs share.
	public static class ProfileTools
	{
		public static LaunchProfile AddLaunchProfile(EmulatorProfile emulator)
		{
			if (emulator.LaunchProfiles == null)
			{
				emulator.LaunchProfiles = new List<LaunchProfile>();
			}
			string name = "New profile";
			for (int i = 2; emulator.LaunchProfiles.Any((LaunchProfile p) => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
			{
				name = "New profile " + i;
			}
			LaunchProfile profile = new LaunchProfile();
			profile.Name = name;
			profile.Arguments = "";
			emulator.LaunchProfiles.Add(profile);
			return profile;
		}

		// Renames a launch profile and keeps the games that use it pointing at it. Returns the number of games updated.
		public static int UpdateLaunchProfile(LibraryData library, EmulatorProfile emulator, LaunchProfile profile, string name, string arguments)
		{
			string old = profile.Name;
			name = string.IsNullOrWhiteSpace(name) ? "Launch profile" : name.Trim();
			if (emulator.LaunchProfiles.Any((LaunchProfile p) => p != profile && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				throw new IOException("Another launch profile of " + emulator.Name + " is already called " + name + ".");
			}
			profile.Name = name;
			profile.Arguments = (arguments ?? "").Trim();
			int updated = 0;
			if (old != name && library != null)
			{
				foreach (GameEntry game in library.Games.Where((GameEntry g) => g.LaunchProfileName == old && GameLibraryQuery.EmulatorFor(library, g) == emulator))
				{
					game.LaunchProfileName = name;
					updated++;
				}
			}
			return updated;
		}

		// Removes a launch profile; games that used it go back to the emulator's default launch.
		public static void RemoveLaunchProfile(LibraryData library, EmulatorProfile emulator, LaunchProfile profile)
		{
			emulator.LaunchProfiles.Remove(profile);
			if (library == null) return;
			foreach (GameEntry game in library.Games.Where((GameEntry g) => g.LaunchProfileName == profile.Name && GameLibraryQuery.EmulatorFor(library, g) == emulator))
			{
				game.LaunchProfileName = null;
			}
		}

		public static List<ControllerProfile> ControllerProfiles(LibraryData library, EmulatorProfile emulator)
		{
			if (library.ControllerProfiles == null)
			{
				library.ControllerProfiles = new List<ControllerProfile>();
			}
			return (from p in library.ControllerProfiles
				where emulator == null || p.EmulatorId == emulator.Id
				orderby p.Name
				select p).ToList();
		}

		public static ControllerProfile AddControllerProfile(LibraryData library, EmulatorProfile emulator)
		{
			if (emulator == null)
			{
				throw new IOException("Select an emulator to add a profile.");
			}
			ControllerProfiles(library, emulator);
			ControllerProfile controllerProfile = new ControllerProfile();
			controllerProfile.Id = Guid.NewGuid().ToString("N");
			controllerProfile.EmulatorId = emulator.Id;
			controllerProfile.Name = "New profile";
			controllerProfile.Notes = "";
			controllerProfile.UpdatedAt = DateTime.UtcNow.ToString("o");
			library.ControllerProfiles.Add(controllerProfile);
			return controllerProfile;
		}

		public static void UpdateControllerProfile(ControllerProfile profile, string name, string notes)
		{
			profile.Name = (string.IsNullOrWhiteSpace(name) ? "Controller profile" : name.Trim());
			profile.Notes = (notes ?? "").Trim();
			profile.UpdatedAt = DateTime.UtcNow.ToString("o");
		}

		// Removes a controller profile and the game reminders that pointed at it.
		public static void RemoveControllerProfile(LibraryData library, ControllerProfile profile)
		{
			library.ControllerProfiles.Remove(profile);
			foreach (GameEntry game in library.Games.Where((GameEntry g) => g.ControllerProfileId == profile.Id))
			{
				game.ControllerProfileId = null;
			}
		}

		// The controller reminder shown before a game starts, or null.
		public static ControllerProfile ControllerReminder(LibraryData library, GameEntry game)
		{
			if (game == null || string.IsNullOrWhiteSpace(game.ControllerProfileId)) return null;
			return (library.ControllerProfiles ?? new List<ControllerProfile>()).FirstOrDefault((ControllerProfile p) => p.Id == game.ControllerProfileId);
		}

		// Break reminders: how many whole break periods a running session has reached (0 when none or reminders are off).
		public static int BreakPeriods(UserToolSettings settings, PlaySession session, DateTime utcNow)
		{
			DateTime started;
			if (settings == null || settings.BreakMinutes <= 0 || session == null || session.Uncertain || session.EndedAt != null) return 0;
			if (!DateTime.TryParse(session.StartedAt, null, DateTimeStyles.RoundtripKind, out started)) return 0;
			return Math.Max(0, (int)((utcNow - started.ToUniversalTime()).TotalMinutes / settings.BreakMinutes));
		}

		// "120 minutes / 300 goal (40%)" for the rolling 7-day goal.
		public static string WeeklyGoal(LibraryData library, DateTime utcNow)
		{
			UserToolSettings s = UserTools.Ensure(library);
			long minutes = UserTools.WeekSeconds(library, utcNow) / 60;
			return minutes + " minutes" + ((s.WeeklyMinutes > 0) ? (" / " + s.WeeklyMinutes + " goal (" + Math.Min(100L, minutes * 100 / s.WeeklyMinutes) + "%)") : " — goal off") + ". Goals use recorded emulator time.";
		}
	}

	// Keyboard shortcut text as both apps store it ("Control, P", "F11", "P, Control, Shift").
	public static class ShortcutText
	{
		public static readonly string[] Reserved = new string[] { "Escape", "Enter", "Control, F" };

		// Splits a shortcut into modifiers and one key name. Returns false when the text is not a single key with modifiers.
		public static bool Parse(string text, out bool control, out bool shift, out bool alt, out string key)
		{
			control = shift = alt = false;
			key = null;
			if (string.IsNullOrWhiteSpace(text)) return false;
			foreach (string raw in text.Split(new char[] { ',', '+' }))
			{
				string part = raw.Trim();
				if (part.Length == 0) return false;
				string lower = part.ToLowerInvariant();
				if (lower == "control" || lower == "ctrl") control = true;
				else if (lower == "shift") shift = true;
				else if (lower == "alt") alt = true;
				else if (key == null) key = Key(part);
				else return false;
			}
			return key != null;
		}

		private static string Key(string part)
		{
			if (part.Length == 1 && char.IsLetter(part[0])) return part.ToUpperInvariant();
			if (part.Length == 1 && char.IsDigit(part[0])) return "D" + part;
			string lower = part.ToLowerInvariant();
			if (lower == "return") return "Enter";
			if (lower == "esc") return "Escape";
			return char.ToUpperInvariant(part[0]) + part.Substring(1);
		}

		// The stored form: modifiers first, as in the Windows defaults ("Control, Shift, P").
		public static string Normalize(string text)
		{
			bool control, shift, alt;
			string key;
			if (!Parse(text, out control, out shift, out alt, out key)) return null;
			List<string> parts = new List<string>();
			if (control) parts.Add("Control");
			if (shift) parts.Add("Shift");
			if (alt) parts.Add("Alt");
			parts.Add(key);
			return string.Join(", ", parts.ToArray());
		}

		// Checks edited shortcuts and returns them normalized. Throws IOException naming the first problem.
		public static Dictionary<string, string> Validate(Dictionary<string, string> edited)
		{
			Dictionary<string, string> used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> result = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> item in edited)
			{
				string normal = Normalize(item.Value);
				if (normal == null)
				{
					throw new IOException("Use a key name such as Control, P for " + item.Key);
				}
				if (Reserved.Contains(normal, StringComparer.OrdinalIgnoreCase))
				{
					throw new IOException("Escape, Enter and Control, F are reserved for navigation.");
				}
				if (used.ContainsKey(normal))
				{
					throw new IOException("Shortcut conflicts with " + used[normal]);
				}
				used[normal] = item.Key;
				result[item.Key] = normal;
			}
			return result;
		}

		// The saved shortcuts with any missing action filled in from the defaults.
		public static Dictionary<string, string> Current(LibraryData library)
		{
			Dictionary<string, string> result = ModelDefaults.Shortcuts();
			if (library.Enhancements != null && library.Enhancements.Shortcuts != null)
			{
				foreach (KeyValuePair<string, string> item in library.Enhancements.Shortcuts)
				{
					if (result.ContainsKey(item.Key) && Normalize(item.Value) != null) result[item.Key] = Normalize(item.Value);
				}
			}
			return result;
		}

		// The action bound to a key press, or null.
		public static string Action(LibraryData library, bool control, bool shift, bool alt, string key)
		{
			foreach (KeyValuePair<string, string> item in Current(library))
			{
				bool c, s, a;
				string k;
				if (Parse(item.Value, out c, out s, out a, out k) && c == control && s == shift && a == alt && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return item.Key;
			}
			return null;
		}
	}
}
