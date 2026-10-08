# Windows 1.29.2 window recovery

- Go navigates the exact existing browser window through its address bar; searches no longer start another browser instance. Edge/Chrome use navigable normal windows with dedicated profiles.
- Open in window changes to Return to Web or Return to Play, with accessible labels and immediate reattachment of the same verified session.
- Share session keeps the game inside Play and uses a modeless dialog. Child renderers switch to an owned viewport for sharing; embedded windows are included by exact PID/start/HWND identity.
- Main includes the Linux save-parity merge. Documentation and generated-file ignore rules are refreshed; native browser keyboard navigation and two-device streaming remain live checks.

# Windows 1.29.1 browser hosting

- Web uses borderless owned-window hosting to preserve browser rendering and DPI while keeping the browser inside FishBowl. Firefox starts as a normal window instead of desktop kiosk mode.
- Section changes hide and restore the same browser; fullscreen preserves its window and FishBowl's exit controls.

# Windows 1.29.0 Web and game view

- Web hosts installed Firefox by default, with selectable Microsoft Edge or Google Chrome adapters. Browser settings saves engine/program/home page choices; separate FishBowl profiles hold browsing data. Personal browser windows are not adopted or closed.
- Added address/search entry, Back/Forward/Reload, fullscreen and external-window recovery using the current UI. Firefox Go opens a tab in its dedicated profile; Edge/Chrome Go closes the previous app window normally before opening the new address. Page permissions and downloads remain managed by the browser.
- Closing FishBowl with a running game asks first. Close games and exit requests normal shutdown; Cancel retains the game and Keep games running explicitly restores external windows. Emulator save/exit vetoes keep FishBowl open. No routine force termination is used.
- Play hides standard native menus and offers saved per-emulator game-view edge adjustments for custom toolbars/status bars. External mode restores original menus, styles and placement.
- Browser and view fields are optional shared data. Linux preserves them but keeps its existing external-launch UI. Actual browser/emulator rendering, input and mixed-monitor compatibility require native verification; external-window fallback is available.
# Windows 1.28.1 polish and recovery

- Play shows actionable waiting/recovery states, disables unavailable actions and labels fullscreen exit explicitly. More opens session guidance; failed detach keeps the game in Play.
- Share session detaches and selects the exact running window for remote hosting, then returns an embedded session to Play when sharing closes. The token service setting persists; target changes stop guest input.
- Dropdown selection, empty and disabled states follow the active theme while native editing remains intact. Emulator filters disappear outside Emulators and toolbar height shrinks to its content.
- Home expansions persist across restarts; existing advanced tools remain in More menus and current settings screens.
- Idle Play avoids repeated renderer placement/clipping, slows settled/hidden process polling and pauses decorative motion during active Play. Process image lookup can use Windows limited-query access while retaining exact PID/start/window ownership checks.
- App/source downloads include source commit metadata, checksums and upgrade/rollback instructions. Tagged draft releases require source on main and passing normal/compact text audits. Physical controllers, audio, multi-monitor rendering and two-device remote streaming still require live verification.

# Windows 1.28.0 Play workspace

- Added Play alongside Home, Emulators and Library. Game and emulator launches route to a persistent native window host; section changes retain running sessions.
- Added fullscreen, session selection, Show in Play and Open in window using existing controls and appearance.
- Per-monitor renderers use a borderless owned window aligned and clipped to Play, preserving renderer DPI.
- Native windows are matched by exact process/start identity and restored before host disposal or handle recreation. FishBowl closes without terminating emulators.
- Linux retains external emulator windows; native embedding needs a separate platform implementation. Renderer/elevation compatibility and actual emulator smoke tests remain required.

# Windows and Linux 1.27.0 session and controller improvements

- Added durable, profile-owned playtime checkpoints. Restart recovers saved active time and resumes exact running process identities without counting app downtime. Games remain running when FishBowl closes.
- Follows verified launcher descendants, excludes pre-existing processes and PID reuse, and conservatively marks unverifiable launches uncertain.
- Windows Appearance and accessibility routes share one hub. General startup/backup preferences retain their separate screen and current controls.
- Added Windows controller deadzone/repeat/reconnect guards and menu, overflow, dialog, checkbox/list/choice/numeric/tab navigation. Linux reads existing joystick devices and routes input only to the active FishBowl window.
- Added the browser LiveKit media client, private room token service, invitations and host-approved keyboard/gamepad-to-keyboard controls. Windows hosts choose a single game window; Linux can join through the browser. Service setup, real two-device streaming and emulator input testing remain required.
- Session tests wait for verified completion instead of fixed delays. Shared journal recovery safely ignores malformed JSON. Normal/compact text audits include the remote-play dialog.

# Windows 1.26.2 compact controls and theme harmony

- Compact control sizing is the default, reducing box/button padding, toolbar/banner spacing and Home card dimensions while preserving configured fonts and text scaling. Standard and Roomy remain available; the controller preset uses Roomy.
- Input, button and selection colors follow the active theme, with readable text and consistent backgrounds. Original colors and custom palettes remain available.
- Corrected stale colors after switching themes. Home previews and Show all retain every option.
- Fixed repeated Library text scaling and overlapping enlarged filters; smaller viewports scroll to keep actions and game rows reachable.

# Windows 1.26.1 Home layout

- Home uses up to two wider columns, more internal spacing and a shared row for a card's primary/More actions.
- Game/emulator cards show three preview entries; information cards show one summary line. Show all reveals the remaining content, and Show less returns to the preview. All games, details and actions remain available.
- Expanded cards retain their state when Home refreshes. Home card ordering/visibility and page scrolling remain supported.

# Windows and Linux 1.26.0 source

- Added Home page scrolling, primary/More actions, theme/accent previews, accessibility presets, cover proportions, card ordering/visibility and restrained accents using current controls.
- Added unified game setup, native executable imports and installed Steam discovery; declarative import catalogs can cover additional storefronts with explicit executable selection.
- Added verified mod overlay backups and resumable rollback, IPS patch copies, manuals/trailer links, screenshot galleries, monthly summaries and session notes.
- Added reviewed file save timeline/comparison/transfer and backup-before-overwrite recovery. Folder archives use the emulator backup manager.
- Added RetroAchievements completion caching, reviewed metadata provider catalogs, declarative extensions and a paired read-only companion.
- Added Linux Home/Library/profiles/collections/living-room keyboard screens and matching title/cover/removal/recovery/game tool actions. Linux remains a preview requiring desktop smoke testing.
- Added complete app/source packaging, checksums and version-checked draft-release automation. No executable is committed to source.
- Windows popup bounds fit compact desktops. Existing startup popup fixes and appearance remain supported.

# Windows 1.25.12

- Expanded themes from 18 to 40, with distinct dark, light, warm, cool and neutral surfaces. Existing saved theme names remain supported.
- Expanded accents from 7 to 28. Appearance, Settings and app icon colors share the same catalog; Match accent follows all new colors.
- Removed the sand, pebbles and plants from the footer. Its water background follows live theme changes.
- Kept the existing UI layouts, controls and wording. Custom palette overrides and Windows high contrast preferences remain supported.
- Added coverage for all 1,120 theme/accent combinations, readable text, saved choices, settings selectors, app icon colors and footer painting. Linux needs matching theme/accent choices and footer styling; saved fields are unchanged.

# Windows 1.25.11

- Normalized organization source paths before grouping playlists, cues and tracks. Repeated references are deduplicated; circular playlists are rejected. Tests cover Windows short temp paths, relative paths and enumeration order (#10).
- Fixed large-text layout scaling so fonts are enlarged once. Existing labels, buttons, headers and form rows fit their text without changing themes or wording. Rows wrap and card headings expand when enlarged text needs more room on a small desktop.
- Window resize and full-screen transitions remain responsive on small desktops. Icon-only toolbars retain their compact widths.
- Added Library tools → Undo removal. The last 20 game or emulator removal batches are retained across restarts. Restores game metadata, collection membership, queue order and emulator assignments, preserving entries edited or re-added afterward. Conflicting entries stay available for recovery.
- Portable libraries record their previous roots. Moving the portable folder repairs missing paths when matching files exist in the new folder. Repair game paths → Review moved reviews matching paths under a selected old/new folder pair before applying them. File contents and names remain unchanged.
- Launching a game whose emulator was removed prompts for a preferred emulator. FishBowl no longer silently uses the sole remaining emulator for those games.
- Added Library tools → Cleanup artwork. It reviews unused generated covers and icons before removal, protecting active entries, Undo removal history, inactive profiles and readable library backups. Unreadable backups stop cleanup.
- Shared optional recovery and portable metadata are preserved by the Linux schema tests. The shared helpers compile into both platforms; Linux still needs corresponding UI actions.

# Windows 1.25.10

### Fixed

- Edited library titles save immediately in the row and selected-game preview. They remain separate from filenames and are preserved by automatic recognition.
- Assigned game covers display in the default list as well as artwork view. Cover replacements refresh cached images and selected-game previews.
- Selected covers are retained in FishBowl artwork storage, preserving original image files and avoiding broken references if those files move.

### Added

- Visible Remove game and Remove emulator actions, including confirmation and removal of multiple selected games.
- Persistent scan exclusions for removed game paths and additional discs; explicit addition restores a path.
- Shared optional title preference and scan-exclusion fields, plus non-UI library helpers compiled into both builds.
- Regression checks for title edits, cover retention and refresh, cancellation, removal, scan behavior and file preservation. Appearance tests use an independent portable fixture to avoid inheriting settings from earlier tests.

# Windows changelog

## 1.25.8

### New

- **Game identification and icons.** Adding a game detects its title, platform and title ID from local metadata, without any online service or decryption:

  | Source | Supported metadata |
  | --- | --- |
  | Nintendo DS | Banners |
  | Nintendo 3DS | SMDH, NCCH and CIA metadata |
  | PlayStation Vita | VPK packages and installed game data |
  | PC games | Executable metadata and icons, plus nearby PNG/ICO files |
  | Game Boy, GBA, N64, GameCube and Wii | Header names and IDs |
  | Switch | Title ID from the file name |

  Detected icons are stored as PNG files under Artwork/GameIcons and kept across restarts; custom names and existing artwork are kept. **Game actions > Identify game and retain icon** fills in an existing game. An emulator is suggested only when exactly one configured emulator accepts the file type.
- **Launching installed games.**
  - **3DS:** CIA packages assigned to Azahar, Citra or Lime3DS launch the matching installed game.
  - **Vita:** VPK/PKG packages assigned to Vita3K launch the installed title by its ID.
  - **No reinstall:** the installation package is not passed to the emulator again. It can be removed after installation; the icon and launch keep working.
  - **When it can't be found:** if the installed game is missing, FishBowl explains how to install it inside the emulator. Update and DLC packages are not launched as base games.

### Fixed

- **Startup popups:**
  - Welcome, update notes, the storage prompts and notifications appear centred at their final size, without the open/close flash or a top-left jump.
  - They no longer add taskbar entries or leave stray windows when closed.
  - They share one branded layout. Notifications gain a Close button with Enter/Escape support.
- **Painting:** windows no longer repaint when nothing has changed. Theme changes refresh open windows, and list views, dialogs and tab headers use buffered painting, which reduces flicker.
- **Add/Edit Emulator:** the action buttons stay visible at every text size; the fields scroll, and Browse columns widen for larger text. New emulators have an explicit **Add emulator** button.
- **Azahar Plus:**
  - Uses the AzaharPlus user folder, portable user folders and custom SD storage.
  - Installed games launch from the main game content rather than manuals or data.
  - Outdated folder overrides no longer hide installed games, and failed lookups report the title ID and searched folders.
- **Settings:**
  - Secondary tabs use the same themed headers as Home, Library and Emulators, and text boxes follow the theme.
  - Repeated appearance changes no longer compound the text size.
  - The game editor has a fixed action footer and room for notes.
- **Launch arguments:** quoted `{game}` templates no longer produce double-quoted paths. The launch log records the emulator and its arguments.

## 1.1

The displayed version became **1.1** (app title, About, What's new and setup banner). No behaviour changes.

## Installing and updating

Run `FishBowl.exe` directly, or `FishBowl Setup.exe` to install. Close FishBowl before replacing an installed copy.

- **Library:** your library stays in `%LOCALAPPDATA%\FishBowl`, or in `FishBowlData` beside the program when an empty `portable.flag` file is present.
- **Upgrading:** keep both of those when updating. Updates never replace your games, emulators or saves.
