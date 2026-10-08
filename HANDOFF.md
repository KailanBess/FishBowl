# Handoff

Current development state for both maintainers. Update this file with every change; older implementation notes are in [Development history](docs/DEVELOPMENT-HISTORY.md).

_Last updated: 2026-10-08, browser viewport fitting._

## Current state

| Area | State |
| --- | --- |
| Windows | Source **1.29.4**, browser fitting on branch `fishbowl-browser-auto-fit`. PR #25 is merged; source includes main through `771e409`. |
| Linux | Library/Home/living-room and save/backup parity merged through PRs #28–#30. Round 2 in progress (see Linux work). |
| Shared | C# 5 game, library, session, save and living-room helpers. Optional Browser/PlayView fields survive Linux saves; Linux browsing remains external. |
| Issues | [Issue #34](https://github.com/KailanBess/FishBowl/issues/34) tracks automatic browser fitting. Issues #23/#27/#31/#32 are implemented through merged PR #25. |

## Open pull requests

- Browser viewport fitting, branch `fishbowl-browser-auto-fit`, targets `main`. PR #25 and #33 are merged. Paired version metadata is 1.29.4 because the Build workflow reads its expected Windows package version from the Linux project; Linux UI and active parity work are otherwise unchanged.

Required checks must pass before merging. Distribute executables through Releases or passing workflow artifacts.
## Linux work

**In progress (round 2):** branches `parity/profiles-2` (profiles, launch profiles with Proton/GameMode/MangoHud/Gamescope, controller profiles, multiplayer, settings, themes), `parity/integrations-2` (Flatpak permissions and updates, AppImage updates, Add to Steam, EmuDeck/RetroDECK, AUR package) and `parity/qa-2` (every Linux screen and dialog checked and fixed). **Windows-side agents: please avoid changing `FishBowl.Avalonia/` until these merge;** Windows work and shared-model additions are fine.

**Round 1 (#28–#30):** shared `FishBowl.Games.cs` (library queries, collections, lists, import, launch/sessions via `GamePlay`), `FishBowl.SaveTools.cs` (save history; Windows keeps its emulator rules through partial-method hooks; both apps use `Game Saves/<id>/<kind>/History`) and `FishBowl.LivingRoom.cs` (Immersion logic and living-room navigation; Windows `Immersion` calls it). Linux controllers use evdev, falling back to `/dev/input/js*`.

**Still missing on Linux:** game details dialog (`GameExperienceDialog`), aquarium bubble rails, ambient artwork on the main window, Home "Save history"/"Readiness" quick actions, embedded Play/Web (Linux launches emulators and browsers as separate windows). Possible follow-up: Windows MainForm could use the shared `SaveMonitor` rules.

## Validation and compatibility

Windows regression suites, browser/process ownership fixtures and normal/compact text audits cover the automated behavior. Run `windows/Build.ps1`, `windows/Run Tests.ps1` and both modes of `windows/Run Overflow Audit.ps1` after shared-source changes. CI also compiles all retained shared sources with C# 5 and runs the Linux build, core tests and schema checks.

Windows 1.29.4 targeted checks pass: browser routing/geometry (40), game exit (196), hosting (90), launch (20), smooth UI (667) and framework/large-library audit (46). Normal and compact text audits report zero findings across five workspaces and 68 dialogs at 100/150/200%. Live browser and monitor compatibility checks remain open.

1.29.3 automated checks pass: the complete Windows regression run, final exit/prompt fixtures (196), native hosting (90), launch routing (20), framework/large-library audit (46), five workspaces plus 68 dialogs with zero normal/compact overflow at 100/150/200%, shared C# 5 compilation, schema preservation (229) and Linux semantic compilation. PR #25 is merged and 1.29.3 is released. Native emulator performance and Qt accessibility-provider behavior still need live checks.

Installed Firefox has rendered inside Web, survived section changes and rendered fullscreen. 1.29.2 address-bar keyboard navigation, browser forms/downloads/permissions, normal live shutdown, Edge/Chrome versions and mixed-monitor setups still need native checks. Browser fixtures use a fixture-only navigation transport because the test desktop cannot activate a foreign window; those passes establish routing/window lifetime, not live keyboard delivery. Browser executables are installed separately; none is bundled. See [Browser setup](docs/BROWSER.md) and [Play validation](docs/PLAY-VALIDATION.md).

Actual emulator rendering/audio, physical controllers and two-device remote streaming remain native compatibility checks. The remote token service is a reference implementation and is not deployed. Linux desktop/controller testing remains required; headless or fixture passes do not establish desktop compatibility.

## Repository housekeeping

Source, scripts, tests, artwork and documentation belong in Git. Generated executables, packages, temporary output, personal libraries and game files are ignored. See [Source layout](SOURCE-LAYOUT.md).

Merged/closed branches should be deleted after checking their pull request and ancestry. Preserve active `fishbowl-browser-auto-fit` work; merged parity branch refs have been removed on GitHub. The owner can enable **Settings → General → Pull Requests → Automatically delete head branches** to prevent future clutter.

## Log

- **2026-10-08:** Windows 1.29.4 addresses issue #34. Browser viewports verify native geometry and restore browser-native movement, resizing and maximization to the Web area. Open in window starts fitted over Web, then respects user adjustments; Return to Web resumes fitting. Unsupported attachment receives an initial external fit. Browser fixtures cover automatic fit, native geometry reset, app move/resize, idle placement stability and external positioning. Updated the Linux version only to satisfy the paired CI package contract; Linux parity implementations are untouched. Based on merged main through #25/#33.

- **2026-10-08 (Linux):** summarized Linux parity round 1 and started round 2 (see Linux work). The Build workflow now takes the expected package version from `FishBowl.Avalonia.csproj` instead of a hardcoded number (it failed on main because it still expected 1.29.2).

- **2026-10-08:** Windows 1.29.3 addresses issue #32. Play defaults to owned renderer viewports so tab layout does not propagate through a foreign child window. Process ownership retains a verified lifetime handle, unchanged viewport clips are reused, renderer discovery stops rescanning stable windows after startup, and decorative motion pauses on every section while a game runs. Close games and exit waits in a responsive FishBowl dialog, acknowledges strictly recognized English plain exit confirmations, presents save/unknown prompts inside the dialog, and completes FishBowl closure when the tracked processes exit. Veto/cancel retains the game; no ordinary game process is force terminated. Added native shutdown and active-navigation fixtures. Linux has matching version metadata only; saved data is unchanged. Native emulator rendering and Qt accessibility remain compatibility checks.

- **2026-10-08:** fixed issue #31 routing and return actions, retained embedded games during multiplayer setup, added ownership/session-return fixtures and bumped paired package metadata to 1.29.2. Preserved main through PR #28 and completed the prior documentation cleanup. Native navigation/capture checks remain required.

- **2026-10-07:** audited GitHub issues, pull requests and workflow status; confirmed zero open issues and two distinct pending pull requests. Refreshed release/shutdown/browser and shared-source documentation, archived historical notes and expanded ignore rules. Copilot workflow approval and native compatibility checks remain explicit.
- **2026-10-07:** synchronized PR #25 with latest main shared game/living-room extraction, retaining `FishBowl.Browser.cs`, `FishBowl.Games.cs` and `FishBowl.LivingRoom.cs` in the Windows build.
- **2026-10-07:** PR #29 merged Library/Home/living-room work; PR #28 retains additional save/backup changes. Windows 1.29.1 browser rendering fix is tracked by closed issues #23/#27 and pending PR #25.
