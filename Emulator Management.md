# FishBowl emulator management

This hub opens emulators. Game installation, game lists, graphics, controller mappings, BIOS setup, and gameplay remain inside each emulator. Any Windows emulator or frontend can be registered through Custom using an .exe, .bat, .cmd, or .lnk. The 3DS preset is Azahar Plus; a generic azahar.exe is deliberately not classified as Plus.

## A dedicated place for emulator packages

The optional default is Documents\FishBowl\Emulators. Choose any suitable folder in Setup assistant. FishBowl does not move existing installations or force portable mode. An installer-based emulator can stay in its normal location. Keep each portable package together in its own subfolder, such as PCSX2\stable or Azahar Plus\2126.1-B. Configure portable mode inside each emulator according to its official instructions.

1. Open Emulators > Setup assistant / emulator folder.
2. Choose an optional preset and open its official download page. Custom can supply a project URL.
3. For a ZIP, use Import emulator ZIP and give the extracted folder a name. The complete package is extracted into a new unique folder, including resources and libraries. Select its main executable from the package results.
4. For 7z/RAR or an installer, use your normal extractor/installer, then Browse for the emulator program.
5. Enter a display name and click Add to FishBowl. To attach the package to the selected profile as another version, choose Register as another build instead, then activate it on Versions.

ZIP extraction rejects path traversal, Windows device names, alternate streams, archive links and duplicate paths. Limits are 50,000 entries, 2 GB per entry and 4 GB total. Oversized packages need external extraction followed by Browse. Existing directories are not overwritten. A cancelled/failed import removes its staging directory.

## Find installed emulators

Find installed searches the chosen emulator folder up to four subfolder levels, with directory/file-count limits. It skips game/storage directories and filesystem links. Known executable names are matched to optional presets; Also show unrecognized .exe programs includes custom candidates. Review the results and add the checked programs. Helper tools and installers can appear among custom candidates, so choose the main program.

To locate emulators elsewhere, choose that folder as the emulator root or use Add emulator/Browse. FishBowl does not search the whole drive, detect installed games, or move emulator files.

## Updates and release notes

Updates uses the project's public GitHub Releases API when a preset has a GitHub release source. Custom profiles may supply owner/repository. A blank override uses the preset's source. Stable checks use the latest published non-preview release; the preview option examines the newest ten published releases and includes previews, while ignoring drafts. Some projects publish their development builds elsewhere, so the checkbox cannot cover every nightly channel.

The current Windows version metadata or a manual version override is compared with the release name/tag. Non-numeric versions, unavailable metadata, and ambiguous suffixes ask you to compare manually. This is release information, not a compatibility guarantee. Cached release notes show their check time; a failed request never establishes that your installation is up to date. Internet access and GitHub API availability/rate limits affect live checks.

Projects without a supported feed use Official downloads. FishBowl does not automatically download, install, or replace an emulator. Download the new build from the official project, extract/install it separately, and register it under Versions.

API reference: https://docs.github.com/en/rest/releases/releases?apiVersion=2022-11-28

## Installed builds and rollback

Versions records executable locations and optional labels. Register installed build adds another existing program to the selected profile. Use selected build changes the program FishBowl launches. To roll back, select the earlier installation. Forget selected entry removes only the inactive registration, never its files.

Keep old build directories until you know the new version works. Manual version information is remembered with each build. Profile notes, favorite status, information and explicit folder overrides remain attached to the emulator profile. Automatically detected folders are recalculated for its active executable. Review Folders after switching, especially when different builds use different portable/user-directory modes. Save states may be incompatible between emulator versions; FishBowl cannot convert them.

## Configuration, in-game saves and save-state backups

The default backup destination is Documents\FishBowl\Backups, or your configured backup folder. Choose a destination outside the source directories. Nothing is backed up on a schedule by these tools; backups and restores are explicit actions.

Close the emulator. On Backups, select Configuration, In-game saves, and/or Save states, then Preview backup. The preview shows source paths, warnings, file counts, size, and the first 500 included files. Review it before Create backup. Every included file is listed in the archive manifest with a SHA-256 checksum. No eligible files means no archive is created.

Configuration includes supported settings-file extensions, rather than every file beside the emulator. Save categories use the detected locations or your manual overrides. Known broad 3DS SD/NAND and Wii title containers are narrowed to data/extdata directories. Executables, libraries, archive packages, known game/ROM formats, BIOS filenames and game/content/cache/firmware directories are excluded. Ambiguous .bin files require a recognizable save/data-directory component. Filesystem links are skipped. Limits are 50,000 files, 2 GB per file and 4 GB total.

This is a targeted backup, not a full emulator-directory image. Unusual/custom layouts may need narrower overrides; check that your important save files appear in the preview. Do not point a manual save override at a general game-download folder. Games are not catalogued or added to FishBowl.

Restore ZIP accepts a FishBowl emulator backup for the same profile. Review the archive, destinations and included files before restoring. Current detected/manual paths must match the backup's destinations; if you changed builds or overrides, select the original build/folder first. The archive paths, file types and all checksums are validated before any destination file is overwritten. Current eligible files are backed up into a Before restore archive first. Matching files are then replaced individually; unrelated files remain. Restoration is not one filesystem transaction: an interruption can leave a partial restore, and the error identifies the preserved pre-restore backup when available.

FishBowl blocks backup/restore and build switching while the selected .exe is detected running. For wrappers, shortcuts, or inaccessible process details, status can be Unknown: close the actual emulator yourself before using these tools. Save-folder shortcuts and automatic detection do not alter emulator settings; a reviewed restore does write configuration/save files.

## Setup checks and location repair

Setup checks reports program existence, launcher format, running status, installed-version information, dedicated-folder availability and each detected/manual folder. A missing save folder can simply mean the emulator has not created it yet. Choose firmware folder records a reference to the folder already configured in your emulator and checks availability. It does not install firmware, set the emulator's BIOS path, validate firmware contents, benchmark your GPU, or configure controllers. Use the official setup guide for those checks.

Repair location selects the new main executable/shortcut when an installation moved. Notes, folder overrides and other profile information stay attached. The former location remains registered as a build; the manual version override is cleared for the new location. A double-click on a missing program also offers location repair.

Running status compares process executable paths, so another program with the same filename in a different directory does not count as the selected emulator. Double-clicking a detected running emulator focuses its window if Windows permits. Wrapper/shortcut status and inaccessible processes may be Unknown.

## Update contents and verification

FishBowl.cs, FishBowl.exe, FishBowl.ico, FishBowl.png and Launch FishBowl.bat retain their names. The original image and icon are unchanged, including the executable's embedded icon. The Open emulator button remains removed; double left-click and Enter remain the primary launch actions. Existing normal-mode profiles use the same %LOCALAPPDATA%\FishBowl storage. For portable mode, copy your existing FishBowlData and portable.flag beside this executable if you used them previously.

Verified with isolated fixtures: ZIP-package/resource preservation and invalid-path rejection, discovery, build switching/repair, stable/preview release responses, checksummed backup/restore and pre-restore archives, corrupt-archive rejection, custom registration, real process-path detection/duplicate prevention, UI background actions, save routing, profile persistence, launch controls and unchanged icon/image resources. Live release lookup could not be verified in the development environment because outbound connection was unavailable; deterministic feed-response and failure checks passed.


## Selection and soft icon highlights

Emulator details are hidden until you click/select a row or use keyboard navigation. Clearing selection hides the panel and gives the list the full available width. Filtering does not automatically select an emulator. Icon hover feedback remains separate from selection and does not reveal details. Toolbar/folder actions and menu/logo/emulator icons use subtle light hover feedback; the existing selected-row marker is retained. The native list columns are refitted whenever the details panel opens or closes.

The updated build also passed 21 selection/highlight checks, launch/icon/settings regressions, save-routing UI checks, real running-process/duplicate-launch checks, 58 management checks, and 14 background UI checks. Tests use isolated fixtures rather than changing user emulator data.
