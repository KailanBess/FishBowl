using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// The game library shared by the Windows and Linux apps: collections, Library filtering and sorting,
// smart lists, the play queue, CSV export, launch paths and play-session bookkeeping.
// Keep this file C# 5 compatible; Linux-only helpers sit behind #if NETCOREAPP.
namespace EmulatorHub
{
	public static class GameCollections
	{
		public static IEnumerable<GameEntry> Entries(GameCollection collection, IEnumerable<GameEntry> games)
		{
			List<GameEntry> source = (games ?? Enumerable.Empty<GameEntry>()).ToList();
			if (collection == null)
			{
				return Enumerable.Empty<GameEntry>();
			}
			if (!collection.IsSmart)
			{
				return source.Where((GameEntry game) => (collection.GameIds ?? new List<string>()).Contains(game.Id));
			}
			switch ((collection.SmartRule ?? "").Trim())
			{
			case "Favorites":
				return source.Where((GameEntry game) => game.Favorite);
			case "Pinned":
				return source.Where((GameEntry game) => game.Pinned);
			case "Playing":
				return source.Where((GameEntry game) => string.Equals(game.PlayStatus, "Playing", StringComparison.OrdinalIgnoreCase));
			case "Completed":
				return source.Where((GameEntry game) => string.Equals(game.PlayStatus, "Completed", StringComparison.OrdinalIgnoreCase));
			case "Recent":
				return (from game in source
					where !string.IsNullOrWhiteSpace(game.LastLaunched)
					orderby GameLibraryQuery.Date(game.LastLaunched) descending
					select game).Take(30);
			case "Missing":
				return source.Where((GameEntry game) => string.IsNullOrWhiteSpace(game.Path) || !File.Exists(game.Path));
			default:
				return Enumerable.Empty<GameEntry>();
			}
		}

		public static readonly string[] SmartRules = new string[6] { "Favorites", "Pinned", "Playing", "Completed", "Recent", "Missing" };

		// Collections in display order: by SortOrder, then name, with children after their parent.
		public static List<KeyValuePair<GameCollection, int>> Tree(LibraryData data)
		{
			List<GameCollection> all = (data.Collections ?? new List<GameCollection>()).ToList();
			HashSet<string> ids = new HashSet<string>(all.Select((GameCollection c) => c.Id ?? ""));
			List<KeyValuePair<GameCollection, int>> result = new List<KeyValuePair<GameCollection, int>>();
			HashSet<string> seen = new HashSet<string>();
			Action<string, int> walk = null;
			walk = delegate(string parent, int depth)
			{
				foreach (GameCollection item in all.Where((GameCollection c) => parent == null ? (string.IsNullOrWhiteSpace(c.ParentId) || !ids.Contains(c.ParentId)) : c.ParentId == parent).OrderBy((GameCollection c) => c.SortOrder).ThenBy((GameCollection c) => c.Name, StringComparer.OrdinalIgnoreCase))
				{
					if (!seen.Add(item.Id ?? "")) continue;
					result.Add(new KeyValuePair<GameCollection, int>(item, depth));
					walk(item.Id, depth + 1);
				}
			};
			walk(null, 0);
			foreach (GameCollection item in all.Where((GameCollection c) => !seen.Contains(c.Id ?? ""))) result.Add(new KeyValuePair<GameCollection, int>(item, 0));
			return result;
		}

		public static void SetParent(LibraryData d, GameCollection child, string parent)
		{
			if (parent == child.Id)
			{
				throw new IOException("A collection cannot contain itself.");
			}
			HashSet<string> visited = new HashSet<string>();
			visited.Add(child.Id);
			string next = parent;
			while (!string.IsNullOrWhiteSpace(next))
			{
				if (!visited.Add(next))
				{
					throw new IOException("That parent would create a collection cycle.");
				}
				string current = next;
				GameCollection found = d.Collections.FirstOrDefault((GameCollection x) => x.Id == current);
				if (found == null)
				{
					throw new IOException("Parent collection is unavailable.");
				}
				next = found.ParentId;
			}
			child.ParentId = string.IsNullOrWhiteSpace(parent) ? null : parent;
		}

		// Deletes a collection; its child collections move up to its parent. Games stay in the library.
		public static void Delete(LibraryData d, GameCollection collection)
		{
			foreach (GameCollection child in d.Collections.Where((GameCollection c) => c.ParentId == collection.Id)) child.ParentId = collection.ParentId;
			d.Collections.Remove(collection);
		}
	}

	public static class GameLibraryQuery
	{
		public static readonly string[] Scopes = new string[7] { "All games", "Pinned", "Favorites", "Playing", "Completed", "Recent", "Missing" };
		public static readonly string[] Sorts = new string[4] { "Title", "Last played", "Added", "Play time" };
		public static readonly string[] SaveFilters = new string[5] { "All save statuses", "Has saves", "No saves", "Missing saves", "Missing games" };
		public static readonly string[] Progress = new string[5] { "Not started", "Playing", "Completed", "On hold", "Dropped" };
		public const string CollectionPrefix = "Collection: ";

		public static string SearchText(GameEntry g)
		{
			return string.Join(" ", g.Title, g.Path, g.Genre, g.Developer, g.ReleaseYear, g.Description, g.Notes, g.ConsoleLabel, g.PlayStatus, string.Join(" ", g.Tags ?? new List<string>()), (g.Extras == null || g.Extras.Fields == null) ? "" : string.Join(" ", g.Extras.Fields.Select((KeyValuePair<string, string> p) => p.Key + " " + p.Value)));
		}

		public static bool Matches(GameEntry g, string search)
		{
			search = (search ?? "").Trim();
			return search.Length == 0 || SearchText(g).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
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

		public static string ConsoleName(GameEntry game)
		{
			return (!string.IsNullOrWhiteSpace(game.ConsoleLabel)) ? game.ConsoleLabel : GameStorage.ConsoleFor(game.Path ?? "");
		}

		public static bool Native(GameEntry game)
		{
			return game != null && game.Extras != null && game.Extras.Native;
		}

		// The emulator a game launches with, ignoring the preferred build; null for native games and games awaiting reassignment.
		public static EmulatorProfile EmulatorFor(LibraryData library, GameEntry game)
		{
			if (game == null || game.RequiresEmulatorAssignment || Native(game)) return null;
			string id = string.IsNullOrWhiteSpace(game.PreferredEmulatorId) ? game.EmulatorId : game.PreferredEmulatorId;
			List<EmulatorProfile> emulators = library.Emulators ?? new List<EmulatorProfile>();
			return emulators.FirstOrDefault((EmulatorProfile item) => item.Id == id) ?? ((emulators.Count == 1) ? emulators[0] : null);
		}

		public static List<string> ScopeChoices(LibraryData library)
		{
			return Scopes.Concat((library.Collections ?? new List<GameCollection>()).OrderBy((GameCollection c) => c.Name, StringComparer.OrdinalIgnoreCase).Select((GameCollection c) => CollectionPrefix + c.Name)).ToList();
		}

		public static List<string> ConsoleChoices(LibraryData library)
		{
			return new string[1] { "All consoles" }.Concat(library.Games.Select(ConsoleName).Distinct().OrderBy((string s) => s)).ToList();
		}

		public static List<string> EmulatorChoices(LibraryData library)
		{
			return new string[2] { "All emulators", "Unassigned" }.Concat(library.Emulators.Select((EmulatorProfile p) => p.Name)).ToList();
		}

		public static IEnumerable<GameEntry> Scope(LibraryData library, IEnumerable<GameEntry> games, string scope)
		{
			scope = scope ?? "All games";
			if (scope == "Pinned") return games.Where((GameEntry item) => item.Pinned);
			if (scope == "Favorites") return games.Where((GameEntry item) => item.Favorite);
			if (scope == "Playing") return games.Where((GameEntry item) => string.Equals(item.PlayStatus, "Playing", StringComparison.OrdinalIgnoreCase));
			if (scope == "Completed") return games.Where((GameEntry item) => string.Equals(item.PlayStatus, "Completed", StringComparison.OrdinalIgnoreCase));
			if (scope == "Recent") return games.Where((GameEntry item) => !string.IsNullOrWhiteSpace(item.LastLaunched)).OrderByDescending((GameEntry item) => Date(item.LastLaunched)).Take(30);
			if (scope == "Missing") return games.Where((GameEntry item) => !File.Exists(item.Path));
			if (scope.StartsWith(CollectionPrefix, StringComparison.OrdinalIgnoreCase))
			{
				GameCollection collection = (library.Collections ?? new List<GameCollection>()).FirstOrDefault((GameCollection item) => CollectionPrefix + item.Name == scope);
				if (collection != null) return NestedEntries(library, collection, games);
			}
			return games;
		}

		public static IEnumerable<GameEntry> Filter(LibraryData library, IEnumerable<GameEntry> items, string console, string emulator, string saves, string tags)
		{
			if (!string.IsNullOrEmpty(console) && console != "All consoles")
			{
				items = items.Where((GameEntry g) => ConsoleName(g) == console);
			}
			if (!string.IsNullOrEmpty(emulator) && emulator != "All emulators")
			{
				items = items.Where(delegate(GameEntry g)
				{
					EmulatorProfile profile = EmulatorFor(library, g);
					return (emulator == "Unassigned") ? (profile == null) : (profile != null && profile.Name == emulator);
				});
			}
			if (!string.IsNullOrEmpty(saves) && saves != "All save statuses")
			{
				items = items.Where(delegate(GameEntry g)
				{
					List<GameSaveEntry> list = g.Saves ?? new List<GameSaveEntry>();
					return (saves == "Has saves") ? list.Any((GameSaveEntry s) => File.Exists(s.Path) || Directory.Exists(s.Path)) : ((saves == "No saves") ? (list.Count == 0) : ((saves == "Missing games") ? (!File.Exists(g.Path)) : list.Any((GameSaveEntry s) => !File.Exists(s.Path) && !Directory.Exists(s.Path))));
				});
			}
			string[] wanted = (from t in (tags ?? "").Split(',')
				select t.Trim() into t
				where t.Length > 0
				select t).ToArray();
			if (wanted.Length > 0)
			{
				items = items.Where((GameEntry g) => wanted.All((string t) => (g.Tags ?? new List<string>()).Any((string saved) => saved.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)));
			}
			return items;
		}

		public static IEnumerable<GameEntry> Sort(IEnumerable<GameEntry> items, string sort)
		{
			if (sort == "Last played") return items.OrderByDescending((GameEntry g) => Date(g.LastLaunched));
			if (sort == "Added") return items.OrderByDescending((GameEntry g) => Date(g.AddedAt));
			if (sort == "Play time") return items.OrderByDescending((GameEntry g) => g.TotalPlaySeconds);
			return items.OrderBy((GameEntry g) => g.Title, StringComparer.CurrentCultureIgnoreCase);
		}

		// Reads both the ISO dates Linux writes and the local "g" dates Windows writes.
		public static DateTime Date(string text)
		{
			DateTime result;
			if (string.IsNullOrWhiteSpace(text)) return DateTime.MinValue;
			text = text.Trim();
			bool iso = text.Length >= 10 && text[4] == '-' && text[7] == '-';
			if (iso && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)) return result.Kind == DateTimeKind.Utc ? result.ToLocalTime() : result;
			return DateTime.TryParse(text, out result) ? result : DateTime.MinValue;
		}
	}

	// Smart lists, the play queue, Surprise me and CSV export.
	public static class GameLists
	{
		public static void Ensure(LibraryData data)
		{
			if (data.SmartLists == null)
			{
				data.SmartLists = new List<SmartLibraryList>();
			}
			if (data.PlayQueue == null)
			{
				data.PlayQueue = new List<string>();
			}
		}

		public static List<GameEntry> Match(LibraryData data, SmartLibraryList rule)
		{
			return (from g in data.Games
				where (string.IsNullOrWhiteSpace(rule.Search) || GameLibraryQuery.SearchText(g).IndexOf(rule.Search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) && (string.IsNullOrWhiteSpace(rule.Platform) || rule.Platform == "Any" || string.Equals(g.ConsoleLabel, rule.Platform, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(rule.Status) || rule.Status == "Any" || string.Equals(g.PlayStatus, rule.Status, StringComparison.OrdinalIgnoreCase)) && (!rule.FavoritesOnly || g.Favorite) && (!rule.UnplayedOnly || g.LaunchCount == 0)
				orderby g.Title
				select g).ToList();
		}

		public static List<GameEntry> QueueGames(LibraryData data)
		{
			Ensure(data);
			return (from id in data.PlayQueue
				select data.Games.FirstOrDefault((GameEntry g) => g.Id == id) into g
				where g != null
				select g).ToList();
		}

		public static void Enqueue(LibraryData data, GameEntry game)
		{
			Ensure(data);
			if (game != null && !data.PlayQueue.Contains(game.Id))
			{
				data.PlayQueue.Add(game.Id);
			}
		}

		public static bool MoveQueue(LibraryData data, string id, int delta)
		{
			Ensure(data);
			int from = data.PlayQueue.IndexOf(id);
			int to = from + delta;
			if (from < 0 || to < 0 || to >= data.PlayQueue.Count)
			{
				return false;
			}
			data.PlayQueue.RemoveAt(from);
			data.PlayQueue.Insert(to, id);
			return true;
		}

		// Games Surprise me can pick from: present files that pass the caller's launch check.
		public static List<GameEntry> SurpriseCandidates(LibraryData data, bool unplayed, Func<GameEntry, bool> launchable)
		{
			return data.Games.Where((GameEntry g) => (!unplayed || g.LaunchCount == 0) && File.Exists(g.Path)).Where((GameEntry g) => launchable == null || launchable(g)).ToList();
		}

		public static string CsvCell(string text)
		{
			text = text ?? "";
			string trimmed = text.TrimStart();
			if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
			{
				text = "'" + text;
			}
			return "\"" + text.Replace("\"", "\"\"") + "\"";
		}

		public static string Csv(IEnumerable<GameEntry> games)
		{
			StringBuilder builder = new StringBuilder("Title,Platform,Genre,Developer,Year,Progress,Favorite,Launches,Play hours,Last launched,Path,Tags\r\n");
			foreach (GameEntry game in games)
			{
				builder.Append(string.Join(",", new string[12]
				{
					game.Title,
					game.ConsoleLabel,
					game.Genre,
					game.Developer,
					game.ReleaseYear,
					game.PlayStatus,
					game.Favorite ? "Yes" : "No",
					game.LaunchCount.ToString(CultureInfo.InvariantCulture),
					((double)game.TotalPlaySeconds / 3600.0).ToString("0.00", CultureInfo.InvariantCulture),
					game.LastLaunched,
					game.Path,
					string.Join("; ", game.Tags ?? new List<string>())
				}.Select(CsvCell).ToArray()));
				builder.Append("\r\n");
			}
			return builder.ToString();
		}
	}

	// Launch paths and play-session bookkeeping shared by both apps.
	public static class GamePlay
	{
		// The disc to launch: the last chosen disc of a multi-disc game, otherwise the game file.
		public static string LaunchPath(GameEntry game)
		{
			if (!string.IsNullOrWhiteSpace(game.LastDiscPath) && (game.Discs ?? new List<string>()).Contains(game.LastDiscPath, StringComparer.OrdinalIgnoreCase))
			{
				return game.LastDiscPath;
			}
			return game.Path;
		}

		// Records a launch on the game itself (LastLaunched and LaunchCount).
		public static void RecordLaunch(LibraryData data, GameEntry game, DateTime now)
		{
			game.LastLaunched = now.ToUniversalTime().ToString("o");
			game.LaunchCount++;
			if (data.Enhancements == null) data.Enhancements = new NextSettings();
			data.Enhancements.LastGameId = game.Id;
		}

		public static PlaySession BeginSession(LibraryData data, GameEntry game, string executable, bool tracked)
		{
			if (data.PlaySessions == null) data.PlaySessions = new List<PlaySession>();
			if (data.Enhancements == null) data.Enhancements = new NextSettings();
			PlaySession session = new PlaySession();
			session.Id = Guid.NewGuid().ToString("N");
			session.GameId = game.Id;
			session.StartedAt = DateTime.UtcNow.ToString("o");
			session.Uncertain = !tracked;
			session.EmulatorPath = executable;
			session.DiscPath = LaunchPath(game);
			session.Profile = game.LaunchProfileName;
			session.Note = (tracked ? "Emulator process time; game changes inside the process cannot be distinguished." : "Wrapper launch: duration unavailable; you can enter it manually.");
			data.PlaySessions.Add(session);
			data.Enhancements.LastGameId = game.Id;
			return session;
		}

		public static void CompleteSession(LibraryData data, GameEntry game, PlaySession session, long seconds)
		{
			session.EndedAt = DateTime.UtcNow.ToString("o");
			session.Seconds = Math.Max(0L, seconds);
			game.TotalPlaySeconds += session.Seconds;
		}

		public static void CorrectSession(LibraryData data, PlaySession session, long seconds, string note)
		{
			if (seconds < 0 || seconds > 31536000)
			{
				throw new ArgumentOutOfRangeException("seconds");
			}
			GameEntry game = data.Games.FirstOrDefault((GameEntry g) => g.Id == session.GameId);
			if (game != null)
			{
				game.TotalPlaySeconds = Math.Max(0L, game.TotalPlaySeconds - session.Seconds + seconds);
			}
			session.Seconds = seconds;
			session.Note = note;
			session.Corrected = true;
			if (session.EndedAt == null)
			{
				session.EndedAt = DateTime.UtcNow.ToString("o");
			}
		}

		// Adds a session the player enters by hand (for example a launcher FishBowl could not track).
		public static PlaySession AddManualSession(LibraryData data, GameEntry game, DateTime startedLocal, long seconds, string note)
		{
			if (seconds <= 0 || seconds > 31536000)
			{
				throw new ArgumentOutOfRangeException("seconds");
			}
			if (data.PlaySessions == null) data.PlaySessions = new List<PlaySession>();
			PlaySession session = new PlaySession();
			session.Id = Guid.NewGuid().ToString("N");
			session.GameId = game.Id;
			session.StartedAt = startedLocal.ToUniversalTime().ToString("o");
			session.EndedAt = startedLocal.ToUniversalTime().AddSeconds(seconds).ToString("o");
			session.Seconds = seconds;
			session.Corrected = true;
			session.Note = string.IsNullOrWhiteSpace(note) ? "Entered manually." : note;
			data.PlaySessions.Add(session);
			game.TotalPlaySeconds += seconds;
			return session;
		}

		public static Dictionary<string, long> WeekTotals(LibraryData data, DateTime now)
		{
			Dictionary<string, long> totals = new Dictionary<string, long>();
			for (int day = 6; day >= 0; day--)
			{
				totals[now.Date.AddDays(-day).ToString("yyyy-MM-dd")] = 0L;
			}
			foreach (PlaySession session in data.PlaySessions ?? new List<PlaySession>())
			{
				DateTime started = GameLibraryQuery.Date(session.StartedAt);
				if (started != DateTime.MinValue)
				{
					string key = started.ToString("yyyy-MM-dd");
					if (totals.ContainsKey(key))
					{
						totals[key] += session.Seconds;
					}
				}
			}
			return totals;
		}

		public static List<PlaySession> SessionsFor(LibraryData data, GameEntry game)
		{
			return (data.PlaySessions ?? new List<PlaySession>()).Where((PlaySession s) => game == null || s.GameId == game.Id).OrderByDescending((PlaySession s) => GameLibraryQuery.Date(s.StartedAt)).ToList();
		}

		// Recently played games for Home, newest first, skipping removed games.
		public static List<GameEntry> Recent(LibraryData data, int count)
		{
			return data.Games.Where((GameEntry g) => !string.IsNullOrWhiteSpace(g.LastLaunched)).OrderByDescending((GameEntry g) => GameLibraryQuery.Date(g.LastLaunched)).Take(count).ToList();
		}

		public static GameEntry LastGame(LibraryData data)
		{
			string id = data.Enhancements == null ? null : data.Enhancements.LastGameId;
			GameEntry game = data.Games.FirstOrDefault((GameEntry g) => g.Id == id);
			if (game != null) return game;
			List<GameEntry> recent = Recent(data, 1);
			return recent.Count == 0 ? null : recent[0];
		}

		public static string Duration(long seconds)
		{
			if (seconds < 60) return seconds <= 0 ? "0 min" : "<1 min";
			long hours = seconds / 3600, minutes = (seconds % 3600) / 60;
			return hours == 0 ? (minutes + " min") : (hours + " h " + minutes.ToString("00") + " min");
		}

#if NETCOREAPP
		// What starts a game on Linux: the program, its separate arguments and the working folder.
		public class LaunchPlan
		{
			public string Program { get; set; }
			public List<string> Arguments { get; set; }
			public string WorkingDirectory { get; set; }
			public string GamePath { get; set; }
			public EmulatorProfile Emulator { get; set; }
		}

		// The emulator with the game's preferred build applied (a copy when the build differs).
		public static EmulatorProfile LaunchEmulator(LibraryData library, GameEntry game)
		{
			EmulatorProfile emulator = GameLibraryQuery.EmulatorFor(library, game);
			if (emulator == null) return null;
			EmulatorBuild build = (emulator.Builds ?? new List<EmulatorBuild>()).FirstOrDefault((EmulatorBuild b) => b.Id == game.PreferredBuildId);
			if (build == null || !File.Exists(build.Executable)) return emulator;
			EmulatorProfile copy = Json.Deserialize<EmulatorProfile>(Json.Serialize(emulator));
			copy.Executable = build.Executable;
			copy.ManualVersion = build.ManualVersion;
			return copy;
		}

		// Validates a game and works out how to start it. Throws IOException with a user-facing message.
		public static LaunchPlan Prepare(LibraryData library, GameEntry game)
		{
			string path = LaunchPath(game);
			if (GameLibraryQuery.Native(game) || (GameLibraryQuery.EmulatorFor(library, game) == null && !game.RequiresEmulatorAssignment && Platform.IsLaunchFile(game.Path)))
			{
				if (!File.Exists(game.Path)) throw new IOException("The game or shortcut is missing. Edit its location or use Library repair.");
				if ((game.Arguments ?? "").Contains("{") || (game.Arguments ?? "").Contains("}")) throw new IOException("Native game arguments must be literal text; templates are not supported.");
				string folder = game.Extras == null ? null : game.Extras.WorkingDirectory;
				if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder)) throw new IOException("The game's working folder is missing.");
				LaunchPlan native = new LaunchPlan();
				native.Program = game.Path; native.GamePath = game.Path; native.Arguments = Platform.SplitArguments(game.Arguments);
				native.WorkingDirectory = string.IsNullOrWhiteSpace(folder) ? Path.GetDirectoryName(game.Path) : folder;
				return native;
			}
			EmulatorProfile emulator = LaunchEmulator(library, game);
			if (emulator == null) throw new IOException("Assign an available emulator.");
			if (!string.IsNullOrWhiteSpace(game.PreferredBuildId) && !(emulator.Builds ?? new List<EmulatorBuild>()).Any((EmulatorBuild b) => b.Id == game.PreferredBuildId))
				throw new IOException("The selected emulator build no longer exists. Choose another build in Game setup.");
			if (string.IsNullOrWhiteSpace(emulator.Executable) || !File.Exists(emulator.Executable)) throw new IOException("The emulator program for " + (emulator.Name ?? "this game") + " is missing. Edit the emulator or choose another one.");
			bool package = InstalledGames.IsPackage(emulator, path);
			if (!File.Exists(path) && (!package || InstalledGames.Find(emulator, game.TitleId, path) == null)) throw new IOException("The game file is missing. Edit its location or use Library repair.");
			string profileArguments = "";
			if (!string.IsNullOrWhiteSpace(game.LaunchProfileName))
			{
				LaunchProfile profile = (emulator.LaunchProfiles ?? new List<LaunchProfile>()).FirstOrDefault((LaunchProfile p) => p.Name == game.LaunchProfileName);
				if (profile == null) throw new IOException("The game's launch profile no longer exists. Choose another profile in Game setup.");
				profileArguments = profile.Arguments;
			}
			string template = string.Join(" ", new string[3] { emulator.Arguments, profileArguments, game.Arguments }.Where((string s) => !string.IsNullOrWhiteSpace(s)).ToArray()).Replace("{rom}", "{game}");
			string installed = InstalledGames.ResolveArguments(emulator, game, template, path);
			List<string> arguments = Platform.SplitArguments(installed ?? template);
			bool hasGame = arguments.Any((string a) => a.Contains("{game}"));
			arguments = arguments.Select((string a) => a.Replace("{game}", path)).ToList();
			if (installed == null && !hasGame) arguments.Add(path);
			if (arguments.Any((string a) => a.Contains("{") || a.Contains("}"))) throw new IOException("Unsupported argument template. Use {game} for the game path.");
			LaunchPlan plan = new LaunchPlan();
			plan.Program = emulator.Executable; plan.Arguments = arguments; plan.GamePath = path; plan.Emulator = emulator;
			string working = game.Extras == null ? null : game.Extras.WorkingDirectory;
			plan.WorkingDirectory = Directory.Exists(working) ? working : Path.GetDirectoryName(emulator.Executable);
			return plan;
		}
#endif
	}
}
