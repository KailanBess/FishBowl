# FishBowl

The current Windows release source is in [`windows/`](windows/), version 1.25.8 (internal build 1.25.8.0). Build it with `windows/Build.ps1`. The retained root split source powers Linux and does not yet include the newer Windows features. See [source layout and compatibility](SOURCE-LAYOUT.md) and the [collaboration design](DESIGN.md).

The current Windows build supports game identification, retained game icons and launching already installed games, alongside emulator management. See the [Windows release source README](windows/README.md) and [change notes](windows/CHANGES.md) for its features, build commands and validation.

The documentation below describes the older split source retained for the Linux port. Its feature coverage differs from the current Windows release.

## Start

Open FishBowl.exe in this folder. Use Add emulator to choose an optional preset, a display name, and an installed program or shortcut. Custom accepts any Windows emulator or frontend using .exe, .bat, .cmd, or .lnk; the preset list does not restrict what you can add. Double left-click a row to open it, or press Enter while the list is focused.

Emulator details start hidden. Click an emulator or select it with the arrow keys to show its information. Click empty list space, or press Escape with an empty filter, to clear the selection and hide the details. Filtering out a selected emulator also hides its details; FishBowl does not automatically choose another row.

Icons receive a subtle, nearly transparent light highlight when hovered. The logo, menu icons and emulator icons use this treatment; toolbar and folder actions share the soft hover feedback. The selected row keeps its purple background, lavender outline and orange left marker, plus a light highlight around its icon. Artwork and icon files are unchanged.

## Information tabs

- Overview: systems, description, strengths, limitations, installed version, executable location, and project/release/changelog links.
- Setup: firmware/BIOS and hardware guidance, documentation, and compatibility links.
- Controls: controller information, your controller notes, documentation, and Windows controller settings.
- Folders: separate In-game saves and Save states shortcuts, plus configuration, screenshots, logs and the program folder. Known emulators are routed automatically from their storage layout and saved settings. Refresh rereads settings; Choose sets an override; Auto restores detection. See Folder Routing.md for coverage and details.
- Help: common startup, firmware, graphics, controller and log troubleshooting, plus project support links.
- Notes: personal emulator notes, saved automatically and searchable in the filter.

Edit information lets you supply or override platform labels, version, description, requirements, controller information, project/documentation/compatibility/release/changelog/controller/support links, and folder paths. This works with presets and custom emulators. Leave fields blank to use preset information when available. Some projects do not publish a separate compatibility list; those links remain disabled until you supply one. Installed version is read from Windows file metadata where available; a manual override is available for programs that do not report it.

## Icon refresh

The interface now uses a consistent set of line icons for toolbar actions, information tabs, management tabs and context menus. Lavender primary actions, warm accent details, subtle rounded borders, emulator icon tiles and status dots give controls more definition while keeping the layout simple. Real emulator logos remain intact; missing icons use a clean monogram fallback.

Buttons keep soft hover feedback, show keyboard focus clearly, and display quieter disabled states. Navigation tabs adapt to the available width, with full labels and arrow-key selection. The artwork, desktop/taskbar icon and file names are unchanged. Emulator details still appear only after click or keyboard selection.

## Smoother selection

Switching emulators keeps the details panel open and updates it once per selection. The Folders tab loads shortcuts only when opened; folder detection and installed-version lookups run in the background. A brief checking message appears while they load. Rapid selections discard older results so they cannot replace the selected emulator's information. Notes still save before switching profiles.

Emulator artwork is cached across filtering and list refreshes. Removed profiles release their cached images; editing an emulator or returning from the manager refreshes program metadata. Action icons reuse a bounded artwork cache, and lists use double buffering. Hover and runtime-status changes repaint only affected rows, with no repaint when status is unchanged.

On this PC, a local 60-emulator fixture averaged about 3–4 ms per row selection, compared with 614 ms in the previous build. Repeated list refresh averaged about 33 ms, compared with 186 ms. Timings depend on your computer and emulator locations. The artwork, taskbar icon, file names, and emulator-only workflow are unchanged.

## Emulator management

Open the Emulators menu, or select an emulator and click Manage. The setup assistant is also available from File when your list is empty.

- Setup assistant: optional dedicated emulator folder, official project links, full ZIP package import, and existing-program registration. Defaults to Documents\FishBowl\Emulators; choose another location if preferred.
- Find installed: discover known emulator executables in that folder and register checked programs. Unrecognized Windows programs can be included for Custom registration.
- Updates: check published GitHub releases where available, read release notes, and open official downloads. Optional preview releases and a repository override work with custom emulators too.
- Versions: register separate installed builds, switch the active executable, or return to an earlier build. Files and folder overrides stay in place.
- Backups: preview and archive configuration, in-game saves, and save states as separate categories. Restore validates the archive and preserves a backup of current eligible files first.
- Setup checks: review executable availability, running status, storage folders, version information, and emulator setup guides.

The hub shows Running, Ready, Missing program, or Status unknown. Launching an already running .exe brings its window forward where possible instead of starting another copy. Missing programs can be relocated using Repair location. Wrappers and shortcuts remain supported; their underlying processes cannot always be identified.

See Emulator Management.md for storage rules, update coverage, and backup/restore details.

## Organization

Use the Favorite button or the row's context menu to favorite an emulator. Favorites sort first and appear with a star. Favorites only limits the list to them. The platform dropdown filters individual system labels, including labels supplied for custom emulators. The text filter searches emulator names, platforms, and personal notes.

## Shortcuts

Ctrl+E adds an emulator; Ctrl+F focuses the filter; F11 toggles full screen; Escape clears the filter, clears an emulator selection when the filter is empty, or leaves full screen. Right-click for opening, editing, favoriting, information editing, the program folder, and removal from FishBowl.

## Files and stored settings

The image, icon, executable filename, and Launch FishBowl.bat are unchanged. The Open emulator button is absent; double-clicking is the primary launch action. FishBowl starts the selected program with no game argument.

Profiles remain in %LOCALAPPDATA%\FishBowl\library.json, or FishBowlData beside the executable when portable.flag is present. Existing profiles, including Azahar Plus, are retained. Legacy game records remain stored for compatibility but have no import or launch interface in FishBowl. Settings saves are atomic and retain library.json.bak; unreadable settings are preserved in a recovery copy.

Preset and information sources are listed in Presets.md, Information Sources.md and Folder Routing.md. Project links open in your browser. FishBowl opens official download pages; it does not automatically download or replace emulator builds. ZIP import extracts a user-selected package into a new folder. Folder detection only reads emulator settings. Explicit backup restoration writes the matching configuration/save files after review; games are never added to the hub.

## Linux

FishBowl also runs on Linux. The Linux version (FishBowl.Avalonia) uses the same library format, presets, folder routing, backups and management tools as the Windows build, with a cross-platform Avalonia interface.

Install for your user (needs the .NET 10 SDK to build, e.g. `sudo pacman -S dotnet-sdk` on Arch):

```sh
linux/install-linux.sh      # builds a self-contained FishBowl into ~/.local/lib/fishbowl and adds a menu entry
linux/uninstall-linux.sh    # removes it again; your library is kept
```

Or run it from source with `dotnet run --project FishBowl.Avalonia`.

On Linux:

- An emulator can be a native program (e.g. /usr/bin/dolphin-emu), an AppImage, a Flatpak (its exports/bin launcher or .desktop entry), a shell script, or a .desktop entry. Emulators > Find installed detects Flatpaks, packages on your PATH, and AppImages in the emulator folder, ~/Applications, ~/AppImages and ~/Downloads.
- Setup assistant can import an AppImage (copied into its own folder and marked executable) as well as a ZIP. Extract tar.gz/tar.xz/7z packages with your usual tool.
- Folder routing follows each emulator's Linux layout: XDG folders such as ~/.config/PCSX2 and ~/.local/share/dolphin-emu, the Flatpak sandbox under ~/.var/app/<app ID>, and portable markers beside the program. See Folder Routing.md.
- Installed versions come from Flatpak metadata, the distribution package (pacman, dpkg or rpm), or the AppImage file name.
- Running status reads /proc, including Flatpak and AppImage processes. Opening an emulator that is already running focuses its window on Hyprland, Sway and X11.
- Settings, activity log and portable mode work as on Windows; the library lives in ~/.local/share/FishBowl/library.json. Tools > Add FishBowl to the application menu creates a menu entry.
- Emulator artwork comes from the emulator's .desktop entry or Flatpak icon; a custom image can still be chosen.
- Windows-only emulators (Altirra, WinUAE, Xenia) can be added with Custom through a Wine launcher script.

## Building from source

For the current Windows release, use `windows/Build.ps1` and `windows/Run Tests.ps1`. The command below builds the older retained split Windows source, not the current release.

The shared logic lives in FishBowl.Core.cs (data model, presets, folder routing, backups, updates) and FishBowl.Platform.cs (Windows/Linux differences). Both stay C# 5 compatible so the Windows build still compiles with the .NET Framework csc.exe:

```bat
csc /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png /resource:FishBowl.ico /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:FishBowl.exe FishBowl.cs FishBowl.Core.cs FishBowl.Platform.cs
```

The Linux/cross-platform front end is FishBowl.Avalonia (.NET 10), which compiles the same two shared files.
