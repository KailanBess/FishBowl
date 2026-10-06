using System;
using System.Collections.Generic;

// FishBowl's saved data (library.json), shared by the Windows (windows/) and Linux (FishBowl.Avalonia/) apps.
// Keep this file C# 5 compatible, add fields as optional, and never rename or remove one.
namespace EmulatorHub
{
	public class BowlUser
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public Dictionary<string, LibraryNavigation> Views { get; set; }

		public LibraryNavigation Navigation { get; set; }

		public List<SaveRouting> SaveRoutes { get; set; }

		public string Id { get; set; }

		public string Name { get; set; }

		public List<PersonalGame> Games { get; set; }

		public ThemeSettings Theme { get; set; }

		public CosmeticSettings Cosmetics { get; set; }

		public NextSettings Enhancements { get; set; }

		public ExperienceSettings Experience { get; set; }

		public List<PlaySession> Sessions { get; set; }

		public List<string> Queue { get; set; }

		public List<SmartLibraryList> Lists { get; set; }

		public int WeeklyMinutes { get; set; }

		public int BreakMinutes { get; set; }

		public bool Controller { get; set; }
	}
	public class BulkUndoRecord
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string At { get; set; }

		public string UserId { get; set; }

		public List<GameEntry> Before { get; set; }

		public List<GameEntry> After { get; set; }

		public List<GameCollection> CollectionsBefore { get; set; }

		public List<GameCollection> CollectionsAfter { get; set; }
	}
	public class ControllerProfile
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Name { get; set; }

		public string EmulatorId { get; set; }

		public string Notes { get; set; }

		public string UpdatedAt { get; set; }
	}
	public class CosmeticSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string LibrarySpacing { get; set; }

		public bool PlatformLabels { get; set; }

		public bool DetailPreview { get; set; }

		public bool CustomPalette { get; set; }

		public string TextColor { get; set; }

		public string MutedColor { get; set; }

		public string TopColor { get; set; }

		public string BottomColor { get; set; }

		public string SurfaceColor { get; set; }

		public string AccentColor { get; set; }

		public string SecondaryColor { get; set; }

		public int CornerRadius { get; set; }

		public string IconStyle { get; set; }

		public bool IconOnlyToolbars { get; set; }

		public string BackgroundStyle { get; set; }

		public string WallpaperPath { get; set; }

		public string WallpaperLayout { get; set; }

		public int WallpaperOpacity { get; set; }

		public string ArtworkFrame { get; set; }

		public bool ArtworkShadow { get; set; }

		public string SelectionColor { get; set; }

		public string FocusColor { get; set; }

		public int FocusWidth { get; set; }

		public string BadgeStyle { get; set; }

		public string ReadyColor { get; set; }

		public string RunningColor { get; set; }

		public string WarningColor { get; set; }

		public bool ShowFooter { get; set; }

		public CosmeticSettings()
		{
			TextColor = "#F1F7FF";
			MutedColor = "#BED3F0";
			TopColor = "#051F4E";
			BottomColor = "#030D26";
			SurfaceColor = "#0C3B7A";
			AccentColor = "#59BEFF";
			SecondaryColor = "#43DCBB";
			LibrarySpacing = "Comfortable";
			CornerRadius = 6;
			IconStyle = "Outline";
			BackgroundStyle = "Plain";
			WallpaperLayout = "Fill";
			WallpaperOpacity = 15;
			ArtworkFrame = "None";
			FocusWidth = 2;
			BadgeStyle = "Text";
			ShowFooter = true;
		}
	}
	public class EmulatorAdapter
	{
        public string ExtensionId { get; set; }
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Name { get; set; }

		public string Platform { get; set; }

		public string Arguments { get; set; }

		public List<string> Extensions { get; set; }

		public string Website { get; set; }

		public string Repository { get; set; }
	}
	public class EmulatorBuild
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Label { get; set; }

		public string Executable { get; set; }

		public string ManualVersion { get; set; }

		public string AddedAt { get; set; }
	}
	public class EmulatorProfile
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Name { get; set; }

		public string Preset { get; set; }

		public string Executable { get; set; }

		public string IconPath { get; set; }

		public List<string> Extensions { get; set; }

		public string Arguments { get; set; }

		public string ScanFolder { get; set; }

		public string SaveFolder { get; set; }

		public string InGameSaveFolder { get; set; }

		public string SaveStateFolder { get; set; }

		public string BannerPath { get; set; }

		public string AccentColor { get; set; }

		public string LastGameFolder { get; set; }

		public List<LaunchProfile> LaunchProfiles { get; set; }

		public string ControllerProfileNotes { get; set; }

		public bool Favorite { get; set; }

		public string Platform { get; set; }

		public string Notes { get; set; }

		public string ManualVersion { get; set; }

		public string Description { get; set; }

		public string Strengths { get; set; }

		public string Limitations { get; set; }

		public string Requirements { get; set; }

		public string ControllerInfo { get; set; }

		public string WebsiteUrl { get; set; }

		public string DocumentationUrl { get; set; }

		public string CompatibilityUrl { get; set; }

		public string ReleasesUrl { get; set; }

		public string ChangelogUrl { get; set; }

		public string ControllerGuideUrl { get; set; }

		public string TroubleshootingUrl { get; set; }

		public string ConfigFolder { get; set; }

		public string ScreenshotFolder { get; set; }

		public string LogFolder { get; set; }

		public List<EmulatorBuild> Builds { get; set; }

		public string GitHubRepository { get; set; }

		public bool IncludePreviewReleases { get; set; }

		public string LatestReleaseTag { get; set; }

		public string LatestReleaseUrl { get; set; }

		public string LatestReleaseNotes { get; set; }

		public string LastUpdateCheck { get; set; }

		public string FirmwareFolder { get; set; }

		public string AddedAt { get; set; }

		public List<SetupChecklistItem> SetupChecklist { get; set; }

		public string IgnoredReleaseTag { get; set; }
	}
	public class ExperienceSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string StartPage { get; set; }

		public List<string> HomeCards { get; set; }

		public bool CompactHome { get; set; }

		public int HomeTileCount { get; set; }

		public int SnapshotCount { get; set; }

		public int SnapshotDays { get; set; }

		public long SnapshotMegabytes { get; set; }

		public int QuietStartHour { get; set; }

		public int QuietEndHour { get; set; }

		public string SnoozeUntil { get; set; }

		public string LibraryView { get; set; }

		public string LibraryFilter { get; set; }

		public string LibrarySort { get; set; }

		public int ArtworkSize { get; set; }

		public string CloudFolder { get; set; }

		public string CloudProvider { get; set; }

		public string LastCloudBackup { get; set; }

		public string LastSuccessfulBackup { get; set; }

		public int BackupArchiveCount { get; set; }

		public int RestorePointCount { get; set; }

		public List<string> PinnedBackupPaths { get; set; }

		public ExperienceSettings()
		{
			StartPage = "Home";
			HomeCards = new List<string> { "Continue playing", "Pinned games", "Favorite emulators", "Save review", "Attention", "Storage and backups", "Recent activity", "Quick actions" };
			HomeTileCount = 6;
			BackupArchiveCount = 20;
			RestorePointCount = 20;
			PinnedBackupPaths = new List<string>();
			SnapshotCount = 20;
			SnapshotDays = 90;
			SnapshotMegabytes = 1024L;
			QuietStartHour = -1;
			QuietEndHour = -1;
			LibraryView = "Details";
			LibrarySort = "Title";
			ArtworkSize = 96;
		}
	}
	public class GameCollection
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string ParentId { get; set; }

		public string Id { get; set; }

		public string Name { get; set; }

		public List<string> GameIds { get; set; }

		public bool IsSmart { get; set; }

		public string SmartRule { get; set; }

		public int SortOrder { get; set; }
	}
	public class GameConverter
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Name { get; set; }

		public string EmulatorId { get; set; }

		public string InputExtensions { get; set; }

		public string OutputExtension { get; set; }

		public string Mode { get; set; }

		public string Program { get; set; }

		public string Arguments { get; set; }
	}
	public class GameEntry
	{
        public GameToolsSettings Tools { get; set; }

        public bool RequiresEmulatorAssignment { get; set; }
        public bool TitleIsCustom { get; set; }

#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public GameExtras Extras { get; set; }

		public string Id { get; set; }

		public string EmulatorId { get; set; }

		public string Title { get; set; }

		public string Path { get; set; }

		public string ArtworkPath { get; set; }

		public string Arguments { get; set; }

		public string Notes { get; set; }

		public string PreferredEmulatorId { get; set; }

		public bool Favorite { get; set; }

		public string AddedAt { get; set; }

		public string LastLaunched { get; set; }

		public int LaunchCount { get; set; }

		public List<string> Tags { get; set; }

		public string Genre { get; set; }

		public string Developer { get; set; }

		public string Description { get; set; }

		public string ReleaseYear { get; set; }

		public long TotalPlaySeconds { get; set; }

		public string PlayStatus { get; set; }

		public int PersonalRating { get; set; }

		public bool Pinned { get; set; }

		public string CompatibilityNotes { get; set; }

		public string ManualPath { get; set; }

		public List<GameSaveEntry> Saves { get; set; }

		public string SaveCopyPreference { get; set; }

		public string ControllerProfileId { get; set; }

		public string LaunchProfileName { get; set; }

		public List<string> Discs { get; set; }

		public string SessionTrackingNote { get; set; }

		public string ConsoleLabel { get; set; }

		public string EmulatorCore { get; set; }

		public string TitleId { get; set; }

		public string PreferredBuildId { get; set; }

		public string LastDiscPath { get; set; }
	}
	public class GameExtras
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
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
	public class GameSaveEntry
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Path { get; set; }

		public string Kind { get; set; }

		public string AddedAt { get; set; }

		public bool IsFolder { get; set; }
	}
	public class GameScreenshot
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string GameId { get; set; }

		public string Path { get; set; }

		public string AddedAt { get; set; }

		public bool Favorite { get; set; }
	}
	public class HubActivity
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string At { get; set; }

		public string UserId { get; set; }

		public string Text { get; set; }
	}
	public class HubSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public ImmersionSettings Immersion { get; set; }

		public List<EmulatorAdapter> Adapters { get; set; }

		public List<PathBinding> Paths { get; set; }

		public List<HubActivity> Activity { get; set; }

		public Dictionary<string, string> SyncHashes { get; set; }

		public bool ResumeAfterCrash { get; set; }

		public bool WatchReleases { get; set; }

		public string LastReleaseWatchAt { get; set; }

		public Dictionary<string, string> NotifiedReleases { get; set; }

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
	public class ImmersionSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public bool Bubbles { get; set; }

		public bool Roomier { get; set; }

		public bool Backdrops { get; set; }

		public bool Transitions { get; set; }

		public bool Sounds { get; set; }

		public bool LaunchPresentation { get; set; }

		public bool Recap { get; set; }

		public bool Ambient { get; set; }

		public int Volume { get; set; }

		public int IdleMinutes { get; set; }

		public string Preset { get; set; }

		public ImmersionSettings()
		{
			Bubbles = true;
			Roomier = true;
			Volume = 20;
			IdleMinutes = 3;
			Preset = "Current";
			Transitions = true;
		}
	}
	public class LaunchProfile
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Name { get; set; }

		public string Arguments { get; set; }
	}
	public class LibraryData
	{
        public GameToolsLibrarySettings GameTools { get; set; }
        public IntegrationSettings Integrations { get; set; }

        public List<LibraryRemoval> RemovalHistory { get; set; }
        public string SavedDataRoot { get; set; }
        public string SavedPortableRoot { get; set; }
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public HubSettings Hub { get; set; }

		public UserToolSettings UserTools { get; set; }

		public CosmeticSettings Cosmetics { get; set; }

		public List<SmartLibraryList> SmartLists { get; set; }

		public List<string> PlayQueue { get; set; }

		public int Version { get; set; }

		public List<EmulatorProfile> Emulators { get; set; }

		public List<GameEntry> Games { get; set; }

        public List<string> RemovedGamePaths { get; set; }

		public List<WebsiteLink> Links { get; set; }

		public List<GameConverter> Converters { get; set; }

		public List<GameCollection> Collections { get; set; }

		public List<ControllerProfile> ControllerProfiles { get; set; }

		public List<WorkspaceItem> WorkspaceItems { get; set; }

		public List<RequirementFileSnapshot> RequirementSnapshots { get; set; }

		public ThemeSettings Theme { get; set; }

		public MultiplayerSettings Multiplayer { get; set; }

		public string BackupFolder { get; set; }

		public string EmulatorRootDirectory { get; set; }

		public string GameLibraryRoot { get; set; }

		public string RequirementsLibraryRoot { get; set; }

		public ExperienceSettings Experience { get; set; }

		public List<SaveSnapshot> SaveSnapshots { get; set; }

		public List<SaveReviewItem> SaveReviews { get; set; }

		public List<OrganizationTransaction> OrganizationHistory { get; set; }

		public NextSettings Enhancements { get; set; }

		public List<PlaySession> PlaySessions { get; set; }

		public List<GameScreenshot> GameScreenshots { get; set; }
	}
	public class LibraryNavigation
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Search { get; set; }

		public string Scope { get; set; }

		public string Console { get; set; }

		public string Emulator { get; set; }

		public string Saves { get; set; }

		public string Sort { get; set; }

		public string Tags { get; set; }

		public string SelectedId { get; set; }

		public string TopId { get; set; }

		public bool Artwork { get; set; }
	}
	public class MultiplayerSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public bool InviteOnly { get; set; }

		public bool RememberRecentSessions { get; set; }

		public bool ShareDiagnosticsWithHost { get; set; }

		public string RelayProvider { get; set; }

		public string RelayGatewayUrl { get; set; }

		public string UpdateChannel { get; set; }

		public bool CheckForHubUpdates { get; set; }
	}
	public class NextSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public int TextPercent { get; set; }

		public bool ReducedMotion { get; set; }

		public bool StrongFocus { get; set; }

		public Dictionary<string, string> Shortcuts { get; set; }

		public string LastGameId { get; set; }

		public Dictionary<string, WorkspaceMemory> Workspaces { get; set; }

		public int BackupIntervalDays { get; set; }

		public long BackupQuotaMegabytes { get; set; }

		public string NextBackupAt { get; set; }

		public string LastPlannedBackup { get; set; }

		public string UpdateManifestUrl { get; set; }

		public bool EnableJumpList { get; set; }

		public NextSettings()
		{
			TextPercent = 100;
			StrongFocus = true;
			EnableJumpList = true;
			BackupQuotaMegabytes = 4096L;
			Shortcuts = ModelDefaults.Shortcuts();
			Workspaces = new Dictionary<string, WorkspaceMemory>();
		}
	}
	public class OrganizationMove
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Source { get; set; }

		public string Destination { get; set; }

		public bool Copy { get; set; }

		public string GameId { get; set; }

		public string Hash { get; set; }
	}
	public class OrganizationTransaction
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string CreatedAt { get; set; }

		public List<OrganizationMove> Moves { get; set; }

		public bool Undone { get; set; }
	}
	public class PathBinding
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Original { get; set; }

		public string Key { get; set; }

		public string Relative { get; set; }
	}
	public class PersonalGame
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public bool Favorite { get; set; }

		public bool Pinned { get; set; }

		public string PlayStatus { get; set; }

		public int PersonalRating { get; set; }

		public long TotalPlaySeconds { get; set; }

		public int LaunchCount { get; set; }

		public string LastLaunched { get; set; }
	}
	public class PlaySession
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string GameId { get; set; }

		public string StartedAt { get; set; }

		public string EndedAt { get; set; }

		public long Seconds { get; set; }

		public bool Uncertain { get; set; }

		public bool Corrected { get; set; }

		public string Note { get; set; }

		public string EmulatorPath { get; set; }

		public string DiscPath { get; set; }

		public string Profile { get; set; }
	}
	public class RequirementFileSnapshot
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Path { get; set; }

		public long Size { get; set; }

		public string LastWriteUtc { get; set; }
	}
	public class SaveReviewItem
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Path { get; set; }

		public string GameId { get; set; }

		public string ChangedAt { get; set; }

		public List<string> Files { get; set; }
	}
	public class SaveRouting
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string EmulatorId { get; set; }

		public string Mode { get; set; }

		public string Arguments { get; set; }
	}
	public class SaveSnapshot
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string GameId { get; set; }

		public string Source { get; set; }

		public string Path { get; set; }

		public string Kind { get; set; }

		public string CreatedAt { get; set; }

		public string Hash { get; set; }

		public long Bytes { get; set; }

		public bool IsFolder { get; set; }

		public bool Pinned { get; set; }

		public string Note { get; set; }

		public string EmulatorId { get; set; }

		public string EmulatorName { get; set; }

		public string EmulatorVersion { get; set; }

		public string Core { get; set; }
	}
	public class SetupChecklistItem
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Label { get; set; }

		public bool Complete { get; set; }
	}
	public class SmartLibraryList
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Name { get; set; }

		public string Search { get; set; }

		public string Platform { get; set; }

		public string Status { get; set; }

		public bool FavoritesOnly { get; set; }

		public bool UnplayedOnly { get; set; }
	}
	public class ThemeSettings
	{
        public List<string> HomeCardOrder { get; set; }
        public List<string> HiddenHomeCards { get; set; }
        public bool RestrainedAccents { get; set; }
        public string CoverAspect { get; set; }

#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Name { get; set; }

		public int AutoBackupDays { get; set; }

		public string LastBackupAt { get; set; }

		public bool DiscordRichPresenceEnabled { get; set; }

		public bool WelcomeComplete { get; set; }

		public bool ShowStartupAssistant { get; set; }

		public bool StartupAssistantPreferenceSet { get; set; }

		public bool StartMaximized { get; set; }

		public bool AutoSyncGameFolders { get; set; }

		public bool ConfirmBeforeGameLaunch { get; set; }

		public int CustomizationVersion { get; set; }

		public string AccentColor { get; set; }

		public string AppIconColor { get; set; }

		public string FontFamily { get; set; }

		public int UiScalePercent { get; set; }

		public string ListDensity { get; set; }

		public bool ShowBanner { get; set; }

		public bool ShowStatusBar { get; set; }

		public bool ShowInformationPanel { get; set; }

		public bool ShowEmulatorIcons { get; set; }

		public bool EnableMotion { get; set; }

		public bool AlternateRowShading { get; set; }

		public string SelectionContrast { get; set; }

		public string IconTileShape { get; set; }

		public bool ShowGameStorageAssistant { get; set; }

		public bool GameStorageAssistantPreferenceSet { get; set; }

		public bool ShowRequirementsStorageAssistant { get; set; }

		public bool RequirementsStorageAssistantPreferenceSet { get; set; }

		public string HubPerformanceMode { get; set; }

		public bool PauseBackgroundRefresh { get; set; }

		public bool ConfirmBeforeEmulatorLaunch { get; set; }

		public bool ConfirmBeforeEmulatorRemoval { get; set; }

		public bool ShowCommandHints { get; set; }

		public bool CheckHealthOnStartup { get; set; }

		public int ScheduledLibraryScanMinutes { get; set; }

		public string LastLibraryScanAt { get; set; }

		public string LastSeenBuild { get; set; }

		public bool DisableInGameSaveNotifications { get; set; }
	}
	public class UserToolSettings
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public Dictionary<string, LibraryNavigation> Views { get; set; }

		public LibraryNavigation Navigation { get; set; }

		public List<SaveRouting> SaveRoutes { get; set; }

		public string ActiveId { get; set; }

		public List<BowlUser> Users { get; set; }

		public int WeeklyMinutes { get; set; }

		public int BreakMinutes { get; set; }

		public bool Controller { get; set; }

		public List<BulkUndoRecord> Undo { get; set; }
	}
	public class WebsiteLink
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Name { get; set; }

		public string Url { get; set; }
	}
	public class WorkspaceItem
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string Id { get; set; }

		public string Title { get; set; }

		public string Kind { get; set; }

		public string Target { get; set; }

		public string Notes { get; set; }

		public bool Favorite { get; set; }

		public string AddedAt { get; set; }
	}
	public class WorkspaceMemory
	{
#if NETCOREAPP
		// Keep fields from newer versions when this one saves (Linux build; JavaScriptSerializer on Windows ignores this).
		[System.Text.Json.Serialization.JsonExtensionData]
		public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
		public string SelectedId { get; set; }

		public int Scroll { get; set; }

		public int[] Columns { get; set; }

		public Dictionary<string, string> Filters { get; set; }
	}

	public static class ModelDefaults
	{
		public static Dictionary<string, string> Shortcuts()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary.Add("Search everything", "Control, P");
			dictionary.Add("Home", "Control, H");
			dictionary.Add("Library", "Control, L");
			dictionary.Add("Emulators", "Control, M");
			dictionary.Add("Add emulator", "Control, E");
			dictionary.Add("Organize games", "Control, G");
			dictionary.Add("Full screen", "F11");
			dictionary.Add("Controller launcher", "Control, B");
			dictionary.Add("Last game", "Control, R");
			return dictionary;
		}
	}
    public class RemovedAssignment
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
        public string GameId { get; set; }
        public string EmulatorId { get; set; }
        public string PreferredEmulatorId { get; set; }
    }
    public class LibraryRemoval
    {
#if NETCOREAPP
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> AdditionalFields { get; set; }
#endif
        public List<GameEntry> Games { get; set; }
        public EmulatorProfile Emulator { get; set; }
        public Dictionary<string, List<string>> Collections { get; set; }
        public List<string> Queue { get; set; }
        public List<RemovedAssignment> Assignments { get; set; }
    }
}
