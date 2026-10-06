# Source layout

FishBowl has two front ends that share their data model and emulator logic.

## Windows (`windows/`)

The Windows application, currently version **1.25.8**, is built from:

- `windows/FishBowl.cs`, `windows/FishBowl.InputPainting.cs` and `windows/FishBowl.TextFit.cs`: the WinForms application
- `FishBowl.Model.cs` and `FishBowl.GameRecognition.cs` (repository root): the shared data model and game identification

`windows/FishBowl.Setup.cs` builds the installer. Run `windows/Build.ps1` to build both executables beside the source, and `windows/Run Tests.ps1` to run the regression tests in `windows/Tests/`. Executables are distributed through GitHub Releases, not committed.

## Linux (`FishBowl.Avalonia/`)

The Avalonia application links these root files:

| File | Contents |
| --- | --- |
| `FishBowl.Model.cs` | Every class saved in `library.json`. Also compiled by the Windows build. |
| `FishBowl.GameRecognition.cs` | Game identification (titles, title IDs, icons) and installed-game lookup. Also compiled by the Windows build. |
| `FishBowl.LinuxShims.cs` | Linux stand-ins for the few Windows-only types the shared files use (`System.Drawing` images; `UserTools.Guest`). Linux only. |
| `FishBowl.Core.cs` | Presets, folder routing, discovery, imports, release checks, backups and setup checks. |
| `FishBowl.Platform.cs` | Windows/Linux differences: launching, process detection, versions, Flatpak and `.desktop` handling. |

It must not compile `windows/FishBowl.cs`. Windows behaviour reaches Linux by moving non-UI logic into shared files that both builds compile.

## Shared-file rules

- **C# 5 only:** shared files must compile with the .NET Framework C# 5 compiler. Linux-only APIs go behind `#if NETCOREAPP`.
- **Library compatibility:**
  - New `library.json` fields must be optional. Never rename or remove an existing field.
  - The Linux build adds `[JsonExtensionData]` to each model class, so fields from newer versions survive a Linux save.
- **Artwork:** `FishBowl.png` and `FishBowl.ico` at the root are used by the Linux app and installer. Windows keeps its own copies in `windows/`.
