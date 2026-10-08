# Handoff

Current development state for both maintainers. Update this file with every change; older implementation notes are in [Development history](docs/DEVELOPMENT-HISTORY.md).

_Last updated: 2026-10-08, active-game switching and shutdown._

## Current state

| Area | State |
| --- | --- |
| Windows | Source **1.29.3**, pending review in [PR #25](https://github.com/KailanBess/FishBowl/pull/25). Public release remains [v1.25.8](https://github.com/KailanBess/FishBowl/releases/tag/v1.25.8). |
| Linux | Library/Home/living-room and save/backup parity merged through PRs #28–#30. Native desktop smoke testing remains required. |
| Shared | C# 5 game, library, session, save and living-room helpers. Optional Browser/PlayView fields survive Linux saves; Linux browsing remains external. |
| Issues | [Issue #32](https://github.com/KailanBess/FishBowl/issues/32) tracks active-game switching and shutdown. [Issue #31](https://github.com/KailanBess/FishBowl/issues/31) tracks Web navigation, embedded multiplayer and external-window return. Issues #23/#27 are closed; their implementation remains under review in PR #25. |

## Open pull requests

- [#25: Windows Web, game-only Play and confirmed shutdown](https://github.com/KailanBess/FishBowl/pull/25), branch `fishbowl-web-game-view`, targets `main`. Includes main through `10a5d4e0`, the merged Linux save-parity work. Source 1.29.3 adds active-game switching and responsive in-app shutdown to the prior fixes. Web navigates the existing browser window, adds Return to Web/Play on the external-window button, and keeps multiplayer renderers in an owned Play viewport with a modeless sharing dialog. Issue #31 remains open pending review and native verification.

PRs #28–#30 are merged. Required checks must pass before merging #25. Distribute executables through Releases or passing workflow artifacts. Latest prior Copilot merge `983f0113` awaits workflow approval; the new pushed commit gets its own checks.

## Validation and compatibility

Windows regression suites, browser/process ownership fixtures and normal/compact text audits cover the automated behavior. Run `windows/Build.ps1`, `windows/Run Tests.ps1` and both modes of `windows/Run Overflow Audit.ps1` after shared-source changes. CI also compiles all retained shared sources with C# 5 and runs the Linux build, core tests and schema checks.

1.29.3 automated checks pass: the complete Windows regression run, final exit/prompt fixtures (196), native hosting (90), launch routing (20), framework/large-library audit (46), five workspaces plus 68 dialogs with zero normal/compact overflow at 100/150/200%, shared C# 5 compilation, schema preservation (229) and Linux semantic compilation. Required GitHub checks and owner review remain pending before merge. Native emulator performance and Qt accessibility-provider behavior still need live checks.

Installed Firefox has rendered inside Web, survived section changes and rendered fullscreen. 1.29.2 address-bar keyboard navigation, browser forms/downloads/permissions, normal live shutdown, Edge/Chrome versions and mixed-monitor setups still need native checks. Browser fixtures use a fixture-only navigation transport because the test desktop cannot activate a foreign window; those passes establish routing/window lifetime, not live keyboard delivery. Browser executables are installed separately; none is bundled. See [Browser setup](docs/BROWSER.md) and [Play validation](docs/PLAY-VALIDATION.md).

Actual emulator rendering/audio, physical controllers and two-device remote streaming remain native compatibility checks. The remote token service is a reference implementation and is not deployed. Linux desktop/controller testing remains required; headless or fixture passes do not establish desktop compatibility.

## Repository housekeeping

Source, scripts, tests, artwork and documentation belong in Git. Generated executables, packages, temporary output, personal libraries and game files are ignored. See [Source layout](SOURCE-LAYOUT.md).

Merged/closed branches should be deleted after checking their pull request and ancestry. Preserve active `fishbowl-web-game-view` work; merged parity branch refs have been removed on GitHub. The owner can enable **Settings → General → Pull Requests → Automatically delete head branches** to prevent future clutter.

## Log

- **2026-10-08:** Windows 1.29.3 addresses issue #32. Play defaults to owned renderer viewports so tab layout does not propagate through a foreign child window. Process ownership retains a verified lifetime handle, unchanged viewport clips are reused, renderer discovery stops rescanning stable windows after startup, and decorative motion pauses on every section while a game runs. Close games and exit waits in a responsive FishBowl dialog, acknowledges strictly recognized English plain exit confirmations, presents save/unknown prompts inside the dialog, and completes FishBowl closure when the tracked processes exit. Veto/cancel retains the game; no ordinary game process is force terminated. Added native shutdown and active-navigation fixtures. Linux has matching version metadata only; saved data is unchanged. Native emulator rendering and Qt accessibility remain compatibility checks.

- **2026-10-08:** fixed issue #31 routing and return actions, retained embedded games during multiplayer setup, added ownership/session-return fixtures and bumped paired package metadata to 1.29.2. Preserved main through PR #28 and completed the prior documentation cleanup. Native navigation/capture checks remain required.

- **2026-10-07:** audited GitHub issues, pull requests and workflow status; confirmed zero open issues and two distinct pending pull requests. Refreshed release/shutdown/browser and shared-source documentation, archived historical notes and expanded ignore rules. Copilot workflow approval and native compatibility checks remain explicit.
- **2026-10-07:** synchronized PR #25 with latest main shared game/living-room extraction, retaining `FishBowl.Browser.cs`, `FishBowl.Games.cs` and `FishBowl.LivingRoom.cs` in the Windows build.
- **2026-10-07:** PR #29 merged Library/Home/living-room work; PR #28 retains additional save/backup changes. Windows 1.29.1 browser rendering fix is tracked by closed issues #23/#27 and pending PR #25.
