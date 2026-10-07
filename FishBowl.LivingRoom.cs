using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Living-room Library and Immersion logic shared by the Windows and Linux builds: settings, shelves,
// presets, interface sounds, session recaps and couch grid navigation. No UI types; C# 5 only.
namespace EmulatorHub
{
	public static partial class Immersion
	{
		public static readonly string[] Shelves = new string[4] { "All games", "Continue playing", "Recently added", "Favorites" };

		public static readonly string[] Presets = new string[4] { "Current", "Arcade", "Minimal", "Retro" };

		public static ImmersionSettings Ensure(LibraryData d)
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
			if (hub.Immersion == null)
			{
				hub.Immersion = new ImmersionSettings();
			}
			return hub.Immersion;
		}

		public static string ArtworkStamp(string path)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(path);
				return fileInfo.Exists ? (path + "|" + fileInfo.Length + "|" + fileInfo.LastWriteTimeUtc.Ticks) : (path ?? "");
			}
			catch
			{
				return path ?? "";
			}
		}

		public static bool Animate(LibraryData d)
		{
			return Ensure(d).Transitions && (d.Enhancements == null || !d.Enhancements.ReducedMotion);
		}

		public static DateTime Date(string text)
		{
			DateTime result;
			return DateTime.TryParse(text, out result) ? result : DateTime.MinValue;
		}

		public static List<GameEntry> Shelf(LibraryData d, string shelf)
		{
			IEnumerable<GameEntry> games = d.Games;
			if (shelf == "Favorites")
			{
				games = from g in games where g.Favorite orderby g.Title select g;
			}
			else if (shelf == "Recently added")
			{
				games = games.OrderByDescending(delegate(GameEntry g) { return Date(g.AddedAt); });
			}
			else
			{
				games = from g in games
					where g.LaunchCount > 0 && !string.Equals(g.PlayStatus, "Completed", StringComparison.OrdinalIgnoreCase)
					orderby Date(g.LastLaunched) descending
					select g;
			}
			return games.Take(40).ToList();
		}

		public static byte[] Wave(int volume, bool launch)
		{
			volume = Math.Max(0, Math.Min(100, volume));
			int num = 22050 * (launch ? 180 : 65) / 1000;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
					binaryWriter.Write(36 + num * 2);
					binaryWriter.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
					binaryWriter.Write(16);
					binaryWriter.Write((short)1);
					binaryWriter.Write((short)1);
					binaryWriter.Write(22050);
					binaryWriter.Write(44100);
					binaryWriter.Write((short)2);
					binaryWriter.Write((short)16);
					binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
					binaryWriter.Write(num * 2);
					for (int i = 0; i < num; i++)
					{
						double num2 = Math.Sin(Math.PI * (double)i / (double)num);
						double num3 = (launch ? ((i < num / 2) ? 440 : 660) : 520);
						binaryWriter.Write((short)(Math.Sin(Math.PI * 2.0 * num3 * (double)i / 22050.0) * num2 * 5000.0 * (double)volume / 100.0));
					}
					return memoryStream.ToArray();
				}
			}
		}

		public static void ApplyPreset(LibraryData d, string preset)
		{
			if (preset == "Current")
			{
				return;
			}
			if (!new string[3] { "Arcade", "Minimal", "Retro" }.Contains(preset))
			{
				throw new ArgumentException("Unknown immersion preset.");
			}
			if (d.Cosmetics == null)
			{
				d.Cosmetics = new CosmeticSettings();
			}
			CosmeticSettings cosmetics = d.Cosmetics;
			cosmetics.CustomPalette = true;
			cosmetics.TextColor = "#F4F5F7";
			cosmetics.MutedColor = "#CBD2DC";
			cosmetics.SelectionColor = "";
			cosmetics.FocusColor = "";
			cosmetics.ArtworkFrame = ((preset == "Minimal") ? "None" : "Rounded");
			cosmetics.LibrarySpacing = ((preset == "Minimal") ? "Compact" : "Comfortable");
			switch (preset)
			{
			case "Arcade":
				cosmetics.TopColor = "#20112C";
				cosmetics.BottomColor = "#110D19";
				cosmetics.SurfaceColor = "#2B2038";
				cosmetics.AccentColor = "#DA9CFA";
				cosmetics.SecondaryColor = "#9BCDF8";
				cosmetics.BackgroundStyle = "Gradient";
				break;
			case "Retro":
				cosmetics.TopColor = "#29251D";
				cosmetics.BottomColor = "#15140F";
				cosmetics.SurfaceColor = "#353129";
				cosmetics.AccentColor = "#E7C67F";
				cosmetics.SecondaryColor = "#AFCEAA";
				cosmetics.BackgroundStyle = "Dots";
				break;
			default:
				cosmetics.TopColor = "#20242B";
				cosmetics.BottomColor = "#14171C";
				cosmetics.SurfaceColor = "#292F37";
				cosmetics.AccentColor = "#B9D6ED";
				cosmetics.SecondaryColor = "#C5CDD9";
				cosmetics.BackgroundStyle = "Plain";
				break;
			}
		}

		// The session recap is offered only for a verified (certain) session when the user turned recaps on.
		public static bool OfferRecap(LibraryData d, PlaySession session)
		{
			return session != null && !session.Uncertain && Ensure(d).Recap;
		}

		// Records the optional recap note and rating. The caller saves the library.
		public static void SaveRecap(GameEntry g, PlaySession session, string note, int rating)
		{
			note = (note ?? "").Trim();
			if (note.Length > 2000)
			{
				throw new ArgumentException("Session notes must be at most 2000 characters.");
			}
			g.PersonalRating = Math.Max(0, Math.Min(5, rating));
			if (note.Length > 0)
			{
				session.Note = (string.IsNullOrWhiteSpace(session.Note) ? note : (session.Note + Environment.NewLine + note));
				g.Notes = (g.Notes ?? "").TrimEnd() + Environment.NewLine + "[" + DateTime.Now.ToString("g") + "] " + note;
			}
		}

		// Ambient artwork starts after the configured idle minutes, only when the user enabled it.
		public static bool AmbientDue(LibraryData d, DateTime lastInputUtc, DateTime nowUtc)
		{
			ImmersionSettings settings = Ensure(d);
			return settings.Ambient && (nowUtc - lastInputUtc).TotalMinutes >= Math.Max(1, settings.IdleMinutes);
		}

		public static List<GameEntry> AmbientGames(LibraryData d)
		{
			return (from g in d.Games
				where !string.IsNullOrWhiteSpace(g.ArtworkPath) && File.Exists(g.ArtworkPath)
				orderby g.Title
				select g).Take(200).ToList();
		}
	}

	// Every living-room input (keyboard, mouse, controller) becomes one of these.
	public enum CouchAction { None, Up, Down, Left, Right, Confirm, Back, Favorite, Details, PreviousShelf, NextShelf, First, Last, Search, Menu }

	public static class LivingRoom
	{
		public static string SearchText(GameEntry g)
		{
			return string.Join(" ", g.Title, g.Path, g.Genre, g.Developer, g.ReleaseYear, g.Description, g.Notes, g.ConsoleLabel, g.PlayStatus, string.Join(" ", g.Tags ?? new List<string>()), (g.Extras == null || g.Extras.Fields == null) ? "" : string.Join(" ", g.Extras.Fields.Select(delegate(KeyValuePair<string, string> p) { return p.Key + " " + p.Value; })));
		}

		// The games a living-room shelf shows: All games by title, the other shelves as Immersion defines them.
		public static List<GameEntry> Games(LibraryData d, string shelf, string search)
		{
			List<GameEntry> source = (string.IsNullOrEmpty(shelf) || shelf == "All games")
				? d.Games.OrderBy(delegate(GameEntry g) { return g.Title; }, StringComparer.OrdinalIgnoreCase).ToList()
				: Immersion.Shelf(d, shelf);
			string text = (search ?? "").Trim();
			if (text.Length == 0)
			{
				return source;
			}
			return source.Where(delegate(GameEntry g) { return SearchText(g).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0; }).ToList();
		}

		public static string NextShelf(string shelf, bool forward)
		{
			int index = Array.IndexOf(Immersion.Shelves, shelf);
			if (index < 0)
			{
				index = 0;
			}
			int count = Immersion.Shelves.Length;
			return Immersion.Shelves[(index + (forward ? 1 : count - 1)) % count];
		}

		// The column count whose tiles come closest to the preferred width.
		public static int Columns(double width, double preferred, double spacing)
		{
			if (double.IsNaN(width) || double.IsInfinity(width) || width <= preferred)
			{
				return 1;
			}
			return Math.Max(1, (int)Math.Round((width + spacing) / (preferred + spacing), MidpointRounding.AwayFromZero));
		}

		public static int Rows(int count, int columns)
		{
			return (count <= 0) ? 0 : ((count + Math.Max(1, columns) - 1) / Math.Max(1, columns));
		}

		// Left/right follow reading order and wrap at the ends; up/down keep the column and stop at the first and
		// last rows, except that moving down into a shorter last row lands on its last tile.
		public static int Move(int index, int count, int columns, CouchAction action)
		{
			if (count <= 0)
			{
				return -1;
			}
			columns = Math.Max(1, columns);
			if (index < 0 || index >= count)
			{
				return 0;
			}
			switch (action)
			{
			case CouchAction.Left:
				return (index > 0) ? (index - 1) : (count - 1);
			case CouchAction.Right:
				return (index < count - 1) ? (index + 1) : 0;
			case CouchAction.Up:
				return (index - columns >= 0) ? (index - columns) : index;
			case CouchAction.Down:
				if (index + columns < count)
				{
					return index + columns;
				}
				return (index / columns < Rows(count, columns) - 1) ? (count - 1) : index;
			case CouchAction.First:
				return 0;
			case CouchAction.Last:
				return count - 1;
			default:
				return index;
			}
		}

		public static string PlayTime(long seconds)
		{
			TimeSpan time = TimeSpan.FromSeconds(Math.Max(0L, seconds));
			if (time.TotalMinutes < 1.0)
			{
				return "No play time recorded";
			}
			if (time.TotalHours < 1.0)
			{
				return (int)time.TotalMinutes + " min played";
			}
			return time.TotalHours.ToString("0.0") + " hours played";
		}
	}
}
