# FishBowl for Windows

This folder contains the source of the Windows version of FishBowl (current source version **1.26.2**). See [CHANGES.md](CHANGES.md) for what's new, and the [main README](../README.md) for features and installation.

## Build

The build uses the .NET Framework C# compiler included with Windows. No Visual Studio or SDK is required.

```powershell
.\Build.ps1
```

Run `Package.ps1` after committing source to create a complete app folder, app/source ZIPs and checksums. It rebuilds from committed source and rejects mixed-version or stale bundles.

This produces `FishBowl.exe` and `FishBowl Setup.exe` in this folder. Releases are published on the repository's Releases page instead of being committed.

## Test

```powershell
& ".\Run Tests.ps1"               # regression tests in Tests\, run against a temporary portable library
& ".\Run Tests.ps1" -FullVisual   # also renders the full set of interface previews
& ".\Run Overflow Audit.ps1"      # reports text that doesn't fit, at 100%, 150% and 200% text size
& ".\Run Overflow Audit.ps1" -Compact # repeats the audit with a 1024x720 desktop
```

Run `Build.ps1` first. The tests never open or modify your real library, games or saves.

## Files

| File | Purpose |
| --- | --- |
| `FishBowl.cs` | The Windows application |
| `FishBowl.InputPainting.cs`, `FishBowl.TextFit.cs` | Painting, input and text-fitting helpers |
| `../FishBowl.GameRecognition.cs` | Game identification from local metadata, shared with the Linux version |
| `FishBowl.Setup.cs` | The installer |
| `../FishBowl.Model.cs` | The library data model, shared with the Linux version |
| `Tests\` | Regression tests |
| `OverflowAudit.cs` | Text overflow audit |
| `Uninstall FishBowl.ps1`, `.bat` | Uninstaller |

Shared files must stay compatible with C# 5. See [SOURCE-LAYOUT.md](../SOURCE-LAYOUT.md).

## Library editing and removal

Game actions → Edit game saves a library display title without changing the game file's name or content. Explicitly entered titles stay intact during later identification. Selected covers are copied into FishBowl's artwork storage and appear in both the list and artwork views. Removing the original cover file does not remove the retained copy.

Select one or more games and choose Remove game to remove their entries after confirmation. The game files and saves stay on disk. Folder scans skip removed games and their additional discs until they are explicitly added again.

The Emulators toolbar includes Remove emulator. Removing an emulator keeps its game entries and clears their assignments so they can be reassigned. Programs and saves remain on disk.

The optional `GameEntry.TitleIsCustom` and `LibraryData.RemovedGamePaths` fields belong to the shared model. `FishBowl.Library.cs` supplies shared removal and artwork-copy logic. Linux preserves these fields; matching Linux interface actions are still needed.
