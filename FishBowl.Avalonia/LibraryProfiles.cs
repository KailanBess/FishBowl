using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace EmulatorHub { internal static class LibraryProfiles {
public static int ActiveLaunches;
        public static T Copy<T>(T value) { return Json.Deserialize<T>(Json.Serialize(value)); }
        public static UserToolSettings Ensure(LibraryData d)
        {
            if (d.UserTools == null) d.UserTools = new UserToolSettings();
            if (d.UserTools.Users == null) d.UserTools.Users = new List<BowlUser>();
            if (d.UserTools.Users.Count == 0) { var user = Capture(d, "Default"); d.UserTools.Users.Add(user); d.UserTools.ActiveId = user.Id; }
            return d.UserTools;
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
			if (ActiveLaunches > 0)
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
			d.Theme = Copy(bowlUser.Theme) ?? new ThemeSettings { Name = "FishBowl Water", AccentColor = "Ocean", ShowBanner = true, ShowStatusBar = true, UiScalePercent = 100, CustomizationVersion = 3 };
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


		}

		} }
