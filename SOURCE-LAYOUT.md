# Source layout and Linux compatibility

`windows/` contains the complete source for the released Windows app version **1.25.8**, internally build **1.25.8.0**. Build it with `windows/Build.ps1` and run `windows/Run Tests.ps1`. This source includes all application C# files, tests, scripts and artwork. Builds create the executables beside that source. Ship executable files through GitHub Releases or CI artifacts.

The repository root `FishBowl.cs`, `FishBowl.Core.cs` and `FishBowl.Platform.cs` remain the older split source used by the Linux port. `FishBowl.Avalonia/` continues linking the root core/platform files; it must not compile the Windows monolith. **The newer Windows non-UI logic has not yet been extracted into the shared core.** The Windows source is committed as-is to make that port possible without overwriting Linux work or claiming feature parity.

Root `FishBowl.png` and `FishBowl.ico` are restored for the current Linux project and installer. Windows also has its own source-folder copies, so changing Windows artwork does not remove Linux inputs.

Linux model classes retain unknown JSON properties under NETCOREAPP. This lets Linux update known fields while retaining newer optional Windows fields, including nested fields on games, emulators, themes and collections. This is data preservation, not an implementation of the newer features. Existing JSON field names remain unchanged. The Framework/C# 5 code path is preserved.

The build workflow and existing Linux core tests were taken from PR #2 and adapted to compile the released Windows source. The PR itself has not been merged. Its optional auto-updater and UI hook were deliberately left out of this repair; review them as a separate change.

The current Windows build compiles `FishBowl.cs` and `GameRecognition.cs` as the application and `FishBowl.Setup.cs` as the installer. The older supplemental Windows source files remain for historical reference and are not compiler inputs to this build. New regression fixtures are in `windows/Tests/`; the runner also retains the existing Windows regression tests.
