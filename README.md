# FishBowl

FishBowl is a hub for your emulators and games. It keeps your installed emulators, your game library, saves and setup information in one place, and launches everything with the emulator you already use.

| Platform | Version | Status |
| --- | --- | --- |
| Windows | 1.25.8 | Current release |
| Linux | Preview | Emulator hub; the Windows library features are being ported |

Source version **1.25.12** is under review in the [theme update](../../compare/main...windows-themes-1.25.12). It adds 40 distinct themes, 28 accents and a clean water footer alongside library editing fixes, Undo removal, portable path recovery, explicit emulator reassignment, artwork cleanup and large-text layout fixes. The public release remains 1.25.8 until review and CI complete.

## Features

### Windows

- **Home, Emulators and Library** sections, with a continue-playing dashboard, pinned games and quick actions.
- **Game library** with cover artwork, game identification, collections (including nested collections), smart lists, a play queue, favorites, ratings, progress and custom metadata. Already installed games can be launched directly.
- **Emulator management:**
  - presets for more than 40 emulators, plus custom emulators
  - setup checks and folder routing for saves, save states, configuration, screenshots and logs
  - registered builds with rollback, and release checks against official GitHub feeds
- **Saves and backups:** archive and restore emulator configuration, in-game saves and save states; snapshots of linked saves, with restore previews and optional scheduled captures.
- **Profiles:** separate favorites, progress, play time and appearance for each person, plus a temporary guest mode.
- **Living-room mode and controller navigation** for full-screen, couch-friendly browsing.
- **Appearance:**
  - 18 themes, seven accents and custom palettes
  - adjustable text size, corner rounding, icon styles and backgrounds
  - reduced-motion support
- **Tools:**
  - command search (Ctrl+K)
  - ROM verification against a local DAT file
  - CSV and HTML catalog export
  - transferring a profile between computers

### Linux

The Linux version shares the emulator presets, folder routing, backups and library format with Windows, and adds Linux integration:

- **Emulator types:** native programs, AppImages, Flatpaks, shell scripts and `.desktop` entries. Find installed detects Flatpaks, packages on the `PATH`, and AppImages in common folders.
- **Folder routing** follows each emulator's Linux layout: XDG folders such as `~/.config/PCSX2`, Flatpak sandboxes under `~/.var/app/<app ID>`, and portable markers.
- **Installed versions** come from Flatpak metadata, the distribution package (pacman, dpkg or rpm), or the AppImage file name.
- **Running emulators** are detected, including Flatpak and AppImage processes. Opening one that is already running brings its window forward on Hyprland, Sway and X11.
- **Importing:** AppImages and ZIP packages can be imported into a dedicated emulator folder.

The game library, profiles, snapshots and living-room mode are Windows-only for now. The library file is shared, and the Linux version preserves every Windows field when it saves.

## Installation

### Windows

Download `FishBowl-Setup.exe` from the [latest release](../../releases/latest) and run it, or download `FishBowl.exe` to run FishBowl without installing.

### Linux

Building requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The installed program is self-contained and does not need .NET.

```sh
linux/install-linux.sh     # installs to ~/.local/lib/fishbowl and adds a menu entry and the fishbowl command
linux/uninstall-linux.sh   # removes the program; your library is kept
```

To run from source without installing: `dotnet run --project FishBowl.Avalonia`.

## Where FishBowl keeps your data

| | Library and settings |
| --- | --- |
| Windows | `%LOCALAPPDATA%\FishBowl\library.json` |
| Linux | `~/.local/share/FishBowl/library.json` |
| Portable mode | A `FishBowlData` folder beside the program, used when an empty `portable.flag` file is present |

Saves are atomic, and the previous file is kept as `library.json.bak`. An unreadable library is preserved as a recovery copy instead of being overwritten.

What FishBowl never does on its own:
- **Emulators:** it opens your installed emulators and official project pages. It never downloads, installs or replaces emulator builds.
- **Firmware:** it never includes or downloads BIOS, firmware or keys.
- **Emulator files:** folder detection only reads emulator settings. Restoring a backup or changing a file happens only after you confirm it.

## Building from source

### Windows

The release source lives in [`windows/`](windows/) and builds with the .NET Framework C# compiler included in Windows:

```powershell
windows\Build.ps1          # builds FishBowl.exe and FishBowl Setup.exe
& "windows\Run Tests.ps1"  # runs the regression suite against a temporary portable library
```

### Linux

```sh
dotnet build FishBowl.Avalonia                        # the Linux app
dotnet run --project tests/FishBowl.Tests             # shared-logic tests
dotnet run --project tests/FishBowl.SchemaTests       # library format compatibility tests
```

### Continuous integration

Every push and pull request is checked by GitHub Actions:
- **Windows:** builds the app with `csc.exe`, runs the Windows regression suite, and checks that the shared files still compile as C# 5.
- **Linux:** builds and tests the Linux app, and publishes a Linux build.
- **Text overflow audit:** a separate workflow opens every Windows dialog at 100%, 150% and 200% text size and reports text that doesn't fit.

## Repository layout

| Path | Contents |
| --- | --- |
| `windows/` | Windows release source, build and test scripts, and change notes |
| `FishBowl.Model.cs` | The library data model (`library.json`), shared by both versions |
| `FishBowl.GameRecognition.cs` | Game identification from local metadata, shared by both versions |
| `FishBowl.Core.cs`, `FishBowl.Platform.cs` | Shared emulator logic, and the differences between Windows and Linux |
| `FishBowl.Avalonia/` | The Linux user interface |
| `linux/` | Linux install scripts and desktop entry |
| `tests/` | Linux and compatibility tests |
| `Folder Routing.md` | How FishBowl finds each emulator's save, state and configuration folders |
| `HANDOFF.md`, `AGENTS.md` | Current development status and instructions for contributors' coding agents |

Shared files must stay compatible with C# 5, because the Windows build uses the .NET Framework compiler. See [SOURCE-LAYOUT.md](SOURCE-LAYOUT.md) for how the two builds fit together.

## Contributing

- **Bugs:** report them with the [bug report form](../../issues/new/choose).
- **Changes:** make them on a branch and open a pull request into `main`. The checks must pass before merging.
- **Ground rules:** [DESIGN.md](DESIGN.md) covers how the Windows and Linux versions are developed together: shared files, the C# 5 requirement, library compatibility and the release process.
