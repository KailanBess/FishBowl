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
using System.Xml;

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

		public static readonly string[] AccessibilityPresets = new string[] { "Current", "Larger text", "High contrast", "Reduced motion", "Controller" };

		// Accessibility presets from "Appearance and Home" and "Live appearance and accessibility".
		public static void ApplyAccessibility(LibraryData data, string preset)
		{
			if (preset == "Current") return;
			if (data.Theme == null) data.Theme = new ThemeSettings();
			if (data.Enhancements == null) data.Enhancements = new NextSettings();
			if (preset == "Larger text") data.Enhancements.TextPercent = 150;
			if (preset == "High contrast") { data.Theme.Name = "High Contrast"; data.Theme.SelectionContrast = "Strong"; data.Theme.RestrainedAccents = false; }
			if (preset == "Reduced motion") { data.Enhancements.ReducedMotion = true; data.Theme.EnableMotion = false; }
			if (preset == "Controller") { UserTools.Ensure(data).Controller = true; Immersion.Ensure(data).Roomier = true; data.Theme.ControlDensity = "Roomy"; data.Enhancements.TextPercent = Math.Max(125, data.Enhancements.TextPercent); }
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

	// "Check selected file against DAT": compares a raw game file with a local XML DAT (No-Intro/Redump style).
	public static class RomIntegrity
	{
		private static readonly uint[] CrcTable = BuildCrcTable();

		private static uint[] BuildCrcTable()
		{
			uint[] array = new uint[256];
			for (uint num = 0u; num < 256; num++)
			{
				uint num2 = num;
				for (int i = 0; i < 8; i++)
				{
					num2 = (num2 >> 1) ^ (((num2 & (true ? 1u : 0u)) != 0) ? 3988292384u : 0u);
				}
				array[num] = num2;
			}
			return array;
		}

		public static string Check(string dat, string path, CancellationToken token, Action<string> progress)
		{
			if (!File.Exists(path))
			{
				throw new IOException("The selected game file is missing.");
			}
			if (new FileInfo(dat).Length > 104857600)
			{
				throw new IOException("DAT exceeds 100 MB.");
			}
			List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
			using (XmlReader xmlReader = XmlReader.Create(dat, new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Ignore,
				XmlResolver = null,
				MaxCharactersInDocument = 104857600L
			}))
			{
				while (xmlReader.Read())
				{
					token.ThrowIfCancellationRequested();
					if (xmlReader.NodeType == XmlNodeType.Element && !(xmlReader.LocalName != "rom"))
					{
						Dictionary<string, string> dictionary = new Dictionary<string, string>();
						string[] array = new string[6] { "name", "size", "crc", "md5", "sha1", "status" };
						foreach (string text in array)
						{
							dictionary[text] = xmlReader.GetAttribute(text);
						}
						list.Add(dictionary);
					}
				}
			}
			long size = new FileInfo(path).Length;
			List<Dictionary<string, string>> list2 = list.Where((Dictionary<string, string> r) => r["status"] != "nodump" && (r["size"] == null || r["size"] == size.ToString(CultureInfo.InvariantCulture))).ToList();
			if (list2.Count == 0)
			{
				return "No reference of this size. The DAT may describe a different format or an uncompressed ROM. No files changed.";
			}
			uint num = uint.MaxValue;
			long num2 = 0L;
			using (MD5 mD = MD5.Create())
			{
				using (SHA1 sHA = SHA1.Create())
				{
					using (FileStream fileStream = File.OpenRead(path))
					{
						byte[] array2 = new byte[1048576];
						int num3;
						while ((num3 = fileStream.Read(array2, 0, array2.Length)) > 0)
						{
							token.ThrowIfCancellationRequested();
							mD.TransformBlock(array2, 0, num3, array2, 0);
							sHA.TransformBlock(array2, 0, num3, array2, 0);
							for (int l = 0; l < num3; l++)
							{
								num = (num >> 8) ^ CrcTable[(num ^ array2[l]) & 0xFF];
							}
							num2 += num3;
							progress("Checking " + Path.GetFileName(path) + " — " + ((size == 0) ? 100 : (num2 * 100 / size)) + "%");
						}
						mD.TransformFinalBlock(new byte[0], 0, 0);
						sHA.TransformFinalBlock(new byte[0], 0, 0);
						string i = Hex(mD.Hash);
						string s = Hex(sHA.Hash);
						string c = (~num).ToString("x8");
						List<Dictionary<string, string>> list3 = list2.Where((Dictionary<string, string> r) => Matches(r, "md5", i) && Matches(r, "sha1", s) && Matches(r, "crc", c) && new string[3] { "crc", "md5", "sha1" }.Any((string k) => !string.IsNullOrWhiteSpace(r[k]))).ToList();
						string text2 = "\r\nCRC32: " + c + "\r\nMD5: " + i + "\r\nSHA1: " + s;
						if (list3.Count > 0)
						{
							return "Verified against reference: " + string.Join(", ", list3.Select((Dictionary<string, string> r) => r["name"])) + text2 + "\r\nAll supplied hashes matched. Reference quality depends on your DAT.";
						}
						return (list.Any((Dictionary<string, string> r) => string.Equals(r["name"], Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) ? "Mismatch against named reference. The file may be modified, damaged, or a different revision." : "Unrecognized file. No hash match in this DAT; this alone does not prove corruption.") + text2 + "\r\nNo files changed.";
					}
				}
			}
		}

		private static bool Matches(Dictionary<string, string> r, string key, string value)
		{
			return string.IsNullOrWhiteSpace(r[key]) || string.Equals(r[key], value, StringComparison.OrdinalIgnoreCase);
		}

		private static string Hex(byte[] b)
		{
			return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
		}
	}

#if NETCOREAPP
	// Linux launch options for an emulator or one of its launch profiles. Saved in that record's extra fields as
	// "LinuxLaunch", so the Windows build ignores them and Linux keeps them; blank values mean the default.
	public class LinuxLaunchOptions
	{
		public string Runner { get; set; }        // Windows programs: "Proton", "Wine", or blank for automatic
		public string WinePrefix { get; set; }    // blank: <FishBowl data>/prefixes/<emulator name>
		public string WineProgram { get; set; }   // blank: wine from PATH
		public string Proton { get; set; }        // blank: umu's UMU-Proton; "GE-Proton"; a version name; or a Proton folder
		public string GameId { get; set; }        // umu GAMEID; blank: umu-default
		public string UmuStore { get; set; }      // umu STORE; blank: not set
		public bool GameMode { get; set; }
		public bool MangoHud { get; set; }
		public bool Gamescope { get; set; }
		public int GamescopeWidth { get; set; }
		public int GamescopeHeight { get; set; }
		public int GamescopeFrameLimit { get; set; }
		public bool GamescopeFullscreen { get; set; }
		public string GamescopeArguments { get; set; }
		public string EnvironmentVariables { get; set; } // KEY=VALUE lines

		public bool IsDefault
		{
			get
			{
				return string.IsNullOrWhiteSpace(Runner) && string.IsNullOrWhiteSpace(WinePrefix) && string.IsNullOrWhiteSpace(WineProgram) && string.IsNullOrWhiteSpace(Proton) && string.IsNullOrWhiteSpace(GameId) && string.IsNullOrWhiteSpace(UmuStore)
					&& !GameMode && !MangoHud && !Gamescope && string.IsNullOrWhiteSpace(EnvironmentVariables);
			}
		}

		public LinuxLaunchOptions Copy() { return (LinuxLaunchOptions)MemberwiseClone(); }
	}

	// What the launch planner may look up on this computer; tests supply their own.
	public class LaunchHost
	{
		public string DataDirectory, Home;
		public Func<string, string> Find; // program name to full path, or null when not installed

		public static LaunchHost Current()
		{
			LaunchHost host = new LaunchHost();
			host.DataDirectory = Store.DataDirectory; host.Home = Platform.Home; host.Find = FindProgram;
			return host;
		}

		// PATH, plus the umu-run Faugus Launcher keeps in its own data folder.
		public static string FindProgram(string name)
		{
			string found = Platform.FindOnPath(name);
			if (found == null && name == "umu-run")
			{
				string faugus = Path.Combine(Platform.DataHome, "faugus-launcher", "umu-run");
				if (File.Exists(faugus)) found = faugus;
			}
			return found;
		}
	}

	// Applies Linux launch options to a launch plan. Wrapper order, outermost first:
	// gamescope [options] -- gamemoderun mangohud <runner> <program> <arguments>.
	public static class LinuxLaunch
	{
		public const string Field = "LinuxLaunch";
		public const string ProtonRunner = "Proton", WineRunner = "Wine", GeProton = "GE-Proton";
		public const string UmuInstall = "Install umu-launcher (Arch: sudo pacman -S umu-launcher, from the multilib repository). Faugus Launcher also includes it.";
		public const string WineInstall = "Install Wine (Arch: sudo pacman -S wine).";

		// ----- Storage ---------------------------------------------------------------------------------------------

		private static LinuxLaunchOptions Read(Dictionary<string, System.Text.Json.JsonElement> fields)
		{
			System.Text.Json.JsonElement element;
			if (fields == null || !fields.TryGetValue(Field, out element) || element.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
			try { return Json.Deserialize<LinuxLaunchOptions>(element.GetRawText()); }
			catch (InvalidDataException) { return null; }
		}

		private static Dictionary<string, System.Text.Json.JsonElement> Write(Dictionary<string, System.Text.Json.JsonElement> fields, LinuxLaunchOptions options)
		{
			if (fields == null) fields = new Dictionary<string, System.Text.Json.JsonElement>();
			if (options == null) fields.Remove(Field);
			else using (System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(Json.Serialize(options))) fields[Field] = document.RootElement.Clone();
			return fields.Count == 0 ? null : fields;
		}

		// The emulator's own options (never null).
		public static LinuxLaunchOptions For(EmulatorProfile emulator)
		{
			return (emulator == null ? null : Read(emulator.AdditionalFields)) ?? new LinuxLaunchOptions();
		}

		// A launch profile's own options, or null when it uses the emulator's.
		public static LinuxLaunchOptions For(LaunchProfile profile)
		{
			return profile == null ? null : Read(profile.AdditionalFields);
		}

		public static void Save(EmulatorProfile emulator, LinuxLaunchOptions options)
		{
			emulator.AdditionalFields = Write(emulator.AdditionalFields, options == null || options.IsDefault ? null : options);
		}

		// null makes the profile use the emulator's options again.
		public static void Save(LaunchProfile profile, LinuxLaunchOptions options)
		{
			profile.AdditionalFields = Write(profile.AdditionalFields, options);
		}

		// The launch profile's options when it has its own, otherwise the emulator's.
		public static LinuxLaunchOptions Effective(EmulatorProfile emulator, string profileName)
		{
			LaunchProfile profile = string.IsNullOrWhiteSpace(profileName) || emulator == null ? null : (emulator.LaunchProfiles ?? new List<LaunchProfile>()).FirstOrDefault((LaunchProfile p) => p.Name == profileName);
			return For(profile) ?? For(emulator);
		}

		// ----- Planning --------------------------------------------------------------------------------------------

		public static bool IsWindowsProgram(string program)
		{
			return string.Equals(Path.GetExtension(program ?? ""), ".exe", StringComparison.OrdinalIgnoreCase);
		}

		// The saved choice, else Proton when umu-run is installed, else Wine when wine is installed.
		public static string RunnerFor(LinuxLaunchOptions options, LaunchHost host)
		{
			string saved = options == null ? null : options.Runner;
			if (saved == ProtonRunner || saved == WineRunner) return saved;
			if (host.Find("umu-run") != null) return ProtonRunner;
			return host.Find("wine") != null ? WineRunner : null;
		}

		public static string DefaultPrefix(string name, LaunchHost host) { return Path.Combine(host.DataDirectory, "prefixes", HubPaths.SafeName(string.IsNullOrWhiteSpace(name) ? "emulator" : name)); }

		public static string PrefixFor(LinuxLaunchOptions options, string name, LaunchHost host)
		{
			return options == null || string.IsNullOrWhiteSpace(options.WinePrefix) ? DefaultPrefix(name, host) : Expand(options.WinePrefix, host);
		}

		public static string Expand(string path, LaunchHost host)
		{
			path = (path ?? "").Trim();
			if (path == "~") return host.Home;
			return path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(host.Home, path.Substring(2)) : path;
		}

		public static string ProtonLabel(string proton)
		{
			if (string.IsNullOrWhiteSpace(proton)) return "UMU-Proton";
			proton = proton.Trim().TrimEnd('/');
			return proton.StartsWith("/", StringComparison.Ordinal) || proton.StartsWith("~", StringComparison.Ordinal) ? Path.GetFileName(proton) : proton;
		}

		private static string WineLabel(string wine)
		{
			if (string.IsNullOrWhiteSpace(wine)) return "Wine";
			string folder = Path.GetDirectoryName(wine.Trim()) ?? "";
			return "Wine (" + (Path.GetFileName(folder) == "bin" ? Path.GetFileName(Path.GetDirectoryName(folder)) : Path.GetFileName(wine.Trim())) + ")";
		}

		// KEY=VALUE lines. Blank lines and # comments are ignored, "export " is accepted and matching outer quotes are removed.
		public static Dictionary<string, string> ParseVariables(string text, List<string> problems)
		{
			Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
			string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
				if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).TrimStart();
				int equals = line.IndexOf('=');
				string key = equals > 0 ? line.Substring(0, equals).Trim() : "";
				if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$"))
				{
					if (problems != null) problems.Add("Line " + (i + 1) + " is not KEY=VALUE: " + line);
					continue;
				}
				string value = line.Substring(equals + 1).Trim();
				if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0]) value = value.Substring(1, value.Length - 2);
				result[key] = value;
			}
			return result;
		}

		public static List<string> GamescopeOptions(LinuxLaunchOptions options, bool mangoapp)
		{
			List<string> result = new List<string>();
			CultureInfo c = CultureInfo.InvariantCulture;
			if (options.GamescopeWidth > 0 && options.GamescopeHeight > 0) result.AddRange(new string[] { "-W", options.GamescopeWidth.ToString(c), "-H", options.GamescopeHeight.ToString(c) });
			if (options.GamescopeFrameLimit > 0) result.AddRange(new string[] { "-r", options.GamescopeFrameLimit.ToString(c) });
			if (options.GamescopeFullscreen) result.Add("-f");
			if (mangoapp) result.Add("--mangoapp");
			result.AddRange(Platform.SplitArguments(options.GamescopeArguments).Where((string a) => a != "--"));
			return result;
		}

		// Rewrites the plan so its program runs through the chosen runner and tools. Throws IOException when it cannot
		// start (for example a Windows program without Proton or Wine); tools that are missing are skipped with a note.
		// Returns a summary such as "Proton (GE-Proton) · GameMode · MangoHud".
		public static string Apply(GamePlay.LaunchPlan plan, LinuxLaunchOptions options, LaunchHost host, string name)
		{
			options = options ?? new LinuxLaunchOptions();
			if (plan.Environment == null) plan.Environment = new Dictionary<string, string>(StringComparer.Ordinal);
			if (plan.Notes == null) plan.Notes = new List<string>();
			string program = plan.Program ?? "";
			bool windows = IsWindowsProgram(program);
			if (!windows && options.IsDefault) return "Native";
			Dictionary<string, string> variables = ParseVariables(options.EnvironmentVariables, plan.Notes);
			string flatpak = windows ? null : Platform.FlatpakId(program);
			List<string> labels = new List<string>(), parts = new List<string>();
			if (windows)
			{
				string runner = RunnerFor(options, host);
				if (runner == null) throw new IOException((name ?? "This emulator") + " is a Windows program and needs Proton or Wine. " + UmuInstall + " Or " + Lower(WineInstall));
				string prefix = PrefixFor(options, name, host);
				if (!Path.IsPathRooted(prefix)) throw new IOException("The Wine prefix must be a full folder path: " + prefix);
				Dictionary<string, string> builtIn = new Dictionary<string, string>(StringComparer.Ordinal);
				builtIn["WINEPREFIX"] = prefix;
				if (runner == ProtonRunner)
				{
					labels.Add("Proton (" + ProtonLabel(options.Proton) + ")");
					string umu = host.Find("umu-run");
					if (umu == null) throw new IOException("umu-launcher is not installed, so Proton is unavailable. " + UmuInstall + " Or choose Wine in Launch options.");
					builtIn["GAMEID"] = string.IsNullOrWhiteSpace(options.GameId) ? "umu-default" : options.GameId.Trim();
					if (!string.IsNullOrWhiteSpace(options.Proton))
					{
						string proton = Expand(options.Proton, host);
						if (Path.IsPathRooted(proton) && !Directory.Exists(proton)) throw new IOException("The chosen Proton folder no longer exists: " + proton + ". Choose another in Launch options.");
						builtIn["PROTONPATH"] = proton;
					}
					if (!string.IsNullOrWhiteSpace(options.UmuStore)) builtIn["STORE"] = options.UmuStore.Trim();
					parts.Add(umu);
				}
				else
				{
					labels.Add(WineLabel(options.WineProgram));
					string wine;
					if (string.IsNullOrWhiteSpace(options.WineProgram))
					{
						wine = host.Find("wine");
						if (wine == null) throw new IOException("Wine is not installed. " + WineInstall + " Or choose Proton in Launch options.");
					}
					else
					{
						wine = Expand(options.WineProgram, host);
						if (!File.Exists(wine)) throw new IOException("The chosen Wine program no longer exists: " + wine + ". Choose another in Launch options.");
					}
					parts.Add(wine);
				}
				// Wine and umu create the prefix itself, but not missing parent folders of FishBowl's default location.
				if (string.IsNullOrWhiteSpace(options.WinePrefix)) Directory.CreateDirectory(Path.GetDirectoryName(prefix));
				parts.Add(program);
				foreach (KeyValuePair<string, string> v in variables) builtIn[v.Key] = v.Value;
				variables = builtIn;
			}
			else if (flatpak != null)
			{
				labels.Add("Flatpak");
				// Flatpak drops LD_PRELOAD from the inherited environment but applies --env afterwards. Inside the sandbox,
				// libgamemodeauto (shipped by recent runtimes) reaches GameMode through its portal; MANGOHUD=1 turns on the
				// MangoHud Vulkan layer extension.
				Dictionary<string, string> sandbox = new Dictionary<string, string>(variables, StringComparer.Ordinal);
				bool gamescopeReady = options.Gamescope && host.Find("gamescope") != null;
				if (options.GameMode) sandbox["LD_PRELOAD"] = "libgamemodeauto.so.0" + (variables.ContainsKey("LD_PRELOAD") ? ":" + variables["LD_PRELOAD"] : "");
				if (options.MangoHud && !gamescopeReady && !variables.ContainsKey("MANGOHUD")) sandbox["MANGOHUD"] = "1";
				if (options.GameMode) labels.Add("GameMode");
				if (sandbox.ContainsKey("MANGOHUD")) labels.Add("MangoHud");
				List<string> run = sandbox.Count == 0 ? BaseCommand(plan, host) : FlatpakRun(program, flatpak, host, plan);
				if (sandbox.Count > 0) run.InsertRange(2, sandbox.OrderBy((KeyValuePair<string, string> v) => v.Key, StringComparer.Ordinal).Select((KeyValuePair<string, string> v) => "--env=" + v.Key + "=" + v.Value));
				parts.AddRange(run);
				variables = new Dictionary<string, string>(StringComparer.Ordinal);
			}
			else
			{
				labels.Add("Native");
				parts.AddRange(BaseCommand(plan, host));
			}
			parts.AddRange(plan.Arguments ?? new List<string>());

			string gamescope = options.Gamescope ? host.Find("gamescope") : null;
			bool mangoapp = false;
			string gameModeLabel = null, mangoLabel = null, gamescopeLabel = null;
			if (options.MangoHud && (flatpak == null || gamescope != null))
			{
				if (gamescope != null)
				{
					if (host.Find("mangoapp") != null) { mangoapp = true; mangoLabel = "MangoHud"; }
					else mangoLabel = Skip(plan, "MangoHud", "mangoapp, which shows MangoHud inside Gamescope, is not installed");
				}
				else
				{
					string mangohud = host.Find("mangohud");
					if (mangohud != null) { parts.Insert(0, mangohud); mangoLabel = "MangoHud"; }
					else mangoLabel = Skip(plan, "MangoHud", "mangohud is not installed (Arch: sudo pacman -S mangohud)");
				}
			}
			if (options.GameMode && flatpak == null)
			{
				string gamemode = host.Find("gamemoderun");
				if (gamemode != null) { parts.Insert(0, gamemode); gameModeLabel = "GameMode"; }
				else gameModeLabel = Skip(plan, "GameMode", "gamemoderun is not installed (Arch: sudo pacman -S gamemode)");
			}
			if (options.Gamescope)
			{
				if (gamescope != null)
				{
					List<string> wrap = new List<string>();
					wrap.Add(gamescope); wrap.AddRange(GamescopeOptions(options, mangoapp)); wrap.Add("--");
					parts.InsertRange(0, wrap);
					gamescopeLabel = "Gamescope";
				}
				else gamescopeLabel = Skip(plan, "Gamescope", "gamescope is not installed (Arch: sudo pacman -S gamescope)");
			}
			labels.AddRange(new string[] { gameModeLabel, mangoLabel, gamescopeLabel }.Where((string l) => l != null));
			plan.Program = parts[0];
			plan.Arguments = parts.Skip(1).ToList();
			foreach (KeyValuePair<string, string> v in variables) plan.Environment[v.Key] = v.Value;
			return string.Join(" · ", labels.ToArray());
		}

		private static string Lower(string text) { return text.Substring(0, 1).ToLowerInvariant() + text.Substring(1); }

		private static string Skip(GamePlay.LaunchPlan plan, string tool, string reason)
		{
			plan.Notes.Add("Started without " + tool + ": " + reason + ".");
			return tool + " (not installed)";
		}

		// The program exactly as FishBowl starts it without options (.desktop entries, scripts), with a full program path.
		private static List<string> BaseCommand(GamePlay.LaunchPlan plan, LaunchHost host)
		{
			System.Diagnostics.ProcessStartInfo info = Platform.StartInfo(plan.Program, "");
			List<string> command = new List<string>();
			string program = Path.IsPathRooted(info.FileName) ? info.FileName : host.Find(info.FileName);
			if (program == null) throw new IOException(info.FileName + " is not installed or not on PATH.");
			command.Add(program);
			command.AddRange(info.ArgumentList);
			if (Path.GetExtension(plan.Program).Equals(".desktop", StringComparison.OrdinalIgnoreCase) && Directory.Exists(info.WorkingDirectory)) plan.WorkingDirectory = info.WorkingDirectory;
			return command;
		}

		// "flatpak run [options] <id>" from a .desktop entry or exports/bin wrapper, so --env options can go before the ID.
		private static List<string> FlatpakRun(string launcher, string id, LaunchHost host, GamePlay.LaunchPlan plan)
		{
			List<string> run = null, rest = new List<string>();
			if (launcher.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase))
			{
				DesktopEntry entry = DesktopEntry.Read(launcher);
				if (entry != null)
				{
					if (Directory.Exists(entry.WorkingDirectory)) plan.WorkingDirectory = entry.WorkingDirectory;
					if (entry.Command.Count > 1 && Path.GetFileName(entry.Command[0]) == "flatpak" && entry.Command[1] == "run") run = new List<string>(entry.Command);
					else if (entry.Command.Count > 0) { run = WrapperCommand(entry.Command[0]); rest.AddRange(entry.Command.Skip(1)); }
				}
			}
			else run = WrapperCommand(launcher);
			if (run == null) run = new List<string> { "flatpak", "run", id };
			if (!Path.IsPathRooted(run[0])) run[0] = host.Find(run[0]) ?? run[0];
			run.AddRange(rest);
			return run;
		}

		// exports/bin wrappers are generated scripts: exec /usr/bin/flatpak run --branch=stable --arch=x86_64 <id> "$@"
		private static List<string> WrapperCommand(string wrapper)
		{
			try
			{
				if (!File.Exists(wrapper) || new FileInfo(wrapper).Length > 64 * 1024) return null;
				foreach (string line in File.ReadAllLines(wrapper))
				{
					List<string> words = Platform.SplitArguments(line.Trim());
					if (words.Count > 0 && words[0] == "exec") words.RemoveAt(0);
					if (words.Count > 2 && Path.GetFileName(words[0]) == "flatpak" && words[1] == "run") return words.Where((string w) => w != "$@").ToList();
				}
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
			return null;
		}

		// Called by GamePlay.Prepare: the game's launch profile options, else the emulator's.
		public static void ApplyToGame(GamePlay.LaunchPlan plan, EmulatorProfile emulator, GameEntry game)
		{
			plan.Summary = Apply(plan, Effective(emulator, game.LaunchProfileName), LaunchHost.Current(), emulator.Name);
		}

		// Opening an emulator by itself (optionally with a launch profile's arguments and options).
		public static GamePlay.LaunchPlan ForEmulator(EmulatorProfile emulator, LaunchProfile profile, LaunchHost host)
		{
			if (string.IsNullOrWhiteSpace(emulator.Executable) || !File.Exists(emulator.Executable)) throw new IOException("The program for " + (emulator.Name ?? "this emulator") + " is missing. Use Repair selected location.");
			GamePlay.LaunchPlan plan = new GamePlay.LaunchPlan();
			plan.Program = emulator.Executable;
			plan.Arguments = Platform.SplitArguments(profile == null ? "" : profile.Arguments);
			plan.WorkingDirectory = Path.GetDirectoryName(emulator.Executable);
			plan.Emulator = emulator;
			LinuxLaunchOptions options = (profile == null ? null : For(profile)) ?? For(emulator);
			plan.Summary = Apply(plan, options, host, emulator.Name);
			return plan;
		}

		// Starts a plan with its environment. Programs that are not wrapped start exactly as FishBowl always started them.
		public static System.Diagnostics.ProcessStartInfo StartInfo(GamePlay.LaunchPlan plan)
		{
			System.Diagnostics.ProcessStartInfo info = Platform.StartInfo(plan.Program, "");
			if (Directory.Exists(plan.WorkingDirectory)) info.WorkingDirectory = plan.WorkingDirectory;
			foreach (string argument in plan.Arguments ?? new List<string>()) info.ArgumentList.Add(argument);
			if (plan.Environment != null) foreach (KeyValuePair<string, string> v in plan.Environment) info.Environment[v.Key] = v.Value;
			return info;
		}

		// The command as a shell line, for the dialog preview and the activity log.
		public static string CommandLine(GamePlay.LaunchPlan plan)
		{
			List<string> parts = (plan.Environment ?? new Dictionary<string, string>()).OrderBy((KeyValuePair<string, string> v) => v.Key, StringComparer.Ordinal).Select((KeyValuePair<string, string> v) => v.Key + "=" + Quote(v.Value)).ToList();
			parts.Add(Quote(plan.Program));
			parts.AddRange((plan.Arguments ?? new List<string>()).Select((string a) => Quote(a)));
			return string.Join(" ", parts.ToArray());
		}

		public static string Quote(string value)
		{
			value = value ?? "";
			if (value.Length > 0 && value.All((char c) => char.IsLetterOrDigit(c) || "-_./=:,+@%".IndexOf(c) >= 0)) return value;
			return "'" + value.Replace("'", "'\\''") + "'";
		}

		// Setup-check rows for the Launch options dialog: what runs, and which tools are installed.
		public static List<string> Checks(EmulatorProfile emulator, LinuxLaunchOptions options, LaunchHost host)
		{
			List<string> rows = new List<string>();
			options = options ?? new LinuxLaunchOptions();
			try
			{
				GamePlay.LaunchPlan plan = new GamePlay.LaunchPlan();
				plan.Program = emulator.Executable; plan.Arguments = new List<string>(); plan.WorkingDirectory = Path.GetDirectoryName(emulator.Executable ?? "");
				LinuxLaunchOptions copy = options.Copy();
				string summary = Apply(plan, copy, host, emulator.Name);
				rows.Add("Runs with: " + summary);
				foreach (string note in plan.Notes) rows.Add(note);
				rows.Add("Command: " + CommandLine(plan) + (File.Exists(emulator.Executable) ? "" : "  (program missing)"));
			}
			catch (Exception error) { rows.Add("Needs attention: " + error.Message); }
			if (IsWindowsProgram(emulator.Executable))
			{
				string umu = host.Find("umu-run"), wine = host.Find("wine");
				rows.Add("umu-launcher (Proton): " + (umu ?? "not installed. " + UmuInstall));
				rows.Add("Wine: " + (wine ?? "not installed. " + WineInstall));
				string prefix = PrefixFor(options, emulator.Name, host);
				rows.Add("Wine prefix: " + prefix + (Directory.Exists(prefix) ? "" : " (created on first start)"));
			}
			string[][] tools = new string[][] {
				new string[] { "GameMode", "gamemoderun", "Install GameMode (Arch: sudo pacman -S gamemode)." },
				new string[] { "MangoHud", "mangohud", "Install MangoHud (Arch: sudo pacman -S mangohud)." },
				new string[] { "Gamescope", "gamescope", "Install Gamescope (Arch: sudo pacman -S gamescope)." } };
			bool flatpak = Platform.FlatpakId(emulator.Executable) != null;
			foreach (string[] tool in tools)
			{
				string found = host.Find(tool[1]);
				string detail = flatpak && tool[0] == "GameMode" ? "inside the Flatpak through the GameMode portal (needs freedesktop runtime 25.08 or newer and GameMode on this computer)"
					: flatpak && tool[0] == "MangoHud" ? "inside the Flatpak through the MangoHud Vulkan layer: flatpak install flathub org.freedesktop.Platform.VulkanLayer.MangoHud"
					: found ?? tool[2];
				rows.Add(tool[0] + ": " + (found != null ? "installed" : "not installed") + " — " + detail);
			}
			return rows;
		}
	}

	public class ProtonBuild
	{
		public string Name, Path, Source;

		public override string ToString() { return Name + " — " + Source; }
	}

	// Installed Proton builds: Steam's compatibilitytools.d folders (native and Flatpak Steam; umu, Faugus Launcher and
	// ProtonUp-Qt download there too), system packages, umu's own folder and Valve's Protons in each Steam library.
	public static class ProtonBuilds
	{
		public static List<ProtonBuild> Find(string home, string dataHome)
		{
			List<ProtonBuild> result = new List<ProtonBuild>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			string[] flatpakSteam = new string[] { System.IO.Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"), System.IO.Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam") };
			string[] steamRoots = new string[] { System.IO.Path.Combine(dataHome, "Steam"), System.IO.Path.Combine(home, ".steam", "root"), System.IO.Path.Combine(home, ".steam", "steam") };
			foreach (string steam in steamRoots) Add(result, seen, System.IO.Path.Combine(steam, "compatibilitytools.d"), "Steam compatibility tools");
			foreach (string steam in flatpakSteam) Add(result, seen, System.IO.Path.Combine(steam, "compatibilitytools.d"), "Flatpak Steam compatibility tools");
			Add(result, seen, System.IO.Path.Combine(dataHome, "umu", "compatibilitytools"), "umu");
			foreach (string system in new string[] { "/usr/share/steam/compatibilitytools.d", "/usr/local/share/steam/compatibilitytools.d" }) Add(result, seen, system, "System package");
			foreach (string steam in steamRoots.Concat(flatpakSteam))
				foreach (string library in Libraries(steam)) Add(result, seen, System.IO.Path.Combine(library, "steamapps", "common"), "Steam library");
			return result;
		}

		private static void Add(List<ProtonBuild> result, HashSet<string> seen, string folder, string source)
		{
			try
			{
				if (!Directory.Exists(folder)) return;
				foreach (string directory in Directory.GetDirectories(folder).OrderBy((string d) => d, StringComparer.OrdinalIgnoreCase).Take(200))
				{
					if (File.Exists(System.IO.Path.Combine(directory, "proton")) && seen.Add(Platform.RealPath(directory)))
					{
						ProtonBuild build = new ProtonBuild();
						build.Name = System.IO.Path.GetFileName(directory); build.Path = directory; build.Source = source;
						result.Add(build);
					}
				}
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		// steamapps/libraryfolders.vdf (Valve KeyValues text) lists every Steam library as a "path" value.
		public static List<string> Libraries(string steam)
		{
			List<string> result = new List<string>();
			if (Directory.Exists(steam)) result.Add(steam);
			try
			{
				string file = System.IO.Path.Combine(steam, "steamapps", "libraryfolders.vdf");
				if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return result;
				foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\""))
				{
					string path = match.Groups[1].Value.Replace("\\\\", "\\").Replace("\\\"", "\"");
					if (!result.Contains(path)) result.Add(path);
				}
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
			return result;
		}
	}

	public static partial class UserTools
	{
		// Linux: a profile saved before themes were per profile switches to FishBowl's default look instead of none.
		static partial void AfterSwitch(LibraryData d)
		{
			if (d.Theme == null)
			{
				ThemeSettings theme = new ThemeSettings();
				theme.Name = "FishBowl Water"; theme.AccentColor = "Ocean"; theme.ShowBanner = true; theme.ShowStatusBar = true; theme.UiScalePercent = 100; theme.CustomizationVersion = 3;
				d.Theme = theme;
			}
			GameLists.Ensure(d);
		}
	}

	// FishBowl's command line. These options start or list games and emulators without opening a window.
	public static class CommandLine
	{
		public const string Usage = "Usage: fishbowl [option]\n\n" +
			"  --launch <game or emulator>  Start a game (or an emulator) with its launch profile and options, without\n" +
			"                               opening FishBowl. Matches an exact id first, then a title or name (any case).\n" +
			"  --launch-last                Start the game FishBowl started most recently.\n" +
			"  --list                       Print each game and emulator: kind, id, name and path, separated by tabs.\n" +
			"  --help                       Show this help.\n\n" +
			"With no option, FishBowl opens normally. A game started here is timed when it closes, unless FishBowl is open;\n" +
			"then FishBowl's own window keeps the library. Exit status: 0 on success, 1 when nothing matches or it cannot\n" +
			"start, 2 for a usage error.\n";

		public static bool Requested(string[] args)
		{
			return (args ?? new string[0]).Any((string a) => a == "--launch" || a.StartsWith("--launch=", StringComparison.Ordinal) || a == "--launch-last" || a == "--list" || a == "--help" || a == "-h");
		}

		// The FishBowl window holds this lock while it is open, so the command line never overwrites its library.
		public static FileStream WindowLock()
		{
			try
			{
				Directory.CreateDirectory(Store.DataDirectory);
				return new FileStream(System.IO.Path.Combine(Store.DataDirectory, "window.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			}
			catch (IOException) { return null; }
			catch (UnauthorizedAccessException) { return null; }
		}

		// Returns -1 when no command-line action was requested (open the window), otherwise the exit status.
		public static int Run(string[] args, TextWriter output, TextWriter error)
		{
			args = args ?? new string[0];
			int i = Array.FindIndex(args, (string a) => a == "--launch" || a.StartsWith("--launch=", StringComparison.Ordinal) || a == "--launch-last" || a == "--list" || a == "--help" || a == "-h");
			if (i < 0) return -1;
			string option = args[i];
			if (option == "--help" || option == "-h") { output.Write(Usage); return 0; }
			Store.ShowWarning = delegate(string message) { error.WriteLine(message); };
			LibraryData library = Store.Load();
			if (option == "--list")
			{
				foreach (GameEntry game in library.Games.OrderBy((GameEntry g) => g.Title, StringComparer.OrdinalIgnoreCase)) output.WriteLine("game\t" + (game.Id ?? "") + "\t" + (game.Title ?? "") + "\t" + (game.Path ?? ""));
				foreach (EmulatorProfile emulator in library.Emulators) output.WriteLine("emulator\t" + (emulator.Id ?? "") + "\t" + (emulator.Name ?? "") + "\t" + (emulator.Executable ?? ""));
				return 0;
			}
			if (option == "--launch-last")
			{
				GameEntry last = GamePlay.LastGame(library);
				if (last == null) { error.WriteLine("FishBowl has not started a game yet."); return 1; }
				return LaunchGame(library, last, output, error);
			}
			string target = option.StartsWith("--launch=", StringComparison.Ordinal) ? option.Substring(9) : i + 1 < args.Length ? args[i + 1] : null;
			if (string.IsNullOrWhiteSpace(target)) { error.WriteLine("--launch needs a game or emulator id or name, for example: fishbowl --launch Dolphin"); return 2; }
			string problem;
			object found = Find(library, target, out problem);
			if (found == null) { error.WriteLine(problem); return 1; }
			GameEntry chosen = found as GameEntry;
			return chosen != null ? LaunchGame(library, chosen, output, error) : LaunchEmulator((EmulatorProfile)found, output, error);
		}

		// A game or emulator by exact id, then by title or name (games first). Returns null with a reason.
		public static object Find(LibraryData library, string target, out string problem)
		{
			problem = null;
			target = target.Trim();
			GameEntry byId = library.Games.FirstOrDefault((GameEntry g) => g.Id == target);
			if (byId != null) return byId;
			EmulatorProfile emulatorById = library.Emulators.FirstOrDefault((EmulatorProfile e) => e.Id == target);
			if (emulatorById != null) return emulatorById;
			List<GameEntry> games = library.Games.Where((GameEntry g) => string.Equals((g.Title ?? "").Trim(), target, StringComparison.OrdinalIgnoreCase)).ToList();
			if (games.Count == 1) return games[0];
			if (games.Count > 1) { problem = "Several games are called \"" + target + "\". Use an id instead: " + string.Join(", ", games.Select((GameEntry g) => g.Id).ToArray()); return null; }
			List<EmulatorProfile> named = library.Emulators.Where((EmulatorProfile e) => string.Equals((e.Name ?? "").Trim(), target, StringComparison.OrdinalIgnoreCase)).ToList();
			if (named.Count == 1) return named[0];
			problem = named.Count == 0 ? "No game or emulator in FishBowl has the id or name \"" + target + "\". See fishbowl --list."
				: "Several emulators are called \"" + target + "\". Use an id instead: " + string.Join(", ", named.Select((EmulatorProfile e) => e.Id).ToArray());
			return null;
		}

		private static int LaunchEmulator(EmulatorProfile emulator, TextWriter output, TextWriter error)
		{
			if (EmulatorRuntime.State(emulator.Executable) == RuntimeState.Running)
			{
				output.WriteLine(emulator.Name + " is already running." + (EmulatorRuntime.BringForward(emulator.Executable) ? "" : " Switch to its window to continue."));
				return 0;
			}
			try
			{
				GamePlay.LaunchPlan plan = LinuxLaunch.ForEmulator(emulator, null, LaunchHost.Current());
				using (System.Diagnostics.Process.Start(LinuxLaunch.StartInfo(plan))) { }
				foreach (string note in plan.Notes) error.WriteLine(note);
				Store.Log("Opened " + emulator.Name + " from the command line: " + LinuxLaunch.CommandLine(plan));
				output.WriteLine("Opened " + emulator.Name + ".");
				return 0;
			}
			catch (Exception failure)
			{
				Store.Log("Command-line launch failed: " + failure.Message);
				error.WriteLine("FishBowl could not open " + emulator.Name + ".\n" + failure.Message);
				return 1;
			}
		}

		private static int LaunchGame(LibraryData library, GameEntry game, TextWriter output, TextWriter error)
		{
			GamePlay.LaunchPlan plan;
			try { plan = GamePlay.Prepare(library, game); }
			catch (IOException failure) { error.WriteLine(game.Title + " could not start.\n" + failure.Message); return 1; }
			bool windowOpen = WindowOpen();
			System.Diagnostics.Process process;
			DateTime started = DateTime.UtcNow;
			try { process = System.Diagnostics.Process.Start(LinuxLaunch.StartInfo(plan)); }
			catch (Exception failure)
			{
				Store.Log("Command-line launch failed: " + failure.Message);
				error.WriteLine(game.Title + " could not start.\n" + failure.Message);
				return 1;
			}
			foreach (string note in plan.Notes ?? new List<string>()) error.WriteLine(note);
			Store.Log("Game launched from the command line: " + game.Title + " | " + LinuxLaunch.CommandLine(plan));
			output.WriteLine("Started " + game.Title + ".");
			if (windowOpen || process == null)
			{
				if (process != null) process.Dispose();
				output.WriteLine("FishBowl is open, so this launch is not added to its play history.");
				return 0;
			}
			using (process) process.WaitForExit();
			long seconds = (long)(DateTime.UtcNow - started).TotalSeconds;
			// Record on a fresh copy of the library, and only while FishBowl's window is closed: an open window owns the
			// library and would overwrite anything saved here.
			if (WindowOpen())
			{
				output.WriteLine("FishBowl was opened while " + game.Title + " ran, so this session (" + GamePlay.Duration(seconds) + ") is not added to its play history.");
				return 0;
			}
			LibraryData current = Store.Load();
			GameEntry saved = current.Games.FirstOrDefault((GameEntry g) => g.Id == game.Id);
			if (saved == null) return 0;
			GamePlay.RecordLaunch(current, saved, started);
			PlaySession session = GamePlay.BeginSession(current, saved, plan.Program, true);
			session.StartedAt = started.ToString("o");
			GamePlay.CompleteSession(current, saved, session, seconds);
			Store.Save(current);
			output.WriteLine("Played " + game.Title + " for " + GamePlay.Duration(seconds) + ".");
			return 0;
		}

		private static bool WindowOpen()
		{
			using (FileStream held = WindowLock()) return held == null;
		}
	}
#endif
}
