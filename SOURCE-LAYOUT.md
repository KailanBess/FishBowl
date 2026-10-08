# Source layout

FishBowl has two front ends that share their data model and emulator logic.

## Windows (`windows/`)

The Windows application is built from:

- `windows/FishBowl.cs`, `windows/FishBowl.InputPainting.cs` and `windows/FishBowl.TextFit.cs`: the WinForms application
- `FishBowl.Model.cs` and `FishBowl.GameRecognition.cs` (repository root): the shared data model and game identification
- `windows/FishBowl.Navigation.cs`, `windows/FishBowl.Sessions.cs` and `windows/FishBowl.RemotePlay.cs`: unified appearance/controller navigation, session UI tracking and the local browser/input bridge
- `windows/FishBowl.Browser.cs`: installed Firefox/Edge/Chrome discovery, dedicated profiles, Web navigation and exact-owned browser window lifetime
- `windows/Play.cs`: exact-process native child/owned-window hosting, persistent Play sessions and safe detach/restore
- `remote-play/client/`: embedded browser media client and the pinned LiveKit SDK, including its license

`windows/FishBowl.Setup.cs` builds the installer. Run `windows/Build.ps1` to build both executables beside the source, and `windows/Run Tests.ps1` to run the regression tests in `windows/Tests/`. Executables are distributed through GitHub Releases, not committed.

## Linux (`FishBowl.Avalonia/`)

The Avalonia application links these root files:

| File | Contents |
| --- | --- |
| `FishBowl.Model.cs` | Every class saved in `library.json`. Also compiled by the Windows build. |
| `FishBowl.GameRecognition.cs` | Game identification (titles, title IDs, icons) and installed-game lookup. Also compiled by the Windows build. |
| `FishBowl.Library.cs`, `FishBowl.Games.cs` | Library edits, filters, collections, queue and game launching. Compiled by both platforms. |
| `FishBowl.SaveTools.cs`, `FishBowl.LivingRoom.cs`, `FishBowl.Sessions.cs` | Save recovery, living-room behavior and verified session journals. Compiled by both platforms. |
| `FishBowl.GameTools.cs`, `FishBowl.Integrations.cs` | Shared import/mod/media/catalog/companion helpers. Compiled by both platforms. |
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

Shared expansion helpers: `FishBowl.GameTools.cs` (imports, mods, media and history), `FishBowl.Integrations.cs` (catalogs, achievements and companion) , `FishBowl.SaveTools.cs` (verified file save snapshots) and `FishBowl.LivingRoom.cs` (Immersion settings, shelves, presets, session recaps and living-room grid navigation). Both platforms compile these helpers.

`FishBowl.Sessions.cs` supplies verified process-tree identities and durable, profile-owned journals on both platforms. `FishBowl.Avalonia/LinuxJoystick.cs` decodes existing Linux joystick devices; platform session/controller screens stay in Avalonia. `remote-play/server.mjs` is a separate, private reference token service, not an automatically deployed part of the desktop application. See [remote-play setup](remote-play/README.md).
