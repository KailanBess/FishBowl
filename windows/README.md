# FishBowl 1.25.8 source

Editable source for the build whose startup popup fixes were confirmed working by the project owner. This upload package contains application and installer source, embedded resources, build scripts and regression tests.

## Build on Windows

Extract the complete folder, open PowerShell in it, and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\Build.ps1"
```

The script uses the Windows .NET Framework 4 compiler and produces `FishBowl.exe` and `FishBowl Setup.exe`. It requires no NuGet packages. Keep the generated executables, `FishBowl.ico`, the uninstall scripts and this README together when distributing the app.

## Validate

After building, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\Run Tests.ps1"
```

Use `-FullVisual` for the larger preview matrix. Tests create a separate portable fixture in the temporary directory. The repository validation passed 2,587 checks: 1,543 current-build checks plus 1,044 retained Windows regression checks. It also rendered 62 previews without failures. The test runner retains the older top-level Windows test sources alongside the newer fixtures in `Tests/`.

## Files

- `FishBowl.cs`: application source, including startup dialog appearance and animation fixes.
- `GameRecognition.cs`: local game identification, icon persistence and installed-game launch helpers.
- `FishBowl.Setup.cs`: installer source.
- `FishBowl.png` and `FishBowl.ico`: embedded application resources.
- `Build.ps1`, `Run Tests.ps1` and `Tests/`: build and validation tools.
- `Uninstall FishBowl.bat` and `Uninstall FishBowl.ps1`: uninstall scripts.
- `CHANGES.md`: detailed changes and validation notes.

## Source provenance

The application baseline was recovered from the project owner's supplied executable using ILSpy 7.2, then edited and rebuilt. Original comments, local variable names and file organization could not be recovered. All 2,063 baseline named method signatures remain in the updated assembly.

This package contains source and project resources. User libraries, game files, emulator files, saved games, runtime traces and screen recordings are not included.
