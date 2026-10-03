# FishBowl 1.1

Displayed title: 1.1. Internal build and update tracking: 1.24.0.0.

## Menu and resize painting

Home card changes run in one guarded layout transaction. Full-screen border, size and window state changes are batched. Menu selections begin with an opaque background and use one highlight layer instead of an additional icon overlay.

## Selectable control stability

Summary buttons use explicit layout sizes without competing AutoSize rules. Button icons no longer appear and disappear according to caption width. Dropdown fields use the standard native border mode. Results and Notifications display full multiline messages in a read-only, selectable text area rather than single-line selectable list rows. Live verification of the reported rapid size effect is still needed.

## Native input corner rollback

The rounded window regions introduced in 1.21 have been removed from native text, dropdown and number fields after the user reported glitching. These inputs temporarily use square corners. Native editing and dropdown drawing remain. The restored 1.12 workspace headers remain rounded and themed.

This removes the recent corner-clipping change; the cause of the previously reported white-control flicker is not yet established.

## Restored 1.12 navigation appearance

Home, Emulators and Library use the 1.12 dark rounded headers, theme text and accent underline again. This replaces the white native headers introduced in 1.19. The header background fill respects the update clip and excludes the page contents. Later text clipping, row painting, hotkey reuse and dialog fixes remain.

This change restores the requested appearance; live confirmation of the reported flicker is still needed.

## Repeatable row painting and animation comparison

Emulator rows start every redraw with an opaque background before adding selection or alternating-row shading. The same selected row previously became lighter after another repaint; repeated identical paints now produce identical pixels.

For diagnosis, launch FishBowl with `--pause-ui-animation` to pause animations temporarily, including side bubbles, without changing the saved motion preference. Restart normally to restore the saved behavior. This comparison does not establish that all display flicker is resolved.

## Static text controls and emulator selection

Tab captions no longer animate. Dropdown text uses Windows native painting. Button lettering stays in the same position when pressed. Startup prompts use ordinary native painting without whole-window compositing. Side bubbles keep their separate local animation.

Updating emulator actions reuses the unselected dashboard instead of rebuilding its controls. Selection changes hide the old right-hand pane before showing the new pane, within one layout update.

## Text repaint clipping repair

Custom text drawing now preserves graphics clipping and translation. A partial button repaint previously changed 724 pixels outside its 2-by-2 update area; the repaired painter changes zero pixels outside it. The tab background eraser uses the actual Windows update region rather than clearing every header on a partial repaint. The old build fails the unrelated-header preservation check; the repaired build passes it.

## Hotkey navigation and still text fields

Home and Library commands reuse the same visit cache as tab clicks instead of forcing a second rebuild. Embedded Library delegates Ctrl+K to the main window. Search opens one dialog at a time and runs a selected action after closing the dialog. Cancelling search leaves the current section intact.

Whole-form buffering has been removed from the main window and editor dialogs. Decorative main-window painting clips out visible child controls. Bubbles retain their own local buffering; animation checks verify that text fields keep their handles, bounds, content and selection.

## Emulators/Library switching and popup painting

Emulator actions now sit inside the Emulators page. The former toolbar row above the workspace stays collapsed, so Home, Emulators and Library keep the same navigation and page bounds. Retained controls keep their existing selection, columns and scroll position without resetting the whole workspace on each visit.

The main workspace uses the restored 1.12 themed header painter; secondary tab controls retain owner-drawn theme headers. Background erasing excludes the live page area, replacing the prior whole-tab custom clearing. Library rows now use the buffered ListView already used by Emulators. Dialogs use a solid theme background, and all dialogs use ordinary native child painting without whole-window compositing. Native text editor dialogs, including release notes, use normal Windows painting to avoid compositing interference with edit controls and caret updates. The large Library uses local list buffering to avoid compositing the entire list window. Theme colors, static selection highlights and side bubbles remain available.

The suites and added rendering checks total 1,075 checks, including 281 UI checks and 43 focused hotkey, selection and native text field checks. Tests verify fixed workspace bounds across 18 Emulators/Library changes, background erasing that leaves page pixels untouched, native startup prompt painting and modal layouts. A 1,000-game fixture measured 18 switches totaling 636 ms, median 10 ms, slowest first switch 302 ms; ten startup dialog construction/show/close cycles took 508 ms. These fixture results do not establish that every real display-driver, DPI or library interaction is flicker-free.

## Startup rendering and navigation performance repair

Startup prompts use a solid theme surface with buffered painting, a wrapping header, scrolling content and a separate action footer. Setup, Games storage, Requirements storage and What's new were checked at 100%, 150% and 200% text size. Read-only release notes no longer take initial focus and select all their text. The owner flushes pending paint before the startup prompt sequence.

Main-section navigation skips unchanged Home rebuilds and repeated font/layout passes. Library visits use a lightweight data fingerprint to skip unchanged row/filter refreshes for up to ten seconds; changed records refresh immediately on the next visit. Explicit reload remains available, and existing background scans retain their behavior. Icon recoloring is cached for the current color pair; callers receive independent image copies. These changes avoid repeated work while switching or opening dialogs.

Bubbles now vary in size, initial spacing, speed and sideways drift. Each recycled bubble receives new motion values, and the two lanes use different patterns while retaining the shared icon/accent gradient.

A before/after fixture with 1,000 games measured 18 section switches: median 61 ms in 1.11 versus 30 ms in 1.12; cumulative time 1,815 versus 1,236 ms. The slowest first switch was 624 versus 498 ms. Ten startup dialog construction/show/close cycles took 2,007 versus 838 ms. These are local fixture measurements, not a guarantee of the same timing with a user's data, network paths, graphics driver or monitor settings. NavigationTiming.cs contains the timing harness.

## Stable navigation, gradient bubbles and more themes

Section changes now animate only the custom tab header. Native page controls remain live and unobstructed. Removed screenshot overlays, whole-window opacity fades and main-window redraw suppression that could flicker or leave stale frames. Startup prompts run after the main Shown handler returns; the four setup/storage/What's new prompts use buffered painting, scrolling content and a separate footer that keeps buttons visible. Reduced motion, disabled app motion and Windows high contrast disable header animation.

Small bubbles rise within 24-pixel lanes on both sides of the main workspace. Each bubble has a diagonal light-to-deep gradient fill and outline, plus a subtle highlight. Colors use the app-icon palette: Match accent follows the active accent, and a fixed icon color also fixes the bubble colors. The theme supplies the lane background. The lanes do not cover controls or take keyboard focus. One timer updates both sides and pauses when inactive, minimized or reduced motion is enabled. With reduced motion bubbles remain static. Immersion settings includes the existing bubble toggle.

Eight new themes appear in Appearance and Settings: Deep Ocean, Aurora, Slate, Plum, Sand, Paper, Nordic and Copper. There are now 18 themes, all using the existing seven accents and custom-palette options. Primary text was checked for at least 4.5:1 contrast against the card background in every theme/accent pairing. Custom palettes can override these defaults.

Home covers are 56 pixels, card actions are about 42 pixels tall, and summaries size themselves to their labels. Card padding and heading space are larger, and unnecessary card horizontal scrolling is removed. Library game names use a larger base font; rows and the same five grouped toolbar commands have more room. Larger text still scales, with scrolling and overflow menus for narrow layouts. Roomier Home cards and Library rows can be toggled in Immersion settings. The banner adapts to larger text.

Repeated visits to an unchanged Home retain its cards; data changes or explicit reload rebuild immediately. Home retains small artwork copies instead of full-size source images. Unchanged font objects are reused during section navigation. Existing lazy Library thumbnails and row reuse remain in place. These are UI improvements; emulator performance depends on the emulator and game.

## Immersion

Open Tools > Library extensions > Presentation > Immersion. Game actions > Game extensions > Focus view opens the selected game directly. The primary menus and Library toolbar stay compact.

- Game backdrops: optional selected-game artwork behind Library rows and focus view, heavily dimmed for readable text. Local images are copied at bounded sizes; missing or damaged images fall back to the theme. Changing an artwork file refreshes its backdrop.
- Focus view: show a cover, description, notes and Play controls in a clean game screen. Focus / shelves reveals or hides navigation.
- Gentle transitions: covers and their surrounding background fade when selections change; custom tab headers animate without moving page contents. Reduced motion disables fades. No flashing or animated text is added.
- Interface sounds: optional short generated selection and launch tones. Separate 0–100% volume and a Test sound button; they do not change Windows or emulator volume. Sounds start disabled.
- Launch presentation: optional brief artwork after validation and confirmation. Cancel or Escape prevents process start. The card describes preparation, not emulator loading progress or proof that the game is ready. Once the process starts, FishBowl does not forcibly stop it through this card.
- Screenshot gallery: browse captures linked to the selected game, with Previous/Next and the existing manage/add screen. Missing or invalid images are ignored. It does not take new emulator screenshots automatically.
- Personal shelves: Continue playing (recently launched, excluding Completed), Recently added and Favorites. Each shelf is bounded to 40 items, with search and focus selection; existing per-user progress supplies the shelves.
- Session recap: optional recorded process duration, note and rating after a directly tracked session closes. Dismiss makes no edits. Notes are attached to the session and game notes; ratings use existing progress storage. Shortcuts/wrappers do not produce uncertain recaps. Automatic recaps require their owner window to remain open.
- Ambient mode: optional local-artwork slideshow after 1–60 idle minutes while the main FishBowl window is active and tracked sessions/background work are inactive. A preview is also available. Slides change every 12 seconds; input delivered to the slideshow or its Exit action closes it. It starts disabled and does not watch input in other applications or become an operating-system screen saver.
- Theme presets: Arcade, Minimal and Retro coordinate readable palette colors, background style, cover frames and row spacing. Current preserves existing cosmetic choices. Applying a preset replaces those cosmetic values; ordinary cosmetic editing remains available.

All switches share Immersion settings, with Apply/Cancel. Settings are saved with the Library; cosmetic presets use the existing profile appearance. New backgrounds, launch cards, recaps, sounds and automatic ambient mode start off. Local artwork, captures and play history are used; no online service is needed for these additions.

## Library extensions

Open Tools > Library extensions, or Library tools > Library extensions. Four tabs group the additions: Library, Emulators, Protect and move, Presentation. The primary Library toolbar still has five controls and the main menu still has six headings. Game-specific media and options also appear in Game actions.

- Command search: Ctrl+K opens the existing global search. Existing configured shortcuts remain available. Search games, menu actions, settings and collections.
- Launch readiness: validate files and arguments, review the emulator setup checklist, and open guided repair options. This checks configuration, not compatibility inside a particular game.
- Per-game overrides: reuse registered emulator builds and launch profiles, with extra arguments in Game extensions. Graphics presets must use flags supported by the selected emulator.
- Recent activity: view recent play, edits made through the new tools and shared save snapshots. Other operations retain their existing activity/history screens; this is not a complete OS activity log.
- Crash recovery: an interrupted-run marker offers to reopen the remembered Library view. Clean closes remove the owned marker; another detected live instance is not treated as a crash. This recovers the persisted view, not unsaved work or an emulator session.
- Preview folder imports: select discovered files before adding them. Existing and duplicate paths are skipped. Disc grouping remains available in the existing game/disc tools.
- Portable collection paths: record bindings under a common root before moving the folder yourself, then review a new root. Bindings cover game/disc/art/manual paths, emulator programs/builds and folders, Library roots, linked saves, snapshots and screenshot references under that root. Outside-root paths stay unchanged. A changed bound path requires a fresh binding. Literal arguments and emulator-internal configuration paths need separate review. No files are moved by rebasing.
- Storage overview: count configured game, artwork, linked-save and snapshot files. Unreadable files are excluded and categories can overlap. The existing reviewed retention controls provide cleanup.
- Nested collections: parent views include games from descendants; cyclic parents are rejected. Existing Collections handles game membership.
- Custom metadata: edit up to 100 Key = Value fields per game. Library search and saved smart lists now search custom fields, description, notes, year, platform and existing metadata.
- Optional metadata lookup: Search online sends the query to English Wikipedia and shows matches for review. Apply a description, review the source, select an available Wikimedia thumbnail, or choose a local cover/catalog. Existing genre, developer and year stay intact. HTML catalogs include a metadata source link. Review source-specific image rights before sharing artwork. Lookup and image decoding were tested with fixtures; the live connection failed because Windows could not obtain TLS credentials in this tool environment. Open search website and local catalog options provide alternatives.
- Trailers/manuals: save an HTTPS trailer address and open it in the browser; open a local PDF/text/HTML manual through its Windows association. FishBowl does not embed a video player.
- Scheduled linked-save captures: disabled by default. Choose an interval while FishBowl is open. Capture changed original linked saves only when a directly registered program and its registered emulator builds can be confirmed closed. Unchanged content and managed snapshots are skipped. Captures are verified; cancelled batches discard newly created snapshots and preserve originals. Stop scheduled capture is available in its settings. The existing separate export schedule handles already-created snapshots and quotas.
- Restore previews: compare incoming snapshot hashes with live contents before the existing confirmation/rollback restore. The preview marks additions, replacements, unchanged files and live-only files removed from the active location but retained in rollback.
- Cloud-folder conflict review: inspect filenames indicating conflict copies in your cloud client's local folder. It does not delete copies, verify completed uploads or identify every possible conflict.
- Emulator versions and updates: inventory current and registered builds. Activate a registered build with the prior executable retained for rollback. Optional daily release watching checks configured GitHub feeds while FishBowl is open and reports known newer versions once per tag. It starts disabled. Release checks and Windows notification delivery remain unverified against live services in this environment; fixtures verify comparison, error handling and notice deduplication.
- Emulator adapters: optional JSON definitions specify a platform, extensions, {game} arguments and setup/repository links. Import with review, export templates and create profiles from installed executables. These are declarative adapters, not executable DLL/script plugins or an automatic emulator downloader.
- Living-room Library: separate full-screen game cards with covers, text search, favorites, launch/details and an explicit exit action. Mouse and keyboard navigation are supported.
- Multiple-computer transfer: export the active personal profile and game records to a JSON file. Review a Library merge or import a separate profile. Conflicts default to keeping local data. Existing launch paths/type, emulator assignments, linked saves and recorded launch/time data remain local; selected metadata and custom fields can merge. New records need local path/emulator review. Transfers contain personal metadata and paths, but no game/save bytes or emulator programs. Cloud clients transport the files; FishBowl performs explicit reviewed merges, not unattended cloud synchronization.
- Native PC games: add installed .exe or .lnk entries alongside emulated games. Direct executable sessions use the existing tracker. Shortcut/wrapper duration is uncertain. This does not import online storefront accounts.
- View styles: adjustable cover size, Compact/Comfortable spacing, optional platform labels/badges, a cover/detail pane, reduced motion and separate remembered details/artwork views. Cosmetic styles includes the new Library spacing, platform and preview controls. Styles and remembered views follow existing user profiles.

These additions implement the requested directions through optional screens and existing tools. Online accounts, proprietary metadata providers and arbitrary binary plugins are not bundled. Your existing Library and game/save files are preserved during the release update.
## Library polish and compatibility checks

- Larger libraries reuse existing rows, debounce search and load visible artwork in the background. Thumbnail storage is bounded; missing or damaged images fall back to placeholders. File statistics are cached briefly to reduce repeated disk reads.
- Empty views explain the cause and offer Add game, Clear filters or Check health. Missing-path rows have diagnostic tooltips.
- Each user remembers Library selection, filters, search, sort, artwork view and scroll position.
- Bulk edits, settings imports and path repair show a before/after review before applying changes. Imports are blocked during tracked launches and background work.
- Accessible control names, tab order, high-contrast colors, larger text, wrapped filter rows and responsive menus improve keyboard and controller use. Controller polling is more responsive; numeric fields and emulator activation are supported.
- Check and repair includes Library health, save-snapshot verification, device detection and emulator-file checks. Diagnostics do not prove a game will run correctly inside an emulator.
- Personal > Emulator saves offers per-user save routing, disabled by default. Dolphin receives a separate --user directory (including its configuration and NAND); RetroArch receives an appended configuration for save and state directories. Custom arguments require flags supported by your emulator. Preview shows the route; directories are created on launch. Existing saves are never migrated or deleted. New Dolphin directories may need emulator setup. Guest launches retain the active user's route, and emulator save files can still change during guest play. Games, emulator definitions and save associations remain shared.
- The combined automated suite covers 812 checks, including 70 theme/accent combinations, 10,000 synthetic games, malformed artwork, repeated artwork disposal, navigation, profile routing, import guards and installer-screen construction. The 10,000-game fixture exercises construction and message pumping; its response time depends on storage and artwork.

The installed Dolphin command-line version check exited successfully using a temporary user directory. No physical XInput controller was detected. Real game sessions, other emulators' live behavior, actual installer writes and the protected existing settings file were not verified. These checks cannot guarantee every emulator/game combination or the absence of all glitches. See FishBowl-1.24-Changes.md for scope and results.

Routing references: [Dolphin command-line parser](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/UICommon/CommandLineParse.cpp), [RetroArch command line](https://docs.libretro.com/guides/cli-intro/), [RetroArch directory settings](https://docs.libretro.com/guides/change-directories/).
## User tools

Open Tools > User tools, or Library tools > User tools. All eight additions share three tabs (Personal, Check and repair, Share); the primary Library toolbar keeps its five controls.

- User profiles: separate favorites, pins, progress, ratings, tracked time, launch history, queues, smart lists, session journals, goals, controller preferences and appearance/settings. New profiles start with fresh progress and inherit the current appearance. Games, metadata, emulator configurations, screenshots and emulator save files are shared by default. Optional per-user emulator save routing is described below. Switch only after tracked emulator sessions and background jobs finish. The first profile preserves existing progress.
- Guided troubleshooting: failed validation or process start opens a diagnostic screen. It explains missing files, assignments and argument/profile issues, with direct game and emulator editors and a Recheck action. It does not diagnose failures that happen inside an emulator after successful process start.
- ROM integrity: choose a local XML DAT and compare the selected raw game/disc file against size and all supplied CRC32, MD5 and SHA1 values. External XML resources are disabled. DAT files are limited to 100 MB. Results distinguish verified, named mismatch and unrecognized files. Archives and disc sets are not unpacked; a DAT describing different bytes cannot verify them. Hash work runs in a cancellable background task and never repairs/deletes files.
- Controller navigation: optional XInput navigation of focus, buttons, lists, checkboxes, dropdowns and tabs while FishBowl is active. D-pad moves, A activates, B closes dialogs, shoulders switch tabs. Enable it in Personal. It does not remap emulator controls. Non-XInput devices require an existing XInput mapping. Physical hardware testing is still needed; simulated UI inputs passed.
- Goals/reminders: a per-user rolling seven-day recorded-play-time goal and optional repeating break reminders during directly tracked emulator sessions. Set either to 0 to disable it. Reminders never pause or close an emulator; Windows notification settings can suppress delivery. Uncertain wrapper launches and old persisted sessions do not trigger reminders.
- Bulk undo: the last ten bulk edits are retained. Undo restores changed metadata/progress fields and collection additions, preserves unrelated edits, and makes no changes if a relevant later edit conflicts. History is matched to the active user. File operations use their existing separate undo behavior.
- Guest browser: a temporary, filtered, read-only Library with launch and details commands. FishBowl Library persistence is suppressed; guest activity is discarded on exit. Close guest-launched tracked emulator sessions before leaving. Main background timers/watchers are paused during this screen. Emulator saves remain controlled by the emulator. Guest mode is a convenience mode, not a password or operating-system security boundary.
- HTML catalog: export all games, favorites, the queue, or a checked selection. Optional local artwork is embedded as small PNG thumbnails without the source path or image metadata. Local paths and personal play history are excluded by default. Descriptions/tags can contain personal information, so preview before sharing. The catalog works offline with local filtering and no remote resources. Large catalogs are limited to approximately 50 MB; reduce the selection or omit artwork if necessary.
## Cosmetic styles

Open View > Appearance and layout > Cosmetic styles, or use the Cosmetic styles button in Live appearance and accessibility. Changes preview on open windows. Apply saves without closing; Revert returns to the last saved state; Restore defaults previews defaults; Save and close saves; Cancel discards changes since the last Apply. Settings are stored with your existing Library.

- Custom palettes: independently choose text, secondary text, header, page, cards and both accent colors. Use the color chooser or #RRGGBB values. Disable the custom palette to return to the selected theme. Text contrast is adjusted automatically where controls are styled.
- Corner rounding: 0–20 pixels, including square controls.
- Icon styles: outline, filled or monochrome. Toolbar commands have distinct symbols.
- Icon-only toolbars: common actions retain their accessible names and hover tooltips. Dialog action buttons keep their text labels.
- Backgrounds: plain, gradient, dots, waves or a local wallpaper, with Fill/Fit/Tile layout and 0–40% opacity. Wallpapers appear in exposed Home and window background areas; content cards retain readable surfaces. Wallpapers stay at their configured local path and are not embedded in exported settings.
- Artwork: None/Thin/Accent/Rounded frames and soft shadows for Home tiles and Library artwork thumbnails. Original artwork files remain unchanged.
- Selection and status: optional selection and focus colors, 1–5 pixel focus outlines, Text/Pill/Square badges, and optional Ready/Running/Warning colors. Blank optional colors follow the theme.
- Footer: toggle the aquarium decoration while keeping status information visible.

All eight requested cosmetic options are implemented. These settings do not change emulator behavior or game/save files.

## Compact menus and toolbars

The Library toolbar has five controls: Add game, Launch, Details, Game actions and Library tools. Selection-specific editing, saves, progress, pins, favorites and bulk actions are in Game actions. Discovery, organization and export commands are in Library tools. The toolbar occupies one approximately 62-pixel row at normal text size and adapts to larger text and narrow windows.

The main menu has six headings. Library commands are grouped into Play, Organize, Saves, Artwork and activity, and Backup and transfer. Tools groups search, launchers, maintenance, settings, controllers and multiplayer. Emulator editing and profile transfers are in Emulators. Useful links and Community are in Help. View groups appearance and layout options. Duplicate Game Library, Community and Remove buttons were removed from the emulator toolbar; the commands remain in navigation, menus and context menus. The original 101 main-menu commands remain available alongside Cosmetic styles and User tools.

Open FishBowl.exe to run, or FishBowl Setup.exe to install. Keep the six release files together.

## Appearance fixes

Library and other action buttons use a muted fill based on the selected accent instead of a bright secondary accent. Text is adjusted for at least 4.5:1 contrast against its background, including painted hover/pressed and disabled button states. Dropdowns and table headers follow the theme. Live appearance changes reapply styling to open windows. Existing themes, accents, icon colors, accessibility settings and 1.3 features remain available.

## Added capabilities

Find these in the main Library menu, or Library > Library tools:

- Smart lists: save combinations of title/genre/developer/tag search, platform, progress, favorites and unplayed filters. Results update from current Library data. Export results or add games to the queue.
- Play queue: choose games to play later, reorder them, launch, inspect details, remove entries or export the queue. It persists in your Library. Launching does not remove entries.
- Surprise me: randomly choose a game with an existing game file and valid configured launch settings. Optionally restrict to games never launched through FishBowl. Preview before launching or add to the queue. This cannot guarantee emulator compatibility with a specific game.
- CSV export: export the entire Library from the menu, the current filtered Library view from Library tools, a smart list, or the queue. Includes metadata, progress, launches and tracked play hours. Spreadsheet formula-like metadata is escaped as literal text.

## Storage and updating

Normal storage is %LOCALAPPDATA%\FishBowl. An empty portable.flag beside FishBowl.exe uses FishBowlData beside the application. Keep existing portable.flag and FishBowlData when updating. Close FishBowl before replacing application files. Your games, emulators, original saves and settings are not included in or replaced by this release.

## Editable source

FishBowl-1.24-Source.zip contains the recovered current source, new additions, artwork, build script and tests. FishBowl 1.3 was recovered from the user's executable with ILSpy 8.2. Original comments, local variable names, source file organization and project history could not be recovered. Three decompiler-generated variable name collisions were repaired to rebuild with the Windows .NET Framework C# compiler.

The recovered baseline matched all named methods in the original executable and passed the 31 existing integration checks. This is evidence of preserved behavior, not a guarantee of binary-identical recovery.

Run Build.ps1 to compile the application and installer with the Windows .NET Framework compiler. Run 'Run Tests.ps1' after building; it creates a separate temporary portable fixture. Tests cover existing saves/import/restore/launch behavior, new feature persistence and filtering, CSV escaping, 70 theme/accent combinations, profile isolation, guest persistence, bulk undo conflicts, HTML privacy/artwork, local DAT checksums and simulated controller actions. The current suite has 1027 checks, including 279 UI stability checks and all 126 theme/accent combinations. The final build passed in an isolated portable fixture. These checks do not guarantee every display-driver, DPI or real-library interaction is glitch-free. Emulator-specific behavior still depends on the user's emulator configuration.
