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
