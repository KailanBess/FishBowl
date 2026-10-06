# FishBowl for Windows

This folder contains the source of the Windows version of FishBowl (current version **1.25.8**). See [CHANGES.md](CHANGES.md) for what's new, and the [main README](../README.md) for features and installation.

## Build

The build uses the .NET Framework C# compiler included with Windows. No Visual Studio or SDK is required.

```powershell
.\Build.ps1
```

This produces `FishBowl.exe` and `FishBowl Setup.exe` in this folder. Releases are published on the repository's Releases page instead of being committed.

## Test

```powershell
& ".\Run Tests.ps1"               # regression tests in Tests\, run against a temporary portable library
& ".\Run Tests.ps1" -FullVisual   # also renders the full set of interface previews
& ".\Run Overflow Audit.ps1"      # reports text that doesn't fit, at 100%, 150% and 200% text size
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
